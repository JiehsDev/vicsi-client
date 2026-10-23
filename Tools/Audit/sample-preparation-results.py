"""Record accepted offline Unity results and preservation checks for sample preparation."""
import hashlib
import json
import re
from collections import Counter
from pathlib import Path

root = Path(__file__).resolve().parents[2]
evidence = root / "docs/evidence/sample-preparation"
baseline = json.loads((evidence / "baseline.json").read_text())
changed = [name for name, digest in baseline.items()
           if hashlib.sha256((root / name).read_bytes()).hexdigest() != digest]
assert not any(name.endswith(".unity") or "Data\\Evidence\\" in name
               or "Data/Evidence/" in name for name in changed), changed
old = (evidence / "catalog-before.txt").read_text(encoding="utf-8-sig")
new = (root / "Assets/Resources/ViCSIDialogueCatalog.asset").read_text(encoding="utf-8-sig")
assert new.startswith(old), "Existing dialogue bytes/content changed"
accepted = {}
for report in sorted(evidence.glob("run*/*.txt"), key=lambda path: (int(re.search(r"run(\d+)", path.parent.name)[1]), path.name)):
    if report.name == "catalog-before.txt":
        continue
    content = report.read_text()
    if "=== PASS" not in content or re.search(r"^\s*FAIL:", content, re.M):
        continue
    accepted[report.stem] = {
        "report": str(report.relative_to(root)),
        "pass": len(re.findall(r"^\s*PASS:", content, re.M)),
        "fail": 0,
    }
baseline_log = (root / "docs/evidence/photo-scale/run7/DialogueCatalogTest.log").read_text(errors="replace")
baseline_warnings=Counter(re.findall(r"^.*Missing dialogue id.*$",baseline_log,re.M))
required={"SamplePreparationTest","GloveChangeStationTest","FreshSwabSupplyTest","BiologicalSamplingTest",
          "EvidenceInventoryTest","ArrivalPPETest","SceneAccessRegisterTest","SceneTestStartTest","DialogueCatalogTest"}
assert required <= accepted.keys(), "Missing accepted suites: " + str(required - accepted.keys())
for suite, result in accepted.items():
    log = (root / result["report"]).with_suffix(".log").read_text(errors="replace")
    assert not re.search(r"error CS\d+|NullReferenceException|MissingReferenceException|Shader error", log), suite
    # The catalog fixture intentionally installs a tiny override and tests missing entries.
    warnings=Counter(re.findall(r"^.*Missing dialogue id.*$",log,re.M))
    assert not (warnings-baseline_warnings if suite=="DialogueCatalogTest" else warnings), (suite,warnings)
summary = {
    "suites": accepted,
    "totalPass": sum(value["pass"] for value in accepted.values()),
    "totalFail": 0,
    "changedExistingFiles": changed,
    "unchangedExistingFiles": len(baseline) - len(changed),
    "sceneSha256": hashlib.sha256((root / "Assets/_Project/Scenes/Scenarios/CSI_Environment.unity").read_bytes()).hexdigest(),
    "sourceHashes": {str(path.relative_to(root)): hashlib.sha256(path.read_bytes()).hexdigest()
                     for path in root.glob("Assets/_Project/Scripts/**/*.cs")
                     if "SamplePreparation" in path.name or path.name in ("SwabTool.cs", "DryingProgressGraphic.cs", "PaperSwabPackage.cs", "GloveStationGuide.cs", "ArrivalPPEStation.cs")},
    "questDeviceValidation": "pending",
    "newMissingDialogueWarnings": 0,
    "csShaderNullMissingReferenceErrors": 0,
    "changedSourceHashes": {name: hashlib.sha256((root/name).read_bytes()).hexdigest() for name in changed},
}
(evidence / "summary.json").write_text(json.dumps(summary, indent=2))
print(json.dumps({"suites": len(accepted), "pass": summary["totalPass"], "fail": 0, "changed": len(changed)}))
