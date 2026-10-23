"""Build an explicit acceptance manifest without counting superseded attempts."""
import json
import re
from pathlib import Path

root = Path(__file__).resolve().parents[1]
folder = root / "docs/evidence/relationship-review"
def read_report(path):
    data = path.read_bytes()
    return data.decode("utf-16" if data.startswith((b"\xff\xfe", b"\xfe\xff")) else "utf-8-sig").replace("\r\n", "\n")

selected = {
    "EvidenceRelationshipsReviewTest": "accepted",
    "FinalDeductionAssessmentTest": "final",
    "ReasoningCheckpointTest": "run2",
    "VendorFollowupTest": "run2",
    "EndingManagerTest": "accepted",
    "SOCOCloseoutTest": "run2",
    "SettingsPauseMenuTest": "run2",
    "AssessmentUploadServiceTest": "run2",
    "StoryContentTest": "accepted",
    "BrotherPathTest": "accepted-cases",
    "InconclusivePathTest": "accepted-cases",
}
suites = []
for name, directory in selected.items():
    path = folder / directory / (name + ".txt")
    report = read_report(path)
    passed = len(re.findall(r"^\s*PASS:", report, re.M))
    failed = len(re.findall(r"^\s*FAIL:", report, re.M))
    assert passed and not failed and "=== PASS" in report, str(path)
    suites.append(dict(suite=name, passed=passed, failed=failed,
                       report=path.relative_to(root).as_posix()))

backend = []
for name in ["backend-current-final", "scoring-final-validation",
             "dashboard-final-validation", "upload-parity-final", "backend-unity-scoped"]:
    path = folder / (name + ".txt")
    report = read_report(path)
    counts = {key: int(re.search(r"^# " + key + r" (\d+)$", report, re.M)[1])
              for key in ["tests", "pass", "fail", "skipped"]}
    assert counts["fail"] == 0
    backend.append(dict(report=path.relative_to(root).as_posix(), **counts))

editor = json.loads((folder / "editor-final.json").read_text(encoding="utf-8-sig"))
assert not editor["play"] and not editor["compiling"] and not editor["compilationFailed"]
assert not editor["dirty"] and not editor["pending"] and not editor["requestPending"]
assert editor["standaloneXR"] and editor["androidXR"]
assert editor["activeNodes"] == 0 and editor["activeLines"] == 0 and not editor["boardActive"]

manifest = dict(
    feature="Evidence Relationships Review", date="2026-10-17",
    weights=dict(reasoningCheckpoints=60, evidenceRelationships=25, finalConclusion=15),
    unitySuites=suites, unityPassed=sum(x["passed"] for x in suites), unityFailed=0,
    backendRuns=backend,
    note="Backend runs overlap; do not sum them as unique tests. Superseded diagnostics are retained outside this selection.",
    staticChecks=dict(typescript="passed", scopedESLint="passed"),
    editor=editor,
    questions="docs/validation/relationship-review-questions-2026-10-17.md",
    wireArtifacts=["upload-wire.json", "completed-upload-wire.json"],
    limitations=[
        "Physical Quest stereo/comfort/controller acceptance remains manual.",
        "Live Supabase upload was not performed; serialized envelopes and client/server scoring parity were tested locally.",
        "Two optional historical frozen-file audits mismatch older ground-truth/source hashes; preserved in backend-scoring-final.txt, not rewritten.",
        "Repeated Editor reloads exhausted the 8 GB test machine; saved project restarted for the final case reruns.",
    ],
    commit=False, push=False, deploy=False,
)
(folder / "summary.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
print(f'{manifest["unityPassed"]} Unity assertions passed; all selected reports accepted.')
