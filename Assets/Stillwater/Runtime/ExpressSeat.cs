using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRCStation = VRC.SDK3.Components.VRCStation;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class ExpressSeat : UdonSharpBehaviour
{
    public ApertureExpress service;
    public VRCStation seat;
    public Transform apertureExit;
    public ApertureApproach approach;
    private VRCPlayerApi occupant;
    private bool pendingApertureExit;
    private float respawnedAt=-1000;

    public override void Interact()
    {
        if (Utilities.IsValid(occupant) || !service.DoorsAreOpen() || !Utilities.IsValid(Networking.LocalPlayer)) return;
        seat.UseStation(Networking.LocalPlayer);
    }
    public override void OnStationEntered(VRCPlayerApi player)
    {
        occupant=player;
        if (player.isLocal) {
            pendingApertureExit=false;
            service.localSeated=true;
            if (Utilities.IsValid(approach)) approach.CancelPullForSeat();
        }
    }
    public override void OnStationExited(VRCPlayerApi player)
    {
        if (Utilities.IsValid(occupant) && occupant.playerId==player.playerId) occupant=null;
        if (player.isLocal) {
            service.localSeated=false;
            // The SDK first uses the ordinary stationary platform exit. Complete
            // the optional summit disembark on the following frame, after it exits.
            pendingApertureExit=Time.time-respawnedAt>.5f && Utilities.IsValid(apertureExit) && service.AtApertureLayover();
            if(pendingApertureExit) SendCustomEventDelayedFrames(nameof(DisembarkAtAperture),1);
        }
    }
    public void DisembarkAtAperture()
    {
        if(!pendingApertureExit) return;
        pendingApertureExit=false;
        var player=Networking.LocalPlayer;
        if(!Utilities.IsValid(player) || service.localSeated || !Utilities.IsValid(apertureExit)) return;
        player.TeleportTo(apertureExit.position,apertureExit.rotation);
        player.SetVelocity(Vector3.zero);
    }
    public override void OnPlayerRespawn(VRCPlayerApi player)
    {
        if(Utilities.IsValid(player) && player.isLocal) {
            pendingApertureExit=false;
            respawnedAt=Time.time;
        }
    }
    private void OnDisable()
    {
        pendingApertureExit=false;
        if(Utilities.IsValid(occupant) && occupant.isLocal && Utilities.IsValid(service)) service.localSeated=false;
    }
    public override void OnPlayerLeft(VRCPlayerApi player)
    {
        if (occupant==player) occupant=null;
    }
}
