// Assets/_Project/Scripts/UI/PhotoAlbumUI.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Drives the photo album gallery panel. Every visual piece - background,
/// header, grid layout, thumbnail template, big-view image - is a normal
/// authored UI GameObject in the scene/prefab (Image/Text/GridLayoutGroup/
/// RawImage), not built in code, specifically so it can be restyled by hand
/// in the Editor like any other UGUI screen. This script wires behavior:
/// cloning thumbnailTemplate into content for every photo PhotoAlbumManager
/// reports, and - while the panel is open - reading the right controller for
/// navigation (thumbstick moves the highlighted thumbnail through the grid),
/// selection (A opens/closes a big view of whichever photo is highlighted,
/// like flipping open a real photo album; browsing with the stick while the
/// big view is open live-updates it), and closing (B always exits the whole
/// panel, from either the grid or the big view). Show()/Hide()/Toggle() stay
/// public so something else (e.g. UtilityMenuController's Album entry) can
/// still trigger this from outside. Movement/turning are suspended for as
/// long as the panel is open (LocomotionSuspender), same as the tool wheel
/// and utility menu.
/// </summary>
public class PhotoAlbumUI : MonoBehaviour
{
    private const float NavDeadzone = 0.5f;

    [Tooltip("The GameObject that gets shown/hidden by Toggle()/Show()/Hide(). Defaults to this GameObject if left unassigned.")]
    [SerializeField] private GameObject panelRoot;

    [Tooltip("Parent that new thumbnails are instantiated into - give it a GridLayoutGroup to control thumbnail size/spacing in the Inspector.")]
    [SerializeField] private RectTransform content;

    [Tooltip("An inactive RawImage in the scene used as the template for each thumbnail - style this one (size, border, background) and every photo added will look the same.")]
    [SerializeField] private RawImage thumbnailTemplate;

    [Tooltip("A large RawImage (start inactive) that displays the selected photo full-size, like opening a real album to that page. Shown/hidden by the right controller's A button.")]
    [SerializeField] private RawImage bigViewImage;

    [SerializeField] private PhotoAlbumManager albumManager;

    [Header("Right-Stick Navigation")]
    [SerializeField] private Color unselectedTint = new Color(0.55f, 0.55f, 0.55f, 1f);
    [SerializeField] private Color selectedTint = Color.white;

    private readonly List<RawImage> thumbnails = new();
    private int selectedIndex = -1;
    private bool isBigViewOpen;

    private InputAction navStickAction;
    private InputAction selectAction;
    private InputAction closeAction;
    private bool stickWasNeutral = true;

    private void Awake()
    {
        if (panelRoot == null)
        {
            panelRoot = gameObject;
        }

        if (albumManager == null)
        {
            albumManager = PhotoAlbumManager.Instance != null ? PhotoAlbumManager.Instance : FindFirstObjectByType<PhotoAlbumManager>();
        }

        if (thumbnailTemplate != null)
        {
            thumbnailTemplate.gameObject.SetActive(false);
        }

        if (bigViewImage != null)
        {
            bigViewImage.gameObject.SetActive(false);
        }

        // Same "raw XR controller button/stick" pattern used elsewhere in this
        // project (PhotographTool, ToolWheelController, ...). Right thumbstick
        // moves the highlighted thumbnail; the right controller's A (primary)
        // button opens/closes the big view of whatever's highlighted; the
        // right controller's B (secondary) button always closes the whole
        // panel, regardless of which sub-view is showing.
        navStickAction = new InputAction("PhotoAlbum_NavStick", InputActionType.Value, "<XRController>{RightHand}/thumbstick", expectedControlType: "Vector2");
        selectAction = new InputAction("PhotoAlbum_Select", InputActionType.Button, "<XRController>{RightHand}/primaryButton");
        closeAction = new InputAction("PhotoAlbum_Close", InputActionType.Button, "<XRController>{RightHand}/secondaryButton");
    }

    private void OnEnable()
    {
        navStickAction.Enable();
        selectAction.Enable();
        closeAction.Enable();

        if (albumManager == null)
        {
            return;
        }

        albumManager.OnPhotoAdded += AddThumbnail;

        // Catch up on anything captured before this UI existed/was enabled.
        foreach (var photo in albumManager.Photos)
        {
            AddThumbnail(photo);
        }
    }

    private void OnDisable()
    {
        navStickAction.Disable();
        selectAction.Disable();
        closeAction.Disable();

        if (albumManager != null)
        {
            albumManager.OnPhotoAdded -= AddThumbnail;
        }
    }

