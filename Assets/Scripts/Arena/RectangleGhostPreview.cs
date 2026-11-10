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
        public const float PlaybackRate = 16f;
        private const float EndHold = 0.42f;
        private const int MaxKeyPoses = 3;
        private readonly Material material;
        private readonly GhostFighter player, enemy;

        public RectangleGhostPreview(PlayerPhysicsAnimator playerRig, GolemPhysicsAnimator enemyRig,
            SpriteRenderer playerBody, SpriteRenderer enemyBody)
        {
            var shader = Resources.Load<Shader>("CombatGhost");
            material = shader != null ? new Material(shader) { hideFlags = HideFlags.DontSave } : null;
            player = new GhostFighter("Player", playerRig != null ? playerRig.transform : playerBody.transform,
                playerRig != null, material, new Color(0.47f, 0.91f, 1f));
            enemy = new GhostFighter("Enemy", enemyRig != null ? enemyRig.transform : enemyBody.transform,
                enemyRig != null, material, new Color(1f, 0.75f, 0.46f));
        }

        public void Capture(RectangleCombat origin, RectanglePlan plan, PlayerPhysicsAnimator playerRig, GolemPhysicsAnimator enemyRig)
        {
            playerRig?.BeginPreview();
            enemyRig?.BeginPreview();
            try
            {
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
                }
                player.SetKeyPoses(origin, plan, 0);
                enemy.SetKeyPoses(origin, plan, 1);
            }
            finally
            {
                playerRig?.EndPreview();
                enemyRig?.EndPreview();
            }
        }

        public RectangleCombat Draw(RectangleCombat origin, RectanglePlan plan, float time)
        {
            float duration = plan.Frames.Count / PlaybackRate;
            float elapsed = Mathf.Repeat(time, duration + EndHold);
            float sample = Mathf.Min(elapsed * PlaybackRate, plan.Frames.Count);
            int index = Mathf.FloorToInt(sample);
            float blend = sample - index;
            float alpha = Mathf.Lerp(0.28f, 0.78f, Mathf.SmoothStep(0f, 1f, elapsed / 0.16f))
                * (1f - Mathf.SmoothStep(0f, 1f, (elapsed - duration - 0.20f) / (EndHold - 0.20f)));
            var frame = index == 0 ? origin : plan.Frames[index - 1];
            player.Draw(index, Mathf.Min(index + 1, plan.Frames.Count), blend, alpha, frame.Player.HurtFlash);
            enemy.Draw(index, Mathf.Min(index + 1, plan.Frames.Count), blend, alpha, frame.Fighters[1].HurtFlash);
            return frame;
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
            private readonly Material material;
            private readonly LocalPose[] samples;
            private readonly GhostPose motion;
            private readonly GhostPose[] keys = new GhostPose[MaxKeyPoses];
            private readonly List<int> keyFrames = new List<int>(MaxKeyPoses);

            public GhostFighter(string name, Transform live, bool rigged, Material material, Color tint)
            {
                source = live.GetComponentsInChildren<Transform>(true);
                this.rigged = rigged;
                this.tint = tint;
                this.material = material;
                samples = new LocalPose[(RectangleCombat.PreviewFrames + 1) * source.Length];
                motion = new GhostPose(name + " preview", source, material, 9);
            }

            public void Capture(int sample, RectangleFighter fighter)
            {
                int offset = sample * source.Length;
                for (int i = 0; i < source.Length; i++)
                    samples[offset + i] = new LocalPose(source[i]);
                if (!rigged)
                {
                    var body = source[0];
                    var center = new Vector3(fighter.X, fighter.Y + (fighter.IsAlive ? 0.9f : 0.15f), body.position.z);
                    samples[offset].Position = body.parent != null ? body.parent.InverseTransformPoint(center) : center;
                    samples[offset].Scale = fighter.IsAlive ? new Vector3(0.9f, 1.8f, 1f) : new Vector3(1.8f, 0.3f, 1f);
                }
            }

            public void SetKeyPoses(RectangleCombat origin, RectanglePlan plan, int fighterIndex)
            {
                keyFrames.Clear();
                int last = plan.Frames.Count;
                var start = origin.Fighters[fighterIndex];
                var previous = start;
                int contact = 0, strike = 0, apex = 0, travel = 0;
                float highest = start.Y + 0.2f;
                for (int i = 1; i <= last; i++)
                {
                    var fighter = plan.Frames[i - 1].Fighters[fighterIndex];
                    if (contact == 0 && (fighter.Health < previous.Health || (fighter.HitTargets & ~previous.HitTargets) != 0))
                        contact = i;
                    var spec = RectangleCombat.Spec(fighter, fighter.Move);
                    if (strike == 0 && spec.Damage > 0 && fighter.Elapsed == spec.Startup)
                        strike = i;
                    if (fighter.Y > highest)
                    {
                        highest = fighter.Y;
                        apex = i;
                    }
                    if (travel == 0 && Distance(start, fighter) > 0.8f) travel = i;
                    previous = fighter;
                }

                AddKey(contact);
                AddKey(strike);
                AddKey(apex);
                if (Distance(start, previous) > 0.35f) AddKey(last);
                if (fighterIndex == 0) AddKey(travel);
                if (keyFrames.Count == 0 && fighterIndex == 0 && plan.Frames[0].Player.Move == CombatMove.Block)
                    AddKey(Mathf.Max(1, last / 2));

                for (int i = 0; i < keys.Length; i++)
                {
                    if (i >= keyFrames.Count)
                    {
                        keys[i]?.SetVisible(false);
                        continue;
                    }
                    keys[i] ??= new GhostPose(motion.Name + " key pose " + (i + 1), source, material, 7 + (i == 0 ? 1 : 0));
                    int frame = keyFrames[i];
                    keys[i].Draw(samples, frame, frame, 0f, WithAlpha(tint, frame == contact ? 0.32f : 0.24f));
                }
            }

            private void AddKey(int frame)
            {
                if (frame == 0 || keyFrames.Count == MaxKeyPoses) return;
                foreach (int key in keyFrames)
                    if (Mathf.Abs(key - frame) <= 1) return;
                keyFrames.Add(frame);
            }

            private static float Distance(RectangleFighter a, RectangleFighter b)
                => Mathf.Abs(a.X - b.X) + Mathf.Abs(a.Y - b.Y);

            public void Draw(int sample, int next, float blend, float alpha, int hurtFlash)
            {
                Color color = Color.Lerp(tint, Color.white, hurtFlash * 0.16f);
                motion.Draw(samples, sample, next, blend, WithAlpha(color, alpha));
            }

            public void Hide()
            {
                motion.SetVisible(false);
                foreach (var key in keys) key?.SetVisible(false);
            }

            public void Dispose()
            {
                motion.Dispose();
                foreach (var key in keys) key?.Dispose();
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
            public string Name => root.name;

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
            }

            public void Draw(LocalPose[] samples, int sample, int next, float blend, Color tint)
            {
                int start = sample * nodes.Length, end = next * nodes.Length;
                for (int i = 0; i < nodes.Length; i++)
                {
                    var from = samples[start + i];
                    var to = samples[end + i];
                    var node = nodes[i];
                    node.SetLocalPositionAndRotation(Vector3.LerpUnclamped(from.Position, to.Position, blend),
                        Quaternion.SlerpUnclamped(from.Rotation, to.Rotation, blend));
                    // Keep a facing flip discrete so the silhouette never collapses through a scale of zero.
                    node.localScale = Mathf.Sign(from.Scale.x) == Mathf.Sign(to.Scale.x)
                        ? Vector3.LerpUnclamped(from.Scale, to.Scale, blend) : from.Scale;
                }
                foreach (var sprite in sprites) sprite.color = tint;
                SetVisible(true);
            }

            public void SetVisible(bool visible)
            {
                if (root != null && root.activeSelf != visible) root.SetActive(visible);
            }

            public void Dispose() => UnityEngine.Object.Destroy(root);
        }
    }
}
