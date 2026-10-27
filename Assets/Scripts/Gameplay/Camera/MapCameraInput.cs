using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace WelpTimeToDie.Cameras
{
    [RequireComponent(typeof(MapCameraController), typeof(Camera))]
    public sealed class MapCameraInput : MonoBehaviour
    {
        [SerializeField] private UIDocument hud;
        private MapCameraController controller;
        private Camera mapCamera;
        private Vector2 lastPointerPosition;
        private Vector2 dragOrigin;
        private bool dragging;
        private bool dragStartedOverHud;

        private void Awake()
        {
            controller = GetComponent<MapCameraController>();
            mapCamera = GetComponent<Camera>();
        }

        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;
            Vector2 pointer = mouse.position.ReadValue();
            if (mouse.rightButton.wasPressedThisFrame)
            {
                dragOrigin = lastPointerPosition = pointer;
                dragging = false;
                dragStartedOverHud = IsOverHud(pointer);
            }
            if (mouse.rightButton.isPressed && !dragStartedOverHud)
            {
                if ((pointer - dragOrigin).sqrMagnitude > 9f) dragging = true;
                if (dragging)
                {
                    Vector2 delta = mapCamera.ScreenToWorldPoint(lastPointerPosition) - mapCamera.ScreenToWorldPoint(pointer);
                    controller.Pan(delta);
                }
                lastPointerPosition = pointer;
            }
            if (mouse.rightButton.wasReleasedThisFrame) dragging = false;
            float scroll = mouse.scroll.ReadValue().y;
            if (scroll != 0f && !IsOverHud(pointer))
                controller.ZoomBy(scroll / 120f, pointer);
        }

        private bool IsOverHud(Vector2 pointer)
        {
            var root = hud != null ? hud.rootVisualElement : null;
            if (root?.panel == null) return false;
            var point = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(pointer.x, Screen.height - pointer.y));
            var picked = root.panel.Pick(point);
            return picked != null && picked != root;
        }

        private void OnApplicationFocus(bool focused)
        {
            dragging = false;
            dragStartedOverHud = true;
        }
    }
}
