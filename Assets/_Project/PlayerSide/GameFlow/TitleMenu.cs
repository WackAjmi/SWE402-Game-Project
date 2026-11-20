using System.Collections;
using System.Collections.Generic;
using Hollow.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Hollow.PlayerSide.GameFlow
{
    /// <summary>
    /// The title screen: background art, the game's name between two fleurs, and
    /// Start Game / Settings / Quit Game. Builds itself at runtime from a MenuArt asset.
    /// </summary>
    public class TitleMenu : MonoBehaviour
    {
        [SerializeField] MenuArt art;
        [SerializeField] string arenaScene = SceneFlow.ArenaScene;

        MenuBuilder ui;
        RectTransform mainPanel, settingsPanel;
        List<Selectable> mainRows, settingsRows;
        Image black;
        AudioSource music;
        float musicFade;
        bool leaving, inSettings;

        void Start()
        {
            if (art == null) { Debug.LogWarning("TitleMenu: assign the MenuArt asset."); enabled = false; return; }
            SceneFlow.ResetRuntimeState();
            MenuKit.EnsureEventSystem();
            Build();
            ShowMain();
            StartMusic();
            StartCoroutine(MenuKit.Fade(black, 1f, 0f, 1.2f));
        }

        void OnEnable() => GameSettings.Changed += ApplyMusicVolume;
        void OnDisable() => GameSettings.Changed -= ApplyMusicVolume;

        void Build()
        {
            var canvas = MenuKit.MakeCanvas(transform, "Title Canvas", 0).transform;
            ui = new MenuBuilder(art, gameObject);

            // Background fills the screen without stretching.
            var bg = MenuKit.FullScreen("Background", canvas, Color.white, art.titleBackground);
            if (art.titleBackground != null)
            {
                var fit = bg.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = art.titleBackground.rect.width / art.titleBackground.rect.height;
            }
            else bg.color = Color.black;
            MenuKit.FullScreen("Dim", canvas, new Color(0f, 0f, 0f, 0.35f));

            // The game's name between the two title fleurs.
            MenuKit.Picture("Fleur Top", canvas, art.titleFleurTop, new Vector2(0f, 345f), 760f);
            var title = MenuKit.Label("Title", canvas, art.Font, art.gameTitle.ToUpperInvariant(), 88,
                                      new Vector2(0f, 262f), new Vector2(1600f, 120f));
            title.color = Color.white;
            var glow = title.gameObject.AddComponent<Outline>();
            glow.effectColor = new Color(1f, 1f, 1f, 0.12f);
            glow.effectDistance = new Vector2(2f, -2f);
            MenuKit.Picture("Fleur Bottom", canvas, art.titleFleurBottom, new Vector2(0f, 150f), 760f);

            mainPanel = MenuKit.Rect("Main", canvas);
            mainRows = new List<Selectable>
            {
                ui.Button(mainPanel, "Start Game", -70f, StartGame),
                ui.Button(mainPanel, "Settings", -145f, ShowSettings),
                ui.Button(mainPanel, "Quit Game", -220f, SceneFlow.QuitGame),
            };
            MenuKit.LinkVertically(mainRows);
            MenuKit.Picture("Fleur", mainPanel, art.menuFleurBottom, new Vector2(0f, -300f), 300f);

            settingsPanel = MenuKit.Rect("Settings", canvas);
            settingsRows = SettingsRows.Build(ui, settingsPanel, -70f, 75f, ShowMain);
            MenuKit.Picture("Fleur", settingsPanel, art.menuFleurBottom, new Vector2(0f, -300f), 300f);

            black = MenuKit.FullScreen("Fade", canvas, Color.black);
        }

        void ShowMain()
        {
            inSettings = false;
            settingsPanel.gameObject.SetActive(false);
            mainPanel.gameObject.SetActive(true);
            MenuKit.Select(mainRows[inSettingsReturnIndex]);
            inSettingsReturnIndex = 0;
        }

        int inSettingsReturnIndex;

        void ShowSettings()
        {
            inSettings = true;
            inSettingsReturnIndex = 1;   // come back to the Settings row
            mainPanel.gameObject.SetActive(false);
            settingsPanel.gameObject.SetActive(true);
            MenuKit.Select(settingsRows[0]);
        }

        void Update()
        {
            if (leaving) return;
            if (inSettings && MenuKit.BackPressed()) { ui.PlayConfirm(); GameSettings.Save(); ShowMain(); }
            if (music != null && musicFade < 1f)
            {
                musicFade = Mathf.MoveTowards(musicFade, 1f, Time.unscaledDeltaTime / 2f);
                ApplyMusicVolume();
            }
        }

        void StartGame()
        {
            if (leaving) return;
            leaving = true;
            GameSession.Attempts = 0;
            StartCoroutine(FadeMusicOut(0.8f));
            StartCoroutine(SceneFlow.FadeAndLoad(black, arenaScene, 0.8f));
        }

        // ---------- Music ----------

        void StartMusic()
        {
            if (art.titleMusic == null) return;
            music = gameObject.AddComponent<AudioSource>();
            music.clip = art.titleMusic;
            music.loop = true;
            music.spatialBlend = 0f;
            music.ignoreListenerVolume = true;   // follows the Music slider, not the Sound slider
            music.ignoreListenerPause = true;
            musicFade = 0f;
            ApplyMusicVolume();
            music.Play();
        }

        void ApplyMusicVolume()
        {
            if (music != null) music.volume = art.titleMusicVolume * GameSettings.MusicVolume * musicFade;
        }

        IEnumerator FadeMusicOut(float seconds)
        {
            float start = musicFade;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                musicFade = Mathf.Lerp(start, 0f, t / seconds);
                ApplyMusicVolume();
                yield return null;
            }
            musicFade = 0f;
            ApplyMusicVolume();
        }
    }
}
