using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Author-time glyph baking. Scene labels never retain the built-in font's
// always-visible shader or a reference to its transient, repackable atlas.
internal static class WorldLettering
{
    const string Folder=WorldAuthor.Root+"/Generated/Lettering";
    const int FontSize=72;
    static readonly Dictionary<char,CharacterInfo> Glyphs=new Dictionary<char,CharacterInfo>();
    static Material material;

    internal static void Install(Transform target,string text,float characterSize,Color color)
    {
        Prepare();
        var vertices=new List<Vector3>();var uvs=new List<Vector2>();
        var colors=new List<Color>();var triangles=new List<int>();
        float cursor=0,minY=float.PositiveInfinity,maxY=float.NegativeInfinity;
        foreach(char letter in text) {
            if(!Glyphs.TryGetValue(letter,out var glyph))
                throw new InvalidOperationException("World sign contains an unbaked character: "+letter);
            if(glyph.maxX>glyph.minX && glyph.maxY>glyph.minY) {
                int first=vertices.Count;
                vertices.Add(new Vector3(cursor+glyph.minX,glyph.minY,0));
                vertices.Add(new Vector3(cursor+glyph.minX,glyph.maxY,0));
                vertices.Add(new Vector3(cursor+glyph.maxX,glyph.maxY,0));
                vertices.Add(new Vector3(cursor+glyph.maxX,glyph.minY,0));
                uvs.Add(glyph.uvBottomLeft);uvs.Add(glyph.uvTopLeft);
                uvs.Add(glyph.uvTopRight);uvs.Add(glyph.uvBottomRight);
                for(int i=0;i<4;i++)colors.Add(color);
                // Face local -Z, matching the signs' existing inward-facing poses.
                triangles.Add(first);triangles.Add(first+1);triangles.Add(first+2);
                triangles.Add(first);triangles.Add(first+2);triangles.Add(first+3);
                minY=Mathf.Min(minY,glyph.minY);maxY=Mathf.Max(maxY,glyph.maxY);
            }
            cursor+=glyph.advance;
        }
        if(vertices.Count==0)throw new InvalidOperationException("Cannot bake an empty world sign.");
        Vector3 offset=new Vector3(cursor*.5f,(minY+maxY)*.5f,0);
        for(int i=0;i<vertices.Count;i++)vertices[i]=(vertices[i]-offset)*(characterSize*.1f);
        string key=text+"|"+characterSize.ToString("R",CultureInfo.InvariantCulture)+"|"+
            color.r.ToString("R",CultureInfo.InvariantCulture)+"|"+color.g.ToString("R",CultureInfo.InvariantCulture)+"|"+
            color.b.ToString("R",CultureInfo.InvariantCulture)+"|"+color.a.ToString("R",CultureInfo.InvariantCulture);
        string meshPath=Folder+"/Sign-"+Hash128.Compute(key)+".asset";
        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        bool create=!mesh;if(create)mesh=new Mesh();
        mesh.Clear();mesh.name="Lettering / "+text;
        mesh.SetVertices(vertices);mesh.SetUVs(0,uvs);mesh.SetColors(colors);
        mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
        if(create)AssetDatabase.CreateAsset(mesh,meshPath);else EditorUtility.SetDirty(mesh);
        target.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
        var renderer=target.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
        renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        GameObjectUtility.SetStaticEditorFlags(target.gameObject,StaticEditorFlags.OccludeeStatic|StaticEditorFlags.BatchingStatic);
    }

    internal static void Fit(Transform target,float maxWidth,float maxHeight)
    {
        var size=target.GetComponent<MeshFilter>().sharedMesh.bounds.size;
        float scale=Mathf.Min(1,Mathf.Min(maxWidth/Mathf.Max(size.x,.0001f),maxHeight/Mathf.Max(size.y,.0001f)));
        target.localScale=Vector3.one*scale;
    }

    static void Prepare()
    {
        if(material && Glyphs.Count>0)return;
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        var font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        string alphabet="";for(char letter=' ';letter<='~';letter++)alphabet+=letter;
        font.RequestCharactersInTexture(alphabet,FontSize,FontStyle.Normal);
        Glyphs.Clear();
        foreach(char letter in alphabet) {
            if(!font.GetCharacterInfo(letter,out var glyph,FontSize,FontStyle.Normal))
                throw new InvalidOperationException("Unable to rasterize sign glyph: "+letter);
            Glyphs.Add(letter,glyph);
        }
        // Read the atlas immediately after requesting the entire alphabet. Save a
        // private copy before any UI requests can cause the dynamic font to repack.
        Texture atlas=font.material.mainTexture;
        if(!atlas)throw new InvalidOperationException("The world-sign font has no glyph atlas.");
        var staging=RenderTexture.GetTemporary(atlas.width,atlas.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
        var previous=RenderTexture.active;
        var pixels=new Texture2D(atlas.width,atlas.height,TextureFormat.RGBA32,false,true);
        var baked=new Texture2D(atlas.width,atlas.height,TextureFormat.Alpha8,true,true);
        try {
            Graphics.Blit(atlas,staging);RenderTexture.active=staging;
            pixels.ReadPixels(new Rect(0,0,atlas.width,atlas.height),0,0,false);pixels.Apply(false,false);
            var rgba=pixels.GetPixels32();var alpha=new byte[rgba.Length];
            bool ink=false,clear=false;
            for(int i=0;i<rgba.Length;i++) {alpha[i]=rgba[i].a;ink|=alpha[i]>128;clear|=alpha[i]<8;}
            if(!ink || !clear)throw new InvalidOperationException("The baked font atlas has no readable glyph coverage.");
            baked.SetPixelData(alpha,0);baked.Apply(true,false);
        } finally {
            RenderTexture.active=previous;RenderTexture.ReleaseTemporary(staging);
            UnityEngine.Object.DestroyImmediate(pixels);
        }
        // Alpha-only serialized texture is small on both platforms and is an
        // authored asset, independent of source-image importers or dynamic fonts.
        baked.name="World sign glyphs";baked.wrapMode=TextureWrapMode.Clamp;baked.filterMode=FilterMode.Trilinear;
        string atlasPath=Folder+"/Sign atlas.asset";
        var saved=AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);
        if(saved) {EditorUtility.CopySerialized(baked,saved);UnityEngine.Object.DestroyImmediate(baked);EditorUtility.SetDirty(saved);}
        else AssetDatabase.CreateAsset(baked,atlasPath);
        var shader=Shader.Find("Liminal uwu/World lettering");
        if(!shader)throw new InvalidOperationException("The depth-tested lettering shader is missing.");
        string materialPath=Folder+"/World lettering.mat";
        material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if(!material) {material=new Material(shader);AssetDatabase.CreateAsset(material,materialPath);}
        material.shader=shader;material.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);
        material.enableInstancing=true;EditorUtility.SetDirty(material);
    }
}
