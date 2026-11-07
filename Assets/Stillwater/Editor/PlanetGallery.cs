using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UdonSharp;
using UdonSharpEditor;
using BobyStar.PlanetMovement;

// A roofless transit rotunda with a continuous vertical walking surface. Its
// gravity is provided by the actual prefab credited by Black Fire's creator.
internal static class PlanetGallery
{
    internal static readonly Vector3 Position=new Vector3(1000,0,200);
    internal static Bounds Bounds=>new Bounds(Position+new Vector3(0,32,0),new Vector3(80,76,80));
    const string Private="Assets/Stillwater/Personal/PlanetMovement";
    internal static void PreparePrograms()
    {
        Program(WorldAuthor.Root+"/Runtime/PlanetGalleryControl");
        foreach(string name in new[]{"BobysPlanetMovement","PlanetMove_GravityField","PlanetMove_GravityFieldGroup","PlanetMove_StationListener","PlanetMove_AutoExitManager"})
            Program(Private+"/"+name);
        AssetDatabase.SaveAssets();
    }
    internal static void KeepProgramsPrivate()
    {
        if(!AssetDatabase.IsValidFolder(Private+"/Compiled"))AssetDatabase.CreateFolder(Private,"Compiled");
        foreach(string name in new[]{"BobysPlanetMovement","PlanetMove_GravityField","PlanetMove_GravityFieldGroup","PlanetMove_StationListener","PlanetMove_AutoExitManager"}) {
            var program=AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(Private+"/"+name+".asset");
            if(!program || !program.SerializedProgramAsset)throw new InvalidOperationException("Gravity program was not compiled: "+name);
            string old=AssetDatabase.GetAssetPath(program.SerializedProgramAsset);
            if(old.StartsWith(Private+"/",StringComparison.Ordinal))continue;
            string target=Private+"/Compiled/"+Path.GetFileName(old);
            string error=AssetDatabase.MoveAsset(old,target);
            if(!string.IsNullOrEmpty(error))throw new InvalidOperationException("Cannot preserve private gravity bytecode: "+error);
            EditorUtility.SetDirty(program);
        }
        AssetDatabase.SaveAssets();
    }
    static void Program(string stem)
    {
        if(AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(stem+".asset"))return;
        var source=AssetDatabase.LoadAssetAtPath<MonoScript>(stem+".cs");
        if(!source)throw new InvalidOperationException("Missing owner-supplied gravity source: "+stem);
        var program=ScriptableObject.CreateInstance<UdonSharpProgramAsset>();program.sourceCsScript=source;
        AssetDatabase.CreateAsset(program,stem+".asset");
    }
    internal static Transform Install(Transform root,string gen,bool quest)
    {
        Directory.CreateDirectory(gen+"/Architecture");
        var room=WorldAuthor.Child(root,"planet");room.position=Position;
        var m=new StationMesh(room,gen+"/Architecture");
        m.Box("wet",0,-.4f,0,78,.8f,78,true);
        // Tall disconnected fins and open roof retain a clear view of the sky.
        foreach(int s in new[]{-1,1}) {
            m.Box("concrete",s*39,31,0,2,64,80,true);
            m.Box("bath tile",s*37.92f,4,0,.15f,8,78);
            for(float z=-34;z<=34;z+=quest?17:8.5f) {
                m.Box("metal",s*37.8f,31,z,.2f,62,.4f);
                m.Box("brass",s*37.6f,1.5f,z,.1f,.12f,5);
            }
            m.Box("concrete",s*23,3,-39,30,6,2,true);
            m.Box("concrete",s*23,3,39,30,6,2,true);
        }
        // Centre plane and fixtures are assembled once per room/material. The
        // 96-segment collision ring is identical on PC and Quest.
        const int segments=96;const float radius=26.3f;
        for(int i=0;i<segments;i++) {
            float angle=i*360f/segments,rad=angle*Mathf.Deg2Rad;
            Vector3 outward=new Vector3(Mathf.Sin(rad),-Mathf.Cos(rad),0);
            Quaternion rotation=Quaternion.Euler(0,0,angle);
            Vector3 p=new Vector3(0,27,0)+outward*radius;
            float width=2*radius*Mathf.Tan(Mathf.PI/segments)+.018f;
            m.Box(i%8==0?"metal":"bath tile",p,new Vector3(width,.6f,24),rotation,true);
            // Recessed strips, with physical edge rails kept out of the bottom
            // gateway so entering and leaving never requires a jump.
            if(i%8==0) {
                m.Box("metal",p-outward*.43f,new Vector3(.42f,.2f,20),rotation);
                m.Box(i%16==0?"pink":"ice",p-outward*.55f,new Vector3(.16f,.045f,19.6f),rotation);
            }
            if(i>5 && i<segments-5) foreach(float z in new[]{-11.8f,11.8f}) {
                var rail=p-outward*.94f;rail.z=z;
                m.Solid(rail,new Vector3(width,1.3f,.2f),rotation,"Wall-walk edge parapet");
                m.Box("metal",rail-outward*.62f,new Vector3(width,.10f,.15f),rotation);
            }
        }
        // Gentle ramps join the one-metre-high ring floor to each level lobby.
        foreach(int s in new[]{-1,1}) {
            m.Box("concrete",new Vector3(0,.40f,s*16.5f),new Vector3(10,.3f,9.2f),Quaternion.Euler(s*6.34f,0,0),true);
            m.Box("metal",-6,1,s*22,.18f,2,.18f,true);
            m.Box("metal",6,1,s*22,.18f,2,.18f,true);
            m.Box("ice",-5.86f,1.2f,s*22,.04f,1.45f,.12f);
            m.Box("pink",5.86f,1.2f,s*22,.04f,1.45f,.12f);
        }
        // Side seating and planters are outside the motion path.
        foreach(int s in new[]{-1,1}) foreach(float z in new[]{-28f,28f}) {
            m.Box("cedar",s*21,.65f,z,8,.25f,1.3f,true);
            m.Box("metal",s*21,.33f,z,6,.6f,.55f);
            m.Box("cedar",s*21,1.12f,z+.6f,8,.9f,.16f);
            m.Box("black",s*30,.7f,z,2.5f,1.4f,2.5f,true);
        }
        m.Finish();
        Label(room,"Rotunda title","THE FALLING UP",new Vector3(0,5,-28),.55f,18);
        Label(room,"Wall walk guide","FOLLOW THE CURVE / WALK ON THE WALLS",new Vector3(0,3.7f,-22.2f),.24f,15);
        Label(room,"Gravity credit","GRAVITY: BOBYSTAR / BOBY'S PLANET MOVEMENT",new Vector3(-20,2.1f,-28.7f),.17f,14);
        Label(room,"Gravity return guide","RETURN THROUGH THE LOWER OPENINGS",new Vector3(20,2.1f,-28.7f),.18f,13);
        var controller=Controller(root,room);
        var field=WorldAuthor.Child(room,"Radial wall gravity");field.localPosition=new Vector3(0,27,0);
        var trigger=field.gameObject.AddComponent<BoxCollider>();trigger.isTrigger=true;trigger.size=new Vector3(60,58,26);
        var gravity=field.gameObject.AddUdonSharpComponent<PlanetMove_GravityField>();
        gravity.fieldType=GravityFieldType.Axis;gravity.axisGravity=Vector3.forward;gravity.strength=-.72f;
        gravity.onlyInTrig=true;gravity.autoEnter=true;gravity.autoEnterController=controller;gravity.priority=10;
        UdonSharpEditorUtility.CopyProxyToUdon(gravity);
        var bridge=WorldAuthor.Child(room,"Gravity room boundary").gameObject.AddUdonSharpComponent<PlanetGalleryControl>();
        bridge.controller=controller;bridge.gravityField=gravity;bridge.room=room;
        bridge.southReturn=WorldAuthor.Child(room,"Return to south lobby");bridge.southReturn.localPosition=new Vector3(0,.2f,-24);bridge.southReturn.localRotation=Quaternion.Euler(0,180,0);
        bridge.northReturn=WorldAuthor.Child(room,"Return to north lobby");bridge.northReturn.localPosition=new Vector3(0,.2f,24);
        UdonSharpEditorUtility.CopyProxyToUdon(bridge);
        return room;
    }
    static BobysPlanetMovement Controller(Transform root,Transform room)
    {
        // Construct the documented prefab hierarchy before adding the controller:
        // its original OnValidate populates these required private references.
        var body=WorldAuthor.Child(root,"BobyStar local gravity controller");body.position=room.position+new Vector3(0,1.8f,0);body.gameObject.layer=18;
        var rigid=body.gameObject.AddComponent<Rigidbody>();rigid.useGravity=false;rigid.constraints=RigidbodyConstraints.FreezeRotation;
        rigid.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
        var sphere=body.gameObject.AddComponent<SphereCollider>();sphere.radius=.75f;
        string path=WorldAuthor.Root+"/Runtime/Planet frictionless.physicMaterial";
        var material=AssetDatabase.LoadAssetAtPath<PhysicMaterial>(path);
        if(!material){material=new PhysicMaterial("Planet frictionless");AssetDatabase.CreateAsset(material,path);}
        material.staticFriction=0;material.dynamicFriction=0;material.bounciness=0;material.frictionCombine=PhysicMaterialCombine.Minimum;sphere.sharedMaterial=material;
        var holder=WorldAuthor.Child(body,"Player Holder");var rotation=WorldAuthor.Child(holder,"Player Rotation");
        var seat=WorldAuthor.Child(rotation,"Local gravity station");seat.localPosition=new Vector3(0,-.775f,0);
        var exit=WorldAuthor.Child(seat,"Exit station");
        var station=seat.gameObject.AddComponent<VRC.SDK3.Components.VRCStation>();station.seated=false;station.disableStationExit=true;station.canUseStationFromStation=true;
        station.PlayerMobility=VRC.SDKBase.VRCStation.Mobility.ImmobilizeForVehicle;station.stationEnterPlayerLocation=seat;station.stationExitPlayerLocation=exit;
        var listener=seat.gameObject.AddUdonSharpComponent<PlanetMove_StationListener>();
        WorldAuthor.Child(body,"BPL Parent Holder");
        var exitObject=WorldAuthor.Child(body,"BPL Auto Exit Manager");
        var manager=exitObject.gameObject.AddUdonSharpComponent<PlanetMove_AutoExitManager>();
        var controller=body.gameObject.AddUdonSharpComponent<BobysPlanetMovement>();
        controller.station=station;controller.rb=rigid;controller.normGravFallback=true;controller.alignGravToGround=false;
        controller.autoDisable=true;controller.comfortTurning=true;controller.groundSnapping=false;controller.gravityFieldCountMax=8;
        listener.planetMove=controller;manager.controller=controller;controller.OnValidate();
        if(!controller.isReady)throw new InvalidOperationException("BobyStar's gravity controller hierarchy is incomplete.");
        UdonSharpEditorUtility.CopyProxyToUdon(listener);UdonSharpEditorUtility.CopyProxyToUdon(manager);UdonSharpEditorUtility.CopyProxyToUdon(controller);
        return controller;
    }
    static void Label(Transform room,string name,string text,Vector3 position,float size,float width)
    {
        var t=WorldAuthor.Child(room,name);t.localPosition=position;
        WorldLettering.Install(t,text,size,new Color(.7f,.86f,.9f));WorldLettering.Fit(t,width,1.2f);
    }
}
