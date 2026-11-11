using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace DBPU
{
    // ---------------------------------------------------------------------------------
    // Singularity Mind: a fighter that plays itself with its own body.
    //
    // Nothing here teleports, injects damage or plays a canned animation. Punches and kicks
    // land through PunchPower/KickPower, ki techniques are fired through the limb's own Use
    // trigger, posture goes through the game's pose motors and movement is force on the
    // ragdoll. The files in this folder:
    //   AutonomousModule      module, per-fighter switch, shared settings
    //   SingularityMind       the brain: targets, fairness governor, escalation, tactics
    //   KinematicsRig         limb map, IK, AI pose, body thrust, restore ledger
    //   KineticStrikeEngine   the strike phase machine and its velocity servo
    //   ThreatSensor          tracks, EKF, time to impact
    //   AbilityBridge         native technique inventory and invocation
    //   Cognition             policy network, online learner, planner, memory, IPC
    //   AutonomousDebugOverlay telemetry
    // ---------------------------------------------------------------------------------
    public enum AiDifficulty { Easy = 0, Normal = 1, Hard = 2, Saiyan = 3 }

    // Duel: fights other ki fighters and anyone who attacks. Guardian: only those who attack.
    // Berserk: everyone alive.
    public enum AiStance { Duel = 0, Guardian = 1, Berserk = 2 }

    public static class AiTactic
    {
        public const int FlightIntercept = 0, MeleeCombo = 1, AfterimageFlank = 2, EvadeDash = 3, EvadeWeave = 4,
            GuardBlock = 5, Parry = 6, KiShield = 7, KiGuard = 8, KiBlastVolley = 9, ChargedBeam = 10, Barrage = 11,
            KiFist = 12, DisengageCharge = 13, Kaioken = 14, Transform = 15, Count = 16;

        public static readonly string[] Names =
        {
            "Flight intercept", "Melee combo", "Afterimage flank", "Evade dash", "Evade weave", "Guard block", "Parry",
            "Ki shield", "Ki guard", "Ki blast volley", "Charged beam", "Barrage", "Ki fist", "Disengage and charge",
            "Kaioken", "Transform"
        };

        public static bool IsDefensive(int tactic)
        {
            return tactic == EvadeDash || tactic == EvadeWeave || tactic == GuardBlock || tactic == Parry || tactic == KiShield || tactic == KiGuard;
        }
    }

    // Everything the fairness governor scales by difficulty, in one table. Learning never
    // writes here: the policy only weighs tactics, these numbers bound what any tactic may do.
    public struct AiDifficultyProfile
    {
        public float ReactionScale;      // multiplies the form-tier reaction delay
        public float TelegraphScale;     // multiplies the strike chamber time
        public float StrikeNoise;        // metres of aim error on a strike
        public float BlastNoise;         // degrees of aim error on a ki shot
        public float MaxStrikeSpeed;     // cap on the commanded limb speed, m/s
        public int ComboHits;            // strikes before a mandatory breather
        public float Breather;           // seconds of defence only after a combo
        public float DodgeChance;        // share of read attacks the AI is allowed to answer
        public float ThinkInterval;      // seconds between tactic decisions
        public float LearnRate;          // multiplies the learner's step
        public float Temperature;        // softmax temperature: higher is more varied and less optimal
        public float Mercy;              // aggression multiplier against a badly hurt opponent
        public float StaminaRegen;       // per second

        public static AiDifficultyProfile For(AiDifficulty d)
        {
            switch (d)
            {
                case AiDifficulty.Easy:
                    return new AiDifficultyProfile { ReactionScale = 1.7f, TelegraphScale = 2.6f, StrikeNoise = .2f, BlastNoise = 9f, MaxStrikeSpeed = 16f, ComboHits = 2, Breather = 1.7f, DodgeChance = .3f, ThinkInterval = .32f, LearnRate = .6f, Temperature = 1.5f, Mercy = .45f, StaminaRegen = .085f };
                case AiDifficulty.Hard:
                    return new AiDifficultyProfile { ReactionScale = 1f, TelegraphScale = 1.15f, StrikeNoise = .05f, BlastNoise = 2.5f, MaxStrikeSpeed = 25f, ComboHits = 4, Breather = .7f, DodgeChance = .8f, ThinkInterval = .18f, LearnRate = 1.25f, Temperature = .75f, Mercy = .9f, StaminaRegen = .12f };
                case AiDifficulty.Saiyan:
                    return new AiDifficultyProfile { ReactionScale = .8f, TelegraphScale = 1f, StrikeNoise = .02f, BlastNoise = 1f, MaxStrikeSpeed = 28f, ComboHits = 6, Breather = .4f, DodgeChance = .93f, ThinkInterval = .14f, LearnRate = 1.6f, Temperature = .55f, Mercy = 1f, StaminaRegen = .14f };
                default:
                    return new AiDifficultyProfile { ReactionScale = 1.25f, TelegraphScale = 1.6f, StrikeNoise = .1f, BlastNoise = 5f, MaxStrikeSpeed = 21f, ComboHits = 3, Breather = 1.1f, DodgeChance = .6f, ThinkInterval = .22f, LearnRate = 1f, Temperature = 1f, Mercy = .7f, StaminaRegen = .1f };
            }
        }
    }

    // Settings in Mods/DBPU_AutonomousAI.cfg, next to the other DBPU settings files.
    public static class AutonomousConfig
    {
        public static AiDifficulty Difficulty = AiDifficulty.Normal;
        public static AiStance Stance = AiStance.Duel;
        public static bool PersistMemory = true;      // learned weights survive between sessions
        public static bool LearnFromPlayer = true;    // fighters the player drives are watched as demonstrations
        public static bool Cinematics = true;         // callouts and hitstop on clean heavy hits
        public static bool Overlay;                   // telemetry overlay
        public static bool Ipc;                       // memory-mapped bridge for an external trainer
        static bool loaded;

        public static string Folder
        {
            get
            {
                try
                {
                    string mod = Singularity.ModDirectory;
                    if (string.IsNullOrEmpty(mod)) return "";
                    var parent = Directory.GetParent(mod);
                    return parent != null ? parent.FullName : mod;
                }
                catch { return ""; }
            }
        }

        static string FilePath { get { return Path.Combine(Folder, "DBPU_AutonomousAI.cfg"); } }

        // QA runs and the read-only install never write settings.
        public static bool CanWrite { get { return !CharacterPowerCatalog.SavingDisabled && !string.IsNullOrEmpty(Folder); } }

        public static void Load()
        {
            if (loaded) return;
            loaded = true;
            try
            {
                if (!File.Exists(FilePath)) return;
                foreach (var raw in File.ReadAllLines(FilePath))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line[0] == '#') continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string key = line.Substring(0, eq).Trim().ToLowerInvariant(), value = line.Substring(eq + 1).Trim();
                    bool on = value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase) || value.Equals("on", StringComparison.OrdinalIgnoreCase);
                    int number;
                    switch (key)
                    {
                        case "difficulty": if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out number)) Difficulty = (AiDifficulty)Mathf.Clamp(number, 0, 3); break;
                        case "stance": if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out number)) Stance = (AiStance)Mathf.Clamp(number, 0, 2); break;
                        case "persist_memory": PersistMemory = on; break;
                        case "learn_from_player": LearnFromPlayer = on; break;
                        case "cinematics": Cinematics = on; break;
                        case "overlay": Overlay = on; break;
                        case "ipc": Ipc = on; break;
                    }
                }
            }
            catch (Exception ex) { Debug.LogWarning("[Singularity] AI settings load: " + ex.Message); }
        }

        public static void Save()
        {
            if (!CanWrite) return;
            try
            {
                File.WriteAllLines(FilePath, new[]
                {
                    "# Singularity Mind settings",
                    "# difficulty: 0 Easy, 1 Normal, 2 Hard, 3 Saiyan   stance: 0 Duel, 1 Guardian, 2 Berserk",
                    "difficulty=" + (int)Difficulty,
                    "stance=" + (int)Stance,
                    "persist_memory=" + (PersistMemory ? "1" : "0"),
                    "learn_from_player=" + (LearnFromPlayer ? "1" : "0"),
                    "cinematics=" + (Cinematics ? "1" : "0"),
                    "overlay=" + (Overlay ? "1" : "0"),
                    "ipc=" + (Ipc ? "1" : "0")
                });
            }
            catch (Exception ex) { Debug.LogWarning("[Singularity] AI settings save: " + ex.Message); }
        }
    }

    public sealed class AutonomousMindModule : SingularityModule
    {
        public const string ModuleId = "autonomous_ai";
        public override string Id { get { return ModuleId; } }
        public override string Title { get { return "Singularity Mind (Autonomous AI)"; } }
        public override bool DefaultOn { get { return false; } }
        public override string Summary { get { return "Off by default. Adds \"Autonomous AI\" to a fighter's head menu: the fighter then fights with its own body, with real punches and kicks, flight, guards, ki blasts, beams and transformations. It never teleports. Difficulty, stance, coaching and memory are on the same menu."; } }
        public static bool On { get { return Singularity.IsEnabled(ModuleId); } }

        public static readonly List<TournamentArena> Arenas = new List<TournamentArena>();
        static bool hooked;

        public override void Boot()
        {
            AutonomousConfig.Load();
            AiMemoryStore.Load();
            if (!hooked)
            {
                hooked = true;
                // Seen even while the module is off, so a pose index saved by a copy of an AI fighter is healed.
                Singularity.PersonReadyEvent += HealStalePose;
                ModAPI.OnItemSpawned += OnItemSpawned;
            }
            AutonomousAiQa.StartIfRequested();
        }

        // A fighter copied or saved while its AI pose was active carries that pose index, which
        // does not exist on the new body. The game would wrap it onto pose 0 and hold it there.
        static void HealStalePose(PersonBehaviour person)
        {
            if (!person || person.Poses == null) return;
            if (person.OverridePoseIndex >= person.Poses.Count && !person.GetComponent<SingularityMind>()) person.OverridePoseIndex = -1;
        }

        static void OnItemSpawned(object sender, UserSpawnEventArgs args)
        {
            try
            {
                if (args == null || !args.Instance) return;
                var arena = args.Instance.GetComponent<TournamentArena>();
                if (arena && !Arenas.Contains(arena)) Arenas.Add(arena);
            }
            catch (Exception ex) { Debug.LogWarning("[Singularity] AI arena watch: " + ex.Message); }
        }

        // The ring of a running tournament, if this point is inside its fighting space.
        public static bool RingFor(Vector2 at, out Bounds ring)
        {
            ring = default(Bounds);
            for (int i = Arenas.Count - 1; i >= 0; i--)
            {
                var arena = Arenas[i];
                if (!arena) { Arenas.RemoveAt(i); continue; }
                if (!arena.Running) continue;
                var sr = arena.GetComponent<SpriteRenderer>();
                Bounds b = sr ? sr.bounds : new Bounds(arena.transform.position, new Vector3(18f, 1f, 1f));
                if (at.x < b.min.x - 3f || at.x > b.max.x + 3f || at.y < b.max.y - 3f || at.y > b.max.y + 12f) continue;
                ring = b;
                return true;
            }
            return false;
        }

        public override void PersonReady(PersonBehaviour person)
        {
            if (!person || person.Limbs == null) return;
            var head = HeadOf(person);
            var phys = head ? head.PhysicalBehaviour : null;
            if (!phys || phys.ContextMenuOptions == null) return;
            var buttons = phys.ContextMenuOptions;
            // Rebuilt bodies (paste, load) get fresh buttons; an existing body never gets a second set.
            if (MCP_Utils.CheckContextButton(buttons, "SingularityMindToggle")) return;

            Func<bool> aiOn = () => { var m = person ? person.GetComponent<SingularityMind>() : null; return m && m.AiOn; };
            buttons.Buttons.Add(new ContextMenuButton("SingularityMindToggle",
                (Func<string>)(() => aiOn() ? "Autonomous AI: turn off" : "Autonomous AI: turn on"),
                "The fighter fights on its own, with its real body and techniques. It never teleports.",
                () => Toggle(person)));
            buttons.Buttons.Add(new ContextMenuButton(aiOn, "SingularityMindDifficulty",
                (Func<string>)(() => "AI difficulty: " + person.GetComponent<SingularityMind>().Difficulty + " (change)"),
                "Easy, Normal, Hard or Saiyan: reaction time, accuracy, combo length and how fast it learns",
                () => { var m = person.GetComponent<SingularityMind>(); if (m) m.CycleDifficulty(); }));
            buttons.Buttons.Add(new ContextMenuButton(aiOn, "SingularityMindStance",
                (Func<string>)(() => "AI stance: " + person.GetComponent<SingularityMind>().Stance + " (change)"),
                "Duel fights ki fighters and attackers, Guardian only attackers, Berserk everyone",
                () => { var m = person.GetComponent<SingularityMind>(); if (m) m.CycleStance(); }));
            buttons.Buttons.Add(new ContextMenuButton(aiOn, "SingularityMindCoachGood", "AI coach: good move",
                "Reward what the fighter did in the last two seconds",
                () => { var m = person.GetComponent<SingularityMind>(); if (m) m.Coach(1f); }));
            buttons.Buttons.Add(new ContextMenuButton(aiOn, "SingularityMindCoachBad", "AI coach: bad move",
                "Discourage what the fighter did in the last two seconds",
                () => { var m = person.GetComponent<SingularityMind>(); if (m) m.Coach(-1f); }));
            buttons.Buttons.Add(new ContextMenuButton(aiOn, "SingularityMindMemory", "AI memory: show",
                "What this fighter has learned against its current opponent",
                () => { var m = person.GetComponent<SingularityMind>(); if (m) ModAPI.Notify(m.DescribeMemory()); }));
            buttons.Buttons.Add(new ContextMenuButton(aiOn, "SingularityMindMemoryReset", "AI memory: reset",
                "Forget everything learned against the current opponent",
                () => { var m = person.GetComponent<SingularityMind>(); if (m) m.ResetMemory(); }));
            buttons.Buttons.Add(new ContextMenuButton(aiOn, "SingularityMindOverlay",
                (Func<string>)(() => AutonomousConfig.Overlay ? "AI telemetry: hide" : "AI telemetry: show"),
                "Show or hide the telemetry overlay",
                () => { AutonomousConfig.Overlay = !AutonomousConfig.Overlay; AutonomousConfig.Save(); }));
        }

        public static LimbBehaviour HeadOf(PersonBehaviour person)
        {
            if (!person || person.Limbs == null) return null;
            LimbBehaviour brain = null;
            for (int i = 0; i < person.Limbs.Length; i++)
            {
                var limb = person.Limbs[i];
                if (!limb) continue;
                if (limb.name == "Head") return limb;
                if (!brain && limb.HasBrain) brain = limb;
            }
            return brain;
        }

        public static void Toggle(PersonBehaviour person)
        {
            if (!person) return;
            if (!On) { ModAPI.Notify("Singularity Mind is switched off (Mod Configurations > Singularity Features)."); return; }
            var mind = SingularityMind.For(person, true);
            if (mind) mind.SetAi(!mind.AiOn);
        }

        public override void Tick(float dt)
        {
            AiIpc.Tick();
            AutonomousDebugOverlay.Tick();
        }

        public override void OnDisabled()
        {
            SingularityMind.DisableAll();
            AiPlannerService.Stop();
            AiIpc.Close();
            AiMemoryStore.Save();
        }
    }
}
