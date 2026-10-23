// Assets/_Project/Editor/Measurement/MeasurementRequirementSetEditor.cs
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Authoring helper for MeasurementRequirementSet: bakes expectedDistanceA/B from the
/// currently open scene - each item's measurement point to EACH OF ITS OWN two references
/// (to the anchor point, or perpendicular to a wall/counter surface), by the requirement's
/// floor-plan/3D method, via the same MeasurementGeometry the runtime uses - and checks
/// that the two reference directions are close to a right angle (rectangular
/// coordinates). The angle check only warns; it never blocks anything at runtime. Only
/// runs when a button is pressed - never per frame, never at runtime, never automatically.
/// </summary>
[CustomEditor(typeof(MeasurementRequirementSet))]
public class MeasurementRequirementSetEditor : Editor
{
    /// <summary>Allowed deviation from 90° between an item's two reference directions before a warning.</summary>
    public static float AngleTolerance = MeasurementGeometry.DefaultReferenceAngleTolerance;

    private string lastReport;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            if (GUILayout.Button("Bake expected distances from open scene"))
            {
                lastReport = Bake((MeasurementRequirementSet)target, write: true);
            }
            if (GUILayout.Button("Check geometry only (no changes)"))
            {
                lastReport = Bake((MeasurementRequirementSet)target, write: false);
            }
        }

        if (!string.IsNullOrEmpty(lastReport))
        {
            EditorGUILayout.HelpBox(lastReport, MessageType.Info);
        }
    }

    /// <summary>Bakes (or just checks) every requirement against the open scene. Returns a human-readable report. Public static for scripted use.</summary>
    public static string Bake(MeasurementRequirementSet set, bool write)
    {
        var report = new StringBuilder();
        if (write)
        {
            Undo.RecordObject(set, "Bake measurement distances");
        }

        foreach (var r in set.requirements)
        {
            if (r == null)
            {
                continue;
            }

            var a = SceneReferencePoint.Find(r.referenceAId);
            var b = SceneReferencePoint.Find(r.referenceBId);
            if (a == null || b == null || !MeasurementGeometry.TryGetEvidenceAnchor(r.targetEvidenceId, out var target))
            {
                report.AppendLine($"{r.requirementId}: SKIPPED - missing {(a == null ? r.referenceAId + " " : "")}{(b == null ? r.referenceBId + " " : "")}{r.targetEvidenceId} in the open scene.");
                continue;
            }

            float da = MeasurementGeometry.DistanceToReference(a, target, r.measureOnFloorPlan);
            float db = MeasurementGeometry.DistanceToReference(b, target, r.measureOnFloorPlan);
            float angle = MeasurementGeometry.ReferenceAngle(target, a, b);
            if (write)
            {
                r.expectedDistanceA = Mathf.Round(da * 1000f) / 1000f;
                r.expectedDistanceB = Mathf.Round(db * 1000f) / 1000f;
            }

            string angleWarning = MeasurementGeometry.ReferenceAngleWarning(r.targetEvidenceId, angle, AngleTolerance);
            if (angleWarning != null)
            {
                Debug.LogWarning("[Measurement] " + angleWarning, set);
            }
            report.AppendLine($"{r.requirementId}: {a.displayName}={da:0.000} m  {b.displayName}={db:0.000} m  angle={angle:0}°"
                + (angleWarning != null ? "  WARNING: " + angleWarning : ""));
        }

        if (write)
        {
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssetIfDirty(set);
        }
        return report.ToString().TrimEnd();
    }
}
