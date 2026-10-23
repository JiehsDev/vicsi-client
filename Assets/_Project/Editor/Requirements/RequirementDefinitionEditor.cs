// Assets/_Project/Editor/Requirements/RequirementDefinitionEditor.cs
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Adds a live summary and an explicit "Validate Now" pass on top of the default
/// Inspector (which already renders `root` via RequirementNodeDrawer) - same
/// "warn, don't block" idiom as HypothesisCheckpointSet's own OnValidate, just also
/// available on demand rather than only when the asset happens to get touched.
/// </summary>
[CustomEditor(typeof(RequirementDefinition))]
public class RequirementDefinitionEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var definition = (RequirementDefinition)target;

        if (definition.root != null)
        {
            EditorGUILayout.HelpBox(definition.root.BuildSummary(), MessageType.None);
        }

        EditorGUILayout.Space();
        DrawDefaultInspector();
        EditorGUILayout.Space();

        if (GUILayout.Button("Validate Now"))
        {
            var warnings = new List<string>();
            definition.root?.Validate(warnings, definition.name);

            if (warnings.Count == 0)
            {
                Debug.Log($"[{definition.name}] Validate Now: no issues found.", definition);
            }
            else
            {
                foreach (var warning in warnings)
                {
                    Debug.LogWarning($"[{definition.name}] {warning}", definition);
                }
            }
        }
    }
}
