# ViCSI narrative and role flow — 2026-10-12

Implemented in Unity 6000.5.6f1 for The Last Drink, reusing the existing role, dialogue, checkpoint, training, finding, witness, relationship and session owners. No duplicate manager, scoring change, retired evidence/wife path, commit, push, deployment or live assessment upload was added. The preceding BODY-A removal work remains intact.

## IOC handoff and closeout

`NPCPresentation` shows the IOC during `SOCOTurnover` (phase 4). The existing `SOCOWorkflowInteraction.FinishBriefing` waits until all three opening lines from `ioc-soco-turnover`, including the closing fade, have drained. Its existing advance to `SOCOInvestigation` (phase 5) now hides the IOC's `visibleBody`, prompt anchor and body collider. The actor root, workflow and serialized references remain alive. Later closeout phases never show the IOC again. There is no disappearance timer or destruction.

The final briefing still covers the operations request and completed CSI Form 2, then says: "The scene's ready for you to take over the investigation. The First Responder will help maintain scene security."

Closeout retains its existing inventory, final survey, accountability, confirmation, release and completion gates. The historical `IOCCompletionReport` phase, methods and event/source IDs remain compatible, but its physical IOC proximity requirement is replaced by `CanReportCompletion`: a main camera and investigation handoff having occurred. The reporting question and release acknowledgement come from the Professor. Catalog labels and the authored completion-report objective no longer direct the student to an absent NPC. Direct release without confirmation is still blocked. BODY-A still requires removal; other evidence accounting requirements are unchanged.

Restart uses the project's existing scene reload. The new FR/session registration resets the role phase, and the freshly loaded IOC is available again when the next opening briefing reaches `SOCOTurnover`.

## Fingerprint assessment and lead

The actual knife lab match confirms `FIND-KNIFE-PRINT-BROTHER`, making the existing integrated `RC-03-FINGERPRINT` eligible. No lead is scheduled at identification alone. Selecting/highlighting a response does not unlock it. The existing explicit confirmation records, flushes and locks the response before releasing explanatory dialogue. Existing HYP-02 justification and dialogue/modal sequencing remain in place.

The existing fingerprint objective can become visible at identification, so its old interpretation-oriented wording was changed to factual wording: "Review the identified knife fingerprint" / "Paul's fingerprint has been identified on the knife." Its unlock, completion and scoring metadata remain unchanged; it cannot supply the preferred interpretation before assessment.

`LEAD-FINGERPRINT-VENDOR` is also included in RC-03's existing dialogue/training deferral lists. The existing `ProceduralTrainingGuidance` schedules the fingerprint lesson followed by the new lead once the confirmed finding and locked response both exist; all valid responses, including a weak one, get the same lead. It waits for modal input, immediate feedback and earlier dialogue. This specific post-assessment lesson/lead pair can run over a nearby ambient evidence prompt, preventing indefinite delay; other training concepts retain their action-prompt behavior. Its existing per-session scheduled set prevents repeat teaching, and an already discovered vendor statement makes a pending lead unnecessary.

Exact Professor guidance:

> Paul's print confirms that he handled the knife at some point, but it doesn't tell us when he touched it or what he did with it. It isn't proof that he used it in the attack. Let's follow up with the vendor and ask whether he noticed anyone else near John's house besides Peter.

## Vendor and relationship integration

`VendorFollowupInterview.FollowupUnlocked` is derived from the existing confirmed finding AND `ReasoningCheckpointManager.IsAnswered("RC-03-FINGERPRINT")`; there is no second persistent unlock flag. The existing question row and handler both enforce it. The existing SOCO investigation phase, physical vendor range, modal ownership, opening-input protection, not-talking and not-already-discovered checks also remain. The initial Peter/discovery/limitation accounts remain available without exposing Paul's follow-up.

Exact question:

> Did you see anyone else near John's house besides Peter?

Exact vendor response:

> Now that you mention it, I remember seeing Paul near John's house around three in the morning. I didn't see where he went after that.

The existing completion coroutine waits for the answer to close, then discovers `statement_vendor_paul_3am`, logs the existing `WitnessFollowupQuestionSelected` / `WitnessStatementDiscovered` events once, and refreshes relationship unlocks. Witness Accounts, Dialogue History and the existing board node continue to use that statement. `REL-paul-timeline-contradiction` still connects `statement_paul_timeline` to `statement_vendor_paul_3am` as a contradiction, with its existing score weight and observation-limit explanation. Its asset and deduction rules were not changed. Seeing Paul is timeline evidence, not witnessing entry or the attack. Talking to the vendor awards no procedure points. The 60/25/15 assessment split and all six checkpoint response weights remain unchanged.

Scene reload discards the old witness discovery, confirmed findings, response records, relationship state and dialogue history. The training owner also clears scheduled/pending lessons on logger-session change. The tests exercise an actual reload after completing the statement and relationship, rather than only resetting fixture fields.

## Files modified for this task

Production:

