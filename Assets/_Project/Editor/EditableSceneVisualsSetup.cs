#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Materialize procedural scene visuals without starting gameplay or recording session events.</summary>
[InitializeOnLoad]
public static class EditableSceneVisualsSetup
{
    const string Output="docs/evidence/editable-scene-visuals-2026-10-20";
    const string AssetFolder="Assets/_Project/Art/EditableSceneVisuals";
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    static double nextPoll;
    static EditableSceneVisualsSetup(){EditorApplication.update+=Poll;}
    public static void RunBatch()
    {
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/Scenarios/CSI_Environment.unity",OpenSceneMode.Single);
        Materialize();
        EditorApplication.Exit(File.ReadAllText(Output+"/materialization.txt").Contains("ERROR ")?1:0);
    }
    static void Poll()
    {
        const string request="Temp/editable-visuals-request.txt";
        if(EditorApplication.timeSinceStartup<nextPoll||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
        nextPoll=EditorApplication.timeSinceStartup+1;
        if(!File.Exists(request))return;
        File.Delete(request);
        try{Materialize();}catch(Exception e){Directory.CreateDirectory(Output);File.WriteAllText(Output+"/setup-error.txt",e.ToString());Debug.LogException(e);}
    }
    [MenuItem("Tools/ViCSI/Scene Setup/Make Runtime Visuals Editable")]
    public static void Materialize()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||scene.path!="Assets/_Project/Scenes/Scenarios/CSI_Environment.unity")
            throw new InvalidOperationException("Open CSI_Environment outside Play mode first.");
        Directory.CreateDirectory(Output);Directory.CreateDirectory(AssetFolder);AssetDatabase.Refresh();
        if(!File.Exists(Output+"/CSI_Environment-before.unity"))
            if(!EditorSceneManager.SaveScene(scene,Output+"/CSI_Environment-before.unity",true))throw new IOException("Scene backup failed.");
        Undo.IncrementCurrentGroup();Undo.SetCurrentGroupName("Materialize editable scene visuals");
        var cordon=UnityEngine.Object.FindAnyObjectByType<SceneCordon>();
        if(cordon==null)throw new InvalidOperationException("Scene cordon missing.");
        var ppe=UnityEngine.Object.FindAnyObjectByType<ArrivalPPEStation>(FindObjectsInactive.Include);
        if(ppe==null)
        {
            var prefab=Resources.Load<GameObject>("ArrivalPPEStation");
            var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);go.name="Arrival PPE station";
            Undo.RegisterCreatedObjectUndo(go,"Author supply station");ppe=go.GetComponent<ArrivalPPEStation>();
            var anchor=cordon.transform.Find("Front yard supply table anchor");
            go.transform.SetPositionAndRotation(anchor.position,anchor.rotation);
        }
        var prep=ppe.GetComponentInChildren<SamplePreparationStation>(true);
        if(prep==null)
        {
            var go=Create("Protected sample preparation station",scene);go.transform.SetParent(ppe.transform,false);go.transform.localPosition=new Vector3(1.55f,0,0);
            prep=go.AddComponent<SamplePreparationStation>();
        }
        if(ppe.GetComponent<GloveStationGuide>()==null)Undo.AddComponent<GloveStationGuide>(ppe.gameObject);
        var guide=ppe.GetComponent<GloveStationGuide>();
        // Visual authoring must retain the procedure's shared destination guide.
        guide.enabled=true;
        var routeField=typeof(GloveStationGuide).GetField("authoredRoute",Flags);
        var authoredRoute=(GloveStationGuide.RouteData)routeField.GetValue(guide);
        if(authoredRoute==null||authoredRoute.waypoints==null||authoredRoute.waypoints.Length<2)
        {
            routeField.SetValue(guide,JsonUtility.FromJson<GloveStationGuide.RouteData>(Resources.Load<TextAsset>("GloveStationRoute_CSI").text));
            EditorUtility.SetDirty(guide);
        }
        var register=UnityEngine.Object.FindAnyObjectByType<SceneAccessRegisterUI>(FindObjectsInactive.Include);
        if(register==null)
        {
            var go=Create("Scene access register",scene);register=go.AddComponent<SceneAccessRegisterUI>();
            var anchor=cordon.RegisterStationAnchor;
            go.transform.SetPositionAndRotation(anchor.position,anchor.rotation);
        }
        Set(register,"useAuthoredPlacement",true);Set(cordon,"authoredPPEStation",ppe);Set(cordon,"authoredRegister",register);
        if(cordon.GetComponent<SceneAccessRegister>()==null)Undo.AddComponent<SceneAccessRegister>(cordon.gameObject);
        if(UnityEngine.Object.FindAnyObjectByType<FingerprintComparisonStation>(FindObjectsInactive.Include)==null)
        {
            var desk=Create("Fingerprint Comparison - Training Station",scene);var anchor=cordon.FingerprintStationAnchor;
            desk.transform.SetPositionAndRotation(anchor.position,anchor.rotation);desk.AddComponent<FingerprintComparisonStation>();
        }
        foreach(var box in UnityEngine.Object.FindObjectsByType<EvidenceBoxDepositZone>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            if(box.GetComponent<EvidenceBoxContentsDisplay>()==null)Undo.AddComponent<EvidenceBoxContentsDisplay>(box.gameObject);
            if(box.GetComponent<EvidenceBoxWaypoint>()==null)Undo.AddComponent<EvidenceBoxWaypoint>(box.gameObject);
        }
        foreach(var npc in UnityEngine.Object.FindObjectsByType<NPCPresentation>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            if(npc.CharacterRole==NPCPresentation.Role.FirstResponder||npc.CharacterRole==NPCPresentation.Role.IOC)
                if(npc.GetComponent<NPCApproachGuide>()==null)Undo.AddComponent<NPCApproachGuide>(npc.gameObject);
        foreach(var assessment in UnityEngine.Object.FindObjectsByType<IOCAssessmentInteraction>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            if(assessment.GetComponent<IOCAssessmentGuide>()==null)Undo.AddComponent<IOCAssessmentGuide>(assessment.gameObject);
        foreach(var photos in UnityEngine.Object.FindObjectsByType<ScenePhotographyManager>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            if(photos.GetComponent<ScenePhotographyGuide>()==null)Undo.AddComponent<ScenePhotographyGuide>(photos.gameObject);
        foreach(var witness in UnityEngine.Object.FindObjectsByType<VendorWitnessInteraction>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            if(witness.GetComponent<VendorPathGuide>()==null)Undo.AddComponent<VendorPathGuide>(witness.gameObject);
        foreach(var tool in UnityEngine.Object.FindObjectsByType<PhotographTool>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            if(tool.GetComponent<PhotographicScaleAccessory>()==null)Undo.AddComponent<PhotographicScaleAccessory>(tool.gameObject);
        var camera=UnityEngine.Object.FindAnyObjectByType<PhotographicScaleAccessory>(FindObjectsInactive.Include);
        if(camera!=null)
        {
            camera.GetType().GetField("tool",Flags).SetValue(camera,camera.GetComponent<PhotographTool>());
            var ruler=(GameObject)camera.GetType().GetField("authoredRuler",Flags).GetValue(camera);
            if(ruler==null)
            {
                ruler=Create("Assisted 30 cm photographic scale - editable",scene);
                var graphic=ruler.AddComponent<PhotographicScaleGraphic>();Invoke(graphic,"Awake");
                ruler.transform.SetPositionAndRotation(new Vector3(18.3f,1.215f,12.8f),Quaternion.Euler(90,0,0));
                Set(camera,"authoredRuler",ruler);ruler.SetActive(false);
            }
        }
        BuildPaperTemplate(scene);
        var report=new List<string>();
        foreach(var owner in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include,FindObjectsSortMode.None)
            .Where(c=>c!=null&&c.gameObject.scene==scene).OrderBy(c=>c.GetType().Name).ToArray())
        {
            var order=new[]{"BuildRuntimeUI","BuildVisuals","BuildDesk","BuildShell","BuildUI","BuildPanel","BuildInteractionVisuals","BuildPresentation","BuildSettings","Awake"};
            var builders=owner.GetType().GetMethods(Flags).Where(m=>m.GetCustomAttribute<EditableSceneBuilderAttribute>()!=null)
                .OrderBy(m=>Array.IndexOf(order,m.Name)<0?99:Array.IndexOf(order,m.Name)).ThenBy(m=>m.MetadataToken).ToArray();
            if(builders.Length==0)continue;
            if(owner.GetComponent<EditableSceneVisuals>()==null)Undo.AddComponent<EditableSceneVisuals>(owner.gameObject);
            foreach(var builder in builders)
            {
                bool wasEnabled=owner.enabled;
                try{builder.Invoke(owner,null);report.Add("OK "+owner.name+" / "+owner.GetType().Name+"."+builder.Name);}
                catch(Exception e){report.Add("ERROR "+owner.name+" / "+builder.Name+": "+(e.InnerException??e));}
                finally{owner.enabled=wasEnabled;}
            }
        }
        foreach(var settings in UnityEngine.Object.FindObjectsByType<SettingsPauseMenu>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            var panel=(GameObject)typeof(SettingsPauseMenu).GetField("panel",Flags).GetValue(settings);
            if(panel==null)continue;
            bool active=panel.activeSelf;panel.SetActive(true);
            var confirmation=typeof(SettingsPauseMenu).GetMethod("ShowConfirmation",Flags);
            confirmation.Invoke(settings,new object[]{"restart","Restart Scenario?","Your current investigation progress will be reset.","Restart"});
            confirmation.Invoke(settings,new object[]{"lobby","Return to Main Lobby?","Your current investigation session will end.","Return"});
            Invoke(settings,"BuildSettings");panel.SetActive(active);
            report.Add("OK Settings restart and lobby confirmation templates");
        }
        PersistAssets(scene);
        foreach(var storage in UnityEngine.Object.FindObjectsByType<EditableSceneVisuals>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            foreach(var group in storage.Groups)
                report.Add("GROUP "+group.owner.GetType().Name+"."+group.builder+" = "+group.objects.Count+" scene objects");
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);
        if(!EditorSceneManager.SaveScene(scene))throw new IOException("Could not save editable scene objects.");
        File.WriteAllLines(Output+"/materialization.txt",report);
        Debug.Log("Editable visual authoring finished; see "+Output+"/materialization.txt");
        Selection.activeGameObject=ppe.gameObject;
    }
    static GameObject Create(string name,UnityEngine.SceneManagement.Scene scene)
    {var go=new GameObject(name);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,scene);Undo.RegisterCreatedObjectUndo(go,"Author "+name);return go;}
    static void Set(UnityEngine.Object owner,string name,UnityEngine.Object value)
    {var data=new SerializedObject(owner);data.FindProperty(name).objectReferenceValue=value;data.ApplyModifiedProperties();}
    static void Set(UnityEngine.Object owner,string name,bool value)
    {var data=new SerializedObject(owner);data.FindProperty(name).boolValue=value;data.ApplyModifiedProperties();}
    static void Invoke(MonoBehaviour owner,string method)=>owner.GetType().GetMethod(method,Flags).Invoke(owner,null);
    static void BuildPaperTemplate(UnityEngine.SceneManagement.Scene scene)
    {
        const string path="Assets/Resources/EditablePaperSwabPackage.prefab";
        if(AssetDatabase.LoadAssetAtPath<GameObject>(path)!=null)return;
        var carrier=Create("Paper sample package authoring",scene);
        try
        {
            PaperSwabPackage.Apply(carrier.transform,"EVD-015");
            PersistAssets(scene);
            var sleeve=carrier.transform.Find("Individual paper swab package").gameObject;
            sleeve.GetComponentInChildren<Text>().text="BLOOD EVIDENCE\n\nEvidence source\n\nDRIED\nPAPER PACKAGE";
            PrefabUtility.SaveAsPrefabAsset(sleeve,path);
        }
        finally{UnityEngine.Object.DestroyImmediate(carrier);}
    }
    static void PersistAssets(UnityEngine.SceneManagement.Scene scene)
    {
        var objects=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
        var saved=new Dictionary<UnityEngine.Object,string>();
        void Save(UnityEngine.Object value,string suffix)
        {
            if(value==null||AssetDatabase.Contains(value)||saved.ContainsKey(value))return;
            value.hideFlags=HideFlags.None;
            if(value is Material material)
                foreach(var property in material.GetTexturePropertyNames())Save(material.GetTexture(property),".asset");
            if(value is Sprite sprite)Save(sprite.texture,".asset");
            // Reserve the parent path only after dependencies exist. An unnamed sprite
            // and its unnamed texture previously chose the same path, losing the texture.
            var safe=string.Concat(value.name.Select(c=>char.IsLetterOrDigit(c)?c:'_'));
            if(string.IsNullOrEmpty(safe))safe=value.GetType().Name;
            var path=AssetDatabase.GenerateUniqueAssetPath(AssetFolder+"/"+safe+suffix);
            AssetDatabase.CreateAsset(value,path);saved[value]=path;
        }
        foreach(var t in objects)
        {
            var filter=t.GetComponent<MeshFilter>();if(filter!=null)Save(filter.sharedMesh,".asset");
            foreach(var renderer in t.GetComponents<Renderer>())foreach(var material in renderer.sharedMaterials)Save(material,".mat");
            foreach(var graphic in t.GetComponents<Graphic>())Save(graphic.material,".mat");
            foreach(var image in t.GetComponents<Image>())Save(image.sprite,".asset");
        }
    }
}
#endif
