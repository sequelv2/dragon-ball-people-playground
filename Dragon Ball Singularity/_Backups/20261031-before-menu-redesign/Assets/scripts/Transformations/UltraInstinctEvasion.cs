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
    public class UltraInstinctEvasion : MonoBehaviour
    {
        public float UiChance = 0.45f;
        public float MuiChance = 0.75f;
        public float DodgeDuration = 0.25f;

        static readonly Dictionary<long, bool> FrameRollCache = new Dictionary<long, bool>();
        static readonly List<UltraInstinctEvasion> ActiveEvaders = new List<UltraInstinctEvasion>();
        static bool _globalHooksReady;

        PersonBehaviour _person;
        UltraInstinctEvasionLimb[] _limbs;
        bool _dodging;
        bool _limbsReady;
        float _nextEvasionRoll;

        public bool IsDodging => _dodging;

        void Awake()
        {
            _person = GetComponent<PersonBehaviour>();
            if (_person == null) _person = GetComponentInChildren<PersonBehaviour>();
        }

        void OnEnable()
        {
            if (!ActiveEvaders.Contains(this)) ActiveEvaders.Add(this);
            EnsureGlobalHooks();
        }

        void OnDisable()
        {
            ActiveEvaders.Remove(this);
        }

        void Start()
        {
            EnsureLimbs();
        }

        static void EnsureGlobalHooks()
        {
            if (_globalHooksReady) return;
            _globalHooksReady = true;

            ModAPI.OnGunShot += (_, gun) =>
            {
                if (gun == null) return;
                for (int i = 0; i < ActiveEvaders.Count; i++)
                {
                    var evasion = ActiveEvaders[i];
                    if (evasion != null && evasion.isActiveAndEnabled)
                        evasion.OnGunShot(gun);
                }
            };

            ModAPI.OnItemSpawned += (_, sender) =>
            {
                if (sender.Instance == null || !sender.Instance.TryGetComponent(out ExplosiveBehaviour explosive)) return;
                explosive.OnExplode.AddListener(() =>
                {
                    for (int i = 0; i < ActiveEvaders.Count; i++)
                    {
                        var evasion = ActiveEvaders[i];
                        if (evasion != null && evasion.isActiveAndEnabled)
                            evasion.OnExplosion(explosive.transform.position);
                    }
                });
            };
        }

        void OnGunShot(FirearmBehaviour gun)
        {
            if (_person == null || _person.Limbs == null || _person.Limbs.Length == 0 || !IsActiveFor(_person)) return;
            float dist = Vector3.Distance(gun.transform.position, _person.Limbs[0].transform.position);
            float delay = dist / gun.Cartridge.StartSpeed / 50f - 0.02f;
            TryDodge(Mathf.Max(0f, delay));
        }

        void OnExplosion(Vector3 position)
        {
            if (_person == null || !IsActiveFor(_person)) return;
            if (Vector3.Distance(position, MCP_Utils.GetMiddlePositionOfBody(_person)) > 8f) return;
            TryDodge(0f);
        }

        void EnsureLimbs()
        {
            if (_limbsReady || _person == null || _person.Limbs == null) return;

            _limbs = new UltraInstinctEvasionLimb[_person.Limbs.Length];
            for (int i = 0; i < _person.Limbs.Length; i++)
            {
                var limb = _person.Limbs[i];
                if (limb == null) continue;

                var limbEvasion = limb.gameObject.GetComponent<UltraInstinctEvasionLimb>();
                if (limbEvasion == null) limbEvasion = limb.gameObject.AddComponent<UltraInstinctEvasionLimb>();
                limbEvasion.Limb = limb;
                limbEvasion.Root = this;
                limbEvasion.EnsureTrigger();
                _limbs[i] = limbEvasion;
            }

            _limbsReady = true;
        }

        public static PersonBehaviour PersonFromPhys(PhysicalBehaviour phys)
        {
            if (phys == null) return null;
            return phys.transform.root.GetComponent<PersonBehaviour>();
        }

        public static PersonBehaviour FindAttackerNearPoint(PersonBehaviour victim, Vector3 point, float maxRadius = 3f)
        {
            if (victim == null) return null;

            PersonBehaviour best = null;
            float bestDist = maxRadius;

            var all = UnityEngine.Object.FindObjectsOfType<PersonBehaviour>();
            for (int i = 0; i < all.Length; i++)
            {
                var p = all[i];
                if (p == null || p == victim || !p.IsAlive()) continue;

                float d = Vector3.Distance(MCP_Utils.GetMiddlePositionOfBody(p), point);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = p;
                }
            }

            return best;
        }

        public static bool TryEvade(LimbBehaviour limb, Vector3? attackPoint = null, PersonBehaviour attacker = null)
        {
            return false;
        }

        public static bool IsUltraInstinctForm(string form)
        {
            return form == "UI" || form == "MUI";
        }

        public static string GetActiveForm(PersonBehaviour person)
        {
            if (person == null) return "Base";

            var headTransform = person.transform.Find("Head");
            if (headTransform != null)
            {
                var headMenu = headTransform.GetComponent<TransformationContextMenu>();
                if (headMenu != null && !string.IsNullOrEmpty(headMenu.Form))
                    return headMenu.Form;
            }

            if (person.Limbs != null)
            {
                for (int i = 0; i < person.Limbs.Length; i++)
                {
                    var limb = person.Limbs[i];
                    if (limb == null) continue;
                    var menu = limb.GetComponent<TransformationContextMenu>();
                    if (menu != null && !string.IsNullOrEmpty(menu.Form))
                        return menu.Form;
                }
            }

            var ki = person.GetComponent<KiSystem>();
            if (ki != null && !string.IsNullOrEmpty(ki.CurrentForm))
                return ki.CurrentForm;

            return "Base";
        }

        public static bool IsActiveFor(PersonBehaviour person)
        {
            return false;
        }

        public void RequestDodge(float delay)
        {
            if (!IsActiveFor(_person)) return;
            TryDodge(delay);
        }

        bool TryDodge(float delay)
        {
            if (_person == null || !_person.IsAlive() || !IsActiveFor(_person)) return false;
            if (Time.time < _nextEvasionRoll) return false;

            string form = GetActiveForm(_person);
            float chance = form == "MUI" ? MuiChance : UiChance;
            if (!_dodging && UnityEngine.Random.value > chance) return false;

            _nextEvasionRoll = Time.time + 0.08f;
            StopAllCoroutines();
            StartCoroutine(DodgeRoutine(delay));
            return true;
        }

        IEnumerator DodgeRoutine(float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);

            EnsureLimbs();
            if (_person == null || !_person.IsAlive() || _limbs == null) yield break;

            _dodging = true;

            for (int i = 0; i < _limbs.Length; i++)
            {
                var limbEvasion = _limbs[i];
                if (limbEvasion == null || limbEvasion.Limb == null) continue;
                var limb = limbEvasion.Limb;
                if (limb.Collider != null) limb.Collider.enabled = false;
                if (limb.PhysicalBehaviour != null && limb.PhysicalBehaviour.rigidbody != null)
                    limb.PhysicalBehaviour.rigidbody.constraints = RigidbodyConstraints2D.FreezeAll;
                limbEvasion.ShouldDoDodgeEffect = true;
            }

            yield return new WaitForSeconds(DodgeDuration);

            for (int i = 0; i < _limbs.Length; i++)
            {
                var limbEvasion = _limbs[i];
                if (limbEvasion == null || limbEvasion.Limb == null) continue;
                var limb = limbEvasion.Limb;
                limbEvasion.ShouldDoDodgeEffect = false;
                if (limb.Collider != null) limb.Collider.enabled = true;
                if (limb.PhysicalBehaviour != null && limb.PhysicalBehaviour.rigidbody != null)
                    limb.PhysicalBehaviour.rigidbody.constraints = RigidbodyConstraints2D.None;
            }

            _dodging = false;
        }

        public class UltraInstinctEvasionLimb : MonoBehaviour
        {
            public LimbBehaviour Limb;
            public UltraInstinctEvasion Root;
            GameObject _triggerObject;
            GameObject _childSprite;
            bool _doDodge;
            Coroutine _createGhostCo;

            public bool ShouldDoDodgeEffect
            {
                get { return _doDodge; }
                set
                {
                    if (_doDodge == value) return;
                    if (value)
                    {
                        if (Root != null && Root._person != null)
                        {
                            foreach (var sr in Root._person.GetComponentsInChildren<SpriteRenderer>(true))
                            {
                                if (sr == null) continue;
                                if (IsDodgeGhost(sr.transform)) continue;
                                sr.enabled = false;
                            }
                        }

                        if (_createGhostCo != null) StopCoroutine(_createGhostCo);
                        _createGhostCo = StartCoroutine(CreateGhostNextFrame());
                    }
                    else
                    {
                        if (_createGhostCo != null)
                        {
                            StopCoroutine(_createGhostCo);
                            _createGhostCo = null;
                        }
                        if (_childSprite != null)
                        {
                            Destroy(_childSprite);
                            _childSprite = null;
                        }
                        if (Root != null && Root._person != null)
                        {
                            foreach (var sr in Root._person.GetComponentsInChildren<SpriteRenderer>(true))
                            {
                                if (sr == null) continue;
                                if (IsDodgeGhost(sr.transform)) continue;
                                sr.enabled = true;
                            }
                        }
                    }
                    _doDodge = value;
                }
            }

            static bool IsDodgeGhost(Transform t)
            {
                while (t != null)
                {
                    if (t.name == "UIDodgeGhost") return true;
                    t = t.parent;
                }
                return false;
            }

            IEnumerator CreateGhostNextFrame()
            {
                yield return null;
                _createGhostCo = null;
                if (!_doDodge || Limb == null || Limb.SkinMaterialHandler == null || Limb.SkinMaterialHandler.renderer == null)
                    yield break;

                var src = Limb.SkinMaterialHandler.renderer;
                if (_childSprite != null) Destroy(_childSprite);

                _childSprite = new GameObject("UIDodgeGhost");
                _childSprite.transform.SetParent(transform, false);
                _childSprite.transform.localPosition = Vector3.zero;
                _childSprite.transform.localRotation = Quaternion.identity;
                _childSprite.transform.localScale = Vector3.one;

                var ghostSr = _childSprite.AddComponent<SpriteRenderer>();
                ghostSr.sprite = src.sprite;
                ghostSr.color = src.color;
                ghostSr.flipX = src.flipX;
                ghostSr.flipY = src.flipY;
                ghostSr.sortingLayerName = src.sortingLayerName;
                ghostSr.sortingOrder = src.sortingOrder;
            }

            void FixedUpdate()
            {
                if (!_doDodge || _childSprite == null) return;
                float shakeOffset = Mathf.Sin(Time.time * 100f) / 2f;
                _childSprite.transform.position = new Vector3(
                    transform.position.x + shakeOffset,
                    transform.position.y,
                    transform.position.z);
            }

            public void EnsureTrigger()
            {
                if (_triggerObject != null) return;

                _triggerObject = new GameObject("UIEvasionTrigger");
                _triggerObject.layer = 9;
                _triggerObject.transform.SetParent(transform);
                _triggerObject.transform.localPosition = Vector3.zero;

                var trigger = _triggerObject.AddComponent<CircleCollider2D>();
                trigger.radius = 5f;
                trigger.isTrigger = true;

                var trig = _triggerObject.AddComponent<UltraInstinctEvasionTrigger>();
                trig.Root = Root;
            }

            void OnDisable()
            {
                ShouldDoDodgeEffect = false;
            }
        }

        class UltraInstinctEvasionTrigger : MonoBehaviour
        {
            public UltraInstinctEvasion Root;

            void OnTriggerStay2D(Collider2D other)
            {
                if (Root == null || other.transform.root == transform.root) return;
                if (other.gameObject.layer != 9 && other.gameObject.layer != 13) return;

                var rb = other.attachedRigidbody;
                float speed = rb != null ? rb.velocity.magnitude : 0.4f;
                if (Vector3.Distance(other.transform.position, transform.position) > Mathf.Max(speed, 0.4f) / 4f) return;

                Root.RequestDodge(0f);
            }
        }
    }
}
