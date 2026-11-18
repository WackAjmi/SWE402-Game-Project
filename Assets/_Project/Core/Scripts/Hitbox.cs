using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hollow.Core
{
    [RequireComponent(typeof(Collider2D))]
    public class Hitbox : MonoBehaviour
    {
        public Team Team;
        public int Damage = 1;

        // Fired for every hurtbox touched, pogo-only ones included.
        // Student A uses it for pogo bounce and Focus gain.
        public event Action<Hurtbox, DamageInfo> OnHit;

        readonly HashSet<Hurtbox> hitThisActivation = new();

        // Enable the hitbox GameObject once per swing or attack:
        // each target is hit at most once per activation.
        void OnEnable() => hitThisActivation.Clear();

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.TryGetComponent(out Hurtbox hurt)) return;
            IDamageable target = hurt.Owner;
            if (target != null && target.Team == Team) return;
            if (!hitThisActivation.Add(hurt)) return;

            Vector2 from = transform.position;
            var info = new DamageInfo
            {
                Amount = Damage,
                SourceTeam = Team,
                HitPoint = other.ClosestPoint(from),
                KnockbackDir = ((Vector2)other.transform.position - from).normalized
            };

            if (target != null && !target.IsInvulnerable)
            {
                target.TakeDamage(info);
                GameEvents.HitLanded?.Invoke(info.HitPoint, Mathf.Clamp01(Damage / 3f));
            }
            OnHit?.Invoke(hurt, info);
        }
    }
}