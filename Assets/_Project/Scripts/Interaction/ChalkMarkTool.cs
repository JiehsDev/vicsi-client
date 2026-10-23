// Assets/_Project/Scripts/Interaction/ChalkMarkTool.cs
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Grabbable chalk prop. The first continuous, real-time interaction in this project -
/// every other tool (tent, swab, bag, fingerprint kit, drag-and-drop report) is a
/// discrete single-trigger action. This one tracks motion over time: while the tool
/// is equipped and the trigger is held, the chalk tip's position is checked every
/// frame against the nearest active EvidenceChalkGuide's uncovered points (see that
/// class), building up real coverage as the player physically traces around the
/// item, rather than a single aim-and-click.
///
/// Deliberately distance-based, not physics-collision-based: a thin ring of trigger
/// colliders against a small, fast-moving tip is a classic VR tunneling risk (the tip
/// can pass through a collider between physics frames without ever registering an
/// overlap). A per-frame distance check from a known tip Transform has no such gap -
/// it samples the tip's actual current position every frame, physics timestep or not.
///
/// No order or direction enforcement, and progress is never reset by releasing the
/// trigger - both by design, see EvidenceChalkGuide's own class comment. The guide
/// itself owns coverage state and the completion threshold check; this tool's job is
/// only to feed it the tip's real-time position and report MarkChalkMarked the one
/// time a trace crosses that threshold.
/// </summary>
public class ChalkMarkTool : PlayerTool
{
    public static ChalkMarkTool Instance { get; private set; }

    [Header("Input (XRI Input Reader pattern)")]
    [SerializeField] private InputActionReference leftActivateAction;
    [SerializeField] private InputActionReference rightActivateAction;

    [Header("Tracing")]
    [Tooltip("The chalk's physical contact point - checked every frame against the nearest active guide's uncovered points while the trigger is held. Not a raycast origin: this tool no longer aims at a distance, it traces by proximity.")]
    [SerializeField] private Transform chalkTip;

    [Header("Chalk Guide")]
    [Tooltip("Authored template for the per-item chalk-tracing guide (see EvidenceChalkGuide's own class comment for the full spawn/configure flow) - EvidenceChalkGuide.SpawnFor clones this per evidence item and configures the clone's pre-built points/segments, rather than building a guide from scratch in code. Lives here (not on EvidenceChalkGuide itself) because SpawnFor is a static factory with no living instance of its own to hold an Inspector-assigned reference on, and a component ADDED at runtime via AddComponent starts with default field values regardless - ChalkMarkTool is the real, persistently-instantiated singleton this feature is already built around (Instance), so its own Inspector is where an authored reference like this can actually survive. MUST be assigned: if left blank, SpawnFor logs a clear warning and no guide can ever spawn for any item - the same silent-gap risk STCSManager.notificationUI sitting unassigned for weeks was a precedent for, so this is checked and warned explicitly rather than left to fail quietly.")]
    [SerializeField] private GameObject chalkGuideTemplate;

    [Header("Audio")]
    [Tooltip("Looping scratch sound - played while the tip is actively contacting a guide's uncovered points, stopped the moment contact lapses (trigger released, or too far from any guide). Not a one-shot: this is a continuous action, not a discrete click, matching this tool's own frame-by-frame tracing model.")]
    [SerializeField] private AudioSource chalkAudioSource;
    [SerializeField] private AudioClip chalkWritingClip;

    public override ToolType ToolRole => ToolType.ChalkMark;

    // See TryTraceAt: a blocked chalk step is reported once per trigger press.
    private bool chalkBlockReportedThisPress;

    /// <summary>The authored chalk-guide template, or null if unassigned. Read by EvidenceChalkGuide.SpawnFor - see this field's own tooltip for why it lives here.</summary>
    public GameObject ChalkGuideTemplate => chalkGuideTemplate;

