using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DBPU
{
    // Opt-in native-game check of the torso technique picker and the Kaioken head tap:
    // -dbpu-torso-picker-qa <absolute folder>. Never runs in an ordinary session.
    public sealed class TorsoPickerQa : MonoBehaviour
    {
        static bool started;
        string output;
        bool finished, pinned;
        Vector3 pin;
        float zoom = 3.4f;
        int errors;

        void Awake() { Application.logMessageReceived += LogReceived; Camera.onPreCull += PinCamera; }
        void OnDestroy() { Application.logMessageReceived -= LogReceived; Camera.onPreCull -= PinCamera; }

        void LogReceived(string message, string stack, LogType type)
        {
            if (type == LogType.Log || type == LogType.Warning) return;
            string text = message + "\n" + stack;
            if (text.IndexOf("TorsoTechnique", StringComparison.Ordinal) < 0 && text.IndexOf("TechniqueCaption", StringComparison.Ordinal) < 0
                && text.IndexOf("KaiokenHeadTap", StringComparison.Ordinal) < 0) return;
            errors++;
            Log(type + " " + text);
        }

        public static void StartIfRequested()
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-dbpu-torso-picker-qa");
            if (index < 0 || index + 1 >= args.Length || started) return;
            started = true;
            if (!Path.IsPathRooted(args[index + 1])) { Debug.LogError("Torso picker QA requires an absolute output folder."); return; }
            Application.runInBackground = true;
            var go = new GameObject("DBPU opt-in torso picker QA");
            DontDestroyOnLoad(go);
            var qa = go.AddComponent<TorsoPickerQa>();
            qa.output = Path.GetFullPath(args[index + 1]);
            Directory.CreateDirectory(qa.output);
            File.Delete(Path.Combine(qa.output, "qa-ready.txt"));
            File.WriteAllText(Path.Combine(qa.output, "result.txt"), "DBPU torso picker QA started\n");
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
            TorsoTechniquePicker.QaPointerActive = false;
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

        static LimbBehaviour Part(GameObject fighter, string name)
        {
            return fighter.GetComponent<PersonBehaviour>().Limbs.First(l => l && l.name == name);
        }

        static void Point(LimbBehaviour limb)
        {
            TorsoTechniquePicker.QaPointerActive = true;
            TorsoTechniquePicker.QaPointer = limb ? limb.PhysicalBehaviour : null;
        }

        static void NativeUse(LimbBehaviour limb)
        {
            var use = new ActivationPropagation(true, 0, limb.gameObject) { Target = limb.gameObject };
            limb.gameObject.BroadcastMessage("Use", use, SendMessageOptions.DontRequireReceiver);
        }

        string Ids(PersonBehaviour person) { return string.Join(", ", TorsoTechniquePicker.Collect(person).Select(t => t.Id)); }

        void StepTo(string id)
        {
            for (int i = 0; i < 12 && TorsoTechniquePicker.QaCurrentId != id; i++) TorsoTechniquePicker.QaPressNext(false);
            Assert(TorsoTechniquePicker.QaCurrentId == id, "Picker never reached " + id + " (at " + TorsoTechniquePicker.QaCurrentId + ")");
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

            GameObject vegeta = null, goku = null, beerus = null;
            if (!Check(() =>
            {
                if (!ModAPI.FindSpawnable("[DBPU] (BM) Goku"))
                {
                    var apply = typeof(CatalogBehaviour).GetMethod("ApplyModifications", BindingFlags.NonPublic | BindingFlags.Instance);
                    Assert(apply != null, "Native catalogue application method missing");
                    apply.Invoke(CatalogBehaviour.Main, null);
                    Log("QA ONLY: resumed native catalogue application after a third-party startup exception");
                }
                vegeta = Spawn("[DBPU] (BM) Vegeta", new Vector3(-3f, 30f, 0f));
                goku = Spawn("[DBPU] (BM) Goku", new Vector3(3f, 30f, 0f));
                try { beerus = Spawn("[DBPU] (BM) Beerus", new Vector3(30f, 30f, 0f)); }
                catch (Exception ex) { Log("SKIP Beerus: " + ex.Message); }
                DBPU.KiDrainMultiplier = 0;
                pin = new Vector3(0f, 30.6f, 0f);
                pinned = true;
                Log("Screen " + Screen.width + "x" + Screen.height);
            })) yield break;
            yield return new WaitForSecondsRealtime(2f);

            PersonBehaviour vp = null, gp = null, bp = null;
            KiShield shield = null;
            Flight flight = null;
            KiSystem vki = null;
            if (!Check(() =>
            {
                vp = vegeta.GetComponent<PersonBehaviour>(); gp = goku.GetComponent<PersonBehaviour>(); bp = beerus ? beerus.GetComponent<PersonBehaviour>() : null;
                Assert(vp.IsAlive() && gp.IsAlive() && (!bp || bp.IsAlive()), "QA fighters did not initialise alive");
                Log("Vegeta torso techniques: " + Ids(vp));
                Log("Goku torso techniques: " + Ids(gp));
                if (bp) Log("Beerus torso techniques: " + Ids(bp));
                Assert(Ids(vp).Contains("KiShield") && Ids(vp).Contains("FinalExplosion"), "Vegeta lacks Ki Shield or Final Explosion");
                Assert(Ids(gp).Contains("Kaioken"), "Goku lacks Kaioken");
                shield = vegeta.GetComponent<KiShield>();
                flight = Part(vegeta, "LowerBody").GetComponent<Flight>();
                vki = vegeta.GetComponent<KiSystem>();

                // R over the head is ki charge, never the picker.
                Point(Part(vegeta, "Head"));
                TorsoTechniquePicker.QaPressNext(false);
                Assert(!TorsoTechniquePicker.QaOpenPerson, "R on the head opened the picker");

                Point(Part(vegeta, "UpperBody"));
                TorsoTechniquePicker.QaPressNext(false);
                Assert(TorsoTechniquePicker.QaOpenPerson == vp, "R on Vegeta's torso did not open his picker");
                Assert(TorsoTechniquePicker.QaCurrentId == "KiShield", "Picker did not start on the first technique: " + TorsoTechniquePicker.QaCurrentId);
                Log("PASS R on the torso opens the picker on Ki Shield; R on the head does not");
            })) yield break;
            yield return new WaitForSecondsRealtime(.4f);
            yield return Shot("1-vegeta-open");

            if (!Check(() =>
            {
                Point(Part(vegeta, "MiddleBody"));
                StepTo("FinalExplosion");
                Point(null);
                TorsoTechniquePicker.QaPressNext(true);
                Assert(TorsoTechniquePicker.QaCurrentId != "FinalExplosion", "Shift+R in empty space did not step back");
                TorsoTechniquePicker.QaPressNext(false);
                Assert(TorsoTechniquePicker.QaCurrentId == "FinalExplosion", "R in empty space did not step forward");
                Log("PASS R steps forward, Shift+R back, also with the pointer off the fighter");
            })) yield break;
            yield return new WaitForSecondsRealtime(.3f);
            yield return Shot("2-vegeta-final-explosion");

            var explosion = vegeta.GetComponent<FinalExplosion>();
            if (!Check(() =>
            {
                vki.currentKi = vki.maxKi * .2f;
                Point(Part(vegeta, "MiddleBody"));
                Assert(TorsoTechniquePicker.QaPressUse(), "Use on the torso did not reach the picker");
                Assert(!explosion.Firing && !explosion.stillActive, "Final Explosion fired without enough ki");
                Assert(TorsoTechniquePicker.QaOpenPerson == vp && TorsoTechniquePicker.QaFailure == "LOW KI", "Failure not shown: " + TorsoTechniquePicker.QaFailure);
                Log("PASS Final Explosion without ki keeps the picker open and shows LOW KI");
            })) yield break;
            yield return new WaitForSecondsRealtime(.08f);
            yield return Shot("3-low-ki");
            yield return new WaitForSecondsRealtime(1.3f);

            if (!Check(() =>
            {
                vki.currentKi = vki.maxKi;
                Point(Part(vegeta, "UpperBody"));
                StepTo("KiShield");
                bool flyingBefore = flight && flight.IsFlying;
                Assert(!shield.Using, "Shield was already up");
                Assert(TorsoTechniquePicker.QaPressUse(), "Use did not confirm Ki Shield");
                Assert(shield.Using, "Confirming Ki Shield did not raise it");
                Assert(!TorsoTechniquePicker.QaOpenPerson, "Picker stayed open after a successful confirm");
                // The same press reaching the torso natively must not lower it again or toggle flight.
                NativeUse(Part(vegeta, "UpperBody"));
                NativeUse(Part(vegeta, "MiddleBody"));
                NativeUse(Part(vegeta, "LowerBody"));
                Assert(shield.Using, "The confirming Use press also reached the shield's own trigger");
                Assert(!explosion.Firing, "The confirming Use press also reached Final Explosion's trigger");
                Assert(!flight || flight.IsFlying == flyingBefore, "The confirming Use press also toggled flight");
                Log("PASS confirm raises Ki Shield; the same Use press is kept from the torso's own triggers");
            })) yield break;
            yield return null;
            if (!Check(() =>
            {
                NativeUse(Part(vegeta, "UpperBody"));
                Assert(!shield.Using, "Native Use on the upper body no longer works after the picker closed");
                Log("PASS next frame, native Use on the torso works as before");
            })) yield break;
            yield return new WaitForSecondsRealtime(.3f);
            if (!Check(() => { Point(Part(vegeta, "UpperBody")); TorsoTechniquePicker.QaPressNext(false); TorsoTechniquePicker.QaPressUse(); })) yield break;
            yield return new WaitForSecondsRealtime(.6f);
            yield return Shot("4-shield-up");
            if (!Check(() => { if (shield.Using) shield.Lower(false); })) yield break;

            Kaioken kaioken = null;
            float savedMax = DBPU.KaiokenMaxMultiplier;
            if (!Check(() =>
            {
                kaioken = goku.GetComponent<Kaioken>();
                Point(Part(goku, "MiddleBody"));
                TorsoTechniquePicker.QaPressNext(false);
                Assert(TorsoTechniquePicker.QaOpenPerson == gp, "R on Goku's torso did not move the picker to Goku");
                StepTo("Kaioken");
                kaioken.ApplyConfiguredMultiplier(2);
                Assert(TorsoTechniquePicker.QaPressUse() && kaioken.Active, "Confirming Kaioken did not start it");
                DBPU.KaiokenMaxMultiplier = 20;
                KaiokenHeadTap.Step(gp, kaioken, false);
                Assert(Mathf.Approximately(kaioken.Multiplier, 3), "Tap did not step x2 to x3: " + kaioken.Multiplier);
                KaiokenHeadTap.Step(gp, kaioken, true);
                Assert(Mathf.Approximately(kaioken.Multiplier, 2), "Shift tap did not step back to x2: " + kaioken.Multiplier);
                KaiokenHeadTap.Step(gp, kaioken, true);
                Assert(Mathf.Approximately(kaioken.Multiplier, 20), "Shift tap from x2 did not wrap to x20: " + kaioken.Multiplier);
                KaiokenHeadTap.Step(gp, kaioken, false);
                Assert(Mathf.Approximately(kaioken.Multiplier, 2), "Tap from x20 did not wrap to x2: " + kaioken.Multiplier);
                kaioken.ApplyConfiguredMultiplier(2.5f);
                KaiokenHeadTap.Step(gp, kaioken, false);
                Assert(Mathf.Approximately(kaioken.Multiplier, 3), "Tap from a custom x2.5 did not land on x3: " + kaioken.Multiplier);
                for (int i = 0; i < 5; i++) KaiokenHeadTap.Step(gp, kaioken, false);
                Assert(Mathf.Approximately(kaioken.Multiplier, 8), "Five taps from x3 did not reach x8: " + kaioken.Multiplier);
                var multipliers = goku.GetComponent<MultipliersAndStuffs>();
                Assert(Mathf.Approximately(multipliers.KaiokenMultiplier, 8), "Power multiplier did not follow the tap: " + multipliers.KaiokenMultiplier);
                Log("PASS Kaioken tap: x2 -> x3, Shift back, wraps x2 <-> x20, custom x2.5 -> x3, power follows (x8)");
                TorsoTechniquePicker.QaPressNext(false);
                StepTo("Kaioken");
                KaiokenHeadTap.Step(gp, kaioken, false);
                Assert(!TechniqueCaptions.IsShown(gp, TechniqueCaptions.ToastSlot), "Toast repeated the open picker's Kaioken line");
                StepTo("KiShield");
                KaiokenHeadTap.Step(gp, kaioken, false);
                Assert(TechniqueCaptions.IsShown(gp, TechniqueCaptions.ToastSlot), "No Kaioken toast while the picker shows another technique");
                Assert(Mathf.Approximately(kaioken.Multiplier, 10), "Two more taps from x8 did not reach x10: " + kaioken.Multiplier);
                Log("PASS the picker takes the tap itself when it shows Kaioken; otherwise a toast sits above it");
                pin = new Vector3(3f, 30.6f, 0f);
            })) yield break;
            yield return new WaitForSecondsRealtime(.5f);
            yield return Shot("5-goku-picker-and-kaioken-toast");

            if (!Check(() => zoom = 9f)) yield break;
            yield return new WaitForSecondsRealtime(.2f);
            yield return Shot("6-zoomed-out");
            if (!Check(() => zoom = 1.6f)) yield break;
            yield return new WaitForSecondsRealtime(.2f);
            yield return Shot("7-zoomed-in");
            if (!Check(() => zoom = 3.4f)) yield break;

            yield return new WaitForSecondsRealtime(5.5f);
            if (!Check(() =>
            {
                Assert(!TorsoTechniquePicker.QaOpenPerson, "Picker did not close itself after 5 s without input (idle " + TorsoTechniquePicker.QaIdle
                    + " s, can act " + KiCombatMath.CanAct() + ", timeScale " + Time.timeScale + ", frame " + Time.frameCount + ")");
                Log("PASS picker closes after 5 s idle");
                kaioken.ToggleFromPowersMenu();
                DBPU.KaiokenMaxMultiplier = savedMax;
            })) yield break;

            GodsWrathBehaviour wrath = null;
            float kiBefore = 0f;
            if (!Check(() =>
            {
                wrath = beerus ? beerus.GetComponent<GodsWrathBehaviour>() : null;
                if (!wrath) { Log("SKIP Beerus has no G.O.D's Wrath here"); return; }
                pin = new Vector3(30f, 30.6f, 0f);
                var ki = beerus.GetComponent<KiSystem>();
                // Regeneration would refill the shots' cost at once; cost must be on to count them.
                ki.baseRegen = 0f; DBPU.KiAttackCostMultiplier = 1f;
                ki.currentKi = ki.maxKi * .9f; kiBefore = ki.currentKi;
                wrath.Active = false;
                Point(Part(beerus, "MiddleBody"));
                TorsoTechniquePicker.QaPressNext(false);
                StepTo("GodsWrath");
            })) yield break;
            if (wrath)
            {
                yield return new WaitForSecondsRealtime(.3f);
                yield return Shot("8-beerus-wrath");
                if (!Check(() => Assert(TorsoTechniquePicker.QaPressUse(), "Use did not confirm G.O.D's Wrath"))) yield break;
                yield return new WaitForSecondsRealtime(.15f);
                yield return Shot("9-beerus-wrath-firing");
                yield return new WaitForSecondsRealtime(.4f);
                if (!Check(() =>
                {
                    var ki = beerus.GetComponent<KiSystem>();
                    Assert(ki.currentKi < kiBefore, "G.O.D's Wrath confirmed without being armed did not fire");
                    Log("PASS G.O.D's Wrath fires from the picker without arming it in the powers menu");
                })) yield break;
            }

            Log("Logged picker errors: " + errors);
            Finish(errors == 0);
        }
    }
}
