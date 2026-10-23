// Assets/_Project/Scripts/Testing/DialogueHistoryTest.cs
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

/// <summary>
/// Play-mode checks for DialogueHistoryManager / DialogueHistoryUI, driven through the
/// real display paths (STCSNotificationUI.ShowLine, NotificationManager.Notify,
/// NarrativeEventManager.Fire) rather than by calling the recorder directly.
/// </summary>
public class DialogueHistoryTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }

    private readonly List<string> failures = new();
    private readonly StringBuilder report = new();

    private static readonly string[] RetiredStoryWords = { "affair", "glove", "phone" };

    public void RunAllTests()
    {
        IsDone = false;
        StartCoroutine(Run());
    }

    private void Check(string label, bool ok)
    {
        report.AppendLine((ok ? "  PASS: " : "  FAIL: ") + label);
        if (!ok)
        {
            failures.Add(label);
        }
    }

    private IEnumerator Run()
    {
        report.AppendLine("=== DialogueHistoryTest ===");
        var manager = DialogueHistoryManager.Instance;
        var ui = DialogueHistoryUI.Instance;
        var lineUi = FindFirstObjectByType<STCSNotificationUI>(FindObjectsInactive.Include);
        if (manager == null || ui == null || lineUi == null)
        {
            Report = "FAIL: DialogueHistoryManager / DialogueHistoryUI / STCSNotificationUI missing.";
            IsDone = true;
            yield break;
        }

        // --- Session: only this session's lines, numbered from 1, in order ---
        var existing = manager.Entries;
        bool ordered = true;
        for (int i = 1; i < existing.Count; i++)
        {
            ordered &= existing[i].sequence > existing[i - 1].sequence;
        }
        Check($"[session] history holds only this session's lines ({existing.Count} so far), numbered from 1 in order",
            ordered && (existing.Count == 0 || existing[0].sequence == 1));

        // --- Record a displayed line once, however long it stays up ---
        int before = manager.Entries.Count;
        lineUi.HideImmediately(); // queued lines wait for the one on screen; these checks want it shown now
        lineUi.ShowLine("History test line A.", null, "Team", "test-a");
        Check("[record] a shown line adds exactly one entry", manager.Entries.Count == before + 1);
        var a = Last(manager);
        Check("[record] entry keeps text, channel label and source id (no invented speaker)",
            a.text == "History test line A." && a.speaker == string.Empty && a.SourceLabel == "Team" && a.sourceId == "test-a");
        for (int i = 0; i < 30; i++)
        {
            yield return null;
        }
        Check("[duplicate] still one entry after the line stayed visible for 30 frames", manager.Entries.Count == before + 1);

        // Same presentation reaching the recorder twice in one frame -> one entry.
        int beforeDouble = manager.Entries.Count;
        manager.Add("History test double.", null, "Team", "test-double");
        manager.Add("History test double.", null, "Team", "test-double");
        Check("[duplicate] the same presentation recorded twice in one frame is one entry", manager.Entries.Count == beforeDouble + 1);
        yield return null;

        // --- Ordering A -> B -> C ---
        lineUi.HideImmediately(); // queued lines wait for the one on screen; these checks want it shown now
        lineUi.ShowLine("History test line B.", null, "Team", "test-b");
        yield return null;
        lineUi.HideImmediately(); // queued lines wait for the one on screen; these checks want it shown now
        lineUi.ShowLine("History test line C.", null, "Team", "test-c");
        yield return null;
        var n = manager.Entries.Count;
        Check("[order] B then C appear after A in presentation order",
            manager.Entries[n - 2].text == "History test line B." && manager.Entries[n - 1].text == "History test line C."
            && manager.Entries[n - 2].sequence < manager.Entries[n - 1].sequence);

        // --- An intentional later repeat is its own entry ---
        int beforeRepeat = manager.Entries.Count;
        lineUi.HideImmediately(); // queued lines wait for the one on screen; these checks want it shown now
        lineUi.ShowLine("History test line A.", null, "Team", "test-a");
        Check("[repeat] the same line shown again later is a new entry", manager.Entries.Count == beforeRepeat + 1);

        // --- Speaker kept separately; on-screen dialogue is the spoken sentence ---
        lineUi.HideImmediately(); // queued lines wait for the one on screen; these checks want it shown now
        lineUi.ShowLine("Speaker test line.", "Witness", "Narrative", "test-speaker");
        var label = (TMPro.TMP_Text)typeof(STCSNotificationUI).GetField("label", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(lineUi);
        Check("[speaker] screen omits the prefix; history keeps speaker and line apart",
            label != null && label.text == "Speaker test line." && Last(manager).speaker == "Witness" && Last(manager).text == "Speaker test line.");
        yield return null;

        // --- Authored inline speaker ("Name: line") becomes the label; times/colons later on don't ---
        lineUi.HideImmediately(); // queued lines wait for the one on screen; these checks want it shown now
        lineUi.ShowLine("First Responder: History inline speaker line.", null, "Team", "test-inline");
        var inline = Last(manager);
        Check("[speaker] an authored 'First Responder: ...' prefix becomes the label, the rest the text",
            label.text == "History inline speaker line." && inline.speaker == "First Responder" && inline.text == "History inline speaker line." && inline.SourceLabel == "First Responder");
        yield return null;
        lineUi.HideImmediately(); // queued lines wait for the one on screen; these checks want it shown now
        lineUi.ShowLine("Arrived at 11:42: scene secured.", null, "Team", "test-time");
        Check("[speaker] a line starting with a time is not split into a fake speaker",
            Last(manager).speaker == string.Empty && Last(manager).text == "Arrived at 11:42: scene secured.");
        yield return null;

        // --- Real narrative event through NarrativeEventManager ---
        var narrative = NarrativeEventManager.Instance;
        if (narrative != null && !narrative.IsCompleted("beat-2-body-doubt"))
        {
            lineUi.HideImmediately(); // so the beat displays (and is recorded) now, not behind the previous test line
            int beforeNarrative = manager.Entries.Count;
            narrative.Fire("beat-2-body-doubt");
            float t = 0f;
            while (!narrative.IsCompleted("beat-2-body-doubt") && t < 30f)
            {
                t += Time.deltaTime;
                yield return null;
            }
            // Narrative events play one at a time and their lines share the dialogue queue with
            // the opening briefing, so the beat's lines can still be waiting behind earlier lines
            // when the event completes: skip ahead until one of them has been shown.
            bool found = false;
            t = 0f;
            while (!found && t < 60f)
            {
                for (int i = beforeNarrative; i < manager.Entries.Count; i++)
                {
                    found |= manager.Entries[i].category == "Narrative" && manager.Entries[i].sourceId == "beat-2-body-doubt";
                }
                if (!found) { lineUi.Skip(); t += Time.deltaTime; yield return null; }
            }
            Check("[narrative] a real narrative beat is recorded with its event id", found);
        }

        // --- System toast: recorded when it reaches the screen ---
        // System guidance now shares the dialogue queue (ViCSIDialogueCatalog): clear it so the
        // toast is shown now rather than after the opening briefing.
        lineUi.HideImmediately();
        int beforeToast = manager.Entries.Count;
        NotificationManager.Notify("History test toast.");
        float waited = 0f;
        while (!Contains(manager, "History test toast.", beforeToast) && waited < 30f)
        {
            waited += Time.deltaTime;
            yield return null;
        }
        Check("[toast] a system toast is recorded (category System) once it is displayed", Contains(manager, "History test toast.", beforeToast)
            && Last(manager, "History test toast.").category == "System");

        // --- Missed dialogue: the popup has faded but the line is still readable ---
        lineUi.HideImmediately(); // queued lines wait for the one on screen; these checks want it shown now
        lineUi.ShowLine("History test missed line.", null, "Team", "test-missed");
        var group = (CanvasGroup)typeof(STCSNotificationUI).GetField("canvasGroup", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(lineUi);
        waited = 0f;
        while (group != null && group.alpha > 0f && waited < 15f)
        {
            waited += Time.deltaTime;
            yield return null;
        }
        Check("[missed] after the popup hides, the exact line is still in history",
            group != null && group.alpha == 0f && Contains(manager, "History test missed line.", 0));

        // --- Panel open / scroll / close, with a tool held ---
        var rig = FindFirstObjectByType<OVRCameraRig>();
        if (PlayerToolRegistry.VirtuallyEquippedRole != ToolType.Measurement && HeldItemCoordinator.Instance != null && rig != null)
        {
            HeldItemCoordinator.Instance.ToggleEquipTool(ToolType.Measurement, rig.rightHandAnchor);
        }
        var equippedBefore = PlayerToolRegistry.VirtuallyEquippedRole;
        ui.Show();
        yield return null;
        Check("[panel] opens: UI gate, locomotion hold and tool lock taken", ui.IsShowing && PlayerUIGate.IsBlocked && ToolInputLock.IsLocked);
        Check($"[panel] one row per entry ({ui.RowCount} rows / {manager.Entries.Count} entries)", ui.RowCount == manager.Entries.Count);
        Check("[panel] opens scrolled to the newest line", ui.VerticalPosition <= 0.01f);
        ui.ScrollBy(100000f);
        Check("[panel] scrolling up reaches the oldest line", ui.VerticalPosition >= 0.99f);
        ui.ScrollBy(-100000f);
        Check("[panel] scrolling down returns to the newest line", ui.VerticalPosition <= 0.01f);

        int rowsBeforeLive = ui.RowCount;
        lineUi.HideImmediately(); // queued lines wait for the one on screen; these checks want it shown now
        lineUi.ShowLine("History test live line.", null, "Team", "test-live");
        yield return null;
        Check("[panel] a line shown while open appears live", ui.RowCount == rowsBeforeLive + 1);

        ui.Hide();
        yield return null;
        Check("[panel] closes: UI gate and tool lock released", !ui.IsShowing && !PlayerUIGate.IsBlocked && !ToolInputLock.IsLocked);
        Check($"[panel] held/equipped tool unchanged across open/close ({equippedBefore})", PlayerToolRegistry.VirtuallyEquippedRole == equippedBefore);

        // --- Capacity: oldest dropped first ---
        int oldMax = manager.MaxEntries;
        manager.MaxEntries = manager.Entries.Count + 2;
        int firstSeqBefore = manager.Entries[0].sequence;
        for (int i = 0; i < 5; i++)
        {
            manager.Add("Capacity line " + i, null, "Team", "cap-" + i);
        }
        Check($"[capacity] trimmed to the limit ({manager.MaxEntries}) by dropping the oldest",
            manager.Entries.Count == manager.MaxEntries && manager.Entries[0].sequence == firstSeqBefore + 3
            && Last(manager).text == "Capacity line 4");
        manager.MaxEntries = oldMax;

        // --- No retired story content anywhere in the live pools or history ---
        var leaks = new List<string>();
        foreach (var pool in Resources.FindObjectsOfTypeAll<DialogueLinePool>())
        {
            if (pool == null || pool.lines == null || UnityEditorPath(pool).Contains("_Archive"))
            {
                continue;
            }
            foreach (var line in pool.lines)
            {
                if (HasRetiredWord(line))
                {
                    leaks.Add(pool.name + ": " + line);
                }
            }
        }
        foreach (var entry in manager.Entries)
        {
            if (HasRetiredWord(entry.text))
            {
                leaks.Add("history: " + entry.text);
            }
        }
        Check("[content] no retired storyline (affair / gloves / phone) in live dialogue or history" + (leaks.Count > 0 ? " -> " + string.Join(" | ", leaks) : ""), leaks.Count == 0);

        // --- New session: history starts over ---
        var logger = SessionLogger.Instance;
        if (logger != null)
        {
            var idProperty = typeof(SessionLogger).GetProperty("SessionId");
            string oldId = logger.SessionId;
            idProperty.SetValue(logger, System.Guid.NewGuid().ToString());
            lineUi.HideImmediately(); // queued lines wait for the one on screen; these checks want it shown now
            lineUi.ShowLine("First line of the new session.", null, "Team", "test-new-session");
            Check("[reset] a new session's first line starts an empty history at sequence 1",
                manager.Entries.Count == 1 && manager.Entries[0].sequence == 1 && manager.Entries[0].text == "First line of the new session.");
            idProperty.SetValue(logger, oldId);
        }

        report.AppendLine(failures.Count == 0 ? "=== PASS, 0 failures ===" : $"=== FAIL: {failures.Count} failure(s) ===");
        Report = report.ToString();
        IsDone = true;
    }

    private static DialogueHistoryEntry Last(DialogueHistoryManager manager) => manager.Entries[manager.Entries.Count - 1];

    private static DialogueHistoryEntry Last(DialogueHistoryManager manager, string text)
    {
        for (int i = manager.Entries.Count - 1; i >= 0; i--)
        {
            if (manager.Entries[i].text == text)
            {
                return manager.Entries[i];
            }
        }
        return null;
    }

    private static bool Contains(DialogueHistoryManager manager, string text, int from)
    {
        for (int i = Mathf.Max(0, from); i < manager.Entries.Count; i++)
        {
            if (manager.Entries[i].text == text)
            {
                return true;
            }
        }
        return false;
    }

    private static bool HasRetiredWord(string line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return false;
        }
        string lower = line.ToLowerInvariant();
        foreach (var word in RetiredStoryWords)
        {
            if (lower.Contains(word))
            {
                return true;
            }
        }
        return false;
    }

    private static string UnityEditorPath(Object asset)
    {
#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.GetAssetPath(asset) ?? string.Empty;
#else
        return string.Empty;
#endif
    }
}
