// Assets/_Project/Scripts/CaseFile/ProcedureStepRequirement.cs
using System;
using UnityEngine;

/// <summary>
/// How a ProcedureStepRequirement reacts when its requirement isn't met at the moment the
/// item tries to reach beforeStatus. Explicit values: append, never renumber (serialized).
/// </summary>
public enum ProcedureEnforcement
{
    /// <summary>Allow the transition, but log ProcedureOutOfOrder (once per step per item) for training feedback/analytics.</summary>
    Warn = 0,

    /// <summary>Refuse the transition, exactly like any other procedural gate.</summary>
    Block = 1
}

/// <summary>
/// What a ProcedureStepRequirement guards. Explicit values: append, never renumber.
/// </summary>
public enum ProcedureAction
{
    /// <summary>The transition into beforeStatus (the original and default meaning).</summary>
    StatusTransition = 0,

    /// <summary>Taking a biological (swab) sample from the item - EvidenceStateManager.MarkSwabbed. beforeStatus is ignored.</summary>
    BiologicalSampling = 1,

    /// <summary>Starting fingerprint processing (powder/brush/lift) - EvidenceStateManager.MarkFingerprinted. beforeStatus is ignored.</summary>
    FingerprintProcessing = 2
}

/// <summary>
/// One data-driven procedure rule on an evidence item: "before this item reaches
/// beforeStatus, requirement must hold". Lets future procedure (scene photography done
/// before collection, measurements taken before movement, ...) be authored per item
/// instead of adding a new flag + IsBlockedBy* method for each rule. Evaluated in one
/// place - EvidenceStateManager.GetItemBlockReason (Block) and SetStatus (Block + Warn) -
/// so tools that already ask CanTransition/CanAdvanceTo respect Block rules for free.
/// Lives outside EvidenceStatus on purpose: measurement and scene photography stay
/// external procedure state, never new lifecycle statuses.
/// </summary>
[Serializable]
public class ProcedureStepRequirement
{
    [Tooltip("Stable id for logging/training analytics, e.g. \"scene-photos-before-collection\". Logged as procedureId on ProcedureOutOfOrder - never derive it from list position.")]
    public string stepId;

    [Tooltip("What this rule guards: a status transition (into beforeStatus), or an action that changes the item without a status of its own (swab sampling, fingerprint processing).")]
    public ProcedureAction action = ProcedureAction.StatusTransition;

    [Tooltip("The rule applies to the transition INTO this status (StatusTransition rules only).")]
    public EvidenceStatus beforeStatus = EvidenceStatus.Collected;

    [Tooltip("What must already hold. An empty ALL group is vacuously true (the rule never fires).")]
    public RequirementNode requirement = new();

    public ProcedureEnforcement enforcement = ProcedureEnforcement.Warn;

    [Tooltip("ViCSIDialogueCatalog id of the player-facing text: the reason a Block rule refuses the action, or the warning the acting tool shows for a Warn rule. Edit the wording in the catalog.")]
    public string messageId;

    [Tooltip("Legacy: used only when messageId is empty. Player-facing wording now lives in ViCSIDialogueCatalog.")]
    public string message;

    /// <summary>The player-facing text for this rule, resolved from the catalog (falls back to the legacy message).</summary>
    public string PlayerMessage => !string.IsNullOrEmpty(messageId) ? GameDialogue.Text(messageId) : message;

    [Tooltip("Optional, logged as expectedPrerequisite on ProcedureOutOfOrder (e.g. \"measurement:MEAS-EVD-014\"). Falls back to message when empty. Never shown to the player.")]
    public string expectedPrerequisite;
}
