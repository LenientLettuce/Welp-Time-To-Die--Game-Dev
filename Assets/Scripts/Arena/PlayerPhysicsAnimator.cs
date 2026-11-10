using System;
using UnityEngine;

namespace WelpTimeToDie.RectanglePrototype
{
    public enum PlayerBone 
    { 
        Head, UpperSpine, LowerSpine, 
        UpperFrontArm, LowerFrontArm, UpperBackArm, LowerBackArm,
        UpperFrontLeg, LowerFrontLeg, FrontFoot, UpperBackLeg, LowerBackLeg, BackFoot
    }

    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class PlayerPhysicsAnimator : MonoBehaviour
    {
        [Serializable]
        public struct Joint
        {
            public string boneName;
            public float length;
            public float frequency;
            [Range(0, 2)] public float dampingRatio;
            public float gravityScale;
        }

        private sealed class Body
        {
            public Transform Bone;
            public Quaternion BindRotation;
            public Joint Joint;
            public float Angle, Velocity, Target, Acceleration;
        }

        private struct Pose
        {
            public float Head, USpine, LSpine, UFArm, LFArm, UBArm, LBArm, UFLeg, LFLeg, FFoot, UBLeg, LBLeg, BFoot, Hip, Stiffness;
            public Pose(
                float head, float uSpine, float lSpine, 
                float uFArm, float lFArm, float uBArm, float lBArm,
                float uFLeg, float lFLeg, float fFoot, float uBLeg, float lBLeg, float bFoot, 
                float hip, float stiffness)
            {
                Head = head; USpine = uSpine; LSpine = lSpine; 
                UFArm = uFArm; LFArm = lFArm; UBArm = uBArm; LBArm = lBArm;
                UFLeg = uFLeg; LFLeg = lFLeg; FFoot = fFoot; UBLeg = uBLeg; LBLeg = lBLeg; BFoot = bFoot;
                Hip = hip; Stiffness = stiffness;
            }
        }

        private const int MaxSubsteps = 12;
        private const float MaxAngularVelocity = 40f, MaxRootAcceleration = 60f;
        // Increased smoothing to make rapid directional changes less jerky
        private const float VelocitySmoothing = 0.12f; 
        private static readonly Color HurtTint = new Color(1f, 0.55f, 0.55f);
        private static readonly Color DeadTint = new Color(0.5f, 0.5f, 0.55f);
        private const int BoneCount = 13;
        
        [Header("Torso & Head")]
        [SerializeField] private Joint head = new Joint { boneName = "head", length = 0.2f, frequency = 3.5f, dampingRatio = 0.4f, gravityScale = 1 };
        [SerializeField] private Joint upperSpine = new Joint { boneName = "upper spine", length = 0.2f, frequency = 2.8f, dampingRatio = 0.6f, gravityScale = 1 };
        [SerializeField] private Joint lowerSpine = new Joint { boneName = "lower spine", length = 0.2f, frequency = 2.5f, dampingRatio = 0.6f, gravityScale = 1 };
        
        [Header("Arms")]
        [SerializeField] private Joint upperLeftArm = new Joint { boneName = "upper left arm", length = 0.2f, frequency = 3.0f, dampingRatio = 0.35f, gravityScale = 1 };
        [SerializeField] private Joint lowerLeftArm = new Joint { boneName = "lower left arm", length = 0.2f, frequency = 3.2f, dampingRatio = 0.3f, gravityScale = 1 };
        [SerializeField] private Joint upperRightArm = new Joint { boneName = "upper right arm", length = 0.2f, frequency = 3.0f, dampingRatio = 0.35f, gravityScale = 1 };
        [SerializeField] private Joint lowerRightArm = new Joint { boneName = "lower right arm", length = 0.2f, frequency = 3.2f, dampingRatio = 0.3f, gravityScale = 1 };

        [Header("Legs")]
        [SerializeField] private Joint upperLeftLeg = new Joint { boneName = "upper left leg", length = 0.25f, frequency = 3.5f, dampingRatio = 0.5f, gravityScale = 1 };
        [SerializeField] private Joint lowerLeftLeg = new Joint { boneName = "lower left leg", length = 0.25f, frequency = 3.8f, dampingRatio = 0.45f, gravityScale = 1 };
        [SerializeField] private Joint leftFoot = new Joint { boneName = "left foot", length = 0.1f, frequency = 4.0f, dampingRatio = 0.6f, gravityScale = 1 };
        [SerializeField] private Joint upperRightLeg = new Joint { boneName = "upper right leg", length = 0.25f, frequency = 3.5f, dampingRatio = 0.5f, gravityScale = 1 };
        [SerializeField] private Joint lowerRightLeg = new Joint { boneName = "lower right leg", length = 0.25f, frequency = 3.8f, dampingRatio = 0.45f, gravityScale = 1 };
        [SerializeField] private Joint rightFoot = new Joint { boneName = "right foot", length = 0.1f, frequency = 4.0f, dampingRatio = 0.6f, gravityScale = 1 };

        [SerializeField] private float hipFrequency = 3.5f;
        [SerializeField, Range(0, 2)] private float hipDampingRatio = 0.5f;
        [SerializeField] private float gravity = 9.81f;
        [SerializeField, Range(30, 480)] private int simulationRate = 120;
        [SerializeField] private float armReaction = 0.15f;
        [SerializeField] private float hitImpulse = 6f;
        [SerializeField, Range(30, 90)] private float groundAngle = 82f;

        private Body[] bodies;
        private Transform rootBone;
        private Vector3 rootBindPosition;
        private SpriteRenderer[] parts;
        private bool rigMissing;
        private float hip, hipVelocity, hipTarget, stiffness = 1, accumulator;
        private Vector3 lastRootPosition;
        private Vector2 rootVelocity, rootAcceleration;
        private bool hasRootSample, wasAlive = true;
        private int lastFrame = -1, lastHitFrame = -1, deathSide = 1;

        public float BoneOffset(PlayerBone bone)
        {
            if (!EnsureRig()) return 0;
            float degrees = bodies[(int)bone].Angle * Mathf.Rad2Deg;
            return bone == PlayerBone.UpperSpine || bone == PlayerBone.LowerSpine || bone == PlayerBone.Head ? -degrees : degrees;
        }

        public void PlaceAtFeet(Vector2 feet, int facing)
        {
            transform.position = new Vector3(feet.x, feet.y, transform.position.z);
            
            Vector3 currentScale = transform.localScale;
            float absScaleX = Mathf.Abs(currentScale.x);
            transform.localScale = new Vector3(facing < 0 ? -absScaleX : absScaleX, currentScale.y, currentScale.z);
        }

        public void Show(RectangleFighter fighter, int frame, bool preview)
        {
            if (!EnsureRig()) return;
            PlaceAtFeet(new Vector2(fighter.X, fighter.Y), fighter.Facing);

            bool discontinuity = lastFrame < 0 || frame < lastFrame;
            if (discontinuity)
            {
                lastHitFrame = -1;
                wasAlive = fighter.IsAlive;
            }
            else if (frame > lastFrame) DetectEvents(fighter, frame);
            
            SetTargets(fighter);
            
            if (discontinuity) SnapToTargets();
            wasAlive = fighter.IsAlive;
            lastFrame = frame;

            Color tint = !fighter.IsAlive ? DeadTint : fighter.HurtFlash > 0 ? HurtTint : Color.white;
            if (preview) tint.a = 0.65f;
            foreach (var part in parts) if (part != null) part.color = tint;
        }

        public void Simulate(float deltaTime)
        {
            if (deltaTime <= 0 || !EnsureRig()) return;
            SampleRoot(deltaTime);
            float step = 1f / simulationRate;
            accumulator = Mathf.Min(accumulator + deltaTime, step * MaxSubsteps);
            while (accumulator >= step)
            {
                Step(step);
                Apply();
                accumulator -= step;
            }
        }

        public void ResetRig() { lastFrame = -1; lastHitFrame = -1; hasRootSample = false; }
        
        private float[] savedState;
        private Vector3 savedRootPosition, savedLastRoot, savedScale;
        private Vector2 savedVelocity, savedAcceleration;
        private bool savedHasSample, savedAlive;
        private int savedFrame, savedHit, savedDeathSide;
        private Color[] savedColors;

        public void BeginPreview()
        {
            if (!EnsureRig() || savedState != null) return;
            savedState = new float[BoneCount * 4 + 5];
            for (int i = 0; i < BoneCount; i++) 
            { 
                savedState[i*4] = bodies[i].Angle; 
                savedState[i*4+1] = bodies[i].Velocity; 
                savedState[i*4+2] = bodies[i].Target; 
                savedState[i*4+3] = bodies[i].Acceleration; 
            }
            int offset = BoneCount * 4;
            savedState[offset] = hip; savedState[offset+1] = hipVelocity; savedState[offset+2] = hipTarget; 
            savedState[offset+3] = stiffness; savedState[offset+4] = accumulator;
            
            savedRootPosition = transform.position; savedScale = transform.localScale; savedLastRoot = lastRootPosition;
            savedVelocity = rootVelocity; savedAcceleration = rootAcceleration; savedHasSample = hasRootSample; savedAlive = wasAlive;
            savedFrame = lastFrame; savedHit = lastHitFrame; savedDeathSide = deathSide;
            savedColors = new Color[parts.Length]; 
            for (int i = 0; i < parts.Length; i++) savedColors[i] = parts[i].color;
        }

        public void EndPreview()
        {
            if (savedState == null) return;
            for (int i = 0; i < BoneCount; i++) 
            { 
                bodies[i].Angle = savedState[i*4]; 
                bodies[i].Velocity = savedState[i*4+1]; 
                bodies[i].Target = savedState[i*4+2]; 
                bodies[i].Acceleration = savedState[i*4+3]; 
            }
            int offset = BoneCount * 4;
            hip = savedState[offset]; hipVelocity = savedState[offset+1]; hipTarget = savedState[offset+2]; 
            stiffness = savedState[offset+3]; accumulator = savedState[offset+4];
            
            transform.position = savedRootPosition; transform.localScale = savedScale; lastRootPosition = savedLastRoot;
            rootVelocity = savedVelocity; rootAcceleration = savedAcceleration; hasRootSample = savedHasSample; wasAlive = savedAlive;
            lastFrame = savedFrame; lastHitFrame = savedHit; deathSide = savedDeathSide;
            for (int i = 0; i < parts.Length; i++) parts[i].color = savedColors[i];
            savedState = null; 
            Apply();
        }

        private void DetectEvents(RectangleFighter fighter, int frame)
        {
            if (fighter.HurtFlash == 4 && frame != lastHitFrame)
            {
                lastHitFrame = frame;
                float direction = Mathf.Abs(fighter.VelocityX) > 0.1f ? Mathf.Sign(fighter.VelocityX) : -fighter.Facing;
                ApplyHit(direction, fighter.Stun > 0 || !fighter.IsAlive ? 1f : 0.45f);
            }
            if (wasAlive && !fighter.IsAlive)
            {
                var torso = bodies[(int)PlayerBone.LowerSpine];
                deathSide = torso.Angle + torso.Velocity * 0.2f >= 0 ? 1 : -1;
            }
        }

        private void ApplyHit(float direction, float strength)
        {
            float mirror = Mirror();
            float kick = -direction * mirror * hitImpulse * strength;
            
            bodies[(int)PlayerBone.LowerSpine].Velocity += kick;
            bodies[(int)PlayerBone.UpperSpine].Velocity += kick * 1.2f;
            bodies[(int)PlayerBone.Head].Velocity += kick * 1.6f;
            
            bodies[(int)PlayerBone.UpperFrontArm].Velocity += kick * 1.2f;
            bodies[(int)PlayerBone.LowerFrontArm].Velocity += kick * 1.5f;
            bodies[(int)PlayerBone.UpperBackArm].Velocity += kick * 0.8f;
            bodies[(int)PlayerBone.LowerBackArm].Velocity += kick * 1.0f;
            
            hipVelocity -= 0.8f * strength;
        }

        private void SetTargets(RectangleFighter fighter)
        {
            if (!fighter.IsAlive)
            {
                stiffness = 0.2f;
                hipTarget = -0.3f;
                SetTarget(PlayerBone.LowerSpine, deathSide * groundAngle * Mathf.Deg2Rad);
                SetTarget(PlayerBone.UpperSpine, deathSide * (groundAngle + 10) * Mathf.Deg2Rad);
                SetTarget(PlayerBone.Head, deathSide * 25 * Mathf.Deg2Rad);
                SetTarget(PlayerBone.UpperFrontArm, 0); SetTarget(PlayerBone.LowerFrontArm, 0);
                SetTarget(PlayerBone.UpperBackArm, 0); SetTarget(PlayerBone.LowerBackArm, 0);
                SetTarget(PlayerBone.UpperFrontLeg, 0); SetTarget(PlayerBone.LowerFrontLeg, 0); SetTarget(PlayerBone.FrontFoot, 0);
                SetTarget(PlayerBone.UpperBackLeg, 0); SetTarget(PlayerBone.LowerBackLeg, 0); SetTarget(PlayerBone.BackFoot, 0);
                return;
            }
            var pose = PoseFor(fighter);
            stiffness = pose.Stiffness;
            hipTarget = pose.Hip;
            SetTarget(PlayerBone.Head, -pose.Head * Mathf.Deg2Rad);
            SetTarget(PlayerBone.UpperSpine, -pose.USpine * Mathf.Deg2Rad);
            SetTarget(PlayerBone.LowerSpine, -pose.LSpine * Mathf.Deg2Rad);
            
            SetTarget(PlayerBone.UpperFrontArm, pose.UFArm * Mathf.Deg2Rad);
            SetTarget(PlayerBone.LowerFrontArm, pose.LFArm * Mathf.Deg2Rad);
            SetTarget(PlayerBone.UpperBackArm, pose.UBArm * Mathf.Deg2Rad);
            SetTarget(PlayerBone.LowerBackArm, pose.LBArm * Mathf.Deg2Rad);
            
            SetTarget(PlayerBone.UpperFrontLeg, pose.UFLeg * Mathf.Deg2Rad);
            SetTarget(PlayerBone.LowerFrontLeg, pose.LFLeg * Mathf.Deg2Rad);
            SetTarget(PlayerBone.FrontFoot, pose.FFoot * Mathf.Deg2Rad);
            SetTarget(PlayerBone.UpperBackLeg, pose.UBLeg * Mathf.Deg2Rad);
            SetTarget(PlayerBone.LowerBackLeg, pose.LBLeg * Mathf.Deg2Rad);
            SetTarget(PlayerBone.BackFoot, pose.BFoot * Mathf.Deg2Rad);
        }

        private void SetTarget(PlayerBone bone, float radians) => bodies[(int)bone].Target = radians;

        private static Pose PoseFor(RectangleFighter f)
        {
            var idle = new Pose(0, 4, 4, 8, 12, -4, -8, 5, -5, 0, -5, 5, 0, 0, 1);
            if (f.Stun > 0)
            {
                float wobble = f.Stun % 2 == 0 ? 6 : -6;
                return new Pose(-25, -5 + wobble, -5 + wobble, -15, -25, -20, -30, 10, -10, -5, 0, 0, 5, -0.06f, 0.35f);
            }
            var spec = RectangleCombat.Spec(f, f.Move);
            int e = f.Elapsed;
            switch (f.Move)
            {
                case CombatMove.Move:
                    {
                        float phase = e / (float)Mathf.Max(1, spec.Duration) * 2 * Mathf.PI;
                        float swing = Mathf.Sin(phase);
                        float legSwing = Mathf.Cos(phase);
                        return new Pose(
                            2, 3 + 1.5f * Mathf.Sin(2 * phase), 3 + 1.5f * Mathf.Sin(2 * phase), 
                            8 + 28 * swing, 15 + 30 * swing, -4 - 28 * swing, -10 - 30 * swing, 
                            20 * legSwing, -10 * legSwing, 5, -20 * legSwing, 10 * legSwing, -5, 
                            -0.05f + 0.04f * Mathf.Cos(2 * phase), 1);
                    }
                case CombatMove.Dash: 
                    return new Pose(-10, 11, 11, -45, -60, -55, -70, -20, -40, 10, -40, -60, 20, -0.06f, 1.2f);
                case CombatMove.Backstep:
                    return new Pose(12, -10, -10, 55, 75, 45, 65, 12, -25, 0, -25, 10, 5, -0.07f, 1.3f);
                case CombatMove.Jump:
                    return f.VelocityY > 0
                    ? new Pose(-10, -2.5f, -2.5f, 140, 120, 130, 110, -15, -30, 10, -30, -60, 20, 0.02f, 1)
                    : new Pose(5, 2.5f, 2.5f, 70, 60, 60, 50, 10, -10, 0, 20, -20, 5, 0, 1);
                case CombatMove.Block: 
                    return new Pose(12, 4, 4, 80, 110, 70, 100, 10, -15, 0, -10, 15, 0, -0.05f, 1.4f);
                case CombatMove.Wait: 
                    return new Pose(4, 1, 1, 4, 6, -2, -4, 2, -2, 0, -2, 2, 0, -0.03f, 1);
                case CombatMove.Punch:
                    return Strike(e, spec,
                    new Pose(0, -5, -5, -55, -70, 15, 20, 5, -5, 0, -5, 5, 0, -0.03f, 1),
                    new Pose(5, 9, 9, 95, 15, -25, -35, 10, -10, 0, -10, 10, 0, -0.05f, 1.8f),
                    new Pose(2, 3, 3, 35, 15, -8, -10, 5, -5, 0, -5, 5, 0, -0.02f, 1));
                case CombatMove.Heavy:
                    return Strike(e, spec,
                    new Pose(-12, -9, -9, 165, 170, 160, 165, 15, -15, 0, -15, 15, 0, 0.03f, 1),
                    new Pose(15, 17.5f, 17.5f, 60, 20, 55, 15, 25, -25, 0, -25, 25, 0, -0.14f, 2f),
                    new Pose(6, 7.5f, 7.5f, 25, 15, 20, 10, 10, -10, 0, -10, 10, 0, -0.06f, 0.8f));
                case CombatMove.Special:
                    return Strike(e, spec,
                    new Pose(6, 6, 6, -35, -45, -10, -20, 0, -10, 0, 0, 10, 0, -0.1f, 1),
                    new Pose(-10, -7.5f, -7.5f, 150, 130, 20, 10, -20, -40, 10, 20, 40, -10, 0.04f, 1.8f),
                    new Pose(-2, -2, -2, 40, 30, 0, -10, 5, -5, 0, -5, 5, 0, 0, 1));
                case CombatMove.Kick:
                    return Strike(e, spec,
                    new Pose(-5, -5, -5, -20, -30, 20, 30, 30, -45, 0, -10, 10, 0, -0.05f, 1),
                    new Pose(10, 5, 5, 40, 20, -40, -20, 110, 10, 15, -20, 20, 0, 0.05f, 1.8f),
                    new Pose(5, 5, 5, 10, 10, -10, -10, 20, -20, 0, -10, 10, 0, 0, 1));
                case CombatMove.DashKick:
                    return Strike(e, spec,
                    new Pose(-10, -10, -10, -30, -40, 30, 40, 45, -60, 0, -20, 20, 0, -0.08f, 1.2f),
                    new Pose(15, 10, 10, 60, 30, -60, -30, 130, 0, 20, -40, 40, 0, 0.1f, 2f),
                    new Pose(5, 5, 5, 15, 15, -15, -15, 30, -30, 0, -15, 15, 0, 0, 1));
                case CombatMove.Sweep:
                    return Strike(e, spec,
                    new Pose(12, -8, -8, -30, -45, 30, 40, 35, -55, 10, -10, -30, 0, -0.16f, 1.2f),
                    new Pose(18, 7, 7, -45, -25, 35, 30, 72, 0, 0, -12, -65, 10, -0.24f, 1.8f),
                    new Pose(5, 4, 4, -10, -15, 10, 15, 20, -25, 0, -10, -15, 0, -0.1f, 1));
                case CombatMove.Uppercut:
                    return Strike(e, spec,
                    new Pose(-10, -15, -15, -60, -80, 20, 10, 20, -20, 0, -20, 20, 0, -0.1f, 1f),
                    new Pose(20, 10, 10, 160, 150, -20, -10, -10, 10, 0, 10, -10, 0, 0.1f, 1.8f),
                    new Pose(5, 5, 5, 60, 30, -10, -10, 5, -5, 0, -5, 5, 0, 0, 1f));
                default: return idle;
            }
        }

        private static Pose Strike(int elapsed, MoveSpec spec, Pose windUp, Pose hit, Pose recover)
        {
            // Give the physics springs 4 frames to travel so the limb is fully extended EXACTLY at spec.Startup
            int swingStart = Mathf.Max(0, spec.Startup - 4);

            if (elapsed < swingStart)
            {
                return windUp; // Hold the wind-up pose
            }
            else if (elapsed < spec.Startup + spec.Active)
            {
                return hit;    // Whip the limb forward and hold it through the active frames
            }
            else
            {
                return recover;// Pull back for the remainder of spec.Duration
            }
        }

        private void SampleRoot(float deltaTime)
        {
            Vector3 position = transform.position;
            if (!hasRootSample)
            {
                lastRootPosition = position;
                rootVelocity = rootAcceleration = Vector2.zero;
                hasRootSample = true;
                return;
            }
            Vector2 raw = (position - lastRootPosition) / deltaTime;
            lastRootPosition = position;
            Vector2 smoothed = Vector2.Lerp(rootVelocity, raw, 1 - Mathf.Exp(-deltaTime / VelocitySmoothing));
            rootAcceleration = Vector2.ClampMagnitude((smoothed - rootVelocity) / deltaTime, MaxRootAcceleration);
            rootVelocity = smoothed;
        }

        private void Step(float dt)
        {
            float mirror = Mirror();
            float worldScale = Mathf.Abs(transform.lossyScale.y);
            Vector2 load = new Vector2(0, -gravity) - rootAcceleration;
            
            foreach (var body in bodies)
            {
                float w = 2 * Mathf.PI * body.Joint.frequency * stiffness;
                float spring = w * w * (body.Target - body.Angle) - 2 * body.Joint.dampingRatio * w * body.Velocity;
                Vector2 direction = ((Vector2)body.Bone.TransformVector(Vector3.right)).normalized;
                float length = Mathf.Max(0.05f, body.Joint.length * worldScale);
                float torque = 1.5f / length * (direction.x * load.y - direction.y * load.x);
                body.Acceleration = spring + torque * body.Joint.gravityScale * mirror;
            }
            
            var lSpine = bodies[(int)PlayerBone.LowerSpine];
            var uSpine = bodies[(int)PlayerBone.UpperSpine];
            float armTorque = armReaction * (
                bodies[(int)PlayerBone.UpperFrontArm].Acceleration + 
                bodies[(int)PlayerBone.LowerFrontArm].Acceleration + 
                bodies[(int)PlayerBone.UpperBackArm].Acceleration + 
                bodies[(int)PlayerBone.LowerBackArm].Acceleration);
            
            lSpine.Acceleration -= armTorque * 0.5f;
            uSpine.Acceleration -= armTorque * 0.5f;
            
            foreach (var body in bodies)
            {
                body.Velocity = Mathf.Clamp(body.Velocity + body.Acceleration * dt, -MaxAngularVelocity, MaxAngularVelocity);
                body.Angle += body.Velocity * dt;
            }
            
            float limit = groundAngle * Mathf.Deg2Rad;
            if (Mathf.Abs(lSpine.Angle) > limit)
            {
                float side = Mathf.Sign(lSpine.Angle);
                lSpine.Angle = side * limit;
                if (lSpine.Velocity * side > 0) lSpine.Velocity *= -0.25f;
            }
            
            float hw = 2 * Mathf.PI * hipFrequency * Mathf.Max(0.5f, stiffness);
            hipVelocity += (hw * hw * (hipTarget - hip) - 2 * hipDampingRatio * hw * hipVelocity) * dt;
            hip += hipVelocity * dt;
        }

        private void Apply()
        {
            foreach (var body in bodies)
                body.Bone.localRotation = body.BindRotation * Quaternion.Euler(0, 0, body.Angle * Mathf.Rad2Deg);
            
            if (rootBone != null)
            {
                rootBone.localPosition = rootBindPosition + new Vector3(0, hip, 0);

                // Floor constraint: Ensure the lowest part of the legs never penetrates the floor
                float floorY = transform.position.y;
                float minFootY = Mathf.Min(
                    bodies[(int)PlayerBone.FrontFoot].Bone.position.y,
                    bodies[(int)PlayerBone.BackFoot].Bone.position.y,
                    bodies[(int)PlayerBone.LowerFrontLeg].Bone.position.y,
                    bodies[(int)PlayerBone.LowerBackLeg].Bone.position.y
                );
                
                // If any foot/leg bone dips below the terrain's mathematical surface, push the root up
                if (minFootY < floorY)
                {
                    rootBone.position += Vector3.up * (floorY - minFootY);
                }
            }
        }

        private void SnapToTargets()
        {
            foreach (var body in bodies) { body.Angle = body.Target; body.Velocity = 0; }
            hip = hipTarget;
            hipVelocity = 0;
            accumulator = 0;
            hasRootSample = false;
            Apply();
        }

        private float Mirror() => Mathf.Sign(transform.lossyScale.x) * Mathf.Sign(transform.lossyScale.y);

        private bool EnsureRig()
        {
            if (bodies != null) return true;
            if (rigMissing) return false;
            
            Transform headT = FindBone(transform, head.boneName);
            Transform uSpineT = FindBone(transform, upperSpine.boneName);
            Transform lSpineT = FindBone(transform, lowerSpine.boneName);
            
            Transform uLeftArmT = FindBone(transform, upperLeftArm.boneName);
            Transform lLeftArmT = FindBone(transform, lowerLeftArm.boneName);
            Transform uRightArmT = FindBone(transform, upperRightArm.boneName);
            Transform lRightArmT = FindBone(transform, lowerRightArm.boneName);

            Transform uLeftLegT = FindBone(transform, upperLeftLeg.boneName);
            Transform lLeftLegT = FindBone(transform, lowerLeftLeg.boneName);
            Transform lFootT = FindBone(transform, leftFoot.boneName);
            Transform uRightLegT = FindBone(transform, upperRightLeg.boneName);
            Transform lRightLegT = FindBone(transform, lowerRightLeg.boneName);
            Transform rFootT = FindBone(transform, rightFoot.boneName);

            if (headT == null || uSpineT == null || lSpineT == null || 
                uLeftArmT == null || lLeftArmT == null || uRightArmT == null || lRightArmT == null ||
                uLeftLegT == null || lLeftLegT == null || lFootT == null || 
                uRightLegT == null || lRightLegT == null || rFootT == null)
            {
                rigMissing = true;
                Debug.LogError($"{name}: PlayerPhysicsAnimator could not find all 13 required bones.", this);
                return false;
            }

            bool rightLeads = transform.InverseTransformPoint(uRightArmT.position).x >= transform.InverseTransformPoint(uLeftArmT.position).x;
            
            bodies = new Body[BoneCount];
            bodies[(int)PlayerBone.Head] = CreateBody(headT, head);
            bodies[(int)PlayerBone.UpperSpine] = CreateBody(uSpineT, upperSpine);
            bodies[(int)PlayerBone.LowerSpine] = CreateBody(lSpineT, lowerSpine);
            
            bodies[(int)PlayerBone.UpperFrontArm] = rightLeads ? CreateBody(uRightArmT, upperRightArm) : CreateBody(uLeftArmT, upperLeftArm);
            bodies[(int)PlayerBone.LowerFrontArm] = rightLeads ? CreateBody(lRightArmT, lowerRightArm) : CreateBody(lLeftArmT, lowerLeftArm);
            bodies[(int)PlayerBone.UpperBackArm] = rightLeads ? CreateBody(uLeftArmT, upperLeftArm) : CreateBody(uRightArmT, upperRightArm);
            bodies[(int)PlayerBone.LowerBackArm] = rightLeads ? CreateBody(lLeftArmT, lowerLeftArm) : CreateBody(lRightArmT, lowerRightArm);

            bodies[(int)PlayerBone.UpperFrontLeg] = rightLeads ? CreateBody(uRightLegT, upperRightLeg) : CreateBody(uLeftLegT, upperLeftLeg);
            bodies[(int)PlayerBone.LowerFrontLeg] = rightLeads ? CreateBody(lRightLegT, lowerRightLeg) : CreateBody(lLeftLegT, lowerLeftLeg);
            bodies[(int)PlayerBone.FrontFoot] = rightLeads ? CreateBody(rFootT, rightFoot) : CreateBody(lFootT, leftFoot);
            
            bodies[(int)PlayerBone.UpperBackLeg] = rightLeads ? CreateBody(uLeftLegT, upperLeftLeg) : CreateBody(uRightLegT, upperRightLeg);
            bodies[(int)PlayerBone.LowerBackLeg] = rightLeads ? CreateBody(lLeftLegT, lowerLeftLeg) : CreateBody(lRightLegT, lowerRightLeg);
            bodies[(int)PlayerBone.BackFoot] = rightLeads ? CreateBody(lFootT, leftFoot) : CreateBody(rFootT, rightFoot);

            rootBone = lSpineT; 
            rootBindPosition = lSpineT.localPosition;
            
            parts = GetComponentsInChildren<SpriteRenderer>(true);
            
            if (TryGetComponent(out Animator animator) && animator.enabled && animator.runtimeAnimatorController != null)
            {
                animator.enabled = false;
            }
            return true;
        }

        private static Body CreateBody(Transform bone, Joint joint)
            => new Body { Bone = bone, BindRotation = bone.localRotation, Joint = joint };

        private static Transform FindBone(Transform parent, string boneName)
        {
            foreach (Transform child in parent)
            {
                if (child.name == boneName) return child;
                var found = FindBone(child, boneName);
                if (found != null) return found;
            }
            return null;
        }
    }
}
