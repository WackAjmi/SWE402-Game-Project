using UnityEngine;

namespace Hollow.PlayerSide.UI
{
    /// <summary>The HUD's sprites and the orb's position inside the frame image.</summary>
    [CreateAssetMenu(menuName = "Hollow/HUD Art", fileName = "HudArt")]
    public class HudArt : ScriptableObject
    {
        public Sprite frame;       // soul vessel with its long tail
        public Sprite maskFull;
        public Sprite maskEmpty;
        public Sprite orbCircle;   // shapes the soul fill into a circle
        public Sprite soulFill;    // wavy-top shape: cuts out the soul's surface as it rises
        public Sprite soulTexture; // the sparkly soul itself (Soul vessels atlas)
        public Sprite orbFace;     // shown when the orb is full

        [Tooltip("Centre of the orb inside the frame image, in pixels from its top-left corner.")]
        public Vector2 orbCenter = new Vector2(105.5f, 108.8f);
        [Tooltip("Inner diameter of the orb in frame pixels.")]
        public float orbDiameter = 106f;
    }
}
