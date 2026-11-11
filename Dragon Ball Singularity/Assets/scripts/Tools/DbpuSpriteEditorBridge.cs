using System;
using UnityEngine;

namespace DBPU
{
    public static class DbpuSpriteEditorBridge
    {
        public static void NotifyCharacterApply(LiveSpriteEditor.CharacterSpriteEditorTarget target)
        {
            if (target == null) return;
            try
            {
                var person = target.Person;
                var exports = target.TakeLastMakerExports();
                if (exports == null || exports.Count == 0) return;
                CustomSkinMakerUI.IngestSpriteEditorApply(person, exports);
                TransformationMakerUI.IngestSpriteEditorApply(person, exports);
                if (!TransformationMakerUI.IsOpen && !CustomSkinMakerUI.IsOpen)
                    TransformationMakerUI.PersistSpriteEditorToCurrentForm(person, exports);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[DBPU] Maker ingest after sprite editor apply failed: " + ex.Message);
            }
        }

        public static bool TryOpen(PersonBehaviour person, LimbBehaviour preferredLimb = null)
        {
            if (person == null) return false;
            try
            {
                LiveSpriteEditor.LiveSpriteEditorEntry.EnsureInstalled();
                var root = person.transform.root.gameObject;
                var target = root.GetComponent<LiveSpriteEditor.CharacterSpriteEditorTarget>();
                if (target == null)
                    target = root.AddComponent<LiveSpriteEditor.CharacterSpriteEditorTarget>();

                LimbBehaviour limb = preferredLimb;
                if (limb == null && person.Limbs != null)
                {
                    for (int i = 0; i < person.Limbs.Length; i++)
                    {
                        if (person.Limbs[i] != null
                            && person.Limbs[i].name.IndexOf("Head", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            limb = person.Limbs[i];
                            break;
                        }
                    }
                    if (limb == null)
                    {
                        for (int i = 0; i < person.Limbs.Length; i++)
                        {
                            if (person.Limbs[i] != null)
                            {
                                limb = person.Limbs[i];
                                break;
                            }
                        }
                    }
                }

                if (limb != null)
                    LiveSpriteEditor.SpriteEditorRuntime.Open(target, limb);
                else
                    LiveSpriteEditor.SpriteEditorRuntime.Open(target, null);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[DBPU] Sprite editor open failed: " + ex.Message);
                try { DialogBoxManager.Notification("Sprite editor failed: " + ex.Message); } catch { }
                return false;
            }
        }

        public static string MapLimbKey(string limbKey)
        {
            if (string.IsNullOrEmpty(limbKey)) return "Head";
            string n = limbKey.ToLowerInvariant().Replace(" ", "");
            if (n.Contains("hair")) return "Hair";
            if (n.Contains("head")) return "Head";
            if (n.Contains("upperarm") || n.Contains("uarm") || (n.Contains("arm") && n.Contains("upper")))
                return "UArm";
            if (n.Contains("lowerarm") || n.Contains("larm") || n.Contains("forearm")
                || (n.Contains("arm") && n.Contains("lower")))
                return "LArm";
            if (n.Contains("upperleg") || n.Contains("uleg") || n.Contains("thigh")
                || (n.Contains("leg") && n.Contains("upper")))
                return "ULeg";
            if (n.Contains("lowerleg") || n.Contains("lleg") || n.Contains("shin")
                || (n.Contains("leg") && n.Contains("lower")))
                return "LLeg";
            if (n.Contains("foot") || n.Contains("shoe")) return "Foot";
            if (n.Contains("upper")) return "Upper";
            if (n.Contains("middle")) return "Middle";
            if (n.Contains("lower")) return "Lower";
            return limbKey;
        }

        public static bool TryOpenForLimbName(PersonBehaviour person, string limbKey)
        {
            if (person == null) return false;
            string mapped = MapLimbKey(limbKey);
            if (string.Equals(mapped, "Hair", StringComparison.OrdinalIgnoreCase))
                return TryOpenHair(person);

            LimbBehaviour match = FindLimbByKey(person, mapped);
            if (match == null && !string.IsNullOrEmpty(limbKey) && !string.Equals(limbKey, mapped, StringComparison.OrdinalIgnoreCase))
                match = FindLimbByKey(person, limbKey);
            return TryOpen(person, match);
        }

        public static LimbBehaviour FindLimbByKey(PersonBehaviour person, string limbKey)
        {
            if (person?.Limbs == null || string.IsNullOrEmpty(limbKey)) return null;
            string needle = MapLimbKey(limbKey).ToLowerInvariant();
            for (int i = 0; i < person.Limbs.Length; i++)
            {
                var limb = person.Limbs[i];
                if (limb == null) continue;
                string n = limb.name.ToLowerInvariant();
                bool hit = false;
                if (needle == "head" && n.Contains("head")) hit = true;
                else if (needle == "upper" && n.Contains("upper") && !n.Contains("arm") && !n.Contains("leg")) hit = true;
                else if (needle == "middle" && n.Contains("middle")) hit = true;
                else if (needle == "lower" && n.Contains("lower") && !n.Contains("arm") && !n.Contains("leg")) hit = true;
                else if (needle == "uleg" && (n.Contains("upperleg") || (n.Contains("leg") && n.Contains("upper")))) hit = true;
                else if (needle == "lleg" && (n.Contains("lowerleg") || (n.Contains("leg") && n.Contains("lower")))) hit = true;
                else if (needle == "foot" && n.Contains("foot")) hit = true;
                else if (needle == "uarm" && (n.Contains("upperarm") || (n.Contains("arm") && n.Contains("upper")))) hit = true;
                else if (needle == "larm" && (n.Contains("lowerarm") || (n.Contains("arm") && n.Contains("lower")))) hit = true;
                if (hit) return limb;
            }
            return null;
        }

        public static bool TryOpenHair(PersonBehaviour person)
        {
            if (person == null) return false;
            try
            {
                LiveSpriteEditor.LiveSpriteEditorEntry.EnsureInstalled();
                var root = person.transform.root.gameObject;
                var target = root.GetComponent<LiveSpriteEditor.CharacterSpriteEditorTarget>();
                if (target == null)
                    target = root.AddComponent<LiveSpriteEditor.CharacterSpriteEditorTarget>();

                LiveSpriteEditor.SpriteEditorRuntime.OpenHair(target);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[DBPU] Sprite editor open hair failed: " + ex.Message);
                try { DialogBoxManager.Notification("Sprite editor failed: " + ex.Message); } catch { }
                return false;
            }
        }
    }
}
