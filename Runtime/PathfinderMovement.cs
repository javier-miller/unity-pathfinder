using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace SparkyGames.Pathfinder
{
    /// <summary>
    /// Pathfinder Movement
    /// </summary>
    /// <seealso cref="MonoBehaviour" />
    [RequireComponent(typeof(Rigidbody2D))]
    public class PathfinderMovement : MonoBehaviour, IPathfinderMovement
    {
        private Rigidbody2D _rigidbody;
        private Vector2 _targetNextPosition;
        private Vector2 _targetPosition;
        private Vector2 _transformPosition;
        private int _currentPathIndex;
        private List<Vector3> _pathVectorList;
        private Vector3 _movementDirection;

        private Action<bool> _moveFinishedCallback = null;

        /// <summary>
        /// The speed
        /// </summary>
        [SerializeField]
        private float speed = 4f;

        /// <summary>
        /// The is active
        /// </summary>
        [SerializeField]
        private bool isActive;

        /// <summary>
        /// Gets the movement direction.
        /// </summary>
        /// <value>
        /// The movement direction.
        /// </value>
        public Vector3 MovementDirection => _movementDirection;

        /// <summary>
        /// Awakes this instance.
        /// </summary>
        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody2D>();
        }

        /// <summary>
        /// Updates this instance.
        /// </summary>
        void Update()
        {
            if (!isActive) return;

            _transformPosition = (Vector2)transform.position;
            if (_pathVectorList.Count == 0) UpdatePathEmpty();
            else UpdatePath();
        }

        /// <summary>
        /// Fixeds the update.
        /// </summary>
        private void FixedUpdate()
        {
            if (!isActive || Vector2.Distance(transform.position, _targetNextPosition) <= 0.1f) return;

            _rigidbody.MovePosition(transform.position + _movementDirection * speed * Time.fixedDeltaTime);
        }

        /// <summary>
        /// Gets a value indicating whether this instance is active.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is active; otherwise, <c>false</c>.
        /// </value>
        public bool IsActive => isActive;

        /// <summary>
        /// Sets the active.
        /// </summary>
        /// <param name="value">if set to <c>true</c> [value].</param>
        /// <returns></returns>
        public bool SetActive(bool value) => isActive = value;

        /// <summary>
        /// Sets the speed.
        /// </summary>
        /// <param name="value">The value.</param>
        public void SetSpeed(float value)
        {
            speed = value;
        }

        /// <summary>
        /// Moves to.
        /// </summary>
        /// <param name="targetPosition">The target position.</param>
        /// <param name="moveFinishedCallback">The move finished callback.</param>
        /// <returns></returns>
        public bool MoveTo(Vector3 targetPosition, Action<bool> moveFinishedCallback = null)
        {
            if (isActive && _moveFinishedCallback is not null) _moveFinishedCallback(false);
            _moveFinishedCallback = moveFinishedCallback;

            var pathfinder = PathfindingManager.GetInstance();
            if (pathfinder is null)
            {
                Debug.LogWarning("Pathfinder is not defined");
                return false;
            }

            if (!pathfinder.FindPath(transform.position, targetPosition, out IEnumerable<Vector3> path))
            {
                _moveFinishedCallback = null;
                return false;
            }

            return MoveTo(targetPosition, path, _moveFinishedCallback);
        }

        /// <summary>
        /// Moves to asynchronous.
        /// </summary>
        /// <param name="position">The position.</param>
        /// <returns></returns>
        public async Task<bool> MoveToAsync(Vector3 position)
        {
            var isMoving = true;
            var result = false;

            result = MoveTo(position, rs =>
            {
                isMoving = false;
                result = rs;
            });

            if (!result) return false;

            while (isMoving)
            {
                await Task.Yield();
            }

            return result;
        }

        /// <summary>
        /// Moves to.
        /// </summary>
        /// <param name="targetPosition">The target position.</param>
        /// <param name="path">The path.</param>
        /// <param name="moveFinishedCallback">The move finished callback.</param>
        /// <returns></returns>
        private bool MoveTo(Vector3 targetPosition, IEnumerable<Vector3> path, Action<bool> moveFinishedCallback = null)
        {
            _targetPosition = targetPosition;
            _pathVectorList = path.ToList();

            //If path has items, movement is started.
            if (_pathVectorList.Count >= 1)
            {
                _currentPathIndex = 0;
                _targetNextPosition = (Vector2)_pathVectorList[_currentPathIndex];
                _movementDirection = (_targetNextPosition - (Vector2)transform.position).normalized;

                isActive = true;

                return true;
            }

            //Checking if the distance is greater than 0.1.
            var distance = Vector2.Distance(transform.position, targetPosition);
            if (distance > 0.1f)
            {
                _targetNextPosition = targetPosition;
                isActive = true;
                return true;
            }

            moveFinishedCallback?.Invoke(true);
            _moveFinishedCallback = null;
            return true;
        }

        /// <summary>
        /// Updates the path empty.
        /// </summary>
        private void UpdatePathEmpty()
        {
            if (Vector2.Distance(_transformPosition, _targetNextPosition) > 0.1f)
            {
                _movementDirection = (_targetNextPosition - _transformPosition).normalized;
                return;
            }

            isActive = false;
            _moveFinishedCallback?.Invoke(true);
        }

        /// <summary>
        /// Updates the path.
        /// </summary>
        private void UpdatePath()
        {
            //Si la distancia al centro es mayor que 0.1f no calculamos
            if (Vector2.Distance(_transformPosition, _targetNextPosition) > 0.1f)
            {
                if (_pathVectorList.Count - _currentPathIndex != 1) return;
                if (Vector2.Distance(_transformPosition, _targetPosition) <= 0.1f) return;

                _targetNextPosition = _targetPosition;
                _movementDirection = (_targetPosition - _transformPosition).normalized;
                return;
            }

            _currentPathIndex++;
            if (_currentPathIndex >= _pathVectorList.Count)
            {
                isActive = false;
                _moveFinishedCallback?.Invoke(true);
                return;
            }

            _targetNextPosition = (Vector2)_pathVectorList[_currentPathIndex];
            _movementDirection = (_targetNextPosition - _transformPosition).normalized;
        }
    }
}
