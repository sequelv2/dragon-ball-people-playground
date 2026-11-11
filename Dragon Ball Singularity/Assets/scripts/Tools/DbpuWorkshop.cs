using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Events;

namespace DBPU
{
    public static class DbpuWorkshopContentType
    {
        public const string Transformation = "Transformation";
        public const string Move = "Move";
        public const string Skin = "Skin";
        public const string Character = "Character";
        public const string Fusion = "Fusion";
    }

    public class DbpuWorkshopPack
    {
        public string Format = "DBPU_Workshop_v1";
        public string ContentType = DbpuWorkshopContentType.Transformation;
        public string Title;
        public string Description;
        public string Author;
        public string CharacterId;
        public string CharacterDisplayName;
        public Dictionary<string, string> Assets = new Dictionary<string, string>();
        public JObject Payload;
    }

    public class DbpuWorkshopLocalEntry
    {
        public string ContentType;
        public string LocalKey;
        public ulong UgcIdentity;
        public bool Uploaded;
        public string Title;
        public string Description;
    }

    public class DbpuWorkshopLocalSave
    {
        public List<DbpuWorkshopLocalEntry> Entries = new List<DbpuWorkshopLocalEntry>();
    }

    public class DbpuWorkshopItemInfo
    {
        public ulong FileId;
        public string FolderPath;
        public string ManifestPath;
        public DbpuWorkshopPack Pack;
        public bool OwnedLocally;
    }

    public class DbpuWorkshopRemoteItem
    {
        public ulong FileId;
        public string Title;
        public string Description;
        public string Author;
        public string[] Tags;
        public bool IsSubscribed;
        public bool IsInstalled;
        public bool NeedsUpdate;
        public string Directory;
        public string PreviewUrl;
        public int VotesUp;
        public ulong NumSubscriptions;
        public string Url;
    }

    public class DbpuWorkshopRunner : MonoBehaviour
    {
        static DbpuWorkshopRunner _instance;

