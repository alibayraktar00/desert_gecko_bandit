using DesertGecko.Core;
using DesertGecko.Effects;
using UnityEngine;

namespace DesertGecko.Enemies
{
    /// <summary>
    /// Mavi Dev: kobalt mavisi, kaslı, yırtık beyaz tişörtlü ve siyah eşofmanlı ağır siklet mini-boss.
    /// Ağır adımlarla Gila'ya yürür. Menzile girince çömelip yaylanır (uyarı), Gila'nın bulunduğu yere gülle gibi
    /// sıçrar ve yere çarpar: iki yana koşan bir şok dalgası çıkar, yakalanan Gila 2 kalp kaybeder; zıplayan kurtulur.
    /// Kas kütlesi her darbeyi sektirir. Tek savunmasız anı yere çarptıktan sonraki nefes payı: yumrukları yerde,
    /// kıpırdayamaz (punishWindow). Sonra doğrulup yeniden yürür.
    /// Kareler sağa bakar; sola bakarken EnemyBase sprite'ı aynalar.
    /// </summary>
    public class BlueGiant : EnemyBase
    {
        public enum GiantState { Walk, Telegraph, Leap, Slam, Stunned, Recover }

        [Header("Algılama")]
        [Tooltip("Gila bu yatay mesafeye girince sıçrar")]
        [SerializeField] float leapRange = 7f;
        [Tooltip("Gila bundan yakınsa sıçramak yerine yerinde zıplayıp çarpar (üstüne iner)")]
        [SerializeField] float minLeap = 0.6f;
        [SerializeField] float attackCooldown = 1.2f;

        [Header("Sıçrayış")]
        [Tooltip("Hazırlık + çömelme: oyuncunun uyarıyı görme payı")]
        [SerializeField] float telegraphDuration = 0.65f;
        [SerializeField] float leapHeight = 3.6f;
        [Tooltip("En uzak sıçrama; Gila daha uzaktaysa bu kadar yaklaşır")]
        [SerializeField] float maxLeapDistance = 7f;
        [Tooltip("İnişte gövde yere bu kadar süre çakılı durur (çarpma karesi)")]
        [SerializeField] float slamDuration = 0.25f;
        [SerializeField] float shakeAmount = 1f / 32f;

        [Header("Şok dalgası")]
        [SerializeField] Sprite[] shockFrames;
        [Tooltip("Dalgada yakalanan Gila'nın kaybettiği can (yarım kalp = 1): 4 = 2 kalp")]
        [SerializeField] int shockDamage = 4;
        [Tooltip("Cephenin menzili, her iki yöne (efektteki 104 px = 3.25 birim)")]
        [SerializeField] float shockReach = 3.25f;
        [SerializeField] float shockDuration = 0.6f;
        [Tooltip("Cephenin boyu: Gila'nın ayakları bundan yüksekteyse dalga altından geçer")]
        [SerializeField] float shockHeight = 0.7f;

        [Header("Savunmasızlık")]
        [Tooltip("Yere çarptıktan sonra yumruklar yerde: bu süre boyunca kıpırdayamaz ve hasar alır")]
        [SerializeField] float punishWindow = 1.8f;
        [SerializeField] float recoverDuration = 0.35f;
        [SerializeField] Color sparkColor = new Color32(0xB8, 0xD8, 0xFF, 0xFF);
        [SerializeField] Color dustColor = new Color32(0x9A, 0x78, 0x52, 0xFF);

        [Header("Ses")]
        [SerializeField] AudioSource audioSource;
        [SerializeField] AudioClip leapClip;
        [SerializeField] AudioClip slamClip;
        [SerializeField] AudioClip deflectClip;

        [Header("Kareler (fps)")]
        [SerializeField] float idleFps = 3f;
        [SerializeField] float walkFps = 7f;

        [Header("Kareler")]
        [SerializeField] Sprite[] idleFrames;
        [SerializeField] Sprite[] walkFrames;
        [Tooltip("Hazırlık, çömelme")]
        [SerializeField] Sprite[] telegraphFrames;
        [SerializeField] Sprite takeoffFrame;
        [SerializeField] Sprite riseFrame;
        [Tooltip("Tepede top olmuş gövde")]
        [SerializeField] Sprite cannonballFrame;
        [Tooltip("Hızlı düşüş: bacaklar aşağıda")]
        [SerializeField] Sprite fallFrame;
        [SerializeField] Sprite slamFrame;
        [Tooltip("Yumruklar yerde, soluklanır")]
        [SerializeField] Sprite stunnedFrame;
        [Tooltip("Ölüm: sendeleyip çöker")]
        [SerializeField] Sprite[] defeatFrames;

