using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DBPU
{
    // Opt-in native-game check of the power scale: -dbpu-power-scale-qa <absolute folder>.
    // Spawns real fighters and reads the multiplier each form shows and actually applies.
    // Never runs in an ordinary session and does not save mod settings.
    public sealed class PowerScaleQa : MonoBehaviour
    {
        static bool started;
        string output;
        bool finished;

        static readonly (string Name, float Innate)[] Fighters =
        {
            ("[DBPU] (BM) Goku", 35f), ("[DBPU] (BM) Gohan", 11f), ("[DBPU] (BM) Vegeta", 55f),
            ("[DBPU] (DBSSH) Goku", 35f), ("[DBPU] (DBSSH) Gohan", 11f), ("[DBPU] (DBSSH) Vegeta", 55f),
            ("[DBPU] Frieza", 100000000f), ("[DBPU] (BM) Gogeta", 1f), ("[DBPU] Vegito", 1f), ("[DBPU] Gogito", 1f)
        };

        // Frieza's real strength must not move when his menus count from Final Form.
        static readonly Dictionary<string, float> FriezaRealPowers = new Dictionary<string, float>
        {
            { "FirstForm", 12500000f }, { "SecondForm", 25000000f }, { "ThirdForm", 50000000f }, { "FinalForm", 100000000f },
            { "FullPower", 200000000f }, { "GoldenForm", 5000000000000f }, { "BlackForm", 1000000000000000f }
        };

        public static void StartIfRequested()
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-dbpu-power-scale-qa");
            if (index < 0 || index + 1 >= args.Length || started) return;
            started = true;
            if (!Path.IsPathRooted(args[index + 1])) { Debug.LogError("Power scale QA requires an absolute output folder."); return; }
            CharacterPowerCatalog.SavingDisabled = true;
            Application.runInBackground = true;
            var go = new GameObject("DBPU opt-in power scale QA");
            DontDestroyOnLoad(go);
            var qa = go.AddComponent<PowerScaleQa>();
            qa.output = Path.GetFullPath(args[index + 1]);
            Directory.CreateDirectory(qa.output);
            File.Delete(Path.Combine(qa.output, "qa-ready.txt"));
            File.WriteAllText(Path.Combine(qa.output, "result.txt"), "DBPU power scale QA started\n");
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

        static bool Same(double actual, double expected, double tolerance = .001)
        {
            return Math.Abs(actual / expected - 1) < tolerance;
        }

        // Every form the catalogue reports: menus count from base x1, the real power adds innate strength.
        void CheckCatalogue(GameObject subject, string fighter, float innate)
        {
            var identity = subject.GetComponent<DbpuCharacterIdentity>();
            var forms = subject.GetComponent<CharacterTransformations>();
            var menu = subject.transform.Find("Head").GetComponent<TransformationContextMenu>();
            Assert(identity && forms && menu, fighter + ": no identity, forms or menu");
            Assert(!CharacterPowerCatalog.HasBaseOverride(identity.CharacterId), fighter + ": base override survived");
            Assert(CharacterPowerCatalog.GetInnatePower(identity.CharacterId) == innate, fighter + ": wrong innate strength");
            float scale = identity.CharacterId == "Gogito" ? DBPU.GogitoBasePower() : 1f;
            foreach (var entry in forms.transformations.GetOrderedEntries())
            {
                float shown = forms.GetDisplayPower(entry.Value), real = forms.GetEffectivePower(entry.Value);
                string label = menu.FormatInfoMultiplier(entry.Value.PowerMultiplier, entry.Value.ID);
                Log(fighter + " | " + entry.Key + " | " + entry.Value.Name + " | shown " + label + " | real x" + real.ToString("0.###"));
                Assert(Same(real, shown * innate), fighter + " " + entry.Key + " real x" + real + " shown x" + shown);
                if (identity.CharacterId == "BM/Freeza") Assert(Same(real, FriezaRealPowers[entry.Key]), fighter + " " + entry.Key + " real x" + real);
                if (entry.Key == menu.BaseForm) Assert(Same(shown, scale), fighter + " base shows x" + shown);
                else if (CharacterPowerCatalog.TryGetSaiyanFormPower(entry.Value.Name, out float power))
                    Assert(Same(shown, power * scale), fighter + " " + entry.Value.Name + " shows x" + shown);
            }
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

            if (!Check(() =>
            {
                if (ModAPI.FindSpawnable("[DBPU] (BM) Goku")) return;
                // A third-party startup exception can stop the catalogue; recover it in this disposable QA only.
                var apply = typeof(CatalogBehaviour).GetMethod("ApplyModifications", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert(apply != null, "Native catalogue application method missing");
                apply.Invoke(CatalogBehaviour.Main, null);
                Log("QA ONLY: resumed native catalogue application after a third-party startup exception");
            })) yield break;

            foreach (var fighter in Fighters)
            {
                GameObject subject = null;
                if (!Check(() => subject = Spawn(fighter.Name, new Vector3(0f, 30f, 0f)))) yield break;
                yield return new WaitForSecondsRealtime(1.5f);

                PersonBehaviour person = null;
                MultipliersAndStuffs mult = null;
                CharacterTransformations forms = null;
                TransformationContextMenu menu = null;
                float baseReal = 0f, baseHealth = 0f, baseBreaking = 0f;
                bool gogito = false;
                if (!Check(() =>
                {
                    CheckCatalogue(subject, fighter.Name, fighter.Innate);
                    person = subject.GetComponent<PersonBehaviour>();
                    mult = subject.GetComponent<MultipliersAndStuffs>();
                    forms = subject.GetComponent<CharacterTransformations>();
                    menu = subject.transform.Find("Head").GetComponent<TransformationContextMenu>();
                    gogito = subject.GetComponent<DbpuCharacterIdentity>().CharacterId == "Gogito";
                    baseReal = forms.GetEffectivePower(forms.transformations.TransformationData[menu.BaseForm]);
                    baseHealth = person.Limbs[0].InitialHealth;
                    baseBreaking = person.Limbs[0].BreakingThreshold;
                    Assert(Same(mult.FormMultiplier, baseReal), fighter.Name + " spawned at x" + mult.FormMultiplier + ", expected x" + baseReal);
                    Assert(Same(menu.PreviousMultiplier, baseReal), fighter.Name + " previous multiplier x" + menu.PreviousMultiplier);
                    Assert(Same(mult.FinalMultiplier, mult.FormMultiplier * CombatPowerController.Mode(person)), fighter.Name + " final x" + mult.FinalMultiplier);
                    Log("PASS " + fighter.Name + " at spawn: real x" + mult.FormMultiplier.ToString("0") + ", head HP " + baseHealth
                        + ", rift power/threshold " + (RealityRift.EffectivePower(person) / RealityRift.PowerThreshold).ToString("0.##"));
                })) yield break;

                // The value the fight actually uses, through the real transformation path.
                var formData = forms.transformations.TransformationData;
                string formId = formData.ContainsKey("SSJBlue") ? "SSJBlue" : formData.ContainsKey("SSJ1") ? "SSJ1" : "FullPower";
                if (!Check(() => menu.ApplyForm(formId))) yield break;
                yield return new WaitForSecondsRealtime(2f);
                if (!Check(() =>
                {
                    float expected = forms.GetEffectivePower(forms.transformations.TransformationData[formId]);
                    Assert(Same(mult.FormMultiplier, expected), fighter.Name + " " + formId + " applied x" + mult.FormMultiplier + ", expected x" + expected);
                    if (!gogito) Assert(Same(person.Limbs[0].InitialHealth, baseHealth * expected / baseReal, .01), fighter.Name + " " + formId + " HP " + person.Limbs[0].InitialHealth);
                    Log("PASS " + fighter.Name + " " + formId + ": real x" + mult.FormMultiplier.ToString("0") + ", shown " + menu.FormatInfoMultiplier(0f, formId));
                })) yield break;

                // Running out of ki drops to base without compounding innate strength.
                if (!Check(() => forms.ApplyTransformation(person, menu.BaseForm, true))) yield break;
                yield return new WaitForSecondsRealtime(1f);
                if (!Check(() =>
                {
                    Assert(Same(mult.FormMultiplier, baseReal), fighter.Name + " depleted at x" + mult.FormMultiplier);
                    if (!gogito)
                    {
                        Assert(Same(person.Limbs[0].InitialHealth, baseHealth, .01), fighter.Name + " depleted HP " + person.Limbs[0].InitialHealth + ", was " + baseHealth);
                        // A fighter without innate strength keeps its spawn threshold until the first weight recalculation.
                        if (fighter.Innate != 1f) Assert(Same(person.Limbs[0].BreakingThreshold, baseBreaking), fighter.Name + " breaking threshold " + person.Limbs[0].BreakingThreshold + ", was " + baseBreaking);
                    }
                    Log("PASS " + fighter.Name + " after ki depletion: real x" + mult.FormMultiplier.ToString("0"));
                    Destroy(subject);
                })) yield break;
                yield return new WaitForSecondsRealtime(.5f);
            }
            Log("PASS all fighters");
            Finish(true);
        }
    }
}
