using UnityEngine;
using static LandmarkGeometry;

// Tile roofs have a rising hip, a concave field and a kicked-up corner at each bell.
internal static class OrchardTower
{
    public static void Build(Transform grove,string folder,bool mobile)
    {
        var tower=WorldAuthor.Child(grove,"Five-storey pagoda");
        tower.localPosition=V(12,0,13);tower.localRotation=Quaternion.Euler(0,-12,0);
        var g=new LandmarkGeometry();
        var baseMesh=new StationMesh(tower,folder+"/Architecture");
        for(int i=0;i<5;i++){
            float w=14.4f-i*.53f;float level=(i+1)*.145f;
            baseMesh.Box("terrazzo",0,level*.5f,0,w,level,w,true);
        }
        float[] widths={5,4.12f,3.43f,2.80f,2.18f}, floor={.73f,8.12f,14.51f,20.29f,25.59f};
        float[] heights={4.6f,3.65f,3.31f,3.02f,2.65f};
        for(int level=0;level<5;level++){
            float y=floor[level], half=widths[level],h=heights[level];
            for(int face=0;face<4;face++){
                g.frame=Frame(V(0,y,0),V(0,face*90,0));
                g.Box("cedar",V(0,.11f,-half-.21f),V(2*half+.56f,.22f,.64f));
                g.Box("vermilion",V(0,h-.13f,-half),V(2*half+.8f,.36f,.38f));
                g.Box("brass",V(0,h-.42f,-half-.2f),V(2*half+.66f,.065f,.057f));
                int bays=level<2?4:3;
                for(int j=0;j<bays;j++){
                    float bay=half*2/bays,x=-half+(j+.5f)*bay;
                    g.Tube("vermilion",V(x-bay/2,.1f,-half),V(x-bay/2,h,-half),.17f,.145f,mobile?6:10);
                    bool openDoor=level==0 && (face==0||face==2) && (j==1||j==2);
                    if(!openDoor){
                        g.Box("shoji",V(x,h*.50f,-half+.20f),V(bay-.18f,h-.53f,.10f));
                        for(int t=1;t<(mobile?3:5);t++)
                            g.Box("cedar",V(x-bay*.5f+t*bay/(mobile?3:5),h*.50f,-half+.133f),V(.046f,h-.50f,.065f));
                        for(int t=0;t<4;t++)g.Box("cedar",V(x,.63f+t*(h-.95f)/4,-half+.13f),V(bay-.11f,.055f,.075f));
                        if(level==0){
                            baseMesh.Frame=Quaternion.Euler(0,face*90,0);
                            baseMesh.Solid(V(x,y+h*.5f,-half+.18f),V(bay-.12f,h,.13f),Quaternion.identity,"Ground floor screen");baseMesh.ResetFrame();
                        }
                    }
                    if(level!=0){
                        g.Box("vermilion",V(x,1.03f,-half-.61f),V(bay+.07f,.12f,.16f));
                        g.Box("cedar",V(x,.48f,-half-.61f),V(bay,.10f,.1f));
                        g.Box("vermilion",V(x-bay/2,.61f,-half-.61f),V(.12f,1.16f,.13f));
                    }
                    for(int u=0;u<(mobile?1:2);u++){
                        float bx=x+(u-.5f)*.35f;
                        g.Box("cedar",V(bx,h+.13f,-half-.31f),V(.25f,.23f,1.04f));
                        g.Box("vermilion",V(bx,h+.36f,-half-.49f),V(.52f,.18f,1.28f));
                    }
                }
                if(level==0){
                    g.Tube("cedar",V(half-.72f,3.3f,-half-.66f),V(half-.72f,4.6f,-half-.66f),.028f,.028f,6);
                    g.Crown("amber",V(half-.72f,3.22f,-half-.66f),V(.28f,.43f,.28f),202+face,12,6);
                }
                RoofFace(g,half,h,mobile);
            }
        }
        g.frame=Matrix4x4.identity;
        float start=31.45f;
        g.Tube("brass",V(0,start-.4f,0),V(0,40.8f,0),.14f,.035f,mobile?8:16);
        for(int i=0;i<9;i++){
            float y=start+.77f+i*.57f, rr=1.24f-i*.095f;
            g.Tube("brass",V(0,y-.07f,0),V(0,y,0),rr*.62f,rr,mobile?12:24);
            g.Tube("brass",V(0,y,0),V(0,y+.11f,0),rr,rr*.71f,mobile?12:24);
        }
        g.Crown("brass",V(0,32.09f,0),V(.78f,.66f,.78f),18,16,8);
        g.Crown("amber",V(0,39.83f,0),V(.16f,.24f,.16f),13,10,6);
        g.Save(tower,folder,"Pagoda roof and timber");baseMesh.Finish();
        var street=WorldAuthor.Child(grove,"Garden lantern walk");var detail=new LandmarkGeometry();
        // Rain-washed flags lead from the torii procession to the pagoda steps.
        for(int i=0;i<17;i++){
            float t=i/16f,curve=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.45f,1,t));
            float x=12*curve,z=-26+32*t;
            float dx=12*(Mathf.SmoothStep(0,1,Mathf.InverseLerp(.45f,1,t+.015f))-curve);
            float turn=Mathf.Atan2(dx,.48f)*Mathf.Rad2Deg;
            detail.frame=Frame(V(x,0,z),V(0,turn,0));
            for(int side=-1;side<=1;side+=2)
                detail.Box("garden flags",V(side*.64f,.029f,0),V(1.21f,.058f,1.85f));
        }
        detail.frame=Matrix4x4.identity;
        for(int row=0;row<6;row++){
            float z=-19+row*4.0f;
            foreach(int side in new[]{-1,1}){
                float x=side*(5.2f+.32f*row);
                detail.Box("terrazzo",V(x,.15f,z),V(1.03f,.30f,1.03f));
                detail.Tube("terrazzo",V(x,.21f,z),V(x,1.46f,z),.32f,.21f,8);
                detail.Box("terrazzo",V(x,1.46f,z),V(.84f,.18f,.84f));
                detail.Box("amber",V(x,1.82f,z),V(.51f,.55f,.51f));
                foreach(int a in new[]{-1,1})foreach(int b in new[]{-1,1})
                    detail.Box("black",V(x+a*.28f,1.82f,z+b*.28f),V(.08f,.61f,.08f));
                detail.Tube("slate roof",V(x,2.07f,z),V(x,2.40f,z),.79f,.18f,4);
            }
        }
        detail.Save(street,folder,"Stone lanterns");
        var mast=WorldAuthor.Child(grove,"Broadcast tower beyond the garden");
        mast.localPosition=V(42,0,50);BroadcastTower(mast,folder,mobile);
    }
    static Vector3 Eave(float u,float f,float half,float h)
    {
        float inner=half*.48f,outer=half+2.04f;
        float radius=Mathf.Lerp(inner,outer,f);
        float height=h+.52f+2.65f*(1-f)*(1-f)+.93f*Mathf.Pow(Mathf.Abs(u),4)*f*f;
        return V(u*radius,height,-radius);
    }
    static void RoofFace(LandmarkGeometry g,float half,float h,bool mobile)
    {
        int columns=mobile?8:16,rows=mobile?3:6;
        for(int j=0;j<columns;j++){
            float u=-1+2f*j/columns,v=-1+2f*(j+1)/columns;
            for(int k=0;k<rows;k++){
                float t=(float)k/rows,s=(float)(k+1)/rows;
                var a=Eave(u,t,half,h);var b=Eave(v,t,half,h);var c=Eave(v,s,half,h);var d=Eave(u,s,half,h);
                g.Quad("slate roof",a,b,c,d);
                g.Quad("cedar",d-Vector3.up*.21f,c-Vector3.up*.21f,b-Vector3.up*.21f,a-Vector3.up*.21f);
                if(k==rows-1){
                    g.Quad("vermilion",d,c,c-Vector3.up*.21f,d-Vector3.up*.21f);
                    g.Beam("brass",d-Vector3.up*.06f,c-Vector3.up*.06f,.042f,.055f);
                }
                if(!mobile && (j%2==0||j==columns-1))g.Beam("slate roof",a+Vector3.up*.05f,d+Vector3.up*.05f,.085f,.09f);
                if(j==0)g.Beam("brass",a+Vector3.up*.09f,d+Vector3.up*.09f,.13f,.14f);
            }
        }
        var tip=Eave(-1,1,half,h);
        g.Tube("brass",tip,tip-Vector3.up*.64f,.03f,.03f,6);
        g.Tube("brass",tip-Vector3.up*.58f,tip-Vector3.up*.91f,.08f,.18f,8);
    }
    static void BroadcastTower(Transform root,string folder,bool mobile)
    {
        var g=new LandmarkGeometry();float[] height={0,7,14,23,31,40,49,55,61,66};
        float[] width={7.8f,6.2f,4.6f,3.3f,2.5f,1.8f,1.21f,.69f,.38f,.15f};
        for(int f=0;f<4;f++){
            g.frame=Frame(Vector3.zero,V(0,90*f,0));
            for(int i=0;i<height.Length-1;i++){
                string paint=i>=3 && (i%3==0||i==7)?"ivory":"vermilion";
                var a=V(-width[i],height[i],-width[i]);var b=V(width[i],height[i],-width[i]);
                var c=V(width[i+1],height[i+1],-width[i+1]);var d=V(-width[i+1],height[i+1],-width[i+1]);
                g.Beam(paint,a,d,.38f,.4f);g.Beam(paint,a,b,.22f,.25f);
                g.Beam(paint,a,c,.16f,.18f);g.Beam(paint,b,d,.16f,.18f);
                if(!mobile && i<5)g.Beam(paint,(a+d)*.5f,(b+c)*.5f,.14f,.17f);
            }
        }
        g.frame=Matrix4x4.identity;
        foreach(float y in new[]{30f,51f}){
            float w=y<40?8:3.6f;
            g.Box("ivory",V(0,y,0),V(w,.57f,w));g.Box("glass",V(0,y+.97f,0),V(w*.96f,1.43f,w*.96f));
            g.Box("amber",V(0,y+1.75f,0),V(w,.14f,w));g.Box("vermilion",V(0,y+2.07f,0),V(w+.16f,.42f,w+.16f));
        }
        g.Crown("red",V(0,66.3f,0),V(.16f,.34f,.16f),41,8,4);
        g.Save(root,folder,"Broadcast tower steelwork");
    }
}
