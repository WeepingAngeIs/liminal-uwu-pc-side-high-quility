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
    public AudioSource carriageAmbience;
    public WorldAudioDirector audioDirector;
    private int displayed = -1;

    // The owner records one shared epoch. Joining, ownership transfer and pressing
    // the button again never replace it, so an active service cannot be recalled.
    [UdonSynced] private double startedAt;
    [UdonSynced] private bool hasStarted;
    [HideInInspector] public bool localSeated;
    private const string State = "Base Layer.Aperture round trip";

    private void Start() { RunClock(); }

    private void Update()
    {
        if (!Utilities.IsValid(carriageAmbience)) return;
        var player=Networking.LocalPlayer;
        if (!Utilities.IsValid(player) || !Utilities.IsValid(audioDirector) || !hasStarted) {
            SilenceCarriage(); return;
        }
        float phase=Phase(Networking.GetServerTimeInSeconds());
        float distanceToStop=0;
        if (phase>=Depart && phase<Summit)
            distanceToStop=Mathf.Min(phase-Depart, Summit-phase);
        else if (phase>=LeaveSummit && phase<Dock)
            distanceToStop=Mathf.Min(phase-LeaveSummit, Dock-phase);
        // Rail noise eases in/out over two seconds at each departure/arrival.
        // Its location is local and head-relative, so it fills the cabin evenly.
        float gain=Mathf.Clamp01(audioDirector.atmosphereVolume)*.16f*Mathf.SmoothStep(0,1,distanceToStop*.5f);
        if (gain<=.0001f || !Utilities.IsValid(carriageAmbience.clip)) { SilenceCarriage(); return; }
        bool aboard=localSeated;
        if (!aboard && coaches!=null) {
            Vector3 position=player.GetPosition();
            for(int i=0;i<coaches.Length;i++) {
                if (!Utilities.IsValid(coaches[i])) continue;
                Vector3 p=coaches[i].InverseTransformPoint(position);
                if (Mathf.Abs(p.x)<=1.55f && Mathf.Abs(p.z)<=3.65f && p.y>=-.18f && p.y<=3.05f) {
                    aboard=true; break;
                }
            }
        }
        // Exiting the coach or muting Atmosphere must never leave a 2D loop
        // audible elsewhere. The ordinary room/rain sources remain untouched.
        if (!aboard) { SilenceCarriage(); return; }
        carriageAmbience.volume=gain;
        if (!carriageAmbience.isPlaying) carriageAmbience.Play();
    }

    private void SilenceCarriage()
    {
        if (!Utilities.IsValid(carriageAmbience)) return;
        carriageAmbience.volume=0;
        if (carriageAmbience.isPlaying) carriageAmbience.Stop();
    }
    private void OnDisable() { SilenceCarriage(); }

    // Tick native animation from the instance clock. Only correct a clock error, so the
    // vehicle moves continuously between samples, including while its rider looks away.
    public void RunClock()
    {
        SampleClock();
        SendCustomEventDelayedSeconds(nameof(RunClock), .3f);
    }
    public override void OnDeserialization() { SampleClock(); }
    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        if (Networking.IsOwner(gameObject)) RequestSerialization();
        SampleClock();
    }
    public override void OnPlayerJoined(VRCPlayerApi player)
    {
        if (Networking.IsOwner(gameObject)) RequestSerialization();
    }

    private float Phase(double now)
    {
        if (!hasStarted || now < startedAt) return 0;
        return (float)((now - startedAt) % Cycle);
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
        SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(StartService));
    }

    public void StartService()
    {
        if (!Networking.IsOwner(gameObject) || hasStarted) return;
        startedAt = Networking.GetServerTimeInSeconds();
        hasStarted = true;
        RequestSerialization();
        SampleClock();
    }

    public override void OnPlayerRespawn(VRCPlayerApi player)
    {
        if (Utilities.IsValid(player) && player.isLocal) { localSeated = false; SilenceCarriage(); }
    }

    private void SampleClock()
    {
        if (!Utilities.IsValid(service)) return;
        double now = Networking.GetServerTimeInSeconds();
        float phase = Phase(now);
        service.speed = hasStarted ? 1f : 0f;
        float current = Mathf.Repeat(service.GetCurrentAnimatorStateInfo(0).normalizedTime,1f) * Cycle;
        float error = Mathf.Abs(current-phase);
        if (!service.GetCurrentAnimatorStateInfo(0).IsName(State) || Mathf.Min(error,Cycle-error)>.09f)
            service.Play(State,0,phase/Cycle);
        if (displayPanels != null && displayPanels.Length == 6)
        {
            int status;
            if (!hasStarted) status = 2;
            else if (phase < Depart - 6f || phase >= Dock + 2.5f) status = 0;
            else if (phase < Depart) status = 1;
            else if (phase < Summit) status = 3;
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
