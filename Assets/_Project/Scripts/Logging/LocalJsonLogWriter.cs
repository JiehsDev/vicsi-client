// Assets/_Project/Scripts/Logging/LocalJsonLogWriter.cs
using System;
using System.Collections.Generic;
using System.IO;
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

    /// <summary>Must be called once (by SessionLogger, right after generating the session ID) before WriteEvent/Flush do anything.</summary>
    public void Initialize(string sessionId)
    {
        string timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        string fileName = $"session_{sessionId}_{timestamp}.jsonl";
        filePath = Path.Combine(Application.persistentDataPath, fileName);
        initialized = true;
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
        if (!initialized || pendingLines.Count == 0)
        {
            return;
        }

        File.AppendAllLines(filePath, pendingLines);
        pendingLines.Clear();
    }
}
