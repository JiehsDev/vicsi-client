using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Read-only projection of authored, available witness accounts. Owns no discovery state.</summary>
public class WitnessAccountsUI : MonoBehaviour
{
    public readonly struct Account
    {
        public readonly string Person, CatalogId, Text;
        public Account(string person, string id) { Person = person; CatalogId = id; Text = GameDialogue.Text(id); }
    }
    // Catalog references only: testimony remains in its existing authored source.
    private static readonly (string person, string id, string node)[] accounts =
    {
        ("Paul", "statement-paul-evening", "statement_brother_drinking"),
        ("Paul", "statement-paul-alibi", "statement_paul_timeline"),
        ("Paul", "statement-paul-knife", "statement_paul_knife_contact"),
        ("Peter", "statement-peter-timeline", "statement_peter_timeline"),
        ("Peter", "statement-peter-appearance", null),
        ("Peter", "statement-peter-money", null),
        ("Vendor", "statement-vendor-peter-leaving", "statement_vendor_observation"),
        ("Vendor", "statement-vendor-discovery", "statement_vendor_observation"),
        ("Vendor", "statement-vendor-limitation", "statement_vendor_observation")
    };
    private readonly Dictionary<string, EvidenceNode> nodes = new();
    private GameObject panel;
    private TMP_Text body;
    private ScrollRect scroll;
    private InputAction close, scrollAction;
    private VendorFollowupInterview vendor;
    private DialogueHistoryManager history;
    private bool dirty;
    public bool IsShowing => panel != null && panel.activeSelf;
    public string ReviewText => body != null ? body.text : string.Empty;

    private void Awake()
    {
        close = new InputAction("WitnessAccounts_Close", InputActionType.Button, "<XRController>{RightHand}/secondaryButton");
        scrollAction = new InputAction("WitnessAccounts_Scroll", InputActionType.Value, "<XRController>{RightHand}/thumbstick", expectedControlType: "Vector2");
        BuildPanel();
    }
    private void OnEnable()
    {
        close.Enable(); scrollAction.Enable(); InputSystem.onDeviceChange += DeviceChanged;
        SceneTurnover.OnPhaseChanged += PhaseChanged;
        EvidenceStateManager.OnFingerprintLabStatusChanged += FingerprintChanged;
    }
    private void Start() => SubscribeSources();
    private void OnDisable()
    {
        Hide(); close.Disable(); scrollAction.Disable(); InputSystem.onDeviceChange -= DeviceChanged;
        SceneTurnover.OnPhaseChanged -= PhaseChanged;
        EvidenceStateManager.OnFingerprintLabStatusChanged -= FingerprintChanged;
        if (vendor != null) vendor.OnStatementDiscovered -= MarkDirty;
        if (history != null) { history.OnEntryAdded -= EntryAdded; history.OnCleared -= MarkDirty; }
        vendor = null; history = null;
    }
    private void OnDestroy() { close?.Dispose(); scrollAction?.Dispose(); }
    private void DeviceChanged(InputDevice device, InputDeviceChange change)
    {
        if (change != InputDeviceChange.Added && change != InputDeviceChange.Reconnected) return;
        close.Disable(); close.Enable(); scrollAction.Disable(); scrollAction.Enable();
    }
    private void SubscribeSources()
    {
        if (nodes.Count == 0)
            foreach (var node in FindObjectsByType<EvidenceNode>(FindObjectsInactive.Include)) nodes.TryAdd(node.nodeId, node);
        if (vendor != VendorFollowupInterview.Instance)
        {
            if (vendor != null) vendor.OnStatementDiscovered -= MarkDirty;
            vendor = VendorFollowupInterview.Instance;
            if (vendor != null) vendor.OnStatementDiscovered += MarkDirty;
        }
        if (history != DialogueHistoryManager.Instance)
        {
            if (history != null) { history.OnEntryAdded -= EntryAdded; history.OnCleared -= MarkDirty; }
            history = DialogueHistoryManager.Instance;
            if (history != null) { history.OnEntryAdded += EntryAdded; history.OnCleared += MarkDirty; }
        }
    }
    private void MarkDirty() => dirty = true;
    private void EntryAdded(DialogueHistoryEntry entry) => MarkDirty();
    private void PhaseChanged(ScenarioPhase phase) => MarkDirty();
    private void FingerprintChanged(string id, FingerprintLabStatus status) => MarkDirty();

