using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

/// <summary>Real scene presentation and out-of-order marker placement, retaining original record keys.</summary>
public sealed class EvidenceDisplayTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }
    private readonly StringBuilder report = new("=== EvidenceDisplayTest ===\n");
    private int failures;
    private void Check(string label, bool value) { report.AppendLine((value ? "PASS: " : "FAIL: ") + label); if (!value) failures++; }
    public void RunAndLog() { AutomatedTestSandbox.Activate(nameof(EvidenceDisplayTest)); StartCoroutine(Run()); }
    private IEnumerator Run()
    {
        RelationshipReviewTestUtil.BeginCaseFixture();
        yield return null;
        if (SceneTurnover.IsPending) SceneTurnover.Controller.CompleteWithoutDialogue();
        Check("Scene photography completes through existing gates", ScenePhotographyTestUtil.CompleteRoute());
        var state = EvidenceStateManager.Instance;
        var tents = FindAnyObjectByType<EvidenceTentTool>(FindObjectsInactive.Include);
        var ids = new[] { "EVD-018", "EVD-015", "EVD-014", "BODY-A" };
        var labels = new[] { "Evidence 3 – Empty Liquor Bottle", "Evidence 2 – Blood Spatter", "Evidence 1 – Kitchen Knife", "Body A – John" };
        var rig = FindAnyObjectByType<OVRCameraRig>();
        HeldItemCoordinator.Instance.ToggleEquipTool(ToolType.EvidenceMarker, rig.rightHandAnchor);
        yield return null;
        for (int i=0;i<ids.Length;i++)
        {
            string id=ids[i]; var record=state.GetRecord(id);
            Check(id+" resolves to its new visible label", state.GetDisplayName(id)==labels[i] && record.definition.PlayerDisplayName==labels[i]);
            Check(id+" retains its serialized identity", record.definition.evidenceId==id);
            state.MarkFound(id, ToolType.None);
            string note=EvidenceNoteComposer.Compose(record);
            Check(id+" note uses display label without raw roster IDs", note.Contains(labels[i]) && !HasIds(note));
            Check(id+" rejects recording before prerequisites", !state.CanAdvanceTo(id,EvidenceStatus.Logged));
            if (id=="BODY-A")
            {
                state.MarkPhotographedRaw(id,ToolType.Photographer);
                state.MarkChalkMarked(id,ToolType.ChalkMark);
                Check("Body still accepts letter A through its original procedure", state.MarkTentedWithLetter(id,ToolType.LetterTentMarker,"A")==TransitionResult.Applied && record.tentLetter=="A");
                continue;
            }
            state.MarkPhotographedRaw(id,ToolType.Photographer);
            if(record.definition.requiresChalk)state.MarkChalkMarked(id,ToolType.ChalkMark);
            var prop=EvidenceProp.FindById(id);
            var origin=(Transform)typeof(EvidenceTentTool).GetField("placementOrigin",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(tents);
            origin.position=prop.transform.position+Vector3.up*1.5f;origin.rotation=Quaternion.LookRotation(Vector3.down);
            Physics.SyncTransforms();
            int expected=EvidenceDisplay.Number(id);
            Check(id+" preview uses assigned number before placement", tents.NextTentNumber==expected);
            typeof(EvidenceTentTool).GetMethod("PlaceTent",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(tents,null);
            Check(id+" actual marker matches its display number despite bottle-first order", record.status==EvidenceStatus.Marked && record.tentNumber==expected);
            var placed=FindObjectsByType<EvidenceTentPickup>(FindObjectsSortMode.None).SingleOrDefault(t=>t.MarkedEvidenceId==id);
            var templates=(GameObject[])typeof(EvidenceTentTool).GetField("tentVisuals",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(tents);
            Check(id+" physical marker mesh matches its numbered scene template", placed!=null
                && placed.GetComponent<MeshFilter>().sharedMesh==templates[expected-1].GetComponent<MeshFilter>().sharedMesh);
        }
        var body=state.GetRecord("BODY-A");
        Check("Body remains letter-marked, examined in place", body.definition.examinedInPlace && EvidenceDisplay.Number("BODY-A")==0);
        Check("Sample names retain source meaning", EvidenceDisplay.Label("fingerprint-sample:EVD-018")=="Fingerprint Sample – Evidence 3 – Empty Liquor Bottle"
            && EvidenceDisplay.Label("blood-sample:EVD-014")=="Blood Sample – Evidence 1 – Kitchen Knife");
        Check("Unknown future content retains authored fallback", EvidenceDisplay.Label("future-evidence","Future item")=="Future item");
        Check("Prose uses the same complete Body A label as the dashboard", EvidenceDisplay.Text("Record BODY-A beside EVD-014.")=="Record Body A – John beside Evidence 1 – Kitchen Knife.");
        Check("Unknown IDs never impersonate the current numbered evidence", EvidenceDisplay.Text("EVD-0149")=="Unrecognized evidence");
        Check("Historical evidence names resolve without raw IDs", EvidenceDisplay.Text("EVD-017 / EVD-019")=="Victim's mobile phone / Disposable gloves");
        for (int i = 0; i < ReasoningCheckpointManager.Instance.Definitions.checkpoints.Count; i++)
        {
            var question = ReasoningCheckpointManager.Instance.Definitions.checkpoints[i];
            Check("Current question uses authored position " + (i + 1), QuestionDisplay.Label(question.id) == "Reasoning Question " + (i + 1));
        }
        Check("Legacy labels retain historical numbering", QuestionDisplay.Text("HYP-01 / HYP-02 / HYP-03") == "Reasoning Question 1 / Reasoning Question 2 / Reasoning Question 3");
        Check("Relationship IDs resolve to authored readable names", !QuestionDisplay.Text("REL-paul-timeline-contradiction").Contains("REL-") && QuestionDisplay.Text("REL-paul-timeline-contradiction").Contains("Paul"));
        Check("Legacy terminology resolves in visible prose", QuestionDisplay.Text("Hypothesis Checkpoint: Submit Hypothesis & Reasoning") == "Reasoning Checkpoint: Confirm Answer");
        SessionLogger.Instance.Flush();
        var original=SessionLogReader.ReadEvents(SessionLogger.Instance.ActiveLogFilePath).Where(e=>e.targetId=="EVD-018").ToList();
        Check("Original logger targets remain internal IDs", original.Count>0 && original.All(e=>!e.targetId.Contains("Evidence 3")));
        Report=report.AppendLine(failures==0?"=== PASS, 0 failures ===":"=== FAIL: "+failures+" failures ===").ToString();
        IsDone=true;Debug.Log(Report);
    }
    private static bool HasIds(string value)=>value.Contains("EVD-014")||value.Contains("EVD-015")||value.Contains("EVD-018")||value.Contains("BODY-A");
}
