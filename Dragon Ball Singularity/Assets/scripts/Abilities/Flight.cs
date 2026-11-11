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
    public class Flight : MonoBehaviour
    {
        private PersonBehaviour _person;
        private bool _isFlying;

        public bool IsFlying => _isFlying;

        private void Start()
        {
            _person = transform.root.GetComponent<PersonBehaviour>();
        }

        public void Use()
        {
            // The Use press that confirmed a torso picker choice is not also a flight toggle.
            if (TorsoTechniquePicker.BlocksUse(this)) return;
            Toggle();
        }

        public void Toggle()
        {
            _isFlying = !_isFlying;
        }

        // AUTONOMOUS: a body driven by an AI strike skips the flight brake until its time runs out, so a
        // punch thrown in the air keeps its speed. Gravity is still cancelled. Nothing is stored unless
        // the AI asks, and then the loop below costs one integer test per limb.
        const int BrakeExemptCapacity = 64;
        static readonly Rigidbody2D[] _brakeExemptBodies = new Rigidbody2D[BrakeExemptCapacity];
        static readonly float[] _brakeExemptUntil = new float[BrakeExemptCapacity];
        static int _brakeExemptCount;

        // untilTime is on the Time.time clock; a time already past removes the exemption.
        public static void ExemptFromBrake(Rigidbody2D rb, float untilTime)
        {
            if (!rb) return;
            for (int i = 0; i < _brakeExemptCount; i++)
                if (_brakeExemptBodies[i] == rb) { _brakeExemptUntil[i] = untilTime; return; }
            if (_brakeExemptCount >= BrakeExemptCapacity) return;
            _brakeExemptBodies[_brakeExemptCount] = rb;
            _brakeExemptUntil[_brakeExemptCount] = untilTime;
            _brakeExemptCount++;
        }

        static bool IsBrakeExempt(Rigidbody2D rb)
        {
            float now = Time.time;
            for (int i = _brakeExemptCount - 1; i >= 0; i--)
            {
                if (!_brakeExemptBodies[i] || _brakeExemptUntil[i] <= now)
                {
                    // Expired or destroyed: the last entry takes its place.
                    _brakeExemptCount--;
                    _brakeExemptBodies[i] = _brakeExemptBodies[_brakeExemptCount];
                    _brakeExemptUntil[i] = _brakeExemptUntil[_brakeExemptCount];
                    _brakeExemptBodies[_brakeExemptCount] = null;
                    continue;
                }
                if (_brakeExemptBodies[i] == rb) return true;
            }
            return false;
        }

        private void FixedUpdate()
        {
            if (!_isFlying || _person.Braindead || _person.Heartbeat == 0) return;

            foreach (var limb in _person.Limbs)
            {
                Rigidbody2D limbRb = limb.PhysicalBehaviour.rigidbody;
                if (limbRb.bodyType == RigidbodyType2D.Static) continue;

                limbRb.AddForce(-1f * limbRb.gravityScale * limbRb.mass * Physics2D.gravity);

                // AUTONOMOUS: see ExemptFromBrake.
                if (_brakeExemptCount > 0 && IsBrakeExempt(limbRb)) continue;

                float brake = Mathf.Pow(0.95f, Mathf.Max(1f, limb.PhysicalBehaviour.Charge));
                float angularBrake = Mathf.Pow(0.9f, Mathf.Max(1f, limb.PhysicalBehaviour.Charge));

                limbRb.velocity *= brake;
                limbRb.angularVelocity *= angularBrake;
            }
        }
    }
}
