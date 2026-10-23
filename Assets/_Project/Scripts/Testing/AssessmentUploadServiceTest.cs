// Assets/_Project/Scripts/Testing/AssessmentUploadServiceTest.cs
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

/// <summary>
/// End-to-end checks for the assessment upload layer (AssessmentSessionUploadDto,
/// AssessmentUploadQueueStore, AssessmentUploadService, MockAssessmentUploadTransport) -
/// same "checked-in test beats a memory of one" reasoning as this project's other test
/// scripts. Runs entirely offline against MockAssessmentUploadTransport and an isolated
/// temp queue directory - never touches Application.persistentDataPath's real queue, a
/// real SessionLogger session already in progress, or any real network endpoint.
///
/// Coroutine-driven (like NarrativeEventManagerTest/StoryContentTest): retry backoff and
/// Task-based upload attempts genuinely span real frames/seconds.
/// </summary>
public sealed class AssessmentUploadServiceTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }

    public void RunAllTests() => StartCoroutine(RunAllTestsRoutine());

    private IEnumerator RunAllTestsRoutine()
    {
        var failures = new List<string>();
        var report = new StringBuilder();
        report.AppendLine("=== AssessmentUploadServiceTest ===");

        string tempRoot = Path.Combine(Path.GetTempPath(), "vicsi_upload_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        GameObject serviceGo = null;
        AssessmentUploadConfiguration config = null;
        MockAssessmentUploadTransport mockTransport = null;
        GameObject pairingGoMain = null;

        // AssessmentSessionBuilder deliberately prefers a LIVE SessionLogger.Instance's
        // own SessionId over whatever sessionId a passed-in log file embeds (correct for
        // real usage, where the two are always the same value - see the builder's own
        // class comment). This scene's real SessionLogger is live during this Play
        // session, which would otherwise make every synthetic per-test session file
        // resolve to the SAME real GUID instead of "session-a"/"session-b"/... - a real
        // consequence confirmed live, not a hypothetical. Neutralized for the duration
        // of this test only, same capture-and-restore pattern already established by
        // EndingManagerTest/RelationshipStateManagerTest/ObjectiveStateManagerTest for
        // exactly this class of risk.
        var previousSessionLoggerInstance = SessionLogger.Instance;
        RestoreStaticInstance(typeof(SessionLogger), null);

        // Captured here (before pairingGoMain shadows it below) so the real scene's
        // PairingIdentityService.Instance (on _Managers) is restored once this whole
        // test finishes - same capture-and-restore discipline as SessionLogger above.
        var previousPairingIdentityInstanceMain = PairingIdentityService.Instance;

        try
        {
            // --- 1. Payload serialization round-trip (no service involved) ---
            var original = new AssessmentSessionUploadDto
            {
                payloadVersion = 1,
                sessionId = Guid.NewGuid().ToString(),
                scenarioId = "CSI-ENVIRONMENT-001",
                durationSeconds = 123.5,
                hasClientReportedScores = true
            };
            original.clientReportedScores.objectivesCompleted = 3;
            original.evidenceResults.Add(new EvidenceResultDto { evidenceId = "EVD-014", finalStatus = "Processed" });
            string json = JsonUtility.ToJson(original);
            var roundTripped = JsonUtility.FromJson<AssessmentSessionUploadDto>(json);
            Check(failures, report, "DTO round-trips through JsonUtility (sessionId)", roundTripped.sessionId == original.sessionId);
            Check(failures, report, "DTO round-trips through JsonUtility (nested list)", roundTripped.evidenceResults.Count == 1 && roundTripped.evidenceResults[0].evidenceId == "EVD-014");
            Check(failures, report, "DTO round-trips through JsonUtility (nested optional section)", roundTripped.clientReportedScores.objectivesCompleted == 3);

            // A payload from an EARLIER, hypothetically smaller version (fewer fields
            // than today's DTO) must still parse without throwing - JsonUtility leaves
            // any field absent from the JSON at its C# default, which is exactly
            // "backward compatible" for an append-only DTO.
            string minimalJson = "{\"payloadVersion\":1,\"sessionId\":\"abc-123\"}";
            AssessmentSessionUploadDto minimal = null;
            bool threw = false;
            try { minimal = JsonUtility.FromJson<AssessmentSessionUploadDto>(minimalJson); }
            catch (Exception) { threw = true; }
            Check(failures, report, "Minimal/older-shaped JSON parses without throwing (backward compatibility)", !threw && minimal != null && minimal.sessionId == "abc-123");

            // --- Set up an isolated service + mock transport ---
            config = ScriptableObject.CreateInstance<AssessmentUploadConfiguration>();
            config.apiBaseUrl = "https://mock.invalid";
            config.requestTimeoutSeconds = 5f;
            config.maxRetryAttempts = 3;
            config.initialRetryDelaySeconds = 0.2f;
            config.maxRetryDelaySeconds = 0.6f;
            config.automaticUploadEnabled = true;
            config.uploadRawEventLogEnabled = true;
            config.maxEventsPerRequest = 5000;

            // Procedural introduction events are informational and must survive raw export
            // without changing the existing scoring/payload sections.
            var currentScenario = ScriptableObject.CreateInstance<ScenarioDefinition>();
            currentScenario.scenarioId = "CSI-ENVIRONMENT-001";
            currentScenario.scenarioVersion = "1.1.0";
            currentScenario.scoringRulesVersion = "1.6.0";
            currentScenario.groundTruthVersion = "1.2.0";
            try
            {
                string baselineLog = BuildFakeSessionLog(tempRoot, "baseline-procedure");
                string procedureLog = BuildFakeSessionLog(tempRoot, "current-procedure", true);
                var baseline = AssessmentSessionBuilder.Build(baselineLog, config, currentScenario, "conclusion-peter", "ending-peter-unsupported");
                var procedure = AssessmentSessionBuilder.Build(procedureLog, config, currentScenario, "conclusion-peter", "ending-peter-unsupported");
                Check(failures, report, "IOC/SOCO events export in order with current versions/outcome/timing and unchanged assessment sections",
                    procedure.orderedEvents.Count == 14
                    && procedure.orderedEvents[1].eventType == "FirstResponderInteractionStarted"
                    && procedure.orderedEvents[12].eventType == "SOCOInvestigationStarted"
                    && procedure.scenarioVersion == "1.1.0" && procedure.scoringRulesVersion == "1.6.0" && procedure.groundTruthVersion == "1.2.0"
                    && procedure.selectedConclusionId == "conclusion-peter" && procedure.resolvedEndingId == "ending-peter-unsupported"
                    && procedure.durationSeconds == baseline.durationSeconds
                    && procedure.evidenceResults.Count == baseline.evidenceResults.Count
                    && procedure.reportLineResults.Count == baseline.reportLineResults.Count
                    && procedure.hypothesisResults.Count == baseline.hypothesisResults.Count
                    && procedure.relationshipResults.Count == baseline.relationshipResults.Count
                    && procedure.proceduralViolations.Count == baseline.proceduralViolations.Count
                    && JsonUtility.ToJson(procedure.clientReportedScores) == JsonUtility.ToJson(baseline.clientReportedScores));
            }
            finally { DestroyImmediate(currentScenario); }

            var scenario = ScriptableObject.CreateInstance<ScenarioDefinition>();
            scenario.scenarioId = "CSI-ENVIRONMENT-001";
            scenario.scenarioVersion = "1.0.0";
            scenario.scoringRulesVersion = "1.0.0";

            serviceGo = new GameObject("TempAssessmentUploadService");
            serviceGo.SetActive(false);
            var service = serviceGo.AddComponent<AssessmentUploadService>();
            SetPrivateField(service, "configuration", config);
            SetPrivateField(service, "scenario", scenario);
            // Test queue + mock transport BEFORE activation, so OnEnable never touches the real queue.
            service.SetQueueRootForTesting(tempRoot);
            mockTransport = new MockAssessmentUploadTransport();
            service.SetTransportForTesting(mockTransport);
            serviceGo.SetActive(true);

            // Tests 2-16 below exercise upload/retry/duplicate behavior and don't care
            // about WHO is paired - they just need AttemptUploadAsync's restart-
            // authentication identity check (see IdentityMatchesCurrentPairing) to pass
            // so it doesn't block them before they ever reach the transport. A single
            // shared "paired" PairingIdentityService, set up once here, does that; test
            // 17 below neutralizes and replaces it with its own isolated instance for
            // the scenarios that specifically exercise pairing/re-pairing.
            pairingGoMain = new GameObject("TempPairingIdentityServiceMain");
            var pairingIdentityMain = pairingGoMain.AddComponent<PairingIdentityService>();
            pairingIdentityMain.SetIdentityForTesting("Test Harness Student", "Test Harness Class", "harness-token", DateTime.UtcNow.AddHours(4));

            // --- 2. Successful upload ---
            string sessionA = BuildFakeSessionLog(tempRoot, "session-a");
            var entryA = service.QueueSessionForUpload(sessionA, "conclusion-wife", "ending-wife-convicted");
            Check(failures, report, "QueueSessionForUpload returns a Pending/Uploading entry", entryA != null);

            yield return WaitFor(() => FindEntry(service, "session-a")?.status == AssessmentUploadStatus.Uploaded, 3f);
            var uploadedA = FindEntry(service, "session-a");
            Check(failures, report, "Successful upload reaches Uploaded", uploadedA?.status == AssessmentUploadStatus.Uploaded);
            Check(failures, report, "Successful upload records a serverReceiptId", !string.IsNullOrEmpty(uploadedA?.serverReceiptId));

            // --- 3. Duplicate prevention (same sessionId queued twice) ---
            var entryAAgain = service.QueueSessionForUpload(sessionA, "conclusion-wife", "ending-wife-convicted");
            var allEntriesForA = service.GetAllEntries().FindAll(e => e.sessionId == "session-a");
            Check(failures, report, "Re-queueing the same sessionId does not create a second entry", allEntriesForA.Count == 1);

            // --- 4. Identical-payload resubmission (HTTP 200, data.duplicate=true) with MATCHING sessionId -> treated as success ---
            // Per the real contract, an identical duplicate is signalled via the normal
            // 200 success envelope's data.duplicate flag, never a different HTTP status
            // - see UnityWebRequestUploadTransport's own class comment.
            string sessionB = BuildFakeSessionLog(tempRoot, "session-b");
            mockTransport.EnqueueResult("session-b", AssessmentUploadAttemptResult.Ok(new AssessmentSubmissionResponseDto { success = true, data = new AssessmentSubmissionDataDto { sessionId = "session-b", duplicate = true, receiptId = "DUP-1" } }, 200));
            service.QueueSessionForUpload(sessionB, "", "");
            yield return WaitFor(() => FindEntry(service, "session-b")?.status != AssessmentUploadStatus.Pending && FindEntry(service, "session-b")?.status != AssessmentUploadStatus.Uploading, 3f);
            Check(failures, report, "Identical duplicate (200, duplicate=true) with matching sessionId is treated as Uploaded", FindEntry(service, "session-b")?.status == AssessmentUploadStatus.Uploaded);

            // --- 5. Duplicate response with MISMATCHED sessionId -> treated as Rejected, never silently trusted ---
            string sessionC = BuildFakeSessionLog(tempRoot, "session-c");
            mockTransport.EnqueueResult("session-c", AssessmentUploadAttemptResult.Ok(new AssessmentSubmissionResponseDto { success = true, data = new AssessmentSubmissionDataDto { sessionId = "session-DIFFERENT", duplicate = true } }, 200));
            service.QueueSessionForUpload(sessionC, "", "");
            yield return WaitFor(() => FindEntry(service, "session-c")?.status != AssessmentUploadStatus.Pending && FindEntry(service, "session-c")?.status != AssessmentUploadStatus.Uploading, 3f);
            Check(failures, report, "A duplicate response for a MISMATCHED sessionId is NOT trusted as success", FindEntry(service, "session-c")?.status == AssessmentUploadStatus.Rejected);

            // --- 5b. Conflicting duplicate (HTTP 409, SESSION_PAYLOAD_CONFLICT) -> Rejected immediately, no retry ---
            string sessionC2 = BuildFakeSessionLog(tempRoot, "session-c2");
            mockTransport.EnqueueResult("session-c2", AssessmentUploadAttemptResult.Fail(AssessmentUploadErrorCategory.ValidationRejected, 409, "conflict", new ApiErrorResponseDto { success = false, error = new ApiErrorDto { code = "SESSION_PAYLOAD_CONFLICT", message = "session-c2 was already submitted with a different payload", retryable = false } }));
            service.QueueSessionForUpload(sessionC2, "", "");
            yield return WaitFor(() => FindEntry(service, "session-c2")?.status == AssessmentUploadStatus.Rejected, 3f);
            Check(failures, report, "HTTP 409 SESSION_PAYLOAD_CONFLICT reaches Rejected without retrying", FindEntry(service, "session-c2")?.status == AssessmentUploadStatus.Rejected && FindEntry(service, "session-c2")?.attemptCount == 1);

            // --- 6. Validation rejected (400) -> Rejected immediately, no retry ---
            string sessionD = BuildFakeSessionLog(tempRoot, "session-d");
            mockTransport.EnqueueResult("session-d", AssessmentUploadAttemptResult.Fail(AssessmentUploadErrorCategory.ValidationRejected, 400, "bad", new ApiErrorResponseDto { success = false, error = new ApiErrorDto { code = "VALIDATION_ERROR", message = "durationSeconds must be >= 0", retryable = false } }));
            service.QueueSessionForUpload(sessionD, "", "");
            yield return WaitFor(() => FindEntry(service, "session-d")?.status == AssessmentUploadStatus.Rejected, 3f);
            var rejectedD = FindEntry(service, "session-d");
            Check(failures, report, "HTTP 400 validation failure reaches Rejected", rejectedD?.status == AssessmentUploadStatus.Rejected);
            Check(failures, report, "HTTP 400 failure does NOT retry (attemptCount == 1)", rejectedD?.attemptCount == 1);

            // --- 7. Authentication failed (401) -> FailedPermanently immediately ---
            string sessionE = BuildFakeSessionLog(tempRoot, "session-e");
            mockTransport.EnqueueResult("session-e", AssessmentUploadAttemptResult.Fail(AssessmentUploadErrorCategory.AuthenticationFailed, 401, "unauthorized"));
            service.QueueSessionForUpload(sessionE, "", "");
            yield return WaitFor(() => FindEntry(service, "session-e")?.status == AssessmentUploadStatus.FailedPermanently, 3f);
            Check(failures, report, "HTTP 401 reaches FailedPermanently without exhausting retries", FindEntry(service, "session-e")?.attemptCount == 1);

            // --- 8. Payload too large (413) -> FailedPermanently immediately ---
            string sessionF = BuildFakeSessionLog(tempRoot, "session-f");
            mockTransport.EnqueueResult("session-f", AssessmentUploadAttemptResult.Fail(AssessmentUploadErrorCategory.PayloadTooLarge, 413, "too big"));
            service.QueueSessionForUpload(sessionF, "", "");
            yield return WaitFor(() => FindEntry(service, "session-f")?.status == AssessmentUploadStatus.FailedPermanently, 3f);
            Check(failures, report, "HTTP 413 reaches FailedPermanently without exhausting retries", FindEntry(service, "session-f")?.attemptCount == 1);

            // --- 9. Rate limited (429) then success -> retries and eventually uploads ---
            string sessionG = BuildFakeSessionLog(tempRoot, "session-g");
            mockTransport.EnqueueResult("session-g", AssessmentUploadAttemptResult.Fail(AssessmentUploadErrorCategory.RateLimited, 429, "slow down", retryAfterSeconds: 0.1f));
            mockTransport.EnqueueResult("session-g", AssessmentUploadAttemptResult.Ok(new AssessmentSubmissionResponseDto { success = true, data = new AssessmentSubmissionDataDto { sessionId = "session-g", receiptId = "OK-G" } }, 200));
            service.QueueSessionForUpload(sessionG, "", "");
            yield return WaitFor(() => FindEntry(service, "session-g")?.status == AssessmentUploadStatus.Uploaded, 4f);
            var uploadedG = FindEntry(service, "session-g");
            Check(failures, report, "HTTP 429 then success eventually reaches Uploaded", uploadedG?.status == AssessmentUploadStatus.Uploaded);
            Check(failures, report, "HTTP 429 retry incremented attemptCount", uploadedG?.attemptCount == 2);

            // --- 10. No network, then success on a later attempt ---
            string sessionH = BuildFakeSessionLog(tempRoot, "session-h");
            mockTransport.EnqueueResult("session-h", AssessmentUploadAttemptResult.Fail(AssessmentUploadErrorCategory.NoNetwork, 0, "no network"));
            mockTransport.EnqueueResult("session-h", AssessmentUploadAttemptResult.Ok(new AssessmentSubmissionResponseDto { success = true, data = new AssessmentSubmissionDataDto { sessionId = "session-h", receiptId = "OK-H" } }, 200));
            service.QueueSessionForUpload(sessionH, "", "");
            yield return WaitFor(() => FindEntry(service, "session-h")?.status == AssessmentUploadStatus.Uploaded, 4f);
            Check(failures, report, "No-network failure recovers to Uploaded once connectivity returns", FindEntry(service, "session-h")?.status == AssessmentUploadStatus.Uploaded);

            // --- 11. Server error (500) exhausting all attempts -> FailedPermanently ---
            string sessionI = BuildFakeSessionLog(tempRoot, "session-i");
            for (int i = 0; i < config.maxRetryAttempts + 1; i++)
            {
                mockTransport.EnqueueResult("session-i", AssessmentUploadAttemptResult.Fail(AssessmentUploadErrorCategory.ServerError, 500, "boom"));
            }
            service.QueueSessionForUpload(sessionI, "", "");
            yield return WaitFor(() => FindEntry(service, "session-i")?.status == AssessmentUploadStatus.FailedPermanently, 5f);
            var failedI = FindEntry(service, "session-i");
            Check(failures, report, $"Persistent 500s exhaust maxRetryAttempts and reach FailedPermanently (attempts={failedI?.attemptCount})", failedI?.status == AssessmentUploadStatus.FailedPermanently);
            Check(failures, report, "Retry loop stops AT maxRetryAttempts, not beyond", failedI?.attemptCount == config.maxRetryAttempts);

            // --- 12. Invalid/unparseable server response ---
            string sessionJ = BuildFakeSessionLog(tempRoot, "session-j");
            mockTransport.EnqueueResult("session-j", AssessmentUploadAttemptResult.Fail(AssessmentUploadErrorCategory.InvalidServerResponse, 200, "body did not parse"));
            mockTransport.EnqueueResult("session-j", AssessmentUploadAttemptResult.Ok(new AssessmentSubmissionResponseDto { success = true, data = new AssessmentSubmissionDataDto { sessionId = "session-j" } }, 200));
            service.QueueSessionForUpload(sessionJ, "", "");
            yield return WaitFor(() => FindEntry(service, "session-j")?.status == AssessmentUploadStatus.Uploaded, 4f);
            Check(failures, report, "Invalid server response is retried, not treated as permanent", FindEntry(service, "session-j")?.status == AssessmentUploadStatus.Uploaded);

            // --- 13. Original session log is never touched by any of the above ---
            bool allLogsSurvive = true;
            foreach (var id in new[] { "session-a", "session-d", "session-e", "session-i" })
            {
                if (!File.Exists(Path.Combine(tempRoot, id + ".jsonl"))) allLogsSurvive = false;
            }
            Check(failures, report, "Original local session .jsonl logs are never deleted, including after permanent failure/rejection", allLogsSurvive);

            // --- 14. Queue index recovery after corruption ---
            string indexPath = Path.Combine(tempRoot, "assessment_upload_queue_index.json");
            string backup = File.ReadAllText(indexPath);
            File.WriteAllText(indexPath, "{ not valid json ][");
            var recoveringStore = new AssessmentUploadQueueStore(tempRoot);
            AssessmentUploadQueueIndex recovered = null;
            bool recoverThrew = false;
            try { recovered = recoveringStore.LoadIndex(); }
            catch (Exception) { recoverThrew = true; }
            Check(failures, report, "A corrupted queue index degrades to an empty queue instead of throwing", !recoverThrew && recovered != null);
            File.WriteAllText(indexPath, backup); // restore for the next check

            // --- 15. Restart recovery: a Pending entry left over from a "previous run" gets resumed ---
            string sessionK = BuildFakeSessionLog(tempRoot, "session-k");
            var freshStore = new AssessmentUploadQueueStore(tempRoot);
            string payloadPath = freshStore.WritePayload("session-k", JsonUtility.ToJson(new AssessmentSessionUploadDto { sessionId = "session-k", payloadVersion = 1 }));
            // pairedStudentDisplayName/pairedClassDisplayName must match the shared
            // "Test Harness Student"/"Test Harness Class" identity set up earlier in this
            // test (pairingGoMain) - this entry is built by hand rather than through
            // QueueSessionForUpload (simulating an index row left over from a previous
            // launch), so it must carry the same identity fields QueueSessionForUpload
            // would have captured, or AttemptUploadAsync's restart-authentication check
            // (IdentityMatchesCurrentPairing) correctly - and unhelpfully, for THIS
            // specific test's purpose - blocks it as AwaitingPairing instead of resuming it.
            freshStore.TryAddEntry(new AssessmentUploadQueueEntry {
                sessionId = "session-k", payloadVersion = 1, localPayloadPath = payloadPath,
                status = AssessmentUploadStatus.Pending, createdAtUtc = DateTime.UtcNow.ToString("O"),
                pairedStudentDisplayName = "Test Harness Student", pairedClassDisplayName = "Test Harness Class"
            });

            var restartGo = new GameObject("TempAssessmentUploadServiceRestart");
            restartGo.SetActive(false);
            var restartService = restartGo.AddComponent<AssessmentUploadService>();
            SetPrivateField(restartService, "configuration", config);
            SetPrivateField(restartService, "scenario", scenario);

            // Overrides BEFORE activation: Awake() now keeps store/transport set this
            // way (it only fills in defaults that are still null), so the startup work
            // in OnEnable - interrupted-upload recovery, retention cleanup and the
            // automatic resume - runs against THIS temp queue and never the real
            // persistentDataPath one. (Previously Awake overwrote them, which forced
            // the opposite order and let OnEnable see the real queue.)
            restartService.SetQueueRootForTesting(tempRoot);
            var restartTransport = new MockAssessmentUploadTransport();
            restartTransport.EnqueueResult("session-k", AssessmentUploadAttemptResult.Ok(new AssessmentSubmissionResponseDto { success = true, data = new AssessmentSubmissionDataDto { sessionId = "session-k", receiptId = "OK-K" } }, 200));
            restartService.SetTransportForTesting(restartTransport);
            restartGo.SetActive(true);   // OnEnable's own resume is the "next launch" being tested
            restartService.ResumePendingUploads();

            yield return WaitFor(() => FindEntry(restartService, "session-k")?.status == AssessmentUploadStatus.Uploaded, 3f);
            Check(failures, report, "A Pending entry left over from a previous run is resumed and uploaded on the next launch", FindEntry(restartService, "session-k")?.status == AssessmentUploadStatus.Uploaded);
            UnityEngine.Object.Destroy(restartGo);

            // --- 16. ClearCompletedEntries only removes Uploaded rows, never the payload of a still-pending one ---
            int clearedCount = freshStore.ClearCompletedEntries();
            var remainingAfterClear = freshStore.LoadIndex().entries;
            bool sessionKRemoved = remainingAfterClear.Find(e => e.sessionId == "session-k") == null;
            Check(failures, report, $"ClearCompletedEntries removes the now-Uploaded session-k entry (cleared={clearedCount})", sessionKRemoved);

            // --- 17. Restart-authentication (Option B): a queued session requires the SAME
            // student to re-pair before it uploads, and a DIFFERENT student pairing must
            // never upload someone else's queued session. See AssessmentUploadStatus
            // .AwaitingPairing's own class comment for the full design this exercises. ---
            var previousPairingIdentityInstance = PairingIdentityService.Instance;
            RestoreStaticInstance(typeof(PairingIdentityService), null);
            GameObject pairingGo = null;
            GameObject restartAuthServiceGo = null;
            try
            {
                pairingGo = new GameObject("TempPairingIdentityService");
                var pairingIdentity = pairingGo.AddComponent<PairingIdentityService>();
                pairingIdentity.SetIdentityForTesting("Student A", "Class A", "token-a", DateTime.UtcNow.AddHours(4));

                var restartAuthRoot = Path.Combine(tempRoot, "restart_auth");
                Directory.CreateDirectory(restartAuthRoot);

                restartAuthServiceGo = new GameObject("TempAssessmentUploadServiceRestartAuth");
                restartAuthServiceGo.SetActive(false);
                var restartAuthService = restartAuthServiceGo.AddComponent<AssessmentUploadService>();
                SetPrivateField(restartAuthService, "configuration", config);
                SetPrivateField(restartAuthService, "scenario", scenario);
                restartAuthService.SetQueueRootForTesting(restartAuthRoot);
                var restartAuthTransport = new MockAssessmentUploadTransport();
                restartAuthService.SetTransportForTesting(restartAuthTransport);
                restartAuthServiceGo.SetActive(true);

                bool originalAutoUpload = config.automaticUploadEnabled;
                config.automaticUploadEnabled = false; // queue only for this section - don't auto-attempt yet

                // --- Complete a session, queue the upload (while paired as Student A) ---
                string sessionRestartA = BuildFakeSessionLog(restartAuthRoot, "session-restart-a");
                restartAuthService.QueueSessionForUpload(sessionRestartA, "", "");
                var queuedEntryA = FindEntry(restartAuthService, "session-restart-a");
                Check(failures, report, "Session queued while paired as Student A captures that identity",
                    queuedEntryA?.pairedStudentDisplayName == "Student A" && queuedEntryA?.status == AssessmentUploadStatus.Pending);

                // --- "Close and restart Unity": identity is never persisted to disk (see
                // PairingIdentityService's own class comment), so a fresh launch starts
                // unpaired - simulated here by clearing identity and re-enabling automatic
                // upload, exactly as a real relaunch would. ---
                pairingIdentity.ClearIdentity();
                config.automaticUploadEnabled = originalAutoUpload;
                restartAuthService.ResumePendingUploads();
                yield return WaitFor(() => FindEntry(restartAuthService, "session-restart-a")?.status == AssessmentUploadStatus.AwaitingPairing, 2f);
                var afterUnpairedAttempt = FindEntry(restartAuthService, "session-restart-a");
                Check(failures, report, "Attempting upload without pairing is blocked safely (AwaitingPairing)",
                    afterUnpairedAttempt?.status == AssessmentUploadStatus.AwaitingPairing);
                Check(failures, report, "The blocked attempt never reached the transport (no network call)",
                    restartAuthTransport.Calls.TrueForAll(c => c.sessionId != "session-restart-a"));

                // --- Pair as the SAME student -> upload resumes automatically via HandlePaired ---
                restartAuthTransport.EnqueueResult("session-restart-a", AssessmentUploadAttemptResult.Ok(new AssessmentSubmissionResponseDto { success = true, data = new AssessmentSubmissionDataDto { sessionId = "session-restart-a", receiptId = "OK-RESTART-A" } }, 200));
                pairingIdentity.SetIdentityForTesting("Student A", "Class A", "token-a-2", DateTime.UtcNow.AddHours(4));
                yield return WaitFor(() => FindEntry(restartAuthService, "session-restart-a")?.status == AssessmentUploadStatus.Uploaded, 3f);
                Check(failures, report, "Pairing as the SAME student resumes and completes the upload",
                    FindEntry(restartAuthService, "session-restart-a")?.status == AssessmentUploadStatus.Uploaded);

                // --- A second session, queued under Student A, must NOT upload if a
                // DIFFERENT student pairs after a restart. ---
                config.automaticUploadEnabled = false;
                string sessionRestartB = BuildFakeSessionLog(restartAuthRoot, "session-restart-b");
                restartAuthService.QueueSessionForUpload(sessionRestartB, "", "");
                var queuedEntryB = FindEntry(restartAuthService, "session-restart-b");
                Check(failures, report, "Second session queued while (still) paired as Student A captures that identity",
                    queuedEntryB?.pairedStudentDisplayName == "Student A");

                pairingIdentity.ClearIdentity(); // restart
                config.automaticUploadEnabled = originalAutoUpload;
                pairingIdentity.SetIdentityForTesting("Student B", "Class A", "token-b", DateTime.UtcNow.AddHours(4)); // a DIFFERENT student pairs
                yield return WaitFor(() => FindEntry(restartAuthService, "session-restart-b")?.status != AssessmentUploadStatus.Pending, 2f);
                var afterWrongStudentPaired = FindEntry(restartAuthService, "session-restart-b");
                Check(failures, report, "A DIFFERENT student pairing does NOT upload the earlier student's queued session",
                    afterWrongStudentPaired?.status == AssessmentUploadStatus.AwaitingPairing);
                Check(failures, report, "Student A's queued session was never sent under Student B's token",
                    restartAuthTransport.Calls.TrueForAll(c => c.sessionId != "session-restart-b"));
            }
            finally
            {
                if (restartAuthServiceGo != null) UnityEngine.Object.Destroy(restartAuthServiceGo);
                if (pairingGo != null) UnityEngine.Object.Destroy(pairingGo);
                RestoreStaticInstance(typeof(PairingIdentityService), previousPairingIdentityInstance);
            }
        }
        finally
        {
            if (serviceGo != null) UnityEngine.Object.Destroy(serviceGo);
            if (config != null) UnityEngine.Object.DestroyImmediate(config);
            if (pairingGoMain != null) UnityEngine.Object.Destroy(pairingGoMain);

            RestoreStaticInstance(typeof(SessionLogger), previousSessionLoggerInstance);
            RestoreStaticInstance(typeof(PairingIdentityService), previousPairingIdentityInstanceMain);

            try { Directory.Delete(tempRoot, recursive: true); }
            catch (IOException) { /* best-effort cleanup */ }
        }

        report.AppendLine(failures.Count == 0 ? "=== PASS, 0 failures ===" : $"=== FAIL: {failures.Count} failure(s) ===");
        Report = report.ToString();
        IsDone = true;
    }

    /// <summary>Writes a small, real JSONL session log directly (bypassing SessionLogger entirely) - fast, isolated, and exercises SessionLogReader/AssessmentSessionBuilder against real file content rather than an in-memory shortcut.</summary>
    private static string BuildFakeSessionLog(string directory, string sessionId, bool procedural = false)
    {
        string path = Path.Combine(directory, sessionId + ".jsonl");
        var lines = new List<string>
        {
            JsonUtility.ToJson(new SessionEvent { sessionId = sessionId, sequenceNumber = 0, timestampMs = 0, eventType = "SessionStarted", targetId = "CSI_Environment", payload = new List<PayloadEntry> { new() { key = "startedAtUtc", value = DateTime.UtcNow.ToString("O") } } })
        };
        if (procedural)
            foreach (var type in new[] { SessionEventType.FirstResponderInteractionStarted, SessionEventType.CSIForm1Received,
                SessionEventType.SceneTurnoverCompleted, SessionEventType.IOCAssessmentStarted, SessionEventType.IOCObservationRecorded,
                SessionEventType.IOCAssessmentCompleted, SessionEventType.SOCOAssistanceDecision, SessionEventType.SOCORequested,
                SessionEventType.CSIForm2Completed, SessionEventType.SOCOTurnoverStarted, SessionEventType.SOCOTurnoverCompleted,
                SessionEventType.SOCOInvestigationStarted })
                lines.Add(JsonUtility.ToJson(new SessionEvent { sessionId = sessionId, sequenceNumber = lines.Count,
                    timestampMs = lines.Count * 10, eventType = type.ToString(), targetId = "procedure-fixture", payload = new List<PayloadEntry>() }));
        lines.Add(JsonUtility.ToJson(new SessionEvent { sessionId = sessionId, sequenceNumber = lines.Count, timestampMs = 500,
            eventType = "SessionEnded", payload = new List<PayloadEntry> { new() { key = "endedAtUtc", value = DateTime.UtcNow.ToString("O") } } }));
        File.WriteAllText(path, string.Join("\n", lines) + "\n");
        return path;
    }

    private static AssessmentUploadQueueEntry FindEntry(AssessmentUploadService service, string sessionId) =>
        service.GetAllEntries().Find(e => e.sessionId == sessionId);

    private static IEnumerator WaitFor(Func<bool> condition, float timeoutSeconds)
    {
        float elapsed = 0f;
        while (!condition() && elapsed < timeoutSeconds)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        field.SetValue(target, value);
    }

    /// <summary>Force-sets a type's static "Instance { get; private set; }" property - see this file's own comment on why SessionLogger.Instance must be neutralized for the test's duration.</summary>
    private static void RestoreStaticInstance(Type type, object value)
    {
        var property = type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
        property?.SetValue(null, value);
    }

    private static void Check(List<string> failures, StringBuilder report, string label, bool condition)
    {
        if (condition) report.AppendLine($"  PASS: {label}");
        else { failures.Add(label); report.AppendLine($"  FAIL: {label}"); }
    }
}
