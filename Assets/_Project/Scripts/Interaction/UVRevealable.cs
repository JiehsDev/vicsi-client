// Assets/_Project/Scripts/Interaction/UVRevealable.cs
using UnityEngine;

/// <summary>
/// Marks an EvidenceProp (typically a fingerprint smudge/decal) as hidden
/// under normal light and visible only for as long as a FlashlightTool's UV
/// beam is currently pointed directly at it - like a real UV-reactive print
/// that only shows up under blacklight. Hidden by default; toggled purely by
/// subscribing to FlashlightTool's public OnBeamTargetChanged event, so it
/// needs no wiring beyond sitting in the scene (the flashlight is found
/// automatically if not assigned) and works for any number of fingerprint
/// props sharing the same flashlight.
///
/// Optionally also the ONLY way this item ever reaches EvidenceStatus.Found -
/// see marksFoundOnReveal. This is what let the old EVD-016 (a latent print,
/// once its own roster item) skip the standard proximity-trigger Found every
/// other item uses: EvidenceProp.foundRequiresExternalTrigger stops it from
/// adding its own proximity trigger, and this component fires Found itself the
/// moment the UV beam actually lands on it. Default false, so every other (or
/// future) UVRevealable stays purely cosmetic unless explicitly opted in - which
/// is exactly the knife's own print sub-step today (EVD-014's handle): it has no
/// Found status of its own to fire, only a visibility state machine (hidden ->
/// clear -> dusted-and-locked -> removed - see LockVisible/Remove), so it keeps
/// marksFoundOnReveal false and carries an EvidenceProp purely so
/// FlashlightTool's beam-target raycast and FingerprintKitTool's own raycast have
/// something to hit - never registered in EvidenceStateManager.sceneEvidenceDefinitions,
/// so it has no EvidenceRecord and PhotographTool's own Found-status filter (see
/// PhotographTool.UpdateAimTarget) already keeps it out of every other tool's
/// targeting for free.
/// </summary>
[RequireComponent(typeof(EvidenceProp))]
public class UVRevealable : MonoBehaviour
{
    [SerializeField] private FlashlightTool flashlightTool;

    [Tooltip("When true, the beam landing on this item for the first time also fires EvidenceStateManager.MarkFound - the deliberate, evidence-type-specific exception to the standard proximity-Found pattern (see EvidenceProp.foundRequiresExternalTrigger). Leave off for a purely cosmetic UV reveal.")]
    [SerializeField] private bool marksFoundOnReveal = false;

    private EvidenceProp evidenceProp;
    private Renderer[] renderers;

    // Once true, SetVisible can never turn the renderers off again - see LockVisible.
    private bool visibilityLocked;

    private void Awake()
    {
        evidenceProp = GetComponent<EvidenceProp>();
        renderers = GetComponentsInChildren<Renderer>(true);

        if (flashlightTool == null)
        {
            flashlightTool = FindFirstObjectByType<FlashlightTool>();
        }

        SetVisible(false);
    }

    private void OnEnable()
    {
        if (flashlightTool != null)
        {
            flashlightTool.OnBeamTargetChanged += HandleBeamTargetChanged;
            if (flashlightTool.CurrentBeamTarget == evidenceProp)
            {
                TriggerReveal();
            }
        }
        else
        {
            Debug.LogWarning($"[{nameof(UVRevealable)}] No FlashlightTool assigned or found in scene.", this);
        }
    }

    private void OnDisable()
    {
        if (flashlightTool != null)
        {
            flashlightTool.OnBeamTargetChanged -= HandleBeamTargetChanged;
        }
    }

    private void HandleBeamTargetChanged(EvidenceProp target)
    {
        if (target == evidenceProp)
        {
            TriggerReveal();
        }
        else
        {
            SetVisible(false);
        }
    }

    /// <summary>
    /// Makes this print visible and, if marksFoundOnReveal is set, fires MarkFound -
    /// exactly what happens when the real UV beam lands on it. Public so
    /// GreyboxFlowTest can exercise this exact path directly (this project's tests
    /// drive state directly rather than simulating a beam raycast; see
    /// GreyboxFlowTest's own class comment), the same discipline already applied to
    /// SwabTool.TrySwab and EvidenceBagTool.TryInsert.
    /// </summary>
    public TransitionResult TriggerReveal()
    {
        SetVisible(true);

        if (!marksFoundOnReveal)
        {
            return TransitionResult.Duplicate;
        }

        if (EvidenceStateManager.Instance == null || evidenceProp == null || string.IsNullOrEmpty(evidenceProp.evidenceId))
        {
            return TransitionResult.Violation;
        }

        return EvidenceStateManager.Instance.MarkFound(evidenceProp.evidenceId, ToolType.IOC);
    }

    /// <summary>
    /// Makes the print permanently visible regardless of the UV beam's current
    /// position - called by FingerprintKitTool once dusting actually succeeds, since
    /// a real print stays visible once revealed by powder, not just while a blacklight
    /// happens to be pointed at it. This only changes what happens to visibility AFTER
    /// a successful dust, never how the print is first discovered.
    /// </summary>
    public void LockVisible()
    {
        visibilityLocked = true;
        SetVisible(true);
    }

    /// <summary>
    /// Makes this print permanently gone - the final visual state (see class
    /// comment: hidden -&gt; clear -&gt; dusted-and-locked -&gt; removed), called once
    /// FingerprintKitTool.TryLift actually lifts it with tape: a lifted print no
    /// longer exists at the scene, tape and all. Deliberately overrides
    /// LockVisible's own "can never turn off again" rule - removal is a later,
    /// final state, not a reversal of being revealed - and disables this
    /// GameObject entirely (not just the renderers), so nothing can raycast-hit
    /// it, dust it again, or re-trigger a beam reveal after this point.
    /// </summary>
    public void Remove()
    {
        gameObject.SetActive(false);
    }

    private void SetVisible(bool visible)
    {
        if (visibilityLocked)
        {
            visible = true;
        }

        foreach (var r in renderers)
        {
            if (r != null)
            {
                r.enabled = visible;
            }
        }
        // The cached renderers include both presentations; only the current stage renders.
        GetComponent<EvidenceFingerprintMarker>()?.SetPrintVisible(visible);
    }
}
