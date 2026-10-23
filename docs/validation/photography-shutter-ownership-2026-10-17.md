# Scene photography shutter ownership — 2026-10-17

A scene-route shutter previously ran evidence capture first, then notified ScenePhotographyManager. Evidence visible in the same frame could advance to its initial/final photograph while the scene station also completed.

PhotographTool now resolves shutter ownership before either workflow changes state. An unfinished scene station owns presses inside its standing zone, including wrong-angle and future-station attempts. Those presses reach the scene manager without executing evidence capture or its warnings, events and notifications. Ownership is resolved before route completion, so the final route shot cannot also become an evidence photo. Evidence photography remains available outside unfinished stations and after route completion. Camera controls, photo gates, evidence definitions and scene geometry are unchanged.

Implementation: `Assets/_Project/Scripts/Interaction/PhotographTool.cs` and `Assets/_Project/Scripts/ScenePhotography/ScenePhotographyManager.cs`. Persistent regression: `Assets/_Project/Scripts/Testing/PhotographyShutterOwnershipTest.cs`.

## Verification

Fresh Play sessions: **87 PASS / zero failures**.

| Suite | Passed |
| --- | ---: |
| PhotographyShutterOwnershipTest | 14 |
| ScenePhotographyProcedureTest | 47 |
| PhotographyGuideTest | 26 |

Reports are under `docs/evidence/photography-shutter-ownership/run1/`. The new regression calls the real camera shutter handler with a supplied valid evidence candidate, independently of house occlusion. It exercises wrong-angle and out-of-sequence attempts, every route station, last-shot isolation, and subsequent separate initial/final evidence shots. It uses a marked-record fixture to isolate final-photo routing. Existing suites cover normal tools/procedure gates, early evidence warnings, route completion, guide display and clean album rendering.

Unity recompiled/reloaded while the first completed diagnostic was awaiting runner collection. Its 14-pass report remained on the live diagnostic component and was recovered through the existing runner's Finish method. The other two reports were collected normally. Meta SDK interaction exceptions occurred during reload; no C# compilation failure remained. Initial shell invocation was rejected by the machine's script execution policy; the normal PowerShell execution-policy override started the runner.

Final Editor check: clean saved CSI_Environment outside Play, not compiling, no compilation failure, no pending runner/XR restoration, both Standalone/Android XR startup settings enabled. `git diff --check` passed for the edited runtime files. No scene authoring changes, build/install, real upload, commit, push or deployment. Physical Quest acceptance remains pending.
