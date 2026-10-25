using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace WelpTimeToDie.RectanglePrototype
{
    // Small overlay only. The arena and fighters are actual saved scene objects.
    public sealed class RectangleArena : MonoBehaviour
    {
        [SerializeField] private RectangleStage stage;
        private readonly Dictionary<CombatMove, Button> moves = new Dictionary<CombatMove, Button>();
        private RectangleCombat world, rehearsalFrame, playbackOrigin;
        private RectanglePlan plan, activePlan;
        private CombatMove selected = CombatMove.Punch;
        private float direction = 1;
        private int playbackIndex;
        private double playbackTime;
        private float previewHold = -1;
        private bool playing, watching;
        private UIDocument document;
        private PanelSettings panel;
        private ThemeStyleSheet theme;
        private Font font;
        private Label status, help;
        private Button goButton, previewButton;

        private void OnEnable()
        {
            if (stage == null)
            {
                Debug.LogError("Open the updated RectangleArena scene: its saved RectangleStage reference is required.", this);
                return;
            }
            panel = ScriptableObject.CreateInstance<PanelSettings>();
            theme = ScriptableObject.CreateInstance<ThemeStyleSheet>();
            panel.themeStyleSheet = theme;
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(960, 540);
            font = Font.CreateDynamicFontFromOSFont("Arial", 16);
            document = gameObject.AddComponent<UIDocument>();
            document.panelSettings = panel;
            var root = document.rootVisualElement;
            root.style.flexGrow = 1;
            root.style.color = Color.white;
            root.style.unityFont = font;
            // Empty screen space passes clicks through to the world.
            root.pickingMode = PickingMode.Ignore;

            var header = new VisualElement();
            header.style.position = Position.Absolute;
            header.style.left = header.style.right = header.style.top = 0;
            header.style.height = 40;
            header.style.paddingLeft = header.style.paddingRight = 12;
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.backgroundColor = new Color(0.055f, 0.07f, 0.10f);
            root.Add(header);
            status = new Label();
            status.style.flexGrow = 1;
            status.style.fontSize = 15;
            status.style.unityTextAlign = TextAnchor.MiddleLeft;
            status.style.overflow = Overflow.Hidden;
            status.pickingMode = PickingMode.Ignore;
            header.Add(status);
            var reset = AddButton(header, "Reset", Restart);
            reset.style.flexGrow = 0;
            reset.style.width = 72;

            var footer = new VisualElement();
            footer.style.position = Position.Absolute;
            footer.style.left = footer.style.right = footer.style.bottom = 0;
            footer.style.paddingLeft = footer.style.paddingRight = 10;
            footer.style.paddingTop = footer.style.paddingBottom = 8;
            footer.style.backgroundColor = new Color(0.055f, 0.07f, 0.10f);
            root.Add(footer);
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            footer.Add(row);
            AddMove(row, CombatMove.Move, "Move", "Move in the direction you click, over 8 combat frames.");
            AddMove(row, CombatMove.Punch, "Punch", "Punch toward your click. Hits after 4 frames; finishes after 9.");
            AddMove(row, CombatMove.Uppercut, "Uppercut", "6-frame startup. Launches into 12 frames of hitstun; punishable on a miss.");
            AddMove(row, CombatMove.Kick, "Kick", "7-frame startup. Longer reach and strong pushback; active for 3 frames.");
            AddMove(row, CombatMove.DashKick, "Dash kick", "Rush forward for 8 frames, then strike. Long recovery; commit carefully.");
            AddMove(row, CombatMove.Block, "Block", "Face your click and reduce incoming frontal damage.");
            AddMove(row, CombatMove.Wait, "Wait", "Let 4 combat frames pass without moving.");
            var actions = new VisualElement();
            actions.style.flexDirection = FlexDirection.Row;
            footer.Add(actions);
            previewButton = AddButton(actions, "Watch preview", WatchPreview);
            previewButton.tooltip = "Rehearse exactly the next Go action, then return to the unchanged live position.";
            goButton = AddButton(actions, "Go  >", Commit);
            goButton.style.backgroundColor = new Color(0.12f, 0.40f, 0.46f);
            help = new Label();
            help.style.fontSize = 12;
            help.style.marginTop = 5;
            help.style.whiteSpace = WhiteSpace.Normal;
            help.pickingMode = PickingMode.Ignore;
            footer.Add(help);

            // Reserve space for the two HUD strips without shrinking the world into a UI box.
            stage.ArenaCamera.rect = new Rect(0, 0.27f, 1, 0.65f);
            Restart();
        }

        private static Button AddButton(VisualElement parent, string text, Action action)
        {
            var button = new Button(action) { text = text };
            button.style.height = 32;
            button.style.minWidth = 72;
            button.style.flexGrow = 1;
            button.style.marginRight = 5;
            button.style.marginBottom = 3;
            button.style.paddingLeft = button.style.paddingRight = 9;
            button.style.backgroundColor = new Color(0.14f, 0.18f, 0.24f);
            button.style.color = Color.white;
            button.style.fontSize = 14;
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            button.style.borderTopWidth = button.style.borderBottomWidth = button.style.borderLeftWidth = button.style.borderRightWidth = 1;
            button.style.borderTopColor = button.style.borderBottomColor = button.style.borderLeftColor = button.style.borderRightColor = new Color(0.28f, 0.35f, 0.44f);
            parent.Add(button);
            return button;
        }

        private void AddMove(VisualElement parent, CombatMove move, string name, string hint)
        {
            var button = AddButton(parent, name, () => Select(move));
            button.tooltip = hint;
            moves.Add(move, button);
        }

        private void Restart()
        {
            world = RectangleCombat.CreateBasic(FighterClass.Barbarian);
            stage.SetStartingPositions(world);
            selected = CombatMove.Punch;
            direction = 1;
            playing = false;
            watching = false;
            rehearsalFrame = null;
            activePlan = null;
            previewHold = -1;
            playbackTime = 0;
            RefreshPrediction();
        }

        private void Select(CombatMove move)
        {
            if (playing || watching || !world.CanStart(0, move)) return;
            selected = move;
            RefreshPrediction();
        }

        private void WatchPreview()
        {
            if (watching)
            {
                watching = false;
                rehearsalFrame = null;
                Render();
                return;
            }
            if (playing || plan == null) return;
            activePlan = plan;
            playbackOrigin = world;
            watching = true;
            playbackIndex = 0;
            playbackTime = 0;
            previewHold = -1;
            Render();
        }

        private void RefreshPrediction()
        {
            // Keep the player's selection. Never silently substitute Wait for an attack
            // on cooldown: explain that another move must be chosen instead.
            plan = RectanglePlan.Create(world, selected, direction);
            Render();
        }

        private void Commit()
        {
            if (playing || watching || world == null || plan == null) return;
            activePlan = plan;
            playbackOrigin = world;
            playing = true;
            previewHold = -1;
            playbackIndex = 0;
            playbackTime = 0;
            Render();
        }

        private void Update()
        {
            if (world == null) return;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.rKey.wasPressedThisFrame) { Restart(); return; }
                if (keyboard.enterKey.wasPressedThisFrame) Commit();
            }
            if (watching && previewHold >= 0)
            {
                previewHold -= Time.unscaledDeltaTime;
                if (previewHold <= 0)
                {
                    watching = false;
                    rehearsalFrame = null;
                    Render();
                }
                return;
            }
            if (!playing && !watching)
            {
                var mouse = Mouse.current;
                if (mouse != null && mouse.leftButton.wasPressedThisFrame && world.Player.IsReady && !world.Finished)
                {
                    Vector2 screen = mouse.position.ReadValue();
                    // Ignore HUD clicks. Only aim when clicking inside the camera's arena viewport.
                    if (stage.ArenaCamera.pixelRect.Contains(screen))
                    {
                        Vector3 point = stage.ArenaCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 10));
                        direction = point.x < world.Player.X ? -1 : 1;
                        RefreshPrediction();
                    }
                }
                return;
            }
            playbackTime += Time.unscaledDeltaTime;
            while ((playing || watching) && playbackTime >= 1d / 12)
            {
                playbackTime -= 1d / 12;
                var previous = watching && rehearsalFrame != null ? rehearsalFrame : world;
                var next = activePlan.Frames[playbackIndex++];
                bool impact = next.Player.Health < previous.Player.Health || next.Fighters[1].Health < previous.Fighters[1].Health;
                if (watching) rehearsalFrame = next;
                else world = next.Copy();
                if (playbackIndex == activePlan.Frames.Count)
                {
                    playbackTime = 0;
                    if (watching)
                    {
                        previewHold = 1.1f;
                        Render();
                    }
                    else
                    {
                        playing = false;
                        RefreshPrediction();
                    }
                    break;
                }
                else Render();
                if (impact) { playbackTime = -0.065; break; }
            }
            if (playing || watching) Render();
        }

        private void Render()
        {
            var shown = watching && rehearsalFrame != null ? rehearsalFrame : world;
            var enemy = shown.Fighters[1];
            string phase = watching ? "PREVIEW - nothing committed" : world.Lost ? "You died - Reset to retry" : world.Won ? "You win!" : playing ? "Playing..."
                : !world.Player.IsReady ? "Move still running - Continue" : "Paused";
            string intent = !enemy.IsAlive ? "Defeated" : enemy.Stun > 0 ? $"Stunned {enemy.Stun}f" : enemy.Move == CombatMove.None ? "Ready"
                : enemy.Elapsed < RectangleCombat.Spec(enemy, enemy.Move).Startup ? $"{RectangleCombat.Spec(enemy, enemy.Move).Name} in {RectangleCombat.Spec(enemy, enemy.Move).Startup - enemy.Elapsed}f"
                : $"{RectangleCombat.Spec(enemy, enemy.Move).Name} ({enemy.Remaining}f left)";
            status.text = $"{phase} | You {shown.Player.Health} HP | Enemy {enemy.Health} HP - {intent}";
            // Enemy intent is available on hover instead of taking another row of screen space.
            var enemyMove = RectangleCombat.Spec(enemy, enemy.Move);
            status.tooltip = enemy.IsAlive && enemy.Move != CombatMove.None
                ? $"Enemy: {enemyMove.Name}. Hits in {Mathf.Max(0, enemyMove.Startup - enemy.Elapsed)} frames.\n{world.LastEvent}"
                : world.LastEvent;
            foreach (var pair in moves)
            {
                bool available = !playing && !watching && world.CanStart(0, pair.Key);
                pair.Value.SetEnabled(available);
                pair.Value.style.opacity = available ? 1 : 0.4f;
                pair.Value.style.backgroundColor = selected == pair.Key ? new Color(0.12f, 0.36f, 0.48f) : new Color(0.14f, 0.18f, 0.24f);
            }
            goButton.text = !world.Player.IsReady && !world.Finished ? "Continue >" : "Go  >";
            goButton.SetEnabled(!playing && !watching && plan != null);
            previewButton.text = watching ? "Stop preview" : "Watch preview";
            previewButton.SetEnabled(!playing && !world.Finished);
            if (!watching) previewButton.SetEnabled(!playing && plan != null);
            string moveName = RectangleCombat.Spec(world.Player, selected).Name;
            help.text = watching ? (previewHold >= 0 ? activePlan.Summary : $"Previewing {activePlan.MoveName}... Your real position and health are safe.")
                : world.Finished ? "Reset to try again."
                : playing ? "Resolving your move..."
                : plan == null ? $"{moveName} is recharging. Choose another move."
                : $"{moveName} {(direction < 0 ? "left" : "right")}. {plan.Summary}";
            RectangleCombat nextVisual = (playing || watching) && previewHold < 0 && playbackIndex < activePlan.Frames.Count
                ? activePlan.Frames[playbackIndex] : null;
            stage.Draw(shown, !playing && !watching && world.Player.IsReady ? direction : 0, watching,
                watching || playing ? playbackOrigin : null, nextVisual, Mathf.Clamp01((float)playbackTime * 12));
        }

        private void OnDisable()
        {
            playing = false;
            watching = false;
            moves.Clear();
            if (document != null) { document.enabled = false; Destroy(document); }
            if (panel != null) Destroy(panel);
            if (theme != null) Destroy(theme);
            if (font != null) Destroy(font);
        }
    }
}
