using System;
using UnityEngine;

namespace WelpTimeToDie.Combat
{
    /// <summary>
    /// Owns the default 2D physics world's clock while enabled in Play Mode.
    /// Nothing advances automatically. Call AdvanceFrames on Unity's main thread.
    /// This controls Rigidbody2D/Collider2D physics, not Unity's render or animation clocks.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    public sealed class CombatPhysicsController : MonoBehaviour
    {
        public const int FramesPerSecond = 24;
        public const float FrameDuration = 1f / FramesPerSecond;

        private static CombatPhysicsController owner;

        [SerializeField, Range(1, 16)]
        [Tooltip("Fixed physics steps within each 1/24-second combat frame. Set before combat starts.")]
        private int physicsStepsPerFrame = 2;

        private SimulationMode2D previousSimulationMode;
        private bool stopRequested;

        /// <summary>Number of fully simulated combat frames. Starts at zero.</summary>
        public long CurrentFrame { get; private set; }
        public double SimulatedSeconds => FramesToSeconds(CurrentFrame);
        public int PhysicsStepsPerFrame => physicsStepsPerFrame;
        public bool IsAdvancing { get; private set; }

        /// <summary>
        /// An advance threw, so a frame or its gameplay callbacks may be incomplete.
        /// Reload/rebuild the combat world before using a new controller; there is no rollback.
        /// </summary>
        public bool IsFaulted { get; private set; }

        /// <summary>
        /// Before a combat frame: (one-based frame number, frame duration in seconds).
        /// Use for frame-based gameplay and rig targets, rather than Update/FixedUpdate.
        /// </summary>
        public event Action<long, float> BeforeFrame;

        /// <summary>
        /// Before each physics step: (frame number, zero-based step index, step seconds).
        /// Use for continuous forces and future physics/IK rig evaluation. Transform
        /// changes are synchronized after this hook and before the physics step.
        /// </summary>
        public event Action<long, int, float> BeforePhysicsStep;

        /// <summary>After physics and collision callbacks, with the completed frame number.</summary>
        public event Action<long> AfterFrame;

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            if (owner != null && owner != this)
            {
                enabled = false;
                Debug.LogError("Only one CombatPhysicsController may control the default 2D physics world.", this);
                return;
            }

            // Disabling/re-enabling inside a callback retains ownership until AdvanceFrames exits.
            if (ReferenceEquals(owner, this))
                return;

            previousSimulationMode = Physics2D.simulationMode;
            owner = this;
            Physics2D.simulationMode = SimulationMode2D.Script;
        }

        private void OnDisable()
        {
            if (!ReferenceEquals(owner, this))
                return;

            if (IsAdvancing)
                stopRequested = true; // Finish the current frame before restoring automatic physics.
            else
                ReleasePhysics();
        }

        private void OnValidate()
        {
            physicsStepsPerFrame = Mathf.Clamp(physicsStepsPerFrame, 1, 16);
        }

        /// <summary>
        /// Synchronously advances up to maxFrames, returning the number actually completed.
        /// shouldStopAfterFrame runs once after EACH completed frame (after AfterFrame),
        /// even if RequestStop was called. Return true when either fighter can act.
        /// No initial readiness check is made: the caller decides whether to start.
        /// Large requests block the calling thread; visual playback can call this one frame at a time.
        /// </summary>
        public int AdvanceFrames(int maxFrames, Func<long, bool> shouldStopAfterFrame = null)
        {
            if (maxFrames < 0)
                throw new ArgumentOutOfRangeException(nameof(maxFrames));
            if (IsAdvancing)
                throw new InvalidOperationException("Combat physics cannot be advanced recursively, including from collision callbacks.");
            if (!Application.isPlaying || this == null || !isActiveAndEnabled || !ReferenceEquals(owner, this))
                throw new InvalidOperationException("Enable this controller in Play Mode before advancing combat physics.");
            if (IsFaulted)
                throw new InvalidOperationException("Combat physics is faulted. Rebuild the combat world before continuing.");
            if (maxFrames > long.MaxValue - CurrentFrame)
                throw new OverflowException("The combat frame counter would overflow.");

            EnsureManualSimulation();
            if (maxFrames == 0)
                return 0;

            // Capture once: Inspector changes during callbacks must not alter a frame's duration.
            int stepCount = Mathf.Clamp(physicsStepsPerFrame, 1, 16);
            float stepSeconds = FrameDuration / stepCount;
            int completed = 0;
            stopRequested = false;
            IsAdvancing = true;

            try
            {
                while (completed < maxFrames)
                {
                    long nextFrame = CurrentFrame + 1;
                    BeforeFrame?.Invoke(nextFrame, FrameDuration);

                    for (int step = 0; step < stepCount; step++)
                    {
                        BeforePhysicsStep?.Invoke(nextFrame, step, stepSeconds);
                        EnsureManualSimulation();
                        Physics2D.SyncTransforms();
                        if (!Physics2D.defaultPhysicsScene.Simulate(stepSeconds))
                            throw new InvalidOperationException("Unity refused to simulate the combat physics step.");
                    }

                    // Commit the clock before callbacks: their exceptions cannot undo completed physics.
                    CurrentFrame = nextFrame;
                    completed++;
                    AfterFrame?.Invoke(CurrentFrame);
                    bool decisionReached = shouldStopAfterFrame?.Invoke(CurrentFrame) ?? false;
                    EnsureManualSimulation();

                    if (decisionReached || stopRequested || this == null || !isActiveAndEnabled)
                        break;
                }

                return completed;
            }
            catch
            {
                // Do not silently resume a potentially partially simulated frame after an exception.
                IsFaulted = true;
                throw;
            }
            finally
            {
                IsAdvancing = false;
                stopRequested = false;
                if (this == null || !isActiveAndEnabled)
                    ReleasePhysics();
            }
        }

        /// <summary>
        /// Requests a stop at the end of the current combat frame, including from collision
        /// callbacks. Has no effect between advances and does not skip remaining physics steps.
        /// </summary>
        public void RequestStop()
        {
            if (IsAdvancing)
                stopRequested = true;
        }

        public static double FramesToSeconds(long frames)
        {
            if (frames < 0)
                throw new ArgumentOutOfRangeException(nameof(frames));
            return frames / (double)FramesPerSecond;
        }

        /// <summary>Rounds up, so a duration never ends before the requested time.</summary>
        public static long SecondsToFrames(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0)
                throw new ArgumentOutOfRangeException(nameof(seconds));

            double frames = Math.Ceiling(seconds * FramesPerSecond);
            // long.MaxValue rounds to 2^63 as a double; reject that boundary before casting.
            if (frames >= (double)long.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(seconds), "Duration exceeds the combat frame counter.");
            return (long)frames;
        }

        private static void EnsureManualSimulation()
        {
            if (Physics2D.simulationMode != SimulationMode2D.Script)
                throw new InvalidOperationException("Another system changed Physics2D.simulationMode. Combat requires Script mode.");
            if (!Physics2D.defaultPhysicsScene.IsValid())
                throw new InvalidOperationException("The default 2D physics scene is invalid.");
        }

        private void ReleasePhysics()
        {
            if (!ReferenceEquals(owner, this))
                return;

            Physics2D.simulationMode = previousSimulationMode;
            owner = null;
        }
    }
}
