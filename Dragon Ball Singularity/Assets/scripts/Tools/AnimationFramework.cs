using UnityEngine;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Mod
{
    [Serializable]
    public class LimbData
    {
        public Vector3 WorldOffset;
        public Quaternion Rotation;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public Vector3? localScale;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string LayerID;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int LayerOrder;
    }

    [Serializable]
    public class FrameData
    {
        public List<LimbData> Limbs = new List<LimbData>();
        public float FrameTime;
        public Vector3 SavedCentroid;
        public Vector3 RootScale;
        public bool IsFlipped;
    }

    [Serializable]
    public class AnimationData
    {
        public string Name;
        public List<FrameData> Frames = new List<FrameData>();
        public bool SmoothInterpolation = true;
    }

    public static class AnimationFramework
    {
        public static void EnsureManager()
        {
            var go = GameObject.Find("AnimationFrameworkManager");
            if (go == null)
            {
                go = new GameObject("AnimationFrameworkManager");
                UnityEngine.Object.DontDestroyOnLoad(go);
            }

            if (go.GetComponent<AnimationFrameworkManager>() == null)
                go.AddComponent<AnimationFrameworkManager>();
        }

        public static AnimationData LoadAnimation(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Debug.LogError("[AnimationFramework] Animation file not found: " + filePath);
                return null;
            }

            string json = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<AnimationData>(json);
        }

        public static IEnumerator PlayAnimationRoutine(Transform root, string animationFilePath, Action<int, Transform> onFrame = null, float playbackSpeed = 1f, bool loop = false, Func<bool> shouldContinue = null, Func<Vector3> movementOffset = null, Func<Vector3> targetCentroid = null)
        {
            AnimationData anim = LoadAnimation(animationFilePath);
            if (anim == null)
            {
                Debug.LogError("[AnimationFramework] AnimationData is null for file: " + animationFilePath);
                yield break;
            }

            if (anim.Frames == null || anim.Frames.Count == 0)
            {
                Debug.LogError("[AnimationFramework] AnimationData.Frames is null or empty for file: " + animationFilePath);
                yield break;
            }

            bool smoothInterpolation = anim.SmoothInterpolation;
            var allLimbs = root.GetComponentsInChildren<PhysicalBehaviour>(true);

            List<Vector3> originalScale = new List<Vector3>();
            List<string> originalLayerID = new List<string>();
            List<int> originalLayerOrder = new List<int>();

            Vector3 c = Vector3.zero;
            foreach (var l in allLimbs)
            {
                c += l.transform.position;
                originalScale.Add(l.transform.localScale);
                originalLayerID.Add(l.gameObject.GetComponent<SpriteRenderer>().sortingLayerName);
                originalLayerOrder.Add(l.gameObject.GetComponent<SpriteRenderer>().sortingOrder);
            }

            c /= allLimbs.Length;

            do
            {
                for (int i = 0; i < anim.Frames.Count - 1; i++)
                {
                    if (shouldContinue != null && !shouldContinue())
                        yield break;

                    var a = anim.Frames[i];
                    var b = anim.Frames[i + 1];
                    float scaleA = Mathf.Abs(root.localScale.x) / Mathf.Abs(a.RootScale.x);
                    float scaleB = Mathf.Abs(root.localScale.x) / Mathf.Abs(b.RootScale.x);
                    bool currentFlipped = root.localScale.x < 0;
                    bool aFlipped = a.IsFlipped;
                    bool bFlipped = b.IsFlipped;
                    float d = a.FrameTime;
                    float e = 0f;

                    onFrame?.Invoke(i, root);

                    while (e < d)
                    {
                        if (shouldContinue != null && !shouldContinue())
                            yield break;

                        Vector3 movementDelta = Vector3.zero;
                        if (movementOffset != null)
                            movementDelta = movementOffset();

                        if (targetCentroid != null)
                            c = targetCentroid() + movementDelta;
                        else
                        {
                            Vector3 baseCentroid = Vector3.zero;
                            foreach (var l in allLimbs)
                                baseCentroid += l.transform.position;
                            baseCentroid /= allLimbs.Length;
                            c = baseCentroid + movementDelta;
                        }

                        float t = smoothInterpolation ? e / d : (e >= d * 0.5f ? 1f : 0f);
                        int limbCount = Math.Min(Math.Min(a.Limbs.Count, b.Limbs.Count), allLimbs.Length);
                        for (int k = 0; k < limbCount; k++)
                        {
                            var la = a.Limbs[k];
                            var lb = b.Limbs[k];
                            var p = allLimbs[k];
                            if (p == null) continue;

                            float limbScale = (p.transform.localScale.x + p.transform.localScale.y) / 2f;
                            if (limbScale < 0.1f) limbScale = 1f;

                            Vector3 pa = la.WorldOffset;
                            Vector3 pb = lb.WorldOffset;

                            if (currentFlipped != aFlipped) pa.x = -pa.x;
                            if (currentFlipped != bFlipped) pb.x = -pb.x;

                            pa = c + pa * scaleA * limbScale;
                            pb = c + pb * scaleB * limbScale;

                            Quaternion rotA = la.Rotation;
                            Quaternion rotB = lb.Rotation;

                            if (currentFlipped != aFlipped)
                            {
                                Vector3 eulerA = rotA.eulerAngles;
                                eulerA.z = 360f - eulerA.z;
                                rotA = Quaternion.Euler(eulerA);
                            }

                            if (currentFlipped != bFlipped)
                            {
                                Vector3 eulerB = rotB.eulerAngles;
                                eulerB.z = 360f - eulerB.z;
                                rotB = Quaternion.Euler(eulerB);
                            }

                            Vector3 interpScale = p.transform.localScale;
                            bool hasA = la.localScale.HasValue;
                            bool hasB = lb.localScale.HasValue;
                            if (hasA || hasB)
                            {
                                var targetA = hasA ? la.localScale.Value : interpScale;
                                var targetB = hasB ? lb.localScale.Value : targetA;
                                interpScale = smoothInterpolation ? Vector3.Lerp(targetA, targetB, t) : (t < 1f ? targetA : targetB);
                                interpScale = new Vector3(interpScale.x * originalScale[k].x, interpScale.y * originalScale[k].y, interpScale.z * originalScale[k].z);
                                if (Mathf.Approximately(interpScale.x, 0f)) interpScale.x = p.transform.localScale.x * originalScale[k].x;
                                if (Mathf.Approximately(interpScale.y, 0f)) interpScale.y = p.transform.localScale.y * originalScale[k].y;
                                if (Mathf.Approximately(interpScale.z, 0f)) interpScale.z = p.transform.localScale.z * originalScale[k].z;
                            }

                            if (smoothInterpolation)
                            {
                                p.transform.position = Vector3.Lerp(pa, pb, t);
                                p.transform.rotation = Quaternion.Slerp(rotA, rotB, t);
                                if (hasA || hasB) p.transform.localScale = interpScale;
                            }
                            else
                            {
                                if (t < 1f)
                                {
                                    p.transform.position = pa;
                                    p.transform.rotation = rotA;
                                    if (hasA || hasB) p.transform.localScale = interpScale;
                                }
                                else
                                {
                                    p.transform.position = pb;
                                    p.transform.rotation = rotB;
                                    if (hasA || hasB) p.transform.localScale = interpScale;
                                }
                            }
                        }

                        e += Time.deltaTime * playbackSpeed;
                        yield return null;
                    }

                    Vector3 movementDeltaFinal = movementOffset != null ? movementOffset() : Vector3.zero;
                    Vector3 movedCentroidFinal;
                    if (targetCentroid != null)
                        movedCentroidFinal = targetCentroid() + movementDeltaFinal;
                    else
                    {
                        Vector3 baseCentroidFinal = Vector3.zero;
                        foreach (var l in allLimbs)
                            baseCentroidFinal += l.transform.position;
                        baseCentroidFinal /= allLimbs.Length;
                        movedCentroidFinal = baseCentroidFinal + movementDeltaFinal;
                    }

                    int limbCount2 = Math.Min(b.Limbs.Count, allLimbs.Length);
                    for (int k = 0; k < limbCount2; k++)
                    {
                        var lb = b.Limbs[k];
                        var la = a.Limbs[k];
                        var p = allLimbs[k];
                        if (p == null) continue;

                        float limbScaleB = (p.transform.localScale.x + p.transform.localScale.y) / 2f;
                        if (limbScaleB < 0.1f) limbScaleB = 1f;

                        Vector3 pb = lb.WorldOffset;
                        Quaternion rotB = lb.Rotation;
                        if (currentFlipped != bFlipped)
                        {
                            pb.x = -pb.x;
                            Vector3 eulerB = rotB.eulerAngles;
                            eulerB.z = 360f - eulerB.z;
                            rotB = Quaternion.Euler(eulerB);
                        }

                        p.transform.position = movedCentroidFinal + pb * scaleB * limbScaleB;
                        p.transform.rotation = rotB;

                        if (la.localScale.HasValue && lb.localScale.HasValue && lb.localScale.Value != la.localScale.Value)
                        {
                            var ls = lb.localScale.Value;
                            ls = new Vector3(ls.x * originalScale[k].x, ls.y * originalScale[k].y, ls.z * originalScale[k].z);
                            if (Mathf.Approximately(ls.x, 0f)) ls.x = p.transform.localScale.x * originalScale[k].x;
                            if (Mathf.Approximately(ls.y, 0f)) ls.y = p.transform.localScale.y * originalScale[k].y;
                            if (Mathf.Approximately(ls.z, 0f)) ls.z = p.transform.localScale.z * originalScale[k].z;
                            p.transform.localScale = ls;
                        }

                        if (la.LayerID != lb.LayerID)
                            p.gameObject.GetComponent<SpriteRenderer>().sortingLayerName = la.LayerID;

                        if (la.LayerOrder != lb.LayerOrder)
                            p.gameObject.GetComponent<SpriteRenderer>().sortingOrder = la.LayerOrder;
                    }
                }

                onFrame?.Invoke(anim.Frames.Count - 1, root);
            } while (loop && (shouldContinue == null || shouldContinue()));

            for (int k = 0; k < allLimbs.Length; k++)
            {
                var p = allLimbs[k];
                if (p == null) continue;
                p.transform.localScale = originalScale[k];
                p.gameObject.GetComponent<SpriteRenderer>().sortingLayerName = originalLayerID[k];
                p.gameObject.GetComponent<SpriteRenderer>().sortingOrder = originalLayerOrder[k];
            }
        }

        public static void PlayAnimation(Transform target, string animationPath, Action<int, Transform> onFrame = null, float playbackSpeed = 1f, bool loop = false, Func<bool> shouldContinue = null, Func<Vector3> movementOffset = null, Func<Vector3> targetCentroid = null)
        {
            EnsureManager();

            var manager = GameObject.Find("AnimationFrameworkManager");
            if (manager == null)
            {
                Debug.LogError("[AnimationFramework] Manager not found, cannot PlayAnimation.");
                return;
            }

            var info = new Hashtable
            {
                ["Target"] = target,
                ["AnimationPath"] = animationPath,
                ["OnFrame"] = onFrame,
                ["PlaybackSpeed"] = playbackSpeed,
                ["Loop"] = loop
            };
            if (shouldContinue != null) info["ShouldContinue"] = shouldContinue;
            if (movementOffset != null) info["MovementOffset"] = movementOffset;
            if (targetCentroid != null) info["TargetCentroid"] = targetCentroid;

            manager.SendMessage("PlayAnimationEventDynamic", info);
        }
    }

    public class AnimationFrameworkManager : MonoBehaviour
    {
        void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        public void PlayAnimationEvent(AnimationEventInfo info)
        {
            if (info == null || info.Target == null || string.IsNullOrEmpty(info.AnimationPath))
                return;

            StartCoroutine(AnimationFramework.PlayAnimationRoutine(info.Target, info.AnimationPath, info.OnFrame, info.PlaybackSpeed));
        }

        public void PlayAnimationEventDynamic(object infoObj)
        {
            if (infoObj == null) return;

            Transform target = null;
            string path = null;
            Action<int, Transform> onFrame = null;
            Func<bool> shouldContinue = null;
            Func<Vector3> movementOffset = null;
            Func<Vector3> targetCentroid = null;
            float playbackSpeed = 1f;
            bool loop = false;

            if (infoObj is IDictionary dict)
            {
                if (dict.Contains("Target")) target = dict["Target"] as Transform;
                if (dict.Contains("AnimationPath")) path = dict["AnimationPath"] as string;
                if (dict.Contains("OnFrame")) onFrame = dict["OnFrame"] as Action<int, Transform>;
                if (dict.Contains("PlaybackSpeed")) playbackSpeed = Convert.ToSingle(dict["PlaybackSpeed"]);
                if (dict.Contains("Loop")) loop = Convert.ToBoolean(dict["Loop"]);
                if (dict.Contains("ShouldContinue")) shouldContinue = dict["ShouldContinue"] as Func<bool>;
                if (dict.Contains("MovementOffset")) movementOffset = dict["MovementOffset"] as Func<Vector3>;
                if (dict.Contains("TargetCentroid")) targetCentroid = dict["TargetCentroid"] as Func<Vector3>;

                if (onFrame == null && dict.Contains("EventKey"))
                {
                    var key = dict["EventKey"] as string;
                    if (!string.IsNullOrEmpty(key))
                        onFrame = FrameEventRegistry.Get(key);
                }

                if (shouldContinue == null && dict.Contains("ShouldContinueKey"))
                {
                    var key = dict["ShouldContinueKey"] as string;
                    if (!string.IsNullOrEmpty(key)) shouldContinue = BoolFuncRegistry.Get(key);
                }

                if (movementOffset == null && dict.Contains("MovementOffsetKey"))
                {
                    var key = dict["MovementOffsetKey"] as string;
                    if (!string.IsNullOrEmpty(key)) movementOffset = Vec3FuncRegistry.Get(key);
                }

                if (targetCentroid == null && dict.Contains("TargetCentroidKey"))
                {
                    var key = dict["TargetCentroidKey"] as string;
                    if (!string.IsNullOrEmpty(key)) targetCentroid = Vec3FuncRegistry.Get(key);
                }
            }

            if (target == null || string.IsNullOrEmpty(path)) return;

            StartCoroutine(AnimationFramework.PlayAnimationRoutine(target, path, onFrame, playbackSpeed, loop, shouldContinue, movementOffset, targetCentroid));
        }
    }

    public class AnimationEventInfo
    {
        public Transform Target { get; set; }
        public string AnimationPath { get; set; }
        public Action<int, Transform> OnFrame { get; set; }
        public float PlaybackSpeed { get; set; } = 1f;
    }

    public static class FrameEventRegistry
    {
        private static Dictionary<string, Action<int, Transform>> events = new Dictionary<string, Action<int, Transform>>();

        public static void Register(string key, Action<int, Transform> callback)
        {
            events[key] = callback;
        }

        public static Action<int, Transform> Get(string key)
        {
            events.TryGetValue(key, out var cb);
            return cb;
        }
    }

    public static class BoolFuncRegistry
    {
        private static Dictionary<string, Func<bool>> events = new Dictionary<string, Func<bool>>();
        public static void Register(string key, Func<bool> callback) { events[key] = callback; }
        public static Func<bool> Get(string key) { events.TryGetValue(key, out var cb); return cb; }
    }

    public static class Vec3FuncRegistry
    {
        private static Dictionary<string, Func<Vector3>> events = new Dictionary<string, Func<Vector3>>();
        public static void Register(string key, Func<Vector3> callback) { events[key] = callback; }
        public static Func<Vector3> Get(string key) { events.TryGetValue(key, out var cb); return cb; }
    }
}
