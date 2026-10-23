#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ArrivalPPEAuthoring
{
    public static void Build()
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Environments/MODELS/NEW/CLEAN_GLOVES.glb");
        if (model == null) throw new System.InvalidOperationException("Clean glove model missing.");
        var go = new GameObject("Arrival PPE station");
        try
        {
            var component = go.AddComponent<ArrivalPPEStation>();
            var data = new SerializedObject(component);
            data.FindProperty("cleanGlovesModel").objectReferenceValue = model;
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(go, "Assets/Resources/ArrivalPPEStation.prefab");
        }
        finally { Object.DestroyImmediate(go); }
        AssetDatabase.SaveAssets();
        Inspect();
    }
    public static void Inspect()
    {
        Directory.CreateDirectory("docs/evidence/arrival-ppe");
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/Scenarios/CSI_Environment.unity");
        var rig = Object.FindAnyObjectByType<OVRCameraRig>();
        var cordon = Object.FindAnyObjectByType<SceneCordon>();
        string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;
        var lines = rig.GetComponentsInChildren<Renderer>(true).Select(r => Path(r.transform) + " active=" + r.gameObject.activeInHierarchy + " enabled=" + r.enabled + " type=" + r.GetType().Name);
        File.WriteAllLines("docs/evidence/arrival-ppe/hand-renderers.txt", lines);
        File.WriteAllLines("docs/evidence/arrival-ppe/placement.txt", new[] { "rig=" + rig.transform.position.ToString("F4"), "left=" + cordon.transform.Find("EntrancePost_L").position.ToString("F4"), "right=" + cordon.transform.Find("EntrancePost_R").position.ToString("F4") });
        EditorApplication.Exit(0);
    }
}
#endif
