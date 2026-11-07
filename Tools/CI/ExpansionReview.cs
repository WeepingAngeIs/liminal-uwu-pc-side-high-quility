using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Staged into Assets/Editor on the licensed hosted graphics runner only.
public static class ExpansionReview
{
    const string Root = "Assets/Stillwater";
    const string Output = "Builds/ExpansionReview";
    const float ReviewTime = 12.5f;
    static readonly Vector3 Pole = new Vector3(0, .743144825f, .669130606f);
    static Report report;
    static readonly List<Material> TemporaryMaterials = new List<Material>();

    [Serializable] sealed class Report
    {
        public string revision, unity, device, graphicsApi, utc;
        public bool authoringSucceeded, reviewPassed;
        public string method = "Both scenes authored from this revision on hosted Mesa/OpenGL, including all PC reflection captures. These editor images exercise shaders and serialized wiring, not live VRChat networking, XR, URL decoding, or headset performance. Quest materials are reviewed in the desktop editor; this is not an Android build.";
        public List<SceneRecord> scenes = new List<SceneRecord>();
        public List<ImageRecord> images = new List<ImageRecord>();
        public List<SkyRecord> sky = new List<SkyRecord>();
        public List<ProgramRecord> programs = new List<ProgramRecord>();
        public List<string> shaderMessages = new List<string>();
        public List<string> errors = new List<string>();
        public List<string> failures = new List<string>();
    }
    [Serializable] sealed class ProgramRecord
    {
        public string asset, compiledAsset, version;
        public bool assemblyError, bytecodeAvailable;
    }
    [Serializable] sealed class SceneRecord
    {
        public string edition, scene, sha256;
        public int rendererCount, enabledRendererCount, colliderCount, reflectionCount, trainSeats, nativeVideoPlayers;
        public long trianglesAllLods;
        public int audioZones, visibilityZones, visibilityRenderers, visibilityPortals, adventureDoors, personalPlatformLights;
        public int loungeSeats, uwuBalls, formationPieces, floatingStreetObjects, authoredEnabledLights, practicalLights;
        public int fixtureVolumes;
        public int nativeStations;
        public bool wallGravityReady;
        public int illuminatedBallCenters, ballReadabilityViews;
        public float minimumBallFixtureEnergy, minimumBallMedianBrightness, minimumBallReadableFraction;
        public List<FixtureRecord> fixtures = new List<FixtureRecord>();
        public bool carriageAudioBound;
        public float carriageAudioSeconds, carriageAudioSpatialBlend;
        public float atmosphereSpatialBlend, rainSpatialBlend, rainVolume, atmosphereVolume;
        public float authoredRainSourceVolume, authoredAtmosphereSourceVolume;
        public bool hasVideoUrlInput, hasLocalMenu, hasSeparateVolumeControls, hasTrain, hasApertureApproach;
        public Vector3 kioskPosition;
        public List<AudioZone> audio = new List<AudioZone>();
        public List<ReflectionRecord> reflections = new List<ReflectionRecord>();
    }
    [Serializable] sealed class AudioZone
    {
        public int index;
        public string clip;
        public bool rainy;
        public Vector3 min, max;
    }
    [Serializable] sealed class ReflectionRecord
    {
        public string asset, sha256;
        public int resolution;
    }
    [Serializable] sealed class ImageRecord
    {
        public string file, sha256;
        public int width, height, magentaPixels;
        public float meanBrightness, fieldOfView, approachScale, filmSecond = -1;
        public float darkFraction, litFraction, coloredLitFraction, lowerFrameBrightness, lowerFrameLitFraction;
        public Vector3 position, forward;
    }
    [Serializable] sealed class FixtureRecord
    {
        public string room, colorAsset, directionAsset;
        public int litMaterials, width, height, depth;
        public float maximumChannel, meanChannel;
    }
    [Serializable] sealed class SkyRecord
    {
        public string edition, view;
        public float scale;
        public int coreSamples, maximumCoreChannel, minimumRimSectorPeak;
    }
    sealed class RendererMaterials
    {
        public Renderer renderer;
        public Material[] materials;
    }

