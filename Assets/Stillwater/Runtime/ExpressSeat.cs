using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRCStation = VRC.SDK3.Components.VRCStation;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class ExpressSeat : UdonSharpBehaviour
{
    public ApertureExpress service;
    public VRCStation seat;
    private VRCPlayerApi occupant;

    public override void Interact()
    {
        if (Utilities.IsValid(occupant) || !service.DoorsAreOpen() || !Utilities.IsValid(Networking.LocalPlayer)) return;
        seat.UseStation(Networking.LocalPlayer);
    }
    public override void OnStationEntered(VRCPlayerApi player)
    {
        occupant=player;
        if (player.isLocal) service.localSeated=true;
    }
    public override void OnStationExited(VRCPlayerApi player)
    {
        if (Utilities.IsValid(occupant) && occupant.playerId==player.playerId) occupant=null;
        if (player.isLocal) service.localSeated=false;
    }
    public override void OnPlayerLeft(VRCPlayerApi player)
    {
        if (occupant==player) occupant=null;
    }
}
