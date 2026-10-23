# Flexible inventory UI

The scene-authored inventory used 100-unit rows, a horizontal group that reserved several 100-unit columns, and fixed 50-unit text cells. Name, marker, status and lock-reason text could therefore overlap or overflow. ContentSizeFitter grew the list without a bounded scrolling viewport.

EvidenceInventoryUI now installs the existing ResponsivePanelLayout at runtime. The panel grows between 240 and 740 units; overflow uses a clipped vertical scroll area with pinned header and control instructions. Empty inventory uses the same padded text sizing. Shared HudFollowCamera placement remains in use.

EvidenceInventoryEntryView measures all visible text at its available width, stacks the fields with 8-unit gaps, and sets its own preferred height. Each row keeps 20-unit horizontal/16-unit vertical padding and the list keeps 12-unit row gaps. Name text is 28 units and details are 24; text wraps rather than shrinking. Narrowing the panel increases row height. The duplicate name in the narrow placeholder-icon slot is hidden, while serialized icon references remain. The selection bar and equipped indicator fit inside the resized row.

Up/down still selects items; selection brings the row into view. Left/right scrolls long content, and pointer scroll arrows are available. Controller-ray selection covers the full resized row and respects viewport clipping. A equips/returns the selected item and B closes as before. Refreshing after custody changes preserves the selected item where possible and removes inactive old rows from layout immediately.

No scene/prefab resave or manual Editor wiring is required. Inventory custody, sample storage, deposit, procedural gates, scoring and dashboard data are unchanged. No commit, push or deployment is performed.

## Validation

InventoryUILayoutTest exercises the real scene-authored panel with six synthetic sealed items admitted through EvidenceInventory.Add. It checks long names and lock reasons, actual rendered TMP glyph bounds, fixed readable font sizes, independent row heights, padding/gaps, narrower widths, bounded scrolling, selection visibility, clipped pointer hits, ray selection, locked actions, close/reopen and empty-state restoration. EvidenceInventoryTest separately covers equip, unequip, deposit and inventory gates.

Accepted results: **51 passed, zero failures** (InventoryUILayoutTest 22; EvidenceInventoryTest 29). Reports are under `docs/evidence/inventory-flexible-ui/run1/`. Unity reports `scriptCompilationFailed=false` and zero console entries matching `error CS`. Final Editor is outside Play mode with the saved CSI scene clean, no pending diagnostic request or XR restoration, and Standalone/Android XR startup enabled. The authored background already stretches to all panel edges, so it follows the new height.

Physical Quest readability/comfort remains manual acceptance.
