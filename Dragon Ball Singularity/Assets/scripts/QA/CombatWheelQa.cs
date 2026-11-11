using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using Stats = DBPU.CharacterTransformations.Transformations.TransformationStats;

namespace DBPU
{
    // Opt-in disposable native-game verification. Never runs in an ordinary session.
    public sealed class CombatWheelQa : MonoBehaviour
    {
        static bool started;
        string output;
        bool finished;
        CombatTransformationWheel wheel;
        GameObject subject, other;
        TransformationContextMenu menu, otherMenu;
        PersonBehaviour person, otherPerson;
        KiSystem ki, otherKi;
        int blockerBaseline;
        int sceneEpoch;
        readonly List<string> temporaryForms = new List<string>();

        void Awake() { SceneManager.activeSceneChanged += SceneChanged; }
        void OnDestroy() { SceneManager.activeSceneChanged -= SceneChanged; }
        void SceneChanged(Scene before, Scene after)
        {
            sceneEpoch++;
            if (!string.IsNullOrEmpty(output)) Log("SCENE " + before.name + " -> " + after.name);
        }

        public static void StartIfRequested()
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-dbpu-combat-wheel-qa");
            if (index < 0 || index + 1 >= args.Length) return;
            if (started) return;
            started = true;
            if (!Path.IsPathRooted(args[index + 1]))
            {
                Debug.LogError("Combat wheel QA requires an absolute output folder.");
                return;
            }
            Application.runInBackground = true;
            var go = new GameObject("DBPU opt-in combat wheel QA");
            DontDestroyOnLoad(go);
            var qa = go.AddComponent<CombatWheelQa>();
            qa.output = Path.GetFullPath(args[index + 1]);
            Directory.CreateDirectory(qa.output);
            File.Delete(Path.Combine(qa.output, "qa-ready.txt"));
            File.WriteAllText(Path.Combine(qa.output, "result.txt"), "DBPU native combat wheel QA started\n");
            qa.StartCoroutine(qa.Run());
        }

        void Log(string message) { File.AppendAllText(Path.Combine(output, "result.txt"), message + "\n"); }
        void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        bool Check(Action action)
        {
            try { action(); return true; }
            catch (Exception ex)
            {
                Log("FAIL " + ex);
                Finish(false);
                return false;
            }
        }

        void Finish(bool success)
        {
            if (finished) return;
            finished = true;
            try { if (wheel) wheel.Close(false); } catch (Exception ex) { Log("Cleanup: " + ex); }
            // Written before Quit so the supervising process can distinguish completion from a hang.
            File.WriteAllText(Path.Combine(output, "qa-ready.txt"), success ? "PASS\n" : "FAIL\n");
            Application.Quit(success ? 0 : 2);
        }

        GameObject Spawn(string name, Vector3 position)
        {
            var asset = ModAPI.FindSpawnable(name);
            Assert(asset, "Missing catalogue entry " + name);
            var instance = Instantiate(ModAPI.FindSpawnable("Human").Prefab, position, Quaternion.identity);
            var performMod = typeof(CatalogBehaviour).GetMethod("PerformMod",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static,
                null, new[] { typeof(SpawnableAsset), typeof(GameObject) }, null);
            Assert(performMod != null, "Catalogue PerformMod method not found");
            performMod.Invoke(CatalogBehaviour.Main, new object[] { asset, instance });
            DontDestroyOnLoad(instance);
            foreach (var body in instance.GetComponentsInChildren<Rigidbody2D>()) body.bodyType = RigidbodyType2D.Kinematic;
            return instance;
        }

        TransformationContextMenu Menu(GameObject root)
        {
            var head = root.transform.Find("Head");
            var result = head ? head.GetComponent<TransformationContextMenu>() : null;
            Assert(result, "Spawned fighter has no native transformation menu");
            return result;
        }

        float[] InitialHealth(PersonBehaviour fighter) { return fighter.Limbs.Select(limb => limb.InitialHealth).ToArray(); }
        void SameHealth(float[] expected, PersonBehaviour fighter, string reason)
        {
            Assert(expected.Length == fighter.Limbs.Length, reason + ": limb count changed");
            for (int i = 0; i < expected.Length; i++)
                Assert(Math.Abs(fighter.Limbs[i].InitialHealth / expected[i] - 1f) < .0001f, reason + ": initial health changed on " + fighter.Limbs[i].name);
        }

        void Open(TransformationContextMenu target)
        {
            Assert(wheel.OpenFor(target, new Vector2(Screen.width * .5f, Screen.height * .5f), KeyCode.None), "Wheel did not open");
            Assert(wheel.IsOpen && wheel.Target == target, "Wheel target was not locked");
            Assert(Global.main.ActiveUiBlockers == blockerBaseline + 1, "Wheel did not own exactly one UI blocker");
        }

