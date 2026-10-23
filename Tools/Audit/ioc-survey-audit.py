"""Compare this task's saved catalog baseline with current authored content."""
import hashlib
import json
from pathlib import Path
import re

project = Path(__file__).resolve().parents[2]
evidence = project / "docs/evidence/ioc-survey"


def entries(path):
    text = path.read_text(encoding="utf-8-sig")
    return {
        match.group(1): match.group(0)
        for match in re.finditer(r"^  - id: (.+)\n(?:(?!^  - id: ).*\n?)*", text, re.M)
    }


before = entries(evidence / "catalog-before.asset.txt")
after = entries(project / "Assets/Resources/ViCSIDialogueCatalog.asset")
changed = [key for key in before if before[key] != after.get(key)]
expected = {"ioc-assessment-guidance", "ioc-observation-bottle", "ioc-observation-survey"}
for key in expected:
    metadata = lambda value: re.sub(r"    text: .*?\n    duration: [^\n]*\n", "", value, flags=re.S)
    assert metadata(before[key]) == metadata(after[key]), f"Unexpected metadata change: {key}"
lines = []
for key in sorted(expected):
    entry = after[key]
    text = re.search(r"    text: (.*?)\n    duration:", entry, re.S).group(1)
    text = " ".join(text.split())
    duration = float(re.search(r"    duration: (.+)", entry).group(1))
    lines.append({"id": key, "text": text, "words": len(text.split()),
                  "durationSeconds": duration,
                  "wordsPerMinute": round(len(text.split()) * 60 / duration, 1)})
scene_hash = hashlib.sha256(
    (project / "Assets/_Project/Scenes/Scenarios/CSI_Environment.unity").read_bytes()
).hexdigest()
result = {
    "changedEntryIds": changed,
    "addedEntryIds": sorted(after.keys() - before.keys()),
    "removedEntryIds": sorted(before.keys() - after.keys()),
    "sceneSha256": scene_hash,
    "dialogue": lines,
}
assert set(changed) == expected, result
assert before.keys() == after.keys(), result
assert scene_hash == "91d5c8c35425cdc32fec5ecb6bbc01ebfb07c368e227d62da5e10fdf12786005", result
(evidence / "content-audit.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
print(json.dumps(result, indent=2))
