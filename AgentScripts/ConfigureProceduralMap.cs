using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UIElements;
using WelpTimeToDie.Cameras;
using WelpTimeToDie.Combat;
using WelpTimeToDie.Input;
using WelpTimeToDie.Levels;
using WelpTimeToDie.Player;
using WelpTimeToDie.UI;

/// <summary>Repeatable scene integration. Run through Unity Pipeline's run_script command.</summary>
public static class ConfigureProceduralMap
{
    public static object Configure()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play Mode before configuring the scene.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/Main.unity") throw new System.InvalidOperationException("Open Main before configuring it.");
        EditorSceneManager.SaveScene(scene, "Temp/MapCreationRestore/MainBeforeIntegration.unity", true);
        var level = GameObject.Find("Level").GetComponent<LevelLayout>();
        var playerObject = GameObject.Find("Player");
        var player = playerObject.GetComponent<PlayerDashController>();
        var clock = Object.FindFirstObjectByType<CombatPhysicsController>();
        var hud = Object.FindFirstObjectByType<CombatHudPresenter>();
        var document = hud.GetComponent<UIDocument>();
        var camera = Camera.main;
        var oldFraming = camera.GetComponent<LevelCameraFraming>();
        if (oldFraming != null) oldFraming.enabled = false;
        var oldGeometry = GameObject.Find("Prototype Geometry (visual only)");
        if (oldGeometry != null) oldGeometry.SetActive(false);

        Directory.CreateDirectory("Assets/Art/Terrain");
        const string texturePath = "Assets/Art/Terrain/PrototypeGround.png";
        if (!File.Exists(texturePath))
        {
            var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                    texture.SetPixel(x, y, x == 0 || y == 0 ? new Color(0.16f, 0.24f, 0.28f) : new Color(0.32f, 0.43f, 0.44f));
            texture.Apply();
            File.WriteAllBytes(texturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }
        AssetDatabase.ImportAsset(texturePath);
        var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = TerrainSettings.PixelsPerBlock;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        var groundSprite = AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);
        const string tilePath = "Assets/Art/Terrain/PrototypeGroundTile.asset";
        var tile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
        if (tile == null)
        {
            tile = ScriptableObject.CreateInstance<Tile>();
            AssetDatabase.CreateAsset(tile, tilePath);
        }
        tile.sprite = groundSprite;
        tile.colliderType = Tile.ColliderType.Grid;
        EditorUtility.SetDirty(tile);

        var tilemap = level.GetComponentInChildren<Tilemap>();
        var renderer = GetOrAdd<TerrainTilemapRenderer>(tilemap.gameObject);
        SetReference(renderer, "groundTile", tile);
        var boundaryObject = level.transform.Find("Map Boundaries");
        if (boundaryObject == null)
        {
            var created = new GameObject("Map Boundaries");
            created.transform.SetParent(level.transform, false);
            boundaryObject = created.transform;
        }
        var boundaries = GetOrAdd<MapBoundaryController>(boundaryObject.gameObject);
        var lineSprite = playerObject.GetComponent<SpriteRenderer>().sprite;
        var lineMaterial = playerObject.GetComponent<SpriteRenderer>().sharedMaterial;
        string[] names = { "Left Boundary", "Right Boundary", "Bottom Boundary", "Top Boundary" };
        string[] fields = { "leftBoundary", "rightBoundary", "bottomBoundary", "topBoundary" };
        for (int i = 0; i < names.Length; i++)
        {
            var child = boundaryObject.Find(names[i]);
            if (child == null)
            {
                var created = new GameObject(names[i]);
                created.transform.SetParent(boundaryObject, false);
                child = created.transform;
            }
            var collider = GetOrAdd<BoxCollider2D>(child.gameObject);
            collider.isTrigger = false;
            SetReference(boundaries, fields[i], collider);
            if (i < 2)
            {
                var oldSprite = child.GetComponent<SpriteRenderer>();
                if (oldSprite != null) Object.DestroyImmediate(oldSprite);
                var line = child.Find("Boundary Line");
                if (line == null)
                {
                    line = new GameObject("Boundary Line").transform;
                    line.SetParent(child, false);
                }
                var sprite = GetOrAdd<SpriteRenderer>(line.gameObject);
                sprite.sprite = lineSprite;
                sprite.sharedMaterial = lineMaterial;
                sprite.color = new Color(0.28f, 0.85f, 0.95f);
                sprite.sortingOrder = 5;
            }
        }
        var terrain = GetOrAdd<ProceduralTerrainMap>(level.gameObject);
        SetReference(terrain, "terrainRenderer", renderer);
        SetReference(terrain, "boundaries", boundaries);
        terrain.GenerateFromSeedInput();

        if (player != null)
        {
            var follower = GetOrAdd<PlayerTerrainFollower>(player.gameObject);
            SetReference(follower, "terrain", terrain);
            SetReference(follower, "combatPhysics", clock);
            var playerSettings = new SerializedObject(player);
            playerSettings.FindProperty("scaleDashWithLevel").boolValue = false;
            playerSettings.FindProperty("fallbackLevelWidth").floatValue = 20f;
            playerSettings.ApplyModifiedPropertiesWithoutUndo();
        }
        else
        {
            SetReference(playerObject.GetComponent<PlayerMovementMotor>(), "terrain", terrain);
            SetReference(playerObject.GetComponent<PlayerActionController>(), "terrain", terrain);
        }
        var navigation = GetOrAdd<MapCameraController>(camera.gameObject);
        SetReference(navigation, "player", playerObject.transform);
        SetReference(navigation, "level", level);
        var navigationSettings = new SerializedObject(navigation);
        navigationSettings.FindProperty("defaultZoom").floatValue = camera.orthographicSize;
        navigationSettings.ApplyModifiedPropertiesWithoutUndo();
        SetReference(GetOrAdd<MapCameraInput>(camera.gameObject), "hud", document);
        var mapHud = GetOrAdd<MapHudPresenter>(hud.gameObject);
        SetReference(mapHud, "mapCamera", navigation);
        SetReference(mapHud, "terrain", terrain);
        var keyboardInput = Object.FindFirstObjectByType<PlayerKeyboardInput>();
        if (keyboardInput != null) SetReference(keyboardInput, "hud", document);
        var clickInput = Object.FindFirstObjectByType<PlayerClickInput>();
        if (clickInput != null) SetReference(clickInput, "hud", document);
        Physics2D.SyncTransforms();
        Vector3 extents = playerObject.GetComponent<BoxCollider2D>().bounds.extents;
        playerObject.transform.position = new Vector3(0f, terrain.GetSurfaceHeight(0f, extents.x) + extents.y + 0.02f, 0f);
        camera.transform.position = new Vector3(0f, playerObject.transform.position.y + 0.8f, -10f);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return new { scene = scene.path, width = terrain.Settings.WidthInBlocks, height = terrain.Settings.HeightInBlocks,
            seed = terrain.CurrentSeed, defaultZoom = camera.orthographicSize, colliders = boundaries.GetComponentsInChildren<BoxCollider2D>().Length };
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
