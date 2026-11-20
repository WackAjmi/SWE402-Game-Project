using System;
using UnityEngine;

namespace Hollow.Core
{
    /// <summary>
    /// Player settings that survive between sessions (saved in PlayerPrefs).
    /// Sound volume drives AudioListener.volume, so every sound effect follows it.
    /// Music players set ignoreListenerVolume and multiply by MusicVolume instead,
    /// so the two sliders stay independent.
    /// </summary>
    public static class GameSettings
    {
        const string MusicKey = "hw_music_volume";
        const string SfxKey = "hw_sfx_volume";

        public static float MusicVolume { get; private set; } = 0.8f;
        public static float SfxVolume { get; private set; } = 0.8f;

        /// <summary>Raised whenever a volume changes.</summary>
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetEvents() => Changed = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Load()
        {
            MusicVolume = PlayerPrefs.GetFloat(MusicKey, 0.8f);
            SfxVolume = PlayerPrefs.GetFloat(SfxKey, 0.8f);
            AudioListener.volume = SfxVolume;
        }

        public static void SetMusicVolume(float value)
        {
            MusicVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(MusicKey, MusicVolume);
            Changed?.Invoke();
        }

        public static void SetSfxVolume(float value)
        {
            SfxVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(SfxKey, SfxVolume);
            AudioListener.volume = SfxVolume;
            Changed?.Invoke();
        }

        public static void Save() => PlayerPrefs.Save();
    }
}
