// Assets/_Project/Scripts/CaseFile/EvidenceProp.cs
using UnityEngine;

/// <summary>
/// Marks a physical scene object as a piece of evidence so interaction tools
/// (camera, sketchpad, etc.) can identify what they're aiming at.
/// </summary>
public class EvidenceProp : MonoBehaviour
{
    [Tooltip("Must match an EvidenceDefinition.evidenceId registered in EvidenceStateManager.")]
    public string evidenceId;
}
