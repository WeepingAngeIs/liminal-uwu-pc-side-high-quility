using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Every room or passage has its own render bounds. Solid form and collision share coordinates.
internal sealed class StationMesh
{
    readonly Transform group;
    readonly string target;
    readonly List<Vector3> vertices = new List<Vector3>();
    readonly List<Vector3> normals = new List<Vector3>();
    readonly List<Vector2> coords = new List<Vector2>();
    readonly Dictionary<Material,List<int>> faces = new Dictionary<Material,List<int>>();
    Transform solids;
    public Vector3 Origin;
    public Quaternion Frame = Quaternion.identity;

    public StationMesh(Transform group, string target) { this.group=group; this.target=target; }
    public void ResetFrame() { Frame=Quaternion.identity; Origin=Vector3.zero; }
    public Vector3 Local(Vector3 p) => Origin + Frame*p;

    void Quad(Material material,Vector3 a,Vector3 b,Vector3 c,Vector3 d,Rect uv)
    {
        int i=vertices.Count;
        Vector3 n=Vector3.Cross(b-a,c-a).normalized;
        vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
        for(int j=0;j<4;j++) normals.Add(n);
        coords.Add(new Vector2(uv.xMin,uv.yMin)); coords.Add(new Vector2(uv.xMax,uv.yMin));
        coords.Add(new Vector2(uv.xMax,uv.yMax)); coords.Add(new Vector2(uv.xMin,uv.yMax));
        if(!faces.TryGetValue(material,out var indices)) {indices=new List<int>(); faces.Add(material,indices);}
        indices.Add(i);indices.Add(i+1);indices.Add(i+2);indices.Add(i);indices.Add(i+2);indices.Add(i+3);
    }

    public void Solid(Vector3 position,Vector3 size,Quaternion rotation,string label)
    {
        if (!solids) solids=WorldAuthor.Child(group,"Walk surfaces and walls");
        var child=WorldAuthor.Child(solids,label);
        child.localPosition=Local(position); child.localRotation=Frame*rotation;
        child.gameObject.AddComponent<BoxCollider>().size=size;
    }
    public void Box(string key,float x,float y,float z,float w,float h,float d,bool collide=false)
    { Box(key,new Vector3(x,y,z),new Vector3(w,h,d),Quaternion.identity,collide); }

    public void Box(string key,Vector3 pos,Vector3 size,Quaternion rotation,bool collide=false)
    {
        if (collide) Solid(pos,size,rotation,key);
        var center=Local(pos); var rot=Frame*rotation;
        Vector3 X=rot*Vector3.right*size.x*.5f,Y=rot*Vector3.up*size.y*.5f,Z=rot*Vector3.forward*size.z*.5f;
        var mat=ReleaseFinishes.Resolve(key); var uv=new Rect(0,0,1,1);
        Quad(mat,center-X-Y+Z,center+X-Y+Z,center+X+Y+Z,center-X+Y+Z,uv);
        Quad(mat,center+X-Y-Z,center-X-Y-Z,center-X+Y-Z,center+X+Y-Z,uv);
        Quad(mat,center-X+Y+Z,center+X+Y+Z,center+X+Y-Z,center-X+Y-Z,uv);
        Quad(mat,center-X-Y-Z,center+X-Y-Z,center+X-Y+Z,center-X-Y+Z,uv);
        Quad(mat,center+X-Y+Z,center+X-Y-Z,center+X+Y-Z,center+X+Y+Z,uv);
        Quad(mat,center-X-Y-Z,center-X-Y+Z,center-X+Y+Z,center-X+Y-Z,uv);
    }

    // Front faces look toward local -Z. The observer's right is local +X.
    public void Print(Material m,float x,float y,float z,float width,float height, Rect uv)
    {
        var a=Local(new Vector3(x-width/2,y-height/2,z));
        var b=Local(new Vector3(x+width/2,y-height/2,z));
        var c=Local(new Vector3(x+width/2,y+height/2,z));
        var d=Local(new Vector3(x-width/2,y+height/2,z));
        // Winding for -Z, with UVs still reading from left to right.
        int i=vertices.Count;
        Quad(m,a,d,c,b,uv);
        coords[i]=new Vector2(uv.xMin,uv.yMin); coords[i+1]=new Vector2(uv.xMin,uv.yMax);
        coords[i+2]=new Vector2(uv.xMax,uv.yMax); coords[i+3]=new Vector2(uv.xMax,uv.yMin);
    }

    public void Rod(string key,Vector3 p,Vector3 q,float radius,int sides=6)
    {
        Vector3 along=(q-p).normalized;
        Vector3 refAxis=Mathf.Abs(along.y)>.9f ? Vector3.right : Vector3.up;
        Vector3 a=Vector3.Cross(along,refAxis).normalized*radius;
        Vector3 b=Vector3.Cross(along,a);
        var mat=ReleaseFinishes.Resolve(key);
        for(int i=0;i<sides;i++){
            float v=i*2*Mathf.PI/sides, u=(i+1)*2*Mathf.PI/sides;
            var s=a*Mathf.Cos(v)+b*Mathf.Sin(v); var t=a*Mathf.Cos(u)+b*Mathf.Sin(u);
            Quad(mat,Local(p+s),Local(p+t),Local(q+t),Local(q+s),new Rect(0,0,1,1));
        }
    }
    public void Finish()
    {
        if(vertices.Count==0)return;
        var mesh=new Mesh {name=group.name, indexFormat=vertices.Count>65534?IndexFormat.UInt32:IndexFormat.UInt16};
        mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,coords);
        mesh.subMeshCount=faces.Count; int s=0;
        foreach(var tris in faces.Values) mesh.SetTriangles(tris,s++,false);
        mesh.RecalculateBounds();
        string file=target+"/"+group.name+".asset";
        var prior=AssetDatabase.LoadAssetAtPath<Mesh>(file);
        if(prior){EditorUtility.CopySerialized(mesh,prior);UnityEngine.Object.DestroyImmediate(mesh);mesh=prior;EditorUtility.SetDirty(mesh);}
        else AssetDatabase.CreateAsset(mesh,file);
        var go=WorldAuthor.Child(group,"Architecture").gameObject;
        go.AddComponent<MeshFilter>().sharedMesh=mesh;
        var renderer=go.AddComponent<MeshRenderer>(); renderer.sharedMaterials=faces.Keys.ToArray();
        renderer.shadowCastingMode=ShadowCastingMode.Off;
        StaticEditorFlags flags=StaticEditorFlags.OccluderStatic|StaticEditorFlags.OccludeeStatic;
        GameObjectUtility.SetStaticEditorFlags(go,flags);
    }
}
