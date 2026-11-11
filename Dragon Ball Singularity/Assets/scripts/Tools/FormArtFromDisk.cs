using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DBPU
{
    public static class FormArtPaths
    {
        static readonly Dictionary<string, string> CharacterIdFolderAliases =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "BM/Bardock", "BardockSuper" },
                { "BardockSuper", "BardockSuper" },
                { "StoryOfBardock/Bardock", "Bardock" },
            };

        public static string ModRoot
        {
            get
            {
                try
                {
                    string meta = ModAPI.Metadata.MetaLocation;
                    if (!string.IsNullOrEmpty(meta) && Directory.Exists(Path.Combine(meta, "Assets", "Sprites", "Characters")))
                        return meta;
                }
                catch { }

                string mods = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Mods"));
                string[] preferred = { "Dragon Ball Singularity", "DragonBall Playground Ultimate" };
                for (int i = 0; i < preferred.Length; i++)
                {
                    string candidate = Path.Combine(mods, preferred[i]);
                    if (Directory.Exists(Path.Combine(candidate, "Assets", "Sprites", "Characters")))
                        return candidate;
                }

                return Path.Combine(mods, "Dragon Ball Singularity");
            }
        }

        public static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value)) return "Unknown";
            char[] invalid = Path.GetInvalidFileNameChars();
            var chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char ch = chars[i];
                if (ch < 32)
                {
                    chars[i] = '_';
                    continue;
                }
                for (int j = 0; j < invalid.Length; j++)
                {
                    if (ch == invalid[j])
                    {
                        chars[i] = '_';
                        break;
                    }
                }
            }
            string cleaned = new string(chars).Trim().TrimEnd('.', ' ');
            return string.IsNullOrEmpty(cleaned) ? "Unknown" : cleaned;
        }

        public static string SafePathLeaf(string value)
        {
            if (string.IsNullOrEmpty(value)) return "Unknown";
            string path = value.Replace('\\', '/').Trim();
            int slash = path.LastIndexOf('/');
            if (slash >= 0 && slash < path.Length - 1)
                path = path.Substring(slash + 1);
            return Sanitize(path);
        }

        public static string NormalizeCharacterId(string characterId)
        {
            if (string.IsNullOrEmpty(characterId)) return "Unknown";
            string path = characterId.Replace('\\', '/').Trim();
            const string prefix = "Assets/Icons/";
            while (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                path = path.Substring(prefix.Length);
            while (path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                path = path.Substring(0, path.Length - 4);

            if (string.IsNullOrEmpty(path)) return "Unknown";

            string[] parts = path.Split('/');
            for (int i = 0; i < parts.Length; i++)
                parts[i] = Sanitize(parts[i]);
            return string.Join("/", parts);
        }

        public static string StripDisplayPrefix(string displayName)
        {
            if (string.IsNullOrEmpty(displayName)) return displayName;
            string name = displayName.Trim();
            if (name.StartsWith("(DBSSH) ", StringComparison.OrdinalIgnoreCase))
                name = name.Substring(8).Trim();
            else if (name.StartsWith("(BM) ", StringComparison.OrdinalIgnoreCase))
                name = name.Substring(5).Trim();
            else if (name.StartsWith("(SS) ", StringComparison.OrdinalIgnoreCase))
                name = name.Substring(5).Trim();
            else if (name.StartsWith("[DBPU] ", StringComparison.OrdinalIgnoreCase))
                name = name.Substring(7).Trim();
            else if (name.StartsWith("[DBI] ", StringComparison.OrdinalIgnoreCase)) // names saved by older builds
                name = name.Substring(6).Trim();
            return name;
        }

        public static bool IsLimbOrHairSpriteName(string spriteName)
        {
            if (string.IsNullOrEmpty(spriteName)) return true;
            string stem;
            try { stem = Path.GetFileNameWithoutExtension(Sanitize(spriteName)); }
            catch { stem = Sanitize(spriteName); }
            if (string.IsNullOrEmpty(stem)) return true;
            if (stem.IndexOf("hair", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (string.Equals(stem, "Head", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(stem, "Face", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(stem, "Upper", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(stem, "Middle", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(stem, "Lower", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(stem, "UArm", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(stem, "LArm", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(stem, "ULeg", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(stem, "LLeg", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(stem, "Foot", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public static bool DiskAssetExists(string absolutePath)
        {
            try
            {
                return !string.IsNullOrEmpty(absolutePath)
                    && File.Exists(absolutePath)
                    && new FileInfo(absolutePath).Length > 64;
            }
            catch { return false; }
        }

        public static string ToAbsoluteModPath(string modRelativePath)
        {
            if (string.IsNullOrEmpty(modRelativePath)) return null;
            try
            {
                if (Path.IsPathRooted(modRelativePath)) return modRelativePath;
                return Path.Combine(ModRoot, modRelativePath.Replace('/', Path.DirectorySeparatorChar));
            }
            catch
            {
                return null;
            }
        }

        public static string ArtRootFromSpriteFolder(string spriteFolderRelOrAbs)
        {
            if (string.IsNullOrEmpty(spriteFolderRelOrAbs)) return null;
            string abs = ToAbsoluteModPath(spriteFolderRelOrAbs.TrimEnd('/', '\\'));
            if (string.IsNullOrEmpty(abs) || !Directory.Exists(abs)) return null;

            string charactersRoot = Path.Combine(ModRoot, "Assets", "Sprites", "Characters");
            string cur = Path.GetFullPath(abs);
            string rootFull = Path.GetFullPath(charactersRoot);
            while (!string.IsNullOrEmpty(cur)
                && cur.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
            {
                if (Directory.Exists(Path.Combine(cur, "Icons"))
                    || Directory.Exists(Path.Combine(cur, "Illustrations")))
                    return cur;

                string parent = Path.GetDirectoryName(cur);
                if (string.IsNullOrEmpty(parent)
                    || string.Equals(parent, rootFull, StringComparison.OrdinalIgnoreCase))
                    return cur;
                cur = parent;
            }
            return abs;
        }

        public static string ResolveCharacterFolder(string characterId, string displayName, string spriteFolder = null)
        {
            string root = Path.Combine(ModRoot, "Assets", "Sprites", "Characters");
            Directory.CreateDirectory(root);

            string fromSprite = ArtRootFromSpriteFolder(spriteFolder);
            if (!string.IsNullOrEmpty(fromSprite) && Directory.Exists(fromSprite))
                return fromSprite;

            characterId = NormalizeCharacterId(characterId);
            if (CharacterIdFolderAliases.TryGetValue(characterId, out string alias)
                && Directory.Exists(Path.Combine(root, alias)))
                return Path.Combine(root, alias);

            string idLeaf = SafePathLeaf(characterId);
            string cleanDisplay = Sanitize(StripDisplayPrefix(displayName));
            string safeDisplay = Sanitize(displayName);

            string[] candidates = { cleanDisplay, safeDisplay, idLeaf, Sanitize(characterId.Replace('/', '_')) };
            string fallback = null;
            for (int i = 0; i < candidates.Length; i++)
            {
                string c = candidates[i];
                if (string.IsNullOrEmpty(c) || c == "Unknown") continue;
                string path = Path.Combine(root, c);
                if (!Directory.Exists(path)) continue;
                if (Directory.Exists(Path.Combine(path, "Icons"))
                    || Directory.Exists(Path.Combine(path, "Illustrations"))
                    || File.Exists(Path.Combine(path, "Upper.png")))
                    return path;
                if (fallback == null) fallback = path;
            }

            if (!string.IsNullOrEmpty(characterId) && characterId.IndexOf('/') >= 0)
            {
                string nested = root;
                bool ok = true;
                string[] parts = characterId.Split('/');
                for (int i = 0; i < parts.Length; i++)
                {
                    string part = Sanitize(parts[i]);
                    if (part == "Unknown") { ok = false; break; }
                    nested = Path.Combine(nested, part);
                }
                if (ok && Directory.Exists(nested))
                {
                    if (Directory.Exists(Path.Combine(nested, "Icons"))
                        || Directory.Exists(Path.Combine(nested, "Illustrations"))
                        || File.Exists(Path.Combine(nested, "Upper.png")))
                        return nested;
                    if (fallback == null) fallback = nested;
                }
            }

            if (!string.IsNullOrEmpty(fallback)) return fallback;

            string createName = cleanDisplay != "Unknown" ? cleanDisplay : idLeaf;
            string created = Path.Combine(root, Sanitize(createName));
            Directory.CreateDirectory(created);
            return created;
        }

        public static void EnsureAssetFolders(string characterFolder)
        {
            if (string.IsNullOrEmpty(characterFolder)) return;
            try
            {
                Directory.CreateDirectory(Path.Combine(characterFolder, "Icons"));
                Directory.CreateDirectory(Path.Combine(characterFolder, "Illustrations"));
                Directory.CreateDirectory(Path.Combine(characterFolder, "SkinIllustrations"));
            }
            catch { }
            EnsurePowerAssetFolders();
        }

        public static void EnsurePowerAssetFolders()
        {
            try
            {
                Directory.CreateDirectory(Path.Combine(ModRoot, "Assets", "Sprites", "Menus", "Powers", "Icons"));
                Directory.CreateDirectory(Path.Combine(ModRoot, "Assets", "Sprites", "Menus", "Powers", "Illustrations"));
            }
            catch { }
        }

        public static string ResolvePowerIconPath(string powerId, Sprite existingSprite = null, string powerName = null)
        {
            EnsurePowerAssetFolders();
            string dir = Path.Combine(ModRoot, "Assets", "Sprites", "Menus", "Powers", "Icons");
            return ResolveNamedPowerAssetPath(dir, powerId, existingSprite, powerName);
        }

        public static string ResolvePowerIllustrationPath(string powerId, Sprite existingSprite = null, string powerName = null)
        {
            EnsurePowerAssetFolders();
            string dir = Path.Combine(ModRoot, "Assets", "Sprites", "Menus", "Powers", "Illustrations");
            return ResolveNamedPowerAssetPath(dir, powerId, existingSprite, powerName);
        }

        static string ResolveNamedPowerAssetPath(string dir, string powerId, Sprite existingSprite, string powerName)
        {
            Directory.CreateDirectory(dir);
            string safeId = Sanitize(powerId);
            string safeName = Sanitize(powerName);
            string existing = FindExistingFormFile(dir, safeId);
            if (string.IsNullOrEmpty(existing))
                existing = FindExistingFormFile(dir, safeName);
            if (!string.IsNullOrEmpty(existing)) return existing;

            if (existingSprite != null
                && !string.IsNullOrEmpty(existingSprite.name)
                && !IsLimbOrHairSpriteName(existingSprite.name))
            {
                string bySprite = FindExistingFormFile(dir, existingSprite.name);
                if (!string.IsNullOrEmpty(bySprite)) return bySprite;
                string spriteStem = Sanitize(existingSprite.name);
                if (spriteStem.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    spriteStem = spriteStem.Substring(0, spriteStem.Length - 4);
                if (!string.IsNullOrEmpty(spriteStem)
                    && !spriteStem.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
                    return Path.Combine(dir, spriteStem + ".png");
            }

            return Path.Combine(dir, (safeId == "Unknown" ? "Power" : safeId) + ".png");
        }

        public static string ThumbnailPath(string characterId)
        {
            string iconsRoot = Path.Combine(ModRoot, "Assets", "Icons");
            Directory.CreateDirectory(iconsRoot);
            string rel = NormalizeCharacterId(characterId);
            string[] parts = rel.Split('/');
            string full = iconsRoot;
            for (int i = 0; i < parts.Length; i++)
            {
                string part = Sanitize(parts[i]);
                if (i == parts.Length - 1)
                    full = Path.Combine(full, part + ".png");
                else
                    full = Path.Combine(full, part);
            }
            string dir = Path.GetDirectoryName(full);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            return full;
        }

        public static string ResolveSkinIllustrationPath(string characterFolder, string skinKey, string damageKey = "Damage0")
        {
            string dir = Path.Combine(characterFolder, "SkinIllustrations");
            Directory.CreateDirectory(dir);
            string skin = Sanitize(string.IsNullOrEmpty(skinKey) ? "Base" : skinKey);
            string damage = Sanitize(string.IsNullOrEmpty(damageKey) ? "Damage0" : damageKey);

            string existing = FindExistingFormFile(dir, skin + "_" + damage);
            if (!string.IsNullOrEmpty(existing)) return existing;
            existing = FindExistingFormFile(dir, skin);
            if (!string.IsNullOrEmpty(existing) && string.Equals(damage, "Damage0", StringComparison.OrdinalIgnoreCase))
                return existing;

            string packIll = Path.Combine(characterFolder, skin, "Illustrations", damage + ".png");
            if (File.Exists(packIll)) return packIll;

            return Path.Combine(dir, skin + "_" + damage + ".png");
        }

        public static string ResolveFormAssetPath(
            string characterFolder,
            string subfolder,
            string formId,
            Sprite existingSprite = null,
            string formName = null)
        {
            string dir = Path.Combine(characterFolder, subfolder);
            Directory.CreateDirectory(dir);

            string existing = FindExistingFormFile(dir, formId);
            if (string.IsNullOrEmpty(existing))
                existing = FindExistingFormFile(dir, formName);
            if (!string.IsNullOrEmpty(existing)) return existing;

            if (existingSprite != null
                && !string.IsNullOrEmpty(existingSprite.name)
                && !IsLimbOrHairSpriteName(existingSprite.name))
            {
                string bySprite = FindExistingFormFile(dir, existingSprite.name);
                if (!string.IsNullOrEmpty(bySprite)) return bySprite;

                string spriteStem = Sanitize(existingSprite.name);
                if (spriteStem.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    spriteStem = spriteStem.Substring(0, spriteStem.Length - 4);
                if (!string.IsNullOrEmpty(spriteStem)
                    && !spriteStem.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
                    return Path.Combine(dir, spriteStem + ".png");
            }

            return Path.Combine(dir, Sanitize(formId) + ".png");
        }

        public static string FindExistingFormFile(string dir, string formId)
        {
            if (string.IsNullOrEmpty(formId) || !Directory.Exists(dir)) return null;
            try
            {
                string want = Sanitize(formId);
                if (want.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    want = want.Substring(0, want.Length - 4);
                string wantCompact = want.Replace(" ", "");
                foreach (var file in Directory.GetFiles(dir, "*.png"))
                {
                    string stem = Path.GetFileNameWithoutExtension(file);
                    string stemCompact = (stem ?? "").Replace(" ", "");
                    if (string.Equals(stem, want, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(stem, formId, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(stemCompact, wantCompact, StringComparison.OrdinalIgnoreCase))
                        return file;
                }
            }
            catch { }
            return null;
        }
    }

    public static class FormArtFromDisk
    {
        public static void ApplyToPerson(GameObject instance)
        {
            if (instance == null) return;
            try
            {
                var identity = instance.GetComponent<DbpuCharacterIdentity>();
                string characterId = identity != null ? identity.CharacterId : instance.name;
                string displayName = identity != null && !string.IsNullOrEmpty(identity.DisplayName)
                    ? identity.DisplayName
                    : instance.name;
                string spriteFolder = identity != null ? identity.SpriteFolder : null;
                string folder = FormArtPaths.ResolveCharacterFolder(characterId, displayName, spriteFolder);
                if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return;
                FormArtPaths.EnsureAssetFolders(folder);

                var ct = instance.GetComponent<CharacterTransformations>();
                if (ct?.transformations?.TransformationData != null)
                {
                    foreach (var kvp in ct.transformations.TransformationData)
                    {
                        var stats = kvp.Value;
                        if (stats == null) continue;
                        string formId = string.IsNullOrEmpty(stats.ID) ? kvp.Key : stats.ID;
                        string formName = stats.Name;

                        string iconPath = FormArtPaths.ResolveFormAssetPath(folder, "Icons", formId, null, formName);
                        string illPath = FormArtPaths.ResolveFormAssetPath(folder, "Illustrations", formId, null, formName);

                        if (FormArtPaths.DiskAssetExists(iconPath))
                        {
                            Sprite icon = LoadDiskOrMod(iconPath);
                            if (icon != null) stats.Icon = icon;
                        }

                        if (FormArtPaths.DiskAssetExists(illPath))
                        {
                            Sprite ill = LoadDiskOrMod(illPath);
                            if (ill != null) stats.Illustration = ill;
                        }
                        else if (stats.Illustration == null && stats.Icon != null
                            && !FormArtPaths.IsLimbOrHairSpriteName(stats.Icon.name))
                        {
                            stats.Illustration = stats.Icon;
                        }
                    }
                }

                ApplySkinIllustrations(instance, folder);
                ApplyPowerIcons(instance);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[DB Singularity] FormArtFromDisk.ApplyToPerson: " + ex.Message);
            }
        }

        public static void ApplyPowerIcons(GameObject instance = null)
        {
            FormArtPaths.EnsurePowerAssetFolders();

            if (instance != null)
            {
                var powers = instance.GetComponent<CharacterPowers>()
                    ?? instance.GetComponentInChildren<CharacterPowers>(true);
                if (powers?.PowerData != null)
                {
                    foreach (var kvp in powers.PowerData)
                    {
                        var stats = kvp.Value;
                        if (stats == null) continue;
                        string id = string.IsNullOrEmpty(stats.ID) ? kvp.Key : stats.ID;
                        string iconPath = FormArtPaths.ResolvePowerIconPath(id, stats.Icon, stats.Name);
                        if (FormArtPaths.DiskAssetExists(iconPath))
                        {
                            Sprite icon = LoadDiskOrMod(iconPath);
                            if (icon != null) stats.Icon = icon;
                        }
                        string illPath = FormArtPaths.ResolvePowerIllustrationPath(id, stats.Illustration, stats.Name);
                        if (FormArtPaths.DiskAssetExists(illPath))
                        {
                            Sprite ill = LoadDiskOrMod(illPath);
                            if (ill != null) stats.Illustration = ill;
                        }
                    }
                }
            }

            foreach (var def in PowerCatalog.All)
            {
                if (def == null || string.IsNullOrEmpty(def.Id)) continue;
                string iconPath = FormArtPaths.ResolvePowerIconPath(def.Id, def.Icon, def.Name);
                if (FormArtPaths.DiskAssetExists(iconPath))
                {
                    Sprite icon = LoadDiskOrMod(iconPath);
                    if (icon != null) def.Icon = icon;
                }
            }
        }

        public static void ApplySkinIllustrations(GameObject instance, string characterFolder = null)
        {
            if (instance == null) return;
            var skins = instance.GetComponent<CharacterSkins>();
            if (skins?.skins?.SkinData == null || skins.skins.SkinData.Count == 0) return;

            if (string.IsNullOrEmpty(characterFolder))
            {
                var identity = instance.GetComponent<DbpuCharacterIdentity>();
                string characterId = identity != null ? identity.CharacterId : instance.name;
                string displayName = identity != null && !string.IsNullOrEmpty(identity.DisplayName)
                    ? identity.DisplayName
                    : instance.name;
                string spriteFolder = identity != null ? identity.SpriteFolder : null;
                characterFolder = FormArtPaths.ResolveCharacterFolder(characterId, displayName, spriteFolder);
            }
            if (string.IsNullOrEmpty(characterFolder) || !Directory.Exists(characterFolder)) return;

            foreach (var kvp in skins.skins.SkinData)
            {
                var info = kvp.Value;
                if (info == null) continue;
                string skinKey = string.IsNullOrEmpty(info.Name) ? kvp.Key : info.Name;
                if (info.Illustrations == null)
                    info.Illustrations = new Dictionary<string, Sprite>();

                var damageKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Damage0" };
                foreach (var existing in info.Illustrations.Keys)
                    damageKeys.Add(existing);

                foreach (string damageKey in damageKeys)
                {
                    string path = FormArtPaths.ResolveSkinIllustrationPath(characterFolder, skinKey, damageKey);
                    if (!FormArtPaths.DiskAssetExists(path)) continue;
                    Sprite spr = LoadDiskOrMod(path);
                    if (spr != null) info.Illustrations[damageKey] = spr;
                }
            }
        }

        public static Sprite LoadDiskOrMod(string absolutePath)
        {
            Sprite spr = CustomTransformationCatalog.LoadSpriteFromDisk(absolutePath);
            if (spr != null) return spr;
            try
            {
                string rel = absolutePath.Replace('\\', '/');
                int idx = rel.IndexOf("Assets/", StringComparison.OrdinalIgnoreCase);
                if (idx >= 0) return ModAPI.LoadSprite(rel.Substring(idx));
            }
            catch { }
            return null;
        }
    }
}
