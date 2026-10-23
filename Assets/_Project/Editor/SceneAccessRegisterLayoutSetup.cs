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
        try { Refresh(); CompetitiveResultsPreview.RunRegister(); }
        catch (Exception error) { File.WriteAllText("Temp/register-layout-error.txt", error.ToString()); Debug.LogException(error); }
    }

    [MenuItem("ViCSI/Scene Setup/Refresh Register Layout")]
    public static void Refresh()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before refreshing the register.");
        var current = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (current.isDirty) throw new InvalidOperationException("The open scene has unsaved changes; save it before refreshing the register.");
        var scene = current.path == ScenePath ? current : EditorSceneManager.OpenScene(ScenePath);
        var ui = UnityEngine.Object.FindAnyObjectByType<SceneAccessRegisterUI>();
        if (ui == null) throw new InvalidOperationException("Scene access register is missing.");
        var storage = ui.GetComponent<EditableSceneVisuals>();
        var group = storage.Groups.First(g => g.owner == ui && g.builder == "BuildPanel");
        File.Copy(ScenePath, "Temp/register-scene-before.unity", true);
        Undo.RecordObject(storage, "Refresh register panel template");
        foreach (var go in group.objects.ToArray()) if (go != null) Undo.DestroyObjectImmediate(go);
        group.objects.Clear();
        typeof(SceneAccessRegisterUI).GetMethod("BuildPanel", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ui, null);
        EditorUtility.SetDirty(storage); EditorUtility.SetDirty(ui);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save the register layout.");
        File.WriteAllText("Temp/register-layout-status.txt", "Saved refreshed register panel template. Logging table and other authored UI groups preserved.");
    }
}
#endif
