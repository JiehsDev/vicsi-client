using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Separate assessment snapshots. Uses the existing choice UI and JSONL log;
/// no evidence, board, conclusion or aggregate-score mutations.</summary>
public sealed class ReasoningCheckpointManager : MonoBehaviour
{
    public static ReasoningCheckpointManager Instance { get; private set; }
    public ReasoningCheckpointSet Definitions { get; private set; }
    private readonly Dictionary<string, double> eligible = new();
    private readonly Dictionary<string, ReasoningCheckpointRecord> answered = new();
    private readonly List<ReasoningCheckpointRecord> history = new();
    private readonly List<DialogueRequest> deferredDialogue = new();
    private HypothesisCheckpointUI ui;
    private VendorFollowupInterview vendor;
    private string activeId, sessionId;
    private Vector3 lastHead;
    private Quaternion lastRotation;
    private Vector3 previousHead;
    private Quaternion previousRotation;
    private float stationarySeconds;
    public IReadOnlyList<ReasoningCheckpointRecord> Records => history.AsReadOnly();
    private readonly HashSet<string> skippedForDevelopment = new();
    public bool IsAnswered(string id) => answered.ContainsKey(id) || skippedForDevelopment.Contains(id);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    internal void SkipEligibleForDevelopmentStart()
    {
        SceneTestStart.RequirePreparation();
        Evaluate();
        foreach (var id in eligible.Keys) skippedForDevelopment.Add(id);
        deferredDialogue.Clear();
    }
#endif
    public bool IsEligible(string id) => eligible.ContainsKey(id);
    public string ActiveId => activeId;
    public bool IsSystemReady => Definitions != null && ui != null && ui.isActiveAndEnabled && isActiveAndEnabled
        && HypothesisCheckpointManager.Instance != null && HypothesisCheckpointManager.Instance.isActiveAndEnabled;
    private ReasoningCheckpointDefinition NextPending => Definitions?.checkpoints
        .Where(c => IsEligible(c.id) && !IsAnswered(c.id)).OrderBy(c => eligible[c.id]).FirstOrDefault();
    public bool HasStandalonePending => NextPending != null && string.IsNullOrEmpty(NextPending.integratedHypothesisId);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Instance = null; SceneManager.sceneLoaded -= SceneLoaded; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    { SceneManager.sceneLoaded -= SceneLoaded; SceneManager.sceneLoaded += SceneLoaded; EnsureOwner(); }
    private static void SceneLoaded(Scene scene, LoadSceneMode mode) => EnsureOwner();
    private static void EnsureOwner()
    {
        if (Instance == null && EvidenceStateManager.Instance != null && STCSNotificationUI.Instance != null)
            new GameObject("Contextual Reasoning Checkpoints").AddComponent<ReasoningCheckpointManager>();
    }
    private void Awake()
    {
        Instance = this;
        Definitions = Resources.Load<ReasoningCheckpointSet>("ReasoningCheckpoints_LastDrink");
        if (Definitions != null && !Definitions.IsValid(out string problem))
        { Debug.LogError("[ReasoningCheckpoints] " + problem); Definitions = null; }
        ui = FindAnyObjectByType<HypothesisCheckpointUI>(FindObjectsInactive.Include);
        sessionId = SessionLogger.Instance?.SessionId;
        if (Definitions == null || ui == null) Debug.LogError("[ReasoningCheckpoints] Authored definitions or existing choice panel missing.");
    }
    private void OnEnable()
    {
        SceneTurnover.OnPhaseChanged += PhaseChanged;
        SceneTurnover.OnIOCObservationRecorded += Observation;
        FindingStateManager.OnFindingConfirmed += Finding;
    }
    private void Start()
    {
        vendor = VendorFollowupInterview.Instance;
        if (vendor != null) vendor.OnStatementDiscovered += Evaluate;
        Evaluate();
    }
    private void OnDisable()
    {
        SceneTurnover.OnPhaseChanged -= PhaseChanged;
        SceneTurnover.OnIOCObservationRecorded -= Observation;
        FindingStateManager.OnFindingConfirmed -= Finding;
        if (vendor != null) vendor.OnStatementDiscovered -= Evaluate;
    }
    private void OnDestroy() { if (Instance == this) Instance = null; }
    private void PhaseChanged(ScenarioPhase phase) => Evaluate();
    private void Observation(string id) => Evaluate();
    private void Finding(string id) => Evaluate();
    private double Seconds => SessionLogger.Instance != null ? SessionLogger.Instance.ElapsedSeconds : Time.timeSinceLevelLoad;

