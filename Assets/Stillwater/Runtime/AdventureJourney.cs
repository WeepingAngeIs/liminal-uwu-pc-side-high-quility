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
    private float readyAt;
    private void Start() { CheckDoor(); }
    public void CheckDoor()
    {
        SendCustomEventDelayedSeconds(nameof(CheckDoor), .08f);
        var player = Networking.LocalPlayer;
        if (!Utilities.IsValid(player) || Time.time < readyAt) return;
        Vector3 position = player.GetPosition();
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
