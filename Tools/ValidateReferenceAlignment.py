"""Check reference-only scene changes against the pre-adjustment snapshot."""
import json
import re
from pathlib import Path

root = Path(__file__).resolve().parents[1]
out = root / "docs/evidence/reference-alignment"


def records(path):
    return {m[2]: (m[1], m[0]) for m in re.finditer(
        r"^--- !u!(\d+) &(\d+)[^\n]*\n.*?(?=^--- !u!|\Z)",
        path.read_text(encoding="utf-8-sig"), re.M | re.S)}


before = records(out / "before/CSI_Environment.unity")
after = records(root / "Assets/_Project/Scenes/Scenarios/CSI_Environment.unity")
changed = [k for k in before.keys() & after.keys() if before[k] != after[k]]
parents, transforms, go_for = {}, {}, {}
allowed_roots = set()
for key, (kind, text) in before.items():
    go = re.search(r"^  m_GameObject: \{fileID: (\d+)\}", text, re.M)
    parent = re.search(r"^  m_Father: \{fileID: (\d+)\}", text, re.M)
    if go:
        go_for[key] = go[1]
    if parent and go:
        parents[key] = parent[1]
        transforms[go[1]] = key
    if "Assembly-CSharp::SceneReferencePoint" in text or "Assembly-CSharp::CrimeSceneSketchTemplate" in text:
        allowed_roots.add(go[1])


def allowed(key):
    kind, text = before[key]
    if "Assembly-CSharp::MeasurementStateManager" in text:
        return text.replace("measurementLayoutId: new-house-v2", "measurementLayoutId: new-house-v3") == after[key][1]
    go = key if kind == "1" else go_for.get(key)
    transform = transforms.get(go)
    while transform:
        if go_for.get(transform) in allowed_roots:
            return True
        transform = parents.get(transform)
    return False


checks = {
    "no_scene_records_added": not after.keys() - before.keys(),
    "no_scene_records_removed": not before.keys() - after.keys(),
    "only_references_sketch_and_layout_changed": all(allowed(k) for k in changed),
}
result = {"checks": checks, "changedRecords": changed,
          "unexpectedRecords": [k for k in changed if not allowed(k)]}
(out / "preservation.json").write_text(json.dumps(result, indent=2))
print(json.dumps(result, indent=2))
assert all(checks.values()), "Unexpected scene mutation"
