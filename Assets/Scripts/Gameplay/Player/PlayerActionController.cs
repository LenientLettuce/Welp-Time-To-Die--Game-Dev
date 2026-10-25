using UnityEngine;
using WelpTimeToDie.Combat;
using WelpTimeToDie.Levels;

namespace WelpTimeToDie.Player
{
    [RequireComponent(typeof(PlayerMovementMotor))]
    [DisallowMultipleComponent]
    public sealed class PlayerActionController : MonoBehaviour
    {
        [SerializeField] private CombatPhysicsController combatPhysics;
        [SerializeField] private ProceduralTerrainMap terrain;
        private PlayerMovementMotor motor;
        private Vector2 spawnPosition;
        public PlayerActionState State { get; private set; } = new PlayerActionState();
        public PlayerMovementMotor Motor => motor;
        public CombatPhysicsController Clock => combatPhysics;
        public bool IsBusy => State.IsBusy;

        private void Awake() { motor = GetComponent<PlayerMovementMotor>(); spawnPosition = motor.Body.position; }
        private void OnEnable()
        {
            if (combatPhysics != null)
            {
                combatPhysics.BeforeFrame += BeginFrame;
                combatPhysics.BeforePhysicsStep += PrepareStep;
                combatPhysics.AfterFrame += FinishFrame;
            }
            if (terrain != null) terrain.Generated += PlaceAtSpawn;
        }
        private void Start() { if (terrain != null) PlaceAtSpawn(); }
        private void OnDisable()
        {
            if (combatPhysics != null)
            {
                combatPhysics.BeforeFrame -= BeginFrame;
                combatPhysics.BeforePhysicsStep -= PrepareStep;
                combatPhysics.AfterFrame -= FinishFrame;
            }
            if (terrain != null) terrain.Generated -= PlaceAtSpawn;
        }

        public void Bind(CombatPhysicsController clock)
        {
            OnDisable();
            combatPhysics = clock;
            if (isActiveAndEnabled) OnEnable();
        }

        public bool TryStart(PlayerMove move, Vector2 direction, float strength)
        {
            if (!Application.isPlaying || !isActiveAndEnabled || combatPhysics == null ||
                !combatPhysics.isActiveAndEnabled || combatPhysics.IsFaulted || combatPhysics.IsAdvancing) return false;
            return State.TryStart(move, direction, strength, motor.IsGrounded);
        }

        private void BeginFrame(long frame, float seconds) { State.BeginFrame(); motor.BeginFrame(State); }
        private void PrepareStep(long frame, int step, float seconds)
        {
            if (State.IsBusy) terrain?.Features?.AdvanceCommitted(motor.BodyCollider.bounds, motor.Body.linearVelocity.y);
            motor.PrepareStep(State, seconds);
        }
        private void FinishFrame(long frame)
        {
            if (State.IsBusy) terrain?.Features?.AdvanceCommitted(motor.BodyCollider.bounds, motor.Body.linearVelocity.y);
            PlayerMove completed = State.Move;
            if (State.FinishFrame()) motor.FinishAction(completed);
        }

        private void PlaceAtSpawn()
        {
            if (terrain != null && terrain.HeightMap != null)
            {
                spawnPosition = PlayerSpawnPlacement.Calculate(terrain, motor.BodyCollider);
            }
            ResetToSpawn();
        }

        public void ResetToSpawn()
        {
            if (combatPhysics != null && combatPhysics.IsAdvancing)
                throw new System.InvalidOperationException("Reset between combat frames.");
            State = new PlayerActionState();
            motor.Teleport(spawnPosition);
        }
    }
}
