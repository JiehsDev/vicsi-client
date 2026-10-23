// Assets/_Project/Scripts/Assessment/AssessmentUploadService.cs
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Runtime owner of the local pending-upload queue and its retry loop - the "separate
/// upload layer" this feature is required to be, sitting entirely alongside
/// SessionLogger/LocalJsonLogWriter rather than replacing or being called by them. Never
/// touches EvidenceStateManager, RelationshipStateManager, ObjectiveStateManager, or
/// SessionLogger's own persistence; it only reads their already-public state once, at
/// session-completion time, via AssessmentSessionBuilder.
///
/// Never logs the bearer token or a full payload body via debugLoggingEnabled - only
/// event names, session ids, status transitions, and error categories. A modified/
/// rooted device is a real threat model for a Quest shared lab headset; verbose network
/// logs are exactly the kind of thing that leaks a token into a log a curious student
/// could read.
///
/// BEFORE BUILDING FOR META QUEST 3S: the `configuration` field below (and
/// PairingIdentityService's own, separate `configuration` field, on the same
/// _Managers GameObject) must both be assigned an HTTPS-reachable, non-localhost
/// AssessmentUploadConfiguration asset - never AssessmentUploadConfig_Development
/// (http://localhost, allowLocalhost=true - refused at runtime off-device anyway).
/// Use AssessmentUploadConfig_QuestDev for headset testing against a dev deployment
/// once its placeholder apiBaseUrl has been replaced with a real HTTPS URL, or
/// AssessmentUploadConfig_Production for a release build. Both fields fail closed
/// (see AssessmentUploadConfiguration.IsValidForRuntimeUse) rather than silently
/// uploading to the wrong host, but a build that never gets past pairing is still a
/// wasted APK push to the headset - verify the right asset is assigned first.
/// This project holds no Supabase service-role key anywhere in Unity code or assets;
/// PairingIdentityService only ever holds the short-lived, per-session, server-issued
/// assessment token in memory - never a standing credential.
///
/// TWO UPLOAD PATHS, chosen per queue entry:
///   - VR-code ownership (entry.ownerStudentId set - the normal classroom path since
///     Phase 3): the session's owner was bound at session start (SessionOwnership).
///     Each attempt requests a fresh 10-minute token for THAT owner and THAT session
///     from /api/v1/vr/session-upload-token, authenticated by the headset's device key,
///     uses it once, and drops it. Works whoever is identified later; never waits for
///     pairing; retryable failures park at RetryWaiting instead of FailedPermanently.
///   - Legacy pairing (no owner record): unchanged - the paired token and the
///     AwaitingPairing identity match described above.
/// </summary>
public class AssessmentUploadService : MonoBehaviour
{
    public static AssessmentUploadService Instance { get; private set; }

    [SerializeField] private AssessmentUploadConfiguration configuration;
    [SerializeField] private ScenarioDefinition scenario;
    public string ScoringRulesVersion => scenario != null ? scenario.scoringRulesVersion : null;

    private AssessmentUploadQueueStore store;
    private IAssessmentUploadTransport transport;

    // VR-code ownership bridge (Phase 3) - see AttemptVrOwnedUploadAsync.
    private IVrUploadAuthorizationClient authorizationClient;
    private Func<string> deviceKeyProvider;
    private SessionOwnershipStore ownershipStoreOverride;
    private SessionOwnershipStore OwnershipStore => ownershipStoreOverride ?? SessionOwnership.Store;
    private Func<DateTime> retentionClock;   // test seam; null = DateTime.UtcNow

    /// <summary>Test seam - the device clock retention cleanup reads.</summary>
    public void SetRetentionClockForTesting(Func<DateTime> deviceUtcNow) => retentionClock = deviceUtcNow;
    private readonly HashSet<string> currentlyUploading = new();
    private CancellationTokenSource lifetimeCts;

    /// <summary>Fired on every queue entry status transition - the completion screen's status UI (Phase 9) subscribes to this instead of polling.</summary>
    public static event Action<AssessmentUploadQueueEntry> OnQueueEntryChanged;

    public AssessmentUploadConfiguration Configuration => configuration;

    private void Awake()
    {
        Instance = this;
        bool usesDefaultQueue = store == null;
        // ??= so test/tooling seams set before activation (SetQueueRootForTesting etc.)
        // survive Awake - otherwise OnEnable's startup recovery/cleanup/resume would run
        // against the REAL queue. In normal play these are all null here.
        store ??= new AssessmentUploadQueueStore();
        transport ??= new UnityWebRequestUploadTransport();
        authorizationClient ??= new UnityWebRequestVrUploadAuthorizationClient();
        deviceKeyProvider ??= () => StudentDirectoryService.Instance != null ? StudentDirectoryService.Instance.DeviceKey : VrDeviceKey.Resolve();
        lifetimeCts = new CancellationTokenSource();
        // Isolate manual fixtures before OnEnable resumes historical uploads/retention.
        if (SceneTestStart.IsActive || (AutomatedTestSandbox.IsActive && usesDefaultQueue))
            AutomatedTestSandbox.Activate(nameof(SceneTestStart));
    }

