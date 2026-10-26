using System.Collections.Generic;
using UnityEngine;

namespace WelpTimeToDie.RectanglePrototype
{
    public sealed class RectangleStage : MonoBehaviour
    {
        [SerializeField] private Camera arenaCamera;
        [SerializeField] private SpriteRenderer playerBody;
        [SerializeField] private SpriteRenderer enemyBody;
        private readonly List<SpriteRenderer> overlays = new List<SpriteRenderer>();
        private Vector3 initialPlayer, initialEnemy;
        private bool captured;
        private int used;
        public Camera ArenaCamera => arenaCamera;
        private static readonly Color Blue = new Color(0.25f, 0.8f, 1f);
        private static readonly Color Red = new Color(1f, 0.35f, 0.40f);

        public void SetStartingPositions(RectangleCombat world)
        {
            if (!captured)
            {
                initialPlayer = playerBody.transform.position;
                initialEnemy = enemyBody.transform.position;
                captured = true;
            }
            SetPosition(world.Player, initialPlayer);
            SetPosition(world.Fighters[1], initialEnemy);
            var enemy = world.Fighters[1];
            enemy.Move = CombatMove.None;
            enemy.Elapsed = 0;
            System.Array.Clear(enemy.Cooldowns, 0, enemy.Cooldowns.Length);
            world.PlanEnemies();
        }

        private static void SetPosition(RectangleFighter fighter, Vector3 position)
        {
            fighter.X = Mathf.Clamp(position.x, 0.6f, RectangleCombat.Width - 0.6f);
            fighter.Y = Mathf.Max(RectangleCombat.Ground, position.y - 0.9f);
        }

        public void Draw(RectangleCombat world, float aimDirection = 0, bool preview = false, RectangleCombat framing = null, RectangleCombat next = null, float blend = 0)
        {
            used = 0;
            DrawBody(playerBody, world.Player, next?.Player, blend, Blue);
            DrawBody(enemyBody, world.Fighters[1], next?.Fighters[1], blend, Red);
            if (aimDirection != 0 && world.Player.IsAlive)
                Box(world.Player.X + aimDirection * 0.9f, world.Player.Y + 0.9f, 0.35f, 0.08f, Blue);
            if (preview)
            {
                playerBody.color = new Color(playerBody.color.r, playerBody.color.g, playerBody.color.b, 0.65f);
                enemyBody.color = new Color(enemyBody.color.r, enemyBody.color.g, enemyBody.color.b, 0.65f);
            }
            for (int i = used; i < overlays.Count; i++) overlays[i].enabled = false;
            FitCamera(framing ?? world);
        }

