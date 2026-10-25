using System;
using UnityEngine;

namespace WelpTimeToDie.Levels
{
    /// <summary>Block dimensions and generation controls, independent of rendering.</summary>
    [Serializable]
    public sealed class TerrainSettings
    {
        public const int PixelsPerBlock = 16;

        [Tooltip("Map width in 16-pixel blocks. Supported range: 4–2048.")]
        [SerializeField, Range(4, 2048)] private int widthInBlocks = 128;
        [Tooltip("Map height, including air, in 16-pixel blocks. Supported range: 4–512.")]
        [SerializeField, Range(4, 512)] private int heightInBlocks = 32;
        [SerializeField, Min(1)] private int baseGroundHeight = 8;
        [SerializeField, Min(0)] private int heightVariation = 3;
        [SerializeField, Min(2)] private int hillWidthInBlocks = 10;
        [Tooltip("Generate a fresh seed on each Play. Turn off to use Seed Input.")]
        [SerializeField] private bool randomizeSeed = true;
        [SerializeField] private string seedInput = "first-map";

        public int WidthInBlocks => Mathf.Clamp(widthInBlocks, 4, 2048);
        public int HeightInBlocks => Mathf.Clamp(heightInBlocks, 4, 512);
        public int BaseGroundHeight => Mathf.Clamp(baseGroundHeight, 1, HeightInBlocks - 3);
        public int HeightVariation => Mathf.Clamp(heightVariation, 0, HeightInBlocks - 2);
        public int HillWidthInBlocks => Mathf.Max(2, hillWidthInBlocks);
        public bool RandomizeSeed => randomizeSeed;
        public string SeedInput => seedInput ?? string.Empty;

        public TerrainSettings() { }

        public TerrainSettings(int width, int height, int groundHeight = 8, int variation = 3, int hillWidth = 10)
        {
            widthInBlocks = width;
            heightInBlocks = height;
            baseGroundHeight = groundHeight;
            heightVariation = variation;
            hillWidthInBlocks = hillWidth;
        }
    }
}
