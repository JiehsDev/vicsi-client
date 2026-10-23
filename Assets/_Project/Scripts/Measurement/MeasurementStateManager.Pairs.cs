// Assets/_Project/Scripts/Measurement/MeasurementStateManager.Pairs.cs
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

public enum EvidencePairRecordResult
{
    /// <summary>Recorded - the pair is now complete (pairs are measured once).</summary>
    Recorded = 0,

    /// <summary>Both ends are the same item.</summary>
    SameEvidence = 1,

    /// <summary>An end is not (yet) discovered - never revealed, nothing logged.</summary>
    NotDiscovered = 2,

    /// <summary>Both ends discovered, but this pair is not in the scene's required set.</summary>
    NotRequired = 3,

    /// <summary>Already measured - re-measurement is not supported; nothing recorded.</summary>
    AlreadyComplete = 4,

    /// <summary>An end has no measurement point to measure from.</summary>
    NoMeasurementPoint = 5,

    InvalidDistance = 6,
    PrerequisiteBlocked = 7
}

/// <summary>
/// One required evidence-to-evidence distance, stored once per unordered pair. Exposes
/// everything a consumer (Recorder, Crime Scene Sketch, upload) needs without reading logs.
/// </summary>
public class EvidencePairRecord
{
    public EvidencePairKey pair;
    public bool isComplete;
    public float measuredDistance = -1f;
    public float expectedDistance = -1f;
    public float tolerance;
    public bool withinTolerance;
    public bool takenAfterDisturbance;
    /// <summary>Time.time when measured, or -1.</summary>
    public float measuredAtSessionTime = -1f;
    /// <summary>1-based order among pair measurements this session, or 0.</summary>
    public int order;

    public string PairKey => pair.Key;
    public string EvidenceA => pair.evidenceA;
    public string EvidenceB => pair.evidenceB;

    /// <summary>The recorded distance at the tape measure's display precision, or empty.</summary>
    public string DisplayDistance => isComplete ? MeasurementStateManager.FormatDisplay(measuredDistance) : string.Empty;
}

/// <summary>Scene-level measurement progress: fixed-reference sides plus pair distances.</summary>
public struct SceneMeasurementProgress
{
    public int fixedCompleted;
    public int fixedRequired;
    public int pairCompleted;
    public int pairRequired;

    public int Completed => fixedCompleted + pairCompleted;
    public int Required => fixedRequired + pairRequired;
    public bool IsComplete => Required > 0 && Completed >= Required;
}

/// <summary>One evidence item's measurement state for the Evidence Procedure Map.</summary>
public struct EvidenceMeasurementStatus
{
    public int fixedCompleted;
    public int fixedRequired;
    public int pairsCompleted;
    /// <summary>Required pairs involving this item whose other item is already discovered.</summary>
    public int pairsAvailable;
    /// <summary>The single next suggested action ("Measure Kitchen knife to Blood spatter pattern."), or empty when done.</summary>
    public string nextSuggestion;

    public bool FixedComplete => fixedRequired == 0 || fixedCompleted >= fixedRequired;
    public bool IsComplete => FixedComplete && pairsCompleted >= pairsAvailable;
}

/// <summary>One line of an item's spatial record (Recorder).</summary>
public struct SpatialMeasurementLine
{
    public bool isPair;
    /// <summary>Reference name ("West Wall") or the other item's display name.</summary>
    public string label;
    public float distance;
    public string DisplayDistance => MeasurementStateManager.FormatDisplay(distance);
}

// Evidence-to-evidence layer of MeasurementStateManager.
//
// Every unordered pair of the configured scene roster (MeasurementRequirementSet
// .ScenePairRoster) must be measured once, between the two items' authoritative
// measurement points. The full required set is known internally from the start, so
// "scene measurements complete" can never be declared early - but only pairs whose two
// items are both discovered are ever offered or named to the player.
//
// EVIDENCE-LEAK RULE: "discovered" here is the Evidence Procedure Map's rule - at or past
// PhotographedRaw. Found is reached by proximity alone, so offering a pair on Found would
// list which nearby objects are evidence. Player-facing progress (GetSceneProgress with
// discoveredOnly) counts only discovered items, so no total reveals the roster size.
// Do not loosen either.
public partial class MeasurementStateManager
{
    private readonly Dictionary<string, EvidencePairRecord> pairRecords = new();
    private readonly List<EvidencePairRecord> pairOrder = new();
    private int pairMeasureCount;

