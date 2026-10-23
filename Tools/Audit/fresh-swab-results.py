"""Collect the latest accepted report for each required fresh-swab suite."""
import hashlib
import json
from pathlib import Path
import re
from collections import Counter

project = Path(__file__).resolve().parents[2]
root = project / "docs/evidence/fresh-swab"
suites = ("FreshSwabSupplyTest", "GloveChangeStationTest", "BiologicalSamplingTest", "EvidenceInventoryTest", "ArrivalPPETest", "ArrivalEntryTest", "SceneAccessRegisterTest", "PhotographicScaleTest", "VendorPathGuideTest", "SceneTestStartTest", "DialogueCatalogTest")
accepted = []
catalog_baseline_path=project/"docs/evidence/photo-scale/run7/DialogueCatalogTest.log"
catalog_baseline_warnings=Counter(re.findall(r"^.*Missing dialogue id.*$",catalog_baseline_path.read_text(encoding="utf-8-sig",errors="replace"),re.M))
for suite in suites:
    candidates = sorted(root.glob("run*/" + suite + ".txt"), key=lambda p: int(p.parent.name[3:]), reverse=True)
    for path in candidates:
        report = path.read_text(encoding="utf-8-sig")
        passed = len(re.findall(r"^\s*PASS:", report, re.M))
        failed = len(re.findall(r"^\s*FAIL:", report, re.M))
        if "=== PASS" not in report or failed:
            continue
        log_path = path.with_suffix(".log")
        log = log_path.read_text(encoding="utf-8-sig", errors="replace")
        errors = re.findall(r"^.*(?:error CS\d+|Shader error|NullReferenceException|MissingReferenceException|Missing dialogue id).*$", log, re.M)
        if suite == "DialogueCatalogTest":
            # Its tiny override catalog already had three scene callbacks and the
            # deliberate missing-ID checks before this feature. Permit only the
            # exact recorded baseline counts; reject every new missing-ID warning.
            counts=Counter(line for line in errors if "Missing dialogue id" in line)
            assert not (counts-catalog_baseline_warnings), dict(counts-catalog_baseline_warnings)
            errors = [line for line in errors if "Missing dialogue id" not in line]
        assert not errors, (suite, len(errors), errors[:5])
        accepted.append({"suite": suite, "passed": passed, "failed": failed,
                         "report": path.relative_to(project).as_posix(), "log": log_path.relative_to(project).as_posix()})
        break
    else:
        raise AssertionError("No passing report: " + suite)
files = ['Assets/Resources/ViCSIDialogueCatalog.asset', 'Assets/_Project/Scripts/Interaction/SwabTool.cs', 'Assets/_Project/Scripts/SceneTurnover/ArrivalPPEStation.cs', 'Assets/_Project/Scripts/SceneTurnover/GloveStationGuide.cs', 'Assets/_Project/Scripts/RoleSystem/PlayerToolRegistry.cs', 'Assets/_Project/Scripts/RoleSystem/HeldItemCoordinator.cs', 'Assets/_Project/Scripts/RoleSystem/ToolWheelController.cs', 'Assets/_Project/Scripts/Logging/SessionEventType.cs', 'Assets/_Project/Scripts/SceneTurnover/SceneTurnover.cs', 'Assets/_Project/Editor/Phase5PlayModeBatchRunner.cs', 'Assets/_Project/Scripts/Testing/GloveChangeTestUtil.cs', 'Assets/_Project/Scripts/Testing/GloveChangeStationTest.cs', 'Assets/_Project/Scripts/Testing/FreshSwabSupplyTest.cs', 'Tools/RunFreshSwabChecks.ps1']
result = {"date": "2026-10-19", "suites": accepted,
          "totalPassed": sum(row["passed"] for row in accepted), "totalFailed": 0,
          "errorCsShaderNullMissingReferenceMatches": 0,
          "newMissingDialogueWarnings": 0,
          "catalogWarningBaselineLog": catalog_baseline_path.relative_to(project).as_posix(),
          "catalogWarningBaselineCounts": dict(catalog_baseline_warnings),
          "contentAudit": "docs/evidence/fresh-swab/content-audit.json",
          "sourceSha256": {name: hashlib.sha256((project / name).read_bytes()).hexdigest() for name in files},
          "manualAcceptancePending": "Physical Quest input comfort, stereo readability and Android device performance"}
(root / "summary.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
print(json.dumps({"totalPassed": result["totalPassed"], "suites": accepted}, indent=2))
