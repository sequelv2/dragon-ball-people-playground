using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DBPU
{
    // Only the inner Output face accepts pointer input; bars and text pass through.
    [DefaultExecutionOrder(-260)]
    public sealed class CombatHudView : MonoBehaviour
    {
        static Canvas canvas;
        static TMP_FontAsset font;
        CombatPowerController power;
        RectTransform panel;
        CanvasGroup group;
        CombatHudGraphic art;
        TextMeshProUGUI number,output,kiText,stmText,strainText,state;
        TextMeshProUGUI shieldText,shieldLabel;
        Transform head;
        double shownPower;
        CombatHudOutputButton outputButton;
        static CombatHudView pointerOwner;
        static int releasedFrame=-1;
        Global pointerBlocker;
        int pointerButton,pressedFrame,releaseRequestedFrame=-1,clickedFrame=-1;
        bool motionReady;
        Vector3 previousHead;
        // Raw spring state, the filtered fighter velocity and the soft-limited
        // offset/angle that are actually drawn.
        Vector2 motionOffset,motionVelocity,smoothedVelocity,shownOffset;
        float motionAngle,angularVelocity,shownAngle,visibility;
        int previousWidth,previousHeight;
        SpriteRenderer headSprite;
        LimbBehaviour headLimb;
        GameObject statusObject;
        Renderer[] statusRenderers;
        // Inertia tuning: about 1.7 Hz with light damping, so a hard stop gives
        // one visible sway, a small counter-swing and a calm settle (~1 s).
        const float Stiffness=120,Damping=8.5f,Carry=.30f,Drag=.012f,VelocitySmoothing=.04f;
        const float AngleStiffness=420,AngleDamping=24,AnglePerPixel=.11f,MaxAngle=3;
        const float FadeInTime=.16f,FadeOutTime=.12f;
        static readonly Color TextColor=new Color(.94f,.96f,1);
        public RectTransform Panel {get{return panel;}}
        public CombatHudGraphic Art {get{return art;}}
        public CombatHudOutputButton OutputButton {get{return outputButton;}}
        public Vector2 MotionOffset {get{return shownOffset;}}
        public float MotionAngle {get{return shownAngle;}}
        public float Visibility {get{return visibility;}}
        // The HUD belongs to the native Detail view (S by default), like limb health.
        public static bool DetailViewActive {get{return Global.main && Global.main.ShowLimbStatus;}}
        public static bool InputCaptured {get{return pointerOwner || Time.frameCount<=releasedFrame;}}
        public static CombatHudView Bind(PersonBehaviour person)
        {if(!person)return null;var v=person.GetComponent<CombatHudView>();return v?v:person.gameObject.AddComponent<CombatHudView>();}
        void Start(){power=CombatPowerController.Bind(GetComponent<PersonBehaviour>());Build();}
        void Build()
        {
            if(!canvas)
            {
                var go=new GameObject("DBPU Combat HUD",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster));DontDestroyOnLoad(go);
                canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=100;
            }
            if(!font)font=TMP_Settings.defaultFontAsset;
            if(!font)font=ModAPI.FindSpawnable("Holographic Display").Prefab.transform.Find("Text").GetComponent<TextMeshPro>().font;
            panel=new GameObject("Live combat gauges",typeof(RectTransform),typeof(CanvasGroup)).GetComponent<RectTransform>();
            panel.SetParent(canvas.transform,false);panel.anchorMin=panel.anchorMax=Vector2.zero;panel.pivot=Vector2.zero;panel.sizeDelta=new Vector2(640,190);
            group=panel.GetComponent<CanvasGroup>();group.blocksRaycasts=true;group.interactable=true;
            art=panel.gameObject.AddComponent<CombatHudGraphic>();art.raycastTarget=false;
            var face=new GameObject("Change Ki Output",typeof(RectTransform)).GetComponent<RectTransform>();
            face.SetParent(panel,false);face.anchorMin=face.anchorMax=face.pivot=Vector2.zero;face.anchoredPosition=new Vector2(20,24);face.sizeDelta=new Vector2(108,108);
            outputButton=face.gameObject.AddComponent<CombatHudOutputButton>();outputButton.Owner=this;outputButton.raycastTarget=true;
            Label("POWERLEVEL",new Vector2(146,169),new Vector2(190,17),13,TextAlignmentOptions.BottomLeft);
            number=Label("0",new Vector2(146,131),new Vector2(400,40),32,TextAlignmentOptions.BottomLeft);number.fontStyle=FontStyles.Bold|FontStyles.Italic;number.color=new Color(1,.88f,.46f);
            output=Label("100%",new Vector2(10,65),new Vector2(128,46),32,TextAlignmentOptions.Center);
            Label("OUTPUT",new Vector2(24,52),new Vector2(99,16),10,TextAlignmentOptions.Center);
            Label("STM",new Vector2(150,116),new Vector2(42,16),10,TextAlignmentOptions.MidlineLeft);
            stmText=Label("100%",new Vector2(231,116),new Vector2(85,16),11,TextAlignmentOptions.Center);
            Label("KI",new Vector2(150,77),new Vector2(40,21),11,TextAlignmentOptions.MidlineLeft);
            kiText=Label("100%",new Vector2(290,77),new Vector2(170,21),12,TextAlignmentOptions.Center);
            Label("STR",new Vector2(151,43),new Vector2(45,17),10,TextAlignmentOptions.MidlineLeft);
            strainText=Label("0%",new Vector2(227,43),new Vector2(85,17),11,TextAlignmentOptions.Center);
            shieldLabel=Label("SHIELD",new Vector2(150,7),new Vector2(65,15),9,TextAlignmentOptions.MidlineLeft);
            shieldText=Label("100%",new Vector2(229,7),new Vector2(85,15),10,TextAlignmentOptions.Center);
            state=Label("",new Vector2(349,115),new Vector2(260,19),12,TextAlignmentOptions.MidlineRight);
            head=transform.Find("Head");
            if(head){headSprite=head.GetComponent<SpriteRenderer>();headLimb=head.GetComponent<LimbBehaviour>();}
        }
        TextMeshProUGUI Label(string text,Vector2 at,Vector2 size,float height,TextAlignmentOptions alignment)
        {
            var go=new GameObject(text.Length>0?text:"Mode",typeof(RectTransform));var rt=go.GetComponent<RectTransform>();
            rt.SetParent(panel,false);rt.anchorMin=rt.anchorMax=rt.pivot=Vector2.zero;rt.anchoredPosition=at;rt.sizeDelta=size;
            var label=go.AddComponent<TextMeshProUGUI>();label.font=font;label.text=text;label.fontSize=height;label.fontStyle=FontStyles.Bold;
            label.alignment=alignment;label.color=TextColor;label.raycastTarget=false;label.enableWordWrapping=false;label.overflowMode=TextOverflowModes.Overflow;
            label.extraPadding=true;
            // A thinner, feathered outline plus a soft shadow instead of a hard
            // dark rim keeps small text readable without the cut-out look.
            var material=label.fontMaterial;
            label.outlineColor=new Color(.015f,.02f,.03f,.78f);
            if(material)
            {
                material.SetFloat("_OutlineSoftness",.12f);
                material.EnableKeyword("UNDERLAY_ON");
                material.SetColor("_UnderlayColor",new Color(0,0,0,.5f));
                material.SetFloat("_UnderlayOffsetX",.3f);material.SetFloat("_UnderlayOffsetY",-.4f);
                material.SetFloat("_UnderlayDilate",.15f);material.SetFloat("_UnderlaySoftness",.5f);
            }
            // Set last so TMP recalculates mesh padding for the shadow.
            label.outlineWidth=.13f;return label;
        }
        bool WantsVisible(){return power && power.HudVisible && DetailViewActive;}
        bool CanInteract()
        {
            if(!enabled || !power || !power.Person || !power.Person.IsAlive() || !WantsVisible() || !panel || !group || group.alpha<.5f || !Global.main || Global.main.GetPausedMenu() || !KiCombatMath.HasFocus)return false;
            if((Global.ActiveUiBlock && (pointerOwner!=this || Global.main.ActiveUiBlockers>1)) || CombatAbilityWheel.InputCaptured || CombatTransformationWheel.InputCaptured || SpiritBombTargeting.InputCaptured || DialogBox.IsAnyDialogboxOpen || LiveSpriteEditor.SpriteEditorRuntime.BlocksGameInput)return false;
            var selected=EventSystem.current?EventSystem.current.currentSelectedGameObject:null;
            return !selected || (!selected.GetComponent<TMP_InputField>() && !selected.GetComponent<InputField>());
        }
        void Update()
        {
            if(pointerOwner==this)
            {
                if(!CanInteract() || pointerBlocker!=Global.main || (releaseRequestedFrame>=0 && Time.frameCount>releaseRequestedFrame))ReleasePointer();
                else if(Time.frameCount>pressedFrame && !Input.GetMouseButton(pointerButton))EndPointer();
                return;
            }
            if(!CanInteract() || (!Input.GetMouseButtonDown(0) && !Input.GetMouseButtonDown(1)) || !outputButton)return;
            if(!outputButton.Raycast(Input.mousePosition,null))return;
            // Claim the gesture before the native world tool's Update can grab or
            // activate a limb underneath this small UI target.
            if(EventSystem.current)
            {
                var hits=new List<RaycastResult>();var data=new PointerEventData(EventSystem.current){position=Input.mousePosition};EventSystem.current.RaycastAll(data,hits);
                if(hits.Count==0 || hits[0].gameObject!=outputButton.gameObject)return;
            }
            BeginPointer(Input.GetMouseButtonDown(1)?1:0);
        }
        internal bool BeginPointer(int button)
        {
            if(button<0 || button>1 || !CanInteract() || (pointerOwner && pointerOwner!=this))return false;
            if(pointerOwner==this)return true;
            pointerOwner=this;pointerBlocker=Global.main;pointerBlocker.AddUiBlocker();pointerButton=button;pressedFrame=Time.frameCount;releaseRequestedFrame=-1;
            return true;
        }
        internal void EndPointer(){if(pointerOwner==this && releaseRequestedFrame<0)releaseRequestedFrame=Time.frameCount;}
        public bool TryCycleOutput(int direction)
        {
            if(clickedFrame==Time.frameCount || !CanInteract())return false;
            clickedFrame=Time.frameCount;power.Cycle(direction);return true;
        }
        internal bool PointerBelongsTo(int button){return pointerOwner==this && pointerButton==button;}
        void ReleasePointer()
        {
            if(pointerBlocker)pointerBlocker.RemoveUiBlocker();pointerBlocker=null;releaseRequestedFrame=-1;
            if(pointerOwner==this){pointerOwner=null;releasedFrame=Time.frameCount;}
        }
        void ResetMotion(Vector3 position)
        {
            previousHead=position;motionOffset=motionVelocity=smoothedVelocity=shownOffset=Vector2.zero;
            motionAngle=angularVelocity=shownAngle=0;motionReady=true;previousWidth=Screen.width;previousHeight=Screen.height;
        }
        static Vector2 SoftLimit(Vector2 value,float limit)
        {float length=value.magnitude;return length<.0001f?Vector2.zero:value*(limit*Tanh(length/limit)/length);}
        static float SoftLimit(float value,float limit){return limit*Tanh(value/limit);}
        static float Tanh(float x){x=Mathf.Clamp(x,-9,9);float e=Mathf.Exp(2*x);return (e-1)/(e+1);}
        void StepMotion(Camera cam,Vector3 position,float scale)
        {
            float dt=Time.unscaledDeltaTime;
            if(!motionReady || (position-previousHead).sqrMagnitude>64 || previousWidth!=Screen.width || previousHeight!=Screen.height || dt>.1f)
            {ResetMotion(position);return;}
            if(Global.main && Global.main.Paused){previousHead=position;return;}
            if(dt<=.00001f)return;
            // Project both positions through the current camera so camera pans do
            // not masquerade as movement of the fighter.
            Vector2 velocity=(cam.WorldToScreenPoint(position)-cam.WorldToScreenPoint(previousHead))/dt;previousHead=position;
            float limit=Mathf.Clamp(scale*44,8,30);
            // Physics positions arrive in fixed steps; filter them before they
            // drive the spring so high frame rates do not make the HUD buzz.
            Vector2 filtered=smoothedVelocity+(velocity-smoothedVelocity)*(1-Mathf.Exp(-dt/VelocitySmoothing));
            // Inertia: a change of the fighter's velocity kicks the HUD the other
            // way, so a sudden stop throws it forward before the spring returns it.
            motionVelocity-=(filtered-smoothedVelocity)*Carry;smoothedVelocity=filtered;
            motionVelocity=Vector2.ClampMagnitude(motionVelocity,limit*Mathf.Sqrt(Stiffness)*1.1f);
            // A small trailing drag while the fighter keeps moving fast.
            Vector2 target=Vector2.ClampMagnitude(-filtered*Drag,limit*.6f);
            for(float left=dt;left>.000001f;)
            {
                float step=Mathf.Min(left,1/240f);left-=step;
                motionVelocity+=((target-motionOffset)*Stiffness-motionVelocity*Damping)*step;motionOffset+=motionVelocity*step;
                // The tilt follows the sideways sway slightly later, like a
                // panel hinged on the fighter's head.
                float targetAngle=Mathf.Clamp(-SoftLimit(motionOffset,limit).x*AnglePerPixel,-MaxAngle,MaxAngle);
                angularVelocity+=((targetAngle-motionAngle)*AngleStiffness-angularVelocity*AngleDamping)*step;motionAngle+=angularVelocity*step;
            }
            motionOffset=Vector2.ClampMagnitude(motionOffset,limit*3);motionAngle=Mathf.Clamp(motionAngle,-MaxAngle*2,MaxAngle*2);
            shownOffset=SoftLimit(motionOffset,limit);shownAngle=SoftLimit(motionAngle,MaxAngle);
        }
        // Height above the head centre that keeps the HUD clear of the head
        // sprite and of the native Detail-view health bar above it.
        float Clearance(Camera cam,Vector3 anchor,Vector3 at,float ppu)
        {
            float clearance=22;
            if(headSprite && headSprite.sprite)
            {
                var extents=headSprite.sprite.bounds.extents;var size=headSprite.transform.lossyScale;
                clearance=Mathf.Max(clearance,Mathf.Max(Mathf.Abs(extents.x*size.x),Mathf.Abs(extents.y*size.y))*ppu+6);
            }
            var status=headLimb?headLimb.LimbStatus:null;
            if(status!=statusObject){statusObject=status;statusRenderers=status?status.GetComponentsInChildren<Renderer>(true):null;}
            if(statusObject && statusObject.activeInHierarchy && statusRenderers!=null)
            {
                float top=float.NegativeInfinity;
                foreach(var r in statusRenderers)if(r && r.enabled && r.gameObject.activeInHierarchy)top=Mathf.Max(top,r.bounds.max.y);
                if(!float.IsInfinity(top) && top>anchor.y)
                {
                    float above=cam.WorldToScreenPoint(new Vector3(anchor.x,top,anchor.z)).y-at.y;
                    if(above>0 && above<Screen.height*.5f)clearance=Mathf.Max(clearance,above+5);
                }
            }
            return Mathf.Min(clearance,Screen.height*.4f);
        }
        void LateUpdate()
        {
            if(!power || !power.Ki || !panel || !Camera.main)return;
            power.Ki.SuppressLegacyHud(true);
            var cam=Camera.main;var anchor=head?head.position:transform.position;var at=cam.WorldToScreenPoint(anchor);
            bool onScreen=at.z>0 && at.x>-150 && at.x<Screen.width+150 && at.y>-100 && at.y<Screen.height+100;
            bool wanted=WantsVisible() && onScreen;
            // Fade in/out with Detail view instead of popping.
            float fadeDt=Mathf.Min(Time.unscaledDeltaTime,.1f);
            visibility=Mathf.MoveTowards(visibility,wanted?1:0,fadeDt/(wanted?FadeInTime:FadeOutTime));
            if(visibility<=0){group.alpha=0;group.blocksRaycasts=group.interactable=false;ResetMotion(anchor);ReleasePointer();return;}
            float ease=visibility*visibility*(3-2*visibility);
            group.alpha=ease;group.blocksRaycasts=wanted && CanInteract();group.interactable=group.blocksRaycasts;
            float ppu=cam.orthographic?Screen.height/(2*cam.orthographicSize):100;
            float scale=Mathf.Clamp(ppu*Mathf.Abs(transform.lossyScale.y)*.005f,.20f,.85f);
            StepMotion(cam,anchor,scale);
            // Appearing: a slight grow and rise into place.
            float shown=scale*(.94f+.06f*ease),width=640*shown,height=190*shown;
            panel.localScale=Vector3.one*shown;
            Vector2 hinge=new Vector2(at.x+shownOffset.x,at.y+Clearance(cam,anchor,at,ppu)-(1-ease)*12*scale+shownOffset.y);
            hinge.x=Mathf.Clamp(hinge.x,width*.5f+4,Mathf.Max(width*.5f+4,Screen.width-width*.5f-4));
            hinge.y=Mathf.Clamp(hinge.y,4,Mathf.Max(4,Screen.height-height-4));
            // Rotate around the bottom centre above the head, not the corner, so
            // the far end no longer swings out further than the near end.
            var rotation=Quaternion.Euler(0,0,shownAngle);panel.localRotation=rotation;
            panel.anchoredPosition=hinge-(Vector2)(rotation*new Vector3(320*shown,0,0));
            var corners=new Vector3[4];panel.GetWorldCorners(corners);Vector2 correction=Vector2.zero;
            float minX=corners[0].x,maxX=corners[0].x,minY=corners[0].y,maxY=corners[0].y;
            foreach(var corner in corners){minX=Mathf.Min(minX,corner.x);maxX=Mathf.Max(maxX,corner.x);minY=Mathf.Min(minY,corner.y);maxY=Mathf.Max(maxY,corner.y);}
            if(minX<3)correction.x=3-minX;else if(maxX>Screen.width-3)correction.x=Screen.width-3-maxX;
            if(minY<3)correction.y=3-minY;else if(maxY>Screen.height-3)correction.y=Screen.height-3-maxY;panel.anchoredPosition+=correction;
            float blend=1-Mathf.Exp(-14*Time.unscaledDeltaTime);
            shownPower=shownPower==0?power.PowerLevel:shownPower+(power.PowerLevel-shownPower)*blend;
            number.text=CombatPowerMath.Format(shownPower);output.text=Mathf.RoundToInt(Mathf.Min(100000,power.Output*100))+"%";
            var ki=power.Ki;float reserve=Mathf.Clamp01(ki.currentKi/Mathf.Max(1,ki.maxKi));
            var barrier=power.GetComponent<KiShield>();bool shielding=barrier && (barrier.Using || barrier.Cooldown>0);
            art.Shielded=shielding;art.ShieldIntegrity=barrier?barrier.Integrity01:0;
            art.ShieldTint=barrier && barrier.Using?barrier.Tint:new Color(1,.25f,.2f);
            shieldLabel.gameObject.SetActive(shielding);shieldText.gameObject.SetActive(shielding);
            if(shielding)shieldText.text=barrier.Using?Mathf.RoundToInt(barrier.Integrity01*100)+"%":"BROKEN";
            // DBPU has no independent stamina resource: STM displays remaining vitality.
            float stamina=power.Health01;
            art.Ki=Mathf.Lerp(art.Ki,reserve,blend);art.Stamina=Mathf.Lerp(art.Stamina,stamina,blend);art.Strain=Mathf.Lerp(art.Strain,Mathf.Clamp01(ki.Strain),blend);
            art.Prep=ki.SparkingOvercharge01;art.Sparking=ki.SparkingOverloadActive;
            bool transformed=!DBPU.BaseConsideredForms.Contains(ki.CurrentForm);
            art.Badge=ki.SparkingOverloadActive?new Color(.63f,.26f,1):transformed?new Color(1,.77f,.16f):new Color(.64f,.76f,.86f);
            // About 1.3-2 screen pixels of anti-aliasing (was ~0.85 px).
            float edge=Mathf.Lerp(1.3f,2f,Mathf.InverseLerp(.2f,.85f,scale));
            art.Feather=Mathf.Clamp(edge/shown,1.2f,9);art.Clock=Time.time;art.SetVerticesDirty();
            if(outputButton){outputButton.Feather=art.Feather;outputButton.SetPressed(pointerOwner==this);}
            kiText.text=Mathf.RoundToInt(reserve*100)+"%";stmText.text=Mathf.RoundToInt(stamina*100)+"%";strainText.text=Mathf.RoundToInt(Mathf.Clamp01(ki.Strain)*100)+"%";
            state.text=ki.SparkingOverloadActive?"SPARKING":ki.SparkingExhaustActive?"EXHAUSTED":ki.SparkingOvercharge01>.01f?"OVERCHARGE":"";
            state.color=ki.SparkingExhaustActive?new Color(1,.4f,.35f):new Color(.88f,.65f,1);
        }
        void OnDisable(){ReleasePointer();motionReady=false;visibility=0;if(group){group.alpha=0;group.blocksRaycasts=false;group.interactable=false;}}
        void OnDestroy(){ReleasePointer();if(panel)Destroy(panel.gameObject);if(power && power.Ki)power.Ki.SuppressLegacyHud(false);}
    }

    public sealed class CombatHudOutputButton : MaskableGraphic,IPointerDownHandler,IPointerUpHandler,IPointerClickHandler,IPointerEnterHandler,IPointerExitHandler
    {
        public CombatHudView Owner;
        float feather=1;
        bool hovered,pressed;
        // Rebuild only when the edge width changes noticeably (zoom).
        public float Feather {get{return feather;}set{if(Mathf.Abs(feather-value)<.05f)return;feather=value;SetVerticesDirty();}}
        public override bool Raycast(Vector2 point,Camera eventCamera)
        {
            Vector2 local;if(!isActiveAndEnabled || !RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform,point,eventCamera,out local))return false;
            return (local-new Vector2(54,54)).sqrMagnitude<=54*54 && base.Raycast(point,eventCamera);
        }
        public void OnPointerDown(PointerEventData data)
        {if(Owner && (data.button==PointerEventData.InputButton.Left || data.button==PointerEventData.InputButton.Right))Owner.BeginPointer(data.button==PointerEventData.InputButton.Right?1:0);}
        public void OnPointerUp(PointerEventData data){if(Owner)Owner.EndPointer();}
        public void OnPointerClick(PointerEventData data)
        {
            int button=data.button==PointerEventData.InputButton.Right?1:0;
            if(Owner && (data.button==PointerEventData.InputButton.Left || data.button==PointerEventData.InputButton.Right) && Owner.PointerBelongsTo(button))
                Owner.TryCycleOutput(button==1 || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)?-1:1);
        }
        public void OnPointerEnter(PointerEventData data){hovered=true;SetVerticesDirty();}
        public void OnPointerExit(PointerEventData data){hovered=false;SetVerticesDirty();}
        public void SetPressed(bool value){if(pressed==value)return;pressed=value;SetVerticesDirty();}
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var c=pressed?new Color(.03f,.08f,.14f,.16f):new Color(.80f,.94f,1,hovered?.10f:.001f);var clear=c;clear.a=0;
            // Feathered rim: the hover/press highlight used to be a hard-edged disc.
            float edge=Mathf.Clamp(feather,.5f,9);
            vh.AddVert(new Vector3(54,54),c,Vector2.zero);
            for(int n=0;n<=96;n++)
            {
                float a=n*Mathf.PI/48;var direction=new Vector3(Mathf.Cos(a),Mathf.Sin(a));
                vh.AddVert(new Vector3(54,54)+direction*(53-edge*.5f),c,Vector2.zero);vh.AddVert(new Vector3(54,54)+direction*(53+edge*.5f),clear,Vector2.zero);
                if(n>0){int p=1+(n-1)*2,q=1+n*2;vh.AddTriangle(0,p,q);vh.AddTriangle(p,p+1,q+1);vh.AddTriangle(p,q+1,q);}
            }
        }
        protected override void OnDisable(){if(Owner)Owner.EndPointer();base.OnDisable();}
    }

    public sealed class CombatHudGraphic : MaskableGraphic
    {
        public float Ki=1,Stamina=1,Strain,Prep,Clock;
        public float Feather=1;
        public bool Shielded;public float ShieldIntegrity;public Color ShieldTint=Color.cyan;
        public bool Sparking;
        public Color Badge=new Color(.64f,.76f,.86f);
        // Opaque backing: overlapping translucent plates used to leave darker
        // seams where the bars meet each other and the Output ring.
        static readonly Color Black=new Color(.012f,.016f,.024f,1);
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Quad(vh,136,64,638,104,Black,Black,34);Quad(vh,138,109,346,137,Black,Black,25);Quad(vh,136,24,344,66,Black,Black,42);
            Quad(vh,145,70,626,98,new Color(.006f,.045f,.07f),new Color(.02f,.1f,.13f),26);
            Gauge(vh,145,70,626,98,Ki,Sparking?new Color(.57f,.12f,.84f):new Color(0,.35f,.5f),Sparking?new Color(.93f,.64f,1):new Color(.03f,.83f,.96f),26);
            if(Prep>0 && !Sparking)Gauge(vh,145,70,626,98,Mathf.Clamp01(Prep),new Color(.48f,.04f,.67f,.84f),new Color(.89f,.41f,1,.9f),26);
            Quad(vh,145,115,335,131,new Color(.19f,.14f,.02f),new Color(.3f,.24f,.03f),16);
            Gauge(vh,145,115,335,131,Stamina,new Color(.61f,.4f,.02f),new Color(1,.92f,.18f),16);
            Gauge(vh,145,31,329,60,Strain,new Color(.35f,.015f,.035f),new Color(1,.2f,.19f),29);
            if(Shielded){Quad(vh,138,4,344,24,Black,Black,20);Gauge(vh,145,8,329,21,ShieldIntegrity,Color.Lerp(ShieldTint,Color.black,.55f),Color.Lerp(ShieldTint,Color.white,.35f),13);}
            Line(vh,new Vector2(145,99),new Vector2(618,99),1.4f,new Color(.6f,1,1,.5f));
            Line(vh,new Vector2(146,132),new Vector2(320,132),1,new Color(1,1,.7f,.65f));
            // Nested metal rings and twelve separately shaded rim segments.
            Disc(vh,74,78,73,Black,new Color(0,0,0,.9f));
            Disc(vh,74,78,69,new Color(.18f,.22f,.29f),new Color(.61f,.66f,.75f));
            Ring(vh,74,78,59,67,new Color(.55f,.59f,.68f),new Color(.17f,.19f,.26f));
            for(int n=0;n<12;n++)
            {float a=n*Mathf.PI/6;Line(vh,new Vector2(74+59*Mathf.Cos(a),78+59*Mathf.Sin(a)),new Vector2(74+69*Mathf.Cos(a),78+69*Mathf.Sin(a)),2.5f,Black);}
            Disc(vh,74,78,58,Black,Black);
            Disc(vh,74,78,54,Color.Lerp(Badge,Color.black,.35f),Color.Lerp(Badge,Color.white,.40f));
            Ring(vh,74,78,50,54,Color.Lerp(Badge,Color.black,.25f),Color.Lerp(Badge,Color.white,.65f));
            if(Sparking)
            {
                Vector2 before=new Vector2(145,30);
                for(int n=1;n<=28;n++)
                {
                    var next=new Vector2(145+n*16.8f,28+7*Mathf.Sin(n*2.3f+Mathf.Floor(Clock*18)*1.7f));
                    Line(vh,before,next,5,new Color(.6f,.2f,1,.30f));Line(vh,before,next,1.3f,new Color(.94f,.8f,1));before=next;
                }
            }
        }
        // Area-true fill: the old fill started one slant-width in, so a gauge at
        // 1% still showed a block and the bar never visibly reached zero.
        void Gauge(VertexHelper v,float x,float y,float end,float top,float fill,Color low,Color high,float slant)
        {
            fill=Mathf.Clamp01(fill);float height=top-y,span=end-x;
            if(fill<=.0001f || height<=0 || span<=0)return;
            slant=Mathf.Clamp(slant,0,span);
            float area=fill*(span-slant*.5f)*height,threshold=slant*height*.5f;
            float right=area>=threshold?area/height+slant*.5f:Mathf.Sqrt(2*slant*area/height);
            right=x+Mathf.Min(right,span);
            if(right-slant>=x-.001f){Quad(v,x,y,right,top,low,high,slant);return;}
            // Nearly empty: clip the slanted leading edge to a small triangle.
            float t=Mathf.Clamp01(1-(right-x)/Mathf.Max(.0001f,slant)),cut=Mathf.Lerp(y,top,t);
            SoftPolygon(v,new[]{new Vector2(x,cut),new Vector2(right,top),new Vector2(x,top)},new[]{Color.Lerp(low,high,t),high,high},Mathf.Min(Feather,(right-x)*.8f,(top-cut)*.8f));
        }
        void Quad(VertexHelper v,float x,float y,float end,float top,Color low,Color high,float slant)
        {SoftPolygon(v,new[]{new Vector2(x,y),new Vector2(end-slant,y),new Vector2(end,top),new Vector2(x,top)},new[]{low,low,high,high},Mathf.Min(Feather,Mathf.Max(.0001f,end-slant-x)*.8f+slant,(top-y)*.8f));}
        // Feathered convex polygon (counter-clockwise). Half of the edge fades
        // inside and half outside so shapes keep their size when softened.
        void SoftPolygon(VertexHelper v,Vector2[] points,Color[] colors,float edge)
        {
            int count=points.Length,start=v.currentVertCount;edge=Mathf.Max(.0001f,edge);
            for(int n=0;n<count;n++)
            {
                Vector2 before=(points[n]-points[(n+count-1)%count]).normalized,after=(points[(n+1)%count]-points[n]).normalized;
                Vector2 a=new Vector2(before.y,-before.x),b=new Vector2(after.y,-after.x);
                if(a.sqrMagnitude<.5f)a=b;if(b.sqrMagnitude<.5f)b=a;
                Vector2 normal=(a+b).normalized;if(normal.sqrMagnitude<.5f)normal=b;
                Vector2 outset=normal*(edge/Mathf.Max(.45f,Vector2.Dot(normal,b)));
                v.AddVert(points[n]-outset*.5f,colors[n],Vector2.zero);
                var transparent=colors[n];transparent.a=0;v.AddVert(points[n]+outset*.5f,transparent,Vector2.zero);
            }
            for(int n=1;n<count-1;n++)v.AddTriangle(start,start+n*2,start+(n+1)*2);
            for(int n=0;n<count;n++){int a=start+n*2,b=start+((n+1)%count)*2;v.AddTriangle(a,a+1,b+1);v.AddTriangle(a,b+1,b);}
        }
        void Disc(VertexHelper v,float x,float y,float r,Color low,Color high)
        {
            int start=v.currentVertCount;v.AddVert(new Vector3(x,y),Color.Lerp(low,high,.5f),Vector2.zero);
            float edge=Mathf.Min(Feather,r*.5f);
            for(int n=0;n<=128;n++)
            {
                float a=n*Mathf.PI/64;Vector2 direction=new Vector2(Mathf.Cos(a),Mathf.Sin(a));Color c=Color.Lerp(low,high,(Mathf.Sin(a)+1)*.5f);
                v.AddVert(new Vector2(x,y)+direction*(r-edge*.5f),c,Vector2.zero);c.a=0;v.AddVert(new Vector2(x,y)+direction*(r+edge*.5f),c,Vector2.zero);
                if(n>0){int p=start+1+(n-1)*2,q=start+1+n*2;v.AddTriangle(start,p,q);v.AddTriangle(p,p+1,q+1);v.AddTriangle(p,q+1,q);}
            }
        }
        void Ring(VertexHelper v,float x,float y,float inner,float outer,Color low,Color high)
        {
            int start=v.currentVertCount;float feather=Mathf.Min(Feather,(outer-inner)*.7f);
            for(int n=0;n<=128;n++)
            {
                float a=n*Mathf.PI/64;Vector2 direction=new Vector2(Mathf.Cos(a),Mathf.Sin(a));Color c=Color.Lerp(low,high,(Mathf.Sin(a)+1)*.5f),clear=c;clear.a=0;
                v.AddVert(new Vector2(x,y)+direction*(inner-feather*.5f),clear,Vector2.zero);
                v.AddVert(new Vector2(x,y)+direction*(inner+feather*.5f),c,Vector2.zero);
                v.AddVert(new Vector2(x,y)+direction*(outer-feather*.5f),c,Vector2.zero);
                v.AddVert(new Vector2(x,y)+direction*(outer+feather*.5f),clear,Vector2.zero);
                if(n>0)for(int strip=0;strip<3;strip++){int p=start+(n-1)*4+strip,q=start+n*4+strip;v.AddTriangle(p,p+1,q+1);v.AddTriangle(p,q+1,q);}
            }
        }
        // Thin lines are widened to the feather and dimmed by the same ratio;
        // before, a 1 px line with a wide feather turned into a bright smear.
        void Line(VertexHelper v,Vector2 a,Vector2 b,float width,Color c)
        {
            float drawn=Mathf.Max(width,Feather*.9f);c.a*=width/drawn;
            var d=b-a;var p=new Vector2(-d.y,d.x).normalized*drawn*.5f;
            SoftPolygon(v,new[]{a-p,b-p,b+p,a+p},new[]{c,c,c,c},Mathf.Min(Feather,drawn*.9f));
        }
    }
}
