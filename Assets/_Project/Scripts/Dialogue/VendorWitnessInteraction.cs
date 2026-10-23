using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Physical access to the existing vendor interview. Owns no witness state.</summary>
// Run after the interview panel's Update so opening with A cannot also select a
// question on the same button press.
[DefaultExecutionOrder(100)]
public class VendorWitnessInteraction : MonoBehaviour
{
    [SerializeField] private VendorFollowupInterview interview;
    [SerializeField] private Transform interactionAnchor;
    [SerializeField] private TMP_Text promptLabel;
    [SerializeField, Min(.5f)] private float interactionRadius = 2.25f;
    private InputAction interactAction;
    private Transform playerHead;
    private bool promptInitialized;
    private GameObject nearbyHint;
    private TMP_Text nearbyHintLabel;
    private static readonly List<VendorWitnessInteraction> active = new();
    private static int captureDepth;
    public bool IsHintVisible => nearbyHint != null && nearbyHint.activeInHierarchy && nearbyHint.GetComponent<Canvas>().enabled;
    public string HintText => nearbyHintLabel != null ? nearbyHintLabel.text : null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetHints() { active.Clear(); captureDepth=0; }

    public VendorFollowupInterview Interview => interview;
    public float InteractionRadius => interactionRadius;
    public Vector3 InteractionPosition => interactionAnchor != null ? interactionAnchor.position : transform.position;
    public bool IsPromptShown { get; private set; }

    private void Awake()
    {
        if (Camera.main != null) playerHead = Camera.main.transform;
        if(interview==null) interview=VendorFollowupInterview.Instance;
        interactAction = PlayerUIGate.CreateConfirmAction(this, "VendorWitness_Interact", () => CanInteract, 200);
        BuildNearbyHint();
    }
    private void Start()
    {
        var route = Resources.Load<VendorPathRoute>("VendorPathRoute_CSI");
        if (route != null && gameObject.scene.name == route.sceneName && GetComponent<VendorPathGuide>() == null)
            gameObject.AddComponent<VendorPathGuide>();
    }
    private void OnEnable() { interactAction.Enable(); if(!active.Contains(this))active.Add(this); }
    private void OnDisable()
    {
        interactAction.Disable();
        SetPrompt(false);
        active.Remove(this); if(nearbyHint!=null)nearbyHint.SetActive(false);
        if(promptLabel!=null)promptLabel.enabled=true;
    }
    private void OnDestroy() { interactAction?.Dispose(); if(nearbyHint!=null)Destroy(nearbyHint); }

    public bool PlayerInRange()
    {
        // Camera creation can follow NPC Awake; a tracked rig can also be replaced.
        // Always resolve the current view instead of keeping a missing/stale head.
        var camera=Camera.main;
        playerHead=camera!=null?camera.transform:null;
        if (playerHead == null) return false;
        Vector3 offset = playerHead.position
            - (interactionAnchor != null ? interactionAnchor.position : transform.position);
        offset.y = 0f;
        return offset.sqrMagnitude <= interactionRadius * interactionRadius;
    }
    private bool CanInteract => isActiveAndEnabled && interview != null && interview.isActiveAndEnabled
        && !PlayerUIGate.IsBlocked && !ProcedureInterlock.BlocksUnrelatedActions
        && SceneTurnover.Phase == ScenarioPhase.SOCOInvestigation
        && !interview.IsTalking && PlayerInRange();

