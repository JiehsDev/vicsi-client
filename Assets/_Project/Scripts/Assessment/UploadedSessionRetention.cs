// Assets/_Project/Scripts/Assessment/UploadedSessionRetention.cs
using System;
using System.Collections.Generic;

/// <summary>
/// 30-day local retention for SERVER-CONFIRMED uploaded sessions - and only those.
///
/// A session's local copy (queue entry + payload file) is removed only when ALL hold:
///   1. status == Uploaded (set only after the server's success response)
///   2. it has the server's receipt time (uploadConfirmedAtUtc)
///   3. trusted "now" - uploadConfirmedAtUtc >= 30 days
///   4. the headset's own clock also says >= 30 days since the confirmation arrived
///      (uploadConfirmedLocalAtUtc), when that was recorded
///   5. it is not being uploaded right now
/// Pending, RetryWaiting (incl. NO_ASSIGNMENT / Interrupted), AwaitingPairing, Uploading,
/// Rejected and FailedPermanently entries are never touched, however old.
///
/// CLOCK SOURCE: retention counts from the SERVER's receipt time. "Now" is the device
/// clock, but capped at the latest server time this headset has actually seen (upload
/// receipts, and the directory sync's generatedAtUtc) plus ServerTimeGraceDays. A device
/// clock that jumps forward therefore cannot delete anything early; with no server time
/// known at all, nothing is deleted. A headset that hasn't talked to the server in a while
/// simply keeps its copies longer - over-retention is the safe failure.
///
/// After removing a local copy, the session's SessionOwnershipStore record is pruned -
/// but only if no queue entry with that sessionId remains. Owner records of every other
/// session are untouched. The session's raw log (LocalJsonLogWriter's .jsonl) is deleted
/// with it only when it is PROVEN to be that session's (RawSessionLogs: GUID in the name
/// AND in the file's first event); anything unproven is kept and reported. Nothing here
/// touches the student directory (never calls StudentDirectoryService.ClearLocalDirectory)
/// or the server.
/// </summary>
public static class UploadedSessionRetention
{
    public const int RetentionDays = 30;
    public const int ServerTimeGraceDays = 1;

    public static TimeSpan Retention => TimeSpan.FromDays(RetentionDays);

    public class Result
    {
        public readonly List<string> RemovedSessionIds = new();
        public readonly List<string> PrunedOwnerSessionIds = new();
        public DateTime? TrustedNowUtc;
        public string SkippedReason;
        public readonly List<string> RemovedRawLogFiles = new();
        /// <summary>Raw logs that could not be proven to belong to a removed session (or could not be deleted) - kept.</summary>
        public readonly List<string> RetainedRawLogFiles = new();
    }

    /// <summary>The "now" cleanup may use: device time, never later than the latest known server time + grace. Null if no server time is known.</summary>
    public static DateTime? TrustedNow(DateTime deviceNowUtc, DateTime? latestServerTimeUtc)
    {
        if (latestServerTimeUtc == null)
        {
            return null;
        }
        DateTime cap = latestServerTimeUtc.Value.AddDays(ServerTimeGraceDays);
        return deviceNowUtc < cap ? deviceNowUtc : cap;
    }

    /// <summary>When an uploaded entry's local copy becomes eligible (server receipt + 30 days); null if not an uploaded entry with a receipt time.</summary>
    public static DateTime? LocalExpiryUtc(AssessmentUploadQueueEntry entry)
    {
        if (entry == null || entry.status != AssessmentUploadStatus.Uploaded) return null;
        return AssessmentUploadQueueStore.TryParseUtc(entry.uploadConfirmedAtUtc, out var confirmed) ? confirmed + Retention : null;
    }

    public static bool IsEligible(AssessmentUploadQueueEntry entry, DateTime trustedNowUtc, DateTime deviceNowUtc, Func<string, bool> isUploading)
    {
        if (entry == null || entry.status != AssessmentUploadStatus.Uploaded) return false;
        if (!AssessmentUploadQueueStore.TryParseUtc(entry.uploadConfirmedAtUtc, out var confirmed)) return false;
        if (confirmed > trustedNowUtc || trustedNowUtc - confirmed < Retention) return false;
        if (AssessmentUploadQueueStore.TryParseUtc(entry.uploadConfirmedLocalAtUtc, out var confirmedLocal)
            && deviceNowUtc - confirmedLocal < Retention)
        {
            return false;
        }
        if (isUploading != null && isUploading(entry.sessionId)) return false;
        return true;
    }

