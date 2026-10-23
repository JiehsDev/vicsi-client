using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Session-local arrival PPE, independent of held tools and evidence.</summary>
[DisallowMultipleComponent]
public sealed class ArrivalPPEStation : MonoBehaviour
{
    public enum GloveState { NotWorn, Clean, NeedsChange }
    public static ArrivalPPEStation Instance { get; private set; }
    public GloveState State { get; private set; }
    public bool HasCleanGloves => State == GloveState.Clean;
    public bool NeedsChange => State == GloveState.NeedsChange;
    public string LastSampleSource { get; private set; }
    public int ReplacementCount { get; private set; }
    public bool IsSwabPackPickedUp { get; private set; }
    public bool SwabSupplyNeeded => (SceneTurnover.Phase==ScenarioPhase.SOCOInvestigation || SceneTurnover.Phase==ScenarioPhase.SOCOCloseout) && SwabTool.Instance!=null && SwabTool.Instance.NeedsFreshSupply;
    private bool SwabNavigationNeeded => SwabSupplyNeeded && SwabTool.Instance.SupplyRequested;
    public bool CanLoadSwabPack => IsSwabPackPickedUp && HasCleanGloves && CanInteract();
    public string DestinationText => GameDialogue.Text(SwabSupplyNeeded?(HasCleanGloves?"swab-destination":"supplies-destination"):"gloves-destination");
    private GameObject swabPack;
    private bool initialWearRecorded;
    public bool CurrentSampleStored => (SwabTool.Instance == null || !SwabTool.Instance.HasUnfinishedSample)
        && (LastSampleSource == null || (EvidenceInventory.Instance != null
        && EvidenceInventory.Instance.AllEntryIds.Any(id => EvidenceInventory.Instance.GetSampleSource(id) == LastSampleSource
            && !EvidenceInventory.Instance.IsFingerprintSample(id) && EvidenceInventory.Instance.IsSampleSealed(id)
            && EvidenceInventory.Instance.IsExplicitlyStoredSample(id))));
    public bool GuidanceNeeded => isActiveAndEnabled && (State != GloveState.Clean || (SwabNavigationNeeded && ProcedureInterlock.PendingFingerprintItemId == null)) && !SceneTurnover.IsPending && SceneTurnover.IsGated
        && CurrentSampleStored && (SceneTurnover.Phase == ScenarioPhase.IOCAssessment || SceneTurnover.Phase == ScenarioPhase.SOCOInvestigation || SceneTurnover.Phase == ScenarioPhase.SOCOCloseout);
    [SerializeField] private GameObject cleanGlovesModel;
    [Tooltip("Tint used when the player wears clean gloves on their tracked hands.")]
    [SerializeField] private Color wornGloveColor = new Color(.25f,.65f,.85f);
    [SerializeField] private float interactionRadius = 1.4f;
    private SceneCordon cordon;
    private OVRCameraRig rig;
    private InputAction confirm, cancel;
    private GameObject pair, preview;
    private Text label;
    private bool promptShown;
    private readonly List<Material> gloveMaterials = new();
    private readonly Dictionary<Renderer, Material[]> originals = new();
    public bool GlovesEquipped => State != GloveState.NotWorn;
    public bool PairPickedUp { get; private set; }
    public bool IsInRange => Camera.main != null && Vector2.Distance(
        new Vector2(Camera.main.transform.position.x, Camera.main.transform.position.z),
        new Vector2(transform.position.x, transform.position.z)) <= interactionRadius;
    public string Instruction => NeedsChange ? (!CurrentSampleStored ? GameDialogue.Text("gloves-finish-sample") : GameDialogue.Text("gloves-dispose"))
        : HasCleanGloves ? GameDialogue.Text(SwabSupplyNeeded?(IsSwabPackPickedUp?"swab-open-pack":"swab-take-pack"):"gloves-clean")
        : SceneTurnover.IsPending ? "Receive the First Responder's briefing first"
        : PairPickedUp ? "[A] Wear gloves    [B] Return pair"
        : "[A] Pick up clean gloves";

