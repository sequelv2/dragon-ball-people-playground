using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Compiles the Super Saiyan Blue aura shaders into the "saiyan_blue_aura" bundle that BlueAuraAssets loads.
// Run through _Pipeline/build_blueauras.ps1.
public static class BlueAuraBuild
{
    public const string Root = "Assets/BlueAura";
    static readonly string[] Shaders = { "BlueAuraField", "BlueAuraTongue", "BlueAuraSpark" };
    static readonly string[] Names = { "saiyanblueaura_field", "saiyanblueaura_tongue", "saiyanblueaura_spark" };

    public static Material[] Materials()
    {
        AssetDatabase.ImportAsset(Root, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
        var result = new Material[Shaders.Length];
        for (int i = 0; i < Shaders.Length; i++)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(Root + "/" + Shaders[i] + ".shader");
            if (!shader) throw new Exception("Missing shader " + Shaders[i]);
            if (ShaderUtil.ShaderHasError(shader))
            {
                foreach (var message in ShaderUtil.GetShaderMessages(shader)) Debug.LogError(Shaders[i] + " line " + message.line + ": " + message.message);
                throw new Exception("Shader failed to compile: " + Shaders[i]);
            }
            var material = new Material(shader) { name = Names[i] };
            AssetDatabase.CreateAsset(material, Root + "/" + Names[i] + ".mat");
            result[i] = material;
        }
        AssetDatabase.SaveAssets();
        return result;
    }

    public static void CommandLine()
    {
        var args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "-blueAuraOut");
        if (index < 0 || index + 1 >= args.Length) throw new Exception("-blueAuraOut is required");
        Materials();
        var assets = new string[Names.Length];
        for (int i = 0; i < Names.Length; i++) assets[i] = Root + "/" + Names[i] + ".mat";
        string temp = Path.GetFullPath("Temp/BlueAuraBundle");
        Directory.CreateDirectory(temp);
        var manifest = BuildPipeline.BuildAssetBundles(temp, new[] { new AssetBundleBuild { assetBundleName = "saiyan_blue_aura", assetNames = assets } }, BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.ForceRebuildAssetBundle, BuildTarget.StandaloneWindows64);
        if (!manifest) throw new Exception("Bundle build failed");
        Directory.CreateDirectory(args[index + 1]);
        File.Copy(Path.Combine(temp, "saiyan_blue_aura"), Path.Combine(args[index + 1], "saiyan_blue_aura"), true);
        Debug.Log("BLUE AURA BUILD COMPLETE");
    }
}
