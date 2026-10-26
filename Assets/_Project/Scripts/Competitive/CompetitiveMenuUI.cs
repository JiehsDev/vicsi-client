using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Fixed lobby menu with an obvious primary action, readable rankings and a VR username keyboard.</summary>
public sealed class CompetitiveMenuUI : MonoBehaviour
{
    GameObject panel;
    RectTransform root, content;
    TMP_Text heading, description, footer;
    WorldPanelPointer pointer;
    InputAction navigate, confirm, back;
    readonly List<(Image image, Action action, TMP_Text label, bool primary)> buttons = new();
    string screen = "home", username, nameError;
    int selected, page;
    float nextMove, inputAfter;
    bool settingsOpen, holdsLocomotion;
    SettingsPauseMenu settings;
    CompetitiveEdition Edition => CompetitiveEdition.Instance;

    void Start()
    {
        FindAnyObjectByType<UIManager>()?.HideResearchPanels();
        foreach (var old in FindObjectsByType<StudentIdentificationPanel>(FindObjectsInactive.Include)) old.Hide();
        foreach (var old in FindObjectsByType<StartMenuController>()) old.HideResearchStatus();
        username = Edition.Username ?? "";
        panel = new GameObject("Competitive menu",typeof(RectTransform),typeof(Canvas));
        root = (RectTransform)panel.transform; root.sizeDelta=new Vector2(1000,900); root.localScale=Vector3.one*.001f;
        var canvas=panel.GetComponent<Canvas>(); canvas.renderMode=RenderMode.WorldSpace; canvas.sortingOrder=32000;
        var anchor=FindAnyObjectByType<LobbyCanvasPlacement>();
        if(anchor!=null)
        {
            root.SetParent(anchor.transform,false);
            root.localScale=Vector3.one*(.001f/anchor.transform.lossyScale.x);
        }
        else panel.AddComponent<LobbyCanvasPlacement>();
        LobbyMenuStyle.Frame(root);
        heading=Label(root,"Title","",-100,374,700,80,48,LobbyMenuStyle.Text);
        description=Label(root,"Description","",0,310,900,55,27,LobbyMenuStyle.Secondary);
        LobbyMenuStyle.Rect(root,"Edition badge",350,375,200,40,LobbyMenuStyle.Surface);
        Label(root,"Edition","COMPETITIVE",350,375,190,40,24,LobbyMenuStyle.Gold,TextAlignmentOptions.Center);
        footer=Label(root,"Controls","Stick: choose   A: select   B: back   |   Point + trigger",0,-412,900,40,24,LobbyMenuStyle.Secondary,TextAlignmentOptions.Center);
        content=new GameObject("Content",typeof(RectTransform)).GetComponent<RectTransform>(); content.SetParent(root,false);
        pointer=new WorldPanelPointer(root,"CompetitiveMenu",this);
        navigate=new InputAction("CompetitiveNavigate",InputActionType.Value,"<XRController>{RightHand}/primary2DAxis");
        confirm=PlayerUIGate.CreateConfirmAction(this,"CompetitiveConfirm",()=>panel!=null&&panel.activeSelf,600);
        back=new InputAction("CompetitiveBack",InputActionType.Button,"<XRController>{RightHand}/secondaryButton");
        navigate.Enable();confirm.Enable();back.Enable();HoldMenu();pointer.Enable();Draw();
    }
    static TMP_Text Label(Transform parent,string name,string value,float x,float y,float width,float height,float size,Color color,
        TextAlignmentOptions alignment=TextAlignmentOptions.MidlineLeft)=>LobbyMenuStyle.Label(parent,name,value,x,y,width,height,size,color,alignment);
    void Button(string copy,float x,float y,float width,Action action,float height=70,bool primary=false)
    {
        var image=LobbyMenuStyle.Rect(content,"Button "+copy,x,y,width,height,primary?LobbyMenuStyle.Gold:LobbyMenuStyle.Surface);
        var label=Label(image.transform,"Label",copy,0,0,width-44,height-12,primary?32:28,
            primary?LobbyMenuStyle.Ink:LobbyMenuStyle.Text,TextAlignmentOptions.Center);
        var focus=LobbyMenuStyle.Rect(image.transform,"Focus",-width*.5f+3,0,6,height,LobbyMenuStyle.Text);
        focus.gameObject.SetActive(false);
        int index=buttons.Count;buttons.Add((image,action,label,primary));
        pointer.Register("menu-"+index,image.rectTransform,Vector2.zero,()=>Activate(index),hover=>{if(hover){selected=index;Highlight();}});
    }
    void Draw(bool keepSelection=false)
    {
        int previous=selected;
        pointer.Unregister("menu-");buttons.Clear();selected=0;inputAfter=Time.unscaledTime+.25f;
        for(int i=content.childCount-1;i>=0;i--){content.GetChild(i).gameObject.SetActive(false);Destroy(content.GetChild(i).gameObject);}
        heading.fontSize=screen=="home"?58:44;
        heading.text=screen=="home"?"ViCSI":screen=="name"?"Choose a username":screen=="board"?"Leaderboard":"Run history";
        description.text=screen=="home"?"Virtual Crime Scene Investigation":screen=="name"
            ?"Your name appears on this headset's leaderboard."
            :screen=="board"?"Best run per player. Higher score first; faster time breaks ties.":"Your completed investigations, most recent first.";
        if(screen=="home") HomeContent();
        else if(screen=="name") KeyboardContent();
        else RankingContent();
        if(keepSelection)selected=Mathf.Clamp(previous,0,buttons.Count-1);
        Highlight();
    }
    void HomeContent()
    {
        LobbyMenuStyle.HomeIntro(content);LobbyMenuStyle.Guide(content);
        Button("Start investigation",-190,22,520,()=>{screen="name";nameError=null;Draw();},96,true);
        Button("Leaderboard",-190,-80,520,()=>{screen="board";page=0;Draw();});
        Button("Run history",-190,-165,520,()=>{screen="history";page=0;Draw();});
        Button("Game settings",-190,-250,520,OpenSettings);
        Button("Demo rankings: "+(Edition.ShowDemoRankings?"On":"Off"),290,-294,290,
            ()=>{Edition.SetDemoRankings(!Edition.ShowDemoRankings);Draw(true);},62);
        Label(content,"Profile",CompetitiveRanking.ValidUsername(username)?"Player: "+username:"Choose your username to begin.",-190,-322,520,40,24,LobbyMenuStyle.Secondary);
    }
    void KeyboardContent()
    {
        LobbyMenuStyle.Rect(content,"Username field",0,220,900,74,LobbyMenuStyle.Surface);
        Label(content,"Entered username",username.Length>0?username:"Enter a username",0,220,850,64,34,
            username.Length>0?LobbyMenuStyle.Text:LobbyMenuStyle.Secondary);
        Label(content,"Username help",nameError??"2-20 characters: letters, numbers, spaces, - or _",0,164,900,38,24,
            nameError==null?LobbyMenuStyle.Secondary:new Color(1,.64f,.60f,1));
        string[] rows={"ABCDEFGHIJ","KLMNOPQRST","UVWXYZ0123","456789-_"};
        for(int row=0;row<rows.Length;row++)for(int col=0;col<rows[row].Length;col++)
        {
            char key=rows[row][col];Button(key.ToString(),-378+col*84,96-row*72,76,()=>Type(key),62);
        }
        Button("Space",-300,-196,280,()=>Type(' '),62);
        Button("Delete",0,-196,280,()=>{if(username.Length>0)username=username[..^1];nameError=null;Draw(true);},62);
        Button("Clear",300,-196,280,()=>{username="";nameError=null;Draw(true);},62);
        Button("Begin investigation",0,-278,900,Begin,72,true);
        Button("Back to menu",0,-345,900,Home,48);
    }
    void RankingContent()
    {
        var list=screen=="board"?Edition.Leaderboard(CompetitiveEdition.RulesKey)
            :Edition.History.runs.Where(r=>CompetitiveRanking.ValidRun(r)).OrderByDescending(r=>r.completedUtc).ToList();
        page=Mathf.Clamp(page,0,Math.Max(0,(list.Count-1)/6));
        Label(content,"Rank heading",screen=="board"?"RANK":"RUN",-410,224,80,36,24,LobbyMenuStyle.Secondary);
        Label(content,"Player heading","PLAYER",-155,224,410,36,24,LobbyMenuStyle.Secondary);
        Label(content,"Score heading","SCORE",218,224,135,36,24,LobbyMenuStyle.Secondary,TextAlignmentOptions.MidlineRight);
        Label(content,"Time heading","TIME",378,224,140,36,24,LobbyMenuStyle.Secondary,TextAlignmentOptions.MidlineRight);
        for(int i=0;i<Math.Min(6,list.Count-page*6);i++)
        {
            var run=list[page*6+i];float y=158-i*67;
            LobbyMenuStyle.Rect(content,"Ranking row",0,y,900,60,i%2==0?LobbyMenuStyle.Surface:LobbyMenuStyle.Background);
            Label(content,"Rank",(page*6+i+1).ToString("00"),-410,y,70,54,29,LobbyMenuStyle.Gold);
            var player=Label(content,"Player",run.username+(run.isDemo?" [DEMO]":""),-155,y,410,60,28,LobbyMenuStyle.Text);
            if(player.GetPreferredValues(player.text,410,Mathf.Infinity).y>54) player.fontSize=24;
            Label(content,"Score",run.score.ToString("0.##")+" / 100",218,y,135,54,28,LobbyMenuStyle.Text,TextAlignmentOptions.MidlineRight);
            Label(content,"Time",CompetitiveRanking.FormatTime(run.seconds),378,y,140,54,28,LobbyMenuStyle.Text,TextAlignmentOptions.MidlineRight);
        }
        if(list.Count==0)Label(content,"Empty history","No completed investigations yet.\nFinish a case to see your score and time here.",0,20,860,150,30,LobbyMenuStyle.Text,TextAlignmentOptions.Center);
        string note=screen=="board"&&Edition.ShowDemoRankings?"DEMO entries are sample opponents.":Edition.StorageMessage??"Saved on this headset.";
        Label(content,"Ranking note","Page "+(page+1)+" / "+Math.Max(1,(list.Count+5)/6)+"  |  "+note,0,-252,900,44,24,LobbyMenuStyle.Secondary,TextAlignmentOptions.Center);
        Button("Previous",-310,-319,270,()=>{page=Math.Max(0,page-1);Draw();},64);
        Button("Back to menu",0,-319,270,Home,64);
        Button("Next",310,-319,270,()=>{page++;Draw();},64);
    }
    void Type(char key){if(username.Length<20)username+=key;nameError=null;Draw(true);}
    void Begin()
    {
        if(!Edition.SetUsername(username)){nameError="Start with a letter or number. Use 2-20 characters.";Draw(true);return;}
        ReleaseMenu();SceneManager.LoadScene("CSI_Environment");
    }
    void Home(){screen="home";nameError=null;Draw();}
    void OpenSettings()
    {
        panel.SetActive(false);pointer.Disable();ReleaseMenu();settings=FindAnyObjectByType<SettingsPauseMenu>();
        if(settings==null)settings=new GameObject("Competitive game settings").AddComponent<SettingsPauseMenu>();
        settings.Show();settingsOpen=true;
    }
    void Highlight()
    {
        for(int i=0;i<buttons.Count;i++)
        {
            bool focused=i==selected;var button=buttons[i];
            button.image.color=button.primary?LobbyMenuStyle.Gold:focused?new Color(.16f,.25f,.34f,1):LobbyMenuStyle.Surface;
            button.label.color=button.primary?LobbyMenuStyle.Ink:LobbyMenuStyle.Text;
            var focus=button.image.transform.Find("Focus");
            focus.GetComponent<Image>().color=button.primary?LobbyMenuStyle.Ink:LobbyMenuStyle.Gold;
            focus.gameObject.SetActive(focused);
        }
    }
    void Activate(int index)
    {
        if(Time.unscaledTime<inputAfter||index<0||index>=buttons.Count||PlayerUIGate.IsBlockedByOther(this))return;
        inputAfter=Time.unscaledTime+.2f;buttons[index].action();
    }
    void Update()
    {
        if(SceneManager.GetActiveScene().name!="Main"){Destroy(this);return;}if(panel==null)return;
        if(settingsOpen)
        {
            if(settings!=null&&settings.IsOpen)return;
            settingsOpen=false;panel.SetActive(true);HoldMenu();pointer.Enable();Draw();
        }
        pointer.Tick();if(back.WasPressedThisFrame()&&screen!="home")Home();
        Vector2 move=navigate.ReadValue<Vector2>();
        if(move.magnitude>.65f&&Time.unscaledTime>=nextMove&&buttons.Count>0)
        {
            int step=Mathf.Abs(move.x)>Mathf.Abs(move.y)?(move.x>0?1:-1):(move.y>0?-1:1);
            selected=(selected+step+buttons.Count)%buttons.Count;nextMove=Time.unscaledTime+.18f;Highlight();
        }
        if(PlayerUIGate.TryConsumeConfirm(this,confirm))Activate(selected);
    }
    void OnDestroy(){ReleaseMenu();pointer?.Dispose();navigate?.Dispose();confirm?.Dispose();back?.Dispose();if(panel!=null)Destroy(panel);}
    void HoldMenu(){PlayerUIGate.Enter(this);if(!holdsLocomotion){LocomotionSuspender.Suspend();holdsLocomotion=true;}}
    void ReleaseMenu(){PlayerUIGate.Exit(this);if(holdsLocomotion){LocomotionSuspender.Resume();holdsLocomotion=false;}}
}
