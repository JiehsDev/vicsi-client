// Assets/_Project/Scripts/Objectives/ObjectiveRecord.cs

/// <summary>Runtime tracking for one Objective, for one play session. Same shape as RelationshipRecord/EvidenceRecord - a definition reference plus mutable session state.</summary>
public class ObjectiveRecord
{
    public Objective definition;
    public ObjectiveState state = ObjectiveState.Hidden;
}
