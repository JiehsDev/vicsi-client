using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Body-only presentation/action adapter. EvidenceStateManager owns every milestone.</summary>
[RequireComponent(typeof(EvidenceProp))]
[DefaultExecutionOrder(100)]
public sealed class BodyRemovalInteraction : MonoBehaviour
{
    public const string TrainingMessage = "BODY-A was identified, wrapped, placed in a cadaver bag, and prepared for transport for medico-legal examination.";
    [SerializeField] private OVRScreenFade screenFade;
    // Explicit dependency keeps the SDK fade shader in Quest builds.
    [SerializeField] private Material fadeShaderReference;
    [SerializeField] private Renderer[] bodyVisuals;
    [SerializeField] private Collider[] bodyColliders;
    [SerializeField, Min(.1f)] private float fadeSeconds = .3f;
    [SerializeField, Min(1f)] private float messageSeconds = 4f;
    private EvidenceProp prop;
    private InputAction accept, cancel;
    private GameObject panel;
    private TMP_Text heading, message, action, controls;
    private WorldPanelPointer pointer;
    private bool locksHeld, promptShown;
    private Material fadeMaterial;
    private readonly List<Canvas> suspendedCanvases = new();
    private float confirmAfter;
    private int closedFrame = -1;
    public bool IsTransitioning { get; private set; }
    public bool IsShowing => panel != null && panel.activeSelf;
    public bool IsAvailable => EvidenceStateManager.Instance != null
        && EvidenceStateManager.Instance.GetBodyRemovalBlockReason(prop.evidenceId) == null;
    public WorldPanelPointer Pointer => pointer;
    public float FadeAlpha => screenFade != null ? screenFade.currentAlpha : 0f;
    public bool VisualRemoved => bodyVisuals != null && System.Array.TrueForAll(bodyVisuals, r => r == null || !r.enabled);

