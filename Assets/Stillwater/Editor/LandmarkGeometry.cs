using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// All measurements are metres. Primitives are appended directly to room-local meshes.
// In particular, window rows and rail sleepers do not create individual renderers.
internal sealed class LandmarkGeometry
{
    readonly List<Vector3> vertices = new List<Vector3>();
    readonly List<Vector3> normals = new List<Vector3>();
    readonly List<Vector2> uv = new List<Vector2>();
    readonly List<string> surfaces = new List<string>();
    readonly Dictionary<string,List<int>> triangles = new Dictionary<string,List<int>>();

    internal Matrix4x4 frame = Matrix4x4.identity;
    internal static Vector3 V(float x, float y, float z) => new Vector3(x,y,z);
    internal static Matrix4x4 Frame(Vector3 position, Vector3 euler)
        => Matrix4x4.TRS(position,Quaternion.Euler(euler),Vector3.one);

    List<int> Indices(string surface)
    {
        if (!triangles.TryGetValue(surface,out var list))
        {
            list = new List<int>();
            triangles.Add(surface,list);
            surfaces.Add(surface);
        }
        return list;
    }

    internal void Quad(string mat, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        a = frame.MultiplyPoint3x4(a); b = frame.MultiplyPoint3x4(b);
        c = frame.MultiplyPoint3x4(c); d = frame.MultiplyPoint3x4(d);
        Vector3 n = Vector3.Cross(b-a,c-a) + Vector3.Cross(c-a,d-a);
        if (n.sqrMagnitude < 1e-12f) return;
        n.Normalize();
        int first = vertices.Count;
        vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
        for (int i=0;i<4;i++) normals.Add(n);
        float width = Mathf.Max((b-a).magnitude,(c-d).magnitude);
        float height = Mathf.Max((d-a).magnitude,(c-b).magnitude);
        uv.Add(Vector2.zero); uv.Add(new Vector2(width,0));
        uv.Add(new Vector2(width,height)); uv.Add(new Vector2(0,height));
        var ix = Indices(mat);
        ix.Add(first); ix.Add(first+1); ix.Add(first+2);
        ix.Add(first); ix.Add(first+2); ix.Add(first+3);
    }

    internal void Box(string mat, Vector3 center, Vector3 size)
    {
        Vector3 a = center - size*0.5f, b = center + size*0.5f;
        Vector3 lbf=V(a.x,a.y,b.z), rbf=V(b.x,a.y,b.z), ltf=V(a.x,b.y,b.z), rtf=V(b.x,b.y,b.z);
        Vector3 lbb=V(a.x,a.y,a.z), rbb=V(b.x,a.y,a.z), ltb=V(a.x,b.y,a.z), rtb=V(b.x,b.y,a.z);
        Quad(mat,lbf,rbf,rtf,ltf); Quad(mat,rbb,lbb,ltb,rtb);
        Quad(mat,rbf,rbb,rtb,rtf); Quad(mat,lbb,lbf,ltf,ltb);
        Quad(mat,ltf,rtf,rtb,ltb); Quad(mat,lbb,rbb,rbf,lbf);
    }

    // Extrude a clockwise-or-counterclockwise plan polygon, including both faces.
    // Used for tapered wings, keels, bridges and other silhouettes.
    internal void Plate(string mat, Vector3[] outline, float thickness)
    {
        Vector3 center=Vector3.zero;
        foreach(var p in outline) center+=p;
        center/=outline.Length;
        Vector3 lift=Vector3.up*thickness*0.5f;
        for(int i=0;i<outline.Length;i++)
        {
            Vector3 a=outline[i], b=outline[(i+1)%outline.Length];
            if(Vector3.Dot(Vector3.Cross(a-center,b-center),Vector3.up)<0) {var t=a;a=b;b=t;}
            Quad(mat,center+lift,a+lift,b+lift,center+lift);
            Quad(mat,center-lift,b-lift,a-lift,center-lift);
            Quad(mat,b+lift,a+lift,a-lift,b-lift);
        }
    }

    internal void Beam(string mat, Vector3 a,Vector3 b,float width,float depth)
    {
        Vector3 axis=b-a;
        if(axis.sqrMagnitude<0.000001f)return;
        Matrix4x4 saved=frame;
        frame*=Matrix4x4.TRS((a+b)*0.5f,Quaternion.FromToRotation(Vector3.up,axis.normalized),Vector3.one);
        Box(mat,Vector3.zero,V(width,axis.magnitude,depth));
        frame=saved;
    }

