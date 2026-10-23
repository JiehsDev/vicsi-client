#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class CompetitiveValidationRunner
{
    static double deadline;
    static bool attached;
    static bool playReady;
    static double nextHeartbeat;
    static CompetitiveFlowSmokeTest suite;
    static CompetitiveValidationRunner()
    {
        EditorApplication.playModeStateChanged += state => playReady = state == PlayModeStateChange.EnteredPlayMode;
        if (SessionState.GetBool("CompetitiveSmoke.Pending", false))
        { deadline = EditorApplication.timeSinceStartup + 360; EditorApplication.update += Poll; }
    }
    public static void Run()
    {
        var xr = UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
        if (xr != null) xr.InitManagerOnStart = false;
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/Main.unity");
        SessionState.SetBool("CompetitiveSmoke.Pending", true);
        deadline = EditorApplication.timeSinceStartup + 360;
        EditorApplication.update -= Poll; EditorApplication.update += Poll;
        EditorApplication.isPlaying = true;
    }
    static void Poll()
    {
        if (EditorApplication.timeSinceStartup >= nextHeartbeat)
        {
            nextHeartbeat = EditorApplication.timeSinceStartup + 3;
            Directory.CreateDirectory("Temp");
            File.WriteAllText("Temp/competitive-smoke-status.txt", "playing=" + EditorApplication.isPlaying + "; ready=" + playReady
                + "; attached=" + attached + "; scene=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
                + "; edition=" + (CompetitiveEdition.Instance != null) + "; menu=" + (GameObject.Find("Competitive menu") != null));
        }
        if (EditorApplication.timeSinceStartup > deadline) { Finish("FAIL: Competitive smoke test timed out.", 1); return; }
        if (!EditorApplication.isPlaying) return;
        EditorApplication.QueuePlayerLoopUpdate();
        if (!attached)
        {
            if (GameObject.Find("Competitive menu") == null) return;
            attached = true;
            suite = new GameObject("Competitive smoke suite").AddComponent<CompetitiveFlowSmokeTest>();
            UnityEngine.Object.DontDestroyOnLoad(suite.gameObject); suite.Run();
        }
        if (suite != null && suite.IsDone) Finish(suite.Report, suite.Failures == 0 ? 0 : 1);
    }
    static void Finish(string report, int code)
    {
        Directory.CreateDirectory("Temp"); File.WriteAllText("Temp/competitive-smoke.txt", report);
        Debug.Log(report); SessionState.SetBool("CompetitiveSmoke.Pending", false);
        EditorApplication.update -= Poll; EditorApplication.isPlaying = false;
        EditorApplication.delayCall += () => EditorApplication.Exit(code);
    }
}
#endif
