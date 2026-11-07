using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRCStation = VRC.SDK3.Components.VRCStation;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class LoungeSeat : UdonSharpBehaviour
{
    public VRCStation seat;
    [HideInInspector] public bool localSeated;
    private VRCPlayerApi occupant;

    public override void Interact()
    {
        var player = Networking.LocalPlayer;
        if (!Utilities.IsValid(player) || !Utilities.IsValid(seat) || Utilities.IsValid(occupant)) return;
        seat.UseStation(player);
    }

    public override void OnStationEntered(VRCPlayerApi player)
    {
        occupant = player;
        if (Utilities.IsValid(player) && player.isLocal) localSeated = true;
    }

    public override void OnStationExited(VRCPlayerApi player)
    {
        if (occupant == player) occupant = null;
        if (Utilities.IsValid(player) && player.isLocal) localSeated = false;
    }

    public override void OnPlayerLeft(VRCPlayerApi player)
    {
        if (occupant == player) occupant = null;
    }

    public override void OnPlayerRespawn(VRCPlayerApi player)
    {
        if (Utilities.IsValid(player) && player.isLocal) localSeated = false;
    }
}
