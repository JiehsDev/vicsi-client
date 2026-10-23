#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class HousePhotographyEntryGateSetup
{
    const string Output = "docs/evidence/house-photography-entry-2026-10-22";
    static HousePhotographyEntryGateSetup() { EditorApplication.update += Poll; }
    public static void OpenAndApply()
    {
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/Scenarios/CSI_Environment.unity");
        Apply();
    }
    static void Poll()
    {
        const string flag = "Temp/setup-house-photography-entry.flag";
        if (!File.Exists(flag) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(flag);
        try { Apply(); } catch (Exception error) { Directory.CreateDirectory(Output); File.WriteAllText(Output + "/setup-error.txt", error.ToString()); Debug.LogException(error); }
    }
    [MenuItem("Tools/ViCSI/Scene Setup/Install House Photography Entry Wall")]
    public static void Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != "Assets/_Project/Scenes/Scenarios/CSI_Environment.unity")
            throw new InvalidOperationException("Open CSI_Environment outside Play mode.");
        var cordon = UnityEngine.Object.FindAnyObjectByType<SceneCordon>();
        var house = GameObject.Find("Vicsi_House");
        var frame = house != null ? house.transform.Find("DoorFrame") : null;
        if (cordon == null || frame == null) throw new InvalidOperationException("House entrance or cordon missing.");
        Directory.CreateDirectory(Output);
        if (!File.Exists(Output + "/CSI_Environment-before.unity") && !EditorSceneManager.SaveScene(scene, Output + "/CSI_Environment-before.unity", true))
            throw new IOException("Scene backup failed.");
        var gate = UnityEngine.Object.FindAnyObjectByType<HousePhotographyEntryGate>();
        if (gate == null)
        {
            var go = new GameObject("House entrance - outdoor photography wall");
            Undo.RegisterCreatedObjectUndo(go, "Add photography entry wall");
            gate = Undo.AddComponent<HousePhotographyEntryGate>(go);
        }
        var wall = gate.GetComponent<BoxCollider>();
        if (wall == null) wall = Undo.AddComponent<BoxCollider>(gate.gameObject);
        var bounds = frame.GetComponent<Renderer>().bounds;
        Undo.RecordObject(gate.transform, "Place entrance wall");
        gate.transform.SetPositionAndRotation(new Vector3(bounds.center.x, frame.position.y + 1.2f, bounds.min.z), Quaternion.identity);
        gate.transform.localScale = Vector3.one;
        Undo.RecordObject(wall, "Size entrance wall");
        wall.center = Vector3.zero; wall.size = new Vector3(bounds.size.x + .12f, 2.4f, .18f);
        wall.isTrigger = false; wall.enabled = false; // IOC starts with no additional obstruction.
        var data = new SerializedObject(gate); data.FindProperty("entranceWall").objectReferenceValue = wall; data.ApplyModifiedProperties();
        data = new SerializedObject(cordon); data.FindProperty("photographyEntryGate").objectReferenceValue = gate; data.ApplyModifiedProperties();
        Physics.SyncTransforms();
        var character = UnityEngine.Object.FindAnyObjectByType<Oculus.Interaction.Locomotion.CharacterController>();
        if (character == null || (character.LayerMask.value & (1 << gate.gameObject.layer)) == 0)
            throw new InvalidOperationException("The player locomotion mask must collide with the wall layer.");
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Scene save failed.");
        File.WriteAllText(Output + "/setup.txt", "Saved editable front doorway wall: " + gate.transform.position + " size " + wall.size
            + ". IOC phases: disabled. SOCO: enabled until all required Exterior and Entrance photos complete. Existing cordon handles room-scale/teleport bypass.\n");
    }
}
#endif