    /// <summary>One cleanup pass. Safe to call repeatedly; does nothing it can't fully justify.</summary>
    public static Result Run(AssessmentUploadQueueStore queue, SessionOwnershipStore owners, DateTime deviceNowUtc,
        DateTime? extraServerTimeUtc, Func<string, bool> isUploading, string rawLogDirectory = null)
    {
        var result = new Result();

        DateTime? latestServer = queue.LastKnownServerTimeUtc;
        if (extraServerTimeUtc != null && (latestServer == null || extraServerTimeUtc > latestServer))
        {
            latestServer = extraServerTimeUtc;
        }
        result.TrustedNowUtc = TrustedNow(deviceNowUtc, latestServer);
        if (result.TrustedNowUtc == null)
        {
            result.SkippedReason = "No server time known yet - nothing is removed until one is.";
            return result;
        }

        foreach (var entry in queue.LoadIndex().entries)
        {
            if (!IsEligible(entry, result.TrustedNowUtc.Value, deviceNowUtc, isUploading)) continue;
            RemoveLocalCopy(queue, owners, entry, rawLogDirectory, result);
        }
        return result;
    }

    /// <summary>
    /// Removes ONE uploaded session's local copy: queue entry + payload, its raw session log
    /// only when provably that session's (RawSessionLogs), then its owner record once no
    /// queue entry with that id remains. Refuses anything not Uploaded (RemoveUploadedEntry).
    /// Shared by the 30-day cleanup and the instructor's explicit legacy cleanup.
    /// </summary>
    public static bool RemoveLocalCopy(AssessmentUploadQueueStore queue, SessionOwnershipStore owners, AssessmentUploadQueueEntry entry,
        string rawLogDirectory, Result result)
    {
        if (!queue.RemoveUploadedEntry(entry.sessionId)) return false;
        result.RemovedSessionIds.Add(entry.sessionId);

        if (!string.IsNullOrEmpty(rawLogDirectory))
        {
            var proven = RawSessionLogs.FindProvenLogs(rawLogDirectory, entry.sessionId, out var unproven);
            foreach (var file in proven)
            {
                try
                {
                    System.IO.File.Delete(file);
                    result.RemovedRawLogFiles.Add(file);
                }
                catch (System.IO.IOException)
                {
                    result.RetainedRawLogFiles.Add(file);
                }
            }
            result.RetainedRawLogFiles.AddRange(unproven);
        }

        // Prune the owner only once nothing local refers to this session any more.
        bool stillQueued = queue.LoadIndex().entries.Exists(e => string.Equals(e.sessionId, entry.sessionId, StringComparison.OrdinalIgnoreCase));
        if (!stillQueued && owners != null)
        {
            try
            {
                if (owners.Remove(entry.sessionId))
                {
                    result.PrunedOwnerSessionIds.Add(entry.sessionId);
                }
            }
            catch (System.IO.InvalidDataException)
            {
                // Damaged owner file: leave it alone (SessionOwnershipStore refuses to guess).
            }
        }
        return true;
    }

    /// <summary>
    /// Explicit instructor action (never automatic): removes the local copies of LEGACY
    /// uploaded sessions - status Uploaded but no server receipt time, so no retention date
    /// can be computed. Only entries that are provably Uploaded are touched; Pending /
    /// RetryWaiting / NO_ASSIGNMENT / Uploading / review entries never are. Server records
    /// are not affected.
    /// </summary>
    public static Result ClearLegacyUploaded(AssessmentUploadQueueStore queue, SessionOwnershipStore owners, string rawLogDirectory, Func<string, bool> isUploading)
    {
        var result = new Result();
        foreach (var entry in queue.LoadIndex().entries)
        {
            if (!AssessmentUploadQueueView.IsLegacyUploaded(entry)) continue;
            if (isUploading != null && isUploading(entry.sessionId)) continue;
            RemoveLocalCopy(queue, owners, entry, rawLogDirectory, result);
        }
        return result;
    }
}

