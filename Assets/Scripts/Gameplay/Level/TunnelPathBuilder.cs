using System.Collections.Generic;
using UnityEngine;

namespace WelpTimeToDie.Levels
{
    /// <summary>Builds a buried U passage with roofed side portals and concealed access routes.</summary>
    public sealed class TunnelPathBuilder
    {
        public const int MouthWidth = 4;

        public TerrainFeature Build(TerrainHeightMap map, int left, int right, TerrainFeatureSettings settings, System.Random random)
        {
            int height = Mathf.Clamp(settings.tunnelHeight + random.Next(0, 2), 4, 12) + 1;
            int variation = Mathf.Clamp(settings.tunnelDepthVariation, 0, 4);
            float slope = Mathf.Clamp(settings.tunnelMaximumSlope, 0.1f, 0.5f);
            int startFloor = MinimumSurface(map, left, left + MouthWidth - 1) - height - random.Next(1, 4);
            int endFloor = MinimumSurface(map, right - MouthWidth + 1, right) - height - random.Next(1, 4);
            int curvedLength = right - left - 2 * (MouthWidth - 1);
            if (curvedLength <= 0) return null;
            float maximumDepth = (slope * curvedLength - Mathf.Abs(endFloor - startFloor) * 1.5f) / Mathf.PI;
            if (maximumDepth < 1f) return null;
            float depth = Mathf.Clamp(settings.tunnelDepth + random.Next(-variation, variation + 1), 1f, maximumDepth);
            var cells = new List<Vector3Int>();
            int previous = startFloor;
            for (int x = left; x <= right; x++)
            {
                float t = Mathf.Clamp01((x - left - MouthWidth + 1) / (float)curvedLength);
                float eased = t * t * (3f - 2f * t);
                float bend = Mathf.Sin(Mathf.PI * t);
                int floor = Mathf.RoundToInt(Mathf.Lerp(startFloor, endFloor, eased) - bend * bend * depth);
                int bottom = Mathf.Min(floor, previous);
                int top = Mathf.Max(floor, previous) + height - 1;
                if (bottom < 1 || top >= map.GetColumnHeight(x) - 1) return null;
                for (int y = bottom; y <= top; y++) cells.Add(new Vector3Int(x, y));
                previous = floor;
            }
            return TunnelAccessBuilder.Connect(map, cells,
                new Vector2(left, startFloor + height * 0.5f),
                new Vector2(right + 1, endFloor + height * 0.5f), height);
        }

        private static int MinimumSurface(TerrainHeightMap map, int left, int right)
        {
            int minimum = int.MaxValue;
            for (int x = left; x <= right; x++) minimum = Mathf.Min(minimum, map.GetColumnHeight(x));
            return minimum;
        }
    }
}
