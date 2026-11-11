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

namespace PHY
{
    public static class Cro
    {
        public static LimbBehaviour GetLimb(this PersonBehaviour person, string limbName)
        {
            return person.Limbs.First((LimbBehaviour l) => l.name == limbName);
        }

        public static void CreatePhysicalCloth(Transform objForAttach, Vector2 connectedAnchor, string layerName, int order, Sprite[] sprites, float minAngleFirstPiece, float maxAngleFirstPiece, float minAngle, float maxAngle, float pieceMass = 0.0018f, float pieceInertia = 0.001f, /* int iden,*/ string Name = "Clothing Piece", bool HasCollision = true, bool CollideJustWithGround = true, bool flipX = false, float anchorTrim = 1.5f, bool horizontalChain = false)
        {
            if (objForAttach.TryGetComponent<HasPhysicalCloth>(out HasPhysicalCloth hasPhysicalCloth) && hasPhysicalCloth.Removed)
            {
                return;
            }

            Transform holder = new GameObject(Name).transform;
            holder.SetParent(objForAttach, false);
            holder.gameObject.AddComponent<Optout>();

            if (horizontalChain)
            {
                holder.localPosition = connectedAnchor;
                holder.localEulerAngles = new Vector3(0f, 0f, 90f);
            }

            PhysicalBehaviour phys = objForAttach.GetComponent<PhysicalBehaviour>();

            SortingGroup sortingGroup = holder.gameObject.AddComponent<SortingGroup>();
            sortingGroup.sortingLayerName = layerName;
            sortingGroup.sortingOrder = order;/*
            sortingGroup.sortingID = iden;*/

            int length = sprites.Length;
            HingeJoint2D[] pieces = new HingeJoint2D[length];

            Dictionary<SpriteRenderer, Sprite> ogs = new Dictionary<SpriteRenderer, Sprite>();

            for (int i = 0; i < length; i++)
            {
                GameObject piece = new GameObject("Cloth piece " + i);
                Transform pieceTransform = piece.transform;
                pieceTransform.SetParent(holder);
                pieceTransform.localScale = Vector3.one;
                if (horizontalChain) pieceTransform.localRotation = Quaternion.identity;
                else pieceTransform.rotation = objForAttach.rotation;
                piece.AddComponent<Optout>();

                Rigidbody2D rigidbody = piece.AddComponent<Rigidbody2D>();
                rigidbody.mass = pieceMass;
                rigidbody.inertia = pieceInertia;
                rigidbody.interpolation = RigidbodyInterpolation2D.Interpolate;

                Sprite sprite = sprites[i];
                SpriteRenderer spriteRenderer = piece.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = sprite;
                if (flipX) spriteRenderer.flipX = true;

                Vector2 jointAnchor;
                if (horizontalChain)
                {
                    float anchorMain = (sprite.rect.width / 2f * ModAPI.PixelSize) - (anchorTrim * ModAPI.PixelSize);
                    jointAnchor = new Vector2(0f, flipX ? -anchorMain : anchorMain);
                }
                else
                {
                    float anchorMain = (sprite.rect.height / 2f * ModAPI.PixelSize) - (anchorTrim * ModAPI.PixelSize);
                    jointAnchor = new Vector2(0f, anchorMain);
                }

                HingeJoint2D joint = piece.AddComponent<HingeJoint2D>();
                joint.anchor = jointAnchor;
                joint.motor = new JointMotor2D()
                {
                    motorSpeed = 0f,
                    maxMotorTorque = 0.00001f
                };
                joint.useMotor = true;
                joint.enableCollision = false;
                joint.autoConfigureConnectedAnchor = false;
                pieces[i] = joint;

                if (HasCollision)
                {
                    if (CollideJustWithGround) piece.layer = 10;
                    else piece.layer = 9;

                    BoxCollider2D collider = piece.AddComponent<BoxCollider2D>();
                    //collider.size = new Vector2(sprite.rect.width * ModAPI.PixelSize, sprite.rect.height * ModAPI.PixelSize);
                    collider.offset = jointAnchor;

                    var nocol = piece.AddComponent<NoCollide>();
                    nocol.NoCollideSetA = objForAttach.root.gameObject.GetComponentsInChildren<Collider2D>();
                    nocol.NoCollideSetB = objForAttach.root.gameObject.GetComponentsInChildren<Collider2D>();
                }

                // Store original sprite in the dictionary
                ogs.Add(spriteRenderer, sprite);

                // Set sorting order for cloth pieces
                SpriteRenderer limbSpriteRenderer = objForAttach.GetComponent<SpriteRenderer>();
                int clothSortingOrder = limbSpriteRenderer.sortingOrder;
                spriteRenderer.sortingOrder = clothSortingOrder;

                // Set initial position and joint angle limits
                if (i != 0)
                {
                    joint.limits = new JointAngleLimits2D
                    {
                        min = minAngle,
                        max = maxAngle
                    };

                    HingeJoint2D previousPiece = pieces[i - 1];
                    joint.connectedBody = previousPiece.attachedRigidbody;
                    joint.connectedAnchor = -previousPiece.anchor;
                    pieceTransform.localPosition = (Vector2)previousPiece.transform.localPosition - previousPiece.anchor - joint.anchor;

                    piece.gameObject.GetOrAddComponent<FollowDirection>().characterRoot = objForAttach.root.transform;
                    piece.gameObject.GetComponent<FollowDirection>().joint = piece.GetComponent<HingeJoint2D>();
                    piece.gameObject.GetComponent<FollowDirection>().Min = minAngle;
                    piece.gameObject.GetComponent<FollowDirection>().Max = maxAngle;
                }

                else
                {
                    pieceTransform.localPosition = horizontalChain ? (Vector3)(-jointAnchor) : (Vector3)(connectedAnchor - jointAnchor);
                    joint.connectedBody = phys.rigidbody;
                    joint.connectedAnchor = connectedAnchor;
                    spriteRenderer.maskInteraction = SpriteMaskInteraction.VisibleOutsideMask;

                    joint.limits = new JointAngleLimits2D
                    {
                        min = minAngleFirstPiece,
                        max = maxAngleFirstPiece
                    };

                    piece.gameObject.GetOrAddComponent<FollowDirection>().characterRoot = objForAttach.root.transform;
                    piece.gameObject.GetComponent<FollowDirection>().joint = piece.GetComponent<HingeJoint2D>();
                    piece.gameObject.GetComponent<FollowDirection>().Min = minAngleFirstPiece;
                    piece.gameObject.GetComponent<FollowDirection>().Max = maxAngleFirstPiece;
                }
            }
        }

        public class FollowDirection : MonoBehaviour
        {
            public Transform characterRoot;
            public HingeJoint2D joint;

            public bool Right = true;

            public float Min;
            public float Max;

            public void Start()
            {
                StartCoroutine(FixUpdate());
            }

            public IEnumerator FixUpdate()
            {
                while (true)
                {
                    if (characterRoot.localScale.x > 0 && Right == false)
                    {
                        Right = true;

                        joint.limits = new JointAngleLimits2D
                        {
                            min = Min,
                            max = Max
                        };
                    }

                    if (characterRoot.localScale.x < 0 && Right == true)
                    {
                        Right = false;

                        joint.limits = new JointAngleLimits2D
                        {
                            min = -Max,
                            max = -Min
                        };
                    }

                    yield return new WaitForSeconds(0.025f);
                }
            }
        }
    }
}
