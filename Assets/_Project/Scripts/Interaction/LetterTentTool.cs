// Assets/_Project/Scripts/Interaction/LetterTentTool.cs
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Separately-equippable dispenser for the LETTER tent - split out of
/// EvidenceTentTool, which used to branch to a private PlaceLetterTent() for any
/// examinedInPlace target (the body, so far the only one) before ever touching its
/// own numbered pool. Same aim-raycast-and-trigger interaction as every other tool in
/// this project, including EvidenceTentTool's own numbered path - no new paradigm,
/// just a genuinely separate tool the player equips instead of an implicit branch
/// inside a different one.
///
/// Deliberately its OWN tool rather than a mode toggle on EvidenceTentTool: the two
/// dispensers mark fundamentally different kinds of claim (a numbered tent identifies
/// one of several candidate evidence items among decoys - see EvidenceTentTool's own
/// "DELIBERATELY IDENTICAL" comment on why that stays ambiguous; a letter tent marks
/// the one item examined where it lies, an entirely different procedural step with no
/// numbered pool and no identification ambiguity to protect), and this project's
/// existing convention is one PlayerTool per real physical prop the player equips, not
/// one tool silently doing two unrelated jobs based on what it happens to be aimed at.
///
/// Targets ONLY examinedInPlace items. Aiming at a numbered-tent item (or anything with
/// no EvidenceRecord at all - bare floor, a held tool, a non-evidence prop) refuses
/// with a clear message rather than silently doing nothing - the mirror image of
/// EvidenceTentTool's own numbered path now refusing an examinedInPlace target instead
/// of quietly re-branching into this tool's job.
///
/// Visuals: the real lettered tent models (Art/Environments/MODELS/Evidence Tents/
/// Evidence Tent A.glb, B.glb), wired in letterTentModels. Each letter tent placed takes
/// the next letter not yet used this session - the body (the only examinedInPlace item
/// today) gets A, a second such item would get B. letterTentStub/letterTentLabel remain
/// only as the fallback when no model is wired.
/// </summary>
public class LetterTentTool : PlayerTool
{
    [Header("Input (XRI Input Reader pattern)")]
    [SerializeField] private InputActionReference leftActivateAction;
    [SerializeField] private InputActionReference rightActivateAction;

    [Header("Raycast")]
    [SerializeField] private Transform placementOrigin;
    [SerializeField] private float maxDistance = 5f;

    [Header("Letter Tent (examinedInPlace items - the body, so far)")]
    [Tooltip("Template for the LETTER tent. STUB: no real letter-tent model exists yet, so this is expected to be an explicit placeholder (a primitive + a visible \"A\" label), not one of the numbered tent models reused unlabeled - see EvidenceDefinition.examinedInPlace. Kept SetActive(false) as a whole (root GameObject, not just its MeshRenderer disabled) - there is no held-preview toggle for this tool the way EvidenceTentTool's UpdateHeldVisual needs one, so a plain inactive template, reactivated whole on the placed clone, is simplest.")]
    [SerializeField] private GameObject letterTentStub;

    [Tooltip("Fallback label when letterTentModels is empty.")]
    [SerializeField] private string letterTentLabel = "A";

    [System.Serializable]
    public struct LetterTentModel
    {
        [Tooltip("The letter printed on this tent, e.g. \"A\".")]
        public string letter;
        [Tooltip("Inactive template of this letter's tent model (MeshRenderer disabled, like EvidenceTentTool's tentVisuals); cloned when placed.")]
        public GameObject visual;
    }

    [Tooltip("Lettered tent models in assignment order (A, B, ...). Each placement takes the first letter not used yet this session.")]
    [SerializeField] private LetterTentModel[] letterTentModels = new LetterTentModel[0];

