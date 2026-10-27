using UnityEngine;
using WelpTimeToDie.Levels;

namespace WelpTimeToDie.Cameras
{
    [RequireComponent(typeof(Camera))]
    public sealed class LevelCameraFraming : MonoBehaviour
    {
        [SerializeField] private LevelLayout level;
        [SerializeField, Min(0f)] private float horizontalPadding = 2f;
        [SerializeField, Min(0.01f)] private float minimumOrthographicSize = 4f;
        private Camera levelCamera;

        private void Awake() => levelCamera = GetComponent<Camera>();

        private void LateUpdate()
        {
            if (level == null || !levelCamera.orthographic) return;
            Bounds bounds = level.WorldBounds;
            levelCamera.orthographicSize = Mathf.Max(minimumOrthographicSize, bounds.extents.y,
                (bounds.extents.x + horizontalPadding) / Mathf.Max(0.01f, levelCamera.aspect));
            transform.position = new Vector3(bounds.center.x, bounds.center.y, transform.position.z);
        }
    }
}