    /// <summary>Every required pair, in stable roster order (internal - includes undiscovered items; never show this list).</summary>
    public IReadOnlyList<EvidencePairRecord> AllPairs => pairOrder;

    private void BuildPairs()
    {
        pairRecords.Clear();
        pairOrder.Clear();
        pairMeasureCount = 0;
        if (requirementSet == null)
        {
            return;
        }
        foreach (var key in requirementSet.RequiredScenePairs())
        {
            if (key.IsSameEvidence || pairRecords.ContainsKey(key.Key))
            {
                continue;
            }
            var record = new EvidencePairRecord { pair = key, tolerance = requirementSet.pairTolerance };
            pairRecords[key.Key] = record;
            pairOrder.Add(record);
        }
    }

    // --- Queries ------------------------------------------------------------------

    /// <summary>The Procedure Map's discovery rule (at or past PhotographedRaw).</summary>
    public static bool IsDiscoveredForMeasurement(string evidenceId)
    {
        var esm = EvidenceStateManager.Instance;
        return esm != null && !string.IsNullOrEmpty(evidenceId) && esm.GetRecord(evidenceId) != null
            && esm.IsAtOrPastStatus(evidenceId, EvidenceStatus.PhotographedRaw);
    }

    public EvidencePairRecord GetPair(string evidenceX, string evidenceY)
    {
        if (string.IsNullOrEmpty(evidenceX) || string.IsNullOrEmpty(evidenceY))
        {
            return null;
        }
        return pairRecords.TryGetValue(EvidencePairKey.Of(evidenceX, evidenceY).Key, out var record) ? record : null;
    }

    /// <summary>
    /// Both items discovered, and either already measured or both still physically in the
    /// scene with a measurement point. A pair whose item left the scene first (collected
    /// into the inventory - its prop is removed) can no longer be measured: it is MISSED,
    /// not available, so it never blocks the Recorder or any later record.
    /// </summary>
    public bool IsPairAvailable(EvidencePairRecord record) =>
        record != null && IsDiscoveredForMeasurement(record.EvidenceA) && IsDiscoveredForMeasurement(record.EvidenceB)
        && (record.isComplete || (HasMeasurementPoint(record.EvidenceA) && HasMeasurementPoint(record.EvidenceB)));

    /// <summary>
    /// Discovered, never measured, and no longer measurable because an item already left the
    /// scene. Kept in the upload (incomplete, missed) for later assessment; excluded from
    /// every requirement count, player-facing or internal, so it can never deadlock a flow.
    /// </summary>
    public bool IsPairMissed(EvidencePairRecord record) =>
        record != null && !record.isComplete && IsDiscoveredForMeasurement(record.EvidenceA) && IsDiscoveredForMeasurement(record.EvidenceB)
        && (!HasMeasurementPoint(record.EvidenceA) || !HasMeasurementPoint(record.EvidenceB));

    private static bool HasMeasurementPoint(string evidenceId) => MeasurementGeometry.TryGetEvidenceAnchor(evidenceId, out _);

    /// <summary>Required pairs whose two items are both discovered, in roster order - the only pairs the player may see.</summary>
    public List<EvidencePairRecord> GetAvailablePairs()
    {
        var list = new List<EvidencePairRecord>();
        foreach (var record in pairOrder)
        {
            if (IsPairAvailable(record))
            {
                list.Add(record);
            }
        }
        return list;
    }

    /// <summary>Available pairs involving evidenceId, in roster order.</summary>
    public List<EvidencePairRecord> GetAvailablePairsFor(string evidenceId)
    {
        var list = new List<EvidencePairRecord>();
        foreach (var record in pairOrder)
        {
            if (record.pair.Contains(evidenceId) && IsPairAvailable(record))
            {
                list.Add(record);
            }
        }
        return list;
    }

    /// <summary>Every available pair involving evidenceId is measured (true when there are none yet).</summary>
    public bool AreAvailablePairMeasurementsComplete(string evidenceId)
    {
        foreach (var record in GetAvailablePairsFor(evidenceId))
        {
            if (!record.isComplete) return false;
        }
        return true;
    }

