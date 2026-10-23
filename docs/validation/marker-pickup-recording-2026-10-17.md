# Marker pickup after recording

Pickup uses a procedural window, rather than a countdown in seconds:

- A tent that applied no marking transition is retrievable immediately, including an extra tent beside evidence that another marker already marked.
- The specific numbered item marker or trace marker that successfully applied its marking transition stays locked until that evidence reaches Logged (recording complete) or later. Marking, final photography and swabbing alone do not unlock it.
- Body Marker A follows the same rule after required body recording, as confirmed by the user.

The placement code already returns an evidence ID only for TransitionResult.Applied. EvidenceTentPickup retains that placement-specific link and checks its evidence's recorded state; it never infers ownership from proximity or from some other marker. Unused tents therefore do not inherit another marker's lock.

The pickup hint and action use the same eligibility check. Nearest-tent arbitration skips locked markers, so they do not hide nearby eligible tents. A direct pickup call also respects the recording lock. A successful cleanup logs MarkerReclaimed exactly once, returns its number/letter and removes the physical marker. It preserves status, report lines and documented marker references. Original marking events remain in the session log. Legacy state-rewind APIs remain available for compatibility but are not called by player pickup.

Body Marker A is now given the same pickup component when LetterTentTool places it. Its removal preserves the documented A reference, required findings and body-removal eligibility. While an eligible tent is within reach, its B pickup hint takes precedence over the body's A removal hint; the removal hint returns after cleanup.

The existing `tent-pickup-already-documented` dialogue ID is retained with revised guidance: “Record the evidence before picking up its marker.” No assessment weights, case findings, custody rules, upload schema or dashboard structures change. Procedures remain non-scored Training Analytics. No scene wiring, commit, push or deployment is required or performed.

Regression fixtures cover real numbered/trace placement, unused extras, individual locks, nearest-candidate selection, actual hint visibility, modal deferral, no early pickup, swabbing not bypassing recording, recorded cleanup, duplicate-action protection, pool exhaustion/recovery, real letter placement and the full Recorder/body-removal/restart flow. Accepted results are recorded under `docs/evidence/marker-pickup-recording/`.

The initial numbered and trace fixture run omitted the bottle/knife's existing on-scene fingerprint prerequisite, so MarkLogged correctly refused to advance and their expected post-recording pickup assertions failed. The fixtures now complete fingerprint processing before recording and explicitly verify that fingerprinting alone also leaves the markers locked. Production recording prerequisites were not relaxed. The first supported body run passes all 49 checks, including the new letter-marker pickup and body-hint handoff.

Accepted results: **91 passed, zero failures**: EvidenceTentPickupTest 21 (run2), EvidenceTraceMarkerTest 21 (run2), BodyRemovalTest 49 (run1). Unity reports `scriptCompilationFailed=false` and zero console entries matching `error CS`. Final Editor is outside Play with clean saved CSI_Environment, no pending diagnostic request/XR restoration, and Standalone/Android XR startup enabled. The imported catalog resolves the revised pickup guidance correctly. [Validation manifest](../evidence/marker-pickup-recording/summary.json).

Physical Quest proximity/hint comfort remains manual acceptance.
