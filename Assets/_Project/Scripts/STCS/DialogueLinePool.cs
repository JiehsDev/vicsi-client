// Assets/_Project/Scripts/STCS/DialogueLinePool.cs
using UnityEngine;

/// <summary>
/// A set of lines for one STCS trigger moment. When a trigger fires a single
/// random line (e.g. flavor-text greetings), use GetRandomLine(). When a
/// trigger plays every line in order (a sequence of distinct teammate
/// actions), STCSManager walks the lines array directly - see
/// STCSTrigger.playAllLinesInSequence.
/// </summary>
[CreateAssetMenu(fileName = "DialoguePool_", menuName = "VR-CSI/STCS/Dialogue Line Pool")]
public class DialogueLinePool : ScriptableObject
{
    [TextArea]
    public string[] lines;

    public string GetRandomLine()
    {
        if (lines == null || lines.Length == 0)
        {
            return string.Empty;
        }

        return lines[Random.Range(0, lines.Length)];
    }
}