    public static void AuthorAndCapture()
    {
        Directory.CreateDirectory(Output);
        report = new Report {
            revision = File.Exists("Builds/author-revision.txt") ? File.ReadAllText("Builds/author-revision.txt").Trim() : "unspecified",
            unity = Application.unityVersion, device = SystemInfo.graphicsDeviceName,
            graphicsApi = SystemInfo.graphicsDeviceType.ToString(), utc = DateTime.UtcNow.ToString("O")
        };
        Application.logMessageReceived += CaptureError;
        Camera.onPreRender += SetClock;
        try {
            Require(Application.isBatchMode, "Run the authoring review on the hosted batch runner.");
            Require(SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLCore, "Authoring requires the hosted Mesa OpenGL graphics context.");
            Require(report.revision != "unspecified", "The workflow did not record its checked-out revision.");
            WorldAuthor.Author(); // Includes all 21 PC custom reflection cubemap captures.
            CheckPrograms();
            AssetDatabase.SaveAssets();
            report.authoringSucceeded = report.failures.Count == 0 && report.errors.Count == 0;
            ReviewScene(false);
            ReviewScene(true);
            CheckShaders(); // After rendering, so newly encountered shader variants are included.
            Require(report.scenes.Count == 2, "Both editions must be reviewed.");
        }
        catch (Exception error) { report.failures.Add(error.ToString()); }
        finally {
            Camera.onPreRender -= SetClock;
            Application.logMessageReceived -= CaptureError;
            report.reviewPassed = report.authoringSucceeded && report.failures.Count == 0 && report.errors.Count == 0;
            File.WriteAllText(Output + "/report.json", JsonUtility.ToJson(report, true));
            File.WriteAllLines(Output + "/authoring-errors.log", report.errors.Concat(report.shaderMessages).Concat(report.failures));
        }
        if (!report.reviewPassed) throw new InvalidOperationException("Expansion authoring/review failed; see Builds/ExpansionReview/report.json.");
        Debug.Log("LIMINAL_EXPANSION_AUTHORED_AND_REVIEWED");
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    static void CaptureError(string message, string stack, LogType kind)
    {
        if (kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert)
            report.errors.Add(kind + ": " + message + "\n" + stack);
    }
    static void SetClock(Camera unused)
    {
        Shader.SetGlobalVector("_Time", new Vector4(ReviewTime / 20f, ReviewTime, ReviewTime * 2, ReviewTime * 3));
    }
    static void CheckPrograms()
    {
        UdonSharp.Compiler.UdonSharpCompilerV1.CompileSync(new UdonSharp.Compiler.UdonSharpCompileOptions());
        foreach (string guid in AssetDatabase.FindAssets("t:UdonSharpProgramAsset", new[] { Root + "/Runtime", Root + "/Personal/PlanetMovement" })) {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var program = AssetDatabase.LoadAssetAtPath<UdonSharp.UdonSharpProgramAsset>(path);
            var serialized = new SerializedObject(program);
            var binary = serialized.FindProperty("serializedUdonProgramAsset").objectReferenceValue;
            var assemblyError = serialized.FindProperty("assemblyError");
            var record = new ProgramRecord { asset = path, compiledAsset = AssetDatabase.GetAssetPath(binary),
                version = program.CompiledVersion.ToString(), assemblyError = assemblyError != null && !string.IsNullOrEmpty(assemblyError.stringValue) };
            if (binary) {
                var retrieve = binary.GetType().GetMethod("RetrieveProgram", Type.EmptyTypes);
                record.bytecodeAvailable = retrieve != null && retrieve.Invoke(binary, null) != null;
            }
            report.programs.Add(record);
            Require(program.CompiledVersion == UdonSharp.UdonSharpProgramVersion.CurrentVersion && !record.assemblyError && record.bytecodeAvailable,
                "Missing or failed compiled Udon program: " + path);
            if (path.StartsWith(Root + "/Personal/PlanetMovement/", StringComparison.Ordinal))
                Require(record.compiledAsset.StartsWith(Root + "/Personal/PlanetMovement/Compiled/", StringComparison.Ordinal),
                    "Owner-licensed gravity bytecode must remain in the private asset directory: " + path);
        }
        Require(report.programs.Count >= 8, "Expected all world runtime programs to have compiled assets.");
        foreach (string name in new[] { "BobysPlanetMovement", "PlanetMove_GravityField", "PlanetMove_GravityFieldGroup", "PlanetMove_StationListener", "PlanetMove_AutoExitManager" })
            Require(report.programs.Any(p => p.asset == Root + "/Personal/PlanetMovement/" + name + ".asset" && p.bytecodeAvailable),
                "Missing original gravity program: " + name);
    }

    static void ReviewScene(bool quest)
    {
        string edition = quest ? "Quest" : "PC";
        string path = quest ? WorldAuthor.QuestScene : WorldAuthor.PCScene;
        EditorSceneManager.OpenScene(path);
        var scene = InspectScene(edition, path);
        report.scenes.Add(scene);
        Directory.CreateDirectory(Output + "/" + edition);
        var originals = UnityEngine.Object.FindObjectsOfType<Renderer>(true).Select(r => new RendererMaterials { renderer = r, materials = r.sharedMaterials }).ToArray();
        var originalSky = RenderSettings.skybox;
        var copies = new Dictionary<Material, Material>();
        Camera camera = null;
        try {
            foreach (var entry in originals) entry.renderer.sharedMaterials = entry.materials.Select(m => Clone(m, copies)).ToArray();
            RenderSettings.skybox = Clone(originalSky, copies);
            foreach (var rain in UnityEngine.Object.FindObjectsOfType<ParticleSystem>()) {
                rain.useAutoRandomSeed = false; rain.randomSeed = 193;
                rain.Simulate(1.1f, true, true, true);
            }
            camera = new GameObject("Hosted expansion camera").AddComponent<Camera>();
            camera.enabled = false; camera.clearFlags = CameraClearFlags.Skybox;
            camera.nearClipPlane = .08f; camera.farClipPlane = 600;
            camera.allowHDR = false; camera.allowMSAA = false;
            camera.renderingPath = RenderingPath.Forward; camera.useOcclusionCulling = false;
            View(camera, edition, "rain-exchange", new Vector3(0, 1.72f, -23), new Vector3(2, 6, 20));
            View(camera, edition, "side-street-receiver", new Vector3(18, 1.74f, -17), new Vector3(24, 2.09f, -20));
            View(camera, edition, "ad-alley", new Vector3(229, 1.75f, -23), new Vector3(263, 10, 8));
            View(camera, edition, "last-concourse", new Vector3(384, 1.75f, -27), new Vector3(410, 8, 22));
            View(camera, edition, "orbital-causeway", new Vector3(397, 1.75f, 142), new Vector3(440, 45, 222), 82);
            View(camera, edition, "quiet-stacks", new Vector3(224, 1.75f, 176), new Vector3(258, 13, 223));
            View(camera, edition, "service-returns", new Vector3(226, 1.75f, 80), new Vector3(249, 5, 122));
            View(camera, edition, "waterfall-chasm", new Vector3(640, 2, 155), new Vector3(640, 55, 230), 88);
            View(camera, edition, "uwu-adventure-doors", new Vector3(0, 1.75f, 15), new Vector3(0, 3, 27), 82);
            View(camera, edition, "personal-platform-lights", new Vector3(14, 2.1f, 51), new Vector3(8.6f, 3.5f, 66), 80);
            View(camera, edition, "suspended-street-life", new Vector3(-9, 1.75f, -20), new Vector3(10, 19, 10), 82);
            View(camera, edition, "uwu-ballroom", new Vector3(640, 1.9f, 376), new Vector3(640, 5.3f, 410), 82);
            BallReadabilityViews(camera, edition, scene);
            View(camera, edition, "quiet-lounge", new Vector3(800, 1.9f, 377), new Vector3(800, 10, 413), 82);
            View(camera, edition, "lounge-light-pools", new Vector3(800, 2.6f, 386), new Vector3(818, 1.1f, 408), 76);
            var loungeImage = report.images.Last();
            Require(loungeImage.lowerFrameBrightness > .004f && loungeImage.lowerFrameLitFraction > .01f,
                edition + " lounge floor is unreadable in the practical-light review.");
            FormationViews(camera, edition);
            var gravityRoom = GameObject.Find("liminal uwu").transform.Find("planet");
            Require(gravityRoom, edition + " missing the new gravity district.");
            View(camera, edition, "gravity-orbit-room", gravityRoom.position + new Vector3(17, 8, -34),
                gravityRoom.position + new Vector3(0, 27, 0), 88);
            Sky(camera, edition, "sky-far", 1, 77);
            camera.fieldOfView = 110;
            camera.transform.rotation = Quaternion.LookRotation(-Pole);
            UnityEngine.Object.DestroyImmediate(Capture(camera, edition + "/distant-nebula", 1280, 800));
            Sky(camera, edition, "sky-approach", 3.1f, 100);
            RenderSettings.skybox.SetFloat("_ApproachScale", 1);
            Train(camera, edition, quest);
            Film(camera, edition, quest, copies);
            Menu(camera, edition);
        }
        finally {
            RenderSettings.skybox = originalSky;
            foreach (var entry in originals) if (entry.renderer) entry.renderer.sharedMaterials = entry.materials;
            foreach (var material in TemporaryMaterials) if (material) UnityEngine.Object.DestroyImmediate(material);
            TemporaryMaterials.Clear();
            if (camera) UnityEngine.Object.DestroyImmediate(camera.gameObject);
        }
        // Review changes are deliberately not saved into authored scene/material assets.
        Require(scene.sha256 == Digest(File.ReadAllBytes(path)), "Image capture changed a serialized scene: " + path);
    }

    static SceneRecord InspectScene(string edition, string path)
    {
        var root = GameObject.Find("liminal uwu");
        Require(root, "World root missing in " + edition);
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        var result = new SceneRecord { edition = edition, scene = path, sha256 = Digest(File.ReadAllBytes(path)),
            rendererCount = renderers.Length, enabledRendererCount = renderers.Count(r => r.enabled && r.gameObject.activeInHierarchy),
            colliderCount = root.GetComponentsInChildren<Collider>(true).Length,
            trainSeats = root.GetComponentsInChildren<ExpressSeat>(true).Length,
            nativeVideoPlayers = root.GetComponentsInChildren<Component>(true).Count(c => c && c.GetType().Name == "VRCUnityVideoPlayer"),
            hasTrain = root.GetComponentInChildren<ApertureExpress>(true),
            hasApertureApproach = root.GetComponentInChildren<ApertureApproach>(true) };
        foreach (var mesh in root.GetComponentsInChildren<MeshFilter>(true).Select(f => f.sharedMesh).Concat(root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(r => r.sharedMesh)))
            if (mesh) for (int i = 0; i < mesh.subMeshCount; i++) if (mesh.GetTopology(i) == MeshTopology.Triangles) result.trianglesAllLods += mesh.GetIndexCount(i) / 3;
        foreach (string area in new[] { "billboard", "concourse", "causeway", "stacks", "returns", "waterfall", "ballroom", "lounge", "formation", "planet" })
            Require(root.transform.Find(area), edition + " missing new district " + area);
        var journey = root.GetComponentInChildren<AdventureJourney>(true);
        Require(journey && journey.entrances.Length == 19 && journey.destinations.Length == 19 && journey.halfExtents.Length == 19,
            edition + " requires all nineteen walk-through adventure doors.");
        Require(journey.entrances.All(t => t) && journey.destinations.All(t => t), edition + " has an unbound adventure door.");
        result.adventureDoors = journey.entrances.Length;
        Physics.SyncTransforms();
        for (int i = 0; i < journey.entrances.Length; i++) {
            CheckStandingSpace(journey.entrances[i].TransformPoint(new Vector3(0, .2f, -1)), edition + " door " + i);
            CheckStandingSpace(journey.destinations[i].position, edition + " landing " + i);
        }
        var track = root.transform.Find("rail/Walkable track bed");
        Require(track && track.GetComponent<MeshCollider>(), edition + " requires physical walkable tracks.");
        foreach (float s in new[] { 12f, 90f, 180f, 280f, 340f, 400f }) {
            float helix = 6 * Mathf.PI * 17, u = Mathf.Clamp(s, 0, helix);
            float y = .07f + .31f * (u - 12 * (1 - Mathf.Exp(-u / 12)));
            if (s > helix) { float v = s - helix, blend = Mathf.Min(v, 18); y += .31f * blend + (1.1106125f - .31f) * blend * blend / 36 + 1.1106125f * Mathf.Max(0, v - 18); }
            Vector3 point = s <= helix ? new Vector3(17 * Mathf.Cos(s / 17), y, 17 * Mathf.Sin(s / 17)) : new Vector3(17, y, s - helix);
            RaycastHit hit;
            Require(Physics.Raycast(root.transform.Find("rail").TransformPoint(point) + Vector3.up, Vector3.down,
                out hit, 2, (1 << 0) | (1 << 11), QueryTriggerInteraction.Ignore) && hit.collider.transform == track,
                edition + " track has no walkable surface at distance " + s);
        }
        var kiosk = root.transform.Find("hub/Embankment signal kiosk");
        Require(kiosk && Mathf.Abs(kiosk.localPosition.x) >= 17, edition + " receiver is not off the central road.");
        result.kioskPosition = kiosk.position;
        var audio = root.GetComponentInChildren<WorldAudioDirector>(true);
        Require(audio && audio.atmosphere && audio.rain, edition + " soundscape sources are missing.");
        result.audioZones = audio.zoneMin.Length;
        result.atmosphereSpatialBlend = audio.atmosphere.spatialBlend; result.rainSpatialBlend = audio.rain.spatialBlend;
        result.rainVolume = audio.rainVolume; result.atmosphereVolume = audio.atmosphereVolume;
        result.authoredRainSourceVolume = audio.rain.volume; result.authoredAtmosphereSourceVolume = audio.atmosphere.volume;
        Require(audio.zoneMax.Length == result.audioZones && audio.atmosphereClips.Length == result.audioZones && audio.rainy.Length == result.audioZones,
            edition + " soundscape zone arrays disagree.");
        Require(Mathf.Approximately(result.atmosphereSpatialBlend, 0) && Mathf.Approximately(result.rainSpatialBlend, 0), edition + " room ambience must be 2D.");
        for (int i = 0; i < result.audioZones; i++) {
            Require(audio.atmosphereClips[i], edition + " missing room audio clip at " + i);
            result.audio.Add(new AudioZone { index = i, clip = AssetDatabase.GetAssetPath(audio.atmosphereClips[i]), rainy = audio.rainy[i], min = audio.zoneMin[i], max = audio.zoneMax[i] });
        }
        var visibility = root.GetComponentInChildren<DistrictVisibility>(true);
        Require(visibility, edition + " district visibility controller is missing.");
        result.visibilityZones = visibility.zoneMin.Length; result.visibilityRenderers = visibility.renderers.Length; result.visibilityPortals = visibility.portalA.Length;
        Require(result.visibilityZones >= 14 && result.visibilityRenderers > 0 && result.visibilityPortals > 0, edition + " district visibility data is incomplete.");
        Require(visibility.zoneMax.Length == result.visibilityZones && visibility.rendererSector.Length == result.visibilityRenderers && visibility.portalB.Length == result.visibilityPortals && visibility.portalCenter.Length == result.visibilityPortals && visibility.portalRadius.Length == result.visibilityPortals,
            edition + " visibility arrays disagree.");
        Require(visibility.renderers.All(r => r) && visibility.rendererSector.All(i => i >= 0 && i < result.visibilityZones) && visibility.portalA.Concat(visibility.portalB).All(i => i >= 0 && i < result.visibilityZones), edition + " invalid visibility references.");
        var player = root.GetComponentInChildren<BillboardPlayer>(true);
        Require(player && player.video && player.screenMaterial && player.speaker, edition + " video player wiring is incomplete.");
        result.hasVideoUrlInput = player.urlInput;
        result.hasLocalMenu = player.menu && player.wristButton;
        result.hasSeparateVolumeControls = player.videoVolume && player.rainVolume && player.atmosphereVolume && player.worldAudio == audio;
        Require(result.hasVideoUrlInput && result.hasLocalMenu && result.hasSeparateVolumeControls && result.nativeVideoPlayers == 1, edition + " video controls are incomplete.");
        Require(result.trainSeats == 6 && result.hasTrain && result.hasApertureApproach, edition + " requires six train seats and the approach controller.");
        var express = root.GetComponentInChildren<ApertureExpress>(true);
        var lighting = root.transform.Find("rail/Platform personal lighting");
        Require(lighting, edition + " personal platform lighting is missing.");
        for (int i = 1; i <= 2; i++) {
            var unit = lighting.Find("Kitty platform light " + i);
            Require(unit, edition + " missing personal platform light " + i);
            var parts = unit.GetComponentsInChildren<MeshRenderer>(true);
            Require(parts.Length > 0 && parts.All(r => r.sharedMaterials.Length > 0 && r.sharedMaterials.All(m => m && m.shader)),
                edition + " personal light has missing geometry or materials.");
            Require(unit.GetComponentsInChildren<Collider>(true).Length == 0, edition + " decorative LED must not obstruct the boarding platform.");
            result.personalPlatformLights++;
        }
        var rail = root.transform.Find("rail");
        foreach (Vector3 point in new[] { new Vector3(13, .54f, -24), new Vector3(13, .54f, -6), new Vector3(8.6f, .25f, -15) })
            CheckStandingSpace(rail.TransformPoint(point), edition + " personal platform clearance " + point);
        result.carriageAudioBound = express.carriageAmbience && express.carriageAmbience.clip && express.audioDirector == audio;
        Require(result.carriageAudioBound, edition + " private carriage sound is not bound to the train and atmosphere director.");
        var carriage = express.carriageAmbience;
        result.carriageAudioSeconds = carriage.clip.length; result.carriageAudioSpatialBlend = carriage.spatialBlend;
        Require(carriage.loop && !carriage.playOnAwake && Mathf.Approximately(carriage.volume, 0) && Mathf.Approximately(carriage.spatialBlend, 0),
            edition + " carriage ambience must start silent and remain local/room-wide.");
        foreach (var probe in root.GetComponentsInChildren<ReflectionProbe>(true)) {
            if (!probe.customBakedTexture) continue;
            string asset = AssetDatabase.GetAssetPath(probe.customBakedTexture);
            result.reflections.Add(new ReflectionRecord { asset = asset, resolution = probe.customBakedTexture.width, sha256 = File.Exists(asset) ? Digest(File.ReadAllBytes(asset)) : "missing" });
        }
        result.reflectionCount = result.reflections.Count;
        if (edition == "PC") Require(result.reflectionCount == 21 && result.reflections.All(p => p.sha256 != "missing"), "Expected 21 authored PC reflection cubemaps.");
        else Require(result.reflectionCount == 0, "Quest must not retain PC room reflection cubemaps.");
        InspectNewRooms(root.transform, result);
        return result;
    }

    static void InspectNewRooms(Transform root, SceneRecord result)
    {
        string edition = result.edition; bool quest = edition == "Quest";
        Require(result.audioZones == 42 && result.visibilityZones == 42, edition + " requires all 42 sound and visibility zones.");
        var seats = root.Find("lounge").GetComponentsInChildren<LoungeSeat>(true);
        result.loungeSeats = seats.Length;
        Require(seats.Length == 4, edition + " lounge requires four functional seats.");
        foreach (var seat in seats) {
            Require(seat.seat && seat.seat.gameObject == seat.gameObject && seat.seat.stationEnterPlayerLocation && seat.seat.stationExitPlayerLocation,
                edition + " unbound lounge seat: " + seat.name);
            Require(seat.seat.seated && !seat.seat.disableStationExit && seat.GetComponent<Collider>(), edition + " invalid lounge station settings.");
            Require(!seat.GetComponent<ExpressSeat>(), edition + " lounge chair must not change train seating state.");
            CheckStandingSpace(seat.seat.stationExitPlayerLocation.position, edition + " lounge seat exit " + seat.name);
        }
        var balls = root.GetComponentInChildren<UwuBallPit>(true);
        Require(balls && balls.room == root.Find("ballroom") && balls.balls != null && balls.radii != null && balls.starts != null,
            edition + " ball room is not bound to its local simulation.");
        result.uwuBalls = balls.balls.Length;
        Require(result.uwuBalls == 24 && balls.radii.Length == 24 && balls.starts.Length == 24 && balls.balls.All(t => t), edition + " requires 24 bounded uwu balls.");
        foreach (var ball in balls.balls) {
            Require(ball.GetComponentsInChildren<Component>(true).All(c => c && c.GetType().Name != "VRCPickup" && c.GetType().Name != "VRC_Pickup"),
                edition + " balls must not be grabbable.");
            Require(ball.GetComponentsInChildren<Collider>(true).Length == 0 && ball.GetComponentsInChildren<Rigidbody>(true).Length == 0,
                edition + " balls must use the bounded analytic player-contact simulation.");
        }
        result.minimumBallFixtureEnergy = float.PositiveInfinity;
        foreach (var ball in balls.balls) {
            var material = ball.GetComponent<Renderer>().sharedMaterial;
            Require(material && material.IsKeywordEnabled("SW_FIXTURE_VOLUME"), edition + " ball lacks fixture lighting.");
            var volume = material.GetTexture("_FixtureColor") as Texture3D;
            Require(volume, edition + " ball has no readable fixture volume.");
            Vector3 origin = material.GetVector("_FixtureMin"), size = material.GetVector("_FixtureSize");
            Vector3 p = ball.position - origin;
            Color energy = volume.GetPixelBilinear(Mathf.Clamp01(p.x / size.x), Mathf.Clamp01(p.y / size.y), Mathf.Clamp01(p.z / size.z));
            float maximum = Mathf.Max(energy.r, Mathf.Max(energy.g, energy.b)) * 4;
            result.minimumBallFixtureEnergy = Mathf.Min(result.minimumBallFixtureEnergy, maximum);
            if (maximum > .005f) result.illuminatedBallCenters++;
        }
        Require(result.illuminatedBallCenters == 24,
            edition + " contains uwu balls outside the reach of the actual fluorescent fixtures; minimum energy=" + result.minimumBallFixtureEnergy);
        var formation = root.GetComponentInChildren<RoomFormation>(true);
        Require(formation && formation.space && formation.space.IsChildOf(root.Find("formation")), edition + " room reconstruction bindings missing.");
        result.formationPieces = formation.pieces.Length;
        int expected = quest ? 16 : 32;
        Require(result.formationPieces == expected && formation.assembledPositions.Length == expected && formation.scatteredPositions.Length == expected &&
            formation.assembledRotations.Length == expected && formation.scatteredRotations.Length == expected && formation.influencePoints.Length == expected,
            edition + " room reconstruction arrays differ.");
        Require(formation.nearDistance < formation.farDistance && formation.assembleSeconds >= 4 && formation.scatterSeconds >= 4,
            edition + " reconstruction needs hysteresis and slow movement.");
        for (int i = 0; i < expected; i++) {
            var piece = formation.pieces[i];
            Require(piece && piece.IsChildOf(formation.space) && piece.GetComponentsInChildren<Collider>(true).Length == 0,
                edition + " moving architecture must not collide with visitors.");
            Require((formation.assembledPositions[i] - formation.scatteredPositions[i]).sqrMagnitude > 1,
                edition + " formation piece has no meaningful scattered pose.");
            foreach (var renderer in piece.GetComponentsInChildren<MeshRenderer>(true))
                Require((GameObjectUtility.GetStaticEditorFlags(renderer.gameObject) & StaticEditorFlags.OccluderStatic) == 0,
                    edition + " animated architecture was marked as a baked occluder.");
        }
        CheckStandingSpace(root.Find("formation").TransformPoint(new Vector3(0, .2f, 0)), edition + " permanent formation foundation");
        var floating = root.GetComponentInChildren<FloatingStreetObjects>(true);
        Require(floating && floating.objects != null && floating.surfaces != null && floating.restPositions != null && floating.restRotations != null,
            edition + " floating street objects are not wired.");
        result.floatingStreetObjects = floating.objects.Length;
        Require(result.floatingStreetObjects == (quest ? 28 : 44) && floating.surfaces.Length == result.floatingStreetObjects &&
            floating.restPositions.Length == result.floatingStreetObjects && floating.restRotations.Length == result.floatingStreetObjects,
            edition + " floating street-object arrays differ.");
        Require(floating.objects.All(t => t && t.GetComponentsInChildren<Collider>(true).Length == 0) && floating.surfaces.All(r => r),
            edition + " decorative floating props must not obstruct routes.");
        var gravity = root.Find("planet").GetComponentInChildren<PlanetGalleryControl>(true);
        Require(gravity && gravity.controller && gravity.gravityField && gravity.room == root.Find("planet") && gravity.southReturn && gravity.northReturn,
            edition + " wall-gravity boundary is not fully wired.");
        var controller = gravity.controller; var field = gravity.gravityField;
        result.wallGravityReady = controller.isReady && controller.station && controller.rb && field.autoEnterController == controller;
        Require(result.wallGravityReady && !controller.station.seated && field.onlyInTrig && field.autoEnter &&
            field.fieldType == BobyStar.PlanetMovement.GravityFieldType.Axis && field.strength < 0 && (field.axisGravity - Vector3.forward).sqrMagnitude < .0001f,
            edition + " original radial wall-walking mechanic is not ready.");
        Require(field.GetComponent<BoxCollider>() && field.GetComponent<BoxCollider>().isTrigger, edition + " wall gravity needs its boundary trigger.");
        CheckStandingSpace(gravity.southReturn.position, edition + " wall-gravity south return");
        CheckStandingSpace(gravity.northReturn.position, edition + " wall-gravity north return");
        result.nativeStations = root.GetComponentsInChildren<VRC.SDK3.Components.VRCStation>(true).Length;
        Require(result.nativeStations == 11, edition + " requires six train seats, four lounge seats and one local gravity station.");
        var practical = root.GetComponentInChildren<LocalPracticalLights>(true);
        Require(practical && practical.lamps != null, edition + " practical-light selector is missing.");
        result.practicalLights = practical.lamps.Length;
        result.authoredEnabledLights = root.GetComponentsInChildren<Light>(true).Count(l => l.enabled && l.gameObject.activeInHierarchy);
        Require(result.authoredEnabledLights == 0 && practical.lamps.All(l => l && !l.enabled && l.shadows == LightShadows.None),
            edition + " realtime fixture lights must start disabled and have no shadow maps.");
        Require(quest ? result.practicalLights == 0 : result.practicalLights > 0, edition + " practical-light count does not match the platform.");
        foreach (string name in new[] { "hub", "ballroom", "lounge", "formation" }) {
            var materials = root.Find(name).GetComponentsInChildren<MeshRenderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m && m.IsKeywordEnabled("SW_FIXTURE_VOLUME")).Distinct().ToArray();
            Require(materials.Length > 0, edition + " missing colored fixture-light bindings in " + name);
            var color = materials[0].GetTexture("_FixtureColor") as Texture3D;
            var direction = materials[0].GetTexture("_FixtureDirection") as Texture3D;
            Require(color && direction && color.width == direction.width && color.height == direction.height && color.depth == direction.depth,
                edition + " mismatched fixture volumes in " + name);
            Require(materials.All(m => m.GetTexture("_FixtureColor") == color && m.GetTexture("_FixtureDirection") == direction),
                edition + " room materials reference inconsistent fixture lighting in " + name);
            var pixels = color.GetPixels();
            float maximum = pixels.Max(c => Mathf.Max(c.r, Mathf.Max(c.g, c.b)));
            float mean = pixels.Average(c => (c.r + c.g + c.b) / 3f);
            Require(maximum > .02f && mean > .00001f, edition + " fixture volume is empty in " + name);
            result.fixtures.Add(new FixtureRecord { room = name, colorAsset = AssetDatabase.GetAssetPath(color), directionAsset = AssetDatabase.GetAssetPath(direction),
                litMaterials = materials.Length, width = color.width, height = color.height, depth = color.depth, maximumChannel = maximum, meanChannel = mean });
        }
        result.fixtureVolumes = result.fixtures.Count;
    }

