// Assets/_Project/Scripts/Testing/NotificationPriorityTest.cs
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// Checked-in regression test for the notification priority rule: ambient prompts are
/// always preemptable, state-driven messages never are (see NotificationUI's class
/// comment for the rule and why it is that way round).
///
/// Exists as a file rather than as editor scratch code because this bug was silent -
/// the refusal was enqueued and simply never drawn, so nothing errored and nothing
/// logged. A regression here would look exactly like a working game right up until a
/// player pulls a trigger, gets no response, and reports the tool as broken.
///
/// Samples the real NotificationUI label every frame across a real posting sequence,
/// rather than asserting on internal state, because "what did the player actually
/// see" is the whole question.
///
/// Run from the editor via:
///     FindFirstObjectByType&lt;NotificationPriorityTest&gt;().RunAndLog()
/// </summary>
public class NotificationPriorityTest : MonoBehaviour
{
    private const string AmbientText = "[B] Pick Up Evidence Tent";

    /// <summary>Runs the check and Debug.Logs a report. Every line starting with FAIL is a real regression.</summary>
    public void RunAndLog()
    {
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        var report = new StringBuilder();
        var failures = new List<string>();

        var manager = FindFirstObjectByType<NotificationManager>();
        if (manager == null)
        {
            Debug.Log("FAIL: no NotificationManager in scene.");
            yield break;
        }

        var uiField = typeof(NotificationManager).GetField(
            "notificationUI",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var ui = uiField != null ? uiField.GetValue(manager) : null;
        if (ui == null)
        {
            Debug.Log("FAIL: NotificationManager has no NotificationUI assigned.");
            yield break;
        }

        var labelField = ui.GetType().GetField(
            "label",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var label = labelField.GetValue(ui) as TMP_Text;

        report.AppendLine("=== Notification priority test ===");

        // --- Case 1: a state-driven refusal must preempt a standing ambient prompt ---
        NotificationManager.HidePrompt();
        yield return new WaitForSeconds(0.5f);

        NotificationManager.ShowPrompt(AmbientText);
        yield return new WaitForSeconds(0.5f);
        report.AppendLine("ambient prompt showing: \"" + label.text + "\"");

        NotificationManager.Notify("Not yet Marked.");

        bool sawRefusal = false;
        bool ambientResumed = false;
        float elapsed = 0f;
        while (elapsed < 6f)
        {
            elapsed += Time.deltaTime;
            if (label.text == "Not yet Marked.")
            {
                sawRefusal = true;
            }
            else if (sawRefusal && label.text == AmbientText)
            {
                ambientResumed = true;
                break;
            }
            yield return null;
        }

        if (!sawRefusal)
        {
            failures.Add("state-driven refusal never reached the label while an ambient prompt was up.");
            report.AppendLine("    FAIL: refusal never displayed - it was swallowed by the ambient prompt.");
        }
        else
        {
            report.AppendLine("    refusal preempted the ambient prompt and displayed.");
        }

        if (!ambientResumed)
        {
            failures.Add("ambient prompt did not resume after the refusal finished.");
            report.AppendLine("    FAIL: ambient prompt did not come back afterwards.");
        }
        else
        {
            report.AppendLine("    ambient prompt resumed afterwards (context still true).");
        }

        // --- Case 2: the rule is general, not specific to that one pairing ---
        NotificationManager.Notify("Not yet Sealed.");
        bool sawSecond = false;
        elapsed = 0f;
        while (elapsed < 4f)
        {
            elapsed += Time.deltaTime;
            if (label.text == "Not yet Sealed.")
            {
                sawSecond = true;
                break;
            }
            yield return null;
        }

        if (!sawSecond)
        {
            failures.Add("a second, different gate-block message did not preempt the ambient prompt.");
            report.AppendLine("    FAIL: \"Not yet Sealed.\" never displayed.");
        }
        else
        {
            report.AppendLine("    second gate-block message also preempted - rule is general.");
        }

        NotificationManager.HidePrompt();

        report.AppendLine(failures.Count == 0
            ? "=== PASS: ambient prompts are preemptable, state-driven messages are not ==="
            : "=== FAILED with " + failures.Count + " problem(s) ===");

        Debug.Log(report.ToString());
    }
}
