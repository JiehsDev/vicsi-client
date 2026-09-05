// Assets/_Project/Scripts/UI/MasterSketchUI.cs
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Displays the one shared master sketch that MasterSketchManager accumulates
/// annotations into. Same review-screen contract as PhotoAlbumUI - Show()/Hide()/
/// Toggle(), PlayerUIGate so it never stacks on top of another exclusive screen,
/// LocomotionSuspender while open, right controller's B button always closes it - and
/// the same "subscribe, then catch up on anything recorded before this UI existed"
/// pattern PhotoAlbumUI uses for photos taken before the album was opened once.
///
/// Where it deliberately DIFFERS from PhotoAlbumUI: there is no artist-authorable
/// background to place ahead of time (a photo has a real captured image; a crime-scene
/// sketch here is explicitly schematic, not a rendered floor plan), so this panel is
/// built at runtime the way UtilityMenuController builds its wheel, rather than being
/// hand-laid-out UGUI in a prefab. A plain bordered rectangle stands in for the room;
/// each annotation is a small numbered dot placed by its normalized position within
/// that rectangle. This is intentionally not a minimap and not scored - it exists to
/// show that one shared document, not five disconnected per-item sketches.
/// </summary>
public class MasterSketchUI : MonoBehaviour
{
    private const float PanelWidth = 640f;
    private const float PanelHeight = 480f;
    private const float MarkerSize = 44f;

    [SerializeField] private MasterSketchManager sketchManager;

    private GameObject panelRoot;
    private RectTransform boardRect;
    private InputAction closeAction;
    private bool isOpen;

    private readonly Dictionary<string, RectTransform> markers = new();

    private void Awake()
    {
        if (sketchManager == null)
        {
            sketchManager = MasterSketchManager.Instance != null ? MasterSketchManager.Instance : FindFirstObjectByType<MasterSketchManager>();
        }

        BuildUI();
        panelRoot.SetActive(false);

        // Same button PhotoAlbumUI closes on - always available to close whichever
        // exclusive screen is currently up, regardless of which one it is.
        closeAction = new InputAction("MasterSketch_Close", InputActionType.Button, "<XRController>{RightHand}/secondaryButton");
    }

    private void OnEnable()
    {
        closeAction.Enable();

        if (sketchManager == null)
        {
            return;
        }

        sketchManager.OnAnnotationAdded += HandleAnnotationAdded;

        // Catch up on anything annotated before this UI existed/was enabled - same
        // reasoning as PhotoAlbumUI replaying albumManager.Photos on enable.
        foreach (var annotation in sketchManager.Annotations)
        {
            HandleAnnotationAdded(annotation);
        }
    }

    private void OnDisable()
    {
        closeAction.Disable();
        PlayerUIGate.Exit(this);

        if (sketchManager != null)
        {
            sketchManager.OnAnnotationAdded -= HandleAnnotationAdded;
        }
    }

    private void OnDestroy()
    {
        closeAction?.Dispose();
    }

    private void Update()
    {
        if (!isOpen)
        {
            return;
        }

        if (closeAction.WasPressedThisFrame())
        {
            Hide();
        }
    }

