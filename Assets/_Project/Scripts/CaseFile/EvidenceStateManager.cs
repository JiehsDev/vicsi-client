// Assets/_Project/Scripts/CaseFile/EvidenceStateManager.cs
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Outcome of a SetStatus/MarkX call, distinguished so a tool script can tell the
/// difference between "nothing to report" and "block this action and tell the
/// player why": Applied (the transition happened), Duplicate (a harmless
/// repeat/backward call - e.g. a proximity trigger re-firing, or a tool re-reporting
/// a step already completed - deliberately not logged as a violation), and Violation
/// (a required step was skipped, or the evidenceId isn't registered - blocked, and
/// logged clearly).
/// </summary>
public enum TransitionResult
{
    Applied,
    Duplicate,
    Violation
}

/// <summary>
/// Outcome of trying to pull a placed evidence tent back off an item.
/// See EvidenceStateManager.TryReclaimMarker for the rule and its rationale.
/// </summary>
public enum ReclaimResult
{
    /// <summary>Status was exactly Marked; it has been reverted to Found and the tent may be removed.</summary>
    Reverted,

    /// <summary>Status is Photographed or later - documentation already happened with this marker in frame. The tent stays.</summary>
    BlockedDocumented,

    /// <summary>Nothing to revert (item unknown, or never reached Marked). The tent may be removed; no state changed.</summary>
    NoChange
}

public class EvidenceStateManager : MonoBehaviour
{
    public static EvidenceStateManager Instance { get; private set; }

    [Tooltip("All evidence definitions used in this scenario — drag every EvidenceDefinition asset here.")]
    [SerializeField] private List<EvidenceDefinition> sceneEvidenceDefinitions;

    /// <summary>
    /// TEMPORARY bypass, not a removal. When true, a successful MarkPhotographed
    /// immediately and automatically calls MarkSketched for the same item with the
    /// same tool - no separate SketchTool interaction, and critically no
    /// MasterSketchManager.RecordAnnotation call, since there was no player action to
    /// derive a position from. Exists because tenting has a clear, distinct
    /// assessment signal (a judgment call about identity) and sketching, as currently
    /// scoped, does not yet have one of its own separate from documentation that's
    /// already covered by Photographed/Logged - rather than ship a step whose purpose
    /// is unresolved, it's bypassed until that's decided.
    ///
    /// MasterSketchManager/MasterSketchUI/SketchTool are completely untouched by this
    /// flag. Flipping it back to false makes the real interaction the only way
    /// Sketched is ever reached again, exactly as it worked before this flag existed.
    /// </summary>
    [Tooltip("TEMPORARY: when true, Sketched fires automatically right after a successful Photographed, with no SketchTool interaction and no master-sketch annotation. Flip to false to require the real sketchpad interaction again (unchanged, still fully built). Default true - the sketch step's own assessment signal is still undecided.")]
    [SerializeField] private bool autoSketchAfterPhotograph = true;

    /// <summary>Current value of the temporary autoSketchAfterPhotograph bypass - see its field comment.</summary>
    public bool AutoSketchAfterPhotograph => autoSketchAfterPhotograph;

    private readonly Dictionary<string, EvidenceRecord> records = new();

    // Anything (STCS, ProceduralGate, DeductionBoard, Logger) can subscribe to this
        public static event Action<string, EvidenceStatus> OnEvidenceStatusChanged;

    /// <summary>
    /// Fired when a real skip-violation is blocked (evidenceId, attemptedStatus,
    /// currentStatus) - a genuine attempt to skip a required step. Deliberately NOT
    /// fired for harmless duplicate/backward calls (see TransitionResult.Duplicate -
    /// those are expected and not diagnostically interesting), and not fired for an
    /// unregistered evidenceId (that's a data/config problem, already surfaced via
    /// the LogWarning above it, not a gameplay block).
    /// </summary>
    public static event Action<string, EvidenceStatus, EvidenceStatus> OnEvidenceTransitionBlocked;

