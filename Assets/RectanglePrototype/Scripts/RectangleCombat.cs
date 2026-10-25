using System;
using System.Collections.Generic;

namespace WelpTimeToDie.RectanglePrototype
{
    public struct MoveSpec
    {
        public string Name;
        public int Startup, Duration, Recharge, Damage, Stun;
        public float Reach, Push;
        public bool Projectile, Unstoppable;

        public MoveSpec(string name, int startup, int duration, int recharge = 0,
            int damage = 0, float reach = 0, float push = 0, int stun = 0,
            bool projectile = false, bool unstoppable = false)
        {
            Name = name; Startup = startup; Duration = duration; Recharge = recharge;
            Damage = damage; Reach = reach; Push = push; Stun = stun;
            Projectile = projectile; Unstoppable = unstoppable;
        }
    }

    public sealed class RectangleProjectile
    {
        public float X, Y, VelocityX, VelocityY;
        public int Owner;
        public MoveSpec Spec;
        public RectangleProjectile Copy() => (RectangleProjectile)MemberwiseClone();
    }

    // Private fixed-frame simulation. No Unity clocks, physics, objects, or global state.
    // Preview and playback both call Step: one frame is exactly 1/24 simulated second.
    public sealed class RectangleCombat
    {
        public const int FramesPerSecond = 24, PreviewFrames = 24;
        public const float Width = 30, Height = 12, Ground = 1.4f;
        public const float SpikeLeft = 17, SpikeRight = 20;
        public readonly List<RectangleFighter> Fighters = new List<RectangleFighter>();
        public readonly List<RectangleProjectile> Projectiles = new List<RectangleProjectile>();
        public bool HazardsEnabled { get; set; } = true;
        public int Frame { get; private set; }
        public string LastEvent { get; private set; } = "Read enemy intent, choose a move, then preview before committing.";
        public RectangleFighter Player => Fighters[0];
        public bool Lost => !Player.IsAlive;
        public bool Won
        {
            get { for (int i = 1; i < Fighters.Count; i++) if (Fighters[i].IsAlive) return false; return true; }
        }
        public bool Finished => Lost || Won;
        public bool SpikesActive => HazardsEnabled && Frame % 48 >= 8 && Frame % 48 < 24;
        public bool SpikesWarning => HazardsEnabled && Frame % 48 < 8;

        public static RectangleCombat Create(FighterClass character)
        {
            var world = new RectangleCombat();
            int hp = character == FighterClass.Barbarian ? 14 : character == FighterClass.Rogue ? 10 : 9;
            world.Fighters.Add(new RectangleFighter { Name = "YOU", Class = character, IsPlayer = true, X = 8, Y = Ground, MaxHealth = hp, Health = hp });
            world.Fighters.Add(new RectangleFighter { Name = "GUARD", Class = FighterClass.Barbarian, X = 11, Y = Ground, Facing = -1, MaxHealth = 7, Health = 7 });
            world.Fighters.Add(new RectangleFighter { Name = "GOLEM", Class = FighterClass.Barbarian, Armored = true, X = 24, Y = Ground, Facing = -1, MaxHealth = 10, Health = 10 });
            world.PlanEnemies();
            return world;
        }

        public static RectangleCombat CreateBasic(FighterClass character)
        {
            var world = Create(character);
            world.Fighters.RemoveAt(2);
            world.Fighters[1].Name = "ENEMY";
            world.HazardsEnabled = false;
            return world;
        }

        public RectangleCombat Copy()
        {
            var copy = new RectangleCombat { Frame = Frame, LastEvent = LastEvent, HazardsEnabled = HazardsEnabled };
            foreach (var fighter in Fighters) copy.Fighters.Add(fighter.Copy());
            foreach (var projectile in Projectiles) copy.Projectiles.Add(projectile.Copy());
            return copy;
        }

