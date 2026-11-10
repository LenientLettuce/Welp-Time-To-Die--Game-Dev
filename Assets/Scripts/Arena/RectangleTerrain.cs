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
        private const float HalfWidth = 0.45f, BodyHeight = 1.8f, Skin = 0.002f;

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

        public float Surface(float x)
        {
            int left = Mathf.FloorToInt((x - HalfWidth - origin.x) / cellSize.x);
            int right = Mathf.FloorToInt((x + HalfWidth - origin.x) / cellSize.x);
            int height = 0;
            for (int c = left; c <= right; c++) height = Math.Max(height, map.GetColumnHeight(c));
            return origin.y + height * cellSize.y + Skin;
        }

        public bool Fits(float x, float y)
        {
            if (x - HalfWidth < MinX || x + HalfWidth > MaxX || y < MinY || y + BodyHeight > MaxY) return false;
            int left = Mathf.FloorToInt((x - HalfWidth + Skin - origin.x) / cellSize.x);
            int right = Mathf.FloorToInt((x + HalfWidth - Skin - origin.x) / cellSize.x);
            int bottom = Mathf.FloorToInt((y + Skin - origin.y) / cellSize.y);
            int top = Mathf.FloorToInt((y + BodyHeight - Skin - origin.y) / cellSize.y);
            for (int c = left; c <= right; c++)
                for (int r = bottom; r <= top; r++)
                    if (map.IsSolid(c, r) && !State.IsRemoved(new Vector3Int(c, r))) return false;
            return true;
        }

        private void Evaluate(RectangleFighter fighter)
        {
            var center = new Vector3((fighter.X - origin.x) / cellSize.x,
                (fighter.Y + BodyHeight / 2 - origin.y) / cellSize.y);
            State.Evaluate(new Bounds(center, new Vector3(2 * HalfWidth / cellSize.x, BodyHeight / cellSize.y, 0)),
                fighter.VelocityY, revealDistance);
        }

        public void Move(RectangleFighter fighter, float dx, float dy, bool allowStep)
        {
            Evaluate(fighter);
            bool grounded = !Fits(fighter.X, fighter.Y - 0.035f) && fighter.VelocityY <= 0;
            int steps = Math.Max(1, Mathf.CeilToInt(Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) / 0.08f));
            float stepX = dx / steps, stepY = dy / steps;
            for (int i = 0; i < steps; i++)
            {
                float targetX = fighter.X + stepX;
                float x = Mathf.Clamp(targetX, MinX + HalfWidth, MaxX - HalfWidth);
                if (Fits(x, fighter.Y)) fighter.X = x;
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
                            if (top - fighter.Y > 0.02f && top - fighter.Y <= cellSize.y + 0.01f && Fits(fighter.X, top) && Fits(x, top))
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
                if (Fits(fighter.X, y)) fighter.Y = y;
                else
                {
                    // Find the contact, rather than leaving the body floating a movement slice away.
                    float low = 0, high = 1;
                    for (int j = 0; j < 12; j++)
                    {
                        float middle = (low + high) / 2;
                        if (Fits(fighter.X, fighter.Y + stepY * middle)) low = middle; else high = middle;
                    }
                    fighter.Y += stepY * low;
                    fighter.VelocityY = 0;
                    stepY = 0;
                }
                Evaluate(fighter);
            }
            fighter.TerrainGrounded = !Fits(fighter.X, fighter.Y - 0.035f) && fighter.VelocityY <= 0;
        }

        public bool ClearStrike(RectangleFighter from, RectangleFighter to)
        {
            var a = new Vector2(from.X, from.Y + 0.9f);
            var b = new Vector2(to.X, to.Y + 0.9f);
            int steps = Mathf.CeilToInt(Vector2.Distance(a, b) / 0.1f);
            for (int i = 1; i < steps; i++)
            {
                var point = Vector2.Lerp(a, b, i / (float)steps);
                int c = Mathf.FloorToInt((point.x - origin.x) / cellSize.x);
                int r = Mathf.FloorToInt((point.y - origin.y) / cellSize.y);
                if (map.IsSolid(c, r) && !State.IsRemoved(new Vector3Int(c, r))) return false;
            }
            return true;
        }
    }
}
