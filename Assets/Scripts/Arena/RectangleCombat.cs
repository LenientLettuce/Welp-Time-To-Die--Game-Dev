using System;
using System.Collections.Generic;


namespace WelpTimeToDie.RectanglePrototype
{
    public struct MoveSpec
    {
        public string Name;
        public int Startup, Duration, Recharge, Damage, Stun, Active;
        public float Reach, Push, Launch;
        public bool Projectile, Unstoppable;

        public MoveSpec(string name, int startup, int duration, int recharge = 0,
            int damage = 0, float reach = 0, float push = 0, int stun = 0,
            bool projectile = false, bool unstoppable = false, int active = 1, float launch = 0)
        {
            Name = name; Startup = startup; Duration = duration; Recharge = recharge;
            Damage = damage; Reach = reach; Push = push; Stun = stun;
            Projectile = projectile; Unstoppable = unstoppable; Active = active; Launch = launch;
        }
    }

    public sealed class RectangleProjectile
    {
        public float X, Y, VelocityX, VelocityY;
        public int Owner;
        public MoveSpec Spec;
        public RectangleProjectile Copy() => (RectangleProjectile)MemberwiseClone();
    }

    public sealed class RectangleCombat
    {
        public const int FramesPerSecond = 24, PreviewFrames = 24;
        public const float Width = 30, Height = 12, Ground = 1.4f;
        public const float SpikeLeft = 17, SpikeRight = 20;
        public const float BodySpacing = 1.1f;
        private const float GolemSpeedMultiplier = 1.5f;
        private const int MotionSubsteps = 2;
        private const float MotionStep = 1f / (FramesPerSecond * MotionSubsteps);
        private const float StepSpeed = 7.2f, DashSpeed = 20f, DashKickSpeed = 10.5f;
        private const float GroundAcceleration = 120f, AirAcceleration = 30f;
        private const float GroundBrake = 32f, GroundDrag = 2.5f, AirBrake = 2f, HitstunBrake = 22f;
        private const float JumpSpeed = 14.5f, JumpDrift = 3f;
        private const float RiseGravity = 38f, FallGravity = 54f, MaxFallSpeed = 26f;
        public const float LegacyMovementPower = 0.3f, MinMovementPower = 0.1f;
        public readonly List<RectangleFighter> Fighters = new List<RectangleFighter>();
        public readonly List<RectangleProjectile> Projectiles = new List<RectangleProjectile>();
        public bool HazardsEnabled { get; set; } = true;
        public bool TacticalEnemies { get; set; }
        public RectangleTerrain Terrain { get; set; }
        private float LeftLimit(RectangleFighter fighter) =>
            Terrain == null ? 0.6f * fighter.SizeMultiplier : Terrain.MinX + fighter.HalfWidth;
        private float RightLimit(RectangleFighter fighter) =>
            Terrain == null ? Width - 0.6f * fighter.SizeMultiplier : Terrain.MaxX - fighter.HalfWidth;
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
            world.Fighters.Add(new RectangleFighter { Name = "GOLEM", Class = FighterClass.Barbarian, IsGolem = true, Armored = true, X = 24, Y = Ground, Facing = -1, MaxHealth = 15, Health = 15 });
            world.PlanEnemies();
            return world;
        }

        public static RectangleCombat CreateBasic(FighterClass character, bool golemEncounter = false)
        {
            var world = Create(character);
            world.Fighters.RemoveAt(2);
            var enemy = world.Fighters[1];
            enemy.IsGolem = golemEncounter;
            enemy.Name = golemEncounter ? "GOLEM" : "ENEMY";
            if (golemEncounter) enemy.Health = enemy.MaxHealth = enemy.MaxHealth * 3 / 2;
            world.HazardsEnabled = false;
            return world;
        }

        public RectangleCombat Copy()
        {
            var copy = new RectangleCombat
            {
                Frame = Frame, LastEvent = LastEvent, HazardsEnabled = HazardsEnabled,
                TacticalEnemies = TacticalEnemies, Terrain = Terrain?.Copy()
            };
            foreach (var fighter in Fighters) copy.Fighters.Add(fighter.Copy());
            foreach (var projectile in Projectiles) copy.Projectiles.Add(projectile.Copy());
            return copy;
        }

        public static MoveSpec Spec(RectangleFighter fighter, CombatMove move)
        {
            var spec = BaseSpec(fighter, move);
            if (!fighter.IsGolem) return spec;

            int startup = spec.Startup * 6 / 5;
            spec.Duration += startup - spec.Startup;
            spec.Startup = startup;
            spec.Damage = spec.Damage * 3 / 2;
            return spec;
        }

