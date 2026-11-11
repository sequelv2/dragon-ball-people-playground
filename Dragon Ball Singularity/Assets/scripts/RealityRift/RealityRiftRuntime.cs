using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DBPU
{
    public enum RealityRiftPhase { Hitstop, Fracturing, Dimension, Exiting }

    // A rift belongs to one real clash and two fighters. World gravity, maps,
    // unrelated actors and the user's pause state are never changed.
    [DefaultExecutionOrder(340)]
    public sealed class RealityRift : MonoBehaviour
    {
        public const float HitstopDuration=.5f,FracturingDuration=.55f,DimensionDuration=18f,ExitDuration=.7f;
        public static RealityRift Active {get;private set;}
        public RealityRiftPhase Phase {get;private set;}
        public float PhaseAge {get;private set;}
        public float DimensionAge {get;private set;}
        public Vector2 Centre {get;private set;}
        public PersonBehaviour Attacker {get;private set;}
        public PersonBehaviour Defender {get;private set;}
        public int Seed {get;private set;}
        public float Intensity {get;private set;}
        public bool Frozen {get{return MenusOrPause();}}
        public int ParticipantBodyCount {get{return bodies.Count;}}
        public bool PhysicsApplied {get;private set;}
        public static double PowerThreshold {get{return GogetaSsj1Threshold();}}
        public static bool ReferenceReady {get{double value=PowerThreshold;return !double.IsInfinity(value) && !double.IsNaN(value) && value>0;}}
        public static string ReferenceDiagnostics {get;private set;}

        sealed class BodyState
        {
            public Rigidbody2D Body;public float Gravity,Drag,AngularDrag;
            public bool Applied;
        }
        readonly List<BodyState> bodies=new List<BodyState>(32);
        Camera view;Behaviour cameraControl;
        Vector3 savedCameraPosition,lastCameraPosition;
        float savedCameraSize,lastCameraSize,savedTimeScale;
        bool savedCameraControl,ownsCameraControl,ownsTimeScale,ownsGlobalTimeTarget,started,cleaned;
        Global timeOwner;float savedGlobalTimeTarget;
        static readonly FieldInfo globalTimeTarget=typeof(Global).GetField("timeScaleTarget",BindingFlags.NonPublic|BindingFlags.Instance);
        int sceneHandle;
        const float OwnedSlowScale=.05f;
        static float cooldownUntil;
        static int lastClashFrame=-1,lastClashA,lastClashB;
        static bool referenceLogged;

        public static void Initialize(string path){RealityRiftAssets.Initialize(path);}

        static double MeanInitialHealth(PersonBehaviour person)
        {
            if(!person || person.Limbs==null)return 0;
            double sum=0;int count=0;
            foreach(var limb in person.Limbs)
                if(limb){sum+=Math.Max(0,KiCombatMath.Finite(limb.InitialHealth));count++;}
            return sum/Math.Max(1,count);
        }
        static double GogetaSsj1Threshold()
        {
            double human=0;int serializedCount=0,childCount=0;double healthTotal=0,initialTotal=0;
            var spawnable=ModAPI.FindSpawnable("Human");
            if(spawnable && spawnable.Prefab)
            {
                var template=spawnable.Prefab.GetComponent<PersonBehaviour>();
                serializedCount=template && template.Limbs!=null?template.Limbs.Length:0;
                // Native PersonBehaviour.Awake builds Limbs with this traversal.
                // Its prefab has not run Awake, so Person.Limbs cannot be used.
                var children=spawnable.Prefab.GetComponentsInChildren<LimbBehaviour>(true);
                foreach(var limb in children)
                {
                    // Native LimbBehaviour.Awake assigns InitialHealth=Health.
                    if(!limb)continue;childCount++;
                    healthTotal+=Math.Max(0,KiCombatMath.Finite(limb.Health));
                    initialTotal+=Math.Max(0,KiCombatMath.Finite(limb.InitialHealth));
                }
                if(childCount>0)human=healthTotal/childCount;
            }
            ReferenceDiagnostics="serializedPersonLimbs="+serializedCount+" actualChildLimbs="+childCount+" serializedHealthSum="+healthTotal+" prefabInitialHealthSum="+initialTotal+" startingHealthMean="+human;
            if(!referenceLogged){referenceLogged=true;Debug.Log("[DBPU Reality Rift Reference] "+ReferenceDiagnostics);}
            // An unreadable reference must never lower the gate to a made-up
            // human health value. No live objects are created for calibration.
            if(childCount==0 || human<=0 || double.IsNaN(human) || double.IsInfinity(human))return double.PositiveInfinity;
            float form=CharacterPowerCatalog.GetDefaultPower("BM/Gogeta","SSJ1",50);
            foreach(var custom in CustomTransformationCatalog.GetForms("BM/Gogeta"))
                if(custom!=null && custom.ID=="SSJ1" && custom.PowerMultiplier>0){form=custom.PowerMultiplier;break;}
            form=CharacterPowerCatalog.GetEffectivePower("BM/Gogeta","SSJ1",form);
            float healthSetting=KiCombatMath.Finite(DBPU.HealthMultiplier);
            if(!Finite(form) || form<=0 || healthSetting<=0)return double.PositiveInfinity;
            return human*5e22*healthSetting*form;
        }

        // Maximum health already contains form, suppression, Kaioken and support
        // health adjustments. Remove those applied factors, then compose actual
        // released combat power once. Current reserve does not lower this gate.
        public static double EffectivePower(PersonBehaviour person)
        {
            if(!person || !person.IsAlive())return 0;
            var mult=person.GetComponent<MultipliersAndStuffs>();
            if(!mult || !person.GetComponent<KiSystem>())return 0;
            var support=person.GetComponent<AllyKiReservoir>();
            double form=Math.Max(.0001,KiCombatMath.Finite(mult.FormMultiplier,1));
            double health=MeanInitialHealth(person)/Math.Max(.001,CombatPowerController.HealthScale(person))
                /Math.Max(1,CombatPowerController.KaiokenHealthScale(person))
                /Math.Max(1,support?KiCombatMath.Finite(support.appliedHealth,1):1)/form;
            double power=health*form*Math.Max(0,KiCombatMath.Finite(mult.BoostMultiplier,1))
                *Math.Max(0,KiCombatMath.Finite(mult.KaiokenMultiplier,1))
                *Math.Max(0,KiCombatMath.Finite(mult.RageMultiplier,1))
                *Math.Max(0,KiCombatMath.Finite(mult.MajinMultiplier,1))
                *Math.Max(0,KiCombatMath.Finite(mult.ExponentialMultiplier,1))
                *Math.Max(0,KiCombatMath.Finite(mult.OtherMultiplier,1))
                *Math.Max(1,support?KiCombatMath.Finite(support.PowerMultiplier,1):1)
                *Math.Max(.01,CombatPowerController.OutputFor(person))*Math.Max(.01,CombatPowerController.Mode(person));
            return double.IsNaN(power)||power<=0?0:Math.Min(1e250,power);
        }
        public static bool PowerEligible(PersonBehaviour a,PersonBehaviour b)
        {
            if(!a || !b || a==b || a.transform.root==b.transform.root)return false;
            double threshold=PowerThreshold;
            if(double.IsInfinity(threshold) || double.IsNaN(threshold) || threshold<=0)return false;
            return EffectivePower(a)>=threshold*.99999 && EffectivePower(b)>=threshold*.99999;
        }
        static bool MenusOrPause()
        {
            return !Global.main || Global.main.Paused || Time.timeScale<=0 || Global.main.GetPausedMenu()
                || Global.ActiveUiBlock || DialogBox.IsAnyDialogboxOpen
                || CombatAbilityWheel.AnyMenuCaptured || CombatTransformationWheel.InputCaptured;
        }
        static bool PairMatches(PersonBehaviour a,PersonBehaviour b,PersonBehaviour c,PersonBehaviour d)
        {return (a==c && b==d)||(a==d && b==c);}
        static bool AlreadyConsumedThisFrame(PersonBehaviour a,PersonBehaviour b)
        {
            if(lastClashFrame!=Time.frameCount || !a || !b)return false;
            int x=a.GetInstanceID(),y=b.GetInstanceID();
            return (x==lastClashA && y==lastClashB)||(x==lastClashB && y==lastClashA);
        }
        static void RecordClash(PersonBehaviour a,PersonBehaviour b)
        {lastClashFrame=Time.frameCount;lastClashA=a.GetInstanceID();lastClashB=b.GetInstanceID();}

        // This public entry still requires live, opposed attacking components at
        // the supplied contact point. Merely meeting a power number cannot open it.
        public static bool TryBegin(PersonBehaviour a,PersonBehaviour b,Vector2 point)
        {
            if(!PowerEligible(a,b) || MenusOrPause())return false;
            foreach(var first in a.GetComponentsInChildren<KiFistHand>())
                foreach(var second in b.GetComponentsInChildren<KiFistHand>())
                    if(TryFistClash(first,second,point))return true;
            return TryBeamClash(a.GetComponent<BeamBehaviour>(),b.GetComponent<BeamBehaviour>(),point);
        }
        public static bool TryFistClash(KiFistHand a,KiFistHand b,Vector2 point)
        {
            if(!a || !b || !a.Person || !b.Person || a.Person==b.Person)return false;
            if(AlreadyConsumedThisFrame(a.Person,b.Person))return true;
            if(!a.RealityStrikeAvailable || !b.RealityStrikeAvailable || MenusOrPause() || !PowerEligible(a.Person,b.Person))return false;
            Vector2 va=a.RealityApproachVelocity,vb=b.RealityApproachVelocity;
            var ba=a.Limb.GetComponent<Rigidbody2D>();var bb=b.Limb.GetComponent<Rigidbody2D>();
            if(!ba || !bb)return false;
            Vector2 axis=bb.worldCenterOfMass-ba.worldCenterOfMass;
            if(axis.sqrMagnitude<.0004f)axis=(Vector2)b.Person.transform.position-(Vector2)a.Person.transform.position;
            if(axis.sqrMagnitude<.0004f)return false;axis.Normalize();
            float closing=Vector2.Dot(va-vb,axis);
            if(Vector2.Dot(va,axis)<.65f || Vector2.Dot(vb,-axis)<.65f || closing<4.5f
                || Vector2.Dot(va.normalized,vb.normalized)>-.35f)return false;
            var ca=a.Limb.GetComponent<Collider2D>();var cb=b.Limb.GetComponent<Collider2D>();
            if(!ca || !cb || !ca.enabled || !cb.enabled)return false;
            float sweepAllowance=Mathf.Clamp(closing*Time.fixedDeltaTime+.12f,.18f,2.8f);
            if(ca.Distance(cb).distance>sweepAllowance || ca.bounds.SqrDistance(point)>sweepAllowance*sweepAllowance
                || cb.bounds.SqrDistance(point)>sweepAllowance*sweepAllowance)return false;
            if(Active)
            {
                if(!PairMatches(a.Person,b.Person,Active.Attacker,Active.Defender)
                    || Active.Phase!=RealityRiftPhase.Dimension || Active.DimensionAge<2
                    || (!a.IsOvercharged && !b.IsOvercharged && closing<12))return false;
                if(!a.CommitRealityClash() || !b.CommitRealityClash())return false;
                RecordClash(a.Person,b.Person);
                Active.RequestExit(a.Person,b.Person,point,true);return true;
            }
            if(Time.realtimeSinceStartup<cooldownUntil)return false;
            if(!BeginValidated(a.Person,b.Person,point))return false;
            if(!a.CommitRealityClash() || !b.CommitRealityClash()){if(Active)Active.EndImmediately();return false;}
            RecordClash(a.Person,b.Person);return true;
        }
        public static bool TryBeamClash(BeamBehaviour a,BeamBehaviour b,Vector2 point)
        {
            if(!a || !b || a==b || !a.Firing || !b.Firing || !a.StillActive || !b.StillActive
                || !a.Clashing || !b.Clashing || a.ClashOpponent!=b || b.ClashOpponent!=a)return false;
            var first=a.GetComponent<PersonBehaviour>();var second=b.GetComponent<PersonBehaviour>();
            if(!PowerEligible(first,second) || MenusOrPause() || Vector3.Dot(a.BeamDirection.normalized,b.BeamDirection.normalized)>-.5f)return false;
            float contactAllowance=Mathf.Max(1.5f,(a.Width+b.Width)*.5f+1.5f);
            if(Vector2.Distance(a.EndBeamPosition,point)>contactAllowance || Vector2.Distance(b.EndBeamPosition,point)>contactAllowance)return false;
            if(AlreadyConsumedThisFrame(first,second))return true;
            if(Active)
            {
                if(!PairMatches(first,second,Active.Attacker,Active.Defender))return false;
                return Active.RequestExit(first,second,point,a.Charge>=a.MaxCharge*.85f && b.Charge>=b.MaxCharge*.85f);
            }
            if(Time.realtimeSinceStartup<cooldownUntil || !BeginValidated(first,second,point))return false;
            RecordClash(first,second);return true;
        }

        static bool BeginValidated(PersonBehaviour a,PersonBehaviour b,Vector2 point)
        {
            if(Active || !Finite(point.x) || !Finite(point.y) || !Global.main || !Global.main.camera || !RealityRiftAssets.Ready)return false;
            var go=new GameObject("DBPU reality fracture from opposing combat powers");
            var rift=go.AddComponent<RealityRift>();
            try
            {
                rift.Attacker=a;rift.Defender=b;rift.Centre=point;
                rift.Seed=unchecked(a.GetInstanceID()*397^b.GetInstanceID()^Time.frameCount*7919);
                rift.Intensity=Mathf.Clamp(1+(float)Math.Log10(Math.Max(1,Math.Min(EffectivePower(a),EffectivePower(b))/PowerThreshold))*.16f,1,2.4f);
                rift.CaptureState();Active=rift;rift.started=true;
                RealityRiftVisual.Create(rift);
                Debug.Log("[DBPU Reality Rift] Actual opposing clash opened: power "+EffectivePower(a)+" / "+EffectivePower(b)+", Gogeta SSJ1 threshold "+PowerThreshold);
                return true;
            }
            catch(Exception ex){Debug.LogError("[DBPU Reality Rift] "+ex);rift.EndImmediately();return false;}
        }
        static bool Finite(float value){return !float.IsNaN(value) && !float.IsInfinity(value);}
        void CaptureState()
        {
            sceneHandle=SceneManager.GetActiveScene().handle;Phase=RealityRiftPhase.Hitstop;PhaseAge=DimensionAge=0;
            view=Global.main.camera;savedCameraPosition=lastCameraPosition=view.transform.position;
            savedCameraSize=lastCameraSize=view.orthographicSize;
            cameraControl=Global.main.CameraControlBehaviour;
            if(cameraControl){savedCameraControl=cameraControl.enabled;if(savedCameraControl){cameraControl.enabled=false;ownsCameraControl=true;}}
            if(globalTimeTarget==null)throw new InvalidOperationException("Native Global.timeScaleTarget is unavailable; refusing an unowned cinematic time override.");
            timeOwner=Global.main;savedGlobalTimeTarget=(float)globalTimeTarget.GetValue(timeOwner);
            globalTimeTarget.SetValue(timeOwner,OwnedSlowScale);ownsGlobalTimeTarget=true;
            savedTimeScale=Time.timeScale;Time.timeScale=OwnedSlowScale;ownsTimeScale=true;
            CaptureBodies(Attacker);CaptureBodies(Defender);
            SceneManager.activeSceneChanged+=SceneChanged;
        }
        void CaptureBodies(PersonBehaviour person)
        {
            if(!person || person.Limbs==null)return;
            foreach(var limb in person.Limbs)
            {
                var body=limb?limb.GetComponent<Rigidbody2D>():null;if(!body)continue;
                bool duplicate=false;foreach(var saved in bodies)if(saved.Body==body){duplicate=true;break;}
                if(!duplicate)bodies.Add(new BodyState{Body=body,Gravity=body.gravityScale,Drag=body.drag,AngularDrag=body.angularDrag});
            }
        }
        void SceneChanged(Scene before,Scene after){if(started && after.handle!=sceneHandle)EndImmediately();}
        void Update()
        {
            if(!started || cleaned)return;
            if(!Attacker || !Defender || !Attacker.IsAlive() || !Defender.IsAlive()
                || !Global.main || !view || SceneManager.GetActiveScene().handle!=sceneHandle){EndImmediately();return;}
            if(Frozen)return;
            if(Phase==RealityRiftPhase.Hitstop && ownsGlobalTimeTarget && timeOwner)
            {
                // Global.Update normally smooths actual time toward its private
                // target every frame. Owning that target prevents a tug-of-war;
                // native slow-motion controls can revoke this ownership.
                if(Mathf.Approximately((float)globalTimeTarget.GetValue(timeOwner),OwnedSlowScale))Time.timeScale=OwnedSlowScale;
                else {ownsGlobalTimeTarget=false;ownsTimeScale=false;}
            }
            float dt=Mathf.Max(0,KiCombatMath.Finite(Time.unscaledDeltaTime));PhaseAge+=dt;
            if(Phase==RealityRiftPhase.Hitstop && PhaseAge>=HitstopDuration)
            {PhaseAge-=HitstopDuration;Phase=RealityRiftPhase.Fracturing;RestoreOwnedTimeScale();}
            if(Phase==RealityRiftPhase.Fracturing && PhaseAge>=FracturingDuration)
            {PhaseAge-=FracturingDuration;Phase=RealityRiftPhase.Dimension;ApplyDimensionPhysics();}
            if(Phase==RealityRiftPhase.Dimension)
            {
                DimensionAge=PhaseAge;
                if(DimensionAge>=DimensionDuration){PhaseAge-=DimensionDuration;BeginExit();}
            }
            if(Phase==RealityRiftPhase.Exiting && PhaseAge>=ExitDuration)EndImmediately();
        }
        void LateUpdate()
        {
            if(!started || cleaned || Frozen || !view)return;
            float t=Phase==RealityRiftPhase.Hitstop?Mathf.Clamp01(PhaseAge/HitstopDuration):Phase==RealityRiftPhase.Fracturing?Mathf.Clamp01(PhaseAge/FracturingDuration):Phase==RealityRiftPhase.Exiting?Mathf.Clamp01(PhaseAge/ExitDuration):1;
            Vector3 centre=new Vector3(Centre.x,Centre.y,savedCameraPosition.z);
            Vector3 targetPosition;float targetSize;
            if(Phase==RealityRiftPhase.Hitstop)
            {
                float ease=Mathf.SmoothStep(0,1,t);
                targetPosition=Vector3.Lerp(savedCameraPosition,centre,ease*.88f);
                targetSize=Mathf.Lerp(savedCameraSize,Mathf.Max(2.6f,savedCameraSize*.72f),ease);
                float shake=Mathf.Sin(PhaseAge*191+Seed*.001f)*.026f*Intensity*Mathf.Sin(t*Mathf.PI);
                targetPosition+=new Vector3(shake,Mathf.Cos(PhaseAge*173)*shake*.65f,0);
            }
            else if(Phase==RealityRiftPhase.Exiting)
            {
                float ease=Mathf.SmoothStep(0,1,t);
                targetPosition=Vector3.Lerp(centre,savedCameraPosition,ease);
                targetSize=Mathf.Lerp(Mathf.Max(4,savedCameraSize*1.12f),savedCameraSize,ease);
            }
            else
            {
                targetPosition=centre;targetSize=Mathf.Max(4,savedCameraSize*1.12f);
                if(Phase==RealityRiftPhase.Fracturing)
                {
                    float ease=Mathf.SmoothStep(0,1,t);
                    targetPosition=Vector3.Lerp(Vector3.Lerp(savedCameraPosition,centre,.88f),centre,ease);
                    targetSize=Mathf.Lerp(Mathf.Max(2.6f,savedCameraSize*.72f),targetSize,ease);
                }
            }
            lastCameraPosition=targetPosition;lastCameraSize=targetSize;
            view.transform.position=targetPosition;view.orthographicSize=targetSize;
        }
        void ApplyDimensionPhysics()
        {
            PhysicsApplied=true;
            foreach(var saved in bodies)
            {
                if(!saved.Body)continue;
                saved.Body.gravityScale=0;saved.Body.drag=Mathf.Max(.08f,saved.Drag);saved.Body.angularDrag=Mathf.Max(.08f,saved.AngularDrag);saved.Applied=true;
            }
        }
        void FixedUpdate()
        {
            if(!started || cleaned || Phase!=RealityRiftPhase.Dimension || Frozen || !view)return;
            float halfHeight=Mathf.Max(2,view.orthographicSize*.86f),halfWidth=Mathf.Max(2,view.orthographicSize*view.aspect*.88f);
            foreach(var saved in bodies)
            {
                var body=saved.Body;if(!body)continue;
                // A native transformation can restore its pre-charge gravity
                // while the dimension is active. Remember that new intended
                // value, and retain the dimension's zero-gravity rule.
                if(saved.Applied && !Mathf.Approximately(body.gravityScale,0))
                {saved.Gravity=body.gravityScale;body.gravityScale=0;}
                if(body.bodyType!=RigidbodyType2D.Dynamic)continue;
                Vector2 offset=body.position-Centre,velocity=Vector2.ClampMagnitude(body.velocity,65);
                Vector2 force=Vector2.zero;
                if(Mathf.Abs(offset.x)>halfWidth)
                {
                    float sign=Mathf.Sign(offset.x),excess=Mathf.Abs(offset.x)-halfWidth;
                    force.x=-sign*body.mass*(excess*32+Mathf.Max(0,velocity.x*sign)*7);
                    if(velocity.x*sign>0)velocity.x=-sign*Mathf.Max(3,Mathf.Abs(velocity.x)*.78f);
                }
                if(Mathf.Abs(offset.y)>halfHeight)
                {
                    float sign=Mathf.Sign(offset.y),excess=Mathf.Abs(offset.y)-halfHeight;
                    force.y=-sign*body.mass*(excess*32+Mathf.Max(0,velocity.y*sign)*7);
                    if(velocity.y*sign>0)velocity.y=-sign*Mathf.Max(3,Mathf.Abs(velocity.y)*.78f);
                }
                body.velocity=velocity;body.AddForce(force,ForceMode2D.Force);
            }
        }
        public bool RequestExit(PersonBehaviour a,PersonBehaviour b,Vector2 point,bool strongHit)
        {
            if(cleaned || !started || Phase!=RealityRiftPhase.Dimension || DimensionAge<2 || !strongHit
                || !PairMatches(a,b,Attacker,Defender) || !PowerEligible(a,b) || !Finite(point.x) || !Finite(point.y))return false;
            PhaseAge=0;BeginExit();return true;
        }
        void BeginExit(){Phase=RealityRiftPhase.Exiting;RestoreBodyProperties();RestoreOwnedTimeScale();}
        void RestoreBodyProperties()
        {
            foreach(var saved in bodies)
            {
                if(!saved.Body || !saved.Applied)continue;
                // Restore only properties still owned by the dimension, leaving a
                // concurrent user/tool adjustment intact.
                if(Mathf.Approximately(saved.Body.gravityScale,0))saved.Body.gravityScale=saved.Gravity;
                if(Mathf.Approximately(saved.Body.drag,Mathf.Max(.08f,saved.Drag)))saved.Body.drag=saved.Drag;
                if(Mathf.Approximately(saved.Body.angularDrag,Mathf.Max(.08f,saved.AngularDrag)))saved.Body.angularDrag=saved.AngularDrag;
                saved.Applied=false;
            }
            PhysicsApplied=false;
        }
        void RestoreOwnedTimeScale()
        {
            // Restoring the native target is safe while paused: Global continues
            // targeting zero until the user resumes, then uses their prior rate.
            if(ownsGlobalTimeTarget && timeOwner && globalTimeTarget!=null
                && Mathf.Approximately((float)globalTimeTarget.GetValue(timeOwner),OwnedSlowScale))
                globalTimeTarget.SetValue(timeOwner,savedGlobalTimeTarget);
            ownsGlobalTimeTarget=false;
            if(ownsTimeScale && Time.timeScale>0 && (!Global.main || (!Global.main.Paused && !Global.main.GetPausedMenu()))
                && Mathf.Approximately(Time.timeScale,OwnedSlowScale))Time.timeScale=savedTimeScale;
            ownsTimeScale=false;
        }
        public void EndImmediately()
        {
            if(cleaned)return;cleaned=true;
            RestoreBodyProperties();RestoreOwnedTimeScale();
            if(view)
            {
                if(Vector3.Distance(view.transform.position,lastCameraPosition)<.01f)view.transform.position=savedCameraPosition;
                if(Mathf.Abs(view.orthographicSize-lastCameraSize)<.001f)view.orthographicSize=savedCameraSize;
            }
            if(cameraControl && ownsCameraControl && !cameraControl.enabled)cameraControl.enabled=savedCameraControl;
            SceneManager.activeSceneChanged-=SceneChanged;
            if(Active==this)Active=null;cooldownUntil=Time.realtimeSinceStartup+3;
            Destroy(gameObject);
        }
        void OnDisable(){if(started)EndImmediately();}
        void OnDestroy(){if(started && !cleaned)EndImmediately();}
    }
}
