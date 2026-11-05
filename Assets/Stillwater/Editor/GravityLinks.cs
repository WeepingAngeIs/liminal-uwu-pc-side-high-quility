using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// These single-destination versions of the station control make gravity galleries into
// optional through routes. Their landings sit on the approach side of a visible return gate.
public static class GravityLinks
{
    public static void Install(Transform root,bool quest)
    {
        string folder=WorldAuthor.Root+"/Generated/"+(quest?"Quest":"PC");
        Material signs=AssetDatabase.LoadAssetAtPath<Material>(folder+"/Station plates.mat");
        if(!signs) throw new System.InvalidOperationException("Station plate material missing.");
        Transform grove=Gate(root.Find("grove"),"Orchard field entrance",new Vector3(-7.2f,0,-20.6f),0);
        Transform soffit=Gate(root.Find("Soffit gallery"),"Soffit orchard entrance",new Vector3(-8.6f,0,17),0);
        Transform horizon=Gate(root.Find("observatory"),"Horizon field entrance",new Vector3(-8.1f,0,-19.2f),0);
        Transform facade=Gate(root.Find("Western facade gallery"),"Facade horizon entrance",new Vector3(3.5f,0,7.5f),180);

        Transform grovePad=Arrival(grove,.2f), soffitPad=Arrival(soffit,1.05f);
        Transform horizonPad=Arrival(horizon,.2f), facadePad=Arrival(facade,1.05f);
        string meshes=folder+"/Architecture";
        Fit(grove,soffitPad,12,"Ceiling walk",signs,meshes);
        Fit(soffit,grovePad,13,"Hanging Orchard",signs,meshes);
        Fit(horizon,facadePad,14,"Wall walk",signs,meshes);
        Fit(facade,horizonPad,15,"Event Horizon",signs,meshes);
    }

    static Transform Gate(Transform room,string name,Vector3 position,float yaw)
    {
        if(!room) throw new System.InvalidOperationException("Missing room for "+name);
        Transform gate=WorldAuthor.Child(room,name);
        gate.localPosition=position;
        gate.localRotation=Quaternion.Euler(0,yaw,0);
        return gate;
    }

    static Transform Arrival(Transform gate,float height)
    {
        Transform pad=WorldAuthor.Child(gate,"Arrival on approach");
        pad.localPosition=new Vector3(0,height,-2.7f);
        return pad;
    }

    static void Fit(Transform gate,Transform destination,int index,string target,Material signs,string meshes)
    {
        var m=new StationMesh(gate,meshes);
        // An open arch preserves walking clearance, and its waist-high brass reader is
        // a solid, reachable interaction from either side. Destination plates face both ways.
        foreach(int side in new[]{-1,1})
        {
            m.Box("metal",side*1.72f,1.48f,0,.22f,2.96f,.42f,true);
            m.Box("brass",side*1.72f,.13f,0,.45f,.26f,.65f,true);
            m.Box("ice",side*1.72f,1.93f,-.222f,.058f,1.23f,.032f);
            m.Box("ice",side*1.72f,1.93f,.222f,.058f,1.23f,.032f);
        }
        m.Box("metal",0,2.94f,0,3.72f,1.02f,.38f,true);
        var uv=new Rect(index%2*.5f+.001f,1-(index/2+1)*.125f+.001f,.498f,.123f);
        m.Print(signs,0,2.94f,-.20f,3.53f,.8825f,uv);
        m.Frame=Quaternion.Euler(0,180,0);
        m.Print(signs,0,2.94f,-.20f,3.53f,.8825f,uv);
        m.ResetFrame();
        m.Box("brass",1.34f,1.17f,0,.61f,.57f,.54f);
        m.Box("amber",1.34f,1.18f,-.283f,.28f,.20f,.035f);
        m.Box("amber",1.34f,1.18f,.283f,.28f,.20f,.035f);
        m.Finish();

        Transform reader=WorldAuthor.Child(gate,"Travel to "+target);
        reader.localPosition=new Vector3(1.34f,1.17f,0);
        reader.gameObject.AddComponent<BoxCollider>().size=new Vector3(.64f,.62f,.62f);
        var action=reader.gameObject.AddUdonSharpComponent<LastTrainBoard>();
        action.destinations=new[]{destination};
        UdonSharpEditorUtility.CopyProxyToUdon(action);
        var udon=UdonSharpEditorUtility.GetBackingUdonBehaviour(action);
        udon.interactText="Travel to "+target;
        udon.proximity=3f;
        EditorUtility.SetDirty(udon);
        EditorUtility.SetDirty(action);
    }
}
