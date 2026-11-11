using Mod;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;
using static FunLittleGames.Linkage.Linkage;
using Random = UnityEngine.Random;
using HarmonyLib;

namespace DBPU
{
    public static class KiDeflectUtils
    {
        public static void DisintegratePersonIfBeamKilled(PersonBehaviour person, bool wasAliveBeforeDamage)
        {
            if (!DBPU.Beam_ToggleDelete || !DBPU.Beam_BurnAwayOnKill || person == null) return;
            if (!wasAliveBeforeDamage || person.IsAlive()) return;
            foreach (var limb in person.Limbs)
            {
                if (limb == null) continue;
                var pb = limb.PhysicalBehaviour;
                if (pb == null || pb.isDisintegrated) continue;
                pb.Disintegrate();
            }
        }

        public static bool IsBeamProtectedCorpse(PersonBehaviour person)
        {
            return person != null && !person.IsAlive() && !DBPU.Beam_ToggleDelete;
        }

        public static bool TryBeamHitCorpseOnlyKnockback(PersonBehaviour person, Rigidbody2D rb, Vector2 direction, float impulseScale)
        {
            if (!IsBeamProtectedCorpse(person) || rb == null) return false;
            float beamKnock = DBPU.EffectiveBeamKnockback;
            if (beamKnock > 0f)
                rb.AddForce(direction * (impulseScale * rb.mass * beamKnock), ForceMode2D.Impulse);
            return true;
        }

        private class BeamCatchState
        {
            public int DefenderRootId;
            public float ReleaseTime;
            public Vector2 AimedDirection;
        }

        private static readonly Dictionary<int, BeamCatchState> BeamCatchStates = new Dictionary<int, BeamCatchState>();

        private static Vector3 GetDefenderSplitAnchor(LimbBehaviour limb)
        {
            if (limb == null) return Vector3.zero;
            var person = limb.Person;
            if (person != null && person.Limbs != null)
            {
                foreach (var l in person.Limbs)
                {
                    if (l == null || string.IsNullOrEmpty(l.name)) continue;
                    if (l.name.IndexOf("UpperBody", StringComparison.OrdinalIgnoreCase) >= 0)
                        return l.transform.position;
                }
            }

            return limb.transform.position;
        }

        private static bool IsHandOrForearmLimb(LimbBehaviour limb)
        {
            if (limb == null || string.IsNullOrEmpty(limb.name)) return false;
            string n = limb.name.ToLowerInvariant();
            return n.Contains("lowerarm") || n.Contains("hand");
        }

        private static Vector2 GetPreferredDeflectDirection(LimbBehaviour contactLimb, Vector2 attackerPos, Vector2 hitPos)
        {
            if (contactLimb == null) return Vector2.right;

            Vector2 limbRight = contactLimb.transform.right;
            Vector2 limbUp = contactLimb.transform.up;
            Vector2 toAttacker = (attackerPos - hitPos).normalized;
            Vector2 chosen = limbRight.sqrMagnitude > 0.001f ? limbRight.normalized : Vector2.right;

            // If the player is actively swinging, prioritize swing direction for anime-style swats.
            var rb = contactLimb.PhysicalBehaviour != null ? contactLimb.PhysicalBehaviour.rigidbody : null;
            if (rb != null && rb.velocity.sqrMagnitude > 4f)
            {
                chosen = rb.velocity.normalized;
            }
            else
            {
                Vector2[] options = new Vector2[]
                {
                    limbRight.normalized,
                    -limbRight.normalized,
                    limbUp.normalized,
                    -limbUp.normalized
                };

                float best = float.NegativeInfinity;
                for (int i = 0; i < options.Length; i++)
                {
                    float score = Vector2.Dot(options[i], toAttacker);
                    if (score > best)
                    {
                        best = score;
                        chosen = options[i];
                    }
                }
            }

            if (chosen.sqrMagnitude < 0.001f) chosen = Vector2.right;
            return chosen.normalized;
        }

