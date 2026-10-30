using UnityEngine;

namespace WelpTimeToDie.RectanglePrototype
{
    using System;
    using UnityEngine;

    namespace WelpTimeToDie.RectanglePrototype
    {
        public enum GolemBone { Spine, Head, FrontArm, BackArm }

        [DefaultExecutionOrder(100)]
        [DisallowMultipleComponent]
        public sealed class GolemPhysicsAnimator : MonoBehaviour
        {
            [Serializable]
            public struct Joint
            {
                public string boneName;
                [Tooltip("Bone length in art units (pixels / pixels-per-unit). Sets the gravity lever.")]
                public float length;
                [Tooltip("Spring natural frequency in Hz. Higher is snappier.")]
                public float frequency;
                [Tooltip("1 = critically damped. Lower values overshoot and wobble.")]
                [Range(0, 2)] public float dampingRatio;
                [Tooltip("Multiplier for gravity and root-acceleration (inertia) torques.")]
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
                // Degrees, forward-positive: torso/head lean toward the facing side; arms swing
                // forward and up from hanging (90 = horizontal, 180 = overhead). Hip in art units.
                public float Spine, Head, Front, Back, Hip, Stiffness;
                public Pose(float spine, float head, float front, float back, float hip, float stiffness)
                {
                    Spine = spine; Head = head; Front = front; Back = back; Hip = hip; Stiffness = stiffness;
                }
            }

            private const int MaxSubsteps = 12;
            private const float MaxAngularVelocity = 40f, MaxRootAcceleration = 60f, VelocitySmoothing = 0.06f;
            private static readonly Color HurtTint = new Color(1f, 0.55f, 0.55f);
            private static readonly Color DeadTint = new Color(0.5f, 0.5f, 0.55f);

            [SerializeField, Tooltip("Uniform world scale. 1.45 makes the 128 px golem as tall as the old 1.8-unit rectangle.")]
            private float scale = 1.45f;
            [SerializeField] private Joint spine = new Joint { boneName = "spine", length = 0.44f, frequency = 2.2f, dampingRatio = 0.55f, gravityScale = 1 };
            [SerializeField] private Joint head = new Joint { boneName = "head", length = 0.32f, frequency = 3.2f, dampingRatio = 0.35f, gravityScale = 1 };
            [SerializeField] private Joint leftArm = new Joint { boneName = "left arm", length = 0.41f, frequency = 2.6f, dampingRatio = 0.32f, gravityScale = 1 };
            [SerializeField] private Joint rightArm = new Joint { boneName = "right arm", length = 0.40f, frequency = 2.6f, dampingRatio = 0.32f, gravityScale = 1 };
            [SerializeField, Tooltip("Vertical hip spring (crouch, bob, slam dip) in Hz.")]
            private float hipFrequency = 3f;
            [SerializeField, Range(0, 2)] private float hipDampingRatio = 0.5f;
            [SerializeField, Tooltip("Units/second squared.")] private float gravity = 9.81f;
            [SerializeField, Range(30, 480)] private int simulationRate = 120;
            [SerializeField, Range(0, 1), Tooltip("How strongly arm swings twist the torso the other way.")]
            private float armReaction = 0.15f;
            [SerializeField, Tooltip("Angular velocity (rad/s) added to the torso by a full hit.")]
            private float hitImpulse = 6f;
            [SerializeField, Range(30, 90), Tooltip("Furthest the torso can tip before it rests on the floor.")]
            private float groundAngle = 82f;

            private Body[] bodies;
            private Transform spineBone;
            private Vector3 spineBindPosition;
            private SpriteRenderer[] parts;
            private bool rigMissing;
            private float hip, hipVelocity, hipTarget, stiffness = 1, accumulator;
            private Vector3 lastRootPosition;
            private Vector2 rootVelocity, rootAcceleration;
            private bool hasRootSample, wasAlive = true;
            private int lastFrame = -1, lastHitFrame = -1, deathSide = 1;

            // Current bone offset from the rig's bind pose, forward-positive like the poses (degrees).
            public float BoneOffset(GolemBone bone)
            {
                if (!EnsureRig()) return 0;
                float degrees = bodies[(int)bone].Angle * Mathf.Rad2Deg;
                return bone == GolemBone.Spine || bone == GolemBone.Head ? -degrees : degrees;
            }

