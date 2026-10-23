using System.Collections;
using System.Reflection;
using System.Text;
using UnityEngine;

/// <summary>Actual bottle discovery physics, scaled models, phase gating and one-shot delivery.</summary>
public sealed class EvidenceDiscoveryTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }
    private readonly StringBuilder report=new("=== EvidenceDiscoveryTest ===\n");
    private int failures;
    private void Check(string label,bool value){report.AppendLine((value?"PASS: ":"FAIL: ")+label);if(!value)failures++;}
    private static IEnumerator PhysicsFrames(){for(int i=0;i<4;i++)yield return new WaitForFixedUpdate();}
    public void RunAllTests()=>StartCoroutine(Run());
    private IEnumerator Run()
    {
        AutomatedTestSandbox.Activate(nameof(EvidenceDiscoveryTest));yield return null;
        var bottle=EvidenceProp.FindById("EVD-018");var esm=EvidenceStateManager.Instance;var record=esm.GetRecord("EVD-018");
        var notice=bottle.GetComponent<SphereCollider>();
        Physics.SyncTransforms();
        Check("Scaled bottle has the authored 1.5 m world-space notice radius",notice!=null&&notice.isTrigger
            &&Mathf.Abs(notice.bounds.extents.x-bottle.InteractionRadius)<.001f&&Mathf.Approximately(bottle.InteractionRadius,1.5f));
        var solid=bottle.GetComponent<BoxCollider>();var originalBox=solid.size;var originalScale=bottle.transform.localScale;
        Check("Bottle keeps its real-sized solid collider and model scale",Mathf.Approximately(originalScale.x,.15f)&&solid.bounds.size.x<.1f&&!solid.isTrigger);
        var marker=EvidenceFingerprintMarker.FindByEvidenceId("EVD-014");
        Check("Latent fingerprint still has no automatic discovery sphere",marker!=null&&marker.GetComponent<SphereCollider>()==null);
        var proxy=new GameObject("Discovery player probe");proxy.tag="Player";
        proxy.transform.position=bottle.transform.position+Vector3.back*4;
        proxy.AddComponent<SphereCollider>().radius=.08f;var rb=proxy.AddComponent<Rigidbody>();rb.isKinematic=true;rb.useGravity=false;
        var phase=typeof(SceneTurnover).GetProperty("Phase").GetSetMethod(true);var originalPhase=SceneTurnover.Phase;
        int discoveries=0;System.Action<string,EvidenceStatus> handler=(id,status)=>{if(id=="EVD-018"&&status==EvidenceStatus.Found)discoveries++;};
        EvidenceStateManager.OnEvidenceStatusChanged+=handler;
        try
        {
            Check("Bottle starts undiscovered",record.status==EvidenceStatus.NotFound);
            phase.Invoke(null,new object[]{ScenarioPhase.IOCAssessment});
            proxy.transform.position=bottle.transform.position+new Vector3(0,-.2f,-1f);Physics.SyncTransforms();
            yield return PhysicsFrames();
            Check("Approach across the counter is inside the notice sphere",notice.bounds.Contains(proxy.transform.position));
            Check("IOC approach cannot discover the bottle",record.status==EvidenceStatus.NotFound&&discoveries==0);
            phase.Invoke(null,new object[]{originalPhase});
            yield return PhysicsFrames();
            Check("Unlocking investigation discovers the already-overlapping player",record.status==EvidenceStatus.Found&&discoveries==1);
            yield return PhysicsFrames();
            proxy.transform.position=bottle.transform.position+Vector3.back*4;Physics.SyncTransforms();yield return PhysicsFrames();
            proxy.transform.position=bottle.transform.position+new Vector3(0,-.2f,-1f);Physics.SyncTransforms();yield return PhysicsFrames();
            Check("Staying and reentering never repeat the Found transition",discoveries==1);
            Check("Discovery keeps bottle placement, physical collider and size intact",solid.size==originalBox&&bottle.transform.localScale==originalScale);
        }
        finally{phase.Invoke(null,new object[]{originalPhase});EvidenceStateManager.OnEvidenceStatusChanged-=handler;Destroy(proxy);}
        // A separate unregistered fixture verifies radius conversion for rotated/non-uniform model scale.
        var fixture=new GameObject("Scaled discovery geometry fixture");fixture.transform.position=new Vector3(50,50,50);
        fixture.transform.localScale=new Vector3(.15f,.3f,.1f);fixture.transform.rotation=Quaternion.Euler(20,30,40);
        fixture.AddComponent<EvidenceProp>();yield return null;Physics.SyncTransforms();
        var sphere=fixture.GetComponent<SphereCollider>();
        Check("Non-uniform rotated imports also retain the world-space radius",sphere!=null&&Mathf.Abs(sphere.bounds.extents.x-1.5f)<.001f);
        Destroy(fixture);
        report.AppendLine(failures==0?"=== PASS, 0 failures ===":"=== FAIL: "+failures+" failure(s) ===");Report=report.ToString();IsDone=true;
    }
}
