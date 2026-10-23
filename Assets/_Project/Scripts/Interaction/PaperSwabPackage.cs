using UnityEngine;
using UnityEngine.UI;

/// <summary>Individual paper sleeve on the inventory-owned sample visual; never creates another evidence entry.</summary>
public sealed class PaperSwabPackage : MonoBehaviour
{
    private Material paper;
    public static void Apply(Transform carrier,string source)
    {
        if(carrier.GetComponent<PaperSwabPackage>()!=null)return;
        var package=carrier.gameObject.AddComponent<PaperSwabPackage>();
        // World-sized wrapper even when imported carrier meshes use an authored scale.
        foreach(var renderer in carrier.GetComponentsInChildren<Renderer>())renderer.enabled=false;
        var template=Resources.Load<GameObject>("EditablePaperSwabPackage");
        if(template!=null)
        {
            var visual=Instantiate(template,carrier,false);
            var scale=carrier.lossyScale;
            visual.transform.localScale=new Vector3(template.transform.localScale.x/Mathf.Abs(scale.x),template.transform.localScale.y/Mathf.Abs(scale.y),template.transform.localScale.z/Mathf.Abs(scale.z));
            visual.GetComponentInChildren<Text>(true).text="BLOOD EVIDENCE\n\n"+EvidenceDisplay.Label(source)+"\n\nDRIED\nPAPER PACKAGE";
            return;
        }
        var sleeve=GameObject.CreatePrimitive(PrimitiveType.Cube);sleeve.name="Individual paper swab package";
        sleeve.transform.SetParent(carrier,false);sleeve.transform.localPosition=Vector3.zero;
        sleeve.transform.localScale=new Vector3(.055f/Mathf.Abs(carrier.lossyScale.x),.008f/Mathf.Abs(carrier.lossyScale.y),.21f/Mathf.Abs(carrier.lossyScale.z));
        EditableSceneVisuals.Remove(sleeve.GetComponent<Collider>());
        package.paper=new Material(Shader.Find("Universal Render Pipeline/Lit"));package.paper.SetColor("_BaseColor",new Color(.88f,.81f,.64f));sleeve.GetComponent<Renderer>().sharedMaterial=package.paper;
        var label=new GameObject("Source label",typeof(RectTransform),typeof(Canvas));label.transform.SetParent(sleeve.transform,false);
        label.transform.localPosition=new Vector3(0,.51f,0);label.transform.localRotation=Quaternion.Euler(90,0,0);
        label.transform.localScale=new Vector3(1f/180,.002f,1f/650);label.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;
        ((RectTransform)label.transform).sizeDelta=new Vector2(160,500);
        var text=new GameObject("Evidence identity",typeof(RectTransform),typeof(Text));text.transform.SetParent(label.transform,false);
        ((RectTransform)text.transform).sizeDelta=new Vector2(150,470);var ui=text.GetComponent<Text>();ui.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        ui.fontSize=26;ui.alignment=TextAnchor.MiddleCenter;ui.color=Color.black;ui.raycastTarget=false;ui.text="BLOOD EVIDENCE\n\n"+EvidenceDisplay.Label(source)+"\n\nDRIED\nPAPER PACKAGE";
    }
    private void OnDestroy() { if(Application.isPlaying && paper!=null)Destroy(paper); }
}
