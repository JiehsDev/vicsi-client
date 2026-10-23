// Assets/_Project/Scripts/UI/InsightsUI.cs
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Minimal, READ-ONLY panel listing currently-unlocked InsightDefinitions - display
/// name, description, and suggested action (if authored) only. Never shows a locked
/// insight, a requirement id, a finding id, or any hidden unlock condition - see this
/// feature's own explicit "do not expose locked insights" rule.
///
/// Deliberately a brand-new, fully self-contained panel rather than an addition to
/// EvidenceBoardController's existing sidecar - the safer of the two options this
/// feature's own spec explicitly offers, chosen specifically to avoid touching the
/// board's already-stabilized code at all. Wired from the Utility Menu's previously-
/// dormant "Item Logs" entry (0 listeners before this), the same handoff shape
/// UtilityMenuController's "Notes" entry already uses for the Deduction Board and
/// "Album" uses for PhotoAlbumUI - PlayerUIGate/LocomotionSuspender held for as long as
/// the panel is up, deferential (Show() refuses while PlayerUIGate.IsBlocked, never a
/// forced interrupt), same convention every panel in this project already follows.
///
/// No selection/equip logic at all - purely a view. Right thumbstick is not read;
/// right B closes. If the list is empty (no insights unlocked yet), shows a plain
/// "No investigative insights yet." message rather than an empty panel.
/// </summary>
public class InsightsUI : MonoBehaviour
{
    public static InsightsUI Instance { get; private set; }

    [Header("Authored UI (optional - leave blank to auto-generate at runtime)")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private RectTransform listContainer;
    [SerializeField] private InsightCardView cardTemplate;
    [SerializeField] private TMP_Text emptyLabel;

    /// <summary>True while the panel is on screen.</summary>
    public bool IsShowing { get; private set; }

    private readonly List<InsightCardView> spawnedCards = new();
    private InputAction closeAction;

    private void Awake()
    {
        Instance = this;

        closeAction = new InputAction("InsightsUI_Close", InputActionType.Button, "<XRController>{RightHand}/secondaryButton");

        if (panelRoot == null || GetComponent<EditableSceneVisuals>() != null)
        {
            BuildRuntimeUI();
        }

        if (cardTemplate != null)
        {
            cardTemplate.gameObject.SetActive(false);
        }

        panelRoot.SetActive(false);
    }

    private void OnEnable()
    {
        closeAction.Enable();
        InputSystem.onDeviceChange += OnDeviceChange;
    }

    private void OnDisable()
    {
        InputSystem.onDeviceChange -= OnDeviceChange;
        closeAction.Disable();

        // Defensive: never leave locomotion suspended or the UI gate held if this
        // component is torn down while the panel is open - same discipline every
        // other panel in this project follows in OnDisable.
        if (IsShowing)
        {
            ReleaseHolds();
            IsShowing = false;
        }
    }

    // Same Quest-hardware device-reconnect fix every other panel's loose InputAction already applies.
    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (change != InputDeviceChange.Added && change != InputDeviceChange.Reconnected)
        {
            return;
        }

        closeAction.Disable();
        closeAction.Enable();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        closeAction?.Dispose();
    }

    private void Update()
    {
        if (!IsShowing)
        {
            return;
        }

        if (closeAction.WasPressedThisFrame())
        {
            Hide();
        }
    }

    public void Toggle()
    {
        if (panelRoot.activeSelf)
        {
            Hide();
        }
        else
        {
            Show();
        }
    }

    public void Show()
    {
        // Defers to whatever else already owns the screen right now - same contract as
        // every other secondary panel in this project (PhotoAlbumUI, Evidence
        // Inventory, the Deduction Board).
        if (!panelRoot.activeSelf && PlayerUIGate.IsBlocked)
        {
            return;
        }

        if (!panelRoot.activeSelf)
        {
            LocomotionSuspender.Suspend();
            PlayerUIGate.Enter(this);
            IsShowing = true;
        }

        HudFollowCamera.ConfigureShared(panelRoot, this, 300);
        panelRoot.SetActive(true);
        RefreshList();
        InteractionFeedback.Confirm(panelRoot.transform);
    }

