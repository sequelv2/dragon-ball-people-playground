using System;
using System.Collections.Generic;
using UnityEngine;

namespace DBPU
{
    public sealed class CombatAbilityEntry
    {
        public string Id;
        public string Name;
        public string Description;
        public string Cost;
        public Sprite Icon;
        public Color Color;
    }

    // Native attacks already share one controller per fighter. This state makes that
    // restriction explicit while allowing the new fist/share abilities on a chosen hand.
    internal sealed class KiFighterLoadout : MonoBehaviour
    {
        internal string NativeSelectedId;
        internal readonly List<KiHandLoadout> Hands = new List<KiHandLoadout>();

        internal bool HasCustomHand
        {
            get
            {
                for (int i = Hands.Count - 1; i >= 0; i--)
                {
                    var hand = Hands[i];
                    if (!hand) { Hands.RemoveAt(i); continue; }
                    if (hand.isActiveAndEnabled && KiHandLoadout.IsHand(hand.Limb) && KiHandLoadout.IsCustom(hand.SelectedId)) return true;
                }
                return false;
            }
        }
    }

    public sealed class KiHandLoadout : MonoBehaviour
    {
        public const string NoneId = "None";
        public const string FistId = "KiFist";
        public const string InfusionId = "KiFistInfusion";
        public const string ShareId = "KiShare";

        public LimbBehaviour Limb;
        public PersonBehaviour Person;
        public string SelectedId;
        KiFighterLoadout fighter;

        public static bool IsInputCaptured { get { return CombatAbilityWheel.AnyMenuCaptured; } }

        public static KiHandLoadout GetOrCreate(LimbBehaviour limb)
        {
            if (!IsStrikeLimb(limb)) return null;
            var result = limb.GetComponent<KiHandLoadout>();
            if (!result) result = limb.gameObject.AddComponent<KiHandLoadout>();
            result.EnsureReferences();
            return result;
        }

