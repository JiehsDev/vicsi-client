using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

/// <summary>Standing-circle visibility follows the existing capture zone, not photo completion.</summary>
public sealed class ScenePhotoZoneVisibilityTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }
    private readonly StringBuilder report = new("=== ScenePhotoZoneVisibilityTest ===\n");
    private int failures;
    private void Check(string label, bool ok)
    {
        report.AppendLine((ok ? "PASS: " : "FAIL: ") + label);
        if (!ok) failures++;
    }
    public void RunAllTests() => StartCoroutine(Run());
    private IEnumerator Run()
    {
        AutomatedTestSandbox.Activate(nameof(ScenePhotoZoneVisibilityTest));
        yield return null;
        if (ReasoningCheckpointManager.Instance != null) ReasoningCheckpointManager.Instance.enabled = false;
        STCSNotificationUI.Instance?.HideImmediately();
        var photos = ScenePhotographyManager.Instance;
        var tool = FindFirstObjectByType<PhotographTool>(FindObjectsInactive.Include);
        var original = Camera.main;
        string tag = original.tag;
        original.tag = "Untagged";
        var fixture = new GameObject("Standing circle visibility test camera", typeof(Camera));
        var camera = fixture.GetComponent<Camera>();
        camera.tag = "MainCamera";
        var rig = FindFirstObjectByType<OVRCameraRig>();
        bool rigEnabled = rig != null && rig.enabled;
        if (rig != null) rig.enabled = false;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var shutter = typeof(ScenePhotographyManager).GetMethod("HandleShutter", flags);
        try
        {
            typeof(ScenePhotographyManager).GetMethod("ActivateFirstPoint", flags).Invoke(photos, null);
            Check("All seven authored photography points are present", photos.Points.Count == 7);
            foreach (var point in photos.Points)
            {
                string id = point.photoPointId;
                Check(id + ": visual is a separate child, so hiding it cannot stop tracking",
                    point.zoneVisual != null && point.zoneVisual != point.gameObject && point.zoneVisual.transform.IsChildOf(point.transform));
                camera.transform.position = point.transform.position + Vector3.up * 1.6f + Vector3.right * (point.standingRadius + .2f);
                yield return null; yield return null;
                Check(id + ": outside shows only the active circle", photos.CurrentPoint == point && point.IsZoneVisible
                    && photos.Points.Where(p => p != point).All(p => !p.IsZoneVisible));
                camera.transform.position = point.transform.position + Vector3.up * 1.6f;
                camera.transform.rotation = Quaternion.LookRotation(camera.transform.position - point.cameraTarget.position);
                yield return null; yield return null;
                Check(id + ": entering hides the circle without advancing", !point.IsZoneVisible && !point.IsCompleted && photos.CurrentPoint == point);
                int wrong = photos.WrongAngleAttempts;
                shutter.Invoke(photos, new object[] { tool, false });
                Check(id + ": wrong-angle shutter stays hidden and unfinished", photos.WrongAngleAttempts == wrong + 1 && !point.IsCompleted && !point.IsZoneVisible);
                camera.transform.position += Vector3.right * (point.standingRadius + .2f);
                yield return null; yield return null;
                Check(id + ": exiting after unsuccessful photo restores the circle", point.IsZoneVisible && !point.IsCompleted && photos.CurrentPoint == point);
                camera.transform.position = point.transform.position + Vector3.up * (point.maxHeadHeight + .1f);
                yield return null; yield return null;
                Check(id + ": another floor does not count as standing inside", point.IsZoneVisible && !point.ContainsPlayer(camera.transform.position));
                camera.transform.position = point.transform.position + Vector3.up * 1.6f;
                camera.transform.rotation = Quaternion.LookRotation(point.cameraTarget.position - camera.transform.position);
                yield return null; yield return null;
                var guide = photos.GetComponent<ScenePhotographyGuide>();
                guide.Refresh();
                Check(id + ": reentry hides circle while framing guidance remains available", !point.IsZoneVisible && guide.IsVisible && guide.IsFramingTarget);
                shutter.Invoke(photos, new object[] { tool, false });
                Check(id + ": successful photo still completes the original route point", point.IsCompleted && !point.IsZoneVisible);
                camera.transform.position += Vector3.right * (point.standingRadius + .2f);
                yield return null; yield return null;
                Check(id + ": completed circle stays hidden after leaving", !point.IsZoneVisible && photos.Points.Where(p => p.IsCompleted).All(p => !p.IsZoneVisible));
            }
            Check("Route completes with all circles hidden", photos.IsScenePhotographyComplete && photos.Points.All(p => p.IsCompleted && !p.IsZoneVisible));
        }
        finally
        {
            original.tag = tag;
            if (rig != null) rig.enabled = rigEnabled;
            Destroy(fixture);
        }
        report.AppendLine(failures == 0 ? "=== PASS, 0 failures ===" : "=== FAIL: " + failures + " failure(s) ===");
        Report = report.ToString(); IsDone = true;
    }
}
