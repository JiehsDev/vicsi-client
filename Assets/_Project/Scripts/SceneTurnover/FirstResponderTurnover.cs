// Assets/_Project/Scripts/SceneTurnover/FirstResponderTurnover.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The First Responder NPC hands the scene to IOC after the conversation.
/// SceneTurnover owns the later IOC/SOCO progression and forensic access. Lives on the NPC
/// root; the model under it ("Visual", the PNP_NPC character) is presentation only - nothing here
/// reads it, so it can be disabled or swapped for another character without touching logic.
///
/// Flow: while the player's head is within interactionRadius of the InteractionZone (and no
/// panel/modal holds PlayerUIGate - e.g. the student-identification lock), the catalog
/// prompt shows on the tooltip panel; A (or the trigger) starts the conversation. Its
/// catalog lines go to the dialogue panel like any other dialogue - queued, skippable with
/// B, written once to Dialogue History. Turnover completes when the last of those lines has
/// left the panel (shown in full or skipped), once per session.
/// </summary>
[DefaultExecutionOrder(-1000)]   // registers the gate before any other component's Awake/OnEnable reads it
public class FirstResponderTurnover : MonoBehaviour
{
    public const string ConversationSourceId = "first-responder-turnover";
    private const string HistoryCategory = "First Responder";

    [Header("Dialogue (ViCSIDialogueCatalog ids)")]
    [Tooltip("Played in order when the player talks to the First Responder. Catalog section STARTUP / FIRST RESPONDER.")]
    [SerializeField] private string[] dialogueLineIds = { "FR_TURNOVER_01", "FR_TURNOVER_02", "FR_TURNOVER_03", "FR_TURNOVER_04" };

    [Tooltip("Catalog tooltip shown while the player is in range.")]
    [SerializeField] private string promptId = "prompt-talk-first-responder";

    [Header("Interaction")]
    [Tooltip("Where interaction distance is measured from. Defaults to this transform.")]
    [SerializeField] private Transform interactionZone;

    [Tooltip("Horizontal distance (m) from the InteractionZone within which the prompt shows and A talks.")]
    [SerializeField, Min(0.25f)] private float interactionRadius = 2f;

    [Tooltip("Optional anchor for a future world-space prompt above the NPC. Unused by the current tooltip-panel prompt.")]
    [SerializeField] private Transform interactionPromptAnchor;

    [Tooltip("The NPC turns (yaw only) to face this on Start - typically the player's arrival point. Blank = keep the authored rotation.")]
    [SerializeField] private Transform facingTarget;

    [Header("Objectives")]
    [Tooltip("Objective that completes on turnover (RequirementType.SceneTurnoverCompleted). Informational - used by validation/tests.")]
    [SerializeField] private string turnoverObjectiveId = "obj-talk-first-responder";

    [Tooltip("Objective the player is given once the scene is turned over. Informational - used by validation/tests.")]
    [SerializeField] private string investigationObjectiveId = "obj-ioc-initial-assessment";

    [Header("Wiring")]
    [Tooltip("Dialogue panel the conversation is posted to. Blank = STCSNotificationUI.Instance.")]
    [SerializeField] private STCSNotificationUI dialoguePanel;

    [Tooltip("Warn after this many seconds if the briefing is still pending; never complete turnover with queued lines.")]
    [SerializeField, Min(5f)] private float conversationTimeoutSeconds = 90f;

    private InputAction interactAction;
    private InputAction triggerAction;
    private bool promptShown;
    private bool talking;
    private Coroutine conversation;

    public string[] DialogueLineIds => dialogueLineIds;
    public string PromptId => promptId;
    public float InteractionRadius => interactionRadius;
    public string TurnoverObjectiveId => turnoverObjectiveId;
    public string InvestigationObjectiveId => investigationObjectiveId;
    public Transform InteractionPromptAnchor => interactionPromptAnchor;

