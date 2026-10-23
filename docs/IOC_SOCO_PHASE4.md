# Phase 4: IOC decision, SOCO request and turnover

Implemented locally on the uncommitted Phase 1–3 foundation. SceneTurnover remains the authoritative phase owner; one CSI_Environment is retained. No commit, push, deployment or Phase 5 work.

## Procedural boundary

The [PNP CSI SOP](https://elibrary.judiciary.gov.ph/thebookshelf/showdocs/11/56392) requires an IOC assessment before a SOCO request, with Form 2 accomplished/submitted at SOCO arrival before processing. The [provided forms reference](https://www.scribd.com/document/534029902/CSI-Form-1-first-responders-form-1) identifies CSI Form 2 as Request for the Conduct of SOCO. This implementation records that procedural milestone and does not create or display invented form fields.

Phase progression remains IOCTurnover → IOCAssessment → IOCSOCODecision → SOCORequested → SOCOTurnover → SOCOInvestigation → InvestigationComplete. PlayerRole now remains IOC through SOCOTurnover and changes to SOCO only at SOCOInvestigation. Form2Accomplished derives from phase >= SOCOTurnover, while SOCOAssistanceRequested derives from phase >= SOCORequested. No duplicate request/form flags were added.

## Decision and arrival

SOCOWorkflowInteraction is a presentation/input adapter hosted on the existing FR root. It neither changes the FR's visual identity nor owns procedural progression. A compact nonmodal world-space choice panel uses existing HudFollowCamera and WorldPanelPointer. The player can point and press right trigger, or choose with right thumbstick and press A. General modal UI suppresses the panel/actions; movement is not locked.

Question: **Does this scene require SOCO assistance?**

- Request SOCO assistance
- Continue without SOCO

Continue does not advance or end the case. Exact corrective feedback:

> The scene contains physical evidence that requires specialized documentation, examination and collection. Reassess whether SOCO assistance is necessary.

The rejected choice logs once per session, even when reconsidered repeatedly; feedback can be repeated. The appropriate request logs the decision and advances through TryAdvance to SOCORequested without unlocking tools or starting the investigation timer. Request feedback: **SOCO assistance requested through the police operations channel.** No real dispatch/network operation is performed.

The local arrival acknowledgement occurs after a two-second request state, waiting if another modal UI is open. It advances to SOCOTurnover and derives the accomplished/submitted Form 2 milestone. Exact feedback: **CSI Form 2 completed and submitted to the SOCO Team Leader.**

Arrival returns the OVRCameraRig to the controlled entrance's authored horizontal position/heading while preserving its settled locomotion floor height and HMD tracking offsets. No fade, animation or cinematic system was introduced. The FR remains at the security position and the cordon stays established. The IOC speaker is represented by the central briefing's speaker identity; a separate visible IOC NPC is deferred, as permitted by this phase's scope.

## Exact IOC briefing

The player explicitly chooses **[A] Receive IOC scene briefing** after arrival. Three central catalog lines, each with a 20-second reading duration, use the existing queue, B-skip protection and Dialogue History:

1. I have completed the initial assessment. The scene remains secured, and no forensic processing has been performed. John is on the kitchen floor with an apparent penetrating injury. A kitchen knife and bloodstaining are visible nearby, and an empty liquor bottle may relate to the reported drinking session.
2. The First Responder's preliminary information places Paul and Peter here drinking with John earlier in the night. The nearby vendor is the reporting witness and reports seeing Peter leave around two in the morning. The First Responder did not witness the attack. These accounts remain preliminary and require verification.
3. SOCO assistance was requested through the operations channel, and I have completed and submitted CSI Form 2 to the SOCO Team Leader. The scene is ready for your forensic documentation and processing. I will retain scene control and coordinate support; the First Responder will continue assisting with security.

Catalog IDs: IOC_SOCO_01, IOC_SOCO_02, IOC_SOCO_03. History category: IOC. Stable source: ioc-soco-turnover, separate from first-responder-turnover. Repeated start attempts are rejected during or after the briefing. Completion waits until all source lines have displayed/skipped; a missing dialogue panel does not complete it.

## SOCO activation and objectives

During decision, request and briefing, role is IOC and all prior SOCO gates/timing remain intact. After the full briefing, ordered TryAdvance(SOCOInvestigation) releases the existing OnInvestigationStarted boundary. Tools/discovery become available, original SOCO objectives/narrative start, the original photo route starts with its existing two-second instruction delay, and InvestigationSeconds begins. Recorder, hypotheses and board retain their original downstream requirements. No evidence is found or processed by the procedural transition itself.

Role notification: **Role: SOCO Investigator. Begin scene documentation.**

Objectives:

1. Determine whether SOCO assistance is required.
2. Await SOCO arrival.
3. Receive IOC scene briefing.
4. Existing ViCSI SOCO objectives.

Request SOCO assistance is the decision action, not an additional redundant objective. IOC objectives remain excluded from scoring. Their completion uses appended RequirementType.ScenarioPhaseReached (20), with inspector support for its phase field. Existing serialized requirement enum values are unchanged.

## Logging and reset

Appended informational SessionEventType entries: SOCOAssistanceDecision, SOCORequested, CSIForm2Completed, SOCOTurnoverStarted, SOCOTurnoverCompleted and SOCOInvestigationStarted. The decision target is request-soco or continue-without-soco and carries appropriate=true/false. Transition events occur once through the authoritative ordered API. TurnoverStarted represents entry into the handover phase; no individual briefing line or generic UI click is logged. No upload contract/schema change.

Fresh same-scene reload resets phase, request derivation, decision deduplication, Form 1/2 derivation, IOC observations, timer and presentation. The player returns to IOC awaiting FR turnover, with prepared/unreceived Form 1, no accomplished Form 2 and locked SOCO systems.

## Focused validation

Only ScenarioPhaseFoundationTest is run through the existing controlled open-Editor runner. Seven grouped checks cover decision UI/state, rejected choice, correct request, arrival/Form 2/entrance, full briefing, original SOCO activation, and reset. The earlier Phase 2/3 diagnostics remain recorded in their reports; this diagnostic prepares those already validated milestones rather than replaying the entire assessment or evidence investigation. No full regression or Quest hardware walkthrough is performed.

Initial runs exposed a test that needed to respect the original photo-route delay, and a real repositioning issue: restoring the early Awake rig height undid the locomotion system's settled floor adjustment. The test now waits for the authored delay and the transition preserves settled height.

Final result: **7 PASS / 0 FAIL**, report at docs/evidence/phase5-last-drink/phase4-soco-transition/focused.txt. Exact grouped checks:

1. Completed assessment reaches IOCSOCODecision with the choice UI/objective and SOCO gates intact.
2. Actual pointer choice rejects Continue without SOCO, offers corrective feedback, logs that choice once and leaves progression unchanged.
3. Request choice records decision/request once and enters only SOCORequested, with the arrival objective and stopped SOCO timer.
4. Arrival completes Form 2 once, restores entrance position while preserving settled height, and enters IOC-role SOCOTurnover with the cordon retained.
5. All three briefing lines display/skip through the normal queue/history once; tools/markers/timing stay gated throughout, and completion enters investigation once.
6. SOCO role/tools, original first photo marker, original body objective and timing activate, while all four evidence records remain NotFound.
7. Actual same-scene reload clears request, Form 2 and assessment and restores IOC startup with SOCO locked.

Unity compiled final source with zero compiler errors. Testing used only the existing controlled runner in the open Editor; no additional Unity process, process termination or lock deletion was needed. The Editor finished outside Play mode in CSI_Environment with a clean scene, no pending/queued suite and restored desktop XR settings. The recovery checkpoint remains 015eafc350564945e59c390af9b802260a19fff6.

## Files changed in Phase 4

- Assets/_Project/Scripts/SceneTurnover/SOCOWorkflowInteraction.cs and .meta (new)
- Assets/_Project/Scripts/SceneTurnover/SceneTurnover.cs
- Assets/_Project/Scripts/SceneTurnover/IOCAssessmentInteraction.cs (assessment panel stops at decision; choice panel takes over)
- Assets/_Project/Scripts/Logging/SessionEventType.cs
- Assets/_Project/Scripts/Requirements/RequirementEnums.cs
- Assets/_Project/Scripts/Requirements/RequirementNode.cs
- Assets/_Project/Scripts/Requirements/RequirementEvaluator.cs
- Assets/_Project/Editor/Requirements/RequirementNodeDrawer.cs
- Assets/_Project/Scripts/Testing/ScenarioPhaseFoundationTest.cs
- Assets/_Project/Scenes/Scenarios/CSI_Environment.unity
- Assets/_Project/Data/Objectives/Objectives_CSI_Environment.asset
- Assets/Resources/ViCSIDialogueCatalog.asset
- taskbycodex.md
- docs/IOC_SOCO_PHASE4.md

Existing Phase 1–3 changes remain uncommitted. Phase 4 changes no SOCO evidence sequence, photo sequence, forensic finding, Recorder choice, hypothesis, relationship, conclusion/ending rule, scoring, ground truth, backend or upload contract.

## Next-phase boundary and limitations

Phase 5 starts with the existing SOCOInvestigation workflow. Form 2 is a procedural milestone, not an interactive document. Local arrival acknowledgement does not simulate dispatch availability or travel. A visible IOC actor and transition polish remain deferred; the briefing explicitly preserves IOC scene control. Quest ergonomics/repositioning remain to be validated on hardware. The ordered API remains a trusted internal progression hook, as in the existing FR implementation; production dialogue completion is handled by the input adapter.
