// Assets/_Project/Scripts/Testing/EvidenceProcedurePanelTest.cs
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Play-mode checks for the Evidence / Procedure panel prototype (EvidenceProcedureUI):
/// placement, evidence list and selection (through the real ray pointer), hidden-evidence
/// safety, one Current step, the four evidence-specific checklists, measurement rows,
/// the single next action, modal locks, close, scroll and reset. Drives the real
/// lifecycle APIs the tools call. Must run in a fresh play session.
///
/// Optional: set captureDirectory to write review screenshots (PNG) of each stage.
/// </summary>
public class EvidenceProcedurePanelTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }

    [Tooltip("When set, review screenshots are written here.")]
    public string captureDirectory;

    private readonly List<string> failures = new();
    private readonly StringBuilder report = new();
    private EvidenceProcedureUI ui;

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

    private static string Name(string id) => EvidenceStateManager.Instance.GetDisplayName(id);

    private IEnumerator Run()
    {
        AutomatedTestSandbox.Activate(nameof(EvidenceProcedurePanelTest));
        // This suite tests the reading panel; narrative/checkpoint presentation has
        // dedicated suites and must not open a second modal while fixtures operate tools.
        if(ReasoningCheckpointManager.Instance!=null)ReasoningCheckpointManager.Instance.enabled=false;
        if(HypothesisCheckpointManager.Instance!=null)HypothesisCheckpointManager.Instance.enabled=false;
        if(ProceduralTrainingGuidance.Instance!=null)ProceduralTrainingGuidance.Instance.enabled=false;
        if(NarrativeEventManager.Instance!=null){NarrativeEventManager.Instance.enabled=false;NarrativeEventManager.Instance.StopAllCoroutines();}
        STCSNotificationUI.Instance?.HideImmediately();NotificationManager.Instance?.UI.ClearTooltip();
        report.AppendLine("=== EvidenceProcedurePanelTest ===");
        var esm = EvidenceStateManager.Instance;
        ui = EvidenceProcedureUI.Instance;
        if (esm == null || ui == null || MeasurementStateManager.Instance == null || Camera.main == null)
        {
            Report = "FAIL: required managers missing.";
            IsDone = true;
            yield break;
        }
        string body = "BODY-A", knife = "EVD-014", spatter = "EVD-015", bottle = "EVD-018";

        // --- Empty: nothing photographed yet ---
        ui.ResetSession();
        ui.Show();
        yield return null;
        // Opening acknowledges the action with a short scale pulse. Measure the
        // settled physical size, rather than an intermediate animation frame.
        float placementWait = 0f;
        while (Mathf.Abs(ui.PanelTransform.lossyScale.x - 0.001f) >= 1e-6f && placementWait < 1f)
        {
            placementWait += Time.unscaledDeltaTime;
            yield return null;
        }
        Check("[empty] nothing photographed: list empty, no procedure, one onboarding instruction",
            ui.ListedEvidenceIds.Count == 0 && ui.CurrentSteps.Count == 0 && ui.NextActionText == "Photograph an item of evidence to begin its record.");

        // --- Placement: in front, comfortable distance, below eye level, facing the player, then still ---
        var cam = Camera.main.transform;
        var panel = ui.PanelTransform;
        Vector3 toPanel = panel.position - cam.position;
        float horizontal = new Vector2(toPanel.x, toPanel.z).magnitude;
        float drop = cam.position.y - panel.position.y;
        float facing = Vector3.Dot(panel.forward, toPanel.normalized);
        Check($"[placement] {horizontal:0.00} m ahead (1.2-1.5), {drop:0.00} m below eye level, facing the eyes (dot {facing:0.000}), size {ui.PanelSizeMetres.x:0.00} x {ui.PanelSizeMetres.y:0.00} m",
            horizontal >= 1.2f && horizontal <= 1.5f && drop > 0.05f && drop < 0.3f && facing > 0.99f);
        Check("[placement] shared wall avoidance preserves world anchoring", panel.GetComponent<HudFollowCamera>() != null && panel.GetComponent<HudFollowCamera>().AnchoredPlacement);
        Check($"[placement] unparented, exact scale (parent {(panel.parent == null ? "none" : panel.parent.name)}, scale {panel.lossyScale.x:0.0000})",
            panel.parent == null && Mathf.Abs(panel.lossyScale.x - 0.001f) < 1e-6f);
        Vector3 placed = panel.position;
        Quaternion placedRot = panel.rotation;
        var camPos = cam.position;
        var camRot = cam.rotation;
        cam.position += cam.right * 0.4f;
        cam.rotation *= Quaternion.Euler(0f, 35f, 0f);
        yield return null;
        yield return null;
        Check("[placement] stays where it was placed while the head moves/turns",
            (panel.position - placed).sqrMagnitude < 1e-8f && Quaternion.Angle(panel.rotation, placedRot) < 0.01f);
        cam.position = camPos;
        cam.rotation = camRot;
        ReportAngles();
        ui.Hide();

        // --- Discovery: found by proximity is NOT enough to be listed ---
        foreach (var id in new[] { body, knife, spatter, bottle })
        {
            esm.MarkFound(id, ToolType.None);
        }
        yield return DismissCheckpoints();
        esm.MarkPhotographedRaw(knife, ToolType.Photographer);
        yield return DismissCheckpoints();
        ui.Show();
        yield return null;
        Check($"[hidden] only the photographed item is listed ({string.Join(",", ui.ListedEvidenceIds)})",
            ui.ListedEvidenceIds.Count == 1 && ui.ListedEvidenceIds[0] == knife && ui.FocusedEvidenceId == knife);
        Check("[hidden] no evidence distance rows while no other item is discovered", ui.EvidenceDistanceRows.Count == 0);
        Check("[hidden] no undiscovered item's name anywhere on the panel", !PanelMentions(body, spatter, bottle));
        Check("[hidden] internal ids never shown", !PanelContains("EVD-") && !PanelContains("BODY-A"));
        Check($"[current] exactly one highlighted step ({CountHighlighted()})", CountHighlighted() == 1 && CountCurrent() == 1);
        yield return Capture("6_hidden_evidence");
        ui.Hide();

        Check("[scene] overall scene photography completed", ScenePhotographyTestUtil.CompleteRoute());
        yield return null;

        // --- BODY-A during chalk ---
        esm.MarkPhotographedRaw(body, ToolType.Photographer);
        yield return DismissCheckpoints();
        ui.Show();
        yield return null;
        Check($"[select] ray click selects {Name(body)}", ClickTarget("evidence:" + body) == "evidence:" + body && ui.FocusedEvidenceId == body);
        yield return null;
        Check("[select] exactly one row shows the selected state", SelectedRowCount() == 1 && SelectedRowIs(body));
        Check($"[BODY-A] chalk is the one Current step; NEXT: '{ui.NextActionText}'",
            CurrentStepId() == "chalk" && CountHighlighted() == 1 && ui.NextActionText == "Chalk the outline of the evidence's position.");
        CheckLabels(body, "Scene Photography,Initial Photograph,Chalk Position,Body Marker A,Final Photograph,Measurement,"
            + EvidenceDisplay.Text("Record BODY-A Findings") + ",Prepare Body for Removal");
        yield return Capture("1_body_chalk");

        // --- Knife during measurement ---
        esm.MarkChalkMarked(knife, ToolType.ChalkMark);
        esm.MarkTented(knife, ToolType.EvidenceMarker, 1);
        esm.MarkTraceTented(knife, ToolType.EvidenceMarker, 4);
        esm.MarkPhotographedRaw(spatter, ToolType.Photographer);
        yield return DismissCheckpoints();
        MeasurementTestUtil.RecordExact(knife, 1);
        // Body progress follows the existing UI focus policy; finish this prerequisite
        // before selecting the knife whose rows the following assertions inspect.
        MeasurementTestUtil.PrepareBodyForMeasurement();
        yield return null;
        Check($"[select] ray click selects {Name(knife)}", ClickTarget("evidence:" + knife) == "evidence:" + knife && ui.FocusedEvidenceId == knife && SelectedRowIs(knife) && SelectedRowCount() == 1);
        Check($"[measurement] Current = measurement, sub-detail {string.Join(" ; ", ui.CurrentStepDetailRows)}",
            CurrentStepId() == "measurement" && ui.CurrentStepDetailRows.Count == 2
            && ui.CurrentStepDetailRows[0] == "Fixed references|1 / 2" && ui.CurrentStepDetailRows[1] == "Evidence distances|0 / 2");
        MeasurementTestUtil.RecordExact(knife, 2);
        MeasurementTestUtil.RecordPairExact(knife, body);
        yield return null;
        ui.Rebuild();
        Check($"[measurement] counts advance: {string.Join(" ; ", ui.CurrentStepDetailRows)}",
            ui.CurrentStepDetailRows.Count == 2 && ui.CurrentStepDetailRows[0] == "Fixed references|2 / 2" && ui.CurrentStepDetailRows[1] == "Evidence distances|1 / 2");
        Check($"[next] one instruction, the next pair: '{ui.NextActionText}'", ui.NextActionText == $"Measure {Name(knife)} to {Name(spatter)}.");
        Check($"[measurement] fixed references render with values: {string.Join(" ; ", ui.FixedReferenceRows)}",
            ui.FixedReferenceRows.Count == 2 && ui.FixedReferenceRows[0].EndsWith(" m") && ui.FixedReferenceRows[1].EndsWith(" m"));
        Check($"[measurement] evidence distances: measured value + known-unmeasured dash: {string.Join(" ; ", ui.EvidenceDistanceRows)}",
            ui.EvidenceDistanceRows.Count == 2 && HasRow(ui.EvidenceDistanceRows, Name(body), true) && HasRow(ui.EvidenceDistanceRows, Name(spatter), false));
        Check("[measurement] the bottle (not photographed) is not listed, counted or named", !PanelMentions(bottle) && !new List<string>(ui.ListedEvidenceIds).Contains(bottle));
        CheckLabels(knife, "Scene Photography,Initial Photograph,Chalk Position,Evidence Marker,Blood Trace Marker,Measurement,Final Photograph,Blood Sample,Contain Blood Sample,Dry Blood Sample,Paper Package,Seal Blood Package,Store Blood Sample,Fingerprint Process,Record,Collect in Bag,Seal,Store Evidence Bag");
        Check($"[scroll] long knife procedure: overflow {ui.ProcedureOverflow:0} units, current row kept in view", ui.ProcedureOverflow > 0f && CurrentRowVisible());
        yield return Capture("2_knife_measurement");
        yield return CaptureEyeView("2b_knife_measurement_eye_view");
        string knifeBodyValue = RowValue(ui.EvidenceDistanceRows, Name(body));
        ClickTarget("evidence:" + body);
        yield return null;
        Check($"[pairs] no duplicate: the body shows the same single pair value ({RowValue(ui.EvidenceDistanceRows, Name(knife))}), never itself",
            RowValue(ui.EvidenceDistanceRows, Name(knife)) == knifeBodyValue && !HasRow(ui.EvidenceDistanceRows, Name(body), true) && !HasRow(ui.EvidenceDistanceRows, Name(body), false)
            && Distinct(ui.EvidenceDistanceRows));

        // --- Spatter during blood sample ---
        MeasurementTestUtil.RecordPairExact(knife, spatter);
        esm.MarkChalkMarked(spatter, ToolType.ChalkMark);
        esm.MarkTented(spatter, ToolType.EvidenceMarker, 2);
        MeasurementTestUtil.RecordExact(spatter, 1);
        MeasurementTestUtil.RecordExact(spatter, 2);
        MeasurementTestUtil.RecordAvailablePairsFor(spatter);
        esm.MarkPhotographedFinal(spatter, ToolType.Photographer);
        yield return DismissCheckpoints();
        Check($"[select] ray click selects {Name(spatter)}", ClickTarget("evidence:" + spatter) == "evidence:" + spatter && SelectedRowIs(spatter) && SelectedRowCount() == 1);
        Check($"[spatter] blood sample is the one Current step; NEXT: '{ui.NextActionText}'",
            CurrentStepId() == "blood_sample" && CountHighlighted() == 1 && ui.CurrentStepDetailRows.Count == 0 && ui.NextActionText.Length > 0);
        CheckLabels(spatter, "Scene Photography,Initial Photograph,Chalk Position,Evidence Marker,Measurement,Final Photograph,Blood Sample,Contain Blood Sample,Dry Blood Sample,Paper Package,Seal Blood Package,Store Blood Sample,Record");
        yield return Capture("3_spatter_blood_sample");

        // --- Bottle during lab fingerprint process ---
        ui.Hide();
        esm.MarkPhotographedRaw(bottle, ToolType.Photographer);
        yield return DismissCheckpoints();
        esm.MarkChalkMarked(bottle, ToolType.ChalkMark);
        esm.MarkTented(bottle, ToolType.EvidenceMarker, 3);
        esm.MarkPhotographedFinal(bottle, ToolType.Photographer);
        MeasurementTestUtil.CompleteDiscoveredMeasurements();
        var bottleLines = new List<string>();
        foreach (var option in esm.GetRecord(bottle).definition.reportLineOptions)
        {
            if (option != null && option.isAccurate) bottleLines.Add(option.id);
        }
        esm.RecordReportLinesSelected(bottle, bottleLines);
        var logged = esm.MarkLogged(bottle, ToolType.Recorder);
        yield return DismissCheckpoints();
        esm.MarkCollected(bottle, ToolType.EvidenceCollector);
        esm.MarkSealed(bottle, ToolType.EvidenceCollector);
        yield return DismissCheckpoints();
        ui.Show();
        yield return null;
        Check($"[select] ray click selects {Name(bottle)} (record {logged})", ClickTarget("evidence:" + bottle) == "evidence:" + bottle && SelectedRowIs(bottle) && SelectedRowCount() == 1);
        foreach (Transform row in ui.PanelTransform.Find("EvidenceList"))
        {
            var label = row.Find("Row/Label").GetComponent<TMP_Text>();
            label.ForceMeshUpdate();
            Check("[display] numbered evidence list label fits: " + label.text, !label.isTextOverflowing
                && label.preferredHeight <= label.rectTransform.rect.height + .5f);
        }
        Check($"[bottle] on-scene fingerprint process is the one Current step; NEXT: '{ui.NextActionText}'",
            CurrentStepId() == "fingerprint_process" && CountHighlighted() == 1 && ui.NextActionText == "Apply fingerprint powder.");
        CheckLabels(bottle, "Scene Photography,Initial Photograph,Chalk Position,Evidence Marker,Final Photograph,Measurement,Fingerprint Process,Record,Collect in Bag,Seal,Store Evidence Bag");
        Check($"[scroll] long bottle procedure overflows ({ui.ProcedureOverflow:0}) and the last (current) step is in view", ui.ProcedureOverflow > 0f && CurrentRowVisible());
        string nextBefore = ui.NextActionText;
        ui.ScrollBy(100000f);
        bool top = ui.VerticalPosition >= 0.99f;
        ui.ScrollBy(-100000f);
        bool bottom = ui.VerticalPosition <= 0.01f;
        Check("[scroll] one scroll area reaches top and bottom; the next action stays in place", top && bottom && ui.NextActionText == nextBefore);
        yield return Capture("4_bottle_lab_fingerprint");

        // --- Completed evidence ---
        yield return UIProcedureTestUtil.CollectAndStoreBlood(spatter);
        Check("[sample] real paper package sealed and B stored",UIProcedureTestUtil.LastSampleStored);
        esm.RecordReportLinesSelected(spatter, new[] { "blood-spatter-location", "blood-spatter-droplet-pattern", "blood-spatter-mechanism-only" });
        esm.MarkLogged(spatter, ToolType.Recorder);
        yield return DismissCheckpoints();
        // The real cabinet/storage fixture closes reading UI while operating tools.
        ui.ShowFor(spatter);
        report.AppendLine("  info: completed panel showing="+ui.IsShowing+" phase="+SceneTurnover.Phase+" owners="+string.Join(",",PlayerUIGate.LockOwnerNames));
        ClickTarget("evidence:" + spatter);
        yield return null;
        Check($"[complete] spatter: no Current step, DONE footer: '{ui.NextActionText}'",
            ui.ShowingProcedureComplete && CountHighlighted() == 0 && ui.NextActionText == $"Procedure complete for {Name(spatter)}.");
        yield return Capture("5_completed_evidence");

        // --- Hit targets ---
        Check("[targets] every target's hit area is larger than its visual and none overlap", TargetsValid(out string targetInfo));
        report.AppendLine("  info: " + targetInfo);
        Check("[targets] the ray on empty panel space clicks nothing", ui.Pointer.ClickWithRay(new Ray(cam.position, ui.PanelTransform.TransformPoint(new Vector3(120f, -200f, 0f)) - cam.position)) == null);

        // --- Modal ---
        Check("[modal] while open: UI gate and tool lock held (world tools inert)", ui.IsShowing && PlayerUIGate.IsBlocked && ToolInputLock.IsLocked);
        string selectedBeforeClose = ui.FocusedEvidenceId;
        Check("[close] ray click on the close button closes the panel", ClickTarget("close") == "close" && !ui.IsShowing);
        Check("[close] gate, locomotion hold and tool lock released", !PlayerUIGate.IsBlocked && !ToolInputLock.IsLocked);
        ui.Show();
        yield return null;
        Check("[close] reopening keeps the selected evidence", ui.FocusedEvidenceId == selectedBeforeClose);
        ui.Hide();

        // --- Reset ---
        ui.ResetSession();
        Check("[reset] reset forgets the selection and closes", ui.FocusedEvidenceId == null && !ui.IsShowing && ui.CurrentSteps.Count == 0);

        report.AppendLine(failures.Count == 0 ? "=== PASS, 0 failures ===" : $"=== FAIL: {failures.Count} failure(s) ===");
        Report = report.ToString();
        IsDone = true;
    }

    // --- Panel inspection ---------------------------------------------------------

    private string ClickTarget(string id)
    {
        Vector3 eye = Camera.main.transform.position;
        string hit = ui.Pointer.ClickWithRay(new Ray(eye, ui.Pointer.WorldCentre(id) - eye));
        return hit;
    }

    private Transform Content => ui.PanelTransform.Find("ProcedureViewport/Content");

    private int CountCurrent() => EvidenceProcedureMapResolver.CurrentCount(new List<ProcedureMapStep>(ui.CurrentSteps));

    private string CurrentStepId()
    {
        foreach (var s in ui.CurrentSteps)
        {
            if (s.state == ProcedureMapStepState.Current) return s.stepId;
        }
        return "none";
    }

    // Rendered rows carrying the current-step treatment.
    private int CountHighlighted()
    {
        int n = 0;
        foreach (Transform row in Content)
        {
            if (row.Find("CurrentBar") != null) n++;
        }
        return n;
    }

    private bool CurrentRowVisible()
    {
        Transform current = null;
        foreach (Transform row in Content)
        {
            if (row.Find("CurrentBar") != null) current = row;
        }
        if (current == null) return false;
        var viewport = (RectTransform)Content.parent;
        var corners = new Vector3[4];
        ((RectTransform)current).GetWorldCorners(corners);
        foreach (var c in corners)
        {
            Vector3 local = viewport.InverseTransformPoint(c);
            var r = viewport.rect;
            if (local.x < r.xMin - 1f || local.x > r.xMax + 1f || local.y < r.yMin - 1f || local.y > r.yMax + 1f) return false; // 1-unit float tolerance
        }
        return true;
    }

    private void CheckLabels(string id, string expected)
    {
        var labels = new List<string>();
        foreach (Transform row in Content)
        {
            if (!row.name.StartsWith("Step_")) continue;
            labels.Add(row.Find("Label").GetComponent<TMP_Text>().text);
        }
        string got = string.Join(",", labels);
        Check($"[{id}] checklist renders the item's own procedure: {got}", got == expected && ui.FocusedEvidenceId == id);
    }

    private int SelectedRowCount()
    {
        int n = 0;
        foreach (Transform row in ui.PanelTransform.Find("EvidenceList"))
        {
            var bar = row.Find("Row/SelectedBar");
            if (bar != null && bar.GetComponent<Image>().enabled) n++;
        }
        return n;
    }

    private bool SelectedRowIs(string id)
    {
        foreach (Transform row in ui.PanelTransform.Find("EvidenceList"))
        {
            var bar = row.Find("Row/SelectedBar");
            if (bar != null && bar.GetComponent<Image>().enabled)
            {
                return row.Find("Row/Label").GetComponent<TMP_Text>().text == Name(id);
            }
        }
        return false;
    }

    private bool PanelContains(string text)
    {
        foreach (var t in ui.PanelTransform.GetComponentsInChildren<TMP_Text>(true))
        {
            if (t.gameObject.activeInHierarchy && t.text.Contains(text)) return true;
        }
        return false;
    }

    private bool PanelMentions(params string[] ids)
    {
        foreach (var id in ids)
        {
            if (PanelContains(Name(id))) return true;
        }
        return false;
    }

    private static bool HasRow(IReadOnlyList<string> rows, string label, bool measured)
    {
        foreach (var r in rows)
        {
            var p = r.Split('|');
            if (p[0] == label && (p[1].EndsWith(" m") == measured)) return true;
        }
        return false;
    }

    private static string RowValue(IReadOnlyList<string> rows, string label)
    {
        foreach (var r in rows)
        {
            var p = r.Split('|');
            if (p[0] == label) return p[1];
        }
        return null;
    }

    private static bool Distinct(IReadOnlyList<string> rows)
    {
        var seen = new HashSet<string>();
        foreach (var r in rows)
        {
            if (!seen.Add(r.Split('|')[0])) return false;
        }
        return true;
    }

    // Grown hit rectangles in canvas space: larger than the visual, pairwise disjoint.
    private bool TargetsValid(out string info)
    {
        var canvas = (RectTransform)ui.PanelTransform;
        var rects = new List<Rect>();
        var sb = new StringBuilder();
        bool ok = true;
        foreach (var t in ui.Pointer.Targets)
        {
            if (t.rect == null || !t.rect.gameObject.activeInHierarchy) continue;
            var corners = new Vector3[4];
            t.rect.GetWorldCorners(corners);
            Vector3 a = canvas.InverseTransformPoint(corners[0]);
            Vector3 b = canvas.InverseTransformPoint(corners[2]);
            var visual = Rect.MinMaxRect(a.x, a.y, b.x, b.y);
            var hit = Rect.MinMaxRect(visual.xMin - t.padding.x, visual.yMin - t.padding.y, visual.xMax + t.padding.x, visual.yMax + t.padding.y);
            ok &= hit.width >= visual.width && hit.height > visual.height && (t.padding.x > 0f || t.padding.y > 0f);
            foreach (var other in rects)
            {
                var inner = Rect.MinMaxRect(hit.xMin + 0.5f, hit.yMin + 0.5f, hit.xMax - 0.5f, hit.yMax - 0.5f);
                if (inner.Overlaps(other)) ok = false; // touching edges are fine
            }
            rects.Add(hit);
            sb.Append($"{t.id} visual {visual.width:0}x{visual.height:0} -> hit {hit.width:0}x{hit.height:0} ({hit.width * 0.001f * 100f:0.0}x{hit.height * 0.001f * 100f:0.0} cm); ");
        }
        info = sb.ToString();
        return ok && rects.Count >= 2;
    }

    private void ReportAngles()
    {
        var cam = Camera.main.transform;
        var canvas = (RectTransform)ui.PanelTransform;
        var corners = new Vector3[4];
        canvas.GetWorldCorners(corners);
        Vector3 fwd = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
        float maxYaw = 0f, maxDown = 0f, maxUp = 0f;
        foreach (var c in corners)
        {
            Vector3 d = c - cam.position;
            float yaw = Vector3.Angle(Vector3.ProjectOnPlane(d, Vector3.up), fwd);
            float pitch = Mathf.Asin(d.normalized.y) * Mathf.Rad2Deg;
            maxYaw = Mathf.Max(maxYaw, yaw);
            maxDown = Mathf.Min(maxDown, pitch);
            maxUp = Mathf.Max(maxUp, pitch);
        }
        float distance = (canvas.position - cam.position).magnitude;
        float bodyDeg = 2f * Mathf.Atan(ForensicUIStyle.BodySize * 0.001f * 0.5f / distance) * Mathf.Rad2Deg;
        float metaDeg = 2f * Mathf.Atan(ForensicUIStyle.MetaSize * 0.001f * 0.5f / distance) * Mathf.Rad2Deg;
        report.AppendLine($"  info: edges at {maxYaw:0.0} deg left/right, {maxUp:0.0} deg up / {-maxDown:0.0} deg down; body text {ForensicUIStyle.BodySize * 0.001f * 1000f:0} mm em = {bodyDeg:0.00} deg, metadata {metaDeg:0.00} deg at {distance:0.00} m");
    }

    // --- Review screenshots ----------------------------------------------------------

    private IEnumerator Capture(string name)
    {
        if (string.IsNullOrEmpty(captureDirectory)) yield break;
        ui.Pointer.RayOverride = () => null; // no editor-controller cursor in review shots
        yield return null;
        var panel = ui.PanelTransform;
        var go = new GameObject("PanelCaptureCamera", typeof(Camera));
        var camera = go.GetComponent<Camera>();
        go.transform.SetPositionAndRotation(panel.position - panel.forward * 1.0f, panel.rotation);
        camera.fieldOfView = 42f;
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 3f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.02f, 0.02f, 0.03f, 1f);
        Save(camera, 1600, 1000, name, 42f);
        Destroy(go);
        ui.Pointer.RayOverride = null;
    }

    // What the player sees: the panel from the eye, at a Quest-like field of view.
    private IEnumerator CaptureEyeView(string name)
    {
        if (string.IsNullOrEmpty(captureDirectory)) yield break;
        yield return null;
        var go = new GameObject("EyeCaptureCamera", typeof(Camera));
        var camera = go.GetComponent<Camera>();
        var eye = Camera.main.transform;
        go.transform.SetPositionAndRotation(eye.position, Quaternion.LookRotation(Vector3.ProjectOnPlane(eye.forward, Vector3.up), Vector3.up));
        camera.fieldOfView = 90f;
        camera.nearClipPlane = 0.05f;
        Save(camera, 1200, 1100, name, 90f);
        Destroy(go);
    }

    private void Save(Camera camera, int width, int height, string name, float fov)
    {
        Canvas.ForceUpdateCanvases();
        var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4, useDynamicScale = false };
        camera.allowDynamicResolution = false; // a scaled viewport would crop/zoom the shot
        camera.rect = new Rect(0f, 0f, 1f, 1f);
        camera.targetTexture = rt;
        camera.aspect = (float)width / height;
        // Pin view and projection explicitly so nothing (XR camera tracking) can override the pose.
        var t = camera.transform;
        report.AppendLine($"  info: capture {name}: camera {Vector3.Distance(t.position, ui.PanelTransform.position):0.000} m from panel, reported fov {camera.fieldOfView:0.0}");
        camera.worldToCameraMatrix = Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) * Matrix4x4.TRS(t.position, t.rotation, Vector3.one).inverse;
        camera.projectionMatrix = Matrix4x4.Perspective(fov, (float)width / height, 0.05f, 20f);
        camera.Render();
        var previous = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();
        RenderTexture.active = previous;
        camera.targetTexture = null;
        System.IO.Directory.CreateDirectory(captureDirectory);
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(captureDirectory, name + ".png"), tex.EncodeToPNG());
        Destroy(tex);
        rt.Release();
        Destroy(rt);
    }

    private static IEnumerator DismissCheckpoints()
    {
        yield return null;
        for (int i = 0; i < 5; i++)
        {
            var checkpoint = Object.FindAnyObjectByType<HypothesisCheckpointUI>();
            if (checkpoint == null || !checkpoint.IsShowing)
            {
                yield break;
            }
            checkpoint.AnswerForTest(0, 0);
            yield return null;
        }
    }
}
