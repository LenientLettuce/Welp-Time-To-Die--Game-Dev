using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.U2D.Animation;

namespace WelpTimeToDie.RectanglePrototype
{
    // Samples the existing fighter rigs once per plan. Only the render copies move during the loop.
    internal sealed class RectangleGhostPreview : IDisposable
    {
        private const float PlaybackRate = 15f;
        private const float StartHold = 0.10f, ImpactHold = 0.08f, EndHold = 0.45f;
        private readonly Material material;
        private readonly GhostFighter player, enemy;
        private int firstImpact;

        public RectangleGhostPreview(PlayerPhysicsAnimator playerRig, GolemPhysicsAnimator enemyRig,
            SpriteRenderer playerBody, SpriteRenderer enemyBody)
        {
            var shader = Resources.Load<Shader>("CombatGhost") ?? Shader.Find("Sprites/Default");
            material = shader != null ? new Material(shader) { hideFlags = HideFlags.DontSave } : null;
            player = new GhostFighter("Player", playerRig != null ? playerRig.transform : playerBody.transform,
                playerRig != null, material, new Color(0.47f, 0.91f, 1f), showPath: true);
            enemy = new GhostFighter("Enemy", enemyRig != null ? enemyRig.transform : enemyBody.transform,
                enemyRig != null, material, new Color(1f, 0.75f, 0.46f), showPath: false);
        }

        public void Capture(RectangleCombat origin, RectanglePlan plan, PlayerPhysicsAnimator playerRig, GolemPhysicsAnimator enemyRig)
        {
            playerRig?.BeginPreview();
            enemyRig?.BeginPreview();
            try
            {
                firstImpact = 0;
                player.Capture(0, origin.Player);
                enemy.Capture(0, origin.Fighters[1]);
                for (int i = 0; i < plan.Frames.Count; i++)
                {
                    var frame = plan.Frames[i];
                    playerRig?.Show(frame.Player, frame.Frame, false);
                    enemyRig?.Show(frame.Fighters[1], frame.Frame, false);
                    playerRig?.Simulate(1f / RectangleStage.PlaybackRate);
                    enemyRig?.Simulate(1f / RectangleStage.PlaybackRate);
                    player.Capture(i + 1, frame.Player);
                    enemy.Capture(i + 1, frame.Fighters[1]);
                    if (firstImpact == 0 && (frame.Player.HurtFlash == 4 || frame.Fighters[1].HurtFlash == 4))
                        firstImpact = i + 1;
                }
                player.SetPath(plan.Frames.Count);
            }
            finally
            {
                playerRig?.EndPreview();
                enemyRig?.EndPreview();
            }
        }

        public RectangleCombat Draw(RectangleCombat origin, RectanglePlan plan, float time)
        {
            int count = plan.Frames.Count;
            float duration = Mathf.Max(0.55f, count / PlaybackRate);
            float impactTime = StartHold + duration * firstImpact / count;
            float pause = firstImpact > 0 ? ImpactHold : 0f;
            float finish = StartHold + duration + pause;
            float elapsed = Mathf.Repeat(time, finish + EndHold);
            float playTime = elapsed < impactTime ? elapsed : Mathf.Max(impactTime, elapsed - pause);
            float sample = Mathf.Clamp01((playTime - StartHold) / duration) * count;
            if (firstImpact > 0)
                sample = elapsed >= impactTime && elapsed < impactTime + pause
                    ? firstImpact : EaseAroundImpact(sample, firstImpact);
            int index = Mathf.FloorToInt(sample);
            var frame = index == 0 ? origin : plan.Frames[index - 1];
            int next = Mathf.Min(index + 1, count);
            float alpha = Mathf.SmoothStep(0f, 1f, elapsed / StartHold)
                * (1f - Mathf.SmoothStep(0f, 1f, (elapsed - finish - 0.23f) / (EndHold - 0.23f)));
            bool playerHit = index > 0 && frame.Player.HurtFlash == 4;
            bool enemyHit = index > 0 && frame.Fighters[1].HurtFlash == 4;
            bool contact = playerHit || enemyHit;
            bool close = Mathf.Abs(frame.Player.X - frame.Fighters[1].X) < 2.4f
                && Mathf.Abs(frame.Player.Y - frame.Fighters[1].Y) < 1.6f;
            bool playerCue = player.Draw(index, next, sample - index, alpha, playerHit, !contact);
            enemy.Draw(index, next, sample - index, alpha, enemyHit, !contact && (!close || !playerCue));
            return frame;
        }

