// Assets/_Project/Scripts/Insights/InsightRecord.cs

/// <summary>
/// Runtime tracking for one InsightDefinition, for one play session. Mirrors
/// FindingRecord/RelationshipRecord's own shape (a definition reference plus mutable
/// session state), kept as a plain class for the same reason those are - held by
/// reference in a Dictionary, mutated in place as the session progresses.
/// </summary>
public class InsightRecord
{
    public InsightDefinition definition;

    /// <summary>
    /// Once true, stays true for the rest of the session - same monotonic "once true,
    /// stays true" rule FindingRecord.isConfirmed already documents. Investigative
    /// reasoning the game has already offered doesn't un-happen because a contributing
    /// finding's live state later changed.
    /// </summary>
    public bool isUnlocked;

    /// <summary>
    /// The findingId whose confirmation triggered the RefreshInsights() call that
    /// actually unlocked this insight, or null if it unlocked on the initial pass (no
    /// specific finding "just arrived"). Lightweight tracking only - per this task's
    /// own explicit guidance ("correctness is more important than prose variation"),
    /// no order-specific text variant is authored or selected from this yet;
    /// InsightDefinition.description is always shown as-is. This field exists purely
    /// as the extension point a future pass could read to pick between authored text
    /// variants without needing any new plumbing - see InsightStateManager.RefreshInsights.
    /// </summary>
    public string triggeringFindingId;
}
