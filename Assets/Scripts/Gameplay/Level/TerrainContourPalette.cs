using System;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace WelpTimeToDie.Levels
{
    [Flags]
    internal enum TerrainOpenNeighbors
    {
        None = 0,
        North = 1,
        East = 2,
        South = 4,
        West = 8,
        NorthEast = 16,
        SouthEast = 32,
        SouthWest = 64,
        NorthWest = 128
    }

    [CreateAssetMenu(menuName = "Terrain/Contour Palette")]
    public sealed class TerrainContourPalette : ScriptableObject
    {
        [Serializable]
        private struct Contour
        {
            public TerrainOpenNeighbors openNeighbors;
            public TileBase tile;
            public TileBase excavatedTile;
        }

        [SerializeField] private Contour[] contours = Array.Empty<Contour>();
        private Contour[] lookup;

        private void OnValidate() => lookup = null;

        internal TileBase GetTile(TerrainOpenNeighbors openNeighbors, bool excavated)
        {
            if (lookup == null)
            {
                lookup = new Contour[256];
                foreach (var contour in contours)
                    lookup[(int)contour.openNeighbors] = contour;
            }

            var match = lookup[(int)openNeighbors];
            return excavated && match.excavatedTile != null ? match.excavatedTile : match.tile;
        }
    }
}
