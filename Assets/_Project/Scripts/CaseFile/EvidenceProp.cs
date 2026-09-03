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
/// duplicate-call guard here by design: EvidenceStateManager.SetStatus already no-ops
/// a repeat/backward MarkFound call against the sequence, so re-entering the trigger
/// after Found is already harmless.
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
        var noticeCollider = gameObject.AddComponent<SphereCollider>();
        noticeCollider.isTrigger = true;
        noticeCollider.radius = InteractionRadius;
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

        foreach (var prop in Registered)
        {
            if (prop == null)
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

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player") || string.IsNullOrEmpty(evidenceId) || EvidenceStateManager.Instance == null)
        {
            return;
        }

        EvidenceStateManager.Instance.MarkFound(evidenceId, ToolType.None);
    }
}
