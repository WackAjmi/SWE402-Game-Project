using Hollow.Core;
using Unity.Cinemachine;
using UnityEngine;

namespace Hollow.PlayerSide
{
    /// <summary>
    /// Hit-stop, screen shake and slow motion for the whole game (Services.Feedback).
    /// Put it on the camera rig together with a CinemachineImpulseSource;
    /// the CinemachineCamera needs a CinemachineImpulseListener to feel the shakes.
    /// </summary>
    public class FeedbackService : MonoBehaviour, IFeedbackService
    {
        [SerializeField] CinemachineImpulseSource impulseSource;

        [Header("Nail hits on enemies")]
        [SerializeField] float nailHitStop = 0.03f;
        [SerializeField] float nailHitShake = 0.15f;

        [Header("Knight hurt (the freeze itself is set on PlayerHealth)")]
        [SerializeField] float hurtShake = 0.5f;

        [Header("Knight death")]
        [SerializeField] float deathSlowScale = 0.3f;
        [SerializeField] float deathSlowTime = 0.8f;

        float hitStopUntil, slowUntil, slowScale = 1f;
        bool controllingTime;

        /// <summary>While true (e.g. a pause menu owns time), this leaves Time.timeScale alone.</summary>
        public bool SuspendTimeControl { get; set; }

        void Awake()
        {
            Services.Feedback = this;
            if (impulseSource == null) impulseSource = GetComponent<CinemachineImpulseSource>();
        }

        void OnEnable()
        {
            GameEvents.HitLanded += OnHitLanded;
            GameEvents.PlayerDamaged += OnPlayerDamaged;
            GameEvents.PlayerDied += OnPlayerDied;
        }

        void OnDisable()
        {
            GameEvents.HitLanded -= OnHitLanded;
            GameEvents.PlayerDamaged -= OnPlayerDamaged;
            GameEvents.PlayerDied -= OnPlayerDied;
            if (controllingTime)
            {
                Time.timeScale = 1f;
                controllingTime = false;
            }
        }

        public void HitStop(float seconds)
        {
            if (seconds <= 0f) return;
            hitStopUntil = Mathf.Max(hitStopUntil, Time.unscaledTime + seconds);
        }

        public void SlowMotion(float timeScale, float seconds)
        {
            slowScale = Mathf.Clamp(timeScale, 0.01f, 1f);
            slowUntil = Time.unscaledTime + seconds;
        }

        public void Shake(Vector2 direction, float strength)
        {
            if (impulseSource == null || strength <= 0f) return;
            Vector2 d = direction.sqrMagnitude > 0.0001f ? direction.normalized : Random.insideUnitCircle.normalized;
            impulseSource.GenerateImpulseWithVelocity(d * strength);
        }

        void Update()
        {
            if (SuspendTimeControl) return;
            float now = Time.unscaledTime;
            float target = now < hitStopUntil ? 0f : now < slowUntil ? slowScale : 1f;
            if (target != 1f || controllingTime)
            {
                Time.timeScale = target;
                controllingTime = target != 1f;
            }
        }

        void OnHitLanded(Vector2 position, float strength)
        {
            // Fires for every damaging hit; the Knight's own hurt is handled in OnPlayerDamaged.
            Vector2 away = Services.Player != null ? position - (Vector2)Services.Player.position : Vector2.zero;
            HitStop(nailHitStop);
            Shake(away, nailHitShake * Mathf.Max(0.5f, strength));
        }

        void OnPlayerDamaged(DamageInfo info) => Shake(info.KnockbackDir, hurtShake);

        void OnPlayerDied() => SlowMotion(deathSlowScale, deathSlowTime);
    }
}
