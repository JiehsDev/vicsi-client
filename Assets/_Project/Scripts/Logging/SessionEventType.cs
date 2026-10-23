// Assets/_Project/Scripts/Logging/SessionEventType.cs

/// <summary>
/// Every kind of event SessionLogger can record. This is the single place a new
/// phase registers a new event type - nothing else in the project should invent its
/// own event-type strings. Not every value here has something calling it yet (see
/// SessionLogger for what's actually wired up today: evidence status changes and
/// blocked transitions, subscribed automatically); the rest exist so the type is
/// ready before the system that fires them is built - Photography/Briefing/
/// Deduction Board scripts call SessionLogger.Instance.LogEvent(...) directly with
/// these once those systems exist or are being touched.
/// </summary>
public enum SessionEventType
{
    SessionStarted,
    SessionEnded,

    SceneEntered,
    PPEEquipped,
    SceneLogSigned,

    EvidenceStatusChanged,
    EvidenceTransitionBlocked,

    ToolEquipped,
    ToolUnequipped,

    PhotoTaken,

    BriefingStatementViewed,
    BriefingStatementFlagged,
    BriefingCompleted,

    HypothesisSubmitted,

    /// <summary>
    /// The player confirmed a drag-and-drop evidence report via RecorderTool/
    /// RecorderReportUI: which ReportLineOption ids ended up in the Notes zone when
    /// Confirm was pressed. Fired once per successful MarkLogged, same granularity
    /// HypothesisSubmitted captures for a checkpoint answer - real per-decision data
    /// worth keeping, not just the fact that Logged happened (EvidenceStatusChanged
    /// already covers that).
    /// </summary>
    ReportLinesSelected,

    DeductionLinkCreated,
    DeductionLinkRemoved,
    DeductionSubmitted,

    /// <summary>
    /// The player placed an evidence tent on something that is not evidence at all -
    /// bare floor, scene dressing, untagged geometry. No status transition happens
    /// (there is nothing to transition), so this event is the ONLY record that the
    /// mis-identification occurred. It is the false-positive signal precision scoring
    /// will read against EvidenceRelevance.
    /// </summary>
    NonEvidenceMarked,

    /// <summary>
    /// The player pulled a placed tent back off, while the item was still only Marked -
    /// a legitimate pre-documentation correction. Appended alongside the original
    /// placement event, which is never deleted or rewritten; see
    /// EvidenceStateManager.TryReclaimMarker for why scoring must read the whole
    /// history rather than the final state.
    /// </summary>
    MarkerReclaimed,

    /// <summary>
    /// The player tried to reclaim a tent on an item already Photographed or later,
    /// and was refused. Logged rather than silently ignored, on the same reasoning as
    /// EvidenceTransitionBlocked: a blocked attempt is itself diagnostic.
    /// </summary>
    MarkerReclaimBlocked,

    /// <summary>
    /// A RelationshipDefinition's unlockRequirement became satisfied - see
    /// RelationshipStateManager.RefreshUnlocks. Fired once per relationship, the moment
    /// it first advances past Locked.
    /// </summary>
    RelationshipUnlocked,

    /// <summary>
    /// The player drew a connection on the deduction board matching a RelationshipDefinition's
    /// (sourceId, targetId) pair - see RelationshipStateManager.MarkSelectedForPair. Fired
    /// once per relationship, the first time it is ever selected; a later disconnect/
    /// reconnect of the same pair does not re-fire this. Deliberately logged the moment
    /// the connection is drawn, not at Submit Theory - see RelationshipRemoved's own
    /// comment for why the log is never rewritten on a later disconnect.
    /// </summary>
    RelationshipAttempted,

    /// <summary>
    /// The player pulled apart a connection that had matched a RelationshipDefinition -
    /// see RelationshipStateManager.MarkDeselectedForPair. Appended alongside the original
    /// RelationshipAttempted event, which is never deleted or rewritten - same "log full
    /// history, not final snapshot" rule as MarkerReclaimed/EvidenceStateManager.
    /// TryReclaimMarker's own comment.
    /// </summary>
    RelationshipRemoved,

    /// <summary>
    /// A NarrativeEvent was queued to play - see NarrativeEventManager.Enqueue. Fired
    /// once per firing (repeatable events log this again on each subsequent fire).
    /// </summary>
    NarrativeEventTriggered,

