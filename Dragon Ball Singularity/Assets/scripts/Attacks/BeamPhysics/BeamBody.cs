using System.Collections.Generic;
using UnityEngine;

namespace DBPU
{
    // The physical body of a beam: a stream of energy leaving a moving hand. Each stretch of the
    // beam remembers the direction it was fired in and carries it downstream at the propagation
    // speed, so sweeping the hand whips a curve down the column like a fire hose. Pins nail the head
    // to a point (a catching hand, a clash), and compression makes the column buckle and ripple
    // between its ends. Damage follows the same spline.
    [DefaultExecutionOrder(75)]
    public sealed class BeamBody : MonoBehaviour
    {
        public static readonly List<BeamBody> Active = new List<BeamBody>();
        const int MaxNodes = BeamRibbon.MaxSections, MaxParcels = 180, MaxPulses = 6;
        struct Pulse { public float Arc, Amplitude, Age, Length; }

        public BeamBehaviour Beam { get; private set; }
        public bool Live { get; private set; }
        public int NodeCount { get; private set; }
        public readonly Vector2[] Nodes = new Vector2[MaxNodes];
        public readonly float[] Arcs = new float[MaxNodes];
        public Vector2 Root { get; private set; }
        public Vector2 Tip { get; private set; }
        public Vector2 TipTangent { get; private set; }
        public Vector2 RootDirection { get; private set; }
        public Vector2 SpanStart { get; private set; }
        public float Length { get; private set; }
        public float Width { get; private set; }
        public BeamStyle Style { get; private set; }
        public bool Pinned { get; private set; }
        public Vector2 Pin { get; private set; }
        public float Strain;
        
        
        public bool Blocked { get; private set; }
        public Vector2 WallPoint { get; private set; }
        public readonly HashSet<PersonBehaviour> Immune = new HashSet<PersonBehaviour>();
        public float TipSpeed { get { return Beam ? Beam.realSpeed * DBPU.Beam_SpeedMultiplier : 0f; } }
        public float DistanceFor(float arc) { return Mathf.Max(0f, arc - tipAlong); }

        Transform arm; PersonBehaviour person;

        // Geometry captured when the beam appears
        Vector3 muzzleLocal; Vector2 muzzle, lineOffset, startOffset, tipLateral; Quaternion startRotation, endRotation;
        float fireAngle, tipAlong, gap, rootAngle, emitAngle, lastEmit; Vector2 lastRoot, rootVelocity; bool rootSeen;

        // Direction carried along the stream
        readonly float[] parcelArc = new float[MaxParcels], parcelAngle = new float[MaxParcels];
        int parcelHead, parcelCount;

        readonly Pulse[] pulses = new Pulse[MaxPulses]; int pulseCount;
        float clock, wallArc, wallClock, bucklePhase;
        Vector2 wallNormal; bool wallFresh;

        // Rendering
        BeamRibbon ribbon; BeamQuad muzzleOrb, muzzleCrown, tipOrb, tipCrown, aura, wallCrown;
        readonly Vector2[] drawNodes = new Vector2[MaxNodes];
        readonly float[] drawArcs = new float[MaxNodes], drawHalf = new float[MaxNodes], drawHeat = new float[MaxNodes], drawAlpha = new float[MaxNodes];
        readonly Vector2[] offsets = new Vector2[MaxNodes];
        readonly Vector3[] linePoints = new Vector3[MaxNodes];

        static readonly RaycastHit2D[] boxHits = new RaycastHit2D[64], rayHits = new RaycastHit2D[16];
        static readonly HashSet<int> seen = new HashSet<int>();
        static int hitMask = -1;
        const int WallLayer = 11;

        public static BeamBody Of(BeamBehaviour beam)
        {
            if (!beam) return null;
            if (beam.Body) return beam.Body;
            var body = beam.gameObject.AddComponent<BeamBody>();
            body.Beam = beam; beam.Body = body;
            body.person = beam.GetComponent<PersonBehaviour>();
            return body;
        }

