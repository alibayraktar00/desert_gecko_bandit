using System;
using UnityEngine;

namespace DesertGecko.Player
{
    /// <summary>
    /// Gila'nın hareketi: koşma, zıplama + çift zıplama (kuyruk itişi), süzülme,
    /// duvara yapışma / tırmanma / duvar zıplaması.
    /// Girdi Update'te okunur, fizik FixedUpdate'te uygulanır.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D), typeof(PlayerInputReader))]
    public class PlayerController : MonoBehaviour
    {
        public enum MoveState { Grounded, Airborne, WallCling, Gliding }

        [Header("Koşma")]
        [SerializeField] float runSpeed = 5.5f;
        [SerializeField] float groundAcceleration = 60f;
        [SerializeField] float groundDeceleration = 75f;
        [SerializeField] float airAcceleration = 40f;
        [SerializeField] float airDeceleration = 25f;

        [Header("Zıplama")]
        [SerializeField] float jumpHeight = 2.8f;
        [SerializeField] float doubleJumpHeight = 2.2f;
        [Tooltip("Kuyruk fırlatılmışken (yeniden çıkana kadar) çift zıplama yüksekliği")]
        [SerializeField] float weakDoubleJumpHeight = 0.9f;
        [SerializeField] float coyoteTime = 0.1f;
        [SerializeField] float jumpBufferTime = 0.12f;

        [Header("Yerçekimi")]
        [SerializeField] float baseGravityScale = 3.5f;
        [SerializeField] float fallGravityMultiplier = 1.6f;
        [Tooltip("Zıplama tuşu erken bırakılınca yükselişi kesen çarpan")]
        [SerializeField] float jumpCutGravityMultiplier = 2.4f;
        [SerializeField] float maxFallSpeed = 14f;

        [Header("Süzülme")]
        [SerializeField] float glideFallSpeed = 1.8f;
        [SerializeField] float glideHorizontalSpeed = 4.5f;

        [Header("Duvar")]
        [SerializeField] float wallClimbSpeed = 2.5f;
        [Tooltip("Yön tuşuna basılmıyorken duvardan kayma hızı (0 = tam yapışır)")]
        [SerializeField] float wallSlideSpeed = 0f;
        [SerializeField] Vector2 wallJumpVelocity = new Vector2(7f, 13f);
        [SerializeField] float wallJumpControlLock = 0.18f;
        [Tooltip("Duvarın tepesine tırmanınca kenara çıkmak için verilen itiş")]
        [SerializeField] Vector2 ledgeHopVelocity = new Vector2(3f, 7f);
        [SerializeField] float wallRegrabDelay = 0.15f;

        [Header("Algılama")]
        [SerializeField] LayerMask groundMask;
        [SerializeField] float probeDepth = 0.06f;

        /// <summary>Sıcaklık sistemi bunu ayarlar (sıcakta &gt; 1, soğukta &lt; 1).</summary>
        public float SpeedMultiplier { get; set; } = 1f;

        /// <summary>Kuyruk fırlatılınca false olur. Kuyruk yokken çift zıplama zayıflar ve süzülme olmaz.</summary>
        public bool HasTail { get; set; } = true;

        /// <summary>Saldırı gibi durumlarda hareketi geçici olarak kilitlemek için.</summary>
        public bool MovementLocked { get; set; }

        public MoveState State { get; private set; } = MoveState.Airborne;
        public int FacingDirection { get; private set; } = 1;
        public Vector2 Velocity => body.linearVelocity;
        public bool IsGrounded => State == MoveState.Grounded;

        public event Action Jumped;
        public event Action DoubleJumped;
        public event Action WallJumped;
        public event Action Landed;

        Rigidbody2D body;
        BoxCollider2D box;
        PlayerInputReader input;

        bool groundContact;
        int wallContact;          // -1 sol, 1 sağ, 0 yok
        float lastGroundedTime = float.NegativeInfinity;
        float lastJumpPressTime = float.NegativeInfinity;
        float controlLockUntil;
        float wallRegrabAllowedAt;
        int airJumpsLeft;
        bool jumpCut;
        bool rising;

        readonly Collider2D[] probeHits = new Collider2D[4];

        void Reset()
        {
            groundMask = LayerMask.GetMask("Ground");
        }

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            box = GetComponent<BoxCollider2D>();
            input = GetComponent<PlayerInputReader>();
            if (groundMask == 0)
                groundMask = LayerMask.GetMask("Ground"); // koddan eklenince (testler) Reset çalışmaz

            body.gravityScale = baseGravityScale;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            // Uyuyan gövde, hareket eden trigger'ları (düşen kristal, süzülen böcek) fark etmez.
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;
        }

