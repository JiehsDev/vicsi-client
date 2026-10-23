/// <summary>Required follow-up is separate from modal UI ownership, so navigation and cleanup remain usable.</summary>
public static class ProcedureInterlock
{
    private static bool Active => SceneTurnover.IsGated && (SceneTurnover.Phase == ScenarioPhase.SOCOInvestigation || SceneTurnover.Phase == ScenarioPhase.SOCOCloseout);
    public static bool GloveChangeRequired => Active && ArrivalPPEStation.Instance != null
        && !ArrivalPPEStation.Instance.HasCleanGloves && ArrivalPPEStation.Instance.LastSampleSource != null;
    public static string PendingFingerprintItemId
    {
        get
        {
            var inventory = EvidenceInventory.Instance;
            if (!Active || inventory == null) return null;
            return inventory.PendingFingerprintItemId;
        }
    }
    public static bool BlocksUnrelatedActions => GloveChangeRequired || PendingFingerprintItemId != null;
    public static string Reason => GloveChangeRequired ? (ArrivalPPEStation.Instance.CurrentSampleStored
        ? "Dispose of the used gloves and put on a fresh pair at the supply table before continuing."
        : "Finish drying, packaging, sealing and storing the current blood evidence, then replace your used gloves at the supply table.")
        : PendingFingerprintItemId == null ? null : EvidenceStateManager.Instance.IsFingerprintMatched(EvidenceInventory.Instance.GetSampleSource(PendingFingerprintItemId))
            ? "Press B to store the matched fingerprint card before continuing."
            : "Take the held fingerprint card to the examination desk and confirm a matching reference before storing it or continuing.";
    public static bool AllowsBloodCompletion(string sourceId) => GloveChangeRequired && sourceId != null
        && sourceId == ArrivalPPEStation.Instance.LastSampleSource && !ArrivalPPEStation.Instance.CurrentSampleStored;
    public static bool AllowsTool(ToolType role) => !BlocksUnrelatedActions
        || (PendingFingerprintItemId == null && role == ToolType.Swab && SwabTool.Instance != null && SwabTool.Instance.HasUnfinishedSample);
    public static bool AllowsHandling(string itemId)
    {
        if (!BlocksUnrelatedActions) return true;
        if (!GloveChangeRequired && itemId == PendingFingerprintItemId) return true;
        var inventory = EvidenceInventory.Instance;
        return inventory != null && !inventory.IsFingerprintSample(itemId)
            && AllowsBloodCompletion(inventory.GetSampleSource(itemId) ?? itemId);
    }
    public static bool AllowsStatus(string sourceId, EvidenceStatus target) => !BlocksUnrelatedActions || target == EvidenceStatus.Found
        || (AllowsBloodCompletion(sourceId) && (target == EvidenceStatus.Collected || target == EvidenceStatus.Sealed || target == EvidenceStatus.Processed));
    public static bool AllowsFingerprintWork(string sourceId) => !GloveChangeRequired
        && (PendingFingerprintItemId == null || EvidenceInventory.Instance.GetSampleSource(PendingFingerprintItemId) == sourceId);
}
