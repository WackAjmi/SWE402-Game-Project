using UnityEngine;

namespace Hollow.PlayerSide
{
    /// <summary>All of the Knight's sound clips in one asset, so KnightAudio needs one reference.</summary>
    [CreateAssetMenu(menuName = "Hollow/Knight Sounds", fileName = "KnightSounds")]
    public class KnightSounds : ScriptableObject
    {
        [Range(0f, 1f)] public float volume = 0.8f;
        [Range(0f, 1f)] public float footstepVolume = 0.6f;

        [Header("Movement")]
        public AudioClip footsteps;      // looped while running
        public AudioClip jump;
        public AudioClip doubleJump;
        public AudioClip landSoft;
        public AudioClip landHard;
        public AudioClip dash;

        [Header("Nail")]
        public AudioClip[] swings;       // one picked at random per swing
        public AudioClip nailHit;        // the nail connecting with an enemy

        [Header("Health")]
        public AudioClip damage;
        public AudioClip doubleDamage;   // hits that take 2 masks
        public AudioClip death;
        public AudioClip focus;
    }
}
