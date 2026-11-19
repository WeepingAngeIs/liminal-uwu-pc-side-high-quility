using System;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.ClientSim;
using VRC.SDK3.Persistence;
using VRC.SDKBase;
using VRC.Udon;

// Uses the compiled Udon VM, real ClientSim Interact and its file-backed local PlayerData.
[InitializeOnLoad]
public static class ObservatoryPrototypeSmoke
{
    const string State = "Liminal.Observatory.Smoke.";
    const string SaveKey = "liminal.observatory.receiver.v1";
    const string Pref = "com.vrchat.clientsim.settings";
    static UdonBehaviour receiver, spool, clock;
    static Transform pendulum;
    static Text desk;
    static GameObject secondSpool;
    static int stage;
    static float fittedAt, nearDeskAt;
    [Serializable] class Result
    {
        public string revision, runtime, recordAfterSpending, restoredRecord, unlockedBenchPhotoSha256;
        public bool insufficientPurchaseRejected, earnedInOtherDistrict, clockRepeatSafe, secondSpoolEarnedExtra, passed;
    }

    static ObservatoryPrototypeSmoke() { EditorApplication.update += Tick; }
    public static void Begin()
    {
        Check(Application.isBatchMode, "Hosted prototype runtime test only.");
        SessionState.SetBool(State+"HadPrefs", EditorPrefs.HasKey(Pref));
        SessionState.SetString(State+"Prefs", EditorPrefs.GetString(Pref,""));
        SessionState.SetString(State+"Start", EditorApplication.timeSinceStartup.ToString("R", CultureInfo.InvariantCulture));
        SessionState.SetBool(State+"Active",true);
        SessionState.SetInt(State+"Visit",1);
        var settings = ClientSimSettings.Instance;
        settings.enableClientSim = true; settings.spawnPlayer = true; settings.hideMenuOnLaunch = true;
        settings.initializationDelay = 0; ClientSimSettings.SaveSettings(settings);
        EditorSceneManager.OpenScene(ObservatoryPrototype.PCScene);
        var instrument = UnityEngine.Object.FindObjectsOfType<ObservatoryReceiver>(true).Single();
        var controls = UnityEngine.Object.FindObjectsOfType<ObservatoryControl>(true);
        SessionState.SetString(State+"Receiver",PathTo(instrument.transform));
        SessionState.SetString(State+"Spool",PathTo(controls.Single(c => !c.recordsClock).transform));
        SessionState.SetString(State+"Clock",PathTo(controls.Single(c => c.recordsClock).transform));
        EditorApplication.isPlaying = true;
    }
    static string PathTo(Transform t) { return t.parent ? PathTo(t.parent)+"/"+t.name : "/"+t.name; }
    static UdonBehaviour Port(string name) { return GameObject.Find(SessionState.GetString(State+name,"")).GetComponent<UdonBehaviour>(); }
    static T Read<T>(string variable) { return (T)receiver.GetProgramVariable(variable); }
    static string Record()
    {
        return PlayerData.TryGetString(Networking.LocalPlayer, SaveKey, out string value) ? value : "";
    }
    static int Lifetime()
    {
        string record = Record();
        return string.IsNullOrEmpty(record) ? 0 : int.Parse(record.Split('|')[0], CultureInfo.InvariantCulture);
    }
    static void Check(bool value,string message) { if (!value) throw new InvalidOperationException(message); }
    static void Travel(VRCPlayerApi player, Vector3 position)
    {
        player.TeleportTo(position, Quaternion.identity); player.SetVelocity(Vector3.zero);
    }