        private static float GetEffectivePowerScore(GameObject root)
        {
            if (root == null) return 1f;
            var ki = root.GetComponent<KiSystem>();
            float powerMul = DBPU.PowerMultiplier;

            var person = root.GetComponent<PersonBehaviour>();
            var tm = (person != null && person.Limbs != null && person.Limbs.Count() > 0) ? person.Limbs[0].GetComponent<TransformationContextMenu>() : null;
            if (tm != null && tm.TransformationsData != null && tm.TransformationsData.transformations != null)
            {
                var data = tm.TransformationsData.transformations.TransformationData;
                string form = ki != null && !string.IsNullOrEmpty(ki.CurrentForm) ? ki.CurrentForm : tm.Form;
                if (!string.IsNullOrEmpty(form) && data.ContainsKey(form))
                    powerMul = Mathf.Max(1f, data[form].PowerMultiplier * DBPU.PowerMultiplier);
                else if (!string.IsNullOrEmpty(tm.Form) && data.ContainsKey(tm.Form))
                    powerMul = Mathf.Max(1f, data[tm.Form].PowerMultiplier * DBPU.PowerMultiplier);
            }

            float ki01 = 1f;
            if (ki != null && ki.maxKi > 0.01f)
                ki01 = Mathf.Clamp01(ki.currentKi / ki.maxKi);
            float kiFactor = Mathf.Lerp(0.45f, 1f, ki01);
            return powerMul * kiFactor;
        }

        private static bool CanDefenderDeflect(GameObject defenderRoot, LimbBehaviour contactLimb, bool isBeam)
        {
            if (!DBPU.KiDeflectEnabled || defenderRoot == null || contactLimb == null) return false;
            if (!IsHandOrForearmLimb(contactLimb)) return false;

            var person = defenderRoot.GetComponent<PersonBehaviour>();
            if (person == null || !person.IsAlive()) return false;

            var ki = defenderRoot.GetComponent<KiSystem>();
            if (ki != null && ki.SparkingExhaustActive) return false;

            return true;
        }

        private static bool PassesHybridThreshold(GameObject defenderRoot, GameObject attackerRoot, bool isBeam)
        {
            float defenderScore = GetEffectivePowerScore(defenderRoot);
            float attackerScore = GetEffectivePowerScore(attackerRoot);
            float typeMul = isBeam ? Mathf.Max(1f, DBPU.KiDeflectBeamThresholdMultiplier) : Mathf.Max(1f, DBPU.KiDeflectBlastThresholdMultiplier);
            float need = Mathf.Max(1f, DBPU.KiDeflectHybridPowerThreshold) * typeMul;
            return defenderScore >= attackerScore * need;
        }

        public static bool TryDeflectBlast(BlastComponent blast, Collision2D collision)
        {
            if (blast == null || collision == null || blast.User == null) return false;
            var limb = collision.gameObject.GetComponent<LimbBehaviour>();
            if (limb == null) return false;
            var defenderRoot = limb.transform.root != null ? limb.transform.root.gameObject : null;
            var attackerRoot = blast.User.transform.root != null ? blast.User.transform.root.gameObject : blast.User;
            if (defenderRoot == null || attackerRoot == null || defenderRoot == attackerRoot) return false;

            if (!CanDefenderDeflect(defenderRoot, limb, false)) return false;
            if (!PassesHybridThreshold(defenderRoot, attackerRoot, false)) return false;

            Vector2 away = GetPreferredDeflectDirection(
                limb,
                attackerRoot.transform.position,
                limb.transform.position
            );

            blast.User = defenderRoot;
            blast.ID = "Deflect" + Random.Range(0, 100000);
            blast.AlreadyHit = false;
            blast.HasHit = false;
            blast.HandleSelfMovement = true;
            blast.transform.right = away.normalized;

            var rb = blast.GetComponent<Rigidbody2D>();
            if (rb != null)
                rb.velocity = away.normalized * Mathf.Max(20f, blast.realSpeed);

            var blastCol = blast.GetComponent<Collider2D>();
            var limbCol = limb.GetComponent<Collider2D>();
            if (blastCol != null && limbCol != null)
                Physics2D.IgnoreCollision(blastCol, limbCol, true);

            return true;
        }

