using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Events;

namespace DBPU.LiveSpriteEditor
{
    public static class LiveSpriteEditorEntry
    {
        private static bool installed;
        private static GameObject bootstrapObject;

        public static void Main()
        {
            Install();
        }

        public static void OnLoad()
        {
            Install();
        }

        public static void EnsureInstalled()
        {
            Install();
        }

        public static void OnUnload()
        {
            if (!installed)
            {
                return;
            }

            installed = false;

            if (bootstrapObject != null)
            {
                UnityEngine.Object.Destroy(bootstrapObject);
                bootstrapObject = null;
            }
        }

        private static void Install()
        {
            if (installed)
            {
                return;
            }

            installed = true;
            DbpuHarmonyBootstrap.EnsureApplied();

            bootstrapObject = new GameObject("DbpuLiveSpriteEditorBootstrap");
            bootstrapObject.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(bootstrapObject);
            bootstrapObject.AddComponent<SpriteEditorBootstrap>();
            bootstrapObject.AddComponent<SpriteEditorRuntime>();

            Debug.Log("[DB Singularity] Live Sprite Editor loaded.");
        }
    }

    public sealed class SpriteEditorBootstrap : MonoBehaviour
    {
        private const float SpawnAttachRetryIntervalSeconds = 0.25f;
        private const float SpawnAttachRetryTimeoutSeconds = 20f;
        private const float FallbackScanIntervalSeconds = 0.35f;

        private float nextFallbackScanTime;

        private void OnEnable()
        {
            ModAPI.OnItemSpawned += OnItemSpawned;
            AttachToExistingPeople();
            AttachToExistingObjects();
        }

        private void OnDisable()
        {
            ModAPI.OnItemSpawned -= OnItemSpawned;
            StopAllCoroutines();
        }

        private void OnItemSpawned(object sender, UserSpawnEventArgs args)
        {
            if (args == null || args.Instance == null)
            {
                return;
            }

            TryAttachSpawn(args.Instance);
            StartCoroutine(AttachSpawnWhenReady(args.Instance));
        }

        private void Update()
        {
            if (Time.unscaledTime < nextFallbackScanTime)
            {
                return;
            }

            nextFallbackScanTime = Time.unscaledTime + FallbackScanIntervalSeconds;
            AttachToExistingPeople();
            AttachToExistingObjects();
        }

        private IEnumerator AttachSpawnWhenReady(GameObject instance)
        {
            float stopTime = Time.unscaledTime + SpawnAttachRetryTimeoutSeconds;
            int previousCount = -1;
            int stableChecks = 0;

            while (instance != null && Time.unscaledTime <= stopTime)
            {
                int count = TryAttachSpawn(instance);
                if (count > 0 && count == previousCount)
                {
                    stableChecks++;
                    if (stableChecks >= 4)
                    {
                        yield break;
                    }
                }
                else
                {
                    stableChecks = 0;
                    previousCount = count;
                }

                yield return new WaitForSecondsRealtime(SpawnAttachRetryIntervalSeconds);
            }
        }

        private static int TryAttachSpawn(GameObject instance)
        {
            if (instance == null)
            {
                return 0;
            }

            int count = 0;

            PersonBehaviour directPerson = instance.GetComponent<PersonBehaviour>();
            if (TryAttach(directPerson))
            {
                count++;
            }

            PersonBehaviour[] people = instance.GetComponentsInChildren<PersonBehaviour>(true);
            for (int i = 0; i < people.Length; i++)
            {
                if (people[i] != directPerson && TryAttach(people[i]))
                {
                    count++;
                }
            }

            PhysicalBehaviour[] objects = instance.GetComponentsInChildren<PhysicalBehaviour>(true);
            for (int i = 0; i < objects.Length; i++)
            {
                if (TryAttach(objects[i]))
                {
                    count++;
                }
            }

            return count;
        }

        private void AttachToExistingPeople()
        {
            PersonBehaviour[] people = UnityEngine.Object.FindObjectsOfType<PersonBehaviour>();

            for (int i = 0; i < people.Length; i++)
            {
                TryAttach(people[i]);
            }
        }

        private void AttachToExistingObjects()
        {
            PhysicalBehaviour[] objects = UnityEngine.Object.FindObjectsOfType<PhysicalBehaviour>();

            for (int i = 0; i < objects.Length; i++)
            {
                TryAttach(objects[i]);
            }
        }

        private static bool TryAttach(PersonBehaviour person)
        {
            if (!IsLivePerson(person))
            {
                return false;
            }

            if (person.GetComponent<CharacterSpriteEditorTarget>() == null)
            {
                person.gameObject.AddComponent<CharacterSpriteEditorTarget>();
            }

            return true;
        }

        private static void TryAttachObjects(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            PhysicalBehaviour[] objects = instance.GetComponentsInChildren<PhysicalBehaviour>(true);
            for (int i = 0; i < objects.Length; i++)
            {
                TryAttach(objects[i]);
            }
        }

        private static bool TryAttach(PhysicalBehaviour physicalBehaviour)
        {
            if (!ObjectSpriteEditorTarget.IsUsableObject(physicalBehaviour))
            {
                return false;
            }

            if (physicalBehaviour.GetComponent<ObjectSpriteEditorTarget>() == null)
            {
                physicalBehaviour.gameObject.AddComponent<ObjectSpriteEditorTarget>();
            }

            return true;
        }

        private static bool IsLivePerson(PersonBehaviour person)
        {
            return person != null &&
                   person.gameObject != null &&
                   person.gameObject.scene.IsValid() &&
                   person.gameObject.scene.isLoaded;
        }
    }

    public class HypermodifiedSerializableBehaviourBase : MonoBehaviour
    {
    }

    [Serializable]
    public sealed class HypermodifiedPersistentSpriteEntry
    {
        public string PartId;
        public string PartLabel;
        public int RectX;
        public int RectY;
        public int RectWidth;
        public int RectHeight;
        public int PivotX;
        public int PivotY;
        public int TextureWidth;
        public int TextureHeight;
        public float PixelsPerUnit;
        public byte[] SkinPng;
        public byte[] FleshPng;
        public byte[] BonePng;
        public byte[] DamagePng;
        public byte[] LayerStackData;
    }

    public sealed class HypermodifiedSpritePersistence : HypermodifiedSerializableBehaviourBase, Messages.IOnBeforeSerialise, Messages.IOnAfterDeserialise
    {
        public const int CurrentVersion = 1;
        public const string CharacterKind = "character";
        public const string ObjectKind = "object";

        public int Version = CurrentVersion;
        public string TargetKind = string.Empty;
        public string[] PartIds = new string[0];
        public string[] PartLabels = new string[0];
        public int[] RectXs = new int[0];
        public int[] RectYs = new int[0];
        public int[] RectWidths = new int[0];
        public int[] RectHeights = new int[0];
        public int[] PivotXs = new int[0];
        public int[] PivotYs = new int[0];
        public int[] TextureWidths = new int[0];
        public int[] TextureHeights = new int[0];
        public float[] PixelsPerUnits = new float[0];
        public string[] SkinPngBase64 = new string[0];
        public string[] FleshPngBase64 = new string[0];
        public string[] BonePngBase64 = new string[0];
        public string[] DamagePngBase64 = new string[0];
        public string[] LayerStackBase64 = new string[0];

        private HypermodifiedPersistentSpriteEntry[] runtimeEntries = new HypermodifiedPersistentSpriteEntry[0];
        private bool restoreStarted;

        public static HypermodifiedSpritePersistence EnsureForCharacter(PersonBehaviour person)
        {
            if (person == null || person.gameObject == null)
            {
                return null;
            }

            HypermodifiedSpritePersistence persistence = person.GetComponent<HypermodifiedSpritePersistence>();
            if (persistence == null)
            {
                persistence = person.gameObject.AddComponent<HypermodifiedSpritePersistence>();
            }

            persistence.Version = CurrentVersion;
            persistence.TargetKind = CharacterKind;
            return persistence;
        }

        public static HypermodifiedSpritePersistence EnsureForObject(PhysicalBehaviour physicalBehaviour)
        {
            if (physicalBehaviour == null || physicalBehaviour.gameObject == null)
            {
                return null;
            }

            HypermodifiedSpritePersistence persistence = physicalBehaviour.GetComponent<HypermodifiedSpritePersistence>();
            if (persistence == null)
            {
                persistence = physicalBehaviour.gameObject.AddComponent<HypermodifiedSpritePersistence>();
            }

            persistence.Version = CurrentVersion;
            persistence.TargetKind = ObjectKind;
            return persistence;
        }

        public static byte[] EncodeTexture(Texture2D texture)
        {
            if (texture == null)
            {
                return null;
            }

            Texture2D readable = LimbSpriteState.CreateReadableTextureCopy(texture);
            if (readable == null)
            {
                return null;
            }

            byte[] bytes = SpritePresetLibraryService.EncodeTextureToPng(readable);
            UnityEngine.Object.Destroy(readable);
            return bytes;
        }

        public static Texture2D LoadTexture(byte[] bytes, string textureName)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return null;
            }

            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.name = string.IsNullOrEmpty(textureName) ? "HypermodifiedPersistentSpriteTexture" : textureName;
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;

