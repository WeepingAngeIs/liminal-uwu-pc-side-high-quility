using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class WorldPhotography
{
    // Scene photographs are regular Unity renders of the authored assets.
    [MenuItem("Liminal uwu/Photograph PC scene")]
    public static void Capture()
    {
        if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        EditorSceneManager.OpenScene(WorldAuthor.PCScene);
        string output=Environment.GetEnvironmentVariable("LIMINAL_PHOTOS");
        if(string.IsNullOrEmpty(output))output="Builds/Photos";
        Directory.CreateDirectory(output);
        Camera c=new GameObject("Photographer").AddComponent<Camera>();
        c.enabled=false;c.allowHDR=false;c.allowMSAA=true;c.nearClipPlane=.1f;c.farClipPlane=260;
        try{
            foreach(var rain in UnityEngine.Object.FindObjectsOfType<ParticleSystem>()) rain.Simulate(1.1f,true,true,true);
            Shot(c,output,"Rain Exchange",new Vector3(-1.2f,1.63f,-19.2f),new Vector3(.052f,.46f,1f),84);
            Shot(c,output,"Night platform",new Vector3(15.8f,1.75f,-25),new Vector3(-.32f,.12f,1),67);
            Shot(c,output,"The aperture",new Vector3(-84.5f,1.8f,66),new Vector3(0,.90f,1),77);
            Shot(c,output,"Photon ring",new Vector3(-83f,1.74f,71),new Vector3(0,1.11f,1),37);
            Shot(c,output,"Embankment broadcast",new Vector3(-17.90f,2.15f,-95.6f),new Vector3(-1.9f,3.08f,-10.05f),54);
            Shot(c,output,"Spiral Line",new Vector3(12,1.73f,52),new Vector3(-.29f,.49f,1),74);
            Shot(c,output,"Long-haul Annex",new Vector3(106,4.1f,-18),new Vector3(-27,8.8f,18),75);
            Shot(c,output,"Hanging Orchard",new Vector3(73,1.68f,55),new Vector3(17,16,38),69);
            Shot(c,output,"Pagoda court",new Vector3(64.5f,1.67f,69),new Vector3(27.5f,16,25),68);
            Shot(c,output,"Platform boarding",new Vector3(7.2f,1.7f,48.9f),new Vector3(.53f,.20f,1),66);
            Shot(c,output,"Aperture Express interior",new Vector3(15.99f,1.95f,68.4f),new Vector3(.40f,-.20f,-1),72);
            Shot(c,output,"Diminishing Baths",new Vector3(.35f,1.66f,-109),new Vector3(-.01f,.08f,-1),71);
            Shot(c,output,"Night Reception",new Vector3(-105.2f,1.63f,-79.65f),new Vector3(-1,-.01f,-.025f),68);
            Shot(c,output,"Summit",new Vector3(17,84,107),new Vector3(0,1.11f,1),65);
            Shot(c,output,"Event Horizon",new Vector3(-96.4f,1.73f,59),new Vector3(28,.53f*28,16),85);
            Shot(c,output,"Ceiling Ward",new Vector3(-72,1.7f,-21),new Vector3(-.22f,.57f,1),78);
            Shot(c,output,"Wall walk",new Vector3(-79,2.25f,-635),new Vector3(-.025f,.50f,-1),81);
            Shot(c,output,"Motor Baths",new Vector3(61,1.65f,-102),new Vector3(.48f,.12f,1),70);
            Shot(c,output,"Print Arcade",new Vector3(-18.2f,5.95f,-68),new Vector3(1,-.08f,-.36f),74);
        }finally{UnityEngine.Object.DestroyImmediate(c.gameObject);}
    }
    static void Shot(Camera c,string output,string name,Vector3 eye,Vector3 look,float fov)
    {
        c.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(look));c.fieldOfView=fov;
        var rt=RenderTexture.GetTemporary(1440,900,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB,2);
        var original=RenderTexture.active;
        var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
        try{
            c.targetTexture=rt;c.Render();RenderTexture.active=rt;
            image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();
            File.WriteAllBytes(Path.Combine(output,name+".jpg"),image.EncodeToJPG(93));
        }finally{c.targetTexture=null;RenderTexture.active=original;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(image);}
    }
}
