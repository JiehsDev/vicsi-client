using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Compare secured lift cards; confirmed correspondence resolves the case identification.</summary>
public sealed class FingerprintComparisonStation : MonoBehaviour
{
    public static FingerprintComparisonStation Instance { get; private set; }
    private readonly HashSet<string> completed=new();
    private readonly Dictionary<string,int> attempts=new();
    private readonly List<Material> materials=new();
    private readonly List<TMP_Text> labels=new();
    private readonly List<Image> referenceButtons=new();
    private Image nextSampleButton;
    private InputAction confirm,cancel,stick;
    private SceneCordon cordon;
    private GameObject panel;
    private TMP_Text sourceLabel,feedback,idleLabel;
    private PracticeFingerprintGraphic recovered,reference;
    private WorldPanelPointer pointer;
    private float nextSelection,inputAfter;
    private bool promptShown,ownsUI;
    private string sampleId;
    private bool showFeatures,success;
    private float recoveredZoom=1,referenceZoom=1,recoveredRotation=20,referenceRotation;
    public int SelectedReference { get; private set; }
    public bool IsShowing => panel!=null&&panel.activeSelf;
    public bool IsActionPromptVisible => idleLabel!=null&&idleLabel.gameObject.activeInHierarchy;
    public string CurrentSampleId => sampleId;
    public string CurrentSourceId => EvidenceInventory.Instance?.GetSampleSource(sampleId);
    public string Feedback => feedback!=null?feedback.text:"";
    public RectTransform Panel => panel!=null?(RectTransform)panel.transform:null;
    public WorldPanelPointer Pointer => pointer;
    public PracticeFingerprintGraphic RecoveredGraphic => recovered;
    public PracticeFingerprintGraphic ReferenceGraphic => reference;
    public IReadOnlyList<TMP_Text> Labels => labels;
    public bool IsComplete(string sourceId) => sourceId!=null&&completed.Contains(sourceId);
    public int AttemptsFor(string sourceId) => sourceId!=null&&attempts.TryGetValue(sourceId,out int count)?count:0;
    public bool IsInRange => Camera.main!=null&&Vector2.Distance(new Vector2(Camera.main.transform.position.x,Camera.main.transform.position.z),
        new Vector2(transform.position.x,transform.position.z))<=1.65f;
    public List<string> AvailableSamples => EvidenceInventory.Instance==null?new List<string>():EvidenceInventory.Instance.AllTrackedEntryIds
        .Where(id=>IsEligibleSample(id)&&!EvidenceStateManager.Instance.IsFingerprintMatched(EvidenceInventory.Instance.GetSampleSource(id)))
        .OrderBy(id=>id,System.StringComparer.Ordinal).ToList();
    private bool IsEligibleSample(string id)
    {
        var inventory=EvidenceInventory.Instance;
        return inventory!=null&&inventory.IsFingerprintSample(id)&&inventory.IsSampleSealed(id)
            &&ProcedureInterlock.AllowsFingerprintWork(inventory.GetSampleSource(id))
            &&(inventory.CurrentHeldEvidenceId==id || (inventory.IsExplicitlyStoredSample(id)&&inventory.IsSecuredCustody(id)))
            &&EvidenceStateManager.Instance?.GetRecord(inventory.GetSampleSource(id))!=null;
    }
    private bool ProcessingPhase => SceneTurnover.Phase==ScenarioPhase.SOCOInvestigation || SceneTurnover.Phase==ScenarioPhase.SOCOCloseout;
    public bool GuidanceNeeded => isActiveAndEnabled&&!SceneTurnover.IsPending&&ProcessingPhase
        &&AvailableSamples.Any(id=>!IsComplete(EvidenceInventory.Instance.GetSampleSource(id)));
    private bool ContextAvailable => isActiveAndEnabled&&!SceneTurnover.IsPending&&ProcessingPhase
        &&!SceneTurnover.IsInvestigationBlocked&&IsInRange&&cordon!=null&&!cordon.RequiresBriefingAt(Camera.main.transform.position)
        &&(cordon.AccessRegister==null||!cordon.AccessRegister.IsInside)
        &&!PlayerUIGate.IsBlockedByOther(this)
        &&(IsShowing||STCSNotificationUI.Instance==null||(!STCSNotificationUI.Instance.IsShowing&&STCSNotificationUI.Instance.PendingCount==0));
    public bool CanOpen => ContextAvailable&&!IsShowing&&!ToolInputLock.HasUIOwners&&AvailableSamples.Count>0
        &&HeldItemCoordinator.Instance!=null&&(HeldItemCoordinator.Instance.CurrentCategory==HeldItemCoordinator.HeldCategory.None
            ||AvailableSamples.Contains(EvidenceInventory.Instance.CurrentHeldEvidenceId));
    private bool CanAct => IsShowing&&ContextAvailable&&IsEligibleSample(sampleId);
    private int TargetVariant => CurrentSourceId=="EVD-018"?1:0;
    private int VariantFor(int index) => CurrentSourceId=="EVD-018"?new[]{2,1,0}[index]:new[]{1,2,0}[index];
    private void Awake()
    {
        Instance=this;cordon=FindAnyObjectByType<SceneCordon>();
        confirm=PlayerUIGate.CreateConfirmAction(this,"FingerprintPractice",()=>IsShowing?CanAct:CanOpen,PlayerUIGate.InformationPriority+30);
        cancel=new InputAction("FingerprintPractice_Close",InputActionType.Button,"<XRController>{RightHand}/secondaryButton");
        stick=new InputAction("FingerprintPractice_Reference",InputActionType.Value,"<XRController>{RightHand}/thumbstick");
        BuildDesk();BuildUI();if(idleLabel!=null)idleLabel.gameObject.SetActive(false);
    }
    private void OnEnable(){confirm.Enable();cancel.Enable();stick.Enable();}
    private void OnDisable(){Close();ClearPrompt();confirm.Disable();cancel.Disable();stick.Disable();}
    private void OnDestroy()
    {
        if(Instance==this)Instance=null;confirm?.Dispose();cancel?.Dispose();stick?.Dispose();pointer?.Dispose();
        foreach(var material in materials)if(material!=null)Destroy(material);
    }
    private void Update()
    {
        if(idleLabel!=null)idleLabel.gameObject.SetActive(!IsShowing&&CanOpen);
        if(IsShowing)
        {
            if(!CanAct){Close();return;}
            if(cancel.WasPressedThisFrame()){Close();return;}
            pointer.Tick();
            float direction=stick.ReadValue<Vector2>().x;
            if(Mathf.Abs(direction)>.65f&&Time.unscaledTime>=nextSelection)
            {SelectReference((SelectedReference+(direction>0?1:2))%3);nextSelection=Time.unscaledTime+.28f;}
            if(Time.unscaledTime>=inputAfter&&PlayerUIGate.TryConsumeConfirm(this,confirm))Submit();
        }
        else if(CanOpen)
        {
            NotificationManager.ShowPrompt(GameDialogue.Text("fp-practice-open"));promptShown=true;
            if(PlayerUIGate.TryConsumeConfirm(this,confirm))Open();
        }
        else ClearPrompt();
    }
    public bool Open()
    {
        if(!CanOpen)return false;
        var samples=AvailableSamples;sampleId=samples.FirstOrDefault(id=>!IsComplete(EvidenceInventory.Instance.GetSampleSource(id)))??samples[0];
        var source = EvidenceStateManager.Instance.GetRecord(CurrentSourceId);
        if (!source.fingerprintingDone && EvidenceStateManager.Instance.MarkFingerprinted(CurrentSourceId,ToolType.FingerprintTape)==TransitionResult.Violation)
        { sampleId=null; return false; }
        ResetView();panel.SetActive(true);idleLabel.gameObject.SetActive(false);ClearPrompt();
        ownsUI=true;PlayerUIGate.Enter(this);ToolInputLock.Enter(this);LocomotionSuspender.Suspend();pointer.Enable();
        inputAfter=Time.unscaledTime+.3f;nextSelection=inputAfter;Refresh();
        SessionLogger.Instance?.LogEvent(SessionEventType.FingerprintPracticeOpened,CurrentSourceId);return true;
    }
    public void Close()
    {
        if(panel!=null)panel.SetActive(false);if(idleLabel!=null)idleLabel.gameObject.SetActive(CanOpen);pointer?.Disable();
        if(!ownsUI)return;ownsUI=false;PlayerUIGate.Exit(this);ToolInputLock.Exit(this);LocomotionSuspender.Resume();
    }
    private void ClearPrompt(){if(promptShown)NotificationManager.HidePrompt();promptShown=false;}
    private void ResetView()
    {
        SelectedReference=0;showFeatures=false;success=false;recoveredZoom=referenceZoom=1;recoveredRotation=20;referenceRotation=0;
        feedback.text=GameDialogue.Text("fp-practice-instruction");
    }
    public bool SelectReference(int index)
    {
        if(!CanAct||index<0||index>2)return false;SelectedReference=index;success=false;feedback.text=GameDialogue.Text("fp-practice-instruction");Refresh();return true;
    }
    public bool Hint()
    {
        if(!CanAct)return false;showFeatures=true;feedback.text=GameDialogue.Text("fp-practice-hint");Refresh();return true;
    }
    public bool AdjustView(bool left,float zoomDelta,float rotationDelta)
    {
        if(!CanAct)return false;
        if(left){recoveredZoom=Mathf.Clamp(recoveredZoom+zoomDelta,1,2);recoveredRotation=Mathf.Repeat(recoveredRotation+rotationDelta+180,360)-180;}
        else{referenceZoom=Mathf.Clamp(referenceZoom+zoomDelta,1,2);referenceRotation=Mathf.Repeat(referenceRotation+rotationDelta+180,360)-180;}
        Refresh();return true;
    }
    public bool NextSample()
    {
        if(!CanAct)return false;var samples=AvailableSamples;if(samples.Count<2)return false;
        sampleId=samples[(samples.IndexOf(sampleId)+1)%samples.Count];ResetView();Refresh();return true;
    }
    public bool Submit()
    {
        if(!CanAct)return false;
        attempts[CurrentSourceId]=AttemptsFor(CurrentSourceId)+1;showFeatures=true;
        bool correct=VariantFor(SelectedReference)==TargetVariant;
        if(correct&&!completed.Contains(CurrentSourceId))
        {
            var result=EvidenceStateManager.Instance.MarkMatched(CurrentSourceId);
            correct=result==TransitionResult.Applied||result==TransitionResult.Duplicate;
            if(correct&&completed.Add(CurrentSourceId))
                SessionLogger.Instance?.LogEvent(SessionEventType.FingerprintPracticeCompleted,CurrentSourceId,
                    new Dictionary<string,string>{{"scored","false"},{"sampleId",sampleId}});
        }
        feedback.text=GameDialogue.Text(correct?"fp-practice-success":"fp-practice-again");success=correct;
        Refresh();return correct;
    }
    private void Refresh()
    {
        sourceLabel.text=GameDialogue.Text("fp-practice-source",EvidenceStateManager.Instance.GetDisplayName(CurrentSourceId));
        recovered.Configure(TargetVariant,true,recoveredZoom,recoveredRotation,showFeatures);
        reference.Configure(VariantFor(SelectedReference),false,referenceZoom,referenceRotation,showFeatures);
        for(int i=0;i<3;i++)referenceButtons[i].color=i==SelectedReference?ForensicUIStyle.SelectedFill:ForensicUIStyle.HeaderBackground;
        bool canSwitch=AvailableSamples.Count>1;
        nextSampleButton.color=canSwitch?ForensicUIStyle.HeaderBackground:new Color(.10f,.115f,.145f,.35f);
        pointer.Targets.Single(target=>target.id=="next-sample").enabled=canSwitch;
        feedback.color=success?ForensicUIStyle.Success:ForensicUIStyle.TextPrimary;
    }
    private Material Material(string name,Color color)
    {var material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};material.SetColor("_BaseColor",color);materials.Add(material);return material;}
    private void Part(string name,Vector3 at,Vector3 size,Material material)
    {
        var part=EditableSceneVisuals.Primitive(PrimitiveType.Cube);part.name=name;part.transform.SetParent(transform,false);
        part.transform.localPosition=at;part.transform.localScale=size;part.GetComponent<Renderer>().sharedMaterial=material;
    }
    [EditableSceneBuilder]
    private void BuildDesk()
    {
        using (EditableSceneVisuals.Begin(this,"BuildDesk"))
        {
        var frame=Material("Fingerprint examination desk",new Color(.07f,.12f,.17f));
        var tray=Material("Packaged lift reference tray",new Color(.25f,.4f,.48f));
        Part("Examination desk",new Vector3(0,.82f,0),new Vector3(1.35f,.06f,.64f),frame);
        foreach(float x in new[]{-.60f,.60f})foreach(float z in new[]{-.25f,.25f})Part("Desk leg",new Vector3(x,.395f,z),new Vector3(.05f,.79f,.05f),frame);
        Part("Monitor stand",new Vector3(0,1.12f,.19f),new Vector3(.07f,.56f,.08f),frame);
        Part("Comparison monitor",new Vector3(0,1.51f,.12f),new Vector3(1.18f,.94f,.045f),frame);
        Part("Labelled packaged lift tray",new Vector3(-.46f,.88f,-.12f),new Vector3(.28f,.045f,.23f),tray);
        var traySign=EditableSceneVisuals.Create("Packaged lift tray label",typeof(RectTransform),typeof(Canvas));traySign.transform.SetParent(transform,false);
        traySign.transform.localPosition=new Vector3(-.46f,.88f,-.238f);traySign.transform.localScale=Vector3.one*.001f;
        traySign.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;
        Label("Tray label",GameDialogue.Text("fp-practice-tray"),18,Vector2.zero,new Vector2(260,48),traySign.transform);
        var sign=EditableSceneVisuals.Create("Fingerprint training station sign",typeof(RectTransform),typeof(Canvas));sign.transform.SetParent(transform,false);
        sign.transform.localPosition=new Vector3(0,.70f,-.326f);sign.transform.localScale=Vector3.one*.001f;sign.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;
        var label=ForensicUIStyle.Text(sign.transform,"Sign",27,Color.white,alignment:TextAlignmentOptions.Center);label.rectTransform.sizeDelta=new Vector2(1150,100);label.text=GameDialogue.Text("fp-practice-sign");
            }
        RefreshCaseworkLabels();
    }
    public void RefreshCaseworkLabels()
    {
        foreach(var text in GetComponentsInChildren<TMP_Text>(true))
        {
            string key=text.name switch
            {
                "Sign"=>"fp-practice-sign", "Title"=>"fp-practice-title", "Idle"=>"fp-practice-idle",
                "Controls"=>"fp-practice-controls", "Recovered heading"=>"fp-practice-recovered",
                "hint label"=>"fp-practice-hint-button", "submit label"=>"fp-practice-submit", _=>null
            };
            if(key!=null)text.text=GameDialogue.Text(key);
        }
    }
    private TMP_Text Label(string name,string text,float size,Vector2 position,Vector2 dimensions,Transform parent=null)
    {
        var label=ForensicUIStyle.Text(parent??panel.transform,name,size,ForensicUIStyle.TextPrimary,alignment:TextAlignmentOptions.Center);
        label.rectTransform.anchoredPosition=position;label.rectTransform.sizeDelta=dimensions;label.text=text;labels.Add(label);return label;
    }
    private RectTransform Button(string id,string text,Vector2 position,Vector2 dimensions,System.Action action)
    {
        var background=ForensicUIStyle.Rect(panel.transform,id,ForensicUIStyle.HeaderBackground);var rect=background.rectTransform;
        rect.anchoredPosition=position;rect.sizeDelta=dimensions;
        Label(id+" label",text,23,Vector2.zero,dimensions-Vector2.one*8,rect);
        pointer.Register(id,rect,Vector2.zero,()=>{if(CanAct)action();});return rect;
    }
    private PracticeFingerprintGraphic Print(string name,Vector2 position)
    {
        var background=ForensicUIStyle.Rect(panel.transform,name+" paper",new Color(.94f,.92f,.86f));
        background.rectTransform.anchoredPosition=position;background.rectTransform.sizeDelta=new Vector2(390,330);
        EditableSceneVisuals.Component<RectMask2D>(background.gameObject);
        var go=EditableSceneVisuals.Create(name,typeof(RectTransform),typeof(PracticeFingerprintGraphic));go.transform.SetParent(background.transform,false);
        ((RectTransform)go.transform).sizeDelta=new Vector2(375,320);var graphic=go.GetComponent<PracticeFingerprintGraphic>();graphic.raycastTarget=false;return graphic;
    }
    [EditableSceneBuilder]
    private void BuildUI()
    {
        using (EditableSceneVisuals.Begin(this,"BuildUI"))
        {
        var root=EditableSceneVisuals.Create("Fingerprint comparison monitor UI",typeof(RectTransform),typeof(Canvas));root.transform.SetParent(transform,false);
        root.transform.localPosition=new Vector3(0,1.51f,.093f);root.transform.localScale=Vector3.one*.0011f;
        ((RectTransform)root.transform).sizeDelta=new Vector2(1040,830);root.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;
        root.GetComponent<Canvas>().sortingOrder=short.MaxValue-5;
        panel=EditableSceneVisuals.Create("Practice comparison",typeof(RectTransform));panel.transform.SetParent(root.transform,false);((RectTransform)panel.transform).sizeDelta=new Vector2(1040,830);
        var bg=ForensicUIStyle.Rect(panel.transform,"Background",ForensicUIStyle.PanelBackground);bg.rectTransform.sizeDelta=new Vector2(1040,830);
        pointer=new WorldPanelPointer((RectTransform)panel.transform,"FingerprintPractice",this);
        Label("Title",GameDialogue.Text("fp-practice-title"),32,new Vector2(0,370),new Vector2(990,58));
        sourceLabel=Label("Source","",25,new Vector2(0,316),new Vector2(990,48));
        Label("Recovered heading",GameDialogue.Text("fp-practice-recovered"),25,new Vector2(-255,264),new Vector2(390,40));
        for(int i=0;i<3;i++)
        {
            int index=i;var button=Button("reference-"+i,GameDialogue.Text("fp-practice-reference",((char)('A'+i)).ToString()),new Vector2(115+i*140,264),new Vector2(132,42),()=>SelectReference(index));
            referenceButtons.Add(button.GetComponent<Image>());
        }
        recovered=Print("Recovered print",new Vector2(-255,65));reference=Print("Selected reference print",new Vector2(255,65));
        for(int side=0;side<2;side++)
        {
            bool left=side==0;float center=left?-255:255;
            Button((left?"left":"right")+"-zoom-out","Zoom -",new Vector2(center-148,-137),new Vector2(95,44),()=>AdjustView(left,-.25f,0));
            Button((left?"left":"right")+"-zoom-in","Zoom +",new Vector2(center-49,-137),new Vector2(95,44),()=>AdjustView(left,.25f,0));
            Button((left?"left":"right")+"-rotate-left","Turn <",new Vector2(center+50,-137),new Vector2(95,44),()=>AdjustView(left,0,-10));
            Button((left?"left":"right")+"-rotate-right","Turn >",new Vector2(center+149,-137),new Vector2(95,44),()=>AdjustView(left,0,10));
        }
        feedback=Label("Feedback",GameDialogue.Text("fp-practice-instruction"),26,new Vector2(0,-212),new Vector2(990,90));
        Button("hint",GameDialogue.Text("fp-practice-hint-button"),new Vector2(-365,-300),new Vector2(200,52),()=>Hint());
        Button("submit",GameDialogue.Text("fp-practice-submit"),new Vector2(0,-300),new Vector2(500,52),()=>Submit());
        nextSampleButton=Button("next-sample",GameDialogue.Text("fp-practice-next"),new Vector2(365,-300),new Vector2(200,52),()=>NextSample()).GetComponent<Image>();
        Label("Controls",GameDialogue.Text("fp-practice-controls"),22,new Vector2(0,-366),new Vector2(1000,68));
        panel.SetActive(false);
        idleLabel=Label("Idle",GameDialogue.Text("fp-practice-idle"),34,Vector2.zero,new Vector2(970,730),root.transform);
            }
        RefreshCaseworkLabels();
    }
}
