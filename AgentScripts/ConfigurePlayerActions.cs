using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using WelpTimeToDie.Combat;
using WelpTimeToDie.Cameras;
using WelpTimeToDie.Input;
using WelpTimeToDie.Levels;
using WelpTimeToDie.Player;
using WelpTimeToDie.Playback;
using WelpTimeToDie.UI;

public static class ConfigurePlayerActions
{
    public static object Configure()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play Mode before scene setup.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/Main.unity") throw new System.InvalidOperationException("Open Main first.");
        Directory.CreateDirectory("Temp/PlayerMovesRestore");
        EditorSceneManager.SaveScene(scene, "Temp/PlayerMovesRestore/MainBeforeActions.unity", true);
        var player = GameObject.Find("Player");
        var oldFollower = player.GetComponent<PlayerTerrainFollower>();
        if (oldFollower != null) Object.DestroyImmediate(oldFollower);
        var oldDash = player.GetComponent<PlayerDashController>();
        if (oldDash != null) Object.DestroyImmediate(oldDash);
        var oldInput = Object.FindFirstObjectByType<PlayerKeyboardInput>();
        GameObject inputObject = oldInput != null ? oldInput.gameObject : GameObject.Find("Player Input");
        if (oldInput != null) Object.DestroyImmediate(oldInput);
        if (inputObject == null) inputObject = new GameObject("Player Input");
        inputObject.name = "Player Input";

        var terrain = Object.FindFirstObjectByType<ProceduralTerrainMap>();
        if (terrain.HeightMap == null) terrain.GenerateFromSeedInput();
        var clock = Object.FindFirstObjectByType<CombatPhysicsController>();
        var playback = Object.FindFirstObjectByType<CombatPlaybackController>();
        var hud = Object.FindFirstObjectByType<UIDocument>();
        var source = player.GetComponent<SpriteRenderer>();
        Vector3 spriteSize = source.sprite.bounds.size;
        player.transform.localScale = new Vector3(1f / spriteSize.x, 2f / spriteSize.y, 1f);
        var collider = player.GetComponent<BoxCollider2D>();
        collider.size = spriteSize;
        collider.offset = source.sprite.bounds.center;
        const string materialPath = "Assets/Settings/PlayerMovement.physicsMaterial2D";
        var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(materialPath);
        if (material == null)
        {
            material = new PhysicsMaterial2D("Player movement") { friction = 0f, bounciness = 0f };
            AssetDatabase.CreateAsset(material, materialPath);
        }
        collider.sharedMaterial = material;
        player.GetComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.FreezeRotation;
        var motor = GetOrAdd<PlayerMovementMotor>(player);
        SetReference(motor, "terrain", terrain);
        var actor = GetOrAdd<PlayerActionController>(player);
        SetReference(actor, "terrain", terrain);
        SetReference(actor, "combatPhysics", clock);
        var selection = GetOrAdd<PlayerActionSelection>(player);
        GetOrAdd<PlayerCombatController>(player);
        GetOrAdd<PlayerActionVisuals>(player);
        GetOrAdd<PlayerActionPreview>(player);
        SetReference(playback, "player", null);
        SetReference(playback, "actionPlayer", actor);
        var input = GetOrAdd<PlayerClickInput>(inputObject);
        SetReference(input, "player", actor);
        SetReference(input, "selection", selection);
        SetReference(input, "playback", playback);
        SetReference(input, "worldCamera", Camera.main);
        SetReference(input, "hud", hud);
        var actionHud = GetOrAdd<PlayerActionHudPresenter>(hud.gameObject);
        SetReference(actionHud, "player", actor);
        SetReference(actionHud, "selection", selection);
        SetReference(actionHud, "playback", playback);
        var camera = Camera.main.GetComponent<MapCameraController>();
        SetReference(camera, "player", player.transform);
        var cameraSettings = new SerializedObject(camera);
        cameraSettings.FindProperty("followOffset").vector2Value = new Vector2(0f, 0.8f);
        cameraSettings.ApplyModifiedPropertiesWithoutUndo();
        Physics2D.SyncTransforms();
        float spawnY = terrain.GetSurfaceHeight(0f, 0.5f) + 1.02f;
        player.transform.position = new Vector3(0f, spawnY, 0f);
        Camera.main.transform.position = new Vector3(0f, spawnY + 0.8f, -10f);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        return new { scene = scene.path, playerWidthPixels = 16, playerHeightPixels = 32, keyboardInputRemoved = true };
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        var component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }
    private static void SetReference(Object target, string name, Object value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(name).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
