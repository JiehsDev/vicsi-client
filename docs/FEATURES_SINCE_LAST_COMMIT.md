# Functionality added and updated since the last commit

Compared with commit `5d5fb74` — `checkpoint: verified SOCO closeout and evidence custody`. Current changes as of October 21, 2026.

1. Reorganized lobby menus, student details, instructor controls and headset status displays.
2. Improved lobby visibility, player starting direction, desk placement, furniture and lighting.
3. Added an in-game Settings menu with player-height adjustment, restart and return-to-lobby controls.
4. Improved VR panel placement, readable text, scrolling and long-dialogue layouts.
5. Improved controller input handling so menus, tools and player movement do not compete for the same buttons.
6. Moved the investigation into the new house, with updated evidence positions, room layout, collisions and usable stairs.
7. Updated body documentation, silhouette chalk marking, recording and body-removal interactions.
8. Added required arrival gloves before entering the controlled scene.
9. Added an outdoor supply table for replacing contaminated gloves.
10. Added fresh-swab pickup and a used/unavailable swab state in the tool wheel.
11. Added scene-entry and exit logging through an access-register table with pen and paper.
12. Updated the inner cordon to secure the whole house and the outer cordon to protect the yard staging area.
13. Added repeating POLICE LINE DO NOT CROSS lettering to the cordon tape.
14. Added IOC survey and contamination-control guidance.
15. Added approach markers for the First Responder and IOC, plus a ground-arrow route and waypoint to the vendor. Confirming Paul's fingerprint on the knife now requires the vendor follow-up before final submission.
16. Added photography location pins, off-screen arrows and clearer standing-position guides.
17. Separated overall-scene photography from individual evidence photography.
18. Added an assisted photographic ruler and evidence-framing checks for scaled photographs.
19. Improved the photo album with subject, stage and time labels, correct image proportions, paging, zoom and pan.
20. Added evidence-discovery dots and readable evidence names and numbers instead of raw internal IDs.
21. Improved tape-measure use with a fixed reference end, aligned hook receivers and updated measurement points.
22. Updated the Recorder to accept exactly three report lines per item, with clearer targeting and report controls.
23. Allowed evidence tents to be picked up after their own evidence has been recorded.
24. Added protected blood-swab drying, progress display, source-label verification and individual paper packaging before sealing and storage.
25. Changed blood-drying instructions and package labels to casework wording, including Blood Evidence Drying and Blood Evidence.
26. Added bottle fingerprint dusting and lifting, including its three-print result and source-linked fingerprint cards.
27. Added a fingerprint comparison desk with reference selection, zoom, rotation and ridge-detail controls.
28. Made fingerprint identification and related conclusions depend on a correct comparison; lifting, storing or choosing a wrong reference cannot confirm the match.
29. Removed practice/training wording from the fingerprint examination interface.
30. Added a flexible evidence inventory with clearer sample identity, sealing, storage and custody information.
31. Added an evidence-box contents display, a supporting table and a waypoint showing where to transfer collected evidence.
32. Replaced the connection board with a question-based Evidence Relationships Review that saves confirmed answers.
33. Added reasoning checkpoints and updated final assessment reporting for reasoning, evidence relationships and the final conclusion.
34. Simplified session records and procedure timelines to show meaningful completed actions and readable question/evidence labels.
35. Expanded Unity's Scene Test Start menu to 16 procedure presets, including ruler photography, blood drying, fingerprint comparison and custody transfer.
36. Made previously script-created props, panels, guides, ruler and packaging editable in Unity, with an Editable Scene Objects browser.
37. Fixed the tool-wheel texture regression and improved the saving of generated visual assets.
38. Updated automated procedure checks, test fixtures, editor setup tools and validation reports for the revised workflows.

Related dashboard work completed during this period, in the separate `vicsi-dashboard` repository:

39. Simplified student and instructor session views around individual session records.
40. Updated evidence labels, question labels and the new-house layout profile.
41. Added missing procedure timeline/upload details and corrected premature scene-release wording.
42. Fixed unavailable-question displays, TypeScript errors and outdated dashboard checks.

Scene Sketch workflow update (2026-10-22):

- Added Scene Sketch to the Y tool wheel with a clipboard icon and clear first-use/measurement-completion guidance.
- Required each item to be measured and correctly placed on the sketch before swabbing or fingerprint processing. Full sketch submission is required before submitting the final conclusion.
- Kept rejected sketch placements attached to the cursor and made recorded measurements appear automatically on correctly placed items.
- Updated procedure maps, Scene Test Start presets and automated checks for the required sketch step.

- Required John's sketch placement before body recording and removal, with independent guards on actual removal and final completion.