    private readonly System.Collections.Generic.HashSet<string> usedLetters = new();
    [SerializeField] private float pickupRadius = .4f;
    public void ReclaimLetter(string letter) => usedLetters.Remove(letter);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    internal void PrepareDevelopmentMarker(string evidenceId)
    {
        SceneTestStart.RequirePreparation();
        string letter = NextLetter(out var template);
        var record = EvidenceStateManager.Instance.GetRecord(evidenceId);
        if (template == null || letter != record.tentLetter)
            throw new System.InvalidOperationException("Missing letter marker template for " + evidenceId);
        var zone = System.Linq.Enumerable.FirstOrDefault(EvidenceMarkerAcceptanceZone.All,z=>z.evidenceId==evidenceId);
        Vector3 point = zone != null && zone.placementPoint != null ? zone.placementPoint.position
            : EvidenceProp.FindById(evidenceId).transform.position + Vector3.right*.4f;
        var placed = Instantiate(template,point,ComputeStandingFacingRotation(point));
        placed.transform.localScale=template.transform.localScale; placed.SetActive(true);
        foreach(var renderer in placed.GetComponentsInChildren<MeshRenderer>(true)) renderer.enabled=true;
        var body=placed.GetComponent<MeshRenderer>();
        if(body!=null) placed.transform.position+=Vector3.up*(point.y-body.bounds.min.y);
        placed.AddComponent<EvidenceTentPickup>().InitializeLetter(this,pickupRadius,letter,evidenceId);
        usedLetters.Add(letter);
    }
#endif

    /// <summary>The letter (and template) the next letter tent will use; falls back to letterTentStub/letterTentLabel when no model is wired or all are used.</summary>
    public string NextLetter(out GameObject template)
    {
        if (letterTentModels != null)
        {
            foreach (var m in letterTentModels)
            {
                if (m.visual != null && !string.IsNullOrEmpty(m.letter) && !usedLetters.Contains(m.letter))
                {
                    template = m.visual;
                    return m.letter;
                }
            }
        }
        template = letterTentStub;
        return letterTentLabel;
    }

    public override ToolType ToolRole => ToolType.LetterTentMarker;

    private Camera povCamera;

    protected override void Awake()
    {
        base.Awake();

        if (placementOrigin == null)
        {
            placementOrigin = transform;
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
            return;
        }

        bool trigger =
            (leftActivateAction != null && leftActivateAction.action.WasPressedThisFrame()) ||
            (rightActivateAction != null && rightActivateAction.action.WasPressedThisFrame());

        if (trigger)
        {
            TryPlaceLetterTent();
        }
    }

    private bool TryGetPlacementHit(out RaycastHit hit)
    {
        return Physics.Raycast(placementOrigin.position, placementOrigin.forward, out hit, maxDistance, ~0, QueryTriggerInteraction.Ignore);
    }

