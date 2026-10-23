// Assets/_Project/Scripts/Assessment/AssessmentUploadQueueStore.cs
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Pure file-backed store for the local pending-upload queue - a plain class, not a
/// MonoBehaviour, so both AssessmentUploadService (at runtime) and the Editor tool
/// (Phase 10, outside Play mode) can use the identical read/write logic against the
/// same files without needing a scene or a running Player.
///
/// Two kinds of file, deliberately separate:
///   - ONE index file (queue_index.json) - the list of AssessmentUploadQueueEntry rows,
///     small, rewritten in full on every change.
///   - ONE payload file PER SESSION (payloads/&lt;sessionId&gt;.json) - the actual
///     AssessmentSessionUploadDto JSON, written once and never rewritten.
/// Splitting them keeps the frequently-rewritten index small (rewriting a payload's own
/// multi-KB JSON on every status transition would be wasteful and would widen the
/// crash-corruption window for no reason), and keeps a payload immutable once queued -
/// exactly what "never delete the original session log after an upload failure" and
/// "prevent duplicate uploads" need: the payload that failed is the exact same payload
/// bytes a retry re-sends.
///
/// Every write (index AND payload) goes through WriteAtomic: write to a sibling ".tmp"
/// file, flush, then File.Replace (or Move, if the destination doesn't exist yet) onto
/// the real path. A crash or power loss mid-write leaves either the old file intact or
/// the new one fully written - never a half-written file in the real path. This is the
/// same "durability over convenience" discipline LocalJsonLogWriter already applies to
/// the session log itself, adapted to a single-file-replace shape instead of an
/// append-only one (the queue index is small and rewritten wholesale, not appended to).
/// </summary>
public class AssessmentUploadQueueStore
{
    private const string IndexFileName = "assessment_upload_queue_index.json";
    private const string PayloadFolderName = "assessment_upload_payloads";

    private readonly string rootDirectory;
    private readonly string indexPath;
    private readonly string payloadDirectory;

    public AssessmentUploadQueueStore(string rootDirectory = null)
    {
        this.rootDirectory = string.IsNullOrEmpty(rootDirectory) ? Application.persistentDataPath : rootDirectory;
        indexPath = Path.Combine(this.rootDirectory, IndexFileName);
        payloadDirectory = Path.Combine(this.rootDirectory, PayloadFolderName);
        Directory.CreateDirectory(payloadDirectory);
    }

    public string PayloadDirectory => payloadDirectory;

