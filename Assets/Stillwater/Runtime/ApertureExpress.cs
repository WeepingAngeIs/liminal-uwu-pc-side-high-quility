using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class ApertureExpress : UdonSharpBehaviour
{
    public const float Cycle = 320f;
    public const float Depart = 36f;
    public const float Summit = 166f;
    public const float LeaveSummit = 196f;
    public const float Dock = 314f;
    public Animator service;
    public Transform[] coaches;
    public Transform platformExit;
    public GameObject[] displayPanels;
    private int displayed = -1;

    [UdonSynced] private double calledReturnAt;
    [UdonSynced] private bool hasCall;
    [HideInInspector] public bool localSeated;
    private const string State = "Base Layer.Aperture round trip";

    private void Start() { RunClock(); }

    // Tick native animation from the instance clock. Only correct a clock error, so the
    // vehicle moves continuously between samples, including while its rider looks away.
    public void RunClock()
    {
        SampleClock();
        SendCustomEventDelayedSeconds(nameof(RunClock), .3f);
    }
    public override void OnDeserialization() { SampleClock(); }

    private float Phase(double now)
    {
        double phase = now % Cycle;
        if (phase < 0) phase += Cycle;
        double start = now - phase;
        if (hasCall && calledReturnAt >= start && calledReturnAt < start + LeaveSummit && now >= calledReturnAt)
            return Mathf.Min(Cycle - .015f, LeaveSummit + (float)(now - calledReturnAt));
        return (float)phase;
    }

    public bool DoorsAreOpen()
    {
        float phase = Phase(Networking.GetServerTimeInSeconds());
        return phase < Depart - 6f || phase >= Dock + 2.5f;
    }

    public bool AtApertureLayover()
    {
        float phase = Phase(Networking.GetServerTimeInSeconds());
        return phase >= Summit && phase < LeaveSummit;
    }

    public override void Interact()
    {
        SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(CallToPlatform));
    }

    public void CallToPlatform()
    {
        if (!Networking.IsOwner(gameObject)) return;
        double now = Networking.GetServerTimeInSeconds();
        double raw = now % Cycle;
        if (raw < 0) raw += Cycle;
        double start = now - raw;
        if (raw < Depart || raw >= LeaveSummit || (hasCall && calledReturnAt >= start && calledReturnAt < start + Cycle)) return;
        // A request while climbing preserves the entire climb. Only the layover is cut.
        calledReturnAt = start + Mathf.Min(LeaveSummit, Mathf.Max(Summit + 2f, (float)raw + 1f));
        hasCall = true;
        RequestSerialization();
        SampleClock();
    }

    public override void OnPlayerRespawn(VRCPlayerApi player)
    {
        if (Utilities.IsValid(player) && player.isLocal) localSeated = false;
    }

    private void SampleClock()
    {
        if (!Utilities.IsValid(service)) return;
        double now = Networking.GetServerTimeInSeconds();
        float phase = Phase(now);
        float current = Mathf.Repeat(service.GetCurrentAnimatorStateInfo(0).normalizedTime,1f) * Cycle;
        float error = Mathf.Abs(current-phase);
        if (!service.GetCurrentAnimatorStateInfo(0).IsName(State) || Mathf.Min(error,Cycle-error)>.09f)
            service.Play(State,0,phase/Cycle);
        if (displayPanels != null && displayPanels.Length == 6)
        {
            int status;
            if (phase < Depart - 6f || phase >= Dock + 2.5f) status = 0;
            else if (phase < Depart) status = 1;
            else if (phase < Summit) status = hasCall && calledReturnAt >= now ? 2 : 3;
            else if (phase < LeaveSummit) status = 4;
            else status = 5;
            if (status != displayed) {
                for (int i = 0; i < displayPanels.Length; i++) displayPanels[i].SetActive(i == status);
                displayed = status;
            }
        }
        if (localSeated || phase < Depart - 5.5f || phase > Depart + 2f) return;
        var player = Networking.LocalPlayer;
        if (!Utilities.IsValid(player)) return;
        for (int i=0; i<coaches.Length; i++)
        {
            Vector3 inside=coaches[i].InverseTransformPoint(player.GetPosition());
            if (Mathf.Abs(inside.x)>1.93f || Mathf.Abs(inside.z)>3.83f || inside.y<-.18f || inside.y>3.05f) continue;
            player.TeleportTo(platformExit.position,platformExit.rotation);
            player.SetVelocity(Vector3.zero);
            return;
        }
    }
}
