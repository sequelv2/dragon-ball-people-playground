using UnityEngine;

namespace PeoplePlayground.VFX
{
    public enum SSJForm
    {
        SSJ1 = 1,
        SSJ2 = 2,
        SSJ3 = 3
    }

    /// <summary>
    /// Контроллер ауры SSJ1 и био-молний SSJ2/SSJ3 (60 FPS) для PersonBehaviour в People Playground.
    /// Управляет основным слоем пламени (SSJ1Aura.shader) и слоем дуговых разрядов (SSJLightningVFX.shader).
    /// (The user's controller, 2026-11-07, adapted for the game: the aura is an egg round the fighter measured
    /// from the limbs every frame; it follows the fighter exactly, only its size is eased; the lightning quad is
    /// centred on the body and sized from it. RefinedAuraVisual.AttachUserAura builds it and feeds Form, Hdr,
    /// Charge and Opacity.)
    /// </summary>
    public class SSJ1AuraController : MonoBehaviour
    {
        // The shaders take this many limb capsules (a People Playground human has 14 limbs).
        public const int MaxLimbs = 14;

        [Header("Target Ragdoll & Transformation")]
        public PersonBehaviour Person;
        public SSJForm CurrentForm = SSJForm.SSJ1;

        [Header("Materials")]
        public Material AuraMaterial;
        public Material LightningMaterial;

        [Header("Live values")]
        public float Hdr, Charge, Opacity = 1;

        private GameObject _auraQuadObj;
        private GameObject _lightningQuadObj;
        private MeshRenderer _auraRenderer;
        private MeshRenderer _lightningRenderer;
        private MaterialPropertyBlock _auraPropBlock;
        private MaterialPropertyBlock _boltPropBlock;

        private readonly Vector4[] _limbCapsules = new Vector4[MaxLimbs];
        private readonly float[] _limbRadii = new float[MaxLimbs];
        private readonly Collider2D[] _colliders = new Collider2D[MaxLimbs];
        // The body's half size and its centre's offset from the limbs' centre, eased; its position is not.
        private Vector2 _halfSize, _centreOffset;
        private bool _hasBody;

        private static readonly int LimbCapsulesId = Shader.PropertyToID("_LimbCapsules");
        private static readonly int LimbRadiiId = Shader.PropertyToID("_LimbRadii");
        private static readonly int EggId = Shader.PropertyToID("_Egg");
        private static readonly int Egg2Id = Shader.PropertyToID("_Egg2");
        private static readonly int TurbulenceId = Shader.PropertyToID("_Turbulence");

        public bool LightningShown { get { return _lightningQuadObj != null && _lightningQuadObj.activeSelf; } }
        public Vector3 AuraCentre { get { return _auraQuadObj != null ? _auraQuadObj.transform.position : Vector3.zero; } }

        private DBPU.SaiyanLightningBolts _bolts;

        private void Awake()
        {
            _auraPropBlock = new MaterialPropertyBlock();
            _boltPropBlock = new MaterialPropertyBlock();
            _bolts = new DBPU.SaiyanLightningBolts(GetInstanceID());
            CreateVFXQuads();
        }

        private GameObject Quad(string name, Material material, string layer, int order)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(transform, false);
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingLayerName = layer;
            renderer.sortingOrder = order;
            return quad;
        }

        private void CreateVFXQuads()
        {
            // 1. Базовый слой пламенной ауры (за рэгдоллом)
            _auraQuadObj = Quad("SSJ_Aura_Base_Quad", AuraMaterial, "Background", -5);
            _auraRenderer = _auraQuadObj.GetComponent<MeshRenderer>();
            // 2. Поверхностный слой био-молний SSJ2 / SSJ3 (поверх ауры и конечностей)
            _lightningQuadObj = Quad("SSJ2_3_Lightning_VFX_Quad", LightningMaterial, "Top", 8);
            _lightningRenderer = _lightningQuadObj.GetComponent<MeshRenderer>();
            _lightningQuadObj.SetActive(false);
        }

