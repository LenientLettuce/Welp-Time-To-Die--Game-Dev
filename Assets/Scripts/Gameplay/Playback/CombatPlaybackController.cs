using System;
using UnityEngine;
using WelpTimeToDie.Combat;
using WelpTimeToDie.Player;

namespace WelpTimeToDie.Playback
{
    /// <summary>Paces combat frames while a player action is active; owns pause and reset state.</summary>
    public sealed class CombatPlaybackController : MonoBehaviour
    {
        public const int PlaybackFramesPerSecond = 12;
        public const double PlaybackFrameDuration = 1d / PlaybackFramesPerSecond;

        [SerializeField] private CombatPhysicsController combatPhysics;
        [SerializeField] private PlayerDashController player;

        private double playbackSeconds;

        public bool IsPaused { get; private set; }

        public void Bind(CombatPhysicsController clock, PlayerDashController dashPlayer)
        {
            combatPhysics = clock != null ? clock : throw new ArgumentNullException(nameof(clock));
            player = dashPlayer != null ? dashPlayer : throw new ArgumentNullException(nameof(dashPlayer));
            playbackSeconds = 0d;
        }

        public void SetPaused(bool paused) => IsPaused = paused;

        public void AdjustStrength(int steps)
        {
            if (player != null)
                player.DashStrength = Mathf.Round((player.DashStrength + steps * 0.1f) * 10f) / 10f;
        }

        public bool IsReady => combatPhysics != null && player != null &&
            player.isActiveAndEnabled && combatPhysics.isActiveAndEnabled && !combatPhysics.IsFaulted;
        public long CurrentFrame => combatPhysics != null ? combatPhysics.CurrentFrame : 0;
        public float DashStrength => player != null ? player.DashStrength : 0f;

        public bool TryDash(int direction)
        {
            if (!IsReady || IsPaused || direction == 0) return false;
            bool started = direction < 0 ? player.DashLeft() : player.DashRight();
            if (started) playbackSeconds = 0d;
            return started;
        }

        public void ResetPlayer()
        {
            if (player != null) player.ResetToSpawn();
            playbackSeconds = 0d;
        }

        /// <summary>
        /// Paces playback at 12 combat frames per real second without changing simulation dt.
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

        private void OnDisable() => playbackSeconds = 0d;
    }
}
