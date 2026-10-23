#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Update saved comparison labels without rebuilding authored visual objects.</summary>
public static class FingerprintCaseworkSetup
{
    [MenuItem("Tools/ViCSI/Scene Setup/Update Fingerprint Casework Labels")]
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.InvalidOperationException("Use Edit mode.");
        var station=Object.FindAnyObjectByType<FingerprintComparisonStation>(FindObjectsInactive.Include);
        if(station==null)throw new System.InvalidOperationException("Open the investigation scene.");
        const string folder="docs/evidence/fingerprint-comparison-gate-2026-10-21";
        Directory.CreateDirectory(folder);
        if(!File.Exists(folder+"/CSI_Environment-before.unity"))EditorSceneManager.SaveScene(station.gameObject.scene,folder+"/CSI_Environment-before.unity",true);
        Undo.RecordObjects(station.GetComponentsInChildren<TMPro.TMP_Text>(true),"Update fingerprint casework labels");
        station.RefreshCaseworkLabels();
        EditorSceneManager.MarkSceneDirty(station.gameObject.scene);EditorSceneManager.SaveScene(station.gameObject.scene);
    }
}
#endif