    public void Evaluate()
    {
        if (Definitions == null) return;
        foreach (var c in Definitions.checkpoints)
        {
            if (eligible.ContainsKey(c.id)) continue;
            bool ready = c.trigger switch
            {
                ReasoningTrigger.Form1Received => SceneTurnover.Form1Received,
                ReasoningTrigger.InitialSceneObserved => SceneTurnover.Form1Received && SceneTurnover.VictimObserved
                    && SceneTurnover.SceneSecurityAcknowledged && SceneTurnover.IOCObservations.Contains("ioc-possible-weapon")
                    && SceneTurnover.IOCObservations.Contains("ioc-bloodstain-area"),
                ReasoningTrigger.FindingConfirmed => RequirementEvaluator.IsSatisfied(new RequirementNode
                    { kind = RequirementNodeKind.Leaf, requirementType = RequirementType.FindingConfirmed, findingId = c.findingId }),
                ReasoningTrigger.VendorStatementDiscovered => SceneTurnover.Form1Received
                    && VendorFollowupInterview.Instance?.HasDiscovered(VendorFollowupInterview.StatementId) == true,
                _ => false
            };
            if (ready)
            {
                eligible.Add(c.id, Seconds);
                SessionLogger.Instance?.LogEvent(SessionEventType.ReasoningCheckpointEligible, c.id, new Dictionary<string,string>
                {
                    { "eligibleSeconds", Seconds.ToString("F3", CultureInfo.InvariantCulture) },
                    { "maximumWeight", "4" }, { "reasoningCategory", c.category }, { "contentVersion", Definitions.contentVersion }
                });
            }
        }
    }

    private void Update()
    {
        if (sessionId != SessionLogger.Instance?.SessionId)
        {
            sessionId = SessionLogger.Instance?.SessionId;
            eligible.Clear(); answered.Clear(); history.Clear(); deferredDialogue.Clear(); skippedForDevelopment.Clear(); activeId = null;
            Evaluate();
        }
        ReleaseDialogue();
        var head = Camera.main != null ? Camera.main.transform : null;
        if (head != null)
        {
            // Wait for translation/large turning to settle; small natural HMD motion is tolerated.
            // Compare with a settling anchor, not the previous frame: cumulative
            // slow locomotion must not count as stationary at high frame rates.
            float step = Vector3.Distance(previousHead, head.position);
            float turn = Quaternion.Angle(previousRotation, head.rotation);
            float dt = Mathf.Max(Time.unscaledDeltaTime, .001f);
            bool moving = step > .01f || step / dt > .25f || turn > 2f || turn / dt > 45f
                || Vector3.Distance(lastHead, head.position) > .04f || Quaternion.Angle(lastRotation, head.rotation) > 8f;
            stationarySeconds = moving ? 0f : stationarySeconds + Time.unscaledDeltaTime;
            if (moving) { lastHead = head.position; lastRotation = head.rotation; }
            previousHead = head.position; previousRotation = head.rotation;
        }
        if (activeId != null || !CanPresent()) return;
        var next = NextPending;
        if (next == null || !string.IsNullOrEmpty(next.integratedHypothesisId)) return;
        activeId = next.id;
        ui.ShowReasoning(next, response => { RecordResponse(next.id, response); activeId = null; });
    }

