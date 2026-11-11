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
    public class Child : MonoBehaviour
    {
        public void Start()
        {
            var head = transform;
            var arm1 = transform.root.transform.Find("FrontArm").Find("LowerArmFront");
            var arm2 = transform.root.transform.Find("BackArm").Find("LowerArm");
            var ulegf = transform.root.Find("FrontLeg").Find("UpperLegFront");
            var ulegb = transform.root.Find("BackLeg").Find("UpperLeg");
            var llegf = transform.root.Find("FrontLeg").Find("LowerLegFront");
            var llegb = transform.root.Find("BackLeg").Find("LowerLeg");
            var upper = transform.root.transform.Find("Body").Find("UpperBody");
            var middle = transform.root.transform.Find("Body").Find("MiddleBody");
            var lower = transform.root.Find("Body").Find("LowerBody");
            var uarmf = transform.root.Find("FrontArm").Find("UpperArmFront");
            var uamrb = transform.root.Find("BackArm").Find("UpperArm");
            var larmf = transform.root.Find("FrontArm").Find("LowerArmFront");
            var larmb = transform.root.Find("BackArm").Find("LowerArm");
            var FootB = transform.root.Find("BackLeg").Find("Foot");
            var FootF = transform.root.Find("FrontLeg").Find("FootFront");

            larmf.GetComponent<GripBehaviour>().GripPosition = new Vector3(0f, -0.1857143f, ModAPI.PixelSize * 6f);
            larmb.GetComponent<GripBehaviour>().GripPosition = new Vector3(0f, -0.1857143f, ModAPI.PixelSize * 6f);

            HingeJoint2D jointTU = upper.GetComponent<HingeJoint2D>();
            jointTU.anchor = new Vector2(0f, -0.1142857f);
            HingeJoint2D jointTD = lower.GetComponent<HingeJoint2D>();
            jointTD.anchor = new Vector2(0f, 0.1571429f);
            HingeJoint2D jointAUF = uarmf.GetComponent<HingeJoint2D>();
            jointAUF.anchor = new Vector2(0f, 0.1f);
            HingeJoint2D jointAUB = uamrb.GetComponent<HingeJoint2D>();
            jointAUB.anchor = new Vector2(0f, 0.1f);
            HingeJoint2D jointALB = arm2.GetComponent<HingeJoint2D>();
            jointALB.anchor = new Vector2(0f, 0.2f);
            HingeJoint2D jointALF = arm1.GetComponent<HingeJoint2D>();
            jointALF.anchor = new Vector2(0f, 0.2f);
            HingeJoint2D jointLUF = ulegf.GetComponent<HingeJoint2D>();
            jointLUF.anchor = new Vector2(0f, 0.1857143f);
            HingeJoint2D jointLUB = ulegb.GetComponent<HingeJoint2D>();
            jointLUB.anchor = new Vector2(0f, 0.1857143f);
            HingeJoint2D jointLLB = llegb.GetComponent<HingeJoint2D>();
            jointLLB.anchor = new Vector2(0f, 0.1857143f);
            HingeJoint2D jointLLF = llegf.GetComponent<HingeJoint2D>();
            jointLLF.anchor = new Vector2(0f, 0.1857143f);

            head.transform.localPosition = new Vector3(0f, 0.4857143f);
            upper.transform.localPosition = new Vector3(0f, 0.2f);
            middle.transform.localPosition = new Vector3(0f, 0f);
            lower.transform.localPosition = new Vector3(0f, -0.2428571f);
            uarmf.transform.localPosition = new Vector3(0f, -0.36428571f);
            uamrb.transform.localPosition = new Vector3(0f, -0.36428571f);
            arm1.transform.localPosition = new Vector3(0f, -0.7214286f);
            arm2.transform.localPosition = new Vector3(0f, -0.7214286f);
            ulegf.transform.localPosition = new Vector3(0f, -0.5196429f);
            ulegb.transform.localPosition = new Vector3(0f, -0.5196429f);
            llegf.transform.localPosition = new Vector3(0f, -0.8625f);
            llegb.transform.localPosition = new Vector3(0f, -0.8625f);
            FootB.transform.localPosition = new Vector3(0.03928571f, -1.091071f);
            FootF.transform.localPosition = new Vector3(0.03928571f, -1.091071f);

            CeceUtils.DelayedInvoke(1f, () =>
            {
                Destroy(gameObject.GetComponent<Child>());
            });
        }
    }
}
