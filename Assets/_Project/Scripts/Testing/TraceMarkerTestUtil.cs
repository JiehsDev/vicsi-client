// Assets/_Project/Scripts/Testing/TraceMarkerTestUtil.cs

/// <summary>
/// The knife takes two tents - one for the knife, one for the blood on its blade
/// (EvidenceDefinition.requiresTraceMarker) - and its final photograph is refused until both
/// are down. Tests that drive an item to PhotographedFinal call this first; it places the
/// trace's tent through the same state call EvidenceTentTool makes (the tool-driven path is
/// covered by EvidenceTraceMarkerTest). No-op for every other item or state.
/// </summary>
public static class TraceMarkerTestUtil
{
    public static void MarkTraceIfRequired(string evidenceId)
    {
        var esm = EvidenceStateManager.Instance;
        var record = esm != null ? esm.GetRecord(evidenceId) : null;
        if (record == null || record.definition == null || !record.definition.requiresTraceMarker
            || record.traceTentNumber.HasValue || record.status != EvidenceStatus.Marked)
        {
            return;
        }
        esm.MarkTraceTented(evidenceId, ToolType.EvidenceMarker, (record.tentNumber ?? 0) + 1);
    }
}
