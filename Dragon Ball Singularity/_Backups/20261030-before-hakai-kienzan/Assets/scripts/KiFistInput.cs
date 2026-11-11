using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace DBPU
{
    // One gesture owns one limb until release, even if the pointer moves away.
    [DefaultExecutionOrder(-240)]
    public sealed class KiFistInput : MonoBehaviour
    {
        public const string ActionId="dbpu.kifistmode";
        public const KeyCode DefaultKey=KeyCode.R;
        public const float HoldThreshold=.24f;
        static KiFistInput instance;
        static int consumedFrame=-1;
        static bool suppressUntilRelease;
        static KeyCode capturedKey=DefaultKey,capturedSecondary=KeyCode.None;
        KiFistHand target;
        float heldSeconds;
        bool claimed,holdStarted;
        public static bool InputCaptured
        {
            get
            {
                if(suppressUntilRelease && KiCombatMath.HasFocus && !CapturedPrimaryHeld())suppressUntilRelease=false;
                return suppressUntilRelease || (instance && instance.claimed) || consumedFrame==Time.frameCount;
            }
        }
        public KiFistHand Target {get{return target;}}
        public static string ModeLabel(KiFistMode mode){return mode==KiFistMode.God?"God Fist":mode==KiFistMode.Infusion?"Energy Infusion":"Ki Fist";}
        public static void Ensure()
        {
            if(instance)return;
            var go=new GameObject("DBPU Ki Fist limb input");DontDestroyOnLoad(go);instance=go.AddComponent<KiFistInput>();
        }
        void Awake(){SceneManager.activeSceneChanged+=SceneChanged;}
        void SceneChanged(Scene before,Scene after){CancelGesture();}
        void OnApplicationFocus(bool focused){if(!focused && !KiCombatMath.HasFocus)CancelGesture();}
        void OnDisable(){CancelGesture();}
        void OnDestroy(){SceneManager.activeSceneChanged-=SceneChanged;CancelGesture();if(instance==this)instance=null;}
        static bool RawDown(){return InputSystem.Has(ActionId)?InputSystem.Down(ActionId):Input.GetKeyDown(DefaultKey);}
        static bool CapturedPrimaryHeld(){return capturedKey!=KeyCode.None && Input.GetKey(capturedKey);}
        static bool CapturedHeld(){return CapturedPrimaryHeld() && (capturedSecondary==KeyCode.None || Input.GetKey(capturedSecondary));}
        static bool CanRead()
        {
            if(!KiCombatMath.CanAct() || LiveSpriteEditor.SpriteEditorRuntime.BlocksGameInput)return false;
            if(EventSystem.current && EventSystem.current.IsPointerOverGameObject())return false;
            var focused=EventSystem.current?EventSystem.current.currentSelectedGameObject:null;
            return !focused || (!focused.GetComponent<TMP_InputField>() && !focused.GetComponent<InputField>());
        }
        static KiFistHand FromPhysical(PhysicalBehaviour physical)
        {
            var limb=physical?CombatAbilityWheel.ResolveAbilityLimb(physical.GetComponent<LimbBehaviour>()):null;
            var fist=limb?limb.GetComponent<KiFistHand>():null;
            return fist && fist.Equipped && KiCombatMath.ValidStrikeLimb(limb,limb.Person)?fist:null;
        }
        public static KiFistHand ResolveTarget()
        {
            var selection=SelectionController.Main;
            var fist=selection!=null?FromPhysical(selection.CurrentlyUnderMouse):null;if(fist)return fist;
            var tool=Global.main && Global.main.ToolControllerBehaviour?Global.main.ToolControllerBehaviour.CurrentTool:null;
            fist=tool!=null?FromPhysical(tool.ActiveSingleSelected):null;if(fist)return fist;
            KiFistHand unique=null;
            if(selection!=null && selection.SelectedObjects!=null)foreach(var physical in selection.SelectedObjects)
            {
                var candidate=FromPhysical(physical);if(!candidate)continue;
                if(unique && candidate!=unique)return null;unique=candidate;
            }
            return unique;
        }
        void Update()
        {
            if(suppressUntilRelease)
            {
                consumedFrame=Time.frameCount;
                if(KiCombatMath.HasFocus && !CapturedPrimaryHeld())suppressUntilRelease=false;
                return;
            }
            if(claimed)
            {
                consumedFrame=Time.frameCount;
                if(!CanRead() || !target || !target.Equipped || !KiCombatMath.ValidStrikeLimb(target.Limb,target.Person)){CancelGesture();return;}
                if(!CapturedHeld())
                {
                    if(holdStarted)target.ReleaseHeldCharge();else target.CycleMode();
                    // Releasing a modifier ends the gesture, but the primary key
                    // still belongs to it until the user physically releases it.
                    suppressUntilRelease=CapturedPrimaryHeld();
                    claimed=holdStarted=false;target=null;return;
                }
                heldSeconds+=Mathf.Max(0,Time.deltaTime);
                if(!holdStarted && heldSeconds>=HoldThreshold){holdStarted=true;target.BeginHeldCharge();}
                return;
            }
            if(!CanRead() || !RawDown())return;
            target=ResolveTarget();if(!target)return;
            claimed=true;holdStarted=false;heldSeconds=0;consumedFrame=Time.frameCount;
            InputAction action;
            if(InputSystem.Actions.TryGetValue(ActionId,out action)){capturedKey=action.Key;capturedSecondary=action.SecondaryKey;}
            else{capturedKey=DefaultKey;capturedSecondary=KeyCode.None;}
        }
        void CancelGesture()
        {
            if(claimed)
            {
                consumedFrame=Time.frameCount;
                suppressUntilRelease=CapturedPrimaryHeld() || !KiCombatMath.HasFocus;
                if(holdStarted && target)target.CancelHeldCharge();
            }
            target=null;claimed=holdStarted=false;heldSeconds=0;
        }
        public static bool BlocksAction(string id,KeyCode fallback)
        {
            if(!InputCaptured || id==ActionId)return false;
            InputAction action;KeyCode primary=fallback,secondary=KeyCode.None;
            if(InputSystem.Actions.TryGetValue(id,out action)){primary=action.Key;secondary=action.SecondaryKey;}
            return primary==capturedKey && (secondary==KeyCode.None || secondary==capturedSecondary || Input.GetKey(secondary));
        }
    }
}
