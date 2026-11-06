using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Staged into Assets/Editor by visual-upgrade.yml on the licensed hosted runner.
public static class VisualUpgradeReview
{
    const string Root = "Assets/Stillwater";
    const string Output = "Builds/VisualReview";
    const float ReviewTime = 12.5f;
    static readonly Vector3 Pole = new Vector3(0, .743144825f, .669130606f);
    static readonly List<Material> TemporaryMaterials = new List<Material>();
    static Report report;
    static float frameTime;

    [Serializable] sealed class ImageRecord
    {
        public string file, sha256;
        public int width, height, magentaPixels;
        public float meanBrightness, shaderTime, fieldOfView;
        public Vector3 position, forward;
    }
    [Serializable] sealed class SkyRecord
    {
        public string variant;
        public int coreSamples, maximumCoreChannel, minimumRimSectorPeak;
        public int[] rimSectorPeaks;
    }
    [Serializable] sealed class Report
    {
        public string revision, baseline, unity, device, vendor, graphicsApi, graphicsVersion, colorSpace, utc;
        public string eyePairMethod = "Two parallel monocular renders separated by 0.064 metres. No XR runtime or headset single-pass instancing is exercised.";
        public string reflectionMethod = "Each scene's committed baked reflection maps are preserved during the shader comparison.";
        public float eyeSeparationMetres = .064f;
        public List<ImageRecord> images = new List<ImageRecord>();
        public List<SkyRecord> sky = new List<SkyRecord>();
        public List<string> shaderMessages = new List<string>();
        public List<string> failures = new List<string>();
    }
    sealed class RendererMaterials
    {
        public Renderer renderer;
        public Material[] original;
    }

