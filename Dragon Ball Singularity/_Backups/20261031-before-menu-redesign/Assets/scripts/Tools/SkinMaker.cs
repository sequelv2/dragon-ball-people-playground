using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DBPU
{
    public class SkinPackDraft
    {
        public string PackFolderName;
        public string DisplayName;
        public string SkinKey;
        public string ActiveSuffix = "";
        public int ActiveDamage;
        public string SelectedLimb = "Upper";
        public Dictionary<string, string> LimbPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> IllustrationPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public List<DBPU.DbpuCustomSkinLoader.SkinBeltDto> Belts = new List<DBPU.DbpuCustomSkinLoader.SkinBeltDto>();
        public bool HasBuffed;
        public bool HasBuffed2;

        public static string LimbKey(string suffix, int damage, string limb)
        {
            return (suffix ?? "") + "|" + damage + "|" + (limb ?? "");
        }
    }

    public static class SkinPackIO
    {
        public static readonly string[] LimbNames =
        {
            "Head", "Upper", "Middle", "Lower", "ULeg", "LLeg", "Foot", "UArm", "LArm"
        };

        public static string SanitizeFolderName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "CustomSkin";
            string cleaned = Regex.Replace(name.Trim(), @"[^\w\s\-]", "");
            cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
            if (string.IsNullOrEmpty(cleaned)) return "CustomSkin";
            return cleaned;
        }

        public static string EnsureCustomSkinsRoot(string characterBasePath)
        {
            string fs = DBPU.DbpuCustomSkinLoader.GetCustomSkinsFolderFs(characterBasePath);
            if (!Directory.Exists(fs)) Directory.CreateDirectory(fs);
            return fs;
        }

        public static string GetSkinOrderPath(string characterBasePath)
        {
            return Path.Combine(EnsureCustomSkinsRoot(characterBasePath), "_order.json");
        }

        public static List<string> GetSkinOrder(string characterBasePath, IEnumerable<string> knownKeys)
        {
            var known = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (knownKeys != null)
            {
                foreach (var k in knownKeys)
                {
                    if (string.IsNullOrEmpty(k) || !seen.Add(k)) continue;
                    known.Add(k);
                }
            }

            var order = new List<string>();
            try
            {
                string path = GetSkinOrderPath(characterBasePath);
                if (File.Exists(path))
                {
                    var dto = JsonConvert.DeserializeObject<SkinOrderDto>(File.ReadAllText(path));
                    if (dto?.Order != null)
                    {
                        for (int i = 0; i < dto.Order.Count; i++)
                        {
                            string id = dto.Order[i];
                            if (string.IsNullOrEmpty(id)) continue;
                            if (!known.Exists(k => string.Equals(k, id, StringComparison.OrdinalIgnoreCase))) continue;
                            if (order.Exists(k => string.Equals(k, id, StringComparison.OrdinalIgnoreCase))) continue;
                            order.Add(id);
                        }
                    }
                }
            }
            catch { }

            for (int i = 0; i < known.Count; i++)
            {
                if (order.Exists(k => string.Equals(k, known[i], StringComparison.OrdinalIgnoreCase))) continue;
                order.Add(known[i]);
            }
            return order;
        }

        public static void SetSkinOrder(string characterBasePath, IList<string> order)
        {
            if (string.IsNullOrEmpty(characterBasePath) || order == null) return;
            try
            {
                var dto = new SkinOrderDto { Order = order.Where(s => !string.IsNullOrEmpty(s)).Distinct(StringComparer.OrdinalIgnoreCase).ToList() };
                File.WriteAllText(GetSkinOrderPath(characterBasePath), JsonConvert.SerializeObject(dto, Formatting.Indented));
            }
            catch { }
        }

        static bool FolderHasLimbSprites(string folderFs)
        {
            if (string.IsNullOrEmpty(folderFs) || !Directory.Exists(folderFs)) return false;
            if (File.Exists(Path.Combine(folderFs, "Upper.png"))) return true;
            string skins = Path.Combine(folderFs, "Skins");
            if (Directory.Exists(skins))
            {
                foreach (var d in Directory.GetDirectories(skins))
                {
                    if (File.Exists(Path.Combine(d, "Upper.png"))) return true;
                }
            }
            foreach (var d in Directory.GetDirectories(folderFs))
            {
                string name = Path.GetFileName(d);
                if (string.Equals(name, "Custom skins", StringComparison.OrdinalIgnoreCase)) continue;
                if (File.Exists(Path.Combine(d, "Upper.png"))) return true;
            }
            return false;
        }

        public static string ResolveCharacterBasePath(PersonBehaviour person)
        {
            if (person == null) return null;
            var root = person.transform.root.gameObject;
            var identity = root.GetComponent<DbpuCharacterIdentity>();
            if (identity != null && !string.IsNullOrEmpty(identity.SpriteFolder))
            {
                string folder = identity.SpriteFolder.Replace('\\', '/');
                if (!folder.EndsWith("/")) folder += "/";
                string fs = CustomTransformationCatalog.ResolveModPath(folder);
                if (Directory.Exists(fs)) return folder;
            }

            string id = identity != null && !string.IsNullOrEmpty(identity.CharacterId)
                ? identity.CharacterId.Replace('\\', '/')
                : root.name;
            string display = identity != null ? identity.DisplayName : null;

            string PreferExisting(params string[] candidates)
            {
                string fallback = null;
                for (int i = 0; i < candidates.Length; i++)
                {
                    string c = candidates[i];
                    if (string.IsNullOrEmpty(c)) continue;
                    string folder = c.Replace('\\', '/');
                    if (!folder.EndsWith("/")) folder += "/";
                    string fs = CustomTransformationCatalog.ResolveModPath(folder);
                    if (!Directory.Exists(fs)) continue;
                    if (FolderHasLimbSprites(fs)) return folder;
                    if (fallback == null) fallback = folder;
                }
                return fallback;
            }

            string leaf = id;
            int slash = id.LastIndexOf('/');
            if (slash >= 0 && slash < id.Length - 1)
                leaf = id.Substring(slash + 1);

            string resolved = PreferExisting(
                "Assets/Sprites/Characters/" + id + "/",
                "Assets/Sprites/Characters/" + leaf + "/",
                !string.IsNullOrEmpty(display) ? "Assets/Sprites/Characters/" + display + "/" : null);
            if (!string.IsNullOrEmpty(resolved)) return resolved;

            string charsRoot = CustomTransformationCatalog.ResolveModPath("Assets/Sprites/Characters/");
            if (Directory.Exists(charsRoot))
            {
                foreach (var dir in Directory.GetDirectories(charsRoot))
                {
                    string name = Path.GetFileName(dir);
                    if (string.Equals(name, leaf, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(name, id, StringComparison.OrdinalIgnoreCase)
                        || (!string.IsNullOrEmpty(display)
                            && (string.Equals(name, display, StringComparison.OrdinalIgnoreCase)
                                || display.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)))
                    {
                        if (FolderHasLimbSprites(dir))
                            return "Assets/Sprites/Characters/" + name + "/";
                    }
                }
            }
            return "Assets/Sprites/Characters/" + leaf + "/";
        }

        static string ResolveFs(string characterBasePath, string relativeOrAbsolute)
        {
            if (string.IsNullOrEmpty(relativeOrAbsolute)) return null;
            if (Path.IsPathRooted(relativeOrAbsolute) && File.Exists(relativeOrAbsolute)) return relativeOrAbsolute;
            string abs = CustomTransformationCatalog.ResolveModPath(relativeOrAbsolute);
            if (File.Exists(abs)) return abs;
            if (!string.IsNullOrEmpty(characterBasePath))
            {
                string joined = CustomTransformationCatalog.ResolveModPath(
                    characterBasePath.TrimEnd('/', '\\') + "/" + relativeOrAbsolute.TrimStart('/', '\\'));
                if (File.Exists(joined)) return joined;
            }
            return abs;
        }

        static void WriteBlankPng(string path, int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var cols = new Color[w * h];
            for (int i = 0; i < cols.Length; i++) cols[i] = new Color(0f, 0f, 0f, 0f);
            tex.SetPixels(cols);
            tex.Apply(false, false);
            CustomTransformationCatalog.WriteAllBytesShared(path, tex.EncodeToPNG());
            UnityEngine.Object.Destroy(tex);
        }

        public static bool IsPlaceholderLimbPng(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return true;
            try
            {
                var fi = new FileInfo(path);
                if (fi.Length < 400) return true;
                var spr = CustomTransformationCatalog.LoadSpriteFromDisk(path);
                if (spr == null || spr.texture == null) return true;
                if (spr.texture.width > 48 || spr.texture.height > 48) return false;
                var tex = spr.texture;
                var pixels = tex.GetPixels32();
                for (int i = 0; i < pixels.Length; i++)
                {
                    if (pixels[i].a > 8) return false;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static string CreateEmptyPack(string characterBasePath, string folderName, string seedSkinKey = null, string seedDisplayName = null, CharacterSkins skins = null)
        {
            folderName = SanitizeFolderName(folderName);
            string root = EnsureCustomSkinsRoot(characterBasePath);
            string packFs = Path.Combine(root, folderName);
            int n = 1;
            string unique = folderName;
            while (Directory.Exists(packFs))
            {
                unique = folderName + "_" + n;
                packFs = Path.Combine(root, unique);
                n++;
            }
            Directory.CreateDirectory(packFs);

            string baseFs = CustomTransformationCatalog.ResolveModPath(characterBasePath);
            bool any = false;
            foreach (var limb in LimbNames)
            {
                string file = limb + ".png";
                string src = Path.Combine(baseFs, file);
                if (File.Exists(src) && !IsPlaceholderLimbPng(src))
                {
                    CustomTransformationCatalog.CopyFileShared(src, Path.Combine(packFs, file), true);
                    any = true;
                }
            }

            if (!File.Exists(Path.Combine(packFs, "Upper.png")) || IsPlaceholderLimbPng(Path.Combine(packFs, "Upper.png")))
            {
                string seedFs = FindSourceFolder(characterBasePath, seedSkinKey, seedDisplayName, skins);
                if (!string.IsNullOrEmpty(seedFs) && Directory.Exists(seedFs))
                {
                    CopyLimbFolder(seedFs, packFs);
                    any = File.Exists(Path.Combine(packFs, "Upper.png")) && !IsPlaceholderLimbPng(Path.Combine(packFs, "Upper.png"));
                }
            }

            if (!File.Exists(Path.Combine(packFs, "Upper.png")))
                WriteBlankPng(Path.Combine(packFs, "Upper.png"), 32, 32);

            Directory.CreateDirectory(Path.Combine(packFs, "Illustrations"));
            Directory.CreateDirectory(Path.Combine(packFs, "Belts"));
            return unique;
        }

        public static void CopyLimbFolder(string srcFs, string dstFs)
        {
            if (string.IsNullOrEmpty(srcFs) || !Directory.Exists(srcFs)) return;
            if (!Directory.Exists(dstFs)) Directory.CreateDirectory(dstFs);
            foreach (var limb in LimbNames)
            {
                string file = limb + ".png";
                string src = Path.Combine(srcFs, file);
                if (File.Exists(src))
                    CustomTransformationCatalog.CopyFileShared(src, Path.Combine(dstFs, file), true);
            }
            if (File.Exists(Path.Combine(srcFs, "Face.png")) && !File.Exists(Path.Combine(dstFs, "Head.png")))
                CustomTransformationCatalog.CopyFileShared(
                    Path.Combine(srcFs, "Face.png"), Path.Combine(dstFs, "Head.png"), true);
        }

        static void CollectLimbsFromFolder(SkinPackDraft draft, string folderFs, string suffix, int damage)
        {
            if (!Directory.Exists(folderFs)) return;
            foreach (var limb in LimbNames)
            {
                string file = Path.Combine(folderFs, limb + ".png");
                if (!File.Exists(file) && limb == "Head")
                    file = Path.Combine(folderFs, "Face.png");
                if (File.Exists(file))
                    draft.LimbPaths[SkinPackDraft.LimbKey(suffix, damage, limb)] = file;
            }
            string hairFile = Path.Combine(folderFs, "Hair.png");
            if (File.Exists(hairFile))
                draft.LimbPaths[SkinPackDraft.LimbKey(suffix, damage, "Hair")] = hairFile;
        }

        static void ScanSuffixTree(SkinPackDraft draft, string packFs, string suffix)
        {
            string root = string.IsNullOrEmpty(suffix) ? packFs : Path.Combine(packFs, suffix);
            if (!Directory.Exists(root)) return;
            CollectLimbsFromFolder(draft, root, suffix, 0);
            foreach (var dir in Directory.GetDirectories(root))
            {
                string name = Path.GetFileName(dir);
                if (!Regex.IsMatch(name, @"^Damage\d+$", RegexOptions.IgnoreCase)) continue;
                if (!int.TryParse(name.Substring("Damage".Length), out int dmg)) continue;
                CollectLimbsFromFolder(draft, dir, suffix, dmg);
            }
        }

        public static SkinPackDraft LoadDraftFromPack(string characterBasePath, string folderName)
        {
            var draft = new SkinPackDraft
            {
                PackFolderName = folderName,
                DisplayName = (folderName ?? "Skin").Replace('_', ' '),
                SkinKey = new string((folderName ?? "CustomSkin").Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray()),
                SelectedLimb = "Upper",
                ActiveSuffix = "",
                ActiveDamage = 0
            };
            if (string.IsNullOrEmpty(draft.SkinKey)) draft.SkinKey = "CustomSkin";

            string packFs = DBPU.DbpuCustomSkinLoader.GetPackFolderFs(characterBasePath, folderName);
            if (!Directory.Exists(packFs)) return draft;

            ScanSuffixTree(draft, packFs, "");
            draft.HasBuffed = Directory.Exists(Path.Combine(packFs, "_Buffed"))
                && File.Exists(Path.Combine(packFs, "_Buffed", "Upper.png"));
            draft.HasBuffed2 = Directory.Exists(Path.Combine(packFs, "_Buffed2"))
                && File.Exists(Path.Combine(packFs, "_Buffed2", "Upper.png"));
            if (draft.HasBuffed) ScanSuffixTree(draft, packFs, "_Buffed");
            if (draft.HasBuffed2) ScanSuffixTree(draft, packFs, "_Buffed2");

            string illDir = Path.Combine(packFs, "Illustrations");
            if (Directory.Exists(illDir))
            {
                foreach (var file in Directory.GetFiles(illDir, "*.png"))
                {
                    string stem = Path.GetFileNameWithoutExtension(file);
                    draft.IllustrationPaths[stem] = file;
                }
            }

            string beltsDir = Path.Combine(packFs, "Belts");
            string jsonPath = Path.Combine(beltsDir, "belt.json");
            if (File.Exists(jsonPath))
            {
                try
                {
                    var list = JsonConvert.DeserializeObject<List<DBPU.DbpuCustomSkinLoader.SkinBeltDto>>(File.ReadAllText(jsonPath));
                    if (list != null) draft.Belts = list;
                }
                catch { }
            }
            return draft;
        }

        static string SuffixFolder(string packFs, string suffix)
        {
            return string.IsNullOrEmpty(suffix) ? packFs : Path.Combine(packFs, suffix);
        }

        static string DamageFolder(string suffixRoot, int damage)
        {
            return damage <= 0 ? suffixRoot : Path.Combine(suffixRoot, "Damage" + damage);
        }

        public static void SaveDraft(string characterBasePath, SkinPackDraft draft)
        {
            if (draft == null || string.IsNullOrEmpty(draft.PackFolderName)) return;
            EnsureCustomSkinsRoot(characterBasePath);
            string packFs = DBPU.DbpuCustomSkinLoader.GetPackFolderFs(characterBasePath, draft.PackFolderName);
            if (!Directory.Exists(packFs)) Directory.CreateDirectory(packFs);
            CustomTransformationCatalog.InvalidateDiskSpriteCache(packFs);

            var limbEntries = draft.LimbPaths.ToList();
            foreach (var kvp in limbEntries)
            {
                if (string.IsNullOrEmpty(kvp.Value)) continue;
                string[] parts = kvp.Key.Split('|');
                if (parts.Length < 3) continue;
                string suffix = parts[0];
                if (!int.TryParse(parts[1], out int damage)) continue;
                string limb = parts[2];
                string src = ResolveFs(characterBasePath, kvp.Value);
                if (string.IsNullOrEmpty(src) || !File.Exists(src)) continue;

                string suffixRoot = SuffixFolder(packFs, suffix);
                string destDir = DamageFolder(suffixRoot, damage);
                if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);
                string dest = Path.Combine(destDir, limb + ".png");
                CustomTransformationCatalog.CopyFileShared(src, dest, true);
                draft.LimbPaths[kvp.Key] = dest;
            }

            if (!File.Exists(Path.Combine(packFs, "Upper.png")))
                WriteBlankPng(Path.Combine(packFs, "Upper.png"), 32, 32);

            if (draft.HasBuffed)
            {
                string b = Path.Combine(packFs, "_Buffed");
                if (!Directory.Exists(b)) Directory.CreateDirectory(b);
                if (!File.Exists(Path.Combine(b, "Upper.png")))
                    CopyLimbFolder(packFs, b);
            }
            if (draft.HasBuffed2)
            {
                string b2 = Path.Combine(packFs, "_Buffed2");
                if (!Directory.Exists(b2)) Directory.CreateDirectory(b2);
                if (!File.Exists(Path.Combine(b2, "Upper.png")))
                    CopyLimbFolder(packFs, b2);
            }

            string illDir = Path.Combine(packFs, "Illustrations");
            if (!Directory.Exists(illDir)) Directory.CreateDirectory(illDir);
            var illEntries = draft.IllustrationPaths.ToList();
            foreach (var kvp in illEntries)
            {
                if (string.IsNullOrEmpty(kvp.Value)) continue;
                string src = ResolveFs(characterBasePath, kvp.Value);
                if (string.IsNullOrEmpty(src) || !File.Exists(src)) continue;
                string dest = Path.Combine(illDir, kvp.Key + ".png");
                CustomTransformationCatalog.CopyFileShared(src, dest, true);
                draft.IllustrationPaths[kvp.Key] = dest;
            }

            string beltsDir = Path.Combine(packFs, "Belts");
            if (!Directory.Exists(beltsDir)) Directory.CreateDirectory(beltsDir);
            if (draft.Belts == null) draft.Belts = new List<DBPU.DbpuCustomSkinLoader.SkinBeltDto>();
            for (int i = 0; i < draft.Belts.Count; i++)
            {
                var belt = draft.Belts[i];
                if (belt == null) continue;
                if (!string.IsNullOrEmpty(belt.SpriteFile))
                {
                    string maybeSrc = ResolveFs(characterBasePath, belt.SpriteFile);
                    string beltsFull = CustomTransformationCatalog.NormalizeFsPath(beltsDir);
                    string maybeFull = maybeSrc != null ? CustomTransformationCatalog.NormalizeFsPath(maybeSrc) : null;
                    if (maybeSrc != null && File.Exists(maybeSrc)
                        && (maybeFull == null || !maybeFull.StartsWith(beltsFull, StringComparison.OrdinalIgnoreCase)))
                    {
                        string destName = "belt_" + i + ".png";
                        CustomTransformationCatalog.CopyFileShared(maybeSrc, Path.Combine(beltsDir, destName), true);
                        belt.SpriteFile = destName;
                    }
                }
            }
            File.WriteAllText(Path.Combine(beltsDir, "belt.json"), JsonConvert.SerializeObject(draft.Belts, Formatting.Indented));
            DBPU.DbpuCustomSkinLoader.InvalidateScanCache(characterBasePath);
            CustomTransformationCatalog.InvalidateDiskSpriteCache(packFs);

            ScanSuffixTree(draft, packFs, "");
            if (draft.HasBuffed) ScanSuffixTree(draft, packFs, "_Buffed");
            if (draft.HasBuffed2) ScanSuffixTree(draft, packFs, "_Buffed2");
        }

        static string FindSourceFolder(string characterBasePath, string sourceSkinKey, string sourceDisplayName, CharacterSkins skins)
        {
            string baseFs = CustomTransformationCatalog.ResolveModPath(characterBasePath);
            var candidates = new List<string>();

            void Consider(string dir)
            {
                if (Directory.Exists(dir) && File.Exists(Path.Combine(dir, "Upper.png")))
                    candidates.Add(dir);
            }

            Consider(baseFs);
            string skinsSub = Path.Combine(baseFs, "Skins");
            if (Directory.Exists(skinsSub))
            {
                foreach (var d in Directory.GetDirectories(skinsSub)) Consider(d);
            }
            if (Directory.Exists(baseFs))
            {
                foreach (var d in Directory.GetDirectories(baseFs))
                {
                    string name = Path.GetFileName(d);
                    if (string.Equals(name, "Custom skins", StringComparison.OrdinalIgnoreCase)) continue;
                    Consider(d);
                }
            }

            string customRoot = DBPU.DbpuCustomSkinLoader.GetCustomSkinsFolderFs(characterBasePath);
            if (Directory.Exists(customRoot))
            {
                foreach (var d in Directory.GetDirectories(customRoot)) Consider(d);
            }

            string[] keys = new[] { sourceSkinKey, sourceDisplayName }
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            bool FolderMatchesKey(string folderFs, string key)
            {
                if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(folderFs)) return false;
                string name = Path.GetFileName(folderFs);
                if (string.Equals(name, key, StringComparison.OrdinalIgnoreCase)) return true;
                if (string.Equals(name.Replace(' ', '_'), key.Replace(' ', '_'), StringComparison.OrdinalIgnoreCase)) return true;
                if (string.Equals(name.Replace('_', ' '), key.Replace('_', ' '), StringComparison.OrdinalIgnoreCase)) return true;
                string nameKey = new string(name.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
                string bareKey = key.StartsWith("Custom_", StringComparison.OrdinalIgnoreCase)
                    ? key.Substring("Custom_".Length)
                    : key;
                if (!string.IsNullOrEmpty(nameKey)
                    && (string.Equals(nameKey, key, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(nameKey, bareKey, StringComparison.OrdinalIgnoreCase)))
                    return true;
                string keyTrim = key.EndsWith("Skin", StringComparison.OrdinalIgnoreCase) ? key.Substring(0, key.Length - 4) : key;
                string nameTrim = name.EndsWith("Skin", StringComparison.OrdinalIgnoreCase) ? name.Substring(0, name.Length - 4) : name;
                if (!string.IsNullOrEmpty(keyTrim) && string.Equals(nameTrim, keyTrim, StringComparison.OrdinalIgnoreCase))
                    return true;
                return false;
            }

            foreach (var key in keys)
            {
                foreach (var c in candidates)
                {
                    if (FolderMatchesKey(c, key)) return c;
                }
            }

            if (skins != null && skins.skins != null && !string.IsNullOrEmpty(sourceSkinKey)
                && skins.skins.SkinData.TryGetValue(sourceSkinKey, out var info) && info != null)
            {
                string dn = info.DisplayName;
                if (!string.IsNullOrEmpty(dn))
                {
                    foreach (var c in candidates)
                    {
                        string name = Path.GetFileName(c);
                        if (string.Equals(name.Replace('_', ' '), dn, StringComparison.OrdinalIgnoreCase))
                            return c;
                        if (FolderMatchesKey(c, dn)) return c;
                    }
                }
            }

            if (File.Exists(Path.Combine(baseFs, "Upper.png"))
                && (string.Equals(sourceSkinKey, "Base", StringComparison.OrdinalIgnoreCase)
                    || string.IsNullOrEmpty(sourceSkinKey)))
                return baseFs;

            return null;
        }

        static void CopyDamageTree(string srcRoot, string dstRoot)
        {
            if (!Directory.Exists(srcRoot)) return;
            if (!Directory.Exists(dstRoot)) Directory.CreateDirectory(dstRoot);
            CopyLimbFolder(srcRoot, dstRoot);
            foreach (var dir in Directory.GetDirectories(srcRoot))
            {
                string name = Path.GetFileName(dir);
                if (!Regex.IsMatch(name, @"^Damage\d+$", RegexOptions.IgnoreCase)) continue;
                CopyLimbFolder(dir, Path.Combine(dstRoot, name));
            }
        }

        public static string CloneBuiltinToCustom(string characterBasePath, string sourceSkinKey, string sourceDisplayName, CharacterSkins skins, string newFolderName = null)
        {
            string srcFs = FindSourceFolder(characterBasePath, sourceSkinKey, sourceDisplayName, skins);
            string folderName = SanitizeFolderName(
                !string.IsNullOrEmpty(newFolderName) ? newFolderName
                : (!string.IsNullOrEmpty(sourceDisplayName) ? sourceDisplayName : sourceSkinKey));
            string packName = CreateEmptyPack(characterBasePath, folderName, sourceSkinKey, sourceDisplayName, skins);
            string packFs = DBPU.DbpuCustomSkinLoader.GetPackFolderFs(characterBasePath, packName);

            if (!string.IsNullOrEmpty(srcFs) && Directory.Exists(srcFs)
                && !CustomTransformationCatalog.PathsEqual(srcFs, packFs))
            {
                CopyDamageTree(srcFs, packFs);

                string buffed = Path.Combine(srcFs, "_Buffed");
                if (Directory.Exists(buffed) && File.Exists(Path.Combine(buffed, "Upper.png")))
                    CopyDamageTree(buffed, Path.Combine(packFs, "_Buffed"));

                string buffed2 = Path.Combine(srcFs, "_Buffed2");
                if (Directory.Exists(buffed2) && File.Exists(Path.Combine(buffed2, "Upper.png")))
                    CopyDamageTree(buffed2, Path.Combine(packFs, "_Buffed2"));

                string illSrc = Path.Combine(srcFs, "Illustrations");
                if (Directory.Exists(illSrc))
                {
                    string illDst = Path.Combine(packFs, "Illustrations");
                    if (!Directory.Exists(illDst)) Directory.CreateDirectory(illDst);
                    foreach (var f in Directory.GetFiles(illSrc, "*.png"))
                        CustomTransformationCatalog.CopyFileShared(f, Path.Combine(illDst, Path.GetFileName(f)), true);
                }

                string beltSrc = Path.Combine(srcFs, "Belts");
                if (Directory.Exists(beltSrc))
                {
                    string beltDst = Path.Combine(packFs, "Belts");
                    if (!Directory.Exists(beltDst)) Directory.CreateDirectory(beltDst);
                    foreach (var f in Directory.GetFiles(beltSrc))
                        CustomTransformationCatalog.CopyFileShared(f, Path.Combine(beltDst, Path.GetFileName(f)), true);
                }
            }

            if (!File.Exists(Path.Combine(packFs, "Upper.png")))
                WriteBlankPng(Path.Combine(packFs, "Upper.png"), 32, 32);

            DBPU.DbpuCustomSkinLoader.InvalidateScanCache(characterBasePath);
            return packName;
        }
    }

    public static class SkinAutoDamage
    {
        public static void GenerateDamageVariants(
            string packFsPath,
            int count,
            float intensity,
            int seed,
            bool tears,
            bool fray,
            bool dirt,
            bool skinReveal,
            bool alsoBuffed,
            bool alsoBuffed2)
        {
            if (string.IsNullOrEmpty(packFsPath) || !Directory.Exists(packFsPath)) return;
            count = Mathf.Clamp(count, 1, 20);
            intensity = Mathf.Clamp01(intensity);
            var rng = new System.Random(seed);

            ProcessTree(packFsPath, count, intensity, rng, tears, fray, dirt, skinReveal);
            if (alsoBuffed)
            {
                string b = Path.Combine(packFsPath, "_Buffed");
                if (Directory.Exists(b) && File.Exists(Path.Combine(b, "Upper.png")))
                    ProcessTree(b, count, intensity, new System.Random(seed + 17), tears, fray, dirt, skinReveal);
            }
            if (alsoBuffed2)
            {
                string b2 = Path.Combine(packFsPath, "_Buffed2");
                if (Directory.Exists(b2) && File.Exists(Path.Combine(b2, "Upper.png")))
                    ProcessTree(b2, count, intensity, new System.Random(seed + 31), tears, fray, dirt, skinReveal);
            }
        }

        static void ProcessTree(string rootFs, int count, float intensity, System.Random rng, bool tears, bool fray, bool dirt, bool skinReveal)
        {
            var baseTex = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
            foreach (var limb in SkinPackIO.LimbNames)
            {
                string path = Path.Combine(rootFs, limb + ".png");
                if (!File.Exists(path) && limb == "Head")
                    path = Path.Combine(rootFs, "Face.png");
                if (!File.Exists(path)) continue;
                var tex = LoadReadable(path);
                if (tex != null) baseTex[limb] = tex;
            }
            if (baseTex.Count == 0) return;

            Color skinTone = SampleSkinTone(baseTex.Values.First());
            for (int i = 1; i <= count; i++)
            {
                float t = (i / (float)count) * intensity;
                string destDir = Path.Combine(rootFs, "Damage" + i);
                if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);
                foreach (var kvp in baseTex)
                {
                    var worn = ApplyWear(kvp.Value, t, rng, tears, fray, dirt, skinReveal, skinTone);
                    File.WriteAllBytes(Path.Combine(destDir, kvp.Key + ".png"), worn.EncodeToPNG());
                    CustomTransformationCatalog.InvalidateDiskSpriteCache(Path.Combine(destDir, kvp.Key + ".png"));
                    UnityEngine.Object.Destroy(worn);
                }
            }

            foreach (var tex in baseTex.Values)
                UnityEngine.Object.Destroy(tex);
        }

        static Texture2D LoadReadable(string path)
        {
            byte[] bytes = CustomTransformationCatalog.ReadAllBytesShared(path);
            if (bytes == null || bytes.Length == 0) return null;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(bytes))
            {
                UnityEngine.Object.Destroy(tex);
                return null;
            }
            tex.filterMode = FilterMode.Point;
            return tex;
        }

        static Color SampleSkinTone(Texture2D tex)
        {
            var fallback = new Color(0.93f, 0.72f, 0.55f, 1f);
            if (tex == null) return fallback;
            var pixels = tex.GetPixels();
            float r = 0f, g = 0f, b = 0f;
            int n = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                var c = pixels[i];
                if (c.a < 0.4f) continue;
                if (c.r > 0.7f && c.g > 0.45f && c.g < 0.85f && c.b > 0.3f && c.b < 0.75f && c.r > c.b)
                {
                    r += c.r; g += c.g; b += c.b;
                    n++;
                }
            }
            if (n < 8) return fallback;
            return new Color(r / n, g / n, b / n, 1f);
        }

        static Texture2D ApplyWear(Texture2D src, float t, System.Random rng, bool tears, bool fray, bool dirt, bool skinReveal, Color skinTone)
        {
            int w = src.width;
            int h = src.height;
            var dst = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = src.GetPixels();
            var outPx = new Color[px.Length];
            Array.Copy(px, outPx, px.Length);

            bool[] solid = new bool[px.Length];
            for (int i = 0; i < px.Length; i++) solid[i] = px[i].a > 0.15f;

            int holeCount = tears ? Mathf.RoundToInt(2 + t * 14f) : 0;
            for (int hIdx = 0; hIdx < holeCount; hIdx++)
            {
                int cx = rng.Next(2, Math.Max(3, w - 2));
                int cy = rng.Next(2, Math.Max(3, h - 2));
                float rad = 1.2f + (float)rng.NextDouble() * (1.5f + t * 4f);
                int rMax = Mathf.CeilToInt(rad) + 1;
                for (int dy = -rMax; dy <= rMax; dy++)
                for (int dx = -rMax; dx <= rMax; dx++)
                {
                    int x = cx + dx;
                    int y = cy + dy;
                    if (x < 0 || y < 0 || x >= w || y >= h) continue;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) + (float)(rng.NextDouble() * 0.8f - 0.4f);
                    if (d > rad) continue;
                    int idx = y * w + x;
                    if (!solid[idx]) continue;
                    if (skinReveal)
                    {
                        float edge = Mathf.Clamp01(1f - (d / rad));
                        outPx[idx] = Color.Lerp(outPx[idx], skinTone, 0.55f + edge * 0.45f);
                        outPx[idx].a = px[idx].a;
                    }
                    else
                    {
                        outPx[idx].a = 0f;
                    }
                }
            }

            for (int i = 0; i < outPx.Length; i++)
            {
                if (outPx[i].a < 0.05f) continue;
                float dark = 1f - t * 0.35f;
                outPx[i].r *= dark;
                outPx[i].g *= dark * (1f - t * 0.05f);
                outPx[i].b *= dark * (1f - t * 0.08f);
            }

            if (dirt)
            {
                int speckles = Mathf.RoundToInt(px.Length * (0.002f + t * 0.02f));
                for (int s = 0; s < speckles; s++)
                {
                    int idx = rng.Next(0, outPx.Length);
                    if (outPx[idx].a < 0.2f) continue;
                    float shade = 0.15f + (float)rng.NextDouble() * 0.25f;
                    outPx[idx] = Color.Lerp(outPx[idx], new Color(shade, shade * 0.85f, shade * 0.6f, outPx[idx].a), 0.4f + t * 0.4f);
                }
            }

            if (fray)
            {
                for (int y = 1; y < h - 1; y++)
                for (int x = 1; x < w - 1; x++)
                {
                    int idx = y * w + x;
                    if (outPx[idx].a < 0.2f) continue;
                    bool edge = false;
                    for (int dy = -1; dy <= 1 && !edge; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        if (outPx[(y + dy) * w + (x + dx)].a < 0.15f) { edge = true; break; }
                    }
                    if (!edge) continue;
                    if (rng.NextDouble() > 0.12 + t * 0.35) continue;
                    if (rng.NextDouble() < 0.45)
                        outPx[idx].a = 0f;
                    else
                    {
                        outPx[idx].r *= 0.55f;
                        outPx[idx].g *= 0.55f;
                        outPx[idx].b *= 0.55f;
                    }
                }
            }

            dst.SetPixels(outPx);
            dst.Apply(false, false);
            return dst;
        }
    }

    public class SkinSharePack
    {
        public string Format = "DBPU_SkinShare_v1";
        public string CharacterId;
        public string DisplayName;
        public string SkinName;
        public string PackFolderName;
    }

    public class SkinWorkshopPayload
    {
        public string SkinName;
        public string PackFolderName;
        public Dictionary<string, string> Files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public static class SkinShare
    {
        public const string FileFilter = "Singularity Skin (*.dbpusk)|*.dbpusk|Zip Archive (*.zip)|*.zip|All files (*.*)|*.*";

        public static bool ExportPack(string characterId, string displayName, string characterBasePath, string packFolderName, string skinName)
        {
            if (string.IsNullOrEmpty(characterBasePath) || string.IsNullOrEmpty(packFolderName))
                throw new InvalidOperationException("Nothing to export.");

            string packFs = DBPU.DbpuCustomSkinLoader.GetPackFolderFs(characterBasePath, packFolderName);
            if (!Directory.Exists(packFs))
                throw new InvalidOperationException("Skin pack folder not found.");

            string safeName = Sanitize(skinName ?? packFolderName) + ".dbpusk";
            string dest = TmFilePicker.SaveFile("Share Skin", FileFilter, safeName);
            if (string.IsNullOrEmpty(dest)) return false;
            if (!dest.EndsWith(".dbpusk", StringComparison.OrdinalIgnoreCase)
                && !dest.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                dest += ".dbpusk";

            string tempRoot = Path.Combine(Path.GetTempPath(), "DBPU_SkinShare_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            try
            {
                CopyDirectory(packFs, Path.Combine(tempRoot, "pack"));
                var meta = new SkinSharePack
                {
                    CharacterId = characterId,
                    DisplayName = displayName,
                    SkinName = string.IsNullOrEmpty(skinName) ? packFolderName : skinName,
                    PackFolderName = packFolderName
                };
                File.WriteAllText(Path.Combine(tempRoot, "skin.json"), JsonConvert.SerializeObject(meta, Formatting.Indented));
                if (File.Exists(dest)) File.Delete(dest);
                ZipFile.CreateFromDirectory(tempRoot, dest, System.IO.Compression.CompressionLevel.Optimal, false);
                return true;
            }
            finally
            {
                try { if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true); } catch { }
            }
        }

        public static string ImportPack(string characterBasePath, CharacterSkins characterSkins, string preferredName = null)
        {
            string src = TmFilePicker.OpenFile("Import Skin", FileFilter,
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            if (string.IsNullOrEmpty(src)) return null;
            return ImportPackFromPath(src, characterBasePath, characterSkins, preferredName);
        }

        public static string ImportPackFromPath(string src, string characterBasePath, CharacterSkins characterSkins, string preferredName = null)
        {
            src = NormalizeImportPath(src);
            if (string.IsNullOrEmpty(src))
                throw new InvalidOperationException("No path provided.");
            if (!File.Exists(src))
                throw new InvalidOperationException("File not found:\n" + src);
            if (string.IsNullOrEmpty(characterBasePath))
                throw new InvalidOperationException("No character selected.");

            string extractRoot = Path.Combine(Path.GetTempPath(), "DBPU_SkinImport_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(extractRoot);
            try
            {
                ZipFile.ExtractToDirectory(src, extractRoot);
                string jsonPath = Path.Combine(extractRoot, "skin.json");
                if (!File.Exists(jsonPath))
                {
                    var found = Directory.GetFiles(extractRoot, "skin.json", SearchOption.AllDirectories).FirstOrDefault();
                    if (found == null) throw new InvalidOperationException("Pack is missing skin.json.");
                    jsonPath = found;
                }

                var meta = JsonConvert.DeserializeObject<SkinSharePack>(File.ReadAllText(jsonPath));
                string packSrc = Path.Combine(Path.GetDirectoryName(jsonPath) ?? extractRoot, "pack");
                if (!Directory.Exists(packSrc) || !File.Exists(Path.Combine(packSrc, "Upper.png")))
                {
                    packSrc = Path.GetDirectoryName(jsonPath);
                    if (string.IsNullOrEmpty(packSrc) || !File.Exists(Path.Combine(packSrc, "Upper.png")))
                    {
                        var upper = Directory.GetFiles(extractRoot, "Upper.png", SearchOption.AllDirectories).FirstOrDefault();
                        if (upper == null) throw new InvalidOperationException("Pack has no Upper.png.");
                        packSrc = Path.GetDirectoryName(upper);
                    }
                }

                string folderName = SkinPackIO.SanitizeFolderName(
                    !string.IsNullOrEmpty(preferredName) ? preferredName
                    : (!string.IsNullOrEmpty(meta?.SkinName) ? meta.SkinName : meta?.PackFolderName));
                string customRoot = SkinPackIO.EnsureCustomSkinsRoot(characterBasePath);
                string dest = Path.Combine(customRoot, folderName);
                int n = 1;
                string unique = folderName;
                while (Directory.Exists(dest))
                {
                    unique = folderName + "_" + n;
                    dest = Path.Combine(customRoot, unique);
                    n++;
                }
                CopyDirectory(packSrc, dest);
                try { File.Delete(Path.Combine(dest, "skin.json")); } catch { }

                DBPU.DbpuCustomSkinLoader.InvalidateScanCache(characterBasePath);
                if (characterSkins != null)
                    DBPU.DbpuCustomSkinLoader.UpsertPackIntoCharacter(characterSkins, characterBasePath, unique, "Head.png", true);
                return unique;
            }
            finally
            {
                try { if (Directory.Exists(extractRoot)) Directory.Delete(extractRoot, true); } catch { }
            }
        }

        public static SkinWorkshopPayload BuildWorkshopPayload(string characterBasePath, string packFolderName, string skinName, out Dictionary<string, string> assets)
        {
            assets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var payload = new SkinWorkshopPayload
            {
                SkinName = string.IsNullOrEmpty(skinName) ? packFolderName : skinName,
                PackFolderName = packFolderName,
                Files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            };

            string packFs = DBPU.DbpuCustomSkinLoader.GetPackFolderFs(characterBasePath, packFolderName);
            if (!Directory.Exists(packFs))
                throw new InvalidOperationException("Skin pack folder not found.");

            int i = 0;
            foreach (string file in Directory.GetFiles(packFs, "*.*", SearchOption.AllDirectories))
            {
                string ext = Path.GetExtension(file);
                if (string.IsNullOrEmpty(ext)) continue;
                if (!ext.Equals(".png", StringComparison.OrdinalIgnoreCase)
                    && !ext.Equals(".json", StringComparison.OrdinalIgnoreCase))
                    continue;
                string rel = file.Substring(packFs.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Replace('\\', '/');
                if (string.IsNullOrEmpty(rel)) continue;
                string key = "f" + i;
                i++;
                assets[key] = file;
                payload.Files[key] = rel;
            }
            if (payload.Files.Count == 0)
                throw new InvalidOperationException("Skin pack has no files to upload.");
            return payload;
        }

        public static string InstallWorkshopPayload(
            string packFolder,
            Dictionary<string, string> packAssets,
            SkinWorkshopPayload payload,
            string characterBasePath,
            CharacterSkins characterSkins)
        {
            if (payload == null || payload.Files == null || payload.Files.Count == 0)
                throw new InvalidOperationException("Pack has no skin files.");
            if (string.IsNullOrEmpty(characterBasePath))
                throw new InvalidOperationException("No character selected.");

            string folderName = SkinPackIO.SanitizeFolderName(
                !string.IsNullOrEmpty(payload.SkinName) ? payload.SkinName : payload.PackFolderName);
            string customRoot = SkinPackIO.EnsureCustomSkinsRoot(characterBasePath);
            string destRoot = Path.Combine(customRoot, folderName);
            int n = 1;
            string unique = folderName;
            while (Directory.Exists(destRoot))
            {
                unique = folderName + "_" + n;
                destRoot = Path.Combine(customRoot, unique);
                n++;
            }
            Directory.CreateDirectory(destRoot);

            foreach (var kvp in payload.Files)
            {
                if (string.IsNullOrEmpty(kvp.Value)) continue;
                string src = null;
                if (packAssets != null && packAssets.TryGetValue(kvp.Key, out string assetRel) && !string.IsNullOrEmpty(assetRel))
                {
                    src = Path.Combine(packFolder, assetRel.Replace('/', Path.DirectorySeparatorChar));
                    if (!File.Exists(src))
                        src = Path.Combine(packFolder, Path.GetFileName(assetRel));
                }
                if ((string.IsNullOrEmpty(src) || !File.Exists(src)) && !string.IsNullOrEmpty(kvp.Key))
                {
                    src = Path.Combine(packFolder, "assets", kvp.Key + ".png");
                    if (!File.Exists(src))
                        src = Path.Combine(packFolder, Path.GetFileName(kvp.Value));
                }
                if (string.IsNullOrEmpty(src) || !File.Exists(src)) continue;

                string dest = Path.Combine(destRoot, kvp.Value.Replace('/', Path.DirectorySeparatorChar));
                string destDir = Path.GetDirectoryName(dest);
                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                    Directory.CreateDirectory(destDir);
                CustomTransformationCatalog.CopyFileShared(src, dest, true);
            }

            if (!File.Exists(Path.Combine(destRoot, "Upper.png")))
                throw new InvalidOperationException("Installed skin is missing Upper.png.");

            DBPU.DbpuCustomSkinLoader.InvalidateScanCache(characterBasePath);
            if (characterSkins != null)
                DBPU.DbpuCustomSkinLoader.UpsertPackIntoCharacter(characterSkins, characterBasePath, unique, "Head.png", true);
            return unique;
        }

        static void CopyDirectory(string src, string dest)
        {
            Directory.CreateDirectory(dest);
            foreach (string dir in Directory.GetDirectories(src, "*", SearchOption.AllDirectories))
            {
                string rel = dir.Substring(src.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                Directory.CreateDirectory(Path.Combine(dest, rel));
            }
            foreach (string file in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
            {
                string rel = file.Substring(src.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string target = Path.Combine(dest, rel);
                string targetDir = Path.GetDirectoryName(target);
                if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
                    Directory.CreateDirectory(targetDir);
                CustomTransformationCatalog.CopyFileShared(file, target, true);
            }
        }

        static string NormalizeImportPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            path = path.Trim().Trim('"').Trim();
            if (string.IsNullOrEmpty(path)) return null;
            try { return Path.GetFullPath(path); }
            catch { return path; }
        }

        static string Sanitize(string name)
        {
            if (string.IsNullOrEmpty(name)) return "skin";
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Trim();
        }
    }

    public static class SkinMakerState
    {
        public static bool Armed;
        public static string Mode;

        public static void Disarm()
        {
            Armed = false;
            Mode = null;
        }

        public static void Arm(string mode)
        {
            Armed = true;
            Mode = mode;
        }
    }

    public class SkinMakerInput : MonoBehaviour
    {
        static SkinMakerInput _instance;

        public static void Ensure()
        {
            if (_instance != null) return;
            var go = new GameObject("SkinMakerInput");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<SkinMakerInput>();
        }

        static bool PointerOverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (DialogBox.IsAnyDialogboxOpen) return;
                if (CustomSkinMakerUI.IsCaptureOpen)
                {
                    CustomSkinMakerUI.CancelIllustrationCapture();
                    return;
                }
                if (CustomSkinMakerUI.HasOverlay)
                {
                    CustomSkinMakerUI.CloseOverlay(true);
                    return;
                }
                if (CustomSkinMakerUI.IsOpen)
                {
                    CustomSkinMakerUI.Close();
                    return;
                }
                if (SkinMakerState.Armed)
                {
                    SkinMakerState.Disarm();
                    CustomSkinMakerUI.EmergencyUnstick();
                    ModAPI.Notify("Skin Maker cancelled");
                    return;
                }
                CustomSkinMakerUI.EmergencyUnstick();
            }

            if (!SkinMakerState.Armed) return;
            if (!Input.GetMouseButtonDown(0) || PointerOverUi()) return;

            var pb = SelectionController.Main.CurrentlyUnderMouse;
            if (pb == null) return;
            var person = pb.transform.root.GetComponent<PersonBehaviour>();
            if (person == null) return;
            CustomSkinMakerUI.OnWorldPersonClicked(person);
        }
    }

    public class SmLimbPreviewClick : MonoBehaviour, IPointerClickHandler
    {
        public string LimbName;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (string.IsNullOrEmpty(LimbName)) return;
            CustomSkinMakerUI.OnPreviewLimbClicked(LimbName);
        }
    }

    public class SmSkinCellDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler, IPointerDownHandler
    {
        public string SkinKey;
        public int Index;
        bool _dragging;
        bool _moved;
        RectTransform _rt;
        CanvasGroup _group;

        void Awake()
        {
            _rt = transform as RectTransform;
            _group = gameObject.GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _moved = false;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            _dragging = true;
            _moved = false;
            if (_group != null) _group.blocksRaycasts = false;
            transform.SetAsLastSibling();
            CustomSkinMakerUI.BeginSkinCellDrag(this);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging || _rt == null) return;
            if (eventData.delta.sqrMagnitude > 0.01f) _moved = true;
            CustomSkinMakerUI.DragSkinCell(this, eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragging) return;
            _dragging = false;
            if (_group != null) _group.blocksRaycasts = true;
            CustomSkinMakerUI.EndSkinCellDrag(this, _moved);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (_dragging || _moved) return;
            if (string.IsNullOrEmpty(SkinKey)) return;
            CustomSkinMakerUI.SelectSkinFromGrid(SkinKey);
        }
    }

    public class SkinOrderDto
    {
        public List<string> Order = new List<string>();
    }

    public static class CustomSkinMakerUI
    {
        enum ScreenKind { PickPerson, Skins, Editor, Damage, Buffed, Belts, Illustrations, ShareChoice, Workshop, WorkshopPublish }

        const float UiScale = 7.2f;
        const float PanelW = 180f;
        const float PanelH = 108f;
        const float TitleH = 8f;
        const float NavH = 8f;
        const float CellW = 24f;
        const float CellH = 36f;
        const float CellGap = 5f;
        const int GridCols = 6;

        static readonly Color PanelBg = new Color(0.06f, 0.06f, 0.06f, 0.98f);
        static readonly Color TitleBarBg = new Color(0.14f, 0.14f, 0.14f, 1f);
        static readonly Color CellBg = new Color(0.12f, 0.12f, 0.12f, 1f);
        static readonly Color NameBar = new Color(0f, 0f, 0f, 0.7f);
        static readonly Color Accent = new Color(0.35f, 0.55f, 0.85f, 1f);

        static GameObject _root;
        static GameObject _blocker;
        static GameObject _overlay;
        static RectTransform _panelHostRt;
        static RectTransform _panelRt;
        static TextMeshProUGUI _titleTmp;
        static Transform _body;
        static TMP_FontAsset _font;

        static ScreenKind _screen = ScreenKind.PickPerson;
        static string _characterBasePath;
        static string _displayName;
        static string _characterId;
        static PersonBehaviour _targetPerson;
        static CharacterSkins _targetSkins;
        static SkinContextMenu _skinMenu;
        static int _makerUiHideDepth;
        static bool _makerUiWantVisible;
        static SkinPackDraft _draft;
        static string _previewSelectedLimb;
        static float _previewFitScale = 1f;
        static int _spriteTab;
        static List<(string Path, Sprite Sprite)> _limbCandidates = new List<(string, Sprite)>();
        static string _selectedHairPath;
        static string _spriteSearch = "";
        static Transform _spriteGridContent;
        static List<string> _skinGridKeys = new List<string>();
        static List<RectTransform> _skinGridCells = new List<RectTransform>();
        static Transform _skinGridContent;
        static ScrollRect _skinGridScroll;
        static SmSkinCellDrag _skinDragActive;
        static Dictionary<string, CharacterSkins.Skins.SkinInfo> _skinGridInfos =
            new Dictionary<string, CharacterSkins.Skins.SkinInfo>(StringComparer.OrdinalIgnoreCase);

        static readonly string[] SpriteCategories =
        {
            "Hair", "Head", "Upper", "Middle", "Lower", "ULeg", "LLeg", "Foot", "UArm", "LArm"
        };
        const int SpriteGridCols = 2;

        static int _illustrationDamage;
        static string _captureIllKey;
        static GameObject _captureRoot;
        static RectTransform _captureFrameRt;
        static GameObject _captureHud;
        static bool _captureBusy;
        static int _captureSx, _captureSy, _captureSw, _captureSh;
        static TmCaptureDriver _captureDriver;

        const string EmptyIllustrationRel = "Assets/Icons/Empty illustration.png";
        const string EmptyIllustrationRelAlt = "Assets/Icons/EmptyIllustration.png";
        const int IllFrameW = 1008;
        const int IllFrameH = 1472;
        const int IllContentX = 48;
        const int IllContentY = 48;
        const int IllContentW = 912;
        const int IllContentH = 1376;

        static string _sharePackFolder;
        static string _shareSkinName;
        static string _workshopPublishTitle;
        static string _workshopPublishDescription;
        static string _workshopPublishPreviewPath;
        static bool _workshopUploading;
        static string _workshopUploadStatus;
        static List<DbpuWorkshopRemoteItem> _workshopRemote;
        static int _workshopPage = 1;
        static string _workshopStatus;
        static bool _workshopLoading;

        public static bool IsOpen => _root != null;
        public static bool HasOverlay => _overlay != null;
        public static bool IsCaptureOpen => _captureRoot != null;

        static TMP_FontAsset Font
        {
            get
            {
                if (_font == null)
                    _font = ModAPI.FindSpawnable("Holographic Display").Prefab.transform.Find("Text").GetComponent<TextMeshPro>().font;
                return _font;
            }
        }

        public static void Open()
        {
            if (IsOpen) Close();
            CloseOverlay(false);
            PowerGiverState.Disarm();
            CharacterPowerPickerState.Disarm();
            TransformationMakerState.Disarm();
            SkinMakerInput.Ensure();

            _screen = ScreenKind.PickPerson;
            _characterBasePath = null;
            _displayName = null;
            _characterId = null;
            _targetPerson = null;
            _targetSkins = null;
            _skinMenu = null;
            _draft = null;
            _selectedHairPath = null;

            SetWorldClickMode(true);
            ModAPI.Notify("Click a person to open Skin Maker");
        }

        static void EnsureShell()
        {
            if (_root != null)
            {
                _root.SetActive(true);
                return;
            }

            _root = new GameObject("CustomSkinMakerUI");
            _root.transform.SetParent(GameObject.Find("Canvas").transform, false);
            var rootRt = _root.AddComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            _blocker = new GameObject("Blocker");
            _blocker.transform.SetParent(_root.transform, false);
            var blockerRt = _blocker.AddComponent<RectTransform>();
            blockerRt.anchorMin = Vector2.zero;
            blockerRt.anchorMax = Vector2.one;
            blockerRt.offsetMin = Vector2.zero;
            blockerRt.offsetMax = Vector2.zero;
            var blockerImg = _blocker.AddComponent<Image>();
            blockerImg.color = new Color(0f, 0f, 0f, 0.4f);
            blockerImg.raycastTarget = true;

            var host = new GameObject("PanelHost");
            host.transform.SetParent(_root.transform, false);
            _panelHostRt = host.AddComponent<RectTransform>();
            _panelHostRt.anchoredPosition = new Vector2(0f, 55f);
            _panelHostRt.localScale = new Vector3(UiScale, UiScale, 1f);

            var panel = new GameObject("Panel");
            panel.transform.SetParent(host.transform, false);
            _panelRt = panel.AddComponent<RectTransform>();
            _panelRt.sizeDelta = new Vector2(PanelW, PanelH);
            panel.AddComponent<Image>().color = PanelBg;
            var outline = panel.AddComponent<Outline>();
            outline.effectColor = Color.white;
            outline.effectDistance = new Vector2(1.2f, -1.2f);

            var titleBar = new GameObject("TitleBar");
            titleBar.transform.SetParent(panel.transform, false);
            var titleBarRt = titleBar.AddComponent<RectTransform>();
            titleBarRt.anchorMin = new Vector2(0.5f, 1f);
            titleBarRt.anchorMax = new Vector2(0.5f, 1f);
            titleBarRt.pivot = new Vector2(0.5f, 1f);
            titleBarRt.anchoredPosition = Vector2.zero;
            titleBarRt.sizeDelta = new Vector2(PanelW, TitleH);
            titleBar.AddComponent<Image>().color = TitleBarBg;
            MenuUI.ConfigureTitleDragArea(titleBar, _panelHostRt, new Vector2(PanelW, TitleH));

            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(titleBar.transform, false);
            var titleRt = titleGo.AddComponent<RectTransform>();
            titleRt.anchorMin = Vector2.zero;
            titleRt.anchorMax = Vector2.one;
            titleRt.offsetMin = new Vector2(4f, 0f);
            titleRt.offsetMax = new Vector2(-4f, 0f);
            _titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
            _titleTmp.font = Font;
            _titleTmp.fontSize = 3.8f;
            _titleTmp.alignment = TextAlignmentOptions.Center;
            _titleTmp.color = Color.white;
            _titleTmp.raycastTarget = false;

            if (DBPU.PCloseButton != null)
            {
                var closeBtn = new GameObject("Close");
                closeBtn.transform.SetParent(host.transform, false);
                MenuUI.ApplyImage(closeBtn, DBPU.PCloseButton, new Vector2(0f, -PanelH * 0.5f - 6f), 0.45f);
                closeBtn.AddComponent<Button>().onClick.AddListener(Close);
            }

            MenuUI.AttachResizeHandle(
                _panelRt,
                () => _panelHostRt.localScale.x,
                u => _panelHostRt.localScale = new Vector3(u, u, 1f),
                5f,
                14f,
                8f);
        }

        public static void EmergencyUnstick()
        {
            CloseOverlay(false);
            ForceClearUiBlocker();
        }

        public static void Close()
        {
            CancelIllustrationCapture(false);
            CloseOverlay(false);
            SkinMakerState.Disarm();
            _makerUiHideDepth = 0;
            _makerUiWantVisible = false;
            _targetPerson = null;
            _targetSkins = null;
            _skinMenu = null;
            _body = null;
            _panelRt = null;
            _panelHostRt = null;
            _titleTmp = null;
            _blocker = null;
            _draft = null;
            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root);
                _root = null;
            }
            ForceClearUiBlocker();
        }

        static void HideMakerUi()
        {
            if (_root == null) return;
            if (_makerUiHideDepth == 0)
                _makerUiWantVisible = _root.activeSelf;
            _makerUiHideDepth++;
            if (_root.activeSelf)
                _root.SetActive(false);
            CloseOverlay(false);
        }

        static void ShowMakerUi()
        {
            if (_makerUiHideDepth <= 0) return;
            _makerUiHideDepth--;
            if (_makerUiHideDepth == 0 && _makerUiWantVisible && _root != null)
                _root.SetActive(true);
        }

        static void RunWithUiYield(Action action)
        {
            HideMakerUi();
            try { action?.Invoke(); }
            finally { ShowMakerUi(); }
        }

        static void ShowConfirmDialog(string message, string confirmLabel, Action onConfirm)
        {
            HideMakerUi();
            DialogBoxManager.Dialog(
                message,
                new DialogButton(confirmLabel, true, new UnityAction[]
                {
                    () =>
                    {
                        try { onConfirm?.Invoke(); }
                        finally { ShowMakerUi(); }
                    }
                }),
                new DialogButton("Cancel", true, new UnityAction[]
                {
                    () => ShowMakerUi()
                }));
        }

        static void ShowNotify(string message)
        {
            HideMakerUi();
            DialogBoxManager.Dialog(
                message,
                new DialogButton("OK", true, new UnityAction[]
                {
                    () => ShowMakerUi()
                }));
        }

        public static void CloseOverlay(bool restoreMain)
        {
            if (_overlay != null)
            {
                UnityEngine.Object.Destroy(_overlay);
                _overlay = null;
            }
            if (restoreMain && _root != null)
                _root.SetActive(true);
        }

        static void ForceClearUiBlocker()
        {
            try
            {
                for (int i = 0; i < 8; i++)
                    Global.main.RemoveUiBlocker();
            }
            catch { }
        }

        static void HideForWorldPick()
        {
            CloseOverlay(false);
            if (_root != null) _root.SetActive(false);
            SkinMakerState.Arm("PickPerson");
            ModAPI.Notify("Click a person in the world");
        }

        static void SetWorldClickMode(bool picking)
        {
            if (_blocker != null && _root != null && _root.activeSelf)
            {
                var img = _blocker.GetComponent<Image>();
                if (img != null)
                {
                    img.raycastTarget = !picking;
                    img.color = picking ? new Color(0f, 0f, 0f, 0.15f) : new Color(0f, 0f, 0f, 0.4f);
                }
            }
            if (picking) SkinMakerState.Arm("PickPerson");
            else SkinMakerState.Disarm();
        }

        public static void OnPreviewLimbClicked(string limbName)
        {
            if (string.IsNullOrEmpty(limbName) || _draft == null) return;
            string mapped = MapLimbName(limbName);
            _draft.SelectedLimb = mapped;
            _previewSelectedLimb = mapped;
            if (_screen == ScreenKind.Editor) RebuildBody();
        }

        static string MapLimbName(string limbName)
        {
            if (string.IsNullOrEmpty(limbName)) return "Upper";
            string n = limbName.ToLowerInvariant().Replace(" ", "");
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
            return limbName;
        }

        public static void OnWorldPersonClicked(PersonBehaviour person)
        {
            if (person == null) return;
            bool editing = _screen == ScreenKind.Editor || _screen == ScreenKind.Damage
                || _screen == ScreenKind.Buffed || _screen == ScreenKind.Belts
                || _screen == ScreenKind.Illustrations;
            if (_screen != ScreenKind.PickPerson && !editing) return;

            var root = person.transform.root.gameObject;
            EnsurePersonSkinSupport(person);

            var identity = root.GetComponent<DbpuCharacterIdentity>();
            _targetPerson = person;
            _targetSkins = root.GetComponent<CharacterSkins>();
            _characterBasePath = SkinPackIO.ResolveCharacterBasePath(person);
            if (identity != null)
            {
                _characterId = identity.CharacterId;
                _displayName = identity.DisplayName ?? identity.CharacterId;
                if (string.IsNullOrEmpty(identity.SpriteFolder))
                    identity.SpriteFolder = _characterBasePath;
            }
            else
            {
                _characterId = person.name;
                _displayName = person.name;
            }

            var head = FindHead(person);
            if (head != null)
                _skinMenu = head.GetComponent<SkinContextMenu>();

            EnsureShell();

            if (editing && _draft != null)
            {
                ApplyDraftLive();
                DbpuMakerPhysics.CalmPerson(person);
                ModAPI.Notify("Preview bound to " + _displayName);
                SetWorldClickMode(false);
                RebuildBody();
                return;
            }

            _screen = ScreenKind.Skins;
            SetWorldClickMode(false);
            RebuildBody();
            DbpuMakerPhysics.CalmPerson(person);
            ModAPI.Notify("Editing skins for " + _displayName);
        }

        static Transform FindHead(PersonBehaviour person)
        {
            if (person == null) return null;
            var root = person.transform.root;
            var head = root.Find("Head");
            if (head != null) return head;
            if (person.Limbs != null)
            {
                for (int i = 0; i < person.Limbs.Length; i++)
                {
                    if (person.Limbs[i] != null && person.Limbs[i].name.IndexOf("Head", StringComparison.OrdinalIgnoreCase) >= 0)
                        return person.Limbs[i].transform;
                }
            }
            return null;
        }

        public static void EnsurePersonSkinSupport(PersonBehaviour person)
        {
            if (person == null) return;
            var root = person.transform.root.gameObject;
            var identity = root.GetComponent<DbpuCharacterIdentity>();
            if (identity == null)
            {
                identity = root.AddComponent<DbpuCharacterIdentity>();
                identity.CharacterId = "World_" + root.GetInstanceID().ToString("x");
                identity.DisplayName = string.IsNullOrEmpty(root.name) ? "Custom Character" : root.name;
            }
            if (string.IsNullOrEmpty(identity.SpriteFolder))
                identity.SpriteFolder = SkinPackIO.ResolveCharacterBasePath(person);

            var skins = root.GetComponent<CharacterSkins>();
            if (skins == null) skins = root.AddComponent<CharacterSkins>();
            if (skins.skins == null) skins.skins = new CharacterSkins.Skins();

            var head = FindHead(person);
            if (head != null)
            {
                var menu = head.GetComponent<SkinContextMenu>();
                if (menu == null) menu = head.gameObject.AddComponent<SkinContextMenu>();
                menu.SkinsData = skins;
                menu.person = person;
                if (string.IsNullOrEmpty(menu.CurrentSkin) && skins.skins.SkinData.Count > 0)
                    menu.CurrentSkin = skins.skins.SkinData.Keys.First();
            }

            try
            {
                DBPU.DbpuCustomSkinLoader.RegisterDiscoveredSkins(skins, identity.SpriteFolder ?? SkinPackIO.ResolveCharacterBasePath(person), "Head.png");
            }
            catch { }
        }

        static void RebuildBody()
        {
            if (_panelRt == null) return;
            if (_body != null) UnityEngine.Object.Destroy(_body.gameObject);

            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(_panelRt, false);
            var bodyRt = bodyGo.AddComponent<RectTransform>();
            bodyRt.anchorMin = new Vector2(0.5f, 1f);
            bodyRt.anchorMax = new Vector2(0.5f, 1f);
            bodyRt.pivot = new Vector2(0.5f, 1f);
            bodyRt.anchoredPosition = new Vector2(0f, -(TitleH + 1.2f));
            bodyRt.sizeDelta = new Vector2(PanelW - 4f, PanelH - TitleH - 3f);
            _body = bodyGo.transform;

            switch (_screen)
            {
                case ScreenKind.Skins:
                    _titleTmp.text = (_displayName ?? "Character") + " Skins";
                    BuildSkinsGrid();
                    break;
                case ScreenKind.Editor:
                    _titleTmp.text = _draft?.DisplayName ?? "Skin Editor";
                    BuildEditor();
                    break;
                case ScreenKind.Damage:
                    _titleTmp.text = "Damage Levels";
                    BuildDamageScreen();
                    break;
                case ScreenKind.Buffed:
                    _titleTmp.text = "Buffed Variants";
                    BuildBuffedScreen();
                    break;
                case ScreenKind.Belts:
                    _titleTmp.text = "Belts";
                    BuildBeltsScreen();
                    break;
                case ScreenKind.Illustrations:
                    _titleTmp.text = "Illustrations";
                    BuildIllustrationsScreen();
                    break;
                case ScreenKind.ShareChoice:
                    _titleTmp.text = "Share Skin";
                    BuildShareChoiceScreen();
                    break;
                case ScreenKind.Workshop:
                    _titleTmp.text = "Workshop Skins";
                    BuildWorkshopBrowser();
                    break;
                case ScreenKind.WorkshopPublish:
                    _titleTmp.text = "Publish Skin";
                    BuildWorkshopPublishScreen();
                    break;
                default:
                    _titleTmp.text = "Skin Maker";
                    break;
            }

            for (int i = 0; i < _panelRt.childCount; i++)
            {
                Transform child = _panelRt.GetChild(i);
                if (child.name.StartsWith("MenuResizeHandle_"))
                    child.SetAsLastSibling();
            }
        }

        static RectTransform CreateScroll(Transform parent, Vector2 anchoredPos, Vector2 size, out Transform content)
        {
            var listGo = new GameObject("Scroll");
            listGo.transform.SetParent(parent, false);
            var listRt = listGo.AddComponent<RectTransform>();
            listRt.anchorMin = new Vector2(0.5f, 1f);
            listRt.anchorMax = new Vector2(0.5f, 1f);
            listRt.pivot = new Vector2(0.5f, 1f);
            listRt.anchoredPosition = anchoredPos;
            listRt.sizeDelta = size;
            listGo.AddComponent<Image>().color = new Color(0.04f, 0.04f, 0.04f, 0.35f);
            listGo.AddComponent<RectMask2D>();
            var scroll = listGo.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 22f;

            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(listGo.transform, false);
            var viewportRt = viewport.AddComponent<RectTransform>();
            viewportRt.anchorMin = Vector2.zero;
            viewportRt.anchorMax = Vector2.one;
            viewportRt.offsetMin = Vector2.zero;
            viewportRt.offsetMax = Vector2.zero;
            viewport.AddComponent<RectMask2D>();

            var contentGo = new GameObject("Content");
            contentGo.transform.SetParent(viewport.transform, false);
            var contentRt = contentGo.AddComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0.5f, 1f);
            contentRt.anchorMax = new Vector2(0.5f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = new Vector2(size.x, size.y);

            scroll.viewport = viewportRt;
            scroll.content = contentRt;
            content = contentGo.transform;
            return listRt;
        }

        static void PlaceGrid(Transform content, int count, Action<int, Vector2> place, float cellW = CellW, float cellH = CellH)
        {
            int rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)GridCols));
            float stepX = cellW + CellGap;
            float stepY = cellH + CellGap;
            float gridW = GridCols * cellW + (GridCols - 1) * CellGap;
            float gridH = rows * cellH + (rows - 1) * CellGap;
            content.GetComponent<RectTransform>().sizeDelta = new Vector2(PanelW - 8f, Mathf.Max(gridH + 6f, 40f));
            float startX = -gridW * 0.5f + cellW * 0.5f;
            float startY = -2f - cellH * 0.5f;
            for (int i = 0; i < count; i++)
            {
                int col = i % GridCols;
                int row = i / GridCols;
                place(i, new Vector2(startX + col * stepX, startY - row * stepY));
            }
        }

        static void AnchorTopCenter(RectTransform rt)
        {
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        static void CreateNavBar(Transform parent, string leftLabel, Action left, string rightLabel = null, Action right = null, string midLabel = null, Action mid = null)
        {
            var bar = new GameObject("Nav");
            bar.transform.SetParent(parent, false);
            var rt = bar.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(PanelW - 4f, NavH);
            CreateSmallButton(bar.transform, leftLabel, new Vector2(-(PanelW * 0.5f - 20f), -NavH * 0.5f), left, true);
            if (!string.IsNullOrEmpty(midLabel))
                CreateSmallButton(bar.transform, midLabel, new Vector2(0f, -NavH * 0.5f), mid, true, 28f);
            if (!string.IsNullOrEmpty(rightLabel))
                CreateSmallButton(bar.transform, rightLabel, new Vector2(PanelW * 0.5f - 20f, -NavH * 0.5f), right, true);
        }

        static void CreateSmallButton(Transform parent, string label, Vector2 pos, Action onClick, bool fromTop = false, float width = 32f)
        {
            var go = new GameObject("Btn_" + label);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            if (fromTop) AnchorTopCenter(rt);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(width, 6.2f);
            go.AddComponent<Image>().color = new Color(0.18f, 0.18f, 0.18f, 1f);
            go.AddComponent<Button>().onClick.AddListener(() => onClick?.Invoke());
            var textGo = new GameObject("Label");
            textGo.transform.SetParent(go.transform, false);
            var trt = textGo.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(1f, 0f);
            trt.offsetMax = new Vector2(-1f, 0f);
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.font = Font;
            tmp.fontSize = 2.5f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
        }

        static RectTransform CreatePanel(Transform parent, string name, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            go.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 0.95f);
            return rt;
        }

        static TextMeshProUGUI CreateLabel(Transform parent, string text, Vector2 pos, float size)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(48f, 18f);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.font = Font;
            tmp.fontSize = size;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            return tmp;
        }

        static void CreateGridCell(Transform parent, string name, Sprite icon, Vector2 pos, Action onClick)
        {
            var cell = new GameObject("Cell");
            cell.transform.SetParent(parent, false);
            var rt = cell.AddComponent<RectTransform>();
            AnchorTopCenter(rt);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(CellW, CellH);
            cell.AddComponent<Image>().color = CellBg;
            var btn = cell.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => onClick?.Invoke());

            if (icon != null)
            {
                var iconGo = new GameObject("Icon");
                iconGo.transform.SetParent(cell.transform, false);
                var irt = iconGo.AddComponent<RectTransform>();
                irt.anchoredPosition = new Vector2(0f, 2.2f);
                irt.sizeDelta = new Vector2(CellW - 4f, CellH - 9f);
                var img = iconGo.AddComponent<Image>();
                img.sprite = icon;
                img.preserveAspect = true;
                img.raycastTarget = false;
            }

            var bar = new GameObject("NameBar");
            bar.transform.SetParent(cell.transform, false);
            var brt = bar.AddComponent<RectTransform>();
            brt.anchorMin = new Vector2(0f, 0f);
            brt.anchorMax = new Vector2(1f, 0f);
            brt.pivot = new Vector2(0.5f, 0f);
            brt.anchoredPosition = Vector2.zero;
            brt.sizeDelta = new Vector2(0f, 6.8f);
            bar.AddComponent<Image>().color = NameBar;

            var labelGo = new GameObject("Name");
            labelGo.transform.SetParent(bar.transform, false);
            var lrt = labelGo.AddComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(0.6f, 0.3f);
            lrt.offsetMax = new Vector2(-0.6f, -0.3f);
            var tmp = labelGo.AddComponent<TextMeshProUGUI>();
            tmp.text = name ?? "";
            tmp.font = Font;
            tmp.fontSize = 2.15f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.raycastTarget = false;
        }

        static void CreateTinyAction(Transform parent, string label, Vector2 pos, float width, Action onClick)
        {
            var go = new GameObject("Act_" + label);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(width, 4.4f);
            go.AddComponent<Image>().color = new Color(0.2f, 0.22f, 0.28f, 1f);
            var button = go.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() =>
            {
                try { onClick?.Invoke(); }
                catch (Exception ex) { DialogBoxManager.Notification(label + " failed: " + ex.Message); }
            });
            var textGo = new GameObject("Label");
            textGo.transform.SetParent(go.transform, false);
            var trt = textGo.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.font = Font;
            tmp.fontSize = 1.55f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
        }

        static bool IsCustomSkin(string skinKey, CharacterSkins.Skins.SkinInfo info)
        {
            if (string.IsNullOrEmpty(_characterBasePath) || string.IsNullOrEmpty(skinKey)) return false;
            string folderGuess = info != null && !string.IsNullOrEmpty(info.DisplayName)
                ? SkinPackIO.SanitizeFolderName(info.DisplayName)
                : skinKey;
            if (DBPU.DbpuCustomSkinLoader.IsCustomPackFolder(_characterBasePath, folderGuess)) return true;
            if (DBPU.DbpuCustomSkinLoader.IsCustomPackFolder(_characterBasePath, skinKey)) return true;
            string bareKey = skinKey.StartsWith("Custom_", StringComparison.OrdinalIgnoreCase)
                ? skinKey.Substring("Custom_".Length)
                : skinKey;
            if (!string.IsNullOrEmpty(bareKey)
                && DBPU.DbpuCustomSkinLoader.IsCustomPackFolder(_characterBasePath, bareKey))
                return true;
            string customRoot = DBPU.DbpuCustomSkinLoader.GetCustomSkinsFolderFs(_characterBasePath);
            if (!Directory.Exists(customRoot)) return false;
            foreach (var dir in Directory.GetDirectories(customRoot))
            {
                string name = Path.GetFileName(dir);
                string key = new string(name.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
                if (string.Equals(key, skinKey, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(key, bareKey, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(name, skinKey, StringComparison.OrdinalIgnoreCase)
                    || string.Equals("Custom_" + key, skinKey, StringComparison.OrdinalIgnoreCase)
                    || (info != null && !string.IsNullOrEmpty(info.DisplayName)
                        && string.Equals(name.Replace('_', ' '), info.DisplayName, StringComparison.OrdinalIgnoreCase)))
                    return true;
            }
            return false;
        }

        static string ResolvePackFolderName(string skinKey, CharacterSkins.Skins.SkinInfo info)
        {
            string customRoot = DBPU.DbpuCustomSkinLoader.GetCustomSkinsFolderFs(_characterBasePath);
            if (!Directory.Exists(customRoot)) return skinKey;
            string bareKey = !string.IsNullOrEmpty(skinKey) && skinKey.StartsWith("Custom_", StringComparison.OrdinalIgnoreCase)
                ? skinKey.Substring("Custom_".Length)
                : skinKey;

            if (info != null && !string.IsNullOrEmpty(info.DisplayName))
            {
                string want = SkinPackIO.SanitizeFolderName(info.DisplayName);
                foreach (var dir in Directory.GetDirectories(customRoot))
                {
                    string name = Path.GetFileName(dir);
                    if (string.Equals(name, want, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(name.Replace('_', ' '), info.DisplayName, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(name.Replace(' ', '_'), info.DisplayName.Replace(' ', '_'), StringComparison.OrdinalIgnoreCase))
                        return name;
                }
            }

            string exactKeyMatch = null;
            foreach (var dir in Directory.GetDirectories(customRoot))
            {
                string name = Path.GetFileName(dir);
                string key = new string(name.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
                if (string.Equals(name, skinKey, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(name, bareKey, StringComparison.OrdinalIgnoreCase))
                    return name;
                if (exactKeyMatch == null
                    && (string.Equals(key, skinKey, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(key, bareKey, StringComparison.OrdinalIgnoreCase)
                        || string.Equals("Custom_" + key, skinKey, StringComparison.OrdinalIgnoreCase)))
                    exactKeyMatch = name;
            }
            if (!string.IsNullOrEmpty(exactKeyMatch)) return exactKeyMatch;

            if (info != null && !string.IsNullOrEmpty(info.DisplayName))
                return SkinPackIO.SanitizeFolderName(info.DisplayName);
            return !string.IsNullOrEmpty(bareKey) ? bareKey : skinKey;
        }

        static Sprite GetSkinIcon(CharacterSkins.Skins.SkinInfo info)
        {
            if (info?.Illustrations != null)
            {
                if (info.Illustrations.TryGetValue("Damage0", out var s0) && s0 != null) return s0;
                var first = info.Illustrations.Values.FirstOrDefault(v => v != null);
                if (first != null) return first;
            }
            return null;
        }

        static void BuildSkinsGrid()
        {
            SetWorldClickMode(false);
            float bodyH = PanelH - TitleH - 3f;
            CreateNavBar(_body, "Back", () =>
            {
                _targetPerson = null;
                _targetSkins = null;
                _skinMenu = null;
                _characterBasePath = null;
                _displayName = null;
                _screen = ScreenKind.PickPerson;
                CloseOverlay(false);
                SetWorldClickMode(true);
                if (_root != null) _root.SetActive(false);
                ModAPI.Notify("Click a person to open Skin Maker");
            }, "New", BeginNewSkin, "Import", ImportSharedSkin);

            _skinGridKeys.Clear();
            _skinGridCells.Clear();
            _skinGridInfos.Clear();
            _skinDragActive = null;

            var byKey = new Dictionary<string, CharacterSkins.Skins.SkinInfo>(StringComparer.OrdinalIgnoreCase);
            if (_targetSkins?.skins?.SkinData != null)
            {
                var seenPacks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var kvp in _targetSkins.skins.SkinData)
                {
                    if (string.IsNullOrEmpty(kvp.Key)) continue;
                    string packId = null;
                    if (IsCustomSkin(kvp.Key, kvp.Value))
                        packId = "pack:" + ResolvePackFolderName(kvp.Key, kvp.Value);
                    else if (kvp.Value != null && !string.IsNullOrEmpty(kvp.Value.DisplayName))
                        packId = "name:" + kvp.Value.DisplayName;
                    else
                        packId = "key:" + kvp.Key;
                    if (!seenPacks.Add(packId)) continue;
                    byKey[kvp.Key] = kvp.Value;
                    _skinGridInfos[kvp.Key] = kvp.Value;
                }
            }

            var order = SkinPackIO.GetSkinOrder(_characterBasePath, byKey.Keys);
            var entries = new List<(string Key, CharacterSkins.Skins.SkinInfo Info)>();
            var placed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < order.Count; i++)
            {
                string key = order[i];
                if (!byKey.TryGetValue(key, out var info) || !placed.Add(key)) continue;
                entries.Add((key, info));
            }
            foreach (var kvp in byKey)
            {
                if (!placed.Add(kvp.Key)) continue;
                entries.Add((kvp.Key, kvp.Value));
            }

            var scrollRt = CreateScroll(_body, new Vector2(0f, -NavH), new Vector2(PanelW - 6f, bodyH - NavH - 2f), out Transform content);
            _skinGridContent = content;
            _skinGridScroll = scrollRt.GetComponent<ScrollRect>();
            int count = entries.Count;
            PlaceGrid(content, count, (i, pos) =>
            {
                var e = entries[i];
                string name = e.Info?.DisplayName ?? e.Key;
                bool custom = IsCustomSkin(e.Key, e.Info);
                var cell = new GameObject("SkinCell");
                cell.transform.SetParent(content, false);
                var rt = cell.AddComponent<RectTransform>();
                AnchorTopCenter(rt);
                rt.anchoredPosition = pos;
                rt.sizeDelta = new Vector2(CellW, CellH);
                cell.AddComponent<Image>().color = CellBg;

                if (GetSkinIcon(e.Info) != null)
                {
                    var iconGo = new GameObject("Icon");
                    iconGo.transform.SetParent(cell.transform, false);
                    var irt = iconGo.AddComponent<RectTransform>();
                    irt.anchorMin = new Vector2(0.5f, 1f);
                    irt.anchorMax = new Vector2(0.5f, 1f);
                    irt.pivot = new Vector2(0.5f, 1f);
                    irt.anchoredPosition = new Vector2(0f, -1.2f);
                    irt.sizeDelta = new Vector2(CellW - 3f, CellH - 16f);
                    var img = iconGo.AddComponent<Image>();
                    img.sprite = GetSkinIcon(e.Info);
                    img.preserveAspect = true;
                    img.raycastTarget = false;
                }

                var bar = new GameObject("NameBar");
                bar.transform.SetParent(cell.transform, false);
                var brt = bar.AddComponent<RectTransform>();
                brt.anchorMin = new Vector2(0f, 0f);
                brt.anchorMax = new Vector2(1f, 0f);
                brt.pivot = new Vector2(0.5f, 0f);
                brt.anchoredPosition = Vector2.zero;
                brt.sizeDelta = new Vector2(0f, 4.6f);
                bar.AddComponent<Image>().color = NameBar;
                var labelGo = new GameObject("Name");
                labelGo.transform.SetParent(bar.transform, false);
                var lrt = labelGo.AddComponent<RectTransform>();
                lrt.anchorMin = Vector2.zero;
                lrt.anchorMax = Vector2.one;
                lrt.offsetMin = Vector2.zero;
                lrt.offsetMax = Vector2.zero;
                var tmp = labelGo.AddComponent<TextMeshProUGUI>();
                tmp.text = name;
                tmp.font = Font;
                tmp.fontSize = 1.7f;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.white;
                tmp.enableWordWrapping = false;
                tmp.overflowMode = TextOverflowModes.Ellipsis;
                tmp.raycastTarget = false;

                if (custom)
                {
                    string packFolder = ResolvePackFolderName(e.Key, e.Info);
                    CreateTinyAction(cell.transform, "Edit", new Vector2(-7.4f, 7.2f), 7.2f, () => OpenEditorForPack(packFolder));
                    CreateTinyAction(cell.transform, "Share", new Vector2(0f, 7.2f), 7.8f, () => ConfirmShareSkin(packFolder, name));
                    CreateTinyAction(cell.transform, "Del", new Vector2(7.4f, 7.2f), 6.6f, () => ConfirmDeleteSkin(e.Key, packFolder));
                }
                else
                {
                    string skinKey = e.Key;
                    string skinName = name;
                    CreateTinyAction(cell.transform, "Clone", new Vector2(0f, 7.2f), 12f, () =>
                    {
                        try
                        {
                            string pack = SkinPackIO.CloneBuiltinToCustom(_characterBasePath, skinKey, skinName, _targetSkins);
                            OpenEditorForPack(pack);
                            ShowNotify("Cloned \"" + skinName + "\" into Custom skins.");
                        }
                        catch (Exception ex)
                        {
                            ShowNotify("Clone failed: " + ex.Message);
                        }
                    });
                }

                var drag = cell.AddComponent<SmSkinCellDrag>();
                drag.SkinKey = e.Key;
                drag.Index = i;
                _skinGridKeys.Add(e.Key);
                _skinGridCells.Add(rt);
            }, CellW, CellH);
        }

        static Vector2 SkinGridSlotPos(int index)
        {
            float stepX = CellW + CellGap;
            float stepY = CellH + CellGap;
            float gridW = GridCols * CellW + (GridCols - 1) * CellGap;
            float startX = -gridW * 0.5f + CellW * 0.5f;
            float startY = -2f - CellH * 0.5f;
            int col = index % GridCols;
            int row = index / GridCols;
            return new Vector2(startX + col * stepX, startY - row * stepY);
        }

        static int SkinGridIndexFromLocal(Vector2 local)
        {
            float stepX = CellW + CellGap;
            float stepY = CellH + CellGap;
            float gridW = GridCols * CellW + (GridCols - 1) * CellGap;
            float startX = -gridW * 0.5f + CellW * 0.5f;
            float startY = -2f - CellH * 0.5f;
            int col = Mathf.RoundToInt((local.x - startX) / stepX);
            int row = Mathf.RoundToInt((startY - local.y) / stepY);
            col = Mathf.Clamp(col, 0, GridCols - 1);
            row = Mathf.Max(0, row);
            int index = row * GridCols + col;
            return Mathf.Clamp(index, 0, _skinGridKeys.Count - 1);
        }

        public static void BeginSkinCellDrag(SmSkinCellDrag drag)
        {
            _skinDragActive = drag;
            if (_skinGridScroll != null)
                _skinGridScroll.enabled = false;
        }

        public static void DragSkinCell(SmSkinCellDrag drag, PointerEventData eventData)
        {
            if (drag == null || _skinGridContent == null) return;
            var contentRt = _skinGridContent as RectTransform;
            var cellRt = drag.transform as RectTransform;
            if (contentRt == null || cellRt == null) return;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(contentRt, eventData.position, eventData.pressEventCamera, out var local))
                cellRt.anchoredPosition = local;

            int from = drag.Index;
            int to = SkinGridIndexFromLocal(cellRt.anchoredPosition);
            if (to == from || to < 0 || to >= _skinGridKeys.Count) return;

            string key = _skinGridKeys[from];
            var moved = _skinGridCells[from];
            _skinGridKeys.RemoveAt(from);
            _skinGridCells.RemoveAt(from);
            _skinGridKeys.Insert(to, key);
            _skinGridCells.Insert(to, moved);
            drag.Index = to;

            for (int i = 0; i < _skinGridCells.Count; i++)
            {
                var rt = _skinGridCells[i];
                if (rt == null) continue;
                var d = rt.GetComponent<SmSkinCellDrag>();
                if (d != null) d.Index = i;
                if (d == drag) continue;
                rt.anchoredPosition = SkinGridSlotPos(i);
            }
        }

        public static void EndSkinCellDrag(SmSkinCellDrag drag, bool moved)
        {
            if (_skinGridScroll != null)
                _skinGridScroll.enabled = true;
            _skinDragActive = null;
            if (drag == null) return;

            var cellRt = drag.transform as RectTransform;
            if (cellRt != null)
                cellRt.anchoredPosition = SkinGridSlotPos(drag.Index);

            if (!moved || string.IsNullOrEmpty(_characterBasePath)) return;
            SkinPackIO.SetSkinOrder(_characterBasePath, _skinGridKeys);
        }

        public static void SelectSkinFromGrid(string skinKey)
        {
            if (string.IsNullOrEmpty(skinKey)) return;
            _skinGridInfos.TryGetValue(skinKey, out var info);
            string name = info?.DisplayName ?? skinKey;
            if (IsCustomSkin(skinKey, info))
            {
                OpenEditorForPack(ResolvePackFolderName(skinKey, info));
                return;
            }

            try
            {
                string pack = SkinPackIO.CloneBuiltinToCustom(_characterBasePath, skinKey, name, _targetSkins);
                OpenEditorForPack(pack);
                ShowNotify("Cloned \"" + name + "\" into Custom skins.");
            }
            catch (Exception ex)
            {
                ShowNotify("Open failed: " + ex.Message);
            }
        }

        static void BeginNewSkin()
        {
            PromptString("New Skin", v =>
            {
                if (string.IsNullOrWhiteSpace(v)) return;
                try
                {
                    string seedKey = _skinMenu != null ? _skinMenu.CurrentSkin : null;
                    string seedName = null;
                    if (!string.IsNullOrEmpty(seedKey)
                        && _targetSkins?.skins?.SkinData != null
                        && _targetSkins.skins.SkinData.TryGetValue(seedKey, out var info)
                        && info != null)
                        seedName = info.DisplayName;

                    string pack = SkinPackIO.CloneBuiltinToCustom(
                        _characterBasePath,
                        seedKey,
                        seedName,
                        _targetSkins,
                        v);
                    if (string.IsNullOrEmpty(pack))
                        pack = SkinPackIO.CreateEmptyPack(_characterBasePath, v, seedKey, seedName, _targetSkins);
                    OpenEditorForPack(pack);
                }
                catch (Exception ex)
                {
                    ShowNotify("Create failed: " + ex.Message);
                }
            });
        }

        static void OpenEditorForPack(string packFolderName)
        {
            _draft = SkinPackIO.LoadDraftFromPack(_characterBasePath, packFolderName);
            _draft.PackFolderName = packFolderName;
            if (string.IsNullOrEmpty(_draft.DisplayName))
                _draft.DisplayName = packFolderName.Replace('_', ' ');
            _previewSelectedLimb = _draft.SelectedLimb;
            _illustrationDamage = _draft.ActiveDamage;
            SyncSelectedHairFromDraft();
            EnsureDraftBeltsFromExisting();
            RegisterDraftInSkinMenu(true);
            _screen = ScreenKind.Editor;
            RebuildBody();
        }

        static string RegisterDraftInSkinMenu(bool applyLive)
        {
            if (_draft == null || string.IsNullOrEmpty(_characterBasePath)) return null;
            try
            {
                if (_targetPerson != null)
                {
                    EnsurePersonSkinSupport(_targetPerson);
                    var root = _targetPerson.transform.root.gameObject;
                    var identity = root.GetComponent<DbpuCharacterIdentity>();
                    string resolved = SkinPackIO.ResolveCharacterBasePath(_targetPerson);
                    if (!string.IsNullOrEmpty(resolved))
                        _characterBasePath = resolved;
                    if (!_characterBasePath.EndsWith("/") && !_characterBasePath.EndsWith("\\"))
                        _characterBasePath = _characterBasePath.Replace('\\', '/') + "/";
                    else
                        _characterBasePath = _characterBasePath.Replace('\\', '/');
                    if (identity != null)
                        identity.SpriteFolder = _characterBasePath;
                    _targetSkins = root.GetComponent<CharacterSkins>();
                    var headSupport = FindHead(_targetPerson);
                    if (headSupport != null)
                    {
                        _skinMenu = headSupport.GetComponent<SkinContextMenu>();
                        if (_skinMenu == null) _skinMenu = headSupport.gameObject.AddComponent<SkinContextMenu>();
                        _skinMenu.SkinsData = _targetSkins;
                        _skinMenu.person = _targetPerson;
                    }
                }

                SkinPackIO.SaveDraft(_characterBasePath, _draft);

                string packFs = DBPU.DbpuCustomSkinLoader.GetPackFolderFs(_characterBasePath, _draft.PackFolderName);
                if (!Directory.Exists(packFs))
                    Directory.CreateDirectory(packFs);
                string upperPng = Path.Combine(packFs, "Upper.png");
                if (!File.Exists(upperPng))
                    SkinPackIO.SaveDraft(_characterBasePath, _draft);

                DBPU.DbpuCustomSkinLoader.InvalidateScanCache(_characterBasePath);

                if (_targetSkins == null && _targetPerson != null)
                    _targetSkins = _targetPerson.transform.root.GetComponent<CharacterSkins>();
                if (_targetSkins == null) return null;
                if (_targetSkins.skins == null)
                    _targetSkins.skins = new CharacterSkins.Skins();

                string key = DBPU.DbpuCustomSkinLoader.UpsertPackIntoCharacter(
                    _targetSkins, _characterBasePath, _draft.PackFolderName, "Head.png", true, _draft.DisplayName);
                if (string.IsNullOrEmpty(key) && Directory.Exists(packFs) && File.Exists(Path.Combine(packFs, "Upper.png")))
                {
                    DBPU.DbpuCustomSkinLoader.InvalidateScanCache(_characterBasePath);
                    key = DBPU.DbpuCustomSkinLoader.UpsertPackIntoCharacter(
                        _targetSkins, _characterBasePath, _draft.PackFolderName, "Head.png", true, _draft.DisplayName);
                }
                if (string.IsNullOrEmpty(key)) return null;
                _draft.SkinKey = key;

                var head = FindHead(_targetPerson);
                if (head != null)
                {
                    _skinMenu = head.GetComponent<SkinContextMenu>();
                    if (_skinMenu == null) _skinMenu = head.gameObject.AddComponent<SkinContextMenu>();
                    _skinMenu.SkinsData = _targetSkins;
                    _skinMenu.person = _targetPerson;
                    _skinMenu.CurrentSkin = key;
                    _skinMenu.Skin_Suffix = _draft.ActiveSuffix ?? "";
                    _skinMenu.CurrentDamage = _draft.ActiveDamage;
                    _skinMenu.JumpToSkin(key);
                }

                if (applyLive && _targetPerson != null)
                {
                    try
                    {
                        _targetSkins.ApplySkin(_targetPerson, key, _draft.ActiveSuffix ?? "", _draft.ActiveDamage);
                        ApplyDraftHeadToPerson();
                        ApplyDraftHairToPerson();
                        DbpuMakerPhysics.CalmPerson(_targetPerson);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("[DBPU] ApplySkin after register failed: " + ex.Message);
                    }
                }
                return key;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[DBPU] RegisterDraftInSkinMenu failed: " + ex.Message);
                return null;
            }
        }

        public static void IngestSpriteEditorApply(PersonBehaviour person, System.Collections.Generic.IList<LiveSpriteEditor.CharacterSpriteEditorTarget.MakerSpriteExport> exports)
        {
            if (!IsOpen || _draft == null || person == null) return;
            if (_targetPerson != null
                && person.transform.root != _targetPerson.transform.root)
                return;
            if (exports == null || exports.Count == 0) return;
            if (string.IsNullOrEmpty(_characterBasePath) || string.IsNullOrEmpty(_draft.PackFolderName)) return;

            string packFs = DBPU.DbpuCustomSkinLoader.GetPackFolderFs(_characterBasePath, _draft.PackFolderName);
            string suffixRoot = string.IsNullOrEmpty(_draft.ActiveSuffix) ? packFs : Path.Combine(packFs, _draft.ActiveSuffix);
            string destDir = _draft.ActiveDamage <= 0 ? suffixRoot : Path.Combine(suffixRoot, "Damage" + _draft.ActiveDamage);
            if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);

            bool any = false;
            for (int i = 0; i < exports.Count; i++)
            {
                var exp = exports[i];
                if (exp == null || exp.Png == null || exp.Png.Length == 0) continue;
                if (exp.IsHair)
                {
                    string hairDest = Path.Combine(destDir, "Hair.png");
                    try
                    {
                        CustomTransformationCatalog.WriteAllBytesShared(hairDest, exp.Png);
                        string hairKey = SkinPackDraft.LimbKey(_draft.ActiveSuffix, _draft.ActiveDamage, "Hair");
                        _draft.LimbPaths[hairKey] = hairDest;
                        _selectedHairPath = hairDest;
                        if (_targetPerson != null)
                        {
                            var head = FindHead(_targetPerson);
                            if (head != null && exp.Sprite != null)
                                TmHairApply.ApplyHair(head, exp.Sprite, Color.white, Vector3.zero, false);
                        }
                        any = true;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("[DBPU] Skin Maker ingest hair failed: " + ex.Message);
                    }
                    continue;
                }

                string limb = MapLimbName(exp.LimbName);
                if (string.IsNullOrEmpty(limb) || string.Equals(limb, "Hair", StringComparison.OrdinalIgnoreCase))
                    continue;
                string dest = Path.Combine(destDir, limb + ".png");
                try
                {
                    CustomTransformationCatalog.WriteAllBytesShared(dest, exp.Png);
                    string key = SkinPackDraft.LimbKey(_draft.ActiveSuffix, _draft.ActiveDamage, limb);
                    _draft.LimbPaths[key] = dest;
                    if (string.Equals(limb, "Upper", StringComparison.OrdinalIgnoreCase)
                        && string.IsNullOrEmpty(_draft.ActiveSuffix)
                        && _draft.ActiveDamage <= 0)
                    {
                        string rootUpper = Path.Combine(packFs, "Upper.png");
                        if (!CustomTransformationCatalog.PathsEqual(dest, rootUpper))
                            CustomTransformationCatalog.CopyFileShared(dest, rootUpper, true);
                    }
                    any = true;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[DBPU] Skin Maker ingest limb failed: " + ex.Message);
                }
            }

            if (!any) return;
            RegisterDraftInSkinMenu(true);
            if (IsOpen) RebuildBody();
            ModAPI.Notify("Sprite edits written to skin draft.");
        }

        static void ConfirmDeleteSkin(string skinKey, string packFolderName)
        {
            string name = packFolderName ?? skinKey;
            ShowConfirmDialog(
                "Delete custom skin \"" + name + "\"?",
                "Delete",
                () => DeleteCustomSkin(skinKey, packFolderName));
        }

        static void DeleteCustomSkin(string skinKey, string packFolderName)
        {
            try
            {
                if (string.IsNullOrEmpty(packFolderName) && !string.IsNullOrEmpty(skinKey))
                {
                    _skinGridInfos.TryGetValue(skinKey, out var info);
                    packFolderName = ResolvePackFolderName(skinKey, info);
                }
                string packFs = !string.IsNullOrEmpty(packFolderName)
                    ? DBPU.DbpuCustomSkinLoader.GetPackFolderFs(_characterBasePath, packFolderName)
                    : null;
                if (!string.IsNullOrEmpty(packFs) && Directory.Exists(packFs))
                    Directory.Delete(packFs, true);
                if (_targetSkins != null)
                    DBPU.DbpuCustomSkinLoader.RemovePackFromCharacter(_targetSkins, packFolderName, skinKey);
                DBPU.DbpuCustomSkinLoader.InvalidateScanCache(_characterBasePath);
                if (_skinMenu != null
                    && !string.IsNullOrEmpty(_skinMenu.CurrentSkin)
                    && (string.Equals(_skinMenu.CurrentSkin, skinKey, StringComparison.OrdinalIgnoreCase)
                        || (!string.IsNullOrEmpty(packFolderName)
                            && string.Equals(
                                new string(packFolderName.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray()),
                                _skinMenu.CurrentSkin,
                                StringComparison.OrdinalIgnoreCase))))
                {
                    _skinMenu.CurrentSkin = "Base";
                    _skinMenu.PreviousSkin = "Base";
                }
                RebuildBody();
                ModAPI.Notify("Deleted \"" + (string.IsNullOrEmpty(packFolderName) ? "skin" : packFolderName) + "\".");
            }
            catch (Exception ex)
            {
                ModAPI.Notify("Delete failed: " + ex.Message);
            }
        }

        static void BuildEditor()
        {
            if (_draft == null) return;
            SyncSelectedHairFromDraft();
            SetWorldClickMode(false);
            float bodyH = PanelH - TitleH - 3f;
            CreateNavBar(_body, "Back", () =>
            {
                try
                {
                    if (_draft != null && !string.IsNullOrEmpty(_characterBasePath))
                        SkinPackIO.SaveDraft(_characterBasePath, _draft);
                }
                catch { }
                _screen = ScreenKind.Skins;
                _draft = null;
                RebuildBody();
            }, "Save", () =>
            {
                try
                {
                    if (_draft == null) return;
                    string key = RegisterDraftInSkinMenu(true);
                    if (string.IsNullOrEmpty(key))
                        ShowNotify("Saved pack, but could not register it in the skin menu yet.");
                    else
                        ShowNotify("Saved skin \"" + (_draft.DisplayName ?? _draft.PackFolderName) + "\".");
                }
                catch (Exception ex)
                {
                    ShowNotify("Save failed: " + ex.Message);
                }
            });

            float panelTop = -NavH - 0.5f;
            float panelH = bodyH - NavH - 1.5f;
            var left = CreatePanel(_body, "Left", new Vector2(-(PanelW * 0.5f - 28f), panelTop - panelH * 0.5f), new Vector2(50f, panelH));
            BuildEditorProps(left);
            var center = CreatePanel(_body, "Center", new Vector2(0f, panelTop - panelH * 0.5f), new Vector2(58f, panelH));
            BuildCenterPreview(center);
            var right = CreatePanel(_body, "Right", new Vector2(PanelW * 0.5f - 28f, panelTop - panelH * 0.5f), new Vector2(50f, panelH));
            BuildLimbSpritePanel(right);
        }

        static void BuildEditorProps(RectTransform left)
        {
            float scrollH = left.sizeDelta.y - 4f;
            CreateScroll(left, new Vector2(0f, -2f), new Vector2(left.sizeDelta.x - 2f, scrollH), out Transform content);
            float y = -3.2f;
            void Add(string label, Action act)
            {
                var go = new GameObject("Prop");
                go.transform.SetParent(content, false);
                var rt = go.AddComponent<RectTransform>();
                AnchorTopCenter(rt);
                rt.anchoredPosition = new Vector2(0f, y);
                rt.sizeDelta = new Vector2(44f, 5.4f);
                go.AddComponent<Image>().color = new Color(0.16f, 0.16f, 0.16f, 1f);
                go.AddComponent<Button>().onClick.AddListener(() => act?.Invoke());
                var textGo = new GameObject("T");
                textGo.transform.SetParent(go.transform, false);
                var trt = textGo.AddComponent<RectTransform>();
                trt.anchorMin = Vector2.zero;
                trt.anchorMax = Vector2.one;
                trt.offsetMin = new Vector2(1.2f, 0f);
                trt.offsetMax = new Vector2(-1.2f, 0f);
                var tmp = textGo.AddComponent<TextMeshProUGUI>();
                tmp.text = label;
                tmp.font = Font;
                tmp.fontSize = 2.15f;
                tmp.alignment = TextAlignmentOptions.Left;
                tmp.color = Color.white;
                tmp.enableWordWrapping = false;
                tmp.overflowMode = TextOverflowModes.Ellipsis;
                tmp.raycastTarget = false;
                y -= 6f;
            }

            Add("Name: " + (_draft.DisplayName ?? ""), () => PromptString(_draft.DisplayName, v =>
            {
                _draft.DisplayName = v;
                RebuildBody();
            }));
            string dmgLabel = "Damage" + _draft.ActiveDamage;
            Add("Damage: " + dmgLabel, () => { _screen = ScreenKind.Damage; RebuildBody(); });
            string suffixLabel = string.IsNullOrEmpty(_draft.ActiveSuffix) ? "Base" : _draft.ActiveSuffix.TrimStart('_');
            Add("Variant: " + suffixLabel, () => { _screen = ScreenKind.Buffed; RebuildBody(); });
            Add("Limb: " + (_draft.SelectedLimb ?? "Upper"), () => { });
            string illKey = "Damage" + _draft.ActiveDamage;
            bool illSet = _draft.IllustrationPaths != null && _draft.IllustrationPaths.ContainsKey(illKey);
            Add(illSet ? "Illustration: set" : "Illustration: (empty)", () =>
            {
                _illustrationDamage = _draft.ActiveDamage;
                _screen = ScreenKind.Illustrations;
                RebuildBody();
            });
            Add("Belts", () => { _screen = ScreenKind.Belts; RebuildBody(); });
            Add("Sprite Editor", () =>
            {
                string limb = _draft?.SelectedLimb;
                if (string.Equals(limb, "Hair", StringComparison.OrdinalIgnoreCase))
                    limb = "Head";
                if (!DbpuSpriteEditorBridge.TryOpenForLimbName(_targetPerson, limb))
                    DialogBoxManager.Notification("Could not open Sprite Editor.");
            });
            Add("Apply", () => ApplyDraftLive());
            content.GetComponent<RectTransform>().sizeDelta = new Vector2(44f, Mathf.Max(40f, Mathf.Abs(y) + 8f));
        }

        static int MaxDamageLevel()
        {
            int max = 0;
            if (_draft?.LimbPaths == null) return 0;
            foreach (var key in _draft.LimbPaths.Keys)
            {
                string[] p = key.Split('|');
                if (p.Length >= 2 && int.TryParse(p[1], out int d) && d > max) max = d;
            }
            string packFs = DBPU.DbpuCustomSkinLoader.GetPackFolderFs(_characterBasePath, _draft.PackFolderName);
            string root = string.IsNullOrEmpty(_draft.ActiveSuffix) ? packFs : Path.Combine(packFs, _draft.ActiveSuffix);
            if (Directory.Exists(root))
            {
                foreach (var dir in Directory.GetDirectories(root))
                {
                    string name = Path.GetFileName(dir);
                    if (Regex.IsMatch(name, @"^Damage\d+$", RegexOptions.IgnoreCase)
                        && int.TryParse(name.Substring("Damage".Length), out int d) && d > max)
                        max = d;
                }
            }
            return max;
        }

        static void BuildCenterPreview(RectTransform center)
        {
            var title = CreateLabel(center, "Live Preview", new Vector2(0f, center.sizeDelta.y * 0.5f - 4f), 2.6f);
            title.color = Accent;
            BuildPersonPreview(center, new Vector2(0f, 2f), new Vector2(52f, center.sizeDelta.y - 28f));
            CreateSmallButton(center, "Apply Now", new Vector2(0f, -center.sizeDelta.y * 0.5f + 14f), () =>
            {
                if (_targetPerson == null)
                {
                    HideForWorldPick();
                    return;
                }
                ApplyDraftLive();
                RebuildBody();
            });
            CreateSmallButton(center, "Pick Person", new Vector2(0f, -center.sizeDelta.y * 0.5f + 7f), HideForWorldPick);
        }

        static void BuildPersonPreview(RectTransform parent, Vector2 pos, Vector2 size)
        {
            var holder = new GameObject("PersonPreview");
            holder.transform.SetParent(parent, false);
            var holderRt = holder.AddComponent<RectTransform>();
            holderRt.anchoredPosition = pos;
            holderRt.sizeDelta = size;
            holder.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.05f, 0.55f);
            holder.AddComponent<RectMask2D>();

            if (_targetPerson == null)
            {
                CreateLabel(holder.transform, "No person", Vector2.zero, 2.4f);
                return;
            }

            var root = _targetPerson.transform.root;
            var head = FindHead(_targetPerson);
            if (head == null)
                head = root.Find("Head");
            if (head == null)
            {
                CreateLabel(holder.transform, _displayName ?? "Person", Vector2.zero, 2.4f);
                return;
            }

            Sprite draftHair = null;
            if (!string.IsNullOrEmpty(_selectedHairPath))
                draftHair = CustomTransformationCatalog.LoadSpriteFlexible(_selectedHairPath);

            Transform hairTf = null;
            if (draftHair != null)
            {
                hairTf = TmHairApply.EnsureSingleHair(head);
                if (hairTf != null)
                {
                    var hairSrLive = hairTf.GetComponent<SpriteRenderer>();
                    if (hairSrLive == null) hairSrLive = hairTf.gameObject.AddComponent<SpriteRenderer>();
                    hairSrLive.sprite = draftHair;
                    hairSrLive.enabled = true;
                }
            }
            else
            {
                for (int i = 0; i < head.childCount; i++)
                {
                    var child = head.GetChild(i);
                    if (child != null && string.Equals(child.name, "Hair", StringComparison.OrdinalIgnoreCase))
                    {
                        hairTf = child;
                        break;
                    }
                }
            }

            Vector3 origin = head.position;
            float uiPerWorld = 18f;
            float flip = root.localScale.x < 0f ? -1f : 1f;
            var layers = new List<(SpriteRenderer Sr, int Order, string Limb)>();
            var seen = new HashSet<SpriteRenderer>();

            void Consider(SpriteRenderer sr, string limbHint)
            {
                if (sr == null || !sr.enabled || seen.Contains(sr)) return;
                string n = sr.gameObject.name;
                if (n.IndexOf("Particle", StringComparison.OrdinalIgnoreCase) >= 0) return;
                if (n.IndexOf("Glow", StringComparison.OrdinalIgnoreCase) >= 0) return;
                if (n.IndexOf("Belt", StringComparison.OrdinalIgnoreCase) >= 0) return;
                if (n.IndexOf("Aura", StringComparison.OrdinalIgnoreCase) >= 0) return;
                if (sr.sprite == null && !string.Equals(limbHint, "Hair", StringComparison.OrdinalIgnoreCase))
                {
                    if (OverridePreviewSpriteForLimb(limbHint) == null)
                        return;
                }
                seen.Add(sr);
                layers.Add((sr, sr.sortingOrder, limbHint));
            }

            if (_targetPerson.Limbs != null)
            {
                for (int i = 0; i < _targetPerson.Limbs.Length; i++)
                {
                    var limb = _targetPerson.Limbs[i];
                    if (limb == null) continue;
                    var sr = limb.GetComponent<SpriteRenderer>();
                    Consider(sr, MapLimbName(limb.name));
                }
            }

            if (hairTf != null)
            {
                var hairSr = hairTf.GetComponent<SpriteRenderer>();
                Consider(hairSr, "Hair");
            }

            var liveAura = root.GetComponent<AuraHandler>();
            GameObject auraObj = liveAura != null ? liveAura.CurrentAuraObject : null;

            layers = layers.OrderBy(l => l.Order).ToList();
            var layerRts = new List<RectTransform>();
            foreach (var layer in layers)
            {
                var sr = layer.Sr;
                if (auraObj != null && (sr.transform == auraObj.transform || sr.transform.IsChildOf(auraObj.transform)))
                    continue;

                Sprite spr = sr.sprite;
                Color col = sr.color;
                string limbKey = layer.Limb;
                bool isHair = string.Equals(limbKey, "Hair", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(sr.gameObject.name, "Hair", StringComparison.OrdinalIgnoreCase);

                if (isHair && draftHair != null)
                    spr = draftHair;
                else if (!isHair)
                {
                    Sprite over = OverridePreviewSpriteForLimb(limbKey);
                    if (over != null) spr = over;
                }

                if (spr == null) continue;

                var go = new GameObject(sr.transform.name);
                go.transform.SetParent(holder.transform, false);
                var rt = go.AddComponent<RectTransform>();
                Vector3 delta = sr.transform.position - origin;
                float w = spr.rect.width / Mathf.Max(1f, spr.pixelsPerUnit) * uiPerWorld;
                float h = spr.rect.height / Mathf.Max(1f, spr.pixelsPerUnit) * uiPerWorld;
                rt.anchoredPosition = new Vector2(delta.x * uiPerWorld * flip, delta.y * uiPerWorld);
                rt.sizeDelta = new Vector2(w, h);
                rt.localScale = new Vector3(flip, 1f, 1f);
                rt.localRotation = Quaternion.Euler(0f, 0f, (sr.transform.eulerAngles.z - head.eulerAngles.z) * flip);
                var img = go.AddComponent<Image>();
                img.sprite = spr;
                img.color = col;
                img.preserveAspect = true;
                img.raycastTarget = true;
                layerRts.Add(rt);
                var click = go.AddComponent<SmLimbPreviewClick>();
                click.LimbName = isHair ? "Hair" : (string.IsNullOrEmpty(limbKey) ? sr.transform.name : limbKey);
            }

            _previewFitScale = CenterPreviewLayers(layerRts, size);
            string hintLimb = _previewSelectedLimb ?? _draft?.SelectedLimb ?? "limb";
            var hint = CreateLabel(holder.transform, "Click limb: " + hintLimb, new Vector2(0f, -size.y * 0.5f + 4f), 1.9f);
            hint.color = new Color(0.75f, 0.75f, 0.75f, 1f);
        }

        static Sprite OverridePreviewSpriteForLimb(string limbKey)
        {
            if (_draft == null || string.IsNullOrEmpty(limbKey)) return null;
            if (string.Equals(limbKey, "Hair", StringComparison.OrdinalIgnoreCase)) return null;
            string key = SkinPackDraft.LimbKey(_draft.ActiveSuffix, _draft.ActiveDamage, limbKey);
            if (_draft.LimbPaths.TryGetValue(key, out var path) && !string.IsNullOrEmpty(path))
            {
                if (SkinPackIO.IsPlaceholderLimbPng(path)) return null;
                return CustomTransformationCatalog.LoadSpriteFlexible(path);
            }
            return null;
        }

        static Sprite OverridePreviewSprite(SpriteRenderer sr)
        {
            if (sr == null) return null;
            return OverridePreviewSpriteForLimb(MapLimbName(sr.transform.name));
        }

        static float CenterPreviewLayers(List<RectTransform> layers, Vector2 boxSize)
        {
            if (layers == null || layers.Count == 0) return 1f;
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < layers.Count; i++)
            {
                var rt = layers[i];
                if (rt == null) continue;
                Vector2 p = rt.anchoredPosition;
                Vector2 half = rt.sizeDelta * 0.5f;
                minX = Mathf.Min(minX, p.x - half.x);
                maxX = Mathf.Max(maxX, p.x + half.x);
                minY = Mathf.Min(minY, p.y - half.y);
                maxY = Mathf.Max(maxY, p.y + half.y);
            }
            if (minX > maxX || minY > maxY) return 1f;
            Vector2 mid = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
            float bw = Mathf.Max(1f, maxX - minX);
            float bh = Mathf.Max(1f, maxY - minY);
            float fit = Mathf.Min((boxSize.x - 4f) / bw, (boxSize.y - 4f) / bh, 1.35f);
            for (int i = 0; i < layers.Count; i++)
            {
                var rt = layers[i];
                if (rt == null) continue;
                rt.anchoredPosition = (rt.anchoredPosition - mid) * fit;
                rt.sizeDelta *= fit;
            }
            return fit;
        }

        static void BuildLimbSpritePanel(RectTransform right)
        {
            string selected = _draft.SelectedLimb ?? "Upper";
            if (Array.IndexOf(SpriteCategories, selected) < 0)
                selected = "Upper";
            _draft.SelectedLimb = selected;

            float tabY = right.sizeDelta.y * 0.5f - 3.5f;
            for (int i = 0; i < SpriteCategories.Length; i++)
            {
                int idx = i;
                string cat = SpriteCategories[i];
                string label = cat.Length > 4 ? cat.Substring(0, 4) : cat;
                int row = i / 5;
                int col = i % 5;
                var btn = CreateSmallButtonColored(
                    right,
                    label,
                    new Vector2(-18f + col * 9f, tabY - row * 6.2f),
                    () =>
                    {
                        _draft.SelectedLimb = SpriteCategories[idx];
                        _spriteTab = idx;
                        RebuildBody();
                    },
                    string.Equals(cat, selected, StringComparison.OrdinalIgnoreCase)
                        ? Accent
                        : new Color(0.18f, 0.18f, 0.18f, 1f),
                    8.5f);
            }

            CreateSearchField(right, new Vector2(0f, tabY - 14.5f), new Vector2(46f, 5.2f), _spriteSearch ?? "", "Search sprites...", v =>
            {
                _spriteSearch = v;
                RefillLimbSpriteGrid();
            });

            RefreshLimbCandidates();
            float scrollTop = -24f;
            float scrollH = Mathf.Max(18f, right.sizeDelta.y + scrollTop - 14f);
            CreateScroll(right, new Vector2(0f, scrollTop), new Vector2(right.sizeDelta.x - 2f, scrollH), out Transform content);
            _spriteGridContent = content;
            RefillLimbSpriteGrid();

            CreateSmallButton(right, "Import PNG", new Vector2(0f, -right.sizeDelta.y * 0.5f + 5f), ImportLimbPng, false, 40f);
        }

        static void RefillLimbSpriteGrid()
        {
            if (_spriteGridContent == null) return;
            for (int i = _spriteGridContent.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(_spriteGridContent.GetChild(i).gameObject);

            string q = (_spriteSearch ?? "").Trim().ToLowerInvariant();
            var filtered = new List<(string Path, Sprite Sprite)>();
            for (int i = 0; i < _limbCandidates.Count; i++)
            {
                var item = _limbCandidates[i];
                string file = Path.GetFileNameWithoutExtension(item.Path ?? "") ?? "";
                if (string.IsNullOrEmpty(q)
                    || (item.Path != null && item.Path.ToLowerInvariant().Contains(q))
                    || file.ToLowerInvariant().Contains(q))
                    filtered.Add(item);
            }

            int count = filtered.Count;
            if (count == 0)
            {
                CreateLabel(_spriteGridContent, string.IsNullOrEmpty(q) ? "No sprites found" : "No matches", new Vector2(0f, -10f), 2.1f);
                _spriteGridContent.GetComponent<RectTransform>().sizeDelta = new Vector2(46f, 40f);
                return;
            }

            PlaceSpriteGrid(_spriteGridContent, count, (i, pos) =>
            {
                var item = filtered[i];
                string name = Path.GetFileNameWithoutExtension(item.Path ?? "spr");
                if (string.IsNullOrEmpty(name)) name = "spr";
                CreateSpriteGridCell(_spriteGridContent, name, item.Sprite, pos, () => AssignLimbPath(item.Path, item.Sprite));
            });
        }

        static TMP_InputField CreateSearchField(Transform parent, Vector2 pos, Vector2 size, string value, string placeholder, Action<string> onChanged)
        {
            var go = new GameObject("SearchField");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            go.AddComponent<Image>().color = new Color(0.95f, 0.95f, 0.95f, 1f);

            var textArea = new GameObject("TextArea");
            textArea.transform.SetParent(go.transform, false);
            var taRt = textArea.AddComponent<RectTransform>();
            taRt.anchorMin = Vector2.zero;
            taRt.anchorMax = Vector2.one;
            taRt.offsetMin = new Vector2(1.5f, 0.6f);
            taRt.offsetMax = new Vector2(-1.5f, -0.6f);

            var phGo = new GameObject("Placeholder");
            phGo.transform.SetParent(textArea.transform, false);
            var phRt = phGo.AddComponent<RectTransform>();
            phRt.anchorMin = Vector2.zero;
            phRt.anchorMax = Vector2.one;
            phRt.offsetMin = Vector2.zero;
            phRt.offsetMax = Vector2.zero;
            var phTmp = phGo.AddComponent<TextMeshProUGUI>();
            phTmp.text = placeholder;
            phTmp.font = Font;
            phTmp.fontSize = 2.0f;
            phTmp.color = new Color(0.45f, 0.45f, 0.45f);
            phTmp.alignment = TextAlignmentOptions.MidlineLeft;

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(textArea.transform, false);
            var textRt = textGo.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
            var inputTmp = textGo.AddComponent<TextMeshProUGUI>();
            inputTmp.font = Font;
            inputTmp.fontSize = 2.0f;
            inputTmp.color = Color.black;
            inputTmp.alignment = TextAlignmentOptions.MidlineLeft;

            var input = go.AddComponent<TMP_InputField>();
            input.textViewport = taRt;
            input.textComponent = inputTmp;
            input.placeholder = phTmp;
            input.text = value ?? "";
            input.onValueChanged.AddListener(v => onChanged?.Invoke(v));
            TmInputGuard.Bind(input);
            return input;
        }

        static void CreateSpriteGridCell(Transform parent, string name, Sprite icon, Vector2 pos, Action onClick)
        {
            const float cellW = 21f;
            const float cellH = 30f;
            var cell = new GameObject("Cell");
            cell.transform.SetParent(parent, false);
            var rt = cell.AddComponent<RectTransform>();
            AnchorTopCenter(rt);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(cellW, cellH);
            cell.AddComponent<Image>().color = CellBg;
            var btn = cell.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => onClick?.Invoke());

            if (icon != null)
            {
                var iconGo = new GameObject("Icon");
                iconGo.transform.SetParent(cell.transform, false);
                var irt = iconGo.AddComponent<RectTransform>();
                irt.anchorMin = new Vector2(0.5f, 1f);
                irt.anchorMax = new Vector2(0.5f, 1f);
                irt.pivot = new Vector2(0.5f, 1f);
                irt.anchoredPosition = new Vector2(0f, -1.2f);
                irt.sizeDelta = new Vector2(cellW - 3f, cellH - 11f);
                var img = iconGo.AddComponent<Image>();
                img.sprite = icon;
                img.preserveAspect = true;
                img.raycastTarget = false;
            }

            var bar = new GameObject("NameBar");
            bar.transform.SetParent(cell.transform, false);
            var brt = bar.AddComponent<RectTransform>();
            brt.anchorMin = new Vector2(0f, 0f);
            brt.anchorMax = new Vector2(1f, 0f);
            brt.pivot = new Vector2(0.5f, 0f);
            brt.anchoredPosition = Vector2.zero;
            brt.sizeDelta = new Vector2(0f, 8.2f);
            bar.AddComponent<Image>().color = NameBar;

            var labelGo = new GameObject("Name");
            labelGo.transform.SetParent(bar.transform, false);
            var lrt = labelGo.AddComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(0.5f, 0.6f);
            lrt.offsetMax = new Vector2(-0.5f, -0.4f);
            var tmp = labelGo.AddComponent<TextMeshProUGUI>();
            tmp.text = name ?? "";
            tmp.font = Font;
            tmp.fontSize = 2.35f;
            tmp.alignment = TextAlignmentOptions.Midline;
            tmp.color = Color.white;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.raycastTarget = false;
        }

        static GameObject CreateSmallButtonColored(Transform parent, string label, Vector2 pos, Action onClick, Color bg, float width = 32f)
        {
            var go = new GameObject("Btn_" + label);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(width, 5.6f);
            go.AddComponent<Image>().color = bg;
            go.AddComponent<Button>().onClick.AddListener(() => onClick?.Invoke());
            var textGo = new GameObject("Label");
            textGo.transform.SetParent(go.transform, false);
            var trt = textGo.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(0.4f, 0f);
            trt.offsetMax = new Vector2(-0.4f, 0f);
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.font = Font;
            tmp.fontSize = 2.1f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            return go;
        }

        static void PlaceSpriteGrid(Transform content, int count, Action<int, Vector2> place)
        {
            float cellW = 21f;
            float cellH = 30f;
            int cols = SpriteGridCols;
            int rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)cols));
            float stepX = cellW + CellGap;
            float stepY = cellH + CellGap;
            float gridW = cols * cellW + (cols - 1) * CellGap;
            float gridH = rows * cellH + (rows - 1) * CellGap;
            content.GetComponent<RectTransform>().sizeDelta = new Vector2(46f, Mathf.Max(gridH + 6f, 40f));
            float startX = -gridW * 0.5f + cellW * 0.5f;
            float startY = -2f - cellH * 0.5f;
            for (int i = 0; i < count; i++)
            {
                int col = i % cols;
                int row = i / cols;
                place(i, new Vector2(startX + col * stepX, startY - row * stepY));
            }
        }

        static void RefreshLimbCandidates()
        {
            _limbCandidates.Clear();
            if (_draft == null) return;
            string limb = _draft.SelectedLimb ?? "Upper";
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var ct = _targetPerson != null
                ? _targetPerson.transform.root.GetComponent<CharacterTransformations>()
                : null;

            void Add(string path, Sprite spr)
            {
                if (spr == null || string.IsNullOrEmpty(path) || !seen.Add(path)) return;
                _limbCandidates.Add((path, spr));
            }

            if (string.Equals(limb, "Hair", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var item in TransformationMakerHairLibrary.GetForCharacter(_characterId, ct))
                    Add(item.Path, item.Sprite);
                return;
            }

            if (string.Equals(limb, "Head", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var item in TransformationMakerHairLibrary.GetHeadsForCharacter(_characterId, ct))
                    Add(item.Path, item.Sprite);
                return;
            }

            string fileName = limb + ".png";
            string charFs = !string.IsNullOrEmpty(_characterBasePath)
                ? CustomTransformationCatalog.ResolveModPath(_characterBasePath)
                : null;
            string charactersRoot = Path.Combine(TransformationMakerHairLibrary.ModRoot, "Assets", "Sprites", "Characters");

            void ScanFolder(string root, bool stopEarly)
            {
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) return;
                foreach (var file in Directory.GetFiles(root, fileName, SearchOption.AllDirectories))
                {
                    if (file.IndexOf("SkinIllustrations", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    if (file.IndexOf("Illustrations", StringComparison.OrdinalIgnoreCase) >= 0
                        && file.IndexOf("Custom skins", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    var spr = CustomTransformationCatalog.LoadSpriteFromDisk(file);
                    if (spr == null) continue;
                    string rel = file.Replace('\\', '/');
                    int idx = rel.IndexOf("Assets/", StringComparison.OrdinalIgnoreCase);
                    if (idx >= 0) rel = rel.Substring(idx);
                    Add(rel, spr);
                    if (stopEarly && _limbCandidates.Count >= 120) return;
                }
            }

            ScanFolder(charFs, true);
            if (_limbCandidates.Count < 120)
                ScanFolder(charactersRoot, true);

            string importRoot = CustomTransformationCatalog.EnsureImportFolder();
            if (Directory.Exists(importRoot) && _limbCandidates.Count < 120)
            {
                foreach (var file in Directory.GetFiles(importRoot, "*.png", SearchOption.AllDirectories))
                {
                    string name = Path.GetFileName(file);
                    if (!string.Equals(name, fileName, StringComparison.OrdinalIgnoreCase)
                        && name.IndexOf(limb, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    var spr = CustomTransformationCatalog.LoadSpriteFromDisk(file);
                    if (spr == null) continue;
                    string rel = file.Replace('\\', '/');
                    int idx = rel.IndexOf("Assets/", StringComparison.OrdinalIgnoreCase);
                    if (idx >= 0) rel = rel.Substring(idx);
                    Add(rel, spr);
                    if (_limbCandidates.Count >= 120) break;
                }
            }
        }

        static void SyncSelectedHairFromDraft()
        {
            _selectedHairPath = null;
            if (_draft?.LimbPaths == null) return;
            string hairKey = SkinPackDraft.LimbKey(_draft.ActiveSuffix, _draft.ActiveDamage, "Hair");
            if (_draft.LimbPaths.TryGetValue(hairKey, out var hairPath) && !string.IsNullOrEmpty(hairPath))
                _selectedHairPath = hairPath;
        }

        static void ApplyDraftHairToPerson()
        {
            if (_targetPerson == null || _draft == null) return;
            string hairPath = _selectedHairPath;
            string hairKey = SkinPackDraft.LimbKey(_draft.ActiveSuffix, _draft.ActiveDamage, "Hair");
            if (_draft.LimbPaths != null && _draft.LimbPaths.TryGetValue(hairKey, out var stored) && !string.IsNullOrEmpty(stored))
                hairPath = stored;
            if (string.IsNullOrEmpty(hairPath)) return;
            var head = FindHead(_targetPerson);
            if (head == null) return;
            var root = _targetPerson.transform.root.gameObject;
            var spr = CustomTransformationCatalog.ResolveSpriteReference(hairPath, root, true)
                ?? CustomTransformationCatalog.LoadSpriteFlexible(hairPath);
            if (spr != null) TmHairApply.ApplyHair(head, spr, Color.white, Vector3.zero, false);
        }

        static void ApplyDraftHeadToPerson()
        {
            if (_targetPerson == null || _draft == null) return;
            string headPath = null;
            string headKey = SkinPackDraft.LimbKey(_draft.ActiveSuffix, _draft.ActiveDamage, "Head");
            if (_draft.LimbPaths != null && _draft.LimbPaths.TryGetValue(headKey, out var stored) && !string.IsNullOrEmpty(stored))
                headPath = stored;

            Sprite spr = null;
            var root = _targetPerson.transform.root.gameObject;
            if (!string.IsNullOrEmpty(headPath))
            {
                spr = CustomTransformationCatalog.ResolveSpriteReference(headPath, root, false)
                    ?? CustomTransformationCatalog.LoadSpriteFlexible(headPath);
            }

            if (spr == null && !string.IsNullOrEmpty(_characterBasePath) && !string.IsNullOrEmpty(_draft.PackFolderName))
            {
                string packFs = DBPU.DbpuCustomSkinLoader.GetPackFolderFs(_characterBasePath, _draft.PackFolderName);
                string suffixRoot = string.IsNullOrEmpty(_draft.ActiveSuffix) ? packFs : Path.Combine(packFs, _draft.ActiveSuffix);
                string destDir = _draft.ActiveDamage <= 0 ? suffixRoot : Path.Combine(suffixRoot, "Damage" + _draft.ActiveDamage);
                string headFs = Path.Combine(destDir, "Head.png");
                if (!File.Exists(headFs)) headFs = Path.Combine(destDir, "Face.png");
                if (!File.Exists(headFs)) headFs = Path.Combine(packFs, "Head.png");
                if (!File.Exists(headFs)) headFs = Path.Combine(packFs, "Face.png");
                if (File.Exists(headFs))
                    spr = CustomTransformationCatalog.LoadSpriteFromDisk(headFs);
            }

            if (spr == null) return;
            var head = FindHead(_targetPerson);
            if (head == null) return;
            head.gameObject.ChangeLimbSprite(spr, LimbsStorage.FHead, LimbsStorage.BHead, LimbsStorage.DHead);
        }

        static string PersistSkinMakerSpritePath(string path, Sprite spriteHint, string subfolder, string fileStem)
        {
            return CustomTransformationCatalog.EnsurePersistedSpritePath(path, spriteHint, subfolder, fileStem);
        }

        static void AssignLimbPath(string path, Sprite spriteHint = null)
        {
            if (_draft == null || string.IsNullOrEmpty(path)) return;
            string limb = _draft.SelectedLimb ?? "Upper";
            var personRoot = _targetPerson != null ? _targetPerson.transform.root.gameObject : null;
            string stem = (_draft.PackFolderName ?? "skin").Replace(' ', '_') + "_" + limb;

            if (string.Equals(limb, "Hair", StringComparison.OrdinalIgnoreCase))
            {
                if (spriteHint == null)
                    spriteHint = CustomTransformationCatalog.ResolveSpriteReference(path, personRoot, true)
                        ?? CustomTransformationCatalog.LoadSpriteFlexible(path);
                string persisted = PersistSkinMakerSpritePath(path, spriteHint, "hair", stem);
                string hairKey = SkinPackDraft.LimbKey(_draft.ActiveSuffix, _draft.ActiveDamage, "Hair");
                _draft.LimbPaths[hairKey] = persisted;
                _selectedHairPath = persisted;
                if (_targetPerson != null && spriteHint != null)
                {
                    var head = FindHead(_targetPerson);
                    if (head != null) TmHairApply.ApplyHair(head, spriteHint, Color.white, Vector3.zero, false);
                }
                if (_targetPerson != null)
                    ApplyDraftLive(false);
                RebuildBody();
                return;
            }

            if (string.Equals(limb, "Head", StringComparison.OrdinalIgnoreCase))
            {
                if (spriteHint == null)
                    spriteHint = CustomTransformationCatalog.ResolveSpriteReference(path, personRoot, false)
                        ?? CustomTransformationCatalog.LoadSpriteFlexible(path);
                path = PersistSkinMakerSpritePath(path, spriteHint, "head", stem);
                if (_targetPerson != null && spriteHint != null)
                {
                    var head = FindHead(_targetPerson);
                    if (head != null)
                        head.gameObject.ChangeLimbSprite(spriteHint, LimbsStorage.FHead, LimbsStorage.BHead, LimbsStorage.DHead);
                }
            }

            string key = SkinPackDraft.LimbKey(_draft.ActiveSuffix, _draft.ActiveDamage, limb);
            _draft.LimbPaths[key] = path;
            if (_targetPerson != null)
                ApplyDraftLive(false);
            RebuildBody();
        }

        static void ImportLimbPng()
        {
            if (TmFilePicker.IsOpen) return;
            string src = null;
            try
            {
                RunWithUiYield(() =>
                {
                    src = TmFilePicker.OpenFile("Import Limb PNG", "PNG Image (*.png)|*.png|All files (*.*)|*.*");
                });
                if (string.IsNullOrEmpty(src) || !File.Exists(src)) return;
                string limb = _draft.SelectedLimb ?? "Upper";
                if (string.Equals(limb, "Hair", StringComparison.OrdinalIgnoreCase))
                {
                    AssignLimbPath(src);
                    return;
                }
                string packFs = DBPU.DbpuCustomSkinLoader.GetPackFolderFs(_characterBasePath, _draft.PackFolderName);
                string suffixRoot = string.IsNullOrEmpty(_draft.ActiveSuffix) ? packFs : Path.Combine(packFs, _draft.ActiveSuffix);
                string destDir = _draft.ActiveDamage <= 0 ? suffixRoot : Path.Combine(suffixRoot, "Damage" + _draft.ActiveDamage);
                if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);
                string dest = Path.Combine(destDir, limb + ".png");
                CustomTransformationCatalog.CopyFileShared(src, dest, true);
                AssignLimbPath(dest);
            }
            catch (Exception ex)
            {
                ShowNotify("Import failed: " + ex.Message);
            }
        }

        public static void ApplyDraftLive(bool notify = true)
        {
            if (_draft == null || _targetPerson == null || string.IsNullOrEmpty(_characterBasePath)) return;
            try
            {
                string key = RegisterDraftInSkinMenu(true);
                if (string.IsNullOrEmpty(key))
                {
                    if (notify)
                        DialogBoxManager.Notification("Could not register skin in the menu. Check that Upper.png exists.");
                    return;
                }
                if (notify)
                    ModAPI.Notify("Applied skin \"" + (_draft.DisplayName ?? key) + "\"");
            }
            catch (Exception ex)
            {
                if (notify)
                    DialogBoxManager.Notification("Apply failed: " + ex.Message);
                else
                    Debug.LogWarning("[DBPU] ApplyDraftLive failed: " + ex.Message);
            }
        }

        static void BuildDamageScreen()
        {
            CreateNavBar(_body, "Back", () => { _screen = ScreenKind.Editor; RebuildBody(); }, "Add", () =>
            {
                int next = MaxDamageLevel() + 1;
                string packFs = DBPU.DbpuCustomSkinLoader.GetPackFolderFs(_characterBasePath, _draft.PackFolderName);
                string suffixRoot = string.IsNullOrEmpty(_draft.ActiveSuffix) ? packFs : Path.Combine(packFs, _draft.ActiveSuffix);
                string dest = Path.Combine(suffixRoot, "Damage" + next);
                if (!Directory.Exists(dest)) Directory.CreateDirectory(dest);
                string seedRoot = _draft.ActiveDamage <= 0
                    ? suffixRoot
                    : Path.Combine(suffixRoot, "Damage" + _draft.ActiveDamage);
                if (!Directory.Exists(seedRoot)) seedRoot = suffixRoot;
                if (!File.Exists(Path.Combine(dest, "Upper.png")))
                    SkinPackIO.CopyLimbFolder(seedRoot, dest);
                _draft.ActiveDamage = next;
                string keepName = _draft.DisplayName;
                string keepSuffix = _draft.ActiveSuffix;
                _draft = SkinPackIO.LoadDraftFromPack(_characterBasePath, _draft.PackFolderName);
                _draft.DisplayName = keepName;
                _draft.ActiveSuffix = keepSuffix;
                _draft.ActiveDamage = next;
                RegisterDraftInSkinMenu(false);
                _screen = ScreenKind.Editor;
                RebuildBody();
                ShowNotify("Editing Damage" + next);
            });
            float bodyH = PanelH - TitleH - 3f;
            CreateScroll(_body, new Vector2(0f, -NavH), new Vector2(PanelW - 6f, bodyH - NavH - 2f), out Transform content);
            float y = -4f;
            CreateLabel(content.GetComponent<RectTransform>(), "Click a level to edit it", new Vector2(0f, y), 2.0f);
            y -= 8f;
            int max = MaxDamageLevel();
            for (int d = 0; d <= max; d++)
            {
                int damage = d;
                var go = new GameObject("Dmg");
                go.transform.SetParent(content, false);
                var rt = go.AddComponent<RectTransform>();
                AnchorTopCenter(rt);
                rt.anchoredPosition = new Vector2(0f, y);
                rt.sizeDelta = new Vector2(PanelW - 16f, 10f);
                go.AddComponent<Image>().color = damage == _draft.ActiveDamage ? Accent : new Color(0.16f, 0.16f, 0.16f, 1f);
                go.AddComponent<Button>().onClick.AddListener(() =>
                {
                    _draft.ActiveDamage = damage;
                    _screen = ScreenKind.Editor;
                    RebuildBody();
                });
                var label = CreateLabel(go.transform, "Damage" + damage + (damage == _draft.ActiveDamage ? "  (editing)" : ""), new Vector2(-8f, 0f), 2.4f);
                label.rectTransform.sizeDelta = new Vector2(90f, 10f);
                if (damage > 0)
                {
                    CreateTinyAction(go.transform, "Del", new Vector2(62f, 0f), 12f, () =>
                    {
                        RemoveDamageLevel(damage);
                        RegisterDraftInSkinMenu(false);
                        RebuildBody();
                    });
                }
                y -= 12f;
            }
            content.GetComponent<RectTransform>().sizeDelta = new Vector2(PanelW - 8f, Mathf.Max(40f, Mathf.Abs(y) + 8f));
        }

        static void RemoveDamageLevel(int damage)
        {
            if (damage <= 0 || _draft == null) return;
            string packFs = DBPU.DbpuCustomSkinLoader.GetPackFolderFs(_characterBasePath, _draft.PackFolderName);
            string suffixRoot = string.IsNullOrEmpty(_draft.ActiveSuffix) ? packFs : Path.Combine(packFs, _draft.ActiveSuffix);
            string dir = Path.Combine(suffixRoot, "Damage" + damage);
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            var remove = _draft.LimbPaths.Keys.Where(k =>
            {
                string[] p = k.Split('|');
                return p.Length >= 2 && p[0] == (_draft.ActiveSuffix ?? "") && p[1] == damage.ToString();
            }).ToList();
            foreach (var k in remove) _draft.LimbPaths.Remove(k);
            if (_draft.ActiveDamage == damage) _draft.ActiveDamage = Math.Max(0, damage - 1);
        }

        static void BuildBuffedScreen()
        {
            CreateNavBar(_body, "Back", () => { _screen = ScreenKind.Editor; RebuildBody(); });
            float bodyH = PanelH - TitleH - 3f;
            CreateScroll(_body, new Vector2(0f, -NavH), new Vector2(PanelW - 6f, bodyH - NavH - 2f), out Transform content);
            float y = -4f;
            CreateLabel(content.GetComponent<RectTransform>(), "Click a variant to edit it", new Vector2(0f, y), 2.0f);
            y -= 8f;

            string packFs = DBPU.DbpuCustomSkinLoader.GetPackFolderFs(_characterBasePath, _draft.PackFolderName);
            _draft.HasBuffed = Directory.Exists(Path.Combine(packFs, "_Buffed"))
                && File.Exists(Path.Combine(packFs, "_Buffed", "Upper.png"));
            _draft.HasBuffed2 = Directory.Exists(Path.Combine(packFs, "_Buffed2"))
                && File.Exists(Path.Combine(packFs, "_Buffed2", "Upper.png"));

            void VariantRow(string label, string suffix, bool exists, Action addAction)
            {
                var go = new GameObject("Var_" + label);
                go.transform.SetParent(content, false);
                var rt = go.AddComponent<RectTransform>();
                AnchorTopCenter(rt);
                rt.anchoredPosition = new Vector2(0f, y);
                rt.sizeDelta = new Vector2(PanelW - 16f, 10f);
                bool active = string.Equals(_draft.ActiveSuffix ?? "", suffix ?? "", StringComparison.OrdinalIgnoreCase);
                go.AddComponent<Image>().color = active ? Accent : new Color(0.16f, 0.16f, 0.16f, 1f);
                if (exists)
                {
                    go.AddComponent<Button>().onClick.AddListener(() =>
                    {
                        _draft.ActiveSuffix = suffix ?? "";
                        _draft.ActiveDamage = 0;
                        _screen = ScreenKind.Editor;
                        RebuildBody();
                    });
                    CreateLabel(go.transform, label + (active ? "  (editing)" : ""), new Vector2(-10f, 0f), 2.4f)
                        .rectTransform.sizeDelta = new Vector2(90f, 10f);
                    if (!string.IsNullOrEmpty(suffix))
                    {
                        CreateTinyAction(go.transform, "Del", new Vector2(62f, 0f), 12f, () =>
                        {
                            string dir = Path.Combine(packFs, suffix);
                            if (Directory.Exists(dir)) Directory.Delete(dir, true);
                            if (string.Equals(_draft.ActiveSuffix, suffix, StringComparison.OrdinalIgnoreCase))
                                _draft.ActiveSuffix = "";
                            if (suffix == "_Buffed") _draft.HasBuffed = false;
                            if (suffix == "_Buffed2") _draft.HasBuffed2 = false;
                            RegisterDraftInSkinMenu(false);
                            RebuildBody();
                        });
                    }
                }
                else
                {
                    go.AddComponent<Button>().onClick.AddListener(() => addAction?.Invoke());
                    CreateLabel(go.transform, "Add " + label, Vector2.zero, 2.4f);
                }
                y -= 12f;
            }

            VariantRow("Base", "", true, null);
            VariantRow("Buffed", "_Buffed", _draft.HasBuffed, () =>
            {
                string b = Path.Combine(packFs, "_Buffed");
                if (!Directory.Exists(b)) Directory.CreateDirectory(b);
                if (!File.Exists(Path.Combine(b, "Upper.png")))
                    SkinPackIO.CopyLimbFolder(packFs, b);
                _draft.HasBuffed = true;
                _draft.ActiveSuffix = "_Buffed";
                _draft.ActiveDamage = 0;
                RegisterDraftInSkinMenu(false);
                _screen = ScreenKind.Editor;
                RebuildBody();
                ShowNotify("Editing Buffed");
            });
            VariantRow("Buffed2", "_Buffed2", _draft.HasBuffed2, () =>
            {
                string b2 = Path.Combine(packFs, "_Buffed2");
                if (!Directory.Exists(b2)) Directory.CreateDirectory(b2);
                if (!File.Exists(Path.Combine(b2, "Upper.png")))
                    SkinPackIO.CopyLimbFolder(packFs, b2);
                _draft.HasBuffed2 = true;
                _draft.ActiveSuffix = "_Buffed2";
                _draft.ActiveDamage = 0;
                RegisterDraftInSkinMenu(false);
                _screen = ScreenKind.Editor;
                RebuildBody();
                ShowNotify("Editing Buffed2");
            });

            content.GetComponent<RectTransform>().sizeDelta = new Vector2(PanelW - 8f, Mathf.Max(40f, Mathf.Abs(y) + 8f));
        }

        static void BuildIllustrationsScreen()
        {
            CreateNavBar(_body, "Back", () => { _screen = ScreenKind.Editor; RebuildBody(); });
            float bodyH = PanelH - TitleH - 3f;
            float leftW = 70f;
            float rightW = PanelW - leftW - 10f;
            var left = CreatePanel(_body, "IllLeft", new Vector2(-(PanelW * 0.5f - leftW * 0.5f - 3f), -NavH - (bodyH - NavH) * 0.5f), new Vector2(leftW, bodyH - NavH - 2f));
            var right = CreatePanel(_body, "IllRight", new Vector2(PanelW * 0.5f - rightW * 0.5f - 3f, -NavH - (bodyH - NavH) * 0.5f), new Vector2(rightW, bodyH - NavH - 2f));

            CreateScroll(left, new Vector2(0f, -2f), new Vector2(leftW - 2f, left.sizeDelta.y - 4f), out Transform list);
            float y = -3f;
            int max = MaxDamageLevel();
            if (_illustrationDamage < 0 || _illustrationDamage > max)
                _illustrationDamage = _draft.ActiveDamage;
            for (int d = 0; d <= max; d++)
            {
                int damage = d;
                string key = "Damage" + damage;
                bool has = _draft.IllustrationPaths != null && _draft.IllustrationPaths.ContainsKey(key);
                var go = new GameObject("IllKey");
                go.transform.SetParent(list, false);
                var rt = go.AddComponent<RectTransform>();
                AnchorTopCenter(rt);
                rt.anchoredPosition = new Vector2(0f, y);
                rt.sizeDelta = new Vector2(leftW - 4f, 8f);
                go.AddComponent<Image>().color = damage == _illustrationDamage ? Accent : new Color(0.16f, 0.16f, 0.16f, 1f);
                go.AddComponent<Button>().onClick.AddListener(() =>
                {
                    _illustrationDamage = damage;
                    RebuildBody();
                });
                CreateLabel(go.transform, key + (has ? " *" : ""), Vector2.zero, 2.0f);
                y -= 9.5f;
            }
            list.GetComponent<RectTransform>().sizeDelta = new Vector2(leftW - 2f, Mathf.Max(30f, Mathf.Abs(y) + 6f));

            string selKey = "Damage" + _illustrationDamage;
            Sprite preview = null;
            if (_draft.IllustrationPaths != null
                && _draft.IllustrationPaths.TryGetValue(selKey, out var path)
                && !string.IsNullOrEmpty(path))
                preview = CustomTransformationCatalog.LoadSpriteFlexible(path);

            CreateLabel(right, selKey, new Vector2(0f, right.sizeDelta.y * 0.5f - 4f), 2.3f);
            var previewGo = new GameObject("Preview");
            previewGo.transform.SetParent(right, false);
            var prt = previewGo.AddComponent<RectTransform>();
            prt.anchoredPosition = new Vector2(0f, 4f);
            prt.sizeDelta = new Vector2(rightW - 8f, right.sizeDelta.y - 28f);
            var pimg = previewGo.AddComponent<Image>();
            pimg.color = new Color(0.1f, 0.1f, 0.1f, 1f);
            pimg.preserveAspect = true;
            if (preview != null)
            {
                pimg.sprite = preview;
                pimg.color = Color.white;
            }

            CreateSmallButton(right, "Capture", new Vector2(-22f, -right.sizeDelta.y * 0.5f + 6f), () =>
            {
                OpenIllustrationCapture(selKey);
            }, true, 28f);
            CreateSmallButton(right, "Import", new Vector2(22f, -right.sizeDelta.y * 0.5f + 6f), () =>
            {
                ImportIllustration(selKey);
            }, true, 28f);
        }

        static void ImportIllustration(string damageKey)
        {
            if (TmFilePicker.IsOpen) return;
            try
            {
                string src = null;
                RunWithUiYield(() =>
                {
                    src = TmFilePicker.OpenFile("Import Illustration", "PNG Image (*.png)|*.png|All files (*.*)|*.*");
                });
                if (string.IsNullOrEmpty(src) || !File.Exists(src)) return;
                SaveIllustrationFile(damageKey, src);
            }
            catch (Exception ex)
            {
                DialogBoxManager.Notification("Import failed: " + ex.Message);
            }
        }

        static void SaveIllustrationFile(string damageKey, string srcPath)
        {
            string packFs = DBPU.DbpuCustomSkinLoader.GetPackFolderFs(_characterBasePath, _draft.PackFolderName);
            string illDir = Path.Combine(packFs, "Illustrations");
            if (!Directory.Exists(illDir)) Directory.CreateDirectory(illDir);
            string dest = Path.Combine(illDir, damageKey + ".png");
            CustomTransformationCatalog.CopyFileShared(srcPath, dest, true);
            _draft.IllustrationPaths[damageKey] = dest;
            RegisterDraftInSkinMenu(false);
            RebuildBody();
        }

        static void OpenIllustrationCapture(string damageKey)
        {
            if (_draft == null || string.IsNullOrEmpty(damageKey)) return;
            CancelIllustrationCapture();
            CloseOverlay(false);
            if (_root != null) _root.SetActive(false);
            SetWorldClickMode(false);
            SkinMakerState.Disarm();

            _captureIllKey = damageKey;
            _captureBusy = false;

            Sprite frameSpr = CustomTransformationCatalog.LoadSpriteFlexible(EmptyIllustrationRel)
                ?? CustomTransformationCatalog.LoadSpriteFlexible(EmptyIllustrationRelAlt);
            try { if (frameSpr == null) frameSpr = ModAPI.LoadSprite(EmptyIllustrationRel); } catch { }
            try { if (frameSpr == null) frameSpr = ModAPI.LoadSprite(EmptyIllustrationRelAlt); } catch { }
            if (frameSpr == null)
            {
                if (_root != null) _root.SetActive(true);
                DialogBoxManager.Notification("Could not load empty illustration frame.");
                return;
            }

            var canvas = GameObject.Find("Canvas");
            if (canvas == null)
            {
                if (_root != null) _root.SetActive(true);
                return;
            }

            _captureRoot = new GameObject("SmMediaCapture");
            _captureRoot.transform.SetParent(canvas.transform, false);
            var rootRt = _captureRoot.AddComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            var canvasRt = canvas.GetComponent<RectTransform>();
            float canvasW = Mathf.Max(100f, canvasRt.rect.width);
            float canvasH = Mathf.Max(100f, canvasRt.rect.height);
            float maxW = canvasW * 0.48f;
            float maxH = canvasH * 0.72f;
            float aspect = IllFrameW / (float)IllFrameH;
            float dispH = maxH;
            float dispW = dispH * aspect;
            if (dispW > maxW)
            {
                dispW = maxW;
                dispH = dispW / aspect;
            }

            var frameGo = new GameObject("Frame");
            frameGo.transform.SetParent(_captureRoot.transform, false);
            _captureFrameRt = frameGo.AddComponent<RectTransform>();
            _captureFrameRt.anchorMin = _captureFrameRt.anchorMax = new Vector2(0.5f, 0.5f);
            _captureFrameRt.pivot = new Vector2(0.5f, 0.5f);
            _captureFrameRt.anchoredPosition = new Vector2(0f, canvasH * 0.04f);
            _captureFrameRt.sizeDelta = new Vector2(dispW, dispH);
            var frameImg = frameGo.AddComponent<Image>();
            frameImg.sprite = frameSpr;
            frameImg.preserveAspect = true;
            frameImg.raycastTarget = false;

            _captureHud = new GameObject("Hud");
            _captureHud.transform.SetParent(_captureRoot.transform, false);
            var hudRt = _captureHud.AddComponent<RectTransform>();
            hudRt.anchorMin = Vector2.zero;
            hudRt.anchorMax = Vector2.one;
            hudRt.offsetMin = Vector2.zero;
            hudRt.offsetMax = Vector2.zero;

            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(_captureHud.transform, false);
            var titleRt = titleGo.AddComponent<RectTransform>();
            titleRt.anchorMin = titleRt.anchorMax = new Vector2(0.5f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -18f);
            titleRt.sizeDelta = new Vector2(420f, 36f);
            var title = titleGo.AddComponent<TextMeshProUGUI>();
            title.text = "Capture Illustration (" + damageKey + ")";
            title.font = Font;
            title.fontSize = 24f;
            title.alignment = TextAlignmentOptions.Center;
            title.color = Accent;
            title.raycastTarget = false;

            void MakeCapBtn(string label, Vector2 pos, Action act)
            {
                var go = new GameObject("Btn_" + label);
                go.transform.SetParent(_captureHud.transform, false);
                var rt = go.AddComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.anchoredPosition = pos;
                rt.sizeDelta = new Vector2(160f, 40f);
                go.AddComponent<Image>().color = new Color(0.18f, 0.18f, 0.18f, 0.95f);
                go.AddComponent<Button>().onClick.AddListener(() => act?.Invoke());
                var tGo = new GameObject("T");
                tGo.transform.SetParent(go.transform, false);
                var trt = tGo.AddComponent<RectTransform>();
                trt.anchorMin = Vector2.zero;
                trt.anchorMax = Vector2.one;
                trt.offsetMin = Vector2.zero;
                trt.offsetMax = Vector2.zero;
                var tmp = tGo.AddComponent<TextMeshProUGUI>();
                tmp.text = label;
                tmp.font = Font;
                tmp.fontSize = 16f;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.white;
                tmp.raycastTarget = false;
            }

            MakeCapBtn("Take Photo", new Vector2(-95f, 28f), BeginIllustrationCaptureShot);
            MakeCapBtn("Cancel", new Vector2(95f, 28f), () => CancelIllustrationCapture());
            ModAPI.Notify("Illustration capture ready");
        }

        public static void CancelIllustrationCapture(bool restoreUi = true)
        {
            _captureBusy = false;
            _captureIllKey = null;
            if (_captureDriver != null)
            {
                UnityEngine.Object.Destroy(_captureDriver.gameObject);
                _captureDriver = null;
            }
            if (_captureRoot != null)
            {
                UnityEngine.Object.Destroy(_captureRoot);
                _captureRoot = null;
            }
            _captureFrameRt = null;
            _captureHud = null;
            if (restoreUi && _root != null)
            {
                _root.SetActive(true);
                _screen = ScreenKind.Illustrations;
                RebuildBody();
            }
        }

        static void BeginIllustrationCaptureShot()
        {
            if (_captureBusy || _captureRoot == null || _captureFrameRt == null) return;
            if (!TryGetIllustrationCaptureRect(out _captureSx, out _captureSy, out _captureSw, out _captureSh))
            {
                DialogBoxManager.Notification("Could not resolve capture area.");
                return;
            }
            _captureBusy = true;
            if (_captureRoot != null)
            {
                for (int i = 0; i < _captureRoot.transform.childCount; i++)
                    _captureRoot.transform.GetChild(i).gameObject.SetActive(false);
            }
            var host = new GameObject("SmCaptureDriver");
            _captureDriver = host.AddComponent<TmCaptureDriver>();
            _captureDriver.OnShot = FinishIllustrationCaptureShot;
            _captureDriver.BeginShot();
        }

        static void FinishIllustrationCaptureShot()
        {
            try
            {
                int cx = IllContentX;
                int cyFromTop = IllContentY;
                int cw = IllContentW;
                int ch = IllContentH;
                int cy = IllFrameH - cyFromTop - ch;
                int sx = Mathf.Clamp(_captureSx, 0, Screen.width - 8);
                int sy = Mathf.Clamp(_captureSy, 0, Screen.height - 8);
                int sw = Mathf.Clamp(_captureSw, 8, Screen.width - sx);
                int sh = Mathf.Clamp(_captureSh, 8, Screen.height - sy);

                var shot = new Texture2D(sw, sh, TextureFormat.RGB24, false);
                shot.ReadPixels(new Rect(sx, sy, sw, sh), 0, 0);
                shot.Apply(false, false);

                var template = LoadIllustrationTemplate();
                if (template == null)
                {
                    UnityEngine.Object.Destroy(shot);
                    DialogBoxManager.Notification("Could not load empty template.");
                    CancelIllustrationCapture();
                    return;
                }

                var scaled = ScaleCaptureTexture(shot, cw, ch);
                UnityEngine.Object.Destroy(shot);
                for (int y = 0; y < ch; y++)
                {
                    for (int x = 0; x < cw; x++)
                    {
                        Color c = scaled.GetPixel(x, y);
                        c.a = 1f;
                        template.SetPixel(cx + x, cy + y, c);
                    }
                }
                template.Apply(false, false);
                UnityEngine.Object.Destroy(scaled);

                string temp = Path.Combine(Path.GetTempPath(), "sm_ill_" + Guid.NewGuid().ToString("N") + ".png");
                File.WriteAllBytes(temp, template.EncodeToPNG());
                UnityEngine.Object.Destroy(template);

                string key = _captureIllKey ?? ("Damage" + _illustrationDamage);
                SaveIllustrationFile(key, temp);
                try { File.Delete(temp); } catch { }
                ModAPI.Notify("Illustration saved");
            }
            catch (Exception ex)
            {
                DialogBoxManager.Notification("Capture failed: " + ex.Message);
            }

            _captureBusy = false;
            if (_captureDriver != null)
            {
                UnityEngine.Object.Destroy(_captureDriver.gameObject);
                _captureDriver = null;
            }
            if (_captureRoot != null)
            {
                UnityEngine.Object.Destroy(_captureRoot);
                _captureRoot = null;
            }
            _captureFrameRt = null;
            _captureHud = null;
            _captureIllKey = null;
            if (_root != null)
            {
                _root.SetActive(true);
                _screen = ScreenKind.Illustrations;
                RebuildBody();
            }
        }

        static bool TryGetIllustrationCaptureRect(out int sx, out int sy, out int sw, out int sh)
        {
            sx = sy = sw = sh = 0;
            if (_captureFrameRt == null) return false;
            float u0 = IllContentX / (float)IllFrameW;
            float u1 = (IllContentX + IllContentW) / (float)IllFrameW;
            float v0 = (IllFrameH - IllContentY - IllContentH) / (float)IllFrameH;
            float v1 = (IllFrameH - IllContentY) / (float)IllFrameH;
            Vector3[] corners = new Vector3[4];
            _captureFrameRt.GetWorldCorners(corners);
            Vector3 bl = Vector3.Lerp(Vector3.Lerp(corners[0], corners[3], u0), Vector3.Lerp(corners[1], corners[2], u0), v0);
            Vector3 tr = Vector3.Lerp(Vector3.Lerp(corners[0], corners[3], u1), Vector3.Lerp(corners[1], corners[2], u1), v1);
            var cam = GameObject.Find("Canvas")?.GetComponent<Canvas>()?.worldCamera;
            Vector2 sbl = RectTransformUtility.WorldToScreenPoint(cam, bl);
            Vector2 str = RectTransformUtility.WorldToScreenPoint(cam, tr);
            sx = Mathf.RoundToInt(Mathf.Min(sbl.x, str.x));
            sy = Mathf.RoundToInt(Mathf.Min(sbl.y, str.y));
            sw = Mathf.RoundToInt(Mathf.Abs(str.x - sbl.x));
            sh = Mathf.RoundToInt(Mathf.Abs(str.y - sbl.y));
            return sw > 4 && sh > 4;
        }

        static Texture2D LoadIllustrationTemplate()
        {
            foreach (var rel in new[] { EmptyIllustrationRel, EmptyIllustrationRelAlt })
            {
                try
                {
                    string abs = CustomTransformationCatalog.ResolveModPath(rel);
                    if (string.IsNullOrEmpty(abs) || !File.Exists(abs)) continue;
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (!tex.LoadImage(File.ReadAllBytes(abs))) continue;
                    tex.filterMode = FilterMode.Point;
                    return tex;
                }
                catch { }
            }
            var blank = new Texture2D(IllFrameW, IllFrameH, TextureFormat.RGBA32, false);
            var cols = new Color[IllFrameW * IllFrameH];
            for (int i = 0; i < cols.Length; i++) cols[i] = new Color(0f, 0f, 0f, 0f);
            blank.SetPixels(cols);
            blank.Apply(false, false);
            return blank;
        }

        static Texture2D ScaleCaptureTexture(Texture2D src, int w, int h)
        {
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            var prev = RenderTexture.active;
            Graphics.Blit(src, rt);
            RenderTexture.active = rt;
            var dst = new Texture2D(w, h, TextureFormat.RGBA32, false);
            dst.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            dst.Apply(false, false);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return dst;
        }

        static void BuildBeltsScreen()
        {
            if (_draft == null)
            {
                _screen = ScreenKind.Skins;
                RebuildBody();
                return;
            }
            EnsureDraftBeltsFromExisting();
            CreateNavBar(_body, "Back", () =>
            {
                if (_draft == null)
                {
                    _screen = ScreenKind.Skins;
                    RebuildBody();
                    return;
                }
                try { SkinPackIO.SaveDraft(_characterBasePath, _draft); } catch { }
                _screen = ScreenKind.Editor;
                RebuildBody();
            }, "Add", AddBeltImport);
            if (_draft.Belts == null) _draft.Belts = new List<DBPU.DbpuCustomSkinLoader.SkinBeltDto>();
            float bodyH = PanelH - TitleH - 3f;
            CreateScroll(_body, new Vector2(0f, -NavH), new Vector2(PanelW - 6f, bodyH - NavH - 2f), out Transform content);
            float y = -4f;
            for (int i = 0; i < _draft.Belts.Count; i++)
            {
                int idx = i;
                var belt = _draft.Belts[i];
                if (belt == null) continue;
                var go = new GameObject("Belt");
                go.transform.SetParent(content, false);
                var rt = go.AddComponent<RectTransform>();
                AnchorTopCenter(rt);
                rt.anchoredPosition = new Vector2(0f, y);
                rt.sizeDelta = new Vector2(PanelW - 16f, 10f);
                go.AddComponent<Image>().color = new Color(0.16f, 0.16f, 0.16f, 1f);
                CreateLabel(go.transform, belt.Name ?? ("Belt " + idx), new Vector2(-24f, 0f), 2.3f).rectTransform.sizeDelta = new Vector2(60f, 10f);
                CreateTinyAction(go.transform, "Name", new Vector2(30f, 0f), 14f, () =>
                {
                    if (_draft == null || idx < 0 || idx >= _draft.Belts.Count) return;
                    var b = _draft.Belts[idx];
                    if (b == null) return;
                    PromptString(b.Name ?? "Belt", v =>
                    {
                        if (_draft == null || idx < 0 || idx >= _draft.Belts.Count) return;
                        if (_draft.Belts[idx] != null) _draft.Belts[idx].Name = v;
                        RebuildBody();
                    });
                });
                CreateTinyAction(go.transform, "Del", new Vector2(55f, 0f), 12f, () =>
                {
                    if (_draft?.Belts == null || idx < 0 || idx >= _draft.Belts.Count) return;
                    _draft.Belts.RemoveAt(idx);
                    RebuildBody();
                });
                y -= 12f;
            }
            CreateSmallButton(content, "Save Belts", new Vector2(0f, y - 4f), () =>
            {
                try
                {
                    if (_draft == null) return;
                    SkinPackIO.SaveDraft(_characterBasePath, _draft);
                    DialogBoxManager.Notification("Belts saved.");
                }
                catch (Exception ex)
                {
                    DialogBoxManager.Notification("Save failed: " + ex.Message);
                }
            }, true, 40f);
            content.GetComponent<RectTransform>().sizeDelta = new Vector2(PanelW - 8f, Mathf.Max(40f, Mathf.Abs(y) + 20f));
        }

        static void AddBeltImport()
        {
            if (_draft == null || TmFilePicker.IsOpen) return;
            try
            {
                string src = null;
                RunWithUiYield(() =>
                {
                    src = TmFilePicker.OpenFile("Import Belt PNG", "PNG Image (*.png)|*.png|All files (*.*)|*.*");
                });
                if (string.IsNullOrEmpty(src) || !File.Exists(src)) return;
                if (_draft.Belts == null) _draft.Belts = new List<DBPU.DbpuCustomSkinLoader.SkinBeltDto>();
                string packFs = DBPU.DbpuCustomSkinLoader.GetPackFolderFs(_characterBasePath, _draft.PackFolderName);
                string beltsDir = Path.Combine(packFs, "Belts");
                if (!Directory.Exists(beltsDir)) Directory.CreateDirectory(beltsDir);
                string destName = "belt_" + (_draft.Belts.Count + 1) + ".png";
                string dest = Path.Combine(beltsDir, destName);
                CustomTransformationCatalog.CopyFileShared(src, dest, true);
                _draft.Belts.Add(new DBPU.DbpuCustomSkinLoader.SkinBeltDto
                {
                    Name = "Belt " + (_draft.Belts.Count + 1),
                    SpriteFile = destName
                });
                if (IsOpen) RebuildBody();
            }
            catch (Exception ex)
            {
                DialogBoxManager.Notification("Import failed: " + ex.Message);
            }
        }

        static Texture2D CopySpriteToReadableTexture(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null) return null;
            var tex = sprite.texture;
            if (tex.isReadable)
            {
                var rect = sprite.textureRect;
                var readable = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGBA32, false);
                readable.SetPixels(tex.GetPixels((int)rect.x, (int)rect.y, (int)rect.width, (int)rect.height));
                readable.Apply(false, false);
                return readable;
            }
            var rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32);
            var prev = RenderTexture.active;
            Graphics.Blit(tex, rt);
            RenderTexture.active = rt;
            var full = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
            full.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
            full.Apply(false, false);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            var cropRect = sprite.textureRect;
            if (cropRect.width >= tex.width - 0.5f && cropRect.height >= tex.height - 0.5f)
                return full;
            var cropped = new Texture2D((int)cropRect.width, (int)cropRect.height, TextureFormat.RGBA32, false);
            cropped.SetPixels(full.GetPixels((int)cropRect.x, (int)cropRect.y, (int)cropRect.width, (int)cropRect.height));
            cropped.Apply(false, false);
            UnityEngine.Object.Destroy(full);
            return cropped;
        }

        static string PersistBeltSprite(Sprite sprite, string beltsDir, string stem)
        {
            if (sprite == null || string.IsNullOrEmpty(beltsDir)) return null;
            try
            {
                if (!Directory.Exists(beltsDir)) Directory.CreateDirectory(beltsDir);
                var readable = CopySpriteToReadableTexture(sprite);
                if (readable == null) return null;
                string fileName = stem + ".png";
                string path = Path.Combine(beltsDir, fileName);
                File.WriteAllBytes(path, readable.EncodeToPNG());
                UnityEngine.Object.Destroy(readable);
                return fileName;
            }
            catch
            {
                return null;
            }
        }

        static void EnsureDraftBeltsFromExisting()
        {
            if (_draft == null || string.IsNullOrEmpty(_characterBasePath) || string.IsNullOrEmpty(_draft.PackFolderName))
                return;
            if (_draft.Belts == null) _draft.Belts = new List<DBPU.DbpuCustomSkinLoader.SkinBeltDto>();
            var existingNames = new HashSet<string>(
                _draft.Belts.Where(b => b != null && !string.IsNullOrEmpty(b.Name)).Select(b => b.Name),
                StringComparer.OrdinalIgnoreCase);
            var existingFiles = new HashSet<string>(
                _draft.Belts.Where(b => b != null && !string.IsNullOrEmpty(b.SpriteFile)).Select(b => Path.GetFileName(b.SpriteFile)),
                StringComparer.OrdinalIgnoreCase);

            string packFs = DBPU.DbpuCustomSkinLoader.GetPackFolderFs(_characterBasePath, _draft.PackFolderName);
            string beltsDir = Path.Combine(packFs, "Belts");
            if (!Directory.Exists(beltsDir)) Directory.CreateDirectory(beltsDir);

            void ConsiderPhy(CharacterSkins.Skins.PhyAccessoryData data, string fallbackName)
            {
                if (data == null || data.sprites == null || data.sprites.Length == 0) return;
                string name = !string.IsNullOrEmpty(data.Name) ? data.Name : fallbackName;
                if (string.IsNullOrEmpty(name)) name = "Belt";
                if (existingNames.Contains(name)) return;
                var spr = data.sprites[0];
                if (spr == null) return;
                string stem = "belt_" + (_draft.Belts.Count + 1);
                string fileName = PersistBeltSprite(spr, beltsDir, stem);
                if (string.IsNullOrEmpty(fileName) || existingFiles.Contains(fileName)) return;
                _draft.Belts.Add(new DBPU.DbpuCustomSkinLoader.SkinBeltDto
                {
                    Name = name,
                    SpriteFile = fileName,
                    AnchorX = data.connectedAnchor.x,
                    AnchorY = data.connectedAnchor.y,
                    LayerName = string.IsNullOrEmpty(data.layerName) ? "Foreground" : data.layerName,
                    Order = data.order,
                    MinAngleFirstPiece = data.minAngleFirstPiece,
                    MaxAngleFirstPiece = data.maxAngleFirstPiece,
                    MinAngle = data.minAngle,
                    MaxAngle = data.maxAngle,
                    PieceMass = data.pieceMass > 0f ? data.pieceMass : 0.0018f,
                    PieceInertia = data.pieceInertia > 0f ? data.pieceInertia : 0.001f,
                    HasCollision = data.HasCollision,
                    CollideJustWithGround = data.CollideJustWithGround
                });
                existingNames.Add(name);
                existingFiles.Add(fileName);
            }

            void ConsiderSkinInfo(CharacterSkins.Skins.SkinInfo info)
            {
                if (info?.PhyAccessories == null) return;
                for (int i = 0; i < info.PhyAccessories.Count; i++)
                    ConsiderPhy(info.PhyAccessories[i], "Belt " + (i + 1));
            }

            if (_targetSkins?.skins?.SkinData != null)
            {
                if (!string.IsNullOrEmpty(_draft.SkinKey)
                    && _targetSkins.skins.SkinData.TryGetValue(_draft.SkinKey, out var own))
                    ConsiderSkinInfo(own);
                if (_skinMenu != null && !string.IsNullOrEmpty(_skinMenu.CurrentSkin)
                    && _targetSkins.skins.SkinData.TryGetValue(_skinMenu.CurrentSkin, out var cur))
                    ConsiderSkinInfo(cur);
                foreach (var kvp in _targetSkins.skins.SkinData)
                {
                    if (kvp.Value == null) continue;
                    if (!string.IsNullOrEmpty(_draft.DisplayName)
                        && string.Equals(kvp.Value.DisplayName, _draft.DisplayName, StringComparison.OrdinalIgnoreCase))
                        ConsiderSkinInfo(kvp.Value);
                }
            }

            if (_targetPerson != null && _targetPerson.Limbs != null)
            {
                for (int li = 0; li < _targetPerson.Limbs.Length; li++)
                {
                    var limb = _targetPerson.Limbs[li];
                    if (limb == null) continue;
                    string limbName = limb.name ?? "";
                    if (!string.Equals(limbName, "LowerBody", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(limbName, "UpperBody", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(limbName, "MiddleBody", StringComparison.OrdinalIgnoreCase))
                        continue;
                    for (int c = 0; c < limb.transform.childCount; c++)
                    {
                        var child = limb.transform.GetChild(c);
                        if (child == null) continue;
                        string cn = child.name ?? "";
                        if (cn.IndexOf("Belt", StringComparison.OrdinalIgnoreCase) < 0
                            && cn.IndexOf("Waist", StringComparison.OrdinalIgnoreCase) < 0
                            && cn.IndexOf("Sash", StringComparison.OrdinalIgnoreCase) < 0
                            && cn.IndexOf("Cape", StringComparison.OrdinalIgnoreCase) < 0
                            && cn.IndexOf("Cloth", StringComparison.OrdinalIgnoreCase) < 0
                            && cn.IndexOf("Tail", StringComparison.OrdinalIgnoreCase) < 0)
                            continue;
                        if (existingNames.Contains(cn)) continue;
                        var srs = child.GetComponentsInChildren<SpriteRenderer>(true);
                        Sprite spr = null;
                        for (int s = 0; s < srs.Length; s++)
                        {
                            if (srs[s] != null && srs[s].sprite != null)
                            {
                                spr = srs[s].sprite;
                                break;
                            }
                        }
                        if (spr == null) continue;
                        string stem = "belt_" + (_draft.Belts.Count + 1);
                        string fileName = PersistBeltSprite(spr, beltsDir, stem);
                        if (string.IsNullOrEmpty(fileName) || existingFiles.Contains(fileName)) continue;
                        _draft.Belts.Add(new DBPU.DbpuCustomSkinLoader.SkinBeltDto
                        {
                            Name = cn,
                            SpriteFile = fileName
                        });
                        existingNames.Add(cn);
                        existingFiles.Add(fileName);
                    }
                }
            }
        }

        static void ConfirmShareSkin(string packFolder, string skinName)
        {
            if (string.IsNullOrEmpty(packFolder))
            {
                DialogBoxManager.Notification("Could not find that skin to share.");
                return;
            }
            _sharePackFolder = packFolder;
            _shareSkinName = skinName ?? packFolder;
            _screen = ScreenKind.ShareChoice;
            RebuildBody();
        }

        static void BuildShareChoiceScreen()
        {
            SetWorldClickMode(false);
            float bodyH = PanelH - TitleH - 3f;
            CreateNavBar(_body, "Back", () =>
            {
                _screen = ScreenKind.Skins;
                RebuildBody();
            });

            CreateScroll(_body, new Vector2(0f, -NavH), new Vector2(PanelW - 6f, bodyH - NavH - 2f), out Transform content);
            float y = -8f;
            CreateLabel(content.GetComponent<RectTransform>(), "Share \"" + (_shareSkinName ?? "Skin") + "\"", new Vector2(0f, y), 2.4f);
            y -= 14f;

            void AddChoice(string label, Action act)
            {
                var go = new GameObject("Share_" + label);
                go.transform.SetParent(content, false);
                var rt = go.AddComponent<RectTransform>();
                AnchorTopCenter(rt);
                rt.anchoredPosition = new Vector2(0f, y);
                rt.sizeDelta = new Vector2(PanelW - 20f, 12f);
                go.AddComponent<Image>().color = new Color(0.16f, 0.18f, 0.22f, 1f);
                go.AddComponent<Button>().onClick.AddListener(() => act?.Invoke());
                var tGo = new GameObject("T");
                tGo.transform.SetParent(go.transform, false);
                var trt = tGo.AddComponent<RectTransform>();
                trt.anchorMin = Vector2.zero;
                trt.anchorMax = Vector2.one;
                trt.offsetMin = Vector2.zero;
                trt.offsetMax = Vector2.zero;
                var tmp = tGo.AddComponent<TextMeshProUGUI>();
                tmp.text = label;
                tmp.font = Font;
                tmp.fontSize = 2.3f;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.white;
                tmp.raycastTarget = false;
                y -= 14f;
            }

            AddChoice("File Pack (.dbpusk)", () =>
            {
                if (TmFilePicker.IsOpen) return;
                try
                {
                    bool exported = false;
                    RunWithUiYield(() =>
                    {
                        exported = SkinShare.ExportPack(_characterId, _displayName, _characterBasePath, _sharePackFolder, _shareSkinName);
                    });
                    if (exported) ShowNotify("Skin pack exported.");
                }
                catch (Exception ex)
                {
                    ShowNotify("Share failed: " + ex.Message);
                }
            });
            AddChoice("Steam Workshop", () => BeginWorkshopUploadMenu());
            content.GetComponent<RectTransform>().sizeDelta = new Vector2(PanelW - 8f, Mathf.Max(40f, Mathf.Abs(y) + 8f));
        }

        static void ImportSharedSkin()
        {
            if (TmFilePicker.IsOpen) return;
            HideMakerUi();
            DialogBoxManager.Dialog(
                "Import a skin pack (.dbpusk / .zip) or browse Workshop",
                new DialogButton("Browse", true, new UnityAction[]
                {
                    () =>
                    {
                        try
                        {
                            if (TmFilePicker.IsOpen)
                            {
                                ShowMakerUi();
                                return;
                            }
                            string pack = SkinShare.ImportPack(_characterBasePath, _targetSkins);
                            ShowMakerUi();
                            if (!string.IsNullOrEmpty(pack))
                            {
                                OpenEditorForPack(pack);
                                ModAPI.Notify("Imported skin \"" + pack + "\".");
                                return;
                            }
                            RebuildBody();
                        }
                        catch (Exception ex)
                        {
                            ShowMakerUi();
                            ShowNotify("Import failed: " + ex.Message);
                        }
                    }
                }),
                new DialogButton("Enter Path", true, new UnityAction[]
                {
                    () => PromptImportSkinPath()
                }),
                new DialogButton("Workshop", true, new UnityAction[]
                {
                    () =>
                    {
                        ShowMakerUi();
                        _workshopRemote = null;
                        _workshopPage = 1;
                        _workshopStatus = null;
                        _workshopLoading = false;
                        _screen = ScreenKind.Workshop;
                        RebuildBody();
                    }
                }),
                new DialogButton("Cancel", true, new UnityAction[]
                {
                    () => ShowMakerUi()
                }));
        }

        static void PromptImportSkinPath()
        {
            DialogBox box = null;
            box = DialogBoxManager.TextEntry(
                "Paste full path to .dbpusk or .zip",
                "C:\\path\\to\\skin.dbpusk",
                new DialogButton("Import", true, new UnityAction[]
                {
                    () =>
                    {
                        try
                        {
                            string path = box != null ? (box.EnteredText ?? "") : "";
                            string pack = SkinShare.ImportPackFromPath(path, _characterBasePath, _targetSkins);
                            ShowMakerUi();
                            if (!string.IsNullOrEmpty(pack))
                            {
                                OpenEditorForPack(pack);
                                ModAPI.Notify("Imported skin \"" + pack + "\".");
                            }
                            else RebuildBody();
                        }
                        catch (Exception ex)
                        {
                            ShowMakerUi();
                            ShowNotify("Import failed: " + ex.Message);
                        }
                    }
                }),
                new DialogButton("Cancel", true, new UnityAction[]
                {
                    () => ShowMakerUi()
                }));
        }

        static void BeginWorkshopUploadMenu()
        {
            if (!DbpuWorkshop.SteamReady)
            {
                ShowNotify("Steam is not available.\nStart PPG through Steam to upload.");
                return;
            }
            _workshopPublishTitle = _shareSkinName ?? "Custom Skin";
            _workshopPublishDescription = "";
            _workshopPublishPreviewPath = null;
            _workshopUploading = false;
            _workshopUploadStatus = null;
            string packFs = DBPU.DbpuCustomSkinLoader.GetPackFolderFs(_characterBasePath, _sharePackFolder);
            string preview = Path.Combine(packFs, "Upper.png");
            if (File.Exists(preview)) _workshopPublishPreviewPath = preview;
            string ill = Path.Combine(packFs, "Illustrations", "Damage0.png");
            if (File.Exists(ill)) _workshopPublishPreviewPath = ill;
            _screen = ScreenKind.WorkshopPublish;
            RebuildBody();
        }

        static void BuildWorkshopPublishScreen()
        {
            SetWorldClickMode(false);
            float bodyH = PanelH - TitleH - 3f;
            CreateNavBar(_body, "Back", () =>
            {
                _screen = ScreenKind.ShareChoice;
                RebuildBody();
            }, _workshopUploading ? null : "Upload", _workshopUploading ? null : (Action)StartWorkshopUpload);

            CreateScroll(_body, new Vector2(0f, -NavH), new Vector2(PanelW - 6f, bodyH - NavH - 2f), out Transform content);
            float y = -6f;
            CreateLabel(content.GetComponent<RectTransform>(), "Title: " + (_workshopPublishTitle ?? ""), new Vector2(0f, y), 2.1f);
            y -= 8f;
            CreateSmallButton(content, "Edit Title", new Vector2(0f, y), () =>
            {
                PromptString(_workshopPublishTitle, v => { _workshopPublishTitle = v; RebuildBody(); });
            }, false, 40f);
            y -= 10f;
            CreateLabel(content.GetComponent<RectTransform>(), "Description:", new Vector2(0f, y), 2.0f);
            y -= 6f;
            CreateLabel(content.GetComponent<RectTransform>(),
                string.IsNullOrEmpty(_workshopPublishDescription) ? "(none)" : _workshopPublishDescription,
                new Vector2(0f, y), 1.8f);
            y -= 10f;
            CreateSmallButton(content, "Edit Description", new Vector2(0f, y), () =>
            {
                PromptString(_workshopPublishDescription, v => { _workshopPublishDescription = v; RebuildBody(); });
            }, false, 44f);
            y -= 12f;
            if (!string.IsNullOrEmpty(_workshopUploadStatus))
                CreateLabel(content.GetComponent<RectTransform>(), _workshopUploadStatus, new Vector2(0f, y), 1.9f);
            content.GetComponent<RectTransform>().sizeDelta = new Vector2(PanelW - 8f, Mathf.Max(40f, Mathf.Abs(y) + 10f));
        }

        static void StartWorkshopUpload()
        {
            if (_workshopUploading) return;
            _workshopUploading = true;
            _workshopUploadStatus = "Uploading...";
            RebuildBody();
            DbpuWorkshop.BeginPublishSkin(
                _characterId,
                _displayName,
                _characterBasePath,
                _sharePackFolder,
                _shareSkinName,
                _workshopPublishTitle,
                _workshopPublishDescription,
                _workshopPublishPreviewPath,
                (ok, msg) =>
                {
                    _workshopUploading = false;
                    _workshopUploadStatus = msg;
                    if (ok)
                    {
                        ModAPI.Notify(msg);
                        _screen = ScreenKind.Skins;
                    }
                    if (IsOpen) RebuildBody();
                    if (!ok) ShowNotify(msg ?? "Upload failed.");
                });
        }

        static void BuildWorkshopBrowser()
        {
            SetWorldClickMode(false);
            float bodyH = PanelH - TitleH - 3f;
            CreateNavBar(_body, "Back", () =>
            {
                _screen = ScreenKind.Skins;
                RebuildBody();
            }, "Refresh", () =>
            {
                _workshopRemote = null;
                _workshopStatus = null;
                _workshopLoading = false;
                RebuildBody();
            });

            if (_workshopLoading)
            {
                CreateLabel(_body.GetComponent<RectTransform>(), "Querying Steam Workshop...", new Vector2(0f, -28f), 2.2f);
                return;
            }

            if (_workshopRemote == null)
            {
                if (!DbpuWorkshop.SteamReady)
                {
                    CreateLabel(_body.GetComponent<RectTransform>(),
                        "Steam is not available.\nStart PPG through Steam to browse Workshop.",
                        new Vector2(0f, -28f), 2.1f);
                    return;
                }
                _workshopLoading = true;
                DbpuWorkshop.BeginQueryRemote(DbpuWorkshopContentType.Skin, _workshopPage, null, (ok, msg, items, total) =>
                {
                    _workshopLoading = false;
                    _workshopStatus = msg;
                    _workshopRemote = items ?? new List<DbpuWorkshopRemoteItem>();
                    if (IsOpen) RebuildBody();
                });
                CreateLabel(_body.GetComponent<RectTransform>(), "Querying Steam Workshop...", new Vector2(0f, -28f), 2.2f);
                return;
            }

            CreateScroll(_body, new Vector2(0f, -NavH), new Vector2(PanelW - 6f, bodyH - NavH - 2f), out Transform content);
            float y = -6f;
            if (!string.IsNullOrEmpty(_workshopStatus))
            {
                CreateLabel(content.GetComponent<RectTransform>(), _workshopStatus, new Vector2(0f, y), 1.8f);
                y -= 8f;
            }
            if (_workshopRemote.Count == 0)
            {
                CreateLabel(content.GetComponent<RectTransform>(), "No skin packs found.", new Vector2(0f, y), 2.0f);
                y -= 10f;
            }
            for (int i = 0; i < _workshopRemote.Count; i++)
            {
                var item = _workshopRemote[i];
                string title = item?.Title ?? "Skin";
                var go = new GameObject("WItem");
                go.transform.SetParent(content, false);
                var rt = go.AddComponent<RectTransform>();
                AnchorTopCenter(rt);
                rt.anchoredPosition = new Vector2(0f, y);
                rt.sizeDelta = new Vector2(PanelW - 16f, 10f);
                go.AddComponent<Image>().color = CellBg;
                var labelGo = new GameObject("T");
                labelGo.transform.SetParent(go.transform, false);
                var lrt = labelGo.AddComponent<RectTransform>();
                lrt.anchorMin = Vector2.zero;
                lrt.anchorMax = Vector2.one;
                lrt.offsetMin = new Vector2(2f, 0f);
                lrt.offsetMax = new Vector2(-28f, 0f);
                var tmp = labelGo.AddComponent<TextMeshProUGUI>();
                tmp.text = title;
                tmp.font = Font;
                tmp.fontSize = 1.9f;
                tmp.alignment = TextAlignmentOptions.MidlineLeft;
                tmp.color = Color.white;
                tmp.raycastTarget = false;
                CreateTinyAction(go.transform, "Get", new Vector2((PanelW - 16f) * 0.5f - 10f, 0f), 16f, () => InstallWorkshopRemoteSkin(item));
                y -= 12f;
            }
            content.GetComponent<RectTransform>().sizeDelta = new Vector2(PanelW - 8f, Mathf.Max(40f, Mathf.Abs(y) + 8f));
        }

        static void InstallWorkshopRemoteSkin(DbpuWorkshopRemoteItem item)
        {
            if (item == null) return;
            DialogBoxManager.Notification("Subscribing / downloading \"" + (item.Title ?? "item") + "\"...");
            DbpuWorkshop.BeginSubscribeAndInstallSkin(
                item,
                _characterBasePath,
                _targetSkins,
                (ok, msg, packFolder) =>
                {
                    if (!ok)
                    {
                        DialogBoxManager.Notification(msg ?? "Install failed.");
                        return;
                    }
                    ModAPI.Notify(msg ?? "Installed");
                    if (!string.IsNullOrEmpty(packFolder))
                        OpenEditorForPack(packFolder);
                    else if (IsOpen)
                        RebuildBody();
                });
        }

        static void PromptString(string value, Action<string> apply)
        {
            HideMakerUi();
            DialogBox box = null;
            box = DialogBoxManager.TextEntry(
                "Set Text",
                string.IsNullOrEmpty(value) ? "Type here..." : value,
                new DialogButton("OK", true, new UnityAction[]
                {
                    () =>
                    {
                        string result = box != null ? (box.EnteredText ?? "") : "";
                        ShowMakerUi();
                        apply?.Invoke(result);
                    }
                }),
                new DialogButton("Cancel", true, new UnityAction[]
                {
                    () =>
                    {
                        ShowMakerUi();
                        RebuildBody();
                    }
                }));
            if (box != null) box.EnteredText = value ?? "";
        }
    }
}
