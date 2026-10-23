# Evidence transport bin wiring — 2026-10-10

The collection bin previously used a primitive cube renderer. It now uses the supplied `Assets/_Project/Art/Environments/MODELS/NEW/EVIDENCE_TRANSPORT_BIN.glb` through a linked `TransportBinVisual` child of `EvidenceHoldingCrate` in `CSI_Environment`.

The root remains at (0.15, 0.55, -0.60). The visual fits the existing footprint with uniform model scaling, approximately 0.45 m wide, 0.22 m high and 0.32 m deep. The root's nonuniform scale is compensated on the child. Only the old renderer is disabled; the deposit component and trigger remain identical. Source GLB unchanged.

Focused checks passed: imported model has five renderers; rendered preview shows the open transport bin and authored evidence label; saved-scene reload confirms the linked model, hidden placeholder and active trigger. Unity reports no C# compilation failure; the scene is clean outside Play mode. Static block comparisons verify all previous scene objects/settings are preserved apart from the renderer and visual-child link. No gameplay tests or full regression were run for this visual-only change.

Changed: CSI_Environment.unity; EvidenceTransportBinSetup.cs and its meta; taskbycodex.md; VR-CSI_SESSION_HANDOFF.md; this report and preview/validation artifacts. Supplied GLB/meta reused without editing.

Quest scene lighting, size and physical approach/deposit acceptance remain pending. No commit, push or deployment.
