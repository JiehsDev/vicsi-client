// Assets/_Project/Scripts/Testing/GreyboxFlowTest.cs
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Re-runnable end-to-end check of the evidence lifecycle, Scene Entry through
/// Hypothesis Checkpoint. Exists because the previous version of this walkthrough was
/// ad-hoc code typed into the editor once and then lost - which meant that when the
/// Marked step was inserted and the old flow stopped being valid, there was no script
/// to fail, only a memory of one. A test that isn't checked in can't regress; it can
/// only be forgotten.
///
/// This drives the STATE MACHINE directly. It deliberately does not simulate VR input,
/// so it proves the procedural sequence, the gates and the logging - not that a
/// controller trigger is bound correctly. A headset pass is still required for that,
/// and this script is not a substitute for one.
///
/// Run it from the editor via:
///     FindFirstObjectByType&lt;GreyboxFlowTest&gt;().RunFullFlow()
/// or tick runOnStart to have it run automatically on entering Play mode.
/// </summary>
public class GreyboxFlowTest : MonoBehaviour
{
    [Tooltip("Run the full flow automatically when entering Play mode. Leave off for normal play sessions — this drives evidence state directly and will complete the scenario on its own.")]
    [SerializeField] private bool runOnStart;

    [Tooltip("Evidence items to walk through the full lifecycle, in order.")]
    [SerializeField]
    private string[] evidenceIds = { "EVD-014", "EVD-015", "EVD-018" };
    private int tentNumberOffset;
    private bool skipConclusionSubmit;
    private static bool resumeFromCurrentStatus;

    /// <summary>
    /// When set, used instead of AnswerForTest()'s index-0 default to answer any
    /// checkpoint that opens mid-flow - lets a caller (BrotherPathTest) answer with a
    /// specific option. Null = the unchanged default.
    /// </summary>
    public static Action<HypothesisCheckpointUI> CheckpointAnswererForTest;

    /// <summary>
    /// Optional: runs right before an item is stepped to Logged (evidenceId). Logging waits
    /// for the discovered evidence's measurements (fixed references + every pair among
    /// discovered items). Default (null): MeasurementTestUtil.CompleteDiscoveredMeasurements -
    /// what a student must do before logging; nothing undiscovered is involved.
    /// </summary>
    public static Action<string> BeforeLoggedForTest;

    /// <summary>
    /// Restricts the next RunFullFlow() to a subset of items, for tests that need to
    /// observe state between items (e.g. BrotherPathTest's fingerprint-only stage).
    /// tentNumberOffset keeps tent numbers unique across successive partial runs.
    /// </summary>
    public void SetEvidenceIdsForTest(string[] ids, int tentOffset = 0, bool skipSubmit = false, bool resume = false)
    {
        evidenceIds = ids;
        tentNumberOffset = tentOffset;
        skipConclusionSubmit = skipSubmit;
        resumeFromCurrentStatus = resume;
    }

    [Header("Debug visualisation")]
    [Tooltip("Draw a yellow ring on the ground at each evidence item's interactionRadius — the distance at which it is Found and within which a tent counts as marking it. MUST stay off for any real play session: showing a student where the evidence is destroys the free-roam search this scenario exists to measure. Compiled out entirely of release builds.")]
    [SerializeField] private bool showEvidenceRadii;

    private void Start()
    {
        if (runOnStart)
        {
            Debug.Log(RunFullFlow());
        }
    }

    // ---------------------------------------------------------------------
    // Evidence radius rings.
    //
    // This lives on the greybox test component rather than in its own script so
    // there is exactly ONE switch for debug visibility, on the one component that
    // already means "this scene is being tested, not played". A scene with no
    // GreyboxFlowTest in it has no code path that can draw a ring at all.
    //
    // The #if is the actual guarantee, not the default-false field: a scene saved
    // with the box accidentally ticked still ships nothing, because in a release
    // build none of this compiles. Default-false only protects the editor.
    // ---------------------------------------------------------------------
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private const int RingSegments = 48;
    private static readonly Color RingColor = new Color(1f, 0.92f, 0.2f, 0.5f);

    private readonly List<GameObject> radiusRings = new();
    private bool ringsVisible;

    private void Update()
    {
        // Rebuilt on a count change as well as on the toggle, so rings appear for
        // props that registered after the toggle was flipped.
        bool stale = ringsVisible && radiusRings.Count != EvidenceProp.All.Count;

        if (showEvidenceRadii == ringsVisible && !stale)
        {
            return;
        }

        ClearRings();
        ringsVisible = showEvidenceRadii;

        if (ringsVisible)
        {
            BuildRings();
        }
    }

    private void OnDisable()
    {
        ClearRings();
        ringsVisible = false;
    }

