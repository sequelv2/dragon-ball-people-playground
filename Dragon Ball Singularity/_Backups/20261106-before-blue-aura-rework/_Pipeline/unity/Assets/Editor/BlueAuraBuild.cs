using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Generates the Super Saiyan Blue family of auras (noise, materials, meshes, prefabs) and packs them into
// the "blue-auras" bundle. Run through _Pipeline/build_blueauras.ps1. The prefabs hold only native
// renderers and particle systems; CinematicAuraRig drives them at runtime through property blocks.
public static class BlueAuraBuild
{
    public const string Root = "Assets/BlueAura";
    public const string Generated = Root + "/Generated";
    const int NoiseSize = 512;
    static Texture2D noise;

    public static void CommandLine()
    {
        var args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "-blueAuraOut");
        if (index < 0 || index + 1 >= args.Length) throw new Exception("-blueAuraOut is required");
        var prefabs = GenerateAll();
        string temp = Path.GetFullPath("Temp/BlueAuraBundle");
        Directory.CreateDirectory(temp);
        var manifest = BuildPipeline.BuildAssetBundles(temp, new[] { new AssetBundleBuild { assetBundleName = "blue-auras", assetNames = prefabs.ToArray() } }, BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.ForceRebuildAssetBundle, BuildTarget.StandaloneWindows64);
        if (!manifest) throw new Exception("Bundle build failed");
        Directory.CreateDirectory(args[index + 1]);
        File.Copy(Path.Combine(temp, "blue-auras"), Path.Combine(args[index + 1], "blue-auras"), true);
        Debug.Log("BLUE AURA BUILD COMPLETE");
    }

    public static List<string> GenerateAll()
    {
        AssetDatabase.ImportAsset(Root, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
        foreach (var name in new[] { "AuraFlame", "AuraFan", "AuraVeil", "AuraGlow", "AuraTongue", "AuraSpark" }) LoadShader(name);
        if (AssetDatabase.IsValidFolder(Generated)) AssetDatabase.DeleteAsset(Generated);
        AssetDatabase.CreateFolder(Root, "Generated");
        noise = MakeNoise();
        var prefabs = new List<string>();
        foreach (bool transition in new[] { false, true })
        {
            prefabs.Add(SsjBlue(transition));
            prefabs.Add(Evolution(transition));
            prefabs.Add(KaiokenBlue(transition));
        }
        AssetDatabase.SaveAssets();
        return prefabs;
    }

    // ---- The three auras. All positions are in aura units; 1.25 of world scale is applied at runtime. ----

    static string SsjBlue(bool transition)
    {
        string name = "SSJBlue" + (transition ? "Transition" : "");
        var root = new GameObject(name);
        Glow(root, "Glow Rear", name, 3000, new Vector4(0, .35f, 2.5f, 3.2f), 2.1f, .34f, .1f);
        Tongues(root, "Tongues Rear", name, 3001, null, 22f, new Vector3(.88f, 1.42f, 1), new Vector3(0, .42f, 0), .34f, .62f, .16f, .26f, 2.5f, 1.7f, 2.9f, .34f);
        Quad(root, "Flame Rear", name + "-Flame", 3002, -3.1f, 3.1f, -2.7f, 4.2f, "AuraFlame", m =>
        {
            m.SetVector("_Shape", new Vector4(0, .42f, 1.02f, 1.9f));
            m.SetFloat("_Taper", .33f); m.SetFloat("_Fan", .1f); m.SetFloat("_Speed", 1.3f); m.SetFloat("_Sway", .4f);
            m.SetFloat("_LobeScale", .3f); m.SetFloat("_LobeAmp", .3f);
            m.SetFloat("_TongueScale", .46f); m.SetFloat("_TongueAmp", .62f);
            m.SetFloat("_StreakScale", .85f); m.SetFloat("_StreakAmp", .34f); m.SetFloat("_BandNoise", .12f);
            m.SetVector("_Bands", new Vector4(.05f, .17f, .28f, .42f)); m.SetFloat("_BandSoft", .03f);
            m.SetFloat("_Mottle", .35f); m.SetFloat("_Rim", .9f); m.SetFloat("_RimWidth", .018f);
            m.SetFloat("_GlowFall", 7f); m.SetFloat("_GlowAmount", .5f); m.SetFloat("_Alpha", .96f);
        });
        Sparks(root, "Sparks Rear", name, 3003, 30f, new Vector3(1.05f, 1.7f, 1), new Vector3(0, .35f, 0), .028f);
        Veil(root, name, new Vector4(0, -.1f, .95f, 1.5f), transition ? .92f : .26f, .08f, .75f, null);
        Sparks(root, "Sparks Front", name, 3005, 12f, new Vector3(.85f, 1.45f, 1), new Vector3(0, .1f, 0), .024f);
        Ring(root, name);
        return Save(root, name);
    }

    static string Evolution(bool transition)
    {
        string name = "SSJBlueEvolution" + (transition ? "Transition" : "");
        var root = new GameObject(name);
        Glow(root, "Glow Rear", name, 3000, new Vector4(0, .35f, 2.8f, 3.5f), 2f, .4f, 0f);
        Tongues(root, "Tongues Rear", name, 3001, null, 26f, new Vector3(1.08f, 1.65f, 1), new Vector3(0, .42f, 0), .42f, .72f, .18f, .32f, 2.2f, 1.5f, 2.7f, .4f);
        Quad(root, "Flame Rear", name + "-Flame", 3002, -3.5f, 3.5f, -2.9f, 4.6f, "AuraFlame", m =>
        {
            m.SetVector("_Shape", new Vector4(0, .42f, 1.32f, 2.2f));
            m.SetFloat("_Taper", .3f); m.SetFloat("_Fan", .08f); m.SetFloat("_Speed", 1.15f); m.SetFloat("_Sway", .5f);
            m.SetFloat("_LobeScale", .25f); m.SetFloat("_LobeAmp", .46f);
            m.SetFloat("_TongueScale", .38f); m.SetFloat("_TongueAmp", .42f);
            m.SetFloat("_StreakScale", .72f); m.SetFloat("_StreakAmp", .4f); m.SetFloat("_BandNoise", .14f);
            m.SetVector("_Bands", new Vector4(.1f, .21f, .31f, .42f)); m.SetFloat("_BandSoft", .03f);
            m.SetFloat("_Mottle", .75f); m.SetFloat("_Rim", 1f); m.SetFloat("_RimWidth", .016f);
            m.SetFloat("_GlowFall", 8f); m.SetFloat("_GlowAmount", .42f); m.SetFloat("_Alpha", .97f);
        });
        Sparks(root, "Sparks Rear", name, 3003, 34f, new Vector3(1.3f, 1.95f, 1), new Vector3(0, .35f, 0), .03f);
        Veil(root, name, new Vector4(0, -.1f, 1.05f, 1.6f), transition ? .94f : .34f, .1f, .85f, null);
        Sparks(root, "Sparks Front", name, 3005, 18f, new Vector3(.9f, 1.5f, 1), new Vector3(0, .05f, 0), .026f);
        Ring(root, name);
        return Save(root, name);
    }

    static readonly Color ShellDeep = new Color32(63, 218, 255, 255), ShellMid = new Color32(177, 255, 255, 255);

    static string KaiokenBlue(bool transition)
    {
        string name = "KaiokenBlue" + (transition ? "Transition" : "");
        var root = new GameObject(name);
        Glow(root, "Glow Rear", name, 3000, new Vector4(0, .55f, 3.9f, 4.6f), 1.8f, .42f, 0f);
        Quad(root, "Fan Rear", name + "-Fan", 3001, -4.6f, 4.6f, -3.4f, 5.4f, "AuraFan", m =>
        {
            m.SetVector("_Shape", new Vector4(0, .6f, 2.15f, 3.1f));
            m.SetFloat("_Taper", .1f); m.SetFloat("_Fan", .55f); m.SetFloat("_Speed", 1.65f);
            m.SetFloat("_SpikeScale", .3f); m.SetFloat("_SpikeAmp", .85f); m.SetFloat("_LobeAmp", .14f);
            m.SetFloat("_Inner", .52f); m.SetFloat("_Bloom", .85f); m.SetFloat("_Dark", .75f);
            m.SetFloat("_GlowFall", 3.5f); m.SetFloat("_GlowAmount", .3f); m.SetFloat("_Alpha", .97f);
        });
        Tongues(root, "Blades Rear", name, 3002, null, 30f, new Vector3(1.55f, 2.1f, 1), new Vector3(0, .5f, 0), .3f, .55f, .18f, .3f, 3.2f, 2.4f, 3.8f, .62f);
        Tongues(root, "Shell tongues Rear", name, 3003, m => Fixed(m), 26f, new Vector3(.86f, 1.36f, 1), new Vector3(0, .34f, 0), .3f, .5f, .13f, .2f, 2.6f, 1.6f, 2.9f, .24f);
        Quad(root, "Shell Rear", name + "-Shell", 3004, -3f, 3f, -2.6f, 4f, "AuraFlame", m =>
        {
            Fixed(m);
            m.SetVector("_Shape", new Vector4(0, .34f, 1.04f, 1.8f));
            m.SetFloat("_Taper", .3f); m.SetFloat("_Fan", .12f); m.SetFloat("_Speed", 1.55f); m.SetFloat("_Sway", .4f);
            m.SetFloat("_LobeScale", .32f); m.SetFloat("_LobeAmp", .24f);
            m.SetFloat("_TongueScale", .5f); m.SetFloat("_TongueAmp", .78f);
            m.SetFloat("_StreakScale", .9f); m.SetFloat("_StreakAmp", .26f); m.SetFloat("_BandNoise", .1f);
            m.SetVector("_Bands", new Vector4(.02f, .07f, .13f, .2f)); m.SetFloat("_BandSoft", .025f);
            m.SetFloat("_Mottle", 0f); m.SetFloat("_Rim", .55f); m.SetFloat("_RimWidth", .02f);
            m.SetFloat("_GlowFall", 6f); m.SetFloat("_GlowAmount", .34f); m.SetFloat("_Alpha", 1f);
        });
        Sparks(root, "Sparks Rear", name, 3005, 44f, new Vector3(1.9f, 2.5f, 1), new Vector3(0, .5f, 0), .032f);
        Veil(root, name, new Vector4(0, -.1f, .95f, 1.5f), transition ? .92f : .2f, .12f, .7f, m => Fixed(m));
        Sparks(root, "Sparks Front", name, 3007, 14f, new Vector3(.85f, 1.4f, 1), new Vector3(0, .05f, 0), .026f);
        Ring(root, name);
        return Save(root, name);
    }

    static void Fixed(Material m)
    {
        m.SetFloat("_UseFixed", 1f);
        m.SetColor("_FixDeep", ShellDeep); m.SetColor("_FixMid", ShellMid); m.SetColor("_FixHot", new Color32(236, 255, 255, 255));
    }

    // ---- Parts ----

    static void Glow(GameObject root, string part, string owner, int queue, Vector4 shape, float power, float amount, float hotMix)
    {
        float halfWidth = shape.z * 1.05f, halfHeight = shape.w * 1.05f;
        Quad(root, part, owner + "-" + part, queue, -halfWidth, halfWidth, shape.y - halfHeight, shape.y + halfHeight, "AuraGlow", m =>
        {
            m.SetVector("_Shape", shape); m.SetFloat("_Power", power); m.SetFloat("_Amount", amount); m.SetFloat("_HotMix", hotMix);
        });
    }

    static void Veil(GameObject root, string owner, Vector4 shape, float wash, float add, float white, Action<Material> extra)
    {
        float halfWidth = shape.z * 1.05f, halfHeight = shape.w * 1.05f;
        Quad(root, "Veil Front", owner + "-Veil", 3004, -halfWidth, halfWidth, shape.y - halfHeight, shape.y + halfHeight, "AuraVeil", m =>
        {
            m.SetVector("_Shape", shape); m.SetFloat("_Wash", wash); m.SetFloat("_Add", add); m.SetFloat("_White", white);
            if (extra != null) extra(m);
        });
    }

    // CinematicAuraRig recognises "ShockRing" and animates its scale when charging starts.
    static void Ring(GameObject root, string owner)
    {
        var ring = Quad(root, "ShockRing Front", owner + "-Ring", 3006, -.5f, .5f, -.5f, .5f, "AuraGlow", m =>
        {
            m.SetVector("_Shape", new Vector4(0, 0, .5f, .5f)); m.SetFloat("_RingMix", 1f); m.SetFloat("_Amount", 1.1f); m.SetFloat("_HotMix", .6f);
        });
        ring.transform.localPosition = new Vector3(0, -1.1f, 0);
        ring.transform.localScale = new Vector3(1.2f, .18f, 1);
    }

    static MeshRenderer Quad(GameObject root, string part, string asset, int queue, float x0, float x1, float y0, float y1, string shader, Action<Material> setup)
    {
        var go = new GameObject(part);
        go.transform.SetParent(root.transform, false);
        var mesh = new Mesh { name = asset };
        mesh.vertices = new[] { new Vector3(x0, y0), new Vector3(x1, y0), new Vector3(x0, y1), new Vector3(x1, y1) };
        mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
        mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, Generated + "/" + Safe(asset) + ".asset");
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = Mat(shader, asset, queue, setup);
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    static Material Mat(string shader, string asset, int queue, Action<Material> setup)
    {
        var material = new Material(LoadShader(shader)) { name = asset };
        if (material.HasProperty("_Noise")) material.SetTexture("_Noise", noise);
        material.renderQueue = queue;
        if (setup != null) setup(material);
        AssetDatabase.CreateAsset(material, Generated + "/" + Safe(asset) + ".mat");
        return material;
    }

    static Shader LoadShader(string name)
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

    static string Safe(string asset) { return asset.Replace(' ', '-'); }

    static ParticleSystem Emitter(GameObject root, string part, string shader, string asset, int queue, Action<Material> setup, ParticleSystemRenderMode mode, float lengthScale, Vector3 emitScale, Vector3 centre, float thickness)
    {
        var go = new GameObject(part);
        go.transform.SetParent(root.transform, false);
        var system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = system.main;
        main.duration = 2f; main.loop = true; main.playOnAwake = true; main.startSpeed = 0f; main.startColor = Color.white;
        main.simulationSpace = ParticleSystemSimulationSpace.World; main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        var shape = system.shape;
        shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = 1f; shape.radiusThickness = thickness;
        shape.position = centre; shape.scale = emitScale;
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = Mat(shader, asset, queue, setup);
        renderer.renderMode = mode; renderer.lengthScale = lengthScale; renderer.velocityScale = 0f; renderer.cameraVelocityScale = 0f;
        renderer.sortMode = ParticleSystemSortMode.OldestInFront; renderer.minParticleSize = 0f; renderer.maxParticleSize = 8f;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
        return system;
    }

    static void Tongues(GameObject root, string part, string owner, int queue, Action<Material> setup, float rate, Vector3 emitScale, Vector3 centre, float lifeMin, float lifeMax, float sizeMin, float sizeMax, float length, float riseMin, float riseMax, float spread)
    {
        var system = Emitter(root, part, "AuraTongue", owner + "-" + part, queue, setup, ParticleSystemRenderMode.Stretch, length, emitScale, centre, .18f);
        var main = system.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        main.maxParticles = Mathf.CeilToInt(rate * lifeMax * 2.6f);
        var emission = system.emission;
        emission.rateOverTime = rate;
        var velocity = system.velocityOverLifetime;
        velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(-spread, spread);
        velocity.y = new ParticleSystem.MinMaxCurve(riseMin, riseMax);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.radial = new ParticleSystem.MinMaxCurve(spread * .9f);
        // Alpha is the life of the tongue, which the shader turns into erosion.
        var color = system.colorOverLifetime;
        color.enabled = true;
        color.color = new ParticleSystem.MinMaxGradient(Fade(0f, .22f, .6f, 1f));
        var size = system.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, .35f), new Keyframe(.3f, 1f), new Keyframe(1, .8f)));
        var renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream> { ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV, ParticleSystemVertexStream.StableRandomX });
    }

    static void Sparks(GameObject root, string part, string owner, int queue, float rate, Vector3 emitScale, Vector3 centre, float size)
    {
        var system = Emitter(root, part, "AuraSpark", owner + "-" + part, queue, null, ParticleSystemRenderMode.Stretch, 3.2f, emitScale, centre, 1f);
        var main = system.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(.4f, .95f);
        main.startSize = new ParticleSystem.MinMaxCurve(size * .55f, size);
        main.maxParticles = Mathf.CeilToInt(rate * 3.2f);
        var emission = system.emission;
        emission.rateOverTime = rate;
        var velocity = system.velocityOverLifetime;
        velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(-.28f, .28f);
        velocity.y = new ParticleSystem.MinMaxCurve(1.1f, 2.6f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        var noiseModule = system.noise;
        noiseModule.enabled = true; noiseModule.strength = .45f; noiseModule.frequency = 1.6f; noiseModule.scrollSpeed = .6f; noiseModule.quality = ParticleSystemNoiseQuality.Low;
        var color = system.colorOverLifetime;
        color.enabled = true;
        color.color = new ParticleSystem.MinMaxGradient(Fade(0f, .12f, .55f, 1f));
    }

    static Gradient Fade(float start, float inAt, float holdUntil, float end)
    {
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(start, 0), new GradientAlphaKey(1, inAt), new GradientAlphaKey(1, holdUntil), new GradientAlphaKey(0, end) });
        return gradient;
    }

    static string Save(GameObject root, string name)
    {
        string path = Generated + "/" + name.ToLowerInvariant() + ".prefab";
        PrefabUtility.SaveAsPrefabAsset(root, path);
        UnityEngine.Object.DestroyImmediate(root);
        return path;
    }

    // ---- Noise ----

    static Texture2D MakeNoise()
    {
        int size = NoiseSize;
        var soft = Layered(size, 4, 5, .52f, 11, v => v);
        var ridged = Layered(size, 4, 4, .55f, 23, v => 1f - Mathf.Abs(v * 2f - 1f));
        var billow = Layered(size, 3, 4, .5f, 37, v => Mathf.Abs(v * 2f - 1f));
        for (int i = 0; i < billow.Length; i++) billow[i] = 1f - billow[i];
        Normalize(soft); Normalize(ridged); Normalize(billow);
        var streaks = FlameStreaks(size, 420, 61);
        var pixels = new Color32[size * size];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color32(Byte(soft[i]), Byte(ridged[i]), Byte(billow[i]), Byte(streaks[i]));
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, true, true) { name = "AuraNoise", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear };
        texture.SetPixels32(pixels);
        texture.Apply(true, false);
        AssetDatabase.CreateAsset(texture, Generated + "/AuraNoise.asset");
        return texture;
    }

    static byte Byte(float value) { return (byte)Mathf.Clamp(Mathf.RoundToInt(value * 255f), 0, 255); }

    static void Normalize(float[] values)
    {
        float low = float.MaxValue, high = float.MinValue;
        foreach (float v in values) { low = Mathf.Min(low, v); high = Mathf.Max(high, v); }
        for (int i = 0; i < values.Length; i++) values[i] = (values[i] - low) / (high - low);
    }

    static float[] Layered(int size, int frequency, int octaves, float gain, int seed, Func<float, float> shape)
    {
        var result = new float[size * size];
        float amplitude = 1f;
        for (int octave = 0; octave < octaves; octave++)
        {
            int period = frequency << octave;
            var gradients = Gradients(period, seed + octave * 101);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    result[y * size + x] += amplitude * shape(Mathf.Clamp01(.5f + .9f * Perlin(x * period / (float)size, y * period / (float)size, period, gradients)));
            amplitude *= gain;
        }
        return result;
    }

    static Vector2[] Gradients(int period, int seed)
    {
        var random = new System.Random(seed);
        var gradients = new Vector2[period * period];
        for (int i = 0; i < gradients.Length; i++)
        {
            double angle = random.NextDouble() * Math.PI * 2;
            gradients[i] = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
        }
        return gradients;
    }

    static float Perlin(float x, float y, int period, Vector2[] gradients)
    {
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float fx = x - x0, fy = y - y0;
        Func<int, int, Vector2> at = (i, j) => gradients[((j % period + period) % period) * period + (i % period + period) % period];
        float a = Vector2.Dot(at(x0, y0), new Vector2(fx, fy)), b = Vector2.Dot(at(x0 + 1, y0), new Vector2(fx - 1, fy));
        float c = Vector2.Dot(at(x0, y0 + 1), new Vector2(fx, fy - 1)), d = Vector2.Dot(at(x0 + 1, y0 + 1), new Vector2(fx - 1, fy - 1));
        float u = fx * fx * fx * (fx * (fx * 6 - 15) + 10), v = fy * fy * fy * (fy * (fy * 6 - 15) + 10);
        return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
    }

    // Long lens-shaped strokes with sharp tips pointing up, painted by hand in the anime: every spike
    // in the Kaioken field and every tongue on the shell outlines comes from this channel.
    static float[] FlameStreaks(int size, int count, int seed)
    {
        var random = new System.Random(seed);
        var result = new float[size * size];
        for (int n = 0; n < count; n++)
        {
            float cx = (float)random.NextDouble() * size, cy = (float)random.NextDouble() * size;
            float halfHeight = Mathf.Lerp(46f, 150f, (float)random.NextDouble());
            float halfWidth = Mathf.Lerp(4f, 15f, (float)random.NextDouble()) * Mathf.Lerp(.7f, 1.15f, halfHeight / 150f);
            float lean = Mathf.Lerp(-.14f, .14f, (float)random.NextDouble()) * halfHeight;
            float level = Mathf.Lerp(.3f, 1f, Mathf.Pow((float)random.NextDouble(), .7f));
            int rows = Mathf.CeilToInt(halfHeight), columns = Mathf.CeilToInt(halfWidth + Mathf.Abs(lean)) + 2;
            for (int dy = -rows; dy <= rows; dy++)
            {
                float along = (dy + rows) / (2f * rows);
                float width = halfWidth * Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Pow(along, .58f)), .85f);
                if (width < .3f) continue;
                float offset = lean * along * along;
                for (int dx = -columns; dx <= columns; dx++)
                {
                    float across = Mathf.Abs(dx - offset) / width;
                    if (across >= 1f) continue;
                    float value = level * Mathf.Pow(1f - across, .55f);
                    int px = (Mathf.RoundToInt(cx) + dx + size * 2) % size, py = (Mathf.RoundToInt(cy) + dy + size * 2) % size;
                    int index = py * size + px;
                    if (value > result[index]) result[index] = value;
                }
            }
        }
        return result;
    }
}