            // Called by RectangleStage whenever a combat snapshot is drawn (live, preview, or playback).
            public void Show(RectangleFighter fighter, int frame, bool preview)
            {
                if (!EnsureRig()) return;
                transform.position = new Vector3(fighter.X, fighter.Y, transform.position.z);
                transform.localScale = new Vector3(fighter.Facing < 0 ? -scale : scale, scale, 1);

                // Rewinds happen on Reset and when a preview hands back to the live world.
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

            private void Update() => Simulate(Mathf.Min(Time.unscaledDeltaTime, 0.1f));

            private void DetectEvents(RectangleFighter fighter, int frame)
            {
                if (fighter.HurtFlash == 4 && frame != lastHitFrame)
                {
                    lastHitFrame = frame;
                    float direction = Mathf.Abs(fighter.VelocityX) > 0.1f ? Mathf.Sign(fighter.VelocityX) : -fighter.Facing;
                    // Blocked or armoured hits do not stun: the golem only shudders.
                    ApplyHit(direction, fighter.Stun > 0 || !fighter.IsAlive ? 1f : 0.45f);
                }
                if (wasAlive && !fighter.IsAlive)
                {
                    var torso = bodies[(int)GolemBone.Spine];
                    deathSide = torso.Angle + torso.Velocity * 0.2f >= 0 ? 1 : -1;
                }
            }

            // direction is the world x direction the hit pushes the golem.
            private void ApplyHit(float direction, float strength)
            {
                float mirror = Mirror();
                // Positive local rotation tips upright bones toward local -x and swings hanging
                // arms toward local +x, so one sign throws the torso away and flings hands back.
                float kick = -direction * mirror * hitImpulse * strength;
                bodies[(int)GolemBone.Spine].Velocity += kick;
                bodies[(int)GolemBone.Head].Velocity += kick * 1.6f;
                bodies[(int)GolemBone.FrontArm].Velocity += kick * 1.2f;
                bodies[(int)GolemBone.BackArm].Velocity += kick * 0.8f;
                hipVelocity -= 0.8f * strength;
            }

            private void SetTargets(RectangleFighter fighter)
            {
                if (!fighter.IsAlive)
                {
                    stiffness = 0.2f;
                    hipTarget = -0.3f;
                    SetTarget(GolemBone.Spine, deathSide * groundAngle * Mathf.Deg2Rad);
                    SetTarget(GolemBone.Head, deathSide * 25 * Mathf.Deg2Rad);
                    SetTarget(GolemBone.FrontArm, 0);
                    SetTarget(GolemBone.BackArm, 0);
                    return;
                }
                var pose = PoseFor(fighter);
                stiffness = pose.Stiffness;
                hipTarget = pose.Hip;
                SetTarget(GolemBone.Spine, -pose.Spine * Mathf.Deg2Rad);
                SetTarget(GolemBone.Head, -pose.Head * Mathf.Deg2Rad);
                SetTarget(GolemBone.FrontArm, pose.Front * Mathf.Deg2Rad);
                SetTarget(GolemBone.BackArm, pose.Back * Mathf.Deg2Rad);
            }

            private void SetTarget(GolemBone bone, float radians) => bodies[(int)bone].Target = radians;

            private static Pose PoseFor(RectangleFighter f)
            {
                var idle = new Pose(0, 0, 8, -4, 0, 1);
                if (f.Stun > 0)
                {
                    float wobble = f.Stun % 2 == 0 ? 6 : -6;
                    return new Pose(-10 + wobble, -25, -15, -20, -0.06f, 0.35f);
                }
                var spec = RectangleCombat.Spec(f, f.Move);
                int e = f.Elapsed;
                switch (f.Move)
                {
                    case CombatMove.Move:
                        {
                            float phase = e / (float)Mathf.Max(1, spec.Duration) * 2 * Mathf.PI;
                            float swing = Mathf.Sin(phase);
                            return new Pose(6 + 3 * Mathf.Sin(2 * phase), 2, 8 + 28 * swing, -4 - 28 * swing,
                                -0.05f + 0.04f * Mathf.Cos(2 * phase), 1);
                        }
                    case CombatMove.Dash: return new Pose(22, -10, -45, -55, -0.06f, 1.2f);
                    case CombatMove.Jump:
                        return f.VelocityY > 0
                        ? new Pose(-5, -10, 140, 130, 0.02f, 1)
                        : new Pose(5, 5, 70, 60, 0, 1);
                    case CombatMove.Block: return new Pose(8, 12, 80, 70, -0.05f, 1.4f);
                    case CombatMove.Wait: return new Pose(2, 4, 4, -2, -0.03f, 1);
                    case CombatMove.Punch:
                        return Strike(e, spec,
                        new Pose(-10, 0, -55, 15, -0.03f, 1),
                        new Pose(18, 5, 95, -25, -0.05f, 1.8f),
                        new Pose(6, 2, 35, -8, -0.02f, 1));
                    case CombatMove.Heavy:
                        return Strike(e, spec,
                        new Pose(-18, -12, 165, 160, 0.03f, 1),
                        new Pose(35, 15, 60, 55, -0.14f, 2f),
                        new Pose(15, 6, 25, 20, -0.06f, 0.8f));
                    case CombatMove.Special:
                        return Strike(e, spec,
                        new Pose(12, 6, -35, -10, -0.1f, 1),
                        new Pose(-15, -10, 150, 20, 0.04f, 1.8f),
                        new Pose(-4, -2, 40, 0, 0, 1));
                    default: return idle;
                }
            }

            private static Pose Strike(int elapsed, MoveSpec spec, Pose windUp, Pose hit, Pose recover)
                => elapsed < spec.Startup ? windUp : elapsed < spec.Startup + 3 ? hit : recover;

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
                // Combat snapshots move the root in steps; smoothing turns them into usable accelerations.
                Vector2 smoothed = Vector2.Lerp(rootVelocity, raw, 1 - Mathf.Exp(-deltaTime / VelocitySmoothing));
                rootAcceleration = Vector2.ClampMagnitude((smoothed - rootVelocity) / deltaTime, MaxRootAcceleration);
                rootVelocity = smoothed;
            }

