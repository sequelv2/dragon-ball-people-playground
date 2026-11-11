using System;
using UnityEngine;

namespace DBPU
{
    // One coordinator per character. Latest request replaces the pending commit, never stacks coroutines.
    public sealed class KiFormTransition : MonoBehaviour
    {
        public bool Busy { get; private set; }
        public bool Committing { get; private set; }
        PersonBehaviour person;
        RefinedAuraVisual veil;
        Action apply;
        string destination;
        float elapsed;
        bool applied;
        public static bool DefersAura(PersonBehaviour p)
        {
            var t=p!=null ? p.GetComponent<KiFormTransition>() : null;
            return t!=null && t.Busy && !t.applied && !t.Committing;
        }
        public static bool Begin(CharacterTransformations owner, PersonBehaviour p, string previous, string next, Action commit)
        {
            if(p==null)return false;
            var t=p.GetComponent<KiFormTransition>();
            if(t!=null&&t.Committing)return false;
            if (!p.gameObject.activeInHierarchy || !p.IsAlive() || !DBPU.AurasEnabled || previous==next || string.IsNullOrEmpty(previous) || owner==null || owner.transformations==null)
            {if(t!=null)t.Cancel();return false;}
            var data=owner.transformations.TransformationData;
            if (next==null||!data.ContainsKey(next)) {if(t!=null)t.Cancel();return false;}
            string from=data.ContainsKey(previous) ? data[previous].AuraID : "Base";
            string to=data[next].AuraID;
            if (!(RefinedAuras.Contains(from) && from!="Base") && !(RefinedAuras.Contains(to) && to!="Base")) {if(t!=null)t.Cancel();return false;}
            if(t==null) t=p.gameObject.AddComponent<KiFormTransition>();
            if(t.Committing) return false;
            {
                // Restart the destination Animator when a newer request replaces this one.
                var go=RefinedAuras.Create(RefinedAuras.Contains(to)?to:from,true);
                if(go==null) {t.Cancel();return false;} // Never leave an older pending transformation alive on fallback.
                if(t.veil!=null)Destroy(t.veil.gameObject);
                t.veil=go.GetComponent<RefinedAuraVisual>();
            }
            t.person=p; t.apply=commit; t.destination=to;
            t.veil.TargetLook=RefinedAuras.Look(to);
            t.veil.SetStartingLook(RefinedAuras.Look(from));
            t.veil.PaletteMix=0f; t.veil.SpeedMultiplier=1.5f;
            t.veil.ChargeAmount=1;
            t.elapsed=0;
            t.applied=false; t.Busy=true;
            t.Position();
            return true;
        }
        void Position()
        {
            if(person==null || veil==null || person.Limbs==null || person.Limbs.Length<3 || person.Limbs[2]==null) return;
            var handler=person.GetComponent<AuraHandler>();
            float s=Mathf.Max(.01f,Mathf.Abs(person.transform.localScale.x));
            float tune=handler!=null ? Mathf.Max(.1f,handler.AuraTuneScale/3.4f) : 1f;
            veil.transform.localScale=new Vector3(1.25f*s*tune,1.25f*s*tune,1);
            veil.transform.position=veil.RootedAtFeet?veil.FeetRoot(person,tune):person.Limbs[2].transform.position+new Vector3(0,.4f*s+(handler!=null?handler.AuraTuneY+.09f:0),0);
        }
        void Update()
        {
            if(!Busy) return;
            if(person==null || !person.IsAlive() || !DBPU.AurasEnabled) { Cancel(); return; }
            if(Global.main!=null&&Global.main.Paused)return;
            elapsed+=Time.deltaTime;
            // Rise, hold across the actual sprite/ability change, then peel away.
            float cover=elapsed<.14f ? Mathf.SmoothStep(0,1,elapsed/.14f) :
                (elapsed<.31f ? 1f : 1f-Mathf.SmoothStep(0,1,(elapsed-.31f)/.41f));
            if(veil!=null)
            {
                veil.CoverAmount=cover;
                veil.PaletteMix=Mathf.SmoothStep(0,1,(elapsed-.11f)/.16f);
                veil.SetOpacity(1);
                Position();
            }
            if(!applied && elapsed>=.21f)
            {
                applied=true; Committing=true;
                try
                {
                    var handlerBefore=person.GetComponent<AuraHandler>();
                    var auraBefore=handlerBefore!=null ? handlerBefore.CurrentAuraObject : null;
                    var pending=apply; apply=null; if(pending!=null) pending();
                    var handler=person.GetComponent<AuraHandler>();
                    if(handler!=null && handler.AuraActive && handler.AuraVisible &&
                        (handler.CurrentAuraObject==null || handler.CurrentAuraObject==auraBefore)) handler.CreateAura(destination);
                }
                catch(Exception ex) { Debug.LogException(ex); Cancel(); }
                finally { Committing=false; }
            }
            if(elapsed>=.72f) Cancel();
        }
        public void Cancel()
        {
            apply=null; Busy=false; applied=false;
            if(veil!=null) Destroy(veil.gameObject);
            veil=null;
        }
        void OnDisable() { Cancel(); }
        void OnDestroy() { Cancel(); }
    }
}
