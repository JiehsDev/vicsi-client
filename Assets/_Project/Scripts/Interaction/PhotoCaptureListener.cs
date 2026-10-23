// Assets/_Project/Scripts/Interaction/PhotoCaptureListener.cs
using UnityEngine;

/// <summary>
/// Bridges PhotographTool's existing OnPhotoCaptured event to an actual
/// screen grab, saved into PhotoAlbumManager - the camera tool itself stays
/// completely unaware that an album exists (same event-driven pattern as
/// AimIndicatorUI, CameraShutterEffect, EvidenceNotifier elsewhere in this
/// project).
///
/// Renders through a dedicated capture camera (built once at runtime, the
/// same "build it yourself in Awake" pattern CameraShutterEffect/
/// ViewfinderFrameMask use for their own child objects) rather than the
/// player's live HMD eye camera (Camera.main / the OVR CenterEyeAnchor)
/// directly. That eye camera is the one the XR compositor is actively
/// presenting frames from every frame, so rendering through it manually -
/// even just for an instant - risks a visible hitch; the dedicated camera
/// never touches that real display path at all. It's a plain, untagged
/// camera that only ever renders into an explicit RenderTexture (never to
/// the XR display), which both the built-in pipeline and URP already treat
/// as a normal mono render - no stereo API needed. It's rigidly parented to
/// the eye camera with a zero local offset, so it always matches head
/// position/rotation, and has every setting it needs (FOV, clip planes,
/// culling mask, clear flags) copied from the eye camera at capture time.
///
/// The viewfinder overlay (ViewfinderFrameMask's black 4:3 crop bars, the
/// aim reticle, HUD text) is a World Space canvas that sits directly in
/// front of the eye camera, so a naive render would still bake all of that
/// into the saved photo even through the dedicated camera. It's hidden for
/// the single synchronous Render() call below and restored immediately
/// after, so the photo is the clean scene content only - the player never
/// sees it flicker since nothing is presented to the screen in between.
///
/// The crop itself is read from that same ViewfinderFrameMask at capture
/// time (its Aspect property) rather than duplicated here as a separately
/// hardcoded width/height - so the saved photo always matches whatever
/// window the mask is actually showing the player, even if a designer
/// retunes the mask's aspect ratio later. captureWidth/captureHeight are
/// only the resolution/fallback used when no mask is found.
/// </summary>
public class PhotoCaptureListener : MonoBehaviour
{
    [SerializeField] private PhotographTool photographTool;
    [Tooltip("Camera whose pose/FOV the capture follows. Defaults to Camera.main (the player's HMD eye camera) if left unassigned.")]
    [SerializeField] private Camera povCamera;
    [Tooltip("The viewfinder's crop mask, whose Aspect drives the captured photo's crop. Auto-found on the PhotographTool's viewfinder overlay if left unassigned.")]
    [SerializeField] private ViewfinderFrameMask viewfinderFrameMask;
    [Tooltip("Capture resolution (height) and the width fallback used only if no ViewfinderFrameMask is found - normally the width is derived from the mask's own aspect ratio instead.")]
    [SerializeField] private int captureWidth = 480;
    [SerializeField] private int captureHeight = 360;

    private Camera captureCamera;

    private void OnEnable()
    {
        if (photographTool == null)
        {
            photographTool = GetComponentInParent<PhotographTool>();
        }

        if (photographTool != null)
        {
            photographTool.OnPhotoCaptured += HandlePhotoCaptured;

            if (viewfinderFrameMask == null && photographTool.ViewfinderOverlay != null)
            {
                viewfinderFrameMask = photographTool.ViewfinderOverlay.GetComponentInChildren<ViewfinderFrameMask>(true);
            }
        }
    }

    private void OnDisable()
    {
        if (photographTool != null)
        {
            photographTool.OnPhotoCaptured -= HandlePhotoCaptured;
        }
    }

    private void OnDestroy()
    {
        if (captureCamera != null)
        {
            Destroy(captureCamera.gameObject);
        }
    }

    private void HandlePhotoCaptured()
    {
        CapturePhotograph(photographTool.LastCaptureSubjectId, photographTool.LastCaptureSubjectName, photographTool.LastCaptureKind);
    }

    public void CaptureScenePhoto(ScenePhotoPoint point)
    {
        if(point!=null)CapturePhotograph(point.photoPointId,point.displayName,PhotoCaptureKind.Scene);
    }

