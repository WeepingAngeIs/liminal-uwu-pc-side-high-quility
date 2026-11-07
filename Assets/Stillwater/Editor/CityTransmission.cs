using System.IO;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

public static class CityTransmission
{
    public static void Install(Transform root,bool quest)
    {
        string folder=WorldAuthor.Root+"/Generated/"+(quest?"Quest":"PC");
        string path=folder+"/Finishes/Embankment receiver.mat";
        var receive=AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!receive)
        {
            receive=new Material(Shader.Find("Liminal uwu/Embankment transmission"));
            AssetDatabase.CreateAsset(receive,path);
        }
        receive.shader=Shader.Find("Liminal uwu/Embankment transmission");
        var plasma=AssetDatabase.LoadAssetAtPath<Texture>(Surface.Media+"/Optics/Orbital streams.png");
        var stars=AssetDatabase.LoadAssetAtPath<Texture>(Surface.Media+"/Optics/Deep field.jpg");
        var transfer=AssetDatabase.LoadAssetAtPath<Texture>(Surface.Media+"/Optics/Geodesic transfer.asset");
        if(!plasma || !stars || (!quest && !transfer))throw new IOException("Missing the observatory transmission media.");
        RemoveOldFilmReference(receive);
        receive.SetTexture("_Plasma",plasma);
        receive.SetTexture("_Stars",stars);
        var nebula=AssetDatabase.LoadAssetAtPath<Texture>(Surface.Media+"/Sky/Nebula.png");
        if(nebula)receive.SetTexture("_Nebula",nebula);
        receive.SetTexture("_GeodesicTransfer",quest?null:transfer);
        receive.SetFloat("_DiskExposure",1.25f);
        receive.SetFloat("_BroadcastSecond",2f);
        if(quest)receive.EnableKeyword("SW_MOBILE");else receive.DisableKeyword("SW_MOBILE");
        EditorUtility.SetDirty(receive);
        // The central promenade and its straight-through tunnel sightlines stay clear.
        Terminal(root.Find("hub"),new Vector3(24.0f,0,-20.0f),105f,receive,folder);
        Projection(root.Find("gallery"),receive,folder);
        // The archive's exterior surveillance windows keep their orientation when
        // the traveller enters a rotated copy of the room.
        foreach(var view in root.GetComponentsInChildren<MeshRenderer>())
        {
            var slots=view.sharedMaterials;bool changed=false;
            for(int i=0;i<slots.Length;i++)
                if(slots[i] && slots[i].name=="Estuary") { slots[i]=receive; changed=true; }
            if(changed)view.sharedMaterials=slots;
        }
        var control=WorldAuthor.Child(root,"Embankment broadcast clock").gameObject;
        var script=control.AddUdonSharpComponent<CitySignalClock>();
        script.receiver=receive;
        UdonSharpEditorUtility.CopyProxyToUdon(script);
        EditorUtility.SetDirty(UdonSharpEditorUtility.GetBackingUdonBehaviour(script));
    }
    static void RemoveOldFilmReference(Material receiver)
    {
        // Drop the unused atlas dependency so the collapsing-city footage is not
        // packed into either edition merely because this material once used it.
        var material=new SerializedObject(receiver);
        var textures=material.FindProperty("m_SavedProperties.m_TexEnvs");
        if(textures==null)return;
        for(int index=textures.arraySize-1;index>=0;index--)
            if(textures.GetArrayElementAtIndex(index).FindPropertyRelative("first").stringValue=="_Film")
                textures.DeleteArrayElementAtIndex(index);
        material.ApplyModifiedPropertiesWithoutUndo();
    }
    static void Terminal(Transform parent,Vector3 position,float angle,Material receiver,string path)
    {
        Transform body=WorldAuthor.Child(parent,"Embankment signal kiosk");
        body.localPosition=position;body.localRotation=Quaternion.Euler(0,angle,0);
        var m=new StationMesh(body,path+"/Architecture");
        m.Box("metal",0,.07f,0,3.65f,.14f,1.02f,true);
        m.Box("teal",0,.59f,.07f,2.97f,1.08f,.60f,true);
        m.Box("metal",0,2.02f,-.03f,3.66f,2.16f,.69f,true);
        m.Box("brass",0,3.15f,-.01f,3.81f,.08f,.80f);
        m.Box("black",0,2.09f,-.38f,3.44f,1.97f,.09f);
        m.Print(receiver,0,2.09f,-.434f,3.30f,1.85625f,new Rect(0,0,1,1));
        for(int i=0;i<13;i++)m.Box("black",-1.19f+i*.20f,.74f,-.243f,.111f,.063f,.021f);
        m.Box("ice",-1.52f,1.05f,-.40f,.18f,.035f,.022f);
        m.Box("amber",-1.29f,1.05f,-.40f,.12f,.035f,.022f);
        m.Finish();
    }
    static void Projection(Transform parent,Material receiver,string path)
    {
        Transform body=WorldAuthor.Child(parent,"Embankment projection");
        body.localPosition=new Vector3(-19.8f,0,-25.8f);
        body.localRotation=Quaternion.Euler(0,180,0);
        var m=new StationMesh(body,path+"/Architecture");
        m.Box("metal",0,5.5f,.02f,10.62f,6.24f,.42f);
        m.Box("black",0,5.51f,-.23f,10.48f,6.08f,.09f);
        m.Print(receiver,0,5.51f,-.284f,10.30f,5.79375f,new Rect(0,0,1,1));
        m.Box("brass",0,2.31f,0,10.79f,.18f,.54f);
        m.Box("black",0,1.41f,.14f,1.6f,.41f,.15f);
        m.Box("amber",-.56f,1.42f,.047f,.22f,.075f,.035f);
        m.Finish();
    }
}
