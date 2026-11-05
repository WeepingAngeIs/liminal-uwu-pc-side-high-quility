using System.IO;
using UnityEditor;
using UnityEngine;

// Street-facing surfaces are authored separately from the interiors they conceal.
internal static class NightArchitecture
{
    static readonly Rect Full=new Rect(0,0,1,1);
    static void Place(StationMesh mesh,Vector3 position,float heading)
    {mesh.Origin=position;mesh.Frame=Quaternion.Euler(0,heading,0);}
    public static void Add(StationMesh m,string id,string folder, Material stationPlates,bool mobile)
    {
        if(id=="hub")Court(m,folder,mobile);
        if(id=="rail")RailElevation(m,stationPlates,mobile);
        if(id=="observatory")Observatory(m,mobile);
        m.ResetFrame();
    }
    static Material LightPool(string folder,string name,Color c)
    {
        string path=folder+"/"+name+".mat";
        var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!mat){mat=new Material(Shader.Find("Liminal uwu/Practical glow"));AssetDatabase.CreateAsset(mat,path);}
        mat.SetColor("_Color",c);EditorUtility.SetDirty(mat);return mat;
    }
    static void Court(StationMesh m,string folder,bool mobile)
    {
        var pink=LightPool(folder,"Rose phosphor",new Color(.99f,.16f,.55f,.39f));
        var gold=LightPool(folder,"Sodium pool",new Color(.97f,.49f,.16f,.22f));
        var blue=LightPool(folder,"Blue pool",new Color(.19f,.54f,.9f,.24f));
        for(int i=0;i<3;i++)foreach(int side in new[]{-1,1}){
            int z=-16+i*15;float x=side*9;
            foreach(int face in new[]{-1,1}){
                Place(m,new Vector3(x-side*1.43f,4.63f,z+face*.19f),face<0?0:180);
                m.Print(pink,0,0,0,3.06f,1.23f,Full);
            }
            m.Origin=new Vector3(x-side*1.82f,.038f,z);
            m.Frame=Quaternion.Euler(90,0,0);
            m.Print(i%2==0?gold:blue,0,0,0,8.3f,10.5f,Full);
        }
        // Road drains, recessed service covers, broken lane paint and platform edging.
        m.ResetFrame();
        for(int side=-1;side<=1;side+=2){
            m.Box("metal",side*10.96f,.025f,0,.27f,.045f,60);
            for(int k=0;k<9;k++){
                float z=-26+k*6.3f;
                m.Box("black",side*10.64f,.045f,z,.42f,.04f,1.28f);
                for(int j=0;j<7;j++)m.Box("metal",side*10.64f,.072f,z-.55f+j*.178f,.41f,.023f,.064f);
            }
            for(int k=0;k<8;k++)m.Box("brass",side*5.9f,.018f,-27+k*7.7f,.075f,.026f,2.16f);
            for(int k=0;k<3;k++){
                float z=-20+k*14;
                m.Rod("metal",new Vector3(side*11.6f,.04f,z),new Vector3(side*11.6f,.92f,z),.118f,8);
                m.Box("brass",side*11.6f,.80f,z,.24f,.095f,.24f);
            }
        }
        // Clock tower with a warm clockface visible from the south arrival steps.
        Place(m,new Vector3(-20,0,27.5f),0);
        m.Box("teal",0,6.2f,0,4.1f,12.4f,2.1f);
        m.Box("brass",0,12.5f,0,4.45f,.25f,2.43f);
        m.Box("metal",0,10.03f,-1.24f,3.26f,3.26f,.32f);
        m.Rod("ivory",new Vector3(0,10.03f,-1.36f),new Vector3(0,10.03f,-1.49f),1.48f,40);
        m.Box("black",0,10.03f,-1.43f,2.88f,2.88f,.12f);
        for(int j=0;j<60;j+=mobile?5:1){
            float a=j*6*Mathf.Deg2Rad;var v=new Vector3(Mathf.Sin(a),Mathf.Cos(a),0);
            var p=new Vector3(0,10.03f,-1.51f);m.Rod(j%5==0?"amber":"brass",p+v*(j%5==0?1.03f:1.20f),p+v*1.32f,j%5==0?.042f:.021f,5);
        }
        var center=new Vector3(0,10.03f,-1.55f);
        m.Rod("ivory",center,center+new Vector3(.84f,-.48f,0),.065f,8);
        m.Rod("ice",center,center+new Vector3(.66f,1.03f,0),.032f,8);
        // Two street canopies add a human-scale foreground beneath the oversized facades.
        for(int k=0;k<2;k++){
            Place(m,new Vector3(k==0?23:-23.8f,0,k==0?-9:-14.5f),k==0?-90:90);
            m.Box("tile",0,.13f,0,8.6f,.26f,3.5f,true);
            for(int s=-1;s<=1;s+=2){
                m.Box("metal",s*3.7f,1.61f,.75f,.14f,2.96f,.19f,true);
                m.Box("metal",s*3.7f,2.92f,-.28f,.13f,.15f,2.1f);
            }
            m.Box("teal",0,3.1f,-.1f,8.84f,.22f,3.02f);
            m.Box("amber",0,2.95f,-1.44f,7.98f,.065f,.07f);
            m.Box("black",0,2.02f,1.06f,7.36f,1.72f,.2f);
            for(int j=0;j<11;j++)
                m.Box(j%3==0?"brass":"metal",-3.27f+j*.65f,2.02f,.91f,.037f,1.63f,.045f);
            for(int j=0;j<3;j++){
                m.Box("ivory",-2.64f+j*2.62f,.67f,.12f,1.94f,.13f,.70f);
                m.Box("metal",-2.64f+j*2.62f,.43f,.13f,.35f,.42f,.41f,true);
            }
        }
        m.ResetFrame();
    }
    static void RailElevation(StationMesh m,Material plates,bool mobile)
    {
        // From the court the outside of the rail hall reads as an old municipal power station.
        // Geometry points south (-Z); the hall's actual south wall lies at z=-34.23.
        Place(m,new Vector3(0,0,-34.46f),0);
        m.Box("brickwork",0,28,0,65,33,.13f);
        for(int j=-3;j<=3;j++){
            float x=j*8.9f;
            m.Box("rust",x,22.2f,-.13f,.71f,43.7f,.88f);
            m.Box("metal",x,23.3f,-.63f,.20f,41.0f,.17f);
            if(j<3){
                float mid=x+4.45f;
                for(int k=0;k<3;k++){
                    float y=17.2f+k*8.4f;
                    m.Box("metal",mid,y,-.14f,7.7f,5.7f,.5f);
                    m.Box("glass",mid,y,-.42f,7.32f,5.33f,.12f);
                    for(int t=-2;t<=2;t++)
                        m.Box("metal",mid+t*1.48f,y,-.56f,.13f,5.44f,.15f);
                    for(int t=-1;t<=1;t++)m.Box("metal",mid,y+t*1.58f,-.55f,7.4f,.16f,.12f);
                    m.Box("rust",mid,y-2.77f,-.57f,7.95f,.30f,.72f);
                }
            }
        }
        for(int i=0;i<2;i++){
            float y=21+i*16.8f;
            m.Box("metal",0,y,-1.04f,64,.36f,1.65f);
            m.Box("brass",0,y+1.17f,-1.76f,64,.078f,.10f);
            for(int j=-15;j<=15;j++)m.Box("metal",j*2.08f,y+.59f,-1.76f,.085f,1.12f,.085f);
        }
        // Glazed transom, platform header and a pipe ladder break the street-level mass.
        m.Box("metal",0,22.88f,-2.19f,24.2f,6.43f,.48f);
        m.Print(plates,0,22.88f,-2.45f,23.6f,5.9f,new Rect(.501f,.876f,.498f,.123f));
        for(int s=-1;s<=1;s+=2){
            for(int j=0;j<2;j++)m.Rod("rust",new Vector3(s*(27.4f+j*.7f),4.1f,-1.26f),
                new Vector3(s*(27.4f+j*.7f),42.8f,-1.26f),.19f,8);
            for(int j=0;j<39;j++)m.Box("metal",s*27.75f,4.6f+j*.98f,-1.49f,.99f,.086f,.09f);
        }
        m.ResetFrame();
    }
    static void Observatory(StationMesh m,bool mobile)
    {
        // Transit-survey brass inlays lead to a human-scale viewing point in the open court.
        Vector3 hub=new Vector3(-8,.025f,2);
        for(int j=0;j<48;j++){
            float a=j*Mathf.PI/24,b=(j+1)*Mathf.PI/24;
            var q=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var r=new Vector3(Mathf.Cos(b),0,Mathf.Sin(b));
            m.Rod("brass",hub+q*7.7f,hub+r*7.7f,.033f,4);
            if(j%2==0)m.Rod(j%6==0?"ivory":"brass",hub+q*7.65f,hub+q*(j%6==0?6.9f:7.32f),.026f,4);
        }
        for(int s=-1;s<=1;s+=2){
            Vector3 foot=hub+new Vector3(s*8.4f,0,-3.8f);
            m.Rod("metal",foot,foot+Vector3.up*1.4f,.14f,8);
            m.Rod("brass",foot+new Vector3(0,1.25f,-.27f),foot+new Vector3(0,1.80f,.61f),.14f,12);
            m.Rod("ice",foot+new Vector3(0,1.80f,.61f),foot+new Vector3(0,1.85f,.7f),.145f,12);
        }
    }
}
