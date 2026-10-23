#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Update the saved drying sign without rebuilding editable props.</summary>
[InitializeOnLoad]
public static class BloodEvidenceCaseworkSetup
{
    const string Output="docs/evidence/blood-drying-wording-2026-10-21";
    static BloodEvidenceCaseworkSetup(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        const string request="Temp/blood-casework-request.txt";
        if(!File.Exists(request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
        File.Delete(request);
        try{Apply();}catch(Exception error){Directory.CreateDirectory(Output);File.WriteAllText(Output+"/setup-error.txt",error.ToString());Debug.LogException(error);}
    }
    [MenuItem("Tools/ViCSI/Scene Setup/Update Blood Evidence Drying Labels")]
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Use Edit mode.");
        var station=UnityEngine.Object.FindAnyObjectByType<SamplePreparationStation>(FindObjectsInactive.Include);
        if(station==null)throw new InvalidOperationException("Open the investigation scene.");
        Directory.CreateDirectory(Output);
        var scene=station.gameObject.scene;
        if(!File.Exists(Output+"/CSI_Environment-before.unity"))EditorSceneManager.SaveScene(scene,Output+"/CSI_Environment-before.unity",true);
        var label=station.GetComponentsInChildren<Text>(true).Single(text=>text.name=="Instruction");
        Undo.RecordObject(label,"Update blood evidence drying wording");
        label.text="BLOOD EVIDENCE DRYING\n"+GameDialogue.Text("sample-station-idle");
        EditorUtility.SetDirty(label);EditorSceneManager.MarkSceneDirty(scene);
        if(!EditorSceneManager.SaveScene(scene))throw new IOException("Could not save drying labels.");
        File.WriteAllText(Output+"/authoring.txt","Saved blood evidence drying sign; existing props and bindings retained.\n"+label.text);
    }
}
#endif
