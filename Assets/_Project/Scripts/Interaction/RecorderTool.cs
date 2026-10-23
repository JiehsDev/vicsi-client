// Assets/_Project/Scripts/Interaction/RecorderTool.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Grabbable dictaphone/logging prop for the Recorder role. Aim-and-activate like
/// PhotographTool; activating opens RecorderReportUI's drag-and-drop report panel for
/// whatever item is in frame, gated on PhotographedFinal - real report-writing rather
/// than an instant log, same "multiple choice, not free text" reasoning already
/// applied to hypothesis reasoning. Confirming the panel is what actually reports
/// EvidenceStatus.Logged (see HandleReportConfirmed), which is what opens the Evidence
/// Collector's gate via EvidenceStateManager.CheckReadyForCollection.
///
/// Falls back to the old plain instant log for any item with no authored
/// reportLineOptions - opening an empty, uncompletable panel would be a soft-lock, and
/// only EVD-014 has real content authored as of this task. Revisit once more items
/// have their own report-line sets.
/// </summary>
public class RecorderTool : PlayerTool
{
    public static RecorderTool Instance { get; private set; }

    [Header("Input (XRI Input Reader pattern)")]
    [SerializeField] private InputActionReference leftActivateAction;
    [SerializeField] private InputActionReference rightActivateAction;

    [Header("Raycast")]
    [SerializeField] private Transform aimOrigin;
    [SerializeField] private float maxDistance = 5f;

    [Header("Aim Pointer")]
    [SerializeField] private LineRenderer pointerLine;
    [SerializeField] private Color pointerColor = new Color(1f, 1f, 1f, 0.5f);
    [SerializeField] private float pointerWidth = 0.004f;

    [Header("Clipboard (left hand)")]
    [Tooltip("Separate object for the clipboard - not a PlayerTool itself, same pattern SwabTool.containerVisual already uses for its own left-hand prop. Reparented to the left hand alongside this tool's own right-hand equip (pen + trigger + raycast stay on the right), and back to its authored home position/parent on holster.")]
    [SerializeField] private Transform clipboardVisual;
    [SerializeField] private Vector3 clipboardEquipLocalPosition;
    [SerializeField] private Vector3 clipboardEquipLocalEulerAngles;

    private Transform clipboardHomeParent;
    private Vector3 clipboardHomeLocalPosition;
    private Quaternion clipboardHomeLocalRotation;

    private OVRCameraRig cameraRig;

    public override ToolType ToolRole => ToolType.Recorder;

    protected override void Awake()
    {
        base.Awake();
        Instance = this;
        if (aimOrigin == null)
        {
            aimOrigin = transform;
        }
        EnsurePointerLine();

        if (clipboardVisual != null)
        {
            clipboardHomeParent = clipboardVisual.parent;
            clipboardHomeLocalPosition = clipboardVisual.localPosition;
            clipboardHomeLocalRotation = clipboardVisual.localRotation;
            SetClipboardVisible(false);
        }
    }

    private void EnsureCameraRig()
    {
        if (cameraRig == null)
        {
            cameraRig = FindFirstObjectByType<OVRCameraRig>();
        }
    }

    /// <summary>Attaches the pen + trigger + raycast to whichever hand the wheel offers (right, per PlayerTool's default), and the clipboard to the left hand in the same gesture - one equip, both hands, mirroring SwabTool.EquipToHand exactly.</summary>
    public override void EquipToHand(Transform handAnchor)
    {
        base.EquipToHand(handAnchor);

        EnsureCameraRig();
        if (clipboardVisual != null && cameraRig != null && cameraRig.leftHandAnchor != null)
        {
            clipboardVisual.SetParent(cameraRig.leftHandAnchor, false);
            clipboardVisual.localPosition = clipboardEquipLocalPosition;
            clipboardVisual.localRotation = Quaternion.Euler(clipboardEquipLocalEulerAngles);
            SetClipboardVisible(true);
        }
    }

    public override void Holster()
    {
        if (clipboardVisual != null)
        {
            clipboardVisual.SetParent(clipboardHomeParent, false);
            clipboardVisual.localPosition = clipboardHomeLocalPosition;
            clipboardVisual.localRotation = clipboardHomeLocalRotation;
            SetClipboardVisible(false);
        }

        base.Holster();
    }

