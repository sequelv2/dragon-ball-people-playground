using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DBPU
{
    // Shared aiming for thrown hand techniques (Kienzan, Hakai Ball).
    //  - The direction comes from the cursor relative to the projectile, not from the forearm:
    //    a raised Kienzan hand points straight up, so the old fallback threw the disc upwards
    //    whenever the cursor was near the fighter.
    //  - Inside a dead zone the last good direction is kept (or the way the fighter faces).
    //  - While aiming, a dotted guide and a reticle show where the technique will go.
    public sealed class DbpuAimGuide
    {
        const float DeadZone = .75f;
        Vector2 last; bool hasLast;
        GameObject root; LineRenderer line; SpriteRenderer reticle, ring; float clock;

        public void Reset() { hasLast = false; }

        public static Vector2 Facing(PersonBehaviour person)
        {
            return person && person.transform.localScale.x < 0f ? Vector2.left : Vector2.right;
        }

        public Vector2 Direction(Vector2 origin, PersonBehaviour person)
        {
            return DirectionTo(origin, DbpuFx.MouseWorld(), person);
        }

        public Vector2 DirectionTo(Vector2 origin, Vector2 target, PersonBehaviour person)
        {
            Vector2 d = target - origin;
            if (d.sqrMagnitude > DeadZone * DeadZone) { last = d.normalized; hasLast = true; }
            return hasLast ? last : Facing(person);
        }

        public void Show(Vector2 origin, Vector2 direction, Color color, float strength = 1f)
        {
            if (!root)
            {
                root = new GameObject("DBPU technique aim guide");
                line = DbpuFx.Line(root.transform, "Aim line", 60);
                reticle = DbpuFx.MakeSprite(root.transform, "Aim reticle", DbpuFx.Cross, 61, true);
                ring = DbpuFx.MakeSprite(root.transform, "Aim ring", DbpuFx.Ring, 61, true);
            }
            clock += Time.unscaledDeltaTime;
            Vector2 cursor = DbpuFx.MouseWorld();
            float reach = Vector2.Distance(origin, cursor);
            bool onCursor = reach > DeadZone;
            float length = onCursor ? reach : 6f;
            Vector2 end = origin + direction * length;

            // Dotted guide: segments drawn as a single strip with transparent gaps.
            int dots = Mathf.Clamp(Mathf.CeilToInt(length / .32f), 2, 90);
            line.positionCount = dots * 2;
            line.startWidth = line.endWidth = .045f;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(.85f * strength, 0f), new GradientAlphaKey(.25f * strength, 1f) });
            line.colorGradient = gradient;
            float phase = Mathf.Repeat(clock * 1.6f, 1f);
            for (int i = 0; i < dots; i++)
            {
                float a = Mathf.Clamp01((i + phase) / dots), b = Mathf.Clamp01((i + phase + .45f) / dots);
                Vector2 p0 = origin + direction * (length * a), p1 = origin + direction * (length * b);
                line.SetPosition(i * 2, new Vector3(p0.x, p0.y, -.5f));
                line.SetPosition(i * 2 + 1, new Vector3(p1.x, p1.y, -.5f));
            }
            line.enabled = true;

            float pulse = 1f + .08f * Mathf.Sin(clock * 7f);
            Color c = new Color(color.r, color.g, color.b, .9f * strength);
            DbpuFx.Place(reticle, end, -.5f, .34f * pulse, .34f * pulse, 45f, c);
            DbpuFx.Place(ring, end, -.5f, .62f * pulse, .62f * pulse, 0f, new Color(color.r, color.g, color.b, .55f * strength));
        }

        public void Hide()
        {
            if (line) line.enabled = false;
            if (reticle) reticle.enabled = false;
            if (ring) ring.enabled = false;
        }

        public void Dispose() { if (root) UnityEngine.Object.Destroy(root); root = null; }
    }

    // Lets a charging technique be fired with a left click at the clicked point. The click is
    // swallowed (UI blocker until the button is released) so the game does not start dragging
    // or deselect anything with it. Alt+click is left to the game, as with Hakai targeting.
    public sealed class DbpuClickFire : MonoBehaviour
    {
        static DbpuClickFire instance;
        Global blocker; int releaseFrame = -1;
        public static bool InputCaptured { get { return instance && (instance.blocker || Time.frameCount <= instance.releaseFrame); } }

        static void Ensure()
        {
            if (instance) return;
            var go = new GameObject("DBPU technique click fire"); DontDestroyOnLoad(go);
            instance = go.AddComponent<DbpuClickFire>();
        }

        public static bool TryConsume(out Vector2 point)
        {
            point = Vector2.zero;
            Ensure();
            if (instance.blocker || !Input.GetMouseButtonDown(0) || !KiCombatMath.HasFocus) return false;
            if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) return false;
            if (EventSystem.current && EventSystem.current.IsPointerOverGameObject()) return false;
            if (CombatAbilityWheel.AnyMenuCaptured || DialogBox.IsAnyDialogboxOpen) return false;
            point = DbpuFx.MouseWorld();
            if (Global.main) { instance.blocker = Global.main; instance.blocker.AddUiBlocker(); }
            return true;
        }

        void Update()
        {
            if (!blocker) return;
            if (!Input.GetMouseButton(0) || !KiCombatMath.HasFocus || blocker != Global.main)
            {
                try { blocker.RemoveUiBlocker(); } catch (Exception) { }
                blocker = null; releaseFrame = Time.frameCount;
            }
        }
        void OnDisable() { if (blocker) { try { blocker.RemoveUiBlocker(); } catch (Exception) { } blocker = null; } }
    }
}
