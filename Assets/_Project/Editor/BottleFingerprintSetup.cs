using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Persist the bottle's on-scene print cluster, procedure gates and dialogue.</summary>
public static class BottleFingerprintSetup
{
    [MenuItem("ViCSI/Scene Setup/Configure Bottle Fingerprints")]
    public static void Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != "Assets/_Project/Scenes/Scenarios/CSI_Environment.unity")
            throw new InvalidOperationException("Open CSI_Environment outside Play.");
        var bottle = UnityEngine.Object.FindObjectsByType<EvidenceProp>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(p=>p.evidenceId=="EVD-018");
        var definition = AssetDatabase.LoadAssetAtPath<EvidenceDefinition>("Assets/_Project/Data/Evidence/EVD-018_Emptyliquorbottle.asset");
        Undo.RecordObject(definition,"Bottle on-scene fingerprint procedure");
        definition.fingerprintBeforeCollection = true;
        definition.measurementAfterFinalPhoto = true;
        definition.fingerprintMatchIdentities = new List<string>{"John","Peter","Paul"};
        // The scalar remains Paul's legacy match for older readers; new readers use the full list.
        definition.fingerprintMatchIdentity = "Paul";
        definition.stepRequirements.RemoveAll(s=>s.action==ProcedureAction.StatusTransition && s.beforeStatus==EvidenceStatus.PhotographedFinal);
        definition.stepRequirements.RemoveAll(s=>s.stepId.StartsWith("bottle-measure-before-fingerprint",StringComparison.Ordinal));
        definition.stepRequirements.Add(new ProcedureStepRequirement {
            stepId="bottle-measure-before-fingerprint-fixed",action=ProcedureAction.FingerprintProcessing,enforcement=ProcedureEnforcement.Block,
            messageId="bottle-measure-before-fingerprint",expectedPrerequisite="measurement:MEAS-EVD-018",
            requirement=new RequirementNode {kind=RequirementNodeKind.Leaf,requirementType=RequirementType.MeasurementCompleted,measurementRequirementId="MEAS-EVD-018"}});
        definition.stepRequirements.Add(new ProcedureStepRequirement {
            stepId="bottle-measure-before-fingerprint-pairs",action=ProcedureAction.FingerprintProcessing,enforcement=ProcedureEnforcement.Block,
            messageId="bottle-measure-before-fingerprint",expectedPrerequisite="evidence-distances:EVD-018",
            requirement=new RequirementNode {kind=RequirementNodeKind.Leaf,requirementType=RequirementType.EvidencePairMeasurementsComplete,evidenceId="EVD-018"}});
        definition.reportLineOptions.Single(o=>o.id=="bottle-first-encountered").text = "Separate fingerprints recovered from the bottle were identified as John's, Peter's and Paul's.";
        definition.reportLineOptions.Single(o=>o.id=="bottle-standard-procedure").text = "The bottle's original location and condition were documented before collection.";
        var interpretation = definition.reportLineOptions.Single(o=>o.id=="bottle-fingerprint-pending");
        interpretation.text = "The recovered prints establish handling, but do not establish when each person touched the bottle.";
        interpretation.extraRequirement = new RequirementNode {kind=RequirementNodeKind.Leaf,requirementType=RequirementType.FingerprintLabStatusAtOrAbove,evidenceId="EVD-018",requiredLabStatus=FingerprintLabStatus.Matched};
        definition.progressiveFindings[0].text = "Three separate recovered prints were identified as John's, Peter's and Paul's. Their timing cannot be established from the prints alone.";
        definition.progressiveFindings[0].revealRequirement = new RequirementNode {kind=RequirementNodeKind.Leaf,requirementType=RequirementType.FingerprintLabStatusAtOrAbove,evidenceId="EVD-018",requiredLabStatus=FingerprintLabStatus.Matched};
        EditorUtility.SetDirty(definition);

        var marker = bottle.GetComponentInChildren<EvidenceFingerprintMarker>(true);
        if (marker == null)
        {
            var source = UnityEngine.Object.FindObjectsByType<EvidenceFingerprintMarker>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(m=>m.evidenceId=="EVD-014");
            var clone = UnityEngine.Object.Instantiate(source.gameObject,bottle.transform,true);
            Undo.RegisterCreatedObjectUndo(clone,"Bottle fingerprint target");
            clone.name="Bottle_RecoveredPrints";
            marker=clone.GetComponent<EvidenceFingerprintMarker>();
            marker.evidenceId="EVD-018";
            clone.GetComponent<EvidenceProp>().evidenceId="EVD-018-print";
            var bounds=bottle.GetComponent<Renderer>().bounds;
            clone.transform.SetPositionAndRotation(new Vector3(bounds.center.x,bounds.center.y-.025f,bounds.min.z+.001f),Quaternion.identity);
            var visual=clone.transform.Find("PowderedFingerprintVisual");
            var group=new GameObject("BottlePrintCluster");group.transform.SetParent(clone.transform,false);
            var local=visual.localPosition;
            visual.SetParent(group.transform,false);visual.localPosition=local+new Vector3(-.65f,0,0);
            var middle=UnityEngine.Object.Instantiate(visual.gameObject,group.transform);middle.name="RecoveredPrint_Peter";middle.transform.localPosition=local+new Vector3(0,.3f,0);middle.SetActive(true);
            var right=UnityEngine.Object.Instantiate(visual.gameObject,group.transform);right.name="RecoveredPrint_Paul";right.transform.localPosition=local+new Vector3(.65f,-.15f,0);right.SetActive(true);
            visual.name="RecoveredPrint_John";visual.gameObject.SetActive(true);group.SetActive(false);
            var so=new SerializedObject(marker);so.FindProperty("powderedVisual").objectReferenceValue=group;so.ApplyModifiedProperties();
            // The original touch detector and backing-card references remain shared with the real kit.
        }
        // Dark powder keeps the recovered ridges legible on this light glass surface.
        // Clone the material so the knife's existing white-powder presentation is preserved.
        const string powderMaterialPath="Assets/_Project/Art/Materials/Bottle_Black_Fingerprint_Powder.mat";
        var powderMaterial=AssetDatabase.LoadAssetAtPath<Material>(powderMaterialPath);
        var printRenderers=marker.transform.Find("BottlePrintCluster").GetComponentsInChildren<Renderer>(true);
        if(powderMaterial==null)
        {
            powderMaterial=new Material(printRenderers[0].sharedMaterial){name="Bottle_Black_Fingerprint_Powder",color=new Color(.04f,.04f,.04f)};
            AssetDatabase.CreateAsset(powderMaterial,powderMaterialPath);
        }
        foreach(var renderer in printRenderers){Undo.RecordObject(renderer,"Bottle powder contrast");renderer.sharedMaterial=powderMaterial;}
        var station = UnityEngine.Object.FindAnyObjectByType<FingerprintStationTool>(FindObjectsInactive.Include);
        var stationData=new SerializedObject(station);stationData.FindProperty("restrictToEvidenceIds").ClearArray();stationData.ApplyModifiedProperties();
        Undo.RecordObject(station,"Retire bottle lab station");station.enabled=false;

        var catalog=AssetDatabase.LoadAssetAtPath<ViCSIDialogueCatalog>("Assets/Resources/ViCSIDialogueCatalog.asset");
        Undo.RecordObject(catalog,"Bottle fingerprint dialogue");
        Set("bottle-measure-before-fingerprint","Complete the bottle's fixed-reference measurements and its distances to discovered evidence before dusting for prints.",DialogueEntryType.Tooltip);
        Set("fingerprint-use-on-scene-kit","Process this item's prints on scene with the powder brush and lifting tape, then store the recovered sample with B.",DialogueEntryType.Tooltip);
        Set("bottle-fingerprint-identification","Comparison identified separate prints from John, Peter and Paul on the bottle. This supports shared handling during the reported gathering; it does not establish when each print was left.");
        Set("narr-beat-5-bottle-supports-brother-1","Recorder: John, Peter and Paul's prints were recovered from the bottle. Those findings fit the reported drinking gathering.");
        Set("narr-beat-5-bottle-supports-brother-2","Recorder: The prints establish handling. They don't show who was here when John was attacked.");
        Set("team-evidence-018-sealed-1","Recorder: The bottle is sealed. Its fingerprint sample is already stored and the identification results are in the record.");
        EditorUtility.SetDirty(catalog);
        var stcs=UnityEngine.Object.FindAnyObjectByType<STCSManager>();var stcsData=new SerializedObject(stcs);
        var triggers=stcsData.FindProperty("triggers");SerializedProperty target=null;
        for(int i=0;i<triggers.arraySize;i++) if(triggers.GetArrayElementAtIndex(i).FindPropertyRelative("triggerId").stringValue=="evidence_018_matched") target=triggers.GetArrayElementAtIndex(i);
        if(target==null){int index=triggers.arraySize;triggers.InsertArrayElementAtIndex(index);target=triggers.GetArrayElementAtIndex(index);}
        target.FindPropertyRelative("triggerId").stringValue="evidence_018_matched";
        target.FindPropertyRelative("pool").objectReferenceValue=null;target.FindPropertyRelative("firedOnce").boolValue=false;
        target.FindPropertyRelative("playAllLinesInSequence").boolValue=true;
        var lines=target.FindPropertyRelative("lineIds");lines.arraySize=1;lines.GetArrayElementAtIndex(0).stringValue="bottle-fingerprint-identification";
        stcsData.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);

        void Set(string id,string text,DialogueEntryType type=DialogueEntryType.Dialogue)
        {
            var entry=catalog.entries.Find(e=>e.id==id);
            if(entry==null){entry=new DialogueCatalogEntry{id=id,category=DialogueCategory.Bottle};catalog.entries.Add(entry);}
            entry.text=text;entry.type=type;entry.duration=type==DialogueEntryType.Dialogue?8:0;entry.priority=DialogueKind.Guidance;
            entry.notes="Bottle on-scene fingerprints: final photo, measurements, dust, lift and explicit B storage.";
        }
    }
}
