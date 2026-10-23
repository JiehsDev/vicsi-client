using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>One protected drying position. The original inventory item keeps its source and custody.</summary>
public sealed class SamplePreparationStation : MonoBehaviour
{
    public static SamplePreparationStation Instance { get; private set; }
    private SceneCordon cordon;
    private InputAction confirm;
    private Text instruction;
    private Canvas instructionCanvas;
    private DryingProgressGraphic progress;
    private Transform rack, carrier, homeParent;
    private Vector3 homePosition, homeScale;
    private Quaternion homeRotation;
    private GameObject dryingVisual;
    private readonly Dictionary<Renderer,bool> carrierRenderers=new();
    private bool promptShown;
    private readonly List<Material> materials = new();
    private SwabTool Swab => SwabTool.Instance;
    private bool DialoguePending => STCSNotificationUI.Instance != null
        && (STCSNotificationUI.Instance.IsShowing || STCSNotificationUI.Instance.PendingCount > 0);
    public bool GuidanceNeeded => isActiveAndEnabled && Swab != null && Swab.NeedsPreparation
        && (SceneTurnover.Phase == ScenarioPhase.SOCOInvestigation || SceneTurnover.Phase == ScenarioPhase.SOCOCloseout) && !SceneTurnover.IsPending;
    public bool IsInRange => Camera.main != null && Vector2.Distance(
        new Vector2(Camera.main.transform.position.x,Camera.main.transform.position.z),
        new Vector2(transform.position.x,transform.position.z)) <= 1.25f;
    public bool CanInteract => GuidanceNeeded && IsInRange && Camera.main != null
        && cordon != null && cordon.isActiveAndEnabled && !cordon.RequiresBriefingAt(Camera.main.transform.position)
        && (cordon.AccessRegister == null || !cordon.AccessRegister.IsInside)
        && !PlayerUIGate.IsBlockedByOther(this) && !ToolInputLock.HasUIOwners
        && !DialoguePending
        && (Swab.PendingContainerId != null
            ? EvidenceInventory.Instance?.CurrentHeldEvidenceId == Swab.PendingContainerId
            : PlayerToolRegistry.CurrentRole == ToolType.Swab);
    public string Instruction => Swab == null || !Swab.NeedsPreparation ? GameDialogue.Text("sample-station-idle")
        : GameDialogue.Text(Swab.Preparation == SwabTool.PreparationStage.NeedsDrying ? "sample-start-drying"
            : Swab.Preparation == SwabTool.PreparationStage.Dried ? "sample-confirm-package" : "sample-drying");
    public float Progress => Swab != null ? Swab.DryingProgress : 0;
    public bool IsActionDisplayVisible => instructionCanvas != null && instructionCanvas.enabled;
    private bool ActionReady => CanInteract && Swab.Preparation != SwabTool.PreparationStage.Drying;
    public void Initialize(SceneCordon owner) { cordon = owner; }
    private void Awake()
    {
        Instance = this; cordon = FindAnyObjectByType<SceneCordon>();
        confirm = PlayerUIGate.CreateConfirmAction(this,"SamplePreparation",()=>ActionReady,PlayerUIGate.InformationPriority+20);
        BuildVisuals();
    }
    private void OnEnable() { confirm.Enable(); }
    private void OnDisable() { RestoreCarrier(); PlayerUIGate.Exit(this); ClearPrompt(); confirm.Disable(); if (instructionCanvas != null) instructionCanvas.enabled=false; }
    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        confirm?.Dispose(); foreach(var material in materials) if(material!=null) Destroy(material);
    }
    private void Update()
    {
        if (Swab != null && Swab.IsInPreparationCabinet)
        {
            ParkCarrier();
            // Keep custody stable while the sample is on the rack. Other modal owners still pause drying.
            // Spoken guidance pauses its own timer behind a UI owner. Yield our token
            // while it plays so both the dialogue and drying can subsequently resume.
            if(IsInRange && !DialoguePending)PlayerUIGate.Enter(this);else PlayerUIGate.Exit(this);
            Swab.AdvanceDrying(this,Time.unscaledDeltaTime);
        }
        else PlayerUIGate.Exit(this);
        RefreshDisplay();
        if (!ActionReady) { ClearPrompt(); return; }
        NotificationManager.ShowPrompt(Instruction); promptShown=true;
        if(PlayerUIGate.TryConsumeConfirm(this,confirm)) Interact();
    }
    public bool Interact()
    {
        if(!CanInteract) return false;
        if(Swab.Preparation == SwabTool.PreparationStage.NeedsDrying)
        {
            if(Swab.PreparationCarrier==null || !Swab.BeginDrying(this)) return false;
            ParkCarrier();PlayerUIGate.Enter(this);ClearPrompt();RefreshDisplay();return true;
        }
        if(Swab.Preparation != SwabTool.PreparationStage.Dried || !Swab.ConfirmPreparedPackage(this)) return false;
        RestoreCarrier();PlayerUIGate.Exit(this);
        // The wrapper stays with the inventory-owned visual through storage and re-equipping.
        var visual=Swab.PreparationCarrier;
        if(visual!=null && Swab.PendingContainerId!=null) PaperSwabPackage.Apply(visual,Swab.PreparationSourceId);
        ClearPrompt();RefreshDisplay();GameDialogue.Say("sample-package-ready");return true;
    }
    private void ParkCarrier()
    {
        if(carrier!=null) return;
        carrier=Swab.PreparationCarrier;if(carrier==null)return;
        homeParent=carrier.parent;homePosition=carrier.localPosition;homeRotation=carrier.localRotation;homeScale=carrier.localScale;
        carrier.SetParent(rack,true);carrier.position=rack.position;carrier.rotation=rack.rotation;
        dryingVisual=Swab.CreateDryingVisual(rack);
        if(dryingVisual!=null)
            foreach(var renderer in carrier.GetComponentsInChildren<Renderer>())
            { carrierRenderers[renderer]=renderer.enabled;renderer.enabled=false; }
    }
    private void RestoreCarrier()
    {
        if(carrier!=null)
        {
            carrier.SetParent(homeParent,false);carrier.localPosition=homePosition;carrier.localRotation=homeRotation;carrier.localScale=homeScale;
        }
        carrier=null;
        foreach(var entry in carrierRenderers)if(entry.Key!=null)entry.Key.enabled=entry.Value;
        carrierRenderers.Clear();
        if(dryingVisual!=null)Destroy(dryingVisual);dryingVisual=null;
    }
    public void RefreshDisplay()
    {
        string source=Swab?.PreparationSourceId;
        string sourceLabel=source==null?"":EvidenceDisplay.Label(source)+"\n";
        instruction.text="BLOOD EVIDENCE DRYING\n"+sourceLabel+Instruction;
        // Graphic.canvas can return null after its Canvas is disabled. Keep the actual component.
        if (instructionCanvas == null) instructionCanvas=instruction.GetComponentInParent<Canvas>(true);
        if (instructionCanvas != null) instructionCanvas.enabled=CanInteract;
        progress.gameObject.SetActive(CanInteract && Swab!=null && Swab.IsInPreparationCabinet);
        progress.SetProgress(Progress);
    }
    private void ClearPrompt() { if(promptShown)NotificationManager.HidePrompt();promptShown=false; }
    private Material Mat(string name,Color color)
    { var material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};material.SetColor("_BaseColor",color);materials.Add(material);return material; }
    private void Part(string name,Vector3 at,Vector3 size,Material material)
    {
        var go=EditableSceneVisuals.Primitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(transform,false);
        go.transform.localPosition=at;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;
    }
    [EditableSceneBuilder]
    private void BuildVisuals()
    {
        using (EditableSceneVisuals.Begin(this,"BuildVisuals"))
        {
        var frame=Mat("Protected preparation cabinet",new Color(.08f,.15f,.19f));
        var clean=Mat("Individual drying position",new Color(.85f,.89f,.88f));
        Part("Separate preparation tabletop",new Vector3(0,.84f,0),new Vector3(.72f,.06f,.5f),frame);
        foreach(float x in new[]{-.30f,.30f})foreach(float z in new[]{-.2f,.2f})
            Part("Preparation table leg",new Vector3(x,.405f,z),new Vector3(.045f,.81f,.045f),frame);
        Part("Protective cabinet roof",new Vector3(0,1.37f,.03f),new Vector3(.7f,.04f,.45f),frame);
        Part("Protective cabinet back",new Vector3(0,1.12f,.24f),new Vector3(.7f,.50f,.035f),frame);
        foreach(float x in new[]{-.33f,.33f})Part("Protective cabinet side",new Vector3(x,1.12f,.03f),new Vector3(.035f,.5f,.45f),frame);
        Part("Single labelled drying holder",new Vector3(0,.89f,0),new Vector3(.25f,.04f,.17f),clean);
        rack=EditableSceneVisuals.Create("Source-specific drying position").transform;rack.SetParent(transform,false);rack.localPosition=new Vector3(0,.96f,0);
        var panel=EditableSceneVisuals.Create("Preparation instructions",typeof(RectTransform),typeof(Canvas));panel.transform.SetParent(transform,false);
        panel.transform.localPosition=new Vector3(0,1.63f,.12f);panel.transform.localScale=Vector3.one*.0013f;
        panel.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;((RectTransform)panel.transform).sizeDelta=new Vector2(540,310);
        var backing=EditableSceneVisuals.Create("Backing",typeof(RectTransform),typeof(Image));backing.transform.SetParent(panel.transform,false);
        ((RectTransform)backing.transform).sizeDelta=new Vector2(540,310);backing.GetComponent<Image>().color=new Color(.025f,.05f,.075f,.98f);backing.GetComponent<Image>().raycastTarget=false;
        var text=EditableSceneVisuals.Create("Instruction",typeof(RectTransform),typeof(Text));text.transform.SetParent(panel.transform,false);
        ((RectTransform)text.transform).sizeDelta=new Vector2(510,210);((RectTransform)text.transform).anchoredPosition=new Vector2(0,42);
        instruction=text.GetComponent<Text>();instruction.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");instruction.fontSize=25;
        instruction.alignment=TextAnchor.MiddleCenter;instruction.color=Color.white;instruction.raycastTarget=false;
        var ring=EditableSceneVisuals.Create("Drying progress",typeof(RectTransform),typeof(DryingProgressGraphic));ring.transform.SetParent(panel.transform,false);
        ((RectTransform)ring.transform).sizeDelta=new Vector2(74,74);((RectTransform)ring.transform).anchoredPosition=new Vector2(0,-106);
        progress=ring.GetComponent<DryingProgressGraphic>();progress.raycastTarget=false;RefreshDisplay();
            }
        RefreshDisplay();
    }
}
