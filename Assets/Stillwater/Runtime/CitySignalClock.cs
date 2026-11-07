using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class CitySignalClock : UdonSharpBehaviour
{
    public Material receiver;

    void Start() { TuneReceiver(); }
    void Update() { TuneReceiver(); }

    public void TuneReceiver()
    {
        if (!Utilities.IsValid(receiver)) return;
        double seconds = Networking.GetServerTimeInSeconds();
        float phase=(float)((seconds % 24.0 + 24.0) % 24.0);
        receiver.SetFloat("_BroadcastSecond",phase);
        // One shared material serves every receiver. This clock is a local read
        // of server time, not a network event, and keeps the film smooth in VR.
    }
}
