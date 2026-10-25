using System;
using System.Collections.Generic;

namespace WelpTimeToDie.RectanglePrototype
{
    // A single immutable-by-convention rehearsal, shared by Watch and Go.
    // No extra hypothetical player turns occur after the move being demonstrated.
    public sealed class RectanglePlan
    {
        public readonly List<RectangleCombat> Frames = new List<RectangleCombat>();
        public string Summary { get; private set; }
        public string MoveName { get; private set; }

        public static RectanglePlan Create(RectangleCombat live, CombatMove move, float direction)
        {
            if (live.Finished || (live.Player.IsReady && !live.CanStart(0, move))) return null;
            var simulation = live.Copy();
            var plan = new RectanglePlan { MoveName = live.Player.IsReady ? RectangleCombat.Spec(live.Player, move).Name : "Finish move" };
            if (simulation.Player.IsReady) simulation.Start(0, move, direction, 0);
            // Enemy AI makes its own decisions. Only the human's next opening needs a pause.
            // The 24-frame cap is a safety checkpoint for unusually long stun sequences.
            for (int frame = 0; frame < RectangleCombat.PreviewFrames; frame++)
            {
                simulation.Step();
                simulation.PlanEnemies();
                plan.Frames.Add(simulation.Copy());
                if (simulation.Player.IsReady || simulation.Finished) break;
            }
            int dealt = live.Fighters[1].Health - simulation.Fighters[1].Health;
            int taken = live.Player.Health - simulation.Player.Health;
            if (simulation.Lost) plan.Summary = "You would die. Try another move.";
            else if (simulation.Won) plan.Summary = "Enemy defeated. You win.";
            else if (dealt > 0 && taken > 0) plan.Summary = $"Both get hit: enemy loses {dealt} HP; you lose {taken} HP.";
            else if (dealt > 0) plan.Summary = $"Attack hits: enemy loses {dealt} HP. You take no damage.";
            else if (taken > 0) plan.Summary = $"You get hit: lose {taken} HP. Try blocking or moving away.";
            else if (!live.Player.IsReady) plan.Summary = "Your move finishes. Ready to choose again.";
            else if (move == CombatMove.Punch) plan.Summary = "Attack misses. Face the enemy and move closer.";
            else if (move == CombatMove.Move) plan.Summary = Math.Abs(simulation.Player.X - live.Player.X) < 0.05f
                ? "Movement blocked by the arena edge." : "Move finishes safely. Neither fighter takes damage.";
            else if (move == CombatMove.Block)
            {
                var enemy = simulation.Fighters[1];
                var enemyMove = RectangleCombat.Spec(enemy, enemy.Move);
                plan.Summary = enemy.Move != CombatMove.None && enemyMove.Damage > 0 && enemyMove.Startup > enemy.Elapsed
                    ? "Block ends before the enemy strikes. You may need to block again."
                    : "You guard safely. Neither fighter takes damage.";
            }
            else plan.Summary = "Time passes. Neither fighter takes damage.";
            return plan;
        }
    }
}
