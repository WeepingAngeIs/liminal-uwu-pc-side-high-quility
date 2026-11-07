using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// A second walking circuit, kept well beyond the original nine courtyards.
// Distances, doors and collision are identical on both platforms. Only dressing
// and the station's mesh/finish complexity change on Quest.
public static class OuterDistricts
{
    public sealed class ZoneSpec
    {
        public readonly string id, title;
        public readonly Vector3 position, size;
        public readonly int[] doors;
        public readonly bool open;
        public ZoneSpec(string id,string title,Vector3 position,Vector3 size,bool open,params int[] doors)
        { this.id=id;this.title=title;this.position=position;this.size=size;this.open=open;this.doors=doors; }
        public bool HasDoor(int side) => Array.IndexOf(doors,side)>=0;
        public Bounds VisibilityBounds => new Bounds(position+Vector3.up*(size.y*.5f-2),size+new Vector3(4,8,4));
    }

    // Cardinal sides: north +Z, east +X, south -Z, west -X.
    public static readonly ZoneSpec[] Specs={
        new ZoneSpec("billboard","AD ALLEY",new Vector3(240,0,0),new Vector3(84,26,68),true,3,1,0),
        new ZoneSpec("concourse","LAST CONCOURSE",new Vector3(400,0,0),new Vector3(88,22,76),false,3,0),
        new ZoneSpec("causeway","ORBITAL CAUSEWAY",new Vector3(400,0,200),new Vector3(160,120,148),true,2,3),
        new ZoneSpec("stacks","QUIET STACKS",new Vector3(240,0,200),new Vector3(88,34,68),true,1,2),
        new ZoneSpec("returns","SERVICE RETURNS",new Vector3(240,0,100),new Vector3(64,12,60),false,0,2)
    };
    public static ZoneSpec Find(string id)
    {
        foreach(var spec in Specs) if(spec.id==id)return spec;
        throw new ArgumentException("Unknown outer district: "+id);
    }
    public static Vector3 DoorDirection(int side) => Quaternion.Euler(0,side*90,0)*Vector3.forward;
    public static Vector3 DoorPoint(ZoneSpec spec,int side)
        => spec.position+DoorDirection(side)*(side%2==0?spec.size.z:spec.size.x)*.5f;
    public static Vector3 DoorPoint(string id,int side) => DoorPoint(Find(id),side);

    public static Transform[] Install(Transform root,string gen,bool quest)
    {
        Directory.CreateDirectory(gen+"/Architecture");
        var result=new Transform[Specs.Length];
        for(int i=0;i<Specs.Length;i++){
            var spec=Specs[i];
            var room=WorldAuthor.Child(root,spec.id);room.localPosition=spec.position;
            result[i]=room;
            var shell=new StationMesh(room,gen+"/Architecture");
            if(spec.id=="causeway") Causeway(shell,room,gen,quest);
            else {
                Shell(shell,spec,quest);
                if(spec.id=="billboard") AdAlley(shell,room,quest);
                if(spec.id=="concourse") Concourse(shell,room,quest);
                if(spec.id=="stacks") Stacks(shell,room,quest);
                if(spec.id=="returns") Returns(shell,room,quest);
            }
            shell.Finish();
            SharedFurnishings(room,spec.id,gen,quest);
            Signs(room,spec);
        }
        return result;
    }

    static void Shell(StationMesh m,ZoneSpec spec,bool quest)
    {
        // Match the original district floor contract: sixteen centimetres beyond
        // each nominal doorway plane. Connector render surfaces stop at this edge.
        m.Box(spec.id=="returns"?"terrazzo":"wet",0,-.24f,0,spec.size.x+.32f,.48f,spec.size.z+.32f,true);
        for(int side=0;side<4;side++){
            float width=side%2==0?spec.size.x:spec.size.z;
            float depth=(side%2==0?spec.size.z:spec.size.x)*.5f;
            m.Frame=Quaternion.Euler(0,side*90,0);m.Origin=m.Frame*new Vector3(0,0,depth);
            Wall(m,width,spec.size.y,spec.HasDoor(side),spec.id=="billboard"?"brickwork":"limestone");
            if(spec.id=="billboard") Facade(m,width,spec.size.y,spec.HasDoor(side),quest);
            else for(float x=-width*.5f+3;x<width*.5f;x+=quest?9.0f:6.0f){
                if(spec.HasDoor(side)&&Mathf.Abs(x)<6)continue;
                m.Box("metal",x,1.4f,-.25f,.07f,2.6f,.06f);
                m.Box("teal",x,2.92f,-.25f,3.8f,.10f,.10f);
            }
            m.ResetFrame();
        }
        if(!spec.open){
            m.Box("concrete",0,spec.size.y+.23f,0,spec.size.x+.5f,.46f,spec.size.z+.5f,true);
            for(float z=-spec.size.z*.5f+6;z<spec.size.z*.5f;z+=12){
                m.Box("metal",0,spec.size.y-.25f,z,spec.size.x,.46f,.38f);
                foreach(float x in new[]{-20f,0f,20f})m.Box("ice",x,spec.size.y-.52f,z,6,.07f,.34f);
            }
        }
    }

