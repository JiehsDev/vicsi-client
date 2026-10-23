// Assets/_Project/Scripts/Narrative/NarrativeEventSet.cs
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Every narrative event for one scenario. Standalone for now, same reasoning as
/// HypothesisCheckpointSet (whose exact shape this mirrors): no ScenarioDefinition root
/// asset exists yet, so this carries its own scenarioId; when that root exists, this
/// becomes one field on it.
/// </summary>
[CreateAssetMenu(fileName = "NarrativeEvents_", menuName = "VR-CSI/Narrative/Narrative Event Set")]
public class NarrativeEventSet : ScriptableObject
{
    [Tooltip("Which scenario these events belong to - match it to the scene/scenario name, e.g. \"CSI_Environment\".")]
    public string scenarioId;

    public List<NarrativeEvent> events = new();

#if UNITY_EDITOR
    /// <summary>Author-time validation - same warn-don't-block idiom as HypothesisCheckpointSet.OnValidate.</summary>
    private void OnValidate()
    {
        if (events == null)
        {
            return;
        }

        var seenIds = new HashSet<string>();

        foreach (var narrativeEvent in events)
        {
            if (narrativeEvent == null)
            {
                continue;
            }

            if (string.IsNullOrEmpty(narrativeEvent.eventId))
            {
                Debug.LogWarning($"[{name}] A narrative event has no eventId. It will never fire, and RequirementType.NarrativeEventCompleted could never reference it.", this);
            }
            else if (!seenIds.Add(narrativeEvent.eventId))
            {
                Debug.LogWarning($"[{name}] Duplicate narrative event id '{narrativeEvent.eventId}'. Only the first registered instance will ever fire.", this);
            }

            if ((narrativeEvent.lineIds == null || narrativeEvent.lineIds.Length == 0) && narrativeEvent.pool == null)
            {
                Debug.LogWarning($"[{name}] Narrative event '{narrativeEvent.eventId}' has no lineIds (ViCSIDialogueCatalog) - it will fire silently.", this);
            }

            if (narrativeEvent.trigger != null)
            {
                var warnings = new List<string>();
                narrativeEvent.trigger.Validate(warnings, $"{narrativeEvent.eventId}/trigger");
                foreach (var warning in warnings)
                {
                    Debug.LogWarning($"[{name}] {warning}", this);
                }
            }
        }
    }
#endif
}
