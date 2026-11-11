using UnityEngine;
using UnityEngine.Tilemaps;

namespace WelpTimeToDie.Levels
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Tilemap), typeof(TilemapRenderer), typeof(TilemapCollider2D))]
    public sealed class TerrainTilemapRenderer : MonoBehaviour
    {
        [SerializeField] private TileBase groundTile;
        [SerializeField] private TerrainContourPalette contourPalette;
        [SerializeField] private TileBase soilTile;
        [SerializeField] private TileBase soilLeftTile;
        [SerializeField] private TileBase soilRightTile;
        [SerializeField] private TileBase grassTile;
        [SerializeField] private TileBase grassLeftTile;
        [SerializeField] private TileBase grassRightTile;
        [SerializeField] private TileBase grassSingleTile;
        [SerializeField] private TileBase soilFadeTile;
        [SerializeField] private TileBase soilFadeSparseTile;
        [SerializeField] private bool extendSceneryToCamera;

        public TileBase GroundTile => groundTile;
        public TileBase FillTile => soilTile != null ? soilTile : groundTile;
        public bool ExtendSceneryToCamera => extendSceneryToCamera;
        public Tilemap Tilemap => GetComponent<Tilemap>();

        public TileBase GetGroundTile(TerrainHeightMap map, int x, int y, TerrainFeatureState state = null)
        {
            int depth = map.GetColumnHeight(x) - y - 1;
            if (contourPalette != null)
            {
                var open = GetOpenNeighbors(map, x, y, state);
                if (open == TerrainOpenNeighbors.None) return GetFillTile(x, y, depth);

                var cell = new Vector3Int(x, y);
                bool excavated = depth > 0 && state != null &&
                    (state.IsRemoved(cell + Vector3Int.up) ||
                     state.IsRemoved(cell + Vector3Int.up + Vector3Int.left) ||
                     state.IsRemoved(cell + Vector3Int.up + Vector3Int.right));
                var tile = contourPalette.GetTile(open, excavated);
                if (tile != null) return tile;
            }

            bool leftEdge = !IsVisualGround(map, x - 1, y, state);
            bool rightEdge = !IsVisualGround(map, x + 1, y, state);
            if (depth == 0 && grassTile != null)
            {
                if (leftEdge && rightEdge && grassSingleTile != null) return grassSingleTile;
                if (leftEdge && grassLeftTile != null) return grassLeftTile;
                if (rightEdge && grassRightTile != null) return grassRightTile;
                return grassTile;
            }

            if (leftEdge && soilLeftTile != null) return soilLeftTile;
            if (rightEdge && soilRightTile != null) return soilRightTile;
            return GetFillTile(x, y, depth);
        }

        private TileBase GetFillTile(int x, int y, int depth)
        {
            // Break up the surface texture without adding a visible repeating row of rock tiles.
            uint pattern = unchecked((uint)x * 0x9E3779B9u ^ (uint)y * 0x85EBCA6Bu);
            int variation = (int)((pattern ^ (pattern >> 16)) & 7);
            if (depth == 1 && variation < 2 && soilFadeTile != null) return soilFadeTile;
            if ((depth == 1 && variation < 5 || depth == 2 && variation == 0) && soilFadeSparseTile != null)
                return soilFadeSparseTile;
            return FillTile;
        }

        private TerrainOpenNeighbors GetOpenNeighbors(TerrainHeightMap map, int x, int y, TerrainFeatureState state)
        {
            bool north = IsVisualGround(map, x, y + 1, state);
            bool east = IsVisualGround(map, x + 1, y, state);
            bool south = IsVisualGround(map, x, y - 1, state);
            bool west = IsVisualGround(map, x - 1, y, state);
            var open = TerrainOpenNeighbors.None;
            if (!north) open |= TerrainOpenNeighbors.North;
            if (!east) open |= TerrainOpenNeighbors.East;
            if (!south) open |= TerrainOpenNeighbors.South;
            if (!west) open |= TerrainOpenNeighbors.West;

            // A diagonal makes an inside corner only when both adjoining sides are solid.
            if (north && east && !IsVisualGround(map, x + 1, y + 1, state)) open |= TerrainOpenNeighbors.NorthEast;
            if (south && east && !IsVisualGround(map, x + 1, y - 1, state)) open |= TerrainOpenNeighbors.SouthEast;
            if (south && west && !IsVisualGround(map, x - 1, y - 1, state)) open |= TerrainOpenNeighbors.SouthWest;
            if (north && west && !IsVisualGround(map, x - 1, y + 1, state)) open |= TerrainOpenNeighbors.NorthWest;
            return open;
        }

        private bool IsVisualGround(TerrainHeightMap map, int x, int y, TerrainFeatureState state)
        {
            if (x < 0 || x >= map.Width || y < 0)
                return extendSceneryToCamera && y < map.GetColumnHeight(x);

            return map.IsSolid(x, y) && (state == null || !state.IsRemoved(new Vector3Int(x, y)));
        }

        public TileBase[] BuildTiles(TerrainHeightMap map, TerrainFeatureState state = null)
        {
            var tiles = new TileBase[map.Width * map.Height];
            for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < map.Width; x++)
                    if (map.IsSolid(x, y) && (state == null || !state.IsRemoved(new Vector3Int(x, y))))
                        tiles[x + y * map.Width] = GetGroundTile(map, x, y, state);
            return tiles;
        }

        public void Render(TerrainHeightMap map, BoundsInt cells)
        {
            if (groundTile == null) throw new System.InvalidOperationException("Assign a ground tile before generating terrain.");
            Tilemap.ClearAllTiles();
            Tilemap.SetTilesBlock(cells, BuildTiles(map));
            GetComponent<TilemapCollider2D>().ProcessTilemapChanges();
        }
    }
}
