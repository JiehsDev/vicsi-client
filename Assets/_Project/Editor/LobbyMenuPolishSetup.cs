using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Restyles existing lobby controls while preserving registration and instructor event bindings.</summary>
public static class LobbyMenuPolishSetup
{
    [MenuItem("ViCSI/Scene Setup/Polish Lobby Menu")]
    public static void Apply()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(EditorApplication.isPlayingOrWillChangePlaymode || scene.path!="Assets/_Project/Scenes/Main.unity")
            throw new InvalidOperationException("Open Main outside Play Mode.");
        var placement=UnityEngine.Object.FindAnyObjectByType<LobbyCanvasPlacement>();
        var root=(RectTransform)placement.transform;
        root.sizeDelta=new Vector2(1000,1100);
        var menu=root.GetComponentsInChildren<RectTransform>(true).First(t=>t.name=="MainMenuPanel");
        LobbyMenuStyle.Place(menu,0,0,1000,900);
        var image=menu.GetComponent<Image>(); image.sprite=null; image.type=Image.Type.Simple; image.color=LobbyMenuStyle.Background;
        var old=menu.Find("Lobby menu design"); if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);
        var decor=new GameObject("Lobby menu design",typeof(RectTransform)).GetComponent<RectTransform>();
        decor.SetParent(menu,false); LobbyMenuStyle.Place(decor,0,0,1000,900); decor.SetAsFirstSibling();
        LobbyMenuStyle.Frame(decor); LobbyMenuStyle.HomeIntro(decor); LobbyMenuStyle.Guide(decor);
        var title=menu.GetComponentsInChildren<TMP_Text>(true).First(t=>t.transform.parent==menu&&t.name=="Text (TMP)");
        LobbyMenuStyle.Place(title.rectTransform,-290,374,320,80); title.text="ViCSI"; title.fontSize=58;
        title.color=LobbyMenuStyle.Text; title.alignment=TextAlignmentOptions.MidlineLeft;
        var subtitle=Find(menu,"Subtitle").GetComponent<TMP_Text>();
        LobbyMenuStyle.Place(subtitle.rectTransform,0,310,900,48); subtitle.fontSize=28; subtitle.color=LobbyMenuStyle.Secondary;
        subtitle.text="Virtual Crime Scene Investigation"; subtitle.alignment=TextAlignmentOptions.MidlineLeft;
        LobbyMenuStyle.Rect(decor,"Edition badge",350,375,200,40,LobbyMenuStyle.Surface);
        LobbyMenuStyle.Label(decor,"Edition","RESEARCH",350,375,190,40,24,LobbyMenuStyle.Gold,TextAlignmentOptions.Center);
        var start=Find(menu,"StartButton"); LobbyMenuStyle.Place(start,-190,22,520,96);
        LobbyMenuStyle.Button(start.GetComponent<Button>(),true);
        start.GetComponentInChildren<TMP_Text>().text="Start investigation";
        var instructor=Find(menu,"InstructorButton"); LobbyMenuStyle.Place(instructor,-190,-88,520,74);
        LobbyMenuStyle.Button(instructor.GetComponent<Button>(),false);
        instructor.GetComponentInChildren<TMP_Text>().text="Instructor / headset settings";
        var heading=Find(menu,"HeadsetHeading").GetComponent<TMP_Text>();
        LobbyMenuStyle.Place(heading.rectTransform,-190,-166,520,34); heading.text="DEVICE STATUS"; heading.fontSize=24;
        heading.color=LobbyMenuStyle.Secondary; heading.alignment=TextAlignmentOptions.MidlineLeft;
        var table=Find(menu,"HeadsetStatusTable"); LobbyMenuStyle.Place(table,-190,-275,520,180);
        var status=Find(menu,"StatusLine").GetComponent<TMP_Text>(); status.alpha=0;
        LobbyMenuStyle.Label(decor,"Lobby controls","Point at a button + press trigger to select",0,-412,900,40,26,LobbyMenuStyle.Secondary,TextAlignmentOptions.Center);
        UnityEngine.Object.FindAnyObjectByType<StartMenuController>()?.RefreshStatus();
        // Carry the same palette into the existing student-code and confirmation flow.
        var identification=root.GetComponentsInChildren<StudentIdentificationPanel>(true).FirstOrDefault();
        if(identification!=null)
        {
            foreach(var background in identification.GetComponentsInChildren<Image>(true))
            {
                if(background.GetComponent<Button>()!=null || background.GetComponent<TMP_InputField>()!=null)
                { background.sprite=null; background.type=Image.Type.Simple; background.color=LobbyMenuStyle.Surface; }
                else if(background.name=="StudentIdentificationPanel")
                { background.sprite=null; background.type=Image.Type.Simple; background.color=LobbyMenuStyle.Background; }
            }
            foreach(var button in identification.GetComponentsInChildren<Button>(true))
                if(new[]{"ContinueButton","ContinueWithoutCodeButton","BackToMenuButton","ConfirmButton","BackButton"}.Contains(button.name))
                    LobbyMenuStyle.Button(button,button.name=="ContinueButton"||button.name=="ConfirmButton");
        }
        Canvas.ForceUpdateCanvases();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
    }
    static RectTransform Find(Transform parent,string name)=>parent.GetComponentsInChildren<RectTransform>(true).First(t=>t.name==name);
}
