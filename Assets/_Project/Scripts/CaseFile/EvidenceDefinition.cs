// Assets/_Project/Scripts/CaseFile/EvidenceDefinition.cs
using UnityEngine;

[CreateAssetMenu(fileName = "Evidence_", menuName = "VR-CSI/Evidence Definition")]
public class EvidenceDefinition : ScriptableObject
{
    [Tooltip("Unique ID used everywhere in code/database — must match across Unity and Supabase.")]
    public string evidenceId; // e.g. "EVD-014"

    public string displayName; // e.g. "Kitchen knife"

    [Tooltip("Does this item need fingerprint processing as part of its correct procedure?")]
    public bool requiresFingerprinting;

    [TextArea]
    public string description;
}