using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DBPU
{
    // Opaque pieces of the actual character sprite build each native limb. This is
    // presentation only; ownership, matter, damage and native joints stay in the core.
    [SkipSerialisation]
    [DefaultExecutionOrder(10000)]
    public sealed class BuuAssemblyView : MonoBehaviour, Messages.IOnBeforeSerialise
    {
        public int[] PieceCounts { get; private set; }
        public int[] ArrivedCounts { get; private set; }
        public int[] SourceOpaquePixelCounts { get; private set; }
        public int[] CoveredPixelCounts { get; private set; }
        public int[] OverlappingPixelCounts { get; private set; }
        public bool PixelAlignedSeams { get; private set; }
        public bool ExactPixelCoverage { get; private set; }
        public bool IsComplete { get; private set; } = true;
        public bool IsBuilding { get; private set; }
        PersonBehaviour person;
        Group[] groups;
        Vector2[] sources;
        float lastProgress;

        sealed class Pixels
        {
            public Color32[] Data;
            public int Width, Height;
        }
        sealed class Image
        {
            public SpriteRenderer Source;
            public Sprite Sprite;
            public Texture2D Texture;
            public Vector2 Min, Max, UvOrigin, UvX, UvY;
            public readonly List<Vector2> Opaque = new List<Vector2>();
            public bool[] Visible;
            public int Width, Height, VisibleCount;
            public float Area;
            public Vector2 UV(Vector2 local) { return UvOrigin + UvX * local.x + UvY * local.y; }
        }
        sealed class Cell
        {
            public Vector2 Centre, From;
            public float Delay, Duration, Rotation, Side, Arc;
            public int First, Count, MotionVariant;
        }
        sealed class Group
        {
            public LimbBehaviour Limb;
            public Renderer[] Native;
            public bool[] Enabled;
            public bool[] ForcedOff;
            public SpriteRenderer Source;
            public MeshRenderer View;
            public GameObject Root;
            public Mesh Mesh;
            public Material Material;
            public Cell[] Cells;
            public Vector2[] Original;
            public Vector3[] Vertices;
            public Color[] Colours;
            public Vector2 Flip;
            public bool Complete;
            public bool PixelAligned = true;
            public int CoveredPixels, Overlaps;
        }
        struct Front { public int Pixel, Owner; public float Cost; }
        struct PixelRect { public int X0, X1, Y0, Y1; }

        public static BuuAssemblyView Attach(PersonBehaviour owner)
        {
            if (!owner) return null;
            var found = owner.GetComponentInChildren<BuuAssemblyView>(true);
            if (found) { found.person = owner; return found; }
            var root = new GameObject("Majin body piece construction");
            root.transform.SetParent(owner.transform, false);
            root.AddComponent<Optout>();
            var result = root.AddComponent<BuuAssemblyView>();
            result.person = owner;
            return result;
        }

        static float Hash(int value) { return Mathf.Repeat(Mathf.Sin(value * 127.17f + 31.73f) * 43758.5453f, 1f); }
        static bool Finite(Vector2 value) { return !float.IsNaN(value.x) && !float.IsNaN(value.y) && !float.IsInfinity(value.x) && !float.IsInfinity(value.y); }

        public void Begin(Vector2[] ownActualSourcePositions, bool[] rebuildingLimbs)
        {
            Cancel(false);
            ReleaseGroups();
            if (!person || person.Limbs == null) return;
            int length = person.Limbs.Length;
            PieceCounts = new int[length]; ArrivedCounts = new int[length];
            SourceOpaquePixelCounts = new int[length]; CoveredPixelCounts = new int[length]; OverlappingPixelCounts = new int[length];
            PixelAlignedSeams = true; ExactPixelCoverage = true;
            var validSources = new List<Vector2>();
            if (ownActualSourcePositions != null)
                foreach (var source in ownActualSourcePositions) if (Finite(source)) validSources.Add(source);
            if (validSources.Count == 0) { IsComplete = true; return; }
            sources = validSources.ToArray();
            groups = new Group[length];
            var pixels = new Dictionary<Texture2D, Pixels>();
            var images = new Image[length];
            float biggest = .0001f;
            for (int i = 0; i < length; i++)
            {
                var limb = person.Limbs[i]; if (!limb || !limb.PhysicalBehaviour) continue;
                var source = limb.PhysicalBehaviour.spriteRenderer;
                if (!source || !source.sprite) continue;
                try
                {
                    images[i] = ReadImage(source, pixels);
                    biggest = Mathf.Max(biggest, images[i].Area * Mathf.Abs(limb.transform.lossyScale.x * limb.transform.lossyScale.y));
                }
                catch (Exception error) { Debug.LogWarning("[Majin assembly] Sprite partition unavailable: " + error.Message); }
            }
            for (int i = 0; i < length; i++)
            {
                if (rebuildingLimbs == null || i >= rebuildingLimbs.Length || !rebuildingLimbs[i] || images[i] == null) continue;
                var limb = person.Limbs[i];
                float area = images[i].Area * Mathf.Abs(limb.transform.lossyScale.x * limb.transform.lossyScale.y);
                int count = Mathf.Clamp(Mathf.RoundToInt(5f + 10f * Mathf.Pow(Mathf.Clamp01(area / biggest), .8f)), 5, 15);
                try
                {
                    groups[i] = BuildGroup(limb, images[i], count, i);
                    PieceCounts[i] = groups[i].Cells.Length;
                    SourceOpaquePixelCounts[i] = images[i].VisibleCount;
                    CoveredPixelCounts[i] = groups[i].CoveredPixels;
                    OverlappingPixelCounts[i] = groups[i].Overlaps;
                    PixelAlignedSeams &= groups[i].PixelAligned;
                    ExactPixelCoverage &= groups[i].CoveredPixels == images[i].VisibleCount && groups[i].Overlaps == 0;
                }
                catch (Exception error) { PixelAlignedSeams = false; ExactPixelCoverage = false; Debug.LogWarning("[Majin assembly] Limb pieces unavailable: " + error); }
            }
            IsComplete = false; IsBuilding = true; lastProgress = 0f;
            Tick(0f);
        }

        static Pixels ReadPixels(Texture2D texture)
        {
            if (texture.isReadable) return new Pixels { Data = texture.GetPixels32(), Width = texture.width, Height = texture.height };
            var temporary = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            RenderTexture previous = RenderTexture.active;
            Texture2D readable = null;
            try
            {
                Graphics.Blit(texture, temporary);
                RenderTexture.active = temporary;
                readable = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false, true);
                readable.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                readable.Apply(false, false);
                return new Pixels { Data = readable.GetPixels32(), Width = texture.width, Height = texture.height };
            }
            finally
            {
                RenderTexture.active = previous;
                if (readable) Destroy(readable);
                RenderTexture.ReleaseTemporary(temporary);
            }
        }

        static Image ReadImage(SpriteRenderer source, Dictionary<Texture2D, Pixels> cache)
        {
            Sprite sprite = source.sprite;
            var image = new Image { Source = source, Sprite = sprite, Texture = sprite.texture };
            float ppu = sprite.pixelsPerUnit;
            image.Min = -sprite.pivot / ppu;
            image.Max = (sprite.rect.size - sprite.pivot) / ppu;
            var vertices = sprite.vertices; var uv = sprite.uv;
            bool mapped = false;
            for (int a = 0; a < vertices.Length && !mapped; a++)
            for (int b = a + 1; b < vertices.Length && !mapped; b++)
            for (int c = b + 1; c < vertices.Length && !mapped; c++)
            {
                Vector2 p = vertices[b] - vertices[a], q = vertices[c] - vertices[a];
                float determinant = p.x * q.y - p.y * q.x;
                if (Mathf.Abs(determinant) < 1e-8f) continue;
                Vector2 du = uv[b] - uv[a], dv = uv[c] - uv[a];
                image.UvX = (du * q.y - dv * p.y) / determinant;
                image.UvY = (dv * p.x - du * q.x) / determinant;
                image.UvOrigin = uv[a] - image.UvX * vertices[a].x - image.UvY * vertices[a].y;
                mapped = true;
            }
            if (!mapped) throw new InvalidOperationException("Degenerate source sprite UVs");
            Pixels pixels;
            if (!cache.TryGetValue(image.Texture, out pixels)) { pixels = ReadPixels(image.Texture); cache[image.Texture] = pixels; }
            int width = Mathf.Max(1, Mathf.RoundToInt(sprite.rect.width)), height = Mathf.Max(1, Mathf.RoundToInt(sprite.rect.height));
            image.Width = width; image.Height = height; image.Visible = new bool[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                Vector2 local = new Vector2((x + .5f - sprite.pivot.x) / ppu, (y + .5f - sprite.pivot.y) / ppu);
                Vector2 textureUV = image.UV(local);
                int px = Mathf.Clamp(Mathf.FloorToInt(textureUV.x * pixels.Width), 0, pixels.Width - 1);
                int py = Mathf.Clamp(Mathf.FloorToInt(textureUV.y * pixels.Height), 0, pixels.Height - 1);
                byte alpha = pixels.Data[py * pixels.Width + px].a;
                if (alpha > 0) { image.Visible[y * width + x] = true; image.VisibleCount++; }
                if (alpha > 48) image.Opaque.Add(local);
            }
            if (image.Opaque.Count == 0) throw new InvalidOperationException("Source sprite has no opaque pixels");
            image.Area = image.VisibleCount / (ppu * ppu);
            return image;
        }

        static Vector2[] Seeds(Image image, int count, int variation)
        {
            var result = new Vector2[count];
            result[0] = image.Opaque[Mathf.Min(image.Opaque.Count - 1, Mathf.FloorToInt(Hash(variation + 73) * image.Opaque.Count))];
            for (int k = 1; k < count; k++)
            {
                float best = -1f; Vector2 selected = image.Opaque[0];
                foreach (Vector2 point in image.Opaque)
                {
                    float nearest = float.PositiveInfinity;
                    for (int j = 0; j < k; j++) nearest = Mathf.Min(nearest, (point - result[j]).sqrMagnitude);
                    if (nearest <= best) continue;
                    best = nearest; selected = point;
                }
                // Tiny sprites can have fewer opaque pixels than pieces. Subpixel
                // seeds stay inside their opaque pixel, so every cell remains visible.
                if (best < 1e-10f) selected += new Vector2(Hash(k * 7 + variation) - .5f, Hash(k * 13 + variation) - .5f) * (.4f / image.Sprite.pixelsPerUnit);
                result[k] = selected;
            }
            return result;
        }

        static void Push(List<Front> heap, Front value)
        {
            int index = heap.Count; heap.Add(value);
            while (index > 0)
            {
                int parent = (index - 1) / 2;
                if (heap[parent].Cost <= value.Cost) break;
                heap[index] = heap[parent]; index = parent;
            }
            heap[index] = value;
        }
        static Front Pop(List<Front> heap)
        {
            Front result = heap[0], last = heap[heap.Count - 1];
            heap.RemoveAt(heap.Count - 1);
            if (heap.Count == 0) return result;
            int index = 0;
            while (index * 2 + 1 < heap.Count)
            {
                int child = index * 2 + 1;
                if (child + 1 < heap.Count && heap[child + 1].Cost < heap[child].Cost) child++;
                if (heap[child].Cost >= last.Cost) break;
                heap[index] = heap[child]; index = child;
            }
            heap[index] = last;
            return result;
        }

        static int[] PixelRegions(Image image, Vector2[] seeds, int variation)
        {
            int width = image.Width, height = image.Height, length = width * height;
            var owners = new int[length]; var distances = new float[length];
            for (int i = 0; i < length; i++) { owners[i] = -1; distances[i] = float.PositiveInfinity; }
            var heap = new List<Front>(length);
            float ppu = image.Sprite.pixelsPerUnit; Vector2 pivot = image.Sprite.pivot;
            for (int i = 0; i < seeds.Length; i++)
            {
                int x = Mathf.Clamp(Mathf.FloorToInt(seeds[i].x * ppu + pivot.x), 0, width - 1);
                int y = Mathf.Clamp(Mathf.FloorToInt(seeds[i].y * ppu + pivot.y), 0, height - 1);
                int pixel = y * width + x;
                if (owners[pixel] >= 0)
                {
                    pixel = -1;
                    foreach (Vector2 point in image.Opaque)
                    {
                        int candidate = Mathf.FloorToInt(point.y * ppu + pivot.y) * width + Mathf.FloorToInt(point.x * ppu + pivot.x);
                        if (candidate >= 0 && candidate < length && owners[candidate] < 0) { pixel = candidate; break; }
                    }
                    if (pixel < 0) throw new InvalidOperationException("Not enough opaque source pixels for independent pieces");
                }
                owners[pixel] = i; distances[pixel] = 0f;
                Push(heap, new Front { Pixel = pixel, Owner = i, Cost = 0f });
            }
            // A noisy four-neighbour growth front makes each tear a connected
            // staircase of complete source pixels, never a diagonal vector cut.
            while (heap.Count > 0)
            {
                Front current = Pop(heap);
                if (current.Owner != owners[current.Pixel] || current.Cost > distances[current.Pixel] + .00001f) continue;
                int x = current.Pixel % width, y = current.Pixel / width;
                for (int direction = 0; direction < 4; direction++)
                {
                    int nx = x + (direction == 0 ? -1 : direction == 1 ? 1 : 0);
                    int ny = y + (direction == 2 ? -1 : direction == 3 ? 1 : 0);
                    if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                    int neighbour = ny * width + nx;
                    if (!image.Visible[neighbour]) continue;
                    int edge = Mathf.Min(current.Pixel, neighbour) * 5 + (direction < 2 ? 1 : 3) + variation * 179;
                    float irregular = Hash(edge * 7 + 13);
                    float grain = Hash((nx / 3 + ny / 3 * width) * 31 + variation * 19);
                    float candidate = current.Cost + .58f + irregular * 2.6f + grain * .7f;
                    if (candidate >= distances[neighbour]) continue;
                    owners[neighbour] = current.Owner; distances[neighbour] = candidate;
                    Push(heap, new Front { Pixel = neighbour, Owner = current.Owner, Cost = candidate });
                }
            }
            // A sprite may contain tiny detached alpha islands. Keep their pixels
            // intact with their nearest piece rather than silently dropping them.
            var queue = new int[length];
            for (int start = 0; start < length; start++)
            {
                if (!image.Visible[start] || owners[start] >= 0) continue;
                int head = 0, tail = 1; queue[0] = start; owners[start] = -2;
                Vector2 centre = Vector2.zero;
                while (head < tail)
                {
                    int pixel = queue[head++], x = pixel % width, y = pixel / width;
                    centre += new Vector2((x + .5f - pivot.x) / ppu, (y + .5f - pivot.y) / ppu);
                    for (int d = 0; d < 4; d++)
                    {
                        int nx = x + (d == 0 ? -1 : d == 1 ? 1 : 0), ny = y + (d == 2 ? -1 : d == 3 ? 1 : 0);
                        if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                        int next = ny * width + nx;
                        if (!image.Visible[next] || owners[next] != -1) continue;
                        owners[next] = -2; queue[tail++] = next;
                    }
                }
                centre /= tail;
                int closest = 0; float best = float.PositiveInfinity;
                for (int i = 0; i < seeds.Length; i++)
                { float distance = (seeds[i] - centre).sqrMagnitude; if (distance < best) { best = distance; closest = i; } }
                for (int i = 0; i < tail; i++) owners[queue[i]] = closest;
            }
            return owners;
        }

        Group BuildGroup(LimbBehaviour limb, Image image, int count, int limbIndex)
        {
            var group = new Group { Limb = limb, Source = image.Source };
            var native = new List<Renderer>();
            foreach (var renderer in limb.GetComponentsInChildren<Renderer>(true))
                if (!renderer.gameObject.name.StartsWith("Majin opaque sprite pieces ", StringComparison.Ordinal)
                    && renderer.GetComponentInParent<LimbBehaviour>() == limb) native.Add(renderer);
            group.Native = native.ToArray();
            group.Enabled = new bool[group.Native.Length];
            group.ForcedOff = new bool[group.Native.Length];
            for (int i = 0; i < group.Native.Length; i++)
            {
                group.Enabled[i] = group.Native[i].enabled;
                group.ForcedOff[i] = group.Native[i].forceRenderingOff;
            }
            group.Flip = new Vector2(image.Source.flipX ? -1f : 1f, image.Source.flipY ? -1f : 1f);
            var seeds = Seeds(image, count, limbIndex * 59);
            int[] owners = PixelRegions(image, seeds, limbIndex * 59);
            var rectangles = new List<PixelRect>[count];
            var previousRuns = new Dictionary<long, int>[count];
            var centres = new Vector2[count]; var masses = new int[count];
            for (int i = 0; i < count; i++) { rectangles[i] = new List<PixelRect>(); previousRuns[i] = new Dictionary<long, int>(); }
            var coverage = new byte[image.Visible.Length];
            float ppu = image.Sprite.pixelsPerUnit; Vector2 pivot = image.Sprite.pivot;
            for (int y = 0; y < image.Height; y++)
            for (int x = 0; x < image.Width;)
            {
                int pixel = y * image.Width + x;
                if (!image.Visible[pixel]) { x++; continue; }
                int owner = owners[pixel];
                if (owner < 0 || owner >= count) throw new InvalidOperationException("Unowned visible source pixel");
                int start = x;
                while (x < image.Width && image.Visible[y * image.Width + x] && owners[y * image.Width + x] == owner)
                {
                    centres[owner] += new Vector2((x + .5f - pivot.x) / ppu, (y + .5f - pivot.y) / ppu);
                    masses[owner]++; x++;
                }
                long key = (long)start * (image.Width + 1) + x;
                int previous;
                if (previousRuns[owner].TryGetValue(key, out previous) && rectangles[owner][previous].Y1 == y)
                {
                    var rectangle = rectangles[owner][previous]; rectangle.Y1 = y + 1;
                    rectangles[owner][previous] = rectangle;
                }
                else
                {
                    previousRuns[owner][key] = rectangles[owner].Count;
                    rectangles[owner].Add(new PixelRect { X0 = start, X1 = x, Y0 = y, Y1 = y + 1 });
                }
            }
            foreach (var region in rectangles)
                foreach (PixelRect rectangle in region)
                    for (int y = rectangle.Y0; y < rectangle.Y1; y++)
                        for (int x = rectangle.X0; x < rectangle.X1; x++) coverage[y * image.Width + x]++;
            for (int i = 0; i < coverage.Length; i++)
            {
                if (!image.Visible[i]) continue;
                if (coverage[i] > 0) group.CoveredPixels++;
                if (coverage[i] > 1) group.Overlaps += coverage[i] - 1;
            }
            var positions = new List<Vector2>(image.VisibleCount * 2);
            var uvs = new List<Vector2>(image.VisibleCount * 2);
            var triangles = new List<int>(image.VisibleCount * 3);
            var cells = new List<Cell>(count);
            for (int i = 0; i < seeds.Length; i++)
            {
                if (masses[i] == 0) throw new InvalidOperationException("A pixel tear piece has no opaque source pixels");
                int first = positions.Count;
                foreach (PixelRect rectangle in rectangles[i])
                {
                    float x0 = (rectangle.X0 - pivot.x) / ppu, x1 = (rectangle.X1 - pivot.x) / ppu;
                    float y0 = (rectangle.Y0 - pivot.y) / ppu, y1 = (rectangle.Y1 - pivot.y) / ppu;
                    int start = positions.Count;
                    AddPixelVertex(group, image, positions, uvs, new Vector2(x0, y0));
                    AddPixelVertex(group, image, positions, uvs, new Vector2(x1, y0));
                    AddPixelVertex(group, image, positions, uvs, new Vector2(x1, y1));
                    AddPixelVertex(group, image, positions, uvs, new Vector2(x0, y1));
                    triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
                    triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
                }
                int serial = limbIndex * 97 + i * 17 + 41;
                float delay = LimbDelay(limbIndex) + Hash(serial) * .14f;
                cells.Add(new Cell
                {
                    Centre = Vector2.Scale(centres[i] / masses[i], group.Flip), From = sources[(i * 7 + limbIndex * 11) % sources.Length],
                    Delay = delay, Duration = .34f + Hash(serial + 3) * .17f,
                    Rotation = (Hash(serial + 7) - .5f) * 330f, Side = (Hash(serial + 11) - .5f) * .42f,
                    Arc = .13f + Hash(serial + 13) * .24f, First = first, Count = positions.Count - first, MotionVariant = serial % 8
                });
            }
            if (cells.Count < 1) throw new InvalidOperationException("No visible pixel tear pieces");
            group.Cells = cells.ToArray(); group.Original = positions.ToArray();
            group.Vertices = new Vector3[positions.Count]; group.Colours = new Color[positions.Count];
            Color spriteColour = image.Source.color; spriteColour.a = 1f;
            for (int i = 0; i < group.Colours.Length; i++) group.Colours[i] = spriteColour;
            group.Root = new GameObject("Majin opaque sprite pieces " + limb.name);
            group.Root.transform.SetParent(limb.transform, false);
            group.Root.AddComponent<Optout>();
            group.Mesh = new Mesh { name = "Majin pixel tears " + limb.name }; group.Mesh.MarkDynamic();
            group.Mesh.indexFormat = positions.Count > 65530 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            group.Mesh.vertices = group.Vertices; group.Mesh.uv = uvs.ToArray(); group.Mesh.colors = group.Colours;
            group.Mesh.triangles = triangles.ToArray();
            group.Root.AddComponent<MeshFilter>().sharedMesh = group.Mesh;
            group.View = group.Root.AddComponent<MeshRenderer>();
            Material sourceMaterial = image.Source.sharedMaterial;
            group.Material = sourceMaterial ? new Material(sourceMaterial) : new Material(Shader.Find("Sprites/Default"));
            group.Material.name = "Majin exact sprite pieces " + limb.name;
            group.Material.SetTexture("_MainTex", image.Texture);
            if (group.Material.HasProperty("_Color"))
            {
                Color materialColour = group.Material.GetColor("_Color"); materialColour.a = 1f;
                group.Material.SetColor("_Color", materialColour);
            }
            if (limb.SkinMaterialHandler)
            {
                // These are newly assembled patches. Old limb-local wound arrays
                // would otherwise cut arbitrary holes into pieces while they fly.
                group.Material.SetInt("_DamagePointCount", 0);
                group.Material.SetFloat("_AcidProgress", 0f);
                group.Material.SetFloat("_BurnProgress", 0f);
                group.Material.SetFloat("_RottenProgress", 0f);
            }
            if (group.Material.HasProperty("_Flip")) group.Material.SetVector("_Flip", new Vector4(1, 1, 1, 1));
            group.View.sharedMaterial = group.Material;
            group.View.sortingLayerID = image.Source.sortingLayerID;
            group.View.sortingOrder = image.Source.sortingOrder;
            group.View.shadowCastingMode = ShadowCastingMode.Off; group.View.receiveShadows = false;
            SetNative(group, false);
            return group;
        }

        static void AddPixelVertex(Group group, Image image, List<Vector2> positions, List<Vector2> uvs, Vector2 local)
        {
            Vector2 pixel = local * image.Sprite.pixelsPerUnit + image.Sprite.pivot;
            if (Mathf.Abs(pixel.x - Mathf.Round(pixel.x)) > .001f || Mathf.Abs(pixel.y - Mathf.Round(pixel.y)) > .001f)
                group.PixelAligned = false;
            positions.Add(Vector2.Scale(local, group.Flip));
            uvs.Add(image.UV(local));
        }

        static float LimbDelay(int index)
        {
            if (index == 0) return .24f;
            if (index <= 3) return .015f + (3 - index) * .035f;
            if (index <= 9) return .10f + (index - 4) % 3 * .025f + (index >= 7 ? .02f : 0f);
            return .16f + (index - 10) * .022f;
        }

        static void SetNative(Group group, bool visible)
        {
            if (group.Native == null) return;
            bool valid = group.Limb && group.Limb.gameObject.activeInHierarchy && group.Limb.PhysicalBehaviour && !group.Limb.PhysicalBehaviour.isDisintegrated;
            for (int i = 0; i < group.Native.Length; i++)
                if (group.Native[i])
                {
                    group.Native[i].enabled = visible && valid && group.Enabled[i];
                    // Barrage and other presentation helpers may restore enabled in
                    // their Update. Keep the unfinished whole part unrenderable even
                    // to a camera explicitly rendered before our late-frame gate.
                    group.Native[i].forceRenderingOff = !visible || !valid || group.ForcedOff[i];
                }
        }

        void LateUpdate()
        {
            if (!IsBuilding || groups == null) return;
            foreach (var group in groups)
            {
                if (group == null) continue;
                SetNative(group, group.Complete);
                if (group.View)
                    group.View.enabled = !group.Complete && group.Limb && group.Limb.gameObject.activeInHierarchy
                        && group.Limb.PhysicalBehaviour && !group.Limb.PhysicalBehaviour.isDisintegrated;
            }
        }

        public bool IsLimbComplete(int index)
        {
            return groups == null || index < 0 || index >= groups.Length || groups[index] == null || groups[index].Complete;
        }

        public void Tick(float progress)
        {
            if (!IsBuilding || groups == null || !person || float.IsNaN(progress) || float.IsInfinity(progress)) return;
            progress = Mathf.Clamp01(Mathf.Max(lastProgress, progress)); lastProgress = progress;
            bool complete = true;
            for (int i = 0; i < groups.Length; i++)
            {
                var group = groups[i]; if (group == null || group.Complete) continue;
                if (!group.Limb || !group.View || !group.Limb.gameObject.activeInHierarchy || group.Limb.PhysicalBehaviour.isDisintegrated)
                {
                    if (group.View) group.View.enabled = false;
                    complete = false; continue;
                }
                SetNative(group, false);
                int arrived = 0;
                foreach (var cell in group.Cells)
                {
                    float flight = Mathf.Clamp01((progress - cell.Delay) / cell.Duration);
                    BuuAssemblyMotion.Pose motion = BuuAssemblyMotion.Sample(flight, cell.MotionVariant);
                    Vector2 target = group.Limb.transform.TransformPoint(cell.Centre);
                    Vector2 path = target - cell.From, side = path.sqrMagnitude > .001f ? new Vector2(-path.y, path.x).normalized : Vector2.right;
                    float distance = path.magnitude;
                    Vector2 centre = Vector2.LerpUnclamped(cell.From, target, motion.Approach)
                        + Vector2.up * motion.Lift * Mathf.Clamp(distance * .3f + cell.Arc, .12f, 1f)
                        + side * motion.Side * Mathf.Clamp(distance * .4f, .1f, 1f) * Mathf.Sign(cell.Side);
                    float rotation = cell.Rotation * (1f - Mathf.Clamp01(motion.Approach)) + motion.Twist;
                    centre += (Vector2)group.Limb.transform.TransformVector(cell.Centre.normalized) * (.0045f * motion.Settle);
                    if (flight >= 1f)
                    {
                        arrived++;
                    }
                    Quaternion turn = Quaternion.Euler(0, 0, rotation);
                    for (int k = 0; k < cell.Count; k++)
                    {
                        int vertex = cell.First + k;
                        Vector3 world = (Vector3)centre + turn * group.Limb.transform.TransformVector(group.Original[vertex] - cell.Centre);
                        world.z = group.Limb.transform.position.z;
                        group.Vertices[vertex] = group.Limb.transform.InverseTransformPoint(world);
                    }
                }
                ArrivedCounts[i] = arrived;
                if (arrived == group.Cells.Length)
                {
                    group.Complete = true; group.View.enabled = false;
                    SetNative(group, true);
                }
                else
                {
                    complete = false; group.View.enabled = true;
                    group.Mesh.vertices = group.Vertices;
                    group.Mesh.RecalculateBounds();
                }
            }
            IsComplete = complete;
            if (complete) IsBuilding = false;
        }

        public void Finish()
        {
            if (groups != null)
                for (int i = 0; i < groups.Length; i++)
                {
                    var group = groups[i]; if (group == null) continue;
                    group.Complete = true;
                    if (group.View) group.View.enabled = false;
                    SetNative(group, true);
                    if (ArrivedCounts != null && i < ArrivedCounts.Length) ArrivedCounts[i] = group.Cells.Length;
                }
            IsBuilding = false; IsComplete = true;
        }
        public void Cancel(bool restoreNative = false)
        {
            if (restoreNative) { Finish(); return; }
            if (groups != null) foreach (var group in groups)
                if (group != null)
                {
                    if (group.View) group.View.enabled = false;
                    ReleaseNativeForcing(group);
                }
            IsBuilding = false; IsComplete = false;
        }
        static void ReleaseNativeForcing(Group group)
        {
            if (group.Native == null || group.ForcedOff == null) return;
            for (int i = 0; i < group.Native.Length; i++)
                if (group.Native[i]) group.Native[i].forceRenderingOff = group.ForcedOff[i];
        }
        public void OnBeforeSerialise() { if (IsBuilding) Finish(); }
        void ReleaseGroups(bool retireForReuse = true)
        {
            if (groups == null) return;
            foreach (var group in groups)
            {
                if (group == null) continue;
                ReleaseNativeForcing(group);
                if (group.Root)
                {
                    group.Root.SetActive(false);
                    // During Begin, queued-for-destruction helpers must stop matching
                    // a native limb's current construction. During OnDestroy Unity
                    // is already dismantling the hierarchy and forbids reparenting.
                    if (retireForReuse)
                    {
                        group.Root.name = "Retired Majin construction pieces " + group.Root.GetInstanceID();
                        group.Root.transform.SetParent(transform, false);
                    }
                    Destroy(group.Root);
                }
                if (group.Mesh) Destroy(group.Mesh);
                if (group.Material) Destroy(group.Material);
            }
            groups = null;
        }
        void OnDestroy() { ReleaseGroups(false); }
    }
}
