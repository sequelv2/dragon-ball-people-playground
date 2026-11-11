using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DBPU
{
    // Live combat gauges above a fighter in the native Detail view. Only the Output dial takes
    // pointer input; the bars and text let clicks through to the world.
    [DefaultExecutionOrder(-260)]
    public sealed class CombatHudView : MonoBehaviour
    {
        static Canvas canvas;
        static readonly Dictionary<TMP_FontAsset, Material> textMaterials = new Dictionary<TMP_FontAsset, Material>();
        CombatPowerController power;
        RectTransform panel, numberRt, chipRt, dialRt;
        CanvasGroup group;
        CombatHudGraphic art;
        TextMeshProUGUI number, header, output, outputCaption, kiText, stmText, strainText, shieldText, shieldLabel, chipText;
        Image chipBack;
        Transform head;
        double shownPower, punchFrom;
        CombatHudOutputButton outputButton;
        static CombatHudView pointerOwner;
        static int releasedFrame = -1;
        Global pointerBlocker;
        int pointerButton, pressedFrame, releaseRequestedFrame = -1, clickedFrame = -1;
        bool motionReady;
        Vector3 previousHead;
        // Raw spring state, the filtered fighter velocity and the soft-limited offset/angle that are drawn.
        Vector2 motionOffset, motionVelocity, smoothedVelocity, shownOffset;
        float motionAngle, angularVelocity, shownAngle, visibility;
        int previousWidth, previousHeight;
        SpriteRenderer headSprite;
        LimbBehaviour headLimb;
        GameObject statusObject;
        Renderer[] statusRenderers;
        readonly Vector3[] corners = new Vector3[4];

        // Displayed values: text is rewritten only when what it shows changes.
        int shownKi = -1, shownStamina = -1, shownStrain = -1, shownShield = -1, shownOutput = -1;
        float numberRefreshAt, kiGhostHold, staminaGhostHold, punch, outputPulse, lastOutput = -1;
        string shownForm, shownChip;
        bool shownShielding, shownBroken;
        Color formAccent = DbTheme.Ki;
        string formName = "";

        // Inertia tuning: about 1.7 Hz with light damping, so a hard stop gives one visible sway,
        // a small counter-swing and a calm settle (~1 s).
        const float Stiffness = 120, Damping = 8.5f, Carry = .30f, Drag = .012f, VelocitySmoothing = .04f;
        const float AngleStiffness = 420, AngleDamping = 24, AnglePerPixel = .11f, MaxAngle = 3;
        const float FadeInTime = .16f, FadeOutTime = .12f;
        const float Width = 640, Height = 190;
        static readonly Color TextColor = new Color(.95f, .97f, 1f);
        static readonly Color BaseAccent = new Color(.55f, .78f, 1f);
        static readonly Color SparkAccent = new Color(.72f, .36f, 1f);
        static readonly Color ExhaustAccent = new Color(1f, .36f, .3f);

        public RectTransform Panel { get { return panel; } }
        public CombatHudGraphic Art { get { return art; } }
        public CombatHudOutputButton OutputButton { get { return outputButton; } }
        public Vector2 MotionOffset { get { return shownOffset; } }
        public float MotionAngle { get { return shownAngle; } }
        public float Visibility { get { return visibility; } }
        // The HUD belongs to the native Detail view (S by default), like limb health.
        public static bool DetailViewActive { get { return Global.main && Global.main.ShowLimbStatus; } }
        public static bool InputCaptured { get { return pointerOwner || Time.frameCount <= releasedFrame; } }

        public static CombatHudView Bind(PersonBehaviour person)
        {
            if (!person) return null;
            var view = person.GetComponent<CombatHudView>();
            return view ? view : person.gameObject.AddComponent<CombatHudView>();
        }

        void Start()
        {
            power = CombatPowerController.Bind(GetComponent<PersonBehaviour>());
            Build();
        }

        void Build()
        {
            if (!canvas)
            {
                var go = new GameObject("DBPU Combat HUD", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
                DontDestroyOnLoad(go);
                canvas = go.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;
            }
            DbAtlas.Ensure();

            panel = new GameObject("Live combat gauges", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
            panel.SetParent(canvas.transform, false);
            panel.anchorMin = panel.anchorMax = Vector2.zero;
            panel.pivot = Vector2.zero;
            panel.sizeDelta = new Vector2(Width, Height);
            group = panel.GetComponent<CanvasGroup>();
            group.blocksRaycasts = true;
            group.interactable = true;
            art = panel.gameObject.AddComponent<CombatHudGraphic>();
            art.raycastTarget = false;

            dialRt = new GameObject("Change Ki Output", typeof(RectTransform)).GetComponent<RectTransform>();
            dialRt.SetParent(panel, false);
            dialRt.anchorMin = dialRt.anchorMax = dialRt.pivot = Vector2.zero;
            dialRt.anchoredPosition = new Vector2(20, 24);
            dialRt.sizeDelta = new Vector2(108, 108);
            outputButton = dialRt.gameObject.AddComponent<CombatHudOutputButton>();
            outputButton.Owner = this;
            outputButton.raycastTarget = true;

            output = Label("100%", new Vector2(14, 72), new Vector2(120, 34), 27, TextAlignmentOptions.Center, FontStyles.Bold | FontStyles.Italic);
            outputCaption = Label("OUTPUT", new Vector2(30, 52), new Vector2(88, 16), 9.5f, TextAlignmentOptions.Center, FontStyles.Bold);
            outputCaption.characterSpacing = 3;
            outputCaption.color = new Color(.78f, .84f, .95f);

            header = Label("POWER LEVEL", new Vector2(150, 168), new Vector2(470, 17), 11.5f, TextAlignmentOptions.BottomLeft, FontStyles.Bold);
            header.characterSpacing = 3;
            header.color = new Color(.9f, .93f, 1f);
            header.richText = true;
            // Long form names shrink instead of running past the plate; it only re-fits on a form change.
            header.enableAutoSizing = true;
            header.fontSizeMin = 8;
            header.fontSizeMax = 11.5f;
            number = Label("0", new Vector2(146, 130), new Vector2(330, 40), 34, TextAlignmentOptions.BottomLeft, FontStyles.Bold | FontStyles.Italic);
            number.color = new Color(1f, .87f, .42f);
            numberRt = number.rectTransform;
            numberRt.pivot = new Vector2(0, 0);

            chipBack = DbUi.Img(panel, DbAtlas.Slant, Color.clear, "State");
            chipRt = chipBack.rectTransform;
            chipRt.anchorMin = chipRt.anchorMax = chipRt.pivot = Vector2.zero;
            chipRt.anchoredPosition = new Vector2(452, 132);
            chipRt.sizeDelta = new Vector2(172, 26);
            chipText = Label("", Vector2.zero, Vector2.zero, 12, TextAlignmentOptions.Center, FontStyles.Bold);
            chipText.characterSpacing = 2;
            // A child of the chip, so its pop-in scales the text with it.
            chipText.rectTransform.SetParent(chipRt, false);
            chipText.rectTransform.Stretch(12, 0, 12, 0);
            chipBack.gameObject.SetActive(false);
            chipText.gameObject.SetActive(false);

            // Thin gauges: name on the plate to the left, value to the right, never over a bright fill.
            Caption("STM", new Vector2(150, 103), new Vector2(48, 16));
            stmText = Label("100%", new Vector2(446, 103), new Vector2(64, 16), 11, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            Caption("KI", new Vector2(156, 69), new Vector2(40, 24)).fontSize = 14;
            kiText = Label("100%", new Vector2(470, 67), new Vector2(118, 28), 16, TextAlignmentOptions.MidlineRight, FontStyles.Bold | FontStyles.Italic);
            Caption("STR", new Vector2(150, 40), new Vector2(48, 16));
            strainText = Label("0%", new Vector2(426, 40), new Vector2(64, 16), 11, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            shieldLabel = Caption("SHIELD", new Vector2(150, 17), new Vector2(50, 15));
            shieldLabel.fontSize = 8.5f;
            shieldLabel.characterSpacing = 1;
            shieldText = Label("100%", new Vector2(426, 17), new Vector2(80, 15), 10.5f, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            shieldLabel.gameObject.SetActive(false);
            shieldText.gameObject.SetActive(false);

            head = transform.Find("Head");
            if (head)
            {
                headSprite = head.GetComponent<SpriteRenderer>();
                headLimb = head.GetComponent<LimbBehaviour>();
            }
        }

        TextMeshProUGUI Caption(string text, Vector2 at, Vector2 size)
        {
            var label = Label(text, at, size, 10, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            label.characterSpacing = 2.5f;
            label.color = new Color(.86f, .9f, 1f, .92f);
            return label;
        }

        TextMeshProUGUI Label(string text, Vector2 at, Vector2 size, float fontSize, TextAlignmentOptions alignment, FontStyles style)
        {
            var go = new GameObject(text.Length > 0 ? text : "Mode", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(panel, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
            rt.anchoredPosition = at;
            rt.sizeDelta = size;
            var label = go.AddComponent<TextMeshProUGUI>();
            var font = DbTheme.DisplayFont;
            label.font = font;
            label.fontSharedMaterial = TextMaterial(font);
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.alignment = alignment;
            label.color = TextColor;
            label.raycastTarget = false;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;
            label.extraPadding = true;
            return label;
        }

        // One shared material per font: a feathered outline and a soft drop shadow keep small text
        // readable over bright auras, and every label still batches together.
        static Material TextMaterial(TMP_FontAsset font)
        {
            if (textMaterials.TryGetValue(font, out var material) && material) return material;
            material = new Material(font.material) { name = "DBS HUD text", hideFlags = HideFlags.HideAndDontSave };
            material.SetColor("_OutlineColor", new Color(.01f, .015f, .03f, .85f));
            material.SetFloat("_OutlineWidth", .14f);
            material.SetFloat("_OutlineSoftness", .1f);
            material.EnableKeyword("UNDERLAY_ON");
            material.SetColor("_UnderlayColor", new Color(0, 0, 0, .55f));
            material.SetFloat("_UnderlayOffsetX", .35f);
            material.SetFloat("_UnderlayOffsetY", -.45f);
            material.SetFloat("_UnderlayDilate", .2f);
            material.SetFloat("_UnderlaySoftness", .45f);
            textMaterials[font] = material;
            return material;
        }

        bool WantsVisible() { return power && power.HudVisible && DetailViewActive; }

        bool CanInteract()
        {
            if (!enabled || !power || !power.Person || !power.Person.IsAlive() || !WantsVisible() || !panel || !group || group.alpha < .5f
                || !Global.main || Global.main.GetPausedMenu() || !KiCombatMath.HasFocus) return false;
            if ((Global.ActiveUiBlock && (pointerOwner != this || Global.main.ActiveUiBlockers > 1)) || CombatAbilityWheel.InputCaptured
                || CombatTransformationWheel.InputCaptured || SpiritBombTargeting.InputCaptured || DialogBox.IsAnyDialogboxOpen
                || LiveSpriteEditor.SpriteEditorRuntime.BlocksGameInput) return false;
            var selected = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            return !selected || (!selected.GetComponent<TMP_InputField>() && !selected.GetComponent<InputField>());
        }

        void Update()
        {
            if (pointerOwner == this)
            {
                if (!CanInteract() || pointerBlocker != Global.main || (releaseRequestedFrame >= 0 && Time.frameCount > releaseRequestedFrame)) ReleasePointer();
                else if (Time.frameCount > pressedFrame && !Input.GetMouseButton(pointerButton)) EndPointer();
                return;
            }
            if (!CanInteract() || (!Input.GetMouseButtonDown(0) && !Input.GetMouseButtonDown(1)) || !outputButton) return;
            if (!outputButton.Raycast(Input.mousePosition, null)) return;
            // Claim the gesture before the native world tool's Update can grab or activate a limb
            // underneath this small UI target.
            if (EventSystem.current)
            {
                var hits = new List<RaycastResult>();
                var data = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
                EventSystem.current.RaycastAll(data, hits);
                if (hits.Count == 0 || hits[0].gameObject != outputButton.gameObject) return;
            }
            BeginPointer(Input.GetMouseButtonDown(1) ? 1 : 0);
        }

        internal bool BeginPointer(int button)
        {
            if (button < 0 || button > 1 || !CanInteract() || (pointerOwner && pointerOwner != this)) return false;
            if (pointerOwner == this) return true;
            pointerOwner = this;
            pointerBlocker = Global.main;
            pointerBlocker.AddUiBlocker();
            pointerButton = button;
            pressedFrame = Time.frameCount;
            releaseRequestedFrame = -1;
            return true;
        }

        internal void EndPointer() { if (pointerOwner == this && releaseRequestedFrame < 0) releaseRequestedFrame = Time.frameCount; }

        public bool TryCycleOutput(int direction)
        {
            if (clickedFrame == Time.frameCount || !CanInteract()) return false;
            clickedFrame = Time.frameCount;
            power.Cycle(direction);
            return true;
        }

        internal bool PointerBelongsTo(int button) { return pointerOwner == this && pointerButton == button; }

        void ReleasePointer()
        {
            DbUi.Release(pointerBlocker);
            pointerBlocker = null;
            releaseRequestedFrame = -1;
            if (pointerOwner == this)
            {
                pointerOwner = null;
                releasedFrame = Time.frameCount;
            }
        }

        void ResetMotion(Vector3 position)
        {
            previousHead = position;
            motionOffset = motionVelocity = smoothedVelocity = shownOffset = Vector2.zero;
            motionAngle = angularVelocity = shownAngle = 0;
            motionReady = true;
            previousWidth = Screen.width;
            previousHeight = Screen.height;
        }

        static Vector2 SoftLimit(Vector2 value, float limit)
        {
            float length = value.magnitude;
            return length < .0001f ? Vector2.zero : value * (limit * Tanh(length / limit) / length);
        }

        static float SoftLimit(float value, float limit) { return limit * Tanh(value / limit); }
        static float Tanh(float x) { x = Mathf.Clamp(x, -9, 9); float e = Mathf.Exp(2 * x); return (e - 1) / (e + 1); }

        void StepMotion(Camera cam, Vector3 position, float scale)
        {
            float dt = Time.unscaledDeltaTime;
            if (!motionReady || (position - previousHead).sqrMagnitude > 64 || previousWidth != Screen.width || previousHeight != Screen.height || dt > .1f)
            {
                ResetMotion(position);
                return;
            }
            if (Global.main && Global.main.Paused) { previousHead = position; return; }
            if (dt <= .00001f) return;
            // Project both positions through the current camera so camera pans do not masquerade as
            // movement of the fighter.
            Vector2 velocity = (cam.WorldToScreenPoint(position) - cam.WorldToScreenPoint(previousHead)) / dt;
            previousHead = position;
            float limit = Mathf.Clamp(scale * 44, 8, 30);
            // Physics positions arrive in fixed steps; filter them before they drive the spring.
            Vector2 filtered = smoothedVelocity + (velocity - smoothedVelocity) * (1 - Mathf.Exp(-dt / VelocitySmoothing));
            // Inertia: a change of the fighter's velocity kicks the HUD the other way.
            motionVelocity -= (filtered - smoothedVelocity) * Carry;
            smoothedVelocity = filtered;
            motionVelocity = Vector2.ClampMagnitude(motionVelocity, limit * Mathf.Sqrt(Stiffness) * 1.1f);
            Vector2 target = Vector2.ClampMagnitude(-filtered * Drag, limit * .6f);
            for (float left = dt; left > .000001f;)
            {
                float step = Mathf.Min(left, 1 / 240f);
                left -= step;
                motionVelocity += ((target - motionOffset) * Stiffness - motionVelocity * Damping) * step;
                motionOffset += motionVelocity * step;
                // The tilt follows the sideways sway slightly later, like a panel hinged on the head.
                float targetAngle = Mathf.Clamp(-SoftLimit(motionOffset, limit).x * AnglePerPixel, -MaxAngle, MaxAngle);
                angularVelocity += ((targetAngle - motionAngle) * AngleStiffness - angularVelocity * AngleDamping) * step;
                motionAngle += angularVelocity * step;
            }
            motionOffset = Vector2.ClampMagnitude(motionOffset, limit * 3);
            motionAngle = Mathf.Clamp(motionAngle, -MaxAngle * 2, MaxAngle * 2);
            shownOffset = SoftLimit(motionOffset, limit);
            shownAngle = SoftLimit(motionAngle, MaxAngle);
        }

        // Height above the head centre that keeps the HUD clear of the head sprite and of the native
        // Detail-view health bar above it.
        float Clearance(Camera cam, Vector3 anchor, Vector3 at, float ppu)
        {
            float clearance = 22;
            if (headSprite && headSprite.sprite)
            {
                var extents = headSprite.sprite.bounds.extents;
                var size = headSprite.transform.lossyScale;
                clearance = Mathf.Max(clearance, Mathf.Max(Mathf.Abs(extents.x * size.x), Mathf.Abs(extents.y * size.y)) * ppu + 6);
            }
            var status = headLimb ? headLimb.LimbStatus : null;
            if (status != statusObject)
            {
                statusObject = status;
                statusRenderers = status ? status.GetComponentsInChildren<Renderer>(true) : null;
            }
            if (statusObject && statusObject.activeInHierarchy && statusRenderers != null)
            {
                float top = float.NegativeInfinity;
                foreach (var r in statusRenderers)
                    if (r && r.enabled && r.gameObject.activeInHierarchy) top = Mathf.Max(top, r.bounds.max.y);
                if (!float.IsInfinity(top) && top > anchor.y)
                {
                    float above = cam.WorldToScreenPoint(new Vector3(anchor.x, top, anchor.z)).y - at.y;
                    if (above > 0 && above < Screen.height * .5f) clearance = Mathf.Max(clearance, above + 5);
                }
            }
            return Mathf.Min(clearance, Screen.height * .4f);
        }

        void LateUpdate()
        {
            if (!power || !power.Ki || !panel || !Camera.main) return;
            power.Ki.SuppressLegacyHud(true);
            var cam = Camera.main;
            var anchor = head ? head.position : transform.position;
            var at = cam.WorldToScreenPoint(anchor);
            bool onScreen = at.z > 0 && at.x > -150 && at.x < Screen.width + 150 && at.y > -100 && at.y < Screen.height + 100;
            bool wanted = WantsVisible() && onScreen;
            // Fade in/out with Detail view instead of popping.
            float fadeDt = Mathf.Min(Time.unscaledDeltaTime, .1f);
            visibility = Mathf.MoveTowards(visibility, wanted ? 1 : 0, fadeDt / (wanted ? FadeInTime : FadeOutTime));
            if (visibility <= 0)
            {
                group.alpha = 0;
                group.blocksRaycasts = group.interactable = false;
                ResetMotion(anchor);
                ReleasePointer();
                return;
            }
            float ease = visibility * visibility * (3 - 2 * visibility);
            group.alpha = ease;
            group.blocksRaycasts = wanted && CanInteract();
            group.interactable = group.blocksRaycasts;
            float ppu = cam.orthographic ? Screen.height / (2 * cam.orthographicSize) : 100;
            float scale = Mathf.Clamp(ppu * Mathf.Abs(transform.lossyScale.y) * .005f, .20f, .85f);
            StepMotion(cam, anchor, scale);
            Place(cam, anchor, at, ppu, scale, ease);
            UpdateGauges(scale, fadeDt);
        }

        void Place(Camera cam, Vector3 anchor, Vector3 at, float ppu, float scale, float ease)
        {
            // Appearing: a slight grow and rise into place.
            float shown = scale * (.94f + .06f * ease), width = Width * shown, height = Height * shown;
            panel.localScale = Vector3.one * shown;
            Vector2 hinge = new Vector2(at.x + shownOffset.x, at.y + Clearance(cam, anchor, at, ppu) - (1 - ease) * 12 * scale + shownOffset.y);
            hinge.x = Mathf.Clamp(hinge.x, width * .5f + 4, Mathf.Max(width * .5f + 4, Screen.width - width * .5f - 4));
            hinge.y = Mathf.Clamp(hinge.y, 4, Mathf.Max(4, Screen.height - height - 4));
            // Rotate around the bottom centre above the head, so both ends swing alike.
            var rotation = Quaternion.Euler(0, 0, shownAngle);
            panel.localRotation = rotation;
            panel.anchoredPosition = hinge - (Vector2)(rotation * new Vector3(Width * .5f * shown, 0, 0));
            panel.GetWorldCorners(corners);
            Vector2 correction = Vector2.zero;
            float minX = corners[0].x, maxX = corners[0].x, minY = corners[0].y, maxY = corners[0].y;
            foreach (var corner in corners)
            {
                minX = Mathf.Min(minX, corner.x); maxX = Mathf.Max(maxX, corner.x);
                minY = Mathf.Min(minY, corner.y); maxY = Mathf.Max(maxY, corner.y);
            }
            if (minX < 3) correction.x = 3 - minX; else if (maxX > Screen.width - 3) correction.x = Screen.width - 3 - maxX;
            if (minY < 3) correction.y = 3 - minY; else if (maxY > Screen.height - 3) correction.y = Screen.height - 3 - maxY;
            panel.anchoredPosition += correction;

            // About 1.3-2 screen pixels of anti-aliasing at any zoom.
            float edge = Mathf.Lerp(1.3f, 2f, Mathf.InverseLerp(.2f, .85f, scale));
            art.Feather = Mathf.Clamp(edge / shown, 1.2f, 9);
            if (outputButton) outputButton.Feather = art.Feather;
        }

        void UpdateGauges(float scale, float dt)
        {
            var ki = power.Ki;
            float blend = 1 - Mathf.Exp(-14 * Time.unscaledDeltaTime);
            float reserve = Mathf.Clamp01(ki.currentKi / Mathf.Max(1, ki.maxKi));
            // DBPU has no independent stamina resource: STM displays remaining vitality.
            float stamina = power.Health01;
            float strain = Mathf.Clamp01(ki.Strain);
            float outputValue = power.Output;

            art.Ki = Mathf.Lerp(art.Ki, reserve, blend);
            art.Stamina = Mathf.Lerp(art.Stamina, stamina, blend);
            art.Strain = Mathf.Lerp(art.Strain, strain, blend);
            // Damage ghosts: a drop leaves a pale trail that holds briefly, then catches up.
            art.KiGhost = Ghost(art.KiGhost, art.Ki, ref kiGhostHold, dt);
            art.StaminaGhost = Ghost(art.StaminaGhost, art.Stamina, ref staminaGhostHold, dt);
            art.Prep = ki.SparkingOvercharge01;
            art.Sparking = ki.SparkingOverloadActive;
            art.Exhausted = ki.SparkingExhaustActive;
            art.LowKi = reserve < .2f;

            if (lastOutput >= 0 && Mathf.Abs(outputValue - lastOutput) > .0001f) outputPulse = 1;
            lastOutput = outputValue;
            art.Output = Mathf.Lerp(art.Output, outputValue, blend);
            outputPulse = Mathf.Max(0, outputPulse - dt * 2.6f);
            art.OutputPulse = outputPulse;
            dialRt.localScale = Vector3.one * (1 + .07f * Mathf.Sin(outputPulse * Mathf.PI));

            var barrier = power.GetComponent<KiShield>();
            bool shielding = barrier && (barrier.Using || barrier.Cooldown > 0);
            bool broken = shielding && !barrier.Using;
            art.Shielded = shielding;
            art.ShieldBroken = broken;
            art.ShieldIntegrity = barrier ? Mathf.Lerp(art.ShieldIntegrity, barrier.Integrity01, blend) : 0;
            art.ShieldTint = barrier && barrier.Using ? barrier.Tint : ExhaustAccent;
            if (shielding != shownShielding)
            {
                shownShielding = shielding;
                shieldLabel.gameObject.SetActive(shielding);
                shieldText.gameObject.SetActive(shielding);
                shownShield = -1;
            }
            if (shielding)
            {
                int percent = Mathf.RoundToInt(barrier.Integrity01 * 100);
                if (broken != shownBroken || (!broken && percent != shownShield))
                {
                    shownBroken = broken;
                    shownShield = percent;
                    shieldText.text = broken ? "BROKEN" : percent + "%";
                    shieldText.color = broken ? ExhaustAccent : TextColor;
                }
            }

            UpdateForm(ki);
            Color accent = ki.SparkingOverloadActive ? SparkAccent : ki.SparkingExhaustActive ? ExhaustAccent : formAccent;
            art.Badge = Color.Lerp(art.Badge, accent, blend);

            UpdatePower(dt);
            UpdateChip(ki);
            SetPercent(kiText, reserve, ref shownKi);
            SetPercent(stmText, stamina, ref shownStamina);
            SetPercent(strainText, strain, ref shownStrain);
            int outputPercent = Mathf.RoundToInt(Mathf.Min(100000, outputValue * 100));
            if (outputPercent != shownOutput)
            {
                shownOutput = outputPercent;
                output.text = outputPercent + "%";
                output.color = outputValue > 1.001f ? new Color(.88f, .7f, 1f) : TextColor;
            }

            art.Clock = Time.unscaledTime;
            art.Commit();
            if (outputButton) outputButton.SetPressed(pointerOwner == this);
        }

        static float Ghost(float ghost, float value, ref float hold, float dt)
        {
            if (value >= ghost - .0005f) { hold = .45f; return value; }
            hold -= dt;
            return hold > 0 ? ghost : Mathf.MoveTowards(ghost, value, dt * .55f);
        }

        static void SetPercent(TextMeshProUGUI label, float value, ref int shown)
        {
            int percent = Mathf.RoundToInt(value * 100);
            if (percent == shown) return;
            shown = percent;
            label.text = percent + "%";
        }

        void UpdatePower(float dt)
        {
            double target = power.PowerLevel;
            float blend = 1 - Mathf.Exp(-14 * Time.unscaledDeltaTime);
            // A sudden jump (a transformation, Sparking, Kaioken) punches the number.
            if (shownPower > 0 && target > punchFrom * 1.08 && target > shownPower * 1.08) punch = 1;
            shownPower = shownPower == 0 ? target : shownPower + (target - shownPower) * blend;
            punchFrom = target;
            punch = Mathf.Max(0, punch - dt * 2.2f);
            float pop = Mathf.Sin(Mathf.Min(1, punch) * Mathf.PI * .5f);
            numberRt.localScale = Vector3.one * (1 + .16f * pop);
            number.color = Color.Lerp(new Color(1f, .87f, .42f), Color.white, pop * .8f);
            if (Time.unscaledTime < numberRefreshAt && punch <= 0) return;
            numberRefreshAt = Time.unscaledTime + .05f;
            string text = CombatPowerMath.Format(shownPower);
            if (number.text != text) number.text = text;
        }

        void UpdateForm(KiSystem ki)
        {
            string form = ki.CurrentForm ?? "";
            if (form == shownForm) return;
            shownForm = form;
            formAccent = BaseAccent;
            formName = "";
            bool transformed = DBPU.BaseConsideredForms == null || !DBPU.BaseConsideredForms.Contains(form);
            var data = power.Person ? power.Person.GetComponent<CharacterTransformations>() : null;
            if (data != null && data.transformations != null && data.transformations.TransformationData != null)
            {
                foreach (var pair in data.transformations.TransformationData)
                {
                    if (pair.Value == null || (pair.Key != form && pair.Value.ID != form)) continue;
                    if (transformed) formAccent = DbTheme.FormAccent(pair.Value);
                    formName = DbTheme.StripTags(pair.Value.Name);
                    break;
                }
            }
            if (string.IsNullOrEmpty(formName) && transformed) formName = form;
            header.text = string.IsNullOrEmpty(formName) || !transformed
                ? "POWER LEVEL"
                : "POWER LEVEL   <color=#" + ColorUtility.ToHtmlStringRGB(formAccent) + ">" + formName.ToUpperInvariant() + "</color>";
        }

        void UpdateChip(KiSystem ki)
        {
            string chip = "";
            Color color = SparkAccent;
            if (ki.SparkingOverloadActive) chip = "SPARKING";
            else if (ki.SparkingExhaustActive) { chip = "EXHAUSTED"; color = ExhaustAccent; }
            else if (ki.SparkingOvercharge01 > .01f) chip = "OVERCHARGE";
            else
            {
                var kaioken = power.GetComponent<Kaioken>();
                if (kaioken && kaioken.Active)
                {
                    chip = "KAIOKEN  x" + Mathf.RoundToInt(kaioken.Multiplier);
                    color = new Color(1f, .3f, .26f);
                }
            }
            if (chip != shownChip)
            {
                bool appear = string.IsNullOrEmpty(shownChip) && chip.Length > 0;
                shownChip = chip;
                bool visible = chip.Length > 0;
                chipBack.gameObject.SetActive(visible);
                chipText.gameObject.SetActive(visible);
                if (visible)
                {
                    chipText.text = chip;
                    chipBack.color = color;
                    chipText.color = DbTheme.TextOnAccent;
                    if (appear)
                    {
                        chipRt.localScale = new Vector3(.6f, .6f, 1);
                        DbMotion.Scale(chipRt, 1f, .22f, DbEase.OutBack);
                    }
                }
            }
            if (chip.Length > 0)
            {
                // Breathes so a state change is noticed in the middle of a fight.
                float breath = .82f + .18f * Mathf.Sin(Time.unscaledTime * 6f);
                var c = chipBack.color;
                c.a = breath;
                chipBack.color = c;
            }
        }

        void OnDisable()
        {
            ReleasePointer();
            motionReady = false;
            visibility = 0;
            if (group) { group.alpha = 0; group.blocksRaycasts = false; group.interactable = false; }
        }

        void OnDestroy()
        {
            ReleasePointer();
            if (panel) Destroy(panel.gameObject);
            if (power && power.Ki) power.Ki.SuppressLegacyHud(false);
        }
    }

    // The round Output face. Its circular hit test keeps the corners of its square clickable world.
    public sealed class CombatHudOutputButton : MaskableGraphic, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public CombatHudView Owner;
        float feather = 1;
        bool hovered, pressed;

        // Rebuild only when the edge width changes noticeably (zoom).
        public float Feather
        {
            get { return feather; }
            set { if (Mathf.Abs(feather - value) < .05f) return; feather = value; SetVerticesDirty(); }
        }

        public override bool Raycast(Vector2 point, Camera eventCamera)
        {
            Vector2 local;
            if (!isActiveAndEnabled || !RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, point, eventCamera, out local)) return false;
            return (local - new Vector2(54, 54)).sqrMagnitude <= 54 * 54 && base.Raycast(point, eventCamera);
        }

        public void OnPointerDown(PointerEventData data)
        {
            if (Owner && (data.button == PointerEventData.InputButton.Left || data.button == PointerEventData.InputButton.Right))
                Owner.BeginPointer(data.button == PointerEventData.InputButton.Right ? 1 : 0);
        }

        public void OnPointerUp(PointerEventData data) { if (Owner) Owner.EndPointer(); }

        public void OnPointerClick(PointerEventData data)
        {
            int button = data.button == PointerEventData.InputButton.Right ? 1 : 0;
            if (Owner && (data.button == PointerEventData.InputButton.Left || data.button == PointerEventData.InputButton.Right) && Owner.PointerBelongsTo(button))
                Owner.TryCycleOutput(button == 1 || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? -1 : 1);
        }

        public void OnPointerEnter(PointerEventData data) { hovered = true; SetVerticesDirty(); }
        public void OnPointerExit(PointerEventData data) { hovered = false; SetVerticesDirty(); }
        public void SetPressed(bool value) { if (pressed == value) return; pressed = value; SetVerticesDirty(); }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            // Hover lights the face and its rim; a press darkens it. Idle draws an invisible disc so the
            // face stays a raycast target.
            var face = pressed ? new Color(0, 0, 0, .28f) : new Color(.85f, .95f, 1, hovered ? .08f : .001f);
            Disc(vh, 54, 54, 53, face);
            if (hovered && !pressed) Ring(vh, 54, 54, 50, 53.5f, new Color(.85f, .95f, 1, .55f));
        }

        void Disc(VertexHelper vh, float x, float y, float r, Color c)
        {
            float edge = Mathf.Clamp(feather, .5f, 9);
            var clear = c; clear.a = 0;
            int start = vh.currentVertCount;
            vh.AddVert(new Vector3(x, y), c, Vector2.zero);
            for (int n = 0; n <= 96; n++)
            {
                float a = n * Mathf.PI / 48;
                var direction = new Vector3(Mathf.Cos(a), Mathf.Sin(a));
                vh.AddVert(new Vector3(x, y) + direction * (r - edge * .5f), c, Vector2.zero);
                vh.AddVert(new Vector3(x, y) + direction * (r + edge * .5f), clear, Vector2.zero);
                if (n > 0)
                {
                    int p = start + 1 + (n - 1) * 2, q = start + 1 + n * 2;
                    vh.AddTriangle(start, p, q); vh.AddTriangle(p, p + 1, q + 1); vh.AddTriangle(p, q + 1, q);
                }
            }
        }

        void Ring(VertexHelper vh, float x, float y, float inner, float outer, Color c)
        {
            float edge = Mathf.Min(Mathf.Clamp(feather, .5f, 9), (outer - inner) * .7f);
            var clear = c; clear.a = 0;
            int start = vh.currentVertCount;
            for (int n = 0; n <= 96; n++)
            {
                float a = n * Mathf.PI / 48;
                var direction = new Vector3(Mathf.Cos(a), Mathf.Sin(a));
                var centre = new Vector3(x, y);
                vh.AddVert(centre + direction * (inner - edge * .5f), clear, Vector2.zero);
                vh.AddVert(centre + direction * (inner + edge * .5f), c, Vector2.zero);
                vh.AddVert(centre + direction * (outer - edge * .5f), c, Vector2.zero);
                vh.AddVert(centre + direction * (outer + edge * .5f), clear, Vector2.zero);
                if (n > 0)
                    for (int strip = 0; strip < 3; strip++)
                    {
                        int p = start + (n - 1) * 4 + strip, q = start + n * 4 + strip;
                        vh.AddTriangle(p, p + 1, q + 1); vh.AddTriangle(p, q + 1, q);
                    }
            }
        }

        protected override void OnDisable()
        {
            if (Owner) Owner.EndPointer();
            base.OnDisable();
        }
    }

    // All gauge art in one anti-aliased mesh. It is rebuilt only when a value it shows moves or an
    // effect is animating, so an idle HUD costs no mesh work.
    public sealed class CombatHudGraphic : MaskableGraphic
    {
        public float Ki = 1, KiGhost = 1, Stamina = 1, StaminaGhost = 1, Strain, Prep, Clock;
        public float Output = 1, OutputPulse;
        public float Feather = 1;
        public bool Shielded, ShieldBroken; public float ShieldIntegrity; public Color ShieldTint = Color.cyan;
        public bool Sparking, Exhausted, LowKi;
        public Color Badge = new Color(.55f, .78f, 1f);

        readonly float[] built = new float[16];
        static readonly float[] now = new float[16];
        bool everBuilt;

        // Bar geometry, shared by the art and the labels laid over it.
        const float BarX = 146, ThinX = 202;
        const float KiY = 64, KiTop = 96, KiEnd = 622, KiSlant = 30;
        const float StmY = 103, StmTop = 119, StmEnd = 440, StmSlant = 14;
        const float StrY = 40, StrTop = 56, StrEnd = 420, StrSlant = 14;
        const float ShieldY = 17, ShieldTop = 32, ShieldEnd = 420, ShieldSlant = 12;
        static readonly Vector2 DialCentre = new Vector2(74, 78);

        static readonly Color Plate = new Color(.012f, .016f, .028f, .92f);
        static readonly Color Track = new Color(1, 1, 1, .055f);

        public void Commit()
        {
            now[0] = Ki; now[1] = KiGhost; now[2] = Stamina; now[3] = StaminaGhost; now[4] = Strain; now[5] = Prep;
            now[6] = Output; now[7] = OutputPulse; now[8] = Feather; now[9] = ShieldIntegrity;
            now[10] = (Shielded ? 1 : 0) + (ShieldBroken ? 2 : 0) + (Sparking ? 4 : 0) + (Exhausted ? 8 : 0) + (LowKi ? 16 : 0);
            now[11] = Badge.r; now[12] = Badge.g; now[13] = Badge.b; now[14] = ShieldTint.r + ShieldTint.g * 2 + ShieldTint.b * 4;
            bool animating = Sparking || LowKi || ShieldBroken || Prep > .001f || OutputPulse > 0;
            bool changed = !everBuilt;
            for (int i = 0; i < now.Length && !changed; i++)
                if (Mathf.Abs(now[i] - built[i]) > .0008f) changed = true;
            if (!changed && !animating) return;
            Array.Copy(now, built, now.Length);
            everBuilt = true;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            DrawPlate(vh);
            DrawBars(vh);
            DrawDial(vh);
            if (Sparking) DrawLightning(vh);
        }

        void DrawPlate(VertexHelper vh)
        {
            // Slanted dark plate that fades to the right, edged by the form colour.
            var left = Plate;
            // Linear blending lets a bright aura show through a dark layer far more than its alpha
            // suggests, so the plate stays nearly opaque where text sits.
            var right = Plate; right.a = .7f;
            SoftPolygon(vh, new[] { new Vector2(130, 12), new Vector2(616, 12), new Vector2(640, 128), new Vector2(130, 128) },
                new[] { left, right, right, left }, Feather);
            // A softer shade behind the power number, which sits over the brightest part of an aura.
            var shade = Plate; shade.a = .86f;
            var gone = Plate; gone.a = 0;
            SoftPolygon(vh, new[] { new Vector2(134, 128), new Vector2(500, 128), new Vector2(530, 188), new Vector2(134, 188) },
                new[] { shade, gone, gone, shade }, 12);
            var edge = Badge; edge.a = .95f;
            var fade = Badge; fade.a = 0;
            Band(vh, new Vector2(132, 127), new Vector2(638, 127), 2.2f, edge, fade);
            Band(vh, new Vector2(132, 13), new Vector2(560, 13), 1.2f, new Color(edge.r, edge.g, edge.b, .35f), fade);
            // Soft glow behind the dial so it lifts off busy backgrounds.
            Disc(vh, DialCentre.x, DialCentre.y, 82, new Color(0, 0, 0, .55f), new Color(0, 0, 0, 0), 22);
        }

        void DrawBars(VertexHelper vh)
        {
            // Stamina.
            Quad(vh, ThinX, StmY, StmEnd, StmTop, Track, Track, StmSlant);
            Gauge(vh, ThinX, StmY, StmEnd, StmTop, StaminaGhost, new Color(1, .95f, .8f, .45f), new Color(1, 1, .9f, .55f), StmSlant);
            float stm = Gauge(vh, ThinX, StmY, StmEnd, StmTop, Stamina, new Color(.78f, .45f, .03f), new Color(1, .9f, .28f), StmSlant);
            Shine(vh, ThinX, StmY, StmEnd, StmTop, Stamina, StmSlant, stm, new Color(1, 1, .85f));

            // Ki: the main gauge, with tick marks every tenth.
            Color low = new Color(0, .3f, .52f), high = new Color(.22f, .9f, 1);
            if (Sparking) { low = new Color(.45f, .1f, .8f); high = new Color(.95f, .66f, 1); }
            if (LowKi && !Sparking)
            {
                float pulse = .5f + .5f * Mathf.Sin(Clock * 9f);
                low = Color.Lerp(low, new Color(.6f, .05f, .08f), pulse * .6f);
                high = Color.Lerp(high, new Color(1, .4f, .4f), pulse * .5f);
            }
            Quad(vh, BarX, KiY, KiEnd, KiTop, new Color(.02f, .07f, .1f, .9f), new Color(.04f, .12f, .16f, .9f), KiSlant);
            Gauge(vh, BarX, KiY, KiEnd, KiTop, KiGhost, new Color(.85f, .95f, 1, .38f), new Color(1, 1, 1, .5f), KiSlant);
            float kiRight = Gauge(vh, BarX, KiY, KiEnd, KiTop, Ki, low, high, KiSlant);
            if (Prep > 0 && !Sparking)
                Gauge(vh, BarX, KiY, KiEnd, KiTop, Mathf.Clamp01(Prep), new Color(.48f, .04f, .67f, .82f), new Color(.89f, .41f, 1, .9f), KiSlant);
            for (int n = 1; n < 10; n++)
            {
                float x = BarX + (KiEnd - KiSlant - BarX) * n / 10f;
                Line(vh, new Vector2(x, KiY + 2), new Vector2(x + KiSlant * .94f, KiTop - 2), 1.6f, new Color(0, 0, 0, .38f));
            }
            Shine(vh, BarX, KiY, KiEnd, KiTop, Ki, KiSlant, kiRight, Sparking ? new Color(1, .85f, 1) : new Color(.85f, 1, 1));
            // Top highlight that makes the gauge read as glass.
            Band(vh, new Vector2(BarX + 2, KiTop - 3), new Vector2(KiEnd - 4, KiTop - 3), 1.6f, new Color(1, 1, 1, .35f), new Color(1, 1, 1, .05f));

            // Strain.
            Quad(vh, ThinX, StrY, StrEnd, StrTop, Track, Track, StrSlant);
            float strRight = Gauge(vh, ThinX, StrY, StrEnd, StrTop, Strain, new Color(.42f, .02f, .05f), new Color(1, .28f, .24f), StrSlant);
            Shine(vh, ThinX, StrY, StrEnd, StrTop, Strain, StrSlant, strRight, new Color(1, .8f, .75f));

            // Ki shield.
            if (!Shielded) return;
            Quad(vh, ThinX, ShieldY, ShieldEnd, ShieldTop, Track, Track, ShieldSlant);
            if (ShieldBroken)
            {
                // Hazard stripes that flash while the barrier recovers.
                float flash = .45f + .55f * Mathf.Abs(Mathf.Sin(Clock * 6f));
                for (float x = ThinX + 4; x < ShieldEnd - ShieldSlant - 6; x += 16)
                    Line(vh, new Vector2(x, ShieldY + 2), new Vector2(x + 9, ShieldTop - 2), 5, new Color(1, .25f, .2f, .55f * flash));
                return;
            }
            float shieldRight = Gauge(vh, ThinX, ShieldY, ShieldEnd, ShieldTop, ShieldIntegrity,
                Color.Lerp(ShieldTint, Color.black, .5f), Color.Lerp(ShieldTint, Color.white, .35f), ShieldSlant);
            Shine(vh, ThinX, ShieldY, ShieldEnd, ShieldTop, ShieldIntegrity, ShieldSlant, shieldRight, Color.Lerp(ShieldTint, Color.white, .7f));
        }

        void DrawDial(VertexHelper vh)
        {
            float x = DialCentre.x, y = DialCentre.y;
            var accent = Badge;
            Disc(vh, x, y, 70, new Color(.01f, .014f, .024f, .95f), new Color(.03f, .04f, .06f, .95f));
            // Output arc: the share of full power released, clockwise from the top.
            Arc(vh, x, y, 59, 66.5f, 0, 1, Track);
            float shown = Mathf.Clamp01(Output);
            if (shown > .001f) Arc(vh, x, y, 59, 66.5f, 0, shown, Color.Lerp(accent, Color.white, .25f));
            if (Output > 1.001f)
            {
                // Overdrive above 100% wraps a second, violet arc around the first.
                Arc(vh, x, y, 67.5f, 70.5f, 0, Mathf.Clamp01((Output - 1) / 1.5f), new Color(.85f, .5f, 1));
            }
            // A notch for each Output step.
            foreach (float step in CombatPowerMath.Steps)
            {
                if (step >= 1) continue;
                float a = step * Mathf.PI * 2;
                var dir = new Vector2(Mathf.Sin(a), Mathf.Cos(a));
                bool reached = Output >= step - .001f;
                Line(vh, new Vector2(x, y) + dir * 57.5f, new Vector2(x, y) + dir * 68, 2.4f,
                    reached ? new Color(1, 1, 1, .9f) : new Color(1, 1, 1, .28f));
            }
            Disc(vh, x, y, 55, Color.Lerp(new Color(.02f, .03f, .05f), accent, .12f), Color.Lerp(new Color(.05f, .07f, .1f), accent, .3f));
            Ring(vh, x, y, 53, 55.5f, Color.Lerp(accent, Color.black, .2f), Color.Lerp(accent, Color.white, .4f));
            if (OutputPulse > 0)
            {
                float r = 70 + (1 - OutputPulse) * 18;
                var c = accent; c.a = OutputPulse * .8f;
                Ring(vh, x, y, r - 2, r, c, c);
            }
        }

        void DrawLightning(VertexHelper vh)
        {
            float frame = Mathf.Floor(Clock * 18) * 1.7f;
            Bolt(vh, KiTop + 3, frame, 0);
            Bolt(vh, KiY - 4, frame, 1.3f);
            // Crackle around the dial as well.
            for (int n = 0; n < 3; n++)
            {
                float a = frame * .9f + n * 2.1f;
                var c = DialCentre;
                Vector2 before = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 64;
                for (int k = 1; k <= 4; k++)
                {
                    float b = a + k * .22f;
                    var next = c + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * (64 + ((k + n) % 2 == 0 ? 7 : -3));
                    Line(vh, before, next, 4, new Color(.6f, .2f, 1, .3f));
                    Line(vh, before, next, 1.2f, new Color(.95f, .82f, 1));
                    before = next;
                }
            }
        }

        void Bolt(VertexHelper vh, float y, float frame, float phase)
        {
            Vector2 before = new Vector2(BarX, y);
            for (int n = 1; n <= 28; n++)
            {
                var next = new Vector2(BarX + n * (KiEnd - BarX) / 28f, y + 6 * Mathf.Sin(n * 2.3f + frame + phase));
                Line(vh, before, next, 5, new Color(.6f, .2f, 1, .28f));
                Line(vh, before, next, 1.2f, new Color(.94f, .8f, 1));
                before = next;
            }
        }

        // A bright edge on the leading end of a fill, so the level reads at a glance.
        void Shine(VertexHelper vh, float x, float y, float end, float top, float fill, float slant, float right, Color c)
        {
            if (fill <= .01f || right <= x + 2) return;
            float bottomX = Mathf.Max(x, right - slant);
            Line(vh, new Vector2(bottomX, y + 1), new Vector2(right, top - 1), 6, new Color(c.r, c.g, c.b, .22f));
            Line(vh, new Vector2(bottomX, y + 1), new Vector2(right, top - 1), 1.8f, new Color(c.r, c.g, c.b, .95f));
        }

        // Area-true fill: a gauge at 1% shows a sliver, never a block. Returns the right edge.
        float Gauge(VertexHelper v, float x, float y, float end, float top, float fill, Color low, Color high, float slant)
        {
            fill = Mathf.Clamp01(fill);
            float height = top - y, span = end - x;
            if (fill <= .0001f || height <= 0 || span <= 0) return x;
            slant = Mathf.Clamp(slant, 0, span);
            float area = fill * (span - slant * .5f) * height, threshold = slant * height * .5f;
            float right = area >= threshold ? area / height + slant * .5f : Mathf.Sqrt(2 * slant * area / height);
            right = x + Mathf.Min(right, span);
            if (right - slant >= x - .001f) { Quad(v, x, y, right, top, low, high, slant); return right; }
            // Nearly empty: clip the slanted leading edge to a small triangle.
            float t = Mathf.Clamp01(1 - (right - x) / Mathf.Max(.0001f, slant)), cut = Mathf.Lerp(y, top, t);
            SoftPolygon(v, new[] { new Vector2(x, cut), new Vector2(right, top), new Vector2(x, top) },
                new[] { Color.Lerp(low, high, t), high, high }, Mathf.Min(Feather, (right - x) * .8f, (top - cut) * .8f));
            return right;
        }

        void Quad(VertexHelper v, float x, float y, float end, float top, Color low, Color high, float slant)
        {
            SoftPolygon(v, new[] { new Vector2(x, y), new Vector2(end - slant, y), new Vector2(end, top), new Vector2(x, top) },
                new[] { low, low, high, high }, Mathf.Min(Feather, Mathf.Max(.0001f, end - slant - x) * .8f + slant, (top - y) * .8f));
        }

        // Feathered convex polygon (counter-clockwise). Half of the edge fades inside and half outside
        // so shapes keep their size when softened.
        void SoftPolygon(VertexHelper v, Vector2[] points, Color[] colors, float edge)
        {
            int count = points.Length, start = v.currentVertCount;
            edge = Mathf.Max(.0001f, edge);
            for (int n = 0; n < count; n++)
            {
                Vector2 before = (points[n] - points[(n + count - 1) % count]).normalized, after = (points[(n + 1) % count] - points[n]).normalized;
                Vector2 a = new Vector2(before.y, -before.x), b = new Vector2(after.y, -after.x);
                if (a.sqrMagnitude < .5f) a = b;
                if (b.sqrMagnitude < .5f) b = a;
                Vector2 normal = (a + b).normalized;
                if (normal.sqrMagnitude < .5f) normal = b;
                Vector2 outset = normal * (edge / Mathf.Max(.45f, Vector2.Dot(normal, b)));
                v.AddVert(points[n] - outset * .5f, colors[n], Vector2.zero);
                var transparent = colors[n]; transparent.a = 0;
                v.AddVert(points[n] + outset * .5f, transparent, Vector2.zero);
            }
            for (int n = 1; n < count - 1; n++) v.AddTriangle(start, start + n * 2, start + (n + 1) * 2);
            for (int n = 0; n < count; n++)
            {
                int a = start + n * 2, b = start + ((n + 1) % count) * 2;
                v.AddTriangle(a, a + 1, b + 1);
                v.AddTriangle(a, b + 1, b);
            }
        }

        void Disc(VertexHelper v, float x, float y, float r, Color low, Color high, float soften = 0)
        {
            int start = v.currentVertCount;
            float edge = soften > 0 ? soften : Mathf.Min(Feather, r * .5f);
            v.AddVert(new Vector3(x, y), Color.Lerp(low, high, .5f), Vector2.zero);
            for (int n = 0; n <= 96; n++)
            {
                float a = n * Mathf.PI / 48;
                Vector2 direction = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Color c = soften > 0 ? low : Color.Lerp(low, high, (Mathf.Sin(a) + 1) * .5f);
                v.AddVert(new Vector2(x, y) + direction * (r - edge * .5f), c, Vector2.zero);
                c.a = 0;
                v.AddVert(new Vector2(x, y) + direction * (r + edge * .5f), c, Vector2.zero);
                if (n > 0)
                {
                    int p = start + 1 + (n - 1) * 2, q = start + 1 + n * 2;
                    v.AddTriangle(start, p, q); v.AddTriangle(p, p + 1, q + 1); v.AddTriangle(p, q + 1, q);
                }
            }
        }

        void Ring(VertexHelper v, float x, float y, float inner, float outer, Color low, Color high)
        {
            int start = v.currentVertCount;
            float feather = Mathf.Min(Feather, (outer - inner) * .7f);
            for (int n = 0; n <= 96; n++)
            {
                float a = n * Mathf.PI / 48;
                Vector2 direction = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Color c = Color.Lerp(low, high, (Mathf.Sin(a) + 1) * .5f), clear = c; clear.a = 0;
                v.AddVert(new Vector2(x, y) + direction * (inner - feather * .5f), clear, Vector2.zero);
                v.AddVert(new Vector2(x, y) + direction * (inner + feather * .5f), c, Vector2.zero);
                v.AddVert(new Vector2(x, y) + direction * (outer - feather * .5f), c, Vector2.zero);
                v.AddVert(new Vector2(x, y) + direction * (outer + feather * .5f), clear, Vector2.zero);
                if (n > 0)
                    for (int strip = 0; strip < 3; strip++)
                    {
                        int p = start + (n - 1) * 4 + strip, q = start + n * 4 + strip;
                        v.AddTriangle(p, p + 1, q + 1); v.AddTriangle(p, q + 1, q);
                    }
            }
        }

        // Part of a ring from one fraction of a turn to another, clockwise from twelve o'clock.
        void Arc(VertexHelper v, float x, float y, float inner, float outer, float from, float to, Color c)
        {
            int steps = Mathf.Max(2, Mathf.CeilToInt((to - from) * 96));
            int start = v.currentVertCount;
            float feather = Mathf.Min(Feather, (outer - inner) * .7f);
            Color clear = c; clear.a = 0;
            for (int n = 0; n <= steps; n++)
            {
                float a = Mathf.Lerp(from, to, n / (float)steps) * Mathf.PI * 2;
                Vector2 direction = new Vector2(Mathf.Sin(a), Mathf.Cos(a));
                v.AddVert(new Vector2(x, y) + direction * (inner - feather * .5f), clear, Vector2.zero);
                v.AddVert(new Vector2(x, y) + direction * (inner + feather * .5f), c, Vector2.zero);
                v.AddVert(new Vector2(x, y) + direction * (outer - feather * .5f), c, Vector2.zero);
                v.AddVert(new Vector2(x, y) + direction * (outer + feather * .5f), clear, Vector2.zero);
                if (n > 0)
                    for (int strip = 0; strip < 3; strip++)
                    {
                        int p = start + (n - 1) * 4 + strip, q = start + n * 4 + strip;
                        v.AddTriangle(p, p + 1, q + 1); v.AddTriangle(p, q + 1, q);
                    }
            }
        }

        // A horizontal line whose colour runs from one end to the other.
        void Band(VertexHelper v, Vector2 a, Vector2 b, float width, Color from, Color to)
        {
            float drawn = Mathf.Max(width, Feather * .9f);
            from.a *= width / drawn; to.a *= width / drawn;
            var p = new Vector2(0, drawn * .5f);
            SoftPolygon(v, new[] { a - p, b - p, b + p, a + p }, new[] { from, to, to, from }, Mathf.Min(Feather, drawn * .9f));
        }

        // Thin lines are widened to the feather and dimmed by the same ratio, so a 1 px line with a wide
        // feather does not turn into a bright smear.
        void Line(VertexHelper v, Vector2 a, Vector2 b, float width, Color c)
        {
            float drawn = Mathf.Max(width, Feather * .9f);
            c.a *= width / drawn;
            var d = b - a;
            var p = new Vector2(-d.y, d.x).normalized * drawn * .5f;
            SoftPolygon(v, new[] { a - p, b - p, b + p, a + p }, new[] { c, c, c, c }, Mathf.Min(Feather, drawn * .9f));
        }
    }
}
