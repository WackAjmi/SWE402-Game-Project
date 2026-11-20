using Hollow.Core;
using UnityEngine;

namespace Hollow.BossSide.Arena
{
    /// <summary>
    /// A solid surface the nail bounces off with a "tink": sound, small spark and a tiny freeze.
    /// Put it on a trigger collider on the BossHurtbox layer, next to a Hurtbox with Pogoable ticked
    /// and no IDamageable above it. The Hurtbox handles the pogo; this script handles the feel.
    /// Pogo-only hurtboxes give no soul, so tinking can't be farmed.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class NailTink : MonoBehaviour
    {
        [SerializeField] AudioClip tinkSound;
        [SerializeField, Range(0f, 1f)] float volume = 0.9f;
        [SerializeField] float pitchJitter = 0.06f;
        [SerializeField] float hitStop = 0.03f;
        [SerializeField] Sprite sparkSprite;
        [SerializeField] float sparkScale = 0.6f;
        [SerializeField] float sparkTime = 0.1f;

        Collider2D surface;
        AudioSource audioSource;
        SpriteRenderer spark;
        float sparkAge = -1f;
        float nextTinkTime;

        void Awake()
        {
            surface = GetComponent<Collider2D>();
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;

            if (sparkSprite != null)
            {
                var go = new GameObject("Tink Spark");
                go.transform.SetParent(transform, false);
                spark = go.AddComponent<SpriteRenderer>();
                spark.sprite = sparkSprite;
                spark.sortingLayerName = "VFX";
                spark.enabled = false;
            }
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (Time.unscaledTime < nextTinkTime) return;
            if (!other.TryGetComponent(out Hitbox hitbox) || hitbox.Team != Team.Player) return;
            nextTinkTime = Time.unscaledTime + 0.1f;

            if (tinkSound != null)
            {
                audioSource.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
                audioSource.PlayOneShot(tinkSound, volume);
            }

            if (spark != null)
            {
                Vector2 point = surface.ClosestPoint(other.bounds.center);
                spark.transform.SetPositionAndRotation(point, Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)));
                spark.transform.localScale = Vector3.one * sparkScale;
                spark.color = Color.white;
                spark.enabled = true;
                sparkAge = 0f;
            }

            if (hitStop > 0f) Services.Feedback.HitStop(hitStop);
        }

        void Update()
        {
            if (sparkAge < 0f) return;
            sparkAge += Time.unscaledDeltaTime;
            float k = sparkAge / sparkTime;
            if (k >= 1f) { spark.enabled = false; sparkAge = -1f; return; }
            spark.color = new Color(1f, 1f, 1f, 1f - k);
            spark.transform.localScale = Vector3.one * sparkScale * (1f + 0.4f * k);
        }
    }
}
