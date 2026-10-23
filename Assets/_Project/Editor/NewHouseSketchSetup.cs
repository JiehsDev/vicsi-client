using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Builds the kitchen detail and both orientation plans from the current house geometry.</summary>
public static class NewHouseSketchSetup
{
    static readonly Color Ink = new Color(.12f,.17f,.22f,1), Wood = new Color(.5f,.3f,.12f,1), Glass = new Color(.08f,.42f,.57f,1);
    static CrimeSceneSketchTemplate template;
    static RectTransform board;
    static Transform house;

    [MenuItem("ViCSI/Scene Setup/Rebuild New House Sketch Map")]
    public static void Rebuild()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Requires Edit mode.");
        template = UnityEngine.Object.FindAnyObjectByType<CrimeSceneSketchTemplate>(FindObjectsInactive.Include);
        house = GameObject.Find("Vicsi_House").transform;
        if (template == null) throw new InvalidOperationException("Missing sketch template.");
        board = template.Board;
        foreach (Transform child in board.Cast<Transform>().ToArray()) Undo.DestroyObjectImmediate(child.gameObject);
        Undo.RecordObject(template,"Remap sketch to rear kitchen");
        Undo.RecordObject(template.definition,"Remake house sketch");
        template.definition.title = "Crime Scene Sketch — Rear Kitchen";
        template.definition.showGrid = false;
        template.definition.gridSpacing = 50;
        template.definition.FindElement("BODY-A").placedSize = new Vector2(58,145);
        template.worldMinXZ = new Vector2(14.5f,10.9f); template.worldMaxXZ = new Vector2(19.9f,15.25484f);
        template.mappedArea = Rect(board,"RearKitchenDetail",new Vector2(-160,90),new Vector2(620,500));
        Fixed(template.mappedArea,SketchFixedElementKind.Outline);
        var main = DrawPlan(template.mappedArea,2.6f,template.worldMinXZ,template.worldMaxXZ,true);
        Label(board,"KitchenTitle","REAR KITCHEN · GROUND FLOOR",new Vector2(-160,440),new Vector2(620,50),25,true);
        Label(board,"KitchenSubtitle","Place evidence on this detail plan",new Vector2(-160,404),new Vector2(620,35),19);
        var fullMin = new Vector2(10.3f,4.15f); var fullMax = new Vector2(20.3f,15.45f);
        var ground = Rect(board,"GroundFloorPlan",new Vector2(330,250),new Vector2(240,271.2f));
        Fixed(ground,SketchFixedElementKind.KeyPlan); DrawPlan(ground,2.6f,fullMin,fullMax,false);
        Label(board,"GroundTitle","GROUND FLOOR",new Vector2(330,414),new Vector2(290,32),20,true);
        var upper = Rect(board,"UpperFloorPlan",new Vector2(330,-70),new Vector2(240,271.2f));
        Fixed(upper,SketchFixedElementKind.KeyPlan); DrawPlan(upper,5.6f,fullMin,fullMax,false);
        Label(board,"UpperTitle","UPPER FLOOR",new Vector2(330,90),new Vector2(290,32),20,true);
        PlanLabel(ground,"Kitchen","Kitchen",new Vector2(17.4f,13.1f),fullMin,fullMax,18);
        PlanLabel(ground,"Living","Living",new Vector2(17.7f,9.6f),fullMin,fullMax,18);
        PlanLabel(ground,"Dining","Dining",new Vector2(13.3f,7.6f),fullMin,fullMax,18);
        PlanLabel(ground,"Entry","Entry",new Vector2(17.7f,5.7f),fullMin,fullMax,16);
        PlanLabel(ground,"StairsUp","Stairs ↑",new Vector2(12.6f,10.4f),fullMin,fullMax,16);
        PlanLabel(upper,"StairsDown","Stairs ↓",new Vector2(12.6f,10.4f),fullMin,fullMax,16);
        PlanLabel(upper,"Hall","Hall",new Vector2(15.1f,10.4f),fullMin,fullMax,16);
        foreach (var center in new[]{new Vector2(13.2f,7),new Vector2(17.7f,6.8f),new Vector2(17.7f,13.2f),new Vector2(12.2f,13.2f)})
            PlanLabel(upper,"Room_"+center.x+"_"+center.y,"Room",center,fullMin,fullMax,16);
        // Kitchen cabinets are fixed reference structures; never add evidence silhouettes to these plans.
        PlanLabel(template.mappedArea,"SinkLabel","Sink",new Vector2(16.25f,14.35f),template.worldMinXZ,template.worldMaxXZ,18);
        PlanLabel(template.mappedArea,"KitchenNorthWall","Rear Kitchen Wall",new Vector2(17.2f,14.95f),template.worldMinXZ,template.worldMaxXZ,18);
        PlanLabel(template.mappedArea,"RearCabinetLabel","Rear cabinet front",new Vector2(16.85f,13.85f),template.worldMinXZ,template.worldMaxXZ,16);
        PlanLabel(template.mappedArea,"WestCabinetLabel","West cabinet front",new Vector2(15.85f,13.55f),template.worldMinXZ,template.worldMaxXZ,15).localEulerAngles=new Vector3(0,0,90);
        PlanLabel(template.mappedArea,"CabinetEndLabel","Right cabinet end",new Vector2(18.7f,14.3f),template.worldMinXZ,template.worldMaxXZ,15);
        PlanLabel(template.mappedArea,"KitchenEastWall","East Wall",new Vector2(19.55f,12.9f),template.worldMinXZ,template.worldMaxXZ,16).localEulerAngles=new Vector3(0,0,90);
        PlanLabel(template.mappedArea,"KitchenDoor","Kitchen entrance",new Vector2(15.55f,11.12f),template.worldMinXZ,template.worldMaxXZ,18);
        var north = Rect(board,"NorthArrow",new Vector2(170,455),new Vector2(40,55)); Fixed(north,SketchFixedElementKind.NorthArrow);
        var ng=north.gameObject.AddComponent<SketchPlanGraphic>(); ng.raycastTarget=false;
        Stroke(ng,new Vector2(0,-20),new Vector2(0,15),Ink,3); Stroke(ng,new Vector2(-8,4),new Vector2(0,15),Ink,3); Stroke(ng,new Vector2(8,4),new Vector2(0,15),Ink,3);
        Label(north,"N","N",new Vector2(0,31),new Vector2(30,25),22,true);
        var scale=Rect(board,"DetailScale",new Vector2(-160,-190),new Vector2(300,30));Fixed(scale,SketchFixedElementKind.ScaleBar);
        var sg=scale.gameObject.AddComponent<SketchPlanGraphic>();sg.raycastTarget=false;float metre=620f/5.4f;
        Stroke(sg,new Vector2(-metre*.5f,0),new Vector2(metre*.5f,0),Ink,3);
        foreach(float x in new[]{-metre*.5f,metre*.5f}) Stroke(sg,new Vector2(x,-5),new Vector2(x,5),Ink,2);
        Label(scale,"ScaleText","0                 1 m · detail",new Vector2(0,-20),new Vector2(260,28),17);
        Label(board,"MeasurementSection","RECORDED DISTANCES · Match each fixed reference",new Vector2(0,-247),new Vector2(940,35),20,true);
        Label(board,"PlanLegend","Walls  ·  brown: doors / cabinets  ·  blue: windows     N = world +Z",new Vector2(0,-480),new Vector2(950,28),17);
        foreach (var element in template.definition.evidenceElements)
        {
            var anchor=Rect(board,element.correctAnchorId,Vector2.zero,new Vector2(10,10)).gameObject.AddComponent<SketchEvidenceAnchor>();
            anchor.anchorId=element.correctAnchorId;anchor.snapRadius=25;
            if(!MeasurementGeometry.TryGetEvidenceAnchor(element.evidenceId,out var point))throw new InvalidOperationException("Missing sketch evidence anchor: "+element.evidenceId);
            anchor.RectTransform.anchoredPosition=template.WorldToBoard(point);
        }
        for(int i=0;i<template.definition.measurementSlots.Count;i++)
        {
            var definition=template.definition.measurementSlots[i];var element=template.definition.FindElement(definition.evidenceId);
            var reference=NewHouseInvestigationSetup.Ref(definition.referenceId);
            definition.relationshipText=element.displayName+" / "+reference.displayName;
            var rect=Rect(board,definition.slotId,new Vector2(-360+(i/2)*240,-335-(i%2)*100),new Vector2(150,40));
            var slot=rect.gameObject.AddComponent<SketchMeasurementSlot>();slot.slotId=definition.slotId;slot.dropMargin=12;
            var foot=Rect(board,definition.slotId+"_Foot",Vector2.zero,Vector2.one);slot.referenceFoot=foot;
            MeasurementGeometry.TryGetEvidenceAnchor(definition.evidenceId,out var point);
            foot.anchoredPosition=template.WorldToBoard(MeasurementGeometry.ReferenceFoot(reference,point));
        }
        EditorUtility.SetDirty(template);EditorUtility.SetDirty(template.definition);
        EditorSceneManager.MarkSceneDirty(board.gameObject.scene);
    }

    public static void SynchronizeEvidence()
    {
        var current = UnityEngine.Object.FindAnyObjectByType<CrimeSceneSketchTemplate>(FindObjectsInactive.Include);
        Undo.RecordObject(current.definition,"Update sketch reference labels");
        foreach (var element in current.definition.evidenceElements)
        {
            if (!MeasurementGeometry.TryGetEvidenceAnchor(element.evidenceId,out var point))
                throw new InvalidOperationException("Missing evidence anchor: "+element.evidenceId);
            var anchor = current.FindAnchor(element.correctAnchorId).RectTransform;
            Undo.RecordObject(anchor,"Follow moved sketch evidence");
            anchor.anchoredPosition = current.WorldToBoard(point);
        }
        foreach (var definition in current.definition.measurementSlots)
        {
            var reference = NewHouseInvestigationSetup.Ref(definition.referenceId);
            definition.relationshipText = current.definition.FindElement(definition.evidenceId).displayName+" / "+reference.displayName;
            MeasurementGeometry.TryGetEvidenceAnchor(definition.evidenceId,out var point);
            var foot = current.FindSlot(definition.slotId).referenceFoot;
            Undo.RecordObject(foot,"Follow moved measurement foot");
            foot.anchoredPosition = current.WorldToBoard(MeasurementGeometry.ReferenceFoot(reference,point));
        }
        EditorUtility.SetDirty(current.definition);
        EditorSceneManager.MarkSceneDirty(current.gameObject.scene);
    }

    static SketchPlanGraphic DrawPlan(RectTransform area,float height,Vector2 min,Vector2 max,bool counters)
    {
        var graphic=area.gameObject.AddComponent<SketchPlanGraphic>();graphic.raycastTarget=false;
        foreach(var filter in house.GetComponentsInChildren<MeshFilter>(true))
        {
            var t=filter.transform;
            if(t.parent!=house||!(t.name.StartsWith("walls")||t.name.StartsWith("Door")||t.name.StartsWith("Window")||t.name=="Handle_Back.016"))continue;
            var mesh=filter.sharedMesh;var vertices=mesh.vertices;var triangles=mesh.triangles;
            Color color=t.name.StartsWith("Window")?Glass:t.name.StartsWith("Door")||t.name.StartsWith("Handle")?Wood:Ink;
            for(int i=0;i<triangles.Length;i+=3)
            {
                var hits=new List<Vector3>(2);
                for(int j=0;j<3;j++)
                {
                    var a=t.TransformPoint(vertices[triangles[i+j]]);var b=t.TransformPoint(vertices[triangles[i+(j+1)%3]]);
                    if((a.y<height)!=(b.y<height))hits.Add(Vector3.Lerp(a,b,(height-a.y)/(b.y-a.y)));
                }
                if(hits.Count!=2)continue;
                Vector2 p=new Vector2(hits[0].x,hits[0].z),q=new Vector2(hits[1].x,hits[1].z);
                if(!ClipLine(ref p,ref q,min,max))continue;
                Stroke(graphic,Project(p,area,min,max),Project(q,area,min,max),color,counters?2.5f:1.6f);
            }
        }
        if(counters)
        {
            foreach(var f in house.GetComponentsInChildren<MeshFilter>(true))
            {
                if(!(f.name.StartsWith("CabinetB")&&!f.name.Contains("Door")&&!f.name.Contains("Drawer"))&&f.name!="Sink01")continue;
                var renderer=f.GetComponent<Renderer>();if(renderer==null)continue;var b=renderer.bounds;
                Rectangle(graphic,new Vector2(b.min.x,b.min.z),new Vector2(b.max.x,b.max.z),area,min,max,Wood,2);
            }
        }
        else
        {
            // Stair tread projections, sampled separately so both floor plans identify the same route.
            for(int i=0;i<10;i++)
            {
                float x=14.727f-i*.3f;Stroke(graphic,Project(new Vector2(x,9.402f),area,min,max),Project(new Vector2(x,10.302f),area,min,max),Wood,1.2f);
            }
            for(int i=0;i<7;i++)
            {
                float x=11.991f+i*.3f;Stroke(graphic,Project(new Vector2(x,10.502f),area,min,max),Project(new Vector2(x,11.402f),area,min,max),Wood,1.2f);
            }
        }
        graphic.SetVerticesDirty();return graphic;
    }
    static void Rectangle(SketchPlanGraphic g,Vector2 a,Vector2 b,RectTransform area,Vector2 min,Vector2 max,Color color,float width)
    {
        var points=new[]{a,new Vector2(b.x,a.y),b,new Vector2(a.x,b.y),a};
        for(int i=0;i<4;i++){var p=points[i];var q=points[i+1];if(ClipLine(ref p,ref q,min,max))Stroke(g,Project(p,area,min,max),Project(q,area,min,max),color,width);}
    }
    static Vector2 Project(Vector2 p,RectTransform area,Vector2 min,Vector2 max)=>new Vector2(((p.x-min.x)/(max.x-min.x)-.5f)*area.sizeDelta.x,((p.y-min.y)/(max.y-min.y)-.5f)*area.sizeDelta.y);
    static bool ClipLine(ref Vector2 a,ref Vector2 b,Vector2 min,Vector2 max)
    {
        Vector2 d=b-a;float low=0,high=1;
        if(!Edge(-d.x,a.x-min.x)||!Edge(d.x,max.x-a.x)||!Edge(-d.y,a.y-min.y)||!Edge(d.y,max.y-a.y))return false;
        b=a+d*high;a+=d*low;return true;
        bool Edge(float p,float q){if(Mathf.Abs(p)<.00001f)return q>=0;float r=q/p;if(p<0){if(r>high)return false;low=Mathf.Max(low,r);}else{if(r<low)return false;high=Mathf.Min(high,r);}return true;}
    }
    static void Stroke(SketchPlanGraphic g,Vector2 a,Vector2 b,Color color,float width)=>g.strokes.Add(new SketchPlanStroke{from=a,to=b,color=color,width=width});
    static RectTransform Rect(Transform parent,string name,Vector2 position,Vector2 size)
    {
        var go=new GameObject(name,typeof(RectTransform));Undo.RegisterCreatedObjectUndo(go,"Remake sketch map");var rect=go.GetComponent<RectTransform>();rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.sizeDelta=size;rect.anchoredPosition=position;return rect;
    }
    static void Fixed(RectTransform rect,SketchFixedElementKind kind){var f=rect.gameObject.AddComponent<SketchFixedElement>();f.elementId=rect.name;f.kind=kind;}
    static RectTransform Label(Transform parent,string name,string text,Vector2 position,Vector2 size,float font,bool bold=false)
    {
        var rect=Rect(parent,name,position,size);var label=rect.gameObject.AddComponent<TextMeshProUGUI>();label.text=text;label.fontSize=font;label.color=Ink;label.alignment=TextAlignmentOptions.Center;label.raycastTarget=false;label.fontStyle=bold?FontStyles.Bold:FontStyles.Normal;label.textWrappingMode=TextWrappingModes.Normal;Fixed(rect,SketchFixedElementKind.Label);return rect;
    }
    static RectTransform PlanLabel(RectTransform plan,string name,string text,Vector2 point,Vector2 min,Vector2 max,float font)=>Label(plan,name,text,Project(point,plan,min,max),new Vector2(140,35),font);
}
