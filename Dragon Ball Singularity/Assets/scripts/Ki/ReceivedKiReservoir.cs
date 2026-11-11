using System;
using UnityEngine;

namespace DBPU
{
    // The historical component name remains compatible with the hand controllers.
    // Receiving ki never requires a faction, class, character type or alliance.
    public sealed class AllyKiReservoir : MonoBehaviour
    {
        public float SupportEnergy;
        public float restoredDonation, observedKi=-1;
        public float TotalReceived { get { SyncRestored(); return Mathf.Max(0,SupportEnergy)+restoredDonation; } }
        public int TeamId; // Legacy serialized data is ignored.
        public PersonBehaviour Person { get { EnsureRefs(); return person; } }
        public float ReferenceKi { get { EnsureRefs(); return ki ? KiCombatMath.MaxKi(ki) : 1000f; } }
        public float Capacity { get { return 1e32f; } }
        public float Support01 { get { return Mathf.Clamp01(StoredBars); } }
        public float StoredBars { get { return TotalReceived/Mathf.Max(1,ReferenceKi); } }
        public bool OverdriveActive { get; private set; }
        public float PowerMultiplier { get { return KiEnergyMath.SupportMultiplier(TotalReceived,ReferenceKi) * (OverdriveActive ? 2f : 1f); } }
        public float HealthMultiplier { get { return KiEnergyMath.HealthMultiplier(TotalReceived,ReferenceKi) * (OverdriveActive ? 1.5f : 1f); } }
        KiSystem ki;
        PersonBehaviour person;
        KiOverdriveAura surge;
        public float appliedHealth = 1f;
        float sinceDonation = 100f, outgoingThisFrame;
        int outgoingFrame = -1;

