# Camera aim feedback + tutorial test scene

## Goal

The Photographer's camera currently only checks whether evidence is in
frame at the moment the shutter is pressed, with no feedback beforehand.
Add continuous aim feedback (red/green viewfinder dot, white wireframe
box outline on the aimed-at object) and block the shutter entirely when
no valid target is in frame. Build and verify all of this in a new,
isolated tutorial scene before wiring it into the main CSI_Environment
scene.

## Components

### 1. `PhotographTool` — continuous aim state
Raycast (existing `aimOrigin`/`maxDistance` params) runs every frame
while `isAiming`, not just on shutter press. Exposes:
- `bool CanCapture`
- `EvidenceProp CurrentAimTarget` (null when nothing valid)
- `event Action<bool> OnAimValidityChanged`
- `event Action<EvidenceProp> OnAimTargetChanged`

`TakePhoto()` reads this cached state instead of re-raycasting. When
`CanCapture` is false, the shutter is a no-op: no flash, no sound, no
evidence marked.

### 2. `AimIndicatorUI` — red/green dot
Standalone `MonoBehaviour` on the viewfinder world-space canvas.
Serialized reference to a `PhotographTool`, a `UnityEngine.UI.Image`
dot, and two colors (red/green). Subscribes to `OnAimValidityChanged`
in `OnEnable`, sets the dot color immediately on enable to match
current state. No other coupling to `PhotographTool` internals, so the
same component works for any future tool exposing the same
event/property shape.

### 3. `AimTargetOutline` — white wireframe box
`MonoBehaviour` on the camera tool. Subscribes to
`OnAimTargetChanged`. On a non-null target: computes the target's
renderer bounds, positions/sizes a pooled wireframe marker (single
`LineRenderer`, 16-point path tracing all 12 cube edges, white,
unlit) to fit those bounds, and shows it. On null: hides the marker.
Marker updates its transform every frame while a target is active (in
case the target or player moves).

### 4. Prefab-ize the camera tool
Extract the camera tool GameObject hierarchy (mesh, aim pivot,
viewfinder canvas + new dot, outline marker, all scripts wired) into
`Assets/_Project/Prefabs/Interaction/PhotographTool.prefab`. Both the
tutorial scene and (later) CSI_Environment reference this one prefab
instance, so future tuning happens once.

### 5. Tutorial test scene
New scene `Assets/_Project/Scenes/Scenarios/Tutorial_ToolTest.unity`:
- Enclosed room built from generic primitives only (cube walls/floor/
  ceiling, no art packages) — just enough to block light and give
  walkable bounds.
- 3-4 `EvidenceProp` cubes (primitives) at varying distances/angles.
- Minimal `EvidenceStateManager` + throwaway `EvidenceDefinition`
  assets for those props.
- Player rig, `PlayerToolRegistry`, `ToolWheelController` copied from
  CSI_Environment via the Unity Editor (not hand-edited YAML).
- The `PhotographTool` prefab equippable from the wheel.

This is where red/green + outline + shutter-block get verified before
anything touches CSI_Environment.

## Out of scope
- Wiring the finished prefab into CSI_Environment (follow-up once
  verified in the tutorial scene).
- Any tool other than the camera/Photographer.
- Outline/indicator styling beyond flat white wireframe / red-green
  dot (no animation, glow, etc.).
