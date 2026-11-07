using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UdonSharp;
using UdonSharpEditor;
using VRC.SDK3.Components;

internal static class DistrictSystems
{
    internal static void PreparePrograms()
    {
        foreach(string name in new[]{"WorldAudioDirector","DistrictVisibility","ApertureApproach"}) {
            string path=WorldAuthor.Root+"/Runtime/"+name;
            if(AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(path+".asset"))continue;
            var script=AssetDatabase.LoadAssetAtPath<MonoScript>(path+".cs");
            if(!script)throw new InvalidOperationException("Missing runtime script "+name);
            var program=ScriptableObject.CreateInstance<UdonSharpProgramAsset>();program.sourceCsScript=script;
            AssetDatabase.CreateAsset(program,path+".asset");
        }
        AssetDatabase.SaveAssets();
    }
    internal static Transform Connector(Transform root,string a,string b,Vector3 start,Vector3 end,string gen,bool quest)
    {
        Vector3 axis=end-start;float length=axis.magnitude;
        var t=WorldAuthor.Child(root,"Passage "+a+" to "+b);
        t.position=(start+end)*.5f;t.rotation=Quaternion.LookRotation(axis);
        var m=new StationMesh(t,gen+"/Architecture");
        // Both rooms overhang their edge by .16m; only collision overlaps.
        m.Box("wet",0,-.19f,0,9.5f,.38f,length-.32f);
        m.Solid(new Vector3(0,-.19f,0),new Vector3(9.5f,.38f,length+.1f),Quaternion.identity,"wet");
        m.Box("concrete",0,5.05f,0,9.7f,.34f,length+.2f,true);
        foreach(int side in new[]{-1,1}) {
            m.Box("limestone",side*4.7f,2.45f,0,.38f,4.9f,length+.18f,true);
            m.Box("bath tile",side*4.46f,.85f,0,.15f,1.7f,length);
            m.Box("brass",side*4.36f,1.74f,0,.09f,.08f,length);
            for(float z=-length/2+1;z<length/2;z+=4.2f)m.Box("metal",side*4.41f,2.45f,z,.15f,4.9f,.21f);
            m.Rod("rust",new Vector3(side*4,4.55f,-length/2),new Vector3(side*4,4.55f,length/2),.09f,quest?5:8);
        }
        for(float z=-length/2+2;z<length/2;z+=6) {
            m.Box("metal",0,4.75f,z,3.8f,.17f,.52f);
            m.Box("ice",0,4.62f,z,3.45f,.07f,.32f);
        }
        m.Finish();return t;
    }

