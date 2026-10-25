using System.Collections.Generic;
using UnityEngine;

namespace WelpTimeToDie.Levels
{
    /// <summary>Connects buried side portals to concealed surface patches without changing the height map.</summary>
    public static class TunnelAccessBuilder
    {
        public const int AccessLength = 12;

        public static TerrainFeature Connect(TerrainHeightMap map, IEnumerable<Vector3Int> coreCells,
            Vector2 entrance, Vector2 exit, int height)
        {
            var cells = new HashSet<Vector3Int>(coreCells);
            int left = Mathf.RoundToInt(entrance.x);
            int right = Mathf.RoundToInt(exit.x) - 1;
            int first = left - AccessLength;
            int last = right + AccessLength;
            AddAccess(map, cells, first, 1, Mathf.RoundToInt(entrance.y - height * 0.5f), height);
            AddAccess(map, cells, last, -1, Mathf.RoundToInt(exit.y - height * 0.5f), height);
            int entranceX = first + TunnelPathBuilder.MouthWidth / 2;
            int exitX = last - TunnelPathBuilder.MouthWidth / 2;
            return new TerrainFeature(TerrainFeatureKind.Tunnel, cells, entrance, exit, passageHeight: height,
                entranceRevealPoint: new Vector2(entranceX + 0.5f, map.GetColumnHeight(entranceX)),
                exitRevealPoint: new Vector2(exitX + 0.5f, map.GetColumnHeight(exitX)));
        }

        private static void AddAccess(TerrainHeightMap map, HashSet<Vector3Int> cells, int outside, int direction, int portalFloor, int height)
        {
            int surfaceFloor = map.GetColumnHeight(outside) - 1;
            for (int step = 1; step < TunnelPathBuilder.MouthWidth; step++)
                surfaceFloor = Mathf.Min(surfaceFloor, map.GetColumnHeight(outside + step * direction) - 1);
            int previous = surfaceFloor;
            for (int step = 0; step < AccessLength; step++)
            {
                int x = outside + step * direction;
                float t = Mathf.Clamp01((step - TunnelPathBuilder.MouthWidth + 1) /
                    (float)(AccessLength - TunnelPathBuilder.MouthWidth - 1));
                int floor = Mathf.Min(Mathf.RoundToInt(Mathf.Lerp(surfaceFloor, portalFloor, t)), map.GetColumnHeight(x) - 1);
                int bottom = Mathf.Max(1, Mathf.Min(floor, previous));
                int top = Mathf.Max(floor, previous) + height - 1;
                // The first four columns are concealed covers, not pre-carved holes or visible ramps.
                if (step < TunnelPathBuilder.MouthWidth) top = Mathf.Max(top, map.GetColumnHeight(x) - 1);
                for (int y = bottom; y <= top; y++)
                    if (map.IsSolid(x, y)) cells.Add(new Vector3Int(x, y));
                // Bridge downward corners in the previous column too, including where natural terrain dips.
                if (step > 0)
                    for (int y = bottom; y < previous; y++)
                        if (map.IsSolid(x - direction, y)) cells.Add(new Vector3Int(x - direction, y));
                previous = floor;
            }
        }

        public static bool IsSubtleDiscoveryPoint(TerrainHeightMap map, int column)
        {
            if (column < 2 || column >= map.Width - 2 || Mathf.Abs(column - map.Width / 2) < 6) return false;
            int localHigh = map.GetColumnHeight(column), localLow = localHigh;
            for (int x = Mathf.Max(0, column - 8); x <= Mathf.Min(map.Width - 1, column + 8); x++)
            {
                localHigh = Mathf.Max(localHigh, map.GetColumnHeight(x));
                localLow = Mathf.Min(localLow, map.GetColumnHeight(x));
            }
            return localHigh > map.GetColumnHeight(column) && localHigh - localLow >= 2;
        }
    }
}
