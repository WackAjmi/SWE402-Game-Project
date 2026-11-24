using System;
using UnityEngine;

namespace Hollow.BossSide.Boss
{
    /// <summary>A list of frames played at a fixed rate.</summary>
    [Serializable]
    public class SpriteClip
    {
        public Sprite[] frames;
        public float fps = 10f;
        public bool loop;

        public float Duration => frames != null && frames.Length > 0 && fps > 0f ? frames.Length / fps : 0f;
    }

    /// <summary>
    /// Plays SpriteClips on a SpriteRenderer. Driven by the boss state machine, which decides
    /// what plays and how fast (charge-ups are stretched or squeezed to match their timing).
    /// Uses scaled time, so hit-stop and the pause menu freeze it.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class SpriteAnimator : MonoBehaviour
    {
        SpriteRenderer sr;
        SpriteClip clip;
        float time;
        float speed = 1f;

        public SpriteClip Current => clip;
        public bool IsDone => clip == null || (!clip.loop && time >= clip.Duration);
        public int FrameIndex { get; private set; }

        void Awake() => sr = GetComponent<SpriteRenderer>();

        /// <summary>Plays a clip. Speed 2 plays it twice as fast.</summary>
        public void Play(SpriteClip newClip, float playbackSpeed = 1f, bool restart = true)
        {
            if (newClip == null || newClip.frames == null || newClip.frames.Length == 0) return;
            if (!restart && clip == newClip) { speed = playbackSpeed; return; }
            clip = newClip;
            speed = playbackSpeed;
            time = 0f;
            Show(0);
        }

        /// <summary>Plays a clip so that it lasts exactly the given number of seconds.</summary>
        public void PlayOver(SpriteClip newClip, float seconds)
        {
            if (newClip == null) return;
            Play(newClip, seconds > 0f ? newClip.Duration / seconds : 1f);
        }

        void Update()
        {
            if (clip == null || clip.frames == null || clip.frames.Length == 0) return;
            time += Time.deltaTime * speed;
            int count = clip.frames.Length;
            int i = Mathf.FloorToInt(time * clip.fps);
            i = clip.loop ? ((i % count) + count) % count : Mathf.Min(i, count - 1);
            if (i != FrameIndex) Show(i);
        }

        void Show(int i)
        {
            FrameIndex = i;
            if (sr == null) sr = GetComponent<SpriteRenderer>();
            sr.sprite = clip.frames[i];
        }
    }
}
