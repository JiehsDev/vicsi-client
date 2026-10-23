// Assets/_Project/Scripts/Interaction/EvidenceTentTool.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Grabbable evidence-tent dispenser. Pressing Activate raycasts forward and, on a
/// hit, drops a numbered evidence tent prop at that point - like a real CSI marking
/// evidence at a scene. The active roster uses the numbers in EvidenceDisplay;
/// trace and spare tents use the lowest available number outside that reserved set.
/// The held prop and placement preview show the number selected for the aim target.
/// While aiming, a translucent "hologram" preview shows exactly where and how the
/// tent would land before the player commits to placing it.
///
/// Only as many tents can be out in the world at once as there are tent models
/// (tentVisuals.Length) - once every number is in use the dispenser holds nothing
/// until the player walks up to a placed tent and reclaims it (see
/// EvidenceTentPickup), which frees that number back up.
///
/// Numbered tents ONLY. An examinedInPlace item (the body, so far) is marked with a
/// LETTER tent instead, through the separate LetterTentTool - this class used to
/// branch into that placement itself (PlaceLetterTent), before the two were split
/// into genuinely separate equippable tools (see LetterTentTool's own class comment
/// for why). Aiming this dispenser at an examinedInPlace item now refuses with a
/// message rather than silently re-branching.
/// </summary>
public class EvidenceTentTool : PlayerTool
{
    // Placed tents can be picked back up (B) - see EvidenceTentPickup for the rule, which
    // is identical for a tent on evidence and a tent on anything else. While this was
    // false, a student who spent all tentVisuals.Length numbers on misplaced tents could
    // never mark real evidence again (EvidenceTentPickup.CompleteReclaim -> ReclaimSlot is
    // the only path that frees a number).
    private const bool TentPickupEnabled = true;


    [Header("Input (XRI Input Reader pattern)")]
    [SerializeField] private InputActionReference leftActivateAction;
    [SerializeField] private InputActionReference rightActivateAction;

    [Header("Raycast")]
    [SerializeField] private Transform placementOrigin;
    [SerializeField] private float maxDistance = 5f;

    [Header("Tents")]
    [Tooltip("One child object per tent number, in order (index 0 = tent \"1\"), each already sized/posed as the in-hand preview. The dispenser always offers the lowest-indexed entry not currently in use. Also caps how many tents can exist in the world at once - one per entry here.")]
    [SerializeField] private GameObject[] tentVisuals;

    [Header("Hologram Preview")]
    [SerializeField] private Color ghostColor = new Color(0.3f, 0.9f, 1f, 0.35f);

    [Header("Reclaiming")]
    [Tooltip("How close the player's head has to get to a placed tent for it to be reclaimed (freeing its number back up).")]
    [SerializeField] private float pickupRadius = 0.4f;

