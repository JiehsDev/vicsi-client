using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Decision/arrival/briefing presentation only. SceneTurnover owns all procedural milestones.</summary>
public class SOCOWorkflowInteraction : MonoBehaviour
{
    public const string BriefingSourceId = "ioc-soco-turnover";
    public readonly string[] BriefingLineIds = { "IOC_SOCO_01", "IOC_SOCO_02", "IOC_SOCO_03" };
    private GameObject panel;
    private TMP_Text heading, firstChoice, secondChoice, controls;
    private RectTransform firstRow, secondRow;
    private ResponsivePanelLayout responsiveLayout;
    private int layoutSelection = -1;
    private ScenarioPhase layoutPhase;
    public ResponsivePanelLayout ResponsiveLayout => responsiveLayout;
    private WorldPanelPointer pointer;
    private InputAction accept, navigate;
    private int selection;
    private float navigateAfter;
    private bool talking;
    private bool approachGuidanceShown;
    private string shownApproachGuidance;
    [Header("IOC Ground Guidance")]
    [SerializeField] private GameObject iocGroundGuidanceMarker;
    [SerializeField, Min(0.25f)] private float interactionRadius = 2.5f;
    [SerializeField] private string guidanceTitle = "Meet the IOC";
    [SerializeField, TextArea(2, 5)] private string guidanceMessage = "Locate the Incident-on-Scene Commander and receive the initial briefing before beginning the investigation.";
    [SerializeField] private bool showGuidanceOnApproach = true;
    [SerializeField] private bool hideMarkerOnBriefing = true;
    private Transform rig;
    [Header("Comfortable SOCO Arrival")]
    [SerializeField] private MeshRenderer arrivalFade;
    [SerializeField] private Transform arrivalFeetAnchor;
    [SerializeField, Min(.1f)] private float arrivalFadeSeconds = .5f;
    [SerializeField, Min(.1f)] private float arrivalSettleSeconds = .4f;
    private Material arrivalMaterial;
    private readonly List<Canvas> arrivalCanvases = new();
    private bool arrivalLocks;
    public bool IsArriving { get; private set; }
    public float ArrivalAlpha { get; private set; }
    public Transform ArrivalFeetAnchor => arrivalFeetAnchor;
    public bool IsTalking => talking;
    public bool IsIOCStartingMarkerVisible => iocGroundGuidanceMarker != null && iocGroundGuidanceMarker.activeSelf;
    public float InteractionRadius => interactionRadius;
    public string GuidanceTitle => guidanceTitle;
    public string GuidanceMessage => guidanceMessage;
    public bool ShowGuidanceOnApproach => showGuidanceOnApproach;
    public bool HideMarkerOnBriefing => hideMarkerOnBriefing;
    public bool IsWithinIOCInteractionRadius => IsInsideInteractionRadius();
    public bool IsPanelVisible => panel != null && panel.activeSelf;
    public WorldPanelPointer Pointer => pointer;

    private void Awake()
    {
        var cameraRig = Camera.main != null ? Camera.main.GetComponentInParent<OVRCameraRig>() : null;
        rig = cameraRig != null ? cameraRig.transform : null;
        accept = PlayerUIGate.CreateConfirmAction(this, "SOCOFlow_Accept", () => IsPanelVisible && SceneTurnover.Phase != ScenarioPhase.SOCORequested, 400);
        navigate = new InputAction("SOCOFlow_Select", InputActionType.Value, "<XRController>{RightHand}/primary2DAxis");
        BuildPanel();
    }
    private void OnEnable()
    {
        accept.Enable(); navigate.Enable();
        SceneTurnover.OnPhaseChanged += PhaseChanged;
    }
    private void OnDisable()
    {
        SceneTurnover.OnPhaseChanged -= PhaseChanged;
        ClearApproachGuidance();
        accept.Disable(); navigate.Disable(); pointer.Disable(); panel.SetActive(false);
        StopAllCoroutines(); talking = false;
        EndArrival();
    }
    private void OnDestroy() { accept?.Dispose(); navigate?.Dispose(); pointer?.Dispose(); if (arrivalMaterial != null) Destroy(arrivalMaterial); }

