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
    public class PersonLimbLayout : MonoBehaviour
    {
        private PersonBehaviour person;
        private float defaultHeadScale;
        private readonly Dictionary<string, Vector3> defaultPositions = new Dictionary<string, Vector3>();
        private readonly Dictionary<string, Vector2> defaultAnchors = new Dictionary<string, Vector2>();
        private bool cached;

        public bool CustomLayoutActive { get; private set; }

        private static readonly string[] PositionKeys =
        {
            "Head", "Upper", "Middle", "Lower",
            "UArmF", "UArmB", "LArmF", "LArmB",
            "ULegF", "ULegB", "LLegF", "LLegB",
            "FootF", "FootB"
        };

        private static readonly string[] AnchorKeys =
        {
            "Head", "Upper", "Middle", "Lower",
            "UArmF", "UArmB", "LArmF", "LArmB",
            "ULegF", "ULegB", "LLegF", "LLegB",
            "FootF", "FootB"
        };

        private void Awake()
        {
            person = GetComponent<PersonBehaviour>();
        }

        public void Apply(CharacterTransformations.Transformations.LimbLayoutData layout)
        {
            EnsureCached();
            if (layout == null)
            {
                Restore();
                return;
            }

            float bodyScale = Mathf.Abs(person.transform.localScale.x);
            if (layout.HeadScale > 0f)
            {
                Transform head = person.transform.Find("Head");
                if (head != null) head.localScale = Vector3.one * (layout.HeadScale / bodyScale);
            }

            if (layout.Positions != null)
            {
                foreach (var kvp in layout.Positions)
                    SetLocalPosition(kvp.Key, kvp.Value);
            }

            if (layout.Anchors != null)
            {
                foreach (var kvp in layout.Anchors)
                    SetJointAnchor(kvp.Key, kvp.Value);
            }

            person.SyncLimbsPhysics();
            person.ForceAllLimbsClearViaExit();
            CustomLayoutActive = true;
        }

        public void Restore()
        {
            EnsureCached();

            Transform head = person.transform.Find("Head");
            if (head != null) head.localScale = Vector3.one * defaultHeadScale;

            foreach (var kvp in defaultPositions)
                SetLocalPosition(kvp.Key, kvp.Value);

            foreach (var kvp in defaultAnchors)
                SetJointAnchor(kvp.Key, kvp.Value);

            person.SyncLimbsPhysics();
            person.ForceAllLimbsClearViaExit();
            CustomLayoutActive = false;
        }

        private void EnsureCached()
        {
            if (cached) return;

            Transform head = person.transform.Find("Head");
            if (head != null) defaultHeadScale = head.localScale.x;

            foreach (string key in PositionKeys)
            {
                Transform limb = GetLimb(key);
                if (limb != null) defaultPositions[key] = limb.localPosition;
            }

            foreach (string key in AnchorKeys)
            {
                Transform limb = GetLimb(key);
                if (limb == null) continue;
                HingeJoint2D joint = limb.GetComponent<HingeJoint2D>();
                if (joint != null) defaultAnchors[key] = joint.anchor;
            }

            cached = true;
        }

        private Transform GetLimb(string key)
        {
            Transform root = person.transform;
            switch (key)
            {
                case "Head": return root.Find("Head");
                case "Upper": return root.Find("Body/UpperBody");
                case "Middle": return root.Find("Body/MiddleBody");
                case "Lower": return root.Find("Body/LowerBody");
                case "UArmF": return root.Find("FrontArm/UpperArmFront");
                case "UArmB": return root.Find("BackArm/UpperArm");
                case "LArmF": return root.Find("FrontArm/LowerArmFront");
                case "LArmB": return root.Find("BackArm/LowerArm");
                case "ULegF": return root.Find("FrontLeg/UpperLegFront");
                case "ULegB": return root.Find("BackLeg/UpperLeg");
                case "LLegF": return root.Find("FrontLeg/LowerLegFront");
                case "LLegB": return root.Find("BackLeg/LowerLeg");
                case "FootF": return root.Find("FrontLeg/FootFront");
                case "FootB": return root.Find("BackLeg/Foot");
                default: return null;
            }
        }

        private void SetLocalPosition(string key, Vector3 position)
        {
            Transform limb = GetLimb(key);
            if (limb != null) limb.localPosition = position;
        }

        private void SetJointAnchor(string key, Vector2 anchor)
        {
            Transform limb = GetLimb(key);
            if (limb == null) return;
            HingeJoint2D joint = limb.GetComponent<HingeJoint2D>();
            if (joint != null) joint.anchor = anchor;
        }
    }
}
