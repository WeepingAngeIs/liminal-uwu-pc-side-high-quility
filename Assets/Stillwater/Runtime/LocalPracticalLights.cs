using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class LocalPracticalLights : UdonSharpBehaviour
{
    public Light[] lamps;
    [HideInInspector] public int enabledCount;
    private void Start(){RefreshLights();}
    public void RefreshLights()
    {
        SendCustomEventDelayedSeconds(nameof(RefreshLights),.25f);
        var p=Networking.LocalPlayer;
        if(!Utilities.IsValid(p))return;
        // Only four nearby lights illuminate avatars / ordinary shaders.
        // Environment lighting is already baked and needs no light cameras.
        int a=-1,b=-1,c=-1,d=-1;
        float da=256,db=256,dc=256,dd=256;
        for(int i=0;i<lamps.Length;i++) {
            float distance=(lamps[i].transform.position-p.GetPosition()).sqrMagnitude;
            if(distance<da){d=c;dd=dc;c=b;dc=db;b=a;db=da;a=i;da=distance;}
            else if(distance<db){d=c;dd=dc;c=b;dc=db;b=i;db=distance;}
            else if(distance<dc){d=c;dd=dc;c=i;dc=distance;}
            else if(distance<dd){d=i;dd=distance;}
        }
        enabledCount=0;
        for(int i=0;i<lamps.Length;i++){bool on=i==a||i==b||i==c||i==d;lamps[i].enabled=on;if(on)enabledCount++;}
    }
}
