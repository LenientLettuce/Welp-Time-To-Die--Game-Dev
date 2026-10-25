using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using WelpTimeToDie.Player;
using WelpTimeToDie.Playback;

namespace WelpTimeToDie.Input
{
    /// <summary>World clicks choose aim; action buttons own all movement commands.</summary>
    public sealed class PlayerClickInput : MonoBehaviour
    {
        [SerializeField] private PlayerActionSelection selection;
        [SerializeField] private PlayerActionController player;
        [SerializeField] private CombatPlaybackController playback;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private UIDocument hud;

        private void Update()
        {
            if (player == null || playback == null) return;
            var mouse = Mouse.current;
            if (!player.IsBusy && mouse != null && mouse.leftButton.wasPressedThisFrame && !IsOverHud(mouse.position.ReadValue()))
            {
                Vector2 point = worldCamera.ScreenToWorldPoint(mouse.position.ReadValue());
                selection.SetDirection(point - (Vector2)player.transform.position);
            }
            playback.AdvancePlayback(Time.unscaledDeltaTime);
        }

        private bool IsOverHud(Vector2 screen)
        {
            var root = hud != null ? hud.rootVisualElement : null;
            if (root?.panel == null) return false;
            var point = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screen.x, Screen.height - screen.y));
            var picked = root.panel.Pick(point);
            return picked != null && picked != root;
        }
    }
}
