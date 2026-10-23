// Assets/_Project/Scripts/Ending/EndingSet.cs
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One conclusion the player can explicitly select at Submit Conclusion time (e.g. "The
/// wife committed the murder"). Distinct from HypothesisOption/ReasoningOption (the
/// earlier, narrative-flavor "what does your character think so far" checkpoints) - this
/// is the deliberate, final, scored pick Submit Conclusion asks for. Reused identically
/// across a scenario's endings via conclusionId, the same "same handful of ids shared
/// across checkpoints" reasoning HypothesisOption's own comment documents.
/// </summary>
[Serializable]
public class ConclusionOption
{
    [Tooltip("Stable, author-set identifier (e.g. \"conclusion-wife\"). Used as the logged value and as what an EndingDefinition.requiredConclusionId references - must not be derived from list position.")]
    public string conclusionId;

    [TextArea]
    [Tooltip("The conclusion as the player reads it, e.g. \"The wife committed the murder.\"")]
    public string text;
}

/// <summary>
/// One possible ending for the scenario. Replaces CompletionScreenUI's old hardcoded
/// 2-outcome BuildLines logic - see EndingManager's own class comment for the resolution
/// flow. "Required/forbidden deductions or connections" from the design brief are NOT
/// separate fields here - author a RequirementType.RelationshipCompleted or
/// ObjectiveCompleted leaf (wrapped in NOT for "forbidden") inside resolutionRequirement
/// instead, the same single-evaluator principle every other *Definition/*Event type in
/// this project already follows.
///
/// Deliberately carries no CSI_Environment-specific data in this class - every field is
/// generic. Whatever gets typed into an actual authored entry is content, not something
/// this file hardcodes.
/// </summary>
[Serializable]
public class EndingDefinition
{
    [Tooltip("Stable, author-set identifier (e.g. \"ending-wife-convicted\"). Used as the logged targetId - must not be derived from list position.")]
    public string endingId;

    [Tooltip("Shown to the player on the completion screen as the result/outcome header (see CompletionScreenUI.BuildLines), in addition to being an author-facing label in editor tooling. Keep it a human-readable outcome name (\"Wife Convicted\", \"Inconclusive\") - never a raw id, score, or correctness judgment; postSessionExplanation below remains the field that stays hidden from the player.")]
    public string title;

    [Tooltip("When several endings' resolutionRequirement (and requiredConclusionId, if set) are simultaneously satisfied, the highest priority wins. Ties keep list order (first-listed wins).")]
    public int priority;

    [Tooltip("Must be satisfied for this ending to be eligible - evidence state, relationships, objectives, narrative events, nested ALL/ANY/NOT, all through the one shared evaluator. Empty ALL group (the default) means always eligible on requiredConclusionId grounds alone.")]
    public RequirementNode resolutionRequirement = new();

    [Tooltip("If set, this ending is only eligible when the player selected THIS conclusionId at the picker. Blank = eligible regardless of (or absent any) conclusion selection.")]
    public string requiredConclusionId;

    [TextArea]
    [Tooltip("The ending's narrative text, shown to the player on the completion screen - same sequencing CompletionScreenUI already uses (one line at a time, auto-advancing). Separate lines with a blank line to break into multiple displayed lines; a single block displays as one line.")]
    public string endingText;

    [Tooltip("Optional - shown alongside the ending text if the display surface supports it. Not read by any UI yet in this pass.")]
    public Sprite panelImage;

    [Tooltip("Optional - played once when this ending is shown.")]
    public AudioClip endingAudioClip;

    [Tooltip("Optional: fires this NarrativeEvent (via NarrativeEventManager.Fire) once this ending is shown. Blank = no follow-up.")]
    public string followUpNarrativeEventId;

    [TextArea]
    [Tooltip("Instructor/dev-facing explanation of why this ending resolved the way it did. NEVER shown to the player live - same boundary as every other post-session-only field in this project (RelationshipDefinition.explanation, ReasoningOption.isCorrect, ...).")]
    public string postSessionExplanation;

    [Tooltip("Marks this as THE fallback ending, used when no other ending's requirements resolve. Exactly one ending in a set should have this set - see EndingSet.OnValidate.")]
    public bool isFallback;
}

/// <summary>
/// Every possible ending for one scenario, plus the submission gate and conclusion
/// options Submit Conclusion itself needs. Standalone for now, same reasoning as every
/// other *Set in this project: no ScenarioDefinition root asset exists yet.
/// </summary>
[CreateAssetMenu(fileName = "Endings_", menuName = "VR-CSI/Ending/Ending Set")]
public class EndingSet : ScriptableObject
{
    [Tooltip("Which scenario this belongs to - match it to the scene/scenario name, e.g. \"CSI_Environment\".")]
    public string scenarioId;

    [Tooltip("SUPERSEDED - no longer read by EndingManager.TrySubmitConclusion, which now gates on EvidenceBoardController.HasEverSubmittedTheory + ConclusionStateManager.GetAvailableConclusions() (Case level) instead (see the Dynamic Submit Conclusion task). Left here as historical data only, in case a future pass wants it back.")]
    public RequirementNode submissionRequirement = new();

    [Tooltip("SUPERSEDED - no longer read by the picker, which now builds its options dynamically from ConclusionStateManager's available Case-level ConclusionDefinitions instead (see the Dynamic Submit Conclusion task). Left here as historical data only.")]
    public List<ConclusionOption> conclusionOptions = new();

    [Tooltip("Every possible ending, in authoring order (priority decides resolution order, not this list order - see EndingDefinition.priority).")]
    public List<EndingDefinition> endings = new();

#if UNITY_EDITOR
    /// <summary>Author-time validation - same warn-don't-block idiom as every other *Set.OnValidate in this project.</summary>
    private void OnValidate()
    {
        ValidateTree(submissionRequirement, "submissionRequirement");

        if (conclusionOptions != null)
        {
            var seenConclusionIds = new HashSet<string>();
            foreach (var option in conclusionOptions)
            {
                if (option == null)
                {
                    continue;
                }
                if (string.IsNullOrEmpty(option.conclusionId))
                {
                    Debug.LogWarning($"[{name}] A conclusion option has no conclusionId.", this);
                }
                else if (!seenConclusionIds.Add(option.conclusionId))
                {
                    Debug.LogWarning($"[{name}] Duplicate conclusionId '{option.conclusionId}'.", this);
                }
            }
        }

        if (endings == null)
        {
            return;
        }

        var seenIds = new HashSet<string>();
        int fallbackCount = 0;

        foreach (var ending in endings)
        {
            if (ending == null)
            {
                continue;
            }

            if (string.IsNullOrEmpty(ending.endingId))
            {
                Debug.LogWarning($"[{name}] An ending has no endingId. It will never be selectable and cannot be logged meaningfully.", this);
            }
            else if (!seenIds.Add(ending.endingId))
            {
                Debug.LogWarning($"[{name}] Duplicate ending id '{ending.endingId}'. Only the first registered instance is ever resolved to.", this);
            }

            if (ending.isFallback)
            {
                fallbackCount++;
            }

            ValidateTree(ending.resolutionRequirement, $"{ending.endingId}/resolutionRequirement");
        }

        if (fallbackCount == 0)
        {
            Debug.LogWarning($"[{name}] No ending is marked isFallback - if the player's evidence/conclusion never matches any ending's requirements, EndingManager has nothing to show.", this);
        }
        else if (fallbackCount > 1)
        {
            Debug.LogWarning($"[{name}] {fallbackCount} endings are marked isFallback - only the first one found will ever be used as the fallback.", this);
        }
    }

    private void ValidateTree(RequirementNode node, string path)
    {
        if (node == null)
        {
            return;
        }

        var warnings = new List<string>();
        node.Validate(warnings, path);
        foreach (var warning in warnings)
        {
            Debug.LogWarning($"[{name}] {warning}", this);
        }
    }
#endif
}
