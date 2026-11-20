using Hollow.Core;
using UnityEngine;

namespace Hollow.PlayerSide
{
    /// <summary>
    /// Plays the Knight's sounds by listening to his controller, health and soul.
    /// Footsteps loop while he runs; everything else is a one-shot with a little pitch variation.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class KnightAudio : MonoBehaviour
    {
        [SerializeField] KnightSounds sounds;
        [Tooltip("Random pitch change per sound, so repeats don't sound identical.")]
        [SerializeField] float pitchJitter = 0.06f;
        [Tooltip("Landings from falls faster than this play the hard landing.")]
        [SerializeField] float hardLandingSpeed = 18f;
        [Tooltip("Landings slower than this are silent.")]
        [SerializeField] float minLandingSpeed = 2f;

        PlayerController controller;
        PlayerSoul soul;
        AudioSource sfx, steps;
        Hitbox[] nailHitboxes;
        int lastSwing = -1;

        void Awake()
        {
            controller = GetComponent<PlayerController>();
            soul = GetComponent<PlayerSoul>();

            sfx = gameObject.AddComponent<AudioSource>();
            steps = gameObject.AddComponent<AudioSource>();
            foreach (var s in new[] { sfx, steps })
            {
                s.playOnAwake = false;
                s.spatialBlend = 0f;   // 2D: the camera always follows the Knight
            }
            steps.loop = true;
            if (sounds != null)
            {
                steps.clip = sounds.footsteps;
                steps.volume = sounds.footstepVolume;
            }

            nailHitboxes = GetComponentsInChildren<Hitbox>(true);
        }

        void OnEnable()
        {
            controller.Jumped += OnJump;
            controller.DoubleJumped += OnDoubleJump;
            controller.Dashed += OnDash;
            controller.Slashed += OnSlash;
            controller.Landed += OnLanded;
            if (soul != null) soul.Focused += OnFocus;
            GameEvents.PlayerDamaged += OnDamaged;
            GameEvents.PlayerDied += OnDied;
            foreach (var hb in nailHitboxes)
                if (hb != null && hb.Team == Team.Player) hb.OnHit += OnNailHit;
        }

        void OnDisable()
        {
            controller.Jumped -= OnJump;
            controller.DoubleJumped -= OnDoubleJump;
            controller.Dashed -= OnDash;
            controller.Slashed -= OnSlash;
            controller.Landed -= OnLanded;
            if (soul != null) soul.Focused -= OnFocus;
            GameEvents.PlayerDamaged -= OnDamaged;
            GameEvents.PlayerDied -= OnDied;
            foreach (var hb in nailHitboxes)
                if (hb != null) hb.OnHit -= OnNailHit;
        }

        void Update()
        {
            bool running = controller.IsRunning;
            if (running && !steps.isPlaying && steps.clip != null) steps.Play();
            else if (!running && steps.isPlaying) steps.Stop();
        }

        void Play(AudioClip clip)
        {
            if (clip == null || sounds == null) return;
            sfx.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            sfx.PlayOneShot(clip, sounds.volume);
        }

        void OnJump() => Play(sounds ? sounds.jump : null);
        void OnDoubleJump() => Play(sounds ? sounds.doubleJump : null);
        void OnDash() => Play(sounds ? sounds.dash : null);
        void OnFocus() => Play(sounds ? sounds.focus : null);

        void OnSlash()
        {
            if (sounds == null || sounds.swings == null || sounds.swings.Length == 0) return;
            int i = Random.Range(0, sounds.swings.Length);
            if (sounds.swings.Length > 1 && i == lastSwing) i = (i + 1) % sounds.swings.Length;
            lastSwing = i;
            Play(sounds.swings[i]);
        }

        void OnLanded(float fallSpeed)
        {
            if (sounds == null || fallSpeed < minLandingSpeed) return;
            Play(fallSpeed >= hardLandingSpeed ? sounds.landHard : sounds.landSoft);
        }

        void OnNailHit(Hurtbox hurt, DamageInfo info)
        {
            if (hurt.Owner != null && sounds != null) Play(sounds.nailHit);
        }

        void OnDamaged(DamageInfo info)
        {
            if (sounds == null) return;
            Play(info.Amount >= 2 && sounds.doubleDamage != null ? sounds.doubleDamage : sounds.damage);
        }

        void OnDied()
        {
            steps.Stop();
            if (sounds != null) Play(sounds.death);
        }
    }
}
