using System.Collections.Generic;
using UnityEngine;

namespace WelpTimeToDie.Levels
{
    public enum TerrainFeatureKind { Tunnel, CollapsingFloor }

    /// <summary>Immutable geometry in map-local block coordinates.</summary>
    public sealed class TerrainFeature
    {
        public TerrainFeatureKind Kind { get; }
        public IReadOnlyList<Vector3Int> Cells { get; }
        public Vector2 Entrance { get; }
        public Vector2 Exit { get; }
        public Rect Surface { get; }
        public int PassageHeight { get; }
        public Vector2 EntranceRevealPoint { get; }
        public Vector2 ExitRevealPoint { get; }

        public TerrainFeature(TerrainFeatureKind kind, IEnumerable<Vector3Int> cells,
            Vector2 entrance, Vector2 exit, Rect surface = default, int passageHeight = 0,
            Vector2? entranceRevealPoint = null, Vector2? exitRevealPoint = null)
        {
            Kind = kind;
            Cells = new List<Vector3Int>(cells).AsReadOnly();
            Entrance = entrance;
            Exit = exit;
            Surface = surface;
            PassageHeight = passageHeight;
            EntranceRevealPoint = entranceRevealPoint ?? entrance;
            ExitRevealPoint = exitRevealPoint ?? exit;
        }
    }
}
