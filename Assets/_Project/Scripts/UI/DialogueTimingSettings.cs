// Assets/_Project/Scripts/UI/DialogueTimingSettings.cs
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// What a timed dialogue/toast message IS, which decides how long it stays up and what
/// may cut it short. Declared lowest to highest priority: the int value is the priority.
/// </summary>
public enum DialogueKind
{
    /// <summary>"Evidence tent 1 placed." - a short report that something just worked.</summary>
    Confirmation = 0,
    /// <summary>Instructor/teammate guidance - the default for anything not otherwise tagged.</summary>
    Guidance = 1,
    /// <summary>Procedure went wrong but nothing was refused - wrong angle, out of sequence, smudged print.</summary>
    Warning = 2,
    /// <summary>A refused/blocked action or a failure - the only report the player gets that their input did nothing.</summary>
    Error = 3,
}

/// <summary>
/// The single place dialogue readability timing lives - NotificationUI (system toasts)
/// and STCSNotificationUI (teammate/narrative lines) both read this, nothing else
/// hardcodes a hold time. Loaded from Resources/DialogueTimingSettings; if that asset is
/// missing, an in-memory instance with these same defaults is used, so a missing asset
/// can never make messages vanish instantly.
///
/// Hold time = clamp(baseSeconds + characters / charactersPerSecond * readingSpeedMultiplier,
///                   kind minimum, kind maximum)
/// Characters exclude TMP rich-text tags. A message can never leave before its kind's
/// minimum (except by an explicit manual dismiss after dismissProtectionSeconds, or a
/// higher-priority message pre-empting a Confirmation after that same protection).
/// </summary>
[CreateAssetMenu(menuName = "ViCSI/Dialogue Timing Settings", fileName = "DialogueTimingSettings")]
public class DialogueTimingSettings : ScriptableObject
{
    private const string ResourcePath = "DialogueTimingSettings";

    [Header("Reading model")]
    [Tooltip("Fixed time added to every message before reading time - covers eyes finding the panel.")]
    public float baseSeconds = 2f;
    [Tooltip("Reading speed in characters per second (~15 cps is roughly 180 wpm).")]
    public float charactersPerSecond = 15f;
    [Tooltip(">1 gives every message more reading time, <1 less. Scales only the length-based part.")]
    public float readingSpeedMultiplier = 1f;

    [Header("Minimum hold per kind (seconds)")]
    public float confirmationMinSeconds = 3f;
    public float guidanceMinSeconds = 5f;
    public float warningMinSeconds = 6f;
    public float errorMinSeconds = 5f;

    [Header("Maximum hold (seconds)")]
    [Tooltip("Confirmations are short reports; they never stay up as long as instruction.")]
    public float confirmationMaxSeconds = 4f;
    [Tooltip("Upper bound for everything else, however long the text.")]
    public float maxSeconds = 11f;

    [Header("Presentation")]
    [Tooltip("Fade in / fade out time. Kept short and motion-free for VR.")]
    public float fadeSeconds = 0.2f;
    [Tooltip("A message cannot be manually dismissed (or pre-empted) until it has been on screen this long.")]
    public float dismissProtectionSeconds = 1f;
    [Tooltip("Input System binding that dismisses the current timed message after the protection period. Right thumbstick click is the only controller input no other system in this project binds.")]
    public string dismissBinding = "<XRController>{RightHand}/thumbstickClicked";
    [HideInInspector] // Legacy serialized value; content-fit panels no longer clip at this height.
    public float maxPanelHeight = 240f;
    [Tooltip("Vertical padding added around the label's preferred height when the panel grows.")]
    public float panelVerticalPadding = 40f;

    private static DialogueTimingSettings current;
    private static readonly Regex RichTextTag = new("<[^>]*>");

    /// <summary>The project's settings asset, or built-in defaults if none is present.</summary>
    public static DialogueTimingSettings Current
    {
        get
        {
            if (current == null)
            {
                current = Resources.Load<DialogueTimingSettings>(ResourcePath);
                if (current == null)
                {
                    current = CreateInstance<DialogueTimingSettings>();
                    current.hideFlags = HideFlags.DontSave;
                }
            }
            return current;
        }
    }

    public float MinSecondsFor(DialogueKind kind) => kind switch
    {
        DialogueKind.Confirmation => confirmationMinSeconds,
        DialogueKind.Warning => warningMinSeconds,
        DialogueKind.Error => errorMinSeconds,
        _ => guidanceMinSeconds,
    };

    public float MaxSecondsFor(DialogueKind kind) =>
        kind == DialogueKind.Confirmation ? Mathf.Min(confirmationMaxSeconds, maxSeconds) : maxSeconds;

    /// <summary>How long a message of this kind and text stays on screen (countdown length).</summary>
    public float DurationFor(DialogueKind kind, string text)
    {
        int characters = string.IsNullOrEmpty(text) ? 0 : RichTextTag.Replace(text, string.Empty).Trim().Length;
        float reading = characters / Mathf.Max(1f, charactersPerSecond) * Mathf.Max(0.1f, readingSpeedMultiplier);
        float min = MinSecondsFor(kind);
        float max = Mathf.Max(min, MaxSecondsFor(kind));
        return Mathf.Clamp(baseSeconds + reading, min, max);
    }
}
