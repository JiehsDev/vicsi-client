// Assets/_Project/Scripts/Logging/ISessionLogWriter.cs

/// <summary>
/// Swappable backend for SessionLogger. LocalJsonLogWriter is the only
/// implementation today; a future SupabaseLogWriter (Phase 8, per
/// ProceduralGateValidator's own doc comment referencing the evidence_events table)
/// drops in behind this interface without any call site elsewhere in the project
/// needing to change - SessionLogger only ever talks to ISessionLogWriter, never a
/// concrete writer type. Not built in this pass; this interface is the seam.
/// </summary>
public interface ISessionLogWriter
{
    void WriteEvent(SessionEvent evt);
    void Flush();
}
