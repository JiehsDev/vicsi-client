// Assets/_Project/Scripts/Assessment/StudentCodeKeypad.cs
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ray-clickable student-code keypad for the lobby code screen. The Quest system keyboard
/// does not open for this app's text field (GameActivity text input), so the code is typed
/// on these keys instead - every code character is on the pad (the code alphabet has no
/// 0/O, 1/I or L), plus Del and Clear.
///
/// Scene-authored keys: each Button under keysRoot named "Key_&lt;character&gt;", "Key_Del" or
/// "Key_Clear". Text is shown grouped like the printed code (K7M4-PQ2D); the identification
/// flow normalizes it, so the hyphen is cosmetic. The text field itself is display-only,
/// so pointing at it never tries to summon the system keyboard.
/// </summary>
public class StudentCodeKeypad : MonoBehaviour
{
    public const string CodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    private const int CodeLength = 8;
    private const int GroupSize = 4;

    [SerializeField] private TMP_InputField codeInputField;
    [SerializeField] private Transform keysRoot;

    private void Awake()
    {
        if (codeInputField != null)
        {
            codeInputField.readOnly = true;
            codeInputField.shouldHideSoftKeyboard = true;
            foreach (var graphic in codeInputField.GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = false;
            }
        }

        if (keysRoot == null) return;
        foreach (var button in keysRoot.GetComponentsInChildren<Button>(true))
        {
            string name = button.gameObject.name;
            if (!name.StartsWith("Key_")) continue;
            string key = name.Substring(4);
            if (key == "Del") button.onClick.AddListener(Backspace);
            else if (key == "Clear") button.onClick.AddListener(Clear);
            else if (key.Length == 1) { char c = key[0]; button.onClick.AddListener(() => Append(c)); }
        }
    }

    /// <summary>Adds one code character (ignored once the code is complete or for characters codes never use).</summary>
    public void Append(char c)
    {
        c = char.ToUpperInvariant(c);
        if (CodeAlphabet.IndexOf(c) < 0) return;
        string raw = RawCode();
        if (raw.Length >= CodeLength) return;
        SetRaw(raw + c);
    }

    public void Backspace()
    {
        string raw = RawCode();
        if (raw.Length > 0) SetRaw(raw.Substring(0, raw.Length - 1));
    }

    public void Clear() => SetRaw(string.Empty);

    /// <summary>The typed characters without the display hyphen.</summary>
    public string RawCode()
    {
        if (codeInputField == null || string.IsNullOrEmpty(codeInputField.text)) return string.Empty;
        var sb = new StringBuilder(CodeLength);
        foreach (char c in codeInputField.text)
        {
            if (c != '-' && c != ' ') sb.Append(char.ToUpperInvariant(c));
        }
        return sb.ToString();
    }

    private void SetRaw(string raw)
    {
        if (codeInputField == null) return;
        codeInputField.text = raw.Length > GroupSize ? raw.Substring(0, GroupSize) + "-" + raw.Substring(GroupSize) : raw;
    }
}
