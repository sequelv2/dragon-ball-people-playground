using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DBPU
{
    [DefaultExecutionOrder(-230)]
    public sealed class SpiritBombTargeting : MonoBehaviour
    {
        static SpiritBombTargeting instance;
        static readonly List<SpiritBombChannel> pending = new List<SpiritBombChannel>();
        static readonly Collider2D[] points = new Collider2D[64];
        static readonly bool scripted = Array.IndexOf(Environment.GetCommandLineArgs(), "-dbpu-ki-combat-qa") >= 0;
        public static bool InputCaptured { get { return instance && (instance.blocker || Time.frameCount <= instance.releaseFrame); } }
        public static SpiritBombChannel Active
        {
            get
            {
                for (int i = pending.Count-1; i >= 0; i--)
                { var channel = pending[i]; if (!channel || !channel.IsReady) { pending.RemoveAt(i); continue; } return channel; }
                return null;
            }
        }
        Global blocker;
        int releaseFrame = -1;
        LineRenderer reticle;
        PersonBehaviour highlighted;
        public static void Ensure()
        {
            if (instance) return;
            var go = new GameObject("DBPU Spirit Bomb target selection"); DontDestroyOnLoad(go);
            instance = go.AddComponent<SpiritBombTargeting>();
        }
        public static void Register(SpiritBombChannel channel)
        { Ensure(); pending.Remove(channel); if (channel && channel.IsReady) pending.Add(channel); }
        public static void Unregister(SpiritBombChannel channel) { pending.Remove(channel); }
        public static PersonBehaviour ResolveTarget(PersonBehaviour owner, Vector2 point)
        {
            int count = Physics2D.OverlapPointNonAlloc(point, points);
            LimbBehaviour nearest = null; float distance = float.MaxValue;
            for (int i=0;i<count;i++)
            {
                var col = points[i]; if (!col || col.isTrigger) continue;
                var limb = col.GetComponentInParent<LimbBehaviour>();
                if (!limb || limb.Health <= 0 || !limb.Person || limb.Person == owner || !limb.Person.IsAlive()) continue;
                float d = ((Vector2)limb.transform.position-point).sqrMagnitude;
                if (d < distance) { nearest = limb; distance = d; }
            }
            return nearest ? nearest.Person : null;
        }
        public static bool TryClickWorld(Vector2 point)
        {
            var channel = Active;
            if (!channel || !KiCombatMath.CanAct()) return false;
            var target = ResolveTarget(channel.Person,point);
            if (!target || !channel.LaunchAt(target)) return false;
            instance.CapturePointer(); return true;
        }
        void CapturePointer()
        {
            if (!blocker && Global.main) { blocker = Global.main; blocker.AddUiBlocker(); }
            highlighted = null; if (reticle) reticle.enabled = false;
        }
        void Update()
        {
            if (blocker)
            {
                if (!Input.GetMouseButton(0) || !KiCombatMath.HasFocus || blocker != Global.main || blocker.Paused)
                { blocker.RemoveUiBlocker(); blocker = null; releaseFrame = Time.frameCount; }
                return;
            }
            highlighted = null;
            if (scripted || !Active || !KiCombatMath.CanAct() || (EventSystem.current && EventSystem.current.IsPointerOverGameObject()))
            { if (reticle) reticle.enabled = false; return; }
            var camera = Global.main.camera; if (!camera) return;
            Vector2 point = camera.ScreenToWorldPoint(Input.mousePosition);
            highlighted = ResolveTarget(Active.Person,point);
            if (highlighted && Input.GetMouseButtonDown(0)) TryClickWorld(point);
        }
        void LateUpdate()
        {
            if (!highlighted || !Active) { if (reticle) reticle.enabled = false; return; }
            if (!reticle) reticle = KiSurgeArt.Line(transform,"Spirit Bomb enemy target",65);
            reticle.enabled = true;
            var chest = highlighted.transform.Find("Body/UpperBody");
            Vector2 centre = chest ? (Vector2)chest.position : (Vector2)highlighted.transform.position;
            float size = .85f;
            reticle.startWidth = reticle.endWidth = .026f;
            reticle.startColor = reticle.endColor = new Color(.6f,.94f,1,.9f);
            // A stable ring indicates the enemy that will receive the click.
            for(int i=0;i<65;i++)
            {
                float angle = i*Mathf.PI*2/64;
                float radius = size*(1+Mathf.Sin(angle*4)*.08f);
                reticle.SetPosition(i,new Vector3(centre.x+Mathf.Cos(angle)*radius,centre.y+Mathf.Sin(angle)*radius,-.4f));
            }
        }
        void OnDisable()
        {
            if (blocker) blocker.RemoveUiBlocker(); blocker=null;
            if (reticle) reticle.enabled=false; highlighted=null;
        }
        void OnDestroy() { OnDisable(); if (instance==this) { instance=null; pending.Clear(); } }
    }
}
