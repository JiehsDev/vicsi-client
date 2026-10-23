# ViCSI engineering report — audit and foundation repairs

Date: 2026-09-14 (Asia/Taipei). Engineer/tester: Codex. Project: `C:/Users/Roduf Eleu/My project (1)`. This session delivers the initial audit, traceability/test plans, a broken asset-reference repair, evidence-boundary fixes and tested local persistence improvements. **The capstone is not complete.**

## 1. Overall completion

**2.45% requirement verification: 4 of 163 requirements marked Implemented with bounded test evidence.** There are 82 Partial and 77 Missing requirements. This conservative figure measures verified requirements, not engineering effort or amount of existing code. End-to-end release acceptance is still outstanding. The matrix contains 159 enumerated functional requirements plus four normative prose requirements, including configurable unapproved weights and database migration obligations.

Verified requirements: **B-07** ordinary numbered marker invariant, **B-08** body letter marker invariant, **B-09** body non-collection invariant, **D-01** local session ID persistence. Broader controller, complete-procedure, complete-assessment and online requirements remain Partial/Missing.

## 2. Files and objects inspected

`docs/evidence/repository-inspection.json` records 146 first-party source/configuration entries with hashes, 10 project scene/prefab records with script bindings, evidence definitions, input assets, placeholder locations and reference checks. This is a repository inventory plus targeted semantic review, not exhaustive semantic inspection of every third-party asset.

Targeted review included:

- `ProjectSettings/ProjectVersion.txt`, `Packages/manifest.json`, `ProjectSettings/EditorBuildSettings.asset`, XR/input configurations.
- Enabled scenes `Assets/_Project/Scenes/Main.unity` and `Assets/_Project/Scenes/Scenarios/CSI_Environment.unity`; source/bindings inventory also includes `Scenario_Basic01_Indoor`, `Tutorial_ToolTest`, `HeacyASF`.
- `Scripts/CaseFile/EvidenceDefinition.cs`, `EvidenceRecord.cs`, `EvidenceStateManager.cs`, `SelectionPrerequisite.cs`; all six `Data/Evidence` assets (BODY-A, EVD-014/015/017/018/019).
- `Scripts/ProceduralGate/ProceduralGateValidator.cs`; photograph, tent, letter tent, recorder, swab, fingerprint, bag and grab tool implementations.
- `Scripts/RoleSystem/ToolWheelController.cs`, `PlayerToolRegistry.cs`, `ToolType.cs`; prepared-choice UI and feedback paths.
- `Scripts/Logging/SessionLogger.cs`, `LocalJsonLogWriter.cs`, `SessionEvent.cs`, `SessionEventType.cs`, `ISessionLogWriter.cs`.
- `Scripts/Requirements/RequirementEvaluator.cs`, relationship/objective/narrative systems, deduction board/scorer, ending manager/set and completion UI; authored scenario relationship/ending assets and existing diagnostics.
- `Tools/EvidenceScorer/Program.cs`, its `.csproj`, exported ground truth; project prefab references including `Prefabs/Interaction/FlashlightTool.prefab`.

Paths beginning `Scripts/`, `Data/` and `Prefabs/` above are relative to `Assets/_Project/`. Pre-existing working-tree changes were preserved; baseline inventory is `docs/evidence/git-status-before.txt` (taken before functional edits, after adding the audit script).

## 3. Features confirmed by execution

- Evidence state boundary: wrong marker types/labels, marker reuse, collection prerequisite skips, body collection/seal refusal, safe duplicate markers/seals, ordinary collection/sealing, existing swab and on-scene-print gates.
- Local JSONL: immediate start persistence, UTC start/end payloads, ordered event sequence/session ID, pause flush, idempotent ending, retry after an actual filesystem access failure without duplicating the buffered event.
- Existing requirement evaluator, relationship state and objective state diagnostic suites.
- Existing legacy greybox evidence flow and story-content diagnostic: narrative beats, objectives, relationship states, wife/brother/inconclusive resolution and completion screen. This test directly drives state and the existing suspect picker; it does **not** verify a player-operable deduction board or the newly specified procedure order. Premature conclusion is not exercised live by this suite.
- Offline identification metrics: known recall/precision, deterministic results, duplicate identification handling, explicit unavailable denominator, rejection of absent lifecycle export and empty events.

## 4. Implemented this session

