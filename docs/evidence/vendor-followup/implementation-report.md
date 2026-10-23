# The Last Drink — vendor follow-up and Paul timeline contradiction

Implemented against checkpoint `4b7573870c85a539270014369109832fb54781d0`. HEAD remains at that checkpoint. No commit, push or deployment. No animation/presentation polish or full regression pass.

1. **Existing architecture:** There is no live vendor NPC/controller or general witness interview tree in the checkpoint. Vendor dialogue exists in `ViCSIDialogueCatalog`, and the board has authored statement cards. Notes opens the deduction board; RecorderReportUI handles physical evidence reports. Existing `GameDialogue`, `STCSNotificationUI`, `DialogueHistoryManager`, `WorldPanelPointer`, utility-menu events, and requirement assets provide the reusable mechanisms.
2. **Implementation:** Added one `VendorFollowupInterview` scene component and a **Vendor Follow-up** utility-menu entry. The panel reviews the existing vendor account and offers one question. Right A or a controller-ray click on the question selects it; B closes the panel. The question and answer use the existing dialogue queue/history. The statement is discovered after the answer leaves that queue through normal completion or the existing skip action. Reopening the panel reviews the discovered record.
3. **Exact player question:** “Did you see anyone else near John's house?”
4. **Exact vendor answer:** “Yeah, now that you mention it, I remember seeing Paul walking near John's house sometime around three in the morning. I didn't see where he went after that, but I recognized him.”
5. **Unlock:** `SceneTurnover.Phase == ScenarioPhase.SOCOInvestigation`. No forensic progress prerequisites. Existing UI gates prevent overlapping interactions. It does not advance or alter IOC/SOCO procedure.
6. **Stable statement ID:** `statement_vendor_paul_3am`. Question catalog ID: `vendor-followup-anyone-else`; answer catalog ID: `statement-vendor-paul-3am`.
7. **Record representation:** A session-local discovered flag exposes a summary and detail in the Vendor Follow-up / Statements panel and a gated board card. The board detail panel also shows the detail when this card is focused. No physical evidence record or persistent witness database was added. Summary: “Vendor saw Paul near John's house around 3:00 AM.” Detail: “The vendor remembers seeing Paul walking near John's house sometime around 3:00 AM. He recognized Paul but did not see where Paul went afterward. He did not see Paul enter the house and did not witness the attack. This observation does not establish who attacked John.”
8. **Paul statement:** Existing `statement_paul_timeline` card, linked to his unchanged catalog alibi: “I went straight home. I did not come back after that.” The board card now includes that exact sentence and its existing midnight context.
9. **Board relationship:** Authored correct relationship `REL-paul-timeline-contradiction`, connecting `statement_paul_timeline` and `statement_vendor_paul_3am`, type `Contradicts`, weight 1. Discovery gates both the card and this relationship. A direct relationship API call cannot select this pair before discovery. The ground-truth relationship roster includes it.
10. **Correct wording:** “Paul's timeline conflicts with the vendor's observation.” Its explanation distinguishes contradiction from guilt and states that the vendor did not see entry or the attack.
11. **Incorrect options:** No new relationship options: this board connects pairs and has no interpretation-choice picker. Existing HYP fingerprint/wound overclaims remain incorrect. HYP-03's correct reasoning explicitly says neither contact nor a sighting proves guilt alone.
12. **Supported Paul:** `REQ_BrotherCaseSupported` retains four physical findings and three physical board relationships, and adds two ALL leaves: the discovered vendor statement and the completed timeline relationship. Nine conditions total. The existing supported ending and Inconclusive suppression both consume this shared requirement. Discovery, a sighting, or a fingerprint alone cannot satisfy it; full physical evidence without the timeline link still fails.
13. **Premature Paul:** Preserved `ending-premature-submission`; incomplete support still resolves there. Its explanation now asks for independently checked statements and board relationships alongside physical findings, without revealing the undiscovered sighting.
14. **Peter:** Existing statements and departure relationship are unchanged. The vendor's initial account retains Peter leaving around 2:00 AM, nervous/sick appearance, later checking on John, finding him, calling police, and not seeing the events inside. Paul discovery does not exonerate Peter. The focused test confirms Peter still resolves to `ending-peter-unsupported` after the supported Paul chain.
15. **Hypotheses:** HYP-01 and HYP-02 unchanged. HYP-03's combined-reasoning text incorporates independently checked statements and a correctly connected contradiction. Its existing ID, trigger, and physical prerequisites remain; it can teach the need for further comparison before that fact is discovered. No HYP-04 or late interview unlock.
16. **Exact supported ending:**

    Professor: Paul's fingerprint alone established contact with the knife, not when or how he used it. The knife is consistent with John's wound, and the bloodstain reconstruction supports the attack location. Paul also said he went home and never returned, but the vendor recognized him near John's house around 3:00 AM. That timeline contradiction, combined with the physical findings, supports Paul as the primary suspect. The vendor did not witness the attack.

    Dispatch: The combined findings and timeline contradiction support Paul as the primary suspect. We'll continue the investigation with him.

