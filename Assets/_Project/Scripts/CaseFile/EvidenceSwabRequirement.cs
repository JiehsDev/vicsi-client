// Assets/_Project/Scripts/CaseFile/EvidenceSwabRequirement.cs
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Marks a physical location (e.g. a blood stain on a knife blade) that must be
/// swabbed before the referenced evidence item can be collected - a required
/// precondition, NOT a separate collectible evidence item with its own Found/
/// Photograph/Chalk/Tent lifecycle.
///
/// This replaced an earlier design (a fully independent EvidenceProp for the blood,
/// co-located with the knife) that turned out to be genuinely broken, not just
/// theoretically awkward: PhotographTool resolves its aim target by camera
/// distance among every registered EvidenceProp in frame, and a stain sitting a few
/// centimetres from the knife's own pivot could not be reliably disambiguated from
/// it - photographing "the knife" kept crediting the blood instead, with no way to
/// fix it short of overhauling PhotographTool's targeting for every item in the
/// scene. Making the trace a swab-only precondition on its parent sidesteps the
/// conflict entirely: there is only ever one registered EvidenceProp in that
/// physical space (the knife's own), so there is nothing left for the camera to
/// confuse it with.
///
/// Deliberately NOT an EvidenceProp - it has no evidenceId of its own, no status,
/// and is invisible to EvidenceProp.All/FindById/FindNearestWithinRadius entirely.
/// SwabTool recognizes it as a distinct, parallel contact type (see
/// HandleTipTriggerEnter) and reports straight to EvidenceStateManager.MarkSwabbed
/// against the REFERENCED item's own record - swabbing this marker never produces a
/// separate sample of its own to contain or seal, it just clears one precondition
/// on the parent item's path to Logged (see EvidenceStateManager.IsBlockedBySwabbing).
/// </summary>
// This marker's BoxCollider is deliberately sized well past the visible blood decal
// (~12x12x10cm world, on the knife - see the scene GameObject, not this script) rather
// than tightly matching the decal art. A collider matched to the decal's own visual
// footprint proved impractical to land a swab tip on in practice (small, thin, flush
// against the blade); this precondition is a "did you swab the trace" check, not a
// precision-aiming test, so a generous hit volume is the correct choice, not a
// placeholder to tighten later. Don't shrink this back down without re-confirming
// reachability the way this pass did (a live Physics/trigger check, not just reading
// the collider's numbers).
public class EvidenceSwabRequirement : MonoBehaviour
{
    private static readonly List<EvidenceSwabRequirement> Registered = new();

    /// <summary>Every EvidenceSwabRequirement currently active in the scene. Read-only view; never mutated by callers.</summary>
    public static IReadOnlyList<EvidenceSwabRequirement> All => Registered;

    [Tooltip("The evidence item this swab satisfies the precondition for (e.g. EVD-014, the knife this blood stain sits on). Must match an EvidenceDefinition.evidenceId with requiresSwabbing set true.")]
    public string evidenceId;

    /// <summary>The registered marker for this evidenceId, or null if none is currently active. Linear search - same reasoning as EvidenceProp.FindById: the registry is small.</summary>
    public static EvidenceSwabRequirement FindByEvidenceId(string evidenceId)
    {
        if (string.IsNullOrEmpty(evidenceId))
        {
            return null;
        }

        foreach (var requirement in Registered)
        {
            if (requirement != null && requirement.evidenceId == evidenceId)
            {
                return requirement;
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
