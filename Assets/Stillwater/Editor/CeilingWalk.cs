using UnityEditor;
using UnityEngine;
using UdonSharpEditor;
using VRC.Udon;

// The walkable copies share the original room's meshes and materials. The VR tracking
// origin stays level, so normal locomotion, crouching and room-scale walking still work.
public static class CeilingWalk
{
    const string Art = "Assets/Stillwater/Runtime/Art";
    const string GravityAudio = "Assets/Stillwater/Media/Audio/gravity.wav";

    public static void Install(Transform gravityRoom, bool quest)
    {
        // A clean batch checkout has no compiled behaviour heap yet. Wait for UdonSharp
        // before copying destination references onto the first interactable.
        var program = UdonSharp.UdonSharpProgramAsset.GetProgramAssetForClass(typeof(StillwaterGravityPort));
        if (!program) throw new System.InvalidOperationException("Gravity Udon program not imported.");
        if (program.CompiledVersion != UdonSharp.UdonSharpProgramVersion.CurrentVersion)
            UdonSharp.Compiler.UdonSharpCompilerV1.CompileSync(new UdonSharp.Compiler.UdonSharpCompileOptions());
        if (program.CompiledVersion != UdonSharp.UdonSharpProgramVersion.CurrentVersion)
            throw new System.InvalidOperationException("Gravity Udon compilation failed. See the first compiler error.");
        string parts = Art + (quest ? "/Quest" : "/PC");
        if (!AssetDatabase.IsValidFolder(parts)) AssetDatabase.CreateFolder(Art, quest ? "Quest" : "PC");
        Material sky = Display("Estuary", ".jpg");
        Material ceiling = Display("Soffit", ".png");
        Material wall = Display("Facade", ".png");
        Material drift = Display("Drift", ".png");
        Material back = Display("Return", ".png");
        Material north = Display("North", ".png");

        AddReadingRoomColliders(gravityRoom);
        Transform windows = Child(gravityRoom, "Floodplain weather windows");
        var frame = new StationMesh(windows, parts);
        Window(frame, sky, new Vector3(-11, 6.7f, -26.45f), Quaternion.Euler(0, 180, 0), 12);
        Window(frame, sky, new Vector3(11, 6.7f, -26.45f), Quaternion.Euler(0, 180, 0), 12);
        frame.Finish();

        // Take both snapshots before the terminals exist, to avoid duplicating live Udon ports.
        Transform upper = RotatedWing(gravityRoom, "Soffit gallery", new Vector3(0, 0, -460), 180, new Vector3(0,21,0));
        Transform lateral = RotatedWing(gravityRoom, "Western facade gallery", new Vector3(0, 0, -640), 90, new Vector3(10.5f,26,0));
        Transform chamber = Child(gravityRoom.parent, "Six axis field chamber");
        chamber.position = gravityRoom.position + new Vector3(0,0,-820);

        Transform entrancePad = Waypoint(gravityRoom, "Gallery landing", -14, 0.40f, -9.1f, 180);
        Transform northPad = Waypoint(gravityRoom, "Northern corridor landing", 0, 0.40f, 22, 0);
        Transform soffitPad = EquipWing(upper, parts, 52, 21, 14, entrancePad, northPad, back, north);
        Transform facadePad = EquipWing(lateral, parts, 21, 52, 3.8f, entrancePad, northPad, back, north);
        Transform[] route = BuildChamber(chamber, parts, sky, entrancePad, back);
        Transform boarding = Waypoint(chamber, "Weightless starting position", -7,1.4f,-8, 18);

        Transform bank = Child(gravityRoom, "Field lab terminals");
        var stations = new StationMesh(bank, parts);
        Terminal(stations, bank, "Ceiling walk", ceiling, -17.1f, 0, -12, 180, soffitPad);
        Terminal(stations, bank, "Wall walk", wall, -14, 0, -12, 180, facadePad);
        StillwaterGravityPort ride = Terminal(stations, bank, "Six direction ride", drift, -10.9f, 0, -12, 180, boarding);
        ride.circuit = chamber;
        ride.stops = route;
        ride.exit = entrancePad;
        UdonSharpEditorUtility.CopyProxyToUdon(ride);
        EditorUtility.SetDirty(ride);
        stations.Finish();
    }

