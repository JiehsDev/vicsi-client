# Dialogue without speaker prefixes

Live dialogue now shows the spoken sentence without `Dispatcher:`, `Dispatch:`, `IOC:`, `SOCO:`, `Recorder:`, `Professor:`, witness names or similar speaker prefixes.

STCSNotificationUI previously added `speaker + ": "` to catalog lines, while some legacy DialogueLinePool lines already included their role name. It now normalizes incoming dialogue before queueing and displays only the resulting sentence. DialoguePresentation recognizes the current scenario's role/name labels and any explicitly supplied matching speaker. It also handles repeated prefixes, while preserving ordinary instructional colons and times such as `Next step: photograph the scene.` and `3:00 AM`.

Speaker attribution is retained separately in Dialogue History, along with source IDs, category, ordering and timestamps. History records the same sentence seen in the dialogue panel. Catalog/legacy assets and their IDs remain intact; no broad rewrite of testimony, procedural instructions or case facts is involved. Queue priorities, grouping, reading duration, skip controls and assessment data are unchanged. Debug log attribution is retained.

Examples:

| Before | Now |
| --- | --- |
| Dispatcher: Keep the scene secure. | Keep the scene secure. |
| IOC: Keep the scene secure. | Keep the scene secure. |
| Recorder: Copy that. Liquor bottle is in the log. | Copy that. Liquor bottle is in the log. |
| Vendor: [authored observation] | [authored observation] |

DialogueCatalogTest covers inline prefixes through the real display/history route, catalog speaker metadata, ordinary colons, the real vendor observation, queueing, priorities, timing, skipping and catalog coverage. DialogueHistoryTest verifies separate speaker metadata, the actual rendered label, inline attribution, ordering, history replay/scrolling and session reset.

Accepted results: **84 passed, zero failures** (DialogueCatalogTest 60; DialogueHistoryTest 24). Unity reports no C# compilation failure and zero console entries matching `error CS`. Final Editor is outside Play with the saved CSI scene clean, no pending runner/request/XR restoration, and Standalone/Android XR startup enabled. [Validation manifest](../evidence/natural-dialogue/summary.json).

No manual Editor wiring is required. No commit, push or deployment is performed.
