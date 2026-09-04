using UnityEngine;
using UnityEngine.Tilemaps;

namespace SparkyGames.Pathfinder
{
    /// <summary>
    /// Pathfinding Tilemap
    /// </summary>
    /// <seealso cref="MonoBehaviour" />
    [RequireComponent(typeof(Tilemap))]
    public class PathfindingTilemap : Pathfinding
    {
        private Tilemap _tilemap;

        [SerializeField]
        private TilemapGridSourceOptions gridSourceOptions =
            new TilemapGridSourceOptions();

        /// <summary>
        /// Gets the Tilemap terrain and bounds configuration used on the next refresh.
        /// </summary>
        public TilemapGridSourceOptions GridSourceOptions => GetGridSourceOptions();

        /// <summary>
        /// Awakes this instance.
        /// </summary>
        protected override void Awake()
        {
            _tilemap = GetComponent<Tilemap>();
            GetGridSourceOptions().Sanitize();

            base.Awake();
        }

        protected override IPathfindingGridSource CreateGridSource()
        {
            if (_tilemap == null)
            {
                _tilemap = GetComponent<Tilemap>();
            }

            return new TilemapGridSource(
                _tilemap,
                StaticObstacleMask.value == 0
                    ? null
                    : new Physics2DStaticObstacleSampler(StaticObstacleMask),
                GetGridSourceOptions());
        }

        /// <summary>
        /// Called when [validate].
        /// </summary>
        private void OnValidate()
        {
            if (_tilemap == null) _tilemap = GetComponent<Tilemap>();
            GetGridSourceOptions().Sanitize();
        }

        private TilemapGridSourceOptions GetGridSourceOptions()
        {
            gridSourceOptions ??= new TilemapGridSourceOptions();
            return gridSourceOptions;
        }
    }
}
