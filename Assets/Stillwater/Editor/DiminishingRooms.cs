using UnityEngine;

// Both branches have a level return path. The room, its fittings and the distance
// between successive doorframes all contract together, down to a sealed miniature.
internal static class DiminishingRooms
{
    static readonly float[] RelativeWidth={1,.824f,.675f,.549f,.442f,.343f,.255f,.171f,.093f,.047f};
    static readonly float[] RelativeHeight={1,.858f,.727f,.624f,.529f,.448f,.365f,.275f,.18f,.094f};
    static readonly float[] Length={8.5f,7.55f,6.6f,5.65f,4.75f,4.05f,3.4f,2.75f,2.1f,1.55f};
    internal static Transform Install(Transform root,string folder,bool bath,bool mobile)
    {
        var wing=WorldAuthor.Child(root,bath?"Diminishing Baths":"Night Reception");
        wing.localPosition=bath?new Vector3(0,0,-133):new Vector3(-123,0,-80);
        wing.localRotation=Quaternion.Euler(0,bath?180:-90,0);
        float start=bath?-25:-19, fullWidth=bath?16.8f:9.6f, fullHeight=bath?7.2f:4.45f;
        float previousWidth=fullWidth,previousHeight=fullHeight;
        for(int i=0;i<Length.Length;i++){
            float len=Length[i]*(bath?1:.82f), width=fullWidth*RelativeWidth[i], height=fullHeight*RelativeHeight[i];
            var bay=WorldAuthor.Child(wing,(bath?"Bath bay ":"Reception bay ")+(i+1).ToString("D2"));
            bay.localPosition=new Vector3(0,0,start+len/2);
            var m=new StationMesh(bay,folder+"/Architecture");
            float near=-len/2,far=len/2;
            m.Box(bath?"porcelain":"terrazzo",0,-.14f,0,width+.44f,.28f,len+.015f,true);
            m.Box(bath?"limestone":"cedar",0,height+.15f,0,width+.44f,.3f,len+.015f);
            m.Solid(new Vector3(0,height+.15f,0),new Vector3(width+.44f,.3f,len+.015f),Quaternion.identity,"Ceiling clearance");
            foreach(int side in new[]{-1,1}){
                float x=side*(width/2+.12f);
                m.Box(bath?"bath tile":"limestone",x,height/2,0,.24f,height,len+.01f);
                m.Solid(new Vector3(x,height/2,0),new Vector3(.24f,height,len+.01f),Quaternion.identity,side<0?"Left boundary":"Right boundary");
                if(i>0){
                    float w=(previousWidth-width)/2;
                    m.Box(bath?"porcelain":"cedar",side*(width+w)/2,previousHeight/2,near,w+.015f,previousHeight,.18f,true);
                }
                float low=height*(bath?.32f:.36f);
                m.Box(bath?"porcelain":"cedar",side*(width/2-.025f),low/2,0,.08f,low,len);
                m.Box(bath?"teal":"brass",side*(width/2-.073f),low+.022f,0,.038f,.056f,len);
            }
            if(i>0)m.Box(bath?"bath tile":"cedar",0,(previousHeight+height)/2,near,width,previousHeight-height+.015f,.18f,true);
            if(i==0){
                // Fill the two return faces either side of the doorway back to the release map.
                float entry=9,sideWidth=(width-entry)/2;
                if(sideWidth>.04f)foreach(int side in new[]{-1,1})m.Box("limestone",side*(entry+sideWidth)/2,height/2,near,sideWidth,height,.18f,true);
                float doorH=bath?5.6f:3.05f;
                if(height>doorH)m.Box("limestone",0,(height+doorH)/2,near,entry,height-doorH,.18f,true);
            }
            if(bath) Bath(m,width,height,len,i,mobile);else Reception(m,width,height,len,i,mobile);
            if(i==Length.Length-1){
                m.Box(bath?"bath tile":"cedar",0,height/2,far,width+.24f,height,.24f);
                m.Solid(new Vector3(0,height/2,far),new Vector3(width+.24f,height,.24f),Quaternion.identity,"Closed end");
                m.Box(bath?"amber":"red",0,height*.53f,far-.13f,width*.13f,.025f,.026f);
            }
            m.Finish();
            if(mobile){
                var renderer=bay.Find("Architecture").GetComponent<MeshRenderer>();
                var group=bay.gameObject.AddComponent<LODGroup>();
                group.SetLODs(new[]{new LOD(.05f,new Renderer[]{renderer})});
                group.localReferencePoint=Vector3.up*height*.5f;
                group.size=Mathf.Max(6f,width);
            }
            previousWidth=width;previousHeight=height;start+=len;
        }
        Surface.Ambience(wing,bath?"baths":"annex",mobile);
        return wing;
    }
    static void Bath(StationMesh m,float width,float h,float len,int bay,bool mobile)
    {
        float s=h/7.2f;
        // At each threshold, a barrel-vault rib describes a slightly smaller ellipse.
        for(int cross=0;cross<(bay<4?2:1);cross++){
            float z=cross==0?-len*.43f:len*.40f;
            int arcs=mobile?12:24;
            for(int j=0;j<arcs;j++){
                float a=(float)j/arcs*Mathf.PI,b=(j+1f)/arcs*Mathf.PI;
                Vector3 p=new Vector3(Mathf.Cos(a)*width*.485f,h*(.53f+.45f*Mathf.Sin(a)),z);
                Vector3 q=new Vector3(Mathf.Cos(b)*width*.485f,h*(.53f+.45f*Mathf.Sin(b)),z);
                m.Rod("porcelain",p,q,s*.13f,mobile?4:6);
            }
            foreach(int side in new[]{-1,1}){
                float x=side*width*.485f;
                m.Box("porcelain",x,h*.269f,z,.24f*s,h*.54f,.31f*s);
                m.Box("teal",x,h*.53f,z,.35f*s,.12f*s,.39f*s);
            }
        }
        foreach(int side in new[]{-1,1}){
            float x=side*(width*.36f);
            m.Box("wet",x,.008f,0,width*.185f,.014f,len-.035f);
            m.Box("brass",side*width*.263f,.026f,0,.032f*s,.04f*s,len);
            m.Box("ivory",x,h*.805f,0,.31f*s,.14f*s,Mathf.Min(len*.78f,3.6f*s));
            m.Box("ice",x,h*.790f,0,.19f*s,.07f*s,Mathf.Min(len*.75f,3.46f*s));
            float lampY=h*.79f;
            foreach(int end in new[]{-1,1})m.Rod("metal",new Vector3(x,lampY,end*len*.12f),new Vector3(x,h,end*len*.12f),.023f*s,5);
            if(bay>=6)continue;
            // The brass shower, floor drain and porcelain ledge provide a clear familiar scale.
            float tap=side*(width/2-.18f*s);
            m.Rod("brass",new Vector3(tap,h*.15f,.2f),new Vector3(tap,h*.46f,.2f),.043f*s);
            m.Rod("brass",new Vector3(tap,h*.46f,.2f),new Vector3(tap-side*.38f*s,h*.46f,.2f),.052f*s);
            m.Box("metal",tap-side*.4f*s,h*.445f,.2f,.42f*s,.085f*s,.31f*s);
            m.Box("brass",tap-side*.09f*s,h*.18f,.2f,.17f*s,.10f*s,.48f*s);
            m.Box("porcelain",tap-side*.17f*s,.74f*s,-len*.25f,.5f*s,.12f*s,1.14f*s);
            if(!mobile)for(int k=0;k<7;k++)
                m.Box("metal",x,.032f,.14f*k-.45f,.27f*s,.03f,.042f*s);
        }
    }
    static void Reception(StationMesh m,float width,float h,float len,int bay,bool mobile)
    {
        float s=h/4.45f;
        float lightLen=Mathf.Min(2.5f*s,len*.6f);
        m.Box("metal",0,h-.085f*s,0,1.1f*s,.14f*s,lightLen);
        m.Box("amber",0,h-.16f*s,0,.85f*s,.035f*s,lightLen-.07f*s);
        foreach(int side in new[]{-1,1}){
            float x=side*(width*.496f);
            m.Box("cedar",x,h*.33f,.12f,.12f*s,h*.66f,1.4f*s);
            m.Box("brass",x-side*.082f*s,h*.38f,.55f*s,.105f*s,.065f*s,.27f*s);
            m.Box("paper",x-side*.09f*s,h*.54f,.08f,.04f*s,.25f*s,.31f*s);
        }
        if(bay<7){
            float x=width*.32f;
            m.Box("cedar",x,.47f*s,-.15f,1.55f*s,.15f*s,.72f*s,true);
            m.Box("cedar",x,.87f*s,.165f*s,1.55f*s,.62f*s,.13f*s);
            foreach(int side in new[]{-1,1}){
                m.Box("black",x+side*.61f*s,.21f*s,-.16f*s,.11f*s,.42f*s,.51f*s);
                m.Box("brass",x+side*.73f*s,.73f*s,-.17f*s,.065f*s,.08f*s,.64f*s);
            }
            float tx=-width*.39f;
            m.Box("cedar",tx,.57f*s,0,.67f*s,1.14f*s,.81f*s,true);
            m.Box("terrazzo",tx,1.18f*s,0,.87f*s,.12f*s,1.03f*s);
            m.Box("vermilion",tx,1.30f*s,0,.32f*s,.11f*s,.25f*s);
            m.Rod("black",new Vector3(tx-.22f*s,1.43f*s,.035f*s),new Vector3(tx+.22f*s,1.43f*s,.035f*s),.065f*s,8);
            foreach(int q in new[]{-1,1})m.Box("black",tx+q*.20f*s,1.40f*s,.02f*s,.11f*s,.085f*s,.15f*s);
        }
    }
}
