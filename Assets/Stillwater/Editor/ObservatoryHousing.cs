using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

internal static class ObservatoryHousing
{
    const string Source = WorldAuthor.Root + "/ThirdParty/ObservatoryInstrument/";

    internal static Transform Place(Transform mount, bool quest)
    {
        string edition = quest ? "Quest" : "PC";
        var mesh = AssetDatabase.LoadAllAssetsAtPath(Source + "Receiver " + edition + ".obj").OfType<Mesh>().Single();
        string path = ObservatoryPrototype.Folder + "/Instrument finish " + edition + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader = Shader.Find(quest ? "Unlit/Texture" : "Liminal uwu/Observatory instrument");
        if (!shader) throw new InvalidOperationException("Missing instrument shader for " + edition);
        if (!material) {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        var color = AssetDatabase.LoadAssetAtPath<Texture2D>(Source + "Enamel.jpg");
        material.mainTexture = color;
        if (!quest) {
            material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Source + "Metal and gloss.png"));
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Source + "Relief.jpg"));
            // Coordinates of the warm strip on the receiver's header, in world space.
            material.SetVector("_Lamp", mount.parent.TransformPoint(new Vector3(0, 2.015f, -.21f)));
        }
        EditorUtility.SetDirty(material);
        var instrument = WorldAuthor.Child(mount, "Orbital telemetry instrument");
        var casing = WorldAuthor.Child(instrument, "Enamel chassis");
        var rotation = Quaternion.Euler(0,180,0);
        float scale = .47f / mesh.bounds.size.x;
        var foot = new Vector3(mesh.bounds.center.x, mesh.bounds.min.y, mesh.bounds.center.z);
        casing.localScale = Vector3.one * scale;
        casing.localRotation = rotation;
        casing.localPosition = -(rotation * foot) * scale;
        casing.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        casing.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
        return instrument;
    }
}
