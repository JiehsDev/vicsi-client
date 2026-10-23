# Scene Test Start refresh — 2026-10-20

The requested test list is the Unity **Tools > ViCSI > Scene Test Start** menu. The existing 12 presets passed their 143 integration assertions before changes; the menu nevertheless lacked direct entry points for photographic rulers and the new comparison station.

The menu now has 16 choices, grouped by activity. Existing serialized enum values and Editor selections retain their meanings; menu display order is mapped explicitly instead of casting a dropdown index to a saved enum. One shared milestone mapping prevents appended focused exercises from being interpreted as later completion stages.

Three focused photography presets prepare only the selected item's original photo, chalk where required, physical item marker and knife trace marker. Each equips a lowered camera and leaves ruler placement and final capture to the tester. Other evidence, sampling, reports, measurements and scored answers remain unfinished. Restart returns the item to Marked with no ruler.

Fingerprint comparison practice starts with empty hands at the current outside desk, two stored sealed fingerprint cards and no tutorial attempts or completions. Later completed-procedure presets now include both cards alongside two bags and two blood samples. Scene photography starts with the camera equipped. Descriptions and the manual guide explain ruler controls, drying and paper packaging, fresh swabs/gloves and custody transfer before closeout.

The integration diagnostic reloads every preset and exercises restart, actual placement/headset/capsule position, physical markers, pending photo gates, real camera/ruler/shutter captures, comparison eligibility, stored custody, checkpoint suppression, original Normal play and assessment isolation. Reports are in `docs/evidence/scene-test-start-refresh-2026-10-20/`; `before.txt` is the unchanged baseline and `run1.txt` is the first 204-assertion pass before adding the three real photo-capture checks.

Manual instructions: `docs/DEVELOPMENT_TEST_STARTS.md`. This change updates development fixtures and their Editor menu; physical Quest interaction remains a separate acceptance check.

Final result: **207 PASS / 0 FAIL** across all 16 presets, including real final-photo captures from all three new ruler starts. Editor returned to Edit mode with no dirty scene or pending suite request, and the refreshed window was opened. Saved scenario SHA256 remains `e441ee08004b03baf5be2ce53598e8542c4318bbb45ff690cd29429d78cadd3d`.
