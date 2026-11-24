using UnityEngine;

namespace Hollow.BossSide.Boss
{
    /// <summary>Every animation of the Hallowed Summoner, plus its flame. Filled in by the setup tool.</summary>
    [CreateAssetMenu(menuName = "Hollow/Boss Sprites", fileName = "BossSprites")]
    public class BossSprites : ScriptableObject
    {
        [Header("Frames 0-5")] public SpriteClip idle = new SpriteClip { fps = 8f, loop = true };
        [Header("Frames 6-10")] public SpriteClip notice = new SpriteClip { fps = 8f };
        [Header("Frames 11-12")] public SpriteClip throwCharge = new SpriteClip { fps = 4f };
        [Header("Frames 13-15")] public SpriteClip throwRelease = new SpriteClip { fps = 14f };
        [Header("Frame 16")] public SpriteClip throwRecover = new SpriteClip { fps = 4f };
        [Header("Frames 17-20")] public SpriteClip rise = new SpriteClip { fps = 10f };
        [Header("Frames 21-26")] public SpriteClip slamCharge = new SpriteClip { fps = 8f };
        [Header("Frames 27-30")] public SpriteClip slam = new SpriteClip { fps = 14f };

        [Header("Projectile")] public Sprite flame;
    }
}
