using System;
using HarmonyLib;

namespace DBPU.LiveSpriteEditor
{
    internal static class SpriteEditorGameInputBlock
    {
        internal static bool ShouldBlock(string name)
        {
            if (!SpriteEditorRuntime.BlocksGameInput || string.IsNullOrEmpty(name))
            {
                return false;
            }

            return string.Equals(name, "undo", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "copy", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "paste", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "delete", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "activateDirect", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "context", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "eyedrop", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "freezeSelection", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "igniteSelection", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "resizeSelection", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "arrangeDown", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "arrangeUp", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "arrangeFront", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "arrangeBack", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "arrangeSelection", StringComparison.OrdinalIgnoreCase);
        }
    }

    [HarmonyPatch(typeof(InputSystem), nameof(InputSystem.Down))]
    internal static class SpriteEditor_InputSystem_Down_Patch
    {
        static bool Prefix(string name, ref bool __result)
        {
            if (!SpriteEditorGameInputBlock.ShouldBlock(name))
            {
                return true;
            }

            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(InputSystem), nameof(InputSystem.Held))]
    internal static class SpriteEditor_InputSystem_Held_Patch
    {
        static bool Prefix(string name, ref bool __result)
        {
            if (!SpriteEditorGameInputBlock.ShouldBlock(name))
            {
                return true;
            }

            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(InputSystem), nameof(InputSystem.Up))]
    internal static class SpriteEditor_InputSystem_Up_Patch
    {
        static bool Prefix(string name, ref bool __result)
        {
            if (!SpriteEditorGameInputBlock.ShouldBlock(name))
            {
                return true;
            }

            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(UndoControllerBehaviour), nameof(UndoControllerBehaviour.Undo))]
    internal static class SpriteEditor_UndoController_Undo_Patch
    {
        static bool Prefix()
        {
            return !SpriteEditorRuntime.BlocksGameInput;
        }
    }
}
