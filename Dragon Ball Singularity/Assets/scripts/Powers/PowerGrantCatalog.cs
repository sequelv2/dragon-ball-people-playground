using Mod;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;
using static FunLittleGames.Linkage.Linkage;
using Random = UnityEngine.Random;
using HarmonyLib;

namespace DBPU
{
    public static class PowerGrantCatalog
    {
        public class Entry
        {
            public string Id;
            public string Name;
            public string Description;
            public string Category;
            public Action<PersonBehaviour, GameObject, CharacterPowers> Grant;
        }

        static List<Entry> _entries;

        public static List<Entry> Entries
        {
            get
            {
                if (_entries == null) Build();
                return _entries;
            }
        }

        static void Build()
        {
            _entries = new List<Entry>
            {
                EntryKi("KiBlast", "Ki Blast", "Ki Blast and Ki Blast Barrage powers.", (p, go, cp) => PowerSetup.GrantKiBlast(cp, go)),
                EntryBeam("EnergyWave", "Energy Wave", "Basic ki beam.", "Energy Wave"),
                EntryBeam("Kamehameha", "Kamehameha", "Signature ki wave.", "Kamehameha", DBPU.Icon_Kamehameha),
                EntryBeam("RoseKamehameha", "Rose Kamehameha", "Rose-tinted kamehameha beam.", "Rose Kamehameha", DBPU.Icon_RoseKamehameha),
                EntryBeam("GalickGun", "Galick Gun", "Purple ki beam.", "Galick Gun", DBPU.Icon_GalickGun),
                EntryBeam("FinalFlash", "Final Flash", "High power beam.", "Final Flash", DBPU.Icon_FinalFlash),
                EntryBeam("SpecialBeamCannon", "Special Beam Cannon", "Piercing spiral beam.", "Special Beam Cannon", null),
                EntryBeam("FullForceKamehameha", "Full Force Kamehameha", "Gogeta full power wave.", "Full Force Kamehameha", DBPU.Icon_FullForceKamehameha),
                EntryBeam("GiganticFullBlast", "Gigantic Full Blast", "Broly arm beam.", "Gigantic Full Blast", DBPU.Icon_GiganticFullBlast, "ArmBeam"),
                EntryBeam("GiganticRoar", "Gigantic Roar", "Broly mouth beam.", "Gigantic Roar", DBPU.Icon_GiganticRoar, "MouthBeam"),
                EntryBeam("MaxCannon", "Max Cannon", "Cell Max mouth beam.", "Max Cannon", DBPU.Icon_MaxCannon, "MouthBeam"),
                new Entry { Id = "SolarFlare", Name = "Solar Flare", Description = "Blinds nearby targets.", Category = "Special", Grant = (p, go, cp) => PowerSetup.GrantSolarFlare(cp, go) },
                new Entry { Id = "WrathOfTheGod", Name = "Wrath of the G.O.D.", Description = "Energy wave beam attack.", Category = "Beerus", Grant = (p, go, cp) => PowerSetup.GrantBeerusPower(cp, go, "WrathOfTheGod") },
                new Entry { Id = "BeerusBall", Name = "Beerus Ball", Description = "A stronger destruction sphere than Supernova.", Category = "Beerus", Grant = (p, go, cp) => PowerSetup.GrantBeerusPower(cp, go, "BeerusBall") },
                new Entry { Id = "GodsWrath", Name = "G.O.D's Wrath", Description = "Omni-directional ki blast barrage from the middle body.", Category = "Beerus", Grant = (p, go, cp) => PowerSetup.GrantBeerusPower(cp, go, "GodsWrath") },
                new Entry { Id = "CataclysmicOrb", Name = "Cataclysmic Orb", Description = "Tiny atom-like sphere with a devastating explosion.", Category = "Beerus", Grant = (p, go, cp) => PowerSetup.GrantBeerusPower(cp, go, "CataclysmicOrb") },
                new Entry { Id = "Kienzan", Name = "Kienzan", Description = DbpuTechniquePowers.Description(KiHandLoadout.KienzanId), Category = "Krillin", Grant = (p, go, cp) => DbpuTechniquePowers.Grant(p, cp, KiHandLoadout.KienzanId) },
                new Entry { Id = "Hakai", Name = "Hakai", Description = DbpuTechniquePowers.Description(KiHandLoadout.HakaiId), Category = "Beerus", Grant = (p, go, cp) => DbpuTechniquePowers.Grant(p, cp, KiHandLoadout.HakaiId) },
                new Entry { Id = "TimeSkip", Name = "Time Skip", Description = "Hit's technique: stop time for about a second; every blow landed meanwhile strikes at once. J with the fighter selected.", Category = "Hit", Grant = (p, go, cp) => TimeSkipModule.Grant(p, cp, TimeSkipModule.SkipId) },
                new Entry { Id = "TimePrison", Name = "Time Prison", Description = "Hit's technique: click a fighter to seal them in stopped time for six seconds. Shift+J with the fighter selected.", Category = "Hit", Grant = (p, go, cp) => TimeSkipModule.Grant(p, cp, TimeSkipModule.PrisonId) },
                new Entry { Id = "HakaiBall", Name = "Hakai Ball", Description = DbpuTechniquePowers.Description(KiHandLoadout.HakaiBallId), Category = "Beerus", Grant = (p, go, cp) => DbpuTechniquePowers.Grant(p, cp, KiHandLoadout.HakaiBallId) },
                new Entry { Id = "KiShield", Name = "Ki Shield", Description = "Mobile ki barrier. Activate on the upper torso. Incoming attacks drain ki and integrity; stronger attacks can break through.", Category = "Defense", Grant = (p, go, cp) => PowerSetup.GrantKiShield(cp, go) },
                new Entry { Id = "FinalExplosion", Name = "Final Explosion", Description = "Self-destruct attack.", Category = "Special", Grant = (p, go, cp) => PowerSetup.GrantFinalExplosion(cp, go) },
                new Entry { Id = "HellzoneGranade", Name = "Hellzone Grenade", Description = "Trap ki blasts in an area.", Category = "Special", Grant = (p, go, cp) => PowerSetup.GrantHellzone(cp, go) },
                new Entry { Id = "GiganticCrusher", Name = "Gigantic Crusher", Description = "Large ki sphere.", Category = "Ball", Grant = (p, go, cp) => PowerSetup.GrantBallPower(cp, go, "GiganticCrusher", "Gigantic Crusher", "Gigantic Crusher") },
                new Entry { Id = "SpiritBomb", Name = "Spirit Bomb", Description = "Grow a Spirit Bomb with charge time and shared Ki. Draw enemies inside for eight pulses and a final explosion.", Category = "Ball", Grant = (p, go, cp) => PowerSetup.GrantBallPower(cp, go, "SpiritBomb", "Spirit Bomb", "Spirit Bomb") },
                new Entry { Id = "SuperSpiritBomb", Name = "Super Spirit Bomb", Description = "Spirit Bomb compatibility variant: the same continuous growth from charge time and shared Ki, with containment pulses and a final explosion.", Category = "Ball", Grant = (p, go, cp) => PowerSetup.GrantBallPower(cp, go, "SuperSpiritBomb", "Super Spirit Bomb", "Super Spirit Bomb") },
                EntryBeam("LastEmperor", "Last Emperor", "Frieza ultimate beam.", "Last Emperor", DBPU.Icon_LastEmperor),
                new Entry { Id = "DeathBeam", Name = "Death Beam", Description = "Finger beam attack.", Category = "Frieza", Grant = (p, go, cp) => PowerSetup.GrantStraightBeam(cp, go, "DeathBeam", "Death Beam", "Death Beam") },
                new Entry { Id = "DeathBall", Name = "Death Ball", Description = "Large destructive ki sphere.", Category = "Frieza", Grant = (p, go, cp) => PowerSetup.GrantBallPower(cp, go, "DeathBall", "Death Ball", "Death Ball") },
                new Entry { Id = "Supernova", Name = "Supernova", Description = "Solar destruction sphere.", Category = "Frieza", Grant = (p, go, cp) => PowerSetup.GrantBallPower(cp, go, "Supernova", "Supernova", "Supernova") },
                new Entry { Id = "StardustFall", Name = "Stardust Fall", Description = "Gogeta special technique.", Category = "Gogeta", Grant = (p, go, cp) => PowerSetup.GrantGogetaSpecial(cp, go, "StardustFall", "Stardust Fall", "StardustFall") },
                new Entry { Id = "MeteorExplosion", Name = "Meteor Explosion", Description = "Gogeta special technique.", Category = "Gogeta", Grant = (p, go, cp) => PowerSetup.GrantGogetaSpecial(cp, go, "MeteorExplosion", "Meteor Explosion", "MeteorExplosion") },
                new Entry { Id = "GodPunisher", Name = "God Punisher", Description = "Gogeta special technique.", Category = "Gogeta", Grant = (p, go, cp) => PowerSetup.GrantGogetaSpecial(cp, go, "GodPunisher", "God Punisher", "GodPunisher") },
                new Entry { Id = "StardustBreaker", Name = "Stardust Breaker", Description = "Gogeta special technique.", Category = "Gogeta", Grant = (p, go, cp) => PowerSetup.GrantGogetaSpecial(cp, go, "StardustBreaker", "Stardust Breaker", "StardustBreaker") },
                new Entry { Id = "PunchPower", Name = "Punch Power", Description = "Melee enhancement on arms.", Category = "Physical", Grant = (p, go, cp) => PowerSetup.GrantPunchPower(p, cp) },
                new Entry { Id = "KickPower", Name = "Kick Power", Description = "Melee enhancement on feet.", Category = "Physical", Grant = (p, go, cp) => PowerSetup.GrantKickPower(p, cp) },
                new Entry { Id = "Barrage", Name = "Barrage", Description = "Rapid projectile barrage (hold H).", Category = "Physical", Grant = (p, go, cp) => PowerSetup.GrantBarrage(p, cp) },
                new Entry { Id = "Flight", Name = "Flight", Description = "Flight on lower body.", Category = "Physical", Grant = (p, go, cp) => PowerSetup.GrantFlight(p, cp) },
                new Entry { Id = "GreatNamekianGrowth", Name = "Great Namekian Growth", Description = "Piccolo giant growth toggle.", Category = "Physical", Grant = (p, go, cp) => PowerSetup.GrantPiccoloGrowth(p, cp) },
                new Entry { Id = "InstantTransmission", Name = "Instant Transmission", Description = "Teleport with R on head.", Category = "Movement", Grant = (p, go, cp) => PowerSetup.GrantInstantTransmission(p, cp) },
                new Entry { Id = "Kaioken", Name = "Kaioken", Description = "Power boost with strain buildup. Toggle from powers menu or press K.", Category = "Special", Grant = (p, go, cp) => PowerSetup.GrantKaioken(p, cp) },
                new Entry { Id = "DisasterRay", Name = "Disaster Ray", Description = "Purple lasers from body spots. Activate the head while enabled.", Category = "Special", Grant = (p, go, cp) => PowerSetup.GrantDisasterRay(p, go, cp) },
                new Entry { Id = "EyeLaser", Name = "Eye Laser", Description = "Yellow eye laser from the head. Activate the head while enabled.", Category = "Special", Grant = (p, go, cp) => PowerSetup.GrantPowerMenuEyeLaser(p, go, cp) }
            };
        }