        // The firing arm itself is frozen by PinFiringLimb; the beam only needs to know which arm it leaves.
        public void PrepareFire(Transform firingArm) { arm = firingArm; Register(); }

        // --------------------------------------------------------------------- external forces
        public void PinTo(Vector2 point) { Pinned = true; Pin = point; }
        public void Unpin() { Pinned = false; }

        // Sends a ripple from the head back towards the hands.
        public void Kick(float amplitude, float length = -1f)
        {
            if (!Live) return;
            if (pulseCount >= MaxPulses) { for (int i = 1; i < pulseCount; i++) pulses[i - 1] = pulses[i]; pulseCount--; }
            pulses[pulseCount++] = new Pulse { Arc = Length, Amplitude = amplitude, Length = length > 0f ? length : Mathf.Max(3f, Width * 2.4f) };
        }

        // ------------------------------------------------------------------------------ frame
        void Register() { if (!Active.Contains(this)) Active.Add(this); }

        void Capture()
        {
            Live = true; Register();
            Style = BeamStyle.Of(Beam);
            if (!arm) arm = Beam.UsedArm;
            Vector2 dir = ((Vector2)Beam.BeamDirection).sqrMagnitude > .001f ? ((Vector2)Beam.BeamDirection).normalized : Vector2.right;
            muzzle = Beam.StartBeamPosition;
            if (arm) muzzleLocal = arm.InverseTransformPoint(muzzle);
            Vector2 lineRoot = Beam.MiddleBeam ? (Vector2)Beam.MiddleBeam.GetPosition(0) : muzzle;
            lineOffset = lineRoot - muzzle;
            startOffset = (Vector2)Beam.StartBeam.transform.position - muzzle;
            startRotation = Beam.StartBeam.transform.rotation; endRotation = Beam.EndBeam.transform.rotation;
            Vector2 tip = Beam.LegacyEndPosition();
            float along = Vector2.Dot(tip - lineRoot, dir);
            tipAlong = along - Beam.Distance;
            tipLateral = (tip - lineRoot) - dir * along;
            gap = Mathf.Max(0f, -(Beam.MiddleMinusDistance + Beam.MiddleOffSet));
            fireAngle = rootAngle = emitAngle = lastEmit = Angle(dir);
            Root = lastRoot = lineRoot; rootVelocity = Vector2.zero; rootSeen = false;
            parcelCount = 0; pulseCount = 0; Immune.Clear();
            Pinned = false; Blocked = false; wallArc = float.MaxValue; Strain = 0f; clock = 0f; wallFresh = true;
            if (BeamFxAssets.Ready && ribbon == null) ribbon = new BeamRibbon();
            if (BeamFxAssets.Ready)
            {
                // The muzzle and head are drawn here now; the old particles would only muddy them.
                if (Beam.MiddleBeam) Beam.MiddleBeam.enabled = false;
                ShowPrefabs(false);
            }
            MakeQuads();
        }

        void ShowPrefabs(bool on)
        {
            foreach (var part in new[] { Beam.StartBeam, Beam.EndBeam })
            {
                if (!part) continue;
                foreach (var r in part.GetComponentsInChildren<ParticleSystemRenderer>(true)) r.enabled = on;
            }
        }

        void MakeQuads()
        {
            if (muzzleOrb != null) return;
            muzzleOrb = new BeamQuad("DBPU beam muzzle orb", BeamQuad.Orb, "Background", -12);
            muzzleCrown = new BeamQuad("DBPU beam muzzle crown", BeamQuad.Crown, "Background", -13);
            tipOrb = new BeamQuad("DBPU beam head orb", BeamQuad.Orb, "Background", -11);
            tipCrown = new BeamQuad("DBPU beam head crown", BeamQuad.Crown, "Background", -12);
            aura = new BeamQuad("DBPU beam fighter aura", BeamQuad.Flame, "Background", -40);
            wallCrown = new BeamQuad("DBPU beam impact crown", BeamQuad.Crown, "Background", -11);
            HideQuads();
        }