    // Explicit, ordered required sequence for every evidence item's lifecycle -
    // written out independently of EvidenceStatus's own declaration order (even
    // though the two currently match) so a future reorder/insert on the enum can't
    // silently change gating behaviour with no compiler warning.
    //
    // Marked sits between Found and Photographed on purpose: a real examiner sets the
    // numbered tent BEFORE photographing, so the marker appears in the photograph.
    // Because every tool checks CanTransition against this list rather than naming a
    // predecessor status directly, inserting Marked here is all it took to make
    // PhotographTool require it - no tool needed special-casing.
    private static readonly EvidenceStatus[] RequiredSequence =
    {
        EvidenceStatus.NotFound,
        EvidenceStatus.Found,
        EvidenceStatus.Marked,
        EvidenceStatus.Photographed,
        EvidenceStatus.Sketched,
        EvidenceStatus.Logged,
        EvidenceStatus.ReadyForCollection,
        EvidenceStatus.Collected,
        EvidenceStatus.Sealed,
        EvidenceStatus.Processed
    };

    /// <summary>True only when target is exactly the next required status after current.</summary>
    public static bool IsValidNextStep(EvidenceStatus current, EvidenceStatus target)
    {
        int currentIndex = Array.IndexOf(RequiredSequence, current);
        int targetIndex = Array.IndexOf(RequiredSequence, target);
        return currentIndex >= 0 && targetIndex == currentIndex + 1;
    }

    /// <summary>What should happen next after current, or null if current is already the last step in the sequence.</summary>
    public static EvidenceStatus? GetNextRequiredStatus(EvidenceStatus current)
    {
        int currentIndex = Array.IndexOf(RequiredSequence, current);
        if (currentIndex < 0 || currentIndex + 1 >= RequiredSequence.Length)
        {
            return null;
        }
        return RequiredSequence[currentIndex + 1];
    }

    private void Awake()
    {
        Instance = this;

        foreach (var def in sceneEvidenceDefinitions)
        {
            records[def.evidenceId] = new EvidenceRecord { definition = def };
        }
    }

    public EvidenceRecord GetRecord(string evidenceId)
    {
        return records.TryGetValue(evidenceId, out var record) ? record : null;
    }

    public bool IsReadyForCollection(string evidenceId)
    {
        var record = GetRecord(evidenceId);
        return record != null && record.status == EvidenceStatus.ReadyForCollection;
    }

    /// <summary>
    /// Whether this specific item may advance to target right now. This is the
    /// item-aware check tools and ProceduralGateValidator should use, as opposed to
    /// the static IsValidNextStep, which only knows the shared sequence: some
    /// preconditions depend on the item's own definition rather than on its position
    /// in the sequence. Today that means requiresFingerprinting gating Processed.
    /// </summary>
    public bool CanAdvanceTo(string evidenceId, EvidenceStatus target)
    {
        var record = GetRecord(evidenceId);
        if (record == null || !IsValidNextStep(record.status, target))
        {
            return false;
        }
        return !IsBlockedByFingerprinting(record, target);
    }

    /// <summary>
    /// Why CanAdvanceTo is false for an item-specific reason, or null if no
    /// item-specific rule is blocking. Sequence-order reasons stay
    /// ProceduralGateValidator's job; this covers only the per-item preconditions
    /// that it cannot see from the shared sequence alone.
    /// </summary>
    public string GetItemBlockReason(string evidenceId, EvidenceStatus target)
    {
        var record = GetRecord(evidenceId);
        if (record != null && IsBlockedByFingerprinting(record, target))
        {
            return "Fingerprint processing required first.";
        }
        return null;
    }

    private static bool IsBlockedByFingerprinting(EvidenceRecord record, EvidenceStatus target)
    {
        return target == EvidenceStatus.Processed
            && record.definition != null
            && record.definition.requiresFingerprinting
            && !record.fingerprintingDone;
    }

    /// <summary>
    /// How many registered evidence items have reached at least threshold in the
    /// canonical sequence. A read-only aggregate so external systems
    /// (HypothesisCheckpointManager's count-threshold triggers) can ask "how much has
    /// the player actually gathered" without being handed the records dictionary.
    /// Compares positions in RequiredSequence rather than casting EvidenceStatus to
    /// int, for the same reason IsValidNextStep does: the enum's declaration order is
    /// not the authority here, RequiredSequence is, and a future enum edit must not
    /// silently change what "at or above" means.
    /// </summary>
    public int CountAtOrAbove(EvidenceStatus threshold)
    {
        int thresholdIndex = Array.IndexOf(RequiredSequence, threshold);
        if (thresholdIndex < 0)
        {
            Debug.LogWarning($"[EvidenceStateManager] CountAtOrAbove({threshold}): status is not in RequiredSequence; returning 0.");
            return 0;
        }

        int count = 0;
        foreach (var record in records.Values)
        {
            if (Array.IndexOf(RequiredSequence, record.status) >= thresholdIndex)
            {
                count++;
            }
        }
        return count;
    }

