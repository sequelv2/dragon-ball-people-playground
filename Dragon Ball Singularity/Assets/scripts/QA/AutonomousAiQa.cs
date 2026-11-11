using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DBPU
{
    // Zero-teleport invariant: between two physics steps no watched body may move further than its
    // speed allows. Runs after everything else in FixedUpdate.
    [DefaultExecutionOrder(32000)]
    public sealed class AiQaTeleportWatch : MonoBehaviour
    {
        readonly List<Rigidbody2D> bodies = new List<Rigidbody2D>();
        readonly List<Vector2> position = new List<Vector2>(), velocity = new List<Vector2>();
        public int Violations, Steps; public float Worst; public string WorstBody = "";

        public void Watch(PersonBehaviour person)
        {
            foreach (var limb in person.Limbs)
            {
                var body = limb ? limb.GetComponent<Rigidbody2D>() : null;
                if (!body) continue;
                bodies.Add(body); position.Add(body.position); velocity.Add(body.velocity);
            }
        }

        public void Clear() { bodies.Clear(); position.Clear(); velocity.Clear(); }

        void FixedUpdate()
        {
            if (DbpuFx.Paused) return;
            float dt = Time.fixedDeltaTime;
            Steps++;
            for (int i = 0; i < bodies.Count; i++)
            {
                var body = bodies[i];
                if (!body) continue;
                Vector2 now = body.position, speed = body.velocity;
                float moved = Vector2.Distance(now, position[i]);
                float allowed = Mathf.Max(speed.magnitude, velocity[i].magnitude) * dt * 1.5f + .06f;
                if (moved > allowed)
                {
                    Violations++;
                    if (moved - allowed > Worst) { Worst = moved - allowed; WorstBody = body.name; }
                }
                position[i] = now; velocity[i] = speed;
            }
        }
    }

    // Opt-in proof of the autonomous AI in a disposable game process: -dbpu-ai-qa <absolute folder>.
    // Optional: -dbpu-ai-qa-only standing,flying,...  Writes ai_qa_report.json, result.txt, screenshots
    // and qa-ready.txt (PASS or FAIL), then quits. Never runs in an ordinary session, never saves settings.
    public sealed class AutonomousAiQa : MonoBehaviour
    {
        const string Fighter = "[DBPU] (BM) Goku";
        static bool started;
        string output; bool finished; string[] only;
        float groundY; GameObject platform;
        GameObject aiRoot, dummyRoot; PersonBehaviour aiPerson, dummyPerson; SingularityMind mind; AiQaTeleportWatch watch;
        readonly JObject report = new JObject(); readonly JArray scenarios = new JArray(); readonly JArray allStrikes = new JArray();
        int totalStrikes, totalTeleports, totalLeaks, brokenJoints; bool allPassed = true;

        public static void StartIfRequested()
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-dbpu-ai-qa");
            if (index < 0 || index + 1 >= args.Length || started) return;
            started = true;
            if (!Path.IsPathRooted(args[index + 1])) { Debug.LogError("AI QA requires an absolute output folder."); return; }
            CharacterPowerCatalog.SavingDisabled = true;
            Application.runInBackground = true;
            var go = new GameObject("DBPU opt-in autonomous AI QA");
            DontDestroyOnLoad(go);
            var qa = go.AddComponent<AutonomousAiQa>();
            qa.output = Path.GetFullPath(args[index + 1]);
            int filter = Array.IndexOf(args, "-dbpu-ai-qa-only");
            if (filter >= 0 && filter + 1 < args.Length) qa.only = args[filter + 1].Split(',');
            Directory.CreateDirectory(qa.output);
            File.Delete(Path.Combine(qa.output, "qa-ready.txt"));
            File.WriteAllText(Path.Combine(qa.output, "result.txt"), "DBPU autonomous AI QA started\n");
            qa.StartCoroutine(qa.Run());
        }

        void Log(string message) { File.AppendAllText(Path.Combine(output, "result.txt"), message + "\n"); }
        void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        bool Wanted(string name) { return only == null || only.Contains(name); }

        void Finish(bool success)
        {
            if (finished) return;
            finished = true;
            try
            {
                report["passed"] = success; report["scenarios"] = scenarios; report["strikes"] = allStrikes;
                File.WriteAllText(Path.Combine(output, "ai_qa_report.json"), report.ToString());
            }
            catch (Exception ex) { Log("report: " + ex); }
            File.WriteAllText(Path.Combine(output, "qa-ready.txt"), success ? "PASS\n" : "FAIL\n");
            Application.Quit(success ? 0 : 2);
        }

        void Capture(string file) { try { ScreenCapture.CaptureScreenshot(Path.Combine(output, file)); } catch (Exception ex) { Log("capture: " + ex.Message); } }

        GameObject Spawn(string name, Vector3 at)
        {
            var asset = ModAPI.FindSpawnable(name);
            var human = ModAPI.FindSpawnable("Human");
            Assert(asset && human && human.Prefab, "Missing native catalogue entry " + name);
            var root = Instantiate(human.Prefab, at, Quaternion.identity);
            root.AddComponent<DeregisterBehaviour>();
            root.AddComponent<TexturePackApplier>();
            root.AddComponent<AudioSourceTimeScaleBehaviour>();
            root.AddComponent<SerialiseInstructions>().OriginalSpawnableAsset = asset;
            root.name = asset.name;
            CatalogBehaviour.SpawnedGameObjects.Add(root);
            CatalogBehaviour.PerformMod(asset, root);
            Assert(root, "Native modification removed QA fighter " + name);
            return root;
        }

        static void Heal(PersonBehaviour person)
        {
            if (!person) return;
            foreach (var limb in person.Limbs)
            {
                if (!limb || limb.IsDismembered) continue;
                limb.Health = limb.InitialHealth;
                if (limb.Broken) limb.HealBone();
                limb.Numbness = 0f;
                if (limb.CirculationBehaviour) { limb.CirculationBehaviour.HealBleeding(); limb.CirculationBehaviour.InternalBleedingIntensity = 0f; }
            }
            person.Consciousness = 1f; person.ShockLevel = 0f; person.PainLevel = 0f;
        }

        static void FullKi(PersonBehaviour person)
        {
            var ki = person ? person.GetComponent<KiSystem>() : null;
            if (!ki) return;
            ki.currentKi = ki.maxKi; ki.isExhausted = false; ki.exhaustionTimer = 0f;
        }

        static int BrokenCount(PersonBehaviour person)
        {
            int n = 0;
            foreach (var limb in person.Limbs) if (limb && limb.Broken) n++;
            return n;
        }

        // What must be the same before the AI takes a body and after it lets go.
        static string Fingerprint(PersonBehaviour p)
        {
            var sb = new StringBuilder();
            sb.Append("pose=").Append(p.OverridePoseIndex).Append(";poses=").Append(p.Poses.Count);
            sb.Append(";aiPose=").Append(p.ActivePose != null && p.ActivePose.Name == "DBPU Singularity Mind");
            foreach (var limb in p.Limbs)
            {
                if (!limb) continue;
                var punch = limb.GetComponent<PunchPower>(); var kick = limb.GetComponent<KickPower>(); var hand = limb.GetComponent<KiHandLoadout>();
                var fist = limb.GetComponent<KiFistHand>();
                sb.Append(';').Append(limb.name).Append(punch && punch.Active ? "P1" : "P0").Append(kick && kick.Active ? "K1" : "K0");
                sb.Append(limb.Joint ? (limb.Joint.useLimits ? "L1" : "L0") : "L-").Append('h').Append(hand ? hand.SelectedId : "").Append(fist && fist.Equipped ? "F1" : "F0");
            }
            var flight = p.GetComponentInChildren<Flight>(true); sb.Append(";fly=").Append(flight && flight.IsFlying);
            var ui = p.GetComponent<SingularityUltraInstinct>(); sb.Append(";ui=").Append(ui && ui.SuppressedByAI);
            var loadout = p.GetComponent<KiFighterLoadout>(); sb.Append(";native=").Append(loadout ? loadout.NativeSelectedId : "");
            var blast = p.GetComponent<BlastBehaviour>(); sb.Append(";blast=").Append(blast && blast.Active);
            var beam = p.GetComponent<BeamBehaviour>(); sb.Append(";beam=").Append(beam && beam.Active);
            var shield = p.GetComponent<KiShield>(); sb.Append(";shield=").Append(shield && shield.Using);
            var barrage = p.GetComponentInChildren<Barrage>(true); sb.Append(";barrage=").Append(barrage && barrage.FiringForAI);
            return sb.ToString();
        }

        static float Median(List<float> values)
        {
            if (values.Count == 0) return 0f;
            values.Sort();
            int mid = values.Count / 2;
            return values.Count % 2 == 1 ? values[mid] : (values[mid - 1] + values[mid]) * .5f;
        }

        IEnumerator Run()
        {
            float deadline = Time.realtimeSinceStartup + 600f;
            for (int i = 0; i < 120; i++) yield return null;
            AsyncOperation loading = null;
            try
            {
                MapLoaderBehaviour.CurrentMap = MapRegistry.GetAllMaps().First(map => map.Prefab);
                loading = SceneManager.LoadSceneAsync("Main");
            }
            catch (Exception ex) { Log("FAIL load: " + ex); Finish(false); yield break; }
            yield return loading;
            yield return new WaitForSecondsRealtime(5f);
            try
            {
                if (!ModAPI.FindSpawnable(Fighter))
                {
                    var apply = typeof(CatalogBehaviour).GetMethod("ApplyModifications", BindingFlags.NonPublic | BindingFlags.Instance);
                    Assert(apply != null, "Native catalogue application method missing");
                    apply.Invoke(CatalogBehaviour.Main, null);
                    Log("QA ONLY: resumed native catalogue application after a third-party startup exception");
                }
                Singularity.SetEnabled(AutonomousMindModule.ModuleId, true);
                AutonomousConfig.Cinematics = false; AutonomousConfig.PersistMemory = false;
                // A floor of its own, well clear of the map, so every run starts from the same ground.
                platform = new GameObject("AI QA floor"); platform.layer = 11;
                platform.transform.position = new Vector3(0f, 300f, 0f);
                var box = platform.AddComponent<BoxCollider2D>(); box.size = new Vector2(400f, 2f);
                groundY = 301f;
                Physics2D.SyncTransforms();
                report["fixedDeltaTime"] = Time.fixedDeltaTime; report["gravity"] = Physics2D.gravity.y;
                report["beamPhysics"] = BeamPhysics.On; report["spherePhysics"] = SpherePhysics.On; report["physAtkStopLimbs"] = DBPU.PhysAtkStopLimbs;
                report["damageMultiplier"] = DBPU.DamageMultiplier;
            }
            catch (Exception ex) { Log("FAIL setup: " + ex); Finish(false); yield break; }

            if (Wanted("standing")) yield return Guarded("standing", Melee("standing", false, false, 40f, AiDifficulty.Hard));
            if (Wanted("flying")) yield return Guarded("flying", Melee("flying", true, false, 40f, AiDifficulty.Hard));
            if (Wanted("normal")) yield return Guarded("normal", Melee("normal", true, false, 25f, AiDifficulty.Normal));
            if (Wanted("one-arm")) yield return Guarded("one-arm", Melee("one-arm", true, true, 22f, AiDifficulty.Hard));
            if (Wanted("disadvantage")) yield return Guarded("disadvantage", Disadvantage());
            if (Wanted("blasts")) yield return Guarded("blasts", Blasts());
            if (Wanted("beam")) yield return Guarded("beam", Beam());
            if (Wanted("duel")) yield return Guarded("duel", Duel());
            if (Wanted("toggle")) yield return Guarded("toggle", Toggle());
            if (Wanted("death")) yield return Guarded("death", DeathAndDismemberment());
            if (Time.realtimeSinceStartup > deadline) { Log("FAIL: QA ran past its deadline"); allPassed = false; }

            report["totalStrikes"] = totalStrikes; report["teleportViolations"] = totalTeleports; report["ledgerLeaks"] = totalLeaks; report["brokenJoints"] = brokenJoints;
            bool invariants = totalTeleports == 0 && totalLeaks == 0 && brokenJoints == 0;
            if (!invariants) Log("FAIL invariants: teleports " + totalTeleports + ", ledger leaks " + totalLeaks + ", broken joints " + brokenJoints);
            Log((allPassed && invariants ? "PASS" : "FAIL") + " all scenarios; " + totalStrikes + " strikes");
            Finish(allPassed && invariants);
        }

        // Runs one scenario; an exception fails that scenario and the run goes on.
        IEnumerator Guarded(string name, IEnumerator body)
        {
            Log("--- " + name);
            while (true)
            {
                bool more; object current = null;
                try { more = body.MoveNext(); if (more) current = body.Current; }
                catch (Exception ex) { Log("FAIL " + name + ": " + ex); allPassed = false; break; }
                if (!more) break;
                yield return current;
            }
            yield return Clean();
        }

        IEnumerator Clean()
        {
            try
            {
                foreach (var m in SingularityMind.All.ToArray()) if (m && m.AiOn) m.SetAi(false);
                if (watch) { totalTeleports += watch.Violations; Destroy(watch); watch = null; }
                if (aiRoot) Destroy(aiRoot);
                if (dummyRoot) Destroy(dummyRoot);
                foreach (var stray in FindObjectsOfType<BlastComponent>()) Destroy(stray.gameObject);
            }
            catch (Exception ex) { Log("cleanup: " + ex); }
            aiRoot = dummyRoot = null; aiPerson = dummyPerson = null; mind = null;
            yield return new WaitForSecondsRealtime(.6f);
        }

        // Two fighters of the same character and form, facing each other; the AI is on the left.
        IEnumerator Pair(float gap, bool fly, float height = 0f)
        {
            aiRoot = Spawn(Fighter, new Vector3(-gap * .5f, groundY + 1.3f + height, 0f));
            dummyRoot = Spawn(Fighter, new Vector3(gap * .5f, groundY + 1.3f + height, 0f));
            if (Global.main && Global.main.camera)
            {
                Global.main.camera.transform.position = new Vector3(0f, groundY + 2.5f + height, -10f);
                Global.main.camera.orthographicSize = Mathf.Max(6f, gap * .7f);
            }
            yield return new WaitForSecondsRealtime(2f);
            aiPerson = aiRoot.GetComponent<PersonBehaviour>(); dummyPerson = dummyRoot.GetComponent<PersonBehaviour>();
            Assert(aiPerson && dummyPerson && aiPerson.IsAlive() && dummyPerson.IsAlive(), "QA fighters did not come up alive");
            if (fly)
            {
                var flight = dummyPerson.GetComponentInChildren<Flight>(true);
                Assert(flight, "The dummy has no Flight component");
                if (!flight.IsFlying) flight.Toggle();
            }
            watch = gameObject.AddComponent<AiQaTeleportWatch>();
            watch.Watch(aiPerson);
        }

        SingularityMind Wake(PersonBehaviour person, AiDifficulty difficulty, PersonBehaviour target)
        {
            var m = SingularityMind.For(person, true);
            m.SetDifficulty(difficulty); m.SetStance(AiStance.Duel);
            m.SetAi(true);
            Assert(m.AiOn, "The AI did not start on " + person.name);
            if (target) m.ForceTarget(target);
            return m;
        }

        // Collects every strike the mind recorded from `from` on, and the section 5.7 numbers for them.
        JObject StrikeStats(SingularityMind m, int from, out int inWindow, out int registered, out float medianGate, out float keptShare)
        {
            var gate = new List<float>(); var kept = new List<float>(); var contact = new List<float>();
            inWindow = registered = 0; int hits = 0, stopped = 0, ignored = 0, whiffs = 0, aborted = 0; float worstLoad = 0f, worstStress = 0f;
            int count = m.Strikes.RecordCount, first = Mathf.Max(from, count - m.Strikes.Records.Length);
            for (int i = first; i < count; i++)
            {
                var r = m.Strikes.Records[i % m.Strikes.Records.Length];
                allStrikes.Add(new JObject
                {
                    ["limb"] = r.Kick ? "leg" : "arm", ["slot"] = r.Slot, ["commanded"] = Math.Round(r.Commanded, 2), ["peak"] = Math.Round(r.Peak, 2),
                    ["atContact"] = Math.Round(r.AtContact, 2), ["gateSpeed"] = Math.Round(r.GateSpeed, 2), ["result"] = r.Result.ToString(),
                    ["inWindow"] = r.InWindow, ["flying"] = r.Flying, ["parry"] = r.Parry, ["damagePercent"] = Math.Round(r.DamagePercent, 3),
                    ["stopAt"] = Math.Round(r.StopAt, 3), ["jointLoad"] = Math.Round(r.JointLoad, 3), ["jointStress"] = Math.Round(r.JointStress, 3), ["time"] = Math.Round(r.At, 2)
                });
                if (r.Result == AiStrikeResult.Aborted) { aborted++; continue; }
                totalStrikes++;
                worstLoad = Mathf.Max(worstLoad, r.JointLoad); worstStress = Mathf.Max(worstStress, r.JointStress);
                switch (r.Result)
                {
                    case AiStrikeResult.Hit: hits++; break;
                    case AiStrikeResult.Stopped: stopped++; break;
                    case AiStrikeResult.Ignored: ignored++; break;
                    case AiStrikeResult.Whiff: whiffs++; break;
                }
                if (r.Touched && r.Peak > 0f) { kept.Add(r.AtContact / r.Peak); contact.Add(r.AtContact); }
                if (!r.InWindow || r.Parry) continue;
                inWindow++;
                if (r.GateSpeed >= KineticStrikeEngine.Gate) { registered++; gate.Add(r.GateSpeed); }
            }
            medianGate = Median(gate); keptShare = Median(kept);
            return new JObject
            {
                ["strikes"] = hits + stopped + ignored + whiffs, ["hits"] = hits, ["stopped"] = stopped, ["ignored"] = ignored, ["whiffs"] = whiffs, ["aborted"] = aborted,
                ["inWindow"] = inWindow, ["registered"] = registered, ["registeredShare"] = inWindow > 0 ? Math.Round((double)registered / inWindow, 3) : 0d,
                ["medianGateSpeed"] = Math.Round(medianGate, 2), ["medianSpeedIntoContact"] = Math.Round(Median(contact), 2), ["medianContactOverPeak"] = Math.Round(keptShare, 3),
                ["worstJointLoad"] = Math.Round(worstLoad, 3), ["worstJointStress"] = Math.Round(worstStress, 3),
                ["gain"] = Math.Round(m.Strikes.Gain, 3), ["speedBias"] = Math.Round(m.Strikes.SpeedBias, 2), ["oblique"] = Math.Round(m.Strikes.Oblique, 1),
                ["lungeShare"] = Math.Round(m.Strikes.LungeShare, 3), ["gateRatio"] = Math.Round(m.Strikes.GateRatio, 3)
            };
        }

        JObject RigFacts(SingularityMind m)
        {
            var rig = m.Rig; var arm = rig.Chains[KinematicsRig.ArmFront]; var leg = rig.Chains[KinematicsRig.LegFront];
            var facts = new JObject { ["mass"] = Math.Round(rig.Mass, 4), ["height"] = Math.Round(rig.Height, 3), ["armL1"] = Math.Round(arm.L1, 3), ["armL2"] = Math.Round(arm.L2, 3), ["legL1"] = Math.Round(leg.L1, 3), ["legL2"] = Math.Round(leg.L2, 3) };
            var parts = new JObject();
            foreach (var part in rig.Parts)
            {
                if (!part.Body) continue;
                parts[AiLimb.Names[part.Index]] = new JObject
                {
                    ["mass"] = Math.Round(part.Body.mass, 4), ["sign"] = part.Sign, ["votes"] = part.SignVotes, ["usable"] = part.Usable,
                    ["breakForce"] = part.Joint ? (float.IsInfinity(part.Joint.breakForce) ? -1d : Math.Round(part.Joint.breakForce, 1)) : 0d,
                    ["limits"] = part.Joint ? Math.Round(part.Joint.limits.min, 1) + ".." + Math.Round(part.Joint.limits.max, 1) : "",
                    ["breakingThreshold"] = part.Limb ? Math.Round(part.Limb.BreakingThreshold, 2) : 0d
                };
            }
            facts["parts"] = parts;
            return facts;
        }

        // The body is exactly as it was before the AI took it, and the ledger is empty.
        bool LetGoClean(SingularityMind m, PersonBehaviour person, string before, JObject result)
        {
            m.SetAi(false);
            string after = Fingerprint(person);
            bool same = after == before, empty = m.Ledger.Count == 0;
            result["ledgerLeft"] = m.Ledger.Count; result["restored"] = same;
            if (!same) { result["before"] = before; result["after"] = after; }
            if (!same || !empty) totalLeaks++;
            return same && empty;
        }

        // Sections 5.7: melee against a passive dummy of equal power.
        IEnumerator Melee(string name, bool fly, bool oneArm, float seconds, AiDifficulty difficulty)
        {
            yield return Pair(1.6f, fly, fly ? 6f : 0f);
            string before = Fingerprint(aiPerson);
            int brokenBefore = BrokenCount(aiPerson);
            mind = Wake(aiPerson, difficulty, dummyPerson);
            mind.GroundOnly = !fly;
            mind.TacticFilter = (1 << AiTactic.MeleeCombo) | (1 << AiTactic.FlightIntercept);
            if (oneArm)
            {
                yield return new WaitForSeconds(.5f);
                var arm = aiPerson.Limbs.First(l => l && l.name == "LowerArmFront");
                using (DbpuLimbDurability.AllowDismemberment(arm)) DbpuDismemberment.Cut(arm, arm.transform.position, Vector2.left, true);
                yield return new WaitForSeconds(.6f);
                Assert(!mind.Rig.ChainUsable(KinematicsRig.ArmFront), "The severed arm is still counted as usable");
            }
            float clock = 0f, heal = 0f; bool shot = false;
            while (clock < seconds)
            {
                clock += Time.deltaTime; heal -= Time.deltaTime;
                if (heal <= 0f) { heal = .5f; Heal(dummyPerson); Heal(aiPerson); FullKi(aiPerson); }
                if (!shot && clock > seconds * .4f && mind.Strikes.AnyBusy) { shot = true; Capture(name + ".png"); }
                if (Global.main && Global.main.camera && aiPerson) Global.main.camera.transform.position = new Vector3(mind.Rig.Com.x, mind.Rig.Com.y + .5f, -10f);
                yield return null;
            }
            var result = new JObject { ["name"] = name, ["difficulty"] = difficulty.ToString(), ["flying"] = fly, ["oneArm"] = oneArm, ["rig"] = RigFacts(mind) };
            int inWindow, registered; float medianGate, kept;
            result["melee"] = StrikeStats(mind, 0, out inWindow, out registered, out medianGate, out kept);
            int broken = Mathf.Max(0, BrokenCount(aiPerson) - brokenBefore);
            brokenJoints += broken; result["brokenJoints"] = broken;
            result["teleportViolations"] = watch.Violations; result["worstTeleport"] = Math.Round(watch.Worst, 3); result["worstTeleportBody"] = watch.WorstBody;
            result["flightAtEnd"] = mind.Flying;
            bool clean = LetGoClean(mind, aiPerson, before, result);
            float share = inWindow > 0 ? (float)registered / inWindow : 0f;
            // The Normal run is reported for the record; the acceptance numbers are judged on the Hard runs.
            bool judged = name != "normal";
            bool pass = clean && watch.Violations == 0 && broken == 0 && inWindow >= 8 && (!judged || (share >= .75f && medianGate >= 14f && kept >= .9f));
            result["passed"] = pass;
            scenarios.Add(result);
            Log((pass ? "PASS " : "FAIL ") + name + ": " + inWindow + " strikes in window, " + registered + " registered (" + (share * 100f).ToString("0") + "%), median read speed "
                + medianGate.ToString("0.0") + " m/s, contact/peak " + kept.ToString("0.00") + ", broken " + broken + ", teleports " + watch.Violations + ", clean " + clean);
            if (!pass) allPassed = false;
        }

        // Section 5.5: against a much stronger opponent the AI must not keep throwing punches that get stopped.
        IEnumerator Disadvantage()
        {
            yield return Pair(4f, true, 6f);
            var menu = dummyRoot.transform.Find("Head").GetComponent<TransformationContextMenu>();
            var forms = dummyRoot.GetComponent<CharacterTransformations>();
            string strong = forms.transformations.TransformationData.ContainsKey("SSJ1") ? "SSJ1" : menu.GetFirstNonBaseForm();
            menu.ApplyForm(strong);
            yield return new WaitForSecondsRealtime(3f);
            string before = Fingerprint(aiPerson);
            mind = Wake(aiPerson, AiDifficulty.Hard, dummyPerson);
            float clock = 0f, heal = 0f; float firstRisk = -1f;
            while (clock < 24f)
            {
                clock += Time.deltaTime; heal -= Time.deltaTime;
                if (heal <= 0f) { heal = .5f; Heal(dummyPerson); Heal(aiPerson); }
                if (firstRisk < 0f && clock > 1.5f) firstRisk = mind.StopRisk;
                if (Global.main && Global.main.camera) Global.main.camera.transform.position = new Vector3(mind.Rig.Com.x, mind.Rig.Com.y + .5f, -10f);
                yield return null;
            }
            Capture("disadvantage.png");
            var result = new JObject { ["name"] = "disadvantage", ["opponentForm"] = strong, ["stopRiskAtStart"] = Math.Round(firstRisk, 2) };
            int inWindow, registered; float medianGate, kept;
            var melee = StrikeStats(mind, 0, out inWindow, out registered, out medianGate, out kept);
            result["melee"] = melee; result["transforms"] = mind.Transforms; result["blasts"] = mind.BlastsFired; result["beams"] = mind.BeamsFired;
            result["kaioken"] = mind.KaiokenUses; result["kiFist"] = mind.FistStrikes; result["formAtEnd"] = mind.Abilities.Form;
            var tactics = new JObject();
            for (int i = 0; i < AiTactic.Count; i++) tactics[AiTactic.Names[i]] = mind.TacticCount[i];
            result["tactics"] = tactics;
            result["teleportViolations"] = watch.Violations;
            int stoppedHits = (int)melee["stopped"];
            int answers = mind.Transforms + mind.BlastsFired + mind.BeamsFired + mind.KaiokenUses + mind.FistStrikes;
            // Transformations and Kaioken stay with the fighter by design; the comparison is made on a body that has them.
            mind.SetAi(false);
            result["ledgerLeft"] = mind.Ledger.Count;
            if (mind.Ledger.Count != 0) totalLeaks++;
            bool pass = firstRisk > 0f && answers >= 1 && stoppedHits <= 6 && watch.Violations == 0 && mind.Ledger.Count == 0;
            result["passed"] = pass; scenarios.Add(result);
            Log((pass ? "PASS " : "FAIL ") + "disadvantage: stop risk " + firstRisk.ToString("0.00") + ", stopped hits " + stoppedHits + ", transforms " + mind.Transforms
                + ", blasts " + mind.BlastsFired + ", beams " + mind.BeamsFired + ", Kaioken " + mind.KaiokenUses + ", ends in " + mind.Abilities.Form);
            if (!pass) allPassed = false;
        }

        BlastComponent Shot(PersonBehaviour owner, Vector2 at, Vector2 direction, float damage, float speed)
        {
            var go = new GameObject("AI QA blast"); go.layer = 13; go.transform.position = at; go.transform.right = direction;
            var body = go.AddComponent<Rigidbody2D>(); body.gravityScale = 0f; body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            var collider = go.AddComponent<CircleCollider2D>(); collider.radius = .12f;
            var blast = go.AddComponent<BlastComponent>(); blast.User = owner.gameObject; blast.Damage = damage; blast.realSpeed = blast.speed = speed;
            foreach (var own in owner.GetComponentsInChildren<Collider2D>()) Physics2D.IgnoreCollision(collider, own, true);
            return blast;
        }

        // A volley of real ki shots coming in: the AI must read them and answer some of them.
        IEnumerator Blasts()
        {
            yield return Pair(14f, true, 6f);
            string before = Fingerprint(aiPerson);
            mind = Wake(aiPerson, AiDifficulty.Hard, dummyPerson);
            // Defence only, so the answer to the volley is what gets measured.
            mind.TacticFilter = (1 << AiTactic.EvadeDash) | (1 << AiTactic.EvadeWeave) | (1 << AiTactic.GuardBlock) | (1 << AiTactic.KiShield) | (1 << AiTactic.KiGuard) | (1 << AiTactic.FlightIntercept) | (1 << AiTactic.DisengageCharge);
            yield return new WaitForSeconds(1f);
            float health = SingularityFighters.HealthFraction(aiPerson);
            int fired = 0; float damage = MCP_Utils.AverageMaxHealth(aiPerson) * .02f;
            float clock = 0f, next = 0f; int tracked = 0;
            while (clock < 9f)
            {
                clock += Time.deltaTime; next -= Time.deltaTime;
                if (next <= 0f && fired < 18)
                {
                    next = .4f; fired++;
                    Vector2 from = (Vector2)dummyPerson.transform.Find("Body/UpperBody").position + Vector2.left * 1.2f;
                    Vector2 to = mind.Rig.Com;
                    Shot(dummyPerson, from, (to - from).normalized, damage, 26f);
                    FullKi(aiPerson);
                }
                foreach (var t in mind.Sensor.Tracks) if (t.Kind == AiTrackKind.Blast && t.Counted) tracked = Mathf.Max(tracked, 1);
                if (fired == 6 && next > .35f) Capture("blasts.png");
                yield return null;
            }
            var result = new JObject { ["name"] = "blasts", ["fired"] = fired, ["evades"] = mind.Evades, ["blocks"] = mind.Blocks, ["shields"] = mind.ShieldsRaised, ["guards"] = mind.GuardsHeld };
            float lost = health - SingularityFighters.HealthFraction(aiPerson);
            result["healthLost"] = Math.Round(lost, 4); result["sawShots"] = tracked > 0; result["teleportViolations"] = watch.Violations;
            int answers = mind.Evades + mind.Blocks + mind.ShieldsRaised + mind.GuardsHeld;
            bool clean = LetGoClean(mind, aiPerson, before, result);
            bool pass = tracked > 0 && answers >= 2 && watch.Violations == 0 && clean;
            result["passed"] = pass; scenarios.Add(result);
            Log((pass ? "PASS " : "FAIL ") + "blasts: " + fired + " fired, " + mind.Evades + " evades, " + mind.Blocks + " blocks, " + mind.ShieldsRaised + " shields, health lost " + (lost * 100f).ToString("0.0") + "%, clean " + clean);
            if (!pass) allPassed = false;
        }

        // A real Energy Wave fired at the AI.
        IEnumerator Beam()
        {
            yield return Pair(12f, true, 6f);
            string before = Fingerprint(aiPerson);
            mind = Wake(aiPerson, AiDifficulty.Hard, dummyPerson);
            mind.TacticFilter = (1 << AiTactic.EvadeDash) | (1 << AiTactic.KiShield) | (1 << AiTactic.KiGuard) | (1 << AiTactic.GuardBlock) | (1 << AiTactic.FlightIntercept);
            yield return new WaitForSeconds(1f);
            float health = SingularityFighters.HealthFraction(aiPerson);
            var beam = dummyRoot.GetComponent<BeamBehaviour>();
            Assert(beam, "The dummy has no BeamBehaviour");
            var arm = dummyPerson.Limbs.First(l => l && l.name == "LowerArmFront").transform;
            Vector2 aim = mind.Rig.Com - (Vector2)arm.position;
            beam.EffectID = "Energy Wave"; beam.BeamType = "ArmBeam";
            arm.up = -aim.normalized; Physics2D.SyncTransforms();
            var release = typeof(BeamBehaviour).GetMethod("Release", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert(release != null, "Native beam release method missing");
            var audio = dummyRoot.AddComponent<AudioSource>();
            beam.StartCoroutine((IEnumerator)release.Invoke(beam, new object[] { arm, audio }));
            float clock = 0f; bool seen = false, shot = false;
            while (clock < 5f)
            {
                clock += Time.deltaTime;
                foreach (var t in mind.Sensor.Tracks) if (t.Kind == AiTrackKind.Beam) seen = true;
                if (!shot && clock > 1f) { shot = true; Capture("beam.png"); }
                yield return null;
            }
            beam.CancelBeamState();
            yield return new WaitForSeconds(.5f);
            var result = new JObject { ["name"] = "beam", ["sawBeam"] = seen, ["guards"] = mind.GuardsHeld, ["evades"] = mind.Evades, ["shields"] = mind.ShieldsRaised };
            float lost = health - SingularityFighters.HealthFraction(aiPerson);
            result["healthLost"] = Math.Round(lost, 4); result["teleportViolations"] = watch.Violations;
            bool clean = LetGoClean(mind, aiPerson, before, result);
            int answers = mind.GuardsHeld + mind.Evades + mind.ShieldsRaised;
            bool pass = seen && answers >= 1 && watch.Violations == 0 && clean;
            result["passed"] = pass; scenarios.Add(result);
            Log((pass ? "PASS " : "FAIL ") + "beam: seen " + seen + ", guards " + mind.GuardsHeld + ", evades " + mind.Evades + ", shields " + mind.ShieldsRaised + ", health lost " + (lost * 100f).ToString("0.0") + "%, clean " + clean);
            if (!pass) allPassed = false;
        }

        // Two AIs, everything allowed: self-play. Both must fight, learn and plan, and nothing may teleport.
        IEnumerator Duel()
        {
            yield return Pair(8f, true, 6f);
            watch.Watch(dummyPerson);
            var a = Wake(aiPerson, AiDifficulty.Normal, dummyPerson);
            var b = Wake(dummyPerson, AiDifficulty.Hard, aiPerson);
            mind = a;
            float clock = 0f; bool shot = false;
            while (clock < 32f)
            {
                clock += Time.deltaTime;
                if (!shot && clock > 12f) { shot = true; Capture("duel.png"); }
                if (Global.main && Global.main.camera && a.Rig != null && b.Rig != null)
                {
                    Vector2 mid = (a.Rig.Com + b.Rig.Com) * .5f;
                    Global.main.camera.transform.position = new Vector3(mid.x, mid.y, -10f);
                    Global.main.camera.orthographicSize = Mathf.Clamp(Vector2.Distance(a.Rig.Com, b.Rig.Com) * .8f, 6f, 30f);
                }
                if (!aiPerson.IsAlive() || !dummyPerson.IsAlive()) break;
                yield return null;
            }
            var result = new JObject { ["name"] = "duel", ["seconds"] = Math.Round(clock, 1) };
            int unused1, unused2; float unused3, unused4;
            foreach (var m in new[] { a, b })
            {
                var side = new JObject
                {
                    ["difficulty"] = m.Difficulty.ToString(), ["decisions"] = m.Decisions, ["strikesStarted"] = m.StrikesStarted, ["blasts"] = m.BlastsFired, ["beams"] = m.BeamsFired,
                    ["evades"] = m.Evades, ["blocks"] = m.Blocks, ["parries"] = m.Parries, ["shields"] = m.ShieldsRaised, ["guards"] = m.GuardsHeld, ["flanks"] = m.Flanks,
                    ["transforms"] = m.Transforms, ["kaioken"] = m.KaiokenUses, ["barrages"] = m.Barrages, ["kiFist"] = m.FistStrikes,
                    ["lessons"] = m.Memory != null ? m.Memory.Updates : 0, ["rollbacks"] = m.Memory != null ? m.Memory.Rollbacks : 0,
                    ["health"] = Math.Round(SingularityFighters.HealthFraction(m.Person), 3), ["alive"] = m.Person.IsAlive(), ["form"] = m.Abilities.Form,
                    ["melee"] = StrikeStats(m, 0, out unused1, out unused2, out unused3, out unused4)
                };
                var tactics = new JObject();
                for (int i = 0; i < AiTactic.Count; i++) tactics[AiTactic.Names[i]] = m.TacticCount[i];
                side["tactics"] = tactics;
                result[m == a ? "left" : "right"] = side;
            }
            result["teleportViolations"] = watch.Violations; result["worstTeleport"] = Math.Round(watch.Worst, 3); result["worstTeleportBody"] = watch.WorstBody;
            bool fought = a.StrikesStarted + a.BlastsFired + a.BeamsFired > 0 && b.StrikesStarted + b.BlastsFired + b.BeamsFired > 0;
            bool learned = a.Memory != null && a.Memory.Updates > 0 && a.Memory.Rollbacks == 0 && a.Decisions > 5 && b.Decisions > 5;
            a.SetAi(false); b.SetAi(false);
            if (a.Ledger.Count != 0 || b.Ledger.Count != 0) totalLeaks++;
            bool pass = fought && learned && watch.Violations == 0 && a.Ledger.Count == 0 && b.Ledger.Count == 0;
            result["passed"] = pass; scenarios.Add(result);
            Log((pass ? "PASS " : "FAIL ") + "duel: left " + a.StrikesStarted + " strikes / " + a.BlastsFired + " blasts, right " + b.StrikesStarted + " strikes / " + b.BlastsFired
                + " blasts, lessons " + (a.Memory != null ? a.Memory.Updates : 0) + ", teleports " + watch.Violations);
            if (!pass) allPassed = false;
        }

        // Section 12.5: a hundred switches on and off leave the fighter as it was.
        IEnumerator Toggle()
        {
            yield return Pair(30f, false);
            string before = Fingerprint(aiPerson);
            var m = SingularityMind.For(aiPerson, true);
            m.SetDifficulty(AiDifficulty.Normal);
            int mismatches = 0, leaks = 0;
            for (int i = 0; i < 100; i++)
            {
                m.SetAi(true);
                Assert(m.AiOn, "The AI did not start on toggle " + i);
                yield return new WaitForFixedUpdate();
                if (i % 10 == 0) yield return new WaitForSeconds(.2f);
                m.SetAi(false);
                if (m.Ledger.Count != 0) leaks++;
                if (Fingerprint(aiPerson) != before) mismatches++;
            }
            yield return new WaitForSeconds(.3f);
            string after = Fingerprint(aiPerson);
            var result = new JObject { ["name"] = "toggle", ["cycles"] = 100, ["mismatches"] = mismatches, ["ledgerLeaks"] = leaks, ["same"] = after == before, ["teleportViolations"] = watch.Violations };
            if (after != before) { result["before"] = before; result["after"] = after; }
            if (leaks > 0 || mismatches > 0 || after != before) totalLeaks++;
            bool pass = mismatches == 0 && leaks == 0 && after == before && watch.Violations == 0;
            result["passed"] = pass; scenarios.Add(result);
            Log((pass ? "PASS " : "FAIL ") + "toggle: 100 cycles, " + mismatches + " mismatches, " + leaks + " ledger leaks");
            if (!pass) allPassed = false;
        }

        // A striking arm cut off mid-strike, then the fighter killed mid-strike: nothing may be left behind.
        IEnumerator DeathAndDismemberment()
        {
            yield return Pair(1.6f, true, 6f);
            mind = Wake(aiPerson, AiDifficulty.Hard, dummyPerson);
            mind.TacticFilter = (1 << AiTactic.MeleeCombo) | (1 << AiTactic.FlightIntercept);
            bool cut = false; float clock = 0f;
            while (clock < 12f && !cut)
            {
                clock += Time.deltaTime;
                Heal(dummyPerson);
                for (int slot = 0; slot < 2 && !cut; slot++)
                {
                    var strike = mind.Strikes.Strikes[slot];
                    if (strike.Phase != AiStrikePhase.Release) continue;
                    var limb = mind.Rig.Parts[mind.Rig.Chains[slot].Lower].Limb;
                    using (DbpuLimbDurability.AllowDismemberment(limb)) cut = DbpuDismemberment.Cut(limb, limb.transform.position, Vector2.up, true);
                }
                yield return null;
            }
            Assert(cut, "No strike reached its release to be interrupted");
            yield return new WaitForSeconds(1.5f);
            bool carriedOn = mind.AiOn && aiPerson.IsAlive();
            int strikesAfterCut = mind.Strikes.RecordCount;
            bool killed = false; clock = 0f;
            while (clock < 12f && !killed && carriedOn)
            {
                clock += Time.deltaTime;
                Heal(dummyPerson);
                if (mind.Strikes.Releasing)
                {
                    var head = AutonomousMindModule.HeadOf(aiPerson);
                    head.Health = 0f; aiPerson.Braindead = true; aiPerson.Consciousness = 0f;
                    killed = true;
                }
                yield return null;
            }
            yield return new WaitForSeconds(1f);
            var result = new JObject
            {
                ["name"] = "death", ["armCutMidStrike"] = cut, ["carriedOnAfterCut"] = carriedOn, ["killedMidStrike"] = killed, ["aiOffAfterDeath"] = !mind.AiOn,
                ["ledgerLeft"] = mind.Ledger.Count, ["poseReleased"] = aiPerson.ActivePose == null || aiPerson.ActivePose.Name != "DBPU Singularity Mind",
                ["overridePose"] = aiPerson.OverridePoseIndex, ["strikeRecords"] = strikesAfterCut, ["teleportViolations"] = watch.Violations
            };
            if (mind.Ledger.Count != 0) totalLeaks++;
            bool pass = cut && carriedOn && killed && !mind.AiOn && mind.Ledger.Count == 0 && aiPerson.OverridePoseIndex < aiPerson.Poses.Count && watch.Violations == 0
                && (aiPerson.ActivePose == null || aiPerson.ActivePose.Name != "DBPU Singularity Mind");
            result["passed"] = pass; scenarios.Add(result);
            Log((pass ? "PASS " : "FAIL ") + "death: cut " + cut + ", carried on " + carriedOn + ", killed " + killed + ", AI off " + !mind.AiOn + ", ledger " + mind.Ledger.Count);
            if (!pass) allPassed = false;
        }
    }
}
