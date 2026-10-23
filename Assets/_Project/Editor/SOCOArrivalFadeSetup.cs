#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class SOCOArrivalFadeSetup
{
    const string Output = "docs/evidence/soco-arrival-fade-2026-10-22";
    static SOCOArrivalFadeSetup() { EditorApplication.update += Poll; }
    static void Poll()
    {
        const string flag = "Temp/setup-soco-arrival-fade.flag";
        if (!File.Exists(flag) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(flag);
        try { Apply(); } catch (Exception e) { Directory.CreateDirectory(Output); File.WriteAllText(Output + "/setup-error.txt", e.ToString()); Debug.LogException(e); }
    }
    [MenuItem("Tools/ViCSI/Scene Setup/Install SOCO Arrival Fade")]
    public static void Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != "Assets/_Project/Scenes/Scenarios/CSI_Environment.unity" || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Open CSI_Environment outside Play mode.");
        var workflow = UnityEngine.Object.FindAnyObjectByType<SOCOWorkflowInteraction>();
        var rig = UnityEngine.Object.FindAnyObjectByType<OVRCameraRig>();
        if (workflow == null || rig == null) throw new InvalidOperationException("Missing workflow or camera rig.");
        const string materialPath = "Assets/_Project/Art/Materials/ArrivalComfortFade.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (mat == null) { mat = new Material(Shader.Find("ViCSI/Arrival Comfort Fade")); mat.color = Color.clear; AssetDatabase.CreateAsset(mat, materialPath); }
        var overlay = GameObject.Find("SOCO arrival - comfort fade");
        if (overlay == null)
        {
            overlay = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Undo.RegisterCreatedObjectUndo(overlay, "Author SOCO fade");
            overlay.name = "SOCO arrival - comfort fade";
            UnityEngine.Object.DestroyImmediate(overlay.GetComponent<Collider>());
        }
        overlay.transform.SetParent(rig.centerEyeAnchor, false);
        overlay.transform.localPosition = new Vector3(0, 0, .4f);
        overlay.transform.localRotation = Quaternion.identity;
        overlay.transform.localScale = Vector3.one * 10;
        var renderer = overlay.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = mat; renderer.enabled = false;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
        var anchor = GameObject.Find("SOCO arrival - grounded destination");
        if (anchor == null)
        {
            anchor = new GameObject("SOCO arrival - grounded destination");
            Undo.RegisterCreatedObjectUndo(anchor, "Author grounded SOCO destination");
            Physics.SyncTransforms();
            var hits = Physics.RaycastAll(rig.transform.position + Vector3.up * 3, Vector3.down, 20, ~0, QueryTriggerInteraction.Ignore)
                .Where(h => h.normal.y > .8f && !h.transform.IsChildOf(rig.transform)).OrderBy(h => h.distance).ToArray();
            if (hits.Length == 0) throw new InvalidOperationException("No ground found at entrance.");
            anchor.transform.SetPositionAndRotation(hits[0].point + Vector3.up * .01f, Quaternion.Euler(0, rig.transform.eulerAngles.y, 0));
        }
        var so = new SerializedObject(workflow);
        so.FindProperty("arrivalFade").objectReferenceValue = renderer;
        so.FindProperty("arrivalFeetAnchor").objectReferenceValue = anchor.transform;
        so.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
        Directory.CreateDirectory(Output);
        File.WriteAllText(Output + "/setup.txt", "Saved editable stereo fade quad and grounded arrival anchor at " + anchor.transform.position + ". Fade 0.5s each direction; black settling hold 0.4s.\n");
    }
}
#endif
