using UnityEngine;

namespace Hollow.PlayerSide.GameFlow
{
    /// <summary>Sprites, sounds and text used by the title screen, pause menu and death screen.</summary>
    [CreateAssetMenu(menuName = "Hollow/Menu Art", fileName = "MenuArt")]
    public class MenuArt : ScriptableObject
    {
        [Header("Text")]
        [Tooltip("Leave empty to use Unity's built-in font. Any .ttf/.otf you import works.")]
        public Font font;
        public string gameTitle = "The Hallowed Summoner";

        [Header("Title screen")]
        public Sprite titleBackground;
        public Sprite titleFleurTop;
        public Sprite titleFleurBottom;
        public AudioClip titleMusic;
        [Range(0f, 1f)] public float titleMusicVolume = 0.6f;

        [Header("Menus")]
        [Tooltip("The selection pointer's grow animation. Shown on the left; mirrored on the right.")]
        public Sprite[] pointerFrames;
        public Sprite menuFleurTop;
        public Sprite menuFleurBottom;
        public Sprite divider;
        public Sprite sliderHandle;
        public Sprite deathBackground;

        [Header("Sounds")]
        public AudioClip selectSound;
        public AudioClip confirmSound;
        [Range(0f, 1f)] public float uiVolume = 0.7f;

        public Font Font => font != null ? font : MenuKit.DefaultFont;
    }
}
