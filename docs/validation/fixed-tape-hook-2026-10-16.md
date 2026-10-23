# Fixed tape reference endpoint — 2026-10-16

The tape's reference end previously followed the moving hand's perpendicular projection onto the reference plane, including the hand's changing height. It now snaps to the reference's authored `AnchorPosition` when hooked and retains that exact world position throughout extension. Moving the hand sideways, up or down moves only the free end. Either allowed reference order captures the correct anchor. Rehooking captures the newly selected reference; existing holster/resume behavior still requires hooking again.

This is an endpoint presentation change. Authored point/plane measurement methods, floor-plan readings, expected distances, tolerance, scene references, Sketch, `new-house-v2` upload identity and backend analytics are unchanged. For a plane reference, the displayed/recorded reading remains perpendicular floor-plan distance rather than the 3D length of the rendered segment.

Changed `Assets/_Project/Scripts/Interaction/MeasurementTool.cs` and extended its existing `MeasurementToolTest` with fixed-end assertions under horizontal and vertical hand movement. The existing live-readout check now verifies the authored measurement method independently from the fixed visual endpoint. The full suite also exercises both reference orders, real recording, holster/resume, disturbance and event counts. **41 PASS / zero failures**. Final results/state are retained in `docs/evidence/fixed-tape-hook/summary.json`. Editor is clean outside Play with no compilation failure or pending diagnostic/XR restore; both XR startup settings are restored.

No scene/object placement edits, backend changes, build/install, upload, commit, push or deployment. Physical Quest tape handling remains a headset acceptance check.
