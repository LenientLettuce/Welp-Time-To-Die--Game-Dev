using System.Collections.Generic;
using UnityEngine;

namespace WelpTimeToDie.Levels
{
    /// <summary>Seeded hillside tunnels and flat, separated collapsing floor patches.</summary>
    public sealed class TerrainFeatureGenerator
    {
        public IReadOnlyList<TerrainFeature> Generate(TerrainHeightMap map, TerrainFeatureSettings settings)
        {
            var random = new System.Random(TerrainHeightGenerator.HashSeed(map.Seed + "/features"));
            var result = new List<TerrainFeature>();
            var reserved = new bool[map.Width];
            var tunnelReserved = new bool[map.Width];
            int minimumSurface = int.MaxValue;
            for (int x = 0; x < map.Width; x++) minimumSurface = Mathf.Min(minimumSurface, map.GetColumnHeight(x));
            var candidates = new List<TunnelCandidate>();
            int minimumLength = Mathf.Max(16, settings.tunnelMinLength);
            int maximumLength = Mathf.Max(minimumLength, settings.tunnelMaxLength);
            int approach = TunnelAccessBuilder.AccessLength;
            for (int left = approach + 1; left < map.Width - minimumLength - approach - 1; left++)
            {
                if (!SuitableMouth(map, left, minimumSurface, true)) continue;
                for (int right = left + minimumLength; right < map.Width - approach - 1 && right <= left + maximumLength; right++)
                {
                    if (!SuitableMouth(map, right - TunnelPathBuilder.MouthWidth + 1, minimumSurface, false)) continue;
                    // Seeded variation avoids repeating the same short, symmetrical passages on every map.
                    candidates.Add(new TunnelCandidate { Left = left, Right = right, Priority = random.Next(100000) });
                }
            }
            candidates.Sort((a, b) => { int order = a.Priority.CompareTo(b.Priority); return order != 0 ? order : a.Left.CompareTo(b.Left); });
            var tunnelBuilder = new TunnelPathBuilder();
            int tunnels = 0;
            foreach (var candidate in candidates)
            {
                if (tunnels >= Mathf.Max(0, settings.tunnelCount)) break;
                if (!IsFree(reserved, candidate.Left - approach, candidate.Right + approach)) continue;
                if (!IsFree(tunnelReserved, candidate.Left - approach, candidate.Right + approach)) continue;
                var tunnel = tunnelBuilder.Build(map, candidate.Left, candidate.Right, settings, random);
                if (tunnel == null) continue;
                result.Add(tunnel);
                Reserve(reserved, candidate.Left - approach, candidate.Right + approach);
                int gap = Mathf.Clamp(settings.tunnelMinimumGap, 0, map.Width);
                Reserve(tunnelReserved, Mathf.Max(0, candidate.Left - approach - gap), Mathf.Min(map.Width - 1, candidate.Right + approach + gap));
                tunnels++;
            }
            int pits = 0;
            for (int attempt = 0; attempt < map.Width * 4 && pits < Mathf.Max(0, settings.pitCount); attempt++)
            {
                int variation = Mathf.Clamp(settings.pitSizeVariation, 0, 3);
                int width = Mathf.Clamp(settings.pitWidth + random.Next(-variation, variation + 1), 2, map.Width);
                if (width + 4 >= map.Width) break;
                int left = random.Next(2, map.Width - width - 1);
                // Leave the spawn platform intact.
                if (left < map.Width / 2 + 3 && left + width > map.Width / 2 - 3 || !IsFree(reserved, left - 1, left + width)) continue;
                int top = map.GetColumnHeight(left);
                bool flat = true;
                for (int x = left; x < left + width; x++) flat &= map.GetColumnHeight(x) == top;
                if (!flat || top < 4) continue;
                int depthVariation = Mathf.Clamp(settings.pitDepthVariation, 0, 32);
                int depth = Mathf.Clamp(settings.pitDepth + random.Next(-depthVariation, depthVariation + 1), 2, top - 1);
                var cells = new List<Vector3Int>();
                for (int x = left; x < left + width; x++)
                    for (int y = top - depth; y < top; y++) cells.Add(new Vector3Int(x, y));
                result.Add(new TerrainFeature(TerrainFeatureKind.CollapsingFloor, cells, default, default, new Rect(left, top, width, 0)));
                Reserve(reserved, left - 1, left + width);
                pits++;
            }
            return result.AsReadOnly();
        }

        private struct TunnelCandidate
        {
            public int Left, Right, Priority;
        }

        private static bool SuitableMouth(TerrainHeightMap map, int left, int minimumSurface, bool entrance)
        {
            int right = left + TunnelPathBuilder.MouthWidth - 1;
            int approachLeft = entrance ? left - TunnelAccessBuilder.AccessLength : left;
            int approachRight = entrance ? right : right + TunnelAccessBuilder.AccessLength;
            if (approachLeft < map.Width / 2 + 3 && approachRight >= map.Width / 2 - 3) return false;
            int low = int.MaxValue, high = int.MinValue;
            for (int x = left; x <= right; x++)
            {
                int surface = map.GetColumnHeight(x);
                low = Mathf.Min(low, surface);
                high = Mathf.Max(high, surface);
            }
            int discovery = entrance ? approachLeft + TunnelPathBuilder.MouthWidth / 2 : approachRight - TunnelPathBuilder.MouthWidth / 2;
            return low >= minimumSurface + 1 && high - low <= 2 && TunnelAccessBuilder.IsSubtleDiscoveryPoint(map, discovery);
        }
        private static bool IsFree(bool[] reserved, int left, int right)
        {
            if (left < 0 || right >= reserved.Length) return false;
            for (int x = left; x <= right; x++) if (reserved[x]) return false;
            return true;
        }
        private static void Reserve(bool[] reserved, int left, int right)
        {
            for (int x = left; x <= right; x++) reserved[x] = true;
        }
    }
}
