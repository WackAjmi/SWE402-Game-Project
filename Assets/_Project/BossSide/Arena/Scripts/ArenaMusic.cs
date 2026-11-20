using System.Collections;
using Hollow.Core;
using UnityEngine;

namespace Hollow.BossSide.Arena
{
    /// <summary>
    /// Music for the Deepnest arena.
    ///   Tunnel:          area theme + ambience fade in.
    ///   Gates slam:      area theme fades out, boss music starts after a short pause.
    ///   Boss dies:       boss music fades, victory sting plays, then the area theme returns.
    ///   Knight dies:     the music fades out under the death screen.
    ///   Paused:          everything plays at half volume.
    /// Fades use unscaled time, so hit-stop and slow motion don't stretch them.
    /// Volumes follow the Music slider (GameSettings.MusicVolume), not the Sound slider.
    /// </summary>
    public class ArenaMusic : MonoBehaviour
    {
        [Header("Clips")]
        [SerializeField] AudioClip areaTheme;
        [SerializeField] AudioClip ambience;
        [SerializeField] AudioClip bossMusic;
        [SerializeField] AudioClip victorySting;

        [Header("Volumes")]
        [SerializeField, Range(0f, 1f)] float areaVolume = 0.5f;
        [SerializeField, Range(0f, 1f)] float ambienceVolume = 0.35f;
        [SerializeField, Range(0f, 1f)] float bossVolume = 0.65f;
        [SerializeField, Range(0f, 1f)] float stingVolume = 0.8f;

        [Header("Timing (seconds)")]
        [SerializeField] float startFade = 2f;
        [SerializeField] float areaFadeOut = 1.2f;
        [Tooltip("Silence between the gate slam and the boss music.")]
        [SerializeField] float bossMusicDelay = 0.6f;
        [SerializeField] float bossFadeIn = 0.3f;
        [SerializeField] float bossFadeOut = 1f;
        [SerializeField] float areaReturnFade = 3f;
        [SerializeField] float deathFade = 2f;
        [Tooltip("Music volume while the pause menu is open.")]
        [SerializeField, Range(0f, 1f)] float pausedVolume = 0.5f;

        class Channel
        {
            public AudioSource source;
            public float level;     // 0..1 before the Music setting and pause are applied
            public float target;
            public float speed;
        }

        Channel area, amb, boss;
        AudioSource sting;
        Coroutine sequence;
        float pauseScale = 1f;

        void Awake()
        {
            area = MakeChannel(areaTheme);
            amb = MakeChannel(ambience);
            boss = MakeChannel(bossMusic);
            sting = gameObject.AddComponent<AudioSource>();
            sting.playOnAwake = false;
            sting.spatialBlend = 0f;
            sting.ignoreListenerVolume = true;
            sting.ignoreListenerPause = true;
        }

        Channel MakeChannel(AudioClip clip)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = true;
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            s.volume = 0f;
            s.ignoreListenerVolume = true;   // music follows the Music slider only
            s.ignoreListenerPause = true;    // keeps playing (quieter) in the pause menu
            return new Channel { source = s };
        }

        void OnEnable()
        {
            GameEvents.EncounterStarted += OnEncounterStarted;
            GameEvents.BossDied += OnBossDied;
            GameEvents.PlayerDied += OnPlayerDied;
            GameEvents.GameStateChanged += OnGameStateChanged;
        }

        void OnDisable()
        {
            GameEvents.EncounterStarted -= OnEncounterStarted;
            GameEvents.BossDied -= OnBossDied;
            GameEvents.PlayerDied -= OnPlayerDied;
            GameEvents.GameStateChanged -= OnGameStateChanged;
        }

        void Start()
        {
            FadeTo(area, areaVolume, startFade);
            FadeTo(amb, ambienceVolume, startFade);
        }

        void OnEncounterStarted() => Run(EncounterSequence());
        void OnBossDied() => Run(VictorySequence());

        void OnPlayerDied()
        {
            if (sequence != null) StopCoroutine(sequence);
            FadeTo(area, 0f, deathFade);
            FadeTo(boss, 0f, deathFade);
        }

        void OnGameStateChanged(GameState state) => pauseScale = state == GameState.Paused ? pausedVolume : 1f;

        IEnumerator EncounterSequence()
        {
            FadeTo(area, 0f, areaFadeOut);
            FadeTo(amb, ambienceVolume * 0.5f, areaFadeOut);
            yield return new WaitForSecondsRealtime(bossMusicDelay);
            boss.source.Stop();                 // always start the fight from the top
            FadeTo(boss, bossVolume, bossFadeIn);
        }

        IEnumerator VictorySequence()
        {
            FadeTo(boss, 0f, bossFadeOut);
            if (victorySting != null)
            {
                sting.PlayOneShot(victorySting, stingVolume * GameSettings.MusicVolume);
                yield return new WaitForSecondsRealtime(victorySting.length * 0.8f);
            }
            area.source.Stop();
            FadeTo(area, areaVolume, areaReturnFade);
            FadeTo(amb, ambienceVolume, areaReturnFade);
        }

        void Run(IEnumerator routine)
        {
            if (sequence != null) StopCoroutine(sequence);
            sequence = StartCoroutine(routine);
        }

        void FadeTo(Channel c, float volume, float seconds)
        {
            if (c.source.clip == null) return;
            if (!c.source.isPlaying) c.level = 0f;
            c.target = volume;
            c.speed = seconds <= 0f ? float.MaxValue : Mathf.Max(Mathf.Abs(volume - c.level), 0.01f) / seconds;
            if (volume > 0f && !c.source.isPlaying) c.source.Play();
        }

        void Update()
        {
            Tick(area);
            Tick(amb);
            Tick(boss);
        }

        void Tick(Channel c)
        {
            var s = c.source;
            if (!s.isPlaying) return;
            c.level = Mathf.MoveTowards(c.level, c.target, c.speed * Time.unscaledDeltaTime);
            s.volume = c.level * GameSettings.MusicVolume * pauseScale;
            if (c.level <= 0f && c.target <= 0f) s.Stop();
        }
    }
}
