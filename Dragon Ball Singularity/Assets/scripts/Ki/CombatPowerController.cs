using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DBPU
{
    public static class CombatPowerMath
    {
        public static readonly float[] Steps={.01f,.1f,.25f,.5f,1};
        public static float Step(float value,int direction)
        {
            int current=Steps.Length-1;
            for(int i=0;i<Steps.Length;i++)if(Math.Abs(Steps[i]-value)<.001f){current=i;break;}
            return Steps[(current+(direction<0?-1:1)+Steps.Length)%Steps.Length];
        }
        public static double Power(double health,double form,double boost,double support,double output,double mode,double reserve)
        {
            double value=health*form*boost*support*output*mode*(.72+.28*Math.Sqrt(Math.Max(0,Math.Min(1,reserve))));
            return double.IsNaN(value)||value<=0?0:Math.Min(1e250,value);
        }
        public static string Format(double power)
        {
            if(double.IsNaN(power)||power<=0)return "0";
            string[] units={"","K","M","B","T","Qa","Qi","Sx","Sp","Oc","No","Dc"};
            int unit=0;while(power>=1000 && unit<units.Length-1){power/=1000;unit++;}
            return power>=1000?power.ToString("0.##e+0",CultureInfo.InvariantCulture)+units[unit]:power.ToString(power>=100?"0":power>=10?"0.#":"0.##",CultureInfo.InvariantCulture)+units[unit];
        }
    }

    [DefaultExecutionOrder(-180)]
    public sealed class CombatPowerController : MonoBehaviour
    {
        public const string ActionId="dbpu.combat.output";
        public const KeyCode DefaultKey=KeyCode.I;
        public float AppliedHealth=1;
        public bool HudVisible=true;
        public float ModeMultiplier {get;private set;}=1;
        public float Charge01 {get;private set;}
        public double PowerLevel {get;private set;}
        public float Health01 {get;private set;}=1;
        public PersonBehaviour Person {get;private set;}
        public KiSystem Ki {get;private set;}
        public MultipliersAndStuffs Multipliers {get;private set;}
        public float Output {get{return OutputFor(Person);}}
        bool menuBound;
        KiChargeModeController charging;
        public static CombatPowerController Bind(PersonBehaviour person)
        {
            if(!person)return null;var result=person.GetComponent<CombatPowerController>();
            if(!result)result=person.gameObject.AddComponent<CombatPowerController>();result.Ensure();return result;
        }
        void Awake(){Ensure();}
        void Ensure()
        {
            if(!Person)Person=GetComponent<PersonBehaviour>();if(!Ki)Ki=GetComponent<KiSystem>();
            if(!Multipliers)Multipliers=GetComponent<MultipliersAndStuffs>();if(!charging)charging=GetComponent<KiChargeModeController>();
        }
        public static float HealthScale(PersonBehaviour person)
        {
            var power=person?person.GetComponent<CombatPowerController>():null;
            return power?Mathf.Max(.001f,power.AppliedHealth):1;
        }
        public static float KaiokenHealthScale(PersonBehaviour person)
        {var kaioken=person?person.GetComponent<Kaioken>():null;return kaioken?kaioken.HealthScale:1;}
        public static float Mode(PersonBehaviour person)
        {var power=person?person.GetComponent<CombatPowerController>():null;return power?power.ModeMultiplier:1;}
        public static float OutputFor(PersonBehaviour person)
        {var mult=person?person.GetComponent<MultipliersAndStuffs>():null;return mult?Mathf.Clamp(KiCombatMath.Finite(mult.KiOutput,1),.01f,1e30f):1;}
        // Native attacks normalise damage by FinalMultiplier. Remove suppression
        // from their health basis once, then use native KiOutput once at discharge.
        public static float AttackHealth(PersonBehaviour person)
        {return KiCombatMath.Cap((double)MCP_Utils.AverageMaxHealth(person)/HealthScale(person)*Mode(person),float.MaxValue);}
        public void Cycle(int direction){SetOutput(CombatPowerMath.Step(Output,direction));}
        public void SetOutput(float output)
        {
            Ensure();if(!Multipliers || !Person || !Person.IsAlive())return;
            float chosen=CombatPowerMath.Steps[0],difference=float.MaxValue;
            foreach(float step in CombatPowerMath.Steps)if(Mathf.Abs(step-output)<difference){difference=Mathf.Abs(step-output);chosen=step;}
            Multipliers.KiOutput=chosen;ReconcileHealth();Multipliers.Recalculate();Refresh();
        }
        void Update()
        {
            Ensure();if(!Person || !Ki || !Multipliers)return;
            BindMenu();
            if(CanReadHeadInput() && DbpuInputUtil.Down(ActionId,DefaultKey))Cycle(Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift)?-1:1);
            if(!Global.main || Global.main.Paused){Refresh();return;}
            Tick(Time.deltaTime);
        }
        public void Tick(float dt)
        {
            Ensure();if(!Person || !Ki || !Multipliers)return;
            dt=Mathf.Clamp(KiCombatMath.Finite(dt),0,.1f);
            float target=charging && charging.IsActivelyCharging?1:0;
            Charge01=Mathf.MoveTowards(Charge01,target,dt*(target>Charge01?.85f:3));
            ModeMultiplier=Ki.SparkingOverloadActive?1.22f:Ki.SparkingExhaustActive?.68f:1+.12f*Charge01;
            ReconcileHealth();Multipliers.Recalculate();Refresh();
        }
        void ReconcileHealth()
        {
            if(!Person || Person.Limbs==null)return;
            float desired=Mathf.Min(1,Output),ratio=desired/Mathf.Max(.001f,AppliedHealth);
            if(Mathf.Abs(ratio-1)<.000001f)return;
            foreach(var limb in Person.Limbs)
            {
                if(!limb || limb.InitialHealth<=0)continue;
                if(GogitoVitality.Scale(limb,ratio))continue;
                double fraction=Math.Max(0,(double)limb.Health/limb.InitialHealth);
                limb.InitialHealth=KiCombatMath.Cap((double)limb.InitialHealth*ratio,float.MaxValue);
                limb.Health=KiCombatMath.Cap(limb.InitialHealth*fraction,float.MaxValue);
            }
            AppliedHealth=desired;MCP_Utils.InvalidatePersonStatCache(Person);
        }
        public void Refresh()
        {
            if(!Person || !Ki || !Multipliers)return;
            var support=Person.GetComponent<AllyKiReservoir>();
            double health=0,current=0;int count=0;
            foreach(var limb in Person.Limbs)if(limb){health+=Math.Max(0,KiCombatMath.Finite(limb.InitialHealth));current+=Math.Max(0,KiCombatMath.Finite(limb.Health));count++;}
            Health01=health>0?Mathf.Clamp01((float)(current/health)):0;
            double form=Math.Max(.0001,Multipliers.FormMultiplier),supportHealth=support?Math.Max(1,support.HealthMultiplier):1;
            // Kaioken already raises native maximum health. Remove that applied
            // health scale before composing its attack multiplier once below.
            double kaiokenHealth=KaiokenHealthScale(Person);
            double basePower=health/Math.Max(1,count)/Math.Max(.001,AppliedHealth)/form/supportHealth/kaiokenHealth;
            double other=(double)Multipliers.BoostMultiplier*Multipliers.KaiokenMultiplier*Multipliers.RageMultiplier*Multipliers.MajinMultiplier*Multipliers.ExponentialMultiplier*Multipliers.OtherMultiplier;
            PowerLevel=CombatPowerMath.Power(basePower,form,other,support?support.PowerMultiplier:1,Output,ModeMultiplier,Ki.currentKi/Mathf.Max(1,Ki.maxKi));
            if(!Person.IsAlive())PowerLevel=0;
        }
        bool CanReadHeadInput()
        {
            if(!Person.IsAlive() || !KiCombatMath.HasFocus || !Global.main || Global.main.GetPausedMenu() || Global.ActiveUiBlock || CombatAbilityWheel.AnyMenuCaptured || DialogBox.IsAnyDialogboxOpen || LiveSpriteEditor.SpriteEditorRuntime.BlocksGameInput)return false;
            if(EventSystem.current && EventSystem.current.IsPointerOverGameObject())return false;
            var focused=EventSystem.current?EventSystem.current.currentSelectedGameObject:null;
            if(focused && (focused.GetComponent<TMP_InputField>() || focused.GetComponent<InputField>()))return false;
            var selection=SelectionController.Main;PhysicalBehaviour physical=selection!=null?selection.CurrentlyUnderMouse:null;
            if(!physical && Global.main.ToolControllerBehaviour && Global.main.ToolControllerBehaviour.CurrentTool)physical=Global.main.ToolControllerBehaviour.CurrentTool.ActiveSingleSelected;
            var limb=physical?physical.GetComponent<LimbBehaviour>():null;
            return limb && limb.Person==Person && limb.name.StartsWith("Head",StringComparison.OrdinalIgnoreCase);
        }
        void BindMenu()
        {
            if(menuBound || !Person)return;var head=Person.transform.Find("Head");var physical=head?head.GetComponent<PhysicalBehaviour>():null;
            if(!physical || physical.ContextMenuOptions==null)return;
            physical.ContextMenuOptions.Buttons.Add(new ContextMenuButton("!CombatHudVisible",()=>HudVisible?"Combat HUD: hide":"Combat HUD: show","Show or hide the live combat gauges",()=>HudVisible=!HudVisible));
            for(int i=0;i<CombatPowerMath.Steps.Length;i++)
            {
                float step=CombatPowerMath.Steps[i];int percent=Mathf.RoundToInt(step*100);
                physical.ContextMenuOptions.Buttons.Add(new ContextMenuButton("!CombatOutput"+percent,"Ki output: "+percent+"%","Adjust actual released attack power and maximum health",()=>SetOutput(step)));
            }
            menuBound=true;
        }
    }
}
