// Assets/_Project/Scripts/RoleSystem/ToolWheelWedgeTemplate.cs
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Marks one wedge's editable pieces so ToolWheelController can clone it per role and
/// still find its parts afterward. Author this once in the Editor - sprite, fill
/// style, colors, icon size, font, whatever - via the "Build/Rebuild Wheel UI"
/// context menu action on ToolWheelController, then tweak the resulting WedgeTemplate
/// child freely. The wheel just clones it per role and swaps content/position/rotation
/// at runtime; it never touches your layout choices.
/// </summary>
public class ToolWheelWedgeTemplate : MonoBehaviour
{
    public Image wedge;
    public Image icon;
    public TMP_Text monogram;
}
