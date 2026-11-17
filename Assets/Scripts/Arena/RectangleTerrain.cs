using System;
using UnityEngine;
using WelpTimeToDie.Levels;

namespace WelpTimeToDie.RectanglePrototype
{
    // A copied collision world, shared by rehearsal and committed combat. No live Unity
    // physics or tilemap is changed while planning a move.
    public sealed class RectangleTerrain
    {
        private readonly TerrainHeightMap map;
        private readonly Vector2 origin;
        private readonly Vector2 cellSize;
        private readonly float revealDistance;
        public TerrainFeatureState State { get; private set; }
        public float MinX => origin.x;
        public float MaxX => origin.x + map.Width * cellSize.x;
        public float MinY => origin.y;
        public float MaxY => origin.y + map.Height * cellSize.y;
        public float BlockWidth => cellSize.x;
        private const float Skin = 0.002f;

        public RectangleTerrain(ProceduralTerrainMap terrain) : this(terrain.HeightMap,
            terrain.Features.State.Copy(), terrain.Layout.Grid.CellToWorld(terrain.Layout.PlayableCells.min),
            terrain.Layout.Grid.CellToWorld(terrain.Layout.PlayableCells.min + Vector3Int.one) -
                terrain.Layout.Grid.CellToWorld(terrain.Layout.PlayableCells.min), terrain.FeatureSettings.tunnelRevealDistance) { }

        public RectangleTerrain(TerrainHeightMap heightMap, TerrainFeatureState state, Vector2 worldOrigin, Vector2 size, float reveal = 5)
        {
            map = heightMap; State = state; origin = worldOrigin; cellSize = size; revealDistance = reveal;
            if (size.x <= 0 || size.y <= 0) throw new ArgumentException("Terrain needs a positive, axis-aligned grid.");
        }

        public RectangleTerrain Copy() => new RectangleTerrain(map, State.Copy(), origin, cellSize, revealDistance);

        internal Rect SpikeAtFeet(RectangleFighter fighter)
        {
            int column = Mathf.FloorToInt((fighter.X - origin.x) / cellSize.x);
            int left = Mathf.FloorToInt((fighter.X - fighter.HalfWidth + Skin - origin.x) / cellSize.x);
            int right = Mathf.FloorToInt((fighter.X + fighter.HalfWidth - Skin - origin.x) / cellSize.x);
            int row = Mathf.RoundToInt((fighter.Y - origin.y) / cellSize.y) - 1;
            if (!IsSolid(column, row))
            {
                for (int c = left; c <= right; c++)
                {
                    if (!IsSolid(c, row)) continue;
                    column = c;
                    break;
                }
            }
            return new Rect(origin.x + column * cellSize.x, origin.y + (row + 1) * cellSize.y, cellSize.x, cellSize.y);
        }

