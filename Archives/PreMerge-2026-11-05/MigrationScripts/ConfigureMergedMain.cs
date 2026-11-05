using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.SceneManagement;
using WelpTimeToDie.RectanglePrototype;
using WelpTimeToDie.Levels;
using WelpTimeToDie.Cameras;

public static class ConfigureMergedMain
{
    private static void Ref(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(field).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
    public static string Main()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop playback first.");
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Main.unity", OpenSceneMode.Single);
        var terrain = Object.FindFirstObjectByType<ProceduralTerrainMap>();
        terrain.GenerateFromSeedInput();
        var camera = Object.FindFirstObjectByType<MapCameraController>();
        foreach (string name in new[] { "Combat Playback", "Player Input", "Combat HUD", "Combat Physics", "Player" })
        {
            var old = GameObject.Find(name);
            if (old != null) { old.SetActive(false); old.name = "Legacy - " + name; }
        }
        var existing = GameObject.Find("Arena Combat");
        if (existing != null) throw new System.InvalidOperationException("Main already configured.");
        var root = new GameObject("Arena Combat");
        var stage = root.AddComponent<RectangleStage>();
        var arena = root.AddComponent<RectangleArena>();
        var document = root.AddComponent<UIDocument>();
        var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/Combat/CombatHudPanel.asset");
        var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Combat/ArenaHud.uxml");
        if (panel == null || tree == null) throw new System.Exception("HUD assets missing.");
        document.panelSettings = panel; document.visualTreeAsset = tree;
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/RectanglePrototype/Art/Rectangle.png");
        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/RectanglePrototype/Art/RectangleUnlit.mat");
        var player = new GameObject("Player - Rectangle").AddComponent<SpriteRenderer>();
        player.transform.SetParent(root.transform, false); player.sprite = sprite; player.sharedMaterial = material;
        player.transform.position = new Vector3(-1.5f, terrain.GetSurfaceHeight(-1.5f, 0.45f) + 0.9f, 0);
        player.transform.localScale = new Vector3(0.9f, 1.8f, 1); player.color = new Color(0.25f, 0.8f, 1);
        player.sortingOrder = 5;
        var enemy = new GameObject("Enemy - Rectangle fallback").AddComponent<SpriteRenderer>();
        enemy.transform.SetParent(root.transform, false); enemy.sprite = sprite; enemy.sharedMaterial = material;
        enemy.transform.position = new Vector3(1.5f, terrain.GetSurfaceHeight(1.5f, 0.45f) + 0.9f, 0);
        enemy.transform.localScale = new Vector3(0.9f, 1.8f, 1); enemy.color = new Color(1, 0.35f, 0.4f);
        enemy.sortingOrder = 5; enemy.enabled = false;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Sprites/Golem.psb");
        if (prefab == null) throw new System.Exception("Golem import missing.");
        var golem = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        golem.name = "Enemy - Golem"; golem.transform.SetParent(root.transform, false);
        golem.transform.position = new Vector3(1.5f, terrain.GetSurfaceHeight(1.5f, 0.45f), 0);
        golem.transform.localScale = new Vector3(-1.45f, 1.45f, 1);
        foreach (var animator in golem.GetComponentsInChildren<Animator>()) animator.enabled = false;
        foreach (var renderer in golem.GetComponentsInChildren<SpriteRenderer>()) renderer.sortingOrder += 5;
        var rig = golem.AddComponent<GolemPhysicsAnimator>();
        Ref(stage, "arenaCamera", camera.GetComponent<Camera>()); Ref(stage, "playerBody", player);
        Ref(stage, "enemyBody", enemy); Ref(stage, "enemyGolem", rig);
        var stageSO = new SerializedObject(stage); stageSO.FindProperty("useMapCamera").boolValue = true; stageSO.ApplyModifiedPropertiesWithoutUndo();
        Ref(arena, "stage", stage); Ref(arena, "terrain", terrain); Ref(arena, "mapCamera", camera);
        Ref(arena, "hudAsset", tree); Ref(arena, "savedPanel", panel);
        Ref(camera, "player", player.transform);
        Ref(camera.GetComponent<MapCameraInput>(), "hud", document);
        var mapUI = root.AddComponent<WelpTimeToDie.UI.MapHudPresenter>();
        Ref(mapUI, "mapCamera", camera); Ref(mapUI, "terrain", terrain);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        return "Main merged; legacy objects retained inactive pending verification.";
    }
}
