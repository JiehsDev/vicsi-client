using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>One session-local follow-up, accessed at the physical vendor, using panel input,
/// catalog and history. New investigations reload the scene and discard this record.</summary>
public class VendorFollowupInterview : MonoBehaviour
{
    public const string StatementId = "statement_vendor_paul_3am";
    public const string QuestionId = "vendor-followup-anyone-else";
    public const string AnswerId = "statement-vendor-paul-3am";
    public const string RelationshipId = "REL-paul-timeline-contradiction";
    public static VendorFollowupInterview Instance { get; private set; }
    public bool IsDiscovered { get; private set; }
    public bool IsTalking { get; private set; }
    public bool IsShowing => panel != null && panel.activeSelf;
    public event System.Action OnStatementDiscovered;
    public WorldPanelPointer Pointer => pointer;
    public bool FollowupUnlocked => EvidenceStateManager.Instance?.IsFingerprintMatched("EVD-014") == true
        && string.Equals(EvidenceStateManager.Instance.GetRecord("EVD-014")?.fingerprintLabMatchResult,
            "Paul", System.StringComparison.OrdinalIgnoreCase);
    public bool RequiresFollowup => FollowupUnlocked && !IsDiscovered;
    public static string CompletionBlockReason => Instance != null && Instance.RequiresFollowup
        ? GameDialogue.Text("vendor-followup-required") : null;
    public bool CanAsk => !ProcedureInterlock.BlocksUnrelatedActions && SceneTurnover.Phase == ScenarioPhase.SOCOInvestigation
        && FollowupUnlocked && !IsDiscovered && !IsTalking;
    public string StatementSummary => IsDiscovered ? GameDialogue.Text("vendor-paul-3am-summary") : string.Empty;
    public string StatementDetail => IsDiscovered ? GameDialogue.Text("vendor-paul-3am-detail") : string.Empty;

    private GameObject panel;
    private TMP_Text content, question;
    private WorldPanelPointer pointer;
    private InputAction accept, close;
    private VendorWitnessInteraction activeVendor;
    private int openedFrame = -1;
    private bool leadAnnounced;

    private void Awake()
    {
        Instance = this;
        accept = PlayerUIGate.CreateConfirmAction(this, "VendorFollowup_Ask", () => IsShowing && CanAsk, 400);
        close = new InputAction("VendorFollowup_Close", InputActionType.Button, "<XRController>{RightHand}/secondaryButton");
        BuildPanel();
    }
    private void OnEnable() { accept.Enable(); close.Enable(); }
    private void OnDisable()
    {
        Hide(); accept.Disable(); close.Disable();
        StopAllCoroutines(); IsTalking = false;
    }
    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        pointer?.Dispose(); accept?.Dispose(); close?.Dispose();
    }

    public bool HasDiscovered(string id) => id == StatementId && IsDiscovered;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    internal void PrepareDevelopmentStart()
    {
        SceneTestStart.RequirePreparation();
        IsDiscovered = true;
        OnStatementDiscovered?.Invoke();
    }