    private void OnEnable()
    {
        // "Restore pending items after restarting Unity or the headset" - on every
        // enable (covers both a fresh app launch and a domain reload in the Editor),
        // resume anything left Pending/RetryWaiting/Uploading/AwaitingPairing from a
        // previous run. Uploading is included deliberately: a crash mid-upload leaves
        // an entry stuck at Uploading forever otherwise, since nothing would ever
        // revisit it. AwaitingPairing entries are re-checked too, in case the device
        // happens to already be paired as the right student by the time this runs.
        // Phase 4: an entry left Uploading by a closed app is first moved to RetryWaiting
        // (Interrupted) so it is never stuck - even with automatic upload off - and the
        // 30-day cleanup of confirmed uploads runs before any new work starts.
        RecoverInterruptedUploads();
        RunRetentionCleanup();
        ResumePendingUploads();

        // Restart-authentication design (Option B - see AssessmentUploadStatus.AwaitingPairing's
        // own comment): a fresh pairing is the ONLY thing that can move an
        // AwaitingPairing entry forward, so re-scan every time one completes -
        // this is what makes "pair as the same student -> upload resumes" actually
        // happen without the player needing to manually retry.
        PairingIdentityService.OnPaired += HandlePaired;

        // A successful directory sync proves the server is reachable again: resume the
        // entries whose backoff is due (no polling of our own).
        StudentDirectoryService.OnSyncFinished += HandleDirectorySyncFinished;
    }

    private void OnDisable()
    {
        PairingIdentityService.OnPaired -= HandlePaired;
        StudentDirectoryService.OnSyncFinished -= HandleDirectorySyncFinished;
    }

    private void HandleDirectorySyncFinished(StudentDirectorySyncStatus status)
    {
        if (status != StudentDirectorySyncStatus.Succeeded) return;
        LogDebug("Server reachable (directory sync succeeded) - resuming due uploads.");
        RunRetentionCleanup();
        ResumeDueUploads();
    }

    /// <summary>Moves entries left Uploading by a closed app back to RetryWaiting (never one in flight in this process).</summary>
    public int RecoverInterruptedUploads()
    {
        int recovered = store.RecoverInterruptedUploads(IsUploadingNow);
        if (recovered > 0)
        {
            LogDebug($"Recovered {recovered} interrupted upload(s) to RetryWaiting.");
        }
        return recovered;
    }

    /// <summary>
    /// One 30-day retention pass over SERVER-CONFIRMED uploads (see UploadedSessionRetention
    /// for every rule and the clock policy). Removes expired local copies and their owner
    /// records; never touches anything not yet uploaded, the student directory, or the server.
    /// </summary>
    public UploadedSessionRetention.Result RunRetentionCleanup()
    {
        var result = UploadedSessionRetention.Run(store, OwnersOrNull(), DeviceNow, DirectoryServerTime(), IsUploadingNow, RawLogDirectory);
        foreach (var removed in result.RemovedSessionIds)
        {
            LogDebug($"Retention: removed local copy of uploaded session {removed} (30 days after server confirmation).");
        }
        foreach (var removed in result.RemovedSessionIds)
        {
            RaiseRemoved(removed);
        }
        return result;
    }

    /// <summary>
    /// Instructor action (Phase 5, PIN-confirmed in the UI): removes the local copies of
    /// LEGACY uploaded sessions only - Uploaded with no server receipt time. Never touches
    /// anything not provably uploaded. See UploadedSessionRetention.ClearLegacyUploaded.
    /// </summary>
    public UploadedSessionRetention.Result ClearLegacyUploadedLocalBackups()
    {
        var result = UploadedSessionRetention.ClearLegacyUploaded(store, OwnersOrNull(), RawLogDirectory, IsUploadingNow);
        foreach (var removed in result.RemovedSessionIds)
        {
            RaiseRemoved(removed);
        }
        return result;
    }

    /// <summary>The capped clock the 30-day cleanup uses right now (null = no trusted server time yet, so nothing is due).</summary>
    public DateTime? TrustedNowForRetention()
    {
        DateTime? latest = store.LastKnownServerTimeUtc;
        var dir = DirectoryServerTime();
        if (dir != null && (latest == null || dir > latest)) latest = dir;
        return UploadedSessionRetention.TrustedNow(DeviceNow, latest);
    }

    /// <summary>How many uploaded backups the cleanup would remove right now (same rule as RunRetentionCleanup).</summary>
    public int CountCleanupEligible()
    {
        var trusted = TrustedNowForRetention();
        if (trusted == null) return 0;
        DateTime now = DeviceNow;
        return store.LoadIndex().entries.FindAll(e => UploadedSessionRetention.IsEligible(e, trusted.Value, now, IsUploadingNow)).Count;
    }

    public DateTime DeviceNow => retentionClock?.Invoke() ?? DateTime.UtcNow;

    /// <summary>Raised with the sessionId when an uploaded session's local copy is removed (retention or legacy cleanup), so live views drop the row. Companion to OnQueueEntryChanged, which only reports entries that still exist.</summary>
    public static event Action<string> OnQueueEntryRemoved;

    private static void RaiseRemoved(string sessionId) => OnQueueEntryRemoved?.Invoke(sessionId);

    private string rawLogDirectoryOverride;
    /// <summary>Where LocalJsonLogWriter writes raw session logs (persistentDataPath).</summary>
    private string RawLogDirectory => rawLogDirectoryOverride ?? Application.persistentDataPath;

    /// <summary>Test seam - point raw-log cleanup at a temp folder.</summary>
    public void SetRawLogDirectoryForTesting(string directory) => rawLogDirectoryOverride = directory;