    private void SetClipboardVisible(bool visible)
    {
        if (clipboardVisual == null)
        {
            return;
        }

        foreach (var renderer in clipboardVisual.GetComponentsInChildren<Renderer>(true))
        {
            renderer.enabled = visible;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // Built at runtime rather than authored as a prefab child so every RecorderTool
    // gets the pointer for free. Its own enabled state is driven every frame in
    // Update() rather than by PlayerTool's equip/holster renderer toggle, since it's
    // created after base.Awake() already captured the tool's renderer list.
    private void EnsurePointerLine()
    {
        if (pointerLine == null)
        {
            var pointerObj = new GameObject("RecorderPointerLine");
            pointerObj.transform.SetParent(transform, false);
            pointerLine = pointerObj.AddComponent<LineRenderer>();
        }

        pointerLine.positionCount = 2;
        pointerLine.useWorldSpace = true;
        pointerLine.startWidth = pointerWidth;
        pointerLine.endWidth = pointerWidth;
        pointerLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        pointerLine.receiveShadows = false;
        if (pointerLine.sharedMaterial == null)
        {
            // Shader.Find can return null in a build if nothing else in the project
            // references this exact shader - Unity's shader stripping only keeps
            // variants an actual material uses, and this was the ONLY reference to
            // "Universal Render Pipeline/Unlit" anywhere in the project. That made
            // `new Material(null)` throw here, which aborted this Awake() call before
            // it returned - and since Awake() throwing means Unity never calls
            // OnEnable() afterward, PlayerToolRegistry.Register(this) (in the base
            // class's OnEnable) never ran, silently leaving this tool permanently
            // unregistered - the exact cause of a real "Recorder wedge greyed out
            // forever" bug that only ever showed up in an actual device build (the
            // Editor never strips shaders during Play Mode, so this could never
            // reproduce there). Guarded now so a missing shader degrades to "no
            // pointer line material" instead of aborting this tool's own startup.
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader != null)
            {
                pointerLine.material = new Material(shader) { color = pointerColor };
            }
            else
            {
                Debug.LogWarning("[RecorderTool] \"Universal Render Pipeline/Unlit\" shader not found (likely stripped from this build) - pointer line will use its default material.", this);
            }
        }
        pointerLine.enabled = false;
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
    }

    private void Update()
    {
        // Dialogue History (a pure review panel) is open - no tool/world input behind it.
        if (ToolInputLock.IsLocked)
        {
            return;
        }

        if (pointerLine != null)
        {
            pointerLine.enabled = IsHeld;
        }

        if (!IsHeld)
        {
            return;
        }

        UpdatePointerLine();

        bool trigger =
            (leftActivateAction != null && leftActivateAction.action.WasPressedThisFrame()) ||
            (rightActivateAction != null && rightActivateAction.action.WasPressedThisFrame());

        if (trigger)
        {
            TryLog();
        }
    }

    // Separate from TryLog's own raycast on purpose: this one runs every held frame
    // purely to draw the pointer, and must never be the thing that decides whether an
    // item gets logged.
    private void UpdatePointerLine()
    {
        if (pointerLine == null)
        {
            return;
        }

        Vector3 start = aimOrigin.position;
        Vector3 end = TryGetAimHit(out var hit, out _)
            ? hit.point
            : start + aimOrigin.forward * maxDistance;

        pointerLine.SetPosition(0, start);
        pointerLine.SetPosition(1, end);
    }

    private void TryLog()
    {
        if (TryGetAimHit(out _, out var evidence) && evidence != null)
            TryOpenReport(evidence);
    }

    private bool TryGetAimHit(out RaycastHit hit, out EvidenceProp evidence)
    {
        var ray = new Ray(aimOrigin.position, aimOrigin.forward);
        bool solidHit = Physics.Raycast(ray, out hit, maxDistance, ~0, QueryTriggerInteraction.Ignore);
        evidence = solidHit ? hit.collider.GetComponentInParent<EvidenceProp>() : null;
        // Solid evidence keeps its normal targeting. The body intentionally has no
        // solid collider, so resolve its existing acceptance box before the floor.
        if (evidence != null && !string.IsNullOrEmpty(evidence.evidenceId)) return true;
        float nearest = solidHit ? hit.distance : maxDistance;
        foreach (var zone in EvidenceMarkerAcceptanceZone.All)
        {
            if (zone == null || !zone.isActiveAndEnabled || !zone.Box.enabled) continue;
            var record = EvidenceStateManager.Instance?.GetRecord(zone.evidenceId);
            if (record?.definition?.examinedInPlace != true || record.bodyRemovalStatus == BodyRemovalStatus.Removed) continue;
            if (!zone.Box.Raycast(ray, out var bodyHit, nearest)) continue;
            var body = EvidenceProp.FindById(zone.evidenceId);
            if (body == null) continue;
            hit = bodyHit; evidence = body; nearest = bodyHit.distance;
        }
        return solidHit || evidence != null;
    }

    /// <summary>
    /// The real, gated open action: refuses via the same procedural gate every other
    /// tool uses (PhotographedFinal is Logged's only valid predecessor, so this gate
    /// check already means "gate on PhotographedFinal" without needing a separate
    /// status comparison - and, for an item with an on-scene swab/fingerprint/flip
    /// sub-step, also means those must already be done: CanTransition(id, Logged)
    /// delegates to EvidenceStateManager.IsBlockedBySwabbing/IsBlockedByOnSceneFingerprinting,
    /// both of which now gate exactly this transition - see their own comments), then
    /// opens RecorderReportUI for that item - or, for an item with no authored
    /// reportLineOptions yet, falls straight through to the old instant log so it
    /// isn't soft-locked. Public so GreyboxFlowTest can exercise this exact logic
    /// directly, the same discipline every other tool's TryX method in this project
    /// follows.
    /// </summary>
    public bool TryOpenReport(EvidenceProp evidence)
    {
        if (SceneTurnover.IsInvestigationBlocked) return false;
        if (evidence == null || string.IsNullOrEmpty(evidence.evidenceId))
        {
            return false;
        }
        if(EvidenceStateManager.Instance?.GetRecord(evidence.evidenceId)?.recorderDocumentation!=null)return false;

        if (ProceduralGateValidator.Instance != null && !ProceduralGateValidator.Instance.CanTransition(evidence.evidenceId, EvidenceStatus.Logged))
        {
            string reason = ProceduralGateValidator.Instance.GetBlockReason(evidence.evidenceId, EvidenceStatus.Logged);
            Debug.Log($"[RecorderTool] Can't mark {evidence.evidenceId} Logged: {reason}");
            NotificationManager.Notify(reason, DialogueKind.Error);
            // A refusal by the procedural gate. Distinguishable from a confirmed action
            // on purpose - the game declined, which is not the same as the player
            // having picked the wrong item.
            InteractionFeedback.Blocked();
            return false;
        }

        var record = EvidenceStateManager.Instance?.GetRecord(evidence.evidenceId);
        var definition = record?.definition;
        bool hasReportLines = definition != null && definition.reportLineOptions != null && definition.reportLineOptions.Count > 0;

        if (!hasReportLines || RecorderReportUI.Instance == null)
        {
            // No authored report content for this item yet (or no panel in the
            // scene at all) - the plain instant log, unchanged from before this
            // task, rather than a panel with nothing to drag.
            ReportEvidence(evidence.evidenceId, (id, tool) => EvidenceStateManager.Instance.MarkLogged(id, tool), "logged");
            return true;
        }

        RecorderReportUI.Instance.Show(evidence, HandleReportConfirmed);
        return true;
    }

    /// <summary>
    /// Called by RecorderReportUI.Confirm() once the player has enough lines in the
    /// Notes zone. Re-checks the gate (state could in principle have changed between
    /// opening the panel and confirming it, same discipline every other tool's
    /// confirm-time check follows) before actually reporting Logged, then logs which
    /// lines were selected as their own session event - real per-decision data, same
    /// granularity HypothesisSubmitted captures for a checkpoint answer.
    /// </summary>
    private TransitionResult HandleReportConfirmed(string evidenceId, List<string> selectedLineIds)
    {
        if (selectedLineIds == null || selectedLineIds.Count != RecorderReportUI.RequiredLineCount
            || new HashSet<string>(selectedLineIds).Count != RecorderReportUI.RequiredLineCount)
            return TransitionResult.Violation;
        if(RecorderDocumentation.AppliesTo(evidenceId))
        {
            if(!RecorderDocumentation.ValidSelection(evidenceId,selectedLineIds))return TransitionResult.Violation;
            var state=EvidenceStateManager.Instance;var record=state?.GetRecord(evidenceId);
            if(record==null)return TransitionResult.Violation;
            if(record.recorderDocumentation!=null)return TransitionResult.Duplicate;
            if(!state.CanAdvanceTo(evidenceId,EvidenceStatus.Logged)
                ||ProceduralGateValidator.Instance!=null&&!ProceduralGateValidator.Instance.CanTransition(evidenceId,EvidenceStatus.Logged))return TransitionResult.Violation;
            var snapshot=RecorderDocumentation.Capture(evidenceId,selectedLineIds);
            record.recorderDocumentation=snapshot;
            var result=state.MarkLogged(evidenceId,ToolType.Recorder);
            if(result!=TransitionResult.Applied){record.recorderDocumentation=null;return result;}
            state.RecordReportLinesSelected(evidenceId,selectedLineIds);
            SessionLogger.Instance?.LogEvent(SessionEventType.ReportLinesSelected,evidenceId,new Dictionary<string,string>{
                {"lineIds",string.Join(",",selectedLineIds)},{"lineCount","3"},{"documentationVersion",snapshot.version.ToString()},{"documentation",JsonUtility.ToJson(snapshot)}});
            if(!record.definition.requiresBodyRemoval)FireEvidenceSTCSTrigger(evidenceId,"logged");
            return TransitionResult.Applied;
        }

        if (ProceduralGateValidator.Instance != null && !ProceduralGateValidator.Instance.CanTransition(evidenceId, EvidenceStatus.Logged))
        {
            string reason = ProceduralGateValidator.Instance.GetBlockReason(evidenceId, EvidenceStatus.Logged);
            Debug.Log($"[RecorderTool] Can't mark {evidenceId} Logged: {reason}");
            NotificationManager.Notify(reason, DialogueKind.Error);
            InteractionFeedback.Blocked();
            return TransitionResult.Violation;
        }

        var bodyRecord = EvidenceStateManager.Instance?.GetRecord(evidenceId);
        if (bodyRecord?.definition?.requiresBodyRemoval == true)
        {
            if (!selectedLineIds.Contains("body-a-location") || !selectedLineIds.Contains("body-a-position")
                || !selectedLineIds.Contains("body-a-wound-sharp-object"))
            {
                NotificationManager.Notify("Complete the required BODY-A report lines before confirming the recording.", DialogueKind.Guidance);
                return TransitionResult.Violation;
            }
            EvidenceStateManager.Instance.RecordReportLinesSelected(evidenceId, selectedLineIds);
            var result = EvidenceStateManager.Instance.MarkLogged(evidenceId, ToolType.Recorder);
            if (result != TransitionResult.Applied) return result;
        }
        else ReportEvidence(evidenceId, (id, tool) => EvidenceStateManager.Instance.MarkLogged(id, tool), "logged");

        // Recorded BEFORE the session-log call below (though order between the two
        // doesn't itself matter) so the cross-item unlock registry reflects this
        // confirm the moment it happens - see SelectionPrerequisite/
        // EvidenceStateManager.HasSelectedReportLine.
        EvidenceStateManager.Instance?.RecordReportLinesSelected(evidenceId, selectedLineIds);

        SessionLogger.Instance?.LogEvent(SessionEventType.ReportLinesSelected, evidenceId, new Dictionary<string, string>
        {
            { "lineIds", string.Join(",", selectedLineIds) },
            { "lineCount", selectedLineIds.Count.ToString() }
        });

        return TransitionResult.Applied;
    }
}
