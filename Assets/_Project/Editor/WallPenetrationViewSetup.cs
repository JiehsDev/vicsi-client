#if UNITY_EDITOR
using System;
using System.IO;
using Oculus.Interaction;
using Oculus.Interaction.Locomotion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class WallPenetrationViewSetup
{
    const string Output = "docs/evidence/wall-penetration-view-2026-10-22";
    static WallPenetrationViewSetup() { EditorApplication.update += Poll; }
    static void Poll()
    {
        const string flag = "Temp/setup-wall-penetration-view.flag";
        if (!File.Exists(flag) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(flag);
        try { Apply(); } catch (Exception error) { Directory.CreateDirectory(Output); File.WriteAllText(Output + "/setup-error.txt", error.ToString()); Debug.LogException(error); }
    }
    [MenuItem("Tools/ViCSI/Scene Setup/Fix Wall Penetration View Filter")]
    public static void Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != "Assets/_Project/Scenes/Scenarios/CSI_Environment.unity")
            throw new InvalidOperationException("Open CSI_Environment outside Play mode.");
        Directory.CreateDirectory(Output);
        if (!File.Exists(Output + "/CSI_Environment-before.unity") && !EditorSceneManager.SaveScene(scene, Output + "/CSI_Environment-before.unity", true)) throw new IOException("Backup failed.");
        ComfortMaskReferenceRepair.Repair();
        int installed = 0;
        foreach (var original in UnityEngine.Object.FindObjectsByType<WallPenetrationTunneling>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var source = new SerializedObject(original);
            var effect = (TunnelingEffect)source.FindProperty("_tunneling").objectReferenceValue;
            var filter = (MeshFilter)new SerializedObject(effect).FindProperty("_meshFilter").objectReferenceValue;
            var guard = original.GetComponent<WallPenetrationViewGuard>();
            if (guard == null) guard = Undo.AddComponent<WallPenetrationViewGuard>(original.gameObject);
            var target = new SerializedObject(guard);
            target.FindProperty("trackedHead").objectReferenceValue = source.FindProperty("_trackedPosition").objectReferenceValue;
            target.FindProperty("logicalHead").objectReferenceValue = source.FindProperty("_logicalPosition").objectReferenceValue;
            var rig = original.GetComponentInParent<OVRCameraRig>();
            target.FindProperty("playerRoot").objectReferenceValue = rig != null ? rig.transform : null;
            target.FindProperty("effect").objectReferenceValue = effect;
            target.FindProperty("maskRenderer").objectReferenceValue = filter.GetComponent<MeshRenderer>();
            target.FindProperty("wallLayers").intValue = original.LayerMask.value & ~(1 << 2) & ~(1 << 5);
            target.FindProperty("penetrationFov").animationCurveValue = original.PenetrationFov;
            target.ApplyModifiedProperties();
            Undo.RecordObject(original, "Replace wall mask query"); original.enabled = false;
            Undo.RecordObject(effect, "Start wall mask hidden"); effect.enabled = false;
            var renderer = filter.GetComponent<MeshRenderer>(); Undo.RecordObject(renderer, "Start wall mesh hidden"); renderer.enabled = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(original);
            PrefabUtility.RecordPrefabInstancePropertyModifications(effect);
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            installed++;
        }
        if (installed == 0) throw new InvalidOperationException("No wall-penetration driver found.");
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Scene save failed.");
        File.WriteAllText(Output + "/setup.txt", "Saved " + installed + " filtered wall-mask controller(s). Triggers, Canvas/RectTransform UI, player hierarchy, Ignore Raycast and the photography entry barrier excluded. No ahead-of-head ray extension. Wall renderer starts hidden; real wall protection retained.\n");
    }
}
#endif
