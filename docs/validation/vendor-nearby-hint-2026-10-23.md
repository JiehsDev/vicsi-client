# Vendor approach hint — 2026-10-23

VendorWitnessInteraction now shows a readable nearby panel within the existing interaction radius. It gives the A-button talk action, explains that an unlocked follow-up must be chosen inside the interview, and offers statement review after discovery. Before the investigation phase, it explains the briefing requirement. When a procedural interlock prevents talking, it displays the existing block reason instead of advertising an unavailable action.

The range check resolves the current main camera, recovering from camera creation after NPC initialization and from replacement of a previously cached camera. The original world label and physical interaction radius remain. The nearby panel is independent of the NPC model's imported scale, takes no UI/input ownership, and yields to modal screens, ongoing vendor speech and unrelated immediate feedback. At the destination it replaces the general vendor-lead/statement-recorded tooltip with the specific interaction action.

The vendor's read-only panel now explains when no follow-up question is available yet and where a discovered statement can be reviewed. Question eligibility, answer delivery, discovery logging and assessment rules are unchanged. Nearby guidance uses nested capture suppression and is excluded from saved photographs.

VendorFollowupTest includes current-camera recovery/replacement, proximity visibility, blocked-approach explanation, capture suppression, ready/completed hints, actual controller A input and the existing statement/deduction/reset checks. Reports and native hint captures are saved under docs/evidence/vendor-nearby-hint-2026-10-23. Physical headset validation was not performed.

The near hint replaces the oversized world label while it is displayed, and the vendor travel waypoint yields within the interaction radius. Both return to their normal navigation roles when the player leaves without interviewing.

Validation: all 39 integration checks passed with zero failures. Script compilation and whitespace checks passed. Ready and blocked native renders were visually inspected. The offline fixture uses a stable camera at normal eye height; earlier capture/visibility failures from its untracked ground-level camera are retained in the diagnostic reports.
