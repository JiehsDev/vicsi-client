"""Feature-start preservation audit for glove replacement, without a clean-git assumption."""
import hashlib
import json
from pathlib import Path
import re

project=Path(__file__).resolve().parents[2]
root=project/"docs/evidence/glove-change"
before=json.loads((root/"baseline.json").read_text())
changed=[name for name,digest in before.items() if not (project/name).exists()
         or hashlib.sha256((project/name).read_bytes()).hexdigest()!=digest]
allowed={"Assets/Resources/ViCSIDialogueCatalog.asset",
         "Assets/_Project/Scripts/SceneTurnover/ArrivalPPEStation.cs",
         "Assets/_Project/Scripts/Interaction/SwabTool.cs",
         "Assets/_Project/Scripts/Dialogue/VendorPathGuide.cs",
         "Assets/_Project/Scripts/Interaction/PhotoCaptureListener.cs",
         "Assets/_Project/Scripts/Logging/SessionEventType.cs",
         "Assets/_Project/Scripts/Testing/BiologicalSamplingTest.cs",
         "Assets/_Project/Scripts/Testing/GreyboxFlowTest.cs",
         "Assets/_Project/Scripts/Testing/DialogueCatalogTest.cs"}
assert set(changed)<=allowed, changed

def entries(path):
    text=path.read_text(encoding="utf-8-sig")
    blocks=list(re.finditer(r"^  - id: (.+)\n(?:(?!^  - id: ).*\n?)*",text,re.M))
    result={m.group(1):m.group(0) for m in blocks}
    assert len(result)==len(blocks), "Duplicate catalog IDs"
    return result

old=entries(root/"catalog-before.asset.txt")
new=entries(project/"Assets/Resources/ViCSIDialogueCatalog.asset")
assert all(new.get(name)==value for name,value in old.items()), "An existing catalog entry changed"
added=sorted(new.keys()-old.keys())
assert len(added)==8 and all(name.startswith("gloves-") for name in added),added
result={"baselineFiles":len(before),"changedExistingFiles":sorted(changed),"unchangedBaselineFiles":len(before)-len(changed),
        "addedDialogueIds":added,"changedOrRemovedExistingDialogueIds":[],
        "sceneSha256":hashlib.sha256((project/"Assets/_Project/Scenes/Scenarios/CSI_Environment.unity").read_bytes()).hexdigest()}
assert result["sceneSha256"]=="91d5c8c35425cdc32fec5ecb6bbc01ebfb07c368e227d62da5e10fdf12786005"
(root/"content-audit.json").write_text(json.dumps(result,indent=2)+"\n")
print(json.dumps(result,indent=2))
