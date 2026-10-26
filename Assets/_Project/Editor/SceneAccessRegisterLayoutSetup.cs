#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class SceneAccessRegisterLayoutSetup
{
    const string Request = "Temp/refresh-register-layout.flag";
    const string ScenePath = "Assets/_Project/Scenes/Scenarios/CSI_Environment.unity";
    static SceneAccessRegisterLayoutSetup() => EditorApplication.update += Poll;
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        try { Refresh(); SceneAccessRegisterPreview.Run(); }
        catch (Exception error) { File.WriteAllText("Temp/register-layout-error.txt", error.ToString()); Debug.LogException(error); }
    }

    [MenuItem("ViCSI/Scene Setup/Refresh Register Layout")]
    public static void Refresh()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before refreshing the register.");
        var current = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (current.path != ScenePath && current.isDirty) throw new InvalidOperationException("Save the open scene before opening CSI_Environment.");
        var scene = current.path == ScenePath ? current : EditorSceneManager.OpenScene(ScenePath);
        // Preserve the currently open scene, including any unsaved edits, before
        // replacing only this panel's generated visual objects.
        if (!EditorSceneManager.SaveScene(scene, "Temp/research-register-before.unity", true))
            throw new IOException("Could not back up the open research scene.");
        File.Copy(ScenePath, "Temp/research-register-disk-before.unity", true);
        var ui = UnityEngine.Object.FindAnyObjectByType<SceneAccessRegisterUI>();
        if (ui == null) throw new InvalidOperationException("Scene access register is missing.");
        var storage = ui.GetComponent<EditableSceneVisuals>();
        var group = storage.Groups.First(g => g.owner == ui && g.builder == "BuildPanel");
        Undo.RecordObject(storage, "Refresh register panel template");
        foreach (var go in group.objects.ToArray()) if (go != null) Undo.DestroyObjectImmediate(go);
        group.objects.Clear();
        typeof(SceneAccessRegisterUI).GetMethod("BuildPanel", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ui, null);
        EditorUtility.SetDirty(storage); EditorUtility.SetDirty(ui);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save the research register layout.");
        File.WriteAllText("Temp/register-layout-status.txt", "Saved research register UI. Pre-existing scene edits retained and backed up; other visual groups preserved.");
    }
}
#endif
