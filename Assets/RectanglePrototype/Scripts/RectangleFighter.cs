using System;

namespace WelpTimeToDie.RectanglePrototype
{
    public enum FighterClass { Barbarian, Rogue, Wizard }
    public enum CombatMove { None, Move, Dash, Jump, Punch, Heavy, Block, Special, Wait }

    // Plain C# data. Prediction copies this state rather than touching the live world.
    public sealed class RectangleFighter
    {
        public string Name;
        public FighterClass Class;
        public bool IsPlayer, Armored;
        public float X, Y, VelocityX, VelocityY;
        public int Facing = 1;
        public int MaxHealth, Health, Stun, HurtFlash, HazardCooldown;
        public CombatMove Move;
        public int Elapsed;
        public float AimX = 1, AimY, Power = 1;
        public int[] Cooldowns = new int[9];
        public bool IsAlive => Health > 0;
        public bool IsReady => IsAlive && Move == CombatMove.None && Stun == 0;
        public bool IsGrounded => Y <= RectangleCombat.Ground + 0.001f;
        public int Remaining => Move == CombatMove.None ? Stun : Math.Max(0, RectangleCombat.Spec(this, Move).Duration - Elapsed);

        public RectangleFighter Copy()
        {
            var copy = (RectangleFighter)MemberwiseClone();
            copy.Cooldowns = (int[])Cooldowns.Clone();
            return copy;
        }
    }
}
