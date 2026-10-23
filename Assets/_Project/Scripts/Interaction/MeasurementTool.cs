// Assets/_Project/Scripts/Interaction/MeasurementTool.cs
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Tape measure for evidence-position measurements (ToolType.Measurement), equipped from
/// the tool wheel like every other tool. Records through MeasurementStateManager only -
/// it owns no measurement state of its own beyond which requirement it is working on.
///
/// Trigger-only flow (right-hand B is already context-bound to tent pickup, which sits
/// right next to the evidence being measured, so this tool deliberately uses nothing else):
///   1. Aim at a discovered evidence item, pull the trigger: selects its requirement.
///   2. Hold the tape at one of THIS item's two fixed references (inside its
///      MeasurementReferenceZone, or within referenceRadius when it has none), pull:
///      hooks the tape there. Either of the item's references may come first. A
///      reference belonging to a different item is refused and changes nothing.
///   3. Bring the tape end to the evidence (within targetRadius of its measurement
///      point), pull: records the distance from the reference to the tape END (to the
///      anchor point, or perpendicular to a wall/counter surface - see
///      MeasurementGeometry.DistanceToReference), where the player actually holds it -
///      never snapped to the expected value, so tolerance is a real check of placement.
///   4. Repeat for the second reference. Both valid -> the requirement completes.
/// Evidence Distance mode (same tool, chosen by state - fixed references always first):
/// selecting an item whose two fixed references are done starts measuring its distances
/// to the other discovered evidence. Hold the tape at the item (its measurement point),
/// pull; extend to another marked item, pull: the distance between the two items'
/// authoritative measurement points is recorded once for that unordered pair
/// (MeasurementStateManager.RecordPairMeasurement). The tape stays at the first item, so
/// its remaining pairs can be measured one after another.
/// Holstering drops an unhooked/extended tape but keeps the selected item, so re-equipping
/// resumes at the next required reference. Recorded progress lives in the manager and is
/// never reset by this tool.
///
/// EVIDENCE-LEAK RULE (see project notes - do not "improve" this): only items the player
/// has already discovered (status at or after Found) can be targeted, the same rule
/// PhotographTool uses, and every prompt is raised here at the action site, never from a
/// state-changed handler, and never names an item by id.
/// </summary>
public class MeasurementTool : PlayerTool
{
    public enum Phase
    {
        SelectTarget = 0,
        AtReference = 1,
        Extending = 2,
        PairAtSource = 3,
        PairExtending = 4
    }

    public enum Mode
    {
        FixedReference = 0,
        EvidenceDistance = 1
    }

    [Header("Input (XRI Input Reader pattern - same actions as ChalkMarkTool)")]
    [SerializeField] private InputActionReference leftActivateAction;
    [SerializeField] private InputActionReference rightActivateAction;

    [Header("Tape")]
    [Tooltip("The tape's free end - its position is what gets measured, and its forward is the aim ray for selecting an item.")]
    [SerializeField] private Transform tapeTip;
    [SerializeField] private LineRenderer tapeLine;
    [SerializeField] private TMP_Text readout;

    [Header("Guides (scene objects, not children - shown only while equipped)")]
    [Tooltip("Shown only on the currently required reference point.")]
    [SerializeField] private GameObject referenceHighlight;
    [Tooltip("Shown on the selected item's measurement anchor.")]
    [SerializeField] private GameObject targetHighlight;

    [Header("Tolerances")]
    [Tooltip("How close (floor-plan metres) the tape must be to the required reference point to hook it.")]
    [SerializeField, Range(0.1f, 0.6f)] private float referenceRadius = 0.35f;
    [Tooltip("How close (floor-plan metres) the tape end must be to the item's measurement anchor to record. Placement accuracy itself is judged by the requirement's tolerance.")]
    [SerializeField, Range(0.1f, 1f)] private float targetRadius = 0.5f;
    [SerializeField] private float aimMaxDistance = 8f;
    [Tooltip("How far off (degrees) the tape may point from an item and still select it. Generous on purpose: a controller held naturally rarely points exactly at a small item on the floor.")]
    [SerializeField, Range(2f, 40f)] private float aimAngle = 25f;

    public override ToolType ToolRole => ToolType.Measurement;

    public static MeasurementTool Instance { get; private set; }

    public Phase CurrentPhase { get; private set; } = Phase.SelectTarget;
    public string SelectedRequirementId { get; private set; }
    public string CurrentReferenceId { get; private set; }

