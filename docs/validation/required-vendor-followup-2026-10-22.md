# Required vendor follow-up after knife fingerprint comparison

The existing vendor question previously required both the confirmed knife-print finding and a response to Reasoning Question 3. It was not a universal completion requirement. The vendor already had a physical interaction panel and ground-arrow route.

## Updated behavior

- A confirmed fingerprint comparison identifying Paul on EVD-014 immediately unlocks the existing vendor follow-up. A lifted/unmatched print, wrong comparison, or another evidence item's print cannot trigger it.
- The player receives a brief factual instruction and a labelled vendor destination waypoint. The existing ground-arrow route follows the same earlier unlock.
- Fingerprint-card storage, blood-sample completion, and glove/supply actions retain their priority. Navigation hides during modal interactions and photography capture.
- The waypoint remains required until the vendor delivers the answer. Opening/closing the interview without asking does not complete it.
- Final conclusion opening, confirmation, and completion all refuse while the answer is pending. SOCO closeout and final investigation completion independently enforce the same requirement.
- The existing question, witness answer, statement ID, history, statement-discovery event, and relationship unlock are reused. Starting a new investigation resets the interview.
- Scene Test Start's fingerprint-comparison preset leaves the vendor answer undiscovered so the new continuation can be tested after comparison. Later completed-procedure presets retain their existing preparation.
- Deduction weights, Recorder documentation scoring, and conclusion resolution rules are unchanged. This is a required procedure, with normal walking and tools still available.

Validation reports: `docs/evidence/required-vendor-followup-2026-10-22/`.

Initial vendor-suite failures exposed test setup issues: the new completion-gate subtest advanced scene photography and moved the camera before the old physical interview test; the route test invoked a private shutter handler on a listener without its serialized tool. The completion-gate subtest now runs after the physical interaction/lifecycle assertions, and the route test uses the public scene-photo API while checking both navigation visuals are excluded from the render. Initial reports are retained.

The route fixture also now completes actual scene photography before seeding a matched knife print, so its walking probe respects the current entrance barrier. It clears unrelated queued reasoning dialogue before checking navigation and checks evidence transforms synchronously around navigation refreshes, separating those writes from ordinary physics settling during the longer dialogue test.

The route ends at an approach position within the vendor's existing 2.25 m interaction radius. A collider audit found that the old direct approach crossed `Neighborhood_Exterior/Property fence boundary`. The route now continues to the sidewalk at (16.9, 0.03, -0.65), then approaches the vendor at (14.1, 0.03, -0.65), staying outside that fence. The walking probe verifies arrival within talking range; intermediate doorway/exit points retain their precise arrival checks. The read-only obstacle audit is retained as `vendor-route-obstacles.txt`; its temporary Editor helper was removed.

## Accepted validation

| Suite | Passed | Failed |
| --- | ---: | ---: |
| VendorFollowupTest | 24 | 0 |
| VendorPathGuideTest | 44 | 0 |
| FingerprintComparisonTest | 66 | 0 |
| InconclusivePathTest | 16 | 0 |
| ReasoningCheckpointTest | 62 | 0 |
| ProcedureDestinationGuideTest | 18 | 0 |
| SceneTestStartTest | 210 | 0 |
| Total | 440 | 0 |

The actual walking probe reached (14.100, 0.035, -0.650), 1.899 m horizontally from the vendor, within his unchanged 2.25 m interaction radius. The photo render excluded both navigation visuals, the delivered answer retired the destination, and scene restart reset the lead. Unity compiled with zero C# errors. Checks ran in Unity with simulated controller input and the SDK player capsule; a headset playthrough was not performed.

`summary.json` identifies the accepted report for each suite. Earlier unsuccessful fixture/route reports remain available for the audit trail. No commits, deployment, or production-record updates were performed.
