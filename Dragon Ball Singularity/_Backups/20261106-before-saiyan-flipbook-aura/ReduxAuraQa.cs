using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DBPU
{
    // Opt-in native-game check of the Redux Super Saiyan auras: -dbpu-redux-aura-qa <absolute folder>.
    // Never runs in an ordinary session and does not save mod settings.
    public sealed class ReduxAuraQa : MonoBehaviour
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
            string text = message + "\n" + stack;
            if (text.IndexOf("ReduxAura", StringComparison.Ordinal) < 0 && text.IndexOf("RefinedAura", StringComparison.Ordinal) < 0) return;
            if (type == LogType.Log) return;
            errors++;
            Log(type + " " + text);
        }

        public static void StartIfRequested()
        {
            SaiyanAuraQa.StartIfRequested();
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-dbpu-redux-aura-qa");
            if (index < 0 || index + 1 >= args.Length || started) return;
            started = true;
            if (!Path.IsPathRooted(args[index + 1])) { Debug.LogError("Redux aura QA requires an absolute output folder."); return; }
            Application.runInBackground = true;
            var go = new GameObject("DBPU opt-in Redux aura QA");
            DontDestroyOnLoad(go);
            var qa = go.AddComponent<ReduxAuraQa>();
            qa.output = Path.GetFullPath(args[index + 1]);
            Directory.CreateDirectory(qa.output);
            File.Delete(Path.Combine(qa.output, "qa-ready.txt"));
            File.WriteAllText(Path.Combine(qa.output, "result.txt"), "DBPU Redux aura QA started\n");
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
            if (!Check(() =>
            {
                var person = subject.GetComponent<PersonBehaviour>();
                Assert(person && person.IsAlive(), "QA fighter did not initialize alive");
                aura = subject.GetComponent<AuraHandler>();
                Assert(aura, "QA fighter has no AuraHandler");
                DBPU.AurasEnabled = true;
                pin = person.Limbs[2].transform.position + new Vector3(0, 1.1f, 0);
                pinned = true;
                foreach (var canvas in FindObjectsOfType<Canvas>()) canvas.enabled = false;
                Log("Screen " + Screen.width + "x" + Screen.height);
            })) yield break;

            foreach (var id in new[] { "SSJ1", "SSJ2", "SSJ3" })
            {
                if (!Check(() =>
                {
                    aura.AuraVisible = true; aura.AuraActive = true;
                    aura.CreateAura(id);
                    Assert(aura.CurrentAuraObject, id + ": no aura object");
                    Assert(aura.CurrentAuraObject.GetComponent<ReduxAuraRig>(), id + ": aura is not the Redux rig");
                    Assert(aura.CurrentAuraObject.GetComponentsInChildren<Renderer>().All(r => r.sharedMaterial && r.sharedMaterial.shader.isSupported), id + ": unsupported material");
                    Log("PASS " + id + " created, renderers " + aura.CurrentAuraObject.GetComponentsInChildren<Renderer>().Length);
                })) yield break;
                yield return new WaitForSecondsRealtime(1.5f);
                yield return Shot(id + "-idle");
                aura.SetChargeVisualActive(true);
                var controllerCharge = aura.CurrentAuraObject.GetComponent<RefinedAuraVisual>();
                float until = Time.realtimeSinceStartup + 1.2f;
                while (Time.realtimeSinceStartup < until) { aura.SetChargeVisualActive(true); yield return null; }
                if (!Check(() => Assert(controllerCharge.SmoothedCharge > .5f, id + ": charge did not reach the rig"))) yield break;
                yield return Shot(id + "-charge");
                aura.SetChargeVisualActive(false);
                yield return new WaitForSecondsRealtime(.6f);
                if (!Check(() =>
                {
                    var veil = RefinedAuras.Create(id, true);
                    Assert(veil && veil.GetComponent<ReduxAuraRig>(), id + ": transition veil is not the Redux rig");
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
