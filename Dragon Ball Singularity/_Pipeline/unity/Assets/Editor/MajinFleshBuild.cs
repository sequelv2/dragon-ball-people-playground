using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Unity 2020.3.1f1: compile the live flesh material with Blender albedo/normal flipbooks.
public static class MajinFleshBuild
{
    static Texture2D Atlas(string name)
    {
        string path = "Assets/MajinFlesh/" + name;
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (!importer) throw new IOException("Missing Blender atlas: " + path);
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = false;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = false;
        importer.mipmapEnabled = false;
        importer.isReadable = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.maxTextureSize = 4096;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        var standalone = importer.GetPlatformTextureSettings("Standalone");
        standalone.overridden = true;
        standalone.maxTextureSize = 4096;
        standalone.format = TextureImporterFormat.RGBA32;
        standalone.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SetPlatformTextureSettings(standalone);
        importer.SaveAndReimport();
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (!texture || texture.width != 3072 || texture.height != 1024)
            throw new IOException("Expected 24 x 8 Blender frames of 128 pixels: " + path);
        return texture;
    }

    public static void CommandLine()
    {
        string[] args = Environment.GetCommandLineArgs();
        int flag = Array.IndexOf(args, "-majinFleshOut");
        if (flag < 0 || flag + 1 >= args.Length) throw new ArgumentException("-majinFleshOut requires an output folder");
        AssetDatabase.ImportAsset("Assets/MajinFlesh", ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
        Texture2D albedo = Atlas("flesh-albedo.png"), normal = Atlas("flesh-normal.png");
        var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/MajinFlesh/MajinFlesh.shader");
        if (!shader) throw new IOException("Majin flesh shader was not imported");
        if (ShaderUtil.ShaderHasError(shader))
        {
            foreach (var message in ShaderUtil.GetShaderMessages(shader))
                Debug.LogError("Majin flesh shader line " + message.line + ": " + message.message);
            throw new InvalidOperationException("Majin flesh shader compilation failed");
        }
        const string materialPath = "Assets/MajinFlesh/MajinFlesh.mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(materialPath)) AssetDatabase.DeleteAsset(materialPath);
        var material = new Material(shader) { name = "MajinFlesh" };
        material.SetTexture("_FleshAtlas", albedo);
        material.SetTexture("_NormalAtlas", normal);
        material.SetColor("_Tint", new Color(.93f, .48f, .68f));
        material.SetFloat("_Wetness", .68f);
        material.SetVector("_LightDir", new Vector4(-.45f, .65f, .62f, 0));
        AssetDatabase.CreateAsset(material, materialPath);
        AssetDatabase.SaveAssets();
        string temporary = Path.GetFullPath("Temp/MajinFleshBundle");
        Directory.CreateDirectory(temporary);
        var build = new AssetBundleBuild { assetBundleName = "majinflesh", assetNames = new[] { materialPath } };
        var manifest = BuildPipeline.BuildAssetBundles(temporary, new[] { build },
            BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.ForceRebuildAssetBundle, BuildTarget.StandaloneWindows64);
        if (!manifest) throw new InvalidOperationException("Majin flesh bundle build failed");
        string destination = Path.GetFullPath(args[flag + 1]);
        Directory.CreateDirectory(destination);
        File.Copy(Path.Combine(temporary, "majinflesh"), Path.Combine(destination, "majinflesh"), true);
        File.WriteAllText(Path.Combine(destination, "build-info.txt"),
            "Unity 2020.3.1f1 / StandaloneWindows64 / D3D11\nBlender authored 24 frames x 8 pieces / 128 pixel tiles\nLinear raw RGBA albedo + camera normals / frame interpolation\n");
        Debug.Log("MAJIN FLESH BUILD COMPLETE: " + Path.Combine(destination, "majinflesh"));
    }
}
