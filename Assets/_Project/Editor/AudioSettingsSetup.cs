#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;

[InitializeOnLoad]
public static class AudioSettingsSetup
{
    const string Output = "docs/evidence/audio-settings-2026-10-22";
    const string MixerPath = "Assets/Resources/GameAudioMixer.mixer";
    const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static AudioSettingsSetup() { EditorApplication.update += Poll; }
    static void Poll()
    {
        const string flag = "Temp/setup-audio-settings.flag";
        if (!File.Exists(flag) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(flag);
        try { Apply(); } catch (Exception error) { Directory.CreateDirectory(Output); File.WriteAllText(Output + "/setup-error.txt", error.ToString()); Debug.LogException(error); }
    }
    [MenuItem("Tools/ViCSI/Scene Setup/Install In-Game Audio Settings")]
    public static void Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != "Assets/_Project/Scenes/Scenarios/CSI_Environment.unity") throw new InvalidOperationException("Open CSI_Environment outside Play mode.");
        Directory.CreateDirectory(Output);
        if (!File.Exists(Output + "/CSI_Environment-before.unity")) File.Copy(scene.path, Output + "/CSI_Environment-before.unity");
        CreateMixer();
        var settings = UnityEngine.Object.FindAnyObjectByType<SettingsPauseMenu>();
        if (settings == null) throw new InvalidOperationException("Existing in-game Settings missing.");
        var storage = settings.GetComponent<EditableSceneVisuals>();
        var group = storage.Groups.First(g => g.owner == settings && g.builder == "BuildSettings");
        // Recover a partially applied template migration without retaining duplicate navigation controls.
        if (group.objects.Count > 48 && group.objects.Any(go => go == null))
        {
            var originalNavigation = group.objects.Skip(group.objects.Count - 8).ToArray();
            if (originalNavigation[0] != null && originalNavigation[0].name == "restart"
                && originalNavigation[7] != null && originalNavigation[7].name == "HintContainer")
            {
                var replacements = group.objects.Skip(40).Take(8).ToArray();
                group.objects.RemoveRange(40, group.objects.Count - 40); group.objects.AddRange(originalNavigation);
                foreach (var go in replacements) if (go != null && !originalNavigation.Contains(go)) Undo.DestroyObjectImmediate(go);
            }
        }
        if (!group.objects.Any(go => go != null && go.name == "ambience-title"))
        {
            int restart = group.objects.FindIndex(go => go != null && go.name == "restart");
            if (restart < 0) throw new InvalidOperationException("Settings template restart control missing.");
            Undo.RecordObject(storage, "Add in-game audio settings rows");
            // Reuse the migration safely after an interrupted editor refresh.
            while (restart > 0 && group.objects[restart - 1] == null) group.objects.RemoveAt(--restart);
            group.objects.InsertRange(restart, new GameObject[18]);
        }
        // Rebind runtime references after a script reload without rebuilding unrelated scene visuals.
        typeof(SettingsPauseMenu).GetField("walkingSpeed", Instance).SetValue(settings, UnityEngine.Object.FindAnyObjectByType<PlayerWalkingSpeed>());
        typeof(SettingsPauseMenu).GetField("height", Instance).SetValue(settings, UnityEngine.Object.FindAnyObjectByType<PlayerHeightOffset>());
        // Unity hot reload can preserve the panel reference while dropping its non-serialized pointer/layout bindings.
        typeof(SettingsPauseMenu).GetMethod("BuildShell", Instance).Invoke(settings, null);
        typeof(SettingsPauseMenu).GetMethod("BuildSettings", Instance).Invoke(settings, null);
        var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
        foreach (var source in UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include))
        {
            var bus = mixer.FindMatchingGroups(source.GetComponentInParent<SceneAmbience>() != null ? "Ambience" : "SFX").Single();
            Undo.RecordObject(source, "Route volume controls"); source.outputAudioMixerGroup = bus;
            PrefabUtility.RecordPrefabInstancePropertyModifications(source);
        }
        File.WriteAllText(Output + "/settings-transform-ids.json", JsonUtility.ToJson(new Ids { ids = settings.GetComponentsInChildren<RectTransform>(true)
            .Select(t => GlobalObjectId.GetGlobalObjectIdSlow(t).targetObjectId.ToString()).ToArray() }));
        EditorUtility.SetDirty(storage); EditorUtility.SetDirty(settings); AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save audio controls.");
        File.WriteAllText(Output + "/setup.txt", "Saved editable Ambience Volume / SFX Volume +/- rows and Reset Sound Volumes in existing Settings. Separate exposed mixer buses; authored and runtime source routing, saved headset preferences.\n");
    }
    [Serializable] sealed class Ids { public string[] ids; }
    static Type EditorType(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Instance).Invoke(target, args);
    static void CreateMixer()
    {
        var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
        var type = EditorType("UnityEditor.Audio.AudioMixerController");
        if (mixer == null) mixer = (AudioMixer)type.GetMethod("CreateMixerControllerAtPath", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Invoke(null, new object[] { MixerPath });
        var master = type.GetProperty("masterGroup", Instance).GetValue(mixer);
        foreach (var pair in new[] { ("Ambience", GameAudioSettings.AmbienceParameter), ("SFX", GameAudioSettings.SfxParameter) })
        {
            object group = mixer.FindMatchingGroups(pair.Item1).FirstOrDefault();
            if (group == null) { group = Call(mixer, "CreateNewGroup", pair.Item1, false); Call(mixer, "AddChildToParent", group, master); }
            var guid = Call(group, "GetGUIDForVolume");
            if (!(bool)Call(mixer, "ContainsExposedParameter", guid))
            {
                var path = Activator.CreateInstance(EditorType("UnityEditor.Audio.AudioGroupParameterPath"), Instance, null, new[] { group, guid }, null);
                Call(mixer, "AddExposedParameter", path);
            }
            var property = type.GetProperty("exposedParameters", Instance);
            var parameters = (Array)property.GetValue(mixer);
            for (int i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters.GetValue(i); var parameterType = parameter.GetType();
                if (!parameterType.GetField("guid", Instance).GetValue(parameter).Equals(guid)) continue;
                parameterType.GetField("name", Instance).SetValue(parameter, pair.Item2); parameters.SetValue(parameter, i);
            }
            property.SetValue(mixer, parameters);
        }
        EditorUtility.SetDirty(mixer); AssetDatabase.SaveAssets();
    }
}
#endif
