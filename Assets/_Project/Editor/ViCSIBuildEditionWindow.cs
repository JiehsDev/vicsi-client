using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>Selects the complete source project for each edition; never swaps runtime files or Git branches.</summary>
public sealed class ViCSIBuildEditionWindow : EditorWindow
{
    public enum Edition { Research, Competitive }
    const string Prefix = "ViCSI.BuildEdition.";
    const string CompetitiveSource = "Assets/_Project/Scripts/Competitive/CompetitiveEdition.cs";
    static string CurrentProject => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    Edition selected;
    string status;
    double nextPoll;

    [MenuItem("ViCSI/Build Edition...")]
    public static void Open() => GetWindow<ViCSIBuildEditionWindow>("ViCSI Build Edition").Show();

    void OnEnable()
    {
        minSize = new Vector2(620, 540);
        selected = (Edition)EditorPrefs.GetInt(Prefix + "Selected", (int)CurrentEdition);
        EditorApplication.update += Poll;
    }
    void OnDisable() => EditorApplication.update -= Poll;
    static Edition CurrentEdition => File.Exists(Path.Combine(CurrentProject, CompetitiveSource)) ? Edition.Competitive : Edition.Research;
    static string Package(Edition edition) => edition == Edition.Research ? "ph.edu.parsu.vicsi" : "ph.edu.parsu.vicsi.competitive";
    static string ProjectPath(Edition edition)
    {
        string fallback = edition == CurrentEdition ? CurrentProject : Path.Combine(
            Directory.GetParent(CurrentProject).FullName, edition == Edition.Research ? "My project (1)" : "ViCSI Competitive");
        return EditorPrefs.GetString(Prefix + edition + ".Path", fallback);
    }
    static bool SamePath(string a, string b) => string.Equals(Path.GetFullPath(a).TrimEnd('/', '\\'),
        Path.GetFullPath(b).TrimEnd('/', '\\'), StringComparison.OrdinalIgnoreCase);

    void OnGUI()
    {
        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("Build a ViCSI edition", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Open project: " + CurrentEdition);
        EditorGUILayout.Space();
        EditorGUI.BeginChangeCheck();
        selected = (Edition)GUILayout.Toolbar((int)selected, new[] { "Research", "Competitive" }, GUILayout.Height(32));
        if (EditorGUI.EndChangeCheck()) { EditorPrefs.SetInt(Prefix + "Selected", (int)selected); status = null; }
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(selected == Edition.Research
            ? "Research: participant registration, course/session tools and study workflow."
            : "Competitive: usernames, score and time, animated results and local leaderboards.", MessageType.Info);
        EditorGUILayout.LabelField("Quest application ID", Package(selected));
        EditorGUILayout.LabelField("Build output", "ViCSI-" + selected + ".apk");
        EditorGUILayout.Space();
        string project = ProjectPath(selected);
        EditorGUILayout.LabelField("Source project", EditorStyles.boldLabel);
        EditorGUILayout.SelectableLabel(project, EditorStyles.textField, GUILayout.Height(36));
        if (GUILayout.Button("Choose source project folder..."))
        {
            string chosen = EditorUtility.OpenFolderPanel("Select " + selected + " Unity project", project, "");
            if (!string.IsNullOrEmpty(chosen)) EditorPrefs.SetString(Prefix + selected + ".Path", chosen);
        }
        string problem = ValidateProject(project, selected);
        bool current = SamePath(project, CurrentProject);
        if (problem != null) EditorGUILayout.HelpBox(problem, MessageType.Error);
        else if (!current && IsProjectOpen(project))
            EditorGUILayout.HelpBox("This source project is open in another Unity editor. Build there using ViCSI > Build Edition, or close that editor first.", MessageType.Warning);
        EditorGUILayout.HelpBox("Selection affects this build tool. Play Mode and Unity's standard Build button continue to use the open project's edition. Each edition keeps its own source, settings and saved data.", MessageType.None);
        bool busy = SessionState.GetInt(Prefix + "Process", 0) != 0 || BuildPipeline.isBuildingPlayer;
        using (new EditorGUI.DisabledScope(problem != null || busy || EditorApplication.isCompiling ||
            EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || (!current && IsProjectOpen(project))))
        {
            if (GUILayout.Button("Build " + selected + " Quest APK...", GUILayout.Height(36))) BuildSelected(project);
        }
        using (new EditorGUI.DisabledScope(problem != null || current || busy))
            if (GUILayout.Button("Open " + selected + " project in Unity")) EditorApplication.OpenProject(project);
        if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, MessageType.Info);
        string log = SessionState.GetString(Prefix + "Log", "");
        if (File.Exists(log) && GUILayout.Button("Show build log")) EditorUtility.RevealInFinder(log);
    }

