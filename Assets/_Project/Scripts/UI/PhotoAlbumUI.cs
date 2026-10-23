// Assets/_Project/Scripts/UI/PhotoAlbumUI.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

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
    private RectTransform imageViewport;
    private TMP_Text caption, controls, empty;
    private float zoom = 1f;
    private Vector2 pan;
    private InputAction panAction;
    public float Zoom => zoom;
    public string SelectedCaption => caption != null ? caption.text : string.Empty;
    public RectTransform DisplayedImage => bigViewImage != null ? bigViewImage.rectTransform : null;
    public bool IsEmptyStateVisible => empty != null && empty.gameObject.activeInHierarchy;

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
        selectAction = PlayerUIGate.CreateConfirmAction(this, "PhotoAlbum_Select", () => panelRoot != null && panelRoot.activeInHierarchy, 300);
        closeAction = new InputAction("PhotoAlbum_Close", InputActionType.Button, "<XRController>{RightHand}/secondaryButton");
        panAction = new InputAction("PhotoAlbum_Pan", InputActionType.Value, "<XRController>{LeftHand}/thumbstick", expectedControlType:"Vector2");
        BuildPresentation();
    }

    private void OnEnable()
    {
        navStickAction.Enable();
        selectAction.Enable();
        closeAction.Enable();
        panAction.Enable();

        if (albumManager == null)
        {
            return;
        }

        albumManager.OnPhotoAdded += AddThumbnail;

        // Catch up on anything captured before this UI existed/was enabled.
        for(int i=thumbnails.Count;i<albumManager.Photos.Count;i++) AddThumbnail(albumManager.Photos[i]);
    }

    private void OnDisable()
    {
        navStickAction.Disable();
        selectAction.Disable();
        closeAction.Disable();
        panAction.Disable();
        PlayerUIGate.Exit(this);

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
        panAction?.Dispose();
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

        if (PlayerUIGate.TryConsumeConfirm(this, selectAction))
        {
            ToggleBigView();
        }

        Vector2 stick = navStickAction.ReadValue<Vector2>();
        if(isBigViewOpen && zoom>1f)
        {
            pan += panAction.ReadValue<Vector2>() * Time.unscaledDeltaTime * .6f;
            ApplyImageFit();
        }
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
            if(Mathf.Abs(stick.y)>Mathf.Abs(stick.x)) SetZoom(stick.y>0?zoom*2:zoom*.5f);
            else MoveSelection(stick.x > 0f ? 1 : -1);
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
        imageViewport.gameObject.SetActive(isBigViewOpen);
        content.gameObject.SetActive(!isBigViewOpen);
        SetZoom(1);
        UpdatePresentation();

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
        ApplyImageFit();
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
        if(grid.constraint==GridLayoutGroup.Constraint.FixedColumnCount)return Mathf.Max(1,grid.constraintCount);

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
            if(panelRoot!=null && panelRoot.activeInHierarchy) InteractionSfx.Play(InteractionSound.Page,panelRoot.transform.position);
            if (selectedIndex >= 0 && selectedIndex < thumbnails.Count)
            {
                thumbnails[selectedIndex].color = unselectedTint;
            }

            selectedIndex = Mathf.Clamp(index, 0, thumbnails.Count - 1);
            thumbnails[selectedIndex].color = selectedTint;
            zoom=1;pan=Vector2.zero;
        }
        UpdatePresentation();

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

        var slot=EditableSceneVisuals.Create("Photo thumbnail",typeof(RectTransform));slot.transform.SetParent(content,false);
        var clone = Instantiate(thumbnailTemplate, slot.transform);
        clone.gameObject.SetActive(true);
        clone.texture = photo;
        clone.color = unselectedTint;

        thumbnails.Add(clone);
        var grid=content.GetComponent<GridLayoutGroup>();var cell=grid!=null?grid.cellSize:new Vector2(160,120);
        var rect=clone.rectTransform;rect.anchorMin=rect.anchorMax=new Vector2(.5f,1);rect.pivot=new Vector2(.5f,1);
        rect.anchoredPosition=Vector2.zero;rect.sizeDelta=Fit(new Vector2(cell.x,cell.y-42),photo.width/(float)photo.height);
        var label=Label(slot.transform,"Photo identity",15);label.alignment=TextAlignmentOptions.Center;
        label.rectTransform.anchorMin=new Vector2(0,0);label.rectTransform.anchorMax=new Vector2(1,0);
        label.rectTransform.pivot=new Vector2(.5f,0);label.rectTransform.sizeDelta=new Vector2(0,42);
        label.text=albumManager.FindRecord(photo)?.Caption??"Photograph";
        if (selectedIndex < 0)
        {
            SetSelectedIndex(0);
        }
        UpdatePresentation();
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
        // Defers to whatever else already owns the screen right now (the
        // camera viewfinder, the utility menu, ...) instead of stacking this
        // panel on top of it - same contract as UtilityMenuController/
        // ToolWheelController.
        if (!panelRoot.activeSelf && PlayerUIGate.IsBlocked)
        {
            return;
        }

        if (!panelRoot.activeSelf)
        {
            LocomotionSuspender.Suspend();
            PlayerUIGate.Enter(this);
        }
        HudFollowCamera.ConfigureShared(panelRoot, this, 300);
        panelRoot.SetActive(true);
        stickWasNeutral = true;
        UpdatePresentation();
    }

    public void Hide()
    {
        if (panelRoot.activeSelf)
        {
            LocomotionSuspender.Resume();
            PlayerUIGate.Exit(this);
        }
        panelRoot.SetActive(false);

        isBigViewOpen = false;
        if(imageViewport!=null)imageViewport.gameObject.SetActive(false);
        if(content!=null)content.gameObject.SetActive(true);
        if (bigViewImage != null)
        {
            bigViewImage.gameObject.SetActive(false);
        }
    }

    private TMP_Text Label(Transform parent,string name,float size)
    {
        var text=ForensicUIStyle.Text(parent,name,size,ForensicUIStyle.TextPrimary);
        text.alignment=TextAlignmentOptions.Center;return text;
    }
    [EditableSceneBuilder]
    private void BuildPresentation()
    {
        using (EditableSceneVisuals.Begin(this,"BuildPresentation"))
        {
        if(content==null||bigViewImage==null)return;
        EditableSceneVisuals.Preserve(content.gameObject,bigViewImage.gameObject);
        content.offsetMin=new Vector2(15,130);content.offsetMax=new Vector2(-15,-90);
        var grid=content.GetComponent<GridLayoutGroup>();
        if(grid!=null)grid.cellSize=new Vector2(grid.cellSize.x,Mathf.Max(160,grid.cellSize.y));
        var go=EditableSceneVisuals.Create("Photo inspection viewport",typeof(RectTransform),typeof(Image),typeof(RectMask2D));
        go.transform.SetParent(panelRoot.transform,false);imageViewport=(RectTransform)go.transform;
        imageViewport.anchorMin=Vector2.zero;imageViewport.anchorMax=Vector2.one;
        imageViewport.offsetMin=content.offsetMin;imageViewport.offsetMax=content.offsetMax;
        go.GetComponent<Image>().color=Color.black;go.GetComponent<Image>().raycastTarget=false;
        bigViewImage.transform.SetParent(imageViewport,false);
        bigViewImage.rectTransform.anchorMin=bigViewImage.rectTransform.anchorMax=new Vector2(.5f,.5f);
        bigViewImage.rectTransform.pivot=new Vector2(.5f,.5f);bigViewImage.uvRect=new Rect(0,0,1,1);
        go.SetActive(false);
        caption=Label(panelRoot.transform,"Selected photo caption",20);
        controls=Label(panelRoot.transform,"Album controls",18);
        foreach(var label in new[]{caption,controls})
        {
            label.rectTransform.anchorMin=new Vector2(0,0);label.rectTransform.anchorMax=new Vector2(1,0);
            label.rectTransform.pivot=new Vector2(.5f,0);label.rectTransform.sizeDelta=new Vector2(-30,42);
        }
        caption.rectTransform.anchoredPosition=new Vector2(0,56);controls.rectTransform.anchoredPosition=new Vector2(0,4);
        caption.rectTransform.sizeDelta=new Vector2(-30,64);controls.rectTransform.sizeDelta=new Vector2(-30,48);
        empty=Label(panelRoot.transform,"No photos",28);empty.rectTransform.sizeDelta=new Vector2(700,140);
        empty.text="No photographs yet\nUse the camera to capture evidence.";
            }
    }
    public static Vector2 Fit(Vector2 available,float aspect)
    {
        return available.x/available.y>aspect?new Vector2(available.y*aspect,available.y):new Vector2(available.x,available.x/aspect);
    }
    public void SetZoom(float value)
    {
        zoom=Mathf.Clamp(value,1,4);if(zoom==1)pan=Vector2.zero;ApplyImageFit();UpdatePresentation();
    }
    private void ApplyImageFit()
    {
        if(bigViewImage==null||bigViewImage.texture==null||imageViewport==null)return;
        Vector2 area=imageViewport.rect.size;
        Vector2 size=Fit(area,bigViewImage.texture.width/(float)bigViewImage.texture.height)*zoom;
        var extent=Vector2.Max(Vector2.zero,(size-area)*.5f);
        pan=new Vector2(Mathf.Clamp(pan.x,-1,1),Mathf.Clamp(pan.y,-1,1));
        bigViewImage.rectTransform.sizeDelta=size;
        bigViewImage.rectTransform.anchoredPosition=Vector2.Scale(pan,extent);
    }
    private void UpdatePresentation()
    {
        if(empty==null)return;
        empty.gameObject.SetActive(thumbnails.Count==0);
        if(selectedIndex>=0&&selectedIndex<thumbnails.Count)
        {
            var record=albumManager.FindRecord(thumbnails[selectedIndex].texture as Texture2D);
            caption.text=$"{selectedIndex+1} / {thumbnails.Count}   {record?.Caption??"Photograph"}";
            if(record!=null)caption.text+="\n"+record.CapturedAtUtc.ToString("HH:mm:ss 'UTC'");
        }
        else caption.text="Photo Album";
        controls.text=isBigViewOpen?$"Right stick: browse / zoom {zoom:0}×   Left stick: pan\nA: gallery   B: close":"Right stick: browse   A: inspect   B: close";
        int columns=GetColumnsPerRow();var grid=content.GetComponent<GridLayoutGroup>();
        int rows=grid!=null?Mathf.Max(1,Mathf.FloorToInt((content.rect.height-grid.padding.top-grid.padding.bottom+grid.spacing.y)/(grid.cellSize.y+grid.spacing.y))):1;
        int count=columns*rows;int first=Mathf.Max(0,selectedIndex)/count*count;
        for(int i=0;i<thumbnails.Count;i++)thumbnails[i].transform.parent.gameObject.SetActive(i>=first&&i<first+count);
    }
}
