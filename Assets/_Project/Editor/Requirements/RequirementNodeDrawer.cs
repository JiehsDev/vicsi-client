// Assets/_Project/Editor/Requirements/RequirementNodeDrawer.cs
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Recursive Inspector drawer for RequirementNode. Applies automatically to every
/// RequirementNode field, including each element of a List&lt;RequirementNode&gt;
/// (RequirementDefinition.root, a Group's own children, ReasoningOption.extraRequirement,
/// ...) - Unity applies a CustomPropertyDrawer to every occurrence of the type, so nesting
/// falls out for free rather than needing separate wiring per consumer.
///
/// Deliberately a hand-rolled add/remove/move list rather than UnityEditorInternal.
/// ReorderableList: a ReorderableList's element-height callback would itself need to
/// recurse into this same drawer's height logic, and getting that exactly right without
/// being able to visually iterate on the live IMGUI layout is a real risk of overlapping
/// controls. The simpler manual layout below is easier to reason about and to keep
/// GetPropertyHeight and OnGUI in lockstep, at the cost of drag-to-reorder (replaced by
/// explicit Up/Down buttons, which satisfy the same "reorder" requirement).
/// </summary>
[CustomPropertyDrawer(typeof(RequirementNode))]
public class RequirementNodeDrawer : PropertyDrawer
{
    private const float LineHeight = 18f;
    private const float Spacing = 2f;
    private const float WarningLineHeight = 32f;
    private const float ButtonRowHeight = 20f;

    // Foldout expansion state, keyed by the SerializedProperty's full path - the standard
    // way a PropertyDrawer (which has no persistent instance of its own per-property)
    // remembers per-node UI state across repaints.
    private static readonly Dictionary<string, bool> Expanded = new();

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        string path = property.propertyPath;
        bool expanded = Expanded.TryGetValue(path, out var e) && e;

        float height = LineHeight; // header/foldout row

        if (!expanded)
        {
            return height;
        }

        height += Spacing + LineHeight; // kind field

        var kindProp = property.FindPropertyRelative("kind");
        var kindValue = (RequirementNodeKind)kindProp.enumValueIndex;

        if (kindValue == RequirementNodeKind.Group)
        {
            height += Spacing + LineHeight; // operator field

            var operatorProp = property.FindPropertyRelative("operatorType");
            var operatorValue = (RequirementLogicalOperator)operatorProp.enumValueIndex;
            var childrenProp = property.FindPropertyRelative("children");

            if (operatorValue == RequirementLogicalOperator.Not && childrenProp.arraySize != 1)
            {
                height += Spacing + WarningLineHeight;
            }
            else if (childrenProp.arraySize == 0 && operatorValue == RequirementLogicalOperator.Any)
            {
                height += Spacing + WarningLineHeight;
            }

            for (int i = 0; i < childrenProp.arraySize; i++)
            {
                height += Spacing + ButtonRowHeight;
                height += Spacing + EditorGUI.GetPropertyHeight(childrenProp.GetArrayElementAtIndex(i), true);
            }

            height += Spacing + ButtonRowHeight; // Add Child button
        }
        else
        {
            var typeProp = property.FindPropertyRelative("requirementType");
            var typeValue = (RequirementType)typeProp.enumValueIndex;
            height += Spacing + LeafFieldsHeight(typeValue);
        }

        height += Spacing + LineHeight; // authoring note

        var node = GetTargetObjectOfProperty(property) as RequirementNode;
        if (node != null)
        {
            var warnings = new List<string>();
            node.Validate(warnings, path);
            height += warnings.Count * (Spacing + WarningLineHeight);
        }

