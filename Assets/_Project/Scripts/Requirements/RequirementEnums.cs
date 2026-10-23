// Assets/_Project/Scripts/Requirements/RequirementEnums.cs

/// <summary>
/// Whether one RequirementNode is a concrete check (Leaf) or a logical combination of
/// child nodes (Group). See RequirementNode's own comment for why this is a single
/// union class rather than a Leaf/Group class pair.
/// </summary>
public enum RequirementNodeKind
{
    Leaf = 0,
    Group = 1
}

/// <summary>
/// How a Group node combines its children. Not, despite the name, evaluated as a binary
/// operator - it is a unary "invert my one child" node, same as any other logic-gate
/// authoring tool. See RequirementNode.Validate for why children.Count must be exactly 1
/// for Not, and RequirementEvaluator for the empty-ALL/empty-ANY truth table this project
/// has settled on (empty ALL = true, empty ANY = false).
/// </summary>
public enum RequirementLogicalOperator
{
    All = 0,
    Any = 1,
    Not = 2
}

/// <summary>
/// What a Leaf node actually checks. Explicit ints from the start, appended-only forever
/// after - the same discipline EvidenceStatus/ToolType/SessionEventType already follow in
/// this project, applied here before any real content has a chance to persist an implicit
/// ordinal. Adding a new leaf type never renumbers an existing one; existing authored
/// RequirementNode trees keep evaluating exactly as before regardless of how many new
/// values are appended below Reserved.
///
/// Each value's evidence/authoritative-source mapping (see RequirementEvaluator):
///   EvidenceStatusAtOrAbove       -> EvidenceStateManager.IsAtOrPastStatus (sequence-aware)
///   ReportLineSelected            -> EvidenceStateManager.HasSelectedReportLine
///   FingerprintLabStatusAtOrAbove -> EvidenceStateManager.IsFingerprintLabStatusAtOrAbove
///   EvidenceSwabbed               -> EvidenceRecord.swabbingDone
///   EvidenceFingerprinted         -> EvidenceRecord.fingerprintingDone
///   EvidenceFlipped               -> EvidenceRecord.isFlipped
///   EvidenceDiscovered            -> EvidenceStateManager.IsAtOrPastStatus(id, Found) (authoring shorthand)
///   LegacySelectionPrerequisite   -> SelectionPrerequisite.EvaluateOne, verbatim - the
///                                    backward-compatibility path; see RequirementNode.FromLegacyList.
///   RequirementAssetReference     -> recurses into a RequirementDefinition asset's root
///                                    node, guarded against cycles - see RequirementEvaluator.
///   NarrativeEventCompleted       -> NarrativeEventManager.IsCompleted. The single unlock
///                                    mechanism for "unlocked by a narrative event" across
///                                    every consumer (relationships, report lines, checkpoints,
///                                    and later objectives/endings) - a consumer that wants
///                                    this simply authors this leaf into its own requirement
///                                    tree, rather than NarrativeEvent pushing unlock state
///                                    out to four different systems.
///   ObjectiveCompleted            -> ObjectiveStateManager.IsCompleted. Also how "depends
///                                    on an earlier objective" is expressed - there is no
///                                    separate dependency-list field on Objective, an
///                                    objective that depends on another just authors this
///                                    leaf into its own unlockRequirement.
///   RelationshipCompleted         -> RelationshipStateManager's record state == Completed.
///                                    Added alongside ObjectiveCompleted for symmetry - a
///                                    relationship has its own completion state too, and
///                                    nothing could reference it until now.
///   FindingConfirmed              -> FindingStateManager.IsConfirmed. The layer BELOW
///                                    RelationshipCompleted in the Finding -> Insight ->
///                                    Relationship -> Conclusion chain - lets a future
///                                    InsightDefinition/RelationshipDefinition/
///                                    ConclusionDefinition say "FindingConfirmed(FIND-x)"
///                                    without any hardcoded per-finding code, the same way
///                                    every other leaf here already works.
/// </summary>
public enum RequirementType
{
    EvidenceStatusAtOrAbove = 0,
    ReportLineSelected = 1,
    FingerprintLabStatusAtOrAbove = 2,
    EvidenceSwabbed = 3,
    EvidenceFingerprinted = 4,
    EvidenceFlipped = 5,
    EvidenceDiscovered = 6,
    LegacySelectionPrerequisite = 7,
    RequirementAssetReference = 8,
    NarrativeEventCompleted = 9,
    ObjectiveCompleted = 10,
    RelationshipCompleted = 11,
    FindingConfirmed = 12,