        int FormIndex(string key)
        {
            return menu.TransformationsData.transformations.GetOrderedEntries().FindIndex(entry => entry.Key == key);
        }

        void SelectForm(string key)
        {
            int index = FormIndex(key);
            Assert(index >= 0, "Missing form " + key);
            int pages = 0;
            while (wheel.Selection.Page != index / CombatWheelSelection.PageSize && pages++ <= wheel.Selection.Pages)
                wheel.ChangePage(1);
            wheel.ChooseSlot(index % CombatWheelSelection.PageSize);
            Assert(wheel.Selection.Selected == index && wheel.Selection.Armed, "Could not select " + key);
        }

        void Released(string reason)
        {
            Assert(!wheel.IsOpen && !wheel.Target, reason + ": target/UI retained after close");
            Assert(Global.main.ActiveUiBlockers == blockerBaseline, reason + ": leaked UI blocker");
            Assert(!CombatTransformationWheel.InputCaptured, reason + ": input capture retained beyond close frame");
        }

        // Call only after WaitForEndOfFrame so native ScreenSpaceOverlay UI is included.
        void Capture(string file)
        {
            var old = RenderTexture.active;
            Texture2D image = null;
            try
            {
                RenderTexture.active = null;
                image = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
                image.Apply();
                File.WriteAllBytes(Path.Combine(output, file), image.EncodeToPNG());
            }
            finally { RenderTexture.active = old; if (image) Destroy(image); }
            Log("SCREENSHOT " + file + " " + Screen.width + "x" + Screen.height);
        }

