// Assets/Editor/ScenarioGroundTruthExporter.cs
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Writes a scenario's evidence relevance classification out as a plain JSON file, so
/// an external reader can score a session without needing the ScriptableObjects, the
/// Unity project, or a running editor.
///
/// The roster comes from EvidenceStateManager.sceneEvidenceDefinitions in the open
/// scene - the same list the game itself registers at runtime - rather than from a
/// second hand-maintained list. There is deliberately no way to author ground truth
/// separately from the scenario data: a divergence between "what the scene contains"
/// and "what the scorer was told the scene contains" would be silent and would corrupt
/// every number downstream.
///
/// Regenerate this whenever an EvidenceRelevance value changes. It is an export, not a
/// source: never edit the JSON by hand.
/// </summary>
public static class ScenarioGroundTruthExporter
{
    private const string OutputFolder = "Assets/_Project/Data/Scenarios";

    [MenuItem("Tools/VICSI/Export Scenario Ground Truth")]
    public static void Export()
    {
        var manager = Object.FindFirstObjectByType<EvidenceStateManager>();
        if (manager == null)
        {
            EditorUtility.DisplayDialog(
                "Export Scenario Ground Truth",
                "No EvidenceStateManager found in the open scene. Open the scenario scene you want to export and try again.",
                "OK");
            return;
        }

        // sceneEvidenceDefinitions is a private [SerializeField]; SerializedObject is
        // the supported way to read it without widening the runtime API purely for the
        // benefit of an editor tool.
        var serialized = new SerializedObject(manager);
        var listProperty = serialized.FindProperty("sceneEvidenceDefinitions");
        if (listProperty == null || !listProperty.isArray)
        {
            Debug.LogError("[ScenarioGroundTruthExporter] Could not read sceneEvidenceDefinitions from EvidenceStateManager.");
            return;
        }

        string scenarioId = manager.gameObject.scene.name;
        var entries = new List<EvidenceDefinition>();
        var seenIds = new HashSet<string>();
        int skipped = 0;

        for (int i = 0; i < listProperty.arraySize; i++)
        {
            var def = listProperty.GetArrayElementAtIndex(i).objectReferenceValue as EvidenceDefinition;

            if (def == null)
            {
                Debug.LogWarning($"[ScenarioGroundTruthExporter] Entry {i} in '{scenarioId}' is an empty slot; skipping.");
                skipped++;
                continue;
            }

            if (string.IsNullOrEmpty(def.evidenceId))
            {
                Debug.LogWarning($"[ScenarioGroundTruthExporter] '{def.name}' has no evidenceId; skipping — it could never be joined to a session event anyway.");
                skipped++;
                continue;
            }

            if (!seenIds.Add(def.evidenceId))
            {
                // A duplicate id would double-count in every denominator downstream.
                Debug.LogError($"[ScenarioGroundTruthExporter] Duplicate evidenceId '{def.evidenceId}' in '{scenarioId}'. Export aborted — fix the roster first.");
                return;
            }

            entries.Add(def);
        }

        if (entries.Count == 0)
        {
            Debug.LogError($"[ScenarioGroundTruthExporter] '{scenarioId}' produced no usable evidence entries; nothing written.");
            return;
        }

        var lifecycleSequence = ReadCanonicalSequence();
        if (lifecycleSequence == null)
        {
            // Loud, not silent: an export missing the sequence would push the scorer
            // straight back onto a hardcoded copy, which is the bug this exists to close.
            Debug.LogError("[ScenarioGroundTruthExporter] Could not read EvidenceStateManager.RequiredSequence; nothing written. If that field was renamed, update this exporter to match.");
            return;
        }

        string json = BuildJson(scenarioId, entries, lifecycleSequence);
        string path = $"{OutputFolder}/GroundTruth_{scenarioId}.json";

        if (!AssetDatabase.IsValidFolder(OutputFolder))
        {
            Debug.LogError($"[ScenarioGroundTruthExporter] Output folder '{OutputFolder}' does not exist.");
            return;
        }

        System.IO.File.WriteAllText(path, json, new UTF8Encoding(false));
        AssetDatabase.ImportAsset(path);

        Debug.Log($"[ScenarioGroundTruthExporter] Wrote {entries.Count} evidence entries for '{scenarioId}' to {path}" + (skipped > 0 ? $" ({skipped} skipped)." : "."));
        EditorUtility.RevealInFinder(path);
    }

    /// <summary>
    /// Reads the game's canonical lifecycle order out of
    /// EvidenceStateManager.RequiredSequence, which is the single structure every
    /// ordering decision in the game consults (IsValidNextStep, GetNextRequiredStatus,
    /// CountAtOrAbove, TryReclaimMarker, SetStatus).
    ///
    /// Deliberately NOT Enum.GetValues(typeof(EvidenceStatus)): the enum's declaration
    /// order is explicitly not the authority here - RequiredSequence's own comment says
    /// it is written independently so that a reorder of the enum cannot silently change
    /// gating. Reflecting over the enum would therefore create exactly the second
    /// source of truth this export exists to eliminate, and would agree with the real
    /// one only by coincidence.
    ///
    /// Reflection is used because the field is private, and widening the runtime API
    /// purely for an editor tool would be a gameplay change. The trade-off is that a
    /// rename isn't caught by the compiler - hence the caller's hard failure rather
    /// than a fallback.
    /// </summary>
    private static List<string> ReadCanonicalSequence()
    {
        var field = typeof(EvidenceStateManager).GetField(
            "RequiredSequence",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        if (field?.GetValue(null) is not EvidenceStatus[] sequence || sequence.Length == 0)
        {
            return null;
        }

        var names = new List<string>(sequence.Length);
        foreach (var status in sequence)
        {
            names.Add(status.ToString());
        }
        return names;
    }

    private static string BuildJson(string scenarioId, List<EvidenceDefinition> entries, List<string> lifecycleSequence)
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine($"  \"scenarioId\": \"{Escape(scenarioId)}\",");
        sb.AppendLine($"  \"generatedAt\": \"{System.DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}\",");

        // The canonical order, exported so external readers don't have to keep their
        // own copy. Names, never ordinals - same rule as relevance below.
        sb.Append("  \"lifecycleSequence\": [");
        for (int i = 0; i < lifecycleSequence.Count; i++)
        {
            sb.Append($"\"{Escape(lifecycleSequence[i])}\"");
            if (i < lifecycleSequence.Count - 1) sb.Append(", ");
        }
        sb.AppendLine("],");

        sb.AppendLine("  \"evidence\": [");

        for (int i = 0; i < entries.Count; i++)
        {
            var def = entries[i];
            // relevance is written as the ENUM NAME, never its ordinal. An int here
            // would be the same bug already fixed twice in this project (ToolType,
            // SessionEvent.eventType): this file outlives the C# declaration, so an
            // inserted enum member would silently repoint every past export.
            sb.Append("    { ");
            sb.Append($"\"evidenceId\": \"{Escape(def.evidenceId)}\", ");
            sb.Append($"\"displayName\": \"{Escape(def.displayName)}\", ");
            sb.Append($"\"relevance\": \"{def.relevance}\"");
            sb.Append(" }");
            sb.AppendLine(i < entries.Count - 1 ? "," : string.Empty);
        }

        sb.AppendLine("  ]");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", string.Empty).Replace("\t", " ");
    }
}