        public static bool TryDeflectBeamHit(BeamBehaviour beam, LimbBehaviour limb)
        {
            if (beam == null || limb == null) return false;
            var defenderRoot = limb.transform.root != null ? limb.transform.root.gameObject : null;
            var attackerRoot = beam.transform.root != null ? beam.transform.root.gameObject : beam.gameObject;
            if (defenderRoot == null || attackerRoot == null || defenderRoot == attackerRoot) return false;

            if (!CanDefenderDeflect(defenderRoot, limb, true)) return false;
            if (!PassesHybridThreshold(defenderRoot, attackerRoot, true)) return false;

            Vector2 originalDir = beam.BeamDirection.sqrMagnitude > 0.001f ? new Vector2(beam.BeamDirection.x, beam.BeamDirection.y).normalized : Vector2.right;
            Vector2 deflectDir = GetPreferredDeflectDirection(
                limb,
                beam.StartBeamPosition,
                limb.transform.position
            );

            if (DBPU.KiDeflectBeamCatchThenAim)
            {
                int beamId = beam.GetInstanceID();
                int defenderId = defenderRoot.GetInstanceID();
                float catchSeconds = Mathf.Max(0.05f, DBPU.KiDeflectBeamCatchSeconds);
                BeamCatchState state;
                if (!BeamCatchStates.TryGetValue(beamId, out state) || state.DefenderRootId != defenderId)
                {
                    state = new BeamCatchState()
                    {
                        DefenderRootId = defenderId,
                        ReleaseTime = Time.time + catchSeconds,
                        AimedDirection = deflectDir
                    };
                    BeamCatchStates[beamId] = state;
                }
                else
                {
                    state.AimedDirection = deflectDir;
                }

                Vector2 activeDir = state.AimedDirection.sqrMagnitude > 0.001f ? state.AimedDirection.normalized : deflectDir;
                if (Time.time < state.ReleaseTime)
                {
                    beam.Distance = Mathf.Min(beam.Distance, Mathf.Max(0.8f, beam.realSpeed * 0.04f));
                    beam.BeamDirection = activeDir;
                    beam.EndBeamPosition = beam.StartBeamPosition + beam.BeamDirection.normalized * beam.Distance;
                    if (beam.EndBeam != null)
                        beam.EndBeam.transform.right = -beam.BeamDirection.normalized;
                    if (beam.EndBeam != null && beam.EndBeam.TryGetComponent<BeamComponent>(out var catchBeamComponent))
                        catchBeamComponent.RayDirection = beam.BeamDirection.normalized;

                    var catchRb = limb.PhysicalBehaviour != null ? limb.PhysicalBehaviour.rigidbody : null;
                    if (catchRb != null)
                        catchRb.AddForce((-originalDir) * Mathf.Max(0.05f, 0.12f * catchRb.mass), ForceMode2D.Impulse);

                    return true;
                }

                BeamCatchStates.Remove(beamId);
                deflectDir = activeDir;
            }

            float shove = Mathf.Max(3.5f, beam.realSpeed * 0.22f);
            beam.Distance = Mathf.Max(0f, beam.Distance - shove);
            beam.BeamDirection = deflectDir;
            beam.EndBeamPosition = beam.StartBeamPosition + beam.BeamDirection.normalized * beam.Distance;
            if (beam.EndBeam != null)
                beam.EndBeam.transform.right = -beam.BeamDirection.normalized;
            if (beam.EndBeam != null && beam.EndBeam.TryGetComponent<BeamComponent>(out var beamComponent))
                beamComponent.RayDirection = beam.BeamDirection.normalized;

            var rb = limb.PhysicalBehaviour != null ? limb.PhysicalBehaviour.rigidbody : null;
            if (rb != null)
                rb.AddForce((-originalDir) * Mathf.Max(0.2f, 0.8f * rb.mass), ForceMode2D.Impulse);

            return true;
        }

        private static bool PassesBeamSplitThreshold(GameObject defenderRoot, GameObject attackerRoot)
        {
            float defenderScore = GetEffectivePowerScore(defenderRoot);
            float attackerScore = GetEffectivePowerScore(attackerRoot);
            float baseNeed = Mathf.Max(1f, DBPU.KiDeflectHybridPowerThreshold) * Mathf.Max(1f, DBPU.KiDeflectBeamThresholdMultiplier);
            float splitNeed = baseNeed * Mathf.Max(0.05f, DBPU.KiDeflectBeamSplitThresholdMultiplier);
            return defenderScore >= attackerScore * splitNeed;
        }

        private static bool CanDefenderBeamSplit(GameObject defenderRoot)
        {
            if (defenderRoot == null) return false;
            var person = defenderRoot.GetComponent<PersonBehaviour>();
            if (person == null || !person.IsAlive()) return false;
            var ki = defenderRoot.GetComponent<KiSystem>();
            if (ki != null && ki.SparkingExhaustActive) return false;
            return true;
        }

        private static void ClipBeamAtHit(BeamBehaviour beam, RaycastHit2D hit)
        {
            float newD = Mathf.Max(0.06f, hit.distance + beam.MiddleMinusDistance - 0.05f);
            beam.Distance = newD;
            Vector3 dir = beam.BeamDirection.sqrMagnitude > 0.001f ? beam.BeamDirection.normalized : Vector3.right;
            beam.EndBeamPosition = beam.StartBeamPosition + dir * beam.Distance;
            beam.realSpeed = 0f;
            if (beam.EndBeam != null)
            {
                beam.EndBeam.transform.right = -dir;
                if (beam.EndBeam.TryGetComponent<BeamComponent>(out var beamComponent))
                    beamComponent.RayDirection = new Vector2(dir.x, dir.y).normalized;
            }
        }