    [Header("Audio")]
    [Tooltip("Plays at the same unconditional point as InteractionFeedback.Confirm below - see that call site's own comment on why this must never differ by whether the mark was real evidence.")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip tentPlacingClip;

    public override ToolType ToolRole => ToolType.EvidenceMarker;

    /// <summary>True if some tent number is currently unused and can be placed next.</summary>
    public bool HasAvailableTent => FindPlacementIndex() >= 0;

    /// <summary>The tent number (1-based) that will be placed on the next Activate press. Only meaningful when HasAvailableTent is true.</summary>
    public int NextTentNumber => FindPlacementIndex() + 1;

    /// <summary>How many tent models exist - the hard cap on tents in the world at once.</summary>
    public int MaxPlacedTents => tentVisuals != null ? tentVisuals.Length : 0;

    /// <summary>How many tents are currently out in the world.</summary>
    public int PlacedCount
    {
        get
        {
            if (tentInUse == null)
            {
                return 0;
            }

            int count = 0;
            foreach (var inUse in tentInUse)
            {
                if (inUse)
                {
                    count++;
                }
            }
            return count;
        }
    }

    // Physical pool occupancy, indexed exactly like the authored tentVisuals.
    private bool[] tentInUse;
    private MeshFilter ghostMeshFilter;
    private MeshRenderer ghostMeshRenderer;
    private Transform ghostTransform;
    private Camera povCamera;

    protected override void Awake()
    {
        base.Awake();

        if (placementOrigin == null)
        {
            placementOrigin = transform;
        }

        tentInUse = new bool[tentVisuals != null ? tentVisuals.Length : 0];

        BuildGhost();
        UpdateHeldVisual();
    }

    private void BuildGhost()
    {
        var ghost = new GameObject("TentGhost", typeof(MeshFilter), typeof(MeshRenderer));
        ghostTransform = ghost.transform;
        ghostTransform.SetParent(null);
        ghostMeshFilter = ghost.GetComponent<MeshFilter>();
        ghostMeshRenderer = ghost.GetComponent<MeshRenderer>();

        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = ghostColor;
        ghostMeshRenderer.material = mat;
        ghostMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ghostMeshRenderer.receiveShadows = false;
        ghost.SetActive(false);
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

    private void OnDestroy()
    {
        if (ghostTransform != null)
        {
            Destroy(ghostTransform.gameObject);
        }
    }

    private void Update()
    {
        // Dialogue History (a pure review panel) is open - no tool/world input behind it.
        if (ToolInputLock.IsLocked)
        {
            return;
        }

        if (!IsHeld)
        {
            SetGhostVisible(false);
            return;
        }

        UpdateGhost();
        UpdateHeldVisual();

        bool trigger =
            (leftActivateAction != null && leftActivateAction.action.WasPressedThisFrame()) ||
            (rightActivateAction != null && rightActivateAction.action.WasPressedThisFrame());

        if (trigger)
        {
            PlaceTent();
        }
    }

    private bool TryGetPlacementHit(out RaycastHit hit)
    {
        return Physics.Raycast(placementOrigin.position, placementOrigin.forward, out hit, maxDistance, ~0, QueryTriggerInteraction.Ignore);
    }

    // Reserve the display numbers for the active roster. Non-evidence/extra/trace
    // placements use the remaining pool, so a floor tent cannot consume Knife's 1.
    internal int SelectPlacementIndex(EvidenceProp prop, EvidenceTraceMarkerTarget trace)
    {
        var state = EvidenceStateManager.Instance;
        var record = prop != null ? state?.GetRecord(prop.evidenceId) : null;
        int number = prop != null ? EvidenceDisplay.Number(prop.evidenceId) : 0;
        if (trace == null && number > 0 && record != null && !record.tentNumber.HasValue)
        {
            int index = number - 1;
            return tentInUse != null && index < tentInUse.Length && !tentInUse[index] ? index : -1;
        }
        bool mappedRoster = false;
        if (state != null)
            foreach (var item in state.AllRecords)
                if (EvidenceDisplay.Number(item.definition.evidenceId) > 0) { mappedRoster = true; break; }
        for (int i = mappedRoster ? 3 : 0; tentInUse != null && i < tentInUse.Length; i++)
            if (!tentInUse[i]) return i;
        return -1;
    }

    private int FindPlacementIndex()
    {
        if (!TryGetPlacementHit(out var hit)) return SelectPlacementIndex(null, null);
        var trace = EvidenceTraceMarkerTarget.FindForTent(hit.point);
        return SelectPlacementIndex(trace == null ? EvidenceProp.FindNearestWithinRadius(hit.point) : null, trace);
    }

    private void UpdateGhost()
    {
        int index = FindPlacementIndex();
        if (index < 0 || tentVisuals == null)
        {
            SetGhostVisible(false);
            return;
        }

        if (!TryGetPlacementHit(out var hit))
        {
            SetGhostVisible(false);
            return;
        }

        var source = tentVisuals[index];
        if (source == null)
        {
            SetGhostVisible(false);
            return;
        }

        var sourceMeshFilter = source.GetComponent<MeshFilter>();
        ghostMeshFilter.sharedMesh = sourceMeshFilter != null ? sourceMeshFilter.sharedMesh : null;
        ghostTransform.SetPositionAndRotation(hit.point, ComputeStandingFacingRotation(hit.point));
        ghostTransform.localScale = source.transform.localScale;

        // Active BEFORE reading bounds below - Renderer.bounds on a still-inactive
        // GameObject (the ghost starts SetActive(false) in BuildGhost, and stays
        // inactive between uses) is not reliable, so this can't be deferred to the
        // SetGhostVisible(true) call it used to end on.
        SetGhostVisible(true);

        // Same base-vs-pivot correction as PlaceTent, and for the same reason: the
        // hologram is supposed to show exactly where and how the tent would land, so
        // it needs to sit on the same corrected footing the placed tent now does,
        // not the old sunk-to-center position.
        if (ghostMeshFilter.sharedMesh != null)
        {
            float worldBottomY = ghostMeshRenderer.bounds.min.y;
            ghostTransform.position += Vector3.up * (hit.point.y - worldBottomY);
        }
    }

    private void SetGhostVisible(bool visible)
    {
        if (ghostTransform != null && ghostTransform.gameObject.activeSelf != visible)
        {
            ghostTransform.gameObject.SetActive(visible);
        }
    }

    // Tents always stand upright (world up), regardless of the surface hit
    // (floor, slope, low table...), and their numbered face turns toward
    // wherever the player's head is at the moment - like a real tent marker
    // placed to be read by whoever set it down.
    private Quaternion ComputeStandingFacingRotation(Vector3 tentPosition)
    {
        // povCamera resolves lazily and is cached once found, instead of trusting
        // Camera.main fresh every call - the OVR CenterEyeAnchor's "MainCamera" tag
        // isn't guaranteed active the instant this tool starts being used (same
        // reasoning as PhotographTool.EnsurePovCamera), and Camera.main silently
        // returning null would otherwise fall back to facing the aim direction
        // instead of the player - i.e. facing away from them, not toward them.
        if (povCamera == null)
        {
            povCamera = Camera.main;
        }

        Vector3 playerPos = povCamera != null ? povCamera.transform.position : placementOrigin.position - placementOrigin.forward;

        Vector3 facing = playerPos - tentPosition;
        facing.y = 0f;

        if (facing.sqrMagnitude < 0.0001f)
        {
            facing = -placementOrigin.forward;
            facing.y = 0f;
        }

        if (facing.sqrMagnitude < 0.0001f)
        {
            facing = Vector3.forward;
        }

        return Quaternion.LookRotation(facing.normalized, Vector3.up);
    }

    private void PlaceTent()
    {
        if (!TryGetPlacementHit(out var hit))
        {
            return;
        }

        // WHERE the tent lands is the raycast's job (hit.point, above). WHICH item it
        // marks is decided by proximity instead, because those are different
        // questions and the raycast only answered the first one well.
        //
        // This used to be hit.collider.GetComponentInParent<EvidenceProp>(), i.e.
        // the ray had to physically strike the evidence's own mesh. In VR that
        // turned a near-miss on a small object - the knife, the phone - into a
        // logged NonEvidenceMarked, so the data recorded a mis-identification the
        // student never made. Aim precision is not the variable this scenario is
        // trying to measure.
        // A second tent on an already-marked item's trace (the blood on the knife blade)
        // marks that trace; everything else resolves to the nearest evidence item as before.
        var trace = EvidenceTraceMarkerTarget.FindForTent(hit.point);
        var prop = trace != null ? null : EvidenceProp.FindNearestWithinRadius(hit.point);

        // Resolved and checked BEFORE anything about the numbered pool - an
        // examinedInPlace item (the body) is marked with a LETTER tent instead,
        // through the separate LetterTentTool (see that class's own comment for why
        // it's a genuinely separate tool rather than a branch here). Refusing here,
        // with a message, rather than silently placing a numbered tent on it or
        // quietly doing nothing: this check has to happen before the numbered
        // early-out below, or a full numbered pool would produce a misleading "no
        // tents available" refusal instead of the real reason.
        var record = prop != null && EvidenceStateManager.Instance != null
            ? EvidenceStateManager.Instance.GetRecord(prop.evidenceId)
            : null;

        if (record != null && record.definition != null && record.definition.examinedInPlace)
        {
            string reason = GameDialogue.Text("tent-use-letter-tent");
            Debug.Log($"[EvidenceTentTool] Refused: {reason}");
            NotificationManager.Notify(reason, DialogueKind.Error);
            return;
        }

        // An item whose procedure has a chalk step (EvidenceDefinition.requiresChalk) must
        // be outlined before its tent goes down. Refused before any tent is placed - a
        // procedural refusal, which may be obvious. Reachable only for an item already
        // photographed (the chalk step follows the raw photo), so it reveals nothing the
        // camera hadn't already.
        // The marker is this item's next procedural step (straight after the initial photo,
        // or after its chalk outline) but isn't allowed yet - chalk still owed, or a
        // procedure rule such as "overall scene photography first" (stepRequirements, read
        // through the central gate). Refused before any tent is placed; one blocked action
        // logged. Reachable only for an item already photographed, so it reveals nothing
        // the camera hadn't.
        var nextStep = record != null ? EvidenceStateManager.GetNextRequiredStatus(record) : null;
        if (record != null && ProceduralGateValidator.Instance != null
            && (nextStep == EvidenceStatus.ChalkMarked || nextStep == EvidenceStatus.Marked)
            && !ProceduralGateValidator.Instance.CanTransition(prop.evidenceId, EvidenceStatus.Marked))
        {
            string reason = ProceduralGateValidator.Instance.GetBlockReason(prop.evidenceId, EvidenceStatus.Marked);
            Debug.Log($"[EvidenceTentTool] Refused: {reason}");
            NotificationManager.Notify(reason, DialogueKind.Error);
            InteractionFeedback.Blocked();
            EvidenceStateManager.Instance.ReportProcedureBlocked(prop.evidenceId, EvidenceStatus.Marked);
            return;
        }

        int index = SelectPlacementIndex(prop, trace);
        if (index < 0 || tentVisuals == null)
        {
            return;
        }

        var source = tentVisuals[index];
        if (source != null)
        {
            string markedEvidenceId = trace != null ? RecordTracePlacement(trace, index) : RecordPlacement(prop, hit.collider, index);
            bool marksTrace = trace != null && markedEvidenceId != null;

            var placed = Instantiate(source, hit.point, ComputeStandingFacingRotation(hit.point));
            placed.transform.localScale = source.transform.localScale;
            placed.SetActive(true);

            // source's own MeshRenderer.enabled is false on every tentVisuals entry
            // (it only ever appears as the in-hand held preview, which is a separate
            // renderer built by BuildGhost - see the class comment) - Instantiate
            // copies that disabled state onto the placed clone verbatim, so without
            // this the placed tent is a real, functional GameObject that simply never
            // renders. Explicitly re-enabled here, scoped to the placed instance only;
            // the source templates are deliberately left untouched.
            var placedRenderer = placed.GetComponent<MeshRenderer>();
            if (placedRenderer != null)
            {
                placedRenderer.enabled = true;

                // source's mesh pivot sits at its own vertical CENTER, not its base
                // (Renderer.localBounds.center.y is ~0, not ~+halfHeight) - positioning
                // the root directly at hit.point therefore sank the bottom half of the
                // tent below the floor, leaving only the top half visible ("appearing
                // offset down, half appears"). Corrected using the real post-transform
                // world bounds rather than a hand-derived per-mesh offset, since each of
                // the 6 tent models is its own .glb and not guaranteed to share the same
                // proportions - this reads whatever bounds THIS instance actually has,
                // after its real position/rotation/scale are already applied above.
                float worldBottomY = placedRenderer.bounds.min.y;
                placed.transform.position += Vector3.up * (hit.point.y - worldBottomY);
            }

            if (TentPickupEnabled)
            {
                placed.AddComponent<EvidenceTentPickup>().Initialize(this, pickupRadius, index, markedEvidenceId, marksTrace);
            }
            tentInUse[index] = true;

            // DELIBERATELY IDENTICAL ON BOTH BRANCHES. DO NOT DIFFERENTIATE.
            //
            // Distinguishing a mark on real evidence from a mark on a decoy or on bare
            // floor - by tone, by haptic pattern, by wording, or by showing a toast in
            // one case and not the other - would reveal identification correctness in
            // real time and defeat the competency this scenario assesses. A player
            // could tent every object in the room and simply listen for which ones
            // sounded positive. That is the same hint-giving the free-placement design
            // has ruled out from the start, arriving through a side channel instead of
            // through a highlight or a restriction.
            //
            // Concretely: the cue is raised HERE, unconditionally, after
            // RecordPlacement has already returned - never from inside either branch -
            // and the toast names the tent number, never an evidence id. This is also
            // why FeedbackDirector explicitly skips EvidenceStatus.Marked and why
            // EvidenceNotifier has no Marked entry: if either of them owned this cue,
            // a mark on non-evidence would produce no status change and therefore no
            // feedback, and the silence itself would be the answer.
            //
            // The one signal that MAY differ is a procedural refusal (acting out of
            // order, reclaiming a marker off documented evidence). Protocol compliance
            // is a hard-gated, transparent requirement and should be obvious to the
            // player; whether an item was really evidence must stay hidden.
            InteractionFeedback.Confirm(placed.transform);
            if (audioSource != null && tentPlacingClip != null)
            {
                audioSource.PlayOneShot(tentPlacingClip);
            }
            GameDialogue.Say("tent-placed", index + 1);
        }

        UpdateHeldVisual();
    }

    /// <summary>
    /// Gives the placement its forensic meaning and returns the evidenceId the tent is
    /// now associated with, or null if it was placed on something that isn't evidence.
    ///
    /// Deliberately binary - evidence or not - with no third "marker on bare floor"
    /// category. Every legitimately markable object in the scenario is backed by a real
    /// EvidenceProp, including the designed distractor, so anything untagged is a
    /// mis-identification rather than a different kind of correct action.
    ///
    /// Placement itself stays unrestricted: a tent can be dropped anywhere, exactly as
    /// before. Tenting is the player's CLAIM that something is evidence, and a claim
    /// the scene refuses to let you make isn't a claim - the wrong ones have to be
    /// possible or there is nothing to score.
    ///
    /// The blank-evidenceId guard below is now belt-and-braces rather than the only
    /// defence: EvidenceProp never registers a blank-id prop in the first place, so
    /// the held tool models (camera, UV light, magnifier) cannot be returned by
    /// FindNearestWithinRadius at all. The guard stays because this method must remain
    /// correct for any caller, not just the one proximity search that feeds it today.
    /// </summary>
    private string RecordPlacement(EvidenceProp prop, Collider hitCollider, int index)
    {
        bool isEvidence = prop != null && !string.IsNullOrEmpty(prop.evidenceId);

        if (isEvidence)
        {
            // Relevance (Critical / Distractor / ...) is deliberately NOT consulted:
            // the distractor must mark exactly like any other item, or the scene would
            // be telling the player which items count.
            //
            // The tent number placed (index + 1, the same number shown on the in-hand
            // preview and on the placed prop itself) is recorded on the record so a
            // later step - the master sketch annotation - can label this item the same
            // way the player already sees it in the world.
            //
            // Only a tent that actually marked the item is tied to it. A second tent dropped
            // near an item already marked changes nothing - and picking it up must not
            // alter the item's real mark. Only a successful marking placement waits for recording.
            var result = EvidenceStateManager.Instance != null
                ? EvidenceStateManager.Instance.MarkTented(prop.evidenceId, ToolRole, index + 1)
                : TransitionResult.Violation;
            return result == TransitionResult.Applied ? prop.evidenceId : null;
        }

        // Nothing to transition - no record exists for untagged geometry - so this
        // event is the only trace that the mis-mark happened.
        if (SessionLogger.Instance != null)
        {
            string objectName = hitCollider != null ? hitCollider.gameObject.name : "(nothing)";
            SessionLogger.Instance.LogEvent(
                SessionEventType.NonEvidenceMarked,
                objectName,
                new Dictionary<string, string>
                {
                    { "objectName", objectName },
                    { "tentNumber", (index + 1).ToString() }
                });
        }

        return null;
    }

    /// <summary>Called by EvidenceTentPickup when the player reclaims a placed tent - frees that number back up.</summary>
    // The second tent on a trace (EvidenceTraceMarkerTarget). Returns the evidenceId the
    // trace belongs to when it was marked, null otherwise.
    private string RecordTracePlacement(EvidenceTraceMarkerTarget trace, int index)
    {
        var result = EvidenceStateManager.Instance != null
            ? EvidenceStateManager.Instance.MarkTraceTented(trace.evidenceId, ToolRole, index + 1)
            : TransitionResult.Violation;
        return result == TransitionResult.Applied ? trace.evidenceId : null;
    }

    public void ReclaimSlot(int tentIndex)
    {
        if (tentInUse == null || tentIndex < 0 || tentIndex >= tentInUse.Length)
        {
            return;
        }

        tentInUse[tentIndex] = false;
        UpdateHeldVisual();
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    internal void PrepareDevelopmentMarker(int number, string evidenceId, Vector3 point, bool trace = false)
    {
        SceneTestStart.RequirePreparation();
        int index = number - 1;
        if (tentVisuals == null || index < 0 || index >= tentVisuals.Length || tentVisuals[index] == null || tentInUse[index])
            throw new System.InvalidOperationException("No development tent template for marker " + number);
        if (Physics.Raycast(point + Vector3.up,Vector3.down,out var hit,3,~0,QueryTriggerInteraction.Ignore)) point = hit.point;
        var source = tentVisuals[index];
        var placed = Instantiate(source,point,ComputeStandingFacingRotation(point));
        placed.transform.localScale = source.transform.localScale; placed.SetActive(true);
        foreach(var renderer in placed.GetComponentsInChildren<MeshRenderer>(true)) renderer.enabled = true;
        var body = placed.GetComponent<MeshRenderer>();
        if (body != null) placed.transform.position += Vector3.up*(point.y-body.bounds.min.y);
        placed.AddComponent<EvidenceTentPickup>().Initialize(this,pickupRadius,index,evidenceId,trace);
        tentInUse[index] = true;
    }
#endif

    private void UpdateHeldVisual()
    {
        if (tentVisuals == null)
        {
            return;
        }

        int index = FindPlacementIndex();
        for (int i = 0; i < tentVisuals.Length; i++)
        {
            if (tentVisuals[i] != null)
            {
                tentVisuals[i].SetActive(i == index);
            }
        }
    }
}
