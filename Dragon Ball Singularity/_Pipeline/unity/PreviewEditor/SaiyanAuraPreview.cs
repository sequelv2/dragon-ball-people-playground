using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using DBPU;

// Renders the Super Saiyan auras to PNG frames with the same rig the game uses, so the look and the animation
// can be judged without the game. Copied into the temporary Unity project only by build_saiyanaura.ps1 -Preview.
// Colours come out as they should look on screen (Hdr 0); the game reaches the same through SOut.
public static class SaiyanAuraPreview
{
    public static void CommandLine()
    {
        var args = Environment.GetCommandLineArgs();
        string output = args[Array.IndexOf(args, "-previewOut") + 1];
        int frames = 24;
        int framesIndex = Array.IndexOf(args, "-previewFrames");
        if (framesIndex >= 0) frames = int.Parse(args[framesIndex + 1]);
        Directory.CreateDirectory(output);
        var assets = SaiyanAuraBuild.Generate();
        SaiyanAuraAssets.Flipbook = AssetDatabase.LoadAssetAtPath<Material>(assets[0]);
        SaiyanAuraAssets.Core = AssetDatabase.LoadAssetAtPath<GameObject>(assets[1]);
        SaiyanAuraAssets.LoadBolts(AssetDatabase.LoadAssetAtPath<TextAsset>(assets[2]).bytes);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        var camera = new GameObject("Preview camera").AddComponent<Camera>();
        camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor;
        camera.transform.position = new Vector3(0, .75f, -10);
        Figure();
        var dark = new Color(.17f, .19f, .22f);
        foreach (var id in new[] { "SSJ1", "SSJ2", "SSJ3" })
        {
            var aura = new GameObject("SaiyanAura/" + id);
            aura.transform.localScale = new Vector3(1.25f, 1.25f, 1f);
            // The rig's soles line (SaiyanAuraRig.Feet) on the stand-in's soles at y=-1.09.
            aura.transform.position = new Vector3(0, -1.09f - SaiyanAuraRig.Feet * 1.25f, 0);
            var rig = aura.AddComponent<SaiyanAuraRig>();
            rig.Setup(id, false, 7);
            float clock = 0, step = 1f / 30f;
            for (int i = 0; i < 30; i++) { rig.Tick(clock, step, 0, 0, 1, false, 0); rig.StepPreview(step); clock += step; }
            for (int frame = 0; frame < frames; frame++)
            {
                rig.Tick(clock, step, 0, 0, 1, false, 0);
                rig.StepPreview(step);
                Shot(camera, 2.9f, dark, 560, 760, Path.Combine(output, id + "-" + frame.ToString("000") + ".png"));
                clock += step;
            }
            rig.Tick(clock, step, 0, 0, 1, false, 0);
            Shot(camera, 2.9f, Color.black, 560, 760, Path.Combine(output, id + "-black.png"));
            Shot(camera, 2.9f, new Color(.72f, .75f, .72f), 560, 760, Path.Combine(output, id + "-bright.png"));
            Shot(camera, .9f, dark, 1000, 1000, Path.Combine(output, id + "-zoom.png"), new Vector3(-.7f, 1.3f, -10));
            Shot(camera, .3f, dark, 1000, 1000, Path.Combine(output, id + "-macro.png"), new Vector3(-.95f, 1.05f, -10));
            for (int i = 0; i < 60; i++) { clock += step; rig.Tick(clock, step, 1, 0, 1, false, 0); rig.StepPreview(step); }
            Shot(camera, 2.9f, dark, 560, 760, Path.Combine(output, id + "-charge.png"));
            UnityEngine.Object.DestroyImmediate(aura);
        }
        File.WriteAllText(Path.Combine(output, "done.txt"), "ok");
        Debug.Log("SAIYAN AURA PREVIEW COMPLETE");
    }

    static void Shot(Camera camera, float size, Color background, int width, int height, string file, Vector3? at = null)
    {
        var position = camera.transform.position;
        if (at.HasValue) camera.transform.position = at.Value;
        camera.orthographicSize = size; camera.backgroundColor = background;
        var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 1 };
        var previous = RenderTexture.active;
        camera.targetTexture = target; camera.Render();
        RenderTexture.active = target;
        var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
        File.WriteAllBytes(file, texture.EncodeToPNG());
        camera.targetTexture = null; RenderTexture.active = previous;
        camera.transform.position = position;
        UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(texture);
    }

    // A rough stand-in for the ragdoll at the size the game draws it: feet 1.09 below the aura origin.
    static void Figure()
    {
        var gi = new Color(.95f, .45f, .1f); var skin = new Color(.86f, .66f, .5f); var hair = new Color(1f, .86f, .25f); var blue = new Color(.12f, .2f, .55f);
        Block(0, .62f, .3f, .32f, skin);
        Block(0, .86f, .42f, .26f, hair);
        Block(0, .08f, .52f, .8f, gi);
        Block(-.33f, .12f, .15f, .74f, skin);
        Block(.33f, .12f, .15f, .74f, skin);
        Block(-.13f, -.68f, .2f, .82f, gi);
        Block(.13f, -.68f, .2f, .82f, gi);
        Block(-.13f, -1.04f, .22f, .1f, blue);
        Block(.13f, -1.04f, .22f, .1f, blue);
    }

    static void Block(float x, float y, float width, float height, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.position = new Vector3(x, y, 0);
        go.transform.localScale = new Vector3(width, height, 1);
        var material = new Material(Shader.Find("Sprites/Default")) { color = color };
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material; renderer.sortingOrder = 0;
    }
}
