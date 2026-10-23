// Assets/_Project/Scripts/Interaction/ViewfinderFrameMask.cs
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Masks the viewfinder canvas down to a fixed aspect ratio (4:3 by default)
/// with solid black bars, like looking through an actual camera viewfinder
/// instead of seeing the full HMD field of view - only the cropped 4:3
/// window shows the scene, everything else is pure black. Builds its own
/// four bar Images at runtime against this GameObject's own RectTransform,
/// so it just needs to sit on the viewfinder canvas - no other wiring. Each
/// bar is deliberately oversized (extends well past the canvas's own edges,
/// not just up to them) so there's no seam or sliver of the 3D scene
/// bleeding through at the periphery, even with per-eye VR discrepancies or
/// a frame of layout lag during the aim-zoom transition. Bar
/// positions/sizes are recomputed every frame from the canvas's current
/// rect, so this stays correct regardless of resolution or FOV changes.
///
/// Also owns tinting the decorative border via SetConfirmed(bool) - see that
/// method's own comment. Knows nothing about why it's being told true or
/// false; a listener elsewhere decides that from actual gate state.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class ViewfinderFrameMask : MonoBehaviour
{
    [SerializeField] private float aspectWidth = 4f;
    [SerializeField] private float aspectHeight = 3f;
    [SerializeField] private Color maskColor = Color.black;

    [Tooltip("Nudges the visible crop window in or out a little, in the same units as the canvas's own RectTransform (hundreds of units is a small nudge, thousands is a big one). Positive shrinks the visible window (black creeps in further); negative grows it. Use this for small manual adjustments instead of changing the aspect ratio.")]
    [SerializeField] private float extraInset = 0f;

    [Tooltip("The single camera-frame overlay graphic (corner brackets/HUD chrome baked into one texture, e.g. Art/Props/camera-frame.png) - resized and repositioned every frame to exactly fill where the black mask begins, instead of sitting at its own separate fixed inset. Replaces the old per-edge FrameBox_Top/Bottom/Left/Right pieces. Leave unassigned to skip this (the mask still works, the frame just won't move to match it).")]
    [SerializeField] private RectTransform frameOverlay;

    [Tooltip("Tint applied to the frame overlay via SetConfirmed(true). Reverts to its own authored color (captured once, the first time the overlay is resolved) via SetConfirmed(false).")]
    [SerializeField] private Color confirmedBorderColor = new Color(0.2f, 1f, 0.2f, 0.6f);

    /// <summary>The width/height ratio of the visible crop window (aspectWidth / aspectHeight) - exposed so a photo-capture listener can size its render texture to match exactly what this mask actually shows, instead of duplicating the ratio as a separate hardcoded value that could silently drift out of sync with it.</summary>
    public float Aspect => aspectHeight > 0f ? aspectWidth / aspectHeight : 1f;

    private RectTransform selfRect;
    private RectTransform topBar;
    private RectTransform bottomBar;
    private RectTransform leftBar;
    private RectTransform rightBar;

    private Image frameOverlayImage;
    private Color frameOverlayRestColor;
    private bool borderColorsCached;
    private bool confirmed;

    private void Awake()
    {
        EnsureBars();
        EnsureFrameOverlayRef();
    }

    // Bar creation is idempotent and also called from LateUpdate (not just
    // Awake) because this component lives on a GameObject that starts
    // deactivated (the viewfinder overlay is hidden until aiming), and Unity
    // doesn't guarantee Awake has already run on every sibling the moment
    // another script deactivates it during its own Awake.
    private void EnsureBars()
    {
        if (selfRect != null)
        {
            return;
        }

        selfRect = (RectTransform)transform;
        // All four bars anchor to the canvas's exact center point, so their
        // position/size is driven entirely by anchoredPosition/sizeDelta in
        // LateUpdate - no anchor-stretch quirks to reason about.
        topBar = CreateBar("MaskBar_Top", new Vector2(0.5f, 0f));
        bottomBar = CreateBar("MaskBar_Bottom", new Vector2(0.5f, 1f));
        leftBar = CreateBar("MaskBar_Left", new Vector2(1f, 0.5f));
        rightBar = CreateBar("MaskBar_Right", new Vector2(0f, 0.5f));
    }

    // Auto-finds the existing CameraFrameOverlay child by name if not
    // explicitly wired in the inspector, so this works without manual setup
    // on a viewfinder that already has one (matching the project's existing
    // naming).
    private void EnsureFrameOverlayRef()
    {
        if (frameOverlay == null)
        {
            var t = transform.Find("CameraFrameOverlay");
            if (t != null) frameOverlay = (RectTransform)t;
        }

        CacheBorderColors();
    }

    // Captures the overlay's own authored color exactly once, the first time it's
    // resolved - so SetConfirmed(false) can revert to what the frame was actually
    // designed with instead of a hardcoded assumption.
    private void CacheBorderColors()
    {
        if (borderColorsCached || frameOverlay == null)
        {
            return;
        }

        frameOverlayImage = frameOverlay.GetComponent<Image>();
        if (frameOverlayImage == null)
        {
            return; // Not resolved yet - try again next call.
        }

        frameOverlayRestColor = frameOverlayImage.color;
        borderColorsCached = true;
    }

    /// <summary>
    /// Tints the frame overlay confirmedBorderColor (true) or reverts it to its own
    /// authored color (false). No-op if left unassigned in the inspector and not
    /// found by name - matching frameOverlay's existing "leave unassigned to skip
    /// this" contract.
    /// </summary>
    public void SetConfirmed(bool value)
    {
        confirmed = value;
        EnsureFrameOverlayRef();

        if (!borderColorsCached || frameOverlayImage == null)
        {
            return;
        }

        frameOverlayImage.color = confirmed ? confirmedBorderColor : frameOverlayRestColor;
    }

    private RectTransform CreateBar(string barName, Vector2 pivot)
    {
        var go = new GameObject(barName, typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)go.transform;
        rect.SetParent(transform, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = pivot;

        var image = go.GetComponent<Image>();
        image.color = maskColor;
        image.raycastTarget = false;

        return rect;
    }

    private void LateUpdate()
    {
        EnsureBars();
        EnsureFrameOverlayRef();

        Rect r = selfRect.rect;
        if (r.width <= 0f || r.height <= 0f)
        {
            return;
        }

        float targetAspect = aspectWidth / aspectHeight;
        float currentAspect = r.width / r.height;

        float visibleWidth;
        float visibleHeight;
        if (currentAspect > targetAspect)
        {
            // Canvas is wider than 4:3 - full height visible, crop the sides.
            visibleHeight = r.height;
            visibleWidth = r.height * targetAspect;
        }
        else
        {
            // Canvas is taller than 4:3 - full width visible, crop top/bottom.
            visibleWidth = r.width;
            visibleHeight = r.width / targetAspect;
        }

        // Manual fine-tune: shrinks (positive) or grows (negative) the crop
        // window a little without touching the aspect ratio.
        visibleWidth = Mathf.Max(0f, visibleWidth - extraInset * 2f);
        visibleHeight = Mathf.Max(0f, visibleHeight - extraInset * 2f);

        UpdateFrameOverlay(visibleWidth, visibleHeight);

        // Oversized a bit past the canvas's own extent on every side, so the
        // black is guaranteed solid all the way to (and past) the true edge
        // of vision, not just up to where this canvas happens to measure.
        // Deliberately modest (not e.g. 2x the canvas size): this canvas's
        // localScale is tiny (~0.00026, since it sits centimeters from the
        // eye), and a RectTransform sizeDelta large enough to combine with
        // that into a many-meter-wide world size silently fails to render
        // at all - keeping the margin proportionate avoids that cliff.
        float margin = Mathf.Max(r.width, r.height) * 0.2f;
        var bigSize = new Vector2(r.width + margin * 2f, r.height + margin * 2f);

        leftBar.anchoredPosition = new Vector2(-visibleWidth * 0.5f, 0f);
        leftBar.sizeDelta = bigSize;

        rightBar.anchoredPosition = new Vector2(visibleWidth * 0.5f, 0f);
        rightBar.sizeDelta = bigSize;

        topBar.anchoredPosition = new Vector2(0f, visibleHeight * 0.5f);
        topBar.sizeDelta = bigSize;

        bottomBar.anchoredPosition = new Vector2(0f, -visibleHeight * 0.5f);
        bottomBar.sizeDelta = bigSize;
    }

    // Resizes the single camera-frame overlay graphic to exactly match the mask's
    // visible window and keeps it centered on the canvas (anchored/pivoted at its
    // own center, same as the mask bars) - the frame always reads as "this is the
    // photo, everything outside is black," and never drifts off-center regardless
    // of resolution, FOV changes, or the shutter flash firing.
    private void UpdateFrameOverlay(float visibleWidth, float visibleHeight)
    {
        if (frameOverlay == null)
        {
            return;
        }

        frameOverlay.anchorMin = new Vector2(0.5f, 0.5f);
        frameOverlay.anchorMax = new Vector2(0.5f, 0.5f);
        frameOverlay.pivot = new Vector2(0.5f, 0.5f);
        frameOverlay.anchoredPosition = Vector2.zero;
        frameOverlay.sizeDelta = new Vector2(visibleWidth, visibleHeight);
    }
}
