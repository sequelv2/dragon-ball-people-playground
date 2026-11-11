using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace DBPU
{
    // These curves are sampled from the authored Blender action library. Runtime
    // only retargets the normalized flight to the actual surviving matter and slot.
    public static class BuuAssemblyMotion
    {
        public struct Pose
        {
            public float Approach, Side, Lift, Twist, Settle;
        }
        [Serializable] sealed class Clip
        {
            public float[] approach, side, lift, twist, settle;
        }
        [Serializable] sealed class Library
        {
            public int version, samples;
            public Clip[] variants;
        }
        static Library library;
        static bool attempted;
        public static bool SourceReady { get { Load(); return library != null; } }

        static void Load()
        {
            if (attempted) return;
            attempted = true;
            try
            {
                string path = Path.Combine(Singularity.ModDirectory, "Assets/VFX/MajinFlesh/assembly-motion.json");
                var loaded = JsonConvert.DeserializeObject<Library>(File.ReadAllText(path));
                if (loaded == null || loaded.version != 1 || loaded.variants == null || loaded.variants.Length != 8)
                    throw new InvalidDataException("Invalid Blender assembly action library");
                foreach (var clip in loaded.variants)
                    if (clip == null || clip.approach == null || clip.approach.Length != loaded.samples ||
                        clip.side == null || clip.side.Length != loaded.samples || clip.lift == null || clip.lift.Length != loaded.samples ||
                        clip.twist == null || clip.twist.Length != loaded.samples || clip.settle == null || clip.settle.Length != loaded.samples)
                        throw new InvalidDataException("Incomplete Blender assembly action");
                library = loaded;
            }
            catch (Exception error) { Debug.LogWarning("[Singularity] Blender assembly curves unavailable: " + error.Message); }
        }

        static float At(float[] values, float progress)
        {
            float position = Mathf.Clamp01(progress) * (values.Length - 1);
            int index = Mathf.Min(values.Length - 2, Mathf.FloorToInt(position));
            return Mathf.Lerp(values[index], values[index + 1], position - index);
        }

        public static Pose Sample(float progress, int variant)
        {
            Load();
            if (library == null) return new Pose { Approach = Mathf.SmoothStep(0f, 1f, progress), Lift = Mathf.Sin(progress * Mathf.PI) * .15f };
            var clip = library.variants[(variant % 8 + 8) % 8];
            return new Pose { Approach = At(clip.approach, progress), Side = At(clip.side, progress),
                Lift = At(clip.lift, progress), Twist = At(clip.twist, progress), Settle = At(clip.settle, progress) };
        }
    }
}