    public List<Account> GetKnownAccounts()
    {
        SubscribeSources();
        var result = new List<Account>();
        foreach (var item in accounts)
        {
            bool available = item.node != null && nodes.TryGetValue(item.node, out var node) && node != null && node.IsStatementAvailable;
            bool delivered = history != null && history.Entries.Any(entry => entry.speaker == item.person && entry.text == GameDialogue.Text(item.id));
            // Prepared catalog-only lines stay hidden until actually delivered; knife keeps its board prerequisite.
            if (item.id == "statement-paul-knife" && !available) continue;
            if ((SceneTurnover.Form1Received && available) || delivered) result.Add(new Account(item.person, item.id));
        }
        if (vendor != null && vendor.IsDiscovered) result.Add(new Account("Vendor", "vendor-paul-3am-summary"));
        return result;
    }
    public void Toggle() { if (IsShowing) Hide(); else Show(); }
    public void Show()
    {
        if (IsShowing || PlayerUIGate.IsBlocked || (STCSNotificationUI.Instance != null && STCSNotificationUI.Instance.IsShowing)) return;
        Rebuild(); WorldPanelPlacement.PlaceInFront(panel.transform, owner: this); panel.SetActive(true);
        PlayerUIGate.Enter(this); ToolInputLock.Enter(this); LocomotionSuspender.Suspend();
        scroll.verticalNormalizedPosition = 1f;
    }
    public void Hide()
    {
        if (!IsShowing) return;
        panel.SetActive(false); PlayerUIGate.Exit(this); ToolInputLock.Exit(this); LocomotionSuspender.Resume();
    }
    private void Update()
    {
        if (!IsShowing) return;
        if (close.WasPressedThisFrame()) { Hide(); return; }
        if (dirty) Rebuild();
        float y = scrollAction.ReadValue<Vector2>().y;
        float overflow = body.rectTransform.rect.height - scroll.viewport.rect.height;
        if (Mathf.Abs(y) > .2f && overflow > 0f)
            scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition + y * 700f * Time.unscaledDeltaTime / overflow);
    }
    private void Rebuild()
    {
        float previousScroll = scroll.verticalNormalizedPosition;
        var text = new StringBuilder();
        foreach (var group in GetKnownAccounts().GroupBy(account => account.Person))
        {
            text.AppendLine("<b><color=#73D4FF>" + group.Key.ToUpperInvariant() + "</color></b>");
            foreach (var account in group) text.AppendLine(account.Text + "\n");
        }
        body.text = text.Length > 0 ? text.ToString() : "No witness accounts obtained yet.";
        Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate(body.rectTransform);
        scroll.verticalNormalizedPosition = previousScroll; dirty = false;
    }
    [EditableSceneBuilder]
    private void BuildPanel()
    {
        using (EditableSceneVisuals.Begin(this,"BuildPanel"))
        {
        panel = EditableSceneVisuals.Create("WitnessAccountsPanel", typeof(RectTransform), typeof(Canvas));
        panel.transform.SetParent(transform, false); panel.transform.localScale = Vector3.one * .0008f;
        var rect = (RectTransform)panel.transform; rect.sizeDelta = new Vector2(1000, 900);
        var canvas = panel.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true; canvas.sortingOrder = short.MaxValue - 4;
        var bg = Image(rect, "Background", new Color(.03f, .05f, .06f, .95f)); Stretch(bg.rectTransform, 0, 0, 0, 0);
        var title = Label(rect, "Title", 34); title.text = "WITNESS ACCOUNTS"; title.fontStyle = FontStyles.Bold;
        title.alignment = TextAlignmentOptions.Center; title.rectTransform.sizeDelta = new Vector2(940, 60);
        title.rectTransform.anchoredPosition = new Vector2(0, 400);
        var viewport = Image(rect, "Viewport", new Color(1, 1, 1, .025f)).rectTransform;
        Stretch(viewport, 28, 90, 28, 80); EditableSceneVisuals.Component<RectMask2D>(viewport.gameObject);
        body = Label(viewport, "Accounts", 30); body.lineSpacing = 8;
        body.rectTransform.anchorMin = new Vector2(0, 1); body.rectTransform.anchorMax = Vector2.one;
        body.rectTransform.pivot = new Vector2(.5f, 1); body.rectTransform.sizeDelta = new Vector2(-24, 0);
        body.rectTransform.anchoredPosition = Vector2.zero;
        EditableSceneVisuals.Component<ContentSizeFitter>(body.gameObject).verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll = EditableSceneVisuals.Component<ScrollRect>(panel); scroll.viewport = viewport; scroll.content = body.rectTransform;
        scroll.horizontal = false; scroll.vertical = true; scroll.inertia = false; scroll.movementType = ScrollRect.MovementType.Clamped;
        var footer = Label(rect, "Controls", 26); footer.text = "Right stick: scroll    [B] Close";
        footer.alignment = TextAlignmentOptions.Center; footer.rectTransform.sizeDelta = new Vector2(940, 50);
        footer.rectTransform.anchoredPosition = new Vector2(0, -410); panel.SetActive(false);
            }
    }
    private static TMP_Text Label(Transform parent, string name, float size)
    {
        var go = EditableSceneVisuals.Create(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
        var label = go.GetComponent<TextMeshProUGUI>(); label.fontSize = size; label.color = Color.white;
        label.raycastTarget = false; label.alignment = TextAlignmentOptions.TopLeft; return label;
    }
    private static Image Image(Transform parent, string name, Color color)
    {
        var go = EditableSceneVisuals.Create(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = false; return image;
    }
    private static void Stretch(RectTransform rect, float left, float top, float right, float bottom)
    { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = new Vector2(left, bottom); rect.offsetMax = new Vector2(-right, -top); }
}