        public GiantState State { get; private set; } = GiantState.Walk;

        /// <summary>Yalnız yere çarptıktan sonraki nefes payında vurulur; kas kütlesi geri kalan her şeyi sektirir.</summary>
        protected override bool Invulnerable => State != GiantState.Stunned;
        protected override bool KeepsFooting => true;
        protected override bool ClimbsLayers => false;
        protected override bool HoldPosition => State != GiantState.Walk && State != GiantState.Leap;

        Vector3 visualHome;
        int bodyContactDamage;
        float stateStartedAt, stateEndsAt, nextAttackAt, leapVx, leapStartedAt, nextDustAt;

        protected override void Awake()
        {
            base.Awake();
            bodyContactDamage = contactDamage;
            if (spriteRenderer != null)
                visualHome = spriteRenderer.transform.localPosition;
        }

        void Enter(GiantState state, float duration = 0f)
        {
            State = state;
            stateStartedAt = Time.time;
            stateEndsAt = Time.time + duration;
            // Yumrukları yerdeyken çarpmak can yakmaz: Gila dibine girip vurabilsin.
            contactDamage = state == GiantState.Stunned ? 0 : bodyContactDamage;
        }

        int DirectionToPlayer => Player != null && Player.transform.position.x >= transform.position.x ? 1 : -1;

        protected override void FixedUpdate()
        {
            base.FixedUpdate();
            if (!isActiveAndEnabled)
                return;

            bool timeUp = Time.time >= stateEndsAt;
            switch (State)
            {
                case GiantState.Walk:
                    if (WantsToLeap())
                    {
                        TurnTowards(DirectionToPlayer);
                        Enter(GiantState.Telegraph, telegraphDuration);
                    }
                    break;
                case GiantState.Telegraph:
                    if (timeUp)
                        Launch();
                    break;
                case GiantState.Leap:
                    // EnemyBase yerdeyken yürüyüşü uygular: kalkış anında hızı ezmesin, havada yönünü korusun.
                    body.linearVelocity = new Vector2(leapVx, body.linearVelocity.y);
                    if (Time.time > leapStartedAt + 0.15f && IsGrounded() && body.linearVelocity.y <= 0.1f)
                        Slam();
                    break;
                case GiantState.Slam:
                    if (timeUp)
                        Enter(GiantState.Stunned, punishWindow);
                    break;
                case GiantState.Stunned:
                    Pant();
                    if (timeUp)
                        Enter(GiantState.Recover, recoverDuration);
                    break;
                case GiantState.Recover:
                    if (timeUp)
                    {
                        nextAttackAt = Time.time + attackCooldown;
                        Enter(GiantState.Walk);
                    }
                    break;
            }
        }

        bool WantsToLeap()
        {
            if (Time.time < nextAttackAt || !IsGrounded() || Player == null || !Player.IsAlive)
                return false;
            Vector2 to = Player.transform.position - transform.position;
            return Mathf.Abs(to.x) <= leapRange && Mathf.Abs(to.y) <= verticalTolerance;
        }

        void Launch()
        {
            int dir = DirectionToPlayer;
            TurnTowards(dir);
            float dx = Player != null ? Player.transform.position.x - transform.position.x : 0f;
            dx = Mathf.Clamp(dx, -maxLeapDistance, maxLeapDistance);
            if (Mathf.Abs(dx) < minLeap)
                dx = 0f;
            dx = SafeLanding(dx);

            float gravity = -Physics2D.gravity.y * body.gravityScale;
            float vy = Mathf.Sqrt(2f * gravity * leapHeight);
            float airTime = 2f * vy / gravity;
            leapVx = dx / airTime;
            body.linearVelocity = new Vector2(leapVx, vy);
            leapStartedAt = Time.time;
            PixelBurst.Spawn(new Vector2(box.bounds.center.x, box.bounds.min.y + 0.05f), dustColor, 10, 3f, 0.4f);
            Play(leapClip);
            Enter(GiantState.Leap);
        }

        /// <summary>İniş noktasında zemin yoksa (uçurum) kısaltır: dev boşluğa atlamasın.</summary>
        float SafeLanding(float dx)
        {
            float feetY = box.bounds.min.y;
            for (int i = 0; i < 8 && Mathf.Abs(dx) > 0.05f; i++)
            {
                Vector2 probe = new Vector2(transform.position.x + dx, feetY + 1.5f);
                RaycastHit2D hit = Physics2D.Raycast(probe, Vector2.down, 3f, GameLayers.GroundMask);
                if (hit && Mathf.Abs(hit.point.y - feetY) < 0.5f)
                    return dx;
                dx *= 0.75f;
            }
            return 0f;
        }

