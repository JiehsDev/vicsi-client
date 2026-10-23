# Lobby room restyle — 2026-10-11

Restyled the existing Main lobby with pale plaster and limestone finishes, navy wall panels, oak fluting and trim, brass accents, sage sofas, a lounge rug, a round coffee table, books, a reception desk and chair, four planters, and four framed abstract prints. Two pendant fixtures and emissive perimeter strips accompany four warm/neutral realtime point lights, with shadows disabled. Replaced the oversized legacy fluorescent fixtures by disabling their five lobby roots.

The original approximately 8 × 8 metre footprint and four-metre ceiling remain. The reception top is at 0.945 m, below the existing menu's 1.10 m bottom edge. The dark feature wall provides a quiet background for the transparent menu without overlapping wall signage.

`LobbyRoomSetup` provides a repeatable editor menu command: ViCSI → Scene Setup → Restyle Lobby Room. It operates only on Main outside Play, preserves a pre-edit scene copy under Temp, groups changes for Undo, and replaces only its own `LobbyInteriorDesign` root when rerun. Materials use the project's URP Lit shader and GPU instancing. Existing furniture meshes are reused from Furniture Mega Pack. The separate Harong building is retained.

Validation: Unity applied the scene successfully with compilationFailed=False and sceneDirty=False. Manual camera renders were inspected and corrected for inward-facing wall art and readable menu background. A comparison against the pre-edit scene found all 48 existing button component blocks unchanged and all 185 existing Transform/RectTransform blocks unchanged. Added room objects contain 243 mesh renderers and four lights. Normal Play verification is recorded in the evidence folder. Physical headset appearance and performance remain unverified.

Evidence: `docs/evidence/lobby-room-restyle/`. No full regression, commit, push, build/install, or deployment.

Normal XR-enabled Play settled at frame 1497: compilationFailed=False, Main dirty=False, camera/player yaw 179.61 degrees, tracking yaw zero, menu enabled and active at Y1.6500 m with bottom1.1000/top2.2000. The player-camera render shows the menu above the reception desk and the new furniture and lights. Validation used the Editor's untracked preview; no HMD was connected. Returned to Main outside Play after capture.
