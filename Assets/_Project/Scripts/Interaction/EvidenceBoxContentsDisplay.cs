using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Render-only custody representations. No evidence behaviours or colliders are cloned.</summary>
public sealed class EvidenceBoxContentsDisplay : MonoBehaviour
{
    private GameObject contents;
    private Material trayMaterial;
    public int ItemCount { get; private set; }
    public Bounds Interior => new Bounds(Vector3.zero, new Vector3(.39f,.20f,.29f));
    public readonly Dictionary<string, Bounds> ItemBounds = new();

    private Transform MetricRoot(string name)
    {
        var root = EditableSceneVisuals.Create(name).transform;
        root.SetParent(transform, false);
        var scale = transform.lossyScale;
        root.localScale = new Vector3(1/scale.x,1/scale.y,1/scale.z);
        return root;
    }
    private void Awake()
    {
        BuildTray();
    }
    [EditableSceneBuilder]
    private void BuildTray()
    {
        using (EditableSceneVisuals.Begin(this,"BuildTray"))
        {
        var authoredTray=transform.Find("Open evidence tray");
        foreach(var renderer in GetComponentsInChildren<Renderer>())
            if(authoredTray==null||!renderer.transform.IsChildOf(authoredTray))renderer.enabled=false;
        var tray=MetricRoot("Open evidence tray");
        trayMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        trayMaterial.color=new Color(.18f,.21f,.24f);
        Cube(tray,"Base",new Vector3(0,-.112f,0),new Vector3(.44f,.024f,.34f));
        Cube(tray,"Left",new Vector3(-.21f,0,0),new Vector3(.025f,.22f,.34f));
        Cube(tray,"Right",new Vector3(.21f,0,0),new Vector3(.025f,.22f,.34f));
        Cube(tray,"Front",new Vector3(0,0,-.16f),new Vector3(.40f,.22f,.025f));
        Cube(tray,"Back",new Vector3(0,0,.16f),new Vector3(.40f,.22f,.025f));
        Cube(tray,"Fingerprint shelf",new Vector3(0,.043f,.112f),new Vector3(.39f,.014f,.062f));
        }
    }
    private void Cube(Transform parent,string name,Vector3 position,Vector3 scale)
    {
        var go=EditableSceneVisuals.Primitive(PrimitiveType.Cube); go.name=name;
        go.transform.SetParent(parent,false); go.transform.localPosition=position; go.transform.localScale=scale;
        var collider=go.GetComponent<Collider>(); EditableSceneVisuals.Remove(collider);
        go.GetComponent<Renderer>().sharedMaterial=trayMaterial;
    }
    public GameObject Stage(IReadOnlyList<string> ids)
    {
        var root=MetricRoot("Evidence box contents"); root.gameObject.SetActive(false);
        try
        {
            var inventory=EvidenceInventory.Instance;
            var groups=ids.GroupBy(id=>inventory.GetSampleSource(id)==null ? 0 : inventory.IsFingerprintSample(id) ? 2 : 1);
            foreach(var group in groups)
            {
                var entries=group.ToArray();
                for(int index=0;index<entries.Length;index++)
                {
                    string id=entries[index];
                    var item=EditableSceneVisuals.Create(id).transform; item.SetParent(root,false);
                    var definition=inventory.GetDefinition(id);
                    var prefab=inventory.GetSampleSource(id)!=null ? definition.heldPrefab : definition.depositedVisual != null ? definition.depositedVisual : definition.heldPrefab;
                    if(prefab!=null)
                    foreach(var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
                    {
                        var sourceRenderer=filter.GetComponent<MeshRenderer>();
                        if(filter.sharedMesh==null || sourceRenderer==null) continue;
                        var mesh=EditableSceneVisuals.Create(filter.name,typeof(MeshFilter),typeof(MeshRenderer));
                        mesh.transform.SetParent(item,false);
                        mesh.transform.localPosition=prefab.transform.InverseTransformPoint(filter.transform.position);
                        mesh.transform.localRotation=Quaternion.Inverse(prefab.transform.rotation)*filter.transform.rotation;
                        var sourceScale=filter.transform.lossyScale; var prefabScale=prefab.transform.lossyScale;
                        mesh.transform.localScale=new Vector3(sourceScale.x/prefabScale.x,sourceScale.y/prefabScale.y,sourceScale.z/prefabScale.z);
                        mesh.GetComponent<MeshFilter>().sharedMesh=filter.sharedMesh;
                        mesh.GetComponent<MeshRenderer>().sharedMaterials=sourceRenderer.sharedMaterials;
                    }
                    if(item.childCount==0) Cube(item,"Representation",Vector3.zero,group.Key==1 ? new Vector3(.03f,.08f,.03f) : new Vector3(.12f,.01f,.08f));
                    var bounds=Measure(item,item);
                    int axis=group.Key==1 ? Largest(bounds.size) : Smallest(bounds.size);
                    item.localRotation=axis==0 ? Quaternion.Euler(0,0,90) : axis==2 ? Quaternion.Euler(-90,0,0) : Quaternion.identity;
                    bounds=Measure(item,root);
                    Vector3 cellSize,center;
                    if(group.Key==0) { cellSize=new Vector3(.23f,.17f/entries.Length,.22f); center=new Vector3(-.07f,-.09f+(index+.5f)*cellSize.y,-.025f); }
                    else if(group.Key==1) { cellSize=new Vector3(.085f,.17f,.22f/entries.Length); center=new Vector3(.135f,-.005f,-.135f+(index+.5f)*cellSize.z); }
                    else { cellSize=new Vector3(.37f/entries.Length,.04f,.055f); center=new Vector3(-.185f+(index+.5f)*cellSize.x,.05f,.112f); }
                    float fit=Mathf.Min(cellSize.x/Mathf.Max(bounds.size.x,.0001f),cellSize.y/Mathf.Max(bounds.size.y,.0001f),cellSize.z/Mathf.Max(bounds.size.z,.0001f))*.88f;
                    item.localScale=Vector3.one*fit;
                    bounds=Measure(item,root);
                    item.localPosition=center-bounds.center;
                    if(group.Key==2) item.localPosition+=Vector3.up*bounds.extents.y; // Rest face-up cards on their shelf.
                }
            }
            return root.gameObject;
        }
        catch { Destroy(root.gameObject); throw; }
    }
    public void Commit(GameObject stage)
    {
        if(contents!=null) { contents.SetActive(false); Destroy(contents); }
        contents=stage; ItemBounds.Clear();
        foreach(Transform child in contents.transform) ItemBounds[child.name]=Measure(child,contents.transform);
        ItemCount=ItemBounds.Count; contents.SetActive(true);
    }
    private static int Largest(Vector3 v)=>v.x>v.y && v.x>v.z ? 0 : v.z>v.y ? 2 : 1;
    private static int Smallest(Vector3 v)=>v.x<v.y && v.x<v.z ? 0 : v.z<v.y ? 2 : 1;
    private static Bounds Measure(Transform item,Transform relative)
    {
        var result=new Bounds(); bool first=true;
        foreach(var mesh in item.GetComponentsInChildren<MeshFilter>(true))
        {
            var b=mesh.sharedMesh.bounds;
            for(int i=0;i<8;i++)
            {
                var corner=b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                var point=relative.InverseTransformPoint(mesh.transform.TransformPoint(corner));
                if(first) { result=new Bounds(point,Vector3.zero); first=false; } else result.Encapsulate(point);
            }
        }
        return result;
    }
    private void OnDestroy() { if(trayMaterial!=null) Destroy(trayMaterial); }
}
