using UnityEngine;
using WelpTimeToDie.Levels;

namespace WelpTimeToDie.Player
{
    /// <summary>Shared placement rule for scene authoring, initial spawn and map regeneration.</summary>
    public static class PlayerSpawnPlacement
    {
        public static Vector2 Calculate(ProceduralTerrainMap terrain, BoxCollider2D collider)
        {
            Physics2D.SyncTransforms();
            Bounds body = collider.bounds;
            float center = terrain.Layout.WorldBounds.center.x;
            float offsetY = body.center.y - collider.transform.position.y;
            float offsetX = body.center.x - collider.transform.position.x;
            return new Vector2(center - offsetX,
                terrain.GetSurfaceHeight(center, body.extents.x) + body.extents.y - offsetY + 0.02f);
        }
    }
}
