using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Unity.Mathematics;
using UnityEngine;

namespace DBPU
{
    // ---------------------------------------------------------------------------------
    // Policy network: 32 inputs -> 64 -> 64 -> 16 tactic weights, tanh hidden layers, weights
    // held in float4 blocks. It starts near zero on purpose: at first the hand-written priors
    // in SingularityMind decide, and what the network learns is the correction to them.
    // ---------------------------------------------------------------------------------
    public sealed class AiPolicyNet
    {
        public const int In = 32, Hidden = 64, Out = AiTactic.Count;
        public const int InBlocks = In / 4, HiddenBlocks = Hidden / 4;
        public const int WeightCount = Hidden * In + Hidden + Hidden * Hidden + Hidden + Out * Hidden + Out;

        readonly float4[] w1 = new float4[Hidden * InBlocks], w2 = new float4[Hidden * HiddenBlocks], w3 = new float4[Out * HiddenBlocks];
        readonly float[] b1 = new float[Hidden], b2 = new float[Hidden], b3 = new float[Out];
        // Scratch for the backward pass; main thread only.
        readonly float4[] d1 = new float4[HiddenBlocks], d2 = new float4[HiddenBlocks], h1 = new float4[HiddenBlocks], h2 = new float4[HiddenBlocks];
        readonly float[] logits = new float[Out];

        static uint Next(ref uint s) { s ^= s << 13; s ^= s >> 17; s ^= s << 5; return s; }
        static float Uniform(ref uint s) { return (Next(ref s) & 0xFFFFFF) / 8388608f - 1f; }

        public void Initialise(uint seed)
        {
            uint s = seed == 0 ? 0x9E3779B9u : seed;
            float l1 = Mathf.Sqrt(6f / (In + Hidden)), l2 = Mathf.Sqrt(6f / (Hidden + Hidden)), l3 = Mathf.Sqrt(6f / (Hidden + Out)) * .1f;
            for (int i = 0; i < w1.Length; i++) w1[i] = new float4(Uniform(ref s), Uniform(ref s), Uniform(ref s), Uniform(ref s)) * l1;
            for (int i = 0; i < w2.Length; i++) w2[i] = new float4(Uniform(ref s), Uniform(ref s), Uniform(ref s), Uniform(ref s)) * l2;
            for (int i = 0; i < w3.Length; i++) w3[i] = new float4(Uniform(ref s), Uniform(ref s), Uniform(ref s), Uniform(ref s)) * l3;
            Array.Clear(b1, 0, b1.Length); Array.Clear(b2, 0, b2.Length); Array.Clear(b3, 0, b3.Length);
        }

        public void Forward(float4[] x, float4[] hidden1, float4[] hidden2, float[] output)
        {
            for (int n = 0; n < Hidden; n++)
            {
                int o = n * InBlocks;
                float sum = b1[n]
                    + math.dot(w1[o], x[0]) + math.dot(w1[o + 1], x[1]) + math.dot(w1[o + 2], x[2]) + math.dot(w1[o + 3], x[3])
                    + math.dot(w1[o + 4], x[4]) + math.dot(w1[o + 5], x[5]) + math.dot(w1[o + 6], x[6]) + math.dot(w1[o + 7], x[7]);
                hidden1[n >> 2][n & 3] = math.tanh(sum);
            }
            for (int n = 0; n < Hidden; n++)
            {
                int o = n * HiddenBlocks; float sum = b2[n];
                for (int b = 0; b < HiddenBlocks; b += 4)
                    sum += math.dot(w2[o + b], hidden1[b]) + math.dot(w2[o + b + 1], hidden1[b + 1]) + math.dot(w2[o + b + 2], hidden1[b + 2]) + math.dot(w2[o + b + 3], hidden1[b + 3]);
                hidden2[n >> 2][n & 3] = math.tanh(sum);
            }
            for (int n = 0; n < Out; n++)
            {
                int o = n * HiddenBlocks; float sum = b3[n];
                for (int b = 0; b < HiddenBlocks; b += 4)
                    sum += math.dot(w3[o + b], hidden2[b]) + math.dot(w3[o + b + 1], hidden2[b + 1]) + math.dot(w3[o + b + 2], hidden2[b + 2]) + math.dot(w3[o + b + 3], hidden2[b + 3]);
                output[n] = sum;
            }
        }

