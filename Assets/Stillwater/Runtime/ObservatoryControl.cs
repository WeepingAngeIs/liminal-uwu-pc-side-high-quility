using UdonSharp;
using VRC.SDK3.UdonNetworkCalling;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class ObservatoryControl : UdonSharpBehaviour
{
    public ObservatoryReceiver receiver;
    public bool recordsClock;

    public override void Interact()
    {
        if (!receiver || NetworkCalling.InNetworkCall) return;
        if (recordsClock) receiver._RecordClock();
        else receiver._FitSpool();
    }
}
