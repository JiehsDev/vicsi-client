#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Restore lost procedural wheel textures without rebuilding the authored layout.</summary>
public static class ToolWheelVisualRepair
{
    const string Folder="Assets/_Project/Art/EditableSceneVisuals";
    const string Evidence="docs/evidence/tool-wheel-textures-2026-10-21";
    const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;

    [MenuItem("Tools/ViCSI/Scene Setup/Repair Tool Wheel Textures")]
    public static void Repair()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Repair outside Play mode.");
        var wheel=UnityEngine.Object.FindAnyObjectByType<ToolWheelController>(FindObjectsInactive.Include);
        if(wheel==null)throw new InvalidOperationException("Open the investigation scene first.");
        Directory.CreateDirectory(Evidence);
        foreach(var name in new[]{"Backdrop","CenterHub","Wedge"})
        {
            string method;object[] arguments;
            switch(name)
            {
                case "Backdrop":method="CreateRingSprite";arguments=new object[]{256,.97f};break;
                case "Wedge":method="CreateRingSprite";arguments=new object[]{256,.42f};break;
                case "CenterHub":method="CreateCircleSprite";arguments=new object[]{256};break;
                default:throw new InvalidOperationException("Unknown missing wheel sprite: "+name);
            }
            string legacy=Folder+"/_asset"+(name=="CenterHub"?" 1":name=="Wedge"?" 2":"");
            string path=Folder+"/ToolWheel_"+name+".asset";
            if(File.Exists(legacy))
            {
                if(!File.Exists(Evidence+"/"+name+"-before.asset"))File.Copy(legacy,Evidence+"/"+name+"-before.asset");
                string error=AssetDatabase.MoveAsset(legacy,path);if(!string.IsNullOrEmpty(error))throw new IOException(error);
            }
            var existing=AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if(existing!=null&&existing.texture!=null&&EditorUtility.IsPersistent(existing.texture))continue;
            string texturePath=Folder+"/ToolWheel_"+name+"_Texture.asset";
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            Sprite rebuilt;
            if(texture==null)
            {
                rebuilt=(Sprite)typeof(ToolWheelController).GetMethod(method,Private).Invoke(null,arguments);
                rebuilt.texture.name="ToolWheel_"+name+"_Texture";
                AssetDatabase.CreateAsset(rebuilt.texture,texturePath);
            }
            else rebuilt=Sprite.Create(texture,new Rect(0,0,256,256),new Vector2(.5f,.5f),100);
            rebuilt.name="ToolWheel_"+name;
            // Keep the original GUID and Sprite fileID so saved scene references reconnect.
            AssetDatabase.CreateAsset(rebuilt,path);
        }
        AssetDatabase.SaveAssets();
        ValidateAndCapture();
    }

    public static void ValidateAndCapture()
    {
        var wheel=UnityEngine.Object.FindAnyObjectByType<ToolWheelController>(FindObjectsInactive.Include);
        var report=new System.Text.StringBuilder();int count=0;
        foreach(var image in wheel.GetComponentsInChildren<Image>(true).Where(i=>i.sprite!=null))
        {
            string path=AssetDatabase.GetAssetPath(image.sprite);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if(sprite==null||sprite.texture==null||!EditorUtility.IsPersistent(sprite.texture))throw new InvalidOperationException("Missing persisted texture: "+image.name);
            var texture=sprite.texture;var center=texture.GetPixel(128,128);var band=texture.GetPixel(128,228);var corner=texture.GetPixel(0,0);
            bool valid=corner.a<.01f&&(image.name=="CenterHub"?center.a>.99f:center.a<.01f)
                &&(image.name=="Backdrop"?band.a<.01f:band.a>.99f);
            if(!valid)throw new InvalidOperationException("Incorrect wheel alpha mask: "+image.name);
            report.AppendLine("PASS: "+image.name+" sprite and texture survive reimport with correct alpha mask");count++;
        }
        if(count!=3)throw new InvalidOperationException("Expected all three authored wheel masks.");
        Directory.CreateDirectory(Evidence);
        var main=wheel.gameObject.scene;bool dirty=main.isDirty;
        var temp=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        var previous=RenderTexture.active;var rt=RenderTexture.GetTemporary(1200,1200,24);
        Texture2D proof=null;
        try
        {
            var copy=UnityEngine.Object.Instantiate(wheel.gameObject);copy.name="Wheel visual verification";
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(copy,temp);
            var controller=copy.GetComponent<ToolWheelController>();
            typeof(ToolWheelController).GetMethod("EnsureUI",Private).Invoke(controller,null);
            typeof(ToolWheelController).GetMethod("BuildSegments",Private).Invoke(controller,null);
            typeof(ToolWheelController).GetField("hoveredIndex",Private).SetValue(controller,1);
            typeof(ToolWheelController).GetMethod("UpdateHighlight",Private).Invoke(controller,null);
            foreach(var child in copy.GetComponentsInChildren<Transform>(true))child.gameObject.layer=31;
            var canvas=copy.GetComponentInChildren<Canvas>(true);canvas.gameObject.SetActive(true);
            canvas.transform.SetPositionAndRotation(new Vector3(0,0,1),Quaternion.identity);canvas.transform.localScale=Vector3.one*.0009f;
            var camera=new GameObject("Wheel proof camera").AddComponent<Camera>();camera.scene=temp;camera.enabled=false;camera.cullingMask=1<<31;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.15f,.22f,.25f);camera.fieldOfView=60;camera.nearClipPlane=.01f;
            camera.targetTexture=rt;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;
            proof=new Texture2D(1200,1200,TextureFormat.RGB24,false);proof.ReadPixels(new Rect(0,0,1200,1200),0,0);proof.Apply();
            File.WriteAllBytes(Evidence+"/wheel-restored.png",proof.EncodeToPNG());
            report.AppendLine("PASS: Existing authored wheel builds its role wedges for a native edit-mode preview");
        }
        finally
        {
            RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);if(proof!=null)UnityEngine.Object.DestroyImmediate(proof);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(main);EditorSceneManager.CloseScene(temp,true);
        }
        if(main.isDirty!=dirty)throw new InvalidOperationException("Preview changed the original scene dirty state.");
        report.AppendLine("PASS: Original scene layout and dirty state retained; temporary preview scene closed");
        File.WriteAllText(Evidence+"/validation.txt",report.ToString());Debug.Log(report.ToString());
    }
}
#endif
