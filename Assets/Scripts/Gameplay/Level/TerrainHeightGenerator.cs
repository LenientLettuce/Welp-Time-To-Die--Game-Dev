using System;
using UnityEngine;

namespace WelpTimeToDie.Levels
{
    /// <summary>Seeded rolling hills with at most one block between adjacent columns.</summary>
    public sealed class TerrainHeightGenerator
    {
        public TerrainHeightMap Generate(TerrainSettings settings, string seed)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            seed = seed ?? string.Empty;
            var random = new System.Random(HashSeed(seed));
            var heights = new int[settings.WidthInBlocks];
            int previous = settings.BaseGroundHeight;
            int from = NextHeight(random, settings);
            int to = NextHeight(random, settings);
            for (int x = 0; x < heights.Length; x++)
            {
                if (x > 0 && x % settings.HillWidthInBlocks == 0)
                {
                    from = to;
                    to = NextHeight(random, settings);
                }
                float t = (x % settings.HillWidthInBlocks) / (float)settings.HillWidthInBlocks;
                int target = Mathf.RoundToInt(Mathf.Lerp(from, to, t * t * (3f - 2f * t)));
                heights[x] = Mathf.Clamp(target, previous - 1, previous + 1);
                previous = heights[x];
            }
            for (int x = 0; x < heights.Length; x++) heights[x] += settings.FloorLiftInBlocks;
            return new TerrainHeightMap(heights, settings.HeightInBlocks + settings.FloorLiftInBlocks, seed);
        }

        private static int NextHeight(System.Random random, TerrainSettings settings) => Mathf.Clamp(
            settings.BaseGroundHeight + random.Next(-settings.HeightVariation, settings.HeightVariation + 1),
            1, settings.HeightInBlocks - 3);

        // string.GetHashCode varies between runtimes; this hash keeps saved seeds reproducible.
        internal static int HashSeed(string seed)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char character in seed) hash = (hash ^ character) * 16777619;
                return (int)hash;
            }
        }
    }
}
