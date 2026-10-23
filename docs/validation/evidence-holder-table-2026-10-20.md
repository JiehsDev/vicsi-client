# Evidence holder table — 2026-10-20

Added a scene-authored table beneath EvidenceHoldingCrate at its existing entrance-side X/Z position (14.4, 1.5). The 0.9 m by 0.65 m tabletop is 0.74 m high, with four metal legs and a wood top. Holder center is raised from 0.55 to 0.864 m; the runtime holder base and tabletop both meet at 0.74 m. The table and holder are independent scene roots so the holder's renderer initialization does not hide the table.

Table colliders are enabled. The custody trigger's horizontal size is widened to admit a player standing beside the table (world size 1.935 by 1.68 m). Existing box ID, contents, requirements, custody behavior and phase gates are preserved. A 0.3 m radius standing-capsule check at the front approach found no obstacles, and that position lies within custody interaction bounds.

Two dedicated material assets were added: EvidenceHolderTableWood and EvidenceHolderTableMetal. The saved CSI_Environment scene now includes the table and raised holder; this intentionally changes its prior scene hash.

CustodyPassTest passed after the saved scene change: review/cancel, proximity checks, seal validation, atomic transfer of all six items, visible content bounds, source provenance, upload preservation and restart. Preview images are in docs/evidence/evidence-holder-table-2026-10-20. The earlier baseline report is retained separately and is not counted as final validation. No grading, dashboard or deployment change.

Final result: 50 PASS / 0 FAIL. Updated scene SHA-256: 7EDEE789FE6AEDBF8B724AC0B42ADC43D082FBCC66F147397633EE4CB9C7FB0F.