#endif

    internal void ShowFromVendor(VendorWitnessInteraction vendor)
    {
        if (vendor == null || vendor.Interview != this || !vendor.isActiveAndEnabled || !vendor.PlayerInRange()
            || SceneTurnover.Phase != ScenarioPhase.SOCOInvestigation || IsTalking || PlayerUIGate.IsBlocked) return;
        activeVendor = vendor; openedFrame = Time.frameCount;
        content.text = GameDialogue.Text("vendor-followup-heading") + "\n\n"
            + GameDialogue.Text("statement-vendor-peter-leaving") + "\n\n"
            + GameDialogue.Text("statement-vendor-discovery") + "\n\n"
            + GameDialogue.Text("statement-vendor-limitation") + "\n\n"
            + (IsDiscovered ? StatementSummary + "\n\n" + StatementDetail : string.Empty);
        if(IsDiscovered) content.text+="\n\nFollow-up recorded. You can review this account in Witness Accounts.";
        else if(!FollowupUnlocked) content.text+="\n\nNo follow-up question is available yet. Continue investigating and return when you have a new lead.";
        question.text = "[A] " + GameDialogue.Text(QuestionId);
        question.gameObject.SetActive(CanAsk);
        panel.GetComponent<ResponsivePanelLayout>().Refresh();
        HudFollowCamera.ConfigureShared(panel, this, PlayerUIGate.DialoguePriority);
        panel.SetActive(true);
        PlayerUIGate.Enter(this); ToolInputLock.Enter(this); LocomotionSuspender.Suspend(); pointer.Enable();
    }
    public void Hide()
    {
        if (!IsShowing) return;
        panel.SetActive(false); pointer.Disable();
        activeVendor = null;
        PlayerUIGate.Exit(this); ToolInputLock.Exit(this); LocomotionSuspender.Resume();
    }
    private void Update()
    {
        if (!leadAnnounced && RequiresFollowup && SceneTurnover.Phase == ScenarioPhase.SOCOInvestigation
            && !ProcedureInterlock.BlocksUnrelatedActions && !PlayerUIGate.IsBlocked
            && (STCSNotificationUI.Instance == null || (!STCSNotificationUI.Instance.IsShowing && STCSNotificationUI.Instance.PendingCount == 0)))
        {
            leadAnnounced = true;
            GameDialogue.Say("vendor-followup-required");
        }
        if (!IsShowing) return;
        if (activeVendor == null || !activeVendor.PlayerInRange() || SceneTurnover.Phase != ScenarioPhase.SOCOInvestigation)
        { Hide(); return; }
        if (close.WasPressedThisFrame()) { Hide(); return; }
        if (Time.frameCount == openedFrame) return;
        pointer.Tick();
        if (PlayerUIGate.TryConsumeConfirm(this, accept)) AskAnyoneElse();
    }

    /// <summary>The UI's one question action. Repeated selections do not duplicate
    /// the conversation or discovery. Completion waits for delivery of the answer.</summary>
    public bool AskAnyoneElse()
    {
        var dialogue = STCSNotificationUI.Instance;
        if (!CanAsk || !IsShowing || activeVendor == null || !activeVendor.PlayerInRange()
            || Time.frameCount == openedFrame || PlayerUIGate.IsBlockedByOther(this) || dialogue == null
            || !GameDialogue.Exists(QuestionId) || !GameDialogue.Exists(AnswerId)) return false;
        Hide(); IsTalking = true;
        SessionLogger.Instance?.LogEvent(SessionEventType.WitnessFollowupQuestionSelected, QuestionId);
        GameDialogue.SayLine(QuestionId, "Witness", StatementId, dialogue);
        GameDialogue.SayLine(AnswerId, "Witness", StatementId, dialogue);
        StartCoroutine(FinishAnswer(dialogue));
        return true;
    }
    private IEnumerator FinishAnswer(STCSNotificationUI dialogue)
    {
        yield return null;
        while (dialogue != null && dialogue.HasLinesFrom(StatementId)) yield return null;
        IsTalking = false;
        if (dialogue == null || IsDiscovered) yield break;
        IsDiscovered = true;
        SessionLogger.Instance?.LogEvent(SessionEventType.WitnessStatementDiscovered, StatementId,
            new Dictionary<string, string> { { "questionId", QuestionId }, { "answerId", AnswerId } });
        RelationshipStateManager.Instance?.RefreshUnlocks();
        OnStatementDiscovered?.Invoke();
    }

    [EditableSceneBuilder]
    private void BuildPanel()
    {
        using (EditableSceneVisuals.Begin(this,"BuildPanel"))
        {
        panel = EditableSceneVisuals.Create("VendorFollowupPanel", typeof(RectTransform), typeof(Canvas), typeof(HudFollowCamera));
        panel.transform.SetParent(transform, false); panel.transform.localScale = Vector3.one * .0006f;
        var rect = (RectTransform)panel.transform; rect.sizeDelta = new Vector2(1000, 1000);
        var canvas = panel.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true; canvas.sortingOrder = short.MaxValue - 5;
        panel.GetComponent<HudFollowCamera>().Distance = 1.05f;
        panel.GetComponent<HudFollowCamera>().LocalOffset = new Vector3(0f, -.1f, 0f);
        var bg = EditableSceneVisuals.Create("Background", typeof(RectTransform), typeof(Image)); bg.transform.SetParent(panel.transform, false);
        var br = (RectTransform)bg.transform; br.anchorMin = Vector2.zero; br.anchorMax = Vector2.one;
        br.offsetMin = br.offsetMax = Vector2.zero;
        bg.GetComponent<Image>().color = new Color(.03f,.05f,.06f,.95f); bg.GetComponent<Image>().raycastTarget = false;
        var label = EditableSceneVisuals.Create("Statement", typeof(RectTransform), typeof(TextMeshProUGUI)); label.transform.SetParent(panel.transform, false);
        content = label.GetComponent<TextMeshProUGUI>(); content.fontSize = 26; content.color = Color.white;
        content.alignment = TextAlignmentOptions.TopLeft; content.raycastTarget = false;
        content.rectTransform.sizeDelta = new Vector2(920, 800); content.rectTransform.anchoredPosition = new Vector2(0, 50);
        var row = EditableSceneVisuals.Create("Question", typeof(RectTransform), typeof(TextMeshProUGUI)); row.transform.SetParent(panel.transform, false);
        question = row.GetComponent<TextMeshProUGUI>(); question.fontSize = 28; question.color = Color.white;
        question.alignment = TextAlignmentOptions.Center; question.raycastTarget = false;
        question.rectTransform.sizeDelta = new Vector2(920, 70); question.rectTransform.anchoredPosition = new Vector2(0, -410);
        var hint = EditableSceneVisuals.Create("Controls", typeof(RectTransform), typeof(TextMeshProUGUI)); hint.transform.SetParent(panel.transform, false);
        var controls = hint.GetComponent<TextMeshProUGUI>(); controls.fontSize = 22; controls.color = Color.white;
        controls.text = "[B] Close"; controls.alignment = TextAlignmentOptions.Center; controls.raycastTarget = false;
        controls.rectTransform.sizeDelta = new Vector2(920, 40); controls.rectTransform.anchoredPosition = new Vector2(0, -470);
        pointer = new WorldPanelPointer(rect, "VendorFollowup", this);
        var responsive = ResponsivePanelLayout.Create(rect, null, null, controls, 700, 1000, 40);
        responsive.AddText(content, responsive.Body, 48);
        var questionRow = responsive.AddText(question, responsive.Footer, 70);
        questionRow.SetAsFirstSibling();
        pointer.Register(QuestionId, questionRow, Vector2.zero, () => AskAnyoneElse());
        responsive.RegisterControls(pointer, "layout-vendor-");
        panel.SetActive(false);
            }
    }
}
