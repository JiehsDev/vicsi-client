// Assets/_Project/Scripts/CaseFile/EvidenceRecord.cs
using System;

[Serializable]
public class EvidenceRecord
{
    public EvidenceDefinition definition;
    public EvidenceStatus status = EvidenceStatus.NotFound;
    public ToolType lastToolUsed = ToolType.None;
    public float statusChangedAtTime; // Time.time when last transitioned, for time-on-task logging later
}