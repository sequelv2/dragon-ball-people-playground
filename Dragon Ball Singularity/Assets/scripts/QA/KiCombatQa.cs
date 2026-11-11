using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DBPU
{
    // Runs only in an explicitly requested, disposable native game process.
    // It never launches itself, saves mod settings, or touches a user's scene.
    public sealed partial class KiCombatQa : MonoBehaviour
    {
        static bool started;
        string output;
        bool finished;
        int sceneEpoch, blockerBaseline;
        CombatAbilityWheel wheel;
        GameObject subject, donor, opponent;
        PersonBehaviour person, donorPerson, opponentPerson;
        KiSystem ki, donorKi, opponentKi;
        KiHandLoadout hand, otherHand, donorHand;
        AllyKiReservoir support, donorSupport, opponentSupport;
        float basicDamage, originalFormPower;
        LimbBehaviour damageTarget;

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
            int index = Array.IndexOf(args, "-dbpu-ki-combat-qa");
            if (index < 0 || index + 1 >= args.Length || started) return;
            started = true;
            if (!Path.IsPathRooted(args[index + 1]))
            {
                Debug.LogError("Ki combat QA requires an absolute output folder.");
                return;
            }
            Application.runInBackground = true;
            var go = new GameObject("DBPU opt-in ki combat QA");
            DontDestroyOnLoad(go);
            var qa = go.AddComponent<KiCombatQa>();
            qa.output = Path.GetFullPath(args[index + 1]);
            Directory.CreateDirectory(qa.output);
            File.Delete(Path.Combine(qa.output, "qa-ready.txt"));
            File.WriteAllText(Path.Combine(qa.output, "result.txt"), "DBPU native ki combat QA started\n");
            qa.StartCoroutine(qa.Run());
        }

        void Log(string message) { File.AppendAllText(Path.Combine(output, "result.txt"), message + "\n"); }
        static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        void Near(float expected, float actual, float tolerance, string message)
        {
            Assert(Finite(expected) && Finite(actual) && Math.Abs(actual - expected) <= tolerance,
                message + ": expected " + expected + ", actual " + actual);
        }
        bool Check(Action action)
        {
            try { action(); return true; }
            catch (Exception ex) { Log("FAIL " + ex); Finish(false); return false; }
        }
        void Finish(bool success)
        {
            if (finished) return;
            finished = true;
            try { if (wheel) wheel.Close(false); } catch (Exception ex) { Log("Cleanup: " + ex); }
            File.WriteAllText(Path.Combine(output, "qa-ready.txt"), success ? "PASS\n" : "FAIL\n");
            Application.Quit(success ? 0 : 2);
        }

        GameObject Spawn(string name, Vector3 position)
        {
            var asset = ModAPI.FindSpawnable(name);
            var human = ModAPI.FindSpawnable("Human");
            Assert(asset && human && human.Prefab, "Missing native catalogue entry " + name);
            var root = Instantiate(human.Prefab, position, Quaternion.identity);
            // Mirror CatalogBehaviour.Spawn's native registration before firing ItemSpawned.
            // Other installed mods inspect OriginalSpawnableAsset in that event.
            root.AddComponent<DeregisterBehaviour>();
            root.AddComponent<TexturePackApplier>();
            root.AddComponent<AudioSourceTimeScaleBehaviour>();
            root.AddComponent<SerialiseInstructions>().OriginalSpawnableAsset = asset;
            root.name = asset.name;
            CatalogBehaviour.SpawnedGameObjects.Add(root);
            CatalogBehaviour.PerformMod(asset, root);
            Assert(root, "Native modification removed QA fighter " + name);
            DontDestroyOnLoad(root);
            foreach (var body in root.GetComponentsInChildren<Rigidbody2D>()) body.bodyType = RigidbodyType2D.Kinematic;
            return root;
        }

        void Prepare(GameObject root, out PersonBehaviour fighter, out KiSystem energy)
        {
            Assert(root, "QA fighter was destroyed during startup");
            fighter = root.GetComponent<PersonBehaviour>();
            energy = root.GetComponent<KiSystem>();
            Assert(fighter && fighter.IsAlive() && energy, "QA fighter did not initialize alive with native KiSystem");
            energy.maxKi = 1000f;
            energy.currentKi = 1000f;
            energy.baseRegen = 0f;
            energy.currentKiDrainRate = 0f;
            energy.isExhausted = false;
            energy.exhaustionTimer = 0f;
        }

        KiHandLoadout GetHand(PersonBehaviour fighter, int ordinal)
        {
            var hands = fighter.Limbs.Where(KiHandLoadout.IsHand).ToArray();
            Assert(hands.Length == 2, "Native fighter must expose both lower arms");
            var result = KiHandLoadout.GetOrCreate(hands[ordinal]);
            Assert(result && result.Limb == hands[ordinal] && result.Person == fighter, "Hand loadout references are invalid");
            return result;
        }

        void Open(KiHandLoadout target)
        {
            Assert(wheel.OpenFor(target, new Vector2(Screen.width * .5f, Screen.height * .5f), KeyCode.None),
                "Ability wheel did not open; pausedMenu="+Global.main.GetPausedMenu()+" ui="+Global.main.ActiveUiBlockers+" dialogs="+DialogBox.IsAnyDialogboxOpen+" alive="+target.Person.IsAlive()+" attached="+target.Limb.transform.IsChildOf(target.Person.transform));
            Assert(wheel.IsOpen && wheel.Target == target, "Ability wheel did not lock its intended hand");
            Assert(Global.main.ActiveUiBlockers == blockerBaseline + 1, "Ability wheel must own exactly one UI blocker");
        }
        void Select(KiHandLoadout target, string id)
        {
            int index = target.GetEntries().FindIndex(entry => entry.Id == id);
            Assert(index >= 0, "Ability absent from selected hand: " + id);
            int guard = 0;
            while (wheel.Selection.Page != index / CombatWheelSelection.PageSize && guard++ <= wheel.Selection.Pages) wheel.ChangePage(1);
            wheel.ChooseSlot(index % CombatWheelSelection.PageSize);
            Assert(wheel.Selection.Armed && wheel.Selection.Selected == index, "Could not select ability " + id);
        }
        void Released(string reason)
        {
            Assert(!wheel.IsOpen && !wheel.Target, reason + ": wheel retained target");
            Assert(!CombatAbilityWheel.InputCaptured, reason + ": wheel retained input capture");
            Assert(Global.main.ActiveUiBlockers == blockerBaseline, reason + ": wheel leaked UI blocker");
        }

        void Capture(string file)
        {
            var old = RenderTexture.active;
            Texture2D screenshot = null;
            try
            {
                RenderTexture.active = null;
                screenshot = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
                screenshot.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
                screenshot.Apply();
                File.WriteAllBytes(Path.Combine(output, file), screenshot.EncodeToPNG());
            }
            finally { RenderTexture.active = old; if (screenshot) Destroy(screenshot); }
            Log("SCREENSHOT " + file + " " + Screen.width + "x" + Screen.height);
        }
        void SnapshotUi(string file)
        {
            var canvas = GameObject.Find("DBPU Combat Ability Canvas");
            Assert(canvas, "Native ability wheel canvas missing");
            Canvas.ForceUpdateCanvases();
            var lines = new List<string>();
            foreach (var label in canvas.GetComponentsInChildren<TextMeshProUGUI>())
            {
                var corners = new Vector3[4];
                label.rectTransform.GetWorldCorners(corners);
                float left = corners.Min(p => p.x), right = corners.Max(p => p.x);
                float bottom = corners.Min(p => p.y), top = corners.Max(p => p.y);
                Assert(left >= -1 && right <= Screen.width + 1 && bottom >= -1 && top <= Screen.height + 1,
                    "Ability text outside viewport: " + label.name + " " + left + "," + bottom + " -> " + right + "," + top);
                Assert(!label.isTextOverflowing, "Ability label truncated or overflowing: " + label.name + " / " + label.text);
                lines.Add(label.name + " | " + label.text + " | rect=" + left + "," + bottom + "," + right + "," + top + " | overflow=" + label.isTextOverflowing);
            }
            Assert(lines.Count > 10, "Incomplete native ability text hierarchy");
            File.WriteAllLines(Path.Combine(output, file), lines.ToArray());
        }

        void VerifyLoadout()
        {
            var entries = hand.GetEntries();
            Assert(entries.Count >= 5 && entries.Select(e => e.Id).Distinct().Count() == entries.Count, "Native hand entries are missing or duplicated");
            Assert(entries.Any(e => e.Id == KiHandLoadout.FistId) && entries.Any(e => e.Id == KiHandLoadout.ShareId), "Stock fist/share are not available without allied energy");
            Assert(!KiHandLoadout.GetOrCreate(person.Limbs.First(l => l.name == "Head")), "Ability loadout accepted a head as a hand");
            Assert(hand.Equip(KiHandLoadout.FistId), "Stock ki fist equip failed");
            Assert(KiFistHand.GetOrCreate(hand.Limb).Equipped, "Equipping fist did not enable native hand component");
            Assert(!KiHandLoadout.LegacyAllowed(hand.Limb, "KiBlast") && !KiHandLoadout.LegacyAllowed(otherHand.Limb, "KiBlast"), "Custom hand did not suppress shared native attack controllers");
            Assert(otherHand.Equip(KiHandLoadout.ShareId), "Other hand could not equip independent share ability");
            Assert(hand.SelectedId == KiHandLoadout.FistId && otherHand.SelectedId == KiHandLoadout.ShareId, "Custom choice escaped its targeted hand");
            Assert(hand.Equip("KiBlast"), "Native KiBlast selection failed");
            Assert(hand.SelectedId == "KiBlast" && otherHand.SelectedId == "KiBlast", "Shared native selection did not synchronize both hands");
            Assert(!KiFistHand.GetOrCreate(hand.Limb).Equipped && !KiShareHand.GetOrCreate(otherHand.Limb).Equipped, "Native selection left custom abilities equipped");
            var power = person.GetComponent<CharacterPowers>().PowerData["KiBlast"];
            Assert((power.IsActive != null ? power.IsActive() : power.Active) && hand.Equip("KiBlast") && (power.IsActive != null ? power.IsActive() : power.Active), "Reselecting an active native ability toggled it off");
            string before = hand.SelectedId;
            Assert(!hand.Equip("QA_UNKNOWN_ABILITY") && hand.SelectedId == before, "Unknown ability changed native loadout");
            VerifyFootStrikeLoadout();
            Assert(hand.Equip(KiHandLoadout.NoneId) && !KiHandLoadout.LegacyAllowed(hand.Limb, "KiBlast"), "Empty hand did not disable shared offensive activation");
            Assert(donorHand.SelectedId != KiHandLoadout.FistId, "Loadout changes affected another fighter");
            Log("PASS stock abilities, hand-only target validation, independent custom hands, shared native selection, repeated native selection, unknown ID rejection and empty hand suppression");
        }

        void VerifyFootStrikeLoadout()
        {
            var footLimb = person.Limbs.First(l => l.name == "FootFront");
            var backLimb = person.Limbs.First(l => l.name == "Foot");
            var foot = KiHandLoadout.GetOrCreate(footLimb);
            var backFoot = KiHandLoadout.GetOrCreate(backLimb);
            Assert(foot && backFoot && foot.SelectedId == KiHandLoadout.NoneId && backFoot.SelectedId == KiHandLoadout.NoneId,
                "Feet inherited a hand technique without explicit selection");
            Assert(person.Limbs.Where(KiHandLoadout.IsHand).Count() == 2, "Foot support changed the definition of the two hands");
            Assert(CombatAbilityWheel.ResolveAbilityLimb(person.Limbs.First(l => l.name == "UpperLegFront")) == footLimb
                && CombatAbilityWheel.ResolveAbilityLimb(person.Limbs.First(l => l.name == "LowerLegFront")) == footLimb
                && CombatAbilityWheel.ResolveAbilityLimb(person.Limbs.First(l => l.name == "UpperLeg")) == backLimb,
                "Leg hover did not resolve the foot on the same side");
            var entries = foot.GetEntries();
            Assert(entries.Count == 2 && entries.Any(e => e.Id == KiHandLoadout.FistId)
                && entries.All(e => e.Id != KiHandLoadout.InfusionId) && entries.Any(e => e.Id == KiHandLoadout.NoneId),
                "Foot must expose the unified Ki Fist and empty-foot choices");
            Assert(!foot.Equip(KiHandLoadout.ShareId) && !foot.Equip("SpiritBomb") && !foot.Equip("KiBlast")
                && foot.SelectedId == KiHandLoadout.NoneId, "Foot accepted a share, sphere or native beam technique");
            Assert(!KiShareHand.GetOrCreate(footLimb) && !KiCombatMath.ValidHand(footLimb, person)
                && !KiHandLoadout.LegacyAllowed(footLimb,"KiBlast"), "Foot passed a hand-only activation gate");
            Assert(!SpiritBombChannel.GetOrCreate(person).BeginCharge(footLimb), "Foot began a Spirit Bomb charge");
            var alias = person.Limbs.First(l => l.name == "LowerLegFront").GetComponent<KiStrikeLimbAlias>();
            Assert(alias && alias.StrikeLimb == footLimb, "Shin Use is not routed to its own foot");
            alias.Activate();
            Assert(!footLimb.GetComponent<KiFistHand>() || !footLimb.GetComponent<KiFistHand>().Equipped,
                "Use on an unselected leg automatically equipped a ki strike");
            Assert(foot.Equip(KiHandLoadout.FistId), "Explicit ki kick selection failed");
            var strike = KiFistHand.GetOrCreate(footLimb);
            Assert(strike && strike.Equipped && strike.Limb == footLimb && strike.Person == person
                && KiCombatMath.ValidStrikeLimb(footLimb,person) && !KiCombatMath.ValidStrikeLimb(footLimb,opponentPerson),
                "Foot strike has the wrong activation limb or owner");
            Assert(hand.SelectedId == "KiBlast" && otherHand.SelectedId == "KiBlast" && KiHandLoadout.LegacyAllowed(hand.Limb,"KiBlast"),
                "Choosing a kick disabled the selected native technique in the hands");
            alias.Activate();Assert(strike.IsCharging,"Use on the selected shin did not charge its own foot");strike.Cancel();
            float energyBefore = ki.currentKi;
            Charge(strike,30);
            Assert(ki.currentKi < energyBefore && !strike.IsOvercharged, "Stock foot charge did not use its owner's ordinary ki");
            KiHandLoadout.NotifyPowerApplied(person.GetComponent<CharacterPowers>(),"KiBlast");
            Assert(strike.Equipped && strike.IsCharged && foot.SelectedId == KiHandLoadout.FistId
                && hand.SelectedId == "KiBlast" && backFoot.SelectedId == KiHandLoadout.NoneId,
                "Native hand synchronization cancelled or automatically assigned a foot strike");
            var victim = opponentPerson.Limbs.First(l => l.name.Contains("LowerArm"));
            float victimHealth = victim.Health;
            Assert(!strike.DischargeForTest(person.Limbs.First(l=>l.name=="Head"),footLimb.transform.position), "Foot hit its own owner");
            Assert(strike.DischargeForTest(victim,victim.transform.position) && victim.Health < victimHealth,
                "Armed foot did not discharge into an enemy");
            Assert(!strike.DischargeForTest(victim,victim.transform.position), "Foot reused a spent charge");
            victim.Health = victimHealth;
            var received = AllyKiReservoir.GetOrCreate(person);
            Assert(received.TotalReceived <= .001f,"Stock foot QA unexpectedly inherited donated energy");
            received.ReturnSupport(140);ki.currentKi=1000;
            Assert(foot.Equip(KiHandLoadout.InfusionId), "Full-energy kick selection failed");
            Charge(strike,30);
            Near(140,strike.InfusedEnergy,.01f,"All-energy kick did not move the received energy into its foot");
            Near(0,received.TotalReceived,.01f,"Foot infusion duplicated donated energy");
            strike.Cancel();Near(140,received.TotalReceived,.01f,"Cancelling a foot infusion did not refund unused energy");
            received.ConsumeSupport(received.TotalReceived);ki.currentKi=1000;
            Assert(foot.Equip(KiHandLoadout.NoneId) && !strike.Equipped && hand.SelectedId == "KiBlast",
                "Clearing a foot changed the hand technique");
            Open(foot);Assert(!wheel.Selection.Armed && wheel.Target.Limb == footLimb,"Foot wheel selected another limb or armed itself");
            Select(foot,KiHandLoadout.FistId);wheel.Close(false);
            Assert(foot.SelectedId == KiHandLoadout.NoneId && backFoot.SelectedId == KiHandLoadout.NoneId,
                "Cancelled foot preview equipped a strike automatically");
            Log("PASS explicit foot-only ki kick/infusion, same-side leg aliases, default empty feet, real charge/hit/refund and native hand/share/sphere isolation");
        }

        IEnumerator Run()
        {
            for (int i = 0; i < 120; i++) yield return null;
            AsyncOperation loading = null;
            if (!Check(() =>
            {
                var cliArgs = Environment.GetCommandLineArgs();
                int mapFlag = Array.IndexOf(cliArgs, "-dbpu-qa-map");
                string wantedMap = mapFlag >= 0 && mapFlag + 1 < cliArgs.Length ? cliArgs[mapFlag + 1] : null;
                Log("Maps: " + string.Join(", ", MapRegistry.GetAllMaps().Where(map => map.Prefab).Select(map => map.name)));
                MapLoaderBehaviour.CurrentMap = MapRegistry.GetAllMaps().Where(map => map.Prefab).FirstOrDefault(map => wantedMap != null && map.name.IndexOf(wantedMap, StringComparison.OrdinalIgnoreCase) >= 0) ?? MapRegistry.GetAllMaps().First(map => map.Prefab);
                Log("Using map: " + MapLoaderBehaviour.CurrentMap.name);
                loading = SceneManager.LoadSceneAsync("Main");
            })) yield break;
            yield return loading;
            yield return new WaitForSecondsRealtime(3f);
            int stableEpoch = sceneEpoch, stableFrames = 0, totalFrames = 0;
            while (stableFrames < 120 && totalFrames++ < 3600)
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
                Assert(stableFrames >= 120, "Native main scene never stabilized");
                if (!ModAPI.FindSpawnable("[DBPU] Gogito"))
                {
                    var apply = typeof(CatalogBehaviour).GetMethod("ApplyModifications", BindingFlags.NonPublic | BindingFlags.Instance);
                    Assert(apply != null, "Native catalogue application method missing");
                    apply.Invoke(CatalogBehaviour.Main, null);
                    Log("QA ONLY: resumed native catalogue application after third-party startup exception; no mod settings changed");
                }
            })) yield break;
            if (!Check(() =>
            {
                donor = Spawn("[DBPU] Gogito", new Vector3(-3.5f, 30, 0));
                subject = Spawn("[DBPU] Gogito", new Vector3(0, 30, 0));
                opponent = Spawn("[DBPU] Gogito", new Vector3(5, 30, 0));
            })) yield break;
            yield return new WaitForSecondsRealtime(1.6f);
            if (!Check(() =>
            {
                Prepare(subject, out person, out ki);
                Prepare(donor, out donorPerson, out donorKi);
                Prepare(opponent, out opponentPerson, out opponentKi);
                hand = GetHand(person, 0); otherHand = GetHand(person, 1); donorHand = GetHand(donorPerson, 0);
                wheel = CombatAbilityWheel.Instance;
                Assert(wheel, "Ability wheel singleton missing");
                blockerBaseline = Global.main.ActiveUiBlockers;
                Assert(blockerBaseline == 0, "Disposable native scene has an unrelated UI blocker");
                Global.main.CameraControlBehaviour.enabled = false;
                Global.main.camera.transform.position = new Vector3(.6f, 30.8f, -10);
                Global.main.camera.orthographicSize = 4f;
                Global.main.camera.backgroundColor=new Color(.13f,.15f,.18f);
                Global.main.camera.clearFlags=CameraClearFlags.SolidColor;
                // A void map has no environmental lights; use the original sprites with
                // an unlit material in this disposable scene so the combat capture is legible.
                foreach(var actor in new[]{subject,donor,opponent})
                    foreach(var renderer in actor.GetComponentsInChildren<SpriteRenderer>())
                        renderer.sharedMaterial=new Material(Shader.Find("Sprites/Default"));
                DBPU.KiAttackCostMultiplier = 1f;
                DBPU.DamageMultiplier = 1f;
                Log("QA ONLY: fixed damage/cost multipliers in this disposable process; no settings are saved");
                Global.main.SetPausedMenu(false);
                foreach(var dialog in FindObjectsOfType<DialogBox>())dialog.Close();
                // Focused suites can skip the loadout checks to reach their own subject.
                if (Array.IndexOf(Environment.GetCommandLineArgs(), "-dbpu-qa-skip-loadout") < 0) VerifyLoadout();
            })) yield break;
            yield return null; yield return null;
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-dbpu-beam-qa") >= 0)
            {
                yield return VerifyBeamPhysics();
                if(!finished)Check(()=>{Log("ALL BEAM PHYSICS CHECKS PASSED");Finish(true);});
                yield break;
            }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-dbpu-reality-rift-qa") >= 0)
            {
                yield return VerifyRealityRift();
                if(!finished)Check(()=>Finish(true));
                yield break;
            }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-dbpu-ki-fist-visual-qa") >= 0
                && Array.IndexOf(Environment.GetCommandLineArgs(), "-dbpu-ki-fist-modes-qa") < 0)
            {
                yield return VerifyKiFistVisuals();
                if(!finished)Check(()=>Finish(true));
                yield break;
            }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-dbpu-ki-fist-modes-qa") >= 0)
            {
                yield return VerifyKiFistModes();
                if(!finished)yield return VerifyKiShield();
                if(!finished)yield return VerifyReferenceHud();
                if(!finished && Array.IndexOf(Environment.GetCommandLineArgs(), "-dbpu-ki-fist-visual-qa") >= 0)
                    yield return VerifyKiFistVisuals();
                if(!finished)Check(()=>{Log("ALL KI FIST, SHIELD AND HUD CHECKS PASSED");Finish(true);});
                yield break;
            }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-dbpu-ki-shield-qa") >= 0)
            {
                yield return VerifyKiShield();
                if(!finished)yield return VerifyReferenceHud();
                if(!finished)Check(()=>{Log("ALL KI SHIELD AND HUD CHECKS PASSED");Finish(true);});
                yield break;
            }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-dbpu-reference-hud-only") >= 0)
            {
                yield return VerifyReferenceHud();
                if (!finished) Check(() => { Log("ALL REFERENCE HUD CHECKS PASSED"); Finish(true); });
                yield break;
            }
            if (!Check(() =>
            {
                Open(hand);
                Assert(!wheel.Selection.Armed, "Opening wheel armed an ability automatically");
                Select(hand, KiHandLoadout.FistId);
            })) yield break;
            yield return new WaitForSecondsRealtime(.2f);
            yield return new WaitForEndOfFrame();
            if (!Check(() => { SnapshotUi("01-fist-preview-ui.txt"); Capture("01-fist-preview.png"); wheel.Close(false); })) yield break;
            yield return null; yield return null;
            if (!Check(() =>
            {
                Released("Explicit cancel");
                Assert(hand.SelectedId == KiHandLoadout.NoneId && otherHand.SelectedId == KiHandLoadout.NoneId, "Cancelled preview changed selected technique");
                Near(1000, ki.currentKi, .01f, "Preview/cancel consumed ki");
                Open(hand); Select(hand, KiHandLoadout.FistId); wheel.Close(true);
            })) yield break;
            yield return null; yield return null;
            if (!Check(() =>
            {
                Released("Fist commit");
                Assert(hand.SelectedId == KiHandLoadout.FistId && otherHand.SelectedId == KiHandLoadout.NoneId, "Wheel fist commit changed the wrong hand");
                Near(1000, ki.currentKi, .01f, "Choosing technique charged/fired it");
                Open(otherHand); Select(otherHand, KiHandLoadout.ShareId);
            })) yield break;
            yield return new WaitForSecondsRealtime(.2f);
            yield return new WaitForEndOfFrame();
            if (!Check(() => { SnapshotUi("02-other-hand-share-ui.txt"); Capture("02-other-hand-share.png"); wheel.Close(true); })) yield break;
            yield return null; yield return null;
            if (!Check(() =>
            {
                Released("Independent share commit");
                Assert(hand.SelectedId == KiHandLoadout.FistId && otherHand.SelectedId == KiHandLoadout.ShareId, "Second hand menu disturbed first hand");
                Open(hand); wheel.Navigate(1); wheel.PointAt(Vector2.zero);
                Assert(!wheel.Selection.Armed && wheel.Selection.Selected < 0, "Center did not cancel pending ability");
                wheel.Close(true);
            })) yield break;
            yield return null; yield return null;
            if (!Check(() =>
            {
                Released("Center cancellation");
                Assert(hand.SelectedId == KiHandLoadout.FistId, "Center cancellation equipped another technique");
                Log("PASS preview/cancel, independent target hand commits, no activation/cost from selection, deadzone cancellation and UI blocker/input release");
                VerifyBasicFistAndTransferRules();
            })) yield break;
            yield return AccumulateRealDonations();
            if (finished) yield break;
            if (!Check(VerifyOverchargeAndExpiry)) yield break;
            yield return VerifyNativeOverdriveVisuals();
            if(finished)yield break;
            yield return VerifyFootVfxRendering();
            if(finished)yield break;
            yield return new WaitForEndOfFrame();
            if (!Check(() => Capture("03-native-ki-combat.png"))) yield break;
            if (!Check(() => Screen.SetResolution(1280, 720, false))) yield break;
            yield return new WaitForSecondsRealtime(.35f);
            if (!Check(() => { Open(hand); Select(hand, KiHandLoadout.FistId); })) yield break;
            yield return new WaitForSecondsRealtime(.1f);
            yield return new WaitForEndOfFrame();
            if (!Check(() =>
            {
                SnapshotUi("04-1280x720-ui.txt"); Capture("04-1280x720-wheel.png"); wheel.Close(false);
            })) yield break;
            yield return null; yield return null;
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-dbpu-spirit-bomb-qa") >= 0)
            {
                yield return VerifySpiritBomb();
                if (finished) yield break;
            }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-dbpu-reference-hud-qa") >= 0)
            {
                yield return VerifyReferenceHud();
                if (finished) yield break;
            }
            if (!Check(() => { Log("ALL CHECKS PASSED"); Finish(true); })) yield break;
        }

        void Charge(KiFistHand fist, int steps)
        {
            Assert(fist.BeginCharge(), "Equipped live hand could not begin a ki charge");
            for (int i = 0; i < steps; i++) fist.ChargeStep(.1f);
            fist.Arm();
            Assert(fist.IsCharged, "Ki fist was not armed after charging");
        }
        IEnumerator VerifyFootVfxRendering()
        {
            var foot=person.Limbs.First(KiHandLoadout.IsFoot);
            var loadout=KiHandLoadout.GetOrCreate(foot);var rb=foot.GetComponent<Rigidbody2D>();
            string previous=loadout.SelectedId;float previousKi=ki.currentKi;
            Vector2 previousPosition=rb.position;Vector3 previousCamera=Global.main.camera.transform.position;
            float previousSize=Global.main.camera.orthographicSize;
            try
            {
                if(!Check(()=>
                {
                    Assert(KiCombatVfxAssets.Ready,"Unity combat VFX package is missing");
                    Assert(loadout.Equip(KiHandLoadout.FistId),"Cannot explicitly select foot VFX");
                    var fist=KiFistHand.GetOrCreate(foot);ki.currentKi=1000;Charge(fist,30);
                    typeof(KiFistHand).GetMethod("ShowVisual",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(fist,null);
                    Vector2 point=KiCombatMath.StrikePoint(foot);
                    Global.main.camera.transform.position=new Vector3(point.x+.3f,point.y+.9f,-10);Global.main.camera.orthographicSize=1.8f;
                }))yield break;
                yield return null;yield return null;yield return new WaitForEndOfFrame();
                if(!Check(()=>
                {
                    var effect=foot.GetComponentInChildren<KiStrikeLimbVfx>(true);
                    Assert(effect,"Charged foot has no volume component");
                    var coating=effect.GetComponentsInChildren<MeshRenderer>(true).First(r=>r.name.Contains("white core"));
                    Assert(coating.enabled && Vector2.Distance(coating.transform.position,KiCombatMath.StrikePoint(foot))<.03f,"3D coating did not follow the chosen foot");
                    Capture("04c-charged-foot-vfx.png");
                }))yield break;
                for(int i=0;i<12;i++)
                {
                    float a=i/11f*Mathf.PI*.9f;
                    rb.position=previousPosition+new Vector2(Mathf.Sin(a)*.7f,(1-Mathf.Cos(a))*.42f);Physics2D.SyncTransforms();
                    yield return null;
                }
                yield return new WaitForEndOfFrame();
                if(!Check(()=>
                {
                    var effect=foot.GetComponentInChildren<KiStrikeLimbVfx>(true);
                    var trail=effect.GetComponentsInChildren<MeshRenderer>(true).First(r=>r.name.Contains("limb wake"));
                    Assert(trail.enabled && trail.GetComponent<MeshFilter>().sharedMesh.vertexCount>4,"Moving foot generated no continuous volumetric trail");
                    Capture("04d-foot-swing-vfx.png");
                    Log("PASS Unity limb material follows selected foot; actual foot motion creates a continuous depth-rendered trail");
                }))yield break;
            }
            finally
            {
                if(loadout)loadout.Equip(string.IsNullOrEmpty(previous)?KiHandLoadout.NoneId:previous);
                if(rb)rb.position=previousPosition;if(ki)ki.currentKi=previousKi;
                if(Global.main && Global.main.camera){Global.main.camera.transform.position=previousCamera;Global.main.camera.orthographicSize=previousSize;}
                Physics2D.SyncTransforms();
            }
        }

        void VerifyBasicFistAndTransferRules()
        {
            support=AllyKiReservoir.GetOrCreate(person); donorSupport=AllyKiReservoir.GetOrCreate(donorPerson);
            opponentSupport=AllyKiReservoir.GetOrCreate(opponentPerson);
            support.SupportEnergy=donorSupport.SupportEnergy=opponentSupport.SupportEnergy=0;
            support.SupportStep(0);
            originalFormPower=person.GetComponent<MultipliersAndStuffs>().FormMultiplier;
            damageTarget=opponentPerson.Limbs.First(l=>l.name.Contains("LowerArm"));
            var fist=KiFistHand.GetOrCreate(hand.Limb);
            float before=ki.currentKi,health=damageTarget.Health;
            Charge(fist,30);
            Assert(fist.IsCharged && !fist.IsOvercharged,"Stock fist must work without received energy");
            Near(before-120,ki.currentKi,.03f,"Ordinary fist own-ki cost");
            Assert(!fist.DischargeForTest(hand.Limb,hand.Limb.transform.position),"Self damage must be rejected");
            Assert(fist.DischargeForTest(damageTarget,damageTarget.transform.position),"Stock hit failed");
            basicDamage=health-damageTarget.Health;
            Assert(Finite(basicDamage)&&basicDamage>0,"Stock fist damage must be finite and positive");
            Assert(!fist.DischargeForTest(damageTarget,damageTarget.transform.position),"Spent fist hit twice");
            damageTarget.Health=damageTarget.InitialHealth;
            Log("PASS stock fist without donors, own-ki payment, single discharge and self rejection");

            donorKi.currentKi=1000; ki.currentKi=1000;
            donorSupport.TeamId=1;support.TeamId=3; // Deliberately different obsolete values.
            Assert(donorSupport.DonateTo(donorSupport,.1f)==0,"Self transfer created energy");
            var position=subject.transform.position; subject.transform.position=new Vector3(100,30,0);
            Assert(donorSupport.DonateTo(support,.1f)==0,"Out-of-range transfer"); subject.transform.position=position;
            var wounded=person.Limbs.First(l=>l.name=="LowerBody");wounded.Health=wounded.InitialHealth*.5f;
            float oldMax=wounded.InitialHealth, donorBefore=donorKi.currentKi;
            float transferred=donorSupport.DonateTo(support,.1f);
            Assert(transferred>0,"Transfer still requires a matching team or class");
            Near(transferred,donorBefore-donorKi.currentKi,.01f,"Donor payment");
            Near(transferred,support.SupportEnergy,.01f,"Full recipient failed to store excess energy");
            Assert(wounded.InitialHealth>oldMax,"Received ki did not increase maximum health");
            Near(.5f,wounded.Health/wounded.InitialHealth,.001f,"Receiving ki healed an existing wound");
            Assert(support.PowerMultiplier>1,"Received energy did not increase strength");
            Assert(donorSupport.DonateTo(support,.1f)==0,"Duplicate per-frame donation");
            donorKi.currentKi=200;
            Assert(donorSupport.DonateTo(support,.1f)==0,"Donor reserve was spent");
            Log("PASS no team/class requirement, real energy conservation, progressive health and strength, wound preservation, reserve and frame limit");
            support.SupportEnergy=0; support.SupportStep(0);
            ki.currentKi=donorKi.currentKi=opponentKi.currentKi=1000;
            Assert(donorHand.Equip(KiHandLoadout.ShareId),"Donor share equip");
        }

        IEnumerator AccumulateRealDonations()
        {
            float total=0, lastHealth=person.Limbs[0].InitialHealth, lastPower=support.PowerMultiplier;
            for(int i=0;i<180 && support.StoredBars<1.1f;i++)
            {
                yield return null;
                if(!Check(()=>
                {
                    // Two real finite donors; neither may spend its last twenty percent.
                    float sent=donorSupport.DonateTo(support,.1f)+opponentSupport.DonateTo(support,.1f);
                    total+=sent;
                    Assert(support.PowerMultiplier>=lastPower,"More donated energy reduced power");
                    Assert(person.Limbs[0].InitialHealth>=lastHealth,"More donated energy reduced maximum health");
                    lastPower=support.PowerMultiplier;lastHealth=person.Limbs[0].InitialHealth;
                    Assert(donorKi.currentKi>=199.99f && opponentKi.currentKi>=199.99f,"Donor reserve violated");
                }))yield break;
            }
            if(!Check(()=>
            {
                Assert(total>1000 && support.OverdriveActive,"Excess received ki did not awaken Ki-Overdrive");
                Assert(person.GetComponentInChildren<KiOverdriveAura>(true),"Overdrive aura is missing");
                Near(originalFormPower,person.GetComponent<MultipliersAndStuffs>().FormMultiplier,1,"Donation changed permanent form");
                Log("PASS two donors, progressive energy/health/strength and Ki-Overdrive fury; received="+total+" power="+support.PowerMultiplier);
            }))yield break;
        }

        void VerifyOverchargeAndExpiry()
        {
            var fist=KiFistHand.GetOrCreate(hand.Limb);
            ki.currentKi=1000; float energyBefore=support.SupportEnergy;
            Charge(fist,61);
            Assert(fist.IsOvercharged,"Supported ordinary fist failed to overcharge");
            float spent=energyBefore-support.SupportEnergy;
            Assert(spent>=99 && spent<=201,"Ordinary overcharge paid incorrect support");
            fist.Arm();Near(energyBefore-spent,support.SupportEnergy,.01f,"Arming twice spent twice");
            float targetBefore=damageTarget.Health;
            Assert(fist.DischargeForTest(damageTarget,damageTarget.transform.position),"Overcharged strike failed");
            Assert(targetBefore-damageTarget.Health>basicDamage*5,"Overcharge is not substantially stronger");
            damageTarget.Health=damageTarget.InitialHealth;
            Log("PASS ordinary supported fist greatly exceeds stock strike and consumes energy once");

            ki.currentKi=1000;
            Assert(hand.Equip(KiHandLoadout.InfusionId),"All-energy technique missing");
            float stored=support.SupportEnergy;
            Charge(fist,30);
            Near(stored,fist.InfusedEnergy,.01f,"All received energy was not moved into fist");
            Near(0,support.SupportEnergy,.01f,"Infused energy remained duplicated in the body");
            fist.Cancel();Near(stored,support.SupportEnergy,.01f,"Cancelling lost or duplicated unused infusion");
            ki.currentKi=1000;Charge(fist,30);
            Assert(fist.DischargeForTest(damageTarget,damageTarget.transform.position),"Infused fist failed to strike");
            Assert(opponentPerson.GetComponent<KiInternalDisruption>(),"Infused fist did not inject internal disruption");
            Near(0,support.SupportEnergy,.01f,"Striking refunded spent infusion");
            Assert(!fist.DischargeForTest(damageTarget,damageTarget.transform.position),"Infused strike reused spent energy");
            foreach(var limb in person.Limbs)Assert(Finite(limb.Health)&&Finite(limb.InitialHealth),"Infusion produced invalid health");
            Log("PASS full-energy transfer into fist, cancellation refund, internal disruption, single spend and finite health");

            // A full charged fist and a large electrical impact for native visual inspection.
            support.ReturnSupport(1500);ki.currentKi=1000;Charge(fist,61);
            typeof(KiFistHand).GetMethod("ShowVisual",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(fist,null);
            KiImpactBurst.Spawn(new Vector2(3,30.5f),2,true);
        }
    }
}
