// Assets/_Project/Scripts/CaseFile/EvidenceDefinition.cs
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One candidate line the player can drag into the report notes for an evidence item,
/// offered alongside plausible-but-wrong filler lines - multiple choice, not free
/// text, same "real judgment, fully scoreable, no VR keyboard" reasoning already
/// applied to ReasoningOption. id is stable and author-set, not index-derived, for
/// the same reason every other id in this project is: reordering options must never
/// repoint a session log entry logged against an earlier build at the wrong line.
/// </summary>
[System.Serializable]
public class ReportLineOption
{
    [Tooltip("Stable, author-set identifier (e.g. \"knife-014-blood-trace\"). Used as the logged value in the session event, so it must NOT be derived from list position.")]
    public string id;

    [TextArea]
    [Tooltip("The candidate report line as the player reads it, e.g. \"Trace blood residue observed along the blade edge.\"")]
    public string text;

    [Tooltip("Authoring metadata only, same boundary as ReasoningOption.isCorrect: NOTHING READS THIS in this pass. No scoring logic runs against it yet - it exists so lines can be authored now and scored once faculty-validated answers exist.")]
    public bool isAccurate;

    [Tooltip("Optional cross-item unlock: every entry must already be satisfied (that evidenceId's line was selected earlier THIS session - see EvidenceStateManager.HasSelectedReportLine) before this option appears in the candidate pool at all. Empty = always available, the unchanged default for every option authored before this field existed. A list, not a single pair, so an option can eventually require more than one prior selection - the current roster only ever needs one entry (the knife's option gated on the body's wound line).")]
    public List<SelectionPrerequisite> prerequisites = new();

    [Tooltip("Optional ADDITIONAL gating condition, checked alongside prerequisites above (both must pass) - the same RequirementEvaluator/RequirementNode extension ReasoningOption.extraRequirement already uses, letting a report line be gated by ALL/ANY/NOT trees or a NarrativeEventCompleted leaf, which the flat prerequisites list above cannot express. Defaults to an empty ALL group (vacuously true), so every option authored before this field existed is unaffected. See RecorderReportUI.PrerequisitesSatisfied.")]
    public RequirementNode extraRequirement = new();
}

/// <summary>
/// One player-facing finding in an evidence note. The text is authored on the
/// EvidenceDefinition, while revealRequirement is evaluated against the same
/// authoritative gameplay state used by reports, procedures, relationships and
/// endings. An unsatisfied finding is omitted completely rather than shown as a
/// locked hint.
/// </summary>
[System.Serializable]
public class EvidenceNoteFinding
{
    [TextArea(2, 5)]
    [Tooltip("Neutral, confirmed information appended to the evidence note once revealRequirement is satisfied. Do not put scoring metadata, correct relationships, suspects, conclusions or ending requirements here.")]
    public string text;

    [Tooltip("When this finding becomes visible. Use authoritative EvidenceStatus/report-line/swab/fingerprint/lab requirements. An empty ALL group is immediately satisfied once the evidence itself has been discovered.")]
    public RequirementNode revealRequirement = new();
}

[CreateAssetMenu(fileName = "Evidence_", menuName = "VR-CSI/Evidence Definition")]
public class EvidenceDefinition : ScriptableObject
{
    [Tooltip("Unique ID used everywhere in code/database — must match across Unity and Supabase.")]
    public string evidenceId; // e.g. "EVD-014"

    public string displayName; // Authored name; serialized content and stable IDs are retained.

    public string PlayerDisplayName => EvidenceDisplay.Label(evidenceId, displayName);

    [Tooltip("How much this item bears on the correct conclusion. Read by scoring AFTER the run — never enforced during play, since gating on relevance would tell the student which items matter. Defaults to Neutral: mark an item Critical/Distractor deliberately, don't inherit it.")]
    public EvidenceRelevance relevance = EvidenceRelevance.Neutral;

    [Tooltip("Radius, in metres, of this item's interaction sphere. Governs BOTH the proximity at which the player marks it Found AND how close a placed evidence tent must land for the tent to count as marking this item. Deliberately one number, not two: they answer the same question — how big is this thing's presence in the scene — and letting them drift apart would mean an item you can notice but cannot successfully tent. Tune per item: a body or a spatter pattern occupies more scene than a knife.")]
    [Min(0.05f)]
    public float interactionRadius = 1.5f;

