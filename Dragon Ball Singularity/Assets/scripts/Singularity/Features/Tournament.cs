using System;
using System.Collections.Generic;
using UnityEngine;

namespace DBPU
{
    // Tournament of Power: a floating stone ring. Put fighters on it and activate it:
    // countdown, then anyone who falls off is eliminated and teleported to the spectator
    // stand. Last fighter standing wins. Pairs naturally with the Zen-O Button.
    public sealed class TournamentModule : SingularityModule
    {
        public override string Id { get { return "tournament"; } }
        public override string Title { get { return "Tournament of Power"; } }
        public override string Summary { get { return "Spawnable arena. Activate it with fighters on the ring: ring-outs are eliminated to the stands, last fighter standing wins."; } }
        public static Sprite ArenaSprite, StandSprite;
        public const string SfxGong = "Assets/SFX/Singularity/Tournament/Gong.wav";
        public const string SfxVictory = "Assets/SFX/Singularity/Tournament/Victory.wav";

        public override void Boot()
        {
            try { ArenaSprite = ModAPI.LoadSprite("Assets/Sprites/Singularity/Tournament/Arena.png"); } catch (Exception ex) { Debug.LogWarning("[Singularity] arena sprite: " + ex.Message); }
            try { StandSprite = ModAPI.LoadSprite("Assets/Sprites/Singularity/Tournament/Stand.png"); } catch (Exception ex) { Debug.LogWarning("[Singularity] stand sprite: " + ex.Message); }
            DbpuFx.PreloadExtra(SfxGong, SfxVictory);
        }

        public override void RegisterSpawnables()
        {
            DBPU.ModAPIPlus.CreateObject(
                "Brick",
                "Tournament of Power Arena",
                "A floating stone ring. Put fighters on it and activate it: ring-outs are eliminated, the last fighter standing wins.",
                "Tournament Arena",
                (Instance) =>
                {
                    var sr = Instance.GetComponent<SpriteRenderer>();
                    if (sr && ArenaSprite) sr.sprite = ArenaSprite;
                    Instance.gameObject.FixColliders();
                    var body = Instance.GetComponent<Rigidbody2D>();
                    if (body) { body.velocity = Vector2.zero; body.angularVelocity = 0f; body.bodyType = RigidbodyType2D.Static; }
                    Instance.transform.rotation = Quaternion.identity;
                    var arena = Instance.AddComponent<TournamentArena>();
                    Instance.AddComponent<UseEventTrigger>().Action = arena.StartOrRestart;
                    var phys = Instance.GetComponent<PhysicalBehaviour>();
                    if (phys)
                    {
                        phys.ContextMenuOptions.Buttons.Add(new ContextMenuButton("TopStart", (Func<string>)(() => arena.Running ? "Restart tournament" : "Start tournament"), "Start the Tournament of Power", () => arena.StartOrRestart()));
                        phys.ContextMenuOptions.Buttons.Add(new ContextMenuButton("TopStop", (Func<string>)(() => "Stop tournament"), "Stop the current tournament", () => arena.Stop()));
                    }
                },
                "Items",
                DBPU.UniqueTag + DBPU.ItemsTag + "007");
        }
    }

    public sealed class TournamentArena : MonoBehaviour
    {
        enum State { Idle, Countdown, Running, Finished }
        static readonly Color Gold = new Color(1f, .85f, .35f, 1f), Red = new Color(1f, .45f, .4f, 1f);
        readonly List<PersonBehaviour> fighters = new List<PersonBehaviour>();
        readonly Dictionary<PersonBehaviour, float> downSince = new Dictionary<PersonBehaviour, float>();
        State state; float timer, check; int countdown; GameObject stand; int benched;
        public bool Running { get { return state == State.Countdown || state == State.Running; } }

        Bounds Ring
        {
            get
            {
                var sr = GetComponent<SpriteRenderer>();
                return sr ? sr.bounds : new Bounds(transform.position, new Vector3(18f, 1f, 1f));
            }
        }

        void Start() { BuildStand(); }

        void BuildStand()
        {
            if (stand) return;
            var ring = Ring;
            stand = new GameObject("DBPU Tournament spectator stand");
            stand.transform.position = new Vector3(ring.min.x - 7.5f, ring.max.y + 3f, 0f);
            var sr = stand.AddComponent<SpriteRenderer>();
            sr.sprite = TournamentModule.StandSprite;
            var mine = GetComponent<SpriteRenderer>();
            if (mine) { sr.sortingLayerID = mine.sortingLayerID; sr.sortingOrder = mine.sortingOrder; }
            var box = stand.AddComponent<BoxCollider2D>();
            if (sr.sprite) box.size = sr.sprite.bounds.size;
            else box.size = new Vector2(5.5f, .5f);
        }

        void OnDestroy() { if (stand) Destroy(stand); }