    /// <summary>
    /// Used ONLY by AutomatedTestSandbox: points this live service at an isolated queue,
    /// owner store and raw-log folder, with offline-only network fakes and no device key,
    /// so an automated test that completes a session can never write the real queue or
    /// reach a server. The same seams the unit tests already use - no second uploader.
    /// </summary>
    public void RedirectForAutomatedTests(AssessmentUploadQueueStore testQueue, IAssessmentUploadTransport offlineTransport,
        IVrUploadAuthorizationClient offlineAuthorization, SessionOwnershipStore testOwners, string testRawLogDirectory)
    {
        store = testQueue;
        transport = offlineTransport;
        authorizationClient = offlineAuthorization;
        deviceKeyProvider = () => null;
        ownershipStoreOverride = testOwners;
        rawLogDirectoryOverride = testRawLogDirectory;
    }

    private static DateTime? DirectoryServerTime()
    {
        var directory = StudentDirectoryService.Instance != null ? StudentDirectoryService.Instance.Cache : null;
        return directory != null && AssessmentUploadQueueStore.TryParseUtc(directory.SyncMetadata.lastServerGeneratedAtUtc, out var generated)
            ? generated   // read-only use of the directory's server timestamp
            : null;
    }

    private SessionOwnershipStore OwnersOrNull()
    {
        try
        {
            return OwnershipStore;
        }
        catch (Exception)
        {
            return null;   // no owner store available - entries still clean, owners kept
        }
    }

    private bool IsUploadingNow(string sessionId)
    {
        lock (currentlyUploading)
        {
            return currentlyUploading.Contains(sessionId);
        }
    }

    /// <summary>Like ResumePendingUploads, but respects each RetryWaiting entry's scheduled backoff (nextAttemptAtUtc): only due or parked entries are attempted.</summary>
    public void ResumeDueUploads()
    {
        if (configuration == null || !configuration.automaticUploadEnabled)
        {
            return;
        }
        DateTime now = DateTime.UtcNow;
        foreach (var entry in store.LoadIndex().entries)
        {
            bool due = entry.status == AssessmentUploadStatus.Pending
                || (entry.status == AssessmentUploadStatus.RetryWaiting
                    && (!AssessmentUploadQueueStore.TryParseUtc(entry.nextAttemptAtUtc, out var next) || next <= now));
            if (due)
            {
                _ = AttemptUploadAsync(entry.sessionId, lifetimeCts.Token);
            }
        }
    }

