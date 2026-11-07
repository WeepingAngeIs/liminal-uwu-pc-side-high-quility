using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UdonSharpEditor;
using VRCStation = VRC.SDK3.Components.VRCStation;

// A sheltered late-night lounge beneath a large opening to the aperture.
// Four independent stationary seats never participate in the train logic.
internal static class QuietLounge
{
    internal static readonly Vector3 Position = new Vector3(800, 0, 400);
    internal static Bounds Bounds => new Bounds(Position + new Vector3(0, 14, 0), new Vector3(68, 34, 66));
    const string SharedAssets = WorldAuthor.Root + "/ThirdParty/KenneyStation/";

    internal static Transform Install(Transform root, string gen, bool quest)
    {
        Directory.CreateDirectory(gen + "/Architecture");
        var room = WorldAuthor.Child(root, "lounge"); room.position = Position;
        var m = new StationMesh(room, gen + "/Architecture");
        var tableTops = new LandmarkGeometry();
        m.Box("terrazzo", 0, -.3f, 0, 66, .6f, 64, true);
        foreach (int side in new[] { -1, 1 }) {
            m.Box("limestone", side * 32.5f, 14, 0, 1, 28, 64, true);
            m.Box("cedar", side * 31.95f, 3.9f, 0, .10f, 7.8f, 61);
            m.Box("teal", side * 31.82f, .8f, 0, .14f, 1.6f, 61);
            m.Box("brass", side * 31.69f, 1.66f, 0, .07f, .08f, 61);
            m.Box("pink", side * 31.69f, 8.1f, 0, .08f, .08f, 61);
            m.Box("metal", side * 27, 27.3f, 0, 12, .7f, 64);
            m.Box("cedar", side * 27, 26.9f, 0, 11.7f, .12f, 63);
            m.Box("ice", side * 20.9f, 26.7f, 0, .10f, .09f, 60);
            // Deep window recesses create a sense of a far larger hotel beyond.
            for (int bay = 0; bay < 5; bay++) {
                float z = -25 + bay * 12.5f;
                m.Box("metal", side * 31.6f, 15.3f, z, .23f, 10.6f, 8.6f);
                m.Box("black", side * 31.42f, 15.3f, z, .09f, 10.1f, 8.15f);
                m.Box("glass", side * 31.35f, 15.3f, z, .035f, 9.6f, 7.7f);
                m.Box("metal", side * 31.28f, 15.3f, z, .12f, 9.7f, .12f);
                m.Box("concrete", side * 31.19f, 10.1f, z, .7f, .2f, 8.8f);
            }
            // Low warm light washes both seating banks while the centre stays open.
            m.Box("amber", side * 7.8f, .035f, 0, .07f, .07f, 56);
            m.Box("black", side * 8, .014f, 0, .15f, .028f, 58);
            for (int row = 0; row < 4; row++) {
                float z = -24 + row * 16;
                m.Box("concrete", side * 29.6f, 13.2f, z, .8f, 26.4f, .8f, true);
                m.Box("brass", side * 29.12f, 13, z, .07f, 25.5f, .09f);
                Lamp(m, new Vector3(side * 27.4f, 0, z + 1.8f), quest);
            }
            for (float z = -29; z < 30; z += quest ? 5.8f : 2.9f)
                m.Box("metal", side * 26.7f, 26.62f, z, 11.5f, .20f, .14f);
        }
        foreach (int side in new[] { -1, 1 }) {
            float z = side * 31.5f;
            m.Box("limestone", 0, 14, z, 64, 28, 1, true);
            m.Box("teal", 0, 1.3f, z - side * .56f, 63, 2.6f, .12f);
            m.Box("brass", 0, 2.67f, z - side * .65f, 63, .09f, .09f);
            m.Box("metal", 0, 27.3f, side * 27.5f, 42, .7f, 8);
            m.Box("cedar", 0, 26.9f, side * 27.5f, 41.8f, .12f, 7.8f);
            m.Box("ice", 0, 26.7f, side * 23.45f, 41.5f, .08f, .09f);
            foreach (int wing in new[] { -1, 1 }) {
                m.Box("black", wing * 18, 6.5f, z - side * .59f, 17, 5.5f, .08f);
                m.Box("brass", wing * 18, 9.34f, z - side * .67f, 17.2f, .10f, .09f);
                m.Box("brass", wing * 18, 3.66f, z - side * .67f, 17.2f, .10f, .09f);
            }
        }
        // Four little conversation islands and their perimeter plants never touch
        // the sixteen-metre-wide middle route or either portal's arrival space.
        for (int side = -1; side <= 1; side += 2)
        for (int row = -1; row <= 1; row += 2) {
            var center = new Vector3(side * 18.4f, 0, row * 12.5f);
            ConversationIsland(m, tableTops, center, side, quest);
        }
        m.Finish();
        tableTops.Save(room, gen + "/Architecture", "Lounge octagonal table tops");
        Plants(room, gen, quest);
        Chairs(room, gen, quest);
        Label(room, "THE STILL HOUR", new Vector3(0, 10.7f, -30.9f), 180, .78f, 23, new Color(1, .68f, .80f));
        Label(room, "STAY A LITTLE LONGER", new Vector3(-18, 6.5f, 30.84f), 0, .40f, 14, new Color(.74f, .88f, .96f));
        Label(room, "uwu", new Vector3(18, 6.5f, 30.84f), 0, .75f, 8, new Color(1, .55f, .76f));
        return room;
    }