    // --- Transition methods: each one is called by the tool script that performed the action.
    // Return value lets the caller distinguish "nothing to report" from "blocked, tell the player
    // why" - see TransitionResult. Existing callers that ignore the return value are unaffected. ---

    public TransitionResult MarkFound(string evidenceId, ToolType tool) => SetStatus(evidenceId, EvidenceStatus.Found, tool);

    /// <summary>
    /// The player deliberately placed a numbered evidence tent on this item. Distinct
    /// from MarkFound, which fires automatically from proximity: Found means the
    /// player was near it, Marked means the player judged it to be evidence. Only the
    /// second is a claim, which is why evidence-identification scoring reads this one
    /// and not Found.
    ///
    /// tentNumber (1-based) records which physical tent this was, so later steps - the
    /// master sketch annotation - can label the item the same way the player already
    /// sees it in the world. Only written when the transition actually applies, and
    /// only when a real number (&gt;0) is given, so a caller that doesn't have one
    /// (e.g. a diagnostic call) can't stamp a bogus 0 over a number set earlier.
    /// </summary>
    public TransitionResult MarkTented(string evidenceId, ToolType tool, int tentNumber = 0)
    {
        var result = SetStatus(evidenceId, EvidenceStatus.Marked, tool);
        if (result == TransitionResult.Applied && tentNumber > 0)
        {
            var record = GetRecord(evidenceId);
            if (record != null)
            {
                record.tentNumber = tentNumber;
            }
        }
        return result;
    }

    public TransitionResult MarkPhotographed(string evidenceId, ToolType tool)
    {
        var result = SetStatus(evidenceId, EvidenceStatus.Photographed, tool);

        // See autoSketchAfterPhotograph's field comment. Deliberately calls MarkSketched
        // directly rather than anything MasterSketchManager owns - the whole point of
        // this bypass is that no player action happened, so there is no position to
        // stamp and no annotation should appear.
        if (result == TransitionResult.Applied && autoSketchAfterPhotograph)
        {
            MarkSketched(evidenceId, tool);
        }

        return result;
    }

    public TransitionResult MarkSketched(string evidenceId, ToolType tool) => SetStatus(evidenceId, EvidenceStatus.Sketched, tool);

    public TransitionResult MarkLogged(string evidenceId, ToolType tool)
    {
        var result = SetStatus(evidenceId, EvidenceStatus.Logged, tool);
        CheckReadyForCollection(evidenceId);
        return result;
    }

    public TransitionResult MarkCollected(string evidenceId, ToolType tool) => SetStatus(evidenceId, EvidenceStatus.Collected, tool);

    /// <summary>
    /// A tamper-evident seal has been applied to the collected item at the scene.
    ///
    /// Sits between Collected and Processed because that is where it sits in real
    /// procedure: the item is bagged, sealed on site, and the seal is broken later at
    /// the lab to analyse it. Sealing is the step that actually protects custody over
    /// that gap, so a lifecycle that went straight from Collected to Processed had no
    /// representation of chain of custody at all.
    ///
    /// Deliberately a status in the shared sequence rather than a per-item flag like
    /// fingerprintingDone: every item that is collected must be sealed, whereas only
    /// some items need fingerprinting. That is the same test used when
    /// requiresFingerprinting was added - shared step, shared sequence.
    /// </summary>
    public TransitionResult MarkSealed(string evidenceId, ToolType tool) => SetStatus(evidenceId, EvidenceStatus.Sealed, tool);

    public TransitionResult MarkProcessed(string evidenceId, ToolType tool) => SetStatus(evidenceId, EvidenceStatus.Processed, tool);