    internal static WorldAudioDirector InstallAudio(Transform root)
    {
        // Replace legacy point emitters, including copied gravity-room emitters.
        foreach(var source in root.GetComponentsInChildren<AudioSource>()) {
            var spatial=source.GetComponent<VRCSpatialAudioSource>();
            if(spatial)UnityEngine.Object.DestroyImmediate(spatial);
            UnityEngine.Object.DestroyImmediate(source);
        }
        var sectors=SectorRoots(root);
        var bounds=sectors.Select(BoundsFor).ToArray();
        var go=WorldAuthor.Child(root,"Local room soundscapes").gameObject;
        var audio=go.AddUdonSharpComponent<WorldAudioDirector>();
        audio.zoneMin=bounds.Select(b=>b.min).ToArray();audio.zoneMax=bounds.Select(b=>b.max).ToArray();
        audio.atmosphereClips=sectors.Select(t=>Clip(Tone(t.name))).ToArray();
        audio.rainy=sectors.Select(t=>t.name=="hub" || t.name=="observatory" || t.name=="billboard").ToArray();
        audio.atmosphere=Source(go.transform,"Atmosphere",null);
        audio.rain=Source(go.transform,"Rain",Clip("rain"));
        UdonSharpEditorUtility.CopyProxyToUdon(audio);return audio;
    }
    internal static AudioSource Source(Transform parent,string name,AudioClip clip)
    {
        var go=WorldAuthor.Child(parent,name).gameObject;
        var source=go.AddComponent<AudioSource>();source.clip=clip;source.loop=true;source.playOnAwake=false;
        source.spatialBlend=0;source.spatialize=false;source.dopplerLevel=0;source.volume=0;source.priority=160;
        var spatial=go.AddComponent<VRCSpatialAudioSource>();spatial.EnableSpatialization=false;spatial.Gain=0;
        return source;
    }
    static AudioClip Clip(string id)
    {
        var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(Surface.Media+"/Audio/"+id+".wav");
        if(!clip)throw new InvalidOperationException("Missing room audio: "+id);return clip;
    }
    static string Tone(string id)
    {
        if(id=="hub" || id=="billboard")return "gallery";
        if(id=="observatory" || id=="causeway")return "space";
        if(id=="concourse")return "rail";
        if(id=="stacks" || id=="returns" || id.StartsWith("Passage "))return "annex";
        if(id=="Diminishing Baths")return "baths";
        if(id=="Night Reception")return "annex";
        if(id=="Soffit gallery" || id=="Western facade gallery" || id=="Six axis field chamber")return "gravity";
        return id;
    }
    static List<Transform> SectorRoots(Transform root)
    {
        var names=new HashSet<string>(WorldAuthor.Places.Select(p=>p.id));
        foreach(var spec in OuterDistricts.Specs)names.Add(spec.id);
        foreach(string n in new[]{"Diminishing Baths","Night Reception","Soffit gallery","Western facade gallery","Six axis field chamber"})names.Add(n);
        var result=new List<Transform>();
        foreach(Transform t in root)if(names.Contains(t.name))result.Add(t);
        foreach(Transform t in root)if(t.name.StartsWith("Passage "))result.Add(t);
        return result;
    }
    internal static Bounds BoundsFor(Transform t)
    {
        var place=Array.Find(WorldAuthor.Places,p=>p.id==t.name);
        if(place!=null) {
            if(t.name=="rail")return new Bounds(new Vector3(0,165,172.92f),new Vector3(68.32f,334,254.16f));
            return new Bounds(t.position+Vector3.up*(place.h*.5f-1),new Vector3(place.w+.32f,place.h+4,place.d+.32f));
        }
        var spec=Array.Find(OuterDistricts.Specs,s=>s.id==t.name);
        if(spec!=null)return new Bounds(t.position+Vector3.up*(spec.size.y*.5f-1),spec.size+new Vector3(.32f,4,.32f));
        var bounds=new Bounds(t.position,Vector3.one);
        foreach(var renderer in t.GetComponentsInChildren<MeshRenderer>())bounds.Encapsulate(renderer.bounds);
        bounds.Expand(new Vector3(.2f,2,.2f));return bounds;
    }
    internal static void InstallVisibility(Transform root)
    {
        var sectors=SectorRoots(root);var bounds=sectors.Select(BoundsFor).ToArray();
        var go=WorldAuthor.Child(root,"District visibility").gameObject;
        var system=go.AddUdonSharpComponent<DistrictVisibility>();
        system.zoneMin=bounds.Select(b=>b.min).ToArray();system.zoneMax=bounds.Select(b=>b.max).ToArray();
        var renderers=new List<Renderer>();var indices=new List<int>();
        for(int i=0;i<sectors.Count;i++)foreach(var renderer in sectors[i].GetComponentsInChildren<Renderer>(true)) {
            // World-space UI follows the local player and must not belong to a district.
            if(renderer.GetComponentInParent<Canvas>())continue;
            renderers.Add(renderer);indices.Add(i);
        }
        var a=new List<int>();var b=new List<int>();var centers=new List<Vector3>();var radii=new List<float>();
        for(int i=0;i<sectors.Count;i++) {
            string name=sectors[i].name;
            if(!name.StartsWith("Passage "))continue;
            string[] ends=name.Substring(8).Split(new[]{" to "},StringSplitOptions.None);
            if(ends.Length!=2)throw new InvalidOperationException("Bad connector name "+name);
            foreach(string end in ends) {
                int j=sectors.FindIndex(t=>t.name==end);
                if(j<0)throw new InvalidOperationException("Missing connector district "+end);
                Vector3 point=bounds[j].ClosestPoint(sectors[i].position);point.y=2.5f;
                a.Add(i);b.Add(j);centers.Add(point);radii.Add(6);
            }
        }
        AddBranch(sectors,a,b,centers,radii,"gallery","Diminishing Baths",new Vector3(0,2.5f,-108));
        AddBranch(sectors,a,b,centers,radii,"service","Night Reception",new Vector3(-104,2,-80));
        system.renderers=renderers.ToArray();system.rendererSector=indices.ToArray();
        system.portalA=a.ToArray();system.portalB=b.ToArray();system.portalCenter=centers.ToArray();system.portalRadius=radii.ToArray();
        UdonSharpEditorUtility.CopyProxyToUdon(system);
    }
    static void AddBranch(List<Transform> sectors,List<int>a,List<int>b,List<Vector3>points,List<float>radii,string from,string to,Vector3 point)
    {
        int x=sectors.FindIndex(t=>t.name==from),y=sectors.FindIndex(t=>t.name==to);
        if(x<0||y<0)throw new InvalidOperationException("Missing annex sector");
        a.Add(x);b.Add(y);points.Add(point);radii.Add(6);
    }
}
