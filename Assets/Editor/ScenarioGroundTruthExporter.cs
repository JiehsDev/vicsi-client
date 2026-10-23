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
        int excludedExaminedInPlace = 0;

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

            // examinedInPlace items (the body, so far - see EvidenceDefinition's own
            // comment) are deliberately excluded from ground truth. They're real,
            // correctly-registered roster members at runtime - this is NOT the same
            // case as the two anomalies above - but nothing has yet decided how they
            // should integrate with the systems that consume this file's roster
            // (HYP-03's "N items processed" threshold semantics, documentationAccuracy,
            // ReasoningAccuracy): those all currently assume every exported evidence
            // entry can reach Processed and gets scored the same way, which is false
            // for an item whose real chain ends at Logged. Exporting it today would
            // silently hand the scorer a 6th roster row no downstream consumer is
            // designed to interpret. Revisit this exclusion when that integration
            // decision is actually made - don't just delete it because the count looks
            // wrong later. Logged via a separate counter, not "skipped": unlike the two
            // anomalies above, this isn't a problem with the data.
            if (def.examinedInPlace)
            {
                excludedExaminedInPlace++;
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

        // Optional, unlike the evidence roster above: not every scenario scene has a
        // HypothesisCheckpointManager, and a scene without one should still export its
        // evidence roster rather than failing the whole export.
        var checkpoints = ReadHypothesisCheckpoints();

        string json = BuildJson(scenarioId, entries, lifecycleSequence, checkpoints);
        string path = $"{OutputFolder}/GroundTruth_{scenarioId}.json";

        if (!AssetDatabase.IsValidFolder(OutputFolder))
        {
            Debug.LogError($"[ScenarioGroundTruthExporter] Output folder '{OutputFolder}' does not exist.");
            return;
        }

        System.IO.File.WriteAllText(path, json, new UTF8Encoding(false));
        AssetDatabase.ImportAsset(path);

        string suffix = (skipped > 0 ? $" ({skipped} skipped)" : "")
            + (excludedExaminedInPlace > 0 ? $" ({excludedExaminedInPlace} examinedInPlace item(s) excluded - see comment in Export())" : "");
        Debug.Log($"[ScenarioGroundTruthExporter] Wrote {entries.Count} evidence entries for '{scenarioId}' to {path}{suffix}.");
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

    /// <summary>
    /// Reads reasoning-option correctness straight from the open scene's
    /// HypothesisCheckpointManager.checkpointSet - same "no second hand-maintained
    /// list" rule as the evidence roster above, and the same SerializedObject-for-a-
    /// private-field technique, since checkpointSet is a private [SerializeField] too.
    /// Only ids and isCorrect are exported, never the option text: this file is an
    /// answer key, not a content mirror, matching the evidence entries above (which
    /// export relevance, never description).
    ///
    /// Checked and confirmed structurally independent of the examinedInPlace exclusion
    /// above: this reads HypothesisCheckpointSet.checkpoints directly, not the entries
    /// list, and GroundTruthCheckpoint carries no evidenceId at all (just a checkpoint
    /// id plus reasoning-option ids) - there is no field here a body-specific
    /// checkpoint could even attach to today, so nothing extra to guard here.
    /// </summary>
    private static List<GroundTruthCheckpoint> ReadHypothesisCheckpoints()
    {
        var manager = Object.FindFirstObjectByType<HypothesisCheckpointManager>();
        if (manager == null)
        {
            return null;
        }

        var serialized = new SerializedObject(manager);
        var setProperty = serialized.FindProperty("checkpointSet");
        var set = setProperty?.objectReferenceValue as HypothesisCheckpointSet;
        if (set == null || set.checkpoints == null)
        {
            return null;
        }

        var result = new List<GroundTruthCheckpoint>();
        foreach (var checkpoint in set.checkpoints)
        {
            if (checkpoint == null || string.IsNullOrEmpty(checkpoint.id))
            {
                continue;
            }

            // HYP-01-shaped checkpoints (requiresReasoning false, no reasoningOptions)
            // correctly have nothing to export here - skipped, not written as an empty
            // entry, so the export mirrors "this checkpoint has no answer key" rather
            // than implying one that happens to be empty.
            if (checkpoint.reasoningOptions == null || checkpoint.reasoningOptions.Count == 0)
            {
                continue;
            }

            var options = new List<GroundTruthReasoningOption>();
            foreach (var option in checkpoint.reasoningOptions)
            {
                if (option == null || string.IsNullOrEmpty(option.id))
                {
                    continue;
                }
                options.Add(new GroundTruthReasoningOption { Id = option.id, IsCorrect = option.isCorrect });
            }

            result.Add(new GroundTruthCheckpoint { Id = checkpoint.id, ReasoningOptions = options });
        }

        return result.Count > 0 ? result : null;
    }

    private struct GroundTruthReasoningOption
    {
        public string Id;
        public bool IsCorrect;
    }

    private struct GroundTruthCheckpoint
    {
        public string Id;
        public List<GroundTruthReasoningOption> ReasoningOptions;
    }

    private static string BuildJson(string scenarioId, List<EvidenceDefinition> entries, List<string> lifecycleSequence, List<GroundTruthCheckpoint> checkpoints)
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

        bool hasCheckpoints = checkpoints != null && checkpoints.Count > 0;

        // Same "no reportLineOptions authored" skip as HYP-01-shaped checkpoints get
        // above, one level up - an item with nothing authored is omitted from this
        // list entirely rather than written with an empty options array, so absence
        // here always means "no answer key," never "authored as empty."
        //
        // Built from `entries`, not from sceneEvidenceDefinitions directly - so an
        // examinedInPlace item is already excluded here too, automatically, via the
        // same exclusion applied when `entries` was built above. No separate guard
        // needed in this section.
        var reportLineEntries = new List<EvidenceDefinition>();
        foreach (var def in entries)
        {
            if (def.reportLineOptions != null && def.reportLineOptions.Count > 0)
            {
                reportLineEntries.Add(def);
            }
        }
        bool hasReportLines = reportLineEntries.Count > 0;

        sb.AppendLine(hasCheckpoints || hasReportLines ? "  ]," : "  ]");

        // Omitted entirely (not an empty array) when the scene has no checkpoints with
        // an answer key to export - same "absence means no data" reasoning as skipping
        // HYP-01-shaped checkpoints above, one level up.
        if (hasCheckpoints)
        {
            sb.AppendLine("  \"hypothesisCheckpoints\": [");
            for (int i = 0; i < checkpoints.Count; i++)
            {
                var checkpoint = checkpoints[i];
                sb.AppendLine("    {");
                sb.AppendLine($"      \"id\": \"{Escape(checkpoint.Id)}\",");
                sb.AppendLine("      \"reasoningOptions\": [");
                for (int j = 0; j < checkpoint.ReasoningOptions.Count; j++)
                {
                    var option = checkpoint.ReasoningOptions[j];
                    sb.Append($"        {{ \"id\": \"{Escape(option.Id)}\", \"isCorrect\": {(option.IsCorrect ? "true" : "false")} }}");
                    sb.AppendLine(j < checkpoint.ReasoningOptions.Count - 1 ? "," : string.Empty);
                }
                sb.AppendLine("      ]");
                sb.Append("    }");
                sb.AppendLine(i < checkpoints.Count - 1 ? "," : string.Empty);
            }
            sb.AppendLine(hasReportLines ? "  ]," : "  ]");
        }

        // Per-item report-line answer key: id + isAccurate only, never text - same
        // boundary as reasoningOptions above and the evidence roster's own
        // relevance-not-description rule. Sourced straight from the SAME entries list
        // the evidence roster above was built from (no separate scene read, unlike
        // ReadHypothesisCheckpoints - reportLineOptions already lives on these
        // EvidenceDefinition objects), so this can never drift from the live roster.
        if (hasReportLines)
        {
            sb.AppendLine("  \"evidenceReportLines\": [");
            for (int i = 0; i < reportLineEntries.Count; i++)
            {
                var def = reportLineEntries[i];
                sb.AppendLine("    {");
                sb.AppendLine($"      \"evidenceId\": \"{Escape(def.evidenceId)}\",");
                sb.AppendLine("      \"reportLineOptions\": [");
                for (int j = 0; j < def.reportLineOptions.Count; j++)
                {
                    var option = def.reportLineOptions[j];
                    sb.Append($"        {{ \"id\": \"{Escape(option.id)}\", \"isAccurate\": {(option.isAccurate ? "true" : "false")} }}");
                    sb.AppendLine(j < def.reportLineOptions.Count - 1 ? "," : string.Empty);
                }
                sb.AppendLine("      ]");
                sb.Append("    }");
                sb.AppendLine(i < reportLineEntries.Count - 1 ? "," : string.Empty);
            }
            sb.AppendLine("  ]");
        }

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
