using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class WorldAudioDirector : UdonSharpBehaviour
{
    public Vector3[] zoneMin, zoneMax;
    public AudioClip[] atmosphereClips;
    public bool[] rainy;
    public AudioSource atmosphere, rain;
    public float rainVolume = .35f, atmosphereVolume = .35f;
    private int current = -2;

    private void Start() { SampleRoom(); }
    public void SetRainVolume(float value) { rainVolume = Mathf.Clamp01(value); ApplyVolume(); }
    public void SetAtmosphereVolume(float value) { atmosphereVolume = Mathf.Clamp01(value); ApplyVolume(); }
    private void ApplyVolume()
    {
        if (Utilities.IsValid(atmosphere)) atmosphere.volume = atmosphereVolume * .22f;
        if (Utilities.IsValid(rain)) rain.volume = rainVolume * .16f;
    }
    public void SampleRoom()
    {
        var player = Networking.LocalPlayer;
        if (Utilities.IsValid(player)) {
            Vector3 p = player.GetPosition() + Vector3.up * .1f;
            int selected = -1;
            for (int i = 0; i < zoneMin.Length; i++) {
                Vector3 a = zoneMin[i], b = zoneMax[i];
                if (p.x >= a.x && p.x <= b.x && p.y >= a.y && p.y <= b.y && p.z >= a.z && p.z <= b.z) {
                    selected = i; break;
                }
            }
            if (selected != current) {
                current = selected;
                atmosphere.Stop();
                atmosphere.clip = selected < 0 ? null : atmosphereClips[selected];
                if (Utilities.IsValid(atmosphere.clip)) atmosphere.Play();
                bool wet = selected >= 0 && rainy[selected];
                if (wet && !rain.isPlaying) rain.Play();
                else if (!wet && rain.isPlaying) rain.Stop();
                ApplyVolume();
            }
        }
        SendCustomEventDelayedSeconds(nameof(SampleRoom), .1f);
    }
}
