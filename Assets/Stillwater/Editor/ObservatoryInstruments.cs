using System;
using System.Collections.Generic;
using System.IO;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using VRCStation = VRC.SDK3.Components.VRCStation;

// Installs into a copy of each finished scene. All new mesh assets are private
// to that edition of the prototype; the existing district finishes are read only.
public static class ObservatoryInstruments
{
    public sealed class Parts
    {
        public Transform bench, clock, pen, feedWheel, clockHand, serviceHand, pendulum, housingMount;
        public GameObject secondSpool, spoolButton, clockButton;
        public Text readout, clockDisplay;
    }

    static readonly Color Paper = new Color32(255, 226, 175, 255);
    static readonly Color Mint = new Color32(169, 247, 220, 255);
    static Mesh cube, cylinder;

    public static Parts Build(Transform hub, Transform gravity, bool quest)
    {
        var parts = new Parts();
        string edition = quest ? "Quest" : "PC";
        string folder = ObservatoryPrototype.Folder + "/" + edition;
        if (!AssetDatabase.IsValidFolder(folder)) {
            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
        }
        if (!cube) cube = PrimitiveMesh(PrimitiveType.Cube);
        if (!cylinder) cylinder = PrimitiveMesh(PrimitiveType.Cylinder);
        Receiver(parts, hub, new Metalwork(edition, "hub", folder), quest);
        Clocks(parts, gravity, new Metalwork(edition, "gravity", folder), quest);
        return parts;
    }

    static Mesh PrimitiveMesh(PrimitiveType type)
    {
        var temporary = GameObject.CreatePrimitive(type);
        var mesh = temporary.GetComponent<MeshFilter>().sharedMesh;
        UnityEngine.Object.DestroyImmediate(temporary);
        return mesh;
    }

    static Transform At(Transform parent, string name, float x, float y, float z)
    {
        var t = WorldAuthor.Child(parent, name);
        t.localPosition = new Vector3(x, y, z);
        return t;
    }

