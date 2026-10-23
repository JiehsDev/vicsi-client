# Knife photograph with ruler — 2026-10-20

The ruler's projected-size gate rejected a fully framed knife photograph from a standing position 1.8 m above and 1.1 m back from the item. The existing close-range fixture passed, but this standing regression failed before the fix (the same regression also exposed the bloodstain restriction).

Scaled captures now use at least 1440 × 1080 instead of 960 × 720. The minimum ruler span is 10% of image width instead of 14%; at the default aspect this retains at least 144 ruler pixels rather than 134, while accepting the standing view. Whole-item and ruler bounds, occlusion and printed-face checks remain active.

The camera accessory prompt now reports whether the player needs to fit the whole subject, clear an obstruction, view the printed face, move closer or aim at the item beside the ruler. A valid view explicitly says to press the trigger. New prompt entries are persisted through Unity's catalog asset and checked for existence by the fixture.

Evidence is in `docs/evidence/photo-ruler-fix-2026-10-20/`. The fixture captures the knife from the standing position through the actual shutter and album listener, checks image dimensions and state/event progression, and rejects an unreadably distant ruler. This is Editor validation; physical headset acceptance remains unverified.

Accepted results: PhotographicScaleTest **68 PASS / 0 FAIL**, PhotographyShutterOwnershipTest **14 PASS / 0 FAIL**. Inspected the saved standing knife photo and green viewfinder with its resolved ready prompt. Editor returned to Edit mode with no dirty scene or pending suite request.
