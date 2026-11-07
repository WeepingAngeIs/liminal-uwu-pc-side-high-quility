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
        public Vector3 position, forward;
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
            WorldAuthor.Author(); // Includes all 17 PC custom reflection cubemap captures.
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
        foreach (string guid in AssetDatabase.FindAssets("t:UdonSharpProgramAsset", new[] { Root + "/Runtime" })) {
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
        }
        Require(report.programs.Count >= 8, "Expected all world runtime programs to have compiled assets.");
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
        foreach (string area in new[] { "billboard", "concourse", "causeway", "stacks", "returns", "waterfall" })
            Require(root.transform.Find(area), edition + " missing new district " + area);
        var journey = root.GetComponentInChildren<AdventureJourney>(true);
        Require(journey && journey.entrances.Length == 15 && journey.destinations.Length == 15 && journey.halfExtents.Length == 15,
            edition + " requires all fifteen walk-through adventure doors.");
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
        if (edition == "PC") Require(result.reflectionCount == 17 && result.reflections.All(p => p.sha256 != "missing"), "Expected 17 authored PC reflection cubemaps.");
        return result;
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
            var pixels = image.GetPixels32(); double brightness = 0; int magenta = 0;
            foreach (var p in pixels) { brightness += (p.r + p.g + p.b) / (255.0 * 3); if (p.r > 250 && p.g < 5 && p.b > 250) magenta++; }
            var record = new ImageRecord { file = file, sha256 = Digest(bytes), width = width, height = height,
                meanBrightness = (float)(brightness / pixels.Length), magentaPixels = magenta, position = camera.transform.position,
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
