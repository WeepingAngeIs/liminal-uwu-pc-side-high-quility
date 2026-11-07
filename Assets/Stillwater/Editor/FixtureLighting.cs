using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UdonSharpEditor;

// Low-frequency irradiance volumes are baked on the hosted author, from real
// fixture positions and collision shadows. No runtime GI or per-room cameras.
internal static class FixtureLighting
{
    struct Emitter { internal Vector3 position;internal Color color;internal float area; }
    internal static void Bake(Transform root,string gen,bool quest)
    {
        Physics.SyncTransforms();
        var lamps=new List<Light>();
        foreach(var room in DistrictSystems.SectorRoots(root)) {
            if(room.name.StartsWith("Passage "))continue;
            var bounds=DistrictSystems.BoundsFor(room);
            // Tall train / waterfall volumes keep fine sampling near the walking
            // surfaces. The upper sky remains dark and the rail has its own lamps.
            bounds.SetMinMax(new Vector3(bounds.min.x,Mathf.Max(room.position.y-.6f,bounds.min.y),bounds.min.z),
                new Vector3(bounds.max.x,(room.name=="rail" || room.name=="waterfall")?Mathf.Min(room.position.y+36,bounds.max.y):bounds.max.y,bounds.max.z));
            var emitters=ReadEmitters(room,bounds);
            if(emitters.Count==0)continue;
            int nx=quest?18:32,ny=quest?10:18,nz=quest?18:32;
            var colour=new Color[nx*ny*nz];var directions=new Color[colour.Length];
            int index=0;
            for(int z=0;z<nz;z++)for(int y=0;y<ny;y++)for(int x=0;x<nx;x++) {
                Vector3 p=bounds.min+Vector3.Scale(bounds.size,new Vector3((x+.5f)/nx,(y+.5f)/ny,(z+.5f)/nz));
                Color energy=Color.black;Vector3 direction=Vector3.zero;
                foreach(var e in emitters) {
                    Vector3 delta=e.position-p;float distance=delta.magnitude;
                    float range=13+Mathf.Min(7,Mathf.Sqrt(e.area)*2);
                    if(distance>=range || distance<.1f)continue;
                    float power=Mathf.Pow(1-distance/range,2)*Mathf.Clamp(Mathf.Sqrt(e.area)*2,1.8f,5)/(1+.032f*distance*distance);
                    if(power<.002f)continue;
                    // Fixtures are broad faces. Leave a small final segment to
                    // avoid shadowing a lamp with its own decorative housing.
                    if(Physics.Raycast(p,delta/distance,Mathf.Max(0,distance-.65f),~0,QueryTriggerInteraction.Ignore))power*=.055f;
                    Color contribution=e.color*power;energy+=contribution;
                    direction+=delta/distance*(contribution.r+contribution.g+contribution.b);
                }
                colour[index]=new Color(Mathf.Clamp01(energy.r*.25f),Mathf.Clamp01(energy.g*.25f),Mathf.Clamp01(energy.b*.25f),1);
                direction=direction.sqrMagnitude>.00001f?direction.normalized:Vector3.up;
                directions[index++]=new Color(direction.x*.5f+.5f,direction.y*.5f+.5f,direction.z*.5f+.5f,1);
            }
            string folder=gen+"/Finishes/";
            var colourTex=Texture(folder+room.name+" fixture light.asset",nx,ny,nz,colour);
            var directionTex=Texture(folder+room.name+" fixture direction.asset",nx,ny,nz,directions);
            var variants=new Dictionary<Material,Material>();
            foreach(var renderer in room.GetComponentsInChildren<MeshRenderer>(true)) {
                var materials=renderer.sharedMaterials;
                for(int i=0;i<materials.Length;i++) {
                    var source=materials[i];if(!source || !source.HasProperty("_FixtureColor"))continue;
                    if(!variants.TryGetValue(source,out var material)) {
                        string path=folder+room.name+" lit "+source.name+".mat";
                        material=AssetDatabase.LoadAssetAtPath<Material>(path);
                        if(!material){material=new Material(source);AssetDatabase.CreateAsset(material,path);}
                        else material.CopyPropertiesFromMaterial(source);
                        material.SetTexture("_FixtureColor",colourTex);material.SetTexture("_FixtureDirection",directionTex);
                        material.SetVector("_FixtureMin",bounds.min);material.SetVector("_FixtureSize",bounds.size);
                        material.EnableKeyword("SW_FIXTURE_VOLUME");EditorUtility.SetDirty(material);variants.Add(source,material);
                    }
                    materials[i]=material;
                }
                renderer.sharedMaterials=materials;
            }
            if(!quest)foreach(var e in emitters.OrderBy(e=>Mathf.Abs(e.position.y-room.position.y-4)).Take(8)) {
                var t=WorldAuthor.Child(room,"Fixture spill light");t.position=e.position;
                var light=t.gameObject.AddComponent<Light>();light.type=LightType.Point;light.color=e.color.gamma;
                light.intensity=1.5f;light.range=14;light.shadows=LightShadows.None;light.renderMode=LightRenderMode.ForceVertex;
                light.enabled=false;lamps.Add(light);
            }
            Debug.Log("FIXTURE_LIGHTING_BAKED "+room.name+" sources="+emitters.Count+" cells="+colour.Length);
        }
        var controller=WorldAuthor.Child(root,"Nearby practical lights").gameObject.AddUdonSharpComponent<LocalPracticalLights>();
        controller.lamps=lamps.ToArray();UdonSharpEditorUtility.CopyProxyToUdon(controller);
    }
    static Texture3D Texture(string path,int x,int y,int z,Color[] pixels)
    {
        var texture=new Texture3D(x,y,z,GraphicsFormat.R8G8B8A8_UNorm,TextureCreationFlags.None){name=System.IO.Path.GetFileNameWithoutExtension(path),wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
        texture.SetPixels(pixels);texture.Apply(false,false);
        var existing=AssetDatabase.LoadAssetAtPath<Texture3D>(path);
        if(existing){EditorUtility.CopySerialized(texture,existing);UnityEngine.Object.DestroyImmediate(texture);EditorUtility.SetDirty(existing);return existing;}
        AssetDatabase.CreateAsset(texture,path);return texture;
    }
    static List<Emitter> ReadEmitters(Transform room,Bounds bounds)
    {
        var cells=new Dictionary<Vector3Int,Emitter>();
        foreach(var r in room.GetComponentsInChildren<MeshRenderer>(true)) {
            // Coach lights move; their coach-space lighting stays with the train.
            if(r.GetComponentInParent<Animator>())continue;
            var fragments=room.Find("Remembered architecture");
            if(fragments && r.transform.IsChildOf(fragments))continue;
            var filter=r.GetComponent<MeshFilter>();
            if(!filter || !filter.sharedMesh || !filter.sharedMesh.isReadable)continue;
            var mesh=filter.sharedMesh;var materials=r.sharedMaterials;Vector3[] vertices=null;
            for(int m=0;m<Mathf.Min(mesh.subMeshCount,materials.Length);m++) {
                var material=materials[m];if(!material)continue;
                string name=material.name;Color color;
                if(name=="pink")color=new Color(1,.035f,.24f);
                else if(name=="ice")color=new Color(.13f,.52f,1);
                else if(name=="amber")color=new Color(1,.42f,.1f);
                else if(name=="red")color=new Color(1,.045f,.015f);
                else if(name.Contains("shoji"))color=new Color(1,.50f,.22f);
                else if(name.Contains("Kitty warm"))color=new Color(1,.025f,.29f);
                else continue;
                if(vertices==null)vertices=mesh.vertices;
                int[] indices=mesh.GetTriangles(m);
                for(int t=0;t<indices.Length;t+=3) {
                    Vector3 a=r.transform.TransformPoint(vertices[indices[t]]),b=r.transform.TransformPoint(vertices[indices[t+1]]),c=r.transform.TransformPoint(vertices[indices[t+2]]);
                    Vector3 position=(a+b+c)/3;
                    if(!bounds.Contains(position))continue;
                    float area=Vector3.Cross(b-a,c-a).magnitude*.5f;
                    if(area<.0001f)continue;
                    var cell=Vector3Int.FloorToInt(position/2.8f);cells.TryGetValue(cell,out var e);
                    e.position+=position*area;e.color+=color*area;e.area+=area;cells[cell]=e;
                }
            }
        }
        var result=new List<Emitter>();
        foreach(var value in cells.Values){var e=value;e.position/=e.area;e.color/=e.area;result.Add(e);}
        return result.OrderByDescending(e=>Mathf.Min(9,e.area)/(1+Mathf.Max(0,e.position.y-room.position.y-6)*.10f)).Take(64).ToList();
    }
}
