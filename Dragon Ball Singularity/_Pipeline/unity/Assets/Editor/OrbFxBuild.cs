using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Compiles the sphere shaders and the explosion flipbook baked in Blender into the "orbfx" bundle.
// Run through _Pipeline/build_orbfx.ps1.
public static class OrbFxBuild
{
    static Texture2D Atlas(string file)
    {
        string path = "Assets/OrbFx/" + file;
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (!importer) throw new Exception("Missing texture " + path);
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = false;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = false;
        importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.maxTextureSize = 2048;
        importer.npotScale = TextureImporterNPOTScale.None;
        var settings = importer.GetPlatformTextureSettings("Standalone");
        settings.overridden = true;
        settings.maxTextureSize = 2048;
        settings.format = TextureImporterFormat.DXT5;
        settings.textureCompression = TextureImporterCompression.CompressedHQ;
        settings.compressionQuality = 100;
        importer.SetPlatformTextureSettings(settings);
        importer.SaveAndReimport();
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (!texture) throw new Exception("Texture failed to import: " + path);
        return texture;
    }

    public static void CommandLine()
    {
        var args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "-orbFxOut");
        if (index < 0 || index + 1 >= args.Length) throw new Exception("-orbFxOut is required");
        AssetDatabase.ImportAsset("Assets/OrbFx", ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
        var atlas = Atlas("burst.png");
        string[] names = { "KiOrb", "KiBurst", "KiShot", "KiRing" };
        var assets = new string[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/OrbFx/" + names[i] + ".shader");
            if (!shader) throw new Exception("Missing shader " + names[i]);
            if (ShaderUtil.ShaderHasError(shader))
            {
                foreach (var message in ShaderUtil.GetShaderMessages(shader)) Debug.LogError(names[i] + " line " + message.line + ": " + message.message);
                throw new Exception("Shader failed to compile: " + names[i]);
            }
            var material = new Material(shader) { name = names[i] };
            if (material.HasProperty("_Atlas")) material.SetTexture("_Atlas", atlas);
            assets[i] = "Assets/OrbFx/" + names[i] + ".mat";
            AssetDatabase.CreateAsset(material, assets[i]);
        }
        AssetDatabase.SaveAssets();
        string temp = Path.GetFullPath("Temp/OrbFxBundle");
        Directory.CreateDirectory(temp);
        var manifest = BuildPipeline.BuildAssetBundles(temp, new[] { new AssetBundleBuild { assetBundleName = "orbfx", assetNames = assets } }, BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.ForceRebuildAssetBundle, BuildTarget.StandaloneWindows64);
        if (!manifest) throw new Exception("Bundle build failed");
        Directory.CreateDirectory(args[index + 1]);
        File.Copy(Path.Combine(temp, "orbfx"), Path.Combine(args[index + 1], "orbfx"), true);
        Debug.Log("ORB FX BUILD COMPLETE");
    }
}
