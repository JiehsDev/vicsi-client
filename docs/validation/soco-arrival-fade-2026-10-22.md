# IOC to SOCO arrival fade

The role handover now fades to black for 0.5 seconds, repositions the locomotion capsule and tracking origin together at a grounded entrance destination, holds black for 0.4 seconds, then fades in for 0.5 seconds. No camera animation or visible falling movement is used. Head tracking continues throughout.

The stereo-compatible fade draws over scene geometry. Gameplay input and locomotion controls are suspended during the transition, and existing canvases are temporarily hidden. Disabling the workflow clears the fade and restores its locks/canvases. A second arrival request is rejected. Missing authored fade/destination references prevent an uncovered teleport.

Editable scene objects:

- `SOCO arrival - comfort fade`: disabled MeshRenderer under the rig's CenterEyeAnchor, using `ArrivalComfortFade.mat` and the stereo `ViCSI/Arrival Comfort Fade` shader.
- `SOCO arrival - grounded destination`: independent entrance floor anchor at approximately `(16.90, 0.01, 0.65)`. Move/rotate this object to change the arrival feet position/yaw.
- `SOCOWorkflowInteraction`: Inspector fade and settling durations and references.

The existing Form 2, IOC briefing, SOCO investigation progression, evidence state, scoring and simulated arrival access-log semantics are preserved.

Validation evidence is in `docs/evidence/soco-arrival-fade-2026-10-22/summary.json` and its referenced Play-mode reports. The narrative suite checks opaque black before repositioning, grounded arrival, no settling drop after reveal, input ownership, duplicate requests, disable cleanup, briefing and session restart.

The initial phase test expected an unchanged rig origin rather than the grounded capsule destination. Its assertion now checks the actual feet position and authored yaw, allowing the tracking origin's horizontal head offset. The access-register fixture now completes the current outdoor-photography prerequisites before exercising SOCO re-entry; gameplay entry protection is unchanged. Initial reports remain in `run1` for traceability.

Unity C# compilation completed with no errors, and no shader errors were reported after this shader was imported. A physical-headset comfort check remains manual.

No commit, push, deployment or production record changes were performed.