        void HideQuads()
        {
            if (muzzleOrb == null) return;
            muzzleOrb.Hide(); muzzleCrown.Hide(); tipOrb.Hide(); tipCrown.Hide(); aura.Hide(); wallCrown.Hide();
        }

        void Retire()
        {
            if (Live && Beam && Beam.MiddleBeam)
            {
                Beam.MiddleBeam.enabled = true;
                Beam.MiddleBeam.positionCount = 2;
                Beam.MiddleBeam.SetPosition(0, Root);
                Beam.MiddleBeam.SetPosition(1, Tip);
            }
            if (Live && Beam) ShowPrefabs(true);
            Live = false; Pinned = false; NodeCount = 0;
            if (ribbon != null) ribbon.Hide();
            HideQuads();
            Active.Remove(this);
        }

        void LateUpdate()
        {
            if (!Beam) { Retire(); return; }

            bool on = BeamPhysics.On && Beam.StillActive && Beam.StartBeam && Beam.EndBeam && Beam.MergeHost == null;
            if (!on)
            {
                if (Live) Retire();
                if (!Beam.Firing) Active.Remove(this);
                return;
            }
            if (!Live) Capture();
            float dt = DbpuFx.Dt;
            clock += dt;
            StepRoot(dt);
            StepStream(dt);
            Build();
            ApplyBuckle(dt);
            Finish();
            Present(dt);
        }

        void StepRoot(float dt)
        {
            if (arm && arm.gameObject.activeInHierarchy)
            {
                // The ragdoll never holds perfectly still. Small shivers of the hand are smoothed away; real moves are followed.
                Vector2 raw = arm.TransformPoint(muzzleLocal);
                float off = Vector2.Distance(raw, muzzle);
                muzzle = off > 2.5f || dt <= 0f ? raw : Vector2.Lerp(muzzle, raw, 1f - Mathf.Exp(-dt * (off < .3f ? 4f : 18f)));
            }
            RootDirection = Direction(rootAngle);
            float turned = rootAngle - fireAngle;
            Root = muzzle + DbpuFx.Rotate(lineOffset, turned * Mathf.Deg2Rad);
            if (dt > 0f)
            {
                Vector2 v = (Root - lastRoot) / dt;
                // A teleport or a snapped limb is not a velocity the stream should bend with.
                if (v.sqrMagnitude > 3600f) v = Vector2.zero;
                // Only a real sweep of the body bends the stream, not the slight drift of a standing fighter.
                float speed = v.magnitude;
                v = speed > 5f ? v * ((speed - 5f) / speed) : Vector2.zero;
                rootVelocity = rootSeen ? Vector2.Lerp(rootVelocity, v, 1f - Mathf.Exp(-dt * 9f)) : Vector2.zero;
                rootSeen = true;
            }
            lastRoot = Root;
            float c = Propagation;
            Vector2 stream = RootDirection * c - rootVelocity;
            emitAngle = rootAngle + Mathf.DeltaAngle(rootAngle, Angle(stream));

            if (Beam.StartBeam && !Beam.StartBeam.transform.parent)
            {
                Beam.StartBeam.transform.position = muzzle + DbpuFx.Rotate(startOffset, turned * Mathf.Deg2Rad);
                Beam.StartBeam.transform.rotation = Quaternion.Euler(0f, 0f, turned) * startRotation;
            }
        }

        float Propagation { get { return Mathf.Max(TipSpeed, Beam.OriginalSpeed * 2.4f * Mathf.Max(.2f, DBPU.Beam_SpeedMultiplier), 70f); } }

        int ParcelIndex(int age) { return (parcelHead - age + MaxParcels) % MaxParcels; }

