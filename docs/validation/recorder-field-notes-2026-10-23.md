# Recorder field-note wording — 2026-10-23

Rewrote all 24 selectable Recorder statements in first-person investigator language. Supported choices describe the discovery location, relevant observations, or completed documentation/collection actions. The game uses “blood” and “bloodstains” directly. Unsupported choices describe inaccurate locations, methods, or actions instead of obvious accusations and invented death times.

The prompt now reads: “Select three statements that accurately record your examination.” Existing selection controls, exact-three confirmation, separate 24-point assessment, and deduction gates are retained.

## Scene alignment

- Body: face-up kitchen-floor discovery, photographs and position measurements, blood on the shirt near the abdomen. BODY-A requires final photography, measurements, and a sketch before recording; removal follows recording.
- Knife: kitchen-floor discovery, blood on the blade, fingerprint lift from the handle. The existing sequence requires photography, swabbing and on-scene fingerprint recovery before recording.
- Bloodstains: kitchen-floor location near the body, photography before sampling, blood sample collected using a swab. Swabbing requires PhotographedFinal.
- Bottle: kitchen-counter location, intact condition, fingerprint recovery using lifting tape. The bottle is empty and has on-scene fingerprint prerequisites.

## Version compatibility

The original v1 catalog is unchanged. Unity now loads `RecorderDocumentation_v2.json` and captures/logs the catalog version dynamically. Statement IDs, option order and weights remain stable. The local dashboard has an identical v2 catalog and validates both versions against their own exact wording; saved v1 snapshots retain their wording and points. No deployment or live-record modification was performed.

## Writing references

- [Idaho State Police, archived Crime Scene Manual revision 3](https://isp.idaho.gov/wp-content/uploads/Forensics/Methods/Methods/Archive/Post-2012/CrimeScene/Crime-Scene-Manual-Rev-03.pdf), sections 7 and 27: record observations, processing methods and results; its report-writing guidance uses first person and active voice.
- [NIJ, Documentation — Chain of Custody](https://nij.ojp.gov/nij-hosted-online-training-courses/crime-scene-and-dna-basics-forensic-analysts/evidence-crime-scene/documentation-chain-custody): record initial scene conditions and update notes during the investigation.

The sentences are adaptations for this game's authored scene, not quotations or a complete real-world reporting template.

## Validation

- Dashboard Recorder suite: 9 passed, including catalog parity with Unity, v1/v2 historical text, version/text tampering rejection, separate scoring, upload/read/render and legacy records.
- Dashboard TypeScript: passed.
- Unity editor loaded catalog v2 and returned the revised first sentence successfully.
- Unity play-mode Recorder confirmation/upload regression: 60 passed, 0 failed. Accepted report: `docs/evidence/recorder-field-notes-2026-10-23/RecorderDocumentationAccuracyTest-compiled.txt`.
- Initial runs exposed a hardcoded v1 upload-event field and a stale Unity assembly. The field now uses the captured snapshot version; a forced rebuild and the accepted run verify v2 in both the snapshot and event metadata.
- Scoped dashboard ESLint and changed-file whitespace checks: passed.
