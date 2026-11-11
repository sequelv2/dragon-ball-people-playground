using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DBPU
{
    // Opt-in native-game check of the Sparking outline: -dbpu-sparking-outline-qa <absolute folder>.
    // Never runs in an ordinary session and does not save mod settings.
    public sealed class SparkingOutlineQa : MonoBehaviour
    {
        static bool started;
        string output;
        bool finished;
        Vector3 pin;
        bool pinned;
        float zoom=3.2f;
        int errors;

        void Awake() { Application.logMessageReceived += LogReceived; Camera.onPreCull += PinCamera; }
        void OnDestroy() { Application.logMessageReceived -= LogReceived; Camera.onPreCull -= PinCamera; }

        void LogReceived(string message, string stack, LogType type)
        {
            string text = message + "\n" + stack;
            if (text.IndexOf("SparkingOutline", StringComparison.Ordinal) < 0 || type == LogType.Log) return;
            errors++;
            Log(type + " " + text);
        }

        public static void StartIfRequested()
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-dbpu-sparking-outline-qa");
            if (index < 0 || index + 1 >= args.Length || started) return;
            started = true;
            if (!Path.IsPathRooted(args[index + 1])) { Debug.LogError("Sparking outline QA requires an absolute output folder."); return; }
            Application.runInBackground = true;
            var go = new GameObject("DBPU opt-in Sparking outline QA");
            DontDestroyOnLoad(go);
            var qa = go.AddComponent<SparkingOutlineQa>();
            qa.output = Path.GetFullPath(args[index + 1]);
            Directory.CreateDirectory(qa.output);
            File.Delete(Path.Combine(qa.output, "qa-ready.txt"));
            File.WriteAllText(Path.Combine(qa.output, "result.txt"), "DBPU Sparking outline QA started\n");
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

            KiSparkingController controller = null;
            SparkingOutline outline = null;
            PersonBehaviour person = null;
            if (!Check(() =>
            {
                person = subject.GetComponent<PersonBehaviour>();
                Assert(person && person.IsAlive(), "QA fighter did not initialize alive");
                controller = subject.GetComponent<KiSparkingController>();
                Assert(controller, "QA fighter has no KiSparkingController");
                outline = subject.GetComponent<SparkingOutline>();
                Assert(outline, "QA fighter has no SparkingOutline");
                Assert(outline.PartCount == 0, "Outline built before the first ignition");
                DBPU.SparkingEnabled = true;
                DBPU.AurasEnabled = true;
                pin = person.Limbs[2].transform.position + new Vector3(0, .15f, 0);
                pinned = true;
                foreach (var canvas in FindObjectsOfType<Canvas>()) canvas.enabled = false;
                Log("Screen " + Screen.width + "x" + Screen.height);
                foreach (var limb in person.Limbs)
                    foreach (var r in limb.GetComponentsInChildren<SpriteRenderer>(true))
                        Log("SPRITE " + limb.name + "/" + r.name + " layer=" + r.sortingLayerName + " order=" + r.sortingOrder + " sprite=" + (r.sprite ? r.sprite.name + " ppu=" + r.sprite.pixelsPerUnit + " rect=" + r.sprite.rect + " tex=" + r.sprite.texture.width + "x" + r.sprite.texture.height : "none") + " scale=" + r.transform.lossyScale);
            })) yield break;
            yield return Shot("0-before");

            if (!Check(() => Assert(controller.TryStartOverload(), "Sparking did not start"))) yield break;
            yield return new WaitForSecondsRealtime(.12f);
            yield return Shot("1-ignite");
            yield return new WaitForSecondsRealtime(1.2f);
            if (!Check(() =>
            {
                Assert(outline.Fade >= .99f, "Outline did not reach full strength: " + outline.Fade);
                Assert(outline.ShownParts > 0, "No outline parts are visible");
                Log("PASS outline active, parts " + outline.PartCount + " shown " + outline.ShownParts);
            })) yield break;
            yield return Shot("2-steady");

            if (!Check(() =>
            {
                var aura = subject.GetComponent<AuraHandler>();
                Assert(aura, "QA fighter has no AuraHandler");
                aura.AuraVisible = true; aura.AuraActive = true;
                aura.CreateAura("SSJ1");
            })) yield break;
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot("3-with-aura");
            if (!Check(() => Log("Camera HDR " + Global.main.camera.allowHDR))) yield break;
            var handler = subject.GetComponent<AuraHandler>();
            zoom = 1.9f;
            foreach (var id in new[] { "SSJBlue", "SSJRose", "Broly", "BlackFrieza", "Kaioken", "UI" })
            {
                if (!Check(() => handler.CreateAura(id, false))) yield break;
                yield return new WaitForSecondsRealtime(1f);
                yield return Shot("form-" + id + "-aura");
                if (!Check(() =>
                {
                    Color deep, mid, light;
                    handler.GetAuraColors(out deep, out mid, out light);
                    float hueWanted, s, v, hue;
                    Color.RGBToHSV(Color.Lerp(mid, deep, .45f), out hueWanted, out s, out v);
                    Color.RGBToHSV(outline.Halo, out hue, out s, out v);
                    Assert(Mathf.Abs(Mathf.DeltaAngle(hue * 360, hueWanted * 360)) < 12, id + ": outline hue " + hue + " does not follow aura hue " + hueWanted);
                    Log("PASS " + id + " outline hue " + hue.ToString("F3") + " aura hue " + hueWanted.ToString("F3"));
                })) yield break;
            }
            if (!Check(() =>
            {
                handler.CreateAura("SSJ1", false);
                handler.ApplyCustomAuraColors(new Color(.1f, .9f, .35f), new Color(.75f, 1f, .8f), true, 1);
            })) yield break;
            yield return new WaitForSecondsRealtime(1f);
            yield return Shot("form-custom-aura");
            if (!Check(() =>
            {
                Assert(outline.Halo.g > outline.Halo.r && outline.Halo.g > outline.Halo.b, "Outline ignored custom aura colours: " + outline.Halo);
                Log("PASS custom aura colours reach the outline");
                handler.AuraActive = false;
                handler.CurrentAuraID = "Base";
            })) yield break;
            yield return new WaitForSecondsRealtime(.2f);
            if (!Check(() => handler.CreateAura("Base", false))) yield break;
            yield return new WaitForSecondsRealtime(.8f);
            yield return Shot("form-Base-hidden");
            if (!Check(() => { handler.CurrentAuraID = "SSJBlue"; zoom = 1.3f; })) yield break;
            yield return new WaitForSecondsRealtime(.8f);
            yield return Shot("4-closeup");

            if (!Check(() =>
            {
                zoom = 3.2f;
                foreach (var body in subject.GetComponentsInChildren<Rigidbody2D>()) body.bodyType = RigidbodyType2D.Dynamic;
            })) yield break;
            yield return new WaitForSecondsRealtime(.45f);
            if (!Check(() => pin = person.Limbs[2].transform.position)) yield break;
            yield return Shot("5-moving");

            float saved = DBPU.SparkingOverloadSeconds;
            if (!Check(() =>
            {
                foreach (var body in subject.GetComponentsInChildren<Rigidbody2D>()) body.bodyType = RigidbodyType2D.Kinematic;
                // The running timer is private, so the phase ends through the same path as turning Sparking off.
                DBPU.SparkingEnabled = false;
            })) yield break;
            yield return new WaitForSecondsRealtime(.8f);
            if (!Check(() =>
            {
                Assert(controller.CurrentPhase == KiSparkingController.SparkPhase.Idle, "Sparking did not end");
                Assert(outline.Fade <= 0 && outline.ShownParts == 0, "Outline stayed after Sparking ended");
                Log("PASS outline gone after Sparking");
                DBPU.SparkingEnabled = true;
                DBPU.SparkingOverloadSeconds = saved;
            })) yield break;
            yield return Shot("6-after");

            int count = outline.PartCount;
            GameObject copy = null;
            if (!Check(() =>
            {
                copy = Instantiate(subject, subject.transform.position + new Vector3(6, 0, 0), Quaternion.identity);
                Assert(copy.GetComponentsInChildren<SparkingOutline>().Length == 1, "Copied fighter has a wrong number of outlines");
                Assert(copy.GetComponent<SparkingOutline>().PartCount == 0, "Copied fighter inherited outline parts");
                Log("PASS copy starts clean");
                Destroy(copy);
                Destroy(subject);
            })) yield break;
            yield return new WaitForSecondsRealtime(.3f);
            if (!Check(() => Assert(!GameObject.Find("Sparking outline"), "Outline root outlived the fighter"))) yield break;
            Log("PASS cleanup, parts were " + count);
            Log("Logged outline warnings/errors: " + errors);
            Finish(errors == 0);
        }
    }
}
