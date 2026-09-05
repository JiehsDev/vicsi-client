// Assets/_Project/Scripts/Interaction/AimIndicatorUI.cs
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Flips a viewfinder dot between red and green based on a PhotographTool's
/// IsConfirmedForCapture state - green means the current aim target is exactly
/// Marked, the one moment a shutter press actually applies Photographed.
///
/// Deliberately NOT CanCapture (a much broader signal: true for any real
/// EvidenceProp in frame, regardless of status). Reading CanCapture here used to
/// mean this dot turned green for an unmarked evidence item exactly the same as a
/// marked one - both revealing "this object is evidence" before the player had
/// done anything to justify knowing that, and promising a successful shutter press
/// the gate would then refuse. IsConfirmedForCapture is the only signal this
/// component may ever read; see its field comment on PhotographTool for why.
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

        photographTool.OnConfirmedForCaptureChanged += SetValidity;
        SetValidity(photographTool.IsConfirmedForCapture);
    }

    private void OnDisable()
    {
        if (photographTool != null)
        {
            photographTool.OnConfirmedForCaptureChanged -= SetValidity;
        }
    }

    private void SetValidity(bool confirmed)
    {
        if (dotImage != null)
        {
            dotImage.color = confirmed ? canCaptureColor : cannotCaptureColor;
        }
    }
}
