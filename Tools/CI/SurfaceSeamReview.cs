using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Editor-only hosted check. Tests indexed triangle interiors in the meshes actually
// loaded by the scene. The overlapping slabs used for walking collision are irrelevant.
public static class SurfaceSeamReview
{
    const float DepthTolerance = .0005f; // 0.5 mm normal to the two rendered faces
    const float EdgeTolerance = .0001f;  // ignore roundoff at perfectly butted edges

    [Serializable] sealed class Finding
    {
        public string first, second, reason;
        public Vector3 point;
        public float overlapSquareMetres, normalSeparationMetres;
    }
    [Serializable] sealed class CheckResult
    {
        public string area;
        public int triangles, visiblePairs, shutteredCapPairs;
        public List<Finding> examples = new List<Finding>();
    }
    [Serializable] sealed class Report
    {
        public string scene, edition;
        public float depthToleranceMetres = DepthTolerance;
        public List<CheckResult> checks = new List<CheckResult>();
        public List<string> walkingFrames = new List<string>();
    }

    sealed class Face
    {
        public Vector3 a, b, c, normal;
        public Bounds bounds;
        public string source;
        public int triangleIndex;
        public Face(Vector3 a, Vector3 b, Vector3 c, string source, int index)
        {
            this.a = a; this.b = b; this.c = c; this.source = source; triangleIndex = index;
            normal = Vector3.Cross(b - a, c - a).normalized;
            bounds = new Bounds(a, Vector3.zero); bounds.Encapsulate(b); bounds.Encapsulate(c);
            bounds.Expand(2 * DepthTolerance);
        }
        public string Label => source + " triangle " + triangleIndex;
    }
    sealed class Area
    {
        public string name;
        public Bounds bounds;
        public Vector3[] eyes;
        public Vector3 tunnelOutward, tunnelMouth;
        public Area(string name, Vector3 center, Vector3 size, params Vector3[] eyes)
        {
            this.name = name; bounds = new Bounds(center, size); this.eyes = eyes;
        }
    }