    static void Wall(StationMesh m,float width,float height,bool door,string finish)
    {
        const float opening=9,doorHeight=6;
        if(door){
            float wing=(width-opening)*.5f;
            for(int sign=-1;sign<=1;sign+=2){
                float x=sign*(opening*.5f+wing*.5f);
                m.Box(finish,x,height*.5f,.25f,wing,height,.5f,true);
                m.Box("tile",x,1.08f,-.10f,wing,2.16f,.20f);
                // The end caps and jamb fronts are deliberately separate planes.
                m.Box("metal",sign*4.56f,3,0,.22f,6,.82f);
                m.Box("amber",sign*4.56f,2.1f,-.45f,.065f,2.6f,.045f);
            }
            m.Box(finish,0,(height+doorHeight)*.5f,.25f,opening,height-doorHeight,.5f,true);
            m.Box("metal",0,doorHeight-.10f,-.05f,9.38f,.20f,.82f);
        }else{
            m.Box(finish,0,height*.5f,.25f,width,height,.5f,true);
            m.Box("tile",0,1.08f,-.10f,width,2.16f,.20f);
        }
        m.Box("metal",0,height-.20f,-.10f,width,.22f,.72f);
    }

    static void Facade(StationMesh m,float width,float height,bool opening,bool quest)
    {
        float interval=quest?7.0f:4.6f;
        for(float x=-width*.5f+3.5f;x<width*.5f-2;x+=interval){
            float start=opening&&Mathf.Abs(x)<6?8.1f:4.2f;
            for(float y=start;y<height-1.5f;y+=3.6f){
                m.Box("black",x,y,-.14f,3.1f,2.2f,.18f);
                m.Box(((Mathf.RoundToInt(x*3+y))%7==0)?"amber":"glass",x,y+.06f,-.25f,2.7f,1.83f,.04f);
                if(!quest){
                    m.Box("metal",x,y+.06f,-.30f,.08f,1.87f,.06f);
                    m.Box("concrete",x,y-1.08f,-.36f,3.28f,.17f,.57f);
                }
            }
        }
    }

    static void AdAlley(StationMesh m,Transform room,bool quest)
    {
        // The advertising structure is installed separately at (23,10,8).
        // Reserve the entire central crossing and a broad paved space below it.
        for(int side=-1;side<=1;side+=2){
            for(int row=0;row<3;row++){
                float z=-25+row*24;
                Lamp(m,room,new Vector3(side*14,0,z),side,quest);
            }
            for(int row=0;row<7;row++){
                float z=-28+row*9.1f;
                if(Mathf.Abs(z)<8)continue;
                m.Box("brass",side*8.7f,.03f,z,.10f,.06f,3.6f);
                m.Box("black",side*11.9f,.035f,z,.48f,.07f,2.4f);
                if(!quest)for(int bar=0;bar<9;bar++)m.Box("metal",side*11.9f,.085f,z-1.05f+bar*.26f,.46f,.025f,.07f);
            }
        }
        // Empty storefront canopy and seating all sit along the left pavement.
        m.Box("teal",-29,3.8f,20,15,.28f,6.5f);
        foreach(float x in new[]{-35.5f,-22.5f})m.Box("metal",x,1.83f,22.5f,.18f,3.66f,.18f,true);
        for(int i=0;i<3;i++)Bench(m,new Vector3(-33+i*4,0,21),0);
        m.Box("black",-29,2.1f,23.45f,13,2.5f,.22f);
        Label(room,"LATE SERVICE",new Vector3(-29,3.22f,23.29f),Quaternion.identity,.25f,new Color(.95f,.76f,.48f));
        foreach(float z in new[]{-22f,24f}){
            m.Box("metal",33,4.6f,z,8.5f,.32f,5.4f);
            m.Box("ice",33,4.39f,z-2.35f,7.4f,.08f,.14f);
        }
        // Raised service grilles are inset from the street, never laid over it.
        for(int i=0;i<(quest?2:5);i++)m.Box("rust",-39.3f,4.6f+i*2.4f,-17,2.7f,1.7f,.27f);
    }

