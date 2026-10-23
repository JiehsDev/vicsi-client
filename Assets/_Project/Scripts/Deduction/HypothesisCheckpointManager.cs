// Assets/_Project/Scripts/Deduction/HypothesisCheckpointManager.cs
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Watches evidence progress and forces the player to commit to a working theory at
/// authored checkpoints. Self-initializing singleton on the same pattern as
/// EvidenceStateManager/ProceduralGateValidator/SessionLogger.
///
/// Subscribes to the existing EvidenceStateManager.OnEvidenceStatusChanged and does
/// its own counting via CountAtOrAbove - EvidenceStateManager deliberately knows
/// nothing about hypothesis checkpoints, exactly as SessionLogger is wired.
///
/// The point of this system is the data, not the screen: every answer is written to
/// SessionLogger along with the evidence state at that moment, so later analysis can
/// see not just what the player concluded but what they knew when they concluded it,
/// and how that changed between checkpoints.
/// </summary>
public class HypothesisCheckpointManager : MonoBehaviour
{
    public static HypothesisCheckpointManager Instance { get; private set; }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    internal void SkipPastForDevelopmentStart(bool evidenceComplete)
    {
        SceneTestStart.RequirePreparation();
        pending.Clear();
        if (evidenceComplete && checkpointSet != null)
            foreach (var checkpoint in checkpointSet.checkpoints)
                if (checkpoint.trigger.type == HypothesisTriggerType.CountThreshold)
                    firedCheckpointIds.Add(checkpoint.id);
    }
#endif

    /// <summary>
    /// Fired the moment a checkpoint's answer is confirmed - AFTER logging, so any
    /// subscriber sees a checkpoint that is already durably recorded. Carries
    /// (checkpointId, selectedOptionId) only - never the reasoning pick, never
    /// anything that could be read as a correctness signal, matching this project's
    /// standing rule that no score/accuracy judgment is ever player-facing. Exists so
    /// other systems (e.g. a completion screen after the scenario's final checkpoint)
    /// can react without this class needing to know anything about them - the same
    /// "publish an event, don't special-case a caller" shape as
    /// EvidenceStateManager.OnEvidenceStatusChanged.
    /// </summary>
    public static event System.Action<string, string> OnCheckpointAnswered;

    [Tooltip("The checkpoints for the scenario in this scene. Authored as a VR-CSI/Hypothesis Checkpoint Set asset (see Data/Scenarios).")]
    [SerializeField] private HypothesisCheckpointSet checkpointSet;

    [Tooltip("The panel that presents a checkpoint and collects the answer. If left unassigned, checkpoints will queue but never be shown (a loud warning is logged) - answers are the entire point, so this is not silently skipped.")]
    [SerializeField] private HypothesisCheckpointUI checkpointUI;

    /// <summary>One checkpoint waiting to be shown, plus the evidence count that satisfied its trigger at the moment it fired.</summary>
    private class PendingCheckpoint
    {
        public HypothesisCheckpoint checkpoint;
        public int evidenceCountAtTrigger;
    }

    // Checkpoint ids that have already fired this session - a checkpoint can never
    // fire twice, even if its threshold keeps being satisfied by later evidence.
    private readonly HashSet<string> firedCheckpointIds = new();
    private readonly Queue<PendingCheckpoint> pending = new();
    private PendingCheckpoint active;
    private bool warnedAboutMissingUI;

    private void Awake()
    {
        Instance = this;
        ValidateCheckpointIds();
    }

    private void OnEnable()
    {
        EvidenceStateManager.OnEvidenceStatusChanged += HandleEvidenceStatusChanged;
        EvidenceStateManager.OnReportLineSelected += HandleReportLineSelected;
        EvidenceStateManager.OnFingerprintLabStatusChanged += HandleFingerprintLabStatusChanged;
    }

    private void OnDisable()
    {
        EvidenceStateManager.OnEvidenceStatusChanged -= HandleEvidenceStatusChanged;
        EvidenceStateManager.OnReportLineSelected -= HandleReportLineSelected;
        EvidenceStateManager.OnFingerprintLabStatusChanged -= HandleFingerprintLabStatusChanged;
    }

    private void Update()
    {
        EvaluateCountThresholds();
        TryShowNext();
    }

    private void HandleEvidenceStatusChanged(string evidenceId, EvidenceStatus newStatus)
    {
        EvaluateCountThresholds();
    }

