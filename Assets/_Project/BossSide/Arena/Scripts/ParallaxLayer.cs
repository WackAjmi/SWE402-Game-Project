using UnityEngine;

namespace Hollow.BossSide.Arena
{
    /// <summary>
    /// Moves a group of sprites with the camera to fake depth.
    /// At the reference point the sprites sit exactly where they were placed.
    /// factor 0 = part of the world, 0..1 = background (further = closer to 1),
    /// negative = foreground (moves faster than the world).
    /// </summary>
    public class ParallaxLayer : MonoBehaviour
    {
        [SerializeField] float factor = 0.5f;
        [SerializeField] bool vertical = true;
        [Tooltip("Camera position at which the layer shows exactly as placed in the editor (the arena's centre).")]
        [SerializeField] Vector2 referencePoint;

        Transform cam;
        Vector3 placed;

        public void Setup(float parallaxFactor, Vector2 reference)
        {
            factor = parallaxFactor;
            referencePoint = reference;
        }

        void Start()
        {
            placed = transform.position;
            var c = Camera.main;
            if (c == null) { enabled = false; return; }
            cam = c.transform;
        }

        void LateUpdate()
        {
            Vector2 d = (Vector2)cam.position - referencePoint;
            transform.position = placed + new Vector3(d.x * factor, vertical ? d.y * factor : 0f, 0f);
        }
    }
}
