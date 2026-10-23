using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Bounded spatial voices and neutral UI clicks. No grading or procedure ownership.</summary>
public sealed class InteractionSfx : MonoBehaviour
{
    public static InteractionSfx Instance { get; private set; }
    [SerializeField] private InteractionSfxLibrary library;
    [SerializeField] private AudioSource uiSource;
    [SerializeField] private AudioSource[] spatialSources;
    private int uiFrame = -1;
    public InteractionSfxLibrary Library => library;
    public int PlaybackCount { get; private set; }
    public AudioClip LastClip { get; private set; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetBootstrap() { Instance=null; SceneManager.sceneLoaded-=SceneLoaded; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap() { SceneManager.sceneLoaded+=SceneLoaded; Ensure(); }
    static void SceneLoaded(Scene scene, LoadSceneMode mode) => Ensure();
    static void Ensure()
    {
        if (Instance!=null || Resources.Load<InteractionSfxLibrary>("InteractionSfxLibrary")==null) return;
        var existing=Object.FindAnyObjectByType<InteractionSfx>();
        if (existing==null) new GameObject("Interaction SFX").AddComponent<InteractionSfx>();
    }
    void Awake()
    {
        if (Instance!=null && Instance!=this) { enabled=false; return; }
        Instance=this;
        if (library==null) library=Resources.Load<InteractionSfxLibrary>("InteractionSfxLibrary");
        if (uiSource==null) uiSource=gameObject.AddComponent<AudioSource>();
        uiSource.playOnAwake=false; uiSource.spatialBlend=0;
        GameAudioSettings.RouteSource(uiSource);
        if (spatialSources==null || spatialSources.Length==0)
        {
            spatialSources=new AudioSource[8];
            for(int i=0;i<spatialSources.Length;i++)
            {
                var voice=new GameObject("Spatial SFX "+(i+1)); voice.transform.SetParent(transform,false);
                spatialSources[i]=voice.AddComponent<AudioSource>();
            }
        }
        foreach(var source in spatialSources)
        {
            source.playOnAwake=false; source.spatialBlend=1; source.dopplerLevel=0;
            source.minDistance=.5f; source.maxDistance=10; source.rolloffMode=AudioRolloffMode.Linear;
            GameAudioSettings.RouteSource(source);
        }
        var rig=Object.FindAnyObjectByType<OVRCameraRig>();
        if(rig!=null && rig.GetComponent<InteractionFootsteps>()==null) rig.gameObject.AddComponent<InteractionFootsteps>();
    }
    void OnDestroy() { if(Instance==this) Instance=null; }
    public static void Select()
    {
        var owner=Instance;
        if(owner==null || owner.library==null || owner.uiFrame==Time.frameCount) return;
        owner.uiFrame=Time.frameCount;
        owner.Emit(owner.library.select,null,owner.library.uiVolume);
    }
    public static void Play(InteractionSound sound, Vector3 position)
    {
        if(Instance!=null && Instance.library!=null) Instance.Emit(Instance.library.Clip(sound),position,Instance.library.physicalVolume);
    }
    public static bool Step(AudioClip clip,Vector3 position)
        => Instance!=null && Instance.library!=null && Instance.Emit(clip,position,Instance.library.footstepVolume);
    bool Emit(AudioClip clip,Vector3? position,float volume)
    {
        if(clip==null || volume<=0 || !isActiveAndEnabled) return false;
        if(!position.HasValue) uiSource.PlayOneShot(clip,volume);
        else
        {
            AudioSource voice=null;
            foreach(var source in spatialSources) if(source!=null && !source.isPlaying) {voice=source;break;}
            if(voice==null) return false;
            voice.transform.position=position.Value; voice.clip=clip; voice.volume=volume; voice.Play();
        }
        LastClip=clip; PlaybackCount++; return true;
    }
}
