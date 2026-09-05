// Assets/_Project/Scripts/Interaction/EvidenceGrabGate.cs
using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;

/// <summary>
/// Keeps an evidence item's physical grab (HandGrabInteractable/GrabInteractable)
/// disabled until it is genuinely ReadyForCollection - consistent with every other
/// action in this project waiting until the moment it's procedurally valid, rather
/// than being freely available and only checked after the fact. Before that point the
/// right hand should not be able to pick this item up at all: collection is meant to
/// happen only once photographing, sketching and logging are done, not whenever a
/// player notices they can grab it.
///
/// Sits alongside EvidenceProp on the same GameObject, which must also carry
/// Grabbable/HandGrabInteractable/GrabInteractable (added once per evidence prop for
/// the two-handed bagging redesign - see EvidenceBagTool, which disables these again
/// once an item is actually bagged, since a bagged item shouldn't be independently
/// re-grabbable either).
/// </summary>
[RequireComponent(typeof(EvidenceProp))]
public class EvidenceGrabGate : MonoBehaviour
{
    private EvidenceProp prop;
    private HandGrabInteractable handGrabInteractable;
    private GrabInteractable grabInteractable;

    private void Awake()
    {
        prop = GetComponent<EvidenceProp>();
        handGrabInteractable = GetComponent<HandGrabInteractable>();
        grabInteractable = GetComponent<GrabInteractable>();
        SetGrabEnabled(false);
    }

    private void OnEnable()
    {
        EvidenceStateManager.OnEvidenceStatusChanged += HandleStatusChanged;
    }

    private void OnDisable()
    {
        EvidenceStateManager.OnEvidenceStatusChanged -= HandleStatusChanged;
    }

    private void HandleStatusChanged(string evidenceId, EvidenceStatus status)
    {
        if (evidenceId == prop.evidenceId && status == EvidenceStatus.ReadyForCollection)
        {
            SetGrabEnabled(true);
        }
    }

    private void SetGrabEnabled(bool value)
    {
        if (handGrabInteractable != null)
        {
            handGrabInteractable.enabled = value;
        }

        if (grabInteractable != null)
        {
            grabInteractable.enabled = value;
        }
    }
}