- `Assets/_Project/Scripts/SceneTurnover/NPCPresentation.cs`
- `Assets/_Project/Scripts/SceneTurnover/SOCOCloseout.cs`
- `Assets/_Project/Scripts/Dialogue/VendorFollowupInterview.cs`
- `Assets/_Project/Scripts/Dialogue/ProceduralTrainingGuidance.cs`
- `Assets/_Project/Scripts/STCS/STCSNotificationUI.cs` (scoped ambient-prompt compatibility for the post-checkpoint guidance)
- `Assets/Resources/ViCSIDialogueCatalog.asset`
- `Assets/Resources/ReasoningCheckpoints_LastDrink.asset`
- `Assets/_Project/Data/Objectives/Objectives_CSI_Environment.asset`

Validation/documentation:

- `Assets/_Project/Scripts/Testing/NarrativeRoleFlowTest.cs` and generated `.meta` (new)
- `Assets/_Project/Scripts/Testing/VendorFollowupTest.cs` (arranges the newly required checkpoint prerequisites)
- `Assets/_Project/Scripts/Testing/SOCOCloseoutTest.cs` (reports while distant from the hidden IOC; retains real confirmation gates)
- `Assets/_Project/Scripts/Testing/ProceduralTrainingGuidanceTest.cs` (isolates presentation checks from automatic reasoning-question modal input)
- `Assets/_Project/Editor/Phase5PlayModeBatchRunner.cs` (new suite starts at the real opening phase)
- This report, `docs/evidence/narrative-role-flow/` artifacts and the appended session handoff note.

No scene wiring changes are required: the saved scene already supplies the IOC visual/collider/workflow, physical vendor, checkpoint UI, closeout and dialogue catalog references. Runtime test state is not saved to the scene.

## Validation

Final result: **397 passed, 0 failed**: 368 Unity checks plus 29 backend scoring checks. Developmental/repeated runs are not counted twice.

| Suite | Passed | Artifact in `docs/evidence/narrative-role-flow/` |
|---|---:|---|
| NarrativeRoleFlowTest | 32 | NarrativeRoleFlowTest-final.txt |
| VendorFollowupTest | 14 | VendorFollowupTest.txt |
| ReasoningCheckpointTest | 60 | ReasoningCheckpointTest.txt |
| ProceduralTrainingGuidanceTest | 20 | ProceduralTrainingGuidanceTest.txt |
| SOCOCloseoutTest | 48 | SOCOCloseoutTest.txt |
| BodyRemovalTest | 30 | BodyRemovalTest.txt |
| BiologicalSamplingTest | 59 | BiologicalSamplingTest.txt |
| EvidenceProcedureMapTest | 60 | EvidenceProcedureMapTest.txt |
| FinalDeductionAssessmentTest | 36 | FinalDeductionAssessmentTest.txt |
| EndingManagerTest | 9 | EndingManagerTest.txt |
| Backend deductionIntegration.test.ts | 29 | backend-deduction-tests.txt |

Unity suites ran sequentially through the existing Phase5PlayModeBatchRunner in fresh CSI scenes. The new test uses real opening briefing, lab-match, checkpoint confirmation, vendor dialogue and relationship handlers; only earlier observation/lab-ready procedures are arranged as fixtures. Separate procedure suites cover their physical interactions. The vendor suite also exercises the actual board link; `vendor-knife-fixture.txt` records its successful knife lifecycle with the blood prerequisite arranged. Automated tests use the existing offline sandbox; no grading/session upload is sent. The backend command was `node --conditions=react-server --experimental-strip-types --test src/lib/scoring/__tests__/deductionIntegration.test.ts` in the existing vicsi-dashboard repository; no backend source changes were needed for this task.

`dialogue-history.json` captures the final real opening, fingerprint lesson, Professor lead, question and vendor answer. `final-editor-state.json` verifies Unity 6000.5.6f1, Android target, compilationFailed=false, compiling=false, playing=false and sceneDirty=false. A console query for C# errors returned zero entries. Both desktop and Android XR initialization are true; no pending runner or XR restoration remains. There is one IOC root, one evidence manager, one vendor interview and one closeout owner. Existing IOC visual/collider/workflow references are intact. The imported catalog and objective assets contain the final wording and both RC-03 lead deferrals. Targeted source whitespace checks passed.

Physical Quest 3S headset/controller acceptance remains pending; these are Editor Play Mode checks. No APK was built or installed.

Developmental reports are retained separately and excluded from final pass totals. The vendor fixture initially pre-marked the lab result, then tried to replay an unidentified print; it now resets that arrangement before exercising real dust/lift. Its old whole-procedure fixture also left a sealed blood sample unstored in the hand, correctly blocking the knife's later bag seal. The witness/board fixture now arranges the already completed blood trace, while the separate biological suite verifies taking, sealing and explicitly storing that sample. The presentation-only training test initially allowed the new standalone reasoning question to acquire the modal token during its timing assertions; it now isolates those automatic assessment owners, with their real integration covered by the narrative and checkpoint suites. These were diagnostic-fixture corrections; no production procedure or assessment gate was relaxed to make them pass.