        private static float EaseAroundImpact(float sample, int impact)
        {
            const float window = 1.5f;
            float offset = sample - impact;
            if (Mathf.Abs(offset) >= window) return sample;
            float t = Mathf.Abs(offset) / window;
            return impact + offset * t * (2f - t);
        }

        public void Hide()
        {
            player.Hide();
            enemy.Hide();
        }

        public void Dispose()
        {
            player.Dispose();
            enemy.Dispose();
            if (material != null) UnityEngine.Object.Destroy(material);
        }

        private sealed class GhostFighter
        {
            private readonly Transform[] source;
            private readonly bool rigged;
            private readonly Color tint;
            private readonly LocalPose[] samples;
            private readonly RectangleFighter[] fighters = new RectangleFighter[RectangleCombat.PreviewFrames + 1];
            private readonly GhostPose motion;
            private readonly SpriteRenderer[] liveSprites;
            private readonly float[] liveAlpha;
            private readonly LineRenderer path, destination, cue;
            private bool backdropActive;
            private int lastSample;

            public GhostFighter(string name, Transform live, bool rigged, Material material, Color tint, bool showPath)
            {
                source = live.GetComponentsInChildren<Transform>(true);
                this.rigged = rigged;
                this.tint = tint;
                samples = new LocalPose[(RectangleCombat.PreviewFrames + 1) * source.Length];
                liveSprites = live.GetComponentsInChildren<SpriteRenderer>(true);
                liveAlpha = new float[liveSprites.Length];
                motion = new GhostPose(name + " preview", source, material, 9);
                if (showPath)
                {
                    path = CreateLine(name + " preview path", material, 8, 0.055f);
                    destination = CreateLine(name + " preview destination", material, 8, 0.045f);
                }
                cue = CreateLine(name + " preview cue", material, 10, 0.10f);
            }

            public void Capture(int sample, RectangleFighter fighter)
            {
                lastSample = sample;
                fighters[sample] = fighter;
                int offset = sample * source.Length;
                for (int i = 0; i < source.Length; i++)
                    samples[offset + i] = new LocalPose(source[i]);
                if (!rigged)
                {
                    var body = source[0];
                    var center = new Vector3(fighter.X, fighter.Y + (fighter.IsAlive ? fighter.BodyHeight / 2 : 0.15f), body.position.z);
                    samples[offset].Position = body.parent != null ? body.parent.InverseTransformPoint(center) : center;
                    samples[offset].Scale = fighter.IsAlive
                        ? new Vector3(2 * fighter.HalfWidth, fighter.BodyHeight, 1f)
                        : new Vector3(fighter.BodyHeight, 0.3f, 1f);
                }
            }

            public void SetPath(int last)
            {
                Vector3 previous = Feet(fighters[0]);
                path.positionCount = last + 1;
                path.SetPosition(0, previous);
                int points = 1;
                float length = 0f;
                for (int i = 1; i <= last; i++)
                {
                    Vector3 point = Feet(fighters[i]);
                    float distance = Vector3.Distance(previous, point);
                    if (distance < 0.02f && (i < last || distance < 0.001f)) continue;
                    path.SetPosition(points++, point);
                    length += distance;
                    previous = point;
                }
                path.positionCount = points;
                path.enabled = destination.enabled = length >= 0.3f;
                if (!path.enabled) return;

                Vector3 end = path.GetPosition(points - 1);
                const float radius = 0.105f;
                destination.positionCount = 4;
                destination.loop = true;
                destination.SetPosition(0, end + Vector3.up * radius);
                destination.SetPosition(1, end + Vector3.right * radius);
                destination.SetPosition(2, end + Vector3.down * radius);
                destination.SetPosition(3, end + Vector3.left * radius);
            }

