using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Entrance logging table plus a bounded review/check-in panel using shared VR input and layout.</summary>
public sealed class SceneAccessRegisterUI : MonoBehaviour
{
    private SceneAccessRegister register;
    private SceneCordon cordon;
    [SerializeField] private bool useAuthoredPlacement;
    private InputAction confirm, cancel, scroll;
    private GameObject panel;
    private TMP_Text footer, action, statusLabel;
    private Text stationLabel;
    private ResponsivePanelLayout layout;
    private WorldPanelPointer pointer;
    private RectTransform actionRow;
    private bool ownsPrompt, ownsUI;
    private float inputAfter;
    private readonly List<GameObject> rows = new();
    private readonly List<Material> materials = new();
    public bool IsShowing => panel != null && panel.activeSelf;
    public RectTransform PanelRect => (RectTransform)panel.transform;
    public ResponsivePanelLayout Layout => layout;
    public WorldPanelPointer Pointer => pointer;
    public bool IsInRange => Camera.main != null && Vector2.Distance(new Vector2(Camera.main.transform.position.x, Camera.main.transform.position.z),
        new Vector2(transform.position.x, transform.position.z)) <= 1.5f;
    private bool Quiet => STCSNotificationUI.Instance == null || (!STCSNotificationUI.Instance.IsShowing && STCSNotificationUI.Instance.PendingCount == 0);
    private bool CanOpen => register != null && (!register.IsFinalized || register.IsAuthorized) && IsInRange && !PlayerUIGate.IsBlocked && Quiet
        && !SceneTurnover.IsPending && cordon.PPEReady && (register.IsAuthorized || SceneTurnover.Phase == ScenarioPhase.IOCAssessment)
        && (HeldItemCoordinator.Instance == null || HeldItemCoordinator.Instance.CurrentCategory == HeldItemCoordinator.HeldCategory.None);
    private void Awake()
    {
        confirm = PlayerUIGate.CreateConfirmAction(this, "SceneAccessRegister", () => IsShowing ? !PlayerUIGate.IsBlockedByOther(this) : CanOpen, PlayerUIGate.InformationPriority);
        cancel = new InputAction("SceneAccessRegister_Cancel", InputActionType.Button, "<XRController>{RightHand}/secondaryButton");
        scroll = new InputAction("SceneAccessRegister_Scroll", InputActionType.Value, "<XRController>{RightHand}/thumbstick");
        BuildPanel();
    }
    public void Initialize(SceneAccessRegister model, SceneCordon owner)
    {
        register = model; cordon = owner; register.Changed += Refresh;
        var left = cordon.transform.Find("EntrancePost_L"); var right = cordon.transform.Find("EntrancePost_R");
        var rig = FindAnyObjectByType<OVRCameraRig>();
        Vector3 across = (right.position - left.position).normalized;
        Vector3 entry = (right.position + left.position) * .5f;
        Vector3 towardPlayer = rig != null ? rig.transform.position - entry : Vector3.back;
        towardPlayer.y = 0; towardPlayer.Normalize();
        Vector3 at = left.position - across * .32f + towardPlayer * .65f;
        at.y = cordon.PPEStation != null ? cordon.PPEStation.transform.position.y : 0;
        Vector3 facing = at - (rig != null ? rig.transform.position : entry + towardPlayer); facing.y = 0;
        if (!useAuthoredPlacement)
        {
            transform.SetPositionAndRotation(at, Quaternion.LookRotation(facing.normalized));
            if (cordon.RegisterStationAnchor != null)
                transform.SetPositionAndRotation(cordon.RegisterStationAnchor.position, cordon.RegisterStationAnchor.rotation);
        }
        BuildLoggingTable();
    }
    private void OnEnable() { confirm.Enable(); cancel.Enable(); scroll.Enable(); }
    private void OnDisable() { Hide(); HidePrompt(); confirm.Disable(); cancel.Disable(); scroll.Disable(); }
    private void OnDestroy()
    {
        if (register != null) register.Changed -= Refresh;
        pointer?.Dispose(); confirm?.Dispose(); cancel?.Dispose(); scroll?.Dispose();
        foreach (var material in materials) Destroy(material);
    }
    private void Update()
    {
        if (register == null) return;
        if (stationLabel != null) stationLabel.text = "SCENE ACCESS REGISTER\nFirst Responder\n" +
            (SceneTurnover.IsPending ? "Receive briefing first" : !cordon.PPEReady ? "Put on gloves first" : register.IsAuthorized ? "[A] Review visits" : "[A] Check in before entry");
        if (IsShowing)
        {
            if (!IsInRange || PlayerUIGate.IsBlockedByOther(this) || cancel.WasPressedThisFrame()) { Hide(); return; }
            float y = scroll.ReadValue<Vector2>().y; if (Mathf.Abs(y) > .2f) layout.ScrollOptions(-y * 350 * Time.unscaledDeltaTime);
            pointer.Tick();
            if (PlayerUIGate.TryConsumeConfirm(this, confirm)) { if (register.IsAuthorized) Hide(); else ConfirmCheckIn(); }
            return;
        }
        if (CanOpen)
        {
            if (!ownsPrompt) { NotificationManager.ShowPrompt(register.IsAuthorized ? "[A] Review scene access register" : "[A] Check in with First Responder"); ownsPrompt = true; }
            if (PlayerUIGate.TryConsumeConfirm(this, confirm)) Open();
        }
        else HidePrompt();
    }
    public bool Open()
    {
        if (IsShowing || !CanOpen) return false;
        register.RetrySave(); Refresh(); HidePrompt(); WorldPanelPlacement.PlaceInFront(panel.transform);
        HudFollowCamera.ConfigureShared(panel, this, PlayerUIGate.InformationPriority);
        panel.SetActive(true); pointer.Enable(); PlayerUIGate.Enter(this); ToolInputLock.Enter(this); ownsUI = true;
        LocomotionSuspender.Suspend();
        layout.Body.Scroll.verticalNormalizedPosition = 1;
        inputAfter = Time.unscaledTime + .25f; return true;
    }
    public bool ConfirmCheckIn()
    {
        if (!IsShowing || Time.unscaledTime < inputAfter || !IsInRange || !Quiet || PlayerUIGate.IsBlockedByOther(this)) return false;
        if (!register.Authorize(out string reason)) { statusLabel.text = reason ?? "Check-in is already confirmed."; statusLabel.color = ForensicUIStyle.Warning; layout.Refresh(); return false; }
        Hide(); GameDialogue.SayLine("access-check-in-confirmed", "Scene security", "scene-access-register");
        if (SceneTurnover.Phase == ScenarioPhase.IOCAssessment) GameDialogue.SayLine("ioc-assessment-guidance", "System", "ioc-assessment");
        return true;
    }
    public void Hide()
    {
        if (panel != null) panel.SetActive(false); pointer?.Disable();
        if (ownsUI) { ownsUI = false; PlayerUIGate.Exit(this); ToolInputLock.Exit(this); LocomotionSuspender.Resume(); }
    }
    private void HidePrompt() { if (ownsPrompt) NotificationManager.HidePrompt(); ownsPrompt = false; }
    private TMP_Text Label(string name, float size, string text)
    {
        var label = ForensicUIStyle.Text(panel.transform, name, size, ForensicUIStyle.TextPrimary);
        // Rows are measured while the review is hidden, before TMP's OnEnable initializes its font.
        label.font = TMP_Settings.defaultFontAsset;
        label.richText = false; label.overflowMode = TextOverflowModes.Overflow; label.text = text; return label;
    }
    [EditableSceneBuilder]
    private void BuildPanel()
    {
        using (EditableSceneVisuals.Begin(this,"BuildPanel"))
        {
        panel = EditableSceneVisuals.Create("Scene access register review", typeof(RectTransform), typeof(Canvas)); panel.transform.SetParent(transform, false);
        var rect = (RectTransform)panel.transform; rect.sizeDelta = new Vector2(820, 600); rect.localScale = Vector3.one * .001f;
        var canvas = panel.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.sortingOrder = short.MaxValue - 5;
        var bg = ForensicUIStyle.Rect(panel.transform, "Background", ForensicUIStyle.PanelBackground).rectTransform;
        bg.anchorMin = Vector2.zero; bg.anchorMax = Vector2.one; bg.offsetMin = bg.offsetMax = Vector2.zero;
        var title = Label("Title", 34, "Scene access register");
        footer = Label("Controls", 22, "Times are elapsed from scene start.\nRight stick: scroll");
        action = Label("Confirm check-in", 28, "[A] Confirm check-in");
        pointer = new WorldPanelPointer(rect, "SceneAccess", this);
        layout = ResponsivePanelLayout.Create(rect, title, null, footer, 520, 760, 36);
        var keeper = Label("Register keeper", 23, "Managed by First Responder"); keeper.color = ForensicUIStyle.TextSecondary;
        layout.AddText(keeper, layout.Header, 34, 4);
        var actions = ResponsivePanelLayout.Rect(layout.Footer.Content, "Register actions");
        var height = actions.gameObject.AddComponent<LayoutElement>(); height.minHeight = height.preferredHeight = 70;
        actions.SetAsFirstSibling();
        actionRow = ActionButton(actions, "Check in", action, 0, .65f, ForensicUIStyle.Accent, ForensicUIStyle.PanelBackground);
        pointer.Register("access-confirm", actionRow, Vector2.zero, () => { if (register != null && register.IsAuthorized) Hide(); else ConfirmCheckIn(); });
        var close = Label("Close register", 26, "[B] Cancel");
        var closeRow = ActionButton(actions, "Cancel", close, .68f, 1, ForensicUIStyle.SelectedFill, ForensicUIStyle.TextPrimary);
        pointer.Register("access-close", closeRow, Vector2.zero, Hide);
        layout.RegisterControls(pointer, "access-scroll-"); panel.SetActive(false);
            }
    }
    private RectTransform ActionButton(RectTransform parent, string name, TMP_Text label, float left, float right, Color fill, Color ink)
    {
        var image = ForensicUIStyle.Rect(parent, name, fill);
        var rect = image.rectTransform; rect.anchorMin = new Vector2(left, 0); rect.anchorMax = new Vector2(right, 1);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        label.transform.SetParent(rect, false); label.color = ink; label.alignment = TextAlignmentOptions.Center; label.fontStyle = FontStyles.Bold;
        label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(12, 8); label.rectTransform.offsetMax = new Vector2(-12, -8);
        return rect;
    }
    private TMP_Text AddRow(string text, float size = 26, float minimum = 48, Color? color = null)
    {
        var label = Label("Access record", size, text);
        if (color.HasValue) label.color = color.Value;
        rows.Add(layout.AddText(label, layout.Body, minimum, 10).gameObject);
        return label;
    }
    private void Detail(string key, string value)
    {
        // Equal padding on a two-column row keeps wrapped values inside its measured bounds.
        var valueLabel = Label("Register " + key, 27, value);
        var row = layout.AddText(valueLabel, layout.Body, 44, 8);
        valueLabel.margin = new Vector4(150, 2, 2, 2);
        var keyLabel = Label(key + " label", 23, key); keyLabel.color = ForensicUIStyle.TextSecondary;
        keyLabel.transform.SetParent(row, false);
        keyLabel.rectTransform.anchorMin = new Vector2(0, 0); keyLabel.rectTransform.anchorMax = new Vector2(0, 1);
        keyLabel.rectTransform.offsetMin = new Vector2(22, 8); keyLabel.rectTransform.offsetMax = new Vector2(160, -8);
        rows.Add(row.gameObject);
    }
    public void Refresh()
    {
        if (register == null || !isActiveAndEnabled || panel == null) return;
        RenderContents(register.StudentName, SceneTurnover.PlayerRole.ToString(), register.CurrentPurpose,
            register.IsAuthorized, register.Visits, register.Transitions, register.LastPersistenceError);
    }
    // Pure presentation entry point, also usable by the isolated editor preview.
    public void RenderContents(string investigator, string role, string purpose, bool authorized,
        IReadOnlyList<SceneAccessRegister.Visit> visits, IReadOnlyList<SceneAccessRegister.Transition> transitions, string saveError)
    {
        foreach (var row in rows) { row.SetActive(false); Destroy(row); } rows.Clear();
        statusLabel = AddRow(!authorized ? "Review your details, then confirm check-in."
            : visits.Count == 0 ? "Checked in. Cross the entrance to record your time in." : "Checked in. Your scene visits are recorded below.",
            25, 56, authorized ? ForensicUIStyle.Success : ForensicUIStyle.Accent);
        Detail("Investigator", investigator);
        Detail("Role", role == "IOC" ? "Investigating officer (IOC)" : role == "SOCO" ? "Scene investigator (SOCO)" : role);
        Detail("Purpose", purpose);
        AddRow("SCENE VISITS", 22, 38, ForensicUIStyle.TextSecondary);
        if (visits.Count == 0) AddRow("No visits yet. Entry and exit times are recorded when you cross the entrance.", 25, 56);
        foreach (var visit in visits)
        {
            string status = visit.presentAtScenarioEnd ? "On scene at closeout" : visit.simulatedRepositionMs >= 0 ? "Handover; exit was not recorded"
                : visit.timeOutMs >= 0 ? "Signed out" : "On scene";
            AddRow("Visit " + visit.number + "  |  " + visit.roleAtEntry + "\n" + visit.purpose
                + "\nIN  " + DisplayTime(visit.timeInMs) + "     OUT  " + DisplayTime(visit.timeOutMs) + "\n" + status);
        }
        if (transitions.Count > 0) AddRow("SCENE UPDATES", 22, 38, ForensicUIStyle.TextSecondary);
        foreach (var transition in transitions) AddRow(DisplayTime(transition.elapsedMs) + "  |  " + transition.description, 24);
        if (!string.IsNullOrEmpty(saveError)) AddRow("Save pending. Reopen this register to retry.", 24, 48, ForensicUIStyle.Warning);
        action.text = authorized ? "[A] Close register" : "[A] Confirm check-in";
        actionRow.parent.Find("Cancel").GetComponentInChildren<TMP_Text>().text = authorized ? "[B] Close" : "[B] Cancel";
        layout.Refresh();
    }
    private static string DisplayTime(long milliseconds) => milliseconds < 0 ? "--:--:--"
        : System.TimeSpan.FromMilliseconds(milliseconds).ToString(@"hh\:mm\:ss");
    private Material StationMaterial(string name, Color color)
    {
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.name = name; material.SetColor("_BaseColor", color); materials.Add(material); return material;
    }
    private Transform TablePart(string name, Vector3 position, Vector3 size, Material material, PrimitiveType shape = PrimitiveType.Cube)
    {
        var part = EditableSceneVisuals.Primitive(shape); part.name = name; part.transform.SetParent(transform, false);
        part.transform.localPosition = position; part.transform.localScale = size;
        part.GetComponent<Renderer>().sharedMaterial = material; EditableSceneVisuals.Remove(part.GetComponent<Collider>());
        return part.transform;
    }
    [EditableSceneBuilder]
    private void BuildLoggingTable()
    {
        using (EditableSceneVisuals.Begin(this,"BuildLoggingTable"))
        {
        var wood = StationMaterial("Logging table wood", new Color(.32f, .22f, .13f));
        var metal = StationMaterial("Logging table legs", new Color(.22f, .24f, .25f));
        var paper = StationMaterial("Register paper", new Color(.96f, .95f, .90f));
        var ink = StationMaterial("Register ruled lines", new Color(.46f, .51f, .56f));
        var pen = StationMaterial("Register blue pen", new Color(.04f, .12f, .38f));
        TablePart("Logging table top", new Vector3(0, .72f, 0), new Vector3(.8f, .04f, .55f), wood);
        foreach (float x in new[] { -.34f, .34f }) foreach (float z in new[] { -.215f, .215f })
            TablePart("Logging table leg", new Vector3(x, .35f, z), new Vector3(.035f, .70f, .035f), metal);
        TablePart("Register paper stack", new Vector3(-.13f, .743f, 0), new Vector3(.28f, .006f, .4f), paper);
        for (int row = 0; row < 6; row++)
            TablePart("Register writing line", new Vector3(-.13f, .7465f, -.015f + row * .027f), new Vector3(.24f, .0005f, .001f), ink);
        var body = TablePart("Register pen", new Vector3(.15f, .75f, .015f), new Vector3(.008f, .075f, .008f), pen, PrimitiveType.Cylinder);
        body.localRotation = Quaternion.Euler(90, 0, -15);
        var cap = TablePart("Register pen cap", body.localPosition + body.localRotation * Vector3.up * .057f, new Vector3(.010f, .019f, .010f), metal, PrimitiveType.Cylinder);
        cap.localRotation = body.localRotation;
        var page = EditableSceneVisuals.Create("Register paper heading", typeof(RectTransform), typeof(Canvas)); page.transform.SetParent(transform, false);
        page.transform.localPosition = new Vector3(-.13f, .747f, 0); page.transform.localRotation = Quaternion.Euler(90, 0, 0);
        page.transform.localScale = Vector3.one * .0007f;
        ((RectTransform)page.transform).sizeDelta = new Vector2(370, 540); page.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        var label = EditableSceneVisuals.Create("Register instruction", typeof(RectTransform), typeof(Text)); label.transform.SetParent(page.transform, false);
        ((RectTransform)label.transform).sizeDelta = new Vector2(350, 510); stationLabel = label.GetComponent<Text>();
        stationLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); stationLabel.fontSize = 22;
        stationLabel.alignment = TextAnchor.UpperCenter; stationLabel.color = new Color(.08f, .10f, .13f); stationLabel.raycastTarget = false;
            }
    }
}