    static void Receiver(Parts p, Transform district, Metalwork m, bool quest)
    {
        // The front of the existing kiosk overlaps a decorative tower. Attach
        // the receiver to the opposite face, clear of its footprint and the road.
        var desk = At(district, "Observatory listening bench", -15.1f, 0, 8.8f);
        desk.localRotation = Quaternion.Euler(0, 180, 0);
        p.bench = desk;
        m.Box("teal", 0, .46f, -.12f, 3.26f, .87f, .59f);
        m.Box("black", 0, .112f, -.23f, 3.12f, .20f, .58f);
        m.Box("metal", 0, .928f, -.25f, 3.48f, .11f, .86f);
        m.Box("brass", 0, .925f, -.69f, 3.44f, .034f, .025f);
        BoxCollision(desk, 0, .48f, -.22f, 3.44f, .96f, .84f);
        // A painted header and a warm strip under the existing shelter.
        m.Box("teal", 0, 2.32f, .014f, 3.39f, .49f, .21f);
        m.Box("brass", 0, 2.54f, -.105f, 3.34f, .027f, .028f);
        m.Box("black", 0, 2.063f, -.08f, 3.46f, .097f, .29f);
        m.Box("amber", 0, 2.008f, -.219f, 3.00f, .027f, .033f);
        m.Box("teal", .86f, 1.53f, -.18f, 1.06f, .91f, .38f);
        m.Box("brass", .86f, 1.63f, -.384f, .94f, .52f, .035f);
        m.Box("black", .86f, 1.63f, -.407f, .91f, .49f, .034f);
        // Strip chart, ink grid, feed rollers and the replaceable spool socket.
        m.Box("metal", -.29f, 1.43f, -.19f, 1.37f, .75f, .41f);
        m.Box("brass", -.29f, 1.43f, -.409f, 1.28f, .67f, .031f);
        m.Box("black", -.29f, 1.43f, -.432f, 1.24f, .63f, .03f);
        m.PrintedBox(new Vector3(-.31f, 1.43f, -.461f), new Vector3(.73f, .39f, .022f), 8, 272);
        m.Disc("brass", .197f, 1.435f, -.46f, .105f, .035f);
        m.Disc("black", .197f, 1.435f, -.485f, .079f, .015f);
        m.Box("brass", -.31f, 1.215f, -.487f, .75f, .024f, .025f);
        m.Box("black", 1.02f, 1.225f, -.416f, .60f, .19f, .08f);
        m.Box("teal", 1.02f, 1.228f, -.468f, .51f, .135f, .036f);
        m.Box("amber", .803f, 1.228f, -.49f, .024f, .085f, .014f);
        if (!quest) {
            for (int i = 0; i < 8; i++)
                m.Box("black", -.66f + i * .076f, .68f, -.426f, .045f, .16f, .013f);
            foreach (float x in new[] {-.885f, .295f})
                foreach (float y in new[] {1.15f, 1.71f})
                    m.Disc("brass", x, y, -.467f, .018f, .012f);
        }
        m.Bake(desk, "Exchange receiver cabinet");
        p.housingMount = At(desk, "Vintage meter mount", -1.28f, .984f, -.26f);
        p.feedWheel = Spool(m, desk, "Ink feed wheel", -.818f, .095f, quest);
        p.secondSpool = Spool(m, desk, "Second recording spool", .197f, .088f, quest).gameObject;
        p.secondSpool.SetActive(false);
        p.pen = At(desk, "Recording stylus pivot", -.29f, 1.216f, -.506f);
        m.Box("black", 0, .10f, 0, .021f, .20f, .021f);
        m.Box("brass", 0, .199f, -.011f, .017f, .083f, .015f);
        m.Disc("brass", 0, 0, 0, .042f, .043f);
        m.Bake(p.pen, "Strip chart stylus");
        p.pendulum = At(desk, "Recovered pendulum", -1.29f, 1.98f, -.31f);
        m.Box("brass", 0, -.121f, 0, .018f, .247f, .023f);
        m.Disc("brass", 0, -.287f, -.006f, .077f, .039f);
        m.Disc("black", 0, -.287f, -.031f, .050f, .013f);
        m.Box("amber", 0, -.286f, -.041f, .015f, .066f, .009f);
        m.Bake(p.pendulum, "Receiver pendulum");
        p.pendulum.gameObject.SetActive(false);
        p.spoolButton = Interactable(desk, "Fit second spool", 1.02f, 1.225f, -.503f, .62f, .23f, .09f);

        var panel = Panel(desk, "Receiver lettering", 3.6f, 2.6f);
        Label(panel, "Listening post name", "OBSERVATORY   /   LISTENING POST", 0, 2.335f, -.102f, 3.1f, .34f, .135f, Paper);
        p.readout = Label(panel, "Receiver display", "MY RECEIVER\n0 READINGS\n2 PER MINUTE", .86f, 1.63f, -.43f, .85f, .405f, .072f, Mint);
        Label(panel, "Spool button legend", "FIT SPOOL", 1.053f, 1.228f, -.491f, .36f, .115f, .061f, Paper);
        // A small two-place banquette sits at the side. Its long axis runs
        // toward the kiosk so spectators never block the standing controls.
        var seat = At(desk, "Side banquette", 2.11f, 0, -1.05f);
        seat.localRotation = Quaternion.Euler(0, -72f, 0);
        m.Box("metal", 0, .27f, 0, 1.35f, .39f, .37f);
        for (int i = 0; i < 4; i++)
            m.Box("bark", 0, .50f, -.21f + i * .14f, 1.57f, .087f, .124f);
        m.Box("teal", 0, .78f, -.318f, 1.55f, .34f, .093f);
        m.Box("brass", 0, .956f, -.318f, 1.576f, .025f, .114f);
        m.Bake(seat, "Observatory side banquette");
        BoxCollision(seat, 0, .26f, 0, 1.57f, .52f, .56f);
        Seat(seat, -.39f); Seat(seat, .39f);
    }

    static Transform Spool(Metalwork m, Transform parent, string name, float x, float r, bool quest)
    {
        var spool = At(parent, name, x, 1.435f, -.51f);
        m.Disc("brass", 0, 0, .007f, r, .026f);
        m.Disc("black", 0, 0, -.015f, r * .80f, .028f);
        int spokes = quest ? 3 : 5;
        for (int i = 0; i < spokes; i++) {
            var end = Quaternion.Euler(0, 0, i * 360f / spokes) * (Vector3.up * r * .81f);
            var start = end.normalized * (r * .16f);
            m.Beam("paper", new Vector3(start.x, start.y, -.036f), new Vector3(end.x, end.y, -.036f), r * .12f, .008f);
        }
        m.Disc("brass", 0, 0, -.045f, r * .22f, .016f);
        m.Bake(spool, name);
        return spool;
    }

