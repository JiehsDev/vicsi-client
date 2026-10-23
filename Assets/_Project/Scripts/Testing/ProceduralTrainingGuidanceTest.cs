using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

/// <summary>Focused presentation/deduplication checks; real milestone integration is in BiologicalSamplingTest and SOCOCloseoutTest.</summary>
public sealed class ProceduralTrainingGuidanceTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }
    private readonly StringBuilder report = new();
    private int failures;
    private int requests;
    private void Check(string label, bool passed)
    {
        report.AppendLine("  " + (passed ? "PASS: " : "FAIL: ") + label);
        if (!passed) failures++;
    }
    public void RunAllTests() => StartCoroutine(Run());
    private void Requested(DialogueRequest request) { if (request.id == "TRAIN-BIOLOGICAL-SAMPLE") requests++; }
    private IEnumerator Run()
    {
        AutomatedTestSandbox.Activate(nameof(ProceduralTrainingGuidanceTest));
        // Presentation-only suite: keep automatic assessment prompts from acquiring
        // the modal token under test. NarrativeRoleFlowTest exercises their real order.
        ReasoningCheckpointManager.Instance.enabled = false;
        HypothesisCheckpointManager.Instance.enabled = false;
        report.AppendLine("=== ProceduralTrainingGuidanceTest ===");
        yield return null;
        var training = ProceduralTrainingGuidance.Instance;
        var upper = STCSNotificationUI.Instance;
        var lower = NotificationManager.Instance.UI;
        Check("Crime scene automatically owns the guidance adapter", training != null);
        if (training == null) { Finish(); yield break; }
        Check("Team role lesson scheduled at investigation entry", training.HasScheduled("TRAIN-SOCO-TEAM"));
        // Isolate presentation from other scene startup dialogue, not gameplay gates.
        upper.HideImmediately(); lower.ClearTooltip(); lower.HidePrompt();
        yield return null;
        yield return null;
        upper.HideImmediately();
        string[] ids = { "TRAIN-SOCO-TEAM", "TRAIN-PHOTOGRAPHY", "TRAIN-MARKER", "TRAIN-TRACE-SOURCE", "TRAIN-MEASUREMENT",
            "TRAIN-FINGERPRINT", "TRAIN-BIOLOGICAL-SAMPLE", "TRAIN-PACKAGING", "TRAIN-CUSTODY", "closeout-start", "closeout-survey-guidance" };
        Check("All eleven concepts are authored, nonmodal Professor dialogue with readable duration",
            ids.All(id => { var entry = ViCSIDialogueCatalog.Current.Find(id); return entry != null && entry.speaker == "Professor"
                && entry.type == DialogueEntryType.Dialogue && entry.skippable && entry.duration >= 8f; }));
        Check("Training does not name suspects or tell the player which conclusion to choose",
            ids.All(id => { string text = GameDialogue.Text(id); return !text.Contains("Paul") && !text.Contains("John")
                && !text.Contains("guilty") && !text.Contains("choose the"); }));
        Check("Photography distinguishes route points from real photograph coverage", GameDialogue.Text("TRAIN-PHOTOGRAPHY").Contains("as many photographs")
            && GameDialogue.Text("TRAIN-PHOTOGRAPHY").Contains("route points"));
        Check("Trace explanation identifies the exercise and source principle", GameDialogue.Text("TRAIN-TRACE-SOURCE").Contains("Here,")
            && GameDialogue.Text("TRAIN-TRACE-SOURCE").Contains("where each finding came from"));
        Check("Custody teaches accountability without equating B with full real custody", GameDialogue.Text("TRAIN-CUSTODY").Contains("does not represent every"));
        Check("Blocked photograph and fingerprint guidance explains the reason", GameDialogue.Text("evidence-step-photograph-first").Contains("original position")
            && GameDialogue.Text("fingerprint-reveal-before-lift").Contains("usable record"));
        GameDialogue.OnRequested += Requested;
        try
        {
            PlayerUIGate.Enter(this);
            ProceduralTrainingGuidance.Schedule("TRAIN-BIOLOGICAL-SAMPLE");
            int queued = training.PendingCount;
            ProceduralTrainingGuidance.Schedule("TRAIN-BIOLOGICAL-SAMPLE");
            Check("Repeated concept queues only once", queued > 0 && training.PendingCount == queued);
            yield return null;
            Check("Lesson waits while a menu owns input", requests == 0 && !upper.IsShowing);
            PlayerUIGate.Exit(this);
            lower.ShowPrompt("Press B to store in inventory");
            yield return null;
            Check("Lesson waits for the explicit action prompt", requests == 0 && !upper.IsShowing);
            lower.HidePrompt();
            lower.ShowTooltip("Secure the evidence first.", 10f);
            yield return null;
            Check("Lesson waits for the immediate tooltip", requests == 0 && !upper.IsShowing);
            lower.ClearTooltip();
            yield return null;
            yield return null;
            Check("Lesson is presented after action guidance clears", requests == 1 && upper.HasLinesFrom("TRAIN-BIOLOGICAL-SAMPLE"));
            Check("Lesson acquires no UI or tool lock", !PlayerUIGate.IsBlocked && !ToolInputLock.IsLocked);
            Check("History records the instructor lesson once with stable source and Training category",
                DialogueHistoryManager.Instance.Entries.Count(e => e.sourceId == "TRAIN-BIOLOGICAL-SAMPLE" && e.category == "Training" && e.speaker == "Professor") == 1);
            yield return new WaitForSecondsRealtime(.35f);
            lower.ShowTooltip("Press B to store in inventory", 10f);
            yield return null;
            yield return null;
            float elapsed = upper.Countdown.Elapsed;
            yield return new WaitForSecondsRealtime(.1f);
            var group = (CanvasGroup)typeof(STCSNotificationUI).GetField("canvasGroup", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(upper);
            Check("An incoming action hint hides the lesson and pauses reading time", Mathf.Approximately(elapsed, upper.Countdown.Elapsed) && group.alpha == 0f);
            lower.ClearTooltip();
            yield return null;
            yield return null;
            Check("Lesson resumes visibly after the hint", group.alpha > .9f && upper.Countdown.Elapsed > elapsed);
            GameDialogue.Say("fingerprint-reveal-before-lift");
            yield return null;
            yield return new WaitForSecondsRealtime(.4f);
            Check("Blocked-action feedback takes precedence over a lesson", upper.CurrentText.Contains("usable record"));
            upper.HideImmediately();
            ProceduralTrainingGuidance.Schedule("TRAIN-BIOLOGICAL-SAMPLE");
            yield return null;
            Check("Later evidence cannot replay the displayed lesson", requests == 1 && !upper.IsShowing);
            ProceduralTrainingGuidance.Schedule("TRAIN-MARKER");
            yield return null;
            lower.ShowPrompt("Press B to store in inventory");
            GameDialogue.Say("fingerprint-reveal-before-lift");
            yield return new WaitForSecondsRealtime(.5f);
            Check("Blocked feedback cannot starve behind a lesson paused immediately after starting", upper.CurrentText.Contains("usable record"));
        }
        finally
        {
            PlayerUIGate.Exit(this);
            GameDialogue.OnRequested -= Requested;
            upper.HideImmediately(); lower.ClearTooltip(); lower.HidePrompt();
        }
        Finish();
    }
    private void Finish()
    {
        report.AppendLine(failures == 0 ? "=== PASS, 0 failures ===" : $"=== FAIL, {failures} failures ===");
        Report = report.ToString(); IsDone = true; Debug.Log(Report);
    }
}