    static void BallReadabilityViews(Camera camera, string edition, SceneRecord scene)
    {
        var pit = UnityEngine.Object.FindObjectOfType<UwuBallPit>();
        var renderers = UnityEngine.Object.FindObjectsOfType<Renderer>(true);
        var enabled = renderers.Select(r => r.enabled).ToArray();
        var oldBackground = camera.backgroundColor;
        scene.minimumBallMedianBrightness = float.PositiveInfinity;
        scene.minimumBallReadableFraction = float.PositiveInfinity;
        try {
            // Render only each selected ball, with its real material/probe and
            // baked lighting intact. Neither the bright sky, fixture faces nor
            // a glowing floor can make a black ball pass this check.
            foreach (var renderer in renderers) renderer.enabled = false;
            Vector3 entranceView = pit.room.TransformPoint(new Vector3(0, 1.9f, -24));
            foreach (int index in new[] { 7, 10, 13, 16 }) {
                var ball = pit.balls[index]; var renderer = ball.GetComponent<Renderer>();
                renderer.enabled = true;
                float radius = pit.radii[index];
                camera.orthographic = true; camera.orthographicSize = radius * 1.3f; camera.cullingMask = -1;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                Vector3 towardsViewer = (entranceView - ball.position).normalized;
                camera.transform.SetPositionAndRotation(ball.position + towardsViewer * radius * 4, Quaternion.LookRotation(-towardsViewer));
                var image = Capture(camera, edition + "/uwu-ball-readability-" + (index + 1).ToString("00"), 256, 256);
                try {
                    var pixels = image.GetPixels32(); var body = new List<float>(); int readable = 0;
                    for (int y = 0; y < image.height; y++) for (int x = 0; x < image.width; x++) {
                        float dx = ((x + .5f) / image.width * 2 - 1) * 1.3f;
                        float dy = ((y + .5f) / image.height * 2 - 1) * 1.3f;
                        if (dx * dx + dy * dy > .75f * .75f) continue;
                        var pixel = pixels[y * image.width + x];
                        float value = (pixel.r + pixel.g + pixel.b) / (255f * 3);
                        body.Add(value); if (value > .04f) readable++;
                    }
                    Require(body.Count > 1000, edition + " ball readability crop missed its sphere.");
                    body.Sort(); float median = body[body.Count / 2], fraction = (float)readable / body.Count;
                    scene.minimumBallMedianBrightness = Mathf.Min(scene.minimumBallMedianBrightness, median);
                    scene.minimumBallReadableFraction = Mathf.Min(scene.minimumBallReadableFraction, fraction);
                    scene.ballReadabilityViews++;
                    Require(median > .025f && fraction > .35f, edition + " uwu ball " + (index + 1)
                        + " remains an unreadable silhouette: median=" + median + ", readable body fraction=" + fraction);
                }
                finally { UnityEngine.Object.DestroyImmediate(image); renderer.enabled = false; }
            }
        }
        finally {
            for (int i = 0; i < renderers.Length; i++) if (renderers[i]) renderers[i].enabled = enabled[i];
            camera.orthographic = false; camera.clearFlags = CameraClearFlags.Skybox; camera.backgroundColor = oldBackground;
        }
    }