    /// <summary>Evidence Distance mode: the item the tape is held at, else null.</summary>
    public string PairSourceEvidenceId { get; private set; }

    public Mode CurrentMode => CurrentPhase == Phase.PairAtSource || CurrentPhase == Phase.PairExtending ? Mode.EvidenceDistance : Mode.FixedReference;
    public string LastMessage { get; private set; }
    public Transform TapeTip => tapeTip;
    public float ReferenceRadius => referenceRadius;

    /// <summary>The live tape reading while extending (full precision; the readout shows 2 decimals), else -1.</summary>
    public float LiveDistance { get; private set; } = -1f;

    // Capture the authored reference once: moving the hand must never slide the hook.
    private Vector3 hookedReferencePosition;

    protected override void Awake()
    {
        base.Awake();
        Instance = this;
        if (tapeTip == null)
        {
            tapeTip = transform;
        }
        SetGuidesVisible(false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        leftActivateAction?.action.Enable();
        rightActivateAction?.action.Enable();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        leftActivateAction?.action.Disable();
        rightActivateAction?.action.Disable();
        SetGuidesVisible(false);
    }

    public override void EquipToHand(Transform handAnchor)
    {
        base.EquipToHand(handAnchor);
        if (!IsVirtuallyEquipped)
        {
            return;
        }

        // Resume: the selected item survives holstering; recorded sides live in the manager.
        if (!string.IsNullOrEmpty(PairSourceEvidenceId))
        {
            if (!TryStartPairMode(PairSourceEvidenceId))
            {
                ResetToSelect(null);
            }
        }
        else if (string.IsNullOrEmpty(SelectedRequirementId) || !ResumeSelected())
        {
            ResetToSelect(null);
        }
        RefreshVisuals();
    }

    public override void Holster()
    {
        if(tapeTip!=null && (CurrentPhase==Phase.Extending || CurrentPhase==Phase.PairExtending))
            InteractionSfx.Play(InteractionSound.Measure,tapeTip.position);
        base.Holster();
        if (CurrentPhase == Phase.Extending)
        {
            CurrentPhase = Phase.AtReference;
        }
        else if (CurrentPhase == Phase.PairExtending)
        {
            CurrentPhase = Phase.PairAtSource;
        }
        LiveDistance = -1f;
        SetGuidesVisible(false);
    }

    private void Update()
    {
        // Dialogue History (a pure review panel) is open - no tool/world input behind it.
        if (ToolInputLock.IsLocked)
        {
            return;
        }

        if (!IsHeld)
        {
            return;
        }

        bool pressed =
            (leftActivateAction != null && leftActivateAction.action.WasPressedThisFrame()) ||
            (rightActivateAction != null && rightActivateAction.action.WasPressedThisFrame());
        if (pressed)
        {
            HandleTrigger();
        }

        RefreshVisuals();
    }

    /// <summary>The trigger press - the single entry point the real input reaches (public so tests drive exactly this).</summary>
    public void HandleTrigger()
    {
        if (!IsHeld || MeasurementStateManager.Instance == null)
        {
            return;
        }

        switch (CurrentPhase)
        {
            case Phase.SelectTarget:
                TrySelectAimed();
                break;
            case Phase.AtReference:
                TryHook();
                break;
            case Phase.Extending:
                TryRecord();
                break;
            case Phase.PairAtSource:
                TryHookAtSource();
                break;
            case Phase.PairExtending:
                TryRecordPair();
                break;
        }
        RefreshVisuals();
    }

    // --- Step 1: select --------------------------------------------------

    private void TrySelectAimed()
    {
        var evidenceId = FindAimedEvidence();
        if (evidenceId == null)
        {
            Say(GameDialogue.Text("measure-select-evidence"), false);
            return;
        }

        var requirement = FindRequirementFor(evidenceId);
        if (requirement == null)
        {
            // Every discovered roster item currently has a requirement, so this never
            // fires in CSI_Environment. If a future item has none, this message is an
            // identification-neutral procedural answer - it is only reachable for items the
            // player already discovered (see FindAimedEvidence), same as the camera.
            Say(GameDialogue.Text("measure-not-required"), false);
            return;
        }

        string bodyReason = MeasurementStateManager.GetBodyMeasurementBlockReason(evidenceId);
        if (bodyReason != null) { Say(bodyReason, false); return; }

        // Other evidence follows identification by marker (marker -> measurement -> final
        // photo). This also stops the tape from bypassing earlier rules - e.g. overall
        // scene photography before markers. A disturbed item still selects, so a late
        // measurement is recorded and flagged rather than silently refused.
        if (!MeasurementStateManager.Instance.IsDisturbed(requirement) && EvidenceStateManager.Instance != null
            && !EvidenceStateManager.Instance.IsAtOrPastStatus(evidenceId, EvidenceStatus.Marked))
        {
            Say(GameDialogue.Text("measure-marker-first"), false);
            return;
        }

        // Fixed references first; once both are done, the item's evidence distances.
        var record = MeasurementStateManager.Instance.GetRecord(requirement.requirementId);
        if (record == null || NextReference(record) != null)
        {
            SelectedRequirementId = requirement.requirementId;
            if (!ResumeSelected())
            {
                SelectedRequirementId = null;
            }
            return;
        }

        if (TryStartPairMode(evidenceId))
        {
            return;
        }
        ResetToSelect(record.IsComplete ? GameDialogue.Text("measure-complete") : GameDialogue.Text("measure-evidence-moved"));
    }

    // --- Evidence Distance mode ---------------------------------------------

    // Enters Evidence Distance mode for source if it still has an available, unmeasured pair.
    private bool TryStartPairMode(string source)
    {
        var manager = MeasurementStateManager.Instance;
        if (manager == null || manager.AreAvailablePairMeasurementsComplete(source))
        {
            return false;
        }
        SelectedRequirementId = null;
        CurrentReferenceId = null;
        PairSourceEvidenceId = source;
        CurrentPhase = Phase.PairAtSource;
        Say(GameDialogue.Text("measure-pair-hold-at-source", MeasurementStateManager.DisplayName(source)), true);
        return true;
    }

    private void TryHookAtSource()
    {
        if (string.IsNullOrEmpty(PairSourceEvidenceId) || !MeasurementGeometry.TryGetEvidenceAnchor(PairSourceEvidenceId, out var anchor))
        {
            ResetToSelect(GameDialogue.Text("measure-select-evidence"));
            return;
        }
        if (MeasurementGeometry.Distance(anchor, tapeTip.position, true) <= targetRadius)
        {
            CurrentPhase = Phase.PairExtending;
            InteractionSfx.Play(InteractionSound.Measure,tapeTip.position);
            Say(GameDialogue.Text("measure-pair-extend-to-other"), true);
            return;
        }
        if (TrySwitchToAimed())
        {
            return;
        }
        Say(GameDialogue.Text("measure-hold-tape-at", MeasurementStateManager.DisplayName(PairSourceEvidenceId)), false);
    }

    private void TryRecordPair()
    {
        var manager = MeasurementStateManager.Instance;
        string source = PairSourceEvidenceId;
        if (manager == null || string.IsNullOrEmpty(source))
        {
            ResetToSelect(GameDialogue.Text("measure-select-evidence"));
            return;
        }

        // Only discovered items are candidates, so an undiscovered item near the tape is
        // indistinguishable from empty floor (EVIDENCE-LEAK RULE).
        string target = FindDiscoveredEvidenceAtTip();
        if (target == null)
        {
            // No switching here: the tape end is down at the floor, where any aim is incidental.
            // Holstering returns to the held-at-source step, which can switch items.
            Say(GameDialogue.Text("measure-pair-select-other"), false);
            return;
        }
        if (target == source)
        {
            manager.RecordPairMeasurement(source, target, 0f); // logged as SameEvidence
            Say(GameDialogue.Text("measure-pair-select-other"), false);
            return;
        }
        var targetRequirement = FindRequirementFor(target);
        string bodyReason = MeasurementStateManager.GetBodyMeasurementBlockReason(source)
            ?? MeasurementStateManager.GetBodyMeasurementBlockReason(target);
        if (bodyReason != null) { Say(bodyReason, false); return; }
        if (EvidenceStateManager.Instance != null && !EvidenceStateManager.Instance.IsAtOrPastStatus(target, EvidenceStatus.Marked)
            && (targetRequirement == null || !manager.IsDisturbed(targetRequirement)))
        {
            Say(GameDialogue.Text("measure-marker-first"), false);
            return;
        }

        // The authoritative measurement points, never the controller or a hit position.
        if (!manager.TryGetPairSceneDistance(source, target, out float measured))
        {
            Say(GameDialogue.Text("measure-pair-select-other"), false);
            return;
        }

        var result = manager.RecordPairMeasurement(source, target, measured);
        switch (result)
        {
            case EvidencePairRecordResult.Recorded:
            {
                var pair = manager.GetPair(source, target);
                string line = GameDialogue.Text("measure-pair-recorded", MeasurementStateManager.DisplayName(source), MeasurementStateManager.DisplayName(target), measured);
                if (pair != null && pair.takenAfterDisturbance)
                {
                    line = GameDialogue.Join(line, GameDialogue.Text("measure-pair-recorded-after-move"));
                }
                if (manager.AreAvailablePairMeasurementsComplete(source))
                {
                    ResetToSelect(GameDialogue.Join(line, GameDialogue.Text("measure-pair-all-complete")), true);
                }
                else
                {
                    Say(GameDialogue.Join(line, GameDialogue.Text("measure-pair-extend-to-next")), true);
                }
                return;
            }
            case EvidencePairRecordResult.AlreadyComplete:
                Say(GameDialogue.Text("measure-pair-already-measured"), false);
                return;
            case EvidencePairRecordResult.NotRequired:
                Say(GameDialogue.Text("measure-pair-not-required"), false);
                return;
            default:
                Say(GameDialogue.Text("measure-pair-select-other"), false);
                return;
        }
    }

    // Same as fixed-reference mode: before the tape is hooked at the item, aiming at a
    // different measurable item starts measuring that one instead, so Evidence Distance mode
    // never traps the player on one item (e.g. when its remaining partner isn't marked yet).
    private bool TrySwitchToAimed()
    {
        var aimed = FindAimedEvidence();
        if (aimed == null || aimed == PairSourceEvidenceId || FindRequirementFor(aimed) == null)
        {
            return false;
        }
        ResetToSelect(null);
        TrySelectAimed();
        return true;
    }

    // The discovered item whose measurement point is nearest the tape end, within targetRadius.
    private string FindDiscoveredEvidenceAtTip()
    {
        Vector3 tip = tapeTip.position;
        string best = null;
        float bestDistance = targetRadius;
        foreach (var prop in EvidenceProp.All)
        {
            if (prop == null || string.IsNullOrEmpty(prop.evidenceId) || !MeasurementStateManager.IsDiscoveredForMeasurement(prop.evidenceId)
                || !MeasurementGeometry.TryGetEvidenceAnchor(prop.evidenceId, out var anchor))
            {
                continue;
            }
            float d = MeasurementGeometry.Distance(anchor, tip, true);
            if (d <= bestDistance)
            {
                bestDistance = d;
                best = prop.evidenceId;
            }
        }
        return best;
    }

    // Puts the tool on the selected requirement's next required side, or explains why not.
    private bool ResumeSelected()
    {
        var manager = MeasurementStateManager.Instance;
        var record = manager != null ? manager.GetRecord(SelectedRequirementId) : null;
        if (record == null)
        {
            ResetToSelect(null);
            return false;
        }

        if (record.IsComplete)
        {
            ResetToSelect(GameDialogue.Text("measure-complete"));
            return false;
        }

        string next = NextReference(record);
        if (next == null)
        {
            // Both sides valid but not complete: only possible when taken late under Block.
            ResetToSelect(GameDialogue.Text("measure-evidence-moved"));
            return false;
        }

        CurrentReferenceId = next;
        CurrentPhase = Phase.AtReference;
        Say(GameDialogue.Text("measure-from-reference", ReferenceLabel(record.requirement, next)), true);
        return true;
    }

    // --- Step 2: hook the tape at the required reference ------------------

    private void TryHook()
    {
        var requirement = SelectedRequirement();
        var required = SceneReferencePoint.Find(CurrentReferenceId);
        var record = MeasurementStateManager.Instance != null && requirement != null
            ? MeasurementStateManager.Instance.GetRecord(requirement.requirementId) : null;
        if (requirement == null || required == null || record == null)
        {
            ResetToSelect(GameDialogue.Text("measure-select-evidence"));
            return;
        }

        Vector3 tip = tapeTip.position;
        if (required.ContainsTip(tip, referenceRadius))
        {
            hookedReferencePosition = required.AnchorPosition;
            CurrentPhase = Phase.Extending;
            InteractionSfx.Play(InteractionSound.Measure,tapeTip.position);
            Say(GameDialogue.Text("measure-extend-to-evidence"), true);
            return;
        }

        // The item's OTHER reference, if it still needs measuring: either order is fine.
        string otherId = CurrentReferenceId == requirement.referenceAId ? requirement.referenceBId : requirement.referenceAId;
        var other = SceneReferencePoint.Find(otherId);
        if (other != null && SideNeeded(record, otherId) && other.ContainsTip(tip, referenceRadius))
        {
            CurrentReferenceId = otherId;
            hookedReferencePosition = other.AnchorPosition;
            CurrentPhase = Phase.Extending;
            InteractionSfx.Play(InteractionSound.Measure,tapeTip.position);
            Say(GameDialogue.Text("measure-extend-to-evidence"), true);
            return;
        }

        // A reference that belongs to a different item never counts for this one.
        foreach (var candidate in SceneReferencePoint.All)
        {
            if (candidate != null && !requirement.UsesReference(candidate.referenceId) && candidate.ContainsTip(tip, referenceRadius))
            {
                Say(GameDialogue.Text("measure-wrong-reference"), false);
                return;
            }
        }

        // Not at any reference: aiming at a different discovered item switches to it.
        var aimed = FindAimedEvidence();
        var aimedRequirement = aimed != null ? FindRequirementFor(aimed) : null;
        if (aimedRequirement != null && aimedRequirement != requirement)
        {
            SelectedRequirementId = aimedRequirement.requirementId;
            if (!ResumeSelected())
            {
                SelectedRequirementId = null;
            }
            return;
        }

        Say(GameDialogue.Text("measure-hold-tape-at", ReferenceName(CurrentReferenceId)), false);
    }

    // Whether this side of the requirement still needs a (valid) measurement.
    private static bool SideNeeded(MeasurementRecord record, string referenceId)
    {
        var r = record.requirement;
        bool isA = referenceId == r.referenceAId;
        bool has = isA ? record.hasA : record.hasB;
        bool within = isA ? record.withinToleranceA : record.withinToleranceB;
        return !(has && (within || !r.validateTolerance));
    }

    // --- Step 3: record at the evidence ------------------------------------

    private void TryRecord()
    {
        var manager = MeasurementStateManager.Instance;
        var requirement = SelectedRequirement();
        var reference = SceneReferencePoint.Find(CurrentReferenceId);
        if (requirement == null || reference == null
            || !MeasurementGeometry.TryGetEvidenceAnchor(requirement.targetEvidenceId, out var anchor))
        {
            ResetToSelect(GameDialogue.Text("measure-select-evidence"));
            return;
        }

        Vector3 tip = tapeTip.position;
        if (MeasurementGeometry.Distance(anchor, tip, true) > targetRadius)
        {
            Say(GameDialogue.Text("measure-bring-tape-to-evidence"), false);
            return;
        }

        // Full precision in, two decimals only for display.
        float measured = MeasurementGeometry.DistanceToReference(reference, tip, requirement.measureOnFloorPlan);
        bool late = requirement.mustPrecedeDisturbance && manager.IsDisturbed(requirement);
        string label = ReferenceLabel(requirement, CurrentReferenceId);
        bool isA = CurrentReferenceId == requirement.referenceAId;

        var result = manager.RecordMeasurement(requirement.requirementId, CurrentReferenceId, measured);
        if (result == MeasurementRecordResult.PrerequisiteBlocked)
        {
            ResetToSelect(MeasurementStateManager.GetBodyMeasurementBlockReason(requirement.targetEvidenceId));
            return;
        }
        var record = manager.GetRecord(requirement.requirementId);
        bool sideValid = !requirement.validateTolerance || (isA ? record.withinToleranceA : record.withinToleranceB);

        if (result == MeasurementRecordResult.AlreadyComplete)
        {
            ResetToSelect(GameDialogue.Text("measure-complete"));
            return;
        }
        if (result != MeasurementRecordResult.Recorded && result != MeasurementRecordResult.Completed)
        {
            ResetToSelect(GameDialogue.Text("measure-select-evidence"));
            return;
        }

        if (late)
        {
            Say(GameDialogue.Text("measure-evidence-moved"), false);
            if (result == MeasurementRecordResult.Completed || NextReference(record) == null)
            {
                ResetToSelect(null);
                return;
            }
            CurrentReferenceId = NextReference(record);
            CurrentPhase = Phase.AtReference;
            return;
        }

        if (!sideValid)
        {
            CurrentPhase = Phase.AtReference;
            Say(GameDialogue.Text("measure-out-of-range"), false);
            return;
        }

        if (result == MeasurementRecordResult.Completed)
        {
            ResetToSelect(GameDialogue.Text("measure-reference-recorded-complete", label, measured), true);
            return;
        }

        CurrentReferenceId = NextReference(record);
        CurrentPhase = Phase.AtReference;
        Say(GameDialogue.Text("measure-reference-recorded-next", label, measured, ReferenceLabel(requirement, CurrentReferenceId)), true);
    }

    // --- Helpers -----------------------------------------------------------

    // The first side not yet validly recorded - A before B. Null when both are valid.
    private static string NextReference(MeasurementRecord record)
    {
        var r = record.requirement;
        if (!(record.hasA && (record.withinToleranceA || !r.validateTolerance)))
        {
            return r.referenceAId;
        }
        if (!(record.hasB && (record.withinToleranceB || !r.validateTolerance)))
        {
            return r.referenceBId;
        }
        return null;
    }

    // "the West Wall" style name for messages; the reference's own student-facing name.
    private static string ReferenceName(string referenceId)
    {
        var reference = SceneReferencePoint.Find(referenceId);
        return reference != null && !string.IsNullOrEmpty(reference.displayName) ? reference.displayName : "fixed reference";
    }

    // "West Wall" etc. Internal ids (A/B, 1/2) are never shown to the student.
    private static string ReferenceLabel(MeasurementRequirement requirement, string referenceId) => ReferenceName(referenceId);

    // "Fixed Reference 1" / "Fixed Reference 2" - the slot, for the readout.
    private static string ReferenceSlot(MeasurementRequirement requirement, string referenceId) =>
        referenceId == requirement.referenceAId ? "Fixed Reference 1" : "Fixed Reference 2";

    private MeasurementRequirement SelectedRequirement()
    {
        var set = MeasurementStateManager.Instance != null ? MeasurementStateManager.Instance.RequirementSet : null;
        return set != null ? set.Find(SelectedRequirementId) : null;
    }

    private static MeasurementRequirement FindRequirementFor(string evidenceId)
    {
        var set = MeasurementStateManager.Instance != null ? MeasurementStateManager.Instance.RequirementSet : null;
        return set != null ? set.requirements.Find(r => r != null && r.targetEvidenceId == evidenceId) : null;
    }

    // The discovered roster item closest to the tape's aim ray. Only registered records
    // (not sub-step props like a print on the knife) at or after Found - see class comment.
    private string FindAimedEvidence()
    {
        var esm = EvidenceStateManager.Instance;
        if (esm == null)
        {
            return null;
        }

        Vector3 origin = tapeTip.position;
        Vector3 forward = tapeTip.forward;
        string best = null;
        float bestAngle = aimAngle;
        foreach (var prop in EvidenceProp.All)
        {
            if (prop == null || string.IsNullOrEmpty(prop.evidenceId) || esm.GetRecord(prop.evidenceId) == null
                || !esm.IsAtOrPastStatus(prop.evidenceId, EvidenceStatus.Found))
            {
                continue;
            }

            Vector3 point = MeasurementGeometry.TryGetEvidenceAnchor(prop.evidenceId, out var anchor) ? anchor : prop.transform.position;
            Vector3 toPoint = point - origin;
            if (toPoint.magnitude > aimMaxDistance)
            {
                continue;
            }
            float angle = Vector3.Angle(forward, toPoint);
            if (angle < bestAngle)
            {
                bestAngle = angle;
                best = prop.evidenceId;
            }
        }
        return best;
    }

    private void ResetToSelect(string message, bool positive = false)
    {
        CurrentPhase = Phase.SelectTarget;
        SelectedRequirementId = null;
        CurrentReferenceId = null;
        PairSourceEvidenceId = null;
        LiveDistance = -1f;
        if (message != null)
        {
            Say(message, positive);
        }
    }

    private void Say(string message, bool positive)
    {
        LastMessage = message;
        NotificationManager.Notify(message);
        if (positive)
        {
            InteractionFeedback.Confirm(transform);
        }
        else
        {
            InteractionFeedback.Blocked(transform);
        }
    }

    private void RefreshVisuals()
    {
        if (!IsHeld)
        {
            SetGuidesVisible(false);
            return;
        }

        if (CurrentMode == Mode.EvidenceDistance)
        {
            RefreshPairVisuals();
            return;
        }

        var requirement = SelectedRequirement();
        var reference = SceneReferencePoint.Find(CurrentReferenceId);
        bool guiding = CurrentPhase != Phase.SelectTarget && requirement != null && reference != null;
        Vector3 tip = tapeTip.position;

        // While choosing, show which item a trigger press would select - but only an item
        // the player has already marked (identified as evidence), so this never points
        // out evidence the player hasn't found and judged themselves.
        string hoverId = CurrentPhase == Phase.SelectTarget ? FindSelectableHover() : null;

        LiveDistance = -1f;
        if (tapeLine != null)
        {
            bool extending = guiding && CurrentPhase == Phase.Extending;
            tapeLine.enabled = extending;
            if (extending)
            {
                // The hook stays at its snapped point, including its height. Only the
                // hand end moves; the reading retains the authored floor-plan method.
                tapeLine.positionCount = 2;
                tapeLine.SetPosition(0, hookedReferencePosition);
                tapeLine.SetPosition(1, tip);
                LiveDistance = MeasurementGeometry.DistanceToReference(reference, tip, requirement.measureOnFloorPlan);
            }
        }

        if (readout != null)
        {
            readout.text = !guiding
                ? (hoverId != null ? "Pull trigger to measure" : "Aim at a marked item")
                : CurrentPhase == Phase.Extending
                    ? $"{LiveDistance:0.00} m"
                    : $"{ReferenceSlot(requirement, CurrentReferenceId)}\n{ReferenceName(CurrentReferenceId)}";

            var cam = Camera.main;
            if (cam != null)
            {
                readout.transform.rotation = Quaternion.LookRotation(readout.transform.position - cam.transform.position);
            }
        }

        if (referenceHighlight != null)
        {
            referenceHighlight.SetActive(guiding);
            if (guiding)
            {
                PlaceReferenceHighlight(reference);
            }
        }

        if (targetHighlight != null)
        {
            string targetId = guiding ? requirement.targetEvidenceId : hoverId;
            bool showTarget = targetId != null && MeasurementGeometry.TryGetEvidenceAnchor(targetId, out var anchor);
            targetHighlight.SetActive(showTarget);
            if (showTarget)
            {
                MeasurementGeometry.TryGetEvidenceAnchor(targetId, out var a);
                targetHighlight.transform.position = a;
            }
        }
    }

    private void RefreshPairVisuals()
    {
        Vector3 tip = tapeTip.position;
        bool hasSource = MeasurementGeometry.TryGetEvidenceAnchor(PairSourceEvidenceId, out var source);
        bool extending = CurrentPhase == Phase.PairExtending && hasSource;
        LiveDistance = -1f;
        if (tapeLine != null)
        {
            tapeLine.enabled = extending;
            if (extending)
            {
                Vector3 start = source;
                start.y = tip.y;
                tapeLine.positionCount = 2;
                tapeLine.SetPosition(0, start);
                tapeLine.SetPosition(1, tip);
                LiveDistance = MeasurementGeometry.Distance(source, tip, true);
            }
        }
        if (readout != null)
        {
            readout.text = extending ? $"{LiveDistance:0.00} m"
                : $"Evidence Distance\n{MeasurementStateManager.DisplayName(PairSourceEvidenceId)}";
            var cam = Camera.main;
            if (cam != null)
            {
                readout.transform.rotation = Quaternion.LookRotation(readout.transform.position - cam.transform.position);
            }
        }
        if (referenceHighlight != null)
        {
            referenceHighlight.SetActive(false);
        }
        if (targetHighlight != null)
        {
            targetHighlight.SetActive(hasSource);
            if (hasSource)
            {
                targetHighlight.transform.position = source;
            }
        }
    }

    // The aimed item, if a trigger press would actually start measuring it: it has a
    // measurement requirement that isn't complete yet, and it is marked (or already
    // disturbed, which TrySelectAimed also accepts). Same checks as TrySelectAimed.
    private string FindSelectableHover()
    {
        var evidenceId = FindAimedEvidence();
        var manager = MeasurementStateManager.Instance;
        if (evidenceId == null || manager == null)
        {
            return null;
        }
        var requirement = FindRequirementFor(evidenceId);
        var record = requirement != null ? manager.GetRecord(requirement.requirementId) : null;
        if (record == null || (record.IsComplete && manager.AreAvailablePairMeasurementsComplete(evidenceId)))
        {
            return null;
        }
        bool marked = EvidenceStateManager.Instance != null && EvidenceStateManager.Instance.IsAtOrPastStatus(evidenceId, EvidenceStatus.Marked);
        return marked || manager.IsDisturbed(requirement) ? evidenceId : null;
    }

    // --- Reference highlight placement (visual only - never affects what is measured) ---

    private struct HighlightPose
    {
        public Vector3 basePosition;
        public float poleHeight;
    }

    private readonly System.Collections.Generic.Dictionary<SceneReferencePoint, HighlightPose> highlightPoses = new();
    private Transform highlightPole;
    private Vector3 poleBaseScale;
    private bool poleScaleRead;

    /// <summary>
    /// Stands the reference highlight on the FLOOR in front of the referenced surface, with
    /// its pole ending at the top of that surface (the countertop for a cabinet edge),
    /// instead of planting it at the anchor's height: anchors sit at 0.6-1.0 m, so the old
    /// placement floated the base disc at counter height and ran the 2.4 m pole up through
    /// the cabinet. The anchor itself (what the tape measures to) is unchanged.
    /// Computed once per reference (static scenery) and cached.
    /// </summary>
    private void PlaceReferenceHighlight(SceneReferencePoint reference)
    {
        if (!highlightPoses.TryGetValue(reference, out var pose))
        {
            pose = ComputeHighlightPose(reference);
            highlightPoses[reference] = pose;
        }
        referenceHighlight.transform.position = pose.basePosition;

        if (!poleScaleRead)
        {
            poleScaleRead = true;
            highlightPole = referenceHighlight.transform.Find("Pole");
            if (highlightPole != null)
            {
                poleBaseScale = highlightPole.localScale;
            }
        }
        if (highlightPole != null)
        {
            // Unit cylinder: 2 units tall, centred on its pivot.
            highlightPole.localScale = new Vector3(poleBaseScale.x, pose.poleHeight * 0.5f, poleBaseScale.z);
            highlightPole.localPosition = new Vector3(highlightPole.localPosition.x, pose.poleHeight * 0.5f, highlightPole.localPosition.z);
        }
    }

    private static HighlightPose ComputeHighlightPose(SceneReferencePoint reference)
    {
        Vector3 anchor = reference.AnchorPosition;
        Vector3 normal = reference.shape == SceneReferencePoint.ReferenceShape.Plane ? reference.SurfaceNormal(true) : Vector3.zero;
        normal.y = 0f;
        normal = normal.sqrMagnitude > 1e-6f ? normal.normalized : Vector3.zero;

        // Floor: straight down just in front of the surface.
        float floorY = anchor.y;
        if (TryHighestStaticHit(anchor + normal * 0.05f, anchor.y + 0.05f, 3f, out float floorHit))
        {
            floorY = floorHit;
        }

        // Top of the referenced fixture: down from just above, a little INSIDE the surface.
        // Walls have no top within reach - keep the pole to roughly the anchor's height.
        float topY = anchor.y;
        if (normal != Vector3.zero && TryHighestStaticHit(anchor - normal * 0.03f, anchor.y + 0.5f, anchor.y + 0.5f - floorY, out float topHit)
            && topHit > floorY + 0.1f)
        {
            topY = topHit;
        }
        return new HighlightPose
        {
            basePosition = new Vector3(anchor.x, floorY, anchor.z),
            poleHeight = Mathf.Clamp(topY - floorY, 0.2f, 1.5f)
        };
    }

    // Highest hit on static scenery below a point - skips
    // evidence, held tools and anything with a moving rigidbody (a bottle on the counter
    // must not become the "top" of the counter).
    private static bool TryHighestStaticHit(Vector3 xz, float fromY, float distance, out float y)
    {
        y = 0f;
        var hits = Physics.RaycastAll(new Vector3(xz.x, fromY, xz.z), Vector3.down, Mathf.Max(0.01f, distance), ~0, QueryTriggerInteraction.Ignore);
        bool found = false;
        foreach (var hit in hits)
        {
            if (hit.collider.GetComponentInParent<EvidenceProp>() != null || hit.collider.GetComponentInParent<PlayerTool>() != null
                || (hit.rigidbody != null && !hit.rigidbody.isKinematic))
            {
                continue;
            }
            if (!found || hit.point.y > y)
            {
                y = hit.point.y;
                found = true;
            }
        }
        return found;
    }

    private void SetGuidesVisible(bool visible)
    {
        if (referenceHighlight != null)
        {
            referenceHighlight.SetActive(visible);
        }
        if (targetHighlight != null)
        {
            targetHighlight.SetActive(visible);
        }
        if (tapeLine != null && !visible)
        {
            tapeLine.enabled = false;
        }
    }
}
