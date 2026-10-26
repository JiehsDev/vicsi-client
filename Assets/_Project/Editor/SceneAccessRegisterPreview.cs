#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Visual check with synthetic display data only; never starts a research session.</summary>
[InitializeOnLoad]
public static class SceneAccessRegisterPreview
{
    const string Key = "SceneAccessRegisterPreview.";
    static bool ready, created;
    static int stage;
    static float started;
    static Camera camera;
    static SceneAccessRegisterUI view;
    static SceneAccessRegisterPreview()
    {
        EditorApplication.playModeStateChanged += state => ready = state == PlayModeStateChange.EnteredPlayMode;
        if (SessionState.GetBool(Key + "Pending", false)) EditorApplication.update += Poll;
        if (SessionState.GetBool(Key + "Restore", false)) EditorApplication.update += Restore;
    }
    public static void Run()
    {
        var xr = UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
        SessionState.SetBool(Key + "XR", xr != null && xr.InitManagerOnStart);
        if (xr != null) xr.InitManagerOnStart = false;
        SessionState.SetString(Key + "StartScene", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        EditorSceneManager.playModeStartScene = null;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Key + "Pending", true);
        EditorApplication.update -= Poll; EditorApplication.update += Poll;
        EditorApplication.isPlaying = true;
    }
    static void Poll()
    {
        if (!ready || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            EditorApplication.QueuePlayerLoopUpdate();
            if (!created)
            {
                camera = new GameObject("Register preview camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
                camera.transform.position = new Vector3(0, 0, -2);
                camera.orthographic = true; camera.orthographicSize = .44f;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.025f, .035f, .05f);
                view = new GameObject("Register preview").AddComponent<SceneAccessRegisterUI>();
                view.RenderContents("Alexandra Maria de los Santos - Student Investigator", "IOC", "Initial scene assessment", false,
                    new SceneAccessRegister.Visit[0], new SceneAccessRegister.Transition[0], null);
                view.PanelRect.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                view.PanelRect.gameObject.SetActive(true); view.Layout.Refresh();
                created = true; started = Time.unscaledTime;
            }
            if (Time.unscaledTime - started < .5f) return;
            if (view.Layout.Footer.CanScroll) throw new Exception("Register action footer is clipped.");
            if (stage == 0)
            {
                Capture("Temp/research-register-check-in.png");
                var visits = new List<SceneAccessRegister.Visit>();
                for (int i = 0; i < 8; i++) visits.Add(new SceneAccessRegister.Visit { number = i + 1,
                    roleAtEntry = i < 2 ? "IOC" : "SOCO", purpose = i < 2 ? "Initial scene assessment" : "Forensic scene investigation",
                    timeInMs = i * 90000, timeOutMs = i == 7 ? -1 : i * 90000 + 60000 });
                view.RenderContents("Alexandra Maria de los Santos - Student Investigator", "SOCO", "Forensic scene investigation", true,
                    visits, new SceneAccessRegister.Transition[0], "Preview only");
                view.Layout.Body.Scroll.verticalNormalizedPosition = 1;
                stage = 1; started = Time.unscaledTime; return;
            }
            if (!view.Layout.Body.CanScroll) throw new Exception("Long visit history cannot scroll.");
            Capture("Temp/research-register-visits.png");
            File.WriteAllText("Temp/research-register-preview.txt", "PASS: long student name, check-in and eight-visit views render; history scrolls; footer stays visible. No identity or session data written.");
            Finish();
        }
        catch (Exception error) { Debug.LogException(error); File.WriteAllText("Temp/research-register-preview.txt", error.ToString()); Finish(); }
    }
    static void Capture(string path)
    {
        var render = RenderTexture.GetTemporary(1100, 1100, 24); var previous = RenderTexture.active;
        try
        {
            Canvas.ForceUpdateCanvases(); camera.targetTexture = render; camera.Render(); RenderTexture.active = render;
            var texture = new Texture2D(1100, 1100, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, 1100, 1100), 0, 0); texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG()); UnityEngine.Object.Destroy(texture);
        }
        finally { camera.targetTexture = null; RenderTexture.active = previous; RenderTexture.ReleaseTemporary(render); }
    }
    static void Finish()
    {
        EditorApplication.update -= Poll; SessionState.SetBool(Key + "Pending", false); SessionState.SetBool(Key + "Restore", true);
        EditorApplication.isPlaying = false; EditorApplication.update += Restore;
    }
    static void Restore()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        EditorApplication.update -= Restore; SessionState.SetBool(Key + "Restore", false);
        var xr = UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
        if (xr != null) xr.InitManagerOnStart = SessionState.GetBool(Key + "XR", false);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(Key + "StartScene", ""));
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/Scenarios/CSI_Environment.unity");
    }
}
#endif
