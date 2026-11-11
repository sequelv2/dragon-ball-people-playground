using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using DBPU;

// Renders the auras to PNGs with the same rig the game uses, so the look can be judged without the game.
// Copied into the temporary Unity project only by build_blueauras.ps1 -Preview.
public static class BlueAuraPreview
{
    struct Palette { public string Id; public Color Deep, Mid, Hot; }
    static Color C(int r, int g, int b) { return new Color32((byte)r, (byte)g, (byte)b, 255); }
    // Keep in step with RefinedAuras.Looks.
    static readonly Palette[] Palettes =
    {
        new Palette { Id = "SSJBlue", Deep = C(17, 112, 232), Mid = C(37, 196, 245), Hot = C(214, 250, 255) },
        new Palette { Id = "SSJBlueEvolution", Deep = C(43, 58, 222), Mid = C(58, 150, 232), Hot = C(214, 242, 250) },
        new Palette { Id = "KaiokenBlue", Deep = C(205, 6, 62), Mid = C(255, 15, 87), Hot = C(255, 150, 196) },
    };

    public static void CommandLine()
    {
        var args = Environment.GetCommandLineArgs();
        string output = args[Array.IndexOf(args, "-previewOut") + 1];
        Directory.CreateDirectory(output);
        BlueAuraBuild.GenerateAll();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        var camera = new GameObject("Preview camera").AddComponent<Camera>();
        camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor;
        camera.transform.position = new Vector3(0, .6f, -10);
        Figure(new Vector3(0, 0, 0));
        foreach (var palette in Palettes)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BlueAuraBuild.Generated + "/" + palette.Id.ToLowerInvariant() + ".prefab");
            var aura = UnityEngine.Object.Instantiate(prefab);
            aura.transform.localScale = new Vector3(1.25f, 1.25f, 1f);
            aura.transform.position = new Vector3(0, .125f, 0);
            var rig = aura.AddComponent<CinematicAuraRig>();
            rig.Setup(palette.Id, false, 7);
            rig.SetPalette(palette.Deep, palette.Mid, palette.Hot);
            rig.AdvancePreview(1.2f);
            float clock = 3f, step = 1f / 12f;
            for (int frame = 0; frame < 4; frame++)
            {
                rig.Tick(clock, step, 0, 0, 1, false, 0);
                Shot(camera, 4.2f, new Color(.19f, .21f, .24f), 720, 960, Path.Combine(output, palette.Id + "-" + frame + ".png"));
                rig.StepPreview(step); clock += step;
            }
            rig.Tick(clock, step, 0, 0, 1, false, 0);
            Shot(camera, 4.2f, new Color(.72f, .75f, .72f), 720, 960, Path.Combine(output, palette.Id + "-bright.png"));
            Shot(camera, 12f, new Color(.19f, .21f, .24f), 360, 480, Path.Combine(output, palette.Id + "-far.png"));
            // The charge envelope, as AuraHandler applies it.
            for (int i = 0; i < 40; i++) { clock += step * .25f; rig.StepPreview(step * .25f); rig.Tick(clock, step * .25f, 1, 0, 1, false, 0); }
            aura.transform.localScale = new Vector3(1.25f * 1.3f, 1.25f * 1.22f, 1f);
            Shot(camera, 5.4f, new Color(.19f, .21f, .24f), 720, 960, Path.Combine(output, palette.Id + "-charge.png"));
            UnityEngine.Object.DestroyImmediate(aura);
        }
        File.WriteAllText(Path.Combine(output, "done.txt"), "ok");
        Debug.Log("BLUE AURA PREVIEW COMPLETE");
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

    // A rough stand-in for the ragdoll, at the size the game draws it, to judge the aura against.
    static void Figure(Vector3 origin)
    {
        var body = new Color(.16f, .24f, .55f); var skin = new Color(.86f, .66f, .5f); var hair = new Color(.1f, .45f, .95f);
        Block(origin, 0, .95f, .36f, .36f, skin);
        Block(origin, 0, 1.2f, .46f, .26f, hair);
        Block(origin, 0, .25f, .62f, .95f, body);
        Block(origin, -.42f, .3f, .18f, .85f, skin);
        Block(origin, .42f, .3f, .18f, .85f, skin);
        Block(origin, -.17f, -.8f, .24f, 1.15f, body);
        Block(origin, .17f, -.8f, .24f, 1.15f, body);
    }

    static void Block(Vector3 origin, float x, float y, float width, float height, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.position = origin + new Vector3(x, y, 0);
        go.transform.localScale = new Vector3(width, height, 1);
        var material = new Material(Shader.Find("Sprites/Default")) { color = color };
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material; renderer.sortingOrder = 0;
    }
}
