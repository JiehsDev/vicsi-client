// Assets/_Project/Scripts/Interaction/EvidenceTentTool.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Grabbable evidence-tent dispenser. Pressing Activate raycasts forward and, on a
/// hit, drops a numbered evidence tent prop at that point - like a real CSI marking
/// evidence 1, 2, 3... at a scene. The dispenser always offers the LOWEST currently
/// unused number, not just "the next one in sequence" - place 1, 2, 3, then reclaim
/// 2, and the dispenser goes back to offering 2 (not 4), because that's the lowest
/// number not currently out in the world. The prop currently held in-hand always
/// shows that number, so the player can see at a glance what they're about to drop.
/// While aiming, a translucent "hologram" preview shows exactly where and how the
/// tent would land before the player commits to placing it.
///
/// Only as many tents can be out in the world at once as there are tent models
/// (tentVisuals.Length) - once every number is in use the dispenser holds nothing
/// until the player walks up to a placed tent and reclaims it (see
/// EvidenceTentPickup), which frees that number back up.
/// </summary>
public class EvidenceTentTool : PlayerTool
{
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

    public override ToolType ToolRole => ToolType.EvidenceMarker;

    /// <summary>True if some tent number is currently unused and can be placed next.</summary>
    public bool HasAvailableTent => FindLowestAvailableIndex() >= 0;

    /// <summary>The tent number (1-based) that will be placed on the next Activate press. Only meaningful when HasAvailableTent is true.</summary>
    public int NextTentNumber => FindLowestAvailableIndex() + 1;

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

    // Which numbers are currently out in the world, indexed the same as
    // tentVisuals - the source of truth for "lowest available number", instead
    // of a simple round-robin counter that would ignore reclaimed numbers.
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
        if (!IsHeld)
        {
            SetGhostVisible(false);
            return;
        }

        UpdateGhost();

        bool trigger =
            (leftActivateAction != null && leftActivateAction.action.WasPressedThisFrame()) ||
            (rightActivateAction != null && rightActivateAction.action.WasPressedThisFrame());

        if (trigger)
        {
            PlaceTent();
        }
    }

    private int FindLowestAvailableIndex()
    {
        if (tentInUse == null)
        {
            return -1;
        }

        for (int i = 0; i < tentInUse.Length; i++)
        {
            if (!tentInUse[i])
            {
                return i;
            }
        }
        return -1;
    }

    private bool TryGetPlacementHit(out RaycastHit hit)
    {
        return Physics.Raycast(placementOrigin.position, placementOrigin.forward, out hit, maxDistance, ~0, QueryTriggerInteraction.Ignore);
    }

    private void UpdateGhost()
    {
        int index = FindLowestAvailableIndex();
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
        SetGhostVisible(true);
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
        int index = FindLowestAvailableIndex();
        if (index < 0 || tentVisuals == null)
        {
            return;
        }

        if (!TryGetPlacementHit(out var hit))
        {
            return;
        }

        var source = tentVisuals[index];
        if (source != null)
        {
            // WHERE the tent lands is the raycast's job (hit.point, above). WHICH item
            // it marks is decided by proximity instead, because those are different
            // questions and the raycast only answered the first one well.
            //
            // This used to be hit.collider.GetComponentInParent<EvidenceProp>(), i.e.
            // the ray had to physically strike the evidence's own mesh. In VR that
            // turned a near-miss on a small object - the knife, the phone - into a
            // logged NonEvidenceMarked, so the data recorded a mis-identification the
            // student never made. Aim precision is not the variable this scenario is
            // trying to measure.
            var prop = EvidenceProp.FindNearestWithinRadius(hit.point);
            string markedEvidenceId = RecordPlacement(prop, hit.collider);

            var placed = Instantiate(source, hit.point, ComputeStandingFacingRotation(hit.point));
            placed.transform.localScale = source.transform.localScale;
            placed.SetActive(true);
            placed.AddComponent<EvidenceTentPickup>().Initialize(this, pickupRadius, index, markedEvidenceId);
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
            NotificationManager.Notify($"Evidence tent {index + 1} placed.");
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
    private string RecordPlacement(EvidenceProp prop, Collider hitCollider)
    {
        bool isEvidence = prop != null && !string.IsNullOrEmpty(prop.evidenceId);

        if (isEvidence)
        {
            // Relevance (Critical / Distractor / ...) is deliberately NOT consulted:
            // the distractor must mark exactly like any other item, or the scene would
            // be telling the player which items count.
            EvidenceStateManager.Instance?.MarkTented(prop.evidenceId, ToolRole);
            return prop.evidenceId;
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
                    { "tentNumber", NextTentNumber.ToString() }
                });
        }

        return null;
    }

    /// <summary>Called by EvidenceTentPickup when the player reclaims a placed tent - frees that number back up.</summary>
    public void ReclaimSlot(int tentIndex)
    {
        if (tentInUse == null || tentIndex < 0 || tentIndex >= tentInUse.Length)
        {
            return;
        }

        tentInUse[tentIndex] = false;
        UpdateHeldVisual();
    }

    private void UpdateHeldVisual()
    {
        if (tentVisuals == null)
        {
            return;
        }

        int index = FindLowestAvailableIndex();
        for (int i = 0; i < tentVisuals.Length; i++)
        {
            if (tentVisuals[i] != null)
            {
                tentVisuals[i].SetActive(i == index);
            }
        }
    }
}
