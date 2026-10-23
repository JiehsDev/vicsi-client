# Lobby panel height above the desk - 2026-10-11

The user's clarification identified panel height as the problem: the UI was down near the floor, not disabled. LobbyCanvasPlacement was deriving canvas Y from eye height minus 0.05 m and tilting toward that eye position. A floor-level pose could therefore pull the panel below the desk.

The canvas now uses an editable fixed centerHeight of 1.65 m in Main and stays upright. Its 1.1 m height puts the bottom edge at 1.10 m. Saved Main's authored canvas Y is also 1.65 m. LobbyLayoutSetup reproduces this height. Horizontal placement and the existing startup player-facing behavior are retained.

Verified with normal XR-enabled Main Play:

- Rendered capture shows the entire panel above the desk.
- Canvas center Y 1.6500 m, bottom 1.1000 m, top 2.2000 m, pitch 0 degrees.
- A deliberate floor-level camera fixture at Y -0.0025 m left those same canvas height/orientation values unchanged after runtime placement.
- The floor-level fixture does not establish a readable view from that camera: the raised panel is naturally above its forward view, with furniture in between. It verifies that camera height cannot pull the panel down.
- Final compilation succeeded; scriptCompilationFailed=False. Existing package/XR warnings remain. XR initialization settings remain enabled.

Evidence: docs/evidence/lobby-panel-height/authored-state.txt, play-state.txt, play-view.png and floor-eye-check.txt.

No navigation, identity, session, upload or scenario gameplay changes; no full regression, commit, push, build/install or deployment. Physical headset acceptance remains pending.
