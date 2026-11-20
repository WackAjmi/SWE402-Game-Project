using Hollow.Core;
using UnityEngine;

namespace Hollow.PlayerSide.Testing
{
    /// <summary>
    /// Stand-in for the boss in Sandbox_A_Player: takes nail hits (flashes red, logs them)
    /// and, if given an attack hitbox, swings back on a timer: turns yellow as a warning,
    /// then switches the hitbox on briefly.
    /// Root: this component. Children: a Hurtbox (trigger, layer BossHurtbox, Hurtbox component)
    /// and optionally an Attack (trigger, layer BossHitbox, Hitbox component with Team = Boss).
    /// </summary>
    public class TrainingDummy : MonoBehaviour, IDamageable
    {
        [SerializeField] SpriteRenderer sprite;

        [Header("Hits back (optional)")]
        [SerializeField] GameObject attackHitbox;
        [SerializeField] float attackInterval = 2f;
        [SerializeField] float telegraphTime = 0.4f;
        [SerializeField] float attackActiveTime = 0.2f;
        [SerializeField] Color telegraphColor = new Color(1f, 0.85f, 0.2f);

        int hits;
        float flashUntil;
        float nextAttackTime, attackEndTime;
        bool attacking;

        public Team Team => Team.Boss;
        public bool IsInvulnerable => false;

        void Awake()
        {
            if (sprite == null) sprite = GetComponentInChildren<SpriteRenderer>();
        }

        void Start()
        {
            if (attackHitbox != null) attackHitbox.SetActive(false);
            nextAttackTime = Time.time + attackInterval;
        }

        public void TakeDamage(DamageInfo info)
        {
            hits++;
            flashUntil = Time.time + 0.08f;
            Debug.Log($"Dummy hit #{hits}: {info.Amount} damage at {info.HitPoint}");
        }

        void Update()
        {
            bool telegraphing = false;

            if (attackHitbox != null)
            {
                float t = Time.time;
                if (!attacking && t >= nextAttackTime)
                {
                    attackHitbox.SetActive(true);
                    attacking = true;
                    attackEndTime = t + attackActiveTime;
                }
                else if (attacking && t >= attackEndTime)
                {
                    attackHitbox.SetActive(false);
                    attacking = false;
                    nextAttackTime = t + attackInterval;
                }
                telegraphing = !attacking && t >= nextAttackTime - telegraphTime;
            }

            if (sprite == null) return;
            if (Time.time < flashUntil) sprite.color = Color.red;
            else if (telegraphing || attacking) sprite.color = telegraphColor;
            else sprite.color = Color.white;
        }
    }
}
