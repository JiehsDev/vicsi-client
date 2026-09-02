// Assets/_Project/Scripts/Logging/SessionEvent.cs
using System;
using System.Collections.Generic;

/// <summary>
/// One key/value pair in a SessionEvent's payload. Exists only because Unity's
/// JsonUtility (what LocalJsonLogWriter serializes with) cannot serialize
/// Dictionary&lt;string,string&gt; at all - a List of this plain struct is the
/// JsonUtility-compatible equivalent. SessionLogger.LogEvent still takes a normal
/// Dictionary&lt;string,string&gt; at the call site and converts it internally;
/// nothing outside this Logging folder needs to know about this struct.
/// </summary>
[Serializable]
public struct PayloadEntry
{
    public string key;
    public string value;
}

/// <summary>
/// One recorded occurrence during a play session. payload is intentionally a generic
/// string-keyed list rather than a typed field per event type - event types are
/// heterogeneous enough (a PhotoTaken event needs shot type + whether scale was in
/// frame, a HypothesisSubmitted event needs free text) that a single rigid struct
/// would end up with dozens of mostly-null fields. Callers pass whatever keys make
/// sense for their event type; nothing here validates payload shape per type.
///
/// eventType is deliberately a string (SessionEventType.ToString()), not the enum
/// itself: JsonUtility serializes an enum field as its raw underlying int, and a
/// written-to-disk log file outlives the enum's declaration - inserting or
/// reordering a SessionEventType value later would silently change what every past
/// int in every past log file means, the exact class of bug just found and fixed
/// twice already this session (EvidenceStatus's RequiredSequence, and the old RoleId
/// enum). Appending new SessionEventType values at the end is still always safe;
/// this just removes the ordinal from the persisted format entirely so it can never
/// matter. No reader parses these files back yet (grepped - only JsonUtility.ToJson
/// exists in the project, no FromJson); whoever builds that should
/// Enum.Parse&lt;SessionEventType&gt;(string) this field, not cast an int.
/// </summary>
[Serializable]
public class SessionEvent
{
    public string sessionId;
    public int sequenceNumber;
    public long timestampMs;
    public string eventType;
    public string targetId;
    public List<PayloadEntry> payload;
}
