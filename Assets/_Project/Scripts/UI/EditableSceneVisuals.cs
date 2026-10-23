using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Marks a visual builder that the scene-authoring menu can materialize without running gameplay.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class EditableSceneBuilderAttribute : Attribute { }

/// <summary>
/// Stores real scene objects for procedural visual builders. Builders still wire their runtime
/// fields/events, but reuse these objects and retain the artist's transforms and visual styling.
/// Gameplay visibility, live text, pointer poses and responsive layout remain runtime controlled.
/// </summary>
[DisallowMultipleComponent]
public sealed class EditableSceneVisuals : MonoBehaviour
{
    [Serializable] public sealed class Group
    {
        public MonoBehaviour owner;
        public string builder;
        public List<GameObject> objects = new();
    }
    [SerializeField] private List<Group> groups = new();
    public IReadOnlyList<Group> Groups => groups;
    private static Scope current;
    private static readonly HashSet<GameObject> authoredObjects=new();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset(){current=null;authoredObjects.Clear();}
    public static Scope Begin(MonoBehaviour owner,string builder,bool activateReused=false)
    {
        var storage=owner.GetComponent<EditableSceneVisuals>();
        if(storage==null && !Application.isPlaying)storage=owner.gameObject.AddComponent<EditableSceneVisuals>();
        if(storage!=null)foreach(var saved in storage.groups)foreach(var go in saved.objects)if(go!=null)authoredObjects.Add(go);
        var group=storage!=null?storage.groups.Find(g=>g.owner==owner&&g.builder==builder):null;
        if(group==null && storage!=null && !Application.isPlaying)
        {group=new Group{owner=owner,builder=builder};storage.groups.Add(group);}
        return new Scope(owner,group,activateReused);
    }
    public static void Retire(GameObject go){if(authoredObjects.Contains(go))go.SetActive(false);else Remove(go);}
    public static void Preserve(params GameObject[] objects){if(current!=null)foreach(var go in objects)current.Preserve(go);}
    public static GameObject Create(string name,params Type[] components)
    {
        var go=current!=null?current.Get(name,()=>new GameObject(name,components),components):new GameObject(name,components);
        InitializeFont(go.GetComponent<TMP_Text>());return go;
    }
    static void InitializeFont(TMP_Text text)
    {
        if(text==null)return;
        if(text.font==null)text.font=TMP_Settings.defaultFontAsset;
        if(text.font!=null&&(text.fontSharedMaterial==null||!text.fontSharedMaterial.HasProperty("_FaceColor")))text.fontSharedMaterial=text.font.material;
    }
    public static GameObject Primitive(PrimitiveType shape)
        => current!=null?current.Get(shape.ToString(),()=>GameObject.CreatePrimitive(shape)):GameObject.CreatePrimitive(shape);
    public static GameObject InstantiateVisual(GameObject prefab,Transform parent,bool worldPositionStays)
    {
        if(current==null)return UnityEngine.Object.Instantiate(prefab,parent,worldPositionStays);
        return current.Get(prefab.name,()=>UnityEngine.Object.Instantiate(prefab,parent,worldPositionStays));
    }
    public static T Component<T>(GameObject go) where T:UnityEngine.Component
    {var value=go.GetComponent<T>();if(value==null)value=go.AddComponent<T>();if(value is TMP_Text text)InitializeFont(text);return value;}
    public static void Remove(UnityEngine.Object value)
    {
        if(value==null)return;
        if(value is Collider collider)collider.enabled=false;
        if(Application.isPlaying)UnityEngine.Object.Destroy(value);else UnityEngine.Object.DestroyImmediate(value);
    }
    public sealed class Scope:IDisposable
    {
        readonly Scope previous;
        readonly MonoBehaviour owner;
        readonly Group group;
        readonly bool activateReused;
        readonly List<Snapshot> snapshots=new();
        int index;
        internal Scope(MonoBehaviour owner,Group group,bool activateReused)
        {
            this.owner=owner;this.group=group;this.activateReused=activateReused;previous=current;current=this;
            if(group!=null)foreach(var go in group.objects)if(go!=null)authoredObjects.Add(go);
        }
        internal void Preserve(GameObject go){if(go!=null&&group!=null&&group.objects.Count>0)snapshots.Add(new Snapshot(go));}
        internal GameObject Get(string name,Func<GameObject> create,params Type[] components)
        {
            GameObject go=group!=null&&index<group.objects.Count?group.objects[index]:null;
            if(go!=null)
            {
                snapshots.Add(new Snapshot(go));
                if(activateReused)go.SetActive(true);
                foreach(var type in components)if(go.GetComponent(type)==null)go.AddComponent(type);
            }
            else
            {
                go=create();
                if(group!=null)authoredObjects.Add(go);
#if UNITY_EDITOR
                if(!Application.isPlaying)UnityEditor.Undo.RegisterCreatedObjectUndo(go,"Create editable scene visual");
#endif
                if(group!=null&&!Application.isPlaying)
                {if(index<group.objects.Count)group.objects[index]=go;else group.objects.Add(go);}
                else if(group!=null)Debug.LogWarning("Visual authoring needs refresh: "+owner.GetType().Name+"."+group.builder+" / "+name,owner);
            }
            index++;return go;
        }
        public void Dispose()
        {
            // Restore in creation/parent order after the builder has rebound all runtime fields.
            foreach(var snapshot in snapshots)snapshot.Restore();
            if(group!=null)
                foreach(var go in group.objects)if(go!=null)go.hideFlags=HideFlags.None;
            current=previous;
        }
    }
    sealed class Snapshot
    {
        readonly Transform transform,parent;
        readonly Vector3 position,scale;
        readonly Quaternion rotation;
        readonly RectTransform rect;
        readonly Vector2 anchorMin,anchorMax,pivot,size;
        readonly Vector3 anchored;
        readonly Renderer renderer;
        readonly Material[] materials;
        readonly Graphic graphic;
        readonly Color color;
        readonly Material graphicMaterial;
        readonly Image image;
        readonly Sprite sprite;
        readonly Canvas canvas;
        readonly RenderMode renderMode;
        readonly int sortingOrder;
        readonly Text text;
        readonly string content;
        readonly Font font;
        readonly int fontSize;
        readonly FontStyle fontStyle;
        readonly TextAnchor alignment;
        readonly TMP_Text tmp;
        readonly TMP_FontAsset tmpFont;
        readonly float tmpSize;
        readonly FontStyles tmpStyle;
        readonly TextAlignmentOptions tmpAlignment;
        readonly Vector4 margin;
        readonly LineRenderer line;
        readonly float lineWidth;
        readonly Color lineStart,lineEnd;
        readonly List<(UnityEngine.Component component,string json)> settings=new();
        public Snapshot(GameObject go)
        {
            transform=go.transform;parent=transform.parent;position=transform.localPosition;rotation=transform.localRotation;scale=transform.localScale;
            rect=transform as RectTransform;
            if(rect!=null){anchorMin=rect.anchorMin;anchorMax=rect.anchorMax;pivot=rect.pivot;size=rect.sizeDelta;anchored=rect.anchoredPosition3D;}
            renderer=go.GetComponent<Renderer>();if(renderer!=null)materials=renderer.sharedMaterials;
            graphic=go.GetComponent<Graphic>();if(graphic!=null){color=graphic.color;graphicMaterial=graphic.material;}
            image=go.GetComponent<Image>();if(image!=null)sprite=image.sprite;
            canvas=go.GetComponent<Canvas>();if(canvas!=null){renderMode=canvas.renderMode;sortingOrder=canvas.sortingOrder;}
            text=go.GetComponent<Text>();tmp=go.GetComponent<TMP_Text>();
            if(text!=null){content=text.text;font=text.font;fontSize=text.fontSize;fontStyle=text.fontStyle;alignment=text.alignment;}
            if(tmp!=null){content=tmp.text;tmpFont=tmp.font;tmpSize=tmp.fontSize;tmpStyle=tmp.fontStyle;tmpAlignment=tmp.alignment;margin=tmp.margin;}
            line=go.GetComponent<LineRenderer>();if(line!=null){lineWidth=line.widthMultiplier;lineStart=line.startColor;lineEnd=line.endColor;}
            foreach(var component in go.GetComponents<UnityEngine.Component>())
                if(component is LayoutGroup || component is LayoutElement || component is ContentSizeFitter || component is Shadow
                    || component is ScrollRect || component is ResponsivePanelLayout || component is HudFollowCamera)
                    settings.Add((component,JsonUtility.ToJson(component)));
        }
        public void Restore()
        {
            if(transform==null)return;
            transform.SetParent(parent,false);transform.SetLocalPositionAndRotation(position,rotation);transform.localScale=scale;
            if(rect!=null){rect.anchorMin=anchorMin;rect.anchorMax=anchorMax;rect.pivot=pivot;rect.sizeDelta=size;rect.anchoredPosition3D=anchored;}
            if(renderer!=null)renderer.sharedMaterials=materials;
            if(graphic!=null){graphic.color=color;if(tmp==null||(graphicMaterial!=null&&graphicMaterial.HasProperty("_FaceColor")))graphic.material=graphicMaterial;}
            if(image!=null)image.sprite=sprite;
            if(canvas!=null){canvas.renderMode=renderMode;canvas.sortingOrder=sortingOrder;}
            if(text!=null){text.text=content;text.font=font;text.fontSize=fontSize;text.fontStyle=fontStyle;text.alignment=alignment;}
            if(tmp!=null){tmp.text=content;if(tmpFont!=null)tmp.font=tmpFont;tmp.fontSize=tmpSize;tmp.fontStyle=tmpStyle;tmp.alignment=tmpAlignment;tmp.margin=margin;}
            if(line!=null){line.widthMultiplier=lineWidth;line.startColor=lineStart;line.endColor=lineEnd;}
            foreach(var setting in settings)if(setting.component!=null)JsonUtility.FromJsonOverwrite(setting.json,setting.component);
        }
    }
}
