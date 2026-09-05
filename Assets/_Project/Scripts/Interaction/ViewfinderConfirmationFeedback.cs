// Assets/_Project/Scripts/Interaction/ViewfinderConfirmationFeedback.cs
using UnityEngine;

/// <summary>
/// Bridges PhotographTool's IsConfirmedForCapture/OnConfirmedForCaptureChanged to
/// ViewfinderFrameMask.SetConfirmed, so the viewfinder's decorative border tints
/// green only for the exact frame the current aim target's status is Marked - not
/// merely "some real evidence item is in frame" (that's CanCapture, a much broader
/// signal AimIndicatorUI used to read; see PhotographTool.IsConfirmedForCapture's
/// field comment for why the two must stay separate). Same event-driven bridge
/// pattern as AimIndicatorUI elsewhere in this file's neighborhood, so neither
/// PhotographTool nor ViewfinderFrameMask needs to know the other exists.
/// </summary>
public class ViewfinderConfirmationFeedback : MonoBehaviour
{
    [SerializeField] private PhotographTool photographTool;
    [SerializeField] private ViewfinderFrameMask frameMask;

    private void Awake()
    {
        if (photographTool == null)
        {
            photographTool = GetComponentInParent<PhotographTool>();
        }
        if (frameMask == null)
        {
            frameMask = GetComponentInChildren<ViewfinderFrameMask>(true);
        }
    }

    private void OnEnable()
    {
        if (photographTool == null || frameMask == null)
        {
            Debug.LogWarning($"[{nameof(ViewfinderConfirmationFeedback)}] Missing PhotographTool or ViewfinderFrameMask reference.", this);
            return;
        }

        photographTool.OnConfirmedForCaptureChanged += frameMask.SetConfirmed;
        frameMask.SetConfirmed(photographTool.IsConfirmedForCapture);
    }

    private void OnDisable()
    {
        if (photographTool != null && frameMask != null)
        {
            photographTool.OnConfirmedForCaptureChanged -= frameMask.SetConfirmed;
            frameMask.SetConfirmed(false);
        }
    }
}
