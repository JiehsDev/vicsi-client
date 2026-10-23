#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class ConclusionPickerUITest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }
    private readonly StringBuilder report=new("=== ConclusionPickerUITest ===\n");
    private int failures;
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private void Check(string label,bool value) { report.AppendLine((value?"PASS: ":"FAIL: ")+label); if(!value) failures++; }
    public void RunAllTests() { DontDestroyOnLoad(gameObject); StartCoroutine(Run()); }
    private IEnumerator Run()
    {
        AutomatedTestSandbox.Activate(nameof(ConclusionPickerUITest));
        int original=SessionState.GetInt(SceneTestStart.EditorSelectionKey,-1);
        bool diagnostic=SessionState.GetBool("ViCSI.SceneTestStart.Diagnostic",false);
        SessionState.SetBool("ViCSI.SceneTestStart.Diagnostic",true);
        SessionState.SetInt(SceneTestStart.EditorSelectionKey,(int)SceneTestStartingPoint.SketchDone);
        ConclusionDefinition longDefinition=null; string originalDescription=null;
        try
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/_Project/Scenes/Scenarios/CSI_Environment.unity",new LoadSceneParameters(LoadSceneMode.Single));
            for(int i=0;i<12;i++) yield return null;
            RelationshipReviewTestUtil.BeginCaseFixture(); RelationshipReviewTestUtil.ConfirmAvailable();
            Check("Real investigation fixture has completed relationships and sketch",SceneTestStart.Instance.IsReady && SceneTestStart.Instance.Failure==null
                && EvidenceRelationshipsReview.Instance.IsReadyForConclusion && CrimeSceneSketchManager.CompletionBlockReason==null);
            var manager=EndingManager.Instance; var screen=FindAnyObjectByType<CompletionScreenUI>();
            var rig=FindAnyObjectByType<OVRCameraRig>(); rig.enabled=false;
            var camera=Camera.main; camera.transform.position=EvidenceBoxDepositZone.Instance.transform.position+new Vector3(0,1.6f,-4);
            camera.transform.rotation=Quaternion.identity;
            manager.TrySubmitConclusion();
            for(int i=0;i<8 && !manager.IsPickerShowing;i++) yield return null;
            Check("Picker opens with an explicit review action",manager.IsPickerShowing && !manager.IsReviewingConclusion && manager.AcceptedEnding==null);
            Check("Opening owns UI and cannot submit through its opening press",PlayerUIGate.IsBlocked);
            manager.ActivatePickerPrimary(); Check("Opening press is ignored",!manager.IsReviewingConclusion && manager.AcceptedEnding==null);
            yield return new WaitForSecondsRealtime(.35f);
            manager.PickerLayout.Refresh(); Canvas.ForceUpdateCanvases();
            var root=(GameObject)typeof(EndingManager).GetField("pickerRoot",Private).GetValue(manager);
            Check("Footer actions fit without clipping",root.GetComponentsInChildren<TMPro.TMP_Text>().Where(t=>t.name.EndsWith("action label")).All(t=>{t.ForceMeshUpdate();return !t.isTextOverflowing&&!t.isTextTruncated;}));
            Check("Conclusion text is readable and padded",manager.PickerLayout.Body.Content.GetComponentsInChildren<ResponsiveTextOption>()
                .All(f=>f.Label.fontSize>=29 && f.HorizontalPadding>=20 && f.VerticalPadding>=16));
            Capture(camera,"choose-conclusion.png");
            manager.PickerPointer.Targets.First(t=>t.id=="conclusion-option-0").onClick();
            Check("Clicking a choice opens review without recording a conclusion",manager.IsReviewingConclusion && manager.AcceptedEnding==null && manager.SelectedConclusionId==null);
            manager.ActivatePickerPrimary(); Check("Same click or rapid A press cannot submit",manager.IsReviewingConclusion && manager.AcceptedEnding==null);
            yield return new WaitForSecondsRealtime(.35f);
            Canvas.ForceUpdateCanvases();
            Check("Review actions fit without truncation",root.GetComponentsInChildren<TMPro.TMP_Text>().Where(t=>t.name.EndsWith("action label")).All(t=>{t.ForceMeshUpdate();return !t.isTextOverflowing&&!t.isTextTruncated;}));
            Check("Review shows the selected name and separate submit/back actions",root.GetComponentsInChildren<TMPro.TMP_Text>().Any(t=>t.name=="Selected conclusion"&&!string.IsNullOrEmpty(t.text))
                && root.GetComponentsInChildren<TMPro.TMP_Text>().Any(t=>t.text=="[A] Submit conclusion"));
            Capture(camera,"review-conclusion.png");
            manager.PickerPointer.Targets.First(t=>t.id=="conclusion-back").onClick();
            Check("Back returns to choices without submission",manager.IsPickerShowing&&!manager.IsReviewingConclusion&&manager.AcceptedEnding==null);
            yield return new WaitForSecondsRealtime(.3f);
            manager.BackFromConclusion(); yield return null; yield return null;
            Check("Cancel returns to investigation and releases ownership",!manager.IsPickerShowing&&!PlayerUIGate.IsBlocked&&!ToolInputLock.HasUIOwners
                && manager.AcceptedEnding==null&&SceneTurnover.Phase==ScenarioPhase.SOCOInvestigation);

            manager.TrySubmitConclusion(); for(int i=0;i<8&&!manager.IsPickerShowing;i++) yield return null;
            yield return new WaitForSecondsRealtime(.35f);
            longDefinition=manager.GetSelectableCaseConclusions()[0]; originalDescription=longDefinition.description;
            longDefinition.description=string.Join(" ",Enumerable.Repeat("A lengthy interpretation must remain readable while the player reviews the documented findings.",20));
            typeof(EndingManager).GetMethod("RefreshPickerOptions",Private).Invoke(manager,null);
            manager.PickerLayout.Refresh(); Canvas.ForceUpdateCanvases();
            Check("Long conclusion descriptions use a bounded scroll area",manager.PickerLayout.Body.CanScroll && manager.PickerLayout.MaximumHeight<=900);
            var primary=manager.PickerPointer.Targets.First(t=>t.id=="conclusion-primary").rect;
            var position=primary.position; manager.PickerLayout.ScrollOptions(10000); Canvas.ForceUpdateCanvases();
            Check("Scrolling leaves footer controls stationary",Vector3.Distance(primary.position,position)<.00001f);
            Check("Long text keeps full measured height",manager.PickerLayout.Body.Content.GetComponentsInChildren<TMPro.TMP_Text>().All(t=>{t.ForceMeshUpdate();return !t.isTextOverflowing;}));
            longDefinition.description=originalDescription; longDefinition=null;
            typeof(EndingManager).GetMethod("RefreshPickerOptions",Private).Invoke(manager,null);
            manager.ActivatePickerPrimary(); yield return new WaitForSecondsRealtime(.35f);
            typeof(EndingManager).GetField("pendingConclusionId",Private).SetValue(manager,"no-longer-selectable");
            manager.ActivatePickerPrimary();
            Check("Stale final choice is refused and returns to choices",!manager.IsReviewingConclusion&&manager.IsPickerShowing&&manager.AcceptedEnding==null);
            STCSNotificationUI.Instance.HideImmediately(); NotificationManager.Instance.UI.ClearTooltip();
            manager.enabled=false; yield return null; yield return null;
            Check("Disabling picker releases UI and hides its canvas",!manager.IsPickerShowing&&!PlayerUIGate.IsBlocked&&manager.AcceptedEnding==null);
            manager.enabled=true;
            manager.TrySubmitConclusion(); for(int i=0;i<8&&!manager.IsPickerShowing;i++) yield return null;
            yield return new WaitForSecondsRealtime(.35f);
            manager.ActivatePickerPrimary(); yield return new WaitForSecondsRealtime(.35f);
            var expected=manager.GetPickerOptionIdsForTest()[0];
            typeof(CompletionScreenUI).GetField("lineDisplaySeconds",Private).SetValue(screen,.01f);
            manager.ActivatePickerPrimary(); manager.ActivatePickerPrimary();
            for(int i=0;i<8&&!screen.IsShowing;i++) yield return null;
            Check("Separate final confirmation records the chosen conclusion once",manager.AcceptedEnding!=null&&manager.SelectedConclusionId==expected&&screen.IsShowing&&!manager.IsPickerShowing);
            yield return new WaitForSecondsRealtime(.3f);
            var feedback=(GameObject)typeof(CompletionScreenUI).GetField("panelRoot",Private).GetValue(screen);
            Check("Feedback clearly hands off to evidence custody",feedback.GetComponentsInChildren<TMPro.TMP_Text>().Any(t=>t.text=="Conclusion recorded")
                && feedback.GetComponentsInChildren<TMPro.TMP_Text>().Any(t=>t.text=="[A] Continue to evidence custody"));
            Capture(camera,"conclusion-recorded.png");
            Check("Continue uses the real closeout transition",screen.ContinueToCloseout()); yield return null; yield return null;
            Check("Conclusion feedback advances to custody without completing the session",manager.FeedbackAccepted&&!screen.IsShowing
                && (SceneTurnover.Phase==ScenarioPhase.SOCOCloseout||SceneTurnover.Phase==ScenarioPhase.EvidenceInventoryReview)
                && !EvidenceBoxDepositZone.Instance.PassComplete);
            SessionLogger.Instance.Flush();
            Check("Only the final submit logs a selected conclusion",SessionLogReader.ReadEvents(SessionLogger.Instance.ActiveLogFilePath).Count(e=>e.eventType=="ConclusionSelected")==1);
        }
        finally
        {
            if(longDefinition!=null) longDefinition.description=originalDescription;
            SessionState.SetInt(SceneTestStart.EditorSelectionKey,original);
            SessionState.SetBool("ViCSI.SceneTestStart.Diagnostic",diagnostic);
        }
        report.AppendLine(failures==0?"=== PASS, 0 failures ===":"=== FAIL: "+failures+" failures ==="); Report=report.ToString(); IsDone=true;
    }
    private void Capture(Camera camera,string filename)
    {
        var previous=camera.targetTexture; var active=RenderTexture.active;
        var target=RenderTexture.GetTemporary(1200,900,24); var texture=new Texture2D(1200,900,TextureFormat.RGB24,false);
        try
        {
            camera.targetTexture=target; Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active=target;
            texture.ReadPixels(new Rect(0,0,1200,900),0,0); texture.Apply();
            const string folder="docs/evidence/conclusion-ui-2026-10-23";
            System.IO.Directory.CreateDirectory(folder); System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder,filename),texture.EncodeToPNG());
        }
        finally {camera.targetTexture=previous;RenderTexture.active=active;RenderTexture.ReleaseTemporary(target);Destroy(texture);}
    }
}
#endif
