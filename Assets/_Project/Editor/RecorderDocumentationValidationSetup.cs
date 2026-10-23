#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>Queue existing isolated Play diagnostics after preserving the current editor scene.</summary>
[InitializeOnLoad]
public static class RecorderDocumentationValidationSetup
{
    [Serializable] sealed class Request {public string suite,output;}
    static double nextPoll;
    static RecorderDocumentationValidationSetup(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        const string request="Temp/recorder-documentation-validation.json";
        if(EditorApplication.timeSinceStartup<nextPoll)return;nextPoll=EditorApplication.timeSinceStartup+1;
        if(!File.Exists(request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
        if(SessionState.GetBool("ViCSI.Phase5.Pending",false)||File.Exists("Tools/phase5-file-request.json"))return;
        try
        {
            var data=UnityEngine.JsonUtility.FromJson<Request>(File.ReadAllText(request));
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(scene.path!="Assets/_Project/Scenes/Scenarios/CSI_Environment.unity")throw new InvalidOperationException("Open CSI_Environment.");
            const string folder="docs/evidence/recorder-documentation-2026-10-21";Directory.CreateDirectory(folder);
            if(!File.Exists(folder+"/CSI_Environment-before.unity")&&!EditorSceneManager.SaveScene(scene,folder+"/CSI_Environment-before.unity",true))throw new IOException("Scene backup failed.");
            if(!EditorSceneManager.SaveScene(scene))throw new IOException("Scene save failed.");
            File.WriteAllText("Tools/phase5-file-request.json",UnityEngine.JsonUtility.ToJson(data));File.Delete(request);
        }
        catch(Exception error){File.Delete(request);File.WriteAllText("Temp/recorder-documentation-validation-error.txt",error.ToString());UnityEngine.Debug.LogException(error);}
    }
}
#endif
