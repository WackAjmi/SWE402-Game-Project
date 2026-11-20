using System.Collections;
using Hollow.Core;
using UnityEngine;

namespace Hollow.PlayerSide
{
    /// <summary>
    /// The Knight's masks. Takes hits through his Hurtbox, flashes colours and gets
    /// pushed away, stays invulnerable for a moment, and dies at 0 masks.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerHealth : MonoBehaviour, IDamageable
    {
        [Header("Masks")]
        [SerializeField] int maxMasks = 5;
        [Tooltip("Invulnerable for this long after a hit.")]
        [SerializeField] float invulnerableTime = 1.3f;

        [Header("Knockback")]
        [SerializeField] float knockbackSpeed = 8f;
        [SerializeField] float knockbackUpSpeed = 6f;
        [Tooltip("No control for this long after a hit.")]
        [SerializeField] float knockbackTime = 0.2f;

        [Header("Hurt feedback")]
        [Tooltip("The sprite cycles through these while invulnerable. White = normal colours.")]
        [SerializeField] Color[] hurtColors = { new Color(1f, 0.25f, 0.25f), Color.white };
        [SerializeField] float hurtFlashStep = 0.08f;
        [Tooltip("Brief freeze on every hit. Does nothing until the feedback step adds it.")]
        [SerializeField] float hitStop = 0.08f;

        [Header("Sandbox only")]
        [Tooltip("Respawn at the start point after dying. Turn off once the game flow handles death.")]
        [SerializeField] bool respawnAfterDeath = true;
        [SerializeField] float respawnDelay = 2.5f;

        PlayerController controller;
        SpriteFlasher flasher;
        int masks;
        float invulnerableUntil;
        bool dead;
        Vector3 spawnPoint;

        public Team Team => Team.Player;
        public bool IsInvulnerable => dead || Time.time < invulnerableUntil || controller.IsDashInvulnerable;
        public int Masks => masks;
        public int MaxMasks => maxMasks;
        public bool IsDead => dead;

        void Awake()
        {
            controller = GetComponent<PlayerController>();
            flasher = GetComponent<SpriteFlasher>();
            masks = maxMasks;
            spawnPoint = transform.position;
        }

        void Start()
        {
            GameEvents.PlayerHealthChanged?.Invoke(masks, maxMasks);
        }

        public void TakeDamage(DamageInfo info)
        {
            if (IsInvulnerable) return;

            masks = Mathf.Max(0, masks - Mathf.Max(1, info.Amount));
            GameEvents.PlayerDamaged?.Invoke(info);
            GameEvents.PlayerHealthChanged?.Invoke(masks, maxMasks);
            Services.Feedback.HitStop(hitStop);

            if (masks == 0)
            {
                Die();
                return;
            }

            invulnerableUntil = Time.time + invulnerableTime;

            // Push away from whatever hit him (the hitbox works out the direction).
            int away = info.KnockbackDir.x > 0.01f ? 1 : info.KnockbackDir.x < -0.01f ? -1 : -controller.Facing;
            controller.ApplyKnockback(away, knockbackSpeed, knockbackUpSpeed, knockbackTime);

            if (flasher != null) flasher.Flash(hurtColors, invulnerableTime, hurtFlashStep);
        }

        /// <summary>Restores masks. The Focus heal will call this.</summary>
        public void Heal(int amount)
        {
            if (dead || amount <= 0 || masks >= maxMasks) return;
            masks = Mathf.Min(maxMasks, masks + amount);
            GameEvents.PlayerHealthChanged?.Invoke(masks, maxMasks);
        }

        void Die()
        {
            dead = true;
            if (flasher != null) flasher.Stop();
            controller.Die();
            GameEvents.PlayerDied?.Invoke();
            if (respawnAfterDeath) StartCoroutine(RespawnAfterDelay());
        }

        IEnumerator RespawnAfterDelay()
        {
            yield return new WaitForSeconds(respawnDelay);
            transform.position = spawnPoint;
            masks = maxMasks;
            dead = false;
            invulnerableUntil = Time.time + 1f;   // a moment of safety after respawning
            controller.Revive();
            GameEvents.PlayerHealthChanged?.Invoke(masks, maxMasks);
        }
    }
}
