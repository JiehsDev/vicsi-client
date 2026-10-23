using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

/// <summary>Rendered TMP glyph bounds, unequal rows, clipped pointer hits, scroll controls and real reasoning input.</summary>
public sealed class ResponsiveOptionLayoutTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }
    private readonly StringBuilder report = new();
    private int failures;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    public void RunAllTests() => StartCoroutine(Run());
    private void Check(string name, bool pass) { if (!pass) failures++; report.AppendLine((pass ? "PASS: " : "FAIL: ") + name); }
    private static bool TextFits(TMP_Text text)
    {
        text.ForceMeshUpdate(true);
        var rect = text.rectTransform.rect;
        for (int i = 0; i < text.textInfo.characterCount; i++)
        {
            var c = text.textInfo.characterInfo[i]; if (!c.isVisible) continue;
            if (c.topLeft.x < rect.xMin - .5f || c.bottomRight.x > rect.xMax + .5f
                || c.topLeft.y > rect.yMax + .5f || c.bottomRight.y < rect.yMin - .5f)
            {
                Debug.Log($"[ResponsiveBounds] {text.name}: rect={rect}; glyph={c.character}; top={c.topLeft}; bottom={c.bottomRight}; alignment={text.alignment}; preferred={text.preferredHeight}; lines={text.textInfo.lineCount}");
                return false;
            }
        }
        return !text.enableAutoSizing && !text.isTextTruncated;
    }
    private IEnumerator Run()
    {
        AutomatedTestSandbox.Activate(nameof(ResponsiveOptionLayoutTest));
        if (ReasoningCheckpointManager.Instance != null) ReasoningCheckpointManager.Instance.enabled = false;
        if (HypothesisCheckpointManager.Instance != null) HypothesisCheckpointManager.Instance.enabled = false;
        if (ProceduralTrainingGuidance.Instance != null) ProceduralTrainingGuidance.Instance.enabled = false;
        if (NarrativeEventManager.Instance != null) { NarrativeEventManager.Instance.enabled = false; NarrativeEventManager.Instance.StopAllCoroutines(); }
        var photography = FindAnyObjectByType<ScenePhotographyManager>(); photography.CancelInvoke(); photography.enabled = false;
        STCSNotificationUI.Instance.HideImmediately(); NotificationManager.Instance.UI.ClearTooltip(); NotificationManager.Instance.UI.HidePrompt();
        var ui = FindAnyObjectByType<HypothesisCheckpointUI>(); ui.AnswerForTest();
        yield return null;
        var originalCamera = Camera.main; originalCamera.tag = "Untagged";
        var cameraObject = new GameObject("ResponsiveLayoutCamera"); var camera = cameraObject.AddComponent<Camera>();
        camera.tag = "MainCamera"; camera.transform.position = new Vector3(1000,1000,1000); camera.depth = 100;
        var panel = new GameObject("MixedOptionFixture", typeof(RectTransform), typeof(Canvas));
        panel.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        var root = (RectTransform)panel.transform; root.sizeDelta = new Vector2(380, 240); root.localScale = Vector3.one * .001f;
        root.SetPositionAndRotation(camera.transform.position + Vector3.forward, Quaternion.identity);
        var header = ForensicUIStyle.Text(root, "Question", 30, Color.white); header.text = "What does the evidence support?";
        var footer = ForensicUIStyle.Text(root, "Controls", 22, Color.white); footer.text = "A: confirm";
        var layout = ResponsivePanelLayout.Create(root, header, null, footer, 240, 600, 40);
        string[] texts = { "Paul handled it.", "Paul handled it.\nTiming is unclear.", "Handled knife.\nTiming unclear.\nNo proof of use.\nMore work.",
            string.Join(" ", Enumerable.Repeat("Contact does not establish when or why it occurred.", 12)) };
        var rows = new List<Image>(); var labels = new List<TMP_Text>();
        using (var pointer = new PointerScope(root))
        {
            int clicked = -1;
            for (int i = 0; i < texts.Length; i++)
            {
                var row = ForensicUIStyle.Rect(layout.Body.Content, "Option"+i, ForensicUIStyle.SelectedFill);
                var label = ForensicUIStyle.Text(row.transform, "Text", 30, Color.white); label.text = texts[i];
                layout.AddOption(row.rectTransform, label, 78); rows.Add(row); labels.Add(label);
                int index = i; pointer.Pointer.Register("option-"+i, row.rectTransform, Vector2.zero, () => clicked=index);
            }
            layout.RegisterControls(pointer.Pointer, "fixture-scroll-"); layout.Refresh();
            labels.ForEach(t => t.ForceMeshUpdate(true));
            Check("One-line option retains the 78-unit comfortable minimum", Mathf.Abs(rows[0].rectTransform.rect.height-78)<.01f);
            Check("Two-line option grows above the minimum", rows[1].rectTransform.rect.height > rows[0].rectTransform.rect.height && labels[1].textInfo.lineCount==2);
            Check("Four-line option independently grows again", rows[2].rectTransform.rect.height > rows[1].rectTransform.rect.height && labels[2].textInfo.lineCount==4);
            Check("Automatic wrapping retains all long text at the original font size", labels[3].textInfo.lineCount>4 && labels.All(t=>t.fontSize==30 && TextFits(t)));
            for (int i=0;i<rows.Count;i++) report.AppendLine("INFO: fixture "+i+" fixed before=78; after="+rows[i].rectTransform.rect.height+"; lines="+labels[i].textInfo.lineCount);
            Check("Every label retains 20 horizontal and 16 vertical padding", rows.Zip(labels,(r,t)=>
                Mathf.Abs(r.rectTransform.rect.width-t.rectTransform.rect.width-40)<.01f
                && Mathf.Abs(r.rectTransform.rect.height-t.rectTransform.rect.height-32)<.01f).All(v=>v));
            Check("Unequal rows maintain equal widths and a 12-unit non-overlapping gap", rows.All(r=>Mathf.Abs(r.rectTransform.rect.width-300)<.01f)
                && Enumerable.Range(1,rows.Count-1).All(i=>Mathf.Abs(rows[i-1].rectTransform.anchoredPosition.y-rows[i].rectTransform.anchoredPosition.y-rows[i-1].rectTransform.rect.height-12)<.1f));
            Check("Total content caps the panel at 600 and creates a real vertical scroll area", root.rect.height==600 && layout.Body.CanScroll && layout.Body.Scroll.vertical && !layout.Body.Scroll.horizontal);
            for(int i=1;i<rows.Count;i++) rows[i].gameObject.SetActive(false);
            layout.Refresh();
            Check("Short total content stays below the cap without unnecessary scrolling", root.rect.height<600 && !layout.Body.CanScroll);
            for(int i=1;i<rows.Count;i++) rows[i].gameObject.SetActive(true);
            layout.Refresh();
            var first=rows[0].rectTransform; Vector3 edge=first.TransformPoint(new Vector3(0,first.rect.yMin+4));
            pointer.Pointer.ClickWithRay(new Ray(edge-first.forward*.5f, first.forward));
            Check("Pointer hit area includes the resized row's bottom padding", clicked==0);
            layout.Body.ScrollBy(78);
            Vector3 clipped=pointer.Pointer.WorldCentre("option-0");
            Check("Scrolled-out rows cannot intercept rays over the pinned question", pointer.Pointer.HitTest(clipped)==null);
            float y=layout.Body.Content.anchoredPosition.y;
            var down=pointer.Pointer.Targets.First(t=>t.id=="fixture-scroll-body-down"); Vector3 point=pointer.Pointer.WorldCentre(down.id);
            pointer.Pointer.ClickWithRay(new Ray(point-down.rect.forward*.5f, down.rect.forward));
            Check("Existing VR pointer can operate the visible scroll-down control", layout.Body.Content.anchoredPosition.y>y);
            layout.EnsureVisible(rows[1].rectTransform);
            Check("Bringing a selected ordinary row into view restores its hit target", pointer.Pointer.HitTest(pointer.Pointer.WorldCentre("option-1"))?.id=="option-1");
            Vector3 questionPosition=header.rectTransform.position;
            layout.Body.ScrollBy(80);
            Check("Body scrolling keeps question and confirmation controls fixed", header.rectTransform.position==questionPosition && footer.transform.IsChildOf(layout.Footer.Content));
            labels[0].text=texts[2]; layout.Refresh();
            Check("Runtime text assignment resizes synchronously without reducing font size", rows[0].rectTransform.rect.height>78 && TextFits(labels[0]) && labels[0].fontSize==30);
            header.text=string.Join(" ",Enumerable.Repeat("A longer question must remain accessible without shrinking its font.",20)); layout.Refresh();
            Check("Oversized questions get an independent scroll viewport while panel stays capped", layout.Header.CanScroll && root.rect.height<=600 && TextFits(header));
            float headerY=layout.Header.Content.anchoredPosition.y; layout.Header.ScrollBy(90);
            Check("Long-question scrolling operates separately from answer scrolling", layout.Header.Content.anchoredPosition.y>headerY);
        }
        Destroy(panel); yield return null;
        Check("Settings, decision and reasoning adopt one shared layout", FindAnyObjectByType<SettingsPauseMenu>().ResponsiveLayout!=null
            && FindAnyObjectByType<SOCOWorkflowInteraction>().ResponsiveLayout!=null && ui.ResponsiveLayout!=null);
        foreach (var type in new[]{typeof(SOCOCloseout), typeof(VendorFollowupInterview), typeof(BodyRemovalInteraction)})
        {
            var owner=FindAnyObjectByType(type); var p=(GameObject)type.GetField("panel",Private).GetValue(owner);
            Check(type.Name+" adopts shared content sizing without manual wiring",p.GetComponent<ResponsivePanelLayout>()!=null);
            var actual=p.GetComponent<ResponsivePanelLayout>();
            var actualRows=actual.Body.Content.GetComponentsInChildren<ResponsiveTextOption>(true)
                .Concat(actual.Footer.Content.GetComponentsInChildren<ResponsiveTextOption>(true)).ToArray();
            foreach(var fit in actualRows)
            {
                string old=fit.Label.text; bool active=fit.Label.gameObject.activeSelf; float size=fit.Label.fontSize;
                fit.Label.gameObject.SetActive(true); fit.Label.text=string.Join(" ",Enumerable.Repeat("Long dialogue and instructional actions must stay readable at their original size.",8));
                actual.Refresh();
                Check(type.Name+" long "+fit.Label.name+" grows and retains all glyphs",fit.RequiredHeight>100 && TextFits(fit.Label) && fit.Label.fontSize==size && ((RectTransform)p.transform).rect.height<=actual.MaximumHeight);
                fit.Label.text=old;fit.Label.gameObject.SetActive(active);actual.Refresh();
            }
        }
        Check("Recorder candidate and Notes columns use bounded scroll areas", typeof(RecorderReportUI).GetField("poolArea",Private).GetValue(RecorderReportUI.Instance)!=null
            && typeof(RecorderReportUI).GetField("notesArea",Private).GetValue(RecorderReportUI.Instance)!=null);
        var definition=new ReasoningCheckpointDefinition { id="RESPONSIVE-TEST", prompt="What can be concluded from the fingerprint?" };
        for(int i=0;i<4;i++) definition.responses.Add(new ReasoningResponse {id="response-"+i,text=i==3?string.Join(" ",Enumerable.Repeat(texts[3],2)):texts[i],weight=1});
        int submitted=0; string response=null; ui.ShowReasoning(definition,id=>{submitted++;response=id;});
        var follow=ui.ResponsiveLayout.GetComponent<HudFollowCamera>();
        typeof(HudFollowCamera).GetField("targetCamera",Private).SetValue(follow,camera);
        ui.ResponsiveLayout.Refresh(); yield return new WaitForSecondsRealtime(.3f);
        Check("Real reasoning options preserve 26-point readable text and fit their own containers", ui.DisplayedOptions.All(t=>t.fontSize==26 && TextFits(t)));
        Check("Real reasoning bounds are capped with scrolling instead of unlimited growth", ((RectTransform)ui.ResponsiveLayout.transform).rect.height<=ui.ResponsiveLayout.MaximumHeight && ui.ResponsiveLayout.Body.CanScroll);
        Check("Reasoning still owns A, tools and shared priority", ReferenceEquals(PlayerUIGate.ConfirmOwnerToken,ui) && ToolInputLock.HasUIOwners && follow.SharedLayout && follow.PlacementPriority==500);
        Capture(camera,ui.ResponsiveLayout.transform,"docs/evidence/responsive-options/reasoning-preview.png");
        var wall=GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.position=camera.transform.position+Vector3.forward;wall.transform.localScale=new Vector3(3,3,.08f);
        Physics.SyncTransforms();yield return new WaitForSecondsRealtime(.5f);
        Check("Resized reasoning remains wall-safe or defers when no safe fit exists",follow.IsLayoutSuppressed || follow.PanelFits(camera.transform.position,ui.ResponsiveLayout.transform.position,ui.ResponsiveLayout.transform.rotation));
        Destroy(wall); yield return new WaitForSecondsRealtime(.3f);
        var companion=new GameObject("ResponsiveCompanion",typeof(RectTransform),typeof(Canvas));
        ((RectTransform)companion.transform).sizeDelta=new Vector2(.35f,.13f); companion.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;
        var companionFollow=HudFollowCamera.ConfigureShared(companion,null,400,true);
        typeof(HudFollowCamera).GetField("targetCamera",Private).SetValue(companionFollow,camera);
        yield return new WaitForSecondsRealtime(.3f);
        Check("Shared layout preserves a non-overlapping companion or defers it",companionFollow.IsLayoutSuppressed
            || RectTransformUtility.CalculateRelativeRectTransformBounds(ui.ResponsiveLayout.transform,companion.transform).min.y>=((RectTransform)ui.ResponsiveLayout.transform).rect.yMax);
        Destroy(companion); yield return null;
        var zero=ui.Pointer.Targets.First(t=>t.id=="reasoning-option-0"); var centre=ui.Pointer.WorldCentre(zero.id);
        ui.Pointer.ClickWithRay(new Ray(centre-zero.rect.forward*.5f,zero.rect.forward));
        Check("Reasoning pointer selects a response without immediately submitting it",submitted==0 && ui.DisplayedHint.Contains("Confirm Answer"));
        yield return null;
        InputSystem.RegisterLayout("{\"name\":\"ResponsiveXR\",\"extend\":\"XRController\",\"controls\":[{\"name\":\"primaryButton\",\"layout\":\"Button\",\"format\":\"FLT\"},{\"name\":\"triggerPressed\",\"layout\":\"Button\",\"format\":\"FLT\"}]}");
        var device=InputSystem.AddDevice("ResponsiveXR");InputSystem.SetDeviceUsage(device,CommonUsages.RightHand);
        InputSystem.QueueDeltaStateEvent((ButtonControl)device["primaryButton"],1f); yield return null; yield return null;
        Check("A confirms the original selected ID exactly once after pointer selection",submitted==1 && response=="response-0" && !ui.IsShowing);
        InputSystem.QueueDeltaStateEvent((ButtonControl)device["primaryButton"],0f);yield return null;
        int legacySubmitted=0;string legacyReasoning=null;
        var legacy=new HypothesisCheckpoint {id="RESPONSIVE-LEGACY",promptText="Which explanation fits?",requiresReasoning=true};
        legacy.options.Add(new HypothesisOption {id="theory-test",text=texts[2]});
        legacy.reasoningOptions.Add(new ReasoningOption {id="justification-test",text=texts[3]});
        ui.Show(legacy,(id,text,why,whyText)=>{legacySubmitted++;legacyReasoning=why;});
        yield return null;yield return null;
        var legacyTarget=ui.Pointer.Targets.First(t=>t.id=="reasoning-option-0");
        Vector3 legacyCentre=ui.Pointer.WorldCentre(legacyTarget.id);
        Vector3 legacyForward=legacyTarget.rect.forward;
        ui.Pointer.RayOverride=()=>new Ray(legacyCentre-legacyForward*.5f,legacyForward);
        InputSystem.QueueDeltaStateEvent((ButtonControl)device["primaryButton"],1f);
        InputSystem.QueueDeltaStateEvent((ButtonControl)device["triggerPressed"],1f);yield return null;yield return null;
        Check("Simultaneous trigger and A advances only one stage of a two-stage question",legacySubmitted==0 && ui.IsShowing && ui.DisplayedHint.Contains("Confirm Answer"));
        ui.Pointer.RayOverride=null;
        InputSystem.QueueDeltaStateEvent((ButtonControl)device["primaryButton"],0f);InputSystem.QueueDeltaStateEvent((ButtonControl)device["triggerPressed"],0f);yield return null;yield return null;
        InputSystem.QueueDeltaStateEvent((ButtonControl)device["primaryButton"],1f);yield return null;yield return null;
        Check("A fresh press submits the original second-stage reasoning ID exactly once",legacySubmitted==1 && legacyReasoning=="justification-test" && !ui.IsShowing);
        InputSystem.QueueDeltaStateEvent((ButtonControl)device["primaryButton"],0f);yield return null;InputSystem.RemoveDevice(device);InputSystem.RemoveLayout("ResponsiveXR");
        var settings=FindAnyObjectByType<SettingsPauseMenu>(); settings.Show(); yield return null; settings.RequestRestart(); settings.ResponsiveLayout.Refresh();
        Check("Settings confirmation retains bounded responsive options and readable fonts", settings.Confirmation=="restart"
            && settings.ResponsiveLayout.Body.Content.GetComponentsInChildren<ResponsiveTextOption>().All(r=>TextFits(r.Label)));
        settings.Close();
        originalCamera.tag="MainCamera";Destroy(cameraObject); yield return null;
        Check("Layout fixture releases modal and tool ownership", !PlayerUIGate.IsBlocked && !ToolInputLock.HasUIOwners);
        report.AppendLine($"=== {(failures==0?"PASS":"FAIL")}: ResponsiveOptionLayoutTest ===");Report=report.ToString();IsDone=true;
    }
    private sealed class PointerScope : System.IDisposable
    {
        public WorldPanelPointer Pointer;
        public PointerScope(RectTransform root) { Pointer=new WorldPanelPointer(root,"LayoutFixture"); }
        public void Dispose() => Pointer.Dispose();
    }
    private static void Capture(Camera camera,Transform panel,string path)
    {
#if UNITY_EDITOR
        var probe=new GameObject("LayoutPreview");var c=probe.AddComponent<Camera>();
        c.transform.SetPositionAndRotation(panel.position-panel.forward*1.15f,panel.rotation);c.fieldOfView=48;
        c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=new Color(.02f,.025f,.03f);
        var rt=new RenderTexture(1000,1100,24);var texture=new Texture2D(1000,1100,TextureFormat.RGB24,false);var old=RenderTexture.active;
        try {c.targetTexture=rt;c.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,1000,1100),0,0);texture.Apply();System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));System.IO.File.WriteAllBytes(path,texture.EncodeToPNG());}
        finally {RenderTexture.active=old;c.targetTexture=null;rt.Release();Destroy(rt);Destroy(texture);Destroy(probe);}
#endif
    }
}
