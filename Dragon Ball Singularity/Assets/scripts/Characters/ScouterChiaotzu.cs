using System;
using UnityEngine;
using UnityEngine.Events;

namespace DBPU
{
    public static class ScouterChiaotzuAssets
    {
        const string Root = "Assets/Sprites/Characters/Chiaotzu/";

        static bool _loaded;

        public static Sprite ChiHat;
        public static Sprite ChiHair;

        public static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;

            ChiHat = ModAPI.LoadSprite(Root + "Hat.png");
            ChiHair = ModAPI.LoadSprite(Root + "hair.png");
        }
    }

    public class ChiaotzuBehaviour : MonoBehaviour
    {
        public GameObject hairObject;
        public SpriteRenderer hairSprite;
        bool menusAdded;
        bool setupDone;

        public void SetupNow()
        {
            ScouterChiaotzuAssets.EnsureLoaded();
            ApplyProportions();
            EnsureHat();
            EnsureMenus();
            setupDone = true;
        }

        void Start()
        {
            if (!setupDone) SetupNow();
            CeceUtils.DelayedInvoke(0.15f, () =>
            {
                if (this == null) return;
                ApplyProportions();
                EnsureHat();
            });
        }

        void EnsureHat()
        {
            var headSr = GetComponent<SpriteRenderer>();
            if (hairObject == null)
            {
                var existing = transform.Find("hair");
                if (existing != null) hairObject = existing.gameObject;
            }
            if (hairObject == null)
            {
                hairObject = new GameObject("hair");
                hairObject.transform.SetParent(transform, false);
                hairObject.transform.localPosition = Vector3.zero;
                hairObject.transform.localRotation = Quaternion.identity;
                hairObject.transform.localScale = Vector3.one;
                hairSprite = hairObject.AddComponent<SpriteRenderer>();
            }
            if (hairSprite == null)
                hairSprite = hairObject.GetComponent<SpriteRenderer>() ?? hairObject.AddComponent<SpriteRenderer>();

            if (hairSprite.sprite == null)
                hairSprite.sprite = ScouterChiaotzuAssets.ChiHat ?? ScouterChiaotzuAssets.ChiHair;

            if (headSr != null)
            {
                hairSprite.sortingLayerName = headSr.sortingLayerName;
                hairSprite.sortingOrder = headSr.sortingOrder + 2;
            }
            else
            {
                hairSprite.sortingLayerName = "Foreground";
                hairSprite.sortingOrder = 10;
            }
        }

        void EnsureMenus()
        {
            if (menusAdded) return;
            var headPhys = GetComponent<PhysicalBehaviour>();
            if (headPhys == null) return;
            menusAdded = true;

            headPhys.ContextMenuOptions.Buttons.Add(new ContextMenuButton("Hat", "Put on hat", "Put on hat",
                new UnityAction[] { () => { EnsureHat(); if (ScouterChiaotzuAssets.ChiHat != null) hairSprite.sprite = ScouterChiaotzuAssets.ChiHat; } }));
            headPhys.ContextMenuOptions.Buttons.Add(new ContextMenuButton("Hair", "Take off hat", "Take off hat",
                new UnityAction[] { () => { EnsureHat(); if (ScouterChiaotzuAssets.ChiHair != null) hairSprite.sprite = ScouterChiaotzuAssets.ChiHair; } }));
        }

        public void ApplyProportions()
        {
            var head = transform;
            var root = transform.root;
            var arm1 = root.Find("FrontArm")?.Find("LowerArmFront");
            var arm2 = root.Find("BackArm")?.Find("LowerArm");
            var ulegf = root.Find("FrontLeg")?.Find("UpperLegFront");
            var ulegb = root.Find("BackLeg")?.Find("UpperLeg");
            var llegf = root.Find("FrontLeg")?.Find("LowerLegFront");
            var llegb = root.Find("BackLeg")?.Find("LowerLeg");
            var upper = root.Find("Body")?.Find("UpperBody");
            var middle = root.Find("Body")?.Find("MiddleBody");
            var lower = root.Find("Body")?.Find("LowerBody");
            var uarmf = root.Find("FrontArm")?.Find("UpperArmFront");
            var uarmb = root.Find("BackArm")?.Find("UpperArm");
            var footB = root.Find("BackLeg")?.Find("Foot");
            var footF = root.Find("FrontLeg")?.Find("FootFront");
            if (upper == null || middle == null || lower == null || arm1 == null || arm2 == null) return;

            SetAnchor(head, new Vector2(0f, -0.1428571f));
            SetAnchor(upper, new Vector2(0f, -0.1f));
            SetAnchor(lower, new Vector2(0f, 0.07142857f));
            SetAnchor(uarmf, new Vector2(0f, 0.04285714f));
            SetAnchor(uarmb, new Vector2(0f, 0.04285714f));
            SetAnchor(arm2, new Vector2(0f, 0.1285714f));
            SetAnchor(arm1, new Vector2(0f, 0.1285714f));
            SetAnchor(ulegf, new Vector2(0f, 0.1f));
            SetAnchor(ulegb, new Vector2(0f, 0.1f));
            SetAnchor(llegb, new Vector2(-0.01428571f, 0.1f));
            SetAnchor(llegf, new Vector2(-0.01428571f, 0.1f));
            SetAnchor(footB, new Vector2(-0.02857143f, 0.02857143f));
            SetAnchor(footF, new Vector2(-0.02857143f, 0.02857143f));

            head.localPosition = new Vector3(0f, 0.4f);
            upper.localPosition = new Vector3(0f, 0.1428571f);
            middle.localPosition = new Vector3(0f, 0f);
            lower.localPosition = new Vector3(0f, -0.1142857f);
            if (uarmf != null) uarmf.localPosition = new Vector3(0f, -0.36428571f);
            if (uarmb != null) uarmb.localPosition = new Vector3(0f, -0.36428571f);
            arm1.localPosition = new Vector3(0f, -0.5785714f);
            arm2.localPosition = new Vector3(0f, -0.5785714f);
            if (ulegf != null) ulegf.localPosition = new Vector3(0f, -0.2571429f);
            if (ulegb != null) ulegb.localPosition = new Vector3(0f, -0.2571429f);
            if (llegf != null) llegf.localPosition = new Vector3(0f, -0.4714286f);
            if (llegb != null) llegb.localPosition = new Vector3(0f, -0.4714286f);
            if (footB != null) footB.localPosition = new Vector3(0.01428571f, -0.6f);
            if (footF != null) footF.localPosition = new Vector3(0.01428571f, -0.6f);

            lower.GetComponent<SpriteRenderer>().sortingLayerName = "Foreground";
            middle.GetComponent<SpriteRenderer>().sortingLayerName = "Foreground";
            upper.GetComponent<SpriteRenderer>().sortingLayerName = "Foreground";
            lower.GetComponent<SpriteRenderer>().sortingOrder = 2;
            middle.GetComponent<SpriteRenderer>().sortingOrder = 1;
            var headSr = head.GetComponent<SpriteRenderer>();
            if (headSr != null) headSr.sortingOrder = Mathf.Max(headSr.sortingOrder, 4);
            upper.GetComponent<SpriteRenderer>().sortingOrder = 2;
            arm1.GetComponent<SpriteRenderer>().sortingOrder = 4;
            if (uarmf != null) uarmf.GetComponent<SpriteRenderer>().sortingOrder = 3;

            var person = root.GetComponent<PersonBehaviour>();
            DbpuAutoLimbCompat.SyncPerson(person);
            DbpuAutoLimbCompat.SyncPersonDelayed(person);
        }

        static void SetAnchor(Transform t, Vector2 anchor)
        {
            if (t == null) return;
            var joint = t.GetComponent<HingeJoint2D>();
            if (joint != null) joint.anchor = anchor;
        }
    }
}