    static void ConversationIsland(StationMesh m, LandmarkGeometry tops, Vector3 center, int side, bool quest)
    {
        var savedOrigin = m.Origin; var savedFrame = m.Frame;
        m.Origin = center;
        // Stone carpet border and upholstered crescents share a gentle curved plan.
        int segments = quest ? 8 : 16;
        for (int segment = 0; segment < segments; segment++) {
            float angle = (-74 + 148f * (segment + .5f) / segments) * Mathf.Deg2Rad;
            var point = new Vector3(side * 4.6f * Mathf.Cos(angle), 0, 4.6f * Mathf.Sin(angle));
            var rotation = Quaternion.Euler(0, -side * angle * Mathf.Rad2Deg, 0);
            float width = 4.6f * 148 * Mathf.Deg2Rad / segments + .055f;
            m.Box("cedar", point + Vector3.up * .29f, new Vector3(1.18f, .58f, width), rotation, true);
            m.Box("teal", point + Vector3.up * .66f, new Vector3(1.30f, .22f, width - .06f), rotation);
            m.Box("ivory", point + Vector3.up * .79f, new Vector3(1.19f, .09f, width - .13f), rotation);
            Vector3 back = point + new Vector3(side * .61f * Mathf.Cos(angle), 0, .61f * Mathf.Sin(angle));
            m.Box("teal", back + Vector3.up * 1.14f, new Vector3(.20f, 1.15f, width), rotation, true);
            m.Box("cedar", back + Vector3.up * 1.77f, new Vector3(.26f, .12f, width + .03f), rotation);
            if (!quest) m.Box("brass", point + Vector3.up * .08f, new Vector3(1.12f, .045f, width - .10f), rotation);
        }
        // A thick octagonal coffee table with chamfered silhouette and brass stem.
        m.Box("metal", 0, .10f, 0, 1.1f, .2f, 1.1f, true);
        m.Rod("brass", new Vector3(0, .2f, 0), new Vector3(0, .73f, 0), .15f, quest ? 6 : 12);
        tops.frame = Matrix4x4.TRS(center + Vector3.up * .83f, Quaternion.identity, Vector3.one);
        tops.Plate("terrazzo", new[] {
            new Vector3(-.9f, 0, -1.25f), new Vector3(.9f, 0, -1.25f),
            new Vector3(1.25f, 0, -.9f), new Vector3(1.25f, 0, .9f),
            new Vector3(.9f, 0, 1.25f), new Vector3(-.9f, 0, 1.25f),
            new Vector3(-1.25f, 0, .9f), new Vector3(-1.25f, 0, -.9f)
        }, .2f);
        m.Solid(Vector3.up * .83f, new Vector3(2.4f, .2f, 2.4f), Quaternion.identity, "Lounge coffee table");
        m.Box("paper", -.42f, .95f, .24f, .46f, .035f, .62f);
        m.Box("pink", .5f, 1.06f, -.28f, .18f, .2f, .18f);
        m.Origin = savedOrigin; m.Frame = savedFrame;
    }