        public static bool IsHand(LimbBehaviour limb)
        {
            return limb && limb.Person && MCP_Utils.LimbCanUsePowers(limb)
                && limb.name.IndexOf("LowerArm", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool IsFoot(LimbBehaviour limb)
        {
            return limb && limb.Person && MCP_Utils.LimbCanUsePowers(limb)
                && limb.name.StartsWith("Foot", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsStrikeLimb(LimbBehaviour limb) { return IsHand(limb) || IsFoot(limb); }

        internal static bool IsCustom(string id) { return id == FistId || id == InfusionId || id == ShareId; }

        void Awake() { EnsureReferences(); }

        void EnsureReferences()
        {
            if (!Limb) Limb = GetComponent<LimbBehaviour>();
            if (!Person && Limb) Person = Limb.Person;
            if (!Person) return;
            if (!fighter) fighter = Person.GetComponent<KiFighterLoadout>();
            if (!fighter) fighter = Person.gameObject.AddComponent<KiFighterLoadout>();
            if (!fighter.Hands.Contains(this)) fighter.Hands.Add(this);
            if (string.IsNullOrEmpty(SelectedId))
            {
                if (IsFoot(Limb)) SelectedId = NoneId;
                else if (!string.IsNullOrEmpty(fighter.NativeSelectedId)) SelectedId = fighter.NativeSelectedId;
            }
            if (SelectedId == InfusionId)
            {
                SelectedId = FistId;
                var legacy = KiFistHand.GetOrCreate(Limb);
                if (legacy) { legacy.Equipped = true; legacy.InfuseAllEnergy = true; }
            }
        }

        void OnDestroy()
        {
            if (fighter) fighter.Hands.Remove(this);
        }

        public List<CombatAbilityEntry> GetEntries()
        {
            EnsureReferences();
            var entries = new List<CombatAbilityEntry>();
            if (!Person) return entries;
            bool foot = IsFoot(Limb);
            if (Person.GetComponent<KiSystem>())
            {
                entries.Add(new CombatAbilityEntry
                {
                    Id = FistId, Name = "Ki Fist", Icon = DBPU.Icon_PunchPower,
                    Description = foot ? "Envelop this foot in ki and swing hard to strike. Tap R to change mode; hold R to charge. God Fist requires divine forms; received ki enables Energy Infusion."
                        : "Envelop this hand in ki and swing hard to strike. Tap R to change mode; hold R to charge. God Fist requires divine forms; received ki enables Energy Infusion.",
                    Cost = "Tap R: mode / hold R: charge", Color = new Color(1f, .57f, .13f)
                });
                if (!foot) entries.Add(new CombatAbilityEntry
                {
                    Id = ShareId, Name = "Share Ki", Icon = DBPU.Icon_SpiritBomb,
                    Description = "Aim at any fighter or gathering Spirit Bomb and hold Use. Ki grows the sphere, or boosts strength, health and attacks.",
                    Cost = "7.5% Ki/s; keeps 20% reserve", Color = new Color(.26f, .88f, 1f)
                });
            }

            var powers = Person.GetComponent<CharacterPowers>();
            if (powers && !foot)
            {
                foreach (var power in powers.GetOrderedPowers())
                {
                    if (!IsHandAttack(power)) continue;
                    if (power.ID == "SuperSpiritBomb" && powers.PowerData.ContainsKey("SpiritBomb")) continue;
                    entries.Add(new CombatAbilityEntry
                    {
                        Id = power.ID, Name = power.Name, Icon = power.Icon ? power.Icon : power.Illustration,
                        Description = NativeDescription(power), Cost = NativeCost(power.ID), Color = NativeColor(power.ID)
                    });
                }
            }
            entries.Add(new CombatAbilityEntry
            {
                Id = NoneId, Name = foot ? "Empty foot" : "Empty hand", Icon = DBPU.Icon_PunchPower,
                Description = foot ? "Remove the ki technique from this foot. Ordinary kicks remain available."
                    : "Unequip the selected ki technique. Ordinary punches and utility powers remain available.",
                Cost = "No Ki cost", Color = new Color(.60f, .65f, .72f)
            });
            return entries;
        }

        public bool Equip(string id)
        {
            EnsureReferences();
            if (!Person || !Limb || !Person.IsAlive() || Limb.Health <= 0f || !IsStrikeLimb(Limb) || string.IsNullOrEmpty(id)) return false;
            bool legacyInfusion = id == InfusionId;
            if (legacyInfusion) id = FistId;
            if (IsFoot(Limb))
            {
                // A foot receives ki only through an explicit local choice. It cannot
                // select native hand powers or change either hand's shared controller.
                if (id != NoneId && id != FistId && id != InfusionId) return false;
                if (id != NoneId && !Person.GetComponent<KiSystem>()) return false;
                var existing = GetComponent<KiFistHand>();
                if (SelectedId == id && (id == NoneId || (existing && existing.Equipped)))
                { if (legacyInfusion && existing) existing.InfuseAllEnergy = true; return true; }
                StopCustom(this); SelectedId = id;
                if (id != NoneId)
                {
                    var strike = KiFistHand.GetOrCreate(Limb);
                    strike.Equipped = true; if (legacyInfusion) strike.InfuseAllEnergy = true;
                }
                return true;
            }
            if (id == NoneId)
            {
                if (IsCustom(SelectedId))
                {
                    StopCustom(this);
                    SelectedId = NoneId;
                    return true;
                }
                if (fighter.HasCustomHand) { SelectedId = NoneId; return true; }
                DisableOffensivePowers(null);
                SetNativeSelection(NoneId);
                return true;
            }
            if (IsCustom(id))
            {
                if (!Person.GetComponent<KiSystem>()) return false;
                if (SelectedId == id)
                {
                    var existingFist = GetComponent<KiFistHand>();
                    var existingShare = GetComponent<KiShareHand>();
                    if ((id == FistId || id == InfusionId) && existingFist && existingFist.Equipped)
                    { if (legacyInfusion) existingFist.InfuseAllEnergy = true; return true; }
                    if (id == ShareId && existingShare && existingShare.Equipped) return true;
                }
                DisableOffensivePowers(null);
                fighter.NativeSelectedId = NoneId;
                foreach (var hand in fighter.Hands)
                    if (hand && !IsCustom(hand.SelectedId)) hand.SelectedId = NoneId;
                StopCustom(this);
                SelectedId = id;
                if (id == FistId || id == InfusionId)
                { var fist=KiFistHand.GetOrCreate(Limb);fist.Equipped=true;if(legacyInfusion)fist.InfuseAllEnergy=true; }
                else KiShareHand.GetOrCreate(Limb).Equipped = true;
                return true;
            }

            var powers = Person.GetComponent<CharacterPowers>();
            CharacterPowers.PowerStats selected;
            if (!powers || !powers.PowerData.TryGetValue(id, out selected) || !IsHandAttack(selected)) return false;
            // Re-selecting an already active native technique must never toggle it off.
            bool alreadyActive = selected.IsActive != null ? selected.IsActive() : selected.Active;
            if (!alreadyActive) DisableOffensivePowers(id);
            foreach (var hand in fighter.Hands)
                if (hand && IsHand(hand.Limb)) { StopCustom(hand); hand.SelectedId = NoneId; }
            fighter.NativeSelectedId = NoneId;
            if (!alreadyActive) powers.ApplyPower(id);
            bool applied = selected.IsActive != null ? selected.IsActive() : selected.Active;
            if (!applied) return false;
            SetNativeSelection(id);
            return true;
        }

        static void StopCustom(KiHandLoadout hand)
        {
            var fist = hand.GetComponent<KiFistHand>();
            if (fist) { fist.Equipped = false; fist.Cancel(); }
            var share = hand.GetComponent<KiShareHand>();
            if (share) { share.Equipped = false; share.Cancel(); }
        }

        void SetNativeSelection(string id)
        {
            fighter.NativeSelectedId = id;
            foreach (var hand in fighter.Hands)
                if (hand && IsHand(hand.Limb)) hand.SelectedId = id;
        }

        void DisableOffensivePowers(string exceptId)
        {
            var powers = Person.GetComponent<CharacterPowers>();
            if (powers)
            {
                foreach (var power in powers.GetOrderedPowers())
                    if (power.ID != exceptId && IsOffensive(power)) power.Disable?.Invoke();
                powers.HarvestPowerStates();
            }
            var beam = Person.GetComponent<BeamBehaviour>();
            if (beam) { beam.CancelBeamState(); beam.Active = false; }
            var blast = Person.GetComponent<BlastBehaviour>();
            if (blast) { blast.Active = false; blast.IsBarrage = false; }
            var ball = Person.GetComponent<BallBehaviour>();
            if (ball) ball.CancelBallState();
            var straight = Person.GetComponent<StraightBeamBehaviour>();
            if (straight) straight.Active = false;
            var refs = PowerSetup.CombatRefs.From(Person.gameObject);
            if (refs.Solar) refs.Solar.Active = false;
            if (refs.FinalExplosion) refs.FinalExplosion.Active = false;
            if (refs.Hellzone) refs.Hellzone.Active = false;
            if (refs.StardustFall) refs.StardustFall.Active = false;
            if (refs.MeteorExplosion) refs.MeteorExplosion.Active = false;
            if (refs.GodPunisher) refs.GodPunisher.Active = false;
            if (refs.StardustBreaker) refs.StardustBreaker.CancelHeldCharge();
            if (refs.GodsWrath) refs.GodsWrath.Active = false;
            if (refs.CataclysmicOrb) refs.CataclysmicOrb.Active = false;
        }

        // All lower-arm native activation sites use this gate. A custom technique on
        // either hand suppresses the shared native controllers on both arms.
        public static bool LegacyAllowed(LimbBehaviour limb, string abilityId)
        {
            if (IsInputCaptured || CombatTransformationWheel.InputCaptured) return false;
            if (!IsHand(limb)) return false;
            var state = limb.Person.GetComponent<KiFighterLoadout>();
            if (!state) return true;
            if (state.HasCustomHand) return false;
            string selected = state.NativeSelectedId;
            if (string.IsNullOrEmpty(selected)) return true;
            if (selected == NoneId) return false;
            if (SameId(selected, abilityId)) return true;
            var definition = PowerCatalog.Get(selected);
            return definition != null && !string.IsNullOrEmpty(definition.EffectId) && SameId(definition.EffectId, abilityId);
        }

        // Called after CharacterPowers.ApplyPower so the existing powers menu and
        // the hand wheel remain two views of the same equipped native technique.
        public static void NotifyPowerApplied(CharacterPowers powers, string id)
        {
            if (!powers) return;
            var state = powers.GetComponent<KiFighterLoadout>();
            CharacterPowers.PowerStats power;
            if (!state || !powers.PowerData.TryGetValue(id, out power) || !IsOffensive(power)) return;
            bool enabled = power.IsActive != null ? power.IsActive() : power.Active;
            if (enabled)
            {
                foreach (var hand in state.Hands)
                    if (hand && IsHand(hand.Limb)) { StopCustom(hand); hand.SelectedId = id; }
                state.NativeSelectedId = id;
            }
            else if (state.NativeSelectedId == id)
            {
                state.NativeSelectedId = NoneId;
                foreach (var hand in state.Hands)
                    if (hand && IsHand(hand.Limb) && !IsCustom(hand.SelectedId)) hand.SelectedId = NoneId;
            }
        }

        static bool SameId(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
            int ai = 0, bi = 0;
            while (true)
            {
                while (ai < a.Length && !char.IsLetterOrDigit(a[ai])) ai++;
                while (bi < b.Length && !char.IsLetterOrDigit(b[bi])) bi++;
                if (ai == a.Length || bi == b.Length) return ai == a.Length && bi == b.Length;
                if (char.ToUpperInvariant(a[ai++]) != char.ToUpperInvariant(b[bi++])) return false;
            }
        }

        static bool IsHandAttack(CharacterPowers.PowerStats power)
        {
            if (power == null || power.Passive) return false;
            switch (power.PowerType)
            {
                case CharacterPowers.PowerGroups.KiBlast:
                case CharacterPowers.PowerGroups.Solar:
                case CharacterPowers.PowerGroups.Hellzone:
                case CharacterPowers.PowerGroups.Ball:
                case CharacterPowers.PowerGroups.StraightBeam:
                case CharacterPowers.PowerGroups.GogetaSpecial:
                    return true;
                case CharacterPowers.PowerGroups.Beam:
                    var def = PowerCatalog.Get(power.ID);
                    return def == null || def.BeamType != "MouthBeam";
                case CharacterPowers.PowerGroups.BeerusSpecial:
                    return power.ID == "CataclysmicOrb";
                default: return false;
            }
        }

        static bool IsOffensive(CharacterPowers.PowerStats power)
        {
            if (power == null || power.Passive) return false;
            return IsHandAttack(power) || power.PowerType == CharacterPowers.PowerGroups.Beam
                || power.PowerType == CharacterPowers.PowerGroups.FinalExplosion
                || power.PowerType == CharacterPowers.PowerGroups.BeerusSpecial
                || power.PowerType == CharacterPowers.PowerGroups.DisasterRay
                || power.PowerType == CharacterPowers.PowerGroups.EyeLaser;
        }

        static string NativeDescription(CharacterPowers.PowerStats power)
        {
            if (SpiritBombChannel.IsSpirit(power.ID)) return "Hold F to gather; release to hold the sphere in place. Click an enemy to launch. Share Ki into the gathering sphere or its caster to grow it faster. Eight internal pulses precede the final blast. Esc cancels.";
            string description = string.IsNullOrEmpty(power.Description) ? "Activate a lower arm to use this technique." : power.Description;
            return description + " Selected for both hands; one ki attack at a time.";
        }

        static string NativeCost(string id)
        {
            if (SpiritBombChannel.IsSpirit(id)) return "1.8% base Ki/s · grows with time + shared Ki";
            if (id == "KiBlast") return "0.5% base Ki / shot";
            if (id == "KiBlastBarrage") return "0.25% base Ki / shot";
            return "Uses Ki on activation";
        }

        static Color NativeColor(string id)
        {
            var def = PowerCatalog.Get(id);
            if (def != null && def.PowerKind == PowerCatalog.Kind.Ball) return new Color(.50f, .72f, 1f);
            if (id.IndexOf("Rose", StringComparison.OrdinalIgnoreCase) >= 0) return new Color(1f, .42f, .78f);
            if (id.IndexOf("Galick", StringComparison.OrdinalIgnoreCase) >= 0 || id.IndexOf("Death", StringComparison.OrdinalIgnoreCase) >= 0)
                return new Color(.78f, .44f, 1f);
            return new Color(1f, .85f, .28f);
        }
    }
}
