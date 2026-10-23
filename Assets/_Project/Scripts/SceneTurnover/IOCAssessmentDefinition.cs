using System;
using System.Collections.Generic;
using UnityEngine;

public enum IOCObservationKind { Victim, PossibleEvidence, SceneSurvey }

[Serializable]
public class IOCObservationDefinition
{
    public string observationId;
    public IOCObservationKind kind;
    public string evidenceId;
    public string dialogueId;
    public Vector3 worldOffset;
}

/// <summary>Authored observation targets, not forensic evidence or a second state machine.</summary>
[CreateAssetMenu(menuName = "VR-CSI/IOC Assessment")]
public class IOCAssessmentDefinition : ScriptableObject
{
    public List<IOCObservationDefinition> observations = new();
    [Min(1)] public int minimumPossibleEvidence = 2;
    public Bounds surveyArea;
    public Vector3 surveyLookAt;
    [Min(1f)] public float observationDistance = 3.5f;
    [Range(1f, 20f)] public float gazeAngle = 8f;
    public IOCObservationDefinition Find(string id) => observations.Find(o => o.observationId == id);
}