    // Identical to EvidenceTentTool's own helper (no shared utility class exists in
    // this project for tent-placement rotation - each tool owns its own small
    // helpers, the same convention every other PlayerTool here already follows).
    // Tents always stand upright (world up), turning their labelled face toward
    // wherever the player's head is at the moment.
    private Quaternion ComputeStandingFacingRotation(Vector3 tentPosition)
    {
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

    /// <summary>
    /// The real, gated placement action. Refuses - with a message, never silently -
    /// for anything that isn't a registered examinedInPlace item: a numbered-tent
    /// item (this tool isn't the one that marks it), or nothing recognizable at all
    /// (bare floor, a held tool, a non-evidence prop - there is no "claim" ambiguity
    /// to protect here the way EvidenceTentTool's numbered path protects one, since
    /// there is only ever one examinedInPlace item and no decoy pool around it, so a
    /// clear refusal costs nothing). Public so GreyboxFlowTest (and this task's own
    /// live verification) can exercise this exact logic directly, the same
    /// discipline every other tool's TryX method in this project follows.
    /// </summary>
    public bool TryPlaceLetterTent()
    {
        if (!TryGetPlacementHit(out var hit))
        {
            return false;
        }

        // An examined-in-place item's acceptance zone wins over "nearest pivot": the body is
        // large, and other items' pivots lie inside its footprint (EvidenceMarkerAcceptanceZone).
        var zone = EvidenceMarkerAcceptanceZone.FindContaining(hit.point);
        var prop = zone != null ? EvidenceProp.FindById(zone.evidenceId) : null;
        if (prop == null)
        {
            prop = EvidenceProp.FindNearestWithinRadius(hit.point);
        }
        var record = prop != null && EvidenceStateManager.Instance != null
            ? EvidenceStateManager.Instance.GetRecord(prop.evidenceId)
            : null;

        if (record == null || record.definition == null || !record.definition.examinedInPlace)
        {
            string reason = record != null
                ? GameDialogue.Text("tent-use-numbered-tent")
                : GameDialogue.Text("tent-letter-no-target");
            Debug.Log($"[LetterTentTool] Refused: {reason}");
            NotificationManager.Notify(reason, DialogueKind.Error);
            return false;
        }

        string letter = NextLetter(out var template);
        if (template == null || EvidenceStateManager.Instance == null)
        {
            return false;
        }

        // Same refusal as EvidenceTentTool: the marker is next but a procedure rule (e.g.
        // overall scene photography first) doesn't allow it yet - say why, place nothing.
        // Same refusal as EvidenceTentTool: the marker is the next step (or the chalk outline
        // still owed before it) but isn't allowed yet - e.g. "Mark the evidence position with
        // chalk first." Refused before any tent is placed; one blocked action logged.
        var nextStep = EvidenceStateManager.GetNextRequiredStatus(record);
        if ((nextStep == EvidenceStatus.ChalkMarked || nextStep == EvidenceStatus.Marked) && ProceduralGateValidator.Instance != null
            && !ProceduralGateValidator.Instance.CanTransition(prop.evidenceId, EvidenceStatus.Marked))
        {
            string reason = ProceduralGateValidator.Instance.GetBlockReason(prop.evidenceId, EvidenceStatus.Marked);
            Debug.Log($"[LetterTentTool] Refused: {reason}");
            NotificationManager.Notify(reason, DialogueKind.Error);
            InteractionFeedback.Blocked();
            EvidenceStateManager.Instance.ReportProcedureBlocked(prop.evidenceId, EvidenceStatus.Marked);
            return false;
        }

        var result = EvidenceStateManager.Instance.MarkTentedWithLetter(prop.evidenceId, ToolRole, letter);
        if (result != TransitionResult.Applied)
        {
            return false;
        }
        usedLetters.Add(letter);

        // Stood at the zone's authored spot beside the item when there is one, not where the
        // ray landed (which may be on the body itself).
        Vector3 standAt = zone != null && zone.evidenceId == prop.evidenceId && zone.placementPoint != null ? zone.placementPoint.position : hit.point;
        var placed = Instantiate(template, standAt, ComputeStandingFacingRotation(standAt));
        placed.transform.localScale = template.transform.localScale;
        placed.SetActive(true);

        // Every template's renderers are disabled (the fallback stub has TWO - body and
        // label). GetComponentsInChildren (plural) re-enables all of them; using the singular form
        // here found only whichever renderer Unity happened to traverse first and
        // silently left the other invisible - confirmed live when this lived on
        // EvidenceTentTool, carried over here rather than re-introduced.
        foreach (var r in placed.GetComponentsInChildren<MeshRenderer>(true))
        {
            r.enabled = true;
        }

        // Base-vs-pivot correction, same reasoning as EvidenceTentTool's own numbered
        // path: the stub's mesh pivot sits at its vertical center, not its base, so
        // positioning the root directly at hit.point sinks the bottom half below the
        // floor. Uses the primitive body's own bounds specifically (not the combined
        // bounds including the floating label far above it), since the body is what
        // should sit flush with the floor.
        var bodyRenderer = placed.GetComponent<MeshRenderer>();
        if (bodyRenderer != null)
        {
            float worldBottomY = bodyRenderer.bounds.min.y;
            placed.transform.position += Vector3.up * (standAt.y - worldBottomY);
        }

        InteractionFeedback.Confirm(placed.transform);
        InteractionSfx.Play(InteractionSound.Place,placed.transform.position);
        placed.AddComponent<EvidenceTentPickup>().InitializeLetter(this, pickupRadius, letter, prop.evidenceId);
        GameDialogue.Say("tent-placed", letter);
        return true;
    }
}
