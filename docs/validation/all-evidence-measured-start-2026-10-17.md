# All evidence measured test start

Use **Tools > ViCSI > Scene Test Start > All evidence measured > Play selected start**.

This new preset is appended as enum value 10; existing saved selections retain their values. The older Measurements done preset remains unchanged.

The new start prepares IOC handover, scene photography, discovery of all four items, required chalk, item/trace/letter markers, final evidence photographs and all fixed-reference/pair measurements. It retains each item's actual measurement/photography order, including the body-specific final-photo-before-measurement gate. All four evidence records stop at PhotographedFinal with no report lines or body findings recorded.

Five real linked tents are visible: three item markers, the knife trace marker and Body Marker A. A sixth, unused numbered tent stands near the kitchen approach for immediate pickup testing. Linked markers remain locked until their own evidence is recorded. The Recorder is equipped and the player faces the body; its three findings can be recorded first to test Marker A. Sampling and fingerprints on the other evidence remain unfinished and follow their normal gates. Collection, inventory, body removal, sketch and scored answers are not pre-completed.

The marker visuals use the existing tent model/template references and normal pickup components. Their guarded preparation helpers compile only in Editor/development builds and require SceneTestStart's isolated preparation scope. Restart selected test reloads the scenario and restores measurements, unrecorded evidence and all six physical tents. Test logs/queues remain in AutomatedTestSandbox; student results are not uploaded. No manual scene wiring is needed.

SceneTestStartTest checks every preset, the new preset's state/physical markers, individual marker unlock after recording the body, and restart restoration. The first run passed the 116 existing assertions and exposed the body's separate final-photo prerequisite; the fixture now honors that rule without changing production measurement gates. Accepted results are stored under `docs/evidence/all-evidence-measured/`.

Accepted validation: **131 pass, zero failures across all eleven presets**, including the new preset, independent marker unlock and restart. Unity reports no C# compilation failure and zero console entries matching `error CS`. Final Editor is outside Play, saved CSI scene clean, no pending diagnostics/XR restoration, Standalone/Android XR startup enabled. Scene Test Start window is open with All evidence measured selected locally; the saved component remains NormalPlay. [Validation manifest](../evidence/all-evidence-measured/summary.json).

No commit, push or deployment is performed. Physical Quest placement/readability remains manual acceptance.