        // One gradient ascent step on sum_i signal[i] * logit[i] for the input x. The whole gradient is
        // clipped to `clip` by its exact norm (each layer's gradient is an outer product, so the norm is
        // the product of two vector norms). Returns false when anything is not finite; nothing is written then.
        public bool Step(float4[] x, float[] signal, float rate, float clip)
        {
            Forward(x, h1, h2, logits);
            float n3 = 0f;
            for (int i = 0; i < Out; i++) n3 += signal[i] * signal[i];
            float4 x2 = float4.zero, a1 = float4.zero, a2 = float4.zero, e1 = float4.zero, e2 = float4.zero;
            for (int b = 0; b < HiddenBlocks; b++)
            {
                float4 back = float4.zero;
                for (int o = 0; o < Out; o++) back += w3[o * HiddenBlocks + b] * signal[o];
                d2[b] = back * (1f - h2[b] * h2[b]);
                a2 += h2[b] * h2[b]; e2 += d2[b] * d2[b];
            }
            for (int b = 0; b < HiddenBlocks; b++)
            {
                float4 back = float4.zero;
                for (int n = 0; n < Hidden; n++) back += w2[n * HiddenBlocks + b] * d2[n >> 2][n & 3];
                d1[b] = back * (1f - h1[b] * h1[b]);
                a1 += h1[b] * h1[b]; e1 += d1[b] * d1[b];
            }
            for (int b = 0; b < InBlocks; b++) x2 += x[b] * x[b];
            float nh2 = math.csum(a2), nh1 = math.csum(a1), nx = math.csum(x2), nd2 = math.csum(e2), nd1 = math.csum(e1);
            float norm = Mathf.Sqrt(n3 * (nh2 + 1f) + nd2 * (nh1 + 1f) + nd1 * (nx + 1f));
            if (float.IsNaN(norm) || float.IsInfinity(norm)) return false;
            float scale = rate * (norm > clip ? clip / norm : 1f);
            if (scale == 0f) return true;
            for (int o = 0; o < Out; o++)
            {
                float g = signal[o] * scale; int at = o * HiddenBlocks;
                if (g == 0f) continue;
                for (int b = 0; b < HiddenBlocks; b++) w3[at + b] += h2[b] * g;
                b3[o] += g;
            }
            for (int n = 0; n < Hidden; n++)
            {
                float g = d2[n >> 2][n & 3] * scale; int at = n * HiddenBlocks;
                for (int b = 0; b < HiddenBlocks; b++) w2[at + b] += h1[b] * g;
                b2[n] += g;
            }
            for (int n = 0; n < Hidden; n++)
            {
                float g = d1[n >> 2][n & 3] * scale; int at = n * InBlocks;
                for (int b = 0; b < InBlocks; b++) w1[at + b] += x[b] * g;
                b1[n] += g;
            }
            return true;
        }

        public bool Finite()
        {
            float4 sum = float4.zero;
            for (int i = 0; i < w1.Length; i++) sum += w1[i];
            for (int i = 0; i < w2.Length; i++) sum += w2[i];
            for (int i = 0; i < w3.Length; i++) sum += w3[i];
            float total = math.csum(sum);
            for (int i = 0; i < Hidden; i++) total += b1[i] + b2[i];
            for (int i = 0; i < Out; i++) total += b3[i];
            return !float.IsNaN(total) && !float.IsInfinity(total);
        }

        public void CopyTo(float[] flat)
        {
            int k = 0;
            for (int i = 0; i < w1.Length; i++) { flat[k++] = w1[i].x; flat[k++] = w1[i].y; flat[k++] = w1[i].z; flat[k++] = w1[i].w; }
            for (int i = 0; i < Hidden; i++) flat[k++] = b1[i];
            for (int i = 0; i < w2.Length; i++) { flat[k++] = w2[i].x; flat[k++] = w2[i].y; flat[k++] = w2[i].z; flat[k++] = w2[i].w; }
            for (int i = 0; i < Hidden; i++) flat[k++] = b2[i];
            for (int i = 0; i < w3.Length; i++) { flat[k++] = w3[i].x; flat[k++] = w3[i].y; flat[k++] = w3[i].z; flat[k++] = w3[i].w; }
            for (int i = 0; i < Out; i++) flat[k++] = b3[i];
        }

        public void CopyFrom(float[] flat)
        {
            int k = 0;
            for (int i = 0; i < w1.Length; i++) { w1[i] = new float4(flat[k], flat[k + 1], flat[k + 2], flat[k + 3]); k += 4; }
            for (int i = 0; i < Hidden; i++) b1[i] = flat[k++];
            for (int i = 0; i < w2.Length; i++) { w2[i] = new float4(flat[k], flat[k + 1], flat[k + 2], flat[k + 3]); k += 4; }
            for (int i = 0; i < Hidden; i++) b2[i] = flat[k++];
            for (int i = 0; i < w3.Length; i++) { w3[i] = new float4(flat[k], flat[k + 1], flat[k + 2], flat[k + 3]); k += 4; }
            for (int i = 0; i < Out; i++) b3[i] = flat[k++];
        }
    }

