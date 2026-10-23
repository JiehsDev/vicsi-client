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
///
/// Also green while the player stands inside the active scene-photography point's
/// marked circle (ScenePhotographyManager.IsPlayerInActiveZone) - the other moment a
/// shutter press counts. That signal is about a scene position, never about evidence.
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

        photographTool.OnConfirmedForCaptureChanged += HandleSignalChanged;
        ScenePhotographyManager.OnActiveZoneChanged += HandleSignalChanged;
        Refresh();
    }

    private void OnDisable()
    {
        ScenePhotographyManager.OnActiveZoneChanged -= HandleSignalChanged;
        if (photographTool != null && frameMask != null)
        {
            photographTool.OnConfirmedForCaptureChanged -= HandleSignalChanged;
            frameMask.SetConfirmed(false);
        }
    }

    private void HandleSignalChanged(bool _) => Refresh();

    private void Refresh()
    {
        if (photographTool == null || frameMask == null)
        {
            return;
        }
        var scene = ScenePhotographyManager.Instance;
        frameMask.SetConfirmed(photographTool.IsConfirmedForCapture || (scene != null && scene.IsPlayerInActiveZone));
    }
}
