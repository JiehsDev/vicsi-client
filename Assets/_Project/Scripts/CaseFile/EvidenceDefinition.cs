// Assets/_Project/Scripts/CaseFile/EvidenceDefinition.cs
using UnityEngine;

[CreateAssetMenu(fileName = "Evidence_", menuName = "VR-CSI/Evidence Definition")]
public class EvidenceDefinition : ScriptableObject
{
    [Tooltip("Unique ID used everywhere in code/database — must match across Unity and Supabase.")]
    public string evidenceId; // e.g. "EVD-014"

    public string displayName; // e.g. "Kitchen knife"

    [Tooltip("How much this item bears on the correct conclusion. Read by scoring AFTER the run — never enforced during play, since gating on relevance would tell the student which items matter. Defaults to Neutral: mark an item Critical/Distractor deliberately, don't inherit it.")]
    public EvidenceRelevance relevance = EvidenceRelevance.Neutral;

    [Tooltip("Does this item need fingerprint processing as part of its correct procedure? When true, Collected → Processed additionally requires the fingerprinting step (see EvidenceStateManager.MarkFingerprinted).")]
    public bool requiresFingerprinting;

    [TextArea]
    public string description;
}
