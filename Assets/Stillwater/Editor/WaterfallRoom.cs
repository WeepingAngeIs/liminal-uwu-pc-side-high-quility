using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// A long, roofless waterworks nave. All collision and route dimensions are shared
// between editions; only surface detail and decorative rib counts change.
internal static class WaterfallRoom
{
    internal static readonly Vector3 Position = new Vector3(640, 0, 200);
    internal static Bounds Bounds => new Bounds(Position + new Vector3(0, 47, 0), new Vector3(100, 110, 140));

    internal static Transform Install(Transform root, string gen, bool quest)
    {
        Directory.CreateDirectory(gen + "/Architecture");
        var room = WorldAuthor.Child(root, "waterfall");
        room.position = Position;
        var m = new StationMesh(room, gen + "/Architecture");
        // The basin is several metres below the dry, flush central path. Even a
        // player who jumps the rail lands on solid basin floor, not in a void.
        m.Box("bath tile", 0, -4.35f, 0, 96, .7f, 136, true);
        m.Box("wet", 0, -.35f, 0, 12, .7f, 130, true);
        foreach (int sign in new[] { -1, 1 }) {
            float x = sign * 44;
            m.Box("concrete", x, 46, 0, 8, 100, 136, true);
            m.Box("limestone", sign * 39.8f, 45, 0, .35f, 90, 135);
            m.Box("bath tile", sign * 39.55f, 4.4f, 0, .14f, 16.8f, 134);
            m.Box("metal", sign * 39.32f, 13, 0, .30f, .30f, 134);
            // Scanned masonry, a repeated wet cornice, and deep shadowed spillway
            // mouths give the water a credible architectural source.
            m.Box("black", sign * 38.8f, 86, 0, 1, 3.3f, 125);
            m.Box("concrete", sign * 37.8f, 84, 0, 3.4f, 1.15f, 129);
            m.Box("brass", sign * 35.97f, 84.65f, 0, .12f, .12f, 129);
            m.Box("concrete", sign * 38.6f, 89.1f, 0, 3.1f, 1.2f, 131);
            m.Box("ice", sign * 37.25f, 88.3f, 0, .10f, .12f, 125);
            for (float z = -60; z <= 60; z += quest ? 15 : 7.5f) {
                m.Box("concrete", sign * 39.05f, 43, z, 1, 82, .7f);
                m.Box("brass", sign * 38.48f, 43, z, .07f, 80, .08f);
                if (!quest) {
                    m.Box("black", sign * 39.50f, 31, z + 1.7f, .12f, 7, 1.3f);
                    m.Box("concrete", sign * 39.35f, 27.4f, z + 1.7f, .55f, .3f, 1.65f);
                    m.Box("metal", sign * 39.39f, 31, z + 1.7f, .22f, .1f, 1.3f);
                }
            }
            // No beams, falls, signs or hanging fixtures cross the central sky.
            m.Box("concrete", sign * 29.5f, -1.7f, 0, 13, 4.6f, 130, true);
            m.Box("wet", sign * 29.5f, .64f, 0, 13.1f, .08f, 130);
            for (float z = -49; z <= 49; z += 14) {
                m.Box("metal", sign * 5.93f, .61f, z, .13f, 1.22f, .13f);
                
                m.Box("ice", sign * 5.84f, .18f, z, .06f, .08f, 10);
                m.Box("black", sign * 4.98f, .017f, z, .035f, .025f, 9);
                m.Box("metal", sign * 23.05f, 1.3f, z, .12f, 1.25f, .12f);
            }
            // Continuous collision under the decorative rail, identical on Quest.
            m.Box("brass", sign * 5.93f, 1.25f, 0, .17f, .10f, 106);
            m.Solid(new Vector3(sign * 5.95f, .66f, 0), new Vector3(.18f, 1.32f, 130), Quaternion.identity, "Central bridge parapet");
            m.Solid(new Vector3(sign * 23.05f, 1.25f, 0), new Vector3(.18f, 1.2f, 130), Quaternion.identity, "Maintenance bank parapet");
            m.Box("metal", sign * 23.05f, 1.9f, 0, .14f, .1f, 106);
            // Benches stay in the side banks, leaving the adventure route empty.
            foreach (float z in new[] { -38f, 0, 38 }) {
                m.Box("cedar", sign * 29, 1.15f, z, 1.15f, .16f, 4.2f);
                m.Box("cedar", sign * 29.47f, 1.65f, z, .14f, .9f, 4.2f);
                foreach (float end in new[] { -1.6f, 1.6f }) m.Box("metal", sign * 29, .9f, z + end, .8f, .45f, .12f);
            }
        }
        // A crosswalk at each end opens a slow optional walk along either bank.
        // Raised bank floors are reached by shallow ramps, not an invisible step.
        foreach (float z in new[] { -58f, 58f }) {
            foreach (int sign in new[] { -1, 1 }) {
                m.Box("wet", sign * 14.5f, -.35f, z, 17, .7f, 8, true);
                var tilt = Quaternion.Euler(0, 0, sign * 7.5f);
                m.Box("concrete", new Vector3(sign * 20.5f, .17f, z), new Vector3(5, .3f, 8), tilt, true);
            }
        }
        foreach (float z in new[] { -68f, 68f }) {
            m.Box("concrete", 0, -1, z, 96, 6, 2, true);
            m.Box("metal", 0, 2.2f, z, 96, .35f, 2.3f);
        }
        // Open side-bank gateways in the collision rails, away from arrival/exit.
        // The centre route remains continuously safe without needing to jump.
        m.Finish();
        TrimRailOpenings(room);
        var falling = WaterMaterial(gen, quest, false);
        var pool = WaterMaterial(gen, quest, true);
        foreach (int side in new[] { -1, 1 }) {
            Sheet(room, gen, "Falling water " + side, new Vector3(side * 35.8f, 41.15f, 0),
                Quaternion.Euler(0, side * 90, 0), new Vector2(122, 86.5f), falling, quest ? 24 : 64, quest ? 10 : 32, side);
            Sheet(room, gen, "Still basin " + side, new Vector3(side * 15.1f, -2.1f, 0),
                Quaternion.Euler(90, 0, 0), new Vector2(17.8f, 128), pool, 1, 1, 0);
        }
        Label(room, "Waterworks title", "THE FALLING HOURS", new Vector3(0, 4.6f, -65.5f), 180, .72f, 17);
        Label(room, "Waterworks quiet plate", "THE WATER REMEMBERS THE WAY", new Vector3(-28.8f, 2.9f, -61), 0, .20f, 9);
        return room;
    }

