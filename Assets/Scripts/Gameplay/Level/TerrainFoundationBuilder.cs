using UnityEngine;

namespace WelpTimeToDie.Levels
{
    /// <summary>Adds underground space for deep features without moving the playable surface.</summary>
    public static class TerrainFoundationBuilder
    {
        public static TerrainHeightMap Expand(TerrainHeightMap map, TerrainFeatureSettings settings, out int addedRows)
        {
            int minimumHeight = int.MaxValue;
            for (int x = 0; x < map.Width; x++) minimumHeight = Mathf.Min(minimumHeight, map.GetColumnHeight(x));
            int requiredDepth = settings.pitCount > 0 ? Mathf.Clamp(settings.pitDepth, 2, 128) + Mathf.Clamp(settings.pitDepthVariation, 0, 32) : 0;
            if (settings.tunnelCount > 0)
                requiredDepth = Mathf.Max(requiredDepth, Mathf.Clamp(settings.tunnelHeight, 4, 12) + 2 +
                    Mathf.Clamp(settings.tunnelDepth, 2, 64) + Mathf.Clamp(settings.tunnelDepthVariation, 0, 4));
            addedRows = Mathf.Max(0, requiredDepth + 1 - minimumHeight);
            var heights = new int[map.Width];
            for (int x = 0; x < map.Width; x++) heights[x] = map.GetColumnHeight(x) + addedRows;
            return new TerrainHeightMap(heights, map.Height + addedRows, map.Seed);
        }
    }
}