    public static void InspectCurrentScene(string edition, string outputDirectory,
        Func<Camera, Vector3, Vector3, float, byte[]> photograph = null)
    {
        CheckDetector();
        var scene = SceneManager.GetActiveScene();
        var root = scene.GetRootGameObjects().Select(go => go.transform).Single(t => t.Find("hub/Architecture"));
        var hub = root.Find("hub");
        var room = WorldAuthor.Places.Single(p => p.id == "hub");
        var areas = new List<Area>();
        var floorEyes = new List<Vector3>();
        var photos = new List<KeyValuePair<string, Vector3[]>>();
        Vector3 Eye(Vector3 point) => hub.TransformPoint(point + Vector3.up * 1.62f);
        string[] sides = { "north", "east", "south", "west" };
        for (int i = 0; i < sides.Length; i++) {
            var rotation = Quaternion.Euler(0, 90 * i, 0);
            var forward = rotation * Vector3.forward;
            var right = rotation * Vector3.right;
            var mouth = forward * ((i % 2 == 0 ? room.d : room.w) / 2);
            var eyes = new List<Vector3>();
            foreach (float depth in new[] { -5f, -1.65f, 1.65f, 5f })
            foreach (float lateral in new[] { -3.55f, 0f, 3.55f })
                eyes.Add(Eye(mouth + depth * forward + lateral * right));
            var size = i % 2 == 0 ? new Vector3(10.8f, 6.2f, 4.5f) : new Vector3(4.5f, 6.2f, 10.8f);
            areas.Add(new Area(sides[i] + "-tunnel", hub.TransformPoint(mouth + Vector3.up * 3), size, eyes.ToArray()) {
                tunnelMouth = hub.TransformPoint(mouth), tunnelOutward = hub.TransformDirection(forward) });
            floorEyes.AddRange(eyes);
            if (i == 0) {
                for (int step = 0; step < 5; step++) {
                    // Walk toward the rain exchange from the tunnel; same fixed jamb and floor stay in view.
                    var eye = Eye(mouth + forward * (4.8f - step * .64f) + right * (.7f + step * .15f));
                    var target = hub.TransformPoint(mouth + right * 4.3f + Vector3.up * .88f);
                    photos.Add(Frame("tunnel-" + step.ToString("00"), eye, target));
                }
            }
        }
        foreach (var door in hub.Cast<Transform>().Where(t => t.name.StartsWith("Adventure door to ", StringComparison.Ordinal))) {
            var eyes = new List<Vector3>();
            foreach (float depth in new[] { -4.3f, 2.1f })
            foreach (float lateral in new[] { -3.45f, 0, 3.45f })
                eyes.Add(door.TransformPoint(new Vector3(lateral, 1.62f, depth)));
            areas.Add(new Area(door.name, door.TransformPoint(new Vector3(0, 2.8f, .25f)),
                new Vector3(8.6f, 5.8f, 2), eyes.ToArray()));
        }
        if (areas.Count != 6) throw new InvalidOperationException("Expected four hub mouths and two active adventure doors.");
        foreach (int x in new[] { -1, 1 })
        foreach (int z in new[] { -1, 1 }) {
            var corner = new Vector3(x * room.w / 2, 0, z * room.d / 2);
            var eyes = new[] { Eye(corner - new Vector3(3 * x, 0, 4 * z)),
                Eye(corner - new Vector3(5 * x, 0, 1.2f * z)), Eye(corner - new Vector3(1.2f * x, 0, 5 * z)) };
            areas.Add(new Area("plaza corner " + x + "," + z, hub.TransformPoint(corner + Vector3.up * 1.5f),
                new Vector3(2.2f, 3.2f, 2.2f), eyes));
            floorEyes.AddRange(eyes);
        }
        for (int x = -2; x <= 2; x++)
        for (int z = -2; z <= 2; z++) floorEyes.Add(Eye(new Vector3(x * 9, 0, z * 9)));
        areas.Add(new Area("plaza walking floor", hub.TransformPoint(Vector3.zero),
            new Vector3(room.w + .4f, .10f, room.d + .4f), floorEyes.ToArray()));
        var receiver = hub.Find("Observatory listening bench");
        var clock = root.Find("gravity/Observatory clock station");
        if (receiver || clock) {
            if (!receiver || !clock) throw new InvalidOperationException("Only one observatory instrument is present.");
            areas.Add(new Area("receiver cabinet and buttons", receiver.TransformPoint(new Vector3(0, 1.12f, -.33f)),
                new Vector3(3.6f, 2.22f, 1.3f),
                receiver.TransformPoint(new Vector3(0, 1.6f, -1.6f)),
                receiver.TransformPoint(new Vector3(1.65f, 1.35f, -1.85f)),
                receiver.TransformPoint(new Vector3(-1.65f, 1.8f, -1.9f))));
            areas.Add(new Area("service dial and buttons", clock.TransformPoint(new Vector3(0, 1.36f, -.23f)),
                new Vector3(1.85f, 1.5f, .7f),
                clock.TransformPoint(new Vector3(0, 1.53f, -1.55f)),
                clock.TransformPoint(new Vector3(1.15f, 1.75f, -1.9f)),
                clock.TransformPoint(new Vector3(-1.15f, 1.35f, -1.65f))));
        }
        for (int step = 0; step < 5; step++) {
            var eye = Eye(new Vector3(1.05f + step * .12f, 0, room.d / 2 + 3.6f - step * .37f));
            photos.Add(Frame("floor-" + step.ToString("00"), eye, hub.TransformPoint(new Vector3(.8f, 0, room.d / 2 + .14f))));
        }

        var envelope = areas[0].bounds;
        foreach (var area in areas) {
            envelope.Encapsulate(area.bounds);
            foreach (var eye in area.eyes) envelope.Encapsulate(eye);
        }
        var faces = ReadFaces(scene, envelope);
        var report = new Report { scene = scene.path, edition = edition };
        foreach (var area in areas) report.checks.Add(Inspect(area, faces));
        string directory = Path.Combine(outputDirectory, edition);
        Directory.CreateDirectory(directory);
        if (photograph != null) {
            var camera = new GameObject("Surface walking review camera").AddComponent<Camera>();
            camera.enabled = false; camera.useOcclusionCulling = false; camera.clearFlags = CameraClearFlags.Skybox;
            camera.nearClipPlane = .03f; camera.farClipPlane = 500; camera.allowHDR = false;
            try {
                foreach (var photo in photos) {
                    string file = photo.Key + ".png";
                    File.WriteAllBytes(Path.Combine(directory, file), photograph(camera, photo.Value[0], photo.Value[1], 72));
                    report.walkingFrames.Add(file);
                }
            } finally { UnityEngine.Object.DestroyImmediate(camera.gameObject); }
        }
        File.WriteAllText(Path.Combine(directory, "report.json"), JsonUtility.ToJson(report, true));
        var failures = report.checks.Where(r => r.visiblePairs + r.shutteredCapPairs > 0).ToArray();
        if (failures.Any()) throw new InvalidOperationException(edition + " has overlapping surfaces: " +
            string.Join(", ", failures.Select(f => f.area + " (" + f.visiblePairs + " visible, " + f.shutteredCapPairs + " behind shutter)")) +
            ". First pair: " + failures[0].examples[0].first + " / " + failures[0].examples[0].second +
            " at " + failures[0].examples[0].point.ToString("F4") + ". See report.json.");
        Debug.Log(edition + " Rain Exchange: checked " + areas.Count + " areas for visible surface overlap.");
    }

