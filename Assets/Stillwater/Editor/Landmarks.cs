using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using static LandmarkGeometry;

public static class Landmarks
{
    public static void Populate(Transform room,string id,bool quest)
    {
        string folder="Assets/Stillwater/Generated/"+(quest?"Quest":"PC");
        Directory.CreateDirectory(folder);
        var g=new LandmarkGeometry();
        switch(id)
        {
            case "rail": ExpressLine.Install(g,room,folder,quest); break;
            case "air": Hangar(g,quest); break;
            case "grove": Grove(g,quest); break;
            case "motor": MotorPool(g,room,quest); break;
            case "gravity": Gravity(g,quest); break;
            case "observatory": Observatory(g,quest); break;
            case "hub": Exchange(g,room,quest); break;
        }
        g.Save(room,folder,"Landmark-"+id);
    }

    static void Hangar(LandmarkGeometry g,bool quest)
    {
        // A long transit jet suspended in landing attitude; outer boarding bridges
        // nearly touch the cabin. The wing edge still reads from directly underneath.
        g.frame=Frame(V(-1,12.8f,0),V(-5,27,6));
        Airliner(g,quest);
        g.frame=Matrix4x4.identity;
        // Sawtooth ribs outline the dimensions of the hangar even in low light.
        for(int i=-2;i<=2;i++)
        {
            float z=i*9.7f;
            foreach(int side in new[]{-1,1})
            {
                g.Beam("metal",V(side*26,2,z),V(side*26,19,z),0.44f,0.5f);
                g.Beam("concrete",V(side*26,18,z),V(side*8,25,z),0.65f,0.53f);
                g.Beam("metal",V(side*26,14,z),V(side*8,25,z),0.19f,0.23f);
                for(int j=1;j<=3;j++)
                {
                    float f=j/4f;
                    Vector3 top=Vector3.Lerp(V(side*26,18,z),V(side*8,25,z),f);
                    Vector3 bottom=Vector3.Lerp(V(side*26,14,z),V(side*8,25,z),f-0.18f);
                    g.Beam("brass",bottom,top,0.11f,0.14f);
                }
                g.Box("ice",V(side*17,21.3f,z),V(2.45f,0.16f,0.31f));
            }
        }
        for(int i=0;i<2;i++)
        {
            float z=i==0?-12:11;
            g.Box("metal",V(-18.7f,10,z),V(14,1.15f,2.85f));
            g.Box("glass",V(-18.7f,11.47f,z),V(14,1.89f,2.62f));
            g.Box("ivory",V(-18.7f,12.58f,z),V(14.2f,0.25f,2.9f));
            for(int j=0;j<9;j++)
                g.Box("teal",V(-25+j*1.55f,11.48f,z),V(0.13f,1.96f,2.72f));
        }
    }