    public bool CanPresent()
    {
        if (SceneTestStart.IsPreparing) return false;
        if (ui == null || ui.IsShowing || PlayerUIGate.IsBlocked || stationarySeconds < .6f) return false;
        if (EndingManager.Instance?.AcceptedEnding != null || SceneTurnover.Phase == ScenarioPhase.InvestigationComplete) return false;
        if (VendorFollowupInterview.Instance?.IsTalking == true) return false;
        if (SceneTurnover.Phase == ScenarioPhase.SOCOTurnover || SceneTurnover.Phase == ScenarioPhase.SOCORequested) return false;
        if (HeldItemCoordinator.Instance != null && HeldItemCoordinator.Instance.CurrentCategory != HeldItemCoordinator.HeldCategory.None) return false;
        if (PlayerToolRegistry.VirtuallyEquippedRole != ToolType.None) return false;
        var dialogue = STCSNotificationUI.Instance;
        if (dialogue != null && (dialogue.IsShowing || dialogue.PendingCount > 0)) return false;
        return NotificationManager.Instance?.UI == null || string.IsNullOrEmpty(NotificationManager.Instance.UI.CurrentTooltip);
    }

    public ReasoningCheckpointDefinition IntegratedDefinition(string hypothesisId) => Definitions?.checkpoints.Find(c => c.integratedHypothesisId == hypothesisId);
    public bool IntegratedReady(string hypothesisId)
    { Evaluate(); var c = IntegratedDefinition(hypothesisId); return c != null && IsEligible(c.id); }

    public bool RecordResponse(string checkpointId, string responseId)
    {
        var c = Definitions?.checkpoints.Find(x => x.id == checkpointId);
        var response = c?.FindResponse(responseId);
        if (!IsEligible(checkpointId) || IsAnswered(checkpointId) || response == null || response.weight < 1 || response.weight > 4) return false;
        var record = new ReasoningCheckpointRecord(c.id, response.id, c.category, response.weight, history.Count + 1, eligible[c.id], Seconds);
        answered.Add(c.id, record); history.Add(record);
        SessionLogger.Instance?.LogEvent(SessionEventType.ReasoningCheckpointSubmitted, c.id, new Dictionary<string, string>
        {
            { "responseId", response.id }, { "weight", response.weight.ToString(CultureInfo.InvariantCulture) },
            { "maximumWeight", "4" }, { "reasoningCategory", c.category }, { "responseOrder", record.Order.ToString() },
            { "eligibleSeconds", record.EligibleSeconds.ToString("F3", CultureInfo.InvariantCulture) },
            { "answeredSeconds", record.AnsweredSeconds.ToString("F3", CultureInfo.InvariantCulture) },
            { "contentVersion", Definitions.contentVersion }
        });
        SessionLogger.Instance?.Flush();
        ReleaseDialogue();
        return true;
    }

    public bool HoldsTraining(string id) => Definitions != null && Definitions.checkpoints.Any(c => !IsAnswered(c.id) && c.deferredTrainingIds.Contains(id));
    public bool DeferDialogue(DialogueRequest request)
    {
        if (!isActiveAndEnabled || Definitions == null || !Definitions.checkpoints.Any(c => !IsAnswered(c.id) && c.deferredDialogueIds.Contains(request.id))) return false;
        if (!deferredDialogue.Any(r => r.id == request.id && r.sourceId == request.sourceId)) deferredDialogue.Add(request);
        return true;
    }
    private void ReleaseDialogue()
    {
        if (PlayerUIGate.IsBlocked || ui != null && ui.IsShowing) return;
        for (int i = 0; i < deferredDialogue.Count;)
        {
            var r = deferredDialogue[i];
            if (Definitions.checkpoints.Any(c => !IsAnswered(c.id) && c.deferredDialogueIds.Contains(r.id))) { i++; continue; }
            deferredDialogue.RemoveAt(i);
            GameDialogue.ShowResolved(r);
        }
    }
}
