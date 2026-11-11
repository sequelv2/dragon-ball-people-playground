using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using DBPU;

// Renders the Blue auras to PNG sequences with the rig the game uses, so the look can be judged without the game.
// The preview shows display colours (no tonemapper and no bloom), the way the shaders are written.
// Copied into the temporary Unity project only by build_blueauras.ps1 -Preview.
public static class BlueAuraPreview
{
    const float Scale = 1.25f;

    public static void CommandLine()
    {
        var args = Environment.GetCommandLineArgs();
        string output = Arg(args, "-previewOut", null);
        string[] ids = Arg(args, "-previewIds", "SSJBlue,SSJBlueEvolution,KaiokenBlue").Split(',');
        int frames = int.Parse(Arg(args, "-previewFrames", "24"));
        Directory.CreateDirectory(output);
        var materials = BlueAuraBuild.Materials();
        BlueAuraAssets.UseMaterials(materials[0], materials[1], materials[2]);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        var camera = new GameObject("Preview camera").AddComponent<Camera>();
        camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor;
        camera.transform.position = new Vector3(0, 1.75f, -10);
        Figure();
        foreach (var id in ids)
        {
            var aura = new GameObject(id);
            aura.transform.localScale = new Vector3(Scale, Scale, 1);
            aura.transform.position = new Vector3(0, .875f * Scale, 0);
            var rig = aura.AddComponent<BlueAuraRig>();
            rig.Setup(id, false, 7);
            float clock = 0, step = 1f / 24f;
            for (int i = 0; i < 30; i++) { rig.Tick(clock, step, 0, 0, 1, false, 0); clock += step; }
            for (int f = 0; f < frames; f++)
            {
                rig.Tick(clock, step, 0, 0, 1, false, 0);
                Shot(camera, 3.1f, Color.black, 600, 800, Path.Combine(output, id + "-idle-" + f.ToString("00") + ".png"));
                clock += step;
            }
            rig.Tick(clock, step, 0, 0, 1, false, 0);
            Shot(camera, 3.1f, new Color(.19f, .21f, .24f), 600, 800, Path.Combine(output, id + "-grey.png"));
            Shot(camera, 3.1f, new Color(.62f, .66f, .7f), 600, 800, Path.Combine(output, id + "-bright.png"));
            for (int i = 0; i < 36; i++) { rig.Tick(clock, step, 1, 0, 1, false, 0); clock += step; }
            for (int f = 0; f < 6; f++)
            {
                rig.Tick(clock, step, 1, 0, 1, false, 0);
                Shot(camera, 3.1f, Color.black, 600, 800, Path.Combine(output, id + "-charge-" + f.ToString("00") + ".png"));
                clock += step;
            }
            UnityEngine.Object.DestroyImmediate(aura);
        }
        File.WriteAllText(Path.Combine(output, "done.txt"), "ok");
        Debug.Log("BLUE AURA PREVIEW COMPLETE");
    }

    static string Arg(string[] args, string name, string fallback)
    {
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
    }

    static void Shot(Camera camera, float size, Color background, int width, int height, string file)
    {
        camera.orthographicSize = size; camera.backgroundColor = background;
        var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var previous = RenderTexture.active;
        camera.targetTexture = target; camera.Render();
        RenderTexture.active = target;
        var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
        File.WriteAllBytes(file, texture.EncodeToPNG());
        camera.targetTexture = null; RenderTexture.active = previous;
        UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(texture);
    }

    // A rough stand-in for the ragdoll at the size the game draws it: feet at 0, 2.19 tall.
    static void Figure()
    {
        var gi = new Color(.18f, .3f, .7f); var skin = new Color(.86f, .66f, .5f); var hair = new Color(.25f, .75f, 1f); var belt = new Color(.95f, .5f, .1f);
        Block(0, 1.93f, .3f, .34f, skin);
        Block(0, 2.12f, .4f, .2f, hair);
        Block(0, 1.42f, .5f, .7f, gi);
        Block(0, 1.07f, .48f, .08f, belt);
        Block(-.33f, 1.3f, .14f, .78f, skin);
        Block(.33f, 1.3f, .14f, .78f, skin);
        Block(-.12f, .5f, .2f, 1f, gi);
        Block(.12f, .5f, .2f, 1f, gi);
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