    /// <summary>True while the turnover conversation is in progress.</summary>
    public bool IsTalking => talking;

    /// <summary>True while the in-range prompt is up.</summary>
    public bool IsPromptShown => promptShown;

    private void Awake()
    {
        // Keep scene-authored case lines, inserting PPE teaching before the existing Form 1 handover.
        var arrivalLines = new List<string>(dialogueLineIds ?? new string[0]);
        int handover = arrivalLines.IndexOf("FR_TURNOVER_04");
        if (handover < 0) handover = arrivalLines.Count;
        foreach (var id in new[] { "FR_PPE_01", "FR_PPE_02", "FR_SECURITY_01" })
            if (!arrivalLines.Contains(id)) arrivalLines.Insert(handover++, id);
        dialogueLineIds = arrivalLines.ToArray();
        SceneTurnover.Register(this);
        // SOCO tools stay inert through IOC; locomotion is unaffected.
        // The phase-derived ToolInputLock gate keeps SOCO tools inert throughout IOC.
        // Same right-hand A every other world interaction in the project uses; trigger as the alternative.
        interactAction = PlayerUIGate.CreateConfirmAction(this, "FirstResponder_Interact", () => SceneTurnover.IsPending && !talking && !PlayerUIGate.IsBlocked && PlayerInRange(), 200);
        triggerAction = new InputAction("FirstResponder_Trigger", InputActionType.Button, "<XRController>{RightHand}/triggerPressed");
    }