    static void Concourse(StationMesh m,Transform room,bool quest)
    {
        for(int row=0;row<5;row++){
            float z=-28+row*14;
            int segments=quest?12:28;
            for(int i=0;i<segments;i++){
                float a=i*Mathf.PI/segments,b=(i+1)*Mathf.PI/segments;
                Vector3 p=new Vector3(39*Mathf.Cos(a),7+14*Mathf.Sin(a),z);
                Vector3 q=new Vector3(39*Mathf.Cos(b),7+14*Mathf.Sin(b),z);
                m.Rod("ivory",p,q,quest?.24f:.17f,quest?4:6);
            }
            foreach(int side in new[]{-1,1}){
                m.Box("limestone",side*39,3.5f,z,.8f,7,.8f,true);
                m.Box("amber",side*38.53f,5.4f,z,.08f,2.7f,.45f);
            }
        }
        foreach(int side in new[]{-1,1}){
            for(int i=0;i<4;i++){
                float z=-26+i*17;
                m.Box("teal",side*28,.65f,z,9,1.3f,2.8f,true);
                m.Box("ivory",side*28,1.37f,z,9.4f,.14f,3.02f);
                if(!quest)for(int slat=0;slat<12;slat++)m.Box("brass",side*28-4+slat*.72f,.72f,z-1.43f,.028f,1.05f,.035f);
            }
            for(int i=0;i<3;i++)Bench(m,new Vector3(side*17,0,-21+i*21),side<0?90:-90);
        }
        // A narrow clerestory floats above a much lower, empty departure board.
        m.Box("metal",18,6.9f,32.5f,18.5f,2.6f,.47f);
        m.Box("black",18,6.9f,32.20f,17.9f,2.12f,.06f);
        Label(room,"NO DEPARTURES",new Vector3(18,6.9f,32.15f),Quaternion.identity,.36f,new Color(.92f,.72f,.40f));
        for(int i=0;i<4;i++)m.Box("ice",-33+i*22,18.1f,36.7f,13,1.1f,.15f);
    }

    static void Stacks(StationMesh m,Transform room,bool quest)
    {
        // Cabinet towers leave a ten-metre crossing and a continuous outer walk.
        for(int sx=-1;sx<=1;sx+=2)for(int sz=-1;sz<=1;sz+=2){
            float x=sx*23,z=sz*19;
            m.Box("metal",x,13,z,12,26,13,true);
            m.Box("concrete",x,26.35f,z,13.1f,.7f,14.1f);
            int rows=quest?9:16,columns=quest?3:5;
            for(int face=0;face<4;face++){
                m.Frame=Quaternion.Euler(0,face*90,0);m.Origin=new Vector3(x,0,z);
                float width=face%2==0?12:13;
                float edge=face%2==0?6.5f:6f;
                for(int row=0;row<rows;row++)for(int column=0;column<columns;column++){
                    float xx=-width*.5f+(column+.5f)*width/columns;
                    float yy=1.25f+(row+.5f)*23/rows;
                    m.Box((column+row)%7==0?"ivory":"teal",xx,yy,edge+.06f,width/columns-.13f,23f/rows-.13f,.12f);
                    if(!quest){
                        m.Box("brass",xx,yy-.12f,edge+.15f,.36f,.055f,.09f);
                        m.Box("paper",xx,yy+.22f,edge+.132f,.57f,.25f,.015f);
                    }
                }
                m.ResetFrame();
            }
        }
        foreach(int sign in new[]{-1,1}){
            m.Box("metal",sign*36,7,0,.28f,14,.28f,true);
            m.Box("amber",sign*36,14.03f,0,5.2f,.16f,1.1f);
            Bench(m,new Vector3(sign*35.5f,0,14),sign<0?90:-90);
        }
        // Familiar office storage appears inverted overhead, clear of all routes.
        m.Box("concrete",0,29,0,8,.6f,40);
        for(int i=0;i<(quest?7:15);i++){
            float z=-18+i*36f/(quest?6:14);
            m.Box("ivory",0,27.2f,z,5.9f,3.0f,1.55f);
            m.Box("metal",0,25.63f,z,6.1f,.14f,1.7f);
        }
        Label(room,"PLEASE RETURN TO THE SAME PLACE",new Vector3(0,8,-33.50f),Quaternion.Euler(0,180,0),.29f,new Color(.83f,.93f,.88f));
    }

    static void Returns(StationMesh m,Transform room,bool quest)
    {
        // Three parallel walks are linked by the two open ends and middle crossway.
        foreach(float x in new[]{-12f,12f})foreach(int sign in new[]{-1,1}){
            m.Box("limestone",x,4.25f,sign*17,1.2f,8.5f,16,true);
            m.Box("teal",x,1.1f,sign*17,1.3f,2.2f,16.1f);
            m.Box("amber",x,8.62f,sign*17,1.5f,.12f,16.4f);
        }
        for(int side=-1;side<=1;side+=2)for(int j=0;j<6;j++){
            float z=-24+j*9.6f;
            m.Box("metal",side*30.9f,1.35f,z,1.28f,2.7f,3.9f,true);
            m.Box("paper",side*30.20f,1.6f,z,.08f,1.3f,3.05f);
            if(!quest)for(int k=0;k<5;k++)m.Box("brass",side*30.13f,.7f+k*.32f,z,.08f,.035f,2.1f);
        }
        foreach(float x in new[]{-22f,0f,22f})for(int i=0;i<4;i++){
            float z=-22.5f+i*15;
            m.Box("black",x,11.34f,z,5,.18f,.62f);
            m.Box("ice",x,11.20f,z,4.6f,.08f,.37f);
        }
        Label(room,"ALL RETURNS ARE FINAL",new Vector3(0,3.2f,26.7f),Quaternion.identity,.26f,new Color(.90f,.82f,.64f));
    }

