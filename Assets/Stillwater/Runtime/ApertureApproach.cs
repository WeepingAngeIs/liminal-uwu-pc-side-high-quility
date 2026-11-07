using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// Entirely local. Moving the train and its passengers remains the shared server-clock job.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class ApertureApproach : UdonSharpBehaviour
{
    public ApertureExpress service;
    public WorldAudioDirector audioDirector;
    public AudioSource rumble;
    public Material localSky;
    public Transform approachStart, sink, safeDeck, arrival;
    public Vector3 deckHalfSize = new Vector3(2.65f,3.5f,5.75f);
    private bool pulling;
    private float savedGravity, savedWalk, savedRun, savedStrafe, savedJump;
    private float pullStarted, cooldownUntil, currentScale=1;

    private void Start() { ResetLocal(); }

    private void Update()
    {
        var player=Networking.LocalPlayer;
        if(!Utilities.IsValid(player) || !Utilities.IsValid(approachStart) || !Utilities.IsValid(sink)) return;
        Vector3 position=player.GetPosition();
        Vector3 axis=approachStart.forward;
        Vector3 delta=position-approachStart.position;
        float along=Vector3.Dot(delta,axis);
        Vector3 side=delta-axis*along;
        bool nearby=along> -25f && along<225f && side.sqrMagnitude<1600f;
        float strength=nearby ? Mathf.SmoothStep(0,1,Mathf.InverseLerp(-25f,132f,along)) : 0;
        if(!nearby || Time.time<cooldownUntil) {
            RestoreMovement();
            SetPresentation(0,true);
            return;
        }
        SetPresentation(strength,false);
        bool seated=Utilities.IsValid(service) && service.localSeated;
        Vector3 deck=Utilities.IsValid(safeDeck) ? safeDeck.InverseTransformPoint(position) : Vector3.one*999;
        bool onDeck=Mathf.Abs(deck.x)<deckHalfSize.x && Mathf.Abs(deck.z)<deckHalfSize.z && deck.y>-.6f && deck.y<deckHalfSize.y;
        // The static overlook is an opt-in boundary. Its open tip leads into the
        // field; the carriage stations and people remaining on the ledge are safe.
        bool insidePull=along>102f && along<220f && side.sqrMagnitude<900f;
        if(seated || onDeck || !insidePull) {
            RestoreMovement();
            return;
        }
        if(!pulling) {
            savedGravity=player.GetGravityStrength();
            savedWalk=player.GetWalkSpeed(); savedRun=player.GetRunSpeed();
            savedStrafe=player.GetStrafeSpeed(); savedJump=player.GetJumpImpulse();
            pulling=true; pullStarted=Time.time;
            player.SetGravityStrength(0);
            player.SetWalkSpeed(0); player.SetRunSpeed(0); player.SetStrafeSpeed(0); player.SetJumpImpulse(0);
        }
        Vector3 toSink=sink.position-position;
        if(toSink.sqrMagnitude<49 || Time.time-pullStarted>8.5f) {
            ReturnToArrival();
            return;
        }
        float speed=Mathf.Lerp(4.0f,18f,Mathf.Clamp01((Time.time-pullStarted)/4.5f));
        Vector3 velocity=Vector3.MoveTowards(player.GetVelocity(),toSink.normalized*speed,8f*Mathf.Min(Time.deltaTime,.08f));
        player.SetVelocity(velocity);
    }

    private void SetPresentation(float strength,bool immediate)
    {
        // Keep the luminous rim within the forward carriage view at the overlook.
        float target=1+2.1f*strength;
        currentScale=immediate ? target : Mathf.MoveTowards(currentScale,target,3.5f*Mathf.Min(Time.deltaTime,.08f));
        if(Utilities.IsValid(localSky)) localSky.SetFloat("_ApproachScale",currentScale);
        if(!Utilities.IsValid(rumble)) return;
        float userVolume=Utilities.IsValid(audioDirector) ? Mathf.Clamp01(audioDirector.atmosphereVolume) : .35f;
        rumble.volume=userVolume*.34f*strength*strength;
        rumble.pitch=Mathf.Lerp(.8f,1f,strength);
        if(strength>.015f && userVolume>0) {
            if(!rumble.isPlaying) rumble.Play();
        } else if(rumble.isPlaying) rumble.Stop();
    }

    public void CancelPullForSeat() { RestoreMovement(); }

    public void ReturnToArrival()
    {
        ResetLocal();
        cooldownUntil=Time.time+1;
        var player=Networking.LocalPlayer;
        if(Utilities.IsValid(player) && Utilities.IsValid(arrival)) {
            player.TeleportTo(arrival.position,arrival.rotation);
            player.SetVelocity(Vector3.zero);
        }
    }

    private void RestoreMovement()
    {
        if(!pulling) return;
        pulling=false;
        var player=Networking.LocalPlayer;
        if(!Utilities.IsValid(player)) return;
        player.SetGravityStrength(savedGravity);
        player.SetWalkSpeed(savedWalk); player.SetRunSpeed(savedRun);
        player.SetStrafeSpeed(savedStrafe); player.SetJumpImpulse(savedJump);
        player.SetVelocity(Vector3.zero);
    }

    private void ResetLocal()
    {
        RestoreMovement();
        currentScale=1;
        if(Utilities.IsValid(localSky)) localSky.SetFloat("_ApproachScale",1);
        if(Utilities.IsValid(rumble)) { rumble.volume=0; rumble.Stop(); }
    }
    public override void OnPlayerRespawn(VRCPlayerApi player)
    {
        if(Utilities.IsValid(player) && player.isLocal) {
            ResetLocal(); cooldownUntil=Time.time+1;
        }
    }
    public override void OnPlayerLeft(VRCPlayerApi player)
    {
        if(Utilities.IsValid(player) && player.isLocal) ResetLocal();
    }
    private void OnDisable() { ResetLocal(); }
}