        public static MoveSpec Spec(RectangleFighter fighter, CombatMove move)
        {
            switch (move)
            {
                case CombatMove.Move: return new MoveSpec("Step", 0, 8);
                case CombatMove.Dash: return new MoveSpec("Dash", 0, 6, 12);
                case CombatMove.Jump: return new MoveSpec("Jump", 0, 12, 16);
                case CombatMove.Punch: return new MoveSpec("Punch", 4, 9, 12, 2, 2.3f, 3, 4);
                case CombatMove.Heavy: return fighter.Armored
                    ? new MoveSpec("UNSTOPPABLE SLAM", 12, 22, 28, 4, 3.2f, 9, 8, unstoppable: true)
                    : new MoveSpec("Heavy strike", 9, 18, 24, 4, 3, 8, 7);
                case CombatMove.Block: return new MoveSpec("Block", 0, 8, 4);
                case CombatMove.Special:
                    if (fighter.Class == FighterClass.Wizard) return new MoveSpec("Fireball", 10, 16, 28, 3, 0, 7, 6, true);
                    if (fighter.Class == FighterClass.Rogue) return new MoveSpec("Throw dagger", 5, 11, 18, 2, 0, 3, 4, true);
                    return new MoveSpec("Uppercut", 7, 14, 22, 3, 2.6f, 6, 8);
                case CombatMove.Wait: return new MoveSpec("Hold", 0, 4);
                default: return new MoveSpec("Ready", 0, 0);
            }
        }

        public bool CanStart(int index, CombatMove move)
        {
            if (Finished || index < 0 || index >= Fighters.Count || move == CombatMove.None) return false;
            var fighter = Fighters[index];
            return fighter.IsReady && fighter.Cooldowns[(int)move] == 0 && (move != CombatMove.Jump || fighter.IsGrounded);
        }

        public bool Start(int index, CombatMove move, float aimX, float aimY, float power = 1)
        {
            if (!CanStart(index, move)) return false;
            var fighter = Fighters[index];
            double length = Math.Sqrt(aimX * aimX + aimY * aimY);
            if (length < 0.001) { aimX = fighter.Facing; aimY = 0; length = 1; }
            fighter.AimX = (float)(aimX / length); fighter.AimY = (float)(aimY / length);
            if (Math.Abs(aimX) > 0.01f) fighter.Facing = aimX < 0 ? -1 : 1;
            fighter.Move = move; fighter.Elapsed = 0;
            fighter.Power = Clamp(power, 0.1f, 1);
            fighter.Cooldowns[(int)move] = Spec(fighter, move).Recharge;
            return true;
        }

        // AI states: approach, wind-up/attack, recovery, stunned, dead.
        // Planning while paused exposes already committed enemy intent to the player.
        public void PlanEnemies()
        {
            if (Finished) return;
            for (int i = 1; i < Fighters.Count; i++)
            {
                var enemy = Fighters[i];
                if (!enemy.IsReady) continue;
                float dx = Player.X - enemy.X;
                CombatMove move = Math.Abs(dx) > 3.1f ? CombatMove.Move
                    : enemy.Cooldowns[(int)CombatMove.Heavy] == 0 ? CombatMove.Heavy
                    : enemy.Cooldowns[(int)CombatMove.Punch] == 0 ? CombatMove.Punch : CombatMove.Wait;
                Start(i, move, dx, 0);
            }
        }

        private struct Hit { public int Attacker, Target; public MoveSpec Spec; public float Direction; }

