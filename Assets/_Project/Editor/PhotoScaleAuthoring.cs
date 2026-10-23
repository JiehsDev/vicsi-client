using System.IO;
using System.Linq;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Read-only scene inspection for the camera's assisted scale layout.</summary>
public static class PhotoScaleAuthoring
{
    public static void Inspect()
    {
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/Scenarios/CSI_Environment.unity");
        var text = new StringBuilder();
        foreach (var prop in Object.FindObjectsByType<EvidenceProp>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(p => new[] { "EVD-014", "EVD-015", "EVD-018", "BODY-A" }.Contains(p.evidenceId)))
        {
            text.AppendLine(prop.evidenceId + " position=" + prop.transform.position.ToString("F4") + " rotation=" + prop.transform.eulerAngles);
            foreach (var renderer in prop.GetComponentsInChildren<Renderer>(true))
                text.AppendLine(" renderer " + renderer.name + " active=" + renderer.gameObject.activeInHierarchy + " enabled=" + renderer.enabled
                    + " center=" + renderer.bounds.center.ToString("F4") + " size=" + renderer.bounds.size.ToString("F4"));
            foreach (var collider in prop.GetComponentsInChildren<Collider>(true))
                text.AppendLine(" collider " + collider.name + " trigger=" + collider.isTrigger + " center=" + collider.bounds.center.ToString("F4") + " size=" + collider.bounds.size.ToString("F4"));
        }
        Directory.CreateDirectory("docs/evidence/photo-scale");
        File.WriteAllText("docs/evidence/photo-scale/geometry-before.txt", text.ToString());
    }
}