    private void OnDestroy()
    {
        navStickAction?.Dispose();
        selectAction?.Dispose();
        closeAction?.Dispose();
    }

    private void Update()
    {
        if (panelRoot == null || !panelRoot.activeSelf)
        {
            stickWasNeutral = true;
            return;
        }

        if (closeAction.WasPressedThisFrame())
        {
            Hide();
            return;
        }

        if (selectAction.WasPressedThisFrame())
        {
            ToggleBigView();
        }

        Vector2 stick = navStickAction.ReadValue<Vector2>();
        if (stick.sqrMagnitude < NavDeadzone * NavDeadzone)
        {
            stickWasNeutral = true;
            return;
        }

        // One flick = one step: a new step only registers once the stick has
        // returned to neutral since the last one, so navigation doesn't race
        // across the whole grid while the stick is held over.
        if (!stickWasNeutral)
        {
            return;
        }
        stickWasNeutral = false;

        if (isBigViewOpen)
        {
            // Flipping through pages while zoomed in - left/right only, no
            // row concept once you're looking at a single photo full-size.
            MoveSelection(stick.x > 0f ? 1 : -1);
            return;
        }

        if (Mathf.Abs(stick.x) >= Mathf.Abs(stick.y))
        {
            MoveSelection(stick.x > 0f ? 1 : -1);
        }
        else
        {
            int columns = GetColumnsPerRow();
            MoveSelection(stick.y > 0f ? -columns : columns);
        }
    }

    private void ToggleBigView()
    {
        if (thumbnails.Count == 0)
        {
            return;
        }

        isBigViewOpen = !isBigViewOpen;

        if (bigViewImage != null)
        {
            bigViewImage.gameObject.SetActive(isBigViewOpen);
        }

        if (isBigViewOpen)
        {
            SyncBigViewTexture();
        }
    }

    private void SyncBigViewTexture()
    {
        if (bigViewImage == null || selectedIndex < 0 || selectedIndex >= thumbnails.Count)
        {
            return;
        }

        bigViewImage.texture = thumbnails[selectedIndex].texture;
    }

    private int GetColumnsPerRow()
    {
        if (content == null)
        {
            return 1;
        }

        var grid = content.GetComponent<GridLayoutGroup>();
        if (grid == null)
        {
            return 1;
        }

        float availableWidth = content.rect.width - grid.padding.left - grid.padding.right;
        float cellWithSpacing = grid.cellSize.x + grid.spacing.x;
        if (cellWithSpacing <= 0f)
        {
            return 1;
        }

        return Mathf.Max(1, Mathf.FloorToInt((availableWidth + grid.spacing.x) / cellWithSpacing));
    }

    private void MoveSelection(int delta)
    {
        if (thumbnails.Count == 0)
        {
            return;
        }

        int newIndex = Mathf.Clamp(selectedIndex + delta, 0, thumbnails.Count - 1);
        SetSelectedIndex(newIndex);
    }

    private void SetSelectedIndex(int index)
    {
        if (thumbnails.Count == 0)
        {
            return;
        }

        if (index != selectedIndex)
        {
            if (selectedIndex >= 0 && selectedIndex < thumbnails.Count)
            {
                thumbnails[selectedIndex].color = unselectedTint;
            }

            selectedIndex = Mathf.Clamp(index, 0, thumbnails.Count - 1);
            thumbnails[selectedIndex].color = selectedTint;
        }

        if (isBigViewOpen)
        {
            SyncBigViewTexture();
        }
    }

    private void AddThumbnail(Texture2D photo)
    {
        if (thumbnailTemplate == null || content == null)
        {
            return;
        }

        var clone = Instantiate(thumbnailTemplate, content);
        clone.gameObject.SetActive(true);
        clone.texture = photo;
        clone.color = unselectedTint;

        thumbnails.Add(clone);
        if (selectedIndex < 0)
        {
            SetSelectedIndex(0);
        }
    }

    public void Toggle()
    {
        if (panelRoot.activeSelf)
        {
            Hide();
        }
        else
        {
            Show();
        }
    }

    public void Show()
    {
        if (!panelRoot.activeSelf)
        {
            LocomotionSuspender.Suspend();
        }
        panelRoot.SetActive(true);
        stickWasNeutral = true;
    }

    public void Hide()
    {
        if (panelRoot.activeSelf)
        {
            LocomotionSuspender.Resume();
        }
        panelRoot.SetActive(false);

        isBigViewOpen = false;
        if (bigViewImage != null)
        {
            bigViewImage.gameObject.SetActive(false);
        }
    }
}
