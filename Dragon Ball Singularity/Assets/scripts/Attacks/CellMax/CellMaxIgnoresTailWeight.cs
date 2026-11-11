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
    public class CellMaxIgnoresTailWeight : MonoBehaviour
    {
        const int TailStartIndex = 14;
        const int TailEndIndexExclusive = 26;
        const int CoreEndIndexExclusive = 14;
        const int TailAnchorIndex = 1;
        const float PullCompensationScale = 0.35f;
        const float FlightTailPullScale = 0.45f;
        const float FlightTailGatherScale = 0.2f;

        PersonBehaviour _person;
        Flight _flight;
        float[] _coreFakeUpright;
        float[] _tailFakeUpright;
        bool _wasFlying;
        bool _coreUprightEnabled = true;

        void Start()
        {
            _person = GetComponent<PersonBehaviour>();
            _flight = _person.GetComponentInChildren<Flight>(true);
            _coreFakeUpright = new float[CoreEndIndexExclusive];
            _tailFakeUpright = new float[TailEndIndexExclusive - TailStartIndex];

            for (int i = 0; i < _person.Limbs.Length && i < CoreEndIndexExclusive; i++)
            {
                var limb = _person.Limbs[i];
                _coreFakeUpright[i] = limb != null ? limb.FakeUprightForce : 0f;
            }

            for (int i = TailStartIndex; i < _person.Limbs.Length && i < TailEndIndexExclusive; i++)
            {
                var limb = _person.Limbs[i];
                _tailFakeUpright[i - TailStartIndex] = limb != null ? limb.FakeUprightForce : 0f;
            }
        }

        bool IsCoreTouchingSomething()
        {
            int coreCount = Mathf.Min(CoreEndIndexExclusive, _person.Limbs.Length);
            for (int i = 0; i < coreCount; i++)
            {
                var limb = _person.Limbs[i];
                if (limb == null || limb.PhysicalBehaviour == null || limb.PhysicalBehaviour.isDisintegrated) continue;
                if (limb.PhysicalBehaviour.IsTouchingSomething) return true;
            }

            return false;
        }

        void SetCoreFakeUpright(bool enabled)
        {
            if (_coreUprightEnabled == enabled) return;

            for (int i = 0; i < _person.Limbs.Length && i < CoreEndIndexExclusive; i++)
            {
                var limb = _person.Limbs[i];
                if (limb == null) continue;

                limb.FakeUprightForce = enabled ? _coreFakeUpright[i] : 0f;
            }

            _coreUprightEnabled = enabled;
        }

        void SetTailFakeUprightForFlight(bool flying)
        {
            for (int i = TailStartIndex; i < _person.Limbs.Length && i < TailEndIndexExclusive; i++)
            {
                var limb = _person.Limbs[i];
                if (limb == null) continue;

                limb.FakeUprightForce = flying ? 0f : _tailFakeUpright[i - TailStartIndex];
            }
        }

        bool TryGetTailStats(out Vector2 tailMassPosition, out float tailMass, out Vector2 tailWeightForce)
        {
            tailMassPosition = Vector2.zero;
            tailMass = 0f;
            tailWeightForce = Vector2.zero;

            for (int i = TailStartIndex; i < _person.Limbs.Length && i < TailEndIndexExclusive; i++)
            {
                var limb = _person.Limbs[i];
                if (limb == null || limb.PhysicalBehaviour.isDisintegrated) continue;

                Rigidbody2D rb = limb.PhysicalBehaviour.rigidbody;
                if (rb.bodyType == RigidbodyType2D.Static) continue;

                float limbMass = rb.mass;
                tailWeightForce += rb.gravityScale * limbMass * Physics2D.gravity;
                tailMassPosition += rb.worldCenterOfMass * limbMass;
                tailMass += limbMass;
            }

            if (tailMass <= 0f) return false;

            tailMassPosition /= tailMass;
            return true;
        }

        Vector2 GetPullCompensation(Vector2 tailMassPosition, float tailMass, Vector2 anchorPos, float scale)
        {
            Vector2 pullDir = tailMassPosition - anchorPos;
            if (pullDir.sqrMagnitude <= 0.01f) return Vector2.zero;

            pullDir.Normalize();
            return -pullDir * (tailMass * Mathf.Abs(Physics2D.gravity.y) * scale);
        }

        void ApplyCoreForce(Vector2 force)
        {
            int coreCount = Mathf.Min(CoreEndIndexExclusive, _person.Limbs.Length);
            if (coreCount <= 0 || force.sqrMagnitude <= 0.0001f) return;

            float anchorShare = 0.55f;
            float restShare = coreCount > 1 ? (1f - anchorShare) / (coreCount - 1) : 0f;

            for (int i = 0; i < coreCount; i++)
            {
                var limb = _person.Limbs[i];
                if (limb == null || limb.PhysicalBehaviour.isDisintegrated) continue;

                Rigidbody2D rb = limb.PhysicalBehaviour.rigidbody;
                if (rb.bodyType == RigidbodyType2D.Static) continue;

                float share = i == TailAnchorIndex ? anchorShare : restShare;
                rb.AddForce(force * share, ForceMode2D.Force);
            }
        }

        void StabilizeTailInFlight(Vector2 anchorPos, float tailMass)
        {
            float gatherStrength = tailMass * Mathf.Abs(Physics2D.gravity.y) * FlightTailGatherScale;

            for (int i = TailStartIndex; i < _person.Limbs.Length && i < TailEndIndexExclusive; i++)
            {
                var limb = _person.Limbs[i];
                if (limb == null || limb.PhysicalBehaviour.isDisintegrated) continue;

                Rigidbody2D rb = limb.PhysicalBehaviour.rigidbody;
                if (rb.bodyType == RigidbodyType2D.Static) continue;

                Vector2 toAnchor = anchorPos - rb.worldCenterOfMass;
                if (toAnchor.sqrMagnitude <= 0.01f) continue;

                rb.AddForce(toAnchor.normalized * gatherStrength, ForceMode2D.Force);
            }
        }

        void FixedUpdate()
        {
            if (_person == null || _person.Braindead || _person.Heartbeat == 0) return;

            bool isFlying = _flight != null && _flight.IsFlying;
            bool coreTouchingSomething = IsCoreTouchingSomething();
            SetCoreFakeUpright(!isFlying && coreTouchingSomething);

            if (isFlying != _wasFlying)
            {
                SetTailFakeUprightForFlight(isFlying);
                _wasFlying = isFlying;
            }

            if (isFlying || !coreTouchingSomething) return;

            if (!TryGetTailStats(out Vector2 tailMassPosition, out float tailMass, out Vector2 tailWeightForce)) return;

            var anchorLimb = _person.Limbs[TailAnchorIndex];
            if (anchorLimb == null || anchorLimb.PhysicalBehaviour.isDisintegrated) return;

            Vector2 anchorPos = anchorLimb.PhysicalBehaviour.rigidbody.worldCenterOfMass;

            Vector2 cancelForce = -tailWeightForce;
            cancelForce += GetPullCompensation(tailMassPosition, tailMass, anchorPos, PullCompensationScale);
            ApplyCoreForce(cancelForce);
        }
    }
}
