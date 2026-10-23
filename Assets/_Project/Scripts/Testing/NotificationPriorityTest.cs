// Assets/_Project/Scripts/Testing/NotificationPriorityTest.cs
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// Regression test for the rule that a refusal is never swallowed by an ambient prompt.
/// The original bug was silent: a "[B] Pick Up Evidence Tent" prompt held the only label,
/// the refusal was queued behind it and never drawn, and the player read the tool as broken.
///
/// With the central catalog the two kinds of message live on different panels: refusals
/// (catalog Dialogue entries, Error priority) go to the dialogue panel, so a standing [B]
/// prompt on the tooltip panel can no longer hide them; and a timed tooltip still takes the
/// tooltip label over the prompt, which comes back by itself afterwards. Samples the real
/// labels every frame, because "what did the player actually see" is the whole question.
///
/// Run from the editor via:
///     FindFirstObjectByType&lt;NotificationPriorityTest&gt;().RunAndLog()
/// </summary>
public class NotificationPriorityTest : MonoBehaviour
{
    private const string AmbientText = "[B] Pick Up Evidence Tent";

    public bool IsDone { get; private set; }
    public string Report { get; private set; }

    /// <summary>Runs the check and Debug.Logs a report. Every line starting with FAIL is a real regression.</summary>
    public void RunAndLog() => StartCoroutine(Run());

    private IEnumerator Run()
    {
        IsDone = false;
        var report = new StringBuilder("=== Notification priority test ===\n");
        int failures = 0;
        void Check(string label, bool ok) { report.AppendLine($"  {(ok ? "PASS" : "FAIL")}: {label}"); if (!ok) failures++; }

        var tooltipUi = NotificationManager.Instance != null ? NotificationManager.Instance.UI : null;
        var dialogueUi = STCSNotificationUI.Instance;
        if (tooltipUi == null || dialogueUi == null)
        {
            Report = "FAIL: tooltip (NotificationUI) or dialogue (STCSNotificationUI) panel missing.";
            Debug.Log(Report);
            IsDone = true;
            yield break;
        }
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var tooltipLabel = typeof(NotificationUI).GetField("label", flags).GetValue(tooltipUi) as TMP_Text;
        var dialogueLabel = typeof(STCSNotificationUI).GetField("label", flags).GetValue(dialogueUi) as TMP_Text;
        // This fixture tests routing/readable-time arbitration, not the untracked
        // Editor head pose or unrelated milestone/checkpoint producers.
        var suspended = new List<Behaviour>();
        foreach (Behaviour presenter in new Behaviour[] { ReasoningCheckpointManager.Instance,
            HypothesisCheckpointManager.Instance, FindAnyObjectByType<HypothesisCheckpointUI>(), ProceduralTrainingGuidance.Instance })
            if (presenter != null && presenter.enabled) { suspended.Add(presenter); presenter.enabled = false; }
        var photography = ScenePhotographyManager.Instance;
        bool photoInstructionPending = photography != null && photography.IsInvoking("ActivateFirstPoint");
        if (photoInstructionPending) photography.CancelInvoke("ActivateFirstPoint");
        var noticeFollow = tooltipUi.GetComponent<HudFollowCamera>();
        var dialogueFollow = dialogueUi.GetComponent<HudFollowCamera>();
        var cameraField = typeof(HudFollowCamera).GetField("targetCamera", flags);
        var priorNoticeCamera = cameraField.GetValue(noticeFollow);
        var priorDialogueCamera = cameraField.GetValue(dialogueFollow);
        var cameraObject = new GameObject("NotificationPriorityCamera", typeof(Camera));
        cameraObject.transform.position = new Vector3(1000, 1000, 1000);
        cameraField.SetValue(noticeFollow, cameraObject.GetComponent<Camera>());
        cameraField.SetValue(dialogueFollow, cameraObject.GetComponent<Camera>());
        dialogueUi.HideImmediately();
        tooltipUi.ClearTooltip();
        // The Editor spawn point sits inside the evidence box's deposit zone, which owns the
        // shared prompt slot while the player is inside it (hides it every frame when nothing
        // eligible is held). Switch the zones off for this check and restore them after.
        var boxZones = FindObjectsByType<EvidenceBoxDepositZone>(FindObjectsSortMode.None);
        foreach (var z in boxZones) z.enabled = false;
        // The live scene posts its own tooltips (photo-point instructions, zone tips); let any finish first.
        float settle = 0f;
        while (tooltipUi.CurrentTooltip != null && settle < 6f) { settle += Time.deltaTime; yield return null; }

        // Case 1: a refusal (catalog Error dialogue) is shown while a [B] prompt holds the tooltip panel.
        NotificationManager.ShowPrompt(AmbientText);
        yield return new WaitForSeconds(0.4f);
        string refusal = GameDialogue.Text("evidence-step-marker-first");
        NotificationManager.Notify(refusal, DialogueKind.Error);
        bool sawRefusal = false;
        float t = 0f;
        while (t < 2f && !sawRefusal)
        {
            t += Time.deltaTime;
            sawRefusal = dialogueLabel.text == refusal && dialogueUi.IsShowing;
            yield return null;
        }
        Check("refusal displayed on the dialogue panel while a [B] prompt was up", sawRefusal);
        Check($"the [B] prompt stayed on the tooltip panel meanwhile (shown whenever no tooltip covers it) [prompt={tooltipUi.HasAmbientPrompt} tooltip='{tooltipUi.CurrentTooltip}' label='{tooltipLabel.text}']",
            tooltipUi.HasAmbientPrompt && (tooltipUi.CurrentTooltip != null || tooltipLabel.text == AmbientText));

        // Case 2: a timed tooltip covers the prompt, then the prompt comes back by itself.
        tooltipUi.ShowTooltip("Measure the west wall.", 0.6f);
        yield return null;
        Check("a timed tooltip takes the tooltip label over the prompt", tooltipLabel.text == "Measure the west wall.");
        bool resumed = false;
        t = 0f;
        while (t < 8f && !resumed)
        {
            t += Time.deltaTime;
            resumed = tooltipUi.CurrentTooltip == null && tooltipLabel.text == AmbientText;
            yield return null;
        }
        Check($"the prompt resumed after the tooltip expired (context still true) [prompt={tooltipUi.HasAmbientPrompt} tooltip='{tooltipUi.CurrentTooltip}' label='{tooltipLabel.text}']", resumed);

        NotificationManager.HidePrompt();
        dialogueUi.HideImmediately();
        foreach (var z in boxZones) if (z != null) z.enabled = true;
        cameraField.SetValue(noticeFollow, priorNoticeCamera);
        cameraField.SetValue(dialogueFollow, priorDialogueCamera);
        Destroy(cameraObject);
        foreach (var presenter in suspended) if (presenter != null) presenter.enabled = true;
        if (photoInstructionPending && photography != null) photography.Invoke("ActivateFirstPoint", 0f);
        report.AppendLine(failures == 0 ? "=== PASS: refusals are never hidden by ambient prompts ===" : $"=== FAILED with {failures} problem(s) ===");
        Report = report.ToString();
        Debug.Log(Report);
        IsDone = true;
    }
}