        void Update()
        {
            if (input.ConsumeJump())
                lastJumpPressTime = Time.time;

            float x = input.Move.x;
            if (State != MoveState.WallCling && !MovementLocked && Mathf.Abs(x) > 0.1f && Time.time >= controlLockUntil)
                FacingDirection = x > 0 ? 1 : -1;
        }

        void FixedUpdate()
        {
            ProbeSurroundings();
            UpdateState();
            HandleJump();
            ApplyHorizontal();
            ApplyVertical();
        }

        void ProbeSurroundings()
        {
            Bounds b = box.bounds;
            var filter = new ContactFilter2D { useLayerMask = true, layerMask = groundMask, useTriggers = false };

            Vector2 groundCenter = new Vector2(b.center.x, b.min.y - probeDepth * 0.5f);
            Vector2 groundSize = new Vector2(b.size.x * 0.9f, probeDepth);
            groundContact = Physics2D.OverlapBox(groundCenter, groundSize, 0f, filter, probeHits) > 0;

            Vector2 sideSize = new Vector2(probeDepth, b.size.y * 0.7f);
            Vector2 rightCenter = new Vector2(b.max.x + probeDepth * 0.5f, b.center.y);
            Vector2 leftCenter = new Vector2(b.min.x - probeDepth * 0.5f, b.center.y);
            if (Physics2D.OverlapBox(rightCenter, sideSize, 0f, filter, probeHits) > 0) wallContact = 1;
            else if (Physics2D.OverlapBox(leftCenter, sideSize, 0f, filter, probeHits) > 0) wallContact = -1;
            else wallContact = 0;
        }

        void UpdateState()
        {
            MoveState previous = State;
            Vector2 move = input.Move;
            bool grounded = groundContact && body.linearVelocity.y <= 0.01f;

            if (grounded)
            {
                lastGroundedTime = Time.time;
                airJumpsLeft = 1;
                jumpCut = false;
                rising = false;
            }

            if (previous == MoveState.WallCling)
            {
                if (wallContact == 0)
                {
                    // Duvarın tepesine ulaştı: kenara çıkması için küçük bir itiş.
                    if (move.y > 0.1f)
                        body.linearVelocity = new Vector2(FacingDirection * ledgeHopVelocity.x, ledgeHopVelocity.y);
                    State = MoveState.Airborne;
                }
                else if (grounded && move.y <= 0.1f)
                    State = MoveState.Grounded;
                else if (Mathf.Abs(move.x) > 0.5f && Mathf.Sign(move.x) == -wallContact)
                {
                    // Duvardan uzağa basınca bırak.
                    State = MoveState.Airborne;
                    wallRegrabAllowedAt = Time.time + wallRegrabDelay;
                }
                return;
            }

            bool pushingIntoWall = wallContact != 0 && Mathf.Abs(move.x) > 0.5f && Mathf.Sign(move.x) == wallContact;
            bool canGrab = !MovementLocked && Time.time >= wallRegrabAllowedAt && Time.time >= controlLockUntil;
            if (canGrab && pushingIntoWall && (!grounded || move.y > 0.5f))
            {
                EnterWallCling();
                return;
            }

            if (grounded)
            {
                State = MoveState.Grounded;
                if (previous != MoveState.Grounded)
                    Landed?.Invoke();
                return;
            }

            bool canGlide = HasTail && input.JumpHeld && body.linearVelocity.y < 0f;
            State = canGlide ? MoveState.Gliding : MoveState.Airborne;
        }

        void EnterWallCling()
        {
            State = MoveState.WallCling;
            FacingDirection = wallContact;
            airJumpsLeft = 1;
            jumpCut = false;
            rising = false;
            body.linearVelocity = Vector2.zero;
        }

        void HandleJump()
        {
            if (MovementLocked || Time.time - lastJumpPressTime > jumpBufferTime)
                return;

            if (State == MoveState.WallCling)
            {
                int away = -wallContact;
                body.linearVelocity = new Vector2(away * wallJumpVelocity.x, wallJumpVelocity.y);
                FacingDirection = away;
                controlLockUntil = Time.time + wallJumpControlLock;
                wallRegrabAllowedAt = Time.time + wallRegrabDelay;
                State = MoveState.Airborne;
                StartRise();
                WallJumped?.Invoke();
            }
            else if (State == MoveState.Grounded || Time.time - lastGroundedTime <= coyoteTime)
            {
                SetJumpVelocity(jumpHeight);
                State = MoveState.Airborne;
                lastGroundedTime = float.NegativeInfinity; // coyote ikinci kez kullanılmasın
                StartRise();
                Jumped?.Invoke();
            }
            else if (airJumpsLeft > 0)
            {
                airJumpsLeft--;
                SetJumpVelocity(HasTail ? doubleJumpHeight : weakDoubleJumpHeight);
                State = MoveState.Airborne;
                StartRise();
                DoubleJumped?.Invoke();
            }
            else
            {
                return; // tampon açık kalsın, yere değince zıplasın
            }

            lastJumpPressTime = float.NegativeInfinity;
        }