            public bool Draw(int sample, int next, float blend, float alpha, bool impact, bool showActionCue)
            {
                var fighter = fighters[sample];
                var following = fighters[next];
                float opacity = fighter.IsPlayer ? 0.90f : 0.82f;
                DimBackdrop(alpha);
                float flash = Mathf.Lerp(fighter.HurtFlash, following.HurtFlash, blend);
                Color color = Color.Lerp(tint, Color.white, flash * 0.09f);
                motion.Draw(samples, sample, next, lastSample, blend, WithAlpha(color, opacity * alpha));
                if (path != null)
                {
                    path.startColor = WithAlpha(tint, 0.25f);
                    path.endColor = WithAlpha(tint, 0.45f);
                    destination.startColor = destination.endColor = WithAlpha(tint, 0.90f);
                }
                cue.enabled = false;
                if (impact || showActionCue) DrawCue(fighter, following, blend, alpha, impact);
                return cue.enabled;
            }

            private void DrawCue(RectangleFighter fighter, RectangleFighter following, float blend, float alpha, bool impact)
            {
                if (alpha <= 0f) return;
                Vector3 feet = Vector3.LerpUnclamped(Feet(fighter), Feet(following), blend);
                float side = fighter.Facing;
                if (impact)
                {
                    var center = feet + new Vector3(0f, 0.78f);
                    float size = Mathf.Lerp(0.20f, 0.42f, blend);
                    SetCue(center, 1f, WithAlpha(Color.white, alpha * (1f - blend)),
                        new Vector2(-size, 0f), new Vector2(0f, size), new Vector2(size, 0f), new Vector2(0f, -size), new Vector2(-size, 0f));
                    return;
                }
                if (!fighter.IsAlive || fighter.Stun > 0) return;
                if (fighter.Move == CombatMove.Block)
                {
                    SetCue(feet, side, WithAlpha(tint, alpha * 0.95f),
                        new Vector2(0.52f, 0.34f), new Vector2(0.73f, 0.53f), new Vector2(0.81f, 1.12f), new Vector2(0.52f, 1.45f));
                    return;
                }

                var spec = RectangleCombat.Spec(fighter, fighter.Move);
                float phase = fighter.Elapsed + blend - spec.Startup;
                if (spec.Damage == 0 || spec.Projectile || phase < -1f || phase >= spec.Active + 1f) return;
                float intensity = Mathf.Min(Mathf.Clamp01(phase + 1f), Mathf.Clamp01(spec.Active + 1f - phase));
                var strikeColor = WithAlpha(tint, alpha * intensity);
                switch (fighter.Move)
                {
                    case CombatMove.Uppercut:
                        SetCue(feet, side, strikeColor, new Vector2(0.45f, 0.38f), new Vector2(1.03f, 0.84f), new Vector2(0.97f, 1.63f));
                        break;
                    case CombatMove.Heavy:
                        SetCue(feet, side, strikeColor, new Vector2(0.42f, 1.65f), new Vector2(1.06f, 1.34f), new Vector2(1.48f, 0.62f));
                        break;
                    case CombatMove.Sweep:
                        SetCue(feet, side, strikeColor, new Vector2(0.38f, 0.14f), new Vector2(0.94f, 0.03f), new Vector2(1.48f, 0.21f));
                        break;
                    default:
                        bool kick = fighter.IsPlayer && (fighter.Move == CombatMove.Kick || fighter.Move == CombatMove.DashKick);
                        float height = kick ? 0.56f : 1.03f;
                        SetCue(feet, side, strikeColor, new Vector2(0.45f, height - 0.12f),
                            new Vector2(0.95f, height), new Vector2(kick ? 1.63f : 1.32f, height + 0.12f));
                        break;
                }
            }

