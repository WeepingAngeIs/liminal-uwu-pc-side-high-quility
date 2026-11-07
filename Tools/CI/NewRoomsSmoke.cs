using System;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRC.SDK3.ClientSim;
using VRC.SDKBase;
using VRC.Udon;
using Station = VRC.SDK3.Components.VRCStation;

// Hosted PC-only integration check. Stage in Assets/Editor after authoring.
// This drives compiled Udon and native ClientSim stations, never editor proxies.
[InitializeOnLoad]
public static class NewRoomsSmoke
{
    const string Active = "Stillwater.NewRoomsSmoke.Active";
    const string Prefs = "com.vrchat.clientsim.settings";
    const double Limit = 180;
    static int phase, phaseFrame, stoppedSteps;
    static float phaseTime, formationAmount;
    static bool bound, holding;
    static Vector3 heldPoint, formationNear, foundationPosition, foundationSize;
    static Quaternion foundationRotation;
    static Transform root, ballroom, formationRoom, lounge, hub, planetRoom;
    static UdonBehaviour pit, formation, drifting, practical, gravityBoundary, gravityCore;
    static UdonBehaviour[] loungeSeats;
    static Station loungeStation;
    static BoxCollider foundation;
    static Transform[] toys, fragments, floating;
    static Vector3[] floatingBefore;
    static Light[] lamps;
    static Vector3[] assembled, scattered;
    static Transform formationSpace;
    static Movement original;

