using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRC.SDKBase.Editor;
using VRC.SDKBase.Editor.BuildPipeline;

// CI exports use the same installed VRChat SDK exporter as Build & Test.
// This method only creates a local bundle on the runner. It never logs in or uploads.
public static class LiminalExport
{
    public static void Build()
    {
        var platform=EditorUserBuildSettings.activeBuildTarget;
        bool quest=platform==BuildTarget.Android;
        if(!quest && platform!=BuildTarget.StandaloneWindows64)
            throw new InvalidOperationException("Expected Android or StandaloneWindows64, got "+platform);
        EditorSceneManager.OpenScene(quest?WorldAuthor.QuestScene:WorldAuthor.PCScene);
        if(!UpdateLayers.AreLayersSetup())UpdateLayers.SetupEditorLayers();
        if(!UpdateLayers.IsCollisionLayerMatrixSetup())UpdateLayers.SetupCollisionLayerMatrix();
        GravityValidation.Validate();
        string target=Path.GetFullPath("Builds/"+(quest?"Quest":"PC")+"/liminal-uwu.vrcw");
        Directory.CreateDirectory(Path.GetDirectoryName(target));
        if(!VRCBuildPipelineCallbacks.OnVRCSDKBuildRequested(VRCSDKRequestedBuildType.Scene))
            throw new InvalidOperationException("SDK build callback declined the scene.");
        VRC_SdkBuilder.shouldBuildUnityPackage=false;
        VRC_SdkBuilder.PreBuildBehaviourPackaging();
        var builder=new VRC.SDK3.Editor.Builder.VRCWorldBuilder();
        string output=null,error=null;
        builder.OnBuildError+=(_,message)=>error=message;
        builder.OnBuildSuccess+=(_,result)=>output=result.Item1;
        bool ok=builder.ExportSceneResource();
        if(!ok || !string.IsNullOrEmpty(error))throw new InvalidOperationException("VRChat export failed: "+error);
        if(!File.Exists(output))throw new FileNotFoundException("The SDK did not produce its world bundle.",output);
        File.Copy(output,target,true);
        long bytes=new FileInfo(target).Length;
        if(bytes<1024 || (quest && bytes>100_000_000))
            throw new IOException("Unexpected world bundle length: "+bytes);
        Debug.Log("Exported "+target+" ("+bytes+" bytes)");
    }
}
