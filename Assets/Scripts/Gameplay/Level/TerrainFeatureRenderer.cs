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
        private Tilemap background, preview, scenery;
        private Tile backgroundTile;
        private BoundsInt sceneryCoverage;

        public void Initialize(TerrainTilemapRenderer source, TerrainHeightMap heightMap, BoundsInt cells, TerrainFeatureSettings settings)
        {
            ground = source; map = heightMap; bounds = cells;
            if (scenery != null) scenery.ClearAllTiles();
            sceneryCoverage = cells;
            if (source.ExtendSceneryToCamera && scenery == null)
            {
                scenery = CreateLayer("Terrain Scenery", 0);
            }
            if (background == null) background = CreateLayer("Terrain Background", -2);
            if (preview == null) preview = CreateLayer("Future Terrain (visual only)", 1);
            preview.ClearAllTiles();
            background.ClearAllTiles();
            if (settings.undergroundBackgroundTile != null)
                RenderUndergroundBackground(settings);
            else
                RenderContourBackground(settings);
            ClearPreview();
        }

        public void EnsureSceneryCovers(Vector3 worldMin, Vector3 worldMax)
        {
            if (scenery == null || !ground.ExtendSceneryToCamera) return;

            Vector3Int viewMin = scenery.WorldToCell(worldMin);
            Vector3Int viewMax = scenery.WorldToCell(worldMax);
            int xMin = Mathf.Min(viewMin.x, viewMax.x);
            int xMax = Mathf.Max(viewMin.x, viewMax.x) + 1;
            int yMin = Mathf.Min(viewMin.y, viewMax.y);
            if (xMin >= sceneryCoverage.xMin && xMax <= sceneryCoverage.xMax && yMin >= sceneryCoverage.yMin)
                return;

            const int padding = 8;
            // Grow in batches to avoid repainting every time the camera crosses a cell.
            sceneryCoverage.xMin = Mathf.Min(sceneryCoverage.xMin, xMin - padding);
            sceneryCoverage.xMax = Mathf.Max(sceneryCoverage.xMax, xMax + padding);
            sceneryCoverage.yMin = Mathf.Min(sceneryCoverage.yMin, yMin - padding);
            int leftHeight = map.GetColumnHeight(0);
            int rightHeight = map.GetColumnHeight(map.Width - 1);
            var cells = new BoundsInt(sceneryCoverage.xMin, sceneryCoverage.yMin, bounds.zMin,
                sceneryCoverage.size.x, bounds.yMin + Mathf.Max(leftHeight, rightHeight) - sceneryCoverage.yMin, 1);
            var tiles = new TileBase[cells.size.x * cells.size.y];
            for (int x = cells.xMin; x < cells.xMax; x++)
            {
                int column = Mathf.Clamp(x - bounds.xMin, 0, map.Width - 1);
                bool outside = x < bounds.xMin || x >= bounds.xMax;
                int top = outside ? bounds.yMin + map.GetColumnHeight(column) : bounds.yMin;
                for (int y = cells.yMin; y < top; y++)
                {
                    int index = x - cells.xMin + (y - cells.yMin) * cells.size.x;
                    tiles[index] = y < bounds.yMin ? ground.FillTile : ground.GetGroundTile(map, x - bounds.xMin, y - bounds.yMin);
                }
            }
            scenery.SetTilesBlock(cells, tiles);
        }

        private void RenderUndergroundBackground(TerrainFeatureSettings settings)
        {
            var tiles = new TileBase[map.Width * map.Height];
            for (int x = 0; x < map.Width; x++)
                for (int y = 0; y < map.GetColumnHeight(x); y++)
                    tiles[x + y * map.Width] = settings.undergroundDetailTile != null && (x * 7 + y * 11) % 19 == 0
                        ? settings.undergroundDetailTile : settings.undergroundBackgroundTile;
            background.SetTilesBlock(bounds, tiles);
        }

        private void RenderContourBackground(TerrainFeatureSettings settings)
        {
            if (backgroundTile == null)
            {
                backgroundTile = ScriptableObject.CreateInstance<Tile>();
                backgroundTile.hideFlags = HideFlags.DontSave;
                backgroundTile.colliderType = Tile.ColliderType.None;
            }
            backgroundTile.sprite = (ground.GroundTile as Tile)?.sprite;
            backgroundTile.color = settings.backgroundColor;
            int height = Mathf.Clamp(settings.backgroundHeight, 0, 64);
            var positions = new Vector3Int[map.Width * height];
            var tiles = new TileBase[positions.Length];
            int index = 0;
            for (int x = 0; x < map.Width; x++)
                for (int y = map.GetColumnHeight(x); y < map.GetColumnHeight(x) + height; y++)
                {
                    positions[index] = bounds.min + new Vector3Int(x, y, 0);
                    tiles[index++] = backgroundTile;
                }
            background.SetTiles(positions, tiles);
        }

        public void RenderCommitted(TerrainFeatureState state)
        {
            // Keep temporary tile assets out of authored scene data when using Generate's context menu.
            if (!Application.isPlaying) { RenderPreview(state); return; }
            ground.Tilemap.SetTilesBlock(bounds, ground.BuildTiles(map, state));
            ground.GetComponent<TilemapCollider2D>().ProcessTilemapChanges();
            ClearPreview();
        }
        public void RenderPreview(TerrainFeatureState state)
        {
            preview.SetTilesBlock(bounds, ground.BuildTiles(map, state));
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
            renderer.sortingOrder = source.sortingOrder + orderOffset;
            var tilemap = item.GetComponent<Tilemap>();
            tilemap.tileAnchor = ground.Tilemap.tileAnchor;
            return tilemap;
        }
        private void OnDestroy()
        {
            Release(background != null ? background.gameObject : null);
            Release(preview != null ? preview.gameObject : null);
            Release(scenery != null ? scenery.gameObject : null);
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
