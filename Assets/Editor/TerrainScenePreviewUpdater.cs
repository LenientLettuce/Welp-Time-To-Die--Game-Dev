using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using WelpTimeToDie.Levels;
using WelpTimeToDie.Player;

namespace WelpTimeToDie.Editor
{
    /// <summary>Refreshes dirty map previews on the editor clock, even when no game frames run.</summary>
    [InitializeOnLoad]
    public static class TerrainScenePreviewUpdater
    {
        private static double nextCheck;
        private static readonly Dictionary<int, TerrainHeightMap> placedMaps = new Dictionary<int, TerrainHeightMap>();

        static TerrainScenePreviewUpdater() => EditorApplication.update += Refresh;

        private static void Refresh()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
                EditorApplication.isUpdating || EditorApplication.timeSinceStartup < nextCheck) return;
            nextCheck = EditorApplication.timeSinceStartup + 0.1;
            foreach (var map in Object.FindObjectsByType<ProceduralTerrainMap>(FindObjectsSortMode.None))
            {
                if (!map.isActiveAndEnabled) continue;
                map.RefreshEditorPreviewIfNeeded();
                int id = map.GetInstanceID();
                if (map.HeightMap == null || placedMaps.TryGetValue(id, out var previous) && previous == map.HeightMap) continue;
                placedMaps[id] = map.HeightMap;
                PlacePlayers(map);
            }
        }

        private static void PlacePlayers(ProceduralTerrainMap map)
        {
            foreach (var player in Object.FindObjectsByType<PlayerActionController>(FindObjectsSortMode.None))
            {
                var motor = player.GetComponent<PlayerMovementMotor>();
                if (motor == null || motor.Terrain != map) continue;
                var collider = player.GetComponent<BoxCollider2D>();
                Vector2 spawn = PlayerSpawnPlacement.Calculate(map, collider);
                player.transform.position = new Vector3(spawn.x, spawn.y, player.transform.position.z);
                EditorUtility.SetDirty(player.transform);
            }
            foreach (var camera in Object.FindObjectsByType<WelpTimeToDie.Cameras.MapCameraController>(FindObjectsSortMode.None))
                if (camera.gameObject.scene == map.gameObject.scene) camera.RecenterOnPlayer();
            Physics2D.SyncTransforms();
        }
    }
}
