// Assets/_Project/Scripts/CaseFile/EvidenceFingerprintMarker.cs
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Marks a physical latent-print location (a UV-revealed mark on the knife's handle,
/// on the inside of the gloves, ...) that must be dusted and lifted before the
/// referenced evidence item can be logged - a required on-scene precondition, NOT a
/// separate collectible evidence item with its own Found/Photograph/Chalk/Tent/Report
/// lifecycle. Generalizes what used to be hardcoded directly onto FingerprintKitTool
/// for the knife alone (KnifeEvidenceId/PrintMarkerId consts) - see that class's own
/// comment for why "hardcoded to one item" stopped being sufficient once the gloves
/// needed the exact same collision-based dust/lift flow.
///
/// Mirrors EvidenceSwabRequirement's own shape closely: a marker with its own
/// evidenceId field pointing to the PARENT's real roster id, found via a static
/// registry, gating the parent's own transition rather than having a status of its
/// own. Sits alongside EvidenceProp (its own pseudo id, e.g. "EVD-019-print", never
/// registered in EvidenceStateManager.sceneEvidenceDefinitions - PhotographTool's own
/// Found-status aim-target filter already excludes it from every other tool's
/// targeting for free, see UVRevealable's class comment) and UVRevealable on the same
/// GameObject.
///
/// IsDusted lives here, per-marker, rather than as a single bool on FingerprintKitTool
/// - now that more than one marker can exist at once, a single shared bool would let
/// dusting one print silently count as dusting the other. Mirrors how
/// fingerprintingDone/swabbingDone/isFlipped already live on EvidenceRecord as
/// per-item flags rather than on a tool; this one stays off EvidenceRecord instead
/// (see FingerprintKitTool's own comment) since dusting is visual-only, not a general
/// evidence state - only Lift (via MarkFingerprinted) ever reaches EvidenceRecord.
/// </summary>
[RequireComponent(typeof(EvidenceProp))]
public class EvidenceFingerprintMarker : MonoBehaviour
{
    private static readonly List<EvidenceFingerprintMarker> Registered = new();

    /// <summary>Every EvidenceFingerprintMarker currently active in the scene. Read-only view; never mutated by callers.</summary>
    public static IReadOnlyList<EvidenceFingerprintMarker> All => Registered;

    [Tooltip("The evidence item this print belongs to (e.g. EVD-014, EVD-019). Must match an EvidenceDefinition.evidenceId with requiresFingerprinting and fingerprintBeforeCollection both set true.")]
    public string evidenceId;

    [Tooltip("Seconds of continuous, valid brush-tip contact (loaded brush, correct target, trigger held) required to fully reveal this print. Mirrors EvidenceDefinition.chalkCompletionThreshold's own 'gradual, not instant' reasoning - see FingerprintKitTool.TryDust.")]
    [SerializeField] private float requiredDustSeconds = 0.75f;

    [Header("Print presentation")]
    [SerializeField] private GameObject powderedVisual;
    [SerializeField] private Renderer latentRenderer;

    /// <summary>Keep the authored latent mark until powdering completes, then show the linked model.</summary>
    internal bool ApplyPowderedVisual()
    {
        if (powderedVisual == null) return false;
        SetPrintVisible(true);
        return true;
    }

    internal void SetPrintVisible(bool visible)
    {
        if (powderedVisual == null) return;
        powderedVisual.SetActive(visible && IsDusted);
        if (latentRenderer != null) latentRenderer.enabled = visible && !IsDusted;
    }

    /// <summary>Whether this specific print has been dusted yet. Visual-only precondition for lifting, not a general evidence state (see class comment). Set only by AddDustProgress crossing its threshold.</summary>
    public bool IsDusted { get; private set; }

    /// <summary>Current reveal progress, 0..1. Exposed for a future progress bar/visual; not read by any gameplay logic today.</summary>
    public float DustProgress { get; private set; }

    internal void MarkDusted()
    {
        IsDusted = true;
        DustProgress = 1f;
    }

    /// <summary>
    /// Accumulates deliberate, continuous dust contact - called once per held frame by
    /// FingerprintKitTool.TryDust while every gate (loaded brush, correct target,
    /// trigger held) already passed, never on a single instantaneous contact. No-op
    /// once already dusted, so a lingering frame of contact after completion can't
    /// throw. Same "progress toward a threshold, not instant" shape
    /// EvidenceChalkGuide.CoverPointsNear already uses for its own gradual reveal.
    /// </summary>
    internal void AddDustProgress(float deltaSeconds)
    {
        if (IsDusted)
        {
            return;
        }

        DustProgress = Mathf.Clamp01(DustProgress + deltaSeconds / Mathf.Max(0.01f, requiredDustSeconds));
        if (DustProgress >= 1f)
        {
            MarkDusted();
        }
    }

    /// <summary>The registered marker for this evidenceId, or null if none is currently active. Linear search - same reasoning as EvidenceProp.FindById/EvidenceSwabRequirement.FindByEvidenceId: the registry is small.</summary>
    public static EvidenceFingerprintMarker FindByEvidenceId(string evidenceId)
    {
        if (string.IsNullOrEmpty(evidenceId))
        {
            return null;
        }

        foreach (var marker in Registered)
        {
            if (marker != null && marker.evidenceId == evidenceId)
            {
                return marker;
            }
        }

        return null;
    }

    private void OnEnable()
    {
        if (!string.IsNullOrEmpty(evidenceId) && !Registered.Contains(this))
        {
            Registered.Add(this);
        }
    }

    private void OnDisable()
    {
        Registered.Remove(this);
    }
}