1. Created `VICSI_IMPLEMENTATION_AUDIT.md`, `VICSI_REQUIREMENTS_TRACEABILITY_MATRIX.md`, `VICSI_TEST_REPORT.md`, this report, and reproducible audit/test tooling under `Tools/Audit/`.
2. Repaired flashlight prefab mesh/material references using the already imported `Art/Environments/MODELS/Low poly v2/UV_LIGHT_30K.glb`. Initial absent GUID replaced with actual imported subasset IDs; final project reference scan is clear. Unity also serialized seven existing default `EvidenceProp` fields while saving this prefab.
3. Hardened `EvidenceStateManager`: null-safe lookup, invalid/duplicate definition diagnostics, no silent duplicate-definition overwrite, singleton cleanup, marker-kind/identity validation, explicit body terminal-state checks and rejection of invalid lifecycle values. Marker identity is assigned before status-change callbacks so event logging can capture it.
4. Added explicit body block reason in `ProceduralGateValidator`; wrong markers log `EvidenceMarkerRejected` with a reason. Updated the existing story test to use the real letter-marker API for BODY-A.
5. Improved `SessionLogger`: immediate start flush, pause flush, scene-destruction end/flush, UTC start/end payloads, marker/tool data and retention of retry thresholds following a failed local flush.
6. Improved `LocalJsonLogWriter`: GUID-only filename identity, one-session initialization, durable append, buffered-event retention on recoverable filesystem errors, rollback to previously persisted prefix, and stopped automatic retry if rollback itself fails. This does not claim power-loss recovery or durable upload synchronization.
7. Added `EvidenceBoundaryTest.cs` and `SessionPersistenceTest.cs` with Unity-generated `.meta` files, plus executable .NET scorer fixtures in `Tools/Audit/scorer.test.mjs`.
8. Added `.env` credential-file exclusions to `.gitignore`, retaining example templates.

## 5. Features still partial

Legacy evidence order differs from the specification: chalk precedes marker; observations follow type-specific steps; applicable-only chalking is not modeled. Knife blood and prints are parent flags rather than independent findings. Some fingerprint work occurs after sealing and uses hardcoded lab-match stubs. Prepared-note IDs are trusted at the state boundary. Full duplicate/attempt logging, student/attempt metadata, explicit completed/interrupted classification, reproducible complete assessment and result UI are incomplete. Quest operation is unverified. Seven mirrored curtain BoxColliders remain invalid under negative scale.

## 6. Features still missing

Additional content audit (`Tools/Audit/inspect-content.mjs`, `docs/evidence/content-consistency.json`) found seven Main-scene button callbacks to missing `UIManager.ShowRoleSelect`: Developers, Scores, Quit, Volume, SnapTurn, Subtitles and Back. Start is wired to the valid `OnStartPressed` method. These broken callbacks are not detected by GUID or missing-MonoBehaviour scans. The exported ground truth also omits the newly authored `phone-message-affair` note; BODY-A note omission is an intentional limitation of the current exporter. Both need correction before complete assessment/menu acceptance.

Usable deduction board in the active scene; relationship-derived conclusion flow without suspect picker; independently marked knife-blood finding; complete configurable assessment/grade policy; Supabase schema/migrations/RLS; authenticated Node API; safe offline-to-online synchronization; Next.js student, instructor and administrator dashboards. Remote infrastructure outside this repository was not inspected. The matrix gives an action for every Missing item.

## 7. Tests and exact results

All executions below used the local workstation on 2026-09-14; tester Codex.

| Test ID | Suite / command | Result | Evidence |
|---|---|---|---|
| UT-01 | `EvidenceBoundaryTest.RunAllTests()` in Unity Play mode | **28 passed, 0 failed** | `docs/evidence/unity-boundary-persistence.txt` |
| UT-02 | `SessionPersistenceTest.RunAllTests()` in Unity Play mode | **12 passed, 0 failed** | same artifact |
| UT-03 | `RequirementEvaluatorTest.RunAllTests()` | **24 passed, 0 failed** | `docs/evidence/unity-component-regressions.txt` |
| UT-04 | `RelationshipStateManagerTest.RunAllTests()` | **14 passed, 0 failed** | same artifact; includes legacy locked-selection behavior, not approval of it |
| UT-05 | `ObjectiveStateManagerTest.RunAllTests()` | **13 passed, 0 failed** | same artifact |
| UT-06 | `StoryContentTest.RunAllTests()` | **33 passed, 0 failed**, plus embedded legacy greybox flow PASS | `docs/evidence/unity-story-content.txt` |
| CLI-01 | `node --test Tools/Audit/scorer.test.mjs` | **6 passed, 0 failed** | `docs/evidence/scorer-tests.tap` |
| SCAN-01 | `node Tools/Audit/inspect.mjs` | **0 unresolved project GUIDs; 0 credential-pattern candidates** | repository inspection JSON; limited-scope scan |
| BUILD-01 | `dotnet build Tools/EvidenceScorer/EvidenceScorer.csproj --nologo` | **Build succeeded; 0 warnings, 0 errors** | initial command execution; CLI tests execute resulting net8.0 assembly |
| BUILD-02 | Unity Android development build, both enabled scenes, `Builds/ViCSI-audit-20260914.apk` | **Build succeeded; 0 errors, 1002 warnings** | `docs/evidence/android-build-result.json`; warning details not retained in inspected Editor log |

