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
    public class OozaruFormBehaviour : MonoBehaviour
    {
        struct JointState
        {
            public bool HadJoint;
            public Vector2 Anchor;
            public Vector2 ConnectedAnchor;
            public bool AutoConfigureConnectedAnchor;
            public bool UseLimits;
            public float MinLimit;
            public float MaxLimit;
            public Rigidbody2D ConnectedBody;
            public float Drag;
            public float AngularDrag;
        }

        struct LimbState
        {
            public Vector3 Scale;
            public float TrueInitialMass;
            public float Health;
            public float InitialHealth;
            public float BreakingThreshold;
            public float BaseStrength;
            public float ImpactDamageMultiplier;
            public float ImpactPainMultiplier;
            public float GForcePassoutThreshold;
            public float ShotDamageMultiplier;
            public float FreezingTemperature;
            public float DiscomfortingHeatTemperature;
            public bool IsAndroid;
            public PhysicalProperties Properties;
            public Sprite Sprite;
            public Texture FleshTex;
            public Texture BoneTex;
            public bool ShouldRecolourWhenCold;
        }

        PersonBehaviour person;
        JointState[] jointStates;
        LimbState[] limbStates;
        bool[] hairEnabled;
        float previousFormMultiplier;
        float previousHeightMeters = 1.64f;
        bool applied;

        void OnEnable()
        {
            ApplyOozaru();
        }

        void Start()
        {
            ApplyOozaru();
        }

        void ApplyOozaru()
        {
            if (applied) return;
            person = GetComponent<PersonBehaviour>();
            if (person == null) person = GetComponentInParent<PersonBehaviour>();
            if (person == null || person.Limbs == null || person.Limbs.Length < 14) return;

            CacheState();

            GameObject sizeLimb = null;
            if (person.Limbs.Length > 2 && person.Limbs[2] != null)
                sizeLimb = person.Limbs[2].gameObject;
            previousHeightMeters = Mathf.Abs(person.transform.localScale.y) * 1.68f;
            if (sizeLimb != null)
                person.transform.root.SetHeight(person, sizeLimb, 1.68f);

            DBPU.ApplyFullOozaruBodySetup(person, person.transform.root.gameObject);

            var multipliers = person.GetComponent<MultipliersAndStuffs>();
            if (multipliers == null) multipliers = person.transform.root.GetComponent<MultipliersAndStuffs>();
            if (multipliers != null)
            {
                previousFormMultiplier = multipliers.FormMultiplier;
                multipliers.FormMultiplier = 25000f;
            }

            Transform head = person.transform.Find("Head");
            if (head != null)
                DBPU.ApplyGohanImmunities(head, person);

            applied = true;
        }

        void CacheState()
        {
            int n = person.Limbs.Length;
            jointStates = new JointState[n];
            limbStates = new LimbState[n];

            for (int i = 0; i < n; i++)
            {
                LimbBehaviour limb = person.Limbs[i];
                if (limb == null) continue;

                var ls = new LimbState
                {
                    Scale = limb.transform.localScale,
                    Health = limb.Health,
                    InitialHealth = limb.InitialHealth,
                    BreakingThreshold = limb.BreakingThreshold,
                    BaseStrength = limb.BaseStrength,
                    ImpactDamageMultiplier = limb.ImpactDamageMultiplier,
                    ImpactPainMultiplier = limb.ImpactPainMultiplier,
                    GForcePassoutThreshold = limb.GForcePassoutThreshold,
                    ShotDamageMultiplier = limb.ShotDamageMultiplier,
                    FreezingTemperature = limb.FreezingTemperature,
                    DiscomfortingHeatTemperature = limb.DiscomfortingHeatTemperature,
                    IsAndroid = limb.IsAndroid
                };

                PhysicalBehaviour phys = limb.GetComponent<PhysicalBehaviour>();
                if (phys != null)
                {
                    ls.TrueInitialMass = phys.TrueInitialMass;
                    ls.Properties = phys.Properties;
                }

                var renderer = limb.GetComponent<SpriteRenderer>();
                if (renderer != null)
                {
                    ls.Sprite = renderer.sprite;
                    if (renderer.material != null)
                    {
                        ls.FleshTex = renderer.material.GetTexture("_FleshTex");
                        ls.BoneTex = renderer.material.GetTexture("_BoneTex");
                    }
                }
                if (limb.SkinMaterialHandler != null)
                    ls.ShouldRecolourWhenCold = limb.SkinMaterialHandler.ShouldRecolourWhenCold;
                limbStates[i] = ls;

                HingeJoint2D joint = limb.GetComponent<HingeJoint2D>();
                var state = new JointState();
                if (joint != null)
                {
                    state.HadJoint = true;
                    state.Anchor = joint.anchor;
                    state.ConnectedAnchor = joint.connectedAnchor;
                    state.AutoConfigureConnectedAnchor = joint.autoConfigureConnectedAnchor;
                    state.UseLimits = joint.useLimits;
                    state.MinLimit = joint.limits.min;
                    state.MaxLimit = joint.limits.max;
                    state.ConnectedBody = joint.connectedBody;
                }
                Rigidbody2D rb = limb.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    state.Drag = rb.drag;
                    state.AngularDrag = rb.angularDrag;
                }
                jointStates[i] = state;
            }

            Transform head = person.transform.Find("Head");
            if (head != null)
            {
                var renderers = head.GetComponentsInChildren<SpriteRenderer>(true);
                hairEnabled = new bool[renderers.Length];
                for (int i = 0; i < renderers.Length; i++)
                {
                    bool isHair = renderers[i] != null && renderers[i].gameObject != null
                        && renderers[i].gameObject.name.IndexOf("Hair", StringComparison.OrdinalIgnoreCase) >= 0;
                    hairEnabled[i] = isHair && renderers[i].enabled;
                }
            }

            var multipliers = person.GetComponent<MultipliersAndStuffs>();
            if (multipliers == null) multipliers = person.transform.root.GetComponent<MultipliersAndStuffs>();
            if (multipliers != null)
                previousFormMultiplier = multipliers.FormMultiplier;
        }

        void SetHairVisible(bool visible)
        {
            Transform head = person != null ? person.transform.Find("Head") : null;
            if (head == null) return;
            var renderers = head.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var sr = renderers[i];
                if (sr == null || sr.gameObject == null) continue;
                if (sr.gameObject.name.IndexOf("Hair", StringComparison.OrdinalIgnoreCase) < 0) continue;
                bool restore = visible && hairEnabled != null && i < hairEnabled.Length && hairEnabled[i];
                sr.enabled = restore;
                if (!visible) sr.enabled = false;
            }
        }

        void OnDestroy()
        {
            if (!applied || person == null || person.Limbs == null) return;
            try
            {
                RestoreState();
            }
            catch { }
        }

        void RestoreState()
        {
            if (jointStates == null || limbStates == null) return;
            int n = Mathf.Min(person.Limbs.Length, jointStates.Length);
            for (int i = 0; i < n; i++)
            {
                LimbBehaviour limb = person.Limbs[i];
                if (limb == null) continue;
                LimbState ls = limbStates[i];

                limb.transform.localScale = ls.Scale;
                limb.gameObject.FixColliders();
                limb.IsAndroid = ls.IsAndroid;
                limb.Health = ls.Health;
                limb.InitialHealth = ls.InitialHealth;
                limb.BreakingThreshold = ls.BreakingThreshold;
                limb.BaseStrength = ls.BaseStrength;
                limb.ImpactDamageMultiplier = ls.ImpactDamageMultiplier;
                limb.ImpactPainMultiplier = ls.ImpactPainMultiplier;
                limb.GForcePassoutThreshold = ls.GForcePassoutThreshold;
                limb.ShotDamageMultiplier = ls.ShotDamageMultiplier;
                limb.FreezingTemperature = ls.FreezingTemperature;
                limb.DiscomfortingHeatTemperature = ls.DiscomfortingHeatTemperature;
                if (limb.SkinMaterialHandler != null)
                    limb.SkinMaterialHandler.ShouldRecolourWhenCold = ls.ShouldRecolourWhenCold;

                PhysicalBehaviour phys = limb.GetComponent<PhysicalBehaviour>();
                if (phys != null)
                {
                    if (ls.TrueInitialMass > 0f)
                    {
                        phys.TrueInitialMass = ls.TrueInitialMass;
                        phys.RecalculateMassBasedOnSize();
                    }
                    if (ls.Properties != null)
                        phys.Properties = ls.Properties;
                }

                JointState state = jointStates[i];
                HingeJoint2D joint = limb.GetComponent<HingeJoint2D>();
                if (state.HadJoint)
                {
                    if (joint == null) joint = limb.gameObject.AddComponent<HingeJoint2D>();
                    joint.anchor = state.Anchor;
                    joint.connectedAnchor = state.ConnectedAnchor;
                    joint.autoConfigureConnectedAnchor = state.AutoConfigureConnectedAnchor;
                    joint.useLimits = state.UseLimits;
                    if (state.UseLimits)
                        joint.limits = new JointAngleLimits2D { min = state.MinLimit, max = state.MaxLimit };
                    if (state.ConnectedBody != null)
                        joint.connectedBody = state.ConnectedBody;
                }
                else if (joint != null)
                {
                    UnityEngine.Object.Destroy(joint);
                }

                Rigidbody2D rb = limb.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.drag = state.Drag;
                    rb.angularDrag = state.AngularDrag;
                }
            }

            var multipliers = person.GetComponent<MultipliersAndStuffs>();
            if (multipliers == null) multipliers = person.transform.root.GetComponent<MultipliersAndStuffs>();
            if (multipliers != null && previousFormMultiplier > 0f)
                multipliers.FormMultiplier = previousFormMultiplier;

            SetHairVisible(true);

            GameObject sizeLimb = null;
            if (person.Limbs != null && person.Limbs.Length > 2 && person.Limbs[2] != null)
                sizeLimb = person.Limbs[2].gameObject;
            if (sizeLimb != null && previousHeightMeters > 0.01f)
                person.transform.root.SetHeight(person, sizeLimb, previousHeightMeters);

            DBPU.BakeOozaruStandingPoses(person);
        }
    }
}
