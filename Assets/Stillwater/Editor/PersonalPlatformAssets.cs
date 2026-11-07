using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UdonSharpEditor;
using VRC.SDK3.Components;

// Licensed source meshes/textures/audio are restored privately by the owner.
// This public authoring code contains placement and newly authored materials only.
internal static class PersonalPlatformAssets
{
    const string Kitty = "Assets/Stillwater/Personal/Kitty/";
    const string TrainClip = "Assets/Stillwater/Personal/TrainInterior/train0049_Loop_low.wav";
    internal static void Install(Transform root, string gen, bool quest, WorldAudioDirector audio)
    {
        var rail = root.Find("rail");
        if (!rail) throw new InvalidOperationException("Personal platform assets require the final rail district.");
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(Kitty + "FBX.fbx");
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(TrainClip);
        if (!model || !clip)
            throw new InvalidOperationException("Restore the owner's private platform asset pack before authoring this release.");
        var source = model.GetComponentsInChildren<MeshFilter>(true).SingleOrDefault(m => m.name == "Wall mounted LED");
        if (!source || !source.sharedMesh || source.sharedMesh.subMeshCount != 3)
            throw new InvalidOperationException("The licensed wall LED model or its three material slots changed.");

        var lights = WorldAuthor.Child(rail, "Platform personal lighting");
        var hardware = new StationMesh(lights, gen + "/Architecture");
        var neon = Material(gen, "Kitty warm pink LED", quest, new Color(1, .36f, .62f), true, false);
        var metal = Material(gen, "Kitty satin metal", quest, new Color(.48f, .56f, .60f), false, true);
        var cable = Material(gen, "Kitty insulated cable", quest, new Color(.034f, .041f, .046f), false, false);
        float[] positions = { -24, -6 };
        for (int i = 0; i < positions.Length; i++) {
            // Behind the platform, above head height, away from the central boarding
            // ramp and the west-facing carriage doors. No collision is introduced.
            Vector3 center = new Vector3(8.6f, 3.7f, positions[i]);
            var mount = WorldAuthor.Child(lights, "Kitty platform light " + (i + 1));
            mount.localRotation = Quaternion.Euler(0, -90, 0);
            var decoration = UnityEngine.Object.Instantiate(source.gameObject, mount, false);
            decoration.name = "Licensed wall LED";
            var renderer = decoration.GetComponent<MeshRenderer>();
            if (!renderer) throw new InvalidOperationException("Wall LED renderer is absent.");
            renderer.sharedMaterials = new[] { neon, metal, cable };
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.reflectionProbeUsage = quest ? ReflectionProbeUsage.Off : ReflectionProbeUsage.BlendProbes;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            float height = renderer.bounds.size.y;
            if (height < .0001f) throw new InvalidOperationException("Wall LED has an empty import bound.");
            mount.localScale = Vector3.one * (1.9f / height);
            mount.position += rail.TransformPoint(center) - renderer.bounds.center;
            // Preserve artist-authored model transforms; no derived copy of the
            // licensed mesh is written into the public generated architecture.
            GameObjectUtility.SetStaticEditorFlags(decoration, StaticEditorFlags.OccludeeStatic);
            hardware.Box("metal", 8.6f, 1.63f, positions[i], .14f, 3.26f, .14f);
            hardware.Box("brass", 8.6f, 2.63f, positions[i], .25f, .12f, .25f);
            hardware.Box("black", 8.6f, .075f, positions[i], .55f, .15f, .55f);
        }
        hardware.Finish();

        var service = rail.GetComponentInChildren<ApertureExpress>(true);
        if (!service) throw new InvalidOperationException("The final platform train service is absent.");
        var owner = WorldAuthor.Child(rail, "Local carriage ambience").gameObject;
        var sourceAudio = owner.AddComponent<AudioSource>();
        sourceAudio.clip = clip; sourceAudio.loop = true; sourceAudio.playOnAwake = false;
        sourceAudio.spatialBlend = 0; sourceAudio.spatialize = false; sourceAudio.volume = 0;
        sourceAudio.dopplerLevel = 0; sourceAudio.priority = 170;
        var spatial = owner.AddComponent<VRCSpatialAudioSource>(); spatial.EnableSpatialization = false; spatial.Gain = 0;
        service.carriageAmbience = sourceAudio; service.audioDirector = audio;
        UdonSharpEditorUtility.CopyProxyToUdon(service);
    }

    static Material Material(string gen, string name, bool quest, Color color, bool glow, bool metal)
    {
        string path = gen + "/Finishes/" + name + ".mat";
        var shader = Shader.Find(quest ? "Unlit/Color" : "Standard");
        if (!shader) throw new InvalidOperationException("Required platform light shader is missing.");
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
        material.shader = shader; material.shaderKeywords = Array.Empty<string>(); material.enableInstancing = true;
        material.SetColor("_Color", color);
        if (!quest) {
            material.SetFloat("_Metallic", metal ? .72f : 0);
            material.SetFloat("_Glossiness", metal ? .70f : .43f);
            material.SetColor("_EmissionColor", glow ? color * 1.65f : Color.black);
            if (glow) material.EnableKeyword("_EMISSION");
            if (metal) {
                var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(Kitty + "Metal nm.png");
                if (!normal) throw new InvalidOperationException("Restore the licensed platform metal normal map.");
                material.SetTexture("_BumpMap", normal); material.SetFloat("_BumpScale", .35f);
                material.EnableKeyword("_NORMALMAP");
            }
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        EditorUtility.SetDirty(material); return material;
    }
}

internal sealed class PersonalPlatformImporter : AssetPostprocessor
{
    void OnPreprocessModel()
    {
        if (assetPath != "Assets/Stillwater/Personal/Kitty/FBX.fbx") return;
        var importer = (ModelImporter)assetImporter;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.importAnimation = false; importer.importCameras = false; importer.importLights = false;
        importer.addCollider = false; importer.isReadable = false;
    }
    void OnPreprocessTexture()
    {
        if (assetPath != "Assets/Stillwater/Personal/Kitty/Metal nm.png") return;
        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.NormalMap; importer.textureShape = TextureImporterShape.Texture2D;
        importer.sRGBTexture = false; importer.maxTextureSize = 1024; importer.mipmapEnabled = true;
        importer.filterMode = FilterMode.Trilinear; importer.wrapMode = TextureWrapMode.Repeat;
        importer.textureCompression = TextureImporterCompression.Compressed;
        var mobile = importer.GetPlatformTextureSettings("Android");
        mobile.name = "Android"; mobile.overridden = true; mobile.maxTextureSize = 128;
        mobile.format = TextureImporterFormat.ASTC_6x6; importer.SetPlatformTextureSettings(mobile);
    }
    void OnPreprocessAudio()
    {
        if (assetPath != "Assets/Stillwater/Personal/TrainInterior/train0049_Loop_low.wav") return;
        var importer = (AudioImporter)assetImporter; importer.forceToMono = true;
        var settings = importer.defaultSampleSettings;
        settings.compressionFormat = AudioCompressionFormat.Vorbis; settings.quality = .25f;
        settings.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate; settings.sampleRateOverride = 24000;
        settings.loadType = AudioClipLoadType.CompressedInMemory; importer.defaultSampleSettings = settings;
    }
}
