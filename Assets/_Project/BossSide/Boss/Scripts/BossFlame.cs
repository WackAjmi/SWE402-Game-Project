using Hollow.Core;
using UnityEngine;

namespace Hollow.BossSide.Boss
{
    /// <summary>
    /// The boss's flame. Flies in a straight line, nose first, and disappears when it touches
    /// the arena (Ground layer), hits the Knight, or runs out of time. Reused from a pool.
    /// Needs on the same object: a trigger Collider2D, a kinematic Rigidbody2D and a Hitbox (Team Boss).
    /// </summary>
    [RequireComponent(typeof(Hitbox))]
    public class BossFlame : MonoBehaviour
    {
        [Tooltip("How far ahead of the centre the flame's nose is, for the wall check.")]
        [SerializeField] float noseDistance = 0.6f;
        [SerializeField] float wallCheckRadius = 0.15f;

        Vector2 velocity;
        float settleY = float.NaN;
        float settleSpeed;
        float scale = 1f;
        float dieAt;
        float safeUntil;
        int groundMask;
        Hitbox hitbox;

        public bool Active => gameObject.activeSelf;

        void Awake()
        {
            hitbox = GetComponent<Hitbox>();
            hitbox.OnHit += OnHitSomething;
            groundMask = LayerMask.GetMask("Ground");
        }

        void OnDestroy()
        {
            if (hitbox != null) hitbox.OnHit -= OnHitSomething;
        }

        /// <summary>
        /// Starts the flame at a position with a velocity (units per second).
        /// size scales the whole flame (sprite and hit area). If settleToY is given, the flame
        /// glides to that height over settleTime while it travels (used for the low horizontal throw).
        /// </summary>
        public void Launch(Vector2 position, Vector2 vel, float lifetime, float ignoreWallsFor = 0f,
                           float size = 1f, float settleToY = float.NaN, float settleTime = 0.25f)
        {
            velocity = vel;
            scale = size;
            transform.localScale = Vector3.one * size;
            settleY = settleToY;
            settleSpeed = float.IsNaN(settleToY) ? 0f : Mathf.Abs(settleToY - position.y) / Mathf.Max(0.01f, settleTime);
            transform.position = position;
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(vel.y, vel.x) * Mathf.Rad2Deg);
            dieAt = Time.time + lifetime;
            safeUntil = Time.time + ignoreWallsFor;
            gameObject.SetActive(true);   // re-arms the Hitbox (one hit per launch)
        }

        public void Despawn() => gameObject.SetActive(false);

        void Update()
        {
            Vector2 pos = (Vector2)transform.position + velocity * Time.deltaTime;
            if (!float.IsNaN(settleY)) pos.y = Mathf.MoveTowards(pos.y, settleY, settleSpeed * Time.deltaTime);
            transform.position = pos;

            if (Time.time >= dieAt) { Despawn(); return; }
            if (Time.time >= safeUntil)
            {
                Vector2 nose = pos + velocity.normalized * noseDistance * scale;
                if (Physics2D.OverlapCircle(nose, wallCheckRadius * scale, groundMask) != null) Despawn();
            }
        }

        void OnHitSomething(Hurtbox hurt, DamageInfo info)
        {
            if (hurt.Owner != null && hurt.Owner.Team == Team.Player) Despawn();
        }
    }
}