        void StepStream(float dt)
        {
            float move = Propagation * dt;
            for (int i = 0; i < parcelCount; i++) parcelArc[ParcelIndex(i)] += move;
            float reach = Mathf.Max(1f, Length);
            while (parcelCount >= 2 && parcelArc[ParcelIndex(parcelCount - 2)] > reach) parcelCount--;
            if (Mathf.Abs(emitAngle - lastEmit) > .35f)
            {
                parcelHead = (parcelHead + 1) % MaxParcels;
                parcelArc[parcelHead] = Mathf.Max(move, .01f); parcelAngle[parcelHead] = lastEmit;
                if (parcelCount < MaxParcels) parcelCount++;
            }
            else if (parcelCount == 1 && parcelArc[parcelHead] > reach && Mathf.Abs(parcelAngle[parcelHead] - emitAngle) < .2f) parcelCount = 0;
            lastEmit = emitAngle;
            for (int i = pulseCount - 1; i >= 0; i--)
            {
                var p = pulses[i];
                p.Age += dt; p.Arc -= dt * 85f;
                if (p.Age > 1.1f || p.Arc < 0f) { for (int k = i + 1; k < pulseCount; k++) pulses[k - 1] = pulses[k]; pulseCount--; continue; }
                pulses[i] = p;
            }
        }

        float StreamAngle(float s, ref int cursor)
        {
            if (parcelCount == 0) return emitAngle;
            float first = parcelArc[parcelHead];
            if (s <= first) return Mathf.Lerp(emitAngle, parcelAngle[parcelHead], s / Mathf.Max(.001f, first));
            while (cursor + 1 < parcelCount && parcelArc[ParcelIndex(cursor + 1)] < s) cursor++;
            if (cursor + 1 >= parcelCount) return parcelAngle[ParcelIndex(parcelCount - 1)];
            int a = ParcelIndex(cursor), b = ParcelIndex(cursor + 1);
            return Mathf.Lerp(parcelAngle[a], parcelAngle[b], (s - parcelArc[a]) / Mathf.Max(.001f, parcelArc[b] - parcelArc[a]));
        }

        // Integrates one stretch of the path; if it has to arrive somewhere, the error is bled in
        // along its length so the column bends towards the target instead of snapping at the end.
        void Span(Vector2 start, float length, bool arrive, Vector2 end, float ds)
        {
            int room = MaxNodes - NodeCount - 1;
            int segments = Mathf.Clamp(Mathf.CeilToInt(length / ds), 1, Mathf.Max(1, room));
            float step = length / segments;
            int first = NodeCount, cursor = 0;
            Vector2 p = start;
            for (int j = 1; j <= segments; j++)
            {
                float s = (j - .5f) * step;
                float a = StreamAngle(s, ref cursor) * Mathf.Deg2Rad;
                p += new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * step;
                Nodes[NodeCount] = p; NodeCount++;
            }
            if (!arrive) return;
            Vector2 error = end - p;
            for (int j = 1; j <= segments; j++)
            {
                float x = j / (float)segments;
                Nodes[first + j - 1] += error * (x * x * (.4f + .6f * x));
            }
        }

        void Build()
        {
            NodeCount = 0;
            Nodes[NodeCount++] = Root;
            float wanted = Mathf.Max(.05f, Beam.Distance + tipAlong);
            if (Blocked && !Pinned) wanted = Mathf.Min(wanted, wallArc);
            float pinChord = Pinned ? Vector2.Distance(Root, Pin) : 0f;
            float total = Pinned ? pinChord : wanted;
            Width = Beam.MiddleBeam ? Mathf.Max(0f, Beam.MiddleBeam.startWidth) : Width;
            float ds = Mathf.Max(1.1f, Width * .5f, total / (MaxNodes - 4));
            SpanStart = Root;
            if (Pinned) Span(Root, Mathf.Max(.02f, pinChord), true, Pin, ds);
            else
            {
                Span(Root, Mathf.Max(.05f, total), false, Vector2.zero, ds);
                Vector2 lateral = DbpuFx.Rotate(tipLateral, (rootAngle - fireAngle) * Mathf.Deg2Rad);
                for (int i = 1; i < NodeCount; i++) Nodes[i] += lateral * (i / (float)(NodeCount - 1));
            }
        }