    static KeyValuePair<string, Vector3[]> Frame(string name, Vector3 eye, Vector3 target) =>
        new KeyValuePair<string, Vector3[]>(name, new[] { eye, target });

    static List<Face> ReadFaces(Scene scene, Bounds envelope)
    {
        var result = new List<Face>();
        foreach (var renderer in UnityEngine.Object.FindObjectsOfType<MeshRenderer>()) {
            if (renderer.gameObject.scene != scene || !renderer.enabled || renderer.forceRenderingOff ||
                renderer.shadowCastingMode == ShadowCastingMode.ShadowsOnly || !renderer.bounds.Intersects(envelope)) continue;
            var filter = renderer.GetComponent<MeshFilter>();
            if (!filter || !filter.sharedMesh) continue;
            var mesh = filter.sharedMesh; var materials = renderer.sharedMaterials;
            var vertices = mesh.vertices; var toWorld = renderer.localToWorldMatrix;
            for (int i = 0; i < vertices.Length; i++) vertices[i] = toWorld.MultiplyPoint3x4(vertices[i]);
            string path = renderer.name;
            for (var parent = renderer.transform.parent; parent; parent = parent.parent) path = parent.name + "/" + path;
            for (int s = 0; s < mesh.subMeshCount; s++) {
                if (s >= materials.Length || !materials[s] || materials[s].renderQueue > (int)RenderQueue.GeometryLast ||
                    mesh.GetTopology(s) != MeshTopology.Triangles) continue;
                var indices = mesh.GetTriangles(s);
                string source = path + " [" + materials[s].name + ", submesh " + s + "]";
                for (int i = 0; i < indices.Length; i += 3) {
                    var face = new Face(vertices[indices[i]], vertices[indices[i + 1]], vertices[indices[i + 2]], source, i / 3);
                    if (face.bounds.Intersects(envelope) && face.normal.sqrMagnitude > .9f) result.Add(face);
                }
            }
        }
        return result;
    }

    static CheckResult Inspect(Area area, List<Face> allFaces)
    {
        var candidates = allFaces.Where(f => f.bounds.Intersects(area.bounds) &&
            area.eyes.Any(e => Vector3.Dot(e - f.a, f.normal) > 0)).ToArray();
        if (candidates.Length < 2) throw new InvalidOperationException("No rendered architecture in " + area.name);
        var sightlineBounds = area.bounds;
        foreach (var eye in area.eyes) sightlineBounds.Encapsulate(eye);
        var occluders = allFaces.Where(f => f.bounds.Intersects(sightlineBounds)).ToArray();
        var result = new CheckResult { area = area.name, triangles = candidates.Length };
        for (int i = 0; i < candidates.Length; i++) {
            var a = candidates[i];
            for (int j = i + 1; j < candidates.Length; j++) {
                var b = candidates[j];
                if (!Overlap(a, b, area.bounds, out var polygon, out float squareMetres, out float distance)) continue;
                var center = Vector3.zero;
                foreach (var p in polygon) center += p;
                center /= polygon.Count;
                // Test the middle and near each corner. A narrow lip generates its own
                // polygon; it cannot disappear between samples in a global grid.
                var probes = new List<Vector3> { center };
                foreach (var p in polygon) probes.Add(Vector3.Lerp(p, center, .15f));
                bool visible = false; var witness = center;
                foreach (var p in probes) {
                    if (!area.eyes.Any(eye => Visible(eye, p, a, b, occluders))) continue;
                    visible = true; witness = p; break;
                }
                // The adventure shutters currently cover the cross-sections of the legacy
                // tunnel linings. Require their actual end caps to meet cleanly as well.
                bool cap = !visible && area.tunnelOutward.sqrMagnitude > .9f &&
                    a.source.Contains("Passage ") && b.source.Contains("Passage ") &&
                    Vector3.Dot(a.normal, area.tunnelOutward) < -.999f &&
                    Mathf.Abs(Vector3.Dot(center - area.tunnelMouth, area.tunnelOutward)) < .20f &&
                    center.y > area.tunnelMouth.y + .05f;
                if (!visible && !cap) continue;
                if (visible) result.visiblePairs++; else result.shutteredCapPairs++;
                if (result.examples.Count < 24) result.examples.Add(new Finding {
                    first = a.Label, second = b.Label, point = witness,
                    reason = visible ? "visible from walking eye" : "tunnel lining end caps behind shutter",
                    overlapSquareMetres = squareMetres, normalSeparationMetres = distance });
            }
        }
        return result;
    }

