using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UdonSharp;
using UdonSharpEditor;
using VRC.SDK3.Components;
using static LandmarkGeometry;

internal static class ExpressLine
{
    const float Radius = 17;
    const float Helix = 6 * Mathf.PI * Radius;
    const float Grade = .31f;
    const float TerminalGrade = 1.1106125f; // tan(48 degrees), matching Eclipse.shader's pole.
    const float Run = Helix + 145;
    const float End = Helix + 143;
    const float Deck = .46f;
    const string TypeFolder = "Assets/Stillwater/Transit";
    static readonly int[] TypeWidths = {485,312,383,234,667,502,487,233,767,408};

    static Transform Point(Transform parent,string name,Vector3 p)
    {
        var t=WorldAuthor.Child(parent,name);t.localPosition=p;return t;
    }
    static void Collider(Transform root,string name,Vector3 p,Vector3 size)
    {
        Point(root,name,p).gameObject.AddComponent<BoxCollider>().size=size;
    }
    static float Elevation(float s)
    {
        float u=Mathf.Clamp(s,0,Helix);
        float y=.07f+Grade*(u-12*(1-Mathf.Exp(-u/12)));
        if(s<=Helix)return y;
        float v=s-Helix,blend=Mathf.Min(v,18);
        return y+Grade*blend+(TerminalGrade-Grade)*blend*blend/36+TerminalGrade*Mathf.Max(0,v-18);
    }
    static Vector3 Track(float s)
    {
        if(s<0)return V(Radius,Elevation(s),s);
        if(s>Helix)return V(Radius,Elevation(s),s-Helix);
        float angle=s/Radius;
        return V(Radius*Mathf.Cos(angle),Elevation(s),Radius*Mathf.Sin(angle));
    }
    static Vector3 Angles(float s)
    {
        float slope=Grade*(1-Mathf.Exp(-Mathf.Max(0,s)/12));
        if(s>Helix)slope=Mathf.Lerp(Grade,TerminalGrade,(s-Helix)/18);
        float yaw=-Mathf.Clamp(s,0,Helix)/Radius*Mathf.Rad2Deg;
        return V(-Mathf.Atan(slope)*Mathf.Rad2Deg,yaw,0);
    }
    static Vector3 Offset(float s,float across,float up)
    {
        return Track(s)+Quaternion.Euler(Angles(s))*V(across,up,0);
    }
    static Vector3 RibPoint(float s,float a)
    {
        return Offset(s,4.28f*Mathf.Cos(a),2.02f+4.0f*Mathf.Sin(a));
    }
    internal static Vector3 ApproachStart => Track(Helix+50);
    internal static Vector3 FrontSummit => Track(Run-7);
    internal static Vector3 ApertureDirection => new Vector3(0,.743144825f,.669130606f);
    static void Tunnel(LandmarkGeometry g,bool quest)
    {
        int rails=quest?220:350;
        for(int i=0;i<rails;i++){
            float a=Mathf.Lerp(-28,End,(float)i/rails),b=Mathf.Lerp(-28,End,(float)(i+1)/rails);
            foreach(float x in new[]{-.85f,.85f})g.Beam("metal",Offset(a,x,0),Offset(b,x,0),.105f,.15f);
            g.Beam("brass",Offset(a,0,-.29f),Offset(b,0,-.29f),.32f,.39f);
            g.Beam("rust",Offset(a,-1.34f,-.095f),Offset(a,1.34f,-.095f),.19f,.17f);
            if(i%3==0)g.Beam("ice",Offset(a,-1.24f,-.13f),Offset(b,-1.24f,-.13f),.065f,.085f);
        }
        int count=quest?65:98, edges=quest?9:15;
        for(int i=0;i<=count;i++){
            float s=Mathf.Lerp(38,End,(float)i/count);
            float next=Mathf.Lerp(38,End,(float)(i+1)/count);
            // Arched cast segments and staggered exterior panels frame the sky ahead of riders.
            for(int j=0;j<edges;j++){
                float a=-.58f+j*(Mathf.PI+1.16f)/edges,b=a+(Mathf.PI+1.16f)/edges;
                Vector3 p=RibPoint(s,a),q=RibPoint(s,b);
                g.Beam(i%5==0?"brass":"metal",p,q,.19f,.21f);
                if(i>=count)continue;
                if(j==0||j==edges-1||j==edges/2)
                    g.Beam(j==edges/2?"brass":"teal",p,RibPoint(next,a),.17f,.15f);
                if(i%4==0 && (j==1||j==edges-2))
                    g.Beam("ice",p,q,.10f,.13f);
                if(s<Helix-8 && (i/4)%3!=1 && ((j+i/7)%edges)<edges/3){
                    Vector3 r=RibPoint(next,b),t=RibPoint(next,a);
                    g.Quad("concrete",p,q,r,t);g.Quad("concrete",t,r,q,p);
                }
            }
        }
        for(int i=0;i<22;i++){
            float s=Mathf.Lerp(42,Helix-10,i/21f);Vector3 rail=Offset(s,0,-.48f),outer=Offset(s,3.85f,-.55f);
            g.Beam("concrete",V(outer.x,.02f,outer.z),outer,.42f,.56f);
            g.Beam("rust",outer,rail,.28f,.32f);
            g.Beam("metal",outer-Vector3.up*2.5f,rail,.17f,.18f);
        }
    }

