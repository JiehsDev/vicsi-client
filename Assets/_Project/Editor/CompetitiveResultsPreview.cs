#if UNITY_EDITOR
using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Isolated visual preview; no case completion, player history or gameplay fixtures.</summary>
[InitializeOnLoad]
public static class CompetitiveResultsPreview
{
    static Camera camera;
    static CompetitiveResultsReveal view;
    static double started;
    static bool created;
    static bool playReady;
    static CompetitiveRunRecord run;
    static SceneAccessRegisterUI registerView;
    static bool registerPreview;
    static int registerStage;
    static CompetitiveResultsPreview()
    {
        EditorApplication.playModeStateChanged += state => playReady = state == PlayModeStateChange.EnteredPlayMode;
        if (SessionState.GetBool("CompetitiveResultsPreview.Pending", false)) EditorApplication.update += Poll;
    }

    public static void Run()
    {
        var xr = UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
        SessionState.SetBool("CompetitiveResultsPreview.XR", xr != null && xr.InitManagerOnStart);
        if (xr != null) xr.InitManagerOnStart = false;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool("CompetitiveResultsPreview.Pending", true);
        EditorApplication.update -= Poll; EditorApplication.update += Poll;
        EditorApplication.isPlaying = true;
    }

    public static void RunRegister()
    {
        SessionState.SetBool("CompetitiveResultsPreview.Register", true);
        Run();
    }

    static void Poll()
    {
        if (!EditorApplication.isPlaying || !playReady || EditorApplication.isCompiling) return;
        try
        {
            EditorApplication.QueuePlayerLoopUpdate();
            if (!created)
            {
                registerPreview = SessionState.GetBool("CompetitiveResultsPreview.Register", false);
                if (registerPreview) CreateRegister(); else Create();
                created = true; started = Time.unscaledTime;
            }
            if (registerPreview) { CheckRegister(); return; }
            if (view.IsRevealing || Time.unscaledTime - started < .5f) return;
            Capture("Temp/competitive-results-preview.png");
            var geometry = new System.Text.StringBuilder();
            foreach (var rect in view.GetComponentsInChildren<RectTransform>(true))
                geometry.AppendLine(rect.name + " active=" + rect.gameObject.activeInHierarchy + " size=" + rect.rect.size + " world=" + rect.position + " scale=" + rect.lossyScale
                    + " cull=" + rect.GetComponent<CanvasRenderer>()?.cull);
            File.WriteAllText("Temp/competitive-results-geometry.txt", geometry.ToString());
            if (view.transform.Find("Rank").GetComponent<TMP_Text>().text != "Leaderboard rank  #4 of 7")
                throw new Exception("Incorrect final preview rank: " + view.transform.Find("Rank").GetComponent<TMP_Text>().text);
            if (view.transform.Find("Score").GetComponent<TMP_Text>().text != "82 / 100")
                throw new Exception("Score animation did not finish");
            view.gameObject.SetActive(false);
            var empty = new CompetitiveRunHistory(); empty.runs.Add(run);
            if (!view.Show(run, CompetitiveRanking.Leaderboard(empty, run.rulesVersion), false, "Preview only"))
                throw new Exception("Single-player leaderboard did not display");
            if (view.transform.Find("Rank").GetComponent<TMP_Text>().text != "Leaderboard rank  #1 of 1")
                throw new Exception("Reopened preview did not settle immediately");
            File.WriteAllText("Temp/competitive-results-preview.txt", "PASS: animated score and rank settle; demo rank #4 of 7; demo-off rank #1 of 1; reopening is immediate. No history saved.");
            Finish(0);
        }
        catch (Exception error)
        { Debug.LogException(error); File.WriteAllText("Temp/competitive-results-preview.txt", error.ToString()); Finish(1); }
    }

