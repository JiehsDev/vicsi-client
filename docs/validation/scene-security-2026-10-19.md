# Crime scene security and access register

Implements the approved second chronological audit improvement: briefing, gloves, access check-in, then scene entry.

## Student flow

The First Responder explains their responsibility for the controlled entrance and register, and why limited access reduces disturbance and contamination. After putting on gloves, the student uses the clipboard on the other side of the exterior entrance. A opens the review and a separate A confirms check-in; B or walking away cancels. The register uses the identified session owner's name, current simulated role and phase-derived purpose. It requires no keyboard entry or additional personal information.

Briefing, worn gloves and confirmed registration are all required for entry and IOC observations. The existing SDK-aware entry return remains in use. Tape and posts remain secured. Check-in confirms authorization and shows Awaiting entry; only observed boundary crossings create visits. The register is non-scored.

## Accurate local records

Head and capsule must both settle beyond the perimeter margin for a crossing to count. The footprint is the union of the existing approach and protected area, so moving across their internal join does not create an exit. Re-entry creates another visit under the same authorization, including travel to the vendor. Denied attempts and boundary jitter create no visit.

IOC and SOCO are simulated roles of the same identified student. The role handover and existing SOCO-arrival reposition are labelled transitions. The reposition does not invent a physical exit. Session end or scene unload labels an open visit Present at scenario end and leaves its exit time unobserved. The register fabricates no earlier NPC visits.

Each meaningful change saves an atomic JSON sidecar beside the session log (`<log path>.access.json`). Initial registration requires a successful local save. Session-end finalization happens before the event stream freezes. `SceneLogSigned` records confirmed authorization; new appended access events record visits, role changes, simulation reposition and finalization. Existing `SceneEntered` retains its scene-load meaning and existing enum ordinals are preserved. Upload/dashboard schemas and deduction scoring are unchanged; the complete register is local only.

The clipboard review uses the shared bounded, padded world-space layout and pointer controls. Right stick scrolls visit history. Student names are plain text, with wrapping and measured rows. Held opening A cannot also confirm. Empty hands, range, dialogue and modal guards prevent competing actions. Later development presets explicitly prepare authorization only in the isolated sandbox; normal play requires check-in.

## Validation

Accepted: **452 PASS / 0 FAIL across 11 fresh Unity processes**, all exit 0, with zero C# compilation errors or null/missing-reference exceptions in accepted logs.

| Suite | PASS | FAIL |
| --- | ---: | ---: |
| SceneAccessRegisterTest | 40 | 0 |
| ArrivalEntryTest | 26 | 0 |
| ArrivalPPETest | 42 | 0 |
| FirstResponderTurnoverTest | 44 | 0 |
| IOCAssessmentGuideTest | 35 | 0 |
| NarrativeRoleFlowTest | 41 | 0 |
| NPCApproachGuideTest | 33 | 0 |
| ScenarioPhaseFoundationTest | 19 | 0 |
| SceneTestStartTest | 143 | 0 |
| SessionPersistenceTest | 12 | 0 |
| VendorFollowupTest | 17 | 0 |

The focused register suite exercises actual simulated A/B input, held A, prerequisites, modal/range cancellation, denied SDK movement, successful crossings, jitter, the yard/house union join, vendor exit/re-entry, one identity across role handover, scripted reposition, local JSON round trip, session end and fresh-session reset. ArrivalPPE exercises all seven briefing lines and the real glove/check-in sequence. SceneTestStart covers all 12 isolated presets and return to normal play. The narrative restart fixture explicitly prepares gloves and authorization inside the sandbox.

The saved scenario scene is byte-identical to the pre-change snapshot: SHA-256 `91d5c8c35425cdc32fec5ecb6bbc01ebfb07c368e227d62da5e10fdf12786005`. The scene evidence, photography and measurement geometry therefore remain unchanged. Both Standalone and Android XR startup remain enabled; no runner request remains pending.

Native [review preview](../evidence/scene-security/register-preview.png) inspected: the long test name wraps, header/footer are separated, and visit history scrolls within the bounded panel. The [finalized example](../evidence/scene-security/register-example.json) contains only isolated fake student data. [Accepted manifest](../evidence/scene-security/summary.json) includes per-suite results, paths and current source hashes.

`run1` is retained: hidden TMP rows were measured before their default font initialized, causing a null reference and runner timeout. Explicit font assignment fixed it; `run2` passed the focused suite. `run3` passed all ten regression suites. Accepted logs retain existing Meta SDK/MRUK teardown diagnostics; this is not a claim of warning-free SDK shutdown.

Reproduce with `powershell -NoProfile -ExecutionPolicy Bypass -File Tools/RunSceneSecurityChecks.ps1 -Attempt <new-folder>` while no Editor owns the project. The script uses a fresh process per suite and refuses existing report files. All test ownership, session files and network access run through AutomatedTestSandbox.


Physical Quest controller interaction, readability and boundary comfort remain manual acceptance. No build/install, live upload, commit, push or deployment is part of this change.
