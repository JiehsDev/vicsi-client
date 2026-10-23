# Evidence points remain until recording

Evidence discovery dots previously disappeared at `Found`, which proximity can trigger before the student documents the item. They now remain visible until the subject has a confirmed Recorder snapshot or reaches `Logged` or a later evidence status.

This applies to BODY-A and the three active evidence items. Photography, marking, processing, selecting statements, incomplete confirmation and cancelling the Recorder do not remove the point. Confirming an entry immediately hides only that subject's point. Previously logged items without new Recorder scoring data also remain hidden.

The existing scene-photography/role gates, depth occlusion, physical-evidence visibility and temporary exclusion from captured photographs are preserved. This change does not advance evidence, change Recorder scoring or write extra timeline events.

Validation: **125 accepted checks passed**, zero failures:

- `run2/EvidenceDiscoveryDotTest.txt`: 95 checks, including all four real Recorder confirmations, cancellation/incomplete confirmation, intermediate evidence states, legacy statuses, native dot rendering/occlusion and actual photo capture without dots.
- `run1/RecorderReportLimitTest.txt`: 30 existing Recorder interaction checks.

Evidence: `docs/evidence/evidence-dots-until-recorded-2026-10-22/`. The first dot-suite attempt timed out after its old camera fixture invoked an evidence-photo callback without a PhotographTool. The fixture now uses the existing public scene-photo capture API; the accepted rerun passes. Final Unity compilation succeeded. No headset validation was performed. No commit, push, deployment or production-data modifications.