        public static bool TryBeamSplitHit(BeamBehaviour beam, LimbBehaviour limb, RaycastHit2D hit, Vector2 castDirection)
        {
            if (beam == null || limb == null) return false;
            if (!DBPU.KiDeflectBeamSplitEnabled) return false;
            if (beam.BeamSplitConsumed) return false;

            var defenderRoot = limb.transform.root != null ? limb.transform.root.gameObject : null;
            var attackerRoot = beam.transform.root != null ? beam.transform.root.gameObject : beam.gameObject;
            if (defenderRoot == null || attackerRoot == null || defenderRoot == attackerRoot) return false;

            if (!CanDefenderBeamSplit(defenderRoot)) return false;
            if (!PassesBeamSplitThreshold(defenderRoot, attackerRoot)) return false;

            float distBeforeClip = beam.Distance;
            Vector3 defenderAnchor = GetDefenderSplitAnchor(limb);

            ClipBeamAtHit(beam, hit);
            beam.BeamSplitConsumed = true;

            Vector3 bf = new Vector3(castDirection.x, castDirection.y, 0f);
            if (bf.sqrMagnitude < 0.0001f)
                bf = beam.BeamDirection.sqrMagnitude > 0.001f ? beam.BeamDirection.normalized : Vector3.right;
            bf.Normalize();

            Color col = (beam.MiddleBeam != null && beam.MiddleBeam.material != null)
                ? beam.MiddleBeam.material.color
                : Color.cyan;
            Texture2D tex = (beam.MiddleBeam != null && beam.MiddleBeam.material != null && beam.MiddleBeam.material.mainTexture != null)
                ? beam.MiddleBeam.material.mainTexture as Texture2D
                : DBPU.StraightBeam_Texture;

            float unspentAlongBeam = Mathf.Max(0f, distBeforeClip - beam.Distance);
            float forwardLen = Mathf.Max(
                DBPU.KiDeflectBeamSplitContinuationLength,
                unspentAlongBeam + beam.Speed * DBPU.Beam_SpeedMultiplier * 5.25f);

            BeamSplitVfx.SpawnFan(hit.point, bf, col, tex, defenderAnchor, forwardLen, beam, null);
            return true;
        }

        public static bool TryBeamSplitStraightBeamHit(StraightBeamBehaviour straight, LimbBehaviour limb, Vector2 hitPoint, Vector2 incomingDirection)
        {
            if (straight == null || limb == null) return false;
            if (!DBPU.KiDeflectBeamSplitEnabled) return false;
            if (straight.DeathBeamSplitConsumed) return false;

            var defenderRoot = limb.transform.root != null ? limb.transform.root.gameObject : null;
            var attackerRoot = straight.transform.root != null ? straight.transform.root.gameObject : straight.gameObject;
            if (defenderRoot == null || attackerRoot == null || defenderRoot == attackerRoot) return false;

            if (!CanDefenderBeamSplit(defenderRoot)) return false;
            if (!PassesBeamSplitThreshold(defenderRoot, attackerRoot)) return false;

            straight.DeathBeamSplitConsumed = true;
            Vector3 bf = new Vector3(incomingDirection.x, incomingDirection.y, 0f);
            if (bf.sqrMagnitude < 0.0001f) bf = Vector3.right;
            bf.Normalize();

            Vector3 defenderAnchor = GetDefenderSplitAnchor(limb);
            float forwardLen = Mathf.Max(
                DBPU.KiDeflectBeamSplitContinuationLength,
                92f * Mathf.Max(0.4f, DBPU.Beam_SizeMultiplier));

            Color col = new Color(0.95f, 0.3f, 0.3f, 1f);
            BeamSplitVfx.SpawnFan(hitPoint, bf, col, DBPU.StraightBeam_Texture, defenderAnchor, forwardLen, null, straight);
            return true;
        }

