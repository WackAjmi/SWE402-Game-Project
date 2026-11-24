using System.Collections.Generic;
using Hollow.Core;
using UnityEngine;

namespace Hollow.BossSide.Boss
{
    /// <summary>
    /// The Hallowed Summoner: a floating caster driven by an explicit finite state machine.
    ///
    ///   Dormant -> Intro -> Idle -> (choose attack)
    ///     Flame Throw:  ThrowCharge (floats down) -> ThrowRelease -> ThrowRecover -> Idle (drifts back up)
    ///     Rising Slam:  Rise -> SlamCharge -> SlamDrop -> Slam -> SlamRecover -> Return -> Idle
    ///   Idle -> PhaseShift -> Idle   (once, at half health)
    ///   any  -> Dead                 (health 0)
    ///
    /// Phase 2 shortens every charge-up and changes the slam's flame pattern from fixed
    /// spots with safe gaps to random marked spots. The boss talks to the rest of the game
    /// only through GameEvents (intro finished, health, phase, death).
    ///
    /// Transform origin = the tip of the tail while floating, the floor while slamming.
    /// Sprites face right; the visual is mirrored to face the Knight.
    /// </summary>
    public class HallowedSummoner : MonoBehaviour, IDamageable
    {
        public enum State
        {
            Dormant, Intro, Idle, ThrowCharge, ThrowRelease, ThrowRecover,
            Rise, SlamCharge, SlamDrop, Slam, SlamRecover, Return, PhaseShift, Dead
        }

        enum Attack { None, Throw, Slam }

        [Header("Parts (set by the setup tool)")]
        [SerializeField] BossSprites sprites;
        [SerializeField] SpriteAnimator anim;
        [SerializeField] SpriteRenderer body;
        [SerializeField] BoxCollider2D hurtCollider;
        [SerializeField] Hitbox contactHitbox;
        [SerializeField] BossFlame flameTemplate;
        [SerializeField] SpriteRenderer chargeGlow;
        [SerializeField] SpriteRenderer markerTemplate;

        [Header("Health")]
        [SerializeField] int maxHealth = 40;
        [Tooltip("Phase 2 starts when health drops to this value or below.")]
        [SerializeField] int phase2Health = 20;

        [Header("Arena (world units)")]
        [SerializeField] float arenaMinX = 3f;
        [SerializeField] float arenaMaxX = 43f;
        [SerializeField] float hoverY = 0.5f;
        [SerializeField] float riseY = 7.5f;

        [Header("Intro")]
        [SerializeField] float introWait = 0.8f;
        [SerializeField] float descendTime = 1.6f;

        [Header("Movement")]
        [SerializeField] float driftSpeed = 3.5f;
        [SerializeField] Vector2 preferredDistance = new Vector2(6f, 10f);
        [SerializeField] Vector2 idleTime = new Vector2(0.9f, 1.5f);

        [Header("Flame Throw")]
        [SerializeField] float throwChargeTime = 0.9f;
        [SerializeField] float throwRecoverTime = 0.9f;
        [SerializeField] float flameSpeed = 15f;
        [Tooltip("Size of the thrown flame (1 = the slam's flames).")]
        [SerializeField] float throwFlameSize = 1.8f;
        [Tooltip("Height the thrown flame flies at. The boss lowers itself during the charge so its hand is at this height.")]
        [SerializeField] float throwLaneY = 1.0f;
        [Tooltip("Seconds the boss takes to float down at the start of the charge (never longer than the charge).")]
        [SerializeField] float throwDescendTime = 0.35f;
        [Tooltip("Where the flame appears, relative to the origin, when facing right.")]
        [SerializeField] Vector2 handOffset = new Vector2(2.2f, 2.4f);
        [Tooltip("Where the charging flame glows, relative to the origin, when facing right.")]
        [SerializeField] Vector2 glowOffset = new Vector2(0.9f, 2.3f);
        [SerializeField] float glowScale = 0.3f;