    static void Tick()
    {
        if (!SessionState.GetBool(State+"Active",false)) return;
        try {
            double start=double.Parse(SessionState.GetString(State+"Start","0"),CultureInfo.InvariantCulture);
            Check(EditorApplication.timeSinceStartup-start<420, "Timeout in phase "+stage+" on visit "+SessionState.GetInt(State+"Visit",0));
            if (SessionState.GetInt(State+"Visit",0)==2 && !EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode) {
                SessionState.SetInt(State+"Visit",3);
                EditorSceneManager.OpenScene(ObservatoryPrototype.PCScene);
                EditorApplication.isPlaying = true;
                return;
            }
            if (SessionState.GetInt(State+"Visit",0)==2 || !EditorApplication.isPlaying || !ClientSimMain.HasInstance()
                || !Utilities.IsValid(Networking.LocalPlayer) || Time.time<1) return;
            var player=Networking.LocalPlayer;
            if (!receiver) {
                receiver=Port("Receiver"); clock=Port("Clock"); spool=Port("Spool");
                foreach (var cam in UnityEngine.Object.FindObjectsOfType<Camera>(true)) {
                    cam.cullingMask=0; cam.clearFlags=CameraClearFlags.SolidColor;
                }
            }
            desk=Read<Text>("receiverDisplay");
            if (!desk || !desk.text.Contains("/ minute")) return;
            pendulum=Read<Transform>("pendulum");
            secondSpool=Read<GameObject>("secondSpool");
            if (SessionState.GetInt(State+"Visit",1)==3) {
                Check(Lifetime()>=9 && pendulum.gameObject.activeSelf && secondSpool.activeSelf && desk.text.StartsWith("3 readings"),
                    "Saved receiver did not restore on second visit.");
                string restored=Record();
                Check(restored.Split('|')[3]=="1" && restored.Split('|')[4]=="1","Restored flags differ.");
                var r=new Result {revision=File.ReadAllText("Builds/author-revision.txt").Trim(),
                    runtime="Desktop ClientSim / compiled Udon / fresh local player plus second play session",
                    recordAfterSpending=SessionState.GetString(State+"Spent",""), restoredRecord=restored,
                    unlockedBenchPhotoSha256=SessionState.GetString(State+"Image",""), insufficientPurchaseRejected=true,
                    earnedInOtherDistrict=true,clockRepeatSafe=true,secondSpoolEarnedExtra=true,passed=true};
                File.WriteAllText(ObservatoryPrototypeReview.Output+"/runtime.json",JsonUtility.ToJson(r,true));
                Finish(true,"Second visit restored personal readings, pendulum and spool.");
                return;
            }
            switch(stage) {
                case 0:
                    Check(Lifetime()==0,"Expected a fresh receiver.");
                    Check(!pendulum.gameObject.activeSelf && !secondSpool.activeSelf && desk.text.StartsWith("0 readings"),"Fresh device display incorrect.");
                    spool.Interact();
                    Check(!secondSpool.activeSelf && desk.text.Contains("Need 6 more"),"Unaffordable spool was accepted.");
                    Travel(player,new Vector3(-72,.2f,-15.4f)); stage++; break;
                case 1:
                    if (Lifetime()<1) return;
                    Check(Vector3.Distance(player.GetPosition(),clock.transform.position)<10,"Player did not earn the first reading away in Ceiling Ward.");
                    clock.Interact(); string first=Record(); clock.Interact();
                    Check(first==Record() && pendulum.gameObject.activeSelf && first.Split('|')[4]=="1","Duplicate or missing clock discovery.");
                    stage++; break;
                case 2:
                    if (Lifetime()<6) return;
                    Travel(player,new Vector3(-15.3f,.2f,11f)); nearDeskAt=Time.realtimeSinceStartup; stage++; break;
                case 3:
                    if (Time.realtimeSinceStartup-nearDeskAt<1) return;
                    spool.Interact(); string spent=Record(); spool.Interact();
                    Check(secondSpool.activeSelf && spent==Record() && desk.text.StartsWith("0 readings\n3 / minute"),
                        "Spool purchase or repeated purchase changed the wrong balance.");
                    SessionState.SetString(State+"Spent",spent); fittedAt=Time.realtimeSinceStartup;
                    stage++; break;
                case 4:
                    if (Time.realtimeSinceStartup-fittedAt<61 || Lifetime()<9) return;
                    Check(Lifetime()==9 && desk.text.StartsWith("3 readings\n3 / minute"), "Extra spool did not produce three readings in a minute.");
                    var cam = new GameObject("Unlocked receiver camera").AddComponent<Camera>();
                    cam.enabled=false; cam.allowHDR=false; cam.allowMSAA=false; cam.useOcclusionCulling=false;
                    cam.clearFlags=CameraClearFlags.Skybox; cam.nearClipPlane=.04f; cam.farClipPlane=500;
                    try {
                        byte[] photo=ObservatoryPrototypeReview.Photograph(cam,new Vector3(-15.3f,1.64f,11),
                            new Vector3(-15.1f,1.25f,9.15f),56);
                        File.WriteAllBytes(ObservatoryPrototypeReview.Output+"/PC/unlocked-receiver.png",photo);
                        SessionState.SetString(State+"Image",ObservatoryPrototypeReview.Digest(photo));
                    } finally { UnityEngine.Object.Destroy(cam.gameObject); }
                    stage++; break;
                case 5:
                    // PlayerData updates encode on LateUpdate. Leave only after a subsequent
                    // frame, once the submitted transaction has had a chance to be written.
                    SessionState.SetInt(State+"Visit",2);
                    EditorApplication.isPlaying=false; break;
            }
        } catch (Exception ex) { Finish(false,ex.ToString()); }
    }
    static void Finish(bool ok,string message)
    {
        SessionState.SetBool(State+"Active",false);
        if(SessionState.GetBool(State+"HadPrefs",false)) EditorPrefs.SetString(Pref,SessionState.GetString(State+"Prefs",""));
        else EditorPrefs.DeleteKey(Pref);
        if(ok) Debug.Log("OBSERVATORY_CLIENTSIM_PASS "+message); else Debug.LogError("OBSERVATORY_CLIENTSIM_FAIL "+message);
        EditorApplication.Exit(ok?0:1);
    }
}
