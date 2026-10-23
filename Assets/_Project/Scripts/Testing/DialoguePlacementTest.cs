using System.Collections;
using System.Reflection;
using System.Text;
using UnityEngine;

/// <summary>Real presenters in an isolated standing-eye fixture, without gameplay input producers.</summary>
public class DialoguePlacementTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }
    private readonly StringBuilder report = new();
    private int failures;
    public void RunAllTests() => StartCoroutine(Run());
    private void Check(string text, bool ok) { report.AppendLine($"  {(ok ? "PASS" : "FAIL")}: {text}"); if (!ok) failures++; }
    private IEnumerator Run()
    {
        yield return null;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var pre = FindAnyObjectByType<PreSessionFlowController>();
        if (pre != null)
        {
            pre.enabled = false;
            var token = typeof(PreSessionFlowController).GetField("uiGateToken", flags)?.GetValue(pre);
            if (token != null) PlayerUIGate.Exit(token);
        }
        FindAnyObjectByType<StudentIdentificationPanel>(FindObjectsInactive.Include)?.Hide();
        foreach (var source in new MonoBehaviour[] { ProceduralTrainingGuidance.Instance, NarrativeEventManager.Instance,
            ReasoningCheckpointManager.Instance, HypothesisCheckpointManager.Instance, ArrivalPPEStation.Instance,
            SamplePreparationStation.Instance, FingerprintComparisonStation.Instance,
            FindAnyObjectByType<ScenePhotographyManager>(), FindAnyObjectByType<HypothesisCheckpointUI>() })
            if (source != null) { source.StopAllCoroutines(); source.CancelInvoke(); source.enabled = false; }
        var original = Camera.main;
        if (original != null) original.tag = "Untagged";
        var eye = new GameObject("Dialogue geometry standing eye", typeof(Camera));
        var camera = eye.GetComponent<Camera>(); camera.tag = "MainCamera";
        eye.transform.position = new Vector3(1000, 1000, 1000);
        var upper = STCSNotificationUI.Instance;
        var lower = NotificationManager.Instance.UI;
        var upperFollow = upper.GetComponent<HudFollowCamera>();
        var lowerFollow = lower.GetComponent<HudFollowCamera>();
        foreach (var follow in new[] { upperFollow, lowerFollow })
        {
            typeof(HudFollowCamera).GetField("targetCamera", flags).SetValue(follow, camera);
            follow.SmoothTime = 0; follow.RotationSmoothing = 100;
        }
        upper.HideImmediately(); lower.ClearTooltip(); lower.HidePrompt();
        Check("Geometry fixture has no modal owner", !PlayerUIGate.IsBlocked);
        string longText = "Compare the ridge endings and splits in the recovered fingerprint with the reference. "
            + "Rotate or zoom the prints if needed. You can keep trying while you learn the comparison process.\n"
            + "Real case findings require evaluation and verification by a trained fingerprint examiner.";
        GameObject wall = null;
        for (int i = 0; i < 3; i++)
        {
            if (i == 2)
            {
                wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.transform.position = eye.transform.position + new Vector3(0, 0, .65f);
                wall.transform.localScale = new Vector3(.4f, 3, .08f); Physics.SyncTransforms();
            }
            upper.HideImmediately(); upper.ShowLine(i == 1 ? "Ready." : longText, null, "Team", "layout-geometry");
            lower.ShowTooltip(i == 1 ? "Sample secured." : "Return to the outside supply table and replace your gloves before collecting another sample.", 60);
            yield return new WaitForSecondsRealtime(.5f);
            string context = i == 0 ? "Long text" : i == 1 ? "Short replacements" : "Long text facing a wall";
            bool visible = !upperFollow.IsLayoutSuppressed && !lowerFollow.IsLayoutSuppressed && !lower.IsPlacementDeferred;
            Check(context + ": both panels have a visible slot", visible);
            var upperRect = (RectTransform)upper.transform; var lowerRect = (RectTransform)lower.transform;
            Vector3 slot = Quaternion.Inverse(upper.transform.rotation) * (lower.transform.position - upper.transform.position);
            float bottom = upperRect.rect.yMin * upper.transform.lossyScale.y;
            float top = slot.y + lowerRect.rect.yMax * lower.transform.lossyScale.y;
            Check(context + ": gap stays at least 5 cm", visible && top <= bottom - .049f);
            Check(context + ": upper clears geometry", upperFollow.PanelFits(camera.transform.position, upper.transform.position, upper.transform.rotation));
            Check(context + ": lower clears geometry", lowerFollow.PanelFits(camera.transform.position, lower.transform.position, lower.transform.rotation));
        }
        upper.HideImmediately(); lower.ClearTooltip(); lower.HidePrompt();
        Destroy(wall); Destroy(eye); if (original != null) original.tag = "MainCamera";
        report.AppendLine(failures == 0 ? "=== PASS ===" : $"=== FAIL: {failures} ===");
        Report = report.ToString(); IsDone = true;
    }
}
