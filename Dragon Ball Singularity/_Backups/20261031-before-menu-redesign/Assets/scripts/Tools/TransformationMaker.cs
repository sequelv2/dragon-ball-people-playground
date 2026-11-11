using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Application = UnityEngine.Application;
using Button = UnityEngine.UI.Button;
using Image = UnityEngine.UI.Image;
using Screen = UnityEngine.Screen;

namespace DBPU
{
    public class DbpuCharacterRegistryEntry
    {
        public string CharacterId;
        public string DisplayName;
        public string SpawnableName;
        public string ThumbPath;
    }

    public static class DbpuCharacterRegistry
    {
        static readonly List<DbpuCharacterRegistryEntry> Entries = new List<DbpuCharacterRegistryEntry>();
        static readonly HashSet<string> Seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static void Register(string characterId, string displayName, string thumbPath)
        {
            if (string.IsNullOrEmpty(characterId) || string.IsNullOrEmpty(displayName)) return;
            if (!Seen.Add(characterId)) return;
            Entries.Add(new DbpuCharacterRegistryEntry
            {
                CharacterId = characterId,
                DisplayName = displayName,
                SpawnableName = "[DBPU] " + displayName,
                ThumbPath = thumbPath
            });
        }

        public static List<DbpuCharacterRegistryEntry> GetAllSorted()
        {
            return Entries.OrderBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static DbpuCharacterRegistryEntry Find(string characterId)
        {
            if (string.IsNullOrEmpty(characterId)) return null;
            for (int i = 0; i < Entries.Count; i++)
                if (string.Equals(Entries[i].CharacterId, characterId, StringComparison.OrdinalIgnoreCase))
                    return Entries[i];
            return null;
        }
    }

    public class CustomTransformationFormDto
    {
        public string ID;
        public string Name;
        public string Description;
        public float PowerMultiplier = 1f;
        public float Size = 1.75f;
        public float KiDrainRate;
        public string AuraID = "Base";
        public bool UseCustomAura;
        public float AuraColorR = 1f;
        public float AuraColorG = 0.95f;
        public float AuraColorB = 0.35f;
        public float AuraColor2R = 1f;
        public float AuraColor2G = 1f;
        public float AuraColor2B = 0.75f;
        public bool UseAuraSecondaryColor;
        public float AuraScale = 1f;
        public string HairSpritePath;
        public string HeadSpritePath;
        public Dictionary<string, string> LimbSpritePaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public string IconPath;
        public string IllustrationPath;
        public string RequiredSkin_Suffix = "";
        public float HairColorR = 1f;
        public float HairColorG = 1f;
        public float HairColorB = 1f;
        public float HairLocalX;
        public float HairLocalY;
        public float HairLocalZ;
        public bool UseHairLocalPosition;
        public bool UseKiBarColor;
        public float KiBarColorR = 0.2f;
        public float KiBarColorG = 0.85f;
        public float KiBarColorB = 1f;
        public List<CustomAccessoryDto> Accessories = new List<CustomAccessoryDto>();
        public bool IsCustom = true;
    }

    public class CustomAccessoryDto
    {
        public string Id;
        public string Name = "Accessory";
        public string LimbName = "Head";
        public string SpritePath;
        public float LocalX;
        public float LocalY;
        public float LocalZ;
        public float Scale = 1f;
        public float RotationZ;
        public int SortingOrder = 1;
    }

    public class CustomTransformationCharacterDto
    {
        public string DisplayName;
        public Dictionary<string, CustomTransformationFormDto> Forms = new Dictionary<string, CustomTransformationFormDto>();
        public List<string> FormOrder = new List<string>();
    }

    public static class HairRecolorUtility
    {
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>(StringComparer.Ordinal);

        public static bool IsDefaultTint(Color c)
        {
            return c.r > 0.97f && c.g > 0.97f && c.b > 0.97f;
        }

        public static Sprite RecolorPreserveShading(Sprite source, Color target)
        {
            if (source == null || source.texture == null) return source;
            if (IsDefaultTint(target)) return source;

            string key = source.GetInstanceID() + "|" + target.r.ToString("0.###") + "," + target.g.ToString("0.###") + "," + target.b.ToString("0.###");
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;

            Texture2D readable = MakeReadable(source.texture);
            if (readable == null) return source;

            Rect rect = source.rect;
            int x0 = Mathf.RoundToInt(rect.x);
            int y0 = Mathf.RoundToInt(rect.y);
            int w = Mathf.Max(1, Mathf.RoundToInt(rect.width));
            int h = Mathf.Max(1, Mathf.RoundToInt(rect.height));
            var src = readable.GetPixels(x0, y0, w, h);
            var dst = new Color[src.Length];

            Color shadow = new Color(target.r * 0.22f, target.g * 0.22f, target.b * 0.22f, 1f);
            Color mid = target;
            Color highlight = Color.Lerp(target, Color.white, 0.62f);

            for (int i = 0; i < src.Length; i++)
            {
                Color p = src[i];
                if (p.a < 0.02f)
                {
                    dst[i] = p;
                    continue;
                }
                float lum = Mathf.Clamp01(0.2126f * p.r + 0.7152f * p.g + 0.0722f * p.b);
                float curve = lum * lum * (3f - 2f * lum);
                Color mapped = curve < 0.5f
                    ? Color.Lerp(shadow, mid, curve * 2f)
                    : Color.Lerp(mid, highlight, (curve - 0.5f) * 2f);
                mapped.a = p.a;
                dst[i] = mapped;
            }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = source.texture.filterMode;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.SetPixels(dst);
            tex.Apply(false, false);

            var spr = Sprite.Create(
                tex,
                new Rect(0f, 0f, w, h),
                new Vector2(source.pivot.x / Mathf.Max(1f, rect.width), source.pivot.y / Mathf.Max(1f, rect.height)),
                source.pixelsPerUnit,
                0,
                SpriteMeshType.FullRect);
            Cache[key] = spr;
            return spr;
        }

        static Texture2D MakeReadable(Texture2D source)
        {
            if (source == null) return null;
            try
            {
                if (source.isReadable) return source;
            }
            catch { }
            try
            {
                var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
                copy.filterMode = FilterMode.Point;
                copy.wrapMode = source.wrapMode;
                copy.SetPixels32(source.GetPixels32());
                copy.Apply(false, false);
                return copy;
            }
            catch
            {
                try
                {
                    var tmp = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Linear);
                    Graphics.Blit(source, tmp);
                    var prev = RenderTexture.active;
                    RenderTexture.active = tmp;
                    var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
                    copy.ReadPixels(new Rect(0, 0, tmp.width, tmp.height), 0, 0);
                    copy.Apply(false, false);
                    RenderTexture.active = prev;
                    RenderTexture.ReleaseTemporary(tmp);
                    return copy;
                }
                catch { return null; }
            }
        }
    }

    public static class DbpuMakerPhysics
    {
        public static void CalmPerson(PersonBehaviour person)
        {
            if (person?.Limbs == null) return;
            for (int i = 0; i < person.Limbs.Length; i++)
            {
                var limb = person.Limbs[i];
                if (limb == null) continue;
                var rb = limb.PhysicalBehaviour != null ? limb.PhysicalBehaviour.rigidbody : limb.GetComponent<Rigidbody2D>();
                if (rb == null || rb.bodyType == RigidbodyType2D.Static) continue;
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
                try { rb.Sleep(); } catch { }
            }
        }

