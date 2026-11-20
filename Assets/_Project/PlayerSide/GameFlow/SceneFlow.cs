using System;
using System.Collections;
using System.Collections.Generic;
using Hollow.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Hollow.PlayerSide.GameFlow
{
    /// <summary>Scene changes and the bits of global state a pause or death can leave behind.</summary>
    public static class SceneFlow
    {
        public const string TitleScene = "Title";
        public const string ArenaScene = "Arena";

        /// <summary>Unpauses time and audio and gives the Knight his controls back.</summary>
        public static void ResetRuntimeState()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            if (Services.Feedback is FeedbackService feedback) feedback.SuspendTimeControl = false;
            var playerMap = InputSystem.actions != null ? InputSystem.actions.FindActionMap("Player") : null;
            playerMap?.Enable();
        }

        /// <summary>Fades the given black image in, then loads the scene.</summary>
        public static IEnumerator FadeAndLoad(Image black, string scene, float seconds = 0.6f)
        {
            black.raycastTarget = true;   // no more clicks while leaving
            yield return MenuKit.Fade(black, black.color.a, 1f, seconds);
            GameSettings.Save();
            ResetRuntimeState();
            SceneManager.LoadScene(scene);
        }

        public static void QuitGame()
        {
            GameSettings.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }

    /// <summary>The Music / Sound / Back rows, shared by the title screen and the pause menu.</summary>
    public static class SettingsRows
    {
        public static List<Selectable> Build(MenuBuilder ui, Transform panel, float topY, float spacing, Action onBack)
        {
            var music = ui.Slider(panel, "Music", topY, GameSettings.MusicVolume, GameSettings.SetMusicVolume);
            var sound = ui.Slider(panel, "Sound", topY - spacing, GameSettings.SfxVolume, v =>
            {
                GameSettings.SetSfxVolume(v);
                if (ui.art.selectSound != null) ui.uiAudio.PlayOneShot(ui.art.selectSound, ui.art.uiVolume);   // preview
            });
            var back = ui.Button(panel, "Back", topY - spacing * 2f, () => { GameSettings.Save(); onBack(); });
            var rows = new List<Selectable> { music, sound, back };
            MenuKit.LinkVertically(rows);
            return rows;
        }
    }
}
