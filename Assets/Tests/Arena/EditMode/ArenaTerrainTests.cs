using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using WelpTimeToDie.Levels;

namespace WelpTimeToDie.RectanglePrototype.Tests
{
    public sealed class ArenaTerrainTests
    {
        private static RectangleTerrain Terrain(int[] heights, params TerrainFeature[] features) =>
            new RectangleTerrain(new TerrainHeightMap(heights, 32, "test"), new TerrainFeatureState(features), Vector2.zero, Vector2.one);

        [Test]
        public void MovementStepsUpOneBlockButCannotPassTallWalls()
        {
            var terrain = Terrain(new[] { 2, 2, 2, 3, 3, 3, 7, 7, 7, 7 });
            var fighter = new RectangleFighter { X = 2, Y = 2.002f, TerrainGrounded = true };
            terrain.Move(fighter, 2, -0.02f, true);
            Assert.That(fighter.X, Is.EqualTo(4).Within(0.02f));
            Assert.That(fighter.Y, Is.EqualTo(3).Within(0.02f));
            terrain.Move(fighter, 5, -0.02f, true);
            Assert.That(fighter.X, Is.LessThanOrEqualTo(5.56f));
            Assert.That(terrain.Fits(fighter.X, fighter.Y), Is.True);
        }
        [Test]
        public void FastKnockbackStopsAtWallsAndMapEdges()
        {
            var terrain = Terrain(new[] { 2, 2, 2, 2, 8, 8 });
            var fighter = new RectangleFighter { X = 2, Y = 2.002f, VelocityX = 500 };
            terrain.Move(fighter, 20, 0, false);
            Assert.That(fighter.X, Is.LessThan(3.56f)); Assert.That(fighter.VelocityX, Is.Zero);
            terrain.Move(fighter, -50, 0, false);
            Assert.That(fighter.X, Is.EqualTo(0.45f).Within(0.01f));
        }
        [Test]
        public void FallingLandsAndJumpHitsTunnelCeiling()
        {
            var cells = new List<Vector3Int>();
            for (int x = 0; x < 8; x++) for (int y = 2; y < 6; y++) cells.Add(new Vector3Int(x, y));
            var tunnel = new TerrainFeature(TerrainFeatureKind.Tunnel, cells, new Vector2(2, 3), new Vector2(7, 3));
            var terrain = Terrain(new[] { 8,8,8,8,8,8,8,8 }, tunnel);
            var fighter = new RectangleFighter { X = 2, Y = 2.002f, VelocityY = 14 };
            terrain.Move(fighter, 0, 0, false); terrain.Move(fighter, 0, 20, false);
            Assert.That(fighter.Y, Is.EqualTo(4.2f).Within(0.01f)); Assert.That(fighter.VelocityY, Is.Zero);
            fighter.VelocityY = -20; terrain.Move(fighter, 0, -20, false);
            Assert.That(fighter.Y, Is.EqualTo(2).Within(0.01f)); Assert.That(fighter.IsGrounded, Is.True);
        }
        [Test]
        public void PitPreviewCannotChangeLiveTerrainAndReplaysExactly()
        {
            var cells = new List<Vector3Int>();
            for (int x = 2; x <= 4; x++) for (int y = 1; y < 6; y++) cells.Add(new Vector3Int(x,y));
            var pit = new TerrainFeature(TerrainFeatureKind.CollapsingFloor, cells, default, default, new Rect(2,6,3,0));
            var world = RectangleCombat.CreateBasic(FighterClass.Barbarian);
            world.Terrain = Terrain(new[] { 6,6,6,6,6,6,6,6,6,6,6,6 }, pit);
            world.Player.X = 2.8f; world.Player.Y = 6.002f; world.Player.TerrainGrounded = true;
            world.Fighters[1].X = 10; world.Fighters[1].Y = 6.002f;
            var plan = RectanglePlan.Create(world, CombatMove.Wait, 1);
            Assert.That(world.Terrain.State.Revision, Is.Zero);
            Assert.That(plan.Frames[0].Terrain.State.IsOpen(0), Is.True);
            Assert.That(plan.Frames[plan.Frames.Count - 1].Player.Y, Is.LessThan(world.Player.Y));
            var actual = world.Copy(); actual.Start(0, CombatMove.Wait, 1, 0);
            foreach (var expected in plan.Frames)
            {
                actual.Step(); actual.PlanEnemies();
                Assert.That(actual.Player.Y, Is.EqualTo(expected.Player.Y));
                Assert.That(actual.Terrain.State.Revision, Is.EqualTo(expected.Terrain.State.Revision));
            }
        }
        [Test]
        public void TerrainBlocksMeleeThroughSolidRock()
        {
            var terrain = Terrain(new[] {2,2,2,8,2,2,2,2});
            Assert.That(terrain.ClearStrike(new RectangleFighter {X=2,Y=2}, new RectangleFighter {X=4.8f,Y=2}), Is.False);
        }
        [Test]
        public void SeededRehearsalMatchesEveryCommittedFighterAndTerrainFrame()
        {
            foreach (var seed in new[] { "merge-a", "merge-b", "merge-c", "merge-d" })
            {
                var map = new TerrainHeightGenerator().Generate(new TerrainSettings(), seed);
                var settings = new TerrainFeatureSettings();
                var terrain = new RectangleTerrain(map, new TerrainFeatureState(new TerrainFeatureGenerator().Generate(map, settings)), Vector2.zero, Vector2.one);
                var world = RectangleCombat.CreateBasic(FighterClass.Barbarian, golemEncounter: true);
                world.Terrain = terrain;
                world.TacticalEnemies = true;
                world.Player.X = 61; world.Player.Y = terrain.Surface(61); world.Player.TerrainGrounded = true;
                var golem = world.Fighters[1];
                golem.X = 64; golem.Y = terrain.Surface(64, golem.HalfWidth); golem.TerrainGrounded = true;
                golem.Move = CombatMove.None;
                world.PlanEnemies();
                foreach (var move in new[] {CombatMove.Move,CombatMove.Punch,CombatMove.Uppercut,CombatMove.Kick,CombatMove.DashKick,CombatMove.Block,CombatMove.Wait,CombatMove.Dash,CombatMove.Jump})
                {
                    var plan = RectanglePlan.Create(world, move, 1);
                    if (plan == null) continue;
                    var actual = world.Copy(); if (actual.Player.IsReady) actual.Start(0, move, 1, 0);
                    foreach (var frame in plan.Frames)
                    {
                        actual.Step(); actual.PlanEnemies();
                        for (int i=0;i<2;i++)
                        {
                            Assert.That(actual.Fighters[i].X, Is.EqualTo(frame.Fighters[i].X));
                            Assert.That(actual.Fighters[i].Y, Is.EqualTo(frame.Fighters[i].Y));
                            Assert.That(actual.Fighters[i].Health, Is.EqualTo(frame.Fighters[i].Health));
                            var fighter = actual.Fighters[i];
                            Assert.That(actual.Terrain.Fits(fighter.X, fighter.Y, fighter), Is.True, seed + " " + move);
                        }
                    }
                }
            }
        }
    }
}
