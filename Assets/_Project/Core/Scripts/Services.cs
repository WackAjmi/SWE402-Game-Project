using UnityEngine;

namespace Hollow.Core
{
    public interface IFeedbackService            // implemented by Student A
    {
        void HitStop(float seconds);
        void Shake(Vector2 direction, float strength);
        void SlowMotion(float timeScale, float seconds);
    }

    public interface IAudioService               // implemented by Student B
    {
        void PlaySfx(AudioClip clip, Vector3 position, float volume = 1f);
        void SetVolume(string mixerGroup, float linear01); // Music, SFX, UI, Ambience
    }

    public static class Services
    {
        public static Transform Player;          // set by PlayerController or DummyPlayer
        public static IFeedbackService Feedback = new NullFeedback();
        public static IAudioService Audio = new NullAudio();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetAll()
        {
            Player = null;
            Feedback = new NullFeedback();
            Audio = new NullAudio();
        }

        class NullFeedback : IFeedbackService
        {
            public void HitStop(float seconds) { }
            public void Shake(Vector2 direction, float strength) { }
            public void SlowMotion(float timeScale, float seconds) { }
        }

        class NullAudio : IAudioService
        {
            public void PlaySfx(AudioClip clip, Vector3 position, float volume = 1f)
            {
                if (clip) AudioSource.PlayClipAtPoint(clip, position, volume);
            }
            public void SetVolume(string mixerGroup, float linear01) { }
        }
    }

    // Survives scene reloads: fast retry and the results card.
    public static class GameSession
    {
        public static int Attempts;
        public static bool SkipIntro => Attempts > 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => Attempts = 0;
    }
}