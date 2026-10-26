# Research register UI port ? 2026-10-23

Applied the register presentation from competitive checkpoint c174f08 to the research workspace on master. No competitive menu, rankings, profile identity or scoring features were brought into research.

- Aligned investigator, role and purpose fields; bounded header, history and footer.
- Compact visit entries, readable second-resolution timestamps, and explicit missing exits.
- Clear check-in/cancel controls; long histories scroll while controls stay visible.
- Modal locomotion pauses until the register closes.
- Refreshed only the authored register BuildPanel group in CSI_Environment. Existing disk scene records changed only for the register visual storage and its owner transform; other scene records were preserved.

Validation: Unity compilation passed; isolated previews passed for a long research student name, check-in and eight visits. Visually checked Temp/research-register-check-in.png and Temp/research-register-visits.png. No participant identity, authorization, session ownership, logging or upload implementation changed. Preview data did not enter a research session.

The open scene, including its unsaved editor layout state, was backed up to Temp/research-register-before.unity before refresh; original on-disk scene is in Temp/research-register-disk-before.unity. Preview-generated fallback-font changes were removed. Unity was returned to CSI_Environment outside Play mode.

Changes are saved locally and have not been committed, pushed, or rebuilt into the headset APK. Screenshots and raw editor logs remain in ignored Temp files.
