using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRC.Core;
using VRC.SDK3.Components;
using VRC.SDKBase.Network;

public static class ObservatoryPrototype
{
    public const string Folder = WorldAuthor.Root + "/Generated/Observatory";
    public const string PCScene = Folder + "/The listening post - PC.unity";
    public const string QuestScene = Folder + "/The listening post - Quest.unity";

    [MenuItem("Liminal uwu/Observatory prototype/Make both test scenes")]
    public static void AuthorBoth()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Directory.CreateDirectory(Folder);
        AssetDatabase.Refresh();
        PreparePrograms();
        Author(false);
        Author(true);
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(PCScene);
    }

    static void PreparePrograms()
    {
        foreach (var type in new[] { typeof(ObservatoryReceiver), typeof(ObservatoryControl) }) {
            var stem = WorldAuthor.Root + "/Runtime/" + type.Name;
            if (AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(stem + ".asset")) continue;
            var program = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
            program.sourceCsScript = AssetDatabase.LoadAssetAtPath<MonoScript>(stem + ".cs");
            if (!program.sourceCsScript) throw new FileNotFoundException("Missing prototype behaviour", stem);
            AssetDatabase.CreateAsset(program, stem + ".asset");
        }
        AssetDatabase.SaveAssets();
        UdonSharp.Compiler.UdonSharpCompilerV1.CompileSync(new UdonSharp.Compiler.UdonSharpCompileOptions());
        foreach (var type in new[] { typeof(ObservatoryReceiver), typeof(ObservatoryControl) }) {
            var p = UdonSharpProgramAsset.GetProgramAssetForClass(type);
            if (!p || !p.SerializedProgramAsset || p.CompiledVersion != UdonSharpProgramVersion.CurrentVersion)
                throw new InvalidOperationException("Prototype program failed to compile: " + type.Name);
        }
    }

    static void Author(bool quest)
    {
        var source = EditorSceneManager.OpenScene(quest ? WorldAuthor.QuestScene : WorldAuthor.PCScene);
        string target = quest ? QuestScene : PCScene;
        if (!EditorSceneManager.SaveScene(source, target, true)) throw new IOException("Could not copy " + source.path);
        var scene = EditorSceneManager.OpenScene(target);
        var root = GameObject.Find("liminal uwu").transform;
        var hub = root.Find("hub");
        var gravity = root.Find("gravity");
        var descriptor = root.GetComponentInChildren<VRCSceneDescriptor>(true);
        var pipeline = descriptor.GetComponent<PipelineManager>();
        if (pipeline) pipeline.blueprintId = string.Empty;

        var parts = ObservatoryInstruments.Build(hub, gravity, quest);
        ObservatoryHousing.Place(parts.housingMount, quest);
        var receiver = WorldAuthor.Child(root, "Observatory receiver").gameObject.AddUdonSharpComponent<ObservatoryReceiver>();
        receiver.receiverDisplay = parts.readout;
        receiver.clockDisplay = parts.clockDisplay;
        receiver.penNeedle = parts.pen;
        receiver.feedWheel = parts.feedWheel;
        receiver.clockHand = parts.clockHand;
        receiver.serviceHand = parts.serviceHand;
        receiver.pendulum = parts.pendulum;
        receiver.secondSpool = parts.secondSpool;
        UdonSharpEditorUtility.CopyProxyToUdon(receiver);
        Bind(parts.spoolButton, receiver, false);
        Bind(parts.clockButton, receiver, true);

        var visibility = root.GetComponentInChildren<DistrictVisibility>(true);
        var sectors = DistrictSystems.SectorRoots(root);
        var renderers = visibility.renderers.ToList();
        var indices = visibility.rendererSector.ToList();
        foreach (var room in new[] { hub, gravity }) {
            int sector = sectors.IndexOf(room);
            if (sector < 0) throw new InvalidOperationException("Missing district: " + room.name);
            var known = new HashSet<Renderer>(renderers);
            foreach (var renderer in room.GetComponentsInChildren<Renderer>(true)) {
                if (renderer.GetComponentInParent<Canvas>() || !known.Add(renderer)) continue;
                renderers.Add(renderer);
                indices.Add(sector);
            }
        }
        visibility.renderers = renderers.ToArray();
        visibility.rendererSector = indices.ToArray();
        UdonSharpEditorUtility.CopyProxyToUdon(visibility);
        NetworkIDAssignment.ConfigureNetworkIDs(descriptor, out List<NetworkIDAssignment.SetErrorLocation> errors,
            NetworkIDAssignment.SetError.IncompatibleTypes);
        if (errors.Count > 0) throw new InvalidOperationException("Prototype has incompatible network IDs.");
        EditorUtility.SetDirty(descriptor);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save " + target);
    }

    static void Bind(GameObject button, ObservatoryReceiver receiver, bool clock)
    {
        var control = button.AddUdonSharpComponent<ObservatoryControl>();
        control.receiver = receiver;
        control.recordsClock = clock;
        UdonSharpEditorUtility.CopyProxyToUdon(control);
        var behaviour = UdonSharpEditorUtility.GetBackingUdonBehaviour(control);
        behaviour.interactText = clock ? "Record the missing beat" : "Fit a second spool";
        behaviour.proximity = 2.5f;
        EditorUtility.SetDirty(behaviour);
    }
}