    static void Create()
    {
        camera = new GameObject("Preview camera", typeof(Camera)).GetComponent<Camera>();
        camera.transform.position = new Vector3(0, 0, -2);
        camera.orthographic = true; camera.orthographicSize = .43f;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.025f, .035f, .05f);
        var root = new GameObject("Preview panel", typeof(RectTransform), typeof(Canvas));
        var rect = (RectTransform)root.transform; rect.sizeDelta = new Vector2(760, 760); rect.localScale = Vector3.one * .001f;
        root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        var background = ForensicUIStyle.Rect(rect, "Background", ForensicUIStyle.PanelBackground).rectTransform;
        background.anchorMin = Vector2.zero; background.anchorMax = Vector2.one; background.offsetMin = background.offsetMax = Vector2.zero;
        var heading = ForensicUIStyle.Text(rect, "Heading", 36, ForensicUIStyle.TextPrimary); heading.text = "Run results";
        var footer = ForensicUIStyle.Text(rect, "Footer", 23, ForensicUIStyle.TextSecondary); footer.text = "[A] Next   |   Right stick: scroll";
        var layout = ResponsivePanelLayout.Create(rect, heading, null, footer, 760, 760, 40);
        view = CompetitiveResultsReveal.Create(layout);
        run = new CompetitiveRunRecord { runId = Guid.NewGuid().ToString(), playerId = Guid.NewGuid().ToString(),
            username = "Detective Lee", score = 82, seconds = 1327, recorderPoints = 20,
            completedUtc = DateTime.UtcNow.ToString("O"), rulesVersion = "preview" };
        var history = new CompetitiveRunHistory(); history.runs.Add(run);
        if (!view.Show(run, CompetitiveRanking.Leaderboard(history, "preview", true), true, "Preview only — not saved."))
            throw new Exception("Could not display result preview");
        layout.Refresh();
    }

    static void CreateRegister()
    {
        camera = new GameObject("Register preview camera", typeof(Camera)).GetComponent<Camera>();
        camera.transform.position = new Vector3(0, 0, -2);
        camera.orthographic = true; camera.orthographicSize = .44f;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.025f, .035f, .05f);
        registerView = new GameObject("Register UI preview").AddComponent<SceneAccessRegisterUI>();
        registerView.RenderContents("Detective Lee", "IOC", "Initial scene assessment", false,
            new SceneAccessRegister.Visit[0], new SceneAccessRegister.Transition[0], null);
        registerView.PanelRect.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        registerView.PanelRect.gameObject.SetActive(true);
        registerView.Layout.Refresh();
    }

    static void CheckRegister()
    {
        if (Time.unscaledTime - started < .5f) return;
        if (registerView.Layout.Footer.CanScroll) throw new Exception("Register action footer is clipped.");
        if (registerStage == 0)
        {
            Capture("Temp/register-check-in-preview.png");
            var visits = new System.Collections.Generic.List<SceneAccessRegister.Visit>();
            for (int i = 0; i < 8; i++) visits.Add(new SceneAccessRegister.Visit { number = i + 1,
                roleAtEntry = i < 2 ? "IOC" : "SOCO", purpose = i < 2 ? "Initial scene assessment" : "Forensic scene investigation",
                timeInMs = i * 90000, timeOutMs = i == 7 ? -1 : i * 90000 + 60000 });
            registerView.RenderContents("Detective Lee", "SOCO", "Forensic scene investigation", true,
                visits, new SceneAccessRegister.Transition[0], "Preview save failure");
            registerView.Layout.Body.Scroll.verticalNormalizedPosition = 1;
            registerStage = 1; started = Time.unscaledTime; return;
        }
        if (!registerView.Layout.Body.CanScroll) throw new Exception("Long register history does not scroll.");
        Capture("Temp/register-visits-preview.png");
        File.WriteAllText("Temp/register-preview.txt", "PASS: check-in and eight-visit views render; long history scrolls; footer actions remain visible; no gameplay or history changed.");
        Finish(0);
    }

    static void Capture(string path)
    {
        var render = RenderTexture.GetTemporary(1100, 1100, 24);
        var previous = RenderTexture.active;
        try
        {
            Canvas.ForceUpdateCanvases();
            camera.targetTexture = render; camera.Render(); RenderTexture.active = render;
            var texture = new Texture2D(1100, 1100, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, 1100, 1100), 0, 0); texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG()); UnityEngine.Object.Destroy(texture);
        }
        finally { camera.targetTexture = null; RenderTexture.active = previous; RenderTexture.ReleaseTemporary(render); }
    }

    static void Finish(int exitCode)
    {
        EditorApplication.update -= Poll;
        SessionState.SetBool("CompetitiveResultsPreview.Pending", false);
        SessionState.SetBool("CompetitiveResultsPreview.Register", false);
        EditorApplication.isPlaying = false;
        EditorApplication.delayCall += () =>
        {
            var xr = UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
            if (xr != null) xr.InitManagerOnStart = SessionState.GetBool("CompetitiveResultsPreview.XR", false);
            if (Application.isBatchMode) EditorApplication.Exit(exitCode);
            else EditorSceneManager.OpenScene("Assets/_Project/Scenes/Main.unity");
        };
    }
}
#endif
