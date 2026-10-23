// Assets/_Project/Scripts/UI/NotificationManager.cs
using UnityEngine;

/// <summary>
/// Scene-wide entry point for showing a toast notification, the same shape as
/// EvidenceStateManager.Instance / STCSManager.Instance elsewhere in this project.
/// Any script anywhere can call NotificationManager.Notify("...") to surface a
/// message - a photo taken, evidence logged, a tool running out of a resource,
/// whatever - without needing a reference to this manager, the UI it drives, or
/// any other event source. Safe to call even before this manager exists in the
/// scene (logs a warning instead of throwing), same contract as
/// PlayerTool.ReportEvidence.
/// </summary>
public class NotificationManager : MonoBehaviour
{
    public static NotificationManager Instance { get; private set; }

    [SerializeField] private NotificationUI notificationUI;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>The lower panel: the tooltip channel (GameDialogue routes tooltips here) and ambient prompts.</summary>
    public NotificationUI UI => notificationUI;

    /// <summary>
    /// Shows an already-composed message. Player-facing text belongs in
    /// ViCSIDialogueCatalog - call GameDialogue.Say(id) instead. This remains for text
    /// resolved through GameDialogue.Text (gate reasons), which keeps its catalog
    /// channel/timing, and for scenario-authored data text that isn't catalog content.
    /// </summary>
    public static void Notify(string message, DialogueKind kind = DialogueKind.Guidance)
    {
        GameDialogue.ShowText(message, kind);
    }

    /// <summary>Shows a message that stays up until HidePrompt() is called - e.g. a "[X] Pick Up ..." prompt while the player is in range of something.</summary>
    public static void ShowPrompt(string message) => ShowPrompt(message, null);

    public static void ShowPrompt(string message, Vector3? cameraOffset)
    {
        if (Instance == null || Instance.notificationUI == null)
        {
            return;
        }

        Instance.notificationUI.ShowPrompt(EvidenceDisplay.Text(message), cameraOffset);
    }

    /// <summary>Hides the current prompt shown via ShowPrompt(), if any.</summary>
    public static void HidePrompt()
    {
        if (Instance == null || Instance.notificationUI == null)
        {
            return;
        }

        Instance.notificationUI.HidePrompt();
    }
}
