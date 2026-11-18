using Hollow.Core;
using UnityEngine;

namespace Hollow.PlayerSide.Testing
{
    /// <summary>
    /// Stand-in for the boss in Sandbox_A_Player: takes nail hits, flashes red and logs them.
    /// Put it on the dummy's root; give it a child with a trigger collider,
    /// layer BossHurtbox and a Hurtbox component (tick Pogoable to test the pogo).
    /// </summary>
    public class TrainingDummy : MonoBehaviour, IDamageable
    {
        [SerializeField] SpriteRenderer sprite;

        int hits;
        float flashUntil;

        public Team Team => Team.Boss;
        public bool IsInvulnerable => false;

        void Awake()
        {
            if (sprite == null) sprite = GetComponentInChildren<SpriteRenderer>();
        }

        public void TakeDamage(DamageInfo info)
        {
            hits++;
            flashUntil = Time.time + 0.08f;
            Debug.Log($"Dummy hit #{hits}: {info.Amount} damage at {info.HitPoint}");
        }

        void Update()
        {
            if (sprite != null) sprite.color = Time.time < flashUntil ? Color.red : Color.white;
        }
    }
}