        private static MoveSpec BaseSpec(RectangleFighter fighter, CombatMove move)
        {
            switch (move)
            {
                case CombatMove.Move: return new MoveSpec("Step", 0, 8);
                case CombatMove.Dash: return new MoveSpec("Dash", 0, 6, 12);
                case CombatMove.Jump: return new MoveSpec("Jump", 0, 12, 16);
                case CombatMove.Punch: return new MoveSpec("Punch", 4, 9, 12, 2, 2.3f, 3, 4);
                case CombatMove.Uppercut: return new MoveSpec("Uppercut", 6, 15, 15, 3, 2.1f, 2, 12, active: 2, launch: 11);
                case CombatMove.Kick: return new MoveSpec(fighter.IsPlayer ? "Kick" : "Shove", 7, 16, 16, 3, 3.3f, 9, 9, active: 3);
                case CombatMove.DashKick: return new MoveSpec(fighter.IsPlayer ? "Dash kick" : "Rush", 8, 21, 24, 3, 2.5f, 11, 10, active: 4);
                case CombatMove.Sweep: return new MoveSpec("Sweep", 5, 12, 18, 1, 1.9f, 1, 11, active: 2);
                case CombatMove.Backstep: return new MoveSpec("Backstep", 0, 5, 14);
                case CombatMove.Heavy: return fighter.Armored
                    ? new MoveSpec("UNSTOPPABLE SLAM", 12, 22, 28, 4, 3.2f, 9, 8, unstoppable: true)
                    : new MoveSpec("Heavy strike", 9, 18, 24, 4, 3, 8, 7);
                case CombatMove.Block: return new MoveSpec("Block", 0, 8, 4);
                case CombatMove.Special:
                    if (fighter.Class == FighterClass.Wizard) return new MoveSpec("Fireball", 10, 16, 28, 3, 0, 7, 6, true);
                    if (fighter.Class == FighterClass.Rogue) return new MoveSpec("Throw dagger", 5, 11, 18, 2, 0, 3, 4, true);
                    return new MoveSpec("Uppercut", 7, 14, 22, 3, 2.6f, 6, 8, launch: 8);
                case CombatMove.Wait: return new MoveSpec("Hold", 0, 4);
                default: return new MoveSpec("Ready", 0, 0);
            }
        }

        public static bool UsesMovementAim(CombatMove move) =>
            move == CombatMove.Dash || move == CombatMove.Jump || move == CombatMove.DashKick;

        public bool CanStart(int index, CombatMove move)
        {
            if (Finished || index < 0 || index >= Fighters.Count || move <= CombatMove.None || (int)move >= Enum.GetValues(typeof(CombatMove)).Length) return false;
            var fighter = Fighters[index];
            bool needsGround = (move == CombatMove.Jump && !fighter.IsPlayer) ||
                move == CombatMove.Sweep || move == CombatMove.Backstep;
            return fighter.IsReady && fighter.Cooldowns[(int)move] == 0 && (!needsGround || fighter.IsGrounded);
        }

        public bool Start(int index, CombatMove move, float aimX, float aimY, float power = 1)
        {
            if (!CanStart(index, move)) return false;
            var fighter = Fighters[index];
            double length = Math.Sqrt(aimX * aimX + aimY * aimY);
            if (length < 0.001) { aimX = fighter.Facing; aimY = 0; length = 1; }
            fighter.AimX = (float)(aimX / length); fighter.AimY = (float)(aimY / length);
            if (Math.Abs(aimX) > 0.01f) fighter.Facing = aimX < 0 ? -1 : 1;
            fighter.Move = move; fighter.Elapsed = 0; fighter.HitTargets = 0;
            fighter.Power = Clamp(power, MinMovementPower, 1);
            fighter.Cooldowns[(int)move] = Spec(fighter, move).Recharge;
            return true;
        }

        public void PlanEnemies()
        {
            if (Finished) return;
            for (int i = 1; i < Fighters.Count; i++)
            {
                var enemy = Fighters[i];
                if (!enemy.IsReady) continue;
                float dx = Player.X - enemy.X;
                if (TacticalEnemies)
                {
                    Start(i, ChooseEnemyMove(i, dx), dx, 0);
                    continue;
                }
                CombatMove move = Math.Abs(dx) > 3.1f ? CombatMove.Move
                    : enemy.Cooldowns[(int)CombatMove.Heavy] == 0 ? CombatMove.Heavy
                    : enemy.Cooldowns[(int)CombatMove.Punch] == 0 ? CombatMove.Punch : CombatMove.Wait;
                if (Terrain != null && enemy.IsGrounded && enemy.Cooldowns[(int)CombatMove.Jump] == 0 &&
                    (Player.Y > enemy.Y + 1.2f || !Terrain.Fits(enemy.X + Math.Sign(dx) * 0.65f, enemy.Y + 1.01f, enemy)))
                    move = CombatMove.Jump;
                Start(i, move, dx, 0);
            }
        }

