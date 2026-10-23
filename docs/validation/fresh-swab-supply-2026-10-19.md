# Fresh swab supply and used Tool Wheel state — 2026-10-19

The outside glove table now has a labelled packaged-swab tray. During investigation, a student with clean worn gloves and empty hands uses A to pick up a packaged swab and a separate A to open/load it. B, walking away or unrelated modal UI returns a pending pack without loading it. One unused swab is available at a time; training stock is unlimited. Initial scene entry/check-in requires no additional action.

The existing Swab role stays in its Tool Wheel position. Its icon becomes grey after a successful sample, remains muted on hover without hover lift and shows a reason in the centre. The unavailable slot refuses before changing another held tool/evidence item. Fresh supply restores normal appearance and equip behaviour. Both sampling paths consume a fresh swab only on success; direct equip/request/grab paths cannot make an exhausted kit clean.

A blood-loaded kit remains recoverable through the grey slot so it can be contained. A held container remains available for sealing and explicit B storage. Sealing no longer replaces the kit's tip with a clean swab: its blood-bearing mesh is retained and the spent loose tip is hidden. A new confirmed pack restores the clean kit. Changing gloves alone does not replenish a swab, and obtaining a swab requires clean gloves. Existing source attribution, sample models, collection flags and custody remain intact.

One shared outside waypoint handles gloves, swabs or both, retaining the existing authored doorway/steps/entrance route and photo-capture hide scope. Initial swab navigation starts when the missing slot/sample action is requested, avoiding distraction during earlier scene photography; used-swab navigation waits for current-sample storage. Students can still collect supplies voluntarily at the table during investigation. Glove changes have priority over pack pickup and no held A can complete both.

No new Tool Wheel role, evidence definition, scoring weight, upload field or dashboard change. Four append-only local events record pack pickup/return, loading and use. Normal sessions begin without a fresh swab. Isolated development presets explicitly prepare a fresh swab, with no production shortcut; legacy procedure fixtures visit the real station for both supplies.

## Evidence

Accepted **604 PASS / 0 FAIL** in eleven fresh Unity suite processes, each exit 0. Accepted logs contain zero C# compile errors, shader errors, null-reference exceptions or missing-reference exceptions, and zero new missing-dialogue warnings against the recorded catalog fixture baseline.

| Suite | Pass | Accepted attempt |
| --- | ---: | --- |
| FreshSwabSupplyTest | 51 | run4 |
| GloveChangeStationTest | 52 | run2 |
| BiologicalSamplingTest | 60 | run2 |
| EvidenceInventoryTest | 29 | run2 |
| ArrivalPPETest | 42 | run2 |
| ArrivalEntryTest | 26 | run2 |
| SceneAccessRegisterTest | 40 | run2 |
| PhotographicScaleTest | 61 | run2 |
| VendorPathGuideTest | 40 | run2 |
| SceneTestStartTest | 143 | run2 |
| DialogueCatalogTest | 60 | run2 |

Twelve existing files changed within the 776-file feature-start scope; the other 764 are hash-identical. Saved scene/prefab geometry, evidence definitions, measurement data, ruler code, photograph capture code, score weights, transport and XR settings are unchanged. Table additions are runtime children; the authored entrance/house route is unchanged.

Final check: no Unity process, no pending file-runner requests, and XR startup remains enabled for Standalone/Android. Earlier validation images emitted to legacy test paths were copied into this task's evidence directory and their saved pre-run versions restored.

Accepted suite counts/source hashes are recorded in `docs/evidence/fresh-swab/summary.json`. The task baseline and `Tools/Audit/fresh-swab-audit.py` verify existing content independently of the pre-existing dirty Git tree. Eight catalog IDs are added with no existing entry changes. Saved scene SHA256 remains `91d5c8c35425cdc32fec5ecb6bbc01ebfb07c368e227d62da5e10fdf12786005`.

The focused test uses actual simulated Y, thumbstick, A and B input. It checks unavailable/fresh/loaded/stored wheel states, opacity/hover behaviour, label fit, distinct pack actions and cancellation, direct registry/coordinator refusal, loaded-kit recovery, real required-source sampling and storage, glove/supply independence and reload. The legacy generic sampling entry point is exercised through a reversible runtime-only cloned-definition fixture; this does not add biological sampling to the actual bottle case.

Native Direct3D wheel and supply-table PNGs are retained per focused attempt. Inspected views confirm a grey used icon, bright fresh selection, readable missing/recovery text, and a separate labelled swab tray beside the glove box. The original Tool Wheel layout and other tools retain their presentation. Physical Quest legibility, controller comfort, pack/table reach and stereo appearance remain manual acceptance.

The full regression run includes glove changes, biological sampling, inventory ownership, arrival PPE/entry, access register, photographic scale/capture, vendor navigation, all development presets and catalogue. Existing SDK/MRUK teardown diagnostics remain. The catalog suite's recorded pre-feature tiny-catalog callback and intentional absent-ID warnings are compared by exact baseline counts; new missing-ID warnings are rejected.

Run1 passed 44 focused checks. Run2 passed the full eleven-suite regression with the initial-navigation timing refined. A final message review added the contained-sample wheel state: that selection preserves the held container and asks the student to finish sealing/storage, rather than requesting a trip outside. Run3 retains one fixture failure because the helper cleared its feedback before asserting it; the fixture now captures feedback before cleanup. No gameplay guard was bypassed to resolve it.

Run4 passes all 51 focused checks with the contained-sample state included. Its native contained-sample view was inspected alongside the fresh/used/recovery wheel and supply-table views retained across run1/run2.

No headset build/install, live upload, commit, push or deployment was performed.
