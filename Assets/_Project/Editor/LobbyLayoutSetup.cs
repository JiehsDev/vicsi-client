using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Scene-authored layout only; preserves all existing buttons and event bindings.</summary>
public static class LobbyLayoutSetup
{
    [MenuItem("ViCSI/Scene Setup/Organize Lobby UI")]
    public static void Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.path != "Assets/_Project/Scenes/Main.unity")
            throw new InvalidOperationException("Open Main outside Play mode.");
        var canvas = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include).First();
        var root = (RectTransform)canvas.transform;
        if (root.GetComponent<LobbyCanvasPlacement>() == null) root.gameObject.AddComponent<LobbyCanvasPlacement>();
        root.sizeDelta = new Vector2(800,1100); root.localScale = Vector3.one * .001f;
        var camera = Camera.main;
        var forward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized;
        root.position = camera.transform.position + forward * 1.5f;
        var position = root.position; position.y = LobbyCanvasPlacement.DefaultCenterHeight; root.position = position;
        root.rotation = Quaternion.LookRotation(forward, Vector3.up);
        foreach (var button in root.GetComponentsInChildren<Button>(true))
        {
            bool primary = button.name == "StartButton" || button.name == "ContinueButton" || button.name == "ConfirmButton";
            button.image.color = primary ? new Color(.29f,.45f,.62f,1) : new Color(.17f,.24f,.33f,1);
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.18f,1.18f,1.18f,1);
            colors.pressedColor = new Color(.85f,.85f,.85f,1);
            colors.disabledColor = new Color(.6f,.6f,.6f,.6f);
            button.colors = colors;
        }
        var menu = Find(root,"MainMenuPanel");
        menu.sizeDelta = Vector2.zero;
        Place(Find(menu,"Text (TMP)"),0,430,736,90,72);
        Place(Find(menu,"Subtitle"),0,354,736,50,30);
        Place(Find(menu,"StartButton"),0,215,640,100,34);
        Find(menu,"StartButton").GetComponent<Image>().color = new Color(.29f,.45f,.62f,1);
        Place(Find(menu,"InstructorButton"),0,85,640,84,28);
        Find(menu,"InstructorButton").GetComponent<Image>().color = new Color(.17f,.24f,.33f,1);
        var status = Find(menu,"StatusLine").GetComponent<TMP_Text>(); status.alpha = 0;
        var heading = Heading(menu,"HeadsetHeading","HEADSET STATUS",0,-72,736,40,status.font);
        var table = TableHost(menu,"HeadsetStatusTable",0,-220,736,220);
        LobbyTableLayout.Clear(table);
        Set(UnityEngine.Object.FindAnyObjectByType<StartMenuController>(),"statusTable",table);
        var identification = UnityEngine.Object.FindObjectsByType<StudentIdentificationPanel>(FindObjectsInactive.Include).First();
        var confirm = Find(root,"ConfirmationGroup");
        Heading(confirm,"StudentHeading","CURRENT STUDENT",0,240,736,50,status.font);
        var confirmation = TableHost(confirm,"StudentDetailsTable",0,100,736,190);
        LobbyTableLayout.Clear(confirmation);
        Set(identification,"confirmationTable",confirmation);
        Place(Find(confirm,"ConfirmTitleLabel"),0,100,736,60,30);
        Place(Find(confirm,"ConfirmDetailLabel"),0,20,736,60,26);
        Place(Find(confirm,"ConfirmQuestionLabel"),0,-50,736,50,28);
        Place(Find(confirm,"ConfirmButton"),-170,-200,300,80,28);
        Place(Find(confirm,"BackButton"),170,-200,300,80,28);
        var code = Find(root,"CodeEntryGroup");
        Find(code,"CodeInputField").GetComponent<Image>().color = new Color(.12f,.13f,.16f,1);

        Place(Find(code,"InstructionsLabel"),0,440,736,70,32);
        Place(Find(code,"CodeInputField"),0,340,500,84,44);
        Place(Find(code,"ErrorLabel"),0,244,736,88,24);
        Place(Find(code,"StatusLabel"),0,176,736,32,24);
        Place(Find(code,"CodeKeypad"),0,-62,538,332,30);
        Find(code,"CodeKeypad").localScale = Vector3.one * 1.2f;
        Place(Find(code,"ContinueButton"),-176,-340,320,80,28);
        Place(Find(code,"ContinueWithoutCodeButton"),176,-340,320,80,26);
        Place(Find(code,"BackToMenuButton"),0,-445,320,64,28);
        Find(code,"ErrorLabel").GetComponent<TMP_Text>().color = new Color(.79f,.54f,.50f,1);
        Find(code,"Key_Clear_Label").GetComponent<TMP_Text>().fontSize = 20;
        Find(code,"Key_Del_Label").GetComponent<TMP_Text>().fontSize = 20;
        Find(menu,"Text (TMP)").GetComponent<TMP_Text>().color = new Color(.82f,.68f,.36f,1);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
    }
    static RectTransform Find(Transform parent,string name) => parent.GetComponentsInChildren<RectTransform>(true).First(t=>t.name==name);
    static void Set(UnityEngine.Object target,string field,UnityEngine.Object value)
    { var so=new SerializedObject(target); so.FindProperty(field).objectReferenceValue=value; so.ApplyModifiedPropertiesWithoutUndo(); }
    static void Place(RectTransform rt,float x,float y,float w,float h,float font)
    {
        rt.anchorMin=rt.anchorMax=new Vector2(.5f,.5f); rt.pivot=new Vector2(.5f,.5f);
        rt.anchoredPosition=new Vector2(x,y); rt.sizeDelta=new Vector2(w,h);
        foreach(var t in rt.GetComponentsInChildren<TMP_Text>(true)) {t.fontSize=font; t.color=LobbyTableLayout.ValueColor;}
    }
    static RectTransform TableHost(Transform parent,string name,float x,float y,float w,float h)
    {
        var existing=parent.Find(name); var rt=existing!=null?(RectTransform)existing:LobbyTableLayout.Create(parent,name);
        Place(rt,x,y,w,h,24); return rt;
    }
    static RectTransform Heading(Transform parent,string name,string copy,float x,float y,float w,float h,TMP_FontAsset font)
    {
        var existing=parent.Find(name); var t=existing!=null?existing.GetComponent<TMP_Text>():LobbyTableLayout.Text(parent,name,copy,font,28,LobbyTableLayout.LabelColor);
        Place(t.rectTransform,x,y,w,h,28); t.alignment=TextAlignmentOptions.Left; t.color=LobbyTableLayout.LabelColor; return t.rectTransform;
    }
}

