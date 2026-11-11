using System;
using System.Collections.Generic;
using UnityEngine;

namespace DBPU
{
    // ---------------------------------------------------------------------------------
    // Ability bridge. Finds out what this fighter can really do and does it the way a player
    // does: a hand technique is equipped through the hand loadout and fired by the limb's own
    // Use trigger, with the arm aimed first (a shot leaves along -Arm.up); shield, flight,
    // Kaioken, barrage, ki charge and transformations go through their own components.
    // There is no fallback that fakes a technique the fighter does not have.
    //
    // Never used, by rule: anything that moves the body instantly (Instant Transmission, the
    // Ultra Instinct dodge), Final Explosion, Zen-O's erasure, Shenron, Time Skip.
    // Not used because they are aimed with the mouse and no safe aim source exists for an AI:
    // Hakai, Hakai Ball, Kienzan, Spirit Bomb, Solar Flare, mouth beams, and the specials.
    // ---------------------------------------------------------------------------------
    public sealed class AbilityBridge
    {
        static readonly string[] BannedTokens = { "vanish", "teleport", "instant", "transmission", "warp", "blink", "timeskip", "time skip", "finalexplosion", "final explosion", "zeno", "shenron", "erase" };
        static readonly string[] GiantTokens = { "oozaru", "giant", "great ape" };

        readonly PersonBehaviour person; readonly KinematicsRig rig; readonly AiRestoreLedger ledger;
        public KiSystem Ki; public MultipliersAndStuffs Multipliers; public Flight Flight; public KiShield Shield; public Kaioken Kaioken;
        public Barrage Barrage; public BlastBehaviour Blast; public BeamBehaviour Beam; public CharacterPowers Powers;
        public TransformationContextMenu Menu; public CharacterTransformations Forms; public KiChargeModeController Charger;
        public SingularityUltraInstinct UltraInstinct; public KiFormTransition Transition;
        KiFighterLoadout fighterLoadout;
        public readonly PunchPower[] Punch = new PunchPower[2];
        public readonly KickPower[] Kick = new KickPower[4];             // front shin, front foot, back shin, back foot
        readonly UseEventTrigger[][] triggers = new UseEventTrigger[2][];
        readonly KiHandLoadout[] hands = new KiHandLoadout[2];
        readonly KiFistHand[] fists = new KiFistHand[2];
        readonly int[] aimSteady = new int[2];
        public string BlastId, BeamId;
        public readonly List<string> FormKeys = new List<string>(12), FormIds = new List<string>(12);
        public readonly List<float> FormPowers = new List<float>(12);
        public readonly List<string> Skipped = new List<string>(8);
        bool snapshotTaken; int loadout; float loadoutAt = -10f, kaiokenAt = -10f, shieldAt, barrageAt, beamAt, beamOffTarget;
        public int BeamArm { get; private set; }
        public int ShotsFired { get; private set; }

        public const int LoadoutNone = 0, LoadoutBlast = 1, LoadoutBeam = 2, LoadoutFist = 3;

        public AbilityBridge(PersonBehaviour person, KinematicsRig rig, AiRestoreLedger ledger)
        {
            this.person = person; this.rig = rig; this.ledger = ledger; BeamArm = -1;
        }