    public static string ValidateProject(string project, Edition edition)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(project) || !Directory.Exists(project)) return "Source project folder was not found. Choose its location above.";
            if (!File.Exists(Path.Combine(project, "ProjectSettings/ProjectVersion.txt"))) return "Choose the Unity project root (the folder containing Assets and ProjectSettings).";
            bool competitive = File.Exists(Path.Combine(project, CompetitiveSource));
            if (competitive != (edition == Edition.Competitive)) return "This folder contains the other edition. Choose the " + edition + " source project.";
            string settings = File.ReadAllText(Path.Combine(project, "ProjectSettings/ProjectSettings.asset"));
            if (!System.Text.RegularExpressions.Regex.IsMatch(settings, @"(?m)^\s+Android:\s*" + System.Text.RegularExpressions.Regex.Escape(Package(edition)) + @"\s*$"))
                return "Android application ID does not match " + Package(edition) + ". Check this source project's Player Settings.";
            string version = File.ReadAllText(Path.Combine(project, "ProjectSettings/ProjectVersion.txt"));
            if (!version.Split('\n').Any(line => line.Trim() == "m_EditorVersion: " + Application.unityVersion)) return "Open this source using its matching Unity editor version before building.";
            if (!File.Exists(Path.Combine(project, "Assets/_Project/Editor/QuestBuild.cs"))) return "The source project is missing QuestBuild.cs.";
            return null;
        }
        catch (Exception error) { return error.Message; }
    }

    static bool IsProjectOpen(string project)
    {
        string file = Path.Combine(project, "Temp/UnityLockfile");
        if (!File.Exists(file)) return false;
        try { using (File.Open(file, FileMode.Open, FileAccess.Read, FileShare.None)) return false; }
        catch (IOException) { return true; }
        catch (UnauthorizedAccessException) { return true; }
    }

    void BuildSelected(string project)
    {
        try
        {
            string error = ValidateProject(project, selected);
            if (error != null) throw new InvalidOperationException(error);
            bool current = SamePath(project, CurrentProject);
            if (!current && IsProjectOpen(project)) throw new InvalidOperationException("Close the selected project's other Unity editor before building here.");
            string folder = Path.Combine(project, "Builds");
            Directory.CreateDirectory(folder);
            string output = EditorUtility.SaveFilePanel("Build " + selected + " Quest APK", folder, "ViCSI-" + selected, "apk");
            if (string.IsNullOrEmpty(output)) return;
            if (current)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                if (PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android) != Package(selected))
                    throw new InvalidOperationException("Loaded Android application ID does not match the selected edition.");
                string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
                if (scenes.Length == 0) throw new InvalidOperationException("No enabled build scenes.");
                if (EditorUserBuildSettings.exportAsGoogleAndroidProject || EditorUserBuildSettings.buildAppBundle)
                    throw new InvalidOperationException("Disable Export Project and Build App Bundle in Android Build Settings to build an APK.");
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = scenes, locationPathName = output, target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android, options = BuildOptions.None });
                status = selected + " build " + report.summary.result + ". Errors: " + report.summary.totalErrors;
                Debug.Log("[ViCSI Build Edition] " + status + " Output: " + output);
                if (report.summary.result == BuildResult.Succeeded) EditorUtility.RevealInFinder(output);
            }
            else
            {
                string log = Path.Combine(folder, "ViCSI-" + selected + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".log");
                var info = new ProcessStartInfo(EditorApplication.applicationPath) {
                    Arguments = "-batchmode -nographics -quit -job-worker-count 2 -projectPath " + Quote(project) +
                        " -buildTarget Android -executeMethod QuestBuild.Build -vicsiOutput " + Quote(output) + " -logFile " + Quote(log),
                    UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
                using (var process = Process.Start(info)) SessionState.SetInt(Prefix + "Process", process.Id);
                SessionState.SetString(Prefix + "Log", log);
                SessionState.SetString(Prefix + "Output", output);
                status = "Building " + selected + " in the background. You can follow progress in the build log.";
            }
        }
        catch (Exception error) { status = error.Message; Debug.LogException(error); }
    }
    static string Quote(string value) => "\"" + value.Replace("\"", "") + "\"";

    void Poll()
    {
        if (EditorApplication.timeSinceStartup < nextPoll) return;
        nextPoll = EditorApplication.timeSinceStartup + 2;
        int pid = SessionState.GetInt(Prefix + "Process", 0);
        if (pid == 0) return;
        try { using (var process = Process.GetProcessById(pid)) { if (!process.HasExited) { Repaint(); return; } } }
        catch (ArgumentException) { }
        SessionState.EraseInt(Prefix + "Process");
        string log = SessionState.GetString(Prefix + "Log", "");
        bool success = File.Exists(log) && File.ReadAllText(log).Contains("[QuestBuild] Result=Succeeded");
        status = success ? "Build succeeded: " + SessionState.GetString(Prefix + "Output", "") : "Build did not succeed. Check the build log for details.";
        Debug.Log("[ViCSI Build Edition] " + status);
        Repaint();
    }
}
