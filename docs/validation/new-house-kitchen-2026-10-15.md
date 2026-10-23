# New house kitchen furniture — 2026-10-15

Added a compact light-wood dining table and two matching ladder-back chairs to the new `Vicsi_House` kitchen in `CSI_Environment`. Reused the existing Furniture Mega Pack `Table19` and `Chair02` linked prefabs.

The group is `Vicsi_House/KitchenDiningSet`, centered at world (13.05, 1.20, 7.05). Table dimensions are approximately 1.25 × 0.77 m, height 0.76 m above the raised kitchen floor. Two chairs face inward from opposite ends. The table edge leaves approximately 1.13 m to the counter fronts. All three pieces are grounded at Y=1.20 m. The existing fallen chair remains in place.

Verified placement in a scene camera render, including final chair orientation. Unity reports compilationFailed=False, outside Play mode. Scene saved successfully. Comparison against a copy of the live scene taken immediately before furnishing found no removed records; the existing house prefab record changes only to attach the new group. Eight new serialized records represent the group and three linked furniture instances. Existing unsaved scene edits were preserved in the snapshot and scene save.

Preview: `docs/evidence/new-house-kitchen/preview.png`. Bounds and save results: `docs/evidence/new-house-kitchen/placement.txt`. Repeatable Editor command: `ViCSI/Scene Setup/Furnish New House Kitchen` in `KitchenDiningSetup.cs` (refuses duplicate placement). No gameplay tests were needed for this visual addition. Physical Quest appearance and navigation were not tested.
