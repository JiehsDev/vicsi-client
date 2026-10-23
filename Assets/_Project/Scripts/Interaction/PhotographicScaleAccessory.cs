using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>One assisted photo ruler belonging to the camera, never an inventory/tool-wheel item.</summary>
[DisallowMultipleComponent]
public sealed class PhotographicScaleAccessory : MonoBehaviour
{
    [Serializable] public class Anchor
    {
        public string evidenceId;
        public Vector3 offset, euler, frameOffset, frameSize;
    }
    [Serializable] private class Layout { public Anchor[] entries; }
    private PhotographTool tool;
    private Anchor[] anchors = Array.Empty<Anchor>();
    private InputAction confirm;
    private GameObject ruler;
    [SerializeField] private GameObject authoredRuler;
    private EvidenceProp source;
    private Vector3 originalSourcePosition;
    private TMP_Text hint;
    private GameObject hintPanel;
    private bool captured;
    public bool IsPlaced => ruler != null && ruler.activeSelf;
    public string PlacedEvidenceId => IsPlaced && source != null ? source.evidenceId : null;
    public Transform ScaleTransform => ruler != null ? ruler.transform : null;
    public string GuidanceText => hint != null ? hint.text : string.Empty;

    private void Awake()
    {
        tool = GetComponent<PhotographTool>();
        if(authoredRuler!=null)authoredRuler.SetActive(false);
        var json = Resources.Load<TextAsset>("PhotographicScaleLayout");
        if(json != null) anchors = JsonUtility.FromJson<Layout>(json.text)?.entries ?? Array.Empty<Anchor>();
        confirm = PlayerUIGate.CreateConfirmAction(tool,"CameraPhotoScale",CanInteract,250);
        BuildHint();
    }
    private void OnEnable() => confirm?.Enable();
    private void OnDisable() { confirm?.Disable(); Remove("camera-disabled"); }
    private void OnDestroy() { confirm?.Dispose(); if(ruler!=null) Destroy(ruler); }
    private Anchor Find(string id) => Array.Find(anchors,a=>a.evidenceId==id);
    public bool RequiresScale(string id) => Find(id)!=null;

    private bool ActiveCamera => tool!=null && tool.isActiveAndEnabled && tool.IsCameraHeld && tool.IsAiming
        && !ToolInputLock.IsLocked && !PlayerUIGate.IsBlockedByOther(tool) && !SceneTurnover.IsInvestigationBlocked;
    private bool CanPlace()
    {
        var prop=tool.CurrentAimTarget; var cam=tool.CapturePov;
        return ActiveCamera && !IsPlaced && prop!=null && Find(prop.evidenceId)!=null && cam!=null
            && ScenePhotographyManager.Instance!=null && ScenePhotographyManager.Instance.IsScenePhotographyComplete
            && EvidenceStateManager.Instance!=null
            && EvidenceStateManager.Instance.GetRecord(prop.evidenceId)?.status==EvidenceStatus.Marked
            && ProceduralGateValidator.Instance!=null
            && ProceduralGateValidator.Instance.CanTransition(prop.evidenceId,EvidenceStatus.PhotographedFinal)
            && Vector3.Distance(cam.transform.position,prop.transform.position)<=2f;
    }
    private bool CanInteract() => ActiveCamera && (IsPlaced || CanPlace());

    // Called by the camera before its shutter input. A cannot place and shoot on one press.
    public bool HandleInput()
    {
        if(PlayerUIGate.TryConsumeConfirm(tool,confirm))
        {
            if(IsPlaced) Remove("student-removed"); else TryPlace();
            RefreshHint(); return true;
        }
        return false;
    }
    public void Refresh()
    {
        if(IsPlaced && (tool==null || !tool.IsCameraHeld || source==null || !source.gameObject.activeInHierarchy
            || SceneTurnover.IsInvestigationBlocked || Vector3.Distance(source.transform.position,originalSourcePosition)>.01f
            || (tool.CapturePov!=null && Vector3.Distance(tool.CapturePov.transform.position,source.transform.position)>3f)))
            Remove("accessory-returned");
        RefreshHint();
    }
    public bool TryPlace()
    {
        // Re-read the actual camera target; callers cannot select an unrelated item or seed a first photo.
        if(!CanPlace()) return false;
        source=tool.CurrentAimTarget; originalSourcePosition=source.transform.position;
        var anchor=Find(source.evidenceId);
        if(ruler==null) ruler=authoredRuler!=null?authoredRuler:EditableSceneVisuals.Create("Assisted 30 cm photographic scale",typeof(PhotographicScaleGraphic));
        ruler.transform.SetPositionAndRotation(source.transform.position+anchor.offset,Quaternion.Euler(anchor.euler));
        ruler.transform.localScale=Vector3.one; ruler.SetActive(true); captured=false;
        Log(SessionEventType.PhotographicScalePlaced,"assisted-placement");
        return true;
    }
    public bool Remove(string reason="student-removed")
    {
        if(!IsPlaced) return false;
        Log(SessionEventType.PhotographicScaleRemoved,reason);
        ruler.SetActive(false); source=null; captured=false;
        return true;
    }

