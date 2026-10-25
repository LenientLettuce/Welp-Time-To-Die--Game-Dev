using System;
using NUnit.Framework;
using UnityEngine;
using WelpTimeToDie.Combat;
using WelpTimeToDie.Player;
using WelpTimeToDie.Playback;
using WelpTimeToDie.Levels;

namespace WelpTimeToDie.Gameplay.Tests
{
    public sealed class PlayerDashControllerTests
    {
        private GameObject clockObject;
        private GameObject playerObject;
        private CombatPhysicsController clock;
        private PlayerDashController player;
        private Rigidbody2D body;
        private SimulationMode2D previousMode;
        private GameObject playbackObject;

        [SetUp]
        public void SetUp()
        {
            previousMode = Physics2D.simulationMode;
            clockObject = new GameObject("Player test clock");
            clock = clockObject.AddComponent<CombatPhysicsController>();
            playerObject = new GameObject("Player dash test");
            playerObject.transform.position = new Vector3(1000f, 1000f, 0f);
            player = playerObject.AddComponent<PlayerDashController>();
            player.Bind(clock);
            body = playerObject.GetComponent<Rigidbody2D>();
        }

        [TearDown]
        public void TearDown()
        {
            if (playbackObject != null)
                UnityEngine.Object.DestroyImmediate(playbackObject);
            UnityEngine.Object.DestroyImmediate(playerObject);
            UnityEngine.Object.DestroyImmediate(clockObject);
            Physics2D.simulationMode = previousMode;
        }

        [TestCase(0.1f, false, -1f)]
        [TestCase(0.1f, true, 1f)]
        [TestCase(0.5f, false, -5f)]
        [TestCase(0.5f, true, 5f)]
        [TestCase(1f, false, -10f)]
        [TestCase(1f, true, 10f)]
        public void DashTravelsScaledMapDistanceInExactlySixFrames(float strength, bool right, float distance)
        {
            player.DashStrength = strength;
            Vector2 start = body.position;
            Assert.That(right ? player.DashRight() : player.DashLeft(), Is.True);
            Assert.That(body.position, Is.EqualTo(start)); // Queueing does not advance physics.

            clock.AdvanceFrames(5);
            Assert.That(player.IsDashing, Is.True);
            Assert.That(player.FramesRemaining, Is.EqualTo(1));
            Assert.That(body.position.x - start.x, Is.EqualTo(distance * 5f / 6f).Within(0.003f));

            clock.AdvanceFrames(1);
            Assert.That(player.IsDashing, Is.False);
            Assert.That(body.position.x - start.x, Is.EqualTo(distance).Within(0.003f));
            Assert.That(body.position.y, Is.EqualTo(start.y));
            Assert.That(body.linearVelocity, Is.EqualTo(Vector2.zero));
            Vector2 end = body.position;
            clock.AdvanceFrames(12);
            Assert.That(body.position, Is.EqualTo(end));
        }

        [Test]
        public void ActiveDashRetainsItsSettingsAndRejectsAnotherMove()
        {
            player.DashStrength = 0.5f;
            Vector2 start = body.position;
            player.DashRight();
            clock.AdvanceFrames(2);
            player.DashStrength = 1f;
            player.FallbackLevelWidth = 30f;

            Assert.That(player.DashLeft(), Is.False);
            clock.AdvanceFrames(4);
            Assert.That(body.position.x - start.x, Is.EqualTo(5f).Within(0.003f));
            Assert.That(player.DashDistance, Is.EqualTo(15f));
        }

        [Test]
        public void ResetCancelsTheDashAndReturnsToTheStartingPosition()
        {
            Vector2 start = body.position;
            player.DashRight();
            clock.AdvanceFrames(3);
            player.ResetToSpawn();

            Assert.That(body.position, Is.EqualTo(start));
            Assert.That(Vector2.Distance(playerObject.transform.position, start), Is.LessThan(0.001f));
            Assert.That(player.IsDashing, Is.False);
            Assert.That(clock.CurrentFrame, Is.EqualTo(3));
            clock.AdvanceFrames(6);
            Assert.That(body.position, Is.EqualTo(start));
        }

        [Test]
        public void DisablingCancelsAndReenablingDoesNotDuplicateSubscriptions()
        {
            player.DashRight();
            clock.AdvanceFrames(1);
            player.enabled = false;
            Assert.That(body.linearVelocity, Is.EqualTo(Vector2.zero));
            Assert.That(player.IsDashing, Is.False);
            Assert.That(player.DashLeft(), Is.False);
            player.enabled = true;
            player.DashLeft();
            clock.AdvanceFrames(1);
            Assert.That(player.FramesRemaining, Is.EqualTo(5));
        }

