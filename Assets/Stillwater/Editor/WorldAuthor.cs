using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.Core;
using VRC.SDKBase;
using VRC.SDK3.Components;
using UdonSharpEditor;

public static class WorldAuthor
{
    internal class District
    {
        public string id;
        public int x,z,number;
        public float w,d,h;
        public District(string id,int x,int z,int n,float w,float d,float h)
        {this.id=id;this.x=x;this.z=z;number=n;this.w=w;this.d=d;this.h=h;}
        public Vector3 Position => new Vector3(x*80,0,z*80);
        public bool Open => id=="hub" || id=="observatory" || id=="grove";
    }
    internal static readonly District[] Places={
        new District("hub",0,0,0,62,62,15),new District("rail",0,1,1,68,68,45),
        new District("air",1,0,2,62,60,28),new District("grove",1,1,3,58,58,31),
        new District("motor",1,-1,4,56,52,20),new District("gravity",-1,0,5,52,54,21),
        new District("observatory",-1,1,6,60,60,16),new District("gallery",0,-1,7,56,56,14),
        new District("service",-1,-1,8,48,46,4)};
    public const string Root="Assets/Stillwater";
    public const string PCScene=Root+"/Scenes/liminal uwu - PC.unity";
    public const string QuestScene=Root+"/Scenes/liminal uwu - Quest.unity";
    static bool mobile;
    static string gen;
    static Material plates, map;
    static readonly Rect Full=new Rect(0,0,1,1);
    internal static Transform Child(Transform parent,string name)
    {
        var go=new GameObject(name); go.transform.SetParent(parent,false); return go.transform;
    }

