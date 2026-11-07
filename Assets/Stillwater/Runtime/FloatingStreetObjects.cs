using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FloatingStreetObjects : UdonSharpBehaviour
{
    public Transform[] objects;
    public Renderer[] surfaces;
    public Vector3[] restPositions;
    public Quaternion[] restRotations;
    [HideInInspector] public int animatedObjects;
    private void Start() { Drift(); }
    public void Drift()
    {
        SendCustomEventDelayedSeconds(nameof(Drift), .05f);
        var player=Networking.LocalPlayer;
        if(!Utilities.IsValid(player))return;
        double time=Networking.GetServerTimeInSeconds();
        animatedObjects=0;
        for(int i=0;i<objects.Length;i++) {
            if(!Utilities.IsValid(objects[i]) || !surfaces[i].enabled ||
                (objects[i].position-player.GetPosition()).sqrMagnitude>19600)continue;
            float cycle=(float)(time%(31.0+i*1.37))/(31f+i*1.37f)*6.283185f+i*2.39996f;
            Vector3 drift=new Vector3(Mathf.Sin(cycle*2)*.34f,Mathf.Sin(cycle)*.92f,Mathf.Sin(cycle)*.83f);
            objects[i].localPosition=restPositions[i]+drift;
            objects[i].localRotation=restRotations[i]*Quaternion.Euler(Mathf.Sin(cycle)*7,Mathf.Cos(cycle)*9,Mathf.Sin(cycle+.7f)*6);
            animatedObjects++;
        }
    }
}