    private void OnDestroy()
    {
        lifetimeCts?.Cancel();
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void HandlePaired()
    {
        LogDebug("PairingIdentityService reported a new pairing - re-scanning the queue for AwaitingPairing entries.");
        ResumePendingUploads();
    }

    /// <summary>Test/tooling seam - swap the transport for MockAssessmentUploadTransport. Never call this from real gameplay code.</summary>
    public void SetTransportForTesting(IAssessmentUploadTransport testTransport) => transport = testTransport;

    /// <summary>Test/tooling seam - point the queue at an isolated temp directory instead of Application.persistentDataPath.</summary>
    public void SetQueueRootForTesting(string rootDirectory) => store = new AssessmentUploadQueueStore(rootDirectory);

    /// <summary>Test/tooling seam - fake upload-authorization server, device key and session-owner store for the VR-code ownership path.</summary>
    public void SetVrOwnershipSeamsForTesting(IVrUploadAuthorizationClient client, Func<string> deviceKey, SessionOwnershipStore owners)
    {
        authorizationClient = client;
        deviceKeyProvider = deviceKey;
        ownershipStoreOverride = owners;
    }

    /// <summary>
    /// Entry point called once a session completes (see EndingManager). Builds the DTO,
    /// writes the payload file, adds the queue entry, and - if automaticUploadEnabled -
    /// starts an upload attempt immediately, all without blocking the caller: this
    /// method returns as soon as the LOCAL save completes ("save the complete session
    /// locally first" / "generate and save the upload payload" happen synchronously and
    /// fast; the network attempt is fire-and-forget from here so the VR completion
    /// screen is never frozen waiting on it).
    /// </summary>
    public AssessmentUploadQueueEntry QueueSessionForUpload(string sessionLogFilePath, string selectedConclusionId, string resolvedEndingId)
    {
        // The DTO is built by reading the log FILE, but SessionLogger buffers events
        // (every N events / N seconds). Without this, the last events before
        // submission - HYP-03, which fires on Submit Conclusion itself - are routinely
        // missing from the payload.
        SessionLogger.Instance?.Flush();

        var dto = AssessmentSessionBuilder.Build(sessionLogFilePath, configuration, scenario, selectedConclusionId, resolvedEndingId);

        // The queue entry's key MUST be whatever sessionId the built DTO actually
        // carries - not a separately-recomputed read of SessionLogger.Instance. The
        // builder already prefers SessionLogger.Instance.SessionId when it's live and
        // falls back to the log file's own embedded sessionId otherwise (see
        // AssessmentSessionBuilder's own class comment); re-deriving it here a second,
        // independent way risked the two disagreeing - confirmed as a real bug during
        // this feature's own test pass, not a hypothetical.
        string sessionId = dto.sessionId;
        if (string.IsNullOrEmpty(sessionId))
        {
            Debug.LogWarning("[AssessmentUploadService] Built DTO has no sessionId - cannot queue an upload.");
            return null;
        }

        // VR-code sessions: the owner was bound when the session STARTED (see
        // SessionOwnership). Read it from that record - never from whoever happens to be
        // identified now. Informational DTO fields mirror it; the server ignores them.
        SessionOwnerRecord owner = null;
        try
        {
            owner = OwnershipStore.Find(sessionId);
        }
        catch (System.IO.InvalidDataException ex)
        {
            Debug.LogError($"[AssessmentUploadService] {ex.Message} Session {sessionId} is queued without a VR owner and will wait for the legacy pairing path instead of guessing an owner.");
        }
        if (owner != null)
        {
            dto.studentId = owner.studentId;
            dto.classId = owner.classIdHint;
            dto.isTestSession = owner.isTestSession;
        }

        string payloadJson = EducationalSessionRecord.Serialize(dto);
        string payloadPath = store.WritePayload(sessionId, payloadJson);

        // Capture WHO was actually paired when this session was played - not from the
        // DTO's own informational studentId/classId, but straight from
        // PairingIdentityService, the only thing this device ever really knows about its
        // own identity. This is the anchor AttemptUploadAsync later checks the CURRENT
        // pairing against before ever sending this entry - see
        // AssessmentUploadStatus.AwaitingPairing's own comment for the full design.
        var identityAtQueueTime = PairingIdentityService.Instance;
        bool pairedNow = identityAtQueueTime != null && identityAtQueueTime.IsPaired;

        var entry = new AssessmentUploadQueueEntry
        {
            sessionId = sessionId,
            payloadVersion = dto.payloadVersion,
            localPayloadPath = payloadPath,
            createdAtUtc = DateTime.UtcNow.ToString("O"),
            // Not paired at the moment a session completes should not happen in normal
            // play (the pairing screen gates entry into a scenario), but if it ever does,
            // this entry has no identity to protect and MUST start at AwaitingPairing
            // rather than Pending - Pending would let it attempt upload with whatever
            // token happens to be live later, which could belong to someone else.
            // A VR-owned session never needs pairing: its owner is already fixed.
            status = owner != null || pairedNow ? AssessmentUploadStatus.Pending : AssessmentUploadStatus.AwaitingPairing,
            studentIdForDisplay = owner != null ? owner.displayNameSnapshot : dto.studentId,
            scenarioIdForDisplay = dto.scenarioId,
            pairedStudentDisplayName = owner == null && pairedNow ? identityAtQueueTime.StudentDisplayName : null,
            pairedClassDisplayName = owner == null && pairedNow ? identityAtQueueTime.ClassDisplayName : null,
            ownerStudentId = owner?.studentId,
            ownerDisplayNameSnapshot = owner?.displayNameSnapshot,
            ownerClassIdHint = owner?.classIdHint,
            isTestSession = owner != null && owner.isTestSession,
            sessionStartedAtUtc = owner?.sessionStartedAtUtc,
            sessionCompletedAtUtc = !string.IsNullOrEmpty(dto.completedAtUtc) ? dto.completedAtUtc : DateTime.UtcNow.ToString("O"),
        };

        if (!store.TryAddEntry(entry))
        {
            // Duplicate sessionId already queued - "prevent duplicate uploads of the
            // same session ID" - the payload file was just overwritten harmlessly
            // (same sessionId, same content shape), but the index is untouched.
            LogDebug($"Session {sessionId} is already queued; not adding a second entry.");
            var existing = store.LoadIndex().entries.Find(e => e.sessionId == sessionId);
            return existing;
        }

        RaiseChanged(entry);
        LogDebug($"Queued session {sessionId} for upload (payloadVersion={dto.payloadVersion}, events={dto.orderedEvents.Count}, status={entry.status}).");

        if (configuration != null && configuration.automaticUploadEnabled && entry.status == AssessmentUploadStatus.Pending)
        {
            _ = AttemptUploadAsync(sessionId, lifetimeCts.Token);
        }

        return entry;
    }

    /// <summary>Manual retry - Editor tool or the completion-screen retry button. Works regardless of automaticUploadEnabled, and regardless of current status EXCEPT Uploaded (nothing to retry) - including FailedPermanently/Rejected, so a developer/instructor can force one more attempt after fixing whatever caused it (e.g. re-pairing for a fresh token).</summary>
    public void RetryNow(string sessionId)
    {
        var index = store.LoadIndex();
        var entry = index.entries.Find(e => e.sessionId == sessionId);
        if (entry == null || entry.status == AssessmentUploadStatus.Uploaded)
        {
            return;
        }

        _ = AttemptUploadAsync(sessionId, lifetimeCts.Token);
    }

    public List<AssessmentUploadQueueEntry> GetAllEntries() => store.LoadIndex().entries;

    public void ResumePendingUploads()
    {
        if (configuration == null || !configuration.automaticUploadEnabled)
        {
            return;
        }

        foreach (var entry in store.LoadIndex().entries)
        {
            if (entry.status is AssessmentUploadStatus.Pending or AssessmentUploadStatus.RetryWaiting
                or AssessmentUploadStatus.Uploading or AssessmentUploadStatus.AwaitingPairing)
            {
                _ = AttemptUploadAsync(entry.sessionId, lifetimeCts.Token);
            }
        }
    }

    /// <summary>
    /// The core of the restart-authentication design (Option B): true only if the
    /// device is CURRENTLY paired (with a still-valid token) as the exact same student
    /// and class this entry was queued under. False whenever the device is unpaired -
    /// including right after a restart, before the player re-pairs - and false when a
    /// DIFFERENT student has paired since. Either false case means "do not upload this
    /// entry," never "upload it under whoever happens to be paired right now."
    /// </summary>
    private static bool IdentityMatchesCurrentPairing(AssessmentUploadQueueEntry entry)
    {
        var identity = PairingIdentityService.Instance;
        if (identity == null || !identity.TokenIsValidNow)
        {
            return false;
        }
        return string.Equals(identity.StudentDisplayName, entry.pairedStudentDisplayName, StringComparison.Ordinal)
            && string.Equals(identity.ClassDisplayName, entry.pairedClassDisplayName, StringComparison.Ordinal);
    }

    private async Task AttemptUploadAsync(string sessionId, CancellationToken cancellationToken)
    {
        // Never run two attempts for the same session concurrently - a manual retry
        // fired while an automatic attempt is already in flight (or vice versa) is a
        // no-op rather than a race on the same queue entry/payload file.
        lock (currentlyUploading)
        {
            if (!currentlyUploading.Add(sessionId))
            {
                return;
            }
        }

        try
        {
            var index = store.LoadIndex();
            var entry = index.entries.Find(e => e.sessionId == sessionId);
            if (entry == null || entry.status == AssessmentUploadStatus.Uploaded)
            {
                return;
            }

            // Blocked safely, with NO network call: if the currently paired identity
            // (or lack of one) doesn't match who this session was queued under, this
            // entry sits at AwaitingPairing until the right student pairs again. See
            // IdentityMatchesCurrentPairing's own comment for the full rule.
            // VR-owned entries skip this: their owner is fixed on the entry itself and
            // they authorize per upload (AttemptVrOwnedUploadAsync), never via pairing.
            if (!entry.UsesVrOwnership && !IdentityMatchesCurrentPairing(entry))
            {
                if (entry.status != AssessmentUploadStatus.AwaitingPairing)
                {
                    LogDebug($"Session {sessionId} requires the originally-paired student to re-pair before it can upload - marking AwaitingPairing.");
                    store.UpdateEntry(sessionId, e => e.status = AssessmentUploadStatus.AwaitingPairing);
                    RaiseChangedFor(sessionId);
                }
                return;
            }

            string payloadJson = store.ReadPayload(entry.localPayloadPath);
            if (payloadJson == null)
            {
                Debug.LogError($"[AssessmentUploadService] Payload file missing for session {sessionId} at {entry.localPayloadPath}. Marking FailedPermanently - the local session log itself is untouched.");
                UpdateStatus(sessionId, AssessmentUploadStatus.FailedPermanently, AssessmentUploadErrorCategory.Unknown, "Local payload file missing.");
                return;
            }

            // Fail closed on a misconfigured asset (e.g. a Quest/Production config that
            // somehow still points at localhost) rather than attempting a request that
            // would either fail unhelpfully on-device or, worse, silently succeed against
            // the wrong host. See AssessmentUploadConfiguration.IsValidForRuntimeUse.
            if (configuration != null && !configuration.IsValidForRuntimeUse(out string invalidReason))
            {
                Debug.LogError($"[AssessmentUploadService] Refusing to upload session {sessionId}: {invalidReason}");
                UpdateStatus(sessionId, AssessmentUploadStatus.FailedPermanently, AssessmentUploadErrorCategory.Unknown, invalidReason);
                return;
            }

            UpdateStatus(sessionId, AssessmentUploadStatus.Uploading, AssessmentUploadErrorCategory.None, null, incrementAttempt: true);

            if (entry.UsesVrOwnership)
            {
                await AttemptVrOwnedUploadAsync(entry, payloadJson, cancellationToken);
                return;
            }

            string bearerToken = PairingIdentityService.Instance != null ? PairingIdentityService.Instance.AssessmentSessionToken : null;
            float timeout = configuration != null ? configuration.requestTimeoutSeconds : 20f;
            string url = configuration != null ? configuration.SubmissionUrl : null;

            AssessmentUploadAttemptResult result;
            try
            {
                result = await transport.UploadAsync(url, payloadJson, bearerToken, timeout, cancellationToken);
            }
            catch (Exception ex)
            {
                LogDebug($"Upload attempt for {sessionId} threw {ex.GetType().Name}.");
                result = AssessmentUploadAttemptResult.Fail(AssessmentUploadErrorCategory.Unknown, 0, ex.Message);
            }

            HandleResult(sessionId, result);
        }
        finally
        {
            lock (currentlyUploading)
            {
                currentlyUploading.Remove(sessionId);
            }
        }
    }

    // ------------------------------------------------------------------------
    // VR-code ownership path (Phase 3)
    // ------------------------------------------------------------------------

    /// <summary>
    /// Upload for a session whose owner was bound at session start (entry.ownerStudentId).
    /// 1. Ask /api/v1/vr/session-upload-token, authenticated by the DEVICE key, for a
    ///    short-lived token for that stored owner and this session only.
    /// 2. Send the unchanged payload through the existing transport/endpoint with it.
    /// 3. If the server says the token expired/invalid, get ONE fresh token and retry once.
    /// The token lives only in this method's locals - never on the entry, never on disk,
    /// never logged. A failure never deletes the payload or the entry.
    /// </summary>
    private async Task AttemptVrOwnedUploadAsync(AssessmentUploadQueueEntry entry, string payloadJson, CancellationToken cancellationToken)
    {
        string sessionId = entry.sessionId;
        var authorization = await RequestUploadAuthorizationAsync(entry, cancellationToken);
        if (!authorization.Success)
        {
            HandleAuthorizationFailure(sessionId, authorization);
            return;
        }

        var result = await SendAsync(sessionId, payloadJson, authorization.UploadToken, cancellationToken);
        if (!result.Success && result.ErrorCategory == AssessmentUploadErrorCategory.AuthenticationFailed)
        {
            LogDebug($"Session {sessionId}: upload token rejected (expired/invalid) - requesting a fresh one.");
            authorization = await RequestUploadAuthorizationAsync(entry, cancellationToken);
            if (!authorization.Success)
            {
                HandleAuthorizationFailure(sessionId, authorization);
                return;
            }
            result = await SendAsync(sessionId, payloadJson, authorization.UploadToken, cancellationToken);
            if (!result.Success && result.ErrorCategory == AssessmentUploadErrorCategory.AuthenticationFailed)
            {
                // Not a pairing problem - never AwaitingPairing for a VR-owned entry.
                ScheduleRetryOrPark(sessionId, AssessmentUploadErrorCategory.AuthenticationFailed, "The server did not accept a fresh upload authorization. Retrying later.", result.RetryAfterSeconds);
                return;
            }
        }

        // Success, or a permanent payload rejection: the existing handling, unchanged.
        if (result.Success || result.ErrorCategory is AssessmentUploadErrorCategory.ValidationRejected or AssessmentUploadErrorCategory.PayloadTooLarge)
        {
            HandleResult(sessionId, result);
            return;
        }
        ScheduleRetryOrPark(sessionId, result.ErrorCategory, SafeMessage(result), result.RetryAfterSeconds);
    }

    private async Task<VrUploadAuthorizationResult> RequestUploadAuthorizationAsync(AssessmentUploadQueueEntry entry, CancellationToken cancellationToken)
    {
        string url = configuration != null ? configuration.VrSessionUploadTokenUrl : null;
        string key = deviceKeyProvider?.Invoke();
        if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(key))
        {
            return new VrUploadAuthorizationResult { Failure = VrUploadAuthorizationFailure.NotConfigured };
        }

        var request = new VrUploadTokenRequestDto
        {
            studentId = entry.ownerStudentId,          // the ORIGINAL owner, from the entry
            sessionId = entry.sessionId,
            scenarioId = entry.scenarioIdForDisplay,
            classIdHint = entry.ownerClassIdHint,
            isTestSession = entry.isTestSession,
            sessionStartedAtUtc = entry.sessionStartedAtUtc,
        };
        float timeout = configuration != null ? configuration.requestTimeoutSeconds : 20f;
        try
        {
            return await authorizationClient.RequestAsync(url, key, request, timeout, cancellationToken);
        }
        catch (Exception ex)
        {
            LogDebug($"Upload authorization for {entry.sessionId} threw {ex.GetType().Name}.");
            return new VrUploadAuthorizationResult { Failure = VrUploadAuthorizationFailure.NoNetwork };
        }
    }

