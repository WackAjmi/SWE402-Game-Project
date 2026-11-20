using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Hollow.PlayerSide.GameFlow
{
    /// <summary>Small helpers that build Hollow Knight style menus in code (like the HUD does).</summary>
    public static class MenuKit
    {
        static Font builtinFont;
        public static Font DefaultFont
        {
            get
            {
                if (builtinFont == null) builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return builtinFont;
            }
        }

        public static readonly Color TextIdle = new Color(0.78f, 0.78f, 0.8f, 1f);
        public static readonly Color TextSelected = Color.white;

        public static Canvas MakeCanvas(Transform owner, string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(owner, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
            return r;
        }

        public static Image FullScreen(string name, Transform parent, Color color, Sprite sprite = null)
        {
            var img = Stretch(Rect(name, parent)).gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>An image of the given width, keeping the sprite's proportions.</summary>
        public static Image Picture(string name, Transform parent, Sprite sprite, Vector2 position, float width)
        {
            var r = Rect(name, parent);
            r.anchoredPosition = position;
            var img = r.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            if (sprite != null) r.sizeDelta = new Vector2(width, width * sprite.rect.height / sprite.rect.width);
            else img.enabled = false;
            return img;
        }

        public static Text Label(string name, Transform parent, Font font, string text, int size, Vector2 position,
                                 Vector2 box, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var r = Rect(name, parent);
            r.anchoredPosition = position;
            r.sizeDelta = box;
            var t = r.gameObject.AddComponent<Text>();
            t.font = font;
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = TextIdle;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>Links the items top to bottom (wrapping round) for keyboard and gamepad.</summary>
        public static void LinkVertically(IList<Selectable> items)
        {
            for (int i = 0; i < items.Count; i++)
            {
                var nav = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = items[(i - 1 + items.Count) % items.Count],
                    selectOnDown = items[(i + 1) % items.Count],
                };
                items[i].navigation = nav;
            }
        }

        public static void Select(Selectable s)
        {
            var es = EventSystem.current;
            if (es == null || s == null) return;
            es.SetSelectedGameObject(null);
            es.SetSelectedGameObject(s.gameObject);
        }

        /// <summary>Esc on keyboard, B or Start on a gamepad.</summary>
        public static bool BackPressed()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) return true;
            var pad = Gamepad.current;
            return pad != null && (pad.buttonEast.wasPressedThisFrame || pad.startButton.wasPressedThisFrame);
        }

        public static IEnumerator Fade(CanvasGroup group, float from, float to, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                group.alpha = Mathf.Lerp(from, to, t / seconds);
                yield return null;
            }
            group.alpha = to;
        }

        public static IEnumerator Fade(Graphic g, float from, float to, float seconds)
        {
            Color c = g.color;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                c.a = Mathf.Lerp(from, to, t / seconds);
                g.color = c;
                yield return null;
            }
            c.a = to;
            g.color = c;
        }
    }

    /// <summary>Builds menu rows (buttons and sliders) that share one look and one sound source.</summary>
    public class MenuBuilder
    {
        public readonly MenuArt art;
        public readonly AudioSource uiAudio;
        public const float RowWidth = 640f;
        public const float RowHeight = 64f;
        public int fontSize = 38;

        public MenuBuilder(MenuArt art, GameObject audioOwner)
        {
            this.art = art;
            uiAudio = audioOwner.AddComponent<AudioSource>();
            uiAudio.playOnAwake = false;
            uiAudio.spatialBlend = 0f;
            uiAudio.ignoreListenerPause = true;   // menus must still click while the game is paused
        }

        public void PlayConfirm()
        {
            if (art.confirmSound != null) uiAudio.PlayOneShot(art.confirmSound, art.uiVolume);
        }

        public Button Button(Transform parent, string label, float y, Action onClick)
        {
            var row = MenuKit.Rect(label, parent);
            row.anchoredPosition = new Vector2(0f, y);
            row.sizeDelta = new Vector2(RowWidth, RowHeight);

            var hit = row.gameObject.AddComponent<Image>();   // invisible, catches the mouse over the whole row
            hit.color = Color.clear;

            var text = MenuKit.Label("Text", row, art.Font, label, fontSize, Vector2.zero, new Vector2(RowWidth, RowHeight));
            var button = row.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => { PlayConfirm(); onClick(); });

            row.gameObject.AddComponent<MenuItem>().Setup(this, text, () => text.preferredWidth * 0.5f);
            return button;
        }

        public Slider Slider(Transform parent, string label, float y, float value, Action<float> onChanged)
        {
            var row = MenuKit.Rect(label, parent);
            row.anchoredPosition = new Vector2(0f, y);
            row.sizeDelta = new Vector2(RowWidth, RowHeight);

            var text = MenuKit.Label("Text", row, art.Font, label, fontSize, new Vector2(-150f, 0f),
                                     new Vector2(300f, RowHeight), TextAnchor.MiddleRight);

            // The track is the selectable part, so a click on the label can't jump the value to 0.
            var track = MenuKit.Rect("Track", row);
            track.anchoredPosition = new Vector2(150f, 0f);
            track.sizeDelta = new Vector2(240f, 44f);
            var hit = track.gameObject.AddComponent<Image>();
            hit.color = Color.clear;

            var line = MenuKit.Rect("Line", track);
            line.anchorMin = new Vector2(0f, 0.47f);
            line.anchorMax = new Vector2(1f, 0.53f);
            line.offsetMin = line.offsetMax = Vector2.zero;
            var lineImage = line.gameObject.AddComponent<Image>();
            lineImage.color = new Color(1f, 1f, 1f, 0.25f);
            lineImage.raycastTarget = false;

            var fillArea = MenuKit.Stretch(MenuKit.Rect("Fill Area", track));
            var fill = MenuKit.Rect("Fill", fillArea);
            fill.anchorMin = new Vector2(0f, 0.45f);
            fill.anchorMax = new Vector2(0f, 0.55f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            var fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.color = new Color(1f, 1f, 1f, 0.8f);
            fillImage.raycastTarget = false;

            var handleArea = MenuKit.Stretch(MenuKit.Rect("Handle Area", track));
            var handle = MenuKit.Rect("Handle", handleArea);
            handle.anchorMin = new Vector2(0f, 0f);
            handle.anchorMax = new Vector2(0f, 1f);
            handle.sizeDelta = new Vector2(21f, 0f);
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.sprite = art.sliderHandle;
            handleImage.preserveAspect = true;
            handleImage.raycastTarget = false;

            var slider = track.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = hit;
            slider.transition = Selectable.Transition.None;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = value;
            slider.onValueChanged.AddListener(v => onChanged(v));

            // Pointers go around the whole row (label and track): it spans x -300..270 in row space,
            // which is centre -15 in row space, -165 in the track's space.
            track.gameObject.AddComponent<MenuItem>().Setup(this, text, () => 285f, new Vector2(-165f, 0f));
            return slider;
        }
    }

    /// <summary>
    /// One menu row: highlights its text and grows the two HK pointers beside it when selected.
    /// Hovering with the mouse selects it too.
    /// </summary>
    public class MenuItem : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler
    {
        const float FramesPerSecond = 40f;
        const float PointerHeight = 58f;
        const float Gap = 16f;

        MenuBuilder builder;
        Text text;
        Func<float> halfWidth;
        Vector2 centre;
        Image left, right;
        float age = -1f;

        public void Setup(MenuBuilder menu, Text label, Func<float> halfWidthOfContent, Vector2? contentCentre = null)
        {
            builder = menu;
            text = label;
            halfWidth = halfWidthOfContent;
            centre = contentCentre ?? Vector2.zero;
            left = MakePointer("Pointer Left", false);
            right = MakePointer("Pointer Right", true);
        }

        Image MakePointer(string name, bool mirrored)
        {
            var frames = builder.art.pointerFrames;
            Sprite last = frames != null && frames.Length > 0 ? frames[frames.Length - 1] : null;
            var r = MenuKit.Rect(name, transform);
            float width = last != null ? PointerHeight * last.rect.width / last.rect.height : PointerHeight;
            r.sizeDelta = new Vector2(width, PointerHeight);
            if (mirrored) r.localScale = new Vector3(-1f, 1f, 1f);
            var img = r.gameObject.AddComponent<Image>();
            img.sprite = last;
            img.raycastTarget = false;
            img.enabled = false;
            return img;
        }

        public void OnSelect(BaseEventData eventData)
        {
            text.color = MenuKit.TextSelected;
            float half = halfWidth() + Gap;
            float w = left.rectTransform.sizeDelta.x;
            left.rectTransform.anchoredPosition = centre + new Vector2(-half - w * 0.5f, 0f);
            right.rectTransform.anchoredPosition = centre + new Vector2(half + w * 0.5f, 0f);
            left.enabled = right.enabled = left.sprite != null;
            age = 0f;
            if (builder.art.selectSound != null)
                builder.uiAudio.PlayOneShot(builder.art.selectSound, builder.art.uiVolume * 0.6f);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            text.color = MenuKit.TextIdle;
            left.enabled = right.enabled = false;
            age = -1f;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != gameObject)
                EventSystem.current.SetSelectedGameObject(gameObject);
        }

        void OnDisable()
        {
            if (text != null) text.color = MenuKit.TextIdle;
            if (left != null) left.enabled = right.enabled = false;
            age = -1f;
        }

        void Update()
        {
            var frames = builder != null ? builder.art.pointerFrames : null;
            if (age < 0f || frames == null || frames.Length == 0) return;
            age += Time.unscaledDeltaTime;
            int i = Mathf.Min(frames.Length - 1, (int)(age * FramesPerSecond));
            left.sprite = right.sprite = frames[i];
            if (i == frames.Length - 1) age = -1f;
        }
    }
}
