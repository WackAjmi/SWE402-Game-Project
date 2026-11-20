using System.Collections;
using System.Collections.Generic;
using Hollow.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Hollow.PlayerSide.GameFlow
{
    /// <summary>
    /// Shown a moment after the Knight dies: Retry (restarts the arena from the tunnel) or Quit to Title.
    /// The Knight's own sandbox respawn must be off in this scene (the menu tool does that).
    /// </summary>
    public class DeathScreen : MonoBehaviour
    {
        [SerializeField] MenuArt art;
        [SerializeField] string titleScene = SceneFlow.TitleScene;
        [Tooltip("Wait this long after death (real seconds) so the death animation and slow motion play out.")]
        [SerializeField] float delay = 2.2f;
        [SerializeField] string message = "Defeated";

        public static bool IsShowing { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => IsShowing = false;

        MenuBuilder ui;
        CanvasGroup group;
        List<Selectable> rows;
        Image black;
        bool leaving;

        void Start()
        {
            if (art == null) { Debug.LogWarning("DeathScreen: assign the MenuArt asset."); enabled = false; return; }
            MenuKit.EnsureEventSystem();
            Build();
            group.gameObject.SetActive(false);
        }

        void OnEnable() => GameEvents.PlayerDied += OnPlayerDied;
        void OnDisable() => GameEvents.PlayerDied -= OnPlayerDied;
        void OnDestroy() => IsShowing = false;

        void Build()
        {
            var canvas = MenuKit.MakeCanvas(transform, "Death Canvas", 60).transform;
            ui = new MenuBuilder(art, gameObject);

            var root = MenuKit.Stretch(MenuKit.Rect("Death", canvas));
            group = root.gameObject.AddComponent<CanvasGroup>();

            var dim = MenuKit.FullScreen("Dim", root, new Color(0f, 0f, 0f, 0.85f));
            dim.raycastTarget = true;
            var bg = MenuKit.Picture("Background", root, art.deathBackground, new Vector2(0f, 20f), 1000f);
            bg.color = new Color(1f, 1f, 1f, 0.6f);

            var title = MenuKit.Label("Message", root, art.Font, message.ToUpperInvariant(), 72,
                                      new Vector2(0f, 110f), new Vector2(1200f, 100f));
            title.color = Color.white;
            MenuKit.Picture("Divider", root, art.divider, new Vector2(0f, 40f), 900f);

            rows = new List<Selectable>
            {
                ui.Button(root, "Retry", -50f, Retry),
                ui.Button(root, "Quit to Title", -125f, QuitToTitle),
            };
            MenuKit.LinkVertically(rows);

            var blackImage = MenuKit.FullScreen("Fade", canvas, Color.black);
            blackImage.color = new Color(0f, 0f, 0f, 0f);
            black = blackImage;
        }

        void OnPlayerDied()
        {
            if (!IsShowing && group != null) StartCoroutine(Show());
        }

        IEnumerator Show()
        {
            IsShowing = true;
            yield return new WaitForSecondsRealtime(delay);
            group.alpha = 0f;
            group.gameObject.SetActive(true);
            MenuKit.Select(rows[0]);
            yield return MenuKit.Fade(group, 0f, 1f, 0.8f);
        }

        void Retry()
        {
            if (leaving) return;
            leaving = true;
            GameSession.Attempts++;
            StartCoroutine(SceneFlow.FadeAndLoad(black, SceneManager.GetActiveScene().name, 0.5f));
        }

        void QuitToTitle()
        {
            if (leaving) return;
            leaving = true;
            StartCoroutine(SceneFlow.FadeAndLoad(black, titleScene, 0.6f));
        }
    }
}
