using Mod;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;
using static FunLittleGames.Linkage.Linkage;
using Random = UnityEngine.Random;
using HarmonyLib;

namespace DBPU
{
    public partial class DBPU
    {

        public static class DbpuCustomSkinLoader
        {
            static readonly HashSet<string> ReservedFolderNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Custom skins", "SkinIllustrations", "Skins", "Icons", "Illustrations", "HeadSkins", "Belts",
                "Drip", "Drip MUI", "Nerfed", "Saitama Skin", "Funny", "Black Goku Black",
                "Goku Black", "Bob", "Ultra Brickstinct", "SuperFit", "Outfits", "DBZ"
            };

            static readonly HashSet<string> SecretVariantFolderNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Drip", "Drip MUI", "Nerfed", "Saitama Skin", "Funny", "Black Goku Black",
                "Goku Black", "Bob", "Ultra Brickstinct"
            };

            static readonly Dictionary<string, List<CachedFolderSkin>> ScanCache = new Dictionary<string, List<CachedFolderSkin>>(StringComparer.OrdinalIgnoreCase);
            static Sprite placeholderIllustration;

            public class CachedFolderSkin
            {
                public string FolderName;
                public string SkinModPath;
                public string CharacterBasePath;
                public string HeadFile;
                public Dictionary<string, List<Sprite>> DamageVariants;
                public Dictionary<string, string> DamageModPaths;
                public Dictionary<string, List<Sprite>> BuffedDamageVariants;
                public Dictionary<string, string> BuffedDamageModPaths;
                public Dictionary<string, List<Sprite>> Buffed2DamageVariants;
                public Dictionary<string, string> Buffed2DamageModPaths;
                public List<CharacterSkins.Skins.PhyAccessoryData> PhyAccessories;
                public bool SpritesLoaded;
            }

            public static readonly string[] LimbFileNames =
            {
                "Head.png", "Upper.png", "Middle.png", "Lower.png",
                "ULeg.png", "LLeg.png", "Foot.png", "UArm.png", "LArm.png"
            };

            static string ResolveModPath(string modRelativePath)
            {
                return Path.Combine(ModFolder, modRelativePath.Replace('/', Path.DirectorySeparatorChar).TrimEnd(Path.DirectorySeparatorChar));
            }

            static string NormalizeModPath(string path)
            {
                if (string.IsNullOrEmpty(path)) return path;
                return path.Replace('\\', '/').TrimEnd('/') + "/";
            }

            static bool HasUpperPng(string folderPath)
            {
                return File.Exists(Path.Combine(folderPath, "Upper.png"));
            }

            static bool IsReservedSkinFolderName(string folderName)
            {
                if (string.IsNullOrEmpty(folderName)) return true;
                if (ReservedFolderNames.Contains(folderName)) return true;
                if (folderName.StartsWith("Thingy", StringComparison.OrdinalIgnoreCase)) return true;
                if (folderName.EndsWith("_Buffed", StringComparison.OrdinalIgnoreCase)) return true;
                if (folderName.EndsWith("_Buffed2", StringComparison.OrdinalIgnoreCase)) return true;
                if (Regex.IsMatch(folderName, @"^Damage\d+$", RegexOptions.IgnoreCase)) return true;
                return false;
            }

            static bool IsSkinPackFolder(string folderName, string folderPath)
            {
                if (IsReservedSkinFolderName(folderName)) return false;
                return HasUpperPng(folderPath);
            }

            static string ResolveHeadFile(string folderPath, string preferredHeadFile)
            {
                if (File.Exists(Path.Combine(folderPath, preferredHeadFile))) return preferredHeadFile;
                if (File.Exists(Path.Combine(folderPath, "Face.png"))) return "Face.png";
                if (File.Exists(Path.Combine(folderPath, "Head.png"))) return "Head.png";
                return preferredHeadFile;
            }

            static Sprite LoadSpriteSafe(string modRelativePath)
            {
                if (string.IsNullOrEmpty(modRelativePath)) return null;
                try
                {
                    string abs = ResolveModPath(modRelativePath);
                    if (!string.IsNullOrEmpty(abs) && File.Exists(abs))
                        return CustomTransformationCatalog.LoadSpriteFromDisk(abs);
                }
                catch { }

                try
                {
                    Sprite spr = CustomTransformationCatalog.LoadSpriteFlexible(modRelativePath);
                    if (spr != null) return spr;
                }
                catch { }

                return null;
            }

