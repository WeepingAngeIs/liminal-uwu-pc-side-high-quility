using System.Collections.Generic;
using UnityEngine;
using UdonSharpEditor;
using static LandmarkGeometry;

internal static class FloatingStreetProps
{
    internal static void Install(Transform root,Dictionary<string,Transform> rooms,string gen,bool quest)
    {
        var objects=new List<Transform>();var surfaces=new List<Renderer>();
        string[] areas={"hub","observatory","causeway","planet"};
        foreach(string id in areas) {
            if(!rooms.ContainsKey(id))continue;
            var room=rooms[id];
            int count=quest?7:11;
            // Objects occupy alternating verges. Neither the portals nor the
            // middle of the street becomes a synchronized line of debris.
            for(int i=0;i<count;i++) {
                int type=i%5;float side=(i%2==0?-1:1);
                var holder=WorldAuthor.Child(room,"Suspended "+Kind(type)+" "+i);
                holder.localPosition=new Vector3(side*(14+i%3*3.1f),7+(i*7%13),-18+(i*11%37));
                holder.localRotation=Quaternion.Euler(-19+i*17%67,i*137.5f,side*(16+i*13%55));
                var g=new LandmarkGeometry();
                if(type==0)Landmarks.Saloon(g,i%2==0?"teal":"ivory",quest);
                if(type==1)Streetlight(g,quest);
                if(type==2)Lamp(g,quest);
                if(type==3)Bin(g,quest);
                if(type==4)Bush(g,quest);
                var model=g.Save(holder,gen+"/Architecture","Lifted "+id+" "+i);
                objects.Add(holder);surfaces.Add(model.GetComponent<Renderer>());
            }
        }
        var effect=WorldAuthor.Child(root,"Suspended street objects").gameObject.AddUdonSharpComponent<FloatingStreetObjects>();
        effect.objects=objects.ToArray();effect.surfaces=surfaces.ToArray();
        effect.restPositions=new Vector3[objects.Count];effect.restRotations=new Quaternion[objects.Count];
        for(int i=0;i<objects.Count;i++){effect.restPositions[i]=objects[i].localPosition;effect.restRotations[i]=objects[i].localRotation;}
        UdonSharpEditorUtility.CopyProxyToUdon(effect);
    }
    static string Kind(int kind) => new[]{"car","streetlight","lamp","bin","bush"}[kind];
    static void Streetlight(LandmarkGeometry g,bool quest)
    {
        g.Box("concrete",V(0,-.2f,0),V(.85f,.4f,.8f));
        g.Tube("metal",V(0,0,0),V(0,5.1f,0),.15f,.07f,quest?6:12);
        g.Beam("metal",V(0,5.1f,0),V(1.55f,5.35f,0),.13f,.16f);
        g.Box("metal",V(1.7f,5.25f,0),V(1.15f,.28f,.5f));
        g.Box("amber",V(1.7f,5.1f,0),V(.94f,.05f,.35f));
        // A loose cable and torn base make it read as a displaced street fixture.
        for(int i=0;i<5;i++)g.Beam("black",V(0,-.3f-i*.24f,i*.07f),V(0,-.54f-i*.24f,(i+1)*.07f),.025f,.025f);
    }
    static void Lamp(LandmarkGeometry g,bool quest)
    {
        g.Tube("brass",V(0,0,0),V(0,.1f,0),.44f,.39f,quest?8:16);
        g.Tube("brass",V(0,.1f,0),V(0,1.75f,0),.045f,.035f,quest?6:10);
        g.Tube("paper",V(0,1.4f,0),V(0,2.1f,0),.63f,.26f,quest?10:20);
        g.Tube("amber",V(0,1.39f,0),V(0,1.42f,0),.59f,.59f,quest?10:20);
    }
    static void Bin(LandmarkGeometry g,bool quest)
    {
        g.Tube("metal",V(0,0,0),V(0,1.05f,0),.40f,.48f,quest?8:16);
        g.Tube("black",V(0,1.06f,0),V(0,1.09f,0),.4f,.4f,quest?8:16);
        g.Tube("brass",V(0,1.1f,0),V(0,1.14f,0),.49f,.49f,quest?8:16);
        for(int i=0;i<8;i++) {float a=i*Mathf.PI/4;g.Beam("black",V(Mathf.Cos(a)*.405f,.06f,Mathf.Sin(a)*.405f),V(Mathf.Cos(a)*.475f,1,Mathf.Sin(a)*.475f),.035f,.035f);}
        g.Box("ivory",V(.09f,1.25f,0),V(.33f,.06f,.22f));
    }
    static void Bush(LandmarkGeometry g,bool quest)
    {
        g.Crown("bark",V(0,0,0),V(.95f,.4f,.8f),777,quest?6:10,quest?3:5);
        for(int i=0;i<5;i++) {
            float a=i*2.4f;
            g.Tube("bark",V(0,0,0),V(Mathf.Cos(a)*.8f,-.75f-i*.06f,Mathf.Sin(a)*.7f),.07f,.012f,5);
            g.Crown("leaf",V(Mathf.Cos(a)*.62f,.7f+i%2*.33f,Mathf.Sin(a)*.5f),V(.9f,.8f,.9f),771+i,quest?6:10,quest?3:5);
        }
    }
}
