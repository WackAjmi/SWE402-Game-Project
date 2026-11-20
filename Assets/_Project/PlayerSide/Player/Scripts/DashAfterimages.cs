using UnityEngine;

namespace Hollow.PlayerSide
{
    /// <summary>
    /// Leaves fading copies of the Knight's sprite behind him while he dashes.
    /// The copies are pooled, so dashing creates no garbage.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class DashAfterimages : MonoBehaviour
    {
        [SerializeField] SpriteRenderer source;                 // the SpriteRenderer on Visual
        [SerializeField] float interval = 0.035f;               // seconds between copies
        [SerializeField] float fadeTime = 0.25f;
        [SerializeField] Color tint = new Color(0.55f, 0.6f, 0.85f, 0.6f);
        [SerializeField] int poolSize = 10;
        [Tooltip("Optional: Sprite-Unlit-Default keeps them visible in a dark arena.")]
        [SerializeField] Material material;

        PlayerController controller;
        SpriteRenderer[] pool;
        float[] age;
        int next;
        float timer;

        void Awake()
        {
            controller = GetComponent<PlayerController>();
            if (source == null) source = GetComponentInChildren<SpriteRenderer>();

            pool = new SpriteRenderer[poolSize];
            age = new float[poolSize];
            for (int i = 0; i < poolSize; i++)
            {
                var go = new GameObject("DashAfterimage");
                go.hideFlags = HideFlags.HideInHierarchy;
                var sr = go.AddComponent<SpriteRenderer>();
                if (material != null) sr.sharedMaterial = material;
                sr.enabled = false;
                pool[i] = sr;
                age[i] = fadeTime;
            }
        }

        void OnDestroy()
        {
            if (pool == null) return;
            foreach (var sr in pool)
                if (sr != null) Destroy(sr.gameObject);
        }

        void Update()
        {
            float dt = Time.deltaTime;

            if (controller.IsDashing && source != null)
            {
                timer -= dt;
                if (timer <= 0f)
                {
                    Spawn();
                    timer = interval;
                }
            }
            else timer = 0f;   // the first copy appears the moment the next dash starts

            for (int i = 0; i < pool.Length; i++)
            {
                if (!pool[i].enabled) continue;
                age[i] += dt;
                float k = 1f - age[i] / fadeTime;
                if (k <= 0f) { pool[i].enabled = false; continue; }
                var c = tint;
                c.a *= k;
                pool[i].color = c;
            }
        }

        void Spawn()
        {
            int i = next;
            next = (next + 1) % pool.Length;
            var sr = pool[i];

            var t = sr.transform;
            t.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            t.localScale = source.transform.lossyScale;   // includes the facing flip

            sr.sprite = source.sprite;
            sr.flipX = source.flipX;
            sr.sortingLayerID = source.sortingLayerID;
            sr.sortingOrder = source.sortingOrder - 1;   // behind the Knight
            sr.color = tint;
            sr.enabled = true;
            age[i] = 0f;
        }
    }
}
