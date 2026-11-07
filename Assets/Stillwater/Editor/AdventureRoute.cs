using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UdonSharpEditor;

internal static class AdventureRoute
{
    static readonly string[] From={"hub","hub","gallery","motor","service","gravity","grove","air","billboard","returns","stacks","concourse","causeway","waterfall","ballroom","formation","planet","lounge","observatory"};
    static readonly string[] To={"gallery","service","motor","gravity","gravity","grove","air","billboard","returns","stacks","concourse","causeway","waterfall","ballroom","formation","planet","lounge","observatory","rail"};
    internal static void Install(Transform root,Dictionary<string,Transform> rooms,string gen)
    {
        // Close the former grid at the room mouths. Their retained geometry is useful
        // for maintenance; it is no longer a visible shortcut past the adventure.
        foreach(var r in WorldAuthor.Places) {
            var t=WorldAuthor.Child(rooms[r.id],"Closed interchange "+r.id);
            var mesh=new StationMesh(t,gen+"/Architecture");
            for(int side=0;side<4;side++) {
                int dx=side==1?1:side==3?-1:0,dz=side==0?1:side==2?-1:0;
                bool gap=Array.Exists(WorldAuthor.Places,n=>n.x==r.x+dx && n.z==r.z+dz) || (r.id=="air"&&side==1);
                if(!gap)continue;
                mesh.Frame=Quaternion.Euler(0,90*side,0);
                mesh.Origin=mesh.Frame*new Vector3(0,0,(side%2==0?r.d:r.w)*.5f-.65f);
                Shutter(mesh,r.id=="service"?3.05f:5.6f);
            }
            mesh.ResetFrame();mesh.Finish();
        }
        foreach(var r in OuterDistricts.Specs) {
            var mesh=new StationMesh(WorldAuthor.Child(rooms[r.id],"Closed interchange "+r.id),gen+"/Architecture");
            foreach(int side in r.doors) {
                mesh.Frame=Quaternion.Euler(0,90*side,0);
                mesh.Origin=mesh.Frame*new Vector3(0,0,(side%2==0?r.size.z:r.size.x)*.5f-.65f);
                Shutter(mesh,6);
            }
            mesh.ResetFrame();mesh.Finish();
        }
        var system=WorldAuthor.Child(root,"Adventure progression").gameObject.AddUdonSharpComponent<AdventureJourney>();
        var entrances=new List<Transform>();var destinations=new List<Transform>();var sizes=new List<Vector3>();
        string materialPath=gen+"/Finishes/Winking doorway.mat";
        var mat=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if(!mat){mat=new Material(Shader.Find("Stillwater/Winking doorway"));AssetDatabase.CreateAsset(mat,materialPath);}
        for(int i=0;i<From.Length;i++) {
            var room=rooms[From[i]];float d=Depth(From[i]);
            var portal=WorldAuthor.Child(room,"Adventure door to "+To[i]);
            portal.localPosition=new Vector3(From[i]=="hub"?(i==0?-8:8):0,0,From[i]=="waterfall"?48:d*.5f-4);
            // Reception has a low ceiling. Keep its complete frame and label below
            // the ceiling services instead of driving a standard five-metre gate through them.
            float height=From[i]=="service"?2.8f:5.4f;
            var mesh=new StationMesh(portal,gen+"/Architecture");
            mesh.Box("metal",-3.8f,height*.5f,0,.4f,height,.6f,true);mesh.Box("metal",3.8f,height*.5f,0,.4f,height,.6f,true);
            mesh.Box("metal",0,height-.05f,0,8,.3f,.6f,true);
            mesh.Box("pink",-3.57f,height*.5f,-.33f,.07f,height-.4f,.07f);mesh.Box("ice",3.57f,height*.5f,-.33f,.07f,height-.4f,.07f);
            mesh.Print(mat,0,height*.5f-.05f,0,7.1f,height-.2f,new Rect(0,0,1,1));
            mesh.Box("black",0,height*.5f,.6f,7.2f,height,.2f,true);mesh.Finish();
            var label=WorldAuthor.Child(portal,"Walk through");label.localPosition=new Vector3(0,height+.45f,-.32f);
            WorldLettering.Install(label,To[i]=="rail"?"FINAL PLATFORM  /  WALK THROUGH":"uwu  /  WALK THROUGH",.11f,new Color(.65f,.9f,1));
            WorldLettering.Fit(label,7,From[i]=="service"?.28f:.5f);
            var arrival=WorldAuthor.Child(rooms[To[i]],"Adventure arrival from "+From[i]);
            arrival.localPosition=new Vector3(0,.2f,To[i]=="waterfall"?-48:-Depth(To[i])*.5f+7);
            entrances.Add(portal);destinations.Add(arrival);sizes.Add(new Vector3(3.5f,height-.1f,1.2f));
        }
        system.entrances=entrances.ToArray();system.destinations=destinations.ToArray();system.halfExtents=sizes.ToArray();
        system.sourceRooms=From;system.destinationRooms=To;UdonSharpEditorUtility.CopyProxyToUdon(system);
    }
    static float Depth(string id) {
        var r=Array.Find(WorldAuthor.Places,p=>p.id==id);if(r!=null)return r.d;
        if(id=="waterfall")return 140;
        if(id=="ballroom" || id=="lounge")return 64;
        if(id=="formation")return 70;
        if(id=="planet")return 80;
        return OuterDistricts.Find(id).size.z;
    }
    static void Shutter(StationMesh m,float h) {
        m.Box("metal",0,h*.5f,0,9.05f,h,.3f,true);
        for(float y=.3f;y<h;y+=.3f)m.Box("teal",0,y,-.17f,8.9f,.055f,.04f);
    }
}