    private async Task<AssessmentUploadAttemptResult> SendAsync(string sessionId, string payloadJson, string bearerToken, CancellationToken cancellationToken)
    {
        float timeout = configuration != null ? configuration.requestTimeoutSeconds : 20f;
        string url = configuration != null ? configuration.SubmissionUrl : null;
        try
        {
            return await transport.UploadAsync(url, payloadJson, bearerToken, timeout, cancellationToken);
        }
        catch (Exception ex)
        {
            LogDebug($"Upload attempt for {sessionId} threw {ex.GetType().Name}.");
            return AssessmentUploadAttemptResult.Fail(AssessmentUploadErrorCategory.Unknown, 0, ex.Message);
        }
    }

    private void HandleAuthorizationFailure(string sessionId, VrUploadAuthorizationResult authorization)
    {
        LogDebug($"Session {sessionId}: upload authorization failed ({authorization.Failure}, {authorization.ServerErrorCode ?? "-"}, HTTP {authorization.HttpStatusCode}).");
        switch (authorization.Failure)
        {
            case VrUploadAuthorizationFailure.Refused:
                // The server permanently refused THIS session (e.g. it is already stored
                // for another student). Kept on disk as Rejected - never deleted - so an
                // instructor can inspect it; RetryNow still works after a server fix.
                UpdateStatus(sessionId, AssessmentUploadStatus.Rejected, AssessmentUploadErrorCategory.ValidationRejected, RefusalMessage(authorization.ServerErrorCode));
                return;
            case VrUploadAuthorizationFailure.NotConfigured:
                // Can't fix itself this run: wait (resumed next launch or on manual retry).
                UpdateStatus(sessionId, AssessmentUploadStatus.RetryWaiting, AssessmentUploadErrorCategory.Unknown, "This headset isn't set up to upload yet (no server address or device key). The session is saved.");
                return;
            case VrUploadAuthorizationFailure.DeviceUnauthorized:
                ScheduleRetryOrPark(sessionId, AssessmentUploadErrorCategory.AuthenticationFailed, "The server did not accept this headset's device key. The session is saved and will retry.", -1f);
                return;
            case VrUploadAuthorizationFailure.NoNetwork:
                ScheduleRetryOrPark(sessionId, AssessmentUploadErrorCategory.NoNetwork, "No internet connection. The session is saved and will upload later.", -1f);
                return;
            case VrUploadAuthorizationFailure.Timeout:
                ScheduleRetryOrPark(sessionId, AssessmentUploadErrorCategory.Timeout, "The server took too long to respond. Retrying later.", -1f);
                return;
            case VrUploadAuthorizationFailure.InvalidResponse:
                ScheduleRetryOrPark(sessionId, AssessmentUploadErrorCategory.InvalidServerResponse, "Unexpected server response. Retrying later.", -1f);
                return;
            default:
                if (authorization.ServerErrorCode == "NO_ASSIGNMENT")
                {
                    // Recoverable business rule: kept local with its owner and payload,
                    // retried with normal backoff until an instructor creates the assignment.
                    ScheduleRetryOrPark(sessionId, AssessmentUploadErrorCategory.NoAssignment,
                        "The student has no assignment for this scenario yet. The session is saved and will upload once the instructor creates one.", -1f);
                    return;
                }
                ScheduleRetryOrPark(sessionId, AssessmentUploadErrorCategory.ServerError, "The server couldn't authorize the upload right now. Retrying later.", -1f);
                return;
        }
    }

