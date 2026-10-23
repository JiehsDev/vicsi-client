# Fingerprint comparison tutorial — 2026-10-19

Implemented the approved outside examination desk near the evidence submission crate, separate from the entrance, clean supplies and drying cabinet. It has a monitor, a labelled packaged-lift tray and a training-station sign. Runtime position in CSI_Environment: `(20.2, 0, -1.2)`; saved scene geometry is unchanged.

A source becomes available after its fingerprint lift is explicitly stored and secured in the existing inventory. The shared waypoint guides the student outside, with preparation and supply needs retaining priority. The route goes around the existing outside tables. A opens the monitor with empty hands; the student selects among three references and can independently zoom/rotate both prints, request a ridge-ending/split hint and confirm correspondence. Incorrect guesses explain a visible difference and permit immediate unlimited retries. There is no timer or score penalty. Success highlights corresponding details and explains that real identification requires a trained examiner and verification.

The diagrams are authored illustrations, not real biometric records or reproductions of a PNP interface. Tutorial completion is separate from case findings: the existing fingerprint results, including the bottle's multiple identities, are unchanged. The tray is a labelled station prop; opening practice reads the stored sample's association without transferring or unpackaging evidence. Completed samples can be replayed, and the next-sample control enables when another stored lift is available. Completion and attempts are tracked independently by source and reset with the scene.

Controller input: A opens/confirms, right stick selects a reference, controller ray plus trigger operates hint/zoom/rotation/sample controls, and B closes. Held opening gestures cannot submit an answer. Modal interruption, range loss, disable and scene reload close safely and release tool input and locomotion ownership. Two append-only local tutorial event types were added; completion carries `scored=false`.

Accepted validation: **396 PASS, 0 FAIL** across nine fresh offline Unity processes, each exit 0.

| Suite | Passed | Run |
| --- | ---: | --- |
| FingerprintComparisonTest | 53 | run4 |
| FingerprintStorageTest | 33 | run5 |
| BottleFingerprintProcedureTest | 40 | run5 |
| SamplePreparationTest | 46 | run5 |
| GloveChangeStationTest | 53 | run5 |
| EvidenceInventoryTest | 29 | run5 |
| ArrivalPPETest | 42 | run5 |
| SceneAccessRegisterTest | 40 | run5 |
| DialogueCatalogTest | 60 | run5 |

The focused suite uses actual XR A/B/stick/trigger input and controller-ray targets, checks thirty additional wrong guesses without lockout, tests two independently lifted/stored sample sources, verifies score/case/custody preservation and exercises replay, closure and reset. Twelve walking segments are traversed with the Meta player capsule. Standing-boundary clearance and absence of solid geometry intersecting the monitor are checked. All practice text fits, and a rendered-pixel check verifies that scene overlays do not obscure the print area.

Native 1280×960 renders inspected:

- [Examination station](../evidence/fingerprint-comparison/run4/outside-comparison-desk.png)
- [Comparison and optional hints](../evidence/fingerprint-comparison/run4/comparison-hint.png)
- [Corresponding details and completion guidance](../evidence/fingerprint-comparison/run4/comparison-complete.png)

Earlier attempts remain available. They caught narrow reference labels, the need for more fence clearance, a bottle fixture missing its measurement prerequisites and a sample-switch click inside the pointer's opening guard. The monitor canvas now uses the existing review-panel sorting convention to keep other overlays off the diagram. The legacy fingerprint-storage report's success marker was normalized for the batch runner; its gameplay assertions are unchanged.

Preservation: six existing files changed in the 491-file baseline; the other 485 remain hash-identical. Seventeen dialogue entries were appended with previous catalog bytes preserved. The saved scene remains SHA256 `91d5c8c35425cdc32fec5ecb6bbc01ebfb07c368e227d62da5e10fdf12786005`. Evidence definitions, inventory/state managers, case findings, scoring, measurement data, tool registry, ruler/capture implementation and transport behavior are unchanged. Legacy test previews and the bottle upload-wire example were restored byte-for-byte; new versions are archived in this feature's evidence directory.

Accepted logs contain no C#/shader errors, null/missing-reference exceptions or new missing-dialogue warnings. The catalog fixture retains its existing missing-ID baseline; existing SDK/licensing/teardown diagnostics remain. No Unity process or pending runner request remains. XR startup remains enabled and scoped XR git status is clean.

Physical Quest controller reach, stereo readability and device performance remain untested. No build/install, live upload, commit, push or deployment was performed.

[Results and source hashes](../evidence/fingerprint-comparison/summary.json) · [Approved plan](../plans/fingerprint-comparison-step-8.md)
