using System.Collections.Generic;
using UnityEngine;

namespace WelpTimeToDie.Levels
{
    /// <summary>Copyable destruction state. Prediction never mutates the committed instance.</summary>
    public sealed class TerrainFeatureState
    {
        private readonly IReadOnlyList<TerrainFeature> features;
        private readonly bool[] opened;
        private readonly HashSet<Vector3Int> removed = new HashSet<Vector3Int>();
        public int Revision { get; private set; }
        public IReadOnlyList<TerrainFeature> Features => features;
        public bool IsOpen(int index) => opened[index];
        public bool IsRemoved(Vector3Int cell) => removed.Contains(cell);

        public TerrainFeatureState(IReadOnlyList<TerrainFeature> features)
        {
            this.features = features;
            opened = new bool[features.Count];
        }
        public TerrainFeatureState Copy()
        {
            var copy = new TerrainFeatureState(features) { Revision = Revision };
            System.Array.Copy(opened, copy.opened, opened.Length);
            copy.removed.UnionWith(removed);
            return copy;
        }

        public bool Evaluate(Bounds playerInBlocks, float verticalVelocity, float revealDistance)
        {
            bool changed = false;
            for (int i = 0; i < features.Count; i++)
            {
                if (opened[i]) continue;
                TerrainFeature feature = features[i];
                bool trigger;
                if (feature.Kind == TerrainFeatureKind.Tunnel)
                {
                    float radiusSquared = Mathf.Max(0, revealDistance) * Mathf.Max(0, revealDistance);
                    trigger = ((Vector2)playerInBlocks.center - feature.EntranceRevealPoint).sqrMagnitude <= radiusSquared ||
                        ((Vector2)playerInBlocks.center - feature.ExitRevealPoint).sqrMagnitude <= radiusSquared;
                }
                else
                {
                    Rect surface = feature.Surface;
                    trigger = verticalVelocity <= 0.1f && playerInBlocks.max.x > surface.xMin + 0.02f &&
                        playerInBlocks.min.x < surface.xMax - 0.02f && Mathf.Abs(playerInBlocks.min.y - surface.y) <= 0.12f;
                }
                if (!trigger) continue;
                opened[i] = true;
                foreach (Vector3Int cell in feature.Cells) removed.Add(cell);
                Revision++;
                changed = true;
            }
            return changed;
        }
    }
}
