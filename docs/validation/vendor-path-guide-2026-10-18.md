# Vendor ground-arrow guide - 2026-10-18

## Result and activation

Added cyan, dark-outlined ground chevrons leading from the rear kitchen, through the existing house exit, down the steps and toward the physical vendor. The trail uses the existing VendorFollowupInterview.FollowupUnlocked predicate: FIND-KNIFE-PRINT-BROTHER confirmed and RC-03-FINGERPRINT answered. This is the current reasoning question integrated with stable HYP-02. Merely identifying the print, making the question eligible, or selecting an unconfirmed response does not show the trail. Any confirmed answer unlocks it, irrespective of weight, matching the original vendor gate.

The path only appears in SOCOInvestigation. Modal UI and spoken dialogue hide it. Entering the existing 2.25 m vendor interaction radius hides it; leaving before completing the interview restores it. The completed vendor statement retires it for the session. Scene reload resets it. No automatic dialogue, movement, discovery or question submission is introduced.

## Files and rendering

- New VendorPathGuide.cs: read-only visibility and one batched, depth-tested arrow mesh; no input actions, colliders, raycasters, locomotion ownership, logging or scoring writes.
- New VendorPathRoute.cs and Resources/VendorPathRoute_CSI.asset: scene-specific nine-point route. The house section reverses the existing validated arrival-to-kitchen traversal. Only existing VendorWitnessInteraction in CSI_Environment auto-attaches the guide in Start.
- Profile dimensions: 0.28 m wide, 0.36 m long chevrons, 0.8 m spacing, 18% larger dark outlines; surface offset 3.5 cm. Floor support and low obstacle checks omit invalid individual arrow positions. Nearby unobstructed joining sections are selected with a sphere cast; unsupported floors and distant/blocked connectors do not generate shortcuts through walls.
- Reuses the existing EvidenceDiscoveryDot shader unchanged: depth-tested unlit vertex colors and stereo macros. World geometry occludes the arrows. No lights, pulse or glow.
- PhotoCaptureListener adds a nested-safe capture scope so arrows stay out of actual photographs and restore according to current valid state afterward.

Native preview: `docs/evidence/vendor-path-guide/kitchen-trail.png`. Inspected the rendered kitchen scene; cyan chevrons with dark outlines are visible on the floor and respect scene depth. Rendering with/without the arrow mesh changed 8,482 cyan pixels.

## Validation

| Suite | Passed | Failed |
| --- | ---: | ---: |
| VendorPathGuideTest (Direct3D 11) | 41 | 0 |
| VendorFollowupTest | 17 | 0 |

58 checks passed; both Unity processes exited 0. Zero C# or shader compile errors in final logs. Logs: `docs/evidence/evidence-display/vendor-path-guide-2026-10-18-3/`.

Focused validation covers exact fingerprint identity and checkpoint gate, real choice UI selection/confirmation, low-weight answer compatibility, finite mesh and native pixels, modal/dialogue suppression, nested capture restoration, actual album render exclusion, floor support, all eight traversal legs with the real Meta controller dimensions and collision settings, route progress trimming, different-floor rejection, original physical vendor interaction/question and completed-statement retirement, disable cleanup, untouched evidence/vendor transforms and reload reset. Original vendor suite also verifies witness records and relationship/deduction behavior.

Two prior diagnostic attempts are preserved. The initial range assertion incorrectly used 3D distance to an elevated vendor label anchor instead of the original horizontal interaction distance. The traversal probe also collided with the duplicate live player's capsule at arrival; Physics.IgnoreCollision pairs do not filter Meta's standalone CapsuleCast queries. The final fixture disables only the live capsule during the synchronous probe and restores it in finally. All original environment collision, step, slope and skin settings remain. The same authored route then passes every leg; no environment fix or waypoint relocation was needed.

## Scope and remaining checks

Only two pre-existing source files changed: VendorWitnessInteraction (attach guide) and PhotoCaptureListener (capture suppression). All 1,393 other scoped existing source/data files remain hash-identical to the task start. No scene or prefab changed. Evidence IDs, hypothesis/checkpoint IDs, findings, procedure order, fingerprint extraction/storage, interaction ranges, input/modal ownership, other guides, evidence dots, photography circles, menus/Tool Wheel, scoring, uploads, dashboards and database records are preserved. See preservation.json and runtime-diff.patch in the evidence folder for the exact task delta.

No Quest headset was connected. Verify stereo comfort, arrow visibility/size, stair placement and walking the complete vendor route on Quest. This is an authored visual route for the current house, not a general navigation system; it does not reroute around newly introduced geometry.

No commit, push, deployment or production-data modification.