        private void DrawBody(SpriteRenderer body, RectangleFighter fighter, RectangleFighter next, float blend, Color color)
        {
            float x = Mathf.Lerp(fighter.X, next?.X ?? fighter.X, blend);
            float y = Mathf.Lerp(fighter.Y, next?.Y ?? fighter.Y, blend);
            CombatMove action = fighter.Move;
            float elapsed = fighter.Elapsed + (next != null ? blend : 0);
            if (action == CombatMove.None && next != null && next.Move != CombatMove.None && next.Elapsed == 1)
            { action = next.Move; elapsed = blend; }
            int facing = fighter.Move == CombatMove.None && next != null && next.Elapsed == 1 ? next.Facing : fighter.Facing;
            var spec = RectangleCombat.Spec(fighter, action);
            float strike = spec.Damage > 0 && !spec.Projectile
                ? Mathf.SmoothStep(0, 1, Mathf.InverseLerp(spec.Startup - 1, spec.Startup, elapsed))
                    * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(spec.Startup + spec.Active, spec.Duration, elapsed))) : 0;
            body.transform.position = new Vector3(x, y + (fighter.IsAlive ? 0.9f : 0.15f), 0);
            body.transform.localScale = fighter.IsAlive ? new Vector3(0.9f, 1.8f, 1) : new Vector3(1.8f, 0.3f, 1);
            body.transform.rotation = Quaternion.identity;
            body.color = !fighter.IsAlive ? Color.gray : fighter.HurtFlash > 0 ? Color.Lerp(color, Color.white, fighter.HurtFlash / 4f) : color;
            if (!fighter.IsAlive) return;
            Box(x, y + 2.1f, 1.1f, 0.07f, new Color(0.2f, 0.25f, 0.3f));
            float width = 1.1f * fighter.Health / fighter.MaxHealth;
            if (width > 0) Box(x - 0.55f + width / 2, y + 2.1f, width, 0.07f, color);
            Box(x + facing * 0.33f, y + 1.15f, 0.12f, 0.18f, Color.white);
            if (fighter.HurtFlash > 0)
            {
                float flash = fighter.HurtFlash / 4f;
                float side = fighter.VelocityX < 0 ? 1 : -1;
                for (int i = -1; i <= 1; i++)
                    Box(x + side * (0.55f + (1 - flash) * 0.3f), y + 0.9f + i * 0.24f,
                        0.28f * flash, 0.055f, new Color(1, 0.85f, 0.45f, flash), i * 35);
            }
            if (action == CombatMove.Block && fighter.Stun == 0)
                Box(x + facing * 0.62f, y + 0.9f, 0.08f, 1.65f, new Color(0.55f, 0.9f, 1, 0.8f));
            if (fighter.Stun > 0 || spec.Damage <= 0 || spec.Projectile || strike <= 0.001f) return;
            // The limb follows the existing strike timing; presentation never changes collision.
            bool kick = action == CombatMove.Kick || action == CombatMove.DashKick;
            float height = kick ? 0.55f : 1.15f;
            float extension = spec.Reach * strike;
            float swing = Mathf.InverseLerp(spec.Startup - 1, spec.Startup + spec.Active, elapsed);
            float angle = spec.Launch > 0 ? facing * Mathf.Lerp(-25, 65, swing) : 0;
            Color strikeColor = new Color(color.r, color.g, color.b, Mathf.Clamp01(strike * 4));
            float dx = Mathf.Cos(angle * Mathf.Deg2Rad) * extension * facing;
            float dy = Mathf.Sin(angle * Mathf.Deg2Rad) * extension * facing;
            float shoulderX = x + facing * 0.38f;
            Box(shoulderX + dx / 2, y + height + dy / 2,
                extension, (kick ? 0.23f : 0.18f) * Mathf.Sqrt(strike), strikeColor, angle);
            Box(shoulderX + dx, y + height + dy,
                (kick ? 0.32f : 0.24f) * strike, 0.27f * strike, strikeColor, angle);
            if (strike > 0.5f)
                Box(shoulderX + dx / 2, y + height + dy / 2 - 0.18f,
                    extension * 0.75f, 0.045f, new Color(color.r, color.g, color.b, strike * 0.25f), angle);
        }

        private void Box(float x, float y, float width, float height, Color color, float angle = 0)
        {
            if (used == overlays.Count)
            {
                var go = new GameObject("Combat overlay");
                go.transform.SetParent(transform, false);
                var sprite = go.AddComponent<SpriteRenderer>();
                sprite.sprite = playerBody.sprite;
                sprite.sharedMaterial = playerBody.sharedMaterial;
                sprite.sortingOrder = 10;
                overlays.Add(sprite);
            }
            var item = overlays[used++];
            item.enabled = true;
            item.transform.position = new Vector3(x, y, 0);
            item.transform.localScale = new Vector3(width, height, 1);
            item.transform.rotation = Quaternion.Euler(0, 0, angle);
            item.color = color;
        }

        private void FitCamera(RectangleCombat world)
        {
            if (arenaCamera == null) return;
            float left = Mathf.Min(world.Player.X, world.Fighters[1].X);
            float right = Mathf.Max(world.Player.X, world.Fighters[1].X);
            float size = Mathf.Max(4.2f, (right - left + 7) / (2 * Mathf.Max(0.3f, arenaCamera.aspect)));
            float smoothing = Application.isPlaying ? 1 - Mathf.Exp(-10 * Time.unscaledDeltaTime) : 1;
            arenaCamera.orthographicSize = Mathf.Lerp(arenaCamera.orthographicSize, size, smoothing);
            arenaCamera.transform.position = Vector3.Lerp(arenaCamera.transform.position,
                new Vector3((left + right) / 2, size + 0.2f, -10), smoothing);
        }
    }
}
