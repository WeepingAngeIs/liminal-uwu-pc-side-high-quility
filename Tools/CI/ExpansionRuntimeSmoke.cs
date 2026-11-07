using System;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.ClientSim;
using VRC.SDKBase;
using VRC.Udon;
using Station = VRC.SDK3.Components.VRCStation;

// Licensed, hosted PC runner only. Stage in Assets/Editor after authoring both scenes.
// -executeMethod ExpansionRuntimeSmoke.Begin, manualExit: true, and no -quit.
// Uses the compiled Udon VM and actual ClientSim player; never calls UdonSharp proxies.
[InitializeOnLoad]
public static class ExpansionRuntimeSmoke
{
    const string Active = "Stillwater.ExpansionRuntimeSmoke.Active";
    const string Prefs = "com.vrchat.clientsim.settings";
    const double Limit = 130;
    static double phaseStart;
    static float phaseSimulationStart;
    static int phaseFrame;
    static int phase;
    static UdonBehaviour audio, visibility, billboard, approach, service, journey;
    static Transform root, hub, returns, passage, billboardRoom, origin, sink, arrival;
    static AudioSource atmosphere, rain, rumble;
    static Material sky;
    static GameObject menu;
    static Slider rainSlider, atmosphereSlider, videoSlider;
    static Collider[] physical;
    static bool[] colliderEnabled, colliderActive;
    static bool hold;
    static Vector3 heldPoint, pullPoint;
    static float farScale, initialSinkDistance;
    static Movement expected;
    static Transform[] doors, landings, coaches;
    static Vector3[] parkedPositions;
    static Animator trainAnimator;
    static double startedAt;

    struct Movement
    {
        public float gravity, walk, run, strafe, jump;
        public static Movement Read(VRCPlayerApi player)
        {
            return new Movement { gravity = player.GetGravityStrength(), walk = player.GetWalkSpeed(),
                run = player.GetRunSpeed(), strafe = player.GetStrafeSpeed(), jump = player.GetJumpImpulse() };
        }
        public void Apply(VRCPlayerApi player)
        {
            player.SetGravityStrength(gravity); player.SetWalkSpeed(walk); player.SetRunSpeed(run);
            player.SetStrafeSpeed(strafe); player.SetJumpImpulse(jump);
        }
        public void CheckRestored(VRCPlayerApi player, string context)
        {
            var actual = Read(player);
            Check(Close(gravity, actual.gravity) && Close(walk, actual.walk) && Close(run, actual.run)
                && Close(strafe, actual.strafe) && Close(jump, actual.jump), context + ": movement was not restored");
        }
    }

    static ExpansionRuntimeSmoke() { EditorApplication.update += Tick; }