    static void Lamp(StationMesh m, Vector3 p, bool quest)
    {
        m.Box("metal", p + new Vector3(0, .08f, 0), new Vector3(.65f, .16f, .65f), Quaternion.identity, true);
        m.Rod("brass", p + Vector3.up * .16f, p + Vector3.up * 3.5f, .035f, quest ? 5 : 8);
        m.Box("amber", p + Vector3.up * 3.45f, new Vector3(.67f, .22f, .67f), Quaternion.identity);
        m.Box("paper", p + Vector3.up * 3.72f, new Vector3(.9f, .52f, .9f), Quaternion.identity);
        m.Box("brass", p + Vector3.up * 4, new Vector3(.93f, .07f, .93f), Quaternion.identity);
    }

    static void Plants(Transform room, string gen, bool quest)
    {
        var foliage = new LandmarkGeometry();
        var collision = WorldAuthor.Child(room, "Lounge planter collision");
        int number = 0;
        foreach (int side in new[] { -1, 1 }) foreach (float z in new[] { -26f, 0f, 26f }) {
            Vector3 p = new Vector3(side * 26, 0, z);
            foliage.frame = Matrix4x4.TRS(p, Quaternion.identity, Vector3.one);
            foliage.Tube("terrazzo", Vector3.zero, Vector3.up * 1.35f, .62f, .95f, quest ? 8 : 14);
            foliage.Tube("black", Vector3.up * 1.34f, Vector3.up * 1.38f, .88f, .88f, quest ? 8 : 14);
            foliage.Tube("bark", Vector3.up * 1.35f, new Vector3(.12f, 4.5f, .12f), .1f, .05f, 6);
            for (int crown = 0; crown < (quest ? 3 : 5); crown++) {
                float angle = crown * 2.4f;
                Vector3 center = new Vector3(Mathf.Sin(angle) * .85f, 2.9f + crown * .36f, Mathf.Cos(angle) * .6f);
                foliage.Beam("bark", new Vector3(0, 2.2f + crown * .26f, 0), center, .055f, .055f);
                foliage.Crown("leaf", center, new Vector3(.72f, .8f, .59f), 430 + number * 7 + crown, quest ? 6 : 9, quest ? 3 : 5);
            }
            var solid = WorldAuthor.Child(collision, "Planter " + number++); solid.localPosition = p + Vector3.up * .7f;
            solid.gameObject.AddComponent<BoxCollider>().size = new Vector3(1.9f, 1.4f, 1.9f);
        }
        foliage.Save(room, gen + "/Architecture", "Lounge planted palms");
    }

