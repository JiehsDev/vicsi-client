// Assets/_Project/Scripts/Assessment/ScenarioDefinition.cs
using UnityEngine;

/// <summary>
/// The stable identity of one playable scenario, for upload/scoring purposes - NOT the
/// Unity scene name. SessionLogger's SessionStarted event currently stamps the active
/// scene name as scenarioId, which its own comment already flags as a temporary
/// stand-in "until a real ScenarioDefinition exists"; this is that asset. Referenced by
/// whatever builds the upload DTO (AssessmentSessionBuilder), not by SessionLogger
/// itself - SessionLogger stays untouched, per this feature's own ground rule.
///
/// One asset per scenario (e.g. Assets/_Project/Data/Scenarios/Scenario_CSI_Environment.
/// asset), analogous to every other per-scenario *Set asset in this project
/// (HypothesisCheckpointSet, NarrativeEventSet, ObjectiveSet, EndingSet - all already
/// scenarioId-scoped). Distinct identifiers, distinct reasons to change:
///   scenarioId          - stable forever once real student data references it; never
///                          reassign an existing id to a different scenario
///   scenarioVersion      - bump when the scenario's authored CONTENT changes (evidence
///                          set, story beats, endings, ...) in a way that could
///                          invalidate comparing a new score against an old one
///   scoringRulesVersion  - bump when the SCORING ALGORITHM changes, independent of
///                          content - matches EvidenceScorer's own "the answer key and
///                          the game must never silently drift apart" discipline
///   groundTruthVersion   - identifies which ScenarioGroundTruthExporter export this
///                          scenario's scoring should be checked against server-side;
///                          independent of scenarioVersion because a content typo fix
///                          (e.g. reworded dialogue) need not invalidate the ground
///                          truth, while an evidence-relevance reclassification would
/// </summary>
[CreateAssetMenu(fileName = "Scenario_", menuName = "VR-CSI/Assessment/Scenario Definition")]
public class ScenarioDefinition : ScriptableObject
{
    [Tooltip("Stable identifier, e.g. \"CSI-ENVIRONMENT-001\". Must match exactly what the server/Supabase scenarios table recognizes. Never reassign an existing id to a different scenario once real student data references it.")]
    public string scenarioId = "CSI-ENVIRONMENT-001";

    [Tooltip("Human-readable name, e.g. \"Kitchen Homicide - CSI Environment\". Display only, never used as a key.")]
    public string displayName = "CSI Environment";

    [Tooltip("Version of this scenario's authored content. Bump when evidence, story beats, relationships, objectives, or endings change in a way that could invalidate comparing a new score against an old one.")]
    public string scenarioVersion = "1.0.0";

    [Tooltip("Version of the scoring rules/algorithm applied to this scenario - independent of scenarioVersion; see the class comment.")]
    public string scoringRulesVersion = "1.0.0";

    [Tooltip("Identifies which ScenarioGroundTruthExporter export this scenario's server-side scoring should be checked against - independent of scenarioVersion; see the class comment.")]
    public string groundTruthVersion = "1.0.0";

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(scenarioId))
        {
            Debug.LogWarning($"[{name}] scenarioId is empty.", this);
        }
        if (string.IsNullOrWhiteSpace(scenarioVersion))
        {
            Debug.LogWarning($"[{name}] scenarioVersion is empty.", this);
        }
        if (string.IsNullOrWhiteSpace(scoringRulesVersion))
        {
            Debug.LogWarning($"[{name}] scoringRulesVersion is empty.", this);
        }
        if (string.IsNullOrWhiteSpace(groundTruthVersion))
        {
            Debug.LogWarning($"[{name}] groundTruthVersion is empty.", this);
        }

        var guids = UnityEditor.AssetDatabase.FindAssets("t:ScenarioDefinition");
        foreach (var guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            var other = UnityEditor.AssetDatabase.LoadAssetAtPath<ScenarioDefinition>(path);
            if (other != null && other != this && !string.IsNullOrEmpty(scenarioId)
                && other.scenarioId == scenarioId)
            {
                Debug.LogWarning($"[{name}] scenarioId \"{scenarioId}\" duplicates {path}.", this);
                break;
            }
        }
    }
#endif
}
