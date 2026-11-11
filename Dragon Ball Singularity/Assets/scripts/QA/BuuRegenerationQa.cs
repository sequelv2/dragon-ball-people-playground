using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DBPU
{
    // Runs only in an explicitly requested, disposable game instance.
    // -dbpu-buu-qa <absolute output directory>
    public sealed class BuuRegenerationQa : MonoBehaviour
    {
        static bool started;
        static BuuRegenerationQa diagnosticOwner;
        static int damageDiagnosticCount;
        string output;
        bool finished;
        int errors;
        Camera captureCamera;
        readonly List<GameObject> subjects = new List<GameObject>();

        sealed class Body
        {
            public GameObject Root;
            public PersonBehaviour Person;
            public BuuRegeneration Regen;
            public LimbBehaviour[] Limbs;
            public Rigidbody2D[] Parents;
            public Vector3[] Scales;
            public int PreviewGroundLayer = -1;
        }

        sealed class ConstructorAudit
        {
            public int[] Arrived;
            public bool Pending, Progress;
            public int Frames;
        }

        public static void StartIfRequested()
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-dbpu-buu-qa");
            if (index < 0 || index + 1 >= args.Length || started) return;
            started = true;
            if (!Path.IsPathRooted(args[index + 1]))
            {
                Debug.LogError("Buu regeneration QA requires an absolute output folder.");
                return;
            }
            CharacterPowerCatalog.SavingDisabled = true;
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            var go = new GameObject("DBPU opt-in Buu regeneration QA");
            DontDestroyOnLoad(go);
            var qa = go.AddComponent<BuuRegenerationQa>();
            qa.output = Path.GetFullPath(args[index + 1]);
            Directory.CreateDirectory(qa.output);
            File.Delete(Path.Combine(qa.output, "qa-ready.txt"));
            File.WriteAllText(Path.Combine(qa.output, "result.txt"), "DBPU native Buu regeneration QA started\n");
            diagnosticOwner = qa;
            var damageMethod = typeof(SkinMaterialHandler).GetMethod("AddDamagePoint", new[] { typeof(DamageType), typeof(Vector2), typeof(float) });
            var prefix = typeof(BuuRegenerationQa).GetMethod("TraceNativeDamage", BindingFlags.Static | BindingFlags.NonPublic);
            new Harmony("dbpu.opt-in.buu-qa.native-damage").Patch(damageMethod, new HarmonyMethod(prefix));
            qa.StartCoroutine(qa.Run());
        }

        // The diagnostic patch is installed only above, after the explicit QA flag is found.
        static void TraceNativeDamage(SkinMaterialHandler __instance, DamageType type, Vector2 globalPosition, float intensity)
        {
            if (!diagnosticOwner || damageDiagnosticCount >= 8 || !__instance) return;
            var limb = __instance.GetComponent<LimbBehaviour>();
            if (!limb || !limb.Person) return;
            var regeneration = limb.Person.GetComponent<BuuRegeneration>();
            if (!regeneration || regeneration.RecoveryCount < 2 || regeneration.IsReforming) return;
            damageDiagnosticCount++;
            diagnosticOwner.Log("NATIVE DAMAGE CALL " + damageDiagnosticCount + " / " + limb.Person.name + " / " + limb.name
                + " type=" + type + " intensity=" + intensity.ToString("G8") + " position=" + globalPosition.ToString("G8")
                + " frame=" + Time.frameCount + " time=" + Time.time.ToString("F4") + " fixedTime=" + Time.fixedTime.ToString("F4")
                + "\n" + Environment.StackTrace);
        }

        void Awake() { Application.logMessageReceived += LogReceived; }
        void OnDestroy()
        {
            Application.logMessageReceived -= LogReceived;
            if (diagnosticOwner == this) diagnosticOwner = null;
            if (captureCamera) Destroy(captureCamera.gameObject);
        }
        void Log(string text) { if (output != null) File.AppendAllText(Path.Combine(output, "result.txt"), text + "\n"); }
        void LogReceived(string message, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert && type != LogType.Warning) return;
            string text = message + "\n" + stack;
            bool owned = new[] { "BuuRegeneration", "BuuAssemblyView", "BuuAssemblyMotion", "BuuFlesh", "BuuMatter", "[Majin assembly]", "Authored Majin flesh unavailable", "Blender assembly curves unavailable" }
                .Any(name => text.IndexOf(name, StringComparison.Ordinal) >= 0);
            bool ownedTeardown = message.IndexOf("Cannot set the parent", StringComparison.Ordinal) >= 0
                && (message.IndexOf("Majin", StringComparison.OrdinalIgnoreCase) >= 0 || message.IndexOf("Blender", StringComparison.OrdinalIgnoreCase) >= 0);
            if (!owned && !ownedTeardown) return;
            errors++;
            Log("RUNTIME " + type + " " + text);
        }
        static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
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
            StartCoroutine(FinalizeRun(success));
        }

        IEnumerator FinalizeRun(bool success)
        {
            // Keep the logger alive through real native actor teardown. A success
            // sentinel must not be written before assembly meshes release their roots.
            foreach (var subject in subjects) if (subject) Destroy(subject);
            if (captureCamera) Destroy(captureCamera.gameObject);
            yield return null;
            yield return null;
            success = success && errors == 0;
            Log("Buu runtime errors: " + errors);
            File.WriteAllText(Path.Combine(output, "qa-ready.txt"), success ? "PASS\n" : "FAIL\n");
            Application.Quit(success ? 0 : 2);
        }

        Body Spawn(string name, Vector3 position)
        {
            var asset = ModAPI.FindSpawnable("[DBPU] " + name);
            var human = ModAPI.FindSpawnable("Human");
            Assert(asset && human && human.Prefab, "Missing native catalogue entry: " + name);
            var root = Instantiate(human.Prefab, position, Quaternion.identity);
            root.AddComponent<DeregisterBehaviour>();
            root.AddComponent<TexturePackApplier>();
            root.AddComponent<AudioSourceTimeScaleBehaviour>();
            root.AddComponent<SerialiseInstructions>().OriginalSpawnableAsset = asset;
            root.name = asset.name;
            CatalogBehaviour.SpawnedGameObjects.Add(root);
            CatalogBehaviour.PerformMod(asset, root);
            Assert(root, "Native modification removed fighter " + name);
            subjects.Add(root);
            return Inspect(root);
        }

        Body Inspect(GameObject root)
        {
            var person = root.GetComponent<PersonBehaviour>();
            Assert(person && person.Limbs != null, root.name + " has no body");
            var body = new Body { Root = root, Person = person, Regen = root.GetComponent<BuuRegeneration>(), Limbs = person.Limbs.ToArray() };
            Assert(body.Regen, root.name + " spawn did not attach regeneration");
            body.Parents = body.Limbs.Select(limb => limb.Joint ? limb.Joint.connectedBody : null).ToArray();
            body.Scales = body.Limbs.Select(limb => limb.transform.localScale).ToArray();
            foreach (var rb in root.GetComponentsInChildren<Rigidbody2D>())
            {
                rb.gravityScale = 0f;
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
            // Keep this airborne test fixture stable after a recovery restores its saved physics.
            for (int i = 0; i < body.Regen.BodyBlueprint.Length; i++)
            {
                var blueprint = body.Regen.BodyBlueprint[i];
                blueprint.Gravity = 0f;
                body.Regen.BodyBlueprint[i] = blueprint;
            }
            return body;
        }

        void CheckBody(Body body, string label)
        {
            Assert(body.Root && body.Person && body.Regen, label + ": original fighter was replaced or destroyed");
            Assert(!body.Regen.IsReforming, label + ": recovery did not finish");
            if (!body.Person.IsAlive())
            {
                var head = body.Limbs.FirstOrDefault(limb => limb && limb.name == "Head");
                var internalHead = typeof(PersonBehaviour).GetField("Head", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(body.Person) as LimbBehaviour;
                Log("DEAD DIAGNOSTIC " + label + " personEnabled=" + body.Person.enabled + " internalHead=" + (internalHead ? internalHead.name + "#" + internalHead.GetInstanceID() : "null")
                    + " ownedHead=" + (head ? head.name + "#" + head.GetInstanceID() : "null") + " sameHead=" + (internalHead == head)
                    + " brainDead=" + body.Person.Braindead + " oxygen=" + body.Person.OxygenLevel);
                if (head) Log("DEAD HEAD health=" + head.Health + "/" + head.InitialHealth + " active=" + head.gameObject.activeInHierarchy + " limbEnabled=" + head.enabled
                    + " consideredAlive=" + head.IsConsideredAlive + " disintegrated=" + head.PhysicalBehaviour.isDisintegrated + " frozen=" + head.Frozen
                    + " nodeConnected=" + head.NodeBehaviour.IsConnectedToRoot + " blood=" + head.CirculationBehaviour.BloodAmount + " flow=" + head.CirculationBehaviour.BloodFlow
                    + " pump=" + head.CirculationBehaviour.IsPump + " circDisconnected=" + head.CirculationBehaviour.IsDisconnected);
            }
            Assert(body.Person.IsAlive(), label + ": fighter is not alive");
            Assert(body.Person.Limbs.Length == body.Limbs.Length, label + ": limb count changed");
            Assert(!body.Person.Braindead && !body.Person.BrainDamaged, label + ": brain state was not restored");
            for (int i = 0; i < body.Limbs.Length; i++)
            {
                var limb = body.Limbs[i];
                string part = label + " / " + (limb ? limb.name : "destroyed limb " + i);
                Assert(limb && body.Person.Limbs.Contains(limb), part + ": original limb missing");
                Assert(limb.gameObject.activeInHierarchy && !limb.PhysicalBehaviour.isDisintegrated, part + ": not restored");
                Assert(limb.Collider && limb.Collider.gameObject == limb.gameObject, part + ": native collider pointer was not rebound after skin replacement");
                Assert(limb.PhysicalBehaviour.rigidbody.simulated, part + ": physics is inactive");
                Assert(limb.Health >= limb.InitialHealth * .9f, part + ": health not recovered");
                Assert(!limb.Broken && !limb.IsDismembered, part + ": still broken/dismembered");
                Assert(limb.BreakingThreshold > 0f, part + ": Slice permanently removed durability");
                Assert(limb.NodeBehaviour.IsConnectedToRoot, part + ": native node graph disconnected; " + DescribeLimb(limb, body.Parents[i]));
                Assert(!limb.CirculationBehaviour.IsDisconnected, part + ": circulation disconnected");
                Assert(limb.CirculationBehaviour.BloodAmount >= .8f, part + ": blood not restored");
                Assert(limb.SkinMaterialHandler.AcidProgress < .01f && limb.SkinMaterialHandler.RottenProgress < .01f, part + ": damaged material remains");
                Assert(limb.SkinMaterialHandler.currentDamagePointCount == 0, part + ": native wound holes remain; " + DescribeLimb(limb, body.Parents[i]));
                Assert(limb.PhysicalBehaviour.BurnProgress < .01f, part + ": burns remain");
                Assert(Vector3.Distance(limb.transform.localScale, body.Scales[i]) < .015f, part + ": growth changed permanent limb scale");
                if (body.Parents[i])
                {
                    Assert(limb.HasJoint && limb.Joint && limb.Joint.enabled, part + ": native hinge not recreated; " + DescribeLimb(limb, body.Parents[i]));
                    Assert(limb.Joint.connectedBody == body.Parents[i], part + ": joint attached to wrong body; " + DescribeLimb(limb, body.Parents[i]));
                    Assert(limb.NodeBehaviour.IsConnectedTo(body.Parents[i].GetComponent<ConnectedNodeBehaviour>()), part + ": hinge and graph disagree; " + DescribeLimb(limb, body.Parents[i]));
                }
                foreach (var neighbour in limb.ConnectedLimbs)
                    Assert(!neighbour || neighbour.Person == body.Person, part + ": another Buu's limb was connected");
            }
            Log("PASS " + label + " | limbs " + body.Limbs.Length + " | recovery count " + body.Regen.RecoveryCount);
        }

        static string DescribeLimb(LimbBehaviour limb, Rigidbody2D expectedParent)
        {
            if (!limb) return "limb destroyed";
            var skin = limb.SkinMaterialHandler;
            int count = skin.currentDamagePointCount;
            float maxActive = skin.damagePoints == null ? 0f : skin.damagePoints.Take(Mathf.Max(0, count)).Select(point => point.z).DefaultIfEmpty(0f).Max();
            float maxStored = skin.damagePoints == null ? 0f : skin.damagePoints.Select(point => point.z).DefaultIfEmpty(0f).Max();
            var node = limb.NodeBehaviour;
            var joint = limb.Joint;
            var parentNode = expectedParent ? expectedParent.GetComponent<ConnectedNodeBehaviour>() : null;
            var rb = limb.PhysicalBehaviour.rigidbody;
            var woundTypes = skin.damagePoints == null ? "none" : string.Join(",", skin.damagePoints.Take(Mathf.Max(0, count)).Select((point, index) =>
                ((DamageType)(int)point.w) + ":" + point.z.ToString("G5") + "@" + (skin.damagePointTimeStamps != null && index < skin.damagePointTimeStamps.Length ? (Time.time - skin.damagePointTimeStamps[index]).ToString("F3") : "unknown")));
            return "woundCount=" + count + " maxActiveZ=" + maxActive.ToString("G6") + " maxStoredZ=" + maxStored.ToString("G6")
                + " health=" + limb.Health.ToString("G6") + "/" + limb.InitialHealth.ToString("G6")
                + " isRoot=" + node.IsRoot + " connectedRoot=" + node.IsConnectedToRoot + " nodeValue=" + node.Value
                + " hasJoint=" + limb.HasJoint + " joint=" + (joint ? joint.enabled.ToString() : "missing")
                + " parent=" + (joint && joint.connectedBody ? joint.connectedBody.name : "none")
                + " expected=" + (expectedParent ? expectedParent.name : "none")
                + " nodeToParent=" + (parentNode && node.IsConnectedTo(parentNode))
                + " isDismembered=" + limb.IsDismembered + " rigidbody=" + rb.bodyType + " velocity=" + rb.velocity.ToString("G6")
                + " angularVelocity=" + rb.angularVelocity.ToString("G6") + " gravity=" + rb.gravityScale.ToString("G6")
                + " position=" + rb.position.ToString("G6") + " rotation=" + rb.rotation.ToString("G6")
                + " wounds[type:z@age]=" + woundTypes;
        }

        void LogPhysicsFixture(Body body, string label)
        {
            Log("PHYSICS FIXTURE " + label + " | root position=" + body.Root.transform.position.ToString("G6")
                + " scale=" + body.Root.transform.lossyScale.ToString("G6"));
            for (int i = 0; i < body.Limbs.Length; i++)
            {
                var limb = body.Limbs[i];
                var collider = ActualCollider(limb);
                if (!limb || !collider) continue;
                var ignored = new List<string>();
                for (int j = i + 1; j < body.Limbs.Length; j++)
                {
                    var other = ActualCollider(body.Limbs[j]);
                    if (other) ignored.Add(j + ":" + Physics2D.GetIgnoreCollision(collider, other));
                }
                Log("SELF IGNORE " + i + ":" + limb.name + " [" + string.Join(",", ignored) + "] colliderEnabled=" + collider.enabled
                    + " nativePointer=" + (limb.Collider ? limb.Collider.name : "NULL"));
                var bounds = collider.bounds;
                var external = Physics2D.OverlapBoxAll(bounds.center, bounds.size + Vector3.one * .01f, 0f)
                    .Where(other => other && other.transform.root != body.Root.transform.root).Select(other =>
                        other.name + " root=" + other.transform.root.name + " layer=" + other.gameObject.layer + " trigger=" + other.isTrigger
                        + " ignored=" + Physics2D.GetIgnoreCollision(collider, other));
                Log("EXTERNAL OVERLAPS " + i + ":" + limb.name + " [" + string.Join("; ", external) + "]"
                    + " blueprintGravity=" + body.Regen.BodyBlueprint[i].Gravity.ToString("G6"));
            }
        }

        static Collider2D ActualCollider(LimbBehaviour limb)
        {
            if (!limb) return null;
            return limb.GetComponents<Collider2D>().FirstOrDefault(collider => collider && !collider.isTrigger) ?? limb.Collider;
        }

        void LogResidualWounds(Body body, string label, string elapsed)
        {
            if (!body.Root) return;
            for (int i = 0; i < body.Limbs.Length; i++)
                if (body.Limbs[i] && body.Limbs[i].SkinMaterialHandler.currentDamagePointCount > 0)
                    Log("RECOVERY DIAGNOSTIC " + label + " / " + body.Limbs[i].name + " at " + elapsed + "; " + DescribeLimb(body.Limbs[i], body.Parents[i]));
        }

        IEnumerator Recover(Body body, int previous, string label, bool fullConstructor = false)
        {
            var construction = new ConstructorAudit();
            float deadline = Time.time + 12f;
            float wallDeadline = Time.realtimeSinceStartup + 30f;
            while (body.Regen && (body.Regen.RecoveryCount <= previous || body.Regen.IsReforming)
                && Time.time < deadline && Time.realtimeSinceStartup < wallDeadline)
            {
                if (body.Regen.Stage == BuuRecoveryStage.Grow && !Check(() => CheckConstructorFrame(body, construction, fullConstructor))) yield break;
                yield return null;
            }
            yield return new WaitForSeconds(.2f);
            LogResidualWounds(body, label, "0.20s after reformation");
            // Give the native hinge solver and normal, progressive wound healing a short
            // settling window. The same full integrity assertions still apply afterward.
            yield return new WaitForSeconds(.45f);
            LogResidualWounds(body, label, "0.65s after reformation");
            if (body.Root && body.Limbs.Any(limb => limb && limb.SkinMaterialHandler.currentDamagePointCount > 0))
            {
                LogPhysicsFixture(body, label);
                yield return Capture(body, "residual-wounds-" + body.Regen.RecoveryCount);
                if (finished) yield break;
            }
            Check(() =>
            {
                Assert(body.Regen && body.Regen.RecoveryCount > previous, label + ": no completed recovery before timeout");
                if (fullConstructor) CheckConstructorComplete(body, construction);
                CheckBody(body, label);
            });
        }

        void Slice(Body body, params int[] indices)
        {
            foreach (int i in indices)
            {
                var limb = body.Limbs[i];
                limb.ImmuneToDamage = false;
                limb.Slice();
            }
        }
        void Scatter(Body body, params int[] indices)
        {
            for (int j = 0; j < indices.Length; j++)
            {
                var limb = body.Limbs[indices[j]];
                var rb = limb.PhysicalBehaviour.rigidbody;
                rb.position += new Vector2((j % 2 == 0 ? -1 : 1) * (1.1f + j * .12f), .35f + j * .2f);
                rb.rotation += 45f + j * 20f;
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
        }

        IEnumerator Capture(Body body, string name, float size = 3.2f)
        {
            yield return new WaitForEndOfFrame();
            Check(() =>
            {
                if (!captureCamera)
                {
                    captureCamera = new GameObject("Buu QA screenshot camera").AddComponent<Camera>();
                    captureCamera.CopyFrom(Global.main.camera);
                    captureCamera.enabled = false;
                    captureCamera.clearFlags = CameraClearFlags.SolidColor;
                    captureCamera.backgroundColor = new Color(.055f, .06f, .075f, 1f);
                }
                var chest = body.Person.Limbs.First(limb => limb && limb.name == "MiddleBody");
                captureCamera.transform.position = new Vector3(chest.transform.position.x, chest.transform.position.y + .25f, -10f);
                captureCamera.transform.rotation = Quaternion.identity;
                captureCamera.orthographic = true;
                captureCamera.orthographicSize = size;
                captureCamera.aspect = 16f / 9f;
                var target = new RenderTexture(1280, 720, 24);
                captureCamera.targetTexture = target;
                var old = RenderTexture.active;
                var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                try
                {
                    captureCamera.Render();
                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                    image.Apply();
                    File.WriteAllBytes(Path.Combine(output, name + ".png"), image.EncodeToPNG());
                }
                finally
                {
                    captureCamera.targetTexture = null;
                    RenderTexture.active = old;
                    Destroy(image);
                    target.Release();
                    Destroy(target);
                }
                Log("SCREENSHOT " + name);
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
            yield return new WaitForSecondsRealtime(5);
            if (!Check(() =>
            {
                if (!ModAPI.FindSpawnable("[DBPU] Kid Buu"))
                {
                    var apply = typeof(CatalogBehaviour).GetMethod("ApplyModifications", BindingFlags.NonPublic | BindingFlags.Instance);
                    Assert(apply != null, "Native catalogue application method missing");
                    apply.Invoke(CatalogBehaviour.Main, null);
                    Log("QA ONLY: resumed native catalogue application after third-party startup exception");
                }
                Global.main.SetPausedMenu(false);
                Time.timeScale = 1f;
                foreach (var canvas in FindObjectsOfType<Canvas>()) canvas.enabled = false;
            })) yield break;

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-dbpu-buu-movement-qa") >= 0)
            {
                yield return CheckMovingAssembly();
                if (!finished) Finish(errors == 0);
                yield break;
            }

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-dbpu-buu-copy-qa") >= 0)
            {
                yield return CheckPauseAndNativeCopy();
                if (!finished) Finish(errors == 0);
                yield break;
            }

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-dbpu-buu-bomb-qa") >= 0)
            {
                yield return CheckHakaiAndSpiritBomb();
                if (!finished) Finish(errors == 0);
                yield break;
            }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-dbpu-buu-preview") >= 0)
            {
                yield return RecordPreview();
                yield break;
            }

            int number = 0;
            foreach (string name in new[] { "Kid Buu", "Majin Buu", "Super Buu", "Evil Buu" })
            {
                Body body = null;
                if (!Check(() => body = Spawn(name, new Vector3(number++ * 25f, 40f, 0f)))) yield break;
                yield return new WaitForSeconds(1.1f);
                if (!Check(() => CheckBody(body, name + " initial"))) yield break;
                if (name == "Kid Buu")
                {
                    yield return AuditVisualIntegration(body);
                    if (finished) yield break;
                }
                yield return Capture(body, name.Replace(" ", "-") + "-intact");
                if (finished) yield break;

                int previous = body.Regen.RecoveryCount;
                if (!Check(() => Slice(body, 0, 11, 13))) yield break;
                yield return new WaitForSeconds(.16f);
                if (!Check(() => Scatter(body, 0, 11, 13))) yield break;
                yield return Capture(body, name.Replace(" ", "-") + "-severed");
                if (finished) yield break;
                yield return new WaitForSeconds(.7f);
                yield return Capture(body, name.Replace(" ", "-") + "-gathering");
                if (finished) yield break;
                yield return Recover(body, previous, name + " scattered head and hands");
                if (finished) yield break;
                yield return Capture(body, name.Replace(" ", "-") + "-reformed");
                if (finished) yield break;

                previous = body.Regen.RecoveryCount;
                if (!Check(() =>
                {
                    var survivor = body.Limbs[6]; // Native human's back foot; deliberately not a brain/root limb.
                    foreach (var limb in body.Limbs) if (limb != survivor) limb.PhysicalBehaviour.Disintegrate();
                    Assert(survivor.gameObject.activeInHierarchy, name + ": foot survivor was removed");
                })) yield break;
                yield return new WaitForSeconds(.7f);
                yield return Capture(body, name.Replace(" ", "-") + "-single-foot", 3.8f);
                if (finished) yield break;
                yield return Recover(body, previous, name + " regenerated from one foot without a head");
                if (finished) yield break;

                previous = body.Regen.RecoveryCount;
                if (!Check(() =>
                {
                    body.Limbs[0].PhysicalBehaviour.Disintegrate();
                    body.Limbs[13].PhysicalBehaviour.Disintegrate();
                })) yield break;
                yield return Recover(body, previous, name + " repeated disintegration did not overcount/remove root");
                if (finished) yield break;

                if (!Check(() =>
                {
                    var limb = body.Limbs[1];
                    limb.Health = limb.InitialHealth * .1f;
                    limb.BreakBone();
                    limb.SkinMaterialHandler.AcidProgress = .35f;
                    limb.SkinMaterialHandler.RottenProgress = .3f;
                    limb.SkinMaterialHandler.AddDamagePoint(DamageType.Bullet, limb.transform.position, 18f);
                    limb.PhysicalBehaviour.BurnProgress = .35f;
                    limb.CirculationBehaviour.InternalBleedingIntensity = 2f;
                    body.Person.Braindead = body.Person.BrainDamaged = true;
                    body.Person.OxygenLevel = .01f;
                })) yield break;
                yield return new WaitForSeconds(4f);
                if (!Check(() => CheckBody(body, name + " local wounds and vital-state recovery"))) yield break;
                Destroy(body.Root);
                yield return null;
            }

            Body toggle = null;
            if (!Check(() => toggle = Spawn("Kid Buu", new Vector3(0f, 40f, 0f)))) yield break;
            yield return new WaitForSeconds(1.1f);
            int toggleCount = toggle.Regen.RecoveryCount;
            if (!Check(() => { toggle.Regen.RegenerationEnabled = false; Slice(toggle, 13); })) yield break;
            yield return new WaitForSeconds(3f);
            if (!Check(() =>
            {
                Assert(toggle.Regen.RecoveryCount == toggleCount && !toggle.Regen.IsReforming, "Disabled regeneration repaired the body");
                Assert(toggle.Limbs[13].IsDismembered || !toggle.Limbs[13].Joint, "Disabled regeneration reattached limb");
                Log("PASS regeneration toggle suppresses automatic repair");
                toggle.Regen.RegenerationEnabled = true;
            })) yield break;
            yield return Recover(toggle, toggleCount, "Re-enabling regeneration repairs existing damage");
            if (finished) yield break;
            Destroy(toggle.Root);
            yield return null;

            Body first = null, second = null;
            if (!Check(() =>
            {
                first = Spawn("Kid Buu", new Vector3(0f, 40f, 0f));
                second = Spawn("Super Buu", new Vector3(1.5f, 40f, 0f));
            })) yield break;
            yield return new WaitForSeconds(1.1f);
            int firstCount = first.Regen.RecoveryCount, secondCount = second.Regen.RecoveryCount;
            if (!Check(() => { Slice(first, 0, 11); Slice(second, 0, 13); })) yield break;
            yield return new WaitForSeconds(.16f);
            if (!Check(() => { Scatter(first, 0, 11); Scatter(second, 0, 13); })) yield break;
            yield return Recover(first, firstCount, "First Buu independent identity");
            if (finished) yield break;
            yield return Recover(second, secondCount, "Second Buu independent identity");
            if (finished) yield break;
            Destroy(first.Root); Destroy(second.Root);
            yield return null;

            yield return CheckRecoveryTimeline();
            if (finished) yield break;
            yield return CheckPauseAndNativeCopy();
            if (finished) yield break;
            yield return CheckHakaiAndSpiritBomb();
            if (finished) yield break;
            yield return CheckNativeCrushFragments();
            if (finished) yield break;

            Body wiped = null;
            if (!Check(() => wiped = Spawn("Kid Buu", new Vector3(0f, 40f, 0f)))) yield break;
            yield return new WaitForSeconds(1.1f);
            if (!Check(() => { foreach (var limb in wiped.Limbs) limb.PhysicalBehaviour.Disintegrate(); })) yield break;
            yield return new WaitForSeconds(4f);
            if (!Check(() =>
            {
                Assert(!wiped.Root || !wiped.Root.GetComponentsInChildren<PhysicalBehaviour>().Any(p => p && !p.isDisintegrated), "Complete destruction resurrected Buu");
                Assert(FindObjectsOfType<BuuRegeneration>().Length == 0, "Regeneration left an independent immortal anchor after complete destruction");
                Log("PASS total destruction leaves no matter, duplicate or immortal anchor");
            })) yield break;
            Finish(errors == 0);
        }

        IEnumerator AuditVisualIntegration(Body body)
        {
            var ribbons = body.Root.GetComponentInChildren<BuuRegenerationFx>();
            if (!Check(() =>
            {
                Assert(BuuAssemblyMotion.SourceReady, "Blender-authored piece flight/snap actions were not loaded");
                ribbons.Link(new Vector2(-8f, 40f), new Vector2(8f, 40f), .45f);
                ribbons.GrowBody(new Vector2(0f, 39f), new Vector2(0f, 42f), 2f, .5f);
            })) yield break;
            yield return new WaitForEndOfFrame();
            if (!Check(() =>
            {
                var mesh = ribbons.GetComponent<MeshFilter>().sharedMesh;
                Assert(mesh && mesh.vertexCount == 0, "Legacy Link/GrowBody calls still created ribbon or smooth body geometry");
                Log("PASS Blender-authored assembly motion loaded; legacy ribbon/body-sheath calls create no visible geometry");
            })) yield break;
            Texture interior = null;
            if (!Check(() =>
            {
                interior = body.Limbs[0].SkinMaterialHandler.renderer.material.GetTexture("_FleshTex");
                // This is the same production skin-replacement route used by costumes.
                body.Person.ChangeSkin(body.Limbs.Select(limb => limb.SkinMaterialHandler.renderer.sprite).ToList());
            })) yield break;
            yield return new WaitForSeconds(.2f);
            if (!Check(() =>
            {
                foreach (var limb in body.Limbs)
                {
                    var material = limb.SkinMaterialHandler.renderer.material;
                    Assert(material.GetTexture("_FleshTex") == interior && material.GetTexture("_BoneTex") == interior,
                        "Skin replacement restored human interior on " + limb.name);
                    var generator = limb.GetComponent<ShatteredObjectSpriteInitialiser>();
                    if (generator && generator.BoneGenerator)
                        Assert(generator.BoneGenerator.Sprite && generator.BoneGenerator.Sprite.name == "DBS Majin tissue shard",
                            "Crushing still uses human bone fragments on " + limb.name);
                }
                Log("PASS costume replacement preserves Majin interior and native fragment sprites");
            })) yield break;

            var effects = body.Root.GetComponentInChildren<BuuRegenerationFx>();
            var clockField = typeof(BuuRegenerationFx).GetField("clock", BindingFlags.NonPublic | BindingFlags.Instance);
            float heldClock = 0, heldHealth = 0;
            if (!Check(() =>
            {
                Assert(effects && clockField != null, "Missing native regeneration FX clock");
                effects.Wound(body.Limbs[1].transform.position, .4f);
                heldClock = (float)clockField.GetValue(effects);
                body.Limbs[1].Health = heldHealth = body.Limbs[1].InitialHealth * .35f;
                Assert(SingularityWorldFreeze.Begin(new Transform[0], .65f, null), "Unable to start native world freeze");
            })) yield break;
            yield return new WaitForSecondsRealtime(.15f);
            if (!Check(() =>
            {
                Assert(SingularityWorldFreeze.Active, "Native world freeze expired too early for the check");
                Assert(Mathf.Abs((float)clockField.GetValue(effects) - heldClock) < .00001f, "Majin FX advanced through stopped time");
                Assert(body.Limbs[1].Health == heldHealth, "Majin tissue healed through stopped time");
                Log("PASS native world freeze holds both Majin tissue and FX");
            })) yield break;
            yield return new WaitForSecondsRealtime(1.15f);
            if (!Check(() =>
            {
                Assert(!SingularityWorldFreeze.Active, "Native world freeze failed to end");
                Assert((float)clockField.GetValue(effects) > heldClock && body.Limbs[1].Health > heldHealth,
                    "Majin tissue or FX failed to resume after world freeze");
                CheckBody(body, "Majin resumes after native world freeze");
            })) yield break;
        }

        IEnumerator RecordPreview()
        {
            Body body = null;
            if (!Check(() =>
            {
                body = Spawn("Kid Buu", new Vector3(0f, 40f, 0f));
                // Hold the actual native rig briefly before the attack, keeping the
                // production blueprint's Dynamic body types intact for regeneration.
                foreach (var limb in body.Limbs) limb.PhysicalBehaviour.rigidbody.bodyType = RigidbodyType2D.Kinematic;
            })) yield break;
            yield return new WaitForSeconds(1.1f);

            yield return AuditVisualIntegration(body);
            if (finished) yield break;

            const int width = 1280, height = 720, fps = 24, frames = 288;
            RenderTexture target = null;
            Texture2D image = null;
            float floorY = 0f;
            string directory = Path.Combine(output, "preview");
            if (!Check(() =>
            {
                CheckBody(body, "Preview native fighter before full-body Crush");
                Assert(BuuFleshAssets.Ready, "Blender-authored flesh bundle is unavailable; a fallback preview is not acceptable");
                Assert(BuuAssemblyMotion.SourceReady, "Blender-authored assembly flight/snap motion is unavailable");
                floorY = PreparePreviewFloor(body);
                Directory.CreateDirectory(directory);
                captureCamera = new GameObject("Buu QA native animation camera").AddComponent<Camera>();
                captureCamera.CopyFrom(Global.main.camera);
                captureCamera.enabled = false;
                captureCamera.clearFlags = CameraClearFlags.SolidColor;
                captureCamera.backgroundColor = new Color(.055f, .06f, .075f, 1f);
                captureCamera.orthographic = true;
                captureCamera.orthographicSize = 3.25f;
                captureCamera.aspect = (float)width / height;
                captureCamera.transform.position = new Vector3(body.Root.transform.position.x, floorY + 2f, -10f);
                captureCamera.transform.rotation = Quaternion.identity;
                target = new RenderTexture(width, height, 24);
                image = new Texture2D(width, height, TextureFormat.RGB24, false);
                File.WriteAllText(Path.Combine(output, "preview-info.txt"), "Native People Playground full-body Crush; all real owned fragments remain alive and physical\n288 frames at 24 fps; 1280x720; gravity and a 40m wide native metal platform\nEvery native body part assembles from 5-15 opaque actual sprite pieces using Blender-authored flight/snap actions\n");
            })) yield break;

            int oldCaptureRate = Time.captureFramerate;
            int before = body.Regen.RecoveryCount;
            BuuRecoveryStage previousStage = BuuRecoveryStage.Idle;
            var stages = new List<BuuRecoveryStage>();
            float growCameraAge = -1f;
            PhysicalBehaviour[] previewFragments = null;
            BuuFleshView previewAuthored = null;
            float previewRestClock = 0f;
            bool checkedAuthoredRest = false, checkedAuthoredQuiver = false;
            var construction = new ConstructorAudit();
            try
            {
                Time.captureFramerate = fps;
                for (int frame = 0; frame < frames; frame++)
                {
                    if (frame == 12)
                    {
                        if (!Check(() =>
                        {
                            foreach (var limb in body.Limbs) limb.PhysicalBehaviour.rigidbody.bodyType = RigidbodyType2D.Dynamic;
                            var fragments = previewFragments = CrushEntireBody(body, false);
                            Assert(fragments.All(part => part && !part.isDisintegrated), "Preview removed genuine fragments before the recovery sequence");
                            foreach (int layer in fragments.Select(part => part.gameObject.layer).Distinct())
                            {
                                bool ignoresGround = Physics2D.GetIgnoreLayerCollision(layer, body.PreviewGroundLayer);
                                Log("PREVIEW COLLISION fragmentLayer=" + layer + " ground/platformLayer=" + body.PreviewGroundLayer + " ignored=" + ignoresGround);
                                Assert(!ignoresGround, "Native physical fragment layer ignores the preview floor collision layer");
                            }
                            Log("PREVIEW DAMAGE native limbs destroyed=" + body.Limbs.Count(limb => limb.PhysicalBehaviour.isDisintegrated)
                                + " surviving real physical fragments=" + fragments.Length + " gravity preserved");
                        })) yield break;
                    }
                    yield return new WaitForEndOfFrame();
                    int sample = frame;
                    if (!Check(() =>
                    {
                        var stage = body.Regen.Stage;
                        if (stage == BuuRecoveryStage.Grow) CheckConstructorFrame(body, construction, true);
                        if (previewFragments != null && (stage == BuuRecoveryStage.Rest || stage == BuuRecoveryStage.Quiver))
                        {
                            var source = previewFragments.FirstOrDefault(part => part && !part.isDisintegrated && part.GetComponentInChildren<BuuFleshView>());
                            Assert(source, "Actual physical Crush fragments have no authored Blender flesh view");
                            if (!previewAuthored)
                            {
                                previewAuthored = CheckAuthoredFragment(source, "Preview actual fragment");
                                previewRestClock = AuthoredClock(previewAuthored);
                            }
                            if (stage == BuuRecoveryStage.Rest)
                            {
                                Assert(Mathf.Abs(AuthoredFrame(previewAuthored)) < .00001f && Mathf.Abs(AuthoredClock(previewAuthored) - previewRestClock) < .00001f,
                                    "Rest phase animated the Blender flesh pose before the physical quiver");
                                checkedAuthoredRest = true;
                            }
                            else if (body.Regen.StageAge >= .15f)
                            {
                                Assert(AuthoredClock(previewAuthored) > previewRestClock && AuthoredFrame(previewAuthored) > 0f,
                                    "Quiver phase did not advance the authored Blender animation");
                                checkedAuthoredQuiver = true;
                            }
                        }
                        if (stage != previousStage)
                        {
                            if (stage == BuuRecoveryStage.Quiver && previewFragments != null)
                            {
                                var below = previewFragments.Where(part => part && !part.isDisintegrated && part.rigidbody.position.y < floorY - .15f).ToArray();
                                Log("PREVIEW FLOOR SUPPORT at Quiver: belowPlatform=" + below.Length + " floorY=" + floorY.ToString("G6")
                                    + " lowestFragmentY=" + previewFragments.Where(part => part && !part.isDisintegrated).Min(part => part.rigidbody.position.y).ToString("G6"));
                                Assert(below.Length == 0, "Real fragments fell through the native preview platform: "
                                    + string.Join(",", below.Select(part => part.name + "@" + part.rigidbody.position.y.ToString("G6"))));
                            }
                            Log("PREVIEW STAGE " + stage + " frame=" + sample + " simTime=" + Time.time.ToString("F3"));
                            if (stage != BuuRecoveryStage.Idle) stages.Add(stage);
                            previousStage = stage;
                        }
                        if (stage == BuuRecoveryStage.Grow && growCameraAge < 0f) growCameraAge = 0f;
                        if (growCameraAge >= 0f)
                        {
                            growCameraAge += 1f / fps;
                            float eased = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(growCameraAge / 2f));
                            captureCamera.orthographicSize = Mathf.Lerp(3.25f, 1.65f, eased);
                            var centre = DbpuFx.BodyBounds(body.Person).center + Vector3.up * .15f;
                            var desired = new Vector3(centre.x, Mathf.Max(floorY + .8f, centre.y), -10f);
                            captureCamera.transform.position = Vector3.Lerp(captureCamera.transform.position, desired, .065f + eased * .06f);
                        }
                        var previousTarget = captureCamera.targetTexture;
                        var previousActive = RenderTexture.active;
                        try
                        {
                            captureCamera.targetTexture = target;
                            captureCamera.Render();
                            RenderTexture.active = target;
                            image.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                            image.Apply();
                            File.WriteAllBytes(Path.Combine(directory, "frame-" + sample.ToString("D3") + ".png"), image.EncodeToPNG());
                        }
                        finally
                        {
                            captureCamera.targetTexture = previousTarget;
                            RenderTexture.active = previousActive;
                        }
                    })) yield break;
                }
            }
            finally
            {
                Time.captureFramerate = oldCaptureRate;
                if (image) Destroy(image);
                if (target) { target.Release(); Destroy(target); }
            }
            yield return new WaitForSeconds(.65f);
            if (!Check(() =>
            {
                Assert(body.Regen.RecoveryCount > before, "Recorded native reformation never completed");
                CheckConstructorComplete(body, construction);
                Assert(checkedAuthoredRest && checkedAuthoredQuiver, "Preview did not verify both static authored Rest and animated authored Quiver");
                Assert(stages.SequenceEqual(new[] { BuuRecoveryStage.Rest, BuuRecoveryStage.Quiver, BuuRecoveryStage.Gather, BuuRecoveryStage.Grow }),
                    "Preview did not show all physical recovery stages in order: " + string.Join(",", stages));
                CheckBody(body, "Preview full-body Crush reformed from its real scattered fragments");
                Log("PREVIEW CAPTURED " + frames + " frames at " + fps + " fps in " + directory);
            })) yield break;
            Destroy(body.Root);
            Finish(errors == 0);
        }

        float PreparePreviewFloor(Body body)
        {
            var from = new Vector2(body.Root.transform.position.x, body.Root.transform.position.y + 2f);
            var hit = Physics2D.RaycastAll(from, Vector2.down, 10000f).FirstOrDefault(candidate =>
                candidate.collider && !candidate.collider.isTrigger && candidate.normal.y > .65f
                && candidate.collider.transform.root != body.Root.transform.root
                && (!candidate.rigidbody || candidate.rigidbody.bodyType == RigidbodyType2D.Static));
            float referenceFloor;
            if (hit.collider)
            {
                referenceFloor = hit.point.y;
                Log("PREVIEW reference terrain " + hit.collider.name + " y=" + referenceFloor.ToString("G6"));
            }
            else referenceFloor = from.y - 4f;
            Assert(hit.collider, "A native terrain collider is required to select the correct fragment/ground collision layer");
            body.PreviewGroundLayer = hit.collider.gameObject.layer;

            // Always use a broad, flat native object. The map's raycast surface may
            // be a short ledge whose edge would send the real scatter into an abyss.
            var asset = ModAPI.FindSpawnable("Metal Cube");
            Assert(asset && asset.Prefab, "The native vanilla Metal Cube is required for the flat-ground preview fixture");
            var platform = Instantiate(asset.Prefab, new Vector3(from.x, referenceFloor + 1f, 0f), Quaternion.identity);
            platform.name = "QA native flat metal platform";
            platform.layer = body.PreviewGroundLayer;
            platform.AddComponent<DeregisterBehaviour>();
            foreach (var rb in platform.GetComponentsInChildren<Rigidbody2D>()) rb.bodyType = RigidbodyType2D.Static;
            Physics2D.SyncTransforms();
            var platformColliders = platform.GetComponentsInChildren<Collider2D>().Where(collider => !collider.isTrigger).ToArray();
            Assert(platformColliders.Length > 0, "Native Metal Cube platform has no physical surface");
            foreach (var collider in platformColliders) collider.gameObject.layer = body.PreviewGroundLayer;
            var bounds = platformColliders[0].bounds;
            foreach (var collider in platformColliders.Skip(1)) bounds.Encapsulate(collider.bounds);
            Assert(bounds.size.x > .001f && bounds.size.y > .001f, "Native metal platform has invalid collider bounds");
            platform.transform.localScale = Vector3.Scale(platform.transform.localScale,
                new Vector3(40f / bounds.size.x, 1f / bounds.size.y, 1f));
            Physics2D.SyncTransforms();
            float floorY = platformColliders.Max(collider => collider.bounds.max.y);
            platform.transform.position += Vector3.up * (referenceFloor + 1.25f - floorY);
            Physics2D.SyncTransforms();
            floorY = platformColliders.Max(collider => collider.bounds.max.y);
            Log("PREVIEW native flat Metal Cube platform width=40m height=1m y=" + floorY.ToString("G6") + " centred x=" + from.x.ToString("G6")
                + " terrainLayer=" + hit.collider.gameObject.layer + " platformLayers=" + string.Join(",", platformColliders.Select(collider => collider.gameObject.layer).Distinct()));
            float bottom = body.Limbs.Select(limb => ActualCollider(limb)).Where(collider => collider).Min(collider => collider.bounds.min.y);
            body.Root.transform.position += Vector3.up * (floorY + .18f - bottom);
            Physics2D.SyncTransforms();
            for (int i = 0; i < body.Limbs.Length; i++)
            {
                body.Limbs[i].PhysicalBehaviour.rigidbody.gravityScale = 1f;
                var blueprint = body.Regen.BodyBlueprint[i];
                blueprint.Gravity = 1f;
                body.Regen.BodyBlueprint[i] = blueprint;
            }
            return floorY;
        }

        IEnumerator WaitForStage(Body body, BuuRecoveryStage desired, string label)
        {
            float deadline = Time.time + 12f;
            float wallDeadline = Time.realtimeSinceStartup + 30f;
            while (body.Regen && body.Regen.Stage != desired && Time.time < deadline && Time.realtimeSinceStartup < wallDeadline)
                yield return null;
            Check(() => Assert(body.Regen && body.Regen.Stage == desired, label + ": did not enter " + desired));
        }

        IEnumerator CheckMovingAssembly()
        {
            for (int fixture = 0; fixture < 2; fixture++)
            {
                Body body = null;
                if (!Check(() => body = Spawn("Majin Buu", new Vector3(fixture * 25f, 40f, 0f)))) yield break;
                yield return new WaitForSeconds(1.1f);
                int before = body.Regen.RecoveryCount;
                if (!Check(() =>
                {
                    if (fixture == 0)
                    {
                        body.Limbs[0].PhysicalBehaviour.Disintegrate();
                        body.Limbs[13].PhysicalBehaviour.Disintegrate();
                    }
                    else foreach (var limb in body.Limbs)
                        if (limb != body.Limbs[6]) limb.PhysicalBehaviour.Disintegrate();
                })) yield break;
                yield return WaitForStage(body, BuuRecoveryStage.Grow, "Moving assembly fixture " + fixture);
                if (finished) yield break;
                var construction = new ConstructorAudit();
                var torso = body.Limbs[1].PhysicalBehaviour.rigidbody;
                var arm = body.Limbs[4].PhysicalBehaviour.rigidbody;
                Vector2 start = torso.position;
                float armAngle = Mathf.DeltaAngle(torso.rotation, arm.rotation), articulated = 0f;
                if (!Check(() =>
                {
                    Assert(body.Person.enabled, "Native person controller is disabled during assembly");
                    int walking = body.Person.Poses.FindIndex(pose => pose.State == PoseState.Walking);
                    Assert(walking >= 0, "Native walking pose is unavailable");
                    body.Person.OverridePoseIndex = walking;
                    foreach (var limb in body.Limbs)
                    {
                        var rb = limb.PhysicalBehaviour.rigidbody;
                        Assert(rb.simulated && rb.bodyType == RigidbodyType2D.Dynamic, "Assembling " + limb.name + " is frozen/kinematic");
                        Assert(!limb.HasJoint || (limb.Joint && limb.Joint.enabled && limb.Joint.connectedBody), "Assembly has no live native joints");
                        rb.velocity = Vector2.right * 3f;
                    }
                    arm.AddTorque(arm.inertia * 2f, ForceMode2D.Impulse);
                })) yield break;
                float until = Time.time + .6f;
                while (Time.time < until && body.Regen.Stage == BuuRecoveryStage.Grow)
                {
                    if (!Check(() => CheckConstructorFrame(body, construction, false))) yield break;
                    articulated = Mathf.Max(articulated, Mathf.Abs(Mathf.DeltaAngle(armAngle, Mathf.DeltaAngle(torso.rotation, arm.rotation))));
                    yield return null;
                }
                if (!Check(() =>
                {
                    Assert(body.Regen.Stage == BuuRecoveryStage.Grow, "Movement was only observed after assembly ended");
                    Log("MOVEMENT fixture=" + fixture + " displacement=" + (torso.position.x - start.x).ToString("F3")
                        + " articulated=" + articulated.ToString("F3") + " pose=" + body.Person.ActivePose.State
                        + " motor=" + body.Limbs[4].Joint.motor.motorSpeed + " constraints=" + arm.constraints);
                    Assert(torso.position.x - start.x > .5f, "Assembling body did not respond to physical motion");
                    Assert(body.Person.ActivePose.State == PoseState.Walking, "Native movement controller did not respond while assembling");
                    Assert(articulated > .5f, "Native limbs remained locked in the canonical pose");
                    Log("PASS moving assembly " + fixture + " translates " + (torso.position.x - start.x).ToString("F3")
                        + "m and articulates " + articulated.ToString("F2") + "deg while opaque pixel pieces are still arriving");
                })) yield break;
                body.Person.OverridePoseIndex = -1;

                Vector3[] pausedAt = body.Limbs.Select(limb => limb.transform.position).ToArray();
                float pausedAge = body.Regen.StageAge;
                if (!Global.main.Paused) Global.main.TogglePaused();
                Time.timeScale = 0f;
                yield return new WaitForSecondsRealtime(.25f);
                bool pauseOk = Check(() =>
                {
                    Assert(Mathf.Abs(body.Regen.StageAge - pausedAge) < .001f, "Paused moving assembly advanced");
                    for (int i = 0; i < pausedAt.Length; i++)
                        Assert(Vector3.Distance(body.Limbs[i].transform.position, pausedAt[i]) < .002f, "Paused assembly moved tissue");
                });
                if (Global.main.Paused) Global.main.TogglePaused();
                Time.timeScale = 1f;
                if (!pauseOk) yield break;
                Log("PASS moving assembly respects the native simulation pause");

                float deadline = Time.time + 8f, wallDeadline = Time.realtimeSinceStartup + 20f;
                Vector2 previous = torso.position;
                float previousSpeed = 0f;
                while (body.Regen.IsReforming && Time.time < deadline && Time.realtimeSinceStartup < wallDeadline)
                {
                    if (!Check(() => CheckConstructorFrame(body, construction, false))) yield break;
                    foreach (var limb in body.Limbs) limb.PhysicalBehaviour.rigidbody.velocity = Vector2.right * 2f;
                    previous = torso.position;
                    previousSpeed = torso.velocity.magnitude;
                    yield return null;
                }
                if (!Check(() =>
                {
                    Assert(!body.Regen.IsReforming && body.Regen.RecoveryCount > before, "Moving assembly did not finish");
                    Assert(Vector2.Distance(torso.position, previous) < .2f + previousSpeed * Mathf.Max(.02f, Time.deltaTime) * 3f,
                        "Completion teleported the moving body back to a canonical pose");
                    Assert(torso.velocity.x > .5f, "Completion erased the body's momentum");
                    CheckBody(body, "Moving assembly native body complete " + fixture);
                    Log("PASS assembly completion preserves body position and momentum");
                })) yield break;
                Destroy(body.Root);
                yield return null;
            }
        }

        IEnumerator CheckRecoveryTimeline()
        {
            Body body = null;
            if (!Check(() => body = Spawn("Kid Buu", new Vector3(0f, 40f, 0f)))) yield break;
            yield return new WaitForSeconds(1.1f);
            int before = body.Regen.RecoveryCount;
            if (!Check(() =>
            {
                foreach (var limb in body.Limbs) if (limb != body.Limbs[6]) limb.PhysicalBehaviour.Disintegrate();
            })) yield break;
            var expected = new[] { BuuRecoveryStage.Rest, BuuRecoveryStage.Quiver, BuuRecoveryStage.Gather, BuuRecoveryStage.Grow };
            var seen = new List<BuuRecoveryStage>();
            BuuRecoveryStage previous = BuuRecoveryStage.Idle;
            float entered = Time.time, damagedAt = Time.time;
            float deadline = damagedAt + 12f, wallDeadline = Time.realtimeSinceStartup + 30f;
            Vector2 quiverStart = Vector2.zero;
            float quiverAngle = 0f, quiverMotion = 0f;
            while (body.Regen && body.Regen.RecoveryCount == before && Time.time < deadline && Time.realtimeSinceStartup < wallDeadline)
            {
                if (!Check(() =>
                {
                    var stage = body.Regen.Stage;
                    if (stage != previous)
                    {
                        if (previous != BuuRecoveryStage.Idle)
                        {
                            float duration = Time.time - entered;
                            float minimum = previous == BuuRecoveryStage.Rest ? .95f : previous == BuuRecoveryStage.Quiver ? .65f : previous == BuuRecoveryStage.Gather ? 1.05f : 1.9f;
                            Assert(duration >= minimum, previous + " was skipped or prematurely ended after " + duration.ToString("F3") + " seconds");
                            Log("PASS physical recovery stage " + previous + " lasted " + duration.ToString("F3") + " seconds");
                        }
                        if (stage != BuuRecoveryStage.Idle)
                        {
                            Assert(seen.Count < expected.Length && stage == expected[seen.Count], "Recovery stage order changed: " + string.Join(",", seen) + " then " + stage);
                            seen.Add(stage);
                            if (stage == BuuRecoveryStage.Quiver)
                            {
                                quiverStart = body.Limbs[6].PhysicalBehaviour.rigidbody.position;
                                quiverAngle = body.Limbs[6].PhysicalBehaviour.rigidbody.rotation;
                            }
                        }
                        previous = stage;
                        entered = Time.time;
                        Log("TIMELINE " + stage + " at " + (Time.time - damagedAt).ToString("F3") + " seconds after destruction");
                    }
                    int active = body.Limbs.Count(limb => limb && limb.gameObject.activeInHierarchy && !limb.PhysicalBehaviour.isDisintegrated);
                    if (stage == BuuRecoveryStage.Rest || stage == BuuRecoveryStage.Quiver || stage == BuuRecoveryStage.Gather)
                    {
                        Assert(active == 1, stage + " resurrected new body tissue before physical gathering completed: " + active + " original limbs active");
                        var foot = body.Limbs[6];
                        Assert(foot.PhysicalBehaviour.rigidbody.simulated && ActualCollider(foot) && ActualCollider(foot).enabled,
                            stage + " made the genuine survivor untargetable/nonphysical");
                    }
                    if (stage == BuuRecoveryStage.Quiver)
                    {
                        var rb = body.Limbs[6].PhysicalBehaviour.rigidbody;
                        quiverMotion = Mathf.Max(quiverMotion, Vector2.Distance(rb.position, quiverStart) + Mathf.Abs(Mathf.DeltaAngle(quiverAngle, rb.rotation)) * .001f);
                    }
                    if (stage == BuuRecoveryStage.Grow)
                        Assert(active > 1, "Grow phase never activated any reconstructed native tissue");
                })) yield break;
                yield return null;
            }
            float completedAt = Time.time;
            yield return new WaitForSeconds(.65f);
            if (!Check(() =>
            {
                Assert(body.Regen && body.Regen.RecoveryCount > before, "Phased recovery did not finish before timeout");
                Assert(seen.SequenceEqual(expected), "Not all four recovery phases were observed");
                Assert(previous == BuuRecoveryStage.Grow && completedAt - entered >= 1.9f, "Native Grow phase finished too early");
                Log("PASS physical recovery stage Grow lasted " + (completedAt - entered).ToString("F3") + " seconds");
                Assert(quiverMotion > .0002f, "Quiver stage did not move genuine physical surviving matter");
                Assert(Time.time - damagedAt >= 5f, "Complete body returned before the requested physical recovery timeline");
                CheckBody(body, "Rest, physical quiver, gathering, growth, and native body completion");
            })) yield break;
            Destroy(body.Root);
            yield return null;
        }

        IEnumerator CheckPauseAndNativeCopy()
        {
            Body source = null, copy = null;
            if (!Check(() => source = Spawn("Majin Buu", new Vector3(0f, 40f, 0f)))) yield break;
            yield return new WaitForSeconds(1.1f);
            if (!Check(() => Slice(source, 0, 11, 13))) yield break;
            yield return new WaitForSeconds(.6f);
            Vector3[] positions = null;
            int count = 0;
            if (!Check(() =>
            {
                Assert(source.Regen.IsReforming, "Pause fixture did not enter recovery");
                Assert(source.Regen.Stage == BuuRecoveryStage.Rest, "Rest fixture entered growth before the inert aftermath elapsed");
                foreach (var limb in source.Limbs)
                {
                    limb.PhysicalBehaviour.rigidbody.velocity = Vector2.zero;
                    limb.PhysicalBehaviour.rigidbody.angularVelocity = 0f;
                }
                positions = source.Limbs.Select(limb => limb.transform.position).ToArray();
                count = source.Regen.RecoveryCount;
                if (!Global.main.Paused) Global.main.TogglePaused();
                Time.timeScale = 0f;
            })) yield break;
            yield return new WaitForSecondsRealtime(.45f);
            bool paused = Check(() =>
            {
                Assert(source.Regen.IsReforming && source.Regen.RecoveryCount == count, "Recovery completed while simulation time was paused");
                for (int i = 0; i < positions.Length; i++)
                    Assert(Vector3.Distance(source.Limbs[i].transform.position, positions[i]) < .002f, "Paused recovery moved native tissue");
                Log("PASS paused simulation holds recovery progress and tissue positions");
            });
            if (Global.main.Paused) Global.main.TogglePaused();
            Time.timeScale = 1f;
            if (!paused) yield break;
            if (!Check(() =>
            {
                var states = ObjectStateConverter.Convert(new[] { source.Root }, source.Root.transform.position);
                Assert(states.Length == 1, "Native copy did not create one state");
                var stored = states[0].RootComponentData.FirstOrDefault(component => component.Type == typeof(BuuRegeneration));
                Assert(stored != null && stored.storedFields.ContainsKey("BodyBlueprint"), "Native serializer omitted the body blueprint");
                Assert(!source.Regen.IsReforming, "OnBeforeSerialise left source physics in recovery mode");
                var copied = ObjectStateConverter.Convert(states, new Vector3(25f, 40f, 0f));
                Assert(copied.Length == 1 && copied[0], "Native deserialisation did not restore one fighter");
                subjects.Add(copied[0]);
                copy = Inspect(copied[0]);
                Assert(copy.Regen.BodyBlueprint != null && copy.Regen.BodyBlueprint.Length == source.Limbs.Length, "Native copy lost the regeneration blueprint");
                Assert(!ReferenceEquals(copy.Regen.ErasedMatter, source.Regen.ErasedMatter), "Native copy shares mutable matter flags with source");
            })) yield break;
            yield return new WaitForSeconds(1.1f);
            if (!Check(() => { CheckBody(source, "Source after native mid-recovery copy"); CheckBody(copy, "Native copied fighter intact"); })) yield break;
            count = copy.Regen.RecoveryCount;
            if (!Check(() => { copy.Limbs[0].PhysicalBehaviour.Disintegrate(); copy.Limbs[13].PhysicalBehaviour.Disintegrate(); })) yield break;
            yield return Recover(copy, count, "Native copied fighter independently regenerates missing head and hand");
            if (finished) yield break;
            if (!Check(() => CheckBody(source, "Native copy did not mutate source fighter"))) yield break;

            if (!Check(() => Slice(copy, 11))) yield break;
            yield return WaitForStage(copy, BuuRecoveryStage.Grow, "Native copy while growing");
            if (finished) yield break;
            Body growthCopy = null;
            if (!Check(() =>
            {
                var states = ObjectStateConverter.Convert(new[] { copy.Root }, copy.Root.transform.position);
                var roots = ObjectStateConverter.Convert(states, new Vector3(50f, 40f, 0f));
                Assert(roots.Length == 1 && roots[0], "Native mid-growth copy failed");
                subjects.Add(roots[0]);
                growthCopy = Inspect(roots[0]);
            })) yield break;
            yield return new WaitForSeconds(1.1f);
            if (!Check(() =>
            {
                CheckBody(copy, "Growth source resolves native physics before serialization");
                CheckBody(growthCopy, "Native copy during Grow preserves complete native topology");
            })) yield break;
            Destroy(growthCopy.Root);
            Destroy(source.Root); Destroy(copy.Root);
            yield return null;
        }

        IEnumerator CheckHakaiAndSpiritBomb()
        {
            Body erased = null;
            if (!Check(() => erased = Spawn("Super Buu", new Vector3(0f, 40f, 0f)))) yield break;
            yield return new WaitForSeconds(1.1f);
            if (!Check(() => Slice(erased, 0, 11))) yield break;
            yield return WaitForStage(erased, BuuRecoveryStage.Grow, "Hakai interruption fixture");
            if (finished) yield break;
            if (!Check(() =>
            {
                Assert(erased.Regen.IsReforming && erased.Regen.Stage == BuuRecoveryStage.Grow, "Hakai fixture did not enter active native body growth");
                HakaiDissolve.DissolvePerson(erased.Person);
                Assert(!erased.Regen.RegenerationEnabled, "Hakai did not suppress Majin regeneration");
            })) yield break;
            yield return new WaitForSeconds(3f);
            if (!Check(() =>
            {
                Assert(!erased.Root, "Native Hakai dissolution did not remove a reforming Majin");
                Log("PASS native Hakai interrupts active recovery and erases the whole fighter");
            })) yield break;

            Body caster = null, victim = null;
            SpiritBombProjectile bomb = null;
            MethodInfo hurt = null;
            if (!Check(() =>
            {
                caster = Spawn("Kid Buu", new Vector3(100f, 40f, 0f));
                victim = Spawn("Kid Buu", new Vector3(0f, 40f, 0f));
            })) yield break;
            yield return new WaitForSeconds(1.1f);
            if (!Check(() =>
            {
                // Exercise the production damage callback on a completely initialized native
                // projectile. Its controlled doses isolate damage thresholds from balancing.
                bomb = SpiritBombProjectile.Create(caster.Person, caster.Limbs[13]);
                Assert(bomb, "Native Spirit Bomb creation failed");
                bomb.AddEnergy(bomb.ReferenceKi * .05f, false);
                bomb.Prepare(); bomb.Launch(Vector2.up);
                bomb.enabled = false;
                hurt = typeof(SpiritBombProjectile).GetMethod("Hurt", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert(hurt != null && bomb.Phase == SpiritBombPhase.Flight, "Spirit Bomb did not reach its native flight phase");
                var limb = victim.Limbs[0];
                hurt.Invoke(bomb, new object[] { limb, limb.InitialHealth * 2f });
                Assert(limb.gameObject.activeInHierarchy && !limb.PhysicalBehaviour.isDisintegrated, "An ordinary Spirit Bomb hit erased Majin matter");
            })) yield break;
            yield return new WaitForSeconds(2f);
            if (!Check(() =>
            {
                CheckBody(victim, "Ordinary Spirit Bomb damage remains regenerable");
                bomb.Detonate();
                Assert(bomb.Phase == SpiritBombPhase.Detonating, "Spirit Bomb did not reach native final phase");
                var limb = victim.Limbs[1];
                hurt.Invoke(bomb, new object[] { limb, limb.InitialHealth * .2f });
                Assert(limb.gameObject.activeInHierarchy && !limb.PhysicalBehaviour.isDisintegrated, "A weak final Spirit Bomb erased Majin matter");
            })) yield break;
            yield return new WaitForSeconds(2f);
            if (!Check(() => CheckBody(victim, "Sublethal final Spirit Bomb remains regenerable"))) yield break;

            // A lethal health dose is still insufficient when the energy attack is
            // weaker than this Majin's native resistance. Verify that distinction.
            if (!Check(() =>
            {
                var limb = victim.Limbs[13];
                double ratio = SpiritBombAttackPower(bomb, limb) / EnergyAnnihilation.Resistance(limb);
                Assert(ratio <= EnergyAnnihilation.MinimumPowerRatio, "Weak Genki Dama fixture unexpectedly exceeds native matter resistance");
                hurt.Invoke(bomb, new object[] { limb, limb.InitialHealth * 2f });
                var erosion = limb.GetComponent<EnergyErosion>();
                Assert(!erosion || erosion.Progress < 1f, "Insufficient attack power committed a terminal energy erasure");
                Log("GENKI WEAK FIXTURE attack/resistance=" + ratio.ToString("G6"));
            })) yield break;
            yield return new WaitForSeconds(2f);
            if (!Check(() => CheckBody(victim, "Lethal health damage with insufficient Genki Dama power remains regenerable"))) yield break;

            // Use the real public ki-output multiplier in this disposable fixture.
            // The attack must qualify against every native limb's actual resistance.
            if (!Check(() =>
            {
                double weakestRatio = victim.Limbs.Min(limb => SpiritBombAttackPower(bomb, limb) / EnergyAnnihilation.Resistance(limb));
                var multipliers = caster.Root.GetComponent<MultipliersAndStuffs>();
                Assert(multipliers, "Native caster has no combat output multiplier");
                double factor = Math.Max(1d, 8d / Math.Max(1e-30d, weakestRatio));
                multipliers.KiOutput = KiCombatMath.Cap((double)multipliers.KiOutput * factor, 1e30f);
                multipliers.Recalculate();
                CombatPowerController.Bind(caster.Person).Refresh();
            })) yield break;
            // BeamPhysics caches the actual fighter output for .15 seconds.
            yield return new WaitForSeconds(.2f);
            if (!Check(() =>
            {
                foreach (var limb in victim.Limbs)
                {
                    double ratio = SpiritBombAttackPower(bomb, limb) / EnergyAnnihilation.Resistance(limb);
                    Assert(ratio > EnergyAnnihilation.MinimumPowerRatio, "Strong native Genki Dama fixture fails attack/resistance threshold on " + limb.name + ": " + ratio);
                }
                Log("GENKI STRONG FIXTURE minimum attack/resistance=" + victim.Limbs.Min(limb => SpiritBombAttackPower(bomb, limb) / EnergyAnnihilation.Resistance(limb)).ToString("G6"));
            })) yield break;
            int before = victim.Regen.RecoveryCount;
            Sprite originalHandSprite = victim.Limbs[13].PhysicalBehaviour.spriteRenderer.sprite;
            EnergyErosion terminal = null;
            if (!Check(() =>
            {
                var limb = victim.Limbs[13];
                hurt.Invoke(bomb, new object[] { limb, limb.InitialHealth * 2f });
                terminal = limb.GetComponent<EnergyErosion>();
                Assert(terminal && terminal.Progress >= .999f && terminal.Shown < 1f && !limb.PhysicalBehaviour.isDisintegrated,
                    "A qualifying final Spirit Bomb failed to begin progressive, non-instant native erasure");
            })) yield break;
            yield return new WaitForSeconds(.2f);
            if (!Check(() => Assert(terminal && terminal.Shown > 0f && terminal.Shown < 1f && !victim.Limbs[13].PhysicalBehaviour.isDisintegrated,
                "Native energy erasure did not expose an intermediate visible stage"))) yield break;
            yield return WaitForEnergyErasure(victim.Limbs[13], "Lethal final Spirit Bomb local progressive erasure");
            if (finished) yield break;
            yield return Recover(victim, before, "Fragment outside lethal final Spirit Bomb can regrow local erased tissue");
            if (finished) yield break;
            if (!Check(() =>
            {
                Assert(victim.Limbs[13].PhysicalBehaviour.spriteRenderer.sprite == originalHandSprite,
                    "Regeneration left the native hand using its eroded runtime sprite");
                var erosion = victim.Limbs[13].GetComponent<EnergyErosion>();
                Assert(!erosion || (!erosion.Erased && erosion.Progress <= .0001f && erosion.Shown <= .0001f),
                    "Regenerated native tissue retained terminal energy-erasure state");
                Log("PASS local progressive energy erasure restores original native sprite and clears erosion after regeneration");
            })) yield break;
            if (!Check(() =>
            {
                foreach (var limb in victim.Limbs)
                {
                    Assert(SpiritBombAttackPower(bomb, limb) / EnergyAnnihilation.Resistance(limb) > EnergyAnnihilation.MinimumPowerRatio,
                        "Final whole-body Genki Dama lost its qualifying native attack power");
                    hurt.Invoke(bomb, new object[] { limb, limb.InitialHealth * 2f });
                    var erosion = limb.GetComponent<EnergyErosion>();
                    Assert(erosion && erosion.Progress >= .999f && !limb.PhysicalBehaviour.isDisintegrated,
                        "Whole-body terminal energy dose erased a limb instantly or failed to commit");
                }
            })) yield break;
            yield return new WaitForSeconds(3f);
            if (!Check(() =>
            {
                Assert(!victim.Root || !victim.Root.GetComponentsInChildren<PhysicalBehaviour>().Any(p => p && !p.isDisintegrated), "Complete lethal final Spirit Bomb was regenerated");
                Log("PASS lethal final Spirit Bomb erases all Majin matter when no fragment survives");
            })) yield break;
            if (bomb) bomb.Vanish();
            Destroy(caster.Root);
            yield return null;
        }

        static double SpiritBombAttackPower(SpiritBombProjectile bomb, LimbBehaviour limb)
        {
            double released = bomb.Orb != null ? bomb.Orb.Power : DbpuFx.PowerOf(bomb.Owner)
                * Mathf.Clamp(.7f + Mathf.Sqrt(Mathf.Max(0f, bomb.Energy / Mathf.Max(1f, bomb.ReferenceKi))) * .7f, .7f, 3f);
            float falloff = SpiritBombMath.Falloff(Vector2.Distance(bomb.transform.position, limb.transform.position), bomb.Radius * 1.65f);
            return KiCombatMath.Cap(released * falloff);
        }

        IEnumerator WaitForEnergyErasure(LimbBehaviour limb, string label)
        {
            float deadline = Time.time + 2.5f, wallDeadline = Time.realtimeSinceStartup + 8f;
            while (limb && !limb.PhysicalBehaviour.isDisintegrated && Time.time < deadline && Time.realtimeSinceStartup < wallDeadline)
                yield return null;
            Check(() =>
            {
                Assert(limb && limb.PhysicalBehaviour.isDisintegrated, label + ": native progressive erosion did not finish before timeout");
                Log("PASS " + label + " reaches real physical disintegration after its visible sequence");
            });
        }

        void CheckConstructorFrame(Body body, ConstructorAudit audit, bool fullBody)
        {
            Assert(BuuAssemblyMotion.SourceReady, "Missing Blender-authored assembly motion");
            var view = body.Root.GetComponentInChildren<BuuAssemblyView>(true);
            Assert(view && view.PieceCounts != null && view.ArrivedCounts != null, "Native body growth has no piece constructor");
            Assert(view.PixelAlignedSeams && view.ExactPixelCoverage, "Constructor uses smooth cuts or does not cover actual opaque sprite pixels exactly once");
            Assert(view.SourceOpaquePixelCounts != null && view.CoveredPixelCounts != null && view.OverlappingPixelCounts != null,
                "Pixel-tear constructor exposes no source/coverage audit");
            if (audit.Arrived == null) audit.Arrived = new int[body.Limbs.Length];
            for (int i = 0; i < body.Limbs.Length; i++)
            {
                int count = body.Regen.AssemblyPieceCount(i), arrived = body.Regen.AssembledPieceCount(i);
                Assert(count == view.PieceCounts[i] && arrived == view.ArrivedCounts[i], "Core and view disagree on construction counts for " + body.Limbs[i].name);
                if (!fullBody && count == 0) continue;
                Assert(count >= 5 && count <= 15, "Rebuilt " + body.Limbs[i].name + " has " + count + " pieces; expected 5-15");
                Assert(view.SourceOpaquePixelCounts[i] > 0 && view.CoveredPixelCounts[i] == view.SourceOpaquePixelCounts[i] && view.OverlappingPixelCounts[i] == 0,
                    "Pixel-tear pieces omit or overlap opaque pixels on " + body.Limbs[i].name);
                Assert(arrived >= audit.Arrived[i] && arrived <= count, "Piece arrival count regressed or exceeded its limb budget");
                if (arrived > audit.Arrived[i]) audit.Progress = true;
                audit.Arrived[i] = arrived;
                var limb = body.Limbs[i];
                Assert(Vector3.Distance(limb.transform.localScale, body.Regen.BodyBlueprint[i].Scale) < .0001f,
                    "Native " + limb.name + " grows by scale interpolation instead of opaque piece construction");
                var renderer = limb.PhysicalBehaviour.spriteRenderer;
                Assert(renderer && Mathf.Abs(renderer.color.a - 1f) < .0001f, "Native limb uses alpha fading during construction");
                if (arrived < count)
                {
                    audit.Pending = true;
                    Assert(!renderer.enabled && !view.IsLimbComplete(i), "Whole " + limb.name + " appeared before its last piece arrived");
                    var piece = limb.transform.Find("Majin opaque sprite pieces " + limb.name);
                    Assert(piece && piece.GetComponent<MeshRenderer>() && piece.GetComponent<MeshRenderer>().enabled, "Pending limb has no visible individual sprite pieces");
                    var mesh = piece.GetComponent<MeshFilter>().sharedMesh;
                    Assert(mesh && mesh.vertexCount > 0 && mesh.colors.All(colour => Mathf.Abs(colour.a - 1f) < .0001f), "Flying construction pieces are not fully opaque");
                    Assert(Vector3.Distance(piece.localScale, Vector3.one) < .0001f, "Flying construction piece mesh uses scale fading");
                }
                else Assert(renderer.enabled && view.IsLimbComplete(i), "Completed limb did not switch to its native renderer");
            }
            if (fullBody)
            {
                int torso = Mathf.Max(view.PieceCounts[1], Mathf.Max(view.PieceCounts[2], view.PieceCounts[3]));
                int foot = Mathf.Max(view.PieceCounts[6], view.PieceCounts[9]);
                Assert(torso > foot && view.PieceCounts.Distinct().Count() > 1, "Piece budgets do not reflect visible torso/foot size");
            }
            audit.Frames++;
        }

        void CheckConstructorComplete(Body body, ConstructorAudit audit)
        {
            var view = body.Root.GetComponentInChildren<BuuAssemblyView>(true);
            Assert(audit.Frames > 2 && audit.Pending && audit.Progress, "No independent opaque piece arrivals were observed during native growth");
            Assert(view && view.IsComplete && !view.IsBuilding, "Piece constructor did not finish");
            for (int i = 0; i < body.Limbs.Length; i++)
            {
                Assert(view.PieceCounts[i] >= 5 && view.PieceCounts[i] <= 15 && view.ArrivedCounts[i] == view.PieceCounts[i], "A complete native body part is missing its required assembled pieces");
                Assert(body.Limbs[i].PhysicalBehaviour.spriteRenderer.enabled, "Native renderer remained hidden after all pieces arrived");
            }
            Log("PASS opaque pixel-tear constructor all14 native limbs, 5-15 size-dependent pieces, exact opaque coverage/no overlap, monotonic arrivals, full canonical scale, no alpha fading, complete native topology");
        }

        static float AuthoredClock(BuuFleshView view)
        {
            return (float)typeof(BuuFleshView).GetField("animationClock", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
        }

        static float AuthoredFrame(BuuFleshView view)
        {
            var block = new MaterialPropertyBlock();
            view.GetComponent<MeshRenderer>().GetPropertyBlock(block);
            return block.GetFloat("_Frame");
        }

        BuuFleshView CheckAuthoredFragment(PhysicalBehaviour fragment, string label)
        {
            Assert(BuuFleshAssets.Ready, label + ": Blender-authored flesh bundle is unavailable");
            Assert(fragment && fragment.rigidbody && fragment.GetComponent<Rigidbody2D>() == fragment.rigidbody,
                label + ": original physical fragment rigidbody was replaced");
            var view = fragment.GetComponentInChildren<BuuFleshView>();
            Assert(view && view.gameObject.activeInHierarchy, label + ": no authored Blender view attached to the physical fragment");
            Assert(view.GetComponentsInChildren<Rigidbody2D>(true).Length == 0 && view.GetComponentsInChildren<Collider2D>(true).Length == 0,
                label + ": authored appearance added replacement physics");
            var renderer = view.GetComponent<MeshRenderer>();
            Assert(renderer && renderer.enabled && renderer.sharedMaterial && renderer.sharedMaterial.shader && renderer.sharedMaterial.shader.isSupported,
                label + ": authored fragment renderer or supported shader is missing");
            Assert(renderer.sharedMaterial.shader.name == "Dragon Ball Singularity/Majin Living Flesh", label + ": wrong authored flesh shader");
            foreach (string property in new[] { "_FleshAtlas", "_NormalAtlas" })
            {
                var atlas = renderer.sharedMaterial.GetTexture(property);
                Assert(atlas && atlas.width == 3072 && atlas.height == 1024, label + ": wrong Blender atlas dimensions for " + property);
            }
            return view;
        }

        IEnumerator CheckAuthoredFragmentTimeline(Body body, PhysicalBehaviour fragment)
        {
            yield return WaitForStage(body, BuuRecoveryStage.Rest, "Authored fragment Rest fixture");
            if (finished) yield break;
            yield return new WaitForEndOfFrame();
            BuuFleshView view = null;
            float restClock = 0f;
            if (!Check(() =>
            {
                view = CheckAuthoredFragment(fragment, "Native Crush authored source");
                restClock = AuthoredClock(view);
                Assert(Mathf.Abs(AuthoredFrame(view)) < .00001f, "Authored Rest did not use the static Blender pose");
            })) yield break;
            yield return new WaitForSeconds(.25f);
            if (!Check(() =>
            {
                Assert(body.Regen.Stage == BuuRecoveryStage.Rest, "Authored fragment skipped its inert Rest period");
                Assert(Mathf.Abs(AuthoredFrame(view)) < .00001f && Mathf.Abs(AuthoredClock(view) - restClock) < .00001f,
                    "Blender flesh animation moved during the inert Rest phase");
                Log("PASS real physical Crush fragment uses supported Blender material, 3072x1024 albedo/normal atlases, and static Rest pose");
            })) yield break;
            yield return WaitForStage(body, BuuRecoveryStage.Quiver, "Authored fragment Quiver fixture");
            if (finished) yield break;
            yield return new WaitForSeconds(.18f);
            float quiverClock = 0f;
            if (!Check(() =>
            {
                quiverClock = AuthoredClock(view);
                Assert(body.Regen.Stage == BuuRecoveryStage.Quiver && quiverClock > restClock && AuthoredFrame(view) > 0f,
                    "Quiver did not animate the actual Blender-authored fragment frames");
            })) yield break;
            yield return new WaitForSeconds(.16f);
            if (!Check(() =>
            {
                Assert(AuthoredClock(view) > quiverClock, "Authored Quiver loop stopped advancing");
                CheckAuthoredFragment(fragment, "Animated native Quiver source");
                Log("PASS physical Quiver advances authored Blender frames without adding physics components");
            })) yield break;
        }

        PhysicalBehaviour[] CrushEntireBody(Body subject, bool stabilize = true)
        {
            bool previousFragments = UserPreferenceManager.Current.ProceduralFragments;
            bool previousGoreless = UserPreferenceManager.Current.GorelessMode;
            UserPreferenceManager.Current.ProceduralFragments = true;
            UserPreferenceManager.Current.GorelessMode = false;
            try
            {
                foreach (var limb in subject.Limbs)
                {
                    limb.ShatteredObjectChance = 1f;
                    limb.ImmuneToDamage = false;
                    limb.Crush();
                }
            }
            finally
            {
                UserPreferenceManager.Current.ProceduralFragments = previousFragments;
                UserPreferenceManager.Current.GorelessMode = previousGoreless;
            }
            var fragments = subject.Limbs.SelectMany(limb => limb.DestroyWith)
                .Where(part => part).Select(part => part.GetComponent<PhysicalBehaviour>())
                .Where(part => part && part.gameObject.activeInHierarchy && !part.isDisintegrated).Distinct().ToArray();
            Assert(fragments.Length > 0, "Native Crush produced no real physical fragments");
            Assert(subject.Limbs.All(limb => limb.PhysicalBehaviour.isDisintegrated), "Native Crush left an original limb alive");
            foreach (var fragment in fragments)
            {
                if (stabilize)
                {
                    fragment.rigidbody.gravityScale = 0f;
                    fragment.rigidbody.velocity = Vector2.zero;
                    fragment.rigidbody.angularVelocity = 0f;
                }
            }
            Log("Native full-body Crush fragments: " + fragments.Length);
            return fragments;
        }

        IEnumerator CheckNativeCrushFragments()
        {
            Body body = null;
            if (!Check(() =>
            {
                Assert(BuuFleshAssets.Ready, "Blender-authored flesh assets must load before native Crush QA; fallback visuals are insufficient");
                Assert(BuuAssemblyMotion.SourceReady, "Blender-authored assembly motion must load before native constructor QA");
                body = Spawn("Kid Buu", new Vector3(0f, 40f, 0f));
            })) yield break;
            yield return new WaitForSeconds(1.1f);
            for (int pass = 0; pass < 2; pass++)
            {
                PhysicalBehaviour[] fragments = null;
                int count = body.Regen.RecoveryCount;
                if (!Check(() =>
                {
                    fragments = CrushEntireBody(body);
                    // One actual, attackable shard is sufficient; no original limb survives.
                    for (int i = 1; i < fragments.Length; i++) fragments[i].Disintegrate();
                    Assert(fragments[0] && fragments[0].gameObject.activeInHierarchy, "Last physical fragment was removed");
                })) yield break;
                if (pass == 0)
                {
                    yield return CheckAuthoredFragmentTimeline(body, fragments[0]);
                    if (finished) yield break;
                }
                yield return Recover(body, count, "Native full-body Crush recovery from one physical shard, pass " + (pass + 1), true);
                if (finished) yield break;
                if (!Check(() =>
                {
                    Assert(fragments.All(fragment => !fragment || fragment.isDisintegrated || !fragment.gameObject.activeInHierarchy), "Gathered fragment duplicates remained after reformation");
                    Assert(body.Limbs.All(limb => limb.CurrentlyShattered == 0), "Native shattered-layer flags were not reset for subsequent attacks");
                    Log("PASS native fragment consumption and repeatable shattering");
                })) yield break;
            }
            Destroy(body.Root);
            yield return null;

            if (!Check(() => body = Spawn("Kid Buu", new Vector3(0f, 40f, 0f)))) yield break;
            yield return new WaitForSeconds(1.1f);
            if (!Check(() =>
            {
                var fragments = CrushEntireBody(body);
                foreach (var fragment in fragments) fragment.Disintegrate();
            })) yield break;
            yield return new WaitForSeconds(3f);
            if (!Check(() =>
            {
                Assert(!body.Root || !body.Root.GetComponentsInChildren<PhysicalBehaviour>().Any(p => p && !p.isDisintegrated), "Fully destroyed limbs and shards regenerated from nothing");
                Log("PASS destroying all native fragments prevents full-body Crush recovery");
            })) yield break;
        }
    }
}
