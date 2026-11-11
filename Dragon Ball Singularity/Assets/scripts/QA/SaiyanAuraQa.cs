using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DBPU
{
    // Opt-in native-game check of the frame-by-frame Super Saiyan auras: -dbpu-saiyan-aura-qa <absolute folder>.
    // Never runs in an ordinary session and does not save mod settings.
    public sealed class SaiyanAuraQa : MonoBehaviour
    {
        static bool started;
        string output;
        bool finished;
        Vector3 pin;
        float zoom = 4.6f;
        bool pinned;
        int errors;

        void Awake() { Application.logMessageReceived += LogReceived; Camera.onPreCull += PinCamera; }
        void OnDestroy() { Application.logMessageReceived -= LogReceived; Camera.onPreCull -= PinCamera; }

        void LogReceived(string message, string stack, LogType type)
        {
            string text = message + "\n" + stack;
            if (text.IndexOf("SaiyanAura", StringComparison.Ordinal) < 0 && text.IndexOf("RefinedAura", StringComparison.Ordinal) < 0) return;
            if (type == LogType.Log) return;
            errors++;
            Log(type + " " + text);
        }

        public static void StartIfRequested()
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-dbpu-saiyan-aura-qa");
            if (index < 0 || index + 1 >= args.Length || started) return;
            started = true;
            if (!Path.IsPathRooted(args[index + 1])) { Debug.LogError("Saiyan aura QA requires an absolute output folder."); return; }
            Application.runInBackground = true;
            var go = new GameObject("DBPU opt-in Saiyan aura QA");
            DontDestroyOnLoad(go);
            var qa = go.AddComponent<SaiyanAuraQa>();
            qa.output = Path.GetFullPath(args[index + 1]);
            Directory.CreateDirectory(qa.output);
            File.Delete(Path.Combine(qa.output, "qa-ready.txt"));
            File.WriteAllText(Path.Combine(qa.output, "result.txt"), "DBPU Saiyan aura QA started\n");
            qa.StartCoroutine(qa.Run());
        }

        void Log(string message) { File.AppendAllText(Path.Combine(output, "result.txt"), message + "\n"); }
        void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

        bool Check(Action action)
        {
            try { action(); return true; }
            catch (Exception ex) { Log("FAIL " + ex); Finish(false); return false; }
        }

        void Finish(bool success)
        {
            if (finished) return;
            finished = true;
            File.WriteAllText(Path.Combine(output, "qa-ready.txt"), success ? "PASS\n" : "FAIL\n");
            Application.Quit(success ? 0 : 2);
        }

        // The game's camera controller moves the camera in its own update, so the pin applies just before culling.
        void PinCamera(Camera camera)
        {
            if (!pinned || !Global.main || camera != Global.main.camera) return;
            camera.transform.position = new Vector3(pin.x, pin.y, camera.transform.position.z);
            camera.orthographicSize = zoom;
        }

        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            Check(() =>
            {
                var image = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
                image.Apply();
                File.WriteAllBytes(Path.Combine(output, name + ".png"), image.EncodeToPNG());
                Destroy(image);
                Log("SCREENSHOT " + name);
            });
        }

        GameObject Spawn(string name, Vector3 position)
        {
            var asset = ModAPI.FindSpawnable(name);
            var human = ModAPI.FindSpawnable("Human");
            Assert(asset && human && human.Prefab, "Missing native catalogue entry " + name);
            var root = Instantiate(human.Prefab, position, Quaternion.identity);
            root.AddComponent<DeregisterBehaviour>();
            root.AddComponent<TexturePackApplier>();
            root.AddComponent<AudioSourceTimeScaleBehaviour>();
            root.AddComponent<SerialiseInstructions>().OriginalSpawnableAsset = asset;
            root.name = asset.name;
            CatalogBehaviour.SpawnedGameObjects.Add(root);
            CatalogBehaviour.PerformMod(asset, root);
            Assert(root, "Native modification removed QA fighter " + name);
            foreach (var body in root.GetComponentsInChildren<Rigidbody2D>()) body.bodyType = RigidbodyType2D.Kinematic;
            return root;
        }

        IEnumerator Run()
        {
            for (int i = 0; i < 120; i++) yield return null;
            AsyncOperation loading = null;
            if (!Check(() =>
            {
                MapLoaderBehaviour.CurrentMap = MapRegistry.GetAllMaps().First(map => map.Prefab);
                loading = SceneManager.LoadSceneAsync("Main");
            })) yield break;
            yield return loading;
            yield return new WaitForSecondsRealtime(5);

            GameObject subject = null;
            if (!Check(() =>
            {
                if (!ModAPI.FindSpawnable("[DBPU] (BM) Goku"))
                {
                    // A third-party startup exception can stop the catalogue; recover it in this disposable QA only.
                    var apply = typeof(CatalogBehaviour).GetMethod("ApplyModifications", BindingFlags.NonPublic | BindingFlags.Instance);
                    Assert(apply != null, "Native catalogue application method missing");
                    apply.Invoke(CatalogBehaviour.Main, null);
                    Log("QA ONLY: resumed native catalogue application after a third-party startup exception");
                }
                subject = Spawn("[DBPU] (BM) Goku", new Vector3(0f, 30f, 0f));
            })) yield break;
            yield return new WaitForSecondsRealtime(1.5f);

            AuraHandler aura = null;
            PersonBehaviour person = null;
            if (!Check(() =>
            {
                person = subject.GetComponent<PersonBehaviour>();
                Assert(person && person.IsAlive(), "QA fighter did not initialize alive");
                aura = subject.GetComponent<AuraHandler>();
                Assert(aura, "QA fighter has no AuraHandler");
                DBPU.AurasEnabled = true;
                pin = person.Limbs[2].transform.position + new Vector3(0, .5f, 0);
                pinned = true;
                foreach (var canvas in FindObjectsOfType<Canvas>()) canvas.enabled = false;
                Log("Screen " + Screen.width + "x" + Screen.height + ", HDR value " + BeamFxAssets.Hdr);
            })) yield break;

            foreach (var id in new[] { "SSJ1", "SSJ2", "SSJ3" })
            {
                SaiyanAuraRig rig = null;
                if (!Check(() =>
                {
                    aura.AuraVisible = true; aura.AuraActive = true;
                    aura.CreateAura(id);
                    Assert(aura.CurrentAuraObject, id + ": no aura object");
                    rig = aura.CurrentAuraObject.GetComponent<SaiyanAuraRig>();
                    Assert(rig, id + ": aura is not the Saiyan rig");
                    var renderers = aura.CurrentAuraObject.GetComponentsInChildren<Renderer>(true);
                    Assert(renderers.All(r => r.sharedMaterial && r.sharedMaterial.shader.isSupported), id + ": unsupported material");
                    Log("PASS " + id + " created, renderers " + renderers.Length + ", root " + aura.CurrentAuraObject.transform.position + " scale " + aura.CurrentAuraObject.transform.localScale
                        + ", soles " + person.Limbs.Min(l => l.transform.position.y));
                })) yield break;
                zoom = 4.6f;
                yield return new WaitForSecondsRealtime(1.2f);
                int lit = 0, rendered = 0;
                float started = Time.realtimeSinceStartup;
                PeoplePlayground.VFX.SSJ1AuraController controller = null;
                Check(() => controller = FindObjectsOfType<PeoplePlayground.VFX.SSJ1AuraController>().FirstOrDefault());
                for (int shot = 0; shot < 4; shot++)
                {
                    yield return Shot(id + "-idle-" + shot);
                    float until = Time.realtimeSinceStartup + .13f;
                    while (Time.realtimeSinceStartup < until) { if (controller && controller.LightningShown) lit++; rendered++; yield return null; }
                }
                float watch = Time.realtimeSinceStartup + 2.5f;
                while (Time.realtimeSinceStartup < watch) { if (controller && controller.LightningShown) lit++; rendered++; yield return null; }
                float fps = rendered / (Time.realtimeSinceStartup - started);
                if (id != "SSJ1" && controller)
                {
                    // A shot in the middle of a flash, and what the lightning layer was given.
                    float until = Time.realtimeSinceStartup + 3f;
                    while (Time.realtimeSinceStartup < until && !controller.LightningShown) yield return null;
                    yield return Shot(id + "-bolt");
                    Check(() =>
                    {
                        var layer = controller.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.name.Contains("Lightning"));
                        var b = new MaterialPropertyBlock(); layer.GetPropertyBlock(b);
                        var pts = b.GetVectorArray("_Pts");
                        Log("BOLT " + id + ": shown " + controller.LightningShown + ", count " + b.GetFloat("_Count") + ", first " + (pts != null && pts.Length > 0 ? pts[0].ToString("0.000") : "none")
                            + ", look " + b.GetVector("_Look").ToString("0.000") + ", quad " + layer.transform.position + " " + layer.transform.lossyScale + ", layer " + layer.sortingLayerName + "/" + layer.sortingOrder
                            + ", shader " + layer.sharedMaterial.shader.name + " " + layer.sharedMaterial.shader.isSupported);
                    });
                }
                // Fly the fighter sideways at 30 units a second: the aura must stay on the body every frame.
                float worst = 0;
                var root = subject.transform;
                Vector3 home = root.position;
                for (int frame = 0; frame < 40 && controller; frame++)
                {
                    root.position += new Vector3(30f * Time.deltaTime, 0, 0);
                    yield return null;
                    float bodyX = person.Limbs.Where(l => l).Average(l => l.transform.position.x);
                    worst = Mathf.Max(worst, Mathf.Abs(controller.AuraCentre.x - bodyX));
                }
                root.position = home;
                yield return null;
                if (!Check(() => { Assert(worst < .35f, id + ": the aura trails the fighter by " + worst); Log("PASS " + id + " follows a fast fighter, worst offset " + worst.ToString("0.000")); })) yield break;
                if (!Check(() =>
                {
                    var user = FindObjectsOfType<PeoplePlayground.VFX.SSJ1AuraController>();
                    Assert(user.Length == 1 && user[0].Person == person && user[0].AuraMaterial, id + ": the user's SSJ1 aura is not attached (" + user.Length + ")");
                    var quad = user[0].GetComponentInChildren<MeshRenderer>(true);
                    Assert(quad, id + ": the user's aura has no quad");
                    var block = new MaterialPropertyBlock(); quad.GetPropertyBlock(block);
                    var caps = block.GetVectorArray("_LimbCapsules");
                    Log("USER AURA " + id + ": active " + quad.gameObject.activeInHierarchy + ", enabled " + quad.enabled + ", visible " + quad.isVisible
                        + ", at " + quad.transform.position + " scale " + quad.transform.lossyScale + ", layer " + quad.sortingLayerName + "/" + quad.sortingOrder
                        + ", shader " + (quad.sharedMaterial ? quad.sharedMaterial.shader.name + " supported " + quad.sharedMaterial.shader.isSupported : "none")
                        + ", egg " + block.GetVector("_Egg") + " " + block.GetVector("_Egg2") + ", capsule0 " + (caps != null && caps.Length > 0 ? caps[0].ToString() : "none")
                        + ", hdr " + block.GetFloat("_Hdr") + ", opacity " + block.GetFloat("_Opacity")
                        + "; aura children: " + string.Join(", ", aura.CurrentAuraObject.GetComponentsInChildren<Transform>(true).Select(c => c.name + (c.gameObject.activeSelf ? "" : "(off)")).ToArray()));
                    Assert(id == "SSJ1" ? lit == 0 : lit > 0, id + ": lightning " + (id == "SSJ1" ? "on SSJ1" : "never shown"));
                    Log("PASS " + id + " lightning frames seen " + lit + ", about " + Mathf.RoundToInt(fps) + " fps while it plays");
                })) yield break;
                zoom = 1.6f;
                pin = person.Limbs[2].transform.position + new Vector3(-.9f, 1.4f, 0);
                yield return null;
                yield return Shot(id + "-zoom");
                // As close as a player can look: the tips of the tongues must stay sharp and clean.
                zoom = .45f;
                pin = person.Limbs[2].transform.position + new Vector3(-1.15f, 1.25f, 0);
                yield return null;
                yield return Shot(id + "-macro");
                pin = person.Limbs[2].transform.position + new Vector3(0, .5f, 0);
                zoom = 4.6f;
                aura.SetChargeVisualActive(true);
                var visual = aura.CurrentAuraObject.GetComponent<RefinedAuraVisual>();
                float until2 = Time.realtimeSinceStartup + 1.2f;
                while (Time.realtimeSinceStartup < until2) { aura.SetChargeVisualActive(true); yield return null; }
                if (!Check(() => Assert(visual.SmoothedCharge > .5f, id + ": charge did not reach the rig"))) yield break;
                yield return Shot(id + "-charge");
                aura.SetChargeVisualActive(false);
                yield return new WaitForSecondsRealtime(.6f);
                if (!Check(() =>
                {
                    var veil = RefinedAuras.Create(id, true);
                    Assert(veil && veil.GetComponent<SaiyanAuraRig>(), id + ": transition veil is not the Saiyan rig");
                    Destroy(veil);
                    Log("PASS " + id + " transition veil");
                })) yield break;
            }
            aura.AuraActive = false;
            yield return new WaitForSecondsRealtime(.5f);
            if (!Check(() => Assert(!aura.CurrentAuraObject, "Aura stayed after deactivation"))) yield break;
            Log("Logged aura warnings/errors: " + errors);
            Finish(errors == 0);
        }
    }
}