    /// <summary>
    /// Every required fixed reference (both sides validly recorded) and every required pair
    /// of the FULL roster. Derived live - never latched - so it can't be declared early.
    /// </summary>
    public bool AreSceneMeasurementsComplete => GetSceneProgress(false).IsComplete;

    /// <summary>
    /// Both fixed references of every DISCOVERED required item and every pair among
    /// discovered items - the Recorder's gate. An undiscovered item never makes this false,
    /// so a refusal can't hint that more evidence exists. Derived live: a later discovery
    /// adds pairs for the records still to be written, and never touches records already
    /// finalized (their status is never revisited).
    /// </summary>
    public bool AreDiscoveredSceneMeasurementsComplete
    {
        get
        {
            var p = GetSceneProgress(true);
            return p.Completed >= p.Required;
        }
    }

    /// <summary>
    /// Scene measurement progress. discoveredOnly = true is the player-facing count (only
    /// discovered items and their pairs); false is the full requirement set (internal).
    /// </summary>
    public SceneMeasurementProgress GetSceneProgress(bool discoveredOnly)
    {
        var progress = new SceneMeasurementProgress();
        if (requirementSet != null)
        {
            foreach (var requirement in requirementSet.requirements)
            {
                if (requirement == null || !requirement.required || (discoveredOnly && !IsDiscoveredForMeasurement(requirement.targetEvidenceId)))
                {
                    continue;
                }
                var record = GetRecord(requirement.requirementId);
                progress.fixedRequired += 2;
                if (record != null)
                {
                    progress.fixedCompleted += (SideValid(record, true) ? 1 : 0) + (SideValid(record, false) ? 1 : 0);
                }
            }
        }
        foreach (var pair in pairOrder)
        {
            if ((discoveredOnly && !IsPairAvailable(pair)) || IsPairMissed(pair))
            {
                continue;
            }
            progress.pairRequired++;
            if (pair.isComplete) progress.pairCompleted++;
        }
        return progress;
    }

    /// <summary>One item's measurement state plus the single next suggested action (fixed references first, then pairs in roster order).</summary>
    public EvidenceMeasurementStatus GetEvidenceMeasurementStatus(string evidenceId)
    {
        var status = new EvidenceMeasurementStatus { nextSuggestion = string.Empty };
        var requirement = requirementSet != null ? requirementSet.requirements.Find(r => r != null && r.targetEvidenceId == evidenceId) : null;
        var record = requirement != null ? GetRecord(requirement.requirementId) : null;
        string name = DisplayName(evidenceId);
        if (record != null)
        {
            status.fixedRequired = 2;
            bool a = SideValid(record, true);
            bool b = SideValid(record, false);
            status.fixedCompleted = (a ? 1 : 0) + (b ? 1 : 0);
            string nextRef = !a ? requirement.referenceAId : !b ? requirement.referenceBId : null;
            if (nextRef != null)
            {
                status.nextSuggestion = $"Measure {name} from the {ReferenceDisplayName(nextRef)}.";
            }
        }
        foreach (var pair in GetAvailablePairsFor(evidenceId))
        {
            status.pairsAvailable++;
            if (pair.isComplete)
            {
                status.pairsCompleted++;
            }
            else if (string.IsNullOrEmpty(status.nextSuggestion))
            {
                status.nextSuggestion = $"Measure {name} to {DisplayName(pair.pair.Other(evidenceId))}.";
            }
        }
        return status;
    }

    /// <summary>The recorded spatial data for one item: its fixed references, then its measured pairs (Recorder).</summary>
    public List<SpatialMeasurementLine> GetSpatialSummary(string evidenceId)
    {
        var lines = new List<SpatialMeasurementLine>();
        var requirement = requirementSet != null ? requirementSet.requirements.Find(r => r != null && r.targetEvidenceId == evidenceId) : null;
        var record = requirement != null ? GetRecord(requirement.requirementId) : null;
        if (record != null)
        {
            if (record.hasA) lines.Add(new SpatialMeasurementLine { label = ReferenceDisplayName(requirement.referenceAId), distance = record.measuredA });
            if (record.hasB) lines.Add(new SpatialMeasurementLine { label = ReferenceDisplayName(requirement.referenceBId), distance = record.measuredB });
        }
        foreach (var pair in pairOrder)
        {
            if (pair.isComplete && pair.pair.Contains(evidenceId))
            {
                lines.Add(new SpatialMeasurementLine { isPair = true, label = DisplayName(pair.pair.Other(evidenceId)), distance = pair.measuredDistance });
            }
        }
        return lines;
    }