    /// <summary>
    /// ScenePhotographyManager.IsScenePhotographyComplete - every required scene photo
    /// point is done. No fields. Fails safely (MissingRuntimeManager) when the scene has
    /// no ScenePhotographyManager. Appended - never renumber the values above.
    /// </summary>
    ScenePhotographyCompleted = 13,

    /// <summary>
    /// MeasurementStateManager.IsMeasurementComplete(measurementRequirementId) - both
    /// reference distances recorded and valid. Fails safely (MissingRuntimeManager) when
    /// the scene has no MeasurementStateManager. Appended - never renumber.
    /// </summary>
    MeasurementCompleted = 14,

    /// <summary>
    /// MeasurementStateManager.AreSceneMeasurementsComplete - every required item's two
    /// fixed references AND every required evidence-to-evidence pair of the configured
    /// scene roster (the full roster, not just what has been discovered). No fields.
    /// For final scene-level tasks only (sketch submission, investigation completion) -
    /// never as a per-record gate or anywhere its refusal is shown to the player, since
    /// "still incomplete" would reveal undiscovered evidence. Appended - never renumber.
    /// </summary>
    SceneMeasurementsComplete = 15,

    /// <summary>
    /// MeasurementStateManager.AreAvailablePairMeasurementsComplete(evidenceId) - every
    /// required pair involving evidenceId whose other item is already discovered has been
    /// measured. Pairs with items not yet discovered don't count (and are never revealed).
    /// Appended - never renumber.
    /// </summary>
    EvidencePairMeasurementsComplete = 16,

    /// <summary>
    /// MeasurementStateManager.AreDiscoveredSceneMeasurementsComplete - both fixed
    /// references of every DISCOVERED required item and every pair among discovered items.
    /// Discovery-safe: an undiscovered item never makes this fail, so it can gate the
    /// Recorder without hinting that more evidence exists. No fields. Appended - never renumber.
    /// </summary>
    DiscoveredSceneMeasurementsComplete = 17,

    /// <summary>
    /// SceneTurnover.IsComplete - the First Responder has turned the scene over to IOC
    /// (or the scene has no turnover gate, which reads as already started). No fields.
    /// Appended - never renumber.
    /// </summary>
    SceneTurnoverCompleted = 18,

    /// <summary>IOC initial assessment accepted through the procedural phase owner. No fields.</summary>
    IOCAssessmentCompleted = 19,
    /// <summary>Authoritative procedural phase has reached scenarioPhase. No evidence mutation.</summary>
    ScenarioPhaseReached = 20,
    /// <summary>Session-local witness record. evidenceId carries the stable statement ID.</summary>
    WitnessStatementDiscovered = 21
}

/// <summary>
/// Why a RequirementEvaluationResult failed (or that it didn't). Dev/debug-facing only -
/// see RequirementEvaluationResult's own comment on why none of this may ever be shown to
/// the player during normal investigation.
/// </summary>
public enum RequirementFailureReason
{
    None = 0,
    Passed = 1,
    MissingEvidenceId = 2,
    UnknownEvidenceId = 3,
    MissingRuntimeManager = 4,
    EmptyAnyGroup = 5,
    InvalidNotGroup = 6,
    StatusNotReached = 7,
    ReportLineNotSelected = 8,
    LabStatusNotReached = 9,
    SwabNotDone = 10,
    FingerprintNotDone = 11,
    NotFlipped = 12,
    NotDiscovered = 13,
    LegacyPrerequisiteFailed = 14,
    MissingRequirementAsset = 15,
    CircularReference = 16,
    Unconfigured = 17,
    MissingNarrativeEventId = 18,
    NarrativeEventNotCompleted = 19,
    MissingObjectiveId = 20,
    ObjectiveNotCompleted = 21,
    MissingRelationshipId = 22,
    RelationshipNotCompleted = 23,
    MissingFindingId = 24,
    FindingNotConfirmed = 25,
    ScenePhotographyIncomplete = 26,
    MissingMeasurementRequirementId = 27,
    MeasurementNotCompleted = 28,
    SceneMeasurementsIncomplete = 29,
    PairMeasurementsIncomplete = 30,
    DiscoveredSceneMeasurementsIncomplete = 31
}
