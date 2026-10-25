using UnityEngine;
using WelpTimeToDie.Combat;
using WelpTimeToDie.Levels;

namespace WelpTimeToDie.Player
{
    /// <summary>Physics movement advanced only by combat substeps, never wall time.</summary>
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public sealed class PlayerMovementMotor : MonoBehaviour
    {
        [SerializeField] private ProceduralTerrainMap terrain;
        [SerializeField, Min(1f)] private float gravity = 30f;
        [SerializeField, Min(1f)] private float jumpSpeed = 14f;
        [SerializeField, Min(0f)] private float maximumStepHeight = 1f;
        private readonly RaycastHit2D[] groundHits = new RaycastHit2D[8];
        private Rigidbody2D body;
        private BoxCollider2D bodyCollider;
        public Rigidbody2D Body => body;
        public BoxCollider2D BodyCollider => bodyCollider;
        public ProceduralTerrainMap Terrain => terrain;

        public bool IsGrounded
        {
            get
            {
                if (body.linearVelocity.y > 0.1f) return false;
                int count = bodyCollider.Cast(Vector2.down, new ContactFilter2D { useTriggers = false }, groundHits, 0.06f);
                for (int i = 0; i < count; i++) if (groundHits[i].normal.y > 0.6f) return true;
                return false;
            }
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<BoxCollider2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            body.linearDamping = 0f;
            body.angularDamping = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.interpolation = RigidbodyInterpolation2D.None;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        public void CopySettingsFrom(PlayerMovementMotor source)
        {
            terrain = source.terrain;
            gravity = source.gravity;
            jumpSpeed = source.jumpSpeed;
            maximumStepHeight = source.maximumStepHeight;
        }

        public void BeginFrame(PlayerActionState state)
        {
            if (state.Move == PlayerMove.Jump && state.ElapsedFrames == 1)
                body.linearVelocity = new Vector2(body.linearVelocity.x, jumpSpeed * Mathf.Lerp(0.6f, 1f, state.Strength));
        }

        public void PrepareStep(PlayerActionState state, float seconds)
        {
            Vector2 velocity = body.linearVelocity;
            velocity.x = 0f;
            bool dashing = state.Move == PlayerMove.Dash;
            if (dashing)
                velocity = state.Direction * (10f * state.Strength / (6f * CombatPhysicsController.FrameDuration));
            else
            {
                if (state.Move == PlayerMove.Move) velocity.x = state.Facing * (2.4f * state.Strength / (8f * CombatPhysicsController.FrameDuration));
                if (state.Move == PlayerMove.Jump) velocity.x = state.Direction.x * 6f * state.Strength;
                velocity.y = Mathf.Max(-35f, velocity.y - gravity * seconds);
            }
            if ((state.Move == PlayerMove.Move || dashing && Mathf.Abs(state.Direction.y) < 0.05f) && IsGrounded)
                TryStepUp(velocity.x * seconds);
            body.linearVelocity = velocity;
        }

        private void TryStepUp(float distance)
        {
            if (terrain == null || terrain.HeightMap == null || Mathf.Abs(distance) < 0.0001f) return;
            Bounds bounds = bodyCollider.bounds;
            float desiredX = body.position.x + distance;
            float surface = terrain.GetSurfaceHeight(desiredX, bounds.extents.x - 0.01f);
            float rise = surface - bounds.min.y;
            if (rise > 0.03f && rise <= maximumStepHeight + 0.02f)
            {
                body.position += Vector2.up * (rise + 0.015f);
                Physics2D.SyncTransforms();
            }
        }

        public void FinishAction(PlayerMove completedMove)
        {
            body.linearVelocity = new Vector2(0f, completedMove == PlayerMove.Dash ? 0f : body.linearVelocity.y);
        }

        public void Teleport(Vector2 position)
        {
            body.position = position;
            transform.position = new Vector3(position.x, position.y, transform.position.z);
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            Physics2D.SyncTransforms();
        }
    }
}