    static void Airliner(LandmarkGeometry g,bool quest)
    {
        const float half=24;
        var s=new[]{V(0.04f,0.08f,-half),V(0.53f,0.65f,-21.5f),V(1.35f,1.40f,-16.8f),
            V(1.70f,1.69f,-11.7f),V(1.76f,1.75f,12.8f),V(1.64f,1.55f,17.6f),
            V(1.22f,1.15f,20.5f),V(0.72f,0.71f,22.4f),V(0.07f,0.07f,23.7f)};
        g.Hull("ivory",s,new[]{1.03f,0.72f,0.27f,0.08f,0f,-0.07f,-0.20f,-0.29f,-0.34f},quest?12:20);
        for(int side=-1;side<=1;side+=2)
        {
            float x=side;
            var wing=new[]{V(x*1.2f,-0.49f,5),V(x*5.6f,-0.37f,2.5f),V(x*16.9f,0.78f,-7.0f),
                V(x*17.72f,1.69f,-8.47f),V(x*15.93f,0.73f,-8.93f),V(x*6.21f,-0.39f,-4.76f),V(x*1.24f,-0.45f,-5.9f)};
            g.Plate("plaster",wing,0.22f);
            g.Beam("brass",wing[1],wing[2],0.075f,0.12f);
            g.Beam(side>0?"ice":"red",wing[2],wing[3],0.11f,0.19f);
            var stabilizer=new[]{V(x*0.58f,0.6f,-15.5f),V(x*7.67f,1.70f,-20.7f),V(x*7.30f,1.7f,-21.57f),V(x*0.7f,0.8f,-19.8f)};
            g.Plate("ivory",stabilizer,0.16f);
            Matrix4x4 aircraft=g.frame;
            foreach(float engineX in new[]{4.95f,8.10f})
            {
                float z=engineX<6?1.4f:-1.4f;
                g.frame=aircraft*Frame(V(x*engineX,-1.23f,z),Vector3.zero);
                g.Beam("metal",V(0,0.41f,0.8f),V(0,1.09f,-0.7f),0.32f,0.45f);
                g.Hull("teal",new[]{V(0.40f,0.40f,-1.62f),V(0.76f,0.76f,-0.62f),V(0.84f,0.84f,1.25f),V(0.75f,0.75f,1.43f)},new[]{0f,0f,0f,0f},quest?10:16);
                g.Tube("metal",V(0,0,1.25f),V(0,0,1.38f),0.73f,0.74f,quest?10:16);
                g.Tube("black",V(0,0,1.38f),V(0,0,1.394f),0.62f,0.62f,quest?10:16);
                g.Tube("brass",V(0,0,1.397f),V(0,0,1.55f),0.18f,0.08f,8);
                if(!quest)for(int j=0;j<10;j++)
                {
                    float a=j*Mathf.PI/5;
                    g.Beam("metal",V(Mathf.Cos(a+0.4f)*0.20f,Mathf.Sin(a+0.4f)*0.20f,1.404f),V(Mathf.Cos(a)*0.59f,Mathf.Sin(a)*0.59f,1.405f),0.055f,0.065f);
                }
            }
            g.frame=aircraft;
            g.Beam("teal",V(x*1.64f,0.13f,-11.8f),V(x*1.72f,0.09f,12.6f),0.20f,0.18f);
            for(int j=0;j<27;j++)
            {
                float z=-11.1f+j*0.90f;
                g.Box(j%7==3?"ice":"glass",V(x*(1.66f+Mathf.Clamp01((z+11)/24)*0.06f),0.55f,z),V(0.16f,0.46f,0.42f));
            }
            g.Beam("glass",V(x*1.54f,0.67f,17.25f),V(x*0.87f,0.71f,20.56f),0.48f,0.19f);
        }
        Matrix4x4 basis=g.frame;
        g.frame*=Frame(Vector3.zero,V(0,0,-90));
        g.Plate("teal",new[]{V(-0.9f,0,-12.2f),V(-6.73f,0,-20.6f),V(-6.92f,0,-23.18f),V(-1.02f,0,-21.39f)},0.27f);
        g.Plate("amber",new[]{V(-4.76f,0.15f,-18.38f),V(-5.29f,0.15f,-19.05f),V(-5.29f,0.15f,-22.47f),V(-4.76f,0.15f,-22.22f)},0.035f);
        g.frame=basis;
    }

    static float Rand(System.Random r,float min,float max) => min+(float)r.NextDouble()*(max-min);

    static void Grove(LandmarkGeometry g,bool quest)
    {
        var roots=new[]{V(-16,30,1),V(14,30,-9),V(-10,30,-13),V(22,30,-.5f),V(-15,30,16)};
        for(int i=0;i<(quest?4:5);i++)
        {
            var r=new System.Random(918+i*157);
            g.frame=Frame(roots[i],V(0,i*61,180));
            float scale=Rand(r,0.88f,1.12f);
            // Exposed roots run along the ceiling from the point where the world flips.
            for(int j=0;j<7;j++)
            {
                float a=j*0.8976f+Rand(r,-0.12f,0.12f);
                Vector3 root=V(Mathf.Cos(a)*2.5f,0.37f,Mathf.Sin(a)*2.5f);
                g.Tube("bark",V(0,1.0f,0),root,0.48f,0.22f,6);
                g.Tube("bark",root,root*2.05f-Vector3.up*0.50f,0.22f,0.04f,5);
            }
            Branch(g,r,Vector3.zero,V(0.10f,1,0.045f).normalized,5.7f*scale,0.59f,quest?3:4,quest);
        }
        g.frame=Matrix4x4.identity;
        // The pedestrian sequence passes three ordinary gates before perspective breaks.
        for(int i=0;i<3;i++)
        {
            g.frame=Frame(V(0,0,-17+i*5.65f),Vector3.zero);
            Torii(g,1f-i*0.045f,quest);
        }
        g.frame=Frame(V(3.2f,16.8f,1),V(0,15,92)); Torii(g,1.12f,quest);
        g.frame=Frame(V(1.5f,23,6.5f),V(0,-9,180)); Torii(g,1.3f,quest);
        g.frame=Frame(V(-16.5f,0,13),V(0,-32,0)); Shrine(g,quest);
        g.frame=Matrix4x4.identity;
    }

