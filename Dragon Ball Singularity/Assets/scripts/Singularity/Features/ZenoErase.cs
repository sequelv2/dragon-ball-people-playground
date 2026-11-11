using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DBPU
{
    // The Zen-O Button the Omni-King gave Goku. One press and the universe is erased:
    // every fighter (optionally every object) dissolves into white light. Angels survive,
    // as they do in the Tournament of Power.
    public sealed class ZenoEraseModule : SingularityModule
    {
        public override string Id { get { return "zeno"; } }
        public override string Title { get { return "Zen-O Button"; } }
        public override string Summary { get { return "Spawnable item. Press it to erase every fighter (or everything) in white light. Angels are spared."; } }

        public static Sprite ButtonSprite;
        public const string SfxPress = "Assets/SFX/Singularity/Zeno/Press.wav";
        public const string SfxErase = "Assets/SFX/Singularity/Zeno/Erase.wav";

        public override void Boot()
        {
            try { ButtonSprite = ModAPI.LoadSprite("Assets/Sprites/Singularity/Items/ZenoButton.png"); }
            catch (Exception ex) { Debug.LogWarning("[Singularity] Zeno sprite: " + ex.Message); }
            DbpuFx.PreloadExtra(SfxPress, SfxErase);
        }

        public override void RegisterSpawnables()
        {
            DBPU.ModAPIPlus.CreateObject(
                "Brick",
                "Zen-O Button",
                "A gift from the Omni-King. Press it and everything is erased. Angels are spared.",
                "Zeno Button",
                (Instance) =>
                {
                    var sr = Instance.GetComponent<SpriteRenderer>();
                    if (sr && ButtonSprite) sr.sprite = ButtonSprite;
                    Instance.gameObject.FixColliders();
                    var phys = Instance.GetComponent<PhysicalBehaviour>();
                    if (phys) phys.RecalculateMassBasedOnSize();
                    var button = Instance.AddComponent<ZenoButtonBehaviour>();
                    Instance.AddComponent<UseEventTrigger>().Action = button.Press;
                    if (phys)
                        phys.ContextMenuOptions.Buttons.Add(new ContextMenuButton("ZenoEraseMode",
                            (Func<string>)(() => button.EraseEverything ? "Erase: everything" : "Erase: fighters only"),
                            "Choose what the Omni-King erases",
                            () => { button.EraseEverything = !button.EraseEverything; ModAPI.Notify(button.EraseEverything ? "Zen-O Button will erase everything." : "Zen-O Button will erase fighters only."); }));
                },
                "Items",
                DBPU.UniqueTag + DBPU.ItemsTag + "006");
        }
    }

    public sealed class ZenoButtonBehaviour : MonoBehaviour
    {
        public bool EraseEverything;
        public void Press()
        {
            var module = Singularity.Get<ZenoEraseModule>();
            if (module == null || !module.Enabled) { ModAPI.Notify("The Zen-O Button is switched off (Singularity Features)."); return; }
            if (ZenoErasure.Running) return;
            DbpuFx.Sound(transform.position, ZenoEraseModule.SfxPress, 1f);
            SingularityFx.Flash(transform.position, new Color(.7f, .95f, 1f, .9f), .7f, .35f);
            var go = new GameObject("DBPU Zen-O erasure");
            go.AddComponent<ZenoErasure>().Begin(transform, EraseEverything);
        }
    }

    public sealed class ZenoErasure : MonoBehaviour
    {
        static int running;
        public static bool Running { get { return running > 0; } }
        static readonly Color White = new Color(.92f, .98f, 1f, 1f), Cyan = new Color(.55f, .9f, 1f, 1f);
        Transform button; bool everything;

        public void Begin(Transform source, bool eraseEverything)
        {
            button = source; everything = eraseEverything;
            running++;
            StartCoroutine(Run());
        }

        void OnDestroy() { running = Mathf.Max(0, running - 1); }

        IEnumerator Run()
        {
            yield return new WaitForSecondsRealtime(.7f);
            SingularityAnnouncer.Show("ERASE.", "THE OMNI-KING HAS SPOKEN", 3.2f, White);
            SingularityVeil.Show("zeno-flash", White, .92f, .08f);
            Vector2 at = button ? (Vector2)button.position : Vector2.zero;
            DbpuFx.Sound(at, ZenoEraseModule.SfxErase, 1f);
            DbpuFx.Shake(at, 2f);
            yield return new WaitForSecondsRealtime(.18f);
            SingularityVeil.Hide("zeno-flash", 1.1f);

            var targets = new List<PersonBehaviour>();
            bool angelSpared = false;
            foreach (var p in FindObjectsOfType<PersonBehaviour>())
            {
                if (!p || p.GetComponent<HakaiDissolveMarker>()) continue;
                if (SingularityFighters.IsAngel(p)) { angelSpared = true; DbpuWorldLabel.Show(p, "SPARED", "ANGELS ARE NOT ERASED", 3f, -1f, Cyan); continue; }
                targets.Add(p);
            }
            foreach (var p in targets)
            {
                if (!p) continue;
                Vector2 c = DbpuFx.Centre(p);
                SingularityFx.Flash(c, Cyan, 2.4f, .6f);
                SingularityFx.Ring(c, White, 2.6f, .55f, 1f);
                HakaiDissolve.DissolvePerson(p, DissolveStyle.Erase);
                yield return new WaitForSecondsRealtime(UnityEngine.Random.Range(.03f, .12f));
            }
            if (everything)
            {
                var roots = new HashSet<Transform>();
                foreach (var phys in FindObjectsOfType<PhysicalBehaviour>())
                {
                    if (!phys || phys.gameObject.isStatic || phys.isDisintegrated) continue;
                    var root = phys.transform.root;
                    if (button && root == button.root) continue;
                    if (root.GetComponent<PersonBehaviour>() || root.GetComponent<EarthDB>() || root.GetComponent<HakaiDissolveMarker>()) continue;
                    var body = phys.GetComponent<Rigidbody2D>();
                    if (body && body.bodyType == RigidbodyType2D.Static) continue;
                    if (!roots.Add(root)) continue;
                    HakaiDissolve.DissolveObject(phys, phys.transform.position, DissolveStyle.Erase);
                }
            }
            if (targets.Count == 0 && !everything)
                SingularityAnnouncer.Show("NOTHING TO ERASE", angelSpared ? "ONLY ANGELS REMAIN" : "THE UNIVERSE IS ALREADY EMPTY", 2.5f, White);
            yield return new WaitForSecondsRealtime(2f);
            Destroy(gameObject);
        }
    }
}
