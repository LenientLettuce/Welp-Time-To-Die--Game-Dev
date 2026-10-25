using NUnit.Framework;

namespace WelpTimeToDie.RectanglePrototype.Tests
{
    public sealed class RectangleCombatTests
    {
        [Test]
        public void RehearsalEndsAtThePlayersNextDecisionInsteadOfTwentyFourFramesLater()
        {
            var live = RectangleCombat.CreateBasic(FighterClass.Barbarian);
            var plan = RectanglePlan.Create(live, CombatMove.Punch, 1);
            Assert.That(plan.Frames.Count, Is.EqualTo(9));
            Assert.That(plan.Frames[8].Player.IsReady, Is.True);
            Assert.That(plan.Frames[8].Fighters[1].Health, Is.EqualTo(5));
            Assert.That(plan.Summary, Does.Contain("enemy loses 2 HP"));
            Assert.That(live.Frame, Is.Zero);
            Assert.That(live.Fighters[1].Health, Is.EqualTo(7));
        }

        [Test]
        public void RehearsalMatchesActualResolutionEveryFrameAndCopiesCannotMutateThePlan()
        {
            var live = RectangleCombat.CreateBasic(FighterClass.Barbarian);
            var plan = RectanglePlan.Create(live, CombatMove.Move, -1);
            live.Start(0, CombatMove.Move, -1, 0);
            foreach (var expected in plan.Frames)
            {
                live.Step(); live.PlanEnemies();
                Assert.That(expected.Frame, Is.EqualTo(live.Frame));
                Assert.That(expected.Player.X, Is.EqualTo(live.Player.X));
                Assert.That(expected.Player.Health, Is.EqualTo(live.Player.Health));
                Assert.That(expected.Fighters[1].Elapsed, Is.EqualTo(live.Fighters[1].Elapsed));
                var copy = expected.Copy();
                copy.Player.Health = 0;
                Assert.That(expected.Player.Health, Is.GreaterThan(0));
            }
            Assert.That(plan.Frames.Count, Is.EqualTo(8));
        }

        [Test]
        public void RechargingSelectionIsRejectedInsteadOfSilentlyBecomingWait()
        {
            var live = RectangleCombat.CreateBasic(FighterClass.Barbarian);
            live.Player.Cooldowns[(int)CombatMove.Punch] = 3;
            Assert.That(RectanglePlan.Create(live, CombatMove.Punch, 1), Is.Null);
            Assert.That(live.Player.Move, Is.EqualTo(CombatMove.None));
            Assert.That(live.Frame, Is.Zero);
        }

        [Test]
        public void PreviewWarnsWhenBlockFinishesBeforeAnIncomingAttack()
        {
            var live = RectangleCombat.CreateBasic(FighterClass.Barbarian);
            var plan = RectanglePlan.Create(live, CombatMove.Block, 1);
            Assert.That(plan.Frames.Count, Is.EqualTo(8));
            Assert.That(plan.Summary, Does.Contain("before the enemy strikes"));
        }

        [Test]
        public void FatalRehearsalStopsImmediatelyWithoutKillingTheRealPlayer()
        {
            var live = RectangleCombat.CreateBasic(FighterClass.Barbarian);
            live.Player.Health = 4;
            live.Fighters[1].Elapsed = 8;
            var plan = RectanglePlan.Create(live, CombatMove.Punch, 1);
            Assert.That(plan.Frames.Count, Is.EqualTo(1));
            Assert.That(plan.Frames[0].Lost, Is.True);
            Assert.That(plan.Summary, Does.Contain("would die"));
            Assert.That(live.Player.Health, Is.EqualTo(4));
        }

