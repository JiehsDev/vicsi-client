// Assets/_Project/Scripts/CaseFile/EvidenceNoteComposer.cs
using System.Collections.Generic;
using System.Text;

/// <summary>
/// Read-only formatter for the player-facing evidence detail shown by the Notes /
/// Deduction Board. It never reads EvidenceRelevance, relationship definitions,
/// scoring, conclusions or endings. EvidenceStateManager state and RequirementNode
/// gates decide what the player has earned; EvidenceDefinition supplies the wording.
/// </summary>
public static class EvidenceNoteComposer
{
    public static string Compose(EvidenceRecord record)
    {
        if (record == null || record.definition == null || EvidenceStateManager.Instance == null
            || !EvidenceStateManager.Instance.IsAtOrPastStatus(record.definition.evidenceId, EvidenceStatus.Found))
        {
            return string.Empty;
        }

        var definition = record.definition;
        var note = new StringBuilder(768);
        note.Append("<b>").Append(definition.PlayerDisplayName).Append("</b>");

        AppendSection(note, "Description", definition.description);
        AppendSection(note, "Found location", definition.foundLocation);
        AppendSection(note, "Observed condition", definition.observedCondition);
        AppendSection(note, "Procedures / status", BuildProcedureSummary(record));
        if (record.fingerprintLabStatus == FingerprintLabStatus.Matched && record.fingerprintLabMatchIdentities.Count > 0)
            AppendSection(note, "Fingerprint identification", "Recovered prints identified as: " + string.Join(", ",record.fingerprintLabMatchIdentities) + ". Identification does not establish when the item was handled.");
        if (definition.requiresBodyRemoval && record.bodyRemovalStatus != BodyRemovalStatus.NotPrepared)
            AppendSection(note, "Body removal", record.bodyRemovalStatus == BodyRemovalStatus.Removed
                ? "Identified, wrapped, placed in a cadaver bag and prepared for transport for medico-legal examination. Removed from the scene."
                : "Prepared for transport for medico-legal examination.");

        if (definition.progressiveFindings != null)
        {
            var findings = new StringBuilder();
            foreach (var finding in definition.progressiveFindings)
            {
                if (finding == null || string.IsNullOrWhiteSpace(finding.text)
                    || !RequirementEvaluator.IsSatisfied(finding.revealRequirement))
                {
                    continue;
                }

                if (findings.Length > 0)
                {
                    findings.Append('\n');
                }
                findings.Append("• ").Append(finding.text.Trim());
            }
            AppendSection(note, "Confirmed findings", findings.ToString());
        }

        AppendSection(note, "Investigative context", definition.investigativeContext);

        // The record's spatial detail, built from the live measurement state each time the
        // note is viewed: the Record action itself is never repeated, but distances to
        // evidence discovered and measured after this item was logged appear here too.
        // Only completed measurements are listed, and a pair can only be completed once
        // both items are discovered, so nothing undiscovered is ever named.
        if ((record.bodyFindingsRecorded || Reached(record, EvidenceStatus.Logged)) && MeasurementStateManager.Instance != null)
        {
            AppendSection(note, "Spatial measurements", MeasurementStateManager.Instance.FormatSpatialSummary(definition.evidenceId));
        }
        return EvidenceDisplay.Text(note.ToString());
    }

    private static string BuildProcedureSummary(EvidenceRecord record)
    {
        var procedures = new List<string>(12) { "Discovered" };

        if (Reached(record, EvidenceStatus.PhotographedRaw))
            procedures.Add("Photographed");
        if (Reached(record, EvidenceStatus.ChalkMarked) && (record.definition == null || record.definition.requiresChalk))
            procedures.Add("Position outlined");
        if (Reached(record, EvidenceStatus.Marked))
            procedures.Add("Evidence marker placed");
        if (record.swabbingDone)
            procedures.Add("Swab sample collected");
        if (record.isFlipped)
            procedures.Add("Additional surface examined");
        if (record.fingerprintingDone)
            procedures.Add("Fingerprint processed");
        if (record.bodyFindingsRecorded || Reached(record, EvidenceStatus.Logged))
            procedures.Add("Logged");
        if (Reached(record, EvidenceStatus.Collected))
            procedures.Add("Collected");
        if (Reached(record, EvidenceStatus.Sealed))
            procedures.Add("Sealed");
        if (EvidenceStateManager.Instance.IsFingerprintLabStatusAtOrAbove(record.definition.evidenceId, FingerprintLabStatus.Matched))
            procedures.Add("Fingerprint comparison completed");
        else if (EvidenceStateManager.Instance.IsFingerprintLabStatusAtOrAbove(record.definition.evidenceId, FingerprintLabStatus.SentToLab))
            procedures.Add("Fingerprint submitted");
        if (Reached(record, EvidenceStatus.Processed))
            procedures.Add("Processing complete");

        var summary = new StringBuilder(256);
        for (int i = 0; i < procedures.Count; i += 2)
        {
            if (summary.Length > 0)
            {
                summary.Append('\n');
            }

            summary.Append("✓ ").Append(procedures[i]);
            if (i + 1 < procedures.Count)
            {
                summary.Append("    ✓ ").Append(procedures[i + 1]);
            }
        }

        return summary.ToString();
    }

    private static bool Reached(EvidenceRecord record, EvidenceStatus status)
    {
        return EvidenceStateManager.Instance.IsAtOrPastStatus(record.definition.evidenceId, status);
    }

    private static void AppendSection(StringBuilder note, string heading, string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return;
        }

        note.Append("\n<line-height=50%>\n</line-height><size=78%><b>").Append(heading).Append("</b>\n")
            .Append(body.Trim()).Append("</size>");
    }
}