    static void Branch(LandmarkGeometry g,System.Random r,Vector3 a,Vector3 direction,float length,float radius,int depth,bool quest)
    {
        Vector3 b=a+direction*length;
        g.Tube("bark",a,b,radius,radius*0.61f,depth>2?7:5);
        if(depth<=1)
        {
            float w=Rand(r,1.00f,1.81f)*(depth==1?1f:0.78f);
            g.Crown("leaf",b,V(w,Rand(r,0.72f,1.4f),w*Rand(r,0.85f,1.21f)),r.Next(),quest?6:8,quest?3:4);
        }
        if(depth==0)return;
        var side=Vector3.Cross(direction,Mathf.Abs(direction.z)<0.8f?Vector3.forward:Vector3.right).normalized;
        var around=Vector3.Cross(direction,side).normalized;
        float clock=Rand(r,-Mathf.PI,Mathf.PI);
        int forks=depth>2?3:2;
        for(int i=0;i<forks;i++)
        {
            float az=clock+i*(2*Mathf.PI/forks)+Rand(r,-0.29f,0.29f);
            Vector3 outward=side*Mathf.Cos(az)+around*Mathf.Sin(az);
            Vector3 newDir=(direction*Rand(r,0.64f,0.93f)+outward*Rand(r,0.63f,1.24f)+Vector3.up*0.20f).normalized;
            Branch(g,r,b-direction*length*Rand(r,0,0.14f),newDir,length*Rand(r,0.67f,0.80f),radius*Rand(r,0.48f,0.67f),depth-1,quest);
        }
    }

    static void Torii(LandmarkGeometry g,float scale,bool quest)
    {
        Matrix4x4 old=g.frame;
        g.frame*=Matrix4x4.Scale(Vector3.one*scale);
        foreach(int sign in new[]{-1,1})
        {
            Vector3 foot=V(3.50f*sign,0,0),shoulder=V(3.13f*sign,6.35f,0);
            g.Tube("vermilion",foot,shoulder,0.34f,0.26f,quest?8:12);
            g.Tube("black",foot,Vector3.Lerp(foot,shoulder,0.13f),0.39f,0.34f,quest?8:12);
            g.Tube("brass",shoulder-Vector3.up*0.16f,shoulder,0.35f,0.35f,8);
        }
        g.Box("vermilion",V(0,4.96f,0),V(8.61f,0.38f,0.37f));
        g.Box("vermilion",V(0,5.56f,0),V(0.64f,0.92f,0.26f));
        g.Box("brass",V(0,5.62f,-0.17f),V(0.46f,0.71f,0.07f));
        for(int i=0;i<10;i++)
        {
            float x=-5.25f+i*1.05f, next=x+1.05f;
            float y=6.31f+0.58f*Mathf.Pow(Mathf.Abs(x)/5.25f,3);
            float yn=6.31f+0.58f*Mathf.Pow(Mathf.Abs(next)/5.25f,3);
            g.Beam("vermilion",V(x,y,0),V(next,yn,0),0.49f,0.61f);
            g.Beam("black",V(x,y+0.30f,0),V(next,yn+0.30f,0),0.19f,0.81f);
        }
        g.frame=old;
    }