    /// <summary>An Objective's unlockRequirement became satisfied (Hidden -> Active). See ObjectiveStateManager.RefreshUnlocks.</summary>
    ObjectiveUnlocked,

    /// <summary>An Objective's completionRequirement became satisfied (Active -> Completed).</summary>
    ObjectiveCompleted,

    /// <summary>An Objective's failureRequirement became satisfied (Active -> Failed).</summary>
    ObjectiveFailed,

    /// <summary>The player invoked Submit Conclusion and EndingManager's submissionRequirement was satisfied. See EndingManager.TrySubmitConclusion.</summary>
    ConclusionSubmissionAttempted,

    /// <summary>The player invoked Submit Conclusion but submissionRequirement was not yet satisfied - blocked, same reasoning as EvidenceTransitionBlocked (a blocked attempt is itself diagnostic).</summary>
    ConclusionSubmissionBlocked,

    /// <summary>The player picked a ConclusionOption at the conclusion-picker screen.</summary>
    ConclusionSelected,

    /// <summary>An EndingDefinition was resolved and shown - payload includes which rule selected it (priority match vs fallback). See EndingManager.ResolveEnding.</summary>
    EndingSelected,

    /// <summary>Wrong marker kind, invalid label, or a marker already assigned to another item.</summary>
    EvidenceMarkerRejected,

    /// <summary>
    /// A sealed item was successfully admitted to EvidenceInventory (custody Scene ->
    /// Inventory) - see EvidenceInventory.Add, called from EvidenceBagTool.TrySeal/
    /// SwabTool.TrySeal once the underlying MarkSealed transition itself succeeds.
    /// Payload: previousCustodyLocation, newCustodyLocation.
    /// </summary>
    EvidenceAddedToInventory,

    /// <summary>
    /// A piece of evidence was equipped from inventory into the player's hand (custody
    /// Inventory -> Held) via HeldItemCoordinator/EvidenceInventory.EquipHeld. Deliberately
    /// separate from the pre-existing ToolEquipped, which stays scoped to PlayerTool
    /// equips only - the two are never fired for the same action. Payload:
    /// previousCustodyLocation, newCustodyLocation.
    /// </summary>
    EvidenceEquipped,

    /// <summary>
    /// Held evidence returned to inventory (custody Held -> Inventory) - either the
    /// player explicitly unequipped it, or equipping a tool/different evidence item
    /// unequipped it automatically. Mirrors EvidenceEquipped; never fired for a
    /// successful deposit (see EvidenceDeposited instead, custody Held -> EvidenceBox,
    /// not Held -> Inventory). Payload: previousCustodyLocation, newCustodyLocation.
    /// </summary>
    EvidenceUnequipped,

    /// <summary>
    /// The player pressed A inside an EvidenceBoxDepositZone with something in hand -
    /// fired on every attempt, successful or not, same "a blocked attempt is itself
    /// diagnostic" reasoning as EvidenceTransitionBlocked. Payload: boxId.
    /// </summary>
    EvidenceDepositAttempted,

    /// <summary>
    /// A deposit attempt was refused - held item is a Tool, not Evidence; nothing held;
    /// eligibility requirement not satisfied; PlayerUIGate blocked; etc. Payload:
    /// boxId, blockReason.
    /// </summary>
    EvidenceDepositBlocked,

    /// <summary>
    /// A deposit succeeded (custody Held -> EvidenceBox) - see EvidenceInventory.Deposit.
    /// Payload: previousCustodyLocation, newCustodyLocation, boxId.
    /// </summary>
    EvidenceDeposited,

    /// <summary>
    /// A FindingDefinition's unlockRequirement became satisfied - see
    /// FindingStateManager.RefreshFindings. Fired once per finding, the moment it first
    /// becomes confirmed; a redundant later refresh never re-fires this. Payload:
    /// displayName, sourceEvidenceId.
    /// </summary>
    FindingConfirmed,

    /// <summary>
    /// An InsightDefinition's unlockRequirement became satisfied - see
    /// InsightStateManager.RefreshInsights. Fired once per insight, the moment it
    /// first unlocks; a redundant later refresh never re-fires this. Payload:
    /// displayName, triggeringFindingId (which finding's confirmation triggered the
    /// refresh pass that unlocked it, if any - descriptive only).
    /// </summary>
    InsightUnlocked,

