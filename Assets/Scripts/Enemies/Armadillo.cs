using DesertGecko.Core;
using DesertGecko.Effects;
using UnityEngine;

namespace DesertGecko.Enemies
{
    /// <summary>
    /// Madenci Armadillo: yeraltının zırhlı muhafızı. Gila'yı görünce kazmasını yere vurur (kaçma penceresi),
    /// gülle gibi kapanıp yuvarlanır. Yuvarlanırken zırhına hiçbir şey işlemez, üstünden atlamak gerekir.
    /// Oyuncuyu biraz geçince açılır ve döner: yakındaysa kazmasını indirir, uzaktaysa yeniden yuvarlanır.
    /// Duvara ya da uçurum kenarına çarparsa sersemler: en rahat vurulduğu an budur.
    /// Kareler sağa bakar; sola bakarken EnemyBase sprite'ı aynalar.
    /// </summary>
    public class Armadillo : EnemyBase
    {
        public enum ArmadilloState { Patrol, Idle, Notice, Curl, Roll, Unfold, Swing, Daze, Recover }

        [Header("Algılama")]
        [SerializeField] float sightRange = 8f;
        [SerializeField] float sightHeight = 1.6f;
        [SerializeField] float attackCooldown = 1.2f;

        [Header("Yuvarlanma")]
        [Tooltip("Oyuncunun zıplayacak yer araması için uyarı süresi")]
        [SerializeField] float noticeDuration = 0.6f;
        [SerializeField] float curlDuration = 0.28f;
        [SerializeField] float rollSpeed = 9f;
        [Tooltip("Oyuncuyu bu kadar geçince durup açılır")]
        [SerializeField] float rollOvershoot = 2.5f;
        [Tooltip("Oyuncuyu geçemese de en fazla bu kadar yuvarlanır")]
        [SerializeField] float maxRollDistance = 10f;
        [Tooltip("Açılma süresi: yuvarlanmadan sonra vurulabildiği kısa an")]
        [SerializeField] float unfoldDuration = 0.5f;
        [SerializeField] int rollDamage = 2;
        [SerializeField] float dustInterval = 0.1f;
        [SerializeField] Color dustColor = new Color32(0x8A, 0x62, 0x40, 0xFF);

        [Header("Kazma")]
        [SerializeField] float swingRange = 2f;
        [Tooltip("Kazmayı kaldırıp indirme süresi; ortasında vurur")]
        [SerializeField] float swingDuration = 0.7f;
        [SerializeField] float swingReach = 1.7f;
        [SerializeField] int swingDamage = 1;
        [SerializeField] float swingCooldown = 0.8f;

        [Header("Sersemleme")]
        [SerializeField] float dazeDuration = 2.6f;
        [SerializeField] float recoverDuration = 0.7f;
        [SerializeField] Vector2 crashBounce = new Vector2(3.5f, 5f);
        [SerializeField] float hurtDuration = 0.25f;
        [SerializeField] Color sparkColor = new Color32(0xFF, 0xD8, 0x5A, 0xFF);

        [Header("Devriye molası")]
        [SerializeField] float walkTime = 3.5f;
        [SerializeField] float idleTime = 1.4f;

        [Header("Kareler")]
        [SerializeField] Sprite[] idleFrames;
        [SerializeField] float idleFps = 5f;
        [SerializeField] Sprite[] patrolFrames;
        [SerializeField] float patrolFps = 9f;
        [SerializeField] Sprite[] noticeFrames;
        [SerializeField] Sprite[] curlFrames;
        [SerializeField] Sprite[] rollFrames;
        [SerializeField] float rollFps = 20f;
        [SerializeField] Sprite[] dazeFrames;
        [SerializeField] float dazeFps = 8f;
        [SerializeField] Sprite[] recoverFrames;
        [SerializeField] Sprite hurtFrame;
        [SerializeField] Sprite[] defeatFrames;

        public ArmadilloState State { get; private set; } = ArmadilloState.Patrol;

        /// <summary>Kapanmışken ve yuvarlanırken zırhı her saldırıyı boşa çıkarır.</summary>
        protected override bool Invulnerable => State == ArmadilloState.Curl || State == ArmadilloState.Roll;
        protected override bool CanChase => false;
        protected override bool HoldPosition => State != ArmadilloState.Patrol;

        readonly RaycastHit2D[] sightHits = new RaycastHit2D[4];
        ContactFilter2D solidFilter;
        int walkingContactDamage;
        int rollDirection;
        float stateStartedAt, stateEndsAt, nextAttackAt, hurtUntil, nextDustAt, nextPauseAt, rollStartX;
        bool swingLanded;

        protected override void Awake()
        {
            base.Awake();
            walkingContactDamage = contactDamage;
            solidFilter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = GameLayers.GroundMask | LayerMask.GetMask("Default"), // dövüş odası kapıları ve kaya da duvardır
                useTriggers = false,
            };
            nextPauseAt = Time.time + walkTime;
        }