    static void Chairs(Transform room, string gen, bool quest)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(SharedAssets + "chair-armrest-headrest.fbx");
        if (!asset) throw new InvalidOperationException("The licensed CC0 lounge chair is missing.");
        var parts = new List<CombineInstance>(); int index = 0;
        foreach (int side in new[] { -1, 1 }) foreach (int row in new[] { -1, 1 }) {
            // A relaxed chair at the open end of each banquette faces its table.
            Vector3 p = new Vector3(side * 15.2f, 0, row * 12.5f);
            Quaternion facing = Quaternion.Euler(0, side * 90, 0);
            var temporary = UnityEngine.Object.Instantiate(asset); temporary.hideFlags = HideFlags.HideAndDontSave;
            temporary.transform.SetParent(room, false); temporary.transform.localPosition = p; temporary.transform.localRotation = facing;
            try {
                var renderers = temporary.GetComponentsInChildren<MeshRenderer>();
                var bounds = renderers[0].bounds; foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                temporary.transform.localScale *= 1.35f / bounds.size.y;
                bounds = renderers[0].bounds; foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                temporary.transform.position += room.TransformPoint(p) - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                foreach (var filter in temporary.GetComponentsInChildren<MeshFilter>())
                for (int submesh = 0; submesh < filter.sharedMesh.subMeshCount; submesh++)
                    parts.Add(new CombineInstance { mesh = filter.sharedMesh, subMeshIndex = submesh, transform = room.worldToLocalMatrix * filter.transform.localToWorldMatrix });
            } finally { UnityEngine.Object.DestroyImmediate(temporary); }
            var stationRoot = WorldAuthor.Child(room, "Lounge seat " + ++index);
            stationRoot.localPosition = p; stationRoot.localRotation = facing;
            var chairSolid = WorldAuthor.Child(stationRoot, "Lounge chair solid");
            var chairBox = chairSolid.gameObject.AddComponent<BoxCollider>();
            chairBox.center = new Vector3(0, .34f, 0); chairBox.size = new Vector3(.85f, .68f, .75f);
            var collider = stationRoot.gameObject.AddComponent<BoxCollider>();
            collider.center = new Vector3(0, .86f, 0); collider.size = new Vector3(1.1f, 1.5f, 1.1f); collider.isTrigger = true;
            var seat = stationRoot.gameObject.AddComponent<VRCStation>();
            seat.seated = true; seat.disableStationExit = false; seat.canUseStationFromStation = false;
            seat.PlayerMobility = VRC.SDKBase.VRCStation.Mobility.Immobilize;
            var enter = WorldAuthor.Child(stationRoot, "Lounge seated pivot"); enter.localPosition = new Vector3(0, .59f, .02f);
            var exit = WorldAuthor.Child(stationRoot, "Lounge clear exit"); exit.localPosition = new Vector3(.95f, .12f, 1.2f);
            seat.stationEnterPlayerLocation = enter; seat.stationExitPlayerLocation = exit;
            var use = stationRoot.gameObject.AddUdonSharpComponent<LoungeSeat>(); use.seat = seat;
            UdonSharpEditorUtility.CopyProxyToUdon(use);
            var vm = UdonSharpEditorUtility.GetBackingUdonBehaviour(use); vm.interactText = "Sit and unwind"; vm.proximity = 2.2f;
            EditorUtility.SetDirty(vm);
        }
        var mesh = new Mesh { name = "Four restored lounge chairs" }; mesh.CombineMeshes(parts.ToArray(), true, true); mesh.RecalculateBounds();
        string path = gen + "/Architecture/Lounge restored chairs.asset";
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (old) { EditorUtility.CopySerialized(mesh, old); UnityEngine.Object.DestroyImmediate(mesh); mesh = old; EditorUtility.SetDirty(old); }
        else AssetDatabase.CreateAsset(mesh, path);
        string materialPath = gen + "/Finishes/Lounge chair upholstery.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (!mat) { mat = new Material(Surface.Get("metal")); AssetDatabase.CreateAsset(mat, materialPath); }
        mat.CopyPropertiesFromMaterial(Surface.Get("metal")); mat.EnableKeyword("SW_PRINT");
        mat.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture>(SharedAssets + "colormap.png"));
        mat.SetColor("_Color", new Color(.73f, .84f, .85f)); mat.SetFloat("_Sheen", .14f); EditorUtility.SetDirty(mat);
        var model = WorldAuthor.Child(room, "Restored lounge chairs"); model.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = model.gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial = mat; renderer.shadowCastingMode = ShadowCastingMode.Off;
        GameObjectUtility.SetStaticEditorFlags(model.gameObject, StaticEditorFlags.OccludeeStatic);
    }

    static void Label(Transform room, string text, Vector3 position, float yaw, float size, float width, Color color)
    {
        var label = WorldAuthor.Child(room, text); label.localPosition = position; label.localRotation = Quaternion.Euler(0, yaw, 0);
        WorldLettering.Install(label, text, size, color); WorldLettering.Fit(label, width, 1.7f);
    }
}
