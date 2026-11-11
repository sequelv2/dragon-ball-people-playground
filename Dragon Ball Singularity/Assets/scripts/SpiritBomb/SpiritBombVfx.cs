using System;
using System.IO;
using UnityEngine;

namespace DBPU
{
    [DefaultExecutionOrder(240)]
    public sealed class SpiritBombVfx : MonoBehaviour
    {
        static string directory;
        static Sprite[] frames;
        static bool attempted;
        static Material spriteMaterial;
        public static bool HasBakedPlasma { get { Prepare(); return frames != null; } }
        public static void Initialize(string path) { directory = path; }
        static void Prepare()
        {
            if (attempted) return; attempted = true;
            try
            {
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!texture.LoadImage(File.ReadAllBytes(Path.Combine(directory, "Assets/VFX/SpiritBomb/plasma-atlas.png"))))
                    throw new IOException("Invalid Spirit Bomb atlas");
                texture.name = "DBPU Blender plasma flipbook"; texture.wrapMode = TextureWrapMode.Clamp; texture.filterMode = FilterMode.Bilinear;
                frames = new Sprite[48]; int cell = texture.width / 8;
                for (int i = 0; i < frames.Length; i++)
                    frames[i] = Sprite.Create(texture, new Rect(i % 8 * cell, texture.height - (i / 8 + 1) * cell, cell, cell), new Vector2(.5f, .5f), cell, 0, SpriteMeshType.FullRect);
            }
            catch (Exception ex) { Debug.LogWarning("[DBPU Spirit Bomb] Plasma atlas unavailable: " + ex.Message); }
        }
        SpiritBombProjectile bomb;
        SpriteRenderer shell, inner;
        readonly LineRenderer[] lines = new LineRenderer[32];
        float clock, nextFrame, pulse, donation, explosionAge = -1;
        bool redrawn;
        public static SpiritBombVfx Create(SpiritBombProjectile bomb)
        {
            Prepare();
            var go = new GameObject("Genkidama plasma, corona and energy currents"); go.transform.SetParent(bomb.transform, false);
            var effect = go.AddComponent<SpiritBombVfx>(); effect.bomb = bomb; effect.Build(); return effect;
        }
        SpriteRenderer SpriteLayer(string name, int order)
        {
            var go = new GameObject(name); go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>(); sr.sortingLayerName = "Top"; sr.sortingOrder = order;
            if (!spriteMaterial) spriteMaterial = new Material(Shader.Find("Sprites/Default")) { name = "DBPU Blender plasma" };
            sr.sharedMaterial = spriteMaterial; if (frames != null) sr.sprite = frames[0];
            return sr;
        }
        void Build()
        {
            shell = SpriteLayer("Blender plasma", 25); inner = SpriteLayer("Counter rotating plasma", 26);
            for (int i = 0; i < lines.Length; i++) lines[i] = KiSurgeArt.Line(transform, "Genkidama current " + i, 33);
            redrawn = bomb.Orb != null;
            if (redrawn)
            {
                // The sphere itself is drawn by its orb; only the currents of gathering energy remain here.
                shell.enabled = inner.enabled = false;
                for (int i = 0; i < 16; i++) lines[i].enabled = false;
            }
        }
        public void DonationFlash() { donation = Mathf.Min(1, donation + .12f); }
        public void Pulse() { pulse = 1; }
        public void Detonate()
        {
            explosionAge = 0;
            shell.enabled = inner.enabled = false;
            foreach (var line in lines) line.enabled = false;
        }
        void LateUpdate()
        {
            if (!bomb || explosionAge >= 0 || (Global.main && Global.main.Paused)) return;
            clock += Time.deltaTime; if (explosionAge >= 0) explosionAge += Time.deltaTime;
            pulse = Mathf.Max(0, pulse - Time.deltaTime * 4.4f); donation = Mathf.Max(0, donation - Time.deltaTime * 1.5f);
            if (clock < nextFrame) return; nextFrame = clock + 1f/30;
            Draw();
        }
        void Style(LineRenderer line, float width, Color colour)
        {
            line.startWidth = line.endWidth = width; line.startColor = line.endColor = colour;
        }
        Vector3 Point(Vector2 p) { return new Vector3(p.x, p.y, -.2f); }
        void Draw()
        {
            float radius = bomb.Radius;
            Vector2 centre = bomb.transform.position;
            bool gathering = bomb.Phase == SpiritBombPhase.Charging;
            bool contained = bomb.Phase == SpiritBombPhase.Containment;
            float boom = Mathf.Max(0, explosionAge), fade = explosionAge < 0 ? 1 : Mathf.Clamp01(1 - boom/1.8f);
            float shellScale = radius * 3.73f * (1 + Mathf.Sin(clock*5)*.009f + pulse*.025f + donation*.02f);
            if (explosionAge >= 0) shellScale *= 1 + boom * 1.7f;
            if (frames != null && !redrawn)
            {
                shell.sprite = frames[(int)(clock*24)%48]; inner.sprite = frames[(int)(clock*24+23)%48];
                shell.transform.localScale = Vector3.one * shellScale;
                inner.transform.localScale = Vector3.one * shellScale * .92f;
                inner.transform.localRotation = Quaternion.Euler(0,0,-clock*13);
                float alpha = (contained ? .38f : .88f) * fade;
                shell.color = new Color(1,1,1,alpha); inner.color = new Color(.72f,.9f,1,alpha*.25f);
            }
            Color cyan = new Color(.33f,.8f,1,fade*.84f), white = new Color(.88f,.98f,1,fade);
            for (int k=redrawn?4:0;k<4;k++)
            {
                var line=lines[k];
                float rr=radius*(1.02f+k*.055f + pulse*(.06f+k*.08f));
                float ellipse=k==1?.52f:k==2?.8f:1;
                if(explosionAge>=0) { rr=radius*(1+boom*(1.5f+k*.65f)); ellipse= k%2==0?1:.45f; }
                Style(line,Mathf.Clamp(radius*.014f,.018f,.14f)*(1+2*pulse),k%2==0?white:cyan);
                float rotation=clock*(k%2==0?.17f:-.2f)+k;
                for(int p=0;p<33;p++)
                {
                    float a=p*Mathf.PI*2/32;
                    float ripple=1+Mathf.Sin(a*7+clock*4+k)*.007f;
                    Vector2 q=new Vector2(Mathf.Cos(a)*rr,Mathf.Sin(a)*rr*ellipse)*ripple;
                    q=new Vector2(q.x*Mathf.Cos(rotation)-q.y*Mathf.Sin(rotation),q.x*Mathf.Sin(rotation)+q.y*Mathf.Cos(rotation));
                    line.SetPosition(p,Point(centre+q));
                }
            }
            for(int k=redrawn?16:4;k<16;k++)
            {
                var line=lines[k];float seed=k*2.399963f;
                Style(line,Mathf.Clamp(radius*.008f,.012f,.1f),new Color(.67f,.92f,1,fade*(.42f+donation*.4f+pulse*.18f)));
                for(int p=0;p<33;p++)
                {
                    float t=p/32f,a=seed+clock*(k%2==0?.3f:-.27f)+t*.75f;
                    float r=radius*(1.04f+Mathf.Sin(t*15+Mathf.Floor(clock*22)+k)*.017f+Mathf.Sin(t*Mathf.PI)*(.05f+pulse*.14f));
                    if(explosionAge>=0)r*=1+boom*2.2f;
                    line.SetPosition(p,Point(centre+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*r));
                }
            }
            for(int k=16;k<32;k++)
            {
                var line=lines[k];float seed=k*2.399963f;
                float head=Mathf.Repeat(clock*(.42f+donation*.6f)+k*.061f,1);
                Style(line,Mathf.Clamp(radius*.01f,.013f,.14f),new Color(.7f,.93f,1,fade*(gathering?.85f:redrawn?0f:contained?.65f:.3f)));
                for(int p=0;p<33;p++)
                {
                    float t=Mathf.Clamp01(head-p*.003f),a=seed+Mathf.Sin(t*5+k)*.12f;
                    float r=gathering?radius*(1+2.1f*(1-t)):radius*(.12f+t*.95f);
                    if(explosionAge>=0)r=radius*(1+boom*(1.8f+(k%4)*.7f)+t*.8f);
                    line.SetPosition(p,Point(centre+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*r));
                }
            }
        }
    }
}
