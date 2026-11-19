using System.Collections.Generic;
using Hollow.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Hollow.PlayerSide.UI
{
    /// <summary>
    /// The Knight's HUD in the top-left corner: the soul vessel with its rising soul fill,
    /// and a row of masks. It builds itself at runtime from a HudArt asset and follows
    /// GameEvents.PlayerHealthChanged / PlayerFocusChanged, so it never talks to the Knight directly.
    /// </summary>
    public class PlayerHud : MonoBehaviour
    {
        [SerializeField] HudArt art;
        [Tooltip("Overall size. 1 = the art's own pixel size on a 1920x1080 screen.")]
        [SerializeField] float scale = 0.8f;
        [SerializeField] Vector2 screenMargin = new Vector2(40f, 30f);

        [Header("Masks (in frame pixels, from the frame's top-left)")]
        [SerializeField] Vector2 firstMaskCenter = new Vector2(188f, 34f);
        [SerializeField] float maskSpacing = 58f;
        [Tooltip("How much a mask pops when it breaks or refills.")]
        [SerializeField] float maskPunch = 0.4f;

        [Header("Soul orb")]
        [Tooltip("How fast the fill catches up, in full orbs per second.")]
        [SerializeField] float fillSpeed = 2.5f;
        [SerializeField] bool showFaceWhenFull = true;

        RectTransform root, orbRect, fillRect;
        Image face, soulTexture;
        readonly List<Image> masks = new List<Image>();
        readonly List<float> punches = new List<float>();
        int filledMasks = -1;
        float soulTarget, soulShown, orbSize;

        void Awake() => Build();

        void OnEnable()
        {
            GameEvents.PlayerHealthChanged += OnHealthChanged;
            GameEvents.PlayerFocusChanged += OnSoulChanged;
        }

        void OnDisable()
        {
            GameEvents.PlayerHealthChanged -= OnHealthChanged;
            GameEvents.PlayerFocusChanged -= OnSoulChanged;
        }

        // ---------- Building ----------

        void Build()
        {
            if (art == null || art.frame == null)
            {
                Debug.LogWarning("PlayerHud: assign the HudArt asset.");
                enabled = false;
                return;
            }

            if (GetComponent<Canvas>() == null)
            {
                var canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 10;
                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
            }

            root = NewRect("HUD", transform);
            SetTopLeft(root);
            root.anchoredPosition = new Vector2(screenMargin.x, -screenMargin.y);
            root.sizeDelta = art.frame.rect.size * scale;

            // 1. The frame (its dark orb interior sits behind the soul).
            var frame = NewImage("Frame", root, art.frame);
            SetTopLeft(frame.rectTransform);
            frame.rectTransform.anchoredPosition = Vector2.zero;
            frame.rectTransform.sizeDelta = art.frame.rect.size * scale;

            // 2. The orb: a circle mask with the soul fill and the face inside it.
            orbSize = art.orbDiameter * scale;
            var orb = NewImage("Orb", root, art.orbCircle);
            orbRect = orb.rectTransform;
            PlaceInFrame(orbRect, art.orbCenter);
            orbRect.sizeDelta = Vector2.one * orbSize;
            orb.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            // The liquid: a wavy-top shape that rises with the soul...
            var fill = NewImage("SoulLevel", orbRect, art.soulFill);
            fill.preserveAspect = false;
            fillRect = fill.rectTransform;
            fillRect.anchorMin = fillRect.anchorMax = new Vector2(0.5f, 0f);   // bottom of the orb
            fillRect.pivot = new Vector2(0.5f, 1f);                            // moved by its wavy top edge
            fillRect.sizeDelta = new Vector2(orbSize * 1.1f, orbSize * 1.1f);

            // ...and cuts out the sparkly soul, which stays still inside the orb.
            if (art.soulTexture != null)
            {
                fill.gameObject.AddComponent<Mask>().showMaskGraphic = false;
                soulTexture = NewImage("Soul", fillRect, art.soulTexture);
                soulTexture.rectTransform.sizeDelta = Vector2.one * orbSize * 1.15f;   // its soft edge hides under the frame
            }

            if (art.orbFace != null)
            {
                face = NewImage("Face", orbRect, art.orbFace);
                face.rectTransform.sizeDelta = Vector2.one * orbSize;
                face.color = new Color(1f, 1f, 1f, 0f);
            }

            ApplySoul(0f);
        }

        // ---------- Events ----------

        void OnHealthChanged(int current, int max)
        {
            while (masks.Count < max) AddMask(masks.Count);
            for (int i = 0; i < masks.Count; i++) masks[i].gameObject.SetActive(i < max);

            for (int i = 0; i < max; i++)
            {
                bool full = i < current;
                bool wasFull = filledMasks < 0 ? full : i < filledMasks;
                SetMask(i, full);
                if (full != wasFull) punches[i] = 1f;   // pop the masks that just broke or refilled
            }
            filledMasks = current;
        }

        void OnSoulChanged(float fraction) => soulTarget = Mathf.Clamp01(fraction);

        // ---------- Per frame (unscaled, so it keeps moving during hit-stop) ----------

        void Update()
        {
            float dt = Time.unscaledDeltaTime;

            soulShown = Mathf.MoveTowards(soulShown, soulTarget, fillSpeed * dt);
            ApplySoul(soulShown);

            for (int i = 0; i < masks.Count; i++)
            {
                punches[i] = Mathf.MoveTowards(punches[i], 0f, dt * 4f);
                masks[i].rectTransform.localScale = Vector3.one * (1f + maskPunch * punches[i]);
            }
        }

        void ApplySoul(float fraction)
        {
            if (fillRect == null) return;
            fillRect.anchoredPosition = new Vector2(0f, fraction * orbSize);
            if (soulTexture != null) soulTexture.rectTransform.position = orbRect.position;   // keep it still
            if (face != null)
            {
                float target = showFaceWhenFull && fraction >= 0.999f ? 1f : 0f;
                var c = face.color;
                c.a = Mathf.MoveTowards(c.a, target, Time.unscaledDeltaTime * 4f);
                face.color = c;
            }
        }

        // ---------- Masks ----------

        void AddMask(int index)
        {
            var img = NewImage($"Mask {index + 1}", root, art.maskFull);
            PlaceInFrame(img.rectTransform, firstMaskCenter + new Vector2(index * maskSpacing, 0f));
            masks.Add(img);
            punches.Add(0f);
        }

        void SetMask(int index, bool full)
        {
            var img = masks[index];
            var sprite = full ? art.maskFull : (art.maskEmpty != null ? art.maskEmpty : art.maskFull);
            img.sprite = sprite;
            img.rectTransform.sizeDelta = sprite.rect.size * scale;
            img.color = full || art.maskEmpty != null ? Color.white : new Color(0.2f, 0.2f, 0.25f, 1f);
        }

        // ---------- Helpers ----------

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static Image NewImage(string name, Transform parent, Sprite sprite)
        {
            var img = NewRect(name, parent).gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            img.preserveAspect = true;
            return img;
        }

        static void SetTopLeft(RectTransform rt)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        }

        // Positions a centred element at a pixel position of the frame image.
        void PlaceInFrame(RectTransform rt, Vector2 framePixel)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(framePixel.x * scale, -framePixel.y * scale);
        }
    }
}