    /// <summary>Rich-text spatial record for the Recorder panel, or empty when nothing is recorded.</summary>
    public string FormatSpatialSummary(string evidenceId)
    {
        var lines = GetSpatialSummary(evidenceId);
        if (lines.Count == 0)
        {
            return string.Empty;
        }
        var sb = new StringBuilder();
        bool anyFixed = false, anyPair = false;
        foreach (var l in lines)
        {
            if (l.isPair) continue;
            if (!anyFixed)
            {
                sb.AppendLine("<b>Fixed References</b>");
                anyFixed = true;
            }
            sb.AppendLine($"{l.label} — {l.DisplayDistance}");
        }
        foreach (var l in lines)
        {
            if (!l.isPair) continue;
            if (!anyPair)
            {
                if (anyFixed) sb.AppendLine();
                sb.AppendLine("<b>Evidence Distances</b>");
                anyPair = true;
            }
            sb.AppendLine($"{l.label} — {l.DisplayDistance}");
        }
        return sb.ToString().TrimEnd();
    }

    /// <summary>The true distance between two items' authoritative measurement points, by the set's pair method.</summary>
    public bool TryGetPairSceneDistance(string evidenceX, string evidenceY, out float distance)
    {
        distance = 0f;
        if (!MeasurementGeometry.TryGetEvidenceAnchor(evidenceX, out var a) || !MeasurementGeometry.TryGetEvidenceAnchor(evidenceY, out var b))
        {
            return false;
        }
        distance = MeasurementGeometry.Distance(a, b, requirementSet == null || requirementSet.pairsMeasureOnFloorPlan);
        return true;
    }

    /// <summary>
    /// Pair disturbance uses each item's own fixed requirement (disturbanceStatus, custody,
    /// swab-as-collection): the pair is late if EITHER item was disturbed first. Late pairs
    /// still complete, flagged - pairs are not scored, so there is no double penalty.
    /// </summary>
    public bool IsPairDisturbed(EvidencePairKey pair)
    {
        return IsEvidenceDisturbed(pair.evidenceA) || IsEvidenceDisturbed(pair.evidenceB);
    }

    private bool IsEvidenceDisturbed(string evidenceId)
    {
        var requirement = requirementSet != null ? requirementSet.requirements.Find(r => r != null && r.targetEvidenceId == evidenceId) : null;
        return requirement != null && requirement.mustPrecedeDisturbance && IsDisturbed(requirement);
    }

    // --- Recording ------------------------------------------------------------------

    /// <summary>
    /// Records one pair distance (either direction - stored once under the normalised key).
    /// Rejections are logged (EvidencePairMeasurementRejected) except NotDiscovered, which
    /// never names an undiscovered item.
    /// </summary>
    public EvidencePairRecordResult RecordPairMeasurement(string fromEvidenceId, string toEvidenceId, float measuredDistance)
    {
        if (ProcedureInterlock.BlocksUnrelatedActions) return EvidencePairRecordResult.PrerequisiteBlocked;
        var key = EvidencePairKey.Of(fromEvidenceId ?? string.Empty, toEvidenceId ?? string.Empty);
        if (string.IsNullOrEmpty(fromEvidenceId) || string.IsNullOrEmpty(toEvidenceId) || !IsDiscoveredForMeasurement(fromEvidenceId) || !IsDiscoveredForMeasurement(toEvidenceId))
        {
            return EvidencePairRecordResult.NotDiscovered;
        }
        if (key.IsSameEvidence)
        {
            return Reject(key, EvidencePairRecordResult.SameEvidence);
        }
        if (GetBodyMeasurementBlockReason(fromEvidenceId) != null || GetBodyMeasurementBlockReason(toEvidenceId) != null)
            return EvidencePairRecordResult.PrerequisiteBlocked;
        if (!pairRecords.TryGetValue(key.Key, out var record))
        {
            return Reject(key, EvidencePairRecordResult.NotRequired);
        }
        if (record.isComplete)
        {
            return Reject(key, EvidencePairRecordResult.AlreadyComplete);
        }
        if (!TryGetPairSceneDistance(key.evidenceA, key.evidenceB, out float expected))
        {
            return Reject(key, EvidencePairRecordResult.NoMeasurementPoint);
        }
        if (float.IsNaN(measuredDistance) || float.IsInfinity(measuredDistance) || measuredDistance < 0f)
        {
            return EvidencePairRecordResult.InvalidDistance;
        }

        record.isComplete = true;
        record.measuredDistance = measuredDistance;
        record.expectedDistance = expected;
        record.withinTolerance = Mathf.Abs(measuredDistance - expected) <= record.tolerance;
        record.takenAfterDisturbance = IsPairDisturbed(key);
        record.measuredAtSessionTime = Time.time;
        record.order = ++pairMeasureCount;

        LogPair(SessionEventType.EvidencePairMeasured, key, new Dictionary<string, string>
        {
            { "measuredDistance", Format(measuredDistance) },
            { "expectedDistance", Format(expected) },
            { "tolerance", Format(record.tolerance) },
            { "withinTolerance", record.withinTolerance.ToString() },
            { "afterDisturbance", record.takenAfterDisturbance.ToString() },
            { "order", record.order.ToString(CultureInfo.InvariantCulture) },
            { "measuredFrom", fromEvidenceId }
        });
        OnMeasurementProgress?.Invoke(key.Key);
        return EvidencePairRecordResult.Recorded;
    }

