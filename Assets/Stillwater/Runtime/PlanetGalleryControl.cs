using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using BobyStar.PlanetMovement;

// The original room's boundary handling around BobyStar's licensed movement
// controller. The controller provides real wall/ceiling locomotion and input.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class PlanetGalleryControl : UdonSharpBehaviour
{
    public BobysPlanetMovement controller;
    public PlanetMove_GravityField gravityField;
    public Transform room;
    public Transform southReturn;
    public Transform northReturn;
    [HideInInspector] public bool returning;
    private bool returnNorth;
    private float eligibleAt;

    private void Start() { WatchBoundary(); }
    public void WatchBoundary()
    {
        SendCustomEventDelayedSeconds(nameof(WatchBoundary), .1f);
        var player = Networking.LocalPlayer;
        if (!Utilities.IsValid(player) || !Utilities.IsValid(controller) || !Utilities.IsValid(room) || returning) return;
        if (!controller.IsOn) return;
        Vector3 p = room.InverseTransformPoint(controller.GetPosition());
        // The open bottom of the ring leads back to the ordinary lobby. A jump
        // over an edge also returns here instead of stranding someone sideways.
        if (Mathf.Abs(p.z) > 13.6f || Mathf.Abs(p.x) > 32 || p.y < -.5f || p.y > 61) {
            returnNorth = p.z > 0;
            ExitGravity();
        }
    }

    public void EnterGravity()
    {
        var player=Networking.LocalPlayer;
        if (!Utilities.IsValid(player) || !Utilities.IsValid(controller) || returning || Time.time < eligibleAt) return;
        Vector3 p=room.InverseTransformPoint(player.GetPosition());
        // Explicit entry is also used by the optional wall-walk console/tests.
        if (Mathf.Abs(p.x)>28 || p.y < 0 || p.y>58 || Mathf.Abs(p.z)>13) return;
        controller._EnterPlanetMovementPlayer();
    }

    public void ExitGravity()
    {
        if (!Utilities.IsValid(controller) || !controller.IsOn || returning) return;
        returning=true;
        controller._ExitPlanetMovementPlayer();
        // Boby's station listener restores the normal tracking orientation over
        // three LateUpdate frames. Finish after that sequence before teleporting.
        SendCustomEventDelayedFrames(nameof(FinishReturn), 8);
    }

    public void FinishReturn()
    {
        if (!returning) return;
        var player=Networking.LocalPlayer;
        var point=returnNorth?northReturn:southReturn;
        returning=false; eligibleAt=Time.time+1.5f;
        if (!Utilities.IsValid(player) || !Utilities.IsValid(point)) return;
        player.SetVelocity(Vector3.zero);
        player.TeleportTo(point.position,point.rotation);
    }

    public override void OnPlayerRespawn(VRCPlayerApi player)
    {
        if (!Utilities.IsValid(player) || !player.isLocal) return;
        // VRChat removes the player from a station on respawn. Cancel our local
        // return so its delayed event cannot pull them back from the world spawn.
        returning=false; eligibleAt=Time.time+1.5f;
    }
}
