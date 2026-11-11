using HarmonyLib;

namespace DBPU
{
    // The Use press that confirms a technique in the torso technique picker belongs to the picker:
    // that frame, the fighter's own Use triggers (Ki Shield, Final Explosion, hand techniques) stay quiet.
    [HarmonyPatch(typeof(UseEventTrigger), "Use")]
    public static class UseEventTrigger_TorsoPickerGate
    {
        static bool Prefix(UseEventTrigger __instance)
        {
            return !TorsoTechniquePicker.BlocksUse(__instance);
        }
    }
}
