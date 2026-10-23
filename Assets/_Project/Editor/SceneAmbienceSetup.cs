#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class SceneAmbienceSetup
{
    const string Output = "docs/evidence/ambient-audio-2026-10-22";
    const string AudioPath = "Assets/_Project/Audio/Ambience/";
    static SceneAmbienceSetup() { EditorApplication.update += Poll; }
    static void Poll()
    {
        const string flag = "Temp/setup-scene-ambience.flag";
        if (!File.Exists(flag) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(flag);
        try { Apply(); } catch (Exception error) { Directory.CreateDirectory(Output); File.WriteAllText(Output + "/setup-error.txt", error.ToString()); Debug.LogException(error); }
    }
    [MenuItem("Tools/ViCSI/Scene Setup/Install CC0 Scene Ambience")]
    public static void Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != "Assets/_Project/Scenes/Scenarios/CSI_Environment.unity")
            throw new InvalidOperationException("Open CSI_Environment outside Play mode.");
        var cordon = UnityEngine.Object.FindAnyObjectByType<SceneCordon>();
        if (cordon == null) throw new InvalidOperationException("House footprint missing.");
        Directory.CreateDirectory(Output);
        if (!File.Exists(Output + "/CSI_Environment-before.unity") && !EditorSceneManager.SaveScene(scene, Output + "/CSI_Environment-before.unity", true)) throw new IOException("Backup failed.");
        foreach (var path in Directory.GetFiles(AudioPath, "*.wav"))
        {
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            if (importer == null) throw new InvalidOperationException("Import audio first: " + path);
            importer.forceToMono = false;
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.Streaming; settings.preloadAudioData = false;
            settings.compressionFormat = AudioCompressionFormat.Vorbis; settings.quality = .65f;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings = settings; importer.SaveAndReimport();
        }
        var ambience = UnityEngine.Object.FindAnyObjectByType<SceneAmbience>();
        if (ambience == null)
        {
            var root = new GameObject("Scene ambience - CC0 house and yard"); Undo.RegisterCreatedObjectUndo(root, "Install scene ambience");
            ambience = Undo.AddComponent<SceneAmbience>(root);
        }
        var exterior = Source(ambience.transform, "Exterior - residential birds", "yard-residential");
        var interior = Source(ambience.transform, "Interior - quiet room tone", "house-room-tone");
        var wind = Source(ambience.transform, "Exterior - tree rustle", "yard-tree-rustle");
        var exteriorFilter = Filter(exterior); var windFilter = Filter(wind);
        var data = new SerializedObject(ambience);
        Set(data, "houseBoundary", cordon); Set(data, "exterior", exterior); Set(data, "interior", interior); Set(data, "treeRustle", wind);
        Set(data, "exteriorFilter", exteriorFilter); Set(data, "treeFilter", windFilter); data.ApplyModifiedProperties();
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Scene save failed.");
        File.WriteAllText(Output + "/setup.txt", "Saved three editable stereo streaming/Vorbis ambience sources, two exterior low-pass filters, house-footprint crossfade and dialogue/menu ducking. Scene-owned, no colliders, no lobby bootstrap.\n");
    }
    static AudioSource Source(Transform parent, string name, string clip)
    {
        var child = parent.Find(name);
        if (child == null) { var go = new GameObject(name); Undo.RegisterCreatedObjectUndo(go, "Add ambience source"); go.transform.SetParent(parent, false); child = go.transform; }
        var source = child.GetComponent<AudioSource>(); if (source == null) source = Undo.AddComponent<AudioSource>(child.gameObject);
        Undo.RecordObject(source, "Configure ambience source");
        source.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioPath + clip + ".wav");
        if (source.clip == null) throw new InvalidOperationException("Missing ambience clip: " + clip);
        source.playOnAwake = false; source.loop = true; source.spatialBlend = 0; source.dopplerLevel = 0; source.volume = 0;
        source.priority = 200; source.ignoreListenerPause = false; source.reverbZoneMix = 0;
        return source;
    }
    static AudioLowPassFilter Filter(AudioSource source)
    {
        var filter = source.GetComponent<AudioLowPassFilter>(); if (filter == null) filter = Undo.AddComponent<AudioLowPassFilter>(source.gameObject);
        Undo.RecordObject(filter, "Configure exterior filter"); filter.cutoffFrequency = 22000; filter.lowpassResonanceQ = 1; return filter;
    }
    static void Set(SerializedObject data, string field, UnityEngine.Object value) => data.FindProperty(field).objectReferenceValue = value;
}
#endif