    public bool AllowsPhoto(string id, EvidenceStatus status, out string reason)
    {
        reason=null;
        var cam=tool.CapturePov;
        if(status==EvidenceStatus.PhotographedRaw)
        {
            if(IsPlaced && cam!=null && ScaleIntersectsFrame(cam)) reason=GameDialogue.Text("photo-scale-remove-for-original");
            return reason==null;
        }
        if(status!=EvidenceStatus.PhotographedFinal || !RequiresScale(id)) return true;
        if(!IsPlaced || PlacedEvidenceId!=id) { reason=GameDialogue.Text("photo-scale-required"); return false; }
        if(cam==null) { reason=GameDialogue.Text("photo-scale-frame"); return false; }
        if(!FrameContainsSubject(cam,id,out reason)) return false;
        return true;
    }
    public void RecordCapture(string id)
    {
        if(!IsPlaced || captured || PlacedEvidenceId!=id) return;
        captured=true; Log(SessionEventType.PhotographicScaleCaptured,"scaled-final-photo");
    }
    private void Log(SessionEventType type,string action) => SessionLogger.Instance?.LogEvent(type,PlacedEvidenceId,
        new Dictionary<string,string>{{"action",action},{"assisted","true"},{"lengthMetres","0.30"},{"units","cm"}});

    public Vector3[] ScaleCorners()
    {
        if(!IsPlaced) return Array.Empty<Vector3>();
        return new[] { ruler.transform.TransformPoint(new Vector3(-.16f,-.022f,0)),
            ruler.transform.TransformPoint(new Vector3(.16f,-.022f,0)),
            ruler.transform.TransformPoint(new Vector3(.16f,.022f,0)),
            ruler.transform.TransformPoint(new Vector3(-.16f,.022f,0)) };
    }
    private Vector3 Project(Camera cam,Vector3 point)
    {
        var p=cam.transform.InverseTransformPoint(point);
        float half=cam.orthographic?cam.orthographicSize:Mathf.Tan(cam.fieldOfView*Mathf.Deg2Rad*.5f)*p.z;
        float aspect=tool.CaptureAspect;
        return new Vector3(.5f+p.x/(2*half*aspect),.5f+p.y/(2*half),p.z);
    }
    private bool Within(Camera cam,Vector3 p)
    {
        var v=Project(cam,p);
        return v.z>cam.nearClipPlane && v.z<cam.farClipPlane && v.x>=.035f && v.x<=.965f && v.y>=.035f && v.y<=.965f;
    }
    private bool Visible(Camera cam,Vector3 p)
    {
        var direction=p-cam.transform.position;
        return !Physics.Raycast(cam.transform.position,direction.normalized,Mathf.Max(0,direction.magnitude-.004f),~0,QueryTriggerInteraction.Ignore);
    }
    private bool ScaleIntersectsFrame(Camera cam)
    {
        float minX=float.MaxValue,minY=float.MaxValue,maxX=float.MinValue,maxY=float.MinValue;
        bool forward=false;
        foreach(var p in ScaleCorners())
        {
            var v=Project(cam,p); forward|=v.z>cam.nearClipPlane;
            minX=Mathf.Min(minX,v.x);maxX=Mathf.Max(maxX,v.x);minY=Mathf.Min(minY,v.y);maxY=Mathf.Max(maxY,v.y);
        }
        return forward && maxX>=0 && minX<=1 && maxY>=0 && minY<=1;
    }
    private bool FrameContainsSubject(Camera cam,string id,out string reason)
    {
        reason=GameDialogue.Text("photo-scale-fit");
        var anchor=Find(id); if(source==null || anchor==null) return false;
        foreach(var point in ScaleCorners()) if(!Within(cam,point)) return false;
        var centre=source.transform.position+anchor.frameOffset; var extent=anchor.frameSize*.5f;
        for(int x=-1;x<=1;x+=2) for(int y=-1;y<=1;y+=2) for(int z=-1;z<=1;z+=2)
            if(!Within(cam,centre+Vector3.Scale(extent,new Vector3(x,y,z)))) return false;
        reason=GameDialogue.Text("photo-scale-front");
        if(Vector3.Dot(-ruler.transform.forward,(cam.transform.position-ruler.transform.position).normalized)<.35f) return false;
        reason=GameDialogue.Text("photo-scale-obstructed");
        foreach(var point in ScaleCorners()) if(!Visible(cam,point)) return false;
        if(!Visible(cam,ruler.transform.position)) return false;
        var a=Project(cam,ruler.transform.TransformPoint(new Vector3(-.15f,0,0)));
        var b=Project(cam,ruler.transform.TransformPoint(new Vector3(.15f,0,0)));
        reason=GameDialogue.Text("photo-scale-closer");
        // At 1440 pixels wide, 10% retains more ruler pixels than the former
        // 14% of 960, while allowing floor evidence to be photographed standing.
        if(Vector2.Distance(new Vector2(a.x,a.y),new Vector2(b.x,b.y))<.10f) return false;
        reason=null;
        return true;
    }

