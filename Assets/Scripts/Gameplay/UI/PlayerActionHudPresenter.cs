using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using WelpTimeToDie.Player;
using WelpTimeToDie.Playback;

namespace WelpTimeToDie.UI
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class PlayerActionHudPresenter : MonoBehaviour
    {
        [SerializeField] private PlayerActionController player;
        [SerializeField] private PlayerActionSelection selection;
        [SerializeField] private CombatPlaybackController playback;
        private UIDocument document;
        private readonly List<Action> unsubscribe = new List<Action>();
        private readonly Dictionary<PlayerMove, Button> moves = new Dictionary<PlayerMove, Button>();
        private Label stateLabel, aimLabel, strengthLabel, detailsLabel;
        private Button commitButton, pauseButton, stepButton;
        private Slider strengthSlider;

        private void OnEnable() => document = GetComponent<UIDocument>();
        private void LateUpdate()
        {
            if (player == null || document.rootVisualElement == null) return;
            if (stateLabel == null || stateLabel.panel == null) BindControls();
            if (stateLabel == null) return;
            var state = player.State;
            stateLabel.text = state.IsBusy ? $"{state.Move} · frame {state.ElapsedFrames}/{PlayerMoveCatalog.Get(state.Move).Duration}"
                : player.Motor.IsGrounded ? "READY · choose your next move" : "AIRBORNE · choose next move to advance time";
            aimLabel.text = $"Aim: {Mathf.Atan2(selection.Direction.y, selection.Direction.x) * Mathf.Rad2Deg:0}° · click the map or a direction below";
            strengthLabel.text = $"Strength: {selection.Strength * 100f:0}%";
            strengthSlider.SetValueWithoutNotify(selection.Strength);
            strengthSlider.SetEnabled(!state.IsBusy);
            foreach (var pair in moves)
            {
                int cooldown = state.Cooldown(pair.Key);
                pair.Value.text = pair.Key + (cooldown > 0 ? $" [{cooldown}f]" : "");
                pair.Value.EnableInClassList("selected-move", selection.Move == pair.Key);
                pair.Value.SetEnabled(!state.IsBusy);
            }
            bool allowed = state.CanStart(selection.Move, player.Motor.IsGrounded);
            commitButton.SetEnabled(allowed);
            pauseButton.SetEnabled(state.IsBusy);
            pauseButton.text = playback.IsPaused ? "Play" : "Pause";
            var definition = PlayerMoveCatalog.Get(selection.Move);
            string unavailable = state.IsBusy ? "Move in progress. Pause or step to inspect it."
                : state.Cooldown(selection.Move) > 0 ? $"Recharging: {state.Cooldown(selection.Move)}f. Wait or advance frames."
                : selection.Move == PlayerMove.Jump && !player.Motor.IsGrounded ? "Jump requires ground contact. Choose another action while airborne."
                : "Preview shows the chosen move. Commit plays it; Next frame advances just one frame.";
            detailsLabel.text = $"{selection.Move}: {definition.Duration}f" + (definition.Startup > 0 ? $" · strike on frame {definition.Startup}" : "") + $" · cooldown {definition.Recharge}f\n{unavailable}";
        }

        private void BindControls()
        {
            UnbindControls();
            var root = document.rootVisualElement;
            stateLabel = root.Q<Label>("playerActionState");
            aimLabel = root.Q<Label>("aimDirection");
            strengthLabel = root.Q<Label>("actionStrengthLabel");
            detailsLabel = root.Q<Label>("actionDetails");
            foreach (PlayerMove move in new[] { PlayerMove.Move, PlayerMove.Dash, PlayerMove.Jump, PlayerMove.Attack, PlayerMove.Block, PlayerMove.Wait })
            {
                var button = root.Q<Button>("action" + move);
                if (button == null) continue;
                moves.Add(move, button);
                Listen(button, () => selection.SelectMove(move));
            }
            Listen(root.Q<Button>("aimLeft"), () => selection.SetDirection(Vector2.left));
            Listen(root.Q<Button>("aimRight"), () => selection.SetDirection(Vector2.right));
            Listen(root.Q<Button>("aimUpLeft"), () => selection.SetDirection(new Vector2(-1, 1)));
            Listen(root.Q<Button>("aimUp"), () => selection.SetDirection(Vector2.up));
            Listen(root.Q<Button>("aimUpRight"), () => selection.SetDirection(Vector2.one));
            Listen(root.Q<Button>("aimDown"), () => selection.SetDirection(Vector2.down));
            Listen(root.Q<Button>("strengthLess"), () => selection.SetStrength(selection.Strength - 0.1f));
            Listen(root.Q<Button>("strengthMore"), () => selection.SetStrength(selection.Strength + 0.1f));
            strengthSlider = root.Q<Slider>("actionStrength");
            strengthSlider.RegisterValueChangedCallback(OnStrengthChanged);
            unsubscribe.Add(() => strengthSlider.UnregisterValueChangedCallback(OnStrengthChanged));
            commitButton = root.Q<Button>("commitAction");
            pauseButton = root.Q<Button>("pauseAction");
            stepButton = root.Q<Button>("stepFrame");
            Listen(commitButton, Commit);
            Listen(pauseButton, () => playback.SetPaused(!playback.IsPaused));
            Listen(stepButton, Step);
            Listen(root.Q<Button>("resetPlayer"), () => { playback.ResetPlayer(); playback.SetPaused(false); });
        }

        private void Commit()
        {
            if (playback.TryAction(selection.Move, selection.Direction, selection.Strength)) playback.SetPaused(false);
        }
        private void Step()
        {
            if (!player.IsBusy && player.State.CanStart(selection.Move, player.Motor.IsGrounded))
                playback.TryAction(selection.Move, selection.Direction, selection.Strength);
            playback.StepFrame();
        }
        private void OnStrengthChanged(ChangeEvent<float> change) { if (!player.IsBusy) selection.SetStrength(change.newValue); }
        private void Listen(Button button, Action action)
        {
            if (button == null) return;
            button.clicked += action;
            unsubscribe.Add(() => button.clicked -= action);
        }
        private void UnbindControls()
        {
            foreach (var action in unsubscribe) action();
            unsubscribe.Clear();
            moves.Clear();
            stateLabel = null;
        }
        private void OnDisable() => UnbindControls();
    }
}
