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
        [SerializeField] private PlayerActionController actionPlayer;

        private double playbackSeconds;

        public bool IsPaused { get; private set; }

        public void Bind(CombatPhysicsController clock, PlayerDashController dashPlayer)
        {
            combatPhysics = clock != null ? clock : throw new ArgumentNullException(nameof(clock));
            player = dashPlayer != null ? dashPlayer : throw new ArgumentNullException(nameof(dashPlayer));
            playbackSeconds = 0d;
        }

        public void SetPaused(bool paused) => IsPaused = paused;

        public void BindActions(CombatPhysicsController clock, PlayerActionController actor)
        {
            combatPhysics = clock != null ? clock : throw new ArgumentNullException(nameof(clock));
            actionPlayer = actor != null ? actor : throw new ArgumentNullException(nameof(actor));
            player = null;
            playbackSeconds = 0d;
        }

        public bool IsActionActive => actionPlayer != null ? actionPlayer.IsBusy : player != null && player.IsDashing;
        public PlayerActionController ActionPlayer => actionPlayer;

        public bool TryAction(PlayerMove move, Vector2 direction, float strength)
        {
            if (!IsReady || actionPlayer == null) return false;
            bool started = actionPlayer.TryStart(move, direction, strength);
            if (started) playbackSeconds = 0d;
            return started;
        }

        public bool StepFrame()
        {
            if (!IsReady || combatPhysics.IsAdvancing) return false;
            IsPaused = true;
            playbackSeconds = 0d;
            combatPhysics.AdvanceFrames(1);
            return true;
        }

        public void AdjustStrength(int steps)
        {
            if (player != null)
                player.DashStrength = Mathf.Round((player.DashStrength + steps * 0.1f) * 10f) / 10f;
        }

        public bool IsReady => combatPhysics != null &&
            (actionPlayer != null ? actionPlayer.isActiveAndEnabled : player != null && player.isActiveAndEnabled) &&
            combatPhysics.isActiveAndEnabled && !combatPhysics.IsFaulted;
        public long CurrentFrame => combatPhysics != null ? combatPhysics.CurrentFrame : 0;
        public float DashStrength => player != null ? player.DashStrength : 0f;

        public bool TryDash(int direction)
        {
            if (!IsReady || IsPaused || direction == 0) return false;
            if (actionPlayer != null) return TryAction(PlayerMove.Dash, Vector2.right * direction, 0.5f);
            bool started = direction < 0 ? player.DashLeft() : player.DashRight();
            if (started) playbackSeconds = 0d;
            return started;
        }

        public void ResetPlayer()
        {
            if (actionPlayer != null) actionPlayer.ResetToSpawn();
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
            if (!IsReady || !IsActionActive)
            {
                playbackSeconds = 0d;
                return;
            }

            playbackSeconds += realSeconds;
            while (!IsPaused && IsActionActive && playbackSeconds >= PlaybackFrameDuration)
            {
                playbackSeconds -= PlaybackFrameDuration;
                combatPhysics.AdvanceFrames(1);
            }
            if (!IsActionActive)
                playbackSeconds = 0d;
        }

        private void OnDisable() => playbackSeconds = 0d;
    }
}