    private static string RefusalMessage(string code) => code switch
    {
        "SESSION_OWNER_MISMATCH" => "This session is already recorded for a different student. Kept on this headset for review.",
        "STUDENT_DISABLED" => "VR access for this student was disabled before this session started. Kept on this headset for review.",
        "STUDENT_NOT_FOUND" => "This session's student no longer exists on the server. Kept on this headset for review.",
        "STUDENT_NOT_PERMITTED" => "This session's student is not set up for VR. Kept on this headset for review.",
        _ => "The server refused to authorize this upload. Kept on this headset for review.",
    };

    /// <summary>
    /// Retryable failure on the VR path: back off like HandleResult does, but once the
    /// attempt budget is spent, PARK the entry at RetryWaiting (picked up again on the next
    /// launch or a manual retry) instead of FailedPermanently - an offline week or a
    /// rotated device key must never strand a completed session.
    /// </summary>
    private void ScheduleRetryOrPark(string sessionId, AssessmentUploadErrorCategory category, string message, float retryAfterSeconds)
    {
        var entry = store.LoadIndex().entries.Find(e => e.sessionId == sessionId);
        int attemptCount = entry?.attemptCount ?? 1;
        int maxAttempts = configuration != null ? configuration.maxRetryAttempts : 8;
        bool parked = attemptCount >= maxAttempts;
        float delaySeconds = parked ? 0f : ComputeBackoffSeconds(attemptCount, retryAfterSeconds);
        DateTime? next = parked ? null : DateTime.UtcNow.AddSeconds(delaySeconds);

        store.UpdateEntry(sessionId, e =>
        {
            e.status = AssessmentUploadStatus.RetryWaiting;
            e.lastErrorCategory = category;
            e.lastErrorMessage = message;
            e.lastAttemptedAtUtc = DateTime.UtcNow.ToString("O");
            e.nextAttemptAtUtc = next?.ToString("O");
        });
        RaiseChangedFor(sessionId);

        if (!parked)
        {
            _ = ScheduleRetryAsync(sessionId, delaySeconds, lifetimeCts.Token);
        }
    }

