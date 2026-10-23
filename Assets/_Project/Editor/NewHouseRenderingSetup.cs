using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class NewHouseRenderingSetup
{
    const string ScenePath="Assets/_Project/Scenes/Scenarios/CSI_Environment.unity";
    static double next;
    static NewHouseRenderingSetup(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(EditorApplication.timeSinceStartup<next||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        next=EditorApplication.timeSinceStartup+1;
        const string request="Temp/new-house-rendering-request.txt";
        if(!File.Exists(request))return;
        var command=File.ReadAllText(request).Trim();File.Delete(request);
        try
        {
            if(command=="inspect")Inspect();
            if(command=="capture")CaptureAll();
            if(command=="apply")Apply();
            if(command=="validate")Validate();
            if(command=="refine")Refine();
        }
        catch(Exception e){File.WriteAllText("Temp/new-house-rendering-error.txt",e.ToString());}
    }
    static Transform House()=>SceneManager.GetActiveScene().GetRootGameObjects().Single(g=>g.name=="Vicsi_House").transform;
    static string PathOf(Transform t)=>t.parent==null?t.name:PathOf(t.parent)+"/"+t.name;
    static void Inspect()
    {
        var scene=SceneManager.GetActiveScene();var house=House();
        var s=new StringBuilder($"scene={scene.path} dirty={scene.isDirty} playing={EditorApplication.isPlaying} compilationFailed={EditorUtility.scriptCompilationFailed}\n");
        foreach(var r in house.GetComponentsInChildren<Renderer>(true))
        {
            var b=r.bounds;
            s.AppendLine($"{PathOf(r.transform)} active={r.gameObject.activeInHierarchy} min={b.min:F4} max={b.max:F4} p={r.transform.position:F4} scale={r.transform.lossyScale:F3} materials="+string.Join(";",r.sharedMaterials.Select(m=>m==null?"NULL":$"{m.name} shader={m.shader.name} path={AssetDatabase.GetAssetPath(m)} normal={(m.HasProperty("_BumpMap")?m.GetTexture("_BumpMap"):null)}")));
        }
        foreach(var l in house.GetComponentsInChildren<Light>(true))s.AppendLine($"LIGHT {PathOf(l.transform)} p={l.transform.position:F3} enabled={l.enabled} intensity={l.intensity} range={l.range} shadows={l.shadows}");
        File.WriteAllText("Temp/new-house-rendering-inspect.txt",s.ToString());
    }
    static void CaptureAll()
    {
        var scene=SceneManager.GetActiveScene();bool wasDirty=scene.isDirty;
        try
        {
            Capture("kitchen",new Vector3(14.1f,2.85f,5.25f),new Vector3(12.7f,1.8f,7.4f));
            Capture("kitchen-ceiling",new Vector3(13.3f,2.85f,6.2f),new Vector3(13f,4f,8f));
            Capture("living",new Vector3(18.8f,2.85f,7f),new Vector3(17.5f,2.2f,10f));
            Capture("living-ceiling",new Vector3(17.5f,2.85f,10.2f),new Vector3(17.5f,4f,8f));
            Capture("upstairs",new Vector3(13.3f,5.85f,6.7f),new Vector3(13f,6.85f,8.4f));
            Capture("upstairs-hall",new Vector3(15.1f,5.85f,10.4f),new Vector3(14.2f,6.8f,10.5f));
            Capture("tv",new Vector3(17.64f,2.5f,9.5f),new Vector3(17.64f,2f,11.1f));
            Capture("exterior",new Vector3(25f,6f,-.5f),new Vector3(15f,4f,9.8f));
        }
        finally{if(!EditorApplication.isPlaying&&!wasDirty&&scene.isDirty)EditorSceneManager.SaveScene(scene);}
        Inspect();
    }
    static Bounds BoundsOf(Transform t)
    {
        var rs=t.GetComponentsInChildren<Renderer>(true);if(rs.Length==0)throw new InvalidOperationException("No visual on "+t.name);
        var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);return b;
    }
    static void Move(Transform t,Vector3 delta)
    {Undo.RecordObject(t,"Align new house furnishings");t.position+=delta;}
    static void Ground(Transform t,float y)=>Move(t,Vector3.up*(y-BoundsOf(t).min.y));
    static void FitFloor(Transform t,float left,float right,float front,float back)
    {
        Undo.RecordObject(t,"Join upper floor and ceiling panels");var b=t.GetComponent<Renderer>().bounds;
        var scale=t.localScale;scale.x*=(right-left)/b.size.x;scale.z*=(back-front)/b.size.z;t.localScale=scale;
        b=t.GetComponent<Renderer>().bounds;t.position+=new Vector3((left+right)*.5f-b.center.x,0,(front+back)*.5f-b.center.z);
    }
    const string Folder="Assets/_Project/Art/Materials/NewHouse";
    const string MeshFolder="Assets/_Project/Art/Environments/MODELS/House/Rendering";
    [MenuItem("ViCSI/Scene Setup/Repair New House Rendering and Alignment")]
    public static void Apply()
    {
        var scene=SceneManager.GetActiveScene();
        if(EditorApplication.isPlayingOrWillChangePlaymode||scene.path!=ScenePath)throw new InvalidOperationException("Open CSI_Environment outside Play.");
        var house=House();
        if(AssetDatabase.GetAssetPath(house.Find("strop").GetComponent<Renderer>().sharedMaterial)==Folder+"/CeilingPlaster.mat")
            throw new InvalidOperationException("New house repair already applied; undo the repair group before applying again.");
        if(!File.Exists("Temp/new-house-before-repair.unity")&&!EditorSceneManager.SaveScene(scene,"Temp/new-house-before-repair.unity",true))throw new IOException("Could not snapshot current scene.");
        Undo.IncrementCurrentGroup();Undo.SetCurrentGroupName("Repair new house rendering and alignment");
        Directory.CreateDirectory(Folder);Directory.CreateDirectory(MeshFolder);
        var plaster=new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/OldHouse/CeilingPlaster.mat"));
        plaster.name="New House Ceiling Plaster";AssetDatabase.CreateAsset(plaster,Folder+"/CeilingPlaster.mat");

        // Fit each authored floor polygon to its neighbors, rather than snapping this irregular house to a grid.
        float left=house.Find("Plane.006").GetComponent<Renderer>().bounds.max.x;
        float right=house.Find("Plane.010").GetComponent<Renderer>().bounds.max.x;
        float seam=house.Find("Plane.010").GetComponent<Renderer>().bounds.min.x;
        var seven=house.Find("Plane.007");var nine=house.Find("Plane.009");
        var b7=seven.GetComponent<Renderer>().bounds;var b9=nine.GetComponent<Renderer>().bounds;
        FitFloor(seven,left,right,b7.min.z,b7.max.z);
        FitFloor(house.Find("Plane.008"),left,right,b7.max.z,b9.min.z);
        FitFloor(nine,left,seam,b9.min.z,b9.max.z);
        CreateFirstFloorLining(house,plaster);

        // The supplied flat upper ceiling sits above the roof's underside; lower its lining slightly below that surface.
        var upper=house.Find("strop");var filter=upper.GetComponent<MeshFilter>();var original=filter.sharedMesh;
        var mesh=UnityEngine.Object.Instantiate(original);mesh.name="New House Upper Ceiling";
        var vertices=mesh.vertices;
        for(int sub=0;sub<mesh.subMeshCount;sub++)
        {var triangles=mesh.GetTriangles(sub);for(int i=0;i<triangles.Length;i+=3)
            if(Vector3.Cross(upper.TransformVector(vertices[triangles[i+1]]-vertices[triangles[i]]),upper.TransformVector(vertices[triangles[i+2]]-vertices[triangles[i]])).y>0)
            {int swap=triangles[i+1];triangles[i+1]=triangles[i+2];triangles[i+2]=swap;}mesh.SetTriangles(triangles,sub);}
        mesh.normals=Enumerable.Repeat(upper.InverseTransformDirection(Vector3.down),vertices.Length).ToArray();
        AssetDatabase.CreateAsset(mesh,MeshFolder+"/UpperCeiling.asset");
        Undo.RecordObject(filter,"Correct upper ceiling facing");filter.sharedMesh=mesh;
        Undo.RecordObject(upper.GetComponent<Renderer>(),"Use upper ceiling plaster");upper.GetComponent<Renderer>().sharedMaterial=plaster;
        Move(upper,new Vector3(0,6.93f-upper.GetComponent<Renderer>().bounds.center.y,0));

        var set=house.Find("CabinetBset");var units=set.Cast<Transform>().Where(t=>t.GetComponent<Renderer>()!=null).ToArray();
        var sink=house.Find("Furniture/Sink01");var sinkUnit=set.Find("CabinetB_Sink");var oldSink=sinkUnit.position;
        var row=units.Where(t=>Mathf.Abs(Mathf.DeltaAngle(t.eulerAngles.y,180))<1).OrderByDescending(t=>t.GetComponent<Renderer>().bounds.max.x).ToArray();
        for(int i=1;i<row.Length;i++)Move(row[i],Vector3.right*(row[i-1].GetComponent<Renderer>().bounds.min.x-row[i].GetComponent<Renderer>().bounds.max.x));
        var corner=set.Find("CabinetBCorner01");float edge=corner.GetComponent<Renderer>().bounds.min.z;
        foreach(var side in units.Where(t=>t!=corner&&Mathf.Abs(Mathf.DeltaAngle(t.eulerAngles.y,90))<1).OrderByDescending(t=>t.GetComponent<Renderer>().bounds.max.z))
        {Move(side,new Vector3(corner.position.x-side.position.x,0,edge-side.GetComponent<Renderer>().bounds.max.z));edge=side.GetComponent<Renderer>().bounds.min.z;}
        foreach(var unit in units)
        {
            Ground(unit,1.2f);
            foreach(Transform front in unit)if(front.name.Contains("Door")||front.name.Contains("Drawer"))Move(front,-unit.up*.006f);
        }
        Move(sink,sinkUnit.position-oldSink);
        Move(sink,Vector3.up*(sinkUnit.GetComponent<Renderer>().bounds.max.y-sink.GetComponent<Renderer>().bounds.max.y));

        var furniture=house.Find("Furniture");
        foreach(Transform t in furniture)if(t.name!="Sink01"&&t.name!="TV")Ground(t,1.2f);
        var sofa=furniture.Find("Sofa01");Undo.RecordObject(sofa,"Straighten sofa");sofa.rotation=Quaternion.Euler(270,180,0);Ground(sofa,1.2f);
        var coffee=furniture.Find("Table35");Undo.RecordObject(coffee,"Straighten coffee table");coffee.rotation=Quaternion.Euler(270,0,0);Ground(coffee,1.2f);
        Move(coffee,Vector3.right*(BoundsOf(sofa).center.x-BoundsOf(coffee).center.x));
        var console=furniture.Find("Table38");Move(console,Vector3.right*(BoundsOf(sofa).center.x-BoundsOf(console).center.x));
        var tv=furniture.Find("TV");Undo.RecordObject(tv,"Stand TV on its console");tv.rotation=Quaternion.Euler(0,180,0);
        var tvBounds=BoundsOf(tv);var consoleBounds=BoundsOf(console);
        Move(tv,new Vector3(consoleBounds.center.x-tvBounds.center.x,consoleBounds.max.y-tvBounds.min.y,consoleBounds.center.z-tvBounds.center.z));
        ConvertFurnitureMaterials(furniture);

        // Move the duplicate upstairs fixture into the unlit room, preserving its separate stairwell light.
        var fixtures=house.Find("Light").Cast<Transform>().Where(t=>t.GetComponent<Renderer>()!=null&&t.GetComponent<Renderer>().bounds.center.y>6).ToArray();
        foreach(var fixture in fixtures)
        {
            var extra=fixture.GetComponentsInChildren<Light>().Skip(1).Select(l=>(light:l,position:l.transform.position)).ToArray();
            Move(fixture,Vector3.up*(6.925f-fixture.GetComponent<Renderer>().bounds.max.y));
            if(fixture.name=="PFB_LightCover (13)")Move(fixture,new Vector3(13.3f-fixture.position.x,0,7.1f-fixture.position.z));
            foreach(var item in extra){Undo.RecordObject(item.light.transform,"Preserve stair light position");item.light.transform.position=item.position;}
        }
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);
        ArrangeLiving(house);
        AlignLowerFixtures(house);
        if(!EditorSceneManager.SaveScene(scene))throw new IOException("Scene save failed.");
        CaptureAll();Validate();Selection.activeGameObject=house.gameObject;
    }
    static void ArrangeLiving(Transform house)
    {
        var furniture=house.Find("Furniture");
        foreach(var item in new[]{(name:"Sofa01",z:8.9f,rotation:Quaternion.Euler(270,180,0)),(name:"Table35",z:10.017f,rotation:Quaternion.Euler(270,0,0)),(name:"Table38",z:11.116f,rotation:Quaternion.Euler(270,0,0))})
        {var t=furniture.Find(item.name);Undo.RecordObject(t,"Preserve living room furniture centers");t.rotation=item.rotation;var b=BoundsOf(t);Move(t,new Vector3(17.64f-b.center.x,1.2f-b.min.y,item.z-b.center.z));}
        var tv=furniture.Find("TV");Undo.RecordObject(tv,"Align upright TV");tv.rotation=Quaternion.Euler(0,180,0);
        var tvBounds=BoundsOf(tv);var console=BoundsOf(furniture.Find("Table38"));
        Move(tv,new Vector3(console.center.x-tvBounds.center.x,console.max.y-tvBounds.min.y,console.center.z-tvBounds.center.z));
    }
    static void CreateFirstFloorLining(Transform house,Material plaster)
    {
        if(house.Find("InteriorCeilingLining")!=null)return;
        // Disjoint room rectangles follow the imported footprint; the stairwell is intentionally open.
        float x0=house.Find("Plane.006").GetComponent<Renderer>().bounds.min.x;
        float x1=house.Find("Plane.006").GetComponent<Renderer>().bounds.max.x;
        float x2=house.Find("Plane.010").GetComponent<Renderer>().bounds.max.x;
        float z0=house.Find("Plane.006").GetComponent<Renderer>().bounds.min.z;
        float z1=house.Find("Plane.006").GetComponent<Renderer>().bounds.max.z;
        var backRight=house.Find("Plane.011").GetComponent<Renderer>().bounds;
        var backLeft=house.Find("Plane.013").GetComponent<Renderer>().bounds;
        var hall=house.Find("Plane.012").GetComponent<Renderer>().bounds;
        var living=house.Find("Plane.007").GetComponent<Renderer>().bounds;
        var rectangles=new[]{new Rect(x0,z0,x1-x0,z1-z0),new Rect(x1,living.min.z,x2-x1,backRight.min.z-living.min.z),new Rect(hall.min.x,z1,x1-hall.min.x,backRight.min.z-z1),new Rect(hall.min.x,backRight.min.z,backRight.min.x-hall.min.x,backLeft.min.z-backRight.min.z),new Rect(backRight.min.x,backRight.min.z,backRight.size.x,backRight.size.z),new Rect(backLeft.min.x,backLeft.min.z,backLeft.size.x,backLeft.size.z)};
        var vertices=new System.Collections.Generic.List<Vector3>();var triangles=new System.Collections.Generic.List<int>();var uv=new System.Collections.Generic.List<Vector2>();
        foreach(var rect in rectangles)
        {int n=vertices.Count;foreach(var p in new[]{new Vector3(rect.xMin,3.945f,rect.yMin),new Vector3(rect.xMax,3.945f,rect.yMin),new Vector3(rect.xMax,3.945f,rect.yMax),new Vector3(rect.xMin,3.945f,rect.yMax)}){vertices.Add(house.InverseTransformPoint(p));uv.Add(new Vector2(p.x*.5f,p.z*.5f));}triangles.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});}
        var mesh=new Mesh{name="New House First Floor Ceiling"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.SetUVs(0,uv);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh,MeshFolder+"/FirstFloorCeiling.asset");
        var go=new GameObject("InteriorCeilingLining");Undo.RegisterCreatedObjectUndo(go,"Close ceiling finish seams");go.transform.SetParent(house,false);
        go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=plaster;
    }
    static void Refine()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Requires Edit mode.");
        var house=House();
        for(int i=6;i<=14;i++)
        {
            var t=house.Find("Plane."+i.ToString("000"));var filter=t.GetComponent<MeshFilter>();var r=t.GetComponent<Renderer>();
            Undo.RecordObject(filter,"Retain imported floor geometry");filter.sharedMesh=PrefabUtility.GetCorrespondingObjectFromSource(filter).sharedMesh;
            Undo.RecordObject(r,"Retain upstairs floor finishes");r.sharedMaterials=PrefabUtility.GetCorrespondingObjectFromSource(r).sharedMaterials;
        }
        var plaster=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/CeilingPlaster.mat");CreateFirstFloorLining(house,plaster);
        AssetDatabase.SaveAssets();ArrangeLiving(house);AlignLowerFixtures(house);
        var tvMaterial=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/TV_Lamp_Table_MAT_3D.mat");
        ConfigureMaterial(AssetDatabase.LoadAssetAtPath<Material>("Assets/FurnishedCabin/Meshes/Materials/TV_Lamp_Table_MAT.mat"),tvMaterial);
        AssetDatabase.SaveAssetIfDirty(tvMaterial);
        var scene=SceneManager.GetActiveScene();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        for(int i=6;i<=14;i++)AssetDatabase.DeleteAsset(MeshFolder+"/Plane_"+i.ToString("000")+"_Ceiling.asset");
        CaptureAll();Validate();
    }
    static void ConvertFurnitureMaterials(Transform furniture)
    {
        var copies=new System.Collections.Generic.Dictionary<Material,Material>();
        foreach(var r in furniture.GetComponentsInChildren<Renderer>(true))
        {
            var materials=r.sharedMaterials;bool changed=false;
            for(int i=0;i<materials.Length;i++)
            {
                var source=materials[i];if(source==null||!source.shader.name.Contains("/2D/"))continue;
                if(!copies.TryGetValue(source,out var copy))
                {
                    copy=new Material(source){shader=Shader.Find("Universal Render Pipeline/Lit"),name=source.name+" New House 3D"};
                    ConfigureMaterial(source,copy);
                    AssetDatabase.CreateAsset(copy,Folder+"/"+source.name+"_3D.mat");copies[source]=copy;
                }
                materials[i]=copy;changed=true;
            }
            if(changed){Undo.RecordObject(r,"Use 3D furniture material");r.sharedMaterials=materials;}
        }
    }
    static void ConfigureMaterial(Material source,Material copy)
    {
        Texture baseMap=source.HasProperty("_BaseMap")?source.GetTexture("_BaseMap"):null;
        if(baseMap==null&&source.HasProperty("_MainTex"))baseMap=source.GetTexture("_MainTex");
        copy.SetTexture("_BaseMap",baseMap);copy.SetFloat("_Smoothness",.25f);copy.SetFloat("_Surface",0);copy.SetFloat("_ZWrite",1);
        copy.SetFloat("_BumpScale",.35f);if(copy.GetTexture("_BumpMap")!=null)copy.EnableKeyword("_NORMALMAP");
        if(!source.IsKeywordEnabled("_EMISSION")){copy.DisableKeyword("_EMISSION");copy.SetColor("_EmissionColor",Color.black);}
        EditorUtility.SetDirty(copy);
    }
    static void AlignLowerFixtures(Transform house)
    {
        foreach(Transform fixture in house.Find("Light"))
        {var r=fixture.GetComponent<Renderer>();if(r==null||r.bounds.center.y<3.5f||r.bounds.center.y>4.3f)continue;Move(fixture,Vector3.up*(3.94f-r.bounds.max.y));}
    }
    static void Validate()
    {
        var s=new StringBuilder();var house=House();int failed=0;
        Action<bool,string> check=(ok,label)=>{s.AppendLine((ok?"PASS ":"FAIL ")+label);if(!ok)failed++;};
        var seven=house.Find("Plane.007").GetComponent<Renderer>().bounds;
        var eight=house.Find("Plane.008").GetComponent<Renderer>().bounds;
        var nine=house.Find("Plane.009").GetComponent<Renderer>().bounds;
        var ten=house.Find("Plane.010").GetComponent<Renderer>().bounds;
        check(Mathf.Abs(seven.max.z-eight.min.z)<.001f,"upper floor 007/008 join");
        check(Mathf.Abs(eight.max.z-nine.min.z)<.001f,"upper floor 008/009 join");
        check(Mathf.Abs(nine.max.x-ten.min.x)<.001f,"upper floor 009/010 join");
        check(Mathf.Abs(seven.min.x-eight.min.x)<.001f&&Mathf.Abs(seven.max.x-eight.max.x)<.001f,"floor panels aligned to room width");
        var tv=house.Find("Furniture/TV");var console=house.Find("Furniture/Table38");
        check(Vector3.Dot(tv.up,Vector3.up)>.999f,"TV upright");check(Mathf.Abs(BoundsOf(tv).min.y-BoundsOf(console).max.y)<.001f,"TV rests on console");
        check(Mathf.Abs(BoundsOf(tv).center.x-17.64f)<.001f&&Mathf.Abs(BoundsOf(tv).center.z-11.116f)<.001f,"TV remains in living room");
        foreach(var name in new[]{"Sofa01","Table35","Table38"})check(Mathf.Abs(BoundsOf(house.Find("Furniture/"+name)).center.x-17.64f)<.001f,name+" aligned in living room");
        foreach(Transform t in house.Find("Furniture"))if(t.name!="Sink01"&&t.name!="TV")check(Mathf.Abs(BoundsOf(t).min.y-1.2f)<.001f,t.name+" grounded");
        var lights=house.GetComponentsInChildren<Light>().Where(l=>l.enabled&&l.gameObject.activeInHierarchy).ToArray();
        check(!lights.SelectMany((l,i)=>lights.Skip(i+1).Select(other=>Vector3.Distance(l.transform.position,other.transform.position))).Any(d=>d<.01f),"no coincident active lights");
        foreach(Transform fixture in house.Find("Light"))
        {var r=fixture.GetComponent<Renderer>();if(r!=null&&r.bounds.center.y>3.5f&&r.bounds.center.y<4.3f)check(Mathf.Abs(r.bounds.max.y-3.94f)<.001f,fixture.name+" below first-floor ceiling");}
        check(tv.GetComponent<Renderer>().sharedMaterial.GetTexture("_BaseMap")!=null,"TV albedo texture retained");
        s.AppendLine($"failures={failed} playing={EditorApplication.isPlaying} compilationFailed={EditorUtility.scriptCompilationFailed} dirty={SceneManager.GetActiveScene().isDirty}");
        File.WriteAllText("Temp/new-house-rendering-validation.txt",s.ToString());
    }
    static void Capture(string name,Vector3 from,Vector3 target)
    {
        var go=new GameObject("New house diagnostic camera"){hideFlags=HideFlags.HideAndDontSave};
        var c=go.AddComponent<Camera>();c.transform.position=from;c.transform.LookAt(target);c.fieldOfView=75;c.nearClipPlane=.1f;c.farClipPlane=100;
        var rt=RenderTexture.GetTemporary(1200,900,24,RenderTextureFormat.Default,RenderTextureReadWrite.Default,4);
        var resolved=RenderTexture.GetTemporary(1200,900,0);var previous=RenderTexture.active;
        try{c.targetTexture=rt;c.Render();Graphics.Blit(rt,resolved);RenderTexture.active=resolved;var tex=new Texture2D(1200,900,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1200,900),0,0);tex.Apply();File.WriteAllBytes("Temp/new-house-"+name+".png",tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);}
        finally{c.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);RenderTexture.ReleaseTemporary(resolved);UnityEngine.Object.DestroyImmediate(go);}
    }
}




