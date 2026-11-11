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
    // Only runs in a disposable instance explicitly started with
    // -dbpu-absorption-qa <absolute output directory>.
    public sealed class BuuAbsorptionQa : MonoBehaviour
    {
        static bool started;
        string output;
        bool finished;
        int errors, passes;
        Camera captureCamera;
        PersonBehaviour attacker;
        static readonly Color EnergyColor = new Color(.35f, .8f, 1f, 1f);

        public static void StartIfRequested()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-dbpu-absorption-qa");
            if (index < 0 || index + 1 >= args.Length || started) return;
            started = true;
            if (!Path.IsPathRooted(args[index + 1]))
            { Debug.LogError("Absorption QA requires an absolute output directory."); return; }
            CharacterPowerCatalog.SavingDisabled = true;
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            var go = new GameObject("DBPU opt-in absorption QA");
            DontDestroyOnLoad(go);
            var qa = go.AddComponent<BuuAbsorptionQa>();
            qa.output = Path.GetFullPath(args[index + 1]);
            Directory.CreateDirectory(qa.output);
            File.Delete(Path.Combine(qa.output, "qa-ready.txt"));
            File.WriteAllText(Path.Combine(qa.output, "result.txt"), "DBPU native Buu absorption QA started\n");
            qa.StartCoroutine(qa.Run());
        }

        void Awake() { Application.logMessageReceived += ReceivedLog; }
        void OnDestroy()
        {
            Application.logMessageReceived -= ReceivedLog;
            if (captureCamera) Destroy(captureCamera.gameObject);
        }
        void ReceivedLog(string message, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            string diagnostic = message + stack;
            if (!diagnostic.Contains("BuuAbsorption") && !diagnostic.Contains("BuuRegeneration")) return;
            errors++;
            Log("RUNTIME ERROR " + message + "\n" + stack);
        }
        void Log(string message)
        { if (output != null) File.AppendAllText(Path.Combine(output, "result.txt"), message + "\n"); }
        void Pass(string message) { passes++; Log("PASS " + message); }
        static void Assert(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
        bool Check(Action action)
        {
            if (finished) return false;
            try { action(); return true; }
            catch (Exception ex) { Log("FAIL " + ex); Finish(false); return false; }
        }
        void Finish(bool success)
        {
            if (finished) return;
            finished = true;
            Time.timeScale = 1f;
            Log("Assertions passed: " + passes + "; absorption runtime errors: " + errors);
            File.WriteAllText(Path.Combine(output, "qa-ready.txt"), success ? "PASS\n" : "FAIL\n");
            Application.Quit(success ? 0 : 2);
        }

        PersonBehaviour Spawn(string name, float x, bool physical = false)
        {
            var asset = ModAPI.FindSpawnable(name);
            var human = ModAPI.FindSpawnable("Human");
            Assert(asset && human && human.Prefab, "Missing native catalogue entry: " + name);
            var root = Instantiate(human.Prefab, new Vector3(x, 40f, 0f), Quaternion.identity);
            root.AddComponent<DeregisterBehaviour>();
            root.AddComponent<TexturePackApplier>();
            root.AddComponent<AudioSourceTimeScaleBehaviour>();
            root.AddComponent<SerialiseInstructions>().OriginalSpawnableAsset = asset;
            root.name = asset.name;
            CatalogBehaviour.SpawnedGameObjects.Add(root);
            CatalogBehaviour.PerformMod(asset, root);
            Assert(root, "Native modification removed fixture: " + name);
            var person = root.GetComponent<PersonBehaviour>();
            Assert(person && person.Limbs != null, "Fixture did not create a person");
            foreach (var body in root.GetComponentsInChildren<Rigidbody2D>())
            {
                body.gravityScale = 0f;
                body.velocity = Vector2.zero;
                body.angularVelocity = 0f;
                if (!physical) body.bodyType = RigidbodyType2D.Kinematic;
            }
            foreach (var limb in person.Limbs) if (limb) limb.ImmuneToDamage = false;
            var regen = person.GetComponent<BuuRegeneration>();
            if (regen && regen.BodyBlueprint != null)
                for (int i = 0; i < regen.BodyBlueprint.Length; i++)
                {
                    var blueprint = regen.BodyBlueprint[i];
                    blueprint.Gravity = 0f;
                    regen.BodyBlueprint[i] = blueprint;
                }
            return person;
        }
        static LimbBehaviour Part(PersonBehaviour person, string name)
        { return person.Limbs.First(limb => limb && limb.name == name); }
        static bool Gone(LimbBehaviour limb)
        { return !limb || !limb.gameObject.activeInHierarchy || !limb.PhysicalBehaviour || limb.PhysicalBehaviour.isDisintegrated; }
        static float Progress(LimbBehaviour limb)
        {
            var erosion = limb ? limb.GetComponent<EnergyErosion>() : null;
            return erosion ? erosion.Progress : 0f;
        }
        static float Power(LimbBehaviour limb, float ratio)
        { return KiCombatMath.Cap((double)EnergyAnnihilation.Resistance(limb) * ratio); }
        void Dose(LimbBehaviour limb, float ratio, float seconds)
        {
            if (Gone(limb)) return;
            EnergyAnnihilation.Expose(attacker, limb, Power(limb, ratio), limb.transform.position, seconds, EnergyColor);
        }

        IEnumerator Capture(Vector2 centre, string name, float size = 2.1f)
        {
            yield return new WaitForEndOfFrame();
            Check(() =>
            {
                if (!captureCamera)
                {
                    captureCamera = new GameObject("Absorption QA camera").AddComponent<Camera>();
                    captureCamera.CopyFrom(Global.main.camera);
                    captureCamera.enabled = false;
                    captureCamera.clearFlags = CameraClearFlags.SolidColor;
                    captureCamera.backgroundColor = new Color(.045f, .05f, .065f, 1f);
                }
                captureCamera.transform.position = new Vector3(centre.x, centre.y, -10f);
                captureCamera.transform.rotation = Quaternion.identity;
                captureCamera.orthographic = true;
                captureCamera.orthographicSize = size;
                captureCamera.aspect = 16f / 9f;
                var target = new RenderTexture(1280, 720, 24);
                var screenshot = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                var previous = RenderTexture.active;
                try
                {
                    captureCamera.targetTexture = target;
                    captureCamera.Render();
                    RenderTexture.active = target;
                    screenshot.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                    screenshot.Apply();
                    File.WriteAllBytes(Path.Combine(output, name + ".png"), screenshot.EncodeToPNG());
                }
                finally
                {
                    captureCamera.targetTexture = null;
                    RenderTexture.active = previous;
                    Destroy(screenshot);
                    target.Release();
                    Destroy(target);
                }
                Log("SCREENSHOT " + name);
            });
        }

        static void Near(double actual, double expected, string message)
        { Assert(Math.Abs(actual - expected) <= Math.Max(1d, expected) * .0002d, message + ": " + actual + " != " + expected); }

        IEnumerator WaitAbsorption(BuuAbsorption ability)
        {
            float deadline = Time.realtimeSinceStartup + 12f;
            while (ability && ability.IsAbsorbing && Time.realtimeSinceStartup < deadline) yield return null;
            Check(() => Assert(ability && !ability.IsAbsorbing, "Absorption did not finish within deadline"));
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
            yield return new WaitForSecondsRealtime(5f);
            PersonBehaviour buu = null, first = null, second = null, third = null, competitor = null;
            BuuAbsorption ability = null;
            Vector2 centre = Vector2.zero;
            double originalPower = 0d, firstPower = 0d, secondPower = 0d, thirdPower = 0d;
            Sprite head = null;
            if (!Check(() =>
            {
                if (!ModAPI.FindSpawnable("[DBPU] (BM) Goku"))
                    typeof(CatalogBehaviour).GetMethod("ApplyModifications", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(CatalogBehaviour.Main, null);
                Global.main.SetPausedMenu(false); Time.timeScale = 1f;
                foreach (var canvas in FindObjectsOfType<Canvas>()) canvas.enabled = false;
                DBPU.VoiceLines = false; DBPU.KiDrainMultiplier = 0f; DBPU.DamageMultiplier = 1f;
                buu = Spawn("[DBPU] Super Buu", 0f);
                first = Spawn("[DBPU] (BM) Goku", 3f);
                second = Spawn("[DBPU] Majin Buu", 5f);
                third = Spawn("Human", 6.5f);
                competitor = Spawn("[DBPU] Kid Buu", -3f);
                ability = buu.GetComponent<BuuAbsorption>();
                Assert(ability, "Super Buu did not receive absorption");
                Assert(second.GetComponent<BuuAbsorption>() && competitor.GetComponent<BuuAbsorption>(), "Other Majin forms missing absorption");
                centre = Part(buu, "MiddleBody").transform.position;
                head = Part(buu, "Head").GetComponent<SpriteRenderer>().sprite;
                var evil = Spawn("[DBPU] Evil Buu", -20f);
                Assert(evil.GetComponent<BuuAbsorption>(), "Evil Buu missing absorption"); Destroy(evil.gameObject);
            })) yield break;
            yield return new WaitForSeconds(1.2f);
            if (!Check(() =>
            {
                originalPower = BuuAbsorption.MaximumPower(buu);
                firstPower = BuuAbsorption.MaximumPower(first);
                secondPower = BuuAbsorption.MaximumPower(second);
                thirdPower = BuuAbsorption.MaximumPower(third);
                Assert(firstPower > secondPower && secondPower > thirdPower, "Fixtures must have decreasing strength");
                var transforms = first.GetComponent<CharacterTransformations>();
                var mult = first.GetComponent<MultipliersAndStuffs>();
                double strongest = transforms.transformations.TransformationData.Max(entry => (double)transforms.GetEffectivePower(entry.Value));
                double average = first.Limbs.Average(limb => (double)limb.InitialHealth);
                Near(firstPower, average / mult.FormMultiplier * strongest, "Highest available form calculation");
                first.GetComponent<CombatPowerController>().SetOutput(.1f);
                Near(BuuAbsorption.MaximumPower(first), firstPower, "Suppressed output reduced available maximum");
                Pass("all four Majin forms attach absorption; target contributes its strongest available form");
                Assert(!ability.TryAbsorb(buu), "Self absorption accepted");
                var distant = Spawn("Human", 30f);
                Assert(!ability.TryAbsorb(distant), "Distant absorption accepted"); Destroy(distant.gameObject);
                Assert(ability.NearestTarget() == first || ability.NearestTarget() == competitor, "Nearest-target selection failed");
                Pass("self and distant targets rejected; nearby target selection works");
                Assert(ability.TryAbsorb(first), "Initial capture failed");
                Assert(!ability.TryAbsorb(first), "Duplicate request accepted");
                Assert(!competitor.GetComponent<BuuAbsorption>().TryAbsorb(first), "Two Buus claimed the same person");
                Assert(first.GetComponentsInChildren<Rigidbody2D>().All(body => !body.simulated), "Victim physics was not held");
                Pass("capture freezes real target physics and prevents duplicate/concurrent absorption");
            })) yield break;
            yield return Capture(centre + new Vector2(1.5f, 0f), "01-tissue-flight", 2.8f);
            yield return new WaitForSeconds(.7f);
            yield return Capture(centre + new Vector2(1.5f, 0f), "02-wrapped", 2.8f);
            float progress = ability.Progress;
            if (!Check(() => Global.main.TogglePaused())) yield break;
            yield return new WaitForSecondsRealtime(.4f);
            bool pauseOk = Check(() =>
            {
                Near(ability.Progress, progress, "Paused absorption advanced");
                Near(ability.AddedPower, 0d, "Paused absorption awarded power");
                Pass("simulation pause freezes capture and awards no premature strength");
            });
            if (Global.main.Paused) Global.main.TogglePaused(); Time.timeScale = 1f;
            if (!pauseOk) yield break;
            if (!Check(() =>
            {
                ability.CancelAbsorption();
                Assert(first && first.GetComponentsInChildren<Rigidbody2D>().All(body => body.simulated), "Cancel did not restore victim simulation");
                Assert(first.Limbs.All(limb => limb.GetComponent<SpriteRenderer>().enabled), "Cancel hid victim body");
                Near(ability.AddedPower, 0d, "Cancellation awarded power");
                Pass("cancel restores the victim and leaves strength unchanged");
                Assert(ability.TryAbsorb(first), "Restart after cancel failed");
            })) yield break;
            yield return WaitAbsorption(ability);
            yield return new WaitForSeconds(.6f);
            if (finished) yield break;
            if (!Check(() =>
            {
                Assert(!first, "Absorbed victim remained in scene");
                Near(ability.AddedPower, firstPower, "First bonus is not additive");
                Near(BuuAbsorption.MaximumPower(buu), originalPower + firstPower, "Final native power does not equal Buu plus target");
                Assert(ability.AbsorbedCount == 1 && ability.StrongestName == "Goku", "Strongest outfit identity missing");
                Assert(ability.Outfit.Length == 13 && ability.Outfit.All(part => !string.IsNullOrEmpty(part.Png)), "Outfit snapshot incomplete");
                Assert(Part(buu, "Head").GetComponent<SpriteRenderer>().sprite == head, "Absorption replaced Majin head");
                Assert(Part(buu, "UpperBody").GetComponent<SpriteRenderer>().sprite.name.Contains("Absorbed outfit"), "Outfit was not applied");
                Pass("native strength equals Buu + highest victim power; real victim removed, clothing changes, Majin head retained");
                var combat = buu.GetComponent<CombatPowerController>();
                combat.SetOutput(1f); var ki = buu.GetComponent<KiSystem>(); ki.currentKi = ki.maxKi; combat.Refresh();
                Near(combat.PowerLevel, originalPower + firstPower, "HUD and native absorption power disagree");
                double melee = CombatDismemberment.PowerOf(buu);
                Assert(melee > originalPower, "Real melee strength did not increase");
                Pass("Power Level HUD and actual combat damage reflect the additive gain");
                combat.SetOutput(.1f); ability.ReconcilePower();
                Near(BuuAbsorption.MaximumPower(buu), originalPower + firstPower, "Suppression compounded the absorption bonus");
                combat.SetOutput(1f); ability.ReconcilePower();
                Near(BuuAbsorption.MaximumPower(buu), originalPower + firstPower, "Restoring output compounded the absorption bonus");
                Pass("suppression neither reduces the victim's maximum contribution nor compounds Buu's stored bonus");
            })) yield break;
            yield return Capture(centre, "03-goku-outfit", 2.5f);
            string firstOutfit = ability.Outfit[0].Png;
            if (!Check(() => Assert(ability.TryAbsorb(second), "Second absorption failed"))) yield break;
            yield return WaitAbsorption(ability); yield return new WaitForSeconds(.5f);
            if (finished) yield break;
            if (!Check(() =>
            {
                Near(ability.AddedPower, firstPower + secondPower, "Sequential bonuses multiplied or were lost");
                Near(BuuAbsorption.MaximumPower(buu), originalPower + firstPower + secondPower, "Sequential native power");
                Assert(ability.StrongestName == "Goku" && ability.Outfit[0].Png == firstOutfit, "Weaker victim replaced strongest outfit");
                Pass("multiple victims add their individual maximum power; weaker victims retain strongest outfit");
                Assert(ability.TryAbsorb(third), "Ordinary human target rejected");
            })) yield break;
            yield return WaitAbsorption(ability); yield return new WaitForSeconds(.5f);
            if (finished) yield break;
            if (!Check(() =>
            {
                Assert(!third && ability.AbsorbedCount == 3, "Ordinary human was not consumed exactly once");
                Near(ability.AddedPower, firstPower + secondPower + thirdPower, "Human bonus lost");
                Pass("ordinary People Playground humans can also be absorbed exactly once");
            })) yield break;

            PersonBehaviour clone = null;
            double beforeCopy = BuuAbsorption.MaximumPower(buu);
            if (!Check(() =>
            {
                foreach (var audio in buu.GetComponentsInChildren<AudioSource>(true))
                    if (audio.transform != buu.transform && !audio.GetComponent<Optout>()) audio.gameObject.AddComponent<Optout>();
                var states = ObjectStateConverter.Convert(new[] { buu.gameObject }, buu.transform.position);
                var roots = ObjectStateConverter.Convert(states, new Vector3(30f, 40f, 0f));
                Assert(roots.Length == 1 && roots[0], "Native copy failed"); clone = roots[0].GetComponent<PersonBehaviour>();
            })) yield break;
            yield return new WaitForSeconds(1.5f);
            if (!Check(() =>
            {
                var copy = clone.GetComponent<BuuAbsorption>();
                Assert(copy && copy.AbsorbedCount == 3 && copy.StrongestName == "Goku", "Native copy lost absorbed identities");
                Near(copy.AddedPower, ability.AddedPower, "Native copy lost additive power");
                Near(BuuAbsorption.MaximumPower(clone), beforeCopy, "Native copy applied absorption a second time");
                Assert(!ReferenceEquals(copy.Outfit, ability.Outfit), "Copy shares mutable outfit snapshots");
                Assert(Part(clone, "UpperBody").GetComponent<SpriteRenderer>().sprite != Part(buu, "UpperBody").GetComponent<SpriteRenderer>().sprite, "Copy shares runtime sprite ownership");
                Pass("native save/copy preserves power and clothing without double addition or shared runtime sprites");
            })) yield break;

            var regen = buu.GetComponent<BuuRegeneration>(); int recoveries = regen.RecoveryCount;
            if (!Check(() =>
            {
                var limb = Part(buu, "LowerArmFront"); limb.ImmuneToDamage = false;
                Assert(CombatDismemberment.SeverLimb(limb), "Real melee severing failed");
            })) yield break;
            float deadline = Time.realtimeSinceStartup + 25f;
            while ((regen.RecoveryCount == recoveries || regen.IsReforming) && Time.realtimeSinceStartup < deadline) yield return null;
            if (!Check(() =>
            {
                Assert(regen.RecoveryCount > recoveries, "Absorbed Buu did not regenerate a severed arm");
                Near(BuuAbsorption.MaximumPower(buu), beforeCopy, "Regeneration changed absorption bonus");
                Assert(Part(buu, "LowerArmFront").GetComponent<SpriteRenderer>().sprite.name.Contains("Absorbed outfit"), "Regrowth lost outfit");
                Pass("native severing and Majin regeneration retain absorbed strength and clothes");
            })) yield break;
            yield return Capture(centre, "04-regenerated-outfit", 2.5f);

            // Costume enforcement must not replace the runtime erosion sprite.
            LimbBehaviour erodedArm = Part(buu, "UpperArmFront");
            recoveries = regen.RecoveryCount;
            bool pixels = false; Sprite erosionSprite = null; int stableFrames = 0;
            deadline = Time.realtimeSinceStartup + 15f;
            while (!Gone(erodedArm) && Time.realtimeSinceStartup < deadline)
            {
                if (!Check(() =>
                {
                    EnergyAnnihilation.Expose(competitor, erodedArm,
                        KiCombatMath.Cap((double)EnergyAnnihilation.Resistance(erodedArm) * 100d),
                        erodedArm.transform.position, Mathf.Min(.05f, Time.deltaTime), new Color(.35f, .8f, 1f));
                })) yield break;
                yield return null;
                var erosion = erodedArm.GetComponent<EnergyErosion>();
                if (erosion && erosion.RemovedPixels > 0)
                {
                    pixels = true;
                    var sprite = erodedArm.GetComponent<SpriteRenderer>().sprite;
                    if (sprite == erosionSprite) stableFrames++; else { erosionSprite = sprite; stableFrames = 0; }
                    if (stableFrames == 4) yield return Capture(centre, "04a-outfit-eroding", 2.5f);
                }
            }
            if (!Check(() => Assert(Gone(erodedArm) && pixels && stableFrames >= 4,
                "Costume prevented stable pixel erosion and native limb erasure"))) yield break;
            deadline = Time.realtimeSinceStartup + 25f;
            while ((regen.RecoveryCount == recoveries || regen.IsReforming) && Time.realtimeSinceStartup < deadline) yield return null;
            if (!Check(() =>
            {
                Assert(regen.RecoveryCount > recoveries && !Gone(erodedArm), "Energy-erased dressed limb did not regenerate");
                Near(BuuAbsorption.MaximumPower(buu), beforeCopy, "Energy erasure/regrowth changed stored strength");
                Assert(erodedArm.GetComponent<SpriteRenderer>().sprite.name.Contains("Absorbed outfit"), "Energy regrowth lost costume");
                Pass("absorbed costume preserves progressive energy erosion, native erasure and Majin regrowth");
            })) yield break;

            // A later stronger opponent must replace the costume even after weaker captures.
            PersonBehaviour stronger = null;
            if (!Check(() =>
            {
                stronger = Spawn("[DBPU] Evil Buu", 3f);
                foreach (var limb in stronger.Limbs) { limb.InitialHealth = (float)(firstPower * 2d); limb.Health = limb.InitialHealth; }
                Assert(ability.TryAbsorb(stronger), "Stronger later victim rejected");
            })) yield break;
            yield return WaitAbsorption(ability);
            if (finished) yield break;
            if (!Check(() =>
            {
                Assert(ability.StrongestName == "Evil Buu" && ability.AbsorbedCount == 4, "Later stronger victim did not replace costume");
                Near(ability.StrongestPower, firstPower * 2d, "Strongest victim ranking wrong");
                Near(BuuAbsorption.MaximumPower(buu), beforeCopy + firstPower * 2d, "Later stronger victim was multiplied");
                Assert(ability.Outfit.Any(part => part.AccessoryName == "Cape"), "Strongest victim's cape was not captured");
                Assert(buu.GetComponentsInChildren<SpriteRenderer>().Any(view => view.name == "Absorbed garment: Cape" && view.enabled), "Strongest cape was not applied");
                Pass("later stronger victim replaces outfit and still adds power without multiplication");
            })) yield break;
            yield return Capture(centre, "05-strongest-cape", 2.5f);
            if (!Check(() =>
            {
                Destroy(clone.gameObject); Destroy(buu.gameObject); Destroy(competitor.gameObject);
            })) yield break;
            yield return null;
            if (!Check(() =>
            {
                Assert(!FindObjectsOfType<MeshFilter>().Any(filter => filter.sharedMesh && filter.sharedMesh.name == "Majin wrapping membrane"), "Absorption membrane leaked after cleanup");
                Pass("deleting fighters cleans up absorption tissue");
                Assert(errors == 0, "Absorption runtime errors detected");
            })) yield break;
            Finish(true);
        }

    }
}

