// Assets/_Project/Scripts/Sketch/CrimeSceneSketchUI.cs
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// "Crime Scene Sketch" board: a small drafting editor where the student drags discovered
/// evidence elements onto the authored floor plan. Recorded distances attach automatically
/// to correctly placed evidence, then the student submits once. A view/controller over
/// CrimeSceneSketchManager, which owns all state - closing and reopening loses nothing.
///
/// Panel conventions match EvidenceProcedureUI / DialogueHistoryUI: PlayerUIGate +
/// LocomotionSuspender + ToolInputLock while open (tools in hand go inert, nothing is
/// unequipped), raw InputActions only - NOT the Meta ISDK ray / uGUI pointer pipeline,
/// which this project found never reaches its runtime world-space canvases (see
/// RecorderReportUI). "Dragging" is a thumbstick-driven cursor: right stick moves it,
/// A picks up / drops / presses, B cancels a drag or closes the board.
///
/// Unlike the head-locked review panels, the board is placed once in front of the player
/// when it opens and then stays put (locomotion is suspended while it is open), so the
/// student can look around a large sheet without it swimming with their head.
///
/// EVIDENCE-LEAK RULE: anchors have no visuals; every wrong evidence drop - another
/// item's anchor or open board - gets the same response; measurement slots appear only
/// after their element is placed and lines only after the distance is filed.
/// </summary>
public class CrimeSceneSketchUI : MonoBehaviour
{
    public static CrimeSceneSketchUI Instance { get; private set; }

    private enum DragKind
    {
        None,
        Evidence,
        Measurement
    }

    private class PaletteCard
    {
        public DragKind kind;
        public string id;
        public RectTransform rect;
        public Image background;
        public Color normalColor;
    }

    private class ReturningGhost
    {
        public RectTransform ghost;
        public Vector2 from;
        public Vector2 to;
        public float t;
        public string sourceId;
    }

    [Header("Layout")]
    [SerializeField] private Vector2 panelSize = new Vector2(1640f, 1120f);
    [SerializeField] private float panelScale = 0.00058f;
    [SerializeField] private float placeDistance = 0.85f;
    [SerializeField] private float placeHeightOffset = -0.08f;

    [Header("Input")]
    [SerializeField] private float cursorSpeed = 950f;
    [SerializeField, Range(0f, 0.9f)] private float stickDeadzone = 0.15f;
    [SerializeField, Min(0.05f)] private float returnDuration = 0.28f;
    [SerializeField, Min(0.1f)] private float refreshInterval = 0.5f;

    [Header("Colours")]
    [SerializeField] private Color panelColor = new Color(0.06f, 0.07f, 0.09f, 0.97f);
    [SerializeField] private Color elementCardColor = new Color(0.16f, 0.19f, 0.24f, 1f);
    [SerializeField] private Color measurementCardColor = new Color(0.93f, 0.90f, 0.78f, 1f);
    [SerializeField] private Color hoverColor = new Color(0.98f, 0.86f, 0.35f, 1f);

    [Tooltip("Wrong drops of the same evidence card after which its placement area stays highlighted during its return and next drag (logged as SketchPlacementHintShown). 0 = always show the area.")]
    [SerializeField, Min(0)] private int hintAfterWrongDrops = 2;
    [SerializeField] private Color hintColor = new Color(0.98f, 0.86f, 0.35f, 0.3f);
    [SerializeField] private Color slotColor = new Color(0.15f, 0.45f, 0.85f, 0.85f);
    [SerializeField] private Color lineColor = new Color(0.12f, 0.16f, 0.25f, 0.95f);

    public bool IsShowing { get; private set; }
    public bool IsDragging => dragKind != DragKind.None;
    public bool IsReturning => returning.Count > 0;
    public bool IsConfirmPending { get; private set; }
    public bool IsReadOnly => Manager != null && Manager.IsSubmitted;
    public SketchPlacementResult? LastDropResult { get; private set; }
    public string LastMessage => messageLabel != null ? messageLabel.text : string.Empty;
    public Vector2 CursorPosition => cursorPos;
    public Vector2? DraggedCardPosition => dragGhost != null ? dragGhost.anchoredPosition : (Vector2?)null;

    private CrimeSceneSketchManager Manager => CrimeSceneSketchManager.Instance;

    private GameObject panelRoot;
    private RectTransform canvasRect;
    private RectTransform boardHolder;
    private RectTransform elementArea;
    private RectTransform measurementArea;
    private RectTransform legendArea;
    private RectTransform cursor;
    private RectTransform dragLayer;
    private RectTransform submitButton;
    private Image submitImage;
    private TMP_Text submitLabel;
    private TMP_Text stateLabel;
    private TMP_Text elementsHeader;
    private TMP_Text measurementsHeader;
    private TMP_Text messageLabel;
    private TMP_Text hintLabel;
    private GameObject confirmOverlay;

    private CrimeSceneSketchTemplate template;
    private RectTransform runtimeLayer;
    private RectTransform linesLayer;
    private RectTransform slotsLayer;
    private RectTransform symbolsLayer;
    private RectTransform hintLayer;
    private RectTransform hintRing;
    private string hintedElementName;
    private static Sprite softDisc;
    private readonly Dictionary<string, RectTransform> placedSymbols = new();
    private readonly Dictionary<string, Image> slotOutlines = new();

    private readonly List<PaletteCard> cards = new();
    private readonly List<ReturningGhost> returning = new();

    private InputAction stickAction;
    private InputAction selectAction;
    private InputAction backAction;
    private bool waitForSelectRelease;

    private Vector2 cursorPos;
    private DragKind dragKind;
    private string dragId;
    private RectTransform dragGhost;
    private string pulseEvidenceId;
    private float pulseStart;
    private string signature;
    private bool dirty = true;
    private float nextRefresh;
    private float messageClearAt;

    private void Awake()
    {
        Instance = this;
        stickAction = new InputAction("CrimeSceneSketch_Stick", InputActionType.Value, "<XRController>{RightHand}/thumbstick", expectedControlType: "Vector2");
        selectAction = PlayerUIGate.CreateConfirmAction(this, "CrimeSceneSketch_Select", () => IsShowing, 300);
        backAction = new InputAction("CrimeSceneSketch_Back", InputActionType.Button, "<XRController>{RightHand}/secondaryButton");
        BuildRuntimeUI();
        AdoptTemplate();
        panelRoot.SetActive(false);
    }

    private void OnEnable()
    {
        stickAction.Enable();
        selectAction.Enable();
        backAction.Enable();
        InputSystem.onDeviceChange += OnDeviceChange;
        CrimeSceneSketchManager.OnSketchChanged += MarkDirty;
        EvidenceStateManager.OnEvidenceStatusChanged += HandleStatusChanged;
        MeasurementStateManager.OnMeasurementProgress += HandleMeasurementProgress;
    }

