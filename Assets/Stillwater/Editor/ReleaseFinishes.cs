using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Scanned albedo and packed XY normal / roughness / AO share one metre scale on all geometry.
internal static class ReleaseFinishes
{
    const string Scans = "Assets/Stillwater/Release/Scans/";
    static readonly Dictionary<string, Material> finishes = new Dictionary<string, Material>();
    public static void Prepare(string folder, bool mobile)
    {
        finishes.Clear();
        Add("bath tile", "blue_floor_tiles_01", "b3f7e8", 2.2f, .68f, 0, folder, mobile);
        Add("porcelain", "blue_floor_tiles_01", "fff3cc", 2.7f, .58f, .88f, folder, mobile);
        Add("slate roof", "grey_roof_tiles", "687b88", 2.8f, .30f, .3f, folder, mobile);
        Add("vermilion", "wood_plank_wall", "d05c32", 3.6f, .23f, 1, folder, mobile);
        Add("cedar", "wood_plank_wall", "ad785b", 2.8f, .22f, .35f, folder, mobile);
        Add("limestone", "painted_plaster_wall", "e6ddd0", 4.4f, .06f, .8f, folder, mobile);
        Add("brickwork", "brick_wall_005", "d8ac92", 3.65f, .18f, .05f, folder, mobile);
        Add("terrazzo", "granite_tile", "b5c3bf", 2.8f, .49f, .60f, folder, mobile);
        Add("garden flags", "granite_tile", "afa899", 3.2f, .26f, .75f, folder, mobile);
        var shader=Shader.Find("Liminal uwu/Shoji paper");
        if(!shader)throw new InvalidOperationException("Missing shoji shader.");
        string path=folder+"/Finishes/Backlit shoji.mat";
        var paper=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!paper){paper=new Material(shader);AssetDatabase.CreateAsset(paper,path);}
        paper.SetTexture("_MainTex",AssetDatabase.LoadAssetAtPath<Texture>(Surface.Media+"/Surfaces/Limewash.jpg"));
        paper.SetColor("_Color",new Color(1f,.72f,.43f));paper.enableInstancing=true;
        EditorUtility.SetDirty(paper);finishes["shoji"]=paper;
    }
    static void Add(string name,string scan,string rgb,float metres,float wet,float desaturate,string folder,bool mobile)
    {
        string path=folder+"/Finishes/"+name+".mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader=Shader.Find("Liminal uwu/Surveyed surface");
        if(!shader)throw new InvalidOperationException("Surveyed surface shader is missing.");
        if(!material){material=new Material(shader);AssetDatabase.CreateAsset(material,path);}
        material.shader=shader; material.enableInstancing=true;
        var color=AssetDatabase.LoadAssetAtPath<Texture2D>(Scans+scan+"_color.jpg");
        var detail=AssetDatabase.LoadAssetAtPath<Texture2D>(Scans+scan+"_detail.png");
        if(!color||!detail)throw new InvalidOperationException("Missing scan "+scan);
        ColorUtility.TryParseHtmlString("#"+rgb,out var tint);
        material.SetTexture("_MainTex",color);material.SetTexture("_Detail",mobile?null:detail);
        material.SetColor("_Tint",tint);material.SetFloat("_Metres",metres);
        material.SetFloat("_Wet",wet);material.SetFloat("_Neutral",desaturate);
        if(mobile)material.EnableKeyword("SURVEY_LITE");else material.DisableKeyword("SURVEY_LITE");
        EditorUtility.SetDirty(material);finishes[name]=material;
    }
    public static Material Resolve(string key) => finishes.TryGetValue(key,out var material) ? material : Surface.Get(key);
}

internal sealed class ReleaseScanImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if(!assetPath.StartsWith("Assets/Stillwater/Release/Scans/",StringComparison.Ordinal))return;
        var tex=(TextureImporter)assetImporter;
        tex.sRGBTexture=assetPath.EndsWith("_color.jpg",StringComparison.Ordinal);
        tex.textureType=TextureImporterType.Default; tex.alphaIsTransparency=false;
        tex.maxTextureSize=1024;tex.mipmapEnabled=true;tex.anisoLevel=6;tex.filterMode=FilterMode.Trilinear;
        var android=tex.GetPlatformTextureSettings("Android");
        android.overridden=true;android.name="Android";android.maxTextureSize=256;
        android.format=TextureImporterFormat.ASTC_6x6;tex.SetPlatformTextureSettings(android);
    }
}
