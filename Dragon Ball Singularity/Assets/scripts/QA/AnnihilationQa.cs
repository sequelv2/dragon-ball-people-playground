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
    // -dbpu-annihilation-qa <absolute output directory>.
    public sealed partial class AnnihilationQa : MonoBehaviour
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
            int index = Array.IndexOf(args, "-dbpu-annihilation-qa");
            if (index < 0 || index + 1 >= args.Length || started) return;
            started = true;
            if (!Path.IsPathRooted(args[index + 1]))
            { Debug.LogError("Annihilation QA requires an absolute output directory."); return; }
            CharacterPowerCatalog.SavingDisabled = true;
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            var go = new GameObject("DBPU opt-in annihilation QA");
            DontDestroyOnLoad(go);
            var qa = go.AddComponent<AnnihilationQa>();
            qa.output = Path.GetFullPath(args[index + 1]);
            Directory.CreateDirectory(qa.output);
            File.Delete(Path.Combine(qa.output, "qa-ready.txt"));
            File.WriteAllText(Path.Combine(qa.output, "result.txt"), "DBPU native annihilation and melee QA started\n");
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
            if (!diagnostic.Contains("EnergyAnnihilation") && !diagnostic.Contains("EnergyErosion") &&
                !diagnostic.Contains("CombatDismemberment") && !diagnostic.Contains("EnergyAnatomy") &&
                !diagnostic.Contains("EnergyVitalFailure") && !diagnostic.Contains("AnnihilationQa")) return;
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
            Log("Assertions passed: " + passes + "; annihilation/melee runtime errors: " + errors);
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
                    captureCamera = new GameObject("Annihilation QA camera").AddComponent<Camera>();
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

        IEnumerator ErasePart(LimbBehaviour limb, float ratio, Vector2 cameraCentre, bool screenshots)
        {
            bool pixelsCaptured = false, skeletonCaptured = false;
            float deadline = Time.time + 12f, wallDeadline = Time.realtimeSinceStartup + 30f;
            while (!Gone(limb) && Time.time < deadline && Time.realtimeSinceStartup < wallDeadline)
            {
                if (!Check(() => Dose(limb, ratio, Mathf.Min(.05f, Time.deltaTime)))) yield break;
                var erosion = limb.GetComponent<EnergyErosion>();
                if (screenshots && erosion && erosion.Shown >= .65f && erosion.RemovedPixels > 0 && !pixelsCaptured)
                {
                    pixelsCaptured = true;
                    yield return Capture(cameraCentre, "02-piecewise-erosion");
                    if (finished) yield break;
                }
                if (screenshots && erosion && erosion.Shown >= .5f && erosion.SkeletonVisible && !skeletonCaptured)
                {
                    skeletonCaptured = true;
                    yield return Capture(cameraCentre, "03-skeleton-exposure");
                    if (finished) yield break;
                }
                yield return null;
            }
            Check(() =>
            {
                Assert(Gone(limb), "Sustained overwhelming energy did not erase the struck limb");
                if (screenshots)
                {
                    Assert(pixelsCaptured, "Erosion never removed individual sprite pixels before erasure");
                    Assert(skeletonCaptured, "No observable skeleton phase before erasure");
                    Pass("piecewise sprite erosion, visible skeleton phase, eventual native limb disintegration");
                }
            });
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
            PersonBehaviour victim = null;
            LimbBehaviour arm = null, otherArm = null;
            Vector2 centre = Vector2.zero;
            if (!Check(() =>
            {
                if (!ModAPI.FindSpawnable("[DBPU] (BM) Goku"))
                {
                    var apply = typeof(CatalogBehaviour).GetMethod("ApplyModifications", BindingFlags.NonPublic | BindingFlags.Instance);
                    Assert(apply != null, "Native catalogue application method missing");
                    apply.Invoke(CatalogBehaviour.Main, null);
                    Log("QA ONLY: resumed native catalogue after third-party startup exception");
                }
                Global.main.SetPausedMenu(false);
                Time.timeScale = 1f;
                foreach (var canvas in FindObjectsOfType<Canvas>()) canvas.enabled = false;
                DBPU.VoiceLines = false;
                DBPU.KiDrainMultiplier = 0f;
                DBPU.DamageMultiplier = 1f;
                attacker = Spawn("[DBPU] (BM) Goku", -40f);
                victim = Spawn("[DBPU] (BM) Goku", 0f);
                arm = Part(victim, "LowerArmFront");
                otherArm = Part(victim, "LowerArm");
                centre = Part(victim, "MiddleBody").transform.position;
            })) yield break;
            yield return new WaitForSeconds(1.2f);
            if (!Check(() =>
            {
                Assert(attacker.IsAlive() && victim.IsAlive(), "Native fighters failed to initialize alive");
                Assert(EnergyAnnihilation.Resistance(arm) > 0f, "Resistance must be positive");
                Log("Energy resistance " + EnergyAnnihilation.Resistance(arm) + "; victim initial health " + arm.InitialHealth);
                Pass("real catalogue fighters initialize with finite positive energy resistance");
            })) yield break;
            yield return Capture(centre, "01-intact");
            if (finished) yield break;

            float weakEnd = Time.time + .8f;
            while (Time.time < weakEnd)
            {
                if (!Check(() => Dose(arm, .8f, Time.deltaTime))) yield break;
                yield return null;
            }
            if (!Check(() =>
            {
                Assert(Progress(arm) == 0f && !Gone(arm), "A weaker energy attack eroded a resistant enemy");
                EnergyAnnihilation.Expose(victim, arm, Power(arm, 100f), arm.transform.position, .1f, EnergyColor);
                Assert(Progress(arm) == 0f, "An attacker eroded its own limb");
                arm.ImmuneToDamage = true;
                Dose(arm, 100f, .1f);
                Assert(Progress(arm) == 0f, "Damage immunity was bypassed");
                arm.ImmuneToDamage = false;
                Pass("weak energy, self hits and explicit damage immunity cannot start erosion");
            })) yield break;

            float warmDeadline = Time.time + 8f;
            while (Progress(arm) < .2f && Time.time < warmDeadline)
            {
                if (!Check(() => Dose(arm, 4f, Mathf.Min(.05f, Time.deltaTime)))) yield break;
                yield return null;
            }
            float pausedProgress = Progress(arm), pausedShown = 0f;
            if (!Check(() =>
            {
                var erosion = arm.GetComponent<EnergyErosion>();
                Assert(erosion && pausedProgress > 0f && pausedProgress < 1f, "Sustained energy did not create a progressive exposure state");
                pausedShown = erosion.Shown;
                Time.timeScale = 0f;
                Dose(arm, 100f, .1f);
            })) yield break;
            yield return new WaitForSecondsRealtime(.25f);
            if (!Check(() =>
            {
                var erosion = arm.GetComponent<EnergyErosion>();
                Assert(Mathf.Abs(erosion.Progress - pausedProgress) < .0001f && Mathf.Abs(erosion.Shown - pausedShown) < .0001f,
                    "Paused exposure or visual erosion advanced");
                Time.timeScale = 1f;
                Pass("simulation pause freezes both exposure and visual erosion");
            })) yield break;
            yield return new WaitForSeconds(.85f);
            if (!Check(() =>
            {
                Assert(!Gone(arm) && Progress(arm) <= pausedProgress + .0001f, "Erosion continued accumulating after energy contact ended");
                Pass("breaking energy contact stops accumulated exposure; a brief hit does not erase a limb");
            })) yield break;

            yield return VerifyErodedCopy(victim, arm);
            if (finished) yield break;

            yield return ErasePart(arm, 4f, centre, true);
            if (finished) yield break;
            if (!Check(() =>
            {
                Assert(!Gone(otherArm) && otherArm.Joint && otherArm.HasJoint, "Local energy erasure destroyed an unexposed arm");
                Assert(victim.IsAlive(), "Erasing only one forearm killed the entire fighter");
                Pass("energy erases only the exposed body part while unexposed limbs and person survive");
            })) yield break;
            yield return Capture(centre, "04-partial-erasure");
            if (finished) yield break;

            yield return VerifyFullErasure();
            if (finished) yield break;
            yield return VerifyEnergyAttackHooks();
            if (finished) yield break;
            yield return VerifyAnatomicalConsequences();
            if (finished) yield break;
            yield return VerifyBuu();
            if (finished) yield break;
            yield return VerifyMelee();
            if (finished) yield break;
            if (!Check(() => Assert(errors == 0, "New runtime code logged an exception"))) yield break;
            Finish(true);
        }

        IEnumerator VerifyErodedCopy(PersonBehaviour person, LimbBehaviour sourceLimb)
        {
            float deadline = Time.time + 5f;
            while (Progress(sourceLimb) < .42f && Time.time < deadline)
            {
                if (!Check(() => Dose(sourceLimb, 4f, Mathf.Min(.05f, Time.deltaTime)))) yield break;
                yield return null;
            }
            PersonBehaviour copy = null;
            float progressBefore = Progress(sourceLimb);
            if (!Check(() =>
            {
                var source = sourceLimb.GetComponent<EnergyErosion>();
                Assert(source && source.Shown > .25f, "Native copy fixture has no visible erosion");
                // Goku's spawn recipe creates multiple identically named audio
                // children. They are transient effects, recreated on spawn;
                // excluding them mirrors Buu's native serialization handling.
                // Keep this fixture repair local to the opt-in QA instance.
                int excludedAudio = 0;
                foreach (var audio in person.GetComponentsInChildren<AudioSource>(true))
                {
                    if (!audio || audio.GetComponent<PhysicalBehaviour>() || audio.transform == person.transform) continue;
                    if (!audio.GetComponent<Optout>()) { audio.gameObject.AddComponent<Optout>(); excludedAudio++; }
                }
                Log("QA ONLY: excluded " + excludedAudio + " respawned audio children from native copy");
                var serialise = person.GetComponent<SerialiseInstructions>();
                Assert(serialise, "Native copy fixture has no serializer");
                // RelevantTransforms is cached during native Start, before these
                // QA-only exclusions. Preserve the native list/order and remove
                // only explicitly opted-out runtime effect objects.
                serialise.RelevantTransforms = serialise.RelevantTransforms
                    .Where(item => item && !item.GetComponent<Optout>()).ToArray();
                var states = ObjectStateConverter.Convert(new[] { person.gameObject }, person.transform.position);
                Assert(states.Length == 1, "Native copy failed to create one fighter state");
                var copied = ObjectStateConverter.Convert(states, new Vector3(200f, 40f, 0f));
                Assert(copied.Length == 1 && copied[0], "Native deserialization failed to restore one fighter");
                copy = copied[0].GetComponent<PersonBehaviour>();
                Assert(copy, "Native copy lost its person");
                foreach (var rb in copy.GetComponentsInChildren<Rigidbody2D>()) rb.bodyType = RigidbodyType2D.Kinematic;
            })) yield break;
            yield return null;
            yield return null;
            if (!Check(() =>
            {
                var copiedLimb = Part(copy, sourceLimb.name);
                var source = sourceLimb.GetComponent<EnergyErosion>();
                var cloned = copiedLimb.GetComponent<EnergyErosion>();
                Assert(source && cloned && cloned.Progress > .25f && cloned.Shown > .25f,
                    "Native copy discarded the saved progressive erosion state");
                Assert(cloned.RemovedPixels > 0 || cloned.SkeletonVisible, "Native copy failed to rebuild eroded sprite pixels");
                Assert(sourceLimb.GetComponent<SpriteRenderer>().sprite != copiedLimb.GetComponent<SpriteRenderer>().sprite,
                    "Native copy shares a mutable erosion sprite with the original");
                Assert(Mathf.Abs(source.Progress - progressBefore) < .03f,
                    "Native copy mutated the original fighter's exposure state");
                Pass("native save/copy rebuilds eroded pixels and independent runtime sprites without altering the source");
                Destroy(copy.gameObject);
            })) yield break;
        }

        IEnumerator VerifyFullErasure()
        {
            PersonBehaviour body = null;
            LimbBehaviour[] limbs = null;
            Vector2 centre = Vector2.zero;
            if (!Check(() => body = Spawn("[DBPU] (BM) Goku", 25f))) yield break;
            yield return new WaitForSeconds(1.1f);
            if (!Check(() => { limbs = body.Limbs.ToArray(); centre = Part(body, "MiddleBody").transform.position; })) yield break;
            float deadline = Time.time + 12f;
            bool midShot = false;
            while (limbs.Any(limb => !Gone(limb)) && Time.time < deadline)
            {
                if (!Check(() => { foreach (var limb in limbs) Dose(limb, 16f, Mathf.Min(.05f, Time.deltaTime)); })) yield break;
                if (!midShot && limbs.Any(limb => limb && limb.GetComponent<EnergyErosion>() &&
                    limb.GetComponent<EnergyErosion>().SkeletonVisible && limb.GetComponent<EnergyErosion>().Shown >= .55f))
                {
                    midShot = true;
                    yield return Capture(centre, "05a-full-body-skeleton");
                    if (finished) yield break;
                }
                yield return null;
            }
            if (!Check(() =>
            {
                Assert(limbs.All(Gone), "Whole-body exposure left physical body matter behind");
                Pass("sustained whole-body energy exposure disintegrates every native body part");
            })) yield break;
            yield return Capture(centre, "05-total-erasure");
        }

        IEnumerator VerifyBuu()
        {
            PersonBehaviour body = null;
            BuuRegeneration regen = null;
            LimbBehaviour arm = null;
            int recoveries = 0;
            if (!Check(() => body = Spawn("[DBPU] Kid Buu", 50f, true))) yield break;
            yield return new WaitForSeconds(1.1f);
            if (!Check(() =>
            {
                regen = body.GetComponent<BuuRegeneration>();
                Assert(regen, "Real Kid Buu did not initialize regeneration");
                recoveries = regen.RecoveryCount;
                arm = Part(body, "LowerArmFront");
            })) yield break;
            yield return ErasePart(arm, 8f, body.transform.position, false);
            if (finished) yield break;
            float deadline = Time.time + 14f;
            while (regen && (regen.RecoveryCount <= recoveries || regen.IsReforming) && Time.time < deadline) yield return null;
            if (!Check(() =>
            {
                Assert(regen && regen.RecoveryCount > recoveries && !regen.IsReforming && body.IsAlive(),
                    "Surviving Majin matter could not regrow an energy-erased arm");
                Assert(!Gone(arm) && arm.Joint && arm.HasJoint && !arm.IsDismembered, "Regrown Majin arm was not physically restored");
                var erosion = arm.GetComponent<EnergyErosion>();
                Assert(!erosion || (!erosion.Erased && erosion.Progress < .001f), "Regrown Majin tissue retained the old annihilation state");
                Assert(EnergyAnatomy.CanUse(arm) && arm.Health > 0f && arm.Joint.useMotor,
                    "Regrown Majin tissue retained loss of muscle function/motor control");
                Pass("surviving Majin matter regrows an erased arm and resets its erosion visuals/state");
            })) yield break;

            // Erosion can arrive while the Majin constructor owns the body's
            // renderers. The completed body must restore its native skin/material.
            var trunk = Part(body, "MiddleBody");
            var nativeSprite = trunk.GetComponent<SpriteRenderer>().sprite;
            recoveries = regen.RecoveryCount;
            if (!Check(() => Assert(CombatDismemberment.SeverLimb(Part(body, "LowerArm")),
                "Could not create active Majin reconstruction fixture"))) yield break;
            deadline = Time.time + 12f;
            while (regen.Stage != BuuRecoveryStage.Grow && Time.time < deadline) yield return null;
            if (!Check(() =>
            {
                Assert(regen.IsReforming && regen.Stage == BuuRecoveryStage.Grow, "Majin did not enter its physical construction phase");
                Dose(trunk, 8f, .1f);
            })) yield break;
            while ((regen.RecoveryCount <= recoveries || regen.IsReforming) && Time.time < deadline) yield return null;
            yield return null;
            if (!Check(() =>
            {
                var renderer = trunk.GetComponent<SpriteRenderer>();
                Assert(regen.RecoveryCount > recoveries && !regen.IsReforming && body.IsAlive(),
                    "Energy contact during active Majin construction prevented completion");
                Assert(renderer.sprite == nativeSprite && renderer.sharedMaterial && renderer.sharedMaterial.shader &&
                    renderer.sharedMaterial.shader.isSupported, "Majin construction retained a disposed erosion skin/material");
                Assert(Progress(trunk) < .001f, "Completed Majin construction retained an old exposure state");
                Pass("energy contact during Majin construction preserves restored native sprites/materials and recovery");
            })) yield break;

            var limbs = body.Limbs.ToArray();
            recoveries = regen.RecoveryCount;
            deadline = Time.time + 12f;
            while (limbs.Any(limb => !Gone(limb)) && Time.time < deadline)
            {
                if (!Check(() => { foreach (var limb in limbs) Dose(limb, 32f, Mathf.Min(.05f, Time.deltaTime)); })) yield break;
                yield return null;
            }
            yield return new WaitForSeconds(4f);
            Check(() =>
            {
                Assert(limbs.All(Gone), "Erasing all Majin matter allowed body resurrection");
                Assert(!regen || regen.RecoveryCount == recoveries, "Majin completed a recovery without surviving matter");
                Pass("erasing every Majin body part prevents regeneration without disabling partial recovery");
            });
        }

        IEnumerator VerifyMelee()
        {
            PersonBehaviour victim = null;
            LimbBehaviour target = null, spare = null;
            if (!Check(() => victim = Spawn("[DBPU] (BM) Goku", 85f))) yield break;
            yield return new WaitForSeconds(1.1f);
            if (!Check(() =>
            {
                target = Part(victim, "LowerArmFront");
                spare = Part(victim, "LowerArm");
                var multipliers = attacker.GetComponent<MultipliersAndStuffs>();
                multipliers.BoostMultiplier = 1f;
                multipliers.Recalculate();
                Assert(!CombatDismemberment.TryMeleeSever(attacker, target, target.InitialHealth * 2f, 15f, target.transform.position, Vector2.right),
                    "An equal-power attacker severed a protected limb");
                multipliers.BoostMultiplier = 100f;
                multipliers.Recalculate();
                Assert(!CombatDismemberment.TryMeleeSever(attacker, target, target.InitialHealth * 2f, 0f, target.transform.position, Vector2.right),
                    "Resting contact severed a protected limb");
                Assert(!CombatDismemberment.TryMeleeSever(attacker, Part(attacker, "LowerArmFront"), target.InitialHealth * 2f, 15f, target.transform.position, Vector2.right),
                    "Self collision severed an own limb");
                Pass("equal-power, stationary and own-body hits cannot sever protected limbs");
                Assert(CombatDismemberment.TryMeleeSever(attacker, target, target.InitialHealth * 2f, 15f, target.transform.position, Vector2.right),
                    "An overwhelming high-speed strike failed to sever a limb");
                Assert(target.IsDismembered && (!target.HasJoint || !target.Joint), "Melee reported severing without detaching the native joint");
                Assert(spare.Joint && spare.HasJoint && !spare.IsDismembered, "Melee severing detached an unrelated limb");
                Assert(DbpuLimbDurability.ShouldBlockDismemberment(spare), "Scoped melee severing permanently disabled normal durability protection");
                Pass("overwhelming melee strike detaches the actual hit joint and preserves other limbs/durability");
            })) yield break;
            yield return Capture(Part(victim, "MiddleBody").transform.position, "06-melee-severing");
            if (finished) yield break;
            yield return VerifyPhysicalKiFist();
            if (finished) yield break;
            yield return VerifyOrdinaryHand();
        }

        IEnumerator VerifyPhysicalKiFist()
        {
            PersonBehaviour fighter = null, victim = null;
            LimbBehaviour target = null, handLimb = null;
            KiFistHand fist = null;
            KiSystem ki = null;
            Rigidbody2D movingHand = null;
            float energyBefore = 0f;
            if (!Check(() =>
            {
                fighter = Spawn("[DBPU] (BM) Goku", 115f);
                victim = Spawn("[DBPU] (BM) Goku", 120f);
            })) yield break;
            yield return new WaitForSeconds(1.1f);
            if (!Check(() =>
            {
                var mult = fighter.GetComponent<MultipliersAndStuffs>();
                mult.BoostMultiplier = 100f;
                mult.Recalculate();
                handLimb = Part(fighter, "LowerArmFront");
                var loadout = KiHandLoadout.GetOrCreate(handLimb);
                Assert(loadout.Equip(KiHandLoadout.FistId), "Could not equip the real Ki Fist");
                fist = KiFistHand.GetOrCreate(handLimb);
                ki = fighter.GetComponent<KiSystem>();
                ki.maxKi = ki.currentKi = 1000f;
                ki.baseRegen = ki.currentKiDrainRate = 0f;
                ki.isExhausted = false;
                energyBefore = ki.currentKi;
                target = Part(victim, "LowerArmFront");
                Assert(fist.IsReady, "Native hand is not ready for a real physical strike");
                foreach (var joint in handLimb.GetComponents<Joint2D>()) joint.enabled = false;
                foreach (var collider in victim.GetComponentsInChildren<Collider2D>())
                    if (collider.GetComponent<LimbBehaviour>() != target) collider.enabled = false;
                var handCollider = handLimb.GetComponent<Collider2D>();
                Assert(handCollider, "Native hand collider missing");
                foreach (var collider in fighter.GetComponentsInChildren<Collider2D>())
                    if (collider != handCollider) Physics2D.IgnoreCollision(handCollider, collider, true);
                movingHand = handLimb.GetComponent<Rigidbody2D>();
                movingHand.position = (Vector2)target.transform.position + Vector2.left;
                movingHand.rotation = 90f;
                movingHand.gravityScale = 0f;
                movingHand.bodyType = RigidbodyType2D.Dynamic;
                movingHand.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                movingHand.velocity = Vector2.right * 15f;
                Physics2D.SyncTransforms();
            })) yield break;
            yield return new WaitForSeconds(.18f);
            if (!Check(() =>
            {
                Log("PHYSICAL KI FIST target=" + target.Health + "/" + target.InitialHealth +
                    " detached=" + target.IsDismembered + " hasJoint=" + target.HasJoint +
                    " ki=" + ki.currentKi + "/" + energyBefore + " ready=" + fist.IsReady +
                    " handVelocity=" + movingHand.velocity + " powerRatio=" +
                    (CombatDismemberment.PowerOf(fighter) / Math.Max(1d, CombatDismemberment.PowerOf(victim))));
                Assert(target.IsDismembered && (!target.Joint || !target.HasJoint), "Real moving Ki Fist contact did not trigger strength-based severing");
                Assert(ki.currentKi < energyBefore, "Real Ki Fist severing did not consume energy");
                movingHand.velocity = Vector2.zero;
                movingHand.bodyType = RigidbodyType2D.Kinematic;
                fist.Equipped = false;
                Pass("real dynamic hand collision triggers Ki Fist severing and pays its native Ki cost");
            })) yield break;
            yield return Capture(Part(victim, "MiddleBody").transform.position, "07-native-ki-fist-contact");
        }

        IEnumerator VerifyOrdinaryHand()
        {
            PersonBehaviour fighter = null, victim = null;
            LimbBehaviour target = null, hand = null;
            Rigidbody2D movingHand = null;
            KiSystem ki = null;
            float energy = 0f;
            if (!Check(() =>
            {
                fighter = Spawn("[DBPU] (BM) Goku", 145f);
                victim = Spawn("[DBPU] (BM) Goku", 150f);
            })) yield break;
            yield return new WaitForSeconds(1.1f);
            if (!Check(() =>
            {
                var mult = fighter.GetComponent<MultipliersAndStuffs>();
                mult.BoostMultiplier = 100f;
                mult.Recalculate();
                foreach (var person in new[] { fighter, victim })
                {
                    foreach (var punch in person.GetComponentsInChildren<PunchPower>()) { punch.Active = false; punch.enabled = false; }
                    foreach (var kick in person.GetComponentsInChildren<KickPower>()) { kick.Active = false; kick.enabled = false; }
                    foreach (var fist in person.GetComponentsInChildren<KiFistHand>()) { fist.Equipped = false; fist.enabled = false; }
                }
                ki = fighter.GetComponent<KiSystem>();
                ki.maxKi = ki.currentKi = 1000f;
                ki.baseRegen = ki.currentKiDrainRate = 0f;
                ki.isExhausted = false;
                energy = ki.currentKi;
                target = Part(victim, "LowerArmFront");
                hand = Part(fighter, "LowerArmFront");
                foreach (var joint in hand.GetComponents<Joint2D>()) joint.enabled = false;
                foreach (var collider in victim.GetComponentsInChildren<Collider2D>())
                    if (collider.GetComponent<LimbBehaviour>() != target) collider.enabled = false;
                var handCollider = hand.GetComponent<Collider2D>();
                Assert(handCollider, "Ordinary hand collider missing");
                foreach (var collider in fighter.GetComponentsInChildren<Collider2D>())
                    if (collider != handCollider) Physics2D.IgnoreCollision(handCollider, collider, true);
                movingHand = hand.GetComponent<Rigidbody2D>();
                movingHand.position = (Vector2)target.transform.position + Vector2.left;
                movingHand.rotation = 90f;
                movingHand.gravityScale = 0f;
                movingHand.bodyType = RigidbodyType2D.Dynamic;
                movingHand.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                movingHand.velocity = Vector2.right * 15f;
                Physics2D.SyncTransforms();
            })) yield break;
            yield return new WaitForSeconds(.18f);
            if (!Check(() =>
            {
                Log("PHYSICAL ORDINARY HAND detached=" + target.IsDismembered + " hasJoint=" + target.HasJoint +
                    " energy=" + ki.currentKi + "/" + energy + " velocity=" + movingHand.velocity);
                Assert(target.IsDismembered && (!target.Joint || !target.HasJoint),
                    "A real unpowered high-strength hand collision did not sever a limb");
                Assert(Mathf.Abs(ki.currentKi - energy) < .01f, "An ordinary physical strike consumed Ki");
                movingHand.velocity = Vector2.zero;
                movingHand.bodyType = RigidbodyType2D.Kinematic;
                Pass("real ordinary hand collision severs by physical strength with all attack powers disabled and no Ki cost");
            })) yield break;
            yield return Capture(Part(victim, "MiddleBody").transform.position, "08-native-ordinary-hand-contact");
        }
    }
}
