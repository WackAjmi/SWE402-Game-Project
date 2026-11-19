using Hollow.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hollow.PlayerSide
{
    /// <summary>
    /// Hollow Knight-style movement and nail for the Knight.
    /// Run, variable-height jump with coyote time and buffering, double jump,
    /// dash with brief invulnerability, side/up/down slash with pogo and recoil,
    /// and code-driven animation. Health and Focus come in later steps.
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

        [Header("Double jump")]
        [SerializeField] bool doubleJumpUnlocked = true;
        [SerializeField] int doubleJumpStepsMax = 8;

        [Header("Dash")]
        [SerializeField] bool dashUnlocked = true;
        [SerializeField] float dashSpeed = 20f;
        [SerializeField] float dashTime = 0.25f;
        [Tooltip("Measured from the start of a dash.")]
        [SerializeField] float dashCooldown = 0.6f;
        [Tooltip("Invulnerable for this long from the start of a dash.")]
        [SerializeField] float dashInvulnerableTime = 0.15f;
        [SerializeField] float dashBuffer = 0.1f;

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

        [Header("Animator state names (missing ones fall back)")]
        [SerializeField] string idleState = "Idle";
        [SerializeField] string runState = "Run";
        [SerializeField] string airState = "Airborne";        // -> Idle
        [SerializeField] string doubleJumpState = "DoubleJump"; // -> Airborne -> Idle
        [SerializeField] string dashState = "Dash";            // -> Airborne -> Idle
        [SerializeField] string slashState = "Slash";
        [SerializeField] string upSlashState = "UpSlash";      // -> Slash
        [SerializeField] string downSlashState = "DownSlash";  // -> Slash

        static readonly Vector2 GroundCheckSize = new Vector2(0.4f, 0.1f);

        Rigidbody2D rb;
        InputAction moveAction, jumpAction, attackAction, dashAction;
        Hitbox sideHitbox, downHitbox;

        Vector2 moveInput;
        bool jumpHeld;
        int facing = -1;                 // -1 = left (the art's default), +1 = right
        bool grounded;

        bool jumping;
        bool canCutJump;
        int jumpSteps;
        int currentJumpStepsMax;
        bool doubleJumpAvailable;
        bool doubleJumpRising;

        bool dashing;
        int dashDir;
        bool airDashAvailable;
        float dashEndTime, nextDashTime, invulnerableUntil;

        float lastGroundedTime = -99f;
        float lastJumpPressedTime = -99f;
        float lastAttackPressedTime = -99f;
        float lastDashPressedTime = -99f;
        float attackEndTime, nextAttackTime, hitboxOffTime, recoilEndTime;

        GameObject activeHitbox;
        string currentState;

        public bool Grounded => grounded;
        public int Facing => facing;
        public bool IsAttacking => Time.time < attackEndTime;
        public bool IsDashing => dashing;
        /// <summary>True during the dash's invulnerable window. PlayerHealth will read this.</summary>
        public bool IsDashInvulnerable => Time.time < invulnerableUntil;

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
            dashAction = actions.FindAction("Player/Dash", false);
            if (dashAction == null) Debug.LogWarning("No 'Dash' action in PlayerControls, so dashing is off.");

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
            if (dashAction != null && dashAction.WasPressedThisFrame()) lastDashPressedTime = Time.time;

            TryAttack();

            if (activeHitbox != null && Time.time >= hitboxOffTime) EndHitbox();

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
                doubleJumpAvailable = true;
                airDashAvailable = true;
                doubleJumpRising = false;
            }

            float x = Mathf.Abs(moveInput.x) > 0.3f ? Mathf.Sign(moveInput.x) : 0f;

            TryStartDash(x);

            if (dashing)
            {
                if (Time.time >= dashEndTime) EndDash();
                else
                {
                    // Straight line at full speed, no gravity. A jump can still cancel a ground dash below.
                    v = new Vector2(dashDir * dashSpeed, 0f);
                }
            }

            if (!dashing)
            {
                // Run: Hollow Knight has no acceleration, the Knight is at full speed instantly.
                if (x != 0f && !IsAttacking) SetFacing((int)x);
                v.x = x * runSpeed;
                if (Time.time < recoilEndTime) v.x = -facing * recoilSpeed;
            }

            // Start a jump: ground jump (with coyote time) first, otherwise the double jump.
            bool jumpBuffered = Time.time - lastJumpPressedTime <= jumpBuffer;
            bool onGroundRecently = Time.time - lastGroundedTime <= coyoteTime;
            if (!jumping && jumpBuffered)
            {
                if (onGroundRecently)
                {
                    StartJump(jumpStepsMax);
                    lastGroundedTime = -99f;
                }
                else if (doubleJumpUnlocked && doubleJumpAvailable && !dashing)
                {
                    StartJump(doubleJumpStepsMax);
                    doubleJumpAvailable = false;
                    doubleJumpRising = true;
                    PlayAnim(doubleJumpState, restart: true);
                }
            }

            if (jumping && dashing) EndDash();   // jumping out of a ground dash

            // Hold phase: constant upward speed for a number of steps.
            if (jumping)
            {
                if (jumpSteps < jumpStepsMin || (jumpHeld && jumpSteps < currentJumpStepsMax))
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

            if (v.y <= 0f) doubleJumpRising = false;

            v.y = Mathf.Max(v.y, -maxFallSpeed);
            rb.linearVelocity = v;
        }

        void StartJump(int maxSteps)
        {
            jumping = true;
            canCutJump = true;
            jumpSteps = 0;
            currentJumpStepsMax = maxSteps;
            lastJumpPressedTime = -99f;
        }

        // ---------- Dash ----------

        void TryStartDash(float inputX)
        {
            if (!dashUnlocked || dashAction == null || dashing) return;
            if (Time.time - lastDashPressedTime > dashBuffer) return;
            if (Time.time < nextDashTime) return;
            if (!grounded && !airDashAvailable) return;

            lastDashPressedTime = -99f;
            if (!grounded) airDashAvailable = false;

            dashDir = inputX != 0f ? (int)inputX : facing;
            SetFacing(dashDir);

            dashing = true;
            dashEndTime = Time.time + dashTime;
            nextDashTime = Time.time + dashCooldown;
            invulnerableUntil = Time.time + dashInvulnerableTime;
            rb.gravityScale = 0f;

            // A dash interrupts a swing and a rising jump.
            if (activeHitbox != null) EndHitbox();
            attackEndTime = Time.time;
            jumping = false;
            canCutJump = false;
            doubleJumpRising = false;

            PlayAnim(dashState, restart: true);
        }

        void EndDash()
        {
            dashing = false;
            rb.gravityScale = gravityScale;
        }

        // ---------- Nail ----------

        void TryAttack()
        {
            if (dashing) return;
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

        void EndHitbox()
        {
            activeHitbox.SetActive(false);
            activeHitbox = null;
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
            canCutJump = false;        // a pogo is not cut by releasing Jump
            doubleJumpAvailable = true; // like Hollow Knight, a pogo restores the double jump and air dash
            airDashAvailable = true;
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
            if (dashing || IsAttacking) return;
            if (!grounded) PlayAnim(doubleJumpRising ? doubleJumpState : airState);
            else if (Mathf.Abs(moveInput.x) > 0.3f) PlayAnim(runState);
            else PlayAnim(idleState);
        }

        void PlayAnim(string state, bool restart = false)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;

            state = ResolveState(state);
            if (state == null) return;

            if (!restart && currentState == state) return;
            animator.Play(state, 0, 0f);
            currentState = state;
        }

        /// <summary>Uses the closest existing state, so missing clips don't break anything.</summary>
        string ResolveState(string state)
        {
            for (int i = 0; i < 3 && state != null; i++)
            {
                if (animator.HasState(0, Animator.StringToHash(state))) return state;
                if (state == doubleJumpState || state == dashState) state = airState;
                else if (state == airState) state = idleState;
                else if (state == upSlashState || state == downSlashState) state = slashState;
                else state = null;
            }
            return null;
        }

        void OnDrawGizmosSelected()
        {
            if (groundCheck == null) return;
            Gizmos.color = grounded ? Color.green : Color.red;
            Gizmos.DrawWireCube(groundCheck.position, GroundCheckSize);
        }
    }
}