    private void Start()
    {
        if (facingTarget != null)
        {
            Vector3 to = facingTarget.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);
        }
    }

    private void OnEnable()
    {
        interactAction.Enable();
        triggerAction.Enable();
    }

    private void OnDisable()
    {
        interactAction.Disable();
        triggerAction.Disable();
        HidePrompt();
    }

    private void OnDestroy()
    {
        // No independent turnover lock: access derives from the authoritative phase.
        SceneTurnover.Unregister(this);
        interactAction?.Dispose();
        triggerAction?.Dispose();
    }

    private void Update()
    {
        if (SceneTurnover.IsComplete || talking || PlayerUIGate.IsBlocked || !PlayerInRange())
        {
            HidePrompt();
            return;
        }

        // Re-asserted when another system withdrew the shared prompt slot while we're still in range.
        var tooltipPanel = NotificationManager.Instance != null ? NotificationManager.Instance.UI : null;
        if (!promptShown || (tooltipPanel != null && !tooltipPanel.HasAmbientPrompt))
        {
            NotificationManager.ShowPrompt(GameDialogue.Text(promptId));
            promptShown = true;
        }

        if (PlayerUIGate.TryConsumeConfirm(this, interactAction) || triggerAction.WasPressedThisFrame())
        {
            Interact();
        }
    }

    /// <summary>Horizontal distance from the player's head to the InteractionZone is within interactionRadius.</summary>
    public bool PlayerInRange()
    {
        var head = Camera.main;
        if (head == null) return false;
        Vector3 zone = (interactionZone != null ? interactionZone : transform).position;
        Vector3 d = head.transform.position - zone;
        d.y = 0f;
        return d.magnitude <= interactionRadius;
    }

    /// <summary>
    /// Starts the turnover conversation (what A does in range). Returns false when turnover is
    /// already complete or the conversation is already running - the lines never replay.
    /// </summary>
    public bool Interact()
    {
        if (!SceneTurnover.IsPending || talking || PlayerUIGate.IsBlocked || !PlayerInRange()) return false;
        var panel = Panel();
        if (panel == null || dialogueLineIds == null || dialogueLineIds.Length == 0
            || System.Array.Exists(dialogueLineIds, id => string.IsNullOrEmpty(id) || ViCSIDialogueCatalog.Current.Find(id) == null))
        {
            Debug.LogWarning("[FirstResponderTurnover] Briefing panel or catalog lines unavailable; turnover remains pending.", this);
            return false;
        }
        talking = true;
        // The prompt must be down while the lines play: B only skips dialogue when no ambient prompt is up.
        HidePrompt();
        SessionLogger.Instance?.LogEvent(SessionEventType.FirstResponderInteractionStarted, ConversationSourceId, new Dictionary<string, string>
        {
            { "lineCount", (dialogueLineIds != null ? dialogueLineIds.Length : 0).ToString() }
        });

        if (dialogueLineIds != null)
        {
            foreach (var id in dialogueLineIds)
            {
                if (!string.IsNullOrEmpty(id)) GameDialogue.SayLine(id, HistoryCategory, ConversationSourceId, panel);
            }
        }
        conversation = StartCoroutine(WaitForConversationThenTurnOver());
        return true;
    }

    private IEnumerator WaitForConversationThenTurnOver()
    {
        yield return null;   // lines posted this frame reach the panel's queue
        float declaredDuration = 0;
        foreach (var id in dialogueLineIds)
        {
            var entry = ViCSIDialogueCatalog.Current.Find(id);
            if (entry != null) declaredDuration += Mathf.Max(0, entry.duration);
        }
        double warningAt = Time.realtimeSinceStartupAsDouble + Mathf.Max(conversationTimeoutSeconds, declaredDuration + 15);
        bool warned = false;
        while (Panel() != null && Panel().HasLinesFrom(ConversationSourceId))
        {
            if (!warned && Time.realtimeSinceStartupAsDouble >= warningAt)
            {
                warned = true;
                Debug.LogWarning("[FirstResponderTurnover] Briefing is still pending; waiting before accepting turnover.", this);
            }
            yield return null;
        }
        if (Panel() == null)
        {
            talking = false;
            conversation = null;
            yield break;
        }
        conversation = null;
        CompleteTurnover("Conversation");
    }

    /// <summary>
    /// Dev/test bypass: turns the scene over immediately without the conversation (used by
    /// diagnostics and the future IOC flow; this does not start SOCO processing). No-op once complete.
    /// </summary>
    public void CompleteWithoutDialogue()
    {
        if (conversation != null) { StopCoroutine(conversation); conversation = null; }
        CompleteTurnover("Bypass");
    }

    private void CompleteTurnover(string via)
    {
        talking = false;
        if (!SceneTurnover.IsPending) return;
        // No independent turnover lock: access derives from the authoritative phase.
        HidePrompt();
        // Logged before the completion event fans out, so it precedes the ObjectiveCompleted/
        // ObjectiveUnlocked entries it causes. Not scored - informational only.
        SessionLogger.Instance?.LogEvent(SessionEventType.CSIForm1Received, "CSI-Form-1", new Dictionary<string, string>
        {
            { "receivingRole", "IOC" },
            { "via", via }
        });
        SessionLogger.Instance?.LogEvent(SessionEventType.SceneTurnoverCompleted, ConversationSourceId, new Dictionary<string, string>
        {
            { "via", via },
            { "sessionTimeSeconds", SessionLogger.Instance.ElapsedSeconds.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) }
        });
        if (!SceneTurnover.TryAdvance(ScenarioPhase.IOCAssessment)) return;
        GameDialogue.Say("csi-form-1-received");
    }

    private STCSNotificationUI Panel() => dialoguePanel != null ? dialoguePanel : STCSNotificationUI.Instance;

    private void HidePrompt()
    {
        if (!promptShown) return;
        promptShown = false;
        NotificationManager.HidePrompt();
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.5f, 1f, 0.6f);
        Vector3 c = (interactionZone != null ? interactionZone : transform).position;
        const int n = 32;
        for (int i = 0; i < n; i++)
        {
            float a0 = i * Mathf.PI * 2f / n, a1 = (i + 1) * Mathf.PI * 2f / n;
            Gizmos.DrawLine(c + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * interactionRadius,
                            c + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * interactionRadius);
        }
    }
#endif
}