    internal static void Install(LandmarkGeometry g,Transform room,string folder,bool quest)
    {
        // Udon backing heaps must exist before adding the first proxy on a clean project.
        var program=UdonSharpProgramAsset.GetProgramAssetForClass(typeof(ApertureExpress));
        if(!program || program.CompiledVersion!=UdonSharpProgramVersion.CurrentVersion)
            UdonSharp.Compiler.UdonSharpCompilerV1.CompileSync(new UdonSharp.Compiler.UdonSharpCompileOptions());
        Tunnel(g,quest);
        Transform bay=WorldAuthor.Child(room,"Platform 01");
        Platform(g,bay);
        Transform exit=Point(bay,"Dock safe exit",V(12.95f,.54f,-14.95f));
        var console=Point(bay,"Call aperture train",V(11.65f,1.63f,-19.2f));
        console.gameObject.AddComponent<BoxCollider>().size=V(1.0f,.87f,.54f);
        var dispatch=console.gameObject.AddUdonSharpComponent<ApertureExpress>();
        var vm=UdonSharpEditorUtility.GetBackingUdonBehaviour(dispatch);
        vm.interactText="Call train down";vm.proximity=3;
        var panels=new GameObject[6];
        for(int i=0;i<panels.Length;i++) {
            panels[i]=Sign(bay,"Platform departure indicator "+i,i,V(13.3f,3.07f,-20.18f),.27f);
            panels[i].SetActive(i==0);
        }
        Sign(bay,"Station header",6,V(13.3f,3.48f,-20.18f),.34f);
        Sign(bay,"Dispatch lettering",7,V(11.65f,1.82f,-19.5f),.17f);
        Sign(bay,"Seat instruction",8,V(13.3f,2.80f,-20.19f),.17f);
        var consist=WorldAuthor.Child(room,"Reciprocating local");
        var mesh=new LandmarkGeometry();Carriage(mesh,quest);
        var first=mesh.Save(consist,folder,"Local-Carriage");
        var doorDesign=new LandmarkGeometry();DoorLeaf(doorDesign);
        var left=doorDesign.Save(first.transform,folder,"Pocket door");
        left.name="Door aft";var right=UnityEngine.Object.Instantiate(left,first.transform);right.name="Door forward";
        var palette=new Dictionary<Material,Material>();
        Tint(first,folder,quest,palette);Tint(left,folder,quest,palette);Tint(right,folder,quest,palette);
        left.transform.localPosition=V(-1.54f,0,-1.49f);right.transform.localPosition=V(-1.54f,0,1.49f);
        left.AddComponent<BoxCollider>().center=V(0,1.47f,0);
        left.GetComponent<BoxCollider>().size=V(.10f,2.2f,.97f);
        right.AddComponent<BoxCollider>().center=V(0,1.47f,0);
        right.GetComponent<BoxCollider>().size=V(.10f,2.2f,.97f);
        Collider(first.transform,"Car floor",V(0,.305f,0),V(3.14f,.17f,7.45f));
        Collider(first.transform,"Far wall",V(1.555f,1.50f,0),V(.10f,2.45f,7.4f));
        foreach(int sign in new[]{-1,1}){
            Collider(first.transform,"Vestibule end",V(0,1.52f,sign*3.70f),V(3.08f,2.40f,.10f));
            Collider(first.transform,"Pocket wall",V(-1.55f,1.52f,sign*2.36f),V(.1f,2.4f,2.73f));
        }
        var coaches=new Transform[3];
        for(int i=0;i<3;i++){
            var car=i==0?first:UnityEngine.Object.Instantiate(first,consist);
            car.name="Coach "+(i+1);
            // Clone ONLY the shared shells before attaching any Udon or seat instances.
            coaches[i]=car.transform;
            float s=-23f+8*i;
            car.transform.localPosition=Track(s);car.transform.localRotation=Quaternion.Euler(Angles(s));
        }
        var anim=MakeAnimation(consist,coaches,folder);
        dispatch.coaches=coaches; dispatch.platformExit=exit; dispatch.displayPanels=panels; dispatch.service=anim;
        UdonSharpEditorUtility.CopyProxyToUdon(dispatch);
        EditorUtility.SetDirty(vm);
        for(int i=0;i<coaches.Length;i++){
            var landing=Point(bay,"Coach "+(i+1)+" exit",V(14.0f,.54f,-23f+8*i));
            foreach(int side in new[]{-1,1}) Seat(coaches[i],side,dispatch,landing);
            Sign(coaches[i],"Carriage plate",9,V(0,2.70f,3.603f),.16f);
        }
    }