    [Tooltip("Does this item need fingerprint processing as part of its correct procedure? When true, an extra fingerprinting step is required somewhere in the chain (see EvidenceStateManager.MarkFingerprinted) - WHERE depends on fingerprintBeforeCollection below.")]
    public bool requiresFingerprinting;

    [Tooltip("When true (currently only the gloves, EVD-019), fingerprinting happens ON SCENE, before the item is ever logged - required while status is exactly PhotographedFinal, and becomes an extra precondition on PhotographedFinal → Logged instead of the default Sealed → Processed (see EvidenceStateManager.MarkFingerprinted/IsBlockedByOnSceneFingerprinting). Only meaningful when requiresFingerprinting is also true. Default false: every other item's fingerprinting happens later, read from the sealed container back at the lab - that's the right default for most items, but a surface only reachable before bagging (like the underside of a flipped item) calls for confirming its print before the item is ever logged, let alone removed from the scene. The knife (EVD-014) used to use this shape too, before its own separate print was removed and consolidated onto EVD-016 - see EvidenceStateManager.PlaceholderMatchTargets' own comment.")]
    public bool fingerprintBeforeCollection;

    [Tooltip("When true (currently only the gloves, EVD-019), on-scene fingerprinting ALSO requires the item to have been physically flipped first (see EvidenceStateManager.MarkFlipped/EvidenceFlipTool) - a surface only reachable once flipped can't be printed before it's turned over. Only meaningful when fingerprintBeforeCollection is also true. When requiresSwabbing is ALSO true, flipping is further locked until swabbingDone (see MarkFlipped's own comment) - real procedure swabs the outside trace before ever turning the item over, so a strict swab-then-flip order is enforced rather than either order.")]
    public bool requiresFlipBeforeFingerprinting;

    [Tooltip("Who this item's lifted print identifies once compared (e.g. \"Victim's brother\"). Empty = the comparison finds no identification: the print stays SentToLab and never reaches Matched. Read by EvidenceStateManager.MarkMatched for both the on-scene kit (FingerprintKitTool) and the lab station (FingerprintStationTool). Still a placeholder for a real suspect database - but authored per item here rather than hardcoded in code.")]
    public string fingerprintMatchIdentity;
    [Tooltip("Distinct recovered-print identities. Empty retains the legacy single match above.")]
    public List<string> fingerprintMatchIdentities = new();
    [Tooltip("Require final documentation photography before fixed-reference and evidence-distance measurements.")]
    public bool measurementAfterFinalPhoto;

    [Header("Player-Facing Evidence Notes")]
    [TextArea(2, 5)]
    [Tooltip("Short plain-language description shown after discovery. Keep this observational and neutral; author-only placement/scoring notes do not belong here.")]
    public string description;

    [TextArea(1, 3)]
    [Tooltip("Where the item was found, shown after discovery.")]
    public string foundLocation;

    [TextArea(2, 5)]
    [Tooltip("Condition visible on initial observation, shown after discovery. Do not include results that require a later procedure.")]
    public string observedCondition;

    [TextArea(2, 5)]
    [Tooltip("Short neutral explanation of how this type of evidence may inform an investigation. It must support deduction without stating the correct relationship, suspect or conclusion.")]
    public string investigativeContext;

    [TextArea(3, 12)]
    [Tooltip("Optional, longer recovered content (e.g. a message thread, a document excerpt) shown ONLY inside the Recorder/report panel for this item - see RecorderReportUI.Show. Distinct from description/observedCondition/investigativeContext above (all shown at discovery, all short) and from progressiveFindings (revealed AFTER a report line is selected, as a confirmation): this is shown BEFORE the player picks a report line, so they understand why a given candidate line exists. Leave blank for every item that doesn't need this - RecorderReportUI's context block stays hidden entirely, same layout as before this field existed.")]
    public string loggingContext;

    [Tooltip("Confirmed findings revealed progressively through RequirementNode gates. The EvidenceStateManager and related report/procedure systems remain authoritative.")]
    public List<EvidenceNoteFinding> progressiveFindings = new();

    [Tooltip("Candidate lines the player can drag into the report notes when logging this item - a mix of accurate observations and plausible-but-wrong filler, same false-lead principle as relevance/reasoningOptions. Leave empty for items that don't use the drag-and-drop report panel yet; RecorderTool falls back to the plain instant log for those.")]
    public List<ReportLineOption> reportLineOptions = new();