    // One decision the brain took, kept so that reward, coaching and rollback can refer to it.
    public sealed class AiDecision
    {
        public readonly float4[] X = new float4[AiPolicyNet.InBlocks];
        public readonly float[] Probability = new float[AiTactic.Count];
        public int Action = -1; public float At, Temperature = 1f;
    }

    // ---------------------------------------------------------------------------------
    // What the AI has learned against one opponent character: a policy network and the
    // plain numbers a person can read (how often each tactic was used and how it paid).
    // ---------------------------------------------------------------------------------
    public sealed class AiBrainMemory
    {
        public readonly string OpponentId;
        public readonly AiPolicyNet Net = new AiPolicyNet();
        readonly float[] snapshot = new float[AiPolicyNet.WeightCount], signal = new float[AiTactic.Count];
        public readonly float[] Payoff = new float[AiTactic.Count];   // running advantage each tactic earned
        public readonly int[] Uses = new int[AiTactic.Count];
        public int Updates, Matches, Wins, Losses, Demonstrations, Coachings, Rollbacks;
        public float Baseline;
        public bool Dirty;
        int sinceSnapshot;

        public AiBrainMemory(string opponentId)
        {
            OpponentId = opponentId ?? "";
            Net.Initialise(AiMemoryStore.Hash(OpponentId));
            Net.CopyTo(snapshot);
        }

        public void Forget()
        {
            Net.Initialise(AiMemoryStore.Hash(OpponentId));
            Net.CopyTo(snapshot);
            Array.Clear(Payoff, 0, Payoff.Length); Array.Clear(Uses, 0, Uses.Length);
            Updates = Matches = Wins = Losses = Demonstrations = Coachings = Rollbacks = 0; Baseline = 0f; sinceSnapshot = 0; Dirty = true;
        }

        // Policy gradient for a contextual bandit: raise log pi(action) in proportion to the advantage.
        public void Reinforce(AiDecision decision, float advantage, float rate)
        {
            if (decision == null || decision.Action < 0) return;
            advantage = Mathf.Clamp(advantage, -3f, 3f);
            float inverse = 1f / Mathf.Max(.2f, decision.Temperature);
            for (int i = 0; i < signal.Length; i++) signal[i] = ((i == decision.Action ? 1f : 0f) - decision.Probability[i]) * advantage * inverse;
            Payoff[decision.Action] = Mathf.Lerp(Payoff[decision.Action], advantage, .05f);
            Apply(decision.X, rate);
        }

        // Imitation: move towards the tactic a demonstrator used in this situation (cross-entropy step).
        public void Imitate(AiDecision decision, int tactic, float rate)
        {
            if (decision == null || tactic < 0 || tactic >= AiTactic.Count) return;
            for (int i = 0; i < signal.Length; i++) signal[i] = (i == tactic ? 1f : 0f) - decision.Probability[i];
            Demonstrations++;
            Apply(decision.X, rate);
        }

        void Apply(float4[] x, float rate)
        {
            // The step decays with experience, so late updates refine instead of overwrite.
            float step = rate / (1f + Updates * .0015f);
            bool ok = Net.Step(x, signal, step, 1f);
            Updates++; Dirty = true;
            if (ok && ++sinceSnapshot >= 32)
            {
                sinceSnapshot = 0;
                if (Net.Finite()) { Net.CopyTo(snapshot); return; }
                ok = false;
            }
            if (!ok) { Net.CopyFrom(snapshot); Rollbacks++; sinceSnapshot = 0; }
        }

        public void CopyWeights(float[] flat) { Net.CopyTo(flat); }
        public void LoadWeights(float[] flat) { Net.CopyFrom(flat); if (!Net.Finite()) Net.CopyFrom(snapshot); else Net.CopyTo(snapshot); Dirty = true; }
    }

    // Mods/DBPU_AutonomousMemory.json: one entry per opponent character.
    public static class AiMemoryStore
    {
        static readonly Dictionary<string, AiBrainMemory> memories = new Dictionary<string, AiBrainMemory>();
        static readonly float[] flat = new float[AiPolicyNet.WeightCount];
        static bool loaded;

        static string FilePath { get { return Path.Combine(AutonomousConfig.Folder, "DBPU_AutonomousMemory.json"); } }

        public static uint Hash(string text)
        {
            uint h = 2166136261u;
            if (text != null) for (int i = 0; i < text.Length; i++) { h ^= text[i]; h *= 16777619u; }
            return h == 0 ? 1u : h;
        }

        public static AiBrainMemory For(string opponentId)
        {
            if (string.IsNullOrEmpty(opponentId)) opponentId = "Unknown";
            AiBrainMemory memory;
            if (!memories.TryGetValue(opponentId, out memory)) { memory = new AiBrainMemory(opponentId); memories[opponentId] = memory; }
            return memory;
        }

