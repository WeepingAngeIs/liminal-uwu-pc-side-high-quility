using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class CitySignalClock : UdonSharpBehaviour
{
    public Material receiver;
    public Material aperture;

    void Start() { TuneReceiver(); }

    public void TuneReceiver()
    {
        if (!Utilities.IsValid(receiver)) return;
        double seconds = Networking.GetServerTimeInSeconds();
        float phase=(float)((seconds % 24.0 + 24.0) % 24.0);
        receiver.SetFloat("_BroadcastSecond",phase);
        if(Utilities.IsValid(aperture)) aperture.SetFloat("_BroadcastSecond",phase);
        // The original broadcast advances a film frame every half second. Update
        // the single shared receiver at eight hertz for a smooth dissolve on PC.
        SendCustomEventDelayedSeconds(nameof(TuneReceiver),.125f);
    }
}
