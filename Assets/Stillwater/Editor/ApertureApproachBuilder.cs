using System;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Components;

public static class ApertureApproachBuilder
{
    public static void Install(Transform root,string gen,bool quest,WorldAudioDirector audio,Transform spawn)
    {
        Transform rail=root.Find("rail");
        var service=rail.GetComponentInChildren<ApertureExpress>();
        if(!service || !spawn || !audio) throw new InvalidOperationException("The aperture approach needs the train, arrival and local audio controls.");
        Transform owner=WorldAuthor.Child(rail,"Aperture approach");
        var start=WorldAuthor.Child(owner,"Aligned field origin");
        start.localPosition=ExpressLine.ApproachStart;
        start.localRotation=Quaternion.LookRotation(ExpressLine.ApertureDirection,Vector3.up);
        var sink=WorldAuthor.Child(owner,"Beyond the aperture");
        sink.localPosition=ExpressLine.FrontSummit+ExpressLine.ApertureDirection*65;
        var deck=WorldAuthor.Child(owner,"Aperture overlook");
        deck.localPosition=ExpressLine.FrontSummit+new Vector3(-5.6f,.44f,0);
        var geometry=new StationMesh(deck,gen+"/Architecture");
        geometry.Box("metal",0,-.16f,0,5,.32f,11,true);
        geometry.Box("tile",0,.008f,0,4.86f,.016f,10.86f);
        for(int side=-1;side<=1;side+=2) {
            foreach(float z in new[]{-5.4f,-1.8f,1.8f,5.4f})
                geometry.Box("brass",side*2.42f,.52f,z,.10f,1.04f,.10f,true);
            geometry.Box("brass",side*2.42f,1.06f,0,.11f,.10f,10.9f,true);
            geometry.Box("ice",side*2.38f,.08f,0,.055f,.045f,10.8f);
        }
        geometry.Box("brass",0,1.06f,-5.4f,4.9f,.10f,.10f,true);
        // The forward edge deliberately opens into the field. Inlaid bars show its boundary.
        for(int index=0;index<7;index++)
            geometry.Box("ochre",-2.1f+index*.70f,.025f,5.10f,.34f,.03f,.36f);
        geometry.Box("metal",-1.7f,.75f,-3.9f,.50f,1.50f,.45f,true);
        geometry.Box("amber",-1.7f,1.25f,-4.15f,.33f,.20f,.05f);
        geometry.Finish();
        var landing=WorldAuthor.Child(deck,"Aperture safe exit");
        landing.localPosition=new Vector3(0,.12f,-2.4f);
        var control=owner.gameObject.AddUdonSharpComponent<ApertureApproach>();
        var rumble=owner.gameObject.AddComponent<AudioSource>();
        rumble.clip=AssetDatabase.LoadAssetAtPath<AudioClip>(WorldAuthor.Root+"/Media/Audio/aperture.wav");
        if(!rumble.clip) throw new InvalidOperationException("Missing original aperture ambience.");
        rumble.loop=true; rumble.playOnAwake=false; rumble.volume=0;
        rumble.spatialBlend=0; rumble.spatialize=false; rumble.dopplerLevel=0;
        rumble.priority=160;
        var spatial=owner.gameObject.AddComponent<VRCSpatialAudioSource>();
        spatial.EnableSpatialization=false;
        control.service=service; control.audioDirector=audio; control.rumble=rumble;
        control.localSky=RenderSettings.skybox;
        if(!control.localSky || !control.localSky.HasProperty("_ApproachScale"))
            throw new InvalidOperationException("The aperture sky must expose its local approach scale.");
        control.localSky.SetFloat("_ApproachScale",1);
        control.approachStart=start; control.sink=sink; control.safeDeck=deck; control.arrival=spawn;
        UdonSharpEditorUtility.CopyProxyToUdon(control);
        EditorUtility.SetDirty(UdonSharpEditorUtility.GetBackingUdonBehaviour(control));
        foreach(var seat in rail.GetComponentsInChildren<ExpressSeat>()) {
            seat.apertureExit=landing; seat.approach=control;
            UdonSharpEditorUtility.CopyProxyToUdon(seat);
            EditorUtility.SetDirty(UdonSharpEditorUtility.GetBackingUdonBehaviour(seat));
        }
        var button=WorldAuthor.Child(deck,"Return from aperture");
        button.localPosition=new Vector3(-1.7f,1.15f,-3.9f);
        button.gameObject.AddComponent<BoxCollider>().size=new Vector3(.6f,.65f,.56f);
        var returnHome=button.gameObject.AddUdonSharpComponent<LastTrainBoard>();
        returnHome.destinations=new[]{spawn};
        UdonSharpEditorUtility.CopyProxyToUdon(returnHome);
        var udon=UdonSharpEditorUtility.GetBackingUdonBehaviour(returnHome);
        udon.interactText="Return to arrival"; udon.proximity=3;
        EditorUtility.SetDirty(udon);
    }
}
