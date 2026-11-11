using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DBPU
{
    // Techniques given to a fighter through the Power Giver menu (on top of the characters
    // that know them natively: Krillin - Kienzan, Beerus & co. - Hakai / Hakai Ball).
    public sealed class DbpuGrantedTechniques : MonoBehaviour
    {
        public readonly HashSet<string> Ids = new HashSet<string>();
        public static bool Has(PersonBehaviour person, string id)
        {
            var granted = person ? person.GetComponent<DbpuGrantedTechniques>() : null;
            return granted && granted.Ids.Contains(id);
        }
        public static void Add(PersonBehaviour person, string id)
        {
            if (!person) return;
            var granted = person.GetComponent<DbpuGrantedTechniques>();
            if (!granted) granted = person.gameObject.AddComponent<DbpuGrantedTechniques>();
            granted.Ids.Add(id);
        }
    }

    // Puts Kienzan / Hakai / Hakai Ball into the fighter's own Powers menu. Enabling one there
    // equips it on the front hand (the same as picking it in the U wheel); disabling unequips it.
    public static class DbpuTechniquePowers
    {
        public static readonly string[] All = { KiHandLoadout.KienzanId, KiHandLoadout.HakaiId, KiHandLoadout.HakaiBallId };

        public static string Name(string id)
        {
            if (id == KiHandLoadout.KienzanId) return "Kienzan";
            if (id == KiHandLoadout.HakaiId) return "Hakai";
            if (id == KiHandLoadout.HakaiBallId) return "Hakai Ball";
            return id;
        }
        public static string Description(string id)
        {
            if (id == KiHandLoadout.KienzanId) return "Destructo Disc. Hold Use on the hand to form it, aim with the cursor, release (or click) to throw. Cuts through limbs; tap Use then hold to steer; a long charge throws a Double Kienzan.";
            if (id == KiHandLoadout.HakaiId) return "Destruction. Click a limb or object to erase it (Alt+click drags normally). Weaker targets dissolve, equals resist. Use on the hand erases what the palm touches.";
            if (id == KiHandLoadout.HakaiBallId) return "Hold Use on the hand to gather the orb, aim with the cursor, release (or click) to fire. Seals a fighter in a sphere of destruction; stronger ones can break out.";
            return "";
        }

        static readonly List<LimbBehaviour> scratch = new List<LimbBehaviour>();
        static List<LimbBehaviour> Hands(PersonBehaviour person)
        {
            scratch.Clear();
            if (!person || person.Limbs == null) return scratch;
            // Front hand first: it is the one drawn in front of the body.
            foreach (var limb in person.Limbs) if (KiHandLoadout.IsHand(limb) && limb.name.IndexOf("Front", StringComparison.OrdinalIgnoreCase) >= 0) scratch.Add(limb);
            foreach (var limb in person.Limbs) if (KiHandLoadout.IsHand(limb) && !scratch.Contains(limb)) scratch.Add(limb);
            return scratch;
        }

        public static bool Equipped(PersonBehaviour person, string id)
        {
            foreach (var hand in Hands(person))
            {
                var loadout = hand.GetComponent<KiHandLoadout>();
                if (loadout && loadout.SelectedId == id) return true;
            }
            return false;
        }

        static void Toggle(PersonBehaviour person, string id)
        {
            if (!person || !person.IsAlive()) return;
            var hands = new List<LimbBehaviour>(Hands(person));
            foreach (var hand in hands)
            {
                var loadout = hand.GetComponent<KiHandLoadout>();
                if (loadout && loadout.SelectedId == id) { loadout.Equip(KiHandLoadout.NoneId); return; }
            }
            foreach (var hand in hands)
            {
                if (hand.Health <= 0f) continue;
                var loadout = KiHandLoadout.GetOrCreate(hand);
                if (loadout && loadout.Equip(id)) return;
            }
            DbpuWorldLabel.Show(person, Name(id).ToUpperInvariant(), "NEEDS A WORKING HAND", 1.4f);
        }

        static void Unequip(PersonBehaviour person, string id)
        {
            foreach (var hand in new List<LimbBehaviour>(Hands(person)))
            {
                var loadout = hand.GetComponent<KiHandLoadout>();
                if (loadout && loadout.SelectedId == id) loadout.Equip(KiHandLoadout.NoneId);
            }
        }

        public static void Register(PersonBehaviour person, CharacterPowers powers, string id)
        {
            if (!person || !powers || powers.PowerData.ContainsKey(id)) return;
            var icon = KiHandLoadout.TechniqueIcon(id);
            powers.AddPower(new CharacterPowers.PowerStats
            {
                ID = id,
                Name = Name(id),
                Description = Description(id),
                Icon = icon,
                Illustration = icon,
                IsActive = () => Equipped(person, id),
                GetStatusText = () => Equipped(person, id) ? "Equipped on the hand" : "Disabled",
                Apply = () => Toggle(person, id),
                Disable = () => Unequip(person, id)
            });
        }

        // Power Giver entry point.
        public static void Grant(PersonBehaviour person, CharacterPowers powers, string id)
        {
            if (!person) return;
            if (!person.GetComponent<KiSystem>()) { ModAPI.Notify(Name(id) + " needs a ki user (a Dragon Ball character)."); return; }
            DbpuGrantedTechniques.Add(person, id);
            Register(person, powers, id);
        }

        // Natively eligible fighters also get the entries in their Powers menu.
        public static void AttachDefaults(PersonBehaviour person)
        {
            if (!person || person.GetComponent<DbpuTechniquePowerSync>()) return;
            person.gameObject.AddComponent<DbpuTechniquePowerSync>();
        }
    }

    public sealed class DbpuTechniquePowerSync : MonoBehaviour
    {
        IEnumerator Start()
        {
            // Character setup registers its own powers over the first frames.
            yield return new WaitForSeconds(.6f);
            var person = GetComponent<PersonBehaviour>();
            var powers = GetComponent<CharacterPowers>();
            if (!person || !powers) yield break;
            if (DbpuHandTechniques.CanKienzan(person)) DbpuTechniquePowers.Register(person, powers, KiHandLoadout.KienzanId);
            if (DbpuHandTechniques.KnowsHakai(person))
            {
                DbpuTechniquePowers.Register(person, powers, KiHandLoadout.HakaiId);
                DbpuTechniquePowers.Register(person, powers, KiHandLoadout.HakaiBallId);
            }
        }
    }
}