        void SnapshotUi(string file)
        {
            var canvas = GameObject.Find("DBPU Combat Wheel Canvas");
            Assert(canvas, "Wheel canvas missing");
            Canvas.ForceUpdateCanvases();
            var lines = new List<string>();
            foreach (var label in canvas.GetComponentsInChildren<TextMeshProUGUI>())
            {
                var corners = new Vector3[4];
                label.rectTransform.GetWorldCorners(corners);
                float left = corners.Min(p => p.x), right = corners.Max(p => p.x);
                float bottom = corners.Min(p => p.y), top = corners.Max(p => p.y);
                Assert(left >= -1 && right <= Screen.width + 1 && bottom >= -1 && top <= Screen.height + 1,
                    "UI text outside viewport: " + label.name + " " + left + "," + bottom + " -> " + right + "," + top);
                Assert(!label.isTextOverflowing, "UI label is truncated or overflowing: " + label.name + " / " + label.text);
                lines.Add(label.name + " | " + label.text + " | rect=" + left + "," + bottom + "," + right + "," + top + " | overflow=" + label.isTextOverflowing);
            }
            Assert(lines.Count > 10, "Incomplete wheel text hierarchy");
            File.WriteAllLines(Path.Combine(output, file), lines.ToArray());
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
            yield return new WaitForSecondsRealtime(3);
            // Wait for startup/reload callbacks from other mods to settle after the synchronous scene load.
            int stableEpoch = sceneEpoch, stableFrames = 0;
            while (stableFrames < 120)
            {
                yield return null;
                var scene = SceneManager.GetActiveScene();
                if (sceneEpoch != stableEpoch || scene.name != "Main" || !Global.main || !CatalogBehaviour.Main
                    || Global.main.gameObject.scene.handle != scene.handle || CatalogBehaviour.Main.gameObject.scene.handle != scene.handle)
                { stableEpoch = sceneEpoch; stableFrames = 0; }
                else stableFrames++;
            }
            if (!Check(() =>
            {
                if (!ModAPI.FindSpawnable("[DBPU] Gogito"))
                {
                    // A third-party InvokeMain postfix may throw after every mod registered but before
                    // Catalog.Populate reaches this native phase. Recover it only in this disposable QA.
                    var apply = typeof(CatalogBehaviour).GetMethod("ApplyModifications", BindingFlags.NonPublic | BindingFlags.Instance);
                    Assert(apply != null, "Native catalogue application method missing");
                    apply.Invoke(CatalogBehaviour.Main, null);
                    Log("QA ONLY: resumed native catalogue application after a third-party startup exception; no mod settings changed");
                }
                subject = Spawn("[DBPU] Gogito", new Vector3(-4, 30, 0));
                string otherName = ModAPI.FindSpawnable("[DBPU] (BM) Vegeta") ? "[DBPU] (BM) Vegeta" : "[DBPU] (BM) Goku";
                other = Spawn(otherName, new Vector3(4, 30, 0));
            })) yield break;
            yield return new WaitForSecondsRealtime(1.5f);
            if (!Check(() =>
            {
                person = subject.GetComponent<PersonBehaviour>(); otherPerson = other.GetComponent<PersonBehaviour>();
                menu = Menu(subject); otherMenu = Menu(other); ki = subject.GetComponent<KiSystem>(); otherKi = other.GetComponent<KiSystem>();
                Assert(person.IsAlive() && otherPerson.IsAlive() && ki && otherKi, "Native fighters did not initialize alive");
                wheel = CombatTransformationWheel.Instance;
                blockerBaseline = Global.main.ActiveUiBlockers;
                Assert(blockerBaseline == 0, "QA scene unexpectedly has an existing UI blocker");
                Global.main.CameraControlBehaviour.enabled = false;
                Global.main.camera.transform.position = new Vector3(0, 31, -10);
                Global.main.camera.orthographicSize = 5.8f;
                ki.currentKi = ki.maxKi; otherKi.currentKi = otherKi.maxKi;
                menu.ApplyForm(menu.BaseForm);
                Log("PASS native catalogue spawn: Gogito and " + other.name + "; forms=" + menu.TransformationsData.transformations.TransformationData.Count);
            })) yield break;
            yield return new WaitForSecondsRealtime(.9f);
            string initial = menu.Form, otherInitial = otherMenu.Form;
            float[] health = InitialHealth(person), otherHealth = InitialHealth(otherPerson);

            if (!Check(() =>
            {
                Open(menu);
                Assert(!wheel.Selection.Armed && wheel.Selection.Selected == FormIndex(initial), "Opening must highlight current without arming");
                SnapshotUi("01-current-unarmed-ui.txt");
            })) yield break;
            yield return new WaitForSecondsRealtime(.2f);
            yield return new WaitForEndOfFrame();
            if (!Check(() => { Capture("01-current-unarmed.png"); wheel.Close(true); Assert(CombatTransformationWheel.InputCaptured, "Close frame should retain input capture"); })) yield break;
            yield return null; yield return null;
            if (!Check(() =>
            {
                Released("Unarmed close");
                Assert(menu.Form == initial && ki.CurrentForm == initial, "Unarmed open/release transformed the fighter");
                SameHealth(health, person, "Unarmed close");
                Open(menu); SelectForm("SSJ1");
                var barrage = person.GetComponentInChildren<Barrage>();
                Assert(barrage, "Native barrage component missing");
                var firing = typeof(Barrage).GetField("isFiring", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert(firing != null, "Native barrage state missing");
                firing.SetValue(barrage, true);
                Assert(!DbpuInputUtil.Down(DBPU.BarrageActionId, KeyCode.H) && !DbpuInputUtil.Held(DBPU.BarrageActionId, KeyCode.H)
                    && DbpuInputUtil.Up(DBPU.BarrageActionId, KeyCode.H), "Input capture must release active abilities");
            })) yield break;
            yield return new WaitForSecondsRealtime(.2f);
            yield return new WaitForEndOfFrame();
            if (!Check(() =>
            {
                var barrage = person.GetComponentInChildren<Barrage>();
                Assert(!(bool)typeof(Barrage).GetField("isFiring", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(barrage), "Barrage retained firing state under menu capture");
                Log("PASS opening wheel releases an active barrage instead of swallowing key-up cleanup");
                Capture("02-gogito-ssj1-selected.png"); SnapshotUi("02-selected-ui.txt"); wheel.Close(false);
            })) yield break;
            yield return null; yield return null;
            if (!Check(() =>
            {
                Released("Explicit cancel"); Assert(menu.Form == initial, "Cancel changed form"); SameHealth(health, person, "Explicit cancel");
                Assert(otherMenu.Form == otherInitial, "Cancel changed the other fighter"); SameHealth(otherHealth, otherPerson, "Target isolation");
                Open(menu); SelectForm("SSJ1"); wheel.Close(true);
            })) yield break;
            yield return new WaitForSecondsRealtime(.9f);
            if (!Check(() =>
            {
                Released("Native apply");
                Assert(menu.Form == "SSJ1" && ki.CurrentForm == "SSJ1", "Wheel did not commit through native ApplyForm");
                Assert(subject.GetComponent<MultipliersAndStuffs>().FormMultiplier > menu.TransformationsData.GetEffectivePower(menu.TransformationsData.transformations.TransformationData[menu.BaseForm]), "Native power multiplier did not update");
                Assert(otherMenu.Form == otherInitial && otherKi.CurrentForm == otherInitial, "Transform request escaped its locked fighter");
                SameHealth(otherHealth, otherPerson, "Apply target isolation");
                Log("PASS unarmed release, cancel health invariance, native SSJ1 commit, target isolation and blocker release");
                menu.ApplyForm(menu.BaseForm);
            })) yield break;
            yield return new WaitForSecondsRealtime(.9f);

            if (!Check(() =>
            {
                Open(menu); wheel.Navigate(1); Assert(wheel.Selection.Armed, "Navigation did not arm");
                wheel.PointAt(Vector2.zero); Assert(!wheel.Selection.Armed && wheel.Selection.Selected < 0, "Center did not cancel pending selection");
                wheel.Close(true);
            })) yield break;
            yield return null; yield return null;
            if (!Check(() =>
            {
                Released("Deadzone cancellation"); Assert(menu.Form == menu.BaseForm, "Deadzone close transformed fighter");
                SameHealth(health, person, "Return to base/deadzone");
                Log("PASS deadzone cancels and native Base restores initial durability");
                var transformations = menu.TransformationsData.transformations;
                var template = transformations.TransformationData[menu.BaseForm];
                var clone = typeof(object).GetMethod("MemberwiseClone", BindingFlags.NonPublic | BindingFlags.Instance);
                for (int i = 0; i < 17; i++)
                {
                    string key = "QA_Only_" + i.ToString("00");
                    var value = (Stats)clone.Invoke(template, null);
                    value.ID = key; value.Name = "QA Form " + (i + 1); value.Description = "Temporary pagination-only QA form";
                    transformations.TransformationData.Add(key, value); transformations.FormOrder.Add(key); temporaryForms.Add(key);
                }
                Open(menu);
                Assert(wheel.Selection.Count > 8 && wheel.Selection.Pages >= 3, "QA failed to create multiple pages");
                wheel.ChangePage(-1); Assert(wheel.Selection.Page == wheel.Selection.Pages - 1, "Previous page did not wrap");
                wheel.ChooseSlot(wheel.Selection.VisibleCount - 1); int last = wheel.Selection.Selected;
                wheel.Navigate(1); Assert(wheel.Selection.Selected == 0, "Full-list navigation did not wrap from final partial page");
                wheel.Navigate(-1); Assert(wheel.Selection.Selected == last, "Full-list previous did not wrap to final form");
                SnapshotUi("03-pagination-ui.txt");
            })) yield break;
            yield return new WaitForSecondsRealtime(.2f);
            yield return new WaitForEndOfFrame();
            if (!Check(() => { Capture("03-pagination-last-page.png"); wheel.Close(false); })) yield break;
            yield return null; yield return null;
            if (!Check(() =>
            {
                Released("Pagination cancel");
                var transformations = menu.TransformationsData.transformations;
                foreach (string key in temporaryForms) { transformations.TransformationData.Remove(key); transformations.FormOrder.Remove(key); }
                temporaryForms.Clear();
                ki.currentKi = ki.maxKi * .03f; ki.baseRegen = 0;
                Open(menu); SelectForm("SSJBlue"); SnapshotUi("04-low-ki-ui.txt");
                Log("PASS >8 forms: page/whole-list wrapping, partial-page selection, temporary form cleanup");
            })) yield break;
            yield return new WaitForSecondsRealtime(.2f);
            yield return new WaitForEndOfFrame();
            if (!Check(() => { Capture("04-low-ki-blue-preview.png"); wheel.Close(false); })) yield break;
            yield return null; yield return null;
            if (!Check(() =>
            {
                Released("Low ki preview"); Assert(menu.Form == menu.BaseForm, "Low ki preview applied without commit");
                ki.currentKi = ki.maxKi;
                DBPU.AurasEnabled = true;
                menu.ApplyForm("SSJ1");
                var transition = person.GetComponent<KiFormTransition>();
                Assert(transition && transition.Busy, "Pending transition reversal test requires native aura transition");
                Assert(menu.Form == menu.BaseForm, "Transition committed before reversal could be tested");
                Open(menu); SelectForm(menu.BaseForm); wheel.Close(true);
                Log("PASS low ki navigation and text bounds; queued SSJ1 then explicitly selected still-current Base");
            })) yield break;
            yield return new WaitForSecondsRealtime(.9f);
            if (!Check(() =>
            {
                Released("Pending transition reversal");
                Assert(menu.Form == menu.BaseForm && ki.CurrentForm == menu.BaseForm, "Selecting current Base failed to cancel pending SSJ1");
                var transition = person.GetComponent<KiFormTransition>();
                Assert(!transition || !transition.Busy, "Transition retained a pending commit after reversal");
                SameHealth(health, person, "Pending transition reversal");
                Assert(otherMenu.Form == otherInitial, "Other fighter changed during navigation");
                SameHealth(otherHealth, otherPerson, "Final target isolation");
                Log("PASS latest-request reversal to current Base; no pending commit, health stable, blocker released\nALL CHECKS PASSED");
                Finish(true);
            })) yield break;
        }
    }
}