/// <summary>
/// Finds a session's raw log written by LocalJsonLogWriter, which names it
/// session_{sessionId:D}_{yyyyMMdd-HHmmss}.jsonl in persistentDataPath. A file counts as
/// that session's only when BOTH hold: the name's id segment parses to exactly that GUID,
/// AND the file's first event line carries that same sessionId. A name match alone is not
/// enough (no filename guessing); anything that fails the content check is reported as
/// unproven and kept.
/// </summary>
public static class RawSessionLogs
{
    public static List<string> FindProvenLogs(string directory, string sessionId, out List<string> unproven)
    {
        var proven = new List<string>();
        unproven = new List<string>();
        if (string.IsNullOrEmpty(directory) || !System.IO.Directory.Exists(directory) || !Guid.TryParse(sessionId, out var wanted))
        {
            return proven;
        }

        foreach (var path in System.IO.Directory.GetFiles(directory, "session_*.jsonl"))
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(path);   // session_<guid>_<stamp>
            if (name.Length < "session_".Length + 36) continue;
            if (!Guid.TryParse(name.Substring("session_".Length, 36), out var fromName) || fromName != wanted) continue;

            if (FirstEventSessionId(path) is string inFile && Guid.TryParse(inFile, out var fromContent) && fromContent == wanted)
            {
                proven.Add(path);
            }
            else
            {
                unproven.Add(path);
            }
        }
        return proven;
    }

    private static string FirstEventSessionId(string path)
    {
        try
        {
            using var reader = new System.IO.StreamReader(path);
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var evt = UnityEngine.JsonUtility.FromJson<SessionEvent>(line);
                return evt?.sessionId;
            }
        }
        catch (Exception)
        {
            // unreadable -> unproven
        }
        return null;
    }
}

/// <summary>
/// Read-only queue view for the future Instructor Management screen (Phase 5): how to
/// bucket AssessmentUploadService.GetAllEntries() and what each entry means. Derived from
/// the existing AssessmentUploadStatus/ErrorCategory - no second state machine.
/// </summary>
public static class AssessmentUploadQueueView
{
    public enum DisplayState
    {
        Pending,            // queued, not yet attempted (or about to be)
        Uploading,
        Uploaded,           // server-confirmed; local copy kept until LocalExpiryUtc
        RetryWaiting,       // retryable failure (offline, server error, device key, interrupted...)
        NoAssignment,       // retryable: waiting for an instructor to create the assignment
        AwaitingPairing,    // legacy pairing-path entry waiting for the same student to pair
        RequiresReview,     // Rejected / FailedPermanently - kept, never auto-deleted
        LegacyUploaded,     // Uploaded before Phase 4: no server receipt time -> no retention date, never auto-cleaned
    }

    /// <summary>Uploaded, but with no server receipt time (pre-Phase 4): retention date unknown. Never auto-expired.</summary>
    public static bool IsLegacyUploaded(AssessmentUploadQueueEntry e) =>
        e != null && e.status == AssessmentUploadStatus.Uploaded && !AssessmentUploadQueueStore.TryParseUtc(e.uploadConfirmedAtUtc, out _);

    public static DisplayState Classify(AssessmentUploadQueueEntry e) => e.status switch
    {
        AssessmentUploadStatus.Uploaded when IsLegacyUploaded(e) => DisplayState.LegacyUploaded,
        AssessmentUploadStatus.Uploaded => DisplayState.Uploaded,
        AssessmentUploadStatus.Uploading => DisplayState.Uploading,
        AssessmentUploadStatus.AwaitingPairing => DisplayState.AwaitingPairing,
        AssessmentUploadStatus.Rejected or AssessmentUploadStatus.FailedPermanently => DisplayState.RequiresReview,
        AssessmentUploadStatus.RetryWaiting when e.lastErrorCategory == AssessmentUploadErrorCategory.NoAssignment => DisplayState.NoAssignment,
        AssessmentUploadStatus.RetryWaiting => DisplayState.RetryWaiting,
        _ => DisplayState.Pending,
    };

    public class Summary
    {
        public int Pending, Uploading, Uploaded, RetryWaiting, NoAssignment, AwaitingPairing, RequiresReview, LegacyUploaded;
        /// <summary>Everything not yet safely on the server.</summary>
        public int NotYetUploaded => Pending + Uploading + RetryWaiting + NoAssignment + AwaitingPairing + RequiresReview;
    }

    public static Summary Summarize(IEnumerable<AssessmentUploadQueueEntry> entries)
    {
        var s = new Summary();
        foreach (var e in entries)
        {
            switch (Classify(e))
            {
                case DisplayState.Pending: s.Pending++; break;
                case DisplayState.Uploading: s.Uploading++; break;
                case DisplayState.Uploaded: s.Uploaded++; break;
                case DisplayState.RetryWaiting: s.RetryWaiting++; break;
                case DisplayState.NoAssignment: s.NoAssignment++; break;
                case DisplayState.AwaitingPairing: s.AwaitingPairing++; break;
                case DisplayState.LegacyUploaded: s.LegacyUploaded++; break;
                default: s.RequiresReview++; break;
            }
        }
        return s;
    }

