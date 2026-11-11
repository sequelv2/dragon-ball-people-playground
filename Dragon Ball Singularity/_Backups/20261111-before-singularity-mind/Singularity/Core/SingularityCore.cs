using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DBPU
{
    // ---------------------------------------------------------------------------------
    // Singularity core
    //
    // Every new system is a SingularityModule: it boots in OnLoad, registers its spawn
    // menu entries in Main, receives each fighter once (PersonReady) and ticks while
    // enabled. A failing module is logged and skipped; it can never stop the mod loading.
    // Toggles live in Mods/DBPU_Singularity.cfg next to DBPU_Configs.json and appear in
    // the Mod Configurations menu under "Singularity Features".
    // ---------------------------------------------------------------------------------
    public abstract class SingularityModule
    {
        public abstract string Id { get; }
        public abstract string Title { get; }
        public virtual string Summary { get { return ""; } }
        public virtual bool DefaultOn { get { return true; } }
        public bool Enabled { get { return Singularity.IsEnabled(Id); } }
        public virtual void Boot() { }
        public virtual void RegisterSpawnables() { }
        public virtual void PersonReady(PersonBehaviour person) { }
        public virtual void Tick(float dt) { }
        public virtual void FixedTick(float dt) { }
        public virtual void OnDisabled() { }
    }

    public static class Singularity
    {
        public const string Version = "1.0 \"Singularity\"";
        static readonly List<SingularityModule> modules = new List<SingularityModule>();
        static readonly Dictionary<string, bool> flags = new Dictionary<string, bool>();
        static bool booted, spawnablesRegistered;
        public static string ModDirectory { get; private set; }
        public static IList<SingularityModule> Modules { get { return modules; } }

        // Raised once per fighter (any PersonBehaviour), after modules have seen it.
        public static event Action<PersonBehaviour> PersonReadyEvent;
        // Raised by SingularityLimbContact for every limb collision above a small threshold.
        public static event Action<SingularityImpact> LimbImpact;

        static void RegisterBuiltIns()
        {
            Add(new ShenronModule());
            Add(new ZenoEraseModule());
            Add(new TimeSkipModule());
            Add(new UltraInstinctModule());
            Add(new UltraEgoModule());
            Add(new ZenkaiModule());
            Add(new ImpactFramesModule());
            Add(new TournamentModule());
            Add(new BeamPhysicsModule());
            Add(new SpherePhysicsModule());
        }

        public static void Add(SingularityModule module)
        {
            if (module == null || Find(module.Id) != null) return;
            modules.Add(module);
        }

        public static SingularityModule Find(string id)
        {
            for (int i = 0; i < modules.Count; i++) if (modules[i].Id == id) return modules[i];
            return null;
        }

        public static T Get<T>() where T : SingularityModule
        {
            for (int i = 0; i < modules.Count; i++) { var m = modules[i] as T; if (m != null) return m; }
            return null;
        }

        // Called from DBPU.OnLoad (ModAPI.LoadSprite/LoadSound work here).
        public static void Boot(string modDirectory)
        {
            if (booted) return;
            booted = true;
            ModDirectory = modDirectory;
            Safe("register modules", RegisterBuiltIns);
            LoadFlags();
            for (int i = 0; i < modules.Count; i++)
            {
                var m = modules[i];
                Safe(m.Id + " boot", m.Boot);
            }
            SingularityRunner.Ensure();
            Debug.Log("[Singularity] " + Version + " booted with " + modules.Count + " modules.");
        }

        // Called from DBPU.Main (spawn menu registration time).
        public static void RegisterSpawnables()
        {
            if (spawnablesRegistered) return;
            spawnablesRegistered = true;
            for (int i = 0; i < modules.Count; i++)
            {
                var m = modules[i];
                Safe(m.Id + " spawnables", m.RegisterSpawnables);
            }
        }

        public static bool IsEnabled(string id)
        {
            bool value;
            if (flags.TryGetValue(id, out value)) return value;
            var m = Find(id);
            return m == null || m.DefaultOn;
        }

        public static void SetEnabled(string id, bool on)
        {
            var m = Find(id);
            flags[id] = on;
            SaveFlags();
            if (m == null) return;
            if (!on) Safe(id + " disable", m.OnDisabled);
            else SingularityRunner.Replay(m);
        }

        internal static void RaisePersonReady(PersonBehaviour person)
        {
            var handler = PersonReadyEvent;
            if (handler != null) Safe("PersonReady event", () => handler(person));
        }

        internal static void RaiseImpact(SingularityImpact impact)
        {
            var handler = LimbImpact;
            if (handler == null) return;
            foreach (Action<SingularityImpact> h in handler.GetInvocationList())
            {
                try { h(impact); }
                catch (Exception ex) { Debug.LogWarning("[Singularity] impact handler: " + ex.Message); }
            }
        }

        public static void Safe(string what, Action action)
        {
            try { action(); }
            catch (Exception ex) { Debug.LogWarning("[Singularity] " + what + " failed: " + ex); }
        }

        static string SettingsPath
        {
            get
            {
                try
                {
                    var parent = Directory.GetParent(ModDirectory);
                    return Path.Combine(parent != null ? parent.FullName : ModDirectory, "DBPU_Singularity.cfg");
                }
                catch { return "DBPU_Singularity.cfg"; }
            }
        }

        static void LoadFlags()
        {
            try
            {
                if (!File.Exists(SettingsPath)) return;
                foreach (var raw in File.ReadAllLines(SettingsPath))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string key = line.Substring(0, eq).Trim(), value = line.Substring(eq + 1).Trim();
                    flags[key] = value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase) || value.Equals("on", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch (Exception ex) { Debug.LogWarning("[Singularity] settings load: " + ex.Message); }
        }

        static void SaveFlags()
        {
            try
            {
                var lines = new List<string> { "# Dragon Ball Singularity feature toggles (1 = on, 0 = off)" };
                for (int i = 0; i < modules.Count; i++)
                    lines.Add(modules[i].Id + "=" + (IsEnabled(modules[i].Id) ? "1" : "0"));
                File.WriteAllLines(SettingsPath, lines.ToArray());
            }
            catch (Exception ex) { Debug.LogWarning("[Singularity] settings save: " + ex.Message); }
        }

        // Builds the "Singularity Features" page of the Mod Configurations menu.
        public static DbpuMenuButton[] MenuButtons(Action reopen, Action back)
        {
            var buttons = new List<DbpuMenuButton>();
            for (int i = 0; i < modules.Count; i++)
            {
                var m = modules[i];
                string state = m.Enabled ? "<color=#55FF55>ON" : "<color=#FF5555>OFF";
                buttons.Add(new DbpuMenuButton(m.Title + "  " + state, () => { SetEnabled(m.Id, !m.Enabled); reopen(); }));
            }
            buttons.Add(new DbpuMenuButton("What do these do?", () =>
            {
                for (int i = 0; i < modules.Count; i++) ModAPI.Notify("<color=#FFD25A>" + modules[i].Title + "</color>: " + modules[i].Summary);
            }));
            buttons.Add(new DbpuMenuButton("Back", back));
            return buttons.ToArray();
        }
    }

    // Collision between a limb and anything solid, measured once per contact.
    public sealed class SingularityImpact
    {
        public LimbBehaviour Limb;            // limb that reported the contact
        public PersonBehaviour Person;        // its owner
        public Collision2D Collision;
        public LimbBehaviour OtherLimb;       // set when the other collider is a limb
        public PersonBehaviour OtherPerson;
        public Rigidbody2D OtherBody;
        public Vector2 Point, RelativeVelocity;
        public float Speed, ReducedMass;
        public float Energy { get { return .5f * ReducedMass * Speed * Speed; } }
    }

    public sealed class SingularityLimbContact : MonoBehaviour
    {
        public LimbBehaviour Limb;
        Rigidbody2D body;
        static readonly ContactPoint2D[] contacts = new ContactPoint2D[4];

        public static void AttachAll(PersonBehaviour person)
        {
            if (!person || person.Limbs == null) return;
            foreach (var limb in person.Limbs)
            {
                if (!limb || limb.GetComponent<SingularityLimbContact>()) continue;
                limb.gameObject.AddComponent<SingularityLimbContact>().Limb = limb;
            }
        }

        void Awake()
        {
            if (!Limb) Limb = GetComponent<LimbBehaviour>();
            body = GetComponent<Rigidbody2D>();
        }

        void OnCollisionEnter2D(Collision2D collision)
        {
            if (!Limb || !Limb.Person || collision == null || collision.collider == null) return;
            Vector2 rel = collision.relativeVelocity;
            float speed = rel.magnitude;
            if (speed < 3f) return;
            var other = collision.collider.GetComponent<LimbBehaviour>();
            // A limb-limb contact arrives on both limbs: report it once, from the lower instance id.
            if (other && other.GetComponent<SingularityLimbContact>() && other.GetInstanceID() < Limb.GetInstanceID()) return;
            var otherBody = collision.rigidbody;
            float m1 = body ? body.mass : 1f, m2 = otherBody && otherBody.bodyType == RigidbodyType2D.Dynamic ? otherBody.mass : m1 * 20f;
            Vector2 point = transform.position;
            int n = collision.GetContacts(contacts);
            if (n > 0) point = contacts[0].point;
            var impact = new SingularityImpact
            {
                Limb = Limb, Person = Limb.Person, Collision = collision,
                OtherLimb = other, OtherPerson = other ? other.Person : null, OtherBody = otherBody,
                Point = point, RelativeVelocity = rel, Speed = speed,
                ReducedMass = m1 * m2 / Mathf.Max(.0001f, m1 + m2)
            };
            Singularity.RaiseImpact(impact);
        }
    }

    [DefaultExecutionOrder(-40)]
    public sealed class SingularityRunner : MonoBehaviour
    {
        static SingularityRunner instance;
        readonly HashSet<int> known = new HashSet<int>();
        readonly List<PersonBehaviour> persons = new List<PersonBehaviour>();
        float scan;

        public static IList<PersonBehaviour> Persons
        {
            get { return instance ? (IList<PersonBehaviour>)instance.persons : new PersonBehaviour[0]; }
        }

        public static void Ensure()
        {
            if (instance) return;
            var go = new GameObject("DBPU Singularity");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<SingularityRunner>();
        }

        // A module switched on later still gets every fighter already in the scene.
        public static void Replay(SingularityModule module)
        {
            if (!instance) return;
            instance.persons.RemoveAll(p => !p);
            foreach (var p in instance.persons)
            {
                var person = p;
                Singularity.Safe(module.Id + " replay", () => module.PersonReady(person));
            }
        }

        void Update()
        {
            scan -= Time.unscaledDeltaTime;
            if (scan <= 0f) { scan = .35f; Rescan(); }
            float dt = DbpuFx.Dt;
            var list = Singularity.Modules;
            for (int i = 0; i < list.Count; i++)
            {
                var m = list[i];
                if (!m.Enabled) continue;
                try { m.Tick(dt); }
                catch (Exception ex) { Debug.LogWarning("[Singularity] " + m.Id + " tick: " + ex.Message); }
            }
        }

        void FixedUpdate()
        {
            if (DbpuFx.Paused) return;
            var list = Singularity.Modules;
            for (int i = 0; i < list.Count; i++)
            {
                var m = list[i];
                if (!m.Enabled) continue;
                try { m.FixedTick(Time.fixedDeltaTime); }
                catch (Exception ex) { Debug.LogWarning("[Singularity] " + m.Id + " fixed tick: " + ex.Message); }
            }
        }

        void Rescan()
        {
            persons.RemoveAll(p => !p);
            PersonBehaviour[] found;
            try { found = FindObjectsOfType<PersonBehaviour>(); }
            catch { return; }
            foreach (var p in found)
            {
                if (!p || !known.Add(p.GetInstanceID())) continue;
                persons.Add(p);
                var person = p;
                SingularityLimbContact.AttachAll(person);
                var list = Singularity.Modules;
                for (int i = 0; i < list.Count; i++)
                {
                    var m = list[i];
                    if (!m.Enabled) continue;
                    Singularity.Safe(m.Id + " person", () => m.PersonReady(person));
                }
                Singularity.RaisePersonReady(person);
            }
        }
    }

    // Small shared helpers for fighters.
    public static class SingularityFighters
    {
        public static string CharacterId(PersonBehaviour person)
        {
            if (!person) return "";
            var id = person.GetComponent<DbpuCharacterIdentity>();
            if (id && !string.IsNullOrEmpty(id.CharacterId)) return id.CharacterId;
            return person.name ?? "";
        }

        public static string DisplayName(PersonBehaviour person)
        {
            if (!person) return "Fighter";
            var id = person.GetComponent<DbpuCharacterIdentity>();
            if (id && !string.IsNullOrEmpty(id.DisplayName)) return StripTags(id.DisplayName);
            string n = person.name ?? "Fighter";
            n = n.Replace("(Clone)", "").Trim();
            return n.Length > 0 ? StripTags(n) : "Fighter";
        }

        static string StripTags(string s)
        {
            return System.Text.RegularExpressions.Regex.Replace(s ?? "", "<[^>]+>", "").Trim();
        }

        static bool Has(string hay, string token) { return !string.IsNullOrEmpty(hay) && hay.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0; }

        // Angels survive the erasure of their universe.
        public static bool IsAngel(PersonBehaviour person)
        {
            string id = CharacterId(person) + " " + DisplayName(person);
            return Has(id, "Whis") || Has(id, "Vados") || Has(id, "Grand Priest") || Has(id, "Daishinkan") || Has(id, "Marcarita");
        }

        public static bool IsSaiyan(PersonBehaviour person)
        {
            return ZenkaiBoostBehaviour.IsSaiyan(CharacterId(person), DisplayName(person));
        }

        public static string Form(PersonBehaviour person)
        {
            try { return UltraInstinctEvasion.GetActiveForm(person) ?? "Base"; }
            catch { return "Base"; }
        }

        public static float HealthFraction(PersonBehaviour person)
        {
            if (!person || person.Limbs == null) return 0f;
            float now = 0f, max = 0f;
            foreach (var limb in person.Limbs)
            {
                if (!limb || limb.IsDismembered) continue;
                float h = limb.Health, m = limb.InitialHealth;
                if (float.IsNaN(h) || float.IsInfinity(h) || float.IsNaN(m) || float.IsInfinity(m) || m <= 0f) continue;
                now += Mathf.Clamp(h, 0f, m); max += m;
            }
            return max <= 0f ? 0f : Mathf.Clamp01(now / max);
        }

        public static KiSystem Ki(PersonBehaviour person) { return person ? person.GetComponent<KiSystem>() : null; }

        public static float KiFraction(PersonBehaviour person)
        {
            var ki = Ki(person);
            if (!ki) return 1f;
            float max = KiCombatMath.MaxKi(ki);
            return max <= 0f ? 1f : Mathf.Clamp01(ki.currentKi / max);
        }

        public static bool Spend(PersonBehaviour person, float fraction)
        {
            var ki = Ki(person);
            if (!ki) return true;
            return DbpuHandTechniques.Spend(ki, fraction);
        }

        public static IEnumerable<Rigidbody2D> Bodies(PersonBehaviour person)
        {
            if (!person || person.Limbs == null) yield break;
            foreach (var limb in person.Limbs)
            {
                if (!limb) continue;
                var body = limb.GetComponent<Rigidbody2D>();
                if (body) yield return body;
            }
        }

        // Moves a whole ragdoll at once (joints keep their shape); velocities are kept.
        public static void Translate(PersonBehaviour person, Vector2 offset)
        {
            if (!person || person.Limbs == null) return;
            foreach (var limb in person.Limbs)
            {
                if (!limb || limb.IsDismembered) continue;
                var body = limb.GetComponent<Rigidbody2D>();
                if (body) body.position += offset;
                limb.transform.position += (Vector3)offset;
            }
        }

        public static LimbBehaviour Torso(PersonBehaviour person)
        {
            if (!person || person.Limbs == null) return null;
            LimbBehaviour best = null;
            foreach (var limb in person.Limbs)
            {
                if (!limb || limb.IsDismembered) continue;
                if (limb.name == "UpperBody") return limb;
                if (!best && limb.name == "MiddleBody") best = limb;
            }
            return best ? best : (person.Limbs.Length > 0 ? person.Limbs[0] : null);
        }

        public static void Push(PersonBehaviour person, Vector2 velocityChange)
        {
            foreach (var body in Bodies(person))
                if (body.bodyType == RigidbodyType2D.Dynamic) body.AddForce(velocityChange * body.mass, ForceMode2D.Impulse);
        }

        public static float PowerRatio(PersonBehaviour a, PersonBehaviour b)
        {
            double pa = DbpuFx.PowerOf(a), pb = DbpuFx.PowerOf(b);
            if (pb <= 0d) return 10f;
            double r = pa / pb;
            if (double.IsNaN(r) || double.IsInfinity(r)) return 10f;
            return (float)Math.Max(.0001d, Math.Min(10000d, r));
        }
    }
}