    [EditableSceneBuilder]
    private void BuildHint()
    {
        using (EditableSceneVisuals.Begin(this,"BuildHint"))
        {
        if(tool.ViewfinderOverlay==null) return;
        hintPanel=EditableSceneVisuals.Create("Photo scale camera accessory guidance",typeof(RectTransform),typeof(Image));
        hintPanel.transform.SetParent(tool.ViewfinderOverlay.transform,false);
        var rect=(RectTransform)hintPanel.transform;
        rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f); rect.sizeDelta=new Vector2(520,94); rect.anchoredPosition=new Vector2(0,-145);
        var background=hintPanel.GetComponent<Image>();background.color=new Color(.015f,.025f,.035f,.94f);background.raycastTarget=false;
        var go=EditableSceneVisuals.Create("Guidance",typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(hintPanel.transform,false);
        var textRect=(RectTransform)go.transform;textRect.anchorMin=Vector2.zero;textRect.anchorMax=Vector2.one;
        textRect.offsetMin=new Vector2(12,6);textRect.offsetMax=new Vector2(-12,-6);
        hint=go.GetComponent<TextMeshProUGUI>();hint.font=TMP_Settings.defaultFontAsset;hint.fontSize=22;
        hint.alignment=TextAlignmentOptions.Center;hint.raycastTarget=false;hint.color=Color.white;
        hintPanel.SetActive(false);
            }
    }
    private void RefreshHint()
    {
        if(hint==null) return;
        var prop=tool.CurrentAimTarget;
        var record=prop!=null?EvidenceStateManager.Instance?.GetRecord(prop.evidenceId):null;
        bool relevant=prop!=null && RequiresScale(prop.evidenceId) && record!=null
            && !EvidenceStateManager.Instance.IsAtOrPastStatus(prop.evidenceId,EvidenceStatus.PhotographedFinal);
        bool show=ActiveCamera && (IsPlaced || relevant);
        hintPanel.SetActive(show);
        if(!show) return;
        if(IsPlaced && !captured)
        {
            bool ready=AllowsPhoto(PlacedEvidenceId,EvidenceStatus.PhotographedFinal,out string reason);
            if(tool.CurrentAimTarget?.evidenceId!=PlacedEvidenceId)
            {
                ready=false; reason=GameDialogue.Text("photo-scale-aim-item");
            }
            hint.text=ready?GameDialogue.Text("photo-scale-ready"):reason+"\n"+GameDialogue.Text("photo-scale-remove");
            return;
        }
        hint.text=IsPlaced ? GameDialogue.Text("photo-scale-saved")
            :CanPlace()?GameDialogue.Text("photo-scale-place-prompt")
            :record?.status==EvidenceStatus.Found?GameDialogue.Text("photo-scale-first-original")
            :record?.status==EvidenceStatus.Marked && ProceduralGateValidator.Instance.CanTransition(prop.evidenceId,EvidenceStatus.PhotographedFinal)
                ?GameDialogue.Text("photo-scale-move-closer"):GameDialogue.Text("photo-scale-prepare-item");
    }
}
