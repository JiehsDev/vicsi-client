using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Real authored object reuse, visual override preservation and conditional template checks.</summary>
public sealed class EditableSceneVisualsTest:MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }
    readonly StringBuilder report=new("=== EditableSceneVisualsTest ===\n");
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    int failures;
    void Check(string name,bool ok){report.AppendLine((ok?"PASS: ":"FAIL: ")+name);if(!ok)failures++;}
    public void RunAllTests()=>StartCoroutine(Run());
    IEnumerator Run()
    {
        yield return null;
        var cordon=FindAnyObjectByType<SceneCordon>();var station=cordon.PPEStation;
        Check("Exactly one authored glove station is used",station!=null&&FindObjectsByType<ArrivalPPEStation>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length==1);
        Check("Cordon references its scene-authored supply station",(ArrivalPPEStation)typeof(SceneCordon).GetField("authoredPPEStation",Flags).GetValue(cordon)==station);
        Check("Authored register is the runtime interaction target",cordon.AuthoredRegister!=null&&cordon.AccessRegister.UI==cordon.AuthoredRegister);
        Check("One preparation station survives startup",FindObjectsByType<SamplePreparationStation>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length==1);
        Check("One comparison desk survives startup",FindObjectsByType<FingerprintComparisonStation>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length==1);
        var guide=station.GetComponent<GloveStationGuide>();
        Check("Authored supply and preparation guide remains enabled with a valid route",guide!=null&&guide.enabled&&guide.Route!=null&&guide.Route.waypoints.Length>1);
        var all=FindObjectsByType<EditableSceneVisuals>(FindObjectsInactive.Include,FindObjectsSortMode.None);
        foreach(var storage in all)
            foreach(var group in storage.Groups)
                Check(group.owner.GetType().Name+"."+group.builder+" has intact authored scene objects",group.objects.Count>0&&group.objects.All(g=>g!=null&&g.scene==storage.gameObject.scene));
        Check("Generated scene visuals have no DontSave flag",all.SelectMany(s=>s.Groups).SelectMany(g=>g.objects).Where(g=>g!=null).All(g=>(g.hideFlags&HideFlags.DontSave)==0));
        PreserveStationEdits(station);
        var scale=FindAnyObjectByType<PhotographicScaleAccessory>(FindObjectsInactive.Include);
        var ruler=(GameObject)typeof(PhotographicScaleAccessory).GetField("authoredRuler",Flags).GetValue(scale);
        Check("Camera has an authored metric ruler",ruler!=null&&ruler.GetComponent<MeshFilter>()?.sharedMesh!=null);
        Check("Authored ruler starts hidden and is not duplicated",ruler!=null&&!ruler.activeSelf&&ruler.GetComponents<MeshRenderer>().Length==1&&ruler.GetComponents<MeshFilter>().Length==1);
        var paper=Resources.Load<GameObject>("EditablePaperSwabPackage");
        Check("Paper package is an editable prefab",paper!=null&&paper.GetComponentInChildren<Text>(true)!=null);
        var sample=new GameObject("Editable package verification carrier");
        try
        {
            PaperSwabPackage.Apply(sample.transform,"EVD-015");
            var label=sample.GetComponentInChildren<Text>(true);
            Check("Runtime package uses the authored template",sample.transform.childCount==1&&sample.GetComponentInChildren<MeshFilter>()?.sharedMesh==paper.GetComponent<MeshFilter>().sharedMesh);
            Check("Source label updates without raw evidence IDs",label!=null&&!label.text.Contains("EVD-")&&label.text.Contains("BLOOD EVIDENCE"));
            Check("Template introduces no sample collider",sample.GetComponentsInChildren<Collider>().Length==0);
        }
        finally{Destroy(sample);}
        report.AppendLine(failures==0?"=== PASS ===":"=== FAIL: "+failures+" failure(s) ===");Report=report.ToString();IsDone=true;
    }
    void PreserveStationEdits(ArrivalPPEStation station)
    {
        var top=station.transform.Find("Glove supply tabletop");var renderer=top.GetComponent<Renderer>();
        var label=station.GetComponentInChildren<Text>(true);var originalPosition=top.localPosition;
        var originalScale=top.localScale;var originalMaterial=renderer.sharedMaterial;var originalFontSize=label.fontSize;
        var editedMaterial=new Material(originalMaterial);editedMaterial.color=Color.magenta;
        var position=originalPosition+new Vector3(.017f,0,0);var scale=originalScale+new Vector3(.021f,0,0);
        var pair=typeof(ArrivalPPEStation).GetField("pair",Flags).GetValue(station);
        int children=station.GetComponentsInChildren<Transform>(true).Length;
        try
        {
            top.localPosition=position;top.localScale=scale;renderer.sharedMaterial=editedMaterial;label.fontSize=originalFontSize+3;
            typeof(ArrivalPPEStation).GetMethod("BuildVisuals",Flags).Invoke(station,null);
            Check("Builder reuses the authored glove model",typeof(ArrivalPPEStation).GetField("pair",Flags).GetValue(station)==pair);
            Check("Rebinding creates no duplicate station objects",station.GetComponentsInChildren<Transform>(true).Length==children);
            Check("Edited local position survives rebinding",top.localPosition==position);
            Check("Edited dimensions survive rebinding",top.localScale==scale);
            Check("Edited material survives rebinding",renderer.sharedMaterial==editedMaterial);
            Check("Edited label font size survives rebinding",label.fontSize==originalFontSize+3);
        }
        finally{top.localPosition=originalPosition;top.localScale=originalScale;renderer.sharedMaterial=originalMaterial;label.fontSize=originalFontSize;Destroy(editedMaterial);}
    }
}