    /// <summary>"Local copy expires in X days" - whole days left (0 = due), or null for anything not uploaded.</summary>
    public static int? DaysUntilLocalExpiry(AssessmentUploadQueueEntry e, DateTime nowUtc)
    {
        var expiry = UploadedSessionRetention.LocalExpiryUtc(e);
        if (expiry == null) return null;
        double days = (expiry.Value - nowUtc).TotalDays;
        return days <= 0 ? 0 : (int)Math.Ceiling(days);
    }

    /// <summary>
    /// The one retention line the UI shows for an entry - built only from
    /// UploadedSessionRetention, never recalculated: "Local backup expires in X days",
    /// "Local backup eligible for cleanup" (trustedNowUtc says it's due), "Retention date
    /// unavailable" (legacy), or null for anything not uploaded. trustedNowUtc is the
    /// cleanup's own capped clock (AssessmentUploadService.TrustedNowForRetention); when it
    /// is unknown, an overdue copy reads "eligible once the server time is confirmed".
    /// </summary>
    public static string RetentionLabel(AssessmentUploadQueueEntry e, DateTime deviceNowUtc, DateTime? trustedNowUtc)
    {
        if (IsLegacyUploaded(e)) return "Retention date unavailable";
        var expiry = UploadedSessionRetention.LocalExpiryUtc(e);
        if (expiry == null) return null;
        if (trustedNowUtc != null && UploadedSessionRetention.IsEligible(e, trustedNowUtc.Value, deviceNowUtc, null))
        {
            return "Local backup eligible for cleanup";
        }
        int days = DaysUntilLocalExpiry(e, deviceNowUtc) ?? 0;
        if (days <= 0)
        {
            return trustedNowUtc == null ? "Local backup eligible once server time is confirmed" : "Local backup kept (waiting for a trusted clock)";
        }
        return days == 1 ? "Local backup expires in 1 day" : $"Local backup expires in {days} days";
    }

    /// <summary>Instructor-facing upload state (Sessions tab).</summary>
    public static string StateLabel(AssessmentUploadQueueEntry e) => Classify(e) switch
    {
        DisplayState.Pending => "Pending",
        DisplayState.Uploading => "Uploading...",
        DisplayState.Uploaded => "Uploaded",
        DisplayState.LegacyUploaded => "Legacy Uploaded",
        DisplayState.NoAssignment => "Waiting for scenario assignment",
        DisplayState.AwaitingPairing => "Waiting for pairing (legacy)",
        DisplayState.RequiresReview => "Requires review",
        _ => e.lastErrorCategory == AssessmentUploadErrorCategory.Interrupted ? "Retry pending (interrupted)" : "Retry pending",
    };

    /// <summary>Plain-language explanation for the details view.</summary>
    public static string StateExplanation(AssessmentUploadQueueEntry e) => Classify(e) switch
    {
        DisplayState.NoAssignment => "The session is safely stored on this headset. Assign the scenario to this student, then retry the upload.",
        DisplayState.RetryWaiting => "The session is saved on this headset and will retry automatically. You can also retry now.",
        DisplayState.Pending => "The session is saved on this headset and waiting to upload.",
        DisplayState.Uploaded => "The server has confirmed this upload. A local backup is kept for 30 days.",
        DisplayState.LegacyUploaded => "Uploaded before upload dates were recorded. The local backup is kept until an instructor clears legacy backups.",
        DisplayState.AwaitingPairing => "Recorded with the older pairing system. It uploads when the same student pairs again.",
        DisplayState.RequiresReview => "The server refused this upload. The session is kept on this headset for review.",
        _ => null,
    };

    /// <summary>Retry is offered for anything not yet on the server except an in-flight upload.</summary>
    public static bool CanRetry(AssessmentUploadQueueEntry e) => Classify(e) is DisplayState.Pending or DisplayState.RetryWaiting
        or DisplayState.NoAssignment or DisplayState.RequiresReview;

    /// <summary>Owner shown for an entry: the VR owner's display snapshot, else the legacy paired name. Display only - the owner of record is SessionOwnershipStore / entry.ownerStudentId.</summary>
    public static string OwnerDisplay(AssessmentUploadQueueEntry e) =>
        !string.IsNullOrEmpty(e.ownerDisplayNameSnapshot) ? e.ownerDisplayNameSnapshot
        : !string.IsNullOrEmpty(e.pairedStudentDisplayName) ? e.pairedStudentDisplayName
        : e.studentIdForDisplay;
}