    static void Shrine(LandmarkGeometry g,bool quest)
    {
        g.Box("concrete",V(0,0.31f,0),V(7.2f,0.62f,5.65f));
        g.Box("bark",V(0,0.85f,0),V(6.91f,0.51f,5.39f));
        for(int i=0;i<3;i++)
            g.Box("concrete",V(0,0.15f+i*0.15f,-4+i*0.43f),V(3.1f,0.30f+i*0.30f,0.48f));
        foreach(int side in new[]{-1,1})
        {
            for(int i=0;i<3;i++)
                g.Tube("vermilion",V(side*2.8f,1,-2.05f+i*1.9f),V(side*2.8f,4.6f,-2.05f+i*1.9f),0.21f,0.20f,8);
            g.Box("paper",V(side*2.72f,2.76f,0.1f),V(0.12f,2.68f,3.57f));
            for(int j=0;j<5;j++)g.Box("bark",V(side*2.83f,2.74f,-1.6f+j*0.83f),V(0.13f,2.90f,0.085f));
            g.Box("paper",V(side*1.23f,2.75f,-1.91f),V(2.3f,2.67f,0.11f));
            for(int j=0;j<4;j++)
                g.Box("bark",V(side*(0.22f+j*0.56f),2.75f,-1.99f),V(0.065f,2.67f,0.09f));
            for(int j=0;j<4;j++)
                g.Box("bark",V(side*1.26f,1.65f+j*0.68f,-2.01f),V(2.42f,0.065f,0.09f));
            g.Tube("metal",V(side*2.0f,4.63f,-2.90f),V(side*2.0f,4.04f,-2.90f),0.04f,0.04f,6);
            g.Tube("amber",V(side*2.0f,3.29f,-2.90f),V(side*2.0f,4.04f,-2.90f),0.29f,0.29f,12);
            foreach(float y in new[]{3.27f,3.43f,3.90f,4.05f})
                g.Tube("black",V(side*2.0f,y,-2.90f),V(side*2.0f,y+0.04f,-2.90f),0.31f,0.31f,10);
        }
        for(int k=0;k<2;k++)
        {
            float width=k==0?4.50f:3.36f, length=k==0?3.35f:2.65f, baseY=k==0?4.43f:5.56f;
            foreach(int side in new[]{-1,1})
            {
                for(int i=0;i<5;i++)
                {
                    float f=i/5f,h=(i+1)/5f;
                    float p=(1-Mathf.Sin(f*Mathf.PI*0.5f))*1.45f;
                    float q=(1-Mathf.Sin(h*Mathf.PI*0.5f))*1.45f;
                    g.Plate("teal",new[]{
                        V(side*width*f,baseY+p,-length*(0.80f+f*0.20f)),V(side*width*h,baseY+q,-length*(0.80f+h*0.20f)),
                        V(side*width*h,baseY+q,length*(0.80f+h*0.20f)),V(side*width*f,baseY+p,length*(0.80f+f*0.20f))},0.15f);
                    g.Beam("brass",V(side*width*f,baseY+p,-length*(0.80f+f*0.20f)),V(side*width*h,baseY+q,-length*(0.8f+h*0.20f)),0.13f,0.19f);
                }
                g.Beam("rust",V(side*width,baseY,-length),V(side*width,baseY,length),0.17f,0.29f);
            }
            g.Beam("metal",V(0,baseY+1.55f,-length*0.92f),V(0,baseY+1.55f,length*0.92f),0.20f,0.25f);
        }
        foreach(float z in new[]{-1.39f,1.39f})
        {
            g.Beam("brass",V(-0.46f,6.42f,z),V(0.58f,7.60f,z),0.115f,0.15f);
            g.Beam("brass",V(0.46f,6.42f,z),V(-0.58f,7.60f,z),0.115f,0.15f);
        }
    }

    static void MotorPool(LandmarkGeometry g,Transform room,bool quest)
    {
        Vector3[] centers={V(-11,0.05f,-9.8f),V(-16,0.05f,8.1f),V(12,0.05f,10.4f),V(13,4.3f,-11.4f),V(-19,7.0f,-12)};
        Vector3[] turns={V(0,16,0),V(0,-51,0),V(0,119,0),V(10,27,85),V(-8,-17,173)};
        string[] paint={"teal","ivory","ochre","red","plaster"};
        for(int i=0;i<(quest?4:5);i++)
        {
            g.frame=Frame(centers[i],turns[i]);
            Saloon(g,paint[i],quest);
            if(i<3)
                Obstacle(room,"Stranded saloon",centers[i]+Vector3.up*0.90f,turns[i],V(2.05f,1.55f,4.72f));
        }
        g.frame=Matrix4x4.identity;
        // An elevated dual fuel island, complete with articulated hoses.
        g.frame=Frame(V(-12,0,15.7f),V(0,11,0));
        g.Box("tile",V(0,0.18f,0),V(8.8f,0.36f,2.70f));
        foreach(int s in new[]{-1,1})
        {
            float x=s*2.87f;
            g.Box("teal",V(x,0.99f,0),V(1.12f,1.24f,0.83f));
            g.Box("ivory",V(x,1.91f,0),V(1.31f,0.89f,0.95f));
            g.Box("black",V(x,2.02f,-0.48f),V(1.02f,0.39f,0.035f));
            for(int i=0;i<3;i++)g.Box("ice",V(x-0.31f+i*0.28f,2.03f,-0.51f),V(0.15f,0.1f,0.03f));
            var h=new[]{V(x+0.58f,2.18f,0),V(x+0.80f,2,0),V(x+0.91f,0.70f,-0.07f),V(x+0.73f,0.55f,-0.20f),V(x+0.50f,1.36f,-0.30f)};
            for(int j=1;j<h.Length;j++)g.Tube("black",h[j-1],h[j],0.054f,0.054f,6);
            g.Tube("metal",V(s*3.85f,0.32f,0),V(s*3.85f,5.49f,0),0.15f,0.15f,8);
        }
        g.Box("ivory",V(0,5.54f,0),V(10.5f,0.52f,5.25f));
        g.Box("teal",V(0,5.76f,0),V(10.59f,0.23f,5.32f));
        g.Box("amber",V(0,5.40f,-2.65f),V(9.94f,0.12f,0.08f));
        g.frame=Matrix4x4.identity;
    }

