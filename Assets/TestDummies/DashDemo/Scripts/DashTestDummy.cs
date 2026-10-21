using System;
using UnityEngine;
using WelpTimeToDie.Combat;

namespace WelpTimeToDie.TestDummies
{
    /// <summary>Isolated test movement, not a production player or combat system.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public sealed class DashTestDummy : MonoBehaviour
    {
        public const int DashFrames = 6;

        [SerializeField] private CombatPhysicsController combatPhysics;
        [SerializeField, Range(0.1f, 1f)] private float dashStrength = 0.5f;
        [SerializeField, Min(0.01f)] private float mapWidth = 20f;

        private Rigidbody2D body;
        private CombatPhysicsController subscribedClock;
        private float dashVelocity;
        private Vector2 startingPosition;

        public int FramesRemaining { get; private set; }
        public bool IsDashing => FramesRemaining > 0;
        public float DashDistance => mapWidth * 0.5f * dashStrength;

        public float DashStrength
        {
            get => dashStrength;
            set
            {
                if (float.IsNaN(value) || float.IsInfinity(value))
                    throw new ArgumentOutOfRangeException(nameof(value));
                dashStrength = Mathf.Clamp(value, 0.1f, 1f);
            }
        }

        public float MapWidth
        {
            get => mapWidth;
            set
            {
                if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
                    throw new ArgumentOutOfRangeException(nameof(value));
                mapWidth = value;
            }
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            startingPosition = body.position;
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            body.linearDamping = 0f;
            body.angularDamping = 0f;
            body.constraints = RigidbodyConstraints2D.FreezePositionY | RigidbodyConstraints2D.FreezeRotation;
            body.interpolation = RigidbodyInterpolation2D.None;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        private void OnEnable() => Subscribe();

        private void OnDisable()
        {
            Unsubscribe();
            CancelDash();
        }

        private void OnValidate()
        {
            dashStrength = float.IsNaN(dashStrength) || float.IsInfinity(dashStrength)
                ? 0.5f : Mathf.Clamp(dashStrength, 0.1f, 1f);
            mapWidth = float.IsNaN(mapWidth) || float.IsInfinity(mapWidth)
                ? 20f : Mathf.Max(0.01f, mapWidth);
        }

        /// <summary>Explicit binding keeps this dummy independent of the real player's scene.</summary>
        public void Bind(CombatPhysicsController clock)
        {
            if (IsDashing)
                throw new InvalidOperationException("Cannot change clocks during a dash.");
            if (clock == null)
                throw new ArgumentNullException(nameof(clock));
            Unsubscribe();
            combatPhysics = clock;
            if (isActiveAndEnabled)
                Subscribe();
        }

        public bool DashLeft() => TryStartDash(-1f);
        public bool DashRight() => TryStartDash(1f);

        private bool TryStartDash(float direction)
        {
            if (!Application.isPlaying || !isActiveAndEnabled || IsDashing || combatPhysics == null ||
                !combatPhysics.isActiveAndEnabled || combatPhysics.IsFaulted || combatPhysics.IsAdvancing)
                return false;

            // Snapshot the settings: changes made in the Inspector affect the NEXT dash.
            float speed = DashDistance / (DashFrames * CombatPhysicsController.FrameDuration);
            if (float.IsInfinity(speed) || speed > Physics2D.maxTranslationSpeed)
                throw new InvalidOperationException("This map needs a dash speed above Physics2D.maxTranslationSpeed. Use a smaller test map.");

            dashVelocity = direction * speed;
            FramesRemaining = DashFrames;
            return true;
        }

        /// <summary>Test convenience only: cancel and return to the scene's starting position.</summary>
        public void ResetDummy()
        {
            if (combatPhysics != null && combatPhysics.IsAdvancing)
                throw new InvalidOperationException("Reset the dummy between combat frames.");
            CancelDash();
            body.position = startingPosition;
            body.angularVelocity = 0f;
            Physics2D.SyncTransforms();
        }

        private void CancelDash()
        {
            FramesRemaining = 0;
            if (body != null)
                body.linearVelocity = Vector2.zero;
        }

        private void BeforePhysicsStep(long frame, int step, float seconds)
        {
            if (IsDashing)
                body.linearVelocity = new Vector2(dashVelocity, 0f);
        }

        private void AfterFrame(long frame)
        {
            if (IsDashing && --FramesRemaining == 0)
                body.linearVelocity = Vector2.zero;
        }

        private void Subscribe()
        {
            if (!Application.isPlaying || combatPhysics == null || subscribedClock != null)
                return;
            subscribedClock = combatPhysics;
            subscribedClock.BeforePhysicsStep += BeforePhysicsStep;
            subscribedClock.AfterFrame += AfterFrame;
        }

        private void Unsubscribe()
        {
            if (subscribedClock != null)
            {
                subscribedClock.BeforePhysicsStep -= BeforePhysicsStep;
                subscribedClock.AfterFrame -= AfterFrame;
            }
            subscribedClock = null;
        }
    }
}
