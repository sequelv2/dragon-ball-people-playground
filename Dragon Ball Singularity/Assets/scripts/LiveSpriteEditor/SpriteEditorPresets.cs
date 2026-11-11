using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace DBPU.LiveSpriteEditor
{
    public enum SpritePresetBrowserMode
    {
        Library,
        BundleFiles,
        PngFiles
    }

    public enum SpritePresetItemKind
    {
        PresetBundle,
        ExternalBundle,
        ExternalPng
    }

    [Serializable]
    public sealed class SpritePresetBundleMetadata
    {
        public string name;
        public string version;
        public string targetKind;
        public string scope;
        public string sourceName;
        public string createdAt;
        public List<SpritePresetEntryMetadata> entries = new List<SpritePresetEntryMetadata>();
    }

    [Serializable]
    public sealed class SpritePresetEntryMetadata
    {
        public string partId;
        public string partLabel;
        public string variantKey;
        public int width;
        public int height;
        public int pivotX;
        public int pivotY;
        public float pixelsPerUnit;
        public string pngPath;
        public string fleshPngPath;
        public string bonePngPath;
        public string damagePngPath;
        public string layerStackPath;
        public bool includesHitbox;
    }

    public sealed class SpritePresetBrowserItem
    {
        public string DisplayName;
        public string Description;
        public string SourcePath;
        public string PreviewPath;
        public Texture2D PreviewTexture;
        public SpritePresetBrowserMode Mode;
        public SpritePresetItemKind ItemKind;
        public SpritePresetBundleMetadata Metadata;
    }

    public sealed class SpritePresetOperationResult
    {
        public bool Success;
        public string Message;
        public SpritePresetBrowserItem BrowserItem;
    }

    public sealed class SpriteClipboardEntry
    {
        public string PartId;
        public string PartLabel;
        public string VariantKey;
        public int Width;
        public int Height;
        public int PivotX;
        public int PivotY;
        public float PixelsPerUnit;
        public Color32[] Pixels;
        public int FleshWidth;
        public int FleshHeight;
        public int BoneWidth;
        public int BoneHeight;
        public int DamageWidth;
        public int DamageHeight;
        public Color32[] FleshPixels;
        public Color32[] BonePixels;
        public Color32[] DamagePixels;
        public LimbSpriteState.LayerStackSnapshot LayerStack;
    }

    public sealed class SpriteClipboardPayload
    {
        public string TargetKind;
        public string Scope;
        public string SourceName;
        public List<SpriteClipboardEntry> Entries = new List<SpriteClipboardEntry>();
    }

    public static class SpriteVariantKeyUtility
    {
        public static string GetKey(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null)
            {
                return string.Empty;
            }

            Texture2D readable = LimbSpriteState.CreateReadableTextureCopy(sprite.texture);
            if (readable == null)
            {
                return string.Empty;
            }

            Rect rect = sprite.rect;
            int x = Mathf.RoundToInt(rect.x);
            int y = Mathf.RoundToInt(rect.y);
            int width = Mathf.RoundToInt(rect.width);
            int height = Mathf.RoundToInt(rect.height);
            if (width <= 0 || height <= 0)
            {
                UnityEngine.Object.Destroy(readable);
                return string.Empty;
            }

            Color32[] pixels = readable.GetPixels32();
            int textureWidth = readable.width;
            ulong hash = 14695981039346656037UL;

            unchecked
            {
                hash ^= (ulong)width;
                hash *= 1099511628211UL;
                hash ^= (ulong)height;
                hash *= 1099511628211UL;

                for (int row = 0; row < height; row++)
                {
                    int rowStart = (y + row) * textureWidth + x;
                    for (int column = 0; column < width; column++)
                    {
                        Color32 pixel = pixels[rowStart + column];
                        hash ^= pixel.r;
                        hash *= 1099511628211UL;
                        hash ^= pixel.g;
                        hash *= 1099511628211UL;
                        hash ^= pixel.b;
                        hash *= 1099511628211UL;
                        hash ^= pixel.a;
                        hash *= 1099511628211UL;
                    }
                }
            }

            UnityEngine.Object.Destroy(readable);

            return string.Format(
                "{0:X16}:{1}:{2}:{3}:{4}:{5}:{6}:{7}",
                hash,
                x,
                y,
                width,
                height,
                Mathf.RoundToInt(sprite.pivot.x),
                Mathf.RoundToInt(sprite.pivot.y),
                Mathf.RoundToInt(sprite.pixelsPerUnit * 100f));
        }
    }

    public static class SpriteClipboardService
    {
        private static SpriteClipboardPayload partClipboard;
        private static SpriteClipboardPayload targetClipboard;
        private static SpriteClipboardPayload layerTargetClipboard;

        public static bool HasPartClipboard(bool forCharacter)
        {
            return partClipboard != null &&
                   string.Equals(partClipboard.TargetKind, forCharacter ? "character" : "object", StringComparison.OrdinalIgnoreCase) &&
                   partClipboard.Entries != null &&
                   partClipboard.Entries.Count > 0;
        }

        public static bool HasTargetClipboard(bool forCharacter)
        {
            return targetClipboard != null &&
                   string.Equals(targetClipboard.TargetKind, forCharacter ? "character" : "object", StringComparison.OrdinalIgnoreCase) &&
                   targetClipboard.Entries != null &&
                   targetClipboard.Entries.Count > 0;
        }

        public static bool HasLayerTargetClipboard(bool forCharacter)
        {
            return layerTargetClipboard != null &&
                   string.Equals(layerTargetClipboard.TargetKind, forCharacter ? "character" : "object", StringComparison.OrdinalIgnoreCase) &&
                   layerTargetClipboard.Entries != null &&
                   layerTargetClipboard.Entries.Count > 0;
        }

        public static SpritePresetOperationResult CopyCharacterPart(CharacterSpriteEditorTarget target, LimbBehaviour selectedLimb)
        {
            if (target == null)
            {
                return Failure("No character was available to copy from.");
            }

            LimbBehaviour limb = target.GetPreferredLimb(selectedLimb);
            LimbSpriteState state = target.GetState(limb);
            if (state == null || !state.IsValid)
            {
                return Failure("No editable limb was available to copy.");
            }

            SpriteClipboardPayload payload = new SpriteClipboardPayload();
            payload.TargetKind = "character";
            payload.Scope = "part";
            payload.SourceName = target.GetCharacterName();
            payload.Entries.Add(CreateClipboardEntry(state, target.GetPartId(limb), target.GetLimbDisplayName(limb)));
            partClipboard = payload;
            return Success("Copied " + target.GetLimbDisplayName(limb) + " to the clipboard.", null);
        }

        public static SpritePresetOperationResult CopyCharacterTarget(CharacterSpriteEditorTarget target)
        {
            if (target == null)
            {
                return Failure("No character was available to copy from.");
            }

            List<LimbBehaviour> limbs = target.GetAvailableLimbs();
            SpriteClipboardPayload payload = new SpriteClipboardPayload();
            payload.TargetKind = "character";
            payload.Scope = "target";
            payload.SourceName = target.GetCharacterName();

            for (int i = 0; i < limbs.Count; i++)
            {
                LimbBehaviour limb = limbs[i];
                LimbSpriteState state = target.GetState(limb);
                if (state == null || !state.IsValid)
                {
                    continue;
                }

                payload.Entries.Add(CreateClipboardEntry(state, target.GetPartId(limb), target.GetLimbDisplayName(limb)));
            }

            if (payload.Entries.Count <= 0)
            {
                return Failure("No editable limbs were available to copy.");
            }

            targetClipboard = payload;
            return Success("Copied " + payload.Entries.Count + " limbs from " + target.GetCharacterName() + ".", null);
        }

        public static SpritePresetOperationResult CopyObjectPart(ObjectSpriteEditorTarget target)
        {
            if (target == null)
            {
                return Failure("No object was available to copy from.");
            }

            LimbSpriteState state = target.GetState();
            if (state == null || !state.IsValid)
            {
                return Failure("No editable sprite was available to copy.");
            }

            SpriteClipboardPayload payload = new SpriteClipboardPayload();
            payload.TargetKind = "object";
            payload.Scope = "part";
            payload.SourceName = target.GetObjectName();
            payload.Entries.Add(CreateClipboardEntry(state, target.GetPartId(), target.GetObjectName()));
            partClipboard = payload;
            targetClipboard = payload;
            return Success("Copied " + target.GetObjectName() + " to the clipboard.", null);
        }

        public static SpritePresetOperationResult CopyObjectLayers(ObjectSpriteEditorTarget target)
        {
            if (target == null)
            {
                return Failure("No object was available to copy layers from.");
            }

            LimbSpriteState state = target.GetState();
            if (state == null || !state.IsValid)
            {
                return Failure("No editable sprite layers were available to copy.");
            }

            SpriteClipboardPayload payload = new SpriteClipboardPayload();
            payload.TargetKind = "object";
            payload.Scope = "layers";
            payload.SourceName = target.GetObjectName();
            payload.Entries.Add(CreateClipboardEntry(state, target.GetPartId(), target.GetObjectName()));
            layerTargetClipboard = payload;
            return Success("Copied layers from " + target.GetObjectName() + ".", null);
        }

        public static SpritePresetOperationResult CopyCharacterTargetLayers(CharacterSpriteEditorTarget target)
        {
            if (target == null)
            {
                return Failure("No character was available to copy layers from.");
            }

            List<LimbBehaviour> limbs = target.GetAvailableLimbs();
            SpriteClipboardPayload payload = new SpriteClipboardPayload();
            payload.TargetKind = "character";
            payload.Scope = "layers";
            payload.SourceName = target.GetCharacterName();

            for (int i = 0; i < limbs.Count; i++)
            {
                LimbBehaviour limb = limbs[i];
                LimbSpriteState state = target.GetState(limb);
                if (state == null || !state.IsValid)
                {
                    continue;
                }

                payload.Entries.Add(CreateClipboardEntry(state, target.GetPartId(limb), target.GetLimbDisplayName(limb)));
            }

            if (payload.Entries.Count <= 0)
            {
                return Failure("No editable layers were available to copy.");
            }

            layerTargetClipboard = payload;
            return Success("Copied layer stacks from " + payload.Entries.Count + " limbs.", null);
        }

        public static SpritePresetOperationResult PasteCharacterPart(CharacterSpriteEditorTarget target, LimbBehaviour selectedLimb)
        {
            if (target == null)
            {
                return Failure("No character target was available.");
            }

            if (!HasPartClipboard(true))
            {
                return Failure("Copy a limb sprite first.");
            }

            LimbBehaviour limb = target.GetPreferredLimb(selectedLimb);
            LimbSpriteState state = target.GetState(limb);
            if (state == null || !state.IsValid)
            {
                return Failure("No editable target limb was available.");
            }

            SpriteClipboardEntry entry = partClipboard.Entries[0];
            if (!HasBodyLayerData(entry))
            {
                return Failure("That copied limb is missing body-layer data. Copy it again with this version, then paste it.");
            }

            bool applied = state.ReplaceWorkingSprite(
                ClonePixels(entry.Pixels),
                entry.Width,
                entry.Height,
                entry.PivotX,
                entry.PivotY,
                entry.PixelsPerUnit,
                ClonePixels(entry.FleshPixels),
                entry.FleshWidth,
                entry.FleshHeight,
                ClonePixels(entry.BonePixels),
                entry.BoneWidth,
                entry.BoneHeight,
                ClonePixels(entry.DamagePixels),
                entry.DamageWidth,
                entry.DamageHeight);

            return applied
                ? Success("Pasted into " + target.GetLimbDisplayName(limb) + ".", null)
                : Failure("That copied limb could not be pasted onto " + target.GetLimbDisplayName(limb) + ".");
        }

        public static SpritePresetOperationResult PasteCharacterTarget(CharacterSpriteEditorTarget target, LimbBehaviour selectedLimb)
        {
            if (target == null)
            {
                return Failure("No character target was available.");
            }

            if (!HasTargetClipboard(true))
            {
                return Failure("Copy a full character first.");
            }

            int applied = 0;
            int skippedMissing = 0;
            int skippedFit = 0;
            LimbBehaviour selected = target.GetPreferredLimb(selectedLimb);

            for (int i = 0; i < targetClipboard.Entries.Count; i++)
            {
                SpriteClipboardEntry entry = targetClipboard.Entries[i];
                if (!HasBodyLayerData(entry))
                {
                    skippedFit++;
                    continue;
                }

                LimbBehaviour limb = FindCharacterLimb(target, entry);
                if (limb == null)
                {
                    skippedMissing++;
                    continue;
                }

                LimbSpriteState state = target.GetState(limb);
                if (state == null || !state.IsValid)
                {
                    skippedMissing++;
                    continue;
                }

                if (!state.ReplaceWorkingSprite(
                    ClonePixels(entry.Pixels),
                    entry.Width,
                    entry.Height,
                    entry.PivotX,
                    entry.PivotY,
                    entry.PixelsPerUnit,
                    ClonePixels(entry.FleshPixels),
                    entry.FleshWidth,
                    entry.FleshHeight,
                    ClonePixels(entry.BonePixels),
                    entry.BoneWidth,
                    entry.BoneHeight,
                    ClonePixels(entry.DamagePixels),
                    entry.DamageWidth,
                    entry.DamageHeight))
                {
                    skippedFit++;
                    continue;
                }

                if (limb != selected)
                {
                    state.FinalizeForWorldDisplay();
                }

                applied++;
            }

            return applied > 0
                ? Success("Pasted " + applied + " limbs. Skipped " + skippedMissing + " missing part, " + skippedFit + " failed paste.", null)
                : Failure("No copied limbs could be pasted onto this character.");
        }

        public static SpritePresetOperationResult PasteObjectPart(ObjectSpriteEditorTarget target)
        {
            if (target == null)
            {
                return Failure("No object target was available.");
            }

            if (!HasPartClipboard(false))
            {
                return Failure("Copy an object sprite first.");
            }

            LimbSpriteState state = target.GetState();
            if (state == null || !state.IsValid)
            {
                return Failure("No editable object sprite was available.");
            }

            SpriteClipboardEntry entry = partClipboard.Entries[0];
            bool applied = state.ReplaceWorkingSprite(
                ClonePixels(entry.Pixels),
                entry.Width,
                entry.Height,
                entry.PivotX,
                entry.PivotY,
                entry.PixelsPerUnit);

            return applied
                ? Success("Pasted into " + target.GetObjectName() + ".", null)
                : Failure("That copied sprite could not be pasted onto " + target.GetObjectName() + ".");
        }

        public static SpritePresetOperationResult PasteObjectLayers(ObjectSpriteEditorTarget target)
        {
            if (target == null)
            {
                return Failure("No object target was available.");
            }

            if (!HasLayerTargetClipboard(false))
            {
                return Failure("Copy object layers first.");
            }

            LimbSpriteState state = target.GetState();
            if (state == null || !state.IsValid)
            {
                return Failure("No editable object sprite was available.");
            }

            SpriteClipboardEntry entry = layerTargetClipboard.Entries[0];
            bool applied = state.PasteLayerStackSnapshot(entry.LayerStack, layerTargetClipboard.SourceName);
            return applied
                ? Success("Pasted copied layers over " + target.GetObjectName() + ".", null)
                : Failure("Those copied layers could not be pasted onto " + target.GetObjectName() + ".");
        }

        public static SpritePresetOperationResult PasteCharacterTargetLayers(CharacterSpriteEditorTarget target, LimbBehaviour selectedLimb)
        {
            if (target == null)
            {
                return Failure("No character target was available.");
            }

            if (!HasLayerTargetClipboard(true))
            {
                return Failure("Copy character layers first.");
            }

            int applied = 0;
            int skippedMissing = 0;
            int skippedFit = 0;
            LimbBehaviour selected = target.GetPreferredLimb(selectedLimb);

            for (int i = 0; i < layerTargetClipboard.Entries.Count; i++)
            {
                SpriteClipboardEntry entry = layerTargetClipboard.Entries[i];
                LimbBehaviour limb = FindCharacterLimb(target, entry);
                if (limb == null)
                {
                    skippedMissing++;
                    continue;
                }

                LimbSpriteState state = target.GetState(limb);
                if (state == null || !state.IsValid)
                {
                    skippedMissing++;
                    continue;
                }

                if (!state.PasteLayerStackSnapshot(entry.LayerStack, layerTargetClipboard.SourceName))
                {
                    skippedFit++;
                    continue;
                }

                if (limb != selected)
                {
                    state.FinalizeForWorldDisplay();
                }

                applied++;
            }

            return applied > 0
                ? Success("Pasted layer stacks onto " + applied + " limbs. Skipped " + skippedMissing + " missing part, " + skippedFit + " size mismatch.", null)
                : Failure("No copied layer stacks could be pasted onto this character.");
        }

        private static SpriteClipboardEntry CreateClipboardEntry(LimbSpriteState state, string partId, string partLabel)
        {
            SpriteClipboardEntry entry = new SpriteClipboardEntry();
            entry.PartId = partId ?? string.Empty;
            entry.PartLabel = partLabel ?? string.Empty;
            entry.VariantKey = state != null ? SpriteVariantKeyUtility.GetKey(state.OriginalSprite) : string.Empty;
            entry.Width = state != null ? state.Width : 0;
            entry.Height = state != null ? state.Height : 0;
            entry.PivotX = state != null && state.OriginalSprite != null ? Mathf.RoundToInt(state.OriginalSprite.pivot.x) : 0;
            entry.PivotY = state != null && state.OriginalSprite != null ? Mathf.RoundToInt(state.OriginalSprite.pivot.y) : 0;
            entry.PixelsPerUnit = state != null && state.OriginalSprite != null ? state.OriginalSprite.pixelsPerUnit : 1f;
            entry.Pixels = state != null ? state.GetCurrentPixelsSnapshot() : null;
            if (state != null)
            {
                entry.LayerStack = state.CaptureLayerStackSnapshot();

                Color32[] fleshPixels;
                int fleshWidth;
                int fleshHeight;
                Color32[] bonePixels;
                int boneWidth;
                int boneHeight;
                if (state.TryGetBodyLayerSnapshots(
                    out fleshPixels,
                    out fleshWidth,
                    out fleshHeight,
                    out bonePixels,
                    out boneWidth,
                    out boneHeight))
                {
                    entry.FleshPixels = ClonePixels(fleshPixels);
                    entry.FleshWidth = fleshWidth;
                    entry.FleshHeight = fleshHeight;
                    entry.BonePixels = ClonePixels(bonePixels);
                    entry.BoneWidth = boneWidth;
                    entry.BoneHeight = boneHeight;
                }

                Color32[] damagePixels;
                int damageWidth;
                int damageHeight;
                if (state.TryGetDamageLayerSnapshot(out damagePixels, out damageWidth, out damageHeight))
                {
                    entry.DamagePixels = ClonePixels(damagePixels);
                    entry.DamageWidth = damageWidth;
                    entry.DamageHeight = damageHeight;
                }
            }

            return entry;
        }

        private static LimbBehaviour FindCharacterLimb(CharacterSpriteEditorTarget target, SpriteClipboardEntry entry)
        {
            if (target == null || entry == null)
            {
                return null;
            }

            LimbBehaviour limb = target.FindLimbByPartId(entry.PartId);
            if (limb != null)
            {
                return limb;
            }

            List<LimbBehaviour> limbs = target.GetAvailableLimbs();
            for (int i = 0; i < limbs.Count; i++)
            {
                if (string.Equals(target.GetLimbDisplayName(limbs[i]), entry.PartLabel, StringComparison.Ordinal))
                {
                    return limbs[i];
                }
            }

            return null;
        }

        private static bool HasBodyLayerData(SpriteClipboardEntry entry)
        {
            return entry != null &&
                   entry.FleshPixels != null &&
                   entry.BonePixels != null &&
                   entry.FleshWidth > 0 &&
                   entry.FleshHeight > 0 &&
                   entry.BoneWidth > 0 &&
                   entry.BoneHeight > 0 &&
                   entry.FleshPixels.Length == entry.FleshWidth * entry.FleshHeight &&
                   entry.BonePixels.Length == entry.BoneWidth * entry.BoneHeight;
        }

        private static Color32[] ClonePixels(Color32[] pixels)
        {
            if (pixels == null)
            {
                return null;
            }

            Color32[] copy = new Color32[pixels.Length];
            Array.Copy(pixels, copy, pixels.Length);
            return copy;
        }

        private static SpritePresetOperationResult Success(string message, SpritePresetBrowserItem item)
        {
            SpritePresetOperationResult result = new SpritePresetOperationResult();
            result.Success = true;
            result.Message = message;
            result.BrowserItem = item;
            return result;
        }

        private static SpritePresetOperationResult Failure(string message)
        {
            SpritePresetOperationResult result = new SpritePresetOperationResult();
            result.Success = false;
            result.Message = message;
            return result;
        }
    }

    public static class SpritePresetLibraryService
    {
        private const string PresetVersion = "1.0.0";
        private const string PresetFileName = "preset.json";
        private const string PreviewFileName = "preview.png";
        private const string PngManifestFileName = "sprite_export.json";
        private const string PartsFolderName = "parts";
        private const string ImportsFolderName = "_Imports";
        private const string ExportsFolderName = "_Exports";
        private const string BundleFolderName = "Bundles";
        private const string PngFolderName = "PNGs";
        private const string ExternalBundleFolderName = "Preset Files";
        private const string ExternalPngFolderName = "Image Files";
        private const string ObjectPartId = "object";
        private const float MinimumAutoFitScale = 0.85f;
        private const int MaxImportTextureDimension = 4096;
        private const long MaxImportPngBytes = 64L * 1024L * 1024L;
        private static readonly Type JsonUtilityType = Type.GetType("UnityEngine.JsonUtility, UnityEngine.JSONSerializeModule");
        private static readonly Type ImageConversionType = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");

        public static string GetPresetsRoot()
        {
            return Path.Combine(HypermodifiedModPathResolver.GetUserDataFolder(), "Presets");
        }

        private static string GetModSourceFolder()
        {
            return HypermodifiedModPathResolver.GetModSourceFolder();
        }

        public static string GetBundleImportsRoot()
        {
            return Path.Combine(GetPresetsRoot(), ImportsFolderName, BundleFolderName);
        }

        public static string GetBundleFilesRoot()
        {
            return Path.Combine(GetPresetsRoot(), ExternalBundleFolderName);
        }

        public static string GetPngImportsRoot()
        {
            return Path.Combine(GetPresetsRoot(), ImportsFolderName, PngFolderName);
        }

        public static string GetPngFilesRoot()
        {
            return Path.Combine(GetPresetsRoot(), ExternalPngFolderName);
        }

        public static string GetBundleExportsRoot()
        {
            return Path.Combine(GetPresetsRoot(), ExportsFolderName, BundleFolderName);
        }

        public static string GetPngExportsRoot()
        {
            return Path.Combine(GetPresetsRoot(), ExportsFolderName, PngFolderName);
        }

        public static void EnsureFolders()
        {
            Directory.CreateDirectory(GetPresetsRoot());
            Directory.CreateDirectory(GetBundleFilesRoot());
            Directory.CreateDirectory(GetPngFilesRoot());
            Directory.CreateDirectory(GetBundleImportsRoot());
            Directory.CreateDirectory(GetPngImportsRoot());
            Directory.CreateDirectory(GetBundleExportsRoot());
            Directory.CreateDirectory(GetPngExportsRoot());
            MigrateAccidentalWorkshopPresetData();
        }

        private static void MigrateAccidentalWorkshopPresetData()
        {
            string currentRoot = GetPresetsRoot();
            string sourceRoot = Path.Combine(GetModSourceFolder(), "Presets");
            if (string.IsNullOrEmpty(sourceRoot) ||
                string.Equals(Path.GetFullPath(sourceRoot), Path.GetFullPath(currentRoot), StringComparison.OrdinalIgnoreCase) ||
                !Directory.Exists(sourceRoot))
            {
                return;
            }

            CopyMissingDirectoryContents(sourceRoot, currentRoot);
        }

        public static List<SpritePresetBrowserItem> EnumerateBrowserItems(SpritePresetBrowserMode mode)
        {
            EnsureFolders();
            List<SpritePresetBrowserItem> items = new List<SpritePresetBrowserItem>();

            if (mode == SpritePresetBrowserMode.Library)
            {
                string[] directories = Directory.GetDirectories(GetPresetsRoot());
                Array.Sort(directories, StringComparer.OrdinalIgnoreCase);

                for (int i = 0; i < directories.Length; i++)
                {
                    string directory = directories[i];
                    string name = Path.GetFileName(directory);
                    if (string.IsNullOrEmpty(name) || name.StartsWith("_", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    SpritePresetBrowserItem item = TryCreateBundleBrowserItem(directory, mode, SpritePresetItemKind.PresetBundle);
                    if (item != null)
                    {
                        items.Add(item);
                    }
                }
            }
            else if (mode == SpritePresetBrowserMode.BundleFiles)
            {
                HashSet<string> seenDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                AppendBundleBrowserItems(items, seenDirectories, GetBundleFilesRoot(), mode, SpritePresetItemKind.ExternalBundle);
                AppendBundleBrowserItems(items, seenDirectories, GetBundleImportsRoot(), mode, SpritePresetItemKind.ExternalBundle);
                AppendBundleBrowserItems(items, seenDirectories, GetBundleExportsRoot(), mode, SpritePresetItemKind.ExternalBundle);
            }
            else
            {
                HashSet<string> seenFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                AppendPngBrowserItems(items, seenFiles, GetPngFilesRoot());
                AppendPngBrowserItems(items, seenFiles, GetPngImportsRoot());
                AppendPngBrowserItems(items, seenFiles, GetPngExportsRoot());
            }

            return items;
        }

        public static SpritePresetOperationResult SaveCharacterPreset(CharacterSpriteEditorTarget target, LimbBehaviour selectedLimb, bool partOnly, byte[] previewPng)
        {
            if (target == null)
            {
                return Failure("No character target was available.");
            }

            EnsureFolders();
            List<LimbBehaviour> limbs = partOnly
                ? new List<LimbBehaviour> { target.GetPreferredLimb(selectedLimb) }
                : target.GetAvailableLimbs();

            if (limbs.Count <= 0 || limbs[0] == null)
            {
                return Failure("No editable limbs were available to save.");
            }

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string partLabel = partOnly ? target.GetLimbDisplayName(limbs[0]) : null;
            string displayName = BuildPresetDisplayName(target.GetCharacterName(), partOnly ? partLabel : "Full", partOnly);
            string presetDirectory = CreateUniquePresetDirectory(displayName, timestamp);
            string partsDirectory = Path.Combine(presetDirectory, PartsFolderName);
            Directory.CreateDirectory(partsDirectory);

            SpritePresetBundleMetadata metadata = new SpritePresetBundleMetadata();
            metadata.name = displayName;
            metadata.version = PresetVersion;
            metadata.targetKind = "character";
            metadata.scope = partOnly ? "part" : "full";
            metadata.sourceName = target.GetCharacterName();
            metadata.createdAt = DateTime.UtcNow.ToString("o");

            for (int i = 0; i < limbs.Count; i++)
            {
                LimbBehaviour limb = limbs[i];
                if (limb == null)
                {
                    continue;
                }

                LimbSpriteState state = target.GetState(limb);
                if (state == null || !state.IsValid || state.OriginalSprite == null)
                {
                    continue;
                }

                string partId = target.GetPartId(limb);
                string fileName = MakeSafeName(partId) + ".png";
                string relativePngPath = PartsFolderName + "/" + fileName;
                string fullPngPath = Path.Combine(partsDirectory, fileName);

                if (!WritePixelsToPng(state.GetCurrentPixelsSnapshot(), state.Width, state.Height, fullPngPath))
                {
                    continue;
                }

                SpritePresetEntryMetadata entry = new SpritePresetEntryMetadata();
                entry.partId = partId;
                entry.partLabel = target.GetLimbDisplayName(limb);
                entry.variantKey = SpriteVariantKeyUtility.GetKey(state.OriginalSprite);
                entry.width = state.Width;
                entry.height = state.Height;
                entry.pivotX = Mathf.RoundToInt(state.OriginalSprite.pivot.x);
                entry.pivotY = Mathf.RoundToInt(state.OriginalSprite.pivot.y);
                entry.pixelsPerUnit = state.OriginalSprite.pixelsPerUnit;
                entry.pngPath = relativePngPath;
                entry.includesHitbox = true;
                WriteLayerStackSidecar(state, fullPngPath, entry);
                metadata.entries.Add(entry);

                if (partOnly)
                {
                    break;
                }
            }

            if (metadata.entries.Count <= 0)
            {
                SafeDeleteDirectory(presetDirectory);
                return Failure("Nothing valid was available to save into a preset.");
            }

            string serialized = SerializeMetadata(metadata);
            if (string.IsNullOrWhiteSpace(serialized))
            {
                SafeDeleteDirectory(presetDirectory);
                return Failure("The preset metadata serializer was unavailable.");
            }

            WritePreviewPng(previewPng, presetDirectory, metadata.entries[0]);
            File.WriteAllText(Path.Combine(presetDirectory, PresetFileName), serialized);

            return Success(
                "Saved preset to " + presetDirectory,
                TryCreateBundleBrowserItem(presetDirectory, SpritePresetBrowserMode.Library, SpritePresetItemKind.PresetBundle));
        }

        public static SpritePresetOperationResult SaveObjectPreset(ObjectSpriteEditorTarget target, bool partOnly, byte[] previewPng)
        {
            if (target == null)
            {
                return Failure("No object target was available.");
            }

            EnsureFolders();
            LimbSpriteState state = target.GetState();
            if (state == null || !state.IsValid || state.OriginalSprite == null)
            {
                return Failure("No editable object sprite was available to save.");
            }

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string displayName = BuildPresetDisplayName(target.GetObjectName(), partOnly ? "Patch" : "Full", partOnly);
            string presetDirectory = CreateUniquePresetDirectory(displayName, timestamp);
            string partsDirectory = Path.Combine(presetDirectory, PartsFolderName);
            Directory.CreateDirectory(partsDirectory);

            SpritePresetBundleMetadata metadata = new SpritePresetBundleMetadata();
            metadata.name = displayName;
            metadata.version = PresetVersion;
            metadata.targetKind = "object";
            metadata.scope = partOnly ? "part" : "full";
            metadata.sourceName = target.GetObjectName();
            metadata.createdAt = DateTime.UtcNow.ToString("o");

            string fileName = ObjectPartId + ".png";
            string relativePngPath = PartsFolderName + "/" + fileName;
            string fullPngPath = Path.Combine(partsDirectory, fileName);
            if (!WritePixelsToPng(state.GetCurrentPixelsSnapshot(), state.Width, state.Height, fullPngPath))
            {
                SafeDeleteDirectory(presetDirectory);
                return Failure("The object preset PNG could not be written.");
            }

            SpritePresetEntryMetadata entry = new SpritePresetEntryMetadata();
            entry.partId = target.GetPartId();
            entry.partLabel = target.GetObjectName();
            entry.variantKey = SpriteVariantKeyUtility.GetKey(state.OriginalSprite);
            entry.width = state.Width;
            entry.height = state.Height;
            entry.pivotX = Mathf.RoundToInt(state.OriginalSprite.pivot.x);
            entry.pivotY = Mathf.RoundToInt(state.OriginalSprite.pivot.y);
            entry.pixelsPerUnit = state.OriginalSprite.pixelsPerUnit;
            entry.pngPath = relativePngPath;
            entry.includesHitbox = true;
            WriteLayerStackSidecar(state, fullPngPath, entry);
            metadata.entries.Add(entry);

            string serialized = SerializeMetadata(metadata);
            if (string.IsNullOrWhiteSpace(serialized))
            {
                SafeDeleteDirectory(presetDirectory);
                return Failure("The preset metadata serializer was unavailable.");
            }

            WritePreviewPng(previewPng, presetDirectory, entry);
            File.WriteAllText(Path.Combine(presetDirectory, PresetFileName), serialized);

            return Success(
                "Saved preset to " + presetDirectory,
                TryCreateBundleBrowserItem(presetDirectory, SpritePresetBrowserMode.Library, SpritePresetItemKind.PresetBundle));
        }

        public static SpritePresetOperationResult ApplyPresetToCharacter(SpritePresetBrowserItem item, CharacterSpriteEditorTarget target, LimbBehaviour selectedLimb)
        {
            if (item == null || item.Metadata == null || target == null)
            {
                return Failure("A compatible preset and character target are required.");
            }

            if (!string.Equals(item.Metadata.targetKind, "character", StringComparison.OrdinalIgnoreCase))
            {
                return Failure("That preset does not target characters.");
            }

            int applied = 0;
            int skippedVariant = 0;
            int skippedMissing = 0;
            int skippedDimensions = 0;

            if (string.Equals(item.Metadata.scope, "part", StringComparison.OrdinalIgnoreCase))
            {
                LimbBehaviour targetLimb = target.GetPreferredLimb(selectedLimb);
                LimbSpriteState state = target.GetState(targetLimb);
                SpritePresetEntryMetadata entry = item.Metadata.entries != null && item.Metadata.entries.Count > 0
                    ? item.Metadata.entries[0]
                    : null;

                if (state == null || entry == null)
                {
                    return Failure("The selected part preset did not contain a usable entry.");
                }

                bool usedAutoFit;
                if (!TryApplyPresetEntry(item, entry, state, true, out usedAutoFit))
                {
                    skippedDimensions++;
                }
                else
                {
                    applied++;
                }
            }
            else
            {
                for (int i = 0; i < item.Metadata.entries.Count; i++)
                {
                    SpritePresetEntryMetadata entry = item.Metadata.entries[i];
                    LimbBehaviour limb = target.FindLimbByPartId(entry.partId);
                    if (limb == null)
                    {
                        skippedMissing++;
                        continue;
                    }

                    LimbSpriteState state = target.GetState(limb);
                    if (state == null)
                    {
                        skippedMissing++;
                        continue;
                    }

                    if (!VariantMatches(entry.variantKey, state.OriginalSprite))
                    {
                        skippedVariant++;
                        continue;
                    }

                    bool usedAutoFit;
                    if (!TryApplyPresetEntry(item, entry, state, true, out usedAutoFit))
                    {
                        skippedDimensions++;
                        continue;
                    }

                    if (limb != target.GetPreferredLimb(selectedLimb))
                    {
                        state.FinalizeForWorldDisplay();
                    }

                    applied++;
                }
            }

            return applied > 0
                ? Success(BuildApplyMessage(applied, skippedVariant, skippedMissing, skippedDimensions), item)
                : Failure(BuildApplyMessage(applied, skippedVariant, skippedMissing, skippedDimensions));
        }

        public static SpritePresetOperationResult ApplyPresetToObject(SpritePresetBrowserItem item, ObjectSpriteEditorTarget target)
        {
            if (item == null || item.Metadata == null || target == null)
            {
                return Failure("A compatible preset and object target are required.");
            }

            if (!string.Equals(item.Metadata.targetKind, "object", StringComparison.OrdinalIgnoreCase))
            {
                return Failure("That preset does not target objects.");
            }

            LimbSpriteState state = target.GetState();
            if (state == null)
            {
                return Failure("No editable object state was available.");
            }

            SpritePresetEntryMetadata entry = item.Metadata.entries != null && item.Metadata.entries.Count > 0
                ? item.Metadata.entries[0]
                : null;
            if (entry == null)
            {
                return Failure("The preset did not contain any object entries.");
            }

            bool requireVariantMatch = !string.Equals(item.Metadata.scope, "part", StringComparison.OrdinalIgnoreCase);
            if (requireVariantMatch && !VariantMatches(entry.variantKey, state.OriginalSprite))
            {
                return Failure("The preset entry did not match this object's current sprite state.");
            }

            bool usedAutoFit;
            if (!TryApplyPresetEntry(item, entry, state, true, out usedAutoFit))
            {
                return Failure("The preset image could not fit onto this object's current sprite.");
            }

            return Success("Applied preset to " + target.GetObjectName() + ".", item);
        }

        public static SpritePresetOperationResult DeletePreset(SpritePresetBrowserItem item)
        {
            if (item == null || item.ItemKind != SpritePresetItemKind.PresetBundle || string.IsNullOrEmpty(item.SourcePath))
            {
                return Failure("Select a saved preset to delete.");
            }

            SafeDeleteDirectory(item.SourcePath);
            return Success("Deleted preset " + item.DisplayName + ".", null);
        }

        public static SpritePresetOperationResult ImportBundle(SpritePresetBrowserItem item)
        {
            if (item == null || item.ItemKind != SpritePresetItemKind.ExternalBundle || string.IsNullOrEmpty(item.SourcePath))
            {
                return Failure("Select a preset folder first.");
            }

            EnsureFolders();
            string targetDirectory = CreateUniqueDirectoryPath(GetPresetsRoot(), Path.GetFileName(item.SourcePath));
            CopyDirectory(item.SourcePath, targetDirectory);

            return Success(
                "Added preset folder to saved presets: " + targetDirectory,
                TryCreateBundleBrowserItem(targetDirectory, SpritePresetBrowserMode.Library, SpritePresetItemKind.PresetBundle));
        }

        public static SpritePresetOperationResult ExportBundle(SpritePresetBrowserItem item)
        {
            if (item == null || item.ItemKind != SpritePresetItemKind.PresetBundle || string.IsNullOrEmpty(item.SourcePath))
            {
                return Failure("Select a saved preset to export.");
            }

            EnsureFolders();
            string exportDirectory = CreateUniqueDirectoryPath(GetBundleFilesRoot(), Path.GetFileName(item.SourcePath));
            CopyDirectory(item.SourcePath, exportDirectory);
            return Success(
                "Wrote preset folder to " + exportDirectory + ". It will show up in the Preset Files tab.",
                TryCreateBundleBrowserItem(exportDirectory, SpritePresetBrowserMode.BundleFiles, SpritePresetItemKind.ExternalBundle));
        }

        public static SpritePresetOperationResult ImportPngToState(SpritePresetBrowserItem item, LimbSpriteState state, string label)
        {
            if (item == null || item.ItemKind != SpritePresetItemKind.ExternalPng || string.IsNullOrEmpty(item.SourcePath))
            {
                return Failure("Select an image file first.");
            }

            return ImportPngFileToState(item.SourcePath, state, label);
        }

        public static SpritePresetOperationResult ImportPngFileToState(string filePath, LimbSpriteState state, string label)
        {
            if (state == null || !state.IsValid)
            {
                return Failure("No editable sprite was selected.");
            }

            Texture2D texture = LoadTexture(filePath);
            if (texture == null)
            {
                return Failure("Could not read PNG " + filePath);
            }

            SpritePresetBundleMetadata manifest = TryLoadPngExportManifest(new string[] { filePath });
            SpritePresetEntryMetadata entry = FindPngManifestEntry(manifest, filePath);
            bool usedAutoFit;
            bool usedReplacement = false;
            bool missingBodyLayerData = false;
            bool applied = false;

            if (entry != null)
            {
                applied = TryReplaceTextureLayout(texture, filePath, entry, state, out missingBodyLayerData);
                usedReplacement = applied;
                usedAutoFit = false;
            }
            else
            {
                applied = TryApplyTextureToState(texture, null, state, true, out usedAutoFit);
                if (!applied)
                {
                    applied = TryReplaceTextureLayout(texture, filePath, null, state, out missingBodyLayerData);
                    usedReplacement = applied;
                    usedAutoFit = false;
                }
                else
                {
                    TryRestoreLayerStackSidecar(state, filePath, null);
                }
            }

            UnityEngine.Object.Destroy(texture);

            return applied
                ? Success(
                    "Loaded image into " + label + (usedReplacement ? " by replacing the sprite layout." : usedAutoFit ? " using auto-fit." : "."),
                    null)
                : missingBodyLayerData
                    ? Failure("That PNG needs matching body-layer data. Export the limb again with this version, then import the newly exported PNG.")
                    : Failure("That image could not fit onto " + label + ". Trim or resize the visible pixels and try again.");
        }

        public static SpritePresetOperationResult ImportPngFilesToCharacter(string[] filePaths, CharacterSpriteEditorTarget target, LimbBehaviour selectedLimb)
        {
            if (target == null)
            {
                return Failure("No character target was available.");
            }

            if (filePaths == null || filePaths.Length <= 0)
            {
                return Failure("No PNG files were selected.");
            }

            List<LimbBehaviour> limbs = target.GetAvailableLimbs();
            if (limbs.Count <= 0)
            {
                return Failure("No editable limbs were available.");
            }

            int applied = 0;
            int skipped = 0;
            int skippedMissingBodyLayers = 0;
            HashSet<LimbBehaviour> usedLimbs = new HashSet<LimbBehaviour>();
            LimbBehaviour selected = target.GetPreferredLimb(selectedLimb);
            SpritePresetBundleMetadata manifest = TryLoadPngExportManifest(filePaths);

            for (int i = 0; i < filePaths.Length; i++)
            {
                string filePath = filePaths[i];
                SpritePresetEntryMetadata entry = FindPngManifestEntry(manifest, filePath);
                LimbBehaviour limb = FindCharacterLimb(target, entry, usedLimbs);
                if (limb == null)
                {
                    limb = FindBestLimbForPngFile(filePath, target, limbs, usedLimbs);
                }

                if (limb == null && i < limbs.Count)
                {
                    limb = limbs[i];
                }

                if (limb == null)
                {
                    skipped++;
                    continue;
                }

                LimbSpriteState state = target.GetState(limb);
                Texture2D texture = LoadTexture(filePath);
                if (state == null || !state.IsValid || texture == null)
                {
                    if (texture != null)
                    {
                        UnityEngine.Object.Destroy(texture);
                    }

                    skipped++;
                    continue;
                }

                bool missingBodyLayerData;
                bool didApply = TryReplaceTextureLayout(texture, filePath, entry, state, out missingBodyLayerData);
                UnityEngine.Object.Destroy(texture);

                if (didApply)
                {
                    if (limb != selected)
                    {
                        state.FinalizeForWorldDisplay();
                    }

                    applied++;
                    usedLimbs.Add(limb);
                }
                else
                {
                    if (missingBodyLayerData)
                    {
                        skippedMissingBodyLayers++;
                    }

                    skipped++;
                }
            }

            return applied > 0
                ? Success("Imported " + applied + " body PNG" + (applied == 1 ? string.Empty : "s") + (skipped > 0 ? " (" + skipped + " skipped)." : "."), null)
                : skippedMissingBodyLayers > 0
                    ? Failure("Those PNGs need matching body-layer data. Export the sprite again with this version, then import the newly exported PNGs.")
                    : Failure("No selected PNGs could be matched to editable limbs.");
        }

        public static SpritePresetOperationResult ExportStatePng(LimbSpriteState state, string targetName, string partName)
        {
            if (state == null || !state.IsValid)
            {
                return Failure("No editable sprite was selected.");
            }

            EnsureFolders();
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string exportDirectory = CreateExportDirectory(timestamp);
            string fileName = MakeSafeName(targetName) + "_" + MakeSafeName(partName) + ".png";
            string outputPath = CreateUniqueFilePath(exportDirectory, fileName);

            if (!WritePixelsToPng(state.GetCurrentPixelsSnapshot(), state.Width, state.Height, outputPath))
            {
                return Failure("The PNG export could not be written.");
            }

            SpritePresetBundleMetadata metadata = CreatePngExportMetadata(targetName, "part", timestamp);
            SpritePresetEntryMetadata entry = CreatePngExportEntry(state, partName, partName, Path.GetFileName(outputPath));
            WriteLayerStackSidecar(state, outputPath, entry);
            WriteBodyLayerSidecars(state, outputPath, entry);
            WriteDamageLayerSidecar(state, outputPath, entry);
            metadata.entries.Add(entry);
            WritePngExportManifest(exportDirectory, metadata);

            return Success("Exported " + partName + " PNG to " + Path.GetFileName(exportDirectory) + ".", TryCreateImportPngItem(outputPath));
        }

        public static SpritePresetOperationResult ExportCharacterPngs(CharacterSpriteEditorTarget target)
        {
            if (target == null)
            {
                return Failure("No character target was available.");
            }

            EnsureFolders();

            List<LimbBehaviour> limbs = target.GetAvailableLimbs();
            if (limbs.Count <= 0)
            {
                return Failure("No editable limbs were available.");
            }

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string exportDirectory = CreateExportDirectory(timestamp);
            string targetName = target.GetCharacterName();
            SpritePresetBundleMetadata metadata = CreatePngExportMetadata(targetName, "target", timestamp);
            int written = 0;
            SpritePresetBrowserItem firstItem = null;

            for (int i = 0; i < limbs.Count; i++)
            {
                LimbBehaviour limb = limbs[i];
                LimbSpriteState state = target.GetState(limb);
                if (state == null || !state.IsValid)
                {
                    continue;
                }

                string partName = target.GetPartId(limb);
                string fileName = MakeSafeName(targetName) + "_" + MakeSafeName(partName) + ".png";
                string outputPath = CreateUniqueFilePath(exportDirectory, fileName);
                if (!WritePixelsToPng(state.GetCurrentPixelsSnapshot(), state.Width, state.Height, outputPath))
                {
                    continue;
                }

                SpritePresetEntryMetadata entry = CreatePngExportEntry(state, partName, target.GetLimbDisplayName(limb), Path.GetFileName(outputPath));
                WriteLayerStackSidecar(state, outputPath, entry);
                WriteBodyLayerSidecars(state, outputPath, entry);
                WriteDamageLayerSidecar(state, outputPath, entry);
                metadata.entries.Add(entry);
                written++;
                if (firstItem == null)
                {
                    firstItem = TryCreateImportPngItem(outputPath);
                }
            }

            if (written > 0)
            {
                WritePngExportManifest(exportDirectory, metadata);
            }

            return written > 0
                ? Success("Exported " + written + " body PNG" + (written == 1 ? string.Empty : "s") + " to " + Path.GetFileName(exportDirectory) + ".", firstItem)
                : Failure("No body PNGs could be written.");
        }

        public static bool TryApplyPixelsToState(
            Color32[] sourcePixels,
            int sourceWidth,
            int sourceHeight,
            Vector2Int? sourcePivot,
            LimbSpriteState state,
            bool allowContentFit,
            out bool usedAutoFit)
        {
            usedAutoFit = false;
            if (sourcePixels == null || state == null || !state.IsValid || sourceWidth <= 0 || sourceHeight <= 0)
            {
                return false;
            }

            if (sourcePixels.Length != sourceWidth * sourceHeight)
            {
                return false;
            }

            if (sourceWidth == state.Width && sourceHeight == state.Height)
            {
                return state.ReplaceWorkingPixels(sourcePixels, sourceWidth, sourceHeight);
            }

            if (!allowContentFit)
            {
                return false;
            }

            Color32[] transferredPixels;
            if (!TryBuildTransferredPixels(sourcePixels, sourceWidth, sourceHeight, sourcePivot, state, out transferredPixels))
            {
                return false;
            }

            usedAutoFit = true;
            return state.ReplaceWorkingPixels(transferredPixels, state.Width, state.Height);
        }

        private static SpritePresetOperationResult Success(string message, SpritePresetBrowserItem item)
        {
            SpritePresetOperationResult result = new SpritePresetOperationResult();
            result.Success = true;
            result.Message = message;
            result.BrowserItem = item;
            return result;
        }

        private static SpritePresetOperationResult Failure(string message)
        {
            SpritePresetOperationResult result = new SpritePresetOperationResult();
            result.Success = false;
            result.Message = message;
            return result;
        }

        private static bool VariantMatches(string expectedVariantKey, Sprite baseSprite)
        {
            if (string.IsNullOrEmpty(expectedVariantKey))
            {
                return true;
            }

            return string.Equals(expectedVariantKey, SpriteVariantKeyUtility.GetKey(baseSprite), StringComparison.Ordinal);
        }

        private static string BuildApplyMessage(int applied, int skippedVariant, int skippedMissing, int skippedDimensions)
        {
            return "Applied " + applied +
                   " preset entr" + (applied == 1 ? "y" : "ies") +
                   ". Skipped " + skippedVariant + " variant mismatch, " +
                   skippedMissing + " missing part, " +
                   skippedDimensions + " fit mismatch.";
        }

        private static void WritePreviewPng(byte[] previewPng, string presetDirectory, SpritePresetEntryMetadata fallbackEntry)
        {
            string previewPath = Path.Combine(presetDirectory, PreviewFileName);

            if (previewPng != null && previewPng.Length > 0)
            {
                File.WriteAllBytes(previewPath, previewPng);
                return;
            }

            if (fallbackEntry == null || string.IsNullOrEmpty(fallbackEntry.pngPath))
            {
                return;
            }

            string fallbackPath = Path.Combine(presetDirectory, fallbackEntry.pngPath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(fallbackPath))
            {
                File.Copy(fallbackPath, previewPath, true);
            }
        }

        private static bool TryApplyPresetEntry(SpritePresetBrowserItem item, SpritePresetEntryMetadata entry, LimbSpriteState state, bool allowContentFit, out bool usedAutoFit)
        {
            usedAutoFit = false;

            if (item == null || entry == null || state == null)
            {
                return false;
            }

            string pngPath = GetEntryPngPath(item.SourcePath, entry);
            Texture2D texture = LoadTexture(pngPath);
            if (texture == null)
            {
                return false;
            }

            bool entryDimensionMatch = texture.width == entry.width && texture.height == entry.height;
            bool applied = entryDimensionMatch &&
                           TryApplyTextureToState(
                               texture,
                               new Vector2Int(entry.pivotX, entry.pivotY),
                               state,
                               allowContentFit,
                               out usedAutoFit);
            UnityEngine.Object.Destroy(texture);
            if (applied)
            {
                TryRestoreLayerStackSidecar(state, pngPath, entry);
            }

            return applied;
        }

        private static SpritePresetBrowserItem TryCreateBundleBrowserItem(string directory, SpritePresetBrowserMode mode, SpritePresetItemKind kind)
        {
            if (string.IsNullOrEmpty(directory))
            {
                return null;
            }

            string presetPath = Path.Combine(directory, PresetFileName);
            if (!File.Exists(presetPath))
            {
                return null;
            }

            SpritePresetBundleMetadata metadata;
            try
            {
                metadata = DeserializeMetadata(File.ReadAllText(presetPath));
            }
            catch
            {
                return null;
            }

            if (metadata == null)
            {
                return null;
            }

            SpritePresetBrowserItem item = new SpritePresetBrowserItem();
            item.DisplayName = string.IsNullOrWhiteSpace(metadata.name) ? Path.GetFileName(directory) : metadata.name;
            item.Description = metadata.targetKind + "  |  " + metadata.scope;
            item.SourcePath = directory;
            item.PreviewPath = Path.Combine(directory, PreviewFileName);
            item.Mode = mode;
            item.ItemKind = kind;
            item.Metadata = metadata;
            item.PreviewTexture = File.Exists(item.PreviewPath) ? LoadTexture(item.PreviewPath) : null;
            item.Description = kind == SpritePresetItemKind.PresetBundle
                ? metadata.targetKind + " preset  |  " + metadata.scope
                : metadata.targetKind + " folder  |  " + metadata.scope;
            return item;
        }

        private static SpritePresetBrowserItem TryCreateImportPngItem(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            SpritePresetBrowserItem item = new SpritePresetBrowserItem();
            item.DisplayName = Path.GetFileNameWithoutExtension(filePath);
            item.SourcePath = filePath;
            item.PreviewPath = filePath;
            item.Mode = SpritePresetBrowserMode.PngFiles;
            item.ItemKind = SpritePresetItemKind.ExternalPng;
            item.PreviewTexture = LoadTexture(filePath);
            item.Description = item.PreviewTexture != null
                ? "image file  |  " + item.PreviewTexture.width + "x" + item.PreviewTexture.height
                : "image file";
            return item;
        }

        private static void AppendBundleBrowserItems(
            List<SpritePresetBrowserItem> items,
            HashSet<string> seenDirectories,
            string root,
            SpritePresetBrowserMode mode,
            SpritePresetItemKind kind)
        {
            if (items == null || seenDirectories == null || string.IsNullOrEmpty(root) || !Directory.Exists(root))
            {
                return;
            }

            string[] directories = Directory.GetDirectories(root);
            Array.Sort(directories, StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < directories.Length; i++)
            {
                string directory = Path.GetFullPath(directories[i]);
                if (!seenDirectories.Add(directory))
                {
                    continue;
                }

                SpritePresetBrowserItem item = TryCreateBundleBrowserItem(directory, mode, kind);
                if (item != null)
                {
                    items.Add(item);
                }
            }
        }

        private static void AppendPngBrowserItems(List<SpritePresetBrowserItem> items, HashSet<string> seenFiles, string root)
        {
            if (items == null || seenFiles == null || string.IsNullOrEmpty(root) || !Directory.Exists(root))
            {
                return;
            }

            string[] files = Directory.GetFiles(root, "*.png", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < files.Length; i++)
            {
                string filePath = Path.GetFullPath(files[i]);
                if (!seenFiles.Add(filePath))
                {
                    continue;
                }

                SpritePresetBrowserItem item = TryCreateImportPngItem(filePath);
                if (item != null)
                {
                    items.Add(item);
                }
            }
        }

        private static bool TryApplyTextureToState(
            Texture2D texture,
            Vector2Int? sourcePivot,
            LimbSpriteState state,
            bool allowContentFit,
            out bool usedAutoFit)
        {
            if (texture == null || state == null || !state.IsValid)
            {
                usedAutoFit = false;
                return false;
            }

            Color32[] sourcePixels = texture.GetPixels32();
            return TryApplyPixelsToState(sourcePixels, texture.width, texture.height, sourcePivot, state, allowContentFit, out usedAutoFit);
        }

        private static bool TryReplaceTextureLayout(Texture2D texture, string skinFilePath, SpritePresetEntryMetadata entry, LimbSpriteState state, out bool missingBodyLayerData)
        {
            missingBodyLayerData = false;
            if (texture == null || state == null || !state.IsValid)
            {
                return false;
            }

            int pivotX;
            int pivotY;
            float pixelsPerUnit;
            ResolvePngReplacementLayout(texture, entry, state, out pivotX, out pivotY, out pixelsPerUnit);
            Color32[] skinPixels = texture.GetPixels32();

            Color32[] fleshPixels;
            int fleshWidth;
            int fleshHeight;
            Color32[] bonePixels;
            int boneWidth;
            int boneHeight;
            bool hasBodyLayerSidecars = TryLoadBodyLayerSidecars(
                skinFilePath,
                entry,
                out fleshPixels,
                out fleshWidth,
                out fleshHeight,
                out bonePixels,
                out boneWidth,
                out boneHeight);

            Color32[] damagePixels;
            int damageWidth;
            int damageHeight;
            bool hasDamageLayerSidecar = TryLoadDamageLayerSidecar(
                skinFilePath,
                entry,
                out damagePixels,
                out damageWidth,
                out damageHeight) &&
                damageWidth == texture.width &&
                damageHeight == texture.height;

            if (state.Limb != null &&
                (!hasBodyLayerSidecars ||
                 !BodyLayerDimensionsMatch(fleshWidth, fleshHeight, boneWidth, boneHeight, texture.width, texture.height)))
            {
                CreateFallbackBodyLayerSnapshots(
                    state,
                    skinPixels,
                    texture.width,
                    texture.height,
                    out fleshPixels,
                    out fleshWidth,
                    out fleshHeight,
                    out bonePixels,
                    out boneWidth,
                    out boneHeight);

                hasBodyLayerSidecars = BodyLayerDimensionsMatch(
                    fleshWidth,
                    fleshHeight,
                    boneWidth,
                    boneHeight,
                    texture.width,
                    texture.height);

                if (!hasBodyLayerSidecars)
                {
                    missingBodyLayerData = true;
                    return false;
                }
            }

            bool applied;
            if (!hasBodyLayerSidecars)
            {
                applied = state.ReplaceWorkingSprite(skinPixels, texture.width, texture.height, pivotX, pivotY, pixelsPerUnit);
            }
            else
            {
                applied = state.ReplaceWorkingSprite(
                    skinPixels,
                    texture.width,
                    texture.height,
                    pivotX,
                    pivotY,
                    pixelsPerUnit,
                    fleshPixels,
                    fleshWidth,
                    fleshHeight,
                    bonePixels,
                    boneWidth,
                    boneHeight,
                    hasDamageLayerSidecar ? damagePixels : null,
                    hasDamageLayerSidecar ? damageWidth : 0,
                    hasDamageLayerSidecar ? damageHeight : 0);
            }

            if (applied)
            {
                TryRestoreLayerStackSidecar(state, skinFilePath, entry);
            }

            return applied;
        }

        private static bool BodyLayerDimensionsMatch(int fleshWidth, int fleshHeight, int boneWidth, int boneHeight, int skinWidth, int skinHeight)
        {
            return fleshWidth == skinWidth &&
                   fleshHeight == skinHeight &&
                   boneWidth == skinWidth &&
                   boneHeight == skinHeight;
        }

        private static void ResolvePngReplacementLayout(
            Texture2D texture,
            SpritePresetEntryMetadata entry,
            LimbSpriteState state,
            out int pivotX,
            out int pivotY,
            out float pixelsPerUnit)
        {
            if (entry != null &&
                entry.width == texture.width &&
                entry.height == texture.height)
            {
                pivotX = entry.pivotX;
                pivotY = entry.pivotY;
                pixelsPerUnit = entry.pixelsPerUnit > 0.01f ? entry.pixelsPerUnit : 1f;
                return;
            }

            Sprite sprite = state != null ? state.OriginalSprite : null;
            if (sprite != null)
            {
                float width = Mathf.Max(1f, sprite.rect.width);
                float height = Mathf.Max(1f, sprite.rect.height);
                pivotX = Mathf.RoundToInt((sprite.pivot.x / width) * texture.width);
                pivotY = Mathf.RoundToInt((sprite.pivot.y / height) * texture.height);
                pixelsPerUnit = sprite.pixelsPerUnit > 0.01f ? sprite.pixelsPerUnit : 1f;
            }
            else
            {
                pivotX = texture.width / 2;
                pivotY = texture.height / 2;
                pixelsPerUnit = 1f;
            }

            pivotX = Mathf.Clamp(pivotX, 0, Mathf.Max(0, texture.width - 1));
            pivotY = Mathf.Clamp(pivotY, 0, Mathf.Max(0, texture.height - 1));
        }

        private static bool TryLoadBodyLayerSidecars(
            string skinFilePath,
            SpritePresetEntryMetadata entry,
            out Color32[] fleshPixels,
            out int fleshWidth,
            out int fleshHeight,
            out Color32[] bonePixels,
            out int boneWidth,
            out int boneHeight)
        {
            fleshPixels = null;
            fleshWidth = 0;
            fleshHeight = 0;
            bonePixels = null;
            boneWidth = 0;
            boneHeight = 0;

            if (string.IsNullOrEmpty(skinFilePath))
            {
                return false;
            }

            string directory = Path.GetDirectoryName(skinFilePath);
            if (string.IsNullOrEmpty(directory))
            {
                return false;
            }

            string fleshPath;
            string bonePath;
            if (entry != null)
            {
                fleshPath = ResolveSidecarPath(directory, entry.fleshPngPath);
                bonePath = ResolveSidecarPath(directory, entry.bonePngPath);
            }
            else
            {
                string baseName = MakeSafeName(Path.GetFileNameWithoutExtension(skinFilePath));
                fleshPath = Path.Combine(directory, baseName + ".flesh.layer");
                bonePath = Path.Combine(directory, baseName + ".bone.layer");
            }
            if (string.IsNullOrEmpty(fleshPath) ||
                string.IsNullOrEmpty(bonePath) ||
                !File.Exists(fleshPath) ||
                !File.Exists(bonePath))
            {
                return false;
            }

            Texture2D fleshTexture = LoadTexture(fleshPath);
            Texture2D boneTexture = LoadTexture(bonePath);
            if (fleshTexture == null || boneTexture == null)
            {
                if (fleshTexture != null)
                {
                    UnityEngine.Object.Destroy(fleshTexture);
                }

                if (boneTexture != null)
                {
                    UnityEngine.Object.Destroy(boneTexture);
                }

                return false;
            }

            fleshWidth = fleshTexture.width;
            fleshHeight = fleshTexture.height;
            boneWidth = boneTexture.width;
            boneHeight = boneTexture.height;
            fleshPixels = fleshTexture.GetPixels32();
            bonePixels = boneTexture.GetPixels32();

            UnityEngine.Object.Destroy(fleshTexture);
            UnityEngine.Object.Destroy(boneTexture);
            return true;
        }

        private static bool TryLoadDamageLayerSidecar(
            string skinFilePath,
            SpritePresetEntryMetadata entry,
            out Color32[] damagePixels,
            out int damageWidth,
            out int damageHeight)
        {
            damagePixels = null;
            damageWidth = 0;
            damageHeight = 0;

            if (string.IsNullOrEmpty(skinFilePath))
            {
                return false;
            }

            string directory = Path.GetDirectoryName(skinFilePath);
            if (string.IsNullOrEmpty(directory))
            {
                return false;
            }

            string damagePath = entry != null
                ? ResolveSidecarPath(directory, entry.damagePngPath)
                : Path.Combine(directory, MakeSafeName(Path.GetFileNameWithoutExtension(skinFilePath)) + ".damage.layer");
            if (string.IsNullOrEmpty(damagePath) || !File.Exists(damagePath))
            {
                return false;
            }

            Texture2D damageTexture = LoadTexture(damagePath);
            if (damageTexture == null)
            {
                return false;
            }

            damageWidth = damageTexture.width;
            damageHeight = damageTexture.height;
            damagePixels = damageTexture.GetPixels32();
            UnityEngine.Object.Destroy(damageTexture);
            return true;
        }

        private static LimbSpriteState.LayerStackSnapshot TryLoadLayerStackSidecar(string skinFilePath, SpritePresetEntryMetadata entry)
        {
            if (string.IsNullOrEmpty(skinFilePath))
            {
                return null;
            }

            string directory = Path.GetDirectoryName(skinFilePath);
            if (string.IsNullOrEmpty(directory))
            {
                return null;
            }

            string layerPath = entry != null
                ? ResolveSidecarPath(directory, entry.layerStackPath)
                : Path.Combine(directory, MakeSafeName(Path.GetFileNameWithoutExtension(skinFilePath)) + ".layers.dat");
            if (string.IsNullOrEmpty(layerPath) || !File.Exists(layerPath))
            {
                return null;
            }

            try
            {
                return HypermodifiedSpritePersistence.DecodeLayerStack(File.ReadAllBytes(layerPath));
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Hypermodified] Failed to read layer stack sidecar: " + exception.Message);
                return null;
            }
        }

        private static bool TryRestoreLayerStackSidecar(LimbSpriteState state, string skinFilePath, SpritePresetEntryMetadata entry)
        {
            LimbSpriteState.LayerStackSnapshot stack = TryLoadLayerStackSidecar(skinFilePath, entry);
            return stack != null && state != null && state.RestoreLayerStackSnapshot(stack, false);
        }

        private static string ResolveSidecarPath(string directory, string relativePath)
        {
            if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(relativePath))
            {
                return string.Empty;
            }

            return Path.IsPathRooted(relativePath)
                ? relativePath
                : Path.Combine(directory, relativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static void CreateFallbackBodyLayerSnapshots(
            LimbSpriteState state,
            Color32[] skinPixels,
            int width,
            int height,
            out Color32[] fleshPixels,
            out int fleshWidth,
            out int fleshHeight,
            out Color32[] bonePixels,
            out int boneWidth,
            out int boneHeight)
        {
            int safeWidth = Mathf.Max(1, width);
            int safeHeight = Mathf.Max(1, height);
            int pixelCount = safeWidth * safeHeight;
            fleshPixels = new Color32[pixelCount];
            bonePixels = new Color32[pixelCount];
            fleshWidth = safeWidth;
            fleshHeight = safeHeight;
            boneWidth = fleshWidth;
            boneHeight = fleshHeight;

            Color32[] sourceFleshPixels;
            int sourceFleshWidth;
            int sourceFleshHeight;
            Color32[] sourceBonePixels;
            int sourceBoneWidth;
            int sourceBoneHeight;
            if (state == null ||
                !state.TryGetBodyLayerSnapshots(
                    out sourceFleshPixels,
                    out sourceFleshWidth,
                    out sourceFleshHeight,
                    out sourceBonePixels,
                    out sourceBoneWidth,
                    out sourceBoneHeight) ||
                sourceFleshPixels == null ||
                sourceBonePixels == null ||
                sourceFleshWidth <= 0 ||
                sourceFleshHeight <= 0 ||
                sourceBoneWidth <= 0 ||
                sourceBoneHeight <= 0)
            {
                return;
            }

            RectInt destinationBounds;
            if (!TryGetVisibleBounds(skinPixels, safeWidth, safeHeight, out destinationBounds))
            {
                return;
            }

            Color32[] sourceSpritePixels = state.ResetPixelsSnapshot;
            RectInt sourceBounds;
            bool hasSourceBounds = TryGetVisibleBounds(sourceSpritePixels, state.Width, state.Height, out sourceBounds);
            if (!hasSourceBounds)
            {
                sourceBounds = new RectInt(0, 0, state.Width, state.Height);
            }

            sourceBounds = ClampBoundsToTexture(sourceBounds, sourceFleshWidth, sourceFleshHeight);
            if (sourceBounds.width <= 0 || sourceBounds.height <= 0)
            {
                sourceBounds = new RectInt(0, 0, sourceFleshWidth, sourceFleshHeight);
            }

            for (int y = 0; y < safeHeight; y++)
            {
                int row = y * safeWidth;
                for (int x = 0; x < safeWidth; x++)
                {
                    int destinationIndex = row + x;
                    byte alpha = skinPixels != null && destinationIndex < skinPixels.Length ? skinPixels[destinationIndex].a : (byte)255;
                    if (alpha <= 0)
                    {
                        continue;
                    }

                    int sourceX = MapCoordinate(x, destinationBounds.xMin, Mathf.Max(1, destinationBounds.width), sourceBounds.xMin, sourceBounds.width);
                    int sourceY = MapCoordinate(y, destinationBounds.yMin, Mathf.Max(1, destinationBounds.height), sourceBounds.yMin, sourceBounds.height);
                    int fleshIndex = Mathf.Clamp(sourceY, 0, sourceFleshHeight - 1) * sourceFleshWidth + Mathf.Clamp(sourceX, 0, sourceFleshWidth - 1);
                    int boneX = Mathf.RoundToInt((sourceX / (float)Mathf.Max(1, sourceFleshWidth - 1)) * Mathf.Max(0, sourceBoneWidth - 1));
                    int boneY = Mathf.RoundToInt((sourceY / (float)Mathf.Max(1, sourceFleshHeight - 1)) * Mathf.Max(0, sourceBoneHeight - 1));
                    int boneIndex = Mathf.Clamp(boneY, 0, sourceBoneHeight - 1) * sourceBoneWidth + Mathf.Clamp(boneX, 0, sourceBoneWidth - 1);

                    fleshPixels[destinationIndex] = sourceFleshPixels[fleshIndex];
                    bonePixels[destinationIndex] = sourceBonePixels[boneIndex];
                }
            }
        }

        private static RectInt ClampBoundsToTexture(RectInt bounds, int width, int height)
        {
            int xMin = Mathf.Clamp(bounds.xMin, 0, Mathf.Max(0, width));
            int yMin = Mathf.Clamp(bounds.yMin, 0, Mathf.Max(0, height));
            int xMax = Mathf.Clamp(bounds.xMax, 0, Mathf.Max(0, width));
            int yMax = Mathf.Clamp(bounds.yMax, 0, Mathf.Max(0, height));
            return new RectInt(xMin, yMin, Mathf.Max(0, xMax - xMin), Mathf.Max(0, yMax - yMin));
        }

        private static int MapCoordinate(int destinationValue, int destinationStart, int destinationLength, int sourceStart, int sourceLength)
        {
            if (destinationLength <= 1 || sourceLength <= 1)
            {
                return sourceStart;
            }

            float normalized = (destinationValue - destinationStart) / (float)(destinationLength - 1);
            return sourceStart + Mathf.RoundToInt(Mathf.Clamp01(normalized) * (sourceLength - 1));
        }

        private static bool TryBuildTransferredPixels(
            Color32[] sourcePixels,
            int sourceWidth,
            int sourceHeight,
            Vector2Int? sourcePivot,
            LimbSpriteState state,
            out Color32[] transferredPixels)
        {
            transferredPixels = null;
            if (sourcePixels == null || state == null || !state.IsValid)
            {
                return false;
            }

            int targetWidth = state.Width;
            int targetHeight = state.Height;
            if (targetWidth <= 0 || targetHeight <= 0)
            {
                return false;
            }

            RectInt sourceBounds;
            bool hasSourceBounds = TryGetVisibleBounds(sourcePixels, sourceWidth, sourceHeight, out sourceBounds);
            if (!hasSourceBounds)
            {
                transferredPixels = new Color32[targetWidth * targetHeight];
                return true;
            }

            Vector2Int targetPivot = state.OriginalSprite != null
                ? new Vector2Int(
                    Mathf.RoundToInt(state.OriginalSprite.pivot.x),
                    Mathf.RoundToInt(state.OriginalSprite.pivot.y))
                : new Vector2Int(targetWidth / 2, targetHeight / 2);

            if (sourcePivot.HasValue)
            {
                Vector2Int pivotOffset = targetPivot - sourcePivot.Value;
                if (TryCopyVisiblePixels(sourcePixels, sourceWidth, sourceHeight, pivotOffset, targetWidth, targetHeight, out transferredPixels))
                {
                    return true;
                }
            }

            RectInt targetVisibleBounds;
            bool hasTargetVisibleBounds = TryGetVisibleBounds(state.ResetPixelsSnapshot, targetWidth, targetHeight, out targetVisibleBounds);
            RectInt targetAnchorBounds = hasTargetVisibleBounds
                ? targetVisibleBounds
                : new RectInt(0, 0, targetWidth, targetHeight);
            RectInt fullTargetBounds = new RectInt(0, 0, targetWidth, targetHeight);

            if (sourcePivot.HasValue)
            {
                Vector2Int pivotOffset = targetPivot - sourcePivot.Value;
                if (TryCopyVisiblePixelsClampedToBounds(
                    sourcePixels,
                    sourceWidth,
                    sourceHeight,
                    sourceBounds,
                    pivotOffset,
                    fullTargetBounds,
                    targetWidth,
                    targetHeight,
                    out transferredPixels))
                {
                    return true;
                }
            }

            Vector2Int centeredOffset = CalculateCenteredOffset(sourceBounds, targetAnchorBounds);
            if (TryCopyVisiblePixels(sourcePixels, sourceWidth, sourceHeight, centeredOffset, targetWidth, targetHeight, out transferredPixels))
            {
                return true;
            }

            centeredOffset = CalculateCenteredOffset(sourceBounds, fullTargetBounds);
            if (TryCopyVisiblePixels(sourcePixels, sourceWidth, sourceHeight, centeredOffset, targetWidth, targetHeight, out transferredPixels))
            {
                return true;
            }

            if (TryScaleVisiblePixelsToBounds(
                sourcePixels,
                sourceWidth,
                sourceHeight,
                sourceBounds,
                sourcePivot ?? new Vector2Int(sourceBounds.x + sourceBounds.width / 2, sourceBounds.y + sourceBounds.height / 2),
                fullTargetBounds,
                targetPivot,
                targetWidth,
                targetHeight,
                true,
                MinimumAutoFitScale,
                out transferredPixels))
            {
                return true;
            }

            return false;
        }

        private static Vector2Int CalculateCenteredOffset(RectInt sourceBounds, RectInt targetBounds)
        {
            float sourceCenterX = sourceBounds.x + (sourceBounds.width - 1) * 0.5f;
            float sourceCenterY = sourceBounds.y + (sourceBounds.height - 1) * 0.5f;
            float targetCenterX = targetBounds.x + (targetBounds.width - 1) * 0.5f;
            float targetCenterY = targetBounds.y + (targetBounds.height - 1) * 0.5f;

            return new Vector2Int(
                Mathf.RoundToInt(targetCenterX - sourceCenterX),
                Mathf.RoundToInt(targetCenterY - sourceCenterY));
        }

        private static bool TryCopyVisiblePixels(
            Color32[] sourcePixels,
            int sourceWidth,
            int sourceHeight,
            Vector2Int offset,
            int targetWidth,
            int targetHeight,
            out Color32[] targetPixels)
        {
            targetPixels = new Color32[targetWidth * targetHeight];

            for (int y = 0; y < sourceHeight; y++)
            {
                int sourceRow = y * sourceWidth;
                for (int x = 0; x < sourceWidth; x++)
                {
                    Color32 pixel = sourcePixels[sourceRow + x];
                    if (pixel.a <= 0)
                    {
                        continue;
                    }

                    int targetX = x + offset.x;
                    int targetY = y + offset.y;
                    if (targetX < 0 || targetY < 0 || targetX >= targetWidth || targetY >= targetHeight)
                    {
                        targetPixels = null;
                        return false;
                    }

                    targetPixels[targetY * targetWidth + targetX] = pixel;
                }
            }

            return true;
        }

        private static bool TryCopyVisiblePixelsClampedToBounds(
            Color32[] sourcePixels,
            int sourceWidth,
            int sourceHeight,
            RectInt sourceBounds,
            Vector2Int preferredOffset,
            RectInt targetBounds,
            int targetWidth,
            int targetHeight,
            out Color32[] targetPixels)
        {
            int minOffsetX = targetBounds.xMin - sourceBounds.xMin;
            int maxOffsetX = targetBounds.xMax - sourceBounds.xMax;
            int minOffsetY = targetBounds.yMin - sourceBounds.yMin;
            int maxOffsetY = targetBounds.yMax - sourceBounds.yMax;

            if (minOffsetX > maxOffsetX || minOffsetY > maxOffsetY)
            {
                targetPixels = null;
                return false;
            }

            Vector2Int clampedOffset = new Vector2Int(
                Mathf.Clamp(preferredOffset.x, minOffsetX, maxOffsetX),
                Mathf.Clamp(preferredOffset.y, minOffsetY, maxOffsetY));

            return TryCopyVisiblePixels(sourcePixels, sourceWidth, sourceHeight, clampedOffset, targetWidth, targetHeight, out targetPixels);
        }

        private static bool TryScaleVisiblePixelsToBounds(
            Color32[] sourcePixels,
            int sourceWidth,
            int sourceHeight,
            RectInt sourceBounds,
            Vector2Int sourcePivot,
            RectInt targetAnchorBounds,
            Vector2Int targetPivot,
            int targetWidth,
            int targetHeight,
            bool preserveAspect,
            float minimumScale,
            out Color32[] targetPixels)
        {
            targetPixels = new Color32[targetWidth * targetHeight];
            if (sourceBounds.width <= 0 || sourceBounds.height <= 0 || targetAnchorBounds.width <= 0 || targetAnchorBounds.height <= 0)
            {
                return false;
            }

            float scaleX = (float)targetAnchorBounds.width / sourceBounds.width;
            float scaleY = (float)targetAnchorBounds.height / sourceBounds.height;
            if (scaleX <= 0f || scaleY <= 0f)
            {
                return false;
            }

            float usedScaleX = preserveAspect ? Mathf.Min(scaleX, scaleY) : scaleX;
            float usedScaleY = preserveAspect ? Mathf.Min(scaleX, scaleY) : scaleY;
            if (usedScaleX < minimumScale || usedScaleY < minimumScale)
            {
                return false;
            }

            int destWidth = Mathf.Max(1, Mathf.RoundToInt(sourceBounds.width * usedScaleX));
            int destHeight = Mathf.Max(1, Mathf.RoundToInt(sourceBounds.height * usedScaleY));

            if (destWidth > targetAnchorBounds.width || destHeight > targetAnchorBounds.height)
            {
                if (preserveAspect)
                {
                    return false;
                }

                destWidth = Mathf.Min(destWidth, targetAnchorBounds.width);
                destHeight = Mathf.Min(destHeight, targetAnchorBounds.height);
            }

            float sourcePivotNormX = sourceBounds.width > 1
                ? Mathf.Clamp01((sourcePivot.x - sourceBounds.x) / (float)(sourceBounds.width - 1))
                : 0.5f;
            float sourcePivotNormY = sourceBounds.height > 1
                ? Mathf.Clamp01((sourcePivot.y - sourceBounds.y) / (float)(sourceBounds.height - 1))
                : 0.5f;

            int destPivotX = Mathf.RoundToInt(sourcePivotNormX * Mathf.Max(0, destWidth - 1));
            int destPivotY = Mathf.RoundToInt(sourcePivotNormY * Mathf.Max(0, destHeight - 1));

            int destStartX = targetPivot.x - destPivotX;
            int destStartY = targetPivot.y - destPivotY;

            int minStartX = targetAnchorBounds.xMin;
            int maxStartX = targetAnchorBounds.xMax - destWidth;
            int minStartY = targetAnchorBounds.yMin;
            int maxStartY = targetAnchorBounds.yMax - destHeight;

            if (maxStartX < minStartX || maxStartY < minStartY)
            {
                return false;
            }

            destStartX = Mathf.Clamp(destStartX, minStartX, maxStartX);
            destStartY = Mathf.Clamp(destStartY, minStartY, maxStartY);

            for (int destY = 0; destY < destHeight; destY++)
            {
                int targetY = destStartY + destY;
                if (targetY < 0 || targetY >= targetHeight)
                {
                    return false;
                }

                float normalizedY = destHeight > 1 ? destY / (float)(destHeight - 1) : 0f;
                int sourceY = sourceBounds.y + Mathf.Clamp(Mathf.RoundToInt(normalizedY * Mathf.Max(0, sourceBounds.height - 1)), 0, Mathf.Max(0, sourceBounds.height - 1));

                for (int destX = 0; destX < destWidth; destX++)
                {
                    int targetX = destStartX + destX;
                    if (targetX < 0 || targetX >= targetWidth)
                    {
                        return false;
                    }

                    float normalizedX = destWidth > 1 ? destX / (float)(destWidth - 1) : 0f;
                    int sourceX = sourceBounds.x + Mathf.Clamp(Mathf.RoundToInt(normalizedX * Mathf.Max(0, sourceBounds.width - 1)), 0, Mathf.Max(0, sourceBounds.width - 1));
                    Color32 pixel = sourcePixels[sourceY * sourceWidth + sourceX];
                    if (pixel.a > 0)
                    {
                        targetPixels[targetY * targetWidth + targetX] = pixel;
                    }
                }
            }

            return true;
        }

        private static bool TryGetVisibleBounds(Color32[] pixels, int width, int height, out RectInt bounds)
        {
            bounds = new RectInt();
            if (pixels == null || pixels.Length != width * height || width <= 0 || height <= 0)
            {
                return false;
            }

            int minX = width;
            int minY = height;
            int maxX = -1;
            int maxY = -1;

            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                for (int x = 0; x < width; x++)
                {
                    if (pixels[row + x].a <= 0)
                    {
                        continue;
                    }

                    if (x < minX) minX = x;
                    if (y < minY) minY = y;
                    if (x > maxX) maxX = x;
                    if (y > maxY) maxY = y;
                }
            }

            if (maxX < minX || maxY < minY)
            {
                return false;
            }

            bounds = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
            return true;
        }

        private static string BuildPresetDisplayName(string sourceName, string scopeLabel, bool partOnly)
        {
            string cleanedSource = string.IsNullOrWhiteSpace(sourceName) ? "Preset" : sourceName.Trim();
            string cleanedScope = string.IsNullOrWhiteSpace(scopeLabel) ? (partOnly ? "Patch" : "Full") : scopeLabel.Trim();
            return cleanedSource + " " + cleanedScope + " " + DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss");
        }

        private static string CreateUniquePresetDirectory(string displayName, string timestamp)
        {
            string baseName = MakeSafeName(displayName);
            if (string.IsNullOrEmpty(baseName))
            {
                baseName = "preset";
            }

            return CreateUniqueDirectoryPath(GetPresetsRoot(), baseName + "_" + timestamp);
        }

        private static string CreateUniqueDirectoryPath(string root, string name)
        {
            string safeName = MakeSafeName(name);
            if (string.IsNullOrEmpty(safeName))
            {
                safeName = "item";
            }

            string candidate = Path.Combine(root, safeName);
            int index = 2;
            while (Directory.Exists(candidate))
            {
                candidate = Path.Combine(root, safeName + "_" + index);
                index++;
            }

            return candidate;
        }

        private static string CreateExportDirectory(string timestamp)
        {
            string exportDirectory = CreateUniqueDirectoryPath(GetPngFilesRoot(), "Export at " + timestamp);
            Directory.CreateDirectory(exportDirectory);
            return exportDirectory;
        }

        private static string CreateUniqueFilePath(string root, string fileName)
        {
            string safeFileName = MakeSafeName(Path.GetFileNameWithoutExtension(fileName)) + Path.GetExtension(fileName);
            string candidate = Path.Combine(root, safeFileName);
            int index = 2;

            while (File.Exists(candidate))
            {
                candidate = Path.Combine(
                    root,
                    MakeSafeName(Path.GetFileNameWithoutExtension(fileName)) + "_" + index + Path.GetExtension(fileName));
                index++;
            }

            return candidate;
        }

        private static SpritePresetBundleMetadata CreatePngExportMetadata(string targetName, string scope, string timestamp)
        {
            SpritePresetBundleMetadata metadata = new SpritePresetBundleMetadata();
            metadata.name = "PNG Export";
            metadata.version = "1";
            metadata.targetKind = "character";
            metadata.scope = scope ?? string.Empty;
            metadata.sourceName = targetName ?? string.Empty;
            metadata.createdAt = timestamp ?? string.Empty;
            return metadata;
        }

        private static SpritePresetEntryMetadata CreatePngExportEntry(LimbSpriteState state, string partId, string partLabel, string fileName)
        {
            SpritePresetEntryMetadata entry = new SpritePresetEntryMetadata();
            entry.partId = partId ?? string.Empty;
            entry.partLabel = partLabel ?? string.Empty;
            entry.variantKey = state != null ? SpriteVariantKeyUtility.GetKey(state.OriginalSprite) : string.Empty;
            entry.width = state != null ? state.Width : 0;
            entry.height = state != null ? state.Height : 0;
            entry.pivotX = state != null && state.OriginalSprite != null ? Mathf.RoundToInt(state.OriginalSprite.pivot.x) : 0;
            entry.pivotY = state != null && state.OriginalSprite != null ? Mathf.RoundToInt(state.OriginalSprite.pivot.y) : 0;
            entry.pixelsPerUnit = state != null && state.OriginalSprite != null ? state.OriginalSprite.pixelsPerUnit : 1f;
            entry.pngPath = fileName ?? string.Empty;
            entry.includesHitbox = false;
            return entry;
        }

        private static void WriteLayerStackSidecar(LimbSpriteState state, string skinOutputPath, SpritePresetEntryMetadata entry)
        {
            if (state == null || entry == null || string.IsNullOrEmpty(skinOutputPath))
            {
                return;
            }

            byte[] bytes = HypermodifiedSpritePersistence.EncodeLayerStack(state.CaptureLayerStackSnapshot());
            if (bytes == null || bytes.Length == 0)
            {
                return;
            }

            string directory = Path.GetDirectoryName(skinOutputPath);
            string baseName = MakeSafeName(Path.GetFileNameWithoutExtension(skinOutputPath));
            if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(baseName))
            {
                return;
            }

            string layerFileName = baseName + ".layers.dat";
            string layerPath = Path.Combine(directory, layerFileName);
            try
            {
                File.WriteAllBytes(layerPath, bytes);
                entry.layerStackPath = layerFileName;
            }
            catch (Exception exception)
            {
                TryDeleteFile(layerPath);
                Debug.LogWarning("[Hypermodified] Failed to write layer stack sidecar: " + exception.Message);
            }
        }

        private static void WriteBodyLayerSidecars(LimbSpriteState state, string skinOutputPath, SpritePresetEntryMetadata entry)
        {
            if (state == null || entry == null || string.IsNullOrEmpty(skinOutputPath))
            {
                return;
            }

            Color32[] fleshPixels;
            int fleshWidth;
            int fleshHeight;
            Color32[] bonePixels;
            int boneWidth;
            int boneHeight;
            if (!state.TryGetBodyLayerSnapshots(
                out fleshPixels,
                out fleshWidth,
                out fleshHeight,
                out bonePixels,
                out boneWidth,
                out boneHeight))
            {
                return;
            }

            string directory = Path.GetDirectoryName(skinOutputPath);
            string baseName = MakeSafeName(Path.GetFileNameWithoutExtension(skinOutputPath));
            if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(baseName))
            {
                return;
            }

            string fleshFileName = baseName + ".flesh.layer";
            string boneFileName = baseName + ".bone.layer";
            string fleshPath = Path.Combine(directory, fleshFileName);
            string bonePath = Path.Combine(directory, boneFileName);

            if (WritePixelsToPng(fleshPixels, fleshWidth, fleshHeight, fleshPath) &&
                WritePixelsToPng(bonePixels, boneWidth, boneHeight, bonePath))
            {
                entry.fleshPngPath = fleshFileName;
                entry.bonePngPath = boneFileName;
            }
            else
            {
                TryDeleteFile(fleshPath);
                TryDeleteFile(bonePath);
            }
        }

        private static void WriteDamageLayerSidecar(LimbSpriteState state, string skinOutputPath, SpritePresetEntryMetadata entry)
        {
            if (state == null || entry == null || string.IsNullOrEmpty(skinOutputPath))
            {
                return;
            }

            Color32[] damagePixels;
            int damageWidth;
            int damageHeight;
            if (!state.TryGetDamageLayerSnapshot(out damagePixels, out damageWidth, out damageHeight))
            {
                return;
            }

            string directory = Path.GetDirectoryName(skinOutputPath);
            string baseName = MakeSafeName(Path.GetFileNameWithoutExtension(skinOutputPath));
            if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(baseName))
            {
                return;
            }

            string damageFileName = baseName + ".damage.layer";
            string damagePath = Path.Combine(directory, damageFileName);
            if (WritePixelsToPng(damagePixels, damageWidth, damageHeight, damagePath))
            {
                entry.damagePngPath = damageFileName;
            }
            else
            {
                TryDeleteFile(damagePath);
            }
        }

        private static void WritePngExportManifest(string exportDirectory, SpritePresetBundleMetadata metadata)
        {
            if (string.IsNullOrEmpty(exportDirectory) || metadata == null)
            {
                return;
            }

            string serialized = SerializePngExportManifest(metadata);
            if (string.IsNullOrWhiteSpace(serialized))
            {
                return;
            }

            try
            {
                File.WriteAllText(Path.Combine(exportDirectory, PngManifestFileName), serialized);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Hypermodified] Failed to write PNG export metadata: " + exception.Message);
            }
        }

        private static string SerializePngExportManifest(SpritePresetBundleMetadata metadata)
        {
            if (metadata == null)
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder();
            builder.Append("H\t");
            builder.Append(EncodeManifestField(metadata.version));
            builder.Append('\t');
            builder.Append(EncodeManifestField(metadata.targetKind));
            builder.Append('\t');
            builder.Append(EncodeManifestField(metadata.scope));
            builder.Append('\t');
            builder.Append(EncodeManifestField(metadata.sourceName));
            builder.Append('\t');
            builder.Append(EncodeManifestField(metadata.createdAt));
            builder.AppendLine();

            if (metadata.entries != null)
            {
                for (int i = 0; i < metadata.entries.Count; i++)
                {
                    SpritePresetEntryMetadata entry = metadata.entries[i];
                    if (entry == null)
                    {
                        continue;
                    }

                    builder.Append("E\t");
                    builder.Append(EncodeManifestField(entry.partId));
                    builder.Append('\t');
                    builder.Append(EncodeManifestField(entry.partLabel));
                    builder.Append('\t');
                    builder.Append(EncodeManifestField(entry.pngPath));
                    builder.Append('\t');
                    builder.Append(entry.width);
                    builder.Append('\t');
                    builder.Append(entry.height);
                    builder.Append('\t');
                    builder.Append(entry.pivotX);
                    builder.Append('\t');
                    builder.Append(entry.pivotY);
                    builder.Append('\t');
                    builder.Append(entry.pixelsPerUnit.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    builder.Append('\t');
                    builder.Append(EncodeManifestField(entry.fleshPngPath));
                    builder.Append('\t');
                    builder.Append(EncodeManifestField(entry.bonePngPath));
                    builder.Append('\t');
                    builder.Append(EncodeManifestField(entry.damagePngPath));
                    builder.Append('\t');
                    builder.Append(EncodeManifestField(entry.layerStackPath));
                    builder.AppendLine();
                }
            }

            return builder.ToString();
        }

        private static SpritePresetBundleMetadata DeserializePngExportManifest(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            string[] lines = text.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length <= 0 || !lines[0].StartsWith("H\t", StringComparison.Ordinal))
            {
                return null;
            }

            SpritePresetBundleMetadata metadata = new SpritePresetBundleMetadata();
            metadata.name = "PNG Export";
            for (int i = 0; i < lines.Length; i++)
            {
                string[] parts = lines[i].Split('\t');
                if (parts.Length <= 0)
                {
                    continue;
                }

                if (parts[0] == "H" && parts.Length >= 6)
                {
                    metadata.version = DecodeManifestField(parts[1]);
                    metadata.targetKind = DecodeManifestField(parts[2]);
                    metadata.scope = DecodeManifestField(parts[3]);
                    metadata.sourceName = DecodeManifestField(parts[4]);
                    metadata.createdAt = DecodeManifestField(parts[5]);
                }
                else if (parts[0] == "E" && parts.Length >= 11)
                {
                    SpritePresetEntryMetadata entry = new SpritePresetEntryMetadata();
                    entry.partId = DecodeManifestField(parts[1]);
                    entry.partLabel = DecodeManifestField(parts[2]);
                    entry.pngPath = DecodeManifestField(parts[3]);
                    entry.width = ParseInt(parts[4]);
                    entry.height = ParseInt(parts[5]);
                    entry.pivotX = ParseInt(parts[6]);
                    entry.pivotY = ParseInt(parts[7]);
                    entry.pixelsPerUnit = ParseFloat(parts[8], 1f);
                    entry.fleshPngPath = DecodeManifestField(parts[9]);
                    entry.bonePngPath = DecodeManifestField(parts[10]);
                    entry.damagePngPath = parts.Length >= 12 ? DecodeManifestField(parts[11]) : string.Empty;
                    entry.layerStackPath = parts.Length >= 13 ? DecodeManifestField(parts[12]) : string.Empty;
                    metadata.entries.Add(entry);
                }
            }

            return metadata;
        }

        private static string EncodeManifestField(string value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));
        }

        private static string DecodeManifestField(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            try
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(value));
            }
            catch
            {
                return string.Empty;
            }
        }

        private static int ParseInt(string value)
        {
            int result;
            return int.TryParse(value, out result) ? result : 0;
        }

        private static float ParseFloat(string value, float fallback)
        {
            float result;
            return float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out result)
                ? result
                : fallback;
        }

        private static SpritePresetBundleMetadata TryLoadPngExportManifest(string[] filePaths)
        {
            if (filePaths == null)
            {
                return null;
            }

            HashSet<string> checkedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < filePaths.Length; i++)
            {
                string filePath = filePaths[i];
                if (string.IsNullOrEmpty(filePath))
                {
                    continue;
                }

                string directory = Path.GetDirectoryName(filePath);
                if (string.IsNullOrEmpty(directory) || !checkedDirectories.Add(directory))
                {
                    continue;
                }

                string manifestPath = Path.Combine(directory, PngManifestFileName);
                if (!File.Exists(manifestPath))
                {
                    continue;
                }

                try
                {
                    string manifestText = File.ReadAllText(manifestPath);
                    SpritePresetBundleMetadata metadata = DeserializePngExportManifest(manifestText);
                    if (metadata == null)
                    {
                        metadata = DeserializeMetadata(manifestText);
                    }

                    if (metadata != null && metadata.entries != null && metadata.entries.Count > 0)
                    {
                        return metadata;
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("[Hypermodified] Failed to read PNG export metadata: " + exception.Message);
                }
            }

            return null;
        }

        private static SpritePresetEntryMetadata FindPngManifestEntry(SpritePresetBundleMetadata metadata, string filePath)
        {
            if (metadata == null || metadata.entries == null || string.IsNullOrEmpty(filePath))
            {
                return null;
            }

            string fileName = Path.GetFileName(filePath);
            for (int i = 0; i < metadata.entries.Count; i++)
            {
                SpritePresetEntryMetadata entry = metadata.entries[i];
                if (entry == null)
                {
                    continue;
                }

                if (string.Equals(Path.GetFileName(entry.pngPath), fileName, StringComparison.OrdinalIgnoreCase))
                {
                    return entry;
                }
            }

            return null;
        }

        private static bool WritePixelsToPng(Color32[] pixels, int width, int height, string outputPath)
        {
            if (pixels == null || pixels.Length != width * height || width <= 0 || height <= 0)
            {
                return false;
            }

            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixels32(pixels);
            texture.Apply();

            byte[] bytes = EncodeTextureToPng(texture);
            UnityEngine.Object.Destroy(texture);

            if (bytes == null || bytes.Length <= 0)
            {
                return false;
            }

            File.WriteAllBytes(outputPath, bytes);
            return true;
        }

        private static Texture2D LoadTexture(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    return null;
                }

                FileInfo fileInfo = new FileInfo(filePath);
                if (!fileInfo.Exists || fileInfo.Length <= 0 || fileInfo.Length > MaxImportPngBytes)
                {
                    return null;
                }

                byte[] bytes = File.ReadAllBytes(filePath);
                if (bytes == null || bytes.Length <= 0)
                {
                    return null;
                }

                int pngWidth;
                int pngHeight;
                if (!TryReadPngDimensions(bytes, out pngWidth, out pngHeight) ||
                    pngWidth <= 0 ||
                    pngHeight <= 0 ||
                    pngWidth > MaxImportTextureDimension ||
                    pngHeight > MaxImportTextureDimension)
                {
                    return null;
                }

                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                texture.filterMode = FilterMode.Point;
                texture.wrapMode = TextureWrapMode.Clamp;

                if (!LoadTextureFromBytes(texture, bytes))
                {
                    UnityEngine.Object.Destroy(texture);
                    return null;
                }

                texture.filterMode = FilterMode.Point;
                texture.wrapMode = TextureWrapMode.Clamp;
                return texture;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Hypermodified] Could not load sprite PNG '" + filePath + "': " + exception.Message);
                return null;
            }
        }

        private static bool TryReadPngDimensions(byte[] bytes, out int width, out int height)
        {
            width = 0;
            height = 0;

            if (bytes == null || bytes.Length < 24)
            {
                return false;
            }

            if (bytes[0] != 137 ||
                bytes[1] != 80 ||
                bytes[2] != 78 ||
                bytes[3] != 71 ||
                bytes[4] != 13 ||
                bytes[5] != 10 ||
                bytes[6] != 26 ||
                bytes[7] != 10)
            {
                return false;
            }

            width = (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19];
            height = (bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23];
            return width > 0 && height > 0;
        }

        private static string GetEntryPngPath(string presetDirectory, SpritePresetEntryMetadata entry)
        {
            string relativePath = entry != null ? entry.pngPath : null;
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return null;
            }

            return Path.Combine(presetDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
        {
            Directory.CreateDirectory(destinationDirectory);

            string[] files = Directory.GetFiles(sourceDirectory, "*", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < files.Length; i++)
            {
                string sourceFile = files[i];
                string destinationFile = Path.Combine(destinationDirectory, Path.GetFileName(sourceFile));
                File.Copy(sourceFile, destinationFile, true);
            }

            string[] subdirectories = Directory.GetDirectories(sourceDirectory, "*", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < subdirectories.Length; i++)
            {
                string sourceSubdirectory = subdirectories[i];
                string destinationSubdirectory = Path.Combine(destinationDirectory, Path.GetFileName(sourceSubdirectory));
                CopyDirectory(sourceSubdirectory, destinationSubdirectory);
            }
        }

        private static void CopyMissingDirectoryContents(string sourceDirectory, string destinationDirectory)
        {
            if (string.IsNullOrEmpty(sourceDirectory) ||
                string.IsNullOrEmpty(destinationDirectory) ||
                !Directory.Exists(sourceDirectory))
            {
                return;
            }

            Directory.CreateDirectory(destinationDirectory);

            string[] files;
            try
            {
                files = Directory.GetFiles(sourceDirectory, "*", SearchOption.TopDirectoryOnly);
            }
            catch
            {
                return;
            }

            for (int i = 0; i < files.Length; i++)
            {
                string sourceFile = files[i];
                string destinationFile = Path.Combine(destinationDirectory, Path.GetFileName(sourceFile));
                if (File.Exists(destinationFile))
                {
                    continue;
                }

                try
                {
                    File.Copy(sourceFile, destinationFile, false);
                }
                catch
                {
                }
            }

            string[] subdirectories;
            try
            {
                subdirectories = Directory.GetDirectories(sourceDirectory, "*", SearchOption.TopDirectoryOnly);
            }
            catch
            {
                return;
            }

            for (int i = 0; i < subdirectories.Length; i++)
            {
                string sourceSubdirectory = subdirectories[i];
                string destinationSubdirectory = Path.Combine(destinationDirectory, Path.GetFileName(sourceSubdirectory));
                CopyMissingDirectoryContents(sourceSubdirectory, destinationSubdirectory);
            }
        }

        private static void SafeDeleteDirectory(string directory)
        {
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }

        private static string MakeSafeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            char[] invalid = Path.GetInvalidFileNameChars();
            string safe = value.Trim();

            for (int i = 0; i < invalid.Length; i++)
            {
                safe = safe.Replace(invalid[i], '_');
            }

            safe = safe.Replace(' ', '_');
            while (safe.Contains("__"))
            {
                safe = safe.Replace("__", "_");
            }

            return safe.Trim('_');
        }

        private static LimbBehaviour FindBestLimbForPngFile(string filePath, CharacterSpriteEditorTarget target, List<LimbBehaviour> limbs, HashSet<LimbBehaviour> usedLimbs)
        {
            if (string.IsNullOrEmpty(filePath) || target == null || limbs == null)
            {
                return null;
            }

            string fileToken = NormalizeMatchToken(Path.GetFileNameWithoutExtension(filePath));
            LimbBehaviour best = null;
            int bestLength = 0;

            for (int i = 0; i < limbs.Count; i++)
            {
                LimbBehaviour limb = limbs[i];
                if (limb == null || (usedLimbs != null && usedLimbs.Contains(limb)))
                {
                    continue;
                }

                string partToken = NormalizeMatchToken(target.GetPartId(limb));
                string labelToken = NormalizeMatchToken(target.GetLimbDisplayName(limb));
                int score = 0;

                if (!string.IsNullOrEmpty(partToken) && fileToken.Contains(partToken))
                {
                    score = Mathf.Max(score, partToken.Length);
                }

                if (!string.IsNullOrEmpty(labelToken) && fileToken.Contains(labelToken))
                {
                    score = Mathf.Max(score, labelToken.Length);
                }

                if (score > bestLength)
                {
                    best = limb;
                    bestLength = score;
                }
            }

            return best;
        }

        private static LimbBehaviour FindCharacterLimb(CharacterSpriteEditorTarget target, SpritePresetEntryMetadata entry, HashSet<LimbBehaviour> usedLimbs)
        {
            if (target == null || entry == null)
            {
                return null;
            }

            LimbBehaviour limb = target.FindLimbByPartId(entry.partId);
            if (limb != null && (usedLimbs == null || !usedLimbs.Contains(limb)))
            {
                return limb;
            }

            List<LimbBehaviour> limbs = target.GetAvailableLimbs();
            for (int i = 0; i < limbs.Count; i++)
            {
                limb = limbs[i];
                if (limb == null || (usedLimbs != null && usedLimbs.Contains(limb)))
                {
                    continue;
                }

                if (string.Equals(target.GetLimbDisplayName(limb), entry.partLabel, StringComparison.Ordinal))
                {
                    return limb;
                }
            }

            return null;
        }

        private static string NormalizeMatchToken(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            string result = string.Empty;
            for (int i = 0; i < value.Length; i++)
            {
                char current = char.ToLowerInvariant(value[i]);
                if (char.IsLetterOrDigit(current))
                {
                    result += current;
                }
            }

            return result;
        }

        public static byte[] EncodeTextureToPng(Texture2D texture)
        {
            if (texture == null || ImageConversionType == null)
            {
                return null;
            }

            System.Reflection.MethodInfo method = ImageConversionType.GetMethod(
                "EncodeToPNG",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                null,
                new Type[] { typeof(Texture2D) },
                null);

            return method != null ? method.Invoke(null, new object[] { texture }) as byte[] : null;
        }

        private static bool LoadTextureFromBytes(Texture2D texture, byte[] bytes)
        {
            if (texture == null || bytes == null || ImageConversionType == null)
            {
                return false;
            }

            System.Reflection.MethodInfo method = ImageConversionType.GetMethod(
                "LoadImage",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                null,
                new Type[] { typeof(Texture2D), typeof(byte[]) },
                null);

            if (method != null)
            {
                return (bool)method.Invoke(null, new object[] { texture, bytes });
            }

            method = ImageConversionType.GetMethod(
                "LoadImage",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                null,
                new Type[] { typeof(Texture2D), typeof(byte[]), typeof(bool) },
                null);

            return method != null && (bool)method.Invoke(null, new object[] { texture, bytes, false });
        }

        private static string SerializeMetadata(SpritePresetBundleMetadata metadata)
        {
            if (metadata == null || JsonUtilityType == null)
            {
                return string.Empty;
            }

            System.Reflection.MethodInfo method = JsonUtilityType.GetMethod(
                "ToJson",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                null,
                new Type[] { typeof(object), typeof(bool) },
                null);

            if (method == null)
            {
                return string.Empty;
            }

            return method.Invoke(null, new object[] { metadata, true }) as string ?? string.Empty;
        }

        private static SpritePresetBundleMetadata DeserializeMetadata(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || JsonUtilityType == null)
            {
                return null;
            }

            System.Reflection.MethodInfo method = JsonUtilityType.GetMethod(
                "FromJson",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                null,
                new Type[] { typeof(string), typeof(Type) },
                null);

            if (method == null)
            {
                return null;
            }

            return method.Invoke(null, new object[] { json, typeof(SpritePresetBundleMetadata) }) as SpritePresetBundleMetadata;
        }
    }

    internal static class HypermodifiedModPathResolver
    {
        private const string LocalFolderName = "LiveSpriteEditor";
        private const string PeoplePlaygroundWorkshopAppId = "1118200";
        private const string PublishedWorkshopId = "3718619896";
        private static string cachedSourceFolder;

        public static string GetModSourceFolder()
        {
            if (!string.IsNullOrEmpty(cachedSourceFolder) && Directory.Exists(cachedSourceFolder))
            {
                return cachedSourceFolder;
            }

            cachedSourceFolder = FindModSourceFolder();
            return cachedSourceFolder;
        }

        public static string GetUserDataFolder()
        {
            try
            {
                string meta = ModAPI.Metadata.MetaLocation;
                if (!string.IsNullOrEmpty(meta))
                    return Path.Combine(meta, "LiveSpriteEditorData");
            }
            catch { }

            return Path.Combine(GetGameRoot(), "Mods", "Dragon Ball Singularity", "LiveSpriteEditorData");
        }

        private static string FindModSourceFolder()
        {
            List<string> candidates = new List<string>();
            string gameRoot = GetGameRoot();
            string modsRoot = Path.Combine(gameRoot, "Mods");
            string steamAppsRoot = Path.GetFullPath(Path.Combine(gameRoot, "..", ".."));
            string workshopRoot = Path.Combine(steamAppsRoot, "workshop", "content", PeoplePlaygroundWorkshopAppId);

            try
            {
                string meta = ModAPI.Metadata.MetaLocation;
                if (!string.IsNullOrEmpty(meta))
                {
                    AddCandidate(candidates, Path.Combine(meta, LocalFolderName));
                    AddCandidate(candidates, meta);
                }
            }
            catch { }

            AddCandidate(candidates, Path.Combine(modsRoot, "Dragon Ball Singularity", LocalFolderName));
            AddCandidate(candidates, Path.Combine(modsRoot, LocalFolderName));
            AddCandidate(candidates, Path.Combine(modsRoot, "Hypermodified-Live-Sprite-Editor"));
            AddDirectoryCandidates(candidates, modsRoot);
            AddCandidate(candidates, Path.Combine(workshopRoot, PublishedWorkshopId));
            AddDirectoryCandidates(candidates, workshopRoot);

            for (int i = 0; i < candidates.Count; i++)
            {
                if (IsThisModFolder(candidates[i]))
                {
                    return candidates[i];
                }
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                if (HasExpectedModFiles(candidates[i]))
                {
                    return candidates[i];
                }
            }

            try
            {
                string meta = ModAPI.Metadata.MetaLocation;
                if (!string.IsNullOrEmpty(meta))
                    return Path.Combine(meta, LocalFolderName);
            }
            catch { }

            return Path.Combine(modsRoot, "Dragon Ball Singularity", LocalFolderName);
        }

        private static string GetGameRoot()
        {
            try
            {
                return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            }
            catch
            {
                return Path.GetFullPath(".");
            }
        }

        private static void AddDirectoryCandidates(List<string> candidates, string root)
        {
            if (candidates == null || string.IsNullOrEmpty(root) || !Directory.Exists(root))
            {
                return;
            }

            string[] directories;
            try
            {
                directories = Directory.GetDirectories(root);
            }
            catch
            {
                return;
            }

            for (int i = 0; i < directories.Length; i++)
            {
                AddCandidate(candidates, directories[i]);
            }
        }

        private static void AddCandidate(List<string> candidates, string path)
        {
            if (candidates == null || string.IsNullOrEmpty(path))
            {
                return;
            }

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(path);
            }
            catch
            {
                return;
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                if (string.Equals(candidates[i], fullPath, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            candidates.Add(fullPath);
        }

        private static bool IsThisModFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                return false;
            }

            string metadataPath = Path.Combine(folder, "mod.json");
            if (!File.Exists(metadataPath))
            {
                return false;
            }

            string metadata;
            try
            {
                metadata = File.ReadAllText(metadataPath);
            }
            catch
            {
                return false;
            }

            return (ContainsThisModName(metadata) || ContainsThisWorkshopIdentity(metadata)) &&
                   HasExpectedModFiles(folder);
        }

        private static bool ContainsThisModName(string metadata)
        {
            return ContainsJsonStringValue(metadata, "Name", "Live Sprite Workshop") ||
                   ContainsJsonStringValue(metadata, "Name", "Live Sprite Editor");
        }

        private static bool ContainsThisWorkshopIdentity(string metadata)
        {
            return ContainsJsonStringValue(metadata, "UGCIdentity", PublishedWorkshopId) ||
                   ContainsJsonStringValue(metadata, "CreatorUGCIdentity", PublishedWorkshopId);
        }

        private static bool ContainsJsonStringValue(string metadata, string propertyName, string expectedValue)
        {
            if (string.IsNullOrEmpty(metadata) ||
                string.IsNullOrEmpty(propertyName) ||
                string.IsNullOrEmpty(expectedValue))
            {
                return false;
            }

            string propertyToken = "\"" + propertyName + "\"";
            int searchIndex = 0;
            while (searchIndex < metadata.Length)
            {
                int propertyIndex = metadata.IndexOf(propertyToken, searchIndex, StringComparison.OrdinalIgnoreCase);
                if (propertyIndex < 0)
                {
                    return false;
                }

                int colonIndex = metadata.IndexOf(':', propertyIndex + propertyToken.Length);
                if (colonIndex < 0)
                {
                    return false;
                }

                int valueStart = colonIndex + 1;
                while (valueStart < metadata.Length && char.IsWhiteSpace(metadata[valueStart]))
                {
                    valueStart++;
                }

                if (valueStart >= metadata.Length)
                {
                    return false;
                }

                if (metadata[valueStart] != '"')
                {
                    searchIndex = valueStart + 1;
                    continue;
                }

                int valueEnd = metadata.IndexOf('"', valueStart + 1);
                if (valueEnd < 0)
                {
                    return false;
                }

                string value = metadata.Substring(valueStart + 1, valueEnd - valueStart - 1);
                if (string.Equals(value, expectedValue, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                searchIndex = valueEnd + 1;
            }

            return false;
        }

        private static bool HasExpectedModFiles(string folder)
        {
            return !string.IsNullOrEmpty(folder) &&
                   Directory.Exists(folder) &&
                   Directory.Exists(Path.Combine(folder, "Assets")) &&
                   (File.Exists(Path.Combine(folder, "Assets", "pencil.png"))
                    || File.Exists(Path.Combine(folder, "SpriteEditorRuntime.cs"))
                    || File.Exists(Path.Combine(folder, "mod.json")));
        }
    }
}

// Originally uploaded by 'hypermodified'. Do not reupload without their explicit permission.
