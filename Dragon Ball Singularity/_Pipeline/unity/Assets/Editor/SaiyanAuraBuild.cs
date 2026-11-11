using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Packs the Super Saiyan aura into the "saiyan_aura" bundle: the frame-by-frame shell (the atlases from
// _Pipeline/saiyan_flipbook.py and its shader) and the energy inside it, a prefab of particle systems.
// Run through _Pipeline/build_saiyanaura.ps1. SaiyanAuraRig sets every other value through property blocks.
public static class SaiyanAuraBuild
{
    public const string Root = "Assets/SaiyanAura";

    // The atlases hold data (distances, and colours already in screen space), so they load linear and without mips.
    static Texture2D Atlas(string file, TextureImporterFormat format)
    {
        string path = Root + "/" + file;
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (!importer) throw new Exception("Missing texture " + path);
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = false;
        importer.alphaSource = format == TextureImporterFormat.BC7 ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
        importer.alphaIsTransparency = false;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = 4096;
        var settings = importer.GetPlatformTextureSettings("Standalone");
        settings.overridden = true;
        settings.maxTextureSize = 4096;
        settings.format = format;
        settings.textureCompression = TextureImporterCompression.CompressedHQ;
        settings.compressionQuality = 100;
        importer.SetPlatformTextureSettings(settings);
        importer.SaveAndReimport();
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (!texture) throw new Exception("Texture failed to import: " + path);
        return texture;
    }

    static Shader CheckedShader(string name)
    {
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(Root + "/" + name + ".shader");
        if (!shader) throw new Exception("Missing shader " + name);
        if (ShaderUtil.ShaderHasError(shader))
        {
            foreach (var message in ShaderUtil.GetShaderMessages(shader)) Debug.LogError(name + " line " + message.line + ": " + message.message);
            throw new Exception("Shader failed to compile: " + name);
        }
        return shader;
    }

    static Material SaveMaterial(Material material)
    {
        string path = Root + "/" + material.name + ".mat";
        AssetDatabase.DeleteAsset(path);
        material.renderQueue = 3000;
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    // Returns the asset paths that go into the bundle: the flipbook material, the core prefab and the outlines.
    public static string[] Generate()
    {
        AssetDatabase.ImportAsset(Root, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
        var flipbook = new Material(CheckedShader("SaiyanAura")) { name = "saiyanaura_flipbook" };
        flipbook.SetTexture("_Bolts", Atlas("bolt_atlas.png", TextureImporterFormat.BC5));
        SaveMaterial(flipbook);
        var particle = CheckedShader("SaiyanParticle");
        var streak = new Material(particle) { name = "saiyanaura_streak" }; streak.SetFloat("_Kind", 0); SaveMaterial(streak);
        var spark = new Material(particle) { name = "saiyanaura_spark" }; spark.SetFloat("_Kind", 1); SaveMaterial(spark);
        string prefab = Core(streak, spark);
        string bolts = Root + "/saiyanaura_bolts.bytes";
        if (!AssetDatabase.LoadAssetAtPath<TextAsset>(bolts)) throw new Exception("Missing " + bolts);
        // The user's own SSJ1 aura (SSJ1Aura.shader, driven by SSJ1AuraController in the game).
        SaveMaterial(new Material(CheckedShader("SSJ1Aura")) { name = "ssj1aura" });
        // The SSJ2/3 lightning after the user's first reference GIF (SaiyanLightning.shader). The user's own
        // SSJLightningVFX.shader stays in the folder but is no longer packed.
        SaveMaterial(new Material(CheckedShader("SaiyanLightning")) { name = "saiyanlightning" });
        AssetDatabase.SaveAssets();
        return new[] { Root + "/saiyanaura_flipbook.mat", prefab, bolts, Root + "/ssj1aura.mat", Root + "/saiyanlightning.mat" };
    }

    // ---- The energy inside the oval, in rig units for Super Saiyan 1: the oval spans x +-.68 and y -1.46..0.84.
    // SaiyanAuraRig scales it per form and with the charge, rooted at the feet, and sorts the renderers.

    static string Core(Material streak, Material spark)
    {
        var root = new GameObject("saiyanaura_core");
        var streaks = Emitter(root, "Ki streaks", streak, 26, 80);
        {
            var main = streaks.main;
            // Short lives, so every streak fades out inside the oval instead of drifting above the aura.
            main.startLifetime = new ParticleSystem.MinMaxCurve(.35f, .6f);
            main.startSize = new ParticleSystem.MinMaxCurve(.05f, .09f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, .93f, .62f, .5f), new Color(1f, .98f, .82f, .42f));
            Rise(streaks, 1.1f, 1.9f, .1f);
            var size = streaks.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, .6f), new Keyframe(.4f, 1f), new Keyframe(1, .7f)));
            var renderer = streaks.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 3.5f; renderer.velocityScale = .12f; renderer.cameraVelocityScale = 0;
        }
        var sparks = Emitter(root, "Sparks", spark, 16, 50);
        Sparkle(sparks, .018f, .035f);
        var front = Emitter(root, "Sparks front", spark, 5, 16);
        Sparkle(front, .014f, .026f);
        string path = Root + "/saiyanaura_core.prefab";
        AssetDatabase.DeleteAsset(path);
        PrefabUtility.SaveAsPrefabAsset(root, path);
        UnityEngine.Object.DestroyImmediate(root);
        return path;
    }

    static void Sparkle(ParticleSystem system, float sizeMin, float sizeMax)
    {
        var main = system.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(.45f, .85f);
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, .98f, .85f, 1f), new Color(1f, .93f, .65f, .9f));
        Rise(system, 1f, 1.9f, .25f);
        var noise = system.noise;
        noise.enabled = true; noise.strength = .3f; noise.frequency = 1.5f; noise.scrollSpeed = .5f; noise.quality = ParticleSystemNoiseQuality.Low;
        system.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.Billboard;
    }

    static void Rise(ParticleSystem system, float min, float max, float sway)
    {
        var velocity = system.velocityOverLifetime;
        velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(-sway, sway);
        velocity.y = new ParticleSystem.MinMaxCurve(min, max);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
    }

    static ParticleSystem Emitter(GameObject root, string name, Material material, float rate, int max)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root.transform, false);
        var system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = system.main;
        main.duration = 2f; main.loop = true; main.playOnAwake = true; main.startSpeed = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World; main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = max;
        var emission = system.emission;
        emission.rateOverTime = rate;
        var shape = system.shape;
        shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Box;
        shape.position = new Vector3(0, -.75f, 0); shape.scale = new Vector3(1f, 1.2f, 0);
        var color = system.colorOverLifetime;
        color.enabled = true;
        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .15f), new GradientAlphaKey(1, .6f), new GradientAlphaKey(0, 1) });
        color.color = new ParticleSystem.MinMaxGradient(fade);
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.sortMode = ParticleSystemSortMode.OldestInFront; renderer.minParticleSize = 0f; renderer.maxParticleSize = 4f;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
        return system;
    }

    public static void CommandLine()
    {
        var args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "-saiyanAuraOut");
        if (index < 0 || index + 1 >= args.Length) throw new Exception("-saiyanAuraOut is required");
        var assets = Generate();
        string temp = Path.GetFullPath("Temp/SaiyanAuraBundle");
        Directory.CreateDirectory(temp);
        var manifest = BuildPipeline.BuildAssetBundles(temp, new[] { new AssetBundleBuild { assetBundleName = "saiyan_aura", assetNames = assets } }, BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.ForceRebuildAssetBundle, BuildTarget.StandaloneWindows64);
        if (!manifest) throw new Exception("Bundle build failed");
        Directory.CreateDirectory(args[index + 1]);
        File.Copy(Path.Combine(temp, "saiyan_aura"), Path.Combine(args[index + 1], "saiyan_aura"), true);
        Debug.Log("SAIYAN AURA BUILD COMPLETE");
    }
}