        static Entry EntryKi(string id, string name, string desc, Action<PersonBehaviour, GameObject, CharacterPowers> grant) =>
            new Entry { Id = id, Name = name, Description = desc, Category = "Ki", Grant = grant };

        static Entry EntryBeam(string id, string name, string desc, string effectId, Sprite icon = null, string beamType = null) =>
            new Entry { Id = id, Name = name, Description = desc, Category = "Beam", Grant = (p, go, cp) => PowerSetup.GrantLegacyBeam(cp, go, id, name, effectId, icon ?? DBPU.PIconTemplate, beamType) };

        public static Entry Find(string id)
        {
            foreach (var e in Entries)
                if (e.Id == id) return e;
            return null;
        }

        public static bool GrantToPerson(PersonBehaviour person, string powerId)
        {
            if (person == null) return false;
            var entry = Find(powerId);
            if (entry == null) return false;

            var root = person.gameObject;
            var cp = PowerSetup.EnsurePowerMenu(root, person);
            if (PowerSetup.HasPowerEntry(cp, powerId))
            {
                ModAPI.Notify(person.name + " already has " + entry.Name + ".");
                return false;
            }

            PowerSetup.EnsureModFighterDurability(person);
            PowerSetup.EnsureKiCombatStack(root);
            entry.Grant(person, root, cp);
            PowerSetup.RegisterUniversalPhysicalPowers(cp, person);
            cp.HarvestPowerStates();
            ModAPI.Notify("Granted " + entry.Name + " to " + person.name + ".");
            return true;
        }
    }
}
