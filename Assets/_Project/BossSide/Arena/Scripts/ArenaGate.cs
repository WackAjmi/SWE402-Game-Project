using Hollow.Core;
using UnityEngine;

namespace Hollow.BossSide.Arena
{
    /// <summary>
    /// A gate that waits raised, slams down when the encounter starts and rises again
    /// when the boss dies. The BoxCollider2D on this object (Ground layer) blocks the
    /// doorway only while the gate is down.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class ArenaGate : MonoBehaviour
    {
        [SerializeField] Transform visual;
        [Tooltip("How far above its closed position the gate waits while open.")]
        [SerializeField] float raiseHeight = 7f;
        [SerializeField] float closeSpeed = 60f;   // units per second: a slam
        [SerializeField] float openSpeed = 6f;
        [SerializeField] AudioClip closeSound;
        [SerializeField] AudioClip openSound;
        [SerializeField, Range(0f, 1f)] float volume = 0.8f;
        [SerializeField] float slamShake = 0.6f;
        [Tooltip("Testing only, until the boss exists: reopen this many seconds after closing (0 = never).")]
        [SerializeField] float autoOpenAfter = 0f;

        BoxCollider2D blocker;
        AudioSource audioSource;
        Vector3 closedPos;
        bool closed;
        float autoOpenAt;

        public bool IsClosed => closed;

        void Awake()
        {
            blocker = GetComponent<BoxCollider2D>();
            blocker.enabled = false;
            if (visual == null && transform.childCount > 0) visual = transform.GetChild(0);
            if (visual != null)
            {
                closedPos = visual.localPosition;
                visual.localPosition = closedPos + Vector3.up * raiseHeight;
            }
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }

        void OnEnable()
        {
            GameEvents.EncounterStarted += Close;
            GameEvents.BossDied += Open;
        }

        void OnDisable()
        {
            GameEvents.EncounterStarted -= Close;
            GameEvents.BossDied -= Open;
        }

        [ContextMenu("Close")]
        public void Close()
        {
            if (closed) return;
            closed = true;
            blocker.enabled = true;
            Play(closeSound);
            autoOpenAt = autoOpenAfter > 0f ? Time.time + autoOpenAfter : 0f;
        }

        [ContextMenu("Open")]
        public void Open()
        {
            if (!closed) return;
            closed = false;
            blocker.enabled = false;
            Play(openSound);
            autoOpenAt = 0f;
        }

        void Update()
        {
            if (visual != null)
            {
                Vector3 target = closed ? closedPos : closedPos + Vector3.up * raiseHeight;
                Vector3 before = visual.localPosition;
                visual.localPosition = Vector3.MoveTowards(before, target, (closed ? closeSpeed : openSpeed) * Time.deltaTime);
                bool justLanded = closed && before != target && visual.localPosition == target;
                if (justLanded) Services.Feedback.Shake(Vector2.down, slamShake);
            }

            if (autoOpenAt > 0f && Time.time >= autoOpenAt) Open();
        }

        void Play(AudioClip clip)
        {
            if (clip != null) audioSource.PlayOneShot(clip, volume);
        }
    }
}
