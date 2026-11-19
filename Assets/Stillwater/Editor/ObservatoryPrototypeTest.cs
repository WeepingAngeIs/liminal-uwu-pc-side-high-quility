using System.IO;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Editor.Builder;

public static class ObservatoryPrototypeTest
{
    [MenuItem("Liminal uwu/Test Observatory prototype in VRChat", false, 2)]
    public static void Test()
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64) {
            EditorUtility.DisplayDialog("Observatory prototype", "Open the Windows project to test the listening post.", "OK");
            return;
        }
        string bundle = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/Observatory/observatory-pc.vrcw"));
        if (!File.Exists(bundle)) {
            EditorUtility.DisplayDialog("Observatory prototype", "The prototype bundle is missing at " + bundle, "OK");
            return;
        }
        VRCWorldAssetExporter.RunScene(bundle);
    }
}
