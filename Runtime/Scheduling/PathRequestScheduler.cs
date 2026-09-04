using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Unity.Profiling;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace SparkyGames.Pathfinder
{
    /// <summary>
    /// Central main-thread scheduler for synchronous path queries. The configured
    /// budget is enforced between queries; one individual A* search is not preempted.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-90)]
    [AddComponentMenu("Sparky Games/Pathfinder/Path Request Scheduler")]
    public sealed class PathRequestScheduler : MonoBehaviour, IPathRequestScheduler
    {
        private const int PriorityCount = 4;
        private const int TimingSampleCapacity = 128;

        private static readonly ProfilerMarker ProcessFrameMarker =
            new ProfilerMarker("SparkyGames.Pathfinder.Scheduler.ProcessFrame");
        private static readonly ProfilerMarker ProcessRequestMarker =
            new ProfilerMarker("SparkyGames.Pathfinder.Scheduler.ProcessRequest");
        private static readonly ProfilerMarker CacheLookupMarker =
            new ProfilerMarker("SparkyGames.Pathfinder.Scheduler.CacheLookup");
        private static readonly ProfilerMarker FindPathMarker =
            new ProfilerMarker("SparkyGames.Pathfinder.Scheduler.FindPath");
        private static readonly ProfilerMarker CacheStoreMarker =
            new ProfilerMarker("SparkyGames.Pathfinder.Scheduler.CacheStore");

        private sealed class QueuedRequest
        {
            public PathRequestHandle Handle;
            public IPathfinding Pathfinder;
            public Vector3 Start;
            public Vector3 Destination;
            public PathQueryOptions Options;
            public Action<PathRequestHandle, PathResult> Completed;
            public LinkedListNode<QueuedRequest> QueueNode;
            public long EnqueuedTimestamp;
            public int EnqueuedFrame;
            public bool PriorityWasAged;
        }

        private readonly struct PathCacheKey : IEquatable<PathCacheKey>
        {
            private readonly IPathfinding _pathfinder;
            private readonly Vector3 _start;
            private readonly Vector3 _destination;
            private readonly bool _allowDiagonal;
            private readonly bool _preventCornerCutting;
            private readonly bool _findNearest;
            private readonly bool _smooth;
            private readonly int _maxExpandedNodes;
            private readonly float _agentRadius;
            private readonly long _gridVersion;

            public PathCacheKey(
                IPathfinding pathfinder,
                Vector3 start,
                Vector3 destination,
                PathQueryOptions options,
                long gridVersion)
            {
                _pathfinder = pathfinder;
                _start = start;
                _destination = destination;
                _allowDiagonal = options.AllowDiagonalMovement;
                _preventCornerCutting = options.PreventCornerCutting;
                _findNearest = options.FindNearestReachableDestination;
                _smooth = options.SmoothPath;
                _maxExpandedNodes = options.MaxExpandedNodes;
                _agentRadius = options.AgentProfile.GetSanitizedRadius();
                _gridVersion = gridVersion;
            }

            public bool Equals(PathCacheKey other) =>
                ReferenceEquals(_pathfinder, other._pathfinder) &&
                _start.Equals(other._start) &&
                _destination.Equals(other._destination) &&
                _allowDiagonal == other._allowDiagonal &&
                _preventCornerCutting == other._preventCornerCutting &&
                _findNearest == other._findNearest &&
                _smooth == other._smooth &&
                _maxExpandedNodes == other._maxExpandedNodes &&
                _agentRadius.Equals(other._agentRadius) &&
                _gridVersion == other._gridVersion;

            public override bool Equals(object obj) =>
                obj is PathCacheKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = RuntimeHelpers.GetHashCode(_pathfinder);
                    hash = hash * 397 ^ _start.GetHashCode();
                    hash = hash * 397 ^ _destination.GetHashCode();
                    hash = hash * 397 ^ _allowDiagonal.GetHashCode();
                    hash = hash * 397 ^ _preventCornerCutting.GetHashCode();
                    hash = hash * 397 ^ _findNearest.GetHashCode();
                    hash = hash * 397 ^ _smooth.GetHashCode();
                    hash = hash * 397 ^ _maxExpandedNodes;
                    hash = hash * 397 ^ _agentRadius.GetHashCode();
                    hash = hash * 397 ^ _gridVersion.GetHashCode();
                    return hash;
                }
            }
        }

        private sealed class CacheEntry
        {
            public PathResult Result;
            public int StoredFrame;
        }

        private readonly LinkedList<QueuedRequest>[] _queues =
        {
            new LinkedList<QueuedRequest>(),
            new LinkedList<QueuedRequest>(),
            new LinkedList<QueuedRequest>(),
            new LinkedList<QueuedRequest>()
        };

        private readonly Dictionary<long, QueuedRequest> _queuedById =
            new Dictionary<long, QueuedRequest>();
        private readonly Dictionary<PathCacheKey, CacheEntry> _resultCache =
            new Dictionary<PathCacheKey, CacheEntry>();
        private readonly double[] _executionSamples =
            new double[TimingSampleCapacity];

        [SerializeField]
        [Min(1)]
        private int maxRequestsPerFrame = 4;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Soft time budget checked between requests. Zero disables the time limit.")]
        private float maxMillisecondsPerFrame = 2f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Seconds before a queued request is promoted by one priority level. Zero disables aging.")]
        private float priorityAgingSeconds = 0.5f;

        [SerializeField]
        private bool enableExactQueryCache = true;

        [SerializeField]
        [Min(0)]
        [Tooltip("Frames for which an exact query result may be reused while the grid version remains unchanged.")]
        private int cacheLifetimeFrames = 2;

        [SerializeField]
        [Min(1)]
        private int maximumCacheEntries = 64;

        private long _nextRequestId;
        private int _executionSampleCount;
        private int _nextExecutionSample;
        private double _totalExecutionMilliseconds;

        public int PendingCount => _queuedById.Count;

        public bool IsProcessing { get; private set; }

        public int LastFrameProcessedCount { get; private set; }

        public double LastFrameElapsedMilliseconds { get; private set; }

        public long TotalCompletedCount { get; private set; }

        public long TotalCancelledCount { get; private set; }

        public long TotalCacheHitCount { get; private set; }

        public long TotalCacheMissCount { get; private set; }

        public long TotalAgedRequestCount { get; private set; }

        public double AverageExecutionMilliseconds =>
            TotalCompletedCount > 0
                ? _totalExecutionMilliseconds / TotalCompletedCount
                : 0d;

        public double MaximumExecutionMilliseconds { get; private set; }

        public double MaximumQueueWaitMilliseconds { get; private set; }

        public int MaxRequestsPerFrame => maxRequestsPerFrame;

        public float MaxMillisecondsPerFrame => maxMillisecondsPerFrame;

        private void OnEnable()
        {
            LastFrameProcessedCount = 0;
            LastFrameElapsedMilliseconds = 0d;
        }

        private void OnDisable()
        {
            CancelAll();
            _resultCache.Clear();
            IsProcessing = false;
        }

        private void OnValidate()
        {
            maxRequestsPerFrame = Mathf.Max(1, maxRequestsPerFrame);
            maxMillisecondsPerFrame = Mathf.Max(0f, maxMillisecondsPerFrame);
            priorityAgingSeconds = Mathf.Max(0f, priorityAgingSeconds);
            cacheLifetimeFrames = Mathf.Max(0, cacheLifetimeFrames);
            maximumCacheEntries = Mathf.Max(1, maximumCacheEntries);
        }

        private void Update() => ProcessFrame();

        /// <summary>
        /// Changes the soft per-frame budget at runtime.
        /// </summary>
        public void SetFrameBudget(int requestCount, float milliseconds)
        {
            maxRequestsPerFrame = Mathf.Max(1, requestCount);
            maxMillisecondsPerFrame = Mathf.Max(0f, milliseconds);
        }

        public PathRequestHandle Enqueue(
            IPathfinding pathfinder,
            Vector3 startWorldPosition,
            Vector3 endWorldPosition,
            PathQueryOptions options = null,
            PathRequestPriority priority = PathRequestPriority.Normal,
            Action<PathRequestHandle, PathResult> completed = null)
        {
            if (pathfinder == null)
            {
                throw new ArgumentNullException(nameof(pathfinder));
            }

            if (!isActiveAndEnabled)
            {
                throw new InvalidOperationException(
                    "PathRequestScheduler must be active and enabled before accepting requests.");
            }

            priority = SanitizePriority(priority);
            var handle = new PathRequestHandle(
                NextRequestId(),
                priority,
                Cancel);
            var request = new QueuedRequest
            {
                Handle = handle,
                Pathfinder = pathfinder,
                Start = startWorldPosition,
                Destination = endWorldPosition,
                Options = options?.Clone() ?? PathQueryOptions.Default,
                Completed = completed,
                EnqueuedTimestamp = Stopwatch.GetTimestamp(),
                EnqueuedFrame = Time.frameCount
            };

            request.QueueNode = _queues[(int)priority].AddLast(request);
            _queuedById.Add(handle.RequestId, request);
            return handle;
        }

        public bool Cancel(PathRequestHandle request)
        {
            if (request == null ||
                !_queuedById.TryGetValue(request.RequestId, out var queued) ||
                !ReferenceEquals(request, queued.Handle))
            {
                return false;
            }

            _queuedById.Remove(request.RequestId);
            if (queued.QueueNode?.List != null)
            {
                queued.QueueNode.List.Remove(queued.QueueNode);
            }

            queued.QueueNode = null;
            var result = PathResult.CreateFailure(
                PathStatus.Cancelled,
                queued.Destination);
            var queueMilliseconds = ElapsedMilliseconds(queued.EnqueuedTimestamp);
            request.Complete(
                result,
                true,
                new PathRequestMetrics(
                    queueMilliseconds,
                    0d,
                    0d,
                    queued.EnqueuedFrame,
                    Time.frameCount,
                    false,
                    queued.PriorityWasAged));
            MaximumQueueWaitMilliseconds = Math.Max(
                MaximumQueueWaitMilliseconds,
                queueMilliseconds);
            TotalCancelledCount++;
            InvokeCompletion(queued, result);
            return true;
        }

        public int CancelAll()
        {
            if (_queuedById.Count == 0)
            {
                return 0;
            }

            // Completion handlers may enqueue or cancel other requests, so operate
            // on the set that existed when cancellation began.
            var snapshot = new List<QueuedRequest>(_queuedById.Values);
            var cancelled = 0;
            for (var i = 0; i < snapshot.Count; i++)
            {
                if (Cancel(snapshot[i].Handle))
                {
                    cancelled++;
                }
            }

            return cancelled;
        }

        /// <summary>
        /// Processes one frame's allowance. Public to support deterministic manual
        /// driving; normal scene usage relies on Update.
        /// </summary>
        public int ProcessFrame()
        {
            using var marker = ProcessFrameMarker.Auto();
            LastFrameProcessedCount = 0;
            LastFrameElapsedMilliseconds = 0d;
            if (IsProcessing || !isActiveAndEnabled || PendingCount == 0)
            {
                return 0;
            }

            IsProcessing = true;
            var stopwatch = Stopwatch.StartNew();
            try
            {
                while (LastFrameProcessedCount < maxRequestsPerFrame)
                {
                    if (LastFrameProcessedCount > 0 &&
                        maxMillisecondsPerFrame > 0f &&
                        stopwatch.Elapsed.TotalMilliseconds >= maxMillisecondsPerFrame)
                    {
                        break;
                    }

                    var request = DequeueNextActive();
                    if (request == null)
                    {
                        break;
                    }

                    Process(request);
                    LastFrameProcessedCount++;
                }
            }
            finally
            {
                stopwatch.Stop();
                LastFrameElapsedMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
                IsProcessing = false;
            }

            return LastFrameProcessedCount;
        }

        private void Process(QueuedRequest request)
        {
            using var marker = ProcessRequestMarker.Auto();
            request.Handle.MarkRunning();
            var queueMilliseconds = ElapsedMilliseconds(request.EnqueuedTimestamp);
            MaximumQueueWaitMilliseconds = Math.Max(
                MaximumQueueWaitMilliseconds,
                queueMilliseconds);
            var startedTimestamp = Stopwatch.GetTimestamp();
            PathResult result;
            var cacheHit = false;
            var pathfindingMilliseconds = 0d;
            try
            {
                var cacheFound = false;
                using (CacheLookupMarker.Auto())
                {
                    cacheFound = TryGetCachedResult(request, out result);
                }

                if (!cacheFound)
                {
                    var pathfindingStartedTimestamp = Stopwatch.GetTimestamp();
                    using (FindPathMarker.Auto())
                    {
                        result = request.Pathfinder.FindPath(
                            request.Start,
                            request.Destination,
                            request.Options);
                    }

                    pathfindingMilliseconds = ElapsedMilliseconds(
                        pathfindingStartedTimestamp);
                    using (CacheStoreMarker.Auto())
                    {
                        TryStoreCachedResult(request, result);
                    }
                }
                else
                {
                    cacheHit = true;
                }

                if (result == null)
                {
                    result = PathResult.CreateFailure(
                        PathStatus.InvalidConfiguration,
                        request.Destination);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                result = PathResult.CreateFailure(
                    PathStatus.InvalidConfiguration,
                    request.Destination);
            }

            var executionMilliseconds = ElapsedMilliseconds(startedTimestamp);
            RecordExecutionTime(executionMilliseconds);
            request.Handle.Complete(
                result,
                false,
                new PathRequestMetrics(
                    queueMilliseconds,
                    executionMilliseconds,
                    pathfindingMilliseconds,
                    request.EnqueuedFrame,
                    Time.frameCount,
                    cacheHit,
                    request.PriorityWasAged));
            TotalCompletedCount++;
            InvokeCompletion(request, result);
        }

        private QueuedRequest DequeueNextActive()
        {
            QueuedRequest selected = null;
            var selectedEffectivePriority = -1;
            for (var priority = PriorityCount - 1; priority >= 0; priority--)
            {
                var queue = _queues[priority];
                while (queue.Count > 0 &&
                       !_queuedById.ContainsKey(queue.First.Value.Handle.RequestId))
                {
                    queue.RemoveFirst();
                }

                if (queue.Count == 0)
                {
                    continue;
                }

                var candidate = queue.First.Value;
                var effectivePriority = GetEffectivePriority(candidate);
                if (selected == null ||
                    effectivePriority > selectedEffectivePriority ||
                    effectivePriority == selectedEffectivePriority &&
                    candidate.EnqueuedTimestamp < selected.EnqueuedTimestamp)
                {
                    selected = candidate;
                    selectedEffectivePriority = effectivePriority;
                }
            }

            if (selected == null)
            {
                return null;
            }

            selected.QueueNode.List.Remove(selected.QueueNode);
            selected.QueueNode = null;
            _queuedById.Remove(selected.Handle.RequestId);
            selected.PriorityWasAged =
                selectedEffectivePriority > (int)selected.Handle.Priority;
            if (selected.PriorityWasAged)
            {
                TotalAgedRequestCount++;
            }

            return selected;
        }

        /// <summary>
        /// Calculates the rolling 95th percentile over the most recent completed
        /// requests. Intended for diagnostics rather than per-frame UI polling.
        /// </summary>
        public double GetExecutionPercentile95Milliseconds()
        {
            if (_executionSampleCount == 0)
            {
                return 0d;
            }

            var copy = new double[_executionSampleCount];
            Array.Copy(_executionSamples, copy, _executionSampleCount);
            Array.Sort(copy);
            var index = Mathf.Clamp(
                Mathf.CeilToInt(copy.Length * 0.95f) - 1,
                0,
                copy.Length - 1);
            return copy[index];
        }

        private int GetEffectivePriority(QueuedRequest request)
        {
            var priority = (int)request.Handle.Priority;
            if (priorityAgingSeconds <= 0f || priority >= PriorityCount - 1)
            {
                return priority;
            }

            var waitedSeconds = ElapsedMilliseconds(request.EnqueuedTimestamp) / 1000d;
            var promotions = (int)(waitedSeconds / priorityAgingSeconds);
            return Math.Min(PriorityCount - 1, priority + promotions);
        }

        private bool TryGetCachedResult(
            QueuedRequest request,
            out PathResult result)
        {
            result = null;
            if (!TryCreateCacheKey(request, out var key))
            {
                return false;
            }

            if (_resultCache.TryGetValue(key, out var entry) &&
                Time.frameCount - entry.StoredFrame <= cacheLifetimeFrames)
            {
                result = entry.Result;
                TotalCacheHitCount++;
                return true;
            }

            if (entry != null)
            {
                _resultCache.Remove(key);
            }

            TotalCacheMissCount++;
            return false;
        }

        private void TryStoreCachedResult(QueuedRequest request, PathResult result)
        {
            if (result == null || !TryCreateCacheKey(request, out var key))
            {
                return;
            }

            if (_resultCache.Count >= maximumCacheEntries &&
                !_resultCache.ContainsKey(key))
            {
                RemoveOldestCacheEntry();
            }

            _resultCache[key] = new CacheEntry
            {
                Result = result,
                StoredFrame = Time.frameCount
            };
        }

        private bool TryCreateCacheKey(
            QueuedRequest request,
            out PathCacheKey key)
        {
            key = default;
            if (!enableExactQueryCache ||
                cacheLifetimeFrames <= 0 ||
                !(request.Pathfinder is IVersionedPathfinding versioned) ||
                versioned.GridVersion <= 0)
            {
                return false;
            }

            key = new PathCacheKey(
                request.Pathfinder,
                request.Start,
                request.Destination,
                request.Options,
                versioned.GridVersion);
            return true;
        }

        private void RemoveOldestCacheEntry()
        {
            var found = false;
            var oldestKey = default(PathCacheKey);
            var oldestFrame = int.MaxValue;
            foreach (var pair in _resultCache)
            {
                if (pair.Value.StoredFrame >= oldestFrame)
                {
                    continue;
                }

                found = true;
                oldestFrame = pair.Value.StoredFrame;
                oldestKey = pair.Key;
            }

            if (found)
            {
                _resultCache.Remove(oldestKey);
            }
        }

        private void RecordExecutionTime(double milliseconds)
        {
            _totalExecutionMilliseconds += milliseconds;
            MaximumExecutionMilliseconds = Math.Max(
                MaximumExecutionMilliseconds,
                milliseconds);
            _executionSamples[_nextExecutionSample] = milliseconds;
            _nextExecutionSample =
                (_nextExecutionSample + 1) % TimingSampleCapacity;
            _executionSampleCount = Math.Min(
                _executionSampleCount + 1,
                TimingSampleCapacity);
        }

        private static double ElapsedMilliseconds(long startTimestamp) =>
            (Stopwatch.GetTimestamp() - startTimestamp) *
            1000d / Stopwatch.Frequency;

        private void InvokeCompletion(QueuedRequest request, PathResult result)
        {
            if (request.Completed == null)
            {
                return;
            }

            try
            {
                request.Completed.Invoke(request.Handle, result);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        private long NextRequestId()
        {
            do
            {
                _nextRequestId = _nextRequestId == long.MaxValue
                    ? 1
                    : _nextRequestId + 1;
            }
            while (_queuedById.ContainsKey(_nextRequestId));

            return _nextRequestId;
        }

        private static PathRequestPriority SanitizePriority(
            PathRequestPriority priority)
        {
            var value = Mathf.Clamp((int)priority, 0, PriorityCount - 1);
            return (PathRequestPriority)value;
        }
    }
}
