# False wall-penetration mask

The installed Meta wall-penetration driver raycasts the logical-to-tracked head segment plus an additional 22 cm, uses the global trigger-query setting, and accepts default-layer UI colliders. The saved wall-mask mesh renderer was also enabled independently of the effect's initial disabled state.

The regression test reproduces three false activations with the original detector: an interaction trigger, a solid world-space Canvas collider on Default, and a physical wall beyond the tracked head.

Added the scene-authored `WallPenetrationViewGuard` to replace that driver's query. It ignores triggers, Canvas/RectTransform UI, player-owned colliders, Ignore Raycast/UI layers and the invisible photography barrier. Only actual crossings between the logical and tracked head positions activate the effect. The nearest valid surface determines depth. The existing SDK effect, material and FOV curve are retained for real walls; movement tunneling is separate and unchanged.

The wall-mask renderer is saved disabled and explicitly cleared at startup, on disable and whenever the head is unobstructed. Settings and references are editable on the existing WallPenetrationTunneling object. Reauthor through **Tools > ViCSI > Scene Setup > Fix Wall Penetration View Filter**. Package-cache sources were not modified. Unrelated UI RectTransform changes produced by editor layout were restored to the pre-task snapshot.

Validation: **96 checks passed, zero failures**:

| Suite | Passed |
| --- | ---: |
| WallPenetrationViewTest | 20 |
| HousePhotographyEntryGateTest | 27 |
| SettingsPauseMenuTest | 49 |

Coverage includes reproduced false activations, clearing/stale mesh lifecycle, genuine-wall activation and depth, initial hidden state, preserved movement effect, the outdoor-photo entry barrier and IOC compatibility, height/walking-speed settings, scenario restart and lobby return. Final Unity compilation succeeded; the initial missing namespace import was corrected before these accepted runs. Headset validation remains unperformed.

Evidence and the original scene snapshot are in `docs/evidence/wall-penetration-view-2026-10-22/`. Play-mode tests use the existing offline sandbox. No commit, push, deployment or production-data modifications.