        [Header("Rising Slam")]
        [SerializeField] float riseTime = 0.6f;
        [SerializeField] float slamChargeTime = 1.1f;
        [SerializeField] float dropSpeed = 32f;
        [SerializeField] float slamRecoverTime = 1.0f;
        [SerializeField] float returnTime = 0.35f;
        [SerializeField] float eruptSpeed = 16f;
        [SerializeField] float slamShake = 0.7f;
        [Tooltip("Phase 1: flames on both sides at this spacing (the gaps between them are safe).")]
        [SerializeField] float patternSpacing = 2.6f;
        [SerializeField] int patternPerSide = 4;
        [Tooltip("Phase 2: number of random flame spots (one is always under the Knight).")]
        [SerializeField] int randomCount = 8;
        [SerializeField] float randomMinGap = 2.2f;

        [Header("Phase 2")]
        [Tooltip("Charge-ups take this fraction of their phase 1 time.")]
        [SerializeField, Range(0.3f, 1f)] float phase2ChargeScale = 0.6f;
        [SerializeField, Range(0.3f, 1f)] float phase2IdleScale = 0.75f;

        [Header("Look")]
        [SerializeField] Color hitTint = new Color(0.55f, 0.55f, 0.68f);
        [SerializeField] float hitTintTime = 0.07f;
        [SerializeField] float bobAmount = 0.1f;

        [Header("Debug")]
        [SerializeField] bool logStates = true;

        // ---------- runtime ----------

        State state = State.Dormant;
        float stateTime;
        int introStep;
        float stateDuration;
        Vector2 moveFrom, moveTo;

        int health;
        int phase = 1;
        bool phase2Pending;
        float facing = 1f;
        Attack lastAttack;
        int sameAttackCount;
        float tintUntil;
        float contactCycle;

        readonly List<BossFlame> flames = new List<BossFlame>();
        readonly List<SpriteRenderer> markers = new List<SpriteRenderer>();
        readonly List<float> eruptXs = new List<float>();
        Transform projectileRoot;
        Vector2 hoverHurtSize, hoverHurtOffset;

        public State CurrentState => state;
        public int Health => health;
        public int Phase => phase;
        public Team Team => Team.Boss;
        public bool IsInvulnerable => state == State.Dormant || state == State.Intro ||
                                      state == State.PhaseShift || state == State.Dead;

        // ---------- setup ----------

        void Awake()
        {
            health = maxHealth;
            projectileRoot = new GameObject("Summoner Projectiles").transform;
            if (flameTemplate != null) flameTemplate.gameObject.SetActive(false);
            if (markerTemplate != null) markerTemplate.gameObject.SetActive(false);
            if (chargeGlow != null) chargeGlow.enabled = false;
            if (contactHitbox != null) contactHitbox.gameObject.SetActive(false);
            if (hurtCollider != null) { hoverHurtSize = hurtCollider.size; hoverHurtOffset = hurtCollider.offset; }
            if (anim != null && sprites != null) anim.Play(sprites.idle);
        }

        void OnEnable() => GameEvents.EncounterStarted += OnEncounterStarted;
        void OnDisable() => GameEvents.EncounterStarted -= OnEncounterStarted;

        void OnDestroy()
        {
            if (projectileRoot != null) Destroy(projectileRoot.gameObject);
        }

        void OnEncounterStarted()
        {
            if (state == State.Dormant) Enter(State.Intro);
        }

        // ---------- state machine ----------

