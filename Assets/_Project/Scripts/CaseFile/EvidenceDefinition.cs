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

    [Tooltip("Radius, in metres, of this item's interaction sphere. Governs BOTH the proximity at which the player marks it Found AND how close a placed evidence tent must land for the tent to count as marking this item. Deliberately one number, not two: they answer the same question — how big is this thing's presence in the scene — and letting them drift apart would mean an item you can notice but cannot successfully tent. Tune per item: a body or a spatter pattern occupies more scene than a knife.")]
    [Min(0.05f)]
    public float interactionRadius = 1.5f;

    [Tooltip("Does this item need fingerprint processing as part of its correct procedure? When true, Collected → Processed additionally requires the fingerprinting step (see EvidenceStateManager.MarkFingerprinted).")]
    public bool requiresFingerprinting;

    [TextArea]
    public string description;
}