    static void Saloon(LandmarkGeometry g,string paint,bool quest)
    {
        g.Box("black",V(0,0.52f,0),V(1.74f,0.36f,4.27f));
        g.Box(paint,V(0,0.86f,0),V(2.12f,0.61f,4.68f));
        g.Box(paint,V(0,1.18f,1.65f),V(2.0f,0.17f,1.17f));
        g.Box(paint,V(0,1.19f,-1.84f),V(2.0f,0.18f,0.77f));
        Vector3[] bottom={V(-0.97f,1.14f,-1.54f),V(-0.97f,1.14f,1.19f),V(0.97f,1.14f,1.19f),V(0.97f,1.14f,-1.54f)};
        Vector3[] top={V(-0.79f,1.95f,-0.94f),V(-0.79f,1.95f,0.43f),V(0.79f,1.95f,0.43f),V(0.79f,1.95f,-0.94f)};
        for(int i=0;i<4;i++)
        {
            int j=(i+1)%4;
            g.Quad("glass",bottom[j],top[j],top[i],bottom[i]);
            g.Beam(paint,bottom[i],top[i],0.10f,0.11f);
            g.Beam("metal",bottom[i],bottom[j],0.075f,0.080f);
        }
        g.Box(paint,V(0,1.976f,-0.24f),V(1.67f,0.14f,1.53f));
        foreach(int side in new[]{-1,1})
        {
            g.Beam("metal",V(side*0.98f,1.14f,-0.19f),V(side*0.81f,1.94f,-0.18f),0.07f,0.09f);
            g.Box("brass",V(side*1.07f,0.98f,0.22f),V(0.07f,0.085f,0.21f));
            g.Box("metal",V(side*1.07f,0.76f,0),V(0.08f,0.07f,4.25f));
            g.Box(paint,V(side*1.12f,1.30f,0.92f),V(0.29f,0.18f,0.21f));
            foreach(float z in new[]{-1.45f,1.44f})
            {
                g.Tube("black",V(side*0.84f,0.46f,z),V(side*1.115f,0.46f,z),0.45f,0.45f,quest?9:15);
                g.Tube("metal",V(side*1.114f,0.46f,z),V(side*1.132f,0.46f,z),0.24f,0.24f,quest?9:12);
                g.Tube("brass",V(side*1.134f,0.46f,z),V(side*1.17f,0.46f,z),0.105f,0.105f,7);
            }
            g.Box("amber",V(side*0.80f,0.97f,2.35f),V(0.36f,0.28f,0.08f));
            g.Box("red",V(side*0.81f,0.92f,-2.35f),V(0.36f,0.24f,0.08f));
        }
        foreach(float z in new[]{-2.41f,2.41f})g.Box("metal",V(0,0.68f,z),V(2.22f,0.15f,0.18f));
        g.Box("black",V(0,0.98f,2.35f),V(1.14f,0.28f,0.075f));
        for(int i=0;i<7;i++)g.Box("brass",V(-0.48f+i*0.16f,0.98f,2.40f),V(0.04f,0.21f,0.035f));
    }

    static void Obstacle(Transform parent,string name,Vector3 center,Vector3 euler,Vector3 size)
    {
        var o=new GameObject(name);
        o.transform.SetParent(parent,false);
        o.transform.localPosition=center; o.transform.localEulerAngles=euler;
        o.AddComponent<BoxCollider>().size=size;
    }