    /// <summary>
    /// Loads the current index. A missing file is a fresh empty queue, not an error - the
    /// normal state the very first time the app ever runs. A file that exists but fails
    /// to parse (disk corruption rather than a torn write - WriteAtomic itself prevents
    /// the torn-write case) is logged and treated as empty rather than throwing, so a
    /// corrupted index degrades to "lost queue bookkeeping", never "app won't start."
    /// </summary>
    public AssessmentUploadQueueIndex LoadIndex()
    {
        if (!File.Exists(indexPath))
        {
            return new AssessmentUploadQueueIndex();
        }

        try
        {
            string json = File.ReadAllText(indexPath);
            var index = JsonUtility.FromJson<AssessmentUploadQueueIndex>(json);
            return index ?? new AssessmentUploadQueueIndex();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AssessmentUploadQueueStore] Failed to read queue index ({ex.GetType().Name}); starting from an empty queue. Original file left on disk at {indexPath} for inspection.");
            return new AssessmentUploadQueueIndex();
        }
    }

    public void SaveIndex(AssessmentUploadQueueIndex index)
    {
        string json = JsonUtility.ToJson(index, prettyPrint: true);
        WriteAtomic(indexPath, json);
    }

    /// <summary>Convenience: load, mutate one entry (by sessionId) via the callback, save. Returns false if no matching entry was found (mutator not called).</summary>
    public bool UpdateEntry(string sessionId, Action<AssessmentUploadQueueEntry> mutate)
    {
        var index = LoadIndex();
        var entry = index.entries.Find(e => e.sessionId == sessionId);
        if (entry == null)
        {
            return false;
        }

        mutate(entry);
        SaveIndex(index);
        return true;
    }

    /// <summary>Appends a new entry. Refuses a duplicate sessionId - see the class comment on "prevent duplicate uploads."</summary>
    public bool TryAddEntry(AssessmentUploadQueueEntry entry)
    {
        var index = LoadIndex();
        if (index.entries.Exists(e => e.sessionId == entry.sessionId))
        {
            return false;
        }

        index.entries.Add(entry);
        SaveIndex(index);
        return true;
    }

    public string GetPayloadPath(string sessionId) => Path.Combine(payloadDirectory, sessionId + ".json");

    /// <summary>Writes the payload JSON for sessionId. Called once, before the entry is ever added to the index - "save the complete session locally first" / "generate and save the upload payload" precede any network attempt.</summary>
    public string WritePayload(string sessionId, string payloadJson)
    {
        string path = GetPayloadPath(sessionId);
        WriteAtomic(path, payloadJson);
        return path;
    }

    public string ReadPayload(string payloadPath)
    {
        return File.Exists(payloadPath) ? File.ReadAllText(payloadPath) : null;
    }

    /// <summary>
    /// Removes only entries whose status is Uploaded - used by the Editor tool's
    /// "clear only completed test queue records" action. Never touches Pending/
    /// Uploading/RetryWaiting/Rejected/FailedPermanently rows, and never deletes the
    /// underlying LocalJsonLogWriter session log regardless of which rows are cleared -
    /// this only removes queue bookkeeping and this subsystem's own copy of the
    /// payload, never the original session's source-of-truth log.
    /// </summary>
    public int ClearCompletedEntries()
    {
        var index = LoadIndex();
        int removed = index.entries.RemoveAll(e =>
        {
            if (e.status != AssessmentUploadStatus.Uploaded)
            {
                return false;
            }
            TryDeleteFile(e.localPayloadPath);
            return true;
        });

        if (removed > 0)
        {
            SaveIndex(index);
        }
        return removed;
    }

    /// <summary>
    /// Launch-time recovery: an entry still marked Uploading was cut off by the app closing,
    /// so whether the server stored it is unknown. Move it to RetryWaiting (category
    /// Interrupted) - same sessionId, same owner, same payload - so the normal retry path
    /// re-sends it; the server's sessionId + payload-hash idempotency turns an
    /// already-stored session into a harmless duplicate. Call only when no upload is in
    /// flight in this process - pass isInFlight so a genuinely running attempt is left
    /// alone (OnEnable can also run after a domain reload). Returns how many were recovered.
    /// </summary>
    public int RecoverInterruptedUploads(Func<string, bool> isInFlight = null)
    {
        var index = LoadIndex();
        int recovered = 0;
        foreach (var entry in index.entries)
        {
            if (entry.status == AssessmentUploadStatus.Uploading && (isInFlight == null || !isInFlight(entry.sessionId)))
            {
                entry.status = AssessmentUploadStatus.RetryWaiting;
                entry.lastErrorCategory = AssessmentUploadErrorCategory.Interrupted;
                entry.lastErrorMessage = "The last upload attempt was interrupted. It will be retried.";
                entry.nextAttemptAtUtc = null;
                recovered++;
            }
        }
        if (recovered > 0)
        {
            SaveIndex(index);
        }
        return recovered;
    }

    /// <summary>
    /// Removes ONE server-confirmed uploaded entry and its payload copy (30-day retention
    /// cleanup - see UploadedSessionRetention). Refuses anything not Uploaded. The payload
    /// file is deleted only if no other entry points at the same path. Never touches the
    /// raw session log or anything on the server.
    /// </summary>
    public bool RemoveUploadedEntry(string sessionId)
    {
        var index = LoadIndex();
        var entry = index.entries.Find(e => e.sessionId == sessionId);
        if (entry == null || entry.status != AssessmentUploadStatus.Uploaded)
        {
            return false;
        }
        index.entries.Remove(entry);
        SaveIndex(index);
        if (!index.entries.Exists(e => e.localPayloadPath == entry.localPayloadPath))
        {
            TryDeleteFile(entry.localPayloadPath);
        }
        return true;
    }

    /// <summary>Remembers the latest server time seen (never moves backwards).</summary>
    public void RecordServerTime(DateTime serverUtc)
    {
        var index = LoadIndex();
        if (TryParseUtc(index.lastKnownServerTimeUtc, out var known) && known >= serverUtc)
        {
            return;
        }
        index.lastKnownServerTimeUtc = serverUtc.ToUniversalTime().ToString("O");
        SaveIndex(index);
    }

    public DateTime? LastKnownServerTimeUtc => TryParseUtc(LoadIndex().lastKnownServerTimeUtc, out var t) ? t : null;

    internal static bool TryParseUtc(string value, out DateTime utc)
    {
        utc = default;
        if (string.IsNullOrEmpty(value)) return false;
        if (!DateTime.TryParse(value, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out utc))
        {
            return false;
        }
        utc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        return true;
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup only - a leftover payload file is harmless disk usage,
            // not a correctness problem, so a locked/in-use file here is not fatal.
        }
    }

    /// <summary>Also used by StudentDirectoryStore - same crash-safe single-file replace.</summary>
    internal static void WriteAtomic(string path, string contents)
    {
        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string tempPath = path + ".tmp";
        File.WriteAllText(tempPath, contents);

        // A just-written file can be held for a few milliseconds by antivirus or the
        // search indexer (seen on Windows: "Unable to remove the file to be replaced").
        // Retry the replace briefly instead of letting one transient lock abort the
        // caller; the old file stays intact until a replace succeeds, so a final failure
        // still loses nothing and is rethrown.
        const int attempts = 5;
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                if (File.Exists(path))
                {
                    // File.Replace is atomic on the same volume and, unlike delete-then-move,
                    // leaves no window where `path` briefly doesn't exist at all.
                    File.Replace(tempPath, path, destinationBackupFileName: null);
                }
                else
                {
                    File.Move(tempPath, path);
                }
                return;
            }
            catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException) && attempt < attempts)
            {
                System.Threading.Thread.Sleep(15 * attempt);
            }
        }
    }
}
