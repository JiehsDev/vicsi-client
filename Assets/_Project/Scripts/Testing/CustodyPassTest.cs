#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Real-scene bulk transfer, sample provenance, display geometry, upload and restart regression.</summary>
public sealed class CustodyPassTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }
    private readonly StringBuilder report=new("=== CustodyPassTest ===\n");
    private int failures;
    private void Check(string label,bool value) { report.AppendLine((value?"PASS: ":"FAIL: ")+label); if(!value) failures++; }
    public void RunAllTests() { DontDestroyOnLoad(gameObject); StartCoroutine(Run()); }
    private IEnumerator Run()
    {
        AutomatedTestSandbox.Activate(nameof(CustodyPassTest));
        int original=SessionState.GetInt(SceneTestStart.EditorSelectionKey,-1);
        SessionState.SetBool("ViCSI.SceneTestStart.Diagnostic",true);
        SessionState.SetInt(SceneTestStart.EditorSelectionKey,(int)SceneTestStartingPoint.CustodyPass);
        try
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/_Project/Scenes/Scenarios/CSI_Environment.unity",new LoadSceneParameters(LoadSceneMode.Single));
            for(int i=0;i<10;i++) yield return null;
            var start=SceneTestStart.Instance; var box=EvidenceBoxDepositZone.Instance; var inventory=EvidenceInventory.Instance;
            var state=EvidenceStateManager.Instance; var closeout=SOCOCloseout.Instance;
            Check("Custody preset ready",start.IsReady && start.Failure==null);
            report.AppendLine("INFO: setup="+start.Failure+" phase="+SceneTurnover.Phase+" range="+box.PlayerInRange+" eye="+Camera.main.transform.position);
            // Offline batch runs have no tracked headset pose. Establish the test
            // observer from the actual receiving bounds; retain the real range gate.
            var rig=FindAnyObjectByType<OVRCameraRig>(); rig.enabled=false;
            var head=Camera.main.transform;
            var receiving=box.GetComponent<Collider>().bounds;
            var approach=Vector3.Lerp(receiving.ClosestPoint(head.position),receiving.center,.25f);
            approach.y=box.transform.position.y+1.6f; head.position=approach;
            Physics.SyncTransforms();
            var ids=inventory.AllEntryIds.OrderBy(id=>id).ToList();
            Check("Six real entries; two bags, two blood containers, two fingerprint cards",ids.Count==6 && ids.Count(id=>inventory.GetSampleSource(id)==null)==2
                && ids.Count(inventory.IsFingerprintSample)==2 && ids.Count(id=>inventory.GetSampleType(id)=="Blood")==2);
            Check("No tool or body in custody roster",!ids.Contains("BODY-A") && ids.All(id=>inventory.GetDefinition(id)!=null));
            Check("Test observer approaches actual receiving bounds beside empty box",box.PlayerInRange && box.Contents.ItemCount==0 && !box.PassComplete);
            var eyePosition=head.position;
            var eyeRotation=head.rotation;
            var guide=box.GetComponent<EvidenceBoxWaypoint>();
            Check("One custody waypoint is installed",guide!=null && box.GetComponents<EvidenceBoxWaypoint>().Length==1);
            STCSNotificationUI.Instance.HideImmediately();
            head.position=box.transform.position+new Vector3(0,1.6f,-4);
            head.LookAt(box.transform.position+Vector3.up*.48f);guide.Refresh();
            Check("Ready stored evidence shows a box destination from a distance",guide.DestinationActive && guide.IsVisible && guide.IsPin
                && guide.WaypointCanvas.GetComponentInChildren<UnityEngine.UI.Text>().text.Contains("Transfer stored evidence"));
            Check("Waypoint keeps uniform world scale [actual="+guide.WaypointCanvas.transform.lossyScale+", parent="+guide.WaypointCanvas.transform.parent+"]",
                guide.WaypointCanvas.transform.parent==null && Vector3.Distance(guide.WaypointCanvas.transform.lossyScale,Vector3.one*.0015f)<.00001f);
            CaptureWaypoint(head.GetComponent<Camera>());
            head.Rotate(0,180,0);guide.Refresh();
            Check("Looking away shows an edge arrow",guide.IsVisible && !guide.IsPin && guide.MarkerViewport.x>=0 && guide.MarkerViewport.x<=1
                && guide.MarkerViewport.y>=0 && guide.MarkerViewport.y<=1);
            head.LookAt(box.transform.position+Vector3.up*.48f);
            PlayerUIGate.Enter(this);guide.Refresh();Check("Modal UI hides the waypoint",!guide.IsVisible);PlayerUIGate.Exit(this);
            using(EvidenceBoxWaypoint.HideForCapture())
            {
                using(EvidenceBoxWaypoint.HideForCapture()) { guide.Refresh();Check("Photo capture hides the waypoint",!guide.IsVisible); }
                guide.Refresh();Check("Nested capture stays hidden until final restore",!guide.IsVisible);
            }
            Check("Waypoint returns after capture without taking input",guide.IsVisible && !PlayerUIGate.IsBlocked && !ToolInputLock.HasUIOwners);
            guide.enabled=false;Check("Disabling the guide hides its canvas",!guide.WaypointCanvas.enabled);guide.enabled=true;
            head.SetPositionAndRotation(eyePosition,eyeRotation);guide.Refresh();
            Check("Local transfer prompt takes over beside box",guide.DestinationActive && box.PlayerInRange && !guide.IsVisible);
            head.position+=Vector3.right*50;
            Check("Review cannot open away from box",!box.RequestCustodyPass()); head.position=eyePosition;
            Check("Closeout blocked before pass",!closeout.ShowInventoryReview() && !closeout.ConfirmInventory() && !SceneTurnover.TryAdvance(ScenarioPhase.FinalSceneSurvey));
            Check("Stale reviewed set cannot partially transfer",!inventory.TransferStoredToBox(box.BoxId,ids.Skip(1).ToList(),out _) && ids.All(inventory.IsInInventory));
            var knife=state.GetRecord("EVD-014"); knife.swabSampleSealed=false;
            Check("Unsealed sample blocks entire set",!inventory.TransferStoredToBox(box.BoxId,ids,out _) && ids.All(id=>inventory.GetCustody(id)==EvidenceCustodyLocation.Inventory));
            guide.Refresh();Check("Incomplete evidence cannot activate custody waypoint",!guide.DestinationActive && !guide.IsVisible);
            knife.swabSampleSealed=true;
            var hand=rig.rightHandAnchor;
            Check("Stored evidence can be held before pass",inventory.EquipHeld("fingerprint-sample:EVD-014",hand));
            Check("Held evidence blocks entire pass",!inventory.TransferStoredToBox(box.BoxId,ids,out _) && !box.RequestCustodyPass() && box.Contents.ItemCount==0);
            inventory.UnequipHeld();
            STCSNotificationUI.Instance.HideImmediately();
            Check("Review opens with explicit student action",box.RequestCustodyPass() && box.IsShowing && PlayerUIGate.IsBlocked && ToolInputLock.HasUIOwners);
            Check("Opening alone moves nothing",ids.All(inventory.IsInInventory) && box.Contents.ItemCount==0);
            Check("Opening press cannot confirm immediately",!box.ConfirmCustodyPass());
            var custodyPointer=(WorldPanelPointer)typeof(EvidenceBoxDepositZone).GetField("pointer",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(box);
            custodyPointer.Targets.Single(t=>t.id=="custody-cancel").onClick(); yield return null;
            Check("Cancel releases UI and keeps all items",!box.IsShowing && !PlayerUIGate.IsBlocked && ids.All(inventory.IsInInventory));
            Check("Reopen after cancel",box.RequestCustodyPass());
            yield return new WaitForSecondsRealtime(.3f);
            knife.swabSampleSealed=false;
            Check("Confirmation revalidates live sealing",!box.ConfirmCustodyPass() && ids.All(inventory.IsInInventory) && box.Contents.ItemCount==0);
            var warning=box.GetComponentsInChildren<TMPro.TMP_Text>(true).Single(t=>t.name=="Transfer warning");
            Check("Validation warning has a separate visible header row",warning.gameObject.activeInHierarchy && !string.IsNullOrEmpty(warning.text)
                && warning.transform.IsChildOf(box.GetComponentInChildren<ResponsivePanelLayout>(true).Header.Content));
            knife.swabSampleSealed=true; box.HideReview(); yield return null;
            Check("Review reopens after validation failure",box.RequestCustodyPass());
            yield return new WaitForSecondsRealtime(.3f);
            var text=box.GetComponentsInChildren<TMPro.TMP_Text>(true).First(t=>t.name=="Items");
            Check("Review lists every item by its player label without exposing internal IDs",ids.All(id=>text.text.Contains(inventory.GetDefinition(id).PlayerDisplayName))
                && !text.text.Contains("EVD-014") && !text.text.Contains("EVD-015") && !text.text.Contains("EVD-018"));
            Check("Flexible padded review uses existing scroll layout",box.GetComponentInChildren<ResponsivePanelLayout>(true)!=null && text.fontSize>=28 && text.enableWordWrapping);
            var layout=box.GetComponentInChildren<ResponsivePanelLayout>(true);
            text.ForceMeshUpdate();
            Check("Item list fits or scrolls without text overflow",!text.isTextOverflowing && text.rectTransform.rect.height>=text.preferredHeight-.5f);
            var actions=layout.Footer.Content.Find("Custody actions") as RectTransform;
            var confirm=actions.Find("Transfer all items") as RectTransform;
            var cancelButton=actions.Find("Cancel transfer") as RectTransform;
            Check("Separate transfer and cancel buttons stay in the fixed footer",actions!=null && confirm!=null && cancelButton!=null
                && !actions.IsChildOf(layout.Body.Content) && confirm.rect.width>cancelButton.rect.width);
            Canvas.ForceUpdateCanvases();
            Check("Both action labels fit their buttons",actions.GetComponentsInChildren<TMPro.TMP_Text>().All(t=>{t.ForceMeshUpdate();return !t.isTextOverflowing;}));
            CaptureWaypoint(head.GetComponent<Camera>(),"docs/evidence/custody-ui-2026-10-23","custody-review.png");
            var buttonPosition=confirm.position;
            layout.ScrollOptions(10000); Canvas.ForceUpdateCanvases();
            Check("Scrolling evidence keeps the action buttons stationary",Vector3.Distance(confirm.position,buttonPosition)<.00001f);
            CaptureWaypoint(head.GetComponent<Camera>(),"docs/evidence/custody-ui-2026-10-23","custody-review-scrolled.png");
            var rotation=layout.transform.rotation; layout.transform.rotation=Quaternion.Euler(25,57,0);
            Check("Rotating the review preserves uniform readable scale",Vector3.Distance(layout.transform.lossyScale,Vector3.one*.001f)<.00001f);
            layout.transform.rotation=rotation;
            eyePosition=head.position; head.position+=Vector3.right*50;
            Check("Confirmation revalidates box proximity",!box.ConfirmCustodyPass() && ids.All(inventory.IsInInventory)); head.position=eyePosition;
            yield return new WaitForSecondsRealtime(.3f);
            var observedAtomic=true; inventory.OnInventoryChanged+=()=>observedAtomic &= ids.All(inventory.IsDeposited);
            Check("Confirm transfers whole set",box.ConfirmCustodyPass());
            Check("Inventory observer sees complete transaction",observedAtomic);
            Check("Pass complete and inventory no longer selectable",box.PassComplete && inventory.AllCustodyDeposited && !inventory.AllEntryIds.Any());
            guide.Refresh();Check("Completed custody pass removes the destination",!guide.DestinationActive && !guide.IsVisible);
            foreach(var id in ids)
            {
                Check(id+": tracked custody and box provenance retained",inventory.IsDeposited(id) && inventory.GetDepositedBoxId(id)==box.BoxId && inventory.GetDefinition(id)!=null);
                Check(id+": visible representation inside box",box.Contents.ItemBounds.TryGetValue(id,out var bounds) && box.Contents.Interior.Contains(bounds.min) && box.Contents.Interior.Contains(bounds.max));
            }
            var allBounds=box.Contents.ItemBounds.ToArray();
            bool overlap=false;
            for(int a=0;a<allBounds.Length;a++) for(int b=a+1;b<allBounds.Length;b++) overlap |= allBounds[a].Value.Intersects(allBounds[b].Value);
            Check("Visible stacks and rows do not overlap",!overlap && allBounds.Length==6);
            Check("Display has no evidence gameplay behaviours",box.Contents.GetComponentsInChildren<EvidenceProp>().Length==0 && box.Contents.GetComponentsInChildren<Collider>().All(c=>c.gameObject==box.gameObject));
            Check("Derived sample source identities preserved",inventory.GetSampleSource("blood-sample:EVD-014")=="EVD-014" && inventory.GetSampleSource("fingerprint-sample:EVD-018")=="EVD-018");
            Check("Body stays outside evidence-box custody",state.GetRecord("BODY-A").custodyLocation!=EvidenceCustodyLocation.EvidenceBox);
            Check("Deposited bags and samples cannot be re-equipped",ids.All(id=>!inventory.EquipHeld(id,hand)) && inventory.AllCustodyDeposited);
            CaptureBox(box);
            Check("Double confirmation and empty retry do nothing",!box.ConfirmCustodyPass() && !box.RequestCustodyPass() && box.Contents.ItemCount==6);
            SessionLogger.Instance.Flush();
            var events=SessionLogReader.ReadEvents(SessionLogger.Instance.ActiveLogFilePath);
            var deposits=events.Where(e=>e.eventType=="EvidenceDeposited").ToList();
            Check("Exactly one deposit per actual item and one pass event",deposits.Count==6 && deposits.Select(e=>e.targetId).Distinct().Count()==6 && events.Count(e=>e.eventType=="CustodyPassCompleted")==1);
            Check("Per-item custody events record box, time, source and non-scored flag",deposits.All(e=>e.timestampMs>=0 && e.payload.Any(p=>p.key=="boxId"&&p.value==box.BoxId)
                && e.payload.Any(p=>p.key=="previousCustodyLocation"&&p.value=="Inventory") && e.payload.Any(p=>p.key=="sourceEvidenceId") && e.payload.Any(p=>p.key=="scored"&&p.value=="false")));
            var config=ScriptableObject.CreateInstance<AssessmentUploadConfiguration>(); config.uploadRawEventLogEnabled=false;
            var scenario=Resources.FindObjectsOfTypeAll<ScenarioDefinition>().First(s=>s.scenarioId==FinalDeductionAssessment.Rules.scenarioId);
            var dto=AssessmentSessionBuilder.Build(SessionLogger.Instance.ActiveLogFilePath,config,scenario,"",""); Destroy(config);
            Check("Essential upload retains custody with raw events disabled",!dto.includesRawEventLog && dto.orderedEvents.Count(e=>e.eventType=="EvidenceDeposited")==6
                && dto.orderedEvents.Count(e=>e.eventType=="CustodyPassCompleted")==1);
            System.IO.Directory.CreateDirectory("docs/evidence/custody-pass");
            System.IO.File.WriteAllText("docs/evidence/custody-pass/upload-wire.json",EducationalSessionRecord.Serialize(dto,true));
            yield return null; yield return null;
            Check("Custody completion enables existing final inventory review",closeout.CustodyPassComplete && (closeout.IsShowing || closeout.ShowInventoryReview()));
            Check("Final review shows box custody",closeout.ReviewText.Contains("In evidence box"));
            closeout.HidePanel();
            string session=SessionLogger.Instance.SessionId; start.RestartSelectedTest();
            for(int i=0;i<12;i++) yield return null;
            Check("Restart restores six stored items and empty box",SceneTestStart.Instance.IsReady && SessionLogger.Instance.SessionId!=session && EvidenceInventory.Instance.AllEntryIds.Count()==6
                && !EvidenceBoxDepositZone.Instance.PassComplete && EvidenceBoxDepositZone.Instance.Contents.ItemCount==0);
            Check("Restart releases prior modal input",!PlayerUIGate.IsBlocked && !ToolInputLock.HasUIOwners);
        }
        finally { SessionState.SetInt(SceneTestStart.EditorSelectionKey,original); SessionState.SetBool("ViCSI.SceneTestStart.Diagnostic",false); }
        report.AppendLine(failures==0?"=== PASS, 0 failures ===":"=== FAIL: "+failures+" failures ==="); Report=report.ToString(); IsDone=true;
    }
    private void CaptureBox(EvidenceBoxDepositZone box)
    {
        var cameraObject=new GameObject("Custody contents proof camera"); var camera=cameraObject.AddComponent<Camera>(); camera.enabled=false;
        camera.transform.position=box.transform.position+new Vector3(.12f,.85f,-.35f); camera.transform.LookAt(box.transform.position);
        camera.fieldOfView=40; camera.nearClipPlane=.01f;
        var target=new RenderTexture(1200,900,24); camera.targetTexture=target;
        var active=RenderTexture.active; camera.Render(); RenderTexture.active=target;
        var texture=new Texture2D(1200,900,TextureFormat.RGB24,false); texture.ReadPixels(new Rect(0,0,1200,900),0,0); texture.Apply();
        System.IO.Directory.CreateDirectory("docs/evidence/custody-pass");
        System.IO.File.WriteAllBytes("docs/evidence/custody-pass/box-contents.png",texture.EncodeToPNG());
        RenderTexture.active=active; camera.targetTexture=null; target.Release(); Destroy(target); Destroy(texture); Destroy(cameraObject);
    }
    private void CaptureWaypoint(Camera camera,string folder="docs/evidence/evidence-box-waypoint-2026-10-20",string filename="waypoint-preview.png")
    {
        var oldTarget=camera.targetTexture;var active=RenderTexture.active;
        var target=RenderTexture.GetTemporary(1200,900,24);var texture=new Texture2D(1200,900,TextureFormat.RGB24,false);
        try
        {
            camera.targetTexture=target;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
            texture.ReadPixels(new Rect(0,0,1200,900),0,0);texture.Apply();
            System.IO.Directory.CreateDirectory(folder);
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder,filename),texture.EncodeToPNG());
        }
        finally {camera.targetTexture=oldTarget;RenderTexture.active=active;RenderTexture.ReleaseTemporary(target);Destroy(texture);}
    }
}
#endif