    static void Clocks(Parts p, Transform gravity, Metalwork m, bool quest)
    {
        var station = At(gravity, "Observatory clock station", 8, 0, -13);
        m.Box("metal", 0, 20.54f, .06f, 2.28f, .42f, .48f);
        foreach (float x in new[] {-.62f, .62f}) {
            m.Box("brass", x, 19.07f, .06f, .073f, 2.62f, .073f);
            m.Box("black", x, 17.6f, .06f, .14f, .40f, .16f);
        }
        m.Box("metal", 0, 17.46f, .07f, 1.61f, .20f, .29f);
        m.Box("teal", 0, .67f, .12f, 1.66f, 1.27f, .58f);
        m.Box("metal", 0, .10f, .12f, 1.77f, .20f, .66f);
        m.Box("teal", 0, 1.425f, .07f, 1.73f, .96f, .45f);
        m.Box("brass", 0, 1.426f, -.168f, 1.64f, .84f, .039f);
        m.Box("black", 0, 1.426f, -.197f, 1.59f, .79f, .035f);
        m.PrintedBox(new Vector3(.335f, 1.215f, -.231f), new Vector3(.65f, .15f, .058f), 320, 432);
        m.Box("brass", 0, 1.937f, .08f, 1.78f, .06f, .48f);
        BoxCollision(station, 0, .94f, .075f, 1.73f, 1.88f, .58f);
        m.Bake(station, "Archive clock fittings");
        // The clock is mounted upside down in the hanging reading room. Its
        // floor repeater has the same register and hand, visible seated.
        p.clock = At(station, "Inverted master clock", 0, 16, 0);
        p.clock.localRotation = Quaternion.Euler(0, 0, 180);
        p.clockHand = ClockFace(m, p.clock, "Master clock", 1.51f, quest, "paper");
        var repeater = At(station, "Eye level service dial", -.475f, 1.46f, -.226f);
        repeater.localRotation = Quaternion.Euler(0, 0, 180);
        p.serviceHand = ClockFace(m, repeater, "Service repeater", .253f, quest, "amber");
        p.clockButton = Interactable(station, "Record the missing beat", .335f, 1.215f, -.283f, .73f, .25f, .09f);
        var panel = Panel(station, "Service dial lettering", 1.8f, 2.05f);
        p.clockDisplay = Label(panel, "Service display", "CEILING WARD\nCLOCK SERVICE\nLISTEN FOR THE GAP", .328f, 1.571f, -.225f, .80f, .334f, .057f, Mint);
    }

    static Transform ClockFace(Metalwork m, Transform face, string name, float r, bool quest, string faceFinish)
    {
        // A small register still needs room between the enamel, ticks and turning
        // hands. Scaling its depth with the dial radius left the hands only a
        // few millimetres apart when viewed across the gallery.
        float depth = Mathf.Max(r, .76f);
        m.Disc("metal", 0, 0, 0, r, .18f * depth);
        m.Disc("brass", 0, 0, -.095f * depth, .978f * r, .052f * depth);
        m.Disc(faceFinish, 0, 0, -.132f * depth, .916f * r, .033f * depth);
        int stride = quest ? 2 : 1;
        for (int tick = 0; tick < 60; tick++) {
            bool major = tick % 5 == 0;
            if (!major && tick % stride != 0) continue;
            var dir = new Vector3(Mathf.Sin(tick * Mathf.PI / 30f), Mathf.Cos(tick * Mathf.PI / 30f), 0);
            var outer = dir * (.87f * r); outer.z = -.158f * depth;
            var inner = dir * ((major ? .68f : .81f) * r); inner.z = outer.z;
            m.Beam("black", inner, outer, r * (major ? .038f : .014f), .010f * depth);
        }
        // A double bar at twelve and a fixed short hour hand identify the
        // dial's orientation even from the opposite end of the gallery.
        foreach (float x in new[] {-.065f, .065f})
            m.Box("black", x * r, .585f * r, -.161f * depth, .038f * r, .119f * r, .012f * depth);
        m.Beam("teal", new Vector3(0, 0, -.176f * depth), new Vector3(-.34f * r, -.20f * r, -.176f * depth), .080f * r, .022f * depth);
        m.Bake(face, name + " register");
        var needle = At(face, name + " hand", 0, 0, -.200f * depth);
        m.Box("black", 0, .284f * r, 0, .047f * r, .825f * r, .017f * depth);
        m.Disc("brass", 0, 0, -.018f * depth, .071f * r, .032f * depth);
        m.Bake(needle, name + " hand");
        return needle;
    }

