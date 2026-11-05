using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using WelpTimeToDie.RectanglePrototype;
using WelpTimeToDie.Levels;

public static class RestoreGolemBind
{
    public static string Main()
    {
        var rig = Object.FindFirstObjectByType<GolemPhysicsAnimator>();
        foreach (var bone in rig.GetComponentsInChildren<Transform>(true))
        {
            if (bone == rig.transform) continue;
            var source = PrefabUtility.GetCorrespondingObjectFromSource(bone);
            if (source == null) continue;
            bone.localPosition = source.localPosition;
            bone.localRotation = source.localRotation;
            bone.localScale = source.localScale;
            PrefabUtility.RecordPrefabInstancePropertyModifications(bone);
        }
        Object.FindFirstObjectByType<RectangleStage>().PlaceOnTerrain(Object.FindFirstObjectByType<ProceduralTerrainMap>());
        EditorSceneManager.SaveScene(rig.gameObject.scene);
        return "Imported bind transforms restored; only root placement is authored.";
    }
}