    public void Hide()
    {
        panelRoot.SetActive(false);

        if (IsShowing)
        {
            ReleaseHolds();
            IsShowing = false;
        }
    }

    private void ReleaseHolds()
    {
        LocomotionSuspender.Resume();
        PlayerUIGate.Exit(this);
    }

    private void RefreshList()
    {
        foreach (var card in spawnedCards)
        {
            if (card != null)
            {
                // Detach before Destroy - Destroy() is deferred to end-of-frame, and a
                // stale card still parented under listContainer would be visible for
                // one more frame otherwise. Same discipline RecorderReportUI.ClearChips
                // already documents for the identical reason.
                card.RectTransform.SetParent(null, false);
                Destroy(card.gameObject);
            }
        }
        spawnedCards.Clear();

        if (InsightStateManager.Instance == null || cardTemplate == null || listContainer == null)
        {
            UpdateEmptyLabel();
            return;
        }

        foreach (var insight in InsightStateManager.Instance.GetUnlockedInsights())
        {
            if (insight == null)
            {
                continue;
            }

            var clone = Instantiate(cardTemplate, listContainer);
            clone.gameObject.SetActive(true);
            clone.Bind(insight.displayName, insight.description, insight.suggestedAction);
            spawnedCards.Add(clone);
        }

        UpdateEmptyLabel();
    }

    private void UpdateEmptyLabel()
    {
        if (emptyLabel == null)
        {
            return;
        }

        emptyLabel.gameObject.SetActive(spawnedCards.Count == 0);
    }

    // --- Runtime fallback UI -------------------------------------------------

