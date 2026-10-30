using System.Collections.Generic;
using UnityEngine;
using WelpTimeToDie.RectanglePrototype.WelpTimeToDie.RectanglePrototype;

namespace WelpTimeToDie.RectanglePrototype
{
    // The primary rectangles are saved SpriteRenderers, visible in Scene view without Play.
    // Runtime overlays only decorate these objects; no editor-time object creation is needed.
    public sealed class RectangleStage : MonoBehaviour
    {
        [SerializeField] private Camera arenaCamera;
        [SerializeField] private SpriteRenderer playerBody;
        [SerializeField] private SpriteRenderer enemyBody;
        [Tooltip("Rigged Golem.psb instance. When set, it replaces the enemy rectangle.")]
        [SerializeField] private GolemPhysicsAnimator enemyGolem;
        private readonly List<SpriteRenderer> overlays = new List<SpriteRenderer>();
        private Vector3 initialPlayer, initialEnemy;
        private bool captured;
        private int used;
        public Camera ArenaCamera => arenaCamera;
        private static readonly Color Blue = new Color(0.25f, 0.8f, 1f);
        private static readonly Color Red = new Color(1f, 0.35f, 0.40f);
        // Rectangles are positioned by their centre; the golem's pivot is at its feet.
        private const float HalfHeight = 0.9f;

        public void SetStartingPositions(RectangleCombat world)
        {
            if (!captured)
            {
                initialPlayer = playerBody.transform.position;
                initialEnemy = enemyGolem != null
                    ? enemyGolem.transform.position + new Vector3(0, HalfHeight, 0)
                    : enemyBody.transform.position;
                captured = true;
            }
            SetPosition(world.Player, initialPlayer);
            SetPosition(world.Fighters[1], initialEnemy);
            var enemy = world.Fighters[1];
            enemy.Move = CombatMove.None;
            enemy.Elapsed = 0;
            enemy.Cooldowns = new int[9];
            world.PlanEnemies();
        }

        private static void SetPosition(RectangleFighter fighter, Vector3 position)
        {
            fighter.X = Mathf.Clamp(position.x, 0.6f, RectangleCombat.Width - 0.6f);
            fighter.Y = Mathf.Max(RectangleCombat.Ground, position.y - HalfHeight);
        }

        public void Draw(RectangleCombat world, float aimDirection = 0, bool preview = false, RectangleCombat framing = null)
        {
            used = 0;
            DrawBody(playerBody, world.Player, Blue);
            if (enemyGolem != null)
            {
                enemyGolem.Show(world.Fighters[1], world.Frame, preview);
                DrawStatus(world.Fighters[1], Red, false);
            }
            else DrawBody(enemyBody, world.Fighters[1], Red);
            if (aimDirection != 0 && world.Player.IsAlive)
                Box(world.Player.X + aimDirection * 0.9f, world.Player.Y + 0.9f, 0.35f, 0.08f, Blue);
            // Preview replays one copied world, rather than stacking ghosts over live bodies.
            // Keep the camera framed on the live world so starting a preview does not zoom out.
            if (preview)
            {
                playerBody.color = new Color(playerBody.color.r, playerBody.color.g, playerBody.color.b, 0.65f);
                if (enemyGolem == null)
                    enemyBody.color = new Color(enemyBody.color.r, enemyBody.color.g, enemyBody.color.b, 0.65f);
            }
            for (int i = used; i < overlays.Count; i++) overlays[i].enabled = false;
            FitCamera(framing ?? world);
        }

        private void DrawBody(SpriteRenderer body, RectangleFighter fighter, Color color)
        {
            body.transform.position = new Vector3(fighter.X, fighter.Y + (fighter.IsAlive ? HalfHeight : 0.15f), 0);
            body.transform.localScale = fighter.IsAlive ? new Vector3(0.9f, 1.8f, 1) : new Vector3(1.8f, 0.3f, 1);
            body.color = !fighter.IsAlive ? Color.gray : fighter.HurtFlash > 0 ? Color.white : color;
            DrawStatus(fighter, color, true);
        }

        // Health bar, facing mark, block guard, and the brief strike-range flash.
        private void DrawStatus(RectangleFighter fighter, Color color, bool facingMark)
        {
            if (!fighter.IsAlive) return;
            Box(fighter.X, fighter.Y + 2.1f, 1.1f, 0.09f, new Color(0.2f, 0.25f, 0.3f));
            float width = 1.1f * fighter.Health / fighter.MaxHealth;
            Box(fighter.X - 0.55f + width / 2, fighter.Y + 2.1f, width, 0.09f, color);
            if (facingMark)
                Box(fighter.X + fighter.Facing * 0.33f, fighter.Y + 1.15f, 0.12f, 0.18f, Color.white);
            if (fighter.Move == CombatMove.Block && fighter.Stun == 0)
                Box(fighter.X + fighter.Facing * 0.65f, fighter.Y + 0.9f, 0.1f, 1.9f, Blue);
            if (fighter.Move == CombatMove.None || fighter.Stun > 0) return;
            var move = RectangleCombat.Spec(fighter, fighter.Move);
            if (move.Damage <= 0 || move.Projectile || fighter.Elapsed > move.Startup) return;
            // Brief strike range only; wind-up information stays in the one-line HUD.
            if (fighter.Elapsed == move.Startup)
                Box(fighter.X + fighter.Facing * (0.45f + move.Reach / 2), fighter.Y + 0.9f,
                    move.Reach, 0.6f, new Color(1f, 0.8f, 0.25f, 0.7f));
        }

        private void Box(float x, float y, float width, float height, Color color)
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
            item.color = color;
        }

        private void FitCamera(RectangleCombat world)
        {
            if (arenaCamera == null) return;
            float left = Mathf.Min(world.Player.X, world.Fighters[1].X);
            float right = Mathf.Max(world.Player.X, world.Fighters[1].X);
            float size = Mathf.Max(4.2f, (right - left + 7) / (2 * Mathf.Max(0.3f, arenaCamera.aspect)));
            arenaCamera.orthographicSize = size;
            arenaCamera.transform.position = new Vector3((left + right) / 2, size + 0.2f, -10);
        }
    }
}