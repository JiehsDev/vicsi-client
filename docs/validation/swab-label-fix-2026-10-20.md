# Swab labels — 2026-10-20

The paper blood-swab package displayed its raw source key (EVD-014 / EVD-015). The sample-preparation cabinet also prefixed its readable name with that key. Both now resolve visible source text through the existing EvidenceDisplay.Label mapping: Evidence 1 – Kitchen Knife and Evidence 2 – Blood Spatter.

Internal source IDs, sample inventory keys, event payloads and scoring remain unchanged. Existing biological-sampling and sample-preparation checks now require the readable source label and reject EVD- in package/cabinet text. The biological suite covers both source packages; the preparation suite checks cabinet layout and labeling.

Native package previews are saved beside the reports in docs/evidence/swab-label-fix-2026-10-20. The bloodstain preview was inspected: the current label fits and the obsolete EVD-015 label is absent.

Validation: 109 PASS / 0 FAIL across BiologicalSamplingTest and SamplePreparationTest. Scene asset hash unchanged.
