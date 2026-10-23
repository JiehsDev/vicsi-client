// Assets/_Project/Scripts/Instructor/HeadsetStatus.cs
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public enum ServerReachability { Unknown, Checking, Online, Offline, NotConfigured }

/// <summary>
/// Is the ViCSI API actually reachable? Probes the dashboard's existing unauthenticated
/// GET /api/v1/health (no credential sent) with a short timeout. Unity's
/// Application.internetReachability is not used as proof - it only says a network
/// interface exists, not that the API answers. A successful student-directory sync also
/// counts as proof of reachability (see Note).
/// </summary>
public static class ServerReachabilityProbe
{
    public static ServerReachability Last { get; private set; } = ServerReachability.Unknown;
    public static DateTime? LastCheckedUtc { get; private set; }
    public static event Action<ServerReachability> OnChanged;

    /// <summary>Records reachability learned elsewhere (e.g. a successful directory sync).</summary>
    public static void Note(ServerReachability value)
    {
        Last = value;
        LastCheckedUtc = DateTime.UtcNow;
        OnChanged?.Invoke(value);
    }

    public static async Task<ServerReachability> CheckAsync(AssessmentUploadConfiguration config, float timeoutSeconds = 5f, CancellationToken ct = default)
    {
        if (config == null || !config.IsValidForRuntimeUse(out _))
        {
            Note(ServerReachability.NotConfigured);
            return Last;
        }
        Last = ServerReachability.Checking;
        OnChanged?.Invoke(Last);

        string url = config.apiBaseUrl.TrimEnd('/') + "/api/v1/health";
        using var request = UnityWebRequest.Get(url);
        request.timeout = Mathf.Max(1, Mathf.CeilToInt(timeoutSeconds));
        var op = request.SendWebRequest();
        while (!op.isDone)
        {
            if (ct.IsCancellationRequested)
            {
                request.Abort();
                Note(ServerReachability.Unknown);
                return Last;
            }
            await Task.Yield();
        }
        Note(request.result == UnityWebRequest.Result.Success && request.responseCode == 200 ? ServerReachability.Online : ServerReachability.Offline);
        return Last;
    }
}

/// <summary>
/// Pure, testable snapshot behind the Headset Status tab and the start-menu status line.
/// Built only from existing sources: StudentDirectoryCache (ActiveStudentCount,
/// SyncMetadata, TestStudent), AssessmentUploadService.GetAllEntries() through
/// AssessmentUploadQueueView, the device-key presence (never its value) and
/// ServerReachabilityProbe.
/// </summary>
public class HeadsetStatusSnapshot
{
    public ServerReachability Connection;
    public bool DeviceCredentialAvailable;
    public bool ServerConfigured;
    public string EnvironmentName;

    public int ActiveCachedStudents;
    public int CachedStudentRows;
    public string LastSuccessfulSyncUtc;
    public string LastSyncAttemptUtc;
    public StudentDirectorySyncStatus LastSyncStatus;
    public string LastSyncError;
    public bool TestModeAvailable;

    public AssessmentUploadQueueView.Summary Queue = new();
    public int TotalLocalSessionRecords;
    public int CleanupEligible;

    public bool Ready;
    public readonly List<string> ActionsNeeded = new();
    public readonly List<string> Notes = new();

    public static HeadsetStatusSnapshot Build(StudentDirectoryCache directory, IReadOnlyList<AssessmentUploadQueueEntry> entries, bool deviceCredentialAvailable,
        AssessmentUploadConfiguration config, ServerReachability connection, int cleanupEligible)
    {
        var s = new HeadsetStatusSnapshot
        {
            Connection = connection,
            DeviceCredentialAvailable = deviceCredentialAvailable,
            ServerConfigured = config != null && config.IsValidForRuntimeUse(out _),
            EnvironmentName = config != null ? config.environmentName : "(none)",
            CleanupEligible = cleanupEligible,
        };

        if (directory != null)
        {
            var meta = directory.SyncMetadata;
            s.ActiveCachedStudents = directory.ActiveStudentCount;
            s.CachedStudentRows = directory.Students.Count;
            s.LastSuccessfulSyncUtc = meta.lastSuccessfulSyncUtc;
            s.LastSyncAttemptUtc = meta.lastAttemptUtc;
            s.LastSyncStatus = meta.LastStatus;
            s.LastSyncError = meta.lastError;
            s.TestModeAvailable = directory.TestStudent.IsKnown;
        }
        if (entries != null)
        {
            s.Queue = AssessmentUploadQueueView.Summarize(entries);
            s.TotalLocalSessionRecords = entries.Count;
        }

        // ---- readiness: only things that stop intended use are "action needed" ----
        if (!s.ServerConfigured) s.ActionsNeeded.Add("No valid server address configured on this headset.");
        if (!deviceCredentialAvailable) s.ActionsNeeded.Add("Device credential missing - this headset can't sync students or upload sessions.");
        if (string.IsNullOrEmpty(s.LastSuccessfulSyncUtc)) s.ActionsNeeded.Add("Student directory has never synced - real student codes won't work offline yet.");
        else if (s.ActiveCachedStudents == 0) s.ActionsNeeded.Add("No active students in the local directory.");
        if (s.Queue.NoAssignment > 0) s.ActionsNeeded.Add($"{s.Queue.NoAssignment} session(s) waiting for a scenario assignment.");
        if (s.Queue.RequiresReview > 0) s.ActionsNeeded.Add($"{s.Queue.RequiresReview} session(s) need review (refused by the server).");
        if (s.Queue.RetryWaiting > 0 && s.Connection == ServerReachability.Online) s.ActionsNeeded.Add($"{s.Queue.RetryWaiting} upload(s) failing while the server is reachable.");

        // Normal offline backlog is not an error.
        if (s.Queue.Pending + s.Queue.RetryWaiting > 0 && s.Connection != ServerReachability.Online)
            s.Notes.Add($"{s.Queue.Pending + s.Queue.RetryWaiting} session(s) saved locally - they upload when the server is reachable.");
        if (s.TestModeAvailable) s.Notes.Add("Test Mode (Continue Without Code) is available.");
        else s.Notes.Add("Test Mode needs one successful directory sync.");

        // Ready = something usable: real students cached, OR test mode available; and no config blocker.
        bool usable = s.ActiveCachedStudents > 0 || s.TestModeAvailable;
        s.Ready = usable && s.ServerConfigured && s.ActionsNeeded.Count == 0;
        return s;
    }

    public string ReadinessLabel => Ready ? "READY FOR TRAINING" : "ACTION NEEDED";

    public string ConnectionLabel => Connection switch
    {
        ServerReachability.Online => "Online",
        ServerReachability.Offline => "Offline",
        ServerReachability.Checking => "Checking...",
        ServerReachability.NotConfigured => "Not configured",
        _ => "Unknown",
    };

    public static string FormatUtc(string isoUtc)
    {
        if (!AssessmentUploadQueueStore.TryParseUtc(isoUtc, out var t)) return "Never";
        return t.ToLocalTime().ToString("MMM d, HH:mm");
    }
}
