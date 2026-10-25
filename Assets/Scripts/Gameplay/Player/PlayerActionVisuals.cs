using UnityEngine;

namespace WelpTimeToDie.Player
{
    [RequireComponent(typeof(PlayerActionController), typeof(PlayerCombatController), typeof(PlayerActionSelection))]
    public sealed class PlayerActionVisuals : MonoBehaviour
    {
        private PlayerActionController actor;
        private PlayerCombatController combat;
        private PlayerActionSelection selection;
        private SpriteRenderer attackArea, guard;
        private LineRenderer aim;
        private SpriteRenderer body;

        private void Awake()
        {
            actor = GetComponent<PlayerActionController>();
            combat = GetComponent<PlayerCombatController>();
            selection = GetComponent<PlayerActionSelection>();
            body = GetComponent<SpriteRenderer>();
            attackArea = CreateOverlay("Attack hitbox", new Color(1f, 0.75f, 0.2f, 0.65f));
            guard = CreateOverlay("Block guard", new Color(0.35f, 0.8f, 1f, 0.85f));
            aim = new GameObject("Selected aim direction").AddComponent<LineRenderer>();
            aim.sharedMaterial = body.sharedMaterial;
            aim.widthMultiplier = 0.045f;
            aim.startColor = aim.endColor = new Color(0.95f, 0.85f, 0.3f);
            aim.sortingOrder = 6;
            aim.positionCount = 5;
        }

        private SpriteRenderer CreateOverlay(string name, Color color)
        {
            var overlay = new GameObject(name).AddComponent<SpriteRenderer>();
            overlay.sprite = body.sprite;
            overlay.sharedMaterial = body.sharedMaterial;
            overlay.color = color;
            overlay.sortingOrder = 6;
            return overlay;
        }

        private void LateUpdate()
        {
            var state = actor.State;
            attackArea.enabled = state.IsAttackActive;
            guard.enabled = state.IsBlocking;
            body.color = state.IsBlocking ? new Color(0.4f, 0.65f, 1f) : state.IsAttackActive ? new Color(1f, 0.85f, 0.4f) : new Color(0.2f, 0.8f, 0.95f);
            if (attackArea.enabled) Place(attackArea, combat.AttackCenter, combat.AttackSize, combat.AttackAngle);
            if (guard.enabled) Place(guard, (Vector2)transform.position + state.Direction * 0.75f, new Vector2(0.12f, 1.8f), combat.AttackAngle);
            aim.enabled = !state.IsBusy;
            if (!aim.enabled) return;
            Vector3 direction = selection.Direction;
            Vector3 origin = transform.position;
            Vector3 tip = origin + direction * (1.2f + selection.Strength);
            Vector3 side = new Vector3(-direction.y, direction.x) * 0.15f;
            aim.SetPositions(new[] { origin, tip, tip - direction * 0.25f + side, tip, tip - direction * 0.25f - side });
        }

        private static void Place(SpriteRenderer sprite, Vector2 position, Vector2 size, float angle)
        {
            sprite.transform.position = position;
            sprite.transform.rotation = Quaternion.Euler(0, 0, angle);
            sprite.transform.localScale = new Vector3(size.x / sprite.sprite.bounds.size.x, size.y / sprite.sprite.bounds.size.y, 1f);
        }

        private void OnDestroy()
        {
            if (attackArea != null) Destroy(attackArea.gameObject);
            if (guard != null) Destroy(guard.gameObject);
            if (aim != null) Destroy(aim.gameObject);
        }
    }
}