    private void Update()
    {
        SetPrompt(CanInteract);
        RefreshHint();
        if (CanInteract && PlayerUIGate.TryConsumeConfirm(this, interactAction)) Interact();
    }
    public bool Interact()
    {
        if (!CanInteract) return false;
        interview.ShowFromVendor(this);
        SetPrompt(false);
        RefreshHint();
        return interview.IsShowing;
    }
    private void SetPrompt(bool shown)
    {
        if (promptInitialized && IsPromptShown == shown) return;
        promptInitialized = true;
        IsPromptShown = shown;
        if (promptLabel != null)
            promptLabel.text = GameDialogue.Text(shown ? "prompt-talk-vendor" : "vendor-placeholder-label");
    }
    public void RefreshHint()
    {
        if(nearbyHint==null)return;
        if(interview==null)interview=VendorFollowupInterview.Instance;
        var tooltip=NotificationManager.Instance?.UI;
        bool show=isActiveAndEnabled && interview!=null && interview.isActiveAndEnabled && PlayerInRange()
            && !interview.IsShowing && !interview.IsTalking && !PlayerUIGate.IsBlocked && !ToolInputLock.HasUIOwners
            && !SceneTestStart.IsPreparing && captureDepth==0;
        if(show&&tooltip!=null&&!string.IsNullOrEmpty(tooltip.CurrentTooltip))
        {
            // At the destination, replace the general "speak to the vendor" notice
            // with the actual A-button action. Unrelated tool feedback keeps priority.
            if(tooltip.CurrentTooltip==GameDialogue.Text("vendor-followup-required")
                ||tooltip.CurrentTooltip==GameDialogue.Text("vendor-statement-recorded")) tooltip.ClearTooltip();
            else show=false;
        }
        nearbyHint.SetActive(show);
        if(promptLabel!=null)promptLabel.enabled=!show;
        if(!show)return;
        string text;
        if(SceneTurnover.Phase!=ScenarioPhase.SOCOInvestigation)
            text="<b>Vendor</b>\n"+(SceneTurnover.HasReachedPhase(ScenarioPhase.SOCOInvestigation)
                ?"Witness interviews are available during the investigation. Follow your current closeout objective."
                :"Complete the scene briefing before interviewing this witness.");
        else if(ProcedureInterlock.BlocksUnrelatedActions)
            text="<b>Before talking to the vendor</b>\n"+ProcedureInterlock.Reason;
        else if(interview.RequiresFollowup)
            text="<b>Vendor follow-up</b>\n[A] Talk to the vendor\nThen choose the follow-up question in the interview.";
        else if(interview.IsDiscovered)
            text="<b>Vendor</b>\n[A] Review the recorded witness statements.";
        else
            text="<b>Vendor</b>\n[A] Talk to the vendor\nReview the witness statements.";
        nearbyHintLabel.text=text;
        float height=Mathf.Clamp(nearbyHintLabel.GetPreferredValues(text,820,Mathf.Infinity).y+40,150,360);
        ((RectTransform)nearbyHint.transform).sizeDelta=new Vector2(900,height);
    }
    [EditableSceneBuilder]
    private void BuildNearbyHint()
    {
        using(EditableSceneVisuals.Begin(this,"BuildNearbyHint"))
        {
            nearbyHint=EditableSceneVisuals.Create("Vendor nearby guidance",typeof(RectTransform),typeof(Canvas),typeof(HudFollowCamera));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(nearbyHint,gameObject.scene);
            nearbyHint.transform.localScale=Vector3.one*.001f;
            var canvas=nearbyHint.GetComponent<Canvas>(); canvas.renderMode=RenderMode.WorldSpace; canvas.sortingOrder=92;
            var background=ForensicUIStyle.Rect(nearbyHint.transform,"Vendor hint background",ForensicUIStyle.PanelBackground).rectTransform;
            background.anchorMin=Vector2.zero; background.anchorMax=Vector2.one; background.offsetMin=background.offsetMax=Vector2.zero;
            nearbyHintLabel=ForensicUIStyle.Text(nearbyHint.transform,"Vendor hint",29,ForensicUIStyle.TextPrimary,FontStyles.Normal,TextAlignmentOptions.Center);
            nearbyHintLabel.rectTransform.anchorMin=Vector2.zero; nearbyHintLabel.rectTransform.anchorMax=Vector2.one;
            nearbyHintLabel.rectTransform.offsetMin=new Vector2(40,20); nearbyHintLabel.rectTransform.offsetMax=new Vector2(-40,-20);
        }
        var follow=HudFollowCamera.ConfigureShared(nearbyHint,this,PlayerUIGate.AmbientPriority,true);
        follow.Distance=1.25f; follow.LocalOffset=new Vector3(0,-.18f,0);
        nearbyHint.SetActive(false);
    }
    public static IDisposable HideForCapture()=>new CaptureScope();
    private sealed class CaptureScope:IDisposable
    {
        private bool disposed;
        public CaptureScope() {captureDepth++;foreach(var witness in active)if(witness!=null&&witness.nearbyHint!=null)witness.nearbyHint.SetActive(false);}
        public void Dispose() {if(disposed)return;disposed=true;captureDepth=Mathf.Max(0,captureDepth-1);foreach(var witness in active)if(witness!=null)witness.RefreshHint();}
    }
}
