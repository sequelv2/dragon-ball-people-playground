using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Compiles the hit lens shader into the "hitlens" bundle. Run through _Pipeline/build_hitlens.ps1.
public static class HitLensBuild
{
    public static void CommandLine()
    {
        var args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "-hitLensOut");
        if (index < 0 || index + 1 >= args.Length) throw new Exception("-hitLensOut is required");
        const string shaderPath = "Assets/HitLens/HitLens.shader", materialPath = "Assets/HitLens/hitlens.mat";
        AssetDatabase.ImportAsset(shaderPath, ImportAssetOptions.ForceUpdate);
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
        if (!shader) throw new Exception("Missing shader");
        if (ShaderUtil.ShaderHasError(shader))
        {
            foreach (var message in ShaderUtil.GetShaderMessages(shader)) Debug.LogError("line " + message.line + ": " + message.message);
            throw new Exception("Shader failed to compile");
        }
        AssetDatabase.CreateAsset(new Material(shader) { name = "hitlens" }, materialPath);
        AssetDatabase.SaveAssets();
        string temp = Path.GetFullPath("Temp/HitLensBundle");
        Directory.CreateDirectory(temp);
        var manifest = BuildPipeline.BuildAssetBundles(temp, new[] { new AssetBundleBuild { assetBundleName = "hitlens", assetNames = new[] { materialPath } } }, BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.ForceRebuildAssetBundle, BuildTarget.StandaloneWindows64);
        if (!manifest) throw new Exception("Bundle build failed");
        Directory.CreateDirectory(args[index + 1]);
        File.Copy(Path.Combine(temp, "hitlens"), Path.Combine(args[index + 1], "hitlens"), true);
        Debug.Log("HIT LENS BUILD COMPLETE");
    }
}
