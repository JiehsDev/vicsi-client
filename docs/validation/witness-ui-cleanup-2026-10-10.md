# Witness Accounts / physical vendor access — 2026-10-10

Result: **PASS, 11 focused Editor checks / 0 failures; zero C# compile errors.** Quest visual/controller acceptance remains pending. No full regression suite, commit, push, build/install or deployment was performed for this change.

1. **Stale access source:** CSI_Environment's utility entry called VendorFollowupInterview.Show directly. AskAnyoneElse also lacked a physical-NPC/open-panel prerequisite.
2. **Removal:** Replaced that listener with WitnessAccountsUI.Toggle. Removed public Show; opening now requires the active VendorWitnessInteraction and its existing proximity check. Asking independently checks the open interview, proximity, phase, modal owner and opening frame.
3. **New UI:** WITNESS ACCOUNTS is a grouped, read-only, scrollable projection of existing authored catalog statements and existing availability/discovery state. It stores no testimony or discovery flags.
4. **Utility integration:** Hold left X, point the right stick at Witness Accounts, release X. Right stick scrolls; B closes. Reuses the existing utility wheel, WorldPanelPlacement, PlayerUIGate, ToolInputLock and LocomotionSuspender. Placement is 1.35 m forward and 0.14 m below eye level, without rotating the camera. Panel dimensions are 0.80 × 0.72 m; body font is 30 canvas units.
5. **Paul:** Existing evening/drinking and alibi accounts appear after Form 1 establishes the initial known statements. Knife-handling text retains the existing board prerequisite, including the matched fingerprint result; it cannot appear early.
6. **Peter:** Existing timeline account appears with initial known statements. Appearance/money catalog lines have no independent current discovery record; they remain hidden unless that authored account was actually delivered and exists in Dialogue History. Catalog existence alone is insufficient.
7. **Vendor before follow-up:** Existing Peter-leaving, discovery and limitation accounts appear with the initial known statements: Peter at about 2 AM and his condition, finding John/calling police, and not witnessing the attack. The 3 AM statement is absent.
8. **Vendor afterward:** Existing vendor-paul-3am-summary is appended once when the canonical VendorFollowupInterview.IsDiscovered becomes true. The live panel refreshes through its discovery event; reopening also rebuilds from current state.
9. **Filtering:** Initial board-backed accounts require Form1Received and their existing availability predicate, or actual authored delivery in history. Knife keeps its prerequisite regardless of history. The 3 AM account exclusively reads canonical vendor discovery. Undelivered Peter catalog-only lines remain hidden.
10. **Physical interaction:** Existing roadside NPC and 2.25 m horizontal interaction range remain. A opens its interview; a fresh A selection asks the unchanged question. Leaving range or the investigation phase closes the interview. Opening A cannot also select the question in the same frame. The unchanged answer discovers statement_vendor_paul_3am once per session.
11. **Prompt:** Existing local “Vendor - [A] Talk” prompt is retained and appears only for the valid nearby vendor interaction. No global follow-up availability hint was added. Existing prompt ownership suppresses it during modal UI.
12. **Review-only:** Witness Accounts has no Ask/Question/interview controls or callbacks. Opening and closing it does not discover statements or create dialogue.
13. **Dialogue History:** Retained unchanged as the exact-dialogue view. Witness Accounts resolves only authored account records; history is used only as proof of delivery for those records, without copying every dialogue line.
14. **Deduction:** Statement ID, REL-paul-timeline-contradiction, relationship linking, supported-Paul requirements, Peter path, HYP, evidence state, scoring and workflow are unchanged. EvidenceNode exposes a pure availability accessor; its original visibility evaluation is unchanged.
15. **Reset:** Existing scene/session reload clears canonical vendor discovery, history, card and relationship state. Accounts are empty before renewed turnover, then derive initial known accounts again. No separate review-state reset is required.
16. **Focused checks:** Ran only VendorFollowupTest via the existing Editor file-request runner. Checked utility wiring, initial filtering, remote rejection, proximity prompt, opening-frame consumption, once-only discovery/history, live review update, existing board support constraints/linking, and fresh-session reset. Static layout/input inspection and git diff --check also passed. BrotherPathTest and StoryContentTest setup were adapted to physical interaction but those suites were not run.
17. **Pass/fail:** 11 PASS / 0 FAIL. Evidence: [focused-checks.txt](../evidence/witness-ui-cleanup/focused-checks.txt).
18. **Compilation:** Unity successfully recompiled and executed the focused test with zero C# errors. Final Editor state is playing=False, compiling=False. A second batch Editor was refused because this project was already open; validation used the existing interactive Editor instead.
19. **Files changed for this task:**
    - Assets/_Project/Scenes/Scenarios/CSI_Environment.unity
    - Assets/_Project/Scripts/UI/WitnessAccountsUI.cs and .meta (new)
    - Assets/_Project/Scripts/Dialogue/VendorFollowupInterview.cs
    - Assets/_Project/Scripts/Dialogue/VendorWitnessInteraction.cs
    - Assets/_Project/Scripts/Deduction/EvidenceNode.cs
    - Assets/_Project/Scripts/Testing/VendorFollowupTest.cs
    - Assets/_Project/Scripts/Testing/BrotherPathTest.cs
    - Assets/_Project/Scripts/Testing/StoryContentTest.cs
    - This report, focused-check evidence, taskbycodex.md and VR-CSI_SESSION_HANDOFF.md
20. **Quest pending:** Confirm actual text readability, scroll behavior, wheel selection, B-close/movement restoration, nearby prompt and the two-step A interview behavior. No manual headset acceptance or rendered visual acceptance is claimed. The currently installed 5238dd2 APK predates this change and the preceding IOC UI fix.

Existing IOC ergonomics changes in the working tree were preserved; they are a separate task. No dialogue catalog, relationship definition, scoring, backend or upload-schema asset was changed.
