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
    }

    private void OnDisable()
    {
        EvidenceStateManager.OnEvidenceStatusChanged -= HandleEvidenceStatusChanged;
    }

    private void HandleEvidenceStatusChanged(string evidenceId, EvidenceStatus newStatus)
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
            if (count < checkpoint.trigger.thresholdCount)
            {
                continue;
            }

            Enqueue(checkpoint, count);
        }

        TryShowNext();
    }

    /// <summary>
    /// PLACEHOLDER - nothing calls this yet. Fires every not-yet-fired
    /// SubmissionInitiated checkpoint; intended to be called the moment the player
    /// submits their case on the Deduction Board. That hook does not exist:
    /// EvidenceBoardController.SubmitTheory() is wired straight to its Button's
    /// onClick in code and raises no event, so adding one there is a separate change
    /// (deliberately not made as a side effect of this pass). Once it exists, calling
    /// this from it is the whole integration.
    /// </summary>
    public void FireSubmissionInitiatedCheckpoints()
    {
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
        pending.Enqueue(new PendingCheckpoint
        {
            checkpoint = checkpoint,
            evidenceCountAtTrigger = evidenceCountAtTrigger
        });

        Debug.Log($"[HypothesisCheckpointManager] Checkpoint '{checkpoint.id}' triggered ({checkpoint.trigger.type}, count={evidenceCountAtTrigger}).");
    }

    private void TryShowNext()
    {
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

        active = pending.Dequeue();
        checkpointUI.Show(active.checkpoint, HandleSubmitted);
    }

    private void HandleSubmitted(string selectedOption, string reasoningOption)
    {
        var completed = active;
        active = null;

        if (completed != null)
        {
            LogSubmission(completed, selectedOption, reasoningOption);
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
    private void LogSubmission(PendingCheckpoint completed, string selectedOption, string reasoningOption)
    {
        var checkpoint = completed.checkpoint;
        var esm = EvidenceStateManager.Instance;

        // selectedOption and reasoningOption are both logged as the authored option
        // text, deliberately the same shape as each other rather than one text and one
        // index/id. Neither is a stable identifier - see the note in the report/commit:
        // if an instructor rewords an option, past sessions no longer join to it. The
        // fix is to give both option kinds stable author-set ids in one pass, not to
        // make one of them an id now and leave the other as text.
        var payload = new Dictionary<string, string>
        {
            { "selectedOption", selectedOption ?? string.Empty },
            { "reasoningOption", reasoningOption ?? string.Empty },
            { "triggerType", checkpoint.trigger.type.ToString() },
            { "triggerThresholdStatus", checkpoint.trigger.thresholdStatus.ToString() },
            { "triggerThresholdCount", checkpoint.trigger.thresholdCount.ToString() },
            { "evidenceCountAtTrigger", completed.evidenceCountAtTrigger.ToString() }
        };

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

        Debug.Log($"[HypothesisCheckpointManager] '{checkpoint.id}' answered: \"{selectedOption}\".");
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
