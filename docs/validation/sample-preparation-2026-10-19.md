# Sample preparation and packaging — 2026-10-19

Implemented the approved preparation flow, including the requested filling circle. A separate protected cabinet sits outside beside the supply station. Its standing approach stays outside the controlled scene boundary.

After containment, the existing waypoint leads to preparation. A places the source-specific sample in the cabinet. The blood-bearing swab appears in the drying holder while a clockwise circle fills over five seconds. The sign explicitly says **“Drying time simulated”** and explains that real drying takes longer. Walking away, modal panels and spoken guidance pause progress without losing the sample. Spoken guidance can finish normally, then drying resumes.

A separate A press confirms the displayed source label and retrieves an individual paper package. Holding the first A never automatically packages it. Trigger sealing and B seal-and-store remain blocked until preparation is complete. The same inventory item, source identity and custody remain authoritative; packaging does not grant clean gloves or a fresh swab. No extra Tool Wheel slot was added. The grey used-swab slot now directs the student to preparation when needed.

The procedure follows [NIJ Packaging & Storage guidance](https://nij.ojp.gov/nij-hosted-online-training-courses/crime-scene-and-dna-basics-forensic-analysts/evidence-crime-scene/packaging-storage), which recommends drying biological evidence before packaging and generally prefers paper. The five-second fill is explicitly a teaching simulation.

Accepted verification: **528 PASS, 0 FAIL** across nine fresh, isolated Unity Editor processes, each exiting 0.

| Suite | Passed | Accepted run |
| --- | ---: | --- |
| SamplePreparationTest | 46 | run7 |
| GloveChangeStationTest | 53 | run7 |
| FreshSwabSupplyTest | 52 | run7 |
| BiologicalSamplingTest | 63 | run10 |
| EvidenceInventoryTest | 29 | run10 |
| ArrivalPPETest | 42 | run10 |
| SceneAccessRegisterTest | 40 | run10 |
| SceneTestStartTest | 143 | run10 |
| DialogueCatalogTest | 60 | run10 |

The focused suite drives actual XR A input and the full real-time fill, checks rendered cyan pixels, tests held-button protection and interruption recovery, and traverses nine route segments with the Meta player capsule. It checks two independent sample sources and scene reset. Legacy synchronous fixtures use the real cabinet actions with explicit sandbox-only time advancement; the glove fixture observes the real preparation trip and boundary settling. Existing fresh-swab tests verify actual B storage after preparation.

Native Editor renders inspected:

- [Partial circular fill](../evidence/sample-preparation/run7/drying-in-progress.png)
- [Completed fill and label confirmation](../evidence/sample-preparation/run7/drying-complete.png)
- [Used-swab wheel guidance](../evidence/sample-preparation/run7/wheel-contained-sample.png)
- [Knife paper package](../evidence/sample-preparation/run10/knife-paper-package.png)
- [Spatter paper package](../evidence/sample-preparation/run10/spatter-paper-package.png)

Earlier attempts remain in the evidence directory. They exposed the missing circle renderer, cabinet positioning before the table's final placement, and an approach inside the controlled footprint. Those are corrected and covered by the accepted checks. Legacy fixtures were updated for preparation trips, queued teaching dialogue before B storage, and above-ground package previews. One invalid comma-separated suite invocation in `regression1` did not run gameplay tests.

Preservation audit: 11 existing files changed in the 486-file feature baseline; the other 475 remain hash-identical. Eight dialogue entries were appended; previous catalog content is unchanged. The saved scene remains SHA256 `91d5c8c35425cdc32fec5ecb6bbc01ebfb07c368e227d62da5e10fdf12786005`. Evidence definitions, saved scene geometry, measurement data, scoring, transport and ruler/capture implementation are unchanged. Legacy test previews were restored byte-for-byte, with new captures retained here.

Accepted logs contain no C#/shader errors, null/missing-reference exceptions or new missing-dialogue warnings. The catalog fixture retains its exact existing missing-ID baseline; existing SDK, licensing and teardown diagnostics remain. No Unity process or pending runner request remains, and XR startup settings are enabled and unchanged.

Physical Quest controller reach, stereo readability and device performance remain untested. No build/install, live upload, commit, push or deployment was performed.

[Machine-readable results and hashes](../evidence/sample-preparation/summary.json)
