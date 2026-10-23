#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Oculus.Interaction.Locomotion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Extend the saved Settings template while preserving existing authored controls.</summary>
[InitializeOnLoad]
public static class WalkingSpeedSettingsSetup
{
    const string Request = "Temp/refresh-walking-speed-settings.flag";
    const string Output = "docs/evidence/walking-speed-settings-2026-10-21";
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static WalkingSpeedSettingsSetup() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        try { Refresh(); }
        catch (Exception error) { Directory.CreateDirectory(Output); File.WriteAllText(Output+"/setup-error.txt",error.ToString()); Debug.LogException(error); }
    }
    [MenuItem("Tools/ViCSI/Scene Setup/Refresh Walking Speed Settings")]
    public static void Refresh()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != "Assets/_Project/Scenes/Scenarios/CSI_Environment.unity") throw new InvalidOperationException("Open CSI_Environment first.");
        Directory.CreateDirectory(Output);
        if (!File.Exists(Output+"/CSI_Environment-before.unity") && !EditorSceneManager.SaveScene(scene,Output+"/CSI_Environment-before.unity",true))
            throw new IOException("Could not back up current scene.");
        var settings = UnityEngine.Object.FindAnyObjectByType<SettingsPauseMenu>();
        var mover = UnityEngine.Object.FindAnyObjectByType<FirstPersonLocomotor>();
        if (settings == null || mover == null) throw new InvalidOperationException("Settings or locomotor missing.");
        if (mover.GetComponent<PlayerWalkingSpeed>() == null) Undo.AddComponent<PlayerWalkingSpeed>(mover.gameObject);
        var storage = settings.GetComponent<EditableSceneVisuals>();
        var group = storage.Groups.First(g=>g.owner==settings && g.builder=="BuildSettings");
        if (!group.objects.Any(go=>go!=null && go.name=="WalkingSpeedTitle"))
        {
            int restart = group.objects.FindIndex(go=>go!=null && go.name=="restart");
            if (restart<0) throw new InvalidOperationException("Saved settings restart control missing.");
            Undo.RecordObject(storage,"Add walking speed settings template");
            // Heading + heading container, value row + text, and three button/label pairs.
            group.objects.InsertRange(restart,new GameObject[10]);
        }
        typeof(SettingsPauseMenu).GetField("walkingSpeed",Flags).SetValue(settings,mover.GetComponent<PlayerWalkingSpeed>());
        var panel = (GameObject)typeof(SettingsPauseMenu).GetField("panel",Flags).GetValue(settings);
        if (panel==null) typeof(SettingsPauseMenu).GetMethod("BuildShell",Flags).Invoke(settings,null);
        typeof(SettingsPauseMenu).GetMethod("BuildSettings",Flags).Invoke(settings,null);
        EditorUtility.SetDirty(storage); EditorUtility.SetDirty(settings);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save settings template.");
        File.WriteAllText(Output+"/setup.txt","Saved editable walking-speed controls and PlayerWalkingSpeed component; existing height/navigation templates preserved.");
    }
}
#endif