    static void Gravity(LandmarkGeometry g,bool quest)
    {
        // A reading room remains fully furnished on its ceiling. The lamps shine upward
        // into the visitors' walking space and the cabinets are readable from below.
        Matrix4x4 ceiling=Frame(V(0,20.1f,0),V(0,0,180));
        for(int row=-1;row<=1;row+=2)
        {
            for(int i=0;i<4;i++)
            {
                float x=-14+i*9.2f, z=row*9.2f;
                Matrix4x4 group=ceiling*Frame(V(x,0,z),V(0,(i%2==0?9:-13)*row,0));
                g.frame=group;
                g.Box("bark",V(0,2.13f,0),V(4.76f,0.22f,2.65f));
                foreach(float dx in new[]{-1.93f,1.93f})foreach(float dz in new[]{-1.01f,1.01f})
                    g.Tube("brass",V(dx*1.12f,0,dz*1.18f),V(dx,2.06f,dz),0.085f,0.135f,6);
                for(int chair=0;chair<4;chair++)
                {
                    float sign=chair%2==0?-1:1;
                    g.frame=group*Frame(V(chair<2?-1.16f:1.16f,0,sign*2.25f),V(0,sign>0?180:0,0));
                    Chair(g);
                }
                g.frame=group;
                g.Tube("brass",V(0,2.24f,0),V(0,2.35f,0),0.33f,0.33f,10);
                g.Tube("brass",V(0,2.32f,0),V(0,3.14f,0),0.065f,0.065f,8);
                g.Tube("amber",V(0,3.12f,0),V(0,3.69f,0),0.51f,0.22f,10);
                g.Box("paper",V(-1.12f,2.27f,0.15f),V(0.70f,0.07f,0.48f));
                g.Box("red",V(0.83f,2.31f,-0.34f),V(0.56f,0.11f,0.80f));
            }
        }
        for(int side=-1;side<=1;side+=2)
        {
            g.frame=ceiling*Frame(V(side*20,0,0),V(0,side*90,0));
            g.Box("bark",V(0,2.73f,0),V(17.55f,5.46f,0.66f));
            for(int i=0;i<=6;i++)
                g.Box("brass",V(-8.65f+i*2.88f,2.70f,-0.44f),V(0.13f,5.42f,0.81f));
            for(int row=0;row<5;row++)
            {
                float y=row*1.01f+0.17f;
                g.Box("bark",V(0,y,-0.62f),V(17.54f,0.14f,0.88f));
                int count=quest?22:38;
                for(int i=0;i<count;i++)
                {
                    int h=(i*17+row*7)%13;
                    if(h<3)continue;
                    float tall=0.41f+0.037f*h;
                    string mat=(h%3==0)?"ochre":((h%3==1)?"teal":"rust");
                    g.Box(mat,V(-8.26f+i*(16.53f/(count-1)),y+0.07f+tall*0.5f,-0.64f),V(quest?0.46f:0.27f,tall,0.44f));
                }
            }
        }
        g.frame=Matrix4x4.identity;
    }

    static void Chair(LandmarkGeometry g)
    {
        g.Box("teal",V(0,0.98f,0),V(0.95f,0.15f,0.92f));
        g.Box("teal",V(0,1.53f,-0.41f),V(0.93f,0.88f,0.14f));
        foreach(float x in new[]{-0.36f,0.36f})foreach(float z in new[]{-0.35f,0.35f})
            g.Beam("brass",V(x*1.18f,0,z*1.2f),V(x,0.93f,z),0.072f,0.078f);
    }

