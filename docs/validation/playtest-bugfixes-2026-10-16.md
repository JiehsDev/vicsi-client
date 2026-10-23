# Play-test fixes — 2026-10-16

Requested order: duplicate fingerprint question, bottle discovery/collection reach, missing Sketch hint. Fixes are implemented and verified sequentially.

## 1. Repeated fingerprint question

The integrated RC-03/HYP-02 flow presented a contextual fingerprint interpretation, then a legacy justification asking essentially the same thing. The contextual answers already contain both interpretation and justification. They now require one explicit selection/confirmation and close the panel after the response is durably recorded. Non-contextual hypothesis checkpoints retain their existing separate theory/justification stages.

RC-03 response IDs, authored weights, eligibility, deferred teaching and vendor prerequisites remain unchanged. HYP-02 emits one completion event linked to the explicit contextual response; no unselected legacy justification is invented. Legacy justification fields are empty, and generic payload fields link the contextual checkpoint/response. Current contextual scoring continues to use the original RC-03 response. Answered integrated entries are skipped if they become stale while queued, preventing replays and queue stalls.

Verified first before starting fix 2: ReasoningCheckpointTest **62 PASS**, FingerprintStorageTest **33 PASS**, zero failures. Verification reports are retained under `docs/evidence/playtest-bugfixes/question/`.

## 2. Bottle discovery reach

The bottle root is scaled to 0.15, so its runtime SphereCollider was shrinking the authored 1.5 m interaction radius to 0.225 m. The notice collider now compensates for the largest absolute parent/model scale, keeping the authored radius in world metres, including non-uniform imports. Physical bottle size, solid collider and grab-reach limits are unchanged.

OnTriggerStay retries discovery after investigation unlocks if a player was already overlapping during IOC assessment. The same external-trigger and phase guards remain, and the authoritative evidence record suppresses duplicate Found transitions. A focused physics test approaches the actual bottle from a metre in front of the counter, verifies IOC blocking and discovery on unlock, then checks stay/reentry, latent-print exclusion and a non-uniform rotated fixture.

Verified before starting fix 3: EvidenceDiscoveryTest **10 PASS**, GrabReachTest **9 PASS**, zero failures. Existing 0.15 m grab reach is retained, including refusal from 2 m/the entrance and the left hand. Reports: `docs/evidence/playtest-bugfixes/bottle/play-run1/`.

## 3. Sketch placement hint visibility

The hint was a translucent soft disc, shown only on the next drag after two wrong attempts and destroyed when the card was returned. It now gains an opaque gold ring with a dark contrasting border at the actual accepted snap radius, appears immediately after the configured wrong-drop threshold and remains visible while the card returns and awaits its next drag. The threshold and snap radius are unchanged. A persistent gold instruction identifies the highlighted area, with larger footer text. Wrong-drop messages remain uniform; only the current eligible card's area is highlighted. Placement, cancellation, switching to another card and closing clear the hint appropriately. Hint usage is still logged once per evidence item.

The existing Sketch diagnostic now checks persistence after the second wrong drop, one-time assistance logging, non-overflowing visible instruction text and actual gold pixels rendered at the correct target. A complete board screenshot is retained under `docs/evidence/playtest-bugfixes/sketch/placement-hint.png`.

Verified: CrimeSceneSketchTest **84 PASS**, zero failures, including 838 rendered gold pixels at the intended target and a visible, non-overflowing instruction. The screenshot was visually inspected. Placement/measurement-card validation, one-time hint logging, cancellation, submission, read-only behavior and reset continue to pass.

## Final state

**198 focused checks pass / zero failures**, with each reported bug fixed and verified before moving to the next. The saved scene and scenario assets were not edited. Existing body/knife/bottle placements, measurement references, chalk outline and photo route remain authored as before this task. No scoring definitions or answer keys changed; the fingerprint contextual response remains the authoritative recorded answer, with one linked HYP-02 completion and no fabricated legacy justification.

Editor is clean outside Play, with no compilation failure, pending runner, request or XR restore. Standalone/Android XR initialization is restored. Manifest and final Editor state: `docs/evidence/playtest-bugfixes/summary.json` and `final-editor-state.json`. Existing serializer warnings and SDK/bridge diagnostics are unchanged. Physical Quest verification remains separate from the Editor diagnostics. No build/install, live upload, database mutation, commit, push or deployment was performed.
