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
    public static class PowerSetup
    {
        public class CombatRefs
        {
            public BlastBehaviour Blast;
            public BeamBehaviour Beam;
            public SolarFlare Solar;
            public KiShield Shield;
            public FinalExplosion FinalExplosion;
            public HellzoneGranade Hellzone;
            public BallBehaviour Ball;
            public StardustFall StardustFall;
            public MeteorExplosion MeteorExplosion;
            public GodPunisher GodPunisher;
            public StardustBreaker StardustBreaker;
            public StraightBeamBehaviour StraightBeam;
            public GodsWrathBehaviour GodsWrath;
            public CataclysmicOrbBehaviour CataclysmicOrb;

            public static CombatRefs From(GameObject instance)
            {
                return new CombatRefs
                {
                    Blast = instance.GetComponent<BlastBehaviour>(),
                    Beam = instance.GetComponent<BeamBehaviour>(),
                    Solar = instance.GetComponent<SolarFlare>(),
                    Shield = instance.GetComponent<KiShield>(),
                    FinalExplosion = instance.GetComponent<FinalExplosion>(),
                    Hellzone = instance.GetComponent<HellzoneGranade>(),
                    Ball = instance.GetComponent<BallBehaviour>(),
                    StardustFall = instance.GetComponent<StardustFall>(),
                    MeteorExplosion = instance.GetComponent<MeteorExplosion>(),
                    GodPunisher = instance.GetComponent<GodPunisher>(),
                    StardustBreaker = instance.GetComponent<StardustBreaker>(),
                    StraightBeam = instance.GetComponent<StraightBeamBehaviour>(),
                    GodsWrath = instance.GetComponent<GodsWrathBehaviour>(),
                    CataclysmicOrb = instance.GetComponent<CataclysmicOrbBehaviour>()
                };
            }

            public bool VegetaBlocked => (FinalExplosion != null && FinalExplosion.stillActive) || (Beam != null && Beam.StillActive);
            public bool BeamStillBlocked => Beam != null && Beam.StillActive;
        }

        public class PowerBuilder
        {
            public GameObject Head;
            public PersonBehaviour Person;
            public GameObject Instance;
            public CharacterPowers Powers;

            public CombatRefs Refs => CombatRefs.From(Instance);

            public void Grant(string powerId, PowerCatalog.Overrides overrides = null) =>
                PowerCatalog.Add(Powers, Instance, Person, powerId, overrides);

            public void GrantMany(PowerCatalog.Overrides shared, params string[] powerIds) =>
                PowerCatalog.AddMany(Powers, Instance, Person, shared, powerIds);

            public void GrantMany(params string[] powerIds) =>
                PowerCatalog.AddMany(Powers, Instance, Person, powerIds);

            public void Finish() => FinishPowers(Head, Person, Instance, Powers);
        }

        public static PowerBuilder BeginPowers(GameObject head, PersonBehaviour person, GameObject instance)
        {
            if (instance.GetComponent<KiSystem>() == null) instance.AddComponent<KiSystem>();
            if (instance.GetComponent<MultipliersAndStuffs>() == null) instance.AddComponent<MultipliersAndStuffs>();

            var characterPowers = instance.GetComponent<CharacterPowers>();
            if (characterPowers == null) characterPowers = instance.AddComponent<CharacterPowers>();

            var powerCM = head.GetComponent<PowerContextMenu>();
            if (powerCM == null) powerCM = head.AddComponent<PowerContextMenu>();
            powerCM.PowersData = characterPowers;
            powerCM.person = person;

            return new PowerBuilder
            {
                Head = head,
                Person = person,
                Instance = instance,
                Powers = characterPowers
            };
        }

        public static void FinishPowers(GameObject head, PersonBehaviour person, GameObject instance, CharacterPowers powers)
        {
            EnsureKickPowerComponents(person);
            RegisterUniversalPhysicalPowers(powers, person);
            ApplyAutoEnablePunchKick(person);
            powers.HarvestPowerStates();

            var beam = instance.GetComponent<BeamBehaviour>();
            if (beam != null) ScheduleBeamTriggers(beam);
        }

        public static void CompletePhysicalPowers(GameObject instance, PersonBehaviour person)
        {
            var powers = instance.GetComponent<CharacterPowers>();
            if (powers == null || person == null) return;
            EnsureKickPowerComponents(person);
            RegisterUniversalPhysicalPowers(powers, person);
            ApplyAutoEnablePunchKick(person);
            powers.HarvestPowerStates();
        }

        public static void ApplyAutoEnablePunchKick(PersonBehaviour person)
        {
            if (!DBPU.AutoEnablePunchKick || person == null) return;
            EnsurePunchPowerComponents(person);
            EnsureKickPowerComponents(person);
            SetAllPunchPowerActive(person, true);
            SetAllKickPowerActive(person, true);
        }

        public static void ApplyAutoEnablePunchKickToAllAlive()
        {
            if (!DBPU.AutoEnablePunchKick) return;
            foreach (var person in UnityEngine.Object.FindObjectsOfType<PersonBehaviour>())
            {
                if (person == null || !person.IsAlive()) continue;
                if (person.transform.root.GetComponent<DbpuCharacterIdentity>() == null
                    && person.GetComponentInChildren<PunchPower>(true) == null
                    && person.GetComponentInChildren<KickPower>(true) == null)
                    continue;
                ApplyAutoEnablePunchKick(person);
                var powers = person.GetComponent<CharacterPowers>();
                if (powers == null) powers = person.transform.root.GetComponent<CharacterPowers>();
                if (powers != null)
                {
                    RegisterUniversalPhysicalPowers(powers, person);
                    powers.HarvestPowerStates();
                }
            }
        }

        public static void WireCombatMenu(GameObject head, PersonBehaviour person, GameObject instance, Action<CharacterPowers, GameObject> register)
        {
            var pw = BeginPowers(head, person, instance);
            register(pw.Powers, instance);
            pw.Finish();
        }

        public static void WireCharacterAtSpawn(GameObject head, PersonBehaviour person, GameObject instance, Action<CharacterPowers, GameObject> register) =>
            WireCombatMenu(head, person, instance, register);

        public static void WireCharacterAtSpawn(GameObject head, PersonBehaviour person, GameObject instance, Action<PowerBuilder> register)
        {
            var pw = BeginPowers(head, person, instance);
            register(pw);
            pw.Finish();
        }

        static void ToggleBeamPower(BeamBehaviour beam, BlastBehaviour blast, SolarFlare solar, KiShield shield, string effectId)
        {
            if (beam.StillActive) return;

            if (beam.Active && beam.EffectID == effectId)
            {
                beam.Active = false;
                beam.CancelBeamState();
                return;
            }

            beam.CancelBeamState();
            beam.EffectID = effectId;
            beam.Active = true;
            blast.Active = false;
            if (solar != null) solar.Active = false;
            if (shield != null) shield.Active = false;
        }

        static void ToggleLegacyBeam(CombatRefs r, string effectId, Func<bool> blockWhen, Action onBeamOn)
        {
            if (blockWhen != null && blockWhen()) return;
            var beam = r.Beam;
            if (!beam.Active)
            {
                beam.EffectID = effectId;
                beam.Active = true;
            }
            else if (beam.EffectID == effectId)
                beam.Active = !beam.Active;
            else
                beam.EffectID = effectId;

            if (beam.Active)
                onBeamOn?.Invoke();
        }

        static bool HasPunchPower(PersonBehaviour person) =>
            person != null && person.GetComponentInChildren<PunchPower>(true) != null;

        static bool HasKickPower(PersonBehaviour person) =>
            person != null && person.GetComponentInChildren<KickPower>(true) != null;

        public static void EnsurePunchPowerComponents(PersonBehaviour person)
        {
            if (person == null) return;
            if (person.Limbs.Length > 13)
            {
                AddPunchPowerIfMissing(person.Limbs[11]);
                AddPunchPowerIfMissing(person.Limbs[13]);
                return;
            }
            if (person.Limbs.Length > 12)
            {
                AddPunchPowerIfMissing(person.Limbs[10]);
                AddPunchPowerIfMissing(person.Limbs[12]);
                return;
            }
            foreach (var limb in person.Limbs)
            {
                if (!limb.name.Contains("LowerArm")) continue;
                AddPunchPowerIfMissing(limb);
            }
        }

        static void AddPunchPowerIfMissing(LimbBehaviour limb)
        {
            if (limb == null || limb.GetComponent<PunchPower>() != null) return;
            limb.gameObject.AddComponent<PunchPower>();
        }

        public static void EnsureKickPowerComponents(PersonBehaviour person)
        {
            if (person == null) return;
            foreach (var limb in person.Limbs)
            {
                if (!IsKickLimb(limb.name)) continue;
                if (limb.GetComponent<KickPower>() == null)
                    limb.gameObject.AddComponent<KickPower>();
            }
        }

        static void SetAllPunchPowerActive(PersonBehaviour person, bool active)
        {
            if (person == null) return;
            foreach (var limb in person.Limbs)
            {
                var pp = limb.GetComponent<PunchPower>();
                if (pp != null) pp.Active = active;
            }
        }

        static void SetAllKickPowerActive(PersonBehaviour person, bool active)
        {
            if (person == null) return;
            foreach (var limb in person.Limbs)
            {
                if (!IsKickLimb(limb.name)) continue;
                var kp = limb.GetComponent<KickPower>();
                if (kp != null) kp.Active = active;
            }
        }

        static bool AnyPunchPowerActive(PersonBehaviour person)
        {
            if (person == null) return false;
            foreach (var limb in person.Limbs)
            {
                var pp = limb.GetComponent<PunchPower>();
                if (pp != null && pp.Active) return true;
            }
            return false;
        }

        static bool AnyKickPowerActive(PersonBehaviour person)
        {
            if (person == null) return false;
            foreach (var limb in person.Limbs)
            {
                if (!IsKickLimb(limb.name)) continue;
                var kp = limb.GetComponent<KickPower>();
                if (kp != null && kp.Active) return true;
            }
            return false;
        }

        public static void AddPunchPowerMenu(CharacterPowers powers, PersonBehaviour person, string name = null, string desc = null, Sprite icon = null)
        {
            icon = icon ?? DBPU.Icon_PunchPower;
            if (HasPowerEntry(powers, "PunchPower")) return;
            powers.InsertPowerAtStart(new CharacterPowers.PowerStats
            {
                ID = "PunchPower",
                Name = name ?? "Punch Power",
                Description = desc ?? "Melee enhancement on arm contact.",
                Icon = icon,
                Illustration = icon,
                IsActive = () => AnyPunchPowerActive(person),
                Disable = () => SetAllPunchPowerActive(person, false),
                Apply = () => SetAllPunchPowerActive(person, !AnyPunchPowerActive(person))
            });
        }

        public static void AddKickPowerMenu(CharacterPowers powers, PersonBehaviour person, string name = null, string desc = null, Sprite icon = null)
        {
            icon = icon ?? DBPU.Icon_KickPower;
            if (HasPowerEntry(powers, "KickPower")) return;
            powers.InsertPowerAtStart(new CharacterPowers.PowerStats
            {
                ID = "KickPower",
                Name = name ?? "Kick Power",
                Description = desc ?? "Melee enhancement on leg contact.",
                Icon = icon,
                Illustration = icon,
                IsActive = () => AnyKickPowerActive(person),
                Disable = () => SetAllKickPowerActive(person, false),
                Apply = () => SetAllKickPowerActive(person, !AnyKickPowerActive(person))
            });
        }

        public static void EnsureBarrageComponent(PersonBehaviour person)
        {
            if (person == null || person.GetComponentInChildren<Barrage>(true) != null) return;

            LimbBehaviour host = null;
            if (person.Limbs.Length > 1)
                host = person.Limbs[1];
            if (host == null)
            {
                foreach (var limb in person.Limbs)
                {
                    if (limb.name.Contains("UpperBody"))
                    {
                        host = limb;
                        break;
                    }
                }
            }
            if (host == null)
            {
                foreach (var limb in person.Limbs)
                {
                    if (limb.name.Contains("MiddleBody") || limb.name.Contains("UpperBody"))
                    {
                        host = limb;
                        break;
                    }
                }
            }
            if (host != null)
                host.gameObject.AddComponent<Barrage>();
        }

        static bool HasBarrage(PersonBehaviour person) =>
            person != null && person.GetComponentInChildren<Barrage>(true) != null;

        static bool HasFlight(PersonBehaviour person) =>
            person != null && person.GetComponentInChildren<Flight>(true) != null;

        public static bool IsKickLimb(string limbName) =>
            limbName != null && (limbName.Contains("LowerLeg") || limbName.Contains("Foot"));

        public static void AddBarrageMenu(CharacterPowers powers, PersonBehaviour person, string name = null, string desc = null, Sprite icon = null)
        {
            icon = icon ?? DBPU.Icon_Barrage;
            if (HasPowerEntry(powers, "Barrage")) return;
            powers.InsertPowerAtStart(new CharacterPowers.PowerStats
            {
                ID = "Barrage",
                Name = name ?? "Barrage",
                Description = desc ?? "Rapid projectile barrage (hold H).",
                Icon = icon,
                Illustration = icon,
                IsActive = () => AnyBarrageEnabled(person),
                Disable = () => SetAllBarrageEnabled(person, false),
                Apply = () => SetAllBarrageEnabled(person, !AnyBarrageEnabled(person))
            });
        }

        static void SetAllBarrageEnabled(PersonBehaviour person, bool enabled)
        {
            if (person == null) return;
            foreach (var barrage in person.GetComponentsInChildren<Barrage>(true))
            {
                if (barrage == null) continue;
                barrage.Enabled = enabled;
            }
        }

        static bool AnyBarrageEnabled(PersonBehaviour person)
        {
            if (person == null) return false;
            foreach (var barrage in person.GetComponentsInChildren<Barrage>(true))
            {
                if (barrage != null && barrage.Enabled) return true;
            }
            return false;
        }

        public static void AddPassiveMenuPower(CharacterPowers powers, string id, string name, string desc, Sprite icon = null)
        {
            icon = icon ?? (id == "Barrage" ? DBPU.Icon_Barrage : id == "Flight" ? DBPU.Icon_Flight : DBPU.PIconTemplate);
            powers.InsertPowerAtStart(new CharacterPowers.PowerStats
            {
                ID = id,
                Name = name,
                Description = desc,
                Icon = icon,
                Illustration = icon,
                Passive = true
            });
        }

        public static void RegisterUniversalPhysicalPowers(CharacterPowers powers, PersonBehaviour person)
        {
            if (powers == null || person == null) return;
            if (HasKickPower(person)) AddKickPowerMenu(powers, person);
            if (HasPunchPower(person)) AddPunchPowerMenu(powers, person);
            if (HasBarrage(person)) AddBarrageMenu(powers, person);
            if (HasFlight(person)) AddPassiveMenuPower(powers, "Flight", "Flight", "Passive flight ability on the lower body.");
            powers.MovePowerBlockToStart(PhysicalPowerMenuOrder);
        }

        static readonly string[] PhysicalPowerMenuOrder = { "KickPower", "PunchPower", "Barrage", "Flight" };

        public static void AddKiBlast(CharacterPowers powers, CombatRefs r, Func<bool> blockWhen, Action onEnable)
        {
            if (!powers.PowerData.ContainsKey("KiBlast"))
            {
                powers.AddPower(new CharacterPowers.PowerStats
                {
                    ID = "KiBlast",
                    Name = "Ki Blast",
                    Description = "Single-shot ki projectiles from the arms.",
                    Icon = DBPU.Icon_KiBlast,
                    Illustration = DBPU.Icon_KiBlast,
                    PowerType = CharacterPowers.PowerGroups.KiBlast,
                    IsActive = () => r.Blast.Active && !r.Blast.IsBarrage,
                    Disable = () => { if (!r.Blast.IsBarrage) r.Blast.Active = false; },
                    Apply = () =>
                    {
                        if (blockWhen != null && blockWhen()) return;
                        if (r.Blast.Active && !r.Blast.IsBarrage)
                        {
                            r.Blast.Active = false;
                            return;
                        }
                        r.Blast.IsBarrage = false;
                        r.Blast.Active = true;
                        onEnable?.Invoke();
                    }
                });
            }

            AddKiBlastBarrage(powers, r, blockWhen, onEnable);
        }

        static void AddKiBlastBarrage(CharacterPowers powers, CombatRefs r, Func<bool> blockWhen, Action onEnable)
        {
            if (powers.PowerData.ContainsKey("KiBlastBarrage")) return;

            powers.AddPower(new CharacterPowers.PowerStats
            {
                ID = "KiBlastBarrage",
                Name = "Ki Blast Barrage",
                Description = "Continuous rapid ki blasts from the arms.",
                Icon = DBPU.Icon_KiBlastBarrage,
                Illustration = DBPU.Icon_KiBlastBarrage,
                PowerType = CharacterPowers.PowerGroups.KiBlast,
                IsActive = () => r.Blast.Active && r.Blast.IsBarrage,
                Disable = () =>
                {
                    if (r.Blast.IsBarrage)
                    {
                        r.Blast.Active = false;
                        r.Blast.IsBarrage = false;
                    }
                },
                Apply = () =>
                {
                    if (blockWhen != null && blockWhen()) return;
                    if (r.Blast.Active && r.Blast.IsBarrage)
                    {
                        r.Blast.Active = false;
                        r.Blast.IsBarrage = false;
                        return;
                    }
                    r.Blast.IsBarrage = true;
                    r.Blast.Active = true;
                    onEnable?.Invoke();
                }
            });
        }

        public static void AddGokuStyleBeam(CharacterPowers powers, CombatRefs r, string id, string name, string effectId, Sprite icon)
        {
            powers.AddPower(new CharacterPowers.PowerStats
            {
                ID = id,
                Name = name,
                Description = "Beam attack.",
                Icon = icon,
                Illustration = icon,
                PowerType = CharacterPowers.PowerGroups.Beam,
                IsActive = () => r.Beam.EffectID == effectId && r.Beam.Active,
                Disable = () =>
                {
                    if (r.Beam.Active && r.Beam.EffectID == effectId)
                    {
                        r.Beam.Active = false;
                        r.Beam.CancelBeamState();
                    }
                },
                Apply = () => ToggleBeamPower(r.Beam, r.Blast, r.Solar, r.Shield, effectId)
            });
        }

        public static void AddLegacyBeam(CharacterPowers powers, CombatRefs r, string id, string name, string effectId, Func<bool> blockWhen, Action onBeamOn, Sprite icon = null)
        {
            icon = icon ?? DBPU.KiBlast_Sprite;
            powers.AddPower(new CharacterPowers.PowerStats
            {
                ID = id,
                Name = name,
                Description = "Beam attack.",
                Icon = icon,
                Illustration = icon,
                PowerType = CharacterPowers.PowerGroups.Beam,
                IsActive = () => r.Beam.EffectID == effectId && r.Beam.Active,
                Disable = () =>
                {
                    if (r.Beam.Active && r.Beam.EffectID == effectId)
                    {
                        r.Beam.Active = false;
                        r.Beam.CancelBeamState();
                    }
                },
                GetStatusText = () => (r.Beam.EffectID == effectId && r.Beam.Active) ? "Enabled" : "Disabled",
                Apply = () => ToggleLegacyBeam(r, effectId, blockWhen, onBeamOn)
            });
        }

        public static void AddLegacyBeamWithType(CharacterPowers powers, CombatRefs r, string id, string name, string effectId, string beamType, Func<bool> blockWhen, Action onBeamOn, Sprite icon = null)
        {
            AddLegacyBeam(powers, r, id, name, effectId, blockWhen, () =>
            {
                if (!string.IsNullOrEmpty(beamType))
                    r.Beam.BeamType = beamType;
                onBeamOn?.Invoke();
            }, icon);
        }

        public static void AddSolarFlare(CharacterPowers powers, CombatRefs r, Sprite icon = null)
        {
            icon = icon ?? DBPU.Icon_SolarFlare;
            powers.AddPower(new CharacterPowers.PowerStats
            {
                ID = "SolarFlare",
                Name = "Solar Flare",
                Description = "Blinds nearby targets.",
                Icon = icon,
                Illustration = icon,
                PowerType = CharacterPowers.PowerGroups.Solar,
                IsActive = () => r.Solar.Active,
                Disable = () => { r.Solar.Active = false; },
                Apply = () =>
                {
                    r.Solar.Active = !r.Solar.Active;
                    if (r.Solar.Active)
                    {
                        r.Beam.CancelBeamState();
                        r.Beam.Active = false;
                        r.Blast.Active = false;
                        if (r.Shield != null) r.Shield.Active = false;
                    }
                }
            });
        }

        public static void AddKiShield(CharacterPowers powers, CombatRefs r, Action onEnable, Sprite icon = null)
        {
            icon = icon ?? DBPU.Icon_KiShield;
            powers.AddPower(new CharacterPowers.PowerStats
            {
                ID = "KiShield",
                Name = "Ki Shield",
                Description = "Mobile ki barrier. Activate on the upper torso. Incoming attacks drain ki and integrity; stronger attacks can break through.",
                Icon = icon,
                Illustration = icon,
                PowerType = CharacterPowers.PowerGroups.Shield,
                IsActive = () => r.Shield.Active,
                Disable = () => { r.Shield.Active = false; },
                Apply = () =>
                {
                    r.Shield.Active = !r.Shield.Active;
                    if (r.Shield.Active)
                        onEnable?.Invoke();
                }
            });
        }

        public static void AddFinalExplosion(CharacterPowers powers, CombatRefs r, Sprite icon = null)
        {
            icon = icon ?? DBPU.Icon_FinalExplosion;
            powers.AddPower(new CharacterPowers.PowerStats
            {
                ID = "FinalExplosion",
                Name = "Final Explosion",
                Description = "Self-destruct attack.",
                Icon = icon,
                Illustration = icon,
                PowerType = CharacterPowers.PowerGroups.FinalExplosion,
                IsActive = () => r.FinalExplosion.Active,
                Disable = () => { r.FinalExplosion.Active = false; },
                Apply = () =>
                {
                    if (r.VegetaBlocked) return;
                    r.FinalExplosion.Active = !r.FinalExplosion.Active;
                    if (r.FinalExplosion.Active)
                    {
                        r.Blast.Active = false;
                        r.Beam.Active = false;
                        if (r.Shield != null) r.Shield.Active = false;
                    }
                }
            });
        }

        public static void AddHellzone(CharacterPowers powers, CombatRefs r, Sprite icon = null)
        {
            icon = icon ?? DBPU.Icon_HellzoneGranade;
            powers.AddPower(new CharacterPowers.PowerStats
            {
                ID = "HellzoneGranade",
                Name = "Hellzone Granade",
                Description = "Trap ki blasts in an area.",
                Icon = icon,
                Illustration = icon,
                PowerType = CharacterPowers.PowerGroups.Hellzone,
                IsActive = () => r.Hellzone.Active,
                Disable = () => { r.Hellzone.Active = false; },
                Apply = () =>
                {
                    if (r.BeamStillBlocked) return;
                    r.Hellzone.Active = !r.Hellzone.Active;
                    if (r.Hellzone.Active)
                    {
                        r.Beam.Active = false;
                        r.Blast.Active = false;
                        if (r.Shield != null) r.Shield.Active = false;
                    }
                }
            });
        }

        public static void AddBallPower(CharacterPowers powers, CombatRefs r, string id, string name, string effectId = null, Sprite icon = null)
        {
            icon = icon ?? DBPU.Icon_GiganticCrusher;
            powers.AddPower(new CharacterPowers.PowerStats
            {
                ID = id,
                Name = name,
                Description = "Large ki sphere attack.",
                Icon = icon,
                Illustration = icon,
                PowerType = CharacterPowers.PowerGroups.Ball,
                IsActive = () => r.Ball != null && r.Ball.Active && (string.IsNullOrEmpty(effectId) || r.Ball.EffectID == effectId),
                Disable = () => { if (r.Ball != null && (string.IsNullOrEmpty(effectId) || r.Ball.EffectID == effectId)) r.Ball.Active = false; },
                Apply = () =>
                {
                    if (r.Ball == null) return;
                    if (!string.IsNullOrEmpty(effectId))
                    {
                        if (r.Ball.EffectID == effectId && r.Ball.Active) { r.Ball.Active = false; return; }
                        r.Ball.EffectID = effectId;
                        r.Ball.Active = true;
                    }
                    else
                        r.Ball.Active = !r.Ball.Active;

                    if (r.Ball.Active)
                    {
                        if (r.Beam != null) { r.Beam.CancelBeamState(); r.Beam.Active = false; }
                        if (r.Shield != null) r.Shield.Active = false;
                        r.Blast.Active = false;
                        if (r.StraightBeam != null) r.StraightBeam.Active = false;
                    }
                }
            });
        }

        public static void AddStraightBeamPower(CharacterPowers powers, CombatRefs r, string id, string name, string effectId, Action onEnable, Sprite icon = null)
        {
            icon = icon ?? DBPU.Icon_DeathBeam;
            powers.AddPower(new CharacterPowers.PowerStats
            {
                ID = id,
                Name = name,
                Description = "Finger beam attack.",
                Icon = icon,
                Illustration = icon,
                PowerType = CharacterPowers.PowerGroups.StraightBeam,
                IsActive = () => r.StraightBeam != null && r.StraightBeam.Active,
                Disable = () => { if (r.StraightBeam != null) r.StraightBeam.Active = false; },
                Apply = () =>
                {
                    if (r.StraightBeam == null) return;
                    r.StraightBeam.Active = !r.StraightBeam.Active;
                    if (r.StraightBeam.Active)
                    {
                        if (!string.IsNullOrEmpty(effectId)) r.StraightBeam.EffectID = effectId;
                        onEnable?.Invoke();
                    }
                }
            });
        }

        public static void AddInstantTransmissionMenu(CharacterPowers powers, GameObject instance, string name = null, string desc = null, Sprite icon = null)
        {
            if (powers == null || instance == null || HasPowerEntry(powers, "InstantTransmission")) return;
            icon = icon ?? DBPU.Icon_InstantTransmission;

            var it = instance.GetComponent<InstantTransmission>();
            if (it == null) it = instance.AddComponent<InstantTransmission>();

            powers.InsertPowerAtStart(new CharacterPowers.PowerStats
            {
                ID = "InstantTransmission",
                Name = name ?? "Instant Transmission",
                Description = desc ?? "Teleport short distances.",
                Icon = icon,
                Illustration = icon,
                PowerType = "Movement",

                IsActive = () => it.Active,

                Apply = () =>
                {
                    it.Active = !it.Active;
                },

                Disable = () =>
                {
                    it.Active = false;
                    it.CanUse = false;
                }
            });
        }

        public static void AddKaiokenMenu(CharacterPowers powers, GameObject instance, PersonBehaviour person, string name = null, string desc = null, Sprite icon = null)
        {
            if (powers == null || instance == null || HasPowerEntry(powers, "Kaioken")) return;
            icon = icon ?? DBPU.Icon_Kaioken;

            var kaioken = instance.GetComponent<Kaioken>();
            if (kaioken == null) kaioken = instance.AddComponent<Kaioken>();

            if (instance.GetComponent<AuraHandler>() == null)
            {
                var auraHandler = instance.AddComponent<AuraHandler>();
                auraHandler.person = person;
            }

            powers.InsertPowerAtStart(new CharacterPowers.PowerStats
            {
                ID = "Kaioken",
                Name = name ?? "Kaioken",
                Description = desc ?? "Power boost with strain buildup. Toggle from the powers menu or press K.",
                Icon = icon,
                Illustration = icon,
                PowerType = "Kaioken",
                IsActive = () => kaioken.Active,
                Apply = () => kaioken.ToggleFromPowersMenu(),
                Disable = () => { if (kaioken.Active) kaioken.ToggleFromPowersMenu(); }
            });
        }

        public static void DisableGogetaSpecials(CombatRefs r)
        {
            if (r.StardustFall != null) r.StardustFall.Active = false;
            if (r.MeteorExplosion != null) r.MeteorExplosion.Active = false;
            if (r.GodPunisher != null) r.GodPunisher.Active = false;
            if (r.StardustBreaker != null) r.StardustBreaker.Active = false;
        }

        public static void DisableBeerusSpecials(CombatRefs r)
        {
            if (r.GodsWrath != null) r.GodsWrath.Active = false;
            if (r.CataclysmicOrb != null) r.CataclysmicOrb.Active = false;
        }

        public static void AddBeerusSpecialToggle(CharacterPowers powers, CombatRefs r, string id, string name, string description, Component comp, Sprite icon = null)
        {
            if (comp is GodsWrathBehaviour godsWrath)
            {
                icon = icon ?? DBPU.Icon_GodsWrath;
                powers.AddPower(new CharacterPowers.PowerStats
                {
                    ID = id,
                    Name = name,
                    Description = description,
                    Icon = icon,
                    Illustration = icon,
                    PowerType = CharacterPowers.PowerGroups.BeerusSpecial,
                    IsActive = () => godsWrath.Active,
                    Disable = () => { godsWrath.Active = false; },
                    Apply = () =>
                    {
                        godsWrath.Active = !godsWrath.Active;
                        if (godsWrath.Active)
                        {
                            r.Blast.Active = false;
                            if (r.Beam != null) { r.Beam.CancelBeamState(); r.Beam.Active = false; }
                            if (r.Ball != null) r.Ball.Active = false;
                            if (r.CataclysmicOrb != null) r.CataclysmicOrb.Active = false;
                        }
                    }
                });
            }
            else if (comp is CataclysmicOrbBehaviour cataclysmicOrb)
            {
                icon = icon ?? DBPU.Icon_CataclysmicOrb;
                powers.AddPower(new CharacterPowers.PowerStats
                {
                    ID = id,
                    Name = name,
                    Description = description,
                    Icon = icon,
                    Illustration = icon,
                    PowerType = CharacterPowers.PowerGroups.BeerusSpecial,
                    IsActive = () => cataclysmicOrb.Active,
                    Disable = () => { cataclysmicOrb.Active = false; },
                    Apply = () =>
                    {
                        cataclysmicOrb.Active = !cataclysmicOrb.Active;
                        if (cataclysmicOrb.Active)
                        {
                            r.Blast.Active = false;
                            if (r.Beam != null) { r.Beam.CancelBeamState(); r.Beam.Active = false; }
                            if (r.Ball != null) r.Ball.Active = false;
                            if (r.GodsWrath != null) r.GodsWrath.Active = false;
                        }
                    }
                });
            }
        }

        public static void AddGogetaSpecialToggle(CharacterPowers powers, CombatRefs r, string id, string name, Component comp, Sprite icon = null)
        {
            icon = icon ?? DBPU.Icon_StardustFall;
            if (comp is StardustFall sf)
            {
                powers.AddPower(new CharacterPowers.PowerStats
                {
                    ID = id,
                    Name = name,
                    Description = "Gogeta special technique.",
                    Icon = icon,
                    Illustration = icon,
                    PowerType = CharacterPowers.PowerGroups.GogetaSpecial,
                    IsActive = () => sf.Active,
                    Disable = () => { sf.Active = false; },
                    Apply = () =>
                    {
                        sf.Active = !sf.Active;
                        if (sf.Active)
                        {
                            r.Blast.Active = false;
                            r.Beam.Active = false;
                            if (r.MeteorExplosion != null) r.MeteorExplosion.Active = false;
                            if (r.GodPunisher != null) r.GodPunisher.Active = false;
                            if (r.StardustBreaker != null) r.StardustBreaker.Active = false;
                        }
                    }
                });
            }
            else if (comp is MeteorExplosion me)
            {
                powers.AddPower(new CharacterPowers.PowerStats
                {
                    ID = id,
                    Name = name,
                    Description = "Gogeta special technique.",
                    Icon = icon,
                    Illustration = icon,
                    PowerType = CharacterPowers.PowerGroups.GogetaSpecial,
                    IsActive = () => me.Active,
                    Disable = () => { me.Active = false; },
                    Apply = () =>
                    {
                        me.Active = !me.Active;
                        if (me.Active)
                        {
                            r.Blast.Active = false;
                            r.Beam.Active = false;
                            if (r.StardustFall != null) r.StardustFall.Active = false;
                            if (r.GodPunisher != null) r.GodPunisher.Active = false;
                            if (r.StardustBreaker != null) r.StardustBreaker.Active = false;
                        }
                    }
                });
            }
            else if (comp is GodPunisher gp)
            {
                powers.AddPower(new CharacterPowers.PowerStats
                {
                    ID = id,
                    Name = name,
                    Description = "Gogeta special technique.",
                    Icon = icon,
                    Illustration = icon,
                    PowerType = CharacterPowers.PowerGroups.GogetaSpecial,
                    IsActive = () => gp.Active,
                    Disable = () => { gp.Active = false; },
                    Apply = () =>
                    {
                        gp.Active = !gp.Active;
                        if (gp.Active)
                        {
                            r.Blast.Active = false;
                            r.Beam.Active = false;
                            if (r.StardustFall != null) r.StardustFall.Active = false;
                            if (r.MeteorExplosion != null) r.MeteorExplosion.Active = false;
                            if (r.StardustBreaker != null) r.StardustBreaker.Active = false;
                        }
                    }
                });
            }
            else if (comp is StardustBreaker sb)
            {
                powers.AddPower(new CharacterPowers.PowerStats
                {
                    ID = id,
                    Name = name,
                    Description = "Gogeta special technique.",
                    Icon = icon,
                    Illustration = icon,
                    PowerType = CharacterPowers.PowerGroups.GogetaSpecial,
                    IsActive = () => sb.Active,
                    Disable = () => { sb.Active = false; },
                    Apply = () =>
                    {
                        sb.Active = !sb.Active;
                        if (sb.Active)
                        {
                            r.Blast.Active = false;
                            r.Beam.Active = false;
                            if (r.StardustFall != null) r.StardustFall.Active = false;
                            if (r.MeteorExplosion != null) r.MeteorExplosion.Active = false;
                            if (r.GodPunisher != null) r.GodPunisher.Active = false;
                        }
                    }
                });
            }
        }

        public static Action VegetaKiOnEnable(CombatRefs r) => () =>
        {
            r.Beam.Active = false;
            r.FinalExplosion.Active = false;
            if (r.Shield != null) r.Shield.Active = false;
        };

        public static Action VegetaBeamOnEnable(CombatRefs r) => () =>
        {
            r.Blast.Active = false;
            r.FinalExplosion.Active = false;
            if (r.Shield != null) r.Shield.Active = false;
        };

        public static Action StandardBeamOnEnable(CombatRefs r, bool disableHellzone = false) => () =>
        {
            r.Blast.Active = false;
            if (r.Shield != null) r.Shield.Active = false;
            if (disableHellzone && r.Hellzone != null) r.Hellzone.Active = false;
        };

        public static PowerCatalog.Overrides BeamBlock(PowerSetup.CombatRefs r, Action onEnable = null)
        {
            return new PowerCatalog.Overrides
            {
                BlockWhen = () => r.BeamStillBlocked,
                OnEnable = onEnable != null ? onEnable : (Action)(() => { r.Blast.Active = false; })
            };
        }

        public static PowerCatalog.Overrides GokuBeamStyle() =>
            new PowerCatalog.Overrides { GokuStyleBeam = true };

        static PowerCatalog.Overrides GokuBeam() => GokuBeamStyle();

        public static void RegisterGokuCombatPowers(PowerBuilder pw)
        {
            var r = pw.Refs;
            pw.Grant("KiBlast", new PowerCatalog.Overrides
            {
                BlockWhen = () => r.Beam.StillActive,
                OnEnable = () => { r.Beam.CancelBeamState(); r.Beam.Active = false; r.Solar.Active = false; r.Shield.Active = false; }
            });
            pw.GrantMany(GokuBeamStyle(), "EnergyWave", "Kamehameha");
            if (r.Solar != null) pw.Grant("SolarFlare");
            if (r.Shield != null) pw.Grant("KiShield", new PowerCatalog.Overrides
            {
                OnEnable = () => { r.Beam.CancelBeamState(); r.Beam.Active = false; r.Blast.Active = false; if (r.Solar != null) r.Solar.Active = false; }
            });
            pw.Grant("SpiritBomb");
            pw.Grant("SuperSpiritBomb");
        }

        public static void RegisterVegetaCombatPowers(CharacterPowers powers, GameObject instance)
        {
            var person = instance.GetComponent<PersonBehaviour>();
            var r = CombatRefs.From(instance);
            var beam = new PowerCatalog.Overrides { BlockWhen = () => r.VegetaBlocked, OnEnable = VegetaBeamOnEnable(r) };
            PowerCatalog.Add(powers, instance, person, "KiBlast", new PowerCatalog.Overrides { BlockWhen = () => r.VegetaBlocked, OnEnable = VegetaKiOnEnable(r) });
            PowerCatalog.AddMany(powers, instance, person, beam, "EnergyWave", "GalickGun", "FinalFlash");
            PowerCatalog.Add(powers, instance, person, "FinalExplosion");
            PowerCatalog.Add(powers, instance, person, "KiShield", new PowerCatalog.Overrides
            {
                OnEnable = () => { r.Beam.Active = false; r.Blast.Active = false; r.FinalExplosion.Active = false; }
            });
        }

        public static void RegisterGohanCombatPowers(CharacterPowers powers, GameObject instance)
        {
            var person = instance.GetComponent<PersonBehaviour>();
            var r = CombatRefs.From(instance);
            var std = BeamBlock(r, StandardBeamOnEnable(r, true));
            PowerCatalog.Add(powers, instance, person, "KiBlast", new PowerCatalog.Overrides
            {
                BlockWhen = () => r.BeamStillBlocked,
                OnEnable = () => { r.Beam.Active = false; if (r.Hellzone != null) r.Hellzone.Active = false; if (r.Shield != null) r.Shield.Active = false; }
            });
            PowerCatalog.AddMany(powers, instance, person, std, "EnergyWave", "Kamehameha");
            if (r.Hellzone != null) PowerCatalog.Add(powers, instance, person, "HellzoneGranade");
            if (r.Shield != null) PowerCatalog.Add(powers, instance, person, "KiShield", new PowerCatalog.Overrides
            {
                OnEnable = () => { r.Beam.Active = false; r.Blast.Active = false; if (r.Hellzone != null) r.Hellzone.Active = false; }
            });
        }

        public static void RegisterKrillinCombatPowers(CharacterPowers powers, GameObject instance)
        {
            var person = instance.GetComponent<PersonBehaviour>();
            var r = CombatRefs.From(instance);
            PowerCatalog.Add(powers, instance, person, "KiBlast", new PowerCatalog.Overrides
            {
                BlockWhen = () => r.Beam.StillActive,
                OnEnable = () => { r.Beam.CancelBeamState(); r.Beam.Active = false; if (r.Solar != null) r.Solar.Active = false; if (r.Shield != null) r.Shield.Active = false; }
            });
            PowerCatalog.AddMany(powers, instance, person, GokuBeam(), "EnergyWave", "Kamehameha");
            if (r.Solar != null) PowerCatalog.Add(powers, instance, person, "SolarFlare");
            if (r.Shield != null) PowerCatalog.Add(powers, instance, person, "KiShield", new PowerCatalog.Overrides
            {
                OnEnable = () => { r.Beam.CancelBeamState(); r.Beam.Active = false; r.Blast.Active = false; if (r.Solar != null) r.Solar.Active = false; }
            });
        }

        public static void RegisterBasicBeamCombatPowers(CharacterPowers powers, GameObject instance)
        {
            var person = instance.GetComponent<PersonBehaviour>();
            var r = CombatRefs.From(instance);
            var basic = BeamBlock(r, () => { r.Beam.Active = false; });
            PowerCatalog.Add(powers, instance, person, "KiBlast", basic);
            PowerCatalog.AddMany(powers, instance, person, BeamBlock(r), "EnergyWave", "Kamehameha");
        }

        public static void RegisterTenshinhanCombatPowers(CharacterPowers powers, GameObject instance)
        {
            RegisterBasicBeamCombatPowers(powers, instance);
            var person = instance.GetComponent<PersonBehaviour>();
            if (CombatRefs.From(instance).Solar != null) PowerCatalog.Add(powers, instance, person, "SolarFlare");
        }

        public static void RegisterPiccoloCombatPowers(CharacterPowers powers, GameObject instance)
        {
            var person = instance.GetComponent<PersonBehaviour>();
            var r = CombatRefs.From(instance);
            var std = BeamBlock(r, StandardBeamOnEnable(r, true));
            PowerCatalog.Add(powers, instance, person, "KiBlast", new PowerCatalog.Overrides
            {
                BlockWhen = () => r.BeamStillBlocked,
                OnEnable = () => { r.Beam.Active = false; if (r.Hellzone != null) r.Hellzone.Active = false; if (r.Shield != null) r.Shield.Active = false; }
            });
            PowerCatalog.AddMany(powers, instance, person, std, "EnergyWave", "SpecialBeamCannon");
            if (r.Hellzone != null) PowerCatalog.Add(powers, instance, person, "HellzoneGranade");
            if (r.Shield != null) PowerCatalog.Add(powers, instance, person, "KiShield", new PowerCatalog.Overrides
            {
                OnEnable = () => { r.Beam.Active = false; r.Blast.Active = false; if (r.Hellzone != null) r.Hellzone.Active = false; }
            });
        }

        public static void RegisterAndroidCombatPowers(CharacterPowers powers, GameObject instance)
        {
            var person = instance.GetComponent<PersonBehaviour>();
            var r = CombatRefs.From(instance);
            PowerCatalog.Add(powers, instance, person, "KiBlast", new PowerCatalog.Overrides
            {
                BlockWhen = () => r.BeamStillBlocked,
                OnEnable = () => { r.Beam.Active = false; if (r.Shield != null) r.Shield.Active = false; }
            });
            PowerCatalog.Add(powers, instance, person, "EnergyWave", new PowerCatalog.Overrides
            {
                BlockWhen = () => r.BeamStillBlocked,
                OnEnable = () => { r.Blast.Active = false; if (r.Shield != null) r.Shield.Active = false; }
            });
            if (r.Shield != null) PowerCatalog.Add(powers, instance, person, "KiShield", new PowerCatalog.Overrides
            {
                OnEnable = () => { r.Beam.Active = false; r.Blast.Active = false; }
            });
        }

        public static void RegisterBrolyCombatPowers(CharacterPowers powers, GameObject instance)
        {
            var person = instance.GetComponent<PersonBehaviour>();
            var r = CombatRefs.From(instance);
            Action brolyBeamOff = () => { r.Blast.Active = false; if (r.Shield != null) r.Shield.Active = false; };
            PowerCatalog.Add(powers, instance, person, "KiBlast", new PowerCatalog.Overrides
            {
                OnEnable = () => { r.Beam.Active = false; if (r.Shield != null) r.Shield.Active = false; }
            });
            PowerCatalog.Add(powers, instance, person, "EnergyWave", new PowerCatalog.Overrides { BlockWhen = () => r.BeamStillBlocked, OnEnable = brolyBeamOff, BeamType = "ArmBeam" });
            PowerCatalog.AddMany(powers, instance, person, BeamBlock(r, brolyBeamOff), "GiganticFullBlast", "GiganticRoar");
            if (r.Ball != null) PowerCatalog.Add(powers, instance, person, "GiganticCrusher");
            if (r.Shield != null) PowerCatalog.Add(powers, instance, person, "KiShield", new PowerCatalog.Overrides
            {
                OnEnable = () => { r.Beam.Active = false; r.Blast.Active = false; }
            });
        }

        public static void RegisterGogetaCombatPowers(CharacterPowers powers, GameObject instance)
        {
            var person = instance.GetComponent<PersonBehaviour>();
            var r = CombatRefs.From(instance);
            Action gogetaOff = () => { r.Blast.Active = false; DisableGogetaSpecials(r); };
            var gogetaBeam = BeamBlock(r, gogetaOff);
            PowerCatalog.Add(powers, instance, person, "KiBlast", new PowerCatalog.Overrides
            {
                BlockWhen = () => r.BeamStillBlocked,
                OnEnable = () => { r.Beam.Active = false; DisableGogetaSpecials(r); }
            });
            PowerCatalog.AddMany(powers, instance, person, gogetaBeam, "EnergyWave", "Kamehameha", "FullForceKamehameha");
            if (r.StardustFall != null) PowerCatalog.Add(powers, instance, person, "StardustFall");
            if (r.MeteorExplosion != null) PowerCatalog.Add(powers, instance, person, "MeteorExplosion");
            if (r.GodPunisher != null) PowerCatalog.Add(powers, instance, person, "GodPunisher");
            if (r.StardustBreaker != null) PowerCatalog.Add(powers, instance, person, "StardustBreaker");
        }

        public static void RegisterBeerusCombatPowers(CharacterPowers powers, GameObject instance)
        {
            var person = instance.GetComponent<PersonBehaviour>();
            var r = CombatRefs.From(instance);

            PowerCatalog.Add(powers, instance, person, "WrathOfTheGod", new PowerCatalog.Overrides
            {
                OnEnable = () =>
                {
                    r.Blast.Active = false;
                    if (r.Ball != null) r.Ball.Active = false;
                    DisableBeerusSpecials(r);
                }
            });
            PowerCatalog.Add(powers, instance, person, "BeerusBall", new PowerCatalog.Overrides
            {
                OnEnable = () =>
                {
                    r.Blast.Active = false;
                    if (r.Beam != null) { r.Beam.CancelBeamState(); r.Beam.Active = false; }
                    DisableBeerusSpecials(r);
                }
            });
            PowerCatalog.Add(powers, instance, person, "GodsWrath", new PowerCatalog.Overrides
            {
                OnEnable = () =>
                {
                    if (r.Beam != null) { r.Beam.CancelBeamState(); r.Beam.Active = false; }
                    if (r.Ball != null) r.Ball.Active = false;
                    if (r.CataclysmicOrb != null) r.CataclysmicOrb.Active = false;
                }
            });
            PowerCatalog.Add(powers, instance, person, "CataclysmicOrb", new PowerCatalog.Overrides
            {
                OnEnable = () =>
                {
                    if (r.Beam != null) { r.Beam.CancelBeamState(); r.Beam.Active = false; }
                    if (r.Ball != null) r.Ball.Active = false;
                    if (r.GodsWrath != null) r.GodsWrath.Active = false;
                }
            });
        }

        public enum BeerusCombatKeep
        {
            None,
            Beam,
            Ball,
            GodsWrath,
            CataclysmicOrb
        }

        public static Action BeerusDisableOthers(CombatRefs r, BeerusCombatKeep keep = BeerusCombatKeep.None)
        {
            return () =>
            {
                r.Blast.Active = false;
                if (keep != BeerusCombatKeep.Beam && r.Beam != null) { r.Beam.CancelBeamState(); r.Beam.Active = false; }
                if (keep != BeerusCombatKeep.Ball && r.Ball != null) r.Ball.Active = false;
                if (keep != BeerusCombatKeep.GodsWrath && r.GodsWrath != null) r.GodsWrath.Active = false;
                if (keep != BeerusCombatKeep.CataclysmicOrb && r.CataclysmicOrb != null) r.CataclysmicOrb.Active = false;
            };
        }

        public enum FriezaCombatKeep
        {
            None,
            Blast,
            Beam,
            StraightBeam,
            Ball,
            Shield
        }

        public static Action FriezaDisableOthers(CombatRefs r, bool includeStraightBeam = true, FriezaCombatKeep keep = FriezaCombatKeep.None)
        {
            return () =>
            {
                if (keep != FriezaCombatKeep.Blast) r.Blast.Active = false;
                if (keep != FriezaCombatKeep.Beam && r.Beam != null) { r.Beam.CancelBeamState(); r.Beam.Active = false; }
                if (keep != FriezaCombatKeep.Shield && r.Shield != null) r.Shield.Active = false;
                if (keep != FriezaCombatKeep.StraightBeam && includeStraightBeam && r.StraightBeam != null) r.StraightBeam.Active = false;
                if (keep != FriezaCombatKeep.Ball && r.Ball != null) r.Ball.Active = false;
            };
        }

        public static void EnsureKiCombatStack(GameObject instance)
        {
            if (instance == null) return;
            if (instance.GetComponent<BlastBehaviour>() == null)
                instance.AddComponent<BlastBehaviour>();
            var beam = instance.GetComponent<BeamBehaviour>();
            if (beam == null) beam = instance.AddComponent<BeamBehaviour>();
            ScheduleBeamTriggers(beam);
        }

        public static void EnsureFriezaCombatComponents(GameObject instance, string characterName = "Frieza")
        {
            if (instance == null) return;
            EnsureKiCombatStack(instance);

            var ball = instance.GetComponent<BallBehaviour>() ?? instance.AddComponent<BallBehaviour>();
            ball.EffectID = "Death Ball";
            ball.Character = characterName;

            var straight = instance.GetComponent<StraightBeamBehaviour>() ?? instance.AddComponent<StraightBeamBehaviour>();
            straight.EffectID = "Death Beam";

            var beam = instance.GetComponent<BeamBehaviour>();
            if (beam != null)
            {
                beam.EffectID = "Last Emperor";
                beam.Character = characterName;
            }

            if (instance.GetComponent<KiShield>() == null)
                instance.AddComponent<KiShield>();
        }

        public static void RegisterFriezaCombatPowers(PowerBuilder pw)
        {
            var r = pw.Refs;

            pw.Grant("KiBlast", new PowerCatalog.Overrides
            {
                BlockWhen = () => r.Beam != null && r.Beam.StillActive,
                OnEnable = FriezaDisableOthers(r, true, FriezaCombatKeep.Blast)
            });
            pw.Grant("DeathBeam", new PowerCatalog.Overrides { OnEnable = FriezaDisableOthers(r, false, FriezaCombatKeep.StraightBeam) });
            pw.Grant("DeathBall", new PowerCatalog.Overrides { OnEnable = FriezaDisableOthers(r, true, FriezaCombatKeep.Ball) });
            pw.Grant("Supernova", new PowerCatalog.Overrides { OnEnable = FriezaDisableOthers(r, true, FriezaCombatKeep.Ball) });
            pw.Grant("LastEmperor", new PowerCatalog.Overrides
            {
                BlockWhen = () => r.Beam != null && r.Beam.StillActive,
                OnEnable = FriezaDisableOthers(r, true, FriezaCombatKeep.Beam)
            });
            if (r.Shield != null) pw.Grant("KiShield", new PowerCatalog.Overrides { OnEnable = FriezaDisableOthers(r, true, FriezaCombatKeep.Shield) });
        }

        public static void RegisterSigmaCombatPowers(PowerBuilder pw)
        {
            var r = pw.Refs;
            Action beamOff = () => r.Beam.Active = false;
            pw.Grant("KiBlast", BeamBlock(r, beamOff));
            pw.GrantMany(BeamBlock(r), "EnergyWave");
            pw.Grant("Kamehameha", new PowerCatalog.Overrides
            {
                Name = "Sigmahameha",
                BlockWhen = () => r.BeamStillBlocked,
                OnEnable = () => { r.Blast.Active = false; if (r.Shield != null) r.Shield.Active = false; }
            });
        }

        public static CharacterPowers EnsurePowerMenu(GameObject root, PersonBehaviour person)
        {
            if (root.GetComponent<KiSystem>() == null)
                root.AddComponent<KiSystem>();
            if (root.GetComponent<MultipliersAndStuffs>() == null)
                root.AddComponent<MultipliersAndStuffs>();
            EnsureKiCombatStack(root);

            var cp = root.GetComponent<CharacterPowers>();
            if (cp == null) cp = root.AddComponent<CharacterPowers>();

            GameObject headGo = person.transform.Find("Head")?.gameObject;
            if (headGo == null)
            {
                foreach (var limb in person.Limbs)
                {
                    if (limb.name.IndexOf("Head", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        headGo = limb.gameObject;
                        break;
                    }
                }
            }
            if (headGo == null && person.Limbs.Length > 2)
                headGo = person.Limbs[2].gameObject;

            if (headGo != null)
            {
                var pcm = headGo.GetComponent<PowerContextMenu>();
                if (pcm == null)
                    pcm = headGo.AddComponent<PowerContextMenu>();
                pcm.PowersData = cp;
                pcm.person = person;
            }

            return cp;
        }

        public static void EnsureModFighterDurability(PersonBehaviour person)
        {
            if (person == null) return;
            if (person.GetComponent<ModPowerDurabilityApplied>() != null) return;

            const float healthScale = 5000000000000000f;
            bool needsHealthBoost = MCP_Utils.AverageMaxHealth(person) < 100000f;

            foreach (var limb in person.Limbs)
            {
                if (limb == null) continue;

                if (needsHealthBoost)
                {
                    limb.Health *= healthScale * DBPU.HealthMultiplier;
                    limb.InitialHealth *= healthScale * DBPU.HealthMultiplier;
                    limb.BreakingThreshold *= healthScale;

                    //limb.SkinMaterialHandler.intensityMultiplier = 0.01f;
                    limb.GForcePassoutThreshold *= 100f;
                    limb.ImpactPainMultiplier = 0f;
                    if (limb.ImpactDamageMultiplier > 0.0001f)
                        limb.ImpactDamageMultiplier /= 10000f;
                    limb.ShotDamageMultiplier = 0f;

                    limb.FreezingTemperature = -300f;
                    limb.DiscomfortingHeatTemperature = 2000f;
                    limb.SkinMaterialHandler.ShouldRecolourWhenCold = false;
                    if (limb.SkinMaterialHandler != null)
                        limb.SkinMaterialHandler.intensityMultiplier = Mathf.Min(limb.SkinMaterialHandler.intensityMultiplier, 0.01f);
                }

                /*var phys = limb.GetComponent<PhysicalBehaviour>();
                if (phys != null)
                    phys.Properties = PhysProperties.GodlyLimbProperties;*/
            }

            if (needsHealthBoost)
                person.ChangeWeightBasedOnHealth();

            ApplyModFighterHumanFeatures(person);

            var ki = person.GetComponent<KiSystem>();
            if (ki != null)
            {
                if (ki.maxKi < 1000000f) ki.maxKi = 1000000f;
                if (ki.baseRegen < 10000f) ki.baseRegen = 10000f;
                if (ki.currentKi < ki.maxKi) ki.currentKi = ki.maxKi;
            }

            person.gameObject.AddComponent<ModPowerDurabilityApplied>();
        }

        static void ApplyModFighterHumanFeatures(PersonBehaviour person)
        {
            GameObject headGo = person.transform.Find("Head")?.gameObject;
            if (headGo == null)
            {
                foreach (var limb in person.Limbs)
                {
                    if (limb.name.IndexOf("Head", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        headGo = limb.gameObject;
                        break;
                    }
                }
            }
            if (headGo == null && person.Limbs.Length > 2)
                headGo = person.Limbs[2].gameObject;
            if (headGo == null) return;

            var hc = headGo.GetComponent<HumanController>();
            if (hc == null)
            {
                hc = headGo.AddComponent<HumanController>();
                hc.person = person;
            }
            else if (hc.person == null)
                hc.person = person;

            hc.Features.ImmuneToKnockout = true;
            hc.Features.ImmuneToPain = true;
            hc.Features.ImmuneToOxigenLoss = true;
            hc.Features.ImmuneToBleeding = true;
            hc.Features.ImmuneToInternalBleeding = true;
            hc.Features.ImmuneToNumbness = true;
            hc.Features.ImmuneToFire = true;
            hc.Features.ImmuneToAcid = true;
            hc.Features.ImmuneToElectricity = true;
            hc.Features.ImmuneToStabbing = true;
            hc.Features.ImmuneToBullets = true;
            hc.Features.ImmuneToBrainDamage = true;
        }

        public static bool HasPowerEntry(CharacterPowers powers, string id) =>
            powers != null && powers.PowerData.ContainsKey(id);

        public static void ScheduleBeamTriggers(BeamBehaviour beam)
        {
            if (beam == null) return;
            CeceUtils.DelayedInvoke(0.15f, () => { if (beam != null) beam.RegisterArmBeamTriggers(); });
        }

        public static void GrantKiBlast(CharacterPowers powers, GameObject instance) =>
            PowerCatalog.Add(powers, instance, instance.GetComponent<PersonBehaviour>(), "KiBlast");

        public static void GrantKiBlastBarrage(CharacterPowers powers, GameObject instance) =>
            GrantKiBlast(powers, instance);

        public static void GrantLegacyBeam(CharacterPowers powers, GameObject instance, string id, string name, string effectId, Sprite icon = null, string beamType = null) =>
            PowerCatalog.Add(powers, instance, instance.GetComponent<PersonBehaviour>(), id, new PowerCatalog.Overrides { Name = name, EffectId = effectId, Icon = icon, BeamType = beamType });

        public static void GrantSolarFlare(CharacterPowers powers, GameObject instance, string character = "Goku") =>
            PowerCatalog.Add(powers, instance, instance.GetComponent<PersonBehaviour>(), "SolarFlare", new PowerCatalog.Overrides { SolarCharacter = character });

        public static void GrantBeerusPower(CharacterPowers powers, GameObject instance, string id) =>
            PowerCatalog.Add(powers, instance, instance.GetComponent<PersonBehaviour>(), id);

        public static void GrantKiShield(CharacterPowers powers, GameObject instance) =>
            PowerCatalog.Add(powers, instance, instance.GetComponent<PersonBehaviour>(), "KiShield");

        public static void GrantFinalExplosion(CharacterPowers powers, GameObject instance) =>
            PowerCatalog.Add(powers, instance, instance.GetComponent<PersonBehaviour>(), "FinalExplosion");

        public static void GrantHellzone(CharacterPowers powers, GameObject instance) =>
            PowerCatalog.Add(powers, instance, instance.GetComponent<PersonBehaviour>(), "HellzoneGranade");

        public static void GrantBallPower(CharacterPowers powers, GameObject instance, string id, string name, string effectId) =>
            PowerCatalog.Add(powers, instance, instance.GetComponent<PersonBehaviour>(), id, new PowerCatalog.Overrides { Name = name, BallEffectId = effectId });

        public static void GrantStraightBeam(CharacterPowers powers, GameObject instance, string id, string name, string effectId) =>
            PowerCatalog.Add(powers, instance, instance.GetComponent<PersonBehaviour>(), id, new PowerCatalog.Overrides { Name = name, EffectId = effectId });

        public static void GrantGogetaSpecial(CharacterPowers powers, GameObject instance, string id, string name, string componentType) =>
            PowerCatalog.Add(powers, instance, instance.GetComponent<PersonBehaviour>(), id, new PowerCatalog.Overrides { Name = name, GogetaComponentType = componentType });

        public static void GrantPunchPower(PersonBehaviour person, CharacterPowers powers) =>
            PowerCatalog.Add(powers, person.gameObject, person, "PunchPower");

        public static void GrantKickPower(PersonBehaviour person, CharacterPowers powers) =>
            PowerCatalog.Add(powers, person.gameObject, person, "KickPower");

        public static void GrantBarrage(PersonBehaviour person, CharacterPowers powers) =>
            PowerCatalog.Add(powers, person.gameObject, person, "Barrage");

        public static void GrantFlight(PersonBehaviour person, CharacterPowers powers) =>
            PowerCatalog.Add(powers, person.gameObject, person, "Flight");

        public static void GrantKaioken(PersonBehaviour person, CharacterPowers powers) =>
            PowerCatalog.Add(powers, person.gameObject, person, "Kaioken");

        public static void GrantInstantTransmission(PersonBehaviour person, CharacterPowers powers)
        {
            if (person == null || powers == null) return;

            PowerCatalog.Add(powers, person.gameObject, person, "InstantTransmission");
        }

        public static void AddPiccoloGrowthPower(CharacterPowers powers, PiccoloGiantForm giantForm, Sprite icon = null)
        {
            icon = icon ?? DBPU.Icon_GreatNamekianGrowth;
            if (powers == null || giantForm == null || HasPowerEntry(powers, "GreatNamekianGrowth")) return;

            powers.InsertPowerAtStart(new CharacterPowers.PowerStats
            {
                ID = "GreatNamekianGrowth",
                Name = "Great Namekian Growth",
                Description = "Giant Namekian transformation.",
                Icon = icon,
                Illustration = icon,
                PowerType = CharacterPowers.PowerGroups.Growth,
                IsActive = () => giantForm.Active,
                Disable = () => { giantForm.Active = false; giantForm.Growing = false; giantForm.NotifyStateChanged(); },
                Apply = () =>
                {
                    giantForm.Active = !giantForm.Active;
                    if (!giantForm.Active) giantForm.Growing = false;
                    giantForm.NotifyStateChanged();
                }
            });
        }

        public static void GrantPiccoloGrowth(PersonBehaviour person, CharacterPowers powers)
        {
            if (person == null) return;
            var giantForm = person.GetComponent<PiccoloGiantForm>();
            if (giantForm == null) giantForm = person.gameObject.AddComponent<PiccoloGiantForm>();
            AddPiccoloGrowthPower(powers, giantForm);
        }

        public static void AddDisasterRayPower(CharacterPowers powers, CellMaxDisasterRayBehaviour disasterRay, CombatRefs r, Sprite icon = null)
        {
            icon = icon ?? DBPU.Icon_DisasterRay ?? DBPU.Icon_DeathBeam;
            if (powers == null || disasterRay == null || HasPowerEntry(powers, "DisasterRay")) return;

            powers.InsertPowerAtStart(new CharacterPowers.PowerStats
            {
                ID = "DisasterRay",
                Name = "Disaster Ray",
                Description = "Black armor spots glow purple and emit destructive lasers in all directions. Activate the head while enabled to unleash.",
                Icon = icon,
                Illustration = icon,
                PowerType = CharacterPowers.PowerGroups.DisasterRay,
                IsActive = () => disasterRay.Active,
                Disable = () => disasterRay.SetActive(false),
                Apply = () =>
                {
                    disasterRay.SetActive(!disasterRay.Active);
                    if (disasterRay.Active && r != null)
                    {
                        if (r.Beam != null) r.Beam.Active = false;
                        r.Blast.Active = false;
                        if (r.Shield != null) r.Shield.Active = false;
                    }
                }
            });
        }

        public static void AddEyeLaserPower(CharacterPowers powers, CellMaxEyeLaserBehaviour eyeLaser, CombatRefs r, Sprite icon = null, string description = null)
        {
            icon = icon ?? DBPU.Icon_EyeLaser ?? DBPU.Icon_DisasterRay ?? DBPU.Icon_DeathBeam;
            if (powers == null || eyeLaser == null || HasPowerEntry(powers, "EyeLaser")) return;

            powers.InsertPowerAtStart(new CharacterPowers.PowerStats
            {
                ID = "EyeLaser",
                Name = "Eye Laser",
                Description = description ?? "Fires straight purple lasers from the eyes. Activate the head while enabled to fire.",
                Icon = icon,
                Illustration = icon,
                PowerType = CharacterPowers.PowerGroups.EyeLaser,
                IsActive = () => eyeLaser.Active,
                Disable = () => eyeLaser.SetActive(false),
                Apply = () =>
                {
                    eyeLaser.SetActive(!eyeLaser.Active);
                    if (eyeLaser.Active && r != null)
                    {
                        if (r.Beam != null) r.Beam.Active = false;
                        r.Blast.Active = false;
                        if (r.Shield != null) r.Shield.Active = false;
                    }
                }
            });
        }

        public static LimbBehaviour ResolveHeadLimb(PersonBehaviour person)
        {
            if (person == null || person.Limbs == null) return null;

            for (int i = 0; i < person.Limbs.Length; i++)
            {
                var limb = person.Limbs[i];
                if (limb != null && limb.RoughClassification == LimbBehaviour.BodyPart.Head)
                    return limb;
            }

            for (int i = 0; i < person.Limbs.Length; i++)
            {
                var limb = person.Limbs[i];
                if (limb != null && limb.name.IndexOf("Head", StringComparison.OrdinalIgnoreCase) >= 0)
                    return limb;
            }

            Transform headTransform = person.transform.Find("Head");
            if (headTransform != null)
            {
                var limb = headTransform.GetComponent<LimbBehaviour>();
                if (limb != null) return limb;
            }

            return person.Limbs.Length > 0 ? person.Limbs[0] : null;
        }

        public static void EnsureDisasterRayRig(PersonBehaviour person)
        {
            if (person == null) return;

            for (int i = 0; i < person.Limbs.Length; i++)
            {
                var limb = person.Limbs[i];
                if (limb == null) continue;

                var sr = limb.GetComponent<SpriteRenderer>();
                if (sr == null || sr.sprite == null) continue;

                var spot = limb.GetComponent<CellMaxSpotGlow>();
                if (spot == null)
                {
                    spot = limb.gameObject.AddComponent<CellMaxSpotGlow>();
                    spot.GenericFallback = true;
                    spot.Initialize();
                    continue;
                }

                if (!spot.HasFireSpots && !spot.HasSpots)
                {
                    spot.GenericFallback = true;
                    spot.ForceRefreshFromCurrentSprite();
                }
            }
        }

        public static void EnsureEyeLaserHeadRig(PersonBehaviour person, bool enableEyeGlow = true)
        {
            var head = ResolveHeadLimb(person);
            if (head == null) return;

            var sr = head.GetComponent<SpriteRenderer>();
            if (sr == null || sr.sprite == null) return;

            var spot = head.GetComponent<CellMaxSpotGlow>();
            if (spot != null && spot.EyeLaserMarker == CellMaxSpotGlow.EyeLaserMarkerMode.Piccolo)
                return;

            if (spot == null)
            {
                spot = head.gameObject.AddComponent<CellMaxSpotGlow>();
                spot.GenericFallback = true;
                spot.ScanEyeLaserMarkers = true;
                spot.DynamicSpriteRefresh = true;
                spot.SkipEyeGlowOverlay = !enableEyeGlow;
                spot.Initialize();
                return;
            }

            spot.ScanEyeLaserMarkers = true;
            spot.DynamicSpriteRefresh = true;
            spot.SkipEyeGlowOverlay = !enableEyeGlow;
            if (!spot.HasEyeLaserSpots)
            {
                spot.GenericFallback = true;
                spot.ForceRefreshFromCurrentSprite();
            }
        }

        public static void EnsurePiccoloEyeLaserHeadRig(PersonBehaviour person)
        {
            var head = ResolveHeadLimb(person);
            if (head == null) return;

            var sr = head.GetComponent<SpriteRenderer>();
            if (sr == null || sr.sprite == null) return;

            var spot = head.GetComponent<CellMaxSpotGlow>();
            if (spot == null)
                spot = head.gameObject.AddComponent<CellMaxSpotGlow>();

            spot.EyeLaserMarker = CellMaxSpotGlow.EyeLaserMarkerMode.Piccolo;
            spot.ScanEyeLaserMarkers = true;
            spot.DynamicSpriteRefresh = true;
            spot.GenericFallback = false;
            spot.SkipEyeGlowOverlay = false;
            spot.ApplyPiccoloEyeGlowColors();
            spot.Initialize();
        }

        static void GrantYellowEyeLaser(PersonBehaviour person, GameObject root, CharacterPowers powers, bool piccoloEyeMarkers, string description)
        {
            if (person == null || root == null || powers == null) return;

            if (piccoloEyeMarkers)
                EnsurePiccoloEyeLaserHeadRig(person);
            else
            {
                EnsureEyeLaserHeadRig(person, true);
                var head = ResolveHeadLimb(person);
                var spot = head != null ? head.GetComponent<CellMaxSpotGlow>() : null;
                if (spot != null)
                {
                    spot.ApplyPiccoloEyeGlowColors();
                    spot.ForceRefreshFromCurrentSprite();
                }
            }

            var eyeLaser = root.GetComponent<CellMaxEyeLaserBehaviour>();
            if (eyeLaser == null) eyeLaser = root.AddComponent<CellMaxEyeLaserBehaviour>();
            eyeLaser.ApplyPiccoloBeamStyle(piccoloEyeMarkers);
            eyeLaser.EnableEyeGlow = true;
            eyeLaser.Setup(person);
            eyeLaser.RefreshHeadSpots();
            AddEyeLaserPower(powers, eyeLaser, CombatRefs.From(root), null, description);
        }

        public static void GrantPowerMenuEyeLaser(PersonBehaviour person, GameObject root, CharacterPowers powers) =>
            GrantYellowEyeLaser(person, root, powers, false, "Fires a straight yellow eye laser. Activate the head while enabled to fire.");

        public static void GrantPiccoloEyeLaser(PersonBehaviour person, GameObject root, CharacterPowers powers) =>
            GrantYellowEyeLaser(person, root, powers, true, "Fires a straight yellow eye laser from the face. Activate the head while enabled to fire.");

        public static void GrantDisasterRay(PersonBehaviour person, GameObject root, CharacterPowers powers)
        {
            if (person == null || root == null || powers == null) return;

            EnsureDisasterRayRig(person);

            var disasterRay = root.GetComponent<CellMaxDisasterRayBehaviour>();
            if (disasterRay == null) disasterRay = root.AddComponent<CellMaxDisasterRayBehaviour>();
            disasterRay.Setup(person);
            disasterRay.RefreshSpotGlows();
            AddDisasterRayPower(powers, disasterRay, CombatRefs.From(root));
        }

        public static void GrantEyeLaser(PersonBehaviour person, GameObject root, CharacterPowers powers, bool enableEyeGlow = true)
        {
            if (person == null || root == null || powers == null) return;

            EnsureEyeLaserHeadRig(person, enableEyeGlow);

            var eyeLaser = root.GetComponent<CellMaxEyeLaserBehaviour>();
            if (eyeLaser == null) eyeLaser = root.AddComponent<CellMaxEyeLaserBehaviour>();
            eyeLaser.EnableEyeGlow = enableEyeGlow;
            eyeLaser.Setup(person);
            eyeLaser.RefreshHeadSpots();
            AddEyeLaserPower(powers, eyeLaser, CombatRefs.From(root));
        }
    }
}
