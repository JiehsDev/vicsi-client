// Assets/_Project/Scripts/CaseFile/EvidenceProp.cs
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Marks a physical scene object as a piece of evidence so interaction tools
/// (camera, sketchpad, etc.) can identify what they're aiming at. Also owns
/// EvidenceStatus.Found detection via a runtime-created trigger collider, sized
/// independently of whatever solid collider(s) this prop already has for aim
/// raycasts - every tool raycast already passes QueryTriggerInteraction.Ignore, so
/// this trigger never interferes with them. "Found" deliberately lives here, not on
/// any individual tool script: it means "the player has noticed this exists," and
/// must not depend on which tool happens to be equipped, or legitimate workflows
/// where evidence is encountered before the relevant tool is drawn would get blocked
/// by EvidenceStateManager's sequence gate. A trigger collider is used instead of a
/// per-frame proximity check (the pattern EvidenceTentPickup/AtticLadderLever use)
/// since it's event-driven rather than polled every frame - cheaper on Quest. No
/// The authoritative evidence record prevents repeated discovery while a player stays
/// in the trigger. Staying also retries after the investigation phase unlocks.
///
/// The size of that trigger comes from EvidenceDefinition.interactionRadius, i.e.
/// from the scenario data, not from a number typed into each prop in the scene. The
/// same radius is what EvidenceTentTool uses to decide which item a tent belongs to,
/// so "close enough to notice" and "close enough to mark" are guaranteed to be the
/// same distance rather than two values that quietly drift apart.
/// </summary>
public class EvidenceProp : MonoBehaviour
{
    [Tooltip("Must match an EvidenceDefinition.evidenceId registered in EvidenceStateManager.")]
    public string evidenceId;

    [Tooltip("FALLBACK ONLY. Used when this prop's evidenceId isn't registered in EvidenceStateManager (a misconfigured scene). The real radius comes from EvidenceDefinition.interactionRadius — edit it there.")]
    [SerializeField] private float noticeRadius = 1.5f;

    [Tooltip("DELIBERATE, EVIDENCE-TYPE-SPECIFIC EXCEPTION - not something to \"fix\" to match the standard pattern. When true, this item gets no automatic proximity trigger at all, so walking near it never fires MarkFound; Found must be triggered by something else entirely (e.g. a latent print only found via UVRevealable.TriggerReveal once a UV beam lands on it). InteractionRadius/FindNearestWithinRadius still work normally for tenting once Found has happened some other way - only the automatic proximity-Found behaviour is suppressed.")]
    [SerializeField] private bool foundRequiresExternalTrigger = false;
    public bool RequiresExternalDiscovery => foundRequiresExternalTrigger;

    /// <summary>
    /// Which of this prop's own LOCAL axes points away from the surface it rests on.
    /// Added for EvidenceChalkGuide, which needs a reliable "outward" direction to
    /// orient its tracing circle - transform.up is NOT that direction for every prop
    /// in this scene, confirmed by inspecting the roster rather than assumed:
    /// - Props built from the flat-quad decal convention (Quad_FlatDecal rotated 90°
    ///   about X to lie flat - the knife, blood spatter, blood-on-knife-blade) have
    ///   their true "up" on -transform.forward; transform.up on these points sideways.
    /// - The victim's phone is rotated the other way (-90° about X) for the same
    ///   "lie flat" reason, landing its true "up" on the OPPOSITE local axis,
    ///   transform.forward unnegated.
    /// - Props resting in their natural upright orientation (the bottle) or left at
    ///   scene-default identity rotation (the stairs latent print placeholder) do
    ///   have transform.up pointing the right way.
    /// Three different conventions on a six-item roster is exactly why this is an
    /// explicit authored field instead of a formula: guessing one rotation heuristic
    /// (e.g. always -forward) would have been wrong for half of what it needed to
    /// cover.
    /// </summary>
    public enum SurfaceNormalAxis { LocalUp, LocalForward, LocalDown, LocalBack }

    [Tooltip("Which local axis points away from this prop's resting surface. Default LocalUp is correct for anything in a natural upright pose; the flat-decal props (knife, blood spatter, blood-on-knife) need LocalBack, and the phone needs LocalForward - see the field's own doc comment. Verify per prop rather than assuming the default.")]
    [SerializeField] private SurfaceNormalAxis surfaceNormalAxis = SurfaceNormalAxis.LocalUp;

