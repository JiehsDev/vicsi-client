using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

/// <summary>Real XR menu input plus rendered glyph geometry, including the densest phase layout.</summary>
public sealed class MenuWheelComfortTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }
    readonly StringBuilder report = new("=== MenuWheelComfortTest ===\n");
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    int failures;
    InputDevice left, right;
    void Check(string name, bool ok) { if (!ok) failures++; report.AppendLine((ok ? "PASS: " : "FAIL: ") + name); }
    static object Field(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
    static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    IEnumerator Button(bool down)
    {
        InputSystem.QueueDeltaStateEvent((ButtonControl)left["primaryButton"], down ? 1f : 0f);
        yield return null; yield return null;
    }
    public void RunAllTests() => StartCoroutine(Run());
    IEnumerator Run()
    {
        yield return null;
        var pre = FindAnyObjectByType<PreSessionFlowController>();
        if (pre != null) { pre.enabled = false; PlayerUIGate.Exit(Field(pre,"uiGateToken")); }
        FindAnyObjectByType<StudentIdentificationPanel>(FindObjectsInactive.Include)?.Hide();
        if (ReasoningCheckpointManager.Instance != null) ReasoningCheckpointManager.Instance.enabled = false;
        if (HypothesisCheckpointManager.Instance != null) HypothesisCheckpointManager.Instance.enabled = false;
        if (ProceduralTrainingGuidance.Instance != null) ProceduralTrainingGuidance.Instance.enabled = false;
        if (NarrativeEventManager.Instance != null) { NarrativeEventManager.Instance.enabled = false; NarrativeEventManager.Instance.StopAllCoroutines(); }
        var photos = FindAnyObjectByType<ScenePhotographyManager>();
        if (photos != null) { photos.CancelInvoke(); photos.StopAllCoroutines(); photos.enabled = false; }
        STCSNotificationUI.Instance?.HideImmediately();
        NotificationManager.Instance?.UI.ClearTooltip(); NotificationManager.Instance?.UI.HidePrompt();
        var wheel = FindAnyObjectByType<UtilityMenuController>();
        Check("Scenario menu exists", wheel != null);
        if (wheel == null) { Finish(); yield break; }
        var original = (List<UtilityMenuController.MenuEntry>)Field(wheel,"entries");
        var runtime = (HashSet<UtilityMenuController.MenuEntry>)Field(wheel,"runtimeEntries");
        var root = (GameObject)Field(wheel,"wheelRoot");
        var follow = root.GetComponent<HudFollowCamera>();
        var rect = (RectTransform)root.transform;
        Check("Smaller wheel with unchanged physical canvas scale", rect.rect.width == 660 && Mathf.Abs(root.transform.localScale.x-.0006f)<.000001f);
        Check("Viewing distance retained", Mathf.Approximately(follow.Distance,.65f));
        var names = original.Select(e=>e.displayName).ToList();
        report.AppendLine("INFO: actual entries: " + string.Join(", ", names));
        InputSystem.RegisterLayout("{\"name\":\"ViCSIMenuComfortController\",\"extend\":\"XRController\",\"controls\":[{\"name\":\"primaryButton\",\"layout\":\"Button\",\"format\":\"FLT\"},{\"name\":\"thumbstick\",\"layout\":\"Vector2\",\"aliases\":[\"primary2DAxis\"]}]}");
        left=InputSystem.AddDevice("ViCSIMenuComfortController"); InputSystem.SetDeviceUsage(left,CommonUsages.LeftHand);
        right=InputSystem.AddDevice("ViCSIMenuComfortController"); InputSystem.SetDeviceUsage(right,CommonUsages.RightHand);
        var fixtures = new List<UtilityMenuController.MenuEntry>();
        // The desktop rig has no active tracked-controller locomotion branch.
        // Supply an active Meta Jump object for the real suspender to disable/restore.
        var locomotionFixture=new GameObject("MenuComfortLocomotionFixture");
        locomotionFixture.AddComponent<Oculus.Interaction.Locomotion.LocomotionActionsBroadcaster>();
        var jumpFixture=new GameObject("Jump");jumpFixture.transform.SetParent(locomotionFixture.transform);
        int chosen=-1, calls=0;
        for (int i=0;i<names.Count;i++)
        {
            int captured=i;
            var entry=new UtilityMenuController.MenuEntry {displayName=names[i],onSelect=new UnityEvent()};
            entry.onSelect.AddListener(()=>{chosen=captured;calls++;}); fixtures.Add(entry);runtime.Add(entry);
        }
        try
        {
            var tutorialNotes=new UtilityMenuController.MenuEntry {displayName="Notes",onSelect=new UnityEvent()};
            int tutorialId=fixtures.Count;
            tutorialNotes.onSelect.AddListener(()=>{chosen=tutorialId;calls++;});
            fixtures.Add(tutorialNotes);runtime.Add(tutorialNotes);
            var tutorial=new[]{tutorialNotes,fixtures[names.IndexOf("Item Logs")],fixtures[names.IndexOf("Album")]};
            var layouts=new List<List<UtilityMenuController.MenuEntry>> {
                tutorial.ToList(), tutorial.Append(fixtures[names.IndexOf("Settings")]).ToList(),
                original.Where(e=>!e.iocAssessmentOnly&&!e.socoCloseoutOnly).Select(e=>fixtures[names.IndexOf(e.displayName)]).ToList(),
                original.Where(e=>!e.socoCloseoutOnly).Select(e=>fixtures[names.IndexOf(e.displayName)]).ToList(),
                original.Where(e=>!e.iocAssessmentOnly).Select(e=>fixtures[names.IndexOf(e.displayName)]).ToList(),
                fixtures.Take(names.Count).ToList()
            };
            foreach(var layout in layouts)
            {
                int count=layout.Count;
                Set(wheel,"entries",layout);
                yield return Button(true);
                Check(count+" entries: actual X opens and locks UI",wheel.IsCanvasActive && PlayerUIGate.IsBlocked);
                var suspended=(List<GameObject>)typeof(LocomotionSuspender).GetField("suspendedObjects",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
                Check(count+" entries: locomotion objects suspended while selecting",suspended.Count>0 && suspended.All(o=>o==null||!o.activeSelf));
                Check(count+" entries: shared wall-safe placement retained",follow.SharedLayout && follow.ObstacleAwarePlacement);
                var labels=root.GetComponentsInChildren<TMP_Text>().Where(t=>t.name=="Label").ToArray();
                Check(count+" entries: correct number of segments",labels.Length==count);
                for(int i=0;i<labels.Length;i++)
                {
                    var label=labels[i];label.ForceMeshUpdate();
                    string name=layout[i].displayName;
                    Check(count+"/"+name+": fixed readable bold type, max two lines, no overflow",label.fontSize==24 && !label.enableAutoSizing
                        && (label.fontStyle&FontStyles.Bold)!=0 && label.textInfo.lineCount<=2 && !label.isTextOverflowing);
                    bool inside=true; float lo=i*360f/count+1f, hi=(i+1)*360f/count-1f;
                    foreach(var c in label.textInfo.characterInfo.Where(c=>c.isVisible))
                        foreach(var corner in new[]{c.bottomLeft,c.topLeft,c.topRight,c.bottomRight})
                        {
                            Vector2 p=label.rectTransform.anchoredPosition+(Vector2)corner;
                            float angle=Mathf.Repeat(Mathf.Atan2(p.x,p.y)*Mathf.Rad2Deg,360);
                            inside &= p.magnitude<330 && p.magnitude>138.6f && angle>=lo-.1f && angle<=hi+.1f;
                        }
                    Check(count+"/"+name+": rendered glyphs inside own segment and central wheel",inside);
                    float mid=(i+.5f)*2*Mathf.PI/count;
                    InputSystem.QueueDeltaStateEvent((Vector2Control)right["thumbstick"],new Vector2(Mathf.Sin(mid),Mathf.Cos(mid)));
                    yield return null; yield return null;
                    Check(count+"/"+name+": real right stick selects correct segment",(int)Field(wheel,"hoveredIndex")==i);
                    Check(count+"/"+name+": selected label uses clear black-on-white state",label.color.r==0 && labels.Where((l,j)=>j!=i).All(l=>l.color.r==1));
                }
                int selected=(int)Field(wheel,"hoveredIndex");
                InputSystem.QueueDeltaStateEvent((Vector2Control)right["thumbstick"],new Vector2(.1f,0));
                yield return null;yield return null;
                Check(count+" entries: original deadzone retains selection",(int)Field(wheel,"hoveredIndex")==selected);
                int before=calls;yield return Button(false);
                Check(count+" entries: X release confirms exactly selected action and unlocks",chosen==fixtures.IndexOf(layout[selected]) && calls==before+1 && !wheel.IsCanvasActive && !PlayerUIGate.IsBlocked);
                Check(count+" entries: original locomotion objects restored",suspended.Count==0 && jumpFixture.activeSelf);
            }
            // Exercise the actual compact menu's measured rectangle near a wall.
            Set(wheel,"entries",fixtures.Take(names.Count).ToList());
            yield return Button(true);
            var cameraGo=new GameObject("MenuComfortCamera");var cam=cameraGo.AddComponent<Camera>();cam.enabled=false;
            cam.transform.position=new Vector3(40,1.7f,40);Set(follow,"targetCamera",cam);follow.SmoothTime=0;
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="MenuComfortWall";
            wall.transform.position=cam.transform.position+Vector3.forward*.6f;wall.transform.localScale=new Vector3(.4f,3,.08f);Physics.SyncTransforms();
            yield return null;yield return null;
            Check("Actual menu rectangle stays on safe side of nearby wall",!follow.IsLayoutSuppressed && follow.PanelFits(cam.transform.position,root.transform.position,root.transform.rotation));
            Check("Actual menu retains readable fallback distance",Vector3.Distance(cam.transform.position,root.transform.position)>=follow.MinimumDistance-.01f);
            Destroy(wall);Set(follow,"targetCamera",Camera.main);Destroy(cameraGo);
            yield return Button(false);
        }
        finally
        {
            Set(wheel,"entries",original);foreach(var entry in fixtures)runtime.Remove(entry);
            if(left!=null)InputSystem.RemoveDevice(left);if(right!=null)InputSystem.RemoveDevice(right);
            InputSystem.RemoveLayout("ViCSIMenuComfortController");
            Destroy(locomotionFixture);
        }
        Finish();
    }
    void Finish(){report.AppendLine(failures==0?"=== PASS ===":"=== FAIL: "+failures+" ===");Report=report.ToString();IsDone=true;Debug.Log(Report);}
}
