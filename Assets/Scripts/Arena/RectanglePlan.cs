using System;
using System.Collections.Generic;

namespace WelpTimeToDie.RectanglePrototype
{
    public sealed class RectanglePlan
    {
        public readonly List<RectangleCombat> Frames = new List<RectangleCombat>();
        public string Summary { get; private set; }
        public string MoveName { get; private set; }
        internal CombatMove StartedMove { get; private set; }

        public static RectanglePlan Create(RectangleCombat live, CombatMove move, float direction, float strength = 1)
            => Create(live, move, direction, 0, strength);

        public static int RecommendBlockDuration(RectangleCombat live, float direction)
        {
            if (!live.CanStart(0, CombatMove.Block)) return 0;
            var simulation = live.Copy();
            simulation.Start(0, CombatMove.Block, direction, 0, blockFrames: RectangleCombat.MaxBlockFrames);
            while (simulation.Player.Move == CombatMove.Block && !simulation.Finished)
                simulation.Step();
            return Math.Max(0, simulation.LastPlayerBlockFrame - live.Frame);
        }

        public static RectanglePlan Create(RectangleCombat live, CombatMove move, float aimX, float aimY, float strength,
            int blockFrames = RectangleCombat.DefaultBlockFrames)
        {
            if (live.Finished || (live.Player.IsReady && !live.CanStart(0, move))) return null;
            var simulation = live.Copy();
            var plan = new RectanglePlan { MoveName = live.Player.IsReady ? RectangleCombat.Spec(live.Player, move).Name : "Finish move" };
            if (simulation.Player.IsReady && simulation.Start(0, move, aimX, aimY, strength, blockFrames))
                plan.StartedMove = move;
            for (int frame = 0; frame < RectangleCombat.PreviewFrames; frame++)
            {
                simulation.Step();
                simulation.PlanEnemies();
                plan.Frames.Add(simulation.Copy());
                if (simulation.Player.IsReady || simulation.Finished) break;
            }
            float dealt = 0;
            for (int i = 1; i < live.Fighters.Count; i++)
                dealt += live.Fighters[i].Health - simulation.Fighters[i].Health;
            float taken = live.Player.Health - simulation.Player.Health;
            bool multipleEnemies = live.Fighters.Count > 2;
            bool golemPair = live.Fighters.Count == 3 && live.Fighters[1].IsGolem && live.Fighters[2].IsGolem;
            string enemyDamage = golemPair ? $"golems lose {dealt} HP total"
                : multipleEnemies ? $"enemies lose {dealt} HP total" : $"enemy loses {dealt} HP";
            if (simulation.Lost) plan.Summary = "You would die. Try another move.";
            else if (simulation.Won) plan.Summary = golemPair ? "Both golems defeated. You win."
                : multipleEnemies ? "All enemies defeated. You win." : "Enemy defeated. You win.";
            else if (dealt > 0 && taken > 0) plan.Summary = $"Trade hits: {enemyDamage}. You lose {taken} HP.";
            else if (dealt > 0) plan.Summary = $"Attack hits: {enemyDamage}. You take no damage.";
            else if (simulation.LastPlayerBlockFrame > live.Frame)
                plan.Summary = $"You block an attack and lose {taken} HP.";
            else if (move == CombatMove.Block && taken > 0) plan.Summary = $"You take {taken} HP during this move.";
            else if (taken > 0) plan.Summary = $"You get hit: lose {taken} HP. Try blocking or moving away.";
            else if (!live.Player.IsReady) plan.Summary = "Your move finishes. Ready to choose again.";
            else if (move == CombatMove.Sweep) plan.Summary = "Sweep misses. Aim at a nearby grounded enemy.";
            else if (RectangleCombat.Spec(live.Player, move).Damage > 0) plan.Summary = "Attack misses. Face the enemy and move closer.";
            else if (move == CombatMove.Backstep) plan.Summary = "You retreat while still facing the chosen side.";
            else if (move == CombatMove.Move) plan.Summary = Math.Abs(simulation.Player.X - live.Player.X) < 0.05f
                ? "Movement blocked." : "Move finishes safely. Nobody takes damage.";
            else if (move == CombatMove.Block)
            {
                plan.Summary = "You guard safely. Nobody takes damage.";
                for (int i = 1; i < simulation.Fighters.Count; i++)
                {
                    var enemy = simulation.Fighters[i];
                    var enemyMove = RectangleCombat.Spec(enemy, enemy.Move);
                    if (!enemy.IsAlive || enemyMove.Damage <= 0 || enemyMove.Startup <= enemy.Elapsed) continue;
                    string attacker = multipleEnemies ? enemy.Name : "the enemy";
                    plan.Summary = $"Block ends before {attacker} strikes. You may need to block again.";
                    break;
                }
            }
            else plan.Summary = "Time passes. Nobody takes damage.";
            return plan;
        }
    }
}
