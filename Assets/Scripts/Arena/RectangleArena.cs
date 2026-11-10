using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace WelpTimeToDie.RectanglePrototype
{
    public sealed class RectangleArena : MonoBehaviour
    {
        [SerializeField] private RectangleStage stage;
        private readonly Dictionary<CombatMove, Button> moves = new Dictionary<CombatMove, Button>();
        private RectangleCombat world, playbackOrigin;
        [SerializeField] private UnityEngine.UIElements.VisualTreeAsset hudAsset;
        [SerializeField] private PanelSettings savedPanel;
        [SerializeField] private WelpTimeToDie.Levels.ProceduralTerrainMap terrain;
        [SerializeField] private WelpTimeToDie.Cameras.MapCameraController mapCamera;
        private float strength = 1;
        private bool paused;
        private VisualElement root;
        public RectangleCombat World => world;
        public RectanglePlan CurrentPlan => plan;
        public bool IsPlaying => playing;
        public bool IsWatching => previewPlan != null;
        public bool IsPaused => paused;
        public UIDocument Document => document;
        private WelpTimeToDie.Levels.TerrainHeightMap initialMap;
        private WelpTimeToDie.Levels.TerrainFeatureState initialTerrain;
        private RectanglePlan plan, activePlan, previewPlan;
        private CombatMove selected = CombatMove.Punch;
        private float direction = 1;
        private int playbackIndex;
        private double playbackTime;
        private float previewTime;
        private int previewTerrainRevision = -1;
        private bool playing, showPreview = true;
        private UIDocument document;
        private Label status, help;
        private Label playerHealth, enemyHealth, resultTitle, resultDetails;
        private VisualElement playerHealthFill, enemyHealthFill, resultOverlay, header, footer;
        private Button rematchButton;
        private Button goButton, aimLeft, aimRight;
        private Toggle previewToggle;

        private void OnEnable()
        {
            if (stage == null)
            {
                Debug.LogError("Assign the combat stage in Main before starting playback.", this);
                return;
            }
            document = GetComponent<UIDocument>();
            if (document == null) document = gameObject.AddComponent<UIDocument>();
            document.panelSettings = savedPanel;
            document.visualTreeAsset = hudAsset;
            root = document.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            status = root.Q<Label>("arenaStatus"); help = root.Q<Label>("arenaHelp");
            header = root.Q("arenaHeader");
            footer = root.Q("arenaFooter");
            playerHealth = root.Q<Label>("playerHealth");
            enemyHealth = root.Q<Label>("enemyHealth");
            playerHealthFill = root.Q("playerHealthFill");
            enemyHealthFill = root.Q("enemyHealthFill");
            resultOverlay = root.Q("resultOverlay");
            resultTitle = root.Q<Label>("resultTitle");
            resultDetails = root.Q<Label>("resultDetails");
            rematchButton = root.Q<Button>("rematchButton");
            rematchButton.clicked += Restart;
            foreach (CombatMove move in new[] { CombatMove.Move, CombatMove.Punch, CombatMove.Uppercut,
                CombatMove.Kick, CombatMove.DashKick, CombatMove.Heavy, CombatMove.Sweep, CombatMove.Backstep,
                CombatMove.Block, CombatMove.Wait, CombatMove.Dash, CombatMove.Jump })
            {
                var choice = move;
                var button = root.Q<Button>("move" + move);
                button.clicked += () => Select(choice);
                moves.Add(move, button);
            }
            goButton = root.Q<Button>("goAction"); goButton.clicked += Commit;
            previewToggle = root.Q<Toggle>("showPreview");
            previewToggle.SetValueWithoutNotify(showPreview);
            previewToggle.RegisterValueChangedCallback(OnPreviewChanged);
            root.Q<Button>("resetEncounter").clicked += Restart;
            root.Q<Button>("pauseAction").clicked += () => paused = !paused;
            root.Q<Button>("stepFrame").clicked += StepFrame;
            aimLeft = root.Q<Button>("aimLeft"); aimLeft.clicked += () => Aim(-1);
            aimRight = root.Q<Button>("aimRight"); aimRight.clicked += () => Aim(1);
            root.Q<Slider>("dashPower").RegisterValueChangedCallback(e => { strength = e.newValue; RefreshPrediction(); });
            if (terrain != null) terrain.Generated += Restart;
            Restart();
        }

        private bool IsOverHud(Vector2 screen)
        {
            if (root?.panel == null) return false;
            var picked = root.panel.Pick(RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screen.x, Screen.height - screen.y)));
            return picked != null && picked != root && picked.name != "arenaRoot";
        }

        public void Aim(float facing)
        {
            if (playing || world == null || world.Finished || !world.Player.IsReady) return;
            float nextDirection = facing < 0 ? -1 : 1;
            if (direction == nextDirection) return;
            direction = nextDirection;
            RefreshPrediction();
        }

        public void StepFrame()
        {
            if (world == null || world.Finished) return;
            if (!playing && plan == null)
            {
                world = world.Copy(); world.Step(); world.PlanEnemies();
                paused = true; RefreshPrediction(); stage.AdvanceRig(1f / RectangleStage.PlaybackRate);
                return;
            }
            if (!playing) Commit();
            if (!playing) return;
            paused = true;
            world = activePlan.Frames[playbackIndex++].Copy();
            playbackTime = 0;
            if (playbackIndex == activePlan.Frames.Count) { playing = false; RefreshPrediction(); }
            else Render();
            stage.AdvanceRig(1f / RectangleStage.PlaybackRate);
        }

        public void Restart()
        {
            HidePreview();
            paused = false;
            world = RectangleCombat.CreateBasic(FighterClass.Barbarian);
            world.TacticalEnemies = true;
            if (terrain != null && terrain.HeightMap != null)
            {
                if (initialMap != terrain.HeightMap)
                { initialMap = terrain.HeightMap; initialTerrain = terrain.Features.State.Copy(); }
                terrain.Features.RestoreCommitted(initialTerrain);
                world.Terrain = new RectangleTerrain(terrain);
                float center = terrain.Layout.WorldBounds.center.x;
                world.Player.X = center - 1.5f;
                world.Player.Y = world.Terrain.Surface(world.Player.X);
                world.Player.TerrainGrounded = true;
                var golem = world.Fighters[1];
                golem.X = Mathf.Min(world.Terrain.MaxX - 0.6f, center + 1.5f);
                golem.Y = world.Terrain.Surface(golem.X); golem.TerrainGrounded = true;
                golem.Name = "GOLEM";
                golem.Move = CombatMove.None;
                System.Array.Clear(golem.Cooldowns, 0, golem.Cooldowns.Length);
                world.PlanEnemies();
            }
            else stage.SetStartingPositions(world);
            stage.ResetRig();
            selected = CombatMove.Punch;
            direction = 1;
            playing = false;
            activePlan = null;
            playbackTime = 0;
            RefreshPrediction();
            mapCamera?.FollowPlayer(stage.PlayerFollowTarget);
        }

        public void Select(CombatMove move)
        {
            if (playing || world == null || world.Finished || selected == move) return;
            selected = move;
            RefreshPrediction();
        }

        private void OnPreviewChanged(ChangeEvent<bool> change)
        {
            showPreview = change.newValue;
            if (!showPreview && previewPlan != null) HidePreview();
        }

        private void UpdatePreview(float deltaTime)
        {
            if (!showPreview || playing || plan == null)
            {
                if (previewPlan != null) HidePreview();
                return;
            }
            if (previewPlan != plan)
            {
                stage.PreparePreview(world, plan);
                previewPlan = plan;
                previewTime = 0f;
            }
            else previewTime += deltaTime;

            var future = stage.DrawPreview(world, previewPlan, previewTime);
            if (terrain == null || future.Terrain == null) return;
            int revision = future.Terrain.State.Revision == world.Terrain.State.Revision ? -1 : future.Terrain.State.Revision;
            if (revision == previewTerrainRevision) return;
            if (revision < 0) terrain.Features.ClearPreview();
            else terrain.Features.Visuals.RenderPreview(future.Terrain.State);
            previewTerrainRevision = revision;
        }

        private void HidePreview()
        {
            stage?.HidePreview();
            if (previewTerrainRevision >= 0 && terrain != null) terrain.Features?.ClearPreview();
            previewTerrainRevision = -1;
            previewPlan = null;
        }

        private void RefreshPrediction()
        {
            plan = RectanglePlan.Create(world, selected, direction, strength);
            Render();
        }

        public void Commit()
        {
            if (playing || world == null || plan == null) return;
            activePlan = plan;
            playbackOrigin = world;
            playing = true;
            paused = false;
            playbackIndex = 0;
            playbackTime = 0;
            Render();
        }

        private void Update()
        {
            if (world == null) return;
            var keyboard = Keyboard.current;
            var focused = root?.focusController?.focusedElement as VisualElement;
            if (keyboard != null && !(focused is TextField) && focused?.GetFirstAncestorOfType<TextField>() == null)
            {
                if (keyboard.rKey.wasPressedThisFrame) { Restart(); return; }
                if (keyboard.enterKey.wasPressedThisFrame && !(focused is TextElement)) Commit();
            }
            if (!playing)
            {
                var mouse = Mouse.current;
                if (mouse != null && mouse.leftButton.wasPressedThisFrame && world.Player.IsReady && !world.Finished)
                {
                    Vector2 screen = mouse.position.ReadValue();
                    if (stage.ArenaCamera.pixelRect.Contains(screen) && !IsOverHud(screen))
                    {
                        Vector3 point = stage.ArenaCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 10));
                        Aim(point.x < world.Player.X ? -1 : 1);
                    }
                }
                UpdatePreview(Time.unscaledDeltaTime);
                return;
            }
            if (paused) { Render(); return; }
            stage.AdvanceRig(Time.unscaledDeltaTime);
            playbackTime += Time.unscaledDeltaTime;
            while (playing && playbackTime >= 1d / RectangleStage.PlaybackRate)
            {
                playbackTime -= 1d / RectangleStage.PlaybackRate;
                var next = activePlan.Frames[playbackIndex++];
                bool impact = next.Player.Health < world.Player.Health || next.Fighters[1].Health < world.Fighters[1].Health;
                world = next.Copy();
                if (playbackIndex == activePlan.Frames.Count)
                {
                    playbackTime = 0;
                    playing = false;
                    RefreshPrediction();
                    break;
                }
                else Render();
                if (impact) { playbackTime = -0.065; break; }
            }
            if (playing) Render();
        }

        private void LateUpdate()
        {
            if (mapCamera == null || world == null || root?.panel == null) return;
            Rect panel = root.worldBound;
            if (panel.height <= 0) return;
            float bottom = (panel.yMax - footer.worldBound.yMin + 6f) / panel.height;
            float top = (panel.yMax - header.worldBound.yMax - 6f) / panel.height;
            if (float.IsNaN(bottom) || float.IsNaN(top)) return;
            mapCamera.SetCombatFraming(new Vector2(Mathf.Clamp01(bottom), Mathf.Clamp01(top)),
                new Vector2(world.Player.Y, world.Fighters[1].Y));
        }

        private WelpTimeToDie.Levels.TerrainFeatureState renderedTerrain;
        private void Render()
        {
            var enemy = world.Fighters[1];
            var enemyMove = RectangleCombat.Spec(enemy, enemy.Move);
            int untilStrike = enemyMove.Startup - enemy.Elapsed;
            string intent = !enemy.IsAlive ? "Defeated" : enemy.Stun > 0 ? $"Stunned · {enemy.Stun}f"
                : enemy.Move == CombatMove.None ? "Ready"
                : untilStrike > 0 ? $"{enemyMove.Name} · hits in {untilStrike}f"
                : enemyMove.Damage > 0 && enemy.Elapsed >= enemyMove.Startup + enemyMove.Active ? $"Recovering · {enemy.Remaining}f"
                : $"{enemyMove.Name} · {enemy.Remaining}f left";
            status.text = intent;
            status.tooltip = world.LastEvent;
            playerHealth.text = $"{world.Player.Health} / {world.Player.MaxHealth} HP";
            enemyHealth.text = $"{enemy.Health} / {enemy.MaxHealth} HP";
            playerHealthFill.style.width = Length.Percent(100f * world.Player.Health / world.Player.MaxHealth);
            enemyHealthFill.style.width = Length.Percent(100f * enemy.Health / enemy.MaxHealth);
            RenderResult();
            bool choosing = !playing && !world.Finished && world.Player.IsReady;
            foreach (var pair in moves)
            {
                pair.Value.SetEnabled(choosing && world.CanStart(0, pair.Key));
                pair.Value.EnableInClassList("selected", selected == pair.Key);
            }
            goButton.text = !world.Player.IsReady && !world.Finished ? "Continue  ›" : "Commit move  ›";
            goButton.SetEnabled(!playing && plan != null);
            string moveName = plan?.MoveName ?? RectangleCombat.Spec(world.Player, selected).Name;
            string aim = direction < 0 ? "left" : "right";
            string choice = selected == CombatMove.Backstep
                ? $"Backstep {(direction < 0 ? "right" : "left")} · face {aim}"
                : $"{moveName} · aim {aim}";
            help.text = world.Finished ? "Reset to try again."
                : playing ? "Resolving your move..."
                : plan == null ? $"{moveName} is unavailable. Check its cooldown or ground requirement."
                : $"{(choosing ? choice : moveName)}. {plan.Summary}";
            aimLeft.SetEnabled(choosing);
            aimRight.SetEnabled(choosing);
            aimLeft.EnableInClassList("selected", direction < 0);
            aimRight.EnableInClassList("selected", direction > 0);
            RectangleCombat nextVisual = playing && playbackIndex < activePlan.Frames.Count
                ? activePlan.Frames[playbackIndex] : null;
            root.Q<Label>("frameCount").text = $"Frame {world.Frame} · {RectangleCombat.FramesPerSecond} Hz combat";
            root.Q<Button>("generateMap").SetEnabled(!playing);
            root.Q<Button>("randomizeMap").SetEnabled(!playing);
            root.Q<Slider>("dashPower").SetEnabled(!playing);
            if (terrain != null && world.Terrain != null && renderedTerrain != world.Terrain.State)
            {
                terrain.Features.RestoreCommitted(world.Terrain.State);
                renderedTerrain = world.Terrain.State;
                previewTerrainRevision = -1;
            }
            stage.Draw(world, !playing && world.Player.IsReady ? direction : 0, playing ? playbackOrigin : null,
                nextVisual, Mathf.Clamp01((float)playbackTime * RectangleStage.PlaybackRate));
            if ((playing || plan == null) && previewPlan != null) HidePreview();
        }

        private void RenderResult()
        {
            bool finished = world.Finished && !playing;
            if (finished == !resultOverlay.ClassListContains("hidden")) return;

            resultOverlay.EnableInClassList("hidden", !finished);
            header.SetEnabled(!finished);
            footer.SetEnabled(!finished);
            if (!finished)
            {
                rematchButton.Blur();
                return;
            }

            resultOverlay.EnableInClassList("result-loss", world.Lost);
            resultTitle.text = world.Lost ? "You Lose" : "You Win";
            resultDetails.text = world.Lost
                ? world.Won ? "Both fighters fell." : "The Golem won this round."
                : $"The Golem fell. You finished with {world.Player.Health} HP.";
            rematchButton.Focus();
        }

        private void OnDisable()
        {
            playing = false;
            if (rematchButton != null) rematchButton.clicked -= Restart;
            previewToggle?.UnregisterValueChangedCallback(OnPreviewChanged);
            moves.Clear();
            if (terrain != null) terrain.Generated -= Restart;
            HidePreview();
        }
    }
}
