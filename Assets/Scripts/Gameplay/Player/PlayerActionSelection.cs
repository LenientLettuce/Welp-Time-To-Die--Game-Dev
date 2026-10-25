using UnityEngine;

namespace WelpTimeToDie.Player
{
    /// <summary>Uncommitted command settings, shared by aiming, UI, and prediction.</summary>
    public sealed class PlayerActionSelection : MonoBehaviour
    {
        [SerializeField] private PlayerMove selectedMove = PlayerMove.Dash;
        [SerializeField] private Vector2 direction = Vector2.right;
        [SerializeField, Range(0.1f, 1f)] private float strength = 0.5f;
        public PlayerMove Move => selectedMove;
        public Vector2 Direction => direction;
        public float Strength => strength;
        public int Revision { get; private set; }

        public void SelectMove(PlayerMove move) { selectedMove = move; Revision++; }
        public void SetDirection(Vector2 value)
        {
            if (value.sqrMagnitude < 0.001f) return;
            direction = value.normalized;
            Revision++;
        }
        public void SetStrength(float value) { strength = Mathf.Round(Mathf.Clamp(value, 0.1f, 1f) * 10f) / 10f; Revision++; }
    }
}