    static void BoxCollision(Transform root, float x, float y, float z, float w, float h, float d)
    {
        var col = root.gameObject.AddComponent<BoxCollider>();
        col.center = new Vector3(x, y, z); col.size = new Vector3(w, h, d);
    }

    static GameObject Interactable(Transform parent, string name, float x, float y, float z, float w, float h, float d)
    {
        var target = At(parent, name, x, y, z);
        var col = target.gameObject.AddComponent<BoxCollider>();
        col.size = new Vector3(w,h,d); col.isTrigger = true;
        return target.gameObject;
    }

    static void Seat(Transform bench, float x)
    {
        var root = At(bench, x < 0 ? "Window seat" : "Aisle seat", x, 0, .01f);
        var col = root.gameObject.AddComponent<BoxCollider>();
        col.isTrigger = true; col.center = Vector3.up * .74f; col.size = new Vector3(.70f, 1.16f, .70f);
        var station = root.gameObject.AddComponent<VRCStation>();
        station.seated = true;
        station.PlayerMobility = VRC.SDKBase.VRCStation.Mobility.Immobilize;
        station.stationEnterPlayerLocation = At(root, "Seat entry", 0, .55f, 0);
        station.stationExitPlayerLocation = At(root, "Seat exit", 0, .12f, 1.01f);
        var use = root.gameObject.AddUdonSharpComponent<LoungeSeat>();
        use.seat = station;
        UdonSharpEditorUtility.CopyProxyToUdon(use);
        var backend = UdonSharpEditorUtility.GetBackingUdonBehaviour(use);
        backend.interactText = "Sit at the listening post"; backend.proximity = 2f;
        EditorUtility.SetDirty(backend);
    }

