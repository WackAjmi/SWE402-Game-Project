using UnityEngine;

namespace Hollow.Core
{
    [RequireComponent(typeof(Collider2D))]
    public class Hurtbox : MonoBehaviour
    {
        [Tooltip("Down-strikes bounce off this (boss body, projectiles).")]
        public bool Pogoable;

        // null = pogo-only hurtbox, e.g. on a projectile
        public IDamageable Owner { get; private set; }

        void Awake() => Owner = GetComponentInParent<IDamageable>();
    }
}