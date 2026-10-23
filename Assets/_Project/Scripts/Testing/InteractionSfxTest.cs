using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>Imported media, saved tool wiring, neutral UI audio and movement/menu cadence.</summary>
public sealed class InteractionSfxTest:MonoBehaviour
{
    public bool IsDone{get;private set;}
    public string Report{get;private set;}
    readonly StringBuilder report=new("=== InteractionSfxTest ===\n");int failures;
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    void Check(string name,bool pass){report.AppendLine((pass?"PASS: ":"FAIL: ")+name);if(!pass)failures++;}
    public void RunAllTests(){AutomatedTestSandbox.Activate(nameof(InteractionSfxTest));StartCoroutine(Run());}
    IEnumerator Run()
    {
        yield return null;
        RelationshipReviewTestUtil.BeginCaseFixture();
        var player=InteractionSfx.Instance;
        var library=Resources.Load<InteractionSfxLibrary>("InteractionSfxLibrary");
        Check("Saved audio player uses the shared editable library",player!=null&&player.Library==library&&library!=null);
        if(player==null||library==null){report.AppendLine("=== FAIL ===");Report=report.ToString();IsDone=true;yield break;}
        foreach(InteractionSound key in Enum.GetValues(typeof(InteractionSound)))
        {
            var clip=library.Clip(key);
            bool decoded=clip!=null&&clip.samples>0&&clip.channels==1;
            if(decoded)
            {
                var pcm=new float[Mathf.Min(clip.samples,8192)];
                decoded=clip.GetData(pcm,0)&&pcm.Any(value=>Mathf.Abs(value)>.0001f)&&pcm.All(value=>!float.IsNaN(value)&&Mathf.Abs(value)<=1);
            }
            Check(key+" has a non-silent decodable mono clip",decoded);
        }
        Check("Nine surface variants are assigned",library.concreteSteps.Length==3&&library.woodSteps.Length==3&&library.grassSteps.Length==3
            &&library.concreteSteps.Concat(library.woodSteps).Concat(library.grassSteps).All(c=>c!=null&&c.samples>0));
        Check("No zipper, door or cabinet sound category exists",!Enum.GetNames(typeof(InteractionSound)).Any(s=>s.Contains("Door")||s.Contains("Cabinet")||s.Contains("Zipper")));
        Check("Normal output levels remain restrained",library.uiVolume>0&&library.uiVolume<=.25f&&library.physicalVolume<=.4f&&library.footstepVolume<=.25f);
        Wired<PhotographTool>("shutterAudioSource","shutterClip",library.shutter);
        Wired<FlashlightTool>("beamAudioSource","beamClickClip",library.switchClick);
        Wired<ChalkMarkTool>("chalkAudioSource","chalkWritingClip",library.chalk);
        Wired<EvidenceBagTool>("audioSource","insertClip",library.paper);
        Wired<EvidenceTentTool>("audioSource","tentPlacingClip",library.place);
        Wired<SwabTool>("audioSource","containerCloseClip",library.container);
        var feedback=Object.FindAnyObjectByType<InteractionFeedback>();
        Check("Existing confirm/refusal feedback uses downloaded clips",(AudioClip)typeof(InteractionFeedback).GetField("confirmClip",Private).GetValue(feedback)==library.confirm
            &&(AudioClip)typeof(InteractionFeedback).GetField("blockedClip",Private).GetValue(feedback)==library.blocked);
        var source=(AudioSource)typeof(InteractionSfx).GetField("uiSource",Private).GetValue(player);
        Check("UI source is non-spatial and does not double attenuate clicks",source!=null&&source.spatialBlend==0&&source.volume==1&&!source.playOnAwake);
        yield return null;
        int before=player.PlaybackCount;InteractionSfx.Select();InteractionSfx.Select();
        Check("A/trigger paths share one neutral click per frame",player.PlaybackCount==before+1&&player.LastClip==library.select);
        var voices=(AudioSource[])typeof(InteractionSfx).GetField("spatialSources",Private).GetValue(player);
        foreach(var voice in voices)voice.Stop();
        int objects=Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include).Length;
        for(int i=0;i<30;i++)InteractionSfx.Play(InteractionSound.Place,new Vector3(1000,1,1000));
        Check("Repeated actions use eight bounded spatial voices without allocating sources",voices.Length==8
            &&Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include).Length==objects
            &&voices.All(v=>!v.playOnAwake&&v.spatialBlend==1&&v.dopplerLevel==0));
        foreach(var voice in voices)voice.Stop();
        var swab=SwabTool.Instance;
        var originalPreparation=swab.Preparation;
        try
        {
            typeof(SwabTool).GetProperty("Preparation").SetValue(swab,SwabTool.PreparationStage.Packaged);
            before=player.PlaybackCount;
            typeof(SwabTool).GetMethod("PlayContainerCloseSound",Private).Invoke(swab,null);
            Check("Prepared paper-package sealing plays tape instead of a plastic lid",player.PlaybackCount==before+1&&player.LastClip==library.tape);
        }
        finally{typeof(SwabTool).GetProperty("Preparation").SetValue(swab,originalPreparation);}
        foreach(var voice in voices)voice.Stop();
        var host=new GameObject("Isolated footstep cadence fixture");var steps=host.AddComponent<InteractionFootsteps>();
        var position=new Vector3(1000,1.7f,1000);
        Check("Initializing stationary footsteps stays silent",!steps.Advance(position,false,0)&&!steps.Advance(position,false,1));
        position+=Vector3.right*.7f;
        Check("Actual horizontal travel plays a step",steps.Advance(position,false,2));
        position+=Vector3.right*.7f;
        Check("Open menus suppress steps and reset cadence",!steps.Advance(position,true,3));
        Check("Closing a menu while stationary does not emit a delayed step",!steps.Advance(position,false,4));
        position+=Vector3.right*100;
        Check("Teleport travel stays silent",!steps.Advance(position,false,5));
        position+=Vector3.up*.5f;
        Check("Height changes alone do not play walking sounds",!steps.Advance(position,false,6));
        position+=Vector3.right*.7f;
        Check("Normal walking resumes after menu or teleport",steps.Advance(position,false,7));
        position+=Vector3.right*.7f;
        Check("Fast movement is rate-limited",!steps.Advance(position,false,7.01f));
        Check("Rate-limited travel cannot make stationary footsteps",!steps.Advance(position,false,8));
        Object.Destroy(host);
        var chalk=ChalkMarkTool.Instance;
        var chalkSource=(AudioSource)typeof(ChalkMarkTool).GetField("chalkAudioSource",Private).GetValue(chalk);
        typeof(ChalkMarkTool).GetMethod("SetWritingSoundPlaying",Private).Invoke(chalk,new object[]{true});
        ToolInputLock.Enter(this);
        try{typeof(ChalkMarkTool).GetMethod("Update",Private).Invoke(chalk,null);}
        finally{ToolInputLock.Exit(this);}
        Check("Opening a menu stops the chalk loop",chalkSource.loop&&!chalkSource.isPlaying);
        Check("Deduction remains 60/25/15",FinalDeductionAssessment.Rules.checkpointWeight==60&&FinalDeductionAssessment.Rules.relationshipWeight==25&&FinalDeductionAssessment.Rules.conclusionWeight==15);
        report.AppendLine(failures==0?"=== PASS ===":"=== FAIL ===");Report=report.ToString();IsDone=true;
    }
    void Wired<T>(string sourceField,string clipField,AudioClip expected)where T:MonoBehaviour
    {
        var owners=Object.FindObjectsByType<T>(FindObjectsInactive.Include);
        Check(typeof(T).Name+" has saved spatial audio and expected clip",owners.Length>0&&owners.All(owner=>
        {
            var source=(AudioSource)typeof(T).GetField(sourceField,Private).GetValue(owner);
            return source!=null&&!source.playOnAwake&&source.spatialBlend==1&&(AudioClip)typeof(T).GetField(clipField,Private).GetValue(owner)==expected;
        }));
    }
}