    private EvidencePairRecordResult Reject(EvidencePairKey key, EvidencePairRecordResult reason)
    {
        LogPair(SessionEventType.EvidencePairMeasurementRejected, key, new Dictionary<string, string> { { "reason", reason.ToString() } });
        return reason;
    }

    private static void LogPair(SessionEventType type, EvidencePairKey key, Dictionary<string, string> extra)
    {
        if (SessionLogger.Instance == null)
        {
            return;
        }
        var payload = new Dictionary<string, string>
        {
            { "pairKey", key.Key },
            { "evidenceA", key.evidenceA },
            { "evidenceB", key.evidenceB },
            { "sessionTimeSeconds", Format(Time.time) }
        };
        foreach (var kv in extra) payload[kv.Key] = kv.Value;
        SessionLogger.Instance.LogEvent(type, key.Key, payload);
    }

    // --- Helpers ----------------------------------------------------------------------

    private static bool SideValid(MeasurementRecord record, bool sideA)
    {
        var r = record.requirement;
        return sideA ? record.hasA && (record.withinToleranceA || !r.validateTolerance)
                     : record.hasB && (record.withinToleranceB || !r.validateTolerance);
    }

    /// <summary>Same precision as the tape measure readout.</summary>
    public static string FormatDisplay(float metres) => $"{metres:0.00} m";

    /// <summary>The item's student-facing name (EvidenceDefinition.displayName), never its id.</summary>
    public static string DisplayName(string evidenceId)
    {
        var record = EvidenceStateManager.Instance != null ? EvidenceStateManager.Instance.GetRecord(evidenceId) : null;
        return record?.definition != null && !string.IsNullOrEmpty(record.definition.PlayerDisplayName) ? record.definition.PlayerDisplayName : "evidence";
    }

    private static string ReferenceDisplayName(string referenceId)
    {
        var reference = SceneReferencePoint.Find(referenceId);
        return reference != null && !string.IsNullOrEmpty(reference.displayName) ? reference.displayName : "fixed reference";
    }

    /// <summary>The upload's pair section (analytics only - pairs are not scored by any rules version yet).</summary>
    public List<EvidencePairResult> BuildPairResults()
    {
        var list = new List<EvidencePairResult>();
        foreach (var p in pairOrder)
        {
            list.Add(new EvidencePairResult
            {
                pairKey = p.PairKey,
                evidenceA = p.EvidenceA,
                evidenceB = p.EvidenceB,
                isComplete = p.isComplete,
                measuredDistance = p.isComplete ? p.measuredDistance : -1f,
                expectedDistance = p.isComplete ? p.expectedDistance : -1f,
                tolerance = p.tolerance,
                withinTolerance = p.withinTolerance,
                late = p.takenAfterDisturbance,
                missed = IsPairMissed(p),
                measuredAtSeconds = p.measuredAtSessionTime,
                order = p.order
            });
        }
        return list;
    }
}
