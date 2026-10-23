// Assets/_Project/Scripts/Assessment/AutomatedTestSandbox.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Keeps automated play-mode tests away from the developer's REAL session data and from
/// any real server. Any test that can reach the end of a session (EndingManager ->
/// AssessmentUploadService.QueueSessionForUpload) calls Activate() before it starts.
///
/// WHAT IT DOES, for the rest of the Play session (deliberately not a short scope: the
/// ending is asynchronous - checkpoint and picker frames run after the test's synchronous
/// part returns - so a scope closed early would let the real queue be written anyway):
///   - redirects the scene's live AssessmentUploadService to a temporary queue folder,
///     temporary owner store and temporary raw-log location;
///   - replaces its network with offline-only fakes (no request ever leaves the editor -
///     not to Vercel, not to Supabase) and gives it NO device key;
///   - redirects SessionOwnership.Store (where identification binds owners) to the same
///     temporary folder;
///   - moves this Play session's raw SessionLogger log into the temporary folder and
///     keeps it appending there (tests still read it via SessionLogger.ActiveLogFilePath).
/// The temporary folder is deleted when Play mode ends (Application.quitting), and any
/// leftover from a crashed run is removed on the next Activate.
///
/// WHAT IT DOESN'T DO: nothing changes for a normal manual Play session - Activate is
/// only ever called by test code. A Play session in which a test has run is a test
/// session from then on (documented, intentional).
///
/// Real-server smoke tests must NOT call Activate - they are opt-in by simply not using
/// this sandbox.
/// </summary>
public static class AutomatedTestSandbox
{
    public static bool IsActive { get; private set; }
    public static string Root { get; private set; }
    /// <summary>The isolated queue the redirected uploader writes to - tests assert "would have been queued" against it.</summary>
    public static AssessmentUploadQueueStore Queue { get; private set; }
    public static SessionOwnershipStore Owners { get; private set; }
    public static readonly List<string> ActivatedBy = new();
    public static OfflineUploadTransport Transport { get; private set; }
    public static OfflineUploadAuthorization Authorization { get; private set; }

    private const string SandboxFolder = "vicsi_test_sandbox";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        // Covers "Enter Play Mode" without domain reload: never carry a sandbox across sessions.
        IsActive = false;
        Root = null;
        Queue = null;
        Owners = null;
        Transport = null;
        Authorization = null;
        ActivatedBy.Clear();
    }

    /// <summary>Isolates this Play session from real data and servers. Idempotent; call at the very start of any test that can complete a session.</summary>
    public static void Activate(string activatedBy)
    {
        ActivatedBy.Add(activatedBy);
        if (!IsActive)
        {
            string parent = Path.Combine(Application.temporaryCachePath, SandboxFolder);
            DeleteStaleSandboxes(parent);
            Root = Path.Combine(parent, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            Queue = new AssessmentUploadQueueStore(Path.Combine(Root, "queue"));
            Owners = new SessionOwnershipStore(Root);
            Transport = new OfflineUploadTransport();
            Authorization = new OfflineUploadAuthorization();
            IsActive = true;
            Application.quitting += Cleanup;

            // This Play session is now a test session: move its raw session log out of
            // persistentDataPath too (tests still read it through ActiveLogFilePath).
            string activeLog = SessionLogger.Instance != null ? SessionLogger.Instance.ActiveLogFilePath : null;
            if (!string.IsNullOrEmpty(activeLog))
            {
                foreach (var writer in UnityEngine.Object.FindObjectsByType<LocalJsonLogWriter>(FindObjectsInactive.Include))
                {
                    if (writer.FilePath == activeLog)
                    {
                        try
                        {
                            writer.RelocateTo(Path.Combine(Root, "session_log"));
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[AutomatedTestSandbox] Could not move the session log into the sandbox ({ex.GetType().Name}); it stays in persistentDataPath.");
                        }
                    }
                }
            }
            Debug.Log($"[AutomatedTestSandbox] Active for this Play session (first: {activatedBy}). Upload queue, session owners and network are isolated in a temp folder.");
        }

        // Re-applied every call: a test may have created/restored another service instance meanwhile.
        SessionOwnership.Store = Owners;
        AssessmentUploadService.Instance?.RedirectForAutomatedTests(Queue, Transport, Authorization, Owners, Path.Combine(Root, "raw"));
    }

    /// <summary>Entry count of the REAL (persistentDataPath) upload queue - read-only, for "real queue unchanged" assertions.</summary>
    public static int RealQueueEntryCount()
    {
        string index = Path.Combine(Application.persistentDataPath, "assessment_upload_queue_index.json");
        if (!File.Exists(index)) return 0;
        try
        {
            var parsed = JsonUtility.FromJson<AssessmentUploadQueueIndex>(File.ReadAllText(index));
            return parsed?.entries?.Count ?? 0;
        }
        catch (Exception)
        {
            return -1;
        }
    }

    /// <summary>Owner-record count of the REAL session owner file - read-only.</summary>
    public static int RealOwnerRecordCount()
    {
        string file = Path.Combine(Application.persistentDataPath, SessionOwnershipStore.FileName);
        if (!File.Exists(file)) return 0;
        try
        {
            return JsonUtility.FromJson<SessionOwnerFile>(File.ReadAllText(file))?.sessions?.Count ?? 0;
        }
        catch (Exception)
        {
            return -1;
        }
    }

    private static void Cleanup()
    {
        Application.quitting -= Cleanup;
        TryDelete(Root);
        IsActive = false;
    }

    private static void DeleteStaleSandboxes(string parent)
    {
        if (!Directory.Exists(parent)) return;
        foreach (var dir in Directory.GetDirectories(parent))
        {
            TryDelete(dir);
        }
    }

    private static void TryDelete(string dir)
    {
        try
        {
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir) && dir.Contains(SandboxFolder))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
        catch (Exception)
        {
            // best effort - removed on the next Activate
        }
    }
}

/// <summary>Offline-only upload transport for the sandbox: records attempts, never touches the network.</summary>
public class OfflineUploadTransport : IAssessmentUploadTransport
{
    public int Attempts;
    public Task<AssessmentUploadAttemptResult> UploadAsync(string url, string payloadJson, string bearerToken, float timeoutSeconds, CancellationToken ct)
    {
        Interlocked.Increment(ref Attempts);
        return Task.FromResult(AssessmentUploadAttemptResult.Fail(AssessmentUploadErrorCategory.NoNetwork, 0, "Automated test sandbox: network disabled."));
    }
}

/// <summary>Offline-only upload authorization for the sandbox: records attempts, never touches the network.</summary>
public class OfflineUploadAuthorization : IVrUploadAuthorizationClient
{
    public int Attempts;
    public Task<VrUploadAuthorizationResult> RequestAsync(string url, string deviceKey, VrUploadTokenRequestDto request, float timeoutSeconds, CancellationToken ct)
    {
        Interlocked.Increment(ref Attempts);
        return Task.FromResult(new VrUploadAuthorizationResult { Failure = VrUploadAuthorizationFailure.NoNetwork });
    }
}
