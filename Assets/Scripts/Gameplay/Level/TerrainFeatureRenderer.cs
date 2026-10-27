using UnityEngine;
using UnityEngine.Tilemaps;

namespace WelpTimeToDie.Levels
{
    /// <summary>Owns background and temporary preview visuals; only committed geometry has colliders.</summary>
    [DisallowMultipleComponent]
    public sealed class TerrainFeatureRenderer : MonoBehaviour
    {
        private TerrainTilemapRenderer ground;
        private TerrainHeightMap map;
        private BoundsInt bounds;
        private Tilemap background, preview;
        private Tile tunnelTile, pitTile, backgroundTile;
        private bool showTunnelLayout;

        public void Initialize(TerrainTilemapRenderer source, TerrainHeightMap heightMap, BoundsInt cells, TerrainFeatureSettings settings)
        {
            ground = source; map = heightMap; bounds = cells;
            showTunnelLayout = !Application.isPlaying && settings.showTunnelLayoutInEditor;
            if (tunnelTile == null)
            {
                tunnelTile = MakeTile(source.GroundTile, Color.red, true);
                pitTile = MakeTile(source.GroundTile, Color.black, true);
                backgroundTile = MakeTile(source.GroundTile, settings.backgroundColor, false);
            }
            backgroundTile.color = settings.backgroundColor;
            if (background == null) background = CreateLayer("Contour Background", -2);
            if (preview == null) preview = CreateLayer("Future Terrain (visual only)", 0);
            preview.ClearAllTiles();
            background.ClearAllTiles();
            int height = Mathf.Clamp(settings.backgroundHeight, 0, 64);
            var positions = new Vector3Int[map.Width * height];
            var tiles = new TileBase[positions.Length];
            int index = 0;
            for (int x = 0; x < map.Width; x++)
                for (int y = map.GetColumnHeight(x); y < map.GetColumnHeight(x) + height; y++)
                {
                    positions[index] = cells.min + new Vector3Int(x, y, 0);
                    tiles[index++] = backgroundTile;
                }
            background.SetTiles(positions, tiles);
            ClearPreview();
        }

        public TileBase[] BuildTiles(TerrainFeatureState state)
        {
            var tiles = new TileBase[map.Width * map.Height];
            for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < map.Width; x++)
                    if (map.IsSolid(x, y) && !state.IsRemoved(new Vector3Int(x, y))) tiles[x + y * map.Width] = ground.GroundTile;
            for (int i = 0; i < state.Features.Count; i++)
            {
                if (state.IsOpen(i)) continue;
                TerrainFeature feature = state.Features[i];
                if (feature.Kind == TerrainFeatureKind.Tunnel && !showTunnelLayout) continue;
                foreach (Vector3Int cell in feature.Cells)
                    tiles[cell.x + cell.y * map.Width] = feature.Kind == TerrainFeatureKind.Tunnel ? tunnelTile : pitTile;
            }
            return tiles;
        }

        public void RenderCommitted(TerrainFeatureState state)
        {
            // Keep temporary tile assets out of authored scene data when using Generate's context menu.
            if (!Application.isPlaying) { RenderPreview(state); return; }
            ground.Tilemap.SetTilesBlock(bounds, BuildTiles(state));
            ground.GetComponent<TilemapCollider2D>().ProcessTilemapChanges();
            ClearPreview();
        }
        public void RenderPreview(TerrainFeatureState state)
        {
            preview.SetTilesBlock(bounds, BuildTiles(state));
            preview.GetComponent<TilemapRenderer>().enabled = true;
            if (Application.isPlaying) ground.GetComponent<TilemapRenderer>().enabled = false;
        }
        public void ClearPreview()
        {
            if (preview != null) preview.GetComponent<TilemapRenderer>().enabled = false;
            if (ground != null) ground.GetComponent<TilemapRenderer>().enabled = true;
        }
        private Tilemap CreateLayer(string name, int orderOffset)
        {
            var existing = ground.transform.parent.Find(name);
            var item = existing != null ? existing.gameObject : new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
            item.hideFlags = HideFlags.DontSave;
            item.transform.SetParent(ground.transform.parent, false);
            item.transform.localPosition = ground.transform.localPosition;
            item.transform.localRotation = ground.transform.localRotation;
            item.transform.localScale = ground.transform.localScale;
            var renderer = item.GetComponent<TilemapRenderer>();
            var source = ground.GetComponent<TilemapRenderer>();
            renderer.sharedMaterial = source.sharedMaterial;
            renderer.sortingLayerID = source.sortingLayerID;
            renderer.sortingOrder = source.sortingOrder + orderOffset + (name == "Future Terrain (visual only)" ? 1 : 0);
            var tilemap = item.GetComponent<Tilemap>();
            tilemap.tileAnchor = ground.Tilemap.tileAnchor;
            return tilemap;
        }
        private static Tile MakeTile(TileBase source, Color color, bool solid)
        {
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.hideFlags = HideFlags.DontSave;
            tile.sprite = (source as Tile)?.sprite;
            tile.color = color;
            tile.colliderType = solid ? Tile.ColliderType.Grid : Tile.ColliderType.None;
            return tile;
        }
        private void OnDestroy()
        {
            Release(background != null ? background.gameObject : null);
            Release(preview != null ? preview.gameObject : null);
            Release(tunnelTile);
            Release(pitTile);
            Release(backgroundTile);
        }
        private static void Release(Object item)
        {
            if (item == null) return;
            if (Application.isPlaying) Destroy(item);
            else DestroyImmediate(item);
        }
    }
}
