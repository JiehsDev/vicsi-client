# Investigation migration to the new house — 2026-10-16

## Implemented scope

The existing `CSI_Environment` investigation now takes place in `Vicsi_House`. `Harong` remains in the saved scene but is inactive, including its lights, colliders and old furniture interactions. Recovery copies of the live pre-migration scene and affected data are retained in `docs/evidence/new-house-investigation/before/`.

BODY-A is on the **rear ground-floor kitchen floor** at approximately `(17.35, 1.326, 12.90)`. The knife retains its position relative to the victim (approximately 1.22 m in plan). The blood pattern is visible beside the victim, rather than hidden underneath. The liquor bottle is upright on the rear kitchen counter. Existing evidence objects and their attached fingerprints, sample targets, measurement anchors, marker acceptance zones and authored chalk guides moved with their parents; evidence IDs, selected-report-line IDs, procedure rules and custody requirements remain intact.

The player arrives outside the new front steps. The First Responder, IOC briefing anchor/NPC, vendor, transport bin, perimeter tape and protected area now serve this house. The cordon remains visibly closed throughout, with its existing nonblocking movement policy. The IOC observation point and yellow standing cue are inside the rear kitchen, facing the victim. The final SOCO survey bounds/look target cover that same kitchen. The existing IOC/SOCO role transition and arrival restoration remain in use.

All seven photography stations and targets moved: exterior, porch/entrance, three living-area overall views, victim-in-scene and rear-kitchen incident view. Stable route IDs/order are retained, including the historical `scene-stair-incident-area` ID whose display and guidance describe the kitchen. The navigation check follows a clear route around the sofa and turns after crossing the kitchen doorway, avoiding the existing open door leaf.

The eight fixed measurement references now identify actual perpendicular kitchen walls/cabinet faces. Selectable hook zones sit in free space just ahead of those surfaces. Expected distances are recomputed from the relocated evidence anchors; requirements, tolerance, evidence-pair roster and stable reference IDs are preserved. Student instructions and displayed names match those surfaces.

## Remade Sketch map

Replaced the old kitchen-only drawing with a rear-kitchen detail for evidence placement plus **ground-floor and upper-floor orientation plans**. Architectural linework is generated from horizontal sections through the actual new-house walls, doors and windows; fixed cabinet/sink footprints and both stair flights are shown. The plans include room labels, north (+world Z), a correctly scaled 1 m detail bar and eight separated measurement-card slots.

`CrimeSceneSketchTemplate.mappedArea` maps world positions into the kitchen detail while preserving the existing full-board mapping when unassigned. Evidence anchors and measurement-line reference feet are rebuilt from actual scene geometry. Anchors have no graphics; undiscovered evidence is not drawn on any plan. Existing unlock, drag/drop, identity-based distance matching, incremental state, submit and read-only behavior are preserved. `SketchPlanGraphic` batches each plan's strokes in one native UI mesh, without generated textures or hundreds of line objects.

## Narrative and panel compatibility

Changed old entry/bottle and table/bottles photography wording to match the kitchen-counter placement and new photography views. Body notes identify the rear ground-floor kitchen. Observation guidance no longer directs the student beside the old TV. Witness timelines, suspect accounts, deduction relationships, conclusion logic and scoring configuration were not rewritten.

The shared panel solver previously called `Collider.ClosestPoint` on non-convex architectural mesh colliders, which Unity rejects. It now uses a supported small-volume eye overlap for those surfaces while retaining primitive containment checks, full rectangle sweeps, endpoint checks and sightlines. A focused UI geometry fixture covers both a blocked path through a non-convex wall and a clear side path without unsupported-call errors.

## Measurement upload compatibility

The dashboard's authoritative measurement analytics contained old-house distances. Added a named **`new-house-v1` geometry profile**, independent of the unchanged 1.7.0 deduction formula. The scene's `MeasurementStateManager` appends `layoutId` to each measurement event through the existing payload dictionary; no DTO field, event type or database schema changes. Repeating the tag on every measurement also survives a capped raw-log prefix.