        void Slam()
        {
            leapVx = 0f;
            body.linearVelocity = Vector2.zero;
            Vector2 ground = new Vector2(transform.position.x, box.bounds.min.y);
            GiantShockwave.Spawn(ground, shockFrames, (spriteRenderer != null ? spriteRenderer.sortingOrder : 8) + 1,
                shockDamage, shockReach, shockDuration, shockHeight);
            PixelBurst.Spawn(ground + Vector2.up * 0.1f, dustColor, 16, 4f, 0.5f);
            CameraShake.AddAt(ground, 0.55f);
            HitStop.Request(0.06f);
            Play(slamClip);
            Enter(GiantState.Slam, slamDuration);
        }

        void Pant()
        {
            if (Time.time < nextDustAt)
                return;
            nextDustAt = Time.time + 0.5f;
            PixelBurst.Spawn((Vector2)transform.position + new Vector2(FacingDirection * 0.7f, 0.1f), dustColor, 3, 1.2f, 0.35f);
        }

        void Play(AudioClip clip)
        {
            if (audioSource != null && clip != null)
                audioSource.PlayOneShot(clip);
        }

        protected override void OnBlocked()
        {
            // Darbe kas kütlesinden seker.
            PixelBurst.Spawn(box.bounds.center + Vector3.up * 0.3f, sparkColor, 5, 3.5f, 0.25f);
            Play(deflectClip);
        }

        protected override float WeaponDropDelay => (defeatFrames != null ? defeatFrames.Length : 0) * 0.09f + 1f;

        protected override void OnDied()
        {
            CameraShake.AddAt(transform.position, 0.35f);
            ArmadilloDefeat.Spawn(transform.position, defeatFrames, spriteRenderer != null && spriteRenderer.flipX,
                spriteRenderer != null ? spriteRenderer.sortingOrder : 8, dustColor);
        }

        public override void ResetEnemy()
        {
            base.ResetEnemy();
            nextAttackAt = 0f;
            leapVx = 0f;
            Enter(GiantState.Walk);
        }

        protected override void Update()
        {
            base.Update();
            if (spriteRenderer == null)
                return;
            Sprite frame = CurrentFrame();
            if (frame != null)
                spriteRenderer.sprite = frame;

            // Çömelirken yaylanır, titrer: sıçrama geliyor.
            Vector3 offset = Vector3.zero;
            if (State == GiantState.Telegraph && Time.time - stateStartedAt > telegraphDuration * 0.45f)
                offset.x = (Mathf.FloorToInt(Time.time * 30f) % 2 == 0 ? 1f : -1f) * shakeAmount;
            spriteRenderer.transform.localPosition = visualHome + offset;
        }

        Sprite CurrentFrame()
        {
            switch (State)
            {
                case GiantState.Telegraph:
                    return Once(telegraphFrames, telegraphDuration);
                case GiantState.Leap:
                    float vy = body.linearVelocity.y;
                    if (Time.time - leapStartedAt < 0.1f)
                        return takeoffFrame;
                    if (vy > 5f)
                        return riseFrame;
                    return vy > -5f ? cannonballFrame : fallFrame;
                case GiantState.Slam:
                    return slamFrame;
                case GiantState.Stunned:
                    return stunnedFrame;
                case GiantState.Recover:
                    return telegraphFrames != null && telegraphFrames.Length > 0 ? telegraphFrames[0] : slamFrame;
                default:
                    return Mathf.Abs(body.linearVelocity.x) > 0.1f ? Loop(walkFrames, walkFps) : Loop(idleFrames, idleFps);
            }
        }

        Sprite Once(Sprite[] frames, float duration)
        {
            if (frames == null || frames.Length == 0)
                return null;
            int i = Mathf.FloorToInt((Time.time - stateStartedAt) / duration * frames.Length);
            return frames[Mathf.Clamp(i, 0, frames.Length - 1)];
        }

        static Sprite Loop(Sprite[] frames, float fps)
        {
            if (frames == null || frames.Length == 0)
                return null;
            return frames[Mathf.FloorToInt(Time.time * fps) % frames.Length];
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            Gizmos.color = new Color(0.3f, 0.5f, 1f, 0.7f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.5f, new Vector3(leapRange * 2f, 1f));
            Gizmos.color = new Color(1f, 0.8f, 0.3f, 0.7f);
            Gizmos.DrawLine(transform.position + Vector3.left * shockReach, transform.position + Vector3.right * shockReach);
        }
    }
}
