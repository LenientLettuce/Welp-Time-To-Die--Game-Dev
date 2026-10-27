using System;
using UnityEngine;

namespace WelpTimeToDie.Levels
{
    [Serializable]
    public sealed class TerrainFeatureSettings
    {
        [Min(0)] public int backgroundHeight = 16;
        public Color backgroundColor = new Color(0.30f, 0.18f, 0.43f);
        [Tooltip("Show buried passages in red while editing. Unopened passages always look like ordinary ground during play.")]
        public bool showTunnelLayoutInEditor = true;
        [Min(0)] public int tunnelCount = 6;
        [Min(16)] public int tunnelMinLength = 28;
        [Min(16)] public int tunnelMaxLength = 64;
        [Tooltip("Minimum horizontal ground gap between separate U-shaped tunnels, in blocks.")]
        [Min(0)] public int tunnelMinimumGap = 10;
        [Tooltip("Minimum passage clearance. Always at least four blocks, including between adjacent columns.")]
        [Min(4)] public int tunnelHeight = 4;
        [Min(2)] public int tunnelDepth = 5;
        [Tooltip("Maximum curve gradient in blocks per horizontal block. Lower values make gentler tunnels.")]
        [Range(0.1f, 0.5f)] public float tunnelMaximumSlope = 0.4f;
        [Min(0)] public int tunnelDepthVariation = 1;
        [Min(0)] public float tunnelRevealDistance = 5f;
        [Min(0)] public int pitCount = 3;
        [Min(2)] public int pitWidth = 4;
        [Min(2)] public int pitDepth = 12;
        [Min(0)] public int pitDepthVariation = 3;
        [Range(0, 3)] public int pitSizeVariation = 1;
    }
}
