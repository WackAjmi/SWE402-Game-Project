using System.Collections;
using Hollow.Core;
using UnityEngine;

namespace Hollow.BossSide.Testing
{
    /// <summary>
    /// Placeholder boss for testing the encounter flow until the real boss exists.
    /// Wakes up when the gates slam, hurts the Knight on contact, and every few seconds
    /// flashes yellow and slams the floor around itself. At 0 health it raises BossDied,
    /// which opens the gates and plays the victory music.
    /// </summary>
    public class DummyBoss : MonoBehaviour, IDamageable
    {
        [Header("Health")]
        [SerializeField] int maxHealth = 10;

        [Header("Parts (set by the menu tool)")]
        [SerializeField] SpriteRenderer body;
        [SerializeField] Hitbox contactHitbox;
        [SerializeField] Hitbox slamHitbox;
        [SerializeField] SpriteRenderer slamFlash;
        [SerializeField] Transform healthBarFill;

        [Header("Attack")]
        [Tooltip("Seconds after the gates slam before it starts attacking.")]
        [SerializeField] float wakeDelay = 1.5f;
        [SerializeField] Vector2 attackInterval = new Vector2(2f, 3.2f);
        [SerializeField] float telegraphTime = 0.6f;
        [SerializeField] float slamActiveTime = 0.15f;
        [SerializeField] float slamShake = 0.5f;

        [Header("Look")]
        [SerializeField] Color idleColor = new Color(0.42f, 0.36f, 0.55f);
        [SerializeField] Color telegraphColor = new Color(1f, 0.85f, 0.2f);
        [SerializeField] float hitFlashTime = 0.08f;

        int health;
        bool awake, dead;
        float hitFlashUntil;
        float contactCycle;
        float barWidth = 1f;

        public Team Team => Team.Boss;
        public bool IsInvulnerable => dead || !awake;
        public int Health => health;

        void Awake()
        {
            health = maxHealth;
            if (body != null) body.color = idleColor;
            if (slamHitbox != null) slamHitbox.gameObject.SetActive(false);
            if (slamFlash != null) slamFlash.enabled = false;
            if (contactHitbox != null) contactHitbox.gameObject.SetActive(false);
            if (healthBarFill != null) barWidth = healthBarFill.localScale.x;
            UpdateHealthBar();
        }

        void OnEnable() => GameEvents.EncounterStarted += OnEncounterStarted;
        void OnDisable() => GameEvents.EncounterStarted -= OnEncounterStarted;

        void OnEncounterStarted()
        {
            if (!awake && !dead) StartCoroutine(Fight());
        }

        IEnumerator Fight()
        {
            yield return new WaitForSeconds(wakeDelay);
            awake = true;
            if (contactHitbox != null) contactHitbox.gameObject.SetActive(true);
            GameEvents.BossHealthChanged?.Invoke(health, maxHealth);

            while (!dead)
            {
                yield return new WaitForSeconds(Random.Range(attackInterval.x, attackInterval.y));
                if (dead) yield break;

                // Telegraph: fade to yellow.
                for (float t = 0f; t < telegraphTime && !dead; t += Time.deltaTime)
                {
                    if (Time.time >= hitFlashUntil && body != null)
                        body.color = Color.Lerp(idleColor, telegraphColor, t / telegraphTime);
                    yield return null;
                }
                if (dead) yield break;

                // Slam.
                if (body != null) body.color = idleColor;
                if (slamFlash != null) slamFlash.enabled = true;
                if (slamHitbox != null) slamHitbox.gameObject.SetActive(true);
                Services.Feedback.Shake(Vector2.down, slamShake);
                yield return new WaitForSeconds(slamActiveTime);
                if (slamHitbox != null) slamHitbox.gameObject.SetActive(false);
                if (slamFlash != null) slamFlash.enabled = false;
            }
        }

        void Update()
        {
            // Re-arm the contact hitbox now and then, so standing inside the boss keeps hurting
            // (each Hitbox activation only hits a target once; the Knight's i-frames still apply).
            if (awake && !dead && contactHitbox != null)
            {
                contactCycle += Time.deltaTime;
                if (contactCycle >= 0.5f)
                {
                    contactCycle = 0f;
                    contactHitbox.gameObject.SetActive(false);
                    contactHitbox.gameObject.SetActive(true);
                }
            }

            if (body != null && hitFlashUntil > 0f && Time.time >= hitFlashUntil)
            {
                hitFlashUntil = 0f;
                body.color = idleColor;
            }
        }

        public void TakeDamage(DamageInfo info)
        {
            if (IsInvulnerable) return;
            health = Mathf.Max(0, health - Mathf.Max(1, info.Amount));
            GameEvents.BossHealthChanged?.Invoke(health, maxHealth);
            UpdateHealthBar();

            if (body != null) body.color = Color.white;
            hitFlashUntil = Time.time + hitFlashTime;

            if (health == 0) Die();
        }

        void Die()
        {
            dead = true;
            StopAllCoroutines();
            if (contactHitbox != null) contactHitbox.gameObject.SetActive(false);
            if (slamHitbox != null) slamHitbox.gameObject.SetActive(false);
            if (slamFlash != null) slamFlash.enabled = false;
            foreach (var c in GetComponentsInChildren<Collider2D>()) c.enabled = false;
            GameEvents.BossDied?.Invoke();
            StartCoroutine(FadeAway());
        }

        IEnumerator FadeAway()
        {
            var renderers = GetComponentsInChildren<SpriteRenderer>();
            for (float t = 0f; t < 1.2f; t += Time.deltaTime)
            {
                float a = 1f - t / 1.2f;
                foreach (var r in renderers)
                {
                    var c = r.color;
                    c.a = Mathf.Min(c.a, a);
                    r.color = c;
                }
                yield return null;
            }
            gameObject.SetActive(false);
        }

        void UpdateHealthBar()
        {
            if (healthBarFill == null) return;
            var s = healthBarFill.localScale;
            s.x = barWidth * (maxHealth > 0 ? (float)health / maxHealth : 0f);
            healthBarFill.localScale = s;
        }
    }
}
