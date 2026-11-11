using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Compiles the beam shaders into the "beamfx" bundle. Run through _Pipeline/build_beamfx.ps1.
public static class BeamFxBuild
{
    public static void CommandLine()
    {
        var args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "-beamFxOut");
        if (index < 0 || index + 1 >= args.Length) throw new Exception("-beamFxOut is required");
        AssetDatabase.ImportAsset("Assets/BeamFx", ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
        string[] names = { "BeamRibbon", "BeamBurst", "BeamGround" };
        var materials = new string[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/BeamFx/" + names[i] + ".shader");
            if (!shader) throw new Exception("Missing shader " + names[i]);
            if (ShaderUtil.ShaderHasError(shader))
            {
                foreach (var message in ShaderUtil.GetShaderMessages(shader)) Debug.LogError(names[i] + " line " + message.line + ": " + message.message);
                throw new Exception("Shader failed to compile: " + names[i]);
            }
            materials[i] = "Assets/BeamFx/" + names[i] + ".mat";
            AssetDatabase.CreateAsset(new Material(shader) { name = names[i] }, materials[i]);
        }
        AssetDatabase.SaveAssets();
        string temp = Path.GetFullPath("Temp/BeamFxBundle");
        Directory.CreateDirectory(temp);
        var manifest = BuildPipeline.BuildAssetBundles(temp, new[] { new AssetBundleBuild { assetBundleName = "beamfx", assetNames = materials } }, BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.ForceRebuildAssetBundle, BuildTarget.StandaloneWindows64);
        if (!manifest) throw new Exception("Bundle build failed");
        Directory.CreateDirectory(args[index + 1]);
        File.Copy(Path.Combine(temp, "beamfx"), Path.Combine(args[index + 1], "beamfx"), true);
        Debug.Log("BEAM FX BUILD COMPLETE");
    }
}
