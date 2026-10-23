// Assets/_Project/Scripts/Testing/FirstResponderTurnoverTest.cs
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Play-mode regression for the First Responder scene turnover (SceneTurnover /
/// FirstResponderTurnover / SceneCordon) in CSI_Environment. Must start from a FRESH scene
/// with turnover still pending (the regression runner's turnover bypass is skipped for this
/// test). Drives the real NPC: prompt in range, Interact (what A does), B-skips through the
/// conversation via the dialogue panel's own Skip, then checks everything the turnover
/// opens - and finally reloads the scene to prove the state is session-local.
///
/// Run from the editor via:
///     FindFirstObjectByType&lt;FirstResponderTurnoverTest&gt;().RunAndLog()
/// </summary>
public class FirstResponderTurnoverTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }

    private StringBuilder report;
    private int failures;

    public void RunAndLog()
    {
        DontDestroyOnLoad(gameObject);   // survives the scene reload in the reset check
        StartCoroutine(Run());
    }

    private void Check(string label, bool ok)
    {
        report.AppendLine($"  {(ok ? "PASS" : "FAIL")}: {label}");
        if (!ok) failures++;
    }

    private IEnumerator Run()
    {
        IsDone = false;
        failures = 0;
        report = new StringBuilder("=== First Responder turnover test ===\n");

        var ctl = SceneTurnover.Controller;
        if (ctl == null || !SceneTurnover.IsPending)
        {
            Finish("FAIL: needs a fresh CSI_Environment with turnover pending (controller=" + (ctl != null) + ", complete=" + SceneTurnover.IsComplete + ").");
            yield break;
        }

        // This suite exercises FR handover and its existing narrative routing. Contextual
        // reasoning has its own suite and intentionally defers the professor's first line.
        if (ReasoningCheckpointManager.Instance != null) ReasoningCheckpointManager.Instance.enabled = false;
        if (HypothesisCheckpointManager.Instance != null) HypothesisCheckpointManager.Instance.enabled = false;

        var photos = ScenePhotographyManager.Instance;
        var panel = STCSNotificationUI.Instance;
        var tooltipUi = NotificationManager.Instance != null ? NotificationManager.Instance.UI : null;
        var logger = SessionLogger.Instance;
        var esm = EvidenceStateManager.Instance;
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var tooltipLabel = tooltipUi != null ? typeof(NotificationUI).GetField("label", flags).GetValue(tooltipUi) as TMP_Text : null;
        var cordon = FindFirstObjectByType<SceneCordon>();

        // Let start-up settle past the photo manager's first-instruction delay and the narrative initial pass.
        yield return new WaitForSeconds(2.6f);
        report.AppendLine("  DIAGNOSTIC: before turnover mission-scene-start completed=" +
            (NarrativeEventManager.Instance != null && NarrativeEventManager.Instance.IsCompleted("mission-scene-start")));

        // --- Before turnover ---------------------------------------------------------
        Check($"1. fresh scene: current objective is 'Receive the First Responder's briefing' [current='{CurrentObjectiveTitle()}']",
            CurrentObjectiveTitle() == "Receive the First Responder's briefing");
        Check("1b. investigation objectives still hidden (obj-document-scene)", StateOf("obj-document-scene") == ObjectiveState.Hidden);
        Check($"2. investigation timer not running [InvestigationSeconds={SceneTurnover.InvestigationSeconds:F2}, started={SceneTurnover.InvestigationStarted}]",
            !SceneTurnover.InvestigationStarted && SceneTurnover.InvestigationSeconds == 0f);
        Check($"3. scene photography not started (no active photo point after its start delay) [current={(photos != null && photos.CurrentPoint != null ? photos.CurrentPoint.photoPointId : "none")}]",
            photos != null && photos.CurrentPoint == null);
        Check($"P1. fresh scene: zero photography route circles visible [visible={VisibleZones(photos)}]", photos != null && VisibleZones(photos) == 0);
        string routeBefore = RouteOrder(photos);
        Check("4. evidence tools locked (ToolInputLock held)", ToolInputLock.IsLocked);
        var handoff = ctl.GetComponent<FirstResponderHandoff>();
        Check("H1. First Responder holds the scene notes (clipboard) before turnover", handoff != null && handoff.IsHolding);
        Check("4b. no narrative/professor guidance played before turnover (beat-1 not completed)",
            NarrativeEventManager.Instance == null || !NarrativeEventManager.Instance.IsCompleted("beat-1-initial-briefing"));

        // Walking onto evidence before turnover must not discover it.
        var prop = FindWalkInProp(esm);
        var player = GameObject.FindGameObjectWithTag("Player");
        var playerCol = player != null ? player.GetComponentInChildren<Collider>() : null;
        var onEnter = typeof(EvidenceProp).GetMethod("OnTriggerEnter", flags);
        if (prop != null && playerCol != null)
        {
            onEnter.Invoke(prop, new object[] { playerCol });
            Check($"4c. walking onto evidence before turnover discovers nothing ({prop.evidenceId} still below Found)",
                !esm.IsAtOrPastStatus(prop.evidenceId, EvidenceStatus.Found));
        }
        else
        {
            report.AppendLine("  (4c skipped: no walk-in EvidenceProp / player collider in this scene)");
        }

        // Early entry: the cordon warns, nothing starts.
        if (cordon != null)
        {
            var rig = GameObject.Find("[BuildingBlock] Camera Rig");
            var cam = Camera.main;
            if (rig != null && cam != null)
            {
                int before = cordon.EarlyEntryWarnings;
                Vector3 home = rig.transform.position;
                rig.transform.position = home + new Vector3(0f, 0f, 3f);   // into the yard, past the tape
                yield return null; yield return null;
                bool attemptedInside = cordon.RequiresBriefingAt(home + new Vector3(0f, 1.6f, 3f));
                bool returnedOutside = !cordon.RequiresBriefingAt(cam.transform.position);
                rig.transform.position = home;
                yield return null; yield return null;
                Check($"16. early entry warns, returns outside and starts nothing [attemptedInside={attemptedInside}, returnedOutside={returnedOutside}, warnings {before}->{cordon.EarlyEntryWarnings}, pending={SceneTurnover.IsPending}]",
                    attemptedInside && returnedOutside && cordon.EarlyEntryWarnings == before + 1 && SceneTurnover.IsPending && photos.CurrentPoint == null);
            }
            Check("C1. cordon visible before turnover, controlled entrance closed", cordon.CordonVisible && !cordon.EntranceIsOpen);
        }

        // Walking into a route point's standing zone before turnover reveals nothing.
        if (photos != null && photos.Points.Count > 0)
        {
            var rig = GameObject.Find("[BuildingBlock] Camera Rig");
            var cam = Camera.main;
            if (rig != null && cam != null)
            {
                var routeStart = photos.Points[0];
                Vector3 home = rig.transform.position;
                Vector3 offset = cam.transform.position - rig.transform.position;
                rig.transform.position = new Vector3(routeStart.transform.position.x - offset.x, home.y, routeStart.transform.position.z - offset.z);
                yield return null; yield return null; yield return null;
                bool insideZone = routeStart.ContainsPlayer(cam.transform.position) || Vector3.Distance(Flat(cam.transform.position), Flat(routeStart.transform.position)) <= routeStart.standingRadius;
                int visibleNear = VisibleZones(photos);
                rig.transform.position = home;
                yield return null; yield return null;
                Check($"P2. standing in route point '{routeStart.photoPointId}' before turnover: circle stays hidden [inZone={insideZone}, visible={visibleNear}, current={(photos.CurrentPoint != null ? photos.CurrentPoint.photoPointId : "none")}]",
                    insideZone && visibleNear == 0 && photos.CurrentPoint == null);
            }
        }

        // --- Interaction (with the visual disabled: logic must not depend on it) -----
        var visual = ctl.transform.Find("Visual");
        if (visual != null) visual.gameObject.SetActive(false);
        tooltipUi?.ClearTooltip();
        yield return null; yield return null;
        string promptText = GameDialogue.Text(ctl.PromptId);
        Check($"5. in range: tooltip prompt '{promptText}' is up [inRange={ctl.PlayerInRange()}, label='{(tooltipLabel != null ? tooltipLabel.text : null)}']",
            ctl.IsPromptShown && tooltipUi != null && tooltipUi.HasAmbientPrompt && tooltipLabel != null && tooltipLabel.text == promptText);
        Check("15. NPC visual disabled - interaction logic still live", visual != null && !visual.gameObject.activeSelf && ctl.IsPromptShown);

        // Every dialogue-channel line requested from the moment turnover completes.
        var postTurnover = new List<DialogueRequest>();
        var requestedPhases = new Dictionary<string, ScenarioPhase>();
        System.Action<DialogueRequest> record = r => {
            if (r.id == "narr-mission-scene-start-1") report.AppendLine("  DIAGNOSTIC: scene-start request turnover=" + SceneTurnover.IsComplete + ", type=" + r.type);
            if (SceneTurnover.IsComplete && r.type == DialogueEntryType.Dialogue)
            {
                postTurnover.Add(r);
                if (r.id != null) requestedPhases[r.id] = SceneTurnover.Phase;
            }
        };
        GameDialogue.OnRequested += record;

        var statusesBefore = SnapshotStatuses(esm);
        int historyBefore = DialogueHistoryManager.Instance != null ? DialogueHistoryManager.Instance.Entries.Count : 0;
        panel.HideImmediately();
        Check("6a. Interact() starts the conversation", ctl.Interact());
        Check("6b. prompt withdrawn while talking (so B skips dialogue)", !ctl.IsPromptShown);

        string line1 = GameDialogue.Text(ctl.DialogueLineIds[0]);
        bool sawLine1 = false;
        float t = 0f;
        while (t < 3f && !sawLine1)
        {
            t += Time.unscaledDeltaTime;
            sawLine1 = panel.IsShowing && panel.CurrentText != null && panel.CurrentText.Contains(line1);
            yield return null;
        }
        Check($"6c. First Responder dialogue begins on the dialogue panel [shown='{panel.CurrentText}']", sawLine1);
        Check("6d. FirstResponderInteractionStarted logged once", Count(logger, SessionEventType.FirstResponderInteractionStarted) == 1);
        Check("6e. still pending while lines play", SceneTurnover.IsPending);

        // --- B-skip through every line (the panel's own Skip is what B calls) --------
        var seen = new HashSet<string>();
        t = 0f;
        while (t < 20f && !SceneTurnover.IsComplete)
        {
            t += Time.unscaledDeltaTime;
            if (panel.IsShowing && panel.CurrentText != null)
            {
                foreach (var id in ctl.DialogueLineIds)
                {
                    if (panel.CurrentText.Contains(GameDialogue.Text(id))) seen.Add(id);
                }
                if (panel.Countdown.VisibleSeconds > ViCSIDialogueCatalog.Current.skipProtectionSeconds + 0.1f) panel.Skip();
            }
            yield return null;
        }
        Check($"13. B-skipping showed every line ({seen.Count}/{ctl.DialogueLineIds.Length}) and still completed the conversation",
            seen.Count == ctl.DialogueLineIds.Length && SceneTurnover.IsComplete);
        Check("7. finishing the dialogue completes turnover", SceneTurnover.IsComplete);
        Check("H2. the clipboard has left the First Responder's hand after turnover (handed over)", handoff != null && !handoff.IsHolding);

        Check("7b. FR turnover enters IOC assessment without unlocking SOCO", SceneTurnover.Phase == ScenarioPhase.IOCAssessment
            && SceneTurnover.IsInvestigationBlocked && ToolInputLock.IsLocked && !SceneTurnover.InvestigationStarted);
        // This diagnostic's remaining checks cover the existing SOCO investigation explicitly.
        var assessment = FindAnyObjectByType<IOCAssessmentInteraction>();
        foreach (var observation in assessment.Definition.observations)
            SceneTurnover.RecordIOCObservation(observation.observationId);
        SceneTurnover.CompleteIOCAssessment();
        SceneTurnover.TryAdvance(ScenarioPhase.SOCORequested);
        SceneTurnover.TryAdvance(ScenarioPhase.SOCOTurnover);
        SceneTurnover.TryAdvance(ScenarioPhase.SOCOInvestigation);

        // --- After SOCO turnover -----------------------------------------------------
        yield return new WaitForSeconds(0.5f);
        Check("8a. Interact() after turnover does nothing (lines never replay)", !ctl.Interact());
        ctl.CompleteWithoutDialogue();
        Check($"8b. turnover completed exactly once [SceneTurnoverCompleted={Count(logger, SessionEventType.SceneTurnoverCompleted)}, InteractionStarted={Count(logger, SessionEventType.FirstResponderInteractionStarted)}]",
            Count(logger, SessionEventType.SceneTurnoverCompleted) == 1 && Count(logger, SessionEventType.FirstResponderInteractionStarted) == 1);
        Check($"9. objective changed to 'Begin documenting the crime scene' [current='{CurrentObjectiveTitle()}', talk={StateOf("obj-talk-first-responder")}]",
            CurrentObjectiveTitle() == "Begin documenting the crime scene" && StateOf("obj-talk-first-responder") == ObjectiveState.Completed);
        var talk = ObjectiveStateManager.Instance.GetRecord("obj-talk-first-responder");
        Check("9b. the turnover objective is excluded from scoring", talk != null && talk.definition.excludeFromScoring);
        Check($"10. investigation timer running [InvestigationSeconds={SceneTurnover.InvestigationSeconds:F2}]",
            SceneTurnover.InvestigationStarted && SceneTurnover.InvestigationSeconds > 0.3f);

        int history = 0, historyDupes = 0;
        if (DialogueHistoryManager.Instance != null)
        {
            var entries = DialogueHistoryManager.Instance.Entries;
            foreach (var id in ctl.DialogueLineIds)
            {
                string text = GameDialogue.Text(id);
                int n = 0;
                for (int i = 0; i < entries.Count; i++) if (entries[i].text == text) n++;
                if (n >= 1) history++;
                if (n > 1) historyDupes++;
            }
        }
        Check($"14b. each First Responder line is in Dialogue History exactly once [{history}/{ctl.DialogueLineIds.Length}, duplicated={historyDupes}, historyBefore={historyBefore}]",
            history == ctl.DialogueLineIds.Length && historyDupes == 0);

        float waitPhoto = 0f;
        while (waitPhoto < 4f && (photos == null || photos.CurrentPoint == null)) { waitPhoto += Time.deltaTime; yield return null; }
        Check($"11. scene photography activated [current={(photos != null && photos.CurrentPoint != null ? photos.CurrentPoint.photoPointId : "none")}]",
            photos != null && photos.CurrentPoint != null);
        if (photos != null && photos.CurrentPoint != null)
        {
            var current = photos.CurrentPoint;
            bool expectedVisible = !current.ContainsPlayer(Camera.main.transform.position);
            Check($"P3. turnover: current circle follows standing-zone visibility [{current.photoPointId} visible={current.IsZoneVisible}]", current.IsZoneVisible == expectedVisible);
            Check($"P4. only the current circle may show while outside its standing zone [visible={VisibleZones(photos)}]",
                VisibleZones(photos) == (expectedVisible ? 1 : 0) && current.IsZoneVisible == expectedVisible);
            Check($"P5. route order unchanged and starts at its first point [{RouteOrder(photos)}]",
                RouteOrder(photos) == routeBefore && current == photos.Points[0]);

            // A valid photo at the current point advances the route exactly as before.
            var shutter = typeof(ScenePhotographyManager).GetMethod("HandleShutter", flags);
            var camTool = FindFirstObjectByType<PhotographTool>(FindObjectsInactive.Include);
            var cam = Camera.main;
            if (shutter != null && camTool != null && cam != null && photos.Points.Count > 1)
            {
                cam.transform.position = current.transform.position + Vector3.up * 1.6f;
                cam.transform.rotation = current.cameraTarget != null
                    ? Quaternion.LookRotation(current.cameraTarget.position - cam.transform.position)
                    : Quaternion.LookRotation(current.transform.forward);
                shutter.Invoke(photos, new object[] { camTool, false });
                var next = photos.CurrentPoint;
                Check($"P6. valid photo completes '{current.photoPointId}' and advances to the next circle [{(next != null ? next.photoPointId : "none")}, visible={VisibleZones(photos)}]",
                    current.IsCompleted && !current.IsZoneVisible && next == photos.Points[1] && next.IsZoneVisible == !next.ContainsPlayer(cam.transform.position)
                    && VisibleZones(photos) == (next.IsZoneVisible ? 1 : 0));
            }
        }
        Check("12a. evidence tools unlocked (ToolInputLock released)", !ToolInputLock.IsLocked);
        Check("17a. turnover itself changed no evidence status", SameStatuses(statusesBefore, SnapshotStatuses(esm)));
        if (prop != null && playerCol != null)
        {
            onEnter.Invoke(prop, new object[] { playerCol });
            Check($"12b. walking onto evidence works again after turnover ({prop.evidenceId} now Found)", esm.IsAtOrPastStatus(prop.evidenceId, EvidenceStatus.Found));
        }
        if (cordon != null)
        {
            Check("C2. cordon still visible after turnover", cordon.CordonVisible);
            Check("C3. controlled entrance remains closed after turnover", !cordon.EntranceIsOpen && cordon.CordonVisible);
            // Pass-through: nothing solid across the entrance between the posts, at foot/waist/head height.
            Vector3 c = cordon.transform.position;
            var rigGo = GameObject.Find("[BuildingBlock] Camera Rig");
            Transform rigRoot = rigGo != null ? rigGo.transform : null;
            bool clear = true;
            string blocker = null;
            foreach (float y in new[] { 0.3f, 0.9f, 1.5f })
            {
                foreach (float dx in new[] { -0.3f, 0f, 0.3f })
                {
                    var from = new Vector3(c.x + dx, y, c.z - 0.8f);
                    foreach (var hit in Physics.RaycastAll(from, Vector3.forward, 1.8f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        // The player's own rig (it spawns right at the entrance) isn't an obstacle.
                        if (rigRoot != null && hit.collider.transform.IsChildOf(rigRoot)) continue;
                        clear = false; blocker = hit.collider.name;
                    }
                }
            }
            Check($"C4. player can pass through the controlled entrance (no solid collider across it) [blocker={blocker ?? "none"}]", clear);
        }

        // Post-turnover dialogue: straight into the first CSI task, no repeated hand-over.
        yield return new WaitForSeconds(1.5f);
        GameDialogue.OnRequested -= record;
        string first = postTurnover.Count > 0 ? postTurnover[0].id : null;
        Check($"D1. IOC assessment guidance precedes professor guidance, which starts only in SOCO investigation [first='{first}']",
            first == "ioc-assessment-guidance" && requestedPhases[first] == ScenarioPhase.IOCAssessment
            && postTurnover.Count(r => r.id == first) == 1
            && requestedPhases.TryGetValue("narr-mission-scene-start-1", out var professorPhase)
            && professorPhase == ScenarioPhase.SOCOInvestigation);
        bool repeatHandover = false;
        foreach (var r in postTurnover)
        {
            string low = r.text.ToLowerInvariant();
            if (r.sourceId == FirstResponderTurnover.ConversationSourceId
                || low.Contains("clear to begin") || low.Contains("chain of custody's yours")) repeatHandover = true;
        }
        Check($"D2. completed FR conversation is not replayed and no old direct SOCO authorization is emitted [repeated={repeatHandover}]",
            !repeatHandover && postTurnover.GroupBy(r => r.id).All(g => g.Count() == 1));
        if (visual != null) visual.gameObject.SetActive(true);

        // --- Reset: a new session / scene reload starts pending again ------------------
        string sceneName = SceneManager.GetActiveScene().name;
        SceneManager.LoadScene(sceneName);
        yield return null; yield return null;
        yield return new WaitForSeconds(2.6f);   // past the photo manager's first-instruction delay
        var photosAfter = ScenePhotographyManager.Instance;
        Check($"P7. scene reload: circles hidden again until turnover [visible={VisibleZones(photosAfter)}, current={(photosAfter != null && photosAfter.CurrentPoint != null ? photosAfter.CurrentPoint.photoPointId : "none")}]",
            photosAfter != null && VisibleZones(photosAfter) == 0 && photosAfter.CurrentPoint == null);
        Check($"14. scene reload resets turnover [pending={SceneTurnover.IsPending}, complete={SceneTurnover.IsComplete}, timer={SceneTurnover.InvestigationSeconds:F2}, current='{CurrentObjectiveTitle()}']",
            SceneTurnover.IsPending && !SceneTurnover.IsComplete && SceneTurnover.InvestigationSeconds == 0f
            && CurrentObjectiveTitle() == "Receive the First Responder's briefing" && ToolInputLock.IsLocked);

        Finish(failures == 0 ? "=== PASS: First Responder turnover flow ===" : $"=== FAILED with {failures} problem(s) ===");
    }

    private void Finish(string last)
    {
        report.AppendLine(last);
        Report = report.ToString();
        Debug.Log(Report);
        IsDone = true;
    }

    /// <summary>What an objective list would show first: the lowest-order Active, listed objective.</summary>
    private static string CurrentObjectiveTitle()
    {
        var osm = ObjectiveStateManager.Instance;
        if (osm == null) return null;
        Objective best = null;
        foreach (var r in osm.AllRecords)
        {
            if (r?.definition == null || r.state != ObjectiveState.Active || !r.definition.showInObjectiveList || !osm.IsAvailableInCurrentPhase(r.definition)) continue;
            if (best == null || r.definition.order < best.order) best = r.definition;
        }
        return best != null ? best.title : null;
    }

    private static int VisibleZones(ScenePhotographyManager photos)
    {
        int n = 0;
        if (photos == null) return n;
        foreach (var p in photos.Points) if (p != null && p.IsZoneVisible) n++;
        return n;
    }

    private static string RouteOrder(ScenePhotographyManager photos)
    {
        if (photos == null) return "";
        var ids = new List<string>();
        foreach (var p in photos.Points) ids.Add(p.sequenceIndex + ":" + p.photoPointId);
        return string.Join(" > ", ids);
    }

    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    private static ObjectiveState? StateOf(string objectiveId) => ObjectiveStateManager.Instance?.GetRecord(objectiveId)?.state;

    private static int Count(SessionLogger logger, SessionEventType type) =>
        logger != null && logger.EventCounts.TryGetValue(type, out int n) ? n : 0;

    private static EvidenceProp FindWalkInProp(EvidenceStateManager esm)
    {
        if (esm == null) return null;
        var field = typeof(EvidenceProp).GetField("foundRequiresExternalTrigger", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
        foreach (var p in FindObjectsByType<EvidenceProp>(FindObjectsSortMode.None))
        {
            if (string.IsNullOrEmpty(p.evidenceId) || esm.GetRecord(p.evidenceId) == null) continue;
            if (field != null && (bool)field.GetValue(p)) continue;
            if (esm.IsAtOrPastStatus(p.evidenceId, EvidenceStatus.Found)) continue;
            return p;
        }
        return null;
    }

    private static Dictionary<string, EvidenceStatus> SnapshotStatuses(EvidenceStateManager esm)
    {
        var map = new Dictionary<string, EvidenceStatus>();
        if (esm == null) return map;
        foreach (var r in esm.AllRecords) if (r?.definition != null) map[r.definition.evidenceId] = r.status;
        return map;
    }

    private static bool SameStatuses(Dictionary<string, EvidenceStatus> a, Dictionary<string, EvidenceStatus> b)
    {
        if (a.Count != b.Count) return false;
        foreach (var kv in a) if (!b.TryGetValue(kv.Key, out var s) || s != kv.Value) return false;
        return true;
    }
}