    [EditableSceneBuilder]
    private void BuildRuntimeUI()
    {
        using (EditableSceneVisuals.Begin(this,"BuildRuntimeUI"))
        {
        var canvasGo = EditableSceneVisuals.Create("InsightsCanvas", typeof(RectTransform), typeof(Canvas), typeof(HudFollowCamera));
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = short.MaxValue - 4;

        var canvasRect = canvasGo.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(900f, 700f);
        canvasGo.transform.localScale = Vector3.one * 0.0007f;

        var follow = canvasGo.GetComponent<HudFollowCamera>();
        follow.Distance = 0.75f;

        var background = CreateImage(canvasRect, "Background", new Color(0.06f, 0.06f, 0.08f, 0.96f));
        Stretch(background.rectTransform);

        titleLabel = CreateLabel(canvasRect, "TitleLabel", new Vector2(0f, 310f), new Vector2(820f, 60f), 26f);
        titleLabel.text = "Investigative Insights";

        var scrollBorder = CreateImage(canvasRect, "ListBorder", new Color(1f, 1f, 1f, 0.15f));
        scrollBorder.rectTransform.anchorMin = scrollBorder.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        scrollBorder.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        scrollBorder.rectTransform.sizeDelta = new Vector2(820f, 560f);
        scrollBorder.rectTransform.anchoredPosition = new Vector2(0f, -20f);

        var listGo = EditableSceneVisuals.Create("List", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        listGo.transform.SetParent(scrollBorder.rectTransform, false);
        listContainer = listGo.GetComponent<RectTransform>();
        listContainer.anchorMin = new Vector2(0f, 1f);
        listContainer.anchorMax = new Vector2(1f, 1f);
        listContainer.pivot = new Vector2(0.5f, 1f);
        listContainer.anchoredPosition = Vector2.zero;
        listContainer.sizeDelta = new Vector2(0f, 0f);

        var listLayout = listGo.GetComponent<VerticalLayoutGroup>();
        listLayout.spacing = 10f;
        listLayout.padding = new RectOffset(14, 14, 14, 14);
        listLayout.childForceExpandWidth = true;
        listLayout.childForceExpandHeight = false;
        listLayout.childControlWidth = true;
        listLayout.childControlHeight = false;

        var fitter = listGo.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Card template: title + description + optional suggested-action line, all
        // generic greybox styling - no final art needed for a data/state-first pass.
        var cardGo = EditableSceneVisuals.Create("CardTemplate", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        cardGo.transform.SetParent(listContainer, false);
        var cardRect = cardGo.GetComponent<RectTransform>();
        cardGo.GetComponent<Image>().color = new Color(0.14f, 0.14f, 0.17f, 1f);
        var cardLayout = cardGo.GetComponent<VerticalLayoutGroup>();
        cardLayout.spacing = 4f;
        cardLayout.padding = new RectOffset(12, 12, 10, 10);
        cardLayout.childForceExpandWidth = true;
        cardLayout.childForceExpandHeight = false;
        cardLayout.childControlWidth = true;
        cardLayout.childControlHeight = false;
        var cardFitter = cardGo.GetComponent<ContentSizeFitter>();
        cardFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var titleGo = EditableSceneVisuals.Create("Title", typeof(RectTransform));
        titleGo.transform.SetParent(cardRect, false);
        var titleText = EditableSceneVisuals.Component<TextMeshProUGUI>(titleGo);
        titleText.fontSize = 22f;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = Color.white;
        titleText.enableWordWrapping = true;
        var titleLayoutEl = EditableSceneVisuals.Component<LayoutElement>(titleGo);
        titleLayoutEl.preferredHeight = 30f;

        var descGo = EditableSceneVisuals.Create("Description", typeof(RectTransform));
        descGo.transform.SetParent(cardRect, false);
        var descText = EditableSceneVisuals.Component<TextMeshProUGUI>(descGo);
        descText.fontSize = 17f;
        descText.color = new Color(0.85f, 0.85f, 0.85f, 1f);
        descText.enableWordWrapping = true;
        var descLayoutEl = EditableSceneVisuals.Component<LayoutElement>(descGo);
        descLayoutEl.preferredHeight = 60f;

        var actionGo = EditableSceneVisuals.Create("SuggestedAction", typeof(RectTransform));
        actionGo.transform.SetParent(cardRect, false);
        var actionText = EditableSceneVisuals.Component<TextMeshProUGUI>(actionGo);
        actionText.fontSize = 16f;
        actionText.fontStyle = FontStyles.Italic;
        actionText.color = new Color(0.7f, 0.85f, 0.95f, 1f);
        actionText.enableWordWrapping = true;
        var actionLayoutEl = EditableSceneVisuals.Component<LayoutElement>(actionGo);
        actionLayoutEl.preferredHeight = 26f;

        cardTemplate = EditableSceneVisuals.Component<InsightCardView>(cardGo);
        cardTemplate.AssignRuntimeVisuals(titleText, descText, actionText);

        emptyLabel = CreateLabel(canvasRect, "EmptyLabel", new Vector2(0f, -20f), new Vector2(700f, 60f), 20f);
        emptyLabel.text = "No investigative insights yet.";
        emptyLabel.color = new Color(0.7f, 0.7f, 0.75f, 1f);

        var hintLabel = CreateLabel(canvasRect, "HintLabel", new Vector2(0f, -320f), new Vector2(820f, 30f), 16f);
        hintLabel.text = "B: close";
        hintLabel.color = new Color(0.7f, 0.7f, 0.75f, 1f);

        panelRoot = canvasGo;
            }
    }

    private static Image CreateImage(RectTransform parent, string name, Color color)
    {
        var go = EditableSceneVisuals.Create(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static TMP_Text CreateLabel(RectTransform parent, string name, Vector2 anchoredPosition, Vector2 size, float fontSize)
    {
        var go = EditableSceneVisuals.Create(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var text = EditableSceneVisuals.Component<TextMeshProUGUI>(go);
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = true;
        text.color = Color.white;

        var rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
