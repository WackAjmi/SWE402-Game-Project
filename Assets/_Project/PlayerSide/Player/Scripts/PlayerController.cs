using Hollow.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hollow.PlayerSide
{
    /// <summary>
    /// Hollow Knight-style movement and nail for the Knight.
    /// Run, variable-height jump with coyote time and jump buffering,
    /// facing, side/up/down slash with pogo and recoil, and code-driven animation.
    /// Dash, double jump, health and Focus come in later milestones.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        enum SlashDir { Side, Up, Down }

        [Header("References")]
        [SerializeField] Animator animator;            // the Animator on Visual
        [SerializeField] Transform groundCheck;        // empty object at the feet
        [SerializeField] LayerMask groundLayers;       // defaults to "Ground"
        [SerializeField] GameObject slashSide;
        [SerializeField] GameObject slashUp;
        [SerializeField] GameObject slashDown;

        [Header("Run")]
        [SerializeField] float runSpeed = 8.3f;

        [Header("Jump (Hollow Knight style)")]
        [SerializeField] float jumpSpeed = 16.65f;
        [Tooltip("Physics steps of full upward speed while Jump is held (0.02 s each).")]
        [SerializeField] int jumpStepsMax = 9;
        [Tooltip("A quick tap still rises for this many steps.")]
        [SerializeField] int jumpStepsMin = 4;
        [Tooltip("Tune this last, against gameplay footage.")]
        [SerializeField] float gravityScale = 6f;
        [SerializeField] float maxFallSpeed = 20.9f;
        [SerializeField] float coyoteTime = 0.1f;
        [SerializeField] float jumpBuffer = 0.12f;

        [Header("Nail")]
        [Tooltip("How long the slash animation locks facing.")]
        [SerializeField] float attackDuration = 0.35f;
        [Tooltip("Minimum time between swings.")]
        [SerializeField] float attackCooldown = 0.41f;
        [Tooltip("How long the hitbox (and its effect) stays switched on.")]
        [SerializeField] float hitboxActiveTime = 0.15f;
        [SerializeField] float attackBuffer = 0.1f;
        [SerializeField] float pogoSpeed = 13f;
        [SerializeField] float recoilSpeed = 6f;
        [SerializeField] float recoilTime = 0.08f;

        [Header("Animator state names")]
        [SerializeField] string idleState = "Idle";
        [SerializeField] string runState = "Run";
        [SerializeField] string airState = "Airborne";      // falls back to Idle until the clip exists
        [SerializeField] string slashState = "Slash";
        [SerializeField] string upSlashState = "UpSlash";   // falls back to Slash until the clip exists
        [SerializeField] string downSlashState = "DownSlash";

        static readonly Vector2 GroundCheckSize = new Vector2(0.4f, 0.1f);

        Rigidbody2D rb;
        InputAction moveAction, jumpAction, attackAction;
        Hitbox sideHitbox, downHitbox;

        Vector2 moveInput;
        bool jumpHeld;
        int facing = -1;                 // -1 = left (the art's default), +1 = right
        bool grounded;
        bool jumping;
        bool canCutJump;
        int jumpSteps;

        float lastGroundedTime = -99f;
        float lastJumpPressedTime = -99f;
        float lastAttackPressedTime = -99f;
        float attackEndTime, nextAttackTime, hitboxOffTime, recoilEndTime;

        GameObject activeHitbox;
        string currentState;

        public bool Grounded => grounded;
        public int Facing => facing;
        public bool IsAttacking => Time.time < attackEndTime;

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.gravityScale = gravityScale;

            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (groundLayers.value == 0) groundLayers = LayerMask.GetMask("Ground");

            // No friction, so the Knight slides down walls instead of sticking to them.
            var body = GetComponent<CapsuleCollider2D>();
            if (body != null)
                body.sharedMaterial = new PhysicsMaterial2D("KnightNoFriction") { friction = 0f, bounciness = 0f };

            var actions = InputSystem.actions;
            if (actions == null)
            {
                Debug.LogError("No project-wide input actions. Set PlayerControls in Project Settings > Input System Package.");
                enabled = false;
                return;
            }
            moveAction = actions.FindAction("Player/Move", true);
            jumpAction = actions.FindAction("Player/Jump", true);
            attackAction = actions.FindAction("Player/Attack", true);

            foreach (var hb in new[] { slashSide, slashUp, slashDown })
                if (hb != null) hb.SetActive(false);

            if (slashSide != null && slashSide.TryGetComponent(out sideHitbox)) sideHitbox.OnHit += OnSideSlashHit;
            if (slashDown != null && slashDown.TryGetComponent(out downHitbox)) downHitbox.OnHit += OnDownSlashHit;

            Services.Player = transform;
        }

        void OnDestroy()
        {
            if (sideHitbox != null) sideHitbox.OnHit -= OnSideSlashHit;
            if (downHitbox != null) downHitbox.OnHit -= OnDownSlashHit;
            if (Services.Player == transform) Services.Player = null;
        }

        // ---------- Input and per-frame logic ----------

        void Update()
        {
            moveInput = moveAction.ReadValue<Vector2>();
            jumpHeld = jumpAction.IsPressed();
            if (jumpAction.WasPressedThisFrame()) lastJumpPressedTime = Time.time;
            if (attackAction.WasPressedThisFrame()) lastAttackPressedTime = Time.time;

            TryAttack();

            if (activeHitbox != null && Time.time >= hitboxOffTime)
            {
                activeHitbox.SetActive(false);
                activeHitbox = null;
            }

            UpdateAnimation();
        }

        // ---------- Physics ----------

        void FixedUpdate()
        {
            Vector2 v = rb.linearVelocity;

            grounded = groundCheck != null &&
                       Physics2D.OverlapBox(groundCheck.position, GroundCheckSize, 0f, groundLayers) != null &&
                       v.y <= 0.01f;
            if (grounded)
            {
                lastGroundedTime = Time.time;
                canCutJump = false;
            }

            // Run: Hollow Knight has no acceleration, the Knight is at full speed instantly.
            float x = Mathf.Abs(moveInput.x) > 0.3f ? Mathf.Sign(moveInput.x) : 0f;
            if (x != 0f && !IsAttacking) SetFacing((int)x);
            v.x = x * runSpeed;
            if (Time.time < recoilEndTime) v.x = -facing * recoilSpeed;

            // Start a jump: buffered press + on the ground (or just left it).
            bool jumpBuffered = Time.time - lastJumpPressedTime <= jumpBuffer;
            bool onGroundRecently = Time.time - lastGroundedTime <= coyoteTime;
            if (!jumping && jumpBuffered && onGroundRecently)
            {
                jumping = true;
                canCutJump = true;
                jumpSteps = 0;
                lastJumpPressedTime = -99f;
                lastGroundedTime = -99f;
            }

            // Hold phase: constant upward speed for a number of steps.
            if (jumping)
            {
                if (jumpSteps < jumpStepsMin || (jumpHeld && jumpSteps < jumpStepsMax))
                {
                    v.y = jumpSpeed;
                    jumpSteps++;
                }
                else
                {
                    jumping = false;
                }
            }

            // Releasing Jump while still rising stops the rise at once.
            if (!jumping && canCutJump && !jumpHeld && v.y > 0f)
            {
                v.y = 0f;
                canCutJump = false;
            }

            v.y = Mathf.Max(v.y, -maxFallSpeed);
            rb.linearVelocity = v;
        }

        // ---------- Nail ----------

        void TryAttack()
        {
            if (Time.time - lastAttackPressedTime > attackBuffer) return;
            if (Time.time < nextAttackTime) return;
            lastAttackPressedTime = -99f;

            SlashDir dir = SlashDir.Side;
            if (moveInput.y > 0.5f) dir = SlashDir.Up;
            else if (moveInput.y < -0.5f && !grounded) dir = SlashDir.Down;

            GameObject hb = dir == SlashDir.Up ? slashUp : dir == SlashDir.Down ? slashDown : slashSide;
            if (hb == null) { dir = SlashDir.Side; hb = slashSide; }
            if (hb == null) return;

            if (activeHitbox != null) activeHitbox.SetActive(false);
            hb.SetActive(false);   // off then on: clears who was already hit and restarts the effect
            hb.SetActive(true);
            activeHitbox = hb;

            hitboxOffTime = Time.time + hitboxActiveTime;
            attackEndTime = Time.time + attackDuration;
            nextAttackTime = Time.time + attackCooldown;

            string state = dir == SlashDir.Up ? upSlashState : dir == SlashDir.Down ? downSlashState : slashState;
            PlayAnim(state, restart: true);
        }

        void OnSideSlashHit(Hurtbox hurt, DamageInfo info)
        {
            // Small push back when the nail connects, like in Hollow Knight.
            if (hurt.Owner != null) recoilEndTime = Time.time + recoilTime;
        }

        void OnDownSlashHit(Hurtbox hurt, DamageInfo info)
        {
            if (!hurt.Pogoable) return;
            Vector2 v = rb.linearVelocity;
            v.y = pogoSpeed;
            rb.linearVelocity = v;
            jumping = false;
            canCutJump = false;   // a pogo is not cut by releasing Jump
        }

        // ---------- Facing and animation ----------

        void SetFacing(int dir)
        {
            if (dir == facing) return;
            facing = dir;
            Vector3 s = transform.localScale;
            s.x = Mathf.Abs(s.x) * (facing > 0 ? -1f : 1f);   // art faces left, so right = -1
            transform.localScale = s;
        }

        void UpdateAnimation()
        {
            if (IsAttacking) return;
            if (!grounded) PlayAnim(airState);
            else if (Mathf.Abs(moveInput.x) > 0.3f) PlayAnim(runState);
            else PlayAnim(idleState);
        }

        void PlayAnim(string state, bool restart = false)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;

            if (!animator.HasState(0, Animator.StringToHash(state)))
            {
                if (state == airState) state = idleState;
                else if (state == upSlashState || state == downSlashState) state = slashState;
                if (!animator.HasState(0, Animator.StringToHash(state))) return;
            }

            if (!restart && currentState == state) return;
            animator.Play(state, 0, 0f);
            currentState = state;
        }

        void OnDrawGizmosSelected()
        {
            if (groundCheck == null) return;
            Gizmos.color = grounded ? Color.green : Color.red;
            Gizmos.DrawWireCube(groundCheck.position, GroundCheckSize);
        }
    }
}
