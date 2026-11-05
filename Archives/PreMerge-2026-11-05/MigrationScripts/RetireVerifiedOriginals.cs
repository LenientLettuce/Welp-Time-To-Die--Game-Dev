using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class RetireVerifiedOriginals
{
    public static string Main()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before cleanup.");
        string archive = "Archives/PreMerge-2026-11-05/OriginalScenesAndSystems.unitypackage";
        if (!File.Exists(archive) || new FileInfo(archive).Length < 100000) throw new Exception("Original archive missing.");
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Main.unity", OpenSceneMode.Single);
        foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true).Reverse())
                if (t != null && (t.name.StartsWith("Legacy - ") || t.name == "Prototype Geometry (visual only)"))
                    UnityEngine.Object.DestroyImmediate(t.gameObject);
        var camera = Camera.main;
        var oldFraming = camera.GetComponent<WelpTimeToDie.Cameras.LevelCameraFraming>();
        if (oldFraming != null) UnityEngine.Object.DestroyImmediate(oldFraming);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        AssetDatabase.StartAssetEditing();
        try
        {
        foreach (var pair in new[] {
            new[] {"Assets/RectanglePrototype/Scripts", "Assets/Scripts/Arena"},
            new[] {"Assets/RectanglePrototype/Art", "Assets/Art/Arena"},
            new[] {"Assets/RectanglePrototype/Tests", "Assets/Tests/Arena"}
        })
        {
            string error = AssetDatabase.MoveAsset(pair[0], pair[1]);
            if (!string.IsNullOrEmpty(error)) throw new Exception(error);
        }
        foreach (var path in new[] {
            "Assets/RectanglePrototype", "Assets/Scripts/Gameplay/Player", "Assets/Scripts/Gameplay/Input",
            "Assets/Scripts/Combat", "Assets/Tests/PlayMode",
            "Assets/Scripts/Gameplay/Playback", "Assets/Scripts/Gameplay/UI/PlayerActionHudPresenter.cs",
            "Assets/Scripts/Gameplay/UI/CombatHudPresenter.cs", "Assets/Scripts/Gameplay/Camera/LevelCameraFraming.cs",
            "Assets/Tests/Gameplay/PlayMode/PlayerActionControllerTests.cs", "Assets/Tests/Gameplay/PlayMode/PlayerDashControllerTests.cs",
            "Assets/Tests/Gameplay/PlayMode/DamageReceiverProbe.cs", "Assets/UI/Combat/CombatHud.uxml", "Assets/UI/Combat/CombatHud.uss",
            "Assets/Archives"
        })
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null || AssetDatabase.IsValidFolder(path))
                if (!AssetDatabase.DeleteAsset(path)) throw new Exception("Could not retire " + path);
        }
        foreach(var definition in new[] {"Assets/Scripts/Gameplay/WelpTimeToDie.Gameplay.asmdef", "Assets/Tests/Gameplay/PlayMode/WelpTimeToDie.Gameplay.Tests.asmdef"})
            File.WriteAllText(definition, File.ReadAllText(definition).Replace("\"WelpTimeToDie.Combat\", ", ""));
        }
        finally { AssetDatabase.StopAssetEditing(); }
        EditorSceneManager.SaveScene(scene);
        return "Originals archived; retired duplicate scenes/controllers/clocks/UI; kept arena/terrain regression tests and sprite assets.";
    }
}
