using UnityEngine;

namespace WelpTimeToDie.Player
{
    /// <summary>Copyable frame data. Planning and live play use the same action rules.</summary>
    public sealed class PlayerActionState
    {
        private int[] cooldowns = new int[7];
        public PlayerMove Move { get; private set; }
        public int ElapsedFrames { get; private set; }
        public Vector2 Direction { get; private set; } = Vector2.right;
        public float Strength { get; private set; } = 0.5f;
        public int Facing { get; private set; } = 1;
        public bool IsBusy => Move != PlayerMove.None;
        public int FramesRemaining => IsBusy ? PlayerMoveCatalog.Get(Move).Duration - ElapsedFrames : 0;
        public bool IsAttackActive => Move == PlayerMove.Attack && ElapsedFrames == PlayerMoveCatalog.Get(Move).Startup;
        public bool IsBlocking => Move == PlayerMove.Block;
        public int Cooldown(PlayerMove move) => cooldowns[(int)move];

        public bool CanStart(PlayerMove move, bool grounded) => move != PlayerMove.None && !IsBusy &&
            Cooldown(move) == 0 && (move != PlayerMove.Jump || grounded);

        public bool TryStart(PlayerMove move, Vector2 direction, float strength, bool grounded)
        {
            if (!CanStart(move, grounded)) return false;
            Direction = direction.sqrMagnitude < 0.001f ? Vector2.right * Facing : direction.normalized;
            if (Mathf.Abs(Direction.x) > 0.01f) Facing = Direction.x < 0f ? -1 : 1;
            Strength = Mathf.Clamp(strength, 0.1f, 1f);
            Move = move;
            ElapsedFrames = 0;
            cooldowns[(int)move] = PlayerMoveCatalog.Get(move).Recharge;
            return true;
        }

        public void BeginFrame()
        {
            for (int i = 0; i < cooldowns.Length; i++) if (cooldowns[i] > 0) cooldowns[i]--;
            if (IsBusy) ElapsedFrames++;
        }

        public bool FinishFrame()
        {
            if (!IsBusy || ElapsedFrames < PlayerMoveCatalog.Get(Move).Duration) return false;
            Move = PlayerMove.None;
            ElapsedFrames = 0;
            return true;
        }

        public PlayerActionState Copy()
        {
            var copy = (PlayerActionState)MemberwiseClone();
            copy.cooldowns = (int[])cooldowns.Clone();
            return copy;
        }
    }
}