    public static void Capture()
    {
        Directory.CreateDirectory(Output);
        report = new Report {
            revision = File.Exists("Builds/review-revision.txt") ? File.ReadAllText("Builds/review-revision.txt").Trim() : "unspecified",
            baseline = File.Exists("Builds/review-baseline.txt") ? File.ReadAllText("Builds/review-baseline.txt").Trim() : "unspecified",
            unity = Application.unityVersion, device = SystemInfo.graphicsDeviceName,
            vendor = SystemInfo.graphicsDeviceVendor, graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
            graphicsVersion = SystemInfo.graphicsDeviceVersion, colorSpace = QualitySettings.activeColorSpace.ToString(),
            utc = DateTime.UtcNow.ToString("O")
        };
        Camera camera = null;
        Material originalSky = null;
        RendererMaterials[] renderers = null;
        try
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use the hosted batch review workflow.");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Rendering requires a graphics device. Remove -nographics.");
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.OpenGLCore)
                throw new InvalidOperationException("Expected the workflow's Mesa OpenGL Core context.");
            EditorSceneManager.OpenScene(Root + "/Scenes/liminal uwu - PC.unity");
            originalSky = RenderSettings.skybox;
            if (!originalSky) throw new InvalidOperationException("The PC sky material is missing.");
            var baselineSky = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Editor/ReviewBaselines/Eclipse.shader");
            var baselineFloor = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Editor/ReviewBaselines/StationFinish.shader");
            if (!baselineSky || !baselineFloor) throw new InvalidOperationException("Staged baseline shaders are missing.");
            if (!originalSky.HasProperty("_ReviewTime"))
                throw new InvalidOperationException("The upgraded sky needs its deterministic _ReviewTime property.");
            renderers = UnityEngine.Object.FindObjectsOfType<Renderer>().Select(r => new RendererMaterials {
                renderer = r, original = r.sharedMaterials
            }).ToArray();
            foreach (var rain in UnityEngine.Object.FindObjectsOfType<ParticleSystem>()) {
                rain.useAutoRandomSeed = false; rain.randomSeed = 193;
                rain.Simulate(1.1f, true, true, true);
            }
            camera = new GameObject("Hosted visual review camera").AddComponent<Camera>();
            camera.enabled = false; camera.clearFlags = CameraClearFlags.Skybox;
            camera.nearClipPlane = .1f; camera.farClipPlane = 300;
            camera.allowHDR = false; camera.allowMSAA = false;
            camera.renderingPath = RenderingPath.Forward;
            camera.useOcclusionCulling = false;
            Camera.onPreRender += SetClock;
            foreach (string variant in new[] { "before", "after" }) {
                bool before = variant == "before";
                RenderSettings.skybox = Clone(originalSky, before ? baselineSky : originalSky.shader);
                var replacements = new Dictionary<Material, Material>();
                foreach (var item in renderers) {
                    item.renderer.sharedMaterials = item.original.Select(m => {
                        if (!m || m.shader.name != "Stillwater/Station finish") return m;
                        if (!replacements.TryGetValue(m, out var replacement)) {
                            replacement = Clone(m, before ? baselineFloor : m.shader);
                            replacements.Add(m, replacement);
                        }
                        return replacement;
                    }).ToArray();
                }
                frameTime = ReviewTime;
                View(camera, variant, "observatory", new Vector3(-84.5f, 1.8f, 66), new Vector3(0, .90f, 1), 77, true);
                View(camera, variant, "photon-ring", new Vector3(-83, 1.74f, 71), new Vector3(0, 1.11f, 1), 37, false);
                View(camera, variant, "rain-exchange-reflections", new Vector3(-1.2f, 1.25f, -19.2f), new Vector3(.052f, -.11f, 1), 74, false);
                View(camera, variant, "platform-reflections", new Vector3(7.2f, 1.2f, 48.9f), new Vector3(.53f, -.065f, 1), 66, false);
                TrainViews(camera, variant);
                SkyCheck(camera, variant);
                if (!before) {
                    foreach (float second in new[] { 0f, 6f, 12f }) {
                        frameTime = second;
                        RenderSettings.skybox.SetFloat("_ReviewTime", frameTime);
                        View(camera, variant, "motion-" + second.ToString("00"), new Vector3(-83, 1.74f, 71), Pole, 37, false);
                    }
                }
            }
            foreach (string file in new[] { Root + "/Shaders/Eclipse.shader", Root + "/Shaders/StationFinish.shader",
                "Assets/Editor/ReviewBaselines/Eclipse.shader", "Assets/Editor/ReviewBaselines/StationFinish.shader" }) {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(file);
                if (!shader || !shader.isSupported) report.failures.Add("Unsupported shader " + file);
                if (!shader) continue;
                foreach (var message in ShaderUtil.GetShaderMessages(shader)) {
                    report.shaderMessages.Add(file + ":" + message.line + " " + message.severity + " " + message.message);
                    if (message.severity.ToString() == "Error") report.failures.Add("Shader compilation error in " + file);
                }
            }
        }
        catch (Exception e) { report.failures.Add(e.ToString()); }
        finally
        {
            Camera.onPreRender -= SetClock;
            if (originalSky) RenderSettings.skybox = originalSky;
            if (renderers != null) foreach (var item in renderers)
                if (item.renderer) item.renderer.sharedMaterials = item.original;
            if (camera) UnityEngine.Object.DestroyImmediate(camera.gameObject);
            foreach (var material in TemporaryMaterials) UnityEngine.Object.DestroyImmediate(material);
            TemporaryMaterials.Clear();
            File.WriteAllText(Output + "/report.json", JsonUtility.ToJson(report, true));
            WriteReport();
        }
        if (report.failures.Count > 0)
            throw new InvalidOperationException("Visual review failed. See Builds/VisualReview/report.json.");
        Debug.Log("VISUAL_UPGRADE_REVIEW_PASS " + report.images.Count + " PNG captures on " + report.device);
    }

    static Material Clone(Material source, Shader shader)
    {
        var material = new Material(source) { shader = shader, hideFlags = HideFlags.HideAndDontSave };
        if (material.HasProperty("_ReviewTime")) material.SetFloat("_ReviewTime", ReviewTime);
        if (material.HasProperty("_BroadcastSecond")) material.SetFloat("_BroadcastSecond", 2);
        TemporaryMaterials.Add(material);
        return material;
    }
    static void SetClock(Camera camera)
    {
        Shader.SetGlobalVector("_Time", new Vector4(frameTime / 20, frameTime, frameTime * 2, frameTime * 3));
    }
    static void TrainViews(Camera camera, string variant)
    {
        var consist = GameObject.Find("liminal uwu/rail/Reciprocating local");
        var trip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "/Generated/PC/SpiralService.anim");
        if (!consist || !trip) throw new InvalidOperationException("Missing express train or animation.");
        var front = consist.transform.Find("Coach 3");
        if (!front) throw new InvalidOperationException("Missing front coach.");
        try {
            trip.SampleAnimation(consist, 78);
            View(camera, variant, "on-the-helix", front.TransformPoint(new Vector3(-.43f, 1.70f, .65f)),
                front.TransformDirection(new Vector3(-.10f, .045f, 1)), 74, false);
            trip.SampleAnimation(consist, 124);
            View(camera, variant, "summit", front.TransformPoint(new Vector3(-.32f, 1.74f, 1.46f)),
                front.TransformDirection(new Vector3(.021f, .002f, 1)), 67, true);
        } finally { trip.SampleAnimation(consist, 0); }
    }
    static void View(Camera camera, string variant, string name, Vector3 origin, Vector3 forward, float fov, bool eyes)
    {
        camera.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(forward));
        camera.fieldOfView = fov;
        var centre = Render(camera, variant + "/" + name, 1280, 800);
        UnityEngine.Object.DestroyImmediate(centre);
        if (!eyes) return;
        foreach (int eye in new[] { -1, 1 }) {
            camera.transform.position = origin + camera.transform.right * (.032f * eye);
            var image = Render(camera, variant + "/" + name + (eye < 0 ? "-left" : "-right"), 900, 600);
            UnityEngine.Object.DestroyImmediate(image);
        }
        camera.transform.position = origin;
    }
    static Texture2D Render(Camera camera, string name, int width, int height)
    {
        string path = Output + "/" + name + ".png";
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var previous = RenderTexture.active;
        var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB, 1);
        var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        try {
            camera.aspect = (float)width / height; camera.targetTexture = target;
            camera.Render(); RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
            byte[] bytes = texture.EncodeToPNG(); File.WriteAllBytes(path, bytes);
            var pixels = texture.GetPixels32();
            double sum = 0; int magenta = 0;
            foreach (Color32 pixel in pixels) {
                sum += (pixel.r + pixel.g + pixel.b) / 765.0;
                if (pixel.r > 250 && pixel.g < 5 && pixel.b > 250) magenta++;
            }
            string hash;
            using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
            report.images.Add(new ImageRecord { file = name + ".png", sha256 = hash, width = width, height = height,
                meanBrightness = (float)(sum / pixels.Length), magentaPixels = magenta, shaderTime = frameTime,
                fieldOfView = camera.fieldOfView, position = camera.transform.position, forward = camera.transform.forward });
            if (magenta > pixels.Length / 200) report.failures.Add("Pink error shader in " + name);
            if (sum / pixels.Length < .001) report.failures.Add("Empty render " + name);
            return texture;
        } catch {
            UnityEngine.Object.DestroyImmediate(texture); throw;
        } finally {
            camera.targetTexture = null; RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target);
        }
    }
    static void SkyCheck(Camera camera, string variant)
    {
        camera.transform.SetPositionAndRotation(new Vector3(-83, 1.74f, 71), Quaternion.LookRotation(Pole));
        camera.fieldOfView = 43;
        int oldMask = camera.cullingMask; camera.cullingMask = 0;
        Texture2D texture = null;
        try {
            texture = Render(camera, variant + "/sky-core-and-ring", 800, 800);
            var pixels = texture.GetPixels32();
            var metric = new SkyRecord { variant = variant, rimSectorPeaks = new int[72] };
            float tangent = Mathf.Tan(camera.fieldOfView * .5f * Mathf.Deg2Rad);
            for (int y = 0; y < texture.height; y++) for (int x = 0; x < texture.width; x++) {
                float dx = (2 * (x + .5f) / texture.width - 1) * tangent;
                float dy = (2 * (y + .5f) / texture.height - 1) * tangent;
                float angle = Mathf.Atan(Mathf.Sqrt(dx * dx + dy * dy));
                Color32 pixel = pixels[y * texture.width + x];
                int peak = Math.Max(pixel.r, Math.Max(pixel.g, pixel.b));
                if (angle < .14f) {
                    metric.coreSamples++;
                    metric.maximumCoreChannel = Math.Max(metric.maximumCoreChannel, peak);
                }
                if (angle > .183f && angle < .207f) {
                    int sector = Mathf.Clamp((int)((Mathf.Atan2(dy, dx) + Mathf.PI) / (2 * Mathf.PI) * 72), 0, 71);
                    metric.rimSectorPeaks[sector] = Math.Max(metric.rimSectorPeaks[sector], peak);
                }
            }
            metric.minimumRimSectorPeak = metric.rimSectorPeaks.Min();
            report.sky.Add(metric);
            if (metric.coreSamples < 1000 || metric.maximumCoreChannel > 1)
                report.failures.Add(variant + " sky has light inside the protected black core.");
            if (metric.minimumRimSectorPeak < 24)
                report.failures.Add(variant + " sky has an unlit five-degree section of its ring.");
        } finally {
            camera.cullingMask = oldMask;
            if (texture) UnityEngine.Object.DestroyImmediate(texture);
        }
    }
    static void WriteReport()
    {
        var text = new StringBuilder();
        text.AppendLine("# Hosted Unity visual review\n");
        text.AppendLine("Revision " + report.revision + " against shader baseline " + report.baseline + ".\n");
        text.AppendLine("Unity " + report.unity + " rendered through " + report.graphicsApi + " on " + report.device + ".\n");
        text.AppendLine(report.eyePairMethod + "\n");
        text.AppendLine(report.reflectionMethod + "\n");
        text.AppendLine("These software renders do not measure headset frame rate, VRChat client behavior or Windows Direct3D rendering.\n");
        text.AppendLine("| Variant | Core samples | Brightest core channel / 255 | Dimmest rim sector peak / 255 |");
        text.AppendLine("|---|---:|---:|---:|");
        foreach (var sky in report.sky)
            text.AppendLine("| " + sky.variant + " | " + sky.coreSamples + " | " + sky.maximumCoreChannel + " | " + sky.minimumRimSectorPeak + " |");
        text.AppendLine();
        foreach (string failure in report.failures) text.AppendLine("- " + failure);
        if (report.failures.Count == 0) text.AppendLine("All render checks passed.\n");
        foreach (string name in new[] { "observatory", "photon-ring", "summit", "rain-exchange-reflections", "platform-reflections" }) {
            text.AppendLine("## " + name.Replace("-", " ") + "\n");
            text.AppendLine("Before\n\n![](before/" + name + ".png)\n\nAfter\n\n![](after/" + name + ".png)\n");
        }
        File.WriteAllText(Output + "/report.md", text.ToString());
    }
}