    private void PhaseChanged(ScenarioPhase phase)
    {
        selection = 0;
        if (phase != ScenarioPhase.SOCOTurnover) ClearApproachGuidance();
        RefreshPanel();
        if (phase == ScenarioPhase.SOCORequested)
        {
            GameDialogue.Say("soco-assistance-requested");
            StartCoroutine(AwaitArrival());
        }
        if (phase == ScenarioPhase.SOCOTurnover) GameDialogue.Say("csi-form-2-submitted");
        if (phase == ScenarioPhase.SOCOInvestigation)
        {
            HideIOCStartingMarker();
            GameDialogue.Say("soco-role-started");
        }
    }
    private IEnumerator AwaitArrival()
    {
        // Local acknowledgement of arrival; no simulated dispatch/network request or cinematic.
        yield return new WaitForSecondsRealtime(2f);
        while (PlayerUIGate.IsBlocked && SceneTurnover.Phase == ScenarioPhase.SOCORequested) yield return null;
        AcknowledgeArrival();
    }
    public bool Choose(bool request)
    {
        if (PlayerUIGate.IsBlocked || SceneTurnover.Phase != ScenarioPhase.IOCSOCODecision) return false;
        bool advanced = SceneTurnover.DecideSOCOAssistance(request);
        if (!request) GameDialogue.Say("soco-decision-reconsider");
        return advanced;
    }
    public bool AcknowledgeArrival()
    {
        if (IsArriving || PlayerUIGate.IsBlocked || SceneTurnover.Phase != ScenarioPhase.SOCORequested) return false;
        // Never perform an uncovered teleport when the authored comfort overlay is missing.
        if (arrivalFade == null || arrivalFeetAnchor == null || rig == null) return false;
        if (!SceneTurnover.TryAdvance(ScenarioPhase.SOCOTurnover)) return false;
        IsArriving = true;
        arrivalLocks = true;
        PlayerUIGate.Enter(this); ToolInputLock.Enter(this); LocomotionSuspender.Suspend();
        foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (canvas.enabled) { arrivalCanvases.Add(canvas); canvas.enabled = false; }
        StartCoroutine(ArriveWithFade());
        return true;
    }
    private IEnumerator ArriveWithFade()
    {
        try
        {
            yield return FadeArrival(0f, 1f);
            // Give both eyes a fully opaque rendered frame before changing the origin.
            yield return new WaitForEndOfFrame();
            RepositionArrival();
            yield return new WaitForSecondsRealtime(arrivalSettleSeconds);
            yield return FadeArrival(1f, 0f);
        }
        finally { EndArrival(); }
    }
    private void RepositionArrival()
    {
        var locomotor = FindAnyObjectByType<Oculus.Interaction.Locomotion.FirstPersonLocomotor>();
        var character = FindAnyObjectByType<Oculus.Interaction.Locomotion.CharacterController>();
        Vector3 feet = arrivalFeetAnchor.position;
        // Tracking continues normally; only the origin yaw changes, while black.
        rig.rotation = arrivalFeetAnchor.rotation;
        if (locomotor != null && character != null && locomotor.isActiveAndEnabled)
        {
            locomotor.HandleLocomotionEvent(new Oculus.Interaction.Locomotion.LocomotionEvent(
                -30001, new Pose(feet, character.Pose.rotation),
                Oculus.Interaction.Locomotion.LocomotionEvent.TranslationType.Absolute,
                Oculus.Interaction.Locomotion.LocomotionEvent.RotationType.None));
            character.SetPosition(feet + Vector3.up * character.Height * .5f);
            locomotor.ResetPlayerToCharacter();
        }
        else
        {
            Vector3 headOffset = Camera.main != null ? Camera.main.transform.position - rig.position : Vector3.zero;
            headOffset.y = 0;
            rig.position = feet - headOffset;
        }
        FindAnyObjectByType<SceneCordon>()?.AccessRegister?.RecordSimulationReposition(Camera.main != null ? Camera.main.transform.position : feet);
    }
    private IEnumerator FadeArrival(float from, float to)
    {
        float elapsed = 0;
        while (elapsed < arrivalFadeSeconds)
        {
            SetArrivalAlpha(Mathf.Lerp(from, to, Mathf.SmoothStep(0, 1, elapsed / arrivalFadeSeconds)));
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        SetArrivalAlpha(to);
    }
    private void SetArrivalAlpha(float alpha)
    {
        ArrivalAlpha = alpha;
        if (arrivalFade == null) return;
        if (arrivalMaterial == null) arrivalMaterial = new Material(arrivalFade.sharedMaterial);
        arrivalMaterial.SetColor("_Color", new Color(0, 0, 0, alpha));
        arrivalFade.sharedMaterial = arrivalMaterial;
        arrivalFade.enabled = alpha > 0;
    }
    private void EndArrival()
    {
        SetArrivalAlpha(0f);
        foreach (var canvas in arrivalCanvases) if (canvas != null) canvas.enabled = true;
        arrivalCanvases.Clear();
        IsArriving = false;
        if (!arrivalLocks) return;
        arrivalLocks = false;
        PlayerUIGate.Exit(this); ToolInputLock.Exit(this); LocomotionSuspender.Resume();
    }
    public bool BeginBriefing()
    {
        var dialogue = STCSNotificationUI.Instance;
        if (SceneTurnover.Phase != ScenarioPhase.SOCOTurnover || talking || PlayerUIGate.IsBlocked || dialogue == null
            || !IsInsideInteractionRadius()) return false;
        foreach (string id in BriefingLineIds) if (ViCSIDialogueCatalog.Current.Find(id) == null) return false;
        ClearApproachGuidance();
        if (hideMarkerOnBriefing) HideIOCStartingMarker();
        talking = true; RefreshPanel();
        foreach (string id in BriefingLineIds) GameDialogue.SayLine(id, "IOC", BriefingSourceId, dialogue);
        StartCoroutine(FinishBriefing(dialogue));
        return true;
    }
    private void HideIOCStartingMarker()
    {
        if (iocGroundGuidanceMarker != null) iocGroundGuidanceMarker.SetActive(false);
    }
    private bool IsInsideInteractionRadius()
    {
        if (iocGroundGuidanceMarker == null) return true;
        Transform player = rig != null ? rig : Camera.main != null ? Camera.main.transform : null;
        if (player == null) return true;
        Vector3 offset = player.position - iocGroundGuidanceMarker.transform.position;
        offset.y = 0f;
        return offset.sqrMagnitude <= interactionRadius * interactionRadius;
    }
    private void UpdateApproachGuidance(bool insideRadius)
    {
        bool shouldShow = showGuidanceOnApproach && insideRadius && !talking
            && SceneTurnover.Phase == ScenarioPhase.SOCOTurnover;
        if (shouldShow && !approachGuidanceShown)
        {
            shownApproachGuidance = string.IsNullOrWhiteSpace(guidanceTitle) ? guidanceMessage : guidanceTitle + "\n" + guidanceMessage;
            NotificationManager.ShowPrompt(shownApproachGuidance);
            approachGuidanceShown = true;
        }
        else if (!shouldShow && approachGuidanceShown)
        {
            ClearApproachGuidance();
        }
    }
    private void ClearApproachGuidance()
    {
        var ui = NotificationManager.Instance != null ? NotificationManager.Instance.UI : null;
        if (approachGuidanceShown && ui != null && ui.CurrentAmbientPrompt == shownApproachGuidance)
            NotificationManager.HidePrompt();
        approachGuidanceShown = false;
        shownApproachGuidance = null;
    }
    private IEnumerator FinishBriefing(STCSNotificationUI dialogue)
    {
        yield return null;
        while (dialogue != null && dialogue.HasLinesFrom(BriefingSourceId)) yield return null;
        talking = false;
        if (dialogue != null && SceneTurnover.Phase == ScenarioPhase.SOCOTurnover)
            SceneTurnover.TryAdvance(ScenarioPhase.SOCOInvestigation);
        RefreshPanel();
    }
    private void Update()
    {
        RefreshPanel();
        if (!IsPanelVisible || SceneTurnover.Phase == ScenarioPhase.SOCORequested) return;
        if (SceneTurnover.Phase == ScenarioPhase.IOCSOCODecision && Time.unscaledTime >= navigateAfter)
        {
            float y = navigate.ReadValue<Vector2>().y;
            if (Mathf.Abs(y) > .6f) { selection = y > 0 ? 0 : 1; navigateAfter = Time.unscaledTime + .2f; }
        }
        pointer.Tick();
        if (PlayerUIGate.TryConsumeConfirm(this, accept))
        {
            if (SceneTurnover.Phase == ScenarioPhase.IOCSOCODecision) Choose(selection == 0);
            else BeginBriefing();
        }
    }
    private void RefreshPanel()
    {
        var phase = SceneTurnover.Phase;
        bool insideInteractionRadius = IsInsideInteractionRadius();
        UpdateApproachGuidance(insideInteractionRadius);
        bool visible = SceneTurnover.IsGated && !PlayerUIGate.IsBlocked && !talking
            && phase >= ScenarioPhase.IOCSOCODecision && phase <= ScenarioPhase.SOCOTurnover
            && (phase != ScenarioPhase.SOCOTurnover || insideInteractionRadius);
        if (panel.activeSelf != visible)
        {
            HudFollowCamera.ConfigureShared(panel, this, PlayerUIGate.DialoguePriority);
            panel.SetActive(visible);
            if (visible) pointer.Enable(); else pointer.Disable();
        }
        if (!visible) return;
        bool decision = phase == ScenarioPhase.IOCSOCODecision;
        heading.text = GameDialogue.Text("ioc-role-label") + "\n" + GameDialogue.Text(decision ? "soco-decision-question"
            : phase == ScenarioPhase.SOCORequested ? "soco-await-arrival" : "soco-arrival-heading");
        firstRow.gameObject.SetActive(phase != ScenarioPhase.SOCORequested);
        secondRow.gameObject.SetActive(decision);
        firstChoice.text = (decision && selection == 0 ? "> " : "") + GameDialogue.Text(decision ? "soco-choice-request" : "soco-briefing-action");
        secondChoice.text = (selection == 1 ? "> " : "") + GameDialogue.Text("soco-choice-continue");
        controls.text = decision ? GameDialogue.Text("soco-choice-controls") : string.Empty;
        responsiveLayout.Refresh(false);
        if (layoutSelection != selection || layoutPhase != phase)
            responsiveLayout.EnsureVisible(selection == 0 ? firstRow : secondRow);
        layoutSelection = selection; layoutPhase = phase;
    }
    [EditableSceneBuilder]
    private void BuildPanel()
    {
        using (EditableSceneVisuals.Begin(this,"BuildPanel"))
        {
        panel = EditableSceneVisuals.Create("IOC_SOCO_Decision", typeof(RectTransform), typeof(Canvas), typeof(HudFollowCamera));
        panel.transform.SetParent(transform, false); panel.transform.localScale = Vector3.one * .00075f;
        var rect = (RectTransform)panel.transform; rect.sizeDelta = new Vector2(800, 380);
        var canvas = panel.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true; canvas.sortingOrder = short.MaxValue - 5;
        panel.GetComponent<HudFollowCamera>().Distance = 1f;
        panel.GetComponent<HudFollowCamera>().LocalOffset = new Vector3(0f, -.06f, 0f);
        var bg = EditableSceneVisuals.Create("Background", typeof(RectTransform), typeof(Image)); bg.transform.SetParent(panel.transform, false);
        var br = (RectTransform)bg.transform; br.anchorMin = Vector2.zero; br.anchorMax = Vector2.one; br.offsetMin = br.offsetMax = Vector2.zero;
        bg.GetComponent<Image>().color = new Color(.03f,.05f,.06f,.9f); bg.GetComponent<Image>().raycastTarget = false;
        heading = Label("Heading", new Vector2(0,95), new Vector2(740,140));
        firstChoice = Label("RequestOrBriefing", new Vector2(0,-25), new Vector2(740,65)); firstRow = firstChoice.rectTransform;
        secondChoice = Label("Continue", new Vector2(0,-105), new Vector2(740,65)); secondRow = secondChoice.rectTransform;
        controls = Label("Controls", new Vector2(0,-160), new Vector2(740,40)); controls.fontSize = 20;
        pointer = new WorldPanelPointer(rect, "SOCOFlow", this);
        responsiveLayout = ResponsivePanelLayout.Create(rect, heading, null, controls, 380, 740, 40);
        firstRow = responsiveLayout.AddText(firstChoice, responsiveLayout.Body, 65);
        secondRow = responsiveLayout.AddText(secondChoice, responsiveLayout.Body, 65);
        responsiveLayout.RegisterControls(pointer, "layout-decision-");
        pointer.Register("request-soco", firstRow, new Vector2(5,5), () => {
            if (SceneTurnover.Phase == ScenarioPhase.IOCSOCODecision) Choose(true); else BeginBriefing(); });
        pointer.Register("continue-without-soco", secondRow, new Vector2(5,5), () => Choose(false));
        panel.SetActive(false);
            }
    }
    private void OnDrawGizmosSelected()
    {
        if (iocGroundGuidanceMarker == null) return;
        Gizmos.color = new Color(1f, .8f, .05f, .85f);
        Gizmos.DrawWireSphere(iocGroundGuidanceMarker.transform.position, interactionRadius);
    }
    private TMP_Text Label(string name, Vector2 position, Vector2 size)
    {
        var go = EditableSceneVisuals.Create(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(panel.transform, false);
        var text = go.GetComponent<TextMeshProUGUI>(); text.fontSize = 30; text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
        text.rectTransform.sizeDelta = size; text.rectTransform.anchoredPosition = position;
        return text;
    }
}
