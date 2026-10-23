// Assets/_Project/Scripts/Ending/EndingResolution.cs

/// <summary>Which rule selected an ending - logged alongside EndingSelected so an instructor can tell a genuine match from a fallback.</summary>
public enum EndingResolutionRule
{
    None = 0,
    PriorityMatch = 1,
    Fallback = 2,
    NoneAvailable = 3
}

/// <summary>Result of EndingManager.ResolveEnding - which ending was selected, and by which rule.</summary>
public class EndingResolution
{
    public EndingDefinition Ending;
    public EndingResolutionRule Rule;
}