        [Test]
        public void BasicEncounterHasOneEnemyAndNoHazardDamage()
        {
            var world = RectangleCombat.CreateBasic(FighterClass.Barbarian);
            Assert.That(world.Fighters.Count, Is.EqualTo(2));
            Assert.That(world.HazardsEnabled, Is.False);
            world.Player.X = 18;
            world.Fighters[1].Move = CombatMove.None;
            world.Fighters[1].Stun = 100;
            for (int i = 0; i < 48; i++)
            {
                world.Step();
                Assert.That(world.SpikesActive, Is.False);
                Assert.That(world.SpikesWarning, Is.False);
            }
            Assert.That(world.Player.Health, Is.EqualTo(14));
        }

        [Test]
        public void BasicForecastKeepsHazardsDisabledAndOnlyOneEnemy()
        {
            var live = RectangleCombat.CreateBasic(FighterClass.Barbarian);
            var forecast = live.Copy();
            Assert.That(forecast.Fighters.Count, Is.EqualTo(2));
            Assert.That(forecast.HazardsEnabled, Is.False);
            forecast.Start(0, CombatMove.Move, -1, 0);
            Advance(forecast, 24);
            Assert.That(live.Frame, Is.Zero);
            Assert.That(live.Player.X, Is.EqualTo(8));
        }

        [TestCase(CombatMove.Punch)]
        [TestCase(CombatMove.Uppercut)]
        [TestCase(CombatMove.Kick)]
        [TestCase(CombatMove.DashKick)]
        public void EveryAttackHitsOnceAndRehearsalMatchesPlayback(CombatMove move)
        {
            var live = RectangleCombat.CreateBasic(FighterClass.Barbarian);
            var enemy = live.Fighters[1];
            enemy.Move = CombatMove.None; enemy.Stun = 100; enemy.Health = enemy.MaxHealth = 30;
            enemy.X = live.Player.X + 2;
            var plan = RectanglePlan.Create(live, move, 1);
            Assert.That(live.Start(0, move, 1, 0), Is.True);
            foreach (var expected in plan.Frames)
            {
                live.Step(); live.PlanEnemies();
                Assert.That(live.Player.X, Is.EqualTo(expected.Player.X));
                Assert.That(live.Fighters[1].Health, Is.EqualTo(expected.Fighters[1].Health));
                Assert.That(live.Fighters[1].Y, Is.EqualTo(expected.Fighters[1].Y));
                Assert.That(live.Player.HitTargets, Is.EqualTo(expected.Player.HitTargets));
            }
            Assert.That(enemy.Health, Is.EqualTo(30 - RectangleCombat.Spec(live.Player, move).Damage));
            Assert.That(live.Player.IsReady, Is.True);
            if (move == CombatMove.Uppercut) Assert.That(enemy.Y, Is.GreaterThan(RectangleCombat.Ground));
        }

        [Test]
        public void DashKickClosesDistanceWithoutPassingThroughTheTarget()
        {
            var world = RectangleCombat.CreateBasic(FighterClass.Barbarian);
            var enemy = world.Fighters[1];
            enemy.Move = CombatMove.None; enemy.Stun = 100;
            world.Start(0, CombatMove.DashKick, 1, 0);
            for (int i = 0; i < 8; i++) world.Step();
            Assert.That(world.Player.X, Is.LessThan(enemy.X));
            Assert.That(enemy.Health, Is.EqualTo(4));
        }

        [TestCase(CombatMove.Uppercut)]
        [TestCase(CombatMove.Kick)]
        [TestCase(CombatMove.DashKick)]
        public void NewAttacksRespectFacingAndReportMisses(CombatMove move)
        {
            var world = RectangleCombat.CreateBasic(FighterClass.Barbarian);
            world.Fighters[1].Move = CombatMove.None; world.Fighters[1].Stun = 100;
            var plan = RectanglePlan.Create(world, move, -1);
            Assert.That(plan.Summary, Does.Contain("misses"));
            Assert.That(world.Player.Cooldowns[(int)move], Is.Zero);
            Assert.That(plan.Frames[plan.Frames.Count - 1].Fighters[1].Health, Is.EqualTo(7));
        }

        private static void Advance(RectangleCombat world, int count)
        {
            for (int i = 0; i < count; i++) { world.Step(); world.PlanEnemies(); }
        }

