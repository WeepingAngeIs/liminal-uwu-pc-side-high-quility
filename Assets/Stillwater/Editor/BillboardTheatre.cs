using System;
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using UdonSharp;
using UdonSharpEditor;
using VRC.SDK3.Components;
using VRC.SDK3.Video.Components;
using VRC.Udon;

internal static class BillboardTheatre
{
    static readonly Color Ink = new Color(.025f,.055f,.068f, .98f);
    static readonly Color Paper = new Color(.87f,.91f,.89f,1);
    static readonly Color Pink = new Color(.95f,.26f,.63f,1);
    static readonly Color Teal = new Color(.07f,.22f,.25f,1);
    static Font font;

    internal static void PreparePrograms()
    {
        foreach (string name in new[] { "BillboardPlayer", "BillboardMenuToggle" }) {
            string path = WorldAuthor.Root + "/Runtime/" + name;
            if (AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(path + ".asset")) continue;
            var program = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
            program.sourceCsScript = AssetDatabase.LoadAssetAtPath<MonoScript>(path + ".cs");
            if (!program.sourceCsScript) throw new IOException("Missing billboard script " + name);
            AssetDatabase.CreateAsset(program, path + ".asset");
        }
        AssetDatabase.SaveAssets();
    }

    internal static BillboardPlayer Install(Transform district, string gen, bool quest, WorldAudioDirector audio = null)
    {
        PreparePrograms();
        var program = UdonSharpProgramAsset.GetProgramAssetForClass(typeof(BillboardPlayer));
        if (!program || program.CompiledVersion != UdonSharpProgramVersion.CurrentVersion)
            UdonSharp.Compiler.UdonSharpCompilerV1.CompileSync(new UdonSharp.Compiler.UdonSharpCompileOptions());
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // Road runs along Z with x=-8..8 free. Every support is on the east pavement.
        Transform frame = WorldAuthor.Child(district, "Side street billboard");
        frame.localPosition = new Vector3(23,10,8);
        frame.localRotation = Quaternion.Euler(0,90,0);
        var m = new StationMesh(frame, gen + "/Architecture");
        const float w = 18, h = 10.125f;
        m.Box("metal",0,0,.16f,w+.62f,h+.62f,.76f);
        m.Box("black",0,0,-.26f,w+.18f,h+.18f,.12f);
        foreach (int side in new[] {-1,1}) {
            m.Box("brass",0,side*(h*.5f+.19f),-.28f,w+.66f,.095f,.17f);
            m.Box("teal",side*(w*.5f+.22f),0,-.28f,.16f,h+.48f,.17f);
            m.Box("metal",side*6,-6.75f,.4f,.72f,7.3f,.72f,true);
            m.Box("concrete",side*6,-9.82f,.4f,1.7f,.36f,1.7f,true);
            for(int i=0;i<(quest?2:5);i++) {
                float y=-8.8f+i*(quest?2.5f:1.05f);
                m.Box("brass",side*6,y,.4f,.87f,.11f,.87f);
            }
        }
        m.Box("metal",0,-5.5f,1.05f,19.2f,.24f,1.55f);
        m.Rod("brass",new Vector3(-9.6f,-4.35f,1.72f),new Vector3(9.6f,-4.35f,1.72f),.055f,quest?4:6);
        for(int i=0;i<=(quest?8:16);i++) {
            float x=Mathf.Lerp(-9.6f,9.6f,i/(quest?8f:16f));
            m.Rod("metal",new Vector3(x,-5.45f,1.72f),new Vector3(x,-4.35f,1.72f),.048f,quest?4:6);
        }
        for (int i=0;i<(quest?3:6);i++) {
            float x=-8.5f+i*(quest?8.5f:3.4f);
            m.Rod("rust",new Vector3(x,-4.8f,.68f),new Vector3(Mathf.Min(x+3.4f,8.5f),4.8f,.68f),.065f,quest?4:6);
            m.Box("metal",x,5.5f,-.78f,1.05f,.27f,.38f);
            m.Box("amber",x,5.34f,-.78f,.82f,.035f,.22f);
        }
        if (!quest) for(int side=-1;side<=1;side+=2) for(int i=0;i<24;i++)
            m.Box("brass",-8.75f+i*.76f,side*5.2f,-.37f,.052f,.052f,.027f);
        var poster = AssetDatabase.LoadAssetAtPath<Material>(gen + "/Finishes/Embankment receiver.mat");
        if (!poster) throw new InvalidOperationException("Install the local film receiver before the billboard.");
        m.Print(poster,0,0,-.34f,w,h,new Rect(0,0,1,1));
        m.Finish();
        WorldText(frame,"Station name","UWU  /  NIGHT TRANSMISSIONS",new Vector3(0,5.88f,-.2f),.25f,Paper);

        string texturePath = gen + "/Billboard target.renderTexture";
        var texture = AssetDatabase.LoadAssetAtPath<RenderTexture>(texturePath);
        int width=quest?512:1024, height=quest?288:576;
        if (!texture) {
            texture=new RenderTexture(width,height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            texture.name="Billboard target"; AssetDatabase.CreateAsset(texture,texturePath);
        } else { texture.Release();texture.width=width;texture.height=height; }
        texture.useMipMap=false;texture.autoGenerateMips=false;texture.antiAliasing=1;
        texture.filterMode=FilterMode.Bilinear;EditorUtility.SetDirty(texture);
        string materialPath=gen+"/Finishes/Billboard video.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (!material) { material=new Material(Shader.Find("Liminal uwu/Billboard video"));AssetDatabase.CreateAsset(material,materialPath); }
        material.SetTexture("_MainTex",texture);material.SetFloat("_VideoActive",0);EditorUtility.SetDirty(material);
        var screen=WorldAuthor.Child(frame,"Billboard video surface");
        var panel=new StationMesh(screen,gen+"/Architecture");
        panel.Print(material,0,0,-.38f,w,h,new Rect(0,0,1,1));panel.Finish();

        // Keep this root active: room visibility should gate renderers, not playback/UI state.
        var controlRoot=WorldAuthor.Child(district,"Billboard local controls");
        var player=controlRoot.gameObject.AddUdonSharpComponent<BillboardPlayer>();
        var native=controlRoot.gameObject.AddComponent<VRCUnityVideoPlayer>();
        native.EnableAutomaticResync=false;
        var sound=WorldAuthor.Child(controlRoot,"Billboard directional speaker");
        sound.localPosition=new Vector3(18.6f,5.4f,8);
        var speaker=sound.gameObject.AddComponent<AudioSource>();
        speaker.playOnAwake=false;speaker.spatialBlend=1;speaker.volume=.1575f;
        speaker.minDistance=5;speaker.maxDistance=34;speaker.rolloffMode=AudioRolloffMode.Logarithmic;
        speaker.dopplerLevel=0;speaker.priority=150;
        // SDK video settings are private serialized fields, as in its sample prefab.
        var serializedVideo=new SerializedObject(native);
        serializedVideo.FindProperty("videoURL").FindPropertyRelative("url").stringValue="";
        serializedVideo.FindProperty("autoPlay").boolValue=false;
        serializedVideo.FindProperty("loop").boolValue=false;
        serializedVideo.FindProperty("renderMode").intValue=0; // RenderTexture
        serializedVideo.FindProperty("targetTexture").objectReferenceValue=texture;
        serializedVideo.FindProperty("aspectRatio").intValue=(int)VideoAspectRatio.FitInside;
        serializedVideo.FindProperty("maximumResolution").intValue=quest?480:1080;
        var outputs=serializedVideo.FindProperty("targetAudioSources");outputs.arraySize=1;
        outputs.GetArrayElementAtIndex(0).objectReferenceValue=speaker;
        serializedVideo.ApplyModifiedPropertiesWithoutUndo();
        player.video=native;player.screenMaterial=material;player.speaker=speaker;player.worldAudio=audio;
        PortableMenu(controlRoot,player);
        SideConsole(district,controlRoot,player,gen);
        var wrist=WorldAuthor.Child(controlRoot,"Local wrist video button");
        wrist.localPosition=new Vector3(0,-25,0);
        var wristMesh=new StationMesh(wrist,gen+"/Architecture");
        wristMesh.Box("metal",0,0,0,.15f,.075f,.018f);
        wristMesh.Box("pink",0,-.031f,-.013f,.13f,.005f,.006f);
        wristMesh.Finish();
        WorldText(wrist,"Wrist label","MENU",new Vector3(0,.003f,-.014f),.027f,Paper);
        foreach(Transform child in wrist.GetComponentsInChildren<Transform>()) GameObjectUtility.SetStaticEditorFlags(child.gameObject,0);
        Toggle(wrist,player,new Vector3(.17f,.09f,.045f),"Open local controls",1.6f);
        player.wristButton=wrist;
        // Portable controls must not belong to the district's renderer visibility group.
        Transform portableParent=district.root==district?null:district.root;
        wrist.SetParent(portableParent,true);
        player.menu.transform.SetParent(portableParent,true);
        UdonSharpEditorUtility.CopyProxyToUdon(player);
        EditorUtility.SetDirty(UdonSharpEditorUtility.GetBackingUdonBehaviour(player));
        return player;
    }

    static void SideConsole(Transform district,Transform controlRoot,BillboardPlayer player,string gen)
    {
        var stand=WorldAuthor.Child(district,"Sidewalk video controls");
        stand.localPosition=new Vector3(15,0,-4);stand.localRotation=Quaternion.Euler(0,90,0);
        var m=new StationMesh(stand,gen+"/Architecture");
        m.Box("concrete",0,.12f,0,1.65f,.24f,1.2f,true);
        m.Box("teal",0,.68f,0,.92f,1.12f,.6f,true);
        m.Box("metal",0,1.35f,-.04f,1.8f,.75f,.3f);
        m.Box("black",0,1.35f,-.21f,1.64f,.61f,.035f);
        m.Box("pink",0,1.02f,-.23f,1.61f,.035f,.025f);
        m.Finish();
        WorldText(stand,"Console lettering","OPEN CONTROLS",new Vector3(0,1.44f,-.236f),.095f,Paper);
        WorldText(stand,"Console instructions","VIDEO  /  RAIN  /  ATMOSPHERE",new Vector3(0,1.25f,-.237f),.047f,Paper);
        var button=WorldAuthor.Child(controlRoot,"Side console interact");
        button.position=stand.TransformPoint(new Vector3(0,1.35f,-.23f));button.rotation=stand.rotation;
        Toggle(button,player,new Vector3(1.8f,.75f,.12f),"Open local controls",3.0f);
    }

    static void Toggle(Transform target,BillboardPlayer player,Vector3 size,string label,float proximity)
    {
        var collider=target.gameObject.AddComponent<BoxCollider>();collider.size=size;collider.isTrigger=true;
        var toggle=target.gameObject.AddUdonSharpComponent<BillboardMenuToggle>();toggle.controls=player;
        UdonSharpEditorUtility.CopyProxyToUdon(toggle);
        var udon=UdonSharpEditorUtility.GetBackingUdonBehaviour(toggle);udon.interactText=label;udon.proximity=proximity;
        EditorUtility.SetDirty(udon);
    }

    static void WorldText(Transform parent,string name,string text,Vector3 position,float size,Color color)
    {
        var t=WorldAuthor.Child(parent,name);t.localPosition=position;
        WorldLettering.Install(t,text,size,color);
        if(name=="Wrist label")WorldLettering.Fit(t,.13f,.045f);
        else if(name=="Console lettering" || name=="Console instructions")WorldLettering.Fit(t,1.5f,.16f);
        else if(name=="Station name")WorldLettering.Fit(t,17.5f,.75f);
    }

    static RectTransform Rect(Transform parent,string name,Vector2 position,Vector2 size)
    {
        var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);
        var rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);
        rect.anchoredPosition=position;rect.sizeDelta=size;return rect;
    }
    static Image Fill(RectTransform rect,Color color)
    { var image=rect.gameObject.AddComponent<Image>();image.color=color;return image; }
    static Text Label(Transform parent,string name,string text,Vector2 position,Vector2 size,int pixels,TextAnchor anchor=TextAnchor.MiddleLeft)
    {
        var rect=Rect(parent,name,position,size);var label=rect.gameObject.AddComponent<Text>();
        label.font=font;label.fontSize=pixels;label.text=text;label.color=Paper;label.alignment=anchor;
        label.raycastTarget=false;label.horizontalOverflow=HorizontalWrapMode.Wrap;label.verticalOverflow=VerticalWrapMode.Truncate;
        return label;
    }
    static void Button(Transform parent,string label,Vector2 position,Vector2 size,UdonBehaviour vm,string method,Color color)
    {
        var rect=Rect(parent,label,position,size);var image=Fill(rect,color);
        var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;
        var colors=button.colors;colors.highlightedColor=new Color(1.15f,1.15f,1.15f,1);colors.pressedColor=new Color(.8f,.8f,.8f,1);button.colors=colors;
        button.navigation=new Navigation{mode=Navigation.Mode.None};
        Label(rect,"Label",label,Vector2.zero,size,20,TextAnchor.MiddleCenter);
        UnityEventTools.AddStringPersistentListener(button.onClick,vm.SendCustomEvent,method);
    }
    static Slider Slider(Transform parent,string label,float y,float initial,UdonBehaviour vm,string method,float width=764)
    {
        Label(parent,label+" label",label,new Vector2(0,y+27),new Vector2(width,30),21);
        var rect=Rect(parent,label+" slider",new Vector2(0,y),new Vector2(width,30));
        Fill(rect,new Color(0,0,0,0)); // Full-height hit target, including the spaces around the rail.
        var rail=Rect(rect,"Rail",Vector2.zero,new Vector2(width,8));Fill(rail,Teal).raycastTarget=false;
        var fill=Rect(rect,"Fill",Vector2.zero,new Vector2(width,8));Fill(fill,Pink).raycastTarget=false;
        fill.sizeDelta=new Vector2(0,8); // Slider sets horizontal anchors to the current fraction.
        var handle=Rect(rect,"Handle",Vector2.zero,new Vector2(24,30));var image=Fill(handle,Paper);
        var slider=rect.gameObject.AddComponent<Slider>();slider.fillRect=fill;slider.handleRect=handle;
        slider.targetGraphic=image;slider.direction=UnityEngine.UI.Slider.Direction.LeftToRight;
        slider.minValue=0;slider.maxValue=1;slider.value=initial;slider.navigation=new Navigation{mode=Navigation.Mode.None};
        UnityEventTools.AddStringPersistentListener(slider.onValueChanged,vm.SendCustomEvent,method);
        return slider;
    }
    static void PortableMenu(Transform root,BillboardPlayer player)
    {
        var rect=Rect(root,"Portable local menu",Vector2.zero,new Vector2(900,680));
        rect.localPosition=new Vector3(0,-24,0);rect.localScale=Vector3.one*.001f;
        var canvas=rect.gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.sortingOrder=10;
        rect.gameObject.AddComponent<GraphicRaycaster>();rect.gameObject.AddComponent<VRCUiShape>();
        Fill(rect,Ink);
        Fill(Rect(rect,"Top line",new Vector2(0,332),new Vector2(900,5)),Pink).raycastTarget=false;
        Label(rect,"Title","NIGHT TRANSMISSIONS",new Vector2(-25,287),new Vector2(790,48),35);
        player.ownerLabel=Label(rect,"Owner","SHARED SCREEN",new Vector2(0,245),new Vector2(790,30),18);
        var vm=UdonSharpEditorUtility.GetBackingUdonBehaviour(player);
        Button(rect,"×",new Vector2(409,290),new Vector2(52,52),vm,nameof(BillboardPlayer.CloseMenu),Teal);
        var fieldRect=Rect(rect,"Video URL",new Vector2(0,180),new Vector2(790,56));var fieldBackground=Fill(fieldRect,new Color(.10f,.16f,.18f,1));
        var field=fieldRect.gameObject.AddComponent<VRCUrlInputField>();field.targetGraphic=fieldBackground;
        field.textComponent=Label(fieldRect,"Entered URL","",Vector2.zero,new Vector2(756,52),21);
        field.placeholder=Label(fieldRect,"Placeholder","Paste an HTTPS video link…",Vector2.zero,new Vector2(756,52),21);
        field.placeholder.color=new Color(.56f,.69f,.70f,1);field.characterLimit=2048;field.AllowSendingOnEndEdit=false;
        field.navigation=new Navigation{mode=Navigation.Mode.None};player.urlInput=field;
        Button(rect,"LOAD / SHARE",new Vector2(-298,107),new Vector2(195,52),vm,nameof(BillboardPlayer.LoadForEveryone),new Color(.36f,.08f,.24f,1));
        Button(rect,"PLAY",new Vector2(-115,107),new Vector2(140,52),vm,nameof(BillboardPlayer.PlayForEveryone),Teal);
        Button(rect,"PAUSE",new Vector2(39,107),new Vector2(140,52),vm,nameof(BillboardPlayer.PauseForEveryone),Teal);
        Button(rect,"STOP",new Vector2(193,107),new Vector2(140,52),vm,nameof(BillboardPlayer.StopForEveryone),Teal);
        Button(rect,"RESYNC",new Vector2(338,107),new Vector2(125,52),vm,nameof(BillboardPlayer.RetryLocal),Teal);
        player.videoVolume=Slider(rect,"Video volume · just you",38,.45f,vm,nameof(BillboardPlayer.UpdateLocalVolume));
        player.statusLabel=Label(rect,"Status","Paste a video URL, then load it for everyone.",new Vector2(0,-15),new Vector2(790,47),18);
        Fill(Rect(rect,"Divider",new Vector2(0,-51),new Vector2(790,2)),Teal).raycastTarget=false;
        Label(rect,"World audio heading","YOUR WORLD AUDIO",new Vector2(0,-81),new Vector2(790,28),18);
        player.rainVolume=Slider(rect,"Rain",-151,.35f,vm,nameof(BillboardPlayer.RainVolumeChanged));
        player.atmosphereVolume=Slider(rect,"Atmosphere",-231,.35f,vm,nameof(BillboardPlayer.AtmosphereVolumeChanged));
        Label(rect,"Menu hints","↑ OPEN   ↓ CLOSE     ·     VR: use the left-wrist MENU button",new Vector2(0,-299),new Vector2(790,35),17,TextAnchor.MiddleCenter);
        player.menu=rect.gameObject;rect.gameObject.SetActive(false);
    }
}
