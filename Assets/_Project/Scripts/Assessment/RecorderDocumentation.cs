using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable] public sealed class RecorderStatementDefinition { public string id,text; public int weight; }
[Serializable] public sealed class RecorderSubjectDefinition { public string id,label; public List<RecorderStatementDefinition> statements=new(); }
[Serializable] public sealed class RecorderDocumentationCatalog
{
    public int version,requiredSelections,maximumPerSubject,maximumTotal;
    public List<RecorderSubjectDefinition> subjects=new();
}
[Serializable] public sealed class RecorderStatementResult
{
    public string id,text;
    public bool selected;
    public int weight,earnedPoints;
}
[Serializable] public sealed class RecorderSubjectResult
{
    public int version;
    public string subjectId,subjectLabel;
    public List<RecorderStatementResult> statements=new();
    public int earnedPoints,maximumPossiblePoints;
}

/// <summary>Separate versioned Recorder assessment. Never contributes to deduction scoring.</summary>
public static class RecorderDocumentation
{
    static RecorderDocumentationCatalog catalog;
    public static RecorderDocumentationCatalog Catalog => catalog??=JsonUtility.FromJson<RecorderDocumentationCatalog>(Resources.Load<TextAsset>("RecorderDocumentation_v2").text);
    public static RecorderSubjectDefinition Subject(string id)=>Catalog.subjects.Find(s=>s.id==id);
    public static bool AppliesTo(string id)=>Subject(id)!=null;
    // Existing case findings remain available at recording completion, independently
    // of assessment choices. These are compatibility observation gates, not selections.
    public static bool HasBaselineFinding(EvidenceRecord record,string legacyId)
    {
        if(record?.recorderDocumentation==null)return false;
        bool known=record.definition.evidenceId switch
        {
            "BODY-A"=>legacyId=="body-a-location"||legacyId=="body-a-position"||legacyId=="body-a-wound-sharp-object",
            "EVD-014"=>legacyId=="knife-wound-match",
            "EVD-015"=>legacyId=="blood-spatter-mechanism-only", _=>false
        };
        var original=record.definition.reportLineOptions.Find(s=>s.id==legacyId);
        return known&&original!=null&&SelectionPrerequisite.AllSatisfied(original.prerequisites)&&RequirementEvaluator.IsSatisfied(original.extraRequirement);
    }
    public static List<ReportLineOption> Options(string id)=>Subject(id)?.statements.Select(s=>new ReportLineOption{id=s.id,text=s.text}).ToList();
    public static bool ValidSelection(string id,IEnumerable<string> selected)
    {
        var subject=Subject(id);if(subject==null||selected==null)return false;
        var ids=selected.ToArray();return ids.Length==3&&ids.Distinct().Count()==3&&ids.All(value=>subject.statements.Any(s=>s.id==value));
    }
    public static RecorderSubjectResult Capture(string id,IEnumerable<string> selected)
    {
        if(!ValidSelection(id,selected))throw new ArgumentException("Recorder requires three distinct subject statements.");
        var chosen=new HashSet<string>(selected);var subject=Subject(id);
        var result=new RecorderSubjectResult{version=Catalog.version,subjectId=id,subjectLabel=subject.label,maximumPossiblePoints=6};
        result.statements=subject.statements.Select(s=>new RecorderStatementResult{id=s.id,text=s.text,selected=chosen.Contains(s.id),
            weight=s.weight,earnedPoints=chosen.Contains(s.id)?s.weight:0}).ToList();
        result.earnedPoints=result.statements.Sum(s=>s.earnedPoints);return result;
    }
}
