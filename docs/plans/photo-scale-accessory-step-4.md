# Step 4: camera photo-scale accessory

Approved scope: a visible ruler placed with A from the camera, outside the Tool Wheel; first capture without it, then frame it with the evidence for a scaled photo, with deliberate removal afterward.

Status: implemented and desktop validation completed on 2026-10-19. Accepted 485 checks across nine suites, zero failures; native photographs/prompts inspected. Physical Quest acceptance remains pending. Report: [photo-scale-accessory-2026-10-19.md](../validation/photo-scale-accessory-2026-10-19.md).

## Integration

- Reuse the existing unmarked and marked/final evidence-photo states. Final camera photographs of the knife, bloodstain and bottle require the ruler; the first photograph remains unscaled. Body and developed-print photography retain their existing workflows.
- The camera installs its accessory at runtime. A places a single 30 cm ruler at an authored metric offset and orientation; another deliberate A removes it. Held A cannot perform both actions. Describe this as assisted placement.
- Placement requires an equipped, raised camera, completed overall scene photography, an existing unscaled photograph, completed item marking prerequisites, a nearby selected item and no unrelated modal UI. No scale is placed automatically by aiming or shutter input.
- Floor items use a flat ruler beside the item; the upright bottle uses an upright ruler beside it. The ruler is render-only, does not cover the evidence and occupies no inventory or tool slot.
- The final shutter requires the same item's complete ruler and authored item bounds within the saved-photo aspect, sufficiently large in frame, with clear ruler sightlines and a readable viewing angle. Missing, clipped, obstructed, wrong-item or overly distant ruler views cannot advance the final-photo state or save a photo.
- Do not include a placed scale in an unscaled photo. Body/derived-print workflows do not gain a new scale requirement.
- Save successful scaled close-ups at 960 x 720 under the default 4:3 configuration so the numbered ruler marks have usable pixels. Preserve existing resolution for other photographs. Keep physical scale geometry visible while hiding the viewfinder and transient feedback from the saved photo.
- Explicit A removal leaves the completed photograph intact. Holstering, leaving the item, moving its source, disabling the camera or reloading returns/clears the accessory. Lowering and raising the camera alone need not discard a placed ruler.
- Use append-only, local, non-scored accessory events. Keep the existing photo-state timeline, deduction weights, upload schema and dashboard unchanged. Do not add a new persisted evidence lifecycle state.

## Validation

Use actual simulated A input for placement/removal and held-button checks, plus the real camera shutter and capture listener for all three items. Exercise missing prerequisites, modal/raised-camera/range guards, wrong source, clipped and occluded rulers, one capture per state, capture resolution, no source movement or sample/inventory credit, cleanup and reload. Inspect the actual saved original/scaled images, including ruler orientation and numbered graduations.

Re-run shutter ownership, scene-photography procedure/guidance, chalk, measurement, biological sampling, development presets and catalog checks. Update older final-photo fixtures to perform the new accessory interaction rather than bypassing the camera's scale validation. Retain failed attempts. Saved scene geometry should remain byte-identical.

Physical Quest readability and controller comfort remain manual acceptance. This task does not implement the wider photography-coverage redesign or developed-print photography.