        public static void Load()
        {
            if (loaded) return;
            loaded = true;
            if (!AutonomousConfig.PersistMemory) return;
            try
            {
                if (string.IsNullOrEmpty(AutonomousConfig.Folder) || !File.Exists(FilePath)) return;
                var rootObject = JObject.Parse(File.ReadAllText(FilePath));
                var opponents = rootObject["opponents"] as JObject;
                if (opponents == null) return;
                foreach (var pair in opponents)
                {
                    var entry = pair.Value as JObject;
                    if (entry == null) continue;
                    var memory = For(pair.Key);
                    memory.Updates = (int?)entry["updates"] ?? 0; memory.Matches = (int?)entry["matches"] ?? 0;
                    memory.Wins = (int?)entry["wins"] ?? 0; memory.Losses = (int?)entry["losses"] ?? 0;
                    memory.Demonstrations = (int?)entry["demonstrations"] ?? 0; memory.Coachings = (int?)entry["coaching"] ?? 0;
                    memory.Baseline = (float?)entry["baseline"] ?? 0f;
                    var uses = entry["uses"] as JObject; var payoff = entry["payoff"] as JObject;
                    for (int i = 0; i < AiTactic.Count; i++)
                    {
                        if (uses != null) memory.Uses[i] = (int?)uses[AiTactic.Names[i]] ?? 0;
                        if (payoff != null) memory.Payoff[i] = (float?)payoff[AiTactic.Names[i]] ?? 0f;
                    }
                    string weights = (string)entry["weights"];
                    if (string.IsNullOrEmpty(weights)) continue;
                    byte[] bytes = Convert.FromBase64String(weights);
                    if (bytes.Length != AiPolicyNet.WeightCount * 4) continue;      // written by another network shape
                    Buffer.BlockCopy(bytes, 0, flat, 0, bytes.Length);
                    memory.LoadWeights(flat);
                    memory.Dirty = false;
                }
            }
            catch (Exception ex) { Debug.LogWarning("[Singularity] AI memory load: " + ex.Message); }
        }

        public static void Save()
        {
            if (!AutonomousConfig.PersistMemory || !AutonomousConfig.CanWrite) return;
            bool dirty = false;
            foreach (var pair in memories) if (pair.Value.Dirty) { dirty = true; break; }
            if (!dirty) return;
            try
            {
                var opponents = new JObject();
                var bytes = new byte[AiPolicyNet.WeightCount * 4];
                foreach (var pair in memories)
                {
                    var m = pair.Value;
                    var uses = new JObject(); var payoff = new JObject();
                    for (int i = 0; i < AiTactic.Count; i++) { uses[AiTactic.Names[i]] = m.Uses[i]; payoff[AiTactic.Names[i]] = Math.Round(m.Payoff[i], 4); }
                    m.CopyWeights(flat);
                    Buffer.BlockCopy(flat, 0, bytes, 0, bytes.Length);
                    opponents[pair.Key] = new JObject
                    {
                        ["updates"] = m.Updates, ["matches"] = m.Matches, ["wins"] = m.Wins, ["losses"] = m.Losses,
                        ["demonstrations"] = m.Demonstrations, ["coaching"] = m.Coachings, ["baseline"] = Math.Round(m.Baseline, 4),
                        ["uses"] = uses, ["payoff"] = payoff, ["weights"] = Convert.ToBase64String(bytes)
                    };
                    m.Dirty = false;
                }
                var rootObject = new JObject
                {
                    ["version"] = 1,
                    ["note"] = "Singularity Mind memory. uses/payoff are readable; weights is the policy network (32-64-64-16, little-endian floats).",
                    ["opponents"] = opponents
                };
                File.WriteAllText(FilePath, rootObject.ToString());
            }
            catch (Exception ex) { Debug.LogWarning("[Singularity] AI memory save: " + ex.Message); }
        }

        public static string Describe(AiBrainMemory m)
        {
            if (m == null) return "No memory yet.";
            int a = -1, b = -1, total = 0;
            for (int i = 0; i < AiTactic.Count; i++)
            {
                total += m.Uses[i];
                if (m.Uses[i] <= 0) continue;
                if (a < 0 || m.Payoff[i] > m.Payoff[a]) { b = a; a = i; }
                else if (b < 0 || m.Payoff[i] > m.Payoff[b]) b = i;
            }
            string best = a < 0 ? "nothing yet" : AiTactic.Names[a] + (b >= 0 ? ", " + AiTactic.Names[b] : "");
            return "<color=#FFD25A>AI memory vs " + m.OpponentId + "</color>: " + m.Matches + " fights (" + m.Wins + " won, " + m.Losses + " lost), "
                + m.Updates + " lessons, " + m.Demonstrations + " from watching you, " + m.Coachings + " from coaching, " + total + " decisions. Works best: " + best + ".";
        }
    }

