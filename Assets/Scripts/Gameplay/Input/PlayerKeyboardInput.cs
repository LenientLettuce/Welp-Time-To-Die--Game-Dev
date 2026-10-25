using UnityEngine;
using UnityEngine.InputSystem;
using WelpTimeToDie.Playback;
using UnityEngine.UIElements;

namespace WelpTimeToDie.Input
{
    /// <summary>Translates keyboard commands and wall time into playback requests.</summary>
    public sealed class PlayerKeyboardInput : MonoBehaviour
    {
        [SerializeField] private CombatPlaybackController playback;
        [SerializeField] private UIDocument hud;

        private void Update()
        {
            if (playback == null || !playback.isActiveAndEnabled || !playback.IsReady) return;
            var keyboard = Keyboard.current;
            var focused = hud != null ? hud.rootVisualElement?.focusController?.focusedElement as VisualElement : null;
            bool enteringText = focused is TextField || focused?.GetFirstAncestorOfType<TextField>() != null;
            if (keyboard != null && !enteringText)
            {
                int strengthSteps = (keyboard.upArrowKey.wasPressedThisFrame ? 1 : 0)
                    - (keyboard.downArrowKey.wasPressedThisFrame ? 1 : 0);
                if (strengthSteps != 0) playback.AdjustStrength(strengthSteps);
                if (keyboard.spaceKey.wasPressedThisFrame)
                {
                    playback.SetPaused(!playback.IsPaused);
                    return;
                }
                if (keyboard.rKey.wasPressedThisFrame)
                {
                    playback.ResetPlayer();
                    return;
                }
                bool left = keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame;
                bool right = keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame;
                if (left != right && playback.TryDash(left ? -1 : 1)) return;
            }
            // The interval that accepts a new move is excluded from its playback time.
            playback.AdvancePlayback(Time.unscaledDeltaTime);
        }
    }
}
