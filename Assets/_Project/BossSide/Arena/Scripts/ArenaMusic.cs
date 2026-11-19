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
    /// Fades use unscaled time, so hit-stop and slow motion don't stretch them.
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

        class Channel
        {
            public AudioSource source;
            public float target;
            public float speed;
        }

        Channel area, amb, boss;
        AudioSource sting;
        Coroutine sequence;

        void Awake()
        {
            area = MakeChannel(areaTheme);
            amb = MakeChannel(ambience);
            boss = MakeChannel(bossMusic);
            sting = gameObject.AddComponent<AudioSource>();
            sting.playOnAwake = false;
            sting.spatialBlend = 0f;
        }

        Channel MakeChannel(AudioClip clip)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = true;
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            s.volume = 0f;
            return new Channel { source = s };
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

        void Start()
        {
            FadeTo(area, areaVolume, startFade);
            FadeTo(amb, ambienceVolume, startFade);
        }

        void OnEncounterStarted() => Run(EncounterSequence());
        void OnBossDied() => Run(VictorySequence());

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
                sting.PlayOneShot(victorySting, stingVolume);
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
            c.target = volume;
            c.speed = seconds <= 0f ? float.MaxValue : Mathf.Max(Mathf.Abs(volume - c.source.volume), 0.01f) / seconds;
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
            s.volume = Mathf.MoveTowards(s.volume, c.target, c.speed * Time.unscaledDeltaTime);
            if (s.volume <= 0f && c.target <= 0f) s.Stop();
        }
    }
}