    static void Causeway(StationMesh m,Transform room,string gen,bool quest)
    {
        // The west branch butts against the main deck. Their top faces never overlap.
        m.Box("wet",0,-.65f,0,20,1.3f,148.32f,true);
        m.Box("metal",-45.08f,-.65f,0,70.16f,1.3f,20,true);
        m.Box("black",0,-2.2f,0,16,1.6f,148);
        m.Box("black",-45,-2.2f,0,70,1.6f,16);
        for(int side=-1;side<=1;side+=2){
            m.Frame=Quaternion.Euler(0,side<0?270:90,0);m.Origin=new Vector3(side*80,0,0);
            Wall(m,148,78,side<0,"concrete");
            for(float x=-68;x<72;x+=quest?17:8.5f){
                m.Box("limestone",x,39,-.48f,1.1f,78,1.1f);
                m.Box("amber",x,59,-1.10f,.14f,13,.12f);
                if(!quest)for(int band=0;band<5;band++)m.Box("black",x+2.7f,8+band*14,-.14f,3.3f,5.4f,.06f);
            }
            m.ResetFrame();
        }
        for(int side=-1;side<=1;side+=2){
            Rail(m,new Vector3(side*9.70f,0,-74),new Vector3(side*9.70f,0,-10.2f));
            Rail(m,new Vector3(side*9.70f,0,10.2f),new Vector3(side*9.70f,0,73.7f));
        }
        Rail(m,new Vector3(9.70f,0,-10.2f),new Vector3(9.70f,0,10.2f));
        Rail(m,new Vector3(-9.70f,0,73.7f),new Vector3(9.70f,0,73.7f));
        Rail(m,new Vector3(-80,0,-9.70f),new Vector3(-10,0,-9.70f));
        Rail(m,new Vector3(-80,0,9.70f),new Vector3(-10,0,9.70f));
        // An entry lintel, two giant walls and the orbital silhouette establish scale.
        foreach(int side in new[]{-1,1})m.Box("metal",side*6.6f,8.5f,-73.7f,.7f,17,.7f,true);
        m.Box("metal",0,17,-73.7f,14,.7f,1.1f);
        for(int i=0;i<10;i++){
            float z=-67+i*14.8f;
            m.Box("brass",0,.028f,z,.13f,.056f,4.7f);
            foreach(int side in new[]{-1,1}){
                m.Box("black",side*8.7f,.06f,z,.55f,.12f,2.6f);
                m.Box("ice",side*8.7f,.135f,z,.20f,.03f,1.4f);
            }
            if(!quest){
                m.Rod("rust",new Vector3(-8,-2.3f,z),new Vector3(8,-2.3f,z),.32f,6);
                m.Rod("metal",new Vector3(-8,-2.3f,z),new Vector3(8,-2.3f,z+6),.15f,6);
            }
        }
        Label(room,"ORBITAL TRANSFER  /  NO ARRIVALS",new Vector3(0,15.85f,-74.30f),Quaternion.identity,.30f,new Color(.82f,.93f,1f));
        BuildStation(room,gen,quest);
    }

    static void Rail(StationMesh m,Vector3 a,Vector3 b)
    {
        var middle=(a+b)*.5f;var size=new Vector3(Mathf.Abs(b.x-a.x)+.12f,1.32f,Mathf.Abs(b.z-a.z)+.12f);
        m.Solid(middle+Vector3.up*.66f,size,Quaternion.identity,"Causeway guardrail");
        m.Rod("ivory",a+Vector3.up*1.32f,b+Vector3.up*1.32f,.065f,6);
        m.Rod("metal",a+Vector3.up*.45f,b+Vector3.up*.45f,.055f,5);
        int count=Mathf.CeilToInt(Vector3.Distance(a,b)/3.2f);
        for(int i=0;i<=count;i++){
            var p=Vector3.Lerp(a,b,(float)i/count);
            m.Rod("metal",p,p+Vector3.up*1.35f,.075f,6);
        }
    }

    static void Bench(StationMesh m,Vector3 position,float heading)
    {
        m.Origin=position;m.Frame=Quaternion.Euler(0,heading,0);
        m.Box("ivory",0,.52f,0,3.4f,.16f,.82f,true);
        m.Box("teal",0,.97f,.39f,3.4f,.82f,.12f);
        foreach(int side in new[]{-1,1}){
            m.Box("metal",side*1.3f,.26f,0,.13f,.52f,.58f);
            m.Rod("metal",new Vector3(side*1.46f,.66f,-.32f),new Vector3(side*1.46f,.66f,.33f),.05f,6);
        }
        m.ResetFrame();
    }

