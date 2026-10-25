using UnityEngine;
using WelpTimeToDie.Combat;
using WelpTimeToDie.Levels;

namespace WelpTimeToDie.Player
{
    /// <summary>Ground-constrained movement adapter for the current horizontal dash prototype.</summary>
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(PlayerDashController))]
    public sealed class PlayerTerrainFollower : MonoBehaviour
    {
        [SerializeField] private ProceduralTerrainMap terrain;
        [SerializeField] private CombatPhysicsController combatPhysics;
        private Rigidbody2D body;
        private BoxCollider2D bodyCollider;
        private const float GroundClearance = 0.02f;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<BoxCollider2D>();
        }

        private void OnEnable()
        {
            if (terrain != null) terrain.Generated += PlaceAtSpawn;
            if (combatPhysics == null) return;
            combatPhysics.BeforePhysicsStep += PrepareStep;
            combatPhysics.AfterFrame += FinishFrame;
        }

        private void Start() => PlaceAtSpawn();

        private void OnDisable()
        {
            if (terrain != null) terrain.Generated -= PlaceAtSpawn;
            if (combatPhysics == null) return;
            combatPhysics.BeforePhysicsStep -= PrepareStep;
            combatPhysics.AfterFrame -= FinishFrame;
        }

        private void PlaceAtSpawn()
        {
            if (terrain == null || terrain.HeightMap == null) return;
            GetComponent<PlayerDashController>().SetSpawnPosition(GroundPosition(terrain.Layout.WorldBounds.center.x));
        }

        private Vector2 GroundPosition(float x)
        {
            Bounds limits = terrain.Layout.WorldBounds;
            Vector3 extents = bodyCollider.bounds.extents;
            x = Mathf.Clamp(x, limits.min.x + extents.x + GroundClearance, limits.max.x - extents.x - GroundClearance);
            float offsetY = bodyCollider.bounds.center.y - transform.position.y;
            return new Vector2(x, terrain.GetSurfaceHeight(x, extents.x) + extents.y - offsetY + GroundClearance);
        }

        private void PrepareStep(long frame, int step, float seconds)
        {
            if (terrain == null || terrain.HeightMap == null) return;
            Vector2 destination = GroundPosition(body.position.x + body.linearVelocity.x * seconds);
            float midpoint = (body.position.x + destination.x) / 2f;
            float sweptHalfWidth = Mathf.Abs(destination.x - body.position.x) / 2f + bodyCollider.bounds.extents.x;
            float surface = terrain.GetSurfaceHeight(midpoint, sweptHalfWidth);
            float offsetY = bodyCollider.bounds.center.y - transform.position.y;
            body.position = new Vector2(body.position.x, surface + bodyCollider.bounds.extents.y - offsetY + GroundClearance);
        }

        private void FinishFrame(long frame)
        {
            if (terrain == null || terrain.HeightMap == null) return;
            Vector2 position = GroundPosition(body.position.x);
            body.position = position;
            transform.position = new Vector3(position.x, position.y, transform.position.z);
            Physics2D.SyncTransforms();
        }
    }
}