    private void HandleAnnotationAdded(SketchAnnotation annotation)
    {
        if (!markers.TryGetValue(annotation.evidenceId, out var marker) || marker == null)
        {
            marker = CreateMarker();
            markers[annotation.evidenceId] = marker;
        }

        // anchorMin/anchorMax pinned to the normalized position (parent stretches over
        // the whole board), so placing a marker is just setting its anchor - no manual
        // size-multiplication to keep in sync if the board is ever resized.
        marker.anchorMin = marker.anchorMax = annotation.normalizedPosition;

        var label = marker.GetComponentInChildren<TMP_Text>();
        if (label != null)
        {
            label.text = annotation.tentNumber > 0 ? annotation.tentNumber.ToString() : "?";
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
        if (!panelRoot.activeSelf && PlayerUIGate.IsBlocked)
        {
            return;
        }

        if (!panelRoot.activeSelf)
        {
            LocomotionSuspender.Suspend();
            PlayerUIGate.Enter(this);
        }
        panelRoot.SetActive(true);
        isOpen = true;
    }

    public void Hide()
    {
        if (panelRoot.activeSelf)
        {
            LocomotionSuspender.Resume();
            PlayerUIGate.Exit(this);
        }
        panelRoot.SetActive(false);
        isOpen = false;
    }

    // --- UI construction ---------------------------------------------------

    private void BuildUI()
    {
        panelRoot = new GameObject("MasterSketchCanvas", typeof(RectTransform), typeof(Canvas));
        panelRoot.transform.SetParent(transform, false);

        var canvas = panelRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = short.MaxValue;

        var rootRect = panelRoot.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(PanelWidth, PanelHeight + 60f);
        panelRoot.transform.localScale = Vector3.one * 0.0012f;

        var hud = panelRoot.AddComponent<HudFollowCamera>();
        hud.Distance = 0.6f;

        var backdrop = CreateImage(rootRect, "Backdrop", new Vector2(PanelWidth, PanelHeight + 60f));
        backdrop.color = new Color(0.05f, 0.05f, 0.05f, 0.92f);

        var title = CreateLabel(rootRect, "Title", new Vector2(0f, PanelHeight * 0.5f + 28f), new Vector2(PanelWidth, 40f), 24f);
        title.text = "MASTER SKETCH";
        title.color = new Color(1f, 1f, 1f, 0.85f);

        // The board: a bordered rectangle standing in for the room, with its own
        // stretched child RectTransform that every marker anchors into by normalized
        // position. Border drawn as a slightly larger backing rect showing through a
        // 4px inset, since Image doesn't have a stroke-only mode without a 9-sliced
        // sprite.
        var boardBorder = CreateImage(rootRect, "BoardBorder", new Vector2(PanelWidth - 40f, PanelHeight - 40f));
        boardBorder.color = new Color(1f, 1f, 1f, 0.6f);

        var boardGO = new GameObject("Board", typeof(RectTransform), typeof(Image));
        boardRect = boardGO.GetComponent<RectTransform>();
        boardRect.SetParent(boardBorder.rectTransform, false);
        boardRect.anchorMin = Vector2.zero;
        boardRect.anchorMax = Vector2.one;
        boardRect.offsetMin = new Vector2(4f, 4f);
        boardRect.offsetMax = new Vector2(-4f, -4f);
        boardGO.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.1f, 1f);

        var bottomHint = CreateLabel(rootRect, "BottomHint", new Vector2(0f, -(PanelHeight * 0.5f + 28f)), new Vector2(PanelWidth, 30f), 16f);
        bottomHint.text = "[B] CLOSE";
        bottomHint.color = new Color(1f, 1f, 1f, 0.6f);
    }

    private RectTransform CreateMarker()
    {
        var go = new GameObject("Marker", typeof(RectTransform), typeof(Image));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(boardRect, false);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(MarkerSize, MarkerSize);
        rect.anchoredPosition = Vector2.zero;

        var image = go.GetComponent<Image>();
        image.color = new Color(0.95f, 0.85f, 0.2f, 0.95f);
        image.sprite = CreateDiscSprite(64);

        var labelGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        var labelRect = labelGO.GetComponent<RectTransform>();
        labelRect.SetParent(rect, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;

        var label = labelGO.GetComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 22f;
        label.fontStyle = FontStyles.Bold;
        label.color = Color.black;
        label.text = "?";

        return rect;
    }

    private static Image CreateImage(RectTransform parent, string name, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = Vector2.zero;
        return go.GetComponent<Image>();
    }

    private static TMP_Text CreateLabel(RectTransform parent, string name, Vector2 anchoredPosition, Vector2 size, float fontSize)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;

        var text = go.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        return text;
    }

    // Simple filled circle sprite, same generation shape as UtilityMenuController's
    // ring sprite - no shipped art asset needed for a schematic marker dot.
    private static Sprite CreateDiscSprite(int diameter)
    {
        var texture = new Texture2D(diameter, diameter, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        float radius = diameter / 2f;
        var pixels = new Color32[diameter * diameter];
        for (int y = 0; y < diameter; y++)
        {
            for (int x = 0; x < diameter; x++)
            {
                float dx = x + 0.5f - radius;
                float dy = y + 0.5f - radius;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01((radius - dist) / 2f);
                pixels[y * diameter + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, diameter, diameter), new Vector2(0.5f, 0.5f), 100f);
    }
}