        void Enter(ArmadilloState state, float duration = 0f)
        {
            State = state;
            stateStartedAt = Time.time;
            stateEndsAt = Time.time + duration;
            // Yuvarlanırken daha çok yakar, sersemken tamamen zararsızdır: yanına gidip vurulabilsin.
            contactDamage = state == ArmadilloState.Roll ? rollDamage
                : state == ArmadilloState.Daze || state == ArmadilloState.Recover ? 0
                : walkingContactDamage;
        }

        protected override void FixedUpdate()
        {
            base.FixedUpdate();
            if (!isActiveAndEnabled)
                return;

            bool timeUp = Time.time >= stateEndsAt;
            switch (State)
            {
                case ArmadilloState.Patrol:
                    if (SeesPlayer())
                        BeginAttack();
                    else if (Time.time >= nextPauseAt)
                        Enter(ArmadilloState.Idle, idleTime);
                    break;
                case ArmadilloState.Idle:
                    if (SeesPlayer())
                        BeginAttack();
                    else if (timeUp)
                    {
                        Enter(ArmadilloState.Patrol);
                        nextPauseAt = Time.time + walkTime;
                    }
                    break;
                case ArmadilloState.Notice:
                    if (timeUp)
                        Enter(ArmadilloState.Curl, curlDuration);
                    break;
                case ArmadilloState.Curl:
                    if (timeUp)
                    {
                        Enter(ArmadilloState.Roll);
                        TurnTowards(rollDirection);
                        rollStartX = transform.position.x;
                    }
                    break;
                case ArmadilloState.Roll:
                    Roll();
                    break;
                case ArmadilloState.Unfold:
                    if (timeUp)
                        AfterUnfold();
                    break;
                case ArmadilloState.Swing:
                    Swing(timeUp);
                    break;
                case ArmadilloState.Daze:
                    if (timeUp)
                        Enter(ArmadilloState.Recover, recoverDuration);
                    break;
                case ArmadilloState.Recover:
                    if (timeUp)
                    {
                        Enter(ArmadilloState.Patrol);
                        nextAttackAt = Time.time + attackCooldown;
                        nextPauseAt = Time.time + walkTime;
                    }
                    break;
            }
        }

        bool SeesPlayer()
        {
            if (Time.time < nextAttackAt || !IsGrounded() || Player == null || !Player.IsAlive)
                return false;
            Vector2 toPlayer = Player.transform.position - transform.position;
            if (Mathf.Abs(toPlayer.x) > sightRange || Mathf.Abs(toPlayer.y) > sightHeight)
                return false;
            Vector2 eye = (Vector2)transform.position + Vector2.up * 0.5f;
            return !SolidAlong(eye, Mathf.Sign(toPlayer.x), Mathf.Abs(toPlayer.x));
        }

        bool SolidAlong(Vector2 from, float direction, float distance)
        {
            int count = Physics2D.Raycast(from, Vector2.right * direction, solidFilter, sightHits, distance);
            for (int i = 0; i < count; i++)
                if (!sightHits[i].collider.usedByEffector)
                    return true;
            return false;
        }

        void BeginAttack()
        {
            if (PlayerInSwingRange())
                BeginSwing();
            else
                BeginNotice();
        }

        bool PlayerInSwingRange()
        {
            if (Player == null || !Player.IsAlive)
                return false;
            Vector2 toPlayer = Player.transform.position - transform.position;
            return Mathf.Abs(toPlayer.x) <= swingRange && Mathf.Abs(toPlayer.y) <= sightHeight;
        }

        // Açılınca geri döner: yakındaysa kazma, görüyorsa yeniden yuvarlanma, yoksa devriye.
        void AfterUnfold()
        {
            if (Player != null && Player.IsAlive)
                TurnTowards(Player.transform.position.x >= transform.position.x ? 1 : -1);
            nextAttackAt = 0f;
            if (SeesPlayer())
            {
                BeginAttack();
                return;
            }
            Enter(ArmadilloState.Patrol);
            nextPauseAt = Time.time + walkTime;
        }

        void BeginSwing()
        {
            TurnTowards(Player.transform.position.x >= transform.position.x ? 1 : -1);
            swingLanded = false;
            Enter(ArmadilloState.Swing, swingDuration);
        }

        void Swing(bool timeUp)
        {
            // Kareler: kaldırır, indirir, toparlar. Darbe inme anında.
            if (!swingLanded && Time.time >= stateStartedAt + swingDuration * 0.4f)
            {
                swingLanded = true;
                Vector2 tip = (Vector2)transform.position + new Vector2(FacingDirection * swingReach * 0.7f, 0.1f);
                PixelBurst.Spawn(tip, sparkColor, 8, 3.5f, 0.3f);
                if (Player != null && Player.IsAlive)
                {
                    Vector2 toPlayer = Player.transform.position - transform.position;
                    if (toPlayer.x * FacingDirection >= -0.3f && Mathf.Abs(toPlayer.x) <= swingReach && Mathf.Abs(toPlayer.y) <= sightHeight)
                        Player.TakeDamage(swingDamage, new Vector2(FacingDirection, 0f));
                }
            }
            if (timeUp)
            {
                Enter(ArmadilloState.Patrol);
                nextAttackAt = Time.time + swingCooldown;
                nextPauseAt = Time.time + walkTime;
            }
        }