            private void Step(float dt)
            {
                float mirror = Mirror();
                float worldScale = Mathf.Abs(transform.lossyScale.y);
                // Gravity plus the pseudo-force from the accelerating root, applied at each rod's centre.
                Vector2 load = new Vector2(0, -gravity) - rootAcceleration;
                foreach (var body in bodies)
                {
                    float w = 2 * Mathf.PI * body.Joint.frequency * stiffness;
                    float spring = w * w * (body.Target - body.Angle) - 2 * body.Joint.dampingRatio * w * body.Velocity;
                    Vector2 direction = ((Vector2)body.Bone.TransformVector(Vector3.right)).normalized;
                    float length = Mathf.Max(0.05f, body.Joint.length * worldScale);
                    // Rod about its end: alpha = (m L/2 * (dir x load)) / (m L^2 / 3).
                    float torque = 1.5f / length * (direction.x * load.y - direction.y * load.x);
                    body.Acceleration = spring + torque * body.Joint.gravityScale * mirror;
                }
                var torso = bodies[(int)GolemBone.Spine];
                torso.Acceleration -= armReaction * (bodies[(int)GolemBone.FrontArm].Acceleration + bodies[(int)GolemBone.BackArm].Acceleration);
                foreach (var body in bodies)
                {
                    body.Velocity = Mathf.Clamp(body.Velocity + body.Acceleration * dt, -MaxAngularVelocity, MaxAngularVelocity);
                    body.Angle += body.Velocity * dt;
                }
                // The floor stops a toppling torso, with a small bounce.
                float limit = groundAngle * Mathf.Deg2Rad;
                if (Mathf.Abs(torso.Angle) > limit)
                {
                    float side = Mathf.Sign(torso.Angle);
                    torso.Angle = side * limit;
                    if (torso.Velocity * side > 0) torso.Velocity *= -0.25f;
                }
                float hw = 2 * Mathf.PI * hipFrequency * Mathf.Max(0.5f, stiffness);
                hipVelocity += (hw * hw * (hipTarget - hip) - 2 * hipDampingRatio * hw * hipVelocity) * dt;
                hip += hipVelocity * dt;
            }

            private void Apply()
            {
                foreach (var body in bodies)
                    body.Bone.localRotation = body.BindRotation * Quaternion.Euler(0, 0, body.Angle * Mathf.Rad2Deg);
                spineBone.localPosition = spineBindPosition + new Vector3(0, hip, 0);
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
                Transform spineT = FindBone(transform, spine.boneName), headT = FindBone(transform, head.boneName);
                Transform leftT = FindBone(transform, leftArm.boneName), rightT = FindBone(transform, rightArm.boneName);
                if (spineT == null || headT == null || leftT == null || rightT == null)
                {
                    rigMissing = true;
                    Debug.LogError($"{name}: GolemPhysicsAnimator needs bones '{spine.boneName}', '{head.boneName}', " +
                        $"'{leftArm.boneName}' and '{rightArm.boneName}'. Add it to the root of a Golem.psb instance.", this);
                    return false;
                }
                // The arm on the art's +x side leads when facing right.
                bool rightLeads = transform.InverseTransformPoint(rightT.position).x >= transform.InverseTransformPoint(leftT.position).x;
                bodies = new Body[4];
                bodies[(int)GolemBone.Spine] = CreateBody(spineT, spine);
                bodies[(int)GolemBone.Head] = CreateBody(headT, head);
                bodies[(int)GolemBone.FrontArm] = rightLeads ? CreateBody(rightT, rightArm) : CreateBody(leftT, leftArm);
                bodies[(int)GolemBone.BackArm] = rightLeads ? CreateBody(leftT, leftArm) : CreateBody(rightT, rightArm);
                spineBone = spineT;
                spineBindPosition = spineT.localPosition;
                parts = GetComponentsInChildren<SpriteRenderer>(true);
                if (TryGetComponent(out Animator animator) && animator.enabled && animator.runtimeAnimatorController != null)
                {
                    animator.enabled = false;
                    Debug.LogWarning($"{name}: disabled the Animator so it does not overwrite the physics-driven bones.", this);
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
}
