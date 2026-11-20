using System.Collections.Generic;
using Hollow.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Hollow.PlayerSide.GameFlow
{
    /// <summary>
    /// Esc (or Start on a gamepad) pauses the fight: Continue / Settings / Restart / Quit to Title.
    /// While paused, time stops, sound effects pause (music keeps playing, quieter) and the
    /// Knight's controls are switched off.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        [SerializeField] MenuArt art;
        [SerializeField] string titleScene = SceneFlow.TitleScene;

        public static bool IsPaused { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => IsPaused = false;

        MenuBuilder ui;
        GameObject root;
        RectTransform mainPanel, settingsPanel;
        List<Selectable> mainRows, settingsRows;
        Image black;
        InputActionMap playerMap;
        bool inSettings, playerDead, leaving;

        void Start()
        {
            if (art == null) { Debug.LogWarning("PauseMenu: assign the MenuArt asset."); enabled = false; return; }
            playerMap = InputSystem.actions != null ? InputSystem.actions.FindActionMap("Player") : null;
            MenuKit.EnsureEventSystem();
            Build();
            root.SetActive(false);
        }

        void OnEnable() => GameEvents.PlayerDied += OnPlayerDied;

        void OnDisable()
        {
            GameEvents.PlayerDied -= OnPlayerDied;
            if (IsPaused) { IsPaused = false; SceneFlow.ResetRuntimeState(); }
        }

        void OnPlayerDied()
        {
            playerDead = true;
            if (IsPaused) Resume();
        }

        void Build()
        {
            var canvas = MenuKit.MakeCanvas(transform, "Pause Canvas", 50).transform;
            ui = new MenuBuilder(art, gameObject);
            root = MenuKit.Stretch(MenuKit.Rect("Pause", canvas)).gameObject;

            var dim = MenuKit.FullScreen("Dim", root.transform, new Color(0f, 0f, 0f, 0.7f));
            dim.raycastTarget = true;   // blocks clicks on anything behind the menu
            MenuKit.Picture("Fleur Top", root.transform, art.menuFleurTop, new Vector2(0f, 230f), 420f);

            mainPanel = MenuKit.Rect("Main", root.transform);
            mainRows = new List<Selectable>
            {
                ui.Button(mainPanel, "Continue", 90f, Resume),
                ui.Button(mainPanel, "Settings", 15f, ShowSettings),
                ui.Button(mainPanel, "Restart", -60f, Restart),
                ui.Button(mainPanel, "Quit to Title", -135f, QuitToTitle),
            };
            MenuKit.LinkVertically(mainRows);

            settingsPanel = MenuKit.Rect("Settings", root.transform);
            settingsRows = SettingsRows.Build(ui, settingsPanel, 90f, 75f, () => ShowMain(1));

            MenuKit.Picture("Fleur Bottom", root.transform, art.menuFleurBottom, new Vector2(0f, -235f), 300f);

            var blackImage = MenuKit.FullScreen("Fade", canvas, Color.black);
            blackImage.color = new Color(0f, 0f, 0f, 0f);
            black = blackImage;
        }

        void Update()
        {
            if (leaving || playerDead || DeathScreen.IsShowing || VictoryScreen.IsShowing) return;

            if (!IsPaused)
            {
                if (PausePressed()) Pause();
            }
            else if (MenuKit.BackPressed())
            {
                ui.PlayConfirm();
                if (inSettings) { GameSettings.Save(); ShowMain(1); }
                else Resume();
            }
        }

        static bool PausePressed()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) return true;
            var pad = Gamepad.current;
            return pad != null && pad.startButton.wasPressedThisFrame;
        }

        public void Pause()
        {
            IsPaused = true;
            Time.timeScale = 0f;
            if (Services.Feedback is FeedbackService feedback) feedback.SuspendTimeControl = true;
            AudioListener.pause = true;    // sound effects stop; music sources ignore this
            playerMap?.Disable();
            root.SetActive(true);
            ShowMain(0);
            GameEvents.GameStateChanged?.Invoke(GameState.Paused);
        }

        public void Resume()
        {
            GameSettings.Save();
            IsPaused = false;
            root.SetActive(false);
            SceneFlow.ResetRuntimeState();
            GameEvents.GameStateChanged?.Invoke(GameState.Fighting);
        }

        void ShowMain(int selectIndex)
        {
            inSettings = false;
            settingsPanel.gameObject.SetActive(false);
            mainPanel.gameObject.SetActive(true);
            MenuKit.Select(mainRows[selectIndex]);
        }

        void ShowSettings()
        {
            inSettings = true;
            mainPanel.gameObject.SetActive(false);
            settingsPanel.gameObject.SetActive(true);
            MenuKit.Select(settingsRows[0]);
        }

        void Restart()
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