    static void Lamp(StationMesh m,Transform parent,Vector3 position,int side,bool quest)
    {
        m.Origin=position;
        m.Box("black",0,.12f,0,.78f,.24f,.78f,true);
        m.Rod("metal",new Vector3(0,.2f,0),new Vector3(0,6.8f,0),.105f,quest?5:8);
        m.Box("metal",-side*1.25f,6.74f,0,2.6f,.15f,.24f);
        m.Box("amber",-side*2.4f,6.56f,0,1.18f,.12f,.48f);
        m.Box("black",-side*1.26f,4.7f,0,2.9f,1.06f,.16f);
        m.Box("pink",-side*1.26f,4.15f,-.10f,2.86f,.065f,.065f);
        Label(parent,"uwu",position+new Vector3(-side*1.26f,4.73f,-.095f),Quaternion.identity,.52f,new Color(1,.15f,.54f));
        m.ResetFrame();
    }

    static string Destination(string id,int side)
    {
        if(id=="billboard")return side==3?"AIR TERMINAL":side==1?"LAST CONCOURSE":"SERVICE RETURNS";
        if(id=="concourse")return side==3?"AD ALLEY":"ORBITAL CAUSEWAY";
        if(id=="causeway")return side==2?"LAST CONCOURSE":"QUIET STACKS";
        if(id=="stacks")return side==1?"ORBITAL CAUSEWAY":"SERVICE RETURNS";
        return side==0?"QUIET STACKS":"AD ALLEY";
    }
    static void Signs(Transform room,ZoneSpec spec)
    {
        foreach(int side in spec.doors){
            var direction=DoorDirection(side);
            float edge=(side%2==0?spec.size.z:spec.size.x)*.5f;
            var pose=Quaternion.Euler(0,side*90,0);
            Label(room,Destination(spec.id,side),direction*(edge-.58f)+Vector3.up*7.05f,pose,.30f,new Color(.91f,.87f,.70f));
        }
    }
    static void Label(Transform parent,string text,Vector3 position,Quaternion rotation,float size,Color color)
    {
        var t=WorldAuthor.Child(parent,"Plate / "+text);t.localPosition=position;t.localRotation=rotation;
        var font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var label=t.gameObject.AddComponent<TextMesh>();
        label.text=text;label.font=font;label.fontSize=72;label.characterSize=size;
        label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;label.color=color;
        var renderer=t.GetComponent<MeshRenderer>();renderer.sharedMaterial=font.material;
        renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
    }