    public static void Begin()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Hosted batch runner only.");
        SessionState.SetBool(Active + ".HadPrefs", EditorPrefs.HasKey(Prefs));
        SessionState.SetString(Active + ".PreviousPrefs", EditorPrefs.GetString(Prefs, ""));
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
        if (SessionState.GetBool(Active + ".HadPrefs", false))
            EditorPrefs.SetString(Prefs, SessionState.GetString(Active + ".PreviousPrefs", ""));
        else EditorPrefs.DeleteKey(Prefs);
        if (success) Debug.Log("EXPANSION_CLIENTSIM_PASS " + message);
        else Debug.LogError("EXPANSION_CLIENTSIM_FAIL phase=" + phase + " " + message);
        EditorApplication.Exit(success ? 0 : 1);
    }

    static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    static bool Close(float a, float b) { return Mathf.Abs(a - b) < .01f; }
    static T Value<T>(UdonBehaviour vm, string name)
    {
        object value = vm.GetProgramVariable(name);
        Check(value is T, vm.name + "." + name + " missing/wrong runtime type");
        return (T)value;
    }
    static UdonBehaviour Port(string name)
    {
        var found = UnityEngine.Object.FindObjectsOfType<UdonBehaviour>().Where(v => v.name == name).ToArray();
        Check(found.Length == 1, "Expected exactly one Udon VM named " + name + ", got " + found.Length);
        return found[0];
    }
    static Transform Room(string name)
    {
        var found = root.Find(name); Check(found, "Missing room " + name); return found;
    }
    static Vector3 FloorPoint(Transform room) { return room.position + Vector3.up * .25f; }
    static void Teleport(VRCPlayerApi player, Vector3 position, Quaternion rotation)
    {
        player.TeleportTo(position, rotation); player.SetVelocity(Vector3.zero);
    }
    static void Hold(VRCPlayerApi player, Vector3 position)
    {
        hold = true; heldPoint = position; Teleport(player, position, Quaternion.identity);
    }
    static void Next(double now, string completed)
    {
        Debug.Log("EXPANSION_CLIENTSIM_CHECK " + completed);
        phase++; phaseStart = now; phaseSimulationStart = Time.time; phaseFrame = Time.frameCount;
    }
    // Udon delays, cooldowns and movement use simulation time. Slow hosted frames
    // must not make an editor wall-clock wait finish before those updates run.
    static bool Ready(double now, double wait = 1)
    {
        return Time.time - phaseSimulationStart >= wait && Time.frameCount - phaseFrame >= 3;
    }

    static int SectorAt(UdonBehaviour vm, Vector3 position)
    {
        var min = Value<Vector3[]>(vm, "zoneMin"); var max = Value<Vector3[]>(vm, "zoneMax");
        Check(min.Length == max.Length && min.Length >= 14, vm.name + ": incomplete room bounds");
        position += Vector3.up * .1f;
        for (int i = 0; i < min.Length; i++)
            if (position.x >= min[i].x && position.x <= max[i].x && position.y >= min[i].y && position.y <= max[i].y
                && position.z >= min[i].z && position.z <= max[i].z) return i;
        throw new InvalidOperationException(vm.name + ": no sector at " + position);
    }

    static void CheckAudio(Vector3 point, bool wet)
    {
        int sector = SectorAt(audio, point);
        Check(Value<int>(audio, "current") == sector, "Room audio did not switch to sector " + sector);
        var clips = Value<AudioClip[]>(audio, "atmosphereClips");
        var rainy = Value<bool[]>(audio, "rainy");
        Check(clips.Length == rainy.Length && clips.Length > sector, "Audio arrays do not match bounds");
        Check(rainy[sector] == wet, "Unexpected rain flag for test room");
        Check(atmosphere.clip && atmosphere.clip == clips[sector] && atmosphere.isPlaying, "Room atmosphere is not playing its assigned clip");
        Check(rain.isPlaying == wet, "Rain did not stop/start at the room boundary");
        Check(Close(atmosphere.spatialBlend, 0) && Close(rain.spatialBlend, 0), "Room audio has distance attenuation");
        Check(Close(atmosphere.volume, Value<float>(audio, "atmosphereVolume") * .22f)
            && Close(rain.volume, Value<float>(audio, "rainVolume") * .16f), "Room audio lost the local slider values");
    }

    static void CheckRendered(Vector3 point, bool shown)
    {
        int sector = SectorAt(visibility, point);
        var renderers = Value<Renderer[]>(visibility, "renderers");
        var indices = Value<int[]>(visibility, "rendererSector");
        Check(renderers.Length == indices.Length, "Visibility renderer binding mismatch");
        int count = 0;
        for (int i = 0; i < indices.Length; i++) if (indices[i] == sector) {
            Check(renderers[i] && renderers[i].enabled == shown, "Sector " + sector + " renderer was " + (shown ? "hidden" : "left visible"));
            count++;
        }
        Check(count > 0, "No renderers bound to sector " + sector);
        if (shown && Vector3.Distance(Networking.LocalPlayer.GetPosition(), point) < 3)
            Check(Value<int>(visibility, "currentSector") == sector, "Current visibility sector did not change");
    }

    static void CheckPhysical()
    {
        for (int i = 0; i < physical.Length; i++)
            Check(physical[i] && physical[i].enabled == colliderEnabled[i]
                && physical[i].gameObject.activeInHierarchy == colliderActive[i], "Visibility changed a physical collider: " + physical[i]);
        foreach (Transform room in new[] { returns, passage }) {
            RaycastHit hit;
            Check(Physics.Raycast(room.position + Vector3.up * 3, Vector3.down, out hit, 6,
                (1 << 0) | (1 << 11), QueryTriggerInteraction.Ignore), "Hidden/current floor has no physical surface: " + room.name);
        }
    }

    static void Bind()
    {
        // The separate hosted visual suite captures the authored pixels. This VM
        // suite retains live cameras/tracking but skips their expensive scene and
        // sky draws; renderer-enabled and physical-collider checks remain intact.
        foreach (var camera in UnityEngine.Object.FindObjectsOfType<Camera>(true)) {
            camera.cullingMask = 0;
            camera.clearFlags = CameraClearFlags.SolidColor;
        }
        audio = Port("Local room soundscapes"); visibility = Port("District visibility");
        billboard = Port("Billboard local controls"); approach = Port("Aperture approach");
        root = audio.transform.parent;
        hub = Room("hub"); returns = Room("returns"); passage = Room("Passage returns to billboard"); billboardRoom = Room("billboard");
        service = Value<UdonBehaviour>(approach, "service");
        journey = Port("Adventure progression");
        doors = Value<Transform[]>(journey, "entrances"); landings = Value<Transform[]>(journey, "destinations");
        Check(doors.Length == 15 && landings.Length == 15, "Missing adventure volume/landing bindings");
        coaches = Value<Transform[]>(service, "coaches"); trainAnimator = Value<Animator>(service, "service");
        parkedPositions = coaches.Select(c => c.position).ToArray();
        Check(!Value<bool>(service, "hasStarted") && Close(trainAnimator.speed, 0), "Train must start parked until the shared button is pressed");
        Check(Value<UdonBehaviour>(billboard, "worldAudio") == audio && Value<UdonBehaviour>(approach, "audioDirector") == audio,
            "Portable controls/aperture do not share the live audio director");
        atmosphere = Value<AudioSource>(audio, "atmosphere"); rain = Value<AudioSource>(audio, "rain");
        rumble = Value<AudioSource>(approach, "rumble"); sky = Value<Material>(approach, "localSky");
        origin = Value<Transform>(approach, "approachStart"); sink = Value<Transform>(approach, "sink"); arrival = Value<Transform>(approach, "arrival");
        Check(sky.HasProperty("_ApproachScale") && rumble.clip, "Approach sky or rumble missing");
        menu = Value<GameObject>(billboard, "menu");
        rainSlider = Value<Slider>(billboard, "rainVolume"); atmosphereSlider = Value<Slider>(billboard, "atmosphereVolume");
        videoSlider = Value<Slider>(billboard, "videoVolume");
        physical = returns.GetComponentsInChildren<Collider>(true).Concat(passage.GetComponentsInChildren<Collider>(true)).ToArray();
        Check(physical.Length > 4 && physical.Any(c => !c.GetComponent<Renderer>()), "Missing non-rendering architecture colliders");
        colliderEnabled = physical.Select(c => c.enabled).ToArray(); colliderActive = physical.Select(c => c.gameObject.activeInHierarchy).ToArray();
        var seats = Room("rail").GetComponentsInChildren<UdonBehaviour>().Where(v => v.name == "Window seat A" || v.name == "Window seat B").ToArray();
        Check(seats.Length == 6, "Expected six compiled train seat behaviours");
        Transform deck = Value<Transform>(approach, "safeDeck");
        foreach (var seat in seats) {
            Check(Value<UdonBehaviour>(seat, "service") == service && Value<UdonBehaviour>(seat, "approach") == approach, "Seat has stale runtime service/approach binding");
            Check(Value<Station>(seat, "seat").gameObject == seat.gameObject, "Seat station binding is not its native station");
            Check(Value<Transform>(seat, "apertureExit").IsChildOf(deck), "Seat does not exit on the safe aperture deck");
        }
        pullPoint = origin.position + origin.forward * 145 + Vector3.right * 5;
        Vector3 deckPoint = deck.InverseTransformPoint(pullPoint);
        Vector3 half = Value<Vector3>(approach, "deckHalfSize");
        Check(Mathf.Abs(deckPoint.x) >= half.x || Mathf.Abs(deckPoint.z) >= half.z || deckPoint.y <= -.6f || deckPoint.y >= half.y,
            "Smoke pull point accidentally intersects the safe overlook");
    }

    static void CheckPull(VRCPlayerApi player)
    {
        Check(Value<bool>(approach, "pulling"), "Unseated player was not captured by the approach field");
        Check(Close(player.GetGravityStrength(), 0) && Close(player.GetWalkSpeed(), 0) && Close(player.GetRunSpeed(), 0)
            && Close(player.GetStrafeSpeed(), 0) && Close(player.GetJumpImpulse(), 0), "Pull did not temporarily suspend ordinary movement");
        Vector3 velocity = player.GetVelocity();
        Check(velocity.magnitude > .1f && Vector3.Dot(velocity.normalized, (sink.position - player.GetPosition()).normalized) > .8f,
            "Pull velocity is not moving toward the aperture");
    }
    static void CheckReturned(VRCPlayerApi player, string context)
    {
        expected.CheckRestored(player, context);
        Check(!Value<bool>(approach, "pulling") && Vector3.Distance(player.GetPosition(), arrival.position) < 3,
            context + ": player did not return to arrival");
        Check(Close(sky.GetFloat("_ApproachScale"), 1) && !rumble.isPlaying && Close(rumble.volume, 0), context + ": presentation was not reset");
    }

    static void CheckTrainPhase(float expectedPhase)
    {
        float actual = Mathf.Repeat(trainAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime, 1) * 320;
        float error = Mathf.Abs(actual - expectedPhase);
        Check(Mathf.Min(error, 320 - error) < 8, "Train did not follow the shared start epoch: expected " + expectedPhase + ", got " + actual);
        Check(Value<bool>(service, "hasStarted") && Close(trainAnimator.speed, 1), "Started train stopped instead of continuing to loop");
    }

    static void Tick()
    {
        if (!SessionState.GetBool(Active, false)) return;
        double now = EditorApplication.timeSinceStartup;
        try {
            double started = double.Parse(SessionState.GetString(Active + ".Started", "0"), CultureInfo.InvariantCulture);
            Check(now - started < Limit, "130 s timeout");
            if (!EditorApplication.isPlaying || !ClientSimMain.HasInstance() || !Utilities.IsValid(Networking.LocalPlayer)) return;
            if (phaseStart == 0) { phaseStart = now; phaseSimulationStart = Time.time; phaseFrame = Time.frameCount; }
            var player = Networking.LocalPlayer;
            if (hold) Teleport(player, heldPoint, Quaternion.identity);
            switch (phase) {
                case 0:
                    if (!Ready(now, 3)) return;
                    Bind();
                    Teleport(player, FloorPoint(hub), Quaternion.identity);
                    Next(now, "live VM bindings, 6 seats, common audio controls"); break;
                case 1:
                    if (!Ready(now)) return;
                    CheckAudio(FloorPoint(hub), true); CheckRendered(FloorPoint(hub), true); CheckRendered(FloorPoint(returns), false); CheckPhysical();
                    billboard.SendCustomEvent("OpenMenu");
                    Next(now, "wet room audible, occupied room rendered, distant room hidden, colliders retained"); break;
                case 2:
                    if (!Ready(now, .4)) return;
                    Check(menu.activeInHierarchy && Vector3.Distance(menu.transform.position, player.GetPosition()) < 3, "Portable menu did not open by the player");
                    // Actual authored UnityEvent bindings must deliver these changes to Udon.
                    rainSlider.value = .12f; atmosphereSlider.value = .61f; videoSlider.value = .23f;
                    Next(now, "portable menu opened"); break;
                case 3:
                    if (!Ready(now, .4)) return;
                    Check(Close(Value<float>(audio, "rainVolume"), .12f) && Close(Value<float>(audio, "atmosphereVolume"), .61f), "Rain/atmosphere slider UnityEvents did not reach the audio VM");
                    Check(Close(rain.volume, .12f * .16f) && Close(atmosphere.volume, .61f * .22f), "Independent ambience slider gains incorrect");
                    Check(Close(Value<AudioSource>(billboard, "speaker").volume, .23f * .35f), "Video volume slider UnityEvent failed");
                    billboard.SendCustomEvent("CloseMenu");
                    Teleport(player, FloorPoint(returns), Quaternion.identity);
                    Next(now, "all three real slider events changed local volumes"); break;
                case 4:
                    if (!Ready(now)) return;
                    Check(!menu.activeSelf, "Portable menu did not close"); CheckAudio(FloorPoint(returns), false);
                    CheckRendered(FloorPoint(returns), true); CheckRendered(FloorPoint(hub), false); CheckPhysical();
                    Teleport(player, FloorPoint(passage), Quaternion.LookRotation(Vector3.back));
                    Next(now, "dry room stopped rain and switched atmosphere without losing slider values"); break;
                case 5:
                    if (!Ready(now)) return;
                    CheckAudio(FloorPoint(passage), false); CheckRendered(FloorPoint(passage), true);
                    CheckRendered(FloorPoint(billboardRoom), false); CheckPhysical();
                    Teleport(player, FloorPoint(billboardRoom), Quaternion.identity);
                    Next(now, "sealed maintenance passage retains collision without revealing its former destination"); break;
                case 6:
                    if (!Ready(now)) return;
                    CheckAudio(FloorPoint(billboardRoom), true); CheckRendered(FloorPoint(billboardRoom), true);
                    billboard.SendCustomEvent("ToggleMenu"); Check(menu.activeSelf, "Menu toggle failed to open");
                    billboard.SendCustomEvent("ToggleMenu"); Check(!menu.activeSelf, "Menu toggle failed to close");
                    expected = new Movement { gravity = .73f, walk = 1.7f, run = 3.1f, strafe = 1.4f, jump = 2.7f };
                    expected.Apply(player); service.SetProgramVariable("localSeated", false);
                    Hold(player, origin.position + Vector3.right * 5);
                    Next(now, "billboard room restarts rain, portable menu toggles"); break;
                case 7:
                    if (!Ready(now, 1.2)) return;
                    farScale = sky.GetFloat("_ApproachScale");
                    Hold(player, origin.position + origin.forward * 85 + Vector3.right * 5);
                    Next(now, "recorded distant aperture size"); break;
                case 8:
                    if (!Ready(now, 2)) return;
                    Check(sky.GetFloat("_ApproachScale") > farScale + 1, "Black hole did not enlarge along the rail approach");
                    expected.CheckRestored(player, "before pull boundary");
                    service.SetProgramVariable("localSeated", true); approach.SendCustomEvent("CancelPullForSeat");
                    Hold(player, pullPoint);
                    Next(now, "local black-hole scale increases on approach"); break;
                case 9:
                    if (!Ready(now)) return;
                    Check(!Value<bool>(approach, "pulling"), "Seated player was pulled"); expected.CheckRestored(player, "seat immunity");
                    Check(rumble.isPlaying && Close(rumble.volume, .61f * .34f), "Aperture rumble does not follow the atmosphere slider");
                    hold = false; service.SetProgramVariable("localSeated", false);
                    initialSinkDistance = Vector3.Distance(player.GetPosition(), sink.position);
                    Next(now, "seat immunity and local rumble gain"); break;
                case 10:
                    if (!Ready(now, .7)) return;
                    CheckPull(player);
                    Check(Vector3.Distance(player.GetPosition(), sink.position) < initialSinkDistance - .05f, "Pull did not move the live player");
                    service.SetProgramVariable("localSeated", true); approach.SendCustomEvent("CancelPullForSeat");
                    expected.CheckRestored(player, "seat entry cancellation"); Hold(player, pullPoint);
                    Next(now, "unseated pull changes velocity and position; seat entry restores movement"); break;
                case 11:
                    if (!Ready(now, .5)) return;
                    Check(!Value<bool>(approach, "pulling"), "Seat cancellation immediately recaptured the player"); expected.CheckRestored(player, "after seat cancellation");
                    hold = false; service.SetProgramVariable("localSeated", false);
                    Next(now, "seat cancellation persists while seated"); break;
                case 12:
                    if (!Ready(now, .5)) return;
                    CheckPull(player); Teleport(player, arrival.position, arrival.rotation);
                    Next(now, "started second pull for field-exit check"); break;
                case 13:
                    if (!Ready(now, .5)) return;
                    CheckReturned(player, "leaving field"); Teleport(player, pullPoint, Quaternion.identity);
                    Next(now, "leaving field restores all movement and local presentation"); break;
                case 14:
                    if (!Ready(now, .5)) return;
                    CheckPull(player); approach.SendCustomEvent("ReturnToArrival");
                    Next(now, "explicit return invoked during active pull"); break;
                case 15:
                    if (!Ready(now, 1.2)) return;
                    CheckReturned(player, "explicit return"); Teleport(player, pullPoint, Quaternion.identity);
                    Next(now, "explicit return restores movement and arrival"); break;
                case 16:
                    if (!Ready(now, .5)) return;
                    CheckPull(player); billboard.SendCustomEvent("OpenMenu"); player.Respawn();
                    Next(now, "real ClientSim respawn requested while being pulled"); break;
                case 17:
                    if (!Ready(now, 1.3)) return;
                    CheckReturned(player, "respawn"); Check(!menu.activeSelf, "Respawn did not close the portable menu");
                    Check(!Value<bool>(service, "localSeated"), "Respawn left train local seated state set");
                    Teleport(player, pullPoint, Quaternion.identity);
                    Next(now, "respawn restores movement/presentation, clears seat flag and closes menu"); break;
                case 18:
                    if (!Ready(now, .5)) return;
                    CheckPull(player); Next(now, "automatic pull return started"); break;
                case 19:
                    if (Vector3.Distance(player.GetPosition(), arrival.position) >= 3) {
                        Check(Time.time - phaseSimulationStart < 10, "Pull did not return the player automatically within its bounded duration"); return;
                    }
                    CheckReturned(player, "automatic return"); CheckPhysical();
                    Check(!Value<bool>(service, "hasStarted") && Close(trainAnimator.speed, 0), "Train departed before the first button press");
                    for (int i = 0; i < coaches.Length; i++) Check(Vector3.Distance(coaches[i].position, parkedPositions[i]) < .01f, "Unstarted coach moved while the player explored");
                    Teleport(player, doors[0].TransformPoint(Vector3.up * .2f), doors[0].rotation);
                    Next(now, "train remains parked throughout exploration; entered first adventure door"); break;
                case 20:
                    if (!Ready(now, .3)) return;
                    Check(Value<int>(journey, "lastDoor") == 0 && Vector3.Distance(player.GetPosition(), landings[0].position) < 2,
                        "Walking into the first door did not warp to its intended room");
                    // Force the cooldown window independently of hosted frame duration,
                    // then invoke the real compiled volume scan while inside another door.
                    journey.SetProgramVariable("readyAt", Time.time + 10f);
                    Teleport(player, doors[1].TransformPoint(Vector3.up * .2f), doors[1].rotation);
                    Next(now, "first walk-through portal arrived at its actual landing"); break;
                case 21:
                    if (!Ready(now, .2)) return;
                    Check(Value<int>(journey, "lastDoor") == 0, "Portal cooldown did not suppress immediate re-entry");
                    journey.SetProgramVariable("readyAt", Time.time - .01f);
                    Next(now, "portal cooldown suppresses immediate second-door traversal"); break;
                case 22:
                    if (!Ready(now, .4)) return;
                    Check(Value<int>(journey, "lastDoor") == 1 && Vector3.Distance(player.GetPosition(), landings[1].position) < 2,
                        "The alternate starting route did not resume after cooldown");
                    service.SendCustomEvent("StartService");
                    Check(Value<bool>(service, "hasStarted"), "Owner start button did not start the shared train");
                    startedAt = Value<double>(service, "startedAt");
                    Check(Math.Abs(Networking.GetServerTimeInSeconds() - startedAt) < 2, "First start did not establish a fresh boarding epoch");
                    Next(now, "alternate branch portal arrived; shared train started with fresh boarding period"); break;
                case 23:
                    if (!Ready(now, .5)) return;
                    service.SendCustomEvent("StartService");
                    Check(Value<double>(service, "startedAt") == startedAt, "Pressing start again reset the train cycle");
                    for (int i = 0; i < coaches.Length; i++) Check(Vector3.Distance(coaches[i].position, parkedPositions[i]) < .01f, "Train skipped its boarding dwell");
                    service.SetProgramVariable("startedAt", Networking.GetServerTimeInSeconds() - 400);
                    service.SendCustomEvent("RunClock");
                    Next(now, "start is irreversible/idempotent and preserves boarding dwell; advanced shared clock"); break;
                case 24:
                    if (!Ready(now, .4)) return;
                    CheckTrainPhase(80);
                    service.SetProgramVariable("startedAt", Networking.GetServerTimeInSeconds() - 650);
                    service.SendCustomEvent("RunClock");
                    Next(now, "shared epoch resolves the moving train after one completed round trip"); break;
                case 25:
                    if (!Ready(now, .4)) return;
                    CheckTrainPhase(10);
                    Finish(true, "Compiled Udon/ClientSim verified room audio, independent sliders, portable menu, isolated districts with persistent colliders, six seats, aperture growth/pull/immunity/return, two walk-through routes and cooldown, initial parked train, one-time start, boarding dwell and repeating epoch after multiple round trips. No external video URL or multi-client session was exercised."); break;
            }
        }
        catch (Exception e) { Finish(false, e.ToString()); }
    }
}
