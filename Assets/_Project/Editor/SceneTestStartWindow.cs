#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Local Editor selection; never writes a test preset into the saved scene.</summary>
public sealed class SceneTestStartWindow : EditorWindow
{
    private const string ScenePath = "Assets/_Project/Scenes/Scenarios/CSI_Environment.unity";
    private static readonly string[] Labels = Array.ConvertAll(
        SceneTestStart.MenuOrder, p => Group(p) + "/" + SceneTestStart.Label(p));

    private static string Group(SceneTestStartingPoint point) => point switch
    {
        SceneTestStartingPoint.NormalPlay or SceneTestStartingPoint.IOCDone => "Arrival and scene",
        SceneTestStartingPoint.KnifeScalePhotography or SceneTestStartingPoint.BloodstainScalePhotography
            or SceneTestStartingPoint.BottleScalePhotography => "Evidence photography",
        SceneTestStartingPoint.PhotographyDone or SceneTestStartingPoint.AllEvidenceMeasured
            or SceneTestStartingPoint.BodyRecording or SceneTestStartingPoint.BottleFingerprinting
            or SceneTestStartingPoint.KnifeBagging => "Evidence procedures",
        SceneTestStartingPoint.FingerprintComparison => "Practice station",
        _ => "Measurements and completion"
    };

    [MenuItem("Tools/ViCSI/Scene Test Start")]
    public static void Open() => GetWindow<SceneTestStartWindow>("Scene Test Start");

    private void OnInspectorUpdate() => Repaint();
    private void OnGUI()
    {
        GUILayout.Label("Start at a major procedure", EditorStyles.boldLabel);
        int selected = SessionState.GetInt(SceneTestStart.EditorSelectionKey, 0);
        int selectedIndex = Array.IndexOf(SceneTestStart.MenuOrder, (SceneTestStartingPoint)selected);
        int next = EditorGUILayout.Popup("Starting point", Mathf.Max(0, selectedIndex), Labels);
        var point = SceneTestStart.MenuOrder[next];
        SessionState.SetInt(SceneTestStart.EditorSelectionKey, (int)point);
        EditorGUILayout.HelpBox("Next: " + SceneTestStart.NextActivity(point), MessageType.Info);
        EditorGUILayout.LabelField("Selection lasts for this Editor session. Normal play is the default when Unity opens.", EditorStyles.wordWrappedLabel);
        EditorGUILayout.Space();
        var live = SceneTestStart.Instance;
        if (EditorApplication.isPlaying)
        {
            if (live != null && SceneTestStart.IsActive)
            {
                EditorGUILayout.LabelField("Active test", SceneTestStart.Label(live.ActivePoint));
                EditorGUILayout.LabelField("State", live.IsReady ? "Ready" : live.Failure ?? "Preparing...");
                EditorGUILayout.HelpBox("Test session: student assessment uploads are disabled.", MessageType.Info);
            }
            using (new EditorGUI.DisabledScope(live == null || !SceneTestStart.IsActive || SceneTestStart.IsPreparing
                || point == SceneTestStartingPoint.NormalPlay))
            {
                if (GUILayout.Button("Restart selected test")) live.RestartSelectedTest();
            }
            if (!SceneTestStart.IsActive || point == SceneTestStartingPoint.NormalPlay)
                EditorGUILayout.HelpBox("Stop Play, then use Play selected start to change between normal play and test mode.", MessageType.None);
        }
        else
        {
            using (new EditorGUI.DisabledScope(EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("Play selected start"))
                {
                    if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                    if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != ScenePath)
                        EditorSceneManager.OpenScene(ScenePath);
                    EditorApplication.isPlaying = true;
                }
            }
        }
        if (GUILayout.Button("Select scene test settings"))
        {
            var settings = UnityEngine.Object.FindFirstObjectByType<SceneTestStart>(FindObjectsInactive.Include);
            if (settings != null) Selection.activeGameObject = settings.gameObject;
        }
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("For development builds, set the SceneTestStart component on _DevelopmentTesting in the Inspector. Release builds always use normal play.", EditorStyles.wordWrappedLabel);
    }
}
#endif
