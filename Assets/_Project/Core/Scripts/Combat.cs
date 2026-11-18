using UnityEngine;

namespace Hollow.Core
{
    public enum Team { Player, Boss }

    public struct DamageInfo
    {
        public int Amount;            // masks for the player, HP for the boss
        public Team SourceTeam;
        public Vector2 HitPoint;
        public Vector2 KnockbackDir;  // normalised, attacker -> target
    }

    public interface IDamageable
    {
        Team Team { get; }
        bool IsInvulnerable { get; }  // dash i-frames, post-hit i-frames, boss death
        void TakeDamage(DamageInfo info);
    }
}