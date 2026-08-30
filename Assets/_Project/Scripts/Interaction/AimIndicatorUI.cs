// Assets/_Project/Scripts/Interaction/AimIndicatorUI.cs
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Flips a viewfinder dot between red and green based on a PhotographTool's
/// CanCapture state - green means the shutter will take a photo right now,
/// red means it won't. Only touches PhotographTool's public CanCapture
/// property and OnAimValidityChanged event, so the same component works for
/// any future aiming tool that exposes the same shape.
/// </summary>
public class AimIndicatorUI : MonoBehaviour
{
    [SerializeField] private PhotographTool photographTool;
    [SerializeField] private Image dotImage;
    [SerializeField] private Color canCaptureColor = Color.green;
    [SerializeField] private Color cannotCaptureColor = Color.red;

    private void Awake()
    {
        if (photographTool == null)
        {
            photographTool = GetComponentInParent<PhotographTool>();
        }
    }

    private void OnEnable()
    {
        if (photographTool == null)
        {
            Debug.LogWarning($"[{nameof(AimIndicatorUI)}] No PhotographTool assigned or found in parents.", this);
            return;
        }

        photographTool.OnAimValidityChanged += SetValidity;
        SetValidity(photographTool.CanCapture);
    }

    private void OnDisable()
    {
        if (photographTool != null)
        {
            photographTool.OnAimValidityChanged -= SetValidity;
        }
    }

    private void SetValidity(bool canCapture)
    {
        if (dotImage != null)
        {
            dotImage.color = canCapture ? canCaptureColor : cannotCaptureColor;
        }
    }
}