        [Test]
        public void ForecastDoesNotChangeLiveHealthClockCooldownsOrProjectiles()
        {
            var live = RectangleCombat.Create(FighterClass.Wizard);
            var preview = live.Copy();
            preview.Start(0, CombatMove.Special, 1, 0);
            Advance(preview, 24);
            Assert.That(live.Frame, Is.Zero);
            Assert.That(live.Player.Health, Is.EqualTo(live.Player.MaxHealth));
            Assert.That(live.Player.Move, Is.EqualTo(CombatMove.None));
            Assert.That(live.Player.Cooldowns[(int)CombatMove.Special], Is.Zero);
            Assert.That(live.Projectiles, Is.Empty);
            Assert.That(live.Fighters[1].Health, Is.EqualTo(7));
        }

        [Test]
        public void ForecastAndPlaybackMatchFrameForFrameIncludingAiAndHazards()
        {
            var live = RectangleCombat.Create(FighterClass.Barbarian);
            var preview = live.Copy();
            live.Start(0, CombatMove.Heavy, 1, 0);
            preview.Start(0, CombatMove.Heavy, 1, 0);
            for (int frame = 0; frame < 24; frame++)
            {
                Advance(live, 1); Advance(preview, 1);
                Assert.That(preview.Frame, Is.EqualTo(live.Frame));
                Assert.That(preview.Projectiles.Count, Is.EqualTo(live.Projectiles.Count));
                for (int i = 0; i < live.Fighters.Count; i++)
                {
                    var a = live.Fighters[i]; var b = preview.Fighters[i];
                    Assert.That(b.X, Is.EqualTo(a.X)); Assert.That(b.Y, Is.EqualTo(a.Y));
                    Assert.That(b.Health, Is.EqualTo(a.Health)); Assert.That(b.Stun, Is.EqualTo(a.Stun));
                    Assert.That(b.Move, Is.EqualTo(a.Move)); Assert.That(b.Elapsed, Is.EqualTo(a.Elapsed));
                    Assert.That(b.Cooldowns, Is.EqualTo(a.Cooldowns));
                }
            }
        }

        [Test]
        public void FourFramePunchCancelsNineFrameHeavyAndStunsForFourFrames()
        {
            var world = RectangleCombat.Create(FighterClass.Barbarian);
            world.Start(0, CombatMove.Punch, 1, 0);
            for (int i = 0; i < 4; i++) world.Step();
            Assert.That(world.Fighters[1].Health, Is.EqualTo(5));
            Assert.That(world.Fighters[1].Move, Is.EqualTo(CombatMove.None));
            Assert.That(world.Fighters[1].Stun, Is.EqualTo(4));
            Assert.That(world.LastEvent, Does.Contain("COUNTER"));
            for (int i = 0; i < 4; i++) world.Step();
            Assert.That(world.Fighters[1].Stun, Is.Zero);
            Assert.That(world.Player.Health, Is.EqualTo(14));
        }

        [Test]
        public void SixFrameDashHasExactDistanceAndRecharge()
        {
            var world = RectangleCombat.Create(FighterClass.Barbarian);
            world.Player.X = 3; world.Fighters[1].Health = 0;
            Assert.That(world.Start(0, CombatMove.Dash, 1, 0, 0.5f), Is.True);
            for (int i = 0; i < 6; i++) world.Step();
            Assert.That(world.Frame, Is.EqualTo(6));
            Assert.That(world.Player.X, Is.EqualTo(5.5f).Within(0.0001f));
            Assert.That(world.Player.IsReady, Is.True);
            Assert.That(world.Player.Cooldowns[(int)CombatMove.Dash], Is.EqualTo(6));
            Assert.That(world.CanStart(0, CombatMove.Dash), Is.False);
        }

