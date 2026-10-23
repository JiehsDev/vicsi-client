# Phase 3: IOC initial assessment

Implemented locally on the Phase 1/2 foundation. SceneTurnover remains the sole procedural state owner; no commit, push or deployment. Normal progression ends at IOCSOCODecision.

The [PNP CSI SOP](https://elibrary.judiciary.gov.ph/thebookshelf/showdocs/11/56392) places IOC initial assessment after FR turnover and before determining the need for SOCO assistance. The supplied [CSI forms reference](https://www.scribd.com/document/534029902/CSI-Form-1-first-responders-form-1) remains the identity reference for the already received Form 1; this phase adds no paperwork UI or form state.

## Checklist and completion

1. Confirm the scene remains secured.
2. Review First Responder turnover.
3. Observe victim and immediate surroundings.
4. Survey the main scene area.
5. Identify possible forensic examination needs.
6. Review preliminary witness/person information.
7. Complete the initial scene assessment.

The received Form 1/full Phase 2 briefing satisfies the two preliminary-information rows; the existing Dialogue History remains available. John, Paul/Peter's reported drinking, Peter's reported 2 AM departure, the vendor and the FR's lack of firsthand attack knowledge remain preliminary information, not findings. No repeat conversation is required.

SceneTurnover owns a session-local set of stable observation IDs. Checklist rows derive from this set, authored observation categories, existing cordon state and Form1Received. They are not independent completion booleans. Security is acknowledged with the general survey while the perimeter remains visible and controlled entrance is open. Readiness requires received Form 1, secured-scene survey, victim and any two of the three possible-evidence observations. An explicit completion interaction then calls CompleteIOCAssessment, which uses ordered TryAdvance(IOCSOCODecision). Direct TryAdvance to that phase also checks readiness. No SOCO request or decision is performed.

The assessment objective uses appended RequirementType.IOCAssessmentCompleted (19), preserving existing enum values. Both IOC objectives are excluded from scoring. The next objective is **Determine whether SOCO assistance is required.** It remains open for Phase 4.

## Interaction and authored observations

IOCAssessmentInteraction on the existing First Responder root is an input/presentation adapter, not another phase/evidence manager. It uses the existing right-hand A/trigger bindings, contextual prompt and dialogue catalog. Head gaze selects an unobserved target within 3.5 m and an 8-degree cone with a read-only solid-collider line-of-sight check. Only explicit interaction records the observation. Evidence trigger entry and looking alone do not count.

| Observation ID | Target | Exact preliminary text |
|---|---|---|
| ioc-victim | BODY-A | John is lying on the kitchen floor. An apparent penetrating injury is visible, but the body has not been examined. |
| ioc-possible-weapon | EVD-014 | A kitchen knife is visible near the victim. It has not been moved or processed. |
| ioc-bloodstain-area | EVD-015 | Bloodstaining is visible near the victim's position. No reconstruction has been performed. |
| ioc-drinking-context | EVD-018 | An empty liquor bottle is visible in the scene. It may relate to the reported drinking session; it has not been examined. |
| ioc-main-scene-survey | Authored main-scene viewpoint | The main scene layout, victim position and visible possible evidence have been surveyed. The cordon remains established and access is controlled. |

The survey area is a 4 x 3 x 3 m Bounds centered at (-2, 1.5, 10), with focus (-3, 0.6, 12.8). Looking toward that focus within 45 degrees offers a survey interaction when no item is selected. No photography circle is reused, no invisible wall is added, and no evidence collider is changed. The victim's disabled solid collider stays disabled; an authored observation offset permits seeing the victim without changing processing or grab access.

The checklist is a small nonmodal world-space canvas using the existing HudFollowCamera pattern. It appears only in assessment (or as the decision directive afterward), has no pointer-ray targets, and does not acquire a locomotion/modal UI lock. Observation feedback uses short catalog tooltips, not Dialogue History or findings. General modal UI temporarily suppresses observation input.

## Exact guidance

> You have control of the scene now. Conduct an initial assessment before deciding what specialized processing is required. Observe the scene as it stands and avoid disturbing anything.

The guidance is one central catalog dialogue on assessment entry, with normal dialogue/history behavior; there is no narration for each checklist row. The completion notification is **Initial assessment complete. Determine whether SOCO assistance is required.**

## Safety, logging and reset

IOC observation writes only the observation ID set. It never calls EvidenceStateManager mutation methods or unlocks report lines, hypotheses or findings. Existing phase gates continue to block discovery, equipment/tools, forensic processing, photography, chalk/tents, measurement, swabbing, fingerprint processing, collection, Recorder evidence flow, procedure UI, sketch, hypotheses, deduction board and conclusion submission. IOC movement remains available through the controlled entrance. InvestigationSeconds remains zero until SOCOInvestigation; session-wide elapsed time retains its existing meaning.

Appended SessionEventType entries are IOCAssessmentStarted, IOCObservationRecorded and IOCAssessmentCompleted. Each observation logs its stable ID once. Start/completion log once per ordered transition. Form 1 receipt logging precedes assessment entry. No event is emitted for passive gaze, individual guidance lines or each frame; no upload schema changes.

Production new-session/same-scene reload resets the authoritative phase, observation set, objectives and managers together. It returns to IOCTurnover with Form 1 prepared but unreceived, fresh evidence, closed controlled entrance and hidden checklist. No standalone partial reset API was introduced.

## Validation

Used the existing controlled file-request runner inside the currently open Editor, one suite per Play session, with desktop XR startup temporarily suppressed and restored afterward. The external scene-change dialog was resolved with its verified Reload action; no process was killed or lock deleted. Only ScenarioPhaseFoundationTest ran; no full regression or Quest hardware walkthrough was performed.

Final focused run: **7 PASS, 0 FAIL**. Exact grouped checks:

1. New session starts as IOC awaiting FR turnover, with prepared Form 1 and stopped investigation clock.
2. Actual four-line briefing/skipping records receipt, feedback, history and logs once; assessment objective and controlled entrance become available.
3. Repeated turnover cannot duplicate phase, receipt, feedback, history or logs.
4. Checklist appears, inherited information is reviewed, incomplete assessment cannot advance, and SOCO access remains gated.
5. Actual gaze selection/interaction records survey, victim, knife and bottle while evidence statuses/positions remain unchanged; attempted processing/grab/UI remains blocked; each observation logs once.
6. Survey/victim/two of three possible-evidence observations complete assessment once, activate the decision objective and stop with SOCO still locked.
7. Actual same-scene new-session reload clears observations/checklist and restores pre-turnover phase/forms, objectives, cordon, evidence and gates.

Final report: docs/evidence/phase5-last-drink/phase3-assessment/focused.txt (local diagnostic artifact). Earlier failed runs had 5 PASS/2 FAIL; those were diagnostic viewpoint problems corrected below. Unity compiled the final source without compiler errors. The Editor finished outside Play mode, with no suite queued and no unsaved scene changes. Existing unrelated scene/package warnings were not changed.

The first run identified diagnostic camera setup errors: its survey gaze selected the nearby bottle, and adding a fixed height above the bottle placed the camera above the ceiling. Corrected the survey viewpoint and used a standing eye height. The production line-of-sight and item-selection rules were retained. The Unity 6.5 obsolete GetInstanceID error in the diagnostic snapshot was corrected to use object references.

## Phase 4 boundary and changed files

Phase 4 should begin at SceneTurnover.Phase == IOCSOCODecision and use the existing ordered TryAdvance API for its eventual request flow. Assessment progress is session-local; this phase introduces no persisted schema. The broad decision objective deliberately has no completing mechanic yet. No Form 2, request interaction, SOCO arrival/handover or interactive form is implemented.

Incremental Phase 3 files (earlier Phase 1/2 work remains uncommitted):

- Assets/Resources/ViCSIDialogueCatalog.asset
- Assets/_Project/Data/Objectives/Objectives_CSI_Environment.asset
- Assets/_Project/Data/Scenarios/IOC_Assessment_CSI_Environment.asset and .meta (new)
- Assets/_Project/Scenes/Scenarios/CSI_Environment.unity
- Assets/_Project/Scripts/SceneTurnover/SceneTurnover.cs
- Assets/_Project/Scripts/SceneTurnover/IOCAssessmentDefinition.cs and .meta (new)
- Assets/_Project/Scripts/SceneTurnover/IOCAssessmentInteraction.cs and .meta (new)
- Assets/_Project/Scripts/SceneTurnover/FirstResponderTurnover.cs (procedural log ordering)
- Assets/_Project/Scripts/Logging/SessionEventType.cs
- Assets/_Project/Scripts/Requirements/RequirementEnums.cs
- Assets/_Project/Scripts/Requirements/RequirementEvaluator.cs
- Assets/_Project/Scripts/Requirements/RequirementNode.cs
- Assets/_Project/Scripts/Testing/ScenarioPhaseFoundationTest.cs
- Assets/_Project/Scripts/Testing/FirstResponderTurnoverTest.cs (legacy SOCO diagnostic preparation only; not run)
- Assets/_Project/Editor/Phase5PlayModeBatchRunner.cs (legacy SOCO diagnostic preparation only)
- taskbycodex.md
- docs/IOC_SOCO_PHASE3.md

SOCO story/data, processing sequences, report choices, hypotheses, conclusions/endings, scoring, ground truth and backend/upload contracts were not changed by Phase 3.