    /// <summary>
    /// A checkpoint whose count threshold was already met earlier, but whose correct
    /// reasoning option had an unmet SelectionPrerequisite, is left un-enqueued (see
    /// EvaluateCountThresholds) rather than fired with only wrong answers available.
    /// Re-running the exact same evaluation here - not a separate check - is what
    /// makes that checkpoint fire RETROACTIVELY the instant the missing prerequisite
    /// clears, without needing its own pending-on-prerequisite bookkeeping: the count
    /// is still met (nothing about it changed), so the only thing standing between
    /// "waiting" and "enqueued" was the prerequisite this event just satisfied.
    /// </summary>
    private void HandleReportLineSelected(string evidenceId, string lineId)
    {
        EvaluateCountThresholds();
    }

    /// <summary>
    /// Same retroactive-re-check reasoning as HandleReportLineSelected, for the
    /// second SelectionPrerequisite kind (see PrerequisiteType.FingerprintMatched) -
    /// a checkpoint whose count was already met but whose correct option was waiting
    /// on a lab match now gets re-evaluated the instant that resolves. Fires on both
    /// SentToLab and Matched (the event carries either); re-checking on SentToLab is
    /// simply a harmless no-op since PrerequisitesSatisfied would still read false
    /// then - cheaper to just always re-check than to filter by newStatus here too.
    /// </summary>
    private void HandleFingerprintLabStatusChanged(string evidenceId, FingerprintLabStatus newStatus)
    {
        EvaluateCountThresholds();
    }

    /// <summary>
    /// Checks every not-yet-fired CountThreshold checkpoint, in authored list order,
    /// against the current evidence tally. Anything satisfied is queued; queued
    /// checkpoints are shown one at a time so two thresholds crossing on the same
    /// status change can't stack two panels on top of each other.
    /// </summary>
    private void EvaluateCountThresholds()
    {
        if (SceneTestStart.IsPreparing) return;
        if (SceneTurnover.IsInvestigationBlocked) return;
        if (checkpointSet == null || checkpointSet.checkpoints == null || EvidenceStateManager.Instance == null)
        {
            return;
        }

        foreach (var checkpoint in checkpointSet.checkpoints)
        {
            if (!IsEligible(checkpoint) || checkpoint.trigger.type != HypothesisTriggerType.CountThreshold)
            {
                continue;
            }

            int count = EvidenceStateManager.Instance.CountAtOrAbove(checkpoint.trigger.thresholdStatus);
            var snapshots = ReasoningCheckpointManager.Instance;
            bool integrated = snapshots?.IntegratedDefinition(checkpoint.id) != null;
            if (integrated && snapshots.IsAnswered(snapshots.IntegratedDefinition(checkpoint.id).id)) continue;
            if (integrated ? !snapshots.IntegratedReady(checkpoint.id) : count < checkpoint.trigger.thresholdCount)
            {
                continue;
            }

            // Count alone is no longer sufficient: if the checkpoint's correct
            // option(s) carry a prerequisite (see SelectionPrerequisite), the
            // checkpoint must wait until that's ALSO satisfied, or it would present
            // the player with only wrong answers and no way to recover (checkpoints
            // fire once - see IsEligible). Deliberately not marked fired here (no
            // Enqueue call) - it stays eligible and gets re-checked on every future
            // call to this method, including the one HandleReportLineSelected makes
            // the moment a new selection is recorded, which is what lets it fire
            // retroactively instead of being stuck forever.
            if (!CorrectOptionPrerequisitesSatisfied(checkpoint))
            {
                continue;
            }

            Enqueue(checkpoint, count);
        }

        TryShowNext();
    }

