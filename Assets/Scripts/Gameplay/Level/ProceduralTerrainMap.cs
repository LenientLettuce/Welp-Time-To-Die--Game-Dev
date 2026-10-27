using System;
using UnityEngine;

namespace WelpTimeToDie.Levels
{
    [DefaultExecutionOrder(-9000)]
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LevelLayout))]
    public sealed class ProceduralTerrainMap : MonoBehaviour
    {
        [SerializeField] private TerrainSettings settings = new TerrainSettings();
        [SerializeField] private TerrainFeatureSettings featureSettings = new TerrainFeatureSettings();
        [SerializeField] private TerrainTilemapRenderer terrainRenderer;
        [SerializeField] private MapBoundaryController boundaries;
        [SerializeField] private string currentSeed;

        public TerrainSettings Settings => settings;
        public TerrainFeatureSettings FeatureSettings => featureSettings;
        public TerrainFeatureController Features { get; private set; }
        public TerrainHeightMap HeightMap { get; private set; }
        public LevelLayout Layout => GetComponent<LevelLayout>();
        public string CurrentSeed => currentSeed;
        public event Action Generated;

        private void Awake()
        {
            if (Application.isPlaying) Generate(settings.RandomizeSeed ? Guid.NewGuid().ToString("N") : settings.SeedInput);
        }

#if UNITY_EDITOR
        private bool previewNeedsRefresh;
        private void OnEnable() => previewNeedsRefresh = true;
        // OnValidate can run on a loading thread. Defer object changes to the editor preview updater.
        private void OnValidate() => previewNeedsRefresh = true;
        public bool RefreshEditorPreviewIfNeeded()
        {
            if (Application.isPlaying || !previewNeedsRefresh || UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating || UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode ||
                terrainRenderer == null || boundaries == null || !gameObject.scene.IsValid() ||
                UnityEditor.SceneManagement.EditorSceneManager.IsPreviewScene(gameObject.scene)) return false;
            previewNeedsRefresh = false;
            Generate(settings.SeedInput);
            UnityEditor.SceneView.RepaintAll();
            return true;
        }
#endif

        [ContextMenu("Generate Using Seed Input")]
        public void GenerateFromSeedInput() => Generate(settings.SeedInput);

        [ContextMenu("Generate Random Map")]
        public void GenerateRandomMap() => Generate(Guid.NewGuid().ToString("N"));

        public void Generate(string seed)
        {
            if (terrainRenderer == null || boundaries == null)
                throw new InvalidOperationException("Assign the terrain renderer and map boundaries.");
            HeightMap = new TerrainHeightGenerator().Generate(settings, seed);
            HeightMap = TerrainFoundationBuilder.Expand(HeightMap, featureSettings, out int foundationRows);
            var features = new TerrainFeatureGenerator().Generate(HeightMap, featureSettings);
            int requiredHeight = HeightMap.Height;
            for (int x = 0; x < HeightMap.Width; x++)
                requiredHeight = Mathf.Max(requiredHeight, HeightMap.GetColumnHeight(x) + Mathf.Clamp(featureSettings.backgroundHeight, 0, 64) + 1);
            if (requiredHeight != HeightMap.Height)
            {
                var heights = new int[HeightMap.Width];
                for (int x = 0; x < heights.Length; x++) heights[x] = HeightMap.GetColumnHeight(x);
                HeightMap = new TerrainHeightMap(heights, requiredHeight, seed);
            }
            currentSeed = HeightMap.Seed;
            var cells = new BoundsInt(-HeightMap.Width / 2, -settings.BaseGroundHeight - foundationRows, 0, HeightMap.Width, HeightMap.Height, 1);
            Layout.SetPlayableCells(cells);
            terrainRenderer.Render(HeightMap, cells);
            Features = GetComponent<TerrainFeatureController>();
            if (Features == null)
            {
                Features = gameObject.AddComponent<TerrainFeatureController>();
                if (!Application.isPlaying) Features.hideFlags = HideFlags.DontSave;
            }
            Features.Initialize(this, terrainRenderer, featureSettings, features);
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

        /// <summary>Find a nearby step, including tunnel bottoms and collapsed pits.</summary>
        public float GetStepSurfaceHeight(float worldX, float halfWidth, float feetY, float maximumRise, TerrainFeatureState state)
        {
            if (HeightMap == null) return feetY;
            state = state ?? Features?.State;
            var grid = Layout.Grid;
            Vector3Int min = grid.WorldToCell(new Vector3(worldX - halfWidth, feetY - 0.05f));
            Vector3Int max = grid.WorldToCell(new Vector3(worldX + halfWidth, feetY + maximumRise + 0.05f));
            float surface = feetY;
            for (int x = min.x; x <= max.x; x++)
                for (int y = min.y - 1; y <= max.y; y++)
                {
                    var cell = new Vector3Int(x - Layout.PlayableCells.xMin, y - Layout.PlayableCells.yMin);
                    if (!HeightMap.IsSolid(cell.x, cell.y) || state != null && state.IsRemoved(cell)) continue;
                    Vector3Int above = cell + Vector3Int.up;
                    if (HeightMap.IsSolid(above.x, above.y) && (state == null || !state.IsRemoved(above))) continue;
                    float top = grid.CellToWorld(new Vector3Int(x, y + 1)).y;
                    if (top <= feetY + maximumRise + 0.02f) surface = Mathf.Max(surface, top);
                }
            return surface;
        }
    }
}
