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
    /// <summary>Status was exactly Marked; it has been reverted to whatever immediately precedes Marked in RequiredSequence and the tent may be removed.</summary>
    Reverted,

    /// <summary>Status is PhotographedFinal or later - documentation already happened with this marker in frame. The tent stays.</summary>
    BlockedDocumented,

    /// <summary>Nothing to revert (item unknown, or never reached Marked). The tent may be removed; no state changed.</summary>
    NoChange
}

/// <summary>
/// A print's lab-identification status - DECOUPLED from EvidenceStatus/RequiredSequence
/// on purpose. Whether a lifted print has been physically sealed into evidence and
/// whether it's been IDENTIFIED are different facts: the item's chain of custody
/// (Collected -> Sealed -> Processed) tracks physical handling and must not wait on an
/// external lab turnaround, while this tracks the print's own, separate investigative
/// thread. See EvidenceStateManager.MarkSentToLab/MarkMatched - neither one ever reads
/// or writes record.status, and nothing in the main chain (CanTransition, CheckProcessed,
/// RequiredSequence) knows this enum exists. First of its kind in this project: the
/// closest existing precedent is fingerprintingDone/swabbingDone (per-item flags on
/// EvidenceRecord, independent of status) - this generalizes that same "field beside
/// status, not part of it" idea from a single flag to a small ordered side-track, since
/// "sent" and "matched" are two distinct, orderable facts rather than one.
/// </summary>
public enum FingerprintLabStatus
{
    // Explicit ints, matching the existing implicit order exactly (no behavior change) -
    // hardened ahead of RequirementNode/RequirementType.FingerprintLabStatusAtOrAbove
    // persisting this enum into new authored data, the same discipline EvidenceStatus's
    // own comment documents and already regretted not having from the start.
    NotSent = 0,
    SentToLab = 1,
    Matched = 2
}

public class EvidenceStateManager : MonoBehaviour
{
    public static EvidenceStateManager Instance { get; private set; }

    [Tooltip("All evidence definitions used in this scenario â€” drag every EvidenceDefinition asset here.")]
    [SerializeField] private List<EvidenceDefinition> sceneEvidenceDefinitions;

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

    /// <summary>
    /// Fired the moment MarkSwabbed actually applies (not on a harmless Duplicate
    /// re-call). Swabbing is a per-item flag, not a status in RequiredSequence, so it
    /// never fires OnEvidenceStatusChanged on its own - anything that needs to react
    /// to "the required blood trace was just swabbed" specifically (EvidenceGrabGate,
    /// so a knife with blood on it can't be picked up before the trace is taken) needs
    /// this event instead, since the item's status usually doesn't change at all at
    /// the moment swabbing happens (it stays ReadyForCollection either way).
    /// </summary>
    public static event Action<string> OnEvidenceSwabbed;
    public static event Action<string> OnBodyProcedureChanged;

    /// <summary>
    /// Fired the moment MarkFingerprinted actually applies (not on a harmless
    /// Duplicate re-call) - the fingerprintingDone mirror of OnEvidenceSwabbed above,
    /// for the identical reason: fingerprintingDone is a per-item flag, not a status
    /// in RequiredSequence, so it never fires OnEvidenceStatusChanged on its own. Added
    /// for EvidenceGrabGate, whose fingerprintBeforeCollection precondition (the
    /// knife) otherwise has no way to learn that the flag it's waiting on just
    /// flipped true - the item's status stays ReadyForCollection either way, exactly
    /// like the swab case.
    /// </summary>
    public static event Action<string> OnEvidenceFingerprinted;

    /// <summary>
    /// Fired on every real (non-Duplicate) transition of the decoupled fingerprint lab
    /// side-track - see FingerprintLabStatus's own class comment for why this is
    /// separate from OnEvidenceStatusChanged. Not subscribed to by anything yet; exists
    /// as the extensibility point a future STCS reveal trigger or a future
    /// SelectionPrerequisite integration would hook, the same "publish an event, don't
    /// special-case a caller" shape as every other event on this class.
    /// </summary>
    public static event Action<string, FingerprintLabStatus> OnFingerprintLabStatusChanged;

    /// <summary>
    /// Who evidenceId's print identifies - EvidenceDefinition.fingerprintMatchIdentity,
    /// authored per item (still a placeholder for a real suspect database, but data
    /// rather than a code table). Null when the item has no authored identity.
    /// </summary>
    public string GetFingerprintMatchIdentity(string evidenceId)
    {
        var identity = GetRecord(evidenceId)?.definition?.fingerprintMatchIdentity;
        return string.IsNullOrWhiteSpace(identity) ? null : identity;
    }

    private string GetPlaceholderMatchTarget(string evidenceId) =>
        GetFingerprintMatchIdentity(evidenceId) ?? "Unidentified (no placeholder authored for this item)";

    public List<string> GetFingerprintMatchIdentities(string evidenceId)
    {
        var definition = GetRecord(evidenceId)?.definition;
        var result = new List<string>();
        if (definition?.fingerprintMatchIdentities != null)
            foreach (var identity in definition.fingerprintMatchIdentities)
                if (!string.IsNullOrWhiteSpace(identity) && !result.Contains(identity.Trim())) result.Add(identity.Trim());
        if (result.Count == 0 && GetFingerprintMatchIdentity(evidenceId) is string single) result.Add(single);
        return result;
    }

    [Serializable] private class FingerprintMatchesPayload { public List<string> identities; }

