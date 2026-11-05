using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using VRC.SDK3.Components;

public static class Surface
{
    public const string Media = "Assets/Stillwater/Media";
    private static readonly Dictionary<string, Material> library = new Dictionary<string, Material>();
    private static bool mobile;
    private static string output;
    private static Shader finish;
    private static Texture weather;

    public static void Prepare(bool quest)
    {
        mobile = quest;
        output = "Assets/Stillwater/Generated/" + (quest ? "Quest" : "PC");
        Directory.CreateDirectory(output + "/Finishes");
        AssetDatabase.Refresh();
        foreach (var guid in AssetDatabase.FindAssets("", new[] { Media }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path);
            if ((importer is AudioImporter || importer is TextureImporter) &&
                importer.userData != StationMediaImporter.Version)
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
        library.Clear();
        finish = Shader.Find("Stillwater/Station finish");
        if (!finish) throw new InvalidOperationException("The station-finish shader could not be found.");
        weather = AssetDatabase.LoadAssetAtPath<Texture>(Media + "/Surfaces/WeatherField.png");
        Add("concrete", "Boardcast", "a3b4b4", 5.5f);
        Add("plaster", "Limewash", "d1c7af", 3.8f);
        Add("tile", "Porcelain", "729ba0", 3.2f, .38f);
        Add("wet", "Rainstone", "91a29e", 5.1f, .62f);
        Add("pool", "Rainstone", "547773", 6.8f, .8f);
        Add("metal", "Enamel", "526270", 3.0f, .48f);
        Add("rust", "Oxide", "aa785d", 3.5f, .22f);
        Add("glass", "Enamel", "87c2cd", 3f, .75f, .57f);
        Add("ivory", "Enamel", "e4d8b8", 4.1f, .20f);
        Add("ochre", "Limewash", "d4a556", 2.9f);
        Add("teal", "Enamel", "3c8e8d", 2.5f, .42f);
        Add("leaf", "Limewash", "6b855c", 1.75f);
        Add("bark", "Cambium", "997c68", 2.1f);
        Add("black", "Enamel", "30373b", 2.7f, .24f);
        Add("amber", null, "ffc17b", 2f, 0, 1.45f);
        Add("ice", null, "b3edff", 2f, 0, 1.40f);
        Add("red", null, "ff7655", 2f, 0, 1.28f);
        Add("pink", null, "ff83ba", 2f, 0, 1.45f);
        Add("brass", "Enamel", "c19d65", 3.1f, .60f);
        Add("paper", "Limewash", "e8dfbf", 2.2f);
        for (int i = 0; i < 4; i++)
        {
            var print = Add("art" + i, null, "ffffff", 1f, 0, .72f);
            print.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture>(Media + "/Prints/Plate" + i + ".jpg"));
            print.EnableKeyword("SW_PRINT");
            EditorUtility.SetDirty(print);
        }
        AssetDatabase.SaveAssets();
    }

