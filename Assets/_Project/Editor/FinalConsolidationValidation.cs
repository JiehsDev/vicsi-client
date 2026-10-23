#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Read-only validation of saved build scenes. Never saves or migrates the environment.</summary>
public static class FinalConsolidationValidation
{
    [Serializable] private class SceneResult
    {
        public string scene;
        public int activeMissingScripts, inactiveMissingScripts;
        public List<string> brokenReferences = new();
    }
    [Serializable] private class Result
    {
        public bool compilationFailed;
        public List<SceneResult> scenes = new();
        public bool newHouseActive, oldHouseInactive, currentEvidencePresent, sceneLeftClean;
        public string measurementLayout;
        public string geometryReport;
    }
    public static void Run()
    {
        const string output = "docs/evidence/final-consolidation";
        Directory.CreateDirectory(output);
        var result = new Result { compilationFailed = EditorUtility.scriptCompilationFailed };
        foreach (var entry in EditorBuildSettings.scenes.Where(s => s.enabled))
        {
            var scene = EditorSceneManager.OpenScene(entry.path);
            var row = new SceneResult { scene = entry.path };
            foreach (var root in scene.GetRootGameObjects()) foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                if (t.gameObject.activeInHierarchy) row.activeMissingScripts += missing;
                else row.inactiveMissingScripts += missing;
                foreach (var component in t.GetComponents<Component>().Where(c => c != null))
                {
                    var serialized = new SerializedObject(component);
                    var property = serialized.GetIterator();
                    while (property.Next(true)) if (property.propertyType == SerializedPropertyType.ObjectReference
                        && property.objectReferenceValue == null && !property.objectReferenceEntityIdValue.Equals(EntityId.None))
                        row.brokenReferences.Add(t.name + "/" + component.GetType().Name + "/" + property.propertyPath);
                }
            }
            result.scenes.Add(row);
        }
        var investigation = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Scenarios/CSI_Environment.unity");
        var roots = investigation.GetRootGameObjects();
        result.newHouseActive = roots.Any(g => g.name == "Vicsi_House" && g.activeInHierarchy);
        result.oldHouseInactive = roots.Any(g => g.name == "Harong" && !g.activeSelf);
        var evidence = UnityEngine.Object.FindObjectsByType<EvidenceProp>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        result.currentEvidencePresent = new[] { "BODY-A", "EVD-014", "EVD-015", "EVD-018" }.All(id => evidence.Count(p => p.evidenceId == id) == 1);
        var manager = UnityEngine.Object.FindAnyObjectByType<MeasurementStateManager>();
        result.measurementLayout = new SerializedObject(manager).FindProperty("measurementLayoutId").stringValue;
        string geometryPath = NewHouseInvestigationSetup.Evidence + "/layout-validation.txt";
        if (File.Exists(geometryPath)) File.Copy(geometryPath, output + "/prior-layout-validation.txt", true);
        NewHouseInvestigationValidation.Validate();
        result.geometryReport = File.ReadAllText(geometryPath);
        File.WriteAllText(output + "/layout-validation.txt", result.geometryReport);
        result.sceneLeftClean = !investigation.isDirty;
        File.WriteAllText(output + "/scene-validation.json", JsonUtility.ToJson(result, true));
        bool passed = !result.compilationFailed && result.scenes.All(s => s.activeMissingScripts == 0 && s.brokenReferences.Count == 0)
            && result.newHouseActive && result.oldHouseInactive && result.currentEvidencePresent && result.measurementLayout == "new-house-v3"
            && result.geometryReport.Contains("failures=0") && result.sceneLeftClean;
        EditorApplication.Exit(passed ? 0 : 1);
    }
}
#endif
