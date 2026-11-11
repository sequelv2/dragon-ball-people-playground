using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DBPU
{
    // Opt-in disposable native-game verification of the menus: -dbpu-menu-qa <absolute folder>.
    // Never runs in an ordinary session and does not save mod settings.
    public sealed class MenuQa : MonoBehaviour
    {
        static bool started;
        string output;
        bool finished;
        int sceneEpoch, baseline, shotIndex;
        readonly List<string> uiErrors = new List<string>();
        GameObject subject;
        PersonBehaviour person;

        void Awake()
        {
            SceneManager.activeSceneChanged += SceneChanged;
            Application.logMessageReceived += LogReceived;
        }

        void OnDestroy()
        {
            SceneManager.activeSceneChanged -= SceneChanged;
            Application.logMessageReceived -= LogReceived;
        }

        void SceneChanged(Scene before, Scene after) { sceneEpoch++; }

        void LogReceived(string message, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && !message.StartsWith("[DBS UI]")) return;
            string text = message + "\n" + stack;
            if (text.IndexOf("DBPU.Db", StringComparison.Ordinal) < 0 && text.IndexOf("ConfigsWindow", StringComparison.Ordinal) < 0
                && text.IndexOf("MenuWindow", StringComparison.Ordinal) < 0 && text.IndexOf("[DBS UI]", StringComparison.Ordinal) < 0
                && text.IndexOf("MenuQa", StringComparison.Ordinal) < 0) return;
            uiErrors.Add(message);
            Log("UI ERROR " + text);
        }

        public static void StartIfRequested()
        {
            var args = Environment.GetCommandLineArgs();
            // Lets the older QA suites run without write access to the Mods folder as well.
            if (Array.IndexOf(args, "-dbpu-qa-no-save") >= 0) CharacterPowerCatalog.SavingDisabled = true;
            int index = Array.IndexOf(args, "-dbpu-menu-qa");
            if (index < 0 || index + 1 >= args.Length || started) return;
            started = true;
            if (!Path.IsPathRooted(args[index + 1]))
            {
                Debug.LogError("Menu QA requires an absolute output folder.");
                return;
            }
            CharacterPowerCatalog.SavingDisabled = true;
            Application.runInBackground = true;
            var go = new GameObject("DBPU opt-in menu QA");
            DontDestroyOnLoad(go);
            var qa = go.AddComponent<MenuQa>();
            qa.output = Path.GetFullPath(args[index + 1]);
            Directory.CreateDirectory(qa.output);
            File.Delete(Path.Combine(qa.output, "qa-ready.txt"));
            File.WriteAllText(Path.Combine(qa.output, "result.txt"), "DBPU menu QA started\n");
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
            File.WriteAllText(Path.Combine(output, "qa-ready.txt"), success ? "PASS\n" : "FAIL\n");
            Application.Quit(success ? 0 : 2);
        }

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
            Log("SCREENSHOT " + file);
        }

        IEnumerator Shot(string name, float wait = .7f)
        {
            yield return new WaitForSecondsRealtime(wait);
            yield return new WaitForEndOfFrame();
            shotIndex++;
            string file = shotIndex.ToString("00") + "-" + name + ".png";
            Check(() => Capture(file));
            ReportText(name);
        }

        // Clipped or overflowing labels are layout mistakes a screenshot can hide.
        void ReportText(string name)
        {
            foreach (var window in FindObjectsOfType<DbWindow>())
                foreach (var label in window.GetComponentsInChildren<TextMeshProUGUI>())
                    if (label.gameObject.activeInHierarchy && label.isTextTruncated && !string.IsNullOrEmpty(label.text))
                        Log("TRUNCATED [" + name + "] " + label.name + ": " + label.text);
        }

        static DbButton Button(string name)
        {
            foreach (var button in FindObjectsOfType<DbButton>())
                if (button.name == name) return button;
            return null;
        }

        void Click(string name)
        {
            var button = Button(name);
            Assert(button, "Button not found: " + name);
            button.OnClick?.Invoke();
        }

        void CloseAllWindows()
        {
            foreach (var window in FindObjectsOfType<DbWindow>()) window.CloseImmediate();
        }

        IEnumerator ExpectClean(string reason)
        {
            yield return null;
            yield return null;
            Check(() =>
            {
                Assert(FindObjectsOfType<DbWindow>().All(w => w.IsClosing), reason + ": a window stayed open");
                Assert(Global.main.ActiveUiBlockers == baseline, reason + ": leaked UI blocker, count " + Global.main.ActiveUiBlockers);
                Log("PASS " + reason);
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
            yield return new WaitForSecondsRealtime(3);
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

            if (!Check(() =>
            {
                person = subject.GetComponent<PersonBehaviour>();
                Assert(person && person.IsAlive(), "QA fighter did not initialize alive");
                baseline = Global.main.ActiveUiBlockers;
                Global.main.CameraControlBehaviour.enabled = false;
                Global.main.camera.transform.position = new Vector3(0f, 31f, -10f);
                Global.main.camera.orthographicSize = 5.8f;
                var scaler = Global.main.CanvasScaler;
                var font = DbTheme.Font;
                Log("ENV screen=" + Screen.width + "x" + Screen.height
                    + " scaler=" + (scaler ? scaler.uiScaleMode + " ref=" + scaler.referenceResolution + " match=" + scaler.matchWidthOrHeight : "none")
                    + " canvasMode=" + (scaler ? scaler.GetComponent<Canvas>().renderMode.ToString() : "none")
                    + " font=" + (font ? font.name : "none")
                    + " colorSpace=" + QualitySettings.activeColorSpace);
                Log("FONTS " + string.Join(", ", Resources.FindObjectsOfTypeAll<TMP_FontAsset>().Select(f => f.name).Distinct().ToArray()));
            })) yield break;

            yield return Settings();
            if (finished) yield break;
            yield return Dialogs();
            if (finished) yield break;
            yield return Powers();
            if (finished) yield break;
            yield return Forms();
            if (finished) yield break;
            yield return Skins();
            if (finished) yield break;
            yield return GiverAndInput();
            if (finished) yield break;
            yield return Wheels();
            if (finished) yield break;
            yield return Editors();
            if (finished) yield break;

            if (uiErrors.Count > 0)
            {
                Log("FAIL " + uiErrors.Count + " UI errors were logged");
                Finish(false);
                yield break;
            }
            Log("PASS all menus");
            Finish(true);
        }

        IEnumerator Settings()
        {
            GameObject host = null;
            if (!Check(() =>
            {
                host = new GameObject("QA Config Menu");
                host.AddComponent<Configs>();
                Assert(FindObjectsOfType<DbWindow>().Length == 1, "Settings window did not open");
                Assert(Global.main.ActiveUiBlockers == baseline + 1, "Settings must own one UI blocker");
            })) yield break;
            yield return Shot("settings-physical");
            foreach (string tab in new[] { "Ki", "Ki Deflect", "Ki Charge", "Beams", "Misc", "Singularity" })
            {
                if (!Check(() => Click("Tab " + tab))) yield break;
                yield return Shot("settings-" + tab.ToLowerInvariant().Replace(" ", "-"));
            }
            if (!Check(() =>
            {
                Click("Tab Ki Charge");
                var scroll = FindObjectsOfType<DbScroll>().First(s => s.gameObject.activeInHierarchy && s.name == "Page");
                Assert(scroll.ContentHeight > scroll.Viewport.rect.height, "Long settings page should scroll");
                scroll.ScrollTo(scroll.MaxOffset);
            })) yield break;
            yield return Shot("settings-scrolled");
            if (!Check(() =>
            {
                var search = FindObjectsOfType<DbField>().First(f => f.name == "Search");
                search.Input.text = "beam";
            })) yield break;
            yield return Shot("settings-search");
            if (!Check(() =>
            {
                CloseAllWindows();
                Destroy(host);
            })) yield break;
            yield return ExpectClean("settings closes cleanly");
        }

        IEnumerator Dialogs()
        {
            float scale = 1.5f;
            if (!Check(() => DbpuConfigMenuUI.OpenAccented(DbTheme.Shenron, "SHENRON  —  2 WISHES LEFT", null,
                new DbpuMenuButton("Revive and heal every fighter  (2 wishes)", null),
                new DbpuMenuButton("<color=#888888>Locked option", null),
                new DbpuMenuButton("A rain of Senzu Beans", null),
                new DbpuMenuButton("Never mind", null)))) yield break;
            yield return Shot("dialog-shenron");
            if (!Check(() => DbpuConfigMenuUI.Open("Aura Scale & Position", null,
                new[] { new DbpuMenuButton("Reset Defaults", null), new DbpuMenuButton("Close", null) },
                new[] { new DbpuMenuSlider("Scale", () => scale, v => scale = v, .5f, 20f, false) }, null))) yield break;
            yield return Shot("dialog-sliders");
            if (!Check(DbpuConfigMenuUI.Hide)) yield break;
            yield return ExpectClean("dialogs close cleanly");
        }

        IEnumerator Powers()
        {
            PowerContextMenu menu = null;
            if (!Check(() =>
            {
                menu = person.Limbs[0].GetComponent<PowerContextMenu>();
                Assert(menu, "Fighter has no powers menu");
                menu.OpenPowersMenu();
                Assert(PowerContextMenu.OpenWindow != null, "Powers window did not open");
                Assert(Global.main.ActiveUiBlockers == baseline, "Powers window must stay non-modal");
            })) yield break;
            yield return Shot("powers");

            CharacterPowers.PowerStats tunable = null;
            if (!Check(() =>
            {
                var window = PowerContextMenu.OpenWindow;
                var list = menu.PowersData.GetOrderedPowers();
                tunable = list.FirstOrDefault(p => p.ID == "Kamehameha") ?? list.FirstOrDefault(p => !p.Passive);
                Assert(tunable != null, "Fighter has no switchable power");
                window.Select(tunable, true);
            })) yield break;
            yield return Shot("powers-selected");

            bool before = false;
            if (!Check(() =>
            {
                before = tunable.Active;
                Click("Toggle");
                Assert(tunable.Active != before, "Toggle did not change " + tunable.ID);
            })) yield break;
            yield return Shot("powers-toggled");
            if (!Check(() =>
            {
                Click("Toggle");
                Assert(tunable.Active == before, "Second toggle did not restore " + tunable.ID);
                Click("Chip Active");
            })) yield break;
            yield return Shot("powers-filter-active");
            if (!Check(() =>
            {
                Click("Chip All");
                FindObjectsOfType<DbField>().First(f => f.name == "Search").Input.text = "kame";
            })) yield break;
            yield return Shot("powers-search");
            if (!Check(CloseAllWindows)) yield break;
            yield return ExpectClean("powers close cleanly");
        }

        TransformationContextMenu FormsMenu()
        {
            var head = person.transform.Find("Head");
            var menu = head ? head.GetComponent<TransformationContextMenu>() : null;
            Assert(menu, "Fighter has no transformations menu");
            return menu;
        }

        IEnumerator Forms()
        {
            TransformationContextMenu menu = null;
            List<KeyValuePair<string, CharacterTransformations.Transformations.TransformationStats>> forms = null;
            if (!Check(() =>
            {
                menu = FormsMenu();
                forms = menu.TransformationsData.transformations.GetOrderedEntries();
                Assert(forms.Count > 1, "Fighter needs several forms");
                var clock = System.Diagnostics.Stopwatch.StartNew();
                menu.OpenTransformationsMenu();
                Log("OPEN forms " + clock.ElapsedMilliseconds + " ms, " + forms.Count + " forms");
                Assert(TransformationContextMenu.OpenWindow != null, "Forms window did not open");
                Assert(Global.main.ActiveUiBlockers == baseline, "Forms window must stay non-modal");
            })) yield break;
            yield return Shot("forms");

            int target = Mathf.Min(1, forms.Count - 1);
            if (!Check(() => TransformationContextMenu.OpenWindow.Select(target, true))) yield break;
            yield return Shot("forms-preview");
            if (!Check(() => TransformationContextMenu.OpenWindow.Select(forms.Count - 1, true))) yield break;
            yield return Shot("forms-last");

            string before = null;
            if (!Check(() =>
            {
                before = menu.Form;
                TransformationContextMenu.OpenWindow.Select(target, false);
                Click("Accept");
                Assert(TransformationContextMenu.OpenWindow == null, "Accept should close the window");
                Assert(GameObject.Find("DBPU form title"), "Menu transform showed no form title");
            })) yield break;
            yield return Shot("form-title-menu", .35f);
            yield return new WaitForSecondsRealtime(3.5f);
            if (!Check(() =>
            {
                Assert(menu.Form != before, "Accept did not change the form, still " + menu.Form);
                Log("PASS forms accept applied " + menu.Form);
                menu.QuickToggleTransform();
                Assert(GameObject.Find("DBPU form title"), "Quick transform showed no form title");
            })) yield break;
            yield return Shot("form-title-quick", .35f);
            yield return new WaitForSecondsRealtime(2f);
            yield return ExpectClean("forms close cleanly");
        }

        IEnumerator Skins()
        {
            SkinContextMenu menu = null;
            int page = 0;
            if (!Check(() =>
            {
                menu = person.GetComponentInChildren<SkinContextMenu>();
                Assert(menu, "Fighter has no skins menu");
                page = menu.Page;
                var clock = System.Diagnostics.Stopwatch.StartNew();
                menu.OpenSkinsMenu();
                Log("OPEN skins " + clock.ElapsedMilliseconds + " ms, " + menu.RefreshSkinList().Count + " skins");
                Assert(SkinContextMenu.OpenWindow != null, "Skins window did not open");
            })) yield break;
            yield return Shot("skins");
            if (!Check(() =>
            {
                var window = SkinContextMenu.OpenWindow;
                if (window.ShownCount > 1) window.Select((window.Selected + 1) % window.ShownCount, 1, true);
            })) yield break;
            yield return Shot("skins-next");
            if (!Check(() =>
            {
                SkinContextMenu.OpenWindow.SelectVariant(1);
                Click("Cancel");
            })) yield break;
            yield return new WaitForSecondsRealtime(.3f);
            if (!Check(() =>
            {
                Assert(SkinContextMenu.OpenWindow == null, "Cancel should close the skins window");
                Assert(menu.Page == menu.PreviousPage && menu.CurrentDamage == menu.PreviousDamage, "Cancel did not restore the selection");
                menu.Page = page;
            })) yield break;
            yield return ExpectClean("skins close cleanly");
        }

        IEnumerator GiverAndInput()
        {
            if (!Check(() =>
            {
                var clock = System.Diagnostics.Stopwatch.StartNew();
                PowerGiverUI.Open();
                Log("OPEN giver " + clock.ElapsedMilliseconds + " ms, " + PowerGrantCatalog.Entries.Count + " powers");
                Assert(PowerGiverUI.IsOpen, "Power Giver did not open");
            })) yield break;
            yield return Shot("giver");
            if (!Check(() => FindObjectsOfType<DbField>().First(f => f.name == "Search").Input.text = "beam")) yield break;
            yield return Shot("giver-search");
            if (!Check(() => Assert(PowerGiverUI.ShownCount > 0, "Search found no beam powers"))) yield break;
            if (!Check(PowerGiverUI.Close)) yield break;
            yield return new WaitForSecondsRealtime(.4f);
            if (!Check(() => Assert(!PowerGiverState.Armed, "Closing Power Giver must disarm it"))) yield break;
            yield return ExpectClean("giver closes cleanly");

            float applied = -1f;
            if (!Check(() => CharacterPowerInputUI.Open("Set Goku Power", "Current: 150", "Default Base: 150", 150f, v => applied = v))) yield break;
            yield return Shot("power-input");
            if (!Check(() =>
            {
                FindObjectsOfType<DbField>().First(f => f.name == "Value").Input.text = "2500";
                Click("Accept");
                Assert(Math.Abs(applied - 2500f) < .01f, "Apply did not deliver the typed value, got " + applied);
            })) yield break;
            yield return new WaitForSecondsRealtime(.4f);
            yield return ExpectClean("power input closes cleanly");
        }

        IEnumerator Wheels()
        {
            var center = new Vector2(Screen.width * .5f, Screen.height * .5f);
            var forms = CombatTransformationWheel.Instance;
            if (!Check(() =>
            {
                Assert(forms.OpenFor(FormsMenu(), center, KeyCode.None), "Forms wheel did not open");
                Assert(Global.main.ActiveUiBlockers == baseline + 1, "Wheel must own one UI blocker");
            })) yield break;
            yield return Shot("wheel-forms");
            if (!Check(() => forms.ChooseSlot(Mathf.Min(1, forms.Selection.VisibleCount - 1)))) yield break;
            yield return Shot("wheel-forms-selected");
            if (!Check(() => forms.Close(true))) yield break;
            yield return Shot("form-title", .35f);
            yield return new WaitForSecondsRealtime(3f);
            if (!Check(() => FormsMenu().ApplyForm(FormsMenu().BaseForm))) yield break;
            yield return new WaitForSecondsRealtime(2f);

            var abilities = CombatAbilityWheel.Instance;
            if (!Check(() =>
            {
                var hand = person.Limbs.First(KiHandLoadout.IsHand);
                Assert(abilities.OpenFor(KiHandLoadout.GetOrCreate(hand), center, KeyCode.None), "Ability wheel did not open");
            })) yield break;
            yield return Shot("wheel-abilities");
            if (!Check(() => abilities.ChooseSlot(0))) yield break;
            yield return Shot("wheel-abilities-selected");
            if (!Check(() => abilities.Close(false))) yield break;
            yield return new WaitForSecondsRealtime(.2f);
            yield return ExpectClean("wheels close cleanly");
        }

        IEnumerator Editors()
        {
            if (!Check(() =>
            {
                TransformationMakerUI.Open();
                TransformationMakerUI.OnWorldPersonClicked(person);
                Assert(TransformationMakerUI.IsOpen, "Transformation Maker did not open");
            })) yield break;
            yield return Shot("editor-transformation-maker", 1f);
            if (!Check(TransformationMakerUI.Close)) yield break;
            yield return new WaitForSecondsRealtime(.2f);

            if (!Check(() =>
            {
                CustomSkinMakerUI.Open();
                CustomSkinMakerUI.OnWorldPersonClicked(person);
            })) yield break;
            yield return Shot("editor-skin-maker", 1f);
            if (!Check(CustomSkinMakerUI.Close)) yield break;
            yield return new WaitForSecondsRealtime(.2f);
            if (!Check(() => Assert(Global.main.ActiveUiBlockers == baseline, "Editors leaked or over-released UI blockers: " + Global.main.ActiveUiBlockers))) yield break;

            if (!Check(() =>
            {
                var target = person.GetComponent<LiveSpriteEditor.CharacterSpriteEditorTarget>() ?? person.GetComponentInChildren<LiveSpriteEditor.CharacterSpriteEditorTarget>();
                if (!target) target = person.gameObject.AddComponent<LiveSpriteEditor.CharacterSpriteEditorTarget>();
                LiveSpriteEditor.SpriteEditorRuntime.Open(target, person.Limbs[0]);
                Assert(LiveSpriteEditor.SpriteEditorRuntime.IsOpen, "Sprite Editor did not open");
            })) yield break;
            yield return Shot("editor-sprite", 1.5f);
            if (!Check(() =>
            {
                var close = FindObjectsOfType<Button>().First(b => b.name == "CloseButton");
                close.onClick.Invoke();
            })) yield break;
            yield return new WaitForSecondsRealtime(.5f);
            if (!Check(() => Log("PASS editors opened; sprite editor open=" + LiveSpriteEditor.SpriteEditorRuntime.IsOpen + " blockers=" + Global.main.ActiveUiBlockers))) yield break;
        }
    }
}