    private void CapturePhotograph(string subjectId,string subjectName,PhotoCaptureKind kind)
    {
        var pov = photographTool != null ? photographTool.CapturePov : (povCamera != null ? povCamera : Camera.main);
        if (pov == null)
        {
            Debug.LogWarning($"[{nameof(PhotoCaptureListener)}] No POV camera assigned and Camera.main is null; photo not captured.", this);
            return;
        }

        EnsureCaptureCamera(pov);
        SyncCaptureCamera(pov);

        var overlay = photographTool != null ? photographTool.ViewfinderOverlay : null;
        bool overlayWasActive = overlay != null && overlay.activeSelf;
        if (overlayWasActive)
        {
            overlay.SetActive(false);
        }

        Texture2D texture;
        var guide = ScenePhotographyManager.Instance != null
            ? ScenePhotographyManager.Instance.GetComponent<ScenePhotographyGuide>() : null;
        var hiddenGuide = guide != null ? guide.HideForCapture() : null;
        var hiddenEvidenceDots = EvidenceDiscoveryDot.HideForCapture();
        var hiddenProcedureGuide = ProcedureDestinationGuide.HideForCapture();
        var hiddenVendorPath = VendorPathGuide.HideForCapture();
        var hiddenVendorHint = VendorWitnessInteraction.HideForCapture();
        var hiddenGloveGuide = GloveStationGuide.HideForCapture();
        var hiddenCustodyGuide = EvidenceBoxWaypoint.HideForCapture();
        // Feedback is not scene evidence. Hide its rendering synchronously without
        // disabling presenters, clearing messages or interrupting their countdowns.
        var notificationGroup = NotificationManager.Instance?.UI?.GetComponent<CanvasGroup>();
        var dialogueGroup = STCSNotificationUI.Instance?.GetComponent<CanvasGroup>();
        float notificationAlpha = notificationGroup != null ? notificationGroup.alpha : 0;
        float dialogueAlpha = dialogueGroup != null ? dialogueGroup.alpha : 0;
        try
        {
            if (notificationGroup != null) notificationGroup.alpha = 0;
            if (dialogueGroup != null) dialogueGroup.alpha = 0;
            texture = CaptureFrame();
        }
        finally
        {
            if (notificationGroup != null) notificationGroup.alpha = notificationAlpha;
            if (dialogueGroup != null) dialogueGroup.alpha = dialogueAlpha;
            hiddenCustodyGuide.Dispose();
            hiddenProcedureGuide.Dispose();
            hiddenGloveGuide.Dispose();
            hiddenVendorPath.Dispose();
            hiddenVendorHint.Dispose();
            hiddenEvidenceDots.Dispose();
            hiddenGuide?.Dispose();
            if (overlayWasActive) overlay.SetActive(true);
        }

        if (PhotoAlbumManager.Instance == null)
        {
            Debug.LogWarning($"[{nameof(PhotoCaptureListener)}] No PhotoAlbumManager.Instance in scene; captured photo was discarded.", this);
            return;
        }

        PhotoAlbumManager.Instance.AddPhoto(texture, subjectId, subjectName, kind);
    }

    // Built once and reused for every shot - a plain (non-XR) camera rigidly
    // parented to the eye camera with a zero local offset, so it always
    // renders exactly what the player is looking at without ever being the
    // camera XR itself is driving.
    private void EnsureCaptureCamera(Camera pov)
    {
        if (captureCamera != null)
        {
            return;
        }

        var go = new GameObject("PhotoCaptureCamera");
        go.transform.SetParent(pov.transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;

        captureCamera = go.AddComponent<Camera>();
        captureCamera.enabled = false; // never auto-renders; only Render() is ever called on it, from CaptureFrame
    }

    // Re-synced every shot (not just once) so the capture always matches the
    // eye camera's current state - in particular fieldOfView, which
    // PhotographTool animates between rest and zoomedFieldOfView while aiming.
    private void SyncCaptureCamera(Camera pov)
    {
        captureCamera.transform.SetParent(pov.transform, false);
        captureCamera.transform.localPosition = Vector3.zero;
        captureCamera.transform.localRotation = Quaternion.identity;
        captureCamera.fieldOfView = pov.fieldOfView;
        captureCamera.nearClipPlane = pov.nearClipPlane;
        captureCamera.farClipPlane = pov.farClipPlane;
        captureCamera.cullingMask = pov.cullingMask;
        captureCamera.clearFlags = pov.clearFlags;
        captureCamera.backgroundColor = pov.backgroundColor;
    }

    private Texture2D CaptureFrame()
    {
        // Width is derived from the mask's own aspect ratio (height stays the
        // configured resolution) so the capture always matches whatever window
        // the player actually sees through the viewfinder, instead of a second
        // hardcoded aspect here silently drifting out of sync with it.
        // Scaled close-ups need enough pixels to read the ruler's numbered graduations.
        int height = photographTool != null && photographTool.LastCaptureUsedScale ? Mathf.Max(1080, captureHeight) : captureHeight;
        int width = viewfinderFrameMask != null
            ? Mathf.Max(1, Mathf.RoundToInt(height * viewfinderFrameMask.Aspect))
            : Mathf.Max(1, Mathf.RoundToInt(height * ((float)captureWidth / captureHeight)));

        var renderTexture = RenderTexture.GetTemporary(width, height, 24);

        captureCamera.targetTexture = renderTexture;
        captureCamera.Render();
        RenderTexture.active = renderTexture;

        var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
        texture.Apply();

        captureCamera.targetTexture = null;
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(renderTexture);

        return texture;
    }
}
