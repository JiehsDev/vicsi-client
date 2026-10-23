using Oculus.Interaction.Locomotion;
using UnityEngine;

/// <summary>Distance-based steps follow actual head travel; menus and teleports reset cadence.</summary>
public sealed class InteractionFootsteps : MonoBehaviour
{
    [SerializeField] private float strideDistance=.65f;
    [SerializeField] private float minimumInterval=.24f;
    [SerializeField] private float teleportDistance=1.5f;
    private OVRCameraRig rig;
    private FirstPersonLocomotor locomotor;
    private Vector3 previous;
    private bool initialized;
    private float distance, nextStep;
    private int variation;
    void Awake() { rig=GetComponent<OVRCameraRig>(); locomotor=Object.FindAnyObjectByType<FirstPersonLocomotor>(); }
    void OnEnable() { initialized=false; distance=0; }
    void LateUpdate()
    {
        if(rig==null || rig.centerEyeAnchor==null) return;
        bool blocked=PlayerUIGate.IsBlocked && !(PlayerUIGate.ConfirmOwnerToken is PhotographTool);
        Advance(rig.centerEyeAnchor.position,blocked || locomotor==null || !locomotor.isActiveAndEnabled || !locomotor.IsGrounded,Time.time);
    }
    public bool Advance(Vector3 head,bool blocked,float now)
    {
        if(!initialized) {previous=head;initialized=true;return false;}
        float travel=Vector3.Distance(new Vector3(previous.x,0,previous.z),new Vector3(head.x,0,head.z));
        previous=head;
        if(blocked || travel>teleportDistance) {distance=0;return false;}
        if(travel<=.001f) return false;
        distance+=travel;
        if(distance<strideDistance || now<nextStep) return false;
        distance=0; nextStep=now+minimumInterval;
        var library=InteractionSfx.Instance!=null ? InteractionSfx.Instance.Library : null;
        if(library==null) return false;
        var clips=library.concreteSteps;
        var feet=head-Vector3.up*1.6f;
        float closest=float.MaxValue;
        foreach(var hit in Physics.RaycastAll(head,Vector3.down,3,~0,QueryTriggerInteraction.Ignore))
        {
            if(hit.transform.IsChildOf(transform) || hit.collider.GetComponentInParent<FirstPersonLocomotor>()!=null || hit.distance>=closest) continue;
            closest=hit.distance; feet=hit.point;
            string material=(hit.collider.sharedMaterial!=null?hit.collider.sharedMaterial.name:"")+" "+hit.transform.name;
            material=material.ToLowerInvariant();
            clips=hit.collider is TerrainCollider || material.Contains("grass") || material.Contains("lawn") ? library.grassSteps
                : material.Contains("wood") ? library.woodSteps : library.concreteSteps;
        }
        return clips!=null && clips.Length>0 && InteractionSfx.Step(clips[variation++%clips.Length],feet);
    }
}
