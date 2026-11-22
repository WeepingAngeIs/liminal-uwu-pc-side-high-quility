using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Staged in Assets/Editor only for the explicitly selected hosted prototype run.
public static class ObservatoryPrototypeReview
{
    public const string Output = "Builds/ObservatoryReview";
    public const string Bundle = "Builds/ObservatoryPrivate/observatory-pc.vrcw";
    [Serializable] class Report
    {
        public string revision, unity, graphicsDevice, questMethod;
        public List<Photo> images = new List<Photo>();
    }
    [Serializable] class Photo { public string path, sha256; }

    public static void Run()
    {
        Check(Application.isBatchMode && EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneWindows64,
            "Prototype export must run in the Windows-targeted batch editor.");
        var report = new Report { revision = File.ReadAllText("Builds/author-revision.txt").Trim(),
            unity = Application.unityVersion, graphicsDevice = SystemInfo.graphicsDeviceName,
            questMethod = "Saved Quest scene and mobile materials rendered in the desktop Unity editor" };
        var releaseScenes = new[] { WorldAuthor.PCScene, WorldAuthor.QuestScene };
        var originals = releaseScenes.Select(path => Digest(File.ReadAllBytes(path))).ToArray();
        ObservatoryPrototype.AuthorBoth();
        Inspect("PC", ObservatoryPrototype.PCScene, report);
        Inspect("Quest", ObservatoryPrototype.QuestScene, report);
        LiminalExport.BuildScene(ObservatoryPrototype.PCScene, Bundle);
        for (int i = 0; i < originals.Length; i++)
            Check(Digest(File.ReadAllBytes(releaseScenes[i])) == originals[i], "Production scene was modified.");
        Check(File.Exists(Bundle), "SDK did not export the prototype bundle.");
        File.WriteAllText(Output + "/report.json", JsonUtility.ToJson(report, true));
    }

    static void Inspect(string edition, string path, Report report)
    {
        Check(path != WorldAuthor.PCScene && path != WorldAuthor.QuestScene, "Expected an isolated prototype scene.");
        EditorSceneManager.OpenScene(path);
        SurfaceSeamReview.InspectCurrentScene(edition, "Builds/SurfaceReview", Photograph);
        var receiver = UnityEngine.Object.FindObjectsOfType<ObservatoryReceiver>(true).Single();
        var controls = UnityEngine.Object.FindObjectsOfType<ObservatoryControl>(true);
        Check(controls.Length == 2 && controls.Count(c => c.recordsClock) == 1 && controls.All(c => c.receiver == receiver),
            "Prototype controls lost their receiver bindings.");
        Check(receiver.receiverDisplay && receiver.clockDisplay && receiver.penNeedle && receiver.pendulum
            && receiver.secondSpool && receiver.serviceHand && receiver.clockHand, "Incomplete instrument.");
        foreach (var control in controls) Check(control.GetComponent<Collider>(), "Unselectable control: " + control.name);
        Directory.CreateDirectory(Output + "/" + edition);
        var camera = new GameObject("Observatory review camera").AddComponent<Camera>();
        camera.enabled = false;
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.allowHDR = false; camera.allowMSAA = false;
        camera.useOcclusionCulling = false; camera.nearClipPlane = .04f; camera.farClipPlane = 500;
        try {
            Add("listening-bench", new Vector3(-15.3f, 1.64f, 11), new Vector3(-15.1f, 1.25f, 9.15f), 56);
            Add("rain-exchange", new Vector3(-10.8f, 1.8f, 14.45f), new Vector3(-15.3f, 1.45f, 9), 63);
            Add("ceiling-clock", new Vector3(-73, 13.4f, -18.5f), new Vector3(-72, 16, -13), 52);
            Add("service-dial", new Vector3(-71.6f, 1.64f, -15.4f), new Vector3(-72, 1.33f, -13), 56);
        } finally { UnityEngine.Object.DestroyImmediate(camera.gameObject); }

        void Add(string name, Vector3 eye, Vector3 aim, float fov)
        {
            string file = edition + "/" + name + ".png";
            byte[] image = Photograph(camera, eye, aim, fov);
            File.WriteAllBytes(Output + "/" + file, image);
            report.images.Add(new Photo { path = file, sha256 = Digest(image) });
        }
    }

    public static byte[] Photograph(Camera camera, Vector3 eye, Vector3 aim, float fov)
    {
        camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(aim - eye));
        camera.fieldOfView = fov;
        const int width = 1280, height = 800;
        var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var previousTarget = camera.targetTexture;
        var active = RenderTexture.active;
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        try {
            camera.targetTexture = target; camera.aspect = (float)width / height; camera.Render();
            RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
            var pixels = image.GetPixels32();
            Check(pixels.Any(p => p.r > 60 || p.b > 60 || p.g > 60), "Capture was dark.");
            Check(pixels.Count(p => p.r > 249 && p.g < 5 && p.b > 249) < pixels.Length / 500, "Missing shader in capture.");
            return image.EncodeToPNG();
        } finally {
            camera.targetTexture = previousTarget; RenderTexture.active = active;
            UnityEngine.Object.DestroyImmediate(image); RenderTexture.ReleaseTemporary(target);
        }
    }
    public static string Digest(byte[] bytes)
    {
        using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
    }
    static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
