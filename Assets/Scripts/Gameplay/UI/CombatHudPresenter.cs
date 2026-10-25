using UnityEngine;
using UnityEngine.UIElements;
using WelpTimeToDie.Playback;

namespace WelpTimeToDie.UI
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class CombatHudPresenter : MonoBehaviour
    {
        [SerializeField] private CombatPlaybackController playback;
        private UIDocument document;
        private Label frameLabel;
        private Label strengthLabel;
        private Label statusLabel;

        private void OnEnable() => document = GetComponent<UIDocument>();

        private void LateUpdate()
        {
            if (playback == null || document.rootVisualElement == null) return;
            if (frameLabel == null || frameLabel.panel == null)
            {
                frameLabel = document.rootVisualElement.Q<Label>("frameCount");
                strengthLabel = document.rootVisualElement.Q<Label>("dashStrength");
                statusLabel = document.rootVisualElement.Q<Label>("playbackStatus");
            }
            if (frameLabel != null) frameLabel.text = $"Frames elapsed: {playback.CurrentFrame}";
            if (strengthLabel != null) strengthLabel.text = $"Dash strength: {playback.DashStrength:0.0}  [Up / Down]";
            if (statusLabel != null) statusLabel.text = playback.IsPaused
                ? "PAUSED  |  Space to resume"
                : $"{CombatPlaybackController.PlaybackFramesPerSecond} frames/s  |  Space to pause";
        }

        private void OnDisable() => frameLabel = strengthLabel = statusLabel = null;
    }
}