            if (!LoadImageBytes(texture, bytes))
            {
                UnityEngine.Object.Destroy(texture);
                return null;
            }

            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            return texture;
        }

        public static Sprite CreateSprite(HypermodifiedPersistentSpriteEntry entry, Texture2D texture, string spriteName)
        {
            if (entry == null || texture == null)
            {
                return null;
            }

            int width = entry.RectWidth > 0 ? entry.RectWidth : texture.width;
            int height = entry.RectHeight > 0 ? entry.RectHeight : texture.height;
            int x = Mathf.Clamp(entry.RectX, 0, Mathf.Max(0, texture.width - 1));
            int y = Mathf.Clamp(entry.RectY, 0, Mathf.Max(0, texture.height - 1));
            width = Mathf.Clamp(width, 1, texture.width - x);
            height = Mathf.Clamp(height, 1, texture.height - y);

            Vector2 pivot = new Vector2(
                width > 0 ? Mathf.Clamp01(entry.PivotX / (float)width) : 0.5f,
                height > 0 ? Mathf.Clamp01(entry.PivotY / (float)height) : 0.5f);

            float pixelsPerUnit = entry.PixelsPerUnit > 0.01f ? entry.PixelsPerUnit : 35f;
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(x, y, width, height),
                pivot,
                pixelsPerUnit,
                0U,
                SpriteMeshType.FullRect);

            if (sprite != null)
            {
                sprite.name = string.IsNullOrEmpty(spriteName) ? "HypermodifiedPersistentSprite" : spriteName;
            }

            return sprite;
        }

        public void OnBeforeSerialise()
        {
            CaptureFromAttachedTarget();
        }

        public void OnAfterDeserialise(List<GameObject> gameObjects)
        {
            BeginRestoreIfNeeded();
        }

        private void Start()
        {
            BeginRestoreIfNeeded();
        }

        private void BeginRestoreIfNeeded()
        {
            if (restoreStarted)
            {
                return;
            }

            runtimeEntries = UnpackEntries();
            if (runtimeEntries == null || runtimeEntries.Length == 0)
            {
                return;
            }

            restoreStarted = true;
            StartCoroutine(ApplyAfterLoad());
        }

        private void CaptureFromAttachedTarget()
        {
            CharacterSpriteEditorTarget characterTarget = GetComponent<CharacterSpriteEditorTarget>();
            HypermodifiedPersistentSpriteEntry[] capturedEntries;
            if (characterTarget != null &&
                characterTarget.TryCapturePersistentEntries(out capturedEntries) &&
                capturedEntries != null &&
                capturedEntries.Length > 0)
            {
                Version = CurrentVersion;
                TargetKind = CharacterKind;
                StoreEntries(capturedEntries);
                return;
            }

            ObjectSpriteEditorTarget objectTarget = GetComponent<ObjectSpriteEditorTarget>();
            if (objectTarget != null &&
                objectTarget.TryCapturePersistentEntries(out capturedEntries) &&
                capturedEntries != null &&
                capturedEntries.Length > 0)
            {
                Version = CurrentVersion;
                TargetKind = ObjectKind;
                StoreEntries(capturedEntries);
            }
        }

        private IEnumerator ApplyAfterLoad()
        {
            for (int i = 0; i < 20; i++)
            {
                if (ApplyToAttachedTarget())
                {
                    yield break;
                }

                yield return null;
            }
        }

        private bool ApplyToAttachedTarget()
        {
            HypermodifiedPersistentSpriteEntry[] entries = GetRuntimeEntries();
            if (entries == null || entries.Length == 0)
            {
                return false;
            }

            string kind = TargetKind ?? string.Empty;
            if (string.Equals(kind, CharacterKind, StringComparison.OrdinalIgnoreCase))
            {
                CharacterSpriteEditorTarget characterTarget = GetComponent<CharacterSpriteEditorTarget>();
                if (characterTarget == null && GetComponent<PersonBehaviour>() != null)
                {
                    characterTarget = gameObject.AddComponent<CharacterSpriteEditorTarget>();
                }

                return characterTarget != null && characterTarget.ApplyPersistentEntries(entries);
            }

            if (string.Equals(kind, ObjectKind, StringComparison.OrdinalIgnoreCase))
            {
                ObjectSpriteEditorTarget objectTarget = GetComponent<ObjectSpriteEditorTarget>();
                PhysicalBehaviour physicalBehaviour = GetComponent<PhysicalBehaviour>();
                if (objectTarget == null && physicalBehaviour != null && ObjectSpriteEditorTarget.IsUsableObject(physicalBehaviour))
                {
                    objectTarget = gameObject.AddComponent<ObjectSpriteEditorTarget>();
                }

                return objectTarget != null && objectTarget.ApplyPersistentEntries(entries);
            }

            return false;
        }

        public void StoreEntries(HypermodifiedPersistentSpriteEntry[] entries)
        {
            runtimeEntries = entries ?? new HypermodifiedPersistentSpriteEntry[0];
            int count = runtimeEntries.Length;

            PartIds = new string[count];
            PartLabels = new string[count];
            RectXs = new int[count];
            RectYs = new int[count];
            RectWidths = new int[count];
            RectHeights = new int[count];
            PivotXs = new int[count];
            PivotYs = new int[count];
            TextureWidths = new int[count];
            TextureHeights = new int[count];
            PixelsPerUnits = new float[count];
            SkinPngBase64 = new string[count];
            FleshPngBase64 = new string[count];
            BonePngBase64 = new string[count];
            DamagePngBase64 = new string[count];
            LayerStackBase64 = new string[count];

            for (int i = 0; i < count; i++)
            {
                HypermodifiedPersistentSpriteEntry entry = runtimeEntries[i] ?? new HypermodifiedPersistentSpriteEntry();
                PartIds[i] = entry.PartId ?? string.Empty;
                PartLabels[i] = entry.PartLabel ?? string.Empty;
                RectXs[i] = entry.RectX;
                RectYs[i] = entry.RectY;
                RectWidths[i] = entry.RectWidth;
                RectHeights[i] = entry.RectHeight;
                PivotXs[i] = entry.PivotX;
                PivotYs[i] = entry.PivotY;
                TextureWidths[i] = entry.TextureWidth;
                TextureHeights[i] = entry.TextureHeight;
                PixelsPerUnits[i] = entry.PixelsPerUnit;
                SkinPngBase64[i] = ToBase64(entry.SkinPng);
                FleshPngBase64[i] = ToBase64(entry.FleshPng);
                BonePngBase64[i] = ToBase64(entry.BonePng);
                DamagePngBase64[i] = ToBase64(entry.DamagePng);
                LayerStackBase64[i] = ToBase64(entry.LayerStackData);
            }
        }

        private HypermodifiedPersistentSpriteEntry[] GetRuntimeEntries()
        {
            if (runtimeEntries != null && runtimeEntries.Length > 0)
            {
                return runtimeEntries;
            }

            runtimeEntries = UnpackEntries();
            return runtimeEntries;
        }

        private HypermodifiedPersistentSpriteEntry[] UnpackEntries()
        {
            int count = PartIds != null ? PartIds.Length : 0;
            if (count <= 0)
            {
                return new HypermodifiedPersistentSpriteEntry[0];
            }

            List<HypermodifiedPersistentSpriteEntry> entries = new List<HypermodifiedPersistentSpriteEntry>();
            for (int i = 0; i < count; i++)
            {
                byte[] skinPng = FromBase64(GetString(SkinPngBase64, i));
                if (skinPng == null || skinPng.Length == 0)
                {
                    continue;
                }

                entries.Add(new HypermodifiedPersistentSpriteEntry
                {
                    PartId = GetString(PartIds, i),
                    PartLabel = GetString(PartLabels, i),
                    RectX = GetInt(RectXs, i),
                    RectY = GetInt(RectYs, i),
                    RectWidth = GetInt(RectWidths, i),
                    RectHeight = GetInt(RectHeights, i),
                    PivotX = GetInt(PivotXs, i),
                    PivotY = GetInt(PivotYs, i),
                    TextureWidth = GetInt(TextureWidths, i),
                    TextureHeight = GetInt(TextureHeights, i),
                    PixelsPerUnit = GetFloat(PixelsPerUnits, i),
                    SkinPng = skinPng,
                    FleshPng = FromBase64(GetString(FleshPngBase64, i)),
                    BonePng = FromBase64(GetString(BonePngBase64, i)),
                    DamagePng = FromBase64(GetString(DamagePngBase64, i)),
                    LayerStackData = FromBase64(GetString(LayerStackBase64, i))
                });
            }

            return entries.ToArray();
        }

        public static byte[] EncodeLayerStack(LimbSpriteState.LayerStackSnapshot stack)
        {
            if (stack == null || stack.Layers == null || stack.Layers.Length <= 0)
            {
                return null;
            }

            try
            {
                using (MemoryStream stream = new MemoryStream())
                {
                    using (BinaryWriter writer = new BinaryWriter(stream))
                    {
                        writer.Write("HMLS");
                        writer.Write(1);
                        writer.Write(stack.ActiveLayerIndex);
                        writer.Write(stack.PasteLayerSerial);
                        writer.Write(stack.Layers.Length);

                        for (int i = 0; i < stack.Layers.Length; i++)
                        {
                            LimbSpriteState.LayerSnapshot layer = stack.Layers[i] ?? new LimbSpriteState.LayerSnapshot();
                            Color32[] pixels = layer.Pixels ?? new Color32[0];
                            writer.Write(layer.Name ?? string.Empty);
                            writer.Write(layer.Visible);
                            writer.Write(pixels.Length);

                            for (int p = 0; p < pixels.Length; p++)
                            {
                                Color32 pixel = pixels[p];
                                writer.Write(pixel.r);
                                writer.Write(pixel.g);
                                writer.Write(pixel.b);
                                writer.Write(pixel.a);
                            }
                        }
                    }

                    return stream.ToArray();
                }
            }
            catch
            {
                return null;
            }
        }

        public static LimbSpriteState.LayerStackSnapshot DecodeLayerStack(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return null;
            }

            try
            {
                using (MemoryStream stream = new MemoryStream(bytes))
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    string magic = reader.ReadString();
                    int version = reader.ReadInt32();
                    if (!string.Equals(magic, "HMLS", StringComparison.Ordinal) || version != 1)
                    {
                        return null;
                    }

                    LimbSpriteState.LayerStackSnapshot stack = new LimbSpriteState.LayerStackSnapshot();
                    stack.ActiveLayerIndex = reader.ReadInt32();
                    stack.PasteLayerSerial = reader.ReadInt32();

                    int count = reader.ReadInt32();
                    if (count <= 0 || count > 128)
                    {
                        return null;
                    }

                    stack.Layers = new LimbSpriteState.LayerSnapshot[count];
                    for (int i = 0; i < count; i++)
                    {
                        LimbSpriteState.LayerSnapshot layer = new LimbSpriteState.LayerSnapshot();
                        layer.Name = reader.ReadString();
                        layer.Visible = reader.ReadBoolean();

                        int pixelCount = reader.ReadInt32();
                        if (pixelCount < 0 || pixelCount > 4194304)
                        {
                            return null;
                        }

                        layer.Pixels = new Color32[pixelCount];
                        for (int p = 0; p < pixelCount; p++)
                        {
                            layer.Pixels[p] = new Color32(
                                reader.ReadByte(),
                                reader.ReadByte(),
                                reader.ReadByte(),
                                reader.ReadByte());
                        }

                        stack.Layers[i] = layer;
                    }

                    return stack;
                }
            }
            catch
            {
                return null;
            }
        }

        private static string ToBase64(byte[] bytes)
        {
            return bytes == null || bytes.Length == 0 ? string.Empty : Convert.ToBase64String(bytes);
        }

        private static byte[] FromBase64(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            try
            {
                return Convert.FromBase64String(value);
            }
            catch
            {
                return null;
            }
        }

        private static string GetString(string[] values, int index)
        {
            return values != null && index >= 0 && index < values.Length ? values[index] : string.Empty;
        }

        private static int GetInt(int[] values, int index)
        {
            return values != null && index >= 0 && index < values.Length ? values[index] : 0;
        }

        private static float GetFloat(float[] values, int index)
        {
            return values != null && index >= 0 && index < values.Length ? values[index] : 0f;
        }

        private static bool LoadImageBytes(Texture2D texture, byte[] bytes)
        {
            if (texture == null || bytes == null)
            {
                return false;
            }

            Type imageConversionType = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
            if (imageConversionType == null)
            {
                return false;
            }

            System.Reflection.MethodInfo method = imageConversionType.GetMethod(
                "LoadImage",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                null,
                new Type[] { typeof(Texture2D), typeof(byte[]) },
                null);

            if (method != null)
            {
                object result = method.Invoke(null, new object[] { texture, bytes });
                return result is bool && (bool)result;
            }

            method = imageConversionType.GetMethod(
                "LoadImage",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                null,
                new Type[] { typeof(Texture2D), typeof(byte[]), typeof(bool) },
                null);

            if (method == null)
            {
                return false;
            }

            object preserveResult = method.Invoke(null, new object[] { texture, bytes, false });
            return preserveResult is bool && (bool)preserveResult;
        }
    }

    public sealed class ObjectSpriteEditorTarget : MonoBehaviour
    {
        private const float AttachRetryIntervalSeconds = 0.25f;
        private const float AttachRetryTimeoutSeconds = 20f;
        private const float VisualRefreshIntervalSeconds = 0.5f;
        private const string EditSpriteIdentity = "hypermodified.liveSpriteWorkshop.openObjectEditor";
        private const string EditSpriteLabel = "Edit Sprite";

        private readonly Dictionary<SpriteRenderer, List<CommittedObjectVisual>> committedVisualsByRenderer = new Dictionary<SpriteRenderer, List<CommittedObjectVisual>>();
        private readonly Dictionary<SpriteRenderer, LimbSpriteState> statesByRenderer = new Dictionary<SpriteRenderer, LimbSpriteState>();
        private readonly Dictionary<int, SpriteStateKey> spriteStateCache = new Dictionary<int, SpriteStateKey>();

        private PhysicalBehaviour physicalBehaviour;
        private SpriteRenderer selectedRenderer;
        private Coroutine startupMaintenanceRoutine;
        private float nextVisualRefreshTime;

        public PhysicalBehaviour PhysicalBehaviour
        {
            get { return physicalBehaviour; }
        }

        public SpriteRenderer SelectedRenderer
        {
            get { return GetPreferredRenderer(selectedRenderer); }
        }

        private void Awake()
        {
            physicalBehaviour = GetComponent<PhysicalBehaviour>();
            selectedRenderer = GetPreferredRenderer(null);
            AttachButton();
            StartStartupMaintenance();
        }

        private void OnEnable()
        {
            physicalBehaviour = GetComponent<PhysicalBehaviour>();
            selectedRenderer = GetPreferredRenderer(selectedRenderer);
            AttachButton();
            StartStartupMaintenance();
        }

        private void StartStartupMaintenance()
        {
            if (startupMaintenanceRoutine != null || !isActiveAndEnabled)
            {
                return;
            }

            startupMaintenanceRoutine = StartCoroutine(StartupMaintenance());
        }

        private IEnumerator StartupMaintenance()
        {
            float stopTime = Time.unscaledTime + AttachRetryTimeoutSeconds;
            bool visualsApplied = false;
            while (Time.unscaledTime <= stopTime)
            {
                bool buttonAttached = AttachButton();
                if (!visualsApplied)
                {
                    ApplyCommittedVisuals();
                    visualsApplied = true;
                }

                if (buttonAttached)
                {
                    break;
                }

                yield return new WaitForSecondsRealtime(AttachRetryIntervalSeconds);
            }

            startupMaintenanceRoutine = null;
        }

        private void OnDisable()
        {
            if (startupMaintenanceRoutine != null)
            {
                StopCoroutine(startupMaintenanceRoutine);
                startupMaintenanceRoutine = null;
            }
        }

        private void Update()
        {
            if (committedVisualsByRenderer.Count <= 0 || Time.unscaledTime < nextVisualRefreshTime)
            {
                return;
            }

            nextVisualRefreshTime = Time.unscaledTime + VisualRefreshIntervalSeconds;
            ApplyCommittedVisuals();
        }

        private void OnDestroy()
        {
            ClearStates();

            foreach (KeyValuePair<SpriteRenderer, List<CommittedObjectVisual>> entry in committedVisualsByRenderer)
            {
                if (entry.Value == null)
                {
                    continue;
                }

                for (int i = 0; i < entry.Value.Count; i++)
                {
                    DestroyCommittedObjectVisual(entry.Value[i]);
                }
            }

            committedVisualsByRenderer.Clear();
            statesByRenderer.Clear();
            spriteStateCache.Clear();
        }

        public string GetObjectName()
        {
            return CleanName(physicalBehaviour != null ? physicalBehaviour.name : "Object");
        }

        public string GetPartId()
        {
            return GetPartId(SelectedRenderer);
        }

        public string GetPartId(SpriteRenderer renderer)
        {
            SpriteRenderer primary = physicalBehaviour != null ? physicalBehaviour.spriteRenderer : null;
            if (renderer == null || renderer == primary)
            {
                return "object";
            }

            return GetRendererPath(renderer);
        }

        public string GetPartLabel()
        {
            return GetPartLabel(SelectedRenderer);
        }

        public string GetPartLabel(SpriteRenderer renderer)
        {
            if (renderer == null)
            {
                return GetObjectName();
            }

            SpriteRenderer primary = physicalBehaviour != null ? physicalBehaviour.spriteRenderer : null;
            if (renderer == primary)
            {
                return GetObjectName();
            }

            string label = CleanName(renderer.gameObject != null ? renderer.gameObject.name : renderer.name);
            return string.IsNullOrWhiteSpace(label) ? "Sprite Part" : label;
        }

        public List<SpriteRenderer> GetEditableRenderers()
        {
            List<SpriteRenderer> result = new List<SpriteRenderer>();
            if (!IsUsableObject(physicalBehaviour))
            {
                return result;
            }

            SpriteRenderer primary = physicalBehaviour.spriteRenderer;
            if (IsUsableRenderer(primary))
            {
                result.Add(primary);
            }

            SpriteRenderer[] renderers = physicalBehaviour.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (!IsUsableRenderer(renderer) || renderer == primary || result.Contains(renderer))
                {
                    continue;
                }

                if (renderer.GetComponentInParent<PersonBehaviour>() != null || renderer.GetComponentInParent<LimbBehaviour>() != null)
                {
                    continue;
                }

                result.Add(renderer);
            }

            return result;
        }

        public void SelectRenderer(SpriteRenderer renderer)
        {
            selectedRenderer = GetPreferredRenderer(renderer);
        }

        private SpriteRenderer GetPreferredRenderer(SpriteRenderer renderer)
        {
            if (IsUsableRendererForThisObject(renderer))
            {
                return renderer;
            }

            if (physicalBehaviour != null && IsUsableRenderer(physicalBehaviour.spriteRenderer))
            {
                return physicalBehaviour.spriteRenderer;
            }

            List<SpriteRenderer> renderers = GetEditableRenderers();
            return renderers.Count > 0 ? renderers[0] : null;
        }

        private bool IsUsableRendererForThisObject(SpriteRenderer renderer)
        {
            if (!IsUsableRenderer(renderer) || physicalBehaviour == null)
            {
                return false;
            }

            return renderer.transform == physicalBehaviour.transform || renderer.transform.IsChildOf(physicalBehaviour.transform);
        }

        public bool TryCapturePersistentEntries(out HypermodifiedPersistentSpriteEntry[] entries)
        {
            List<HypermodifiedPersistentSpriteEntry> captured = new List<HypermodifiedPersistentSpriteEntry>();

            foreach (KeyValuePair<SpriteRenderer, List<CommittedObjectVisual>> item in committedVisualsByRenderer)
            {
                SpriteRenderer renderer = item.Key;
                if (!IsUsableRendererForThisObject(renderer) || item.Value == null || item.Value.Count <= 0)
                {
                    continue;
                }

                CommittedObjectVisual visual = FindCommittedVisual(renderer, GetDisplayedSprite(renderer));
                if (visual == null)
                {
                    visual = item.Value[item.Value.Count - 1];
                }

                HypermodifiedPersistentSpriteEntry entry = CreatePersistentEntry(renderer, visual);
                if (entry != null)
                {
                    captured.Add(entry);
                }
            }

            entries = captured.ToArray();
            return entries.Length > 0;
        }

        public bool ApplyPersistentEntries(HypermodifiedPersistentSpriteEntry[] entries)
        {
            if (!IsUsableObject(physicalBehaviour) || entries == null)
            {
                return false;
            }

            bool appliedAny = false;
            for (int i = 0; i < entries.Length; i++)
            {
                HypermodifiedPersistentSpriteEntry entry = entries[i];
                if (entry == null)
                {
                    continue;
                }

                SpriteRenderer renderer = FindRendererByPartId(entry.PartId);
                if (renderer == null)
                {
                    renderer = FindRendererByLabel(entry.PartLabel);
                }

                if (renderer != null && ApplyPersistentEntry(renderer, entry))
                {
                    appliedAny = true;
                }
            }

            return appliedAny;
        }

        public LimbSpriteState GetState()
        {
            if (!IsUsableObject(physicalBehaviour))
            {
                return null;
            }

            SpriteRenderer renderer = SelectedRenderer;
            if (!IsUsableRendererForThisObject(renderer))
            {
                return null;
            }

            ApplyCommittedVisual(renderer);
            Sprite displayedSprite = GetDisplayedSprite(renderer);

            LimbSpriteState state;
            if (statesByRenderer.TryGetValue(renderer, out state) && state != null)
            {
                if (IsReusableState(state, displayedSprite))
                {
                    return state;
                }

                state.Dispose();
                statesByRenderer.Remove(renderer);
            }

            Sprite currentSprite = displayedSprite;
            CommittedObjectVisual committedVisual = FindCommittedVisual(renderer, currentSprite);

            if (committedVisual != null && (committedVisual.BaseSprite != null || committedVisual.BasePixels != null))
            {
                state = new LimbSpriteState(renderer, committedVisual.BaseSprite, committedVisual.BasePixels, currentSprite);
                state.RestoreLayerStackSnapshot(committedVisual.LayerStack);
            }
            else if (renderer == physicalBehaviour.spriteRenderer)
            {
                state = new LimbSpriteState(physicalBehaviour);
            }
            else
            {
                state = new LimbSpriteState(renderer);
            }

            if (state == null || !state.IsValid)
            {
                if (state != null)
                {
                    state.Dispose();
                }

                return null;
            }

            statesByRenderer[renderer] = state;
            return state;
        }

        public bool TryGetExistingState(out LimbSpriteState existingState)
        {
            SpriteRenderer renderer = SelectedRenderer;
            LimbSpriteState state;
            if (renderer != null && statesByRenderer.TryGetValue(renderer, out state) && state != null)
            {
                Sprite displayedSprite = GetDisplayedSprite(renderer);
                if (IsReusableState(state, displayedSprite))
                {
                    existingState = state;
                    return true;
                }

                state.Dispose();
                statesByRenderer.Remove(renderer);
            }

            existingState = null;
            return false;
        }

        private static bool IsReusableState(LimbSpriteState state, Sprite displayedSprite)
        {
            return state != null &&
                   state.IsValid &&
                   (state.HasChanges || state.HasLayerStack || state.MatchesDisplayedSprite(displayedSprite));
        }

        public void ResetState()
        {
            if (!IsUsableObject(physicalBehaviour))
            {
                return;
            }

            SpriteRenderer renderer = SelectedRenderer;
            if (!IsUsableRendererForThisObject(renderer))
            {
                return;
            }

            LimbSpriteState existingState;
            if (TryGetExistingState(out existingState))
            {
                if (existingState.CanResetToOriginal)
                {
                    existingState.ResetToOriginal();
                }

                return;
            }

            CommittedObjectVisual committedVisual = FindCommittedVisual(renderer, GetDisplayedSprite(renderer));
            if (committedVisual == null || (committedVisual.BaseSprite == null && committedVisual.BasePixels == null))
            {
                return;
            }

            LimbSpriteState state = renderer == physicalBehaviour.spriteRenderer
                ? new LimbSpriteState(physicalBehaviour, committedVisual.BaseSprite, committedVisual.BasePixels, GetDisplayedSprite(renderer))
                : new LimbSpriteState(renderer, committedVisual.BaseSprite, committedVisual.BasePixels, GetDisplayedSprite(renderer));
            if (state == null || !state.IsValid)
            {
                if (state != null)
                {
                    state.Dispose();
                }

                return;
            }

            statesByRenderer[renderer] = state;
            state.ResetToOriginal();
            state.FinalizeForWorldDisplay();
        }

        public bool HasResettableChanges()
        {
            foreach (KeyValuePair<SpriteRenderer, LimbSpriteState> item in statesByRenderer)
            {
                if (item.Value != null && item.Value.IsValid && item.Value.CanResetToOriginal)
                {
                    return true;
                }
            }

            if (!IsUsableObject(physicalBehaviour))
            {
                return false;
            }

            foreach (KeyValuePair<SpriteRenderer, List<CommittedObjectVisual>> item in committedVisualsByRenderer)
            {
                CommittedObjectVisual committedVisual = FindCommittedVisual(item.Key, GetDisplayedSprite(item.Key));
                if (committedVisual != null && (committedVisual.BaseSprite != null || committedVisual.BasePixels != null))
                {
                    return true;
                }
            }

            return false;
        }

        public void ApplyPendingHitboxChanges()
        {
            bool appliedHitbox = false;
            bool committedVisual = false;

            List<SpriteRenderer> renderers = new List<SpriteRenderer>(statesByRenderer.Keys);
            for (int i = 0; i < renderers.Count; i++)
            {
                SpriteRenderer renderer = renderers[i];
                LimbSpriteState state;
                if (!statesByRenderer.TryGetValue(renderer, out state) || state == null || !state.IsValid)
                {
                    continue;
                }

                state.FinalizeForWorldDisplay();

                if (renderer == physicalBehaviour.spriteRenderer && state.HasPendingHitboxChanges())
                {
                    appliedHitbox |= state.ApplyPendingHitboxChanges();
                }

                if (state.HasChanges || state.HasLayerStack)
                {
                    bool committed = CommitEditedObjectVisual(renderer, state);
                    committedVisual |= committed;
                    if (committed)
                    {
                        state.AcceptCurrentAsSessionBase();
                    }
                }
            }

            if (appliedHitbox)
            {
                Physics2D.SyncTransforms();
            }

        }

        public void FinalizeStateForWorldDisplay()
        {
            foreach (KeyValuePair<SpriteRenderer, LimbSpriteState> item in statesByRenderer)
            {
                if (item.Value != null && item.Value.IsValid)
                {
                    item.Value.FinalizeForWorldDisplay();
                }
            }
        }

        public void OpenEditor()
        {
            SpriteEditorRuntime.Open(this);
        }

        private void ClearStates()
        {
            foreach (KeyValuePair<SpriteRenderer, LimbSpriteState> item in statesByRenderer)
            {
                if (item.Value != null)
                {
                    item.Value.Dispose();
                }
            }

            statesByRenderer.Clear();
        }

        private bool CommitEditedObjectVisual(SpriteRenderer renderer, LimbSpriteState spriteState)
        {
            if (!IsUsableObject(physicalBehaviour) || !IsUsableRendererForThisObject(renderer) || spriteState == null || !spriteState.IsValid)
            {
                return false;
            }

            Sprite sourceSprite = GetDisplayedSprite(renderer);
            if (sourceSprite == null)
            {
                return false;
            }

            CommittedObjectVisual existingVisual = FindCommittedVisual(renderer, sourceSprite);
            Sprite baseSprite = spriteState.ResetSprite != null ? spriteState.ResetSprite : sourceSprite;
            SpriteStateKey baseStateKey = existingVisual != null && existingVisual.BaseStateKey.IsValid
                ? existingVisual.BaseStateKey
                : GetSpriteStateKey(baseSprite);

            Texture2D displayTexture;
            Sprite displaySprite;
            if (!spriteState.TryCreateCommittedDisplay(out displayTexture, out displaySprite))
            {
                return false;
            }

            if (renderer == null)
            {
                if (displaySprite != null)
                {
                    UnityEngine.Object.Destroy(displaySprite);
                }

                if (displayTexture != null)
                {
                    UnityEngine.Object.Destroy(displayTexture);
                }

                return false;
            }

            renderer.sprite = displaySprite;
            if (renderer == physicalBehaviour.spriteRenderer)
            {
                physicalBehaviour.RefreshOutline();
            }

            if (existingVisual != null)
            {
                if (existingVisual.DisplaySprite != null && existingVisual.DisplaySprite != displaySprite)
                {
                    UnityEngine.Object.Destroy(existingVisual.DisplaySprite);
                }

                if (existingVisual.DisplayTexture != null && existingVisual.DisplayTexture != displayTexture)
                {
                    UnityEngine.Object.Destroy(existingVisual.DisplayTexture);
                }
            }

            CommittedObjectVisual visual = existingVisual ?? new CommittedObjectVisual();
            visual.BaseStateKey = baseStateKey;
            visual.CommittedStateKey = GetSpriteStateKey(displaySprite);
            visual.BaseSprite = spriteState.ResetSprite != null ? spriteState.ResetSprite : visual.BaseSprite;
            visual.BasePixels = spriteState.ResetPixelsSnapshot ?? visual.BasePixels;
            visual.LayerStack = spriteState.CaptureLayerStackSnapshot();
            visual.DisplayTexture = displayTexture;
            visual.DisplaySprite = displaySprite;

            List<CommittedObjectVisual> committedVisuals = GetCommittedVisuals(renderer, true);
            if (existingVisual == null)
            {
                committedVisuals.Add(visual);
            }

            ApplyCommittedVisual(renderer, visual);
            UpdatePersistencePayload();
            return true;
        }

        private void UpdatePersistencePayload()
        {
            HypermodifiedSpritePersistence persistence = HypermodifiedSpritePersistence.EnsureForObject(physicalBehaviour);
            HypermodifiedPersistentSpriteEntry[] entries;
            if (persistence != null && TryCapturePersistentEntries(out entries))
            {
                persistence.Version = HypermodifiedSpritePersistence.CurrentVersion;
                persistence.TargetKind = HypermodifiedSpritePersistence.ObjectKind;
                persistence.StoreEntries(entries);
            }
        }

        private HypermodifiedPersistentSpriteEntry CreatePersistentEntry(SpriteRenderer renderer, CommittedObjectVisual visual)
        {
            if (visual == null || visual.DisplayTexture == null || visual.DisplaySprite == null)
            {
                return null;
            }

            byte[] skinPng = HypermodifiedSpritePersistence.EncodeTexture(visual.DisplayTexture);
            if (skinPng == null || skinPng.Length == 0)
            {
                return null;
            }

            Rect rect = visual.DisplaySprite.rect;
            return new HypermodifiedPersistentSpriteEntry
            {
                PartId = GetPartId(renderer),
                PartLabel = GetPartLabel(renderer),
                RectX = Mathf.RoundToInt(rect.x),
                RectY = Mathf.RoundToInt(rect.y),
                RectWidth = Mathf.RoundToInt(rect.width),
                RectHeight = Mathf.RoundToInt(rect.height),
                PivotX = Mathf.RoundToInt(visual.DisplaySprite.pivot.x),
                PivotY = Mathf.RoundToInt(visual.DisplaySprite.pivot.y),
                TextureWidth = visual.DisplayTexture.width,
                TextureHeight = visual.DisplayTexture.height,
                PixelsPerUnit = visual.DisplaySprite.pixelsPerUnit,
                SkinPng = skinPng,
                LayerStackData = HypermodifiedSpritePersistence.EncodeLayerStack(visual.LayerStack)
            };
        }

        private bool ApplyPersistentEntry(SpriteRenderer renderer, HypermodifiedPersistentSpriteEntry entry)
        {
            if (!IsUsableRendererForThisObject(renderer))
            {
                return false;
            }

            Texture2D displayTexture = HypermodifiedSpritePersistence.LoadTexture(entry.SkinPng, GetObjectName() + "_" + GetPartLabel(renderer) + "_PersistentSpriteTexture");
            if (displayTexture == null)
            {
                return false;
            }

            Sprite displaySprite = HypermodifiedSpritePersistence.CreateSprite(entry, displayTexture, GetObjectName() + "_" + GetPartLabel(renderer) + "_PersistentSprite");
            if (displaySprite == null)
            {
                UnityEngine.Object.Destroy(displayTexture);
                return false;
            }

            Sprite baseSprite = GetDisplayedSprite(renderer);
            List<CommittedObjectVisual> committedVisuals = GetCommittedVisuals(renderer, true);
            for (int i = 0; i < committedVisuals.Count; i++)
            {
                DestroyCommittedObjectVisual(committedVisuals[i]);
            }

            committedVisuals.Clear();
            spriteStateCache.Clear();
            LimbSpriteState existingState;
            if (statesByRenderer.TryGetValue(renderer, out existingState) && existingState != null)
            {
                existingState.Dispose();
                statesByRenderer.Remove(renderer);
            }

            CommittedObjectVisual visual = new CommittedObjectVisual();
            visual.BaseStateKey = GetSpriteStateKey(baseSprite);
            visual.CommittedStateKey = GetSpriteStateKey(displaySprite);
            visual.BaseSprite = baseSprite;
            visual.LayerStack = HypermodifiedSpritePersistence.DecodeLayerStack(entry.LayerStackData);
            visual.DisplayTexture = displayTexture;
            visual.DisplaySprite = displaySprite;
            committedVisuals.Add(visual);

            ApplyCommittedVisual(renderer, visual);
            RestoreObjectHitboxState(renderer, visual);
            return true;
        }

        private void RestoreObjectHitboxState(SpriteRenderer renderer, CommittedObjectVisual visual)
        {
            if (!IsUsableRendererForThisObject(renderer) || visual == null || visual.DisplaySprite == null)
            {
                return;
            }

            LimbSpriteState restoredState = renderer == physicalBehaviour.spriteRenderer
                ? new LimbSpriteState(physicalBehaviour, visual.BaseSprite, visual.BasePixels, visual.DisplaySprite)
                : new LimbSpriteState(renderer, visual.BaseSprite, visual.BasePixels, visual.DisplaySprite);

            if (restoredState == null || !restoredState.IsValid)
            {
                if (restoredState != null)
                {
                    restoredState.Dispose();
                }

                return;
            }

            restoredState.RestoreLayerStackSnapshot(visual.LayerStack);

            if (renderer == physicalBehaviour.spriteRenderer)
            {
                restoredState.ForceApplyHitboxChanges();
                ApplyCommittedVisual(renderer, visual);
            }

            restoredState.AcceptCurrentAsSessionBase();
            statesByRenderer[renderer] = restoredState;
        }

        private void ApplyCommittedVisuals()
        {
            foreach (KeyValuePair<SpriteRenderer, List<CommittedObjectVisual>> item in committedVisualsByRenderer)
            {
                ApplyCommittedVisual(item.Key);
            }
        }

        private void ApplyCommittedVisual(SpriteRenderer renderer)
        {
            if (!IsUsableRendererForThisObject(renderer))
            {
                return;
            }

            CommittedObjectVisual visual = FindCommittedVisual(renderer, GetDisplayedSprite(renderer));
            if (visual == null)
            {
                return;
            }

            ApplyCommittedVisual(renderer, visual);
        }

        private void ApplyCommittedVisual(SpriteRenderer renderer, CommittedObjectVisual visual)
        {
            if (!IsUsableRendererForThisObject(renderer) ||
                visual == null ||
                visual.DisplaySprite == null)
            {
                return;
            }

            if (renderer.sprite == visual.DisplaySprite)
            {
                return;
            }

            if (renderer.sprite != visual.DisplaySprite)
            {
                renderer.sprite = visual.DisplaySprite;
            }

            if (renderer == physicalBehaviour.spriteRenderer)
            {
                physicalBehaviour.RefreshOutline();
            }
        }

        private CommittedObjectVisual FindCommittedVisual(SpriteRenderer renderer, Sprite sprite)
        {
            if (sprite == null)
            {
                return null;
            }

            SpriteStateKey currentStateKey = GetSpriteStateKey(sprite);
            if (!currentStateKey.IsValid)
            {
                return null;
            }

            List<CommittedObjectVisual> committedVisuals = GetCommittedVisuals(renderer, false);
            if (committedVisuals == null)
            {
                return null;
            }

            for (int i = 0; i < committedVisuals.Count; i++)
            {
                CommittedObjectVisual visual = committedVisuals[i];
                if (visual != null &&
                    (visual.BaseStateKey.Matches(currentStateKey) || visual.CommittedStateKey.Matches(currentStateKey)))
                {
                    return visual;
                }
            }

            return null;
        }

        private List<CommittedObjectVisual> GetCommittedVisuals(SpriteRenderer renderer, bool create)
        {
            if (renderer == null)
            {
                return null;
            }

            List<CommittedObjectVisual> visuals;
            if (!committedVisualsByRenderer.TryGetValue(renderer, out visuals) || visuals == null)
            {
                if (!create)
                {
                    return null;
                }

                visuals = new List<CommittedObjectVisual>();
                committedVisualsByRenderer[renderer] = visuals;
            }

            return visuals;
        }

        private static void DestroyCommittedObjectVisual(CommittedObjectVisual visual)
        {
            if (visual == null)
            {
                return;
            }

            if (visual.DisplaySprite != null)
            {
                UnityEngine.Object.Destroy(visual.DisplaySprite);
                visual.DisplaySprite = null;
            }

            if (visual.DisplayTexture != null)
            {
                UnityEngine.Object.Destroy(visual.DisplayTexture);
                visual.DisplayTexture = null;
            }
        }

        private SpriteStateKey GetSpriteStateKey(Sprite sprite)
        {
            if (sprite == null)
            {
                return default(SpriteStateKey);
            }

            int instanceId = sprite.GetInstanceID();
            SpriteStateKey cached;
            if (spriteStateCache.TryGetValue(instanceId, out cached) && cached.IsValid)
            {
                return cached;
            }

            SpriteStateKey computed = CreateSpriteStateKey(sprite);
            if (computed.IsValid)
            {
                spriteStateCache[instanceId] = computed;
            }

            return computed;
        }

        private static SpriteStateKey CreateSpriteStateKey(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null)
            {
                return default(SpriteStateKey);
            }

            Texture2D readable = LimbSpriteState.CreateReadableTextureCopy(sprite.texture);
            if (readable == null)
            {
                return default(SpriteStateKey);
            }

            Rect rect = sprite.rect;
            int x = Mathf.RoundToInt(rect.x);
            int y = Mathf.RoundToInt(rect.y);
            int width = Mathf.RoundToInt(rect.width);
            int height = Mathf.RoundToInt(rect.height);
            if (width <= 0 || height <= 0)
            {
                UnityEngine.Object.Destroy(readable);
                return default(SpriteStateKey);
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

            return new SpriteStateKey
            {
                PixelHash = hash,
                RectX = x,
                RectY = y,
                RectWidth = width,
                RectHeight = height,
                PivotX = Mathf.RoundToInt(sprite.pivot.x),
                PivotY = Mathf.RoundToInt(sprite.pivot.y),
                PixelsPerUnitTimesHundred = Mathf.RoundToInt(sprite.pixelsPerUnit * 100f)
            };
        }

        private bool AttachButton()
        {
            return false;
        }

        public static bool IsUsableObject(PhysicalBehaviour physical)
        {
            return physical != null &&
                   physical.gameObject != null &&
                   physical.gameObject.scene.IsValid() &&
                   physical.gameObject.scene.isLoaded &&
                   physical.spriteRenderer != null &&
                   physical.GetComponent<LimbBehaviour>() == null &&
                   physical.GetComponentInParent<PersonBehaviour>() == null &&
                   GetDisplayedSprite(physical) != null;
        }

        public static bool IsUsableRenderer(SpriteRenderer renderer)
        {
            return renderer != null &&
                   renderer.gameObject != null &&
                   renderer.gameObject.scene.IsValid() &&
                   renderer.gameObject.scene.isLoaded &&
                   renderer.sprite != null;
        }

        private static Sprite GetDisplayedSprite(PhysicalBehaviour physical)
        {
            if (physical == null || physical.spriteRenderer == null)
            {
                return null;
            }

            return physical.spriteRenderer.sprite;
        }

        private static Sprite GetDisplayedSprite(SpriteRenderer renderer)
        {
            if (renderer == null)
            {
                return null;
            }

            return renderer.sprite;
        }

        private SpriteRenderer FindRendererByPartId(string partId)
        {
            if (string.IsNullOrWhiteSpace(partId))
            {
                return null;
            }

            List<SpriteRenderer> renderers = GetEditableRenderers();
            for (int i = 0; i < renderers.Count; i++)
            {
                if (string.Equals(GetPartId(renderers[i]), partId, StringComparison.Ordinal))
                {
                    return renderers[i];
                }
            }

            return null;
        }

        private SpriteRenderer FindRendererByLabel(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                return null;
            }

            List<SpriteRenderer> renderers = GetEditableRenderers();
            for (int i = 0; i < renderers.Count; i++)
            {
                if (string.Equals(GetPartLabel(renderers[i]), label, StringComparison.Ordinal))
                {
                    return renderers[i];
                }
            }

            return null;
        }

        private string GetRendererPath(SpriteRenderer renderer)
        {
            if (renderer == null || physicalBehaviour == null)
            {
                return string.Empty;
            }

            Transform root = physicalBehaviour.transform;
            Transform current = renderer.transform;
            List<string> names = new List<string>();
            while (current != null && current != root)
            {
                names.Add(CleanName(current.name));
                current = current.parent;
            }

            names.Reverse();
            return names.Count > 0 ? string.Join("/", names.ToArray()) : "object";
        }

        private static string CleanName(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName))
            {
                return string.Empty;
            }

            return rawName.Replace("(Clone)", string.Empty).Trim();
        }

        private sealed class CommittedObjectVisual
        {
            public SpriteStateKey BaseStateKey;
            public SpriteStateKey CommittedStateKey;
            public Sprite BaseSprite;
            public Color32[] BasePixels;
            public LimbSpriteState.LayerStackSnapshot LayerStack;
            public Texture2D DisplayTexture;
            public Sprite DisplaySprite;
        }

        private struct SpriteStateKey
        {
            public ulong PixelHash;
            public int PivotX;
            public int PivotY;
            public int PixelsPerUnitTimesHundred;
            public int RectHeight;
            public int RectWidth;
            public int RectX;
            public int RectY;

            public bool IsValid
            {
                get { return PixelHash != 0UL && RectWidth > 0 && RectHeight > 0; }
            }

            public bool Matches(SpriteStateKey other)
            {
                return IsValid &&
                       other.IsValid &&
                       PixelHash == other.PixelHash &&
                       RectX == other.RectX &&
                       RectY == other.RectY &&
                       RectWidth == other.RectWidth &&
                       RectHeight == other.RectHeight &&
                       PivotX == other.PivotX &&
                       PivotY == other.PivotY &&
                       PixelsPerUnitTimesHundred == other.PixelsPerUnitTimesHundred;
            }
        }
    }

    public sealed class CharacterSpriteEditorTarget : MonoBehaviour
    {
        public sealed class MakerSpriteExport
        {
            public string LimbName;
            public byte[] Png;
            public Sprite Sprite;
            public bool IsHair;
        }

        private const float AttachRetryIntervalSeconds = 0.25f;
        private const float AttachRetryTimeoutSeconds = 20f;
        private const float VisualRefreshIntervalSeconds = 0.5f;
        private const string EditSpriteIdentity = "hypermodified.liveSpriteWorkshop.openEditor";
        private const string EditSpriteLabel = "Edit Character Sprite";

        private readonly Dictionary<LimbBehaviour, LimbSpriteState> states = new Dictionary<LimbBehaviour, LimbSpriteState>();
        private readonly Dictionary<LimbBehaviour, List<CommittedLimbVisual>> committedVisuals = new Dictionary<LimbBehaviour, List<CommittedLimbVisual>>();
        private readonly Dictionary<int, SpriteStateKey> spriteStateCache = new Dictionary<int, SpriteStateKey>();
        private readonly Dictionary<SpriteRenderer, LimbSpriteState> accessoryStates = new Dictionary<SpriteRenderer, LimbSpriteState>();
        private readonly Dictionary<SpriteRenderer, CommittedAccessoryVisual> committedAccessories = new Dictionary<SpriteRenderer, CommittedAccessoryVisual>();
        private readonly List<MakerSpriteExport> lastMakerExports = new List<MakerSpriteExport>();

        private PersonBehaviour person;
        private SpriteRenderer selectedAccessory;
        private Coroutine startupMaintenanceRoutine;
        private float nextVisualRefreshTime;

        public PersonBehaviour Person
        {
            get { return person; }
        }

        private void Awake()
        {
            person = GetComponent<PersonBehaviour>();
            AttachButtons();
            StartStartupMaintenance();
        }

        private void OnEnable()
        {
            person = GetComponent<PersonBehaviour>();
            AttachButtons();
            StartStartupMaintenance();
        }

        private void StartStartupMaintenance()
        {
            if (startupMaintenanceRoutine != null || !isActiveAndEnabled)
            {
                return;
            }

            startupMaintenanceRoutine = StartCoroutine(StartupMaintenance());
        }

        private IEnumerator StartupMaintenance()
        {
            float stopTime = Time.unscaledTime + AttachRetryTimeoutSeconds;
            bool visualsApplied = false;
            while (Time.unscaledTime <= stopTime)
            {
                bool buttonsAttached = AttachButtons();
                if (!visualsApplied)
                {
                    ApplyCommittedVisuals();
                    visualsApplied = true;
                }

                if (buttonsAttached)
                {
                    break;
                }

                yield return new WaitForSecondsRealtime(AttachRetryIntervalSeconds);
            }

            startupMaintenanceRoutine = null;
        }

        private void OnDisable()
        {
            if (startupMaintenanceRoutine != null)
            {
                StopCoroutine(startupMaintenanceRoutine);
                startupMaintenanceRoutine = null;
            }
        }

        private void Update()
        {
            if (committedVisuals.Count <= 0 || Time.unscaledTime < nextVisualRefreshTime)
            {
                return;
            }

            nextVisualRefreshTime = Time.unscaledTime + VisualRefreshIntervalSeconds;
            ApplyCommittedVisuals();
        }

        private void OnDestroy()
        {
            ClearStates();

            foreach (KeyValuePair<LimbBehaviour, List<CommittedLimbVisual>> entry in committedVisuals)
            {
                if (entry.Value == null)
                {
                    continue;
                }

                for (int i = 0; i < entry.Value.Count; i++)
                {
                    DestroyCommittedLimbTextures(entry.Value[i]);
                }
            }

            committedVisuals.Clear();
            spriteStateCache.Clear();
        }

        public string GetCharacterName()
        {
            return CleanName(person != null ? person.name : "Character");
        }

        public string GetPartId(LimbBehaviour limb)
        {
            LimbBehaviour target = GetPreferredLimb(limb);
            if (!IsUsableLimb(target))
            {
                return string.Empty;
            }

            List<LimbBehaviour> limbs = GetAvailableLimbs();
            string targetName = GetLimbDisplayName(target);
            int occurrenceIndex = 0;
            int totalOccurrences = 0;

            for (int i = 0; i < limbs.Count; i++)
            {
                if (!string.Equals(GetLimbDisplayName(limbs[i]), targetName, StringComparison.Ordinal))
                {
                    continue;
                }

                totalOccurrences++;
                if (limbs[i] == target)
                {
                    occurrenceIndex = totalOccurrences;
                }
            }

            return totalOccurrences > 1
                ? targetName + "#" + occurrenceIndex
                : targetName;
        }

        public LimbBehaviour FindLimbByPartId(string partId)
        {
            if (string.IsNullOrWhiteSpace(partId))
            {
                return null;
            }

            List<LimbBehaviour> limbs = GetAvailableLimbs();
            for (int i = 0; i < limbs.Count; i++)
            {
                LimbBehaviour limb = limbs[i];
                if (string.Equals(GetPartId(limb), partId, StringComparison.Ordinal))
                {
                    return limb;
                }
            }

            return null;
        }

        public bool TryCapturePersistentEntries(out HypermodifiedPersistentSpriteEntry[] entries)
        {
            List<HypermodifiedPersistentSpriteEntry> captured = new List<HypermodifiedPersistentSpriteEntry>();

            if (person != null && person.Limbs != null)
            {
                for (int i = 0; i < person.Limbs.Length; i++)
                {
                    LimbBehaviour limb = person.Limbs[i];
                    if (!IsUsableLimb(limb))
                    {
                        continue;
                    }

                    CommittedLimbVisual visual = FindCommittedVisual(limb, GetDisplayedSprite(limb));
                    if (visual == null)
                    {
                        List<CommittedLimbVisual> limbVisuals;
                        if (committedVisuals.TryGetValue(limb, out limbVisuals) && limbVisuals != null && limbVisuals.Count > 0)
                        {
                            visual = limbVisuals[limbVisuals.Count - 1];
                        }
                    }

                    HypermodifiedPersistentSpriteEntry entry = CreatePersistentEntry(limb, visual);
                    if (entry != null)
                    {
                        captured.Add(entry);
                    }
                }
            }

            entries = captured.ToArray();
            return entries.Length > 0;
        }

        public bool ApplyPersistentEntries(HypermodifiedPersistentSpriteEntry[] entries)
        {
            if (entries == null)
            {
                return false;
            }

            person = person != null ? person : GetComponent<PersonBehaviour>();
            if (person == null || person.Limbs == null)
            {
                return false;
            }

            ClearStates();
            foreach (KeyValuePair<LimbBehaviour, List<CommittedLimbVisual>> existingEntry in committedVisuals)
            {
                if (existingEntry.Value == null)
                {
                    continue;
                }

                for (int i = 0; i < existingEntry.Value.Count; i++)
                {
                    DestroyCommittedLimbTextures(existingEntry.Value[i]);
                }
            }

            committedVisuals.Clear();
            spriteStateCache.Clear();

            bool appliedAny = false;
            for (int i = 0; i < entries.Length; i++)
            {
                HypermodifiedPersistentSpriteEntry entry = entries[i];
                if (entry == null)
                {
                    continue;
                }

                LimbBehaviour limb = FindLimbByPartId(entry.PartId);
                if (!IsUsableLimb(limb))
                {
                    limb = FindLimbByDisplayName(entry.PartLabel);
                }

                if (IsUsableLimb(limb) && ApplyPersistentEntry(limb, entry))
                {
                    appliedAny = true;
                }
            }

            if (appliedAny)
            {
                RefreshSelfCollisionIgnores();
            }

            return appliedAny;
        }

        public List<LimbBehaviour> GetAvailableLimbs()
        {
            List<LimbBehaviour> result = new List<LimbBehaviour>();

            if (person == null || person.Limbs == null)
            {
                return result;
            }

            for (int i = 0; i < person.Limbs.Length; i++)
            {
                LimbBehaviour limb = person.Limbs[i];

                if (IsUsableLimb(limb))
                {
                    result.Add(limb);
                }
            }

            return result;
        }

        public SpriteRenderer SelectedAccessory
        {
            get { return selectedAccessory; }
        }

        public bool IsAccessorySelected
        {
            get { return IsUsableAccessory(selectedAccessory); }
        }

        public void ClearAccessorySelection()
        {
            selectedAccessory = null;
        }

        public void SelectAccessory(SpriteRenderer renderer)
        {
            if (!IsUsableAccessory(renderer))
            {
                selectedAccessory = null;
                return;
            }

            selectedAccessory = renderer;
        }

        public SpriteRenderer GetHairRenderer(bool createIfMissing)
        {
            LimbBehaviour head = FindHeadLikeLimb();
            if (head == null)
            {
                head = FindHeadLimbLoose();
            }

            if (head == null)
            {
                return null;
            }

            Transform hairTransform = null;
            if (createIfMissing)
            {
                hairTransform = global::DBPU.TmHairApply.EnsureSingleHair(head.transform);
            }
            else
            {
                for (int i = 0; i < head.transform.childCount; i++)
                {
                    Transform child = head.transform.GetChild(i);
                    if (child != null && string.Equals(child.name, "Hair", StringComparison.OrdinalIgnoreCase))
                    {
                        hairTransform = child;
                        break;
                    }
                }
            }

            if (hairTransform == null)
            {
                return null;
            }

            SpriteRenderer hairRenderer = hairTransform.GetComponent<SpriteRenderer>();
            if (hairRenderer == null)
            {
                if (!createIfMissing)
                {
                    return null;
                }

                hairRenderer = hairTransform.gameObject.AddComponent<SpriteRenderer>();
            }

            if (hairRenderer.sprite == null && createIfMissing)
            {
                EnsureBlankAccessorySprite(hairRenderer, head);
            }

            return IsUsableAccessory(hairRenderer) ? hairRenderer : null;
        }

        public LimbSpriteState GetAccessoryState(SpriteRenderer renderer)
        {
            if (!IsUsableAccessory(renderer))
            {
                return null;
            }

            ApplyCommittedAccessory(renderer);
            Sprite displayedSprite = renderer.sprite;
            CommittedAccessoryVisual committedVisual;
            committedAccessories.TryGetValue(renderer, out committedVisual);

            LimbSpriteState state;
            if (accessoryStates.TryGetValue(renderer, out state) && IsReusableState(state, displayedSprite))
            {
                return state;
            }

            if (state != null)
            {
                state.Dispose();
            }

            if (committedVisual != null && (committedVisual.BaseSprite != null || committedVisual.BasePixels != null))
            {
                state = new LimbSpriteState(renderer, committedVisual.BaseSprite, committedVisual.BasePixels, displayedSprite);
                state.RestoreLayerStackSnapshot(committedVisual.LayerStack);
            }
            else
            {
                state = new LimbSpriteState(renderer);
            }

            if (state == null || !state.IsValid)
            {
                if (state != null)
                {
                    state.Dispose();
                }

                accessoryStates.Remove(renderer);
                return null;
            }

            accessoryStates[renderer] = state;
            return state;
        }

        public bool TryGetExistingAccessoryState(SpriteRenderer renderer, out LimbSpriteState state)
        {
            if (accessoryStates.TryGetValue(renderer, out state))
            {
                if (IsReusableState(state, renderer != null ? renderer.sprite : null))
                {
                    return true;
                }

                if (state != null)
                {
                    state.Dispose();
                }

                accessoryStates.Remove(renderer);
            }

            state = null;
            return false;
        }

        public static bool IsUsableAccessory(SpriteRenderer renderer)
        {
            return renderer != null &&
                   renderer.gameObject != null &&
                   renderer.gameObject.scene.IsValid() &&
                   renderer.gameObject.scene.isLoaded &&
                   renderer.sprite != null &&
                   renderer.sprite.texture != null;
        }

        private LimbBehaviour FindHeadLimbLoose()
        {
            if (person == null || person.Limbs == null)
            {
                return null;
            }

            for (int i = 0; i < person.Limbs.Length; i++)
            {
                LimbBehaviour limb = person.Limbs[i];
                if (limb == null || limb.gameObject == null)
                {
                    continue;
                }

                string name = CleanName(limb.gameObject.name).ToLowerInvariant();
                if (name.Contains("head"))
                {
                    return limb;
                }
            }

            return null;
        }

        private void EnsureBlankAccessorySprite(SpriteRenderer renderer, LimbBehaviour head)
        {
            if (renderer == null || renderer.sprite != null)
            {
                return;
            }

            Sprite headSprite = GetDisplayedSprite(head);
            int width = headSprite != null ? Mathf.Max(8, Mathf.RoundToInt(headSprite.rect.width)) : 64;
            int height = headSprite != null ? Mathf.Max(8, Mathf.RoundToInt(headSprite.rect.height)) : 64;
            float ppu = headSprite != null ? headSprite.pixelsPerUnit : 35f;
            Vector2 pivot = headSprite != null
                ? new Vector2(headSprite.pivot.x / Mathf.Max(1f, headSprite.rect.width), headSprite.pivot.y / Mathf.Max(1f, headSprite.rect.height))
                : new Vector2(0.5f, 0.5f);

            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            Color32[] pixels = new Color32[width * height];
            texture.SetPixels32(pixels);
            texture.Apply();
            texture.name = GetCharacterName() + "_HairBlank";

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), pivot, ppu, 0U, SpriteMeshType.FullRect);
            sprite.name = GetCharacterName() + "_Hair";
            renderer.sprite = sprite;
            renderer.enabled = true;
            renderer.gameObject.SetActive(true);

            SpriteRenderer headRenderer = head != null && head.SkinMaterialHandler != null ? head.SkinMaterialHandler.renderer : null;
            if (headRenderer == null && head != null)
            {
                headRenderer = head.GetComponent<SpriteRenderer>();
            }

            if (headRenderer != null)
            {
                renderer.sortingLayerName = headRenderer.sortingLayerName;
                if (renderer.sortingOrder <= headRenderer.sortingOrder)
                {
                    renderer.sortingOrder = headRenderer.sortingOrder + 1;
                }
            }
        }

        private void ApplyCommittedAccessory(SpriteRenderer renderer)
        {
            CommittedAccessoryVisual visual;
            if (!committedAccessories.TryGetValue(renderer, out visual) || visual == null || visual.DisplaySprite == null)
            {
                return;
            }

            if (renderer.sprite != visual.DisplaySprite)
            {
                renderer.sprite = visual.DisplaySprite;
            }
        }

        private bool CommitEditedAccessoryVisual(SpriteRenderer renderer, LimbSpriteState spriteState)
        {
            if (!IsUsableAccessory(renderer) || spriteState == null || !spriteState.IsValid)
            {
                return false;
            }

            Sprite sourceSprite = renderer.sprite;
            if (sourceSprite == null)
            {
                return false;
            }

            CommittedAccessoryVisual existingVisual;
            committedAccessories.TryGetValue(renderer, out existingVisual);
            Sprite baseSprite = spriteState.ResetSprite != null ? spriteState.ResetSprite : sourceSprite;

            Texture2D displayTexture;
            Sprite displaySprite;
            if (!spriteState.TryCreateCommittedDisplay(out displayTexture, out displaySprite))
            {
                return false;
            }

            renderer.sprite = displaySprite;
            renderer.enabled = true;

            if (existingVisual != null)
            {
                if (existingVisual.DisplaySprite != null && existingVisual.DisplaySprite != displaySprite)
                {
                    UnityEngine.Object.Destroy(existingVisual.DisplaySprite);
                }

                if (existingVisual.DisplayTexture != null && existingVisual.DisplayTexture != displayTexture)
                {
                    UnityEngine.Object.Destroy(existingVisual.DisplayTexture);
                }
            }

            CommittedAccessoryVisual visual = existingVisual ?? new CommittedAccessoryVisual();
            visual.BaseSprite = baseSprite;
            visual.BasePixels = spriteState.ResetPixelsSnapshot ?? visual.BasePixels;
            visual.LayerStack = spriteState.CaptureLayerStackSnapshot();
            visual.DisplayTexture = displayTexture;
            visual.DisplaySprite = displaySprite;
            committedAccessories[renderer] = visual;
            return true;
        }

        private void ClearAccessoryStates()
        {
            foreach (KeyValuePair<SpriteRenderer, LimbSpriteState> entry in accessoryStates)
            {
                if (entry.Value != null)
                {
                    entry.Value.Dispose();
                }
            }

            accessoryStates.Clear();
            selectedAccessory = null;
        }

        public LimbBehaviour GetPreferredLimb(LimbBehaviour requested)
        {
            if (IsUsableLimb(requested))
            {
                return requested;
            }

            LimbBehaviour head = FindHeadLikeLimb();
            if (IsUsableLimb(head))
            {
                return head;
            }

            List<LimbBehaviour> limbs = GetAvailableLimbs();
            return limbs.Count > 0 ? limbs[0] : null;
        }

        public string GetLimbDisplayName(LimbBehaviour limb)
        {
            if (limb == null)
            {
                return "Unknown Limb";
            }

            string rawName = limb.gameObject != null ? limb.gameObject.name : limb.name;
            rawName = CleanName(rawName);

            if (string.IsNullOrWhiteSpace(rawName))
            {
                rawName = limb.RoughClassification.ToString();
            }

            return rawName;
        }

        private LimbBehaviour FindLimbByDisplayName(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return null;
            }

            List<LimbBehaviour> limbs = GetAvailableLimbs();
            for (int i = 0; i < limbs.Count; i++)
            {
                LimbBehaviour limb = limbs[i];
                if (string.Equals(GetLimbDisplayName(limb), displayName, StringComparison.Ordinal))
                {
                    return limb;
                }
            }

            return null;
        }

        public LimbSpriteState GetState(LimbBehaviour limb)
        {
            LimbBehaviour target = GetPreferredLimb(limb);
            LimbSpriteState state;

            if (target == null)
            {
                return null;
            }

            ApplyCommittedVisual(target);
            Sprite displayedSprite = GetDisplayedSprite(target);
            CommittedLimbVisual committedVisual = FindCommittedVisual(target, displayedSprite);

            if (states.TryGetValue(target, out state) && IsReusableState(state, displayedSprite))
            {
                return state;
            }

            if (state != null)
            {
                state.Dispose();
            }

            if (committedVisual != null && (committedVisual.BaseSprite != null || committedVisual.BasePixels != null))
            {
                state = new LimbSpriteState(target, committedVisual.BaseSprite, committedVisual.BasePixels, GetDisplayedSprite(target));
                state.RestoreLayerStackSnapshot(committedVisual.LayerStack);
            }
            else
            {
                state = new LimbSpriteState(target);
            }

            if (!state.IsValid)
            {
                state.Dispose();
                states.Remove(target);
                return null;
            }

            states[target] = state;
            return state;
        }

        public bool TryGetExistingState(LimbBehaviour limb, out LimbSpriteState state)
        {
            if (states.TryGetValue(limb, out state))
            {
                Sprite displayedSprite = GetDisplayedSprite(limb);
                if (IsReusableState(state, displayedSprite))
                {
                    return true;
                }

                if (state != null)
                {
                    state.Dispose();
                }

                states.Remove(limb);
            }

            state = null;
            return false;
        }

        private static bool IsReusableState(LimbSpriteState state, Sprite displayedSprite)
        {
            return state != null &&
                   state.IsValid &&
                   (state.HasChanges || state.HasLayerStack || state.MatchesDisplayedSprite(displayedSprite));
        }

        public void ResetAllStates()
        {
            if (person == null || person.Limbs == null)
            {
                return;
            }

            for (int i = 0; i < person.Limbs.Length; i++)
            {
                LimbBehaviour limb = person.Limbs[i];
                if (!IsUsableLimb(limb))
                {
                    continue;
                }

                LimbSpriteState state;
                if (TryGetExistingState(limb, out state))
                {
                    if (state.CanResetToOriginal)
                    {
                        state.ResetToOriginal();
                    }

                    continue;
                }

                CommittedLimbVisual committedVisual = FindCommittedVisual(limb, GetDisplayedSprite(limb));
                if (committedVisual == null || (committedVisual.BaseSprite == null && committedVisual.BasePixels == null))
                {
                    continue;
                }

                state = new LimbSpriteState(limb, committedVisual.BaseSprite, committedVisual.BasePixels, GetDisplayedSprite(limb));
                if (!state.IsValid)
                {
                    state.Dispose();
                    continue;
                }

                states[limb] = state;
                state.ResetToOriginal();
                state.FinalizeForWorldDisplay();
            }

            List<SpriteRenderer> accessories = new List<SpriteRenderer>(accessoryStates.Keys);
            for (int i = 0; i < accessories.Count; i++)
            {
                SpriteRenderer renderer = accessories[i];
                LimbSpriteState state;
                if (!TryGetExistingAccessoryState(renderer, out state) || state == null)
                {
                    continue;
                }

                if (state.CanResetToOriginal)
                {
                    state.ResetToOriginal();
                    state.FinalizeForWorldDisplay();
                }
            }

            foreach (KeyValuePair<SpriteRenderer, CommittedAccessoryVisual> entry in committedAccessories)
            {
                if (entry.Key == null || entry.Value == null)
                {
                    continue;
                }

                if (accessoryStates.ContainsKey(entry.Key))
                {
                    continue;
                }

                if (entry.Value.BaseSprite == null && entry.Value.BasePixels == null)
                {
                    continue;
                }

                LimbSpriteState state = new LimbSpriteState(entry.Key, entry.Value.BaseSprite, entry.Value.BasePixels, entry.Key.sprite);
                if (!state.IsValid)
                {
                    state.Dispose();
                    continue;
                }

                accessoryStates[entry.Key] = state;
                state.ResetToOriginal();
                state.FinalizeForWorldDisplay();
            }
        }

        public bool HasAnyResettableChanges()
        {
            foreach (KeyValuePair<LimbBehaviour, LimbSpriteState> entry in states)
            {
                if (entry.Value != null && entry.Value.IsValid && entry.Value.CanResetToOriginal)
                {
                    return true;
                }
            }

            foreach (KeyValuePair<SpriteRenderer, LimbSpriteState> entry in accessoryStates)
            {
                if (entry.Value != null && entry.Value.IsValid && entry.Value.CanResetToOriginal)
                {
                    return true;
                }
            }

            foreach (KeyValuePair<SpriteRenderer, CommittedAccessoryVisual> entry in committedAccessories)
            {
                if (entry.Value != null && (entry.Value.BaseSprite != null || entry.Value.BasePixels != null))
                {
                    return true;
                }
            }

            if (person == null || person.Limbs == null)
            {
                return false;
            }

            for (int i = 0; i < person.Limbs.Length; i++)
            {
                LimbBehaviour limb = person.Limbs[i];
                if (!IsUsableLimb(limb))
                {
                    continue;
                }

                CommittedLimbVisual committedVisual = FindCommittedVisual(limb, GetDisplayedSprite(limb));
                if (committedVisual != null && (committedVisual.BaseSprite != null || committedVisual.BasePixels != null))
                {
                    return true;
                }
            }

            return false;
        }

        public void ApplyPendingHitboxChanges()
        {
            lastMakerExports.Clear();
            bool appliedAnyHitboxes = false;
            bool committedAnyVisuals = false;
            SpriteRenderer hairRenderer = GetHairRenderer(false);
            foreach (KeyValuePair<LimbBehaviour, LimbSpriteState> entry in states)
            {
                if (entry.Value == null || !entry.Value.IsValid)
                {
                    continue;
                }

                entry.Value.FinalizeForWorldDisplay();

                if (entry.Value.HasPendingHitboxChanges())
                {
                    appliedAnyHitboxes |= entry.Value.ApplyPendingHitboxChanges();
                }
            }

            foreach (KeyValuePair<LimbBehaviour, LimbSpriteState> entry in states)
            {
                if (entry.Value == null || !entry.Value.IsValid || (!entry.Value.HasChanges && !entry.Value.HasLayerStack))
                {
                    continue;
                }

                if (CommitEditedLimbVisual(entry.Key, entry.Value))
                {
                    committedAnyVisuals = true;
                    entry.Value.AcceptCurrentAsSessionBase();
                    RecordMakerLimbExport(entry.Key);
                }
            }

            List<SpriteRenderer> accessories = new List<SpriteRenderer>(accessoryStates.Keys);
            for (int i = 0; i < accessories.Count; i++)
            {
                SpriteRenderer renderer = accessories[i];
                LimbSpriteState state;
                if (!accessoryStates.TryGetValue(renderer, out state) || state == null || !state.IsValid)
                {
                    continue;
                }

                state.FinalizeForWorldDisplay();
                if (!state.HasChanges && !state.HasLayerStack)
                {
                    continue;
                }

                if (CommitEditedAccessoryVisual(renderer, state))
                {
                    committedAnyVisuals = true;
                    state.AcceptCurrentAsSessionBase();
                    bool isHair = hairRenderer != null && renderer == hairRenderer;
                    RecordMakerAccessoryExport(renderer, isHair);
                }
            }

            if (appliedAnyHitboxes || committedAnyVisuals)
            {
                RefreshSelfCollisionIgnores();
                Physics2D.SyncTransforms();
            }

        }

        public List<MakerSpriteExport> TakeLastMakerExports()
        {
            var copy = new List<MakerSpriteExport>(lastMakerExports);
            lastMakerExports.Clear();
            return copy;
        }

        private void RecordMakerLimbExport(LimbBehaviour limb)
        {
            if (!IsUsableLimb(limb))
            {
                return;
            }

            CommittedLimbVisual visual = FindCommittedVisual(limb, GetDisplayedSprite(limb));
            if (visual == null || visual.SkinTexture == null)
            {
                return;
            }

            byte[] png = HypermodifiedSpritePersistence.EncodeTexture(visual.SkinTexture);
            if (png == null || png.Length == 0)
            {
                return;
            }

            lastMakerExports.Add(new MakerSpriteExport
            {
                LimbName = GetLimbDisplayName(limb),
                Png = png,
                Sprite = visual.Sprites != null ? visual.Sprites.Skin : GetDisplayedSprite(limb),
                IsHair = false
            });
        }

        private void RecordMakerAccessoryExport(SpriteRenderer renderer, bool isHair)
        {
            CommittedAccessoryVisual visual;
            if (renderer == null || !committedAccessories.TryGetValue(renderer, out visual) || visual == null || visual.DisplayTexture == null)
            {
                return;
            }

            byte[] png = HypermodifiedSpritePersistence.EncodeTexture(visual.DisplayTexture);
            if (png == null || png.Length == 0)
            {
                return;
            }

            lastMakerExports.Add(new MakerSpriteExport
            {
                LimbName = isHair ? "Hair" : (renderer.gameObject != null ? renderer.gameObject.name : "Accessory"),
                Png = png,
                Sprite = visual.DisplaySprite,
                IsHair = isHair
            });
        }

        private void RefreshSelfCollisionIgnores()
        {
            if (person == null || person.Limbs == null)
            {
                return;
            }

            try
            {
                var method = typeof(PersonBehaviour).GetMethod("SetCollisionIgnores", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (method != null)
                {
                    method.Invoke(person, null);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Hypermodified] Failed to refresh character collision ignores: " + exception.Message);
            }

            for (int i = 0; i < person.Limbs.Length; i++)
            {
                List<Collider2D> firstColliders = GetLimbColliders(person.Limbs[i]);
                if (firstColliders.Count <= 0)
                {
                    continue;
                }

                for (int j = i + 1; j < person.Limbs.Length; j++)
                {
                    List<Collider2D> secondColliders = GetLimbColliders(person.Limbs[j]);
                    if (secondColliders.Count <= 0)
                    {
                        continue;
                    }

                    for (int firstIndex = 0; firstIndex < firstColliders.Count; firstIndex++)
                    {
                        Collider2D firstCollider = firstColliders[firstIndex];
                        if (firstCollider == null)
                        {
                            continue;
                        }

                        for (int secondIndex = 0; secondIndex < secondColliders.Count; secondIndex++)
                        {
                            Collider2D secondCollider = secondColliders[secondIndex];
                            if (secondCollider == null || firstCollider == secondCollider)
                            {
                                continue;
                            }

                            Physics2D.IgnoreCollision(firstCollider, secondCollider, true);
                        }
                    }
                }
            }
        }

        private static List<Collider2D> GetLimbColliders(LimbBehaviour limb)
        {
            List<Collider2D> result = new List<Collider2D>();
            if (!IsUsableLimb(limb))
            {
                return result;
            }

            AddCollider(result, limb.Collider);

            if (limb.PhysicalBehaviour != null)
            {
                Collider2D[] colliders = limb.PhysicalBehaviour.GetComponents<Collider2D>();
                for (int i = 0; i < colliders.Length; i++)
                {
                    AddCollider(result, colliders[i]);
                }
            }

            return result;
        }

        private static void AddCollider(List<Collider2D> colliders, Collider2D collider)
        {
            if (colliders == null || collider == null)
            {
                return;
            }

            for (int i = 0; i < colliders.Count; i++)
            {
                if (colliders[i] == collider)
                {
                    return;
                }
            }

            colliders.Add(collider);
        }

        public void FinalizeAllStatesForWorldDisplay()
        {
            foreach (KeyValuePair<LimbBehaviour, LimbSpriteState> entry in states)
            {
                if (entry.Value != null && entry.Value.IsValid)
                {
                    entry.Value.FinalizeForWorldDisplay();
                }
            }

            foreach (KeyValuePair<SpriteRenderer, LimbSpriteState> entry in accessoryStates)
            {
                if (entry.Value != null && entry.Value.IsValid)
                {
                    entry.Value.FinalizeForWorldDisplay();
                }
            }
        }

        public void OpenEditorFor(LimbBehaviour limb)
        {
            SpriteEditorRuntime.Open(this, limb);
        }

        private void ClearStates()
        {
            foreach (KeyValuePair<LimbBehaviour, LimbSpriteState> entry in states)
            {
                if (entry.Value != null)
                {
                    entry.Value.Dispose();
                }
            }

            states.Clear();
            ClearAccessoryStates();
        }

        private bool CommitEditedLimbVisual(LimbBehaviour limb, LimbSpriteState state)
        {
            if (!IsUsableLimb(limb) || state == null || !state.IsValid)
            {
                return false;
            }

            SpriteRenderer skinRenderer = limb.SkinMaterialHandler != null ? limb.SkinMaterialHandler.renderer : null;
            Sprite sourceSprite = GetDisplayedSprite(limb);

            if (sourceSprite == null && state.WorkingTexture == null)
            {
                return false;
            }

            CommittedLimbVisual existingVisual = FindCommittedVisual(limb, sourceSprite);
            Sprite baseSprite = state.ResetSprite != null ? state.ResetSprite : sourceSprite;
            SpriteStateKey baseStateKey = existingVisual != null && existingVisual.BaseStateKey.IsValid
                ? existingVisual.BaseStateKey
                : GetSpriteStateKey(baseSprite);

            Texture2D committedSkin = null;
            Sprite loadBasis = null;
            bool ownsLoadBasis = false;
            if (state.WorkingTexture != null)
            {
                committedSkin = LimbSpriteState.CreateReadableTextureCopy(state.WorkingTexture);
                Sprite pivotSource = state.OriginalSprite != null ? state.OriginalSprite : (sourceSprite != null ? sourceSprite : baseSprite);
                loadBasis = CreateTextureSizedSprite(committedSkin, pivotSource, GetCharacterName() + "_" + GetLimbDisplayName(limb) + "_CommitBasis");
                ownsLoadBasis = loadBasis != null;
            }
            else if (sourceSprite != null && sourceSprite.texture != null)
            {
                committedSkin = ExtractSpriteLocalTexture(sourceSprite);
                if (committedSkin == null)
                {
                    committedSkin = LimbSpriteState.CreateReadableTextureCopy(sourceSprite.texture);
                }

                if (committedSkin != null &&
                    (Mathf.RoundToInt(sourceSprite.rect.width) != committedSkin.width ||
                     Mathf.RoundToInt(sourceSprite.rect.height) != committedSkin.height))
                {
                    loadBasis = CreateTextureSizedSprite(committedSkin, sourceSprite, GetCharacterName() + "_" + GetLimbDisplayName(limb) + "_CommitBasis");
                    ownsLoadBasis = loadBasis != null;
                }
                else
                {
                    loadBasis = sourceSprite;
                }
            }

            if (committedSkin == null)
            {
                if (ownsLoadBasis && loadBasis != null)
                {
                    UnityEngine.Object.Destroy(loadBasis);
                }

                return false;
            }

            if (loadBasis == null)
            {
                loadBasis = CreateTextureSizedSprite(committedSkin, sourceSprite != null ? sourceSprite : baseSprite, GetCharacterName() + "_" + GetLimbDisplayName(limb) + "_CommitBasis");
                ownsLoadBasis = loadBasis != null;
            }

            if (loadBasis == null)
            {
                UnityEngine.Object.Destroy(committedSkin);
                return false;
            }

            Texture2D fleshTexture;
            Texture2D boneTexture;
            bool ownsBodyLayerTextures;
            if (!state.TryCreateReplacementBodyLayerTextures(out fleshTexture, out boneTexture, out ownsBodyLayerTextures) &&
                !TryGetBodyLayerTextures(limb, out fleshTexture, out boneTexture))
            {
                UnityEngine.Object.Destroy(committedSkin);
                if (ownsLoadBasis)
                {
                    UnityEngine.Object.Destroy(loadBasis);
                }

                return false;
            }

            if (!ownsBodyLayerTextures &&
                (fleshTexture.width != committedSkin.width || fleshTexture.height != committedSkin.height ||
                 boneTexture.width != committedSkin.width || boneTexture.height != committedSkin.height))
            {
                Texture2D matchedFlesh = ResizeOrCopyTexture(fleshTexture, committedSkin.width, committedSkin.height);
                Texture2D matchedBone = ResizeOrCopyTexture(boneTexture, committedSkin.width, committedSkin.height);
                if (matchedFlesh == null || matchedBone == null)
                {
                    if (matchedFlesh != null)
                    {
                        UnityEngine.Object.Destroy(matchedFlesh);
                    }

                    if (matchedBone != null)
                    {
                        UnityEngine.Object.Destroy(matchedBone);
                    }

                    UnityEngine.Object.Destroy(committedSkin);
                    if (ownsLoadBasis)
                    {
                        UnityEngine.Object.Destroy(loadBasis);
                    }

                    return false;
                }

                fleshTexture = matchedFlesh;
                boneTexture = matchedBone;
                ownsBodyLayerTextures = true;
            }
            else if (ownsBodyLayerTextures)
            {
                if (!EnsureOwnedTextureMatchesSize(ref fleshTexture, committedSkin.width, committedSkin.height) ||
                    !EnsureOwnedTextureMatchesSize(ref boneTexture, committedSkin.width, committedSkin.height))
                {
                    UnityEngine.Object.Destroy(committedSkin);
                    if (ownsLoadBasis)
                    {
                        UnityEngine.Object.Destroy(loadBasis);
                    }

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
            }

            Texture2D damageTexture;
            bool ownsDamageTexture;
            if (!state.TryCreateReplacementDamageLayerTexture(out damageTexture, out ownsDamageTexture))
            {
                ownsDamageTexture = false;
                TryGetDamageLayerTexture(limb, out damageTexture);
            }

            if (damageTexture != null &&
                (damageTexture.width != committedSkin.width || damageTexture.height != committedSkin.height))
            {
                Texture2D matchedDamage = ResizeOrCopyTexture(damageTexture, committedSkin.width, committedSkin.height);
                if (ownsDamageTexture && damageTexture != null)
                {
                    UnityEngine.Object.Destroy(damageTexture);
                }

                damageTexture = matchedDamage;
                ownsDamageTexture = matchedDamage != null;
                if (damageTexture == null)
                {
                    ownsDamageTexture = false;
                }
            }

            LimbSpriteCache cache = LimbSpriteCache.Instance;
            if (cache == null)
            {
                UnityEngine.Object.Destroy(committedSkin);
                if (ownsLoadBasis)
                {
                    UnityEngine.Object.Destroy(loadBasis);
                }

                if (ownsBodyLayerTextures)
                {
                    UnityEngine.Object.Destroy(fleshTexture);
                    UnityEngine.Object.Destroy(boneTexture);
                }

                if (ownsDamageTexture && damageTexture != null)
                {
                    UnityEngine.Object.Destroy(damageTexture);
                }

                Debug.LogWarning("[Hypermodified] LimbSpriteCache was unavailable during limb sprite commit.");
                return false;
            }

            committedSkin.name = GetCharacterName() + "_" + GetLimbDisplayName(limb) + "_LiveSpriteWorkshopSkin";
            LimbSpriteCache.LimbSprites sprites;

            try
            {
                sprites = cache.LoadFor(loadBasis, committedSkin, fleshTexture, boneTexture, 1f);
            }
            catch (Exception exception)
            {
                UnityEngine.Object.Destroy(committedSkin);
                if (ownsLoadBasis)
                {
                    UnityEngine.Object.Destroy(loadBasis);
                }

                if (ownsBodyLayerTextures)
                {
                    UnityEngine.Object.Destroy(fleshTexture);
                    UnityEngine.Object.Destroy(boneTexture);
                }

                if (ownsDamageTexture && damageTexture != null)
                {
                    UnityEngine.Object.Destroy(damageTexture);
                }

                Debug.LogError("[Hypermodified] Failed to build committed limb sprites: " + exception);
                return false;
            }

            if (sprites == null || sprites.Skin == null)
            {
                UnityEngine.Object.Destroy(committedSkin);
                if (ownsLoadBasis)
                {
                    UnityEngine.Object.Destroy(loadBasis);
                }

                if (ownsBodyLayerTextures)
                {
                    UnityEngine.Object.Destroy(fleshTexture);
                    UnityEngine.Object.Destroy(boneTexture);
                }

                if (ownsDamageTexture && damageTexture != null)
                {
                    UnityEngine.Object.Destroy(damageTexture);
                }

                return false;
            }

            SpriteRenderer physicalRenderer = limb.PhysicalBehaviour != null ? limb.PhysicalBehaviour.spriteRenderer : null;
            SpriteRenderer targetRenderer = skinRenderer != null ? skinRenderer : physicalRenderer;
            if (targetRenderer == null)
            {
                UnityEngine.Object.Destroy(committedSkin);
                if (ownsLoadBasis)
                {
                    UnityEngine.Object.Destroy(loadBasis);
                }

                if (ownsBodyLayerTextures)
                {
                    UnityEngine.Object.Destroy(fleshTexture);
                    UnityEngine.Object.Destroy(boneTexture);
                }

                if (ownsDamageTexture && damageTexture != null)
                {
                    UnityEngine.Object.Destroy(damageTexture);
                }

                return false;
            }

            targetRenderer.sprite = sprites.Skin;
            if (physicalRenderer != null && physicalRenderer != targetRenderer)
            {
                physicalRenderer.sprite = sprites.Skin;
            }

            Material material = targetRenderer.material;
            if (material != null)
            {
                material.SetTexture("_FleshTex", fleshTexture);
                material.SetTexture("_BoneTex", boneTexture);
                if (damageTexture != null)
                {
                    material.SetTexture("_DamageTex", damageTexture);
                }
            }

            if (physicalRenderer != null && physicalRenderer != targetRenderer && physicalRenderer.material != null)
            {
                physicalRenderer.material.SetTexture("_FleshTex", fleshTexture);
                physicalRenderer.material.SetTexture("_BoneTex", boneTexture);
                if (damageTexture != null)
                {
                    physicalRenderer.material.SetTexture("_DamageTex", damageTexture);
                }
            }

            ShatteredObjectSpriteInitialiser shattered;
            if (limb.TryGetComponent<ShatteredObjectSpriteInitialiser>(out shattered) && shattered != null)
            {
                try
                {
                    shattered.UpdateSprites(sprites);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("[Hypermodified] Failed to refresh shattered limb sprites: " + exception);
                }
            }

            if (existingVisual != null &&
                existingVisual.SkinTexture != null &&
                existingVisual.SkinTexture != committedSkin)
            {
                UnityEngine.Object.Destroy(existingVisual.SkinTexture);
            }

            if (existingVisual != null && existingVisual.OwnsBodyLayerTextures)
            {
                if (existingVisual.FleshTexture != null && existingVisual.FleshTexture != fleshTexture)
                {
                    UnityEngine.Object.Destroy(existingVisual.FleshTexture);
                }

                if (existingVisual.BoneTexture != null && existingVisual.BoneTexture != boneTexture)
                {
                    UnityEngine.Object.Destroy(existingVisual.BoneTexture);
                }
            }

            if (existingVisual != null &&
                existingVisual.OwnsDamageTexture &&
                existingVisual.DamageTexture != null &&
                existingVisual.DamageTexture != damageTexture)
            {
                UnityEngine.Object.Destroy(existingVisual.DamageTexture);
            }

            if (existingVisual != null &&
                existingVisual.OwnsSourceSprite &&
                existingVisual.SourceSprite != null &&
                existingVisual.SourceSprite != loadBasis)
            {
                UnityEngine.Object.Destroy(existingVisual.SourceSprite);
            }

            CommittedLimbVisual visual = existingVisual ?? new CommittedLimbVisual();
            visual.BaseStateKey = baseStateKey;
            visual.CommittedStateKey = GetSpriteStateKey(sprites.Skin);
            visual.BaseSprite = state.ResetSprite != null ? state.ResetSprite : visual.BaseSprite;
            visual.BasePixels = state.ResetPixelsSnapshot ?? visual.BasePixels;
            visual.LayerStack = state.CaptureLayerStackSnapshot();
            visual.SkinTexture = committedSkin;
            visual.FleshTexture = fleshTexture;
            visual.BoneTexture = boneTexture;
            visual.DamageTexture = damageTexture;
            visual.OwnsBodyLayerTextures = ownsBodyLayerTextures;
            visual.OwnsDamageTexture = ownsDamageTexture;
            visual.Sprites = sprites;
            visual.SourceSprite = ownsLoadBasis ? loadBasis : null;
            visual.OwnsSourceSprite = ownsLoadBasis;
            visual.MaterialsApplied = false;

            List<CommittedLimbVisual> limbVisuals;
            if (!committedVisuals.TryGetValue(limb, out limbVisuals) || limbVisuals == null)
            {
                limbVisuals = new List<CommittedLimbVisual>();
                committedVisuals[limb] = limbVisuals;
            }

            if (existingVisual == null)
            {
                limbVisuals.Add(visual);
            }

            ApplyCommittedVisual(limb, visual);
            UpdatePersistencePayload();
            return true;
        }

        private void UpdatePersistencePayload()
        {
            HypermodifiedSpritePersistence persistence = HypermodifiedSpritePersistence.EnsureForCharacter(person);
            HypermodifiedPersistentSpriteEntry[] entries;
            if (persistence != null && TryCapturePersistentEntries(out entries))
            {
                persistence.Version = HypermodifiedSpritePersistence.CurrentVersion;
                persistence.TargetKind = HypermodifiedSpritePersistence.CharacterKind;
                persistence.StoreEntries(entries);
            }
        }

        private HypermodifiedPersistentSpriteEntry CreatePersistentEntry(LimbBehaviour limb, CommittedLimbVisual visual)
        {
            if (!IsUsableLimb(limb) ||
                visual == null ||
                visual.SkinTexture == null ||
                visual.FleshTexture == null ||
                visual.BoneTexture == null ||
                visual.Sprites == null ||
                visual.Sprites.Skin == null)
            {
                return null;
            }

            byte[] skinPng = HypermodifiedSpritePersistence.EncodeTexture(visual.SkinTexture);
            byte[] fleshPng = HypermodifiedSpritePersistence.EncodeTexture(visual.FleshTexture);
            byte[] bonePng = HypermodifiedSpritePersistence.EncodeTexture(visual.BoneTexture);
            byte[] damagePng = HypermodifiedSpritePersistence.EncodeTexture(visual.DamageTexture);
            if (skinPng == null || skinPng.Length == 0 ||
                fleshPng == null || fleshPng.Length == 0 ||
                bonePng == null || bonePng.Length == 0)
            {
                return null;
            }

            Sprite sprite = visual.Sprites.Skin;
            Rect rect = sprite.rect;
            return new HypermodifiedPersistentSpriteEntry
            {
                PartId = GetPartId(limb),
                PartLabel = GetLimbDisplayName(limb),
                RectX = Mathf.RoundToInt(rect.x),
                RectY = Mathf.RoundToInt(rect.y),
                RectWidth = Mathf.RoundToInt(rect.width),
                RectHeight = Mathf.RoundToInt(rect.height),
                PivotX = Mathf.RoundToInt(sprite.pivot.x),
                PivotY = Mathf.RoundToInt(sprite.pivot.y),
                TextureWidth = visual.SkinTexture.width,
                TextureHeight = visual.SkinTexture.height,
                PixelsPerUnit = sprite.pixelsPerUnit,
                SkinPng = skinPng,
                FleshPng = fleshPng,
                BonePng = bonePng,
                DamagePng = damagePng,
                LayerStackData = HypermodifiedSpritePersistence.EncodeLayerStack(visual.LayerStack)
            };
        }

        private bool ApplyPersistentEntry(LimbBehaviour limb, HypermodifiedPersistentSpriteEntry entry)
        {
            Texture2D skinTexture = HypermodifiedSpritePersistence.LoadTexture(entry.SkinPng, GetCharacterName() + "_" + GetLimbDisplayName(limb) + "_PersistentSkin");
            Texture2D fleshTexture = HypermodifiedSpritePersistence.LoadTexture(entry.FleshPng, GetCharacterName() + "_" + GetLimbDisplayName(limb) + "_PersistentFlesh");
            Texture2D boneTexture = HypermodifiedSpritePersistence.LoadTexture(entry.BonePng, GetCharacterName() + "_" + GetLimbDisplayName(limb) + "_PersistentBone");
            Texture2D damageTexture = HypermodifiedSpritePersistence.LoadTexture(entry.DamagePng, GetCharacterName() + "_" + GetLimbDisplayName(limb) + "_PersistentDamage");
            if (skinTexture == null || fleshTexture == null || boneTexture == null)
            {
                if (skinTexture != null)
                {
                    UnityEngine.Object.Destroy(skinTexture);
                }

                if (fleshTexture != null)
                {
                    UnityEngine.Object.Destroy(fleshTexture);
                }

                if (boneTexture != null)
                {
                    UnityEngine.Object.Destroy(boneTexture);
                }

                if (damageTexture != null)
                {
                    UnityEngine.Object.Destroy(damageTexture);
                }

                return false;
            }

            Sprite sourceSprite = HypermodifiedSpritePersistence.CreateSprite(entry, skinTexture, GetCharacterName() + "_" + GetLimbDisplayName(limb) + "_PersistentSource");
            if (sourceSprite == null)
            {
                UnityEngine.Object.Destroy(skinTexture);
                UnityEngine.Object.Destroy(fleshTexture);
                UnityEngine.Object.Destroy(boneTexture);
                if (damageTexture != null)
                {
                    UnityEngine.Object.Destroy(damageTexture);
                }
                return false;
            }

            LimbSpriteCache cache = LimbSpriteCache.Instance;
            if (cache == null)
            {
                UnityEngine.Object.Destroy(sourceSprite);
                UnityEngine.Object.Destroy(skinTexture);
                UnityEngine.Object.Destroy(fleshTexture);
                UnityEngine.Object.Destroy(boneTexture);
                if (damageTexture != null)
                {
                    UnityEngine.Object.Destroy(damageTexture);
                }
                return false;
            }

            LimbSpriteCache.LimbSprites sprites;
            try
            {
                sprites = cache.LoadFor(sourceSprite, skinTexture, fleshTexture, boneTexture, 1f);
            }
            catch (Exception exception)
            {
                Debug.LogError("[Hypermodified] Failed to restore persistent limb sprites: " + exception);
                UnityEngine.Object.Destroy(sourceSprite);
                UnityEngine.Object.Destroy(skinTexture);
                UnityEngine.Object.Destroy(fleshTexture);
                UnityEngine.Object.Destroy(boneTexture);
                if (damageTexture != null)
                {
                    UnityEngine.Object.Destroy(damageTexture);
                }
                return false;
            }

            if (sprites == null || sprites.Skin == null)
            {
                UnityEngine.Object.Destroy(sourceSprite);
                UnityEngine.Object.Destroy(skinTexture);
                UnityEngine.Object.Destroy(fleshTexture);
                UnityEngine.Object.Destroy(boneTexture);
                if (damageTexture != null)
                {
                    UnityEngine.Object.Destroy(damageTexture);
                }
                return false;
            }

            LimbSpriteState state;
            if (states.TryGetValue(limb, out state) && state != null)
            {
                state.Dispose();
                states.Remove(limb);
            }

            Sprite baseSprite = GetDisplayedSprite(limb);
            CommittedLimbVisual visual = new CommittedLimbVisual();
            visual.BaseStateKey = GetSpriteStateKey(baseSprite);
            visual.CommittedStateKey = GetSpriteStateKey(sprites.Skin);
            visual.BaseSprite = baseSprite;
            visual.LayerStack = HypermodifiedSpritePersistence.DecodeLayerStack(entry.LayerStackData);
            visual.SkinTexture = skinTexture;
            visual.FleshTexture = fleshTexture;
            visual.BoneTexture = boneTexture;
            visual.DamageTexture = damageTexture;
            visual.OwnsBodyLayerTextures = true;
            visual.OwnsDamageTexture = damageTexture != null;
            visual.Sprites = sprites;
            visual.SourceSprite = sourceSprite;
            visual.OwnsSourceSprite = true;

            List<CommittedLimbVisual> visuals;
            if (!committedVisuals.TryGetValue(limb, out visuals) || visuals == null)
            {
                visuals = new List<CommittedLimbVisual>();
                committedVisuals[limb] = visuals;
            }

            visuals.Add(visual);
            ApplyCommittedVisual(limb, visual);
            RestoreLimbHitboxState(limb, visual);
            return true;
        }

        private void RestoreLimbHitboxState(LimbBehaviour limb, CommittedLimbVisual visual)
        {
            if (!IsUsableLimb(limb) || visual == null || visual.Sprites == null || visual.Sprites.Skin == null)
            {
                return;
            }

            LimbSpriteState restoredState = new LimbSpriteState(limb, visual.BaseSprite, visual.BasePixels, visual.Sprites.Skin);
            if (restoredState == null || !restoredState.IsValid)
            {
                if (restoredState != null)
                {
                    restoredState.Dispose();
                }

                return;
            }

            restoredState.RestoreLayerStackSnapshot(visual.LayerStack);
            restoredState.ForceApplyHitboxChanges();
            ApplyCommittedVisual(limb, visual);
            restoredState.AcceptCurrentAsSessionBase();
            states[limb] = restoredState;
        }

        private static void DestroyCommittedLimbTextures(CommittedLimbVisual visual)
        {
            if (visual == null)
            {
                return;
            }

            if (visual.SkinTexture != null)
            {
                UnityEngine.Object.Destroy(visual.SkinTexture);
                visual.SkinTexture = null;
            }

            if (visual.OwnsBodyLayerTextures)
            {
                if (visual.FleshTexture != null)
                {
                    UnityEngine.Object.Destroy(visual.FleshTexture);
                    visual.FleshTexture = null;
                }

                if (visual.BoneTexture != null)
                {
                    UnityEngine.Object.Destroy(visual.BoneTexture);
                    visual.BoneTexture = null;
                }
            }

            if (visual.OwnsDamageTexture && visual.DamageTexture != null)
            {
                UnityEngine.Object.Destroy(visual.DamageTexture);
                visual.DamageTexture = null;
            }

            if (visual.OwnsSourceSprite && visual.SourceSprite != null)
            {
                UnityEngine.Object.Destroy(visual.SourceSprite);
                visual.SourceSprite = null;
            }
        }

        private static Sprite CreateTextureSizedSprite(Texture2D texture, Sprite pivotSource, string spriteName)
        {
            if (texture == null || texture.width <= 0 || texture.height <= 0)
            {
                return null;
            }

            Vector2 pivot = new Vector2(0.5f, 0.5f);
            float pixelsPerUnit = 35f;
            if (pivotSource != null)
            {
                float pw = Mathf.Max(1f, pivotSource.rect.width);
                float ph = Mathf.Max(1f, pivotSource.rect.height);
                pivot = new Vector2(
                    Mathf.Clamp01(pivotSource.pivot.x / pw),
                    Mathf.Clamp01(pivotSource.pivot.y / ph));
                if (pivotSource.pixelsPerUnit > 0.01f)
                {
                    pixelsPerUnit = pivotSource.pixelsPerUnit;
                }
            }

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                pivot,
                pixelsPerUnit,
                0U,
                SpriteMeshType.FullRect);
            if (sprite != null && !string.IsNullOrEmpty(spriteName))
            {
                sprite.name = spriteName;
            }

            return sprite;
        }

        private static Texture2D ExtractSpriteLocalTexture(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null)
            {
                return null;
            }

            int width = Mathf.Max(1, Mathf.RoundToInt(sprite.rect.width));
            int height = Mathf.Max(1, Mathf.RoundToInt(sprite.rect.height));
            if (sprite.texture.width == width && sprite.texture.height == height &&
                Mathf.RoundToInt(sprite.rect.x) == 0 && Mathf.RoundToInt(sprite.rect.y) == 0)
            {
                return LimbSpriteState.CreateReadableTextureCopy(sprite.texture);
            }

            Texture2D readable = LimbSpriteState.CreateReadableTextureCopy(sprite.texture);
            if (readable == null)
            {
                return null;
            }

            int x = Mathf.Clamp(Mathf.RoundToInt(sprite.rect.x), 0, Mathf.Max(0, readable.width - 1));
            int y = Mathf.Clamp(Mathf.RoundToInt(sprite.rect.y), 0, Mathf.Max(0, readable.height - 1));
            width = Mathf.Clamp(width, 1, readable.width - x);
            height = Mathf.Clamp(height, 1, readable.height - y);
            Color[] pixels = readable.GetPixels(x, y, width, height);
            UnityEngine.Object.Destroy(readable);
            Texture2D local = new Texture2D(width, height, TextureFormat.RGBA32, false);
            local.filterMode = FilterMode.Point;
            local.wrapMode = TextureWrapMode.Clamp;
            local.SetPixels(pixels);
            local.Apply(false, false);
            return local;
        }

        private static bool EnsureOwnedTextureMatchesSize(ref Texture2D texture, int width, int height)
        {
            if (texture == null || width <= 0 || height <= 0)
            {
                return false;
            }

            if (texture.width == width && texture.height == height)
            {
                return true;
            }

            Texture2D resized = ResizeOrCopyTexture(texture, width, height);
            if (resized == null)
            {
                return false;
            }

            UnityEngine.Object.Destroy(texture);
            texture = resized;
            return true;
        }

        private static Texture2D ResizeOrCopyTexture(Texture2D source, int width, int height)
        {
            if (source == null || width <= 0 || height <= 0)
            {
                return null;
            }

            Texture2D readable = source;
            bool ownsReadable = false;
            try
            {
                if (!source.isReadable)
                {
                    readable = LimbSpriteState.CreateReadableTextureCopy(source);
                    ownsReadable = readable != null;
                    if (readable == null)
                    {
                        return null;
                    }
                }
            }
            catch
            {
                readable = LimbSpriteState.CreateReadableTextureCopy(source);
                ownsReadable = readable != null;
                if (readable == null)
                {
                    return null;
                }
            }

            if (readable.width == width && readable.height == height)
            {
                if (ownsReadable)
                {
                    return readable;
                }

                Texture2D copy = LimbSpriteState.CreateReadableTextureCopy(readable);
                return copy;
            }

            Color32[] sourcePixels = readable.GetPixels32();
            Color32[] destPixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                int srcY = Mathf.Clamp((y * readable.height) / height, 0, readable.height - 1);
                for (int x = 0; x < width; x++)
                {
                    int srcX = Mathf.Clamp((x * readable.width) / width, 0, readable.width - 1);
                    destPixels[y * width + x] = sourcePixels[srcY * readable.width + srcX];
                }
            }

            if (ownsReadable)
            {
                UnityEngine.Object.Destroy(readable);
            }

            Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false);
            result.filterMode = FilterMode.Point;
            result.wrapMode = TextureWrapMode.Clamp;
            result.SetPixels32(destPixels);
            result.Apply(false, false);
            return result;
        }

        private bool TryGetBodyLayerTextures(LimbBehaviour limb, out Texture2D fleshTexture, out Texture2D boneTexture)
        {
            fleshTexture = null;
            boneTexture = null;

            if (!IsUsableLimb(limb))
            {
                return false;
            }

            SpriteRenderer skinRenderer = limb.SkinMaterialHandler != null ? limb.SkinMaterialHandler.renderer : null;
            Material material = skinRenderer != null ? skinRenderer.material : null;
            if (material == null && limb.PhysicalBehaviour != null && limb.PhysicalBehaviour.spriteRenderer != null)
            {
                material = limb.PhysicalBehaviour.spriteRenderer.material;
            }

            if (material == null)
            {
                return false;
            }

            fleshTexture = material.GetTexture("_FleshTex") as Texture2D;
            boneTexture = material.GetTexture("_BoneTex") as Texture2D;
            return fleshTexture != null && boneTexture != null;
        }

        private bool TryGetDamageLayerTexture(LimbBehaviour limb, out Texture2D damageTexture)
        {
            damageTexture = null;

            if (!IsUsableLimb(limb))
            {
                return false;
            }

            SpriteRenderer skinRenderer = limb.SkinMaterialHandler != null ? limb.SkinMaterialHandler.renderer : null;
            Material material = skinRenderer != null ? skinRenderer.material : null;
            if (material == null && limb.PhysicalBehaviour != null && limb.PhysicalBehaviour.spriteRenderer != null)
            {
                material = limb.PhysicalBehaviour.spriteRenderer.material;
            }

            if (material == null)
            {
                return false;
            }

            damageTexture = material.GetTexture("_DamageTex") as Texture2D;
            return damageTexture != null;
        }

        private void ApplyCommittedVisuals()
        {
            foreach (KeyValuePair<LimbBehaviour, List<CommittedLimbVisual>> entry in committedVisuals)
            {
                ApplyCommittedVisual(entry.Key);
            }
        }

        private void ApplyCommittedVisual(LimbBehaviour limb)
        {
            Sprite currentSprite = GetDisplayedSprite(limb);
            if (currentSprite == null)
            {
                return;
            }

            CommittedLimbVisual visual = FindCommittedVisual(limb, currentSprite);
            if (visual == null)
            {
                return;
            }

            ApplyCommittedVisual(limb, visual);
        }

        private void ApplyCommittedVisual(LimbBehaviour limb, CommittedLimbVisual visual)
        {
            if (!IsUsableLimb(limb) ||
                visual == null ||
                visual.Sprites == null ||
                visual.Sprites.Skin == null)
            {
                return;
            }

            SpriteRenderer physicalRenderer = limb.PhysicalBehaviour != null ? limb.PhysicalBehaviour.spriteRenderer : null;
            SpriteRenderer skinRenderer = limb.SkinMaterialHandler != null ? limb.SkinMaterialHandler.renderer : null;
            SpriteRenderer targetRenderer = skinRenderer != null ? skinRenderer : physicalRenderer;
            if (targetRenderer == null)
            {
                return;
            }

            bool targetSpriteApplied = targetRenderer.sprite == visual.Sprites.Skin;
            bool physicalSpriteApplied = physicalRenderer == null ||
                                         physicalRenderer == targetRenderer ||
                                         physicalRenderer.sprite == visual.Sprites.Skin;
            if (targetSpriteApplied && physicalSpriteApplied && visual.MaterialsApplied)
            {
                return;
            }

            if (targetRenderer.sprite != visual.Sprites.Skin)
            {
                targetRenderer.sprite = visual.Sprites.Skin;
            }

            if (physicalRenderer != null &&
                physicalRenderer != targetRenderer &&
                physicalRenderer.sprite != visual.Sprites.Skin)
            {
                physicalRenderer.sprite = visual.Sprites.Skin;
            }

            Material targetMaterial = targetRenderer.material;
            if (targetMaterial != null)
            {
                if (visual.FleshTexture != null)
                {
                    targetMaterial.SetTexture("_FleshTex", visual.FleshTexture);
                }

                if (visual.BoneTexture != null)
                {
                    targetMaterial.SetTexture("_BoneTex", visual.BoneTexture);
                }

                if (visual.DamageTexture != null)
                {
                    targetMaterial.SetTexture("_DamageTex", visual.DamageTexture);
                }
            }

            if (physicalRenderer != null && physicalRenderer != targetRenderer && physicalRenderer.material != null)
            {
                if (visual.FleshTexture != null)
                {
                    physicalRenderer.material.SetTexture("_FleshTex", visual.FleshTexture);
                }

                if (visual.BoneTexture != null)
                {
                    physicalRenderer.material.SetTexture("_BoneTex", visual.BoneTexture);
                }

                if (visual.DamageTexture != null)
                {
                    physicalRenderer.material.SetTexture("_DamageTex", visual.DamageTexture);
                }
            }

            ShatteredObjectSpriteInitialiser shattered;
            if (limb.TryGetComponent<ShatteredObjectSpriteInitialiser>(out shattered) && shattered != null)
            {
                try
                {
                    LimbSpriteCache.LimbSprites sprites = visual.Sprites;
                    shattered.UpdateSprites(sprites);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("[Hypermodified] Failed to reapply shattered limb sprites: " + exception);
                }
            }

            if (limb.SkinMaterialHandler != null)
            {
                limb.SkinMaterialHandler.Sync();
            }

            if (limb.PhysicalBehaviour != null)
            {
                limb.PhysicalBehaviour.RefreshOutline();
            }

            visual.MaterialsApplied = true;
        }

        private sealed class CommittedAccessoryVisual
        {
            public Sprite BaseSprite;
            public Color32[] BasePixels;
            public LimbSpriteState.LayerStackSnapshot LayerStack;
            public Texture2D DisplayTexture;
            public Sprite DisplaySprite;
        }

        private sealed class CommittedLimbVisual
        {
            public SpriteStateKey BaseStateKey;
            public SpriteStateKey CommittedStateKey;
            public Sprite BaseSprite;
            public Color32[] BasePixels;
            public LimbSpriteState.LayerStackSnapshot LayerStack;
            public Texture2D BoneTexture;
            public Texture2D DamageTexture;
            public Texture2D FleshTexture;
            public Texture2D SkinTexture;
            public bool OwnsBodyLayerTextures;
            public bool OwnsDamageTexture;
            public bool OwnsSourceSprite;
            public bool MaterialsApplied;
            public Sprite SourceSprite;
            public LimbSpriteCache.LimbSprites Sprites;
        }

        private struct SpriteStateKey
        {
            public ulong PixelHash;
            public int PivotX;
            public int PivotY;
            public int PixelsPerUnitTimesHundred;
            public int RectHeight;
            public int RectWidth;
            public int RectX;
            public int RectY;

            public bool IsValid
            {
                get { return PixelHash != 0UL && RectWidth > 0 && RectHeight > 0; }
            }

            public bool Matches(SpriteStateKey other)
            {
                return IsValid &&
                       other.IsValid &&
                       PixelHash == other.PixelHash &&
                       RectX == other.RectX &&
                       RectY == other.RectY &&
                       RectWidth == other.RectWidth &&
                       RectHeight == other.RectHeight &&
                       PivotX == other.PivotX &&
                       PivotY == other.PivotY &&
                       PixelsPerUnitTimesHundred == other.PixelsPerUnitTimesHundred;
            }
        }

        private CommittedLimbVisual FindCommittedVisual(LimbBehaviour limb, Sprite sprite)
        {
            List<CommittedLimbVisual> visuals;
            if (sprite == null || !committedVisuals.TryGetValue(limb, out visuals) || visuals == null)
            {
                return null;
            }

            SpriteStateKey currentStateKey = GetSpriteStateKey(sprite);
            if (!currentStateKey.IsValid)
            {
                return null;
            }

            for (int i = 0; i < visuals.Count; i++)
            {
                CommittedLimbVisual visual = visuals[i];
                if (visual != null &&
                    (visual.BaseStateKey.Matches(currentStateKey) || visual.CommittedStateKey.Matches(currentStateKey)))
                {
                    return visual;
                }
            }

            return null;
        }

        private SpriteStateKey GetSpriteStateKey(Sprite sprite)
        {
            if (sprite == null)
            {
                return default(SpriteStateKey);
            }

            int instanceId = sprite.GetInstanceID();
            SpriteStateKey cached;
            if (spriteStateCache.TryGetValue(instanceId, out cached) && cached.IsValid)
            {
                return cached;
            }

            SpriteStateKey computed = CreateSpriteStateKey(sprite);
            if (computed.IsValid)
            {
                spriteStateCache[instanceId] = computed;
            }

            return computed;
        }

        private static SpriteStateKey CreateSpriteStateKey(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null)
            {
                return default(SpriteStateKey);
            }

            Texture2D readable = LimbSpriteState.CreateReadableTextureCopy(sprite.texture);
            if (readable == null)
            {
                return default(SpriteStateKey);
            }

            Rect rect = sprite.rect;
            int x = Mathf.RoundToInt(rect.x);
            int y = Mathf.RoundToInt(rect.y);
            int width = Mathf.RoundToInt(rect.width);
            int height = Mathf.RoundToInt(rect.height);
            if (width <= 0 || height <= 0)
            {
                UnityEngine.Object.Destroy(readable);
                return default(SpriteStateKey);
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

            return new SpriteStateKey
            {
                PixelHash = hash,
                RectX = x,
                RectY = y,
                RectWidth = width,
                RectHeight = height,
                PivotX = Mathf.RoundToInt(sprite.pivot.x),
                PivotY = Mathf.RoundToInt(sprite.pivot.y),
                PixelsPerUnitTimesHundred = Mathf.RoundToInt(sprite.pixelsPerUnit * 100f)
            };
        }

        private static Sprite GetDisplayedSprite(LimbBehaviour limb)
        {
            if (limb == null)
            {
                return null;
            }

            if (limb.SkinMaterialHandler != null &&
                limb.SkinMaterialHandler.renderer != null &&
                limb.SkinMaterialHandler.renderer.sprite != null)
            {
                return limb.SkinMaterialHandler.renderer.sprite;
            }

            if (limb.PhysicalBehaviour != null &&
                limb.PhysicalBehaviour.spriteRenderer != null)
            {
                return limb.PhysicalBehaviour.spriteRenderer.sprite;
            }

            return null;
        }

        private bool AttachButtons()
        {
            if (person == null || person.Limbs == null)
            {
                return false;
            }

            bool hasAnyUsableLimb = false;
            bool attachedAllUsableLimbs = true;
            for (int i = 0; i < person.Limbs.Length; i++)
            {
                LimbBehaviour limb = person.Limbs[i];
                if (!IsUsableLimb(limb))
                {
                    continue;
                }

                hasAnyUsableLimb = true;
                if (!TryAttachContextMenuButton(limb))
                {
                    attachedAllUsableLimbs = false;
                }
            }

            return hasAnyUsableLimb && attachedAllUsableLimbs;
        }

        private bool TryAttachContextMenuButton(LimbBehaviour limb)
        {
            return false;
        }

        public static bool IsUsableLimb(LimbBehaviour limb)
        {
            return limb != null &&
                   limb.gameObject != null &&
                   limb.gameObject.scene.IsValid() &&
                   limb.gameObject.scene.isLoaded &&
                   limb.Person != null &&
                   limb.PhysicalBehaviour != null &&
                   GetDisplayedSprite(limb) != null;
        }

        private LimbBehaviour FindHeadLikeLimb()
        {
            if (person == null || person.Limbs == null)
            {
                return null;
            }

            for (int i = 0; i < person.Limbs.Length; i++)
            {
                LimbBehaviour limb = person.Limbs[i];

                if (!IsUsableLimb(limb))
                {
                    continue;
                }

                string name = GetLimbDisplayName(limb).ToLowerInvariant();
                if (name.Contains("head"))
                {
                    return limb;
                }
            }

            return null;
        }

        private static string CleanName(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName))
            {
                return string.Empty;
            }

            return rawName.Replace("(Clone)", string.Empty).Trim();
        }
    }
}

// Originally uploaded by 'hypermodified'. Do not reupload without their explicit permission.
