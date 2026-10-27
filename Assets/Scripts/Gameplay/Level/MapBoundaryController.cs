using UnityEngine;

namespace WelpTimeToDie.Levels
{
    /// <summary>Four solid limits; only the two side limits have visible line sprites.</summary>
    public sealed class MapBoundaryController : MonoBehaviour
    {
        [SerializeField] private BoxCollider2D leftBoundary;
        [SerializeField] private BoxCollider2D rightBoundary;
        [SerializeField] private BoxCollider2D bottomBoundary;
        [SerializeField] private BoxCollider2D topBoundary;
        [SerializeField, Min(0.01f)] private float thickness = 0.08f;

        public void Rebuild(Bounds bounds)
        {
            Place(leftBoundary, new Vector2(bounds.min.x - thickness / 2f, bounds.center.y), new Vector2(thickness, bounds.size.y + thickness * 2f));
            Place(rightBoundary, new Vector2(bounds.max.x + thickness / 2f, bounds.center.y), new Vector2(thickness, bounds.size.y + thickness * 2f));
            Place(bottomBoundary, new Vector2(bounds.center.x, bounds.min.y - thickness / 2f), new Vector2(bounds.size.x, thickness));
            Place(topBoundary, new Vector2(bounds.center.x, bounds.max.y + thickness / 2f), new Vector2(bounds.size.x, thickness));
        }

        private static void Place(BoxCollider2D boundary, Vector2 position, Vector2 size)
        {
            if (boundary == null) return;
            boundary.transform.position = new Vector3(position.x, position.y, 0f);
            boundary.transform.localScale = Vector3.one;
            boundary.size = size;
            var line = boundary.GetComponentInChildren<SpriteRenderer>();
            if (line != null && line.transform != boundary.transform && line.sprite != null)
            {
                line.drawMode = SpriteDrawMode.Simple;
                Vector3 spriteSize = line.sprite.bounds.size;
                line.transform.localPosition = Vector3.zero;
                line.transform.localScale = new Vector3(size.x / spriteSize.x, size.y / spriteSize.y, 1f);
            }
        }
    }
}