        private CombatMove ChooseEnemyMove(int index, float dx)
        {
            var enemy = Fighters[index];
            float distance = Math.Abs(dx);
            int facing = dx < 0 ? -1 : 1;
            bool clearStrike = Terrain == null || Terrain.ClearStrike(enemy, Player);
            bool inHeight = OverlapHeight(Player, enemy, 0.2f);
            float reachPadding = Player.HalfWidth + enemy.HalfWidth;
            var playerMove = Spec(Player, Player.Move);
            int untilHit = playerMove.Startup - Player.Elapsed;
            bool underThreat = inHeight && clearStrike && playerMove.Damage > 0 && !playerMove.Projectile
                && Player.Facing == -facing && untilHit > -playerMove.Active && untilHit <= 5
                && (Player.HitTargets & (1 << index)) == 0
                && distance < playerMove.Reach + reachPadding + (Player.Move == CombatMove.DashKick ? 1.5f : 0);
            if (underThreat && CanStart(index, CombatMove.Block))
                return CombatMove.Block;

            if (clearStrike && inHeight)
            {
                var heavy = Spec(enemy, CombatMove.Heavy);
                bool recovering = playerMove.Damage > 0 && Player.Elapsed >= playerMove.Startup + playerMove.Active;
                if (distance < heavy.Reach + reachPadding && CanStart(index, CombatMove.Heavy)
                    && (Player.Move == CombatMove.Block || (recovering && Player.Remaining >= heavy.Startup - 1)))
                    return CombatMove.Heavy;

                if (distance < 2.6f && CanStart(index, CombatMove.Punch)) return CombatMove.Punch;
                if (distance < 4f && CanStart(index, CombatMove.Kick)) return CombatMove.Kick;
                if (distance >= 4f && distance < 6f && enemy.IsGrounded && CanStart(index, CombatMove.DashKick)
                    && (Terrain == null || Terrain.Fits(enemy.X + facing * 0.65f, enemy.Y + 1.01f, enemy)))
                    return CombatMove.DashKick;
            }

            if (Terrain != null && CanStart(index, CombatMove.Jump) && Terrain.Fits(enemy.X, enemy.Y + 0.5f, enemy)
                && ((Player.IsGrounded && Player.Y > enemy.Y + 1.2f)
                    || !Terrain.Fits(enemy.X + facing * 0.65f, enemy.Y + 1.01f, enemy)))
                return CombatMove.Jump;

            // Keep striking distance while attacks recharge or the player lands.
            return distance > 2.4f || !clearStrike ? CombatMove.Move : CombatMove.Wait;
        }

