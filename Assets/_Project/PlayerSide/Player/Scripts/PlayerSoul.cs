using Hollow.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hollow.PlayerSide
{
    /// <summary>
    /// Soul economy and the Focus heal.
    /// Nail hits on enemies fill the soul meter. Pressing Focus spends soul and restores a mask
    /// instantly, on the ground or in the air, and flashes the Knight green.
    /// </summary>
    [RequireComponent(typeof(PlayerController), typeof(PlayerHealth))]
    public class PlayerSoul : MonoBehaviour
    {
        [Header("Soul")]
        [SerializeField] int maxSoul = 99;
        [SerializeField] int soulPerHit = 11;
        [Tooltip("The Knight's nail hitboxes. Left empty, it finds every Player hitbox under the Knight.")]
        [SerializeField] Hitbox[] nailHitboxes;

        [Header("Focus heal")]
        [SerializeField] int focusCost = 33;
        [SerializeField] int healAmount = 1;
        [Tooltip("How long the green flash lasts after a heal.")]
        [SerializeField] float flashTime = 1f;
        [SerializeField] Color[] focusColors = { new Color(0.45f, 1f, 0.45f), Color.white };
        [SerializeField] float focusFlashStep = 0.1f;

        PlayerController controller;
        PlayerHealth health;
        SpriteFlasher flasher;
        InputAction focusAction;
        int soul;

        public int Soul => soul;
        public int MaxSoul => maxSoul;

        /// <summary>Fired when a Focus heal happens (KnightAudio plays the heal sound).</summary>
        public event System.Action Focused;

        void Awake()
        {
            controller = GetComponent<PlayerController>();
            health = GetComponent<PlayerHealth>();
            flasher = GetComponent<SpriteFlasher>();

            focusAction = InputSystem.actions != null ? InputSystem.actions.FindAction("Player/Focus", false) : null;
            if (focusAction == null) Debug.LogWarning("No 'Focus' action in PlayerControls, so the Focus heal is off.");

            if (nailHitboxes == null || nailHitboxes.Length == 0)
                nailHitboxes = GetComponentsInChildren<Hitbox>(true);
        }

        void OnEnable()
        {
            foreach (var hb in nailHitboxes)
                if (hb != null && hb.Team == Team.Player) hb.OnHit += OnNailHit;
        }

        void OnDisable()
        {
            foreach (var hb in nailHitboxes)
                if (hb != null) hb.OnHit -= OnNailHit;
        }

        void Start() => Broadcast();

        void Update()
        {
            if (focusAction != null && focusAction.WasPressedThisFrame()) TryFocus();
        }

        /// <summary>Instant heal: needs enough soul and at least one missing mask.</summary>
        public bool TryFocus()
        {
            if (health.IsDead || controller.IsDead) return false;
            if (soul < focusCost || health.Masks >= health.MaxMasks) return false;

            soul -= focusCost;
            health.Heal(healAmount);
            Broadcast();
            if (flasher != null) flasher.Flash(focusColors, flashTime, focusFlashStep);
            Focused?.Invoke();
            return true;
        }

        void OnNailHit(Hurtbox hurt, DamageInfo info)
        {
            if (hurt.Owner == null) return;   // pogo-only things (projectiles) give no soul
            AddSoul(soulPerHit);
        }

        public void AddSoul(int amount)
        {
            int before = soul;
            soul = Mathf.Clamp(soul + amount, 0, maxSoul);
            if (soul != before) Broadcast();
        }

        void Broadcast() => GameEvents.PlayerFocusChanged?.Invoke(maxSoul > 0 ? (float)soul / maxSoul : 0f);
    }
}
