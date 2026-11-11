using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Compiles the Sparking outline shaders into the "sparking-outline" bundle. Run through _Pipeline/build_sparkingoutline.ps1.
public static class SparkingOutlineBuild
{
    public static void CommandLine()
    {
        var args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "-sparkingOutlineOut");
        if (index < 0 || index + 1 >= args.Length) throw new Exception("-sparkingOutlineOut is required");
        var shaders = new[] { "Assets/SparkingOutline/SparkingOutline.shader", "Assets/SparkingOutline/SparkingOutlineBake.shader" };
        foreach (var path in shaders)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (!shader) throw new Exception("Missing shader " + path);
            if (ShaderUtil.ShaderHasError(shader))
            {
                foreach (var message in ShaderUtil.GetShaderMessages(shader)) Debug.LogError(path + " line " + message.line + ": " + message.message);
                throw new Exception("Shader failed to compile: " + path);
            }
        }
        string temp = Path.GetFullPath("Temp/SparkingOutlineBundle");
        Directory.CreateDirectory(temp);
        var manifest = BuildPipeline.BuildAssetBundles(temp, new[] { new AssetBundleBuild { assetBundleName = "sparking-outline", assetNames = shaders } }, BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.ForceRebuildAssetBundle, BuildTarget.StandaloneWindows64);
        if (!manifest) throw new Exception("Bundle build failed");
        Directory.CreateDirectory(args[index + 1]);
        File.Copy(Path.Combine(temp, "sparking-outline"), Path.Combine(args[index + 1], "sparking-outline"), true);
        Debug.Log("SPARKING OUTLINE BUILD COMPLETE");
    }
}
