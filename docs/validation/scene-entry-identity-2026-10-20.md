# Scene entry identity dead end

Direct scenario starts deliberately unlock without a student owner when
`assignDirectRunsToTestStudent` is off (or the cached test student is unavailable).
The entrance register previously refused those sessions with
“Confirm your student identity before checking in,” leaving no way to proceed.

The pre-session controller now remembers the specific session it opened without
an owner. The register accepts that controller's permission only in the same
scene and for the same unlocked session. Its local record explicitly identifies
an unassigned session, uses “Unassigned investigator (local session),” and leaves
the student ID unset. No student ownership record is created. Lobby identification,
existing owners, PPE, briefing, boundary tracking and upload eligibility are unchanged.

Validation:

- Runtime C# assembly compilation passed, zero errors (250 existing project warnings).
  Generated project dependencies were supplied from existing Library/ScriptAssemblies
  copies in Temp; no Unity Library assembly was overwritten.
- Seven checks against the compiled controller passed: locked/unidentified refusal,
  unlocked lobby refusal, explicitly unassigned direct session acceptance, different
  session refusal, null/empty refusal, and locked direct session refusal.
- Five corresponding regression assertions were added to SceneAccessRegisterTest.
  The scene suite was not run because a user-owned Unity Editor is open.
- Saved scenario SHA-256 remains
  `e441ee08004b03baf5be2ce53598e8542c4318bbb45ff690cd29429d78cadd3d`.
- Compilation log and executed scope-check results are in
  `docs/evidence/scene-entry-identity/`.

Manual follow-up: restart Play, launch the scenario directly, complete briefing,
put on gloves and confirm entrance check-in. Repeat through the lobby to verify
the confirmed student's name still appears. Headset interaction remains unverified.
