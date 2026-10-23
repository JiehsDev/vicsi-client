// Assets/_Project/Scripts/Testing/DialogueCatalogTest.cs
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// Play-mode checks for the central dialogue/tooltip catalog (ViCSIDialogueCatalog +
/// GameDialogue) and its two channels: dialogue (STCSNotificationUI - priority queue,
/// skip, history) and tooltip (NotificationUI - latest wins, no backlog). Uses a test
/// catalog swapped in through ViCSIDialogueCatalog.SetOverrideForTesting, so editing
/// "the catalog" here never touches the project asset; the last section validates the
/// REAL catalog against every id the project uses. Run in CSI_Environment, once per fresh
/// Play session: RunAllTests(), then poll IsDone / Report.
/// </summary>
public class DialogueCatalogTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }

    private readonly List<string> failures = new();
    private readonly StringBuilder report = new();
    private ProceduralTrainingGuidance training;
    private bool trainingWasEnabled;
    private readonly List<Behaviour> suspendedPresenters = new();
    private Camera originalCamera;
    private GameObject standingEyeFixture;
    private void OnDisable() => RestoreTraining();
    private void RestoreTraining()
    {
        if (training != null) training.enabled = trainingWasEnabled;
        foreach (var presenter in suspendedPresenters) if (presenter != null) presenter.enabled = true;
        suspendedPresenters.Clear();
        if (originalCamera != null) originalCamera.tag = "MainCamera";
        if (standingEyeFixture != null) Destroy(standingEyeFixture);
    }

    public void RunAllTests()
    {
        IsDone = false;
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        report.AppendLine("=== DialogueCatalogTest ===");
        AutomatedTestSandbox.Activate(nameof(DialogueCatalogTest));   // keeps this session's raw log out of real data
        ReleasePreSessionGate();   // the scene opened directly holds the UI gate until someone identifies
        // No tracked HMD in this test. Keep timed UI visibly placed instead of letting
        // the legitimate floor/wall defer policy pause a tooltip's visible countdown.
        originalCamera = Camera.main;
        if (originalCamera != null) originalCamera.tag = "Untagged";
        standingEyeFixture = new GameObject("CatalogStandingEyeFixture");
        var fixtureCamera = standingEyeFixture.AddComponent<Camera>();
        fixtureCamera.tag = "MainCamera"; fixtureCamera.transform.position = new Vector3(1000, 1000, 1000);
        foreach (var follow in FindObjectsByType<HudFollowCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            typeof(HudFollowCamera).GetField("targetCamera", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(follow, fixtureCamera);
        // Isolate the tiny substituted catalog from real milestone content.
        training = ProceduralTrainingGuidance.Instance;
        trainingWasEnabled = training != null && training.enabled;
        if (training != null) training.enabled = false;
        // This catalog-only fixture advances turnover before it runs. That makes a
        // real checkpoint eligible; it must not own B during the isolated skip checks.
        foreach (Behaviour presenter in new Behaviour[] { ReasoningCheckpointManager.Instance,
            HypothesisCheckpointManager.Instance, FindAnyObjectByType<HypothesisCheckpointUI>(), ArrivalPPEStation.Instance,
            SamplePreparationStation.Instance, FingerprintComparisonStation.Instance })
            if (presenter != null && presenter.enabled) { suspendedPresenters.Add(presenter); presenter.enabled = false; }
        var dialogue = STCSNotificationUI.Instance;
        var tooltip = NotificationManager.Instance != null ? NotificationManager.Instance.UI : null;
        if (dialogue == null || tooltip == null)
        {
            Finish("FAIL: dialogue (STCSNotificationUI) or tooltip (NotificationUI) panel missing from scene.");
            yield break;
        }

        var cat = ScriptableObject.CreateInstance<ViCSIDialogueCatalog>();
        cat.skipProtectionSeconds = 0.2f;
        Add(cat, "t-dlg", DialogueEntryType.Dialogue, DialogueKind.Guidance, "Professor line A.", 3f);
        Add(cat, "t-dlg-2", DialogueEntryType.Dialogue, DialogueKind.Guidance, "Professor line two.", 3f);
        Add(cat, "t-dlg-3", DialogueEntryType.Dialogue, DialogueKind.Guidance, "Professor line three.", 3f);
        Add(cat, "t-err", DialogueEntryType.Dialogue, DialogueKind.Error, "That action is refused.", 3f);
        Add(cat, "t-conf", DialogueEntryType.Dialogue, DialogueKind.Confirmation, "Done.", 3f);
        Add(cat, "t-tip-a", DialogueEntryType.Tooltip, DialogueKind.Guidance, "Measure the west wall.", 0.6f);
        Add(cat, "t-tip-b", DialogueEntryType.Tooltip, DialogueKind.Guidance, "Measure the north wall.", 0.6f);
        Add(cat, "t-fmt", DialogueEntryType.Tooltip, DialogueKind.Confirmation, "Evidence tent {0} placed.", 0f);
        Add(cat, "t-long", DialogueEntryType.Dialogue, DialogueKind.Guidance,
            "This is a deliberately long professor line that keeps going so that it needs more than the panel's authored height: " +
            "document the scene, photograph everything before touching it, mark each item, measure from two fixed references, " +
            "and only then collect and seal the evidence so the chain of custody holds up in court.", 3f);
        ViCSIDialogueCatalog.SetOverrideForTesting(cat);
        try
        {
            yield return Clean(dialogue, tooltip);

            foreach (var speaker in new[] { "Dispatcher", "Dispatch", "IOC", "SOCO", "Recorder", "Professor", "First Responder", "Vendor", "Paul", "Peter" })
            {
                dialogue.ShowLine(speaker + ": Keep the scene secure.", null, "Team", "prefix-" + speaker);
                var history = DialogueHistoryManager.Instance.Entries;
                Check("Speaker prefix removed from dialogue: " + speaker,
                    dialogue.CurrentText == "Keep the scene secure." && history[history.Count-1].speaker == speaker
                    && history[history.Count-1].text == dialogue.CurrentText);
                yield return Clean(dialogue, tooltip);
            }
            cat.Find("t-dlg").speaker = "Dispatcher";
            GameDialogue.Say("t-dlg");
            Check("Catalog speaker metadata does not become a visible prefix", dialogue.CurrentText == "Professor line A.");
            cat.Find("t-dlg").speaker = null;
            yield return Clean(dialogue, tooltip);
            foreach (var sentence in new[] { "Next step: photograph the scene.", "The vendor saw Paul around 3:00 AM.", "Evidence: the sample is sealed." })
            {
                dialogue.ShowLine(sentence, null, "Team", "colon-check");
                var history = DialogueHistoryManager.Instance.Entries;
                Check("Ordinary colons remain intact: " + sentence, dialogue.CurrentText == sentence && history[history.Count-1].text == sentence);
                yield return Clean(dialogue, tooltip);
            }

            // 1. edited dialogue text is what shows
            GameDialogue.Say("t-dlg");
            Check("1. dialogue shows catalog text", dialogue.CurrentText == "Professor line A.");
            yield return Clean(dialogue, tooltip);
            cat.Find("t-dlg").text = "Professor line A - edited in the Inspector.";
            GameDialogue.Say("t-dlg");
            Check("1. edited dialogue text is used on the next Say", dialogue.CurrentText == "Professor line A - edited in the Inspector.");

            // 3. edited dialogue duration drives the countdown
            Check($"3. dialogue duration from catalog (3s, got {dialogue.Countdown.Duration})", Mathf.Approximately(dialogue.Countdown.Duration, 3f));
            yield return Clean(dialogue, tooltip);
            cat.Find("t-dlg").duration = 4.5f;
            GameDialogue.Say("t-dlg");
            Check($"3. edited dialogue duration applies (4.5s, got {dialogue.Countdown.Duration})", Mathf.Approximately(dialogue.Countdown.Duration, 4.5f));
            cat.Find("t-dlg").duration = 0f;
            yield return Clean(dialogue, tooltip);
            GameDialogue.Say("t-dlg");
            Check($"3. duration 0 uses the catalog default for Guidance ({cat.dialogueGuidanceSeconds}s)", Mathf.Approximately(dialogue.Countdown.Duration, cat.dialogueGuidanceSeconds));
            cat.Find("t-dlg").text = "Professor line A.";
            cat.Find("t-dlg").duration = 3f;
            yield return Clean(dialogue, tooltip);

            // 2. edited tooltip text / 4. edited tooltip duration
            GameDialogue.Say("t-tip-a");
            Check("2. tooltip shows catalog text", tooltip.CurrentTooltip == "Measure the west wall.");
            cat.Find("t-tip-a").text = "Measure the WEST wall (edited).";
            cat.Find("t-tip-a").duration = 1.4f;
            GameDialogue.Say("t-tip-a");
            Check("2. edited tooltip text is used", tooltip.CurrentTooltip == "Measure the WEST wall (edited).");
            Check($"4. edited tooltip duration applies (1.4s, got {tooltip.TooltipRemaining:0.00})", Mathf.Abs(tooltip.TooltipRemaining - 1.4f) < 0.05f);
            Check("2. tooltips are not dialogue (dialogue panel untouched)", !dialogue.IsShowing);
            cat.Find("t-tip-a").text = "Measure the west wall.";
            cat.Find("t-tip-a").duration = 0.6f;
            GameDialogue.Say("t-fmt", 3);
            Check("2. placeholders are filled ({0} -> 3)", tooltip.CurrentTooltip == "Evidence tent 3 placed.");
            Check($"4. duration 0 uses the tooltip default ({cat.tooltipSeconds}s)", Mathf.Abs(tooltip.TooltipRemaining - cat.tooltipSeconds) < 0.05f);
            yield return Clean(dialogue, tooltip);

            // 5. dialogue queues  /  priority
            GameDialogue.Say("t-dlg");
            GameDialogue.Say("t-dlg-2");
            GameDialogue.Say("t-err");
            Check("5. second and third dialogue wait in the queue", dialogue.CurrentText == "Professor line A." && dialogue.PendingCount == 2);

            // 6 + 7. skip ends the current line, the next appears at once - highest priority first
            yield return new WaitForSecondsRealtime(0.3f);
            int historyBefore = HistoryCount("Professor line A.");
            Check("6. Skip() accepted for a skippable line past protection", dialogue.Skip());
            yield return null;
            yield return null;
            Check($"7. skip advances to the next queued line, Error before Guidance (now '{dialogue.CurrentText}')", dialogue.CurrentText == "That action is refused.");
            Check("13. the skipped line stays in history exactly once", HistoryCount("Professor line A.") == historyBefore && historyBefore == 1);
            Check("13. the line now shown was recorded once", HistoryCount("That action is refused.") == 1);

            // 6. B arbitration: B belongs to dialogue only when no panel / [B] prompt owns it
            object token = new object();
            PlayerUIGate.Enter(token);
            bool blockedByPanel = !DialogueSkipInput.BelongsToDialogue();
            PlayerUIGate.Exit(token);
            tooltip.ShowPrompt("[B] Pick Up Evidence Tent");
            bool blockedByPrompt = !DialogueSkipInput.BelongsToDialogue();
            tooltip.HidePrompt();
            Check("6. B never skips while a panel holds the UI gate", blockedByPanel);
            Check("6. B never skips while a [B] world prompt is up", blockedByPrompt);
            Check("6. B skips when nothing else owns it (owners=" + string.Join(",", PlayerUIGate.LockOwnerNames)
                + "; ambient=" + tooltip.HasAmbientPrompt + ")", DialogueSkipInput.BelongsToDialogue());
            Check($"6. binding is the right controller B button ({DialogueSkipInput.Binding})", DialogueSkipInput.Binding == "<XRController>{RightHand}/secondaryButton");

            // 11 + 12. tooltips never displace dialogue, dialogue never delays tooltips
            GameDialogue.Say("t-tip-a");
            Check("11. a tooltip does not overwrite the Error dialogue on screen", dialogue.CurrentText == "That action is refused." && dialogue.CurrentKind == DialogueKind.Error);
            Check("12. the tooltip shows immediately while dialogue is busy and queued", tooltip.CurrentTooltip == "Measure the west wall." && dialogue.PendingCount == 1);
            yield return Clean(dialogue, tooltip);

            // 8 + 9. latest wins, the replaced tooltip never comes back
            GameDialogue.Say("t-tip-a");
            GameDialogue.Say("t-tip-b");
            Check("8. tooltip B immediately replaces A", tooltip.CurrentTooltip == "Measure the north wall.");
            bool aReappeared = false;
            float until = Time.realtimeSinceStartup + 1.2f;
            while (Time.realtimeSinceStartup < until)
            {
                if (tooltip.CurrentTooltip == "Measure the west wall.") aReappeared = true;
                yield return null;
            }
            Check("9. tooltip A never reappears after B, and B is gone after its duration", !aReappeared && tooltip.CurrentTooltip != "Measure the north wall.");

            // 10. repeating the same tooltip doesn't build a backlog
            for (int i = 0; i < 6; i++) GameDialogue.Say("t-tip-b");
            Check("10. six identical tooltips -> one on screen, nothing queued", tooltip.CurrentTooltip == "Measure the north wall." && tooltip.PendingCount == 0);
            until = Time.realtimeSinceStartup + 1.0f;
            while (Time.realtimeSinceStartup < until) yield return null;
            bool repeated = tooltip.CurrentTooltip == "Measure the north wall.";
            if (repeated) report.AppendLine($"INFO: repeated tooltip remaining={tooltip.TooltipRemaining}; deferred={tooltip.IsPlacementDeferred}; owners={string.Join(",", PlayerUIGate.LockOwnerNames)}");
            Check("10. ...and it is gone after one duration (no replays)", !repeated);

            // 14. missing id
            string warned = null;
            void Capture(string msg, string stack, LogType type) { if (type == LogType.Warning && msg.Contains("Missing dialogue id 'does-not-exist'")) warned = msg; }
            Application.logMessageReceived += Capture;
            bool threw = false;
            try { GameDialogue.Say("does-not-exist"); GameDialogue.Text("does-not-exist"); }
            catch (System.Exception) { threw = true; }
            Application.logMessageReceived -= Capture;
            Check("14. missing id never throws", !threw);
            Check("14. missing id logs a warning naming the id", warned != null);
            Check("14. development builds show a short placeholder, not stale text", tooltip.CurrentTooltip == "[Missing dialogue: does-not-exist]");
            yield return Clean(dialogue, tooltip);

            // countdown pauses while a panel holds the UI gate (carried over from the old readability test)
            GameDialogue.Say("t-dlg");
            yield return new WaitForSecondsRealtime(0.3f);
            object modal = new object();
            PlayerUIGate.Enter(modal);
            yield return null;
            float frozenAt = dialogue.Countdown.Elapsed;
            yield return new WaitForSecondsRealtime(0.5f);
            bool paused = dialogue.Countdown.IsPaused && Mathf.Abs(dialogue.Countdown.Elapsed - frozenAt) < 0.05f && dialogue.IsShowing;
            PlayerUIGate.Exit(modal);
            yield return new WaitForSecondsRealtime(0.3f);
            Check("[modal] dialogue countdown freezes while a panel is open, line stays visible", paused);
            Check("[modal] countdown resumes when the panel closes", !dialogue.Countdown.IsPaused && dialogue.Countdown.Elapsed > frozenAt + 0.2f);
            yield return Clean(dialogue, tooltip);

            // long lines grow the panel instead of shrinking the font
            var panel = dialogue.transform as RectTransform;
            float authored = panel.sizeDelta.y;
            GameDialogue.Say("t-long");
            yield return null;
            Check($"[long] panel grew for a long line ({authored:0} -> {panel.sizeDelta.y:0})", panel.sizeDelta.y > authored);
            yield return Clean(dialogue, tooltip);

            // a conversation's lines stay together: A1, B1, A2 posted -> A1, A2, B1 shown
            GameDialogue.SayLine("t-dlg", "Team", "seq-a");
            GameDialogue.SayLine("t-dlg-2", "Team", "seq-b");
            GameDialogue.SayLine("t-dlg-3", "Team", "seq-a");
            var order = new List<string> { dialogue.CurrentText };
            for (int i = 0; i < 2; i++)
            {
                yield return new WaitForSecondsRealtime(0.25f);
                dialogue.Skip();
                yield return null;
                yield return null;
                order.Add(dialogue.CurrentText);
            }
            Check($"[sequence] a follow-up line stays with its own conversation ({string.Join(" | ", order)})",
                order.Count == 3 && order[0] == "Professor line A." && order[1] == "Professor line three." && order[2] == "Professor line two.");
            yield return Clean(dialogue, tooltip);

            // replace behaviors (dialogue)
            GameDialogue.Say("t-err");
            GameDialogue.Say("t-err");
            Check("IgnoreIfAlreadyShowing: a repeated refusal is not queued again", dialogue.PendingCount == 0);
            yield return Clean(dialogue, tooltip);
        }
        finally
        {
            ViCSIDialogueCatalog.SetOverrideForTesting(null);
            Object.Destroy(cat);
        }

        // 15. every id the project uses resolves in the REAL catalog
        var real = ViCSIDialogueCatalog.Current;
        var unresolved = new List<string>();
        foreach (var id in UsedIds()) if (real.Find(id) == null) unresolved.Add(id);
        Check($"15. all {UsedIds().Count} ids used by code/scene/data resolve in the real catalog (missing: {string.Join(", ", unresolved)})", unresolved.Count == 0 && UsedIds().Count > 0);
        var dupes = new HashSet<string>(); var seen = new HashSet<string>();
        foreach (var e in real.entries) if (e != null && !seen.Add(e.id)) dupes.Add(e.id);
        Check($"15. real catalog has no duplicate ids ({string.Join(", ", dupes)})", dupes.Count == 0);
        var empty = real.entries.FindAll(e => e != null && string.IsNullOrWhiteSpace(e.text)).ConvertAll(e => e.id);
        Check($"15. no real catalog entry has empty text ({string.Join(", ", empty)})", empty.Count == 0);

        // Content polish safeguards against the real authored scenario, without driving evidence/progression.
        var spoken = real.entries.FindAll(e => e != null && (e.id.StartsWith("narr-")
            || e.id.StartsWith("team-") || e.id.StartsWith("FR_TURNOVER_")
            || e.id.StartsWith("IOC_SOCO_") || e.id.StartsWith("statement-")
            || e.id.StartsWith("ioc-observation-") || e.id == "ioc-assessment-guidance"
            || e.id == "soco-decision-reconsider" || e.id.StartsWith("ending-radio-")));
        var robotic = new Regex(@"has not been processed|procedure has not|\brequirement\b|assessment condition|evidence state|\bsystem\b", RegexOptions.IgnoreCase);
        Check("16. active spoken catalog lines avoid software/procedure-state wording",
            spoken.Count > 0 && spoken.TrueForAll(e => !robotic.IsMatch(e.text)));
        string sighting = GameDialogue.Text("statement-vendor-paul-3am");
        Check("16. vendor recognizes Paul near the house around three, without claiming entry or attack",
            sighting.Contains("seeing Paul") && sighting.Contains("near John's house") && sighting.Contains("around three in the morning")
            && sighting.Contains("didn't see where he went")
            && !sighting.Contains("entered") && !sighting.Contains("attacked"));
        Check("16. Paul denies returning and knife dialogue limits contact",
            GameDialogue.Text("statement-paul-alibi").Contains("didn't come back")
            && GameDialogue.Text("statement-paul-knife").Contains("doesn't mean I used it on him")
            && GameDialogue.Text("narr-mission-knife-print-result-1").Contains("doesn't tell us when or why"));
        Check("16. IOC observations remain preliminary and briefings preserve both forms",
            GameDialogue.Text("ioc-observation-victim").Contains("what looks like a penetrating wound")
            && GameDialogue.Text("ioc-observation-weapon").Contains("could be the weapon")
            && GameDialogue.Text("FR_TURNOVER_04").Contains("CSI Form 1")
            && GameDialogue.Text("IOC_SOCO_03").Contains("CSI Form 2"));
#if UNITY_EDITOR
        var endingData = UnityEditor.AssetDatabase.LoadAssetAtPath<EndingSet>("Assets/_Project/Data/Ending/Endings_CSI_Environment.asset");
        var hypData = UnityEditor.AssetDatabase.LoadAssetAtPath<HypothesisCheckpointSet>("Assets/_Project/Data/Scenarios/HypothesisCheckpoints_CSI_Environment.asset");
        Check("16. all ending and HYP player-facing content is non-empty",
            endingData != null && endingData.endings.TrueForAll(e => !string.IsNullOrWhiteSpace(e.endingText))
            && hypData != null && hypData.checkpoints.TrueForAll(h => !string.IsNullOrWhiteSpace(h.promptText)
                && h.options.TrueForAll(o => !string.IsNullOrWhiteSpace(o.text))
                && h.reasoningOptions.TrueForAll(o => !string.IsNullOrWhiteSpace(o.text))));
        var supported = endingData.endings.Find(e => e.endingId == "ending-brother-accused").endingText;
        Check("16. supported Paul combines physical findings and timeline conflict without absolute proof",
            supported.Contains("fingerprint") && supported.Contains("not when or how") && supported.Contains("blood pattern")
            && supported.Contains("never returned") && supported.Contains("3:00 AM") && supported.Contains("timeline")
            && supported.Contains("strongest supported suspect") && supported.Contains("isn't absolute proof"));
        Check("16. Peter remains unsupported and inconclusive remains defensible",
            endingData.endings.Find(e => e.endingId == "ending-peter-unsupported").endingText.Contains("Nothing you've found ties Peter")
            && endingData.endings.Find(e => e.endingId == "ending-inconclusive").endingText.Contains("Leaving the case open is better"));
        Check("16. HYP preserves suspicion/contact limits and combined timeline reasoning",
            hypData.checkpoints[0].reasoningOptions.Exists(o => o.isCorrect && o.text.Contains("questions to ask, not a conclusion"))
            && hypData.checkpoints[1].reasoningOptions.Exists(o => o.isCorrect && o.text.Contains("doesn't tell us when or why"))
            && hypData.checkpoints[2].reasoningOptions.Exists(o => o.isCorrect && o.text.Contains("linked to that claim on the board")));
#endif
        yield return Clean(dialogue, tooltip);
        GameDialogue.SayLine("statement-vendor-paul-3am", "Witness", "dialogue-content-check", dialogue);
        yield return null;
        Check("16. rewritten real dialogue is displayed exactly and stored in history once",
            dialogue.CurrentText == sighting && HistoryCount(sighting) == 1);
        yield return Clean(dialogue, tooltip);

        Finish(null);
    }

    // Ids referenced by code (Say/Text/SayLine literals, ternary id strings) and by scene/data fields.
    private static List<string> usedIds;
    private static List<string> UsedIds()
    {
        if (usedIds != null) return usedIds;
        var ids = new HashSet<string>();
#if UNITY_EDITOR
        var call = new Regex("GameDialogue\\.(?:Say|Text|SayLine)\\(\"([a-z0-9-]+)\"");
        var idLiteral = new Regex("\"((?:prompt|photo|evidence-status|fingerprint-smudged|measure|team|narr|step)-[a-z0-9-]+|TRAIN-[A-Z0-9-]+)\"");
        foreach (var file in System.IO.Directory.GetFiles(Application.dataPath + "/_Project/Scripts", "*.cs", System.IO.SearchOption.AllDirectories))
        {
            if (file.Replace('\\', '/').Contains("/Testing/")) continue;
            string src = System.IO.File.ReadAllText(file);
            foreach (Match m in call.Matches(src)) ids.Add(m.Groups[1].Value);
            foreach (Match m in idLiteral.Matches(src)) ids.Add(m.Groups[1].Value);
        }
#endif
        foreach (var stcs in Object.FindObjectsByType<STCSManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var field = typeof(STCSManager).GetField("triggers", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (field?.GetValue(stcs) is List<STCSTrigger> triggers)
                foreach (var t in triggers) if (t?.lineIds != null) foreach (var id in t.lineIds) ids.Add(id);
        }
        foreach (var point in Object.FindObjectsByType<ScenePhotoPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (!string.IsNullOrEmpty(point.instructionId)) ids.Add(point.instructionId);
        var esm = EvidenceStateManager.Instance;
        if (esm != null)
            foreach (var record in esm.AllRecords)
                foreach (var step in record.definition?.stepRequirements ?? new List<ProcedureStepRequirement>())
                    if (!string.IsNullOrEmpty(step?.messageId)) ids.Add(step.messageId);
        var narrative = NarrativeEventManager.Instance;
        if (narrative != null)
            foreach (var def in narrative.DefinitionsForTest())
                if (def?.lineIds != null) foreach (var id in def.lineIds) ids.Add(id);
        usedIds = new List<string>(ids);
        return usedIds;
    }

    private static void ReleasePreSessionGate()
    {
        var controller = Object.FindFirstObjectByType<PreSessionFlowController>();
        if (controller == null) return;
        controller.enabled = false;
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var token = typeof(PreSessionFlowController).GetField("uiGateToken", flags)?.GetValue(controller);
        if (token != null) PlayerUIGate.Exit(token);
        var panel = Object.FindFirstObjectByType<StudentIdentificationPanel>(FindObjectsInactive.Include);
        if (panel != null) panel.Hide();
    }

    private static void Add(ViCSIDialogueCatalog cat, string id, DialogueEntryType type, DialogueKind priority, string text, float duration)
    {
        cat.entries.Add(new DialogueCatalogEntry
        {
            id = id, type = type, priority = priority, text = text, duration = duration, skippable = true,
            replaceBehavior = priority == DialogueKind.Error ? DialogueReplaceBehavior.IgnoreIfAlreadyShowing
                : type == DialogueEntryType.Tooltip ? DialogueReplaceBehavior.ReplaceCurrent : DialogueReplaceBehavior.Queue,
        });
    }

    private static int HistoryCount(string text)
    {
        var history = DialogueHistoryManager.Instance;
        if (history == null) return -1;
        int n = 0;
        foreach (var e in history.Entries) if (e.text == text) n++;
        return n;
    }

    private static IEnumerator Clean(STCSNotificationUI dialogue, NotificationUI tooltip)
    {
        dialogue.HideImmediately();
        tooltip.ClearTooltip();
        tooltip.HidePrompt();
        DialogueHistoryManager.Instance?.Clear();
        yield return null;
    }

    private void Check(string label, bool passed)
    {
        report.AppendLine($"  {(passed ? "PASS" : "FAIL")}: {label}");
        if (!passed) failures.Add(label);
    }

    private void Finish(string fatal)
    {
        RestoreTraining();
        if (fatal != null) report.AppendLine(fatal);
        report.AppendLine(fatal == null && failures.Count == 0 ? "=== PASS, 0 failures ===" : $"=== FAIL: {failures.Count + (fatal != null ? 1 : 0)} failure(s) ===");
        Report = report.ToString();
        IsDone = true;
    }
}