The sibling dashboard at `C:/Users/Roduf Eleu/Desktop/vicsi-dashboard` registers the eight new expected distances/reference identities under that tag. Untagged historical sessions continue using their historical expected distances with identical outputs. Unknown or mixed tags leave measurement analytics unavailable instead of silently applying the wrong house; client-provided expected distances and accuracy claims remain untrusted. The deduction answer key, scoring version, weights, scenario registration and historical profile values are unchanged. This dashboard change is local source only; no deployment or live database mutation was performed.

Backend source: `src/lib/scoring/measurementScorer.ts`, `types.ts`, `data/csiEnvironment.v1_7.ts`, new `data/csiEnvironment.newHouse.v1.ts`, and `__tests__/newHouseMeasurementLayout.test.ts`. Focused tests cover new-layout accuracy, historical isolation, untrusted client claims, unknown/mixed tags and parity with the actual saved Unity scene. The Unity measurement diagnostic also checks tags on actual logged measurement events.

## Validation

Final accepted results are tracked in `docs/evidence/new-house-investigation/summary.json`: **570 Play-mode checks across 13 suites, 63 layout checks, 42 stair/controller checks and 29 preservation checks, all passing**. The first Play run is retained under `play-run1/`. It passed scene photography, fixed/pair measurements, real tape gestures, cabinet references, body removal, narrative role flow, the full supported story and dialogue catalog. It exposed stale diagnostics using the old observation/survey coordinates and a Sketch fixture that omitted BODY-A's existing final-photo prerequisite; those fixtures are corrected without relaxing runtime gates. The initial walking probe also targeted a route through the sofa/open door; its final route follows the actual clear passage.

The dashboard's five new-layout tests pass, including actual saved-scene geometry parity, historical results, tag handling and rejection of client accuracy claims. TypeScript and scoped ESLint pass. The broader scoring run reports **192 passing and four existing failures**: `lastDrink` detects the historical October 8/9 server-key/export mismatch (including `REL-paul-timeline-contradiction`); `procedureOrder` and `scoringVersion` still expect already-registered 1.7.0 to be unsupported; `trainingSeparation` compares an October 13 source against an older Phase 1 hash. These unrelated historical fixtures/exports were left unchanged. The full output is retained in `backend-scoring.txt`; the full backend suite is not green.

Final saved-scene reload is clean outside Play, with no compilation failure, pending runner, XR restore or request file. Standalone and Android XR initialization are restored to enabled. `Harong` is inactive, `Vicsi_House` is active and the saved measurement tag is `new-house-v1`. The preservation audit confirms all 307 removed records belong to the replaced old Sketch drawing, with 209 added records and no removed records elsewhere.

Layout validation checks evidence placement, fixed-reference geometry, invisible sketch targets, distance-line endpoints, renderable map graphics, cordon bounds, photography floor support/standing space/visibility and continuous movement from arrival through the porch and kitchen door. The preservation audit checks serialized identities and procedure/scoring fields against the recovery copies and verifies that all removed records belong to the replaced Sketch drawing. Rendered kitchen, arrival and Sketch previews are retained alongside reports.

The focused diagnostics run in the existing offline test sandbox, with no live result upload. Physical Quest controller use, room-scale tracking, stereo readability/comfort and frame time still require hardware acceptance. No build/install, commit, push or deployment was performed.

## Persistent implementation

- `Assets/_Project/Editor/NewHouseInvestigationSetup.cs`: undoable migration, layout/narrative configuration and backups.
- `Assets/_Project/Editor/NewHouseSketchSetup.cs`: repeatable geometric map authoring.
- `Assets/_Project/Editor/NewHouseInvestigationValidation.cs`: layout/traversal validation and previews.
- `Assets/_Project/Scripts/Sketch/SketchPlanGraphic.cs` and `CrimeSceneSketchTemplate.cs`: native map rendering and detail projection.
- `Assets/_Project/Scripts/Interaction/HudFollowCamera.Layout.cs`: supported mesh-collider overlap handling.
- `Tools/ValidateNewHouseMigration.py`: repeatable saved-scene/data preservation audit.

Editor menu entries are under **ViCSI / Scene Setup**. Migration refuses duplicate application; the saved scene needs no manual wiring.
