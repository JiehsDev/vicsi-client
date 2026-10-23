// Assets/_Project/Scripts/Testing/PerEvidenceMeasurementTest.cs
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Per-evidence rectangular measurement: every item is measured from its OWN two nearby
/// fixed references (wall / built-in counter faces, perpendicular distances), and only
/// those count for it. Drives MeasurementStateManager and the geometry directly; the
/// tape-measure gestures are covered by MeasurementToolTest. Fresh play session.
/// </summary>
public class PerEvidenceMeasurementTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }

    private readonly List<string> failures = new();
    private readonly StringBuilder report = new();

    private static readonly string[] Items = { "BODY-A", "EVD-014", "EVD-015", "EVD-018" };

    public void RunAllTests()
    {
        IsDone = false;
        StartCoroutine(Run());
    }

    private void Check(string label, bool ok)
    {
        report.AppendLine((ok ? "  PASS: " : "  FAIL: ") + label);
        if (!ok)
        {
            failures.Add(label);
        }
    }

    private IEnumerator Run()
    {
        report.AppendLine("=== PerEvidenceMeasurementTest ===");
        var m = MeasurementStateManager.Instance;
        var esm = EvidenceStateManager.Instance;
        if (m == null || esm == null)
        {
            Report = "FAIL: managers missing.";
            IsDone = true;
            yield break;
        }

        // --- Independent references ---
        var all = new HashSet<string>();
        foreach (var id in Items)
        {
            var r = MeasurementTestUtil.RequirementFor(id);
            var a = SceneReferencePoint.Find(r.referenceAId);
            var b = SceneReferencePoint.Find(r.referenceBId);
            all.Add(r.referenceAId);
            all.Add(r.referenceBId);
            Check($"[{id}] own pair resolves: '{a?.displayName}' + '{b?.displayName}' (plane references with zones)",
                a != null && b != null && a.shape == SceneReferencePoint.ReferenceShape.Plane && b.shape == SceneReferencePoint.ReferenceShape.Plane
                && a.Zone != null && b.Zone != null && !string.IsNullOrEmpty(a.displayName) && !string.IsNullOrEmpty(b.displayName));
            Check($"[{id}] student-facing names never expose ids", !a.displayName.Contains("REF") && !b.displayName.Contains("REF"));
        }
        Check($"[independent] 8 distinct references for 4 items (got {all.Count})", all.Count == 8);
        Check("[independent] knife and bottle use different pairs",
            MeasurementTestUtil.Ref("EVD-014", 1) != MeasurementTestUtil.Ref("EVD-018", 1) && MeasurementTestUtil.Ref("EVD-014", 2) != MeasurementTestUtil.Ref("EVD-018", 2));

        // --- Wrong evidence's reference: refused, state unchanged ---
        var knife = m.GetRecord("MEAS-EVD-014");
        foreach (var other in new[] { MeasurementTestUtil.Ref("EVD-015", 1), MeasurementTestUtil.Ref("EVD-018", 1), MeasurementTestUtil.Ref("EVD-018", 2) })
        {
            var result = m.RecordMeasurement("MEAS-EVD-014", other, knife.requirement.expectedDistanceA);
            Check($"[wrong ref] knife measured from {other} -> {result}, nothing recorded",
                result == MeasurementRecordResult.WrongReference && knife.progress == MeasurementProgress.NotStarted && !knife.hasA && !knife.hasB);
        }

        // --- Partial: reference 1 only is incomplete ---
        Check("[partial] knife reference 1 recorded", MeasurementTestUtil.RecordExact("EVD-014", 1) == MeasurementRecordResult.Recorded);
        Check("[partial] reference 1 only -> Partial, not complete", knife.progress == MeasurementProgress.Partial && !knife.IsComplete);

        // --- Full: both own references complete it ---
        Check("[full] knife reference 2 completes it", MeasurementTestUtil.RecordExact("EVD-014", 2) == MeasurementRecordResult.Completed && knife.IsComplete);

        // --- Each other item completes only with its own pair ---
        ScenePhotographyTestUtil.CompleteRoute();
        esm.MarkFound("BODY-A", ToolType.None); esm.MarkPhotographedRaw("BODY-A", ToolType.Photographer);
        MeasurementTestUtil.PrepareBodyForMeasurement();
        esm.GetRecord("EVD-018").status = EvidenceStatus.PhotographedFinal; // Geometry fixture; photo-order gate is tested separately.
        foreach (var id in new[] { "BODY-A", "EVD-015", "EVD-018" })
        {
            var r = MeasurementTestUtil.RequirementFor(id);
            string foreign = MeasurementTestUtil.Ref(id == "EVD-018" ? "EVD-015" : "EVD-018", 1);
            var refused = m.RecordMeasurement(r.requirementId, foreign, r.expectedDistanceA);
            var first = MeasurementTestUtil.RecordExact(id, 1);
            var second = MeasurementTestUtil.RecordExact(id, 2);
            Check($"[{id}] another item's reference refused ({refused}); own pair: {first} then {second}",
                refused == MeasurementRecordResult.WrongReference && first == MeasurementRecordResult.Recorded && second == MeasurementRecordResult.Completed && m.IsMeasurementComplete(r.requirementId));
        }

        // --- Accuracy: 0.10 m tolerance against the item's own expected distances ---
        var bodyReq = MeasurementTestUtil.RequirementFor("BODY-A");
        Check($"[accuracy] tolerance still 0.10 m on every item", MeasurementTestUtil.RequirementFor("EVD-014").tolerance == 0.1f && bodyReq.tolerance == 0.1f
            && MeasurementTestUtil.RequirementFor("EVD-015").tolerance == 0.1f && MeasurementTestUtil.RequirementFor("EVD-018").tolerance == 0.1f);
        float exact = DistanceNow("EVD-014", 1);
        Check($"[accuracy] the live scene distance matches the baked expected value ({exact:0.000} vs {MeasurementTestUtil.RequirementFor("EVD-014").expectedDistanceA:0.000})",
            Mathf.Abs(exact - MeasurementTestUtil.RequirementFor("EVD-014").expectedDistanceA) < 0.002f);
        Check("[accuracy] 0.09 m off is within tolerance, 0.11 m is not (same rule as before)",
            Mathf.Abs(0.09f) <= bodyReq.tolerance && Mathf.Abs(0.11f) > bodyReq.tolerance);

        // --- Wall references: where the tape is hooked along the wall does not change the value ---
        var westWall = SceneReferencePoint.Find(MeasurementTestUtil.Ref("EVD-014", 1));
        MeasurementGeometry.TryGetEvidenceAnchor("EVD-014", out var knifePoint);
        Vector3 along = Vector3.Cross(Vector3.up, westWall.SurfaceNormal(true)) * 0.15f;
        Vector3 hookA = MeasurementTestUtil.HookPoint(westWall.referenceId);
        Check("[wall] two different hook spots along the zone both count as held at this reference",
            westWall.ContainsTip(hookA + along, 0.35f) && westWall.ContainsTip(hookA - along, 0.35f));
        float atItem = MeasurementGeometry.DistanceToReference(westWall, knifePoint, true);
        float shiftedAlongWall = MeasurementGeometry.DistanceToReference(westWall, knifePoint + along, true);
        Check($"[wall] perpendicular distance is the same anywhere along the wall ({atItem:0.000} vs {shiftedAlongWall:0.000})",
            Mathf.Abs(atItem - shiftedAlongWall) < 1e-5f);

        // --- Rectangular relationship (authoring validation) ---
        foreach (var id in Items)
        {
            var r = MeasurementTestUtil.RequirementFor(id);
            MeasurementGeometry.TryGetEvidenceAnchor(id, out var point);
            float angle = MeasurementGeometry.ReferenceAngle(point, SceneReferencePoint.Find(r.referenceAId), SceneReferencePoint.Find(r.referenceBId));
            Check($"[angle] {id} reference directions {angle:0.0}° - within 90° ± 15°, no warning",
                MeasurementGeometry.ReferenceAngleWarning(id, angle) == null);
        }
        Check("[angle] near 90° (88°) -> no warning", MeasurementGeometry.ReferenceAngleWarning("EVD-014", 88f) == null);
        string poor = MeasurementGeometry.ReferenceAngleWarning("EVD-014", 68f);
        Check($"[angle] poor angle (68°) -> warning '{poor}'", poor == "Measurement reference directions for EVD-014 are 68°. Review the reference placement.");

        // --- Late / disturbance rules unchanged ---
        Check("[late] route completed for lifecycle setup", ScenePhotographyTestUtil.CompleteRoute());
        foreach (var id in new[] { "EVD-014", "EVD-015" })
        {
            esm.MarkFound(id, ToolType.None);
        }
        yield return null;
        esm.MarkPhotographedRaw("EVD-014", ToolType.Photographer);
        esm.MarkChalkMarked("EVD-014", ToolType.ChalkMark);
        esm.MarkTented("EVD-014", ToolType.EvidenceMarker, 1);
        TraceMarkerTestUtil.MarkTraceIfRequired("EVD-014");
        esm.MarkPhotographedFinal("EVD-014", ToolType.Photographer);
        // Fixture for an already sampled surface; live sampling requires measurement and sketch first.
        esm.GetRecord("EVD-014").swabbingDone = true;
        Check("[late] knife blood sampling does NOT count as moving the knife", esm.GetRecord("EVD-014").swabbingDone && !m.IsDisturbed(MeasurementTestUtil.RequirementFor("EVD-014")));
        esm.MarkPhotographedRaw("EVD-015", ToolType.Photographer);
        esm.MarkChalkMarked("EVD-015", ToolType.ChalkMark); // outline first: chalk is required before its marker
        esm.MarkTented("EVD-015", ToolType.EvidenceMarker, 2);
        esm.MarkPhotographedFinal("EVD-015", ToolType.Photographer);
        // Fixture for an already sampled surface; live sampling requires measurement and sketch first.
        esm.GetRecord("EVD-015").swabbingDone = true;
        Check("[late] spatter sampling DOES count as disturbance", m.IsDisturbed(MeasurementTestUtil.RequirementFor("EVD-015")));

        SessionLogger.Instance?.Flush();
        using var logStream = new System.IO.FileStream(SessionLogger.Instance.ActiveLogFilePath,System.IO.FileMode.Open,System.IO.FileAccess.Read,System.IO.FileShare.ReadWrite);
        using var logReader = new System.IO.StreamReader(logStream);
        string rawLog = logReader.ReadToEnd();
        int taggedMeasurements = 0, takenMeasurements = 0;
        foreach(string line in rawLog.Split('\n'))
        {
            if(!line.Contains("\"eventType\":\"MeasurementTaken\""))continue;
            takenMeasurements++;
            var logged = JsonUtility.FromJson<UploadSessionEventDto>(line);
            if(logged.payload.Exists(p=>p.key=="layoutId"&&p.value==m.MeasurementLayoutId))taggedMeasurements++;
        }
        Check("[wire] actual measurement events retain the registered new-house geometry profile",
            m.MeasurementLayoutId=="new-house-v3"&&takenMeasurements>0&&taggedMeasurements==takenMeasurements);
        report.AppendLine(failures.Count == 0 ? "=== PASS, 0 failures ===" : $"=== FAIL: {failures.Count} failure(s) ===");
        Report = report.ToString();
        IsDone = true;
    }

    private static float DistanceNow(string evidenceId, int slot)
    {
        var r = MeasurementTestUtil.RequirementFor(evidenceId);
        MeasurementStateManager.Instance.TryGetSceneDistance(r.requirementId, MeasurementTestUtil.Ref(evidenceId, slot), out var d);
        return d;
    }
}
