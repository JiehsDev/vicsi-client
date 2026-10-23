using UnityEngine;

/// <summary>Compare a held lift (or a historical secured card) through the real desk.</summary>
public static class FingerprintComparisonTestUtil
{
    public static bool CompareStoredSource(string sourceId) => CompareSourceWithReference(sourceId, sourceId=="EVD-018"?1:2);
    public static bool CompareSourceWithReference(string sourceId, int referenceIndex)
    {
        var station=FingerprintComparisonStation.Instance;var rig=Object.FindAnyObjectByType<OVRCameraRig>();
        var original=Camera.main;var oldTag=original.tag;bool rigEnabled=rig.enabled;GameObject eye=null;
        try
        {
            rig.enabled=false;original.tag="Untagged";eye=new GameObject("Comparison diagnostic eye",typeof(Camera));eye.tag="MainCamera";
            eye.transform.position=station.transform.position-station.transform.forward*1.25f+Vector3.up*1.6f;
            eye.transform.LookAt(station.transform.position+Vector3.up*1.5f);
            var cordon=Object.FindAnyObjectByType<SceneCordon>();cordon.AccessRegister.RecordSimulationReposition(eye.transform.position);
            STCSNotificationUI.Instance?.HideImmediately();
            if(!station.Open())return false;
            for(int i=0;i<station.AvailableSamples.Count&&station.CurrentSourceId!=sourceId;i++)station.NextSample();
            return station.CurrentSourceId==sourceId&&station.SelectReference(referenceIndex)&&station.Submit();
        }
        finally
        {
            station.Close();if(eye!=null){eye.tag="Untagged";Object.Destroy(eye);}original.tag=oldTag;rig.enabled=rigEnabled;
            Object.FindAnyObjectByType<SceneCordon>().AccessRegister.RecordSimulationReposition(original.transform.position);
        }
    }
}
