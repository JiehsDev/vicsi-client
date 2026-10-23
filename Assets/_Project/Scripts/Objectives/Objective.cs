// Assets/_Project/Scripts/Objectives/Objective.cs
using System;
using UnityEngine;

/// <summary>
/// One investigation objective: a title/description, an unlock condition (Hidden -&gt;
/// Active), a completion condition (Active -&gt; Completed), and an optional failure
/// condition (Active -&gt; Failed) - see ObjectiveState's own class comment for the state
/// machine. A plain [Serializable] class held in an ObjectiveSet's list, not its own
/// asset - same "no meaning outside the set it belongs to" reasoning as HypothesisCheckpoint/
/// NarrativeEvent.
///
/// "Dependencies on earlier objectives" is deliberately NOT a separate field here - an
/// objective that depends on another authors a RequirementType.ObjectiveCompleted leaf
/// into its own unlockRequirement instead, the same single-evaluator principle
/// NarrativeEvent's own class comment already establishes for "unlockable" targets.
///
/// Deliberately carries no CSI_Environment-specific data in this class - every field is
/// generic. Whatever gets typed into an actual authored entry is content, not something
/// this file hardcodes.
/// </summary>
public enum ObjectivePhaseScope { SOCOInvestigation = 0, AnyPhase = 1, ExactPhase = 2 }

[Serializable]
public class Objective
{
    [Tooltip("Stable, author-set identifier (e.g. \"obj-search-kitchen\"). Used as the logged targetId and as what a RequirementType.ObjectiveCompleted leaf elsewhere references - must not be derived from list position.")]
    public string objectiveId;

    [Tooltip("Existing investigation objectives default to SOCO. Future procedural objectives may target an exact phase.")]
    public ObjectivePhaseScope phaseScope;
    public ScenarioPhase requiredPhase;


    [Tooltip("Player-facing title, e.g. \"Search the kitchen for evidence.\"")]
    public string title;

    [TextArea]
    [Tooltip("Player-facing longer description, optional.")]
    public string description;

    [Tooltip("Must be satisfied before this objective becomes visible/active (Hidden -> Active). Empty ALL group (the default) means active immediately once the scene starts.")]
    public RequirementNode unlockRequirement = new();

    [Tooltip("Must be satisfied for this objective to complete (Active -> Completed). Empty ALL group (the default) means it completes the instant it becomes active - author a real condition for anything meant to represent actual progress.")]
    public RequirementNode completionRequirement = new();

    [Tooltip("Optional. If set and satisfied while Active, this objective fails instead of completing (Active -> Failed). Left at its default (an empty ALL group) means this objective can never fail - see ObjectiveStateManager.HasRealFailureCondition, which treats that specific default shape as \"unconfigured\" rather than vacuously true. Author a real ALL/ANY/NOT tree here only when this objective should actually be failable.")]
    public RequirementNode failureRequirement;

    [Tooltip("Display-sort hint only - never gates anything. Objectives are evaluated and can unlock/complete in any order regardless of this value; use RequirementType.ObjectiveCompleted in unlockRequirement for a real dependency.")]
    public int order;

    [Tooltip("Author-time visibility toggle for objective-list UI, independent of state - an objective can be Active but still excluded from what the player sees listed (e.g. a purely internal bookkeeping objective). Does not affect unlock/completion/failure evaluation at all.")]
    public bool showInObjectiveList = true;

    [Tooltip("Optional: fires this NarrativeEvent (via NarrativeEventManager.Fire) the moment this objective unlocks (Hidden -> Active). Blank = no narrative side effect.")]
    public string narrativeEventOnUnlockId;

    [Tooltip("Optional: fires this NarrativeEvent the moment this objective completes (Active -> Completed). Blank = no narrative side effect.")]
    public string narrativeEventOnCompletionId;

    [Tooltip("Procedural bookkeeping objective that is not part of the assessment (e.g. \"Talk to the First Responder\"): tracked and logged like any other, but left out of the uploaded objective counts so adding it never changes a student's reported scores.")]
    public bool excludeFromScoring;
}
