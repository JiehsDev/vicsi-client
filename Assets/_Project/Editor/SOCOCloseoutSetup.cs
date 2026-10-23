using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;

/// <summary>Persists closeout scene references, utility access and unscored objectives.</summary>
public static class SOCOCloseoutSetup
{
    [MenuItem("ViCSI/Scene Setup/Connect SOCO Closeout")]
    public static void Connect()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling) throw new InvalidOperationException("Connect closeout outside Play mode.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != "Assets/_Project/Scenes/Scenarios/CSI_Environment.unity" || scene.isDirty)
            throw new InvalidOperationException("Open the clean saved CSI scene before connecting closeout.");
        var ending = UnityEngine.Object.FindFirstObjectByType<EndingManager>();
        var controller = ending.GetComponent<SOCOCloseout>() ?? ending.gameObject.AddComponent<SOCOCloseout>();
        var required = new[] { "BODY-A", "EVD-014", "EVD-015", "EVD-018" };
        var definitions = AssetDatabase.FindAssets("t:EvidenceDefinition").Select(g => AssetDatabase.LoadAssetAtPath<EvidenceDefinition>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(d => required.Contains(d.evidenceId)).ToArray();
        var serialized = new SerializedObject(controller);
        var evidence = serialized.FindProperty("requiredEvidence"); evidence.arraySize = required.Length;
        for (int i = 0; i < required.Length; i++) evidence.GetArrayElementAtIndex(i).objectReferenceValue = definitions.Single(d => d.evidenceId == required[i]);
        serialized.FindProperty("closedBagPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Data/Evidence/HeldPlaceholders/EvidencePaperBagClosed.prefab");
        serialized.FindProperty("completedSamplePrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Data/Evidence/HeldPlaceholders/EVD-015_SealedSwabContainer.prefab");
        serialized.FindProperty("ioc").objectReferenceValue = UnityEngine.Object.FindObjectsByType<NPCPresentation>(FindObjectsInactive.Include).Single(n => n.CharacterRole == NPCPresentation.Role.IOC).transform;
        serialized.FindProperty("cordon").objectReferenceValue = UnityEngine.Object.FindFirstObjectByType<SceneCordon>();
        serialized.ApplyModifiedPropertiesWithoutUndo();
        var menu = UnityEngine.Object.FindFirstObjectByType<UtilityMenuController>();
        var menuObject = new SerializedObject(menu); var entries = menuObject.FindProperty("entries");
        int index = -1;
        for (int i = 0; i < entries.arraySize; i++) if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("displayName").stringValue == "Closeout Inventory") index = i;
        if (index < 0) { index = entries.arraySize; entries.arraySize++; }
        var entry = entries.GetArrayElementAtIndex(index);
        entry.FindPropertyRelative("displayName").stringValue = "Closeout Inventory";
        entry.FindPropertyRelative("description").stringValue = "Review evidence accountability before scene release.";
        entry.FindPropertyRelative("iocAssessmentOnly").boolValue = false;
        entry.FindPropertyRelative("socoCloseoutOnly").boolValue = true;
        // A duplicated serialized array element can carry old listeners. Replace the event explicitly.
        entry.FindPropertyRelative("onSelect.m_PersistentCalls.m_Calls").arraySize = 0;
        menuObject.ApplyModifiedPropertiesWithoutUndo();
        var field = typeof(UtilityMenuController).GetField("entries", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var actualEntries = (List<UtilityMenuController.MenuEntry>)field.GetValue(menu);
        actualEntries[index].onSelect = new UnityEvent();
        UnityEventTools.AddPersistentListener(actualEntries[index].onSelect, controller.RequestInventoryReview);
        EditorUtility.SetDirty(menu);
        var objectives = AssetDatabase.LoadAssetAtPath<ObjectiveSet>("Assets/_Project/Data/Objectives/Objectives_CSI_Environment.asset");
        AddObjective(objectives, "obj-soco-closeout", ScenarioPhase.SOCOCloseout, ScenarioPhase.EvidenceInventoryReview, "closeout-objective-processing");
        AddObjective(objectives, "obj-evidence-inventory-review", ScenarioPhase.EvidenceInventoryReview, ScenarioPhase.FinalSceneSurvey, "closeout-objective-inventory");
        AddObjective(objectives, "obj-final-scene-survey", ScenarioPhase.FinalSceneSurvey, ScenarioPhase.IOCCompletionReport, "closeout-objective-survey");
        AddObjective(objectives, "obj-ioc-completion-report", ScenarioPhase.IOCCompletionReport, ScenarioPhase.SceneRelease, "closeout-objective-report");
        EditorUtility.SetDirty(objectives); AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
    }
    private static void AddObjective(ObjectiveSet set, string id, ScenarioPhase phase, ScenarioPhase complete, string title)
    {
        var objective = set.objectives.FirstOrDefault(o => o.objectiveId == id);
        if (objective == null) { objective = new Objective { objectiveId = id }; set.objectives.Add(objective); }
        objective.phaseScope = ObjectivePhaseScope.ExactPhase; objective.requiredPhase = phase;
        objective.title = GameDialogue.Text(title); objective.excludeFromScoring = true;
        objective.completionRequirement = new RequirementNode { kind = RequirementNodeKind.Leaf, requirementType = RequirementType.ScenarioPhaseReached, scenarioPhase = complete };
    }
}
