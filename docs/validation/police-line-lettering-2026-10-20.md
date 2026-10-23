# Printed police cordon tape — 2026-10-20

Added bold black `POLICE LINE DO NOT CROSS` lettering on yellow tape at the house entrance, side door and outer cordon. The current scene has 10 matching tape strips. Long strips repeat the phrase at approximately .8 m intervals; short door seals show one full phrase. Opposite broad faces have separate UV orientation so the words read correctly from both sides. Thin edges retain the original yellow material.

Persistent assets are in `Assets/_Project/Art/Textures/PoliceTape/`: one 2048×128 texture with mipmaps, two mesh orientations and three shared repeat materials. `Tools/GeneratePoliceTapeTexture.ps1` regenerates the precise lettering. `PoliceCordonLetteringSetup.cs` applies it through `Tools > ViCSI > Scene Setup > Add Police Line Tape Lettering`. The existing house/yard setup also applies lettering when rebuilding cordons.

Validation: inspected native [front-door](../evidence/police-line-lettering-2026-10-20/front-door.png), [outer-front](../evidence/police-line-lettering-2026-10-20/outer-front.png), [outer-back](../evidence/police-line-lettering-2026-10-20/outer-back.png) and [side-door](../evidence/police-line-lettering-2026-10-20/side-door.png) renders. Front/back lettering is upright and readable. Comparing all Transform, BoxCollider, CapsuleCollider and MonoBehaviour scene records against the preserved before-scene found **zero changes**. Positions, passage widths and access rules are preserved. Code whitespace checks pass; Unity editor compilation succeeded. No behavioral suites were rerun for this visual change.

The saved scene contains the lettering. A subsequent editor dirty flag was preserved during read-only previews; the temporary camera/additive scene were closed. The first menu preview declined that dirty scene, with its diagnostic retained; native capture then rendered without saving or discarding that state. Physical headset readability remains manual.

Scene SHA256: `F595FA0F19DAC9FBDD695C2393C7A5CB5D717A9C5C845508CF3E21181C2368B0`.