        public void StartOrRestart()
        {
            var module = Singularity.Get<TournamentModule>();
            if (module == null || !module.Enabled) { ModAPI.Notify("The Tournament of Power is switched off (Singularity Features)."); return; }
            BuildStand();
            fighters.Clear(); downSince.Clear(); benched = 0;
            var ring = Ring;
            foreach (var p in SingularityRunner.Persons)
            {
                if (!p || !p.IsAlive()) continue;
                Vector2 c = DbpuFx.Centre(p);
                if (c.x < ring.min.x - .5f || c.x > ring.max.x + .5f || c.y < ring.max.y - .6f || c.y > ring.max.y + 7f) continue;
                fighters.Add(p);
            }
            if (fighters.Count < 2)
            {
                SingularityAnnouncer.Show("TOURNAMENT OF POWER", "PUT AT LEAST TWO FIGHTERS ON THE RING", 2.6f, Gold);
                state = State.Idle;
                return;
            }
            state = State.Countdown; countdown = 3; timer = 0f;
            SingularityAnnouncer.Show("TOURNAMENT OF POWER", fighters.Count + " FIGHTERS  ·  RING-OUT ELIMINATES", 1.6f, Gold);
        }

        public void Stop()
        {
            if (state == State.Idle) return;
            state = State.Idle;
            SingularityAnnouncer.Show("TOURNAMENT STOPPED", "", 1.5f, Gold);
        }

        void Update()
        {
            if (DbpuFx.Paused) return;
            float dt = Time.deltaTime;
            switch (state)
            {
                case State.Countdown:
                    timer -= dt;
                    if (timer > 0f) break;
                    timer = .9f;
                    if (countdown > 0) { SingularityAnnouncer.Show(countdown.ToString(), "", .85f, Gold); countdown--; }
                    else
                    {
                        state = State.Running;
                        SingularityAnnouncer.Show("BEGIN!", "LAST FIGHTER STANDING WINS", 1.6f, Gold);
                        DbpuFx.Sound(transform.position, TournamentModule.SfxGong, 1f);
                        DbpuFx.Shake(transform.position, 1.5f);
                    }
                    break;
                case State.Running:
                    check -= dt;
                    if (check > 0f) break;
                    check = .15f;
                    Judge();
                    break;
            }
        }

        void Judge()
        {
            var ring = Ring;
            for (int i = fighters.Count - 1; i >= 0; i--)
            {
                var p = fighters[i];
                if (!p) { fighters.RemoveAt(i); Announce("ERASED", "A FIGHTER VANISHED"); continue; }
                Vector2 c = DbpuFx.Centre(p);
                bool below = c.y < ring.max.y - 2.5f;
                bool outside = (c.x < ring.min.x - 1.5f || c.x > ring.max.x + 1.5f) && c.y < ring.max.y + .2f;
                if (below || outside) { Eliminate(i, "RING OUT"); continue; }
                if (!p.IsAlive())
                {
                    float since;
                    if (!downSince.TryGetValue(p, out since)) downSince[p] = Time.time;
                    else if (Time.time - since > 4f) Eliminate(i, "KNOCKED OUT");
                }
                else downSince.Remove(p);
            }
            if (fighters.Count <= 1) Finish();
        }

        void Eliminate(int index, string reason)
        {
            var p = fighters[index];
            fighters.RemoveAt(index);
            downSince.Remove(p);
            string name = SingularityFighters.DisplayName(p);
            Announce("ELIMINATED!", name.ToUpperInvariant() + "  ·  " + reason);
            if (!p || !stand) return;
            Vector2 c = DbpuFx.Centre(p);
            SingularityFx.Flash(c, new Color(.8f, .95f, 1f, .9f), 1.5f, .35f);
            var sr = stand.GetComponent<SpriteRenderer>();
            float width = sr && sr.sprite ? sr.sprite.bounds.size.x : 5f;
            float x = stand.transform.position.x - width * .4f + (benched % 6) * (width * .16f);
            Vector2 target = new Vector2(x, stand.transform.position.y + 1.4f);
            benched++;
            foreach (var body in SingularityFighters.Bodies(p)) { body.velocity = Vector2.zero; body.angularVelocity = 0f; }
            SingularityFighters.Translate(p, target - c);
            SingularityFx.Flash(target, new Color(.8f, .95f, 1f, .9f), 1.5f, .35f);
        }

        void Finish()
        {
            state = State.Finished;
            if (fighters.Count == 1 && fighters[0])
            {
                var winner = fighters[0];
                Vector2 c = DbpuFx.Centre(winner);
                SingularityAnnouncer.Show("WINNER: " + SingularityFighters.DisplayName(winner).ToUpperInvariant(), "SURVIVOR OF THE TOURNAMENT OF POWER", 4f, Gold);
                SingularityFx.Burst(c, Gold, 2f, 40);
                SingularityFx.Ring(c, Gold, 4f, .8f, 2f);
            }
            else SingularityAnnouncer.Show("NO SURVIVORS", "THE OMNI-KING IS NOT AMUSED", 3f, Red);
            DbpuFx.Sound(transform.position, TournamentModule.SfxVictory, 1f);
        }

        void Announce(string title, string subtitle)
        {
            SingularityAnnouncer.Show(title, subtitle, 2f, Red);
            DbpuFx.Sound(transform.position, TournamentModule.SfxGong, .8f);
        }
    }
}