    const string SharedAssets=WorldAuthor.Root+"/ThirdParty/KenneyStation/";
    static void SharedFurnishings(Transform room,string id,string gen,bool quest)
    {
        var meshes=new List<CombineInstance>();
        var simpleCollision=WorldAuthor.Child(room,"Furnishing collision");
        if(id=="concourse")foreach(int side in new[]{-1,1})for(int i=0;i<4;i++){
            float z=-26+i*17;
            AddShared(meshes,room,"computer-wide",new Vector3(side*28,1.44f,z+.10f),0,.78f);
            AddShared(meshes,room,"chair-armrest-headrest",new Vector3(side*28,0,z+2.6f),180,1.10f);
            FurnitureBox(simpleCollision,new Vector3(side*28,.45f,z+2.6f),new Vector3(.70f,.90f,.62f));
        }
        if(id=="returns" || id=="stacks")foreach(int side in new[]{-1,1})for(int i=0;i<2;i++){
            var place=new Vector3(side*(id=="returns"?26.0f:37.6f),0,-18+i*36);
            AddShared(meshes,room,"container-wide",place,side*23,1.35f);
            FurnitureBox(simpleCollision,place+Vector3.up*.675f,new Vector3(1.55f,1.35f,1.55f));
        }
        if(id=="billboard"){
            AddShared(meshes,room,"container-wide",new Vector3(-37.6f,0,26.8f),17,1.35f);
            FurnitureBox(simpleCollision,new Vector3(-37.6f,.675f,26.8f),new Vector3(1.55f,1.35f,1.55f));
        }
        if(id=="causeway")for(int i=0;i<(quest?2:7);i++)
            AddShared(meshes,room,"pipe-bend",new Vector3(-7.6f,-3.7f,-55+i*15.5f),90,1.65f);
        else for(int side=-1;side<=1;side+=2){
            var spec=Find(id);
            for(int i=0;i<(quest?1:3);i++){
                float z=-spec.size.z*.32f+i*spec.size.z*.32f;
                AddShared(meshes,room,"wall-switch",new Vector3(side*(spec.size.x*.5f-.42f),1.5f,z),side*90,.34f);
            }
        }
        if(meshes.Count==0)return;
        string path=gen+"/Architecture/"+id+" restored furnishings.asset";
        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);bool create=!mesh;if(create)mesh=new Mesh();
        int totalVertices=0;foreach(var instance in meshes)totalVertices+=instance.mesh.vertexCount;
        mesh.Clear();mesh.name=id+" restored furnishings";mesh.indexFormat=totalVertices>65535?IndexFormat.UInt32:IndexFormat.UInt16;
        mesh.CombineMeshes(meshes.ToArray(),true,true);mesh.RecalculateBounds();
        if(create)AssetDatabase.CreateAsset(mesh,path);else EditorUtility.SetDirty(mesh);
        string materialPath=gen+"/Finishes/Restored station furnishings.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if(!material){material=new Material(Surface.Get("metal"));AssetDatabase.CreateAsset(material,materialPath);}
        material.CopyPropertiesFromMaterial(Surface.Get("metal"));material.EnableKeyword("SW_PRINT");
        material.SetTexture("_MainTex",AssetDatabase.LoadAssetAtPath<Texture>(SharedAssets+"colormap.png"));
        material.SetColor("_Color",new Color(.78f,.82f,.85f));material.SetFloat("_Sheen",.24f);
        EditorUtility.SetDirty(material);
        var owner=WorldAuthor.Child(room,"Restored station furnishings").gameObject;
        owner.AddComponent<MeshFilter>().sharedMesh=mesh;
        var renderer=owner.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;
        GameObjectUtility.SetStaticEditorFlags(owner,StaticEditorFlags.OccludeeStatic);
    }
    static void AddShared(List<CombineInstance> output,Transform room,string name,Vector3 position,float heading,float height)
    {
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>(SharedAssets+name+".fbx");
        if(!asset)throw new InvalidOperationException("Missing selected Kenney asset: "+name);
        var temporary=UnityEngine.Object.Instantiate(asset);temporary.hideFlags=HideFlags.HideAndDontSave;
        temporary.transform.SetParent(room,false);temporary.transform.localPosition=position;
        temporary.transform.localRotation=Quaternion.Euler(0,heading,0);
        try{
            var renderers=temporary.GetComponentsInChildren<MeshRenderer>();
            if(renderers.Length==0)throw new InvalidOperationException("Selected model has no mesh: "+name);
            var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            if(bounds.size.y<.001f)throw new InvalidOperationException("Selected model has invalid scale: "+name);
            temporary.transform.localScale*=height/bounds.size.y;
            bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            temporary.transform.position+=room.TransformPoint(position)-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            foreach(var filter in temporary.GetComponentsInChildren<MeshFilter>()){
                for(int submesh=0;submesh<filter.sharedMesh.subMeshCount;submesh++)
                    output.Add(new CombineInstance {mesh=filter.sharedMesh,subMeshIndex=submesh,transform=room.worldToLocalMatrix*filter.transform.localToWorldMatrix});
            }
        }finally{UnityEngine.Object.DestroyImmediate(temporary);}
    }
    static void FurnitureBox(Transform root,Vector3 center,Vector3 size)
    {
        var box=WorldAuthor.Child(root,"Shared furnishing");box.localPosition=center;box.gameObject.AddComponent<BoxCollider>().size=size;
    }

    static void BuildStation(Transform room,string gen,bool quest)
    {
        var station=WorldAuthor.Child(room,"Asterion orbital station");
        station.localPosition=new Vector3(40,55,22);station.localRotation=Quaternion.Euler(0,-18,-8);
        var lods=new List<LOD>();
        int count=quest?1:2;
        for(int detail=0;detail<count;detail++){
            var level=WorldAuthor.Child(station,"Detail "+detail);
            int slices=quest?28:detail==0?64:36;
            int bands=quest?14:detail==0?32:18;
            var body=new OrbitalMesh();
            for(int row=0;row<bands;row++)for(int column=0;column<slices;column++){
                float a=(column+.018f)*Mathf.PI*2/slices,b=(column+.982f)*Mathf.PI*2/slices;
                float low=-Mathf.PI*.5f+(row+.018f)*Mathf.PI/bands;
                float high=-Mathf.PI*.5f+(row+.982f)*Mathf.PI/bands;
                float tone=.77f+.23f*Hash(row,column);
                float lift=.04f+.13f*Hash(column+37,row+19);
                body.Patch(a,b,low,high,35+lift,new Color(tone,tone,tone,1));
            }
            body.Save(level,gen+"/Architecture","Orbital hull "+detail,OrbitalMaterial(gen,quest));
            var under=new LandmarkGeometry();
            Sphere(under,"black",34.78f,quest?24:48,quest?12:24);
            var parts=new LandmarkGeometry();
            int segments=quest?36:detail==0?112:64;
            Ring(parts,"black",Vector3.zero,35.65f,1.3f,segments);
            Ring(parts,"metal",new Vector3(0,-1.34f,0),35.65f,.28f,segments);
            Ring(parts,"ivory",new Vector3(0,1.34f,0),35.65f,.21f,segments);
            int greebles=quest?48:detail==0?520:150;
            for(int i=0;i<greebles;i++){
                float longitude=Hash(i,91)*Mathf.PI*2;
                float latitude=Mathf.Asin((Hash(i,47)*2-1)*.94f);
                Vector3 normal=Direction(longitude,latitude);
                parts.frame=Matrix4x4.TRS(normal*35.18f,Quaternion.FromToRotation(Vector3.up,normal),Vector3.one);
                float width=.32f+Hash(i,5)*(quest?1.3f:2.2f);
                parts.Box(i%7==0?"ivory":i%3==0?"black":"metal",new Vector3(0,.18f,0),new Vector3(width,.20f+Hash(i,13)*.7f,.32f+Hash(i,7)*2.6f));
                if(!quest && detail==0 && i%3==0)parts.Box("black",new Vector3(.11f,.65f,.07f),new Vector3(width*.52f,.08f,1.1f));
            }
            parts.frame=Matrix4x4.identity;
            // Two recessed service arrays replace the franchise's singular weapon dish.
            foreach(float longitude in new[]{3.65f,5.15f}){
                Vector3 normal=Direction(longitude,.42f);
                parts.frame=Matrix4x4.TRS(normal*34.8f,Quaternion.FromToRotation(Vector3.up,normal),Vector3.one);
                parts.Tube("metal",Vector3.zero,Vector3.up*.85f,5.3f,5.3f,quest?16:36);
                parts.Tube("black",Vector3.up*.88f,Vector3.up*.95f,4.72f,4.72f,quest?16:36);
                parts.Tube("teal",Vector3.up*.98f,Vector3.up*1.03f,2.9f,2.9f,quest?12:32);
                if(!quest)for(int ray=0;ray<10;ray++){
                    float angle=ray*Mathf.PI*.2f;
                    Vector3 p=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                    parts.Beam("ivory",p*1.1f+Vector3.up*1.13f,p*4.6f+Vector3.up*1.13f,.08f,.08f);
                }
            }
            parts.frame=Matrix4x4.identity;
            // Crown communications decks and lower radiator fins form a varied silhouette.
            parts.Box("black",new Vector3(0,35,0),new Vector3(9,3,7));
            parts.Box("metal",new Vector3(0,37,0),new Vector3(6,1,5));
            foreach(float x in new[]{-2.2f,1.8f}){
                parts.Tube("ivory",new Vector3(x,37,0),new Vector3(x,48+x,0),.30f,.12f,quest?5:8);
                parts.Beam("metal",new Vector3(x-2,42,0),new Vector3(x+2,42,0),.12f,.12f);
            }
            for(int fin=0;fin<(quest?4:9);fin++){
                float z=-8+fin*16f/(quest?3:8);
                parts.Box("black",new Vector3(0,-35.5f,z),new Vector3(16,7,.23f));
                parts.Box("metal",new Vector3(0,-39.1f,z),new Vector3(16.6f,.25f,.42f));
            }
            var windows=new LandmarkGeometry();
            int lights=quest?40:detail==0?240:100;
            for(int j=0;j<lights;j++){
                float angle=j*2*Mathf.PI/lights;
                var normal=Direction(angle,0);
                windows.frame=Matrix4x4.TRS(normal*35.82f,Quaternion.FromToRotation(Vector3.up,normal),Vector3.one);
                windows.Box(j%9==0?"ice":"amber",Vector3.zero,new Vector3(quest?.35f:.19f,.05f,.65f));
            }
            windows.frame=Matrix4x4.identity;
            under.Save(level,gen+"/Architecture","Orbital backing "+detail);
            parts.Save(level,gen+"/Architecture","Orbital servicing "+detail);
            windows.Save(level,gen+"/Architecture","Orbital windows "+detail);
            var renderers=level.GetComponentsInChildren<Renderer>();
            foreach(var renderer in renderers)GameObjectUtility.SetStaticEditorFlags(renderer.gameObject,StaticEditorFlags.OccludeeStatic);
            lods.Add(new LOD(quest?.005f:detail==0?.26f:.005f,renderers));
        }
        var group=station.gameObject.AddComponent<LODGroup>();group.SetLODs(lods.ToArray());group.RecalculateBounds();
        group.fadeMode=LODFadeMode.None;
    }

    static Material OrbitalMaterial(string gen,bool quest)
    {
        string path=gen+"/Finishes/Orbital hull.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader=Shader.Find("Liminal uwu/Orbital hull");
        if(!shader)throw new InvalidOperationException("Orbital hull shader is missing.");
        if(!material){material=new Material(shader);AssetDatabase.CreateAsset(material,path);}
        material.shader=shader;
        material.SetTexture("_MainTex",AssetDatabase.LoadAssetAtPath<Texture>(Surface.Media+"/Surfaces/Enamel.jpg"));
        material.SetColor("_Color",new Color(.53f,.61f,.67f));
        if(quest)material.EnableKeyword("ORBITAL_LITE");else material.DisableKeyword("ORBITAL_LITE");
        material.enableInstancing=true;EditorUtility.SetDirty(material);return material;
    }
    static float Hash(int a,int b)
    { uint n=unchecked((uint)(a*73856093^b*19349663));n^=n>>13;n*=1274126177;return (n&65535)/65535f; }
    static Vector3 Direction(float longitude,float latitude)
        => new Vector3(Mathf.Cos(latitude)*Mathf.Cos(longitude),Mathf.Sin(latitude),Mathf.Cos(latitude)*Mathf.Sin(longitude));
    static void Sphere(LandmarkGeometry mesh,string material,float radius,int slices,int rings)
    {
        for(int y=0;y<rings;y++)for(int x=0;x<slices;x++){
            float a=x*2*Mathf.PI/slices,b=(x+1)*2*Mathf.PI/slices;
            float c=-Mathf.PI*.5f+y*Mathf.PI/rings,d=-Mathf.PI*.5f+(y+1)*Mathf.PI/rings;
            mesh.Quad(material,Direction(a,c)*radius,Direction(a,d)*radius,Direction(b,d)*radius,Direction(b,c)*radius);
        }
    }
    static void Ring(LandmarkGeometry mesh,string material,Vector3 center,float radius,float thick,int sides)
    {
        for(int j=0;j<sides;j++){
            float a=j*2*Mathf.PI/sides,b=(j+1)*2*Mathf.PI/sides;
            mesh.Beam(material,center+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*radius,
                center+new Vector3(Mathf.Cos(b),0,Mathf.Sin(b))*radius,thick,thick);
        }
    }
    sealed class OrbitalMesh
    {
        readonly List<Vector3> vertices=new List<Vector3>(),normals=new List<Vector3>();
        readonly List<Vector2> uv=new List<Vector2>();
        readonly List<Color> colors=new List<Color>();
        readonly List<int> triangles=new List<int>();
        public void Patch(float a,float b,float low,float high,float radius,Color color)
        {
            int first=vertices.Count;
            foreach(var p in new[]{new Vector2(a,low),new Vector2(a,high),new Vector2(b,high),new Vector2(b,low)}){
                var normal=Direction(p.x,p.y);vertices.Add(normal*radius);normals.Add(normal);
                uv.Add(new Vector2(p.x/Mathf.PI*.5f,p.y/Mathf.PI+.5f));colors.Add(color);
            }
            triangles.Add(first);triangles.Add(first+1);triangles.Add(first+2);
            triangles.Add(first);triangles.Add(first+2);triangles.Add(first+3);
        }
        public void Save(Transform parent,string folder,string name,Material material)
        {
            string path=folder+"/"+name+".asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);bool create=!mesh;if(create)mesh=new Mesh();
            mesh.Clear();mesh.name=name;mesh.indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16;
            mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
            if(create)AssetDatabase.CreateAsset(mesh,path);else EditorUtility.SetDirty(mesh);
            var go=WorldAuthor.Child(parent,name).gameObject;go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;
        }
    }
}