    /// <summary>
    /// Records that fingerprint processing has been performed on a sealed item.
    /// Deliberately not a status of its own: only some items require it, so it is a
    /// per-item flag that becomes an extra precondition on Sealed -> Processed
    /// rather than a step every item would have to walk through. Must happen while
    /// the item is Sealed - see the comment on the status check below for why that is
    /// Sealed and not Collected.
    /// </summary>
    public TransitionResult MarkFingerprinted(string evidenceId, ToolType tool)
    {
        var record = GetRecord(evidenceId);
        if (record == null)
        {
            Debug.LogWarning($"[EvidenceStateManager] Unknown evidenceId: {evidenceId}");
            return TransitionResult.Violation;
        }

        if (record.definition != null && !record.definition.requiresFingerprinting)
        {
            // Harmless: this item simply doesn't need it. Not a procedural error.
            return TransitionResult.Duplicate;
        }

        if (record.fingerprintingDone)
        {
            return TransitionResult.Duplicate;
        }

        // Sealed, not Collected. Fingerprinting is the last thing before Processed, and
        // Sealed is now what immediately precedes Processed, so the prerequisite moved
        // with it. Reading off Collected instead would create TWO gates on the way to
        // Processed that could be satisfied in either order - fingerprint-then-seal, or
        // seal-then-fingerprint - and a chain of custody that can be established after
        // the item was already opened and dusted is not a chain of custody. The path is
        // strictly Collected -> Sealed -> (fingerprint if required) -> Processed.
        if (record.status != EvidenceStatus.Sealed)
        {
            Debug.LogWarning($"[EvidenceStateManager] Blocked fingerprinting of {evidenceId}: item is {record.status}, must be Sealed first.");
            OnEvidenceTransitionBlocked?.Invoke(evidenceId, EvidenceStatus.Processed, record.status);
            return TransitionResult.Violation;
        }

        record.fingerprintingDone = true;
        record.lastToolUsed = tool;
        Debug.Log($"[EvidenceStateManager] {evidenceId} fingerprinted (via {tool}).");
        return TransitionResult.Applied;
    }

    /// <summary>
    /// Try to pull a placed evidence tent back off an item.
    ///
    /// Allowed only while the item is exactly Marked - i.e. before anything has been
    /// documented with that marker in frame. That is a legitimate correction: the
    /// player looked closer and decided this isn't evidence. Once the item is
    /// Photographed or later, the marker is already in the record and the reclaim is
    /// refused outright, matching every other one-way gate in the project.
    ///
    /// LOAD-BEARING ASSUMPTION FOR SCORING: reverting here rewinds STATE only. The
    /// original MarkTented / NonEvidenceMarked event stays in the session log exactly
    /// as it happened, and a MarkerReclaimed event is appended beside it - nothing is
    /// deleted or rewritten. Scoring is therefore expected to read the full event
    /// history, NOT a final-state snapshot taken at submission. If it ever reads only
    /// the end state, a player could tent everything, see what reacts, quietly walk
    /// back the wrong ones, and score as though they had identified correctly the
    /// first time - which would erase exactly the identification signal this mechanic
    /// exists to capture.
    /// </summary>
    public ReclaimResult TryReclaimMarker(string evidenceId, ToolType tool)
    {
        var record = GetRecord(evidenceId);
        if (record == null)
        {
            return ReclaimResult.NoChange;
        }

        int currentIndex = Array.IndexOf(RequiredSequence, record.status);
        int markedIndex = Array.IndexOf(RequiredSequence, EvidenceStatus.Marked);

        if (currentIndex > markedIndex)
        {
            Debug.LogWarning($"[EvidenceStateManager] Blocked marker reclaim on {evidenceId}: already {record.status}.");
            OnEvidenceTransitionBlocked?.Invoke(evidenceId, EvidenceStatus.Found, record.status);
            return ReclaimResult.BlockedDocumented;
        }

        if (currentIndex < markedIndex)
        {
            return ReclaimResult.NoChange;
        }

        record.status = EvidenceStatus.Found;
        record.lastToolUsed = tool;
        record.statusChangedAtTime = Time.time;
        record.tentNumber = null; // the physical tent is gone; nothing to label with anymore

        OnEvidenceStatusChanged?.Invoke(evidenceId, EvidenceStatus.Found);
        Debug.Log($"[EvidenceStateManager] {evidenceId} -> Found (marker reclaimed via {tool})");
        return ReclaimResult.Reverted;
    }