        private struct Hit { public int Attacker, Target; public MoveSpec Spec; public float Direction; }

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
                if (fighter.Elapsed == 1) BeginMotion(fighter);
            }
            var previousX = new float[Fighters.Count];
            for (int substep = 0; substep < MotionSubsteps; substep++)
            {
                for (int i = 0; i < Fighters.Count; i++) previousX[i] = Fighters[i].X;
                foreach (var fighter in Fighters)
                {
                    if (fighter.IsAlive) AdvanceMotion(fighter, stunnedThisFrame.Contains(fighter));
                }
                SeparateBodies(previousX);
                StepProjectiles(MotionStep, hits);
            }
           for (int i = 0; i < Fighters.Count; i++)
            {
                var attacker = Fighters[i];
                if (!attacker.IsAlive || stunnedThisFrame.Contains(attacker) || attacker.Move == CombatMove.None) continue;
                MoveSpec spec = Spec(attacker, attacker.Move);
                if (spec.Damage == 0 || (attacker.Elapsed < spec.Startup || attacker.Elapsed >= spec.Startup + spec.Active)) continue;
                if (spec.Projectile)
                {
                    float speed = attacker.Class == FighterClass.Rogue ? 26 : 18;
                    Projectiles.Add(new RectangleProjectile { Owner = i, X = attacker.X + attacker.AimX * 0.7f, Y = attacker.Y + 0.9f + attacker.AimY * 0.7f,
                        VelocityX = attacker.AimX * speed, VelocityY = attacker.AimY * speed, Spec = spec });
                    LastEvent = $"{attacker.Name} released {spec.Name}. Travel time still matters.";
                }
                else for (int t = 0; t < Fighters.Count; t++)
                    if (Opponents(i, t) && Fighters[t].IsAlive && (attacker.HitTargets & (1 << t)) == 0 &&
                        (attacker.Move != CombatMove.Sweep || Fighters[t].IsGrounded) && MeleeOverlaps(attacker, Fighters[t], spec.Reach) &&
                        (Terrain == null || Terrain.ClearStrike(attacker, Fighters[t])))
                    {
                        attacker.HitTargets |= 1 << t;
                        hits.Add(new Hit { Attacker = i, Target = t, Spec = spec, Direction = attacker.Facing });
                    }
            }
            foreach (var hit in hits) ApplyHit(hit);
            foreach (var fighter in Fighters)
            {
                if (!fighter.IsAlive) { fighter.Move = CombatMove.None; continue; }
                if (SpikesActive && fighter.X + fighter.HalfWidth > SpikeLeft && fighter.X - fighter.HalfWidth < SpikeRight && fighter.Y < Ground + 0.5f && fighter.HazardCooldown == 0)
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

        private static void BeginMotion(RectangleFighter fighter)
        {
            if (fighter.IsPlayer && UsesMovementAim(fighter.Move))
            {
                float baseSpeed = fighter.Move switch
                {
                    CombatMove.Dash => DashSpeed,
                    CombatMove.DashKick => DashKickSpeed,
                    _ => (float)Math.Sqrt(JumpSpeed * JumpSpeed + JumpDrift * JumpDrift)
                };
                float speed = baseSpeed * fighter.Power / LegacyMovementPower;
                fighter.VelocityX = fighter.AimX * speed;
                fighter.VelocityY = fighter.AimY * speed;
                return;
            }

            float speedMultiplier = fighter.IsGolem ? GolemSpeedMultiplier : 1f;
            switch (fighter.Move)
            {
                case CombatMove.Move:
                    if (fighter.VelocityX * fighter.Facing < 0) fighter.VelocityX = 0;
                    break;
                case CombatMove.Jump:
                    fighter.VelocityY = JumpSpeed * speedMultiplier;
                    SetForwardSpeed(fighter, fighter.Facing, JumpDrift * speedMultiplier);
                    break;
                case CombatMove.Dash:
                    SetForwardSpeed(fighter, fighter.Facing, DashSpeed * fighter.Power * speedMultiplier);
                    break;
                case CombatMove.Backstep:
                    fighter.VelocityX = -fighter.Facing * 16f * speedMultiplier;
                    break;
                case CombatMove.DashKick:
                    SetForwardSpeed(fighter, fighter.Facing, DashKickSpeed * speedMultiplier);
                    break;
            }
        }

        private void AdvanceMotion(RectangleFighter fighter, bool stunned)
        {
            bool grounded = fighter.IsGrounded;
            var spec = Spec(fighter, fighter.Move);
            bool advancingAttack = fighter.IsGolem && fighter.Move != CombatMove.DashKick &&
                spec.Damage > 0 && !spec.Projectile && fighter.Elapsed <= spec.Startup;
            bool stepping = !stunned && (fighter.Move == CombatMove.Move || advancingAttack);
            bool dashing = !stunned && (fighter.Move == CombatMove.Dash || fighter.Move == CombatMove.Backstep ||
                (fighter.Move == CombatMove.DashKick && fighter.Elapsed <= spec.Startup));
            if (stepping)
            {
                float speedMultiplier = fighter.IsGolem ? GolemSpeedMultiplier : 1f;
                float target = fighter.Facing * StepSpeed * speedMultiplier;
                float change = (grounded ? GroundAcceleration : AirAcceleration) * MotionStep * speedMultiplier;
                fighter.VelocityX += Clamp(target - fighter.VelocityX, -change, change);
            }
            else if (!dashing)
            {
                // Passive moves coast. Hitstun slides longer on the ground, and air has very little drag.
                float speed = Math.Abs(fighter.VelocityX);
                float brake = !grounded ? AirBrake : stunned ? HitstunBrake : GroundBrake + GroundDrag * speed;
                fighter.VelocityX = Math.Sign(fighter.VelocityX) * Math.Max(0, speed - brake * MotionStep);
            }

            float gravity = fighter.VelocityY > 0 ? RiseGravity : FallGravity;
            float fallLimit = !stunned && fighter.IsPlayer && UsesMovementAim(fighter.Move)
                ? Math.Max(MaxFallSpeed, -fighter.VelocityY)
                : MaxFallSpeed;
            fighter.VelocityY = Math.Max(-fallLimit, fighter.VelocityY - gravity * MotionStep);
            float x = fighter.X + fighter.VelocityX * MotionStep;
            float y = fighter.Y + fighter.VelocityY * MotionStep;
            if (Terrain != null)
            {
                Terrain.Move(fighter, x - fighter.X, y - fighter.Y, stepping || dashing);
                return;
            }

            fighter.X = Clamp(x, LeftLimit(fighter), RightLimit(fighter));
            if (fighter.X != x) fighter.VelocityX = 0;
            fighter.Y = Math.Max(y, Ground);
            if (y <= Ground) fighter.VelocityY = 0;
        }

        private static void SetForwardSpeed(RectangleFighter fighter, float direction, float speed)
        {
            fighter.VelocityX = direction * Math.Max(speed, fighter.VelocityX * direction);
        }

        private void SeparateBodies(float[] previousX)
        {
            for (int pass = 0; pass < Fighters.Count; pass++)
                for (int a = 0; a < Fighters.Count; a++)
                    for (int b = a + 1; b < Fighters.Count; b++)
                    {
                        var first = Fighters[a]; var second = Fighters[b];
                        if (!first.IsAlive || !second.IsAlive || !OverlapHeight(first, second)) continue;
                        bool firstOnLeft = previousX[a] <= previousX[b];
                        var left = firstOnLeft ? first : second;
                        var right = firstOnLeft ? second : first;
                        float spacing = BodySpacing + left.HalfWidth + right.HalfWidth - 2 * RectangleFighter.BaseHalfWidth;
                        float overlap = spacing - (right.X - left.X);
                        if (overlap <= 0) continue;
                        float leftTravel = Math.Max(0, left.X - previousX[firstOnLeft ? a : b]);
                        float rightTravel = Math.Max(0, previousX[firstOnLeft ? b : a] - right.X);
                        float inwardTravel = leftTravel + rightTravel;
                        float leftShare = inwardTravel > 0 ? leftTravel / inwardTravel : 0.5f;
                        float leftShift = Math.Min(overlap * leftShare, left.X - LeftLimit(left));
                        float rightShift = Math.Min(overlap - leftShift, RightLimit(right) - right.X);
                        leftShift = Math.Min(overlap - rightShift, left.X - LeftLimit(left));
                        if (Terrain == null) { left.X -= leftShift; right.X += rightShift; }
                        else
                        {
                            Terrain.Move(left, -leftShift, 0, false); Terrain.Move(right, rightShift, 0, false);
                            float remaining = spacing - (right.X - left.X);
                            if (remaining > 0.001f) Terrain.Move(right, remaining, 0, false);
                            remaining = spacing - (right.X - left.X);
                            if (remaining > 0.001f) Terrain.Move(left, -remaining, 0, false);
                        }
                        if (left.VelocityX > right.VelocityX)
                        {
                            // Remove closing speed while preserving shared motion along the ground.
                            float contactSpeed = Clamp(0, right.VelocityX, left.VelocityX);
                            left.VelocityX = right.VelocityX = contactSpeed;
                        }
                    }
        }

        private bool Opponents(int a, int b) => a != b && Fighters[a].IsPlayer != Fighters[b].IsPlayer;
        private static bool OverlapHeight(RectangleFighter a, RectangleFighter b, float clearance = 0) =>
            b.Y - a.Y < a.BodyHeight - clearance && a.Y - b.Y < b.BodyHeight - clearance;

        private static bool MeleeOverlaps(RectangleFighter a, RectangleFighter b, float reach)
        {
            float forward = (b.X - a.X) * a.Facing;
            return forward > 0 && forward < reach + a.HalfWidth + b.HalfWidth && OverlapHeight(a, b, 0.2f);
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
                    float halfWidth = target.HalfWidth + 0.2f;
                    if (SegmentHitsBox(x0, y0, shot.X, shot.Y, target.X - halfWidth, target.Y - 0.2f,
                        target.X + halfWidth, target.Y + target.BodyHeight + 0.2f))
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
                SetForwardSpeed(target, hit.Direction, hit.Spec.Push);
                if (hit.Spec.Launch > 0) target.VelocityY = Math.Max(target.VelocityY, hit.Spec.Launch);
            }
            LastEvent = !target.IsAlive ? $"{target.Name} defeated." : counter
                ? $"COUNTER! {target.Name}'s wind-up cancelled. Stunned for {hit.Spec.Stun} frames."
                : $"{target.Name} took {damage} damage{(armored ? "; unstoppable attack continues" : "")}.";
        }

        public static float Clamp(float value, float low, float high) => Math.Max(low, Math.Min(high, value));
    }
}