            static Sprite CloneSpriteIndependent(Sprite src)
            {
                if (src == null || src.texture == null) return src;
                try
                {
                    var tex = src.texture;
                    var rect = src.textureRect;
                    int w = Mathf.Max(1, (int)rect.width);
                    int h = Mathf.Max(1, (int)rect.height);
                    Texture2D copy;
                    if (tex.isReadable)
                    {
                        copy = new Texture2D(w, h, TextureFormat.RGBA32, false);
                        copy.SetPixels(tex.GetPixels((int)rect.x, (int)rect.y, w, h));
                        copy.Apply(false, false);
                    }
                    else
                    {
                        var rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32);
                        Graphics.Blit(tex, rt);
                        var prev = RenderTexture.active;
                        RenderTexture.active = rt;
                        copy = new Texture2D(w, h, TextureFormat.RGBA32, false);
                        copy.ReadPixels(new Rect(rect.x, rect.y, w, h), 0, 0);
                        copy.Apply(false, false);
                        RenderTexture.active = prev;
                        RenderTexture.ReleaseTemporary(rt);
                    }
                    copy.filterMode = FilterMode.Point;
                    copy.wrapMode = TextureWrapMode.Clamp;
                    return Sprite.Create(copy, new Rect(0, 0, copy.width, copy.height), new Vector2(0.5f, 0.5f), src.pixelsPerUnit > 0.01f ? src.pixelsPerUnit : 35f);
                }
                catch
                {
                    return src;
                }
            }

            static List<Sprite> LoadLimbSpritesFromDisk(string limbModFolder, string characterBasePath, string headFile)
            {
                string limbFsPath = ResolveModPath(limbModFolder);
                string resolvedHeadFile = ResolveHeadFile(limbFsPath, headFile);
                string headModPath;
                bool usedPackHead = File.Exists(Path.Combine(limbFsPath, resolvedHeadFile));
                if (usedPackHead)
                {
                    headModPath = NormalizeModPath(limbModFolder) + resolvedHeadFile;
                }
                else
                {
                    string baseModFolder = NormalizeModPath(characterBasePath);
                    string baseHeadFile = ResolveHeadFile(ResolveModPath(baseModFolder), headFile);
                    headModPath = baseModFolder + baseHeadFile;
                }

                Sprite headSpr = LoadSpriteSafe(headModPath);
                if (!usedPackHead && headSpr != null)
                    headSpr = CloneSpriteIndependent(headSpr);

                string limb = NormalizeModPath(limbModFolder);
                return new List<Sprite>
                {
                    headSpr,
                    LoadSpriteSafe(limb + "Upper.png"),
                    LoadSpriteSafe(limb + "Middle.png"),
                    LoadSpriteSafe(limb + "Lower.png"),
                    LoadSpriteSafe(limb + "ULeg.png"),
                    LoadSpriteSafe(limb + "LLeg.png"),
                    LoadSpriteSafe(limb + "Foot.png"),
                    LoadSpriteSafe(limb + "ULeg.png"),
                    LoadSpriteSafe(limb + "LLeg.png"),
                    LoadSpriteSafe(limb + "Foot.png"),
                    LoadSpriteSafe(limb + "UArm.png"),
                    LoadSpriteSafe(limb + "LArm.png"),
                    LoadSpriteSafe(limb + "UArm.png"),
                    LoadSpriteSafe(limb + "LArm.png")
                };
            }

            static List<Sprite> LoadSkinLimbSprites(string limbModFolder, string characterBasePath, string headFile)
            {
                return LoadLimbSpritesFromDisk(limbModFolder, characterBasePath, headFile);
            }

            static bool IsBuffedSubfolderName(string folderName)
            {
                if (string.IsNullOrEmpty(folderName)) return false;
                return folderName.Equals("_Buffed", StringComparison.OrdinalIgnoreCase)
                    || folderName.Equals("_Buffed2", StringComparison.OrdinalIgnoreCase);
            }

            static bool IsDamageNamedFolder(string folderName)
            {
                return !string.IsNullOrEmpty(folderName)
                    && Regex.IsMatch(folderName, @"^Damage\d+$", RegexOptions.IgnoreCase);
            }

            static bool IsVariantSubfolder(string folderName, string folderPath)
            {
                if (string.IsNullOrEmpty(folderName)) return false;
                if (ReservedFolderNames.Contains(folderName)) return false;
                if (folderName.StartsWith("Thingy", StringComparison.OrdinalIgnoreCase)) return false;
                if (IsBuffedSubfolderName(folderName)) return false;
                if (folderName.EndsWith("_Buffed", StringComparison.OrdinalIgnoreCase)) return false;
                if (folderName.EndsWith("_Buffed2", StringComparison.OrdinalIgnoreCase)) return false;
                return HasUpperPng(folderPath);
            }

            static List<string> GetVariantSubfolders(string skinFolderPath)
            {
                if (!Directory.Exists(skinFolderPath)) return new List<string>();

                return Directory.GetDirectories(skinFolderPath)
                    .Where(dir => IsVariantSubfolder(Path.GetFileName(dir), dir))
                    .OrderBy(dir =>
                    {
                        string name = Path.GetFileName(dir);
                        if (IsDamageNamedFolder(name)
                            && int.TryParse(name.Substring("Damage".Length), out int n))
                            return n;
                        return 1000;
                    })
                    .ThenBy(dir => Path.GetFileName(dir), StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            static Dictionary<string, List<Sprite>> BuildDamageVariants(string skinModPath, string skinFolderPath, string characterBasePath, string headFile)
            {
                var damageVariants = new Dictionary<string, List<Sprite>>();
                var variantDirs = GetVariantSubfolders(skinFolderPath);
                bool rootHasLimbs = HasUpperPng(skinFolderPath);
                string normalizedSkinModPath = NormalizeModPath(skinModPath);

                if (rootHasLimbs)
                {
                    damageVariants["Damage0"] = LoadSkinLimbSprites(normalizedSkinModPath, characterBasePath, headFile);
                    int fallbackIndex = 1;
                    for (int i = 0; i < variantDirs.Count; i++)
                    {
                        string variantName = Path.GetFileName(variantDirs[i]);
                        string variantModPath = normalizedSkinModPath + variantName + "/";
                        string key;
                        if (IsDamageNamedFolder(variantName))
                            key = "Damage" + variantName.Substring("Damage".Length);
                        else
                            key = "Damage" + (fallbackIndex++);
                        if (damageVariants.ContainsKey(key))
                            key = "Damage" + (damageVariants.Count);
                        damageVariants[key] = LoadSkinLimbSprites(variantModPath, characterBasePath, headFile);
                    }
                }
                else if (variantDirs.Count > 0)
                {
                    for (int i = 0; i < variantDirs.Count; i++)
                    {
                        string variantName = Path.GetFileName(variantDirs[i]);
                        string variantModPath = normalizedSkinModPath + variantName + "/";
                        string key = IsDamageNamedFolder(variantName)
                            ? "Damage" + variantName.Substring("Damage".Length)
                            : "Damage" + i;
                        damageVariants[key] = LoadSkinLimbSprites(variantModPath, characterBasePath, headFile);
                    }
                }

                return damageVariants;
            }

            static Sprite GetPlaceholderIllustration()
            {
                if (placeholderIllustration != null) return placeholderIllustration;

                var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                var cols = new Color[16];
                for (int i = 0; i < cols.Length; i++) cols[i] = Color.white;
                tex.SetPixels(cols);
                tex.Apply();
                placeholderIllustration = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
                return placeholderIllustration;
            }

            static Sprite GetIllustrationForDamage(string damageKey, Dictionary<string, List<Sprite>> damageVariants, Dictionary<string, string> damageModPaths, string characterBasePath, string headFile, string packFsPath = null)
            {
                if (!string.IsNullOrEmpty(packFsPath))
                {
                    string illDir = Path.Combine(packFsPath, "Illustrations");
                    string illFile = Path.Combine(illDir, damageKey + ".png");
                    if (File.Exists(illFile))
                    {
                        try
                        {
                            return CustomTransformationCatalog.LoadSpriteFromDisk(illFile);
                        }
                        catch { }
                    }
                }

                if (damageVariants != null && damageVariants.TryGetValue(damageKey, out var sprites) && sprites != null && sprites.Count > 0 && sprites[0] != null)
                    return sprites[0];

                if (damageModPaths != null && damageModPaths.TryGetValue(damageKey, out var modPath))
                {
                    string fsPath = ResolveModPath(modPath);
                    foreach (var fileName in new[] { "Upper.png", "Face.png", "Head.png" })
                    {
                        if (File.Exists(Path.Combine(fsPath, fileName)))
                            return LoadSpriteSafe(NormalizeModPath(modPath) + fileName);
                    }
                }

                string baseFsPath = ResolveModPath(characterBasePath);
                foreach (var fileName in new[] { ResolveHeadFile(baseFsPath, headFile), "Upper.png" })
                {
                    if (File.Exists(Path.Combine(baseFsPath, fileName)))
                        return LoadSpriteSafe(NormalizeModPath(characterBasePath) + fileName);
                }

                return GetPlaceholderIllustration();
            }

            static Dictionary<string, Sprite> LoadHairSpritesForPack(string packFs, Dictionary<string, string> damageModPaths)
            {
                var result = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
                if (string.IsNullOrEmpty(packFs) || !Directory.Exists(packFs)) return result;

                void TryAdd(string damageKey, string folderFs)
                {
                    if (string.IsNullOrEmpty(damageKey) || string.IsNullOrEmpty(folderFs)) return;
                    string hairPath = Path.Combine(folderFs, "Hair.png");
                    if (!File.Exists(hairPath)) return;
                    try
                    {
                        var spr = CustomTransformationCatalog.LoadSpriteFromDisk(hairPath);
                        if (spr != null) result[damageKey] = spr;
                    }
                    catch { }
                }

                TryAdd("Damage0", packFs);
                if (damageModPaths != null)
                {
                    foreach (var kvp in damageModPaths)
                    {
                        if (string.IsNullOrEmpty(kvp.Key) || string.IsNullOrEmpty(kvp.Value)) continue;
                        if (result.ContainsKey(kvp.Key)) continue;
                        TryAdd(kvp.Key, ResolveModPath(kvp.Value));
                    }
                }

                foreach (var dir in Directory.GetDirectories(packFs))
                {
                    string name = Path.GetFileName(dir);
                    if (!IsDamageNamedFolder(name)) continue;
                    string key = "Damage" + name.Substring("Damage".Length);
                    if (!result.ContainsKey(key))
                        TryAdd(key, dir);
                }

                return result;
            }

            static Dictionary<string, string> BuildDamageModPaths(string skinModPath, string skinFolderPath)
            {
                var damageModPaths = new Dictionary<string, string>();
                var variantDirs = GetVariantSubfolders(skinFolderPath);
                bool rootHasLimbs = HasUpperPng(skinFolderPath);
                string normalizedSkinModPath = NormalizeModPath(skinModPath);

                if (rootHasLimbs)
                {
                    damageModPaths["Damage0"] = normalizedSkinModPath;
                    int fallbackIndex = 1;
                    for (int i = 0; i < variantDirs.Count; i++)
                    {
                        string variantName = Path.GetFileName(variantDirs[i]);
                        string key;
                        if (IsDamageNamedFolder(variantName))
                            key = "Damage" + variantName.Substring("Damage".Length);
                        else
                            key = "Damage" + (fallbackIndex++);
                        if (damageModPaths.ContainsKey(key))
                            key = "Damage" + damageModPaths.Count;
                        damageModPaths[key] = normalizedSkinModPath + variantName + "/";
                    }
                }
                else
                {
                    for (int i = 0; i < variantDirs.Count; i++)
                    {
                        string variantName = Path.GetFileName(variantDirs[i]);
                        string key = IsDamageNamedFolder(variantName)
                            ? "Damage" + variantName.Substring("Damage".Length)
                            : "Damage" + i;
                        damageModPaths[key] = normalizedSkinModPath + variantName + "/";
                    }
                }

                return damageModPaths;
            }

            static List<CharacterSkins.Skins.PhyAccessoryData> LoadBeltsFromPack(string packFsPath, PersonBehaviour personHint)
            {
                var result = new List<CharacterSkins.Skins.PhyAccessoryData>();
                string beltsDir = Path.Combine(packFsPath, "Belts");
                string jsonPath = Path.Combine(beltsDir, "belt.json");
                if (!Directory.Exists(beltsDir) || !File.Exists(jsonPath)) return result;

                try
                {
                    var list = ModAPI.DeserialiseJSON<List<SkinBeltDto>>(jsonPath);
                    if (list == null) return result;

                    Transform lower = null;
                    if (personHint != null && personHint.Limbs != null)
                    {
                        for (int i = 0; i < personHint.Limbs.Length; i++)
                        {
                            if (personHint.Limbs[i] != null && personHint.Limbs[i].name == "LowerBody")
                            {
                                lower = personHint.Limbs[i].transform;
                                break;
                            }
                        }
                    }

                    foreach (var dto in list)
                    {
                        if (dto == null || string.IsNullOrEmpty(dto.SpriteFile)) continue;
                        string sprPath = Path.Combine(beltsDir, dto.SpriteFile);
                        if (!File.Exists(sprPath)) continue;
                        var spr = CustomTransformationCatalog.LoadSpriteFromDisk(sprPath);
                        if (spr == null) continue;

                        result.Add(new CharacterSkins.Skins.PhyAccessoryData
                        {
                            objForAttach = lower,
                            connectedAnchor = new Vector2(dto.AnchorX, dto.AnchorY),
                            layerName = string.IsNullOrEmpty(dto.LayerName) ? "Foreground" : dto.LayerName,
                            order = dto.Order,
                            sprites = new[] { spr },
                            minAngleFirstPiece = dto.MinAngleFirstPiece,
                            maxAngleFirstPiece = dto.MaxAngleFirstPiece,
                            minAngle = dto.MinAngle,
                            maxAngle = dto.MaxAngle,
                            pieceMass = dto.PieceMass > 0f ? dto.PieceMass : 0.0018f,
                            pieceInertia = dto.PieceInertia > 0f ? dto.PieceInertia : 0.001f,
                            Name = string.IsNullOrEmpty(dto.Name) ? "Belt" : dto.Name,
                            HasCollision = dto.HasCollision,
                            CollideJustWithGround = dto.CollideJustWithGround
                        });
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("Failed loading Belts/belt.json: " + ex.Message);
                }

                return result;
            }

            public class SkinBeltDto
            {
                public string Name = "Belt";
                public string SpriteFile;
                public float AnchorX = 0.1285f;
                public float AnchorY = 0.1286f;
                public string LayerName = "Foreground";
                public int Order = 1;
                public float MinAngleFirstPiece = -15f;
                public float MaxAngleFirstPiece = 32f;
                public float MinAngle = -17f;
                public float MaxAngle = 25f;
                public float PieceMass = 0.0018f;
                public float PieceInertia = 0.001f;
                public bool HasCollision = true;
                public bool CollideJustWithGround = true;
            }

            static List<CachedFolderSkin> ScanFolderSkins(string characterBasePath, string headFile)
            {
                characterBasePath = NormalizeModPath(characterBasePath);
                string cacheKey = characterBasePath + "|" + headFile;
                if (ScanCache.TryGetValue(cacheKey, out var cachedList)) return cachedList;

                var discovered = new List<CachedFolderSkin>();
                string baseFsPath = ResolveModPath(characterBasePath);
                if (!Directory.Exists(baseFsPath))
                {
                    ScanCache[cacheKey] = discovered;
                    return discovered;
                }

                var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var skinEntries = new List<(string name, string modPath, string fsPath)>();

                string customSkinsFsPath = Path.Combine(baseFsPath, "Custom skins");
                if (Directory.Exists(customSkinsFsPath))
                {
                    foreach (var dir in Directory.GetDirectories(customSkinsFsPath))
                    {
                        string folderName = Path.GetFileName(dir);
                        if (!IsSkinPackFolder(folderName, dir) || !seenNames.Add(folderName)) continue;
                        skinEntries.Add((folderName, characterBasePath + "Custom skins/" + folderName + "/", dir));
                    }
                }

                foreach (var entry in skinEntries)
                {
                    var damageModPaths = BuildDamageModPaths(entry.modPath, entry.fsPath);
                    if (damageModPaths.Count == 0) continue;

                    var folderSkin = new CachedFolderSkin
                    {
                        FolderName = entry.name,
                        SkinModPath = NormalizeModPath(entry.modPath),
                        CharacterBasePath = characterBasePath,
                        HeadFile = headFile,
                        DamageVariants = null,
                        DamageModPaths = damageModPaths,
                        PhyAccessories = null,
                        SpritesLoaded = false
                    };

                    string buffedFs = Path.Combine(entry.fsPath, "_Buffed");
                    if (Directory.Exists(buffedFs) && HasUpperPng(buffedFs))
                    {
                        string buffedMod = NormalizeModPath(entry.modPath) + "_Buffed/";
                        folderSkin.BuffedDamageModPaths = BuildDamageModPaths(buffedMod, buffedFs);
                    }

                    string buffed2Fs = Path.Combine(entry.fsPath, "_Buffed2");
                    if (Directory.Exists(buffed2Fs) && HasUpperPng(buffed2Fs))
                    {
                        string buffed2Mod = NormalizeModPath(entry.modPath) + "_Buffed2/";
                        folderSkin.Buffed2DamageModPaths = BuildDamageModPaths(buffed2Mod, buffed2Fs);
                    }

                    discovered.Add(folderSkin);
                }

                ScanCache[cacheKey] = discovered;
                return discovered;
            }

            static string PreferredSkinKeyFromFolder(string folderName)
            {
                if (string.IsNullOrEmpty(folderName)) return "CustomSkin";
                var chars = folderName.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray();
                return chars.Length > 0 ? new string(chars) : "CustomSkin";
            }

            static string PackDisplayName(string folderName)
            {
                return string.IsNullOrEmpty(folderName) ? "" : folderName.Replace('_', ' ');
            }

            static bool SkinInfoMatchesPack(CharacterSkins.Skins.SkinInfo info, string packFolderName)
            {
                if (info == null || string.IsNullOrEmpty(packFolderName)) return false;
                string display = PackDisplayName(packFolderName);
                string dn = info.DisplayName ?? "";
                if (string.Equals(dn, display, StringComparison.OrdinalIgnoreCase)) return true;
                if (string.Equals(dn.Replace(' ', '_'), packFolderName.Replace(' ', '_'), StringComparison.OrdinalIgnoreCase))
                    return true;
                string preferred = PreferredSkinKeyFromFolder(packFolderName);
                string name = info.Name ?? "";
                return string.Equals(name, preferred, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(name, "Custom_" + preferred, StringComparison.OrdinalIgnoreCase);
            }

            static bool PackFolderMatches(CharacterSkins.Skins.SkinInfo info, string packFolderName)
            {
                if (info == null || string.IsNullOrEmpty(packFolderName)) return false;
                if (string.IsNullOrEmpty(info.PackFolderName)) return false;
                return string.Equals(info.PackFolderName, packFolderName, StringComparison.OrdinalIgnoreCase);
            }

            public static string FindExistingKeyForPack(CharacterSkins characterSkins, string packFolderName)
            {
                if (characterSkins?.skins?.SkinData == null || string.IsNullOrEmpty(packFolderName)) return null;
                string preferred = PreferredSkinKeyFromFolder(packFolderName);
                string customKey = "Custom_" + preferred;
                if (characterSkins.skins.SkinData.TryGetValue(customKey, out var customInfo)
                    && PackFolderMatches(customInfo, packFolderName))
                    return customKey;
                if (characterSkins.skins.SkinData.TryGetValue(preferred, out var preferredInfo)
                    && PackFolderMatches(preferredInfo, packFolderName))
                    return preferred;
                foreach (var kvp in characterSkins.skins.SkinData)
                {
                    if (PackFolderMatches(kvp.Value, packFolderName))
                        return kvp.Key;
                }
                return null;
            }

            static string ResolveSkinKeyForPack(CharacterSkins characterSkins, string packFolderName, bool replace)
            {
                _ = replace;
                string preferred = PreferredSkinKeyFromFolder(packFolderName);
                string customKey = "Custom_" + preferred;
                if (characterSkins?.skins?.SkinData == null) return preferred;
                string existing = FindExistingKeyForPack(characterSkins, packFolderName);
                if (!string.IsNullOrEmpty(existing)) return existing;
                if (characterSkins.skins.SkinData.ContainsKey(customKey))
                    return customKey;
                if (characterSkins.skins.SkinData.ContainsKey(preferred))
                    return customKey;
                return preferred;
            }

            static string SanitizeSkinKey(string folderName, CharacterSkins characterSkins)
            {
                return ResolveSkinKeyForPack(characterSkins, folderName, true);
            }

            public static int RemovePackFromCharacter(CharacterSkins characterSkins, string packFolderName, string alsoKey = null)
            {
                if (characterSkins?.skins?.SkinData == null) return 0;
                var toRemove = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (!string.IsNullOrEmpty(alsoKey)) toRemove.Add(alsoKey);
                if (!string.IsNullOrEmpty(packFolderName))
                {
                    string preferred = PreferredSkinKeyFromFolder(packFolderName);
                    string customKey = "Custom_" + preferred;
                    if (characterSkins.skins.SkinData.TryGetValue(customKey, out var customInfo)
                        && PackFolderMatches(customInfo, packFolderName))
                        toRemove.Add(customKey);
                    if (characterSkins.skins.SkinData.TryGetValue(preferred, out var preferredInfo)
                        && PackFolderMatches(preferredInfo, packFolderName))
                        toRemove.Add(preferred);
                    foreach (var kvp in characterSkins.skins.SkinData)
                    {
                        if (PackFolderMatches(kvp.Value, packFolderName))
                            toRemove.Add(kvp.Key);
                    }
                }
                else if (!string.IsNullOrEmpty(alsoKey))
                {
                    toRemove.Add(alsoKey);
                    if (alsoKey.StartsWith("Custom_", StringComparison.OrdinalIgnoreCase))
                        toRemove.Add(alsoKey.Substring("Custom_".Length));
                    else
                        toRemove.Add("Custom_" + alsoKey);
                }
                int n = 0;
                foreach (var key in toRemove)
                {
                    if (characterSkins.skins.RemoveSkin(key)) n++;
                }
                return n;
            }

            static void PurgeDuplicateCustomPackKeys(CharacterSkins characterSkins)
            {
                if (characterSkins?.skins?.SkinData == null) return;
                var remove = new List<string>();
                foreach (var kvp in characterSkins.skins.SkinData)
                {
                    if (kvp.Key == null || !kvp.Key.StartsWith("Custom_", StringComparison.OrdinalIgnoreCase)) continue;
                    string baseKey = kvp.Key.Substring("Custom_".Length);
                    if (string.IsNullOrEmpty(baseKey) || !characterSkins.skins.SkinData.TryGetValue(baseKey, out var baseInfo))
                        continue;
                    var customInfo = kvp.Value;
                    if (baseInfo == null || customInfo == null) continue;
                    if (!string.Equals(baseInfo.DisplayName, customInfo.DisplayName, StringComparison.OrdinalIgnoreCase))
                        continue;
                    remove.Add(kvp.Key);
                }
                for (int i = 0; i < remove.Count; i++)
                    characterSkins.skins.SkinData.Remove(remove[i]);
            }

            static Dictionary<string, List<Sprite>> CreateDamageShell(Dictionary<string, string> damageModPaths)
            {
                var shell = new Dictionary<string, List<Sprite>>();
                if (damageModPaths == null) return shell;
                foreach (var key in damageModPaths.Keys)
                    shell[key] = new List<Sprite>();
                return shell;
            }

            static bool DamageMapLooksLoaded(Dictionary<string, List<Sprite>> damageMap)
            {
                if (damageMap == null || damageMap.Count == 0) return false;
                if (!damageMap.TryGetValue("Damage0", out var list) || list == null || list.Count < 2)
                    return false;
                return list[1] != null;
            }

            public static void EnsureFolderSkinSprites(CachedFolderSkin cachedSkin, PersonBehaviour personHint = null)
            {
                if (cachedSkin == null || cachedSkin.SpritesLoaded) return;
                string characterBasePath = cachedSkin.CharacterBasePath ?? "";
                string headFile = string.IsNullOrEmpty(cachedSkin.HeadFile) ? "Head.png" : cachedSkin.HeadFile;
                string packFs = ResolveModPath(cachedSkin.SkinModPath);

                if (cachedSkin.DamageVariants == null || cachedSkin.DamageVariants.Count == 0)
                    cachedSkin.DamageVariants = BuildDamageVariants(cachedSkin.SkinModPath, packFs, characterBasePath, headFile);

                if (cachedSkin.BuffedDamageModPaths != null && cachedSkin.BuffedDamageModPaths.Count > 0
                    && (cachedSkin.BuffedDamageVariants == null || cachedSkin.BuffedDamageVariants.Count == 0))
                {
                    string buffedFs = Path.Combine(packFs, "_Buffed");
                    string buffedMod = NormalizeModPath(cachedSkin.SkinModPath) + "_Buffed/";
                    cachedSkin.BuffedDamageVariants = BuildDamageVariants(buffedMod, buffedFs, characterBasePath, headFile);
                }

                if (cachedSkin.Buffed2DamageModPaths != null && cachedSkin.Buffed2DamageModPaths.Count > 0
                    && (cachedSkin.Buffed2DamageVariants == null || cachedSkin.Buffed2DamageVariants.Count == 0))
                {
                    string buffed2Fs = Path.Combine(packFs, "_Buffed2");
                    string buffed2Mod = NormalizeModPath(cachedSkin.SkinModPath) + "_Buffed2/";
                    cachedSkin.Buffed2DamageVariants = BuildDamageVariants(buffed2Mod, buffed2Fs, characterBasePath, headFile);
                }

                if (cachedSkin.PhyAccessories == null)
                    cachedSkin.PhyAccessories = LoadBeltsFromPack(packFs, personHint);

                cachedSkin.SpritesLoaded = cachedSkin.DamageVariants != null && cachedSkin.DamageVariants.Count > 0;
            }

            public static void EnsureSkinSpritesLoaded(CharacterSkins characterSkins, string skinKey, string characterBasePath = null, string headFile = "Head.png")
            {
                if (characterSkins?.skins?.SkinData == null || string.IsNullOrEmpty(skinKey)) return;
                if (!characterSkins.skins.SkinData.TryGetValue(skinKey, out var info) || info == null) return;
                bool limbsLoaded = info.Skins != null && info.Skins.TryGetValue(skinKey, out var forms)
                    && forms != null && forms.TryGetValue("None", out var damageMap)
                    && DamageMapLooksLoaded(damageMap);
                if (limbsLoaded)
                {
                    if (info.HairSprites == null && !string.IsNullOrEmpty(info.PackFolderName))
                    {
                        if (string.IsNullOrEmpty(characterBasePath))
                        {
                            var identityQuick = characterSkins.GetComponent<DbpuCharacterIdentity>();
                            if (identityQuick != null && !string.IsNullOrEmpty(identityQuick.SpriteFolder))
                                characterBasePath = NormalizeModPath(identityQuick.SpriteFolder);
                            var sourceQuick = characterSkins.GetComponent<DbpuCustomSkinSource>();
                            if (sourceQuick != null && !string.IsNullOrEmpty(sourceQuick.CharacterBasePath))
                                characterBasePath = NormalizeModPath(sourceQuick.CharacterBasePath);
                        }
                        if (!string.IsNullOrEmpty(characterBasePath))
                        {
                            string packFsQuick = GetPackFolderFs(characterBasePath, info.PackFolderName);
                            info.HairSprites = LoadHairSpritesForPack(packFsQuick, null);
                        }
                    }
                    return;
                }

                if (string.IsNullOrEmpty(characterBasePath))
                {
                    var source = characterSkins.GetComponent<DbpuCustomSkinSource>();
                    if (source != null && !string.IsNullOrEmpty(source.CharacterBasePath))
                    {
                        characterBasePath = source.CharacterBasePath;
                        if (!string.IsNullOrEmpty(source.HeadFile)) headFile = source.HeadFile;
                    }
                }
                if (string.IsNullOrEmpty(characterBasePath))
                {
                    var identity = characterSkins.GetComponent<DbpuCharacterIdentity>();
                    if (identity != null && !string.IsNullOrEmpty(identity.SpriteFolder))
                        characterBasePath = NormalizeModPath(identity.SpriteFolder);
                    else if (identity != null && !string.IsNullOrEmpty(identity.CharacterId))
                        characterBasePath = NormalizeModPath("Assets/Sprites/Characters/" + identity.CharacterId + "/");
                }
                if (string.IsNullOrEmpty(characterBasePath)) return;

                characterBasePath = NormalizeModPath(characterBasePath);
                var person = characterSkins.GetComponent<PersonBehaviour>();
                foreach (var cachedSkin in ScanFolderSkins(characterBasePath, headFile))
                {
                    string preferred = PreferredSkinKeyFromFolder(cachedSkin.FolderName);
                    string customKey = "Custom_" + preferred;
                    if (!string.Equals(preferred, skinKey, StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(customKey, skinKey, StringComparison.OrdinalIgnoreCase)
                        && !SkinInfoMatchesPack(info, cachedSkin.FolderName))
                        continue;

                    EnsureFolderSkinSprites(cachedSkin, person);
                    if (cachedSkin.DamageVariants == null || cachedSkin.DamageVariants.Count == 0) return;

                    var baseVariants = cachedSkin.DamageVariants;
                    var buffedVariants = cachedSkin.BuffedDamageVariants ?? baseVariants;
                    var buffed2Variants = cachedSkin.Buffed2DamageVariants ?? baseVariants;

                    if (info.Skins == null)
                        info.Skins = new Dictionary<string, Dictionary<string, Dictionary<string, List<Sprite>>>>();
                    info.Skins[skinKey] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", new Dictionary<string, List<Sprite>>(baseVariants) } };
                    info.Skins[skinKey + "_Buffed"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", new Dictionary<string, List<Sprite>>(buffedVariants) } };
                    info.Skins[skinKey + "_Buffed2"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", new Dictionary<string, List<Sprite>>(buffed2Variants) } };

                    if (info.Illustrations == null)
                        info.Illustrations = new Dictionary<string, Sprite>();
                    string packFs = ResolveModPath(cachedSkin.SkinModPath);
                    foreach (var damageKey in baseVariants.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
                    {
                        if (!info.Illustrations.ContainsKey(damageKey) || info.Illustrations[damageKey] == null
                            || info.Illustrations[damageKey] == placeholderIllustration)
                        {
                            info.Illustrations[damageKey] = GetIllustrationForDamage(
                                damageKey, baseVariants, cachedSkin.DamageModPaths, characterBasePath, headFile, packFs);
                        }
                    }

                    if (cachedSkin.PhyAccessories != null && cachedSkin.PhyAccessories.Count > 0)
                    {
                        info.PhyAccessories = cachedSkin.PhyAccessories;
                        WireBeltAttaches(info, person);
                    }
                    info.PackFolderName = cachedSkin.FolderName;
                    info.HairSprites = LoadHairSpritesForPack(packFs, cachedSkin.DamageModPaths);
                    return;
                }
            }

            static CharacterSkins.Skins.SkinInfo BuildSkinInfo(CachedFolderSkin cachedSkin, string skinKey, string characterBasePath, string headFile)
            {
                var baseShell = cachedSkin.DamageVariants != null && cachedSkin.DamageVariants.Count > 0
                    ? new Dictionary<string, List<Sprite>>(cachedSkin.DamageVariants)
                    : CreateDamageShell(cachedSkin.DamageModPaths);
                var buffedShell = cachedSkin.BuffedDamageVariants != null && cachedSkin.BuffedDamageVariants.Count > 0
                    ? new Dictionary<string, List<Sprite>>(cachedSkin.BuffedDamageVariants)
                    : (cachedSkin.BuffedDamageModPaths != null && cachedSkin.BuffedDamageModPaths.Count > 0
                        ? CreateDamageShell(cachedSkin.BuffedDamageModPaths)
                        : baseShell);
                var buffed2Shell = cachedSkin.Buffed2DamageVariants != null && cachedSkin.Buffed2DamageVariants.Count > 0
                    ? new Dictionary<string, List<Sprite>>(cachedSkin.Buffed2DamageVariants)
                    : (cachedSkin.Buffed2DamageModPaths != null && cachedSkin.Buffed2DamageModPaths.Count > 0
                        ? CreateDamageShell(cachedSkin.Buffed2DamageModPaths)
                        : baseShell);

                string packFsForHair = ResolveModPath(cachedSkin.SkinModPath);
                var skinInfo = new CharacterSkins.Skins.SkinInfo
                {
                    DisplayName = cachedSkin.FolderName.Replace('_', ' '),
                    Name = skinKey,
                    PackFolderName = cachedSkin.FolderName,
                    Skins = new Dictionary<string, Dictionary<string, Dictionary<string, List<Sprite>>>>(),
                    Illustrations = new Dictionary<string, Sprite>(),
                    PhyAccessories = cachedSkin.PhyAccessories,
                    HairSprites = LoadHairSpritesForPack(packFsForHair, cachedSkin.DamageModPaths)
                };

                skinInfo.Skins[skinKey] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", baseShell } };
                skinInfo.Skins[skinKey + "_Buffed"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", buffedShell } };
                skinInfo.Skins[skinKey + "_Buffed2"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", buffed2Shell } };

                string packFs = ResolveModPath(cachedSkin.SkinModPath);
                IEnumerable<string> keys = cachedSkin.DamageModPaths != null
                    ? (IEnumerable<string>)cachedSkin.DamageModPaths.Keys
                    : baseShell.Keys;
                foreach (var damageKey in keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
                {
                    skinInfo.Illustrations[damageKey] = GetIllustrationForDamage(
                        damageKey, cachedSkin.DamageVariants, cachedSkin.DamageModPaths, characterBasePath, headFile, packFs);
                }

                return skinInfo;
            }

            static CharacterSkins.Skins.SkinInfo BuildBaseSkinInfo(string characterBasePath, string headFile, string displayName)
            {
                string head = ResolveHeadFile(ResolveModPath(characterBasePath), headFile);
                var damageVariants = new Dictionary<string, List<Sprite>>
                {
                    { "Damage0", LoadLimbSpritesFromDisk(NormalizeModPath(characterBasePath), characterBasePath, head) }
                };

                var skinInfo = new CharacterSkins.Skins.SkinInfo
                {
                    DisplayName = displayName,
                    Name = "Base",
                    Skins = new Dictionary<string, Dictionary<string, Dictionary<string, List<Sprite>>>>(),
                    Illustrations = new Dictionary<string, Sprite>()
                };

                skinInfo.Skins["Base"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", damageVariants } };
                skinInfo.Skins["Base_Buffed"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", damageVariants } };
                skinInfo.Skins["Base_Buffed2"] = new Dictionary<string, Dictionary<string, List<Sprite>>> { { "None", damageVariants } };
                skinInfo.Illustrations["Damage0"] = GetIllustrationForDamage("Damage0", damageVariants, new Dictionary<string, string> { { "Damage0", NormalizeModPath(characterBasePath) } }, characterBasePath, headFile);
                return skinInfo;
            }

            public static void RegisterDiscoveredSkins(CharacterSkins characterSkins, string characterBasePath, string headFile = "Head.png")
            {
                if (characterSkins == null) return;
                if (characterSkins.skins == null)
                    characterSkins.skins = new CharacterSkins.Skins();

                characterBasePath = NormalizeModPath(characterBasePath);
                var source = characterSkins.GetComponent<DbpuCustomSkinSource>();
                if (source == null) source = characterSkins.gameObject.AddComponent<DbpuCustomSkinSource>();
                source.CharacterBasePath = characterBasePath;
                source.HeadFile = headFile;

                PurgeSecretVariantSkins(characterSkins);
                PurgeDuplicateCustomPackKeys(characterSkins);
                InvalidateScanCache(characterBasePath);
                foreach (var cachedSkin in ScanFolderSkins(characterBasePath, headFile))
                {
                    string existing = FindExistingKeyForPack(characterSkins, cachedSkin.FolderName);
                    string skinKey = !string.IsNullOrEmpty(existing)
                        ? existing
                        : ResolveSkinKeyForPack(characterSkins, cachedSkin.FolderName, false);
                    if (string.IsNullOrEmpty(skinKey))
                        skinKey = PreferredSkinKeyFromFolder(cachedSkin.FolderName);

                    var info = BuildSkinInfo(cachedSkin, skinKey, characterBasePath, headFile);
                    info.DisplayName = PackDisplayName(cachedSkin.FolderName);
                    WireBeltAttaches(info, characterSkins.GetComponent<PersonBehaviour>());
                    characterSkins.skins.UpsertSkin(skinKey, info);
                }
                PurgeDuplicateCustomPackKeys(characterSkins);
            }

            static void PurgeSecretVariantSkins(CharacterSkins characterSkins)
            {
                if (characterSkins?.skins?.SkinData == null) return;
                var remove = new List<string>();
                foreach (var kvp in characterSkins.skins.SkinData)
                {
                    if (kvp.Value == null) continue;
                    string key = kvp.Key ?? "";
                    string display = kvp.Value.DisplayName ?? "";
                    if (SecretVariantFolderNames.Contains(key) || SecretVariantFolderNames.Contains(display)
                        || SecretVariantFolderNames.Contains(key.Replace("Custom_", "")))
                        remove.Add(kvp.Key);
                }
                for (int i = 0; i < remove.Count; i++)
                    characterSkins.skins.SkinData.Remove(remove[i]);
            }

            static void WireBeltAttaches(CharacterSkins.Skins.SkinInfo info, PersonBehaviour person)
            {
                if (info?.PhyAccessories == null || person?.Limbs == null) return;
                Transform lower = null;
                for (int i = 0; i < person.Limbs.Length; i++)
                {
                    if (person.Limbs[i] != null && person.Limbs[i].name == "LowerBody")
                    {
                        lower = person.Limbs[i].transform;
                        break;
                    }
                }
                if (lower == null) return;
                for (int i = 0; i < info.PhyAccessories.Count; i++)
                {
                    if (info.PhyAccessories[i] != null && info.PhyAccessories[i].objForAttach == null)
                        info.PhyAccessories[i].objForAttach = lower;
                }
            }

            public static void InvalidateScanCache(string characterBasePath = null)
            {
                if (string.IsNullOrEmpty(characterBasePath))
                {
                    ScanCache.Clear();
                    return;
                }
                string prefix = NormalizeModPath(characterBasePath);
                var keys = ScanCache.Keys.Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();
                for (int i = 0; i < keys.Count; i++)
                    ScanCache.Remove(keys[i]);
            }

            public static string GetCustomSkinsFolderFs(string characterBasePath)
            {
                characterBasePath = NormalizeModPath(characterBasePath);
                return Path.Combine(ResolveModPath(characterBasePath), "Custom skins");
            }

            public static string GetPackFolderFs(string characterBasePath, string packFolderName)
            {
                return Path.Combine(GetCustomSkinsFolderFs(characterBasePath), packFolderName);
            }

            public static bool IsCustomPackFolder(string characterBasePath, string packFolderName)
            {
                string fs = GetPackFolderFs(characterBasePath, packFolderName);
                return Directory.Exists(fs) && HasUpperPng(fs);
            }

            public static List<CachedFolderSkin> ListFolderSkins(string characterBasePath, string headFile = "Head.png")
            {
                return new List<CachedFolderSkin>(ScanFolderSkins(NormalizeModPath(characterBasePath), headFile));
            }

            public static string UpsertPackIntoCharacter(CharacterSkins characterSkins, string characterBasePath, string packFolderName, string headFile = "Head.png", bool replace = true, string displayName = null)
            {
                if (characterSkins == null || string.IsNullOrEmpty(packFolderName)) return null;
                characterBasePath = NormalizeModPath(characterBasePath);
                InvalidateScanCache(characterBasePath);

                string packFs = GetPackFolderFs(characterBasePath, packFolderName);
                if (!Directory.Exists(packFs) || !HasUpperPng(packFs)) return null;

                try { CustomTransformationCatalog.InvalidateDiskSpriteCache(packFs); } catch { }

                string modPath = characterBasePath + "Custom skins/" + packFolderName + "/";
                var damageVariants = BuildDamageVariants(modPath, packFs, characterBasePath, headFile);
                if (damageVariants.Count == 0) return null;

                var cached = new CachedFolderSkin
                {
                    FolderName = packFolderName,
                    SkinModPath = NormalizeModPath(modPath),
                    CharacterBasePath = characterBasePath,
                    HeadFile = headFile,
                    DamageVariants = damageVariants,
                    DamageModPaths = BuildDamageModPaths(modPath, packFs),
                    PhyAccessories = LoadBeltsFromPack(packFs, characterSkins.GetComponent<PersonBehaviour>()),
                    SpritesLoaded = true
                };

                string buffedFs = Path.Combine(packFs, "_Buffed");
                if (Directory.Exists(buffedFs) && HasUpperPng(buffedFs))
                {
                    string buffedMod = NormalizeModPath(modPath) + "_Buffed/";
                    cached.BuffedDamageVariants = BuildDamageVariants(buffedMod, buffedFs, characterBasePath, headFile);
                    cached.BuffedDamageModPaths = BuildDamageModPaths(buffedMod, buffedFs);
                }

                string buffed2Fs = Path.Combine(packFs, "_Buffed2");
                if (Directory.Exists(buffed2Fs) && HasUpperPng(buffed2Fs))
                {
                    string buffed2Mod = NormalizeModPath(modPath) + "_Buffed2/";
                    cached.Buffed2DamageVariants = BuildDamageVariants(buffed2Mod, buffed2Fs, characterBasePath, headFile);
                    cached.Buffed2DamageModPaths = BuildDamageModPaths(buffed2Mod, buffed2Fs);
                }

                string preferredKey = PreferredSkinKeyFromFolder(packFolderName);
                string skinKey = ResolveSkinKeyForPack(characterSkins, packFolderName, replace);
                if (string.IsNullOrEmpty(skinKey)) skinKey = preferredKey;

                var info = BuildSkinInfo(cached, skinKey, characterBasePath, headFile);
                if (!string.IsNullOrEmpty(displayName))
                    info.DisplayName = displayName;
                else if (!string.IsNullOrEmpty(cached.FolderName))
                    info.DisplayName = PackDisplayName(cached.FolderName);
                WireBeltAttaches(info, characterSkins.GetComponent<PersonBehaviour>());
                characterSkins.skins.UpsertSkin(skinKey, info);
                PurgeDuplicateCustomPackKeys(characterSkins);

                try
                {
                    var personPb = characterSkins.GetComponent<PersonBehaviour>();
                    Transform headTf = null;
                    if (personPb != null && personPb.Limbs != null && personPb.Limbs.Length > 0 && personPb.Limbs[0] != null)
                        headTf = personPb.Limbs[0].transform;
                    if (headTf == null)
                        headTf = characterSkins.transform.Find("Head");
                    if (headTf != null)
                    {
                        var skinsCM = headTf.GetComponent<SkinContextMenu>();
                        if (skinsCM != null)
                        {
                            skinsCM.SkinsData = characterSkins;
                            skinsCM.JumpToSkin(skinKey);
                        }
                    }
                }
                catch { }

                return skinKey;
            }

            public static bool SetupCharacterFolderSkins(GameObject instance, Transform head, string characterBasePath, string headFile = "Head.png", string defaultSkinDisplayName = null)
            {
                characterBasePath = NormalizeModPath(characterBasePath);
                var folderSkins = ScanFolderSkins(characterBasePath, headFile);
                bool shouldRegisterBase = !string.IsNullOrEmpty(defaultSkinDisplayName);
                if (folderSkins.Count == 0 && !shouldRegisterBase) return false;

                var characterSkins = instance.GetComponent<CharacterSkins>();
                if (characterSkins == null) characterSkins = instance.AddComponent<CharacterSkins>();

                var skinsCM = head.GetComponent<SkinContextMenu>();
                if (skinsCM == null) skinsCM = head.gameObject.AddComponent<SkinContextMenu>();
                skinsCM.SkinsData = characterSkins;

                string firstSkinKey = null;

                if (!characterSkins.skins.SkinData.ContainsKey("Base"))
                {
                    if (shouldRegisterBase || folderSkins.Count > 0)
                    {
                        string baseDisplayName = defaultSkinDisplayName ?? "Base";
                        characterSkins.skins.AddSkin("Base", BuildBaseSkinInfo(characterBasePath, headFile, baseDisplayName));
                        firstSkinKey = "Base";
                    }
                }
                else
                {
                    firstSkinKey = "Base";
                }

                RegisterDiscoveredSkins(characterSkins, characterBasePath, headFile);

                if (firstSkinKey == null)
                {
                    foreach (var cachedSkin in folderSkins)
                    {
                        string skinKey = SanitizeSkinKey(cachedSkin.FolderName, characterSkins);
                        if (characterSkins.skins.SkinData.ContainsKey(skinKey))
                        {
                            firstSkinKey = skinKey;
                            break;
                        }
                    }
                }

                if (firstSkinKey == null && characterSkins.skins.SkinData.Count > 0)
                    firstSkinKey = characterSkins.skins.SkinData.Keys.First();

                if (!string.IsNullOrEmpty(firstSkinKey))
                {
                    skinsCM.CurrentSkin = firstSkinKey;
                    skinsCM.PreviousSkin = firstSkinKey;
                }

                return true;
            }
        }
    }
}