        public static bool Banned(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            for (int i = 0; i < BannedTokens.Length; i++)
                if (name.IndexOf(BannedTokens[i], StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        static bool Giant(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            for (int i = 0; i < GiantTokens.Length; i++)
                if (name.IndexOf(GiantTokens[i], StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        // ------------------------------------------------------------------------ inventory
        // Run when the AI starts and whenever the body or the form changes.
        public void Refresh()
        {
            Ki = person.GetComponent<KiSystem>(); Multipliers = person.GetComponent<MultipliersAndStuffs>();
            Flight = person.GetComponentInChildren<Flight>(true); Shield = person.GetComponent<KiShield>();
            Kaioken = person.GetComponent<Kaioken>(); Barrage = person.GetComponentInChildren<Barrage>(true);
            Blast = person.GetComponent<BlastBehaviour>(); Beam = person.GetComponent<BeamBehaviour>();
            Powers = person.GetComponent<CharacterPowers>(); Forms = person.GetComponent<CharacterTransformations>();
            Charger = person.GetComponent<KiChargeModeController>(); UltraInstinct = person.GetComponent<SingularityUltraInstinct>();
            Transition = person.GetComponent<KiFormTransition>();
            var head = AutonomousMindModule.HeadOf(person);
            Menu = head ? head.GetComponent<TransformationContextMenu>() : null;

            for (int arm = 0; arm < 2; arm++)
            {
                var limb = rig.Parts[arm == 0 ? AiLimb.LowerArmFront : AiLimb.LowerArm].Limb;
                Punch[arm] = null; triggers[arm] = null; hands[arm] = null; fists[arm] = null; aimSteady[arm] = 0;
                if (!limb) continue;
                var punch = limb.GetComponent<PunchPower>();
                // A fighter with no punch component still gets the mod's own one; it stays inert once the AI is off.
                if (!punch) punch = limb.gameObject.AddComponent<PunchPower>();
                Punch[arm] = punch;
                triggers[arm] = limb.GetComponents<UseEventTrigger>();
                if (Ki && MCP_Utils.LimbCanUsePowers(limb)) hands[arm] = KiHandLoadout.GetOrCreate(limb);
                fists[arm] = limb.GetComponent<KiFistHand>();
            }
            try { PowerSetup.EnsureKickPowerComponents(person); } catch (Exception ex) { Debug.LogWarning("[Singularity] AI kick setup: " + ex.Message); }
            Kick[0] = KickOf(AiLimb.LowerLegFront); Kick[1] = KickOf(AiLimb.FootFront); Kick[2] = KickOf(AiLimb.LowerLeg); Kick[3] = KickOf(AiLimb.Foot);
            fighterLoadout = person.GetComponent<KiFighterLoadout>();

            BlastId = BeamId = null; Skipped.Clear();
            if (Powers)
            {
                var ordered = Powers.GetOrderedPowers();
                for (int i = 0; i < ordered.Count; i++)
                {
                    var power = ordered[i];
                    if (power == null || power.Passive) continue;
                    if (Banned(power.ID) || Banned(power.Name)) { Skip(power.Name ?? power.ID, "banned"); continue; }
                    if (power.PowerType == CharacterPowers.PowerGroups.KiBlast) { if (BlastId == null && Blast) BlastId = power.ID; }
                    else if (power.PowerType == CharacterPowers.PowerGroups.Beam)
                    {
                        var definition = PowerCatalog.Get(power.ID);
                        if (definition != null && definition.BeamType == "MouthBeam") { Skip(power.Name ?? power.ID, "mouth beam"); continue; }
                        if (BeamId == null && Beam) BeamId = power.ID;
                    }
                    else if (power.PowerType == CharacterPowers.PowerGroups.Shield) { }
                    else if (!string.IsNullOrEmpty(power.PowerType)) Skip(power.Name ?? power.ID, "not driven by the AI");
                }
            }
            RefreshForms();
        }

        KickPower KickOf(int part)
        {
            var limb = rig.Parts[part].Limb;
            return limb ? limb.GetComponent<KickPower>() : null;
        }

        void Skip(string what, string why)
        {
            string line = what + " (" + why + ")";
            if (!Skipped.Contains(line)) Skipped.Add(line);
        }

        void RefreshForms()
        {
            FormKeys.Clear(); FormIds.Clear(); FormPowers.Clear();
            if (!Menu || !Forms || Forms.transformations == null) return;
            var entries = Forms.transformations.GetOrderedEntries();
            for (int i = 0; i < entries.Count; i++)
            {
                var stats = entries[i].Value;
                if (stats == null || Banned(entries[i].Key) || Banned(stats.Name)) continue;
                // Giant bodies are not something this rig is built to drive: the AI never picks them itself.
                // Size is a height in metres (about 1.75 for a person).
                if (Giant(entries[i].Key) || Giant(stats.Name) || stats.Size > 4f) { Skip(stats.Name ?? entries[i].Key, "giant form"); continue; }
                float power = Forms.GetEffectivePower(stats);
                int at = FormPowers.Count;
                for (int j = 0; j < FormPowers.Count; j++) if (power < FormPowers[j]) { at = j; break; }
                FormKeys.Insert(at, entries[i].Key); FormIds.Insert(at, stats.ID); FormPowers.Insert(at, power);
            }
        }

        public int FormIndex
        {
            get
            {
                if (!Menu) return -1;
                string form = Menu.Form;
                for (int i = 0; i < FormIds.Count; i++) if (FormIds[i] == form || FormKeys[i] == form) return i;
                return -1;
            }
        }

        public string Form { get { return Menu ? Menu.Form : (Ki ? Ki.CurrentForm : "Base"); } }

        // 0 in the weakest form, 1 in the strongest; Ultra Instinct always reads as the top tier.
        public float Tier01
        {
            get
            {
                string form = Form;
                if (form == "UI" || form == "MUI") return 1f;
                int index = FormIndex;
                return index < 0 || FormIds.Count < 2 ? .25f : (float)index / (FormIds.Count - 1);
            }
        }

        public bool Transforming { get { return Transition && Transition.Busy; } }
        public float KiFraction { get { if (!Ki) return 1f; float max = KiCombatMath.MaxKi(Ki); return max <= 0f ? 1f : Mathf.Clamp01(Ki.currentKi / max); } }
        public bool KiSpent { get { return Ki && (Ki.isExhausted || Ki.SparkingExhaustActive); } }
        public bool Guarded { get { return BeamPhysics.GuardOf(person) != null; } }
        public bool Anchored { get { return Beam && Beam.Firing && Beam.StillActive; } }

        // -------------------------------------------------------------------- always-on setup
        public void TakeControl()
        {
            for (int i = 0; i < Punch.Length; i++)
                if (Punch[i]) { ledger.Record(AiRestoreLedger.Kind.PunchActive, Punch[i], Punch[i].Active); Punch[i].Active = true; }
            for (int i = 0; i < Kick.Length; i++)
                if (Kick[i]) { ledger.Record(AiRestoreLedger.Kind.KickActive, Kick[i], Kick[i].Active); Kick[i].Active = true; }
            // The Ultra Instinct dodge teleports: it stays off for as long as the AI holds this body.
            if (UltraInstinct) { ledger.Record(AiRestoreLedger.Kind.UiDodge, UltraInstinct, UltraInstinct.SuppressedByAI); UltraInstinct.SuppressedByAI = true; }
        }

        // Section 5.5: what PunchPower or KickPower would make of a hit at this limb speed.
        public bool MeleeOutlook(int slot, AiFoe foe, float speed, out float percent, out float stopAt)
        {
            percent = 0f; stopAt = 0f;
            if (foe == null || !foe.Person) return false;
            float damage, output, factor;
            if (slot < 2)
            {
                var punch = Punch[slot];
                if (!punch) return false;
                damage = punch.Damage; output = punch.DamageOutput; factor = .01f;
            }
            else
            {
                int at = (slot - 2) * 2;
                var kick = Kick[at] ? Kick[at] : Kick[at + 1];
                if (!kick) return false;
                damage = kick.Damage; output = kick.DamageOutput; factor = .02f;
            }
            float final = Multipliers ? Mathf.Max(.01f, Multipliers.FinalMultiplier) : 1f;
            float ownHealth = MCP_Utils.AverageMaxHealth(person), ownKaioken = Kaioken ? Kaioken.HealthScale : 1f;
            if (damage < 0f) damage = ownHealth * factor / Mathf.Max(1f, final);
            float released = Multipliers ? Mathf.Clamp(KiCombatMath.Finite(Multipliers.KiOutput, 1f), .01f, 1e30f) : 1f;
            float dealt = damage * final * output * released * (Mathf.Clamp(speed, 5f, 25f) / 5f) * DBPU.DamageMultiplier;
            float victimHealth = MCP_Utils.AverageMaxHealth(foe.Person);
            if (victimHealth <= 0f) victimHealth = 1f;
            percent = dealt / victimHealth * 100f;
            float attacker = ownHealth / ownKaioken * (Multipliers ? Multipliers.FinalMultiplier : 1f);
            float victim = victimHealth / (foe.Kaioken ? foe.Kaioken.HealthScale : 1f) * (foe.Multipliers ? Mathf.Max(1f, foe.Multipliers.FinalMultiplier) : 1f);
            stopAt = MCP_Utils.MeleeStopThreshold(attacker, victim, .5f);
            return true;
        }

        // ---------------------------------------------------------------------------- loadout
        // Recorded once, before the first technique the AI equips, so the player's own choice comes back.
        void Snapshot()
        {
            if (snapshotTaken) return;
            snapshotTaken = true;
            for (int arm = 0; arm < 2; arm++)
                if (fists[arm]) ledger.Record(AiRestoreLedger.Kind.KiFist, fists[arm], fists[arm].Equipped);
            // Restored in reverse: power states first, then the selection labels they would otherwise overwrite.
            for (int arm = 0; arm < 2; arm++)
                if (hands[arm]) ledger.Record(AiRestoreLedger.Kind.HandLoadout, hands[arm], false, 0, hands[arm].SelectedId);
            if (!fighterLoadout) fighterLoadout = person.GetComponent<KiFighterLoadout>();
            if (fighterLoadout) ledger.Record(AiRestoreLedger.Kind.FighterLoadout, fighterLoadout, false, 0, fighterLoadout.NativeSelectedId);
            if (Powers)
            {
                var ordered = Powers.GetOrderedPowers();
                for (int i = 0; i < ordered.Count; i++)
                {
                    var power = ordered[i];
                    if (power == null || power.Passive) continue;
                    bool active = power.IsActive != null ? power.IsActive() : power.Active;
                    ledger.Record(AiRestoreLedger.Kind.PowerState, Powers, active, 0, power.ID);
                }
            }
        }

        KiHandLoadout Hand(int arm)
        {
            if (arm >= 0 && arm < 2 && hands[arm] && KiHandLoadout.IsHand(hands[arm].Limb)) return hands[arm];
            return null;
        }

        public int UsableArm
        {
            get
            {
                if (Hand(0) != null && rig.ChainUsable(KinematicsRig.ArmFront)) return 0;
                if (Hand(1) != null && rig.ChainUsable(KinematicsRig.ArmBack)) return 1;
                return -1;
            }
        }

        public bool Equip(int mode, int arm)
        {
            if (mode == loadout && (mode != LoadoutFist || (fists[arm] && fists[arm].Equipped))) return true;
            if (Time.time - loadoutAt < .6f) return false;         // a hand does not change technique every frame
            var hand = Hand(arm);
            if (hand == null) return false;
            string id = mode == LoadoutBlast ? BlastId : mode == LoadoutBeam ? BeamId : mode == LoadoutFist ? KiHandLoadout.FistId : KiHandLoadout.NoneId;
            if (string.IsNullOrEmpty(id)) return false;
            Snapshot();
            bool ok;
            try { ok = hand.Equip(id); }
            catch (Exception ex) { Debug.LogWarning("[Singularity] AI equip " + id + ": " + ex.Message); ok = false; }
            loadoutAt = Time.time;
            if (!ok) return false;
            loadout = mode;
            if (mode == LoadoutFist)
            {
                fists[arm] = hand.Limb.GetComponent<KiFistHand>();
                if (fists[arm]) ledger.Record(AiRestoreLedger.Kind.KiFist, fists[arm], false);
                triggers[arm] = hand.Limb.GetComponents<UseEventTrigger>();
            }
            return true;
        }

        // One native Use on the limb's triggers. The grip on the same limb is left alone on purpose,
        // so firing a shot never grabs whatever happens to be in reach.
        void Press(int arm)
        {
            var list = triggers[arm];
            if (list == null) return;
            for (int i = 0; i < list.Length; i++)
                if (list[i] && list[i].enabled) list[i].Use(null);
        }

        // ------------------------------------------------------------------------------- aim
        // Points the arm down the line of fire and reports when the muzzle has settled on it.
        public bool AimArm(int arm, Vector2 target, float toleranceDegrees)
        {
            if (arm < 0 || !rig.ChainUsable(arm)) return false;
            var chain = rig.Chains[arm]; var upper = rig.Parts[chain.Upper]; var lower = rig.Parts[chain.Lower];
            Vector2 d = target - rig.Anchor(upper);
            if (d.sqrMagnitude < 1e-4f) return false;
            float line = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            Vector2 muzzle = -(Vector2)lower.Tf.up;
            // The shot leaves along -Arm.up, which need not be exactly the direction the forearm was measured to point.
            float offset = Mathf.DeltaAngle(rig.Pointing(lower), Mathf.Atan2(muzzle.y, muzzle.x) * Mathf.Rad2Deg);
            if (upper.Joint.useLimits && !rig.WithinLimits(chain.Upper, line)) rig.FreeLimits(chain.Upper);
            rig.Aim(chain.Upper, line, 2f);
            rig.Aim(chain.Lower, line - offset, 2f);
            float error = Vector2.Angle(muzzle, d);
            if (error <= toleranceDegrees && Mathf.Abs(lower.Body.angularVelocity) < 260f) aimSteady[arm]++; else aimSteady[arm] = 0;
            return aimSteady[arm] >= 3;
        }

        public void ReleaseAim(int arm)
        {
            if (arm < 0 || arm > 1) return;
            aimSteady[arm] = 0;
            rig.RestoreLimits(rig.Chains[arm].Upper);
        }

        // ----------------------------------------------------------------------------- blasts
        public bool HasBlast { get { return Blast && BlastId != null && UsableArm >= 0; } }

        public bool BlastAffordable
        {
            get { return HasBlast && !Blast.IsBarrage && Ki && !KiSpent && Ki.currentKi >= Ki.maxKi * Blast.CostPercent * 4f + .01f; }
        }

        public bool FireBlast(int arm)
        {
            if (!BlastAffordable || Blast.Firing || !Equip(LoadoutBlast, arm)) return false;
            Press(arm);
            if (!Blast.Firing) return false;
            ShotsFired++; aimSteady[arm] = 0;
            return true;
        }

        // ------------------------------------------------------------------------------ beams
        public bool HasBeam { get { return Beam && BeamId != null && UsableArm >= 0; } }
        public bool BeamBusy { get { return Beam && (Beam.Charging || Beam.Firing || Beam.StillActive); } }
        public bool BeamCharging { get { return Beam && Beam.Charging && !Beam.Firing; } }
        public bool BeamCharged { get { return Beam && Beam.Charging && Beam.FullCharged && !Beam.Firing; } }
        public bool BeamFiring { get { return Beam && Beam.Firing; } }
        public bool BeamAffordable { get { return HasBeam && Ki && !KiSpent && Ki.currentKi >= Ki.maxKi * Beam.CostPercent * 1.15f; } }
        public float BeamHeld { get { return BeamFiring ? Time.time - beamAt : 0f; } }

        // Press one of three: start charging on this arm.
        public bool StartBeam(int arm)
        {
            if (!BeamAffordable || BeamBusy || !Equip(LoadoutBeam, arm)) return false;
            ledger.Record(AiRestoreLedger.Kind.Beam, Beam);
            Press(arm);
            if (!Beam.Charging && !Beam.Firing) return false;
            BeamArm = arm; beamOffTarget = 0f;
            return true;
        }

        // Press two of three: let it go.
        public bool ReleaseBeam()
        {
            if (BeamArm < 0 || !BeamCharged) return false;
            Press(BeamArm);
            beamAt = Time.time;
            return Beam.Firing;
        }

        // Press three of three: stop a running beam.
        public void StopBeam()
        {
            if (BeamArm < 0 || !Beam) { BeamArm = -1; return; }
            if (Beam.Firing && !Beam.Charging && !Beam.FullCharged) Press(BeamArm);
            else if (Beam.Charging && !Beam.Firing) Beam.CancelBeamState();
        }

        // A struggle this fighter's beam is in: who is winning, and how close the clash is to it.
        public bool Duel(out float balance, out float gap)
        {
            balance = 0f; gap = float.MaxValue;
            if (!Beam) return false;
            var duel = BeamDuel.Of(Beam);
            if (duel == null) return false;
            balance = duel.A == Beam ? duel.Balance : -duel.Balance;
            gap = duel.GapTo(Beam);
            return true;
        }

        public void TrackBeamTarget(bool onTarget, float dt) { beamOffTarget = onTarget ? 0f : beamOffTarget + dt; }
        public float BeamOffTarget { get { return beamOffTarget; } }

        // ----------------------------------------------------------------------------- shield
        public bool HasShield { get { return Shield && Shield.isActiveAndEnabled; } }
        public bool ShieldUp { get { return Shield && Shield.Using; } }

        public bool RaiseShield()
        {
            if (!HasShield || KiSpent) return false;
            if (Shield.Using) return true;
            ledger.Record(AiRestoreLedger.Kind.Shield, Shield, false);
            bool up = Shield.TryRaise();
            if (up) shieldAt = Time.time;
            return up;
        }

        public void LowerShield() { if (Shield && Shield.Using) Shield.Lower(false); }
        public float ShieldHeld { get { return ShieldUp ? Time.time - shieldAt : 0f; } }

        // ----------------------------------------------------------------------------- flight
        public bool HasFlight { get { return Flight; } }
        public bool Flying { get { return Flight && Flight.IsFlying; } }

        public void SetFlight(bool on)
        {
            if (!Flight || Flight.IsFlying == on) return;
            ledger.Record(AiRestoreLedger.Kind.FlightState, Flight, Flight.IsFlying);
            Flight.Toggle();
        }

        // ---------------------------------------------------------------------------- Kaioken
        public bool KaiokenOn { get { return Kaioken && Kaioken.Active; } }

        public bool KaiokenAllowed
        {
            get { return Kaioken && Kaioken.isActiveAndEnabled && (!Menu || Kaioken.CompatibleForms.Contains(Menu.Form)) && Time.time - kaiokenAt > 3f; }
        }

        public bool SetKaioken(bool on)
        {
            if (!Kaioken || Kaioken.Active == on || !KaiokenAllowed) return false;
            kaiokenAt = Time.time;
            Kaioken.ToggleFromPowersMenu();
            return true;
        }

        public float Strain { get { return Ki ? Ki.Strain : 0f; } }

        // ---------------------------------------------------------------------------- barrage
        public bool HasBarrage { get { return Barrage && Barrage.Enabled && Punch[0]; } }
        public bool BarrageFiring { get { return Barrage && Barrage.FiringForAI; } }
        public float BarrageReach { get { return Barrage ? Barrage.distance * Mathf.Abs(person.transform.localScale.x) : 0f; } }
        public float BarrageHeld { get { return BarrageFiring ? Time.time - barrageAt : 0f; } }

        public bool StartBarrage()
        {
            if (!HasBarrage) return false;
            ledger.Record(AiRestoreLedger.Kind.Barrage, Barrage);
            if (!Barrage.TryStartFromAI()) return false;
            barrageAt = Time.time;
            return true;
        }

        public void StopBarrage() { if (Barrage && Barrage.FiringForAI) Barrage.StopFromAI(); }

        // ---------------------------------------------------------------------------- Ki Fist
        public bool HasKiFist { get { return Ki && UsableArm >= 0; } }
        public KiFistHand Fist(int arm) { return arm >= 0 && arm < 2 ? fists[arm] : null; }

        public bool BeginFist(int arm)
        {
            if (!Ki || KiSpent || !Equip(LoadoutFist, arm)) return false;
            var fist = fists[arm];
            if (!fist) return false;
            if (fist.IsCharged || fist.IsCharging) return true;
            return fist.BeginHeldCharge();
        }

        public void ArmFist(int arm) { var fist = Fist(arm); if (fist && fist.IsCharging) fist.ReleaseHeldCharge(); }
        public void DropFist(int arm) { var fist = Fist(arm); if (fist && (fist.IsCharging || fist.IsCharged)) fist.Cancel(); }

        // -------------------------------------------------------------------------- ki charge
        public bool Charging { get { return Charger && Charger.IsActivelyCharging; } }

        public void ChargeKi(bool hold)
        {
            if (!Charger) return;
            if (hold) ledger.Record(AiRestoreLedger.Kind.KiCharge, Charger);
            Charger.SetHoldDesired(hold);
        }

        // --------------------------------------------------------------------- transformation
        public bool CanTransform(int steps)
        {
            if (!Menu || FormKeys.Count < 2 || Transforming || !person.IsAlive() || KiSpent) return false;
            int index = FormIndex;
            return index >= 0 && index + steps < FormKeys.Count && index + steps >= 0;
        }

        public bool Transform(int steps)
        {
            if (!CanTransform(steps)) return false;
            int index = Mathf.Clamp(FormIndex + steps, 0, FormKeys.Count - 1);
            try { Menu.ApplyFormWithTitle(FormKeys[index]); }
            catch (Exception ex) { Debug.LogWarning("[Singularity] AI transform: " + ex.Message); return false; }
            return true;
        }

        public float FormPower(int index) { return index >= 0 && index < FormPowers.Count ? FormPowers[index] : 1f; }

        // Everything the AI switched on that is not a lasting result of the fight goes off with it.
        public void LetGo()
        {
            StopBarrage();
            if (Charger) Charger.SetHoldDesired(false);
            for (int arm = 0; arm < 2; arm++) { ReleaseAim(arm); }
            loadout = LoadoutNone; snapshotTaken = false; BeamArm = -1;
        }
    }
}