    [Tooltip("DELIBERATE, PER-PROP EXCEPTION - not something to \"fix\" to match the standard pattern. When true, EvidenceChalkGuide.SpawnFor uses chalkGuideLocalPosition/EulerAngles/Scale below instead of its own default (zero local position, identity local rotation, and 1/parentLossyScale to counteract the prop's own visual scale). Exists for a prop whose own authored rotation/scale isn't a sensible basis for a guide at all - e.g. a prop posed at an oblique angle for its own visual reasons, where surfaceNormalAxis alone (which only reorients the circle WITHIN the prop's inherited rotation) can't produce a guide that reads as flat/upright to the player. The guide still spawns as this prop's own child (so it still moves with the prop if picked up - see EvidenceChalkGuide's own class comment), only its LOCAL transform relative to the prop is overridden.")]
    [SerializeField] private bool overrideChalkGuideTransform = false;

    [Tooltip("Only read when overrideChalkGuideTransform is true.")]
    [SerializeField] private Vector3 chalkGuideLocalPosition = Vector3.zero;
    [Tooltip("Only read when overrideChalkGuideTransform is true.")]
    [SerializeField] private Vector3 chalkGuideLocalEulerAngles = Vector3.zero;
    [Tooltip("Only read when overrideChalkGuideTransform is true.")]
    [SerializeField] private Vector3 chalkGuideLocalScale = Vector3.one;

    public bool OverrideChalkGuideTransform => overrideChalkGuideTransform;
    public Vector3 ChalkGuideLocalPosition => chalkGuideLocalPosition;
    public Vector3 ChalkGuideLocalEulerAngles => chalkGuideLocalEulerAngles;
    public Vector3 ChalkGuideLocalScale => chalkGuideLocalScale;

    private static Vector3 LocalAxisVector(SurfaceNormalAxis axis) => axis switch
    {
        SurfaceNormalAxis.LocalForward => Vector3.forward,
        SurfaceNormalAxis.LocalDown => Vector3.down,
        SurfaceNormalAxis.LocalBack => Vector3.back,
        _ => Vector3.up
    };

    /// <summary>The authored surface-normal axis, in this prop's own local space (i.e. before its current rotation is applied). Used by EvidenceChalkGuide to build its guide circle's orientation entirely in local space, so it needs no per-frame recomputation and is unaffected by whatever this prop's current world rotation happens to be.</summary>
    public Vector3 SurfaceNormalLocalAxis => LocalAxisVector(surfaceNormalAxis);

    /// <summary>World-space direction pointing away from the surface this prop rests on, resolved from the authored surfaceNormalAxis rather than assumed to be transform.up - see that field's doc comment for why the assumption doesn't hold uniformly across this roster.</summary>
    public Vector3 SurfaceNormal => transform.TransformDirection(SurfaceNormalLocalAxis);

    // Every prop that can actually be identified, i.e. has a non-blank evidenceId.
    // Kept as a registry rather than found via FindObjectsByType so that resolving a
    // tent placement doesn't sweep the whole scene on every trigger press.
    private static readonly List<EvidenceProp> Registered = new();

    private float? cachedRadius;

    /// <summary>Every identifiable evidence prop currently active in the scene. Read-only view; never mutated by callers.</summary>
    public static IReadOnlyList<EvidenceProp> All => Registered;

    /// <summary>
    /// This prop's interaction radius in metres, taken from its EvidenceDefinition.
    /// Falls back to the serialized noticeRadius only when the id isn't registered.
    /// </summary>
    public float InteractionRadius
    {
        get
        {
            if (cachedRadius.HasValue)
            {
                return cachedRadius.Value;
            }

            var record = EvidenceStateManager.Instance != null
                ? EvidenceStateManager.Instance.GetRecord(evidenceId)
                : null;

            if (record?.definition == null)
            {
                // Don't cache: EvidenceStateManager may simply not have run Awake yet.
                return noticeRadius;
            }

            cachedRadius = record.definition.interactionRadius;
            return cachedRadius.Value;
        }
    }

    private void OnEnable()
    {
        // Blank-id props (the held tool models - camera, UV light, magnifier - all
        // carry an EvidenceProp so aim raycasts can classify them) are deliberately
        // never registered. They are not identifiable evidence, and leaving them out
        // of the registry means a tent can never be attributed to one no matter how
        // close it lands - which matters now that attribution is by proximity rather
        // than by what the raycast physically struck.
        if (!string.IsNullOrEmpty(evidenceId) && !Registered.Contains(this))
        {
            Registered.Add(this);
        }
    }