    private void OnDisable()
    {
        InputSystem.onDeviceChange -= OnDeviceChange;
        CrimeSceneSketchManager.OnSketchChanged -= MarkDirty;
        EvidenceStateManager.OnEvidenceStatusChanged -= HandleStatusChanged;
        MeasurementStateManager.OnMeasurementProgress -= HandleMeasurementProgress;
        stickAction.Disable();
        selectAction.Disable();
        backAction.Disable();
        if (IsShowing)
        {
            ReleaseHolds();
            IsShowing = false;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
        stickAction?.Dispose();
        selectAction?.Dispose();
        backAction?.Dispose();
    }

    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (change != InputDeviceChange.Added && change != InputDeviceChange.Reconnected)
        {
            return;
        }
        foreach (var action in new[] { stickAction, selectAction, backAction })
        {
            action.Disable();
            action.Enable();
        }
    }

    private void MarkDirty() => dirty = true;
    private void HandleStatusChanged(string evidenceId, EvidenceStatus status) => dirty = true;
    private void HandleMeasurementProgress(string requirementId) => dirty = true;

    // --- Open / close --------------------------------------------------------------

    public void Toggle()
    {
        if (IsShowing) Hide();
        else Show();
    }

    public void Show()
    {
        if (SceneTurnover.IsInvestigationBlocked || IsShowing || PlayerUIGate.IsBlocked || Manager == null)
        {
            return;
        }
        if (template == null)
        {
            AdoptTemplate();
        }
        LocomotionSuspender.Suspend();
        PlayerUIGate.Enter(this);
        ToolInputLock.Enter(this);
        IsShowing = true;
        PlaceInFrontOfPlayer();
        panelRoot.SetActive(true);
        waitForSelectRelease = true; // the press that opened the board must not also pick something up
        cursorPos = boardHolder.anchoredPosition;
        IsConfirmPending = false;
        confirmOverlay.SetActive(false);
        ShowMessage(Manager.IsSubmitted ? "This sketch has been submitted." : "Place measured evidence on the floor plan. Recorded measurements appear automatically.", 0f);
        Manager.NotifyOpened();
        dirty = true;
        Rebuild();
        InteractionFeedback.Confirm(panelRoot.transform);
    }

    public void Hide()
    {
        if (IsDragging)
        {
            CancelDrag();
        }
        FinishAllReturns();
        HidePlacementHint();
        IsConfirmPending = false;
        if (confirmOverlay != null) confirmOverlay.SetActive(false);
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
        ToolInputLock.Exit(this);
    }