    static void FormationViews(Camera camera, string edition)
    {
        var formation = UnityEngine.Object.FindObjectOfType<RoomFormation>();
        Vector3 position = formation.space.position + new Vector3(-4, 2, -27);
        Vector3 target = formation.space.position + new Vector3(8, 12, 5);
        var positions = formation.pieces.Select(t => t.localPosition).ToArray();
        var rotations = formation.pieces.Select(t => t.localRotation).ToArray();
        try {
            View(camera, edition, "remembering-hall-assembled", position, target, 85);
            for (int i = 0; i < formation.pieces.Length; i++) {
                formation.pieces[i].localPosition = formation.scatteredPositions[i];
                formation.pieces[i].localRotation = formation.scatteredRotations[i];
            }
            Physics.SyncTransforms();
            CheckStandingSpace(formation.space.TransformPoint(new Vector3(0, .2f, 0)), edition + " dispersed-room foundation");
            View(camera, edition, "remembering-hall-scattered", position, target, 85);
        } finally {
            for (int i = 0; i < formation.pieces.Length; i++) {
                formation.pieces[i].localPosition = positions[i];
                formation.pieces[i].localRotation = rotations[i];
            }
            Physics.SyncTransforms();
        }
    }

    static void CheckStandingSpace(Vector3 feet, string label)
    {
        const int mask = (1 << 0) | (1 << 11);
        Require(Physics.Raycast(feet + Vector3.up, Vector3.down, 2, mask, QueryTriggerInteraction.Ignore), label + " has no walkable floor.");
        var obstructions = Physics.OverlapCapsule(feet + Vector3.up * .35f, feet + Vector3.up * 1.45f,
            .25f, mask, QueryTriggerInteraction.Ignore);
        Require(obstructions.Length == 0, label + " is obstructed by: " + string.Join(", ", obstructions.Select(c => c.transform.parent.name + "/" + c.name)));
    }