            private LineRenderer CreateLine(string name, Material lineMaterial, int order, float width)
            {
                var node = new GameObject(name) { hideFlags = HideFlags.DontSave, layer = source[0].gameObject.layer };
                node.transform.SetParent(motion.Root.parent, false);
                var line = node.AddComponent<LineRenderer>();
                line.sharedMaterial = lineMaterial;
                line.sortingLayerID = motion.SortingLayer;
                line.sortingOrder = order;
                line.startWidth = line.endWidth = width;
                line.numCornerVertices = 3;
                line.numCapVertices = 3;
                line.enabled = false;
                return line;
            }

            private void SetCue(Vector3 feet, float side, Color color, Vector2 a, Vector2 b, Vector2 c,
                Vector2? d = null, Vector2? e = null)
            {
                cue.positionCount = e.HasValue ? 5 : d.HasValue ? 4 : 3;
                cue.SetPosition(0, feet + new Vector3(a.x * side, a.y));
                cue.SetPosition(1, feet + new Vector3(b.x * side, b.y));
                cue.SetPosition(2, feet + new Vector3(c.x * side, c.y));
                if (d.HasValue) cue.SetPosition(3, feet + new Vector3(d.Value.x * side, d.Value.y));
                if (e.HasValue) cue.SetPosition(4, feet + new Vector3(e.Value.x * side, e.Value.y));
                cue.startColor = cue.endColor = color;
                cue.enabled = true;
            }

            private static Vector3 Feet(RectangleFighter fighter) => new Vector3(fighter.X, fighter.Y + 0.19f);

            private void DimBackdrop(float alpha)
            {
                for (int i = 0; i < liveSprites.Length; i++)
                {
                    var sprite = liveSprites[i];
                    var color = sprite.color;
                    if (!backdropActive) liveAlpha[i] = color.a;
                    color.a = liveAlpha[i] * Mathf.Lerp(1f, 0.17f, alpha);
                    sprite.color = color;
                }
                backdropActive = true;
            }

            public void Hide()
            {
                motion.SetVisible(false);
                cue.enabled = false;
                if (path != null) path.enabled = destination.enabled = false;
                if (!backdropActive) return;
                for (int i = 0; i < liveSprites.Length; i++)
                {
                    var sprite = liveSprites[i];
                    if (sprite == null) continue;
                    var color = sprite.color;
                    color.a = liveAlpha[i];
                    sprite.color = color;
                }
                backdropActive = false;
            }

            public void Dispose()
            {
                Hide();
                motion.Dispose();
                if (path != null)
                {
                    UnityEngine.Object.Destroy(path.gameObject);
                    UnityEngine.Object.Destroy(destination.gameObject);
                }
                UnityEngine.Object.Destroy(cue.gameObject);
            }

            private static Color WithAlpha(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);
        }

        private struct LocalPose
        {
            public Vector3 Position, Scale;
            public Quaternion Rotation;

            public LocalPose(Transform source)
            {
                Position = source.localPosition;
                Scale = source.localScale;
                Rotation = source.localRotation;
            }
        }

        private sealed class GhostPose
        {
            private readonly Transform[] nodes;
            private readonly SpriteRenderer[] sprites;
            private readonly GameObject root;
            public Transform Root => root.transform;
            public int SortingLayer { get; }