        public static AllyKiReservoir GetOrCreate(PersonBehaviour person)
        {
            if (!person) return null;
            var result = person.GetComponent<AllyKiReservoir>();
            if (!result) result = person.gameObject.AddComponent<AllyKiReservoir>();
            result.EnsureRefs(); return result;
        }
        public static bool AreAllies(PersonBehaviour a, PersonBehaviour b) { return false; }
        public void SetTeam(int ignored) { } // Compatibility only; no menu or eligibility check.
        void Awake() { EnsureRefs(); }
        void EnsureRefs()
        {
            if (!person) person = GetComponent<PersonBehaviour>();
            if (!ki) ki = GetComponent<KiSystem>();
        }
        void SyncRestored()
        {
            EnsureRefs();if(!ki)return;
            float current=Mathf.Max(0,KiCombatMath.Finite(ki.currentKi));
            if(observedKi>=0 && current<observedKi)restoredDonation=Mathf.Max(0,restoredDonation-(observedKi-current));
            restoredDonation=Mathf.Min(restoredDonation,current);observedKi=current;
        }
        void Update()
        {
            EnsureRefs();
            if (!person || !person.IsAlive()) { SupportEnergy = restoredDonation = 0; Reconcile(); return; }
            if (!Global.main || Global.main.Paused || Time.timeScale <= 0) return;
            SupportStep(Time.deltaTime);
        }
        public void SupportStep(float dt)
        {
            EnsureRefs(); dt = KiCombatMath.Step(dt);
            SyncRestored();
            SupportEnergy = Mathf.Clamp(KiCombatMath.Finite(SupportEnergy),0,Capacity);
            float before = sinceDonation; sinceDonation = Mathf.Min(100,sinceDonation+dt);
            // A charge-up window, then a slow exponential release of excess energy.
            float decay = before >= 12 ? dt : Mathf.Max(0,sinceDonation-12);
            SupportEnergy *= Mathf.Exp(-decay*.008f);
            if (SupportEnergy < .0001f) SupportEnergy = 0;
            Reconcile();
        }
        void Reconcile()
        {
            if (!person) return;
            if (StoredBars >= 1f) OverdriveActive = true;
            else if (StoredBars < .65f) OverdriveActive = false;
            float wanted = Mathf.Max(1,HealthMultiplier);
            if (Mathf.Abs(wanted/appliedHealth-1) > .000001f && person.Limbs != null)
            {
                float ratio = wanted/appliedHealth;
                foreach (var limb in person.Limbs)
                {
                    if (!limb || limb.InitialHealth <= 0) continue;
                    if (GogitoVitality.Scale(limb,ratio)) continue;
                    double fraction = (double)limb.Health/limb.InitialHealth;
                    limb.InitialHealth = KiCombatMath.Cap((double)limb.InitialHealth*ratio,1e35f);
                    limb.Health = KiCombatMath.Cap(limb.InitialHealth*Math.Max(0,fraction),1e35f);
                }
                appliedHealth = wanted;
                MCP_Utils.InvalidatePersonStatCache(person);
            }
            var strength = person.GetComponent<MultipliersAndStuffs>();
            if (!strength && SupportEnergy > 0) strength = person.gameObject.AddComponent<MultipliersAndStuffs>();
            if (strength) strength.Recalculate();
            if (OverdriveActive && !surge) surge = KiOverdriveAura.Bind(person,this);
            if (surge) surge.enabled = OverdriveActive;
        }
        public float AvailableDonation(float dt)
        {
            EnsureRefs(); if (!ki) return 0;
            if (outgoingFrame != Time.frameCount) { outgoingFrame = Time.frameCount; outgoingThisFrame = 0; }
            return Mathf.Max(0,KiCombatMath.TransferBudget(ki.currentKi,ReferenceKi,dt)-outgoingThisFrame);
        }
        public float DonateTo(AllyKiReservoir receiver,float dt)
        {
            EnsureRefs();
            if (!receiver || receiver == this || !person || !receiver.Person || !person.IsAlive() || !receiver.Person.IsAlive()
                || Vector2.Distance(person.transform.position,receiver.Person.transform.position)>KiShareHand.Range+2) return 0;
            var gathering = receiver.Person.GetComponent<SpiritBombChannel>();
            if (gathering && gathering.IsCharging) return DonateToBomb(gathering.ChargingBomb, dt);
            float offered = AvailableDonation(dt); if (offered<=0) return 0;
            float accepted = receiver.AcceptDonation(offered);
            if (accepted<=0) return 0;
            ki.currentKi = Mathf.Max(ReferenceKi*.2f,ki.currentKi-accepted);
            outgoingThisFrame += accepted; return accepted;
        }
        public float DonateToBomb(SpiritBombProjectile bomb, float dt)
        {
            EnsureRefs();
            if (!bomb || !bomb.CanReceive || !person || !person.IsAlive() || !ki) return 0;
            // Distance is measured to the surface, so enormous spheres remain targetable.
            if (Vector2.Distance(person.transform.position, bomb.transform.position) - bomb.Radius > KiShareHand.Range + 2) return 0;
            float offered = AvailableDonation(dt); if (offered <= 0) return 0;
            float accepted = bomb.AddEnergy(offered, true); if (accepted <= 0) return 0;
            ki.currentKi = Mathf.Max(ReferenceKi * .2f, ki.currentKi - accepted);
            outgoingThisFrame += accepted; return accepted;
        }
        float AcceptDonation(float offered)
        {
            EnsureRefs(); if (!person || !person.IsAlive()) return 0;
            SyncRestored();
            float missing = ki ? Mathf.Max(0,ReferenceKi-KiCombatMath.Finite(ki.currentKi)) : 0;
            var plan = KiEnergyMath.PlanDonation(offered,missing,Mathf.Max(0,Capacity-SupportEnergy));
            if (ki)
            {
                ki.currentKi = Mathf.Min(ReferenceKi,Mathf.Max(0,ki.currentKi)+plan.Restore);
                restoredDonation+=plan.Restore;observedKi=ki.currentKi;
            }
            SupportEnergy += plan.Support;
            if (plan.Accepted>0) sinceDonation = 0;
            Reconcile(); return plan.Accepted;
        }
        public float ConsumeSupport(float requested)
        {
            SyncRestored();
            float used = Mathf.Min(Mathf.Max(0,KiCombatMath.Finite(requested)),TotalReceived);
            float stored=Mathf.Min(used,Mathf.Max(0,SupportEnergy));SupportEnergy-=stored;
            float ordinary=used-stored;restoredDonation=Mathf.Max(0,restoredDonation-ordinary);
            if(ki){ki.currentKi=Mathf.Max(0,ki.currentKi-ordinary);observedKi=ki.currentKi;}
            Reconcile(); return used;
        }
        public void ReturnSupport(float energy)
        {
            if (!Person || !Person.IsAlive()) return;
            SupportEnergy = Mathf.Min(Capacity,SupportEnergy+Mathf.Max(0,KiCombatMath.Finite(energy)));
            Reconcile();
        }
        void OnDisable()
        {
            if (surge) surge.enabled = false;
        }
    }
}
