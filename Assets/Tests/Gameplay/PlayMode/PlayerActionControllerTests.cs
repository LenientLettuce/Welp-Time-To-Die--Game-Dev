using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using WelpTimeToDie.Combat;
using WelpTimeToDie.Player;
using WelpTimeToDie.Playback;

namespace WelpTimeToDie.Gameplay.Tests
{
    public sealed class PlayerActionControllerTests
    {
        private GameObject clockObject, playerObject, floorObject, playbackObject, targetObject;
        private CombatPhysicsController clock;
        private PlayerActionController player;
        private CombatPlaybackController playback;

        [SetUp]
        public void SetUp()
        {
            clockObject = new GameObject("Action test clock");
            clock = clockObject.AddComponent<CombatPhysicsController>();
            floorObject = new GameObject("Action test floor", typeof(BoxCollider2D));
            floorObject.transform.position = new Vector3(1000, 999.5f);
            floorObject.GetComponent<BoxCollider2D>().size = new Vector2(100, 1);
            playerObject = new GameObject("Action test player", typeof(BoxCollider2D), typeof(Rigidbody2D));
            playerObject.transform.position = new Vector3(1000, 1001.02f);
            playerObject.GetComponent<BoxCollider2D>().size = new Vector2(1, 2);
            playerObject.AddComponent<PlayerMovementMotor>();
            player = playerObject.AddComponent<PlayerActionController>();
            player.Bind(clock);
            playbackObject = new GameObject("Action test playback");
            playback = playbackObject.AddComponent<CombatPlaybackController>();
            playback.BindActions(clock, player);
            Physics2D.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            if (targetObject != null) Object.DestroyImmediate(targetObject);
            Object.DestroyImmediate(playbackObject);
            Object.DestroyImmediate(playerObject);
            Object.DestroyImmediate(floorObject);
            Object.DestroyImmediate(clockObject);
        }

        [TestCase(PlayerMove.Dash, 6)]
        [TestCase(PlayerMove.Attack, 9)]
        [TestCase(PlayerMove.Block, 8)]
        [TestCase(PlayerMove.Jump, 12)]
        public void ActionsFinishOnTheirExactFrameAndRejectReplacement(PlayerMove move, int duration)
        {
            Assert.That(player.TryStart(move, Vector2.right, 0.5f), Is.True);
            Assert.That(player.TryStart(PlayerMove.Wait, Vector2.left, 1f), Is.False);
            clock.AdvanceFrames(duration - 1);
            Assert.That(player.IsBusy, Is.True);
            Assert.That(player.State.FramesRemaining, Is.EqualTo(1));
            clock.AdvanceFrames(1);
            Assert.That(player.IsBusy, Is.False);
        }

        [UnityTest]
        public IEnumerator AirbornePlayerFreezesBetweenDecisionsAndLandsWhenFramesAdvance()
        {
            Assert.That(player.TryStart(PlayerMove.Jump, Vector2.up, 1f), Is.True);
            clock.AdvanceFrames(12);
            Assert.That(player.Motor.IsGrounded, Is.False);
            Vector2 pausedPosition = player.Motor.Body.position;
            yield return null;
            yield return null;
            Assert.That(player.Motor.Body.position, Is.EqualTo(pausedPosition));
            Assert.That(player.TryStart(PlayerMove.Jump, Vector2.up, 1f), Is.False);
            clock.AdvanceFrames(24);
            Assert.That(player.Motor.IsGrounded, Is.True);
            Assert.That(player.Motor.BodyCollider.bounds.min.y, Is.EqualTo(1000f).Within(0.03f));
        }

        [Test]
        public void NextFrameAdvancesExactlyOneFrameAndDiscardsPlaybackWallTime()
        {
            Assert.That(playback.TryAction(PlayerMove.Jump, Vector2.up, 1f), Is.True);
            playback.StepFrame();
            Assert.That(clock.CurrentFrame, Is.EqualTo(1));
            Assert.That(player.State.ElapsedFrames, Is.EqualTo(1));
            Assert.That(playback.IsPaused, Is.True);
            playback.AdvancePlayback(30d);
            Assert.That(clock.CurrentFrame, Is.EqualTo(1));
            playback.SetPaused(false);
            playback.AdvancePlayback(CombatPlaybackController.PlaybackFrameDuration);
            Assert.That(clock.CurrentFrame, Is.EqualTo(2));
        }

        [Test]
        public void AttackHitsOnceOnFrameFourAndCanRechargeThroughWait()
        {
            playerObject.AddComponent<PlayerCombatController>();
            targetObject = new GameObject("Attack target probe", typeof(BoxCollider2D), typeof(DamageReceiverProbe));
            targetObject.transform.position = new Vector3(1002, 1001);
            targetObject.AddComponent<BoxCollider2D>();
            var receiver = targetObject.GetComponent<DamageReceiverProbe>();
            Physics2D.SyncTransforms();
            Assert.That(player.TryStart(PlayerMove.Attack, Vector2.right, 1f), Is.True);
            clock.AdvanceFrames(3);
            Assert.That(receiver.DamageReceived, Is.Zero);
            clock.AdvanceFrames(1);
            Assert.That(receiver.DamageReceived, Is.EqualTo(2));
            Assert.That(receiver.HitCount, Is.EqualTo(1));
            clock.AdvanceFrames(5);
            Assert.That(player.TryStart(PlayerMove.Attack, Vector2.right, 1f), Is.False);
            Assert.That(player.TryStart(PlayerMove.Wait, Vector2.right, 1f), Is.True);
            clock.AdvanceFrames(4);
            Assert.That(player.TryStart(PlayerMove.Attack, Vector2.right, 1f), Is.True);
        }

        [Test]
        public void BlockProtectsOnlyTheChosenDirectionDuringItsEightFrames()
        {
            var combat = playerObject.AddComponent<PlayerCombatController>();
            Assert.That(player.TryStart(PlayerMove.Block, Vector2.right, 1f), Is.True);
            Assert.That(combat.ResolveIncomingDamage(2, new Vector2(1002, 1001)), Is.Zero);
            Assert.That(combat.ResolveIncomingDamage(2, new Vector2(998, 1001)), Is.EqualTo(2));
            clock.AdvanceFrames(8);
            Assert.That(combat.ResolveIncomingDamage(2, new Vector2(1002, 1001)), Is.EqualTo(2));
        }

        [Test]
        public void PlanningSnapshotsDoNotChangeLiveActionsOrCooldowns()
        {
            var copy = player.State.Copy();
            copy.TryStart(PlayerMove.Dash, Vector2.left, 1f, true);
            copy.BeginFrame();
            Assert.That(player.State.Move, Is.EqualTo(PlayerMove.None));
            Assert.That(player.State.Cooldown(PlayerMove.Dash), Is.Zero);
            Assert.That(copy.Cooldown(PlayerMove.Dash), Is.EqualTo(11));
        }
    }
}
