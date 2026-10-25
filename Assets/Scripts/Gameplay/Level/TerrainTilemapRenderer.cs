using UnityEngine;
using UnityEngine.Tilemaps;

namespace WelpTimeToDie.Levels
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Tilemap), typeof(TilemapRenderer), typeof(TilemapCollider2D))]
    public sealed class TerrainTilemapRenderer : MonoBehaviour
    {
        [SerializeField] private TileBase groundTile;
        public TileBase GroundTile => groundTile;
        public Tilemap Tilemap => GetComponent<Tilemap>();

        public void Render(TerrainHeightMap map, BoundsInt cells)
        {
            if (groundTile == null) throw new System.InvalidOperationException("Assign a ground tile before generating terrain.");
            var tiles = new TileBase[map.Width * map.Height];
            for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < map.Width; x++)
                    if (map.IsSolid(x, y)) tiles[x + y * map.Width] = groundTile;
            Tilemap.ClearAllTiles();
            Tilemap.SetTilesBlock(cells, tiles);
            GetComponent<TilemapCollider2D>().ProcessTilemapChanges();
        }
    }
}
