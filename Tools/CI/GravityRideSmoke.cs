using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRC.SDK3.ClientSim;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;

// Optional PC-only licensed-runner check. Place in Assets/Editor on the runner.
// Use -executeMethod GravityRideSmoke.Begin and omit -quit. Exit code reflects the result.
[InitializeOnLoad]
public static class GravityRideSmoke
{
    const string Active = "Stillwater.GravityRideSmoke.Active";
    const string Prefs = "com.vrchat.clientsim.settings";
    static double started,phaseStart;
    static int phase,mask;
    static float gravity;

    static GravityRideSmoke() { EditorApplication.update += Tick; }
    public static void Begin()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Batch runner only.");
        SessionState.SetBool(Active+".HadPrefs",EditorPrefs.HasKey(Prefs));
        SessionState.SetString(Active+".PreviousPrefs",EditorPrefs.GetString(Prefs,""));
        SessionState.SetBool(Active,true);
        var settings=ClientSimSettings.Instance;
        settings.enableClientSim=true; settings.spawnPlayer=true; settings.hideMenuOnLaunch=true;
        settings.initializationDelay=0; ClientSimSettings.SaveSettings(settings);
        EditorSceneManager.OpenScene(WorldAuthor.PCScene);
        EditorApplication.isPlaying=true;
    }

    static void Finish(bool success,string message)
    {
        SessionState.SetBool(Active,false);
        if (SessionState.GetBool(Active+".HadPrefs",false))
            EditorPrefs.SetString(Prefs,SessionState.GetString(Active+".PreviousPrefs",""));
        else EditorPrefs.DeleteKey(Prefs);
        if(success) Debug.Log("GRAVITY_CLIENTSIM_PASS "+message);
        else Debug.LogError("GRAVITY_CLIENTSIM_FAIL "+message);
        EditorApplication.Exit(success?0:1);
    }

    static UdonBehaviour Port(string name, string wing=null)
    {
        var results=UnityEngine.Object.FindObjectsOfType<UdonBehaviour>().Where(v=>v.name==name);
        if (wing!=null) results=results.Where(v=>v.transform.parent.parent.name==wing);
        return results.Single();
    }
    static void Next(double now) { phase++; phaseStart=now; }
    static void Check(bool value,string message) { if(!value) throw new Exception(message); }
    static bool Near(VRCPlayerApi p, Transform t) { return (p.GetPosition()-t.position).sqrMagnitude<4; }
    static void Tick()
    {
        if(!SessionState.GetBool(Active,false) || !EditorApplication.isPlaying) return;
        double now=EditorApplication.timeSinceStartup;
        if(started==0) {started=now;phaseStart=now;}
        try
        {
            Check(now-started<125,"125 s timeout in phase "+phase);
            if(!ClientSimMain.HasInstance() || !Utilities.IsValid(Networking.LocalPlayer)) return;
            var player=Networking.LocalPlayer;
            var ride=Port("Six direction ride");
            var home=ride.GetProgramVariable("exit") as Transform;
            if(home==null || now-started<3) return;
            bool ready=now-phaseStart>.7;
            switch(phase)
            {
                case 0:
                    gravity=player.GetGravityStrength();
                    Port("Ceiling walk").Interact(); Next(now); break;
                case 1:
                    if(!ready) return;
                    Check(Near(player,(Transform)Port("Ceiling walk").GetProgramVariable("destination")),"ceiling arriving");
                    Port("Return to the floor","Soffit gallery").Interact(); Next(now); break;
                case 2:
                    if(!ready) return;
                    Check(Near(player,home),"ceiling return"); Port("Wall walk").Interact(); Next(now); break;
                case 3:
                    if(!ready) return;
                    Check(Near(player,(Transform)Port("Wall walk").GetProgramVariable("destination")),"wall arriving");
                    Port("Northern exit","Western facade gallery").Interact(); Next(now); break;
                case 4:
                    if(!ready) return;
                    Check(Near(player,(Transform)Port("Northern exit","Western facade gallery").GetProgramVariable("destination")),"wall shortcut");
                    ride.Interact(); Next(now); break;
                case 5:
                    Vector3 v=player.GetVelocity();
                    if(v.y>1) mask|=1; if(v.x>1) mask|=2; if(v.z>1) mask|=4;
                    if(v.y< -1) mask|=8; if(v.x< -1) mask|=16; if(v.z< -1) mask|=32;
                    if(!Near(player,home)) return;
                    Check(mask==63,"missing velocity axes: observed mask "+mask);
                    Check(Mathf.Abs(gravity-player.GetGravityStrength())<.01f,"ride did not restore prior gravity");
                    ride.Interact(); Next(now); break;
                case 6:
                    if(!ready || Time.time<1) return;
                    Check(player.GetGravityStrength()<.01f,"ride did not enter zero gravity");
                    ride.RunInputEvent(UdonManager.UDON_INPUT_JUMP,new UdonInputEventArgs(true,HandType.RIGHT));
                    Next(now); break;
                case 7:
                    if(!ready) return;
                    Check(Near(player,home) && Mathf.Abs(gravity-player.GetGravityStrength())<.01f,"jump cancellation");
                    ride.Interact(); Next(now); break;
                case 8:
                    if(!ready) return;
                    player.Respawn(); Next(now); break;
                case 9:
                    if(!ready) return;
                    Check(Mathf.Abs(gravity-player.GetGravityStrength())<.01f,"respawn gravity");
                    Check(player.GetPosition().z>-100,"respawn left player in isolated region");
                    Finish(true,"PC simulation verified two reversible walking orientations, six local velocity directions, timed return, jump cancel and respawn gravity restoration.");
                    break;
            }
        }
        catch(Exception e) { Finish(false,e.ToString()); }
    }
}