Total final assertions: **124 Unity + 6 CLI = 130 passed, 0 failed**. Embedded greybox assertions are not double-counted. The first CLI run had 4 passes and 2 test-expectation failures: precision was expected as unrounded 2/3 while the documented CLI returns 0.6667. Corrected the fixture expectations after checking `Program.Ratio`; rerun passed 6/6. No scoring behavior or weights were changed to make tests pass.

The 29 required acceptance cases, steps, expected/actual result, environment, tester/date, defects and corrective actions are in `VICSI_TEST_REPORT.md` and `docs/evidence/acceptance-test-plan.json`. End-to-end acceptance remains NOT RUN where only component evidence exists.

## 8. Build status

Unity **6000.5.6f1** reports `scriptCompilationFailed=False` after the repairs and successfully loads both new test types. The loaded CSI scene has no missing MonoBehaviour scripts. .NET target **net8.0** is preserved; SDK **10.0.302** and installed .NET **8.0.10** runtime executed the scorer.

Fresh Android development build completed using both enabled scenes, output `Builds/ViCSI-audit-20260914.apk`, job `build-a6b80b396a`. Unity reported **Build Succeeded**, **0 errors** and **1002 warnings**. The APK is 710,050,618 bytes with SHA-256 `9e8d69b674ffc996213ce327db647bb61d26de95abf7fae495423647036726ff`; the BuildReport total size was 4400.59 MB. The warning count is material and must be triaged before release. The individual warning messages were not retained in the inspected Editor log window, so this report does not claim a warning category breakdown. The old `Builds/VR-CSI.apk` predates this session and is not release evidence.

## 9. Meta Quest 3S status

Unity Android SDK `adb devices -l` returned an empty device list. No headset installation, real controller/haptic test, frame-rate trace, thermal run or loading-time acceptance executed. The XR packages match the requested stack. Editor Play mode is not a substitute for Quest testing.

## 10. Security status

Narrow filesystem secret-pattern scan: zero candidates. This is not an exhaustive secret scan or Git-history audit. Ignored local Unity editor logs contain authentication material; they were not copied into repository reports and must stay excluded. `.env` files are now excluded. No Node authorization, Supabase RLS, role escalation, upload tamper/idempotency or dashboard access tests are possible against a repository implementation because those components are missing. Offline scorer metrics are not a validated authoritative server-scoring API.

## 11–13. Database, API and dashboards

- **Database migrations:** none authored or applied this session; no Supabase migration/schema/policy files found. Remote schema unverified.
- **API:** no Node package/routes present or implemented; no server deployed. Authentication, roles, validation, canonical score verification and idempotent uploads remain required.
- **Dashboards:** no Next.js application found or implemented. Student, instructor and administrator views remain required after the secure API.

## 14. Researcher/external inputs needed

Approve the scenario answer key and scoring dimensions, weights, grade thresholds and time policy; no official values have been invented. Supply the intended Supabase/deployment environment when backend integration begins. Provide physical Quest 3S availability and a named human UAT tester, plus approved performance targets. These inputs do not block further local implementation.

## 15. Exact next implementation task

Triage the 1002 Android build warnings, repair Main-scene persistent callbacks according to each button's purpose (Back can use existing `OnBackFromSettingsPressed`), regenerate the stale phone answer key, and repair the seven mirrored curtain collider configurations recorded in `docs/evidence/unity-scene-health.txt`, verifying equivalent collision bounds and player movement. Continue with a coordinated evidence-model migration: independent parent/finding identities and marker targeting, optional tracing, marker-before-chalk and prepared notes before type-specific processing; migrate the scenario assets, tools, answer-key export and regression fixtures together. Only then advance through deduction, configurable assessment, device interactions, Supabase, API, synchronization and role dashboards in the requested dependency order.

Known limitation: the initial repository audit is complete as an inventory/targeted audit, but the requested complete capstone and its definition of done have not been achieved. This report must not be submitted as release approval.
