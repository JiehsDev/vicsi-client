using System.Collections;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Rendered bounds and live presenter checks for content-sized dialogue.</summary>
public class AdaptiveDialogueLayoutTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }
    private readonly StringBuilder report = new();
    private int failures;
    public void RunAllTests() => StartCoroutine(Run());
    private void Check(string name, bool ok)
    {
        report.AppendLine($"  {(ok ? "PASS" : "FAIL")}: {name}");
        if (!ok) failures++;
    }

    private static bool Fits(TMP_Text label)
    {
        label.ForceMeshUpdate();
        var bounds = label.textBounds;
        var rect = label.rectTransform.rect;
        return bounds.min.x >= rect.xMin - 1 && bounds.max.x <= rect.xMax + 1
            && bounds.min.y >= rect.yMin - 1 && bounds.max.y <= rect.yMax + 1;
    }

    private static TMP_Text MakePanel(Transform parent, string name, Vector2 position, out RectTransform panel)
    {
        var go = new GameObject(name, typeof(RectTransform));
        panel = (RectTransform)go.transform;
        panel.SetParent(parent, false);
        panel.sizeDelta = new Vector2(480, 120);
        panel.anchoredPosition = position;
        var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(panel, false);
        background.transform.localScale = new Vector3(1, .78841f, 1);
        background.GetComponent<Image>().color = new Color(.045f, .075f, .11f, .98f);
        var textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(panel, false);
        var label = textObject.GetComponent<TMP_Text>();
        label.fontSize = 20;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        return label;
    }

    private static bool BackgroundMatches(RectTransform panel)
    {
        var corners = new Vector3[4];
        var visible = new Vector3[4];
        panel.GetWorldCorners(corners);
        var image = panel.GetComponentInChildren<Image>(true);
        if (image == null) return false;
        image.rectTransform.GetWorldCorners(visible);
        for (int i = 0; i < 4; i++) if (Vector3.Distance(corners[i], visible[i]) > .001f) return false;
        return true;
    }

    private static bool CountdownFits(MonoBehaviour presenter, TMP_Text label)
    {
        var bar = presenter.GetComponentInChildren<DialogueCountdownBar>(true);
        if (bar == null) return false;
        var panel = (RectTransform)presenter.transform;
        var corners = new Vector3[4];
        ((RectTransform)bar.transform).GetWorldCorners(corners);
        float bottom = panel.InverseTransformPoint(corners[0]).y;
        float top = panel.InverseTransformPoint(corners[1]).y;
        label.rectTransform.GetWorldCorners(corners);
        float textBottom = panel.InverseTransformPoint(corners[0]).y;
        return bottom >= panel.rect.yMin && top < textBottom;
    }

    private IEnumerator Run()
    {
        yield return null;
        report.AppendLine("=== AdaptiveDialogueLayoutTest ===");
        var root = new GameObject("Dialogue layout preview", typeof(RectTransform), typeof(Canvas));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        ((RectTransform)root.transform).sizeDelta = new Vector2(1200, 900);
        root.transform.position = new Vector3(1000, 1000, 1000);
        root.transform.localScale = Vector3.one * .01f;
        var label = MakePanel(root.transform, "Dialogue", new Vector2(-285, 180), out var panel);
        var layout = new DialoguePanelLayout(panel, label, 120, false);
        label.text = "Evidence secured.";
        layout.Fit();
        Vector2 compact = panel.rect.size;
        Check("Short line uses a compact background", compact.x < 480 && compact.y < 120);
        Check("Font has a readable floor and never autoshrinks", label.fontSize >= 24 && !label.enableAutoSizing);
        Check("Short text stays inside the label", Fits(label));
        Check("Authored background scale/offsets are corrected", BackgroundMatches(panel));
        Check("Bottom edge stays at its authored location", Mathf.Abs(panel.rect.yMin + 60) < .01f);
        label.text = "Compare the recovered fingerprint with each reference. Look for ridge endings and splits. "
            + "You can zoom and rotate both prints, then choose the reference you think matches. "
            + "If your choice does not match, compare the ridge details and try again. There is no attempt limit.\n\n"
            + "This is a practice comparison. A trained fingerprint examiner must evaluate and verify real case findings.";
        layout.Fit();
        Vector2 expanded = panel.rect.size;
        Check("Long line expands to the authored maximum width", Mathf.Approximately(expanded.x, 480));
        Check("Long text grows beyond the former clipping height", expanded.y > DialogueTimingSettings.Current.maxPanelHeight);
        Check("All long text fits, including explicit paragraph breaks", Fits(label));
        Check("Long background matches the measured panel rectangle", BackgroundMatches(panel));
        Check("Long text preserves the font size", Mathf.Approximately(label.fontSize, 24));
        Check("Expanded bottom edge remains fixed", Mathf.Abs(panel.rect.yMin + 60) < .01f);
        layout.Fit();
        Check("Repeated fitting is stable", panel.rect.size == expanded);
        label.text = "Evidence secured.";
        layout.Fit();
        Check("A short replacement restores the compact size", panel.rect.size == compact);

        int checkedEntries = 0, clippedEntries = 0;
        string firstClipped = null;
        foreach (var entry in ViCSIDialogueCatalog.Current.entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.text)) continue;
            label.text = EvidenceDisplay.Text(entry.text);
            layout.Fit();
            checkedEntries++;
            if (!Fits(label)) { clippedEntries++; firstClipped ??= entry.id; }
        }
        Check($"All {checkedEntries} catalog messages fit their rendered bounds (clipped={clippedEntries}, first={firstClipped})", checkedEntries > 100 && clippedEntries == 0);
        label.text = "Evidence secured.";
        layout.Fit();

        var lower = MakePanel(root.transform, "Long guidance", new Vector2(285, 240), out var lowerPanel);
        var lowerLayout = new DialoguePanelLayout(lowerPanel, lower, 120, true);
        lower.text = "Before collecting another sample, return to the supply table outside the house.\n\n"
            + "Replace your gloves, take a fresh packaged swab, and return to the evidence. "
            + "Keep each collected sample separate to avoid cross-contamination.\n\n"
            + "Use the waypoint to find the supply table.";
        lowerLayout.Fit();
        Check("Tooltip top edge stays at its authored location", Mathf.Abs(lowerPanel.rect.yMax - 60) < .01f);
        Check("Long tooltip has real padding on all sides", Fits(lower) && lower.rectTransform.offsetMin.x >= 24 && lower.rectTransform.offsetMin.y >= 16);
        string longToken = new string('W', 130);
        label.text = longToken;
        layout.Fit();
        Check("A long unbroken word wraps inside the background", Fits(label));
        label.text = "Evidence secured.";
        layout.Fit();

        var live = STCSNotificationUI.Instance;
        var tooltip = NotificationManager.Instance?.UI;
        Check("Both live scene presenters exist", live != null && tooltip != null);
        if (live != null && tooltip != null)
        {
            live.HideImmediately();
            live.ShowLine(lower.text, null, "Team", "adaptive-layout-test");
            var liveLabel = live.GetComponentInChildren<TMP_Text>(true);
            Check("Dialogue presenter applies content fitting", Fits(liveLabel));
            Check("Real dialogue background encloses the text panel", BackgroundMatches((RectTransform)live.transform));
            Check("Dialogue countdown stays below text inside its padding", CountdownFits(live, liveLabel));
            float tall = ((RectTransform)live.transform).rect.height;
            live.HideImmediately();
            live.ShowLine("Ready.", null, "Team", "adaptive-layout-short");
            Check("Dialogue presenter shrinks for the next short line", ((RectTransform)live.transform).rect.height < tall);
            tooltip.ShowPrompt("[B] Replace gloves");
            tooltip.ShowTooltip(lower.text, 10);
            var tooltipLabel = tooltip.GetComponentInChildren<TMP_Text>(true);
            Check("Tooltip presenter applies content fitting", Fits(tooltipLabel));
            Check("Real tooltip background matches its measured rectangle", BackgroundMatches((RectTransform)tooltip.transform));
            Check("Tooltip countdown stays below text inside its padding", CountdownFits(tooltip, tooltipLabel));
            tall = ((RectTransform)tooltip.transform).rect.height;
            tooltip.ClearTooltip();
            Check("Ambient prompt returns at its compact size", ((RectTransform)tooltip.transform).rect.height < tall && Fits(tooltipLabel));
            live.HideImmediately();
            tooltip.HidePrompt();
        }

        var cameraObject = new GameObject("Dialogue preview camera", typeof(Camera));
        var camera = cameraObject.GetComponent<Camera>();
        camera.transform.position = root.transform.position + Vector3.back * 10;
        camera.orthographic = true;
        camera.orthographicSize = 4.5f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.16f, .19f, .23f);
        var target = RenderTexture.GetTemporary(1200, 900, 24);
        var previous = RenderTexture.active;
        var texture = new Texture2D(1200, 900, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 1200, 900), 0, 0);
            texture.Apply();
            var args = System.Environment.GetCommandLineArgs();
            int index = System.Array.IndexOf(args, "-phase5Output");
            string folder = index >= 0 ? Path.GetDirectoryName(args[index + 1]) : "docs/evidence/adaptive-dialogue";
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, "short-and-long-dialogue.png"), texture.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            Destroy(texture);
            Destroy(cameraObject);
            Destroy(root);
        }
        report.AppendLine(failures == 0 ? "=== PASS ===" : $"=== FAIL: {failures} ===");
        Report = report.ToString();
        IsDone = true;
    }
}
