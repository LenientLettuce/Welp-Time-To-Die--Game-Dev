using UnityEngine;
using WelpTimeToDie.Levels;

namespace WelpTimeToDie.Cameras
{
    /// <summary>Owns follow/browse state, zoom, and view bounds. Input and UI are separate.</summary>
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public sealed class MapCameraController : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private LevelLayout level;
        [SerializeField] private Vector2 followOffset = new Vector2(0f, 2f);
        [SerializeField, Min(0.1f)] private float defaultZoom = 6.75f;
        [SerializeField, Min(0.1f)] private float minimumZoom = 2f;
        [SerializeField, Min(0.1f)] private float maximumZoom = 24f;
        private Camera mapCamera;

        public bool IsBrowsing { get; private set; }
        public float Zoom => ViewCamera.orthographicSize;
        private Camera ViewCamera => mapCamera != null ? mapCamera : (mapCamera = GetComponent<Camera>());

        private void Awake()
        {
            ViewCamera.orthographic = true;
            ViewCamera.orthographicSize = defaultZoom;
        }

        private void LateUpdate()
        {
            if (!IsBrowsing && player != null)
                SetPosition((Vector2)player.position + followOffset);
            else SetPosition(transform.position);
        }

        public void RecenterOnPlayer()
        {
            IsBrowsing = false;
            if (player != null) SetPosition((Vector2)player.position + followOffset);
        }

        public void Pan(Vector2 worldDelta)
        {
            if (worldDelta.sqrMagnitude <= 0f) return;
            IsBrowsing = true;
            SetPosition((Vector2)transform.position + worldDelta);
        }

        public void ZoomBy(float scrollSteps, Vector2 screenPosition)
        {
            if (scrollSteps == 0f) return;
            Vector3 before = ViewCamera.ScreenToWorldPoint(screenPosition);
            ViewCamera.orthographicSize = Mathf.Clamp(Zoom * Mathf.Exp(-scrollSteps * 0.12f),
                minimumZoom, Mathf.Max(minimumZoom, maximumZoom));
            if (IsBrowsing)
                SetPosition(transform.position + before - ViewCamera.ScreenToWorldPoint(screenPosition));
        }

        private void SetPosition(Vector2 position)
        {
            if (level != null)
            {
                Bounds bounds = level.WorldBounds;
                position.x = ClampAxis(position.x, bounds.min.x, bounds.max.x, Zoom * ViewCamera.aspect);
                position.y = ClampAxis(position.y, bounds.min.y, bounds.max.y, Zoom);
            }
            transform.position = new Vector3(position.x, position.y, transform.position.z);
        }

        public static float ClampAxis(float center, float minimum, float maximum, float halfView) =>
            halfView * 2f >= maximum - minimum ? (minimum + maximum) / 2f : Mathf.Clamp(center, minimum + halfView, maximum - halfView);
    }
}
