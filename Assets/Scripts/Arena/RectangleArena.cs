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
        private Button goButton;
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
            foreach (CombatMove move in new[] { CombatMove.Move, CombatMove.Punch, CombatMove.Uppercut,
                CombatMove.Kick, CombatMove.DashKick, CombatMove.Block, CombatMove.Wait, CombatMove.Dash, CombatMove.Jump })
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
            root.Q<Button>("aimLeft").clicked += () => Aim(-1);
            root.Q<Button>("aimRight").clicked += () => Aim(1);
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
            if (playing || world == null || world.Finished) return;
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
            if (keyboard != null && !(root?.focusController?.focusedElement is TextField) && !(root?.focusController?.focusedElement is TextElement))
            {
                if (keyboard.rKey.wasPressedThisFrame) { Restart(); return; }
                if (keyboard.enterKey.wasPressedThisFrame) Commit();
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

        private WelpTimeToDie.Levels.TerrainFeatureState renderedTerrain;
        private void Render()
        {
            var enemy = world.Fighters[1];
            string phase = world.Lost ? "You died - Reset to retry" : world.Won ? "You win!" : playing ? "Playing..."
                : !world.Player.IsReady ? "Move still running - Continue" : "Paused";
            string intent = !enemy.IsAlive ? "Defeated" : enemy.Stun > 0 ? $"Stunned {enemy.Stun}f" : enemy.Move == CombatMove.None ? "Ready"
                : enemy.Elapsed < RectangleCombat.Spec(enemy, enemy.Move).Startup ? $"{RectangleCombat.Spec(enemy, enemy.Move).Name} in {RectangleCombat.Spec(enemy, enemy.Move).Startup - enemy.Elapsed}f"
                : $"{RectangleCombat.Spec(enemy, enemy.Move).Name} ({enemy.Remaining}f left)";
            status.text = $"{phase} | You {world.Player.Health} HP | Enemy {enemy.Health} HP - {intent}";
            var enemyMove = RectangleCombat.Spec(enemy, enemy.Move);
            status.tooltip = enemy.IsAlive && enemy.Move != CombatMove.None
                ? $"Enemy: {enemyMove.Name}. Hits in {Mathf.Max(0, enemyMove.Startup - enemy.Elapsed)} frames.\n{world.LastEvent}"
                : world.LastEvent;
            foreach (var pair in moves)
            {
                bool available = !playing && world.CanStart(0, pair.Key);
                pair.Value.SetEnabled(!playing && !world.Finished);
                pair.Value.tooltip = available ? RectangleCombat.Spec(world.Player, pair.Key).Name : "Recharging or requires ground contact";
                pair.Value.EnableInClassList("selected", selected == pair.Key);
            }
            goButton.text = !world.Player.IsReady && !world.Finished ? "Continue >" : "Go  >";
            goButton.SetEnabled(!playing && plan != null);
            string moveName = plan?.MoveName ?? RectangleCombat.Spec(world.Player, selected).Name;
            help.text = world.Finished ? "Reset to try again."
                : playing ? "Resolving your move..."
                : plan == null ? $"{moveName} is recharging. Choose another move."
                : $"{moveName} {(direction < 0 ? "left" : "right")}. {plan.Summary}";
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

        private void OnDisable()
        {
            playing = false;
            previewToggle?.UnregisterValueChangedCallback(OnPreviewChanged);
            moves.Clear();
            if (terrain != null) terrain.Generated -= Restart;
            HidePreview();
        }
    }
}
