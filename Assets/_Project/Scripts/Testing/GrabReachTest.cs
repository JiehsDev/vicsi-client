// Assets/_Project/Scripts/Testing/GrabReachTest.cs
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Oculus.Interaction;
using UnityEngine;

/// <summary>
/// Regression for "ready evidence jumps into my hand from across the room": the SDK's
/// controller GrabInteractor has no distance limit of its own, so RightHandOnlyFilter now
/// also requires the right hand's grab point to be within reach of the item. Positions the
/// real ControllerGrabInteractors' grab points around the knife and bottle and asks each
/// item's GrabInteractable whether it can be selected. The editor has no tracked controllers,
/// so the interactor branches are forced active for the duration (restored afterwards).
///
/// Run from the editor via:
///     FindFirstObjectByType&lt;GrabReachTest&gt;().RunAndLog()
/// </summary>
public class GrabReachTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }

    private StringBuilder report;
    private int failures;

    public void RunAndLog() => StartCoroutine(Run());

    private void Check(string label, bool ok)
    {
        report.AppendLine($"  {(ok ? "PASS" : "FAIL")}: {label}");
        if (!ok) failures++;
    }

    private IEnumerator Run()
    {
        IsDone = false;
        failures = 0;
        report = new StringBuilder("=== GrabReachTest ===\n");
        yield return null;

        GrabInteractor right = null, left = null;
        foreach (var gi in FindObjectsByType<GrabInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            string side = Side(gi.transform);
            if (side == "Right") right = gi;
            else if (side == "Left") left = gi;
        }
        var knife = Find("EVD-014");
        var bottle = Find("EVD-018");
        if (right == null || left == null || knife == null || bottle == null)
        {
            Finish($"FAIL: missing pieces (right={right != null}, left={left != null}, knife={knife != null}, bottle={bottle != null}).");
            yield break;
        }

        // Editor: no tracked controllers, so the controller interactor branches are inactive.
        var forced = new List<GameObject>();
        foreach (var gi in new[] { right, left })
        {
            for (var t = gi.transform; t != null; t = t.parent)
            {
                if (!t.gameObject.activeSelf) { t.gameObject.SetActive(true); forced.Add(t.gameObject); }
            }
        }
        // Both items "ready for collection" at once: switch their grab on exactly as
        // EvidenceGrabGate does (restored afterwards). Interactables register a frame later.
        var setGrab = typeof(EvidenceGrabGate).GetMethod("SetGrabEnabled", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var gates = new[] { knife.GetComponent<EvidenceGrabGate>(), bottle.GetComponent<EvidenceGrabGate>() };
        foreach (var g in gates) setGrab.Invoke(g, new object[] { true });
        yield return null;
        yield return null;

        var knifeGrab = knife.GetComponent<GrabInteractable>();
        var bottleGrab = bottle.GetComponent<GrabInteractable>();
        var knifeFilter = knife.GetComponent<RightHandOnlyFilter>();
        var bottleFilter = bottle.GetComponent<RightHandOnlyFilter>();
        Check($"both items grab-enabled (ready) for the test [knife={knifeGrab.enabled}, bottle={bottleGrab.enabled}]", knifeGrab.enabled && bottleGrab.enabled);
        Check($"both items carry the reach filter (reach {knifeFilter?.MaxReachDistance:F2} m)", knifeFilter != null && bottleFilter != null);

        Transform rightPoint = right.Rigidbody.transform, leftPoint = left.Rigidbody.transform;
        Vector3 rightHome = rightPoint.position, leftHome = leftPoint.position;
        Vector3 knifeSurface = SurfacePoint(knife), bottleSurface = SurfacePoint(bottle);

        rightPoint.position = knifeSurface + Vector3.up * 0.05f;
        Check($"right hand touching the knife (~5 cm): knife grabbable [{knifeFilter.DistanceToSurface(rightPoint.position):F2} m]", knifeGrab.CanBeSelectedBy(right));
        Check("  ... and the bottle (other ready item, metres away) is NOT", !bottleGrab.CanBeSelectedBy(right));

        rightPoint.position = knifeSurface + Vector3.up * 0.3f;
        Check($"right hand 30 cm from the knife: not grabbable [{knifeFilter.DistanceToSurface(rightPoint.position):F2} m]", !knifeGrab.CanBeSelectedBy(right));

        rightPoint.position = knifeSurface + Vector3.up * 2f;
        Check("right hand 2 m away: not grabbable", !knifeGrab.CanBeSelectedBy(right));

        rightPoint.position = new Vector3(0f, 1.2f, 0f);
        Check($"right hand at the scene entrance (~{Vector3.Distance(rightPoint.position, knife.transform.position):F0} m): neither item grabbable",
            !knifeGrab.CanBeSelectedBy(right) && !bottleGrab.CanBeSelectedBy(right));

        rightPoint.position = bottleSurface + Vector3.right * 0.05f;
        Check("right hand touching the bottle: bottle grabbable, knife not", bottleGrab.CanBeSelectedBy(right) && !knifeGrab.CanBeSelectedBy(right));

        leftPoint.position = knifeSurface + Vector3.up * 0.05f;
        Check("left hand touching the knife: still refused (left hand holds the bag)", !knifeGrab.CanBeSelectedBy(left));

        foreach (var g in gates) setGrab.Invoke(g, new object[] { false });
        rightPoint.position = rightHome;
        leftPoint.position = leftHome;
        foreach (var go in forced) go.SetActive(false);
        Finish(failures == 0 ? "=== PASS, 0 failures ===" : $"=== FAIL: {failures} failure(s) ===");
    }

    private static string Side(Transform t)
    {
        for (; t != null; t = t.parent)
        {
            if (t.name.Contains("InteractorsRight")) return "Right";
            if (t.name.Contains("InteractorsLeft")) return "Left";
        }
        return null;
    }

    private static GameObject Find(string evidenceId)
    {
        foreach (var p in FindObjectsByType<EvidenceProp>(FindObjectsSortMode.None))
        {
            if (p.evidenceId == evidenceId && p.GetComponent<GrabInteractable>() != null) return p.gameObject;
        }
        return null;
    }

    // A point on the item's top surface (its solid collider), from above.
    private static Vector3 SurfacePoint(GameObject item)
    {
        var col = item.GetComponent<Collider>();
        Vector3 above = item.transform.position + Vector3.up * 1f;
        return col != null ? col.ClosestPoint(above) : item.transform.position;
    }

    private void Finish(string last)
    {
        report.AppendLine(last);
        Report = report.ToString();
        Debug.Log(Report);
        IsDone = true;
    }
}