    /// <summary>
    /// Shutter pressed while standing in a scene photo point's zone (the current point, or
    /// a later point aimed at correctly) - see ScenePhotographyManager. targetId:
    /// photoPointId. Payload: phase, sequenceIndex, result, angle.
    /// </summary>
    ScenePhotoAttempted,

    /// <summary>A scene photo point was completed - once per point. targetId: photoPointId. Payload: phase, sequenceIndex, angle, inOrder.</summary>
    ScenePhotoCompleted,

    /// <summary>A later scene photo point was correctly photographed before an earlier required one - not completed. targetId: photoPointId. Payload: expectedPointId, phase.</summary>
    ScenePhotoOutOfSequence,

    /// <summary>Standing in the current scene photo point's zone but the camera faced away from its target - not completed. targetId: photoPointId. Payload: angle, tolerance.</summary>
    ScenePhotoWrongAngle,

    /// <summary>Every required scene photo point of one phase is complete - once per phase. targetId: phase name.</summary>
    ScenePhotographyPhaseCompleted,

    /// <summary>Every required scene photo point is complete - once per session. Payload: completedCount, outOfSequenceAttempts, wrongAngleAttempts.</summary>
    ScenePhotographyCompleted,

    /// <summary>
    /// The scene photography route became active (first point shown) - once per session.
    /// The server's scene-photography score uses this as the coverage denominator and the
    /// completion-time start. Payload: requiredPointCount, pointCount.
    /// </summary>
    ScenePhotographyStarted,

    /// <summary>A pure scene shot (no evidence in frame) aimed at the current point's view from near, but outside, its standing zone - not completed. targetId: photoPointId. Payload: distance, angle.</summary>
    ScenePhotoWrongPosition,

    /// <summary>
    /// An action the game allowed, but performed before its expected prerequisite (see
    /// ProcedureLog.LogOutOfOrder). targetId: evidenceId, or procedureId for a
    /// scene-level procedure. Payload: procedureId, evidenceId, expectedPrerequisite,
    /// sessionTimeSeconds, optional context. Not scored yet.
    /// </summary>
    ProcedureOutOfOrder,

    // --- Evidence-position measurement (see MeasurementStateManager). targetId: evidenceId.
    // Payload always: requirementId, evidenceId, sessionTimeSeconds; per-side events add
    // referenceId, measuredDistance, expectedDistance, error, tolerance, withinTolerance,
    // afterDisturbance. Not scored yet.

    /// <summary>First distance recorded for a measurement requirement - once per requirement.</summary>
    MeasurementStarted,

    /// <summary>One reference distance recorded (or re-recorded).</summary>
    MeasurementTaken,

    /// <summary>Both distances recorded and valid - once per requirement. Payload: measuredDistanceA/B, errorA/B, withinTolerance, takenBeforeDisturbance.</summary>
    MeasurementCompleted,

    /// <summary>A recorded distance outside the requirement's tolerance (validated requirements only).</summary>
    MeasurementOutOfTolerance,

    /// <summary>A distance recorded after the item was disturbed (collected / out of scene custody). Payload adds enforcement: warned | blocked.</summary>
    MeasurementAfterDisturbance,

    // --- Crime Scene Sketch (see CrimeSceneSketchManager) - a scene-level procedure, never
    // an EvidenceStatus. Payload always: sketchId, sessionTimeSeconds; placement events add
    // order (running count of sketch placement attempts). Not scored yet - logged so a
    // later Sketch assessment can read correct and incorrect attempts.

    /// <summary>The sketch board was opened. targetId: sketchId. Payload adds state, openCount.</summary>
    SketchOpened,

    /// <summary>An evidence element was placed on its correct anchor. targetId: evidenceId. Payload adds evidenceId, anchorId.</summary>
    SketchEvidencePlaced,

    /// <summary>An evidence element was dropped on the board somewhere other than its anchor (returned to the palette). targetId: evidenceId. Payload adds evidenceId, attemptedAnchorId ("none" = open board).</summary>
    SketchPlacementIncorrect,

    /// <summary>A measurement card was placed in its slot. targetId: evidenceId. Payload adds evidenceId, referenceId, requirementId, slotId, measuredDistance.</summary>
    SketchMeasurementPlaced,

    /// <summary>A measurement card was dropped on another slot (returned). targetId: the card's evidenceId. Payload adds evidenceId, referenceId, requirementId, attemptedSlotId.</summary>
    SketchMeasurementIncorrect,