    static void AddReadingRoomColliders(Transform room)
    {
        // The archive is assembled into one decorative render mesh. Give its furniture
        // separate simple collision shapes so it also behaves like furniture from above.
        Transform solid = Child(room,"Archive furniture collision");
        solid.localPosition = new Vector3(0,20.1f,0);
        solid.localRotation = Quaternion.Euler(0,0,180);
        for (int row=-1; row<=1; row+=2)
        for (int i=0; i<4; i++)
        {
            Transform table = Child(solid,"Reading table");
            table.localPosition = new Vector3(-14+i*9.2f,0,row*9.2f);
            table.localRotation = Quaternion.Euler(0,(i%2==0?9:-13)*row,0);
            FurnitureBox(table,new Vector3(0,2.13f,0),new Vector3(4.76f,.22f,2.65f));
            foreach(float dx in new[]{-1.93f,1.93f})
            foreach(float dz in new[]{-1.01f,1.01f})
                FurnitureBox(table,new Vector3(dx*1.05f,1.05f,dz*1.08f),new Vector3(.24f,2.08f,.25f));
            foreach(float dx in new[]{-1.16f,1.16f})
            foreach(float dz in new[]{-2.25f,2.25f})
                FurnitureBox(table,new Vector3(dx,.92f,dz),new Vector3(.96f,.35f,.93f));
        }
        foreach(int side in new[]{-1,1})
            FurnitureBox(solid,new Vector3(side*20,2.73f,0),new Vector3(1.40f,5.46f,17.6f));
    }

    static void FurnitureBox(Transform parent, Vector3 center, Vector3 size)
    {
        var collider = parent.gameObject.AddComponent<BoxCollider>();
        collider.center = center;
        collider.size = size;
    }

    static Transform Child(Transform parent, string name)
    {
        Transform item = new GameObject(name).transform;
        item.SetParent(parent, false);
        return item;
    }

    static Transform Waypoint(Transform parent, string name, float x, float y, float z, float heading)
    {
        Transform point = Child(parent, name);
        point.localPosition = new Vector3(x,y,z);
        point.localRotation = Quaternion.Euler(0,heading,0);
        return point;
    }

