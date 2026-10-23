using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>One-time, undoable scene migration. Stable evidence, reference and narrative IDs are retained.</summary>
public static class NewHouseInvestigationSetup
{
    public const string Evidence = "docs/evidence/new-house-investigation";
    const string ScenePath = "Assets/_Project/Scenes/Scenarios/CSI_Environment.unity";
    public static readonly Vector3 BodyPosition = new Vector3(17.35f, 1.326f, 12.9f);
    public static readonly Vector3 Arrival = new Vector3(16.9f, -.15f, .7f);
    static Transform Find(string name) => SceneManager.GetActiveScene().GetRootGameObjects().Single(g => g.name == name).transform;

    [MenuItem("ViCSI/Scene Setup/Move Investigation Into New House")]
    public static void Apply()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != ScenePath)
            throw new InvalidOperationException("Open CSI_Environment outside Play mode.");
        var house = Find("Vicsi_House");
        if (house.Find("InvestigationLayout") != null) throw new InvalidOperationException("Investigation migration already applied.");
        Directory.CreateDirectory(Evidence);
        Directory.CreateDirectory(Evidence+"/before");
        if (!EditorSceneManager.SaveScene(scene, Evidence+"/before/CSI_Environment.unity", true)) throw new IOException("Snapshot failed.");
        foreach (string path in new[] { "Assets/_Project/Data/Measurement/Measurements_CSI_Environment.asset", "Assets/_Project/Data/Sketch/Sketch_CSI_Environment.asset", "Assets/_Project/Data/Scenarios/IOC_Assessment_CSI_Environment.asset", "Assets/Resources/ViCSIDialogueCatalog.asset", "Assets/_Project/Data/Evidence/EVD-018_Emptyliquorbottle.asset", "Assets/_Project/Data/Evidence/BODY-A_Victimsbody.asset", "Assets/_Project/Data/DialoguePools/DialoguePool_Evidence018Logged.asset", "Assets/_Project/Data/DialoguePools/DialoguePool_Evidence018Photographed.asset" })
            File.Copy(path, Evidence+"/before/migration-original-" + Path.GetFileName(path), true);
        Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Move investigation into new house");

        // Preserve the body/knife relationship and their attached procedure geometry;
        // the floor stain sits visibly beside the body in the new kitchen.
        var body = Find("BODY_100K");
        Vector3 delta = BodyPosition - body.position;
        Move(body, BodyPosition);
        Move(Find("Evidence_KitchenKnife"), Find("Evidence_KitchenKnife").position + delta);
        Move(Find("Evidence_Bloodspatterpattern"), new Vector3(18.32f,1.204f,13.12f));
        var bottle = Find("Evidence_Emptyliquorbottle");
        Move(bottle, new Vector3(17.75f, 2.260f, 14.3f));

        Move(Find("[BuildingBlock] Camera Rig"), Arrival);
        Move(Find("Teleport Hotspot"), new Vector3(Arrival.x, 0, Arrival.z));
        Move(Find("NPC_FirstResponder"), new Vector3(18.15f, .03f, .8f));
        Face(Find("NPC_FirstResponder"), Arrival);
        Move(Find("NPC_Vendor_PLACEHOLDER"), new Vector3(11.35f, .031f, 1f));
        Face(Find("NPC_Vendor_PLACEHOLDER"), new Vector3(14, 0, 2));
        Move(Find("NPC_IOC"), new Vector3(19.5f, .031f, 1.1f));
        Face(Find("NPC_IOC"), Arrival);
        Move(Find("IOC_Start_Ground_Circle"), new Vector3(19.5f, 0, 1.1f));
        Move(Find("EvidenceHoldingCrate"), new Vector3(18.55f, .55f, 2f));

        var observe = Find("IOC_Observe_Point");
        Move(observe, new Vector3(18.55f, 1.2f, 12.05f));
        Face(observe, BodyPosition);
        var highlight = observe.Find("IOC_Observe_Standing_Highlight");
        Undo.RecordObject(highlight, "Ground kitchen observation highlight");
        highlight.localPosition = new Vector3(0, .008f, 0);

        ConfigureCordon();
        ConfigurePhotography();
        ConfigureMeasurements();
        ConfigureAssessment();
        UpdateNarrative();
        NewHouseSketchSetup.Rebuild();

        // The complete old environment is retained, recoverable, without old lights/collisions.
        Undo.RecordObject(Find("Harong").gameObject, "Archive old house"); Find("Harong").gameObject.SetActive(false);
        var marker = new GameObject("InvestigationLayout"); Undo.RegisterCreatedObjectUndo(marker, "Mark migrated layout"); marker.transform.SetParent(house, false);
        Undo.CollapseUndoOperations(group);
        AssetDatabase.SaveAssets(); Physics.SyncTransforms();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Scene save failed.");
    }

    public static void RefineLayout()
    {
        SynchronizeMovedEvidence();
    }

    [MenuItem("ViCSI/Scene Setup/Synchronize Moved Kitchen Evidence")]
    public static void SynchronizeMovedEvidence()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != ScenePath)
            throw new InvalidOperationException("Open CSI_Environment outside Play mode.");
        Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Synchronize moved kitchen evidence");
        Physics.SyncTransforms();
        MeasurementGeometry.TryGetEvidenceAnchor("EVD-014", out var knife);
        MeasurementGeometry.TryGetEvidenceAnchor("EVD-015", out var spatter);
        MeasurementGeometry.TryGetEvidenceAnchor("BODY-A", out var body);
        MeasurementGeometry.TryGetEvidenceAnchor("EVD-018", out var bottle);
        // Each hook lies directly across from its own evidence anchor in the floor plan.
        // Nearby items may share a surface patch: the selected requirement owns selection.
        Surface("REF-BODYA-1", "East Kitchen Wall", new Vector3(body.x,1.95f,body.z), Vector3.right, "walls", .8f);
        Surface("REF-BODYA-2", "Foot-side Kitchen Wall", new Vector3(body.x,1.95f,body.z), Vector3.back, "walls", .8f);
        Surface("REF-KNIFE-1", "East Kitchen Wall", new Vector3(knife.x,1.95f,knife.z), Vector3.right, "walls", .8f);
        Surface("REF-KNIFE-CAB", "Rear Kitchen Wall", new Vector3(knife.x,1.95f,knife.z), Vector3.forward, "walls", .8f);
        Surface("REF-SPATTER-CAB", "Foot-side Kitchen Wall", new Vector3(spatter.x,1.95f,spatter.z), Vector3.back, "walls", .8f);
        Surface("REF-SPATTER-2", "West Kitchen Wall", new Vector3(spatter.x,1.95f,spatter.z), Vector3.left, "walls", .8f);
        Surface("REF-BOTTLE-1", "Rear Kitchen Wall", new Vector3(bottle.x,2.25f,bottle.z), Vector3.forward, "walls", .8f);
        // Sample the cabinet's exposed outer end from the room, not an internal door seam.
        Surface("REF-BOTTLE-2", "Right Cabinet End", new Vector3(18.8f,1.8f,bottle.z), Vector3.left, "Cabinet", .8f);
        AlignBottleReferenceHeight();
        foreach (var reference in UnityEngine.Object.FindObjectsByType<SceneReferencePoint>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            Undo.RecordObject(reference.Zone.Box,"Enlarge tape hook receiver");
            reference.Zone.Box.size = new Vector3(.8f,.9f,.4f);
            Undo.RecordObject(reference.Zone.transform,"Expose tape hook receiver into room");
            reference.Zone.transform.position = reference.AnchorPosition + reference.SurfaceNormal(false) * .18f;
        }
        var manager = UnityEngine.Object.FindAnyObjectByType<MeasurementStateManager>();
        var serialized = new SerializedObject(manager);
        serialized.FindProperty("measurementLayoutId").stringValue = "new-house-v3";
        serialized.ApplyModifiedProperties();
        RecalculateMeasurementExpectations();
        NewHouseSketchSetup.SynchronizeEvidence();
        // The standing cue is a child of the user's observation point; retain its yaw.
        var highlight = Find("IOC_Observe_Point").Find("IOC_Observe_Standing_Highlight");
        Undo.RecordObject(highlight,"Ground observation cue");
        highlight.localPosition = new Vector3(0,.008f,0);
        Undo.CollapseUndoOperations(group);
        AssetDatabase.SaveAssets(); Physics.SyncTransforms();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Scene save failed.");
    }

    [MenuItem("ViCSI/Scene Setup/Align Bottle Reference Height")]
    public static void AlignBottleReferenceHeight()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != ScenePath)
            throw new InvalidOperationException("Open CSI_Environment outside Play mode.");
        if (!MeasurementGeometry.TryGetEvidenceAnchor("EVD-018", out var bottle))
            throw new InvalidOperationException("Missing bottle measurement anchor.");
        foreach (var id in new[] { "REF-BOTTLE-1", "REF-BOTTLE-2" })
        {
            var reference = Ref(id);
            var delta = Vector3.up * (bottle.y - reference.AnchorPosition.y);
            Move(reference.transform, reference.transform.position + delta);
            // Normally these are children and move with the reference. Preserve support
            // for separately assigned anchors, receivers and markers as well.
            foreach (var target in new[] { reference.anchor, reference.Zone.transform,
                reference.debugMarker != null ? reference.debugMarker.transform : null })
                if (target != null && target != reference.transform && !target.IsChildOf(reference.transform))
                    Move(target, target.position + delta);
        }
        Physics.SyncTransforms();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Scene save failed.");
    }

    static void Surface(string id, string label, Vector3 from, Vector3 direction, string expectedCollider, float span)
    {
        if (!Physics.Raycast(from,direction,out var hit,5f,~0,QueryTriggerInteraction.Ignore)
            || !hit.collider.name.StartsWith(expectedCollider,StringComparison.Ordinal))
            throw new InvalidOperationException("No accessible fixed surface for "+id);
        Reference(id,label,hit.point,-direction,span);
    }

    static void Move(Transform t, Vector3 position) { Undo.RecordObject(t, "Relocate investigation"); t.position = position; PrefabUtility.RecordPrefabInstancePropertyModifications(t); }
    static void Face(Transform t, Vector3 target)
    {
        Vector3 direction = target - t.position; direction.y = 0;
        Undo.RecordObject(t, "Face investigation route"); t.rotation = Quaternion.LookRotation(direction);
        PrefabUtility.RecordPrefabInstancePropertyModifications(t);
    }
    static void ConfigureCordon()
    {
        var t = Find("SceneCordon_PLACEHOLDER"); Move(t, new Vector3(16.9f, 0, 2.4f));
        var cordon = t.GetComponent<SceneCordon>(); var so = new SerializedObject(cordon);
        so.FindProperty("protectedArea").boundsValue = new Bounds(new Vector3(-1.1f, 3.5f, 6.55f), new Vector3(11.5f, 8, 13.1f)); so.ApplyModifiedProperties();
        Move(t.Find("Post_L"), new Vector3(10.1f, .5f, 2.4f)); Move(t.Find("Post_R"), new Vector3(21.5f, .5f, 2.4f));
        Move(t.Find("EntrancePost_L"), new Vector3(16.2f, .5f, 2.4f)); Move(t.Find("EntrancePost_R"), new Vector3(17.6f, .5f, 2.4f));
        foreach (var line in t.GetComponentsInChildren<LineRenderer>(true))
        {
            Undo.RecordObject(line, "Cordon new house perimeter"); line.useWorldSpace = true;
            Vector3[] points = line.transform.IsChildOf(t.Find("EntranceTape_Closed"))
                ? new[] { new Vector3(16.2f,.85f,2.4f),new Vector3(17.6f,.85f,2.4f) }
                : line.transform.IsChildOf(t.Find("EntranceTape_Open"))
                ? new[] { new Vector3(16.2f,.85f,2.4f),new Vector3(16.25f,.85f,2.5f) }
                : new[] {new Vector3(16.2f,.85f,2.4f),new Vector3(10.1f,.85f,2.4f),new Vector3(10.1f,.85f,15.5f),new Vector3(21.5f,.85f,15.5f),new Vector3(21.5f,.85f,2.4f),new Vector3(17.6f,.85f,2.4f)};
            line.positionCount = points.Length; line.SetPositions(points);
        }
    }
    static void ConfigurePhotography()
    {
        Photo("scene-exterior-establishing", new Vector3(16.9f,0,-.8f),new Vector3(16.8f,3.2f,7.5f));
        Photo("scene-entrance-approach",new Vector3(16.9f,1.2f,5.25f),new Vector3(16.7f,2.1f,6.5f));
        Photo("scene-interior-from-entrance",new Vector3(16.8f,1.2f,7f),new Vector3(17.2f,2,10.4f));
        Photo("scene-interior-left",new Vector3(15.9f,1.2f,8.7f),new Vector3(18.1f,2,9.7f));
        Photo("scene-interior-right",new Vector3(18.85f,1.2f,9.6f),new Vector3(16.4f,2,8.8f));
        Photo("scene-body-in-scene",new Vector3(18.5f,1.2f,12.1f),new Vector3(17.35f,1.45f,12.9f));
        Photo("scene-stair-incident-area",new Vector3(16.25f,1.2f,12.1f),new Vector3(17.35f,1.8f,13.5f));
    }
    static void Photo(string id, Vector3 position, Vector3 target)
    {
        var p = UnityEngine.Object.FindObjectsByType<ScenePhotoPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(x => x.photoPointId == id);
        Move(p.transform, position);
        if (p.cameraTarget == null) throw new InvalidOperationException("Missing photo target: " + id);
        Move(p.cameraTarget, target);
        if (p.zoneVisual != null) Move(p.zoneVisual.transform, position + Vector3.up * .012f);
    }
    static void Reference(string id, string label, Vector3 position, Vector3 normal, float span)
    {
        var r = UnityEngine.Object.FindObjectsByType<SceneReferencePoint>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(x => x.referenceId == id);
        Move(r.transform, position); Undo.RecordObject(r.transform,"Orient measurement surface"); r.transform.rotation = Quaternion.LookRotation(normal);
        Undo.RecordObject(r,"Relabel measurement reference"); r.displayName = label; r.description = "Fixed " + label.ToLowerInvariant() + " in the rear ground-floor kitchen."; r.shape = SceneReferencePoint.ReferenceShape.Plane;
        if (r.anchor != null) Move(r.anchor, position);
        var zone = r.Zone; Undo.RecordObject(zone.transform,"Place measurement reference zone"); zone.transform.SetPositionAndRotation(position+normal*.1f, r.transform.rotation);
        Undo.RecordObject(zone.Box,"Fit new reference surface"); zone.Box.center = Vector3.zero; zone.Box.size = new Vector3(span, .6f, .16f); zone.Box.isTrigger = true;
        if (r.debugMarker != null) Move(r.debugMarker.transform, position);
    }
    static void ConfigureMeasurements()
    {
        var manager = UnityEngine.Object.FindAnyObjectByType<MeasurementStateManager>();
        var serialized = new SerializedObject(manager); serialized.FindProperty("measurementLayoutId").stringValue="new-house-v1"; serialized.ApplyModifiedProperties();
        Reference("REF-BODYA-1","East Kitchen Wall",new Vector3(19.417f,1.95f,12.9f),Vector3.left,1.2f);
        Reference("REF-BODYA-2","Rear Cabinet Front",new Vector3(17.35f,1.8f,14.071f),Vector3.back,.9f);
        Reference("REF-KNIFE-1","West Cabinet Front",new Vector3(15.645f,1.8f,13.417f),Vector3.right,.7f);
        Reference("REF-KNIFE-CAB","Rear Cabinet Front",new Vector3(16.25f,1.8f,14.071f),Vector3.back,.6f);
        Reference("REF-SPATTER-CAB","Rear Cabinet Front",new Vector3(18.1f,1.8f,14.071f),Vector3.back,.45f);
        Reference("REF-SPATTER-2","East Kitchen Wall",new Vector3(19.417f,1.95f,13.4f),Vector3.left,.6f);
        Reference("REF-BOTTLE-1","Rear Kitchen Wall",new Vector3(17.75f,2.25f,14.632f),Vector3.back,.55f);
        Reference("REF-BOTTLE-2","Right Cabinet End",new Vector3(18.344f,1.8f,14.35f),Vector3.right,.45f);
        RecalculateMeasurementExpectations();
    }
    static void RecalculateMeasurementExpectations()
    {
        var set = AssetDatabase.LoadAssetAtPath<MeasurementRequirementSet>("Assets/_Project/Data/Measurement/Measurements_CSI_Environment.asset");
        Undo.RecordObject(set,"Update measurement geometry");
        foreach (var requirement in set.requirements)
        {
            var a = Ref(requirement.referenceAId); var b = Ref(requirement.referenceBId);
            if (!MeasurementGeometry.TryGetEvidenceAnchor(requirement.targetEvidenceId,out var point)) throw new InvalidOperationException("No evidence anchor: "+requirement.targetEvidenceId);
            requirement.expectedDistanceA = MeasurementGeometry.DistanceToReference(a,point,requirement.measureOnFloorPlan);
            requirement.expectedDistanceB = MeasurementGeometry.DistanceToReference(b,point,requirement.measureOnFloorPlan);
            requirement.instructionText = "Measure the " + (requirement.targetEvidenceId=="BODY-A"?"victim":requirement.targetEvidenceId=="EVD-014"?"knife":requirement.targetEvidenceId=="EVD-015"?"blood pattern":"bottle") + " from " + a.displayName + " and " + b.displayName + " before disturbing it.";
        }
        EditorUtility.SetDirty(set);
    }
    public static SceneReferencePoint Ref(string id) => UnityEngine.Object.FindObjectsByType<SceneReferencePoint>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(x=>x.referenceId==id);
    static void ConfigureAssessment()
    {
        var definition = AssetDatabase.LoadAssetAtPath<IOCAssessmentDefinition>("Assets/_Project/Data/Scenarios/IOC_Assessment_CSI_Environment.asset");
        Undo.RecordObject(definition,"Move IOC survey into rear kitchen"); definition.surveyArea = new Bounds(new Vector3(17.5f,2.4f,12.35f),new Vector3(4,3,2)); definition.surveyLookAt = new Vector3(17.35f,1.6f,12.9f); EditorUtility.SetDirty(definition);
        var closeout = UnityEngine.Object.FindAnyObjectByType<SOCOCloseout>(); var so = new SerializedObject(closeout);
        so.FindProperty("finalSurveyArea").boundsValue = new Bounds(new Vector3(17.5f,2.4f,12.35f),new Vector3(4,3,2));
        so.FindProperty("finalSurveyLookAt").vector3Value = definition.surveyLookAt; so.ApplyModifiedProperties();
    }
    static void UpdateNarrative()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<ViCSIDialogueCatalog>("Assets/Resources/ViCSIDialogueCatalog.asset"); Undo.RecordObject(catalog,"Match narrative to new house");
        Set("ioc-assessment-guidance","You've got the scene now. Follow the waypoint through the living area to the rear ground-floor kitchen, face John's body, and press A when Observe appears. Look at the body and possible evidence without disturbing anything before deciding what support you need.");
        Set("ioc-observation-survey","The scene remains secured and the cordon is intact. John's body appears undisturbed on the floor of the rear ground-floor kitchen.");
        Set("team-evidence-018-photographed-1","Dispatch: We've got the bottle photographed on the kitchen counter.");
        Set("team-evidence-018-logged-1","Dispatch: Got the bottle's position on the rear kitchen counter.");
        Set("photo-point-stair-incident-area","Photograph the rear kitchen to show the counters, the body and the floor around the evidence.");
        Set("photo-point-entrance-approach","Photograph the porch and front doorway before moving inside.");
        Set("photo-point-interior-left","Photograph the living area from the left side to record the furniture layout.");
        Set("photo-point-interior-right","Photograph the living area from the opposite side to complete its overall coverage.");
        Set("photo-point-body-in-scene","Enter the rear ground-floor kitchen. Photograph John's body in its surroundings.");
        EditorUtility.SetDirty(catalog);
        var bottle = AssetDatabase.LoadAssetAtPath<EvidenceDefinition>("Assets/_Project/Data/Evidence/EVD-018_Emptyliquorbottle.asset");
        Undo.RecordObject(bottle,"Relocate bottle description"); bottle.foundLocation = "On the rear ground-floor kitchen counter."; bottle.reportLineOptions.Single(x=>x.id=="bottle-location").text = "Found on the rear kitchen counter, beside the preparation area."; EditorUtility.SetDirty(bottle);
        var body = AssetDatabase.LoadAssetAtPath<EvidenceDefinition>("Assets/_Project/Data/Evidence/BODY-A_Victimsbody.asset");
        Undo.RecordObject(body,"Identify rear kitchen in body notes");body.foundLocation="On the rear ground-floor kitchen floor.";body.reportLineOptions.Single(x=>x.id=="body-a-location").text="Body observed on the rear ground-floor kitchen floor.";EditorUtility.SetDirty(body);
        foreach (var pair in new[] {new[]{"DialoguePool_Evidence018Photographed.asset","Dispatch: Bottle's on film, on the rear kitchen counter."},new[]{"DialoguePool_Evidence018Logged.asset","Dispatch: Position on the rear kitchen counter — logged."}})
        {
            string path="Assets/_Project/Data/DialoguePools/"+pair[0]; var asset=AssetDatabase.LoadMainAssetAtPath(path);var so=new SerializedObject(asset);var lines=so.FindProperty("lines");
            if(lines!=null&&lines.isArray&&lines.arraySize>0){Undo.RecordObject(asset,"Match legacy dialogue pool location");lines.GetArrayElementAtIndex(0).stringValue=pair[1];so.ApplyModifiedProperties();}
        }
        void Set(string id,string text) { var entry=catalog.entries.Single(x=>x.id==id); entry.text=text; }
    }
}