internal sealed class SelectedStationAssetImporter : AssetPostprocessor
{
    const string Prefix="Assets/Stillwater/ThirdParty/KenneyStation/";
    void OnPreprocessModel()
    {
        if(!assetPath.StartsWith(Prefix,StringComparison.Ordinal))return;
        var model=(ModelImporter)assetImporter;
        model.materialImportMode=ModelImporterMaterialImportMode.None;
        model.importAnimation=false;model.importCameras=false;model.importLights=false;
        model.isReadable=true;model.addCollider=false;model.globalScale=1;
        model.meshCompression=ModelImporterMeshCompression.Off;
    }
    void OnPreprocessTexture()
    {
        if(assetPath!=Prefix+"colormap.png")return;
        var texture=(TextureImporter)assetImporter;
        texture.textureType=TextureImporterType.Default;texture.sRGBTexture=true;
        texture.alphaSource=TextureImporterAlphaSource.None;texture.mipmapEnabled=false;
        texture.wrapMode=TextureWrapMode.Clamp;texture.filterMode=FilterMode.Bilinear;
        texture.maxTextureSize=512;texture.textureCompression=TextureImporterCompression.Uncompressed;
        var mobile=texture.GetPlatformTextureSettings("Android");mobile.name="Android";
        mobile.overridden=true;mobile.maxTextureSize=256;mobile.format=TextureImporterFormat.ASTC_6x6;
        texture.SetPlatformTextureSettings(mobile);
    }
}
