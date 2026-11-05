using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Bake horizontal soft contact and indirect light beside the room's actual luminous
// fixtures into one low-frequency texture. No runtime lights or shadow cameras.
internal static class CourtIrradiance
{
    struct Lamp
    {
        public Vector3 center;
        public Color flux;
        public float area;
    }
    static readonly Dictionary<string,Color> Emitters=new Dictionary<string,Color>{
        {"amber",new Color(1f,.40f,.085f)},{"ice",new Color(.22f,.66f,1f)},{"pink",new Color(1f,.075f,.34f)}
    };
    static List<Lamp> ReadLamps(Transform room,float floor)
    {
        var cells=new Dictionary<Vector3Int,Lamp>();
        foreach(var renderer in room.GetComponentsInChildren<MeshRenderer>())
        {
            if(renderer.GetComponentInParent<Animator>())continue;
            var filter=renderer.GetComponent<MeshFilter>();
            if(!filter || !filter.sharedMesh || !filter.sharedMesh.isReadable)continue;
            var mesh=filter.sharedMesh;var materials=renderer.sharedMaterials;
            Vector3[] vertices=null;
            for(int m=0;m<Mathf.Min(materials.Length,mesh.subMeshCount);m++)
            {
                if(!materials[m] || !Emitters.TryGetValue(materials[m].name,out Color light))continue;
                if(vertices==null)vertices=mesh.vertices;
                var indices=mesh.GetTriangles(m);
                for(int t=0;t<indices.Length;t+=3)
                {
                    Vector3 a=filter.transform.TransformPoint(vertices[indices[t]]);
                    Vector3 b=filter.transform.TransformPoint(vertices[indices[t+1]]);
                    Vector3 c=filter.transform.TransformPoint(vertices[indices[t+2]]);
                    Vector3 center=(a+b+c)/3f;
                    float y=center.y-floor;
                    if(y<.28f || y>20f)continue;
                    float area=Mathf.Min(8f,.5f*Vector3.Cross(b-a,c-a).magnitude);
                    if(area<.001f)continue;
                    var key=Vector3Int.FloorToInt(center/2f);
                    cells.TryGetValue(key,out var lump);
                    lump.center+=center*area;lump.flux+=light*area;lump.area+=area;
                    cells[key]=lump;
                }
            }
        }
        var lamps=new List<Lamp>();
        foreach(var entry in cells.Values)
        {
            var lamp=entry;
            lamp.center/=lamp.area;lamp.flux/=lamp.area;
            lamps.Add(lamp);
        }
        return lamps.OrderByDescending(l=>Mathf.Min(8,l.area)/(.7f+Mathf.Abs(l.center.y-floor)*.25f)).Take(40).ToList();
    }

    public static Texture2D Bake(Transform room,string path,Vector3 bottom,Vector3 top)
    {
        const int size=56, rays=12;
        var result=new Texture2D(size,size,TextureFormat.RGBA32,false,true);
        result.name=room.name+" pavement irradiance";result.wrapMode=TextureWrapMode.Clamp;
        result.filterMode=FilterMode.Bilinear;
        var texels=new Color32[size*size];
        Physics.SyncTransforms();
        float floor=room.position.y;
        var lamps=ReadLamps(room,floor);
        for(int y=0;y<size;y++)
        for(int x=0;x<size;x++)
        {
            float u=(x+.5f)/size,v=(y+.5f)/size;
            Vector3 p=new Vector3(Mathf.Lerp(bottom.x,top.x,u),floor+.16f,Mathf.Lerp(bottom.z,top.z,v));
            float blocked=0f;
            float turn=((x*37+y*17)%11)*.14f;
            for(int j=0;j<rays;j++)
            {
                float altitude=.16f+.68f*(j+.5f)/rays;
                float radius=Mathf.Sqrt(1f-altitude*altitude);
                float angle=j*2.399963f+turn;
                var dir=new Vector3(radius*Mathf.Cos(angle),altitude,radius*Mathf.Sin(angle));
                if(Physics.Raycast(p,dir,out var hit,9f,~0,QueryTriggerInteraction.Ignore))
                    blocked+=Mathf.Exp(-hit.distance*.32f);
            }
            Color light=Color.black;
            foreach(var lamp in lamps)
            {
                Vector3 offset=lamp.center-p;
                float sq=offset.sqrMagnitude;
                if(sq>250f || sq<.02f)continue;
                float length=Mathf.Sqrt(sq);
                Vector3 direction=offset/length;
                float power=Mathf.Min(2f,Mathf.Sqrt(lamp.area))*(.25f+.75f*direction.y)/(1f+.095f*sq);
                if(power<.018f)continue;
                if(Physics.Raycast(p,direction,Mathf.Max(0f,length-.84f),~0,QueryTriggerInteraction.Ignore)) power*=.17f;
                light+=lamp.flux*power;
            }
            light.a=1f-blocked/rays;
            texels[y*size+x]=light;
        }
        result.SetPixels32(texels);result.Apply(false);
        var prior=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if(!prior){AssetDatabase.CreateAsset(result,path);return result;}
        EditorUtility.CopySerialized(result,prior);
        EditorUtility.SetDirty(prior);Object.DestroyImmediate(result);
        return prior;
    }
}