    /// <summary>
    /// Whether every ReasoningOption marked isCorrect on this checkpoint currently has
    /// all of its prerequisites satisfied (see SelectionPrerequisite.AllSatisfied -
    /// the shared check, also used by RecorderReportUI and, for per-option display
    /// filtering, HypothesisCheckpointUI). Checked only against isCorrect options - a
    /// wrong option's prerequisites, if it ever had any, would never gate anything,
    /// since the point is guaranteeing a correct option is actually pickable the
    /// moment the checkpoint opens.
    ///
    /// Vacuously true for a checkpoint with no reasoningOptions at all and for any
    /// option authored before this field existed. Count-threshold checkpoints use this
    /// check to wait until each defensible reasoning path is actually selectable.
    /// </summary>
    private static bool CorrectOptionPrerequisitesSatisfied(HypothesisCheckpoint checkpoint)
    {
        if (checkpoint.reasoningOptions == null)
        {
            return true;
        }

        foreach (var option in checkpoint.reasoningOptions)
        {
            if (option == null || !option.isCorrect)
            {
                continue;
            }

            if (!SelectionPrerequisite.AllSatisfied(option.prerequisites))
            {
                return false;
            }

            // Proof-of-concept integration of the new shared evaluator (see
            // Requirements/RequirementEvaluator.cs): an ADDITIONAL gate, checked
            // alongside the flat prerequisites list above rather than replacing it, so
            // this can express ALL/ANY/NOT trees the flat list cannot. Defaults to an
            // empty ALL group (vacuously true), so every option authored before this
            // field existed is unaffected.
            if (!RequirementEvaluator.IsSatisfied(option.extraRequirement))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Fires every not-yet-fired SubmissionInitiated checkpoint - called for real now
    /// by the Submit Conclusion action (UtilityMenuController's "Submit Conclusion"
    /// entry, see the scene's UtilityMenu). Deliberately does NOT run
    /// CorrectOptionPrerequisitesSatisfied the way EvaluateCountThresholds does for
    /// CountThreshold checkpoints: that gate exists so an automatically-firing
    /// checkpoint never opens with only wrong answers and no way back (it just waits
    /// and re-checks later). Submit is the opposite kind of trigger - a deliberate,
    /// one-shot player action with no "wait and retry", so gating it the same way
    /// would mean pressing Submit before the gloves ever match silently does nothing
    /// at all, which is worse: HYP-03 must always open the instant the player submits,
    /// with whichever reasoning options are actually plausible. Correctness/ending
    /// resolution for HYP-03 specifically happens independently of the reasoning pick
    /// (see CompletionScreenUI.BuildLines, which reads EvidenceStateManager state
    /// directly, not the answer logged here) - that is what makes it safe to always
    /// open regardless of whether the gated option is currently pickable.
    /// </summary>
    public void FireSubmissionInitiatedCheckpoints()
    {
        if (SceneTurnover.IsInvestigationBlocked) return;
        if (checkpointSet == null || checkpointSet.checkpoints == null)
        {
            return;
        }

        foreach (var checkpoint in checkpointSet.checkpoints)
        {
            if (!IsEligible(checkpoint) || checkpoint.trigger.type != HypothesisTriggerType.SubmissionInitiated)
            {
                continue;
            }

            int count = EvidenceStateManager.Instance != null
                ? EvidenceStateManager.Instance.CountAtOrAbove(EvidenceStatus.Collected)
                : 0;

            Enqueue(checkpoint, count);
        }

        TryShowNext();
    }

    private bool IsEligible(HypothesisCheckpoint checkpoint)
    {
        return checkpoint != null
            && checkpoint.trigger != null
            && !string.IsNullOrEmpty(checkpoint.id)
            && !firedCheckpointIds.Contains(checkpoint.id);
    }

    private void Enqueue(HypothesisCheckpoint checkpoint, int evidenceCountAtTrigger)
    {
        // Marked fired at enqueue time, not at answer time - otherwise a second
        // status change arriving while the panel is still up would queue it again.
        firedCheckpointIds.Add(checkpoint.id);
        var entry = new PendingCheckpoint
        {
            checkpoint = checkpoint,
            evidenceCountAtTrigger = evidenceCountAtTrigger
        };
        if (checkpoint.trigger.type == HypothesisTriggerType.SubmissionInitiated)
        {
            // EndingManager already owns the submission hold and expects HYP-03
            // to open synchronously before its picker. Earlier deferred questions
            // must not prevent that established handoff.
            var earlier = pending.ToArray();
            pending.Clear(); pending.Enqueue(entry);
            foreach (var previous in earlier) pending.Enqueue(previous);
        }
        else pending.Enqueue(entry);

        Debug.Log($"[HypothesisCheckpointManager] Checkpoint '{checkpoint.id}' triggered ({checkpoint.trigger.type}, count={evidenceCountAtTrigger}).");
    }

    private void TryShowNext()
    {
        if (SceneTestStart.IsPreparing) return;
        if (active != null || pending.Count == 0)
        {
            return;
        }

        if (checkpointUI == null)
        {
            if (!warnedAboutMissingUI)
            {
                Debug.LogWarning($"[HypothesisCheckpointManager] {pending.Count} checkpoint(s) triggered but no HypothesisCheckpointUI is assigned; they will stay queued and unanswered.", this);
                warnedAboutMissingUI = true;
            }
            return;
        }

        var snapshots = ReasoningCheckpointManager.Instance;
        bool submission = pending.Peek().checkpoint.trigger.type == HypothesisTriggerType.SubmissionInitiated;
        if (!submission && snapshots != null && (!snapshots.CanPresent() || snapshots.HasStandalonePending)) return;
        if (checkpointUI.IsShowing || (submission
            ? PlayerUIGate.IsBlockedByOther(EndingManager.Instance) : PlayerUIGate.IsBlocked)) return;
        // An integrated response may have been recorded while this entry waited
        // behind another modal. Discard it instead of reopening or blocking the queue.
        var integrated = snapshots?.IntegratedDefinition(pending.Peek().checkpoint.id);
        if (integrated != null && snapshots.IsAnswered(integrated.id))
        {
            pending.Dequeue();
            TryShowNext();
            return;
        }
        active = pending.Dequeue();
        checkpointUI.Show(active.checkpoint, HandleSubmitted);
    }

    private void HandleSubmitted(string selectedOptionId, string selectedOptionText, string reasoningOptionId, string reasoningOptionText)
    {
        var completed = active;
        active = null;

        if (completed != null)
        {
            LogSubmission(completed, selectedOptionId, selectedOptionText, reasoningOptionId, reasoningOptionText);
            OnCheckpointAnswered?.Invoke(completed.checkpoint.id, selectedOptionId);
        }

        TryShowNext();
    }

    /// <summary>
    /// Writes the answer plus the evidence state around it. Both the count that
    /// satisfied the trigger and the count at the moment of answering are recorded -
    /// they differ whenever a checkpoint sat queued behind another, and the whole
    /// analytical value of this mechanic is knowing what the player had actually
    /// gathered when they committed to a theory.
    /// </summary>
    private void LogSubmission(PendingCheckpoint completed, string selectedOptionId, string selectedOptionText, string reasoningOptionId, string reasoningOptionText)
    {
        var checkpoint = completed.checkpoint;
        var esm = EvidenceStateManager.Instance;

        // Ids are the join key for scoring - stable across an instructor rewording an
        // option's text later, unlike the raw text this used to log alone (see
        // HypothesisOption/ReasoningOption's own comments). Text still travels
        // alongside for anyone reading the raw session log by eye without cross
        // referencing the checkpoint asset.
        var payload = new Dictionary<string, string>
        {
            { "selectedOptionId", selectedOptionId ?? string.Empty },
            { "selectedOptionText", selectedOptionText ?? string.Empty },
            { "reasoningOptionId", reasoningOptionId ?? string.Empty },
            { "reasoningOptionText", reasoningOptionText ?? string.Empty },
            { "triggerType", checkpoint.trigger.type.ToString() },
            { "triggerThresholdStatus", checkpoint.trigger.thresholdStatus.ToString() },
            { "triggerThresholdCount", checkpoint.trigger.thresholdCount.ToString() },
            { "evidenceCountAtTrigger", completed.evidenceCountAtTrigger.ToString() }
        };

        var integrated = ReasoningCheckpointManager.Instance?.IntegratedDefinition(checkpoint.id);
        if (integrated != null)
        {
            // Link completion to the one explicit contextual response. No second
            // legacy justification was selected, so its fields remain empty.
            payload["reasoningCheckpointId"] = integrated.id;
            payload["reasoningResponseId"] = selectedOptionId ?? string.Empty;
            payload["integratedSingleResponse"] = "true";
        }

        if (esm != null)
        {
            // Recomputed at answer time. NotFound is the first entry in the canonical
            // sequence, so counting at-or-above it yields every tracked item - the
            // denominator, without needing another query on EvidenceStateManager.
            payload["evidenceAtThresholdOnSubmit"] = esm.CountAtOrAbove(checkpoint.trigger.thresholdStatus).ToString();
            payload["evidenceCollectedOnSubmit"] = esm.CountAtOrAbove(EvidenceStatus.Collected).ToString();
            payload["evidenceTrackedTotal"] = esm.CountAtOrAbove(EvidenceStatus.NotFound).ToString();
        }

        if (SessionLogger.Instance != null)
        {
            SessionLogger.Instance.LogEvent(SessionEventType.HypothesisSubmitted, checkpoint.id, payload);
        }
        else
        {
            Debug.LogWarning($"[HypothesisCheckpointManager] No SessionLogger.Instance; hypothesis answer for '{checkpoint.id}' was not recorded.", this);
        }

        Debug.Log($"[HypothesisCheckpointManager] '{checkpoint.id}' answered: \"{selectedOptionText}\" (id: {selectedOptionId}).");
    }

    // Ids are the join key for every logged answer, so a blank or duplicated one
    // corrupts the data quietly rather than throwing. Cheap to check once at startup.
    private void ValidateCheckpointIds()
    {
        if (checkpointSet == null || checkpointSet.checkpoints == null)
        {
            return;
        }

        var seen = new HashSet<string>();
        foreach (var checkpoint in checkpointSet.checkpoints)
        {
            if (checkpoint == null)
            {
                continue;
            }

            if (string.IsNullOrEmpty(checkpoint.id))
            {
                Debug.LogWarning($"[HypothesisCheckpointManager] A checkpoint in '{checkpointSet.name}' has no id and will never fire.", checkpointSet);
                continue;
            }

            if (!seen.Add(checkpoint.id))
            {
                Debug.LogWarning($"[HypothesisCheckpointManager] Duplicate checkpoint id '{checkpoint.id}' in '{checkpointSet.name}' - only the first will ever fire.", checkpointSet);
            }
        }
    }
}