    internal void Tube(string mat, Vector3 a, Vector3 b, float radiusA, float radiusB, int sides=10)
    {
        Vector3 axis=(b-a).normalized;
        if(axis.sqrMagnitude<0.5f)return;
        Vector3 t=Vector3.Cross(axis,Mathf.Abs(axis.y)<0.85f?Vector3.up:Vector3.forward).normalized;
        Vector3 u=Vector3.Cross(axis,t);
        for(int i=0;i<sides;i++)
        {
            float angle=i*(2*Mathf.PI/sides), next=(i+1)*(2*Mathf.PI/sides);
            Vector3 e=t*Mathf.Cos(angle)+u*Mathf.Sin(angle);
            Vector3 f=t*Mathf.Cos(next)+u*Mathf.Sin(next);
            Quad(mat,a+e*radiusA,a+f*radiusA,b+f*radiusB,b+e*radiusB);
            Quad(mat,a,a+f*radiusA,a+e*radiusA,a);
            Quad(mat,b,b+e*radiusB,b+f*radiusB,b);
        }
    }

    // Elliptical sections normal to +Z, sorted from tail to nose. Each vector contains
    // (radius x, radius y, section z); 'rise' controls drooping noses and arching roofs.
    internal void Hull(string mat,Vector3[] sections,float[] rise,int sides)
    {
        for(int k=0;k<sections.Length-1;k++)
        {
            Vector3 s=sections[k], t=sections[k+1];
            for(int i=0;i<sides;i++)
            {
                float a=i*(2*Mathf.PI/sides), b=(i+1)*(2*Mathf.PI/sides);
                Vector3 p=V(s.x*Mathf.Cos(a),rise[k]+s.y*Mathf.Sin(a),s.z);
                Vector3 q=V(s.x*Mathf.Cos(b),rise[k]+s.y*Mathf.Sin(b),s.z);
                Vector3 r=V(t.x*Mathf.Cos(b),rise[k+1]+t.y*Mathf.Sin(b),t.z);
                Vector3 h=V(t.x*Mathf.Cos(a),rise[k+1]+t.y*Mathf.Sin(a),t.z);
                Quad(mat,p,q,r,h);
            }
        }
    }

    internal void Crown(string mat, Vector3 center,Vector3 radii,int seed,int sides,int rings)
    {
        var points=new Vector3[rings+1,sides];
        var rand=new System.Random(seed);
        for(int j=0;j<=rings;j++)
        {
            float latitude=-Mathf.PI*0.5f+j*Mathf.PI/rings;
            for(int i=0;i<sides;i++)
            {
                float angle=(i+(j%2)*0.32f)*Mathf.PI*2/sides;
                float r=(j==0||j==rings)?1:(0.84f+(float)rand.NextDouble()*0.30f);
                points[j,i]=center+r*Vector3.Scale(radii,V(Mathf.Cos(angle)*Mathf.Cos(latitude),Mathf.Sin(latitude),Mathf.Sin(angle)*Mathf.Cos(latitude)));
            }
        }
        for(int j=0;j<rings;j++)for(int i=0;i<sides;i++)
        {
            int k=(i+1)%sides;
            Quad(mat,points[j,i],points[j+1,i],points[j+1,k],points[j,k]);
        }
    }

    internal GameObject Save(Transform room,string folder,string name)
    {
        if(vertices.Count==0)return null;
        Directory.CreateDirectory(folder);
        string path=folder+"/"+name+".asset";
        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        bool create=mesh==null;
        if(create)mesh=new Mesh();
        mesh.Clear();
        mesh.name=name;
        mesh.indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16;
        mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0,uv);
        mesh.subMeshCount=surfaces.Count;
        var materials=new Material[surfaces.Count];
        for(int i=0;i<surfaces.Count;i++)
        {
            mesh.SetTriangles(triangles[surfaces[i]],i,false);
            materials[i]=ReleaseFinishes.Resolve(surfaces[i]);
        }
        mesh.RecalculateBounds();
        if(create)AssetDatabase.CreateAsset(mesh,path);else EditorUtility.SetDirty(mesh);
        var obj=new GameObject(name);
        obj.transform.SetParent(room,false);
        obj.AddComponent<MeshFilter>().sharedMesh=mesh;
        var renderer=obj.AddComponent<MeshRenderer>();
        renderer.sharedMaterials=materials;
        renderer.shadowCastingMode=ShadowCastingMode.Off;
        return obj;
    }
}