    private void HandleResult(string sessionId, AssessmentUploadAttemptResult result)
    {
        if (result.Success)
        {
            // A 409-with-body result arrives here as Success (see
            // UnityWebRequestUploadTransport) but MUST still be cross-checked: only a
            // server-confirmed matching sessionId counts as "this session is safely
            // stored," per this feature's own explicit duplicate-handling rule.
            var data = result.Response.data;
            if (data.duplicate && data.sessionId != sessionId)
            {
                LogDebug($"Session {sessionId}: server reported a duplicate for a DIFFERENT sessionId ({data.sessionId}) - treating as an error, not success.");
                UpdateStatus(sessionId, AssessmentUploadStatus.Rejected, AssessmentUploadErrorCategory.InvalidServerResponse, "Server's duplicate response did not match this session.");
                return;
            }

            LogDebug($"Session {sessionId} uploaded successfully (receipt={data.receiptId}, duplicate={data.duplicate}).");
            UpdateStatus(sessionId, AssessmentUploadStatus.Uploaded, AssessmentUploadErrorCategory.None, null, serverReceiptId: data.receiptId);
            RecordUploadConfirmation(sessionId, data.serverReceivedAtUtc);
            RunRetentionCleanup();
            return;
        }

        LogDebug($"Session {sessionId} upload failed: {result.ErrorCategory} (HTTP {result.HttpStatusCode}).");

        // An authentication failure (expired/invalid token) is not "permanently
        // failed" - it's "needs the same student to pair again," exactly what
        // AwaitingPairing already means. Routing it there (rather than
        // FailedPermanently) means HandlePaired's automatic re-scan resumes this
        // entry the moment that happens, with no manual retry required.
        if (result.ErrorCategory == AssessmentUploadErrorCategory.AuthenticationFailed)
        {
            LogDebug($"Session {sessionId}: authentication failed (token expired/invalid) - marking AwaitingPairing.");
            UpdateStatus(sessionId, AssessmentUploadStatus.AwaitingPairing, result.ErrorCategory, SafeMessage(result));
            return;
        }

        bool isPermanent = result.ErrorCategory is
            AssessmentUploadErrorCategory.ValidationRejected or
            AssessmentUploadErrorCategory.PayloadTooLarge;

        if (isPermanent)
        {
            var terminalStatus = result.ErrorCategory == AssessmentUploadErrorCategory.ValidationRejected
                ? AssessmentUploadStatus.Rejected
                : AssessmentUploadStatus.FailedPermanently;
            UpdateStatus(sessionId, terminalStatus, result.ErrorCategory, SafeMessage(result));
            return;
        }

        var index = store.LoadIndex();
        var entry = index.entries.Find(e => e.sessionId == sessionId);
        int attemptCount = entry?.attemptCount ?? 1;
        int maxAttempts = configuration != null ? configuration.maxRetryAttempts : 8;

        if (attemptCount >= maxAttempts)
        {
            LogDebug($"Session {sessionId} exhausted {attemptCount} attempts - marking FailedPermanently.");
            UpdateStatus(sessionId, AssessmentUploadStatus.FailedPermanently, result.ErrorCategory, SafeMessage(result));
            return;
        }

        float delaySeconds = ComputeBackoffSeconds(attemptCount, result.RetryAfterSeconds);
        var nextAttemptUtc = DateTime.UtcNow.AddSeconds(delaySeconds);

        store.UpdateEntry(sessionId, e =>
        {
            e.status = AssessmentUploadStatus.RetryWaiting;
            e.lastErrorCategory = result.ErrorCategory;
            e.lastErrorMessage = SafeMessage(result);
            e.nextAttemptAtUtc = nextAttemptUtc.ToString("O");
        });
        RaiseChangedFor(sessionId);

        LogDebug($"Session {sessionId} retry {attemptCount}/{maxAttempts} scheduled in {delaySeconds:F1}s.");
        _ = ScheduleRetryAsync(sessionId, delaySeconds, lifetimeCts.Token);
    }