        [Test]
        public void FrontalBlockReducesHeavyDamageWithoutHitstun()
        {
            var world = RectangleCombat.Create(FighterClass.Barbarian);
            world.Fighters[1].Elapsed = 5;
            world.Start(0, CombatMove.Block, 1, 0);
            for (int i = 0; i < 4; i++) world.Step();
            Assert.That(world.Player.Health, Is.EqualTo(13));
            Assert.That(world.Player.Stun, Is.Zero);
            Assert.That(world.Player.Move, Is.EqualTo(CombatMove.Block));
            Assert.That(world.LastEvent, Does.Contain("blocked"));
        }

        [Test]
        public void UnstoppableWindupTakesDamageButCannotBeCancelled()
        {
            var world = RectangleCombat.Create(FighterClass.Barbarian);
            world.Fighters[1].Health = 0;
            var golem = world.Fighters[2];
            golem.X = 11; golem.Move = CombatMove.None;
            world.PlanEnemies();
            world.Start(0, CombatMove.Punch, 1, 0);
            for (int i = 0; i < 4; i++) world.Step();
            Assert.That(golem.Health, Is.EqualTo(8));
            Assert.That(golem.Move, Is.EqualTo(CombatMove.Heavy));
            Assert.That(golem.Stun, Is.Zero);
        }

        [Test]
        public void ProjectileHasChargeTimeAndTravelTimeAndCopyOwnsItsShots()
        {
            var world = RectangleCombat.Create(FighterClass.Wizard);
            world.Fighters[1].X = 16;
            world.Start(0, CombatMove.Special, 1, 0);
            for (int i = 0; i < 9; i++) world.Step();
            Assert.That(world.Projectiles, Is.Empty);
            world.Step();
            Assert.That(world.Projectiles.Count, Is.EqualTo(1));
            Assert.That(world.Fighters[1].Health, Is.EqualTo(7));
            var copy = world.Copy();
            copy.Projectiles[0].X = 29;
            Assert.That(world.Projectiles[0].X, Is.LessThan(10));
            Advance(world, 12);
            Assert.That(world.Fighters[1].Health, Is.LessThan(7));
        }

        [Test]
        public void SpikesDamageBothTeamsOnlyWhenActiveAndNearTheGround()
        {
            var world = RectangleCombat.Create(FighterClass.Barbarian);
            world.Player.X = 18;
            for (int i = 0; i < 7; i++) world.Step();
            Assert.That(world.Player.Health, Is.EqualTo(14));
            world.Step();
            Assert.That(world.Player.Health, Is.EqualTo(12));
            for (int i = 0; i < 8; i++) world.Step();
            Assert.That(world.Fighters[2].Health, Is.LessThan(10));
        }

        [Test]
        public void JumpCanFinishWhileAirborneButCannotBeStartedAgainInAir()
        {
            var world = RectangleCombat.Create(FighterClass.Rogue);
            world.Start(0, CombatMove.Jump, -1, 0);
            for (int i = 0; i < 12; i++) world.Step();
            Assert.That(world.Player.IsReady, Is.True);
            Assert.That(world.Player.IsGrounded, Is.False);
            world.Player.Cooldowns[(int)CombatMove.Jump] = 0;
            Assert.That(world.CanStart(0, CombatMove.Jump), Is.False);
        }

        [Test]
        public void SameFrameHitsTradeAndDeadPlayerCannotActOrAdvanceWorld()
        {
            var world = RectangleCombat.Create(FighterClass.Barbarian);
            world.Player.Health = 2; world.Fighters[1].Health = 2;
            world.Fighters[1].Move = CombatMove.Punch; world.Fighters[1].Elapsed = 0;
            world.Start(0, CombatMove.Punch, 1, 0);
            for (int i = 0; i < 4; i++) world.Step();
            Assert.That(world.Player.Health, Is.Zero);
            Assert.That(world.Fighters[1].Health, Is.Zero);
            Assert.That(world.Lost, Is.True);
            Assert.That(world.Start(0, CombatMove.Punch, 1, 0), Is.False);
            int frame = world.Frame;
            world.Step();
            Assert.That(world.Frame, Is.EqualTo(frame));
        }
    }
}
