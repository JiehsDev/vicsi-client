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
/// </summary>
[RequireComponent(typeof(EvidenceProp))]
public class UVRevealable : MonoBehaviour
{
    [SerializeField] private FlashlightTool flashlightTool;

    private EvidenceProp evidenceProp;
    private Renderer[] renderers;

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
            SetVisible(flashlightTool.CurrentBeamTarget == evidenceProp);
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
        SetVisible(target == evidenceProp);
    }

    private void SetVisible(bool visible)
    {
        foreach (var r in renderers)
        {
            if (r != null)
            {
                r.enabled = visible;
            }
        }
    }
}
