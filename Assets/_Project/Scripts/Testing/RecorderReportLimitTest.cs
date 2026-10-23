using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

/// <summary>Exercises real B/stick selection, replacement and the exact-three confirmation boundary.</summary>
public sealed class RecorderReportLimitTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }
    private readonly StringBuilder report = new("=== RecorderReportLimitTest ===\n");
    private int failures;
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private void Check(string label, bool ok)
    { report.AppendLine((ok ? "PASS: " : "FAIL: ") + label); if (!ok) failures++; }
    public void RunAllTests() { AutomatedTestSandbox.Activate(nameof(RecorderReportLimitTest)); StartCoroutine(Run()); }
    private static IEnumerator Button(InputDevice device)
    {
        InputSystem.QueueDeltaStateEvent((ButtonControl)device["secondaryButton"],1f);
        yield return null; yield return null;
        InputSystem.QueueDeltaStateEvent((ButtonControl)device["secondaryButton"],0f);
        yield return null; yield return null;
    }
    private static IEnumerator Stick(InputDevice device,float x)
    {
        InputSystem.QueueDeltaStateEvent((Vector2Control)device["thumbstick"],new Vector2(x,0));
        yield return null; yield return null;
        InputSystem.QueueDeltaStateEvent((Vector2Control)device["thumbstick"],Vector2.zero);
        yield return null; yield return null;
    }
    private IEnumerator Run()
    {
        yield return null;
        Check("scene photography complete for isolated UI fixture",ScenePhotographyTestUtil.CompleteRoute());
        STCSNotificationUI.Instance.HideImmediately();
        var ui = RecorderReportUI.Instance;
        var prop = EvidenceProp.FindById("EVD-018");
        int calls = 0;
        List<string> confirmed = null;
        ui.Show(prop,(id,lines)=>{calls++; confirmed=lines; return TransitionResult.Applied;});
        var notes = (RectTransform)typeof(RecorderReportUI).GetField("notesContainer",Private).GetValue(ui);
        var pool = (RectTransform)typeof(RecorderReportUI).GetField("poolContainer",Private).GetValue(ui);
        var hint = (TMP_Text)typeof(RecorderReportUI).GetField("hintLabel",Private).GetValue(ui);
        Check("bottle fixture opens with exactly six candidates",ui.IsShowing && pool.GetComponentsInChildren<ReportLineChip>(true).Count(c=>!string.IsNullOrEmpty(c.LineId)) == 6);
        Check("Recorder explains six choices and selecting or deselecting before confirmation",ui.GetComponentsInChildren<TMP_Text>(true).Any(t=>t.text.Contains("3 of 6")&&t.text.Contains("select or deselect")));
        for(int frame=0;frame<4;frame++)yield return null;
        Capture(ui,"recorder-six-choices");
        Check("empty Notes cannot confirm",ui.Confirm()==TransitionResult.Violation && calls==0 && ui.IsShowing);
        InputSystem.RegisterLayout("{\"name\":\"ViCSIRecorderLimitController\",\"extend\":\"XRController\",\"controls\":[{\"name\":\"secondaryButton\",\"layout\":\"Button\",\"format\":\"FLT\"},{\"name\":\"thumbstick\",\"layout\":\"Vector2\"}]}");
        var device = InputSystem.AddDevice("ViCSIRecorderLimitController");
        InputSystem.SetDeviceUsage(device,CommonUsages.RightHand);
        try
        {
            for(int i=1;i<=3;i++)
            {
                yield return Button(device);
                Check("actual B adds line "+i,notes.childCount==i);
                if(i<3) Check(i+" lines cannot confirm",ui.Confirm()==TransitionResult.Violation && calls==0 && ui.IsShowing);
            }
            var firstIds = notes.GetComponentsInChildren<ReportLineChip>().Select(c=>c.LineId).ToArray();
            Capture(ui,"recorder-three-selected");
            yield return Button(device);
            Check("fourth B selection is blocked without changing the three choices",notes.childCount==3
                && firstIds.SequenceEqual(notes.GetComponentsInChildren<ReportLineChip>().Select(c=>c.LineId)));
            Check("full Notes explains how to replace a line",hint.text.Contains("3 / 3") && hint.text.Contains("replace"));
            Check("programmatic selection uses the same limit",!ui.SelectLineForTest(pool.GetChild(0).GetComponent<ReportLineChip>().LineId));
            yield return Stick(device,1);
            yield return Button(device);
            Check("actual B can remove a line from full Notes",notes.childCount==2);
            Check("removing a line disables confirmation again",ui.Confirm()==TransitionResult.Violation && calls==0);
            yield return Stick(device,-1);
            yield return Button(device);
            Check("a replacement line can fill the freed slot",notes.childCount==3);
            // Simulate an invalid external UI mutation: confirmation must still refuse four.
            var extra = pool.GetChild(0); extra.SetParent(notes,false);
            Check("confirmation independently rejects an overfilled Notes container",ui.Confirm()==TransitionResult.Violation && calls==0 && ui.IsShowing);
            extra.SetParent(pool,false);
            Check("exactly three unique choices reach the callback and close",ui.Confirm()==TransitionResult.Applied
                && calls==1 && confirmed.Count==3 && confirmed.Distinct().Count()==3 && !ui.IsShowing);
            foreach(var id in new[]{"BODY-A","EVD-014","EVD-015","EVD-018"})
            {
                STCSNotificationUI.Instance.HideImmediately();
                ui.Show(EvidenceProp.FindById(id),(e,lines)=>TransitionResult.Applied);
                Check(id+": next report starts with empty Notes",ui.IsShowing && notes.childCount==0);
                Check(id+": exactly six subject choices are presented",pool.GetComponentsInChildren<ReportLineChip>(true).Count(c=>!string.IsNullOrEmpty(c.LineId))==6);
                ui.Hide();
            }
            var callback = typeof(RecorderTool).GetMethod("HandleReportConfirmed",Private);
            foreach(var ids in new[]{new List<string>(),new List<string>{"a","b"},new List<string>{"a","b","c","d"},new List<string>{"a","a","b"}})
                Check("Recorder callback refuses invalid count/duplicates: "+string.Join(",",ids),
                    (TransitionResult)callback.Invoke(RecorderTool.Instance,new object[]{"EVD-018",ids})==TransitionResult.Violation);
            Check("invalid submissions leave evidence unlogged",EvidenceStateManager.Instance.GetRecord("EVD-018").status==EvidenceStatus.NotFound);
        }
        finally
        {
            ui.Hide(); InputSystem.RemoveDevice(device); InputSystem.RemoveLayout("ViCSIRecorderLimitController");
        }
        report.AppendLine(failures==0?"=== PASS, 0 failures ===":"=== FAIL: "+failures+" failure(s) ===");
        Report=report.ToString(); IsDone=true;
    }
    private static void Capture(RecorderReportUI ui,string name)
    {
        var root=((GameObject)typeof(RecorderReportUI).GetField("panelRoot",Private).GetValue(ui)).GetComponent<RectTransform>();
        var children=root.GetComponentsInChildren<Transform>(true);var layers=children.Select(t=>t.gameObject.layer).ToArray();
        var go=new GameObject("Recorder visual proof camera");var camera=go.AddComponent<Camera>();camera.enabled=false;
        camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.16f,.19f);
        camera.orthographic=true;camera.aspect=1.2f;camera.nearClipPlane=.01f;
        camera.orthographicSize=Mathf.Max(root.rect.height*Mathf.Abs(root.lossyScale.y)/2,root.rect.width*Mathf.Abs(root.lossyScale.x)/(2*camera.aspect))*1.12f;
        camera.transform.SetPositionAndRotation(root.position-root.forward*2,root.rotation);
        var previous=RenderTexture.active;var rt=RenderTexture.GetTemporary(1440,1200,24);Texture2D texture=null;
        try
        {
            foreach(var child in children)child.gameObject.layer=31;
            Canvas.ForceUpdateCanvases();camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            texture=new Texture2D(1440,1200,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1440,1200),0,0);texture.Apply();
            System.IO.Directory.CreateDirectory("docs/evidence/recorder-documentation-2026-10-21");
            System.IO.File.WriteAllBytes("docs/evidence/recorder-documentation-2026-10-21/"+name+".png",texture.EncodeToPNG());
        }
        finally
        {
            for(int i=0;i<children.Length;i++)children[i].gameObject.layer=layers[i];
            camera.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);if(texture!=null)Destroy(texture);Destroy(go);
        }
    }
}