    // ---------------------------------------------------------------------------------
    // Planner. A background task looks six steps of 0.2 s ahead in a point-mass model of the
    // fight (distance, health, ki, stamina, cooldowns) and reports how good each tactic looks
    // as the next move. The first two moves are a small UCT tree; the four moves after them
    // are a tail kept alive by rolling-horizon evolution. The task only ever touches the
    // plain numbers below. The brain publishes a snapshot and reads back sixteen bonuses.
    // ---------------------------------------------------------------------------------
    public struct AiPlanState
    {
        public float Distance, Closing, Health, FoeHealth, Ki, Stamina, StopRisk, Reach, BarrageReach, Speed, FoeSpeed, FoeRate, Threat;
        public float MeleeGain, BlastGain, BeamGain, BarrageGain, FistGain, Aggression;
        public int Mask, NextFormGain;          // bit i set when tactic i is available now
    }

    public sealed class AiPlanSlot
    {
        public const int Horizon = 6;
        public const float StepSeconds = .2f;
        internal int InUse, SeqIn, VersionIn, VersionDone, SeqOut;
        internal AiPlanState StateIn;
        internal readonly float[] Bonus = new float[AiTactic.Count];
        internal readonly byte[] Best = new byte[Horizon];
        // Worker-side scratch.
        internal readonly float[] n1 = new float[AiTactic.Count], q1 = new float[AiTactic.Count];
        internal readonly float[] n2 = new float[AiTactic.Count * AiTactic.Count], q2 = new float[AiTactic.Count * AiTactic.Count];
        internal readonly byte[] tail = new byte[Horizon], trial = new byte[Horizon], work = new byte[Horizon];
        internal readonly float[] bonusWork = new float[AiTactic.Count];
        internal uint rng = 0x1234567u;
        public int Plans { get; internal set; }
        public int Faults { get; internal set; }

        public void Publish(ref AiPlanState state)
        {
            Interlocked.Increment(ref SeqIn);
            StateIn = state;
            VersionIn++;
            Interlocked.Increment(ref SeqIn);
        }

        // Copies the latest finished plan. False while the worker is in the middle of writing one.
        public bool Read(float[] bonus, byte[] best)
        {
            int before = Volatile.Read(ref SeqOut);
            if ((before & 1) != 0) return false;
            for (int i = 0; i < bonus.Length; i++) bonus[i] = Bonus[i];
            if (best != null) for (int i = 0; i < Horizon && i < best.Length; i++) best[i] = Best[i];
            return Volatile.Read(ref SeqOut) == before;
        }
    }

    public static class AiPlannerService
    {
        const int SlotCount = 12;
        static readonly AiPlanSlot[] slots = new AiPlanSlot[SlotCount];
        static CancellationTokenSource cancel; static Task worker;
        static readonly object gate = new object();

        static AiPlannerService() { for (int i = 0; i < slots.Length; i++) slots[i] = new AiPlanSlot(); }

