using UnityEngine;
using System.Collections.Generic;

namespace WelpTimeToDie.Levels
{
    /// <summary>Coordinates feature generation, committed state and preview presentation.</summary>
    [DisallowMultipleComponent]
    public sealed class TerrainFeatureController : MonoBehaviour
    {
        private ProceduralTerrainMap terrain;
        private TerrainFeatureRenderer visuals;
        public TerrainFeatureState State { get; private set; }
        public TerrainFeatureRenderer Visuals => visuals;
        public int Revision => State?.Revision ?? 0;

        public void Initialize(ProceduralTerrainMap map, TerrainTilemapRenderer renderer, TerrainFeatureSettings settings,
            IReadOnlyList<TerrainFeature> features = null)
        {
            terrain = map;
            State = new TerrainFeatureState(features ?? new TerrainFeatureGenerator().Generate(map.HeightMap, settings));
            visuals = GetComponent<TerrainFeatureRenderer>();
            if (visuals == null)
            {
                visuals = gameObject.AddComponent<TerrainFeatureRenderer>();
                if (!Application.isPlaying) visuals.hideFlags = HideFlags.DontSave;
            }
            visuals.Initialize(renderer, map.HeightMap, map.Layout.PlayableCells, settings);
            visuals.RenderCommitted(State);
        }
        public Bounds ToBlockBounds(Bounds worldBounds)
        {
            // Convert all corners; generation uses grid coordinates, not world units.
            var result = new Bounds(ToBlocks(worldBounds.min), Vector3.zero);
            for (int x = 0; x < 2; x++)
                for (int y = 0; y < 2; y++)
                    for (int z = 0; z < 2; z++)
                        result.Encapsulate(ToBlocks(new Vector3(x == 0 ? worldBounds.min.x : worldBounds.max.x,
                            y == 0 ? worldBounds.min.y : worldBounds.max.y, z == 0 ? worldBounds.min.z : worldBounds.max.z)));
            return result;
        }
        private Vector3 ToBlocks(Vector3 world)
        {
            Vector3 local = terrain.Layout.Grid.LocalToCellInterpolated(terrain.Layout.Grid.transform.InverseTransformPoint(world));
            return local - (Vector3)terrain.Layout.PlayableCells.min;
        }
        public bool Evaluate(TerrainFeatureState state, Bounds worldBounds, float verticalVelocity) =>
            state.Evaluate(ToBlockBounds(worldBounds), verticalVelocity, terrain.FeatureSettings.tunnelRevealDistance);

        public void AdvanceCommitted(Bounds playerBounds, float verticalVelocity)
        {
            visuals.ClearPreview();
            if (!Evaluate(State, playerBounds, verticalVelocity)) return;
            visuals.RenderCommitted(State);
            Physics2D.SyncTransforms();
        }
        public void ClearPreview() { if (visuals != null) visuals.ClearPreview(); }
    }
}
