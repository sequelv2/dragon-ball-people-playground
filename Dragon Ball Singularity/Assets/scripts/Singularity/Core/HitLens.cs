using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DBPU
{
    // Brief gravitational lenses at the point of a blow, one image effect on the game camera.
    // It only stays enabled while a lens is alive, because an enabled OnRenderImage forces an
    // intermediate render texture every frame. Without its bundle every pulse is a no-op.
    [RequireComponent(typeof(Camera)), DefaultExecutionOrder(315)]
    public sealed class HitLens : MonoBehaviour
    {
        const int Capacity = 8;
        sealed class Lens { public Vector2 Position; public float Einstein, Reach, Age, Life, Ring; public Color Tint; }
        static string directory; static bool attempted; static AssetBundle bundle; static Material source; static HitLens current;
        readonly List<Lens> lenses = new List<Lens>(Capacity);
        readonly Vector4[] lens = new Vector4[Capacity], shape = new Vector4[Capacity], tint = new Vector4[Capacity];
        Camera view; Material material; bool failed;

        public static int ActiveCount { get { return current ? current.lenses.Count : 0; } }
        public static bool Rendering { get { return current && current.enabled; } }
        public static void Initialize(string path) { directory = path; }

        static bool Ready
        {
            get
            {
                if (attempted) return source;
                attempted = true;
                try
                {
                    bundle = AssetBundle.LoadFromFile(Path.Combine(directory, "Assets/VFX/HitLens/hitlens"));
                    if (!bundle) throw new IOException("Bundle is missing");
                    var value = bundle.LoadAsset<Material>("assets/hitlens/hitlens.mat");
                    if (!value || !value.shader || !value.shader.isSupported) throw new IOException("Shader is unsupported");
                    source = value;
                }
                catch (Exception ex) { Debug.LogWarning("[Singularity] Hit lens unavailable: " + ex.Message); }
                return source;
            }
        }

        // einstein is in world units: the radius of the ring light bends around.
        public static void Pulse(Vector2 at, float einstein, float seconds, Color color, float ring = .35f)
        {
            if (!Ready) return;
            var camera = Global.main ? Global.main.camera : Camera.main;
            if (!camera) return;
            if (!current || current.view != camera)
            {
                current = camera.GetComponent<HitLens>();
                if (!current) current = camera.gameObject.AddComponent<HitLens>();
            }
            current.enabled = true;
            current.Add(new Lens { Position = at, Einstein = einstein, Reach = einstein * 7f, Life = Mathf.Max(.05f, seconds), Ring = ring, Tint = color });
        }

        void Add(Lens item)
        {
            if (lenses.Count >= Capacity)
            {
                // The lens closest to finishing makes room for the new blow.
                int oldest = 0;
                for (int i = 1; i < lenses.Count; i++) if (lenses[i].Age / lenses[i].Life > lenses[oldest].Age / lenses[oldest].Life) oldest = i;
                lenses.RemoveAt(oldest);
            }
            lenses.Add(item);
        }

        void Awake()
        {
            view = GetComponent<Camera>();
            material = new Material(source) { name = "DBPU hit lens instance", hideFlags = HideFlags.HideAndDontSave };
        }

        void LateUpdate()
        {
            if (DbpuFx.Paused) return;
            // Real time: the bend should still bloom during the hitstop of the same blow.
            for (int i = lenses.Count - 1; i >= 0; i--)
            {
                lenses[i].Age += Time.unscaledDeltaTime;
                if (lenses[i].Age >= lenses[i].Life) lenses.RemoveAt(i);
            }
            if (lenses.Count == 0) enabled = false;
        }

        void OnDestroy()
        {
            if (material) Destroy(material);
            if (current == this) current = null;
        }

        void OnRenderImage(RenderTexture sourceImage, RenderTexture destination)
        {
            if (failed || lenses.Count == 0) { Graphics.Blit(sourceImage, destination); return; }
            try
            {
                float height = Mathf.Max(.01f, view.orthographicSize * 2f);
                for (int i = 0; i < lenses.Count; i++)
                {
                    var l = lenses[i];
                    float k = Mathf.Sin(Mathf.Pow(Mathf.Clamp01(l.Age / l.Life), .55f) * Mathf.PI);
                    Vector3 vp = view.WorldToViewportPoint(new Vector3(l.Position.x, l.Position.y, 0f));
                    lens[i] = new Vector4(vp.x, vp.y, l.Einstein * k / height, l.Reach / height);
                    shape[i] = new Vector4(.35f, l.Ring * k, 0f, 0f);
                    tint[i] = l.Tint;
                }
                material.SetInt("_LensCount", lenses.Count);
                material.SetVectorArray("_Lens", lens);
                material.SetVectorArray("_LensB", shape);
                material.SetVectorArray("_LensTint", tint);
                Graphics.Blit(sourceImage, destination, material);
            }
            catch (Exception ex)
            {
                failed = true;
                Debug.LogWarning("[Singularity] Hit lens disabled: " + ex.Message);
                Graphics.Blit(sourceImage, destination);
            }
        }
    }
}