    [Tooltip("How many lines must be in the Notes zone before Confirm is available. Configurable per item, not hardcoded - default 1.")]
    [Min(1)]
    public int minimumLinesRequired = 1;

    [Tooltip("Radius, in metres, of the chalk-tracing guide circle drawn around this item once it reaches PhotographedRaw. MUST be authored per item, not left at the default - scale varies hugely across the roster (a phone needs something far smaller than a body or a spatter pattern). See EvidenceChalkGuide.")]
    [Min(0.01f)]
    public float chalkCircleRadius = 0.15f;

    [Tooltip("Whether this item's procedure includes a chalk outline. When false the lifecycle goes PhotographedRaw -> Marked directly (ChalkMarked is bypassed, never faked), no chalk guide spawns, and the chalk tool only answers that chalk isn't required. For fixed stains/patterns and items whose position is documented by photographs, marker and measurement. Provisional per item - keep it data, never an evidenceId check.")]
    public bool requiresChalk = true;

    [Tooltip("How many guide points make up the tracing circle.")]
    [Min(4)]
    public int chalkPointCount = 24;

    [Tooltip("Fraction of guide points (0-1) that must be covered before chalking completes. Deliberately not 1.0 by default - a hard 100% means one point sitting just outside tolerance blocks completion forever, which reads as unfair rather than as a skill check.")]
    [Range(0f, 1f)]
    public float chalkCompletionThreshold = 0.9f;

    [Tooltip("Does this item have a biological trace (e.g. blood) on it that must be swabbed before it can be logged? When true, PhotographedFinal → Logged additionally requires the swab step (see EvidenceStateManager.MarkSwabbed and EvidenceSwabRequirement). Mirrors requiresFingerprinting's own pattern, but gates Logged instead of Processed - the physical sample has to be taken before the item itself is ever logged (and, in turn, bagged and removed from the scene), not after, so that logging's report-line selection can reflect the swab result. Use this instead of making the trace a separate independent EvidenceDefinition: a trace that is physically co-located with its parent item (blood on a knife blade) is indistinguishable from the parent to PhotographTool's camera-distance targeting once both are separately registered EvidenceProps in the same shot - confirmed as a real, reproducible failure, not a hypothetical.")]
    public bool requiresSwabbing;

    [Tooltip("The item carries a separately visible trace (the blood on the knife blade) that gets its OWN numbered evidence tent beside the item's tent - one record, two tents. The second tent goes on the trace's sub-target (EvidenceTraceMarkerTarget) once the item itself is Marked, and is required before the item's final photograph so both tents are in frame (EvidenceStateManager.MarkTraceTented). Not a separate EvidenceDefinition, for the same co-location reason as requiresSwabbing.")]
    public bool requiresTraceMarker;

    [Tooltip("Is this item's OWN collection method a swab into SwabTool's container, rather than a physical grab-and-bag through EvidenceBagTool? True for an item with no physical presence to pick up (e.g. EVD-015, blood spatter on the floor - see SwabTool's class comment). When requiresSwabbing is ALSO set (EVD-015 now), the sample is taken on scene at the final photo, contained and sealed BEFORE the record is written, and that sealed sample IS this item's collection: after Logged it advances Collected -> Sealed automatically and enters the inventory - never a second swab (see EvidenceStateManager.CheckReadyForCollection). Read by EvidenceNotifier to decide whether the ReadyForCollection toast should say \"ready to swab\" instead of \"ready for collection\".")]
    public bool collectedBySwab;

    [Tooltip("Biological sampling must be finished before fingerprint processing starts: powder/brush/lift is refused until this item's required swab (requiresSwabbing) is done, so powder can't contaminate the blood trace. Enforced in EvidenceStateManager.MarkFingerprinted for every fingerprint path, and early in FingerprintKitTool.TryDust so no powder is applied at all.")]
    public bool swabBeforeFingerprinting;

