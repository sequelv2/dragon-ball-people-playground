using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace DBPU
{
    // The same Atom Bomb shockwave prefab used by PunchPower/KickPower.NukeShockwave.
    // Run its native animation/effector without its nuclear Start (global heat and EMP).
    public sealed class SpiritNukeShockwave : MonoBehaviour
    {
        static readonly HashSet<SpiritNukeShockwave> live = new HashSet<SpiritNukeShockwave>();
        static readonly MethodInfo nativeUpdate = typeof(NewAtomBombExplosionBehaviour).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
        public static int ActiveCount { get { return live.Count; } }
        public NewAtomBombExplosionBehaviour NativeEffect { get; private set; }
        public float Age { get; private set; }
        public float Lifetime { get; private set; }
        Action tick;

        public static SpiritNukeShockwave Emit(Vector2 position)
        {
            GameObject root = null;
            try
            {
                var atom = ModAPI.FindSpawnable("Atom Bomb").Prefab.GetComponent<AtomBombBehaviour>();
                root = Instantiate(atom.ExplosionPrefab, position, Quaternion.identity);
                var effect = root.GetComponent<NewAtomBombExplosionBehaviour>();
                effect.enabled = false; // Disable before Unity can invoke nuclear Start.
                effect.VaporisationDistance = 0;
                if (root.transform.childCount > 0)
                {
                    var mushroom = root.transform.GetChild(0).GetComponent<ParticleSystem>();
                    if (mushroom) mushroom.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
                var wave = root.AddComponent<SpiritNukeShockwave>();
                wave.NativeEffect = effect;
                wave.tick = (Action)Delegate.CreateDelegate(typeof(Action), effect, nativeUpdate);
                var keys = effect.ShockwaveGrowth.keys;
                float duration = keys.Length > 0 ? keys[keys.Length - 1].time / Mathf.Max(.001f, effect.SpeedMultiplier) : 5;
                wave.Lifetime = Mathf.Clamp(duration + .25f, .5f, 30);
                live.Add(wave);
                return wave;
            }
            catch (Exception ex)
            {
                if (root) Destroy(root);
                Debug.LogError("[DBPU Spirit Bomb] Native Nuke Shockwave unavailable: " + ex);
                return null;
            }
        }
        void Update()
        {
            if (Global.main && Global.main.Paused) return;
            if (!NativeEffect || tick == null) { Destroy(gameObject); return; }
            tick();
            Age += Time.deltaTime;
            if (Age >= Lifetime) Destroy(gameObject);
        }
        void OnDestroy() { live.Remove(this); }
    }
}
