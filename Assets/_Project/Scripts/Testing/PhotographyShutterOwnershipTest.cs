using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

/// <summary>Regression for one shutter advancing both scene and evidence photos.</summary>
public sealed class PhotographyShutterOwnershipTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }
    private readonly StringBuilder report = new("=== PhotographyShutterOwnershipTest ===\n");
    private int failures;
    private void Check(string label, bool ok)
    {
        report.AppendLine((ok ? "PASS: " : "FAIL: ") + label);
        if (!ok) failures++;
    }

    public void RunAllTests()
    {
        AutomatedTestSandbox.Activate(nameof(PhotographyShutterOwnershipTest));
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        yield return null;
        var photos = ScenePhotographyManager.Instance;
        var tool = FindFirstObjectByType<PhotographTool>(FindObjectsInactive.Include);
        var head = Camera.main.transform;
        var state = EvidenceStateManager.Instance;
        var prop = EvidenceProp.FindById("EVD-018");
        var record = state.GetRecord("EVD-018");
        if (SceneTurnover.IsPending) SceneTurnover.Controller.CompleteWithoutDialogue();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(ScenePhotographyManager).GetMethod("ActivateFirstPoint", flags).Invoke(photos, null);
        var target = typeof(PhotographTool).GetMethod("SetAimTarget", flags);
        var shutter = typeof(PhotographTool).GetMethod("TakePhoto", flags);
        state.MarkFound("EVD-018", ToolType.None);
        // Supply a valid in-frame target independently of house visibility: this
        // regression isolates shutter routing, not the existing occlusion algorithm.
        target.Invoke(tool, new object[] { prop });
        Check("evidence is a capture candidate before the route shot", tool.CanCapture && tool.IsConfirmedForCapture);
        int captures = 0;
        bool evidenceCaptured = false;
        tool.OnPhotoCaptured += () => captures++;
        tool.OnShutterReleased += (_, captured) => evidenceCaptured = captured;

        var first = photos.CurrentPoint;
        head.position = first.transform.position + Vector3.up * 1.6f;
        head.rotation = Quaternion.LookRotation(head.position - first.cameraTarget.position);
        int wrong = photos.WrongAngleAttempts;
        shutter.Invoke(tool, null);
        Check("wrong-angle route retry does not photograph evidence", photos.WrongAngleAttempts == wrong + 1
            && record.status == EvidenceStatus.Found && captures == 0 && !evidenceCaptured && tool.LastProcedureWarning == null);

        var last = photos.Points.Last();
        head.SetPositionAndRotation(last.transform.position + Vector3.up * 1.6f,
            Quaternion.LookRotation(last.cameraTarget.position - (last.transform.position + Vector3.up * 1.6f)));
        int outOfSequence = photos.OutOfSequenceAttempts;
        shutter.Invoke(tool, null);
        Check("future-station attempt does not photograph evidence", photos.OutOfSequenceAttempts == outOfSequence + 1
            && record.status == EvidenceStatus.Found && captures == 0 && !evidenceCaptured);

        foreach (var point in photos.Points)
        {
            head.position = point.transform.position + Vector3.up * 1.6f;
            head.rotation = Quaternion.LookRotation(point.cameraTarget.position - head.position);
            target.Invoke(tool, new object[] { prop });
            shutter.Invoke(tool, null);
            Check("route shutter completes only " + point.photoPointId, point.IsCompleted
                && record.status == EvidenceStatus.Found && captures == 0 && !evidenceCaptured && tool.LastProcedureWarning == null);
        }
        Check("last route shutter completes photography without leaking into evidence", photos.IsScenePhotographyComplete
            && record.status == EvidenceStatus.Found && !photos.OwnsShutter(head.position));
        Check("Only successful route photographs enter the labelled album",PhotoAlbumManager.Instance.Records.Count==photos.Points.Count
            &&PhotoAlbumManager.Instance.Records.All(r=>r.Kind==PhotoCaptureKind.Scene&&!string.IsNullOrEmpty(r.SubjectId)));
        target.Invoke(tool, new object[] { prop });
        shutter.Invoke(tool, null);
        Check("a separate shutter after the route takes the initial evidence photo", record.status == EvidenceStatus.PhotographedRaw
            && captures == 1 && evidenceCaptured && tool.LastProcedureWarning == null);

        // Isolate final-photo routing with a ready-for-final fixture at a station.
        // Reopen that point only for the ownership check; no gameplay reset is added.
        last.ResetState();
        typeof(ScenePhotographyManager).GetField("<IsScenePhotographyComplete>k__BackingField", flags).SetValue(photos, false);
        record.status = EvidenceStatus.Marked;
        target.Invoke(tool, new object[] { prop });
        shutter.Invoke(tool, null);
        Check("route ownership also suppresses final evidence photography", record.status == EvidenceStatus.Marked
            && captures == 1 && !evidenceCaptured);
        typeof(ScenePhotographyManager).GetField("<IsScenePhotographyComplete>k__BackingField", flags).SetValue(photos, true);
        PhotoScaleTestUtil.PrepareForFinalPhoto(tool, prop.evidenceId);
        target.Invoke(tool, new object[] { prop });
        shutter.Invoke(tool, null);
        Check("a separate evidence shutter still takes the final photo", record.status == EvidenceStatus.PhotographedFinal
            && captures == 2 && evidenceCaptured);
        report.AppendLine(failures == 0 ? "=== PASS, 0 failures ===" : "=== FAIL: " + failures + " failure(s) ===");
        Report = report.ToString();
        IsDone = true;
    }
}