        // Compression buckles the column between its ends; struck beams carry ripples back upstream.
        void ApplyBuckle(float dt)
        {
            Arcs[0] = 0f;
            for (int i = 1; i < NodeCount; i++) Arcs[i] = Arcs[i - 1] + Vector2.Distance(Nodes[i - 1], Nodes[i]);
            float total = Arcs[NodeCount - 1];
            Length = total;
            if (NodeCount < 3 || total < .5f) return;
            bucklePhase += dt * (3f + Strain * 4f);
            float amp = Mathf.Lerp(0f, Mathf.Min(Width * .2f, total * .03f), Mathf.Clamp01(Strain));
            for (int i = 1; i < NodeCount - 1; i++)
            {
                float s = Arcs[i], x = s / total;
                float value = Mathf.Sin(x * 8.2f - bucklePhase) * .85f + Mathf.Sin(x * 19.5f + bucklePhase * 1.7f) * .15f;
                float shift = amp * Mathf.Pow(Mathf.Sin(x * Mathf.PI), .8f) * value;
                for (int k = 0; k < pulseCount; k++)
                {
                    var p = pulses[k];
                    float u = (s - p.Arc) / p.Length;
                    if (u > -2.6f && u < 2.6f) shift += p.Amplitude * Mathf.Exp(-u * u) * Mathf.Sin(u * 3.1f) * Mathf.Exp(-p.Age * 2.4f);
                }
                Vector2 d = Nodes[i + 1] - Nodes[i - 1];
                offsets[i] = new Vector2(-d.y, d.x).normalized * shift;
            }
            for (int i = 1; i < NodeCount - 1; i++) Nodes[i] += offsets[i];
            for (int i = 1; i < NodeCount; i++) Arcs[i] = Arcs[i - 1] + Vector2.Distance(Nodes[i - 1], Nodes[i]);
            Length = Arcs[NodeCount - 1];
        }

        void Finish()
        {
            Tip = Nodes[NodeCount - 1];
            Vector2 last = Tip - Nodes[Mathf.Max(0, NodeCount - 2)];
            TipTangent = last.sqrMagnitude > .00001f ? last.normalized : RootDirection;
            Beam.BeamDirection = TipTangent;
            Beam.StartBeamPosition = muzzle;
            Beam.EndBeamPosition = Tip;
            if (Pinned || (Blocked && Beam.Distance > DistanceFor(wallArc))) Beam.Distance = DistanceFor(Length);
            var endBeam = Beam.EndBeam.transform;
            endBeam.position = Tip;
            endBeam.rotation = Quaternion.Euler(0f, 0f, Angle(TipTangent) - fireAngle) * endRotation;
            var comp = Beam.EndBeam.GetComponent<BeamComponent>();
            if (comp) comp.RayDirection = RootDirection;
        }

