// Assets/_Project/Scripts/Logging/ProcedureLog.cs
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reusable entry point for SessionEventType.ProcedureOutOfOrder - an action the game
/// ALLOWED but that happened before its expected prerequisite. Exists because the
/// training analytics also need allowed-but-wrong-order steps alongside refused
/// (blocked) actions. Events and payloads are preserved. Under scoring 1.6.0 these
/// warnings never contribute to the deduction grade; older rules remain historical.
/// </summary>
public static class ProcedureLog
{
    /// <param name="procedureId">Stable id of the rule/action, e.g. "scene-photos-before-collection".</param>
    /// <param name="evidenceId">The evidence item involved, or null for a scene-level procedure.</param>
    /// <param name="expectedPrerequisite">What should have happened first (a phase, step id or short description).</param>
    /// <param name="context">Optional free-form detail, e.g. the attempted transition.</param>
    public static void LogOutOfOrder(string procedureId, string evidenceId = null, string expectedPrerequisite = null, string context = null)
    {
        if (SessionLogger.Instance == null || string.IsNullOrEmpty(procedureId))
        {
            return;
        }

        var payload = new Dictionary<string, string>
        {
            { "procedureId", procedureId },
            { "evidenceId", evidenceId ?? string.Empty },
            { "expectedPrerequisite", expectedPrerequisite ?? string.Empty },
            { "sessionTimeSeconds", Time.time.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) }
        };
        if (!string.IsNullOrEmpty(context))
        {
            payload["context"] = context;
        }

        // The event's own timestampMs/sequenceNumber come from SessionLogger as usual.
        SessionLogger.Instance.LogEvent(SessionEventType.ProcedureOutOfOrder, string.IsNullOrEmpty(evidenceId) ? procedureId : evidenceId, payload);
    }
}