    private async Task ScheduleRetryAsync(string sessionId, float delaySeconds, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
        }
        catch (TaskCanceledException)
        {
            return; // application closing - "never delete the original session log", and a cancelled retry simply resumes next launch via ResumePendingUploads
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        await AttemptUploadAsync(sessionId, cancellationToken);
    }

    /// <summary>Exponential backoff from initialRetryDelaySeconds, doubling per attempt, clamped to maxRetryDelaySeconds - honors a server-provided Retry-After (HTTP 429) as a FLOOR when it's larger than the computed backoff, never as a way to retry sooner than the normal policy would.</summary>
    private float ComputeBackoffSeconds(int attemptCount, float serverRetryAfterSeconds)
    {
        float initial = configuration != null ? configuration.initialRetryDelaySeconds : 5f;
        float max = configuration != null ? configuration.maxRetryDelaySeconds : 300f;

        float exponential = initial * Mathf.Pow(2f, Mathf.Max(0, attemptCount - 1));
        float clamped = Mathf.Min(exponential, max);

        if (serverRetryAfterSeconds > clamped)
        {
            clamped = Mathf.Min(serverRetryAfterSeconds, max);
        }

        return clamped;
    }

    private static string SafeMessage(AssessmentUploadAttemptResult result)
    {
        if (!string.IsNullOrEmpty(result.ServerError?.error?.message))
        {
            return result.ServerError.error.message;
        }
        return result.ErrorCategory switch
        {
            AssessmentUploadErrorCategory.NoNetwork => "No internet connection.",
            AssessmentUploadErrorCategory.Timeout => "The server took too long to respond.",
            AssessmentUploadErrorCategory.ValidationRejected => "The server rejected this submission.",
            AssessmentUploadErrorCategory.AuthenticationFailed => "Your session has expired. Please pair again.",
            AssessmentUploadErrorCategory.PayloadTooLarge => "This session's data was too large to upload.",
            AssessmentUploadErrorCategory.RateLimited => "Too many requests. Retrying shortly.",
            AssessmentUploadErrorCategory.ServerError => "The server had a problem. Retrying shortly.",
            AssessmentUploadErrorCategory.InvalidServerResponse => "Unexpected server response.",
            _ => "Upload failed."
        };
    }

    /// <summary>
    /// Starts the 30-day local retention for a server-confirmed upload. The retention clock
    /// is the SERVER's receipt time (serverReceivedAtUtc - the original receipt even for a
    /// duplicate), recorded together with the headset's own time of confirmation as a
    /// second guard. If the server sent no usable time, uploadConfirmedAtUtc stays empty and
    /// the entry is simply never auto-cleaned (safe), but it is still Uploaded.
    /// </summary>
    private void RecordUploadConfirmation(string sessionId, string serverReceivedAtUtc)
    {
        bool hasServerTime = AssessmentUploadQueueStore.TryParseUtc(serverReceivedAtUtc, out var serverTime);
        DateTime localNow = DateTime.UtcNow;
        store.UpdateEntry(sessionId, e =>
        {
            e.uploadConfirmedLocalAtUtc = localNow.ToString("O");
            if (hasServerTime)
            {
                e.uploadConfirmedAtUtc = serverTime.ToString("O");
                e.localExpiresAtUtc = (serverTime + UploadedSessionRetention.Retention).ToString("O");
            }
        });
        if (hasServerTime)
        {
            store.RecordServerTime(serverTime);
        }
        RaiseChangedFor(sessionId);
    }

    private void UpdateStatus(string sessionId, AssessmentUploadStatus status, AssessmentUploadErrorCategory errorCategory, string errorMessage, bool incrementAttempt = false, string serverReceiptId = null)
    {
        store.UpdateEntry(sessionId, e =>
        {
            e.status = status;
            e.lastErrorCategory = errorCategory;
            e.lastErrorMessage = errorMessage;
            e.lastAttemptedAtUtc = DateTime.UtcNow.ToString("O");
            if (incrementAttempt) e.attemptCount++;
            if (serverReceiptId != null) e.serverReceiptId = serverReceiptId;
        });
        RaiseChangedFor(sessionId);
    }

    private void RaiseChangedFor(string sessionId)
    {
        var entry = store.LoadIndex().entries.Find(e => e.sessionId == sessionId);
        if (entry != null) RaiseChanged(entry);
    }

    private static void RaiseChanged(AssessmentUploadQueueEntry entry) => OnQueueEntryChanged?.Invoke(entry);

    private void LogDebug(string message)
    {
        if (configuration != null && configuration.debugLoggingEnabled)
        {
            Debug.Log($"[AssessmentUploadService] {message}");
        }
    }
}
