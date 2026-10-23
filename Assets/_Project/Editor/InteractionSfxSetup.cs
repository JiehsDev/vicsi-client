#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Save editable audio sources, clip assignments and the shared library in the current scene.</summary>
[InitializeOnLoad]
public static class InteractionSfxSetup
{
    const string Output="docs/evidence/interaction-sfx-2026-10-22";
    const string AudioPath="Assets/_Project/Audio/InteractionSfx/";
    static InteractionSfxSetup(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        const string request="Temp/setup-interaction-sfx.flag";
        if(!File.Exists(request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
        File.Delete(request);
        try{Apply();}catch(Exception error){Directory.CreateDirectory(Output);File.WriteAllText(Output+"/setup-error.txt",error.ToString());Debug.LogException(error);}
    }
    [MenuItem("Tools/ViCSI/Scene Setup/Install Interaction Sound Effects")]
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play mode first.");
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(scene.path!="Assets/_Project/Scenes/Scenarios/CSI_Environment.unity")throw new InvalidOperationException("Open CSI_Environment.");
        Directory.CreateDirectory(Output);
        if(!File.Exists(Output+"/CSI_Environment-before.unity")&&!EditorSceneManager.SaveScene(scene,Output+"/CSI_Environment-before.unity",true))throw new IOException("Scene backup failed.");
        foreach(string path in Directory.GetFiles(AudioPath,"*.wav"))
        {
            var importer=(AudioImporter)AssetImporter.GetAtPath(path);
            if(importer==null)throw new InvalidOperationException("Import audio first: "+path);
            importer.forceToMono=true;
            var settings=importer.defaultSampleSettings;
            settings.preloadAudioData=true;
            settings.loadType=AudioClipLoadType.DecompressOnLoad;settings.compressionFormat=AudioCompressionFormat.Vorbis;
            settings.quality=.75f;settings.sampleRateSetting=AudioSampleRateSetting.OverrideSampleRate;settings.sampleRateOverride=44100;
            importer.defaultSampleSettings=settings; importer.SaveAndReimport();
        }
        const string libraryPath="Assets/Resources/InteractionSfxLibrary.asset";
        var library=AssetDatabase.LoadAssetAtPath<InteractionSfxLibrary>(libraryPath);
        if(library==null){library=ScriptableObject.CreateInstance<InteractionSfxLibrary>();AssetDatabase.CreateAsset(library,libraryPath);}
        library.select=Clip("ui-select");library.confirm=Clip("ui-confirm");library.blocked=Clip("ui-blocked");
        library.shutter=Clip("camera-shutter");library.switchClick=Clip("flashlight-switch");library.measure=Clip("tape-measure");
        library.chalk=Clip("chalk-contact");library.writing=Clip("pen-writing");library.paper=Clip("paper-package");
        library.page=Clip("paper-page");library.tape=Clip("tape-peel");library.container=Clip("container-close");library.place=Clip("object-place");
        library.concreteSteps=Steps("concrete");library.woodSteps=Steps("wood");library.grassSteps=Steps("grass");EditorUtility.SetDirty(library);
        var player=UnityEngine.Object.FindAnyObjectByType<InteractionSfx>();
        if(player==null){var host=new GameObject("Interaction SFX");Undo.RegisterCreatedObjectUndo(host,"Install interaction sounds");player=host.AddComponent<InteractionSfx>();}
        Set(player,"library",library);
        var ui=Source(player.transform,"UI audio",0,1f);Set(player,"uiSource",ui);
        var playerData=new SerializedObject(player);var voices=playerData.FindProperty("spatialSources");voices.arraySize=8;
        for(int i=0;i<8;i++)voices.GetArrayElementAtIndex(i).objectReferenceValue=Source(player.transform,"Spatial SFX "+(i+1),1,library.physicalVolume);
        playerData.ApplyModifiedPropertiesWithoutUndo();
        var rig=UnityEngine.Object.FindAnyObjectByType<OVRCameraRig>();
        if(rig!=null && rig.GetComponent<InteractionFootsteps>()==null)Undo.AddComponent<InteractionFootsteps>(rig.gameObject);
        foreach(var owner in UnityEngine.Object.FindObjectsByType<InteractionFeedback>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {Set(owner,"confirmClip",library.confirm);Set(owner,"blockedClip",library.blocked);SetFloat(owner,"volume",.16f);}
        Wire<PhotographTool>("shutterAudioSource","shutterClip",library.shutter,.45f);
        Wire<FlashlightTool>("beamAudioSource","beamClickClip",library.switchClick,.25f);
        Wire<ChalkMarkTool>("chalkAudioSource","chalkWritingClip",library.chalk,.16f,true);
        Wire<EvidenceBagTool>("audioSource","insertClip",library.paper,.22f);
        Wire<EvidenceTentTool>("audioSource","tentPlacingClip",library.place,.3f);
        // Only the selected container closure. No speculative swab-contact recording or zipper.
        Wire<SwabTool>("audioSource","containerCloseClip",library.container,.25f);
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);
        if(!EditorSceneManager.SaveScene(scene))throw new IOException("Scene save failed.");
        File.WriteAllText(Output+"/setup.txt","Saved 22 imported clips, editable InteractionSfxLibrary, 8 spatial voices, UI source, footsteps and existing tool clip assignments. No zipper/door/cabinet wiring.");
    }
    static AudioClip Clip(string name)=>AssetDatabase.LoadAssetAtPath<AudioClip>(AudioPath+name+".wav")??throw new InvalidOperationException("Missing clip "+name);
    static AudioClip[] Steps(string surface)=>Enumerable.Range(0,3).Select(i=>Clip("footstep-"+surface+"-"+i)).ToArray();
    static AudioSource Source(Transform parent,string name,float spatial,float volume,bool loop=false)
    {
        var child=parent.Find(name);
        if(child==null){var go=new GameObject(name);Undo.RegisterCreatedObjectUndo(go,"Create editable audio source");go.transform.SetParent(parent,false);child=go.transform;}
        var source=child.GetComponent<AudioSource>();if(source==null)source=Undo.AddComponent<AudioSource>(child.gameObject);
        source.playOnAwake=false;source.loop=loop;source.volume=volume;source.spatialBlend=spatial;source.dopplerLevel=0;
        source.minDistance=.5f;source.maxDistance=10;source.rolloffMode=AudioRolloffMode.Linear;EditorUtility.SetDirty(source);return source;
    }
    static void Wire<T>(string sourceField,string clipField,AudioClip clip,float volume,bool loop=false)where T:MonoBehaviour
    {
        foreach(var owner in UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {Set(owner,sourceField,Source(owner.transform,"Interaction audio - "+clipField,1,volume,loop));Set(owner,clipField,clip);}
    }
    static void Set(UnityEngine.Object target,string field,UnityEngine.Object value)
    {var data=new SerializedObject(target);var property=data.FindProperty(field);if(property==null)throw new InvalidOperationException(target.name+" lacks "+field);property.objectReferenceValue=value;data.ApplyModifiedPropertiesWithoutUndo();}
    static void SetFloat(UnityEngine.Object target,string field,float value)
    {var data=new SerializedObject(target);data.FindProperty(field).floatValue=value;data.ApplyModifiedPropertiesWithoutUndo();}
}
#endif
