# Phase 2: First Responder to IOC turnover

Implemented locally on top of the uncommitted Phase 1 foundation. Recovery checkpoint remains 015eafc350564945e59c390af9b802260a19fff6. No commit, push or deployment.

## Normal play

The same CSI_Environment starts with the IOC at the existing controlled entrance. The current rig origin is (0, -0.15, 0), the NPC root is (1.6, 0.03, -0.35), and the existing interaction radius is 2 m. Positions, NPC model, cordon geometry and interaction bindings were retained.

The initial objective is **Receive scene turnover from the First Responder**. The contextual prompt is **[A] Receive scene turnover**, with the existing trigger alternative. Four catalog lines provide the scene condition, preliminary witness/person information, the limits of initial response, and formal scene/Form 1 handover.

## Exact briefing

1. The house is secured, and access has been controlled since we arrived. John was found on the floor inside with what appears to be a stab wound. We are treating this as a suspected violent death, and the scene remains cordoned off.
2. The reporting witness is the nearby vendor. According to the preliminary information we gathered, John was drinking here earlier with his brother Paul and a friend named Peter. The vendor reports seeing Peter leave around two in the morning. He later found John inside and called us.
3. We have completed the initial response and preserved the scene for your assessment. We have not carried out forensic processing, and I did not witness the attack. The account of the drinking session and Peter leaving is preliminary information; it still needs to be checked.
4. I am formally turning over the crime scene and the preliminary information to you as the investigator on case. Here is the completed CSI Form 1, the First Responder's Report, for your case records. You now have responsibility for the scene. We will maintain the cordon and assist you as needed.

Each line uses the existing dialogue queue/history/B-skip mechanism and has an 18-second reading duration. No dialogue is hardcoded in the interaction script. The [PNP CSI SOP](https://elibrary.judiciary.gov.ph/thebookshelf/showdocs/11/56392) assigns the briefing and Form 1 submission to FR and scene responsibility to IOC. The supplied [CSI forms document](https://www.scribd.com/document/534029902/CSI-Form-1-first-responders-form-1) identifies Form 1 as the actual FR-to-IOC turnover form; no document fields or form UI were fabricated.

## Receipt and state

When all four lines have been displayed or skipped, FirstResponderTurnover calls the existing ordered SceneTurnover.TryAdvance(IOCAssessment). Form1Prepared and Form1Received continue to derive from phase; no new form flag or phase owner was introduced.

The lower notification channel displays: **CSI Form 1 received and added to case records.** followed by **Conduct an initial assessment of the scene.** It lasts six seconds and is separate from dialogue history. The existing clipboard presentation disappears through the existing handoff event; no animation or new asset was added.

The initial objective completes and the new **Conduct an initial assessment of the scene** objective becomes active. It is scoped exactly to IOCAssessment and excluded from scoring. Its completion is intentionally held open with the existing empty-ANY requirement semantics; Phase 3 will supply the actual checklist/completion condition. Phase-aware objective presentation must use IsAvailableInCurrentPhase, as the legacy diagnostic now does, rather than list every historically active record.

The cordon perimeter stays established. Only its controlled entrance opens after turnover. Locomotion remains available, while Phase 1 still gates forensic tools/equip, photography, procedure UI, Recorder, evidence discovery/processing, sketch, hypotheses, deduction and submission. The SOCO investigation timer remains stopped. Normal gameplay stops in IOCAssessment; no decision, request, Form 2 or SOCO-arrival interactions were built.

## Logging and robustness

Existing FirstResponderInteractionStarted and SceneTurnoverCompleted events remain, with their existing values and contracts. CSIForm1Received is appended to SessionEventType as one informational event, target CSI-Form-1, with receivingRole=IOC and via=Conversation or Bypass. No per-line procedural events, scoring changes or upload-schema changes were added.

Completed-phase and talking guards reject repeats. Interaction also checks range, the existing general UI gate, and required panel/catalog availability. The previous 90-second fallback could accept turnover while lines remained queued; it now warns once and continues waiting instead. Missing panel/catalog content cannot silently accept responsibility.

New sessions retain the same-scene reload reset: IOC/IOCTurnover, Form 1 prepared but unreceived, initial objective active, fresh conversation/history/log counts, closed controlled entrance, stopped investigation timer and locked SOCO processing.

## Verification

Only the extended ScenarioPhaseFoundationTest was run in the already-open, clean Editor through the existing controlled file-request runner. Final result: **6 PASS / 0 FAIL**:

1. IOC startup, unreceived prepared Form 1, initial objective and available FR interaction.
2. Actual four-line conversation with normal skip; exactly one receipt/feedback/event per handover, each line once in history, no prompt in history, assessment objective and controlled entrance available.
3. Repeat interaction/completion cannot duplicate phase, receipt, feedback, history or logs.
4. IOC/request/turnover phases retain forensic restrictions, hidden route, stopped timer and unchanged evidence; illegal skips rejected.
5. Existing Phase 1 SOCO activation boundary still releases its investigation systems in the diagnostic.
6. Same-scene reload restores the pre-turnover state, initial objective, fresh receipt count and closed entrance.

The larger FirstResponderTurnoverTest was updated for objective wording/phase filtering but was not run. No full regression or Quest hardware test was run. Final C# compile check reported zero current compilation errors; Play mode exited and no test continuation remained queued.

## Phase 3 handoff

Build assessment mechanics against the current IOCAssessment phase and replace the broad objective's completion requirement. Keep existing phase gates and form-derived state. Do not infer forensic discovery from IOC proximity. All The Last Drink content after SOCO start, evidence procedures, findings, Recorder content, hypotheses, relationships, conclusions, endings, ground truth, scoring, backend and upload contracts were left unchanged in this phase.

## Files changed in Phase 2

- `Assets/Resources/ViCSIDialogueCatalog.asset`
- `Assets/_Project/Data/Objectives/Objectives_CSI_Environment.asset`
- `Assets/_Project/Scenes/Scenarios/CSI_Environment.unity`
- `Assets/_Project/Scripts/SceneTurnover/FirstResponderTurnover.cs`
- `Assets/_Project/Scripts/Logging/SessionEventType.cs`
- `Assets/_Project/Scripts/Testing/ScenarioPhaseFoundationTest.cs`
- `Assets/_Project/Scripts/Testing/FirstResponderTurnoverTest.cs`
- `taskbycodex.md`
- `docs/IOC_SOCO_PHASE2.md`