    private void Awake()
    {
        prop = GetComponent<EvidenceProp>();
        accept = PlayerUIGate.CreateConfirmAction(this, "BodyRemoval_A", () => !IsTransitioning && (IsShowing || (WorldAvailable && InRange() && IsAvailable)), 600);
        cancel = new InputAction("BodyRemoval_B", InputActionType.Button, "<XRController>{RightHand}/secondaryButton");
        BuildPanel();
    }
    private void OnEnable()
    {
        accept.Enable(); cancel.Enable();
        EvidenceStateManager.OnBodyProcedureChanged += RefreshVisual;
    }
    private void Start()
    {
        // OVRScreenFade creates its mesh in Start. With fadeOnStart=false its new
        // renderer otherwise stays enabled with no material until the first fade.
        if (screenFade != null) ApplyFade(0f);
    }
    private void OnDisable()
    {
        EvidenceStateManager.OnBodyProcedureChanged -= RefreshVisual;
        accept.Disable(); cancel.Disable(); StopAllCoroutines();
        IsTransitioning = false;
        if (screenFade != null) ApplyFade(0f);
        RestoreCanvases();
        HidePanel(); ClearPrompt();
    }
    private void OnDestroy() { accept.Dispose(); cancel.Dispose(); pointer.Dispose(); if (panel != null) Destroy(panel); if (fadeMaterial != null) Destroy(fadeMaterial); }
    private bool InRange()
    {
        if (Camera.main == null) return false;
        Vector3 distance = Camera.main.transform.position - transform.position; distance.y = 0;
        return distance.sqrMagnitude <= prop.InteractionRadius * prop.InteractionRadius;
    }
    private bool WorldAvailable => !SceneTurnover.IsInvestigationBlocked && !PlayerUIGate.IsBlocked
        && !ToolInputLock.HasUIOwners && (STCSNotificationUI.Instance == null || !STCSNotificationUI.Instance.IsShowing)
        && PlayerToolRegistry.VirtuallyEquippedRole == ToolType.None
        && (HeldItemCoordinator.Instance == null || HeldItemCoordinator.Instance.CurrentCategory == HeldItemCoordinator.HeldCategory.None)
        && (EvidenceInventory.Instance == null || EvidenceInventory.Instance.CurrentHeldEvidenceId == null)
        && Time.frameCount != closedFrame;
    private void Update()
    {
        if (IsTransitioning) return;
        if (IsShowing)
        {
            if (cancel.WasPressedThisFrame()) { HidePanel(); return; }
            if (Time.unscaledTime >= confirmAfter) { pointer.Tick(); if (PlayerUIGate.TryConsumeConfirm(this, accept)) ConfirmPreparation(); }
            return;
        }
        bool show = WorldAvailable && InRange() && IsAvailable;
        // Marker cleanup owns the ambient hint while an eligible tent is in reach.
        // Release our flag without clearing its B hint, then restore A after pickup.
        bool markerHint = Camera.main != null && !PlayerUIGate.IsBlocked && !ToolInputLock.HasUIOwners
            && !SceneTurnover.IsInvestigationBlocked
            && EvidenceTentPickupCoordinator.FindNearestInRange(Camera.main.transform.position) != null;
        if (markerHint) promptShown = false;
        else if (show != promptShown)
        {
            if (show) { NotificationManager.ShowPrompt("Press A: Prepare Body for Removal", new Vector3(0, -.16f, 0)); promptShown = true; }
            else ClearPrompt();
        }
        if (show && PlayerUIGate.TryConsumeConfirm(this, accept)) RequestPreparation();
    }
    public bool RequestPreparation()
    {
        if (!WorldAvailable || !InRange() || IsShowing || IsTransitioning) return false;
        string reason = EvidenceStateManager.Instance?.GetBodyRemovalBlockReason(prop.evidenceId);
        if (reason != null)
        {
            // Existing guidance channel, no scored transition/violation.
            EvidenceStateManager.Instance.MarkBodyPreparedForRemoval(prop.evidenceId);
            NotificationManager.Notify(reason, DialogueKind.Guidance);
            return false;
        }
        if (screenFade == null || !screenFade.isActiveAndEnabled || Camera.main == null || fadeShaderReference == null)
        {
            NotificationManager.Notify("Body removal transition is unavailable. Please contact the instructor.", DialogueKind.Guidance);
            return false;
        }
        ClearPrompt();
        heading.text = "Prepare Body for Removal";
        message.text = "Confirm that the required body documentation is complete. Identification tagging, wrapping, cadaver-bag placement and transport preparation will be represented by a short transition.";
        action.text = "Prepare Body for Removal"; controls.text = "A / trigger: Confirm    B: Cancel";
        action.gameObject.SetActive(true); controls.gameObject.SetActive(true);
        panel.GetComponent<ResponsivePanelLayout>().Refresh();
        WorldPanelPlacement.PlaceInFront(panel.transform);
        HudFollowCamera.ConfigureShared(panel, this, PlayerUIGate.CriticalPriority); panel.SetActive(true);
        Acquire(); pointer.Enable(); confirmAfter = Time.unscaledTime + .25f;
        return true;
    }
    public bool ConfirmPreparation()
    {
        if (!IsShowing || IsTransitioning || Time.unscaledTime < confirmAfter || PlayerUIGate.IsBlockedByOther(this)) return false;
        string reason = EvidenceStateManager.Instance.GetBodyRemovalBlockReason(prop.evidenceId);
        if (reason != null) { NotificationManager.Notify(reason, DialogueKind.Guidance); HidePanel(); return false; }
        if (screenFade == null || !screenFade.isActiveAndEnabled) { HidePanel(); return false; }
        IsTransitioning = true; pointer.Disable(); panel.SetActive(false);
        foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (canvas != panel.GetComponent<Canvas>() && canvas.enabled) { suspendedCanvases.Add(canvas); canvas.enabled = false; }
        StartCoroutine(RemoveBody()); return true;
    }
    private IEnumerator RemoveBody()
    {
        try
        {
            yield return Fade(0f, 1f);
            // Commit only at full black, after a final authoritative prerequisite check.
            if (EvidenceStateManager.Instance.MarkBodyPreparedForRemoval(prop.evidenceId) == TransitionResult.Violation) yield break;
            heading.text = "Body Removal Procedure"; message.text = TrainingMessage;
            action.gameObject.SetActive(false); controls.gameObject.SetActive(false);
            panel.transform.SetParent(Camera.main.transform, false);
            panel.transform.localPosition = new Vector3(0, 0, .65f); panel.transform.localRotation = Quaternion.identity;
            panel.transform.localScale = Vector3.one * .00065f;
            panel.SetActive(true);
            heading.ForceMeshUpdate(); message.ForceMeshUpdate();
            DialogueHistoryManager.Record(TrainingMessage, "Body Removal Procedure", "Training", "body-removal:" + prop.evidenceId);
            // The body record and original-position measurement anchors remain alive.
            EvidenceStateManager.Instance.MarkBodyRemoved(prop.evidenceId);
            RefreshVisual(prop.evidenceId);
            yield return new WaitForSecondsRealtime(messageSeconds);
            panel.SetActive(false); panel.transform.SetParent(transform, true);
            yield return Fade(1f, 0f);
        }
        finally
        {
            if (screenFade != null) ApplyFade(0f);
            RestoreCanvases();
            IsTransitioning = false; HidePanel();
        }
    }
    private IEnumerator Fade(float from, float to)
    {
        float elapsed = 0;
        while (elapsed < fadeSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            ApplyFade(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / fadeSeconds)));
            yield return null;
        }
        ApplyFade(to);
    }
    private void ApplyFade(float alpha)
    {
        screenFade.SetExplicitFade(alpha);
        // SDK supplies the stereo camera-attached fade mesh/alpha. Its legacy fixed-function
        // shader needs a URP-compatible material in this project. Draw below the training UI.
        var renderer = screenFade.GetComponent<MeshRenderer>();
        if (renderer == null || fadeShaderReference == null) return;
        if (fadeMaterial == null) fadeMaterial = new Material(fadeShaderReference);
        fadeMaterial.SetColor("_BaseColor", new Color(0, 0, 0, alpha));
        renderer.sharedMaterial = fadeMaterial;
    }
    private void RestoreCanvases() { foreach (var canvas in suspendedCanvases) if (canvas != null) canvas.enabled = true; suspendedCanvases.Clear(); }
    private void RefreshVisual(string id)
    {
        if (id != prop.evidenceId || EvidenceStateManager.Instance.GetRecord(id)?.bodyRemovalStatus != BodyRemovalStatus.Removed) return;
        foreach (var renderer in bodyVisuals) if (renderer != null) renderer.enabled = false;
        foreach (var collider in bodyColliders) if (collider != null) collider.enabled = false;
        // Keep outline/marker/measurement anchors and the EvidenceProp's data identity.
    }
    private void Acquire() { if (locksHeld) return; locksHeld = true; PlayerUIGate.Enter(this); ToolInputLock.Enter(this); LocomotionSuspender.Suspend(); }
    private void Release() { if (!locksHeld) return; locksHeld = false; PlayerUIGate.Exit(this); ToolInputLock.Exit(this); LocomotionSuspender.Resume(); }
    private void HidePanel() { if (panel != null) panel.SetActive(false); pointer?.Disable(); Release(); closedFrame = Time.frameCount; }
    private void ClearPrompt() { if (!promptShown) return; promptShown = false; NotificationManager.HidePrompt(); }
    [EditableSceneBuilder]
    private void BuildPanel()
    {
        using (EditableSceneVisuals.Begin(this,"BuildPanel"))
        {
        panel = EditableSceneVisuals.Create("BodyRemovalProcedure", typeof(RectTransform), typeof(Canvas)); panel.transform.SetParent(transform, false);
        var rect = (RectTransform)panel.transform; rect.sizeDelta = new Vector2(900, 620); rect.localScale = Vector3.one * .001f;
        var canvas = panel.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.sortingOrder = short.MaxValue;
        var bg = ForensicUIStyle.Rect(panel.transform, "Background", ForensicUIStyle.PanelBackground).rectTransform;
        bg.anchorMin = Vector2.zero; bg.anchorMax = Vector2.one; bg.offsetMin = bg.offsetMax = Vector2.zero;
        heading = Label("Heading", new Vector2(0, 240), new Vector2(820, 70), 36);
        message = Label("Training", new Vector2(0, 15), new Vector2(800, 340), 32);
        action = Label("Confirm", new Vector2(0, -220), new Vector2(800, 65), 30);
        controls = Label("Controls", new Vector2(0, -280), new Vector2(820, 40), 24);
        pointer = new WorldPanelPointer(rect, "BodyRemoval", this); pointer.Register("body-removal-confirm", action.rectTransform, new Vector2(5, 5), () => ConfirmPreparation());
        var responsive = ResponsivePanelLayout.Create(rect, heading, null, controls, 620, 800, 40);
        responsive.AddText(message, responsive.Body, 48);
        var actionRow = responsive.AddText(action, responsive.Footer, 65);
        actionRow.SetAsFirstSibling();
        pointer.Unregister("body-removal-confirm");
        pointer.Register("body-removal-confirm", actionRow, Vector2.zero, () => ConfirmPreparation());
        responsive.RegisterControls(pointer, "layout-body-");
        panel.SetActive(false);
            }
    }
    private TMP_Text Label(string name, Vector2 position, Vector2 size, float fontSize)
    {
        var label = ForensicUIStyle.Text(panel.transform, name, fontSize, ForensicUIStyle.TextPrimary);
        label.rectTransform.sizeDelta = size; label.rectTransform.anchoredPosition = position; label.alignment = TextAlignmentOptions.Center;
        label.enableWordWrapping = true; return label;
    }
}