        void Present(float dt)
        {
            float fade = Mathf.Clamp01(Beam.Opacity);
            float strain = Mathf.Clamp01(Strain);
            if (ribbon == null)
            {
                if (Beam.MiddleBeam)
                {
                    int count = Mathf.Min(NodeCount, MaxNodes);
                    for (int i = 0; i < count; i++) linePoints[i] = Nodes[i];
                    Beam.MiddleBeam.positionCount = count;
                    Beam.MiddleBeam.SetPositions(linePoints);
                }
            }
            else DrawRibbon(fade, strain);
            float w = Mathf.Max(Width, .35f);
            bool show = fade > .02f && Width > .02f;
            if (!show || muzzleOrb == null) { HideQuads(); return; }
            muzzleOrb.Style(Style); muzzleCrown.Style(Style); tipOrb.Style(Style); tipCrown.Style(Style); wallCrown.Style(Style);
            const float breath = 1f;
            muzzleOrb.Place(Root + RootDirection * w * .1f, w * .62f * breath); muzzleOrb.Opacity = fade; muzzleOrb.Apply(dt);
            muzzleCrown.Dir = RootDirection; muzzleCrown.Spread = .8f; muzzleCrown.Power = 1f; muzzleCrown.Speed = 1.15f;
            muzzleCrown.Place(Root + RootDirection * w * .1f, w * (2.5f + strain * 1.1f)); muzzleCrown.Opacity = fade; muzzleCrown.Apply(dt);
            if (Pinned) { tipOrb.Hide(); tipCrown.Hide(); }
            else
            {
                Vector2 head = VisualEnd;
                tipOrb.Place(head, w * .6f * breath); tipOrb.Opacity = fade; tipOrb.Apply(dt);
                tipCrown.Dir = -TipTangent; tipCrown.Spread = .72f; tipCrown.Speed = 1.4f;
                tipCrown.Place(head, w * 2.1f); tipCrown.Opacity = fade; tipCrown.Apply(dt);
            }
            if (person)
            {
                aura.Style(Style); aura.Speed = 1.2f;
                Vector2 c = DbpuFx.Centre(person);
                aura.Place(c + Vector2.up * .2f, 2.4f + strain * 1.1f); aura.Opacity = fade * (.62f + strain * .3f); aura.Apply(dt);
            }
            else aura.Hide();
            StepWall(dt, fade, w);
        }

        Vector2 VisualEnd
        {
            get
            {
                float end = Mathf.Max(0f, Length - gap * .75f);
                for (int i = 1; i < NodeCount; i++)
                    if (Arcs[i] >= end) return Vector2.Lerp(Nodes[i - 1], Nodes[i], (end - Arcs[i - 1]) / Mathf.Max(.0001f, Arcs[i] - Arcs[i - 1]));
                return Tip;
            }
        }

        void DrawRibbon(float fade, float strain)
        {
            float visual = Pinned ? Length : Mathf.Max(.05f, Length - gap * .75f);
            int count = 0;
            for (int i = 0; i < NodeCount && count < MaxNodes; i++)
            {
                if (Arcs[i] >= visual)
                {
                    float t = (visual - Arcs[Mathf.Max(0, i - 1)]) / Mathf.Max(.0001f, Arcs[i] - Arcs[Mathf.Max(0, i - 1)]);
                    drawNodes[count] = Vector2.Lerp(Nodes[Mathf.Max(0, i - 1)], Nodes[i], t);
                    drawArcs[count] = visual; count++;
                    break;
                }
                drawNodes[count] = Nodes[i]; drawArcs[count] = Arcs[i]; count++;
            }
            float w = Mathf.Max(Width, .001f), radius = Mathf.Max(.05f, Style.Radius);
            float cap = w * .7f;
            for (int i = 0; i < count; i++)
            {
                float s = drawArcs[i], d = visual - s;
                float fromRoot = s / (w * .9f), fromEnd = d / (w * 1.05f);
                float p = 1f + .5f * Mathf.Exp(-fromRoot * fromRoot);
                if (Pinned) p += (.3f + strain * .55f) * Mathf.Exp(-fromEnd * fromEnd);
                else if (d < cap) { float k = 1f - d / cap; p *= Mathf.Sqrt(Mathf.Max(0f, 1f - k * k)); }
                drawHalf[i] = w * .5f / radius * p;
                float rootHeat = Mathf.Exp(-(s / w) * (s / w)) * .25f;
                float endHeat = Pinned ? Mathf.Exp(-(d / (w * 1.4f)) * (d / (w * 1.4f))) * (.35f + strain * .6f) : 0f;
                drawHeat[i] = Mathf.Clamp01(rootHeat + endHeat);
                // The ends melt into the knots of plasma drawn over them.
                drawAlpha[i] = Mathf.Clamp01(s / (w * .5f)) * (Pinned ? Mathf.Clamp01(d / (w * .3f)) : 1f);
            }
            ribbon.Draw(drawNodes, drawArcs, drawHalf, drawHeat, drawAlpha, count, Style, w, strain, Beam.MiddleBeam ? fade : 0f, clock);
        }

