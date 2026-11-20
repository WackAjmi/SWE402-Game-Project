using System.Collections;
using System.Collections.Generic;
using Hollow.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Hollow.PlayerSide.GameFlow
{
    /// <summary>
    /// Shown a few seconds after the boss dies: VICTORY, the fight time and the number of attempts,
    /// then Fight Again (restarts the arena) or Quit to Title.
    /// </summary>
    public class VictoryScreen : MonoBehaviour
    {
        [SerializeField] MenuArt art;
        [SerializeField] string titleScene = SceneFlow.TitleScene;
        [Tooltip("Real seconds after the boss dies, so the gates open and the victory sting starts first.")]
        [SerializeField] float delay = 3.5f;
        [SerializeField] string message = "Victory";

        public static bool IsShowing { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => IsShowing = false;

        MenuBuilder ui;
        CanvasGroup group;
        List<Selectable> rows;
        Text timeText, attemptsText;
        Image black;
        float fightStart = -1f;
        bool leaving;

        void Start()
        {
            if (art == null) { Debug.LogWarning("VictoryScreen: assign the MenuArt asset."); enabled = false; return; }
            MenuKit.EnsureEventSystem();
            Build();
            group.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            GameEvents.EncounterStarted += OnEncounterStarted;
            GameEvents.BossDied += OnBossDied;
        }

        void OnDisable()
        {
            GameEvents.EncounterStarted -= OnEncounterStarted;
            GameEvents.BossDied -= OnBossDied;
        }

        void OnDestroy() => IsShowing = false;

        void Build()
        {
            var canvas = MenuKit.MakeCanvas(transform, "Victory Canvas", 60).transform;
            ui = new MenuBuilder(art, gameObject);

            var root = MenuKit.Stretch(MenuKit.Rect("Victory", canvas));
            group = root.gameObject.AddComponent<CanvasGroup>();

            var dim = MenuKit.FullScreen("Dim", root, new Color(0f, 0f, 0f, 0.75f));
            dim.raycastTarget = true;

            MenuKit.Picture("Fleur Top", root, art.menuFleurTop, new Vector2(0f, 250f), 420f);
            var title = MenuKit.Label("Message", root, art.Font, message.ToUpperInvariant(), 80,
                                      new Vector2(0f, 150f), new Vector2(1200f, 110f));
            title.color = Color.white;
            MenuKit.Picture("Divider", root, art.divider, new Vector2(0f, 80f), 900f);

            timeText = MenuKit.Label("Time", root, art.Font, "", 32, new Vector2(0f, 30f), new Vector2(800f, 50f));
            attemptsText = MenuKit.Label("Attempts", root, art.Font, "", 32, new Vector2(0f, -15f), new Vector2(800f, 50f));

            rows = new List<Selectable>
            {
                ui.Button(root, "Fight Again", -95f, FightAgain),
                ui.Button(root, "Quit to Title", -170f, QuitToTitle),
            };
            MenuKit.LinkVertically(rows);
            MenuKit.Picture("Fleur Bottom", root, art.menuFleurBottom, new Vector2(0f, -255f), 300f);

            var blackImage = MenuKit.FullScreen("Fade", canvas, Color.black);
            blackImage.color = new Color(0f, 0f, 0f, 0f);
            black = blackImage;
        }

        void OnEncounterStarted() => fightStart = Time.time;

        void OnBossDied()
        {
            if (!IsShowing && !DeathScreen.IsShowing && group != null) StartCoroutine(Show());
        }

        IEnumerator Show()
        {
            IsShowing = true;
            float seconds = fightStart >= 0f ? Time.time - fightStart : 0f;
            yield return new WaitForSecondsRealtime(delay);

            // The fight is over: no more pausing or moving behind the screen.
            var playerMap = InputSystem.actions != null ? InputSystem.actions.FindActionMap("Player") : null;
            playerMap?.Disable();
            GameEvents.GameStateChanged?.Invoke(GameState.Victory);

            int m = Mathf.FloorToInt(seconds / 60f);
            int s = Mathf.FloorToInt(seconds % 60f);
            timeText.text = $"Time   {m}:{s:00}";
            int attempts = GameSession.Attempts + 1;
            attemptsText.text = attempts == 1 ? "Won on the first attempt" : $"Attempts   {attempts}";

            group.alpha = 0f;
            group.gameObject.SetActive(true);
            MenuKit.Select(rows[0]);
            yield return MenuKit.Fade(group, 0f, 1f, 0.8f);
        }

        void FightAgain()
        {
            if (leaving) return;
            leaving = true;
            GameSession.Attempts = 0;   // a fresh fight
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
