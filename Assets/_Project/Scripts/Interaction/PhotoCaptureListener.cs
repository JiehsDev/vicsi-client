// Assets/_Project/Scripts/Interaction/PhotoCaptureListener.cs
using UnityEngine;

/// <summary>
/// Bridges PhotographTool's existing OnPhotoCaptured event to an actual
/// screen grab, saved into PhotoAlbumManager - the camera tool itself stays
/// completely unaware that an album exists (same event-driven pattern as
/// AimIndicatorUI, CameraShutterEffect, EvidenceNotifier elsewhere in this
/// project). Renders the capture camera's current view into an offscreen
/// texture at the moment the shutter fires, so what ends up in the album is
/// genuinely what the player was looking at, not a placeholder icon.
/// </summary>
public class PhotoCaptureListener : MonoBehaviour
{
    [SerializeField] private PhotographTool photographTool;
    [Tooltip("Camera to capture from. Defaults to Camera.main (the player's HMD eye camera) if left unassigned.")]
    [SerializeField] private Camera captureCamera;
    [SerializeField] private int captureWidth = 480;
    [SerializeField] private int captureHeight = 360;

    private void OnEnable()
    {
        if (photographTool == null)
        {
            photographTool = GetComponentInParent<PhotographTool>();
        }

        if (photographTool != null)
        {
            photographTool.OnPhotoCaptured += HandlePhotoCaptured;
        }
    }

    private void OnDisable()
    {
        if (photographTool != null)
        {
            photographTool.OnPhotoCaptured -= HandlePhotoCaptured;
        }
    }

    private void HandlePhotoCaptured()
    {
        var cam = captureCamera != null ? captureCamera : Camera.main;
        if (cam == null)
        {
            Debug.LogWarning($"[{nameof(PhotoCaptureListener)}] No capture camera assigned and Camera.main is null; photo not captured.", this);
            return;
        }

        var texture = CaptureFrame(cam);

        if (PhotoAlbumManager.Instance == null)
        {
            Debug.LogWarning($"[{nameof(PhotoCaptureListener)}] No PhotoAlbumManager.Instance in scene; captured photo was discarded.", this);
            return;
        }

        PhotoAlbumManager.Instance.AddPhoto(texture);
    }

    private Texture2D CaptureFrame(Camera cam)
    {
        var renderTexture = RenderTexture.GetTemporary(captureWidth, captureHeight, 24);
        var previousTarget = cam.targetTexture;
        var previousActive = RenderTexture.active;

        cam.targetTexture = renderTexture;
        cam.Render();
        RenderTexture.active = renderTexture;

        var texture = new Texture2D(captureWidth, captureHeight, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0f, 0f, captureWidth, captureHeight), 0, 0);
        texture.Apply();

        cam.targetTexture = previousTarget;
        RenderTexture.active = previousActive;
        RenderTexture.ReleaseTemporary(renderTexture);

        return texture;
    }
}