        // ----------------------------------------------------------------- the world in the way
        void StepWall(float dt, float fade, float w)
        {
            if (!Blocked || Pinned || !Beam.Firing) { wallCrown.Hide(); return; }
            wallClock -= dt;
            if (wallFresh)
            {
                wallFresh = false; wallClock = .12f;
                float s = Mathf.Clamp(w * .55f, .4f, 3.4f);
                BeamGround.Erupt(WallPoint, s, Style.Mid, wallNormal);
                BeamContact.Burst(WallPoint, wallNormal, Style, Style, s);
                DbpuFx.Shake(WallPoint, 1.4f + s * 1.3f);
                DbpuFx.Sound(WallPoint, ImpactFramesModule.SfxClash, .8f);
            }
            else if (wallClock <= 0f)
            {
                wallClock = Random.Range(.09f, .17f);
                float s = Mathf.Clamp(w * .4f, .3f, 2.4f);
                BeamGround.Tear(WallPoint, Random.insideUnitCircle, .9f, Style.Mid);
                if (Random.value < .35f) BeamGround.Erupt(WallPoint + new Vector2(-wallNormal.y, wallNormal.x) * Random.Range(-w, w) * .5f, s * .5f, Style.Mid, wallNormal);
                if (Random.value < .3f) BeamContact.Shock(WallPoint, Style, 2f + s * 2.6f, .35f);
                DbpuFx.Shake(WallPoint, .15f + s * .15f);
            }
            wallCrown.Dir = wallNormal; wallCrown.Spread = .62f; wallCrown.Speed = 1.8f;
            wallCrown.Place(WallPoint, w * 2.5f); wallCrown.Opacity = fade; wallCrown.Apply(dt);
        }

        public void CastHits(BeamComponent comp)
        {
            if (!Live || NodeCount < 2) return;
            if (hitMask < 0) hitMask = LayerMask.GetMask("Objects", "CollidingDebris", "Bounds");
            float w = Mathf.Max(.02f, Width * .96f), stride = Mathf.Max(w * .9f, 2.2f);
            var self = Beam.transform;
            Physics2D.queriesHitTriggers = true;
            seen.Clear();
            for (int i = 0; i < NodeCount - 1;)
            {
                int j = i + 1;
                while (j < NodeCount - 1 && Arcs[j] - Arcs[i] < stride) j++;
                Vector2 d = Nodes[j] - Nodes[i]; float length = d.magnitude;
                if (length > .001f)
                {
                    d /= length;
                    int n = Physics2D.BoxCastNonAlloc(Nodes[i], new Vector2(.1f, w), Angle(d), d, boxHits, length, hitMask);
                    for (int k = 0; k < n; k++)
                    {
                        var col = boxHits[k].collider;
                        if (!col || col.transform.root == self || !seen.Add(col.GetInstanceID())) continue;
                        comp.StartCoroutine(comp.OnHit(boxHits[k], d));
                    }
                }
                i = j;
            }
            Physics2D.queriesHitTriggers = false;
            if (!Pinned) FindWall(comp);
            else { Blocked = false; wallArc = float.MaxValue; wallFresh = true; }
        }