    // Explicit, ordered required sequence for every evidence item's lifecycle -
    // written out independently of EvidenceStatus's own declaration order (even
    // though the two currently match) so a future reorder/insert on the enum can't
    // silently change gating behaviour with no compiler warning.
    //
    // Documentation-flow restructure: a real examiner photographs the undisturbed
    // scene FIRST (PhotographedRaw), then chalks an outline around the item
    // (ChalkMarked), then sets the numbered tent on top of that outline (Marked),
    // then takes a second photograph with the tent in frame (PhotographedFinal) -
    // two photographs of the same item, at two different, meaningful moments, not
    // one. Sketched is retired outright, not kept alongside this - it added no
    // assessment signal of its own once ChalkMarked and the two photo states exist.
    // Because every tool checks CanTransition against this list rather than naming a
    // predecessor status directly, this reorder is all it took to change what every
    // tool requires - no tool needed special-casing beyond PhotographTool itself
    // (which now has two possible outcomes instead of one - see its own comment).
    private static readonly EvidenceStatus[] RequiredSequence =
    {
        EvidenceStatus.NotFound,
        EvidenceStatus.Found,
        EvidenceStatus.PhotographedRaw,
        EvidenceStatus.ChalkMarked,
        EvidenceStatus.Marked,
        EvidenceStatus.PhotographedFinal,
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

    /// <summary>
    /// Player-facing sentence describing what step comes next after current, e.g.
    /// GameDialogue.Text("evidence-step-photograph-first") - the single, shared translation
    /// from EvidenceStatus to plain language. Every tool's "not ready yet" refusal
    /// (ProceduralGateValidator.GetBlockReason, FingerprintKitTool/SwabTool's own
    /// GetNotReadyReason) used to each build this string independently as
    /// $"Not yet {next}." - a raw enum name shown directly to the player - and all
    /// three had to be fixed the same way once found, exactly the kind of drift this
    /// project's own "one authoritative place" convention exists to prevent (see e.g.
    /// SelectionPrerequisite.AllSatisfied's own comment). Deliberately generic - no
    /// evidenceId, no per-item wording - since what differs between items belongs in
    /// EvidenceStateManager.GetItemBlockReason instead. Internal debug/session logs
    /// (DescribeNextStep, used only in Debug.Log calls) are untouched by this - this
    /// method is for player-visible text only.
    /// </summary>
    /// <summary>
    /// The next required status for this specific item - GetNextRequiredStatus, except
    /// that ChalkMarked is bypassed (PhotographedRaw -> Marked) when the item's definition
    /// says requiresChalk = false. The enum and RequiredSequence are unchanged; a bypassed
    /// item simply never holds ChalkMarked. Every item-aware gate uses this.
    /// </summary>
    public static EvidenceStatus? GetNextRequiredStatus(EvidenceRecord record)
    {
        if (record == null)
        {
            return null;
        }

        var next = GetNextRequiredStatus(record.status);
        if (next == EvidenceStatus.ChalkMarked && record.definition != null && !record.definition.requiresChalk)
        {
            return EvidenceStatus.Marked;
        }
        return next;
    }

    /// <summary>Item-aware IsValidNextStep - see GetNextRequiredStatus(EvidenceRecord).</summary>
    public static bool IsValidNextStep(EvidenceRecord record, EvidenceStatus target) =>
        record != null && GetNextRequiredStatus(record) == target;

    /// <summary>Item-aware DescribeNextStepForPlayer - see GetNextRequiredStatus(EvidenceRecord).</summary>
    public static string DescribeNextStepForPlayer(EvidenceRecord record)
    {
        if (record == null)
        {
            return DescribeNextStepForPlayer(EvidenceStatus.Processed);
        }
        if (record.definition?.requiresBodyRemoval == true)
        {
            if (record.status == EvidenceStatus.Marked)
                return "Complete the final body documentation photograph with Body Marker A in frame.";
            if (record.status == EvidenceStatus.PhotographedFinal)
                return MeasurementStateManager.Instance == null || !MeasurementStateManager.Instance.GetEvidenceMeasurementStatus(record.definition.evidenceId).IsComplete
                    ? "Measure and document the body position before recording BODY-A findings."
                    : "Record BODY-A findings with the Recorder.";
        }
        var next = GetNextRequiredStatus(record);
        return next == EvidenceStatus.Marked && record.status == EvidenceStatus.PhotographedRaw
            ? GameDialogue.Text("evidence-step-marker-first")
            : DescribeNextStepForPlayer(record.status);
    }

    public static string DescribeNextStepForPlayer(EvidenceStatus current)
    {
        var next = GetNextRequiredStatus(current);
        if (!next.HasValue)
        {
            return GameDialogue.Text("evidence-step-none-needed");
        }

        switch (next.Value)
        {
            case EvidenceStatus.Found:
                return GameDialogue.Text("evidence-step-find-first");
            case EvidenceStatus.PhotographedRaw:
                return GameDialogue.Text("evidence-step-photograph-first");
            case EvidenceStatus.ChalkMarked:
                return GameDialogue.Text("evidence-step-chalk-first");
            case EvidenceStatus.Marked:
                return GameDialogue.Text("evidence-step-marker-first");
            case EvidenceStatus.PhotographedFinal:
                return GameDialogue.Text("evidence-step-final-photo-first");
            case EvidenceStatus.Logged:
                return GameDialogue.Text("evidence-step-documentation-first");
            case EvidenceStatus.ReadyForCollection:
                return GameDialogue.Text("evidence-step-not-ready-for-collection");
            case EvidenceStatus.Collected:
                return GameDialogue.Text("evidence-step-collect-first");
            case EvidenceStatus.Sealed:
                return GameDialogue.Text("evidence-step-seal-first");
            case EvidenceStatus.Processed:
                return GameDialogue.Text("evidence-step-processing-needed");
            default:
                return GameDialogue.Text("evidence-step-procedure-first");
        }
    }

    private void Awake()
    {
        Instance = this;

        if (sceneEvidenceDefinitions == null)
        {
            Debug.LogError("[EvidenceStateManager] No scenario evidence definitions assigned.", this);
            return;
        }

        foreach (var def in sceneEvidenceDefinitions)
        {
            if (def == null || string.IsNullOrWhiteSpace(def.evidenceId) || records.ContainsKey(def.evidenceId))
            {
                Debug.LogError("[EvidenceStateManager] Null, blank or duplicate evidence definition; entry ignored.", this);
                continue;
            }
            records.Add(def.evidenceId, new EvidenceRecord { definition = def });
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public EvidenceRecord GetRecord(string evidenceId)
    {
        return evidenceId != null && records.TryGetValue(evidenceId, out var record) ? record : null;
    }

    /// <summary>Every tracked EvidenceRecord, in no particular order - for post-session review/debug aggregation (e.g. "X/Y evidence discovered"). Same read-only convention as FindingStateManager/RelationshipStateManager/ConclusionStateManager.AllRecords. Read-only - do not mutate via this enumeration.</summary>
    public IEnumerable<EvidenceRecord> AllRecords => records.Values;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    internal void PrepareDevelopmentStart(string collectionReadyId = null, bool prepareFingerprint = false, bool awaitFingerprintComparison = false)
    {
        SceneTestStart.RequirePreparation();
        int number = 4;
        foreach (var record in records.Values)
        {
            var def = record.definition;
            if (collectionReadyId != null && def.evidenceId != collectionReadyId) continue;
            record.status = prepareFingerprint ? EvidenceStatus.PhotographedFinal : collectionReadyId != null ? EvidenceStatus.ReadyForCollection
                : def.examinedInPlace ? EvidenceStatus.Logged : EvidenceStatus.Processed;
            record.statusChangedAtTime = Time.time;
            record.fingerprintingDone = record.fingerprintLifted = !prepareFingerprint && def.requiresFingerprinting;
            record.fingerprintLabStatus = !record.fingerprintingDone ? FingerprintLabStatus.NotSent
                : awaitFingerprintComparison ? FingerprintLabStatus.SentToLab : FingerprintLabStatus.Matched;
            record.fingerprintLabMatchResult = record.fingerprintingDone&&!awaitFingerprintComparison ? GetFingerprintMatchIdentity(def.evidenceId) : null;
            record.fingerprintLabMatchIdentities = record.fingerprintingDone&&!awaitFingerprintComparison ? GetFingerprintMatchIdentities(def.evidenceId) : new List<string>();
            record.swabbingDone = record.swabSampleSealed = def.requiresSwabbing;
            record.isFlipped = def.requiresFlipBeforeFingerprinting;
            record.bodyFindingsRecorded = def.examinedInPlace;
            if (def.examinedInPlace) record.tentLetter = "A";
            else record.tentNumber = EvidenceDisplay.Number(def.evidenceId) > 0 ? EvidenceDisplay.Number(def.evidenceId) : number++;
            if (def.requiresTraceMarker) record.traceTentNumber = number++;
            if (def.requiresBodyRemoval) record.bodyRemovalStatus = BodyRemovalStatus.Removed;
            foreach (var line in prepareFingerprint ? new List<ReportLineOption>() : def.reportLineOptions)
                if (line != null && line.isAccurate) record.selectedReportLineIds.Add(line.id);
        }
        foreach (var record in records.Values)
        {
            string id = record.definition.evidenceId;
            if (collectionReadyId != null && id != collectionReadyId) continue;
            OnEvidenceStatusChanged?.Invoke(id, record.status);
            OnFingerprintLabStatusChanged?.Invoke(id, record.fingerprintLabStatus);
            OnBodyProcedureChanged?.Invoke(id);
            foreach (var line in record.selectedReportLineIds) OnReportLineSelected?.Invoke(id, line);
        }
    }
#endif

    /// <summary>
    /// Player-facing display name for evidenceId, resolved from its own
    /// EvidenceDefinition - the single shared place every notification-building call
    /// site should go through instead of interpolating the raw id directly. Confirmed
    /// as a real, repeated pattern across EvidenceNotifier, EvidenceFlipTool,
    /// FingerprintKitTool, FingerprintStationTool and SwabTool, each independently
    /// building player-facing text like "{evidenceId} sealed" before this existed.
    /// Falls back to evidenceId itself - never throws, never suppresses the
    /// notification - for an id with no registered record, or a record whose
    /// definition/displayName is blank, so a malformed or not-yet-wired id degrades to
    /// exactly what every caller already showed before this method existed, not
    /// silence. Internal systems (session logs, assessment logs, saved state,
    /// relationship data, requirement evaluation, Debug.Log) must keep using the raw
    /// id directly - this is for player-visible text only.
    /// </summary>
    public string GetDisplayName(string evidenceId)
    {
        var record = GetRecord(evidenceId);
        return record?.definition != null ? record.definition.PlayerDisplayName : EvidenceDisplay.Label(evidenceId);
    }

    /// <summary>
    /// Fired once per NEWLY recorded selection (evidenceId, lineId) - not for a
    /// repeat/duplicate line, same "don't over-notify on a harmless re-call" shape as
    /// OnEvidenceSwabbed only firing on a real Applied transition. Exists so
    /// HypothesisCheckpointManager (or anything else gating on a prerequisite - see
    /// SelectionPrerequisite) can re-evaluate firing eligibility the moment a
    /// prerequisite becomes satisfied, rather than only on the next evidence-status
    /// change - a checkpoint whose count threshold was already met earlier has to
    /// fire RETROACTIVELY the instant its missing prerequisite clears, and nothing
    /// else in this class's event surface would tell a listener that happened.
    /// </summary>
    public static event System.Action<string, string> OnReportLineSelected;

    /// <summary>
    /// Records that lineIds were confirmed as selected on evidenceId's report - the
    /// runtime side of the cross-item unlock mechanism (see SelectionPrerequisite).
    /// Called from RecorderTool.HandleReportConfirmed right alongside the existing
    /// ReportLinesSelected session-log event, same data and same moment - this just
    /// also keeps it queryable in memory for the rest of the session via
    /// HasSelectedReportLine, rather than only ever being written to disk. A no-op for
    /// an unregistered evidenceId (same defensive shape as every other per-item method
    /// here), since a caller should never be able to corrupt state by passing a bad id.
    /// </summary>
    public void RecordReportLinesSelected(string evidenceId, IEnumerable<string> lineIds)
    {
        if (ProcedureInterlock.BlocksUnrelatedActions) return;
        if (SceneTurnover.IsInvestigationBlocked) return;
        var record = GetRecord(evidenceId);
        if (record == null || lineIds == null)
        {
            return;
        }

        var submittedIds = new List<string>(lineIds);
        if (record.recorderDocumentation != null)
        {
            var frozenIds = new HashSet<string>();
            foreach (var statement in record.recorderDocumentation.statements)
                if (statement.selected) frozenIds.Add(statement.id);
            if (!frozenIds.SetEquals(submittedIds)) return;
        }

        foreach (var lineId in submittedIds)
        {
            if (!string.IsNullOrEmpty(lineId) && record.selectedReportLineIds.Add(lineId))
            {
                OnReportLineSelected?.Invoke(evidenceId, lineId);
            }
        }
    }

    /// <summary>
    /// Whether evidenceId's print has resolved to a lab match - what
    /// SelectionPrerequisite.PrerequisiteType.FingerprintMatched checks against.
    /// Mirrors HasSelectedReportLine's own shape: false for an unregistered
    /// evidenceId, never an exception - a prerequisite that can't be resolved reads
    /// as "not yet satisfied," not a crash.
    /// </summary>
    public bool IsFingerprintMatched(string evidenceId)
    {
        var record = GetRecord(evidenceId);
        return record != null && record.fingerprintLabStatus == FingerprintLabStatus.Matched;
    }

    /// <summary>
    /// Whether evidenceId's print has reached (or passed) threshold on the decoupled lab
    /// side-track - the FingerprintLabStatus counterpart to IsAtOrPastStatus. Ordinal
    /// comparison is correct here (unlike EvidenceStatus): FingerprintLabStatus has no
    /// separate RequiredSequence of its own, and its two real transitions
    /// (MarkSentToLab/MarkMatched) already enforce NotSent -&gt; SentToLab -&gt; Matched as a
    /// strict, small, linear order in code - the enum's declared order IS the sequence.
    /// This is the authoritative query RequirementType.FingerprintLabStatusAtOrAbove
    /// reads, rather than a raw int compare at the call site. False for an unregistered
    /// evidenceId, never an exception - same "unresolved reads as not yet satisfied"
    /// convention as every other query on this class.
    /// </summary>
    public bool IsFingerprintLabStatusAtOrAbove(string evidenceId, FingerprintLabStatus threshold)
    {
        var record = GetRecord(evidenceId);
        return record != null && record.fingerprintLabStatus >= threshold;
    }

    /// <summary>
    /// Whether lineId has been confirmed as selected on evidenceId's report at some
    /// point THIS session - what ReportLineOption.prerequisites (and, once wired,
    /// ReasoningOption's own future prerequisites) check against. False for an
    /// unregistered evidenceId or a lineId never selected, never an exception - a
    /// prerequisite that can't be resolved should read as "not yet satisfied," not
    /// crash the panel trying to open.
    /// </summary>
    public bool HasSelectedReportLine(string evidenceId, string lineId)
    {
        var record = GetRecord(evidenceId);
        return record != null && !string.IsNullOrEmpty(lineId)
            &&(record.selectedReportLineIds.Contains(lineId)||RecorderDocumentation.HasBaselineFinding(record,lineId));
    }

    public bool IsReadyForCollection(string evidenceId)
    {
        var record = GetRecord(evidenceId);
        return record != null && record.status == EvidenceStatus.ReadyForCollection;
    }

    /// <summary>
    /// Whether this item's status is threshold or anything later in RequiredSequence -
    /// as opposed to IsReadyForCollection, which means exactly ReadyForCollection and
    /// goes false again the moment the item advances past it. Exists for gates that
    /// should stay satisfied for the rest of the item's life once a point is reached
    /// (e.g. "may this be swabbed" is true at ReadyForCollection and stays true
    /// through Collected/Sealed/Processed, it does not re-close). Same index
    /// comparison as CountAtOrAbove, just for one item instead of a scene-wide count.
    /// </summary>
    public bool IsAtOrPastStatus(string evidenceId, EvidenceStatus threshold)
    {
        var record = GetRecord(evidenceId);
        if (record == null)
        {
            return false;
        }

        int thresholdIndex = Array.IndexOf(RequiredSequence, threshold);
        int currentIndex = Array.IndexOf(RequiredSequence, record.status);
        return thresholdIndex >= 0 && currentIndex >= thresholdIndex;
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
        if (!ProcedureInterlock.AllowsStatus(evidenceId, target)) return false;
        if (SceneTurnover.IsInvestigationBlocked) return false;
        var record = GetRecord(evidenceId);
        if (record == null || !IsValidNextStep(record, target))
        {
            return false;
        }
        return GetItemBlockReason(evidenceId, target) == null;
    }

    /// <summary>
    /// The one specific thing that still stands between this item and being picked up for
    /// collection, as a player-facing line - or null when it can be collected now (or is
    /// already collected). Reuses the existing per-item block reasons (blood sample, on-scene
    /// print, scene measurements, authored step rules) for the very next transition, then the
    /// sequence's own next-step wording ("Record this evidence with the Recorder..."). Read
    /// only - logs nothing and changes no state; used to tell the player why a grab did nothing.
    /// </summary>
    public string GetCollectionBlockReason(string evidenceId)
    {
        var record = GetRecord(evidenceId);
        if (record == null || IsAtOrPastStatus(evidenceId, EvidenceStatus.Collected))
        {
            return null;
        }
        if (record.definition != null && record.definition.examinedInPlace)
        {
            return GameDialogue.Text("evidence-examined-in-place");
        }
        if (record.status == EvidenceStatus.ReadyForCollection)
        {
            // Swab / on-scene print are enforced before Logged, so normally nothing is left here.
            return GetItemBlockReason(evidenceId, EvidenceStatus.Collected);
        }
        var next = GetNextRequiredStatus(record);
        if (next.HasValue)
        {
            string itemReason = GetItemBlockReason(evidenceId, next.Value);
            if (itemReason != null)
            {
                return itemReason;
            }
        }
        return DescribeNextStepForPlayer(record);
    }

    /// <summary>
    /// Why CanAdvanceTo is false for an item-specific reason, or null if no
    /// item-specific rule is blocking. Sequence-order reasons stay
    /// ProceduralGateValidator's job; this covers only the per-item preconditions
    /// that it cannot see from the shared sequence alone.
    /// </summary>
    public string GetItemBlockReason(string evidenceId, EvidenceStatus target)
    {
        if (!ProcedureInterlock.AllowsStatus(evidenceId, target)) return ProcedureInterlock.Reason;
        var record = GetRecord(evidenceId);
        if (record?.definition?.requiresBodyRemoval == true)
        {
            if (target == EvidenceStatus.PhotographedFinal && (!IsAtOrPastStatus(evidenceId, EvidenceStatus.Marked) || record.tentLetter != "A"))
                return "Place Body Marker A before completing the final body documentation photograph.";
            if (target == EvidenceStatus.Logged && !IsAtOrPastStatus(evidenceId, EvidenceStatus.PhotographedFinal))
                return DescribeNextStepForPlayer(record);
            if (target == EvidenceStatus.Logged && (MeasurementStateManager.Instance == null
                || !MeasurementStateManager.Instance.GetEvidenceMeasurementStatus(evidenceId).IsComplete))
                return "Measure/document the body position and required evidence distances before recording BODY-A findings.";
            if (target == EvidenceStatus.Logged && CrimeSceneSketchManager.GetProcessingBlockReason(evidenceId) != null)
                return GameDialogue.Text("sketch-body-before-recording");
        }
        if (record != null && IsBlockedByInPlaceExamination(record, target))
        {
            return GameDialogue.Text("evidence-examined-in-place");
        }
        if (record != null && IsBlockedByFingerprinting(record, target))
        {
            return GameDialogue.Text("evidence-fingerprint-required-first");
        }
        if (record != null && IsBlockedBySwabbing(record, target))
        {
            return GameDialogue.Text("evidence-blood-sample-before-record");
        }
        if (record != null && IsBlockedByOnSceneFingerprinting(record, target))
        {
            if (record.fingerprintLifted) return GameDialogue.Text("prompt-store-fingerprint-sample");
            return GameDialogue.Text("evidence-fingerprint-before-record");
        }
        if (record != null && IsBlockedByTraceMarker(record, target))
        {
            return GameDialogue.Text("evidence-trace-marker-first");
        }
        var blockingStep = FindUnmetStep(record, target, ProcedureEnforcement.Block);
        if (blockingStep != null)
        {
            return string.IsNullOrEmpty(blockingStep.PlayerMessage) ? GameDialogue.Text("procedure-step-required-first") : blockingStep.PlayerMessage;
        }
        return null;
    }

    /// <summary>
    /// First of record's authored stepRequirements for a transition into target, with the
    /// given enforcement, whose requirement is not currently met - or null. See
    /// ProcedureStepRequirement.
    /// </summary>
    private static ProcedureStepRequirement FindUnmetStep(EvidenceRecord record, EvidenceStatus target, ProcedureEnforcement enforcement)
    {
        var steps = record?.definition?.stepRequirements;
        if (steps == null)
        {
            return null;
        }

        foreach (var step in steps)
        {
            if (step != null && step.action == ProcedureAction.StatusTransition && step.beforeStatus == target && step.enforcement == enforcement
                && !RequirementEvaluator.IsSatisfied(step.requirement))
            {
                return step;
            }
        }
        return null;
    }

    /// <summary>
    /// The Warn-enforced stepRequirements whose requirement is unmet for a transition of
    /// evidenceId into target right now - what the acting tool shows the player at the
    /// action site (the transition itself still goes ahead and logs ProcedureOutOfOrder).
    /// Empty when none apply.
    /// </summary>
    public List<ProcedureStepRequirement> GetUnmetProcedureWarnings(string evidenceId, EvidenceStatus target)
    {
        var result = new List<ProcedureStepRequirement>();
        var steps = GetRecord(evidenceId)?.definition?.stepRequirements;
        if (steps == null)
        {
            return result;
        }

        foreach (var step in steps)
        {
            if (step != null && step.action == ProcedureAction.StatusTransition && step.beforeStatus == target && step.enforcement == ProcedureEnforcement.Warn
                && !RequirementEvaluator.IsSatisfied(step.requirement))
            {
                result.Add(step);
            }
        }
        return result;
    }

    // Warn rules already logged, per "evidenceId|stepId" - ProcedureOutOfOrder fires once
    // per rule per item, not on every retry.
    private readonly HashSet<string> loggedOutOfOrderSteps = new();

    private static bool IsBlockedByInPlaceExamination(EvidenceRecord record, EvidenceStatus target)
    {
        return record.definition != null && record.definition.examinedInPlace
            && Array.IndexOf(RequiredSequence, target) > Array.IndexOf(RequiredSequence, EvidenceStatus.Logged);
    }

    /// <summary>
    /// Gates Processed, not Collected - the DEFAULT fingerprinting shape (most items):
    /// the print is lifted from a sealed container back at the lab, after the item is
    /// already bagged, so this only ever blocks the final step. Does NOT apply to an
    /// item with definition.fingerprintBeforeCollection set (currently only the
    /// gloves, EVD-019) - see IsBlockedByOnSceneFingerprinting for that item's own,
    /// different shape, and MarkFingerprinted for why the two can't both gate the
    /// same item.
    /// </summary>
    private static bool IsBlockedByFingerprinting(EvidenceRecord record, EvidenceStatus target)
    {
        return target == EvidenceStatus.Processed
            && record.definition != null
            && record.definition.requiresFingerprinting
            && !record.definition.fingerprintBeforeCollection
            && !record.fingerprintingDone;
    }

    /// <summary>
    /// Whether target is blocked because this item requires ON-SCENE fingerprinting
    /// (definition.fingerprintBeforeCollection) that hasn't happened yet. Gates
    /// Logged, not Collected or Processed - the mirror image of IsBlockedByFingerprinting's
    /// default shape: for an item whose procedure calls for confirming it's a
    /// candidate weapon before it's ever removed from the scene (the knife), the
    /// print has to come off it before the item is ever logged, not after it's sealed
    /// and back at the lab. Gating Logged rather than Collected is deliberate: report-
    /// line selection (RecorderTool.TryOpenReport/HandleReportConfirmed, both gated on
    /// CanTransition(id, Logged)) should reflect everything actually discovered,
    /// including an on-scene print's lab match, and logging feeds conclusion
    /// availability downstream - so all on-scene discovery must land before Logged,
    /// not merely before the item is physically removed from the scene. Same "gates
    /// Logged" shape as IsBlockedBySwabbing, for the same reason - an on-scene action
    /// has to happen while the item is still on scene, and its findings have to be
    /// available before that item's own report is written.
    /// </summary>
    /// <summary>
    /// Whether target is blocked because this item's trace (the blood on the knife blade)
    /// still needs its own tent: the final photograph must show both tents.
    /// </summary>
    private static bool IsBlockedByTraceMarker(EvidenceRecord record, EvidenceStatus target)
    {
        return target == EvidenceStatus.PhotographedFinal
            && record.definition != null
            && record.definition.requiresTraceMarker
            && !record.traceTentNumber.HasValue;
    }

    /// <summary>
    /// The second tent went down on this item's trace (EvidenceTraceMarkerTarget). Only while
    /// the item itself is exactly Marked - its own tent is down and its final photograph not
    /// yet taken. Duplicate when already placed; Violation otherwise. Logged as
    /// EvidenceTraceMarked.
    /// </summary>
    public TransitionResult MarkTraceTented(string evidenceId, ToolType tool, int tentNumber)
    {
        if (ProcedureInterlock.BlocksUnrelatedActions) return TransitionResult.Violation;
        if (SceneTurnover.IsInvestigationBlocked) return TransitionResult.Violation;
        var record = GetRecord(evidenceId);
        if (record == null || record.definition == null || !record.definition.requiresTraceMarker)
        {
            return TransitionResult.Violation;
        }
        if (record.traceTentNumber.HasValue)
        {
            return TransitionResult.Duplicate;
        }
        if (record.status != EvidenceStatus.Marked)
        {
            return TransitionResult.Violation;
        }
        record.traceTentNumber = tentNumber;
        record.lastToolUsed = tool;
        SessionLogger.Instance?.LogEvent(SessionEventType.EvidenceTraceMarked, evidenceId, new Dictionary<string, string>
        {
            { "tentNumber", tentNumber.ToString() },
            { "itemTentNumber", record.tentNumber.HasValue ? record.tentNumber.Value.ToString() : "" }
        });
        Debug.Log($"[EvidenceStateManager] {evidenceId} trace marked with tent {tentNumber} (via {tool}).");
        return TransitionResult.Applied;
    }

    /// <summary>
    /// The trace tent was picked back up. Reverted (mark cleared) before the final
    /// photograph; BlockedDocumented once the item was photographed with it in frame;
    /// NoChange when there was nothing to clear.
    /// </summary>
    public ReclaimResult TryReclaimTraceMarker(string evidenceId)
    {
        var record = GetRecord(evidenceId);
        if (record == null || !record.traceTentNumber.HasValue)
        {
            return ReclaimResult.NoChange;
        }
        if (IsAtOrPastStatus(evidenceId, EvidenceStatus.PhotographedFinal))
        {
            return ReclaimResult.BlockedDocumented;
        }
        record.traceTentNumber = null;
        return ReclaimResult.Reverted;
    }

    private static bool IsBlockedByOnSceneFingerprinting(EvidenceRecord record, EvidenceStatus target)
    {
        return target == EvidenceStatus.Logged
            && record.definition != null
            && record.definition.fingerprintBeforeCollection
            && !record.fingerprintingDone;
    }

    /// <summary>
    /// Whether target is blocked because this item has a biological trace (see
    /// EvidenceDefinition.requiresSwabbing) that hasn't been swabbed yet. Gates
    /// Logged, not Collected or Processed - the physical sample has to be taken
    /// before the item itself is ever logged (not merely before it's bagged and
    /// removed from the scene), so that logging's report-line selection can reflect
    /// the swab result - see IsBlockedByOnSceneFingerprinting's own comment for the
    /// full reasoning, which applies identically here.
    /// </summary>
    private static bool IsBlockedBySwabbing(EvidenceRecord record, EvidenceStatus target)
    {
        return target == EvidenceStatus.Logged
            && record.definition != null
            && record.definition.requiresSwabbing
            && (!record.swabbingDone || (record.definition.collectedBySwab && !record.swabSampleSealed));
    }

    // True when the item's sealed on-scene swab sample is its collection (see
    // EvidenceDefinition.collectedBySwab) - the custody steps after Logged then follow
    // from that sample instead of a second swab.
    private static bool IsPreSampledSwabItem(EvidenceRecord record) =>
        record?.definition != null && record.definition.collectedBySwab && record.definition.requiresSwabbing;

    /// <summary>
    /// Why fingerprint processing may not start on this item yet, or null. Today only
    /// EvidenceDefinition.swabBeforeFingerprinting: the blood sample comes first.
    /// </summary>
    public string GetFingerprintBlockReason(string evidenceId)
    {
        if (!ProcedureInterlock.AllowsFingerprintWork(evidenceId)) return ProcedureInterlock.Reason;
        string actionReason = GetActionBlockReason(evidenceId, ProcedureAction.FingerprintProcessing);
        if (actionReason != null)
        {
            return actionReason;
        }

        var record = GetRecord(evidenceId);
        if (record?.definition != null && record.definition.swabBeforeFingerprinting
            && record.definition.requiresSwabbing && !record.swabbingDone)
        {
            return GameDialogue.Text("evidence-blood-sample-before-powder");
        }
        return null;
    }

    /// <summary>
    /// Records a refused fingerprint attempt the same way a refused transition is
    /// recorded (OnEvidenceTransitionBlocked -> EvidenceTransitionBlocked in the session
    /// log), for tools that refuse before ever calling MarkFingerprinted.
    /// </summary>
    public void ReportFingerprintBlocked(string evidenceId)
    {
        var record = GetRecord(evidenceId);
        if (record == null)
        {
            return;
        }
        bool onScene = record.definition != null && record.definition.fingerprintBeforeCollection;
        ReportProcedureBlocked(evidenceId, onScene ? EvidenceStatus.Logged : EvidenceStatus.Processed);
    }

    /// <summary>
    /// The first Block-enforced stepRequirement guarding this non-transition action on
    /// evidenceId whose requirement is unmet, as its player-facing message - or null.
    /// The one authoritative check for swab sampling and fingerprint processing rules;
    /// MarkSwabbed/MarkFingerprinted enforce it, tools only pre-check it.
    /// </summary>
    public string GetActionBlockReason(string evidenceId, ProcedureAction action)
    {
        var steps = GetRecord(evidenceId)?.definition?.stepRequirements;
        if (steps == null)
        {
            return GetSketchActionBlockReason(evidenceId, action);
        }
        foreach (var step in steps)
        {
            if (step != null && step.action == action && step.enforcement == ProcedureEnforcement.Block
                && !RequirementEvaluator.IsSatisfied(step.requirement))
            {
                return string.IsNullOrEmpty(step.PlayerMessage) ? GameDialogue.Text("procedure-step-required-first") : step.PlayerMessage;
            }
        }
        return GetSketchActionBlockReason(evidenceId, action);
    }

    private static string GetSketchActionBlockReason(string evidenceId, ProcedureAction action) =>
        action == ProcedureAction.BiologicalSampling || action == ProcedureAction.FingerprintProcessing
            ? CrimeSceneSketchManager.GetProcessingBlockReason(evidenceId) : null;

    /// <summary>
    /// Records an action a tool refused before any transition was attempted, the same way
    /// a refused transition is recorded (EvidenceTransitionBlocked in the session log).
    /// Exactly one per refused action - never also ProcedureOutOfOrder.
    /// </summary>
    public void ReportProcedureBlocked(string evidenceId, EvidenceStatus attempted)
    {
        var record = GetRecord(evidenceId);
        if (record != null)
        {
            OnEvidenceTransitionBlocked?.Invoke(evidenceId, attempted, record.status);
        }
    }

    /// <summary>
    /// The on-scene swab sample's container was sealed (SwabTool.TrySeal on a required
    /// sample). For a collectedBySwab item this completes the sample before its record
    /// is written - see IsBlockedBySwabbing. Requires the swab itself first.
    /// </summary>
    public TransitionResult MarkSwabSampleSealed(string evidenceId, ToolType tool)
    {
        if (ProcedureInterlock.BlocksUnrelatedActions && !ProcedureInterlock.AllowsBloodCompletion(evidenceId)) return TransitionResult.Violation;
        if (SceneTurnover.IsInvestigationBlocked) return TransitionResult.Violation;
        var record = GetRecord(evidenceId);
        if (record == null || !record.swabbingDone)
        {
            return TransitionResult.Violation;
        }
        if (record.swabSampleSealed)
        {
            return TransitionResult.Duplicate;
        }
        record.swabSampleSealed = true;
        record.lastToolUsed = tool;
        Debug.Log($"[EvidenceStateManager] {evidenceId} swab sample sealed (via {tool}).");
        OnEvidenceSwabbed?.Invoke(evidenceId);
        return TransitionResult.Applied;
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
            // examinedInPlace items (the body, so far) are deliberately excluded from
            // every count this method produces. Every current caller - Hypothesis
            // CheckpointManager's CountThreshold triggers and its logged payload,
            // GreyboxFlowTest's diagnostic totals - assumes its count reflects the
            // numbered roster; letting a non-roster item silently nudge those counts
            // (e.g. helping satisfy a "3 items Found" threshold one item early) would
            // be exactly the kind of accidental roster-count coupling this item's
            // addition was scoped to avoid. See EvidenceDefinition.examinedInPlace.
            if (record.definition != null && record.definition.examinedInPlace)
            {
                continue;
            }

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
        if (ProcedureInterlock.BlocksUnrelatedActions) return TransitionResult.Violation;
        if (SceneTurnover.IsInvestigationBlocked) return TransitionResult.Violation;
        var record = GetRecord(evidenceId);
        if (record == null || record.definition == null || record.definition.examinedInPlace || tentNumber <= 0)
        {
            return RejectMarker(evidenceId, record, GameDialogue.Text("marker-number-required"));
        }
        if (record.tentNumber.HasValue && record.tentNumber != tentNumber)
            return RejectMarker(evidenceId, record, GameDialogue.Text("marker-reclaim-before-renumber"));
        foreach (var other in records.Values)
            if (other != record && other.tentNumber == tentNumber)
                return RejectMarker(evidenceId, record, GameDialogue.Text("marker-number-in-use"));
        return SetStatus(evidenceId, EvidenceStatus.Marked, tool, tentNumber);
    }

    /// <summary>
    /// Mirrors MarkTented exactly, but for a LETTER tent instead of a numbered one -
    /// see EvidenceDefinition.examinedInPlace. Kept as a separate method rather than
    /// an overload/optional-letter-param on MarkTented itself: a single item is never
    /// marked with both a number and a letter, so there is no case where a caller
    /// needs to pass one or the other conditionally - the two are used by entirely
    /// different tent-placement code paths in EvidenceTentTool.
    /// </summary>
    public TransitionResult MarkTentedWithLetter(string evidenceId, ToolType tool, string letter)
    {
        if (ProcedureInterlock.BlocksUnrelatedActions) return TransitionResult.Violation;
        if (SceneTurnover.IsInvestigationBlocked) return TransitionResult.Violation;
        var record = GetRecord(evidenceId);
        if (record == null || record.definition == null || !record.definition.examinedInPlace
            || letter == null || letter.Length != 1 || letter[0] < 'A' || letter[0] > 'Z')
        {
            return RejectMarker(evidenceId, record, GameDialogue.Text("marker-letter-required"));
        }
        if (record.tentLetter != null && record.tentLetter != letter)
            return RejectMarker(evidenceId, record, GameDialogue.Text("marker-letter-already-set"));
        foreach (var other in records.Values)
            if (other != record && other.tentLetter == letter)
                return RejectMarker(evidenceId, record, GameDialogue.Text("marker-letter-in-use"));
        return SetStatus(evidenceId, EvidenceStatus.Marked, tool, markerLetter: letter);
    }

    private static TransitionResult RejectMarker(string evidenceId, EvidenceRecord record, string reason)
    {
        Debug.LogWarning($"[EvidenceStateManager] Blocked marker for {evidenceId}: {reason}");
        if (record != null) OnEvidenceTransitionBlocked?.Invoke(evidenceId, EvidenceStatus.Marked, record.status);
        SessionLogger.Instance?.LogEvent(SessionEventType.EvidenceMarkerRejected, evidenceId,
            new Dictionary<string, string> { { "reason", reason } });
        return TransitionResult.Violation;
    }

    /// <summary>
    /// The first of two photographs: the undisturbed scene, before the outline is
    /// chalked or the tent is placed. Same PhotographTool as MarkPhotographedFinal -
    /// which of the two applies is decided by the item's current status, not by a
    /// separate tool or a separate action type. See PhotographTool's class comment.
    /// </summary>
    public TransitionResult MarkPhotographedRaw(string evidenceId, ToolType tool) => SetStatus(evidenceId, EvidenceStatus.PhotographedRaw, tool);

    /// <summary>
    /// An outline has been chalked around the item, between the raw photograph and
    /// the numbered tent going down - real procedure, not merely a gate to make
    /// ChalkMarked reachable. See ChalkMarkTool.
    /// </summary>
    public TransitionResult MarkChalkMarked(string evidenceId, ToolType tool) => SetStatus(evidenceId, EvidenceStatus.ChalkMarked, tool);

    /// <summary>
    /// The second of two photographs: the documented scene, with the numbered tent
    /// now in frame. See MarkPhotographedRaw and PhotographTool's class comment.
    /// </summary>
    public TransitionResult MarkPhotographedFinal(string evidenceId, ToolType tool) => SetStatus(evidenceId, EvidenceStatus.PhotographedFinal, tool);

    public TransitionResult MarkLogged(string evidenceId, ToolType tool)
    {
        var record = GetRecord(evidenceId);
        if (record?.definition?.requiresBodyRemoval == true)
        {
            if (record.bodyFindingsRecorded) return TransitionResult.Duplicate;
            if (!CanAdvanceTo(evidenceId, EvidenceStatus.Logged) || !HasRequiredBodyReport(record)) return TransitionResult.Violation;
            var bodyResult = SetStatus(evidenceId, EvidenceStatus.Logged, tool);
            if (bodyResult != TransitionResult.Applied) return bodyResult;
            record.bodyFindingsRecorded = true;
            SessionLogger.Instance?.LogEvent(SessionEventType.BodyDocumentationRecorded, evidenceId,
                new Dictionary<string, string> { { "procedureId", "body-documentation" }, { "scored", "false" } });
            OnBodyProcedureChanged?.Invoke(evidenceId);
            return TransitionResult.Applied;
        }
        var result = SetStatus(evidenceId, EvidenceStatus.Logged, tool);
        CheckReadyForCollection(evidenceId);
        return result;
    }

    private static bool HasRequiredBodyReport(EvidenceRecord record) => record.recorderDocumentation!=null
        ||record.selectedReportLineIds.Contains("body-a-location")&&record.selectedReportLineIds.Contains("body-a-position")
            &&record.selectedReportLineIds.Contains("body-a-wound-sharp-object");

    public string GetBodyRemovalBlockReason(string evidenceId)
    {
        if (ProcedureInterlock.BlocksUnrelatedActions) return ProcedureInterlock.Reason;
        var record = GetRecord(evidenceId);
        if (record?.definition?.requiresBodyRemoval != true || !record.definition.examinedInPlace)
            return "This evidence does not use body removal.";
        if (record.bodyRemovalStatus == BodyRemovalStatus.Removed) return "BODY-A is already prepared for transport and removed.";
        if (SceneTurnover.IsInvestigationBlocked) return "Complete the officer handover before preparing the body for removal.";
        if (record.status != EvidenceStatus.Logged || !record.bodyFindingsRecorded || !HasRequiredBodyReport(record))
            return "Complete the required body documentation, measurement, and recording before preparing BODY-A for removal.";
        if (record.tentLetter != "A") return "Place Body Marker A before preparing the victim for removal.";
        if (MeasurementStateManager.Instance == null || !MeasurementStateManager.Instance.GetEvidenceMeasurementStatus(evidenceId).IsComplete)
            return "Complete the required body documentation, measurement, and recording before preparing BODY-A for removal.";
        if (CrimeSceneSketchManager.GetProcessingBlockReason(evidenceId) != null)
            return GameDialogue.Text("sketch-body-before-removal");
        return null;
    }

    public TransitionResult MarkBodyPreparedForRemoval(string evidenceId)
    {
        var record = GetRecord(evidenceId);
        if (record?.bodyRemovalStatus >= BodyRemovalStatus.PreparedForTransport) return TransitionResult.Duplicate;
        string reason = GetBodyRemovalBlockReason(evidenceId);
        if (reason != null)
        {
            SessionLogger.Instance?.LogEvent(SessionEventType.BodyRemovalBlocked, evidenceId,
                new Dictionary<string, string> { { "reason", reason }, { "procedureId", "body-removal" }, { "scored", "false" } });
            return TransitionResult.Violation;
        }
        record.bodyRemovalStatus = BodyRemovalStatus.PreparedForTransport;
        SessionLogger.Instance?.LogEvent(SessionEventType.BodyPreparedForRemoval, evidenceId,
            new Dictionary<string, string> { { "procedureId", "body-removal" }, { "scored", "false" },
                { "steps", "identification-tagging,wrapping,cadaver-bag,medico-legal-transport" } });
        OnBodyProcedureChanged?.Invoke(evidenceId);
        return TransitionResult.Applied;
    }

    public TransitionResult MarkBodyRemoved(string evidenceId)
    {
        var record = GetRecord(evidenceId);
        if (record?.definition?.requiresBodyRemoval != true || record.bodyRemovalStatus == BodyRemovalStatus.NotPrepared) return TransitionResult.Violation;
        if (record.bodyRemovalStatus == BodyRemovalStatus.Removed) return TransitionResult.Duplicate;
        if (GetBodyRemovalBlockReason(evidenceId) != null) return TransitionResult.Violation;
        record.bodyRemovalStatus = BodyRemovalStatus.Removed;
        SessionLogger.Instance?.LogEvent(SessionEventType.BodyRemoved, evidenceId,
            new Dictionary<string, string> { { "procedureId", "body-removal" }, { "scored", "false" }, { "destination", "medico-legal-examination" } });
        SessionLogger.Instance?.Flush();
        OnBodyProcedureChanged?.Invoke(evidenceId);
        return TransitionResult.Applied;
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
    public TransitionResult MarkSealed(string evidenceId, ToolType tool)
    {
        var result = SetStatus(evidenceId, EvidenceStatus.Sealed, tool);
        CheckProcessed(evidenceId);
        return result;
    }

    public TransitionResult MarkProcessed(string evidenceId, ToolType tool) => SetStatus(evidenceId, EvidenceStatus.Processed, tool);

    /// <summary>
    /// Records that fingerprint processing has been performed. Deliberately not a
    /// status of its own: only some items require it, so it is a per-item flag rather
    /// than a step every item would have to walk through. WHEN it's allowed to happen
    /// depends on definition.fingerprintBeforeCollection:
    /// - Default (most items, e.g. EVD-017/018): must happen while Sealed - the print
    ///   is read from a sealed container back at the lab, after the item is already
    ///   bagged. Becomes an extra precondition on Sealed -> Processed (see
    ///   IsBlockedByFingerprinting). Reading off Collected instead would create TWO
    ///   gates on the way to Processed satisfiable in either order - fingerprint-then-
    ///   seal, or seal-then-fingerprint - and a chain of custody established after the
    ///   item was already opened and dusted is not a chain of custody. Strictly
    ///   Collected -> Sealed -> (fingerprint if required) -> Processed.
    /// - fingerprintBeforeCollection (currently only the gloves, EVD-019): must happen
    ///   ON SCENE, while exactly PhotographedFinal - same "exactly this one status,
    ///   not later" precondition MarkSwabbed already uses for the same reason (any
    ///   earlier and there's nothing to process yet; any later and the item may
    ///   already be logged and its report written without this finding reflected in
    ///   it). Becomes an extra precondition on PhotographedFinal -> Logged instead
    ///   (see IsBlockedByOnSceneFingerprinting) - confirming a candidate weapon
    ///   before it's ever logged, let alone removed from the scene.
    /// </summary>
    public bool CanLiftFingerprint(string evidenceId)
    {
        var record = GetRecord(evidenceId);
        return !SceneTurnover.IsInvestigationBlocked && ProcedureInterlock.AllowsFingerprintWork(evidenceId) && record?.definition != null
            && record.definition.requiresFingerprinting && record.definition.fingerprintBeforeCollection
            && record.status == EvidenceStatus.PhotographedFinal && !record.fingerprintLifted && !record.fingerprintingDone
            && (!record.definition.requiresFlipBeforeFingerprinting || record.isFlipped)
            && GetFingerprintBlockReason(evidenceId) == null;
    }

    public bool MarkFingerprintLifted(string evidenceId)
    {
        if (!CanLiftFingerprint(evidenceId)) return false;
        GetRecord(evidenceId).fingerprintLifted = true;
        return true;
    }

    public TransitionResult MarkFingerprinted(string evidenceId, ToolType tool)
    {
        if (!ProcedureInterlock.AllowsFingerprintWork(evidenceId)) return TransitionResult.Violation;
        // IOC observation is separate from SOCO processing; phase refusals are not scored violations.
        if (SceneTurnover.IsInvestigationBlocked) return TransitionResult.Violation;
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

        bool onScene = record.definition != null && record.definition.fingerprintBeforeCollection;
        EvidenceStatus requiredStatus = onScene ? EvidenceStatus.PhotographedFinal : EvidenceStatus.Sealed;

        if (record.status != requiredStatus)
        {
            Debug.LogWarning($"[EvidenceStateManager] Blocked fingerprinting of {evidenceId}: item is {record.status}, must be {requiredStatus} first.");
            OnEvidenceTransitionBlocked?.Invoke(evidenceId, onScene ? EvidenceStatus.Logged : EvidenceStatus.Processed, record.status);
            return TransitionResult.Violation;
        }

        // Additional on-scene precondition (currently only the gloves, EVD-019): a
        // surface only reachable once flipped can't be printed before it's actually
        // been turned over - see EvidenceDefinition.requiresFlipBeforeFingerprinting
        // and MarkFlipped.
        if (onScene && record.definition.requiresFlipBeforeFingerprinting && !record.isFlipped)
        {
            Debug.LogWarning($"[EvidenceStateManager] Blocked fingerprinting of {evidenceId}: not flipped yet.");
            return TransitionResult.Violation;
        }

        if (GetFingerprintBlockReason(evidenceId) != null)
        {
            Debug.LogWarning($"[EvidenceStateManager] Blocked fingerprinting of {evidenceId}: blood sample not collected yet.");
            OnEvidenceTransitionBlocked?.Invoke(evidenceId, onScene ? EvidenceStatus.Logged : EvidenceStatus.Processed, record.status);
            return TransitionResult.Violation;
        }

        record.fingerprintingDone = true;
        record.lastToolUsed = tool;
        SessionLogger.Instance?.LogEvent(SessionEventType.EvidenceFingerprintProcessed, evidenceId);
        Debug.Log($"[EvidenceStateManager] {evidenceId} fingerprinted (via {tool}) | next: {DescribeNextStep(record.status)}");
        OnEvidenceFingerprinted?.Invoke(evidenceId);
        CheckProcessed(evidenceId);

        // The secured lift is ready for examination. Identification stays pending
        // until the comparison desk confirms the selected reference.
        MarkSentToLab(evidenceId);

        return TransitionResult.Applied;
    }

    /// <summary>
    /// First stage of the decoupled fingerprint lab side-track (see
    /// FingerprintLabStatus). Currently only ever called automatically from
    /// MarkFingerprinted above - see that call site's comment for why. Deliberately
    /// does not touch record.status, RequiredSequence, or CheckProcessed: whether a
    /// print has been sent for identification has no bearing on the item's own chain
    /// of custody, which must be able to reach Processed regardless of lab turnaround.
    /// </summary>
    public TransitionResult MarkSentToLab(string evidenceId)
    {
        if (!ProcedureInterlock.AllowsFingerprintWork(evidenceId)) return TransitionResult.Violation;
        // IOC observation is separate from SOCO processing; phase refusals are not scored violations.
        if (SceneTurnover.IsInvestigationBlocked) return TransitionResult.Violation;
        var record = GetRecord(evidenceId);
        if (record == null)
        {
            Debug.LogWarning($"[EvidenceStateManager] Unknown evidenceId: {evidenceId}");
            return TransitionResult.Violation;
        }

        if (record.fingerprintLabStatus != FingerprintLabStatus.NotSent)
        {
            return TransitionResult.Duplicate;
        }

        record.fingerprintLabStatus = FingerprintLabStatus.SentToLab;
        Debug.Log($"[EvidenceStateManager] {evidenceId} print sent to lab.");
        OnFingerprintLabStatusChanged?.Invoke(evidenceId, FingerprintLabStatus.SentToLab);
        return TransitionResult.Applied;
    }

    /// <summary>
    /// Second and final stage of the decoupled fingerprint lab side-track (see
    /// FingerprintLabStatus). Called by the comparison desk after correspondence
    /// with the selected reference is confirmed. Identity comes from the scenario's
    /// authored reference data. Requires SentToLab; collection alone cannot identify
    /// the print. This does not advance the source item's physical procedure state.
    /// </summary>
    public TransitionResult MarkMatched(string evidenceId)
    {
        if (!ProcedureInterlock.AllowsFingerprintWork(evidenceId)) return TransitionResult.Violation;
        // IOC observation is separate from SOCO processing; phase refusals are not scored violations.
        if (SceneTurnover.IsInvestigationBlocked) return TransitionResult.Violation;
        var record = GetRecord(evidenceId);
        if (record == null)
        {
            Debug.LogWarning($"[EvidenceStateManager] Unknown evidenceId: {evidenceId}");
            return TransitionResult.Violation;
        }

        if (record.fingerprintLabStatus == FingerprintLabStatus.Matched)
        {
            return TransitionResult.Duplicate;
        }

        if (record.fingerprintLabStatus != FingerprintLabStatus.SentToLab)
        {
            Debug.LogWarning($"[EvidenceStateManager] Blocked lab match for {evidenceId}: print is {record.fingerprintLabStatus}, must be SentToLab first.");
            return TransitionResult.Violation;
        }

        record.fingerprintLabStatus = FingerprintLabStatus.Matched;
        string matchTarget = GetPlaceholderMatchTarget(evidenceId);
        record.fingerprintLabMatchResult = matchTarget;
        record.fingerprintLabMatchIdentities = GetFingerprintMatchIdentities(evidenceId);
        SessionLogger.Instance?.LogEvent(SessionEventType.FingerprintIdentified, evidenceId,
            new Dictionary<string,string> { { "matches", JsonUtility.ToJson(new FingerprintMatchesPayload { identities = record.fingerprintLabMatchIdentities }) },
                { "sourceSampleId", EvidenceInventory.FingerprintSampleId(evidenceId) }, { "scored", "false" } });
        Debug.Log($"[EvidenceStateManager] {evidenceId} print matched (STUBBED result: \"{matchTarget}\").");
        OnFingerprintLabStatusChanged?.Invoke(evidenceId, FingerprintLabStatus.Matched);
        return TransitionResult.Applied;
    }

    /// <summary>
    /// Records that this item's required biological trace has been swabbed.
    /// Mirrors MarkFingerprinted's own shape, but the precondition sits on Logged
    /// rather than Processed - see IsBlockedBySwabbing for why. Required while the
    /// item is exactly PhotographedFinal: any earlier and there's nothing to swab
    /// yet in the documented procedure, any later and the item may already be logged
    /// and its report written without this finding reflected in it.
    /// </summary>
    public TransitionResult MarkSwabbed(string evidenceId, ToolType tool)
    {
        if (ProcedureInterlock.BlocksUnrelatedActions && !ProcedureInterlock.AllowsBloodCompletion(evidenceId)) return TransitionResult.Violation;
        // IOC observation is separate from SOCO processing; phase refusals are not scored violations.
        if (SceneTurnover.IsInvestigationBlocked) return TransitionResult.Violation;
        var record = GetRecord(evidenceId);
        if (record == null)
        {
            Debug.LogWarning($"[EvidenceStateManager] Unknown evidenceId: {evidenceId}");
            return TransitionResult.Violation;
        }

        if (record.definition != null && !record.definition.requiresSwabbing)
        {
            // Harmless: this item simply doesn't need it. Not a procedural error.
            return TransitionResult.Duplicate;
        }

        if (record.swabbingDone)
        {
            return TransitionResult.Duplicate;
        }

        if (GetActionBlockReason(evidenceId, ProcedureAction.BiologicalSampling) != null)
        {
            Debug.LogWarning($"[EvidenceStateManager] Blocked swabbing of {evidenceId}: a procedure step comes first.");
            OnEvidenceTransitionBlocked?.Invoke(evidenceId, EvidenceStatus.Logged, record.status);
            return TransitionResult.Violation;
        }

        if (record.status != EvidenceStatus.PhotographedFinal)
        {
            Debug.LogWarning($"[EvidenceStateManager] Blocked swabbing of {evidenceId}: item is {record.status}, must be PhotographedFinal first.");
            OnEvidenceTransitionBlocked?.Invoke(evidenceId, EvidenceStatus.Logged, record.status);
            return TransitionResult.Violation;
        }

        record.swabbingDone = true;
        record.lastToolUsed = tool;
        SessionLogger.Instance?.LogEvent(SessionEventType.EvidenceSwabbed, evidenceId);
        Debug.Log($"[EvidenceStateManager] {evidenceId} swabbed (via {tool}) | next: {DescribeNextStep(record.status)}");
        OnEvidenceSwabbed?.Invoke(evidenceId);
        return TransitionResult.Applied;
    }

    /// <summary>
    /// Records that this item has been physically flipped. Mirrors MarkSwabbed's
    /// exact shape - same "harmless Duplicate if not required or already done,
    /// Violation if not exactly PhotographedFinal" pattern, for the same reason: an
    /// on-scene action has to happen while the item is still on scene, no earlier (a
    /// surface can't be flipped before it exists to the player) and no later (the
    /// item may already be logged and its report written). Only meaningful when
    /// definition.requiresFlipBeforeFingerprinting is true (the gloves, EVD-019, so
    /// far) - see EvidenceDefinition's own comment.
    ///
    /// LOCKED BEHIND swabbingDone when the item also requiresSwabbing: real
    /// procedure swabs the outside blood trace before ever turning the item over, so
    /// flipping can't disturb or contaminate a trace not yet sampled. Vacuously
    /// satisfied (no extra wait) for an item that requiresFlipBeforeFingerprinting
    /// without also requiresSwabbing - there is no trace order to protect for one.
    /// This reuses IsBlockedBySwabbing's own "target == Logged" check rather than a
    /// new one: since that helper already treats an unswabbed requiresSwabbing item
    /// as blocking Logged regardless of caller, ProceduralGateValidator.GetBlockReason
    /// (which EvidenceFlipTool.TryFlip calls on a Violation) already surfaces "Blood
    /// trace must be swabbed first." here for free, with no message wiring needed on
    /// EvidenceFlipTool itself.
    /// </summary>
    public TransitionResult MarkFlipped(string evidenceId, ToolType tool)
    {
        if (ProcedureInterlock.BlocksUnrelatedActions) return TransitionResult.Violation;
        // IOC observation is separate from SOCO processing; phase refusals are not scored violations.
        if (SceneTurnover.IsInvestigationBlocked) return TransitionResult.Violation;
        var record = GetRecord(evidenceId);
        if (record == null)
        {
            Debug.LogWarning($"[EvidenceStateManager] Unknown evidenceId: {evidenceId}");
            return TransitionResult.Violation;
        }

        if (record.definition != null && !record.definition.requiresFlipBeforeFingerprinting)
        {
            // Harmless: this item simply doesn't need it. Not a procedural error.
            return TransitionResult.Duplicate;
        }

        if (record.isFlipped)
        {
            return TransitionResult.Duplicate;
        }

        if (record.status != EvidenceStatus.PhotographedFinal)
        {
            Debug.LogWarning($"[EvidenceStateManager] Blocked flipping of {evidenceId}: item is {record.status}, must be PhotographedFinal first.");
            OnEvidenceTransitionBlocked?.Invoke(evidenceId, EvidenceStatus.Logged, record.status);
            return TransitionResult.Violation;
        }

        if (record.definition != null && record.definition.requiresSwabbing && !record.swabbingDone)
        {
            Debug.LogWarning($"[EvidenceStateManager] Blocked flipping of {evidenceId}: blood trace must be swabbed first.");
            OnEvidenceTransitionBlocked?.Invoke(evidenceId, EvidenceStatus.Logged, record.status);
            return TransitionResult.Violation;
        }

        record.isFlipped = true;
        record.lastToolUsed = tool;
        Debug.Log($"[EvidenceStateManager] {evidenceId} flipped (via {tool}) | next: {DescribeNextStep(record.status)}");
        return TransitionResult.Applied;
    }

    /// <summary>
    /// Try to pull a placed evidence tent back off an item.
    ///
    /// Allowed only while the item is exactly Marked - i.e. before anything has been
    /// documented with that marker in frame. That is a legitimate correction: the
    /// player looked closer and decided this isn't evidence. Once the item is
    /// PhotographedFinal or later, the marker is already in the record and the
    /// reclaim is refused outright, matching every other one-way gate in the project.
    ///
    /// Reverts to whichever status immediately precedes Marked in RequiredSequence,
    /// NOT hardcoded to Found. Before the documentation-flow restructure, Marked sat
    /// immediately after Found, so those were the same thing; now PhotographedRaw and
    /// ChalkMarked sit between them, and a hardcoded revert-to-Found would silently
    /// erase both of those already-completed steps along with the tent. Reading the
    /// immediate predecessor out of RequiredSequence instead keeps this correct
    /// regardless of what sits before Marked in the future.
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
            OnEvidenceTransitionBlocked?.Invoke(evidenceId, RequiredSequence[markedIndex - 1], record.status);
            return ReclaimResult.BlockedDocumented;
        }

        if (currentIndex < markedIndex)
        {
            return ReclaimResult.NoChange;
        }

        // The status before Marked for THIS item - PhotographedRaw when it has no chalk step.
        EvidenceStatus revertStatus = record.definition != null && !record.definition.requiresChalk
            ? EvidenceStatus.PhotographedRaw
            : RequiredSequence[markedIndex - 1];

        record.status = revertStatus;
        record.lastToolUsed = tool;
        record.statusChangedAtTime = Time.time;
        record.tentNumber = null; // the physical tent is gone; nothing to label with anymore

        OnEvidenceStatusChanged?.Invoke(evidenceId, revertStatus);
        Debug.Log($"[EvidenceStateManager] {evidenceId} -> {revertStatus} (marker reclaimed via {tool}) | next: {DescribeNextStep(revertStatus)}");
        return ReclaimResult.Reverted;
    }

    private void CheckReadyForCollection(string evidenceId)
    {
        var record = GetRecord(evidenceId);
        if (record == null) return;

        // examinedInPlace items (the body, so far) stop at Logged for real - there is
        // no ReadyForCollection/Collected/Sealed/Processed for something that's
        // examined where it lies and never bagged. RequiredSequence itself has no
        // concept of a per-item terminal point (see EvidenceDefinition.examinedInPlace's
        // own comment), so this is the one place that has to know to stop, the same
        // way IsBlockedByFingerprinting/IsBlockedBySwabbing are the one place that
        // knows about their own per-item preconditions.
        if (record.definition != null && record.definition.examinedInPlace)
        {
            return;
        }

        // For MVP: "ready" once Logged has happened (which implies PhotographedRaw,
        // ChalkMarked, Marked, and PhotographedFinal already occurred, since SetStatus
        // below now enforces that order on the way in).
        if (record.status == EvidenceStatus.Logged)
        {
            SetStatus(evidenceId, EvidenceStatus.ReadyForCollection, record.lastToolUsed);
        }

        // A collectedBySwab item sampled on scene (requiresSwabbing): its swab sample was
        // already taken, contained and sealed before this record was written (Logged is
        // gated on exactly that - IsBlockedBySwabbing), and that sealed sample IS its
        // collection. Advance the custody statuses from the sample now and admit it to the
        // inventory, rather than asking for a second swab. Each step still goes through
        // SetStatus and is logged like any other transition (via the swab tool).
        if (record.status == EvidenceStatus.ReadyForCollection && IsPreSampledSwabItem(record) && record.swabSampleSealed)
        {
            SetStatus(evidenceId, EvidenceStatus.Collected, ToolType.Swab);
            MarkSealed(evidenceId, ToolType.Swab);
            if (IsAtOrPastStatus(evidenceId, EvidenceStatus.Sealed) && EvidenceInventory.Instance != null && !EvidenceInventory.Instance.IsInInventory(evidenceId))
            {
                EvidenceInventory.Instance.Add(evidenceId);
            }
        }
    }

    /// <summary>
    /// Auto-advances Sealed -&gt; Processed the moment every precondition is already
    /// true - the same "ready" shape CheckReadyForCollection uses for
    /// Logged -&gt; ReadyForCollection. Processed has no distinct physical action of its
    /// own anywhere in this project's real procedure (see MarkSealed's and
    /// MarkFingerprinted's own comments on what each represents): once an item is
    /// sealed and, where required, fingerprinted, there is nothing further for a real
    /// investigator to separately do to it before the case file can count it as
    /// processed - it just IS processed at that point, the same way an item just IS
    /// ready for collection once Logged happens. Confirmed as a real, previously-
    /// undetected gap before this fix: no player-facing tool anywhere in the game ever
    /// called MarkProcessed for a bag- or swab-collected item (only
    /// FingerprintKitTool's own bundled EVD-016 flow did, and only for that one item).
    ///
    /// Called from both MarkSealed (covers items with no fingerprinting requirement,
    /// e.g. EVD-015 - Sealed is already the last precondition for them) and
    /// MarkFingerprinted (covers items where fingerprinting is the last precondition,
    /// e.g. EVD-014/016/017/018 - fingerprinting always happens after Sealed, see
    /// MarkFingerprinted's own comment on why), so this fires regardless of which of
    /// those two completes last. Harmless if status isn't exactly Sealed, or if
    /// fingerprinting is still outstanding - SetStatus's own Duplicate handling
    /// covers the case where this already ran (e.g. FingerprintKitTool.TrySealAndProcess
    /// still calling MarkProcessed explicitly afterward for EVD-016 - that call just
    /// finds the item already Processed and no-ops).
    /// </summary>
    private void CheckProcessed(string evidenceId)
    {
        var record = GetRecord(evidenceId);
        if (record == null) return;

        if (record.status != EvidenceStatus.Sealed)
        {
            return;
        }

        if (record.definition != null && record.definition.requiresFingerprinting && !record.fingerprintingDone)
        {
            return;
        }

        SetStatus(evidenceId, EvidenceStatus.Processed, record.lastToolUsed);
    }

    private TransitionResult SetStatus(string evidenceId, EvidenceStatus newStatus, ToolType tool, int? markerNumber = null, string markerLetter = null)
    {
        if (!ProcedureInterlock.AllowsStatus(evidenceId, newStatus)) return TransitionResult.Violation;
        // IOC observation is separate from SOCO processing; phase refusals are not scored violations.
        if (SceneTurnover.IsInvestigationBlocked) return TransitionResult.Violation;
        var record = GetRecord(evidenceId);
        if (record == null)
        {
            Debug.LogWarning($"[EvidenceStateManager] Unknown evidenceId: {evidenceId}");
            return TransitionResult.Violation;
        }

        if (Array.IndexOf(RequiredSequence, newStatus) < 0 || Array.IndexOf(RequiredSequence, record.status) < 0)
        {
            Debug.LogWarning($"[EvidenceStateManager] Invalid lifecycle status for {evidenceId}.");
            OnEvidenceTransitionBlocked?.Invoke(evidenceId, newStatus, record.status);
            return TransitionResult.Violation;
        }

        if (IsBlockedByInPlaceExamination(record, newStatus))
        {
            Debug.LogWarning($"[EvidenceStateManager] Blocked {evidenceId}: evidence examined in place cannot be collected or sealed.");
            OnEvidenceTransitionBlocked?.Invoke(evidenceId, newStatus, record.status);
            return TransitionResult.Violation;
        }

        if (!IsValidNextStep(record, newStatus))
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
            var nextRequired = GetNextRequiredStatus(record);
            Debug.LogWarning($"[EvidenceStateManager] Blocked {evidenceId}: {record.status} -> {newStatus} skips a required step (needs {nextRequired} first).");
            OnEvidenceTransitionBlocked?.Invoke(evidenceId, newStatus, record.status);
            return TransitionResult.Violation;
        }

        // Body final photography must include the correct letter marker even on direct calls.
        if (record.definition?.requiresBodyRemoval == true && newStatus == EvidenceStatus.PhotographedFinal
            && GetItemBlockReason(evidenceId, newStatus) != null)
            return TransitionResult.Violation;

        // Sequence order is satisfied, but an item-specific precondition may still
        // block - requiresFingerprinting standing between Collected and Processed, or
        // requiresSwabbing standing between ReadyForCollection and Collected. Checked
        // here as well as in CanAdvanceTo so a tool that forgets to pre-check still
        // cannot skip either.
        if (IsBlockedByFingerprinting(record, newStatus))
        {
            Debug.LogWarning($"[EvidenceStateManager] Blocked {evidenceId}: {newStatus} requires fingerprint processing first.");
            OnEvidenceTransitionBlocked?.Invoke(evidenceId, newStatus, record.status);
            return TransitionResult.Violation;
        }

        if (IsBlockedBySwabbing(record, newStatus))
        {
            Debug.LogWarning($"[EvidenceStateManager] Blocked {evidenceId}: {newStatus} requires the blood trace to be swabbed first.");
            OnEvidenceTransitionBlocked?.Invoke(evidenceId, newStatus, record.status);
            return TransitionResult.Violation;
        }

        if (IsBlockedByOnSceneFingerprinting(record, newStatus))
        {
            // Same defense-in-depth reasoning as the two checks above - added late
            // (after IsBlockedByOnSceneFingerprinting itself was introduced for the
            // knife's reorder) and confirmed as a real, exploitable gap, not a
            // hypothetical: a direct MarkCollected call on an on-scene-fingerprint
            // item bypassed this precondition entirely until now, since only
            // CanAdvanceTo (the tool-facing gate) enforced it. It happened to be
            // masked in practice because both current fingerprintBeforeCollection
            // items (the knife, the gloves) also requiresSwabbing - the swab check
            // above caught the same direct call for the wrong reason. Not a
            // guarantee for any future item that doesn't also require swabbing.
            Debug.LogWarning($"[EvidenceStateManager] Blocked {evidenceId}: {newStatus} requires on-scene fingerprinting first.");
            OnEvidenceTransitionBlocked?.Invoke(evidenceId, newStatus, record.status);
            return TransitionResult.Violation;
        }

        if (IsBlockedByTraceMarker(record, newStatus))
        {
            Debug.LogWarning($"[EvidenceStateManager] Blocked {evidenceId}: {newStatus} requires the trace's own evidence tent first.");
            OnEvidenceTransitionBlocked?.Invoke(evidenceId, newStatus, record.status);
            return TransitionResult.Violation;
        }

        var blockingStep = FindUnmetStep(record, newStatus, ProcedureEnforcement.Block);
        if (blockingStep != null)
        {
            Debug.LogWarning($"[EvidenceStateManager] Blocked {evidenceId}: {newStatus} requires procedure step '{blockingStep.stepId}' first.");
            OnEvidenceTransitionBlocked?.Invoke(evidenceId, newStatus, record.status);
            return TransitionResult.Violation;
        }

        // Warn rules never refuse - the transition goes ahead and the out-of-order step
        // is recorded for feedback/scoring instead.
        foreach (var step in record.definition?.stepRequirements ?? new List<ProcedureStepRequirement>())
        {
            if (step != null && step.action == ProcedureAction.StatusTransition && step.beforeStatus == newStatus && step.enforcement == ProcedureEnforcement.Warn
                && !RequirementEvaluator.IsSatisfied(step.requirement)
                && loggedOutOfOrderSteps.Add(evidenceId + "|" + step.stepId))
            {
                ProcedureLog.LogOutOfOrder(step.stepId, evidenceId,
                    string.IsNullOrEmpty(step.expectedPrerequisite) ? step.PlayerMessage : step.expectedPrerequisite,
                    $"{record.status} -> {newStatus}");
            }
        }

        record.status = newStatus;
        if (newStatus == EvidenceStatus.Marked)
        {
            record.tentNumber = markerNumber;
            record.tentLetter = markerLetter;
        }
        record.lastToolUsed = tool;
        record.statusChangedAtTime = Time.time;

        OnEvidenceStatusChanged?.Invoke(evidenceId, newStatus);
        Debug.Log($"[EvidenceStateManager] {evidenceId} â†’ {newStatus} (via {tool}) | next: {DescribeNextStep(newStatus)}");
        return TransitionResult.Applied;
    }

    /// <summary>
    /// Human-readable "what happens next" for a status, e.g. "PhotographedRaw" or
    /// "lifecycle complete" once nothing remains. Appended to every status-change log
    /// line so the console alone shows both where an item is and what it's waiting on,
    /// without cross-referencing RequiredSequence by hand to tell whether something is
    /// stuck versus simply not yet attempted.
    /// </summary>
    private static string DescribeNextStep(EvidenceStatus current)
    {
        var next = GetNextRequiredStatus(current);
        return next.HasValue ? next.Value.ToString() : "lifecycle complete";
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
