# References synchronized to user placements — 2026-10-16

Preserved the user's knife `(18.627, 1.205983, 12.885)`, blood pattern `(16.823, 1.204, 12.647)` and IOC standing point `(15.5927, 1.2, 11.96078)`, including the IOC's authored rotation. A snapshot of the live scene, including unsaved user edits, is retained in `docs/evidence/new-house-reposition/before/`.

Knife references now use the east kitchen wall and the exposed rear kitchen wall beyond the cabinet end. Blood-pattern references use the rear cabinet front and west kitchen wall. Surface positions are sampled from actual architectural colliders. Hook patches face into the room, remain at reachable heights and are separated from the victim's patches. Reference IDs, evidence IDs, tolerances and required procedure gates are unchanged.

Recomputed expected floor-plan distances:

| Evidence | Reference A | Reference B |
| --- | ---: | ---: |
| Victim | 2.067 m | 1.171 m |
| Knife | 0.789829 m | 1.747010 m |
| Blood pattern | 1.434996 m | 1.758171 m |
| Bottle | 0.332 m | 0.594 m |

Updated the existing Sketch evidence targets, perpendicular measurement-line feet and relationship labels in place. Both floor plans and the kitchen drawing are preserved. Child measurement anchors, fingerprint/blood targets, chalk geometry and the IOC highlight already follow their parent objects; the highlight stays grounded and retains the user's facing. Existing kitchen survey bounds/look target and photography placements remain valid.

`NewHouseInvestigationSetup.RefineLayout()` now calls the new **Synchronize Moved Kitchen Evidence** menu operation, which preserves evidence placements rather than resetting the blood pattern to its first migration position. The operation updates measurements and Sketch without rebuilding scene objects.

New events use `new-house-v2`. The local sibling dashboard registers a separate immutable v2 geometry profile; v1 and untagged historical sessions retain their previous distances. Six focused backend tests pass, including saved Unity geometry parity, old-session compatibility, mixed-profile handling and rejection of client accuracy claims. TypeScript and scoped ESLint pass. No backend deployment or live database change was performed.

Validation reports are retained in `docs/evidence/new-house-reposition/`. Layout validation passes 62 checks, including all seven photography stations, expected geometry, invisible Sketch targets/line endpoints and actual-controller traversal to the moved IOC point. Preservation audit passes 23 checks: no added/removed scene records, exact user placements/prefab overrides, stable identities and unchanged tolerances. Five Play-mode suites pass **195 checks / zero failures**: scenario observation 19, per-evidence measurements/logging 34, reference selection 25, Sketch 80 and tape tool 37. Play-mode results and final Editor state are recorded in `summary.json`.

Physical Quest tracking/input, stereo readability and comfort remain hardware acceptance work. No build/install, upload, commit, push or deployment was performed.