    static void Platform(LandmarkGeometry g,Transform bay)
    {
        var center=V(13.08f,Deck/2,-15f);
        g.Box("tile",center,V(4.48f,Deck,25.8f));
        Collider(bay,"Boarding island",center,V(4.48f,Deck,25.8f));
        g.Box("ochre",V(15.11f,Deck+.012f,-15),V(.37f,.025f,25.76f));
        for(int i=0;i<36;i++)
            g.Box("black",V(15.10f,Deck+.028f,-27.55f+i*.70f),V(.19f,.014f,.17f));
        // 3.5 m ramp along the west side; level running surface continues across the sill.
        Vector3 from=V(7.24f,.012f,-15),to=V(10.85f,Deck,-15);
        Quaternion tilt=Quaternion.Euler(0,0,Mathf.Atan2(to.y-from.y,to.x-from.x)*Mathf.Rad2Deg);
        Vector3 pos=(from+to)/2-tilt*Vector3.up*.12f,size=V(Vector3.Distance(from,to)+.025f,.24f,2.84f);
        g.frame=Matrix4x4.TRS(pos,tilt,Vector3.one);g.Box("concrete",Vector3.zero,size);g.frame=Matrix4x4.identity;
        Transform ramp=Point(bay,"Step-free boarding ramp",pos);ramp.localRotation=tilt;ramp.gameObject.AddComponent<BoxCollider>().size=size;
        g.Box("teal",V(11.65f,.99f,-19.2f),V(.74f,1.07f,.47f));
        g.Box("metal",V(11.65f,1.63f,-19.2f),V(1.02f,.87f,.55f));
        g.Box("amber",V(11.65f,1.50f,-19.489f),V(.36f,.13f,.04f));
        g.Box("black",V(13.3f,3.13f,-20.09f),V(4.07f,1.06f,.15f));
        g.Box("brass",V(13.3f,3.67f,-20.09f),V(4.1f,.055f,.16f));
        for(int i=-1;i<=1;i+=2)g.Box("metal",V(13.3f+i*1.93f,1.56f,-20.02f),V(.107f,2.28f,.13f));
    }
    static GameObject Sign(Transform parent,string name,int row,Vector3 p,float height)
    {
        // Printed metal faces use the opaque, backface-culled texture pass. Unity's
        // legacy font material otherwise renders reversed letters through walls.
        string path=TypeFolder+"/Lettering "+row+".mat";
        var ink=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!ink){
            ink=new Material(Shader.Find("Unlit/Texture"));
            AssetDatabase.CreateAsset(ink,path);
        }
        int width=TypeWidths[row];
        ink.mainTexture=AssetDatabase.LoadAssetAtPath<Texture>(TypeFolder+"/Platform lettering.png");
        ink.mainTextureScale=new Vector2(width/1024f,62/1024f);
        ink.mainTextureOffset=new Vector2(.5f-width/2048f,1-(row*64+63)/1024f);
        EditorUtility.SetDirty(ink);
        var face=GameObject.CreatePrimitive(PrimitiveType.Quad);
        face.name=name;face.transform.SetParent(parent,false);face.transform.localPosition=p;
        face.transform.localScale=V(height*width/62f,height,1);
        UnityEngine.Object.DestroyImmediate(face.GetComponent<MeshCollider>());
        var renderer=face.GetComponent<MeshRenderer>();
        renderer.sharedMaterial=ink;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        return face;
    }
    static void Seat(Transform coach,int sign,ApertureExpress dispatch,Transform landing)
    {
        var t=Point(coach,sign<0?"Window seat A":"Window seat B",V(.69f,.78f,sign*1.92f));
        t.gameObject.AddComponent<BoxCollider>().size=V(.91f,.35f,.69f);
        var station=t.gameObject.AddComponent<VRCStation>();
        station.seated=true;station.disableStationExit=false;station.canUseStationFromStation=false;
        station.PlayerMobility=VRC.SDKBase.VRCStation.Mobility.ImmobilizeForVehicle;
        station.stationEnterPlayerLocation=Point(t,"Seat pivot",V(0,.065f,.05f));
        station.stationExitPlayerLocation=landing;
        var use=t.gameObject.AddUdonSharpComponent<ExpressSeat>();
        use.service=dispatch;use.seat=station;UdonSharpEditorUtility.CopyProxyToUdon(use);
        var vm=UdonSharpEditorUtility.GetBackingUdonBehaviour(use);vm.interactText="Sit and ride";vm.proximity=2.5f;
        EditorUtility.SetDirty(vm);
    }
    static void DoorLeaf(LandmarkGeometry g)
    {
        g.Box("teal",V(0,.91f,0),V(.10f,1.08f,.97f));
        g.Box("ivory",V(0,2.56f,0),V(.10f,.25f,.97f));
        foreach(int s in new[]{-1,1})g.Box("metal",V(0,1.96f,s*.435f),V(.10f,1.10f,.10f));
        g.Box("brass",V(.062f,1.57f,0),V(.048f,.52f,.035f));
    }

    // Three rounded sections turn the upholstery around the edge of the foam.
    static void Pad(LandmarkGeometry g,Vector3 position,Vector3 size,float tilt,bool quest)
    {
        var saved=g.frame;
        g.frame*=Frame(position,V(tilt,0,0));
        int steps=quest?2:4;
        int n=4*steps;
        var rings=new Vector3[4,n];
        float radius=Mathf.Min(.105f,size.z*.20f);
        for(int k=0;k<4;k++){
            float inset=(k==0||k==3)?.044f:0;
            float y=size.y*(k==0?-.5f:k==1?-.30f:k==2?.21f:.5f);
            for(int j=0;j<n;j++){
                int quadrant=j/steps;
                float a=(quadrant+(j%steps)/(float)(steps-1))*Mathf.PI*.5f;
                float x=(quadrant==0 || quadrant==3?1:-1)*(size.x*.5f-radius);
                float z=(quadrant<2?1:-1)*(size.z*.5f-radius);
                rings[k,j]=V(x+(radius-inset)*Mathf.Cos(a),y,z+(radius-inset)*Mathf.Sin(a));
            }
        }
        for(int j=0;j<n;j++){
            int next=(j+1)%n;
            g.Quad("ochre",V(0,-size.y*.5f,0),rings[0,j],rings[0,next],rings[0,next]);
            for(int k=0;k<3;k++)g.Quad("ochre",rings[k,j],rings[k+1,j],rings[k+1,next],rings[k,next]);
            g.Quad("ochre",V(0,size.y*.5f,0),rings[3,next],rings[3,j],rings[3,j]);
        }
        g.frame=saved;
    }

    static void Carriage(LandmarkGeometry g,bool quest)
    {
        g.Box("metal",V(0,.24f,0),V(3.16f,.28f,7.5f));
        g.Box("black",V(0,.382f,0),V(3.10f,.02f,7.43f));
        g.Box("ivory",V(0,2.95f,0),V(3.16f,.19f,7.54f));
        g.Box("teal",V(0,3.07f,0),V(2.94f,.13f,7.36f));
        foreach(int side in new[]{-1,1}){
            float x=side*1.52f;
            for(int direction=-1;direction<=1;direction+=2){
                float len=side==-1?2.72f:3.71f;
                float mid=side==-1?2.36f:1.855f;
                g.Box("teal",V(x,.81f,direction*mid),V(.12f,.88f,len));
                g.Box("brass",V(x,1.19f,direction*mid),V(.15f,.077f,len));
                g.Box("ivory",V(x,2.70f,direction*mid),V(.14f,.34f,len));
                foreach(float z in new[]{3.63f,2.39f,1.02f})
                    g.Box("metal",V(x,1.88f,direction*z),V(.12f,1.37f,.10f));
                if(!quest){
                    g.Box("brass",V(x-side*.10f,1.31f,direction*mid),V(.09f,.08f,len));
                    for(int j=0;j<7;j++)
                        g.Box("brass",V(x+side*.066f,.53f,direction*(1.12f+j*.40f)),V(.022f,.032f,.035f));
                }
            }
            g.Box("metal",V(x,2.80f,0),V(.22f,.20f,7.45f));
            if(side==1)g.Box("metal",V(x,1.88f,0),V(.13f,1.38f,.12f));
            foreach(float z in new[]{-2.65f,2.65f})
                g.Tube("black",V(side*.78f,.12f,z),V(side*1.16f,.12f,z),.21f,.21f,quest?8:12);
            foreach(float z in new[]{-3.68f,3.68f}){
                if(side==-1)g.Box("teal",V(0,.91f,z),V(2.98f,.96f,.11f));
                g.Box("brass",V(side*1.28f,1.59f,z),V(.19f,.38f,.14f));
                g.Box("metal",V(side*1.40f,2.0f,z),V(.17f,1.20f,.14f));
                if(side==-1)g.Box("ivory",V(0,2.70f,z),V(2.98f,.27f,.13f));
                g.Box("amber",V(side*1.11f,1.1f,z+Mathf.Sign(z)*.08f),V(.34f,.24f,.08f));
            }
        }
        for(int s=-1;s<=1;s+=2){
            float z=s*1.92f;
            g.Box("metal",V(.71f,.53f,z),V(.76f,.26f,.51f));
            Pad(g,V(.69f,.777f,z),V(.99f,.205f,.79f),-3,quest);
            Pad(g,V(.69f,1.195f,z-.34f),V(.98f,.18f,.85f),82,quest);
            g.Tube("brass",V(-1.24f,.39f,s*1.07f),V(-1.24f,2.86f,s*1.07f),.033f,.033f,quest?7:12);
            foreach(float x in new[]{.16f,1.19f}){
                g.Beam("metal",V(x,.75f,z-.29f),V(x,1.04f,z-.29f),.05f,.05f);
                g.Tube("brass",V(x,1.04f,z-.30f),V(x,1.04f,z+.26f),.031f,.035f,quest?7:12);
                if(!quest)g.Tube("black",V(x,1.04f,z-.22f),V(x,1.04f,z+.18f),.042f,.042f,10);
            }
            g.Box("metal",V(0,2.827f,z),V(.35f,.10f,2.38f));
            foreach(float x in new[]{-.079f,.079f})
                g.Tube("amber",V(x,2.782f,z-1.08f),V(x,2.782f,z+1.08f),.027f,.027f,quest?5:10);
            if(!quest)for(int k=-1;k<=1;k++)g.Box("ivory",V(0,2.754f,z+k*1.075f),V(.26f,.029f,.053f));
            g.Box("metal",V(0,3.22f,z),V(1.22f,.21f,1.04f));
            if(!quest)for(int j=-3;j<=3;j++)g.Box("black",V(0,3.34f,z+j*.12f),V(1.04f,.02f,.045f));
        }
        g.Beam("brass",V(-.82f,2.60f,-3.38f),V(-.82f,2.60f,3.38f),.045f,.045f);
        for(int j=0;j<(quest?3:6);j++){
            float z=-2.9f+j*(quest?2.3f:1.1f);
            g.Beam("black",V(-.82f,2.60f,z),V(-.82f,2.37f,z),.045f,.045f);
            Vector3 top=V(-.82f,2.38f,z);
            g.Beam("brass",top,top+V(-.10f,-.16f,0),.03f,.03f);
            g.Beam("brass",top+V(-.10f,-.16f,0),top+V(.10f,-.16f,0),.03f,.03f);
            g.Beam("brass",top+V(.10f,-.16f,0),top,.03f,.03f);
        }
    }
    static void Tint(GameObject car,string folder,bool quest,Dictionary<Material,Material> variants)
    {
        var renderer=car.GetComponent<MeshRenderer>();var materials=renderer.sharedMaterials;
        for(int i=0;i<materials.Length;i++){
            var original=materials[i];
            if(original.name=="amber"||original.name=="ice")continue;
            if(!variants.TryGetValue(original,out var local)){
                string path=folder+"/Finishes/Rail "+original.name+".mat";
                local=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(!local){local=new Material(Shader.Find("Stillwater/Carriage enamel"));AssetDatabase.CreateAsset(local,path);}
                local.SetColor("_Color",original.GetColor("_Color"));
                int finish=original.name=="ochre"?1:original.name=="black"?2:original.name=="brass"?3:0;
                local.SetFloat("_Finish",finish);
                local.SetFloat("_Gloss",finish==1?.11f:finish==2?.23f:finish==3?.88f:.49f);
                if(finish==1)local.SetTexture("_Moquette",AssetDatabase.LoadAssetAtPath<Texture>(Surface.Media+"/Surfaces/Platform moquette.jpg"));
                if(!quest)local.SetTexture("_Wear",AssetDatabase.LoadAssetAtPath<Texture>(Surface.Media+"/Surfaces/WeatherField.png"));
                if(quest)local.EnableKeyword("RAIL_MOBILE");else local.DisableKeyword("RAIL_MOBILE");
                local.enableInstancing=true;EditorUtility.SetDirty(local);variants[original]=local;
            }
            materials[i]=local;
        }
        renderer.sharedMaterials=materials;
    }
    static float Travel(float t)
    {
        if(t<ApertureExpress.Depart)return 0;
        if(t<ApertureExpress.Summit)return Mathf.SmoothStep(0,Run,(t-ApertureExpress.Depart)/(ApertureExpress.Summit-ApertureExpress.Depart));
        if(t<ApertureExpress.LeaveSummit)return Run;
        return Mathf.SmoothStep(Run,0,(t-ApertureExpress.LeaveSummit)/(ApertureExpress.Dock-ApertureExpress.LeaveSummit));
    }
    static void Key(AnimationClip clip,string path,string property,AnimationCurve curve)
        =>AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(path,typeof(Transform),property),curve);

    static Animator MakeAnimation(Transform consist,Transform[] coaches,string folder)
    {
        string cp=folder+"/SpiralService.controller",ap=folder+"/SpiralService.anim";
        var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(ap);
        if(!clip){clip=new AnimationClip();AssetDatabase.CreateAsset(clip,ap);}
        clip.ClearCurves();clip.name="Aperture round trip";clip.frameRate=30;
        for(int car=0;car<coaches.Length;car++){
            string name=coaches[car].name;
            var curves=new AnimationCurve[5];
            for(int i=0;i<curves.Length;i++)curves[i]=new AnimationCurve();
            for(int j=0;j<=Mathf.RoundToInt(ApertureExpress.Cycle*2);j++){
                float t=j*.5f,s=-23+car*8+Travel(t);
                Vector3 pos=Track(s),angles=Angles(s);
                float[] v={pos.x,pos.y,pos.z,angles.x,angles.y};
                for(int i=0;i<curves.Length;i++)curves[i].AddKey(t,v[i]);
            }
            string[] prop={"m_LocalPosition.x","m_LocalPosition.y","m_LocalPosition.z","localEulerAnglesRaw.x","localEulerAnglesRaw.y"};
            for(int i=0;i<curves.Length;i++){
                for(int j=0;j<curves[i].length;j++)curves[i].SmoothTangents(j,0);
                Key(clip,name,prop[i],curves[i]);
            }
            Key(clip,name,"localEulerAnglesRaw.z",AnimationCurve.Constant(0,ApertureExpress.Cycle,0));
            for(int dir=-1;dir<=1;dir+=2){
                var door=new AnimationCurve();
                foreach(float t in new[]{0f,ApertureExpress.Depart-5,ApertureExpress.Depart-1,ApertureExpress.Dock,ApertureExpress.Dock+2.5f,ApertureExpress.Cycle}){
                    bool shut=t>=ApertureExpress.Depart-1 && t<=ApertureExpress.Dock;
                    door.AddKey(new Keyframe(t,dir*(shut?.49f:1.49f),0,0));
                }
                string path=name+(dir==-1?"/Door aft":"/Door forward");
                Key(clip,path,"m_LocalPosition.x",AnimationCurve.Constant(0,ApertureExpress.Cycle,-1.54f));
                Key(clip,path,"m_LocalPosition.y",AnimationCurve.Constant(0,ApertureExpress.Cycle,0));
                Key(clip,path,"m_LocalPosition.z",door);
            }
        }
        var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=true;
        AnimationUtility.SetAnimationClipSettings(clip,settings);EditorUtility.SetDirty(clip);
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(cp);
        if(!controller)controller=AnimatorController.CreateAnimatorControllerAtPath(cp);
        var machine=controller.layers[0].stateMachine;
        var state=machine.defaultState;
        if(!state)state=machine.AddState("Aperture round trip");
        state.name="Aperture round trip";state.motion=clip; state.writeDefaultValues=false;machine.defaultState=state;
        EditorUtility.SetDirty(state);EditorUtility.SetDirty(machine);EditorUtility.SetDirty(controller);
        var animator=consist.gameObject.AddComponent<Animator>();animator.runtimeAnimatorController=controller;
        animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;return animator;
    }
}
