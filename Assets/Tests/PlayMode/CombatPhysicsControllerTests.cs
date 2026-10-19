using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace WelpTimeToDie.Combat.Tests
{
    public sealed class CombatPhysicsControllerTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private CombatPhysicsController controller;
        private SimulationMode2D originalMode;
        private float originalTimeScale;

        [SetUp]
        public void SetUp()
        {
            originalMode = Physics2D.simulationMode;
            originalTimeScale = Time.timeScale;
            controller = CreateObject("Combat clock test").AddComponent<CombatPhysicsController>();
        }

        [TearDown]
        public void TearDown()
        {
            for (int index = objects.Count - 1; index >= 0; index--)
                if (objects[index] != null)
                    UnityEngine.Object.DestroyImmediate(objects[index]);
            objects.Clear();
            Physics2D.simulationMode = originalMode;
            Time.timeScale = originalTimeScale;
        }

        [Test]
        public void TwentyFourFramesAdvanceOneSecondOfRealPhysics()
        {
            var body = CreateBody();
            body.linearVelocity = new Vector2(24f, 0f);
            float start = body.position.x;

            Assert.That(controller.AdvanceFrames(24), Is.EqualTo(24));
            Assert.That(controller.CurrentFrame, Is.EqualTo(24));
            Assert.That(controller.SimulatedSeconds, Is.EqualTo(1d));
            Assert.That(body.position.x - start, Is.EqualTo(24f).Within(0.003f));
        }

        [UnityTest]
        public IEnumerator RenderingAndFixedUpdatesDoNotAdvancePausedPhysics()
        {
            var body = CreateBody();
            body.linearVelocity = new Vector2(24f, 0f);
            Vector2 start = body.position;

            yield return new WaitForSecondsRealtime(0.15f);

            Assert.That(body.position, Is.EqualTo(start));
            Assert.That(controller.CurrentFrame, Is.Zero);
        }

        [TestCase(0f)]
        [TestCase(0.1f)]
        [TestCase(3f)]
        public void ExplicitAdvancementIgnoresUnityTimeScale(float timeScale)
        {
            Time.timeScale = timeScale;
            var body = CreateBody();
            body.linearVelocity = new Vector2(24f, 0f);
            float start = body.position.x;

            controller.AdvanceFrames(1);

            Assert.That(body.position.x - start, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void DecisionPredicateRunsAfterEveryCompletedFrameAndStopsEarly()
        {
            var checkedFrames = new List<long>();
            controller.AfterFrame += frame => checkedFrames.Add(-frame);

            int advanced = controller.AdvanceFrames(24, frame =>
            {
                checkedFrames.Add(frame);
                Assert.That(controller.CurrentFrame, Is.EqualTo(frame));
                return frame == 3;
            });

            Assert.That(advanced, Is.EqualTo(3));
            CollectionAssert.AreEqual(new long[] { -1, 1, -2, 2, -3, 3 }, checkedFrames);
            Assert.That(controller.CurrentFrame, Is.EqualTo(3));
            Assert.That(controller.AdvanceFrames(2), Is.EqualTo(2));
            Assert.That(controller.CurrentFrame, Is.EqualTo(5));
        }

        [Test]
        public void HooksSurroundPhysicsAndAllStepsAddUpToOneCombatFrame()
        {
            var body = CreateBody();
            var order = new List<string>();
            float start = body.position.x;
            float totalStepSeconds = 0f;
            controller.BeforeFrame += (frame, seconds) =>
            {
                order.Add("before");
                Assert.That(frame, Is.EqualTo(1));
                Assert.That(seconds, Is.EqualTo(1f / 24f));
                body.linearVelocity = new Vector2(24f, 0f);
            };
            controller.BeforePhysicsStep += (frame, step, seconds) =>
            {
                order.Add("step" + step);
                totalStepSeconds += seconds;
            };
            controller.AfterFrame += frame =>
            {
                order.Add("after");
                Assert.That(body.position.x - start, Is.EqualTo(1f).Within(0.001f));
            };

            controller.AdvanceFrames(1);

            CollectionAssert.AreEqual(new[] { "before", "step0", "step1", "after" }, order);
            Assert.That(totalStepSeconds, Is.EqualTo(CombatPhysicsController.FrameDuration).Within(1e-7f));
        }

        [Test]
        public void ZeroAndInvalidRequestsDoNotMutateTheWorld()
        {
            int callbacks = 0;
            controller.BeforeFrame += (frame, seconds) => callbacks++;
            Assert.That(controller.AdvanceFrames(0), Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => controller.AdvanceFrames(-1));
            Assert.That(callbacks, Is.Zero);
            Assert.That(controller.CurrentFrame, Is.Zero);
            Assert.That(controller.IsFaulted, Is.False);
        }

        [Test]
        public void RecursiveAdvanceIsRejectedWithoutAdvancingExtraFrames()
        {
            controller.BeforePhysicsStep += (frame, step, seconds) =>
                Assert.Throws<InvalidOperationException>(() => controller.AdvanceFrames(1));

            Assert.That(controller.AdvanceFrames(2), Is.EqualTo(2));
            Assert.That(controller.CurrentFrame, Is.EqualTo(2));
            Assert.That(controller.IsFaulted, Is.False);
        }

        [Test]
        public void StopRequestsFinishTheFrameAndDoNotLeakIntoTheNextAdvance()
        {
            int calls = 0;
            int decisions = 0;
            Action<long, int, float> stop = (frame, step, seconds) =>
            {
                calls++;
                controller.RequestStop();
            };
            controller.BeforePhysicsStep += stop;

            Assert.That(controller.AdvanceFrames(24, frame => { decisions++; return false; }), Is.EqualTo(1));
            Assert.That(calls, Is.EqualTo(controller.PhysicsStepsPerFrame));
            Assert.That(decisions, Is.EqualTo(1));
            controller.BeforePhysicsStep -= stop;
            controller.RequestStop(); // Outside an advance, this is a no-op.
            Assert.That(controller.AdvanceFrames(2), Is.EqualTo(2));
        }

        [Test]
        public void DisablingDuringAStepCompletesTheFrameThenRestoresSimulationMode()
        {
            var body = CreateBody();
            body.linearVelocity = new Vector2(24f, 0f);
            float start = body.position.x;
            controller.BeforePhysicsStep += (frame, step, seconds) =>
            {
                controller.enabled = false;
                Assert.That(Physics2D.simulationMode, Is.EqualTo(SimulationMode2D.Script));
            };

            Assert.That(controller.AdvanceFrames(24), Is.EqualTo(1));
            Assert.That(body.position.x - start, Is.EqualTo(1f).Within(0.001f));
            Assert.That(controller.IsAdvancing, Is.False);
            Assert.That(Physics2D.simulationMode, Is.EqualTo(originalMode));
            Assert.Throws<InvalidOperationException>(() => controller.AdvanceFrames(1));
        }

        [Test]
        public void DuplicateControllerCannotStealOrReleaseOwnership()
        {
            LogAssert.Expect(LogType.Error, "Only one CombatPhysicsController may control the default 2D physics world.");
            var duplicate = CreateObject("Duplicate clock test").AddComponent<CombatPhysicsController>();

            Assert.That(duplicate.enabled, Is.False);
            UnityEngine.Object.DestroyImmediate(duplicate.gameObject);
            Assert.That(Physics2D.simulationMode, Is.EqualTo(SimulationMode2D.Script));
            Assert.That(controller.AdvanceFrames(1), Is.EqualTo(1));
        }

        [Test]
        public void DisablingAndReenablingRetainsTheCombatClock()
        {
            controller.AdvanceFrames(3);
            controller.enabled = false;
            Assert.That(Physics2D.simulationMode, Is.EqualTo(originalMode));
            controller.enabled = true;
            Assert.That(Physics2D.simulationMode, Is.EqualTo(SimulationMode2D.Script));
            controller.AdvanceFrames(2);
            Assert.That(controller.CurrentFrame, Is.EqualTo(5));
        }

        [Test]
        public void ExceptionsDuringAPartialFrameFaultTheControllerAndReleaseAdvanceGuard()
        {
            var body = CreateBody();
            body.linearVelocity = new Vector2(24f, 0f);
            float start = body.position.x;
            controller.BeforePhysicsStep += (frame, step, seconds) =>
            {
                if (step == 1)
                    throw new InvalidOperationException("Injected rig failure");
            };

            Assert.Throws<InvalidOperationException>(() => controller.AdvanceFrames(24));

            Assert.That(body.position.x - start, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(controller.CurrentFrame, Is.Zero);
            Assert.That(controller.IsFaulted, Is.True);
            Assert.That(controller.IsAdvancing, Is.False);
            Assert.Throws<InvalidOperationException>(() => controller.AdvanceFrames(1));
        }

        [Test]
        public void ExceptionsAfterPhysicsRetainTheCompletedFrameNumber()
        {
            controller.AfterFrame += frame => throw new InvalidOperationException("Injected combat failure");

            Assert.Throws<InvalidOperationException>(() => controller.AdvanceFrames(24));
            Assert.That(controller.CurrentFrame, Is.EqualTo(1));
            Assert.That(controller.IsFaulted, Is.True);
            Assert.That(controller.IsAdvancing, Is.False);
        }

        [Test]
        public void ExternalAutomaticSimulationIsRejectedBeforePhysicsMoves()
        {
            Physics2D.simulationMode = SimulationMode2D.FixedUpdate;

            Assert.Throws<InvalidOperationException>(() => controller.AdvanceFrames(1));
            Assert.That(controller.CurrentFrame, Is.Zero);
            Assert.That(controller.IsAdvancing, Is.False);
        }

        [Test]
        public void PhysicsCallbacksCanRequestAStopAndCannotRecursivelyAdvance()
        {
            var body = CreateBody();
            body.gameObject.AddComponent<BoxCollider2D>();
            var sensor = body.gameObject.AddComponent<CombatPhysicsCollisionProbe>();
            sensor.Controller = controller;
            var wall = CreateObject("Test wall");
            wall.transform.position = body.transform.position + new Vector3(0.75f, 0f, 0f);
            wall.AddComponent<BoxCollider2D>();

            Assert.That(controller.AdvanceFrames(24), Is.EqualTo(1));
            Assert.That(sensor.SawCollision, Is.True);
            Assert.That(sensor.RecursiveAdvanceRejected, Is.True);
            Assert.That(controller.CurrentFrame, Is.EqualTo(1));
        }

        [Test]
        public void TimeConversionsHandleRoundingAndInvalidDurations()
        {
            Assert.That(CombatPhysicsController.FramesToSeconds(24), Is.EqualTo(1d));
            Assert.That(CombatPhysicsController.SecondsToFrames(1d), Is.EqualTo(24));
            Assert.That(CombatPhysicsController.SecondsToFrames(0d), Is.Zero);
            Assert.That(CombatPhysicsController.SecondsToFrames(0.01d), Is.EqualTo(1));
            Assert.That(CombatPhysicsController.SecondsToFrames(1.01d), Is.EqualTo(25));
            Assert.Throws<ArgumentOutOfRangeException>(() => CombatPhysicsController.FramesToSeconds(-1));
            foreach (double value in new[] { -1d, double.NaN, double.PositiveInfinity, double.MaxValue })
                Assert.Throws<ArgumentOutOfRangeException>(() => CombatPhysicsController.SecondsToFrames(value));
        }

        private GameObject CreateObject(string name)
        {
            var instance = new GameObject(name);
            objects.Add(instance);
            return instance;
        }

        private Rigidbody2D CreateBody()
        {
            var instance = CreateObject("Test body");
            instance.transform.position = new Vector3(1000f, 1000f, 0f);
            var body = instance.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.linearDamping = 0f;
            body.angularDamping = 0f;
            body.interpolation = RigidbodyInterpolation2D.None;
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;
            return body;
        }
    }

    public sealed class CombatPhysicsCollisionProbe : MonoBehaviour
    {
        public CombatPhysicsController Controller;
        public bool SawCollision;
        public bool RecursiveAdvanceRejected;

        private void OnCollisionEnter2D(Collision2D collision)
        {
            SawCollision = true;
            try
            {
                Controller.AdvanceFrames(1);
            }
            catch (InvalidOperationException)
            {
                RecursiveAdvanceRejected = true;
            }
            Controller.RequestStop();
        }
    }
}