    [MenuItem("Liminal uwu/Author both scenes")]
    public static void Author()
    {
        if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if(!UpdateLayers.AreLayersSetup()) UpdateLayers.SetupEditorLayers();
        if(!UpdateLayers.IsCollisionLayerMatrixSetup()) UpdateLayers.SetupCollisionLayerMatrix();
        BuildScene(false); BuildScene(true);
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(PCScene,true),new EditorBuildSettingsScene(QuestScene,false)};
        EditorSceneManager.OpenScene(PCScene);
        AssetDatabase.SaveAssets();
        Debug.Log("LIMINAL_UWU_SCENES_AUTHORED");
        if(!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LIMINAL_PHOTOS"))) WorldPhotography.Capture();
    }
    static void BuildScene(bool quest)
    {
        mobile=quest; gen=Root+"/Generated/"+(quest?"Quest":"PC");
        Directory.CreateDirectory(gen+"/Architecture");
        Directory.CreateDirectory(Root+"/Scenes");
        AssetDatabase.Refresh();
        string scenePath=quest?QuestScene:PCScene;
        string blueprint="";
        if(File.Exists(scenePath)){
            EditorSceneManager.OpenScene(scenePath);
            var old=UnityEngine.Object.FindObjectOfType<PipelineManager>();
            if(old)blueprint=old.blueprintId;
        }
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        Surface.Prepare(quest); Surface.SetSky();
        plates=Printed("Station plates");map=Printed("Walking routes");
        var root=Child(null,"liminal uwu");
        var rooms=new Dictionary<string,Transform>();
        foreach(var r in Places)
        {
            var t=Child(root,r.id); rooms[r.id]=t; t.localPosition=r.Position;
            var mesh=new StationMesh(t,gen+"/Architecture");
            Shell(mesh,r);
            Furnish(mesh,r);
            NightArchitecture.Add(mesh,r.id,gen,plates,quest);
            mesh.Finish();
            Landmarks.Populate(t,r.id,quest);
            Surface.Ambience(t,r.id,quest);
            if(quest) CullDistantProps(t);
        }
        foreach(var r in Places)
        foreach(var k in new[]{0,1}){
            var n=Neighbour(r,k); if(n!=null) Passage(root,r,n,k);
        }
        CeilingWalk.Install(rooms["gravity"],quest);
        Departure(rooms);
        GravityLinks.Install(root,quest);
        foreach(var area in rooms.Values) BakingVolume(area,new Vector3(78,34,78));
        foreach(Transform zone in root)
            if(zone.name=="Soffit gallery" || zone.name=="Western facade gallery" || zone.name=="Six axis field chamber")
                BakingVolume(zone,new Vector3(62,54,65));
        var focus=Child(root,"World descriptor");
        var desc=focus.gameObject.AddComponent<VRCSceneDescriptor>();
        if(!focus.GetComponent<PipelineManager>())focus.gameObject.AddComponent<PipelineManager>();
        var pipeline=focus.GetComponent<PipelineManager>();
        pipeline.contentType=PipelineManager.ContentType.world;
        if(!string.IsNullOrWhiteSpace(blueprint))pipeline.blueprintId=blueprint;
        var spawn=Child(focus,"Arrival");spawn.position=new Vector3(0,.12f,-15);
        desc.spawns=new[]{spawn};desc.spawnRadius=1f;desc.RespawnHeightY=-35;
        // An edit-time lens is tagged EditorOnly and will be stripped from uploads.
        var lens=Child(root,"Editor arrival view");lens.gameObject.tag="EditorOnly";
        lens.position=spawn.position+new Vector3(0,1.55f,0);lens.rotation=Quaternion.Euler(-12,0,0);
        var cam=lens.gameObject.AddComponent<Camera>(); cam.fieldOfView=72;cam.farClipPlane=260;
        // Capture room probes while authoring the richer PC scene, never at runtime.
        if(!quest) foreach(var r in Places) Surface.Capture(rooms[r.id],r.id,false);
        EditorSceneManager.MarkSceneDirty(scene);
        if(!EditorSceneManager.SaveScene(scene,quest?QuestScene:PCScene))throw new IOException("Could not save scene.");
        AssetDatabase.SaveAssets();
    }
    static void BakingVolume(Transform parent,Vector3 size)
    {
        var volume=Child(parent,"Occlusion bake volume");
        volume.localPosition=new Vector3(0,size.y*.5f-1,0);
        volume.gameObject.AddComponent<OcclusionArea>().size=size;
    }
    static void CullDistantProps(Transform room)
    {
        foreach(Transform obj in room){
            if(!obj.name.StartsWith("Landmark-"))continue;
            var renderer=obj.GetComponent<Renderer>();
            if(!renderer)continue;
            var group=obj.gameObject.AddComponent<LODGroup>();
            group.SetLODs(new[]{new LOD(.17f,new[]{renderer})});
            group.localReferencePoint=obj.InverseTransformPoint(renderer.bounds.center);
            group.size=29f;
        }
    }
    static Material Printed(string name)
    {
        string path=gen+"/"+name+".mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m){m=new Material(Shader.Find("Unlit/Texture"));AssetDatabase.CreateAsset(m,path);}
        m.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Media/"+name+".png");
        if(!m.mainTexture) throw new IOException("Missing "+name);
        EditorUtility.SetDirty(m);return m;
    }
    static District Neighbour(District r,int k)
    {
        int dx=k==1?1:k==3?-1:0, dz=k==0?1:k==2?-1:0;
        return Places.FirstOrDefault(n=>n.x==r.x+dx && n.z==r.z+dz);
    }
    internal static void Plate(StationMesh m,int index,float x,float y,float z,float width)
    {
        float h=width/4;
        m.Box("metal",x,y,z+.06f,width+.11f,h+.10f,.12f);
        m.Print(plates,x,y,z-.012f,width,h,new Rect(index%2*.5f+.001f,1-(index/2+1)*.125f+.001f,.498f,.123f));
    }
    static void Shell(StationMesh m,District r)
    {
        m.Box(r.id=="service"?"ochre":r.id=="grove"?"tile":"wet",0,-.22f,0,r.w+.32f,.44f,r.d+.32f,true);
        for(int k=0;k<4;k++)
        {
            float w=(k%2==0?r.w:r.d);
            float depth=(k%2==0?r.d:r.w)*.5f;
            var n=Neighbour(r,k);
            m.Frame=Quaternion.Euler(0,90*k,0);m.Origin=m.Frame*new Vector3(0,0,depth);
            float h=r.h;
            if(r.id=="hub") h=new[]{10,16,12,19}[k];
            if(r.id=="grove")h=10;
            float doorH=r.id=="service"?3.05f:5.6f;
            float gap=n==null?0:9;
            foreach(int side in new[]{-1,1}){
                float width=(w-gap)*.5f;float mid=side*(gap/2+width/2);
                m.Box("concrete",mid,h*.5f,.2f,width,h,.46f,true);
                m.Box(r.id=="service"?"ochre":"tile",mid,1.02f,-.11f,width,2.04f,.21f);
                m.Box("metal",mid,.17f,-.22f,width,.32f,.11f);
                m.Box(r.id=="gallery"?"brass":"teal",mid,2.12f,-.18f,width,.13f,.19f);
            }
            if(gap>0){
                m.Box("concrete",0,(h+doorH)/2,.2f,gap,h-doorH,.46f,true);
                m.Box("metal",0,doorH-.06f,.06f,gap+.42f,.22f,.75f);
                if(r.id=="service") Plate(m,n.number,7.2f,2.41f,-.43f,4.5f);
                else Plate(m,n.number,0,doorH+.72f,-.43f,6);
                foreach(int j in new[]{-1,1}){
                    m.Box("metal",j*(gap/2+.11f),doorH/2,0,.22f,doorH,.78f);
                    m.Box(k%2==0?"amber":"ice",j*(gap/2+.11f),1.95f,-.42f,.085f,2.65f,.07f);
                }
            }
            if(r.id!="service"){
                bool facade=r.id=="hub"||r.id=="air"||r.id=="rail";
                float step=facade?(mobile && r.id!="hub"?6.25f:4.6f):6.5f;
                for(float x=-w/2+2.6f;x<w/2-1;x+=step){
                    bool nearOpening=Mathf.Abs(x)<6.1f && n!=null;
                    float low=nearOpening?doorH+2.35f:3.35f;
                    if(facade){
                        for(float y=low;y<h-1;y+=3.3f){
                            if(mobile && r.id!="hub"){
                                m.Print(Surface.Get("glass"),x,y,-.075f,3.15f,1.90f,Full);
                                continue;
                            }
                            m.Box("metal",x,y,-.08f,3.35f,2.18f,.24f);
                            m.Box("glass",x,y+.06f,-.22f,2.96f,1.76f,.075f);
                            if(!mobile || r.id=="hub") m.Box("metal",x,y+.06f,-.28f,.13f,1.8f,.06f);
                            m.Box("concrete",x,y-1.1f,-.29f,3.59f,.2f,.6f);
                        }
                    }else if(!nearOpening){
                        m.Box(r.id=="observatory"?"brass":"concrete",x,h*.5f,-.22f,.5f,h,.65f);
                        if(r.id!="gallery")m.Box("amber",x,h*.57f,-.58f,.11f,1.27f,.07f);
                    }
                }
            }
            if(r.id=="service")for(float x=-w/2+3;x<w/2;x+=4)if(gap==0||Mathf.Abs(x)>gap*.5f)
                m.Box("brass",x,1.55f,-.23f,.04f,2.96f,.05f);
            m.ResetFrame();
        }
        if(!r.Open){
            m.Box(r.id=="service"?"ivory":"concrete",0,r.h+.22f,0,r.w+.45f,.44f,r.d+.45f,true);
            float step=r.id=="service"?4.3f:7.5f;
            for(float z=-r.d/2+step*.5f;z<r.d/2;z+=step){
                m.Box("metal",0,r.h-.32f,z,r.w,.46f,.36f);
                if(r.id=="service"||r.id=="gallery")for(float x=-r.w/2+4;x<r.w/2;x+=7)
                    m.Box(r.id=="service"?"amber":"ice",x,r.h-.64f,z,2.6f,.08f,.38f);
            }
        }
    }

