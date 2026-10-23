// Assets/_Project/Scripts/Testing/OfflineQueueRetentionTest.cs
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Phase 4 checks: the local session lifecycle - offline persistence across a restart,
/// confirmed-only Uploaded, NO_ASSIGNMENT as a recoverable state, owner isolation, John
/// Doe, interrupted-upload recovery, 30-day retention of server-confirmed uploads (and
/// nothing else), its clock guards, SessionOwnershipStore pruning, the Phase 5 queue view,
/// resume-on-reachable, and isolation from the student directory. Fake server, isolated
/// temp folders; never the real queue, owner file, directory or network.
///
/// Run in Play mode:
///     var t = new GameObject("OQRT").AddComponent&lt;OfflineQueueRetentionTest&gt;(); t.RunAllTests();
/// then read t.Report once t.IsDone.
/// </summary>
public class OfflineQueueRetentionTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }

    private readonly List<string> failures = new();
    private readonly StringBuilder report = new();

    private const string Juan = "11111111-1111-4111-8111-111111111111";
    private const string Maria = "22222222-2222-4222-8222-222222222222";
    private const string JohnDoe = "99999999-9999-4999-8999-999999999999";
    private const string ClassX = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa";

    public void RunAllTests()
    {
        IsDone = false;
        StartCoroutine(Run());
    }

    private void Check(string label, bool ok)
    {
        report.AppendLine((ok ? "  PASS: " : "  FAIL: ") + label);
        if (!ok) failures.Add(label);
    }

    // ===================================================================== fake server

    private class FakeServer
    {
        public bool Online = true;
        public bool UploadFailsWithServerError;
        public readonly HashSet<string> StudentsWithoutAssignment = new();
        public DateTime Now = DateTime.UtcNow;
        public readonly Dictionary<string, (string student, string session)> Tokens = new();
        public readonly Dictionary<string, (string student, DateTime receivedAt)> Sessions = new();
        public readonly List<VrUploadTokenRequestDto> TokenRequests = new();
        public int Uploads;
    }

    private class FakeAuth : IVrUploadAuthorizationClient
    {
        private readonly FakeServer s;
        public FakeAuth(FakeServer server) => s = server;
        public Task<VrUploadAuthorizationResult> RequestAsync(string url, string deviceKey, VrUploadTokenRequestDto r, float timeout, CancellationToken ct)
        {
            s.TokenRequests.Add(r);
            if (!s.Online) return Task.FromResult(new VrUploadAuthorizationResult { Failure = VrUploadAuthorizationFailure.NoNetwork });
            if (s.StudentsWithoutAssignment.Contains(r.studentId))
                return Task.FromResult(new VrUploadAuthorizationResult { Failure = VrUploadAuthorizationFailure.RetryLater, ServerErrorCode = "NO_ASSIGNMENT", HttpStatusCode = 409 });
            string token = "tok-" + Guid.NewGuid().ToString("N");
            s.Tokens[token] = (r.studentId, r.sessionId);
            return Task.FromResult(new VrUploadAuthorizationResult { UploadToken = token, ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10) });
        }
    }

    private class FakeTransport : IAssessmentUploadTransport
    {
        private readonly FakeServer s;
        public FakeTransport(FakeServer server) => s = server;
        public Task<AssessmentUploadAttemptResult> UploadAsync(string url, string payload, string token, float timeout, CancellationToken ct)
        {
            s.Uploads++;
            if (!s.Online) return Task.FromResult(AssessmentUploadAttemptResult.Fail(AssessmentUploadErrorCategory.NoNetwork, 0, "offline"));
            if (s.UploadFailsWithServerError) return Task.FromResult(AssessmentUploadAttemptResult.Fail(AssessmentUploadErrorCategory.ServerError, 503, "down"));
            if (token == null || !s.Tokens.TryGetValue(token, out var t)) return Task.FromResult(AssessmentUploadAttemptResult.Fail(AssessmentUploadErrorCategory.AuthenticationFailed, 401, "TOKEN_INVALID"));
            var dto = JsonUtility.FromJson<AssessmentSessionUploadDto>(payload);
            bool duplicate = s.Sessions.TryGetValue(dto.sessionId, out var existing);
            if (!duplicate) s.Sessions[dto.sessionId] = (t.student, s.Now);
            var receivedAt = duplicate ? existing.receivedAt : s.Now;
            return Task.FromResult(AssessmentUploadAttemptResult.Ok(new AssessmentSubmissionResponseDto
            {
                success = true,
                data = new AssessmentSubmissionDataDto { receiptId = Guid.NewGuid().ToString(), sessionId = dto.sessionId, duplicate = duplicate, uploadStatus = "accepted", serverReceivedAtUtc = receivedAt.ToString("O") },
            }, 200));
        }
    }

    private class FakeDirectoryTransport : IStudentDirectoryTransport
    {
        public string Body;
        public Task<StudentDirectoryFetchResult> FetchAsync(string url, string key, float timeout, CancellationToken ct) =>
            Task.FromResult(StudentDirectoryFetchResult.Ok(200, Body));
    }

    // ===================================================================== helpers

    private string root;
    private FakeServer server;
    private SessionOwnershipStore owners;
    private AssessmentUploadConfiguration config;
    private ScenarioDefinition scenario;

    private AssessmentUploadService NewService(string queueRoot, bool automatic = true)
    {
        config.automaticUploadEnabled = automatic;
        var go = new GameObject("Upload service (Phase 4 test)");
        go.SetActive(false);
        var service = go.AddComponent<AssessmentUploadService>();
        typeof(AssessmentUploadService).GetField("configuration", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(service, config);
        typeof(AssessmentUploadService).GetField("scenario", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(service, scenario);
        // Seams BEFORE OnEnable so its startup recovery/cleanup/resume act on the test folders.
        service.SetQueueRootForTesting(queueRoot);
        service.SetTransportForTesting(new FakeTransport(server));
        service.SetVrOwnershipSeamsForTesting(new FakeAuth(server), () => "device-key", owners);
        go.SetActive(true);
        // Hard guard: never let this test run against the real queue.
        var store = (AssessmentUploadQueueStore)typeof(AssessmentUploadService).GetField("store", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(service);
        if (!Path.GetFullPath(store.PayloadDirectory).StartsWith(Path.GetFullPath(queueRoot)))
        {
            DestroyImmediate(go);
            throw new InvalidOperationException("Test upload service is not using the test queue folder - aborting before touching real data.");
        }
        return service;
    }

    private string Log(string sessionId)
    {
        string path = Path.Combine(root, sessionId + ".jsonl");
        File.WriteAllText(path,
            JsonUtility.ToJson(new SessionEvent { sessionId = sessionId, sequenceNumber = 0, eventType = "SessionStarted", payload = new List<PayloadEntry> { new() { key = "startedAtUtc", value = DateTime.UtcNow.ToString("O") } } }) + "\n" +
            JsonUtility.ToJson(new SessionEvent { sessionId = sessionId, sequenceNumber = 1, timestampMs = 500, eventType = "SessionEnded", payload = new List<PayloadEntry> { new() { key = "endedAtUtc", value = DateTime.UtcNow.ToString("O") } } }) + "\n");
        return path;
    }

    private string Bind(string studentId, string name, bool test = false)
    {
        string sid = Guid.NewGuid().ToString();
        owners.TryBind(sid, new IdentifiedStudent { StudentId = studentId, DisplayName = name, IsTestSession = test, Classes = new List<CachedStudentClass> { new() { classId = ClassX, className = "CRIM 101", classSection = "A" } } }, DateTime.UtcNow, out _);
        return sid;
    }

    private static AssessmentUploadQueueEntry E(AssessmentUploadService s, string sid) => s.GetAllEntries().Find(e => e.sessionId == sid);

    private static IEnumerator WaitFor(Func<bool> c, float limit = 5f)
    {
        float t = 0f;
        while (!c() && t < limit) { t += Time.unscaledDeltaTime; yield return null; }
    }

    private static void SetStatic(Type type, object value) => type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.SetValue(null, value);

    // ===================================================================== tests

    private IEnumerator Run()
    {
        report.AppendLine("=== OfflineQueueRetentionTest ===");
        root = Path.Combine(Application.temporaryCachePath, "queue_retention_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        var prevLogger = SessionLogger.Instance;
        var prevPairing = PairingIdentityService.Instance;
        var prevUpload = AssessmentUploadService.Instance;
        var prevDirectory = StudentDirectoryService.Instance;
        SetStatic(typeof(SessionLogger), null);
        SetStatic(typeof(PairingIdentityService), null);
        // The scene's real uploader listens to directory syncs too; pause it so this test's
        // fake sync can't make it resume the REAL queue against the real server.
        bool realUploaderWasEnabled = prevUpload != null && prevUpload.enabled;
        if (realUploaderWasEnabled) prevUpload.enabled = false;
        var spawned = new List<GameObject>();

        try
        {
            server = new FakeServer();
            owners = new SessionOwnershipStore(root);
            config = ScriptableObject.CreateInstance<AssessmentUploadConfiguration>();
            config.apiBaseUrl = "https://queue-test.invalid";
            config.requestTimeoutSeconds = 5f;
            config.maxRetryAttempts = 3;
            config.initialRetryDelaySeconds = 60f;   // no automatic retries during the test
            config.maxRetryDelaySeconds = 60f;
            config.uploadRawEventLogEnabled = true;
            config.maxEventsPerRequest = 5000;
            scenario = ScriptableObject.CreateInstance<ScenarioDefinition>();
            scenario.scenarioId = "CSI-ENVIRONMENT-001";
            scenario.scenarioVersion = "1.0.0";
            scenario.scoringRulesVersion = "1.0.0";

            string queueRoot = Path.Combine(root, "queue");
            Directory.CreateDirectory(queueRoot);
            var service = NewService(queueRoot);
            spawned.Add(service.gameObject);

            // ------------------------------------------------ persistence across restart
            server.Online = false;
            string sJuan = Bind(Juan, "Juan Dela Cruz");
            service.QueueSessionForUpload(Log(sJuan), null, null);
            yield return WaitFor(() => E(service, sJuan)?.status == AssessmentUploadStatus.RetryWaiting);
            string payloadJuan = E(service, sJuan)?.localPayloadPath;
            DestroyImmediate(service.gameObject);
            service = NewService(queueRoot, automatic: false);   // "restart"; automatic off so it just reloads
            spawned.Add(service.gameObject);
            var reloaded = E(service, sJuan);
            Check("[persistence] offline session survives a restart: same id, same owner, payload on disk, not uploaded",
                reloaded != null && reloaded.status == AssessmentUploadStatus.RetryWaiting && reloaded.ownerStudentId == Juan
                && File.Exists(payloadJuan) && owners.Find(sJuan)?.studentId == Juan && !server.Sessions.ContainsKey(sJuan)
                && !string.IsNullOrEmpty(reloaded.sessionCompletedAtUtc));

            // ------------------------------------------------ Juan -> Maria, then later success
            string sMaria = Bind(Maria, "Maria Santos");   // Maria is now the identified/current student
            server.Online = true;
            server.UploadFailsWithServerError = true;
            config.automaticUploadEnabled = true;
            service.RetryNow(sJuan);
            yield return WaitFor(() => E(service, sJuan)?.lastErrorCategory == AssessmentUploadErrorCategory.ServerError);
            Check("[confirmed-only] a token issued + request sent but no success response is NOT Uploaded",
                E(service, sJuan)?.status == AssessmentUploadStatus.RetryWaiting && string.IsNullOrEmpty(E(service, sJuan)?.uploadConfirmedAtUtc));
            server.UploadFailsWithServerError = false;
            server.Now = DateTime.UtcNow.AddMinutes(-3);
            service.RetryNow(sJuan);
            yield return WaitFor(() => E(service, sJuan)?.status == AssessmentUploadStatus.Uploaded);
            var up = E(service, sJuan);
            Check("[later upload] retried after Maria became current: same sessionId, stored under JUAN, Uploaded only after confirmation",
                up?.status == AssessmentUploadStatus.Uploaded && server.Sessions[sJuan].student == Juan
                && server.TokenRequests.FindLast(r => r.sessionId == sJuan).studentId == Juan && owners.Find(sJuan).studentId == Juan
                && owners.Find(sMaria).studentId == Maria);
            Check("[timestamp] retention starts from the SERVER's receipt time; expiry = +30 days",
                AssessmentUploadQueueStore.TryParseUtc(up?.uploadConfirmedAtUtc, out var confirmedAt) && Math.Abs((confirmedAt - server.Now).TotalSeconds) < 1
                && AssessmentUploadQueueStore.TryParseUtc(up.localExpiresAtUtc, out var expires) && Math.Abs((expires - server.Now.AddDays(30)).TotalSeconds) < 1
                && !string.IsNullOrEmpty(up.uploadConfirmedLocalAtUtc));
            Check("[after upload] the fresh upload is kept locally (inside retention) with its owner",
                E(service, sJuan) != null && File.Exists(up.localPayloadPath) && owners.Find(sJuan) != null);

            // ------------------------------------------------ NO_ASSIGNMENT
            server.StudentsWithoutAssignment.Add(Maria);
            service.QueueSessionForUpload(Log(sMaria), null, null);
            yield return WaitFor(() => E(service, sMaria)?.lastErrorCategory == AssessmentUploadErrorCategory.NoAssignment);
            var na = E(service, sMaria);
            Check("[NO_ASSIGNMENT] kept local, retryable, distinct reason, payload + owner preserved, not uploaded",
                na?.status == AssessmentUploadStatus.RetryWaiting && AssessmentUploadQueueView.Classify(na) == AssessmentUploadQueueView.DisplayState.NoAssignment
                && File.Exists(na.localPayloadPath) && na.ownerStudentId == Maria && owners.Find(sMaria).studentId == Maria
                && !server.Sessions.ContainsKey(sMaria) && !string.IsNullOrEmpty(na.nextAttemptAtUtc) && na.lastErrorMessage.Contains("assignment"));
            server.StudentsWithoutAssignment.Remove(Maria);   // the instructor creates the assignment
            service.RetryNow(sMaria);
            yield return WaitFor(() => E(service, sMaria)?.status == AssessmentUploadStatus.Uploaded);
            Check("[NO_ASSIGNMENT] once the assignment exists, the SAME session uploads under the SAME owner",
                E(service, sMaria)?.status == AssessmentUploadStatus.Uploaded && server.Sessions[sMaria].student == Maria);

            // ------------------------------------------------ John Doe
            server.StudentsWithoutAssignment.Add(JohnDoe);
            string sDoe = Bind(JohnDoe, "John Doe", test: true);
            service.QueueSessionForUpload(Log(sDoe), null, null);
            yield return WaitFor(() => E(service, sDoe)?.lastErrorCategory == AssessmentUploadErrorCategory.NoAssignment);
            Check("[John Doe] no assignment -> stays local as John Doe's, never rerouted to another student",
                E(service, sDoe)?.status == AssessmentUploadStatus.RetryWaiting && server.TokenRequests.FindAll(r => r.sessionId == sDoe).TrueForAll(r => r.studentId == JohnDoe && r.isTestSession));
            server.StudentsWithoutAssignment.Remove(JohnDoe);
            service.RetryNow(sDoe);
            yield return WaitFor(() => E(service, sDoe)?.status == AssessmentUploadStatus.Uploaded);
            Check("[John Doe] same queue/upload path: uploaded, owner = the existing John Doe id",
                E(service, sDoe)?.status == AssessmentUploadStatus.Uploaded && server.Sessions[sDoe].student == JohnDoe && E(service, sDoe).isTestSession);

            // ------------------------------------------------ interrupted upload
            string sCut = Bind(Juan, "Juan Dela Cruz");
            service.QueueSessionForUpload(Log(sCut), null, null);
            yield return WaitFor(() => E(service, sCut)?.status == AssessmentUploadStatus.Uploaded);
            var indexStore = new AssessmentUploadQueueStore(queueRoot);
            indexStore.UpdateEntry(sCut, e => { e.status = AssessmentUploadStatus.Uploading; e.uploadConfirmedAtUtc = null; });   // app died mid-upload; server already has it
            DestroyImmediate(service.gameObject);
            service = NewService(queueRoot, automatic: false);
            spawned.Add(service.gameObject);
            var recovered = E(service, sCut);
            Check("[interrupted] Uploading at launch -> RetryWaiting (Interrupted), same id/owner, never stuck even with auto-upload off",
                recovered?.status == AssessmentUploadStatus.RetryWaiting && recovered.lastErrorCategory == AssessmentUploadErrorCategory.Interrupted
                && recovered.ownerStudentId == Juan && File.Exists(recovered.localPayloadPath));
            config.automaticUploadEnabled = true;
            service.RetryNow(sCut);
            yield return WaitFor(() => E(service, sCut)?.status == AssessmentUploadStatus.Uploaded);
            Check("[interrupted] idempotent retry: server answers duplicate, entry Uploaded with the ORIGINAL receipt time, still one server row",
                E(service, sCut)?.status == AssessmentUploadStatus.Uploaded && server.Sessions[sCut].student == Juan
                && AssessmentUploadQueueStore.TryParseUtc(E(service, sCut).uploadConfirmedAtUtc, out var cutAt) && Math.Abs((cutAt - server.Sessions[sCut].receivedAt).TotalSeconds) < 1);

            // ------------------------------------------------ resume when the server becomes reachable
            server.Online = false;
            string sLater = Bind(Juan, "Juan Dela Cruz");
            service.QueueSessionForUpload(Log(sLater), null, null);
            yield return WaitFor(() => E(service, sLater)?.status == AssessmentUploadStatus.RetryWaiting);
            new AssessmentUploadQueueStore(queueRoot).UpdateEntry(sLater, e => e.nextAttemptAtUtc = DateTime.UtcNow.AddSeconds(-1).ToString("O"));   // its backoff is due
            server.Online = true;
            var dirGo = new GameObject("Directory (Phase 4 test)");
            dirGo.SetActive(false);
            spawned.Add(dirGo);
            var dirService = dirGo.AddComponent<StudentDirectoryService>();
            string dirRoot = Path.Combine(root, "directory");
            Directory.CreateDirectory(dirRoot);
            var dirTransport = new FakeDirectoryTransport { Body = DirectoryBody() };
            var dirConfig = ScriptableObject.CreateInstance<AssessmentUploadConfiguration>();
            dirConfig.apiBaseUrl = "https://queue-test.invalid";
            dirService.ConfigureForTesting(dirConfig, dirTransport, new StudentDirectoryStore(dirRoot), "device-key");
            dirGo.SetActive(true);
            var sync = dirService.SyncNow();
            yield return WaitFor(() => sync.IsCompleted && E(service, sLater)?.status == AssessmentUploadStatus.Uploaded);
            Check("[resume] a successful directory sync (server reachable) resumes a due pending upload - no polling",
                sync.IsCompleted && sync.Result == StudentDirectorySyncStatus.Succeeded && E(service, sLater)?.status == AssessmentUploadStatus.Uploaded);

            // ------------------------------------------------ retention rules (crafted states)
            string rRoot = Path.Combine(root, "retention");
            Directory.CreateDirectory(rRoot);
            var rq = new AssessmentUploadQueueStore(rRoot);
            var ro = new SessionOwnershipStore(rRoot);
            DateTime now = DateTime.UtcNow;
            var cases = new Dictionary<string, AssessmentUploadQueueEntry>();
            AssessmentUploadQueueEntry Make(string label, AssessmentUploadStatus status, double uploadedDaysAgo, AssessmentUploadErrorCategory cat = AssessmentUploadErrorCategory.None, bool hasServerTime = true)
            {
                string sid = Guid.NewGuid().ToString();
                ro.TryBind(sid, new IdentifiedStudent { StudentId = Juan, DisplayName = label }, now.AddDays(-61), out _);
                var e = new AssessmentUploadQueueEntry
                {
                    sessionId = sid, payloadVersion = 1, status = status, lastErrorCategory = cat, ownerStudentId = Juan,
                    localPayloadPath = rq.WritePayload(sid, "{\"sessionId\":\"" + sid + "\"}"),
                    createdAtUtc = now.AddDays(-60).ToString("O"),
                };
                if (status == AssessmentUploadStatus.Uploaded && hasServerTime)
                {
                    e.uploadConfirmedAtUtc = now.AddDays(-uploadedDaysAgo).ToString("O");
                    e.uploadConfirmedLocalAtUtc = now.AddDays(-uploadedDaysAgo).ToString("O");
                }
                rq.TryAddEntry(e);
                cases[label] = e;
                return e;
            }
            Make("uploaded 29d", AssessmentUploadStatus.Uploaded, 29);
            Make("uploaded 30d", AssessmentUploadStatus.Uploaded, 30.0001);
            Make("uploaded 45d", AssessmentUploadStatus.Uploaded, 45);
            Make("pending 60d", AssessmentUploadStatus.Pending, 0);
            Make("retry 60d", AssessmentUploadStatus.RetryWaiting, 0, AssessmentUploadErrorCategory.ServerError);
            Make("no-assignment 60d", AssessmentUploadStatus.RetryWaiting, 0, AssessmentUploadErrorCategory.NoAssignment);
            Make("uploading (unknown outcome)", AssessmentUploadStatus.Uploading, 0);
            Make("rejected (review)", AssessmentUploadStatus.Rejected, 0);
            Make("awaiting pairing", AssessmentUploadStatus.AwaitingPairing, 0);
            Make("uploaded, no server time", AssessmentUploadStatus.Uploaded, 50, hasServerTime: false);
            var busy = Make("uploaded 40d but uploading now", AssessmentUploadStatus.Uploaded, 40);
            string unrelated = Guid.NewGuid().ToString();   // e.g. the session being played right now - no queue entry
            ro.TryBind(unrelated, new IdentifiedStudent { StudentId = Maria, DisplayName = "active" }, now, out _);

            // no server time known yet -> nothing at all is removed
            var none = UploadedSessionRetention.Run(rq, ro, now, null, id => id == busy.sessionId);
            Check("[clock] with no server time known, nothing is removed", none.RemovedSessionIds.Count == 0 && none.SkippedReason != null);

            // device clock jumped 5 years ahead, last server contact 20 days ago -> trusted now is capped
            rq.RecordServerTime(now.AddDays(-20));
            var jumped = UploadedSessionRetention.Run(rq, ro, now.AddYears(5), null, id => id == busy.sessionId);
            // trusted now = (now - 20d) + 1d = now - 19d, so even the 45-day upload is only 26 days old by it
            Check("[clock] a device clock 5 years ahead can't expire anything early (trusted now capped at last server time + 1 day)",
                jumped.RemovedSessionIds.Count == 0 && AssessmentUploadQueueStore.TryParseUtc(jumped.TrustedNowUtc?.ToString("O"), out var cap)
                && Math.Abs((cap - now.AddDays(-19)).TotalMinutes) < 1);

            rq.RecordServerTime(now);
            var run = UploadedSessionRetention.Run(rq, ro, now, null, id => id == busy.sessionId);
            bool Kept(string label) => rq.LoadIndex().entries.Exists(e => e.sessionId == cases[label].sessionId) && File.Exists(cases[label].localPayloadPath);
            bool Gone(string label) => !rq.LoadIndex().entries.Exists(e => e.sessionId == cases[label].sessionId) && !File.Exists(cases[label].localPayloadPath);
            Check("[retention] uploaded 29 days ago remains", Kept("uploaded 29d"));
            Check("[retention] uploaded 30+ days ago is removed (entry + payload)", Gone("uploaded 30d") && Gone("uploaded 45d"));
            Check("[retention] Pending 60 days old remains", Kept("pending 60d"));
            Check("[retention] FailedRetryable 60 days old remains", Kept("retry 60d"));
            Check("[retention] NO_ASSIGNMENT 60 days old remains", Kept("no-assignment 60d"));
            Check("[retention] unknown outcome (Uploading) remains", Kept("uploading (unknown outcome)"));
            Check("[retention] requires-review and awaiting-pairing remain", Kept("rejected (review)") && Kept("awaiting pairing"));
            Check("[retention] Uploaded without a server receipt time is never auto-removed", Kept("uploaded, no server time"));
            Check("[retention] an expired upload that is uploading right now is skipped", Kept("uploaded 40d but uploading now"));

            bool Owned(string label) => ro.Find(cases[label].sessionId) != null;
            Check("[ownership] pending / failed / NO_ASSIGNMENT / uploading / review owners retained",
                Owned("pending 60d") && Owned("retry 60d") && Owned("no-assignment 60d") && Owned("uploading (unknown outcome)") && Owned("rejected (review)") && Owned("awaiting pairing"));
            Check("[ownership] uploaded-but-inside-retention owner retained", Owned("uploaded 29d") && Owned("uploaded, no server time") && Owned("uploaded 40d but uploading now"));
            Check("[ownership] uploaded + expired + local record removed -> owner pruned (and only those)",
                !Owned("uploaded 30d") && !Owned("uploaded 45d")
                && run.PrunedOwnerSessionIds.Count == 2);
            Check("[ownership] an unrelated session's owner (no queue entry) is untouched", ro.Find(unrelated)?.studentId == Maria);
            var again = UploadedSessionRetention.Run(rq, ro, now, null, id => id == busy.sessionId);
            Check("[retention] a second pass changes nothing (idempotent)", again.RemovedSessionIds.Count == 0 && again.PrunedOwnerSessionIds.Count == 0);

            // the headset's own clock also has to agree (server says 40 days, headset received it 2 days ago)
            var oddRoot = Path.Combine(root, "odd");
            Directory.CreateDirectory(oddRoot);
            var oq = new AssessmentUploadQueueStore(oddRoot);
            string oddSid = Guid.NewGuid().ToString();
            oq.TryAddEntry(new AssessmentUploadQueueEntry { sessionId = oddSid, status = AssessmentUploadStatus.Uploaded, localPayloadPath = oq.WritePayload(oddSid, "{}"),
                uploadConfirmedAtUtc = now.AddDays(-40).ToString("O"), uploadConfirmedLocalAtUtc = now.AddDays(-2).ToString("O") });
            oq.RecordServerTime(now);
            UploadedSessionRetention.Run(oq, null, now, null, null);
            Check("[clock] if the headset itself received the confirmation < 30 days ago, the copy is kept", oq.LoadIndex().entries.Exists(e => e.sessionId == oddSid));

            // ------------------------------------------------ Phase 5 view
            var summary = AssessmentUploadQueueView.Summarize(rq.LoadIndex().entries);
            var u29 = rq.LoadIndex().entries.Find(e => e.sessionId == cases["uploaded 29d"].sessionId);
            // Phase 5: an Uploaded entry without a server receipt time is its own LegacyUploaded bucket.
            Check($"[view] counts: uploaded {summary.Uploaded}, legacy {summary.LegacyUploaded}, retry {summary.RetryWaiting}, no-assignment {summary.NoAssignment}, review {summary.RequiresReview}, pending {summary.Pending}, uploading {summary.Uploading}, pairing {summary.AwaitingPairing}",
                summary.Uploaded == 2 && summary.LegacyUploaded == 1 && summary.RetryWaiting == 1 && summary.NoAssignment == 1 && summary.RequiresReview == 1 && summary.Pending == 1 && summary.Uploading == 1 && summary.AwaitingPairing == 1);
            Check("[view] 'local copy expires in X days': 29 days old -> 1 day; not-uploaded -> none",
                AssessmentUploadQueueView.DaysUntilLocalExpiry(u29, now) == 1 && AssessmentUploadQueueView.DaysUntilLocalExpiry(cases["pending 60d"], now) == null);

            // ------------------------------------------------ directory isolation
            int countBefore = dirService.Cache.ActiveStudentCount;
            string dirFileBefore = File.ReadAllText(Path.Combine(dirRoot, StudentDirectoryStore.FileName));
            var events = new List<StudentDirectorySyncStatus>();
            Action<StudentDirectorySyncStatus> listener = s => events.Add(s);
            StudentDirectoryService.OnSyncFinished += listener;
            // make Juan's real upload expire and clean it through the SERVICE path
            new AssessmentUploadQueueStore(queueRoot).UpdateEntry(sJuan, e => { e.uploadConfirmedAtUtc = DateTime.UtcNow.AddDays(-31).ToString("O"); e.uploadConfirmedLocalAtUtc = e.uploadConfirmedAtUtc; });
            var serviceRun = service.RunRetentionCleanup();
            StudentDirectoryService.OnSyncFinished -= listener;
            Check("[directory isolation] retention removed the expired upload via the service...",
                serviceRun.RemovedSessionIds.Contains(sJuan) && E(service, sJuan) == null && owners.Find(sJuan) == null);
            Check("[directory isolation] ...without touching the student directory: same active count, identical cache file, no Cleared event",
                dirService.Cache.ActiveStudentCount == countBefore && countBefore > 0
                && File.ReadAllText(Path.Combine(dirRoot, StudentDirectoryStore.FileName)) == dirFileBefore && !events.Contains(StudentDirectorySyncStatus.Cleared));
            Check("[directory isolation] other students' sessions untouched by that cleanup",
                E(service, sMaria)?.status == AssessmentUploadStatus.Uploaded && owners.Find(sMaria) != null && E(service, sDoe) != null);
        }
        finally
        {
            foreach (var go in spawned) if (go != null) DestroyImmediate(go);
            SetStatic(typeof(AssessmentUploadService), prevUpload);
            if (realUploaderWasEnabled && prevUpload != null) prevUpload.enabled = true;
            SetStatic(typeof(StudentDirectoryService), prevDirectory);
            SetStatic(typeof(SessionLogger), prevLogger);
            SetStatic(typeof(PairingIdentityService), prevPairing);
        }

        report.AppendLine(failures.Count == 0 ? "=== PASS, 0 failures ===" : $"=== FAIL: {failures.Count} failure(s) ===");
        Report = report.ToString();
        IsDone = true;
    }

    private static string DirectoryBody()
    {
        string salt = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        var data = new StudentDirectoryDataDto
        {
            generatedAtUtc = DateTime.UtcNow.ToString("o"),
            codeLookup = new StudentDirectoryCodeLookupDto { algorithm = "PBKDF2-HMAC-SHA256", iterations = 2000, keyBytes = 32, saltBase64 = salt, input = "canonical-code-utf8", output = "hex" },
            students = new List<StudentDirectoryStudentDto>
            {
                new() { studentId = Juan, displayName = "Juan Dela Cruz", codeVersion = 1, enabled = true, codeHash = StudentAccessCodes.DeriveLookupHash("K7M4PQ2D", salt, 2000, 32), updatedAtUtc = DateTime.UtcNow.ToString("o"), classes = new List<StudentDirectoryClassDto>() },
            },
            testStudent = new StudentDirectoryTestStudentDto(),
        };
        return JsonUtility.ToJson(new StudentDirectoryResponseDto { success = true, data = data });
    }
}