        [Test]
        public void StrengthIsClampedAndInvalidSettingsAreRejected()
        {
            player.DashStrength = -10f;
            Assert.That(player.DashStrength, Is.EqualTo(0.1f));
            player.DashStrength = 10f;
            Assert.That(player.DashStrength, Is.EqualTo(1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => player.DashStrength = float.NaN);
            Assert.Throws<ArgumentOutOfRangeException>(() => player.FallbackLevelWidth = 0f);
            player.FallbackLevelWidth = 10000f;
            Assert.Throws<InvalidOperationException>(() => player.DashRight());
            Assert.That(player.IsDashing, Is.False);
        }

        [Test]
        public void HalfSpeedPlaybackTakesHalfASecondForTheSixFrameDash()
        {
            var playback = CreatePlayback();
            float start = body.position.x;
            player.DashRight();

            playback.AdvancePlayback(0.25d);
            Assert.That(clock.CurrentFrame, Is.EqualTo(3));
            Assert.That(player.FramesRemaining, Is.EqualTo(3));
            Assert.That(body.position.x - start, Is.EqualTo(2.5f).Within(0.003f));

            playback.AdvancePlayback(0.25d);
            Assert.That(clock.CurrentFrame, Is.EqualTo(6));
            Assert.That(player.IsDashing, Is.False);
            Assert.That(body.position.x - start, Is.EqualTo(5f).Within(0.003f));
        }

        [Test]
        public void PausePreservesAPartialFrameAndNeverAccumulatesPausedWallTime()
        {
            var playback = CreatePlayback();
            player.DashRight();
            playback.AdvancePlayback(CombatPlaybackController.PlaybackFrameDuration / 2d);
            Vector2 pausedPosition = body.position;
            playback.SetPaused(true);

            playback.AdvancePlayback(60d);
            Assert.That(clock.CurrentFrame, Is.Zero);
            Assert.That(body.position, Is.EqualTo(pausedPosition));
            Assert.That(player.FramesRemaining, Is.EqualTo(6));

            playback.SetPaused(false);
            playback.AdvancePlayback(CombatPlaybackController.PlaybackFrameDuration / 2d);
            Assert.That(clock.CurrentFrame, Is.EqualTo(1));
            Assert.That(player.FramesRemaining, Is.EqualTo(5));
        }

        [Test]
        public void KeyboardStrengthStepsAreBoundedAndDoNotAlterAnActiveDash()
        {
            var playback = CreatePlayback();
            playback.AdjustStrength(1);
            Assert.That(player.DashStrength, Is.EqualTo(0.6f));
            player.DashRight();
            playback.SetPaused(true);
            playback.AdjustStrength(10);
            Assert.That(player.DashStrength, Is.EqualTo(1f));
            playback.AdjustStrength(-20);
            Assert.That(player.DashStrength, Is.EqualTo(0.1f));
            playback.SetPaused(false);
            float start = body.position.x;
            playback.AdvancePlayback(0.5d);
            Assert.That(body.position.x - start, Is.EqualTo(6f).Within(0.003f));
            Assert.That(clock.CurrentFrame, Is.EqualTo(6));
        }

        [Test]
        public void LevelBoundsSupplyDashDistanceAndRespectGridScale()
        {
            var levelObject = new GameObject("Test level", typeof(Grid), typeof(LevelLayout));
            try
            {
                var level = levelObject.GetComponent<LevelLayout>();
                levelObject.transform.localScale = new Vector3(2f, 1f, 1f);
                player.Level = level;
                player.FallbackLevelWidth = 99f;
                Assert.That(level.WorldBounds.size.x, Is.EqualTo(40f));
                Assert.That(player.DashReferenceWidth, Is.EqualTo(40f));
                Assert.That(player.DashDistance, Is.EqualTo(10f));
                float start = body.position.x;
                player.DashRight();
                levelObject.transform.localScale = Vector3.one;
                clock.AdvanceFrames(6);
                Assert.That(body.position.x - start, Is.EqualTo(10f).Within(0.003f));
                Assert.That(player.DashDistance, Is.EqualTo(5f));
            }
            finally { UnityEngine.Object.DestroyImmediate(levelObject); }
        }

        [Test]
        public void GridCellCentersRoundTripForTranslatedAndScaledLevels()
        {
            var levelObject = new GameObject("Test level", typeof(Grid), typeof(LevelLayout));
            try
            {
                var level = levelObject.GetComponent<LevelLayout>();
                levelObject.transform.position = new Vector3(7f, 3f, 0f);
                levelObject.transform.localScale = new Vector3(2f, 3f, 1f);
                var cell = new Vector3Int(-3, 2, 0);
                Assert.That(level.WorldToCell(level.CellCenterToWorld(cell)), Is.EqualTo(cell));
                Assert.That(level.WorldBounds.min.x, Is.EqualTo(-13f));
                Assert.That(level.WorldBounds.max.x, Is.EqualTo(27f));
            }
            finally { UnityEngine.Object.DestroyImmediate(levelObject); }
        }

        [Test]
        public void PlaybackCommandsRespectPauseAndResetDiscardsPartialFrame()
        {
            var playback = CreatePlayback();
            playback.SetPaused(true);
            Assert.That(playback.TryDash(1), Is.False);
            playback.SetPaused(false);
            Assert.That(playback.TryDash(0), Is.False);
            Assert.That(playback.TryDash(1), Is.True);
            playback.AdvancePlayback(CombatPlaybackController.PlaybackFrameDuration / 2d);
            playback.ResetPlayer();
            Assert.That(playback.TryDash(-1), Is.True);
            playback.AdvancePlayback(CombatPlaybackController.PlaybackFrameDuration / 2d);
            Assert.That(clock.CurrentFrame, Is.Zero);
            playback.AdvancePlayback(CombatPlaybackController.PlaybackFrameDuration / 2d);
            Assert.That(clock.CurrentFrame, Is.EqualTo(1));
        }

        private CombatPlaybackController CreatePlayback()
        {
            playbackObject = new GameObject("Combat playback test");
            var playback = playbackObject.AddComponent<CombatPlaybackController>();
            playback.Bind(clock, player);
            return playback;
        }
    }
}