        internal Vector2 ClipBeam(Vector2 start, Vector2 end)
        {
            Vector2 delta = end - start;
            int left = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(start.x, end.x) - origin.x) / cellSize.x));
            int right = Mathf.Min(map.Width - 1, Mathf.FloorToInt((Mathf.Max(start.x, end.x) - origin.x) / cellSize.x));
            int bottom = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(start.y, end.y) - origin.y) / cellSize.y));
            int top = Mathf.Min(map.Height - 1, Mathf.FloorToInt((Mathf.Max(start.y, end.y) - origin.y) / cellSize.y));
            float extent = 1;
            for (int c = left; c <= right; c++)
                for (int r = bottom; r <= top; r++)
                {
                    if (!IsSolid(c, r)) continue;
                    float x = origin.x + c * cellSize.x, y = origin.y + r * cellSize.y;
                    float entry = 0, exit = extent;
                    if (RectangleCombat.Clip(start.x, delta.x, x, x + cellSize.x, ref entry, ref exit) &&
                        RectangleCombat.Clip(start.y, delta.y, y, y + cellSize.y, ref entry, ref exit))
                        extent = entry;
                }
            return start + delta * extent;
        }

        private bool IsSolid(int column, int row) =>
            map.IsSolid(column, row) && !State.IsRemoved(new Vector3Int(column, row));

        public float Surface(float x, float halfWidth = RectangleFighter.BaseHalfWidth)
        {
            int left = Mathf.FloorToInt((x - halfWidth - origin.x) / cellSize.x);
            int right = Mathf.FloorToInt((x + halfWidth - origin.x) / cellSize.x);
            int height = 0;
            for (int c = left; c <= right; c++) height = Math.Max(height, map.GetColumnHeight(c));
            return origin.y + height * cellSize.y + Skin;
        }

        public bool Fits(float x, float y, RectangleFighter fighter = null)
        {
            float halfWidth = fighter?.HalfWidth ?? RectangleFighter.BaseHalfWidth;
            float height = fighter?.BodyHeight ?? RectangleFighter.BaseHeight;
            if (x - halfWidth < MinX || x + halfWidth > MaxX || y < MinY || y + height > MaxY) return false;
            int left = Mathf.FloorToInt((x - halfWidth + Skin - origin.x) / cellSize.x);
            int right = Mathf.FloorToInt((x + halfWidth - Skin - origin.x) / cellSize.x);
            int bottom = Mathf.FloorToInt((y + Skin - origin.y) / cellSize.y);
            int top = Mathf.FloorToInt((y + height - Skin - origin.y) / cellSize.y);
            for (int c = left; c <= right; c++)
                for (int r = bottom; r <= top; r++)
                    if (IsSolid(c, r)) return false;
            return true;
        }

        private void Evaluate(RectangleFighter fighter)
        {
            var center = new Vector3((fighter.X - origin.x) / cellSize.x,
                (fighter.Y + fighter.BodyHeight / 2 - origin.y) / cellSize.y);
            State.Evaluate(new Bounds(center, new Vector3(2 * fighter.HalfWidth / cellSize.x, fighter.BodyHeight / cellSize.y, 0)),
                fighter.VelocityY, revealDistance);
        }

        public void Move(RectangleFighter fighter, float dx, float dy, bool allowStep)
        {
            Evaluate(fighter);
            float halfWidth = fighter.HalfWidth;
            bool grounded = !Fits(fighter.X, fighter.Y - 0.035f, fighter) && fighter.VelocityY <= 0;
            int steps = Math.Max(1, Mathf.CeilToInt(Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) / 0.08f));
            float stepX = dx / steps, stepY = dy / steps;
            for (int i = 0; i < steps; i++)
            {
                float targetX = fighter.X + stepX;
                float x = Mathf.Clamp(targetX, MinX + halfWidth, MaxX - halfWidth);
                if (Fits(x, fighter.Y, fighter)) fighter.X = x;
                else
                {
                    bool stepped = false;
                    if (allowStep && grounded)
                    {
                        // Step only onto an exposed tile top, never through a low tunnel ceiling.
                        int first = Mathf.FloorToInt((fighter.Y - origin.y) / cellSize.y) + 1;
                        for (int row = first; row <= first + 1 && !stepped; row++)
                        {
                            float top = origin.y + row * cellSize.y + Skin;
                            if (top - fighter.Y > 0.02f && top - fighter.Y <= cellSize.y + 0.01f &&
                                Fits(fighter.X, top, fighter) && Fits(x, top, fighter))
                            { fighter.X = x; fighter.Y = top; stepped = true; }
                        }
                    }
                    if (!stepped)
                    {
                        if (fighter.VelocityX * stepX > 0) fighter.VelocityX = 0;
                        stepX = 0;
                    }
                }
                if (x != targetX && fighter.VelocityX * stepX > 0) fighter.VelocityX = 0;
                float y = fighter.Y + stepY;
                if (Fits(fighter.X, y, fighter)) fighter.Y = y;
                else
                {
                    // Find the contact, rather than leaving the body floating a movement slice away.
                    float low = 0, high = 1;
                    for (int j = 0; j < 12; j++)
                    {
                        float middle = (low + high) / 2;
                        if (Fits(fighter.X, fighter.Y + stepY * middle, fighter)) low = middle; else high = middle;
                    }
                    fighter.Y += stepY * low;
                    fighter.VelocityY = 0;
                    stepY = 0;
                }
                Evaluate(fighter);
            }
            fighter.TerrainGrounded = !Fits(fighter.X, fighter.Y - 0.035f, fighter) && fighter.VelocityY <= 0;
        }

        public bool ClearStrike(RectangleFighter from, RectangleFighter to)
        {
            var a = new Vector2(from.X, from.Y + from.BodyHeight / 2);
            var b = new Vector2(to.X, to.Y + to.BodyHeight / 2);
            int steps = Mathf.CeilToInt(Vector2.Distance(a, b) / 0.1f);
            for (int i = 1; i < steps; i++)
            {
                var point = Vector2.Lerp(a, b, i / (float)steps);
                int c = Mathf.FloorToInt((point.x - origin.x) / cellSize.x);
                int r = Mathf.FloorToInt((point.y - origin.y) / cellSize.y);
                if (IsSolid(c, r)) return false;
            }
            return true;
        }
    }
}
