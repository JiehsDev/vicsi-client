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

    /// <summary>Shows a toast notification. durationSeconds overrides the UI's default hold time if given.</summary>
    public static void Notify(string message, float? durationSeconds = null)
    {
        if (Instance == null)
        {
            Debug.LogWarning($"[NotificationManager] No NotificationManager.Instance in scene; dropped notification: \"{message}\"");
            return;
        }

        if (Instance.notificationUI == null)
        {
            Debug.LogWarning("[NotificationManager] No NotificationUI assigned.", Instance);
            return;
        }

        Instance.notificationUI.Show(message, durationSeconds);
    }

    /// <summary>Shows a message that stays up until HidePrompt() is called - e.g. a "[X] Pick Up ..." prompt while the player is in range of something.</summary>
    public static void ShowPrompt(string message)
    {
        if (Instance == null || Instance.notificationUI == null)
        {
            return;
        }

        Instance.notificationUI.ShowPrompt(message);
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
