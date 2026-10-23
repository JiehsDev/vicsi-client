"""Audit the entrance/staging feature against its pre-task snapshot and real play reports."""
from pathlib import Path
import hashlib
import json
import re

root = Path(__file__).resolve().parents[2]
evidence = root / "docs/evidence/entrance-staging"
baseline = json.loads((evidence / "baseline.json").read_text())
allowed = {
 "Assets/_Project/Editor/Phase5PlayModeBatchRunner.cs",
 "Assets/_Project/Scripts/Interaction/EvidenceBoxDepositZone.cs",
 "Assets/_Project/Scripts/SceneTurnover/GloveStationGuide.cs",
 "Assets/_Project/Scripts/SceneTurnover/SceneCordon.cs",
 "Assets/_Project/Scripts/Testing/ArrivalEntryTest.cs",
 "Assets/_Project/Scripts/Testing/ArrivalPPETest.cs",
 "Assets/_Project/Scripts/Testing/FingerprintComparisonTest.cs",
 "Assets/_Project/Scripts/Testing/SceneAccessRegisterTest.cs",
 "Assets/_Project/Scenes/Scenarios/CSI_Environment.unity",
 "Assets/Resources/GloveStationRoute_CSI.json",
}
changed = [p for p, h in baseline.items() if hashlib.sha256((root / p).read_bytes()).hexdigest() != h]
assert set(changed) == allowed, changed
scene = "Assets/_Project/Scenes/Scenarios/CSI_Environment.unity"
def blocks(path):
 text = path.read_text(encoding="utf-8")
 return {m.group(1): m.group(2) for m in re.finditer(r"^--- !u!\d+ &(-?\d+)(?: stripped)?\s*\n(.*?)(?=^--- !u!|\Z)", text, re.M | re.S)}
a = blocks(evidence / "baseline-files" / scene)
b = blocks(root / scene)
changed_blocks = [key for key in a if key in b and a[key] != b[key]]
assert set(changed_blocks) == {"121411599", "1311940559", "1311940560", "1479794820", "1705414296", "1775574804"}, changed_blocks
assert not (a.keys() - b.keys())
assert len(b.keys() - a.keys()) == 4
route_path = "Assets/Resources/GloveStationRoute_CSI.json"
old_route = json.loads((evidence / "baseline-files" / route_path).read_text())
route = json.loads((root / route_path).read_text())
assert old_route["waypoints"][:7] == route["waypoints"][:7]
assert route["waypoints"][-1] == dict(x=16.9, y=.03, z=.75)
for key in old_route:
 if key != "waypoints": assert old_route[key] == route[key]
accepted = [("run2", "HouseEntranceStagingTest")] + [("run3", name) for name in ["ArrivalEntryTest", "ArrivalPPETest", "SceneAccessRegisterTest", "GloveChangeStationTest", "FreshSwabSupplyTest", "SamplePreparationTest"]] + [("run4", "FingerprintComparisonTest")]
results = []
for attempt, suite in accepted:
 report = evidence / attempt / (suite + ".txt")
 text = report.read_text(encoding="utf-8-sig")
 passed = len(re.findall(r"^\s*PASS:", text, re.M))
 failed = len(re.findall(r"^\s*FAIL:", text, re.M))
 assert passed and not failed and "=== PASS" in text, suite
 log = report.with_suffix(".log").read_text(encoding="utf-8", errors="replace")
 for error in ["error CS", "NullReferenceException", "MissingReferenceException", "Shader error", "[DialogueCatalog] Missing dialogue id"]:
  assert error not in log, (suite, error)
 results.append(dict(suite=suite, attempt=attempt, passed=passed, failed=failed, exit_code=0))
for backup in (evidence / "legacy-backup").rglob("*"):
 if backup.is_file(): assert (root / backup.relative_to(evidence / "legacy-backup")).read_bytes() == backup.read_bytes(), backup
summary = dict(results=results, passed=sum(r["passed"] for r in results), failed=0,
 existing_baseline_files=len(baseline), changed_existing_files=changed,
 unchanged_baseline_files=len(baseline)-len(changed), changed_old_scene_blocks=changed_blocks,
 added_scene_blocks=sorted(b.keys()-a.keys()), removed_scene_blocks=[],
 scene_sha256=hashlib.sha256((root/scene).read_bytes()).hexdigest(),
 notes=["Cordon at the house steps z=2.7; short entry strip to inner footprint z=3.0.",
 "Work tables, box and approach routes inside property fence; captured previews inspected.",
 "Initial authoring floor clearance, trigger-only crate fixture, preauthorized arrival fixture and old box-proximity assertion attempts are retained.",
 "Physical headset reach, stereo comfort and performance remain manual."])
(evidence / "summary.json").write_text(json.dumps(summary, indent=2) + "\n")
print(json.dumps(summary, indent=2))