    static Material Clone(Material source, Dictionary<Material, Material> copies)
    {
        if (!source) return null;
        if (!copies.TryGetValue(source, out var copy)) {
            copy = new Material(source) { name = source.name + " (review)" };
            if (copy.HasProperty("_ReviewTime")) copy.SetFloat("_ReviewTime", ReviewTime);
            if (copy.HasProperty("_ApproachScale")) copy.SetFloat("_ApproachScale", 1);
            copies.Add(source, copy); TemporaryMaterials.Add(copy);
        }
        return copy;
    }
    static void View(Camera camera, string edition, string name, Vector3 position, Vector3 target, float fov = 74)
    {
        camera.orthographic = false; camera.fieldOfView = fov; camera.cullingMask = -1;
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position));
        var image = Capture(camera, edition + "/" + name, 1280, 800);
        UnityEngine.Object.DestroyImmediate(image);
    }
    static void Sky(Camera camera, string edition, string name, float scale, float fov)
    {
        camera.orthographic = false; camera.fieldOfView = fov; camera.cullingMask = 0;
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.transform.SetPositionAndRotation(new Vector3(-80, 1.74f, 80), Quaternion.LookRotation(Pole));
        RenderSettings.skybox.SetFloat("_ApproachScale", scale);
        var image = Capture(camera, edition + "/" + name, 800, 800);
        var check = new SkyRecord { edition = edition, view = name, scale = scale };
        int[] peaks = new int[72]; var pixels = image.GetPixels32();
        float tangent = Mathf.Tan(fov * Mathf.Deg2Rad * .5f);
        for (int y = 0; y < image.height; y++) for (int x = 0; x < image.width; x++) {
            float dx = ((x + .5f) / image.width * 2 - 1) * tangent, dy = ((y + .5f) / image.height * 2 - 1) * tangent;
            float angle = Mathf.Atan(Mathf.Sqrt(dx * dx + dy * dy)) / scale;
            var pixel = pixels[y * image.width + x]; int peak = Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b));
            if (angle < .14f) { check.coreSamples++; check.maximumCoreChannel = Mathf.Max(check.maximumCoreChannel, peak); }
            if (angle >= .183f && angle <= .207f) {
                int sector = Mathf.Clamp((int)((Mathf.Atan2(dy, dx) + Mathf.PI) / (2 * Mathf.PI) * peaks.Length), 0, peaks.Length - 1);
                peaks[sector] = Mathf.Max(peaks[sector], peak);
            }
        }
        check.minimumRimSectorPeak = peaks.Min(); report.sky.Add(check);
        UnityEngine.Object.DestroyImmediate(image);
        Require(check.coreSamples > 100 && check.maximumCoreChannel <= 1, edition + " " + name + " has light crossing the protected black core.");
        Require(check.minimumRimSectorPeak >= 24, edition + " " + name + " has a discontinuity in its light ring.");
    }
    static void Train(Camera camera, string edition, bool quest)
    {
        var train = UnityEngine.Object.FindObjectOfType<ApertureExpress>();
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "/Generated/" + (quest ? "Quest" : "PC") + "/SpiralService.anim");
        Require(train && train.service && clip, "Missing train animation in " + edition);
        var front = train.service.transform.Find("Coach 3");
        Require(front, "Front coach missing in " + edition);
        try {
            clip.SampleAnimation(train.service.gameObject, ApertureExpress.Summit + 2);
            RenderSettings.skybox.SetFloat("_ApproachScale", 3.1f);
            Vector3 position = front.TransformPoint(new Vector3(-.32f, 1.74f, 1.46f));
            View(camera, edition, "train-summit", position, position + front.TransformDirection(new Vector3(.021f, .002f, 1)), 74);
        }
        finally { clip.SampleAnimation(train.service.gameObject, 0); RenderSettings.skybox.SetFloat("_ApproachScale", 1); }
    }
    static void Film(Camera camera, string edition, bool quest, Dictionary<Material, Material> copies)
    {
        var original = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Generated/" + (quest ? "Quest" : "PC") + "/Finishes/Embankment receiver.mat");
        Require(original, edition + " receiver material is missing.");
        var material = Clone(original, copies);
        var panel = new GameObject("Temporary film review screen");
        panel.layer = 31; panel.transform.position = new Vector3(0, -10000, 0);
        var mesh = new Mesh { vertices = new[] { new Vector3(-16f / 9, -1, 0), new Vector3(16f / 9, -1, 0), new Vector3(16f / 9, 1, 0), new Vector3(-16f / 9, 1, 0) },
            uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up }, triangles = new[] { 0, 2, 1, 0, 3, 2 } };
        panel.AddComponent<MeshFilter>().sharedMesh = mesh;
        panel.AddComponent<MeshRenderer>().sharedMaterial = material;
        try {
            camera.orthographic = true; camera.orthographicSize = 1; camera.cullingMask = 1 << 31;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            camera.transform.SetPositionAndRotation(panel.transform.position + Vector3.back * 2, Quaternion.identity);
            foreach (float second in new[] { 2f, 11f, 18f, 23f }) {
                material.SetFloat("_BroadcastSecond", second);
                var image = Capture(camera, edition + "/film-" + second.ToString("00"), 1280, 720, second >= 22, second);
                if (second >= 22) Require(image.GetPixels32().All(p => p.r <= 1 && p.g <= 1 && p.b <= 1), "The film blackout does not return to black in " + edition);
                UnityEngine.Object.DestroyImmediate(image);
            }
        }
        finally { material.SetFloat("_BroadcastSecond", 2); UnityEngine.Object.DestroyImmediate(panel); UnityEngine.Object.DestroyImmediate(mesh); }
    }
    static void Menu(Camera camera, string edition)
    {
        var player = UnityEngine.Object.FindObjectOfType<BillboardPlayer>();
        var menu = player.menu; bool active = menu.activeSelf;
        Vector3 position = menu.transform.position; Quaternion rotation = menu.transform.rotation;
        try {
            menu.SetActive(true); menu.transform.SetPositionAndRotation(new Vector3(0, -2000, 0), Quaternion.identity);
            camera.orthographic = false; camera.fieldOfView = 65; camera.cullingMask = -1;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.015f, .025f, .04f);
            camera.transform.SetPositionAndRotation(new Vector3(0, -2000, -1.1f), Quaternion.identity);
            var image = Capture(camera, edition + "/local-controls", 1280, 800);
            UnityEngine.Object.DestroyImmediate(image);
        }
        finally { menu.transform.SetPositionAndRotation(position, rotation); menu.SetActive(active); }
    }
    static Texture2D Capture(Camera camera, string name, int width, int height, bool allowBlack = false, float filmSecond = -1)
    {
        var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var previous = RenderTexture.active; var oldTarget = camera.targetTexture;
        Texture2D image = null;
        try {
            camera.targetTexture = target; camera.aspect = (float)width / height; camera.Render();
            RenderTexture.active = target;
            image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
            byte[] bytes = image.EncodeToPNG(); string file = name + ".png";
            File.WriteAllBytes(Output + "/" + file, bytes);
            var pixels = image.GetPixels32(); double brightness = 0, lowerBrightness = 0;
            int magenta = 0, dark = 0, lit = 0, colored = 0, lowerLit = 0, lowerCount = width * (height / 3);
            for (int i = 0; i < pixels.Length; i++) {
                var p = pixels[i]; double value = (p.r + p.g + p.b) / (255.0 * 3); brightness += value;
                if (p.r > 250 && p.g < 5 && p.b > 250) magenta++;
                if (value < .02) dark++;
                if (value > .08) lit++;
                if (value > .08 && Mathf.Max(p.r, Mathf.Max(p.g, p.b)) - Mathf.Min(p.r, Mathf.Min(p.g, p.b)) > 30) colored++;
                if (i < lowerCount) { lowerBrightness += value; if (value > .035) lowerLit++; }
            }
            var record = new ImageRecord { file = file, sha256 = Digest(bytes), width = width, height = height,
                meanBrightness = (float)(brightness / pixels.Length), magentaPixels = magenta, position = camera.transform.position,
                darkFraction = (float)dark / pixels.Length, litFraction = (float)lit / pixels.Length, coloredLitFraction = (float)colored / pixels.Length,
                lowerFrameBrightness = (float)(lowerBrightness / Math.Max(1, lowerCount)), lowerFrameLitFraction = (float)lowerLit / Math.Max(1, lowerCount),
                forward = camera.transform.forward, fieldOfView = camera.fieldOfView, filmSecond = filmSecond,
                approachScale = RenderSettings.skybox && RenderSettings.skybox.HasProperty("_ApproachScale") ? RenderSettings.skybox.GetFloat("_ApproachScale") : 1 };
            report.images.Add(record);
            Require(magenta < pixels.Length / 200, "Possible error shader in " + file);
            Require(allowBlack || record.meanBrightness > .001f, "Unexpected empty image: " + file);
            return image;
        }
        catch { if (image) UnityEngine.Object.DestroyImmediate(image); throw; }
        finally { camera.targetTexture = oldTarget; RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); }
    }
    static void CheckShaders()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Shader", new[] { Root + "/Shaders" })) {
            string path = AssetDatabase.GUIDToAssetPath(guid); var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (!shader || !shader.isSupported) report.failures.Add("Unsupported shader: " + path);
            if (!shader) continue;
            foreach (var message in ShaderUtil.GetShaderMessages(shader)) {
                report.shaderMessages.Add(path + ":" + message.line + " " + message.severity + " " + message.message);
                if (message.severity.ToString() == "Error") report.failures.Add("Shader compilation failed: " + path + ":" + message.line + " " + message.message);
            }
        }
    }
    static string Digest(byte[] bytes)
    {
        using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
    }
}