    protected override void Awake()
    {
        base.Awake();
        Instance = this;

        if (chalkTip == null)
        {
            chalkTip = transform;
        }

        if (chalkGuideTemplate == null)
        {
            Debug.LogWarning("[ChalkMarkTool] chalkGuideTemplate is unassigned - no chalk guide will ever be able to spawn for any evidence item.", this);
        }

        if (chalkAudioSource != null)
        {
            chalkAudioSource.loop = true;
            chalkAudioSource.playOnAwake = false;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        leftActivateAction?.action.Enable();
        rightActivateAction?.action.Enable();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        leftActivateAction?.action.Disable();
        rightActivateAction?.action.Disable();
        SetWritingSoundPlaying(false);
    }

    private void Update()
    {
        // Dialogue History (a pure review panel) is open - no tool/world input behind it.
        if (ToolInputLock.IsLocked)
        {
            SetWritingSoundPlaying(false);
            return;
        }

        if (!IsHeld)
        {
            // Holstered/dropped mid-trace: stop the loop here too, or it would keep
            // playing forever with no Update reaching SetWritingSoundPlaying(false).
            SetWritingSoundPlaying(false);
            return;
        }

        // IsPressed(), not WasPressedThisFrame(): this is a continuous hold-to-trace
        // action, not a discrete click. Every frame the trigger is down counts.
        bool triggerHeld =
            (leftActivateAction != null && leftActivateAction.action.IsPressed()) ||
            (rightActivateAction != null && rightActivateAction.action.IsPressed());

        if (!triggerHeld)
        {
            chalkBlockReportedThisPress = false;
        }
        bool tracingNow = triggerHeld && TryTraceAt(chalkTip.position);
        SetWritingSoundPlaying(tracingNow && traceContact);

        bool pressedNow =
            (leftActivateAction != null && leftActivateAction.action.WasPressedThisFrame()) ||
            (rightActivateAction != null && rightActivateAction.action.WasPressedThisFrame());
        // Independent of tracingNow: TryTraceAt reports true whenever ANY guide is active
        // (it picks the nearest one at any distance), so gating on it would silence this
        // answer while another item's outline is in progress. It only answers within reach
        // of a no-chalk item, which has no guide of its own to conflict with.
        if (pressedNow)
        {
            TryRefuseUnneededChalk(chalkTip.position);
        }
    }

    /// <summary>
    /// Chalk used at an item whose procedure has no chalk step (EvidenceDefinition.
    /// requiresChalk = false): never changes its state and never counts as a procedure
    /// step - just a neutral answer. Only for items already photographed (where a guide
    /// would otherwise have appeared), so it reveals nothing the camera hadn't.
    /// Returns true if it answered. Public so tests drive exactly what a press does.
    /// </summary>
    public bool TryRefuseUnneededChalk(Vector3 tipWorldPosition)
    {
        var esm = EvidenceStateManager.Instance;
        if (esm == null)
        {
            return false;
        }

        foreach (var prop in EvidenceProp.All)
        {
            var record = prop != null ? esm.GetRecord(prop.evidenceId) : null;
            if (record?.definition == null || record.definition.requiresChalk
                || !esm.IsAtOrPastStatus(prop.evidenceId, EvidenceStatus.PhotographedRaw))
            {
                continue;
            }

            Vector3 offset = prop.transform.position - tipWorldPosition;
            offset.y = 0f;
            if (offset.magnitude <= record.definition.chalkCircleRadius + 0.25f)
            {
                GameDialogue.Say("chalk-not-required");
                return true;
            }
        }
        return false;
    }

    private void SetWritingSoundPlaying(bool playing)
    {
        if (chalkAudioSource == null || chalkWritingClip == null)
        {
            return;
        }

        if (playing && !chalkAudioSource.isPlaying)
        {
            chalkAudioSource.clip = chalkWritingClip;
            chalkAudioSource.Play();
        }
        else if (!playing && chalkAudioSource.isPlaying)
        {
            chalkAudioSource.Stop();
        }
    }

    /// <summary>
    /// The real, gated tracing action for a single frame's tip position: finds the
    /// nearest active chalk guide (regardless of which item it belongs to - only one
    /// guide is ever evaluated per call, so a stray pass near one item's circle can
    /// never credit a different item) and checks its uncovered points against
    /// tipWorldPosition. Reports MarkChalkMarked the one time this call causes that
    /// guide to cross its completion threshold. Public so GreyboxFlowTest can drive
    /// this exact method directly with real guide-point positions, the same way
    /// SwabTool.TrySwab is driven directly rather than simulated through Update.
    /// </summary>
    private bool traceContact;
    public bool TryTraceAt(Vector3 tipWorldPosition)
    {
        traceContact=false;
        var guide = EvidenceChalkGuide.FindByNearestPoint(tipWorldPosition);
        if (guide == null)
        {
            return false;
        }

        // Belt-and-braces: guides don't spawn for no-chalk items, but never let any trace
        // fake a ChalkMarked for one (it would also log a blocked transition).
        var guideDefinition = EvidenceStateManager.Instance?.GetRecord(guide.EvidenceId)?.definition;
        if (guideDefinition != null && !guideDefinition.requiresChalk)
        {
            return false;
        }

        // A procedure rule on the chalk step (e.g. overall scene photography first) refuses
        // it before any outline is drawn - read from the central gate's item rules. This
        // runs every frame the trigger is held, so it's reported once per press.
        var guideRecord = EvidenceStateManager.Instance?.GetRecord(guide.EvidenceId);
        if (guideRecord != null && guideRecord.status == EvidenceStatus.PhotographedRaw)
        {
            string blocked = EvidenceStateManager.Instance.GetItemBlockReason(guide.EvidenceId, EvidenceStatus.ChalkMarked);
            if (blocked != null)
            {
                if (!chalkBlockReportedThisPress)
                {
                    chalkBlockReportedThisPress = true;
                    NotificationManager.Notify(blocked, DialogueKind.Error);
                    InteractionFeedback.Blocked();
                    EvidenceStateManager.Instance.ReportProcedureBlocked(guide.EvidenceId, EvidenceStatus.ChalkMarked);
                }
                return false;
            }
        }

        var record = EvidenceStateManager.Instance != null
            ? EvidenceStateManager.Instance.GetRecord(guide.EvidenceId)
            : null;
        float threshold = record?.definition != null ? record.definition.chalkCompletionThreshold : 0.9f;

        traceContact=guide.IsTouching(tipWorldPosition);
        bool justCompleted = guide.CoverPointsNear(tipWorldPosition, threshold);
        if (justCompleted)
        {
            ReportEvidence(guide.EvidenceId, (id, tool) => EvidenceStateManager.Instance.MarkChalkMarked(id, tool), "chalk_marked");
        }

        return true;
    }
}