    private void PlaceInFrontOfPlayer()
    {
        var cam = Camera.main;
        if (cam == null)
        {
            return;
        }
        Vector3 forward = cam.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 1e-4f) forward = cam.transform.up;
        forward.y = 0f;
        forward.Normalize();
        Vector3 position = cam.transform.position + forward * placeDistance + Vector3.up * placeHeightOffset;
        panelRoot.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward, Vector3.up));
        var follow = HudFollowCamera.ConfigureShared(panelRoot, this, PlayerUIGate.InformationPriority);
        follow.Distance = placeDistance; follow.MinimumDistance = Mathf.Min(0.82f, placeDistance);
        follow.SetWorldAnchor(position, panelRoot.transform.rotation);
    }

    // --- Input ---------------------------------------------------------------------------

    private void Update()
    {
        if (!IsShowing)
        {
            return;
        }

        UpdateReturns();
        UpdatePulse();
        if (messageClearAt > 0f && Time.unscaledTime >= messageClearAt)
        {
            messageLabel.text = string.Empty;
            messageClearAt = 0f;
        }

        if (waitForSelectRelease && !selectAction.IsPressed())
        {
            waitForSelectRelease = false;
        }

        if (IsConfirmPending)
        {
            if (!waitForSelectRelease && PlayerUIGate.TryConsumeConfirm(this, selectAction)) ConfirmSubmit();
            else if (backAction.WasPressedThisFrame()) CancelSubmit();
            return;
        }

        if (backAction.WasPressedThisFrame())
        {
            if (IsDragging) CancelDrag();
            else Hide();
            return;
        }

        Vector2 stick = stickAction.ReadValue<Vector2>();
        float magnitude = stick.magnitude;
        if (magnitude > stickDeadzone)
        {
            float speed = cursorSpeed * Mathf.Pow((magnitude - stickDeadzone) / (1f - stickDeadzone), 1.6f);
            MoveCursorTo(cursorPos + stick.normalized * speed * Time.unscaledDeltaTime);
        }

        if (!waitForSelectRelease && PlayerUIGate.TryConsumeConfirm(this, selectAction))
        {
            Select();
        }

        if ((dirty || Time.unscaledTime >= nextRefresh) && !IsDragging && !IsReturning)
        {
            Rebuild();
        }
        UpdateHover();
    }

    /// <summary>Moves the cursor (canvas coordinates, origin at the centre), clamped to the panel. Tests use this too.</summary>
    public void MoveCursorTo(Vector2 canvasPosition)
    {
        Vector2 half = canvasRect.rect.size * 0.5f;
        cursorPos = new Vector2(Mathf.Clamp(canvasPosition.x, -half.x, half.x), Mathf.Clamp(canvasPosition.y, -half.y, half.y));
        cursor.anchoredPosition = cursorPos;
        if (dragGhost != null)
        {
            dragGhost.anchoredPosition = cursorPos;
        }
    }

    /// <summary>A pressed at the cursor: drop what is held, pick up a card, or press Submit.</summary>
    public void Select()
    {
        if (IsDragging)
        {
            DropAtCursor();
            return;
        }
        if (IsReturning)
        {
            return;
        }
        if (Contains(submitButton, cursorPos))
        {
            PressSubmit();
            return;
        }
        if (IsReadOnly)
        {
            return;
        }
        foreach (var card in cards)
        {
            if (card.rect != null && card.rect.gameObject.activeInHierarchy && Contains(card.rect, cursorPos))
            {
                BeginDrag(card.kind, card.id);
                return;
            }
        }
    }

    // --- Drag & drop ---------------------------------------------------------------------

    public bool BeginDragEvidence(string evidenceId) => BeginDrag(DragKind.Evidence, evidenceId);

    public bool BeginDragMeasurement(string cardId) => BeginDrag(DragKind.Measurement, cardId);

    private bool BeginDrag(DragKind kind, string id)
    {
        if (!IsShowing || IsReadOnly || IsDragging || IsReturning) return false;
        var card = FindCard(kind, id);
        if (card == null) return false;
        if (ProcedureInterlock.BlocksUnrelatedActions)
        {
            ShowMessage(ProcedureInterlock.Reason, 0f);
            return false;
        }
        if (kind == DragKind.Evidence && !Manager.CanSketchEvidence(id))
        {
            ShowMessage(GameDialogue.Text("sketch-measure-first", EvidenceDisplay.Label(id)), 0f);
            return false;
        }
        dragKind = kind;
        dragId = id;
        dragGhost = Instantiate(card.rect.gameObject, dragLayer, false).GetComponent<RectTransform>();
        dragGhost.name = "DragGhost";
        dragGhost.anchorMin = dragGhost.anchorMax = new Vector2(0.5f, 0.5f);
        dragGhost.pivot = new Vector2(0.5f, 0.5f);
        dragGhost.anchoredPosition = cursorPos;
        dragGhost.localScale = Vector3.one * 1.05f;
        SetCardAlpha(card, 0.25f);
        InteractionFeedback.Confirm(null);
        if (kind == DragKind.Evidence) ShowPlacementHint(id);
        else HidePlacementHint();
        UpdateStatusWidgets(Manager);
        return true;
    }

    /// <summary>The highlighted area for the dragged evidence card (null when not shown).</summary>
    public RectTransform PlacementHint => hintRing;

    // After hintAfterWrongDrops wrong drops of THIS card, keep its area visible through
    // the return animation and next drag. Only ever this card's own area - every card on the
    // sketch is evidence the student already found and processed, so this reveals nothing
    // about which objects are evidence; it only makes placement forgiving.
    private void ShowPlacementHint(string evidenceId, bool announce = true)
    {
        HidePlacementHint();
        if (template == null || hintLayer == null || Manager == null) return;
        if (Manager.IncorrectEvidenceAttemptsFor(evidenceId) < hintAfterWrongDrops) return;
        var element = Manager.Definition.FindElement(evidenceId);
        var anchor = element != null ? template.FindAnchor(element.correctAnchorId) : null;
        if (anchor == null) return;

        var image = NewImage(hintLayer, "PlacementHint", hintColor);
        image.sprite = SoftDisc();
        image.raycastTarget = false;
        hintRing = image.rectTransform;
        hintRing.anchorMin = hintRing.anchorMax = hintRing.pivot = new Vector2(0.5f, 0.5f);
        hintRing.sizeDelta = Vector2.one * template.EffectiveSnapRadius(anchor) * 2f;
        hintRing.anchoredPosition = template.ToBoard(anchor.transform);
        hintedElementName = EvidenceDisplay.Label(element.evidenceId, element.displayName);
        runtimeLayer.SetAsLastSibling();
        hintLayer.SetAsLastSibling();
        var border = EditableSceneVisuals.Component<SketchPlanGraphic>(NewRect(hintRing,"VisibleHintBorder").gameObject);
        border.rectTransform.sizeDelta = hintRing.sizeDelta;
        border.raycastTarget = false;
        float radius = template.EffectiveSnapRadius(anchor);
        for(int pass=0;pass<2;pass++)
        {
            for(int i=0;i<48;i++)
            {
                float a=i*Mathf.PI*2f/48,b=(i+1)*Mathf.PI*2f/48;
                border.strokes.Add(new SketchPlanStroke
                {
                    from=new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,
                    to=new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius,
                    width=pass==0?8f:4f,
                    color=pass==0?new Color(.08f,.12f,.16f,1f):new Color(1f,.72f,.12f,1f)
                });
            }
        }
        border.SetVerticesDirty();
        Manager.NoteHintShown(evidenceId);
        UpdateStatusWidgets(Manager);
        if(announce)ShowMessage($"Hint: drop the {EvidenceDisplay.Label(element.evidenceId, element.displayName)} inside the highlighted area.", 4f);
    }

    private void HidePlacementHint()
    {
        if (hintRing != null) { hintRing.gameObject.SetActive(false); Destroy(hintRing.gameObject); }
        hintRing = null;
        hintedElementName = null;
    }

    // A soft-edged disc generated once (no sprite asset needed).
    private static Sprite SoftDisc()
    {
        if (softDisc != null) return softDisc;
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float r = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                float a = d >= 1f ? 0f : (d > 0.8f ? (1f - d) / 0.2f : 0.55f + 0.45f * (d / 0.8f));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        softDisc = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return softDisc;
    }

    /// <summary>Drops what is held at a board position (board coordinates). Tests call this directly.</summary>
    public SketchPlacementResult? DropAtBoard(Vector2 boardPosition)
    {
        if (!IsDragging || template == null) return null;
        MoveCursorTo(BoardToCanvas(boardPosition));
        return DropAtCursor();
    }

    private SketchPlacementResult? DropAtCursor()
    {
        if (!IsDragging) return null;
        Vector2 boardPos = CanvasToBoard(cursorPos);
        bool onBoard = template != null && Mathf.Abs(boardPos.x) <= template.Board.rect.width * 0.5f && Mathf.Abs(boardPos.y) <= template.Board.rect.height * 0.5f;
        SketchPlacementResult? result = null;

        if (dragKind == DragKind.Evidence)
        {
            if (!onBoard)
            {
                ShowMessage("Move the element onto the sketch before placing it.");
                return null;
            }
            var element = Manager.Definition.FindElement(dragId);
            string hit = template.ResolveEvidenceDrop(element != null ? element.correctAnchorId : null, boardPos);
            result = Manager.PlaceEvidence(dragId, hit);
            if (result == SketchPlacementResult.Placed)
            {
                pulseEvidenceId = dragId;
                pulseStart = Time.unscaledTime;
                ShowMessage("Placed on the sketch.");
                InteractionFeedback.Confirm(null);
                EndDrag(false);
            }
            else
            {
                // Same words for every wrong spot - never hints where another item belongs.
                ShowMessage("That position doesn't match the scene. Move the element and try again.", 0f);
                InteractionFeedback.Blocked(null);
                ShowPlacementHint(dragId,false);
            }
        }
        else
        {
            var slot = onBoard ? template.ResolveSlotDrop(boardPos, s => Manager.IsSlotVisible(s.slotId) && !Manager.IsSlotFilled(s.slotId)) : null;
            if (slot == null)
            {
                ShowMessage(onBoard ? "Drop the distance into a measurement box on the sketch." : string.Empty);
                return null;
            }
            result = Manager.PlaceMeasurement(dragId, slot.slotId);
            if (result == SketchPlacementResult.Placed)
            {
                ShowMessage("Measurement added to the sketch.");
                InteractionFeedback.Confirm(null);
                EndDrag(false);
            }
            else
            {
                ShowMessage("That distance doesn't belong there. Move the card and try again.", 0f);
                InteractionFeedback.Blocked(null);
            }
        }
        LastDropResult = result;
        dirty = true;
        return result;
    }

    /// <summary>Cancels the drag (B, or dropped off the board): the card goes back, nothing is logged.</summary>
    public void CancelDrag()
    {
        if (IsDragging) EndDrag(true);
    }

    private void EndDrag(bool bounce)
    {
        HidePlacementHint();
        var card = FindCard(dragKind, dragId);
        if (bounce && dragGhost != null && card != null)
        {
            returning.Add(new ReturningGhost
            {
                ghost = dragGhost,
                from = dragGhost.anchoredPosition,
                to = CanvasPositionOf(card.rect),
                t = 0f,
                sourceId = card.id
            });
        }
        else
        {
            if (dragGhost != null) Destroy(dragGhost.gameObject);
            if (card != null) SetCardAlpha(card, 1f);
        }
        dragGhost = null;
        dragKind = DragKind.None;
        dragId = null;
        if(Manager!=null&&stateLabel!=null)UpdateStatusWidgets(Manager);
    }

    private void UpdateReturns()
    {
        for (int i = returning.Count - 1; i >= 0; i--)
        {
            var r = returning[i];
            r.t += Time.unscaledDeltaTime / returnDuration;
            if (r.ghost != null)
            {
                float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(r.t), 3f);
                r.ghost.anchoredPosition = Vector2.LerpUnclamped(r.from, r.to, e);
            }
            if (r.t >= 1f)
            {
                FinishReturn(r);
                returning.RemoveAt(i);
            }
        }
    }

    private void FinishAllReturns()
    {
        foreach (var r in returning) FinishReturn(r);
        returning.Clear();
    }

    private void FinishReturn(ReturningGhost r)
    {
        if (r.ghost != null) Destroy(r.ghost.gameObject);
        foreach (var c in cards)
        {
            if (c.id == r.sourceId) SetCardAlpha(c, 1f);
        }
    }

    // --- Submission ----------------------------------------------------------------------

    /// <summary>First press on an enabled Submit asks for confirmation.</summary>
    public bool PressSubmit()
    {
        if (!IsShowing || Manager == null || !Manager.IsReadyForSubmission || IsConfirmPending) return false;
        IsConfirmPending = true;
        waitForSelectRelease = true;
        confirmOverlay.SetActive(true);
        return true;
    }

    public bool ConfirmSubmit()
    {
        if (!IsConfirmPending) return false;
        IsConfirmPending = false;
        confirmOverlay.SetActive(false);
        bool ok = Manager.Submit();
        if (ok)
        {
            ShowMessage("Crime scene sketch submitted.", 0f);
            InteractionFeedback.Confirm(panelRoot.transform);
        }
        dirty = true;
        Rebuild();
        return ok;
    }

    public void CancelSubmit()
    {
        IsConfirmPending = false;
        confirmOverlay.SetActive(false);
    }

    // --- Queries for tests -----------------------------------------------------------------

    public List<string> PaletteIds(bool measurements)
    {
        var list = new List<string>();
        foreach (var c in cards)
        {
            if ((c.kind == DragKind.Measurement) == measurements) list.Add(c.id);
        }
        return list;
    }

    /// <summary>Board position of a placed evidence symbol, if shown.</summary>
    public Vector2? PlacedSymbolBoardPosition(string evidenceId)
    {
        return placedSymbols.TryGetValue(evidenceId, out var rt) && rt != null ? rt.anchoredPosition : (Vector2?)null;
    }

    public bool IsSlotShown(string slotId) => slotOutlines.ContainsKey(slotId);

    public CrimeSceneSketchTemplate Template => template;

    public Vector2 CanvasToBoard(Vector2 canvasPosition) =>
        template != null ? (Vector2)template.Board.InverseTransformPoint(canvasRect.TransformPoint(canvasPosition)) : canvasPosition;

    public Vector2 BoardToCanvas(Vector2 boardPosition) =>
        template != null ? (Vector2)canvasRect.InverseTransformPoint(template.Board.TransformPoint(boardPosition)) : boardPosition;

    // --- Rendering -------------------------------------------------------------------------

    /// <summary>Redraws the palettes, legend and placed content from the manager (no-op if nothing changed).</summary>
    public void Rebuild()
    {
        dirty = false;
        nextRefresh = Time.unscaledTime + refreshInterval;
        var manager = Manager;
        if (manager == null || manager.Definition == null)
        {
            return;
        }

        var elements = manager.GetAvailableElements();
        var measurementCards = manager.GetAvailableMeasurementCards();
        string newSignature = BuildSignature(manager, elements, measurementCards);
        UpdateStatusWidgets(manager);
        if (newSignature == signature)
        {
            return;
        }
        signature = newSignature;

        RebuildPalettes(manager, elements, measurementCards);
        RebuildLegend(manager);
        RebuildBoardContent(manager);
    }

    private static string BuildSignature(CrimeSceneSketchManager m, List<SketchEvidenceElementDefinition> elements, List<SketchMeasurementCard> measurementCards)
    {
        var sb = new StringBuilder();
        sb.Append((int)m.State).Append('#');
        foreach (var e in elements) sb.Append(e.evidenceId).Append(',');
        sb.Append('#');
        foreach (var c in measurementCards) sb.Append(c.CardId).Append('=').Append(c.DisplayValue).Append(',');
        sb.Append('#');
        foreach (var id in m.PlacedEvidenceInOrder) sb.Append(id).Append(':').Append(CrimeSceneSketchManager.MarkerLabel(id)).Append(',');
        sb.Append('#');
        foreach (var s in m.Definition.measurementSlots)
        {
            if (m.TryGetSlotCard(s.slotId, out var card)) sb.Append(s.slotId).Append('=').Append(card.DisplayValue).Append(',');
        }
        return sb.ToString();
    }

    private void UpdateStatusWidgets(CrimeSceneSketchManager m)
    {
        var state = m.State;
        stateLabel.text = state switch
        {
            SketchState.Submitted => "<color=#9fe3b0>Submitted</color>",
            SketchState.ReadyForSubmission => "<color=#bfeeff>Ready to submit</color>",
            SketchState.InProgress => "In progress",
            _ => "Not started"
        };
        bool ready = state == SketchState.ReadyForSubmission;
        submitLabel.text = state == SketchState.Submitted ? "Sketch Submitted" : "Submit Sketch";
        submitLabel.color = ready || state == SketchState.Submitted ? Color.white : new Color(0.55f, 0.56f, 0.6f, 1f);
        hintLabel.text = state == SketchState.Submitted
            ? "The sketch is submitted and read-only  ·  B: close"
            : hintRing != null ? $"Hint: place the {hintedElementName} inside the gold ring."
            : IsDragging ? "Right stick: move  ·  A: drop  ·  B: cancel"
            : "Right stick: move cursor  ·  A: pick up / drop  ·  B: close";
        hintLabel.color = hintRing != null ? new Color(1f,.82f,.3f,1f) : new Color(.7f,.72f,.76f,1f);
    }

    private void RebuildPalettes(CrimeSceneSketchManager m, List<SketchEvidenceElementDefinition> elements, List<SketchMeasurementCard> measurementCards)
    {
        cards.Clear();
        ClearChildren(elementArea);
        ClearChildren(measurementArea);

        elementsHeader.text = "Available Elements";
        measurementsHeader.text = "Recorded Measurements";

        if (m.IsSubmitted)
        {
            AddEmptyNote(elementArea, "Sketch submitted - read only.");
            return;
        }

        const float elementW = 266f, elementH = 62f, gap = 12f;
        for (int i = 0; i < elements.Count; i++)
        {
            var e = elements[i];
            var rect = NewRect(elementArea, "Element_" + e.evidenceId);
            SetTopLeft(rect, (i % 2) * (elementW + gap), (i / 2) * (elementH + gap), elementW, elementH);
            var bg = EditableSceneVisuals.Component<Image>(rect.gameObject);
            bg.color = elementCardColor;
            var symbolHolder = NewRect(rect, "Symbol");
            SetTopLeft(symbolHolder, 10f, 10f, 42f, 42f);
            BuildSymbol(symbolHolder, e, new Vector2(30f, 30f), string.Empty, true);
            var label = CreateText(rect, "Name", 22f, Color.white, FontStyles.Bold);
            SetTopLeft(label.rectTransform, 62f, 0f, elementW - 70f, elementH);
            label.text = EvidenceDisplay.Label(e.evidenceId, e.displayName);
            cards.Add(new PaletteCard { kind = DragKind.Evidence, id = e.evidenceId, rect = rect, background = bg, normalColor = elementCardColor });
        }
        if (elements.Count == 0)
        {
            AddEmptyNote(elementArea, m.PlacedEvidenceInOrder.Count > 0
                ? "Every discovered element is on the sketch."
                : "Photograph evidence in the scene to add it here.");
        }

        const float cardW = 128f, cardH = 52f, cardGap = 9f;
        for (int i = 0; i < measurementCards.Count; i++)
        {
            var c = measurementCards[i];
            var rect = NewRect(measurementArea, "Measurement_" + c.CardId);
            SetTopLeft(rect, (i % 4) * (cardW + cardGap), (i / 4) * (cardH + cardGap), cardW, cardH);
            var bg = EditableSceneVisuals.Component<Image>(rect.gameObject);
            bg.color = measurementCardColor;
            var label = CreateText(rect, "Value", 24f, new Color(0.1f, 0.1f, 0.12f, 1f), FontStyles.Bold);
            label.alignment = TextAlignmentOptions.Center;
            Stretch(label.rectTransform, 0f, 0f, 0f, 0f);
            label.text = c.DisplayValue;
        }
        if (measurementCards.Count == 0)
        {
            AddEmptyNote(measurementArea, "Recorded distances appear automatically when the evidence is correctly placed. Use the tape measure to record missing distances.");
        }
    }

    private void RebuildLegend(CrimeSceneSketchManager m)
    {
        ClearChildren(legendArea);
        var placed = m.PlacedEvidenceInOrder;
        if (placed.Count == 0)
        {
            AddEmptyNote(legendArea, "Placed evidence is listed here.");
            return;
        }
        for (int i = 0; i < placed.Count; i++)
        {
            var e = m.Definition.FindElement(placed[i]);
            if (e == null) continue;
            var row = NewRect(legendArea, "Legend_" + e.evidenceId);
            SetTopLeft(row, 0f, i * 34f, 540f, 32f);
            var symbol = NewRect(row, "Symbol");
            SetTopLeft(symbol, 4f, 2f, 28f, 28f);
            BuildSymbol(symbol, e, new Vector2(22f, 22f), string.Empty, true);
            string marker = CrimeSceneSketchManager.MarkerLabel(e.evidenceId);
            var text = CreateText(row, "Text", 21f, new Color(0.88f, 0.9f, 0.93f, 1f), FontStyles.Normal);
            SetTopLeft(text.rectTransform, 42f, 0f, 490f, 32f);
            text.text = (string.IsNullOrEmpty(marker) ? "<color=#7d828c>—</color>" : $"<b>{marker}</b>") + "   " + EvidenceDisplay.Label(e.evidenceId, e.displayName);
        }
    }

    private void RebuildBoardContent(CrimeSceneSketchManager m)
    {
        if (template == null) return;
        ClearChildren(linesLayer);
        ClearChildren(slotsLayer);
        ClearChildren(symbolsLayer);
        placedSymbols.Clear();
        slotOutlines.Clear();

        foreach (var e in m.Definition.evidenceElements)
        {
            if (!m.IsEvidencePlaced(e.evidenceId)) continue;
            var anchor = template.FindAnchor(e.correctAnchorId);
            if (anchor == null) continue;
            var rt = NewRect(symbolsLayer, "Placed_" + e.evidenceId);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = e.placedSize;
            rt.anchoredPosition = template.ToBoard(anchor.transform);
            BuildSymbol(rt, e, e.placedSize, CrimeSceneSketchManager.MarkerLabel(e.evidenceId), false);
            placedSymbols[e.evidenceId] = rt;
        }

        foreach (var s in m.Definition.measurementSlots)
        {
            if (!m.IsSlotVisible(s.slotId)) continue;
            var slot = template.FindSlot(s.slotId);
            var element = m.Definition.FindElement(s.evidenceId);
            var anchor = element != null ? template.FindAnchor(element.correctAnchorId) : null;
            if (slot == null) continue;

            bool filled = m.TryGetSlotCard(s.slotId, out var card);
            if (filled && anchor != null && slot.referenceFoot != null)
            {
                Vector2 from = template.ToBoard(anchor.transform);
                Vector2 to = template.ToBoard(slot.referenceFoot);
                DrawLine(linesLayer, from, to, 3f, lineColor);
                Vector2 dir = (to - from).sqrMagnitude > 1e-4f ? (to - from).normalized : Vector2.right;
                Vector2 tick = new Vector2(-dir.y, dir.x) * 9f;
                DrawLine(linesLayer, to - tick, to + tick, 3f, lineColor); // end stop on the reference
            }

            Vector2 centre = template.ToBoard(slot.transform);
            Vector2 size = slot.RectTransform.rect.size;
            var box = NewRect(slotsLayer, "Slot_" + s.slotId);
            box.anchorMin = box.anchorMax = box.pivot = new Vector2(0.5f, 0.5f);
            box.sizeDelta = size;
            box.anchoredPosition = centre;
            var fill = EditableSceneVisuals.Component<Image>(box.gameObject);
            fill.color = filled ? new Color(1f, 1f, 1f, 0.92f) : new Color(1f, 1f, 1f, 0.55f);
            var outline = BuildOutline(box, size, 2f, filled ? lineColor : slotColor);
            slotOutlines[s.slotId] = outline;

            var value = CreateText(box, "Value", filled ? 20f : 15f, filled ? new Color(0.08f, 0.1f, 0.15f, 1f) : new Color(0.15f, 0.35f, 0.65f, 1f), filled ? FontStyles.Bold : FontStyles.Italic);
            value.alignment = TextAlignmentOptions.Center;
            Stretch(value.rectTransform, 2f, 0f, 2f, 0f);
            value.text = filled ? card.DisplayValue : "distance";

            var relation = CreateText(box, "Relation", 15f, new Color(0.15f, 0.2f, 0.3f, 1f), FontStyles.Normal);
            relation.alignment = TextAlignmentOptions.Center;
            var rr = relation.rectTransform;
            rr.anchorMin = new Vector2(0f, 1f);
            rr.anchorMax = new Vector2(1f, 1f);
            rr.pivot = new Vector2(0.5f, 0f);
            rr.offsetMin = new Vector2(-30f, 0f);
            rr.offsetMax = new Vector2(30f, 20f);
            relation.text = RelationshipText(s);
        }
    }

    private static string RelationshipText(SketchMeasurementSlotDefinition s)
    {
        if (!string.IsNullOrEmpty(s.relationshipText)) return s.relationshipText;
        var reference = SceneReferencePoint.Find(s.referenceId);
        return "to " + (reference != null && !string.IsNullOrEmpty(reference.displayName) ? reference.displayName : "reference");
    }

    private void UpdateHover()
    {
        bool overSubmit = Contains(submitButton, cursorPos);
        foreach (var c in cards)
        {
            if (c.background == null) continue;
            bool hover = !IsDragging && !IsReturning && !IsReadOnly && Contains(c.rect, cursorPos);
            var color = hover ? hoverColor : c.normalColor;
            color.a = c.background.color.a;
            c.background.color = color;
        }
        if (Manager != null)
        {
            var state = Manager.State;
            submitImage.color = state == SketchState.Submitted ? new Color(0.14f, 0.30f, 0.19f, 1f)
                : state != SketchState.ReadyForSubmission ? new Color(0.22f, 0.22f, 0.25f, 1f)
                : overSubmit && !IsDragging ? new Color(0.2f, 0.58f, 0.80f, 1f) : new Color(0.12f, 0.42f, 0.60f, 1f);
        }

        SketchMeasurementSlot hoverSlot = null;
        if (dragKind == DragKind.Measurement && template != null)
        {
            hoverSlot = template.ResolveSlotDrop(CanvasToBoard(cursorPos), s => Manager.IsSlotVisible(s.slotId) && !Manager.IsSlotFilled(s.slotId));
        }
        foreach (var kv in slotOutlines)
        {
            if (kv.Value == null || Manager.IsSlotFilled(kv.Key)) continue;
            SetOutlineColor(kv.Value, hoverSlot != null && hoverSlot.slotId == kv.Key ? hoverColor : slotColor);
        }
    }

    private void UpdatePulse()
    {
        if (string.IsNullOrEmpty(pulseEvidenceId)) return;
        float t = (Time.unscaledTime - pulseStart) / 0.35f;
        if (placedSymbols.TryGetValue(pulseEvidenceId, out var rt) && rt != null)
        {
            rt.localScale = Vector3.one * (t < 1f ? 1f + 0.35f * Mathf.Sin(t * Mathf.PI) : 1f);
        }
        if (t >= 1f) pulseEvidenceId = null;
    }

    private void ShowMessage(string text, float seconds = 3f)
    {
        messageLabel.text = text;
        messageClearAt = seconds > 0f && !string.IsNullOrEmpty(text) ? Time.unscaledTime + seconds : 0f;
    }

    // --- Template adoption ----------------------------------------------------------------

    private void AdoptTemplate()
    {
        var manager = Manager != null ? Manager : FindFirstObjectByType<CrimeSceneSketchManager>();
        CrimeSceneSketchTemplate found = null;
        foreach (var t in FindObjectsByType<CrimeSceneSketchTemplate>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (manager == null || t.definition == manager.Definition)
            {
                found = t;
                break;
            }
        }
        if (found == null)
        {
            return;
        }
        template = found;
        var board = template.Board;
        var nested = template.GetComponent<Canvas>();
        if (nested != null)
        {
            nested.overrideSorting = false;
        }
        board.SetParent(boardHolder, false);
        board.anchorMin = board.anchorMax = board.pivot = new Vector2(0.5f, 0.5f);
        board.anchoredPosition3D = Vector3.zero;
        board.localRotation = Quaternion.identity;
        board.localScale = Vector3.one;
        var definition = template.definition;
        if (definition != null)
        {
            board.sizeDelta = definition.canvasSize;
        }
        boardHolder.sizeDelta = board.sizeDelta;
        template.gameObject.SetActive(true);

        ApplyPaper(definition);
        ApplyReferenceLabels();

        var layerGo = EditableSceneVisuals.Create("SketchRuntimeLayer", typeof(RectTransform));
        layerGo.transform.SetParent(board, false);
        runtimeLayer = layerGo.GetComponent<RectTransform>();
        Stretch(runtimeLayer, 0f, 0f, 0f, 0f);
        linesLayer = NewLayer(runtimeLayer, "Lines");
        slotsLayer = NewLayer(runtimeLayer, "Slots");
        symbolsLayer = NewLayer(runtimeLayer, "Symbols");
        hintLayer = NewLayer(runtimeLayer, "Hints");
    }

    private void ApplyPaper(CrimeSceneSketchDefinition definition)
    {
        if (definition == null) return;
        var board = template.Board;
        var paperTransform = board.Find("Paper");
        Image paper = paperTransform != null ? paperTransform.GetComponent<Image>() : null;
        if (paper == null)
        {
            paper = NewImage(board, "Paper", definition.paperColor);
            Stretch(paper.rectTransform, 0f, 0f, 0f, 0f);
            paper.transform.SetAsFirstSibling();
        }
        paper.color = definition.paperColor;
        if (!definition.showGrid) return;

        var grid = NewLayer(board, "Grid");
        grid.SetSiblingIndex(paper.transform.GetSiblingIndex() + 1);
        Vector2 size = definition.canvasSize;
        for (float x = -size.x * 0.5f + definition.gridSpacing; x < size.x * 0.5f; x += definition.gridSpacing)
        {
            var line = NewImage(grid, "V", definition.gridColor).rectTransform;
            line.anchorMin = line.anchorMax = new Vector2(0.5f, 0.5f);
            line.sizeDelta = new Vector2(1.5f, size.y);
            line.anchoredPosition = new Vector2(x, 0f);
        }
        for (float y = -size.y * 0.5f + definition.gridSpacing; y < size.y * 0.5f; y += definition.gridSpacing)
        {
            var line = NewImage(grid, "H", definition.gridColor).rectTransform;
            line.anchorMin = line.anchorMax = new Vector2(0.5f, 0.5f);
            line.sizeDelta = new Vector2(size.x, 1.5f);
            line.anchoredPosition = new Vector2(0f, y);
        }
    }

    // Reference labels read the same display names the tape measure uses.
    private void ApplyReferenceLabels()
    {
        foreach (var f in template.FixedElements)
        {
            if (f.kind != SketchFixedElementKind.ReferenceLabel || string.IsNullOrEmpty(f.referenceId)) continue;
            var reference = SceneReferencePoint.Find(f.referenceId);
            var text = f.GetComponentInChildren<TMP_Text>(true);
            if (reference != null && text != null && !string.IsNullOrEmpty(reference.displayName))
            {
                text.text = reference.displayName;
            }
        }
    }

    // --- Runtime UI ----------------------------------------------------------------------

    [EditableSceneBuilder]
    private void BuildRuntimeUI()
    {
        using (EditableSceneVisuals.Begin(this,"BuildRuntimeUI"))
        {
        var canvasGo = EditableSceneVisuals.Create("CrimeSceneSketchCanvas", typeof(RectTransform), typeof(Canvas));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = short.MaxValue - 4;
        canvasRect = canvasGo.GetComponent<RectTransform>();
        canvasRect.sizeDelta = panelSize;
        canvasGo.transform.localScale = Vector3.one * panelScale;
        panelRoot = canvasGo;

        var background = NewImage(canvasRect, "Background", panelColor);
        Stretch(background.rectTransform, 0f, 0f, 0f, 0f);

        var title = CreateText(canvasRect, "Title", 34f, Color.white, FontStyles.Bold);
        SetTopLeft(title.rectTransform, 24f, 14f, 600f, 50f);
        title.text = "Crime Scene Sketch";
        stateLabel = CreateText(canvasRect, "State", 22f, new Color(0.75f, 0.78f, 0.82f, 1f), FontStyles.Normal);
        SetTopLeft(stateLabel.rectTransform, 360f, 20f, 240f, 40f);
        stateLabel.alignment = TextAlignmentOptions.MidlineRight;

        elementsHeader = CreateHeader(canvasRect, "ElementsHeader", 80f);
        elementArea = NewRect(canvasRect, "ElementArea");
        SetTopLeft(elementArea, 24f, 118f, 556f, 136f);

        measurementsHeader = CreateHeader(canvasRect, "MeasurementsHeader", 272f);
        measurementArea = NewRect(canvasRect, "MeasurementArea");
        SetTopLeft(measurementArea, 24f, 310f, 556f, 175f);

        var legendHeader = CreateHeader(canvasRect, "LegendHeader", 500f);
        legendHeader.text = "Legend";
        legendArea = NewRect(canvasRect, "LegendArea");
        SetTopLeft(legendArea, 24f, 538f, 556f, 300f);

        messageLabel = CreateText(canvasRect, "Message", 21f, new Color(1f, 0.86f, 0.55f, 1f), FontStyles.Italic);
        SetTopLeft(messageLabel.rectTransform, 24f, 880f, 556f, 60f);
        messageLabel.alignment = TextAlignmentOptions.Center;

        submitImage = NewImage(canvasRect, "SubmitButton", new Color(0.22f, 0.22f, 0.25f, 1f));
        submitButton = submitImage.rectTransform;
        SetTopLeft(submitButton, 24f, 970f, 556f, 72f);
        submitLabel = CreateText(submitButton, "Label", 28f, Color.white, FontStyles.Bold);
        submitLabel.alignment = TextAlignmentOptions.Center;
        Stretch(submitLabel.rectTransform, 0f, 0f, 0f, 0f);
        submitLabel.text = "Submit Sketch";

        var boardFrame = NewImage(canvasRect, "BoardFrame", new Color(0.3f, 0.33f, 0.38f, 1f));
        SetTopLeft(boardFrame.rectTransform, 610f, 72f, 1012f, 1012f);
        boardHolder = NewRect(canvasRect, "Board");
        boardHolder.anchorMin = boardHolder.anchorMax = boardHolder.pivot = new Vector2(0.5f, 0.5f);
        boardHolder.sizeDelta = new Vector2(1000f, 1000f);
        boardHolder.anchoredPosition = new Vector2(panelSize.x * 0.5f - 24f - 500f, panelSize.y * 0.5f - 78f - 500f);
        var frameRect = boardFrame.rectTransform;
        frameRect.anchorMin = frameRect.anchorMax = frameRect.pivot = new Vector2(0.5f, 0.5f);
        frameRect.sizeDelta = new Vector2(1012f, 1012f);
        frameRect.anchoredPosition = boardHolder.anchoredPosition;

        hintLabel = CreateText(canvasRect, "Hint", 23f, new Color(0.7f, 0.72f, 0.76f, 1f), FontStyles.Normal);
        hintLabel.alignment = TextAlignmentOptions.Center;
        var hr = hintLabel.rectTransform;
        hr.anchorMin = new Vector2(0f, 0f);
        hr.anchorMax = new Vector2(1f, 0f);
        hr.offsetMin = new Vector2(610f, 4f);
        hr.offsetMax = new Vector2(-24f, 34f);

        dragLayer = NewRect(canvasRect, "DragLayer");
        Stretch(dragLayer, 0f, 0f, 0f, 0f);

        confirmOverlay = BuildConfirmOverlay();

        cursor = NewRect(canvasRect, "Cursor");
        cursor.anchorMin = cursor.anchorMax = cursor.pivot = new Vector2(0.5f, 0.5f);
        cursor.sizeDelta = new Vector2(30f, 30f);
        BuildOutline(cursor, new Vector2(30f, 30f), 4f, new Color(1f, 0.85f, 0.2f, 1f));
        var dot = NewImage(cursor, "Dot", new Color(1f, 0.85f, 0.2f, 1f)).rectTransform;
        dot.anchorMin = dot.anchorMax = new Vector2(0.5f, 0.5f);
        dot.sizeDelta = new Vector2(6f, 6f);
        foreach (var g in cursor.GetComponentsInChildren<Graphic>()) g.raycastTarget = false;
            }
    }

    private GameObject BuildConfirmOverlay()
    {
        var dim = NewImage(canvasRect, "ConfirmOverlay", new Color(0f, 0f, 0f, 0.6f));
        Stretch(dim.rectTransform, 0f, 0f, 0f, 0f);
        var box = NewImage(dim.transform, "Box", new Color(0.1f, 0.12f, 0.16f, 1f)).rectTransform;
        box.anchorMin = box.anchorMax = box.pivot = new Vector2(0.5f, 0.5f);
        box.sizeDelta = new Vector2(760f, 220f);
        BuildOutline(box, box.sizeDelta, 3f, new Color(0.45f, 0.85f, 1f, 1f));
        var question = CreateText(box, "Question", 30f, Color.white, FontStyles.Bold);
        question.alignment = TextAlignmentOptions.Center;
        SetTopLeft(question.rectTransform, 20f, 36f, 720f, 70f);
        question.text = "Submit completed crime scene sketch?";
        var keys = CreateText(box, "Keys", 23f, new Color(0.75f, 0.92f, 1f, 1f), FontStyles.Normal);
        keys.alignment = TextAlignmentOptions.Center;
        SetTopLeft(keys.rectTransform, 20f, 120f, 720f, 60f);
        keys.text = "A: submit (cannot be changed afterwards)   ·   B: go back";
        dim.gameObject.SetActive(false);
        return dim.gameObject;
    }

    private TMP_Text CreateHeader(RectTransform parent, string name, float top)
    {
        var header = CreateText(parent, name, 24f, new Color(0.45f, 0.85f, 1f, 1f), FontStyles.Bold);
        SetTopLeft(header.rectTransform, 24f, top, 556f, 34f);
        return header;
    }

    private void AddEmptyNote(RectTransform parent, string text)
    {
        var note = CreateText(parent, "Empty", 19f, new Color(0.62f, 0.64f, 0.68f, 1f), FontStyles.Italic);
        SetTopLeft(note.rectTransform, 4f, 0f, 548f, 60f);
        note.alignment = TextAlignmentOptions.TopLeft;
        note.text = text;
    }

    // Shape drawn from Image rectangles only - no sprite assets needed.
    private void BuildSymbol(RectTransform parent, SketchEvidenceElementDefinition e, Vector2 size, string marker, bool icon)
    {
        if (e.icon != null)
        {
            var img = NewImage(parent, "Icon", Color.white);
            img.sprite = e.icon;
            img.preserveAspect = true;
            CentreSize(img.rectTransform, size);
        }
        else
        {
            switch (e.shape)
            {
                case SketchSymbolShape.BodyOutline:
                {
                    Vector2 s = icon ? new Vector2(size.x * 0.55f, size.y) : size;
                    var fill = NewImage(parent, "Fill", new Color(e.color.r, e.color.g, e.color.b, 0.18f));
                    CentreSize(fill.rectTransform, s);
                    var holder = NewRect(parent, "Outline");
                    CentreSize(holder, s);
                    BuildOutline(holder, s, icon ? 3f : 4f, e.color);
                    break;
                }
                case SketchSymbolShape.Diamond:
                {
                    var img = NewImage(parent, "Diamond", e.color);
                    CentreSize(img.rectTransform, size * 0.72f);
                    img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                    break;
                }
                case SketchSymbolShape.Bar:
                {
                    var img = NewImage(parent, "Bar", e.color);
                    CentreSize(img.rectTransform, icon ? new Vector2(size.x, size.y * 0.32f) : size);
                    img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, icon ? 35f : 0f);
                    break;
                }
                default:
                {
                    var img = NewImage(parent, "Square", e.color);
                    CentreSize(img.rectTransform, size);
                    break;
                }
            }
        }

        if (!string.IsNullOrEmpty(marker))
        {
            bool outline = e.shape == SketchSymbolShape.BodyOutline;
            var badge = NewImage(parent, "Marker", outline ? new Color(0.98f, 0.97f, 0.92f, 1f) : new Color(0.08f, 0.09f, 0.12f, 1f));
            var br = badge.rectTransform;
            br.anchorMin = br.anchorMax = new Vector2(0.5f, 0.5f);
            br.sizeDelta = new Vector2(26f, 24f);
            br.anchoredPosition = outline ? Vector2.zero : new Vector2(size.x * 0.5f + 12f, size.y * 0.5f + 8f);
            var text = CreateText(badge.transform, "Text", 18f, outline ? e.color : Color.white, FontStyles.Bold);
            text.alignment = TextAlignmentOptions.Center;
            Stretch(text.rectTransform, 0f, 0f, 0f, 0f);
            text.text = marker;
        }
    }

    private static void DrawLine(RectTransform parent, Vector2 from, Vector2 to, float thickness, Color color)
    {
        var img = NewImage(parent, "Line", color);
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        Vector2 d = to - from;
        rt.sizeDelta = new Vector2(d.magnitude, thickness);
        rt.anchoredPosition = (from + to) * 0.5f;
        rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
    }

    // Four thin bars; returns the first so the caller can recolour the set via SetOutlineColor.
    private static Image BuildOutline(RectTransform parent, Vector2 size, float thickness, Color color)
    {
        Image first = null;
        var specs = new[]
        {
            (new Vector2(0f, size.y * 0.5f - thickness * 0.5f), new Vector2(size.x, thickness)),
            (new Vector2(0f, -size.y * 0.5f + thickness * 0.5f), new Vector2(size.x, thickness)),
            (new Vector2(-size.x * 0.5f + thickness * 0.5f, 0f), new Vector2(thickness, size.y)),
            (new Vector2(size.x * 0.5f - thickness * 0.5f, 0f), new Vector2(thickness, size.y))
        };
        foreach (var (pos, sz) in specs)
        {
            var bar = NewImage(parent, "Edge", color);
            var rt = bar.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = sz;
            rt.anchoredPosition = pos;
            if (first == null) first = bar;
        }
        return first;
    }

    private static void SetOutlineColor(Image first, Color color)
    {
        var parent = first.transform.parent;
        foreach (Transform child in parent)
        {
            if (child.name == "Edge" && child.TryGetComponent<Image>(out var img)) img.color = color;
        }
    }

    private PaletteCard FindCard(DragKind kind, string id)
    {
        foreach (var c in cards)
        {
            if (c.kind == kind && c.id == id) return c;
        }
        return null;
    }

    private static void SetCardAlpha(PaletteCard card, float alpha)
    {
        if (card?.rect == null) return;
        var group = card.rect.GetComponent<CanvasGroup>();
        if (group == null) group = EditableSceneVisuals.Component<CanvasGroup>(card.rect.gameObject);
        group.alpha = alpha;
    }

    private bool Contains(RectTransform rt, Vector2 canvasPosition)
    {
        if (rt == null) return false;
        Vector2 local = rt.InverseTransformPoint(canvasRect.TransformPoint(canvasPosition));
        return rt.rect.Contains(local);
    }

    private Vector2 CanvasPositionOf(RectTransform rt)
    {
        Vector3 centreWorld = rt.TransformPoint(rt.rect.center);
        return canvasRect.InverseTransformPoint(centreWorld);
    }

    private static void ClearChildren(RectTransform parent)
    {
        if (parent == null) return;
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var child = parent.GetChild(i);
            child.SetParent(null, false);
            Destroy(child.gameObject);
        }
    }

    private static RectTransform NewLayer(RectTransform parent, string name)
    {
        var rt = NewRect(parent, name);
        Stretch(rt, 0f, 0f, 0f, 0f);
        return rt;
    }

    private static RectTransform NewRect(Transform parent, string name)
    {
        var go = EditableSceneVisuals.Create(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    private static TMP_Text CreateText(Transform parent, string name, float size, Color color, FontStyles style)
    {
        var go = EditableSceneVisuals.Create(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var text = EditableSceneVisuals.Component<TextMeshProUGUI>(go);
        text.fontSize = size;
        text.color = color;
        text.fontStyle = style;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Overflow;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.richText = true;
        text.raycastTarget = false;
        return text;
    }

    private static Image NewImage(Transform parent, string name, Color color)
    {
        var go = EditableSceneVisuals.Create(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static void CentreSize(RectTransform rt, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;
    }

    private static void Stretch(RectTransform rect, float left, float top, float right, float bottom)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    // Rect at (left, top) from the parent's top-left corner.
    private static void SetTopLeft(RectTransform rect, float left, float top, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = new Vector2(left, -top);
    }
}
