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
        private Vector2 followViewport = new Vector2(0, 1);
        private Vector2? fighterHeights;

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
                UpdateFollowPosition();
        }

        public void SetCombatFraming(Vector2 viewport, Vector2 feet)
        {
            followViewport = viewport;
            fighterHeights = feet;
        }

        private void UpdateFollowPosition()
        {
            var position = (Vector2)player.position + followOffset;
            if (fighterHeights is Vector2 feet)
            {
                const float fighterClearance = 2.35f;
                float viewHeight = Zoom * 2f;
                float bottom = (followViewport.x - 0.5f) * viewHeight;
                float top = (followViewport.y - 0.5f) * viewHeight;
                float minCenter = Mathf.Max(feet.x, feet.y) + fighterClearance - top;
                float maxCenter = Mathf.Min(feet.x, feet.y) - bottom;
                if (minCenter > maxCenter)
                {
                    // At close zoom, frame the player if both fighters don't fit.
                    position.y = (minCenter + maxCenter) * 0.5f;
                    minCenter = feet.x + fighterClearance - top;
                    maxCenter = feet.x - bottom;
                }
                position.y = minCenter <= maxCenter ? Mathf.Clamp(position.y, minCenter, maxCenter)
                    : (minCenter + maxCenter) * 0.5f;
            }
            SetPosition(position);
        }

        public void FollowPlayer(Transform target)
        {
            player = target;
            RecenterOnPlayer();
        }

        public void RecenterOnPlayer()
        {
            IsBrowsing = false;
            if (player != null) UpdateFollowPosition();
        }

        public void BeginBrowse() => IsBrowsing = true;

        public void Pan(Vector2 worldDelta)
        {
            if (worldDelta.sqrMagnitude <= 0f) return;
            BeginBrowse();
            SetBrowsePosition((Vector2)transform.position + worldDelta);
        }

        public void ZoomBy(float scrollSteps, Vector2 screenPosition)
        {
            if (scrollSteps == 0f) return;
            Vector3 before = ViewCamera.ScreenToWorldPoint(screenPosition);
            ViewCamera.orthographicSize = Mathf.Clamp(Zoom * Mathf.Exp(-scrollSteps * 0.12f),
                minimumZoom, Mathf.Max(minimumZoom, maximumZoom));
            if (IsBrowsing)
                SetBrowsePosition(transform.position + before - ViewCamera.ScreenToWorldPoint(screenPosition));
        }

        private void SetBrowsePosition(Vector2 position)
        {
            if (level != null)
            {
                Bounds bounds = level.WorldBounds;
                position.x = ClampAxis(position.x, bounds.min.x, bounds.max.x, Zoom * ViewCamera.aspect);
                position.y = ClampAxis(position.y, bounds.min.y, bounds.max.y, Zoom);
            }
            SetPosition(position);
        }

        private void SetPosition(Vector2 position) =>
            transform.position = new Vector3(position.x, position.y, transform.position.z);

        public static float ClampAxis(float center, float minimum, float maximum, float halfView) =>
            halfView * 2f >= maximum - minimum ? (minimum + maximum) / 2f : Mathf.Clamp(center, minimum + halfView, maximum - halfView);
    }
}