    static RectTransform Panel(Transform parent, string name, float width, float height)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas));
        go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.pivot = new Vector2(.5f, 0f);
        rect.sizeDelta = new Vector2(width, height) * 1000f;
        rect.localPosition = Vector3.zero;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one * .001f;
        return rect;
    }

    static Text Label(RectTransform panel, string name, string message, float x, float y, float z,
                      float width, float height, float size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        var rect = (RectTransform)go.transform;
        rect.SetParent(panel, false); rect.sizeDelta = new Vector2(width, height) * 1000f;
        rect.localPosition = new Vector3(x, y, z) * 1000f;
        var text = go.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = Mathf.RoundToInt(size * 1000f); text.alignment = TextAnchor.MiddleCenter;
        text.resizeTextForBestFit = true; text.resizeTextMinSize = text.fontSize / 2; text.resizeTextMaxSize = text.fontSize;
        text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false; text.supportRichText = false; text.color = color; text.text = message;
        return text;
    }

    // A whole stationary mechanism is batched into one mesh with one submesh
    // per district finish. Only parts that the player can animate get a renderer.
    sealed class Metalwork
    {
        readonly string finishes, folder, district;
        readonly Dictionary<string, List<CombineInstance>> pieces = new Dictionary<string, List<CombineInstance>>();
        readonly List<Mesh> printedMeshes = new List<Mesh>();

        public Metalwork(string edition, string district, string folder)
        {
            finishes = WorldAuthor.Root + "/Generated/" + edition + "/Finishes/";
            this.district = district; this.folder = folder;
        }

        void Piece(string finish, Mesh mesh, Vector3 p, Quaternion rot, Vector3 scale)
        {
            if (!pieces.TryGetValue(finish, out var group)) { group = new List<CombineInstance>(); pieces.Add(finish, group); }
            group.Add(new CombineInstance { mesh = mesh, transform = Matrix4x4.TRS(p, rot, scale) });
        }

        public void Box(string finish, float x, float y, float z, float w, float h, float d)
        {
            Piece(finish, cube, new Vector3(x,y,z), Quaternion.identity, new Vector3(w,h,d));
        }

        // Ink and enamel share the front two triangles. A mipmapped image keeps
        // crossings, lettering joints and hairlines stable at a distance.
        public void PrintedBox(Vector3 center, Vector3 size, int topPixel, int bottomPixel)
        {
            var plate = UnityEngine.Object.Instantiate(cube);
            var vertices = plate.vertices;
            var normals = plate.normals;
            var uv = new Vector2[vertices.Length];
            for (int i = 0; i < uv.Length; i++) {
                var p = vertices[i];
                uv[i] = normals[i].z < -.99f
                    ? new Vector2(Mathf.Lerp(8, 504, p.x + .5f), 512 - Mathf.Lerp(bottomPixel, topPixel, p.y + .5f)) / 512f
                    : new Vector2(10, 512 - topPixel - 2) / 512f;
            }
            plate.uv = uv;
            printedMeshes.Add(plate);
            Piece("faceplate", plate, center, Quaternion.identity, size);
        }

        Material FaceplateMaterial()
        {
            bool backlit = district == "gravity";
            var original = AssetDatabase.LoadAssetAtPath<Material>(finishes + district + " lit " + (backlit ? "amber" : "paper") + ".mat");
            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(WorldAuthor.Root + "/Media/Prints/Observatory instrument faces.png");
            if (!original || !atlas) throw new InvalidOperationException("Missing Observatory faceplate assets");
            string path = folder + "/" + district + " faceplate.mat";
            var result = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!result) {
                result = new Material(original);
                AssetDatabase.CreateAsset(result, path);
            } else result.CopyPropertiesFromMaterial(original);
            result.mainTexture = atlas;
            result.color = Color.white;
            result.EnableKeyword("SW_PRINT");
            EditorUtility.SetDirty(result);
            return result;
        }

        public void Beam(string finish, Vector3 a, Vector3 b, float w, float d)
        {
            var axis = b-a;
            Piece(finish, cube, (a+b)*.5f, Quaternion.Euler(0,0, Mathf.Atan2(-axis.x,axis.y)*Mathf.Rad2Deg), new Vector3(w,axis.magnitude,d));
        }

        public void Disc(string finish, float x, float y, float z, float r, float depth)
        {
            Piece(finish, cylinder, new Vector3(x,y,z), Quaternion.Euler(90,0,0), new Vector3(r*2,depth*.5f,r*2));
        }

        public void Bake(Transform target, string name)
        {
            var merged = new List<CombineInstance>();
            var materials = new List<Material>();
            var mesh = new Mesh { name = name };
            foreach (var entry in pieces) {
                string path = finishes + district + " lit " + entry.Key + ".mat";
                var mat = entry.Key == "faceplate" ? FaceplateMaterial() : AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!mat) throw new InvalidOperationException("Missing district instrument finish " + path);
                materials.Add(mat);
                var sub = new Mesh();
                sub.CombineMeshes(entry.Value.ToArray(), true, true);
                merged.Add(new CombineInstance { mesh = sub });
            }
            mesh.CombineMeshes(merged.ToArray(), false, false);
            foreach (var part in merged) UnityEngine.Object.DestroyImmediate(part.mesh);
            foreach (var temporary in printedMeshes) UnityEngine.Object.DestroyImmediate(temporary);
            printedMeshes.Clear();
            pieces.Clear();
            string file = folder + "/" + name + ".asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(file);
            if (saved) {
                EditorUtility.CopySerialized(mesh, saved); EditorUtility.SetDirty(saved);
                UnityEngine.Object.DestroyImmediate(mesh);
            } else {
                saved = mesh; AssetDatabase.CreateAsset(saved, file);
            }
            target.gameObject.AddComponent<MeshFilter>().sharedMesh = saved;
            var rend = target.gameObject.AddComponent<MeshRenderer>();
            rend.sharedMaterials = materials.ToArray(); rend.shadowCastingMode = ShadowCastingMode.Off;
        }
    }
}
