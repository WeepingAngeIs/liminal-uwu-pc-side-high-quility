using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class AdventureJourney : UdonSharpBehaviour
{
    public Transform[] entrances;
    public Transform[] destinations;
    public Vector3[] halfExtents;
    public string[] sourceRooms;
    public string[] destinationRooms;
    [HideInInspector] public int lastDoor = -1;
    public DistrictVisibility roomTracker;
    public Transform[] roomCheckpoints;
    public bool[] rememberEntry;
    public Transform[] belts;
    private int savedRoom = -1;
    private Vector3 checkpoint;
    private Quaternion facing;
    private float readyAt;

    public override void OnPlayerRespawn(VRCPlayerApi player)
    {
        if (!player.isLocal || savedRoom < 0) return;
        player.TeleportTo(checkpoint, facing);
        player.SetVelocity(Vector3.zero);
        readyAt = Time.time + 1.5f;
    }

    private void CheckTravel(VRCPlayerApi player, Vector3 position)
    {
        if (!roomTracker || !player.IsPlayerGrounded()) return;
        int room = roomTracker.currentSector;
        if (room >= 0 && room < roomCheckpoints.Length && room != savedRoom &&
            (roomCheckpoints[room] || rememberEntry[room])) {
            Vector3 lo = roomTracker.zoneMin[room], hi = roomTracker.zoneMax[room];
            if (position.x >= lo.x && position.x <= hi.x && position.z >= lo.z && position.z <= hi.z &&
                position.y >= lo.y && position.y <= hi.y) {
                savedRoom = room;
                Transform anchor = roomCheckpoints[room];
                checkpoint = anchor ? anchor.position : position + Vector3.up * .12f;
                facing = anchor ? anchor.rotation : player.GetRotation();
            }
        }
        for (int i = 0; i < belts.Length; i++) {
            Transform belt = belts[i];
            Vector3 local = belt.InverseTransformPoint(position);
            if (Mathf.Abs(local.x) > .5f || Mathf.Abs(local.z) > .5f || local.y < -.7f || local.y > 3f) continue;
            Vector3 velocity = player.GetVelocity();
            float speed = Vector3.Dot(velocity, belt.forward);
            if (speed < 5f) player.SetVelocity(velocity + belt.forward * (5f - speed));
            break;
        }
    }
    private void Start() { CheckDoor(); }
    public void CheckDoor()
    {
        SendCustomEventDelayedSeconds(nameof(CheckDoor), .08f);
        var player = Networking.LocalPlayer;
        if (!Utilities.IsValid(player) || Time.time < readyAt) return;
        Vector3 position = player.GetPosition();
        CheckTravel(player, position);
        for (int i=0; i<entrances.Length; i++) {
            Vector3 p=entrances[i].InverseTransformPoint(position);
            Vector3 h=halfExtents[i];
            if (Mathf.Abs(p.x)>h.x || p.y<-.35f || p.y>h.y || Mathf.Abs(p.z)>h.z) continue;
            readyAt=Time.time+1.5f; lastDoor=i;
            player.SetVelocity(Vector3.zero);
            player.TeleportTo(destinations[i].position,destinations[i].rotation);
            return;
        }
    }
}