        void FindWall(BeamComponent comp)
        {
            float best = float.MaxValue; Vector2 point = Vector2.zero, normal = Vector2.up;
            int mask = 1 << WallLayer;
            float stride = Mathf.Max(Length / 18f, 2.5f);
            for (int i = 0; i < NodeCount - 1 && best == float.MaxValue;)
            {
                int j = i + 1;
                while (j < NodeCount - 1 && Arcs[j] - Arcs[i] < stride) j++;
                Vector2 d = Nodes[j] - Nodes[i]; float length = d.magnitude;
                if (length > .001f) TraceWall(Nodes[i], d / length, length, Arcs[i], mask, ref best, ref point, ref normal);
                i = j;
            }
            if (best == float.MaxValue)
            {
                float ahead = TipSpeed * Mathf.Max(DbpuFx.Dt, .02f) * 1.6f + .1f;
                TraceWall(Tip, TipTangent, ahead, Length, mask, ref best, ref point, ref normal);
            }
            bool hit = best != float.MaxValue;
            if (hit)
            {
                wallArc = best; WallPoint = point; wallNormal = normal;
                if (!Blocked) { Blocked = true; wallFresh = true; }
                comp.BodyHitWall(point);
            }
            else { Blocked = false; wallArc = float.MaxValue; }
        }

        void TraceWall(Vector2 from, Vector2 direction, float length, float arc, int mask, ref float best, ref Vector2 point, ref Vector2 normal)
        {
            int n = Physics2D.RaycastNonAlloc(from, direction, rayHits, length, mask);
            for (int k = 0; k < n; k++)
            {
                var hit = rayHits[k];
                if (!hit.collider || hit.distance <= .001f || hit.collider.transform.root == Beam.transform.root || hit.collider.GetComponent<KiShieldSurface>()) continue;
                if (arc + hit.distance < best) { best = arc + hit.distance; point = hit.point; normal = hit.normal; }
            }
        }

        // Ki Shields stop the beam wherever its path first crosses them, however it is bent.
        public bool ClipShields(ref float distance)
        {
            if (!Live || Pinned || NodeCount < 2) return false;
            Beam.ShieldBarrier = null;
            double pressure = (double)Beam.RealDamage * 60;
            int id = Beam.GetInstanceID();
            KiShield barrier;
            for (int i = 0; i < NodeCount - 1; i++)
            {
                Vector2 d = Nodes[i + 1] - Nodes[i]; float length = d.magnitude;
                if (length < .001f) continue;
                if (KiShield.ClipAttack(Beam.gameObject, id, Nodes[i], d / length, ref length, 0f, pressure, out barrier))
                {
                    Beam.ShieldBarrier = barrier; distance = DistanceFor(Arcs[i] + length);
                    return true;
                }
            }
            float ahead = distance + tipAlong - Length;
            if (ahead > 0f && KiShield.ClipAttack(Beam.gameObject, id, Tip, TipTangent, ref ahead, 0f, pressure, out barrier))
            {
                Beam.ShieldBarrier = barrier; distance = DistanceFor(Length + ahead);
                return true;
            }
            return false;
        }

        // The point on the spline nearest to p.
        public Vector2 ClosestPoint(Vector2 p, out float arc, out Vector2 tangent)
        {
            arc = 0f; tangent = RootDirection; Vector2 best = Root; float bestDistance = float.MaxValue;
            for (int i = 0; i < NodeCount - 1; i++)
            {
                Vector2 a = Nodes[i], d = Nodes[i + 1] - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, d) / Mathf.Max(.0001f, d.sqrMagnitude));
                Vector2 q = a + d * t; float m = (p - q).sqrMagnitude;
                if (m < bestDistance) { bestDistance = m; best = q; arc = Arcs[i] + d.magnitude * t; tangent = d.normalized; }
            }
            return best;
        }

        void OnDestroy()
        {
            Active.Remove(this);
            if (ribbon != null) ribbon.Destroy();
            if (muzzleOrb != null) { muzzleOrb.Destroy(); muzzleCrown.Destroy(); tipOrb.Destroy(); tipCrown.Destroy(); aura.Destroy(); wallCrown.Destroy(); }
        }

        public static float Angle(Vector2 d) { return Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg; }
        public static Vector2 Direction(float degrees) { float r = degrees * Mathf.Deg2Rad; return new Vector2(Mathf.Cos(r), Mathf.Sin(r)); }
    }
}