        // True means a living fighter reached an action/stun boundary, or combat ended.
        public bool Step()
        {
            if (Finished) return true;
            PlanEnemies();
            Frame++;
            bool boundary = false;
            var hits = new List<Hit>();
            var stunnedThisFrame = new HashSet<RectangleFighter>();
            foreach (var fighter in Fighters)
            {
                if (!fighter.IsAlive) continue;
                for (int c = 0; c < fighter.Cooldowns.Length; c++) if (fighter.Cooldowns[c] > 0) fighter.Cooldowns[c]--;
                if (fighter.HurtFlash > 0) fighter.HurtFlash--;
                if (fighter.HazardCooldown > 0) fighter.HazardCooldown--;
                if (fighter.Stun > 0)
                {
                    stunnedThisFrame.Add(fighter);
                    if (--fighter.Stun == 0) boundary = true;
                    continue;
                }
                if (fighter.Move == CombatMove.None) continue;
                fighter.Elapsed++;
                if (fighter.Move == CombatMove.Jump && fighter.Elapsed == 1) fighter.VelocityY = 14;
            }
            // Two motion substeps per combat frame; the player can act while airborne.
            for (int substep = 0; substep < 2; substep++)
            {
                const float dt = 1f / (FramesPerSecond * 2);
                foreach (var fighter in Fighters)
                {
                    if (!fighter.IsAlive) continue;
                    float move = 0;
                    if (!stunnedThisFrame.Contains(fighter))
                    {
                        if (fighter.Move == CombatMove.Move) move = fighter.Facing * 2.4f / 8 * FramesPerSecond;
                        if (fighter.Move == CombatMove.Dash) move = fighter.Facing * 5f * fighter.Power / 6 * FramesPerSecond;
                        if (fighter.Move == CombatMove.Jump) move = fighter.Facing * 3;
                    }
                    fighter.X = Clamp(fighter.X + (move + fighter.VelocityX) * dt, 0.6f, Width - 0.6f);
                    fighter.VelocityX *= 0.90f;
                    fighter.VelocityY -= 30 * dt; fighter.Y += fighter.VelocityY * dt;
                    if (fighter.Y < Ground) { fighter.Y = Ground; fighter.VelocityY = 0; }
                }
                StepProjectiles(dt, hits);
            }
            // Collect before applying: strikes on the same frame trade fairly.
            for (int i = 0; i < Fighters.Count; i++)
            {
                var attacker = Fighters[i];
                if (!attacker.IsAlive || stunnedThisFrame.Contains(attacker) || attacker.Move == CombatMove.None) continue;
                MoveSpec spec = Spec(attacker, attacker.Move);
                if (spec.Damage == 0 || attacker.Elapsed != spec.Startup) continue;
                if (spec.Projectile)
                {
                    float speed = attacker.Class == FighterClass.Rogue ? 26 : 18;
                    Projectiles.Add(new RectangleProjectile { Owner = i, X = attacker.X + attacker.AimX * 0.7f, Y = attacker.Y + 0.9f + attacker.AimY * 0.7f,
                        VelocityX = attacker.AimX * speed, VelocityY = attacker.AimY * speed, Spec = spec });
                    LastEvent = $"{attacker.Name} released {spec.Name}. Travel time still matters.";
                }
                else for (int t = 0; t < Fighters.Count; t++)
                    if (Opponents(i, t) && Fighters[t].IsAlive && MeleeOverlaps(attacker, Fighters[t], spec.Reach))
                        hits.Add(new Hit { Attacker = i, Target = t, Spec = spec, Direction = attacker.Facing });
            }
            foreach (var hit in hits) ApplyHit(hit);
            foreach (var fighter in Fighters)
            {
                if (!fighter.IsAlive) { fighter.Move = CombatMove.None; continue; }
                if (SpikesActive && fighter.X + 0.45f > SpikeLeft && fighter.X - 0.45f < SpikeRight && fighter.Y < Ground + 0.5f && fighter.HazardCooldown == 0)
                {
                    fighter.Health = Math.Max(0, fighter.Health - 2); fighter.HurtFlash = 4; fighter.HazardCooldown = 12;
                    LastEvent = $"{fighter.Name} took 2 spike damage. Jump traps or knock enemies into them.";
                }
                if (!fighter.IsAlive) { fighter.Move = CombatMove.None; boundary = true; continue; }
                if (fighter.Move != CombatMove.None && fighter.Elapsed >= Spec(fighter, fighter.Move).Duration)
                { fighter.Move = CombatMove.None; fighter.Elapsed = 0; boundary = true; }
            }
            return boundary || Finished;
        }