        public static void StripWorldPhysics(GameObject root)
        {
            if (root == null) return;
            var rbs = root.GetComponentsInChildren<Rigidbody2D>(true);
            for (int i = 0; i < rbs.Length; i++)
            {
                var rb = rbs[i];
                if (rb == null) continue;
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
                rb.simulated = false;
                rb.bodyType = RigidbodyType2D.Kinematic;
            }
            var cols = root.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] != null) cols[i].enabled = false;
            }
            var phys = root.GetComponentsInChildren<PhysicalBehaviour>(true);
            for (int i = 0; i < phys.Length; i++)
            {
                if (phys[i] != null) phys[i].enabled = false;
            }
        }

        public static GameObject MakeDialogPhysicsHost(string name)
        {
            var host = new GameObject(string.IsNullOrEmpty(name) ? "DbpuDialogPhysHost" : name);
            host.transform.position = new Vector3(100000f, 100000f, 0f);
            var phys = host.AddComponent<PhysicalBehaviour>();
            StripWorldPhysics(host);
            return host;
        }
    }

    public static class TmHairApply
    {
        public static void ClearHairGlow(Transform hair)
        {
            if (hair == null) return;
            var glow = hair.GetComponent<TransformationHairGlow>();
            if (glow != null) UnityEngine.Object.DestroyImmediate(glow);
            for (int i = hair.childCount - 1; i >= 0; i--)
            {
                var child = hair.GetChild(i);
                if (child == null) continue;
                string n = child.name;
                if (n.IndexOf("Glow", StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("TransformationHair", StringComparison.OrdinalIgnoreCase) >= 0)
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }

        public static Transform EnsureSingleHair(Transform head)
        {
            if (head == null) return null;
            Transform keep = null;
            for (int i = head.childCount - 1; i >= 0; i--)
            {
                var child = head.GetChild(i);
                if (child == null) continue;
                if (!string.Equals(child.name, "Hair", StringComparison.OrdinalIgnoreCase)) continue;
                if (keep == null) keep = child;
                else UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
            if (keep == null)
            {
                var go = new GameObject("Hair");
                go.transform.SetParent(head, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                var sr = go.AddComponent<SpriteRenderer>();
                var headSr = head.GetComponent<SpriteRenderer>();
                if (headSr != null)
                {
                    sr.sortingLayerName = headSr.sortingLayerName;
                    sr.sortingOrder = headSr.sortingOrder + 1;
                }
                keep = go.transform;
            }
            ClearHairGlow(keep);
            return keep;
        }

        public static void ApplyHair(Transform head, Sprite sprite, Color tint, Vector3 localPos, bool useLocalPos)
        {
            var hair = EnsureSingleHair(head);
            if (hair == null) return;
            var sr = hair.GetComponent<SpriteRenderer>();
            if (sr == null) sr = hair.gameObject.AddComponent<SpriteRenderer>();
            var headSr = head.GetComponent<SpriteRenderer>();
            if (headSr != null)
            {
                sr.sortingLayerName = headSr.sortingLayerName;
                if (sr.sortingOrder <= headSr.sortingOrder)
                    sr.sortingOrder = headSr.sortingOrder + 1;
            }
            if (sprite != null) sr.sprite = sprite;
            sr.color = tint;
            sr.enabled = sprite != null || sr.sprite != null;
            hair.gameObject.SetActive(true);
            hair.localPosition = useLocalPos ? localPos : Vector3.zero;
            ClearHairGlow(hair);
        }
    }

    public static class TmInputGuard
    {
        static int _depth;

        public static void Push()
        {
            if (_depth == 0)
            {
                try { Global.main.AddUiBlocker(); } catch { }
            }
            _depth++;
        }

        public static void Pop()
        {
            if (_depth <= 0) return;
            _depth--;
            if (_depth == 0)
            {
                try { Global.main.RemoveUiBlocker(); } catch { }
            }
        }

        public static void Bind(TMP_InputField field)
        {
            if (field == null) return;
            field.onSelect.AddListener(_ => Push());
            field.onDeselect.AddListener(_ => Pop());
        }

        public static void ResetAll()
        {
            while (_depth > 0) Pop();
        }
    }

    public static class TmFilePicker
    {
        static bool _visualStylesTried;
        static int _openDepth;

        public static bool IsOpen => _openDepth > 0;

        public static string OpenFile(string title, string filter, string initialDir = null)
        {
            return ShowFileDialog(false, title, filter, null, initialDir);
        }

        public static string SaveFile(string title, string filter, string defaultName, string initialDir = null)
        {
            return ShowFileDialog(true, title, filter, defaultName, initialDir);
        }

        static string ShowFileDialog(bool save, string title, string filter, string defaultName, string initialDir)
        {
            if (_openDepth > 0)
                return null;

            string result = null;
            Exception error = null;
            var done = new ManualResetEvent(false);
            _openDepth++;
            try
            {
                var thread = new Thread(() =>
                {
                    try
                    {
                        if (!_visualStylesTried)
                        {
                            _visualStylesTried = true;
                            try { System.Windows.Forms.Application.EnableVisualStyles(); }
                            catch { }
                        }

                        using (var owner = new Form())
                        {
                            owner.TopMost = true;
                            owner.ShowInTaskbar = false;
                            owner.FormBorderStyle = FormBorderStyle.None;
                            owner.StartPosition = FormStartPosition.Manual;
                            owner.Location = new System.Drawing.Point(-20000, -20000);
                            owner.Size = new System.Drawing.Size(1, 1);
                            owner.Opacity = 0;
                            owner.Show();

                            if (save)
                            {
                                using (var dlg = new SaveFileDialog())
                                {
                                    ConfigureDialog(dlg, title, filter, defaultName, initialDir, true);
                                    if (dlg.ShowDialog(owner) == DialogResult.OK)
                                        result = dlg.FileName;
                                }
                            }
                            else
                            {
                                using (var dlg = new OpenFileDialog())
                                {
                                    ConfigureDialog(dlg, title, filter, defaultName, initialDir, false);
                                    if (dlg.ShowDialog(owner) == DialogResult.OK)
                                        result = dlg.FileName;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        error = ex;
                    }
                    finally
                    {
                        done.Set();
                    }
                });
                thread.SetApartmentState(ApartmentState.STA);
                thread.IsBackground = true;
                thread.Start();
                while (!done.WaitOne(30))
                { }
            }
            finally
            {
                _openDepth--;
            }
            if (error != null && result == null)
                throw error;
            return result;
        }

        static void ConfigureDialog(FileDialog dlg, string title, string filter, string defaultName, string initialDir, bool save)
        {
            dlg.Title = title ?? (save ? "Save" : "Open");
            dlg.Filter = filter ?? "All files (*.*)|*.*";
            dlg.RestoreDirectory = true;
            dlg.AutoUpgradeEnabled = true;
            dlg.ShowHelp = false;
            dlg.SupportMultiDottedExtensions = true;
            dlg.AddExtension = true;

            if (save)
            {
                var saveDlg = (SaveFileDialog)dlg;
                saveDlg.FileName = defaultName ?? "";
                saveDlg.OverwritePrompt = true;
                saveDlg.CheckFileExists = false;
                saveDlg.CheckPathExists = true;
                if (!string.IsNullOrEmpty(defaultName))
                {
                    string ext = Path.GetExtension(defaultName);
                    if (!string.IsNullOrEmpty(ext))
                        saveDlg.DefaultExt = ext.TrimStart('.');
                }
                dlg.InitialDirectory = !string.IsNullOrEmpty(initialDir) && Directory.Exists(initialDir)
                    ? initialDir
                    : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }
            else
            {
                var openDlg = (OpenFileDialog)dlg;
                openDlg.CheckFileExists = true;
                openDlg.Multiselect = false;
                openDlg.ShowReadOnly = false;
                dlg.InitialDirectory = !string.IsNullOrEmpty(initialDir) && Directory.Exists(initialDir)
                    ? initialDir
                    : Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            }
        }
    }

    public class TransformationSharePack
    {
        public string Format = "DBPU_TransformShare_v1";
        public string CharacterId;
        public string DisplayName;
        public CustomTransformationFormDto Form;
    }

    public static class TransformationShare
    {
        public const string FileFilter = "Singularity Transformation (*.dbputf)|*.dbputf|Zip Archive (*.zip)|*.zip|All files (*.*)|*.*";

        public static void ExportForm(string characterId, string displayName, CustomTransformationFormDto form)
        {
            if (form == null || string.IsNullOrEmpty(form.ID))
                throw new InvalidOperationException("Nothing to export.");

            string safeName = Sanitize(form.Name ?? form.ID) + ".dbputf";
            string dest = TmFilePicker.SaveFile("Share Transformation", FileFilter, safeName);
            if (string.IsNullOrEmpty(dest)) return;
            if (!dest.EndsWith(".dbputf", StringComparison.OrdinalIgnoreCase)
                && !dest.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                dest += ".dbputf";

            string tempRoot = Path.Combine(Path.GetTempPath(), "DBPU_Share_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            try
            {
                var packForm = CustomTransformationCatalog.CloneForm(form);
                packForm.HairSpritePath = CopyAsset(packForm.HairSpritePath, tempRoot, "hair");
                packForm.HeadSpritePath = CopyAsset(packForm.HeadSpritePath, tempRoot, "head");
                if (packForm.LimbSpritePaths != null)
                {
                    var nextLimbs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var kvp in packForm.LimbSpritePaths)
                    {
                        if (string.IsNullOrEmpty(kvp.Key) || string.IsNullOrEmpty(kvp.Value)) continue;
                        string copied = CopyAsset(kvp.Value, tempRoot, "limb_" + kvp.Key);
                        if (!string.IsNullOrEmpty(copied))
                            nextLimbs[kvp.Key] = copied;
                    }
                    packForm.LimbSpritePaths = nextLimbs;
                }
                packForm.IconPath = CopyAsset(packForm.IconPath, tempRoot, "icon");
                packForm.IllustrationPath = CopyAsset(packForm.IllustrationPath, tempRoot, "illustration");
                if (packForm.Accessories != null)
                {
                    for (int i = 0; i < packForm.Accessories.Count; i++)
                    {
                        var acc = packForm.Accessories[i];
                        if (acc == null) continue;
                        acc.SpritePath = CopyAsset(acc.SpritePath, tempRoot, "acc_" + i);
                    }
                }

                var pack = new TransformationSharePack
                {
                    CharacterId = characterId,
                    DisplayName = displayName,
                    Form = packForm
                };
                File.WriteAllText(Path.Combine(tempRoot, "form.json"), JsonConvert.SerializeObject(pack, Formatting.Indented));

                if (File.Exists(dest)) File.Delete(dest);
                ZipFile.CreateFromDirectory(tempRoot, dest, System.IO.Compression.CompressionLevel.Optimal, false);
            }
            finally
            {
                try { if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true); } catch { }
            }
        }

        public static CustomTransformationFormDto ImportForm(string characterId, string displayName, out string importedId)
        {
            importedId = null;
            string src = TmFilePicker.OpenFile("Import Transformation", FileFilter,
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            if (string.IsNullOrEmpty(src)) return null;
            return ImportFormFromPath(src, characterId, displayName, out importedId);
        }

        public static CustomTransformationFormDto ImportFormFromPath(string src, string characterId, string displayName, out string importedId)
        {
            importedId = null;
            src = NormalizeImportPath(src);
            if (string.IsNullOrEmpty(src))
                throw new InvalidOperationException("No path provided.");
            if (!File.Exists(src))
                throw new InvalidOperationException("File not found:\n" + src);

            string extractRoot = Path.Combine(
                CustomTransformationCatalog.EnsureImportFolder(),
                "Shared",
                Path.GetFileNameWithoutExtension(src) + "_" + DateTime.Now.Ticks.ToString("x"));
            Directory.CreateDirectory(extractRoot);

            ZipFile.ExtractToDirectory(src, extractRoot);
            string jsonPath = Path.Combine(extractRoot, "form.json");
            if (!File.Exists(jsonPath))
            {
                var found = Directory.GetFiles(extractRoot, "form.json", SearchOption.AllDirectories).FirstOrDefault();
                if (found == null) throw new InvalidOperationException("Pack is missing form.json.");
                jsonPath = found;
                extractRoot = Path.GetDirectoryName(found);
            }

            var pack = JsonConvert.DeserializeObject<TransformationSharePack>(File.ReadAllText(jsonPath));
            if (pack?.Form == null || string.IsNullOrEmpty(pack.Form.ID))
                throw new InvalidOperationException("Pack has no valid form.");

            var form = CustomTransformationCatalog.CloneForm(pack.Form);
            form.HairSpritePath = ResolvePackAsset(extractRoot, form.HairSpritePath);
            form.HeadSpritePath = ResolvePackAsset(extractRoot, form.HeadSpritePath);
            if (form.LimbSpritePaths != null)
            {
                var nextLimbs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var kvp in form.LimbSpritePaths)
                {
                    if (string.IsNullOrEmpty(kvp.Key) || string.IsNullOrEmpty(kvp.Value)) continue;
                    string resolved = ResolvePackAsset(extractRoot, kvp.Value);
                    if (!string.IsNullOrEmpty(resolved))
                        nextLimbs[kvp.Key] = resolved;
                }
                form.LimbSpritePaths = nextLimbs;
            }
            form.IconPath = ResolvePackAsset(extractRoot, form.IconPath);
            form.IllustrationPath = ResolvePackAsset(extractRoot, form.IllustrationPath);
            if (form.Accessories != null)
            {
                for (int i = 0; i < form.Accessories.Count; i++)
                {
                    if (form.Accessories[i] == null) continue;
                    form.Accessories[i].SpritePath = ResolvePackAsset(extractRoot, form.Accessories[i].SpritePath);
                }
            }

            form.IsCustom = true;
            if (string.IsNullOrEmpty(form.ID) || !form.ID.StartsWith("Custom_", StringComparison.OrdinalIgnoreCase))
                form.ID = "Custom_" + DateTime.Now.Ticks.ToString("x");

            string targetChar = string.IsNullOrEmpty(characterId) ? pack.CharacterId : characterId;
            string targetName = string.IsNullOrEmpty(displayName) ? pack.DisplayName : displayName;
            if (string.IsNullOrEmpty(targetChar))
                targetChar = "Imported";

            CustomTransformationCatalog.UpsertForm(targetChar, targetName, form);
            TransformationMakerHairLibrary.Invalidate();
            importedId = form.ID;
            return form;
        }

        static string NormalizeImportPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            path = path.Trim().Trim('"').Trim();
            if (string.IsNullOrEmpty(path)) return null;
            try { return Path.GetFullPath(path); }
            catch { return path; }
        }

        static string CopyAsset(string path, string tempRoot, string stem)
        {
            if (string.IsNullOrEmpty(path)) return null;
            string abs = path;
            if (!File.Exists(abs))
                abs = CustomTransformationCatalog.ResolveModPath(path);
            if (string.IsNullOrEmpty(abs) || !File.Exists(abs)) return null;

            string ext = Path.GetExtension(abs);
            if (string.IsNullOrEmpty(ext)) ext = ".png";
            string fileName = stem + ext;
            string dest = Path.Combine(tempRoot, fileName);
            File.Copy(abs, dest, true);
            return fileName;
        }

        static string ResolvePackAsset(string extractRoot, string relativeOrAbsolute)
        {
            if (string.IsNullOrEmpty(relativeOrAbsolute)) return null;
            if (File.Exists(relativeOrAbsolute)) return relativeOrAbsolute;
            string combined = Path.Combine(extractRoot, relativeOrAbsolute.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(combined)) return combined;
            string justName = Path.Combine(extractRoot, Path.GetFileName(relativeOrAbsolute));
            if (File.Exists(justName)) return justName;
            return null;
        }

        static string Sanitize(string name)
        {
            if (string.IsNullOrEmpty(name)) return "form";
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Trim();
        }
    }

    public class CustomTransformationSaveData
    {
        public Dictionary<string, CustomTransformationCharacterDto> Characters = new Dictionary<string, CustomTransformationCharacterDto>();
    }

    public static class CustomTransformationCatalog
    {
        public const string SavePath = "DBPU_CustomTransformations.json";
        public const string ImportFolder = "Assets/Sprites/CustomTransforms";
        static CustomTransformationSaveData saveData = new CustomTransformationSaveData();
        static bool _loaded;
        static readonly Dictionary<string, (long WriteTicks, Sprite Sprite)> DiskSpriteCache =
            new Dictionary<string, (long, Sprite)>(StringComparer.OrdinalIgnoreCase);

        public static void EnsureLoaded()
        {
            if (_loaded) return;
            Load();
        }

        public static void Load()
        {
            saveData = new CustomTransformationSaveData();
            try
            {
                saveData = ModAPI.DeserialiseJSON<CustomTransformationSaveData>(SavePath) ?? new CustomTransformationSaveData();
            }
            catch
            {
                saveData = new CustomTransformationSaveData();
            }
            if (saveData.Characters == null)
                saveData.Characters = new Dictionary<string, CustomTransformationCharacterDto>();
            _loaded = true;
        }

        public static void Save()
        {
            if (saveData == null) saveData = new CustomTransformationSaveData();
            if (saveData.Characters == null) saveData.Characters = new Dictionary<string, CustomTransformationCharacterDto>();
            ModAPI.SerialiseJSON(saveData, SavePath);
        }

        public static List<CustomTransformationFormDto> GetForms(string characterId)
        {
            EnsureLoaded();
            var list = new List<CustomTransformationFormDto>();
            if (string.IsNullOrEmpty(characterId) || saveData?.Characters == null) return list;
            if (!saveData.Characters.TryGetValue(characterId, out var ch) || ch?.Forms == null) return list;
            EnsureCharacterFormOrder(ch);
            var byId = new Dictionary<string, CustomTransformationFormDto>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in ch.Forms)
                if (kvp.Value != null && !string.IsNullOrEmpty(kvp.Key))
                    byId[kvp.Key] = kvp.Value;
            for (int i = 0; i < ch.FormOrder.Count; i++)
            {
                string id = ch.FormOrder[i];
                if (byId.TryGetValue(id, out var form))
                {
                    list.Add(form);
                    byId.Remove(id);
                }
            }
            foreach (var leftover in byId.Values)
                list.Add(leftover);
            return list;
        }

        static CustomTransformationCharacterDto GetOrCreateCharacter(string characterId, string displayName = null)
        {
            if (saveData.Characters == null) saveData.Characters = new Dictionary<string, CustomTransformationCharacterDto>();
            if (!saveData.Characters.TryGetValue(characterId, out var ch) || ch == null)
            {
                ch = new CustomTransformationCharacterDto();
                saveData.Characters[characterId] = ch;
            }
            if (!string.IsNullOrEmpty(displayName))
                ch.DisplayName = displayName;
            if (ch.Forms == null) ch.Forms = new Dictionary<string, CustomTransformationFormDto>();
            if (ch.FormOrder == null) ch.FormOrder = new List<string>();
            return ch;
        }

        static void EnsureCharacterFormOrder(CustomTransformationCharacterDto ch)
        {
            if (ch == null) return;
            if (ch.Forms == null) ch.Forms = new Dictionary<string, CustomTransformationFormDto>();
            if (ch.FormOrder == null) ch.FormOrder = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = ch.FormOrder.Count - 1; i >= 0; i--)
            {
                string id = ch.FormOrder[i];
                if (string.IsNullOrEmpty(id) || !seen.Add(id))
                    ch.FormOrder.RemoveAt(i);
            }
            foreach (var kvp in ch.Forms)
            {
                if (kvp.Value == null || string.IsNullOrEmpty(kvp.Key)) continue;
                if (!seen.Add(kvp.Key)) continue;
                ch.FormOrder.Add(kvp.Key);
            }
        }

        public static List<string> GetFormOrder(string characterId, IEnumerable<string> knownIds = null)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(characterId)) return result;
            var ch = GetOrCreateCharacter(characterId);
            EnsureCharacterFormOrder(ch);
            var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (knownIds != null)
            {
                foreach (var id in knownIds)
                    if (!string.IsNullOrEmpty(id)) known.Add(id);
            }
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < ch.FormOrder.Count; i++)
            {
                string id = ch.FormOrder[i];
                if (string.IsNullOrEmpty(id) || !seen.Add(id)) continue;
                if (known.Count > 0 && !known.Contains(id)) continue;
                result.Add(id);
            }
            if (known.Count > 0)
            {
                foreach (var id in known)
                {
                    if (!seen.Add(id)) continue;
                    result.Add(id);
                    if (!ch.FormOrder.Exists(x => string.Equals(x, id, StringComparison.OrdinalIgnoreCase)))
                        ch.FormOrder.Add(id);
                }
            }
            return result;
        }

        public static bool MoveFormOrder(string characterId, string formId, int direction, IEnumerable<string> knownIds = null)
        {
            if (string.IsNullOrEmpty(characterId) || string.IsNullOrEmpty(formId) || direction == 0) return false;
            var order = GetFormOrder(characterId, knownIds);
            int index = order.FindIndex(id => string.Equals(id, formId, StringComparison.OrdinalIgnoreCase));
            if (index < 0) return false;
            int target = index + (direction < 0 ? -1 : 1);
            if (target < 0 || target >= order.Count) return false;
            string temp = order[target];
            order[target] = order[index];
            order[index] = temp;
            SetFormOrder(characterId, order);
            return true;
        }

        public static void SetFormOrder(string characterId, IList<string> order)
        {
            if (string.IsNullOrEmpty(characterId) || order == null) return;
            var ch = GetOrCreateCharacter(characterId);
            var next = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < order.Count; i++)
            {
                string id = order[i];
                if (string.IsNullOrEmpty(id) || !seen.Add(id)) continue;
                next.Add(id);
            }
            ch.FormOrder = next;
            Save();
        }

        public static void ApplySavedFormOrderToPerson(GameObject instance)
        {
            if (instance == null) return;
            var identity = instance.GetComponent<DbpuCharacterIdentity>();
            var ct = instance.GetComponent<CharacterTransformations>();
            if (identity == null || ct?.transformations == null || string.IsNullOrEmpty(identity.CharacterId)) return;
            var known = ct.transformations.TransformationData != null
                ? ct.transformations.TransformationData.Keys
                : null;
            var order = GetFormOrder(identity.CharacterId, known);
            ct.transformations.ApplyFormOrder(order);
        }

        public static CustomTransformationFormDto GetForm(string characterId, string formId)
        {
            if (string.IsNullOrEmpty(characterId) || string.IsNullOrEmpty(formId)) return null;
            if (saveData?.Characters == null) return null;
            if (!saveData.Characters.TryGetValue(characterId, out var ch) || ch?.Forms == null) return null;
            ch.Forms.TryGetValue(formId, out var form);
            return form;
        }

        public static CustomTransformationFormDto CloneForm(CustomTransformationFormDto src)
        {
            if (src == null) return null;
            var dto = new CustomTransformationFormDto
            {
                ID = src.ID,
                Name = src.Name,
                Description = src.Description,
                PowerMultiplier = src.PowerMultiplier,
                Size = src.Size,
                KiDrainRate = src.KiDrainRate,
                AuraID = src.AuraID,
                UseCustomAura = src.UseCustomAura,
                AuraColorR = src.AuraColorR,
                AuraColorG = src.AuraColorG,
                AuraColorB = src.AuraColorB,
                AuraColor2R = src.AuraColor2R,
                AuraColor2G = src.AuraColor2G,
                AuraColor2B = src.AuraColor2B,
                UseAuraSecondaryColor = src.UseAuraSecondaryColor,
                AuraScale = src.AuraScale,
                HairSpritePath = src.HairSpritePath,
                HeadSpritePath = src.HeadSpritePath,
                LimbSpritePaths = src.LimbSpritePaths != null
                    ? new Dictionary<string, string>(src.LimbSpritePaths, StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                IconPath = src.IconPath,
                IllustrationPath = src.IllustrationPath,
                RequiredSkin_Suffix = src.RequiredSkin_Suffix,
                HairColorR = src.HairColorR,
                HairColorG = src.HairColorG,
                HairColorB = src.HairColorB,
                HairLocalX = src.HairLocalX,
                HairLocalY = src.HairLocalY,
                HairLocalZ = src.HairLocalZ,
                UseHairLocalPosition = src.UseHairLocalPosition,
                UseKiBarColor = src.UseKiBarColor,
                KiBarColorR = src.KiBarColorR,
                KiBarColorG = src.KiBarColorG,
                KiBarColorB = src.KiBarColorB,
                IsCustom = src.IsCustom,
                Accessories = new List<CustomAccessoryDto>()
            };
            if (src.Accessories != null)
            {
                for (int i = 0; i < src.Accessories.Count; i++)
                {
                    var a = src.Accessories[i];
                    if (a == null) continue;
                    dto.Accessories.Add(new CustomAccessoryDto
                    {
                        Id = a.Id,
                        Name = a.Name,
                        LimbName = a.LimbName,
                        SpritePath = a.SpritePath,
                        LocalX = a.LocalX,
                        LocalY = a.LocalY,
                        LocalZ = a.LocalZ,
                        Scale = a.Scale,
                        RotationZ = a.RotationZ,
                        SortingOrder = a.SortingOrder
                    });
                }
            }
            return dto;
        }

        public static void UpsertForm(string characterId, string displayName, CustomTransformationFormDto form)
        {
            if (string.IsNullOrEmpty(characterId) || form == null || string.IsNullOrEmpty(form.ID)) return;
            var ch = GetOrCreateCharacter(characterId, displayName ?? characterId);
            if (!string.IsNullOrEmpty(displayName))
                ch.DisplayName = displayName;
            else if (string.IsNullOrEmpty(ch.DisplayName))
                ch.DisplayName = characterId;
            form.IsCustom = true;
            bool isNew = !ch.Forms.ContainsKey(form.ID);
            ch.Forms[form.ID] = CloneForm(form);
            if (isNew && !ch.FormOrder.Exists(id => string.Equals(id, form.ID, StringComparison.OrdinalIgnoreCase)))
                ch.FormOrder.Add(form.ID);
            Save();
        }

        public static void DeleteForm(string characterId, string formId)
        {
            if (string.IsNullOrEmpty(characterId) || string.IsNullOrEmpty(formId)) return;
            if (saveData?.Characters == null) return;
            if (!saveData.Characters.TryGetValue(characterId, out var ch) || ch?.Forms == null) return;
            ch.Forms.Remove(formId);
            if (ch.FormOrder != null)
                ch.FormOrder.RemoveAll(id => string.Equals(id, formId, StringComparison.OrdinalIgnoreCase));
            Save();
        }

        public static bool HasSavedForm(string characterId, string formId)
        {
            return GetForm(characterId, formId) != null;
        }

        public static string ResolveModPath(string relativeOrAbsolute)
        {
            if (string.IsNullOrEmpty(relativeOrAbsolute)) return null;

            string modRoot;
            try { modRoot = ModAPI.Metadata.MetaLocation; }
            catch { modRoot = Path.Combine(Application.dataPath, "..", "Mods", "Dragon Ball Singularity"); }

            string normalized = relativeOrAbsolute.Replace('\\', '/');
            int assetsIdx = normalized.IndexOf("/Assets/", StringComparison.OrdinalIgnoreCase);
            if (assetsIdx >= 0)
                normalized = normalized.Substring(assetsIdx + 1);

            if (Path.IsPathRooted(relativeOrAbsolute) && File.Exists(relativeOrAbsolute)
                && relativeOrAbsolute.IndexOf(modRoot, StringComparison.OrdinalIgnoreCase) >= 0)
                return relativeOrAbsolute;

            string rel = normalized.Replace('/', Path.DirectorySeparatorChar);
            string combined = Path.Combine(modRoot, rel);
            try { combined = Path.GetFullPath(combined); } catch { }
            if (File.Exists(combined)) return combined;

            string caseHit = FindCaseInsensitiveFile(modRoot, normalized);
            if (!string.IsNullOrEmpty(caseHit))
            {
                try { return Path.GetFullPath(caseHit); } catch { return caseHit; }
            }

            string[] fallbacks =
            {
                "Dragon Ball Singularity",
                "Dragon Ball Singularity",
                "DragonBall Playground Ultimate"
            };
            for (int i = 0; i < fallbacks.Length; i++)
            {
                string alt = Path.Combine(Application.dataPath, "..", "Mods", fallbacks[i], rel);
                try { alt = Path.GetFullPath(alt); } catch { }
                if (File.Exists(alt)) return alt;
            }
            return combined;
        }

        public static string FindCaseInsensitiveFile(string root, string relativePath)
        {
            if (string.IsNullOrEmpty(root) || string.IsNullOrEmpty(relativePath) || !Directory.Exists(root))
                return null;
            string[] parts = relativePath.Replace('\\', '/').Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            string current = root;
            for (int i = 0; i < parts.Length; i++)
            {
                bool isLast = i == parts.Length - 1;
                try
                {
                    if (isLast)
                    {
                        foreach (string file in Directory.GetFiles(current))
                        {
                            if (string.Equals(Path.GetFileName(file), parts[i], StringComparison.OrdinalIgnoreCase))
                                return file;
                        }
                        return null;
                    }
                    string next = null;
                    foreach (string dir in Directory.GetDirectories(current))
                    {
                        if (string.Equals(Path.GetFileName(dir), parts[i], StringComparison.OrdinalIgnoreCase))
                        {
                            next = dir;
                            break;
                        }
                    }
                    if (next == null) return null;
                    current = next;
                }
                catch { return null; }
            }
            return null;
        }

        public static CharacterTransformations.Transformations.TransformationStats CloneStats(
            CharacterTransformations.Transformations.TransformationStats src)
        {
            if (src == null) return null;
            return new CharacterTransformations.Transformations.TransformationStats
            {
                ID = src.ID,
                Name = src.Name,
                Description = src.Description,
                PowerMultiplier = src.PowerMultiplier,
                Size = src.Size,
                KiDrainRate = src.KiDrainRate,
                AuraID = src.AuraID,
                HairSprite = src.HairSprite,
                HeadSprite = src.HeadSprite,
                Icon = src.Icon,
                Illustration = src.Illustration,
                UseHairLocalPosition = src.UseHairLocalPosition,
                HairLocalPosition = src.HairLocalPosition,
                RequiredSkin_Suffix = src.RequiredSkin_Suffix,
                RequiredSkin = src.RequiredSkin,
                requiredKiColor = src.requiredKiColor,
                BurstSFX = src.BurstSFX,
                CutsceneID = src.CutsceneID,
                SkinDamageAmount = src.SkinDamageAmount,
                PhyAccessory = src.PhyAccessory,
                SituationalAccessories = src.SituationalAccessories,
                DamageSkins = src.DamageSkins,
                LimbLayout = src.LimbLayout,
                SpecialBehaviours = src.SpecialBehaviours != null
                    ? new List<Type>(src.SpecialBehaviours)
                    : new List<Type>()
            };
        }

        public static void BackupStockIfNeeded(GameObject instance, string formId,
            CharacterTransformations.Transformations.TransformationStats existing)
        {
            if (instance == null || string.IsNullOrEmpty(formId) || existing == null) return;
            if (IsPureCustomFormId(formId)) return;
            var backup = instance.GetComponent<CustomFormStockBackup>();
            if (backup == null) backup = instance.AddComponent<CustomFormStockBackup>();
            backup.StoreIfAbsent(formId, existing);
        }

        public static bool IsPureCustomFormId(string formId)
        {
            if (string.IsNullOrEmpty(formId)) return false;
            return formId.StartsWith("Custom_", StringComparison.OrdinalIgnoreCase)
                || formId.StartsWith("Workshop_", StringComparison.OrdinalIgnoreCase);
        }

        public static string WorkshopFormId(ulong fileId)
        {
            return fileId == 0 ? null : "Workshop_" + fileId;
        }

        public static CharacterTransformations.Transformations.TransformationStats ToStats(
            CustomTransformationFormDto dto,
            GameObject personRoot = null)
        {
            if (dto == null) return null;
            var stats = new CharacterTransformations.Transformations.TransformationStats
            {
                ID = dto.ID,
                Name = string.IsNullOrEmpty(dto.Name) ? dto.ID : dto.Name,
                Description = dto.Description ?? "",
                PowerMultiplier = dto.PowerMultiplier <= 0f ? 1f : dto.PowerMultiplier,
                Size = dto.Size <= 0f ? 1.75f : dto.Size,
                KiDrainRate = Mathf.Max(0f, dto.KiDrainRate),
                AuraID = string.IsNullOrEmpty(dto.AuraID) ? "Base" : dto.AuraID,
                RequiredSkin_Suffix = dto.RequiredSkin_Suffix ?? "",
                UseHairLocalPosition = dto.UseHairLocalPosition,
                HairLocalPosition = new Vector3(dto.HairLocalX, dto.HairLocalY, dto.HairLocalZ),
                requiredKiColor = dto.UseKiBarColor
                    ? new Color(dto.KiBarColorR, dto.KiBarColorG, dto.KiBarColorB, 1f)
                    : Color.clear
            };

            stats.HairSprite = ResolveSpriteReference(dto.HairSpritePath, personRoot, true);
            stats.HeadSprite = ResolveSpriteReference(dto.HeadSpritePath, personRoot, false);
            if (string.IsNullOrEmpty(dto.HeadSpritePath) && dto.LimbSpritePaths != null
                && dto.LimbSpritePaths.TryGetValue("Head", out var headPath)
                && !string.IsNullOrEmpty(headPath))
            {
                stats.HeadSprite = LoadSpriteFlexible(headPath);
            }
            if (!string.IsNullOrEmpty(dto.IconPath))
                stats.Icon = LoadSpriteFlexible(dto.IconPath);
            if (!string.IsNullOrEmpty(dto.IllustrationPath))
                stats.Illustration = LoadSpriteFlexible(dto.IllustrationPath);
            if (stats.Icon == null) stats.Icon = stats.HairSprite ?? stats.HeadSprite;
            if (stats.Illustration == null) stats.Illustration = stats.Icon;
            return stats;
        }

        public static Sprite ResolveSpriteReference(string path, GameObject personRoot, bool hair)
        {
            if (string.IsNullOrEmpty(path)) return null;
            Sprite spr = LoadSpriteFlexible(path);
            if (spr != null) return spr;

            if (path.StartsWith("form:", StringComparison.OrdinalIgnoreCase) && personRoot != null)
            {
                string formId = path.Substring("form:".Length);
                var ct = personRoot.GetComponent<CharacterTransformations>();
                if (ct?.transformations?.TransformationData != null
                    && ct.transformations.TransformationData.TryGetValue(formId, out var stats)
                    && stats != null)
                {
                    return hair ? stats.HairSprite : stats.HeadSprite;
                }
            }
            return null;
        }

        public static bool IsVirtualSpritePath(string path)
        {
            return !string.IsNullOrEmpty(path)
                && path.StartsWith("form:", StringComparison.OrdinalIgnoreCase);
        }

        public static string PersistSpriteAsset(Sprite sprite, string subfolder, string fileStem)
        {
            if (sprite == null || sprite.texture == null) return null;
            try
            {
                string dir = Path.Combine(EnsureImportFolder(), subfolder ?? "hair");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string safe = fileStem ?? "sprite";
                foreach (char c in Path.GetInvalidFileNameChars())
                    safe = safe.Replace(c, '_');
                string path = Path.Combine(dir, safe + ".png");

                var tex = sprite.texture;
                Texture2D readable;
                if (tex.isReadable)
                {
                    var rect = sprite.textureRect;
                    readable = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGBA32, false);
                    readable.SetPixels(tex.GetPixels((int)rect.x, (int)rect.y, (int)rect.width, (int)rect.height));
                    readable.Apply(false, false);
                }
                else
                {
                    var rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32);
                    var prev = RenderTexture.active;
                    Graphics.Blit(tex, rt);
                    RenderTexture.active = rt;
                    readable = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
                    readable.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
                    readable.Apply(false, false);
                    RenderTexture.active = prev;
                    RenderTexture.ReleaseTemporary(rt);
                }
                File.WriteAllBytes(path, readable.EncodeToPNG());
                UnityEngine.Object.Destroy(readable);
                return path;
            }
            catch
            {
                return null;
            }
        }

        public static string EnsurePersistedSpritePath(string path, Sprite spriteHint, string subfolder, string fileStem)
        {
            if (string.IsNullOrEmpty(path) && spriteHint == null) return path;
            if (!IsVirtualSpritePath(path))
            {
                if (!string.IsNullOrEmpty(path))
                {
                    if (File.Exists(path)) return path;
                    string resolved = ResolveModPath(path);
                    if (!string.IsNullOrEmpty(resolved) && File.Exists(resolved)) return resolved;
                }
                if (spriteHint == null) return path;
            }

            Sprite spr = spriteHint;
            if (spr == null) spr = LoadSpriteFlexible(path);
            if (spr == null) return path;
            string saved = PersistSpriteAsset(spr, subfolder, fileStem);
            return string.IsNullOrEmpty(saved) ? path : saved;
        }

        public static bool BakeVirtualSpritePaths(CustomTransformationFormDto dto, GameObject personRoot)
        {
            if (dto == null) return false;
            bool changed = false;
            string stemBase = (dto.ID ?? "form") + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);

            if (IsVirtualSpritePath(dto.HairSpritePath))
            {
                Sprite hair = ResolveSpriteReference(dto.HairSpritePath, personRoot, true);
                string next = EnsurePersistedSpritePath(dto.HairSpritePath, hair, "hair", stemBase + "_hair");
                if (!string.IsNullOrEmpty(next) && !string.Equals(next, dto.HairSpritePath, StringComparison.OrdinalIgnoreCase))
                {
                    dto.HairSpritePath = next;
                    changed = true;
                }
            }

            if (IsVirtualSpritePath(dto.HeadSpritePath))
            {
                Sprite head = ResolveSpriteReference(dto.HeadSpritePath, personRoot, false);
                string next = EnsurePersistedSpritePath(dto.HeadSpritePath, head, "head", stemBase + "_head");
                if (!string.IsNullOrEmpty(next) && !string.Equals(next, dto.HeadSpritePath, StringComparison.OrdinalIgnoreCase))
                {
                    dto.HeadSpritePath = next;
                    changed = true;
                }
            }

            return changed;
        }

        public static void EnsureLimbSpritePaths(CustomTransformationFormDto dto)
        {
            if (dto == null) return;
            if (dto.LimbSpritePaths == null)
                dto.LimbSpritePaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            else
                dto.LimbSpritePaths = new Dictionary<string, string>(dto.LimbSpritePaths, StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(dto.HeadSpritePath)
                && !dto.LimbSpritePaths.ContainsKey("Head"))
                dto.LimbSpritePaths["Head"] = dto.HeadSpritePath;
            if (!string.IsNullOrEmpty(dto.HairSpritePath)
                && !dto.LimbSpritePaths.ContainsKey("Hair"))
                dto.LimbSpritePaths["Hair"] = dto.HairSpritePath;
        }

        public static void SetLimbSpritePath(CustomTransformationFormDto dto, string limbKey, string path)
        {
            if (dto == null || string.IsNullOrEmpty(limbKey)) return;
            EnsureLimbSpritePaths(dto);
            string key = DbpuSpriteEditorBridge.MapLimbKey(limbKey);
            if (string.IsNullOrEmpty(path))
                dto.LimbSpritePaths.Remove(key);
            else
                dto.LimbSpritePaths[key] = path;
            if (string.Equals(key, "Head", StringComparison.OrdinalIgnoreCase))
                dto.HeadSpritePath = path;
            else if (string.Equals(key, "Hair", StringComparison.OrdinalIgnoreCase))
                dto.HairSpritePath = path;
        }

        public static string GetLimbSpritePath(CustomTransformationFormDto dto, string limbKey)
        {
            if (dto == null || string.IsNullOrEmpty(limbKey)) return null;
            EnsureLimbSpritePaths(dto);
            string key = DbpuSpriteEditorBridge.MapLimbKey(limbKey);
            if (dto.LimbSpritePaths.TryGetValue(key, out var path) && !string.IsNullOrEmpty(path))
                return path;
            if (string.Equals(key, "Head", StringComparison.OrdinalIgnoreCase))
                return dto.HeadSpritePath;
            if (string.Equals(key, "Hair", StringComparison.OrdinalIgnoreCase))
                return dto.HairSpritePath;
            return null;
        }

        public static void ApplyLimbSpritePaths(PersonBehaviour person, CustomTransformationFormDto dto)
        {
            if (person?.Limbs == null || dto == null) return;
            EnsureLimbSpritePaths(dto);
            if (dto.LimbSpritePaths.Count == 0 && string.IsNullOrEmpty(dto.HeadSpritePath)) return;

            for (int i = 0; i < person.Limbs.Length; i++)
            {
                var limb = person.Limbs[i];
                if (limb == null) continue;
                string key = DbpuSpriteEditorBridge.MapLimbKey(limb.name);
                if (string.Equals(key, "Hair", StringComparison.OrdinalIgnoreCase)) continue;
                string path = GetLimbSpritePath(dto, key);
                if (string.IsNullOrEmpty(path)) continue;
                Sprite spr = LoadSpriteFlexible(path);
                if (spr == null) continue;
                var sr = limb.GetComponent<SpriteRenderer>();
                if (sr == null && limb.SkinMaterialHandler != null)
                    sr = limb.SkinMaterialHandler.renderer;
                if (sr != null) sr.sprite = spr;
            }
        }

        public static CustomTransformationFormDto FromStats(CharacterTransformations.Transformations.TransformationStats stats, CustomTransformationFormDto existing = null)
        {
            var dto = existing ?? new CustomTransformationFormDto();
            if (stats == null) return dto;
            dto.ID = stats.ID;
            dto.Name = stats.Name;
            dto.Description = stats.Description;
            dto.PowerMultiplier = stats.PowerMultiplier;
            dto.Size = stats.Size;
            dto.KiDrainRate = stats.KiDrainRate;
            dto.AuraID = stats.AuraID;
            dto.RequiredSkin_Suffix = stats.RequiredSkin_Suffix ?? "";
            dto.UseHairLocalPosition = stats.UseHairLocalPosition;
            dto.HairLocalX = stats.HairLocalPosition.x;
            dto.HairLocalY = stats.HairLocalPosition.y;
            dto.HairLocalZ = stats.HairLocalPosition.z;
            if (stats.requiredKiColor != Color.clear)
            {
                dto.UseKiBarColor = true;
                dto.KiBarColorR = stats.requiredKiColor.r;
                dto.KiBarColorG = stats.requiredKiColor.g;
                dto.KiBarColorB = stats.requiredKiColor.b;
            }
            return dto;
        }

        public static Sprite LoadSpriteFlexible(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            try
            {
                if (File.Exists(path))
                    return LoadSpriteFromDisk(path);

                string resolved = ResolveModPath(path);
                if (!string.IsNullOrEmpty(resolved) && File.Exists(resolved))
                    return LoadSpriteFromDisk(resolved);

                Sprite fromApi = null;
                try { fromApi = ModAPI.LoadSprite(path); } catch { }
                if (fromApi != null) return fromApi;

                string rel = path.Replace('\\', '/');
                int assetsIdx = rel.IndexOf("Assets/", StringComparison.OrdinalIgnoreCase);
                if (assetsIdx > 0) rel = rel.Substring(assetsIdx);
                try { fromApi = ModAPI.LoadSprite(rel); } catch { }
                return fromApi;
            }
            catch
            {
                return null;
            }
        }

        public static string NormalizeFsPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            try { return Path.GetFullPath(path); }
            catch { return path.Replace('/', Path.DirectorySeparatorChar); }
        }

        public static bool PathsEqual(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
            try
            {
                return string.Equals(NormalizeFsPath(a), NormalizeFsPath(b), StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
            }
        }

        public static byte[] ReadAllBytesShared(string absolutePath)
        {
            using (var fs = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                long len = fs.Length;
                if (len < 0 || len > int.MaxValue) return null;
                byte[] bytes = new byte[(int)len];
                int offset = 0;
                while (offset < bytes.Length)
                {
                    int n = fs.Read(bytes, offset, bytes.Length - offset);
                    if (n <= 0) break;
                    offset += n;
                }
                if (offset != bytes.Length)
                    Array.Resize(ref bytes, offset);
                return bytes;
            }
        }

        public static void WriteAllBytesShared(string absolutePath, byte[] bytes)
        {
            if (bytes == null) return;
            string dir = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            string tmp = absolutePath + ".tmp";
            using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.Read))
                fs.Write(bytes, 0, bytes.Length);
            try
            {
                if (File.Exists(absolutePath))
                    File.Delete(absolutePath);
            }
            catch
            {
                System.Threading.Thread.Sleep(30);
                if (File.Exists(absolutePath))
                    File.Delete(absolutePath);
            }
            File.Move(tmp, absolutePath);
            InvalidateDiskSpriteCache(absolutePath);
        }

        public static void CopyFileShared(string src, string dest, bool overwrite = true)
        {
            if (string.IsNullOrEmpty(src) || string.IsNullOrEmpty(dest)) return;
            if (!File.Exists(src)) return;
            if (PathsEqual(src, dest)) return;
            string destDir = Path.GetDirectoryName(dest);
            if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                Directory.CreateDirectory(destDir);
            byte[] bytes = ReadAllBytesShared(src);
            if (bytes == null) return;
            try
            {
                WriteAllBytesShared(dest, bytes);
            }
            catch (IOException)
            {
                System.Threading.Thread.Sleep(40);
                WriteAllBytesShared(dest, bytes);
            }
            catch
            {
                if (overwrite || !File.Exists(dest))
                    File.Copy(src, dest, true);
                InvalidateDiskSpriteCache(dest);
            }
        }

        public static Sprite LoadSpriteFromDisk(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath) || !File.Exists(absolutePath)) return null;
            string cacheKey = NormalizeFsPath(absolutePath);
            long writeTicks = 0;
            try { writeTicks = File.GetLastWriteTimeUtc(cacheKey).Ticks; } catch { }
            if (DiskSpriteCache.TryGetValue(cacheKey, out var cached)
                && cached.Sprite != null
                && cached.WriteTicks == writeTicks)
                return cached.Sprite;

            byte[] bytes = ReadAllBytesShared(cacheKey);
            if (bytes == null || bytes.Length == 0) return null;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(bytes)) return null;
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            var spr = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 35f);
            DiskSpriteCache[cacheKey] = (writeTicks, spr);
            return spr;
        }

        public static void InvalidateDiskSpriteCache(string absolutePath = null)
        {
            if (string.IsNullOrEmpty(absolutePath))
            {
                DiskSpriteCache.Clear();
                return;
            }
            string full;
            try { full = Path.GetFullPath(absolutePath); }
            catch { full = absolutePath; }
            bool isDir = Directory.Exists(full);
            string dirPrefix = null;
            if (isDir)
            {
                dirPrefix = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;
            }
            var remove = new List<string>();
            foreach (var key in DiskSpriteCache.Keys)
            {
                try
                {
                    string kFull = Path.GetFullPath(key);
                    if (string.Equals(kFull, full, StringComparison.OrdinalIgnoreCase))
                        remove.Add(key);
                    else if (dirPrefix != null && kFull.StartsWith(dirPrefix, StringComparison.OrdinalIgnoreCase))
                        remove.Add(key);
                }
                catch
                {
                    if (string.Equals(key, absolutePath, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(key, full, StringComparison.OrdinalIgnoreCase))
                        remove.Add(key);
                }
            }
            for (int i = 0; i < remove.Count; i++)
                DiskSpriteCache.Remove(remove[i]);
        }

        public static void ApplyToPerson(GameObject instance)
        {
            if (instance == null) return;
            var identity = instance.GetComponent<DbpuCharacterIdentity>();
            if (identity == null || string.IsNullOrEmpty(identity.CharacterId)) return;

            var forms = GetForms(identity.CharacterId);
            var person = instance.GetComponent<PersonBehaviour>();
            var ct = instance.GetComponent<CharacterTransformations>();

            if (forms.Count > 0 && person != null)
            {
                TransformationMakerUI.EnsurePersonTransformSupport(person);
                ct = instance.GetComponent<CharacterTransformations>();
            }

            if (ct == null || ct.transformations == null) return;
            if (ct.transformations.TransformationData == null) return;

            for (int i = 0; i < forms.Count; i++)
            {
                var form = forms[i];
                if (form == null) continue;
                if (BakeVirtualSpritePaths(form, instance))
                    UpsertForm(identity.CharacterId, identity.DisplayName, form);

                var stats = ToStats(form, instance);
                if (stats == null || string.IsNullOrEmpty(stats.ID)) continue;
                if (ct.transformations.TransformationData.ContainsKey(stats.ID))
                {
                    var existing = ct.transformations.TransformationData[stats.ID];
                    if (!IsPureCustomFormId(stats.ID))
                        BackupStockIfNeeded(instance, stats.ID, existing);
                    if (existing != null)
                    {
                        if (stats.HairSprite == null) stats.HairSprite = existing.HairSprite;
                        if (stats.HeadSprite == null) stats.HeadSprite = existing.HeadSprite;
                        if (stats.Icon == null) stats.Icon = existing.Icon;
                        if (stats.Illustration == null) stats.Illustration = existing.Illustration;
                        if (stats.LimbLayout == null) stats.LimbLayout = existing.LimbLayout;
                        if (stats.RequiredSkin == null) stats.RequiredSkin = existing.RequiredSkin;
                        if (stats.PhyAccessory == null) stats.PhyAccessory = existing.PhyAccessory;
                        if ((stats.SpecialBehaviours == null || stats.SpecialBehaviours.Count == 0)
                            && existing.SpecialBehaviours != null && existing.SpecialBehaviours.Count > 0)
                            stats.SpecialBehaviours = new List<Type>(existing.SpecialBehaviours);
                        if (existing.Size <= 0f)
                            stats.Size = existing.Size;
                    }
                    ct.transformations.TransformationData[stats.ID] = stats;
                }
                else
                    ct.transformations.AddTransformation(stats.ID, stats);
            }

            var tint = instance.GetComponent<CustomFormHairTintBehaviour>();
            if (tint == null) tint = instance.AddComponent<CustomFormHairTintBehaviour>();
            tint.CharacterId = identity.CharacterId;

            var accessories = instance.GetComponent<CustomFormAccessoryBehaviour>();
            if (accessories == null) accessories = instance.AddComponent<CustomFormAccessoryBehaviour>();
            accessories.CharacterId = identity.CharacterId;

            var auraTint = instance.GetComponent<CustomFormAuraTintBehaviour>();
            if (auraTint == null) auraTint = instance.AddComponent<CustomFormAuraTintBehaviour>();
            auraTint.CharacterId = identity.CharacterId;

            FormArtFromDisk.ApplyToPerson(instance);
            ApplySavedFormOrderToPerson(instance);
        }

        public static string EnsureImportFolder()
        {
            string modRoot;
            try { modRoot = ModAPI.Metadata.MetaLocation; }
            catch { modRoot = System.IO.Path.Combine(Application.dataPath, "..", "Mods", "Dragon Ball Singularity"); }
            string root = System.IO.Path.Combine(modRoot, ImportFolder);
            try
            {
                if (!Directory.Exists(root)) Directory.CreateDirectory(root);
            }
            catch { }
            return root;
        }
    }

    public class CustomFormStockBackup : MonoBehaviour
    {
        readonly Dictionary<string, CharacterTransformations.Transformations.TransformationStats> _originals =
            new Dictionary<string, CharacterTransformations.Transformations.TransformationStats>(StringComparer.OrdinalIgnoreCase);

        public void StoreIfAbsent(string formId, CharacterTransformations.Transformations.TransformationStats stats)
        {
            if (string.IsNullOrEmpty(formId) || stats == null) return;
            if (_originals.ContainsKey(formId)) return;
            _originals[formId] = CustomTransformationCatalog.CloneStats(stats);
        }

        public bool TryGet(string formId, out CharacterTransformations.Transformations.TransformationStats stats)
        {
            return _originals.TryGetValue(formId, out stats) && stats != null;
        }

        public bool Has(string formId)
        {
            return !string.IsNullOrEmpty(formId) && _originals.ContainsKey(formId);
        }
    }

    public class CustomFormHairTintBehaviour : MonoBehaviour
    {
        public string CharacterId;
        TransformationContextMenu _menu;
        string _lastForm;

        public void NotifyFormApplied(string formId)
        {
            _lastForm = formId;
        }

        void LateUpdate()
        {
            if (string.IsNullOrEmpty(CharacterId)) return;
            if (_menu == null)
            {
                var head = transform.Find("Head");
                if (head != null) _menu = head.GetComponent<TransformationContextMenu>();
            }
            if (_menu == null) return;
            if (_menu.Form == _lastForm) return;
            _lastForm = _menu.Form;
            var dto = CustomTransformationCatalog.GetForm(CharacterId, _menu.Form);
            if (dto == null) return;
            var headTf = transform.Find("Head");
            if (headTf == null) return;

            if (CustomTransformationCatalog.BakeVirtualSpritePaths(dto, gameObject))
                CustomTransformationCatalog.UpsertForm(CharacterId, null, dto);

            Sprite source = CustomTransformationCatalog.ResolveSpriteReference(dto.HairSpritePath, gameObject, true);
            if (source == null)
                return;

            TmHairApply.ApplyHair(
                headTf,
                source,
                new Color(dto.HairColorR, dto.HairColorG, dto.HairColorB, 1f),
                new Vector3(dto.HairLocalX, dto.HairLocalY, dto.HairLocalZ),
                dto.UseHairLocalPosition);
        }
    }

    public class CustomFormAccessoryBehaviour : MonoBehaviour
    {
        public string CharacterId;
        TransformationContextMenu _menu;
        string _lastForm;
        readonly List<GameObject> _spawned = new List<GameObject>();

        void LateUpdate()
        {
            if (string.IsNullOrEmpty(CharacterId)) return;
            if (_menu == null)
            {
                var head = transform.Find("Head");
                if (head != null) _menu = head.GetComponent<TransformationContextMenu>();
            }
            if (_menu == null) return;
            if (_menu.Form == _lastForm) return;
            _lastForm = _menu.Form;
            ApplyForForm(_menu.Form);
        }

        public void ApplyForForm(string formId)
        {
            ClearSpawned();
            var dto = CustomTransformationCatalog.GetForm(CharacterId, formId);
            if (dto?.Accessories == null || dto.Accessories.Count == 0) return;
            for (int i = 0; i < dto.Accessories.Count; i++)
                SpawnOne(dto.Accessories[i]);
        }

        public void ApplyDraft(CustomTransformationFormDto dto)
        {
            ClearSpawned();
            if (dto?.Accessories == null) return;
            for (int i = 0; i < dto.Accessories.Count; i++)
                SpawnOne(dto.Accessories[i]);
        }

        void ClearSpawned()
        {
            for (int i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] != null) UnityEngine.Object.DestroyImmediate(_spawned[i]);
            }
            _spawned.Clear();
        }

        void SpawnOne(CustomAccessoryDto acc)
        {
            if (acc == null || string.IsNullOrEmpty(acc.SpritePath) || string.IsNullOrEmpty(acc.LimbName)) return;
            var limb = FindLimb(acc.LimbName);
            if (limb == null) return;
            var spr = CustomTransformationCatalog.LoadSpriteFlexible(acc.SpritePath);
            if (spr == null) return;
            string name = string.IsNullOrEmpty(acc.Id) ? "TmAccessory" : ("TmAccessory_" + acc.Id);
            var existing = limb.Find(name);
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);
            float scale = acc.Scale <= 0.01f ? 1f : acc.Scale;
            limb.CreateAccessory(
                name,
                spr,
                Color.white,
                new Vector3(acc.LocalX, acc.LocalY, acc.LocalZ),
                Quaternion.Euler(0f, 0f, acc.RotationZ),
                new Vector3(scale, scale, 1f),
                null,
                -999);
            var spawned = limb.Find(name);
            if (spawned != null)
            {
                if (acc.SortingOrder != 1)
                {
                    var sr = spawned.GetComponent<SpriteRenderer>();
                    if (sr != null) sr.sortingOrder = (limb.GetComponent<SpriteRenderer>()?.sortingOrder ?? 0) + acc.SortingOrder;
                }
                _spawned.Add(spawned.gameObject);
            }
        }

        Transform FindLimb(string limbName)
        {
            if (string.IsNullOrEmpty(limbName)) return null;
            var direct = transform.Find(limbName);
            if (direct != null) return direct;
            var body = transform.Find("Body");
            if (body != null)
            {
                var underBody = body.Find(limbName);
                if (underBody != null) return underBody;
            }
            var all = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (string.Equals(all[i].name, limbName, StringComparison.OrdinalIgnoreCase))
                    return all[i];
            }
            return null;
        }
    }

    public class CustomFormAuraTintBehaviour : MonoBehaviour
    {
        public string CharacterId;
        TransformationContextMenu _menu;
        string _lastForm;
        string _lastAura;
        bool _applied;
        bool _hadAuraInstance;

        public void Invalidate()
        {
            _applied = false;
            _lastForm = null;
            _lastAura = null;
            _hadAuraInstance = false;
        }

        void LateUpdate()
        {
            if (string.IsNullOrEmpty(CharacterId)) return;
            if (_menu == null)
            {
                var head = transform.Find("Head");
                if (head != null) _menu = head.GetComponent<TransformationContextMenu>();
            }
            if (_menu == null) return;

            string form = _menu.Form;
            string auraId = _menu.CurrentAuraID;
            var aura = GetComponent<AuraHandler>();
            bool hasInstance = aura != null && aura.HasSpawnedAuraInstance;
            if (hasInstance && !_hadAuraInstance)
                _applied = false;
            _hadAuraInstance = hasInstance;

            if (form == _lastForm && auraId == _lastAura && _applied) return;

            var dto = CustomTransformationCatalog.GetForm(CharacterId, form);
            if (dto == null)
                return;

            if (!dto.UseCustomAura)
            {
                _lastForm = form;
                _lastAura = auraId;
                _applied = true;
                return;
            }

            if (!hasInstance) return;

            Color primary = new Color(dto.AuraColorR, dto.AuraColorG, dto.AuraColorB, 1f);
            Color secondary = new Color(dto.AuraColor2R, dto.AuraColor2G, dto.AuraColor2B, 1f);
            float scale = dto.AuraScale <= 0.01f ? 1f : dto.AuraScale;
            aura.ApplyCustomAuraColors(primary, secondary, dto.UseAuraSecondaryColor, scale);
            _lastForm = form;
            _lastAura = auraId;
            _applied = true;
        }
    }

    public static class TransformationMakerHairLibrary
    {
        static List<(string Path, Sprite Sprite)> _cache;
        static List<(string Path, Sprite Sprite)> _headCache;

        public static string ModRoot
        {
            get
            {
                try
                {
                    string meta = ModAPI.Metadata.MetaLocation;
                    if (!string.IsNullOrEmpty(meta) && Directory.Exists(System.IO.Path.Combine(meta, "Assets", "Sprites", "Characters")))
                        return meta;
                }
                catch { }

                string mods = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Mods"));
                string[] names = { "DragonBall Playground Ultimate", "Dragon Ball Singularity" };
                for (int i = 0; i < names.Length; i++)
                {
                    string candidate = System.IO.Path.Combine(mods, names[i]);
                    if (Directory.Exists(System.IO.Path.Combine(candidate, "Assets", "Sprites", "Characters")))
                        return candidate;
                }
                return System.IO.Path.Combine(mods, "DragonBall Playground Ultimate");
            }
        }

        static Sprite LoadFileSprite(string absolutePath, out string rel)
        {
            rel = null;
            if (string.IsNullOrEmpty(absolutePath) || !File.Exists(absolutePath)) return null;
            rel = absolutePath.Replace('\\', '/');
            int idx = rel.IndexOf("Assets/", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0) rel = rel.Substring(idx);
            else
            {
                string rootNorm = ModRoot.Replace('\\', '/').TrimEnd('/');
                if (rel.StartsWith(rootNorm, StringComparison.OrdinalIgnoreCase))
                    rel = rel.Substring(rootNorm.Length).TrimStart('/');
            }

            // Prefer disk load. ModAPI.LoadSprite is restricted outside OnLoad/AfterSpawn/Main.
            Sprite spr = CustomTransformationCatalog.LoadSpriteFromDisk(absolutePath);
            if (spr == null)
            {
                try { spr = ModAPI.LoadSprite(rel); } catch { }
            }
            return spr;
        }

        public static List<(string Path, Sprite Sprite)> GetAll()
        {
            if (_cache != null) return _cache;
            _cache = new List<(string, Sprite)>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void AddFile(string absolutePath)
            {
                if (IsIconOrIllustrationAsset(absolutePath)) return;
                Sprite spr = LoadFileSprite(absolutePath, out string rel);
                if (spr == null || string.IsNullOrEmpty(rel) || !seen.Add(rel)) return;
                _cache.Add((rel, spr));
            }

            string charactersRoot = System.IO.Path.Combine(ModRoot, "Assets", "Sprites", "Characters");
            if (Directory.Exists(charactersRoot))
            {
                foreach (var file in Directory.GetFiles(charactersRoot, "*.png", SearchOption.AllDirectories))
                {
                    string name = System.IO.Path.GetFileName(file);
                    if (name.IndexOf("hair", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    AddFile(file);
                }
            }

            string importRoot = CustomTransformationCatalog.EnsureImportFolder();
            if (Directory.Exists(importRoot))
            {
                foreach (var file in Directory.GetFiles(importRoot, "*.png", SearchOption.AllDirectories))
                {
                    if (IsIconOrIllustrationAsset(file)) continue;
                    if (IsLikelyHeadOnlyAsset(file)) continue;
                    string name = System.IO.Path.GetFileNameWithoutExtension(file) ?? "";
                    string folder = (System.IO.Path.GetDirectoryName(file) ?? "").Replace('\\', '/');
                    if (folder.IndexOf("/accessories", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    if (name.StartsWith("acc_", StringComparison.OrdinalIgnoreCase)) continue;

                    bool inHairFolder = folder.IndexOf("/hair", StringComparison.OrdinalIgnoreCase) >= 0;
                    bool namedHair = name.IndexOf("hair", StringComparison.OrdinalIgnoreCase) >= 0
                        || string.Equals(name, "hair", StringComparison.OrdinalIgnoreCase);
                    if (inHairFolder || namedHair)
                    {
                        AddFile(file);
                        continue;
                    }
                }
            }

            _cache = _cache.OrderBy(x => System.IO.Path.GetFileName(x.Path), StringComparer.OrdinalIgnoreCase).ToList();
            return _cache;
        }

        public static bool IsIconOrIllustrationAsset(string file)
        {
            if (string.IsNullOrEmpty(file)) return false;
            string name = System.IO.Path.GetFileNameWithoutExtension(file) ?? "";
            string folder = (System.IO.Path.GetDirectoryName(file) ?? "").Replace('\\', '/');
            if (name.EndsWith("_icon", StringComparison.OrdinalIgnoreCase)) return true;
            if (name.EndsWith("_illustration", StringComparison.OrdinalIgnoreCase)) return true;
            if (name.IndexOf("Empty icon", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (name.IndexOf("EmptyIcon", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (name.IndexOf("Empty illustration", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (name.IndexOf("EmptyIllustration", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (folder.IndexOf("/Icons", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        public static bool IsLikelyHeadOnlyAsset(string file)
        {
            if (string.IsNullOrEmpty(file)) return false;
            string name = System.IO.Path.GetFileNameWithoutExtension(file) ?? "";
            string folder = (System.IO.Path.GetDirectoryName(file) ?? "").Replace('\\', '/');
            if (name.IndexOf("hair", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            if (folder.IndexOf("/hair", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            if (folder.IndexOf("/HeadSkins", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (folder.IndexOf("/head", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (string.Equals(name, "Head", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(name, "Face", StringComparison.OrdinalIgnoreCase)) return true;
            if (name.StartsWith("Head", StringComparison.OrdinalIgnoreCase)) return true;
            if (name.EndsWith("Head", StringComparison.OrdinalIgnoreCase)) return true;
            if (name.EndsWith("_head", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public static List<(string Path, Sprite Sprite)> GetAllHeads()
        {
            if (_headCache != null) return _headCache;
            _headCache = new List<(string, Sprite)>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void AddFile(string absolutePath)
            {
                Sprite spr = LoadFileSprite(absolutePath, out string rel);
                if (spr == null || string.IsNullOrEmpty(rel) || !seen.Add(rel)) return;
                _headCache.Add((rel, spr));
            }

            bool IsHeadFile(string file)
            {
                string name = System.IO.Path.GetFileNameWithoutExtension(file);
                string folder = System.IO.Path.GetDirectoryName(file) ?? "";
                string folderNorm = folder.Replace('\\', '/');
                if (folderNorm.IndexOf("/HeadSkins", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (string.Equals(name, "Head", StringComparison.OrdinalIgnoreCase)) return true;
                if (string.Equals(name, "Face", StringComparison.OrdinalIgnoreCase)) return true;
                if (name.StartsWith("Head", StringComparison.OrdinalIgnoreCase)) return true;
                if (name.EndsWith("Head", StringComparison.OrdinalIgnoreCase)) return true;
                if (name.IndexOf("SSJ", StringComparison.OrdinalIgnoreCase) >= 0 && folderNorm.IndexOf("/Head", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                return false;
            }

            string charactersRoot = System.IO.Path.Combine(ModRoot, "Assets", "Sprites", "Characters");
            if (Directory.Exists(charactersRoot))
            {
                foreach (var file in Directory.GetFiles(charactersRoot, "*.png", SearchOption.AllDirectories))
                {
                    if (!IsHeadFile(file)) continue;
                    AddFile(file);
                }
            }

            string importRoot = CustomTransformationCatalog.EnsureImportFolder();
            if (Directory.Exists(importRoot))
            {
                foreach (var file in Directory.GetFiles(importRoot, "*.png", SearchOption.AllDirectories))
                {
                    if (IsIconOrIllustrationAsset(file)) continue;
                    string name = System.IO.Path.GetFileName(file);
                    string folder = (System.IO.Path.GetDirectoryName(file) ?? "").Replace('\\', '/');
                    bool inHeadFolder = folder.IndexOf("/head", StringComparison.OrdinalIgnoreCase) >= 0
                        || folder.IndexOf("/HeadSkins", StringComparison.OrdinalIgnoreCase) >= 0;
                    bool namedHead = name.IndexOf("head", StringComparison.OrdinalIgnoreCase) >= 0
                        || name.IndexOf("face", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!inHeadFolder && !namedHead) continue;
                    AddFile(file);
                }
            }

            _headCache = _headCache.OrderBy(x => System.IO.Path.GetFileName(x.Path), StringComparer.OrdinalIgnoreCase).ToList();
            return _headCache;
        }

        public static List<(string Path, Sprite Sprite)> GetForCharacter(string characterId, CharacterTransformations ct)
        {
            var all = GetAll();
            var result = new List<(string, Sprite)>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void Add(string path, Sprite spr)
            {
                if (spr == null) return;
                string key = path ?? spr.name;
                if (!seen.Add(key)) return;
                result.Add((path ?? key, spr));
            }

            // Prefer this character's form hairs first, then every hair in the mod.
            if (ct?.transformations?.TransformationData != null)
            {
                foreach (var kvp in ct.transformations.TransformationData)
                {
                    var stats = kvp.Value;
                    if (stats?.HairSprite != null)
                        Add("form:" + (stats.ID ?? kvp.Key), stats.HairSprite);
                }
            }

            string needle = (characterId ?? "").Replace(" ", "");
            if (!string.IsNullOrEmpty(needle))
            {
                foreach (var item in all)
                {
                    string p = (item.Path ?? "").Replace('\\', '/');
                    if (p.IndexOf("/" + characterId + "/", StringComparison.OrdinalIgnoreCase) >= 0
                        || p.IndexOf("/" + needle + "/", StringComparison.OrdinalIgnoreCase) >= 0)
                        Add(item.Path, item.Sprite);
                }
            }

            foreach (var item in all)
                Add(item.Path, item.Sprite);

            return result;
        }

        public static List<(string Path, Sprite Sprite)> GetHeadsForCharacter(string characterId, CharacterTransformations ct)
        {
            var all = GetAllHeads();
            var result = new List<(string, Sprite)>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void Add(string path, Sprite spr)
            {
                if (spr == null) return;
                string key = path ?? spr.name;
                if (!seen.Add(key)) return;
                result.Add((path ?? key, spr));
            }

            if (ct?.transformations?.TransformationData != null)
            {
                foreach (var kvp in ct.transformations.TransformationData)
                {
                    var stats = kvp.Value;
                    if (stats?.HeadSprite != null)
                        Add("form:" + (stats.ID ?? kvp.Key), stats.HeadSprite);
                }
            }

            string needle = (characterId ?? "").Replace(" ", "");
            if (!string.IsNullOrEmpty(needle))
            {
                foreach (var item in all)
                {
                    string p = (item.Path ?? "").Replace('\\', '/');
                    if (p.IndexOf("/" + characterId + "/", StringComparison.OrdinalIgnoreCase) >= 0
                        || p.IndexOf("/" + needle + "/", StringComparison.OrdinalIgnoreCase) >= 0)
                        Add(item.Path, item.Sprite);
                }
            }

            foreach (var item in all)
                Add(item.Path, item.Sprite);

            return result;
        }

        public static void Invalidate()
        {
            _cache = null;
            _headCache = null;
        }
    }

    public static class TransformationMakerState
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

    public class TransformationMakerInput : MonoBehaviour
    {
        static TransformationMakerInput _instance;

        public static void Ensure()
        {
            if (_instance != null) return;
            var go = new GameObject("TransformationMakerInput");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<TransformationMakerInput>();
        }

        static bool PointerOverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (DialogBox.IsAnyDialogboxOpen)
                    return;
                if (TransformationMakerUI.IsCaptureOpen)
                {
                    TransformationMakerUI.CancelCapture();
                    return;
                }
                if (TransformationMakerUI.HasOverlay)
                {
                    TransformationMakerUI.CloseOverlay(true);
                    return;
                }
                if (TransformationMakerUI.IsOpen)
                {
                    TransformationMakerUI.Close();
                    return;
                }
                if (TransformationMakerState.Armed)
                {
                    TransformationMakerState.Disarm();
                    TransformationMakerUI.EmergencyUnstick();
                    ModAPI.Notify("Transformation Maker cancelled");
                    return;
                }
                TransformationMakerUI.EmergencyUnstick();
            }

            if (!TransformationMakerState.Armed) return;
            if (!Input.GetMouseButtonDown(0) || PointerOverUi()) return;

            var pb = SelectionController.Main.CurrentlyUnderMouse;
            if (pb == null) return;
            var person = pb.transform.root.GetComponent<PersonBehaviour>();
            if (person == null) return;

            TransformationMakerUI.OnWorldPersonClicked(person);
        }
    }

    public class TmHairPreviewDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public void OnBeginDrag(PointerEventData eventData) { }

        public void OnDrag(PointerEventData eventData)
        {
            TransformationMakerUI.NudgeHairByScreenDelta(eventData.delta);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            TransformationMakerUI.FinishHairDrag();
        }
    }

    public class TmFormCellDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler, IPointerDownHandler
    {
        public string FormId;
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
            TransformationMakerUI.BeginFormCellDrag(this);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging || _rt == null) return;
            if (eventData.delta.sqrMagnitude > 0.01f) _moved = true;
            TransformationMakerUI.DragFormCell(this, eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragging) return;
            _dragging = false;
            if (_group != null) _group.blocksRaycasts = true;
            TransformationMakerUI.EndFormCellDrag(this, _moved);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (_dragging || _moved) return;
            if (string.IsNullOrEmpty(FormId)) return;
            TransformationMakerUI.SelectFormFromGrid(FormId);
        }
    }

    public class TmLimbPreviewClick : MonoBehaviour, IPointerClickHandler
    {
        public string LimbName;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (string.IsNullOrEmpty(LimbName)) return;
            if (!TransformationMakerUI.AllowPreviewLimbPick) return;
            TransformationMakerUI.OnPreviewLimbClicked(LimbName);
        }
    }


    public class TmAuraRtDriver : MonoBehaviour
    {
        public Camera Cam;

        void LateUpdate()
        {
            if (Cam != null)
                Cam.Render();
        }
    }

    public class TmCaptureDriver : MonoBehaviour
    {
        public Action OnShot;

        public void BeginShot()
        {
            StartCoroutine(ShotCo());
        }

        IEnumerator ShotCo()
        {
            yield return new WaitForEndOfFrame();
            OnShot?.Invoke();
        }
    }

    public static class TransformationMakerUI
    {
        enum ScreenKind { PickPerson, Forms, Editor, Aura, Accessories, AccessoryEdit, Workshop, WorkshopDetail, WorkshopPublish, ShareChoice }
        enum CaptureKind { Icon, Illustration }

        const float UiScale = 7.2f;
        const float PanelW = 180f;
        const float PanelH = 108f;
        const float TitleH = 8f;
        const float NavH = 8f;
        const float CellW = 24f;
        const float CellH = 28f;
        const float FormCellH = 35f;
        const float CellGap = 2.5f;
        const int GridCols = 6;

        const string EmptyIconRel = "Assets/Icons/Empty icon.png";
        const string EmptyIconRelAlt = "Assets/Icons/EmptyIcon.png";
        const string EmptyIllustrationRel = "Assets/Icons/Empty illustration.png";
        const string EmptyIllustrationRelAlt = "Assets/Icons/EmptyIllustration.png";
        const int IconFrameW = 400;
        const int IconFrameH = 400;
        const int IconContentX = 30;
        const int IconContentY = 30;
        const int IconContentW = 340;
        const int IconContentH = 340;
        const int IllFrameW = 1008;
        const int IllFrameH = 1472;
        const int IllContentX = 48;
        const int IllContentY = 48;
        const int IllContentW = 912;
        const int IllContentH = 1376;

        static GameObject _root;
        static GameObject _blocker;
        static GameObject _overlay;
        static RectTransform _panelHostRt;
        static RectTransform _panelRt;
        static TextMeshProUGUI _titleTmp;
        static TextMeshProUGUI _statusTmp;
        static TextMeshProUGUI _hairOffsetLabel;
        static Transform _body;
        static RectTransform _previewHairRt;
        static Vector2 _previewHairBasePos;
        static float _previewFitScale = 1f;
        static string _previewSelectedLimb;
        static string _selectedLimbKey = "Head";
        static readonly string[] SpriteCategories =
        {
            "Hair", "Head", "Upper", "Middle", "Lower", "ULeg", "LLeg", "Foot", "UArm", "LArm"
        };
        static GameObject _previewAccessoryBtn;
        static TMP_FontAsset _font;

        static ScreenKind _screen = ScreenKind.PickPerson;
        static string _characterId;
        static string _displayName;
        static string _formId;
        static bool _creatingNew;
        static CustomTransformationFormDto _draft;
        static PersonBehaviour _targetPerson;
        static CharacterTransformations _targetCt;
        static Color _hairTint = Color.white;
        static string _selectedHairPath;
        static string _selectedHeadPath;
        static List<(string Path, Sprite Sprite)> _hairList = new List<(string, Sprite)>();
        static List<(string Path, Sprite Sprite)> _headList = new List<(string, Sprite)>();
        static int _spriteTab;
        static string _spriteSearch = "";
        static Transform _spriteGridContent;
        static RectTransform _spritePanelRt;
        static Color _auraPrimary = new Color(1f, 0.95f, 0.35f, 1f);
        static Color _auraSecondary = new Color(1f, 1f, 0.75f, 1f);
        static int _editAccessoryIndex = -1;
        static string _auraBackupId;
        static bool _auraBackupUseCustom;
        static bool _auraBackupUseSecondary;
        static float _auraBackupScale;
        static float _auraBackupR, _auraBackupG, _auraBackupB;
        static float _auraBackup2R, _auraBackup2G, _auraBackup2B;
        static Color _auraBackupPrimary;
        static Color _auraBackupSecondary;
        static GameObject _auraRtWorld;
        static Camera _auraRtCam;
        static RenderTexture _auraRt;
        static RawImage _auraRtImage;
        static GameObject _auraRtInstance;
        static TmAuraRtDriver _auraRtDriver;
        static GameObject _captureRoot;
        static RectTransform _captureFrameRt;
        static GameObject _captureHud;
        static CaptureKind _captureKind;
        static TmCaptureDriver _captureDriver;
        static bool _captureBusy;
        static bool _standaloneCapture;
        static int _captureSx, _captureSy, _captureSw, _captureSh;
        static List<DbpuWorkshopRemoteItem> _workshopRemote;
        static bool _workshopLoading;
        static string _workshopStatus;
        static int _workshopPage = 1;
        static int _workshopTotal;
        static DbpuWorkshopRemoteItem _workshopSelected;
        static CustomTransformationFormDto _workshopPublishForm;
        static string _workshopPublishTitle = "";
        static string _workshopPublishDescription = "";
        static string _workshopPublishPreviewPath;
        static bool _workshopUploading;
        static string _workshopUploadStatus;
        static ScreenKind _captureReturnScreen = ScreenKind.Editor;

        static Transform _formGridContent;
        static ScrollRect _formGridScroll;
        static readonly List<string> _formGridIds = new List<string>();
        static readonly List<RectTransform> _formGridCells = new List<RectTransform>();
        static TmFormCellDrag _formDragActive;

        static readonly Color PanelBg = new Color(0.06f, 0.06f, 0.06f, 0.98f);
        static readonly Color TitleBarBg = new Color(0.14f, 0.14f, 0.14f, 1f);
        static readonly Color CellBg = new Color(0.12f, 0.12f, 0.12f, 1f);
        static readonly Color NameBar = new Color(0f, 0f, 0f, 0.7f);
        static readonly Color Accent = new Color(0.35f, 0.55f, 0.85f, 1f);

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
            TransformationMakerInput.Ensure();
            CustomTransformationCatalog.EnsureLoaded();

            _screen = ScreenKind.PickPerson;
            _characterId = null;
            _displayName = null;
            _formId = null;
            _creatingNew = false;
            _draft = null;
            _targetPerson = null;
            _targetCt = null;
            _standaloneCapture = false;

            SetWorldClickMode(true);
            ModAPI.Notify("Click a person to open Transformation Maker");
        }

        public static void OpenStandaloneCapture(bool icon)
        {
            PowerGiverState.Disarm();
            CharacterPowerPickerState.Disarm();
            CancelCapture();
            _standaloneCapture = true;
            OpenCapture(icon ? CaptureKind.Icon : CaptureKind.Illustration);
        }

        static void EnsureShell()
        {
            if (_root != null)
            {
                _root.SetActive(true);
                return;
            }

            _root = new GameObject("TransformationMakerUI");
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
            CancelCapture();
            DestroyAuraRtPreview();
            CloseOverlay(false);
            TransformationMakerState.Disarm();
            _makerUiHideDepth = 0;
            _makerUiWantVisible = false;
            _targetPerson = null;
            _targetCt = null;
            _body = null;
            _panelRt = null;
            _panelHostRt = null;
            _titleTmp = null;
            _statusTmp = null;
            _hairOffsetLabel = null;
            _previewHairRt = null;
            _blocker = null;
            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root);
                _root = null;
            }
            ForceClearUiBlocker();
        }

        public static void CloseOverlay(bool restoreMain)
        {
            if (_overlay != null)
            {
                UnityEngine.Object.Destroy(_overlay);
                _overlay = null;
            }
            TmInputGuard.ResetAll();
            if (restoreMain && _root != null)
                _root.SetActive(true);
        }

        static void ForceClearUiBlocker()
        {
            TmInputGuard.ResetAll();
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
            if (_root != null)
                _root.SetActive(false);
            TransformationMakerState.Arm("PickPerson");
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

            if (picking)
                TransformationMakerState.Arm("PickPerson");
            else
                TransformationMakerState.Disarm();
        }

        public static void NudgeHairByScreenDelta(Vector2 screenDelta)
        {
            if (_draft == null) return;
            float sens = 0.0032f / Mathf.Max(0.2f, _previewFitScale);
            _draft.HairLocalX += screenDelta.x * sens;
            _draft.HairLocalY += screenDelta.y * sens;
            _draft.UseHairLocalPosition = true;
            ApplyHairOffsetLive();
            if (_hairOffsetLabel != null)
                _hairOffsetLabel.text = "Hair Offset  X " + _draft.HairLocalX.ToString("0.###") + "  Y " + _draft.HairLocalY.ToString("0.###");
        }

        public static void FinishHairDrag()
        {
            ApplyDraftToTarget();
        }

        public static bool AllowPreviewLimbPick =>
            _screen == ScreenKind.Editor
            || _screen == ScreenKind.Accessories
            || _screen == ScreenKind.AccessoryEdit;

        public static void OnPreviewLimbClicked(string limbName)
        {
            if (!AllowPreviewLimbPick) return;
            if (string.IsNullOrEmpty(limbName) || _draft == null) return;
            string mapped = DbpuSpriteEditorBridge.MapLimbKey(limbName);
            _previewSelectedLimb = mapped;
            _selectedLimbKey = mapped;
            if (Array.IndexOf(SpriteCategories, mapped) >= 0)
                _spriteTab = Array.IndexOf(SpriteCategories, mapped);
            if (_screen == ScreenKind.Editor) RebuildBody();
            else RefreshPreviewAccessoryButton();
        }

        static void ClearPreviewLimbPick()
        {
            _previewSelectedLimb = null;
            if (_previewAccessoryBtn != null)
            {
                UnityEngine.Object.Destroy(_previewAccessoryBtn);
                _previewAccessoryBtn = null;
            }
        }

        static void RefreshPreviewAccessoryButton()
        {
            if (_previewAccessoryBtn != null)
            {
                UnityEngine.Object.Destroy(_previewAccessoryBtn);
                _previewAccessoryBtn = null;
            }
            if (!AllowPreviewLimbPick) return;
            if (string.IsNullOrEmpty(_previewSelectedLimb) || _body == null) return;

            Transform parent = _body.Find("Center");
            if (parent == null) parent = _body;
            var parentRt = parent.GetComponent<RectTransform>();
            if (parentRt == null) return;

            _previewAccessoryBtn = new GameObject("AddAccessoryBtn");
            _previewAccessoryBtn.transform.SetParent(parent, false);
            var rt = _previewAccessoryBtn.AddComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(0f, -parentRt.sizeDelta.y * 0.5f + 14f);
            rt.sizeDelta = new Vector2(52f, 7f);
            _previewAccessoryBtn.AddComponent<Image>().color = Accent;
            string limb = _previewSelectedLimb;
            if (string.Equals(limb, "Hair", StringComparison.OrdinalIgnoreCase))
                limb = "Head";
            _previewAccessoryBtn.AddComponent<Button>().onClick.AddListener(() => BeginAccessoryForLimb(limb));
            var labelGo = new GameObject("L");
            labelGo.transform.SetParent(_previewAccessoryBtn.transform, false);
            var lrt = labelGo.AddComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            var tmp = labelGo.AddComponent<TextMeshProUGUI>();
            tmp.text = "Add Accessory (" + limb + ")";
            tmp.font = Font;
            tmp.fontSize = 2.2f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
        }

        static void BeginAccessoryForLimb(string limbName)
        {
            if (_draft == null || string.IsNullOrEmpty(limbName)) return;
            if (_draft.Accessories == null) _draft.Accessories = new List<CustomAccessoryDto>();
            _draft.Accessories.Add(new CustomAccessoryDto
            {
                Id = DateTime.Now.Ticks.ToString("x"),
                Name = "Accessory",
                LimbName = limbName,
                Scale = 1f,
                SortingOrder = 1
            });
            EditAccessory(_draft.Accessories.Count - 1);
        }

        static void ApplyHairOffsetLive()
        {
            if (_draft == null) return;
            if (_targetPerson != null)
            {
                var head = _targetPerson.transform.root.Find("Head");
                if (head != null)
                {
                    var hair = TmHairApply.EnsureSingleHair(head);
                    if (hair != null)
                        hair.localPosition = new Vector3(_draft.HairLocalX, _draft.HairLocalY, _draft.HairLocalZ);
                }
            }
            if (_previewHairRt != null)
            {
                float uiPerWorld = 18f * _previewFitScale;
                _previewHairRt.anchoredPosition = _previewHairBasePos + new Vector2(_draft.HairLocalX * uiPerWorld, _draft.HairLocalY * uiPerWorld);
            }
        }

        public static void OnWorldPersonClicked(PersonBehaviour person)
        {
            if (person == null) return;
            bool editing = _screen == ScreenKind.Editor || _screen == ScreenKind.Aura
                || _screen == ScreenKind.Accessories || _screen == ScreenKind.AccessoryEdit;
            if (_screen != ScreenKind.PickPerson && !editing) return;

            var root = person.transform.root.gameObject;
            EnsurePersonTransformSupport(person);
            var identity = root.GetComponent<DbpuCharacterIdentity>();
            var ct = root.GetComponent<CharacterTransformations>();
            if (ct == null)
            {
                ModAPI.Notify("Could not set up transformations on that person.");
                return;
            }

            _targetPerson = person;
            _targetCt = ct;
            CustomTransformationCatalog.ApplyToPerson(root);
            DbpuMakerPhysics.CalmPerson(person);

            if (identity != null)
            {
                _characterId = identity.CharacterId;
                _displayName = identity.DisplayName ?? identity.CharacterId;
            }
            else
            {
                _characterId = person.name;
                _displayName = person.name;
            }

            EnsureShell();

            if (editing && _draft != null)
            {
                ApplyDraftToTarget();
                ModAPI.Notify("Preview bound to " + _displayName);
                SetWorldClickMode(false);
                RebuildBody();
                return;
            }

            _screen = ScreenKind.Forms;
            SetWorldClickMode(false);
            RebuildBody();
            ModAPI.Notify("Editing " + _displayName);
        }

        public static void EnsurePersonTransformSupport(PersonBehaviour person)
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
            else if (string.IsNullOrEmpty(identity.CharacterId))
            {
                identity.CharacterId = "World_" + root.GetInstanceID().ToString("x");
            }
            if (string.IsNullOrEmpty(identity.DisplayName))
                identity.DisplayName = root.name;

            var ct = root.GetComponent<CharacterTransformations>();
            if (ct == null) ct = root.AddComponent<CharacterTransformations>();
            if (ct.transformations == null) ct.transformations = new CharacterTransformations.Transformations();
            ct.transformations.Owner = ct;

            if (ct.transformations.TransformationData == null || ct.transformations.TransformationData.Count == 0)
            {
                float size = Mathf.Abs(person.transform.localScale.y) * 1.68f;
                if (size < 0.2f) size = 1.75f;
                var baseForm = new CharacterTransformations.Transformations.TransformationStats
                {
                    ID = "Base",
                    Name = "Base",
                    Description = "Base form",
                    PowerMultiplier = 1f,
                    Size = size,
                    AuraID = "Base",
                    KiDrainRate = 0f
                };
                ct.transformations.AddTransformation("Base", baseForm);
            }

            if (root.GetComponent<KiSystem>() == null)
            {
                var ki = root.AddComponent<KiSystem>();
                ki.maxKi = 100000f;
                ki.baseRegen = 500f;
                ki.EnsureReadyForTransforms();
            }
            else
            {
                root.GetComponent<KiSystem>().EnsureReadyForTransforms();
            }

            var aura = root.GetComponent<AuraHandler>();
            if (aura == null) aura = root.AddComponent<AuraHandler>();
            aura.person = person;

            var head = root.transform.Find("Head");
            if (head == null && person.Limbs != null)
            {
                for (int i = 0; i < person.Limbs.Length; i++)
                {
                    if (person.Limbs[i] != null && person.Limbs[i].name.IndexOf("Head", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        head = person.Limbs[i].transform;
                        break;
                    }
                }
            }

            PowerSetup.EnsurePowerMenu(root, person);
            PowerSetup.EnsureModFighterDurability(person);

            if (head != null)
            {
                bool createdMenu = false;
                var menu = head.GetComponent<TransformationContextMenu>();
                if (menu == null)
                {
                    menu = head.gameObject.AddComponent<TransformationContextMenu>();
                    createdMenu = true;
                }
                menu.TransformationsData = ct;
                menu.person = person;
                if (string.IsNullOrEmpty(menu.Form) || !ct.transformations.TransformationData.ContainsKey(menu.Form))
                    menu.Form = "Base";
                if (string.IsNullOrEmpty(menu.CurrentAuraID)) menu.CurrentAuraID = "Base";
                menu.AuraHandler = aura;
                menu.EnsureBesidePowersMenu();

                if (person.Limbs != null && person.Limbs.Length > 0 && person.Limbs[0] != null
                    && person.Limbs[0].gameObject != head.gameObject)
                {
                    var limbMenu = person.Limbs[0].GetComponent<TransformationContextMenu>();
                    if (limbMenu == null) limbMenu = person.Limbs[0].gameObject.AddComponent<TransformationContextMenu>();
                    limbMenu.TransformationsData = ct;
                    limbMenu.person = person;
                    limbMenu.Form = menu.Form;
                    limbMenu.CurrentAuraID = menu.CurrentAuraID;
                    limbMenu.AuraHandler = aura;
                    limbMenu.EnsureBesidePowersMenu();
                }

                if (createdMenu)
                    ModAPI.Notify("Transformations Menu added beside Powers Menu");
            }

            FormArtFromDisk.ApplyToPerson(root);
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
                case ScreenKind.Forms:
                    _titleTmp.text = (_displayName ?? "Character") + " Forms";
                    BuildFormGrid();
                    break;
                case ScreenKind.Editor:
                    _titleTmp.text = _creatingNew ? "New Transformation" : (_draft?.Name ?? "Edit Transformation");
                    BuildEditor();
                    break;
                case ScreenKind.Aura:
                    _titleTmp.text = "Aura Maker";
                    BuildAuraScreen();
                    break;
                case ScreenKind.Accessories:
                    _titleTmp.text = "Accessories";
                    BuildAccessoriesScreen();
                    break;
                case ScreenKind.AccessoryEdit:
                    _titleTmp.text = "Edit Accessory";
                    BuildAccessoryEditScreen();
                    break;
                case ScreenKind.Workshop:
                    _titleTmp.text = "Steam Workshop";
                    BuildWorkshopBrowser();
                    break;
                case ScreenKind.WorkshopDetail:
                    _titleTmp.text = "Workshop Item";
                    BuildWorkshopDetailScreen();
                    break;
                case ScreenKind.WorkshopPublish:
                    _titleTmp.text = "Publish to Workshop";
                    BuildWorkshopPublishScreen();
                    break;
                case ScreenKind.ShareChoice:
                    _titleTmp.text = "Share Form";
                    BuildShareChoiceScreen();
                    break;
                default:
                    _titleTmp.text = "Transformation Maker";
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
            float contentW = PanelW - 8f;
            content.GetComponent<RectTransform>().sizeDelta = new Vector2(contentW, Mathf.Max(gridH + 6f, 40f));

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

        static void BuildFormGrid()
        {
            SetWorldClickMode(false);
            float bodyH = PanelH - TitleH - 3f;
            CreateNavBar(_body, "Back", () =>
            {
                _targetPerson = null;
                _targetCt = null;
                _characterId = null;
                _displayName = null;
                _screen = ScreenKind.PickPerson;
                CloseOverlay(false);
                if (_root != null)
                {
                    UnityEngine.Object.Destroy(_root);
                    _root = null;
                    _body = null;
                    _panelRt = null;
                    _panelHostRt = null;
                    _titleTmp = null;
                    _statusTmp = null;
                    _hairOffsetLabel = null;
                    _previewHairRt = null;
                    _blocker = null;
                }
                SetWorldClickMode(true);
                ModAPI.Notify("Click a person to open Transformation Maker");
            }, "New Form", BeginNewForm, "Import", ImportSharedForm);

            var forms = CollectForms();
            _formGridIds.Clear();
            _formGridCells.Clear();
            _formDragActive = null;
            var scrollRt = CreateScroll(_body, new Vector2(0f, -NavH), new Vector2(PanelW - 6f, bodyH - NavH - 2f), out Transform content);
            _formGridContent = content;
            _formGridScroll = scrollRt.GetComponent<ScrollRect>();
            PlaceGrid(content, forms.Count, (i, pos) =>
            {
                var form = forms[i];
                string label = form.Custom ? form.Name + " *" : form.Name;
                bool hasSaved = CustomTransformationCatalog.HasSavedForm(_characterId, form.Id);
                bool canReset = hasSaved && !CustomTransformationCatalog.IsPureCustomFormId(form.Id);
                bool canDelete = hasSaved && CustomTransformationCatalog.IsPureCustomFormId(form.Id);
                bool canShare = hasSaved;
                var cell = CreateFormGridCell(content, label, form.Icon, pos, form.Id, canShare, canReset, canDelete, i);
                _formGridIds.Add(form.Id);
                _formGridCells.Add(cell);
            }, CellW, FormCellH);
        }

        static List<(string Id, string Name, Sprite Icon, bool Custom)> CollectForms()
        {
            var byId = new Dictionary<string, (string Id, string Name, Sprite Icon, bool Custom)>(StringComparer.OrdinalIgnoreCase);
            var catalogIds = new HashSet<string>(
                CustomTransformationCatalog.GetForms(_characterId).Select(f => f.ID),
                StringComparer.OrdinalIgnoreCase);

            if (_targetCt != null && _targetCt.transformations?.TransformationData != null)
            {
                foreach (var kvp in _targetCt.transformations.TransformationData)
                {
                    var s = kvp.Value;
                    if (s == null || string.IsNullOrEmpty(s.ID)) continue;
                    bool custom = catalogIds.Contains(s.ID);
                    byId[s.ID] = (s.ID, s.Name ?? s.ID, s.Icon ?? s.Illustration, custom);
                }
            }
            foreach (var custom in CustomTransformationCatalog.GetForms(_characterId))
            {
                if (custom == null || string.IsNullOrEmpty(custom.ID) || byId.ContainsKey(custom.ID)) continue;
                byId[custom.ID] = (custom.ID, custom.Name ?? custom.ID, CustomTransformationCatalog.ToStats(custom)?.Icon, true);
            }

            var forms = new List<(string Id, string Name, Sprite Icon, bool Custom)>();
            var order = CustomTransformationCatalog.GetFormOrder(_characterId, byId.Keys);
            var placed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < order.Count; i++)
            {
                string id = order[i];
                if (!byId.TryGetValue(id, out var form) || !placed.Add(id)) continue;
                forms.Add(form);
            }
            foreach (var form in byId.Values)
            {
                if (!placed.Add(form.Id)) continue;
                forms.Add(form);
            }
            return forms;
        }

        static Vector2 FormGridSlotPos(int index)
        {
            float stepX = CellW + CellGap;
            float stepY = FormCellH + CellGap;
            float gridW = GridCols * CellW + (GridCols - 1) * CellGap;
            float startX = -gridW * 0.5f + CellW * 0.5f;
            float startY = -2f - FormCellH * 0.5f;
            int col = index % GridCols;
            int row = index / GridCols;
            return new Vector2(startX + col * stepX, startY - row * stepY);
        }

        static int FormGridIndexFromLocal(Vector2 local)
        {
            if (_formGridIds.Count == 0) return 0;
            float stepX = CellW + CellGap;
            float stepY = FormCellH + CellGap;
            float gridW = GridCols * CellW + (GridCols - 1) * CellGap;
            float startX = -gridW * 0.5f + CellW * 0.5f;
            float startY = -2f - FormCellH * 0.5f;
            int col = Mathf.RoundToInt((local.x - startX) / stepX);
            int row = Mathf.RoundToInt((startY - local.y) / stepY);
            col = Mathf.Clamp(col, 0, GridCols - 1);
            row = Mathf.Max(0, row);
            int index = row * GridCols + col;
            return Mathf.Clamp(index, 0, _formGridIds.Count - 1);
        }

        public static void BeginFormCellDrag(TmFormCellDrag drag)
        {
            _formDragActive = drag;
            if (_formGridScroll != null)
                _formGridScroll.enabled = false;
        }

        public static void DragFormCell(TmFormCellDrag drag, PointerEventData eventData)
        {
            if (drag == null || _formGridContent == null) return;
            var contentRt = _formGridContent as RectTransform;
            var cellRt = drag.transform as RectTransform;
            if (contentRt == null || cellRt == null) return;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(contentRt, eventData.position, eventData.pressEventCamera, out var local))
                cellRt.anchoredPosition = local;

            int from = drag.Index;
            int to = FormGridIndexFromLocal(cellRt.anchoredPosition);
            if (to == from || to < 0 || to >= _formGridIds.Count) return;

            string id = _formGridIds[from];
            var moved = _formGridCells[from];
            _formGridIds.RemoveAt(from);
            _formGridCells.RemoveAt(from);
            _formGridIds.Insert(to, id);
            _formGridCells.Insert(to, moved);
            drag.Index = to;

            for (int i = 0; i < _formGridCells.Count; i++)
            {
                var rt = _formGridCells[i];
                if (rt == null) continue;
                var d = rt.GetComponent<TmFormCellDrag>();
                if (d != null) d.Index = i;
                if (d == drag) continue;
                rt.anchoredPosition = FormGridSlotPos(i);
            }
        }

        public static void EndFormCellDrag(TmFormCellDrag drag, bool moved)
        {
            if (_formGridScroll != null)
                _formGridScroll.enabled = true;
            _formDragActive = null;
            if (drag == null) return;

            var cellRt = drag.transform as RectTransform;
            if (cellRt != null)
                cellRt.anchoredPosition = FormGridSlotPos(drag.Index);

            if (!moved || string.IsNullOrEmpty(_characterId)) return;

            CustomTransformationCatalog.SetFormOrder(_characterId, _formGridIds);
            if (_targetCt?.transformations != null)
                _targetCt.transformations.ApplyFormOrder(_formGridIds);
        }

        public static void SelectFormFromGrid(string formId)
        {
            SelectForm(formId);
        }

        static void BeginNewForm()
        {
            _creatingNew = true;
            _formId = "Custom_" + DateTime.Now.Ticks.ToString("x");
            _draft = new CustomTransformationFormDto
            {
                ID = _formId,
                Name = "Custom Form",
                Description = "Custom transformation",
                PowerMultiplier = 10f,
                Size = 1.75f,
                AuraID = "Base",
                UseCustomAura = false,
                AuraColorR = 1f,
                AuraColorG = 0.95f,
                AuraColorB = 0.35f,
                AuraColor2R = 1f,
                AuraColor2G = 1f,
                AuraColor2B = 0.75f,
                AuraScale = 1f,
                HairColorR = 1f,
                HairColorG = 1f,
                HairColorB = 1f,
                Accessories = new List<CustomAccessoryDto>(),
                LimbSpritePaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            };
            _hairTint = Color.white;
            _selectedHairPath = null;
            _selectedHeadPath = null;
            _selectedLimbKey = "Head";
            _previewSelectedLimb = "Head";
            _auraPrimary = new Color(1f, 0.95f, 0.35f, 1f);
            _auraSecondary = new Color(1f, 1f, 0.75f, 1f);
            _spriteTab = 1;
            _screen = ScreenKind.Editor;
            if (_targetPerson != null) ApplyDraftToTarget();
            RebuildBody();
        }

        static void SelectForm(string formId)
        {
            _formId = formId;
            _creatingNew = false;
            _draft = CustomTransformationCatalog.CloneForm(CustomTransformationCatalog.GetForm(_characterId, formId));
            if (_draft == null && _targetCt != null && _targetCt.transformations.TransformationData.TryGetValue(formId, out var stats))
                _draft = CustomTransformationCatalog.FromStats(stats);
            if (_draft == null)
                _draft = new CustomTransformationFormDto { ID = formId, Name = formId, PowerMultiplier = 1f, Size = 1.75f, AuraID = "Base", Accessories = new List<CustomAccessoryDto>(), LimbSpritePaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) };
            if (_draft.Accessories == null) _draft.Accessories = new List<CustomAccessoryDto>();
            CustomTransformationCatalog.EnsureLimbSpritePaths(_draft);

            _hairTint = new Color(_draft.HairColorR, _draft.HairColorG, _draft.HairColorB, 1f);
            _selectedHairPath = _draft.HairSpritePath;
            _selectedHeadPath = _draft.HeadSpritePath;
            _selectedLimbKey = "Head";
            _previewSelectedLimb = "Head";
            _auraPrimary = new Color(_draft.AuraColorR, _draft.AuraColorG, _draft.AuraColorB, 1f);
            _auraSecondary = new Color(_draft.AuraColor2R, _draft.AuraColor2G, _draft.AuraColor2B, 1f);
            _spriteTab = 1;
            _screen = ScreenKind.Editor;
            if (_targetPerson != null) ApplyDraftToTarget();
            RebuildBody();
        }

        static void CreateNavBar(Transform parent, string leftLabel, Action left, string rightLabel, Action right, string midLabel = null, Action mid = null)
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
            CreateSmallButtonColored(parent, label, pos, onClick, new Color(0.18f, 0.18f, 0.18f, 1f), width, fromTop);
        }

        static GameObject CreateSmallButtonColored(Transform parent, string label, Vector2 pos, Action onClick, Color bg, float width = 32f, bool fromTop = false)
        {
            var go = new GameObject("Btn_" + label);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            if (fromTop) AnchorTopCenter(rt);
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

        static RectTransform CreateFormGridCell(
            Transform parent,
            string name,
            Sprite icon,
            Vector2 pos,
            string formId,
            bool canShare,
            bool canReset,
            bool canDelete,
            int index)
        {
            var cell = new GameObject("FormCell");
            cell.transform.SetParent(parent, false);
            var rt = cell.AddComponent<RectTransform>();
            AnchorTopCenter(rt);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(CellW, FormCellH);
            var cellBg = cell.AddComponent<Image>();
            cellBg.color = CellBg;
            cellBg.raycastTarget = true;

            var drag = cell.AddComponent<TmFormCellDrag>();
            drag.FormId = formId;
            drag.Index = index;

            var editGo = new GameObject("EditHit");
            editGo.transform.SetParent(cell.transform, false);
            var editRt = editGo.AddComponent<RectTransform>();
            editRt.anchorMin = Vector2.zero;
            editRt.anchorMax = Vector2.one;
            editRt.offsetMin = new Vector2(0f, 5.2f);
            editRt.offsetMax = Vector2.zero;
            var editImg = editGo.AddComponent<Image>();
            editImg.color = new Color(0f, 0f, 0f, 0.001f);
            editImg.raycastTarget = false;

            if (icon != null)
            {
                var iconGo = new GameObject("Icon");
                iconGo.transform.SetParent(editGo.transform, false);
                var irt = iconGo.AddComponent<RectTransform>();
                irt.anchorMin = new Vector2(0.5f, 0.5f);
                irt.anchorMax = new Vector2(0.5f, 0.5f);
                irt.anchoredPosition = new Vector2(0f, 1.2f);
                irt.sizeDelta = new Vector2(CellW - 4f, FormCellH - 18f);
                var img = iconGo.AddComponent<Image>();
                img.sprite = icon;
                img.preserveAspect = true;
                img.raycastTarget = false;
            }

            var bar = new GameObject("NameBar");
            bar.transform.SetParent(editGo.transform, false);
            var brt = bar.AddComponent<RectTransform>();
            brt.anchorMin = new Vector2(0f, 0f);
            brt.anchorMax = new Vector2(1f, 0f);
            brt.pivot = new Vector2(0.5f, 0f);
            brt.anchoredPosition = Vector2.zero;
            brt.sizeDelta = new Vector2(0f, 5.2f);
            bar.AddComponent<Image>().color = NameBar;

            var labelGo = new GameObject("Name");
            labelGo.transform.SetParent(bar.transform, false);
            var lrt = labelGo.AddComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(0.4f, 0.2f);
            lrt.offsetMax = new Vector2(-0.4f, -0.2f);
            var tmp = labelGo.AddComponent<TextMeshProUGUI>();
            tmp.text = name ?? "";
            tmp.font = Font;
            tmp.fontSize = 1.85f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.raycastTarget = false;

            float actionY = 2.5f;
            if (canShare && (canReset || canDelete))
            {
                CreateTinyFormAction(cell.transform, "Share", new Vector2(-5.6f, actionY), 10.6f,
                    () => ConfirmShareForm(formId));
                if (canReset)
                    CreateTinyFormAction(cell.transform, "Reset", new Vector2(5.6f, actionY), 10.6f,
                        () => ConfirmResetForm(formId));
                else
                    CreateTinyFormAction(cell.transform, "Del", new Vector2(5.6f, actionY), 10.6f,
                        () => ConfirmDeleteForm(formId));
            }
            else if (canShare)
            {
                CreateTinyFormAction(cell.transform, "Share", new Vector2(0f, actionY), 14f,
                    () => ConfirmShareForm(formId));
            }
            else if (canReset)
            {
                CreateTinyFormAction(cell.transform, "Reset", new Vector2(0f, actionY), 14f,
                    () => ConfirmResetForm(formId));
            }
            else if (canDelete)
            {
                CreateTinyFormAction(cell.transform, "Del", new Vector2(0f, actionY), 14f,
                    () => ConfirmDeleteForm(formId));
            }

            return rt;
        }

        static void CreateTinyFormAction(Transform parent, string label, Vector2 pos, float width, Action onClick, bool enabled = true)
        {
            var go = new GameObject("Act_" + label);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(width, 4.4f);
            go.AddComponent<Image>().color = enabled
                ? new Color(0.2f, 0.22f, 0.28f, 1f)
                : new Color(0.14f, 0.14f, 0.16f, 1f);
            var button = go.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.interactable = enabled;
            if (enabled)
            {
                button.onClick.AddListener(() =>
                {
                    try { onClick?.Invoke(); }
                    catch (Exception ex) { DialogBoxManager.Notification(label + " failed: " + ex.Message); }
                });
            }
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
            tmp.fontSize = 1.7f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = enabled ? Color.white : new Color(0.45f, 0.45f, 0.45f, 1f);
            tmp.raycastTarget = false;
        }

        static void BuildEditor()
        {
            if (_draft == null) _draft = new CustomTransformationFormDto { ID = _formId ?? "Custom", Name = "Custom Form" };
            _hairList = TransformationMakerHairLibrary.GetForCharacter(_characterId, _targetCt);
            _headList = TransformationMakerHairLibrary.GetHeadsForCharacter(_characterId, _targetCt);
            SetWorldClickMode(false);

            float bodyH = PanelH - TitleH - 3f;
            CreateNavBar(_body, "Back", () =>
            {
                _screen = ScreenKind.Forms;
                RebuildBody();
            }, "Save", SaveDraft);

            float panelTop = -NavH - 0.5f;
            float panelH = bodyH - NavH - 1.5f;

            var left = CreatePanel(_body, "Left", new Vector2(-(PanelW * 0.5f - 28f), panelTop - panelH * 0.5f), new Vector2(50f, panelH));
            BuildPropertyList(left);

            var center = CreatePanel(_body, "Center", new Vector2(0f, panelTop - panelH * 0.5f), new Vector2(58f, panelH));
            BuildCenterPreview(center);

            var right = CreatePanel(_body, "Right", new Vector2(PanelW * 0.5f - 28f, panelTop - panelH * 0.5f), new Vector2(50f, panelH));
            BuildSpritePanel(right);
        }

        static void PreviewLive()
        {
            ApplyDraftToTarget();
            RebuildBody();
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
                PreviewLive();
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

            var layerRts = new List<RectTransform>();
            BuildAuraRtPreview(holder.transform, size);

            if (_targetPerson == null)
            {
                CreateLabel(holder.transform, "No person", Vector2.zero, 2.4f);
                return;
            }

            var root = _targetPerson.transform.root;
            var head = root.Find("Head");
            if (head == null)
            {
                CreateLabel(holder.transform, _displayName ?? "Person", Vector2.zero, 2.4f);
                return;
            }

            Sprite draftHair = ResolveSelectedSprite(_selectedHairPath ?? _draft?.HairSpritePath, _hairList);
            Sprite draftHead = ResolveSelectedSprite(_selectedHeadPath ?? _draft?.HeadSpritePath, _headList);

            var hairTf = TmHairApply.EnsureSingleHair(head);
            if (hairTf != null)
            {
                var hairSr = hairTf.GetComponent<SpriteRenderer>();
                if (hairSr == null) hairSr = hairTf.gameObject.AddComponent<SpriteRenderer>();
                if (draftHair != null)
                {
                    hairSr.sprite = draftHair;
                    hairSr.color = _hairTint;
                    hairSr.enabled = true;
                }
            }

            Vector3 origin = head.position;
            float uiPerWorld = 18f;
            var layers = new List<(SpriteRenderer Sr, int Order)>();
            bool hairAdded = false;

            var liveAura = root.GetComponent<AuraHandler>();
            GameObject auraObj = liveAura != null ? liveAura.CurrentAuraObject : null;

            foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr == null || !sr.enabled) continue;
                if (auraObj != null && (sr.transform == auraObj.transform || sr.transform.IsChildOf(auraObj.transform)))
                    continue;
                string n = sr.gameObject.name;
                if (n.IndexOf("Particle", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (n.IndexOf("Glow", StringComparison.OrdinalIgnoreCase) >= 0
                    && n.IndexOf("TransformationHairGlow", StringComparison.OrdinalIgnoreCase) < 0
                    && !string.Equals(n, "Hair", StringComparison.OrdinalIgnoreCase))
                    continue;
                bool isHairLayer = string.Equals(n, "Hair", StringComparison.OrdinalIgnoreCase);
                if (isHairLayer)
                {
                    if (hairAdded) continue;
                    if (sr.transform.parent != head) continue;
                    if (sr.sprite == null && draftHair == null) continue;
                    hairAdded = true;
                }
                else if (sr.sprite == null)
                {
                    continue;
                }
                layers.Add((sr, sr.sortingOrder));
            }

            if (!hairAdded && draftHair != null && hairTf != null)
            {
                var hairSr = hairTf.GetComponent<SpriteRenderer>();
                if (hairSr != null)
                {
                    if (hairSr.sprite == null) hairSr.sprite = draftHair;
                    layers.Add((hairSr, hairSr.sortingOrder));
                    hairAdded = true;
                }
            }

            layers = layers.OrderBy(l => l.Order).ToList();
            bool limbPick = AllowPreviewLimbPick;
            RectTransform hairRt = null;
            foreach (var layer in layers)
            {
                var sr = layer.Sr;
                Sprite spr = sr.sprite;
                Color col = sr.color;
                bool isHair = string.Equals(sr.transform.name, "Hair", StringComparison.OrdinalIgnoreCase);
                bool isHead = sr.transform == head;
                bool isOverlay = IsPreviewAccessoryOverlay(sr, head);
                string limbName = sr.transform.name;
                if (isHair)
                {
                    if (draftHair != null) spr = draftHair;
                    col = _hairTint;
                    limbName = "Hair";
                }
                else if (isHead && draftHead != null)
                {
                    spr = draftHead;
                    limbName = "Head";
                }
                else if (!isOverlay && _draft != null)
                {
                    string key = DbpuSpriteEditorBridge.MapLimbKey(limbName);
                    string path = CustomTransformationCatalog.GetLimbSpritePath(_draft, key);
                    if (!string.IsNullOrEmpty(path))
                    {
                        Sprite overrideSpr = CustomTransformationCatalog.LoadSpriteFlexible(path);
                        if (overrideSpr != null) spr = overrideSpr;
                    }
                    limbName = key;
                }

                if (spr == null) continue;
                var go = new GameObject(sr.transform.name);
                go.transform.SetParent(holder.transform, false);
                var rt = go.AddComponent<RectTransform>();
                Vector3 delta = sr.transform.position - origin;
                float w = spr.rect.width / Mathf.Max(1f, spr.pixelsPerUnit) * uiPerWorld;
                float h = spr.rect.height / Mathf.Max(1f, spr.pixelsPerUnit) * uiPerWorld;
                rt.anchoredPosition = new Vector2(delta.x * uiPerWorld, delta.y * uiPerWorld);
                rt.sizeDelta = new Vector2(w, h);
                rt.localRotation = Quaternion.Euler(0f, 0f, sr.transform.eulerAngles.z - head.eulerAngles.z);
                var img = go.AddComponent<Image>();
                img.sprite = spr;
                img.color = col;
                img.preserveAspect = true;
                layerRts.Add(rt);
                if (isHair)
                {
                    img.raycastTarget = true;
                    hairRt = rt;
                    go.AddComponent<TmHairPreviewDrag>();
                }
                else if (isOverlay)
                {
                    img.raycastTarget = false;
                }
                else if (limbPick)
                {
                    img.raycastTarget = true;
                    var click = go.AddComponent<TmLimbPreviewClick>();
                    click.LimbName = limbName;
                }
                else
                {
                    img.raycastTarget = false;
                }
            }

            if (hairRt == null && draftHair != null)
            {
                var go = new GameObject("Hair");
                go.transform.SetParent(holder.transform, false);
                var rt = go.AddComponent<RectTransform>();
                float w = draftHair.rect.width / Mathf.Max(1f, draftHair.pixelsPerUnit) * uiPerWorld;
                float h = draftHair.rect.height / Mathf.Max(1f, draftHair.pixelsPerUnit) * uiPerWorld;
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(w, h);
                var img = go.AddComponent<Image>();
                img.sprite = draftHair;
                img.color = _hairTint;
                img.preserveAspect = true;
                img.raycastTarget = true;
                go.AddComponent<TmHairPreviewDrag>();
                layerRts.Add(rt);
                hairRt = rt;
            }

            _previewFitScale = CenterPreviewLayers(layerRts, size);
            if (hairRt != null)
            {
                hairRt.SetAsLastSibling();
                _previewHairRt = hairRt;
                _previewHairBasePos = hairRt.anchoredPosition - new Vector2(_draft.HairLocalX, _draft.HairLocalY) * (uiPerWorld * _previewFitScale);
            }
            else
            {
                _previewHairRt = null;
            }

            string hintText = limbPick
                ? "Click limb to attach  |  Drag hair"
                : "Drag hair to reposition";
            var hint = CreateLabel(holder.transform, hintText, new Vector2(0f, -size.y * 0.5f + 4f), 1.9f);
            hint.color = new Color(0.75f, 0.75f, 0.75f, 1f);
            if (_draft != null && !string.IsNullOrEmpty(_draft.AuraID))
            {
                Color ac = ResolveAuraPreviewPrimary();
                var auraTag = CreateLabel(holder.transform, (_draft.AuraID ?? "Aura") + (_draft.UseCustomAura ? " *" : ""), new Vector2(0f, size.y * 0.5f - 5f), 1.9f);
                auraTag.color = new Color(ac.r, ac.g, ac.b, 0.95f);
            }

            if (limbPick && !string.IsNullOrEmpty(_previewSelectedLimb))
                RefreshPreviewAccessoryButton();
            _statusTmp = null;
        }

        static bool IsPreviewAccessoryOverlay(SpriteRenderer sr, Transform head)
        {
            if (sr == null || sr.transform == null) return false;
            if (sr.transform == head) return false;
            if (string.Equals(sr.transform.name, "Hair", StringComparison.OrdinalIgnoreCase)) return false;
            if (sr.GetComponent<LimbBehaviour>() != null) return false;
            string n = sr.gameObject.name ?? "";
            if (n.StartsWith("TmAccessory_", StringComparison.OrdinalIgnoreCase)) return true;
            if (n.IndexOf("Accessory", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (n.IndexOf("Scouter", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (n.IndexOf("Cape", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (n.IndexOf("Extra", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (n.IndexOf("Ear", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        static Color ResolveAuraPreviewPrimary()
        {
            if (_draft != null && _draft.UseCustomAura)
                return new Color(_draft.AuraColorR, _draft.AuraColorG, _draft.AuraColorB, 1f);
            return GuessAuraPresetColor(_draft?.AuraID, false);
        }

        static Color ResolveAuraPreviewSecondary()
        {
            if (_draft != null && _draft.UseCustomAura)
            {
                if (_draft.UseAuraSecondaryColor)
                    return new Color(_draft.AuraColor2R, _draft.AuraColor2G, _draft.AuraColor2B, 1f);
                Color p = ResolveAuraPreviewPrimary();
                return Color.Lerp(p, Color.white, 0.35f);
            }
            return GuessAuraPresetColor(_draft?.AuraID, true);
        }

        static Color GuessAuraPresetColor(string auraId, bool secondary)
        {
            string id = string.IsNullOrEmpty(auraId) ? "Base" : auraId;
            Color baseCol;
            switch (id)
            {
                case "SSJ1":
                case "SSJ2":
                case "SSJ3":
                case "OrangePiccolo":
                case "GoldenFrieza":
                    baseCol = new Color(1f, 0.82f, 0.2f);
                    break;
                case "SSJGod":
                case "BeastGohan":
                    baseCol = new Color(0.95f, 0.25f, 0.35f);
                    break;
                case "SSJBlue":
                case "PSSJBlue":
                    baseCol = new Color(0.25f, 0.75f, 1f);
                    break;
                case "SSJBlueEvolution":
                case "UE":
                    baseCol = new Color(0.15f, 0.35f, 0.95f);
                    break;
                case "Frieza":
                    baseCol = new Color(0.75f, 0.2f, 0.9f);
                    break;
                case "Broly":
                case "PowerAwakened":
                    baseCol = new Color(0.35f, 1f, 0.3f);
                    break;
                case "Mystic":
                    baseCol = new Color(0.85f, 0.75f, 1f);
                    break;
                case "UI":
                case "MUI":
                case "Base":
                default:
                    baseCol = new Color(0.92f, 0.95f, 1f);
                    break;
            }
            return secondary ? Color.Lerp(baseCol, Color.white, 0.4f) : baseCol;
        }

        static void DestroyAuraRtPreview()
        {
            if (_auraRtDriver != null)
            {
                UnityEngine.Object.Destroy(_auraRtDriver.gameObject);
                _auraRtDriver = null;
            }
            if (_auraRtWorld != null)
            {
                UnityEngine.Object.Destroy(_auraRtWorld);
                _auraRtWorld = null;
            }
            _auraRtCam = null;
            _auraRtInstance = null;
            _auraRtImage = null;
            if (_auraRt != null)
            {
                _auraRt.Release();
                UnityEngine.Object.Destroy(_auraRt);
                _auraRt = null;
            }
        }

        static void BuildAuraRtPreview(Transform holder, Vector2 boxSize)
        {
            DestroyAuraRtPreview();
            if (_draft == null || string.IsNullOrEmpty(_draft.AuraID)) return;
            if (!AurasStorage.AurasVFX.TryGetValue(_draft.AuraID, out GameObject prefab) || prefab == null)
                return;

            int rw = 384;
            int rh = 480;
            _auraRt = new RenderTexture(rw, rh, 16, RenderTextureFormat.ARGB32);
            _auraRt.antiAliasing = 1;
            _auraRt.Create();

            _auraRtWorld = new GameObject("TmAuraRtWorld");
            UnityEngine.Object.DontDestroyOnLoad(_auraRtWorld);
            _auraRtWorld.transform.position = new Vector3(4800f, 4800f, 0f);

            var camGo = new GameObject("AuraCam");
            camGo.transform.SetParent(_auraRtWorld.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 0.2f, -8f);
            _auraRtCam = camGo.AddComponent<Camera>();
            _auraRtCam.orthographic = true;
            float auraScale = _draft.AuraScale <= 0.01f ? 1f : Mathf.Clamp(_draft.AuraScale, 0.35f, 3f);
            _auraRtCam.orthographicSize = 2.35f * auraScale;
            _auraRtCam.nearClipPlane = 0.1f;
            _auraRtCam.farClipPlane = 40f;
            _auraRtCam.clearFlags = CameraClearFlags.SolidColor;
            _auraRtCam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _auraRtCam.allowHDR = false;
            _auraRtCam.allowMSAA = false;
            _auraRtCam.cullingMask = ~0;
            _auraRtCam.targetTexture = _auraRt;
            _auraRtCam.enabled = false;

            _auraRtInstance = UnityEngine.Object.Instantiate(prefab, _auraRtWorld.transform);
            _auraRtInstance.name = "AuraPreviewInstance";
            _auraRtInstance.transform.localPosition = new Vector3(0f, -0.85f, 0f);
            _auraRtInstance.transform.localRotation = Quaternion.identity;
            _auraRtInstance.transform.localScale = new Vector3(1.25f, 1.25f, 1f) * auraScale;
            DbpuMakerPhysics.StripWorldPhysics(_auraRtInstance);
            TintAuraPreviewInstance(_auraRtInstance);

            var rawGo = new GameObject("AuraRtView");
            rawGo.transform.SetParent(holder, false);
            rawGo.transform.SetAsFirstSibling();
            var rawRt = rawGo.AddComponent<RectTransform>();
            rawRt.anchoredPosition = Vector2.zero;
            rawRt.sizeDelta = boxSize;
            _auraRtImage = rawGo.AddComponent<RawImage>();
            _auraRtImage.texture = _auraRt;
            _auraRtImage.color = Color.white;
            _auraRtImage.raycastTarget = false;

            var driverGo = new GameObject("TmAuraRtDriver");
            UnityEngine.Object.DontDestroyOnLoad(driverGo);
            _auraRtDriver = driverGo.AddComponent<TmAuraRtDriver>();
            _auraRtDriver.Cam = _auraRtCam;
            if (_auraRtCam != null)
                _auraRtCam.Render();
        }

        static void TintAuraPreviewInstance(GameObject instance)
        {
            if (instance == null || _draft == null || !_draft.UseCustomAura) return;
            Color primary = new Color(_draft.AuraColorR, _draft.AuraColorG, _draft.AuraColorB, 1f);
            Color secondary = _draft.UseAuraSecondaryColor
                ? new Color(_draft.AuraColor2R, _draft.AuraColor2G, _draft.AuraColor2B, 1f)
                : Color.Lerp(primary, Color.white, 0.35f);
            foreach (var ps in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (ps == null) continue;
                var main = ps.main;
                main.startColor = new ParticleSystem.MinMaxGradient(primary, secondary);
            }
            foreach (var sr in instance.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr == null) continue;
                float a = sr.color.a > 0.01f ? sr.color.a : 1f;
                sr.color = new Color(primary.r, primary.g, primary.b, a);
            }
        }

        static Sprite ResolveSelectedSprite(string path, List<(string Path, Sprite Sprite)> list)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (!path.StartsWith("form:", StringComparison.OrdinalIgnoreCase))
            {
                var spr = CustomTransformationCatalog.LoadSpriteFlexible(path);
                if (spr != null) return spr;
            }
            for (int i = 0; i < list.Count; i++)
            {
                if (string.Equals(list[i].Path, path, StringComparison.OrdinalIgnoreCase))
                    return list[i].Sprite;
            }
            return null;
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

        static void BuildPropertyList(RectTransform left)
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

            Add("Name: " + (_draft.Name ?? ""), () => PromptString(_draft.Name, v => { _draft.Name = v; RebuildBody(); }));
            Add("Power: " + _draft.PowerMultiplier.ToString("0.##"), () => PromptFloat(_draft.PowerMultiplier, v => { _draft.PowerMultiplier = Mathf.Max(0.01f, v); PreviewLive(); }));
            Add("Size: " + _draft.Size.ToString("0.##"), () => PromptFloat(_draft.Size, v => { _draft.Size = Mathf.Clamp(v, 0.2f, 50f); PreviewLive(); }));
            Add("Ki Drain: " + _draft.KiDrainRate.ToString("0.##"), () => PromptFloat(_draft.KiDrainRate, v => { _draft.KiDrainRate = Mathf.Max(0f, v); PreviewLive(); }));
            string auraLabel = (_draft.AuraID ?? "Base") + (_draft.UseCustomAura ? " *" : "");
            Add("Aura: " + auraLabel, OpenAuraMaker);
            string kiLabel = _draft.UseKiBarColor
                ? "Ki Bar Color *"
                : "Ki Bar Color";
            Add(kiLabel, OpenKiBarColorPicker);
            string suffixLabel = string.IsNullOrEmpty(_draft.RequiredSkin_Suffix) ? "(default)" : _draft.RequiredSkin_Suffix;
            Add("Skin Suffix: " + suffixLabel, OpenSkinSuffixPicker);
            int accCount = _draft.Accessories != null ? _draft.Accessories.Count : 0;
            Add("Accessories: " + accCount, OpenAccessoriesEditor);
            Add("Sprite Editor", () =>
            {
                string limb = _selectedLimbKey;
                if (string.IsNullOrEmpty(limb))
                    limb = !string.IsNullOrEmpty(_previewSelectedLimb) ? _previewSelectedLimb : "Head";
                limb = DbpuSpriteEditorBridge.MapLimbKey(limb);
                if (!DbpuSpriteEditorBridge.TryOpenForLimbName(_targetPerson, limb))
                    DialogBoxManager.Notification("Could not open Sprite Editor.");
            });
            string iconLabel = string.IsNullOrEmpty(_draft.IconPath) ? "Icon: (empty)" : "Icon: set";
            Add(iconLabel, () =>
            {
                _captureReturnScreen = ScreenKind.Editor;
                OpenCapture(CaptureKind.Icon);
            });
            string illLabel = string.IsNullOrEmpty(_draft.IllustrationPath) ? "Illustration: (empty)" : "Illustration: set";
            Add(illLabel, () =>
            {
                _captureReturnScreen = ScreenKind.Editor;
                OpenCapture(CaptureKind.Illustration);
            });

            var offsetGo = new GameObject("HairOffsetLabel");
            offsetGo.transform.SetParent(content, false);
            var offsetRt = offsetGo.AddComponent<RectTransform>();
            AnchorTopCenter(offsetRt);
            offsetRt.anchoredPosition = new Vector2(0f, y);
            offsetRt.sizeDelta = new Vector2(44f, 10f);
            _hairOffsetLabel = offsetGo.AddComponent<TextMeshProUGUI>();
            _hairOffsetLabel.text = "Hair Offset  X " + _draft.HairLocalX.ToString("0.###") + "  Y " + _draft.HairLocalY.ToString("0.###");
            _hairOffsetLabel.font = Font;
            _hairOffsetLabel.fontSize = 1.9f;
            _hairOffsetLabel.alignment = TextAlignmentOptions.Center;
            _hairOffsetLabel.color = new Color(0.8f, 0.85f, 1f, 1f);
            _hairOffsetLabel.raycastTarget = false;
            y -= 11f;

            Add("Hair X: " + _draft.HairLocalX.ToString("0.###"), () => PromptFloat(_draft.HairLocalX, v =>
            {
                _draft.HairLocalX = v;
                _draft.UseHairLocalPosition = true;
                PreviewLive();
            }));
            Add("Hair Y: " + _draft.HairLocalY.ToString("0.###"), () => PromptFloat(_draft.HairLocalY, v =>
            {
                _draft.HairLocalY = v;
                _draft.UseHairLocalPosition = true;
                PreviewLive();
            }));
            Add("Reset Hair Offset", () =>
            {
                _draft.HairLocalX = 0f;
                _draft.HairLocalY = 0f;
                _draft.HairLocalZ = 0f;
                _draft.UseHairLocalPosition = false;
                PreviewLive();
            });
            Add("Hair Recolour", OpenHairColorPicker);
            Add("Description", () => PromptString(_draft.Description ?? "", v => { _draft.Description = v; RebuildBody(); }));

            content.GetComponent<RectTransform>().sizeDelta = new Vector2(left.sizeDelta.x - 2f, Mathf.Abs(y) + 4f);
        }

        static void BuildSpritePanel(RectTransform right)
        {
            _spritePanelRt = right;
            CreateLabel(right, "Sprites", new Vector2(0f, right.sizeDelta.y * 0.5f - 3f), 2.5f);

            if (string.IsNullOrEmpty(_selectedLimbKey) || Array.IndexOf(SpriteCategories, _selectedLimbKey) < 0)
                _selectedLimbKey = "Head";
            _spriteTab = Array.IndexOf(SpriteCategories, _selectedLimbKey);
            if (_spriteTab < 0) _spriteTab = 1;

            float tabY = right.sizeDelta.y * 0.5f - 9f;
            for (int i = 0; i < SpriteCategories.Length; i++)
            {
                int idx = i;
                string cat = SpriteCategories[i];
                string label = cat.Length > 4 ? cat.Substring(0, 4) : cat;
                int row = i / 5;
                int col = i % 5;
                var btn = CreateSmallButtonColored(
                    right.transform,
                    label,
                    new Vector2(-11f + col * 5.5f, tabY - row * 5.5f),
                    () =>
                    {
                        _selectedLimbKey = SpriteCategories[idx];
                        _previewSelectedLimb = _selectedLimbKey;
                        _spriteTab = idx;
                        RebuildBody();
                    },
                    string.Equals(cat, _selectedLimbKey, StringComparison.OrdinalIgnoreCase)
                        ? Accent
                        : new Color(0.18f, 0.18f, 0.18f, 1f),
                    5.2f);
            }

            CreateSmallButton(right.transform, "Import PNG", new Vector2(0f, right.sizeDelta.y * 0.5f - 22f), ImportSpritePng);

            CreateSearchField(right.transform, new Vector2(0f, right.sizeDelta.y * 0.5f - 28f), new Vector2(44f, 5.5f), _spriteSearch ?? "", "Search sprites...", v =>
            {
                _spriteSearch = v;
                RefillSpriteGrid();
            });

            float scrollH = right.sizeDelta.y - 32f;
            CreateScroll(right, new Vector2(0f, -30f), new Vector2(46f, scrollH), out Transform content);
            _spriteGridContent = content;
            RefillSpriteGrid();
        }

        static void RefillSpriteGrid()
        {
            if (_spriteGridContent == null) return;
            for (int i = _spriteGridContent.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(_spriteGridContent.GetChild(i).gameObject);

            if (_draft != null) CustomTransformationCatalog.EnsureLimbSpritePaths(_draft);
            string limbKey = string.IsNullOrEmpty(_selectedLimbKey) ? "Head" : _selectedLimbKey;
            bool hairTab = string.Equals(limbKey, "Hair", StringComparison.OrdinalIgnoreCase);
            bool headTab = string.Equals(limbKey, "Head", StringComparison.OrdinalIgnoreCase);

            var list = RefreshLimbSpriteCandidates(limbKey);

            string selectedPath = CustomTransformationCatalog.GetLimbSpritePath(_draft, limbKey);
            if (hairTab) selectedPath = _selectedHairPath ?? selectedPath;
            if (headTab) selectedPath = _selectedHeadPath ?? selectedPath;

            string q = (_spriteSearch ?? "").Trim().ToLowerInvariant();
            var filtered = new List<(string Path, Sprite Sprite)>();
            for (int i = 0; i < list.Count; i++)
            {
                var item = list[i];
                string file = System.IO.Path.GetFileNameWithoutExtension(item.Path ?? "") ?? "";
                if (string.IsNullOrEmpty(q)
                    || (item.Path != null && item.Path.ToLowerInvariant().Contains(q))
                    || file.ToLowerInvariant().Contains(q))
                    filtered.Add(item);
            }

            int cols = 3;
            float cell = 13f;
            float gap = 1.3f;
            int count = filtered.Count;
            int rows = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(1, count) / (float)cols));
            _spriteGridContent.GetComponent<RectTransform>().sizeDelta = new Vector2(46f, rows * (cell + gap) + 4f);

            if (count == 0)
            {
                var empty = CreateLabel(_spriteGridContent, "No " + limbKey + " sprites found", new Vector2(0f, -8f), 2.2f);
                AnchorTopCenter(empty.rectTransform);
                empty.rectTransform.anchoredPosition = new Vector2(0f, -8f);
                return;
            }

            float gridW = cols * cell + (cols - 1) * gap;
            float startX = -gridW * 0.5f + cell * 0.5f;
            float startY = -1.5f - cell * 0.5f;

            for (int i = 0; i < count; i++)
            {
                int col = i % cols;
                int row = i / cols;
                var item = filtered[i];
                var cellGo = new GameObject(limbKey);
                cellGo.transform.SetParent(_spriteGridContent, false);
                var crt = cellGo.AddComponent<RectTransform>();
                AnchorTopCenter(crt);
                crt.anchoredPosition = new Vector2(startX + col * (cell + gap), startY - row * (cell + gap));
                crt.sizeDelta = new Vector2(cell, cell);
                bool selected = string.Equals(selectedPath, item.Path, StringComparison.OrdinalIgnoreCase);
                cellGo.AddComponent<Image>().color = selected ? Accent : new Color(0.15f, 0.15f, 0.15f, 1f);
                var btn = cellGo.AddComponent<Button>();
                string path = item.Path;
                Sprite spr = item.Sprite;
                string captureLimb = limbKey;
                bool isHair = hairTab;
                btn.onClick.AddListener(() =>
                {
                    CustomTransformationCatalog.SetLimbSpritePath(_draft, captureLimb, path);
                    if (isHair)
                    {
                        _selectedHairPath = path;
                        _draft.HairSpritePath = path;
                    }
                    else if (string.Equals(captureLimb, "Head", StringComparison.OrdinalIgnoreCase))
                    {
                        _selectedHeadPath = path;
                        _draft.HeadSpritePath = path;
                    }
                    PreviewLive();
                });
                if (spr != null)
                {
                    var icon = new GameObject("I");
                    icon.transform.SetParent(cellGo.transform, false);
                    var irt = icon.AddComponent<RectTransform>();
                    irt.anchorMin = Vector2.zero;
                    irt.anchorMax = Vector2.one;
                    irt.offsetMin = new Vector2(1f, 1f);
                    irt.offsetMax = new Vector2(-1f, -1f);
                    var img = icon.AddComponent<Image>();
                    img.sprite = spr;
                    img.color = isHair ? _hairTint : Color.white;
                    img.preserveAspect = true;
                    img.raycastTarget = false;
                }
            }
        }

        static List<(string Path, Sprite Sprite)> RefreshLimbSpriteCandidates(string limbKey)
        {
            var result = new List<(string Path, Sprite Sprite)>();
            if (string.IsNullOrEmpty(limbKey)) return result;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void Add(string path, Sprite spr)
            {
                if (spr == null || string.IsNullOrEmpty(path) || !seen.Add(path)) return;
                result.Add((path, spr));
            }

            if (string.Equals(limbKey, "Hair", StringComparison.OrdinalIgnoreCase))
            {
                if (_hairList != null)
                {
                    for (int i = 0; i < _hairList.Count; i++)
                        Add(_hairList[i].Path, _hairList[i].Sprite);
                }
                return result;
            }

            if (string.Equals(limbKey, "Head", StringComparison.OrdinalIgnoreCase))
            {
                if (_headList != null)
                {
                    for (int i = 0; i < _headList.Count; i++)
                        Add(_headList[i].Path, _headList[i].Sprite);
                }
                return result;
            }

            string current = CustomTransformationCatalog.GetLimbSpritePath(_draft, limbKey);
            if (!string.IsNullOrEmpty(current))
            {
                Sprite curSpr = CustomTransformationCatalog.LoadSpriteFlexible(current);
                if (curSpr != null) Add(current, curSpr);
            }

            string fileName = limbKey + ".png";
            string charFs = null;
            try
            {
                if (_targetPerson != null)
                {
                    string basePath = SkinPackIO.ResolveCharacterBasePath(_targetPerson);
                    if (!string.IsNullOrEmpty(basePath))
                        charFs = CustomTransformationCatalog.ResolveModPath(basePath);
                }
            }
            catch { }
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
                    if (stopEarly && result.Count >= 120) return;
                }
            }

            ScanFolder(charFs, true);
            if (result.Count < 120)
                ScanFolder(charactersRoot, true);

            string importRoot = CustomTransformationCatalog.EnsureImportFolder();
            if (Directory.Exists(importRoot) && result.Count < 120)
            {
                string limbDir = Path.Combine(importRoot, "limbs", limbKey);
                if (Directory.Exists(limbDir))
                {
                    foreach (var file in Directory.GetFiles(limbDir, "*.png"))
                    {
                        var spr = CustomTransformationCatalog.LoadSpriteFromDisk(file);
                        if (spr == null) continue;
                        string rel = file.Replace('\\', '/');
                        int idx = rel.IndexOf("Assets/", StringComparison.OrdinalIgnoreCase);
                        if (idx >= 0) rel = rel.Substring(idx);
                        Add(rel, spr);
                        if (result.Count >= 120) break;
                    }
                }

                if (result.Count < 120)
                {
                    foreach (var file in Directory.GetFiles(importRoot, "*.png", SearchOption.AllDirectories))
                    {
                        string name = Path.GetFileName(file);
                        if (!string.Equals(name, fileName, StringComparison.OrdinalIgnoreCase)
                            && name.IndexOf(limbKey, StringComparison.OrdinalIgnoreCase) < 0)
                            continue;
                        var spr = CustomTransformationCatalog.LoadSpriteFromDisk(file);
                        if (spr == null) continue;
                        string rel = file.Replace('\\', '/');
                        int idx = rel.IndexOf("Assets/", StringComparison.OrdinalIgnoreCase);
                        if (idx >= 0) rel = rel.Substring(idx);
                        Add(rel, spr);
                        if (result.Count >= 120) break;
                    }
                }
            }

            return result;
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
            phTmp.fontSize = 2.2f;
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
            inputTmp.fontSize = 2.2f;
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

        static void ImportSpritePng()
        {
            if (TmFilePicker.IsOpen) return;
            try
            {
                string limbKey = string.IsNullOrEmpty(_selectedLimbKey) ? "Head" : _selectedLimbKey;
                string src = null;
                RunWithUiYield(() =>
                {
                    src = TmFilePicker.OpenFile(
                        "Import " + limbKey + " PNG",
                        "PNG Images (*.png)|*.png|All files (*.*)|*.*");
                });
                if (string.IsNullOrEmpty(src)) return;

                string folder = CustomTransformationCatalog.EnsureImportFolder();
                string sub = string.Equals(limbKey, "Hair", StringComparison.OrdinalIgnoreCase) ? "hair"
                    : string.Equals(limbKey, "Head", StringComparison.OrdinalIgnoreCase) ? "head"
                    : Path.Combine("limbs", limbKey);
                string destDir = System.IO.Path.Combine(folder, sub);
                if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);
                string dest = System.IO.Path.Combine(destDir, System.IO.Path.GetFileName(src));
                File.Copy(src, dest, true);
                TransformationMakerHairLibrary.Invalidate();
                CustomTransformationCatalog.SetLimbSpritePath(_draft, limbKey, dest);
                if (string.Equals(limbKey, "Hair", StringComparison.OrdinalIgnoreCase))
                {
                    _selectedHairPath = dest;
                    _draft.HairSpritePath = dest;
                }
                else if (string.Equals(limbKey, "Head", StringComparison.OrdinalIgnoreCase))
                {
                    _selectedHeadPath = dest;
                    _draft.HeadSpritePath = dest;
                }
                PreviewLive();
            }
            catch (Exception ex)
            {
                DialogBoxManager.Notification("Import failed: " + ex.Message);
            }
        }

        static void ShareCurrentForm()
        {
            if (_draft == null) return;
            ShareFormDto(_draft);
        }

        static CustomTransformationFormDto ResolveFormDto(string formId)
        {
            if (string.IsNullOrEmpty(formId)) return null;
            if (_draft != null && string.Equals(_draft.ID, formId, StringComparison.OrdinalIgnoreCase))
            {
                _draft.HairSpritePath = _selectedHairPath ?? _draft.HairSpritePath;
                _draft.HeadSpritePath = _selectedHeadPath ?? _draft.HeadSpritePath;
                return _draft;
            }
            var form = CustomTransformationCatalog.CloneForm(CustomTransformationCatalog.GetForm(_characterId, formId));
            if (form == null && _targetCt != null
                && _targetCt.transformations?.TransformationData != null
                && _targetCt.transformations.TransformationData.TryGetValue(formId, out var stats))
                form = CustomTransformationCatalog.FromStats(stats);
            return form;
        }

        static void ShareFormDto(CustomTransformationFormDto form)
        {
            if (TmFilePicker.IsOpen) return;
            try
            {
                if (form == null) return;
                if (string.IsNullOrEmpty(form.ID))
                    form.ID = "Custom_" + DateTime.Now.Ticks.ToString("x");
                EnrichFormPathsFromLiveSprites(form);
                RunWithUiYield(() => TransformationShare.ExportForm(_characterId, _displayName, form));
                ModAPI.Notify("Transformation pack saved");
            }
            catch (Exception ex)
            {
                DialogBoxManager.Notification("Share failed: " + ex.Message);
            }
        }

        static int _makerUiHideDepth;
        static bool _makerUiWantVisible;

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

        static void EnrichFormPathsFromLiveSprites(CustomTransformationFormDto form)
        {
            if (form == null || _targetCt?.transformations?.TransformationData == null) return;
            if (!_targetCt.transformations.TransformationData.TryGetValue(form.ID, out var stats) || stats == null)
                return;

            string packDir = Path.Combine(CustomTransformationCatalog.EnsureImportFolder(), "ExportCache");
            try { if (!Directory.Exists(packDir)) Directory.CreateDirectory(packDir); } catch { return; }

            if (string.IsNullOrEmpty(form.HairSpritePath) && stats.HairSprite != null)
                form.HairSpritePath = WriteSpriteCache(stats.HairSprite, packDir, form.ID + "_hair");
            if (string.IsNullOrEmpty(form.HeadSpritePath) && stats.HeadSprite != null)
                form.HeadSpritePath = WriteSpriteCache(stats.HeadSprite, packDir, form.ID + "_head");
            if (string.IsNullOrEmpty(form.IconPath) && stats.Icon != null)
                form.IconPath = WriteSpriteCache(stats.Icon, packDir, form.ID + "_icon");
            if (string.IsNullOrEmpty(form.IllustrationPath) && stats.Illustration != null)
                form.IllustrationPath = WriteSpriteCache(stats.Illustration, packDir, form.ID + "_illustration");
        }

        static string WriteSpriteCache(Sprite sprite, string folder, string stem)
        {
            if (sprite == null || sprite.texture == null) return null;
            try
            {
                var tex = sprite.texture;
                Texture2D readable;
                if (tex.isReadable)
                {
                    var rect = sprite.textureRect;
                    readable = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGBA32, false);
                    readable.SetPixels(tex.GetPixels((int)rect.x, (int)rect.y, (int)rect.width, (int)rect.height));
                    readable.Apply(false, false);
                }
                else
                {
                    var rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32);
                    var prev = RenderTexture.active;
                    Graphics.Blit(tex, rt);
                    RenderTexture.active = rt;
                    readable = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
                    readable.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
                    readable.Apply(false, false);
                    RenderTexture.active = prev;
                    RenderTexture.ReleaseTemporary(rt);
                }
                string path = Path.Combine(folder, stem + ".png");
                File.WriteAllBytes(path, readable.EncodeToPNG());
                UnityEngine.Object.Destroy(readable);
                return path;
            }
            catch
            {
                return null;
            }
        }

        static void ConfirmShareForm(string formId)
        {
            var form = ResolveFormDto(formId);
            if (form == null)
            {
                DialogBoxManager.Notification("Could not find that form to share.");
                return;
            }
            _workshopPublishForm = CustomTransformationCatalog.CloneForm(form) ?? form;
            _screen = ScreenKind.ShareChoice;
            RebuildBody();
        }

        static void BuildShareChoiceScreen()
        {
            SetWorldClickMode(false);
            float bodyH = PanelH - TitleH - 3f;
            CreateNavBar(_body, "Back", () =>
            {
                _screen = ScreenKind.Forms;
                RebuildBody();
            }, null, null);

            CreateScroll(_body, new Vector2(0f, -NavH), new Vector2(PanelW - 6f, bodyH - NavH - 2f), out Transform content);
            float y = -8f;
            string name = _workshopPublishForm?.Name ?? _workshopPublishForm?.ID ?? "Form";
            CreateLabel(content.GetComponent<RectTransform>(), "Share \"" + name + "\"", new Vector2(0f, y), 2.4f);
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

            AddChoice("File Pack (.dbputf)", () =>
            {
                try { ShareFormDto(_workshopPublishForm); }
                catch (Exception ex) { DialogBoxManager.Notification("Share failed: " + ex.Message); }
            });
            AddChoice("Steam Workshop", () => BeginWorkshopUploadMenu(_workshopPublishForm));
            content.GetComponent<RectTransform>().sizeDelta = new Vector2(PanelW - 8f, Mathf.Max(40f, Mathf.Abs(y) + 8f));
        }

        static void BeginWorkshopUploadMenu(CustomTransformationFormDto form)
        {
            if (form == null) return;
            if (!DbpuWorkshop.SteamReady)
            {
                DialogBoxManager.Notification("Steam is not available.\nStart PPG through Steam to upload.");
                return;
            }

            _workshopPublishForm = CustomTransformationCatalog.CloneForm(form) ?? form;
            _workshopPublishTitle = _workshopPublishForm.Name ?? "Custom Form";
            _workshopPublishDescription = _workshopPublishForm.Description ?? "";
            try
            {
                var local = DbpuWorkshop.FindLocal(DbpuWorkshop.MakeLocalKey(
                    DbpuWorkshopContentType.Transformation, _characterId, _workshopPublishForm.ID));
                if (local != null && !string.IsNullOrEmpty(local.Description))
                    _workshopPublishDescription = local.Description;
            }
            catch { }
            _workshopPublishPreviewPath = null;
            _workshopUploading = false;
            _workshopUploadStatus = null;
            _screen = ScreenKind.WorkshopPublish;
            RebuildBody();
        }

        static string ResolveWorkshopPreviewPath(CustomTransformationFormDto form)
        {
            if (!string.IsNullOrEmpty(_workshopPublishPreviewPath) && File.Exists(_workshopPublishPreviewPath))
                return _workshopPublishPreviewPath;

            if (form != null && !string.IsNullOrEmpty(form.IconPath))
            {
                string icon = form.IconPath;
                if (!File.Exists(icon))
                    icon = CustomTransformationCatalog.ResolveModPath(icon);
                if (!string.IsNullOrEmpty(icon) && File.Exists(icon))
                    return icon;
            }

            if (_targetCt != null && form != null && !string.IsNullOrEmpty(form.ID)
                && _targetCt.transformations.TransformationData.TryGetValue(form.ID, out var stats)
                && stats?.Icon != null)
            {
                try
                {
                    string folder = CustomTransformationCatalog.EnsureImportFolder();
                    string baked = WriteSpriteCache(stats.Icon, folder, "ws_preview_" + SanitizeFileToken(form.ID));
                    if (!string.IsNullOrEmpty(baked) && File.Exists(baked))
                        return baked;
                }
                catch { }
            }

            return null;
        }

        static void BuildWorkshopPublishScreen()
        {
            SetWorldClickMode(false);
            float bodyH = PanelH - TitleH - 3f;
            CreateNavBar(_body, "Back", () =>
            {
                if (_workshopUploading) return;
                _screen = ScreenKind.ShareChoice;
                RebuildBody();
            }, _workshopUploading ? null : "Upload", () =>
            {
                if (_workshopUploading) return;
                StartWorkshopUploadFromMenu();
            });

            CreateScroll(_body, new Vector2(0f, -NavH), new Vector2(PanelW - 6f, bodyH - NavH - 2f), out Transform content);
            float y = -4f;
            var form = _workshopPublishForm;

            void Row(string label, Action act = null)
            {
                var go = new GameObject("PubRow");
                go.transform.SetParent(content, false);
                var rt = go.AddComponent<RectTransform>();
                AnchorTopCenter(rt);
                rt.anchoredPosition = new Vector2(0f, y);
                rt.sizeDelta = new Vector2(PanelW - 16f, 10f);
                go.AddComponent<Image>().color = act != null
                    ? new Color(0.15f, 0.17f, 0.22f, 1f)
                    : new Color(0.12f, 0.12f, 0.14f, 1f);
                if (act != null && !_workshopUploading)
                    go.AddComponent<Button>().onClick.AddListener(() => act());
                var tGo = new GameObject("T");
                tGo.transform.SetParent(go.transform, false);
                var trt = tGo.AddComponent<RectTransform>();
                trt.anchorMin = Vector2.zero;
                trt.anchorMax = Vector2.one;
                trt.offsetMin = new Vector2(2f, 0f);
                trt.offsetMax = new Vector2(-2f, 0f);
                var tmp = tGo.AddComponent<TextMeshProUGUI>();
                tmp.text = label;
                tmp.font = Font;
                tmp.fontSize = 2.05f;
                tmp.alignment = TextAlignmentOptions.MidlineLeft;
                tmp.color = Color.white;
                tmp.enableWordWrapping = false;
                tmp.overflowMode = TextOverflowModes.Ellipsis;
                tmp.raycastTarget = false;
                y -= 11f;
            }

            CreateLabel(content.GetComponent<RectTransform>(),
                (form?.Name ?? "Form") + "  ->  " + (_displayName ?? _characterId ?? "Character"),
                new Vector2(0f, y), 2.1f);
            y -= 10f;

            string titleShow = string.IsNullOrEmpty(_workshopPublishTitle) ? "(untitled)" : _workshopPublishTitle;
            Row("Title: " + titleShow, () => PromptString(_workshopPublishTitle, v =>
            {
                _workshopPublishTitle = v;
                RebuildBody();
            }));

            string descShow = string.IsNullOrEmpty(_workshopPublishDescription) ? "(no description)" : _workshopPublishDescription;
            if (descShow.Length > 42) descShow = descShow.Substring(0, 42) + "...";
            Row("Description: " + descShow, () => PromptString(_workshopPublishDescription, v =>
            {
                _workshopPublishDescription = v;
                RebuildBody();
            }));

            string previewPath = ResolveWorkshopPreviewPath(form);
            bool hasPreview = !string.IsNullOrEmpty(previewPath) && File.Exists(previewPath);
            string previewLabel = hasPreview
                ? ("Preview: " + Path.GetFileName(previewPath))
                : "Preview: (none - capture an icon)";
            Row(previewLabel);

            if (hasPreview)
            {
                try
                {
                    var spr = CustomTransformationCatalog.LoadSpriteFromDisk(previewPath);
                    if (spr != null)
                    {
                        var imgGo = new GameObject("PreviewImg");
                        imgGo.transform.SetParent(content, false);
                        var irt = imgGo.AddComponent<RectTransform>();
                        AnchorTopCenter(irt);
                        irt.anchoredPosition = new Vector2(0f, y - 10f);
                        irt.sizeDelta = new Vector2(22f, 22f);
                        var img = imgGo.AddComponent<Image>();
                        img.sprite = spr;
                        img.preserveAspect = true;
                        img.raycastTarget = false;
                        y -= 24f;
                    }
                }
                catch { }
            }

            Row(hasPreview ? "Recapture Icon Preview" : "Capture Icon Preview", () =>
            {
                if (_workshopUploading) return;
                if (form == null) return;
                _draft = form;
                _captureReturnScreen = ScreenKind.WorkshopPublish;
                OpenCapture(CaptureKind.Icon);
            });
            Row("Browse PNG Preview", () =>
            {
                if (_workshopUploading || TmFilePicker.IsOpen) return;
                string picked = null;
                try
                {
                    RunWithUiYield(() =>
                    {
                        picked = TmFilePicker.OpenFile(
                            "Workshop Preview",
                            "PNG Images (*.png)|*.png|All files (*.*)|*.*");
                    });
                }
                catch (Exception ex)
                {
                    DialogBoxManager.Notification("Preview pick failed: " + ex.Message);
                    return;
                }
                if (!string.IsNullOrEmpty(picked) && File.Exists(picked))
                {
                    _workshopPublishPreviewPath = picked;
                    RebuildBody();
                }
            });

            if (!string.IsNullOrEmpty(_workshopUploadStatus))
            {
                CreateLabel(content.GetComponent<RectTransform>(), _workshopUploadStatus, new Vector2(0f, y - 4f), 2.0f);
                y -= 10f;
            }

            content.GetComponent<RectTransform>().sizeDelta = new Vector2(PanelW - 8f, Mathf.Max(40f, Mathf.Abs(y) + 10f));
        }

        static void StartWorkshopUploadFromMenu()
        {
            var form = _workshopPublishForm;
            if (form == null) return;
            string title = string.IsNullOrEmpty(_workshopPublishTitle) ? (form.Name ?? "Custom Form") : _workshopPublishTitle.Trim();
            string description = _workshopPublishDescription ?? "";
            string preview = ResolveWorkshopPreviewPath(form);

            if (string.IsNullOrEmpty(preview) || !File.Exists(preview))
            {
                DialogBoxManager.Notification("Set a preview first.\nCapture an icon or browse a PNG.");
                return;
            }

            try
            {
                var info = new FileInfo(preview);
                if (info.Length > 1000000L)
                {
                    DialogBoxManager.Notification("Preview image must be under 1 MB.");
                    return;
                }
            }
            catch { }

            if (!string.IsNullOrEmpty(preview) && string.IsNullOrEmpty(form.IconPath))
                form.IconPath = preview;

            _workshopUploading = true;
            _workshopUploadStatus = "Uploading \"" + title + "\"...";
            RebuildBody();

            DbpuWorkshop.BeginPublishTransformation(
                _characterId,
                _displayName,
                form,
                title,
                description,
                preview,
                (ok, msg) =>
                {
                    _workshopUploading = false;
                    _workshopUploadStatus = msg;
                    if (ok)
                    {
                        ModAPI.Notify(msg);
                        _screen = ScreenKind.Forms;
                    }
                    if (IsOpen) RebuildBody();
                    if (!ok) DialogBoxManager.Notification(msg ?? "Upload failed.");
                });
        }

        static void ImportSharedForm()
        {
            if (TmFilePicker.IsOpen) return;
            HideMakerUi();
            DialogBoxManager.Dialog(
                "Import a transformation pack (.dbputf / .zip) or browse Workshop",
                new DialogButton("Browse", true, new UnityAction[]
                {
                    () =>
                    {
                        try
                        {
                            if (TmFilePicker.IsOpen) return;
                            CompleteImportFromBrowse();
                        }
                        catch (Exception ex) { DialogBoxManager.Notification("Import failed: " + ex.Message); }
                        finally { ShowMakerUi(); }
                    }
                }),
                new DialogButton("Enter Path", true, new UnityAction[]
                {
                    () => PromptImportPath()
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

        static void BuildWorkshopBrowser()
        {
            SetWorldClickMode(false);
            float bodyH = PanelH - TitleH - 3f;
            CreateNavBar(_body, "Back", () =>
            {
                _screen = ScreenKind.Forms;
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
                CreateLabel(_body.GetComponent<RectTransform>(),
                    "Querying Steam Workshop...",
                    new Vector2(0f, -28f), 2.2f);
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
                CreateLabel(_body.GetComponent<RectTransform>(),
                    "Querying Steam Workshop...",
                    new Vector2(0f, -28f), 2.2f);

                DbpuWorkshop.BeginQueryRemote(
                    DbpuWorkshopContentType.Transformation,
                    _workshopPage,
                    null,
                    (ok, msg, items, total) =>
                    {
                        _workshopLoading = false;
                        _workshopRemote = items ?? new List<DbpuWorkshopRemoteItem>();
                        _workshopTotal = total;
                        _workshopStatus = msg;
                        if (_screen == ScreenKind.Workshop && IsOpen)
                            RebuildBody();
                    });
                return;
            }

            CreateScroll(_body, new Vector2(0f, -NavH), new Vector2(PanelW - 6f, bodyH - NavH - 2f), out Transform content);
            float y = -2f;

            string header = "Steam Workshop  |  page " + _workshopPage;
            if (_workshopTotal > 0) header += "  (" + _workshopTotal + ")";
            CreateLabel(content.GetComponent<RectTransform>(), header, new Vector2(0f, y), 2.0f);
            y -= 6f;

            if (_workshopRemote.Count == 0)
            {
                string empty = !string.IsNullOrEmpty(_workshopStatus)
                    ? _workshopStatus
                    : "No Singularity transformation items found on Workshop yet.\nUpload one, then Refresh.";
                CreateLabel(content.GetComponent<RectTransform>(), empty, new Vector2(0f, y - 10f), 2.1f);
                content.GetComponent<RectTransform>().sizeDelta = new Vector2(PanelW - 8f, 50f);
                return;
            }

            for (int i = 0; i < _workshopRemote.Count; i++)
            {
                var item = _workshopRemote[i];
                string title = item.Title ?? ("Item " + item.FileId);
                string label = title;
                if (!string.IsNullOrEmpty(item.Author)) label += "\nby " + item.Author;
                if (item.IsInstalled) label += "  [installed]";
                else if (item.IsSubscribed) label += "  [subscribed]";
                if (item.VotesUp > 0 || item.NumSubscriptions > 0)
                    label += "\n+" + item.VotesUp + "  ·  " + item.NumSubscriptions + " subs";

                var go = new GameObject("Ws_" + item.FileId);
                go.transform.SetParent(content, false);
                var rt = go.AddComponent<RectTransform>();
                AnchorTopCenter(rt);
                rt.anchoredPosition = new Vector2(0f, y);
                rt.sizeDelta = new Vector2(PanelW - 14f, 14f);
                go.AddComponent<Image>().color = new Color(0.14f, 0.14f, 0.16f, 1f);
                var capture = item;
                go.AddComponent<Button>().onClick.AddListener(() =>
                {
                    _workshopSelected = capture;
                    _screen = ScreenKind.WorkshopDetail;
                    RebuildBody();
                });

                var textGo = new GameObject("T");
                textGo.transform.SetParent(go.transform, false);
                var trt = textGo.AddComponent<RectTransform>();
                trt.anchorMin = Vector2.zero;
                trt.anchorMax = Vector2.one;
                trt.offsetMin = new Vector2(2f, 0.6f);
                trt.offsetMax = new Vector2(-2f, -0.6f);
                var tmp = textGo.AddComponent<TextMeshProUGUI>();
                tmp.text = label;
                tmp.font = Font;
                tmp.fontSize = 2.05f;
                tmp.alignment = TextAlignmentOptions.MidlineLeft;
                tmp.color = Color.white;
                tmp.enableWordWrapping = true;
                tmp.raycastTarget = false;

                y -= 16f;
            }

            float pageY = y - 2f;
            if (_workshopPage > 1)
            {
                CreateTinyFormAction(content, "Prev", new Vector2(-20f, pageY), 14f, () =>
                {
                    _workshopPage--;
                    _workshopRemote = null;
                    RebuildBody();
                });
            }
            if (_workshopRemote.Count > 0 && (_workshopTotal <= 0 || _workshopPage * 50 < _workshopTotal))
            {
                CreateTinyFormAction(content, "Next", new Vector2(20f, pageY), 14f, () =>
                {
                    _workshopPage++;
                    _workshopRemote = null;
                    RebuildBody();
                });
            }

            content.GetComponent<RectTransform>().sizeDelta = new Vector2(PanelW - 8f, Mathf.Max(40f, Mathf.Abs(pageY) + 16f));
        }

        static void BuildWorkshopDetailScreen()
        {
            SetWorldClickMode(false);
            float bodyH = PanelH - TitleH - 3f;
            var item = _workshopSelected;
            CreateNavBar(_body, "Back", () =>
            {
                _workshopSelected = null;
                _screen = ScreenKind.Workshop;
                RebuildBody();
            }, item != null && !item.IsInstalled ? "Install" : null, () =>
            {
                if (item != null) InstallWorkshopRemoteItem(item);
            });

            if (item == null)
            {
                CreateLabel(_body.GetComponent<RectTransform>(), "Item not found.", new Vector2(0f, -28f), 2.2f);
                return;
            }

            CreateScroll(_body, new Vector2(0f, -NavH), new Vector2(PanelW - 6f, bodyH - NavH - 2f), out Transform content);
            float y = -4f;

            CreateLabel(content.GetComponent<RectTransform>(), item.Title ?? ("Item " + item.FileId), new Vector2(0f, y), 2.5f);
            y -= 10f;

            string meta = "";
            if (!string.IsNullOrEmpty(item.Author)) meta += "by " + item.Author + "\n";
            if (item.IsInstalled) meta += "Status: installed\n";
            else if (item.IsSubscribed) meta += "Status: subscribed\n";
            else meta += "Status: online\n";
            meta += "+" + item.VotesUp + " votes  ·  " + item.NumSubscriptions + " subscriptions";
            CreateLabel(content.GetComponent<RectTransform>(), meta, new Vector2(0f, y), 2.0f);
            y -= 18f;

            string desc = string.IsNullOrEmpty(item.Description) ? "(No description)" : item.Description;
            var descGo = new GameObject("Desc");
            descGo.transform.SetParent(content, false);
            var drt = descGo.AddComponent<RectTransform>();
            AnchorTopCenter(drt);
            drt.anchoredPosition = new Vector2(0f, y);
            float descH = Mathf.Clamp(8f + desc.Length * 0.08f, 16f, 80f);
            drt.sizeDelta = new Vector2(PanelW - 16f, descH);
            var dtmp = descGo.AddComponent<TextMeshProUGUI>();
            dtmp.text = desc;
            dtmp.font = Font;
            dtmp.fontSize = 2.0f;
            dtmp.alignment = TextAlignmentOptions.TopLeft;
            dtmp.color = new Color(0.88f, 0.88f, 0.9f, 1f);
            dtmp.enableWordWrapping = true;
            dtmp.raycastTarget = false;
            y -= descH + 4f;

            void ActionRow(string label, Action act)
            {
                var go = new GameObject("Act_" + label);
                go.transform.SetParent(content, false);
                var rt = go.AddComponent<RectTransform>();
                AnchorTopCenter(rt);
                rt.anchoredPosition = new Vector2(0f, y);
                rt.sizeDelta = new Vector2(PanelW - 16f, 11f);
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
                tmp.fontSize = 2.2f;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.white;
                tmp.raycastTarget = false;
                y -= 13f;
            }

            if (!item.IsInstalled)
                ActionRow("Install to this character", () => InstallWorkshopRemoteItem(item));
            else
                ActionRow("Reinstall / Update", () => InstallWorkshopRemoteItem(item));

            ActionRow("Open on Steam", () =>
            {
                string url = !string.IsNullOrEmpty(item.Url)
                    ? item.Url
                    : ("https://steamcommunity.com/sharedfiles/filedetails/?id=" + item.FileId);
                DbpuWorkshop.SafeOpenUrl(url);
            });

            content.GetComponent<RectTransform>().sizeDelta = new Vector2(PanelW - 8f, Mathf.Max(40f, Mathf.Abs(y) + 10f));
        }

        static void InstallWorkshopRemoteItem(DbpuWorkshopRemoteItem item)
        {
            if (item == null) return;
            DialogBoxManager.Notification("Subscribing / downloading \"" + (item.Title ?? "item") + "\"...");
            DbpuWorkshop.BeginSubscribeAndInstallTransformation(
                item,
                _characterId,
                _displayName,
                (ok, msg, formId) =>
                {
                    if (!ok)
                    {
                        DialogBoxManager.Notification(msg ?? "Install failed.");
                        return;
                    }
                    item.IsInstalled = true;
                    item.IsSubscribed = true;
                    if (_targetPerson != null)
                        CustomTransformationCatalog.ApplyToPerson(_targetPerson.transform.root.gameObject);
                    ModAPI.Notify(msg ?? "Installed");
                    if (!string.IsNullOrEmpty(formId))
                        SelectForm(formId);
                    else if (IsOpen)
                        RebuildBody();
                });
        }

        static void InstallWorkshopItem(DbpuWorkshopItemInfo item)
        {
            try
            {
                string formId;
                var form = DbpuWorkshop.InstallTransformation(item, _characterId, _displayName, out formId);
                if (_targetPerson != null)
                    CustomTransformationCatalog.ApplyToPerson(_targetPerson.transform.root.gameObject);
                ModAPI.Notify("Installed \"" + (form?.Name ?? formId) + "\"");
                if (!string.IsNullOrEmpty(formId))
                    SelectForm(formId);
                else
                    RebuildBody();
            }
            catch (Exception ex)
            {
                DialogBoxManager.Notification("Install failed: " + ex.Message);
            }
        }

        static void CompleteImportFromBrowse()
        {
            string importedId;
            var form = TransformationShare.ImportForm(_characterId, _displayName, out importedId);
            ApplyImportedForm(form, importedId);
        }

        static void PromptImportPath()
        {
            DialogBox box = null;
            box = DialogBoxManager.TextEntry(
                "Paste full path to .dbputf or .zip",
                "C:\\path\\to\\form.dbputf",
                new DialogButton("Import", true, new UnityAction[]
                {
                    () =>
                    {
                        try
                        {
                            string path = box != null ? (box.EnteredText ?? "") : "";
                            string importedId;
                            var form = TransformationShare.ImportFormFromPath(path, _characterId, _displayName, out importedId);
                            ApplyImportedForm(form, importedId);
                        }
                        catch (Exception ex)
                        {
                            DialogBoxManager.Notification("Import failed: " + ex.Message);
                        }
                        finally
                        {
                            ShowMakerUi();
                        }
                    }
                }),
                new DialogButton("Cancel", true, new UnityAction[]
                {
                    () => ShowMakerUi()
                }));
        }

        static void ApplyImportedForm(CustomTransformationFormDto form, string importedId)
        {
            if (form == null) return;
            if (_targetPerson != null)
                CustomTransformationCatalog.ApplyToPerson(_targetPerson.transform.root.gameObject);
            ModAPI.Notify("Imported \"" + (form.Name ?? importedId) + "\"");
            if (!string.IsNullOrEmpty(importedId))
                SelectForm(importedId);
            else
                RebuildBody();
        }

        static void GetEditorLayout(out float panelTop, out float panelH)
        {
            float bodyH = PanelH - TitleH - 3f;
            panelTop = -NavH - 0.5f;
            panelH = bodyH - NavH - 1.5f;
        }

        static void SyncAuraDraftPreview()
        {
            if (_draft == null) return;
            _draft.AuraColorR = _auraPrimary.r;
            _draft.AuraColorG = _auraPrimary.g;
            _draft.AuraColorB = _auraPrimary.b;
            _draft.AuraColor2R = _auraSecondary.r;
            _draft.AuraColor2G = _auraSecondary.g;
            _draft.AuraColor2B = _auraSecondary.b;
            ApplyDraftToTarget();
        }

        static void SyncAuraDraftPreviewAndUi()
        {
            SyncAuraDraftPreview();
            RebuildBody();
        }

        static void CaptureAuraBackup()
        {
            if (_draft == null) return;
            _auraBackupId = _draft.AuraID;
            _auraBackupUseCustom = _draft.UseCustomAura;
            _auraBackupUseSecondary = _draft.UseAuraSecondaryColor;
            _auraBackupScale = _draft.AuraScale;
            _auraBackupR = _draft.AuraColorR;
            _auraBackupG = _draft.AuraColorG;
            _auraBackupB = _draft.AuraColorB;
            _auraBackup2R = _draft.AuraColor2R;
            _auraBackup2G = _draft.AuraColor2G;
            _auraBackup2B = _draft.AuraColor2B;
            _auraBackupPrimary = _auraPrimary;
            _auraBackupSecondary = _auraSecondary;
        }

        static void RestoreAuraBackup()
        {
            if (_draft == null) return;
            _draft.AuraID = _auraBackupId;
            _draft.UseCustomAura = _auraBackupUseCustom;
            _draft.UseAuraSecondaryColor = _auraBackupUseSecondary;
            _draft.AuraScale = _auraBackupScale;
            _draft.AuraColorR = _auraBackupR;
            _draft.AuraColorG = _auraBackupG;
            _draft.AuraColorB = _auraBackupB;
            _draft.AuraColor2R = _auraBackup2R;
            _draft.AuraColor2G = _auraBackup2G;
            _draft.AuraColor2B = _auraBackup2B;
            _auraPrimary = _auraBackupPrimary;
            _auraSecondary = _auraBackupSecondary;
            ApplyDraftToTarget();
        }

        static void OpenCapture(CaptureKind kind)
        {
            if (!_standaloneCapture && _draft == null) return;
            bool standalone = _standaloneCapture;
            CancelCapture();
            _standaloneCapture = standalone;
            if (!_standaloneCapture)
            {
                CloseOverlay(false);
                if (_root != null) _root.SetActive(false);
                SetWorldClickMode(false);
                TransformationMakerState.Disarm();
            }

            _captureKind = kind;
            _captureBusy = false;

            bool icon = kind == CaptureKind.Icon;
            string rel = icon ? EmptyIconRel : EmptyIllustrationRel;
            string relAlt = icon ? EmptyIconRelAlt : EmptyIllustrationRelAlt;
            int frameW = icon ? IconFrameW : IllFrameW;
            int frameH = icon ? IconFrameH : IllFrameH;
            Sprite frameSpr = LoadEmptyFrameSprite(rel, relAlt, frameW, frameH);
            if (frameSpr == null)
            {
                if (!_standaloneCapture && _root != null) _root.SetActive(true);
                _standaloneCapture = false;
                DialogBoxManager.Notification("Could not load empty frame: " + rel);
                return;
            }

            var canvas = GameObject.Find("Canvas");
            if (canvas == null)
            {
                if (!_standaloneCapture && _root != null) _root.SetActive(true);
                _standaloneCapture = false;
                return;
            }

            _captureRoot = new GameObject("TmMediaCapture");
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
            float aspect = frameW / (float)frameH;
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
            frameImg.color = Color.white;

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
            title.text = icon ? "Capture Icon" : "Capture Illustration";
            title.font = Font;
            title.fontSize = 26f;
            title.alignment = TextAlignmentOptions.Center;
            title.color = Accent;
            title.raycastTarget = false;

            var tipGo = new GameObject("Tip");
            tipGo.transform.SetParent(_captureHud.transform, false);
            var tipRt = tipGo.AddComponent<RectTransform>();
            tipRt.anchorMin = tipRt.anchorMax = new Vector2(0.5f, 1f);
            tipRt.pivot = new Vector2(0.5f, 1f);
            tipRt.anchoredPosition = new Vector2(0f, -52f);
            tipRt.sizeDelta = new Vector2(520f, 40f);
            var tip = tipGo.AddComponent<TextMeshProUGUI>();
            tip.text = "Line up the character inside the border, then take a photo.";
            tip.font = Font;
            tip.fontSize = 16f;
            tip.alignment = TextAlignmentOptions.Center;
            tip.color = new Color(0.9f, 0.9f, 0.9f, 1f);
            tip.raycastTarget = false;

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

            MakeCapBtn("Take Photo", new Vector2(-95f, 28f), BeginCaptureShot);
            MakeCapBtn("Cancel", new Vector2(95f, 28f), CancelCapture);

            ModAPI.Notify(icon ? "Icon capture ready" : "Illustration capture ready");
        }

        public static void CancelCapture()
        {
            bool standalone = _standaloneCapture;
            _captureBusy = false;
            if (!standalone && _captureReturnScreen != ScreenKind.Editor)
                _screen = _captureReturnScreen;
            _captureReturnScreen = ScreenKind.Editor;
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
            _standaloneCapture = false;
            if (!standalone && _root != null)
            {
                _root.SetActive(true);
                RebuildBody();
            }
        }

        static void BeginCaptureShot()
        {
            if (_captureBusy || _captureRoot == null || _captureFrameRt == null) return;
            if (!TryGetCaptureScreenRect(out _captureSx, out _captureSy, out _captureSw, out _captureSh))
            {
                DialogBoxManager.Notification("Could not read capture frame.");
                return;
            }
            _captureBusy = true;
            if (_captureRoot != null)
            {
                for (int i = 0; i < _captureRoot.transform.childCount; i++)
                    _captureRoot.transform.GetChild(i).gameObject.SetActive(false);
            }

            if (_captureDriver == null)
            {
                var go = new GameObject("TmCaptureDriver");
                UnityEngine.Object.DontDestroyOnLoad(go);
                _captureDriver = go.AddComponent<TmCaptureDriver>();
            }
            _captureDriver.OnShot = FinishCaptureShot;
            _captureDriver.BeginShot();
        }

        static void FinishCaptureShot()
        {
            try
            {
                bool icon = _captureKind == CaptureKind.Icon;
                int frameW = icon ? IconFrameW : IllFrameW;
                int frameH = icon ? IconFrameH : IllFrameH;
                int cx = icon ? IconContentX : IllContentX;
                int cyFromTop = icon ? IconContentY : IllContentY;
                int cw = icon ? IconContentW : IllContentW;
                int ch = icon ? IconContentH : IllContentH;
                int cy = frameH - cyFromTop - ch;

                int sx = _captureSx;
                int sy = _captureSy;
                int sw = _captureSw;
                int sh = _captureSh;
                sw = Mathf.Clamp(sw, 8, Screen.width);
                sh = Mathf.Clamp(sh, 8, Screen.height);
                sx = Mathf.Clamp(sx, 0, Screen.width - sw);
                sy = Mathf.Clamp(sy, 0, Screen.height - sh);

                var shot = new Texture2D(sw, sh, TextureFormat.RGB24, false);
                shot.ReadPixels(new Rect(sx, sy, sw, sh), 0, 0);
                shot.Apply(false, false);

                string rel = icon ? EmptyIconRel : EmptyIllustrationRel;
                string relAlt = icon ? EmptyIconRelAlt : EmptyIllustrationRelAlt;
                var template = LoadTemplateTexture(rel) ?? LoadTemplateTexture(relAlt);
                if (template == null)
                    template = CreateEmptyFrameTexture(frameW, frameH, icon ? IconContentX : IllContentX, icon ? IconContentY : IllContentY, cw, ch);
                if (template == null)
                {
                    UnityEngine.Object.Destroy(shot);
                    DialogBoxManager.Notification("Could not load empty template.");
                    CancelCapture();
                    return;
                }

                var scaled = ScaleTexture(shot, cw, ch);
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

                string folder = CustomTransformationCatalog.EnsureImportFolder();
                string fileName;
                if (_standaloneCapture)
                {
                    fileName = "capture_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + (icon ? "_icon.png" : "_illustration.png");
                }
                else
                {
                    string safeChar = SanitizeFileToken(_characterId ?? "char");
                    string safeForm = SanitizeFileToken(_draft != null ? _draft.ID : "form");
                    fileName = safeChar + "_" + safeForm + (icon ? "_icon.png" : "_illustration.png");
                }
                string abs = System.IO.Path.Combine(folder, fileName);
                File.WriteAllBytes(abs, template.EncodeToPNG());
                UnityEngine.Object.Destroy(template);

                if (!_standaloneCapture && _draft != null)
                {
                    if (icon) _draft.IconPath = abs;
                    else _draft.IllustrationPath = abs;

                    if (_captureReturnScreen == ScreenKind.WorkshopPublish && icon)
                    {
                        _workshopPublishPreviewPath = abs;
                        if (_workshopPublishForm != null)
                            _workshopPublishForm.IconPath = abs;
                        _screen = ScreenKind.WorkshopPublish;
                    }
                    else if (_captureReturnScreen != ScreenKind.Editor)
                    {
                        _screen = _captureReturnScreen;
                    }

                    if (!string.IsNullOrEmpty(_characterId))
                    {
                        _draft.IsCustom = true;
                        CustomTransformationCatalog.UpsertForm(_characterId, _displayName, _draft);
                    }
                    ApplyDraftToTarget();
                }

                ModAPI.Notify((icon ? "Icon" : "Illustration") + " saved" + (_standaloneCapture ? (": " + fileName) : ""));
            }
            catch (Exception ex)
            {
                DialogBoxManager.Notification("Capture failed: " + ex.Message);
            }

            bool standalone = _standaloneCapture;
            _captureReturnScreen = ScreenKind.Editor;
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
            _captureBusy = false;
            _standaloneCapture = false;
            if (!standalone && _root != null)
            {
                _root.SetActive(true);
                RebuildBody();
            }
        }

        static bool TryGetCaptureScreenRect(out int sx, out int sy, out int sw, out int sh)
        {
            sx = sy = sw = sh = 0;
            if (_captureFrameRt == null) return false;
            bool icon = _captureKind == CaptureKind.Icon;
            int frameW = icon ? IconFrameW : IllFrameW;
            int frameH = icon ? IconFrameH : IllFrameH;
            int cx = icon ? IconContentX : IllContentX;
            int cyFromTop = icon ? IconContentY : IllContentY;
            int cw = icon ? IconContentW : IllContentW;
            int ch = icon ? IconContentH : IllContentH;
            float u0 = cx / (float)frameW;
            float u1 = (cx + cw) / (float)frameW;
            float v0 = (frameH - cyFromTop - ch) / (float)frameH;
            float v1 = (frameH - cyFromTop) / (float)frameH;

            Vector3[] corners = new Vector3[4];
            _captureFrameRt.GetWorldCorners(corners);
            Vector3 bl = FrameLerp(corners, u0, v0);
            Vector3 tr = FrameLerp(corners, u1, v1);
            var cam = GameObject.Find("Canvas")?.GetComponent<Canvas>()?.worldCamera;
            Vector2 sbl = RectTransformUtility.WorldToScreenPoint(cam, bl);
            Vector2 str = RectTransformUtility.WorldToScreenPoint(cam, tr);
            sx = Mathf.RoundToInt(Mathf.Min(sbl.x, str.x));
            sy = Mathf.RoundToInt(Mathf.Min(sbl.y, str.y));
            sw = Mathf.RoundToInt(Mathf.Abs(str.x - sbl.x));
            sh = Mathf.RoundToInt(Mathf.Abs(str.y - sbl.y));
            return sw > 4 && sh > 4;
        }

        static Vector3 FrameLerp(Vector3[] c, float u, float vFromBottom)
        {
            Vector3 bottom = Vector3.Lerp(c[0], c[3], u);
            Vector3 top = Vector3.Lerp(c[1], c[2], u);
            return Vector3.Lerp(bottom, top, vFromBottom);
        }

        static Texture2D LoadTemplateTexture(string relPath)
        {
            try
            {
                string abs = CustomTransformationCatalog.ResolveModPath(relPath);
                if (string.IsNullOrEmpty(abs) || !File.Exists(abs)) return null;
                byte[] bytes = File.ReadAllBytes(abs);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!tex.LoadImage(bytes)) return null;
                tex.filterMode = FilterMode.Point;
                tex.wrapMode = TextureWrapMode.Clamp;
                return tex;
            }
            catch { return null; }
        }

        static Sprite LoadEmptyFrameSprite(string rel, string relAlt, int w, int h)
        {
            Sprite spr = CustomTransformationCatalog.LoadSpriteFlexible(rel);
            if (spr == null) spr = CustomTransformationCatalog.LoadSpriteFlexible(relAlt);
            if (spr == null)
            {
                try { spr = ModAPI.LoadSprite(rel); } catch { }
            }
            if (spr == null)
            {
                try { spr = ModAPI.LoadSprite(relAlt); } catch { }
            }
            if (spr != null) return spr;

            int cx = w == IconFrameW ? IconContentX : IllContentX;
            int cy = w == IconFrameW ? IconContentY : IllContentY;
            int cw = w == IconFrameW ? IconContentW : IllContentW;
            int ch = w == IconFrameW ? IconContentH : IllContentH;
            var tex = CreateEmptyFrameTexture(w, h, cx, cy, cw, ch);
            if (tex == null) return null;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 35f);
        }

        static Texture2D CreateEmptyFrameTexture(int w, int h, int contentX, int contentYFromTop, int contentW, int contentH)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            Color clear = new Color(0f, 0f, 0f, 0f);
            Color border = new Color(1f, 0.92f, 0.2f, 1f);
            Color fill = new Color(0f, 0f, 0f, 0.18f);
            var pixels = new Color[w * h];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;

            int cy = h - contentYFromTop - contentH;
            int x0 = contentX;
            int y0 = cy;
            int x1 = contentX + contentW - 1;
            int y1 = cy + contentH - 1;
            int thickness = Mathf.Max(8, Mathf.Min(w, h) / 28);

            void Set(int x, int y, Color c)
            {
                if (x < 0 || y < 0 || x >= w || y >= h) return;
                pixels[y * w + x] = c;
            }

            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                    Set(x, y, fill);
            }

            for (int t = 0; t < thickness; t++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    Set(x, y0 + t, border);
                    Set(x, y1 - t, border);
                }
                for (int y = y0; y <= y1; y++)
                {
                    Set(x0 + t, y, border);
                    Set(x1 - t, y, border);
                }
            }

            // Outer frame rim so the whole card stays visible even if content rect is soft.
            int outer = Mathf.Max(4, thickness / 2);
            for (int t = 0; t < outer; t++)
            {
                for (int x = 0; x < w; x++)
                {
                    Set(x, t, border);
                    Set(x, h - 1 - t, border);
                }
                for (int y = 0; y < h; y++)
                {
                    Set(t, y, border);
                    Set(w - 1 - t, y, border);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false, false);
            return tex;
        }

        static void ConfirmDeleteForm(string formId)
        {
            if (string.IsNullOrEmpty(_characterId) || string.IsNullOrEmpty(formId)) return;
            var form = ResolveFormDto(formId);
            string name = form?.Name ?? formId;
            ShowConfirmDialog(
                "Delete custom form \"" + name + "\"?",
                "Delete",
                () => DeleteOrResetForm(formId, false));
        }

        static void ConfirmResetForm(string formId)
        {
            if (string.IsNullOrEmpty(_characterId) || string.IsNullOrEmpty(formId)) return;
            var form = ResolveFormDto(formId);
            string name = form?.Name ?? formId;
            ShowConfirmDialog(
                "Reset \"" + name + "\" to the default form?",
                "Reset",
                () => DeleteOrResetForm(formId, true));
        }

        static void DeleteOrResetForm(string formId, bool preferReset)
        {
            if (string.IsNullOrEmpty(_characterId) || string.IsNullOrEmpty(formId)) return;
            var root = _targetPerson != null ? _targetPerson.transform.root.gameObject : null;
            var backup = root != null ? root.GetComponent<CustomFormStockBackup>() : null;
            bool restored = false;

            CustomTransformationCatalog.DeleteForm(_characterId, formId);

            if (preferReset)
            {
                if (backup != null && backup.TryGet(formId, out var stock) && _targetCt != null)
                {
                    _targetCt.transformations.TransformationData[formId] = CustomTransformationCatalog.CloneStats(stock);
                    restored = true;
                    try
                    {
                        if (_targetPerson != null)
                            _targetCt.ApplyTransformation(_targetPerson, formId);
                    }
                    catch { }
                    ModAPI.Notify("Reset \"" + formId + "\" to default");
                }
                else
                {
                    ModAPI.Notify("Override cleared. Respawn the character to fully restore the default form.");
                }
            }
            else if (_targetCt != null && _targetCt.transformations?.TransformationData != null)
            {
                if (backup != null && backup.TryGet(formId, out var stock2))
                {
                    _targetCt.transformations.TransformationData[formId] = CustomTransformationCatalog.CloneStats(stock2);
                    restored = true;
                    try
                    {
                        if (_targetPerson != null)
                            _targetCt.ApplyTransformation(_targetPerson, formId);
                    }
                    catch { }
                }
                else
                {
                    _targetCt.transformations.TransformationData.Remove(formId);
                    if (_targetPerson != null && _targetCt.transformations.TransformationData.ContainsKey("Base"))
                    {
                        try { _targetCt.ApplyTransformation(_targetPerson, "Base"); }
                        catch { }
                    }
                }
                ModAPI.Notify(restored ? ("Reset \"" + formId + "\" to default") : ("Deleted \"" + formId + "\""));
            }
            else
            {
                ModAPI.Notify("Deleted \"" + formId + "\"");
            }

            if (_draft != null && string.Equals(_draft.ID, formId, StringComparison.OrdinalIgnoreCase))
            {
                _draft = null;
                _formId = null;
                _creatingNew = false;
                _screen = ScreenKind.Forms;
            }
            RebuildBody();
        }

        static void DeleteOrResetCurrentForm(bool preferReset)
        {
            if (_draft == null) return;
            DeleteOrResetForm(_draft.ID, preferReset);
        }

        static Texture2D ScaleTexture(Texture2D src, int w, int h)
        {
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            Graphics.Blit(src, rt);
            var dst = new Texture2D(w, h, TextureFormat.RGBA32, false);
            dst.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            dst.Apply(false, false);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return dst;
        }

        static string SanitizeFileToken(string value)
        {
            if (string.IsNullOrEmpty(value)) return "x";
            var chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (!(char.IsLetterOrDigit(c) || c == '_' || c == '-'))
                    chars[i] = '_';
            }
            return new string(chars);
        }

        static void OpenAuraMaker()
        {
            if (_draft == null) return;
            CloseOverlay(false);
            CaptureAuraBackup();
            _screen = ScreenKind.Aura;
            if (_root != null) _root.SetActive(true);
            if (_targetPerson != null) ApplyDraftToTarget();
            RebuildBody();
        }

        static void BuildAuraScreen()
        {
            if (_draft == null) return;
            SetWorldClickMode(false);
            GetEditorLayout(out float panelTop, out float panelH);

            CreateNavBar(_body, "Cancel", () =>
            {
                RestoreAuraBackup();
                _screen = ScreenKind.Editor;
                RebuildBody();
            }, "Apply", () =>
            {
                if (!string.IsNullOrEmpty(_characterId))
                {
                    if (string.IsNullOrEmpty(_draft.ID))
                        _draft.ID = "Custom_" + DateTime.Now.Ticks.ToString("x");
                    if (string.IsNullOrEmpty(_draft.Name))
                        _draft.Name = _draft.ID;
                    _draft.IsCustom = true;
                    CustomTransformationCatalog.UpsertForm(_characterId, _displayName, _draft);
                }
                SyncAuraDraftPreview();
                _screen = ScreenKind.Editor;
                RebuildBody();
            });

            var left = CreatePanel(_body, "Left", new Vector2(-(PanelW * 0.5f - 28f), panelTop - panelH * 0.5f), new Vector2(50f, panelH));
            var center = CreatePanel(_body, "Center", new Vector2(0f, panelTop - panelH * 0.5f), new Vector2(58f, panelH));
            var right = CreatePanel(_body, "Right", new Vector2(PanelW * 0.5f - 28f, panelTop - panelH * 0.5f), new Vector2(50f, panelH));

            CreateLabel(left, "Aura Presets", new Vector2(0f, left.sizeDelta.y * 0.5f - 4f), 2.4f).color = Accent;
            string selectedAura = string.IsNullOrEmpty(_draft.AuraID) ? "Base" : _draft.AuraID;
            var presets = AurasStorage.AurasVFX.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();
            float scrollH = left.sizeDelta.y - 10f;
            CreateScroll(left, new Vector2(0f, -8f), new Vector2(left.sizeDelta.x - 2f, scrollH), out Transform content);
            float y = -2.5f;
            for (int i = 0; i < presets.Count; i++)
            {
                string id = presets[i];
                var go = new GameObject("Aura_" + id);
                go.transform.SetParent(content, false);
                var rt = go.AddComponent<RectTransform>();
                AnchorTopCenter(rt);
                rt.anchoredPosition = new Vector2(0f, y);
                rt.sizeDelta = new Vector2(44f, 5.2f);
                bool sel = string.Equals(id, selectedAura, StringComparison.OrdinalIgnoreCase);
                go.AddComponent<Image>().color = sel ? Accent : new Color(0.16f, 0.16f, 0.16f, 1f);
                string capture = id;
                go.AddComponent<Button>().onClick.AddListener(() =>
                {
                    _draft.AuraID = capture;
                    SyncAuraDraftPreviewAndUi();
                });
                var textGo = new GameObject("T");
                textGo.transform.SetParent(go.transform, false);
                var trt = textGo.AddComponent<RectTransform>();
                trt.anchorMin = Vector2.zero;
                trt.anchorMax = Vector2.one;
                trt.offsetMin = new Vector2(1.2f, 0f);
                trt.offsetMax = new Vector2(-1.2f, 0f);
                var tmp = textGo.AddComponent<TextMeshProUGUI>();
                tmp.text = id;
                tmp.font = Font;
                tmp.fontSize = 2.05f;
                tmp.alignment = TextAlignmentOptions.Left;
                tmp.color = Color.white;
                tmp.enableWordWrapping = false;
                tmp.overflowMode = TextOverflowModes.Ellipsis;
                tmp.raycastTarget = false;
                y -= 5.8f;
            }
            content.GetComponent<RectTransform>().sizeDelta = new Vector2(left.sizeDelta.x - 2f, Mathf.Max(scrollH, Mathf.Abs(y) + 4f));

            BuildCenterPreview(center);

            string statusText = selectedAura + (_draft.UseCustomAura ? " | custom" : " | preset") + " | x" + (_draft.AuraScale <= 0.01f ? 1f : _draft.AuraScale).ToString("0.##");
            CreateLabel(right, statusText, new Vector2(0f, right.sizeDelta.y * 0.5f - 5f), 2f).color = Accent;

            float by = right.sizeDelta.y * 0.5f - 14f;
            void RightBtn(string label, Action act)
            {
                CreateSmallButton(right, label, new Vector2(0f, by), act, false, 44f);
                by -= 7.2f;
            }

            RightBtn("Primary Color", () =>
            {
                OpenColorPicker("Aura Primary", "Pick an aura colour and press Apply.", _auraPrimary, color =>
                {
                    _auraPrimary = new Color(color.r, color.g, color.b, 1f);
                    _draft.UseCustomAura = true;
                    SyncAuraDraftPreviewAndUi();
                });
            });
            RightBtn("Secondary Color", () =>
            {
                OpenColorPicker("Aura Secondary", "Pick an aura colour and press Apply.", _auraSecondary, color =>
                {
                    _auraSecondary = new Color(color.r, color.g, color.b, 1f);
                    _draft.UseCustomAura = true;
                    _draft.UseAuraSecondaryColor = true;
                    SyncAuraDraftPreviewAndUi();
                });
            });
            RightBtn(_draft.UseCustomAura ? "Custom: ON" : "Custom: OFF", () =>
            {
                _draft.UseCustomAura = !_draft.UseCustomAura;
                SyncAuraDraftPreviewAndUi();
            });
            RightBtn("Scale: x" + (_draft.AuraScale <= 0.01f ? 1f : _draft.AuraScale).ToString("0.##"), () =>
            {
                float cur = _draft.AuraScale <= 0.01f ? 1f : _draft.AuraScale;
                PromptFloat(cur, v =>
                {
                    _draft.AuraScale = Mathf.Clamp(v, 0.25f, 4f);
                    SyncAuraDraftPreviewAndUi();
                });
            });
        }

        static void OpenHairColorPicker()
        {
            OpenColorPicker("Hair Recolour", "Pick a hair colour tint and press Apply.", _hairTint, color =>
            {
                _hairTint = new Color(color.r, color.g, color.b, 1f);
                _draft.HairColorR = color.r;
                _draft.HairColorG = color.g;
                _draft.HairColorB = color.b;
                PreviewLive();
            });
        }

        static void OpenKiBarColorPicker()
        {
            Color current = _draft.UseKiBarColor
                ? new Color(_draft.KiBarColorR, _draft.KiBarColorG, _draft.KiBarColorB, 1f)
                : new Color(0.2f, 0.85f, 1f, 1f);
            OpenColorPicker("Ki Bar Colour", "Pick the ki bar colour for this form.", current, color =>
            {
                _draft.UseKiBarColor = true;
                _draft.KiBarColorR = color.r;
                _draft.KiBarColorG = color.g;
                _draft.KiBarColorB = color.b;
                PreviewLive();
            });
        }

        static void OpenColorPicker(string title, string description, Color current, Action<Color> onApply)
        {
            var picker = ColorpickerDialogBehaviour.Instance;
            if (picker == null)
            {
                DialogBoxManager.Notification("Color picker is not available right now.");
                return;
            }
            bool rootWasOn = _root != null && _root.activeSelf;
            if (_root != null) _root.SetActive(false);
            if (_overlay != null) _overlay.SetActive(false);
            if (picker.Title != null) picker.Title.text = title;
            if (picker.Description != null) picker.Description.text = description;
            picker.OnApply.RemoveAllListeners();
            picker.OnCancel.RemoveAllListeners();
            picker.OnApply.AddListener(new UnityEngine.Events.UnityAction<Color>(color =>
            {
                if (rootWasOn && _root != null) _root.SetActive(true);
                if (_overlay != null) _overlay.SetActive(true);
                onApply?.Invoke(color);
            }));
            picker.OnCancel.AddListener(new UnityEngine.Events.UnityAction(() =>
            {
                if (rootWasOn && _root != null) _root.SetActive(true);
                if (_overlay != null) _overlay.SetActive(true);
            }));
            picker.Open(current);
        }

        static void PromptFloat(float value, Action<float> apply)
        {
            bool rootWasOn = _root != null && _root.activeSelf;
            if (_root != null) _root.SetActive(false);
            CloseOverlay(false);
            var host = DbpuMakerPhysics.MakeDialogPhysicsHost("TmPromptHost");
            var phys = host.GetComponent<PhysicalBehaviour>();
            if (phys == null) phys = host.AddComponent<PhysicalBehaviour>();
            DbpuMakerPhysics.StripWorldPhysics(host);
            Utils.OpenFloatInputDialog(value, phys, (p, v) =>
            {
                apply?.Invoke(v);
                UnityEngine.Object.Destroy(host);
                if (rootWasOn && _root != null) _root.SetActive(true);
            }, "Set Value", "Enter a number");
        }

        static void PromptString(string value, Action<string> apply)
        {
            bool rootWasOn = _root != null && _root.activeSelf;
            if (_root != null) _root.SetActive(false);
            CloseOverlay(false);

            DialogBox box = null;
            box = DialogBoxManager.TextEntry(
                "Set Text",
                string.IsNullOrEmpty(value) ? "Type here..." : value,
                new DialogButton("OK", true, new UnityEngine.Events.UnityAction[]
                {
                    () =>
                    {
                        string result = box != null ? (box.EnteredText ?? "") : "";
                        if (rootWasOn && _root != null) _root.SetActive(true);
                        apply?.Invoke(result);
                    }
                }),
                new DialogButton("Cancel", true, new UnityEngine.Events.UnityAction[]
                {
                    () =>
                    {
                        if (rootWasOn && _root != null)
                        {
                            _root.SetActive(true);
                            RebuildBody();
                        }
                    }
                }));

            if (box != null)
                box.EnteredText = value ?? "";
        }

        static List<string> CollectSkinSuffixOptions()
        {
            var options = new List<string> { "" };
            if (_targetPerson == null) return options;
            var root = _targetPerson.transform.root;
            var head = root.Find("Head");
            var skinsMenu = head != null ? head.GetComponent<SkinContextMenu>() : null;
            var cSkins = root.GetComponent<CharacterSkins>();
            if (skinsMenu == null || cSkins == null || cSkins.skins == null || cSkins.skins.SkinData == null)
                return options;

            string skinKey = skinsMenu.CurrentSkin;
            if (string.IsNullOrEmpty(skinKey) || !cSkins.skins.SkinData.TryGetValue(skinKey, out var info) || info?.Skins == null)
                return options;

            foreach (var key in info.Skins.Keys)
            {
                if (string.Equals(key, skinKey, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (key.StartsWith(skinKey, StringComparison.OrdinalIgnoreCase))
                {
                    string suffix = key.Substring(skinKey.Length);
                    if (!options.Contains(suffix)) options.Add(suffix);
                }
            }

            if (!options.Contains("_Buffed")) options.Add("_Buffed");
            if (!options.Contains("_Buffed2")) options.Add("_Buffed2");
            return options;
        }

        static void OpenSkinSuffixPicker()
        {
            if (_draft == null) return;
            CloseOverlay(false);
            if (_root != null) _root.SetActive(false);

            _overlay = new GameObject("SkinSuffixPicker");
            _overlay.transform.SetParent(GameObject.Find("Canvas").transform, false);
            var ort = _overlay.AddComponent<RectTransform>();
            ort.anchorMin = Vector2.zero;
            ort.anchorMax = Vector2.one;
            ort.offsetMin = Vector2.zero;
            ort.offsetMax = Vector2.zero;
            _overlay.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

            var host = new GameObject("Host");
            host.transform.SetParent(_overlay.transform, false);
            var hostRt = host.AddComponent<RectTransform>();
            hostRt.anchoredPosition = new Vector2(0f, 30f);
            hostRt.localScale = new Vector3(6.5f, 6.5f, 1f);

            var panel = new GameObject("Panel");
            panel.transform.SetParent(host.transform, false);
            var panelRt = panel.AddComponent<RectTransform>();
            panelRt.sizeDelta = new Vector2(90f, 78f);
            panel.AddComponent<Image>().color = PanelBg;
            CreateLabel(panel.transform, "Skin Suffix", new Vector2(0f, 33f), 3.2f).color = Accent;

            var options = CollectSkinSuffixOptions();
            CreateScroll(panel.transform, new Vector2(0f, -4f), new Vector2(82f, 48f), out Transform content);
            float y = -3f;
            for (int i = 0; i < options.Count; i++)
            {
                string suffix = options[i];
                string label = string.IsNullOrEmpty(suffix) ? "(default / none)" : suffix;
                bool sel = string.Equals(_draft.RequiredSkin_Suffix ?? "", suffix, StringComparison.Ordinal);
                var go = new GameObject("Suf");
                go.transform.SetParent(content, false);
                var rt = go.AddComponent<RectTransform>();
                AnchorTopCenter(rt);
                rt.anchoredPosition = new Vector2(0f, y);
                rt.sizeDelta = new Vector2(76f, 5.2f);
                go.AddComponent<Image>().color = sel ? Accent : new Color(0.16f, 0.16f, 0.16f, 1f);
                string capture = suffix;
                go.AddComponent<Button>().onClick.AddListener(() =>
                {
                    _draft.RequiredSkin_Suffix = capture ?? "";
                    ApplyDraftToTarget();
                    CloseOverlay(true);
                    if (_root != null) RebuildBody();
                });
                var t = CreateLabel(go.transform, label, Vector2.zero, 2.1f);
                t.alignment = TextAlignmentOptions.Center;
                y -= 6f;
            }
            content.GetComponent<RectTransform>().sizeDelta = new Vector2(82f, Mathf.Abs(y) + 4f);
            CreateSmallButton(panel.transform, "Close", new Vector2(0f, -32f), () => CloseOverlay(true));
        }

        static List<string> CollectLimbNames()
        {
            var names = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (_targetPerson == null) return names;
            foreach (var limb in _targetPerson.Limbs)
            {
                if (limb == null) continue;
                string n = limb.gameObject.name;
                if (seen.Add(n)) names.Add(n);
            }
            if (names.Count == 0)
            {
                names.AddRange(new[] { "Head", "UpperBody", "MiddleBody", "LowerBody" });
            }
            return names;
        }

        static void SeedAccessoriesFromCharacter()
        {
            if (_draft == null || _targetPerson == null) return;
            if (_draft.Accessories == null) _draft.Accessories = new List<CustomAccessoryDto>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _draft.Accessories.Count; i++)
            {
                var a = _draft.Accessories[i];
                if (a == null) continue;
                string id = !string.IsNullOrEmpty(a.Id) ? a.Id : ((a.Name ?? "") + "@" + (a.LimbName ?? "") + "|" + (a.SpritePath ?? ""));
                if (!string.IsNullOrEmpty(id)) seen.Add(id);
            }

            var root = _targetPerson.transform.root;
            var renders = root.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renders.Length; i++)
            {
                var sr = renders[i];
                if (sr == null || sr.sprite == null || sr.transform == null) continue;
                string n = sr.gameObject.name ?? "";
                if (string.Equals(n, "Hair", StringComparison.OrdinalIgnoreCase)) continue;
                if (n.StartsWith("TmAccessory_", StringComparison.OrdinalIgnoreCase)
                    || n.IndexOf("Accessory", StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("Extra", StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("Scouter", StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("Cape", StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("Ear", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    string limbName = sr.transform.parent != null ? sr.transform.parent.name : "Head";
                    string key = n + "@" + limbName;
                    if (!seen.Add(key)) continue;
                    string path = CustomTransformationCatalog.EnsurePersistedSpritePath(
                        null, sr.sprite, "accessories", "seed_" + (_draft.Accessories.Count + 1));
                    if (string.IsNullOrEmpty(path)) continue;
                    _draft.Accessories.Add(new CustomAccessoryDto
                    {
                        Id = DateTime.Now.Ticks.ToString("x") + "_" + i,
                        Name = n,
                        LimbName = limbName,
                        SpritePath = path,
                        LocalX = sr.transform.localPosition.x,
                        LocalY = sr.transform.localPosition.y,
                        LocalZ = sr.transform.localPosition.z,
                        RotationZ = sr.transform.localEulerAngles.z,
                        Scale = Mathf.Abs(sr.transform.localScale.x) < 0.01f ? 1f : Mathf.Abs(sr.transform.localScale.x),
                        SortingOrder = 1
                    });
                }
            }

            if (_targetCt?.transformations?.TransformationData != null && !string.IsNullOrEmpty(_draft.ID)
                && _targetCt.transformations.TransformationData.TryGetValue(_draft.ID, out var stats)
                && stats?.PhyAccessory != null
                && stats.PhyAccessory.sprites != null
                && stats.PhyAccessory.sprites.Length > 0
                && stats.PhyAccessory.sprites[0] != null)
            {
                string phyName = string.IsNullOrEmpty(stats.PhyAccessory.Name) ? "PhyAccessory" : stats.PhyAccessory.Name;
                if (seen.Add("phy:" + phyName))
                {
                    string path = CustomTransformationCatalog.EnsurePersistedSpritePath(
                        null, stats.PhyAccessory.sprites[0], "accessories", "phy_" + phyName);
                    if (!string.IsNullOrEmpty(path))
                    {
                        _draft.Accessories.Add(new CustomAccessoryDto
                        {
                            Id = "phy_" + phyName,
                            Name = phyName,
                            LimbName = stats.PhyAccessory.objForAttach != null
                                ? stats.PhyAccessory.objForAttach.name
                                : "LowerBody",
                            SpritePath = path,
                            Scale = 1f,
                            SortingOrder = 1
                        });
                    }
                }
            }
        }

        static void OpenAccessoriesEditor()
        {
            if (_draft == null) return;
            if (_draft.Accessories == null) _draft.Accessories = new List<CustomAccessoryDto>();
            SeedAccessoriesFromCharacter();
            CloseOverlay(false);
            _editAccessoryIndex = -1;
            _screen = ScreenKind.Accessories;
            if (_root != null) _root.SetActive(true);
            RebuildBody();
        }

        static void BuildAccessoriesScreen()
        {
            if (_draft == null)
            {
                _screen = ScreenKind.Forms;
                RebuildBody();
                return;
            }
            if (_draft.Accessories == null) _draft.Accessories = new List<CustomAccessoryDto>();
            SetWorldClickMode(false);
            GetEditorLayout(out float panelTop, out float panelH);

            CreateNavBar(_body, "Back", () =>
            {
                try { ApplyDraftToTarget(); } catch (Exception ex) { Debug.LogWarning("[DBPU] Accessories back apply failed: " + ex.Message); }
                _editAccessoryIndex = -1;
                ClearPreviewLimbPick();
                _screen = ScreenKind.Editor;
                RebuildBody();
            }, "Done", () =>
            {
                try { ApplyDraftToTarget(); } catch (Exception ex) { Debug.LogWarning("[DBPU] Accessories done apply failed: " + ex.Message); }
                _editAccessoryIndex = -1;
                ClearPreviewLimbPick();
                _screen = ScreenKind.Editor;
                RebuildBody();
            });

            var left = CreatePanel(_body, "Left", new Vector2(-(PanelW * 0.5f - 28f), panelTop - panelH * 0.5f), new Vector2(50f, panelH));
            var center = CreatePanel(_body, "Center", new Vector2(0f, panelTop - panelH * 0.5f), new Vector2(58f, panelH));
            var right = CreatePanel(_body, "Right", new Vector2(PanelW * 0.5f - 28f, panelTop - panelH * 0.5f), new Vector2(50f, panelH));

            CreateLabel(left, "List", new Vector2(0f, left.sizeDelta.y * 0.5f - 4f), 2.4f).color = Accent;
            float scrollH = left.sizeDelta.y - 10f;
            CreateScroll(left, new Vector2(0f, -8f), new Vector2(left.sizeDelta.x - 2f, scrollH), out Transform content);
            float y = -2.5f;
            for (int i = 0; i < _draft.Accessories.Count; i++)
            {
                int idx = i;
                var acc = _draft.Accessories[i];
                if (acc == null) continue;
                string label = (acc.Name ?? "Accessory") + " @ " + (acc.LimbName ?? "?");
                var go = new GameObject("Acc");
                go.transform.SetParent(content, false);
                var rt = go.AddComponent<RectTransform>();
                AnchorTopCenter(rt);
                rt.anchoredPosition = new Vector2(0f, y);
                rt.sizeDelta = new Vector2(44f, 10.5f);
                go.AddComponent<Image>().color = new Color(0.16f, 0.16f, 0.16f, 1f);
                CreateLabel(go.transform, label, new Vector2(0f, 2.2f), 1.85f);
                CreateSmallButton(go.transform, "Edit", new Vector2(-8f, -2.4f), () => EditAccessory(idx), false, 18f);
                CreateSmallButton(go.transform, "X", new Vector2(12f, -2.4f), () =>
                {
                    if (_draft?.Accessories == null || idx < 0 || idx >= _draft.Accessories.Count) return;
                    _draft.Accessories.RemoveAt(idx);
                    try { PreviewLive(); } catch { }
                    RebuildBody();
                }, false, 12f);
                y -= 11.5f;
            }
            content.GetComponent<RectTransform>().sizeDelta = new Vector2(left.sizeDelta.x - 2f, Mathf.Max(scrollH, Mathf.Abs(y) + 4f));

            BuildCenterPreview(center);

            CreateLabel(right, "Actions", new Vector2(0f, right.sizeDelta.y * 0.5f - 4f), 2.4f).color = Accent;
            CreateSmallButton(right, "Add Accessory", new Vector2(0f, right.sizeDelta.y * 0.5f - 14f), () =>
            {
                if (_draft == null) return;
                if (_draft.Accessories == null) _draft.Accessories = new List<CustomAccessoryDto>();
                var limbs = CollectLimbNames();
                _draft.Accessories.Add(new CustomAccessoryDto
                {
                    Id = DateTime.Now.Ticks.ToString("x"),
                    Name = "Accessory",
                    LimbName = !string.IsNullOrEmpty(_previewSelectedLimb) ? _previewSelectedLimb
                        : (limbs.Count > 0 ? limbs[0] : "Head"),
                    Scale = 1f,
                    SortingOrder = 1
                });
                EditAccessory(_draft.Accessories.Count - 1);
            }, false, 44f);
            CreateLabel(right, "Click a limb in\nthe preview to\npreselect attach.", new Vector2(0f, 0f), 2f);
        }

        static void EditAccessory(int index)
        {
            if (_draft?.Accessories == null || index < 0 || index >= _draft.Accessories.Count) return;
            CloseOverlay(false);
            _editAccessoryIndex = index;
            _screen = ScreenKind.AccessoryEdit;
            if (_root != null) _root.SetActive(true);
            RebuildBody();
        }

        static void BuildAccessoryEditScreen()
        {
            if (_draft?.Accessories == null || _editAccessoryIndex < 0 || _editAccessoryIndex >= _draft.Accessories.Count)
            {
                _editAccessoryIndex = -1;
                _screen = ScreenKind.Accessories;
                RebuildBody();
                return;
            }

            var acc = _draft.Accessories[_editAccessoryIndex];
            if (acc == null)
            {
                _editAccessoryIndex = -1;
                _screen = ScreenKind.Accessories;
                RebuildBody();
                return;
            }
            int index = _editAccessoryIndex;
            SetWorldClickMode(false);
            GetEditorLayout(out float panelTop, out float panelH);

            CreateNavBar(_body, "Back", () =>
            {
                try { ApplyDraftToTarget(); } catch (Exception ex) { Debug.LogWarning("[DBPU] Accessory edit back failed: " + ex.Message); }
                _editAccessoryIndex = -1;
                ClearPreviewLimbPick();
                _screen = ScreenKind.Accessories;
                RebuildBody();
            }, null, null);

            var left = CreatePanel(_body, "Left", new Vector2(-(PanelW * 0.5f - 28f), panelTop - panelH * 0.5f), new Vector2(50f, panelH));
            var center = CreatePanel(_body, "Center", new Vector2(0f, panelTop - panelH * 0.5f), new Vector2(58f, panelH));
            var right = CreatePanel(_body, "Right", new Vector2(PanelW * 0.5f - 28f, panelTop - panelH * 0.5f), new Vector2(50f, panelH));

            CreateLabel(left, "Properties", new Vector2(0f, left.sizeDelta.y * 0.5f - 4f), 2.4f).color = Accent;
            float scrollH = left.sizeDelta.y - 10f;
            CreateScroll(left, new Vector2(0f, -8f), new Vector2(left.sizeDelta.x - 2f, scrollH), out Transform content);
            float y = -2.5f;
            void Prop(string label, Action act)
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
                tmp.fontSize = 2.05f;
                tmp.alignment = TextAlignmentOptions.Left;
                tmp.color = Color.white;
                tmp.enableWordWrapping = false;
                tmp.overflowMode = TextOverflowModes.Ellipsis;
                tmp.raycastTarget = false;
                y -= 6f;
            }

            Prop("Name: " + (acc.Name ?? ""), () => PromptString(acc.Name ?? "", v => { acc.Name = v; RebuildBody(); }));
            Prop("Sprite: " + (string.IsNullOrEmpty(acc.SpritePath) ? "(none)" : System.IO.Path.GetFileName(acc.SpritePath)), () => PickAccessorySprite(index));
            Prop("Scale: " + (acc.Scale <= 0.01f ? 1f : acc.Scale).ToString("0.##"), () => PromptFloat(acc.Scale <= 0.01f ? 1f : acc.Scale, v =>
            {
                acc.Scale = Mathf.Clamp(v, 0.05f, 20f);
                PreviewLive();
            }));
            Prop("Offset X: " + acc.LocalX.ToString("0.###"), () => PromptFloat(acc.LocalX, v => { acc.LocalX = v; PreviewLive(); }));
            Prop("Offset Y: " + acc.LocalY.ToString("0.###"), () => PromptFloat(acc.LocalY, v => { acc.LocalY = v; PreviewLive(); }));
            Prop("Rotation: " + acc.RotationZ.ToString("0.#"), () => PromptFloat(acc.RotationZ, v => { acc.RotationZ = v; PreviewLive(); }));
            content.GetComponent<RectTransform>().sizeDelta = new Vector2(left.sizeDelta.x - 2f, Mathf.Max(scrollH, Mathf.Abs(y) + 4f));

            BuildCenterPreview(center);

            CreateLabel(right, "Attach Limb", new Vector2(0f, right.sizeDelta.y * 0.5f - 4f), 2.4f).color = Accent;
            var limbs = CollectLimbNames();
            float limbScrollH = right.sizeDelta.y - 18f;
            CreateScroll(right, new Vector2(0f, -8f), new Vector2(right.sizeDelta.x - 2f, limbScrollH), out Transform limbContent);
            float ly = -2.5f;
            for (int i = 0; i < limbs.Count; i++)
            {
                string limb = limbs[i];
                bool sel = string.Equals(acc.LimbName, limb, StringComparison.OrdinalIgnoreCase);
                var go = new GameObject("Limb");
                go.transform.SetParent(limbContent, false);
                var rt = go.AddComponent<RectTransform>();
                AnchorTopCenter(rt);
                rt.anchoredPosition = new Vector2(0f, ly);
                rt.sizeDelta = new Vector2(44f, 5.2f);
                go.AddComponent<Image>().color = sel ? Accent : new Color(0.16f, 0.16f, 0.16f, 1f);
                string capture = limb;
                go.AddComponent<Button>().onClick.AddListener(() =>
                {
                    acc.LimbName = capture;
                    PreviewLive();
                });
                CreateLabel(go.transform, limb, Vector2.zero, 2f).alignment = TextAlignmentOptions.Center;
                ly -= 5.8f;
            }
            limbContent.GetComponent<RectTransform>().sizeDelta = new Vector2(right.sizeDelta.x - 2f, Mathf.Max(limbScrollH, Mathf.Abs(ly) + 4f));
            CreateSmallButton(right, "Import PNG", new Vector2(0f, -right.sizeDelta.y * 0.5f + 5f), () => PickAccessorySprite(index), false, 44f);
        }

        static void PickAccessorySprite(int accessoryIndex)
        {
            if (_draft?.Accessories == null || accessoryIndex < 0 || accessoryIndex >= _draft.Accessories.Count) return;
            if (TmFilePicker.IsOpen) return;
            var acc = _draft.Accessories[accessoryIndex];
            try
            {
                string src = null;
                RunWithUiYield(() =>
                {
                    src = TmFilePicker.OpenFile("Accessory Sprite", "PNG Images (*.png)|*.png|All files (*.*)|*.*");
                });
                if (string.IsNullOrEmpty(src)) return;
                string folder = CustomTransformationCatalog.EnsureImportFolder();
                string destDir = System.IO.Path.Combine(folder, "accessories");
                if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);
                string dest = System.IO.Path.Combine(destDir, System.IO.Path.GetFileName(src));
                File.Copy(src, dest, true);
                acc.SpritePath = dest;
                ApplyDraftToTarget();
            }
            catch (Exception ex)
            {
                DialogBoxManager.Notification("Import failed: " + ex.Message);
            }
            _editAccessoryIndex = accessoryIndex;
            _screen = ScreenKind.AccessoryEdit;
            RebuildBody();
        }

        static void ApplyDraftToTarget()
        {
            if (_draft == null || _targetPerson == null || _targetCt == null) return;
            var stats = CustomTransformationCatalog.ToStats(_draft);
            if (stats == null) return;

            if (stats.HairSprite == null && !string.IsNullOrEmpty(_selectedHairPath))
                stats.HairSprite = ResolveSelectedSprite(_selectedHairPath, _hairList);
            if (stats.HeadSprite == null && !string.IsNullOrEmpty(_selectedHeadPath))
                stats.HeadSprite = ResolveSelectedSprite(_selectedHeadPath, _headList);
            if (stats.HairSprite == null && !string.IsNullOrEmpty(_selectedHairPath))
                stats.HairSprite = CustomTransformationCatalog.LoadSpriteFlexible(_selectedHairPath);
            if (stats.HeadSprite == null && !string.IsNullOrEmpty(_selectedHeadPath))
                stats.HeadSprite = CustomTransformationCatalog.LoadSpriteFlexible(_selectedHeadPath);
            if (stats.Icon == null) stats.Icon = stats.HairSprite ?? stats.HeadSprite;
            if (stats.Illustration == null) stats.Illustration = stats.Icon;

            if (_targetCt.transformations.TransformationData.ContainsKey(stats.ID))
            {
                CustomTransformationCatalog.BackupStockIfNeeded(
                    _targetPerson.transform.root.gameObject,
                    stats.ID,
                    _targetCt.transformations.TransformationData[stats.ID]);
                _targetCt.transformations.TransformationData[stats.ID] = stats;
            }
            else
                _targetCt.transformations.AddTransformation(stats.ID, stats);

            var ki = _targetPerson.GetComponent<KiSystem>();
            if (ki != null) ki.EnsureReadyForTransforms();

            try { _targetCt.ApplyTransformation(_targetPerson, stats.ID); }
            catch (Exception ex)
            {
                Debug.LogWarning("Transformation Maker apply failed: " + ex.Message);
            }

            var root = _targetPerson.transform.root;
            var head = root.Find("Head");
            if (head != null)
            {
                var headSr = head.GetComponent<SpriteRenderer>();
                if (headSr != null && stats.HeadSprite != null)
                    headSr.sprite = stats.HeadSprite;

                TmHairApply.ApplyHair(
                    head,
                    stats.HairSprite,
                    _hairTint,
                    new Vector3(_draft.HairLocalX, _draft.HairLocalY, _draft.HairLocalZ),
                    _draft.UseHairLocalPosition);

                var menu = head.GetComponent<TransformationContextMenu>();
                if (menu != null)
                {
                    menu.Form = stats.ID;
                    menu.CurrentAuraID = stats.AuraID;
                }
            }

            CustomTransformationCatalog.ApplyLimbSpritePaths(_targetPerson, _draft);

            var formTint = root.GetComponent<CustomFormHairTintBehaviour>();
            if (formTint == null) formTint = root.gameObject.AddComponent<CustomFormHairTintBehaviour>();
            formTint.CharacterId = _characterId;
            formTint.NotifyFormApplied(stats.ID);

            var accBeh = root.GetComponent<CustomFormAccessoryBehaviour>();
            if (accBeh == null) accBeh = root.gameObject.AddComponent<CustomFormAccessoryBehaviour>();
            accBeh.CharacterId = _characterId;
            accBeh.ApplyDraft(_draft);

            var auraTint = root.GetComponent<CustomFormAuraTintBehaviour>();
            if (auraTint == null) auraTint = root.gameObject.AddComponent<CustomFormAuraTintBehaviour>();
            auraTint.CharacterId = _characterId;
            auraTint.Invalidate();

            var aura = root.GetComponent<AuraHandler>();
            if (aura != null && !string.IsNullOrEmpty(stats.AuraID))
            {
                if (aura.person == null) aura.person = _targetPerson;
                aura.CurrentAuraID = stats.AuraID;
                aura.AuraVisible = true;
                aura.AuraActive = true;
                aura.CreateAura(stats.AuraID, false);
                float scale = _draft.AuraScale <= 0.01f ? 1f : _draft.AuraScale;
                if (aura.HasSpawnedAuraInstance)
                {
                    if (_draft.UseCustomAura)
                    {
                        Color primary = new Color(_draft.AuraColorR, _draft.AuraColorG, _draft.AuraColorB, 1f);
                        Color secondary = new Color(_draft.AuraColor2R, _draft.AuraColor2G, _draft.AuraColor2B, 1f);
                        aura.ApplyCustomAuraColors(primary, secondary, _draft.UseAuraSecondaryColor, scale);
                    }
                    else
                    {
                        aura.SetAuraScale(scale);
                    }
                }
            }

            DbpuMakerPhysics.CalmPerson(_targetPerson);
        }

        public static void IngestSpriteEditorApply(PersonBehaviour person, IList<LiveSpriteEditor.CharacterSpriteEditorTarget.MakerSpriteExport> exports)
        {
            if (!IsOpen || _draft == null || person == null) return;
            if (_targetPerson != null
                && person.transform.root != _targetPerson.transform.root)
                return;
            if (exports == null || exports.Count == 0) return;

            CustomTransformationCatalog.EnsureLimbSpritePaths(_draft);
            bool any = false;
            string stem = (_draft.ID ?? "form") + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            for (int i = 0; i < exports.Count; i++)
            {
                var exp = exports[i];
                if (exp == null || exp.Png == null || exp.Png.Length == 0) continue;
                try
                {
                    if (exp.IsHair)
                    {
                        string dir = Path.Combine(CustomTransformationCatalog.EnsureImportFolder(), "hair");
                        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                        string path = Path.Combine(dir, stem + "_hair.png");
                        File.WriteAllBytes(path, exp.Png);
                        _selectedHairPath = path;
                        CustomTransformationCatalog.SetLimbSpritePath(_draft, "Hair", path);
                        any = true;
                        continue;
                    }

                    string limb = DbpuSpriteEditorBridge.MapLimbKey(exp.LimbName ?? "");
                    if (string.IsNullOrEmpty(limb) || string.Equals(limb, "Hair", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string sub = string.Equals(limb, "Head", StringComparison.OrdinalIgnoreCase)
                        ? "head"
                        : Path.Combine("limbs", limb);
                    string limbDir = Path.Combine(CustomTransformationCatalog.EnsureImportFolder(), sub);
                    if (!Directory.Exists(limbDir)) Directory.CreateDirectory(limbDir);
                    string limbPath = Path.Combine(limbDir, stem + "_" + limb.ToLowerInvariant() + ".png");
                    File.WriteAllBytes(limbPath, exp.Png);
                    CustomTransformationCatalog.SetLimbSpritePath(_draft, limb, limbPath);
                    if (string.Equals(limb, "Head", StringComparison.OrdinalIgnoreCase))
                    {
                        _selectedHeadPath = limbPath;
                        _draft.HeadSpritePath = limbPath;
                    }
                    _selectedLimbKey = limb;
                    _previewSelectedLimb = limb;
                    any = true;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[DBPU] Transformation Maker ingest failed: " + ex.Message);
                }
            }

            if (!any) return;
            ApplyDraftToTarget();
            if (IsOpen) RebuildBody();
            ModAPI.Notify("Sprite edits written to transformation draft.");
        }

        public static void PersistSpriteEditorToCurrentForm(
            PersonBehaviour person,
            IList<LiveSpriteEditor.CharacterSpriteEditorTarget.MakerSpriteExport> exports)
        {
            if (person == null || exports == null || exports.Count == 0) return;
            var root = person.transform.root.gameObject;
            var identity = root.GetComponent<DbpuCharacterIdentity>();
            if (identity == null || string.IsNullOrEmpty(identity.CharacterId)) return;

            string formId = "Base";
            var head = root.transform.Find("Head");
            var menu = head != null ? head.GetComponent<TransformationContextMenu>() : null;
            if (menu != null && !string.IsNullOrEmpty(menu.Form))
                formId = menu.Form;

            var dto = CustomTransformationCatalog.CloneForm(
                CustomTransformationCatalog.GetForm(identity.CharacterId, formId));
            if (dto == null)
            {
                var ct = root.GetComponent<CharacterTransformations>();
                if (ct?.transformations?.TransformationData != null
                    && ct.transformations.TransformationData.TryGetValue(formId, out var stats)
                    && stats != null)
                    dto = CustomTransformationCatalog.FromStats(stats);
                else
                    dto = new CustomTransformationFormDto { ID = formId, Name = formId };
                dto.ID = formId;
                if (string.IsNullOrEmpty(dto.Name)) dto.Name = formId;
            }

            CustomTransformationCatalog.EnsureLimbSpritePaths(dto);
            bool any = false;
            string stem = (dto.ID ?? "form") + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            for (int i = 0; i < exports.Count; i++)
            {
                var exp = exports[i];
                if (exp == null || exp.Png == null || exp.Png.Length == 0) continue;
                try
                {
                    if (exp.IsHair)
                    {
                        string dir = Path.Combine(CustomTransformationCatalog.EnsureImportFolder(), "hair");
                        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                        string path = Path.Combine(dir, stem + "_hair.png");
                        CustomTransformationCatalog.WriteAllBytesShared(path, exp.Png);
                        CustomTransformationCatalog.SetLimbSpritePath(dto, "Hair", path);
                        any = true;
                        continue;
                    }

                    string limb = DbpuSpriteEditorBridge.MapLimbKey(exp.LimbName ?? "");
                    if (string.IsNullOrEmpty(limb) || string.Equals(limb, "Hair", StringComparison.OrdinalIgnoreCase))
                        continue;
                    string sub = string.Equals(limb, "Head", StringComparison.OrdinalIgnoreCase)
                        ? "head"
                        : Path.Combine("limbs", limb);
                    string limbDir = Path.Combine(CustomTransformationCatalog.EnsureImportFolder(), sub);
                    if (!Directory.Exists(limbDir)) Directory.CreateDirectory(limbDir);
                    string limbPath = Path.Combine(limbDir, stem + "_" + limb.ToLowerInvariant() + ".png");
                    CustomTransformationCatalog.WriteAllBytesShared(limbPath, exp.Png);
                    CustomTransformationCatalog.SetLimbSpritePath(dto, limb, limbPath);
                    any = true;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[DBPU] Persist form sprites failed: " + ex.Message);
                }
            }

            if (!any) return;
            CustomTransformationCatalog.UpsertForm(identity.CharacterId, identity.DisplayName, dto);
            var liveCt = root.GetComponent<CharacterTransformations>();
            if (liveCt?.transformations != null)
            {
                var stats = CustomTransformationCatalog.ToStats(dto, root);
                if (stats != null && !string.IsNullOrEmpty(stats.ID))
                {
                    if (liveCt.transformations.TransformationData.ContainsKey(stats.ID))
                        liveCt.transformations.TransformationData[stats.ID] = stats;
                    else
                        liveCt.transformations.AddTransformation(stats.ID, stats);
                }
            }
            CustomTransformationCatalog.ApplyLimbSpritePaths(person, dto);
            if (!string.IsNullOrEmpty(dto.HairSpritePath) && head != null)
            {
                Sprite hairSrc = CustomTransformationCatalog.ResolveSpriteReference(dto.HairSpritePath, root, true);
                if (hairSrc != null)
                {
                    TmHairApply.ApplyHair(
                        head,
                        hairSrc,
                        new Color(dto.HairColorR, dto.HairColorG, dto.HairColorB, 1f),
                        new Vector3(dto.HairLocalX, dto.HairLocalY, dto.HairLocalZ),
                        dto.UseHairLocalPosition);
                }
            }
            var tint = root.GetComponent<CustomFormHairTintBehaviour>();
            if (tint == null) tint = root.AddComponent<CustomFormHairTintBehaviour>();
            tint.CharacterId = identity.CharacterId;
            tint.NotifyFormApplied(formId);
            ModAPI.Notify("Sprite edits saved to form \"" + (dto.Name ?? formId) + "\".");
        }

        static void SaveDraft()
        {
            if (_draft == null || string.IsNullOrEmpty(_characterId)) return;
            if (string.IsNullOrEmpty(_draft.ID))
                _draft.ID = "Custom_" + DateTime.Now.Ticks.ToString("x");
            if (string.IsNullOrEmpty(_draft.Name))
                _draft.Name = _draft.ID;
            _draft.HairSpritePath = _selectedHairPath ?? _draft.HairSpritePath;
            _draft.HeadSpritePath = _selectedHeadPath ?? _draft.HeadSpritePath;
            CustomTransformationCatalog.EnsureLimbSpritePaths(_draft);
            if (!string.IsNullOrEmpty(_draft.HeadSpritePath))
                CustomTransformationCatalog.SetLimbSpritePath(_draft, "Head", _draft.HeadSpritePath);
            if (!string.IsNullOrEmpty(_draft.HairSpritePath))
                CustomTransformationCatalog.SetLimbSpritePath(_draft, "Hair", _draft.HairSpritePath);

            Sprite hairSpr = ResolveSelectedSprite(_draft.HairSpritePath, _hairList);
            Sprite headSpr = ResolveSelectedSprite(_draft.HeadSpritePath, _headList);
            string stem = (_draft.ID ?? "form") + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            _draft.HairSpritePath = CustomTransformationCatalog.EnsurePersistedSpritePath(
                _draft.HairSpritePath, hairSpr, "hair", stem + "_hair");
            _draft.HeadSpritePath = CustomTransformationCatalog.EnsurePersistedSpritePath(
                _draft.HeadSpritePath, headSpr, "head", stem + "_head");
            _selectedHairPath = _draft.HairSpritePath;
            _selectedHeadPath = _draft.HeadSpritePath;
            CustomTransformationCatalog.SetLimbSpritePath(_draft, "Hair", _draft.HairSpritePath);
            CustomTransformationCatalog.SetLimbSpritePath(_draft, "Head", _draft.HeadSpritePath);

            if (_draft.LimbSpritePaths != null)
            {
                var keys = new List<string>(_draft.LimbSpritePaths.Keys);
                for (int i = 0; i < keys.Count; i++)
                {
                    string key = keys[i];
                    if (string.Equals(key, "Hair", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(key, "Head", StringComparison.OrdinalIgnoreCase))
                        continue;
                    string path = _draft.LimbSpritePaths[key];
                    Sprite spr = CustomTransformationCatalog.LoadSpriteFlexible(path);
                    string next = CustomTransformationCatalog.EnsurePersistedSpritePath(
                        path, spr, Path.Combine("limbs", key), stem + "_" + key.ToLowerInvariant());
                    if (!string.IsNullOrEmpty(next))
                        CustomTransformationCatalog.SetLimbSpritePath(_draft, key, next);
                }
            }

            _draft.HairColorR = _hairTint.r;
            _draft.HairColorG = _hairTint.g;
            _draft.HairColorB = _hairTint.b;
            _draft.AuraColorR = _auraPrimary.r;
            _draft.AuraColorG = _auraPrimary.g;
            _draft.AuraColorB = _auraPrimary.b;
            _draft.AuraColor2R = _auraSecondary.r;
            _draft.AuraColor2G = _auraSecondary.g;
            _draft.AuraColor2B = _auraSecondary.b;
            _draft.IsCustom = true;
            CustomTransformationCatalog.UpsertForm(_characterId, _displayName, _draft);
            ApplyDraftToTarget();
            DialogBoxManager.Notification("Saved transformation \"" + _draft.Name + "\".");
            _creatingNew = false;
            _formId = _draft.ID;
            _screen = ScreenKind.Forms;
            RebuildBody();
        }
    }
}