    private void OnDisable()
    {
        Registered.Remove(this);
    }

    private void Start()
    {
        if (foundRequiresExternalTrigger)
        {
            // No proximity trigger at all for this item - see the field's own tooltip.
            // Nothing else in this class depends on it existing (FindNearestWithinRadius
            // is a pure distance check, not physics-driven), so there is nothing to wire
            // up in its place.
            return;
        }

        if (!string.IsNullOrEmpty(evidenceId) && GetComponent<EvidenceDiscoveryDot>() == null)
            gameObject.AddComponent<EvidenceDiscoveryDot>();

        var noticeCollider = gameObject.AddComponent<SphereCollider>();
        noticeCollider.isTrigger = true;
        // SphereCollider scales by the largest absolute transform axis. The authored
        // interaction radius is in world metres, not in the imported model's units.
        Vector3 scale = transform.lossyScale;
        float largestScale = Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z),.0001f);
        noticeCollider.radius = InteractionRadius / largestScale;
    }

    /// <summary>
    /// The identifiable evidence prop whose own interaction radius contains this
    /// point, nearest first, or null if the point falls outside every item's radius.
    ///
    /// The radius tested is each candidate's OWN radius, not a single shared distance:
    /// a large item is markable from further away than a small one, which is the whole
    /// reason the radius is per-item data. Ties (overlapping radii) go to whichever
    /// item's origin is closest to the point, so marking between two items resolves to
    /// the one the player was standing over rather than to whichever happened to
    /// register first.
    /// </summary>
    public static EvidenceProp FindNearestWithinRadius(Vector3 point)
    {
        EvidenceProp nearest = null;
        float nearestSqr = float.MaxValue;

        var esm = EvidenceStateManager.Instance;
        foreach (var prop in Registered)
        {
            if (prop == null)
            {
                continue;
            }

            // Only real evidence records can receive a tent. Sub-step targets carry an
            // EvidenceProp with a pseudo id that is never registered (the knife's handle
            // print, "EVD-014-print" - see EvidenceFingerprintMarker), and must not compete:
            // that print sits 0.11 m from the knife's pivot, so a tent set down on the handle
            // side used to resolve to it - leaving the knife unmarked and logging an
            // EvidenceMarkerRejected violation. Same rule EvidenceSwabRequirement already
            // follows by staying out of this registry. Without a state manager (misconfigured
            // scene, editor tooling) every prop still counts, as before.
            if (esm != null && esm.GetRecord(prop.evidenceId) == null)
            {
                continue;
            }

            float radius = prop.InteractionRadius;
            float distanceSqr = (prop.transform.position - point).sqrMagnitude;

            if (distanceSqr <= radius * radius && distanceSqr < nearestSqr)
            {
                nearestSqr = distanceSqr;
                nearest = prop;
            }
        }

        return nearest;
    }

    /// <summary>The registered prop with this evidenceId, or null if none is currently active. Linear search - the registry is small (one entry per evidence item in a scene), so this isn't worth a dictionary.</summary>
    public static EvidenceProp FindById(string evidenceId)
    {
        if (string.IsNullOrEmpty(evidenceId))
        {
            return null;
        }

        foreach (var prop in Registered)
        {
            if (prop != null && prop.evidenceId == evidenceId)
            {
                return prop;
            }
        }

        return null;
    }

    private void OnTriggerEnter(Collider other) => TryNotice(other);
    private void OnTriggerStay(Collider other) => TryNotice(other);

    private void TryNotice(Collider other)
    {
        // SceneTurnover.IsInvestigationBlocked: IOC proximity must never discover or process evidence.
        if (foundRequiresExternalTrigger || SceneTurnover.IsInvestigationBlocked || !other.CompareTag("Player") || string.IsNullOrEmpty(evidenceId) || EvidenceStateManager.Instance == null)
        {
            return;
        }

        var record = EvidenceStateManager.Instance.GetRecord(evidenceId);
        if (record == null || record.status != EvidenceStatus.NotFound) return;
        EvidenceStateManager.Instance.MarkFound(evidenceId, ToolType.None);
    }
}
