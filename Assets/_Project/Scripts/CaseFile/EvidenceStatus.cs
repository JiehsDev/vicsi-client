// Assets/_Project/Scripts/CaseFile/EvidenceStatus.cs
public enum EvidenceStatus
{
    NotFound,       // exists in scene, not yet interacted with
    Found,          // player has looked at/approached it
    Photographed,
    Sketched,
    Logged,
    ReadyForCollection, // all three above are done — Collector's gate opens here
    Collected,
    Processed       // e.g. fingerprint dusting complete, if applicable
}