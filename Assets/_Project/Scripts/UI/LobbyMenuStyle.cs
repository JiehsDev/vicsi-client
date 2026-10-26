using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Lobby-only presentation: a clear entry action, readable type and a consistent visual hierarchy.</summary>
public static class LobbyMenuStyle
{
    public static readonly Color Background = new(.035f,.065f,.095f,1);
    public static readonly Color Surface = new(.075f,.12f,.165f,1);
    public static readonly Color Text = new(.95f,.97f,.99f,1);
    public static readonly Color Secondary = new(.72f,.80f,.86f,1);
    public static readonly Color Gold = new(.91f,.76f,.43f,1);
    public static readonly Color Ink = new(.045f,.075f,.10f,1);
    public static readonly Color Line = new(.20f,.28f,.35f,1);

    public static TMP_Text Label(Transform parent, string name, string value, float x, float y,
        float width, float height, float size, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
    {
        var text = ForensicUIStyle.Text(parent,name,size,color,alignment:alignment);
        Place(text.rectTransform,x,y,width,height);
        text.text=value; text.richText=false; text.enableAutoSizing=false;
        text.overflowMode=TextOverflowModes.Ellipsis;
        return text;
    }
    public static void Place(RectTransform rect,float x,float y,float width,float height)
    {
        rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);
        rect.localScale=Vector3.one; rect.localRotation=Quaternion.identity;
        rect.anchoredPosition3D=new Vector3(x,y,0); rect.sizeDelta=new Vector2(width,height);
    }
    public static Image Rect(Transform parent,string name,float x,float y,float width,float height,Color color)
    {
        var image=ForensicUIStyle.Rect(parent,name,color);
        Place(image.rectTransform,x,y,width,height); return image;
    }
    public static void Frame(Transform parent)
    {
        Rect(parent,"Lobby background",0,0,1000,900,Background);
        Rect(parent,"Header accent",-416,438,68,4,Gold);
        Rect(parent,"Header rule",0,278,900,1,Line);
        Rect(parent,"Footer rule",0,-370,900,1,Line);
    }
    public static void HomeIntro(Transform parent)
    {
        Label(parent,"Welcome heading","Ready to investigate?",-190,210,520,54,36,Text);
        Label(parent,"Welcome detail","Explore the scene, document evidence,\nand explain what happened.",-190,145,520,78,28,Secondary);
    }
    public static void Guide(Transform parent)
    {
        Rect(parent,"Guide divider",105,-30,1,540,Line);
        Label(parent,"Guide heading","YOUR INVESTIGATION",290,214,290,36,24,Gold);
        Step(parent,1,"Collect","Photograph and process\neach piece of evidence.",155);
        Step(parent,2,"Connect","Compare the evidence\nand witness statements.",12);
        Step(parent,3,"Conclude","Explain what happened\nand close the case.",-131);
    }
    static void Step(Transform parent,int number,string title,string detail,float y)
    {
        Label(parent,"Guide step "+number,number.ToString("00")+"  "+title,290,y,290,40,29,Text);
        Label(parent,"Guide detail "+number,detail,290,y-56,290,76,26,Secondary);
    }
    public static void Button(Button button,bool primary)
    {
        var image=button.image; image.sprite=null; image.type=Image.Type.Simple;
        image.color=primary?Gold:Surface;
        var colors=button.colors; colors.normalColor=Color.white;
        colors.highlightedColor=new Color(1.15f,1.15f,1.15f,1);
        colors.selectedColor=colors.highlightedColor; colors.pressedColor=new Color(.80f,.85f,.9f,1);
        colors.disabledColor=new Color(.55f,.55f,.55f,1); button.colors=colors;
        foreach(var label in button.GetComponentsInChildren<TMP_Text>(true))
        {
            var rect=label.rectTransform; rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one;
            rect.pivot=new Vector2(.5f,.5f); rect.localScale=Vector3.one;
            rect.offsetMin=new Vector2(24,8); rect.offsetMax=new Vector2(-24,-8);
            label.fontSize=primary?32:27; label.enableAutoSizing=false;
            label.alignment=TextAlignmentOptions.Center; label.color=primary?Ink:Text;
            label.overflowMode=TextOverflowModes.Ellipsis;
        }
    }
}