        public static AiPlanSlot Acquire()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (Interlocked.CompareExchange(ref slots[i].InUse, 1, 0) != 0) continue;
                Array.Clear(slots[i].Bonus, 0, AiTactic.Count);
                slots[i].VersionDone = slots[i].VersionIn;
                EnsureRunning();
                return slots[i];
            }
            return null;        // more brains than slots: the extra ones simply plan without look-ahead
        }

        public static void Release(AiPlanSlot slot)
        {
            if (slot == null) return;
            Volatile.Write(ref slot.InUse, 0);
            bool any = false;
            for (int i = 0; i < slots.Length; i++) if (Volatile.Read(ref slots[i].InUse) != 0) any = true;
            if (!any) Stop();
        }

        static void EnsureRunning()
        {
            lock (gate)
            {
                if (worker != null && !worker.IsCompleted) return;
                cancel = new CancellationTokenSource();
                var token = cancel.Token;
                worker = Task.Factory.StartNew(() => Loop(token), token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }
        }

        public static void Stop()
        {
            lock (gate)
            {
                if (cancel != null)
                {
                    try { cancel.Cancel(); }
                    catch (Exception ex) { Debug.LogWarning("[Singularity] AI planner stop: " + ex.Message); }
                    cancel = null;
                }
                worker = null;
            }
        }

        static void Loop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                bool worked = false;
                for (int i = 0; i < slots.Length && !token.IsCancellationRequested; i++)
                {
                    var slot = slots[i];
                    if (Volatile.Read(ref slot.InUse) == 0) continue;
                    AiPlanState state; int version;
                    // Seqlock read: retry until a snapshot was copied with no write in between.
                    int tries = 0;
                    while (true)
                    {
                        int before = Volatile.Read(ref slot.SeqIn);
                        state = slot.StateIn; version = slot.VersionIn;
                        if ((before & 1) == 0 && Volatile.Read(ref slot.SeqIn) == before) break;
                        if (++tries > 64) { version = slot.VersionDone; break; }
                    }
                    if (version == slot.VersionDone) continue;
                    // A fault in one plan must not end the worker; the brain just keeps its last bonuses.
                    try { Plan(slot, ref state); } catch (Exception) { slot.Faults++; }
                    slot.VersionDone = version;
                    worked = true;
                }
                if (!worked) token.WaitHandle.WaitOne(12);
            }
        }

        static uint Next(ref uint s) { s ^= s << 13; s ^= s >> 17; s ^= s << 5; return s; }
        static float Unit(ref uint s) { return (Next(ref s) & 0xFFFFFF) / 16777216f; }

        static byte Pick(ref uint s, int mask)
        {
            for (int tries = 0; tries < 24; tries++)
            {
                int a = (int)(Next(ref s) % AiTactic.Count);
                if ((mask & (1 << a)) != 0) return (byte)a;
            }
            return AiTactic.FlightIntercept;
        }

        static void Plan(AiPlanSlot slot, ref AiPlanState s)
        {
            int mask = s.Mask == 0 ? 1 : s.Mask;
            Array.Clear(slot.n1, 0, slot.n1.Length); Array.Clear(slot.q1, 0, slot.q1.Length);
            Array.Clear(slot.n2, 0, slot.n2.Length); Array.Clear(slot.q2, 0, slot.q2.Length);
            // Rolling horizon: last plan's tail, moved up one step, seeds this one.
            for (int i = 0; i < AiPlanSlot.Horizon - 1; i++) slot.tail[i] = slot.Best[i + 1];
            slot.tail[AiPlanSlot.Horizon - 1] = Pick(ref slot.rng, mask);
            for (int i = 0; i < AiPlanSlot.Horizon; i++) if ((mask & (1 << slot.tail[i])) == 0) slot.tail[i] = Pick(ref slot.rng, mask);
            float bestValue = float.MinValue, total = 0f;
            const int Simulations = 160;
            for (int sim = 0; sim < Simulations; sim++)
            {
                // Selection (UCT) for the first two moves.
                int a1 = Select(slot.n1, slot.q1, 0, mask, sim + 1, ref slot.rng);
                int a2 = Select(slot.n2, slot.q2, a1 * AiTactic.Count, mask, (int)slot.n1[a1] + 1, ref slot.rng);
                slot.trial[0] = (byte)a1; slot.trial[1] = (byte)a2;
                // Evolution for the rest: the best tail so far with a mutation, sometimes a fresh one.
                for (int i = 2; i < AiPlanSlot.Horizon; i++)
                {
                    float roll = Unit(ref slot.rng);
                    slot.trial[i] = roll < .3f ? Pick(ref slot.rng, mask) : slot.tail[i];
                }
                float value = Rollout(ref s, slot.trial, mask);
                slot.n1[a1] += 1f; slot.q1[a1] += (value - slot.q1[a1]) / slot.n1[a1];
                int child = a1 * AiTactic.Count + a2;
                slot.n2[child] += 1f; slot.q2[child] += (value - slot.q2[child]) / slot.n2[child];
                total += value;
                if (value > bestValue)
                {
                    bestValue = value;
                    for (int i = 0; i < AiPlanSlot.Horizon; i++) { slot.work[i] = slot.trial[i]; slot.tail[i] = slot.trial[i]; }
                }
            }
            float mean = total / Simulations, spread = 1e-4f;
            for (int a = 0; a < AiTactic.Count; a++) if (slot.n1[a] > 0f) spread = Mathf.Max(spread, Mathf.Abs(slot.q1[a] - mean));
            for (int a = 0; a < AiTactic.Count; a++)
                slot.bonusWork[a] = slot.n1[a] > 0f ? Mathf.Clamp((slot.q1[a] - mean) / spread, -1f, 1f) * 1.2f : 0f;
            Interlocked.Increment(ref slot.SeqOut);
            for (int a = 0; a < AiTactic.Count; a++) slot.Bonus[a] = slot.bonusWork[a];
            for (int i = 0; i < AiPlanSlot.Horizon; i++) slot.Best[i] = slot.work[i];
            Interlocked.Increment(ref slot.SeqOut);
            slot.Plans++;
        }

        static int Select(float[] visits, float[] values, int offset, int mask, int parentVisits, ref uint rng)
        {
            int best = -1; float bestScore = float.MinValue;
            float log = Mathf.Log(Mathf.Max(2, parentVisits));
            for (int a = 0; a < AiTactic.Count; a++)
            {
                if ((mask & (1 << a)) == 0) continue;
                float n = visits[offset + a];
                float score = n <= 0f ? 1e6f + Unit(ref rng) : values[offset + a] + .7f * Mathf.Sqrt(log / n);
                if (score > bestScore) { bestScore = score; best = a; }
            }
            return best < 0 ? 0 : best;
        }

        // The point-mass fight model. Values are shares of health: positive is good for this fighter.
        static float Rollout(ref AiPlanState s, byte[] plan, int mask)
        {
            const float dt = AiPlanSlot.StepSeconds;
            float distance = s.Distance, health = s.Health, foe = s.FoeHealth, ki = s.Ki, stamina = s.Stamina, stop = s.StopRisk;
            float boost = 1f, value = 0f, discount = 1f; int charge = 0, flank = 0, stall = 0;
            for (int step = 0; step < plan.Length; step++)
            {
                int a = plan[step];
                float dealt = 0f, guard = 1f, moved = 0f;
                if (stall > 0) { stall--; a = -1; }
                bool tired = stamina < .2f;
                switch (a)
                {
                    case AiTactic.FlightIntercept:
                        moved = -s.Speed * dt; stamina -= .01f; break;
                    case AiTactic.MeleeCombo:
                        if (distance <= s.Reach * 1.25f) { dealt = s.MeleeGain * boost * (1f - stop) * (tired ? .5f : 1f) * (flank > 0 ? 1.3f : 1f); if (stop > .5f) guard = 1.3f; stamina -= .07f; }
                        else { moved = -s.Speed * dt * .7f; stamina -= .02f; }
                        break;
                    case AiTactic.AfterimageFlank:
                        if (tired) break;
                        moved = -s.Speed * dt * 1.5f; stamina -= .2f; flank = 2; guard = .7f; break;
                    case AiTactic.EvadeDash:
                        if (tired) break;
                        moved = 2.2f; stamina -= .15f; guard = .2f; break;
                    case AiTactic.EvadeWeave: guard = distance < s.Reach * 1.6f ? .5f : .9f; stamina -= .02f; break;
                    case AiTactic.GuardBlock: guard = .45f; stamina -= .02f; break;
                    case AiTactic.Parry: guard = distance < s.Reach * 1.6f ? .35f : 1f; dealt = distance < s.Reach * 1.3f ? s.MeleeGain * .25f : 0f; stamina -= .04f; break;
                    case AiTactic.KiShield: guard = .1f; ki -= .05f; break;
                    case AiTactic.KiGuard: guard = s.Threat > .2f ? .25f : 1f; ki -= .02f; break;
                    case AiTactic.KiBlastVolley:
                        if (ki > .05f && distance > 1.5f) { dealt = s.BlastGain * Mathf.Clamp01(1.15f - distance / 30f); ki -= .03f; }
                        break;
                    case AiTactic.ChargedBeam:
                        if (ki < .18f) break;
                        charge++; guard = 1.4f;
                        if (charge >= 2 && distance > 3f) { dealt = s.BeamGain; ki -= .15f; charge = 0; }
                        break;
                    case AiTactic.Barrage:
                        if (distance <= s.BarrageReach) { dealt = s.BarrageGain * boost; stamina -= .1f; }
                        else moved = -s.Speed * dt * .7f;
                        break;
                    case AiTactic.KiFist:
                        if (ki > .08f && distance <= s.Reach * 1.25f) { dealt = s.FistGain * boost; ki -= .05f; stamina -= .06f; }
                        else moved = -s.Speed * dt * .7f;
                        break;
                    case AiTactic.DisengageCharge:
                        moved = s.Speed * dt * .6f; ki += .08f; stamina += .05f; guard = distance > 6f ? .8f : 1.2f; break;
                    case AiTactic.Kaioken:
                        if (boost < 1.4f) { boost = 1.5f; stop *= .5f; }
                        ki -= .03f; break;
                    case AiTactic.Transform:
                        if (s.NextFormGain > 0 && ki > .1f) { boost = Mathf.Max(boost, 2f); stop = 0f; ki -= .1f; stall = 1; guard = 1.2f; }
                        break;
                }
                if (a != AiTactic.ChargedBeam) charge = 0;
                if (flank > 0) flank--;
                // The opponent keeps coming and keeps hitting; how much lands depends on range and on the defence chosen.
                distance = Mathf.Max(.3f, distance + moved - s.Aggression * s.FoeSpeed * dt * .5f);
                float incoming = s.FoeRate * dt * guard * (distance < s.Reach * 1.5f ? 1f : Mathf.Lerp(.55f, .2f, Mathf.Clamp01(distance / 20f)));
                foe -= dealt; health -= incoming;
                ki = Mathf.Clamp01(ki + .004f); stamina = Mathf.Clamp01(stamina + .02f);
                value += discount * (dealt - incoming * 1.1f);
                // Spending below empty is not a plan.
                if (ki <= 0f || stamina <= 0f) value -= discount * .02f;
                discount *= .9f;
                if (health <= 0f) { value -= 1f; break; }
                if (foe <= 0f) { value += 1f; break; }
            }
            return value + (ki - s.Ki) * .03f + (stamina - s.Stamina) * .02f;
        }
    }

    // ---------------------------------------------------------------------------------
    // Optional bridge for an external trainer (off unless ipc=1 in DBPU_AutonomousAI.cfg).
    // A named memory-mapped file "DBPU_SingularityMind": the game writes the state tensor and
    // the last decision under a sequence counter, and reads tactic priors and, if offered, a
    // full set of network weights the same way. Nothing here ever waits.
    //   0 magic 'DBPU'  4 version  8 stateSeq  12 stateCount(32)  16 time  20 state[32]
    //   148 tacticCount(16)  152 probability[16]  216 action
    //   256 priorSeq  260 prior[16]
    //   324 weightSeq  328 weightCount  1024 weight[7312]
    // A writer makes its sequence odd, writes, then makes it even again.
    // ---------------------------------------------------------------------------------
    public static class AiIpc
    {
        const string MapName = "DBPU_SingularityMind";
        const int Capacity = 1024 + AiPolicyNet.WeightCount * 4 + 64;
        static MemoryMappedFile file; static MemoryMappedViewAccessor view; static bool failed;
        static int stateSeq, lastPriorSeq = -1, lastWeightSeq = -1; static float clock;
        static readonly float[] state = new float[AiPolicyNet.In], probability = new float[AiTactic.Count], incoming = new float[AiTactic.Count];
        static readonly float[] weights = new float[AiPolicyNet.WeightCount];
        public static readonly float[] Priors = new float[AiTactic.Count];
        public static bool Open { get { return view != null; } }

        public static void Tick()
        {
            if (!AutonomousConfig.Ipc || failed) { if (view != null) Close(); return; }
            clock -= Time.unscaledDeltaTime;
            if (clock > 0f) return;
            clock = .1f;
            try
            {
                if (view == null)
                {
                    file = MemoryMappedFile.CreateOrOpen(MapName, Capacity);
                    view = file.CreateViewAccessor(0, Capacity);
                    view.Write(0, 0x55504244); view.Write(4, 1); view.Write(12, AiPolicyNet.In); view.Write(148, AiTactic.Count);
                }
                var mind = SingularityMind.First;
                if (mind != null && mind.ExportState(state, probability))
                {
                    view.Write(8, ++stateSeq);
                    view.Write(16, Time.time);
                    view.WriteArray(20, state, 0, state.Length);
                    view.WriteArray(152, probability, 0, probability.Length);
                    view.Write(216, mind.LastAction);
                    view.Write(8, ++stateSeq);
                }
                int seq = view.ReadInt32(256);
                if ((seq & 1) == 0 && seq != lastPriorSeq)
                {
                    view.ReadArray(260, incoming, 0, incoming.Length);
                    if (view.ReadInt32(256) == seq)
                    {
                        lastPriorSeq = seq;
                        for (int i = 0; i < Priors.Length; i++) Priors[i] = float.IsNaN(incoming[i]) || float.IsInfinity(incoming[i]) ? 0f : Mathf.Clamp(incoming[i], -4f, 4f);
                    }
                }
                seq = view.ReadInt32(324);
                if ((seq & 1) == 0 && seq != lastWeightSeq && seq != 0 && view.ReadInt32(328) == AiPolicyNet.WeightCount)
                {
                    view.ReadArray(1024, weights, 0, weights.Length);
                    if (view.ReadInt32(324) == seq)
                    {
                        lastWeightSeq = seq;
                        if (mind != null) mind.InjectWeights(weights);
                    }
                }
            }
            catch (Exception ex)
            {
                failed = true;
                Debug.LogWarning("[Singularity] AI IPC disabled: " + ex.Message);
                Close();
            }
        }

        public static void Close()
        {
            try { if (view != null) view.Dispose(); if (file != null) file.Dispose(); }
            catch (Exception ex) { Debug.LogWarning("[Singularity] AI IPC close: " + ex.Message); }
            view = null; file = null;
            Array.Clear(Priors, 0, Priors.Length);
        }
    }
}
