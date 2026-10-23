# Connected-headset Play Mode recovery

The failed manual Play attempt logged `D3D11: Failed to create RenderTexture (1824 x 1872 fmt 19 aa 1), error 0x8007000e`. Windows maps this HRESULT to System.OutOfMemoryException. Only approximately 295 MiB of system commit remained out of 32472 MiB; the long-running Editor held approximately 13.4 GB of private committed memory on a machine with 7895 MiB RAM and 3977 MiB dedicated graphics memory. This supports memory exhaustion during XR texture allocation. It does not establish a custody-transfer code fault.

The same session had a separate startup assertion: WallPenetrationTunneling's TunnelingEffect had no assigned MeshFilter and no mesh child. Its center-eye camera was assigned. SmoothMovementTunneling had a valid mesh and renderer using the existing tunneling material.

Restored a Mesh child under WallPenetrationTunneling using the sibling effect's existing mesh/renderer setup, assigned its serialized `_meshFilter` field, and saved CSI_Environment.unity. This is a persistent scene repair, with no package-cache edit, new dependency, disabled comfort effect, or C# gameplay change.

Unloading unused assets and collecting managed memory did not recover the old Editor's allocations. The scene was saved and a normal window-close restart was requested. The old process emitted a native crash during shutdown; its final stack is preserved in the evidence directory. The saved scene reopened successfully. Fresh Editor private committed memory was approximately 4.2 GB, and available system commit recovered to approximately 16.5 GB during reopening. No process was forcibly terminated. The diagnostic bridge reconnected after startup.

Retried the actual manual Play path with the connected headset configuration and Standalone XR startup enabled. The custody preset became ready with six inventory entries, no setup failure, and the existing EvidenceInventoryReview phase. Unity remained in Play, unpaused, with compilationFailed=false. The repaired mesh reference remained assigned after reopen and at runtime.

The new Editor log contains zero render-texture allocation failures, zero missing-tunneling-mesh assertions, and zero C# errors. No new native crash report appeared after the restarted Editor began loading. These checks are recorded in log-checks.json.

Full headset-image validation is **pending**. OpenXR initialized and OVRManager detected the headset, but the device reported userPresent=false and inputFocus=false; the XR display subsystem was not running and eye texture dimensions were zero at the recorded check. The user was asked to wear the headset and enter Quest Link. This must not be reported as a verified active-headset rendering test. Existing XR audio fallback, unsupported-feature/haptics warnings, and unrelated local bridge port conflicts remain distinct from the repaired assertion and the prior allocation failure.

Evidence: docs/evidence/play-mode-recovery/initial-findings.txt, runtime-proof.json, log-checks.json, shutdown-stack.txt. Current Editor log: Logs/PlayModeRecovery.log. Editor left running in the isolated Custody pass preset so the headset image can be checked. No XR quality settings, Android build settings, scoring, evidence custody logic, or upload behavior were changed. No commit, push, or deployment.
