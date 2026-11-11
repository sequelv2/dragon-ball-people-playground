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

        private void FixedUpdate()
        {
            if (!_isFlying || _person.Braindead || _person.Heartbeat == 0) return;

            foreach (var limb in _person.Limbs)
            {
                Rigidbody2D limbRb = limb.PhysicalBehaviour.rigidbody;
                if (limbRb.bodyType == RigidbodyType2D.Static) continue;

                limbRb.AddForce(-1f * limbRb.gravityScale * limbRb.mass * Physics2D.gravity);

                float brake = Mathf.Pow(0.95f, Mathf.Max(1f, limb.PhysicalBehaviour.Charge));
                float angularBrake = Mathf.Pow(0.9f, Mathf.Max(1f, limb.PhysicalBehaviour.Charge));

                limbRb.velocity *= brake;
                limbRb.angularVelocity *= angularBrake;
            }
        }
    }
}
