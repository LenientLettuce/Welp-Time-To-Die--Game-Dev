using UnityEngine;

namespace WelpTimeToDie.Player
{
    public interface ICombatDamageReceiver
    {
        void ReceiveDamage(int damage, Vector2 sourcePosition);
    }

    /// <summary>Frame-four attack hit detection and directional block rules.</summary>
    [RequireComponent(typeof(PlayerActionController))]
    public sealed class PlayerCombatController : MonoBehaviour
    {
        [SerializeField, Min(0)] private int attackDamage = 2;
        [SerializeField, Min(0)] private int blockReduction = 3;
        [SerializeField, Min(0.1f)] private float attackReach = 2.3f;
        private PlayerActionController actor;
        private readonly Collider2D[] hits = new Collider2D[32];
        private readonly System.Collections.Generic.HashSet<ICombatDamageReceiver> damaged = new System.Collections.Generic.HashSet<ICombatDamageReceiver>();
        public Vector2 AttackCenter => (Vector2)transform.position + actor.State.Direction * (0.5f + attackReach / 2f);
        public Vector2 AttackSize => new Vector2(attackReach, 1f);
        public float AttackAngle => Mathf.Atan2(actor.State.Direction.y, actor.State.Direction.x) * Mathf.Rad2Deg;

        private void Awake() => actor = GetComponent<PlayerActionController>();
        private void OnEnable()
        {
            if (actor == null) actor = GetComponent<PlayerActionController>();
            if (actor.Clock != null) actor.Clock.AfterFrame += ResolveAttack;
        }
        private void OnDisable() { if (actor != null && actor.Clock != null) actor.Clock.AfterFrame -= ResolveAttack; }

        private void ResolveAttack(long frame)
        {
            if (!actor.State.IsAttackActive) return;
            damaged.Clear();
            int count = Physics2D.OverlapBox(AttackCenter, AttackSize, AttackAngle, new ContactFilter2D { useTriggers = true }, hits);
            for (int i = 0; i < count; i++)
            {
                if (hits[i].attachedRigidbody == actor.Motor.Body) continue;
                foreach (var component in hits[i].GetComponentsInParent<MonoBehaviour>())
                    if (component is ICombatDamageReceiver receiver && damaged.Add(receiver))
                        receiver.ReceiveDamage(attackDamage, transform.position);
            }
        }

        public int ResolveIncomingDamage(int damage, Vector2 sourcePosition)
        {
            bool frontal = Vector2.Dot(sourcePosition - (Vector2)transform.position, actor.State.Direction) >= 0f;
            return actor.State.IsBlocking && frontal ? Mathf.Max(0, damage - blockReduction) : Mathf.Max(0, damage);
        }
    }
}