            public GhostPose(string name, Transform[] source, Material material, int order)
            {
                root = new GameObject(name) { hideFlags = HideFlags.DontSave };
                root.SetActive(false);
                root.transform.SetParent(source[0].parent, false);
                nodes = new Transform[source.Length];
                nodes[0] = root.transform;
                var copies = new Dictionary<Transform, Transform> { [source[0]] = nodes[0] };
                for (int i = 1; i < source.Length; i++)
                {
                    var node = new GameObject(source[i].name) { layer = source[i].gameObject.layer };
                    node.transform.SetParent(copies[source[i].parent], false);
                    node.SetActive(source[i].gameObject.activeSelf);
                    copies.Add(source[i], nodes[i] = node.transform);
                }

                root.layer = source[0].gameObject.layer;
                var group = root.AddComponent<SortingGroup>();
                group.sortingOrder = order;
                var parts = new List<SpriteRenderer>();
                for (int i = 0; i < source.Length; i++)
                {
                    if (!source[i].TryGetComponent(out SpriteRenderer live)) continue;
                    if (parts.Count == 0) group.sortingLayerID = live.sortingLayerID;
                    var sprite = nodes[i].gameObject.AddComponent<SpriteRenderer>();
                    sprite.sprite = live.sprite;
                    sprite.sharedMaterial = material != null ? material : live.sharedMaterial;
                    sprite.sortingLayerID = live.sortingLayerID;
                    sprite.sortingOrder = live.sortingOrder;
                    sprite.flipX = live.flipX;
                    sprite.flipY = live.flipY;
                    parts.Add(sprite);
                    if (!source[i].TryGetComponent(out SpriteSkin skin) || !skin.enabled || skin.rootBone == null) continue;
                    var ghostSkin = nodes[i].gameObject.AddComponent<SpriteSkin>();
                    var bones = new Transform[skin.boneTransforms.Length];
                    for (int j = 0; j < bones.Length; j++) copies.TryGetValue(skin.boneTransforms[j], out bones[j]);
                    ghostSkin.SetRootBone(copies[skin.rootBone]);
                    ghostSkin.SetBoneTransforms(bones);
                }
                sprites = parts.ToArray();
                SortingLayer = group.sortingLayerID;
            }

            public void Draw(LocalPose[] samples, int sample, int next, int last, float blend, Color tint)
            {
                int start = sample * nodes.Length, end = next * nodes.Length;
                Vector3 before = samples[Mathf.Max(0, sample - 1) * nodes.Length].Position;
                Vector3 after = samples[Mathf.Min(last, next + 1) * nodes.Length].Position;
                for (int i = 0; i < nodes.Length; i++)
                {
                    var from = samples[start + i];
                    var to = samples[end + i];
                    var node = nodes[i];
                    Vector3 position = i == 0 && sample != next
                        ? SmoothPosition(before, from.Position, to.Position, after, blend)
                        : Vector3.LerpUnclamped(from.Position, to.Position, blend);
                    node.SetLocalPositionAndRotation(position,
                        Quaternion.SlerpUnclamped(from.Rotation, to.Rotation, blend));
                    // Keep a facing flip discrete so the silhouette never collapses through a scale of zero.
                    node.localScale = Mathf.Sign(from.Scale.x) == Mathf.Sign(to.Scale.x)
                        ? Vector3.LerpUnclamped(from.Scale, to.Scale, blend) : from.Scale;
                }
                foreach (var sprite in sprites) sprite.color = tint;
                SetVisible(true);
            }

            private static Vector3 SmoothPosition(Vector3 before, Vector3 from, Vector3 to, Vector3 after, float t)
                => new Vector3(SmoothAxis(before.x, from.x, to.x, after.x, t),
                    SmoothAxis(before.y, from.y, to.y, after.y, t), Mathf.LerpUnclamped(from.z, to.z, t));

            // Preserve the simulated endpoints and flatten the tangent at stops or reversals.
            private static float SmoothAxis(float before, float from, float to, float after, float t)
            {
                float step = to - from;
                float startSlope = JoinSlope(from - before, step);
                float endSlope = JoinSlope(step, after - to);
                return Mathf.LerpUnclamped(from, to, t * t * (3f - 2f * t))
                    + t * (1f - t) * ((1f - t) * startSlope - t * endSlope);
            }

            private static float JoinSlope(float previous, float next)
                => previous * next > 0f ? 2f * previous * next / (previous + next) : 0f;

            public void SetVisible(bool visible)
            {
                if (root != null && root.activeSelf != visible) root.SetActive(visible);
            }

            public void Dispose() => UnityEngine.Object.Destroy(root);
        }
    }
}
