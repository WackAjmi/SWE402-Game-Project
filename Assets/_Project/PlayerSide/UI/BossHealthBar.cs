using System.Collections;
using Hollow.Core;
using Hollow.PlayerSide.GameFlow;
using UnityEngine;
using UnityEngine.UI;

namespace Hollow.PlayerSide.UI
{
    /// <summary>
    /// The boss's name and health bar at the top of the screen. Appears when the boss's intro
    /// ends (or on the first health update), drains with a short trailing "damage" bar, and fades
    /// out when the boss dies. Driven only by GameEvents.
    /// </summary>
    public class BossHealthBar : MonoBehaviour
    {
        [SerializeField] MenuArt art;
        [SerializeField] string bossName = "The Hallowed Summoner";
        [SerializeField] float barWidth = 900f;
        [SerializeField] float barHeight = 12f;
        [SerializeField] float topMargin = 40f;
        [SerializeField] Color fillColor = new Color(0.93f, 0.92f, 0.88f);
        [SerializeField] Color trailColor = new Color(0.75f, 0.25f, 0.25f);

        CanvasGroup group;
        RectTransform fill, trail;
        float target = 1f, trailValue = 1f, trailDelay;
        bool shown;

        void Awake() => Build();

        void OnEnable()
        {
            GameEvents.BossIntroFinished += Show;
            GameEvents.BossHealthChanged += OnHealthChanged;
            GameEvents.BossDied += OnBossDied;
        }

        void OnDisable()
        {
            GameEvents.BossIntroFinished -= Show;
            GameEvents.BossHealthChanged -= OnHealthChanged;
            GameEvents.BossDied -= OnBossDied;
        }

        void Build()
        {
            var canvas = MenuKit.MakeCanvas(transform, "Boss Bar Canvas", 20).transform;
            var root = MenuKit.Rect("Boss Bar", canvas);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 1f);
            root.pivot = new Vector2(0.5f, 1f);
            root.anchoredPosition = new Vector2(0f, -topMargin);
            root.sizeDelta = new Vector2(barWidth, 110f);
            group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            Font font = art != null ? art.Font : MenuKit.DefaultFont;
            var nameText = MenuKit.Label("Name", root, font, bossName.ToUpperInvariant(), 34,
                                     new Vector2(0f, -22f), new Vector2(barWidth, 44f));
            nameText.color = Color.white;
            var shadow = nameText.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            shadow.effectDistance = new Vector2(2f, -2f);

            // Bar: dark back, red trailing bar, pale fill on top. Both bars shrink from the right.
            var back = MenuKit.Rect("Back", root);
            back.anchoredPosition = new Vector2(0f, -62f);
            back.sizeDelta = new Vector2(barWidth + 6f, barHeight + 6f);
            var backImage = back.gameObject.AddComponent<Image>();
            backImage.color = new Color(0f, 0f, 0f, 0.7f);
            backImage.raycastTarget = false;

            trail = Bar("Trail", back, trailColor);
            fill = Bar("Fill", back, fillColor);

            if (art != null && art.divider != null)
            {
                var d = MenuKit.Picture("Ornament", root, art.divider, new Vector2(0f, -86f), barWidth * 0.8f);
                d.color = new Color(1f, 1f, 1f, 0.7f);
            }
        }

        RectTransform Bar(string name, RectTransform parent, Color color)
        {
            var r = MenuKit.Rect(name, parent);
            r.anchorMin = new Vector2(0f, 0f);
            r.anchorMax = new Vector2(1f, 1f);
            r.offsetMin = new Vector2(3f, 3f);
            r.offsetMax = new Vector2(-3f, -3f);
            r.pivot = new Vector2(0f, 0.5f);
            var img = r.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return r;
        }

        void Show()
        {
            if (shown) return;
            shown = true;
            StartCoroutine(MenuKit.Fade(group, group.alpha, 1f, 0.6f));
        }

        void OnHealthChanged(int current, int max)
        {
            Show();
            float v = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;
            if (v < target) trailDelay = 0.4f;   // the red part waits a moment, then catches up
            target = v;
        }

        void OnBossDied()
        {
            target = 0f;
            StartCoroutine(HideLater());
        }

        IEnumerator HideLater()
        {
            yield return new WaitForSecondsRealtime(1.2f);
            yield return MenuKit.Fade(group, group.alpha, 0f, 1f);
        }

        void Update()
        {
            if (fill == null) return;
            SetWidth(fill, target);

            if (trailDelay > 0f) trailDelay -= Time.unscaledDeltaTime;
            else trailValue = Mathf.MoveTowards(trailValue, target, Time.unscaledDeltaTime * 0.6f);
            if (trailValue < target) trailValue = target;
            SetWidth(trail, trailValue);
        }

        static void SetWidth(RectTransform r, float value)
        {
            var s = r.localScale;
            s.x = value;
            r.localScale = s;
        }
    }
}