        void BeginNotice()
        {
            rollDirection = Player.transform.position.x >= transform.position.x ? 1 : -1;
            TurnTowards(rollDirection);
            Enter(ArmadilloState.Notice, noticeDuration);
        }

        void Roll()
        {
            Bounds b = box.bounds;
            bool wall = SolidAlong(b.center, rollDirection, b.extents.x + 0.15f);
            if (wall || (IsGrounded() && IsBlocked(rollDirection)))
            {
                Crash();
                return;
            }
            // Oyuncuya çarptıysa da durur: geri tepen oyuncunun peşinden sürüklenmesin.
            bool passedPlayer = Player == null || !Player.IsAlive || Player.IsInvincible
                || (transform.position.x - Player.transform.position.x) * rollDirection >= rollOvershoot;
            if (passedPlayer || Mathf.Abs(transform.position.x - rollStartX) >= maxRollDistance)
            {
                body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
                Enter(ArmadilloState.Unfold, unfoldDuration);
                return;
            }
            body.linearVelocity = new Vector2(rollDirection * rollSpeed, body.linearVelocity.y);
            if (Time.time >= nextDustAt && IsGrounded())
            {
                nextDustAt = Time.time + dustInterval;
                PixelBurst.Spawn(new Vector2(b.center.x - rollDirection * b.extents.x, b.min.y + 0.1f), dustColor, 3, 2f, 0.3f);
            }
        }

        void Crash()
        {
            Enter(ArmadilloState.Daze, dazeDuration);
            body.linearVelocity = new Vector2(-rollDirection * crashBounce.x, crashBounce.y);
            Bounds b = box.bounds;
            Vector2 front = new Vector2(b.center.x + rollDirection * b.extents.x, b.center.y);
            PixelBurst.Spawn(front, sparkColor, 12, 5f, 0.45f);
            PixelBurst.Spawn(front, dustColor, 12, 4f, 0.5f);
        }

        protected override void OnBlocked()
        {
            PixelBurst.Spawn(box.bounds.center, sparkColor, 5, 3.5f, 0.25f); // zırhtan seken kıvılcım
        }

        protected override void OnDamaged()
        {
            hurtUntil = Time.time + hurtDuration;
            // Yürürken vurulan armadillo hemen yuvarlanır; sersemken vurulan sersemliğini sürdürür.
            if ((State == ArmadilloState.Patrol || State == ArmadilloState.Idle) && Player != null)
            {
                nextAttackAt = 0f;
                BeginAttack();
            }
        }

        protected override void OnDied()
        {
            ArmadilloDefeat.Spawn(transform.position, defeatFrames, spriteRenderer != null && spriteRenderer.flipX,
                spriteRenderer != null ? spriteRenderer.sortingOrder : 8, dustColor);
        }

        public override void ResetEnemy()
        {
            base.ResetEnemy();
            Enter(ArmadilloState.Patrol);
            nextAttackAt = 0f;
            hurtUntil = 0f;
            nextPauseAt = Time.time + walkTime;
        }

        protected override void Update()
        {
            base.Update();
            if (spriteRenderer == null)
                return;
            Sprite frame = CurrentFrame();
            if (frame != null)
                spriteRenderer.sprite = frame;
        }

        Sprite CurrentFrame()
        {
            if (Time.time < hurtUntil && hurtFrame != null)
                return hurtFrame;
            switch (State)
            {
                case ArmadilloState.Idle: return Loop(idleFrames, idleFps);
                case ArmadilloState.Notice: return Once(noticeFrames, noticeDuration);
                case ArmadilloState.Curl: return Once(curlFrames, curlDuration);
                case ArmadilloState.Roll: return Loop(rollFrames, rollFps);
                case ArmadilloState.Unfold: return OnceReversed(curlFrames, unfoldDuration);
                case ArmadilloState.Swing: return Once(noticeFrames, swingDuration);
                case ArmadilloState.Daze: return Loop(dazeFrames, dazeFps);
                case ArmadilloState.Recover: return Once(recoverFrames, recoverDuration);
                default: return Loop(patrolFrames, patrolFps);
            }
        }

        Sprite Once(Sprite[] frames, float duration)
        {
            if (frames == null || frames.Length == 0)
                return null;
            int i = Mathf.FloorToInt((Time.time - stateStartedAt) / duration * frames.Length);
            return frames[Mathf.Clamp(i, 0, frames.Length - 1)];
        }

        Sprite OnceReversed(Sprite[] frames, float duration)
        {
            if (frames == null || frames.Length == 0)
                return null;
            int i = Mathf.FloorToInt((Time.time - stateStartedAt) / duration * frames.Length);
            return frames[frames.Length - 1 - Mathf.Clamp(i, 0, frames.Length - 1)];
        }

        static Sprite Loop(Sprite[] frames, float fps)
        {
            if (frames == null || frames.Length == 0)
                return null;
            return frames[Mathf.FloorToInt(Time.time * fps) % frames.Length];
        }
    }
}
