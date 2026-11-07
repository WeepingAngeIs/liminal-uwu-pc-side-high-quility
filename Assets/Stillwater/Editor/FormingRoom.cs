using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UdonSharpEditor;

// An abandoned atrium whose finely divided structural bays remember the visitor.
// Every animated piece is decorative and excluded from the occlusion bake.
internal static class FormingRoom
{
    internal static readonly Vector3 Position = new Vector3(800, 0, 200);
    internal static Bounds Bounds => new Bounds(Position + new Vector3(0, 18, 0), new Vector3(78, 42, 78));

    internal static Transform Install(Transform root, string gen, bool quest)
    {
        Directory.CreateDirectory(gen + "/Architecture");
        var room = WorldAuthor.Child(root, "formation"); room.position = Position;
        var baseMesh = new StationMesh(room, gen + "/Architecture");
        baseMesh.Box("concrete", 0, -.4f, 0, 64, .8f, 70, true);
        // A raised structural edge is intentionally outside the six-metre promenade.
        foreach (int side in new[] { -1, 1 }) {
            baseMesh.Box("limestone", side * 31.3f, .65f, 0, 1.4f, 1.3f, 70, true);
            baseMesh.Box("brass", side * 30.55f, 1.34f, 0, .12f, .12f, 68);
            baseMesh.Box("black", side * 10.8f, .015f, 0, .11f, .03f, 67);
            baseMesh.Box("ice", side * 11f, .04f, 0, .06f, .04f, 67);
            for (int row = 0; row < 10; row++) {
                float z = -30 + row * 6.6f;
                baseMesh.Box("terrazzo", side * 20.2f, .045f, z, 16.8f, .09f, 6.25f);
                baseMesh.Box("metal", side * 29.9f, .14f, z, .48f, .28f, 1.8f);
            }
        }
        foreach (float z in new[] { -34.5f, 34.5f }) {
            baseMesh.Box("concrete", 0, 1.3f, z, 64, 2.6f, 1, true);
            baseMesh.Box("brass", 0, 2.64f, z, 63, .08f, 1.1f);
        }
        // These practicals remain anchored while the surrounding building moves.
        foreach (int side in new[] { -1, 1 }) foreach (float z in new[] { -27f, 27f }) {
            baseMesh.Box("metal", side * 28.7f, 2.65f, z, .18f, 5.3f, .18f, true);
            baseMesh.Box("metal", side * 28.7f, 5.4f, z, 1.7f, .2f, .7f);
            baseMesh.Box(side < 0 ? "ice" : "pink", side * 28.7f, 5.27f, z, 1.5f, .08f, .5f);
        }
        // The door and arrival stay on this uninterrupted, permanently solid floor.
        baseMesh.Box("terrazzo", 0, .015f, 0, 18, .03f, 68);
        baseMesh.Finish();

        var structure = WorldAuthor.Child(room, "Remembered architecture");
        var pieces = new List<Transform>(); var positions = new List<Vector3>();
        var rotations = new List<Quaternion>(); var scattered = new List<Vector3>();
        var brokenRotations = new List<Quaternion>(); var anchors = new List<Vector3>();
        int rows = quest ? 4 : 8;
        for (int side = -1; side <= 1; side += 2)
        for (int row = 0; row < rows; row++) {
            float z = -27 + (row + .5f) * 54f / rows;
            var piece = WorldAuthor.Child(structure, "Remembered bay " + side + " " + row);
            var m = new StationMesh(piece, gen + "/Architecture");
            float bay = 54f / rows - .28f;
            m.Box("limestone", 0, 8.5f, 0, .8f, 17, bay);
            m.Box("bath tile", -side * .45f, 2.2f, 0, .10f, 4.4f, bay);
            m.Box("metal", -side * .52f, 4.48f, 0, .09f, .12f, bay);
            foreach (float edge in new[] { -.5f, .5f }) {
                m.Box("concrete", -side * .37f, 8.8f, edge * (bay - .25f), 1.6f, 17.6f, .3f);
                m.Box("brass", -side * 1.21f, 8.8f, edge * (bay - .25f), .065f, 16.5f, .11f);
            }
            m.Box("concrete", -side * .55f, 17.25f, 0, 2.4f, .55f, bay + .22f);
            m.Box("amber", -side * 1.8f, 16.72f, 0, .08f, .10f, bay - .4f);
            m.Box("black", -side * .46f, 11.15f, 0, .12f, 5.6f, bay * .65f);
            m.Box("glass", -side * .535f, 11.15f, 0, .035f, 5.35f, bay * .61f);
            m.Finish();
            AddPiece(piece, new Vector3(side * 25, 0, z), new Vector3(side * 3.5f, 4.5f + row % 3, (row % 2 == 0 ? -1 : 1) * 2.2f),
                new Vector3(5 + row * 2, side * (8 + row * 1.8f), side * 11), new Vector3(side * 16, .1f, z),
                pieces, positions, rotations, scattered, brokenRotations, anchors);
        }
        for (int row = 0; row < rows; row++) {
            float z = -27 + (row + .5f) * 54f / rows;
            // Alternating half-span lintels leave the middle of the sky uncovered.
            int side = row % 2 == 0 ? -1 : 1;
            var cornice = WorldAuthor.Child(structure, "Remembered canopy " + row);
            var m = new StationMesh(cornice, gen + "/Architecture");
            m.Box("limestone", 0, 0, 0, 12, .72f, 54f / rows - .25f);
            m.Box("cedar", 0, -.4f, 0, 11.7f, .1f, 54f / rows - .5f);
            m.Box("brass", -side * 5.9f, -.5f, 0, .1f, .1f, 54f / rows - .4f);
            m.Box("ice", 0, -.48f, 0, 8.7f, .06f, .16f);
            m.Finish();
            AddPiece(cornice, new Vector3(side * 18, 18.25f, z), new Vector3(side * 1.7f, 7 + row % 3, 1.6f),
                new Vector3(-12 + row * 3, side * 7, side * 13), new Vector3(side * 12, .1f, z),
                pieces, positions, rotations, scattered, brokenRotations, anchors);

            var stair = WorldAuthor.Child(structure, "Remembered suspended stair " + row);
            var stepMesh = new StationMesh(stair, gen + "/Architecture");
            for (int step = 0; step < (quest ? 5 : 8); step++) {
                float offset = step * .48f;
                stepMesh.Box("terrazzo", 0, offset * .55f, offset, 3.7f, .18f, .55f);
                stepMesh.Box("metal", 0, offset * .55f - .14f, offset, 3.8f, .08f, .6f);
            }
            stepMesh.Rod("brass", new Vector3(-1.95f, .8f, 0), new Vector3(-1.95f, 3f, 4), .05f, 6);
            stepMesh.Finish();
            AddPiece(stair, new Vector3(-side * 17, 5.5f + (row % 2) * 3, z), new Vector3(-side * 2, 4.2f, -2),
                new Vector3(21, row * 11 - 17, -side * 18), new Vector3(-side * 12, .1f, z),
                pieces, positions, rotations, scattered, brokenRotations, anchors);
        }
        var system = WorldAuthor.Child(room, "Local reconstruction").gameObject.AddUdonSharpComponent<RoomFormation>();
        system.space = structure; system.pieces = pieces.ToArray();
        system.assembledPositions = positions.ToArray(); system.assembledRotations = rotations.ToArray();
        system.scatteredPositions = scattered.ToArray(); system.scatteredRotations = brokenRotations.ToArray();
        system.influencePoints = anchors.ToArray();
        UdonSharpEditorUtility.CopyProxyToUdon(system);
        Label(room, "THE REMEMBERING HALL", new Vector3(0, 3.4f, -34.1f), 180, .55f, 18);
        Label(room, "THE ROOM RETURNS WHEN YOU DO", new Vector3(-20, 2.1f, -30), 0, .22f, 13);
        return room;
    }

    static void AddPiece(Transform piece, Vector3 position, Vector3 displacement, Vector3 twist, Vector3 anchor,
        List<Transform> pieces, List<Vector3> positions, List<Quaternion> rotations,
        List<Vector3> scattered, List<Quaternion> brokenRotations, List<Vector3> anchors)
    {
        piece.localPosition = position;
        piece.localRotation = Quaternion.identity;
        foreach (var renderer in piece.GetComponentsInChildren<MeshRenderer>()) {
            GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, 0);
            renderer.allowOcclusionWhenDynamic = false;
        }
        pieces.Add(piece); positions.Add(position); rotations.Add(Quaternion.identity);
        scattered.Add(position + displacement); brokenRotations.Add(Quaternion.Euler(twist)); anchors.Add(anchor);
    }

    static void Label(Transform room, string text, Vector3 position, float yaw, float size, float width)
    {
        var sign = WorldAuthor.Child(room, text); sign.localPosition = position; sign.localRotation = Quaternion.Euler(0, yaw, 0);
        WorldLettering.Install(sign, text, size, new Color(.72f, .90f, .95f)); WorldLettering.Fit(sign, width, 1);
    }
}