        void Enter(State next)
        {
            if (logStates) Debug.Log($"[Summoner] {state} -> {next} (HP {health}/{maxHealth}, phase {phase})", this);
            state = next;
            stateTime = 0f;

            switch (next)
            {
                case State.Intro:
                    introStep = 0;
                    anim.Play(sprites.idle);
                    break;

                case State.Idle:
                    anim.Play(sprites.idle, 1f, false);
                    SetGroundedPose(false);
                    stateDuration = Random.Range(idleTime.x, idleTime.y) * (phase == 2 ? phase2IdleScale : 1f);
                    break;

                case State.ThrowCharge:
                    FacePlayer();
                    anim.PlayOver(sprites.throwCharge, ChargeTime(throwChargeTime));
                    stateDuration = ChargeTime(throwChargeTime);
                    anim.transform.localPosition = Vector3.zero;   // no bobbing: the hand must line up with the lane
                    moveFrom = transform.position;
                    moveTo = new Vector2(transform.position.x, throwLaneY - handOffset.y);
                    if (chargeGlow != null) chargeGlow.enabled = true;
                    break;

                case State.ThrowRelease:
                    if (chargeGlow != null) chargeGlow.enabled = false;
                    anim.Play(sprites.throwRelease);
                    ThrowFlame();
                    break;

                case State.ThrowRecover:
                    anim.Play(sprites.throwRecover);
                    stateDuration = throwRecoverTime;
                    break;

                case State.Rise:
                    anim.PlayOver(sprites.rise, riseTime);
                    moveFrom = transform.position;
                    moveTo = new Vector2(transform.position.x, riseY);
                    break;

                case State.SlamCharge:
                    FacePlayer();
                    anim.PlayOver(sprites.slamCharge, ChargeTime(slamChargeTime));
                    stateDuration = ChargeTime(slamChargeTime);
                    PlaceMarkers();
                    break;

                case State.SlamDrop:
                    break;

                case State.Slam:
                    transform.position = new Vector3(transform.position.x, 0f, 0f);
                    SetGroundedPose(true);
                    anim.Play(sprites.slam);
                    Services.Feedback.Shake(Vector2.down, slamShake);
                    Erupt();
                    break;

                case State.SlamRecover:
                    stateDuration = slamRecoverTime;
                    break;

                case State.Return:
                    SetGroundedPose(false);
                    anim.Play(sprites.idle);
                    moveFrom = transform.position;
                    moveTo = new Vector2(transform.position.x, hoverY);
                    break;

                case State.PhaseShift:
                    phase = 2;
                    phase2Pending = false;
                    anim.Play(sprites.notice);
                    stateDuration = Mathf.Max(1f, sprites.notice.Duration + 0.3f);
                    Services.Feedback.Shake(Vector2.up, 0.4f);
                    GameEvents.BossPhaseChanged?.Invoke(2);
                    GameEvents.BossRoared?.Invoke();
                    break;

                case State.Dead:
                    Die();
                    break;
            }
        }

        void Update()
        {
            stateTime += Time.deltaTime;

            switch (state)
            {
                case State.Dormant:
                    Bob();
                    break;

                case State.Intro: UpdateIntro(); break;

                case State.Idle:
                    FacePlayer();
                    Drift();
                    Bob();
                    if (stateTime >= stateDuration)
                    {
                        if (phase2Pending) Enter(State.PhaseShift);
                        else ChooseAttack();
                    }
                    break;

                case State.ThrowCharge:
                    MoveOver(Mathf.Min(throwDescendTime, stateDuration), true);
                    UpdateGlow();
                    if (stateTime >= stateDuration) Enter(State.ThrowRelease);
                    break;

                case State.ThrowRelease:
                    if (anim.IsDone) Enter(State.ThrowRecover);
                    break;

                case State.ThrowRecover:
                    Bob();
                    if (stateTime >= stateDuration) Enter(State.Idle);
                    break;

                case State.Rise:
                    MoveOver(riseTime, true);
                    if (stateTime >= riseTime) Enter(State.SlamCharge);
                    break;

                case State.SlamCharge:
                    Bob();
                    PulseMarkers();
                    if (stateTime >= stateDuration) Enter(State.SlamDrop);
                    break;

                case State.SlamDrop:
                {
                    PulseMarkers();
                    float y = transform.position.y - dropSpeed * Time.deltaTime;
                    if (y <= 0f) Enter(State.Slam);
                    else transform.position = new Vector3(transform.position.x, y, 0f);
                    break;
                }

                case State.Slam:
                    if (anim.IsDone) Enter(State.SlamRecover);
                    break;

                case State.SlamRecover:
                    if (stateTime >= stateDuration) Enter(State.Return);
                    break;

                case State.Return:
                    MoveOver(returnTime, true);
                    if (stateTime >= returnTime) Enter(State.Idle);
                    break;

                case State.PhaseShift:
                    if (stateTime >= stateDuration) Enter(State.Idle);
                    break;

                case State.Dead:
                    UpdateDeath();
                    break;
            }

            UpdateContactHitbox();
            UpdateTint();
        }

