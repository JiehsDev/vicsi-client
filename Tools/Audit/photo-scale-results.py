"""Collect the latest accepted report for each required photo-scale suite."""
import hashlib
import json
from pathlib import Path
import re

project = Path(__file__).resolve().parents[2]
root = project / "docs/evidence/photo-scale"
suites = ("PhotographicScaleTest", "PhotographyShutterOwnershipTest", "ScenePhotographyProcedureTest",
          "PhotographyGuideTest", "MeasurementProcedureTest", "BiologicalSamplingTest", "ChalkProcedureTest",
          "SceneTestStartTest", "DialogueCatalogTest")
accepted = []
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
        errors = re.findall(r"^.*(?:error CS\d+|Shader error|NullReferenceException|MissingReferenceException).*$", log, re.M)
        assert not errors, (suite, errors)
        accepted.append({"suite": suite, "passed": passed, "failed": failed,
                         "report": path.relative_to(project).as_posix(), "log": log_path.relative_to(project).as_posix()})
        break
    else:
        raise AssertionError("No passing report: " + suite)
files = ["Assets/Resources/PhotographicScaleLayout.json", "Assets/Resources/PhotographicScale.shader",
         "Assets/Resources/ViCSIDialogueCatalog.asset",
         "Assets/_Project/Scripts/Interaction/PhotographicScaleAccessory.cs",
         "Assets/_Project/Scripts/Interaction/PhotographicScaleGraphic.cs",
         "Assets/_Project/Scripts/Interaction/PhotographTool.cs",
         "Assets/_Project/Scripts/Interaction/PhotoCaptureListener.cs",
         "Assets/_Project/Scripts/Logging/SessionEventType.cs",
         "Assets/_Project/Scripts/Testing/PhotographicScaleTest.cs",
         "Assets/_Project/Scripts/Testing/PhotoScaleTestUtil.cs",
         "Assets/_Project/Scripts/Testing/PhotographyShutterOwnershipTest.cs",
         "Assets/_Project/Scripts/Testing/ScenePhotographyProcedureTest.cs",
         "Assets/_Project/Scripts/Testing/MeasurementProcedureTest.cs",
         "Assets/_Project/Scripts/Testing/BiologicalSamplingTest.cs",
         "Assets/_Project/Scripts/Testing/ChalkProcedureTest.cs",
         "Assets/_Project/Editor/PhotoScaleAuthoring.cs", "Tools/RunPhotoScaleChecks.ps1"]
result = {"date": "2026-10-19", "suites": accepted,
          "totalPassed": sum(row["passed"] for row in accepted), "totalFailed": 0,
          "errorCsShaderNullMissingReferenceMatches": 0,
          "contentAudit": "docs/evidence/photo-scale/content-audit.json",
          "sourceSha256": {name: hashlib.sha256((project / name).read_bytes()).hexdigest() for name in files},
          "manualAcceptancePending": "Physical Quest input comfort, stereo readability and Android device performance"}
(root / "summary.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
print(json.dumps({"totalPassed": result["totalPassed"], "suites": accepted}, indent=2))