    /// <summary>The completed sketch was submitted (once per session; read-only afterwards). targetId: sketchId. Payload adds evidencePlaced, measurementsPlaced, incorrectEvidenceAttempts, incorrectMeasurementAttempts.</summary>
    SketchSubmitted,

    // --- Evidence-to-evidence distances (see MeasurementStateManager.Pairs). targetId: the
    // normalised pair key "A|B". Payload always: pairKey, evidenceA, evidenceB,
    // sessionTimeSeconds. Not scored yet.

    /// <summary>A required pair distance recorded (once per pair). Payload adds measuredDistance, expectedDistance, tolerance, withinTolerance, afterDisturbance, order, measuredFrom.</summary>
    EvidencePairMeasured,

    /// <summary>A pair measurement refused for two discovered items. Payload adds reason (SameEvidence | NotRequired | AlreadyComplete | NoMeasurementPoint).</summary>
    EvidencePairMeasurementRejected,

    // --- Scene turnover (see SceneTurnover / FirstResponderTurnover). Informational, not scored.

    /// <summary>The player started talking to the First Responder (once per session). targetId: "first-responder-turnover". Payload: lineCount.</summary>
    FirstResponderInteractionStarted,

    /// <summary>The First Responder turned the scene over to IOC (SOCO processing does not begin) (once per session). targetId: "first-responder-turnover". Payload: via (Conversation | Bypass), sessionTimeSeconds.</summary>
    SceneTurnoverCompleted,

    /// <summary>Sketch: an evidence element's placement area was highlighted after repeated wrong drops (once per element). targetId: evidenceId. Payload: evidenceId, incorrectAttemptsBeforeHint, sketchId. Not scored; lets assessment tell assisted placements from unaided ones.</summary>
    SketchPlacementHintShown,

    /// <summary>A second numbered tent was placed on an item's trace (the blood on the knife blade) - one record, two tents. targetId: evidenceId. Payload: tentNumber, itemTentNumber. Not scored yet.</summary>
    EvidenceTraceMarked,

    /// <summary>IOC received the prepared CSI Form 1 during FR turnover. Informational, not scored. targetId: CSI-Form-1. Payload: receivingRole, via.</summary>
    CSIForm1Received,

    // Informational IOC procedure events. Observation targetId is a stable authored ID.
    IOCAssessmentStarted,
    IOCObservationRecorded,
    IOCAssessmentCompleted,
    // Informational IOC/SOCO milestones; appended to preserve historical event ordinals.
    SOCOAssistanceDecision,
    SOCORequested,
    CSIForm2Completed,
    SOCOTurnoverStarted,
    SOCOTurnoverCompleted,
    SOCOInvestigationStarted,
    WitnessFollowupQuestionSelected,
    WitnessStatementDiscovered,
    SOCOCloseoutStarted,
    EvidenceInventoryConfirmed,
    FinalSceneSurveyCompleted,
    SceneReleaseConfirmed,
    InvestigationCompleted,

    // Append-only: separate Phase 3 snapshots, not an aggregate-score input.
    ReasoningCheckpointSubmitted,
    ReasoningCheckpointEligible,
    // Append-only 1.7.0 conclusion-time facts/locked IDs; no live score feedback.
    DeductionAssessmentSnapshot,
    // Body-only procedural training, never deduction-grade inputs. Append-only ordinals.
    BodyDocumentationRecorded,
    BodyPreparedForRemoval,
    BodyRemoved,
    BodyRemovalBlocked,
    FingerprintIdentified,
    RelationshipInterpretationSubmitted,
    CustodyPassCompleted,
    EvidenceFingerprintProcessed,
    EvidenceSwabbed,
    // Append-only local scene-access training events; SceneEntered retains scene-load semantics.
    SceneAccessEntered,
    SceneAccessExited,
    SceneAccessRoleChanged,
    SceneAccessSimulationTransition,
    SceneAccessRegisterFinalized,
    // Append-only local, non-scored camera-accessory training events.
    PhotographicScalePlaced,
    PhotographicScaleCaptured,
    PhotographicScaleRemoved,
    GlovesUsedForSampling,
    UsedGlovesDisposed,
    FreshGlovesEquipped,
    FreshSwabPackPickedUp,
    FreshSwabPackReturned,
    FreshSwabLoaded,
    FreshSwabUsed,
    SampleDryingStarted,
    SampleDryingCompleted,
    SamplePackaged,
    FingerprintPracticeOpened,
    FingerprintPracticeCompleted
}
