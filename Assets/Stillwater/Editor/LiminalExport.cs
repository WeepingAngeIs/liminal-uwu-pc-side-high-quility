using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRC.SDKBase.Editor;
using VRC.SDKBase.Editor.BuildPipeline;

// CI exports use the same installed VRChat SDK exporter as Build & Test.
// This method only creates a local bundle on the runner. It never logs in or uploads.
public static class LiminalExport
{
    [Serializable]
    private class SizeReport
    {
        public string platform;
        public long downloadBytes;
        public long unpackedAssetBytes;
        public int downloadLimitBytes;
        public int unpackedLimitBytes;
    }

    private static long ReadPackedAssetSize(BuildTarget platform)
    {
        // This is the same native report and packed-size accounting used by the SDK's
        // ValidationEditorHelpers. The SDK skips reading it when a platform has no limit.
        if (!File.Exists("Library/LastBuild.buildreport")) return -1;
        string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Liminal-CI.buildreport");
        try
        {
            File.Copy("Library/LastBuild.buildreport", path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var report = AssetDatabase.LoadAssetAtPath<BuildReport>(path);
            if (!report || report.summary.platform != platform) return -1;
            var bytes = report.packedAssets.Sum(group =>
                (decimal)group.overhead + group.contents.Sum(asset => (decimal)asset.packedSize));
            return bytes > 0 ? (long)bytes : -1;
        }
        catch (Exception e)
        {
            Debug.LogWarning("Could not read unpacked asset statistics: " + e.Message);
            return -1;
        }
        finally
        {
            if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
        }
    }

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
        // Bake visibility offline on the runner so rooms behind solid walls can be skipped.
        StaticOcclusionCulling.Compute();
        EditorSceneManager.SaveOpenScenes();
        string target=Path.GetFullPath("Builds/"+(quest?"Quest":"PC")+"/liminal-uwu.vrcw");
        Directory.CreateDirectory(Path.GetDirectoryName(target));
        if(!VRCBuildPipelineCallbacks.OnVRCSDKBuildRequested(VRCSDKRequestedBuildType.Scene))
            throw new InvalidOperationException("SDK build callback declined the scene.");
        VRC.Editor.EnvConfig.ConfigurePlayerSettings();
        EditorPrefs.SetBool("VRC.SDKBase_StripAllShaders", false);
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
        var size = new SizeReport
        {
            platform = platform.ToString(),
            downloadBytes = new FileInfo(target).Length,
            unpackedAssetBytes = ReadPackedAssetSize(platform),
            downloadLimitBytes = VRC.ValidationHelpers.GetAssetBundleSizeLimit(VRC.ContentType.World, quest, true),
            unpackedLimitBytes = VRC.ValidationHelpers.GetAssetBundleSizeLimit(VRC.ContentType.World, quest, false),
        };
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(target), "size-report.json"),
            JsonUtility.ToJson(size, true));
        Debug.Log("Exported " + target + " (" + size.downloadBytes + " bytes)");
    }
}
