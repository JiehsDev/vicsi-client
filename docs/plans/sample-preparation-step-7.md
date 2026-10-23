# Sample preparation and packaging

Approved by the user on 2026-10-19, including a filling circular progress indicator.

Status: implemented and validated; 528 accepted checks passed, zero failed. Physical Quest validation remains pending. See `docs/validation/sample-preparation-2026-10-19.md`.

- Add a protected, single-sample preparation cabinet beside (separate from) clean supplies.
- Keep the current container as a temporary carrier. Guide the student outside after containment.
- A transfers the carrier into the cabinet. A five-second circular fill explicitly represents simulated drying, not actual drying duration.
- Pause on modal interruptions or leaving interaction range; retain source, sample and progress. No automatic sealing or custody credit.
- Show source identity; a separate A confirms the label and retrieves an individual paper swab package. Existing trigger seal / B seal-and-store follows.
- Block early sealing/storage and replacement supplies. Reuse the existing authored exit route and waypoint, with preparation taking priority.
- Validate real XR input, progress, interruption/recovery, source isolation, packaging, storage and scene reset; rerun affected biological and supplies regressions. Preserve scene/evidence assets and offline test isolation.

Procedure reference: NIJ Packaging & Storage recommends drying biological evidence before packaging, generally in paper: https://nij.ojp.gov/nij-hosted-online-training-courses/crime-scene-and-dna-basics-forensic-analysts/evidence-crime-scene/packaging-storage

Implementation detail: the cabinet is a child of the runtime supply station at local (1.55, 0, 0), following SceneCordon placement. Both its standing approach and the cabinet are checked outside the security footprint; the original gate/scene geometry is preserved.
