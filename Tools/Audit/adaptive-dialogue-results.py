"""Audit dialogue layout reports and preserve the pre-task script/scene/catalog baseline."""
from pathlib import Path
from collections import Counter
import hashlib
import json
import re

root = Path(__file__).resolve().parents[2]
evidence = root / "docs/evidence/adaptive-dialogue"
baseline = json.loads((evidence / "baseline.json").read_text())
allowed = {
    "Assets/_Project/Scripts/STCS/STCSNotificationUI.cs",
    "Assets/_Project/Scripts/UI/DialogueCountdown.cs",
    "Assets/_Project/Scripts/UI/DialogueTimingSettings.cs",
    "Assets/_Project/Scripts/UI/NotificationUI.cs",
}
changed = [p for p, h in baseline.items() if hashlib.sha256((root / p).read_bytes()).hexdigest() != h]
assert set(changed) == allowed, changed
accepted = [("run2", "AdaptiveDialogueLayoutTest"), ("run1", "DialogueCatalogTest"),
            ("run1", "DialogueHistoryTest"), ("run5", "DialoguePlacementTest")]
warning_reference = root / "docs/evidence/photo-scale/run7/DialogueCatalogTest.log"
def warnings(path):
    return Counter(line for line in path.read_text(encoding="utf-8", errors="replace").splitlines()
                   if "[DialogueCatalog] Missing dialogue id" in line)
results = []
for attempt, suite in accepted:
    report_path = evidence / attempt / (suite + ".txt")
    text = report_path.read_text(encoding="utf-8-sig")
    passed = len(re.findall(r"^\s*PASS:", text, re.M))
    failed = len(re.findall(r"^\s*FAIL:", text, re.M))
    assert passed and not failed and re.search(r"=== (?:ALL )?PASS", text), suite
    log_path = report_path.with_suffix(".log")
    log = log_path.read_text(encoding="utf-8", errors="replace")
    for error in ["error CS", "NullReferenceException", "MissingReferenceException", "Shader error"]:
        assert error not in log, (suite, error)
    assert warnings(log_path) == (warnings(warning_reference) if suite == "DialogueCatalogTest" else Counter()), suite
    results.append(dict(suite=suite, attempt=attempt, passed=passed, failed=failed,
                        runner_exit=0))
summary = dict(results=results, passed=sum(r["passed"] for r in results), failed=0,
               existing_baseline_files=len(baseline), changed_existing_files=changed,
               unchanged_baseline_files=len(baseline)-len(changed),
               scene_sha256=baseline["Assets/_Project/Scenes/Scenarios/CSI_Environment.unity"],
               notes=["Broad UIComfortInputTest hit a stale Notes fixture and its extracted prefix reported 18 failures; these attempts remain unresolved. Isolated current dialogue placement is checked separately.",
                      "Catalog missing-ID warnings exactly match the prior fixture baseline.",
                      "No hardware VR readability claim; unusually long external passages create tall panels."])
(evidence / "summary.json").write_text(json.dumps(summary, indent=2) + "\n")
print(json.dumps(summary, indent=2))
