using System;
using NUnit.Framework;
using UnityEngine;
using WelpTimeToDie.Combat;

namespace WelpTimeToDie.TestDummies.Tests
{
    public sealed class DashTestDummyTests
    {
        private GameObject clockObject;
        private GameObject dummyObject;
        private CombatPhysicsController clock;
        private DashTestDummy dummy;
        private Rigidbody2D body;
        private SimulationMode2D previousMode;
        private GameObject demoObject;

        [SetUp]
        public void SetUp()
        {
            previousMode = Physics2D.simulationMode;
            clockObject = new GameObject("Dummy test clock");
            clock = clockObject.AddComponent<CombatPhysicsController>();
            dummyObject = new GameObject("Dash dummy test");
            dummyObject.transform.position = new Vector3(1000f, 1000f, 0f);
            dummy = dummyObject.AddComponent<DashTestDummy>();
            dummy.Bind(clock);
            body = dummyObject.GetComponent<Rigidbody2D>();
        }

        [TearDown]
        public void TearDown()
        {
            if (demoObject != null)
                UnityEngine.Object.DestroyImmediate(demoObject);
            UnityEngine.Object.DestroyImmediate(dummyObject);
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
            dummy.DashStrength = strength;
            Vector2 start = body.position;
            Assert.That(right ? dummy.DashRight() : dummy.DashLeft(), Is.True);
            Assert.That(body.position, Is.EqualTo(start)); // Queueing does not advance physics.

            clock.AdvanceFrames(5);
            Assert.That(dummy.IsDashing, Is.True);
            Assert.That(dummy.FramesRemaining, Is.EqualTo(1));
            Assert.That(body.position.x - start.x, Is.EqualTo(distance * 5f / 6f).Within(0.003f));

            clock.AdvanceFrames(1);
            Assert.That(dummy.IsDashing, Is.False);
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
            dummy.DashStrength = 0.5f;
            Vector2 start = body.position;
            dummy.DashRight();
            clock.AdvanceFrames(2);
            dummy.DashStrength = 1f;
            dummy.MapWidth = 30f;

            Assert.That(dummy.DashLeft(), Is.False);
            clock.AdvanceFrames(4);
            Assert.That(body.position.x - start.x, Is.EqualTo(5f).Within(0.003f));
            Assert.That(dummy.DashDistance, Is.EqualTo(15f));
        }

        [Test]
        public void ResetCancelsTheDashAndReturnsToTheStartingPosition()
        {
            Vector2 start = body.position;
            dummy.DashRight();
            clock.AdvanceFrames(3);
            dummy.ResetDummy();

            Assert.That(body.position, Is.EqualTo(start));
            Assert.That(dummy.IsDashing, Is.False);
            Assert.That(clock.CurrentFrame, Is.EqualTo(3));
            clock.AdvanceFrames(6);
            Assert.That(body.position, Is.EqualTo(start));
        }

        [Test]
        public void DisablingCancelsAndReenablingDoesNotDuplicateSubscriptions()
        {
            dummy.DashRight();
            clock.AdvanceFrames(1);
            dummy.enabled = false;
            Assert.That(body.linearVelocity, Is.EqualTo(Vector2.zero));
            Assert.That(dummy.IsDashing, Is.False);
            Assert.That(dummy.DashLeft(), Is.False);
            dummy.enabled = true;
            dummy.DashLeft();
            clock.AdvanceFrames(1);
            Assert.That(dummy.FramesRemaining, Is.EqualTo(5));
        }

        [Test]
        public void StrengthIsClampedAndInvalidSettingsAreRejected()
        {
            dummy.DashStrength = -10f;
            Assert.That(dummy.DashStrength, Is.EqualTo(0.1f));
            dummy.DashStrength = 10f;
            Assert.That(dummy.DashStrength, Is.EqualTo(1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => dummy.DashStrength = float.NaN);
            Assert.Throws<ArgumentOutOfRangeException>(() => dummy.MapWidth = 0f);
            dummy.MapWidth = 10000f;
            Assert.Throws<InvalidOperationException>(() => dummy.DashRight());
            Assert.That(dummy.IsDashing, Is.False);
        }

        [Test]
        public void HalfSpeedPlaybackTakesHalfASecondForTheSixFrameDash()
        {
            var demo = CreateDemo();
            float start = body.position.x;
            dummy.DashRight();

            demo.AdvancePlayback(0.25d);
            Assert.That(clock.CurrentFrame, Is.EqualTo(3));
            Assert.That(dummy.FramesRemaining, Is.EqualTo(3));
            Assert.That(body.position.x - start, Is.EqualTo(2.5f).Within(0.003f));

            demo.AdvancePlayback(0.25d);
            Assert.That(clock.CurrentFrame, Is.EqualTo(6));
            Assert.That(dummy.IsDashing, Is.False);
            Assert.That(body.position.x - start, Is.EqualTo(5f).Within(0.003f));
        }

        [Test]
        public void PausePreservesAPartialFrameAndNeverAccumulatesPausedWallTime()
        {
            var demo = CreateDemo();
            dummy.DashRight();
            demo.AdvancePlayback(DashDummyDemo.PlaybackFrameDuration / 2d);
            Vector2 pausedPosition = body.position;
            demo.SetPaused(true);

            demo.AdvancePlayback(60d);
            Assert.That(clock.CurrentFrame, Is.Zero);
            Assert.That(body.position, Is.EqualTo(pausedPosition));
            Assert.That(dummy.FramesRemaining, Is.EqualTo(6));

            demo.SetPaused(false);
            demo.AdvancePlayback(DashDummyDemo.PlaybackFrameDuration / 2d);
            Assert.That(clock.CurrentFrame, Is.EqualTo(1));
            Assert.That(dummy.FramesRemaining, Is.EqualTo(5));
        }

        [Test]
        public void KeyboardStrengthStepsAreBoundedAndDoNotAlterAnActiveDash()
        {
            var demo = CreateDemo();
            demo.AdjustStrength(1);
            Assert.That(dummy.DashStrength, Is.EqualTo(0.6f));
            dummy.DashRight();
            demo.SetPaused(true);
            demo.AdjustStrength(10);
            Assert.That(dummy.DashStrength, Is.EqualTo(1f));
            demo.AdjustStrength(-20);
            Assert.That(dummy.DashStrength, Is.EqualTo(0.1f));
            demo.SetPaused(false);
            float start = body.position.x;
            demo.AdvancePlayback(0.5d);
            Assert.That(body.position.x - start, Is.EqualTo(6f).Within(0.003f));
            Assert.That(clock.CurrentFrame, Is.EqualTo(6));
        }

        private DashDummyDemo CreateDemo()
        {
            demoObject = new GameObject("Demo playback test");
            var demo = demoObject.AddComponent<DashDummyDemo>();
            demo.Bind(clock, dummy);
            return demo;
        }
    }
}