        void StartRise()
        {
            rising = true;
            jumpCut = false;
        }

        void SetJumpVelocity(float height)
        {
            float gravity = Mathf.Abs(Physics2D.gravity.y) * baseGravityScale;
            body.linearVelocity = new Vector2(body.linearVelocity.x, Mathf.Sqrt(2f * gravity * height));
        }

        void ApplyHorizontal()
        {
            // Duvar zıplaması sonrası kontrol kilidindeyken hızı koru.
            if (State == MoveState.WallCling || Time.time < controlLockUntil)
                return;

            Vector2 v = body.linearVelocity;
            float inputX = MovementLocked ? 0f : input.Move.x;
            float maxSpeed = (State == MoveState.Gliding ? glideHorizontalSpeed : runSpeed) * SpeedMultiplier;
            float target = inputX * maxSpeed;

            bool onGround = State == MoveState.Grounded;
            bool speedingUp = Mathf.Abs(target) > 0.01f && (Mathf.Sign(target) == Mathf.Sign(v.x) || Mathf.Abs(v.x) < 0.01f);
            float rate = onGround
                ? (speedingUp ? groundAcceleration : groundDeceleration)
                : (speedingUp ? airAcceleration : airDeceleration);

            v.x = Mathf.MoveTowards(v.x, target, rate * Time.fixedDeltaTime);
            body.linearVelocity = v;
        }

        void ApplyVertical()
        {
            Vector2 v = body.linearVelocity;

            switch (State)
            {
                case MoveState.WallCling:
                    body.gravityScale = 0f;
                    float climb = MovementLocked ? 0f : input.Move.y;
                    v.x = 0f;
                    v.y = Mathf.Abs(climb) > 0.1f ? climb * wallClimbSpeed * SpeedMultiplier : -wallSlideSpeed;
                    body.linearVelocity = v;
                    return;

                case MoveState.Gliding:
                    body.gravityScale = baseGravityScale * 0.5f;
                    v.y = Mathf.Max(v.y, -glideFallSpeed);
                    body.linearVelocity = v;
                    return;

                case MoveState.Grounded:
                    body.gravityScale = baseGravityScale;
                    return;
            }

            if (rising && !input.JumpHeld && v.y > 0f)
                jumpCut = true;
            if (v.y <= 0f)
                rising = false;

            float scale = baseGravityScale;
            if (v.y < 0f) scale *= fallGravityMultiplier;
            else if (jumpCut) scale *= jumpCutGravityMultiplier;
            body.gravityScale = scale;

            if (v.y < -maxFallSpeed)
            {
                v.y = -maxFallSpeed;
                body.linearVelocity = v;
            }
        }

        public void Teleport(Vector2 position)
        {
            body.position = position;
            transform.position = position;
            body.linearVelocity = Vector2.zero;
            State = MoveState.Airborne;
        }

        /// <summary>Hasar alınca geri itme: hızı doğrudan ayarlar ve kısa süre yön kontrolünü kilitler.</summary>
        public void Knockback(Vector2 velocity, float controlLock)
        {
            body.linearVelocity = velocity;
            controlLockUntil = Time.time + controlLock;
            wallRegrabAllowedAt = Time.time + controlLock;
            State = MoveState.Airborne;
            rising = false;
            jumpCut = false;
        }

        /// <summary>Ölüm gibi durumlarda fiziği tamamen durdurur (düşmez, çarpışmaz).</summary>
        public bool Frozen
        {
            get => !body.simulated;
            set
            {
                body.simulated = !value;
                if (value)
                    body.linearVelocity = Vector2.zero;
            }
        }

        void OnDrawGizmosSelected()
        {
            var col = GetComponent<BoxCollider2D>();
            if (col == null) return;
            Bounds b = col.bounds;
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(new Vector3(b.center.x, b.min.y - probeDepth * 0.5f), new Vector3(b.size.x * 0.9f, probeDepth));
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(new Vector3(b.max.x + probeDepth * 0.5f, b.center.y), new Vector3(probeDepth, b.size.y * 0.7f));
            Gizmos.DrawWireCube(new Vector3(b.min.x - probeDepth * 0.5f, b.center.y), new Vector3(probeDepth, b.size.y * 0.7f));
        }
    }
}