    private static Material Add(string key, string texture, string pigment, float scale, float sheen = 0, float emission = 0)
    {
        string path = output + "/Finishes/" + key + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m)
        {
            m = new Material(finish) { name = key };
            AssetDatabase.CreateAsset(m, path);
        }
        m.shader = finish;
        m.enableInstancing = true;
        m.shaderKeywords = Array.Empty<string>();
        if (mobile) m.EnableKeyword("SW_MOBILE");
        if (key == "wet" || key == "pool") m.EnableKeyword("SW_WET");
        ColorUtility.TryParseHtmlString("#" + pigment, out Color color);
        m.SetColor("_Color", color);
        m.SetTexture("_MainTex", texture == null ? null : AssetDatabase.LoadAssetAtPath<Texture>(Media + "/Surfaces/" + texture + ".jpg"));
        m.SetTexture("_Field", weather);
        m.SetFloat("_Scale", scale);
        m.SetFloat("_Sheen", sheen);
        m.SetFloat("_Emission", emission);
        m.SetFloat("_Flood", key == "pool" ? 1 : 0);
        EditorUtility.SetDirty(m);
        library.Add(key, m);
        return m;
    }

    public static Material Get(string key)
    {
        return library[key];
    }

    public static void SetSky()
    {
        var path = output + "/Finishes/Night sky.mat";
        var sky = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!sky)
        {
            sky = new Material(Shader.Find("Stillwater/Eclipse sky")) { name = "Stillwater night" };
            AssetDatabase.CreateAsset(sky, path);
        }
        RenderSettings.skybox = sky;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(.34f, .43f, .52f);
        RenderSettings.ambientEquatorColor = new Color(.27f, .30f, .32f);
        RenderSettings.ambientGroundColor = new Color(.18f, .18f, .16f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = .0065f;
        RenderSettings.fogColor = new Color(.125f, .186f, .208f);
        RenderSettings.reflectionIntensity = .8f;
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
        RenderSettings.defaultReflectionResolution = 64;
    }

    public static void Ambience(Transform room, string id, bool quest)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Media + "/Audio/" + id + ".wav");
        if (!clip) throw new InvalidOperationException("Missing soundscape for " + id);
        var go = new GameObject(id + " / room tone");
        go.transform.SetParent(room, false);
        go.transform.localPosition = new Vector3(0, 3.2f, 0);
        var source = go.AddComponent<AudioSource>();
        var space = go.AddComponent<VRCSpatialAudioSource>();
        source.clip = clip;
        source.loop = true;
        source.playOnAwake = true;
        source.volume = .53f;
        source.priority = 190;
        source.spatialBlend = 1;
        source.dopplerLevel = 0;
        source.spread = 100;
        source.minDistance = 17;
        source.maxDistance = 57;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.spatialize = true;
        space.EnableSpatialization = true;
        space.UseAudioSourceVolumeCurve = true;
        space.Gain = 0;
        space.Near = 17;
        space.Far = 57;
        space.VolumetricRadius = 12;
        if (id == "hub" || id == "observatory") Rain(room, quest);
    }

    private static void Rain(Transform room, bool quest)
    {
        string path = output + "/Finishes/Rain.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!material)
        {
            material = new Material(Shader.Find("Stillwater/Drizzle"));
            AssetDatabase.CreateAsset(material, path);
        }
        var shower = new GameObject("Courtyard rain");
        shower.transform.SetParent(room, false);
        shower.transform.localPosition = new Vector3(0, 12.3f, 0);
        var particles = shower.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main;
        main.loop = true;
        main.prewarm = true;
        main.startLifetime = .94f;
        main.startSpeed = 13f;
        main.startSize3D = true;
        main.startSizeX = new ParticleSystem.MinMaxCurve(.023f, .037f);
        main.startSizeY = new ParticleSystem.MinMaxCurve(.24f, .48f);
        main.startSizeZ = .02f;
        main.startColor = new Color(.69f, .82f, .84f, .28f);
        main.maxParticles = quest ? 38 : 240;
        var shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.rotation = new Vector3(90, 0, 0);
        shape.scale = quest ? new Vector3(21, 21, .2f) : new Vector3(40, 40, .25f);
        var emission = particles.emission;
        emission.rateOverTime = quest ? 35 : 220;
        var render = particles.GetComponent<ParticleSystemRenderer>();
        render.renderMode = ParticleSystemRenderMode.VerticalBillboard;
        render.shadowCastingMode = ShadowCastingMode.Off;
        render.receiveShadows = false;
        render.sharedMaterial = material;
    }

    // Bake the geometry itself once, using the editor's graphics device. No runtime cameras.
    public static void Capture(Transform room, string id, bool quest)
    {
        if (quest || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
        const int size = 128;
        var cameraOwner = new GameObject("Reflection capture");
        var camera = cameraOwner.AddComponent<Camera>();
        camera.enabled = false;
        camera.allowHDR = false;
        camera.allowMSAA = false;
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.nearClipPlane = .14f;
        camera.farClipPlane = 125;
        camera.transform.position = room.TransformPoint(new Vector3(0, 3.4f, 0));
        var target = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32)
        {
            dimension = TextureDimension.Cube,
            hideFlags = HideFlags.HideAndDontSave
        };
        var read = new Texture2D(size, size, TextureFormat.RGB24, false);
        var cube = new Cubemap(size, TextureFormat.RGBA32, true) { name = id + " reflection" };
        var before = RenderTexture.active;
        try
        {
            target.Create();
            if (!camera.RenderToCubemap(target)) throw new InvalidOperationException("Could not render the room probe for " + id);
            for (int i = 0; i < 6; i++)
            {
                Graphics.SetRenderTarget(target, 0, (CubemapFace)i);
                read.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                read.Apply();
                cube.SetPixels(read.GetPixels(), (CubemapFace)i);
            }
            cube.Apply(true);
            var path = output + "/Finishes/" + id + " reflection.asset";
            var old = AssetDatabase.LoadAssetAtPath<Cubemap>(path);
            if (old) { EditorUtility.CopySerialized(cube, old); UnityEngine.Object.DestroyImmediate(cube); }
            else { AssetDatabase.CreateAsset(cube, path); old = cube; }
            var anchor = new GameObject(id + " / static room reflection");
            anchor.transform.SetParent(room, false);
            anchor.transform.localPosition = new Vector3(0, 3.4f, 0);
            var probe = anchor.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Custom;
            probe.customBakedTexture = old;
            probe.boxProjection = true;
            probe.size = new Vector3(75, 52, 75);
            probe.center = new Vector3(0, 18, 0);
            probe.blendDistance = 3;
            probe.importance = 2;
        }
        finally
        {
            RenderTexture.active = before;
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(read);
            UnityEngine.Object.DestroyImmediate(cameraOwner);
            if (cube && !AssetDatabase.Contains(cube)) UnityEngine.Object.DestroyImmediate(cube);
        }
    }
}

public class StationMediaImporter : AssetPostprocessor
{
    public const string Version = "stillwater-media-3";
    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Surface.Media + "/", StringComparison.Ordinal)) return;
        var importer = (TextureImporter)assetImporter;
        bool print = assetPath.Contains("/Prints/");
        bool plate = assetPath.EndsWith("/Station plates.png", StringComparison.Ordinal);
        bool map = assetPath.EndsWith("/Walking routes.png", StringComparison.Ordinal);
        importer.wrapMode = print ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
        importer.sRGBTexture = !assetPath.EndsWith("WeatherField.png", StringComparison.Ordinal);
        importer.mipmapEnabled = true;
        importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = print ? 4 : 8;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = plate ? 2048 : map || print ? 1024 : 512;
        var android = importer.GetPlatformTextureSettings("Android");
        android.name = "Android";
        android.overridden = true;
        android.maxTextureSize = plate ? 2048 : map ? 1024 : print ? 512 : 256;
        android.format = TextureImporterFormat.ASTC_6x6;
        importer.SetPlatformTextureSettings(android);
        importer.userData = Version;
    }
    private void OnPreprocessAudio()
    {
        if (!assetPath.StartsWith(Surface.Media + "/Audio/", StringComparison.Ordinal)) return;
        var importer = (AudioImporter)assetImporter;
        importer.forceToMono = true;
        var settings = importer.defaultSampleSettings;
        settings.compressionFormat = AudioCompressionFormat.Vorbis;
        settings.quality = .55f;
        settings.loadType = AudioClipLoadType.CompressedInMemory;
        settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
        importer.defaultSampleSettings = settings;
        importer.userData = Version;
    }
}


