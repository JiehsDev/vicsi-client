// Assets/_Project/Editor/QuestBuild.cs
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Headless Quest build entry point for low-memory machines, where an interactive Editor
/// build can run out of memory uploading textures to the GPU:
///   Unity.exe -batchmode -nographics -quit -projectPath "&lt;project&gt;" -buildTarget Android
///     -executeMethod QuestBuild.Build -vicsiOutput "&lt;path&gt;.apk" -logFile "&lt;log&gt;"
/// Uses the project's current Player Settings and enabled Build Settings scenes unchanged
/// (package id, QuestDev config, XR setup, signing). Exits non-zero on failure.
/// </summary>
public static class QuestBuild
{
    public static void Build()
    {
        string output = ArgValue("-vicsiOutput");
        if (string.IsNullOrEmpty(output))
        {
            Debug.LogError("[QuestBuild] Missing -vicsiOutput <path.apk>.");
            EditorApplication.Exit(2);
            return;
        }

        bool remoteTest = Environment.GetCommandLineArgs().Contains("-vicsiRemoteTest");
        string originalId = PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android);
        string originalName = PlayerSettings.productName;
        if (remoteTest)
        {
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, originalId + ".playtest");
            PlayerSettings.productName = originalName + " Playtest";
        }

        var options = new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = output,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = remoteTest ? BuildOptions.Development : BuildOptions.None,
        };

        Debug.Log($"[QuestBuild] Building {PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android)} " +
            $"({options.scenes.Length} scenes) -> {output}");
        BuildReport report;
        try { report = BuildPipeline.BuildPlayer(options); }
        finally
        {
            if (remoteTest)
            {
                PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, originalId);
                PlayerSettings.productName = originalName;
            }
        }
        BuildSummary summary = report.summary;
        Debug.Log($"[QuestBuild] Result={summary.result} errors={summary.totalErrors} warnings={summary.totalWarnings} " +
            $"size={summary.totalSize} time={summary.totalTime}");
        EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
    }

    private static string ArgValue(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }
}
