using UnityEngine;

namespace Hollow.PlayerSide
{
    /// <summary>
    /// Tints the Knight's sprite through a list of colours for a while, then back to normal.
    /// Used for the hurt flash now, and for the green Focus heal later.
    /// </summary>
    public class SpriteFlasher : MonoBehaviour
    {
        [SerializeField] SpriteRenderer target;   // the SpriteRenderer on Visual

        Color baseColor = Color.white;
        Color[] colors;
        float endTime, interval = 0.08f;

        public bool IsFlashing => Time.time < endTime;

        void Awake()
        {
            if (target == null) target = GetComponentInChildren<SpriteRenderer>();
            if (target != null) baseColor = target.color;
        }

        /// <summary>Cycle through <paramref name="cycle"/> every <paramref name="step"/> seconds for <paramref name="duration"/> seconds.</summary>
        public void Flash(Color[] cycle, float duration, float step = 0.08f)
        {
            colors = cycle;
            endTime = Time.time + duration;
            interval = Mathf.Max(0.01f, step);
        }

        public void Stop()
        {
            endTime = 0f;
            if (target != null) target.color = baseColor;
        }

        void LateUpdate()
        {
            if (target == null) return;
            if (colors == null || colors.Length == 0 || !IsFlashing)
            {
                target.color = baseColor;
                return;
            }
            int i = (int)(Time.time / interval) % colors.Length;
            target.color = colors[i];
        }
    }
}