    struct Movement
    {
        internal float gravity, walk, run, strafe, jump;
        internal static Movement Read(VRCPlayerApi p)
        {
            return new Movement { gravity = p.GetGravityStrength(), walk = p.GetWalkSpeed(), run = p.GetRunSpeed(),
                strafe = p.GetStrafeSpeed(), jump = p.GetJumpImpulse() };
        }
        internal void Restore(VRCPlayerApi p)
        {
            p.SetGravityStrength(gravity); p.SetWalkSpeed(walk); p.SetRunSpeed(run);
            p.SetStrafeSpeed(strafe); p.SetJumpImpulse(jump);
        }
        internal void CheckRestored(VRCPlayerApi p, string where)
        {
            var m = Read(p);
            Check(Mathf.Abs(gravity - m.gravity) < .01f && Mathf.Abs(walk - m.walk) < .01f
                && Mathf.Abs(run - m.run) < .01f && Mathf.Abs(strafe - m.strafe) < .01f
                && Mathf.Abs(jump - m.jump) < .01f, where + ": ordinary locomotion was not restored");
        }
    }
    static NewRoomsSmoke() { EditorApplication.update += Tick; }
    public static void Begin()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Hosted batch runner only.");
        SessionState.SetBool(Active + ".HadPrefs", EditorPrefs.HasKey(Prefs));
        SessionState.SetString(Active + ".Prefs", EditorPrefs.GetString(Prefs, ""));
        SessionState.SetString(Active + ".Started", EditorApplication.timeSinceStartup.ToString("R", CultureInfo.InvariantCulture));
        SessionState.SetBool(Active, true);
        var settings = ClientSimSettings.Instance;
        settings.enableClientSim = true; settings.spawnPlayer = true; settings.hideMenuOnLaunch = true;
        settings.initializationDelay = 0; ClientSimSettings.SaveSettings(settings);
        EditorSceneManager.OpenScene(WorldAuthor.PCScene);
        EditorApplication.isPlaying = true;
    }
    static void Finish(bool success, string message)
    {
        SessionState.SetBool(Active, false);
        if (SessionState.GetBool(Active + ".HadPrefs", false)) EditorPrefs.SetString(Prefs, SessionState.GetString(Active + ".Prefs", ""));
        else EditorPrefs.DeleteKey(Prefs);
        if (success) Debug.Log("NEW_ROOMS_CLIENTSIM_PASS " + message);
        else Debug.LogError("NEW_ROOMS_CLIENTSIM_FAIL phase=" + phase + " " + message);
        EditorApplication.Exit(success ? 0 : 1);
    }
    static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    static T Value<T>(UdonBehaviour vm, string field)
    {
        object value = vm.GetProgramVariable(field);
        Check(value is T, vm.name + "." + field + " missing/wrong runtime type"); return (T)value;
    }
    static UdonBehaviour Port(string name)
    {
        var matches = UnityEngine.Object.FindObjectsOfType<UdonBehaviour>(true).Where(v => v.name == name).ToArray();
        Check(matches.Length == 1, "Expected exactly one Udon VM named " + name); return matches[0];
    }
    static Transform Room(string name) { var r = root.Find(name); Check(r, "Missing district " + name); return r; }
    static void Move(VRCPlayerApi p, Vector3 target)
    {
        p.TeleportTo(target, Quaternion.identity); p.SetVelocity(Vector3.zero);
    }
    static void Hold(VRCPlayerApi p, Vector3 target)
    {
        holding = true; heldPoint = target; p.SetGravityStrength(0); Move(p, target);
    }
    static void Next(string message)
    {
        Debug.Log("NEW_ROOMS_CLIENTSIM_CHECK " + message);
        phase++; phaseTime = Time.time; phaseFrame = Time.frameCount;
    }
    static bool Ready(float seconds = .5f, int frames = 3)
    { return Time.time - phaseTime >= seconds && Time.frameCount - phaseFrame >= frames; }
    static bool Finite(Vector3 v)
    {
        return !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z)
            && !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
    }
    static void Bind(VRCPlayerApi p)
    {
        // Visual reviews render separately. Keep tracking/ClientSim cameras alive
        // while removing software-rendering cost from a behaviour test.
        foreach (var camera in UnityEngine.Object.FindObjectsOfType<Camera>(true)) {
            camera.cullingMask = 0; camera.clearFlags = CameraClearFlags.SolidColor;
        }
        root = GameObject.Find("liminal uwu").transform;
        ballroom = Room("ballroom"); formationRoom = Room("formation"); lounge = Room("lounge"); hub = Room("hub"); planetRoom = Room("planet");
        pit = Port("Local bouncy uwu physics"); formation = Port("Local reconstruction");
        drifting = Port("Suspended street objects"); practical = Port("Nearby practical lights");
        gravityBoundary = Port("Gravity room boundary"); gravityCore = Port("BobyStar local gravity controller");
        Check(Value<UdonBehaviour>(gravityBoundary, "controller") == gravityCore, "Gravity boundary lost the licensed controller binding");
        Check(Value<bool>(gravityCore, "isReady"), "Licensed gravity controller hierarchy is not ready");
        toys = Value<Transform[]>(pit, "balls");
        Check(toys.Length == 24 && Value<float[]>(pit, "radii").Length == toys.Length, "Expected 24 bound local uwu toys");
        foreach (var toy in toys) {
            Check(toy && toy.IsChildOf(ballroom), "Ball escaped its authored room");
            Check(toy.GetComponentsInChildren<Collider>(true).Length == 0 && toy.GetComponentsInChildren<Rigidbody>(true).Length == 0
                && toy.GetComponentsInChildren<UdonBehaviour>(true).Length == 0, "A ball has extra physics or per-ball VM overhead");
            Check(!toy.GetComponents<Component>().Any(c => c && c.GetType().Name.IndexOf("Pickup", StringComparison.OrdinalIgnoreCase) >= 0), "A ball is grabbable");
            var renderer = toy.GetComponent<Renderer>();
            Check(renderer && renderer.sharedMaterial.IsKeywordEnabled("SW_FIXTURE_VOLUME")
                && renderer.sharedMaterial.GetTexture("_FixtureColor") is Texture3D, "Ball lacks baked coloured fixture lighting");
        }
        fragments = Value<Transform[]>(formation, "pieces"); formationSpace = Value<Transform>(formation, "space");
        assembled = Value<Vector3[]>(formation, "assembledPositions"); scattered = Value<Vector3[]>(formation, "scatteredPositions");
        var influence = Value<Vector3[]>(formation, "influencePoints");
        Check(fragments.Length >= 16 && fragments.Length == assembled.Length && assembled.Length == scattered.Length
            && scattered.Length == influence.Length, "Formation arrays are incomplete");
        Check(fragments.All(t => t && t.GetComponentsInChildren<Collider>(true).Length == 0), "Reconstructing decoration can move collision floors/walls");
        formationNear = formationSpace.TransformPoint(influence[0] + Vector3.up * .15f);
        foundation = formationRoom.GetComponentsInChildren<BoxCollider>(true).Single(c =>
            Vector3.Distance(c.size, new Vector3(64, .8f, 70)) < .01f);
        foundationPosition = foundation.transform.position; foundationRotation = foundation.transform.rotation; foundationSize = foundation.size;
        loungeSeats = lounge.GetComponentsInChildren<UdonBehaviour>(true).Where(v => v.name.StartsWith("Lounge seat ", StringComparison.Ordinal)).OrderBy(v => v.name).ToArray();
        Check(loungeSeats.Length == 4, "Expected four lounge seats");
        foreach (var seat in loungeSeats) {
            var station = Value<Station>(seat, "seat");
            Check(station && station.gameObject == seat.gameObject && station.stationEnterPlayerLocation && station.stationExitPlayerLocation
                && !station.disableStationExit && station.seated, "Lounge chair lacks a usable native station and free exit");
        }
        loungeStation = Value<Station>(loungeSeats[0], "seat");
        floating = Value<Transform[]>(drifting, "objects").Where(t => t && t.parent == hub).ToArray();
        Check(floating.Length >= 5, "Hub lacks varied floating street props");
        foreach (string kind in new[] { "car", "streetlight", "lamp", "bin", "bush" })
            Check(floating.Any(t => t.name.StartsWith("Suspended " + kind + " ", StringComparison.Ordinal)), "Missing lifted " + kind);
        lamps = Value<Light[]>(practical, "lamps");
        Check(lamps.Length > 0 && lamps.All(l => l && l.shadows == LightShadows.None), "PC practical lights are absent or retain shadow cameras");
        original = Movement.Read(p); bound = true;
    }
    static void CheckFoundation()
    {
        Check(foundation && foundation.enabled && foundation.gameObject.activeInHierarchy, "Formation foundation was disabled");
        Check(Vector3.Distance(foundationPosition, foundation.transform.position) < .001f
            && Quaternion.Angle(foundationRotation, foundation.transform.rotation) < .01f
            && Vector3.Distance(foundationSize, foundation.size) < .001f, "Formation foundation moved with decorative fragments");
        RaycastHit hit;
        Check(foundation.Raycast(new Ray(formationNear + Vector3.up * 3, Vector3.down), out hit, 6), "Formation lost the walkable foundation below the visitor");
    }
    static void SeedAndCheckContacts(VRCPlayerApi p)
    {
        pit.SendCustomEvent("ResetBalls");
        var positions = Value<Vector3[]>(pit, "positions"); var velocities = Value<Vector3[]>(pit, "velocities");
        positions[0] = new Vector3(-.85f, 1, 0); velocities[0] = new Vector3(3, 0, 0);
        positions[1] = new Vector3(6, .08f, 0); velocities[1] = new Vector3(0, -3, 0);
        positions[2] = new Vector3(-9, 3, 0); positions[3] = new Vector3(-7.8f, 3, 0);
        velocities[2] = Vector3.right * 3; velocities[3] = Vector3.left * 3;
        Vector3 extent = Value<Vector3>(pit, "halfExtent");
        positions[4] = new Vector3(extent.x + 2, 7, 4); velocities[4] = Vector3.right * 7;
        pit.SetProgramVariable("positions", positions); pit.SetProgramVariable("velocities", velocities);
        int players = Value<int>(pit, "playerBounces"), floor = Value<int>(pit, "boundaryBounces"), pairs = Value<int>(pit, "pairBounces");
        Vector3 before = p.GetPosition(), speed = p.GetVelocity(); var movement = Movement.Read(p);
        pit.SendCustomEvent("SimulateStep");
        positions = Value<Vector3[]>(pit, "positions"); velocities = Value<Vector3[]>(pit, "velocities");
        var radii = Value<float[]>(pit, "radii");
        Check(Value<int>(pit, "playerBounces") > players && Mathf.Abs(positions[0].x) > radii[0] && velocities[0].x < 0,
            "Player capsule did not reverse and separate the incoming ball");
        Check(Value<int>(pit, "boundaryBounces") > floor && positions[1].y >= radii[1] && velocities[1].y > 0, "Floor did not bounce the falling ball");
        Check(Value<int>(pit, "pairBounces") > pairs && Vector3.Distance(positions[2], positions[3]) >= radii[2] + radii[3] - .001f,
            "Closing ball pair did not separate and bounce");
        Check(positions[4].x <= extent.x - radii[4] + .001f && velocities[4].x < 0, "An out-of-bounds ball was not contained and reflected");
        Check(Vector3.Distance(before, p.GetPosition()) < .001f && Vector3.Distance(speed, p.GetVelocity()) < .001f,
            "Toy contacts moved or kicked the player");
        movement.CheckRestored(p, "toy contacts");
        Check(positions.All(Finite) && velocities.All(v => Finite(v) && v.magnitude <= 8.001f), "Toy simulation produced nonfinite/excessive velocity");
    }
    static void PlaceGravity(Vector3 local, Vector3 up)
    {
        gravityCore.SetProgramVariable("API_TeleportToPosition", planetRoom.TransformPoint(local));
        gravityCore.SetProgramVariable("API_TeleportToRotation", Quaternion.LookRotation(Vector3.forward, up));
        gravityCore.SendCustomEvent("APITeleportTo");
    }
    static void CheckGravityPose(Vector3 target, Vector3 up)
    {
        Check(Value<bool>(gravityCore, "_isOn"), "Wall/ceiling walk unexpectedly exited its station");
        Check(Vector3.Dot(Value<Vector3>(gravityCore, "_upDir").normalized, up) > .6f, "Gravity did not follow the requested wall/ceiling orientation");
        var rigidbody = Value<Rigidbody>(gravityCore, "rb");
        Check(Finite(rigidbody.position) && Finite(rigidbody.velocity) && Vector3.Distance(rigidbody.position, planetRoom.TransformPoint(target)) < 3,
            "Gravity controller escaped the authored wall/ceiling surface");
        var station = Value<Station>(gravityCore, "station");
        Check(station && Vector3.Dot(station.stationEnterPlayerLocation.up, up) > .5f, "Native gravity station stayed upright on a wall/ceiling");
    }
    static void Tick()
    {
        if (!SessionState.GetBool(Active, false)) return;
        try {
            double started = double.Parse(SessionState.GetString(Active + ".Started", "0"), CultureInfo.InvariantCulture);
            Check(EditorApplication.timeSinceStartup - started < Limit, "180 s timeout");
            if (!EditorApplication.isPlaying || !ClientSimMain.HasInstance() || !Utilities.IsValid(Networking.LocalPlayer)) return;
            var player = Networking.LocalPlayer;
            if (!bound) {
                if (Time.time < 3) return;
                Bind(player); phaseTime = Time.time; phaseFrame = Time.frameCount;
            }
            if (holding) { player.SetGravityStrength(0); Move(player, heldPoint); }
            switch (phase) {
                case 0:
                    Hold(player, ballroom.position + new Vector3(0, .2f, 0));
                    SeedAndCheckContacts(player);
                    Next("24 non-grabbable local toys: compiled player, floor and pair contacts bounce balls without changing player locomotion"); break;
                case 1:
                    if (!Ready(.4f)) return;
                    Check(Value<bool>(pit, "simulationActive") && Value<int>(pit, "simulationSteps") > 1, "Nearby toy room did not run its timed simulation");
                    Hold(player, hub.position + new Vector3(0, .2f, -15));
                    Next("toy physics updates while the visitor is in the room"); break;
                case 2:
                    if (!Ready(.4f)) return;
                    Check(!Value<bool>(pit, "simulationActive"), "Remote toy physics stayed active");
                    stoppedSteps = Value<int>(pit, "simulationSteps");
                    floatingBefore = floating.Select(t => t.localPosition).ToArray();
                    Next("toy physics pauses when the room is left; observing lifted street objects"); break;
                case 3:
                    if (!Ready(1.2f)) return;
                    Check(Value<int>(pit, "simulationSteps") == stoppedSteps, "Distant ball room still spends simulation steps");
                    Vector3[] changes = floating.Select((t, i) => t.localPosition - floatingBefore[i]).ToArray();
                    Check(changes.Count(v => v.sqrMagnitude > .00001f) >= 3 && changes.Any(v => (v - changes[0]).sqrMagnitude > .00001f),
                        "Floating props did not move at distinct phases");
                    Hold(player, formationNear);
                    Next("street cars, lamps, bins and bushes drift independently, without a synchronized debris line"); break;
                case 4:
                    if (!Ready(1.3f)) return;
                    CheckFoundation();
                    formationAmount = Value<float[]>(formation, "amounts")[0];
                    Check(Value<bool[]>(formation, "forming")[0] && formationAmount > .05f && formationAmount < .95f
                        && Vector3.Distance(fragments[0].localPosition, scattered[0]) > .01f, "Approaching did not slowly form the first fragment");
                    Hold(player, hub.position + new Vector3(0, .2f, -15));
                    Next("nearby reconstruction begins gradually above a permanent physical floor"); break;
                case 5:
                    if (!Ready(.65f)) return;
                    CheckFoundation();
                    float receding = Value<float[]>(formation, "amounts")[0];
                    Check(!Value<bool[]>(formation, "forming")[0] && receding > 0 && receding < formationAmount - .015f, "Moving away did not reverse the partly built room gradually");
                    formationAmount = receding; Hold(player, formationNear);
                    Next("moving away reverses incomplete formation without snapping or removing the foundation"); break;
                case 6:
                    if (!Ready(.65f)) return;
                    Check(Value<bool[]>(formation, "forming")[0] && Value<float[]>(formation, "amounts")[0] > formationAmount + .03f,
                        "Returning during dissolution did not reverse toward assembly");
                    Next("returning before dissolution completes resumes assembly from the current state"); break;
                case 7:
                    if (!Ready(Value<float>(formation, "assembleSeconds") + .7f)) return;
                    CheckFoundation();
                    Check(Value<float[]>(formation, "amounts")[0] > .999f && Vector3.Distance(fragments[0].localPosition, assembled[0]) < .01f,
                        "Nearby fragment did not finish at its authored assembled position");
                    Hold(player, hub.position + new Vector3(0, .2f, -15));
                    Next("nearby fragments finish precisely at their authored architectural positions"); break;
                case 8:
                    if (!Ready(Value<float>(formation, "scatterSeconds") + .7f)) return;
                    CheckFoundation();
                    Check(Value<float[]>(formation, "amounts")[0] < .001f && Vector3.Distance(fragments[0].localPosition, scattered[0]) < .01f,
                        "Distant fragment did not finish dissolving");
                    holding = false; original.Restore(player); Move(player, loungeStation.stationEnterPlayerLocation.position);
                    loungeSeats[0].Interact();
                    Next("distant formation fully disperses while its physical foundation remains; entering a lounge chair"); break;
                case 9:
                    if (!Ready(.8f)) return;
                    Check(Value<bool>(loungeSeats[0], "localSeated") && Vector3.Distance(player.GetPosition(), loungeStation.stationEnterPlayerLocation.position) < 2,
                        "Native lounge station did not seat the local player");
                    loungeStation.ExitStation(player);
                    Next("lounge chair interaction enters its native VRChat station"); break;
                case 10:
                    if (!Ready(.8f)) return;
                    Check(!Value<bool>(loungeSeats[0], "localSeated") && Vector3.Distance(player.GetPosition(), loungeStation.stationExitPlayerLocation.position) < 2,
                        "Lounge chair did not release the player at its clear exit");
                    original.CheckRestored(player, "lounge exit");
                    Hold(player, lamps[0].transform.position + Vector3.down * 1.5f);
                    Next("lounge chair exits freely without retaining a seated flag or altering locomotion"); break;
                case 11:
                    if (!Ready(.8f)) return;
                    int count = lamps.Count(l => l.enabled);
                    Check(count > 0 && count <= 4 && Value<int>(practical, "enabledCount") == count, "PC light pool failed its four-lamp limit");
                    Check(lamps.Where(l => l.enabled).All(l => (l.transform.position - player.GetPosition()).sqrMagnitude < 257), "A remote fixture remained enabled");
                    Hold(player, new Vector3(-4000, 10, -4000));
                    Next("only up to four nearby shadowless PC practical lights illuminate the visitor"); break;
                case 12:
                    if (!Ready(.8f)) return;
                    Check(Value<int>(practical, "enabledCount") == 0 && lamps.All(l => !l.enabled), "Distant practical lights did not switch off");
                    holding = false; original.Restore(player); Move(player, planetRoom.position + new Vector3(0, 1.05f, 0));
                    gravityBoundary.SendCustomEvent("EnterGravity");
                    Next("all practical lights switch off outside their range; entering the licensed wall-gravity controller"); break;
                case 13:
                    if (!Ready(.8f)) return;
                    Check(Value<bool>(gravityCore, "_isOn") && Value<bool>(gravityCore, "isReady"), "Gravity room did not enter the licensed movement controller");
                    PlaceGravity(new Vector3(25.2f, 27, 0), Vector3.left);
                    Next("the gravity room activates the original controller through its native station"); break;
                case 14:
                    if (!Ready(.7f)) return;
                    CheckGravityPose(new Vector3(25.2f, 27, 0), Vector3.left);
                    PlaceGravity(new Vector3(0, 52.2f, 0), Vector3.down);
                    Next("licensed controller maintains real sideways wall orientation with finite physical motion"); break;
                case 15:
                    if (!Ready(.7f)) return;
                    CheckGravityPose(new Vector3(0, 52.2f, 0), Vector3.down);
                    gravityBoundary.SendCustomEvent("ExitGravity");
                    Next("licensed controller maintains upside-down ceiling orientation; requesting ordinary-floor return"); break;
                case 16:
                    if (!Ready(1.8f, 14)) return;
                    Check(!Value<bool>(gravityCore, "_isOn") && !Value<bool>(gravityBoundary, "returning"), "Gravity station did not complete its staged exit");
                    var exit = Value<Transform>(gravityBoundary, "southReturn");
                    Check(Vector3.Distance(player.GetPosition(), exit.position) < 2, "Gravity exit did not reach the ordinary south lobby");
                    original.CheckRestored(player, "gravity return");
                    if (Time.time < Value<float>(gravityBoundary, "eligibleAt") + .1f) return;
                    Move(player, planetRoom.position + new Vector3(0, 1.05f, 0)); gravityBoundary.SendCustomEvent("EnterGravity");
                    Next("wall/ceiling station returns upright to the clear lobby with ordinary locomotion restored"); break;
                case 17:
                    if (!Ready(.8f)) return;
                    Check(Value<bool>(gravityCore, "_isOn"), "Gravity controller did not permit a second visit");
                    gravityBoundary.SendCustomEvent("ExitGravity"); player.Respawn();
                    Next("respawning during staged gravity exit tests cancellation of a queued return"); break;
                case 18:
                    if (!Ready(1.4f, 16)) return;
                    Check(!Value<bool>(gravityCore, "_isOn") && !Value<bool>(gravityBoundary, "returning"), "Respawn retained gravity activity or a queued return");
                    var arrival = root.Find("World descriptor/Arrival");
                    Check(arrival && Vector3.Distance(player.GetPosition(), arrival.position) < 3, "Delayed gravity return pulled the respawned player away from spawn");
                    original.CheckRestored(player, "gravity respawn"); CheckFoundation();
                    Finish(true, "Compiled Udon/ClientSim verified 24 local non-grabbable balls, player/floor/pair bounce with no player forces, distant sleep, independent floating props, slow reversible reconstruction over a permanent floor, four lounge stations with native enter/exit, four-light PC budget/range shutdown, original licensed sideways and ceiling gravity, ordinary return and respawn cancellation. No headset frame-rate or multi-client synchronization measurement."); break;
            }
        } catch (Exception e) { Finish(false, e.ToString()); }
    }
}