    static void TrimRailOpenings(Transform room)
    {
        var solids = room.Find("Walk surfaces and walls");
        if (!solids) return;
        // Split rails at the crosswalks. Only these named custom solids change;
        // the surrounding structural walls and dry walking surfaces stay intact.
        foreach (var collider in solids.GetComponentsInChildren<BoxCollider>()) {
            if (collider.name != "Central bridge parapet" && collider.name != "Maintenance bank parapet") continue;
            collider.size = new Vector3(collider.size.x, collider.size.y, 106);
        }
    }

    static void Label(Transform room, string name, string text, Vector3 point, float yaw, float size, float width)
    {
        var t = WorldAuthor.Child(room, name); t.localPosition = point; t.localRotation = Quaternion.Euler(0, yaw, 0);
        WorldLettering.Install(t, text, size, new Color(.72f, .88f, .88f)); WorldLettering.Fit(t, width, 1.2f);
    }

    static Material WaterMaterial(string gen, bool quest, bool horizontal)
    {
        string path = gen + "/Finishes/" + (horizontal ? "Waterworks basin" : "Waterworks cascade") + ".mat";
        var shader = Shader.Find("Liminal uwu/Falling hours water");
        if (!shader) throw new InvalidOperationException("Waterfall shader is missing.");
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
        material.shader = shader; material.enableInstancing = true;
        material.SetTexture("_Gradient", AssetDatabase.LoadAssetAtPath<Texture2D>(Surface.Media + "/Water/Water Gradient.png"));
        material.SetTexture("_Noise", AssetDatabase.LoadAssetAtPath<Texture2D>(Surface.Media + "/Surfaces/WeatherField.png"));
        material.SetFloat("_Pool", horizontal ? 1 : 0);
        if (quest) material.EnableKeyword("FALLS_MOBILE"); else material.DisableKeyword("FALLS_MOBILE");
        EditorUtility.SetDirty(material); return material;
    }

    static void Sheet(Transform room, string gen, string name, Vector3 position, Quaternion rotation,
        Vector2 size, Material material, int across, int up, int side)
    {
        var vertices = new Vector3[(across + 1) * (up + 1)];
        var uv = new Vector2[vertices.Length];
        var triangles = new int[across * up * 6]; int index = 0;
        for (int y = 0; y <= up; y++) for (int x = 0; x <= across; x++) {
            int i = y * (across + 1) + x;
            float u = (float)x / across, v = (float)y / up;
            // Displaced curtain catches the animated sheen without a physics mesh.
            float fold = side == 0 ? 0 : .22f * Mathf.Sin(u * 87 + v * 4) + .11f * Mathf.Sin(u * 173 - v * 9);
            vertices[i] = new Vector3((u - .5f) * size.x, (v - .5f) * size.y, fold);
            uv[i] = new Vector2(u, v);
            if (x == across || y == up) continue;
            triangles[index++] = i; triangles[index++] = i + across + 1; triangles[index++] = i + 1;
            triangles[index++] = i + 1; triangles[index++] = i + across + 1; triangles[index++] = i + across + 2;
        }
        var mesh = new Mesh { name = name }; mesh.vertices = vertices; mesh.uv = uv; mesh.triangles = triangles;
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        string path = gen + "/Architecture/" + name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing) { EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh); mesh = existing; EditorUtility.SetDirty(existing); }
        else AssetDatabase.CreateAsset(mesh, path);
        var target = WorldAuthor.Child(room, name); target.localPosition = position; target.localRotation = rotation;
        target.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = target.gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        GameObjectUtility.SetStaticEditorFlags(target.gameObject, StaticEditorFlags.OccludeeStatic);
    }
}
