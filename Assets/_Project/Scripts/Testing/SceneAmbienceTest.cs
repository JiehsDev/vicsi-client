using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

public sealed class SceneAmbienceTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }
    readonly StringBuilder report = new("=== SceneAmbienceTest ===\n");
    int failures;
    void Check(string name, bool pass) { report.AppendLine((pass ? "PASS: " : "FAIL: ") + name); if (!pass) failures++; }
    public void RunAllTests() { AutomatedTestSandbox.Activate(nameof(SceneAmbienceTest)); StartCoroutine(Run()); }
    IEnumerator Run()
    {
        yield return null;
        RelationshipReviewTestUtil.BeginCaseFixture();
        var ambience = FindAnyObjectByType<SceneAmbience>();
        Check("Exactly one scene-authored ambience controller", ambience != null && FindObjectsByType<SceneAmbience>().Length == 1);
        if (ambience == null) { Finish(); yield break; }
        var sources = new[] { ambience.Exterior, ambience.Interior, ambience.TreeRustle };
        Check("Three distinct saved sources and clips exist", sources.All(s => s != null && s.clip != null) && sources.Distinct().Count() == 3 && sources.Select(s => s.clip).Distinct().Count() == 3);
        if (sources.Any(s => s == null || s.clip == null)) { Finish(); yield break; }
        foreach (var source in sources)
        {
            Check(source.name + ": stereo streamed clip exceeds 15 seconds", source.clip.channels == 2 && source.clip.length > 15 && source.clip.loadType == AudioClipLoadType.Streaming);
            Check(source.name + ": diffuse stereo loop does not pre-empt tool voices", source.loop && !source.playOnAwake && source.spatialBlend == 0 && source.dopplerLevel == 0 && source.priority >= 200);
            Check(source.name + ": belongs to this scene/controller", source.transform.IsChildOf(ambience.transform) && source.gameObject.scene == ambience.gameObject.scene);
        }
        Check("Ambience owns no collision or UI geometry", ambience.GetComponentsInChildren<Collider>().Length == 0 && ambience.GetComponentsInChildren<Canvas>().Length == 0);
        var yard = new Vector3(16.9f, 1.6f, 3);
        var indoors = new Vector3(17.7f, 2.8f, 9.6f);
        var upperFloor = new Vector3(17.3f, 5.8f, 13);
        var wing = new Vector3(13.3f, 2.8f, 5.4f);
        void Settle(Vector3 point, bool dialogue = false) { for (int i = 0; i < 200; i++) ambience.Advance(point, dialogue, .1f); }
        Settle(yard);
        float outdoorLevel = ambience.Exterior.volume, windLevel = ambience.TreeRustle.volume;
        Check("Yard has restrained outdoor birds/rustle and no interior bed", ambience.InsideBlend < .001f && outdoorLevel > 0 && outdoorLevel <= .3f && windLevel > 0 && windLevel < outdoorLevel && ambience.Interior.volume < .001f);
        var filter = ambience.Exterior.GetComponent<AudioLowPassFilter>(); var windFilter = ambience.TreeRustle.GetComponent<AudioLowPassFilter>();
        Check("Exterior beds are full-band outside", filter != null && windFilter != null && filter.cutoffFrequency > 21000 && windFilter.cutoffFrequency > 21000);
        ambience.Advance(indoors, false, .1f);
        Check("Crossing doorway blends rather than instantly cutting tracks", ambience.InsideBlend > 0 && ambience.InsideBlend < .5f && ambience.Exterior.volume < outdoorLevel && ambience.Exterior.volume > outdoorLevel * .2f && ambience.Interior.volume > 0);
        Settle(indoors);
        float indoorLevel = ambience.Interior.volume;
        Check("Interior room tone becomes the main bed", ambience.InsideBlend > .999f && indoorLevel > 0 && indoorLevel <= .25f && ambience.Exterior.volume < outdoorLevel * .15f);
        Check("Outdoor beds are muffled through the house", filter.cutoffFrequency < 1300 && windFilter.cutoffFrequency < 1300);
        Settle(upperFloor); Check("Upper storey uses indoor ambience", ambience.InsideBlend > .999f);
        Settle(wing); Check("Projecting front wing uses indoor ambience", ambience.InsideBlend > .999f);
        Settle(indoors, true);
        Check("Dialogue smoothly ducks every ambience bed", ambience.DialogueGain > .5f && ambience.DialogueGain < .6f && ambience.Interior.volume < indoorLevel * .6f && ambience.Exterior.volume < outdoorLevel * .1f);
        Settle(indoors); Check("Closing dialogue restores the quiet mix", ambience.DialogueGain > .999f && Mathf.Abs(ambience.Interior.volume - indoorLevel) < .001f);
        int events = SessionLogger.Instance.EventCounts.Values.Sum();
        Settle(yard);
        Check("Returning outside restores outdoor mix and removes room tone", ambience.InsideBlend < .001f && Mathf.Abs(ambience.Exterior.volume - outdoorLevel) < .001f && ambience.Interior.volume < .001f);
        Check("Mixing creates no timeline or scoring events", SessionLogger.Instance.EventCounts.Values.Sum() == events);
        PlayerUIGate.Enter(this);
        try
        {
            typeof(SceneAmbience).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ambience, null);
            Check("Existing modal UI ownership triggers automatic ducking", ambience.DialogueGain < 1);
        }
        finally { PlayerUIGate.Exit(this); }
        float scale = Time.timeScale;
        try
        {
            Time.timeScale = 0;
            Settle(yard, true);
            Check("Mix transitions remain functional while scaled game time is paused", ambience.DialogueGain < .6f);
        }
        finally { Time.timeScale = scale; }
        ambience.enabled = false;
        Check("Disabling the scene controller stops all ambience voices", sources.All(s => !s.isPlaying && s.volume == 0));
        ambience.enabled = true;
        for (int i = 0; i < 10; i++) yield return null;
        Check("Re-enabling starts exactly the three existing loops", sources.All(s => s.isPlaying) && ambience.GetComponentsInChildren<AudioSource>().Length == 3);
        Check("Deduction remains 60/25/15", FinalDeductionAssessment.Rules.checkpointWeight == 60 && FinalDeductionAssessment.Rules.relationshipWeight == 25 && FinalDeductionAssessment.Rules.conclusionWeight == 15);
        Finish();
    }
    void Finish() { report.AppendLine(failures == 0 ? "=== PASS, 0 failures ===" : "=== FAIL: " + failures + " failure(s) ==="); Report = report.ToString(); IsDone = true; }
}
