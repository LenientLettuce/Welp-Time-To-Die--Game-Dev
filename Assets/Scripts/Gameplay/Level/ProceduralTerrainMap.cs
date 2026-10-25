using System;
using UnityEngine;

namespace WelpTimeToDie.Levels
{
    [DefaultExecutionOrder(-9000)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LevelLayout))]
    public sealed class ProceduralTerrainMap : MonoBehaviour
    {
        [SerializeField] private TerrainSettings settings = new TerrainSettings();
        [SerializeField] private TerrainTilemapRenderer terrainRenderer;
        [SerializeField] private MapBoundaryController boundaries;
        [SerializeField] private string currentSeed;

        public TerrainSettings Settings => settings;
        public TerrainHeightMap HeightMap { get; private set; }
        public LevelLayout Layout => GetComponent<LevelLayout>();
        public string CurrentSeed => currentSeed;
        public event Action Generated;

        private void Awake() => Generate(settings.RandomizeSeed ? Guid.NewGuid().ToString("N") : settings.SeedInput);

        [ContextMenu("Generate Using Seed Input")]
        public void GenerateFromSeedInput() => Generate(settings.SeedInput);

        [ContextMenu("Generate Random Map")]
        public void GenerateRandomMap() => Generate(Guid.NewGuid().ToString("N"));

        public void Generate(string seed)
        {
            if (terrainRenderer == null || boundaries == null)
                throw new InvalidOperationException("Assign the terrain renderer and map boundaries.");
            HeightMap = new TerrainHeightGenerator().Generate(settings, seed);
            currentSeed = HeightMap.Seed;
            var cells = new BoundsInt(-HeightMap.Width / 2, -settings.BaseGroundHeight, 0, HeightMap.Width, HeightMap.Height, 1);
            Layout.SetPlayableCells(cells);
            terrainRenderer.Render(HeightMap, cells);
            boundaries.Rebuild(Layout.WorldBounds);
            Physics2D.SyncTransforms();
            Generated?.Invoke();
        }

        public float GetSurfaceHeight(float worldX, float halfWidth = 0f)
        {
            if (HeightMap == null) throw new InvalidOperationException("Generate the map before querying its surface.");
            var grid = Layout.Grid;
            int left = grid.WorldToCell(new Vector3(worldX - halfWidth, 0f)).x;
            int right = grid.WorldToCell(new Vector3(worldX + halfWidth, 0f)).x;
            int height = 1;
            for (int x = left; x <= right; x++)
                height = Mathf.Max(height, HeightMap.GetColumnHeight(x - Layout.PlayableCells.xMin));
            return grid.CellToWorld(new Vector3Int(left, Layout.PlayableCells.yMin + height, 0)).y;
        }
    }
}
