// Assets/_Project/Scripts/Testing/DevDiagnosticOverlay.cs
//
// TEMPORARY. In-headset development diagnostic overlay for the Quest X/Y
// tool-wheel/utility-menu investigation. Delete this file once that
// investigation is closed - it is not meant to ship or to survive long-term.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Self-spawns via RuntimeInitializeOnLoadMethod, so it needs no scene wiring and
/// can be deleted cleanly. Reads X/Y through its OWN independent InputActions
/// (never the tool wheel's or utility menu's) so it can never be fooled by the same
/// bug it exists to diagnose. Compiles out of every non-development build via the
/// file-level #if above.
/// </summary>
public class DevDiagnosticOverlay : MonoBehaviour
{
    // Turned off - was cluttering the view during normal Editor/Quest Link testing.
    // Flip back to true if the tool-wheel/utility-menu investigation this exists for
    // needs it again.
    private const bool Enabled = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (!Enabled)
        {
            return;
        }

        var go = new GameObject("DevDiagnosticOverlay (TEMPORARY - delete when done)");
        DontDestroyOnLoad(go);
        go.AddComponent<DevDiagnosticOverlay>();
    }

    private const float RefreshInterval = 0.15f;

    private InputAction rawX;
    private InputAction rawY;

    private TMP_Text label;
    private float refreshTimer;

    private ToolWheelController toolWheel;
    private UtilityMenuController utilityMenu;
    private PreSessionFlowController preSessionFlow;
    private PairingCodeIdentityPanel pairingPanel;
    private AssessmentUploadService uploadService;

    private void Awake()
    {
        // Independent of ToolWheelController/UtilityMenuController's own actions -
        // this must keep reading correctly even if theirs is the thing that's broken.
        rawX = new InputAction("Diag_RawX", InputActionType.Button, "<XRController>{LeftHand}/primaryButton");
        rawY = new InputAction("Diag_RawY", InputActionType.Button, "<XRController>{LeftHand}/secondaryButton");
        rawX.Enable();
        rawY.Enable();

        BuildUI();
    }

    private void OnDestroy()
    {
        rawX?.Dispose();
        rawY?.Dispose();
    }

    private void Update()
    {
        refreshTimer += Time.deltaTime;
        if (refreshTimer < RefreshInterval)
        {
            return;
        }
        refreshTimer = 0f;

        ResolveReferences();
        label.text = BuildReport();
    }

    private void ResolveReferences()
    {
        if (toolWheel == null) toolWheel = FindFirstObjectByType<ToolWheelController>();
        if (utilityMenu == null) utilityMenu = FindFirstObjectByType<UtilityMenuController>();
        if (preSessionFlow == null) preSessionFlow = FindFirstObjectByType<PreSessionFlowController>();
        if (pairingPanel == null) pairingPanel = FindFirstObjectByType<PairingCodeIdentityPanel>();
        if (uploadService == null) uploadService = FindFirstObjectByType<AssessmentUploadService>();
    }

    private string BuildReport()
    {
        var identity = PairingIdentityService.Instance;
        var sb = new StringBuilder();

        sb.AppendLine("<b>DEV DIAGNOSTIC (temporary)</b>");
        sb.AppendLine($"X raw: {(rawX.IsPressed() ? "<color=#5CFF5C>DOWN</color>" : "up")}   Y raw: {(rawY.IsPressed() ? "<color=#5CFF5C>DOWN</color>" : "up")}");
        sb.AppendLine($"X action enabled: {(utilityMenu != null ? utilityMenu.IsOpenActionEnabled.ToString() : "n/a (not found)")}");
        sb.AppendLine($"Y action enabled: {(toolWheel != null ? toolWheel.IsOpenActionEnabled.ToString() : "n/a (not found)")}");
        sb.AppendLine("Action map: none - X/Y are loose InputActions, not asset-backed");
        sb.AppendLine($"Gate blocked: {PlayerUIGate.IsBlocked}   locks: {PlayerUIGate.LockCount}");

        string owners = string.Join(", ", PlayerUIGate.LockOwnerNames);
        sb.AppendLine($"Lock owners: {(string.IsNullOrEmpty(owners) ? "(none)" : owners)}");

        sb.AppendLine($"PreSessionFlow unlocked: {(preSessionFlow != null ? preSessionFlow.SessionUnlocked.ToString() : "n/a (not found)")}");
        sb.AppendLine($"Paired: {(identity != null ? identity.IsPaired.ToString() : "n/a (no instance)")}   Token valid now: {(identity != null ? identity.TokenIsValidNow.ToString() : "n/a")}");
        sb.AppendLine($"  Student: {(identity != null ? identity.StudentDisplayName ?? "(none)" : "n/a")}   Real token present: {(identity != null && !string.IsNullOrEmpty(identity.AssessmentSessionToken))}");
        sb.AppendLine($"Pairing panel active: {(pairingPanel != null ? pairingPanel.IsPanelActive.ToString() : "n/a (not found)")}");
        sb.AppendLine(BuildUploadReport());

        bool canvasHierarchyActive = pairingPanel != null && pairingPanel.PanelRootObject != null && pairingPanel.PanelRootObject.activeInHierarchy;
        sb.AppendLine($"PairingUICanvas activeInHierarchy: {canvasHierarchyActive}");

        if (pairingPanel != null && pairingPanel.PanelRootObject != null)
        {
            var cg = pairingPanel.PanelRootObject.GetComponent<CanvasGroup>();
            var raycaster = pairingPanel.PanelRootObject.GetComponent<GraphicRaycaster>();
            sb.AppendLine($"  CanvasGroup blocksRaycasts: {(cg != null ? cg.blocksRaycasts.ToString() : "n/a")}  interactable: {(cg != null ? cg.interactable.ToString() : "n/a")}");
            sb.AppendLine($"  GraphicRaycaster enabled: {(raycaster != null ? raycaster.enabled.ToString() : "n/a")}");
        }

        sb.AppendLine($"ToolWheel GO active: {(toolWheel != null ? toolWheel.gameObject.activeInHierarchy.ToString() : "n/a (not found)")}  comp enabled: {(toolWheel != null ? toolWheel.enabled.ToString() : "n/a")}");
        sb.AppendLine($"UtilityMenu GO active: {(utilityMenu != null ? utilityMenu.gameObject.activeInHierarchy.ToString() : "n/a (not found)")}  comp enabled: {(utilityMenu != null ? utilityMenu.enabled.ToString() : "n/a")}");
        sb.AppendLine($"Last reject - Wheel(Y): {(toolWheel != null ? (toolWheel.LastRejectionReason ?? "(none)") : "n/a")}");
        sb.AppendLine($"Last reject - Menu(X): {(utilityMenu != null ? (utilityMenu.LastRejectionReason ?? "(none)") : "n/a")}");

        return sb.ToString();
    }

    // Real Supabase connectivity signal, distinct from "Paired" above - IsPaired is
    // true even under the dev bypass's fake identity (no real token), so it alone
    // can't tell you whether a session actually reached the backend. This reports
    // the most recently queued upload's real network result (Pending/Uploading/
    // Uploaded/AwaitingPairing/RetryWaiting/Rejected/FailedPermanently) and, for an
    // auth failure specifically, calls out that a real pairing is what's missing.
    private string BuildUploadReport()
    {
        if (uploadService == null)
        {
            return "Supabase upload: n/a (AssessmentUploadService not found)";
        }

        var entries = uploadService.GetAllEntries();
        if (entries == null || entries.Count == 0)
        {
            return "Supabase upload: (no session queued yet)";
        }

        AssessmentUploadQueueEntry latest = entries[0];
        foreach (var e in entries)
        {
            if (string.CompareOrdinal(e.createdAtUtc, latest.createdAtUtc) > 0)
            {
                latest = e;
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Supabase upload: {latest.status} (session {latest.sessionId.Substring(0, 8)}..., attempts {latest.attemptCount})");
        if (latest.lastErrorCategory != AssessmentUploadErrorCategory.None)
        {
            sb.Append($"  last error: {latest.lastErrorCategory} - {latest.lastErrorMessage}");
        }

        return sb.ToString();
    }

    // Same world-space-canvas-pinned-to-camera recipe already used by ToolWheelController/
    // UtilityMenuController/etc. (HudFollowCamera) - kept off to the side and low so it
    // never blocks the evidence scene while still staying easily readable in the headset.
    private void BuildUI()
    {
        var canvasGO = new GameObject("DevDiagnosticCanvas");
        canvasGO.transform.SetParent(transform, false);
        var rootRect = canvasGO.AddComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(900, 620);
        canvasGO.transform.localScale = Vector3.one * 0.0006f;

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = short.MaxValue;

        var hud = canvasGO.AddComponent<HudFollowCamera>();
        hud.Distance = 0.5f;
        hud.LocalOffset = new Vector3(0.28f, -0.16f, 0f);

        var bgGO = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
        bgGO.transform.SetParent(rootRect, false);
        var bgRect = bgGO.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        bgGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.78f);

        var labelGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelGO.transform.SetParent(rootRect, false);
        var labelRect = labelGO.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(20, 20);
        labelRect.offsetMax = new Vector2(-20, -20);

        label = labelGO.GetComponent<TextMeshProUGUI>();
        label.fontSize = 26f;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.TopLeft;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.text = "DEV DIAGNOSTIC - waiting for first refresh...";
    }
}
#endif
