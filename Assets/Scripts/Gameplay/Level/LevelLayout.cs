using UnityEngine;

namespace WelpTimeToDie.Levels
{
    /// <summary>Authored level extent, independent of sprites or painted tile occupancy.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Grid))]
    public sealed class LevelLayout : MonoBehaviour
    {
        [SerializeField] private BoundsInt playableCells = new BoundsInt(-10, -1, 0, 20, 4, 1);

        public Grid Grid => GetComponent<Grid>();
        public BoundsInt PlayableCells => playableCells;

        public Bounds WorldBounds
        {
            get
            {
                var bounds = new Bounds(Grid.CellToWorld(playableCells.min), Vector3.zero);
                // Include every corner so rotated/scaled grids still have valid world bounds.
                for (int x = 0; x < 2; x++)
                    for (int y = 0; y < 2; y++)
                        for (int z = 0; z < 2; z++)
                            bounds.Encapsulate(Grid.CellToWorld(new Vector3Int(
                                x == 0 ? playableCells.xMin : playableCells.xMax,
                                y == 0 ? playableCells.yMin : playableCells.yMax,
                                z == 0 ? playableCells.zMin : playableCells.zMax)));
                return bounds;
            }
        }

        public Vector3Int WorldToCell(Vector3 position) => Grid.WorldToCell(position);
        public Vector3 CellCenterToWorld(Vector3Int cell) => Grid.GetCellCenterWorld(cell);

        private void OnValidate()
        {
            playableCells.size = Vector3Int.Max(Vector3Int.one, playableCells.size);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(WorldBounds.center, WorldBounds.size);
        }
    }
}