    public void Initialize(SceneCordon owner) { cordon = owner; SamplePreparationStation.Instance?.Initialize(owner); }
    private void Awake()
    {
        Instance = this;
        rig = FindAnyObjectByType<OVRCameraRig>();
        confirm = PlayerUIGate.CreateConfirmAction(this, "ArrivalPPE", CanInteract, PlayerUIGate.InformationPriority + 10);
        cancel = new InputAction("ArrivalPPE_Return", InputActionType.Button, "<XRController>{RightHand}/secondaryButton");
        BuildVisuals();
        var existingPreparation = GetComponentInChildren<SamplePreparationStation>(true);
        if (existingPreparation == null)
        {
        var preparation = EditableSceneVisuals.Create("Protected sample preparation station");
        // SceneCordon positions the station after Instantiate/Awake. A local child
        // follows that final placement and shares the station's scene lifetime.
        preparation.transform.SetParent(transform, false);
        preparation.transform.localPosition = new Vector3(1.55f,0,0);
        EditableSceneVisuals.Component<SamplePreparationStation>(preparation).Initialize(cordon);
        }
        else existingPreparation.Initialize(cordon);
        if (GetComponent<GloveStationGuide>() == null) EditableSceneVisuals.Component<GloveStationGuide>(gameObject);
    }
    private void OnEnable() { confirm.Enable(); cancel.Enable(); }
    private void OnDisable() { CancelPickup(); ClearPrompt(); confirm.Disable(); cancel.Disable(); }
    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        foreach (var entry in originals) if (entry.Key != null) entry.Key.sharedMaterials = entry.Value;
        foreach (var material in gloveMaterials) if (material != null) Destroy(material);
        confirm?.Dispose(); cancel?.Dispose();
    }
    private bool CanInteract() => isActiveAndEnabled && (!HasCleanGloves || SwabSupplyNeeded) && CurrentSampleStored && !SceneTurnover.IsPending
        && (SceneTurnover.Phase == ScenarioPhase.IOCAssessment || SceneTurnover.Phase == ScenarioPhase.SOCOInvestigation || SceneTurnover.Phase == ScenarioPhase.SOCOCloseout)
        && SceneTurnover.IsGated && cordon != null && cordon.isActiveAndEnabled && IsInRange && !PlayerUIGate.IsBlockedByOther(this) && !ToolInputLock.HasUIOwners
        && !cordon.RequiresBriefingAt(Camera.main.transform.position)
        && (cordon.AccessRegister == null || !cordon.AccessRegister.IsInside)
        && (STCSNotificationUI.Instance == null || (!STCSNotificationUI.Instance.IsShowing && STCSNotificationUI.Instance.PendingCount == 0))
        && (HeldItemCoordinator.Instance == null || HeldItemCoordinator.Instance.CurrentCategory == HeldItemCoordinator.HeldCategory.None);
    private void Update()
    {
        if ((PairPickedUp || IsSwabPackPickedUp) && (!IsInRange || PlayerUIGate.IsBlockedByOther(this))) CancelPickup();
        if ((PairPickedUp || IsSwabPackPickedUp) && cancel.WasPressedThisFrame()) { CancelPickup(); return; }
        if (label != null) label.text = (HasCleanGloves && SwabSupplyNeeded?"FRESH SWABS":"CLEAN GLOVES")+"\n"+Instruction;
        if (CanInteract())
        {
            NotificationManager.ShowPrompt(Instruction); promptShown = true;
            if (PlayerUIGate.TryConsumeConfirm(this, confirm)) Interact();
        }
        else ClearPrompt();
    }
    /// <summary>The same two-step pickup/confirm path as controller A. Revalidates on both presses.</summary>
    public bool Interact()
    {
        if (!CanInteract()) return false;
        ClearPrompt();
        if(HasCleanGloves && SwabSupplyNeeded)
        {
            if(!IsSwabPackPickedUp)
            {
                IsSwabPackPickedUp=true;PlayerUIGate.Enter(this);
                preview=Instantiate(swabPack,rig.rightHandAnchor,false);preview.name="Unopened fresh swab pack";
                preview.transform.localPosition=new Vector3(0,-.025f,.12f);preview.transform.localRotation=Quaternion.identity;
                foreach(var collider in preview.GetComponentsInChildren<Collider>()) Destroy(collider);
                SessionLogger.Instance?.LogEvent(SessionEventType.FreshSwabPackPickedUp,"swab-supply");
                return true;
            }
            if(!SwabTool.Instance.LoadFreshSwab(this))return false;
            IsSwabPackPickedUp=false;CancelPickup();GameDialogue.Say("swab-fresh-loaded");return true;
        }
        if (NeedsChange)
        {
            State = GloveState.NotWorn; RestoreHands();
            LogGloves(SessionEventType.UsedGlovesDisposed, LastSampleSource);
            return true;
        }
        if (!PairPickedUp)
        {
            PairPickedUp = true; pair.SetActive(false); PlayerUIGate.Enter(this);
            if (rig != null && rig.rightHandAnchor != null)
            {
                preview = Instantiate(pair, rig.rightHandAnchor, false); preview.name = "Clean gloves awaiting wear";
                preview.transform.localPosition = new Vector3(0, -.025f, .12f);
                preview.transform.localRotation = Quaternion.identity; preview.SetActive(true);
            }
            return true;
        }
        State = GloveState.Clean; CancelPickup(); pair.SetActive(false);
        ApplyHandGloves();
        if (initialWearRecorded)
        {
            ReplacementCount++; LogGloves(SessionEventType.FreshGlovesEquipped, LastSampleSource);
            LastSampleSource = null;
            GameDialogue.Say("gloves-replaced");
            return true;
        }
        initialWearRecorded = true;
        SessionLogger.Instance?.LogEvent(SessionEventType.PPEEquipped, "arrival-gloves", new Dictionary<string, string>
        { { "equipment", "clean-gloves" }, { "stage", "before-scene-entry" } });
        GameDialogue.SayLine("ppe-gloves-equipped", "PPE", "arrival-ppe");
        if (SceneTurnover.Phase == ScenarioPhase.IOCAssessment)
            GameDialogue.SayLine(SceneTurnover.EntryAccessReady ? "ioc-assessment-guidance" : "access-check-in-next", "System", "ioc-assessment");
        return true;
    }
    public void CancelPickup()
    {
        if(IsSwabPackPickedUp)SessionLogger.Instance?.LogEvent(SessionEventType.FreshSwabPackReturned,"swab-supply");
        IsSwabPackPickedUp=false;
        PairPickedUp = false; PlayerUIGate.Exit(this);
        if (preview != null) { preview.SetActive(false); Destroy(preview); preview = null; }
        if (pair != null) pair.SetActive(!GlovesEquipped);
        ClearPrompt();
    }
    private void ClearPrompt() { if (promptShown) NotificationManager.HidePrompt(); promptShown = false; }

    private void ApplyHandGloves()
    {
        if (rig == null) return;
        // Preserve the existing animated/tracked hand geometry and its visibility owner.
        // Material copies never modify imported assets or tool/evidence renderers.
        foreach (var renderer in rig.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            string meshName = renderer.name;
            bool hand = meshName == "LeftHand" || meshName == "RightHand" || meshName == "l_handMeshNode"
                || meshName == "r_handMeshNode" || meshName.StartsWith("[BuildingBlock] Hand Tracking");
            if (!hand) continue;
            if (originals.ContainsKey(renderer))
            {
                renderer.sharedMaterials = handGloves[renderer];
                continue;
            }
            var source = renderer.sharedMaterials; var replacement = new Material[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Session clean glove" };
                mat.SetColor("_BaseColor", wornGloveColor); mat.SetFloat("_Smoothness", .25f);
                replacement[i] = mat; gloveMaterials.Add(mat);
            }
            originals.Add(renderer, source); handGloves.Add(renderer, replacement); renderer.sharedMaterials = replacement;
        }
    }

    private readonly Dictionary<Renderer, Material[]> handGloves = new();
    private void RestoreHands()
    { foreach (var entry in originals) if (entry.Key != null) entry.Key.sharedMaterials = entry.Value; }
    public void MarkUsedForSample(string sourceId)
    {
        if (!HasCleanGloves) return;
        State = GloveState.NeedsChange; LastSampleSource = sourceId;
        LogGloves(SessionEventType.GlovesUsedForSampling, sourceId);
        GameDialogue.Say("gloves-finish-sample");
    }
    private void LogGloves(SessionEventType type, string source) => SessionLogger.Instance?.LogEvent(type, source,
        new Dictionary<string,string> { { "equipment", "gloves" }, { "scored", "false" }, { "replacementCount", ReplacementCount.ToString() } });

    [EditableSceneBuilder]
    private void BuildVisuals()
    {
        using (EditableSceneVisuals.Begin(this,"BuildVisuals"))
        {
        pair = EditableSceneVisuals.InstantiateVisual(cleanGlovesModel, transform, false); pair.name = "Clean glove pair";
        // The existing clean-pair asset is real-world sized, lying flat in its XZ plane.
        pair.transform.localPosition = new Vector3(.075f, .88f, -.10f);
        pair.transform.localScale = Vector3.one;
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        var tableMaterial = new Material(shader) { name = "PPE station surface" };
        tableMaterial.SetColor("_BaseColor", new Color(.06f, .12f, .16f)); gloveMaterials.Add(tableMaterial);
        Part("Glove supply tabletop", new Vector3(0,.84f,0),new Vector3(.74f,.06f,.44f),tableMaterial);
        foreach(float x in new[]{-.31f,.31f}) foreach(float z in new[]{-.16f,.16f})
            Part("Table leg",new Vector3(x,.405f,z),new Vector3(.045f,.81f,.045f),tableMaterial);
        var boxMaterial = new Material(shader) { name="Glove dispenser box" };
        boxMaterial.SetColor("_BaseColor",new Color(.12f,.45f,.72f));gloveMaterials.Add(boxMaterial);
        Part("Clean glove dispenser box",new Vector3(-.21f,.95f,0),new Vector3(.25f,.16f,.21f),boxMaterial);
        Part("Dispenser opening",new Vector3(-.21f,1.032f,0),new Vector3(.14f,.004f,.055f),tableMaterial);
        var binMaterial = new Material(shader) { name="Used glove receptacle" };
        binMaterial.SetColor("_BaseColor",new Color(.7f,.48f,.08f));gloveMaterials.Add(binMaterial);
        Part("Used glove disposal bin",new Vector3(.53f,.25f,0),new Vector3(.25f,.5f,.29f),binMaterial);
        Part("Disposal opening",new Vector3(.53f,.504f,0),new Vector3(.18f,.006f,.2f),tableMaterial);
        StationLabel("Dispenser label","CLEAN GLOVES",new Vector3(-.21f,.96f,-.108f),new Vector2(200,60),18,.001f);
        StationLabel("Disposal label","USED GLOVES\nDISPOSAL",new Vector3(.53f,.33f,-.15f),new Vector2(205,90),20,.001f);
        var packMaterial=new Material(shader){name="Sealed swab package"};packMaterial.SetColor("_BaseColor",new Color(.94f,.95f,.88f));gloveMaterials.Add(packMaterial);
        Part("Packaged swab supply tray",new Vector3(.22f,.89f,.06f),new Vector3(.24f,.035f,.16f),boxMaterial);
        swabPack=Part("Fresh sealed swab pack",new Vector3(.22f,.916f,.06f),new Vector3(.19f,.012f,.043f),packMaterial);
        StationLabel("Fresh swab supply label","FRESH SWABS",new Vector3(.22f,.886f,-.024f),new Vector2(200,32),18,.001f);
        // Solid station, outside the walking approach. No evidence/grab behaviours.
        var go = EditableSceneVisuals.Create("PPE station instruction", typeof(RectTransform), typeof(Canvas));
        go.transform.SetParent(transform, false); go.transform.localPosition = new Vector3(0, 1.13f, .13f);
        go.transform.localScale = Vector3.one * .0015f;
        var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
        ((RectTransform)go.transform).sizeDelta = new Vector2(460, 160);
        var backing = EditableSceneVisuals.Create("Backing", typeof(RectTransform), typeof(Image)); backing.transform.SetParent(go.transform, false);
        ((RectTransform)backing.transform).sizeDelta = new Vector2(460, 160);
        backing.GetComponent<Image>().color = new Color(.025f, .05f, .075f, .96f); backing.GetComponent<Image>().raycastTarget = false;
        var text = EditableSceneVisuals.Create("Instruction", typeof(RectTransform), typeof(Text)); text.transform.SetParent(go.transform, false);
        ((RectTransform)text.transform).sizeDelta = new Vector2(430, 136);
        label = text.GetComponent<Text>(); label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 26; label.alignment = TextAnchor.MiddleCenter; label.color = Color.white;
        label.supportRichText = false; label.raycastTarget = false; label.text = "CLEAN GLOVES\n" + Instruction;
            }
    }

    private GameObject Part(string partName,Vector3 at,Vector3 size,Material mat)
    {
        var go=EditableSceneVisuals.Primitive(PrimitiveType.Cube);go.name=partName;go.transform.SetParent(transform,false);
        go.transform.localPosition=at;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=mat;
        return go;
    }
    private void StationLabel(string name,string text,Vector3 at,Vector2 size,int fontSize,float scale)
    {
        var go=EditableSceneVisuals.Create(name,typeof(RectTransform),typeof(Canvas));go.transform.SetParent(transform,false);
        go.transform.localPosition=at;go.transform.localScale=Vector3.one*scale;
        go.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;((RectTransform)go.transform).sizeDelta=size;
        var child=EditableSceneVisuals.Create("Label",typeof(RectTransform),typeof(Text));child.transform.SetParent(go.transform,false);
        ((RectTransform)child.transform).sizeDelta=size;var textUI=child.GetComponent<Text>();textUI.text=text;
        textUI.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");textUI.fontSize=fontSize;textUI.color=Color.white;
        textUI.alignment=TextAnchor.MiddleCenter;textUI.raycastTarget=false;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>Explicit preparation for diagnostics that start after the arrival exercise.</summary>
    public void PrepareDevelopmentGloves()
    {
        if (!AutomatedTestSandbox.IsActive) throw new System.InvalidOperationException("Glove fixtures require an isolated test sandbox.");
        State = GloveState.Clean; initialWearRecorded = true; LastSampleSource = null; CancelPickup(); ApplyHandGloves();
    }
#endif
}