        private bool Opponents(int a, int b) => a != b && Fighters[a].IsPlayer != Fighters[b].IsPlayer;
        private static bool MeleeOverlaps(RectangleFighter a, RectangleFighter b, float reach)
        {
            float forward = (b.X - a.X) * a.Facing;
            return forward > 0 && forward < reach + 0.9f && Math.Abs(b.Y - a.Y) < 1.6f;
        }

        private void StepProjectiles(float dt, List<Hit> hits)
        {
            for (int p = Projectiles.Count - 1; p >= 0; p--)
            {
                var shot = Projectiles[p];
                float x0 = shot.X, y0 = shot.Y;
                shot.X += shot.VelocityX * dt; shot.Y += shot.VelocityY * dt;
                bool remove = shot.X < 0 || shot.X > Width || shot.Y < Ground || shot.Y > Height;
                for (int t = 0; t < Fighters.Count && !remove; t++)
                {
                    var target = Fighters[t];
                    if (!target.IsAlive || !Opponents(shot.Owner, t)) continue;
                    if (SegmentHitsBox(x0, y0, shot.X, shot.Y, target.X - 0.65f, target.Y - 0.2f, target.X + 0.65f, target.Y + 2f))
                    {
                        hits.Add(new Hit { Attacker = shot.Owner, Target = t, Spec = shot.Spec, Direction = shot.VelocityX < 0 ? -1 : 1 });
                        remove = true;
                    }
                }
                if (remove) Projectiles.RemoveAt(p);
            }
        }

        private static bool SegmentHitsBox(float x0, float y0, float x1, float y1, float left, float bottom, float right, float top)
        {
            float min = 0, max = 1;
            return Clip(x0, x1 - x0, left, right, ref min, ref max) && Clip(y0, y1 - y0, bottom, top, ref min, ref max);
        }

        private static bool Clip(float start, float delta, float low, float high, ref float min, ref float max)
        {
            if (Math.Abs(delta) < 0.000001f) return start >= low && start <= high;
            float a = (low - start) / delta, b = (high - start) / delta;
            if (a > b) { float swap = a; a = b; b = swap; }
            min = Math.Max(min, a); max = Math.Min(max, b);
            return min <= max;
        }

        private void ApplyHit(Hit hit)
        {
            var attacker = Fighters[hit.Attacker]; var target = Fighters[hit.Target];
            if (!target.IsAlive) return;
            bool blocking = target.Move == CombatMove.Block && target.Stun == 0 && (attacker.X - target.X) * target.Facing >= 0;
            int damage = blocking ? Math.Max(0, hit.Spec.Damage - 3) : hit.Spec.Damage;
            target.Health = Math.Max(0, target.Health - damage); target.HurtFlash = 4;
            if (blocking) { LastEvent = $"{target.Name} blocked {hit.Spec.Name} ({damage} damage)."; return; }
            bool armored = target.Move != CombatMove.None && Spec(target, target.Move).Unstoppable;
            bool counter = target.Move != CombatMove.None && target.Elapsed < Spec(target, target.Move).Startup && !armored;
            if (!armored || !target.IsAlive)
            {
                target.Move = CombatMove.None; target.Elapsed = 0;
                target.Stun = target.IsAlive ? hit.Spec.Stun : 0;
                target.VelocityX = hit.Direction * hit.Spec.Push;
                if (attacker.Move == CombatMove.Special && attacker.Class == FighterClass.Barbarian) target.VelocityY = 8;
            }
            LastEvent = !target.IsAlive ? $"{target.Name} defeated." : counter
                ? $"COUNTER! {target.Name}'s wind-up cancelled. Stunned for {hit.Spec.Stun} frames."
                : $"{target.Name} took {damage} damage{(armored ? "; unstoppable attack continues" : "")}.";
        }

        public static float Clamp(float value, float low, float high) => Math.Max(low, Math.Min(high, value));
    }
}
