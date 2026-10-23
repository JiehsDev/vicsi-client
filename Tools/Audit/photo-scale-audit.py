"""Audit the photo-scale task against its saved catalog and scene baseline."""
import hashlib
import json
from pathlib import Path
import re

project = Path(__file__).resolve().parents[2]
evidence = project / "docs/evidence/photo-scale"


def entries(path):
    text = path.read_text(encoding="utf-8-sig")
    matches = list(re.finditer(r"^  - id: (.+)\n(?:(?!^  - id: ).*\n?)*", text, re.M))
    result = {match.group(1): match.group(0) for match in matches}
    assert len(result) == len(matches), "Duplicate dialogue IDs"
    return result


before = entries(evidence / "catalog-before.asset.txt")
after = entries(project / "Assets/Resources/ViCSIDialogueCatalog.asset")
changed = sorted(key for key in before if before[key] != after.get(key))
added = sorted(after.keys() - before.keys())
removed = sorted(before.keys() - after.keys())
expected = {"photo-scale-" + name for name in (
    "place-prompt", "placed", "saved", "first-original", "required", "frame",
    "remove-for-original", "prepare-item", "move-closer")}
scene_hash = hashlib.sha256((project / "Assets/_Project/Scenes/Scenarios/CSI_Environment.unity").read_bytes()).hexdigest()
result = {"changedExistingEntryIds": changed, "addedEntryIds": added,
          "removedEntryIds": removed, "sceneSha256": scene_hash}
assert not changed and not removed and set(added) == expected, result
assert scene_hash == "91d5c8c35425cdc32fec5ecb6bbc01ebfb07c368e227d62da5e10fdf12786005", result
(evidence / "content-audit.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
print(json.dumps(result, indent=2))