        public static DbpuWorkshopRunner Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("DBPU_WorkshopRunner");
                    UnityEngine.Object.DontDestroyOnLoad(go);
                    _instance = go.AddComponent<DbpuWorkshopRunner>();
                }
                return _instance;
            }
        }

        public void Run(IEnumerator routine)
        {
            if (routine == null) return;
            StartCoroutine(routine);
        }
    }

    public static class DbpuWorkshop
    {
        public const string ManifestFileName = "dbpu.json";
        public const string LocalSavePath = "DBPU_WorkshopLocal.json";
        public const string WorkshopRelativePath = "..\\..\\workshop\\content\\1118200";
        public const string TagMods = "Mods";
        public const string TagRoot = "Singularity Content";
        public const string DescHeader = "Workshop content for Dragon Ball: Singularity.";
        public const string ContentMarker = "[Singularity:";
        // Items uploaded by earlier builds carry the older headers, markers and title brands; keep recognising them.
        static readonly string[] DescHeaderPrefixes = { DescHeader, "DBI Workshop content for ", "DBPU Workshop content for " };
        static readonly string[] ContentMarkers = { ContentMarker, "[DBI:", "[DBPU:" };
        static readonly string[] LegacyTitleBrands = { "DBI", "DBPU" };
        public const string SearchNeedle = "Singularity";
        public const string BrandShort = "Singularity";

        static DbpuWorkshopLocalSave _local = new DbpuWorkshopLocalSave();
        static bool _loaded;

        public static string WorkshopContentRoot
        {
            get
            {
                string[] candidates =
                {
                    Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, WorkshopRelativePath)),
                    Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "workshop", "content", "1118200")),
                    Path.GetFullPath(Path.Combine(Application.dataPath, "..", "workshop", "content", "1118200"))
                };
                for (int i = 0; i < candidates.Length; i++)
                {
                    if (Directory.Exists(candidates[i]))
                        return candidates[i];
                }
                return candidates[0];
            }
        }

        public static void SafeOpenUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            try
            {
                if (DbpuSteamGate.IsOverlayEnabled)
                    DbpuSteamGate.OpenWebOverlay(url, false);
                else
                    Application.OpenURL(url);
            }
            catch
            {
                try { Application.OpenURL(url); } catch { }
            }
        }

        public static void OpenWorkshopItemPage(ulong fileId)
        {
            if (fileId == 0) return;
            SafeOpenUrl("steam://url/CommunityFilePage/" + fileId);
        }

        public static void OpenWorkshopLegalAgreement()
        {
            SafeOpenUrl("https://steamcommunity.com/workshop/workshoplegalagreement/?appid=1118200");
        }

        static string StagingRoot
        {
            get
            {
                string root = Path.Combine(Path.GetTempPath(), "DBPU_Workshop");
                if (!Directory.Exists(root)) Directory.CreateDirectory(root);
                return root;
            }
        }

        static long DirectoryByteSize(string folder)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return 0;
            long total = 0;
            foreach (string file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
            {
                try { total += new FileInfo(file).Length; } catch { }
            }
            return total;
        }

        static void RememberSubmittedItem(string localKey, DbpuWorkshopPack pack, ulong fileId, bool uploaded)
        {
            if (string.IsNullOrEmpty(localKey) || pack == null || fileId == 0) return;
            UpsertLocal(new DbpuWorkshopLocalEntry
            {
                ContentType = pack.ContentType,
                LocalKey = localKey,
                UgcIdentity = fileId,
                Uploaded = uploaded,
                Title = pack.Title,
                Description = pack.Description
            });
        }

        public static bool LooksLikeDbpuItem(string title, string description)
        {
            if (!string.IsNullOrEmpty(description))
            {
                if (HasWorkshopHeader(description))
                    return true;
                for (int i = 0; i < ContentMarkers.Length; i++)
                {
                    if (description.IndexOf(ContentMarkers[i], StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }
            }
            // New uploads are recognised by their description; only old ones relied on the title brand.
            if (!string.IsNullOrEmpty(title))
            {
                for (int i = 0; i < LegacyTitleBrands.Length; i++)
                {
                    if (title.IndexOf(LegacyTitleBrands[i], StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }
            }
            return false;
        }

        public static bool HasWorkshopHeader(string description)
        {
            if (string.IsNullOrEmpty(description)) return false;
            for (int i = 0; i < DescHeaderPrefixes.Length; i++)
            {
                if (description.IndexOf(DescHeaderPrefixes[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        public static string MakeWorkshopTitle(string packTitle, string contentType)
        {
            string title = (packTitle ?? "Form") + " - " + BrandShort + " " + (contentType ?? "Content");
            if (title.Length > 128) title = title.Substring(0, 128);
            return title;
        }

        public static void LoadLocal()
        {
            _loaded = true;
            try
            {
                _local = ModAPI.DeserialiseJSON<DbpuWorkshopLocalSave>(LocalSavePath) ?? new DbpuWorkshopLocalSave();
            }
            catch
            {
                _local = new DbpuWorkshopLocalSave();
            }
            if (_local.Entries == null)
                _local.Entries = new List<DbpuWorkshopLocalEntry>();
        }

        public static void SaveLocal()
        {
            if (!_loaded) LoadLocal();
            if (_local == null) _local = new DbpuWorkshopLocalSave();
            if (_local.Entries == null) _local.Entries = new List<DbpuWorkshopLocalEntry>();
            ModAPI.SerialiseJSON(_local, LocalSavePath);
        }

        public static string MakeLocalKey(string contentType, string characterId, string itemId)
        {
            return (contentType ?? "") + "|" + (characterId ?? "") + "|" + (itemId ?? "");
        }

        public static DbpuWorkshopLocalEntry FindLocal(string localKey)
        {
            if (!_loaded) LoadLocal();
            if (string.IsNullOrEmpty(localKey) || _local?.Entries == null) return null;
            for (int i = 0; i < _local.Entries.Count; i++)
            {
                var e = _local.Entries[i];
                if (e != null && string.Equals(e.LocalKey, localKey, StringComparison.OrdinalIgnoreCase))
                    return e;
            }
            return null;
        }

        public static void UpsertLocal(DbpuWorkshopLocalEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.LocalKey)) return;
            if (!_loaded) LoadLocal();
            var existing = FindLocal(entry.LocalKey);
            if (existing == null)
                _local.Entries.Add(entry);
            else
            {
                existing.ContentType = entry.ContentType;
                existing.UgcIdentity = entry.UgcIdentity;
                existing.Uploaded = entry.Uploaded;
                existing.Title = entry.Title;
                existing.Description = entry.Description;
            }
            SaveLocal();
        }

        public static bool SteamReady
        {
            get
            {
                try { return DbpuSteamGate.IsValid && DbpuSteamGate.IsLoggedOn; }
                catch { return false; }
            }
        }

        public static string SteamName
        {
            get
            {
                try { return DbpuSteamGate.IsValid ? DbpuSteamGate.Name : "Unknown"; }
                catch { return "Unknown"; }
            }
        }

        public static string DescribeSteamState()
        {
            try
            {
                return "Steam logged=" + DbpuSteamGate.IsLoggedOn
                    + " app=" + DbpuSteamGate.AppId
                    + " user=" + DbpuSteamGate.SteamId;
            }
            catch (Exception ex)
            {
                return "Steam state unavailable: " + ex.Message;
            }
        }

        public static bool TryGetPublishBlockReason(out string reason)
        {
            reason = null;
            try
            {
                if (!DbpuSteamGate.IsValid || !DbpuSteamGate.IsLoggedOn)
                {
                    reason = "Steam is not logged on. Launch People Playground through Steam.";
                    return true;
                }
                if (Utils.IsFamilyShared())
                {
                    reason = "Workshop uploads are blocked on Family Shared copies of the game.";
                    return true;
                }
                if (DbpuSteamGate.AppId != 1118200)
                {
                    reason = "Steam AppId is " + DbpuSteamGate.AppId + " (expected 1118200). Workshop upload needs the Steam version of PPG.";
                    return true;
                }
            }
            catch (Exception ex)
            {
                reason = "Steam check failed: " + ex.Message;
                return true;
            }
            return false;
        }

        public static string AlphaNumeric(string value)
        {
            if (string.IsNullOrEmpty(value)) return "content";
            return Regex.Replace(value.Normalize(), "[^a-zA-Z0-9 -]", "").Trim();
        }

        public static string BuildWorkshopDescription(string userDescription, string contentType, string characterDisplayName)
        {
            string body = string.IsNullOrEmpty(userDescription) ? "(No description)" : userDescription.Trim();
            string type = string.IsNullOrEmpty(contentType) ? "Content" : contentType;
            return DescHeader + "\n"
                + ContentMarker + type + "]\n"
                + "Type: " + type + "\n"
                + "Character: " + (characterDisplayName ?? "Any") + "\n\n"
                + body;
        }

        public static string StagePackFolder(DbpuWorkshopPack pack, out string previewPath)
        {
            previewPath = null;
            if (pack == null) throw new InvalidOperationException("Nothing to pack.");
            if (string.IsNullOrEmpty(pack.Title)) pack.Title = "Untitled";
            if (string.IsNullOrEmpty(pack.ContentType)) pack.ContentType = DbpuWorkshopContentType.Transformation;
            if (pack.Assets == null) pack.Assets = new Dictionary<string, string>();

            // Stage under %TEMP% so Steam can read files even when PPG lives under Program Files.
            string tmpRoot = StagingRoot;

            string shortName = AlphaNumeric(pack.Title);
            if (shortName.Length > 12) shortName = shortName.Substring(0, 12);
            if (string.IsNullOrEmpty(shortName)) shortName = "pack";
            string folder = Path.GetFullPath(Path.Combine(tmpRoot, "dbpu_" + shortName + "_" + (DateTime.UtcNow.Ticks & 0xffffff).ToString("x6")));
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
            Directory.CreateDirectory(folder);
            string assetsDir = Path.Combine(folder, "assets");
            Directory.CreateDirectory(assetsDir);

            var remapped = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in pack.Assets.ToList())
            {
                if (string.IsNullOrEmpty(kvp.Value) || !File.Exists(kvp.Value)) continue;
                string ext = Path.GetExtension(kvp.Value);
                if (string.IsNullOrEmpty(ext)) ext = ".png";
                string fileName = AlphaNumeric(kvp.Key) + ext;
                string dest = Path.Combine(assetsDir, fileName);
                File.Copy(kvp.Value, dest, true);
                remapped[kvp.Key] = "assets/" + fileName;
            }
            pack.Assets = remapped;

            if (string.Equals(pack.ContentType, DbpuWorkshopContentType.Transformation, StringComparison.OrdinalIgnoreCase)
                && pack.Payload != null)
            {
                var form = pack.Payload.ToObject<CustomTransformationFormDto>();
                if (form != null)
                {
                    form.HairSpritePath = RemapAsset(form.HairSpritePath, remapped, "hair");
                    form.HeadSpritePath = RemapAsset(form.HeadSpritePath, remapped, "head");
                    form.IconPath = RemapAsset(form.IconPath, remapped, "icon");
                    form.IllustrationPath = RemapAsset(form.IllustrationPath, remapped, "illustration");
                    if (form.Accessories != null)
                    {
                        for (int i = 0; i < form.Accessories.Count; i++)
                        {
                            var acc = form.Accessories[i];
                            if (acc == null) continue;
                            acc.SpritePath = RemapAsset(acc.SpritePath, remapped, "acc_" + i);
                        }
                    }
                    pack.Payload = JObject.FromObject(form);
                }
            }
            else if (string.Equals(pack.ContentType, DbpuWorkshopContentType.Skin, StringComparison.OrdinalIgnoreCase)
                && pack.Payload != null)
            {
                var skin = pack.Payload.ToObject<SkinWorkshopPayload>();
                if (skin?.Files != null)
                {
                    // Keep destination relative paths; Assets already remapped to assets/*.
                    pack.Payload = JObject.FromObject(skin);
                }
            }

            File.WriteAllText(Path.Combine(folder, ManifestFileName), JsonConvert.SerializeObject(pack, Formatting.Indented));

            if (remapped.TryGetValue("icon", out string iconRel))
                previewPath = Path.Combine(folder, iconRel.Replace('/', Path.DirectorySeparatorChar));
            else if (remapped.TryGetValue("illustration", out string illRel))
                previewPath = Path.Combine(folder, illRel.Replace('/', Path.DirectorySeparatorChar));
            else if (remapped.TryGetValue("hair", out string hairRel))
                previewPath = Path.Combine(folder, hairRel.Replace('/', Path.DirectorySeparatorChar));
            else if (remapped.Count > 0)
            {
                var first = remapped.Values.FirstOrDefault(v =>
                    !string.IsNullOrEmpty(v) && v.EndsWith(".png", StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(first))
                    previewPath = Path.Combine(folder, first.Replace('/', Path.DirectorySeparatorChar));
            }

            return folder;
        }

        static string RemapAsset(string originalPath, Dictionary<string, string> remapped, string key)
        {
            if (remapped != null && remapped.TryGetValue(key, out string rel))
                return rel;
            return originalPath;
        }

        public static DbpuWorkshopPack BuildTransformationPack(
            string title,
            string description,
            string characterId,
            string characterDisplayName,
            CustomTransformationFormDto form)
        {
            if (form == null) throw new InvalidOperationException("No form to upload.");
            var clone = CustomTransformationCatalog.CloneForm(form);
            var assets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            AddAsset(assets, "hair", clone.HairSpritePath);
            AddAsset(assets, "head", clone.HeadSpritePath);
            AddAsset(assets, "icon", clone.IconPath);
            AddAsset(assets, "illustration", clone.IllustrationPath);
            if (clone.Accessories != null)
            {
                for (int i = 0; i < clone.Accessories.Count; i++)
                {
                    var acc = clone.Accessories[i];
                    if (acc == null) continue;
                    AddAsset(assets, "acc_" + i, acc.SpritePath);
                }
            }

            return new DbpuWorkshopPack
            {
                ContentType = DbpuWorkshopContentType.Transformation,
                Title = string.IsNullOrEmpty(title) ? (clone.Name ?? clone.ID) : title,
                Description = description ?? "",
                Author = SteamName,
                CharacterId = characterId,
                CharacterDisplayName = characterDisplayName,
                Assets = assets,
                Payload = JObject.FromObject(clone)
            };
        }

        static void AddAsset(Dictionary<string, string> assets, string key, string path)
        {
            if (assets == null || string.IsNullOrEmpty(key) || string.IsNullOrEmpty(path)) return;
            string abs = path;
            if (!File.Exists(abs))
                abs = CustomTransformationCatalog.ResolveModPath(path);
            if (!string.IsNullOrEmpty(abs) && File.Exists(abs))
                assets[key] = abs;
        }

        static string ToAbsolutePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            if (Path.IsPathRooted(path)) return Path.GetFullPath(path);
            return Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, path));
        }

        static void ClearLocalIdentity(string localKey)
        {
            if (string.IsNullOrEmpty(localKey)) return;
            var existing = FindLocal(localKey);
            if (existing == null) return;
            existing.UgcIdentity = 0;
            existing.Uploaded = false;
            SaveLocal();
        }

        public static IEnumerator PublishPack(
            DbpuWorkshopPack pack,
            string localKey,
            string customPreviewPath,
            bool forceNew,
            Action<bool, string, ulong> onDone)
        {
            // Mirrors Mod Profiles (Batrix) PublishProfile.
            if (pack == null)
            {
                onDone?.Invoke(false, "Nothing to upload.", 0);
                yield break;
            }
            if (!DbpuSteamGate.IsValid)
            {
                onDone?.Invoke(false, "Steam is not available.", 0);
                yield break;
            }
            if (!_loaded) LoadLocal();
            var local = FindLocal(localKey);
            // Dead stubs (Uploaded=false) must never block a new upload.
            if (local != null && local.UgcIdentity != 0 && !local.Uploaded && !forceNew)
            {
                ClearLocalIdentity(localKey);
                local = null;
                forceNew = true;
            }

            string previewSource = customPreviewPath;
            if (string.IsNullOrEmpty(previewSource) || !File.Exists(previewSource))
            {
                if (pack.Assets != null && pack.Assets.TryGetValue("icon", out string iconPath) && File.Exists(iconPath))
                    previewSource = iconPath;
            }
            if (string.IsNullOrEmpty(previewSource) || !File.Exists(previewSource))
            {
                try
                {
                    previewSource = CustomTransformationCatalog.ResolveModPath("Assets/Icons/Empty icon.png");
                }
                catch { }
            }
            if (string.IsNullOrEmpty(previewSource) || !File.Exists(previewSource))
            {
                onDone?.Invoke(false, "Preview image is required for Workshop upload.", 0);
                yield break;
            }
            previewSource = Path.GetFullPath(previewSource);
            try
            {
                if (new FileInfo(previewSource).Length > 1000000L)
                {
                    onDone?.Invoke(false, "Preview image must be under 1 MB.", 0);
                    yield break;
                }
            }
            catch { }

            // Preview outside content folder (Mod Profiles style), under %TEMP%.
            string prm = Path.GetFullPath(Path.Combine(StagingRoot, "thumb.png"));
            File.Copy(previewSource, prm, true);

            string staged;
            try
            {
                string ignored;
                staged = StagePackFolder(pack, out ignored);
            }
            catch (Exception ex)
            {
                onDone?.Invoke(false, "Pack failed: " + ex.Message, 0);
                yield break;
            }

            string loc = Path.GetFullPath(staged);
            long contentBytes = DirectoryByteSize(loc);
            if (contentBytes <= 0 || !File.Exists(Path.Combine(loc, ManifestFileName)))
            {
                onDone?.Invoke(false, "Staged workshop folder is empty. Nothing to upload.", 0);
                yield break;
            }
            if (!File.Exists(prm))
            {
                onDone?.Invoke(false, "Preview file missing after staging.", 0);
                yield break;
            }

            string desc = BuildWorkshopDescription(pack.Description, pack.ContentType, pack.CharacterDisplayName);
            string title = MakeWorkshopTitle(pack.Title, pack.ContentType);

            Debug.Log("DBPU Workshop staging content=" + loc + " bytes=" + contentBytes + " preview=" + prm);

            DbpuSteamPublishResult publishResult = null;
            DbpuSteamEditor editor = DbpuSteamGate.NewCommunityFile
                .WithTitle(title)
                .WithDescription(desc)
                .WithPreviewFile(prm)
                .WithContent(loc)
                .WithTag(TagMods)
                .WithPublicVisibility();

            ulong existingId = (local != null && !forceNew) ? local.UgcIdentity : 0UL;
            if (existingId != 0 && !forceNew)
            {
                object fetchRaw = null;
                DbpuSteamItem fetched = null;
                try
                {
                    fetchRaw = DbpuSteamGate.StartGetItem(existingId);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("Failed to retrieve original Workshop item: " + ex.Message);
                }
                if (fetchRaw != null)
                {
                    while (!DbpuSteamGate.IsTaskCompleted(fetchRaw)) yield return null;
                    if (!DbpuSteamGate.IsTaskFaulted(fetchRaw))
                    {
                        try { fetched = DbpuSteamGate.ReadItem(fetchRaw); }
                        catch (Exception ex) { Debug.LogWarning("Failed to read workshop item: " + ex.Message); }
                    }
                }
                if (fetched != null
                    && !string.IsNullOrEmpty(fetched.Title)
                    && LooksLikeDbpuItem(fetched.Title, fetched.Description ?? ""))
                {
                    editor = fetched.Edit();
                    string existingDesc = fetched.Description ?? "";
                    if (HasWorkshopHeader(existingDesc))
                        editor = editor.WithDescription(desc);
                    editor = editor.WithTitle(title).WithContent(loc).WithPreviewFile(prm);
                }
                else
                {
                    Debug.LogWarning("Failed to retrieve original Workshop item... creating a new one");
                    ClearLocalIdentity(localKey);
                    existingId = 0;
                    var retry = PublishPack(pack, localKey, customPreviewPath, true, onDone);
                    while (retry.MoveNext()) yield return retry.Current;
                    yield break;
                }
            }

            DialogBoxManager.Notification("Uploading... Please wait!");

            object submitRaw = null;
            try
            {
                submitRaw = editor.StartSubmit();
            }
            catch (Exception ex)
            {
                onDone?.Invoke(false, "Upload failed to start: " + ex.Message, 0);
                yield break;
            }
            while (!DbpuSteamGate.IsTaskCompleted(submitRaw)) yield return null;
            if (DbpuSteamGate.IsTaskFaulted(submitRaw))
            {
                Exception fault = DbpuSteamGate.GetTaskException(submitRaw);
                onDone?.Invoke(false, "Upload failed: " + (fault != null ? fault.Message : "unknown"), 0);
                yield break;
            }
            try
            {
                publishResult = DbpuSteamGate.ReadPublishResult(submitRaw);
            }
            catch (Exception ex)
            {
                onDone?.Invoke(false, "Upload result read failed: " + ex.Message, 0);
                yield break;
            }

            if (publishResult == null)
            {
                onDone?.Invoke(false, "Upload returned no result.", 0);
                yield break;
            }

            if (!publishResult.Ok)
            {
                if (!forceNew && existingId != 0
                    && publishResult.ResultText.IndexOf("FileNotFound", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    ClearLocalIdentity(localKey);
                    try { if (Directory.Exists(loc)) Directory.Delete(loc, true); } catch { }
                    DialogBoxManager.Notification("Previous workshop id was invalid. Creating a new item...");
                    var retry = PublishPack(pack, localKey, customPreviewPath, true, onDone);
                    while (retry.MoveNext()) yield return retry.Current;
                    yield break;
                }

                ClearLocalIdentity(localKey);
                onDone?.Invoke(false, "Could not create workshop item\n" + publishResult.ResultText, publishResult.FileId);
                yield break;
            }

            ulong fileId = publishResult.FileId;
            if (fileId == 0)
            {
                onDone?.Invoke(false, "Steam returned no workshop id.", 0);
                yield break;
            }

            RememberSubmittedItem(localKey, pack, fileId, false);

            if (publishResult.NeedsWorkshopAgreement)
            {
                OpenWorkshopItemPage(fileId);
                onDone?.Invoke(false,
                    "Steam flagged NeedsWorkshopAgreement for this submit.\n"
                    + "Open the Steam item page that just opened. If there is no agreement prompt, "
                    + "this is usually a failed content upload (Steam Cloud / path), not a missing agreement.\n"
                    + "Staged " + contentBytes + " bytes from:\n" + loc,
                    fileId);
                yield break;
            }

            DbpuSteamItem verify = null;
            for (int attempt = 0; attempt < 10; attempt++)
            {
                float wait = 0f;
                while (wait < 0.75f)
                {
                    wait += Time.unscaledDeltaTime;
                    yield return null;
                }
                object verifyRaw = null;
                try { verifyRaw = DbpuSteamGate.StartGetItem(fileId); }
                catch { }
                if (verifyRaw == null) continue;
                while (!DbpuSteamGate.IsTaskCompleted(verifyRaw)) yield return null;
                if (!DbpuSteamGate.IsTaskFaulted(verifyRaw))
                {
                    try { verify = DbpuSteamGate.ReadItem(verifyRaw); }
                    catch { verify = null; }
                }
                if (verify != null && !string.IsNullOrEmpty(verify.Title))
                    break;
            }

            OpenWorkshopItemPage(fileId);

            bool looksGood = verify != null && !string.IsNullOrEmpty(verify.Title);
            if (looksGood)
            {
                try { if (Directory.Exists(loc)) Directory.Delete(loc, true); } catch { }
                RememberSubmittedItem(localKey, pack, fileId, true);
                onDone?.Invoke(true, "Uploaded \"" + verify.Title + "\" (" + contentBytes + " bytes staged)", fileId);
                yield break;
            }

            RememberSubmittedItem(localKey, pack, fileId, false);
            onDone?.Invoke(false,
                "Steam created id " + fileId + " but the page is still blank/empty.\n"
                + "Local staging had " + contentBytes + " bytes, so this is a Steam upload failure, not missing files.\n"
                + "Check Steam → People Playground → Properties → General → Keep game saves in the Steam Cloud (enable it),\n"
                + "then fully restart Steam and upload again as NEW.\n"
                + "Staging left at:\n" + loc,
                fileId);
        }

        public static string TagForType(string contentType)
        {
            return BrandShort + " " + (string.IsNullOrEmpty(contentType) ? "Content" : contentType);
        }

        static bool MatchesContentTypeFilter(string title, string description, string contentTypeFilter)
        {
            if (string.IsNullOrEmpty(contentTypeFilter)) return true;
            if (!string.IsNullOrEmpty(description))
            {
                for (int i = 0; i < ContentMarkers.Length; i++)
                {
                    if (description.IndexOf(ContentMarkers[i] + contentTypeFilter + "]", StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }
            }
            if (!string.IsNullOrEmpty(title)
                && title.IndexOf(contentTypeFilter, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            return false;
        }

        public static List<DbpuWorkshopItemInfo> ScanSubscribed(string contentTypeFilter = null)
        {
            var list = new List<DbpuWorkshopItemInfo>();
            string root = WorkshopContentRoot;
            if (!Directory.Exists(root)) return list;

            if (!_loaded) LoadLocal();
            foreach (var dir in Directory.GetDirectories(root))
            {
                string manifest = Path.Combine(dir, ManifestFileName);
                if (!File.Exists(manifest)) continue;
                try
                {
                    var pack = JsonConvert.DeserializeObject<DbpuWorkshopPack>(File.ReadAllText(manifest));
                    if (pack == null) continue;
                    if (!string.IsNullOrEmpty(contentTypeFilter)
                        && !string.Equals(pack.ContentType, contentTypeFilter, StringComparison.OrdinalIgnoreCase))
                        continue;

                    ulong id;
                    if (!ulong.TryParse(Path.GetFileName(dir), out id)) id = 0;
                    bool owned = false;
                    if (_local?.Entries != null)
                    {
                        for (int i = 0; i < _local.Entries.Count; i++)
                        {
                            var e = _local.Entries[i];
                            if (e != null && e.UgcIdentity == id && e.Uploaded)
                            {
                                owned = true;
                                break;
                            }
                        }
                    }

                    list.Add(new DbpuWorkshopItemInfo
                    {
                        FileId = id,
                        FolderPath = dir,
                        ManifestPath = manifest,
                        Pack = pack,
                        OwnedLocally = owned
                    });
                }
                catch { }
            }

            return list.OrderBy(x => x.Pack?.Title ?? "", StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static void BeginQueryRemote(
            string contentTypeFilter,
            int page,
            string searchText,
            Action<bool, string, List<DbpuWorkshopRemoteItem>, int> onDone)
        {
            DbpuWorkshopRunner.Instance.Run(QueryRemoteRoutine(contentTypeFilter, page, searchText, onDone));
        }

        static IEnumerator QueryRemoteRoutine(
            string contentTypeFilter,
            int page,
            string searchText,
            Action<bool, string, List<DbpuWorkshopRemoteItem>, int> onDone)
        {
            if (!SteamReady)
            {
                onDone?.Invoke(false, "Steam is not available.", new List<DbpuWorkshopRemoteItem>(), 0);
                yield break;
            }

            if (page < 1) page = 1;
            List<DbpuWorkshopRemoteItem> items = new List<DbpuWorkshopRemoteItem>();
            int total = 0;

            string needle = searchText;
            if (string.IsNullOrEmpty(needle))
            {
                if (!string.IsNullOrEmpty(contentTypeFilter))
                    needle = contentTypeFilter;
                else
                    needle = SearchNeedle;
            }

            object pageTask = null;
            try
            {
                DbpuSteamQuery q = DbpuSteamGate.QueryAll
                    .WithTag(TagMods)
                    .WhereSearchText(needle)
                    .WithLongDescription(true);
                pageTask = q.StartGetPage(page);
            }
            catch (Exception ex)
            {
                onDone?.Invoke(false, "Workshop query failed: " + ex.Message, items, 0);
                yield break;
            }

            while (!DbpuSteamGate.IsTaskCompleted(pageTask))
                yield return null;

            if (DbpuSteamGate.IsTaskFaulted(pageTask))
            {
                Exception error = DbpuSteamGate.GetTaskException(pageTask);
                onDone?.Invoke(false, "Workshop query failed: " + (error != null ? error.Message : "unknown"), items, 0);
                yield break;
            }

            DbpuSteamResultPage pageResult = null;
            try
            {
                pageResult = DbpuSteamGate.ReadResultPage(pageTask);
            }
            catch (Exception ex)
            {
                onDone?.Invoke(false, "Workshop query failed: " + ex.Message, items, 0);
                yield break;
            }

            if (pageResult == null)
            {
                onDone?.Invoke(true, "No workshop items found.", items, 0);
                yield break;
            }

            try
            {
                total = pageResult.TotalCount;
                foreach (DbpuSteamItem entry in pageResult.Entries)
                {
                    string title = entry.Title ?? ("Item " + entry.Id);
                    string description = entry.Description ?? "";
                    if (!LooksLikeDbpuItem(title, description))
                        continue;
                    if (!MatchesContentTypeFilter(title, description, contentTypeFilter))
                        continue;

                    string author = "Unknown";
                    try { author = entry.OwnerName; } catch { }
                    items.Add(new DbpuWorkshopRemoteItem
                    {
                        FileId = entry.Id,
                        Title = title,
                        Description = description,
                        Author = author,
                        Tags = entry.Tags,
                        IsSubscribed = entry.IsSubscribed,
                        IsInstalled = entry.IsInstalled,
                        NeedsUpdate = entry.NeedsUpdate,
                        Directory = entry.Directory,
                        PreviewUrl = entry.PreviewImageUrl,
                        VotesUp = entry.VotesUp,
                        NumSubscriptions = entry.NumSubscriptions,
                        Url = entry.Url
                    });
                }
            }
            catch (Exception ex)
            {
                onDone?.Invoke(false, "Workshop query failed: " + ex.Message, items, 0);
                yield break;
            }

            onDone?.Invoke(true, items.Count == 0 ? "No workshop items found." : null, items, total);
        }

        public static void BeginSubscribeAndInstallTransformation(
            DbpuWorkshopRemoteItem remote,
            string targetCharacterId,
            string targetDisplayName,
            Action<bool, string, string> onDone)
        {
            DbpuWorkshopRunner.Instance.Run(SubscribeAndInstallRoutine(remote, targetCharacterId, targetDisplayName, onDone));
        }

        static IEnumerator SubscribeAndInstallRoutine(
            DbpuWorkshopRemoteItem remote,
            string targetCharacterId,
            string targetDisplayName,
            Action<bool, string, string> onDone)
        {
            if (remote == null || remote.FileId == 0)
            {
                onDone?.Invoke(false, "Invalid workshop item.", null);
                yield break;
            }
            if (!SteamReady)
            {
                onDone?.Invoke(false, "Steam is not available.", null);
                yield break;
            }

            DbpuSteamItem item = null;
            object fetchRaw = null;
            try { fetchRaw = DbpuSteamGate.StartGetItem(remote.FileId); }
            catch (Exception ex)
            {
                onDone?.Invoke(false, "Could not load workshop item: " + ex.Message, null);
                yield break;
            }
            while (!DbpuSteamGate.IsTaskCompleted(fetchRaw)) yield return null;
            if (DbpuSteamGate.IsTaskFaulted(fetchRaw))
            {
                Exception error = DbpuSteamGate.GetTaskException(fetchRaw);
                onDone?.Invoke(false, "Could not load workshop item: " + (error != null ? error.Message : "unknown"), null);
                yield break;
            }
            try { item = DbpuSteamGate.ReadItem(fetchRaw); }
            catch (Exception ex)
            {
                onDone?.Invoke(false, "Could not load workshop item: " + ex.Message, null);
                yield break;
            }

            if (item == null)
            {
                onDone?.Invoke(false, "Could not load workshop item.", null);
                yield break;
            }

            DbpuSteamItem ugc = item;

            if (!ugc.IsSubscribed)
            {
                object subRaw = ugc.StartSubscribe();
                while (!DbpuSteamGate.IsTaskCompleted(subRaw)) yield return null;
                if (DbpuSteamGate.IsTaskFaulted(subRaw))
                {
                    Exception fault = DbpuSteamGate.GetTaskException(subRaw);
                    onDone?.Invoke(false, "Subscribe failed: " + (fault != null ? fault.Message : "unknown"), null);
                    yield break;
                }
            }

            if (!ugc.IsInstalled || ugc.NeedsUpdate || string.IsNullOrEmpty(ugc.Directory) || !Directory.Exists(ugc.Directory))
            {
                object dlRaw = ugc.StartDownload(null, 100);
                float timeout = 120f;
                float elapsed = 0f;
                while (!DbpuSteamGate.IsTaskCompleted(dlRaw) && elapsed < timeout)
                {
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
                if (!DbpuSteamGate.IsTaskCompleted(dlRaw))
                {
                    onDone?.Invoke(false, "Download timed out.", null);
                    yield break;
                }
                if (DbpuSteamGate.IsTaskFaulted(dlRaw))
                {
                    Exception fault = DbpuSteamGate.GetTaskException(dlRaw);
                    onDone?.Invoke(false, "Download failed: " + (fault != null ? fault.Message : "unknown"), null);
                    yield break;
                }

                object refreshRaw = null;
                try { refreshRaw = DbpuSteamGate.StartGetItem(remote.FileId); }
                catch (Exception ex)
                {
                    onDone?.Invoke(false, "Download finished but item could not be refreshed: " + ex.Message, null);
                    yield break;
                }
                while (!DbpuSteamGate.IsTaskCompleted(refreshRaw)) yield return null;
                if (DbpuSteamGate.IsTaskFaulted(refreshRaw))
                {
                    onDone?.Invoke(false, "Download finished but item could not be refreshed.", null);
                    yield break;
                }
                try { item = DbpuSteamGate.ReadItem(refreshRaw); }
                catch
                {
                    onDone?.Invoke(false, "Download finished but item could not be refreshed.", null);
                    yield break;
                }
                if (item == null)
                {
                    onDone?.Invoke(false, "Download finished but item could not be refreshed.", null);
                    yield break;
                }
                ugc = item;
            }

            string folder = ugc.Directory;
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                string fallback = Path.Combine(WorkshopContentRoot, remote.FileId.ToString());
                if (Directory.Exists(fallback)) folder = fallback;
            }
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                onDone?.Invoke(false, "Workshop item folder not found after download.", null);
                yield break;
            }

            string manifest = Path.Combine(folder, ManifestFileName);
            if (!File.Exists(manifest))
            {
                onDone?.Invoke(false, "Not a DBPU pack (missing dbpu.json).", null);
                yield break;
            }

            DbpuWorkshopPack pack = null;
            try { pack = JsonConvert.DeserializeObject<DbpuWorkshopPack>(File.ReadAllText(manifest)); }
            catch (Exception ex)
            {
                onDone?.Invoke(false, "Invalid dbpu.json: " + ex.Message, null);
                yield break;
            }

            var info = new DbpuWorkshopItemInfo
            {
                FileId = remote.FileId,
                FolderPath = folder,
                ManifestPath = manifest,
                Pack = pack,
                OwnedLocally = false
            };

            try
            {
                string formId;
                var form = InstallTransformation(info, targetCharacterId, targetDisplayName, out formId);
                onDone?.Invoke(true, "Installed \"" + (form?.Name ?? formId) + "\"", formId);
            }
            catch (Exception ex)
            {
                onDone?.Invoke(false, "Install failed: " + ex.Message, null);
            }
        }

        public static void BeginSubscribeAndInstallSkin(
            DbpuWorkshopRemoteItem remote,
            string characterBasePath,
            CharacterSkins characterSkins,
            Action<bool, string, string> onDone)
        {
            DbpuWorkshopRunner.Instance.Run(SubscribeAndInstallSkinRoutine(remote, characterBasePath, characterSkins, onDone));
        }

        static IEnumerator SubscribeAndInstallSkinRoutine(
            DbpuWorkshopRemoteItem remote,
            string characterBasePath,
            CharacterSkins characterSkins,
            Action<bool, string, string> onDone)
        {
            if (remote == null || remote.FileId == 0)
            {
                onDone?.Invoke(false, "Invalid workshop item.", null);
                yield break;
            }
            if (!SteamReady)
            {
                onDone?.Invoke(false, "Steam is not available.", null);
                yield break;
            }

            DbpuSteamItem item = null;
            object fetchRaw = null;
            try { fetchRaw = DbpuSteamGate.StartGetItem(remote.FileId); }
            catch (Exception ex)
            {
                onDone?.Invoke(false, "Could not load workshop item: " + ex.Message, null);
                yield break;
            }
            while (!DbpuSteamGate.IsTaskCompleted(fetchRaw)) yield return null;
            if (DbpuSteamGate.IsTaskFaulted(fetchRaw))
            {
                onDone?.Invoke(false, "Could not load workshop item.", null);
                yield break;
            }
            try { item = DbpuSteamGate.ReadItem(fetchRaw); }
            catch
            {
                onDone?.Invoke(false, "Could not load workshop item.", null);
                yield break;
            }
            if (item == null)
            {
                onDone?.Invoke(false, "Could not load workshop item.", null);
                yield break;
            }

            if (!item.IsSubscribed)
            {
                object subRaw = item.StartSubscribe();
                while (!DbpuSteamGate.IsTaskCompleted(subRaw)) yield return null;
                if (DbpuSteamGate.IsTaskFaulted(subRaw))
                {
                    Exception fault = DbpuSteamGate.GetTaskException(subRaw);
                    onDone?.Invoke(false, "Subscribe failed: " + (fault != null ? fault.Message : "unknown"), null);
                    yield break;
                }
            }

            if (!item.IsInstalled || item.NeedsUpdate || string.IsNullOrEmpty(item.Directory) || !Directory.Exists(item.Directory))
            {
                object dlRaw = item.StartDownload(null, 100);
                float timeout = 120f;
                float elapsed = 0f;
                while (!DbpuSteamGate.IsTaskCompleted(dlRaw) && elapsed < timeout)
                {
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
                if (!DbpuSteamGate.IsTaskCompleted(dlRaw))
                {
                    onDone?.Invoke(false, "Download timed out.", null);
                    yield break;
                }
                if (DbpuSteamGate.IsTaskFaulted(dlRaw))
                {
                    Exception fault = DbpuSteamGate.GetTaskException(dlRaw);
                    onDone?.Invoke(false, "Download failed: " + (fault != null ? fault.Message : "unknown"), null);
                    yield break;
                }
                object refreshRaw = null;
                try { refreshRaw = DbpuSteamGate.StartGetItem(remote.FileId); }
                catch
                {
                    onDone?.Invoke(false, "Download finished but item could not be refreshed.", null);
                    yield break;
                }
                while (!DbpuSteamGate.IsTaskCompleted(refreshRaw)) yield return null;
                if (DbpuSteamGate.IsTaskFaulted(refreshRaw))
                {
                    onDone?.Invoke(false, "Download finished but item could not be refreshed.", null);
                    yield break;
                }
                try { item = DbpuSteamGate.ReadItem(refreshRaw); }
                catch
                {
                    onDone?.Invoke(false, "Download finished but item could not be refreshed.", null);
                    yield break;
                }
                if (item == null)
                {
                    onDone?.Invoke(false, "Download finished but item could not be refreshed.", null);
                    yield break;
                }
            }

            string folder = item.Directory;
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                string fallback = Path.Combine(WorkshopContentRoot, remote.FileId.ToString());
                if (Directory.Exists(fallback)) folder = fallback;
            }
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                onDone?.Invoke(false, "Workshop item folder not found after download.", null);
                yield break;
            }

            string manifest = Path.Combine(folder, ManifestFileName);
            if (!File.Exists(manifest))
            {
                onDone?.Invoke(false, "Not a DBPU pack (missing dbpu.json).", null);
                yield break;
            }

            DbpuWorkshopPack pack = null;
            try { pack = JsonConvert.DeserializeObject<DbpuWorkshopPack>(File.ReadAllText(manifest)); }
            catch (Exception ex)
            {
                onDone?.Invoke(false, "Invalid dbpu.json: " + ex.Message, null);
                yield break;
            }

            var info = new DbpuWorkshopItemInfo
            {
                FileId = remote.FileId,
                FolderPath = folder,
                ManifestPath = manifest,
                Pack = pack,
                OwnedLocally = false
            };

            try
            {
                string packFolder;
                InstallSkin(info, characterBasePath, characterSkins, out packFolder);
                onDone?.Invoke(true, "Installed skin \"" + (packFolder ?? "skin") + "\"", packFolder);
            }
            catch (Exception ex)
            {
                onDone?.Invoke(false, "Install failed: " + ex.Message, null);
            }
        }

        public static string InstallSkin(
            DbpuWorkshopItemInfo item,
            string characterBasePath,
            CharacterSkins characterSkins,
            out string packFolderName)
        {
            packFolderName = null;
            if (item?.Pack == null)
                throw new InvalidOperationException("Invalid workshop item.");
            if (!string.Equals(item.Pack.ContentType, DbpuWorkshopContentType.Skin, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("This item is not a skin.");
            if (item.Pack.Payload == null)
                throw new InvalidOperationException("Pack has no skin payload.");

            var payload = item.Pack.Payload.ToObject<SkinWorkshopPayload>();
            if (payload == null)
                throw new InvalidOperationException("Pack skin is invalid.");

            string basePath = characterBasePath;
            if (string.IsNullOrEmpty(basePath) && !string.IsNullOrEmpty(item.Pack.CharacterId))
                basePath = "Assets/Sprites/Characters/" + item.Pack.CharacterId + "/";
            if (string.IsNullOrEmpty(basePath))
                throw new InvalidOperationException("No character folder for skin install.");

            packFolderName = SkinShare.InstallWorkshopPayload(
                item.FolderPath,
                item.Pack.Assets,
                payload,
                basePath,
                characterSkins);
            return packFolderName;
        }

        public static CustomTransformationFormDto InstallTransformation(DbpuWorkshopItemInfo item, string targetCharacterId, string targetDisplayName, out string formId)
        {
            formId = null;
            if (item?.Pack == null)
                throw new InvalidOperationException("Invalid workshop item.");
            if (!string.Equals(item.Pack.ContentType, DbpuWorkshopContentType.Transformation, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("This item is not a transformation.");
            if (item.Pack.Payload == null)
                throw new InvalidOperationException("Pack has no transformation payload.");

            var form = item.Pack.Payload.ToObject<CustomTransformationFormDto>();
            if (form == null || string.IsNullOrEmpty(form.ID))
                throw new InvalidOperationException("Pack transformation is invalid.");

            string installRoot = Path.Combine(
                CustomTransformationCatalog.EnsureImportFolder(),
                "Workshop",
                item.FileId != 0 ? item.FileId.ToString() : DateTime.Now.Ticks.ToString("x"));
            if (!Directory.Exists(installRoot))
                Directory.CreateDirectory(installRoot);

            form.HairSpritePath = InstallAsset(item.FolderPath, form.HairSpritePath, installRoot, "hair");
            form.HeadSpritePath = InstallAsset(item.FolderPath, form.HeadSpritePath, installRoot, "head");
            form.IconPath = InstallAsset(item.FolderPath, form.IconPath, installRoot, "icon");
            form.IllustrationPath = InstallAsset(item.FolderPath, form.IllustrationPath, installRoot, "illustration");
            if (form.Accessories != null)
            {
                for (int i = 0; i < form.Accessories.Count; i++)
                {
                    var acc = form.Accessories[i];
                    if (acc == null) continue;
                    acc.SpritePath = InstallAsset(item.FolderPath, acc.SpritePath, installRoot, "acc_" + i);
                }
            }

            form.IsCustom = true;
            if (item.FileId != 0)
                form.ID = CustomTransformationCatalog.WorkshopFormId(item.FileId);
            else if (string.IsNullOrEmpty(form.ID) || !form.ID.StartsWith("Custom_", StringComparison.OrdinalIgnoreCase))
                form.ID = "Custom_" + DateTime.Now.Ticks.ToString("x");

            string charId = string.IsNullOrEmpty(targetCharacterId) ? item.Pack.CharacterId : targetCharacterId;
            string charName = string.IsNullOrEmpty(targetDisplayName) ? item.Pack.CharacterDisplayName : targetDisplayName;
            if (string.IsNullOrEmpty(charId)) charId = "Imported";

            CustomTransformationCatalog.UpsertForm(charId, charName, form);
            TransformationMakerHairLibrary.Invalidate();
            formId = form.ID;
            return form;
        }

        public static int ImportSubscribedTransformations()
        {
            int imported = 0;
            List<DbpuWorkshopItemInfo> items;
            try { items = ScanSubscribed(DbpuWorkshopContentType.Transformation); }
            catch { return 0; }
            if (items == null || items.Count == 0) return 0;

            for (int i = 0; i < items.Count; i++)
            {
                DbpuWorkshopItemInfo item = items[i];
                if (item?.Pack == null) continue;
                if (string.IsNullOrEmpty(item.Pack.CharacterId)) continue;
                try
                {
                    string formId;
                    InstallTransformation(item, item.Pack.CharacterId, item.Pack.CharacterDisplayName, out formId);
                    if (!string.IsNullOrEmpty(formId))
                        imported++;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[DB Singularity] Workshop transform import failed for "
                        + (item.FileId != 0 ? item.FileId.ToString() : item.FolderPath) + ": " + ex.Message);
                }
            }
            return imported;
        }

        static string InstallAsset(string packFolder, string relativeOrAbsolute, string installRoot, string stem)
        {
            if (string.IsNullOrEmpty(relativeOrAbsolute)) return null;
            string src = relativeOrAbsolute;
            if (!File.Exists(src))
                src = Path.Combine(packFolder, relativeOrAbsolute.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(src))
                src = Path.Combine(packFolder, Path.GetFileName(relativeOrAbsolute));
            if (!File.Exists(src)) return null;

            string ext = Path.GetExtension(src);
            if (string.IsNullOrEmpty(ext)) ext = ".png";
            string dest = Path.Combine(installRoot, stem + ext);
            File.Copy(src, dest, true);
            return dest;
        }

        public static void BeginPublishTransformation(
            string characterId,
            string displayName,
            CustomTransformationFormDto form,
            string title,
            string description,
            string previewPath,
            Action<bool, string> onDone)
        {
            if (form == null)
            {
                onDone?.Invoke(false, "No form selected.");
                return;
            }
            var pack = BuildTransformationPack(title, description, characterId, displayName, form);
            string key = MakeLocalKey(DbpuWorkshopContentType.Transformation, characterId, form.ID);
            // Always create a new workshop item. Updating reused dead stub ids from failed
            // uploads (e.g. 3766741677) just reopens an inaccessible Steam page.
            DbpuWorkshopRunner.Instance.Run(PublishPack(pack, key, previewPath, true, (ok, msg, id) =>
            {
                onDone?.Invoke(ok, msg);
            }));
        }

        public static DbpuWorkshopPack BuildSkinPack(
            string title,
            string description,
            string characterId,
            string characterDisplayName,
            string characterBasePath,
            string packFolderName,
            string skinName)
        {
            Dictionary<string, string> assets;
            var payload = SkinShare.BuildWorkshopPayload(characterBasePath, packFolderName, skinName, out assets);
            return new DbpuWorkshopPack
            {
                ContentType = DbpuWorkshopContentType.Skin,
                Title = string.IsNullOrEmpty(title) ? (payload.SkinName ?? packFolderName) : title,
                Description = description ?? "",
                Author = SteamName,
                CharacterId = characterId,
                CharacterDisplayName = characterDisplayName,
                Assets = assets,
                Payload = JObject.FromObject(payload)
            };
        }

        public static void BeginPublishSkin(
            string characterId,
            string displayName,
            string characterBasePath,
            string packFolderName,
            string skinName,
            string title,
            string description,
            string previewPath,
            Action<bool, string> onDone)
        {
            if (string.IsNullOrEmpty(characterBasePath) || string.IsNullOrEmpty(packFolderName))
            {
                onDone?.Invoke(false, "No skin selected.");
                return;
            }
            var pack = BuildSkinPack(title, description, characterId, displayName, characterBasePath, packFolderName, skinName);
            string key = MakeLocalKey(DbpuWorkshopContentType.Skin, characterId, packFolderName);
            DbpuWorkshopRunner.Instance.Run(PublishPack(pack, key, previewPath, true, (ok, msg, id) =>
            {
                onDone?.Invoke(ok, msg);
            }));
        }
    }
}