    static void Furnish(StationMesh m,District r)
    {
        switch(r.id){
            case "hub":
                for(int i=0;i<3;i++)foreach(int side in new[]{-1,1}) Streetlight(m,side*9, -16+i*15,side);
                // Street kiosk and folding transit seats flank an unobstructed promenade.
                m.Box("teal",-15,1.45f,7,5.2f,2.9f,3.2f,true);m.Box("ivory",-15,3.04f,7,6.2f,.32f,4.1f);
                m.Box("glass",-15,1.83f,5.36f,4.5f,1.35f,.12f);m.Box("brass",-15,.99f,5.1f,5.2f,.14f,.44f);
                m.Box("metal",5,1.73f,-8,3.12f,3.33f,.28f,true);
                m.Print(map,5,1.76f,-8.16f,2.92f,2.92f,Full);
                for(int i=0;i<3;i++)Bench(m,13,.02f,-11+i*9);
                Plate(m,9,0,6.9f,-23,8.8f); 
                for(int s=-1;s<=1;s+=2)m.Box("metal",s*4.65f,3.5f,-22.9f,.21f,7,.32f,true);
                break;
            case "gallery":
                GalleryDeck(m);
                for(int k=0;k<4;k++){
                    m.Frame=Quaternion.Euler(0,90*k,0);
                    foreach(int s in new[]{-1,1}){
                        float x=s*13.4f;
                        m.Box("brass",x,4.4f,27.28f,4.48f,6.65f,.20f);
                        m.Print(Surface.Get("art"+((k+(s+1)/2)%4)),x,4.4f,27.16f,4.26f,6.40f,Full);
                        m.Box("metal",x,8.0f,26.95f,2.1f,.19f,.68f);
                        m.Box("amber",x,7.90f,26.85f,1.84f,.08f,.41f);
                    }
                }m.ResetFrame();break;
            case "service":
                for(int k=0;k<2;k++){
                    m.Frame=Quaternion.Euler(0,180*k,0);
                    for(int i=0;i<3;i++){
                        m.Box("ochre",9+i*3.8f,1.82f,9+(i%2)*3,.36f,3.64f,8.6f,true);
                        m.Box("brass",9+i*3.8f,.17f,9+(i%2)*3,.43f,.34f,8.63f);
                    }
                }m.ResetFrame();break;
            case "rail":
                foreach(int s in new[]{-1,1})for(int i=0;i<3;i++)Bench(m,s*10,0,i*5-5);
                break;
        }
    }
    static void Bench(StationMesh m,float x,float y,float z){
        m.Box("metal",x,y+.26f,z,2.5f,.48f,.49f,true);
        for(int i=0;i<4;i++)m.Box("rust",x,y+.56f,z-.32f+i*.2f,3,.15f,.15f);
        m.Box("teal",x,y+.92f,z+.4f,3,.49f,.12f);
    }
    static void GalleryDeck(StationMesh m)
    {
        // Two gentle access ramps feed a continuous balcony crossing the art hall.
        foreach(int side in new[]{-1,1}){
            Vector3 a=new Vector3(side*19,0,-18), b=new Vector3(side*19,4.4f,5);
            var dir=b-a; var rot=Quaternion.Euler(-Mathf.Atan2(dir.y,dir.z)*Mathf.Rad2Deg,0,0);
            m.Box("tile",(a+b)*.5f-rot*Vector3.up*.17f,new Vector3(4,.34f,dir.magnitude+.05f),rot,true);
            m.Box("tile",side*19,4.22f,10,4,.36f,10.1f,true);
            for(int rail=-1;rail<=1;rail+=2){
                Vector3 off=new Vector3(rail*1.92f,.98f,0);
                m.Rod("brass",a+off,b+off,.055f);
                for(int j=0;j<=6;j++){Vector3 p=Vector3.Lerp(a,b,j/6f)+new Vector3(rail*1.92f,0,0);m.Rod("metal",p,p+Vector3.up,.07f);}
            }
            m.Box("metal",side*19,2.03f,14,.52f,4.06f,1.2f,true);
        }
        m.Box("tile",0,4.22f,13,34.2f,.36f,4,true);
        for(int s=-1;s<=1;s+=2){
            m.Box("brass",0,5.37f,13+s*1.9f,34,.13f,.13f);
            for(int j=-16;j<=16;j+=4)m.Box("metal",j,4.92f,13+s*1.9f,.1f,1.04f,.1f);
        }
    }
    static void Streetlight(StationMesh m,float x,float z,int side)
    {
        m.Origin=new Vector3(x,0,z);
        m.Box("concrete",0,.22f,0,.89f,.44f,.89f,true);
        m.Rod("metal",new Vector3(0,.35f,0),new Vector3(0,5.9f,0),.13f,8);
        m.Rod("brass",new Vector3(0,5.8f,0),new Vector3(-side*2.25f,6.35f,0),.10f,8);
        m.Box("metal",-side*2.04f,6.31f,0,1.14f,.21f,.47f);
        m.Box("amber",-side*2.04f,6.17f,0,1,.11f,.4f);
        float mid=-side*1.43f;
        m.Box("black",mid,4.63f,0,2.6f,1.02f,.16f);
        m.Rod("metal",new Vector3(mid-.9f,5.11f,0),new Vector3(mid-.9f,6.04f,0),.045f);
        m.Rod("metal",new Vector3(mid+.9f,5.11f,0),new Vector3(mid+.9f,6.04f,0),.045f);
        foreach(int face in new[]{-1,1}){
            for(int letter=0;letter<3;letter++){
                float offset=mid+(letter-1)*.72f;
                if(letter==1){
                    float[] px={-.31f,-.18f,0,.18f,.31f}, py={.21f,-.22f,-.02f,-.22f,.21f};
                    for(int j=1;j<5;j++) m.Rod("pink",new Vector3(offset+px[j-1],4.64f+py[j-1],face*.11f),new Vector3(offset+px[j],4.64f+py[j],face*.11f),.043f,8);
                }else{
                    m.Rod("pink",new Vector3(offset-.24f,4.89f,face*.11f),new Vector3(offset-.24f,4.62f,face*.11f),.045f,8);
                    for(int j=0;j<10;j++){
                        float a=Mathf.PI+j*Mathf.PI/10,b=a+Mathf.PI/10;
                        m.Rod("pink",new Vector3(offset+.24f*Mathf.Cos(a),4.62f+.24f*Mathf.Sin(a),face*.11f),new Vector3(offset+.24f*Mathf.Cos(b),4.62f+.24f*Mathf.Sin(b),face*.11f),.045f,8);
                    }
                    m.Rod("pink",new Vector3(offset+.24f,4.62f,face*.11f),new Vector3(offset+.24f,4.89f,face*.11f),.045f,8);
                }
            }
        }m.ResetFrame();
    }
    static void Passage(Transform root,District a,District b,int k)
    {
        float da=(k==0?a.d:a.w)/2,db=(k==0?b.d:b.w)/2,len=80-da-db;
        var path=Child(root,"Passage "+a.id+" to "+b.id); var f=Quaternion.Euler(0,k*90,0);
        path.position=a.Position+f*new Vector3(0,0,da+len/2);path.rotation=f;
        var m=new StationMesh(path,gen+"/Architecture");
        float height=a.id=="service"||b.id=="service"?3.25f:4.45f;
        m.Box("wet",0,-.19f,0,9.5f,.38f,len+.1f,true);
        m.Box("concrete",0,height+.17f,0,9.7f,.34f,len+.2f,true);
        foreach(int s in new[]{-1,1}){
            m.Box("plaster",s*4.7f,height/2,0,.38f,height,len+.18f,true);
            m.Box((a.number+b.number)%2==0?"teal":"tile",s*4.46f,.84f,0,.15f,1.68f,len);
            m.Box("brass",s*4.36f,1.69f,0,.09f,.08f,len);
            if((a.number+b.number)%3!=0){
                for(int pipe=0;pipe<(mobile?1:3);pipe++)m.Rod(pipe==0?"rust":"metal",new Vector3(s*(3.94f-pipe*.22f),height-.38f,-len/2),new Vector3(s*(3.94f-pipe*.22f),height-.38f,len/2),.095f,8);
            }
            for(float d=-len/2+1;d<len/2;d+=3.8f)
                m.Box("metal",s*4.41f,height/2,d,.15f,height,.21f);
        }
        for(float d=-len/2+1.7f;d<len/2;d+=4.2f){
            m.Box("metal",0,height-.16f,d,3.55f,.20f,.53f);
            m.Box((a.number+b.number)%3==0?"amber":"ice",0,height-.29f,d,3.18f,.08f,.3f);
        }
        m.Finish();
    }
    static void Departure(Dictionary<string,Transform> rooms)
    {
        var t=Child(rooms["hub"],"Request an unknown platform");
        t.localPosition=new Vector3(-5,0,-8);
        var m=new StationMesh(t,gen+"/Architecture");
        m.Box("teal",0,.95f,0,2.02f,1.9f,.68f,true);m.Box("metal",0,2.31f,.13f,2.1f,1.02f,.43f);
        Plate(m,10,0,2.39f,-.13f,1.98f);
        m.Box("amber",0,1.48f,-.4f,.68f,.21f,.1f);m.Finish();
        var action=Child(t,"Request departure");action.localPosition=new Vector3(0,1.49f,-.47f);
        action.gameObject.AddComponent<BoxCollider>().size=new Vector3(1.75f,1.51f,.2f);
        var script=action.gameObject.AddUdonSharpComponent<LastTrainBoard>();
        script.destinations=Places.Where(r=>r.id!="hub").Select(r=>{
            var dest=Child(rooms[r.id],"Unscheduled arrival");dest.localPosition=new Vector3(0,.13f,-8);
            return dest;}).ToArray();
        UdonSharpEditorUtility.CopyProxyToUdon(script);
        var udon=UdonSharpEditorUtility.GetBackingUdonBehaviour(script);udon.interactText="Take a random departure";udon.proximity=3;
    }
}
