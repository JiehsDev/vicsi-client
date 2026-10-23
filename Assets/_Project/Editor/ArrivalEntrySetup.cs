#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using PlayerController = Oculus.Interaction.Locomotion.CharacterController;

/// <summary>Prepare a reviewable scene copy, or explicitly apply the validated arrival setup.</summary>
public static class ArrivalEntrySetup
{
    const string Output = "docs/evidence/arrival-entry";
    [Serializable] sealed class Result
    {
        public Vector3 originalRig, arrivalRig, originalHotspot, arrivalHotspot, entry, responder, sign;
        public float arrivalYaw, responderAngle, entryAngle, groundHeight;
        public bool standingClear, footprintSupported, outsideProtectedArea, outsideEntryApproach, compilationFailed;
        public string[] changedExistingLocalTransforms, standingObstructions;
    }
    public static void Prepare() => Run(false);
    public static void Apply() => Run(true);
    static void Run(bool apply)
    {
        Directory.CreateDirectory(Output);
        try
        {
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Scenarios/CSI_Environment.unity");
            var transforms = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(t => t.gameObject.scene == scene).ToArray();
            var before = transforms.ToDictionary(t => t, t => (t.localPosition, t.localRotation, t.localScale));
            var rig = scene.GetRootGameObjects().Single(g => g.name == "[BuildingBlock] Camera Rig").transform;
            var hotspot = scene.GetRootGameObjects().Single(g => g.name == "Teleport Hotspot").transform;
            var cordon = UnityEngine.Object.FindAnyObjectByType<SceneCordon>();
            var responder = UnityEngine.Object.FindAnyObjectByType<FirstResponderTurnover>().transform;
            var left = cordon.transform.Find("EntrancePost_L"); var right = cordon.transform.Find("EntrancePost_R");
            var controller = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
            int mask = controller.LayerMask.value & ~(1 << 2);
            var result = new Result { originalRig = rig.position, originalHotspot = hotspot.position, responder = responder.position,
                compilationFailed = EditorUtility.scriptCompilationFailed };
            Vector3 entry = (left.position + right.position) * .5f;
            Vector3 across = right.position - left.position; across.y = 0;
            Vector3 inward = Vector3.Cross(across.normalized, Vector3.up);
            var cordonData = new SerializedObject(cordon);
            var protectedArea = cordonData.FindProperty("protectedArea").boundsValue;
            if (Vector3.Dot(cordon.transform.TransformPoint(protectedArea.center) - entry, inward) < 0) inward = -inward;
            var arrival = entry - inward * .75f;
            var oldFloor = Ground(result.originalRig, rig, mask);
            var floor = Ground(arrival, rig, mask);
            arrival.y = floor.y + result.originalRig.y - oldFloor.y;
            Vector3 toResponder = responder.position - arrival; toResponder.y = 0;
            Vector3 toEntry = entry - arrival; toEntry.y = 0;
            var facing = Quaternion.LookRotation(toResponder.normalized + toEntry.normalized);
            rig.SetPositionAndRotation(arrival, facing);
            hotspot.SetPositionAndRotation(new Vector3(arrival.x, floor.y, arrival.z), facing);
            // The existing protected area begins deeper into the yard. Add only a warning strip
            // between the visible entry tape and that existing boundary; no collider or phase gate.
            Vector3 localEntry = cordon.transform.InverseTransformPoint(entry);
            var approach = new Bounds();
            approach.SetMinMax(new Vector3(localEntry.x - across.magnitude * .5f, -.5f, localEntry.z),
                new Vector3(localEntry.x + across.magnitude * .5f, 3.5f, protectedArea.min.z));
            cordonData.FindProperty("controlledEntryApproach").boundsValue = approach; cordonData.ApplyModifiedPropertiesWithoutUndo();
            if (cordon.transform.Find("Controlled Entry Point Sign") != null) throw new InvalidOperationException("Arrival sign already exists; refusing to duplicate it.");
            var sign = new GameObject("Controlled Entry Point Sign", typeof(RectTransform), typeof(Canvas));
            sign.layer = 2; sign.transform.SetParent(cordon.transform, false);
            sign.transform.position = right.position + Vector3.up * .66f - inward * .06f;
            var towardSign = sign.transform.position - arrival; towardSign.y = 0;
            sign.transform.rotation = Quaternion.LookRotation(towardSign);
            sign.transform.localScale = Vector3.one * .002f;
            var rect = (RectTransform)sign.transform; rect.sizeDelta = new Vector2(320,120);
            var canvas = sign.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            Image(sign.transform, "Frame", new Vector2(320,120), new Color(.95f,.72f,.12f,1));
            Image(sign.transform, "Backing", new Vector2(306,106), new Color(.025f,.05f,.075f,1));
            var labelObject = new GameObject("Controlled entry label", typeof(RectTransform), typeof(Text)); labelObject.layer = 2;
            labelObject.transform.SetParent(sign.transform, false); ((RectTransform)labelObject.transform).sizeDelta = new Vector2(294,100);
            var label = labelObject.GetComponent<Text>(); label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = "Controlled\nEntry Point"; label.fontSize = 30; label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter; label.color = Color.white; label.raycastTarget = false;
            result.arrivalRig = rig.position; result.arrivalHotspot = hotspot.position; result.entry = entry; result.sign = sign.transform.position;
            result.arrivalYaw = rig.eulerAngles.y; result.groundHeight = floor.y;
            result.responderAngle = Vector3.Angle(rig.forward, toResponder); result.entryAngle = Vector3.Angle(rig.forward, toEntry);
            result.outsideProtectedArea = !cordon.Contains(floor + Vector3.up * 1.6f);
            result.outsideEntryApproach = !cordon.RequiresBriefingAt(floor + Vector3.up * 1.6f);
            float radius = controller.Radius;
            var overlaps = Physics.OverlapCapsule(floor + Vector3.up * (radius + .02f), floor + Vector3.up * (1.7f - radius - .02f),
                radius, mask, QueryTriggerInteraction.Ignore).Where(c => !c.transform.IsChildOf(rig)).ToArray();
            result.standingClear = overlaps.Length == 0; result.standingObstructions = overlaps.Select(c => PathOf(c.transform)).ToArray();
            result.footprintSupported = true;
            for (int i=0;i<16;i++)
            {
                float angle = 2 * Mathf.PI * i / 16;
                var at = floor + new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius);
                result.footprintSupported &= Mathf.Abs(Ground(at, rig, mask).y - floor.y) < .04f;
            }
            result.changedExistingLocalTransforms = transforms.Where(t =>
            {
                var prior = before[t];
                return t.localPosition != prior.Item1 || Quaternion.Angle(t.localRotation, prior.Item2) > .001f || t.localScale != prior.Item3;
            }).Select(PathOf).ToArray();
            File.WriteAllText(Output + "/setup.json", JsonUtility.ToJson(result,true));
            if (result.compilationFailed || !result.standingClear || !result.footprintSupported || !result.outsideProtectedArea || !result.outsideEntryApproach
                || result.responderAngle > 30 || result.entryAngle > 30 || result.changedExistingLocalTransforms.Length != 2
                || result.changedExistingLocalTransforms.Any(p => p != PathOf(rig) && p != PathOf(hotspot)))
                throw new InvalidOperationException("Arrival setup validation failed; inspect setup.json.");
            if (!EditorSceneManager.SaveScene(scene, Output + "/candidate.unity", true)) throw new IOException("Could not save review scene copy.");
            if (apply && !EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save validated arrival layout.");
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); File.WriteAllText(Output + "/setup-error.txt", error.ToString()); EditorApplication.Exit(1); }
    }
    static Vector3 Ground(Vector3 position, Transform rig, int mask)
    {
        var hits = Physics.RaycastAll(new Vector3(position.x, 2.5f, position.z), Vector3.down, 4f, mask, QueryTriggerInteraction.Ignore)
            .Where(h => h.normal.y > .9f && !h.collider.transform.IsChildOf(rig)).OrderByDescending(h => h.point.y).ToArray();
        if (hits.Length == 0) throw new InvalidOperationException("No floor support at " + position);
        return hits[0].point;
    }
    static void Image(Transform parent, string name, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.layer = 2; go.transform.SetParent(parent,false);
        ((RectTransform)go.transform).sizeDelta = size; var image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = false;
    }
    static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
}
#endif
