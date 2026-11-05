using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class LastTrainBoard : UdonSharpBehaviour
{
    public Transform[] destinations;
    private int last = -1;
    public override void Interact()
    {
        var player=Networking.LocalPlayer;
        if (!Utilities.IsValid(player) || destinations==null || destinations.Length==0) return;
        int index=Random.Range(0,destinations.Length);
        if(destinations.Length>1 && last>=0)
            index=(last+Random.Range(1,destinations.Length))%destinations.Length;
        var stop=destinations[index];
        if(!Utilities.IsValid(stop)) return;
        last=index;
        player.TeleportTo(stop.position,stop.rotation);
        player.SetVelocity(Vector3.zero);
    }
}