        public static class BeamSplitVfx
        {
            public static void SpawnFan(
                Vector3 hitPoint,
                Vector3 beamForward,
                Color color,
                Texture2D texture,
                Vector3 defenderAnchor,
                float forwardLength,
                BeamBehaviour beamHost,
                StraightBeamBehaviour straightHost)
            {
                int n = Mathf.Clamp(DBPU.KiDeflectBeamSplitRayCount, 3, 24);
                float wrap = DBPU.KiDeflectBeamSplitWrapDistance * Mathf.Max(0.2f, DBPU.Beam_SizeMultiplier);
                float arcDeg = DBPU.KiDeflectBeamSplitArcDegrees;
                float lineW = 0.055f * Mathf.Max(0.2f, DBPU.Beam_SizeMultiplier);

                Vector3 bf = beamForward;
                bf.z = 0f;
                if (bf.sqrMagnitude < 0.0001f) bf = Vector3.right;
                bf.Normalize();

                Vector2 out2 = new Vector2(hitPoint.x - defenderAnchor.x, hitPoint.y - defenderAnchor.y);
                if (out2.sqrMagnitude < 0.0004f)
                    out2 = new Vector2(-bf.y, bf.x);
                out2.Normalize();
                Vector3 outward = new Vector3(out2.x, out2.y, 0f);
                Vector3 tangentBase = new Vector3(-out2.y, out2.x, 0f);
                if (tangentBase.sqrMagnitude < 0.0001f)
                    tangentBase = new Vector3(-bf.y, bf.x, 0f);
                tangentBase.Normalize();

                Texture2D tex = texture != null ? texture : DBPU.StraightBeam_Texture;
                Material matBase = ModAPI.FindMaterial("VeryBright");

                GameObject root = new GameObject("BeamSplitFan");
                root.transform.position = hitPoint;
                var track = root.AddComponent<BeamSplitVfxRoot>();
                track.BeamHost = beamHost;
                track.StraightHost = straightHost;
                track.SpawnedAt = Time.time;
                if (straightHost != null)
                    track.MinStraightSeconds = Mathf.Max(0.55f, DBPU.KiDeflectBeamSplitLifetime);

                float forwardSeg = Mathf.Max(0.5f, forwardLength);

                for (int i = 0; i < n; i++)
                {
                    float t = n <= 1 ? 0.5f : (float)i / (n - 1);
                    float halfArc = arcDeg * 0.5f * Mathf.Deg2Rad;
                    float ang = Mathf.Lerp(-halfArc, halfArc, t);
                    float cosa = Mathf.Cos(ang);
                    float sina = Mathf.Sin(ang);
                    Vector3 lateralDir = new Vector3(
                        tangentBase.x * cosa - tangentBase.y * sina,
                        tangentBase.x * sina + tangentBase.y * cosa,
                        0f);
                    if (lateralDir.sqrMagnitude < 0.0001f) lateralDir = tangentBase;
                    lateralDir.Normalize();

                    Vector3 p0 = hitPoint;
                    Vector3 p1 = p0 + lateralDir * (wrap * 0.58f) + outward * (wrap * 0.48f);
                    Vector3 p2 = p1 + bf * (wrap * 0.28f);
                    Vector3 p3 = p2 + bf * forwardSeg;

                    GameObject seg = new GameObject("BeamSplitSeg");
                    seg.transform.SetParent(root.transform, true);
                    LineRenderer lr = seg.AddComponent<LineRenderer>();
                    lr.useWorldSpace = true;
                    lr.textureMode = LineTextureMode.Tile;
                    lr.numCapVertices = 2;
                    lr.positionCount = 4;
                    lr.SetPosition(0, p0);
                    lr.SetPosition(1, p1);
                    lr.SetPosition(2, p2);
                    lr.SetPosition(3, p3);
                    lr.startWidth = lineW;
                    lr.endWidth = lineW * 0.3f;
                    lr.sortingLayerName = "Bubbles";
                    lr.sortingOrder = 6;
                    if (matBase != null)
                    {
                        lr.material = new Material(matBase);
                        lr.material.mainTexture = tex;
                        lr.material.mainTextureScale = new Vector2(0.22f, 1f);
                        lr.material.color = color;
                    }
                }
            }
        }

        public static bool TryDeflectStraightBeam(GameObject attackerRoot, LimbBehaviour limb, Vector2 incomingDirection, out Vector2 deflectDirection)
        {
            deflectDirection = incomingDirection.sqrMagnitude > 0.001f ? incomingDirection.normalized : Vector2.right;
            if (attackerRoot == null || limb == null) return false;

            var defenderRoot = limb.transform.root != null ? limb.transform.root.gameObject : null;
            if (defenderRoot == null || defenderRoot == attackerRoot) return false;

            if (!CanDefenderDeflect(defenderRoot, limb, true)) return false;
            if (!PassesHybridThreshold(defenderRoot, attackerRoot, true)) return false;

            deflectDirection = GetPreferredDeflectDirection(
                limb,
                attackerRoot.transform.position,
                limb.transform.position
            );

            var rb = limb.PhysicalBehaviour != null ? limb.PhysicalBehaviour.rigidbody : null;
            if (rb != null)
                rb.AddForce((-incomingDirection.normalized) * Mathf.Max(0.2f, 0.8f * rb.mass), ForceMode2D.Impulse);

            return true;
        }
    }
}