    private void BuildRings()
    {
        foreach (var prop in EvidenceProp.All)
        {
            if (prop == null)
            {
                continue;
            }

            var go = new GameObject($"RadiusRing_{prop.evidenceId}", typeof(LineRenderer));
            go.transform.SetParent(transform, false);

            var line = go.GetComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = true;
            line.positionCount = RingSegments;
            line.widthMultiplier = 0.02f;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = RingColor;
            line.endColor = RingColor;

            float radius = prop.InteractionRadius;
            Vector3 centre = GroundUnder(prop.transform.position);

            for (int i = 0; i < RingSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / RingSegments;
                line.SetPosition(i, centre + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }

            radiusRings.Add(go);
        }

        Debug.Log($"[GreyboxFlowTest] Evidence radius rings ON ({radiusRings.Count} item(s)). Turn this off before any real play session.", this);
    }

    private void ClearRings()
    {
        foreach (var ring in radiusRings)
        {
            if (ring != null)
            {
                Destroy(ring);
            }
        }
        radiusRings.Clear();
    }

    // The ring is meant to read as a footprint on the floor, so it drops to whatever
    // surface is under the item rather than floating at the item's own height. Falls
    // back to a hair below the prop if nothing is beneath it (an item on a shelf with
    // open air under the raycast).
    private static Vector3 GroundUnder(Vector3 position)
    {
        Vector3 from = position + Vector3.up * 0.25f;
        if (Physics.Raycast(from, Vector3.down, out var hit, 6f, ~0, QueryTriggerInteraction.Ignore))
        {
            return hit.point + Vector3.up * 0.01f;
        }
        return position - Vector3.up * 0.01f;
    }
#endif

    /// <summary>
    /// Walks every configured item from NotFound to Processed in the canonical order,
    /// asserting at each step. Returns a human-readable report; every line that starts
    /// with FAIL is a real regression.
    /// </summary>
    public string RunFullFlow()
    {
        // Can submit a conclusion (-> queue an upload): isolate real data/servers first.
        AutomatedTestSandbox.Activate(nameof(GreyboxFlowTest));
        var report = new StringBuilder();
        var failures = new List<string>();

        var esm = EvidenceStateManager.Instance;
        var gate = ProceduralGateValidator.Instance;

        if (esm == null || gate == null)
        {
            return "FAIL: EvidenceStateManager or ProceduralGateValidator missing from scene.";
        }

        ReleasePreSessionGateIfPresent(report);

        report.AppendLine("=== Greybox flow test ===");

        // Evidence work is gated on the overall scene photography (stepRequirements:
        // ScenePhotographyCompleted), so the route comes first - same as a real player.
        if (ScenePhotographyManager.Instance != null && !ScenePhotographyManager.Instance.IsScenePhotographyComplete)
        {
            if (ScenePhotographyTestUtil.CompleteRoute())
            {
                report.AppendLine("--- scene photography route completed first");
            }
            else
            {
                Fail(failures, report, "could not complete the scene photography route before evidence work.");
            }
        }

        int tentCounter = tentNumberOffset;
        foreach (var id in evidenceIds)
        {
            tentCounter++;
            var record = esm.GetRecord(id);
            if (record == null)
            {
                Fail(failures, report, id + ": not registered in EvidenceStateManager.");
                continue;
            }

            if (!GloveChangeTestUtil.EnsureClean()) Fail(failures, report, id + ": required glove/supply visit before continuing failed: " + GloveChangeTestUtil.LastFailure);
            var prop = EvidenceProp.FindById(id);

            report.AppendLine("--- " + id + " (" + record.definition.displayName
                + ", relevance=" + record.definition.relevance
                + ", requiresFingerprinting=" + record.definition.requiresFingerprinting + ")");

            Step(esm, gate, failures, report, id, EvidenceStatus.Found);

            // PLACEHOLDER PASS-THROUGH pending the deferred full rewrite (see class
            // comment / task report) - this project's restructure of the
            // documentation flow (Found -> PhotographedRaw -> ChalkMarked -> Marked ->
            // PhotographedFinal -> Logged) retired Sketched/MasterSketchManager
            // outright and split Photographed into two statuses. This walks the new
            // order with plain direct EvidenceStateManager calls only - no gate-order
            // assertions (the old "refused until Marked" checks asserted the OLD
            // order and are now simply wrong, not just broken, so they were removed
            // rather than adapted), no per-item tool routing beyond ChalkMarked (which
            // does route through the real ChalkMarkTool below), and no annotation-
            // equivalent verification (nothing replaces the old master-sketch check -
            // there is no equivalent artifact for PhotographedFinal yet). Sufficient
            // to prove the sequence gate accepts the new order and nothing more.
            Step(esm, gate, failures, report, id, EvidenceStatus.PhotographedRaw);
            if (record.definition.requiresChalk)
            {
                Step(esm, gate, failures, report, id, EvidenceStatus.ChalkMarked);
            }
            else
            {
                report.AppendLine("    (no chalk step for this item - requiresChalk is off)");
            }
            Step(esm, gate, failures, report, id, EvidenceStatus.Marked, tentCounter);
            Step(esm, gate, failures, report, id, EvidenceStatus.PhotographedFinal);

            // This item's powdering gate requires measurements before the print sub-step.
            bool preparedBeforeFingerprint = true; // All items now need measurement and sketch before processing.
            if (preparedBeforeFingerprint)
            {
                if (BeforeLoggedForTest != null) BeforeLoggedForTest(id);
                else MeasurementTestUtil.CompleteDiscoveredMeasurements();
                DismissAnyOpenHypothesisCheckpoint(report);
            }

            if (!MeasurementTestUtil.CompleteSketchFor(id)) failures.Add(id + ": measurement/sketch fixture failed.");

            // Sub-steps that gate Logged now live HERE, between PhotographedFinal and
            // Logged, rather than between ReadyForCollection and Collected - report-line
            // selection (RecorderTool.TryOpenReport/HandleReportConfirmed, both gated on
            // CanTransition(id, Logged)) should reflect everything actually discovered,
            // including a swab result or an on-scene print's lab match, and logging
            // selections feed conclusion availability downstream - see
            // EvidenceStateManager.IsBlockedBySwabbing/IsBlockedByOnSceneFingerprinting.

            // An item that requires BOTH a swab and a flip-before-fingerprinting
            // (currently only the gloves, EVD-019) must not be flippable until its
            // swab is done - real procedure: swab the outside trace before ever
            // turning the item over, so the flip can't disturb it. Proven BEFORE the
            // swab block below actually swabs it, or this would have nothing left to
            // prove. See EvidenceStateManager.MarkFlipped's own swabbingDone check.
            if (record.definition.requiresFlipBeforeFingerprinting && record.definition.requiresSwabbing)
            {
                if (EvidenceFlipTool.Instance == null)
                {
                    Fail(failures, report, id + ": no EvidenceFlipTool in scene - can't exercise the real flip-lock path.");
                }
                else
                {
                    var earlyFlip = EvidenceFlipTool.Instance.TryFlip(id);
                    if (earlyFlip == TransitionResult.Applied)
                    {
                        Fail(failures, report, id + ": flip succeeded before the required blood swab - flip is not locked behind swabbing.");
                    }
                    else
                    {
                        report.AppendLine("    flip correctly refused before the required blood swab");
                    }
                }
            }

            // Items with a required biological trace (the knife, with blood on its
            // blade - see EvidenceDefinition.requiresSwabbing) must have that trace
            // swabbed before Logged will apply at all. Routes through the same
            // sample-then-contain gesture as a real EvidenceProp swab - TrySampleRequiredSwab
            // against the physical EvidenceSwabRequirement marker on the blade, then
            // TryContain to deposit it - the exact calls a real touch-then-carry-to-container
            // action makes, rather than calling MarkSwabbed directly. Also asserts Logged
            // is refused BEFORE swabbing, mirroring the existing fingerprint-gate-order
            // checks just below.
            if (record.definition.requiresSwabbing && record.swabbingDone && (!record.definition.collectedBySwab || record.swabSampleSealed))
            {
                // Resume mode: the caller already took (and, where needed, sealed) the sample with the real swab tool.
                report.AppendLine("    (required blood sample already taken)");
            }
            else if (record.definition.requiresSwabbing)
            {
                if (gate.CanTransition(id, EvidenceStatus.Logged))
                {
                    Fail(failures, report, id + ": Logged was allowed before the required blood swab - requiresSwabbing is not being enforced.");
                }
                else
                {
                    report.AppendLine("    gate correctly refuses Logged: " + gate.GetBlockReason(id, EvidenceStatus.Logged));
                }

                var swabRequirement = EvidenceSwabRequirement.FindByEvidenceId(id);
                if (swabRequirement == null)
                {
                    Fail(failures, report, id + ": requiresSwabbing is true but no EvidenceSwabRequirement marker was found for it.");
                }
                else if (SwabTool.Instance == null)
                {
                    Fail(failures, report, id + ": no SwabTool in scene - can't exercise the real required-swab path.");
                }
                else if (!GloveChangeTestUtil.EnsureClean() || !SwabTool.Instance.TrySampleRequiredSwab(swabRequirement))
                {
                    Fail(failures, report, id + ": TrySampleRequiredSwab was refused even though the item is PhotographedFinal.");
                }
                else
                {
                    var swabResult = SwabTool.Instance.TryContain();
                    if (swabResult != TransitionResult.Applied)
                    {
                        Fail(failures, report, id + ": TryContain (required-swab sample) returned " + swabResult + " instead of Applied.");
                    }
                    else
                    {
                        if(!SamplePreparationTestUtil.PrepareCurrent())
                            Fail(failures,report,id+": sample preparation failed.");
                        SwabTool.Instance.TrySeal();
                        if (!HeldItemCoordinator.Instance.TryStoreHeldEvidence())
                            Fail(failures, report, id + ": required blood sample could not be stored.");
                        report.AppendLine("    swabbed required blood trace (SwabTool.TrySampleRequiredSwab -> TryContain -> TrySeal)");
                    }
                }
            }

            if (!GloveChangeTestUtil.EnsureClean()) Fail(failures, report, id + ": replace used gloves after sample storage before recording or fingerprints: " + GloveChangeTestUtil.LastFailure);

            // Fingerprinting order depends on definition.fingerprintBeforeCollection -
            // both on-scene items today (the knife, EVD-014; the gloves, EVD-019) now
            // use the exact same real tool pair (FingerprintKitTool + FingerprintTapeTool,
            // generalized off a real EvidenceFingerprintMarker rather than hardcoded to
            // the knife alone - see FingerprintKitTool's own class comment). On-scene
            // items must be fingerprinted BEFORE Logged, since the print has to come
            // off the item before its report is ever written, let alone before it's
            // bagged; every other fingerprinted item is unaffected and keeps the
            // original after-Sealed order below (FingerprintStationTool, still used
            // there - see RunFingerprintStationSequence).
            if (record.definition.requiresFingerprinting && record.definition.fingerprintBeforeCollection)
            {
                if (gate.CanTransition(id, EvidenceStatus.Logged))
                {
                    Fail(failures, report, id + ": Logged was allowed before on-scene fingerprinting - fingerprintBeforeCollection is not being enforced.");
                }
                else
                {
                    report.AppendLine("    gate correctly refuses Logged: " + gate.GetBlockReason(id, EvidenceStatus.Logged));
                }

                var marker = EvidenceFingerprintMarker.FindByEvidenceId(id);
                if (marker == null)
                {
                    Fail(failures, report, id + ": fingerprintBeforeCollection is true but no EvidenceFingerprintMarker was found for it.");
                }
                else if (FingerprintKitTool.Instance == null || FingerprintTapeTool.Instance == null)
                {
                    Fail(failures, report, id + ": FingerprintKitTool/FingerprintTapeTool missing from scene - can't exercise the real dust/lift path.");
                }
                else
                {
                    var touchZone = marker.transform.Find("TouchZone");
                    var touchCollider = touchZone != null ? touchZone.GetComponent<Collider>() : null;
                    if (touchCollider == null)
                    {
                        Fail(failures, report, id + ": print marker has no TouchZone trigger collider - can't exercise the real brush/tape contact path.");
                    }
                    else
                    {
                        var revealable = marker.GetComponent<UVRevealable>();
                        if (revealable == null)
                        {
                            Fail(failures, report, id + ": print marker has no UVRevealable component.");
                        }
                        else
                        {
                            // The real call FlashlightTool's beam makes when it lands on
                            // the print - proves the reveal mechanism itself, independent
                            // of dust/lift below.
                            revealable.TriggerReveal();
                            report.AppendLine("    -> print revealed under UV (UVRevealable.TriggerReveal)");
                        }

                        // Items whose print is only reachable once turned over (the
                        // gloves, EVD-019 - see EvidenceDefinition.requiresFlipBeforeFingerprinting)
                        // must be flipped BEFORE dusting will succeed at all. Proves the
                        // failure path first (dust refused pre-flip), same discipline as
                        // RunFingerprintStationSequence's own zero-dust-lift check, then
                        // drives the real EvidenceFlipTool.TryFlip - the exact call a real
                        // flip gesture makes - rather than calling MarkFlipped directly.
                        if (record.definition.requiresFlipBeforeFingerprinting)
                        {
                            if (EvidenceFlipTool.Instance == null)
                            {
                                Fail(failures, report, id + ": no EvidenceFlipTool in scene - can't exercise the real flip path.");
                            }
                            else
                            {
                                // Loaded here, ahead of RunFingerprintKitSubStepSequence's
                                // own load step, so this refusal is unambiguously about the
                                // flip precondition rather than a leftover "brush has no
                                // dust" refusal - dust persists once loaded (see
                                // FingerprintKitTool's own class comment), so loading it
                                // early is harmless.
                                FingerprintKitTool.Instance.TryLoadDust();

                                FingerprintKitTool.Instance.HandleBrushTriggerEnter(touchCollider);
                                if (marker.IsDusted)
                                {
                                    Fail(failures, report, id + ": brush-print contact dusted the print before flipping - requiresFlipBeforeFingerprinting is not being enforced.");
                                }
                                else
                                {
                                    report.AppendLine("    dust correctly refused before flipping");

                                    var flipResult = EvidenceFlipTool.Instance.TryFlip(id);
                                    if (flipResult != TransitionResult.Applied)
                                    {
                                        Fail(failures, report, id + ": TryFlip returned " + flipResult + " instead of Applied.");
                                    }
                                    else
                                    {
                                        report.AppendLine("    -> flipped (EvidenceFlipTool.TryFlip)");
                                    }
                                }
                            }
                        }

                        // Both on-scene items now share this one sequence - see the
                        // class comment above on why the id-branch that used to live
                        // here (knife -> FingerprintKitTool, gloves -> FingerprintStationTool)
                        // is gone.
                        RunFingerprintKitSubStepSequence(failures, report, id, record, marker, touchCollider);
                    }
                }

                if (!record.fingerprintingDone)
                {
                    Fail(failures, report, id + ": on-scene fingerprinting did not complete - can't proceed to Logged.");
                }
                else
                {
                    report.AppendLine("    -> fingerprinted on scene, before logging");
                }
            }

            if (!preparedBeforeFingerprint)
            {
                if (BeforeLoggedForTest != null) BeforeLoggedForTest(id);
                else MeasurementTestUtil.CompleteDiscoveredMeasurements();
            }
            DismissAnyOpenHypothesisCheckpoint(report);
            Step(esm, gate, failures, report, id, EvidenceStatus.Logged);

            // MarkLogged auto-advances to ReadyForCollection, so this is already there -
            // or, for an item whose sealed on-scene swab sample is its collection, further
            // (see EvidenceStateManager.CheckReadyForCollection).
            bool preSampled = record.definition.collectedBySwab && record.definition.requiresSwabbing;
            if (preSampled)
            {
                report.AppendLine("    sealed on-scene sample carried custody on: " + record.status
                    + (EvidenceInventory.Instance != null && EvidenceInventory.Instance.IsInInventory(id) ? ", in inventory" : ", NOT in inventory"));
                if (EvidenceInventory.Instance == null || !EvidenceInventory.Instance.IsInInventory(id))
                {
                    Fail(failures, report, id + ": the sealed swab sample was not admitted to the evidence inventory.");
                }
            }
            else if (record.status != EvidenceStatus.ReadyForCollection)
            {
                Fail(failures, report, id + ": expected ReadyForCollection after Logged, got " + record.status);
            }

            // Blood spatter (EVD-015) is collected by the combined swab tool, not the
            // bag - it has no physical presence to grab (see SwabTool's own class
            // comment). This is the item's own real Collected action (collectedBySwab),
            // not a Logged precondition, so it stays here, after ReadyForCollection -
            // unlike requiresSwabbing above, which is a precondition on a DIFFERENT
            // item's own logging/collection and moved earlier in this reorder. Taking
            // a sample is a tool-local prerequisite fact (SwabTool's internal
            // hasSample flag), not a state-machine transition in its own right -
            // TryContain is what actually advances EvidenceStateManager.
            if (record.definition.collectedBySwab && !record.definition.requiresSwabbing)
            {
                if (prop == null)
                {
                    Fail(failures, report, id + ": no registered EvidenceProp - can't exercise the real swab sampling path.");
                }
                else if (SwabTool.Instance == null)
                {
                    Fail(failures, report, id + ": no SwabTool in scene - can't exercise the real swab sampling path.");
                }
                else if (!GloveChangeTestUtil.EnsureClean() || !SwabTool.Instance.TrySwab(prop))
                {
                    Fail(failures, report, id + ": SwabTool.TrySwab was refused even though the item is ReadyForCollection.");
                }
                else if (SwabTool.Instance.SampledFrom == null || SwabTool.Instance.SampledFrom.evidenceId != id)
                {
                    Fail(failures, report, id + ": SwabTool.SampledFrom is '"
                        + (SwabTool.Instance.SampledFrom != null ? SwabTool.Instance.SampledFrom.evidenceId : "null")
                        + "' after swabbing " + id + " - cross-attribution failure.");
                }
                else
                {
                    report.AppendLine("    swabbed (SwabTool.SampledFrom=" + SwabTool.Instance.SampledFrom.evidenceId + ", HasSample=" + SwabTool.Instance.HasSample + ")");
                }
            }

            Step(esm, gate, failures, report, id, EvidenceStatus.Collected);

            // Chain of custody: Processed must be refused until the item is sealed,
            // for the same reason Photographed is refused until it is Marked. This
            // assertion is the thing that fails if Sealed is ever removed from
            // RequiredSequence, or quietly reordered around the fingerprint step.
            if (gate.CanTransition(id, EvidenceStatus.Processed))
            {
                Fail(failures, report, id + ": Processed was allowed straight from Collected - Sealed is not being required.");
            }
            else
            {
                report.AppendLine("    gate correctly refuses Processed: " + gate.GetBlockReason(id, EvidenceStatus.Processed));
            }

            // Everything below is the DEFAULT (after-Sealed) fingerprinting shape -
            // skipped entirely for an on-scene item like the knife, which already
            // completed fingerprinting above, before Collected.
            if (record.definition.requiresFingerprinting && !record.definition.fingerprintBeforeCollection)
            {
                // Fingerprinting must ALSO be refused before the seal, so the two cannot be
                // satisfied in either order - the path is strictly
                // Collected -> Sealed -> (fingerprint) -> Processed.
                if (esm.MarkFingerprinted(id, ToolType.IOC) != TransitionResult.Violation)
                {
                    Fail(failures, report, id + ": fingerprinting was accepted while only Collected - it must require Sealed first.");
                }
            }

            Step(esm, gate, failures, report, id, EvidenceStatus.Sealed);

            // Completed bags now wait in the hand for explicit B storage before another tool is equipped.
            // Drive the same coordinator handler as B instead of assuming sealing already stored custody.
            if (EvidenceInventory.Instance?.RequiresExplicitBagStorage == true
                && HeldItemCoordinator.Instance?.CurrentEvidenceId == id)
            {
                if (!HeldItemCoordinator.Instance.TryStoreHeldEvidence())
                    Fail(failures, report, id + ": explicit bag inventory storage was refused after sealing.");
                else report.AppendLine("    -> stored sealed bag through the B storage handler");
            }

            if (record.definition.requiresFingerprinting && !record.definition.fingerprintBeforeCollection)
            {
                if (gate.CanTransition(id, EvidenceStatus.Processed))
                {
                    Fail(failures, report, id + ": Processed allowed without fingerprinting despite requiresFingerprinting.");
                }
                else
                {
                    report.AppendLine("    gate correctly refuses Processed: " + gate.GetBlockReason(id, EvidenceStatus.Processed));
                }

                RunFingerprintStationSequence(failures, report, id, record);
            }

            // EvidenceStateManager now auto-advances Sealed -> Processed the moment
            // every precondition is already true (CheckProcessed, called from both
            // MarkSealed and MarkFingerprinted) - the same "ready" shape
            // CheckReadyForCollection already used for Logged -> ReadyForCollection.
            // By this point every item here (whether or not it required
            // fingerprinting) is therefore already Processed: items with no
            // fingerprinting requirement advanced the instant Sealed applied a few
            // lines up; items that did require it advanced inside
            // RunFingerprintStationSequence's real TryCompare call just above. Calling
            // the generic Step(..., Processed) here regardless would ask the gate to
            // approve a transition to the status the item is already AT, which
            // CanTransition correctly refuses (IsValidNextStep requires the target to
            // be exactly the next step, not the current one) - a real, once-silent
            // conflict this fix surfaced, not a pre-existing assumption.
            if (record.status == EvidenceStatus.Processed)
            {
                report.AppendLine("    -> Processed (auto-advanced once Sealed" + (record.definition.requiresFingerprinting ? " and fingerprinted" : "") + ")");

                // This branch bypasses Step() entirely, which is the only place that
                // otherwise calls DismissAnyOpenHypothesisCheckpoint after every
                // transition. Now that Processed can be reached mid-
                // RunFingerprintStationSequence (inside TryCompare, via CheckProcessed)
                // rather than only through a Step(..., Processed) call, a checkpoint -
                // HYP-01 or HYP-02, both still CountThreshold - can fire at exactly
                // this moment with nothing to dismiss it. Confirmed live: running the
                // full item flow left a real, unanswered checkpoint panel open
                // (PlayerUIGate still blocked) after RunFullFlow returned "PASS",
                // because the auto-advance on an item's fingerprint compare never
                // passed through this call.
                // HYP-03 itself no longer fires here at all: its trigger is now
                // SubmissionInitiated, not CountThreshold (see the explicit
                // FireSubmissionInitiatedCheckpoints call after the main loop below) -
                // reaching Processed on every item is no longer what opens it.
                DismissAnyOpenHypothesisCheckpoint(report);
            }
            else
            {
                Step(esm, gate, failures, report, id, EvidenceStatus.Processed);
            }
        }

        // HYP-03's trigger is SubmissionInitiated (fired as a side effect of the real
        // Submit Conclusion action), not a Processed-count threshold, so nothing in the
        // loop above opens it any more. The real Submit Conclusion entry point is now
        // EndingManager.TrySubmitConclusion (wired to UtilityMenuController's "Submit
        // Conclusion" entry) - it calls HypothesisCheckpointManager.
        // FireSubmissionInitiatedCheckpoints internally as step one, then resolves and
        // shows an ending. Calling this same public method is what this test drives
        // directly, exactly like every other real action here (TryDust, TryFlip,
        // TrySampleRequiredSwab, ...) - not a simulated button press, but the real
        // state-machine entry point a button press would reach.
        if (skipConclusionSubmit)
        {
            report.AppendLine("-> Submit Conclusion skipped (caller drives it)");
        }
        else if (EndingManager.Instance == null)
        {
            Fail(failures, report, "No EndingManager in scene - can't exercise Submit Conclusion.");
        }
        else
        {
            MeasurementTestUtil.CompleteSceneSketch();
            STCSNotificationUI.Instance?.HideImmediately(); NotificationManager.Instance?.UI.ClearTooltip();
            EndingManager.Instance.TrySubmitConclusion();
            report.AppendLine("-> Submit Conclusion (EndingManager.TrySubmitConclusion)");
            DismissAnyOpenHypothesisCheckpoint(report);
        }

        report.AppendLine("--- totals ---");
        report.AppendLine("at/above Found:     " + esm.CountAtOrAbove(EvidenceStatus.Found));
        report.AppendLine("at/above Marked:    " + esm.CountAtOrAbove(EvidenceStatus.Marked));
        report.AppendLine("at/above Collected: " + esm.CountAtOrAbove(EvidenceStatus.Collected));
        report.AppendLine("at/above Sealed:    " + esm.CountAtOrAbove(EvidenceStatus.Sealed));
        report.AppendLine("at/above Processed: " + esm.CountAtOrAbove(EvidenceStatus.Processed));

        report.AppendLine(failures.Count == 0
            ? "=== PASS: " + evidenceIds.Length + " items completed the full lifecycle ==="
            : "=== FAILED with " + failures.Count + " problem(s) ===");

        return report.ToString();
    }

    /// <summary>
    /// PreSessionFlowController (Part 1, added to the real _Managers GameObject) locks
    /// PlayerUIGate the instant this scene's Play session starts and only releases it
    /// once a real pairing completes - correct for real gameplay, but this test never
    /// pairs, so without this the lock never lifts and every gated interaction this
    /// flow depends on (RecorderReportUI.Show, in particular) silently refuses to open
    /// for the rest of the run.
    ///
    /// Two steps, not one, to close a real startup race: if this runs before the
    /// controller's own Start() has fired yet (component ordering across different
    /// GameObjects is not guaranteed - confirmed to actually happen, not
    /// hypothetical), releasing only the token here is immediately undone the moment
    /// that deferred Start() finally calls its own LockInvestigation(). Disabling the
    /// component FIRST prevents that: Unity never calls Start() on a component that
    /// is disabled before its Start callback would fire, so if Start() hasn't run
    /// yet, this permanently pre-empts it; if Start() already ran and already locked
    /// the gate, disabling here is a harmless no-op (no OnDisable override exists)
    /// and the explicit token release below handles that case instead. Together they
    /// cover both orderings without depending on which one actually happened.
    ///
    /// Released via the exact same token/reflection discipline PreSessionFlowControllerTest
    /// already uses to inspect this gate, rather than reaching into PlayerUIGate's
    /// blocker set directly (which would affect every blocker, not just this one
    /// controller's) or editing PreSessionFlowController itself, which stays exactly
    /// as authored for Part 1. No-op (and silently so) if no PreSessionFlowController
    /// exists in the scene.
    /// </summary>
    private static void ReleasePreSessionGateIfPresent(StringBuilder report)
    {
        // The investigation opens at the First Responder's turnover - complete it (test bypass).
        if (SceneTurnover.IsPending)
        {
            SceneTurnover.Controller.CompleteWithoutDialogue();
            report.AppendLine("(test setup: completed the First Responder scene turnover - this run skips the conversation)");
        }

        var controller = FindFirstObjectByType<PreSessionFlowController>();
        if (controller == null)
        {
            return;
        }

        controller.enabled = false;

        var tokenField = typeof(PreSessionFlowController).GetField("uiGateToken",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var sessionUnlockedField = typeof(PreSessionFlowController).GetField("sessionUnlocked",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var token = tokenField?.GetValue(controller);
        if (token != null)
        {
            PlayerUIGate.Exit(token);
        }
        sessionUnlockedField?.SetValue(controller, true);
        report.AppendLine("(test setup: disabled and released the real scene's PreSessionFlowController gate - this run never pairs)");
    }

    private static void Step(EvidenceStateManager esm, ProceduralGateValidator gate,
        List<string> failures, StringBuilder report, string id, EvidenceStatus target, int tentNumber = 0)
    {
        // The knife's second tent (blood on the blade) goes down before its final photo -
        // before the gate check below, which refuses PhotographedFinal without it.
        if (target == EvidenceStatus.PhotographedFinal)
        {
            TraceMarkerTestUtil.MarkTraceIfRequired(id);
        }

        // An item whose sealed on-scene swab sample is its collection reaches the custody
        // statuses from that sample right after Logged - nothing left to do for them.
        var stepDefinition = esm.GetRecord(id)?.definition;
        if (stepDefinition != null && stepDefinition.collectedBySwab && stepDefinition.requiresSwabbing
            && target >= EvidenceStatus.Collected && esm.IsAtOrPastStatus(id, target))
        {
            report.AppendLine("    (" + target + " already provided by the sealed on-scene sample)");
            return;
        }

        // Resume mode (MeasurementProcedureTest): steps the caller already performed with
        // the real tools are skipped rather than failed as a Duplicate.
        if (resumeFromCurrentStatus && esm.IsAtOrPastStatus(id, target))
        {
            report.AppendLine("    (already " + target + ")");
            return;
        }

        if (!gate.CanTransition(id, target))
        {
            Fail(failures, report, id + ": gate refused " + target + " - " + gate.GetBlockReason(id, target));
            return;
        }

        TransitionResult result;
        switch (target)
        {
            case EvidenceStatus.Found:
                // No roster item routes Found through UV reveal anymore - the knife's
                // print sub-step (formerly EVD-016, its own roster item with its own
                // Found) has no status of its own at all now; see
                // RunKnifePrintSubStepSequence for where its real UVRevealable.TriggerReveal
                // call is actually exercised instead.
                result = esm.MarkFound(id, ToolType.None);
                break;
            // PLACEHOLDER, direct calls only - see the RunFullFlow call site's comment.
            case EvidenceStatus.PhotographedRaw: result = esm.MarkPhotographedRaw(id, ToolType.Photographer); break;
            case EvidenceStatus.ChalkMarked:
                // Routes through the real continuous-tracing mechanism instead of
                // calling MarkChalkMarked directly: MarkPhotographedRaw just above
                // fired OnEvidenceStatusChanged, which is what spawns this item's
                // EvidenceChalkGuide (see that class's static PhotographedRaw
                // listener). Walking the guide's own real point positions through
                // ChalkMarkTool.TryTraceAt - the exact public method a real per-frame
                // trace would call - exercises the real distance-check coverage
                // logic and the real completion-threshold check, not a shortcut.
                if (ChalkMarkTool.Instance == null)
                {
                    Fail(failures, report, id + ": no ChalkMarkTool in scene - can't exercise the real tracing path.");
                    return;
                }

                var chalkGuide = EvidenceChalkGuide.FindActive(id);
                if (chalkGuide == null)
                {
                    Fail(failures, report, id + ": no active EvidenceChalkGuide - PhotographedRaw should have spawned one.");
                    return;
                }

                int totalPoints = chalkGuide.PointCount;
                for (int p = 0; p < totalPoints && !chalkGuide.IsComplete; p++)
                {
                    ChalkMarkTool.Instance.TryTraceAt(chalkGuide.GetPointPosition(p));
                }

                report.AppendLine("    traced " + chalkGuide.CoveredCount + "/" + totalPoints + " guide points");
                result = chalkGuide.IsComplete ? TransitionResult.Applied : TransitionResult.Violation;
                break;
            case EvidenceStatus.Marked: result = esm.MarkTented(id, ToolType.EvidenceMarker, tentNumber); break;
            case EvidenceStatus.PhotographedFinal: result = esm.MarkPhotographedFinal(id, ToolType.Photographer); break;
            case EvidenceStatus.Logged:
                // Routes through RecorderTool.TryOpenReport - the exact call a real
                // aim-and-trigger makes - rather than calling MarkLogged directly.
                // For an item with authored reportLineOptions (EVD-014 as of this
                // task), that opens RecorderReportUI for real; this then selects
                // enough lines via RecorderReportUI.SelectLineForTest (the documented
                // test-only equivalent of a successful drag-and-drop onto the Notes
                // zone - see its own comment for why a synthetic drag isn't needed)
                // and calls the real Confirm(). For an item with no authored lines
                // yet, TryOpenReport already applied MarkLogged directly via its own
                // documented fallback, and there is no panel to drive here.
                if (RecorderTool.Instance == null)
                {
                    Fail(failures, report, id + ": no RecorderTool in scene - can't exercise the real report-writing path.");
                    return;
                }
                var evidenceForLog = EvidenceProp.FindById(id);
                if (evidenceForLog == null)
                {
                    Fail(failures, report, id + ": no registered EvidenceProp - can't exercise the real report-writing path.");
                    return;
                }
                if (!RecorderTool.Instance.TryOpenReport(evidenceForLog))
                {
                    Fail(failures, report, id + ": RecorderTool.TryOpenReport was refused even though the item is PhotographedFinal.");
                    return;
                }

                if (RecorderReportUI.Instance != null && RecorderReportUI.Instance.IsShowing)
                {
                    var reportRecord = esm.GetRecord(id);
                    int needed = System.Math.Max(1, reportRecord.definition.minimumLinesRequired);
                    int selected = 0;
                    foreach (var option in RecorderDocumentation.Options(id)??reportRecord.definition.reportLineOptions)
                    {
                        if (selected >= needed)
                        {
                            break;
                        }
                        if (RecorderReportUI.Instance.SelectLineForTest(option.id))
                        {
                            selected++;
                        }
                    }

                    report.AppendLine("    selected " + selected + " report line(s) for " + id);
                    result = RecorderReportUI.Instance.Confirm();
                    break;
                }

                // No panel opened (no authored reportLineOptions for this item yet) -
                // TryOpenReport's own fallback already applied MarkLogged directly.
                if (esm.GetRecord(id).status == EvidenceStatus.PhotographedFinal)
                {
                    Fail(failures, report, id + ": report panel did not open and no report was logged.");
                    return;
                }
                result = TransitionResult.Applied;
                break;
            case EvidenceStatus.Collected:
                if (IsSwabCollectedAfterLogging(esm, id))
                {
                    // Blood spatter has no physical presence to grab - it's
                    // collected by the combined swab tool, not the bag (see
                    // SwabTool's class comment). Routes through SwabTool.TryContain -
                    // the exact call the container's receiving zone makes on a real
                    // click - rather than MarkCollected alone. Relies on the caller
                    // having already run the item through SwabTool.TrySwab first
                    // (see RunFullFlow), the same way this case relies on the bag
                    // being open for every other item.
                    if (SwabTool.Instance == null)
                    {
                        Fail(failures, report, id + ": no SwabTool in scene - can't exercise the real swab collection path.");
                        return;
                    }
                    result = SwabTool.Instance.TryContain();
                    break;
                }

                // Routes through EvidenceBagTool.TryInsert - the exact call the bag's
                // receiving zone makes on a real insertion - rather than MarkCollected
                // alone.
                var evidenceForCollect = EvidenceProp.FindById(id);
                if (evidenceForCollect == null)
                {
                    Fail(failures, report, id + ": no registered EvidenceProp - can't exercise the real bagging insertion path.");
                    return;
                }
                if (EvidenceBagTool.Instance == null)
                {
                    Fail(failures, report, id + ": no EvidenceBagTool in scene - can't exercise the real bagging insertion path.");
                    return;
                }
                result = EvidenceBagTool.Instance.TryInsert(evidenceForCollect);
                break;
            case EvidenceStatus.Sealed:
                if (IsSwabCollectedAfterLogging(esm, id))
                {
                    // Routes through SwabTool.TrySeal - closing the container on
                    // whatever it currently holds - rather than MarkSealed alone.
                    // Relies on the Collected case just above having deposited this
                    // same id's sample and nothing else since, which holds here for
                    // the same reason the bag's own equivalent comment gives.
                    if (SwabTool.Instance == null)
                    {
                        Fail(failures, report, id + ": no SwabTool in scene - can't exercise the real swab seal path.");
                        return;
                    }
                    if(!SamplePreparationTestUtil.PrepareCurrent())
                        Fail(failures,report,id+": sample preparation failed.");
                    result = SwabTool.Instance.TrySeal();
                    break;
                }

                // Routes through EvidenceBagTool.TrySeal - closing the bag on whatever
                // it currently holds - rather than MarkSealed alone. Relies on the
                // Collected step just above having inserted this same id into the bag
                // and nothing else having been inserted since, which holds here because
                // RunFullFlow processes one item's full lifecycle at a time.
                if (EvidenceBagTool.Instance == null)
                {
                    Fail(failures, report, id + ": no EvidenceBagTool in scene - can't exercise the real bagging seal path.");
                    return;
                }
                result = EvidenceBagTool.Instance.TrySeal();
                break;
            case EvidenceStatus.Processed: result = esm.MarkProcessed(id, ToolType.IOC); break;
            default:
                Fail(failures, report, id + ": no transition method for " + target);
                return;
        }

        if (result != TransitionResult.Applied)
        {
            Fail(failures, report, id + ": " + target + " returned " + result + " instead of Applied.");
            return;
        }

        report.AppendLine("    -> " + target);
        DismissAnyOpenHypothesisCheckpoint(report);
    }

    /// <summary>
    /// A real HypothesisCheckpointManager/HypothesisCheckpointUI, independent of this
    /// test, can genuinely pop a checkpoint open mid-run - a CountThreshold trigger
    /// (e.g. HYP-01's "3 items at Found") fires the instant enough items in THIS
    /// scene cross it, which happens naturally partway through RunFullFlow walking
    /// several items' lifecycles one after another. Left unanswered, that panel holds
    /// PlayerUIGate, which silently blocks RecorderReportUI.Show() for any item logged
    /// while it's up - a real integration point this test has to account for now that
    /// every roster item has authored reportLineOptions and therefore actually reaches
    /// RecorderReportUI (an item with none never touched PlayerUIGate at all, which is
    /// why this was never visible before). Called after every Step so it can never be
    /// missed regardless of which trigger or threshold fires it. Answers with
    /// AnswerForTest's default (index 0 for both picks) - which specific theory/
    /// reasoning is chosen doesn't matter here, only that the panel closes so the rest
    /// of the flow can proceed for real.
    /// </summary>
    private static void DismissAnyOpenHypothesisCheckpoint(StringBuilder report)
    {
        var ui = UnityEngine.Object.FindFirstObjectByType<HypothesisCheckpointUI>();
        if (ui != null && ui.IsShowing)
        {
            if (CheckpointAnswererForTest != null)
            {
                CheckpointAnswererForTest(ui);
            }
            else
            {
                ui.AnswerForTest();
            }
            report.AppendLine("    (auto-answered a real hypothesis checkpoint that fired mid-flow)");
        }

        // EndingManager.TrySubmitConclusion resolves and shows an ending the moment a
        // conclusion is confirmed, so CompletionScreenUI may genuinely be showing here.
        // DismissForTest closes it WITHOUT loading the menu scene (see its own comment
        // for why that's a deliberately different path from the real return-to-menu
        // action): a real SceneManager.LoadScene call here would tear down this very
        // Play session mid-test.
        var completionScreen = UnityEngine.Object.FindFirstObjectByType<CompletionScreenUI>();
        if (completionScreen != null && completionScreen.IsShowing)
        {
            completionScreen.DismissForTest();
            report.AppendLine("    (dismissed the completion screen shown by Submit Conclusion, without leaving the scene)");
        }
    }

    /// <summary>
    /// The dust -> lift half of an on-scene print sub-step (both the knife, EVD-014,
    /// and the gloves, EVD-019, as of this task - see FingerprintKitTool's own class
    /// comment on generalizing off EvidenceFingerprintMarker instead of staying
    /// hardcoded to the knife alone), exercising the exact real COLLISION entry points
    /// (FingerprintKitTool.HandleBrushTriggerEnter, FingerprintTapeTool.
    /// HandleTipTriggerEnter) a real brush/tape touching the print's own TouchZone
    /// collider would call - not the bare TryDust()/TryLift() action methods directly,
    /// so this also proves the contact-recognition wiring itself (the marker lookup
    /// inside each Handle*TriggerEnter), not just the gate logic those methods already
    /// had. UV reveal and (where applicable) the flip-before-dust gate are the CALLER's
    /// job, not this method's - both need to run before the swab-block-order and
    /// flip-lock assertions the call site itself proves, so duplicating them here
    /// would either re-run them pointlessly or run them too late to prove anything.
    /// Unlike the old EVD-016 roster item the knife's own half of this used to drive
    /// (its own independent Collected/Sealed/Processed tail, bundled into one "seal"
    /// click), a print marker has no status progression of its own at all - TryLift
    /// (reached through the tape's contact handler) reports MarkFingerprinted then
    /// MarkMatched straight against the PARENT item's record (id/record here are the
    /// parent's, e.g. the knife's or the gloves'), and the print simply disappears
    /// (UVRevealable.Remove) rather than being sealed anywhere.
    /// </summary>
    private static void RunFingerprintKitSubStepSequence(List<string> failures, StringBuilder report, string id, EvidenceRecord record, EvidenceFingerprintMarker marker, Collider touchCollider)
    {
        if (!GloveChangeTestUtil.EnsureClean()) { Fail(failures,report,id+": replace gloves before fingerprint work: "+GloveChangeTestUtil.LastFailure); return; }
        // Prove the failure path first: the tape touching a not-yet-dusted print must
        // be refused, not silently succeed - lifting has to come strictly after
        // dusting even though both are now plain contact gestures with no click to
        // sequence them for you.
        FingerprintTapeTool.Instance.HandleTipTriggerEnter(touchCollider);
        if (record.fingerprintingDone)
        {
            Fail(failures, report, id + ": tape lifted an undusted print - the dust-before-lift gate isn't being enforced.");
            return;
        }
        report.AppendLine("    tape-on-undusted-print correctly refused (FingerprintTapeTool.HandleTipTriggerEnter)");

        // Prove the other failure path: the brush touching the print with no dust
        // loaded must be refused too, not silently succeed - the same "show the
        // failure path before the success path" discipline FingerprintStationTool's
        // own sequence uses. Skipped if dust is already loaded (the call site's own
        // flip-lock check, for an item with requiresFlipBeforeFingerprinting, loads it
        // early to isolate that check - dust persists once loaded, it is never
        // reset between items in the same session, real or tested).
        if (!FingerprintKitTool.Instance.DustLoaded)
        {
            FingerprintKitTool.Instance.HandleBrushTriggerEnter(touchCollider);
            if (marker.IsDusted)
            {
                Fail(failures, report, id + ": brush-print contact dusted the print with no dust loaded on the brush - the brush-load gate isn't being enforced.");
                return;
            }
            report.AppendLine("    brush-on-print correctly refused with no dust loaded on the brush");
        }
        else
        {
            report.AppendLine("    (brush already has dust loaded from an earlier check this item or an earlier item - no-dust-loaded refusal not re-provable here)");
        }

        if (!FingerprintKitTool.Instance.TryLoadDust())
        {
            Fail(failures, report, id + ": FingerprintKitTool.TryLoadDust was refused.");
            return;
        }
        report.AppendLine("    loaded dust onto the brush (FingerprintKitTool.DustLoaded=" + FingerprintKitTool.Instance.DustLoaded + ")");

        // The real contact a dusted brush swiped across the print makes. Dusting
        // accumulates over sustained contact (EvidenceFingerprintMarker's
        // requiredDustSeconds), so keep brushing - the same TryDust the held brush
        // calls every frame - bounded so a genuine refusal still fails here.
        FingerprintKitTool.Instance.HandleBrushTriggerEnter(touchCollider);
        for (int brushStroke = 0; brushStroke < 5000 && !marker.IsDusted; brushStroke++)
        {
            if (!FingerprintKitTool.Instance.TryDust(marker))
            {
                break;
            }
        }
        if (!marker.IsDusted)
        {
            Fail(failures, report, id + ": brush-print contact was refused even though " + id + " is PhotographedFinal, flipped if required, and the brush is loaded.");
            return;
        }
        report.AppendLine("    dusted and locked visible via brush-print contact (marker.IsDusted=" + marker.IsDusted + ")");

        // The real contact the tape makes against the now-dusted print, then the lift
        // itself - FingerprintTapeTool.Update calls exactly this TryLift while its
        // trigger is held on a touching print.
        FingerprintTapeTool.Instance.HandleTipTriggerEnter(touchCollider);
        FingerprintKitTool.Instance.TryLift(marker);
        var inventory = EvidenceInventory.Instance;
        if (!record.fingerprintLifted || record.fingerprintingDone
            || inventory.CurrentHeldEvidenceId != EvidenceInventory.FingerprintSampleId(id)
            || HeldItemCoordinator.Instance.TryStoreHeldEvidence())
        {
            Fail(failures, report, id + ": lifted print did not become separately held/storable evidence.");
            return;
        }
        if (!FingerprintComparisonTestUtil.CompareStoredSource(id) || !HeldItemCoordinator.Instance.TryStoreHeldEvidence())
        { Fail(failures, report, id + ": held print must be compared and matched before storage."); return; }
        report.AppendLine("    -> compared, matched and explicitly stored fingerprint sample associated with " + id);

        if (!record.fingerprintingDone)
        {
            Fail(failures, report, id + ": fingerprintingDone was not set on " + id + "'s own record by the tape-print contact.");
            return;
        }
        report.AppendLine("    -> lifted via tape-print contact, fingerprinted on " + id + "'s own record (FingerprintTapeTool.HandleTipTriggerEnter -> FingerprintKitTool.TryLift)");

        if (marker != null && marker.gameObject.activeSelf)
        {
            Fail(failures, report, id + ": print marker is still active after lifting - it should be removed entirely (UVRevealable.Remove).");
        }
        else
        {
            report.AppendLine("    -> print removed entirely (UVRevealable.Remove, via TryLift)");
        }

        if (!EvidenceStateManager.Instance.IsFingerprintMatched(id))
            Fail(failures, report, id + ": matching identity was lost after storage.");
        else report.AppendLine("    -> confirmed comparison identity retained: " + record.fingerprintLabMatchResult);
    }

    /// <summary>
    /// Drives the real dust -&gt; lift -&gt; compare interaction through
    /// FingerprintStationTool for EVD-017/018/019. The knife (EVD-014) is on-scene
    /// too (fingerprintBeforeCollection), but never routes through this station -
    /// its own print sub-step goes through FingerprintKitTool instead (see
    /// RunKnifePrintSubStepSequence and this method's own call site, which branches
    /// by id specifically because the two on-scene items use different real tools).
    /// Rather than calling MarkFingerprinted directly - closes the coverage gap the retired
    /// FingerprintStationPlaceholder left (it had zero real interaction to exercise,
    /// only the gate). Deliberately proves the failure path first (lifting with zero
    /// dust must be refused, not silently succeed) before proving the success path -
    /// a step that cannot be shown failing here has not actually been shown working
    /// either, the same reasoning the retired placeholder's own class comment gave for
    /// why it was insufficient.
    /// </summary>
    private static void RunFingerprintStationSequence(List<string> failures, StringBuilder report, string id, EvidenceRecord record)
    {
        var station = FingerprintStationTool.Instance;
        if (station == null)
        {
            Fail(failures, report, id + ": no FingerprintStationTool in scene - can't exercise the real dust/lift/compare path.");
            return;
        }

        var earlyLift = station.TryLift(id);
        if (earlyLift != TransitionResult.Violation)
        {
            Fail(failures, report, id + ": TryLift succeeded with zero dust coverage - the station cannot fail, which defeats its purpose.");
            return;
        }
        report.AppendLine("    lift correctly refused with zero dust coverage");

        for (int i = 0; i < station.MinGoodDustCoverage; i++)
        {
            if (!station.TryDust(id))
            {
                Fail(failures, report, id + ": TryDust was refused mid-sequence.");
                return;
            }
        }

        var liftResult = station.TryLift(id);
        if (liftResult != TransitionResult.Applied)
        {
            Fail(failures, report, id + ": TryLift returned " + liftResult + " instead of Applied with correct dust coverage.");
            return;
        }
        report.AppendLine("    dusted and lifted a good print");

        var compareResult = station.TryCompare(id);
        if (compareResult != TransitionResult.Applied)
        {
            Fail(failures, report, id + ": TryCompare returned " + compareResult + " instead of Applied.");
            return;
        }

        if (!record.fingerprintingDone)
        {
            Fail(failures, report, id + ": fingerprintingDone flag was not set by TryCompare.");
            return;
        }

        report.AppendLine("    -> compared and fingerprinted (FingerprintStationTool.TryDust -> TryLift -> TryCompare)");

        // TryCompare itself now resolves the lab match (MarkMatched) for an item with an
        // authored EvidenceDefinition.fingerprintMatchIdentity - no test-side MarkMatched
        // shortcut. Assert the real compare got there on its own.
        string expectedIdentity = EvidenceStateManager.Instance.GetFingerprintMatchIdentity(id);
        if (expectedIdentity == null)
        {
            report.AppendLine("    -> no authored match identity; lab status stays " + record.fingerprintLabStatus);
        }
        else if (record.fingerprintLabStatus != FingerprintLabStatus.Matched || record.fingerprintLabMatchResult != expectedIdentity)
        {
            Fail(failures, report, id + ": TryCompare did not resolve the lab match (status " + record.fingerprintLabStatus + ", result '" + record.fingerprintLabMatchResult + "', expected '" + expectedIdentity + "').");
        }
        else
        {
            report.AppendLine("    -> lab match resolved by TryCompare itself: " + record.fingerprintLabMatchResult);
        }
    }

    // Old-style swab collection (collectedBySwab without an on-scene sample): the swab
    // itself is the Collected/Sealed action after Logged.
    private static bool IsSwabCollectedAfterLogging(EvidenceStateManager esm, string id)
    {
        var definition = esm.GetRecord(id)?.definition;
        return definition != null && definition.collectedBySwab && !definition.requiresSwabbing;
    }

    private static void Fail(List<string> failures, StringBuilder report, string message)
    {
        failures.Add(message);
        report.AppendLine("    FAIL: " + message);
    }
}