        return height;
    }

    private static float LeafFieldsHeight(RequirementType type)
    {
        switch (type)
        {
            case RequirementType.EvidenceStatusAtOrAbove:
                return (LineHeight + Spacing) * 3f; // evidenceReference, evidenceId, requiredStatus
            case RequirementType.ReportLineSelected:
                return (LineHeight + Spacing) * 3f; // evidenceReference, evidenceId, reportLineId
            case RequirementType.FingerprintLabStatusAtOrAbove:
                return (LineHeight + Spacing) * 3f; // evidenceReference, evidenceId, requiredLabStatus
            case RequirementType.EvidenceSwabbed:
            case RequirementType.EvidenceFingerprinted:
            case RequirementType.EvidenceFlipped:
            case RequirementType.EvidenceDiscovered:
                return (LineHeight + Spacing) * 2f; // evidenceReference, evidenceId
            case RequirementType.WitnessStatementDiscovered:
                return LineHeight + Spacing;
            case RequirementType.LegacySelectionPrerequisite:
                return LineHeight + Spacing; // one nested property (default SelectionPrerequisite drawing)
            case RequirementType.RequirementAssetReference:
                return LineHeight + Spacing;
            case RequirementType.NarrativeEventCompleted:
                return LineHeight + Spacing;
            case RequirementType.ObjectiveCompleted:
                return LineHeight + Spacing;
            case RequirementType.RelationshipCompleted:
                return LineHeight + Spacing;
            case RequirementType.FindingConfirmed:
                return LineHeight + Spacing;
            case RequirementType.MeasurementCompleted:
                return LineHeight + Spacing;
            case RequirementType.ScenarioPhaseReached:
                return LineHeight + Spacing;
            case RequirementType.EvidencePairMeasurementsComplete:
                return (LineHeight + Spacing) * 2f; // evidenceReference, evidenceId
            default:
                return LineHeight;
        }
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        string path = property.propertyPath;
        bool expanded = Expanded.TryGetValue(path, out var e) && e;

        var node = GetTargetObjectOfProperty(property) as RequirementNode;
        string summaryText = node != null ? Truncate(SafeSummary(node)) : "(unavailable)";
        string headerText = string.IsNullOrEmpty(label?.text) ? summaryText : $"{label.text} - {summaryText}";

        Rect headerRect = new Rect(position.x, position.y, position.width, LineHeight);
        bool newExpanded = EditorGUI.Foldout(headerRect, expanded, headerText, true);
        if (newExpanded != expanded)
        {
            Expanded[path] = newExpanded;
        }

        if (!newExpanded)
        {
            return;
        }

        float y = headerRect.yMax + Spacing;
        EditorGUI.indentLevel++;

        var kindProp = property.FindPropertyRelative("kind");
        Rect kindRect = new Rect(position.x, y, position.width, LineHeight);
        EditorGUI.PropertyField(kindRect, kindProp, new GUIContent("Kind"));
        y += LineHeight + Spacing;

        var kindValue = (RequirementNodeKind)kindProp.enumValueIndex;

        if (kindValue == RequirementNodeKind.Group)
        {
            y = DrawGroup(position, y, property);
        }
        else
        {
            y = DrawLeaf(position, y, property);
        }

        Rect noteRect = new Rect(position.x, y, position.width, LineHeight);
        EditorGUI.PropertyField(noteRect, property.FindPropertyRelative("authoringNote"), new GUIContent("Note"));
        y += LineHeight + Spacing;

        if (node != null)
        {
            var warnings = new List<string>();
            node.Validate(warnings, path);
            foreach (var warning in warnings)
            {
                Rect warnRect = new Rect(position.x, y, position.width, WarningLineHeight);
                EditorGUI.HelpBox(warnRect, warning, MessageType.Warning);
                y += WarningLineHeight + Spacing;
            }
        }

        EditorGUI.indentLevel--;
    }

    private static float DrawGroup(Rect position, float y, SerializedProperty property)
    {
        var operatorProp = property.FindPropertyRelative("operatorType");
        Rect opRect = new Rect(position.x, y, position.width, LineHeight);
        EditorGUI.PropertyField(opRect, operatorProp, new GUIContent("Operator"));
        y += LineHeight + Spacing;

        var operatorValue = (RequirementLogicalOperator)operatorProp.enumValueIndex;
        var childrenProp = property.FindPropertyRelative("children");

        if (operatorValue == RequirementLogicalOperator.Not && childrenProp.arraySize != 1)
        {
            Rect warnRect = new Rect(position.x, y, position.width, WarningLineHeight);
            EditorGUI.HelpBox(warnRect, "NOT must have exactly one child - currently fails closed.", MessageType.Warning);
            y += WarningLineHeight + Spacing;
        }
        else if (childrenProp.arraySize == 0 && operatorValue == RequirementLogicalOperator.Any)
        {
            Rect warnRect = new Rect(position.x, y, position.width, WarningLineHeight);
            EditorGUI.HelpBox(warnRect, "Empty ANY group always evaluates to false.", MessageType.Warning);
            y += WarningLineHeight + Spacing;
        }

        for (int i = 0; i < childrenProp.arraySize; i++)
        {
            var childProp = childrenProp.GetArrayElementAtIndex(i);

            Rect buttonRowRect = new Rect(position.x, y, position.width, ButtonRowHeight);
            float buttonWidth = 24f;
            Rect upRect = new Rect(buttonRowRect.x, buttonRowRect.y, buttonWidth, buttonRowRect.height);
            Rect downRect = new Rect(upRect.xMax + 2f, buttonRowRect.y, buttonWidth, buttonRowRect.height);
            Rect removeRect = new Rect(downRect.xMax + 2f, buttonRowRect.y, buttonWidth, buttonRowRect.height);
            Rect labelRect = new Rect(removeRect.xMax + 6f, buttonRowRect.y, buttonRowRect.width - (buttonWidth * 3f + 10f), buttonRowRect.height);

            EditorGUI.LabelField(labelRect, $"Child {i}");

            using (new EditorGUI.DisabledScope(i == 0))
            {
                if (GUI.Button(upRect, "▲"))
                {
                    childrenProp.MoveArrayElement(i, i - 1);
                }
            }
            using (new EditorGUI.DisabledScope(i == childrenProp.arraySize - 1))
            {
                if (GUI.Button(downRect, "▼"))
                {
                    childrenProp.MoveArrayElement(i, i + 1);
                }
            }
            if (GUI.Button(removeRect, "✕"))
            {
                childrenProp.DeleteArrayElementAtIndex(i);
                y += ButtonRowHeight + Spacing;
                break; // array size changed under us - finish this pass, redraw next frame
            }

            y += ButtonRowHeight + Spacing;

            float childHeight = EditorGUI.GetPropertyHeight(childProp, true);
            Rect childRect = new Rect(position.x, y, position.width, childHeight);
            EditorGUI.PropertyField(childRect, childProp, GUIContent.none, true);
            y += childHeight + Spacing;
        }

        Rect addRect = new Rect(position.x, y, position.width, ButtonRowHeight);
        if (GUI.Button(addRect, "+ Add Child"))
        {
            int newIndex = childrenProp.arraySize;
            childrenProp.InsertArrayElementAtIndex(newIndex);

            // children is [SerializeReference] - InsertArrayElementAtIndex on a
            // managed-reference list can alias a neighboring element's reference
            // instead of creating an independent instance. Always assign a fresh
            // RequirementNode explicitly rather than trusting the inserted default.
            childrenProp.GetArrayElementAtIndex(newIndex).managedReferenceValue = new RequirementNode();
        }
        y += ButtonRowHeight + Spacing;

        return y;
    }

    private static float DrawLeaf(Rect position, float y, SerializedProperty property)
    {
        var typeProp = property.FindPropertyRelative("requirementType");
        Rect typeRect = new Rect(position.x, y, position.width, LineHeight);
        EditorGUI.PropertyField(typeRect, typeProp, new GUIContent("Requirement Type"));
        y += LineHeight + Spacing;

        var typeValue = (RequirementType)typeProp.enumValueIndex;

        switch (typeValue)
        {
            case RequirementType.EvidenceStatusAtOrAbove:
                y = DrawEvidenceIdWithReference(position, y, property);
                y = DrawField(position, y, property, "requiredStatus", "Required Status");
                break;

            case RequirementType.ReportLineSelected:
                y = DrawEvidenceIdWithReference(position, y, property);
                y = DrawField(position, y, property, "reportLineId", "Report Line Id");
                break;

            case RequirementType.FingerprintLabStatusAtOrAbove:
                y = DrawEvidenceIdWithReference(position, y, property);
                y = DrawField(position, y, property, "requiredLabStatus", "Required Lab Status");
                break;

            case RequirementType.EvidenceSwabbed:
            case RequirementType.EvidenceFingerprinted:
            case RequirementType.EvidenceFlipped:
            case RequirementType.EvidenceDiscovered:
                y = DrawEvidenceIdWithReference(position, y, property);
                break;
            case RequirementType.WitnessStatementDiscovered:
                y = DrawField(position, y, property, "evidenceId", "Statement ID");
                break;

            case RequirementType.LegacySelectionPrerequisite:
                y = DrawField(position, y, property, "legacyPrerequisite", "Legacy Prerequisite");
                break;

            case RequirementType.RequirementAssetReference:
                y = DrawField(position, y, property, "requirementAsset", "Requirement Asset");
                break;

            case RequirementType.NarrativeEventCompleted:
                y = DrawField(position, y, property, "narrativeEventId", "Narrative Event Id");
                break;

            case RequirementType.ObjectiveCompleted:
                y = DrawField(position, y, property, "objectiveId", "Objective Id");
                break;

            case RequirementType.RelationshipCompleted:
                y = DrawField(position, y, property, "relationshipId", "Relationship Id");
                break;

            case RequirementType.FindingConfirmed:
                y = DrawField(position, y, property, "findingId", "Finding Id");
                break;

            case RequirementType.MeasurementCompleted:
                y = DrawField(position, y, property, "measurementRequirementId", "Measurement Requirement Id");
                break;
            case RequirementType.ScenarioPhaseReached:
                y = DrawField(position, y, property, "scenarioPhase", "Scenario Phase");
                break;

            case RequirementType.EvidencePairMeasurementsComplete:
                y = DrawEvidenceIdWithReference(position, y, property);
                break;
        }

        return y;
    }

    private static float DrawEvidenceIdWithReference(Rect position, float y, SerializedProperty property)
    {
        var referenceProp = property.FindPropertyRelative("evidenceReference");
        var evidenceIdProp = property.FindPropertyRelative("evidenceId");

        Rect refRect = new Rect(position.x, y, position.width, LineHeight);
        EditorGUI.BeginChangeCheck();
        EditorGUI.PropertyField(refRect, referenceProp, new GUIContent("Evidence (picker)"));
        if (EditorGUI.EndChangeCheck() && referenceProp.objectReferenceValue is EvidenceDefinition def && !string.IsNullOrEmpty(def.evidenceId))
        {
            evidenceIdProp.stringValue = def.evidenceId;
        }
        y += LineHeight + Spacing;

        Rect idRect = new Rect(position.x, y, position.width, LineHeight);
        EditorGUI.PropertyField(idRect, evidenceIdProp, new GUIContent("Evidence Id"));
        y += LineHeight + Spacing;

        return y;
    }

    private static float DrawField(Rect position, float y, SerializedProperty property, string relativeName, string label)
    {
        var fieldProp = property.FindPropertyRelative(relativeName);
        float height = EditorGUI.GetPropertyHeight(fieldProp, true);
        Rect rect = new Rect(position.x, y, position.width, height);
        EditorGUI.PropertyField(rect, fieldProp, new GUIContent(label), true);
        return y + height + Spacing;
    }

    private static string Truncate(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "(empty)";
        }

        int firstLineBreak = text.IndexOf('\n');
        string firstLine = firstLineBreak >= 0 ? text.Substring(0, firstLineBreak) + " ..." : text;
        return firstLine.Length > 90 ? firstLine.Substring(0, 90) + "..." : firstLine;
    }

    private static string SafeSummary(RequirementNode node)
    {
        try
        {
            return node.BuildSummary();
        }
        catch (Exception)
        {
            return "(summary unavailable)";
        }
    }

    // --- Reflection helper: resolves the actual managed object a SerializedProperty
    // points at, so the drawer can call BuildSummary()/Validate() on the real instance
    // rather than re-deriving them from raw SerializedProperty fields. Standard, widely
    // used Unity-editor idiom for exactly this problem (SerializedProperty has no public
    // "give me the boxed object" API for non-Unity-Object managed types). ---

    private static object GetTargetObjectOfProperty(SerializedProperty prop)
    {
        if (prop == null)
        {
            return null;
        }

        string path = prop.propertyPath.Replace(".Array.data[", "[");
        object obj = prop.serializedObject.targetObject;
        var elements = path.Split('.');

        foreach (var element in elements)
        {
            if (element.Contains("["))
            {
                string elementName = element.Substring(0, element.IndexOf("["));
                int index = Convert.ToInt32(element.Substring(element.IndexOf("[")).Replace("[", "").Replace("]", ""));
                obj = GetValue(obj, elementName, index);
            }
            else
            {
                obj = GetValue(obj, element);
            }
        }

        return obj;
    }

    private static object GetValue(object source, string name)
    {
        if (source == null)
        {
            return null;
        }

        var type = source.GetType();
        while (type != null)
        {
            var field = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (field != null)
            {
                return field.GetValue(source);
            }

            var property = type.GetProperty(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (property != null)
            {
                return property.GetValue(source, null);
            }

            type = type.BaseType;
        }

        return null;
    }

    private static object GetValue(object source, string name, int index)
    {
        var enumerable = GetValue(source, name) as System.Collections.IEnumerable;
        if (enumerable == null)
        {
            return null;
        }

        var enumerator = enumerable.GetEnumerator();
        for (int i = 0; i <= index; i++)
        {
            if (!enumerator.MoveNext())
            {
                return null;
            }
        }

        return enumerator.Current;
    }
}