    static void Observatory(LandmarkGeometry g,bool quest)
    {
        // Four foundations are fixed to a listing piece of the plaza. Keeping the tower
        // off centre leaves the open court as an uninterrupted viewing point for the sky.
        g.frame=Frame(V(12.2f,4.35f,-8.4f),V(17,18,-20));
        float[] y={0,2.5f,5.9f,9.0f,11.9f,15.1f,18.3f,21.6f,24.9f};
        float[] width={5.70f,4.45f,3.03f,2.14f,1.54f,1.11f,0.81f,0.58f,0.39f};
        float[] gauge={0.88f,0.76f,0.59f,0.43f,0.32f,0.23f,0.14f,0.105f,0.075f};
        foreach(int sx in new[]{-1,1})foreach(int sz in new[]{-1,1})
        {
            g.Box("concrete",V(sx*5.7f,-0.22f,sz*5.7f),V(2.63f,0.54f,2.63f));
            for(int j=0;j<y.Length-1;j++)
            {
                Vector3 a=V(sx*width[j],y[j],sz*width[j]);
                Vector3 b=V(sx*width[j+1],y[j+1],sz*width[j+1]);
                float thickness=j<3?0.17f:(j<5?0.11f:0.079f);
                // Each of the four main legs is itself an open lattice column.
                for(int edge=0;edge<4;edge++)
                {
                    float dx=edge<2?-1:1, dz=(edge==0||edge==3)?-1:1;
                    int next=(edge+1)%4;
                    float nx=next<2?-1:1, nz=(next==0||next==3)?-1:1;
                    Vector3 off=V(dx,0,dz), nxt=V(nx,0,nz);
                    Vector3 p=a+off*gauge[j], q=b+off*gauge[j+1];
                    Vector3 p2=a+nxt*gauge[j], q2=b+nxt*gauge[j+1];
                    g.Beam("rust",p,q,thickness,thickness);
                    if(j<5)
                    {
                        g.Beam("metal",p,p2,thickness*0.71f,thickness*0.71f);
                        g.Beam("brass",p,q2,thickness*0.68f,thickness*0.68f);
                        if(!quest)g.Beam("brass",p2,q,thickness*0.64f,thickness*0.64f);
                    }
                }
            }
        }
        Matrix4x4 tower=g.frame;
        for(int face=0;face<4;face++)
        {
            g.frame=tower*Frame(Vector3.zero,V(0,face*90,0));
            // Shallow spandrel and deep arch make the silhouette readable from beneath.
            const int steps=18;
            Vector3 last=Vector3.zero;
            for(int i=0;i<=steps;i++)
            {
                float f=i/(float)steps, x=(f*2-1)*4.68f;
                float arch=1.87f+3.65f*Mathf.Sqrt(Mathf.Max(0,1-Mathf.Pow(x/4.68f,2)));
                Vector3 p=V(x,arch,Mathf.Lerp(4.8f,3.14f,(arch-1.87f)/3.65f));
                if(i>0)g.Beam("rust",last,p,0.23f,0.27f);
                if(i%2==0&&Mathf.Abs(x)<3.65f)
                    g.Beam("brass",p,V(x*0.94f,5.96f,3.10f),0.087f,0.09f);
                last=p;
            }
            for(int j=2;j<y.Length-1;j++)
            {
                Vector3 l=V(-width[j],y[j],width[j]),r=V(width[j],y[j],width[j]);
                Vector3 ln=V(-width[j+1],y[j+1],width[j+1]),rn=V(width[j+1],y[j+1],width[j+1]);
                g.Beam("rust",l,r,0.11f,0.15f);
                g.Beam("brass",l,rn,0.10f,0.105f);g.Beam("brass",r,ln,0.10f,0.105f);
            }
        }
        g.frame=tower;
        foreach(int level in new[]{2,4,8})
        {
            float w=width[level]+(level==2?0.85f:(level==4?0.59f:0.53f));
            float h=y[level];
            g.Box("rust",V(0,h,0),V(w*2,0.41f,w*2));
            g.Box("brass",V(0,h-0.23f,0),V(w*2+0.1f,0.095f,w*2+0.1f));
            foreach(int side in new[]{-1,1})
            {
                g.Beam("metal",V(-w,h+0.65f,side*w),V(w,h+0.65f,side*w),0.067f,0.067f);
                g.Beam("metal",V(side*w,h+0.65f,-w),V(side*w,h+0.65f,w),0.067f,0.067f);
                int posts=level==2?9:5;
                for(int i=0;i<posts;i++)
                {
                    float p=-w+2*w*i/(posts-1);
                    g.Beam("brass",V(p,h+0.13f,side*w),V(p,h+0.65f,side*w),0.052f,0.052f);
                    g.Beam("brass",V(side*w,h+0.13f,p),V(side*w,h+0.65f,p),0.052f,0.052f);
                }
            }
        }
        g.Tube("brass",V(0,24.9f,0),V(0,26.4f,0),0.60f,0.31f,12);
        g.Tube("metal",V(0,26.4f,0),V(0,29.8f,0),0.16f,0.034f,10);
        g.Tube("amber",V(0,29.78f,0),V(0,29.99f,0),0.095f,0.065f,8);
        g.frame=Matrix4x4.identity;
        // Graduated arcs inlaid in the plaza are deliberately confined to the floor.
        for(int r=0;r<3;r++)
        {
            float rad=7.5f+r*1.15f;
            for(int i=0;i<52;i++)
            {
                float a=i*Mathf.PI*2/52,b=(i+1)*Mathf.PI*2/52;
                if(r==1&&i%3!=0)continue;
                Vector3 p=V(rad*Mathf.Cos(a),0.032f,rad*Mathf.Sin(a));
                Vector3 q=V(rad*Mathf.Cos(b),0.032f,rad*Mathf.Sin(b));
                g.Beam(r==1?"amber":"brass",p,q,0.048f,r==1?0.09f:0.075f);
            }
        }
    }