        void UpdateIntro()
        {
            switch (introStep)
            {
                case 0:   // floating, hasn't noticed the Knight yet
                    Bob();
                    if (stateTime >= introWait) { introStep = 1; stateTime = 0f; FacePlayer(); anim.Play(sprites.notice); }
                    break;
                case 1:   // notices
                    if (anim.IsDone) { introStep = 2; stateTime = 0f; anim.Play(sprites.idle); moveFrom = transform.position; moveTo = new Vector2(transform.position.x, hoverY); }
                    break;
                case 2:   // lowers itself
                    MoveOver(descendTime, true);
                    if (stateTime >= descendTime)
                    {
                        GameEvents.BossIntroFinished?.Invoke();
                        GameEvents.BossHealthChanged?.Invoke(health, maxHealth);
                        GameEvents.BossPhaseChanged?.Invoke(1);
                        Enter(State.Idle);
                    }
                    break;
            }
        }

        void ChooseAttack()
        {
            Transform player = Services.Player;
            float distance = player != null ? Mathf.Abs(player.position.x - transform.position.x) : 8f;
            float throwWeight = distance > 7f ? 0.65f : 0.35f;
            Attack pick = Random.value < throwWeight ? Attack.Throw : Attack.Slam;

            // Never the same attack more than twice in a row.
            if (pick == lastAttack && sameAttackCount >= 2) pick = pick == Attack.Throw ? Attack.Slam : Attack.Throw;
            sameAttackCount = pick == lastAttack ? sameAttackCount + 1 : 1;
            lastAttack = pick;

            Enter(pick == Attack.Throw ? State.ThrowCharge : State.Rise);
        }

        float ChargeTime(float phase1Time) => phase == 2 ? phase1Time * phase2ChargeScale : phase1Time;

        // ---------- movement ----------

        void Drift()
        {
            Transform player = Services.Player;
            if (player == null) return;
            float px = player.position.x;
            float side = Mathf.Sign(transform.position.x - px);
            if (side == 0f) side = 1f;
            float wanted = px + side * Mathf.Clamp(Mathf.Abs(transform.position.x - px), preferredDistance.x, preferredDistance.y);
            if (wanted < arenaMinX || wanted > arenaMaxX)   // cornered: go to the other side of the Knight
                wanted = px - side * preferredDistance.x;
            wanted = Mathf.Clamp(wanted, arenaMinX, arenaMaxX);

            Vector3 p = transform.position;
            p.x = Mathf.MoveTowards(p.x, wanted, driftSpeed * Time.deltaTime);
            p.y = Mathf.MoveTowards(p.y, hoverY, driftSpeed * Time.deltaTime);
            transform.position = p;
        }

        void MoveOver(float seconds, bool smooth)
        {
            float t = seconds > 0f ? Mathf.Clamp01(stateTime / seconds) : 1f;
            if (smooth) t = t * t * (3f - 2f * t);
            transform.position = Vector2.Lerp(moveFrom, moveTo, t);
        }

        void Bob()
        {
            if (anim == null) return;
            anim.transform.localPosition = new Vector3(0f, Mathf.Sin(Time.time * 2.2f) * bobAmount, 0f);
        }

