// Assets/_Project/Scripts/Testing/CabinetReferenceTest.cs
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// The knife's and blood pattern's unreachable "North Wall" references (behind the
/// built-in north cabinet run) were replaced by the front edge of that cabinet run.
/// Checks the new references, that the old wall references no longer exist or select,
/// that each cabinet reference serves only its own item, the reachable zone placement,
/// and the 90-degree pair. Synchronous; run in a fresh Play session.
/// </summary>
public class CabinetReferenceTest : MonoBehaviour
{
    private readonly List<string> failures = new();
    private readonly StringBuilder report = new();

    private void Check(string label, bool ok)
    {
        report.AppendLine((ok ? "  PASS: " : "  FAIL: ") + label);
        if (!ok) failures.Add(label);
    }

    public string RunAllTests()
    {
        report.AppendLine("=== CabinetReferenceTest ===");
        var m = MeasurementStateManager.Instance;
        if (m == null) return "FAIL: MeasurementStateManager missing.";

        var knife = MeasurementTestUtil.RequirementFor("EVD-014");
        var spatter = MeasurementTestUtil.RequirementFor("EVD-015");
        var knifeCab = SceneReferencePoint.Find("REF-KNIFE-CAB");
        var spatterCab = SceneReferencePoint.Find("REF-SPATTER-CAB");

        // --- New references ---
        Check("[config] knife = East Kitchen Wall + Rear Kitchen Wall", knife.referenceAId == "REF-KNIFE-1" && knife.referenceBId == "REF-KNIFE-CAB");
        Check("[config] spatter = Foot-side Kitchen Wall + West Kitchen Wall", spatter.referenceAId == "REF-SPATTER-CAB" && spatter.referenceBId == "REF-SPATTER-2");
        Check("[config] both cabinet references resolve as planes with zones",
            knifeCab != null && spatterCab != null && knifeCab.shape == SceneReferencePoint.ReferenceShape.Plane
            && spatterCab.shape == SceneReferencePoint.ReferenceShape.Plane && knifeCab.Zone != null && spatterCab.Zone != null);
        Check($"[label] student-facing wall names", knifeCab.displayName == "Rear Kitchen Wall" && spatterCab.displayName == "Foot-side Kitchen Wall");
        Check("[config] tolerance unchanged (0.10 m)", Mathf.Approximately(knife.tolerance, 0.1f) && Mathf.Approximately(spatter.tolerance, 0.1f));

        // --- Old hidden-wall references are gone ---
        Check("[old] REF-KNIFE-2 / REF-SPATTER-1 no longer exist in the scene", SceneReferencePoint.Find("REF-KNIFE-2") == null && SceneReferencePoint.Find("REF-SPATTER-1") == null);
        var oldKnife = m.RecordMeasurement(knife.requirementId, "REF-KNIFE-2", 1.663f);
        var oldSpatter = m.RecordMeasurement(spatter.requirementId, "REF-SPATTER-1", 1.966f);
        Check($"[old] recording against the old wall ids is refused ({oldKnife}, {oldSpatter}) and records nothing",
            oldKnife != MeasurementRecordResult.Recorded && oldSpatter != MeasurementRecordResult.Recorded
            && !m.GetRecord(knife.requirementId).hasB && !m.GetRecord(spatter.requirementId).hasA);
        // Where the old wall zones were (above the counter, against the wall): no reference selects there now.
        foreach (var oldSpot in new[] { new Vector3(-4.109f, 1.4f, 14.73f), new Vector3(-3.162f, 1.4f, 14.73f), new Vector3(-4.109f, 1.1f, 14.95f) })
        {
            bool any = false;
            foreach (var r in SceneReferencePoint.All) any |= r.ContainsTip(oldSpot, 0.35f);
            Check($"[old] tape held at the old wall spot {oldSpot} selects no reference", !any);
        }

        // --- Reachable zones on the cabinet front ---
        foreach (var rp in new[] { knifeCab, spatterCab })
        {
            var box = rp.Zone.Box;
            Vector3 c = box.transform.TransformPoint(box.center);
            Vector3 size = Vector3.Scale(box.size, box.transform.lossyScale);
            Check($"[zone] {rp.referenceId}: enlarged receiver projects into room at reachable height (c={c:F2}, size={size:F2})",
                Vector3.Dot(c-rp.AnchorPosition,rp.SurfaceNormal(false)) > .15f && c.y > 1.5f && c.y < 2.3f
                && size.x >= .79f && size.y >= .89f && size.z >= .39f);
            Check($"[zone] {rp.referenceId}: the hook point selects it", rp.ContainsTip(MeasurementTestUtil.HookPoint(rp.referenceId), 0.35f));
        }
        Check("[zone] the two cabinet zones do not overlap",
            !knifeCab.ContainsTip(MeasurementTestUtil.HookPoint("REF-SPATTER-CAB"), 0.35f) && !spatterCab.ContainsTip(MeasurementTestUtil.HookPoint("REF-KNIFE-CAB"), 0.35f));

        foreach (var requirement in m.RequirementSet.requirements)
        {
            MeasurementGeometry.TryGetEvidenceAnchor(requirement.targetEvidenceId,out var evidence);
            foreach (var id in new[]{requirement.referenceAId,requirement.referenceBId})
            {
                var reference = SceneReferencePoint.Find(id);
                var delta = evidence-reference.AnchorPosition; delta.y = 0;
                var normal = reference.SurfaceNormal(true);
                Check("[alignment] hook directly faces its evidence: " + id,
                    (delta-normal*Vector3.Dot(delta,normal)).magnitude < .002f);
                var box = reference.Zone.Box;
                var edge = box.transform.TransformPoint(box.center + new Vector3(.35f,.4f,.15f));
                Check("[reach] enlarged off-center contact is accepted: " + id, reference.ContainsTip(edge,.35f));
            }
        }
        Check("[wall] body and blood use the wall near the feet, opposite rear cabinets",
            SceneReferencePoint.Find("REF-BODYA-2").SurfaceNormal(true).z > .99f
            && spatterCab.SurfaceNormal(true).z > .99f
            && SceneReferencePoint.Find("REF-BODYA-2").AnchorPosition.z < 11.7f && spatterCab.AnchorPosition.z < 11.7f);

        // --- Value independent of where on the zone the tape is held ---
        MeasurementGeometry.TryGetEvidenceAnchor("EVD-014", out var knifePoint);
        float d = MeasurementGeometry.DistanceToReference(knifeCab, knifePoint, true);
        Check($"[value] knife -> cabinet edge {d:0.000} m equals the baked expected {knife.expectedDistanceB:0.000} m", Mathf.Abs(d - knife.expectedDistanceB) < 0.002f);
        MeasurementGeometry.TryGetEvidenceAnchor("EVD-015", out var spatterPoint);
        float ds = MeasurementGeometry.DistanceToReference(spatterCab, spatterPoint, true);
        Check($"[value] spatter -> cabinet edge {ds:0.000} m equals the baked expected {spatter.expectedDistanceA:0.000} m", Mathf.Abs(ds - spatter.expectedDistanceA) < 0.002f);

        // --- Angle validation (global tolerance untouched) ---
        float ka = MeasurementGeometry.ReferenceAngle(knifePoint, SceneReferencePoint.Find(knife.referenceAId), knifeCab);
        float sa = MeasurementGeometry.ReferenceAngle(spatterPoint, spatterCab, SceneReferencePoint.Find(spatter.referenceBId));
        Check($"[angle] knife pair {ka:0.0}°, spatter pair {sa:0.0}° - no warning", MeasurementGeometry.ReferenceAngleWarning("EVD-014", ka) == null && MeasurementGeometry.ReferenceAngleWarning("EVD-015", sa) == null);
        Check("[angle] global tolerance still 15°", Mathf.Approximately(MeasurementGeometry.DefaultReferenceAngleTolerance, 15f));

        // --- Evidence-specific, two references still required ---
        Check("[specific] spatter cannot use the knife's cabinet reference", m.RecordMeasurement(spatter.requirementId, "REF-KNIFE-CAB", 1.382f) == MeasurementRecordResult.WrongReference);
        Check("[specific] knife cannot use the spatter's cabinet reference", m.RecordMeasurement(knife.requirementId, "REF-SPATTER-CAB", 1.079f) == MeasurementRecordResult.WrongReference);
        Check("[partial] cabinet edge alone -> Partial, not complete",
            MeasurementTestUtil.RecordExact("EVD-014", 2) == MeasurementRecordResult.Recorded && m.GetRecord(knife.requirementId).progress == MeasurementProgress.Partial);
        Check("[full] second reference completes the knife", MeasurementTestUtil.RecordExact("EVD-014", 1) == MeasurementRecordResult.Completed);
        Check("[full] spatter completes with cabinet edge + cabinet end",
            MeasurementTestUtil.RecordExact("EVD-015", 1) == MeasurementRecordResult.Recorded && MeasurementTestUtil.RecordExact("EVD-015", 2) == MeasurementRecordResult.Completed);
        var assessment = m.BuildAssessment();
        Check("[assessment] client assessment uses the new pairs", assessment != null);

        report.AppendLine(failures.Count == 0 ? "=== PASS, 0 failures ===" : $"=== FAIL: {failures.Count} failure(s) ===");
        return report.ToString();
    }
}
