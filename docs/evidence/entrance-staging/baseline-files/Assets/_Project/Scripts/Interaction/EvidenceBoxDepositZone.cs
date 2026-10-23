using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Explicit, all-or-nothing custody pass at the physical evidence box.</summary>
public sealed class EvidenceBoxDepositZone : MonoBehaviour
{
    // Retained serialized fields preserve existing scenes and box configuration.
    public enum DepositVisualMode { LogicalOnly, ActivatePredefinedSlot, SpawnSlotRepresentation }
    public static EvidenceBoxDepositZone Instance { get; private set; }
    [SerializeField] private string boxId="EvidenceBox";
    [SerializeField] private Collider triggerCollider;
    [SerializeField] private RequirementNode additionalRequirement=new();
    [SerializeField, HideInInspector] private DepositVisualMode depositVisualMode;
    [SerializeField, HideInInspector] private Transform[] visualSlots=new Transform[0];
    [SerializeField, HideInInspector] private GameObject genericDepositedVisualFallback;
    private InputAction accept, cancel, scroll;
    private GameObject panel;
    private TMP_Text body;
    private WorldPanelPointer pointer;
    private ResponsivePanelLayout layout;
    private List<string> reviewed;
    private bool completed, ownsPrompt, ownsUI;
    private float inputAfter;
    public EvidenceBoxContentsDisplay Contents { get; private set; }
    public bool IsShowing => panel!=null && panel.activeSelf;
    public bool PassComplete => completed && EvidenceInventory.Instance?.AllCustodyDeposited==true;
    public string BoxId => boxId;
    public bool PlayerInRange
    {
        get
        {
            if(Camera.main==null || triggerCollider==null) return false;
            var position=Camera.main.transform.position; position.y=triggerCollider.bounds.center.y;
            return triggerCollider.bounds.Contains(position);
        }
    }
    private bool EligiblePhase => SceneTurnover.Phase==ScenarioPhase.SOCOCloseout || SceneTurnover.Phase==ScenarioPhase.EvidenceInventoryReview;
    private bool WorldAvailable => !PlayerUIGate.IsBlocked && !ToolInputLock.HasUIOwners && STCSNotificationUI.Instance?.IsShowing!=true;
    private void Awake()
    {
        Instance=this;
        if(triggerCollider==null) triggerCollider=GetComponent<Collider>();
        if(triggerCollider!=null) triggerCollider.isTrigger=true;
        Contents=GetComponent<EvidenceBoxContentsDisplay>() ?? gameObject.AddComponent<EvidenceBoxContentsDisplay>();
        accept=PlayerUIGate.CreateConfirmAction(this,"CustodyPass_A",()=>IsShowing || (PlayerInRange && EligiblePhase && WorldAvailable && !PassComplete),200);
        cancel=new InputAction("CustodyPass_B",InputActionType.Button,"<XRController>{RightHand}/secondaryButton");
        scroll=new InputAction("CustodyPass_Scroll",InputActionType.Value,"<XRController>{RightHand}/thumbstick",expectedControlType:"Vector2");
        BuildUI();
    }
    private void OnEnable() { accept.Enable(); cancel.Enable(); scroll.Enable(); }
    private void Start()
    {
        if(FingerprintComparisonStation.Instance!=null)return;
        var desk=new GameObject("Fingerprint Comparison - Training Station");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(desk,gameObject.scene);
        // Independent of the crate's authored nonuniform scale. Near the submission
        // area, with a standing approach south of the controlled scene footprint.
        desk.transform.SetPositionAndRotation(new Vector3(transform.position.x+1.65f,0,transform.position.z-3.2f),Quaternion.identity);
        desk.AddComponent<FingerprintComparisonStation>();
    }
    private void OnDisable() { accept.Disable(); cancel.Disable(); scroll.Disable(); HideReview(); HidePrompt(); }
    private void OnDestroy()
    {
        accept?.Dispose(); cancel?.Dispose(); scroll?.Dispose(); pointer?.Dispose();
        if(Instance==this) Instance=null;
    }
    private void Update()
    {
        if(IsShowing)
        {
            if(!PlayerInRange || !EligiblePhase || PlayerUIGate.IsBlockedByOther(this) || cancel.WasPressedThisFrame()) { HideReview(); return; }
            pointer.Tick();
            float axis=scroll.ReadValue<Vector2>().y;
            if(Mathf.Abs(axis)>.25f) layout.ScrollOptions(axis*350*Time.unscaledDeltaTime);
            if(Time.unscaledTime>=inputAfter && PlayerUIGate.TryConsumeConfirm(this,accept)) ConfirmCustodyPass();
            return;
        }
        bool show=PlayerInRange && EligiblePhase && WorldAvailable && !PassComplete && EvidenceInventory.Instance?.CurrentHeldEvidenceId==null && EvidenceInventory.Instance?.AllEntryIds.Any()==true;
        if(show && !ownsPrompt) { NotificationManager.ShowPrompt("[A] Transfer evidence to box ("+EvidenceInventory.Instance.AllEntryIds.Count()+" items)",new Vector3(0,-.16f,0)); ownsPrompt=true; }
        else if(!show) HidePrompt();
        if(show && PlayerUIGate.TryConsumeConfirm(this,accept)) RequestCustodyPass();
    }
    private void HidePrompt() { if(!ownsPrompt) return; ownsPrompt=false; NotificationManager.HidePrompt(); }
    public bool IsShowingDepositPromptFor(string evidenceId)=>false; // Held items must first be stored.
    private string BlockReason(IReadOnlyList<string> ids)
    {
        if(!EligiblePhase || !PlayerInRange) return "Stand beside the evidence box during closeout.";
        if(SOCOCloseout.Instance?.AllAccountedFor!=true) return "Finish processing and securely store every item before the custody pass.";
        if(!RequirementEvaluator.IsSatisfied(additionalRequirement)) return "The evidence box is not ready for transfer.";
        var inventory=EvidenceInventory.Instance;
        if(inventory==null) return "Evidence inventory is unavailable.";
        var reason=inventory.CustodyTransferBlockReason(ids);
        if(reason!=null) return reason;
        foreach(var id in ids)
            if(!RequirementEvaluator.IsSatisfied(inventory.GetDefinition(id).depositRequirement)) return "Complete the transfer requirements for "+inventory.GetDefinition(id).PlayerDisplayName+".";
        return null;
    }
    public bool RequestCustodyPass()
    {
        if(IsShowing || !WorldAvailable || !EligiblePhase || !PlayerInRange || PassComplete || EvidenceInventory.Instance==null) return false;
        reviewed=EvidenceInventory.Instance.AllEntryIds.OrderBy(id=>id,System.StringComparer.Ordinal).ToList();
        var reason=BlockReason(reviewed);
        if(reason!=null) { NotificationManager.Notify(reason, DialogueKind.Confirmation); return false; }
        body.text=ReviewText(); layout.Refresh(); HidePrompt();
        WorldPanelPlacement.PlaceInFront(panel.transform);
        HudFollowCamera.ConfigureShared(panel,this,PlayerUIGate.CriticalPriority);
        panel.SetActive(true); pointer.Enable();
        ownsUI=true; PlayerUIGate.Enter(this); ToolInputLock.Enter(this); LocomotionSuspender.Suspend();
        inputAfter=Time.unscaledTime+.25f;
        return true;
    }
    public bool ConfirmCustodyPass()
    {
        if(!IsShowing || Time.unscaledTime<inputAfter || PlayerUIGate.IsBlockedByOther(this)) return false;
        var reason=BlockReason(reviewed);
        if(reason!=null)
        {
            reviewed=EvidenceInventory.Instance.AllEntryIds.OrderBy(id=>id,System.StringComparer.Ordinal).ToList();
            body.text=reason+"\n\n"+ReviewText(); layout.Refresh(); inputAfter=Time.unscaledTime+.25f; return false;
        }
        var inventory=EvidenceInventory.Instance;
        var allBoxIds=inventory.AllTrackedEntryIds.Where(id=>inventory.GetDepositedBoxId(id)==boxId).Concat(reviewed).ToList();
        GameObject staged;
        try { staged=Contents.Stage(allBoxIds); }
        catch(System.Exception error) { Debug.LogException(error); body.text="Unable to prepare the box contents. Please try again.\n\n"+ReviewText(); layout.Refresh(); return false; }
        if(!inventory.TransferStoredToBox(boxId,reviewed,out reason)) { Destroy(staged); return false; }
        Contents.Commit(staged); completed=true;
        SessionLogger.Instance?.LogEvent(SessionEventType.CustodyPassCompleted,boxId,new Dictionary<string,string> {
            {"boxId",boxId},{"itemCount",reviewed.Count.ToString()},{"evidenceIds",string.Join(",",reviewed)},{"scored","false"} });
        SessionLogger.Instance?.Flush();
        HideReview(); SOCOCloseout.Instance?.NotifyCustodyPassCompleted();
        NotificationManager.Notify("Custody pass complete. Evidence secured in the box.", DialogueKind.Confirmation);
        return true;
    }
    public void HideReview()
    {
        if(panel!=null) panel.SetActive(false);
        pointer?.Disable();
        if(!ownsUI) return;
        ownsUI=false; PlayerUIGate.Exit(this); ToolInputLock.Exit(this); LocomotionSuspender.Resume();
    }
    private string ReviewText()=>"Confirm transfer of all "+reviewed.Count+" stored items to the evidence box.\n\n"+
        string.Join("\n\n",reviewed.Select(id=>"<b>"+EvidenceInventory.Instance.GetDefinition(id).PlayerDisplayName+"</b>\nSealed and stored"));
    private void BuildUI()
    {
        // Cancel the crate's nonuniform scale before the HUD rotates toward the headset.
        var anchor=new GameObject("CustodyReviewAnchor").transform; anchor.SetParent(transform,false);
        var scale=transform.lossyScale; anchor.localScale=new Vector3(1/scale.x,1/scale.y,1/scale.z);
        panel=new GameObject("CustodyPassReview",typeof(RectTransform),typeof(Canvas)); panel.transform.SetParent(anchor,false);
        var rect=(RectTransform)panel.transform; rect.sizeDelta=new Vector2(900,740); rect.localScale=Vector3.one*.001f;
        var canvas=panel.GetComponent<Canvas>(); canvas.renderMode=RenderMode.WorldSpace; canvas.sortingOrder=short.MaxValue-4;
        var bg=ForensicUIStyle.Rect(panel.transform,"Background",ForensicUIStyle.PanelBackground).rectTransform;
        bg.anchorMin=Vector2.zero; bg.anchorMax=Vector2.one; bg.offsetMin=bg.offsetMax=Vector2.zero;
        var title=Label("Title",36,"Evidence custody pass"); body=Label("Items",29,""); body.alignment=TextAlignmentOptions.TopLeft; body.lineSpacing=8;
        var action=Label("Confirm",29,"[A] Confirm transfer to evidence box");
        var controls=Label("Controls",23,"Right stick: scroll   ·   [B] Cancel");
        pointer=new WorldPanelPointer(rect,"CustodyPass",this);
        layout=ResponsivePanelLayout.Create(rect,title,null,controls,740,900,40);
        layout.AddText(body,layout.Body,48);
        var row=layout.AddText(action,layout.Footer,55); row.SetAsFirstSibling();
        pointer.Register("custody-confirm",row,Vector2.zero,()=>ConfirmCustodyPass());
        layout.RegisterControls(pointer,"layout-custody-"); panel.SetActive(false);
    }
    private TMP_Text Label(string name,float size,string text)
    {
        var label=ForensicUIStyle.Text(panel.transform,name,size,ForensicUIStyle.TextPrimary);
        label.rectTransform.sizeDelta=new Vector2(820,55); label.alignment=TextAlignmentOptions.Center;
        label.enableWordWrapping=true; label.text=text; return label;
    }
}