    static bool Overlap(Face a, Face b, Bounds region, out List<Vector3> polygon, out float area, out float separation)
    {
        polygon = null; area = 0; separation = 0;
        if (!a.bounds.Intersects(b.bounds) || Vector3.Dot(a.normal, b.normal) < .999995f) return false;
        separation = Mathf.Max(Mathf.Abs(Vector3.Dot(b.a - a.a, a.normal)),
            Mathf.Max(Mathf.Abs(Vector3.Dot(b.b - a.a, a.normal)), Mathf.Abs(Vector3.Dot(b.c - a.a, a.normal))));
        if (separation > DepthTolerance) return false;
        polygon = new List<Vector3> { a.a, a.b, a.c };
        polygon = Clip(polygon, b.a, Vector3.Cross(b.normal, b.b - b.a));
        polygon = Clip(polygon, b.b, Vector3.Cross(b.normal, b.c - b.b));
        polygon = Clip(polygon, b.c, Vector3.Cross(b.normal, b.a - b.c));
        foreach (var axis in new[] { Vector3.right, Vector3.up, Vector3.forward }) {
            polygon = Clip(polygon, region.min, axis); polygon = Clip(polygon, region.max, -axis);
        }
        if (polygon.Count < 3) return false;
        float perimeter = 0;
        for (int i = 0; i < polygon.Count; i++) {
            var next = polygon[(i + 1) % polygon.Count];
            perimeter += Vector3.Distance(polygon[i], next);
            area += .5f * Vector3.Dot(Vector3.Cross(polygon[i] - polygon[0], next - polygon[0]), a.normal);
        }
        return area > .000001f && 2 * area > EdgeTolerance * perimeter;
    }

    // Clip convex polygons against a half-space, retaining positive-area interiors.
    static List<Vector3> Clip(List<Vector3> points, Vector3 origin, Vector3 inward)
    {
        if (points.Count < 3) return points;
        var result = new List<Vector3>(points.Count + 1);
        var prev = points[points.Count - 1]; float prevD = Vector3.Dot(prev - origin, inward);
        foreach (var p in points) {
            float d = Vector3.Dot(p - origin, inward);
            if ((prevD < 0) != (d < 0)) result.Add(Vector3.LerpUnclamped(prev, p, prevD / (prevD - d)));
            if (d >= 0) result.Add(p);
            prev = p; prevD = d;
        }
        return result;
    }

    static bool Visible(Vector3 eye, Vector3 point, Face a, Face b, Face[] faces)
    {
        var toPoint = point - eye; var ray = new Ray(eye, toPoint);
        float range = toPoint.magnitude, incidence = -Vector3.Dot(ray.direction, a.normal);
        if (incidence <= .005f) return false;
        // The two candidates can sit up to 0.5 mm apart along their plane normal.
        // Exclude only that depth interval from checking blockers at a grazing angle.
        float before = range - DepthTolerance / incidence - .0001f;
        foreach (var f in faces) {
            if (f == a || f == b) continue;
            float dot = Vector3.Dot(ray.direction, f.normal);
            if (dot >= -.00001f) continue;
            float t = Vector3.Dot(f.a - eye, f.normal) / dot;
            if (t <= .015f || t >= before) continue;
            var hit = ray.GetPoint(t);
            var min = f.bounds.min; var max = f.bounds.max;
            if (hit.x < min.x || hit.x > max.x || hit.y < min.y || hit.y > max.y || hit.z < min.z || hit.z > max.z) continue;
            if (Inside(f.a, f.b, hit, f.normal) && Inside(f.b, f.c, hit, f.normal) &&
                Inside(f.c, f.a, hit, f.normal)) return false;
        }
        return true;
    }
    static bool Inside(Vector3 start, Vector3 end, Vector3 p, Vector3 normal) =>
        Vector3.Dot(Vector3.Cross(end - start, p - start), normal) >= -.000001f;

    // The old lip and two triangles meeting along their diagonal exercise both sides of the gate.
    static void CheckDetector()
    {
        var a = new Face(Vector3.zero, Vector3.forward, Vector3.right, "", 0);
        var diagonal = new Face(Vector3.forward, Vector3.one - Vector3.up, Vector3.right, "", 1);
        var lip = new Face(new Vector3(.2f, .0002f, .2f), new Vector3(.2f, .0002f, .24f),
            new Vector3(.203f, .0002f, .2f), "", 2);
        var region = new Bounds(Vector3.zero, Vector3.one * 5);
        if (Overlap(a, diagonal, region, out _, out _, out _) || !Overlap(a, lip, region, out _, out _, out _))
            throw new InvalidOperationException("Surface overlap detector failed its butt-joint or narrow-lip control.");
    }
}