    [Tooltip("This item is examined and logged WHERE IT LIES and never bagged - its real procedural chain ends at Logged (Found -> PhotographedRaw -> ChalkMarked -> Marked -> PhotographedFinal -> Logged), with no ReadyForCollection/Collected/Sealed/(Fingerprinted)/Processed beyond that. The body is the first item to need this. Two places specifically honor it: EvidenceStateManager.CheckReadyForCollection (skips its normal Logged -> ReadyForCollection auto-advance for this item, since RequiredSequence itself is one shared array with no per-item terminal point) and CountAtOrAbove (excludes it, since every current caller - HypothesisCheckpointManager's threshold triggers, GreyboxFlowTest's diagnostic totals - assumes its count reflects the numbered roster, not this item). Also marked with a LETTER tent (see EvidenceTentTool) instead of a number, so its physical marker is visibly distinct from the numbered roster's.")]
    public bool examinedInPlace;

    [Tooltip("Body-only procedural removal after documentation; never ordinary grab-and-bag collection.")]
    public bool requiresBodyRemoval;

    [Header("Evidence Inventory / Held Representation")]
    [Tooltip("Shown in the evidence inventory panel's entry list. Missing icon fails safe (entry still functions, just shows a blank/placeholder swatch) rather than breaking the panel - see EvidenceInventoryUI.")]
    public Sprite icon;

    [Tooltip("Spawned/activated at the hand anchor when this sealed item is equipped from inventory (see EvidenceInventory.EquipHeld). MUST show the item inside its bag/container, never the raw crime-scene model - e.g. EvidenceBagTool's own sealedBagVisuals entry for this id, or a dedicated held-only prefab. A missing prefab is a real authoring gap: EquipHeld logs an Inspector warning and refuses to equip (fails safe) rather than showing nothing or falling back to the raw scene model.")]
    public GameObject heldPrefab;

    [Tooltip("Local position applied to heldPrefab once spawned under the hand anchor.")]
    public Vector3 heldLocalPosition;

    [Tooltip("Local Euler angles applied to heldPrefab once spawned under the hand anchor.")]
    public Vector3 heldLocalEulerAngles;

    [Tooltip("Local scale applied to heldPrefab once spawned under the hand anchor. Defaults to 1,1,1 - override per item since heldPrefab's own authored scale may not match the hand-held read Vector3.one implies.")]
    public Vector3 heldLocalScale = Vector3.one;

    [Tooltip("Additional, per-item eligibility check evaluated by EvidenceBoxDepositZone before accepting this item, on top of the zone's own built-in core checks (held, sealed, in inventory, not already deposited, category is Evidence). Defaults to a vacuous ALL group (always true) - the same default every other optional RequirementNode field in this project uses. Use this for e.g. \"Require Processed\" on a box that should only accept fully lab-processed items.")]
    public RequirementNode depositRequirement = new();

    [Tooltip("Optional. If a deposit zone's depositVisualMode is SpawnSlotRepresentation, this lightweight prefab is what actually appears in the box's visual slot - deliberately separate from heldPrefab, since a box slot's needs (small, static, no physics) can differ from a hand-held item's. Leave blank to use the zone's own generic fallback, or when the zone's mode doesn't spawn anything at all (LogicalOnly/ActivatePredefinedSlot).")]
    public GameObject depositedVisual;

    [Header("Procedure Step Requirements")]
    [Tooltip("Optional extra procedure rules on this item's lifecycle, evaluated by EvidenceStateManager on every transition: before the item may reach beforeStatus, requirement must hold. Block refuses the transition (same as any other gate); Warn lets it through and logs ProcedureOutOfOrder once. Empty for every item today - the existing swab/fingerprint/collection gates are NOT expressed here.")]
    public List<ProcedureStepRequirement> stepRequirements = new();

#if UNITY_EDITOR
    /// <summary>Author-time nudge, mirrors EvidenceStateManager.OnValidate's own warn-don't-block idiom. examinedInPlace items never reach Sealed/inventory, so they're exempt.</summary>
    private void OnValidate()
    {
        if (!examinedInPlace && heldPrefab == null)
        {
            Debug.LogWarning($"[EvidenceDefinition] '{name}' ({evidenceId}) has no heldPrefab assigned. This item can reach Sealed and be admitted to inventory, but cannot be equipped from it until a held representation (a clearly-named placeholder is fine for now) is assigned - EvidenceInventory.EquipHeld will refuse and log a warning at runtime rather than ever show the raw scene model.", this);
        }

        if (icon == null)
        {
            Debug.LogWarning($"[EvidenceDefinition] '{name}' ({evidenceId}) has no icon assigned - its evidence inventory entry will show a blank swatch.", this);
        }
    }
#endif
}