        void FacePlayer()
        {
            Transform player = Services.Player;
            if (player == null) return;
            float dx = player.position.x - transform.position.x;
            if (Mathf.Abs(dx) > 0.2f) facing = Mathf.Sign(dx);
            if (body != null) body.flipX = facing < 0f;
        }

        Vector2 Mirrored(Vector2 offset) => (Vector2)transform.position + new Vector2(offset.x * facing, offset.y);

        /// <summary>Floating and slamming poses need different hurtboxes (origin moves from tail tip to floor).</summary>
        void SetGroundedPose(bool grounded)
        {
            if (anim != null) anim.transform.localPosition = Vector3.zero;
            if (hurtCollider == null) return;
            if (grounded)
            {
                hurtCollider.size = new Vector2(2.4f, 2.6f);
                hurtCollider.offset = new Vector2(0f, 1.3f);
            }
            else
            {
                hurtCollider.size = hoverHurtSize;
                hurtCollider.offset = hoverHurtOffset;
            }
            if (contactHitbox != null && contactHitbox.TryGetComponent(out BoxCollider2D contact))
            {
                contact.size = hurtCollider.size * 0.75f;
                contact.offset = hurtCollider.offset;
            }
        }

        // ---------- Flame Throw ----------

        void UpdateGlow()
        {
            if (chargeGlow == null) return;
            float k = stateDuration > 0f ? Mathf.Clamp01(stateTime / stateDuration) : 1f;
            chargeGlow.transform.position = Mirrored(glowOffset);
            chargeGlow.transform.rotation = Quaternion.Euler(0f, 0f, facing > 0f ? 0f : 180f);
            float pulse = 1f + Mathf.Sin(Time.time * 30f) * 0.08f;
            chargeGlow.transform.localScale = Vector3.one * glowScale * Mathf.Lerp(0.2f, 1f, k) * pulse;
            chargeGlow.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.3f, 1f, k));
        }

        void ThrowFlame()
        {
            // The boss has floated down so its hand is at the lane height: the flame flies straight
            // sideways toward the Knight's side (facing was locked when the charge began). Jump over it.
            Vector2 from = Mirrored(handOffset);
            GetFlame()?.Launch(from, new Vector2(facing * flameSpeed, 0f), 4f, 0f, throwFlameSize);
        }

        // ---------- Rising Slam ----------

        void PlaceMarkers()
        {
            eruptXs.Clear();
            float bx = transform.position.x;

            if (phase == 1)
            {
                // Pattern A: fixed spots on both sides; the gaps between them are safe.
                for (int k = 1; k <= patternPerSide; k++)
                {
                    TryAddSpot(bx - k * patternSpacing);
                    TryAddSpot(bx + k * patternSpacing);
                }
            }
            else
            {
                // Pattern C: random spots, one always under the Knight, each marked first.
                Transform player = Services.Player;
                if (player != null) TryAddSpot(player.position.x);
                int guard = 0;
                while (eruptXs.Count < randomCount && guard++ < 200)
                {
                    float x = Random.Range(arenaMinX - 1.5f, arenaMaxX + 1.5f);
                    bool clear = true;
                    foreach (float e in eruptXs) if (Mathf.Abs(e - x) < randomMinGap) { clear = false; break; }
                    if (clear) TryAddSpot(x);
                }
            }

            for (int i = 0; i < eruptXs.Count; i++)
            {
                var m = GetMarker(i);
                if (m == null) continue;
                m.transform.position = new Vector3(eruptXs[i], 0.3f, 0f);
                m.gameObject.SetActive(true);
            }
        }

        void TryAddSpot(float x)
        {
            if (x < arenaMinX - 1.5f || x > arenaMaxX + 1.5f) return;
            eruptXs.Add(x);
        }

        void PulseMarkers()
        {
            float pulse = 0.55f + 0.45f * Mathf.Sin(Time.time * 14f);
            foreach (var m in markers)
            {
                if (!m.gameObject.activeSelf) continue;
                var c = m.color; c.a = pulse; m.color = c;
            }
        }

        void HideMarkers()
        {
            foreach (var m in markers) m.gameObject.SetActive(false);
        }

        void Erupt()
        {
            HideMarkers();
            foreach (float x in eruptXs)
                GetFlame()?.Launch(new Vector2(x, 0.35f), new Vector2(0f, eruptSpeed), 2.5f, 0.1f);
            eruptXs.Clear();
        }

        // ---------- pools ----------

        BossFlame GetFlame()
        {
            if (flameTemplate == null) return null;
            foreach (var f in flames) if (!f.Active) return f;
            var copy = Instantiate(flameTemplate, projectileRoot);
            copy.name = "Flame";
            flames.Add(copy);
            return copy;
        }

        SpriteRenderer GetMarker(int index)
        {
            if (markerTemplate == null) return null;
            while (markers.Count <= index)
            {
                var copy = Instantiate(markerTemplate, projectileRoot);
                copy.name = "Flame Marker";
                markers.Add(copy);
            }
            return markers[index];
        }

        void ClearProjectiles()
        {
            foreach (var f in flames) f.Despawn();
            HideMarkers();
            eruptXs.Clear();
            if (chargeGlow != null) chargeGlow.enabled = false;
        }

        // ---------- damage ----------

        public void TakeDamage(DamageInfo info)
        {
            if (IsInvulnerable) return;
            health = Mathf.Max(0, health - Mathf.Max(1, info.Amount));
            GameEvents.BossHealthChanged?.Invoke(health, maxHealth);
            tintUntil = Time.time + hitTintTime;
            if (body != null) body.color = hitTint;

            if (health == 0) { Enter(State.Dead); return; }
            if (phase == 1 && health <= phase2Health) phase2Pending = true;   // takes effect after the current attack
        }

        void UpdateTint()
        {
            if (state == State.Dead || body == null || tintUntil <= 0f || Time.time < tintUntil) return;
            tintUntil = 0f;
            body.color = Color.white;
        }

        void UpdateContactHitbox()
        {
            if (contactHitbox == null) return;
            bool armed = state != State.Dormant && state != State.Intro && state != State.Dead;
            if (!armed) { if (contactHitbox.gameObject.activeSelf) contactHitbox.gameObject.SetActive(false); return; }

            // Re-arm now and then, so staying inside the boss keeps hurting (the Knight's i-frames still apply).
            contactCycle += Time.deltaTime;
            if (!contactHitbox.gameObject.activeSelf || contactCycle >= 0.5f)
            {
                contactCycle = 0f;
                contactHitbox.gameObject.SetActive(false);
                contactHitbox.gameObject.SetActive(true);
            }
        }

        // ---------- death ----------

        void Die()
        {
            ClearProjectiles();
            if (hurtCollider != null) hurtCollider.enabled = false;
            if (contactHitbox != null) contactHitbox.gameObject.SetActive(false);
            moveFrom = transform.position;
            if (body != null) body.color = Color.white;
            Services.Feedback.Shake(Vector2.down, 0.6f);
            GameEvents.BossDied?.Invoke();
        }

        void UpdateDeath()
        {
            // Freeze for a moment, then sink to the floor and fade away.
            if (stateTime < 0.4f || body == null) return;
            float t = Mathf.Clamp01((stateTime - 0.4f) / 1.6f);
            float y = Mathf.Lerp(moveFrom.y, 0f, t * t);
            transform.position = new Vector3(transform.position.x, y, 0f);
            body.color = new Color(1f, 1f, 1f, 1f - t);
            if (t >= 1f) gameObject.SetActive(false);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.5f, 0.2f, 0.8f);
            Gizmos.DrawLine(new Vector3(arenaMinX, hoverY), new Vector3(arenaMaxX, hoverY));
            Gizmos.DrawLine(new Vector3(arenaMinX, riseY), new Vector3(arenaMaxX, riseY));
            Gizmos.DrawWireSphere(Mirrored(handOffset), 0.2f);
        }
    }
}
