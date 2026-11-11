using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DBPU
{
    // Opt-in native-game check of the Super Saiyan Blue auras: -dbpu-blue-aura-qa <absolute folder>.
    // SSJ Blue and Blue Evolution idle and charging, the real transformation into Blue with its veil, and
    // Kaioken over Blue (one rig for the red flame and the white-blue core, the plain aura stepping aside).
    // Never runs in an ordinary session and does not save mod settings.
    public sealed class BlueAuraQa : MonoBehaviour
    {
        static bool started;
        string output;
        bool finished;
        Vector3 pin;
        bool pinned;
        int errors;

        void Awake() { Application.logMessageReceived += LogReceived; Camera.onPreCull += PinCamera; }
        void OnDestroy() { Application.logMessageReceived -= LogReceived; Camera.onPreCull -= PinCamera; }

        void LogReceived(string message, string stack, LogType type)
        {
            if (type == LogType.Log) return;
            string text = message + "\n" + stack;
            bool ours = text.IndexOf("BlueAura", StringComparison.Ordinal) >= 0 || text.IndexOf("RefinedAura", StringComparison.Ordinal) >= 0
                || (type == LogType.Exception && (text.IndexOf("Kaioken", StringComparison.Ordinal) >= 0 || text.IndexOf("AuraHandler", StringComparison.Ordinal) >= 0));
            if (!ours) return;
            errors++;
            Log(type + " " + text);
        }

        public static void StartIfRequested()
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-dbpu-blue-aura-qa");
            if (index < 0 || index + 1 >= args.Length || started) return;
            started = true;
            if (!Path.IsPathRooted(args[index + 1])) { Debug.LogError("Blue aura QA requires an absolute output folder."); return; }
            Application.runInBackground = true;
            var go = new GameObject("DBPU opt-in Blue aura QA");
            DontDestroyOnLoad(go);
            var qa = go.AddComponent<BlueAuraQa>();
            qa.output = Path.GetFullPath(args[index + 1]);
            Directory.CreateDirectory(qa.output);
            File.Delete(Path.Combine(qa.output, "qa-ready.txt"));
            File.WriteAllText(Path.Combine(qa.output, "result.txt"), "DBPU Blue aura QA started\n");
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
            camera.orthographicSize = 4.6f;
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

        static BlueAuraRig Rig(GameObject go) { return go ? go.GetComponent<BlueAuraRig>() : null; }

        IEnumerator Charge(AuraHandler aura, float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) { aura.SetChargeVisualActive(true); yield return null; }
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

            AuraHandler aura = null; PersonBehaviour person = null; TransformationContextMenu menu = null; Kaioken kaioken = null;
            if (!Check(() =>
            {
                person = subject.GetComponent<PersonBehaviour>();
                Assert(person && person.IsAlive(), "QA fighter did not initialize alive");
                aura = subject.GetComponent<AuraHandler>();
                Assert(aura, "QA fighter has no AuraHandler");
                menu = subject.transform.Find("Head").GetComponent<TransformationContextMenu>();
                Assert(menu, "QA fighter has no transformation menu");
                kaioken = subject.GetComponent<Kaioken>();
                Assert(kaioken, "QA fighter has no Kaioken");
                DBPU.AurasEnabled = true;
                pin = person.Limbs[2].transform.position + new Vector3(0, 1.1f, 0);
                pinned = true;
                foreach (var canvas in FindObjectsOfType<Canvas>()) canvas.enabled = false;
                Log("Screen " + Screen.width + "x" + Screen.height + ", HDR " + BeamFxAssets.Hdr);
            })) yield break;

            // The real transformation into Blue, through its veil.
            if (!Check(() => { aura.AuraActive = true; aura.AuraVisible = true; menu.ApplyForm("SSJBlue"); })) yield break;
            yield return new WaitForSecondsRealtime(.18f);
            yield return Shot("SSJBlue-transform");
            yield return new WaitForSecondsRealtime(1.6f);
            if (!Check(() =>
            {
                var rig = Rig(aura.CurrentAuraObject);
                Assert(rig, "Transformation into Blue did not leave a Blue rig (" + aura.CurrentAuraID + ")");
                Log("PASS transformation into " + menu.Form + ", aura " + aura.CurrentAuraID + " drawn as " + rig.ProfileId);
                // Where the fighter stands in the aura's own space: the rig assumes feet at -.875 and a 1.75 tall fighter.
                float low = float.MaxValue, high = float.MinValue;
                foreach (var limb in person.Limbs)
                    foreach (var sr in limb.GetComponentsInChildren<SpriteRenderer>())
                    { if (!sr.enabled || !sr.sprite) continue; low = Mathf.Min(low, sr.bounds.min.y); high = Mathf.Max(high, sr.bounds.max.y); }
                var t = aura.CurrentAuraObject.transform;
                Log("GEOMETRY fighter " + low.ToString("0.000") + ".." + high.ToString("0.000") + " world, aura root y " + t.position.y.ToString("0.000") + " scale " + t.lossyScale.y.ToString("0.000")
                    + ", local feet " + ((low - t.position.y) / t.lossyScale.y).ToString("0.000") + " head " + ((high - t.position.y) / t.lossyScale.y).ToString("0.000")
                    + ", limb2 y " + person.Limbs[2].transform.position.y.ToString("0.000") + " (" + person.Limbs[2].name + "), tune " + aura.AuraTuneScale + "/" + aura.AuraTuneY);
            })) yield break;
            yield return Shot("SSJBlue-form");

            // Kaioken over Blue.
            if (!Check(() => { kaioken.ApplyConfiguredMultiplier(10); kaioken.ToggleFromPowersMenu(); })) yield break;
            yield return new WaitForSecondsRealtime(1.4f);
            RefinedAuraVisual outer = null;
            if (!Check(() =>
            {
                Assert(kaioken.Active, "Kaioken did not start over Blue");
                outer = FindObjectsOfType<RefinedAuraVisual>().FirstOrDefault(v => v.KaiokenPerson == person);
                Assert(outer, "No Kaioken aura over Blue");
                var rig = outer.GetComponent<BlueAuraRig>();
                Assert(rig && rig.ProfileId == "KaiokenBlue", "Kaioken over Blue is not the Blue rig");
                var inner = Rig(aura.CurrentAuraObject);
                Assert(inner && inner.Hidden > .99f, "The plain Blue aura did not step aside under Kaioken (" + (inner ? inner.Hidden.ToString() : "none") + ")");
                Assert(outer.GetComponentsInChildren<Renderer>().All(r => r.sharedMaterial && r.sharedMaterial.shader.isSupported), "Kaioken: unsupported material");
                Log("PASS Kaioken x10 over Blue: one rig, plain aura hidden");
            })) yield break;
            yield return Shot("KaiokenBlue-idle");
            yield return new WaitForSecondsRealtime(.41f);
            yield return Shot("KaiokenBlue-idle-b");
            yield return Charge(aura, 1.2f);
            if (!Check(() => Assert(outer.SmoothedCharge > .5f, "Kaioken over Blue did not take up the charge"))) yield break;
            yield return Shot("KaiokenBlue-charge");
            aura.SetChargeVisualActive(false);
            yield return new WaitForSecondsRealtime(.6f);
            if (!Check(() => kaioken.ToggleFromPowersMenu())) yield break;
            yield return new WaitForSecondsRealtime(1f);
            if (!Check(() =>
            {
                Assert(!kaioken.Active, "Kaioken did not stop");
                Assert(!outer, "Kaioken aura stayed after Kaioken ended");
                var inner = Rig(aura.CurrentAuraObject);
                Assert(inner && inner.Hidden < .01f, "The Blue aura did not come back after Kaioken");
                Log("PASS Kaioken ended, Blue aura back");
            })) yield break;
            yield return Shot("Blue-after-kaioken");

            // Each aura on its own, idle and charging.
            foreach (var id in new[] { "SSJBlue", "SSJBlueEvolution" })
            {
                if (!Check(() =>
                {
                    aura.AuraVisible = true; aura.AuraActive = true;
                    aura.CreateAura(id);
                    var rig = Rig(aura.CurrentAuraObject);
                    Assert(rig, id + ": aura is not the Blue rig");
                    Assert(rig.ProfileId == id, id + ": wrong profile " + rig.ProfileId);
                    Assert(aura.CurrentAuraObject.GetComponentsInChildren<Renderer>().All(r => r.sharedMaterial && r.sharedMaterial.shader.isSupported), id + ": unsupported material");
                    Log("PASS " + id + " created, renderers " + aura.CurrentAuraObject.GetComponentsInChildren<Renderer>().Length);
                })) yield break;
                yield return new WaitForSecondsRealtime(1.5f);
                yield return Shot(id + "-idle");
                yield return new WaitForSecondsRealtime(.37f);
                yield return Shot(id + "-idle-b");
                yield return Charge(aura, 1.2f);
                if (!Check(() => Assert(aura.CurrentAuraObject.GetComponent<RefinedAuraVisual>().SmoothedCharge > .5f, id + ": charge did not reach the rig"))) yield break;
                yield return Shot(id + "-charge");
                aura.SetChargeVisualActive(false);
                yield return new WaitForSecondsRealtime(.6f);
                if (!Check(() =>
                {
                    var veil = RefinedAuras.Create(id, true);
                    Assert(veil && Rig(veil), id + ": transition veil is not the Blue rig");
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
