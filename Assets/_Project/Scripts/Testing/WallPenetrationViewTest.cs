using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text;
using Oculus.Interaction;
using Oculus.Interaction.Locomotion;
using UnityEngine;

public sealed class WallPenetrationViewTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }
    readonly StringBuilder report = new("=== WallPenetrationViewTest ===\n");
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    int failures;
    void Check(string name, bool pass) { report.AppendLine((pass ? "PASS: " : "FAIL: ") + name); if (!pass) failures++; }
    static object Get(object owner, string name) => owner.GetType().GetField(name, Private).GetValue(owner);
    static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Private).SetValue(owner, value);
    public void RunAllTests() => StartCoroutine(Run());
    IEnumerator Run()
    {
        AutomatedTestSandbox.Activate(nameof(WallPenetrationViewTest));
        RelationshipReviewTestUtil.BeginCaseFixture();
        yield return null;
        var guard = FindAnyObjectByType<WallPenetrationViewGuard>();
        var original = FindAnyObjectByType<WallPenetrationTunneling>();
        Check("Saved filtered guard exists; broad SDK query is disabled", guard != null && original != null && !original.enabled);
        if (guard == null || original == null) { Finish(); yield break; }
        var effect = (TunnelingEffect)Get(guard, "effect");
        var renderer = (MeshRenderer)Get(guard, "maskRenderer");
        Check("Wall mask starts hidden in the saved scene", !effect.enabled && !renderer.enabled);
        Check("Movement tunneling remains separate", FindObjectsByType<TunnelingEffect>(FindObjectsInactive.Include, FindObjectsSortMode.None).Any(e => e != effect));
        var oldTracked = (Transform)Get(guard, "trackedHead"); var oldLogical = (Transform)Get(guard, "logicalHead"); var oldRoot = (Transform)Get(guard, "playerRoot");
        var sourceTracked = (Transform)Get(original, "_trackedPosition"); var sourceLogical = (Transform)Get(original, "_logicalPosition");
        bool oldTriggerQueries = Physics.queriesHitTriggers;
        var root = new GameObject("Wall view regression fixtures"); root.transform.position = new Vector3(1000, 1000, 1000);
        var logical = new GameObject("Logical head").transform; logical.SetParent(root.transform, false);
        var tracked = new GameObject("Tracked head").transform; tracked.SetParent(root.transform, false); tracked.localPosition = Vector3.forward;
        var player = new GameObject("Player fixture").transform; player.SetParent(root.transform, false);
        Set(guard, "trackedHead", tracked); Set(guard, "logicalHead", logical); Set(guard, "playerRoot", player);
        original.InjectTrackedPosition(tracked); original.InjectLogicalPosition(logical);
        Physics.queriesHitTriggers = true;
        bool Hit() => guard.TryGetPenetration(logical.position, tracked.position, out _, out _);
        bool SDKHit() => (bool)typeof(WallPenetrationTunneling).GetMethod("CalculatePenetration", Private).Invoke(original, new object[] { 0f });
        GameObject Box(string name, float z)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(root.transform, false);
            go.transform.localPosition = new Vector3(0, 0, z); go.transform.localScale = Vector3.one * .1f; return go;
        }
        try
        {
            Physics.SyncTransforms(); guard.Refresh();
            Check("Clear space leaves the mask and renderer off", !Hit() && !effect.enabled && !renderer.enabled);
            var trigger = Box("Interaction trigger", .5f); trigger.GetComponent<Collider>().isTrigger = true;
            Physics.SyncTransforms();
            Check("Regression reproduced: original SDK treats interaction trigger as wall", SDKHit());
            Check("Interaction triggers cannot activate the filtered mask", !Hit());
            trigger.SetActive(false);
            var panel = new GameObject("World UI panel", typeof(RectTransform), typeof(Canvas), typeof(BoxCollider)); panel.transform.SetParent(root.transform, false);
            panel.transform.localPosition = Vector3.forward * .5f; panel.GetComponent<BoxCollider>().size = Vector3.one * .1f;
            Physics.SyncTransforms();
            Check("Regression reproduced: original SDK treats default-layer world UI as wall", SDKHit());
            guard.Refresh(); Check("Canvas UI cannot block view even with a solid collider", !Hit() && !renderer.enabled);
            panel.SetActive(false);
            var barrier = Box("Invisible procedure barrier", .5f); barrier.AddComponent<HousePhotographyEntryGate>();
            Physics.SyncTransforms();
            Check("Invisible barrier remains physically solid", !barrier.GetComponent<Collider>().isTrigger && barrier.GetComponent<Collider>().enabled);
            Check("Invisible photography barrier cannot activate the mask", !Hit()); barrier.SetActive(false);
            var hand = Box("Player-owned collider", .5f); hand.transform.SetParent(player, true); Physics.SyncTransforms();
            Check("Untagged player/held-tool hierarchy cannot activate the mask", !Hit()); hand.SetActive(false);
            var ahead = Box("Real wall just beyond tracked head", 1.15f); Physics.SyncTransforms();
            Check("Regression reproduced: SDK look-ahead masks a wall beyond the head", SDKHit());
            Check("A wall not crossed by the head does not obscure view", !Hit()); ahead.SetActive(false);
            var wall = Box("Real wall crossing", .5f); Physics.SyncTransforms(); guard.Refresh();
            Check("Genuine head crossing is detected", Hit());
            Check("Real walls still activate the comfort renderer", effect.enabled && renderer.enabled && effect.UserFOV < 360);
            wall.transform.localPosition = Vector3.forward * .8f;
            var nearer = Box("Nearest real wall", .3f); Physics.SyncTransforms();
            Check("Nearest valid surface supplies stable penetration depth", guard.TryGetPenetration(logical.position, tracked.position, out float depth, out var found) && found == nearer.GetComponent<Collider>() && Mathf.Abs(depth - .75f) < .005f);
            wall.SetActive(false); nearer.SetActive(false); Physics.SyncTransforms(); guard.Refresh();
            Check("Leaving a wall immediately clears the mask and mesh", !effect.enabled && !renderer.enabled && effect.UserFOV == 360);
            tracked.position = logical.position; guard.Refresh();
            Check("Coincident logical/tracked head cannot leave a stale mask", !Hit() && !renderer.enabled);
            renderer.enabled = true; guard.enabled = false;
            Check("Disabling the guard clears even a stale enabled mesh", !renderer.enabled && !effect.enabled);
            guard.enabled = true; guard.Refresh();
            Check("Guard re-enables cleanly without a phantom wall", !renderer.enabled && !effect.enabled);
        }
        finally
        {
            Set(guard, "trackedHead", oldTracked); Set(guard, "logicalHead", oldLogical); Set(guard, "playerRoot", oldRoot);
            original.InjectTrackedPosition(sourceTracked); original.InjectLogicalPosition(sourceLogical);
            Physics.queriesHitTriggers = oldTriggerQueries; Destroy(root); guard.Refresh();
        }
        Finish();
    }
    void Finish() { report.AppendLine(failures == 0 ? "=== PASS, 0 failures ===" : "=== FAIL: " + failures + " failure(s) ==="); Report = report.ToString(); IsDone = true; }
}
