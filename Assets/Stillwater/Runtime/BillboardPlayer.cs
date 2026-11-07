using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.SDK3.Components;
using VRC.SDK3.Components.Video;
using VRC.SDK3.Video.Components;
using VRC.Udon.Common.Interfaces;

// The screen/timeline is shared. The portable panel, audio sliders and retries are local.
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class BillboardPlayer : UdonSharpBehaviour
{
    public VRCUnityVideoPlayer video;
    public Material screenMaterial;
    public AudioSource speaker;
    public GameObject menu;
    public Transform wristButton;
    public VRCUrlInputField urlInput;
    public Text statusLabel;
    public Text ownerLabel;
    public Slider videoVolume;
    public Slider rainVolume;
    public Slider atmosphereVolume;
    public WorldAudioDirector worldAudio;

    [UdonSynced] private VRCUrl sharedUrl = VRCUrl.Empty;
    [UdonSynced] private int revision;
    [UdonSynced] private int playbackState; // 0 poster, 1 playing, 2 paused
    [UdonSynced] private double timelineStart;
    [UdonSynced] private float pausedAt;
    [UdonSynced] private double lastSharedLoad;
    [UdonSynced] private bool awaitingStart;

    private int loadedRevision = -1;
    private bool prepared;
    private bool loading;
    private bool pendingRetry;
    private bool initialized;
    private bool slidersReady;
    private float nextLoadTime;
    private float loadingSince;
    private int retries;
    private string notice = "Paste a video URL, then load it for everyone.";
    private const float UrlInterval = 6f;

    private void Start()
    {
        if (Utilities.IsValid(menu)) menu.SetActive(false);
        if (Utilities.IsValid(video)) { video.Loop = false; video.EnableAutomaticResync = false; }
        if (Utilities.IsValid(screenMaterial)) screenMaterial.SetFloat("_VideoActive", 0);
        initialized = true;
        UpdateLocalVolume();
        ReceiveState();
        Pulse();
    }

    private void Update()
    {
        var local = Networking.LocalPlayer;
        if (!Utilities.IsValid(local)) return;
        if (!local.IsUserInVR()) {
            if (Input.GetKeyDown(KeyCode.UpArrow)) OpenMenu();
            if (Input.GetKeyDown(KeyCode.DownArrow)) CloseMenu();
        }
    }

    public override void PostLateUpdate()
    {
        var local = Networking.LocalPlayer;
        if (!Utilities.IsValid(local) || !Utilities.IsValid(wristButton)) return;
        bool visible = local.IsUserInVR();
        if (wristButton.gameObject.activeSelf != visible) wristButton.gameObject.SetActive(visible);
        if (!visible) return;
        var hand = local.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand);
        var head = local.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
        Vector3 position = hand.position + hand.rotation * new Vector3(0, .075f, .035f);
        Vector3 facing = position - head.position;
        if (facing.sqrMagnitude < .001f) facing = head.rotation * Vector3.forward;
        wristButton.SetPositionAndRotation(position, Quaternion.LookRotation(facing, head.rotation * Vector3.up));
    }

    public void OpenMenu()
    {
        var local = Networking.LocalPlayer;
        if (!Utilities.IsValid(local) || !Utilities.IsValid(menu)) return;
        var head = local.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
        Vector3 forward = head.rotation * Vector3.forward;
        forward.y = 0;
        if (forward.sqrMagnitude < .01f) forward = local.GetRotation() * Vector3.forward;
        forward.Normalize();
        menu.transform.SetPositionAndRotation(head.position + forward * .85f - Vector3.up * .08f,
            Quaternion.LookRotation(forward, Vector3.up));
        menu.SetActive(true);
        RefreshSliders();
        RefreshLabels();
    }

    public void CloseMenu() { if (Utilities.IsValid(menu)) menu.SetActive(false); }
    public void ToggleMenu() { if (Utilities.IsValid(menu) && menu.activeSelf) CloseMenu(); else OpenMenu(); }
    public override void OnPlayerRespawn(VRCPlayerApi player) { if (Utilities.IsValid(player) && player.isLocal) CloseMenu(); }

    public void LoadForEveryone()
    {
        if (!Utilities.IsValid(urlInput)) return;
        var requested = urlInput.GetUrl();
        if (VRCUrl.IsNullOrEmpty(requested)) { Notice("Paste a video URL first."); return; }
        if (!requested.Get().ToLower().StartsWith("https://")) {
            Notice("Use an HTTPS video link so PC and Quest can both request it."); return;
        }
        double now = Networking.GetServerTimeInSeconds();
        if (lastSharedLoad > 0 && now - lastSharedLoad < UrlInterval) {
            Notice("Wait a few seconds before loading another video."); return;
        }
        if (!TakeControl()) return;
        sharedUrl = requested;
        revision++;
        playbackState = 1;
        pausedAt = 0;
        timelineStart = now;
        awaitingStart = true;
        lastSharedLoad = now;
        RequestSerialization();
        ReceiveState();
    }

    private bool TakeControl()
    {
        var local = Networking.LocalPlayer;
        if (!Utilities.IsValid(local)) return false;
        if (!Networking.IsOwner(gameObject)) Networking.SetOwner(local, gameObject);
        if (!Networking.IsOwner(gameObject)) { Notice("Taking control; try the action again in a moment."); return false; }
        return true;
    }

    public void PlayForEveryone()
    {
        if (VRCUrl.IsNullOrEmpty(sharedUrl)) { Notice("Load a video first."); return; }
        if ((playbackState == 1 && !awaitingStart) || !TakeControl()) return;
        if (playbackState == 0) { pausedAt = 0; awaitingStart = true; }
        timelineStart = Networking.GetServerTimeInSeconds() - pausedAt;
        playbackState = 1;
        RequestSerialization();
        if (!prepared) RetryLocal(); else AlignPlayback();
        Notice("Playing for everyone.");
    }

    public void PauseForEveryone()
    {
        if (playbackState != 1 || !TakeControl()) return;
        pausedAt = DesiredTime();
        playbackState = 2;
        RequestSerialization();
        AlignPlayback();
        Notice("Paused for everyone.");
    }

    public void StopForEveryone()
    {
        if (!TakeControl()) return;
        playbackState = 0;
        awaitingStart = false;
        pausedAt = 0;
        RequestSerialization();
        StopLocal();
    }

    public void RetryLocal()
    {
        if (VRCUrl.IsNullOrEmpty(sharedUrl) || playbackState == 0) { Notice("Load or play a video first."); return; }
        retries = 0;
        pendingRetry = true;
        prepared = false;
        Notice("Resyncing your screen; the shared timeline is unchanged.");
        if (Time.realtimeSinceStartup >= nextLoadTime) BeginLoad();
    }

    public override void OnDeserialization() { if (initialized) ReceiveState(); }
    public override void OnPlayerJoined(VRCPlayerApi player) { if (Networking.IsOwner(gameObject)) RequestSerialization(); }
    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        // Keep the server-time origin through a host departure or an explicit control handoff.
        if (Utilities.IsValid(player) && player.isLocal) RequestSerialization();
        RefreshLabels();
    }

    private void ReceiveState()
    {
        if (!Utilities.IsValid(video)) return;
        if (playbackState == 0 || VRCUrl.IsNullOrEmpty(sharedUrl)) { StopLocal(); return; }
        if (loadedRevision != revision) {
            retries = 0;
            pendingRetry = true;
            prepared = false;
            if (Utilities.IsValid(screenMaterial)) screenMaterial.SetFloat("_VideoActive", 0);
            if (Time.realtimeSinceStartup >= nextLoadTime) BeginLoad();
            else Notice("Waiting for the video request cooldown.");
        } else {
            AlignPlayback();
            if (prepared) Notice(awaitingStart ? "Ready; waiting for the host to finish loading." : playbackState == 2 ? "Paused for everyone." : "Playing for everyone.");
        }
    }

    private void BeginLoad()
    {
        if (!Utilities.IsValid(video) || VRCUrl.IsNullOrEmpty(sharedUrl) || playbackState == 0) return;
        if (Time.realtimeSinceStartup < nextLoadTime) return;
        video.Stop();
        prepared = false;
        loading = true;
        pendingRetry = false;
        loadedRevision = revision;
        loadingSince = Time.realtimeSinceStartup;
        nextLoadTime = loadingSince + UrlInterval;
        if (Utilities.IsValid(screenMaterial)) screenMaterial.SetFloat("_VideoActive", 0);
        Notice("Loading video…");
        video.LoadURL(sharedUrl);
    }

    public override void OnVideoReady()
    {
        if (loadedRevision != revision || playbackState == 0) return;
        prepared = true;
        loading = false;
        pendingRetry = false;
        if (Utilities.IsValid(screenMaterial)) screenMaterial.SetFloat("_VideoActive", 1);
        video.SetTime(DesiredTime());
        video.Play(); // Render the first frame; OnVideoStart applies a shared pause or loading hold.
        Notice(awaitingStart ? "Ready; waiting for the host to finish loading." : playbackState == 2 ? "Paused for everyone." : "Playing for everyone.");
    }

    public override void OnVideoStart()
    {
        if (awaitingStart && !Networking.IsOwner(gameObject)) { video.Pause(); return; }
        AlignPlayback();
    }
    public override void OnVideoEnd()
    {
        if (Networking.IsOwner(gameObject)) { playbackState = 0; pausedAt = 0; awaitingStart = false; RequestSerialization(); }
        if (Networking.IsOwner(gameObject)) StopLocal();
        else {
            prepared = false; loading = false; pendingRetry = false;
            if (Utilities.IsValid(screenMaterial)) screenMaterial.SetFloat("_VideoActive", 0);
        }
        Notice("The video has ended. Load another or press Play to replay.");
    }

    public override void OnVideoError(VideoError error)
    {
        loading = false;
        prepared = false;
        if (Utilities.IsValid(screenMaterial)) screenMaterial.SetFloat("_VideoActive", 0);
        if (error == VideoError.AccessDenied) {
            Notice("This link is blocked by VRChat URL settings. Try a supported host.");
            pendingRetry = false;
        } else if (error == VideoError.InvalidURL) {
            Notice("This link is not a playable video. Try another HTTPS video URL.");
            pendingRetry = false;
        } else if (retries < 2 && playbackState != 0) {
            retries++;
            nextLoadTime = Time.realtimeSinceStartup + UrlInterval;
            pendingRetry = true;
            Notice("Video request failed; retrying shortly.");
        } else { pendingRetry = false; Notice("Video could not load. Try Resync or another video."); }
    }

    private float DesiredTime()
    {
        float value = awaitingStart ? pausedAt : playbackState == 1 ? (float)(Networking.GetServerTimeInSeconds() - timelineStart) : pausedAt;
        float duration = Utilities.IsValid(video) && prepared ? video.GetDuration() : 0;
        if (duration > 0 && !float.IsInfinity(duration) && !float.IsNaN(duration)) value = Mathf.Min(value, Mathf.Max(0, duration - .05f));
        return Mathf.Max(0, value);
    }

    private void AlignPlayback()
    {
        if (!prepared || !Utilities.IsValid(video) || playbackState == 0) return;
        if (awaitingStart) {
            if (!Networking.IsOwner(gameObject)) return;
            awaitingStart = false;
            timelineStart = Networking.GetServerTimeInSeconds() - pausedAt;
            RequestSerialization();
        }
        float desired = DesiredTime();
        if (Mathf.Abs(video.GetTime() - desired) > (playbackState == 2 ? .15f : 1.2f)) video.SetTime(desired);
        if (playbackState == 2) { if (video.IsPlaying) video.Pause(); }
        else if (!video.IsPlaying) video.Play();
    }

    private void StopLocal()
    {
        if (Utilities.IsValid(video)) video.Stop();
        prepared = false; loading = false; pendingRetry = false; loadedRevision = -1;
        if (Utilities.IsValid(screenMaterial)) screenMaterial.SetFloat("_VideoActive", 0);
        Notice("Screen idle. Paste a video URL to share a film.");
    }

    public void Pulse()
    {
        if (!slidersReady && Utilities.IsValid(worldAudio)) RefreshSliders();
        if (playbackState != 0 && loadedRevision != revision) ReceiveState();
        else if (pendingRetry && Time.realtimeSinceStartup >= nextLoadTime) BeginLoad();
        else if (prepared) AlignPlayback();
        if (loading && Time.realtimeSinceStartup - loadingSince > 40) {
            loading = false;
            Notice("This video is taking too long. Try Resync or another link.");
        }
        RefreshLabels();
        SendCustomEventDelayedSeconds(nameof(Pulse), 1f);
    }

    private void RefreshSliders()
    {
        if (!Utilities.IsValid(worldAudio)) return;
        if (Utilities.IsValid(rainVolume)) rainVolume.SetValueWithoutNotify(worldAudio.rainVolume);
        if (Utilities.IsValid(atmosphereVolume)) atmosphereVolume.SetValueWithoutNotify(worldAudio.atmosphereVolume);
        slidersReady = true;
    }
    public void RainVolumeChanged() { if (Utilities.IsValid(worldAudio) && Utilities.IsValid(rainVolume)) worldAudio.SetRainVolume(rainVolume.value); }
    public void AtmosphereVolumeChanged() { if (Utilities.IsValid(worldAudio) && Utilities.IsValid(atmosphereVolume)) worldAudio.SetAtmosphereVolume(atmosphereVolume.value); }
    public void UpdateLocalVolume() { if (Utilities.IsValid(speaker) && Utilities.IsValid(videoVolume)) speaker.volume = .35f * videoVolume.value; }
    private void Notice(string text) { notice = text; RefreshLabels(); }
    private void RefreshLabels()
    {
        if (Utilities.IsValid(statusLabel)) statusLabel.text = notice;
        if (Utilities.IsValid(ownerLabel)) {
            var owner = Networking.GetOwner(gameObject);
            ownerLabel.text = Utilities.IsValid(owner) ? "SHARED SCREEN  ·  " + owner.displayName : "SHARED SCREEN";
        }
    }
}