    private void CheckReadyForCollection(string evidenceId)
    {
        var record = GetRecord(evidenceId);
        if (record == null) return;

        // For MVP: "ready" once Logged has happened (which implies Photographed + Sketched
        // already occurred, since SetStatus below now enforces that order on the way in).
        if (record.status == EvidenceStatus.Logged)
        {
            SetStatus(evidenceId, EvidenceStatus.ReadyForCollection, record.lastToolUsed);
        }
    }

    private TransitionResult SetStatus(string evidenceId, EvidenceStatus newStatus, ToolType tool)
    {
        var record = GetRecord(evidenceId);
        if (record == null)
        {
            Debug.LogWarning($"[EvidenceStateManager] Unknown evidenceId: {evidenceId}");
            return TransitionResult.Violation;
        }

        if (!IsValidNextStep(record.status, newStatus))
        {
            int currentIndex = Array.IndexOf(RequiredSequence, record.status);
            int targetIndex = Array.IndexOf(RequiredSequence, newStatus);

            if (targetIndex <= currentIndex)
            {
                // Duplicate or backward call - harmless (a proximity trigger re-firing,
                // a tool re-reporting a step already completed). Expected, happens often,
                // and deliberately NOT logged as a violation.
                return TransitionResult.Duplicate;
            }

            // A required step was skipped - block it, leave record.status untouched.
            var nextRequired = GetNextRequiredStatus(record.status);
            Debug.LogWarning($"[EvidenceStateManager] Blocked {evidenceId}: {record.status} -> {newStatus} skips a required step (needs {nextRequired} first).");
            OnEvidenceTransitionBlocked?.Invoke(evidenceId, newStatus, record.status);
            return TransitionResult.Violation;
        }

        // Sequence order is satisfied, but an item-specific precondition may still
        // block - today that is requiresFingerprinting standing between Collected and
        // Processed. Checked here as well as in CanAdvanceTo so a tool that forgets to
        // pre-check still cannot skip it.
        if (IsBlockedByFingerprinting(record, newStatus))
        {
            Debug.LogWarning($"[EvidenceStateManager] Blocked {evidenceId}: {newStatus} requires fingerprint processing first.");
            OnEvidenceTransitionBlocked?.Invoke(evidenceId, newStatus, record.status);
            return TransitionResult.Violation;
        }

        record.status = newStatus;
        record.lastToolUsed = tool;
        record.statusChangedAtTime = Time.time;

        OnEvidenceStatusChanged?.Invoke(evidenceId, newStatus);
        Debug.Log($"[EvidenceStateManager] {evidenceId} → {newStatus} (via {tool})");
        return TransitionResult.Applied;
    }

#if UNITY_EDITOR
    /// <summary>
    /// Author-time nudge: a scenario where every item is still the default Neutral has
    /// almost certainly not been classified yet, rather than being a scenario in which
    /// nothing discriminates. A warning, never a failure - Neutral is a legitimate
    /// value and a small test scene may genuinely be all-Neutral, which is why this
    /// only fires once there are enough items for the omission to matter.
    /// </summary>
    private void OnValidate()
    {
        const int MinimumItemsWorthClassifying = 3;

        if (sceneEvidenceDefinitions == null || sceneEvidenceDefinitions.Count < MinimumItemsWorthClassifying)
        {
            return;
        }

        // Count what was actually inspected. OnValidate also runs mid-domain-reload,
        // when these asset references can still be null - treating "couldn't tell" as
        // "everything is Neutral" made this warn on a correctly-classified scene.
        int inspected = 0;
        foreach (var def in sceneEvidenceDefinitions)
        {
            if (def == null)
            {
                continue;
            }

            if (def.relevance != EvidenceRelevance.Neutral)
            {
                return;
            }

            inspected++;
        }

        if (inspected < MinimumItemsWorthClassifying)
        {
            return;
        }

        Debug.LogWarning($"[EvidenceStateManager] All {inspected} evidence items in this scene are still EvidenceRelevance.Neutral. Scoring cannot distinguish a correct identification from a false positive until at least some are classified.", this);
    }
#endif
}