    static void Exchange(LandmarkGeometry g,Transform room,bool quest)
    {
        // Two disconnected housing slabs turn as they climb. Deep irregular balconies
        // make real shadowed recesses against the window grid at pedestrian scale.
        for(int tower=0;tower<2;tower++)
        {
            float side=tower==0?1f:-1f;
            int floors=tower==0?12:9;
            Vector3 foot=V(side*18.6f,0,tower==0?14f:3.9f);
            float initial=tower==0?-26:23;
            for(int floor=0;floor<floors;floor++)
            {
                Vector3 shift=V(side*Mathf.Sin(floor*0.18f)*1.7f,floor*2.87f,Mathf.Sin(floor*0.21f+tower)*1.0f);
                g.frame=Frame(foot+shift,V(0,initial+side*floor*2.8f,0));
                g.Box("plaster",V(0,1.34f,0),V(10.1f,2.68f,4.78f));
                g.Box("concrete",V(0,0.075f,0),V(11.4f,0.32f,6.22f));
                foreach(int face in new[]{-1,1})
                {
                    for(int bay=0;bay<5;bay++)
                    {
                        float x=-4.08f+bay*2.04f, z=face*2.412f;
                        g.Box("metal",V(x,1.46f,z),V(1.70f,1.94f,0.11f));
                        int light=(bay*11+floor*7+tower*2)%17;
                        g.Box(light<2?"amber":(light==9?"ice":"glass"),V(x,1.49f,z+face*0.07f),V(1.51f,1.69f,0.08f));
                        g.Box("ivory",V(x-0.23f,1.48f,z+face*0.12f),V(0.07f,1.76f,0.055f));
                        if(!quest)g.Box("plaster",V(x,1.36f,z+face*0.12f),V(1.53f,0.065f,0.06f));
                    }
                    if((floor+tower)%3==1)
                    {
                        g.Box("concrete",V(0,0.54f,face*3.07f),V(10.46f,0.72f,0.17f));
                        g.Beam("brass",V(-5.11f,1.02f,face*3.09f),V(5.11f,1.02f,face*3.09f),0.075f,0.075f);
                    }
                }
                foreach(int end in new[]{-1,1})
                {
                    g.Box("teal",V(end*5.13f,1.41f,0),V(0.18f,2.56f,4.72f));
                    g.Box("brass",V(end*5.24f,1.41f,-1.82f),V(0.065f,2.48f,0.13f));
                }
            }
            Obstacle(room,"Footprint of the exchange",foot+Vector3.up*1.36f,V(0,initial,0),V(10.35f,2.72f,5.07f));
        }
        g.frame=Matrix4x4.identity;
        // A perforated skybridge catches the eye between the towers from spawn.
        Vector3 a=V(-18,14.4f,6), b=V(18,14.4f,14);
        Vector3 axis=(b-a).normalized, normal=Vector3.Cross(Vector3.up,axis);
        foreach(int s in new[]{-1,1})
        {
            Vector3 near=normal*(s*1.05f);
            g.Beam("concrete",a+near,b+near,0.42f,0.36f);
            g.Beam("concrete",a+near+Vector3.up*2.55f,b+near+Vector3.up*2.55f,0.43f,0.27f);
            for(int i=0;i<19;i++)
            {
                Vector3 p=Vector3.Lerp(a,b,i/19f)+near;
                Vector3 q=Vector3.Lerp(a,b,(i+1)/19f)+near+Vector3.up*2.55f;
                g.Beam(i%5==0?"brass":"teal",p,q,0.16f,0.19f);
            }
        }
        g.Beam("metal",a,b,1.96f,0.26f);
    }
}
