# Blood evidence drying wording

Changed the drying station, route guidance, drying status, packaging prompts and paper-wrapper label to casework language. The station now reads **BLOOD EVIDENCE DRYING**; its active instruction reads **Air-drying bloodstained swab — Keep protected until completely dry.** Once ready, the player verifies the source label, collects the paper package, seals it and stores it. Removed simulation/practice language and generic sample wording from these preparation messages. Paper wrappers now read **BLOOD EVIDENCE**.

The existing drying duration and gameplay gates are retained. Source identity, independent drying progress, custody, separate packaging confirmation and sealing/storage requirements are unchanged. Saved scene objects and the editable paper-package prefab remain editable. `BloodEvidenceCaseworkSetup` updates the saved sign without rebuilding props; the runtime builder refreshes its text after reconnecting the saved objects. Internal class, dialogue and event identifiers remain stable.

Validation: **SamplePreparationTest: 47 PASS / 0 FAIL** in the live Unity Editor. Updated its expected drying message and checked all eight preparation messages for unwanted wording. Existing checks cover source identity, text height, physical drying holder, interruption/pause behavior, separate packaging confirmation, sealing/storage, independent sources and scene reset. Code whitespace checks pass. The paper-package expectation in `EditableSceneVisualsTest` was updated to the new label; that separate suite was not rerun for this wording change.

Native [drying](../evidence/blood-drying-wording-2026-10-21/drying-in-progress.png) and [ready-for-packaging](../evidence/blood-drying-wording-2026-10-21/drying-complete.png) renders inspected. Existing nearby foliage partly overlaps the sign in this camera view; this change retains the current scene layout. The sign's text-height check passes. No physical headset test was performed.

Before-scene copy, authoring output, accepted report and summary are in `docs/evidence/blood-drying-wording-2026-10-21/`. No commit, push or external upload.
