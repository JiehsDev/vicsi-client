# Evidence discovery dots — 2026-10-18

Added a small highlighted circular cue to registered physical evidence after the scene-photography route is complete. All still-undiscovered eligible items become visible together on `OnScenePhotographyCompleted`; a matching `Found` transition immediately hides its dot independently. There are no arrows, labels, new interactions or forced selection order.

## Discovery timing

Existing discovery behavior is deliberately preserved: proximity can already discover an item during scene photography. Such an item stays unmarked when photography finishes; it is not reset to undiscovered. The user was asked about delaying discovery as an alternative; with no answer received during implementation, the stated default was to preserve current discovery and show dots only on remaining undiscovered items. No new discovery gate was added.

This matters on the actual photo route, whose interior stations pass close to some evidence. The implementation does not promise that all four current physical items will still be undiscovered at route completion. It guarantees simultaneous availability for whatever eligible items remain `NotFound`.

Current eligible physical roster: Body A / John, Kitchen Knife, Blood Spatter and Empty Liquor Bottle, retaining all existing internal IDs. Blank-ID tool models, unregistered pseudo-targets and external-trigger/latent fingerprint targets do not become visible evidence hints. The original fingerprint/UV reveal behavior remains unchanged.

## Visual and lifecycle behavior

- Circular cyan/white cue with a dark outline and subtle halo; no text or arrows.
- A 5 cm outer halo diameter, with a smaller solid core, independent of imported prop scale.
- Headset-facing world-space marker above the visible mesh bounds; fallback above the prop pivot for decal-only targets. The evidence itself never moves.
- Depth-tested transparent shader (`ZTest LEqual`), so opaque walls/props occlude it. This is a world cue, not an offscreen edge indicator or through-wall highlight.
- No lights, colliders, raycasters, click targets, input actions or modal/locomotion ownership.
- Hidden before scene-photography completion, during IOC/blocked phases, after discovery, while owner/component is disabled, and during actual album capture.
- Capture uses a nested synchronous hide scope and restores only still-eligible markers. A newly discovered item cannot be resurrected by scope disposal.

## Source scope

- New `EvidenceDiscoveryDot` component and `EvidenceDiscoveryDotGraphic` circle mesh, with metadata.
- New `Assets/Resources/EvidenceDiscoveryDot.shader`, with metadata; original guide shader unchanged.
- `EvidenceProp`: read-only access to the existing external-discovery flag and guarded visual-component attachment after its existing external-trigger early return. Original discovery trigger/radius and `TryNotice` logic remain unchanged.
- `PhotoCaptureListener`: hide/restore the dots around the existing capture render. Image dimensions, photo eligibility, crop, album data and uploads are unchanged.
- New `EvidenceDiscoveryDotTest` and task-local check runner. The new suite uses Direct3D 11 to verify actual rendered pixels; existing regressions use their normal offline batch environment.

The task-start hash inventory confirms that 1,383 other existing scoped source/data files, including all scenes, evidence definitions, existing IOC/NPC/photography guides, menu/Tool Wheels, shared input/VR placement, scoring, uploads and resources, remain unchanged. Evidence: `docs/evidence/evidence-discovery-dots/preservation.json`. No database or dashboard records were edited.

## Validation

The focused dot suite passes **45 checks**, including simultaneous completion-event visibility, per-item immediate disappearance, unchanged discovery/events, late readiness, no resurrection, hidden fingerprint/tool targets, constant world size on the scaled bottle, native mesh/shader, real rendered wall occlusion, billboard orientation, disable/re-enable behavior, nested capture scope, actual album render exclusion, IOC-phase suppression and preserved evidence transforms/scales. Render fixtures are saved as `dot-visible.png` and `dot-occluded.png` under `docs/evidence/evidence-discovery-dots/`; both were visually inspected.

Existing discovery and photography-guide regression results and compiler checks are recorded in `docs/evidence/evidence-discovery-dots/summary.json` after completion. Quest 3S visual acceptance remains manual: verify the small cue's brightness, distance readability and surface placement in the actual environment.

No commit, push, deployment or production-data write was performed.

## Accepted final results

- EvidenceDiscoveryDotTest: **45 pass / 0 fail**, exit 0, Direct3D 11; actual highlighted pixels, opaque-wall occlusion and clean album capture verified.
- EvidenceDiscoveryTest: **10 pass / 0 fail**, exit 0; original proximity discovery/radius, IOC gating, latent-print exclusion and once-only transitions preserved.
- PhotographyGuideTest: **26 pass / 0 fail**, exit 0; original photo-route behavior and capture guidance preserved.
- Total: **81 pass / 0 fail**, **zero C# or shader compile errors** across the accepted logs.
- Final hashes preserve 1,383 other existing scoped source/data files. No scene, evidence data/ID, scoring, upload or other guide/menu code changed.
- No headset connected over ADB; optical comfort and cue visibility in the actual Quest environment still need manual acceptance.

The discovery-timing clarification received no answer during this run; the implementation retains the explicitly stated conservative default. To guarantee that all four items are still undiscovered when photography finishes would require an additional gameplay discovery gate, which this visual-only change does not add.
