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
            Shot(c,output,"Rain Exchange",new Vector3(.7f,1.67f,-17.7f),new Vector3(.04f,.36f,1f),81);
            Shot(c,output,"Night platform",new Vector3(15.8f,1.75f,-25),new Vector3(-.32f,.12f,1),67);
            Shot(c,output,"Spiral Line",new Vector3(-12,1.75f,54),new Vector3(.24f,.49f,1),76);
            Shot(c,output,"Long-haul Annex",new Vector3(55,2.3f,-15),new Vector3(.96f,.28f,.46f),70);
            Shot(c,output,"Hanging Orchard",new Vector3(62,1.75f,58),new Vector3(.58f,.23f,1),74);
            Shot(c,output,"Event Horizon",new Vector3(-94,1.7f,56),new Vector3(.30f,.49f,1),78);
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
