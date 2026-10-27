using System;

namespace WelpTimeToDie.Levels
{
    /// <summary>Immutable column heights. Every column is solid from the map bottom.</summary>
    public sealed class TerrainHeightMap
    {
        private readonly int[] columnHeights;
        public int Width => columnHeights.Length;
        public int Height { get; }
        public string Seed { get; }

        public TerrainHeightMap(int[] heights, int height, string seed)
        {
            columnHeights = (int[])heights.Clone();
            Height = height;
            Seed = seed;
        }

        public int GetColumnHeight(int column) => columnHeights[Math.Max(0, Math.Min(Width - 1, column))];
        public bool IsSolid(int column, int row) => column >= 0 && column < Width && row >= 0 && row < columnHeights[column];
    }
}
