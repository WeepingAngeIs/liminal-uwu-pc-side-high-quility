using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common;

// Each terminal acts on the person who pressed it. Only a running ride schedules ticks.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class StillwaterGravityPort : UdonSharpBehaviour
{
    public Transform destination;
    public Transform circuit;
    public Transform[] stops;
    public Transform exit;

    private bool carrying;
    private bool stepQueued;
    private float savedGravity;
    private float startedAt;
    private int nextStop;

    public override void Interact()
    {
        VRCPlayerApi player = Networking.LocalPlayer;
        if (carrying || !Utilities.IsValid(player) || destination == null) return;

        if (circuit != null && stops != null && stops.Length > 0 && exit != null)
        {
            savedGravity = player.GetGravityStrength();
            carrying = true;
            nextStop = 0;
            startedAt = Time.time;
            player.SetGravityStrength(0f);
        }

        player.SetVelocity(Vector3.zero);
        player.TeleportTo(destination.position, destination.rotation);
        if (carrying && !stepQueued)
        {
            stepQueued = true;
            SendCustomEventDelayedFrames(nameof(CarryStep), 2);
        }
    }

    public void CarryStep()
    {
        stepQueued = false;
        if (!carrying) return;
        VRCPlayerApi player = Networking.LocalPlayer;
        if (!Utilities.IsValid(player))
        {
            carrying = false;
            return;
        }

        Vector3 position = player.GetPosition();
        Vector3 local = circuit.InverseTransformPoint(position);
        if (Mathf.Abs(local.x) > 15f || Mathf.Abs(local.z) > 16f || local.y < -1f || local.y > 19f)
        {
            // A respawn, a station, or another local teleport owns the new position.
            Restore(false);
            return;
        }
        if (Time.time - startedAt > 55f || nextStop >= stops.Length || stops[nextStop] == null)
        {
            Restore(true);
            return;
        }

        Vector3 delta = stops[nextStop].position - position;
        float distance = delta.magnitude;
        if (distance < 0.52f)
        {
            nextStop++;
            if (nextStop == stops.Length)
            {
                Restore(true);
                return;
            }
            delta = stops[nextStop].position - position;
            distance = delta.magnitude;
        }

        float speed = Mathf.Min(3.15f, Mathf.Sqrt(2f * 3.2f * distance));
        Vector3 targetVelocity = delta.normalized * speed;
        Vector3 velocity = Vector3.MoveTowards(player.GetVelocity(), targetVelocity, 3.9f * Mathf.Min(Time.deltaTime, 0.12f));
        player.SetVelocity(velocity);
        stepQueued = true;
        SendCustomEventDelayedFrames(nameof(CarryStep), 1);
    }

    public override void InputJump(bool value, UdonInputEventArgs args)
    {
        if (value && carrying && Time.time - startedAt > 0.5f) Restore(true);
    }

    public override void OnPlayerRespawn(VRCPlayerApi player)
    {
        if (Utilities.IsValid(player) && player.isLocal) Restore(false);
    }

    private void OnDisable()
    {
        Restore(false);
    }

    private void Restore(bool landAtTerminal)
    {
        if (!carrying) return;
        carrying = false;
        VRCPlayerApi player = Networking.LocalPlayer;
        if (!Utilities.IsValid(player)) return;
        player.SetGravityStrength(savedGravity);
        player.SetVelocity(Vector3.zero);
        if (landAtTerminal && exit != null) player.TeleportTo(exit.position, exit.rotation);
    }
}