    static Material Display(string name, string extension)
    {
        string path = Art + "/" + name;
        Material result = AssetDatabase.LoadAssetAtPath<Material>(path + ".mat");
        if (result != null) return result;
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path + extension);
        if (!texture) throw new System.InvalidOperationException("Missing gravity gallery texture: " + path + extension);
        result = new Material(Shader.Find("Unlit/Texture")) { name = name, mainTexture = texture };
        AssetDatabase.CreateAsset(result, path + ".mat");
        return result;
    }

    static Transform RotatedWing(Transform room, string name, Vector3 offset, float roll, Vector3 pivot)
    {
        Transform wing = Child(room.parent,name);
        wing.position = room.position + offset;
        GameObject copy = Object.Instantiate(room.gameObject, wing, false);
        copy.name = "Architecture carried through " + roll + " degrees";
        copy.transform.localPosition = pivot;
        copy.transform.localRotation = Quaternion.Euler(0,0,roll);
        return wing;
    }

    static Transform EquipWing(Transform wing, string folder, float width, float height, float entryX,
                                Transform home, Transform northExit, Material back, Material north)
    {
        Transform hardware = Child(wing, wing.name + " fittings");
        var body = new StationMesh(hardware,folder);
        // The original roof or facade sits just above this deck in most places. The solid
        // trim bridges the original door and window openings so a walker cannot fall out.
        body.Box("concrete",0,-0.57f,0,width+0.8f,0.7f,54.8f,true);
        body.Box("metal",0,height/2,-27.6f,width+0.8f,height+1,0.35f,true);
        body.Box("metal",0,height/2,27.6f,width+0.8f,height+1,0.35f,true);
        body.Box("metal",-width/2-0.44f,height/2,0,0.35f,height+1,54.8f,true);
        body.Box("metal",width/2+0.44f,height/2,0,0.35f,height+1,54.8f,true);
        Terminal(body,hardware,"Return to the floor",back,entryX,0.36f,-17.35f,180,home);
        Terminal(body,hardware,"Northern exit",north,-entryX,0.36f,20,0,northExit);
        // Short lit approach markers are useful in both orientations and keep the broad
        // central floor clear. All instances of each material form one mesh section.
        for (int z = -9; z <= 16; z += 5)
        {
            float t = (z+9f)/25;
            float x = Mathf.Lerp(entryX,-entryX,t);
            body.Box("ice",x,0.42f,z,0.42f,0.026f,0.11f);
            body.Box("metal",x,0.40f,z,0.62f,0.045f,0.19f);
        }
        body.Finish();
        if (wing.GetComponentInChildren<AudioSource>() == null) Ambience(wing, new Vector3(0,5,0));
        return Waypoint(wing, "Arrival on rotated architecture", entryX, 0.94f,-14.45f,0);
    }

    static StillwaterGravityPort Terminal(StationMesh m, Transform parent, string name, Material face,
                                           float x,float y,float z,float heading, Transform destination)
    {
        m.Origin = new Vector3(x,y,z);
        m.Frame = Quaternion.Euler(0,heading,0);
        m.Box("metal",0,.08f,0,1.62f,.16f,.70f,true);
        m.Box("brass",0,.68f,0,.23f,1.10f,.29f,true);
        m.Box("metal",0,1.44f,0,1.70f,.95f,.24f);
        m.Print(face,0,1.46f,-.126f,1.57f,.785f,new Rect(0,0,1,1));
        m.Box("ice",0,1.97f,0,.48f,.045f,.14f);
        Transform target = Child(parent,name);
        target.localPosition = m.Local(new Vector3(0,1.44f,0));
        target.localRotation = m.Frame;
        target.gameObject.AddComponent<BoxCollider>().size = new Vector3(1.71f,.99f,.29f);
        var port = target.gameObject.AddUdonSharpComponent<StillwaterGravityPort>();
        port.destination = destination;
        UdonSharpEditorUtility.CopyProxyToUdon(port);
        UdonBehaviour backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(port);
        backing.interactText = name;
        backing.proximity = 3.0f;
        EditorUtility.SetDirty(backing);
        EditorUtility.SetDirty(port);
        m.ResetFrame();
        return port;
    }

    static void Window(StationMesh m, Material sky, Vector3 center, Quaternion orientation, float width)
    {
        float height=width*.5f;
        m.Origin=center;
        m.Frame=orientation;
        m.Box("metal",0,0,0,width+.48f,height+.48f,.46f);
        m.Print(sky,0,0,-.238f,width,height,new Rect(0,0,1,1));
        m.Box("brass",0,-height/2-.15f,-.20f,width+.56f,.15f,.22f);
        // The mullions sit outside the round aperture image so the void stays visibly black.
        m.Box("metal",width*-.26f,0,-.26f,.07f,height,.08f);
        m.ResetFrame();
    }

    static Transform[] BuildChamber(Transform chamber, string folder, Material sky, Transform home, Material back)
    {
        Transform shell = Child(chamber,"Field chamber lining");
        var m = new StationMesh(shell,folder);
        m.Box("concrete",0,-.20f,0,26,.40f,28,true);
        m.Box("metal",0,17.9f,0,26,.32f,28,true);
        m.Box("teal",0,9,-14,26,18,.36f,true);
        m.Box("teal",0,9,14,26,18,.36f,true);
        m.Box("concrete",13,9,0,.35f,18,28,true);
        m.Box("concrete",-13,9,0,.35f,18,28,true);
        for(int k=-2;k<=2;k++)
        {
            float z=k*5.4f;
            foreach(int side in new[]{-1,1})
            {
                m.Box("metal",side*12.6f,9,z,.42f,17.7f,.38f);
                m.Box("ice",side*12.37f,9,z,.05f,1.3f,.18f);
            }
            m.Box("metal",0,17.55f,z,25.25f,.38f,.37f);
        }
        Window(m,sky,new Vector3(0,8.4f,-13.5f),Quaternion.Euler(0,180,0),17.4f);
        Window(m,sky,new Vector3(0,9.2f,13.5f),Quaternion.Euler(0,0,180),16.2f);
        Window(m,sky,new Vector3(12.43f,8.7f,0),Quaternion.Euler(0,90,90),14.9f);
        Terminal(m,shell,"Return from field chamber",back,-7,0,-10,180,home);
        Vector3[] positions={
            new Vector3(-7,9,-8),new Vector3(7,9,-8),new Vector3(7,9,8),
            new Vector3(7,2.5f,8),new Vector3(-7,2.5f,8),new Vector3(-7,2.5f,-8)
        };
        Transform[] stops = new Transform[positions.Length];
        for(int i=0;i<positions.Length;i++)
        {
            stops[i]=Child(chamber,"Field stop "+(i+1));
            stops[i].localPosition=positions[i];
            // Each small survey instrument hangs just outside its passenger trajectory.
            Vector3 p=positions[i]+new Vector3(positions[i].x>0?1.7f:-1.7f,1.15f,0);
            m.Box("brass",p.x,p.y,p.z,.46f,.65f,.44f);
            m.Box("amber",p.x,p.y+.39f,p.z,.32f,.12f,.32f);
            m.Rod("metal",new Vector3(p.x,17.55f,p.z),p,.025f,5);
        }
        m.Finish();
        Ambience(chamber,new Vector3(0,7,0));
        return stops;
    }

    static void Ambience(Transform room, Vector3 localPosition)
    {
        Transform speaker = Child(room,"Local field induction tone");
        speaker.localPosition=localPosition;
        var audio=speaker.gameObject.AddComponent<AudioSource>();
        audio.clip=AssetDatabase.LoadAssetAtPath<AudioClip>(GravityAudio);
        audio.spatialBlend=1;
        audio.loop=true;
        audio.playOnAwake=true;
        audio.volume=.42f;
        audio.dopplerLevel=0;
        audio.rolloffMode=AudioRolloffMode.Linear;
        audio.minDistance=5;
        audio.maxDistance=39;
    }
}