        private void LateUpdate()
        {
            if (Person == null || Person.Limbs == null || Person.Limbs.Length == 0 || _auraRenderer == null)
                return;

            // The fighter's extent, from where every limb is drawn this frame (transforms, not physics bounds,
            // which trail the interpolated sprites when the fighter moves fast).
            int count = Mathf.Min(Person.Limbs.Length, MaxLimbs);
            Vector2 lo = new Vector2(float.MaxValue, float.MaxValue), hi = new Vector2(float.MinValue, float.MinValue);
            Vector2 limbCentre = Vector2.zero;
            int limbs = 0;
            for (int i = 0; i < count; i++)
            {
                LimbBehaviour limb = Person.Limbs[i];
                // LimbBehaviour.Collider is not always filled in; the limb's own collider is the same shape.
                Collider2D collider = limb == null ? null : limb.Collider ? limb.Collider : limb.GetComponent<Collider2D>();
                _colliders[i] = collider;
                if (collider == null) continue;
                Vector2 at = limb.transform.position;
                Vector2 extents = collider.bounds.extents;
                lo = Vector2.Min(lo, at - extents); hi = Vector2.Max(hi, at + extents);
                limbCentre += at; limbs++;
            }
            for (int i = count; i < MaxLimbs; i++) _colliders[i] = null;
            if (limbs == 0) return;
            limbCentre /= limbs;
            Vector2 half = (hi - lo) * .5f, offset = (hi + lo) * .5f - limbCentre;
            // Only the size and the shape are eased, so the egg breathes instead of twitching with every limb;
            // where it stands follows the fighter exactly, however fast.
            float ease = _hasBody ? 1f - Mathf.Exp(-Time.deltaTime * 14f) : 1f;
            _halfSize = Vector2.Lerp(_halfSize, half, ease);
            _centreOffset = Vector2.Lerp(_centreOffset, offset, ease);
            _hasBody = true;
            Vector2 bodyCentre = limbCentre + _centreOffset;
            Vector2 bodyMin = bodyCentre - _halfSize, bodyMax = bodyCentre + _halfSize;

            // The egg: as wide again as the shoulders, from just under the feet to well above the head.
            float form = CurrentForm == SSJForm.SSJ3 ? 1.12f : CurrentForm == SSJForm.SSJ2 ? 1.06f : 1f;
            float unit = Mathf.Max(.3f, (bodyMax.y - bodyMin.y) / 2.3f) * (1f + Charge * .2f);
            float cx = bodyCentre.x;
            float halfWidth = Mathf.Max((bodyMax.x - bodyMin.x) * .5f + .38f * unit, .92f * unit) * form;
            float top = bodyMax.y + .8f * unit * form, bottom = bodyMin.y - .3f * unit;
            float cy = bottom + (top - bottom) * .44f;
            Vector2 quadHalf = new Vector2(halfWidth + .75f * unit, (top - bottom) * .5f + .55f * unit);
            Vector3 centre = new Vector3(cx, (top + bottom) * .5f, 0f);
            _auraQuadObj.transform.position = new Vector3(centre.x, centre.y, 0.05f);
            _auraQuadObj.transform.rotation = Quaternion.identity;
            _auraQuadObj.transform.localScale = new Vector3(quadHalf.x * 2f, quadHalf.y * 2f, 1f);

            for (int i = 0; i < MaxLimbs; i++)
            {
                Collider2D collider = _colliders[i];
                if (collider == null)
                {
                    // An absent limb (cut off, or a smaller ragdoll) must not leave a stale capsule behind.
                    _limbCapsules[i] = new Vector4(-99, -99, -99, -99);
                    _limbRadii[i] = 0;
                    continue;
                }
                Transform limb = collider.transform;
                Vector2 extents = collider.bounds.extents;
                Vector3 along = limb.up * (extents.y * 0.75f);
                Vector3 a = limb.position - along - centre, z = limb.position + along - centre;
                _limbCapsules[i] = new Vector4(a.x, a.y, z.x, z.y);
                _limbRadii[i] = Mathf.Clamp(Mathf.Min(extents.x, extents.y), .04f, .24f);
            }

            // Обновляем параметры ауры в зависимости от формы (SSJ1 / SSJ2 / SSJ3)
            _auraRenderer.GetPropertyBlock(_auraPropBlock);
            _auraPropBlock.SetVectorArray(LimbCapsulesId, _limbCapsules);
            _auraPropBlock.SetFloatArray(LimbRadiiId, _limbRadii);
            _auraPropBlock.SetVector(EggId, new Vector4(cx - centre.x, cy - centre.y, halfWidth, top - cy));
            _auraPropBlock.SetVector(Egg2Id, new Vector4(cy - bottom, quadHalf.x, quadHalf.y, bodyMin.y - centre.y));
            _auraPropBlock.SetFloat(TurbulenceId, CurrentForm == SSJForm.SSJ1 ? 2.1f : 2.9f);
            _auraPropBlock.SetFloat("_Hdr", Hdr);
            _auraPropBlock.SetFloat("_Charge", Charge);
            _auraPropBlock.SetFloat("_Opacity", Opacity);
            _auraRenderer.SetPropertyBlock(_auraPropBlock);

            // Включаем молнии для SSJ2 и SSJ3: flashes of jagged lightning round the aura (SaiyanLightningBolts,
            // after the user's first reference GIF). The points are relative to the quad's centre, so a flash
            // moves with the fighter.
            bool lightning = CurrentForm >= SSJForm.SSJ2 && LightningMaterial != null;
            float bodyUnit = Mathf.Max(.3f, (bodyMax.y - bodyMin.y) / 2.3f);
            if (lightning)
                _bolts.Step(Time.deltaTime, (int)CurrentForm, Charge, new Vector2(cx - centre.x, cy - centre.y), halfWidth, top - cy, cy - bottom, bodyUnit);
            bool showLightning = lightning && _bolts.On && Opacity > .01f;
            if (_lightningQuadObj.activeSelf != showLightning) _lightningQuadObj.SetActive(showLightning);
            if (showLightning)
            {
                _lightningQuadObj.transform.position = new Vector3(centre.x, centre.y, -0.05f);
                _lightningQuadObj.transform.rotation = Quaternion.identity;
                _lightningQuadObj.transform.localScale = new Vector3(quadHalf.x * 2f, quadHalf.y * 2f, 1f);
                _lightningRenderer.GetPropertyBlock(_boltPropBlock);
                _boltPropBlock.SetVectorArray("_Pts", _bolts.Points);
                _boltPropBlock.SetFloat("_Count", _bolts.Count);
                _boltPropBlock.SetVector("_QuadHalf", new Vector4(quadHalf.x, quadHalf.y, 0, 0));
                _boltPropBlock.SetVector("_Look", new Vector4(_bolts.GlowWidth, _bolts.BlurWidth, _bolts.Brightness, 0));
                _boltPropBlock.SetVectorArray(LimbCapsulesId, _limbCapsules);
                _boltPropBlock.SetFloatArray(LimbRadiiId, _limbRadii);
                _boltPropBlock.SetFloat("_Hdr", Hdr);
                _boltPropBlock.SetFloat("_Opacity", Opacity);
                _lightningRenderer.SetPropertyBlock(_boltPropBlock);
            }
        }

        private void OnDestroy()
        {
            if (_auraQuadObj != null) Destroy(_auraQuadObj);
            if (_lightningQuadObj != null) Destroy(_lightningQuadObj);
        }
    }
}
