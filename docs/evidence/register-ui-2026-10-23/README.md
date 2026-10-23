# Scene access register UI ? 2026-10-23

Changed the competitive project only. Header, check-in status, investigator/role/purpose fields, visit log and footer actions now occupy separate regions. Repeated investigator names were removed from visit entries. Entry/exit timestamps use readable seconds; missing exits remain explicitly unrecorded. Long histories scroll independently of the fixed action footer. Locomotion pauses while this modal is open and resumes when it closes.

Refreshed only SceneAccessRegisterUI's authored BuildPanel group in CSI_Environment; retained the logging table and other visual groups. An unrelated editor-driven CardTemplate position change was restored from the pre-refresh scene copy.

Validation: Unity compiled successfully. Isolated previews passed for check-in and an eight-visit history, with a scrollable body and unclipped footer. Inspected Temp/register-check-in-preview.png and Temp/register-visits-preview.png. Preview data did not authorize a player, complete gameplay, or enter history. Full controller/headset interaction was not replayed, and the installed APK was not updated.

The previous interactive Unity session crashed on close in AppUINativePlugin. The fresh editor completed the scene refresh and visual checks. Existing Editor SearchDatabase and Meta MCP port-conflict diagnostics also appeared; no register preview exception occurred.

Screenshots, scene backup and raw logs remain local in ignored Temp files.
