// Assets/_Project/Scripts/CaseFile/EvidenceProp.cs
using UnityEngine;

/// <summary>
/// Marks a physical scene object as a piece of evidence so interaction tools
/// (camera, sketchpad, etc.) can identify what they're aiming at. Also owns
/// EvidenceStatus.Found detection via a runtime-created trigger collider, sized
/// independently of whatever solid collider(s) this prop already has for aim
/// raycasts - every tool raycast already passes QueryTriggerInteraction.Ignore, so
/// this trigger never interferes with them. "Found" deliberately lives here, not on
/// any individual tool script: it means "the player has noticed this exists," and
/// must not depend on which tool happens to be equipped, or legitimate workflows
/// where evidence is encountered before the relevant tool is drawn would get blocked
/// by EvidenceStateManager's sequence gate. A trigger collider is used instead of a
/// per-frame proximity check (the pattern EvidenceTentPickup/AtticLadderLever use)
/// since it's event-driven rather than polled every frame - cheaper on Quest. No
/// duplicate-call guard here by design: EvidenceStateManager.SetStatus already no-ops
/// a repeat/backward MarkFound call against the sequence, so re-entering the trigger
/// after Found is already harmless.
/// </summary>
public class EvidenceProp : MonoBehaviour
{
    [Tooltip("Must match an EvidenceDefinition.evidenceId registered in EvidenceStateManager.")]
    public string evidenceId;

    [Tooltip("How close the player has to get before this item is marked Found.")]
    [SerializeField] private float noticeRadius = 1.5f;

    private void Start()
    {
        var noticeCollider = gameObject.AddComponent<SphereCollider>();
        noticeCollider.isTrigger = true;
        noticeCollider.radius = noticeRadius;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player") || string.IsNullOrEmpty(evidenceId) || EvidenceStateManager.Instance == null)
        {
            return;
        }

        EvidenceStateManager.Instance.MarkFound(evidenceId, ToolType.None);
    }
}
