using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UdonSharpEditor;

// A deserted, oversized waiting hall occupied by rubber-faced toys. Both
// editions use the same local toy count and collision dimensions.
internal static class UwuBallRoom
{
    internal static readonly Vector3 Position = new Vector3(640, 0, 400);
    internal static Bounds Bounds => new Bounds(Position + new Vector3(0, 14, 0), new Vector3(62, 34, 66));
    internal static Transform Install(Transform root, string gen, bool quest)
    {
        Directory.CreateDirectory(gen + "/Architecture");
        Directory.CreateDirectory(gen + "/Finishes");
        var room = WorldAuthor.Child(root, "ballroom"); room.position = Position;
        var mesh = new StationMesh(room, gen + "/Architecture");
        var obstacleCenters = new List<Vector3>(); var obstacleSizes = new List<Vector3>();
        mesh.Box("terrazzo", 0, -.35f, 0, 60, .7f, 64, true);
        // The dark central inlay is above the stone by a definite thickness, not
        // a coplanar decal. All entrances and the centre remain walkable.
        mesh.Box("bath tile", 0, .025f, 0, 43, .05f, 39);
        foreach (int sign in new[] { -1, 1 }) {
            mesh.Box("concrete", sign * 29.2f, 12, 0, 1.6f, 24, 64, true);
            mesh.Box("limestone", sign * 28.3f, 13.6f, 0, .16f, 20.8f, 62.5f);
            mesh.Box("bath tile", sign * 28.19f, 1.65f, 0, .13f, 3.3f, 62.2f);
            mesh.Box("brass", sign * 28.08f, 3.38f, 0, .10f, .12f, 62.2f);
            mesh.Box("black", sign * 28.17f, 20.4f, 0, .10f, 4.3f, 58);
            mesh.Box("shoji", sign * 28.04f, 20.4f, 0, .10f, 3.8f, 56.8f);
            mesh.Box("concrete", sign * 23.9f, 23.8f, 0, 10.6f, .8f, 64, true);
            mesh.Box("metal", sign * 18.55f, 23.33f, 0, .2f, .15f, 63);
            mesh.Box("pink", sign * 18.45f, 23.2f, 0, .08f, .11f, 59);
            mesh.Box("metal", sign * 22.2f, .08f, 0, .12f, .1f, 42);
            for (float z = -24; z <= 24; z += 16) {
                Vector3 center = new Vector3(sign * 27.15f, 11.7f, z);
                mesh.Box("porcelain", center.x, center.y, center.z, 1.4f, 23.4f, 1.4f, true);
                obstacleCenters.Add(center); obstacleSizes.Add(new Vector3(.7f, 11.7f, .7f));
                mesh.Box("brass", sign * 26.4f, 3.42f, z, .09f, .15f, 1.5f);
                mesh.Box("ice", sign * 26.38f, 11.8f, z, .07f, 15.2f, .10f);
                if (!quest) {
                    mesh.Box("metal", sign * 26.37f, 19.2f, z, .1f, .12f, 1.48f);
                    mesh.Box("metal", sign * 26.37f, 4.25f, z, .1f, .12f, 1.48f);
                }
            }
            foreach (float z in new[] { -10f, 10f }) {
                // Wall-side seating keeps a broad centre and both portal aprons
                // unobstructed. One conservative contact box covers each bench.
                mesh.Box("metal", sign * 24.7f, .3f, z, 2.4f, .6f, 6.4f, true);
                mesh.Box("cedar", sign * 24.7f, .65f, z, 2.5f, .16f, 6.5f);
                mesh.Box("porcelain", sign * 24.7f, .87f, z, 2.3f, .28f, 6.15f);
                mesh.Box("porcelain", sign * 25.75f, 1.45f, z, .23f, 1.25f, 6.15f, true);
                obstacleCenters.Add(new Vector3(sign * 24.8f, 1.1f, z));
                obstacleSizes.Add(new Vector3(1.25f, 1.1f, 3.25f));
                foreach (float dz in new[] { -3.1f, 3.1f })
                    mesh.Rod("brass", new Vector3(sign * 24.65f, 1.25f, z + dz), new Vector3(sign * 25.7f, 1.25f, z + dz), .06f, quest ? 5 : 8);
            }
            mesh.Box("concrete", 0, 12, sign * 31.2f, 60, 24, 1.6f, true);
            mesh.Box("limestone", 0, 13.6f, sign * 30.3f, 56.5f, 20.8f, .16f);
            mesh.Box("bath tile", 0, 1.65f, sign * 30.18f, 56.5f, 3.3f, .13f);
            mesh.Box("brass", 0, 3.38f, sign * 30.07f, 56.5f, .12f, .1f);
            mesh.Box("concrete", 0, 23.8f, sign * 28.1f, 37.3f, .8f, 7.8f, true);
            mesh.Box("ice", 0, 23.3f, sign * 24.12f, 36.4f, .08f, .07f);
            mesh.Box("metal", 0, .08f, sign * 20.1f, 44.5f, .1f, .12f);
            for (float x = -15; x <= 15; x += 6) {
                mesh.Box("brass", x, .014f, sign * 24.0f, .7f, .027f, .06f);
                mesh.Box("brass", x, .014f, sign * 25.0f, .7f, .027f, .06f);
            }
        }
        // The 37 x 48 metre opening above the court contains no cross beams.
        // The black hole is the ceiling, with a quiet continuous perimeter glow.
        mesh.Finish();
        Label(room, "Soft terminal title", "THE SOFT TERMINAL", new Vector3(0, 9.4f, 30.02f), 0, .60f, 18);
        Label(room, "Local play plate", "uwu  /  SOFT LANDINGS", new Vector3(-16.5f, 5.2f, 30.02f), 0, .24f, 9);
        Label(room, "Lost time plate", "ALL DEPARTURES  00:00", new Vector3(16.5f, 5.2f, 30.02f), 0, .24f, 9);
        Label(room, "Entrance title", "PLEASE DO NOT WAKE THE TERMINAL", new Vector3(0, 7.2f, -30.02f), 180, .33f, 22);

        var sphere = BallMesh(gen, quest);
        Color[] colors = { new Color(.97f, .55f, .75f), new Color(.48f, .84f, .94f), new Color(.92f, .83f, .55f), new Color(.70f, .61f, .95f) };
        var materials = new Material[colors.Length];
        for (int i = 0; i < materials.Length; i++) materials[i] = BallMaterial(gen, quest, colors[i], i);
        var moving = WorldAuthor.Child(room, "Soft terminal toys");
        var balls = new Transform[24]; var starts = new Vector3[24]; var radii = new float[24];
        for (int i = 0; i < balls.Length; i++) {
            var ball = WorldAuthor.Child(moving, "Bouncy uwu " + (i + 1).ToString("00"));
            // Balls are direct children of a zero-transform group, so their
            // simulated local coordinates are the room's coordinates as well.
            starts[i] = new Vector3(-18 + (i % 6) * 7.2f, 1.5f + ((i * 7) % 9) * .48f, -15 + (i / 6) * 10);
            radii[i] = .68f + (i % 4) * .10f;
            ball.localPosition = starts[i]; ball.localScale = Vector3.one * radii[i];
            ball.localRotation = Quaternion.Euler((i % 3 - 1) * 12, i * 37, 0);
            ball.gameObject.AddComponent<MeshFilter>().sharedMesh = sphere;
            var renderer = ball.gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial = materials[i % materials.Length];
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.reflectionProbeUsage = quest ? ReflectionProbeUsage.Off : ReflectionProbeUsage.BlendProbes;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            // There are deliberately no Pickup, Collider, Rigidbody, network
            // sync or per-ball behaviour components on these soft local toys.
            balls[i] = ball;
        }
        var manager = WorldAuthor.Child(room, "Local bouncy uwu physics").gameObject.AddUdonSharpComponent<UwuBallPit>();
        manager.room = room; manager.balls = balls; manager.radii = radii; manager.starts = starts;
        manager.halfExtent = new Vector3(28.05f, 20, 29.95f);
        manager.obstacleCenters = obstacleCenters.ToArray(); manager.obstacleHalfSizes = obstacleSizes.ToArray();
        UdonSharpEditorUtility.CopyProxyToUdon(manager);
        return room;
    }
    static void Label(Transform room, string name, string text, Vector3 position, float yaw, float size, float width)
    {
        var label = WorldAuthor.Child(room, name); label.localPosition = position; label.localRotation = Quaternion.Euler(0, yaw, 0);
        WorldLettering.Install(label, text, size, new Color(.7f, .9f, .94f)); WorldLettering.Fit(label, width, 1.2f);
    }
    static Material BallMaterial(string gen, bool quest, Color color, int index)
    {
        var shader = Shader.Find("Liminal uwu/Soft terminal ball");
        if (!shader) throw new InvalidOperationException("Soft terminal ball shader is missing.");
        string path = gen + "/Finishes/Soft ball " + index + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
        material.shader = shader; material.SetColor("_Color", color); material.enableInstancing = true;
        if (quest) material.EnableKeyword("BALL_LITE"); else material.DisableKeyword("BALL_LITE");
        EditorUtility.SetDirty(material); return material;
    }
    static Mesh BallMesh(string gen, bool quest)
    {
        int around = quest ? 12 : 24, rows = quest ? 8 : 16;
        var vertices = new Vector3[(around + 1) * (rows + 1)]; var uv = new Vector2[vertices.Length];
        var triangles = new int[around * rows * 6]; int k = 0;
        for (int y = 0; y <= rows; y++) for (int x = 0; x <= around; x++) {
            float longitude = (float)x / around * Mathf.PI * 2, latitude = (float)y / rows * Mathf.PI;
            int i = y * (around + 1) + x;
            vertices[i] = new Vector3(Mathf.Sin(longitude) * Mathf.Sin(latitude), Mathf.Cos(latitude), -Mathf.Cos(longitude) * Mathf.Sin(latitude));
            uv[i] = new Vector2((float)x / around, 1f - (float)y / rows);
            if (x == around || y == rows) continue;
            triangles[k++] = i; triangles[k++] = i + 1; triangles[k++] = i + around + 1;
            triangles[k++] = i + 1; triangles[k++] = i + around + 2; triangles[k++] = i + around + 1;
        }
        var mesh = new Mesh { name = "Soft terminal shared sphere" };
        mesh.vertices = vertices; mesh.normals = vertices; mesh.uv = uv; mesh.triangles = triangles; mesh.RecalculateBounds();
        string path = gen + "/Architecture/Soft terminal shared sphere.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing) { EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh); mesh = existing; EditorUtility.SetDirty(existing); }
        else AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }
}
