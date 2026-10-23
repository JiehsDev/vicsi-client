# Mandatory sketch completion ? 2026-10-22

Closed the two completion gaps found in the previous audit.

- Body recording requires completed measurement and correct body sketch placement. Recorder availability and the authoritative Logged transition share this gate.
- Body preparation and actual removal independently recheck sketch placement. A prepared-state fixture cannot skip this check.
- The full scene sketch must be explicitly submitted before the final conclusion. Populating the four items and eight distances alone is insufficient. The real conclusion entry point, picker confirmation and final submission recheck readiness; a stale picker returns ownership to the sketch.
- The phase controller also refuses entry into closeout and final InvestigationComplete while the sketch remains unsubmitted. This preserves the requirement even for direct transition calls.
- The sketch check runs before entering final conclusion feedback, keeping the player free to open the Y tool wheel and finish the pad. Per-item processing still requires placement only; students can finish samples and transfer physical evidence before final sketch submission.
- Dialogue catalogue messages explain how to open Scene Sketch and press Submit Sketch. Body recording and removal use their own wording.
- Test presets and affected procedure fixtures include prior sketch steps. No scoring weights, Recorder scoring, evidence relationships or conclusion resolution rules changed.

Validation: 683 passing checks across 12 suites, with 0 failures in accepted reports and 0 C# compilation errors. The runtime assembly is newer than the final edited script. Accepted report paths are listed in docs/evidence/sketch-completion-gates-2026-10-22/summary.json.

Coverage includes direct completion/body-removal bypass attempts, real body Recorder/removal interaction, closeout for the existing endings, separate Recorder points and upload preservation, all 16 test-start presets, biological sampling, ending selection, full-case narrative progression, relationship review and spatial measurements.

Updated test fixtures replace used gloves before recording/continuing, seed a submitted sketch for the Custody Pass preset, and inspect current scene managers after restart. The inconclusive fixture leaves the spatter examination/record unfinished; completed recording still confirms its existing baseline finding, as designed. The actual finding rules, conclusion resolution and scoring were preserved. Earlier failed reports remain in the evidence folder. No commit, push, deployment or production-data changes.
