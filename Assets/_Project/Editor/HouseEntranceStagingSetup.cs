#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Move only the entry/staging layout; preserve investigation objects and data.</summary>
public static class HouseEntranceStagingSetup
{
    public static void Apply()
    {
        const string output = "docs/evidence/entrance-staging";
        Directory.CreateDirectory(output);
        try
        {
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Scenarios/CSI_Environment.unity");
            var cordon = UnityEngine.Object.FindAnyObjectByType<SceneCordon>();
            var root = cordon.transform;
            var left = root.Find("EntrancePost_L"); var right = root.Find("EntrancePost_R");
            var entry = (left.position + right.position) * .5f;
            root.position += new Vector3(0, 0, 2.7f - entry.z);
            foreach (var line in root.GetComponentsInChildren<LineRenderer>(true))
                if (line.useWorldSpace)
                    for (int i = 0; i < line.positionCount; i++) line.SetPosition(i, line.GetPosition(i) + new Vector3(0, 0, 2.7f-entry.z));
            var data = new SerializedObject(cordon);
            // A short entry strip joins the tape to the inner footprint, without
            // extending the controlled approach across the accessible work yard.
            var bounds = new Bounds();
            bounds.SetMinMax(root.InverseTransformPoint(new Vector3(10.05f,-.5f,3f)),root.InverseTransformPoint(new Vector3(21.55f,7.5f,15.5f)));
            data.FindProperty("protectedArea").boundsValue = bounds;
            var approach=new Bounds();
            approach.SetMinMax(root.InverseTransformPoint(new Vector3(left.position.x,-.5f,2.7f)),root.InverseTransformPoint(new Vector3(right.position.x,3.5f,3f)));
            data.FindProperty("controlledEntryApproach").boundsValue = approach;
            Transform Anchor(string name, Vector3 position)
            {
                var anchor=root.Find(name);
                if (anchor==null) { anchor=new GameObject(name).transform; anchor.SetParent(root,false); }
                anchor.SetPositionAndRotation(position,Quaternion.identity); return anchor;
            }
            data.FindProperty("supplyStationAnchor").objectReferenceValue=Anchor("Front yard supply table anchor",new Vector3(18.2f,.03f,1.6f));
            data.FindProperty("fingerprintStationAnchor").objectReferenceValue=Anchor("Front yard comparison desk anchor",new Vector3(21.35f,.03f,1.6f));
            data.ApplyModifiedPropertiesWithoutUndo();
            var responder=GameObject.Find("NPC_FirstResponder").transform;
            responder.position=new Vector3(18.15f,.03f,2.4f);
            var rig=UnityEngine.Object.FindAnyObjectByType<OVRCameraRig>().transform;
            var hotspot=GameObject.Find("Teleport Hotspot").transform;
            var feet=new Vector3(16.9f,.03f,.65f);
            float eyeOriginOffset=rig.position.y-hotspot.position.y;
            Vector3 toResponder=responder.position-feet; toResponder.y=0;
            Vector3 toEntrance=(left.position+right.position)*.5f-feet; toEntrance.y=0;
            var rotation=Quaternion.LookRotation(toResponder.normalized+toEntrance.normalized);
            rig.SetPositionAndRotation(feet+Vector3.up*eyeOriginOffset,rotation);
            hotspot.SetPositionAndRotation(feet,rotation);
            responder.rotation=Quaternion.LookRotation(feet-responder.position);
            var box=GameObject.Find("EvidenceHoldingCrate").transform;
            box.position=new Vector3(14.4f,box.position.y,1.5f);
            Physics.SyncTransforms();
            var controller=UnityEngine.Object.FindAnyObjectByType<Oculus.Interaction.Locomotion.CharacterController>();
            int mask=controller.LayerMask.value & ~(1<<2);
            var standing=new[]{ feet,new Vector3(18.2f,.03f,.75f),new Vector3(19.75f,.03f,.75f),new Vector3(21.35f,.03f,.75f),new Vector3(14.4f,.03f,.65f) };
            var errors=standing.SelectMany(at=>Physics.OverlapCapsule(at+Vector3.up*(controller.Radius+.025f),at+Vector3.up*(1.7f-controller.Radius),
                controller.Radius,mask,QueryTriggerInteraction.Ignore).Where(c=>!c.transform.IsChildOf(rig)).Select(c=>at+": "+c.name)).ToArray();
            File.WriteAllLines(output+"/authoring-standing-obstructions.txt",errors);
            if(errors.Length>0) throw new InvalidOperationException("Staging standing clearance failed; saved scene untouched.");
            if(!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save entrance staging.");
            File.WriteAllText(output+"/authoring.txt","Applied entrance at z=2.7; yard tables and arrival inside fence.\n");
            EditorApplication.Exit(0);
        }
        catch(Exception error) { Debug.LogException(error);File.WriteAllText(output+"/authoring-error.txt",error.ToString());EditorApplication.Exit(1); }
    }
}
#endif