17. **Logging:** Appended `WitnessFollowupQuestionSelected` and `WitnessStatementDiscovered` event types. One question event and one discovery event per successful interaction. Existing relationship logging and Dialogue History are reused. No upload schema change or general dialogue-click logging.
18. **Reset:** Uses the project's existing new-session scene reload. The interview record, board connection, relationship state, and dialogue history reset together. No partial live-session reset API was added.
19. **Tests changed:** Added `VendorFollowupTest` with seven checks. It reuses the existing knife lifecycle fixture and report-selection APIs; it does not submit a conclusion or upload a session. Updated StoryContentTest and BrotherPathTest setup to obtain the newly required statement, and allowed the focused test to start before the runner advances procedure. Those broader suites were not run.
20. **Exact verification:** `VendorFollowupTest` through `Phase5PlayModeBatchRunner` in the existing Editor, twice. Each run: **7 PASS, 0 FAIL**; 14 successful assertions across two runs of the same seven checks. The second run exercises the actual controller-ray question target. Checks: undiscovered/startup gate; answer delivery and exactly-once events/history; record and card availability; sighting/fingerprint insufficiency; physical-chain-plus-discovery remains premature; actual board connection permits supported Paul while Peter stays unsupported; real scene reload resets state. `git diff --check` passed. The 872-check suite was not run.
21. **Compile:** Unity 6000.5.6f1 completed compilation; new component/test types and authored requirement/relationship assets load successfully. Console filter `error CS` returned zero entries. Editor returned to Edit mode with no dirty scene. Existing nullable-field serialization warnings and Meta bridge port errors were observed; these are not C# compile failures.
22. **Changed files:** Listed below, including new Unity metadata. Verification artifacts are this report plus `focused-checks.txt` and `focused-checks-final.txt`. Temporary authoring helpers remain under ignored `Temp`, not product source.
23. **Next polish phase:** The working menu interaction can later receive a visual vendor presentation if requested; no animation, NPC model, cinematic, form, clipboard, or environmental polish was started. Validate controller readability in the headset during the separately requested polish/checking work.
24. **Concerns:** Existing XR `WallPenetrationTunneling / TunnelingEffect` reports a missing Mesh Filter during scene startup/reload. It did not fail these seven checks and was left outside this gameplay change. No headset validation or broader regression claim is made. The board's existing pair-connection design has no dedicated wording-choice picker; the contradiction's wording is authored in the relationship explanation. The scene's existing fingerprint match remains its established placeholder implementation.

## Product and diagnostic files

- `Assets/Resources/ViCSIDialogueCatalog.asset`
- `Assets/_Project/Data/Conclusions/Conclusion_BROTHER-RESPONSIBLE.asset`
- `Assets/_Project/Data/Ending/Endings_CSI_Environment.asset`
- `Assets/_Project/Data/Relationships/CSI_Environment/REL-paul-timeline-contradiction.asset` (new)
- `Assets/_Project/Data/Relationships/CSI_Environment/REL-paul-timeline-contradiction.asset.meta` (new)
- `Assets/_Project/Data/Requirements/REQ_BrotherCaseSupported.asset`
- `Assets/_Project/Data/Scenarios/GroundTruth_CSI_Environment.json`
- `Assets/_Project/Data/Scenarios/HypothesisCheckpoints_CSI_Environment.asset`
- `Assets/_Project/Editor/Phase5PlayModeBatchRunner.cs`
- `Assets/_Project/Editor/Requirements/RequirementNodeDrawer.cs`
- `Assets/_Project/Scenes/Scenarios/CSI_Environment.unity`
- `Assets/_Project/Scripts/Deduction/EvidenceBoardController.cs`
- `Assets/_Project/Scripts/Dialogue/VendorFollowupInterview.cs` (new)
- `Assets/_Project/Scripts/Dialogue/VendorFollowupInterview.cs.meta` (new)
- `Assets/_Project/Scripts/Logging/SessionEventType.cs`
- `Assets/_Project/Scripts/Relationships/RelationshipStateManager.cs`
- `Assets/_Project/Scripts/Requirements/RequirementEnums.cs`
- `Assets/_Project/Scripts/Requirements/RequirementEvaluator.cs`
- `Assets/_Project/Scripts/Requirements/RequirementNode.cs`
- `Assets/_Project/Scripts/Testing/BrotherPathTest.cs`
- `Assets/_Project/Scripts/Testing/StoryContentTest.cs`
- `Assets/_Project/Scripts/Testing/VendorFollowupTest.cs` (new)
- `Assets/_Project/Scripts/Testing/VendorFollowupTest.cs.meta` (new)
