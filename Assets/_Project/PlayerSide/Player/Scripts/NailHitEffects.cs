using Hollow.Core;
using UnityEngine;

namespace Hollow.PlayerSide
{
    /// <summary>
    /// A white flash where the nail connects with an enemy (Hollow Knight's hit flash).
    /// Pooled; animates in unscaled time so it still shows during hit-stop.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class NailHitEffects : MonoBehaviour
    {
        [SerializeField] Sprite sparkSprite;
        [Tooltip("Optional: Sprite-Unlit-Default keeps the flash bright in a dark arena.")]
        [SerializeField] Material material;
        [SerializeField] string sortingLayer = "VFX";
        [SerializeField] float lifetime = 0.12f;
        [SerializeField] float startScale = 0.5f;
        [SerializeField] float endScale = 1f;
        [SerializeField] int poolSize = 6;

        Hitbox[] nailHitboxes;
        SpriteRenderer[] pool;
        float[] age;
        int next;

        void Awake()
        {
            nailHitboxes = GetComponentsInChildren<Hitbox>(true);

            pool = new SpriteRenderer[poolSize];
            age = new float[poolSize];
            for (int i = 0; i < poolSize; i++)
            {
                var go = new GameObject("NailHitSpark");
                go.hideFlags = HideFlags.HideInHierarchy;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sparkSprite;
                if (material != null) sr.sharedMaterial = material;
                sr.sortingLayerName = sortingLayer;
                sr.enabled = false;
                pool[i] = sr;
                age[i] = lifetime;
            }
        }

        void OnEnable()
        {
            foreach (var hb in nailHitboxes)
                if (hb != null && hb.Team == Team.Player) hb.OnHit += OnNailHit;
        }

        void OnDisable()
        {
            foreach (var hb in nailHitboxes)
                if (hb != null) hb.OnHit -= OnNailHit;
        }

        void OnDestroy()
        {
            if (pool == null) return;
            foreach (var sr in pool)
                if (sr != null) Destroy(sr.gameObject);
        }

        void OnNailHit(Hurtbox hurt, DamageInfo info)
        {
            if (hurt.Owner == null || sparkSprite == null) return;   // only real enemies
            int i = next;
            next = (next + 1) % pool.Length;
            pool[i].transform.SetPositionAndRotation(info.HitPoint, Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)));
            pool[i].transform.localScale = Vector3.one * startScale;
            pool[i].color = Color.white;
            pool[i].enabled = true;
            age[i] = 0f;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < pool.Length; i++)
            {
                if (!pool[i].enabled) continue;
                age[i] += dt;
                float k = age[i] / lifetime;
                if (k >= 1f) { pool[i].enabled = false; continue; }
                pool[i].transform.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, k);
                pool[i].color = new Color(1f, 1f, 1f, 1f - k * k);
            }
        }
    }
}
