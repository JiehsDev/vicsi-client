using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Utility-wheel settings; existing modal/placement owners and full scene reset, no alternate progress owner.</summary>
public sealed class SettingsPauseMenu : MonoBehaviour
{
    [SerializeField] private string title = "Settings";
    [SerializeField] private string heightTitle = "Player Height";
    [SerializeField] private string walkingSpeedTitle = "Walking Speed";
    [SerializeField] private string restartWarning = "Your current investigation progress will be reset.";
    [SerializeField] private string lobbyWarning = "Your current investigation session will end.";
    private const int Priority = 650;
    private const string NavigationHint = "Right stick: select   A / trigger: confirm   B: close";
    private GameObject panel;
    private RectTransform content;
    private RectTransform heightRow;
    private ResponsivePanelLayout responsiveLayout;
    public ResponsivePanelLayout ResponsiveLayout => responsiveLayout;
    private TMP_Text heightValue, message;
    private TMP_Text speedValue;
    private TMP_Text ambienceValue, sfxValue;
    private PlayerHeightOffset height;
    private PlayerWalkingSpeed walkingSpeed;
    private InputAction confirm, close, navigate;
    private WorldPanelPointer pointer;
    private readonly List<(Image image, Action action)> buttons = new();
    private int selected, openedFrame, activatedFrame = -1;
    private float navigateAfter;
    private string confirmation;
    private bool holdsModal;
    private int visibleSelection = -1;
    public bool IsOpen => panel != null && panel.activeSelf;
    public string Confirmation => confirmation;
    public WorldPanelPointer Pointer => pointer;
    public GameObject Panel => panel;
    public PlayerHeightOffset Height => height;
    public PlayerWalkingSpeed WalkingSpeed => walkingSpeed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetBootstrap() { SceneManager.sceneLoaded -= SceneLoaded; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap() { SceneManager.sceneLoaded += SceneLoaded; EnsureMenu(); }
    private static void SceneLoaded(Scene scene, LoadSceneMode mode) => EnsureMenu();
    private static void EnsureMenu()
    {
        if (SceneTurnover.Controller != null && Object.FindAnyObjectByType<SettingsPauseMenu>() == null)
            EditableSceneVisuals.Component<SettingsPauseMenu>(SceneTurnover.Controller.gameObject);
    }
    [EditableSceneBuilder]
    private void BuildShell()
    {
        using (EditableSceneVisuals.Begin(this,"BuildShell"))
        {
        panel = EditableSceneVisuals.Create("SettingsPauseCanvas", typeof(RectTransform), typeof(Canvas));
        panel.transform.SetParent(transform, false);
        panel.transform.localScale = Vector3.one * .001f;
        var rect = (RectTransform)panel.transform;
        rect.sizeDelta = new Vector2(620, 580);
        var canvas = panel.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true; canvas.sortingOrder = short.MaxValue;
        var follow = HudFollowCamera.ConfigureShared(panel, this, Priority);
        follow.Distance = 1.05f; follow.LocalOffset = new Vector3(0, -.12f, 0);
        follow.MinimumDistance = .65f; follow.WallClearance = .12f;
        var background = ForensicUIStyle.Rect(rect, "Background", ForensicUIStyle.PanelBackground).rectTransform;
        background.anchorMin = Vector2.zero; background.anchorMax = Vector2.one; background.sizeDelta = Vector2.zero;
        content = EditableSceneVisuals.Create("Content", typeof(RectTransform)).GetComponent<RectTransform>();
        content.SetParent(rect, false); content.sizeDelta = rect.sizeDelta;
        responsiveLayout = ResponsivePanelLayout.Create(rect, null, content, null, 580, 740, 40);
        pointer = new WorldPanelPointer(rect, "Settings", this);
        responsiveLayout.RegisterControls(pointer, "layout-settings-");
        panel.SetActive(false);
        }
    }
    private void Awake()
    {
        walkingSpeed = PlayerWalkingSpeed.EnsureOwner();
        height = Object.FindAnyObjectByType<PlayerHeightOffset>();
        if (height == null)
        {
            var rig = Object.FindAnyObjectByType<OVRCameraRig>();
            if (rig != null) height = EditableSceneVisuals.Component<PlayerHeightOffset>(rig.gameObject);
        }
        BuildShell();
        confirm = PlayerUIGate.CreateConfirmAction(this, "Settings_A", () => IsOpen, Priority);
        close = new InputAction("Settings_B", InputActionType.Button, "<XRController>{RightHand}/secondaryButton");
        navigate = new InputAction("Settings_Select", InputActionType.Value, "<XRController>{RightHand}/primary2DAxis");
        panel.SetActive(false);
    }
    private void Start() => Object.FindAnyObjectByType<UtilityMenuController>()?.RegisterSettings(this);
    private void OnEnable() { confirm.Enable(); close.Enable(); navigate.Enable(); }
    private void OnDisable() { Close(); confirm.Disable(); close.Disable(); navigate.Disable(); }
    private void OnDestroy() { confirm.Dispose(); close.Dispose(); navigate.Dispose(); pointer.Dispose(); }
    public void Show()
    {
        if (IsOpen || PlayerUIGate.IsBlocked) return;
        openedFrame = Time.frameCount;
        panel.SetActive(true);
        PlayerUIGate.Enter(this); ToolInputLock.Enter(this); LocomotionSuspender.Suspend();
        holdsModal = true;
        pointer.Enable(); BuildSettings();
    }
    public void Close()
    {
        pointer?.Disable();
        if (panel != null) panel.SetActive(false);
        confirmation = null;
        if (!holdsModal) return;
        holdsModal = false;
        PlayerUIGate.Exit(this); ToolInputLock.Exit(this); LocomotionSuspender.Resume();
    }
    private void Update()
    {
        if (!IsOpen || Time.frameCount <= openedFrame) return;
        // A withheld panel must remain escapable even when geometry suppresses its canvas.
        if (close.WasPressedThisFrame() && Equals(PlayerUIGate.ConfirmOwnerToken, this))
        { if (confirmation != null) Cancel(); else Close(); return; }
        if (!PlayerUIGate.CanUsePanel(this)) return;
        pointer.Tick();
        if (!IsOpen) return;
        Vector2 stick = navigate.ReadValue<Vector2>();
        if (Mathf.Abs(stick.x) > .5f) responsiveLayout.ScrollOptions(-stick.x * 240 * Time.unscaledDeltaTime);
        if (Mathf.Abs(stick.y) > .55f && Time.unscaledTime >= navigateAfter)
        {
            selected = (selected + (stick.y < 0 ? 1 : buttons.Count - 1)) % buttons.Count;
            navigateAfter = Time.unscaledTime + .25f; RefreshSelection();
        }
        if (PlayerUIGate.TryConsumeConfirm(this, confirm)) Activate(selected);
    }
    private void Activate(int index)
    {
        if (!IsOpen || Time.frameCount <= openedFrame || activatedFrame == Time.frameCount || !PlayerUIGate.CanUsePanel(this)) return;
        activatedFrame = Time.frameCount;
        buttons[index].action();
    }
    private void ClearContent()
    {
        pointer.Unregister("settings-"); buttons.Clear();
        foreach (var area in new[] { responsiveLayout.Header, responsiveLayout.Body, responsiveLayout.Footer })
            for (int i = area.Content.childCount - 1; i >= 0; i--)
            { area.Content.GetChild(i).gameObject.SetActive(false); EditableSceneVisuals.Retire(area.Content.GetChild(i).gameObject); }
        selected = 0;
        visibleSelection = -1;
    }
    private TMP_Text Label(string name, string text, float y, float size = 29)
    {
        var label = ForensicUIStyle.Text(content, name, size, ForensicUIStyle.TextPrimary, alignment: TextAlignmentOptions.Center);
        label.text = text; label.rectTransform.sizeDelta = new Vector2(550, 62); label.rectTransform.anchoredPosition = new Vector2(0, y);
        responsiveLayout.AddText(label, name == "Title" ? responsiveLayout.Header : name == "Hint" ? responsiveLayout.Footer : responsiveLayout.Body);
        return label;
    }
    private RectTransform Button(string id, string text, float y, Action action, float x = 0, float width = 540, RectTransform row = null)
    {
        var image = ForensicUIStyle.Rect(content, id, ForensicUIStyle.HeaderBackground);
        image.rectTransform.sizeDelta = new Vector2(width, 54); image.rectTransform.anchoredPosition = new Vector2(x, y);
        var label = ForensicUIStyle.Text(image.transform, "Label", 28, ForensicUIStyle.TextPrimary, alignment: TextAlignmentOptions.Center);
        label.text = text; label.rectTransform.sizeDelta = image.rectTransform.sizeDelta;
        if (width < 540) image.transform.SetParent(row != null ? row : heightRow, false);
        else responsiveLayout.AddOption(image.rectTransform, label, 54);
        int index = buttons.Count; buttons.Add((image, action));
        pointer.Register("settings-" + id, image.rectTransform, new Vector2(4, 4), () => Activate(index), hovered => { if (hovered) { selected = index; RefreshSelection(); } });
        return image.rectTransform;
    }
    private void RefreshSelection()
    {
        for (int i = 0; i < buttons.Count; i++) buttons[i].image.color = i == selected ? ForensicUIStyle.SelectedFill : ForensicUIStyle.HeaderBackground;
        if (buttons.Count > 0 && selected != visibleSelection) responsiveLayout.EnsureVisible(buttons[selected].image.rectTransform);
        visibleSelection = selected;
    }
    [EditableSceneBuilder]
    private void BuildSettings()
    {
        var rows = new List<Transform>();
        using (EditableSceneVisuals.Begin(this,"BuildSettings",true))
        {
        confirmation = null; ClearContent();
        Label("Title", title, 237, 36); rows.Add(Label("HeightTitle", heightTitle, 168, 27).transform.parent);
        heightRow = ResponsivePanelLayout.Rect(content, "HeightControls");
        rows.Add(heightRow);
        heightRow.sizeDelta = new Vector2(540, 62);
        var fixedLayout = EditableSceneVisuals.Component<LayoutElement>(heightRow.gameObject); fixedLayout.minHeight = fixedLayout.preferredHeight = 62;
        heightValue = ForensicUIStyle.Text(heightRow, "HeightValue", 34, ForensicUIStyle.TextPrimary, alignment: TextAlignmentOptions.Center);
        heightValue.rectTransform.sizeDelta = new Vector2(260, 62);
        Button("minus", "-", 0, () => ChangeHeight(-1), -216, 108);
        Button("plus", "+", 0, () => ChangeHeight(1), 216, 108);
        rows.Add(Button("reset", "Reset Height", 32, ResetHeight));
        rows.Add(Label("WalkingSpeedTitle", walkingSpeedTitle, 0, 27).transform.parent);
        var speedRow = ResponsivePanelLayout.Rect(content, "WalkingSpeedControls");
        rows.Add(speedRow);
        speedRow.sizeDelta = new Vector2(540, 62);
        var speedLayout = EditableSceneVisuals.Component<LayoutElement>(speedRow.gameObject);
        speedLayout.minHeight = speedLayout.preferredHeight = 62;
        speedValue = ForensicUIStyle.Text(speedRow, "WalkingSpeedValue", 34, ForensicUIStyle.TextPrimary, alignment: TextAlignmentOptions.Center);
        speedValue.rectTransform.sizeDelta = new Vector2(260, 62);
        Button("speed-minus", "-", 0, () => ChangeSpeed(-1), -216, 108, speedRow);
        Button("speed-plus", "+", 0, () => ChangeSpeed(1), 216, 108, speedRow);
        rows.Add(Button("speed-reset", "Reset Walking Speed", 0, ResetSpeed));
        AddVolumeControls(rows, "Ambience Volume", "ambience", out ambienceValue);
        AddVolumeControls(rows, "SFX Volume", "sfx", out sfxValue);
        rows.Add(Button("audio-reset", "Reset Sound Volumes", 0, ResetAudio));
        rows.Add(Button("restart", SceneTestStart.IsActive ? "Restart selected test" : "Restart Scenario", -36, RequestRestart));
        rows.Add(Button("lobby", "Back to Main Lobby", -104, RequestLobbyReturn));
        rows.Add(Button("close", "Close", -172, Close));
        message = Label("Hint", NavigationHint, -242, 20);
        responsiveLayout.Refresh();
        selected = 0;
            }
        for (int i = 0; i < rows.Count; i++) rows[i].SetSiblingIndex(i);
        responsiveLayout.Refresh();
        message.text = NavigationHint;
        RefreshHeight(); RefreshSpeed(); RefreshAudio(); RefreshSelection();
    }
    private void AddVolumeControls(List<Transform> rows, string label, string id, out TMP_Text value)
    {
        rows.Add(Label(id + "-title", label, 0, 27).transform.parent);
        var row = ResponsivePanelLayout.Rect(content, id + "-controls"); rows.Add(row);
        row.sizeDelta = new Vector2(540, 62);
        var layout = EditableSceneVisuals.Component<LayoutElement>(row.gameObject); layout.minHeight = layout.preferredHeight = 62;
        value = ForensicUIStyle.Text(row, id + "-value", 34, ForensicUIStyle.TextPrimary, alignment: TextAlignmentOptions.Center);
        value.rectTransform.sizeDelta = new Vector2(260, 62);
        Button(id + "-minus", "-", 0, () => ChangeAudio(id == "ambience", -1), -216, 108, row);
        Button(id + "-plus", "+", 0, () => ChangeAudio(id == "ambience", 1), 216, 108, row);
    }
    private void ChangeAudio(bool ambience, int direction)
    {
        if (ambience) GameAudioSettings.SetAmbience(GameAudioSettings.AmbienceVolume + direction * .1f);
        else GameAudioSettings.SetSfx(GameAudioSettings.SfxVolume + direction * .1f);
        RefreshAudio(); message.text = "Sound volume saved on this headset.";
    }
    private void ResetAudio()
    {
        GameAudioSettings.ResetVolumes(); RefreshAudio(); message.text = "Sound volumes reset to 100%.";
    }
    private void RefreshAudio()
    {
        if (ambienceValue != null) ambienceValue.text = Mathf.RoundToInt(GameAudioSettings.AmbienceVolume * 100) + "%";
        if (sfxValue != null) sfxValue.text = Mathf.RoundToInt(GameAudioSettings.SfxVolume * 100) + "%";
    }
    private void ChangeHeight(int direction)
    {
        bool changed = height != null && height.AdjustHeight(direction * height.HeightStep);
        RefreshHeight(); message.text = changed ? "Height saved on this headset." : "Not enough overhead space to raise height.";
    }
    private void ResetHeight()
    {
        bool reset = height != null && height.ResetHeight(); RefreshHeight();
        message.text = reset ? "Height reset to headset calibration." : "Not enough overhead space to reset height.";
    }
    private void RefreshHeight() { if (heightValue != null) heightValue.text = height != null ? height.CurrentHeight.ToString("0.00") + " m" : "Unavailable"; }
    private void ChangeSpeed(int direction)
    {
        bool changed = walkingSpeed != null && walkingSpeed.AdjustSpeed(direction * walkingSpeed.SpeedStep);
        RefreshSpeed();
        message.text = changed ? "Walking speed saved on this headset." : "Walking speed is unavailable.";
    }
    private void ResetSpeed()
    {
        if (walkingSpeed != null) walkingSpeed.ResetSpeed();
        RefreshSpeed();
        message.text = walkingSpeed != null ? "Walking speed reset to 100%." : "Walking speed is unavailable.";
    }
    private void RefreshSpeed()
    {
        if (speedValue != null) speedValue.text = walkingSpeed != null ? (walkingSpeed.CurrentMultiplier * 100).ToString("0") + "%" : "Unavailable";
    }
    public void RequestRestart() => ShowConfirmation("restart", SceneTestStart.IsActive ? "Restart selected test?" : "Restart Scenario?",
        SceneTestStart.IsActive ? "Reload this test starting point with fresh procedure state. Assessment uploads remain disabled." : restartWarning, "Restart");
    public void RequestLobbyReturn() => ShowConfirmation("lobby", "Return to Main Lobby?", lobbyWarning, "Return");
    private void ShowConfirmation(string kind, string heading, string warning, string action)
    {
        using (EditableSceneVisuals.Begin(this,"Confirmation-"+kind,true))
        {
        if (!IsOpen) return;
        ClearContent(); confirmation = kind;
        Label("Title", heading, 182, 36); Label("Warning", warning, 70, 28);
        Button("confirm", action, -65, ConfirmNavigation);
        Button("cancel", "Cancel", -140, Cancel);
        responsiveLayout.Refresh();
        selected = 1; RefreshSelection(); openedFrame = Time.frameCount;
        if(Application.isPlaying){pointer.Disable(); pointer.Enable();}
            }
    }
    public void Cancel()
    {
        if (!IsOpen) return;
        BuildSettings(); openedFrame = Time.frameCount; pointer.Disable(); pointer.Enable();
    }
    private void ConfirmNavigation()
    {
        string destination = confirmation;
        if (destination == "lobby" && Object.FindAnyObjectByType<CompletionScreenUI>() == null) return;
        HeldItemCoordinator.Instance?.HolsterCurrentTool();
        Close();
        if (destination == "restart")
        {
            if (SceneTestStart.IsActive && SceneTestStart.Instance != null) SceneTestStart.Instance.RestartSelectedTest();
            else SceneManager.LoadSceneAsync(gameObject.scene.path); // existing full-reload reset contract
        }
        else if (destination == "lobby")
            Object.FindAnyObjectByType<CompletionScreenUI>().ReturnToLobbyFromSettings();
    }
}
