using UnityEngine;
using UnityEngine.UI;

namespace DBPU
{
    // Slow continuous rotation. Inside its own canvas this is a transform-only update.
    public sealed class DbSpin : MonoBehaviour
    {
        public float Speed = 8f;
        void Update() { transform.Rotate(0f, 0f, Speed * Time.unscaledDeltaTime); }
    }

    // Spotlight for character art: tinted glow, rotating rays and a sliding crossfade between images.
    public sealed class DbStage
    {
        readonly Image first, second;
        readonly Vector2 home;
        bool usingFirst = true;
        Sprite shownSprite;

        public RectTransform Root { get; private set; }
        public RectTransform Overlay { get; private set; }

        public DbStage(Transform parent, float width, float height, DbWindow window)
        {
            var well = DbUi.Well(parent, "Stage");
            Root = well.Rt();
            Root.sizeDelta = new Vector2(width, height);

            var clip = DbUi.Node("Clip", Root);
            clip.Stretch(2f, 2f, 2f, 2f);
            clip.gameObject.AddComponent<RectMask2D>();

            float glowSize = Mathf.Max(width, height) * 1.25f;
            var glow = DbUi.Img(clip, DbAtlas.Glow, DbTheme.Alpha(window.Accent, .2f), "Glow");
            glow.Rt().Centre(0f, 10f, glowSize, glowSize);
            window.BindAccent(glow, .2f);

            var rays = DbUi.Node("Rays", clip);
            rays.Centre(0f, 10f, glowSize * 1.25f, glowSize * 1.25f);
            rays.gameObject.AddComponent<Canvas>();
            var burst = DbUi.Img(rays, DbAtlas.Burst, DbTheme.Alpha(window.Accent, .08f), "Burst");
            burst.Rt().Stretch();
            window.BindAccent(burst, .08f);
            rays.gameObject.AddComponent<DbSpin>().Speed = 7f;

            float inset = height * .03f;
            first = Art(clip, "ArtA", inset);
            second = Art(clip, "ArtB", inset);
            home = first.Rt().anchoredPosition;

            var shade = DbUi.Img(clip, DbAtlas.GradUp, new Color(.02f, .03f, .06f, .95f), "BottomShade");
            shade.Rt().anchorMin = Vector2.zero;
            shade.Rt().anchorMax = new Vector2(1f, 0f);
            shade.Rt().pivot = new Vector2(.5f, 0f);
            shade.Rt().offsetMin = Vector2.zero;
            shade.Rt().offsetMax = new Vector2(0f, height * .34f);
            shade.Rt().localScale = new Vector3(1f, -1f, 1f);
            shade.Rt().pivot = new Vector2(.5f, .5f);
            shade.Rt().anchoredPosition = new Vector2(0f, height * .17f);

            var frame = DbUi.Img(Root, DbAtlas.ChamferTightLine, DbTheme.Alpha(window.Accent, .7f), "Frame");
            frame.Rt().Stretch();
            window.BindAccent(frame, .7f);

            Overlay = DbUi.Node("Overlay", Root);
            Overlay.Stretch();
        }

        static Image Art(Transform parent, string name, float inset)
        {
            var image = DbUi.Img(parent, null, new Color(1f, 1f, 1f, 0f), name);
            image.preserveAspect = true;
            image.enabled = false;
            image.Rt().Stretch(inset, inset, inset, inset);
            return image;
        }

        public void Show(Sprite sprite, int direction = 1, bool animate = true)
        {
            if (sprite == shownSprite) return;
            shownSprite = sprite;
            var incoming = usingFirst ? second : first;
            var outgoing = usingFirst ? first : second;
            usingFirst = !usingFirst;

            incoming.sprite = sprite;
            incoming.enabled = sprite;
            incoming.transform.SetAsLastSibling();
            // Keep the shade above the art.
            var shade = incoming.transform.parent.Find("BottomShade");
            if (shade) shade.SetAsLastSibling();

            if (!animate)
            {
                DbMotion.Stop(incoming); DbMotion.Stop(outgoing);
                incoming.color = Color.white;
                incoming.Rt().anchoredPosition = home;
                outgoing.color = new Color(1f, 1f, 1f, 0f);
                outgoing.enabled = false;
                return;
            }

            incoming.color = new Color(1f, 1f, 1f, 0f);
            incoming.Rt().anchoredPosition = home + new Vector2(46f * direction, 0f);
            DbMotion.Tint(incoming, Color.white, .18f);
            DbMotion.Move(incoming.Rt(), home, .3f, DbEase.OutQuint);
            DbMotion.Tint(outgoing, new Color(1f, 1f, 1f, 0f), .14f);
            DbMotion.Move(outgoing.Rt(), home - new Vector2(36f * direction, 0f), .2f, DbEase.OutCubic);
        }
    }
}
