using Hollow.Core;
using UnityEngine;

namespace Hollow.BossSide.Arena
{
    /// <summary>
    /// Starts the encounter (GameEvents.EncounterStarted) once the Knight walks past this
    /// object's X position. Uses the Knight's position directly, so it needs no physics layer.
    /// </summary>
    public class ArenaTrigger : MonoBehaviour
    {
        [Tooltip("The Knight must also be between these heights.")]
        [SerializeField] float minY = -2f;
        [SerializeField] float maxY = 20f;

        bool fired;

        void Update()
        {
            if (fired || Services.Player == null) return;
            Vector3 p = Services.Player.position;
            if (p.x > transform.position.x && p.y > minY && p.y < maxY)
            {
                fired = true;
                GameEvents.EncounterStarted?.Invoke();
            }
        }

        [ContextMenu("Re-arm")]
        public void ReArm() => fired = false;

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.8f);
            Vector3 x = transform.position;
            Gizmos.DrawLine(new Vector3(x.x, minY, 0f), new Vector3(x.x, maxY, 0f));
        }
    }
}
