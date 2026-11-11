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
    public static class PowerCatalog
    {
        public class Overrides
        {
            public string Name;
            public string Description;
            public Sprite Icon;
            public string EffectId;
            public string BeamType;
            public string SolarCharacter;
            public string BallEffectId;
            public string GogetaComponentType;
            public string BeerusComponentType;
            public bool GokuStyleBeam;
            public Func<bool> BlockWhen;
            public Action OnGrant;
            public Action OnEnable;
            public Action<CharacterPowers, PowerSetup.CombatRefs, PersonBehaviour, GameObject> CustomRegister;
        }

        public enum Kind { KiBlast, Beam, Solar, Shield, FinalExplosion, Hellzone, Ball, StraightBeam, GogetaSpecial, BeerusSpecial, Punch, Kick, Barrage, Flight, InstantTransmission, Kaioken }

        public class Definition
        {
            public string Id;
            public string Name;
            public string Description;
            public string Category;
            public Kind PowerKind;
            public Sprite Icon;
            public string EffectId;
            public string BeamType;
            public string GogetaComponentType;
            public string BeerusComponentType;
            public bool Passive;
        }

        static Dictionary<string, Definition> _defs;

        static void Def(Definition d) => _defs[d.Id] = d;

        static void Build()
        {
            _defs = new Dictionary<string, Definition>();
            Def(new Definition { Id = "KiBlast", Name = "Ki Blast", Description = "Single-shot ki projectiles from the arms.", Category = "Ki", PowerKind = Kind.KiBlast, Icon = DBPU.Icon_KiBlast });
            Def(new Definition { Id = "EnergyWave", Name = "Energy Wave", Description = "Beam attack.", Category = "Beam", PowerKind = Kind.Beam, Icon = DBPU.Icon_EnergyWave, EffectId = "Energy Wave" });
            Def(new Definition { Id = "Kamehameha", Name = "Kamehameha", Description = "Beam attack.", Category = "Beam", PowerKind = Kind.Beam, Icon = DBPU.Icon_Kamehameha, EffectId = "Kamehameha" });
            Def(new Definition { Id = "RoseKamehameha", Name = "Rose Kamehameha", Description = "Rose-tinted kamehameha beam.", Category = "Beam", PowerKind = Kind.Beam, Icon = DBPU.Icon_RoseKamehameha, EffectId = "Rose Kamehameha" });
            Def(new Definition { Id = "GalickGun", Name = "Galick Gun", Description = "Beam attack.", Category = "Beam", PowerKind = Kind.Beam, Icon = DBPU.Icon_GalickGun, EffectId = "Galick Gun" });
            Def(new Definition { Id = "FinalFlash", Name = "Final Flash", Description = "Beam attack.", Category = "Beam", PowerKind = Kind.Beam, Icon = DBPU.Icon_FinalFlash, EffectId = "Final Flash" });
            Def(new Definition { Id = "SpecialBeamCannon", Name = "Special Beam Cannon", Description = "Beam attack.", Category = "Beam", PowerKind = Kind.Beam, Icon = null, EffectId = "Special Beam Cannon" });
            Def(new Definition { Id = "FullForceKamehameha", Name = "Full Force Kamehameha", Description = "Beam attack.", Category = "Beam", PowerKind = Kind.Beam, Icon = DBPU.Icon_FullForceKamehameha, EffectId = "Full Force Kamehameha" });
            Def(new Definition { Id = "GiganticFullBlast", Name = "Gigantic Full Blast", Description = "Beam attack.", Category = "Beam", PowerKind = Kind.Beam, Icon = DBPU.Icon_GiganticFullBlast, EffectId = "Gigantic Full Blast", BeamType = "ArmBeam" });
            Def(new Definition { Id = "GiganticRoar", Name = "Gigantic Roar", Description = "Beam attack.", Category = "Beam", PowerKind = Kind.Beam, Icon = DBPU.Icon_GiganticRoar, EffectId = "Gigantic Roar", BeamType = "MouthBeam" });
            Def(new Definition { Id = "MaxCannon", Name = "Max Cannon", Description = "Mouth beam attack.", Category = "Beam", PowerKind = Kind.Beam, Icon = DBPU.Icon_MaxCannon, EffectId = "Max Cannon", BeamType = "MouthBeam" });
            Def(new Definition { Id = "SolarFlare", Name = "Solar Flare", Description = "Blinds nearby targets.", Category = "Special", PowerKind = Kind.Solar, Icon = DBPU.Icon_SolarFlare });
            Def(new Definition { Id = "KiShield", Name = "Ki Shield", Description = "Mobile ki barrier. Activate on the upper torso. Incoming attacks drain ki and integrity; stronger attacks can break through.", Category = "Defense", PowerKind = Kind.Shield, Icon = DBPU.Icon_KiShield });
            Def(new Definition { Id = "FinalExplosion", Name = "Final Explosion", Description = "Self-destruct attack.", Category = "Special", PowerKind = Kind.FinalExplosion, Icon = DBPU.Icon_FinalExplosion });
            Def(new Definition { Id = "HellzoneGranade", Name = "Hellzone Grenade", Description = "Trap ki blasts in an area.", Category = "Special", PowerKind = Kind.Hellzone, Icon = DBPU.Icon_HellzoneGranade });
            Def(new Definition { Id = "GiganticCrusher", Name = "Gigantic Crusher", Description = "Large ki sphere attack.", Category = "Ball", PowerKind = Kind.Ball, Icon = DBPU.Icon_GiganticCrusher, EffectId = "Gigantic Crusher" });
            Def(new Definition { Id = "SpiritBomb", Name = "Spirit Bomb", Description = "Grow a Spirit Bomb with charge time and shared Ki. Draw enemies inside for eight pulses and a final explosion.", Category = "Ball", PowerKind = Kind.Ball, Icon = DBPU.Icon_SpiritBomb, EffectId = "Spirit Bomb" });
            Def(new Definition { Id = "SuperSpiritBomb", Name = "Super Spirit Bomb", Description = "Spirit Bomb compatibility variant: the same continuous growth from charge time and shared Ki, with containment pulses and a final explosion.", Category = "Ball", PowerKind = Kind.Ball, Icon = DBPU.Icon_SuperSpiritBomb, EffectId = "Super Spirit Bomb" });
            Def(new Definition { Id = "DeathBeam", Name = "Death Beam", Description = "Finger beam attack.", Category = "Beam", PowerKind = Kind.StraightBeam, Icon = DBPU.Icon_DeathBeam, EffectId = "Death Beam" });
            Def(new Definition { Id = "DeathBall", Name = "Death Ball", Description = "Large destructive ki sphere.", Category = "Ball", PowerKind = Kind.Ball, Icon = DBPU.Icon_DeathBall, EffectId = "Death Ball" });
            Def(new Definition { Id = "Supernova", Name = "Supernova", Description = "Solar destruction sphere.", Category = "Ball", PowerKind = Kind.Ball, Icon = DBPU.Icon_Supernova, EffectId = "Supernova" });
            Def(new Definition { Id = "LastEmperor", Name = "Last Emperor", Description = "Emperor's ultimate beam.", Category = "Beam", PowerKind = Kind.Beam, Icon = DBPU.Icon_LastEmperor, EffectId = "Last Emperor" });
            Def(new Definition { Id = "StardustFall", Name = "Stardust Fall", Description = "Gogeta special technique.", Category = "Gogeta", PowerKind = Kind.GogetaSpecial, Icon = DBPU.Icon_StardustFall, GogetaComponentType = "StardustFall" });
            Def(new Definition { Id = "MeteorExplosion", Name = "Meteor Explosion", Description = "Gogeta special technique.", Category = "Gogeta", PowerKind = Kind.GogetaSpecial, Icon = DBPU.Icon_MeteorExplosion, GogetaComponentType = "MeteorExplosion" });
            Def(new Definition { Id = "GodPunisher", Name = "God Punisher", Description = "Gogeta special technique.", Category = "Gogeta", PowerKind = Kind.GogetaSpecial, Icon = DBPU.Icon_GodPunisher, GogetaComponentType = "GodPunisher" });
            Def(new Definition { Id = "StardustBreaker", Name = "Stardust Breaker", Description = "Gogeta special technique.", Category = "Gogeta", PowerKind = Kind.GogetaSpecial, Icon = DBPU.Icon_StardustBreaker, GogetaComponentType = "StardustBreaker" });
            Def(new Definition { Id = "PunchPower", Name = "Punch Power", Description = "Melee enhancement on arm contact.", Category = "Physical", PowerKind = Kind.Punch, Icon = DBPU.Icon_PunchPower, Passive = false });
            Def(new Definition { Id = "KickPower", Name = "Kick Power", Description = "Melee enhancement on leg contact.", Category = "Physical", PowerKind = Kind.Kick, Icon = DBPU.Icon_KickPower, Passive = false });
            Def(new Definition { Id = "Barrage", Name = "Barrage", Description = "Rapid projectile barrage (hold H).", Category = "Physical", PowerKind = Kind.Barrage, Icon = DBPU.Icon_Barrage, Passive = false });
            Def(new Definition { Id = "Flight", Name = "Flight", Description = "Passive flight ability on the lower body.", Category = "Physical", PowerKind = Kind.Flight, Icon = DBPU.Icon_Flight, Passive = true });
            Def(new Definition { Id = "InstantTransmission", Name = "Instant Transmission", Description = "Teleport short distances.", Category = "Movement", PowerKind = Kind.InstantTransmission, Icon = DBPU.Icon_InstantTransmission });
            Def(new Definition { Id = "Kaioken", Name = "Kaioken", Description = "Power boost with strain buildup. Toggle from the powers menu or press K.", Category = "Special", PowerKind = Kind.Kaioken, Icon = DBPU.Icon_Kaioken });
            Def(new Definition { Id = "WrathOfTheGod", Name = "Wrath of the G.O.D.", Description = "Energy wave beam attack.", Category = "Beerus", PowerKind = Kind.Beam, Icon = DBPU.Icon_WrathOfTheGod, EffectId = "Energy Wave" });
            Def(new Definition { Id = "BeerusBall", Name = "Beerus Ball", Description = "A stronger destruction sphere than Supernova.", Category = "Beerus", PowerKind = Kind.Ball, Icon = DBPU.Icon_BeerusBall, EffectId = "Beerus Ball" });
            Def(new Definition { Id = "GodsWrath", Name = "G.O.D's Wrath", Description = "Omni-directional ki blast barrage from the middle body.", Category = "Beerus", PowerKind = Kind.BeerusSpecial, Icon = DBPU.Icon_GodsWrath, BeerusComponentType = "GodsWrath" });
            Def(new Definition { Id = "CataclysmicOrb", Name = "Cataclysmic Orb", Description = "Tiny atom-like sphere with a devastating explosion.", Category = "Beerus", PowerKind = Kind.BeerusSpecial, Icon = DBPU.Icon_CataclysmicOrb, BeerusComponentType = "CataclysmicOrb" });
        }

        public static Definition Get(string id)
        {
            if (_defs == null) Build();
            return _defs.TryGetValue(id, out var d) ? d : null;
        }

        public static List<Definition> All
        {
            get
            {
                if (_defs == null) Build();
                return new List<Definition>(_defs.Values);
            }
        }

        static Action DefaultKiOnEnable(PowerSetup.CombatRefs r) => () =>
        {
            if (r.Beam != null) { r.Beam.CancelBeamState(); r.Beam.Active = false; }
            if (r.Shield != null) r.Shield.Active = false;
        };

        static void EnsureBeamStack(GameObject instance)
        {
            if (instance.GetComponent<BlastBehaviour>() == null) instance.AddComponent<BlastBehaviour>();
            if (instance.GetComponent<BeamBehaviour>() == null) instance.AddComponent<BeamBehaviour>();
            PowerSetup.ScheduleBeamTriggers(instance.GetComponent<BeamBehaviour>());
        }

        static void EnsureComponents(Definition def, PersonBehaviour person, GameObject instance, Overrides o)
        {
            switch (def.PowerKind)
            {
                case Kind.KiBlast:
                    if (instance.GetComponent<BlastBehaviour>() == null) instance.AddComponent<BlastBehaviour>();
                    break;
                case Kind.Beam:
                    EnsureBeamStack(instance);
                    var beamBehaviour = instance.GetComponent<BeamBehaviour>();
                    string beamEffect = o.EffectId ?? def.EffectId;
                    if (beamBehaviour != null && !string.IsNullOrEmpty(beamEffect))
                        beamBehaviour.EffectID = beamEffect;
                    break;
                case Kind.Solar:
                    var solar = instance.GetComponent<SolarFlare>() ?? instance.AddComponent<SolarFlare>();
                    solar.Character = o.SolarCharacter ?? "Goku";
                    EnsureBeamStack(instance);
                    break;
                case Kind.Shield:
                    if (instance.GetComponent<KiShield>() == null) instance.AddComponent<KiShield>();
                    EnsureBeamStack(instance);
                    break;
                case Kind.FinalExplosion:
                    if (instance.GetComponent<FinalExplosion>() == null) instance.AddComponent<FinalExplosion>();
                    EnsureBeamStack(instance);
                    break;
                case Kind.Hellzone:
                    if (instance.GetComponent<HellzoneGranade>() == null) instance.AddComponent<HellzoneGranade>();
                    EnsureBeamStack(instance);
                    break;
                case Kind.Ball:
                    var ball = instance.GetComponent<BallBehaviour>() ?? instance.AddComponent<BallBehaviour>();
                    ball.EffectID = o.BallEffectId ?? o.EffectId ?? def.EffectId ?? def.Id;
                    EnsureBeamStack(instance);
                    break;
                case Kind.StraightBeam:
                    var straight = instance.GetComponent<StraightBeamBehaviour>() ?? instance.AddComponent<StraightBeamBehaviour>();
                    straight.EffectID = o.EffectId ?? def.EffectId ?? "Death Beam";
                    EnsureBeamStack(instance);
                    break;
                case Kind.GogetaSpecial:
                    Component comp = null;
                    string ct = o.GogetaComponentType ?? def.GogetaComponentType;
                    if (ct == "StardustFall") comp = instance.GetComponent<StardustFall>() ?? instance.AddComponent<StardustFall>();
                    else if (ct == "MeteorExplosion") comp = instance.GetComponent<MeteorExplosion>() ?? instance.AddComponent<MeteorExplosion>();
                    else if (ct == "GodPunisher") comp = instance.GetComponent<GodPunisher>() ?? instance.AddComponent<GodPunisher>();
                    else if (ct == "StardustBreaker") comp = instance.GetComponent<StardustBreaker>() ?? instance.AddComponent<StardustBreaker>();
                    if (comp != null) EnsureBeamStack(instance);
                    break;
                case Kind.BeerusSpecial:
                    Component beerusComp = null;
                    string bt = o.BeerusComponentType ?? def.BeerusComponentType;
                    if (bt == "GodsWrath") beerusComp = instance.GetComponent<GodsWrathBehaviour>() ?? instance.AddComponent<GodsWrathBehaviour>();
                    else if (bt == "CataclysmicOrb") beerusComp = instance.GetComponent<CataclysmicOrbBehaviour>() ?? instance.AddComponent<CataclysmicOrbBehaviour>();
                    if (beerusComp != null) EnsureBeamStack(instance);
                    break;
                case Kind.Punch:
                    PowerSetup.EnsurePunchPowerComponents(person);
                    break;
                case Kind.Kick:
                    PowerSetup.EnsureKickPowerComponents(person);
                    break;
                case Kind.Barrage:
                    PowerSetup.EnsurePunchPowerComponents(person);
                    PowerSetup.EnsureBarrageComponent(person);
                    break;
                case Kind.Flight:
                    Transform lower = person.transform.Find("Body/LowerBody");
                    if (lower == null)
                        foreach (var limb in person.Limbs)
                            if (limb.name.Contains("LowerBody")) { lower = limb.transform; break; }
                    if (lower != null && lower.GetComponent<Flight>() == null)
                        lower.gameObject.AddComponent<Flight>();
                    break;
                case Kind.InstantTransmission:
                    if (instance.GetComponent<InstantTransmission>() == null)
                        instance.AddComponent<InstantTransmission>();
                    break;
                case Kind.Kaioken:
                    if (instance.GetComponent<Kaioken>() == null)
                        instance.AddComponent<Kaioken>();
                    if (instance.GetComponent<AuraHandler>() == null)
                    {
                        var auraHandler = instance.AddComponent<AuraHandler>();
                        auraHandler.person = person;
                    }
                    break;
            }
        }

        static void RegisterMenu(Definition def, CharacterPowers powers, PowerSetup.CombatRefs r, PersonBehaviour person, GameObject instance, Overrides o)
        {
            string name = o.Name ?? def.Name;
            string desc = o.Description ?? def.Description;
            Sprite icon = o.Icon ?? def.Icon ?? DBPU.KiBlast_Sprite;

            switch (def.PowerKind)
            {
                case Kind.KiBlast:
                    if (PowerSetup.HasPowerEntry(powers, "KiBlast")) return;
                    var kiBlock = o.BlockWhen ?? (() => r.Beam != null && r.Beam.StillActive);
                    var kiOn = o.OnEnable ?? DefaultKiOnEnable(r);
                    PowerSetup.AddKiBlast(powers, r, kiBlock, kiOn);
                    break;
                case Kind.Beam:
                    if (PowerSetup.HasPowerEntry(powers, def.Id)) return;
                    string effectId = o.EffectId ?? def.EffectId ?? name;
                    string beamType = o.BeamType ?? def.BeamType;
                    if (o.GokuStyleBeam)
                        PowerSetup.AddGokuStyleBeam(powers, r, def.Id, name, effectId, icon);
                    else
                    {
                        var beamBlock = o.BlockWhen ?? (() => r.BeamStillBlocked);
                        var beamOn = o.OnEnable ?? (() => { r.Blast.Active = false; if (r.Shield != null) r.Shield.Active = false; });
                        if (!string.IsNullOrEmpty(beamType))
                            PowerSetup.AddLegacyBeamWithType(powers, r, def.Id, name, effectId, beamType, beamBlock, beamOn, icon);
                        else
                            PowerSetup.AddLegacyBeam(powers, r, def.Id, name, effectId, beamBlock, beamOn, icon);
                    }
                    break;
                case Kind.Solar:
                    if (PowerSetup.HasPowerEntry(powers, "SolarFlare")) return;
                    PowerSetup.AddSolarFlare(powers, r, icon);
                    break;
                case Kind.Shield:
                    if (PowerSetup.HasPowerEntry(powers, "KiShield")) return;
                    PowerSetup.AddKiShield(powers, r, o.OnEnable ?? (() =>
                    {
                        if (r.Beam != null) { r.Beam.CancelBeamState(); r.Beam.Active = false; }
                        r.Blast.Active = false;
                    }), icon);
                    break;
                case Kind.FinalExplosion:
                    if (PowerSetup.HasPowerEntry(powers, "FinalExplosion")) return;
                    PowerSetup.AddFinalExplosion(powers, r, icon);
                    break;
                case Kind.Hellzone:
                    if (PowerSetup.HasPowerEntry(powers, "HellzoneGranade")) return;
                    PowerSetup.AddHellzone(powers, r, icon);
                    break;
                case Kind.Ball:
                    if (PowerSetup.HasPowerEntry(powers, def.Id)) return;
                    PowerSetup.AddBallPower(powers, r, def.Id, name, o.BallEffectId ?? o.EffectId ?? def.EffectId, icon);
                    break;
                case Kind.StraightBeam:
                    if (PowerSetup.HasPowerEntry(powers, def.Id)) return;
                    PowerSetup.AddStraightBeamPower(powers, r, def.Id, name, o.EffectId ?? def.EffectId, o.OnEnable ?? DefaultKiOnEnable(r), icon);
                    break;
                case Kind.GogetaSpecial:
                    if (PowerSetup.HasPowerEntry(powers, def.Id)) return;
                    string ct = o.GogetaComponentType ?? def.GogetaComponentType;
                    Component comp = ct == "StardustFall" ? r.StardustFall
                        : ct == "MeteorExplosion" ? (Component)r.MeteorExplosion
                        : ct == "GodPunisher" ? (Component)r.GodPunisher
                        : ct == "StardustBreaker" ? (Component)r.StardustBreaker : null;
                    if (comp != null) PowerSetup.AddGogetaSpecialToggle(powers, r, def.Id, name, comp, icon);
                    break;
                case Kind.Punch:
                    if (PowerSetup.HasPowerEntry(powers, "PunchPower")) return;
                    PowerSetup.AddPunchPowerMenu(powers, person, name, desc, icon);
                    break;
                case Kind.Kick:
                    if (PowerSetup.HasPowerEntry(powers, "KickPower")) return;
                    PowerSetup.AddKickPowerMenu(powers, person, name, desc, icon);
                    break;
                case Kind.Barrage:
                    if (PowerSetup.HasPowerEntry(powers, "Barrage")) return;
                    PowerSetup.AddBarrageMenu(powers, person, name, desc, icon);
                    break;
                case Kind.Flight:
                    if (PowerSetup.HasPowerEntry(powers, "Flight")) return;
                    PowerSetup.AddPassiveMenuPower(powers, "Flight", name, desc, icon);
                    break;
                case Kind.InstantTransmission:
                    if (PowerSetup.HasPowerEntry(powers, "InstantTransmission")) return;
                    PowerSetup.AddInstantTransmissionMenu(powers, instance, name, desc, icon);
                    break;
                case Kind.Kaioken:
                    if (PowerSetup.HasPowerEntry(powers, "Kaioken")) return;
                    PowerSetup.AddKaiokenMenu(powers, instance, person, name, desc, icon);
                    break;
                case Kind.BeerusSpecial:
                    if (PowerSetup.HasPowerEntry(powers, def.Id)) return;
                    string bct = o.BeerusComponentType ?? def.BeerusComponentType;
                    Component beerusComp = bct == "GodsWrath" ? r.GodsWrath
                        : bct == "CataclysmicOrb" ? (Component)r.CataclysmicOrb : null;
                    if (beerusComp != null) PowerSetup.AddBeerusSpecialToggle(powers, r, def.Id, name, desc, beerusComp, icon);
                    break;
            }
        }

        public static bool Add(CharacterPowers powers, GameObject instance, PersonBehaviour person, string id, Overrides o = null)
        {
            if (powers == null || instance == null || person == null || string.IsNullOrEmpty(id)) return false;
            o = o ?? new Overrides();

            if (o.CustomRegister != null)
            {
                if (PowerSetup.HasPowerEntry(powers, id)) return false;
                o.OnGrant?.Invoke();
                var customDef = Get(id);
                if (customDef != null) EnsureComponents(customDef, person, instance, o);
                var refs = PowerSetup.CombatRefs.From(instance);
                o.CustomRegister(powers, refs, person, instance);
                return true;
            }

            var def = Get(id);
            if (def == null) return false;
            if (def.PowerKind != Kind.InstantTransmission && PowerSetup.HasPowerEntry(powers, id)) return false;
            if (def.PowerKind == Kind.KiBlast && PowerSetup.HasPowerEntry(powers, "KiBlast")) return false;

            o.OnGrant?.Invoke();
            EnsureComponents(def, person, instance, o);

            var r = PowerSetup.CombatRefs.From(instance);
            RegisterMenu(def, powers, r, person, instance, o);

            return true;
        }

        public static void AddMany(CharacterPowers powers, GameObject instance, PersonBehaviour person, Overrides shared, params string[] ids)
        {
            if (ids == null) return;
            for (int i = 0; i < ids.Length; i++)
                Add(powers, instance, person, ids[i], shared);
        }

        public static void AddMany(CharacterPowers powers, GameObject instance, PersonBehaviour person, params string[] ids)
        {
            if (ids == null) return;
            for (int i = 0; i < ids.Length; i++)
                Add(powers, instance, person, ids[i]);
        }
    }
}
