using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using WelpTimeToDie.Combat;

namespace WelpTimeToDie.TestDummies
{
    /// <summary>Keyboard input and visible playback exclusively for the dash test scene.</summary>
    public sealed class DashDummyDemo : MonoBehaviour
    {
        public const int PlaybackFramesPerSecond = 12;
        public const double PlaybackFrameDuration = 1d / PlaybackFramesPerSecond;

        [SerializeField] private CombatPhysicsController combatPhysics;
        [SerializeField] private DashTestDummy player;
        [SerializeField] private Camera mapCamera;
        [SerializeField] private UIDocument hud;

        private double playbackSeconds;
        private Label frameLabel;
        private Label strengthLabel;
        private Label statusLabel;

        public bool IsPaused { get; private set; }

        public void Bind(CombatPhysicsController clock, DashTestDummy dummy)
        {
            combatPhysics = clock != null ? clock : throw new ArgumentNullException(nameof(clock));
            player = dummy != null ? dummy : throw new ArgumentNullException(nameof(dummy));
            playbackSeconds = 0d;
        }

        public void SetPaused(bool paused) => IsPaused = paused;

        public void AdjustStrength(int steps)
        {
            if (player != null)
                player.DashStrength = Mathf.Round((player.DashStrength + steps * 0.1f) * 10f) / 10f;
        }

        private void Update()
        {
            if (combatPhysics == null || player == null || !player.isActiveAndEnabled ||
                !combatPhysics.isActiveAndEnabled || combatPhysics.IsFaulted)
            {
                playbackSeconds = 0d;
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                int strengthSteps = (keyboard.upArrowKey.wasPressedThisFrame ? 1 : 0)
                    - (keyboard.downArrowKey.wasPressedThisFrame ? 1 : 0);
                if (strengthSteps != 0)
                    AdjustStrength(strengthSteps);
                if (keyboard.spaceKey.wasPressedThisFrame)
                {
                    SetPaused(!IsPaused);
                    // This render interval straddles the pause/resume event; don't simulate it.
                    return;
                }
            }

            if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
            {
                player.ResetDummy();
                playbackSeconds = 0d;
                return;
            }

            if (IsPaused)
                return;

            if (!player.IsDashing)
            {
                playbackSeconds = 0d;
                if (keyboard != null)
                {
                    bool left = keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame;
                    bool right = keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame;
                    if (left != right)
                    {
                        if (left) player.DashLeft();
                        else player.DashRight();
                    }
                }
                // Start counting wall-clock playback time AFTER accepting the move.
                return;
            }

            AdvancePlayback(Time.unscaledDeltaTime);
        }

        /// <summary>
        /// Paces the demo at 12 combat frames per real second without changing simulation dt.
        /// Paused wall time is discarded; a partial frame is retained for a smooth resume.
        /// </summary>
        public void AdvancePlayback(double realSeconds)
        {
            if (double.IsNaN(realSeconds) || double.IsInfinity(realSeconds) || realSeconds < 0d)
                throw new ArgumentOutOfRangeException(nameof(realSeconds));
            if (IsPaused)
                return;
            if (combatPhysics == null || player == null || !player.isActiveAndEnabled ||
                !combatPhysics.isActiveAndEnabled || combatPhysics.IsFaulted || !player.IsDashing)
            {
                playbackSeconds = 0d;
                return;
            }

            playbackSeconds += realSeconds;
            while (!IsPaused && player.IsDashing && playbackSeconds >= PlaybackFrameDuration)
            {
                playbackSeconds -= PlaybackFrameDuration;
                combatPhysics.AdvanceFrames(1);
            }
            if (!player.IsDashing)
                playbackSeconds = 0d;
        }

        private void LateUpdate()
        {
            if (mapCamera != null && player != null)
                mapCamera.orthographicSize = Mathf.Max(4f, (player.MapWidth + 4f) / (2f * mapCamera.aspect));
            RefreshHud();
        }

        private void RefreshHud()
        {
            if (hud == null)
                return;
            if (frameLabel == null || frameLabel.panel == null)
            {
                frameLabel = hud.rootVisualElement.Q<Label>("frameCount");
                strengthLabel = hud.rootVisualElement.Q<Label>("dashStrength");
                statusLabel = hud.rootVisualElement.Q<Label>("playbackStatus");
            }
            if (frameLabel != null)
                frameLabel.text = $"Frames elapsed: {combatPhysics?.CurrentFrame ?? 0}";
            if (strengthLabel != null && player != null)
                strengthLabel.text = $"Dash strength: {player.DashStrength:0.0}  [Up / Down]";
            if (statusLabel != null)
                statusLabel.text = IsPaused ? "PAUSED  |  Space to resume" : "12 frames/s  |  Space to pause";
        }

        private void OnDisable()
        {
            playbackSeconds = 0d;
            frameLabel = strengthLabel = statusLabel = null;
        }
    }
}
