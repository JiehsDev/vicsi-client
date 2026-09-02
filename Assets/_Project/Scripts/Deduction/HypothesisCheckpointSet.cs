// Assets/_Project/Scripts/Deduction/HypothesisCheckpointSet.cs
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What makes a hypothesis checkpoint fire. A small tagged union rather than a
/// single-purpose struct: CountThreshold is the only kind implemented today, but the
/// scenario design also calls for a checkpoint fired by the player submitting their
/// case on the Deduction Board, and later likely a criticality-weighted variant once
/// EvidenceDefinition grows a priority field. Fields not used by the active type are
/// simply ignored - see HypothesisCheckpointManager for which ones each type reads.
/// </summary>
public enum HypothesisTriggerType
{
    /// <summary>Fires once thresholdCount evidence items have reached thresholdStatus or beyond.</summary>
    CountThreshold,

    /// <summary>
    /// Reserved: fires when the player submits their case via the Deduction Board.
    /// Nothing raises this yet - EvidenceBoardController.SubmitTheory() currently
    /// exposes no event to hook, so HypothesisCheckpointManager only offers a
    /// placeholder method an eventual hook would call. Authoring a checkpoint with
    /// this type today is harmless but it will never fire on its own.
    /// </summary>
    SubmissionInitiated
}

[Serializable]
public class HypothesisTrigger
{
    [Tooltip("What makes this checkpoint fire. CountThreshold is the only type wired up today; SubmissionInitiated is reserved until the Deduction Board exposes a submission hook.")]
    public HypothesisTriggerType type = HypothesisTriggerType.CountThreshold;

    [Tooltip("CountThreshold only: the evidence status items must have reached (or passed, in the canonical sequence) to be counted.")]
    public EvidenceStatus thresholdStatus = EvidenceStatus.Collected;

    [Tooltip("CountThreshold only: how many evidence items must have reached thresholdStatus before this checkpoint fires.")]
    public int thresholdCount = 1;
}

/// <summary>
/// One justification the player can pick to explain their chosen theory. Reasoning is
/// multiple choice rather than free text on purpose: VR keyboard entry is poor, and -
/// more importantly - typed reasoning can't be compared across students or scored
/// without an NLP step this project has no reason to take on, whereas a keyed choice
/// makes "right answer, wrong reasoning" directly detectable.
///
/// Authoring rule, and it matters more than it looks: each option must be plausible
/// for MORE THAN ONE of the checkpoint's hypotheses. If one justification is visibly
/// the competent-sounding one, the list has told the student which theory to pick and
/// the checkpoint stops measuring anything.
/// </summary>
[Serializable]
public class ReasoningOption
{
    [TextArea]
    [Tooltip("The justification as the player reads it, e.g. \"The recovered weapon is inconsistent with an accidental injury\".")]
    public string text;

    [Tooltip("Authoring metadata for later scoring: whether this is the defensible justification. NOTHING READS THIS YET - no scoring logic runs against it in the current build; it exists so checkpoints can be authored now and scored once faculty-validated answers exist.")]
    public bool isCorrect;
}

/// <summary>
/// One forced commitment point: a prompt, a short set of mutually exclusive theories
/// to pick from, and optionally a second multiple-choice pick justifying that theory.
/// Deliberately a plain serializable class rather than its own asset - a checkpoint has
/// no meaning outside the set it belongs to, and one asset per checkpoint would make
/// reordering a scenario a file-management chore.
/// </summary>
[Serializable]
public class HypothesisCheckpoint
{
    [Tooltip("Stable, author-set identifier for this checkpoint (e.g. \"HYP-01\"). Used as the targetId on the logged HypothesisSubmitted event, so it must NOT be derived from list position - reordering checkpoints would silently repoint every past session's data at the wrong checkpoint.")]
    public string id;

    [TextArea]
    [Tooltip("The question shown to the player, e.g. \"Based on what you've observed so far, what is your working theory?\"")]
    public string promptText;

    [Tooltip("The mutually exclusive theories the player picks between, e.g. Accidental fall / Homicide / Undetermined. The player must choose exactly one.")]
    public List<string> options = new();

    [Tooltip("Whether the player must also pick a justification after choosing a theory. An early opening checkpoint may reasonably skip this; later ones generally shouldn't. If true, reasoningOptions must be non-empty (checked in OnValidate).")]
    public bool requiresReasoning;

    [Tooltip("The justifications offered after a theory is chosen, when requiresReasoning is true. Authored per checkpoint rather than shared across the set, since different checkpoints fire with different evidence in hand and so have different plausible justifications available.")]
    public List<ReasoningOption> reasoningOptions = new();

    [Tooltip("What causes this checkpoint to appear.")]
    public HypothesisTrigger trigger = new();
}

/// <summary>
/// Every hypothesis checkpoint for one scenario, in the order they should fire.
/// Standalone for now: no ScenarioDefinition root asset exists yet (Data/Scenarios is
/// still empty), so this carries its own scenarioId. When that root does exist, this
/// becomes one field on it and scenarioId here can go away.
/// </summary>
[CreateAssetMenu(fileName = "HypothesisCheckpoints_", menuName = "VR-CSI/Hypothesis Checkpoint Set")]
public class HypothesisCheckpointSet : ScriptableObject
{
    [Tooltip("Which scenario these checkpoints belong to. A plain string until a real ScenarioDefinition root asset exists to hold this reference properly - match it to the scene/scenario name, e.g. \"CSI_Environment\".")]
    public string scenarioId;

    [Tooltip("Checkpoints in the order they should fire. If two triggers are satisfied at the same moment, the one earlier in this list is shown first and the other queues behind it.")]
    public List<HypothesisCheckpoint> checkpoints = new();

#if UNITY_EDITOR
    /// <summary>
    /// Author-time validation. This is the project's first OnValidate - everything
    /// else validates at runtime with Debug.LogWarning (see
    /// HypothesisCheckpointManager.ValidateCheckpointIds, PlayerTool.Awake,
    /// PlayerToolRegistry.Register). Both are kept deliberately: OnValidate catches
    /// mistakes while the instructor is actually editing the asset, and the runtime
    /// check still covers an asset that was authored but never reopened in the
    /// Inspector. Editor-only, so it costs a player build nothing.
    /// </summary>
    private void OnValidate()
    {
        if (checkpoints == null)
        {
            return;
        }

        var seenIds = new HashSet<string>();

        foreach (var checkpoint in checkpoints)
        {
            if (checkpoint == null)
            {
                continue;
            }

            if (string.IsNullOrEmpty(checkpoint.id))
            {
                Debug.LogWarning($"[{name}] A checkpoint has no id. It will never fire, and its answers would have nothing to key against.", this);
            }
            else if (!seenIds.Add(checkpoint.id))
            {
                Debug.LogWarning($"[{name}] Duplicate checkpoint id '{checkpoint.id}'. Only the first will ever fire, and past sessions' data would be ambiguous.", this);
            }

            if (checkpoint.requiresReasoning && (checkpoint.reasoningOptions == null || checkpoint.reasoningOptions.Count == 0))
            {
                Debug.LogWarning($"[{name}] Checkpoint '{checkpoint.id}' requires reasoning but has no reasoningOptions - the player would have nothing to pick and could not submit.", this);
            }
        }
    }
#endif
}
