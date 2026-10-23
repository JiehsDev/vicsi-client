// Assets/_Project/Scripts/Logging/LocalJsonLogWriter.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Default ISessionLogWriter: appends one JSON object per line (newline-delimited
/// JSON / JSONL) to a file under Application.persistentDataPath, one file per
/// session. JSONL instead of a single wrapped JSON array deliberately: keeping a
/// literal JSON array valid on disk while appending means either rewriting the whole
/// file every flush or juggling the closing bracket, whereas each JSONL line is
/// independently valid the instant it's written - a crash mid-session still leaves
/// every prior event safely readable, which matters given this runs on standalone
/// Quest hardware with no guaranteed clean shutdown.
///
/// A MonoBehaviour (not a plain class) specifically so SessionLogger can hold it as
/// an Inspector-assignable [SerializeField] reference - Unity can't serialize a bare
/// interface field or a plain C# object reference in the Inspector. Swapping in a
/// future SupabaseLogWriter just means dropping a different MonoBehaviour
/// implementing ISessionLogWriter into that same field; no code changes elsewhere.
/// </summary>
public class LocalJsonLogWriter : MonoBehaviour, ISessionLogWriter
{
    private string filePath;
    private readonly List<string> pendingLines = new();
    private bool initialized;

    public string FilePath => filePath;
    public int PendingEventCount => pendingLines.Count;
    public bool LastFlushSucceeded { get; private set; } = true;
    public bool RecoveryRequired { get; private set; }

    /// <summary>Must be called once (by SessionLogger, right after generating the session ID) before WriteEvent/Flush do anything.</summary>
    public void Initialize(string sessionId)
    {
        if (initialized) throw new InvalidOperationException("A log writer can only be initialized once per session.");
        if (!Guid.TryParse(sessionId, out var parsedId)) throw new ArgumentException("Session ID must be a GUID.", nameof(sessionId));
        string timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        string fileName = $"session_{parsedId:D}_{timestamp}.jsonl";
        filePath = Path.Combine(Application.persistentDataPath, fileName);
        initialized = true;
    }

    /// <summary>
    /// Used ONLY by AutomatedTestSandbox: moves this session's log (flushed first) into
    /// another folder and keeps appending there, so a test-driven Play session leaves no
    /// raw log in persistentDataPath. Same file name, same content; never called in
    /// normal play.
    /// </summary>
    public void RelocateTo(string directory)
    {
        if (!initialized || string.IsNullOrEmpty(directory)) return;
        Flush();
        Directory.CreateDirectory(directory);
        string newPath = Path.Combine(directory, Path.GetFileName(filePath));
        if (string.Equals(Path.GetFullPath(newPath), Path.GetFullPath(filePath), StringComparison.OrdinalIgnoreCase)) return;
        if (File.Exists(filePath))
        {
            File.Move(filePath, newPath);
        }
        filePath = newPath;
    }

    public void WriteEvent(SessionEvent evt)
    {
        if (!initialized)
        {
            Debug.LogWarning("[LocalJsonLogWriter] WriteEvent called before Initialize(sessionId); event dropped.");
            return;
        }

        pendingLines.Add(JsonUtility.ToJson(evt));
    }

    public void Flush()
    {
        if (!initialized || pendingLines.Count == 0 || RecoveryRequired)
        {
            return;
        }

        FileStream stream = null;
        long originalLength = 0;
        try
        {
            stream = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
            originalLength = stream.Length;
            stream.Position = originalLength;
            // Encode before writing so each batch has a known byte representation.
            byte[] bytes = Encoding.UTF8.GetBytes(string.Join("\n", pendingLines) + "\n");
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush(true);
            pendingLines.Clear();
            LastFlushSucceeded = true;
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            LastFlushSucceeded = false;
            // Roll back a partially appended batch before retrying, preserving the
            // already persisted prefix. Abrupt process/power loss still requires
            // JSONL tail recovery in the future synchronization reader.
            if (stream != null)
            {
                try { stream.SetLength(originalLength); stream.Flush(true); }
                catch (IOException)
                {
                    RecoveryRequired = true;
                    Debug.LogError("[LocalJsonLogWriter] Could not roll back partial log write; automatic retries stopped. Recovery is required before upload.");
                }
            }
            Debug.LogWarning($"[LocalJsonLogWriter] Flush failed ({ex.GetType().Name}); {pendingLines.Count} events retained for retry.");
        }
        finally { stream?.Dispose(); }
    }
}
