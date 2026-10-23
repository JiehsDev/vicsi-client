using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>VR-native username keyboard, local rankings and complete run history.</summary>
public sealed class CompetitiveMenuUI : MonoBehaviour
{
    GameObject panel;
    RectTransform root, content;
    TMP_Text heading, description;
    WorldPanelPointer pointer;
    InputAction navigate, confirm, back;
    readonly List<(UnityEngine.UI.Image image, Action action)> buttons = new();
    string screen = "home", username;
    int selected, page;
    float nextMove, inputAfter;
    bool settingsOpen;
    bool holdsLocomotion;
    SettingsPauseMenu settings;
    CompetitiveEdition Edition => CompetitiveEdition.Instance;

    void Start()
    {
        FindAnyObjectByType<UIManager>()?.HideResearchPanels();
        foreach (var old in FindObjectsByType<StudentIdentificationPanel>(FindObjectsInactive.Include)) old.Hide();
        foreach (var old in FindObjectsByType<StartMenuController>()) old.HideResearchStatus();
        username = Edition.Username ?? "";
        panel = new GameObject("Competitive menu", typeof(RectTransform), typeof(Canvas));
        root = (RectTransform)panel.transform; root.sizeDelta = new Vector2(900, 760); root.localScale = Vector3.one * .001f;
        var canvas = panel.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.sortingOrder = 32000;
        // Share the lobby's furniture-mounted anchor, including its initial tracking
        // alignment. Keep every menu page here rather than following head movement.
        var lobbyAnchor = FindAnyObjectByType<LobbyCanvasPlacement>();
        if (lobbyAnchor != null)
        {
            root.SetParent(lobbyAnchor.transform, false);
            root.localScale = Vector3.one * (.001f / lobbyAnchor.transform.lossyScale.x);
        }
        else panel.AddComponent<LobbyCanvasPlacement>();
        var bg = ForensicUIStyle.Rect(root, "Background", ForensicUIStyle.PanelBackground);
        bg.rectTransform.sizeDelta = root.sizeDelta;
        content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>(); content.SetParent(root, false);
        heading = Text(root, "Title", 38, 330, 840, 60);
        description = Text(root, "Description", 24, 252, 830, 95);
        description.richText = false;
        pointer = new WorldPanelPointer(root, "CompetitiveMenu", this);
        navigate = new InputAction("CompetitiveNavigate", InputActionType.Value, "<XRController>{RightHand}/primary2DAxis");
        confirm = PlayerUIGate.CreateConfirmAction(this, "CompetitiveConfirm", () => panel != null && panel.activeSelf, 600);
        back = new InputAction("CompetitiveBack", InputActionType.Button, "<XRController>{RightHand}/secondaryButton");
        navigate.Enable(); confirm.Enable(); back.Enable();
        HoldMenu(); pointer.Enable(); Draw();
    }
    TMP_Text Text(Transform parent, string name, float size, float y, float width, float height)
    {
        var label = ForensicUIStyle.Text(parent, name, size, ForensicUIStyle.TextPrimary, alignment: TextAlignmentOptions.Center);
        label.rectTransform.sizeDelta = new Vector2(width, height); label.rectTransform.anchoredPosition = new Vector2(0, y);
        return label;
    }
    void Button(string label, float x, float y, float width, Action action, float height = 58)
    {
        var image = ForensicUIStyle.Rect(content, "Button", ForensicUIStyle.HeaderBackground);
        image.rectTransform.sizeDelta = new Vector2(width, height); image.rectTransform.anchoredPosition = new Vector2(x, y);
        var text = Text(image.transform, "Label", 25, 0, width - 12, height - 8); text.text = label;
        int index = buttons.Count; buttons.Add((image, action));
        pointer.Register("menu-" + index, image.rectTransform, Vector2.zero, () => Activate(index), hover => { if (hover) { selected = index; Highlight(); } });
    }
    void Draw(bool keepSelection = false)
    {
        int previousSelection = selected;
        pointer.Unregister("menu-"); buttons.Clear(); selected = 0; inputAfter = Time.unscaledTime + .25f;
        for (int i = content.childCount - 1; i >= 0; i--) { content.GetChild(i).gameObject.SetActive(false); Destroy(content.GetChild(i).gameObject); }
        heading.text = screen == "name" ? "Choose your username" : screen == "board" ? "Leaderboard" : screen == "history" ? "Run history" : "ViCSI · Competitive";
        description.text = "Local rankings · Higher scores first, faster time breaks ties.\nThumbstick: choose · A: select · B: back · Or point and trigger.";
        if (screen == "home")
        {
            description.text = "Investigate the scene, follow the evidence, and close the case.\nYour score and time are saved on this device.";
            Button("Start investigation", 0, 145, 650, () => { screen = "name"; Draw(); });
            Button("Leaderboard", 0, 65, 650, () => { screen = "board"; page = 0; Draw(); });
            Button("Run history", 0, -15, 650, () => { screen = "history"; page = 0; Draw(); });
            Button("Game settings", 0, -95, 650, OpenSettings);
            Button("Demo rankings: " + (Edition.ShowDemoRankings ? "On" : "Off"), 0, -175, 650,
                () => { Edition.SetDemoRankings(!Edition.ShowDemoRankings); Draw(); });
        }
        else if (screen == "name")
        {
            description.text = (username.Length > 0 ? username : "Enter a username") + "\n2–20 characters · Letters, numbers, spaces, - and _";
            string[] rows = { "ABCDEFGHIJ", "KLMNOPQRST", "UVWXYZ0123", "456789-_" };
            for (int row = 0; row < rows.Length; row++)
                for (int col = 0; col < rows[row].Length; col++)
                { char key = rows[row][col]; Button(key.ToString(), -360 + col * 80, 145 - row * 68, 70, () => Type(key)); }
            Button("Space", -240, -140, 230, () => Type(' '));
            Button("Delete", 0, -140, 230, () => { if (username.Length > 0) username = username[..^1]; Draw(true); });
            Button("Clear", 240, -140, 230, () => { username = ""; Draw(true); });
            Button("Begin investigation", 0, -220, 710, Begin);
        }
        else
        {
            var list = screen == "board" ? Edition.Leaderboard(CompetitiveEdition.RulesKey)
                : Edition.History.runs.Where(r => CompetitiveRanking.ValidRun(r)).OrderByDescending(r => r.completedUtc).ToList();
            if (screen == "board" && Edition.ShowDemoRankings)
                description.text = "Demo entries are labeled DEMO. Your best run ranks alongside them.\nHigher scores first; faster time breaks ties.";
            page = Mathf.Clamp(page, 0, Math.Max(0, (list.Count - 1) / 6));
            for (int i = 0; i < Math.Min(6, list.Count - page * 6); i++)
            {
                var run = list[page * 6 + i];
                var label = Text(content, "Run", 25, 145 - i * 53, 820, 48); label.richText = false;
                label.text = $"{page * 6 + i + 1}.  {run.username}{(run.isDemo ? " [DEMO]" : "")}     {run.score:0.##} / 100     {CompetitiveRanking.FormatTime(run.seconds)}";
            }
            if (list.Count == 0) Text(content, "Empty", 28, 30, 800, 80).text = "No completed runs yet. Finish an investigation to join the leaderboard.";
            Button("Previous", -210, -220, 250, () => { page = Math.Max(0, page - 1); Draw(); });
            Button("Next", 210, -220, 250, () => { page++; Draw(); });
        }
        if (screen != "home") Button("Back to menu", 0, -310, 650, Home);
        Text(content, "Storage", 21, screen == "home" ? -270 : -360, 820, 40).text = Edition.StorageMessage ?? "Local play · No account or course code required";
        if (keepSelection) selected = Mathf.Clamp(previousSelection, 0, buttons.Count - 1);
        Highlight();
    }
    void Type(char key) { if (username.Length < 20) username += key; Draw(true); }
    void Begin()
    {
        if (!Edition.SetUsername(username)) { description.text = "Use 2–20 characters, starting with a letter or number."; return; }
        ReleaseMenu(); SceneManager.LoadScene("CSI_Environment");
    }
    void Home() { screen = "home"; Draw(); }
    void OpenSettings()
    {
        panel.SetActive(false); pointer.Disable(); ReleaseMenu();
        settings = FindAnyObjectByType<SettingsPauseMenu>();
        if (settings == null) settings = new GameObject("Competitive game settings").AddComponent<SettingsPauseMenu>();
        settings.Show(); settingsOpen = true;
    }
    void Highlight() { for (int i = 0; i < buttons.Count; i++) buttons[i].image.color = i == selected ? ForensicUIStyle.SelectedFill : ForensicUIStyle.HeaderBackground; }
    void Activate(int index)
    {
        if (Time.unscaledTime < inputAfter || index < 0 || index >= buttons.Count || PlayerUIGate.IsBlockedByOther(this)) return;
        inputAfter = Time.unscaledTime + .2f; buttons[index].action();
    }
    void Update()
    {
        if (SceneManager.GetActiveScene().name != "Main") { Destroy(this); return; }
        if (panel == null) return;
        if (settingsOpen)
        {
            if (settings != null && settings.IsOpen) return;
            settingsOpen = false; panel.SetActive(true); HoldMenu(); pointer.Enable(); Draw();
        }
        pointer.Tick();
        if (back.WasPressedThisFrame() && screen != "home") Home();
        Vector2 move = navigate.ReadValue<Vector2>();
        if (move.magnitude > .65f && Time.unscaledTime >= nextMove && buttons.Count > 0)
        {
            int step = Mathf.Abs(move.x) > Mathf.Abs(move.y) ? (move.x > 0 ? 1 : -1) : (move.y > 0 ? -1 : 1);
            selected = (selected + step + buttons.Count) % buttons.Count; nextMove = Time.unscaledTime + .18f; Highlight();
        }
        if (confirm.WasPressedThisFrame()) Activate(selected);
    }
    void OnDestroy()
    {
        ReleaseMenu(); pointer?.Dispose(); navigate?.Dispose(); confirm?.Dispose(); back?.Dispose();
        if (panel != null) Destroy(panel);
    }
    void HoldMenu()
    {
        PlayerUIGate.Enter(this);
        if (!holdsLocomotion) { LocomotionSuspender.Suspend(); holdsLocomotion = true; }
    }
    void ReleaseMenu()
    {
        PlayerUIGate.Exit(this);
        if (holdsLocomotion) { LocomotionSuspender.Resume(); holdsLocomotion = false; }
    }
}
