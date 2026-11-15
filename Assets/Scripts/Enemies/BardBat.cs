using DesertGecko.Core;
using DesertGecko.Effects;
using UnityEngine;

namespace DesertGecko.Enemies
{
    /// <summary>
    /// Bard'ın çağırdığı yarasa. Haydut Yarasa'nın kareleriyle uçar ama yuvası yoktur: yukarıdaki karanlıktan uçarak
    /// gelir, Bard'ın başının üstünde toplanır, sırası gelince saldırır ve Gila onu öldürene kadar saldırmayı bırakmaz
    /// (Bard ölse de). Dinamiti ve dalışı tükenmez.
    /// Dinamitçi: Gila'nın tepesine süzülür, fitili yakıp (uyarı) dinamit bırakır.
    /// Dalışçı: Gila'nın çaprazında asılı kalıp alay eder (uyarı), sonra o anki yerine dümdüz, hızla dalar;
    /// dalış sırasında yön değiştirmez, zıplayan ya da çekilen kurtulur.
    /// Canını Bard verir (insan fazında 2, canavar fazında 3 vuruş). Vurulup ölmeyen yarasa sarsılıp geri savrulur,
    /// dalıyorsa dalışı bozulur; kısa bir sersemlemeden sonra Gila'ya saldırmaya devam eder (Bard'a dönmez).
    /// Son vuruşta yere çakılıp ölür.
    /// Kristal ve silah düşürmez; oyuncu yeniden doğunca silinir (sürüyü Bard yeniden çağırır).
    /// Transform yarasanın ortasıdır; kareler sağa bakar.
    /// </summary>
    public class BardBat : EnemyBase
    {
        public enum BatRole { Bomber, Diver }
        public enum BatState { Appear, Hover, Windup, Release, Aim, Dive, Climb, Leave }

        [Header("Uçuş")]
        [SerializeField] float flySpeed = 6f;
        [SerializeField] float flyAcceleration = 18f;
        [Tooltip("Dinamitçi Gila'nın bu kadar üstünde süzülür")]
        [SerializeField] float hoverHeight = 3.4f;
        [SerializeField] float bobAmplitude = 0.18f;
        [SerializeField] float bobFrequency = 1.6f;
        [Tooltip("Belirdikten sonra saldırıya geçmeden önce başın üstünde bu kadar süzülür (+ sıra gecikmesi)")]
        [SerializeField] float appearDuration = 0.4f;

        [Header("Dinamit")]
        [SerializeField] BatDynamite dynamitePrefab;
        [Tooltip("Yatayda Gila'ya bu kadar yaklaşınca fitili yakar")]
        [SerializeField] float aimTolerance = 1f;
        [Tooltip("Tepesine gelemezse bu kadar bekleyip dinamiti kavisle atar")]
        [SerializeField] float lobPatience = 1.4f;
        [SerializeField] float windupDuration = 0.6f;
        [SerializeField] float releaseDuration = 0.3f;
        [SerializeField] float throwInterval = 1.3f;
        [SerializeField] Vector2 dropOffset = new Vector2(0.2f, -0.55f);
        [SerializeField] Color sparkColor = new Color32(0xFF, 0xD2, 0x4A, 0xFF);

        [Header("Dalış")]
        [Tooltip("Dalıştan önce Gila'ya göre asılı kaldığı yer (yatay, dikey)")]
        [SerializeField] Vector2 aimOffset = new Vector2(3.2f, 2.8f);
        [Tooltip("Dalış uyarısı: alay eder, gözleri parlar")]
        [SerializeField] float aimDuration = 0.6f;
        [SerializeField] float diveSpeed = 13f;
        [Tooltip("Hedefi bu kadar geçince dalışı bırakır")]
        [SerializeField] float diveOvershoot = 1.6f;
        [SerializeField] float maxDiveTime = 0.9f;
        [SerializeField] float climbDuration = 0.9f;
        [SerializeField] Color diveColor = new Color32(0xFF, 0x5A, 0x5A, 0xFF);

        [Header("Darbe")]
        [Tooltip("Vurulup ölmeyen yarasa bu kadar sersemler: geri savrulur, sonra saldırıya döner")]
        [SerializeField] float staggerTime = 0.35f;
        [SerializeField] float staggerPush = 5f;

        [Header("Ayrılış")]
        [SerializeField] float leaveDuration = 0.8f;
        [SerializeField] float targetHeight = 0.45f;
        [SerializeField] Color dustColor = new Color32(0x9C, 0x7A, 0x52, 0xFF);

        [Header("Kareler")]
        [SerializeField] float flyFps = 12f;
        [SerializeField] float tauntFps = 12f;
        [SerializeField] Sprite[] flyFrames;
        [SerializeField] Sprite[] glideFrames;
        [SerializeField] Sprite[] windupFrames;
        [SerializeField] Sprite[] releaseFrames;
        [SerializeField] Sprite[] tauntFrames;
        [SerializeField] Sprite[] hurtFrames;
        [SerializeField] Sprite[] defeatFrames;

        public BatState State { get; private set; } = BatState.Appear;
        public BatRole Role { get; private set; }

        protected override bool CanChase => false;
        protected override bool HoldPosition => true; // kendi uçuşunu aşağıda yapar
        public override bool IgnoresEnemyFire => true;

        Bard owner;
        float speedScale = 1f, side = 1f;
        float stateStartedAt, stateEndsAt, nextThrowAt, hoverSince, nextSparkAt;
        bool lobbing;
        Vector2 perch, perchOffset, diveDirection, diveFrom;
        float diveLength;
        Color baseTint = Color.white;

        bool PlayerAlive => Player != null && Player.IsAlive;
        Vector2 PlayerTarget => (Vector2)Player.transform.position + Vector2.up * targetHeight;
        int DirectionToPlayer => Player != null && Player.transform.position.x >= transform.position.x ? 1 : -1;

        protected override void Awake()
        {
            base.Awake();
            body.gravityScale = 0f;
            perch = transform.position;
            if (spriteRenderer != null)
                baseTint = spriteRenderer.color;
            Enter(BatState.Appear, appearDuration);
        }

        /// <summary>
        /// Bard çağırınca: rolü, hız çarpanı, saldırıya geçme gecikmesi, yelpazedeki yeri (-1 sol, 1 sağ) ve Bard'a göre
        /// toplanacağı nokta. Yarasa olduğu yerden (yukarıdaki karanlık) oraya uçar.
        /// </summary>
        public void Summon(Bard bard, BatRole role, float speed, float delay, float spread, Vector2 gatherOffset, int health)
        {
            SetHealth(health);
            owner = bard;
            Role = role;
            speedScale = speed;
            side = spread >= 0f ? 1f : -1f;
            perchOffset = gatherOffset;
            perch = (Vector2)bard.transform.position + gatherOffset;
            Enter(BatState.Appear, appearDuration + delay);
        }

        /// <summary>Toplanma noktası: Bard yaşıyorsa onunla birlikte kayar.</summary>
        Vector2 Perch => owner != null && owner.IsAlive ? (Vector2)owner.transform.position + perchOffset : perch;

        void Enter(BatState state, float duration)
        {
            State = state;
            stateStartedAt = Time.time;
            stateEndsAt = Time.time + duration;
        }

        protected override void FixedUpdate()
        {
            base.FixedUpdate();
            if (!isActiveAndEnabled)
                return;
            body.gravityScale = 0f;
            bool timeUp = Time.time >= stateEndsAt;

            // Gila öldüyse çekilir (Gila doğunca sürü baştan çağrılır). Bard ölse de saldırmayı bırakmaz.
            if (State != BatState.Leave && State != BatState.Dive && !PlayerAlive)
            {
                Enter(BatState.Leave, leaveDuration);
                return;
            }

            if (Time.time < staggerUntil)
            {
                // Sersemlemiş: savrulduğu yönde yavaşlar, saldırmaz.
                body.linearVelocity = Vector2.MoveTowards(body.linearVelocity, Vector2.zero, flyAcceleration * Time.fixedDeltaTime);
                return;
            }

            switch (State)
            {
                case BatState.Appear:
                    // Karanlıktan uçarak gelir; toplanma noktasına varınca (ve sırası gelince) saldırır.
                    FlyTowards(Perch, flySpeed, bob: true);
                    TurnTowards(DirectionToPlayer);
                    if (timeUp && (Vector2.Distance(body.position, Perch) < 0.8f || Time.time > stateEndsAt + 2f))
                        BeginAttack();
                    break;
                case BatState.Hover:
                    Hover();
                    break;
                case BatState.Windup:
                    FlyTowards(HoverPoint(), flySpeed * 0.5f);
                    Spark();
                    if (timeUp)
                        Release();
                    break;
                case BatState.Release:
                    FlyTowards(HoverPoint(), flySpeed * 0.3f);
                    if (timeUp)
                        AfterThrow();
                    break;
                case BatState.Aim:
                    FlyTowards(AimPoint(), flySpeed, bob: true);
                    TurnTowards(DirectionToPlayer);
                    if (timeUp)
                        BeginDive();
                    break;
                case BatState.Dive:
                    Dive();
                    break;
                case BatState.Climb:
                    FlyTowards(AimPoint(), flySpeed);
                    if (timeUp)
                        BeginAttack();
                    break;
                case BatState.Leave:
                    body.linearVelocity = Vector2.MoveTowards(body.linearVelocity, new Vector2(side * 2f, 7f), flyAcceleration * 2f * Time.fixedDeltaTime);
                    if (timeUp)
                        Destroy(gameObject);
                    break;
            }
        }

        void BeginAttack()
        {
            if (Role == BatRole.Bomber)
            {
                hoverSince = Time.time;
                nextThrowAt = Time.time + 0.25f;
                Enter(BatState.Hover, 0f);
            }
            else
            {
                // Gila'nın hangi yanındaysa o çaprazdan dalar.
                side = transform.position.x >= Player.transform.position.x ? 1f : -1f;
                Enter(BatState.Aim, aimDuration / speedScale + 0.5f);
            }
        }

        // --- Dinamitçi ---

        Vector2 HoverPoint() => (Vector2)Player.transform.position + Vector2.up * hoverHeight;

        void Hover()
        {
            FlyTowards(HoverPoint(), flySpeed, bob: true);
            TurnTowards(DirectionToPlayer);
            if (Time.time < nextThrowAt)
                return;
            float dx = Mathf.Abs(Player.transform.position.x - body.position.x);
            if (dx <= aimTolerance)
                lobbing = false;
            else if (Time.time >= hoverSince + lobPatience)
                lobbing = true; // tepesine gelemedi (tavan, hızlı Gila): boş beklemez, kavisle atar
            else
                return;
            Enter(BatState.Windup, windupDuration / speedScale);
        }

        Vector2 DropPoint() => body.position + new Vector2(dropOffset.x * FacingDirection, dropOffset.y);

        void Spark()
        {
            if (Time.time < nextSparkAt)
                return;
            nextSparkAt = Time.time + 0.08f;
            PixelBurst.Spawn(DropPoint() + new Vector2(FacingDirection * 0.1f, 0.15f), sparkColor, 2, 1.8f, 0.2f);
        }

        void Release()
        {
            if (dynamitePrefab != null)
            {
                BatDynamite d = Instantiate(dynamitePrefab, DropPoint(), Quaternion.identity);
                d.Launch(lobbing && PlayerAlive ? LobVelocity(d.Gravity) : new Vector2(body.linearVelocity.x * 0.4f, -1f), FacingDirection);
            }
            Enter(BatState.Release, releaseDuration);
        }

        void AfterThrow()
        {
            hoverSince = Time.time;
            nextThrowAt = Time.time + throwInterval / speedScale;
            Enter(BatState.Hover, 0f);
        }

        /// <summary>Hafif yukarı atılıp Gila'nın atış anındaki yerine düşecek hız (BanditBat ile aynı).</summary>
        Vector2 LobVelocity(float gravity)
        {
            const float up = 3f;
            Vector2 from = DropPoint();
            Vector2 to = Player.transform.position;
            float drop = Mathf.Max(0.5f, from.y - to.y);
            float g = Mathf.Max(1f, gravity);
            float t = (up + Mathf.Sqrt(up * up + 2f * g * drop)) / g;
            float vx = Mathf.Clamp((to.x - from.x) / t, -10f, 10f);
            return new Vector2(vx, up);
        }

        // --- Dalışçı ---

        Vector2 AimPoint() => (Vector2)Player.transform.position + new Vector2(side * aimOffset.x, aimOffset.y);

        void BeginDive()
        {
            diveFrom = body.position;
            Vector2 toTarget = PlayerTarget - diveFrom;
            diveLength = toTarget.magnitude + diveOvershoot;
            diveDirection = toTarget.sqrMagnitude > 0.01f ? toTarget.normalized : Vector2.down;
            TurnTowards(diveDirection.x >= 0f ? 1 : -1);
            PixelBurst.Spawn(body.position, diveColor, 5, 3f, 0.25f);
            Enter(BatState.Dive, maxDiveTime);
        }

        void Dive()
        {
            float speed = diveSpeed * speedScale;
            body.linearVelocity = diveDirection * speed;
            bool travelled = Vector2.Distance(body.position, diveFrom) >= diveLength;
            // Yere ya da duvara tosladı: fizik onu durdurdu.
            bool stopped = Time.time - stateStartedAt > 0.12f
                && Physics2D.Raycast(body.position, diveDirection, 0.55f, GameLayers.GroundMask);
            if (travelled || stopped || Time.time >= stateEndsAt)
            {
                body.linearVelocity *= 0.3f;
                Enter(BatState.Climb, climbDuration);
            }
        }

        // --- Ortak ---

        void FlyTowards(Vector2 target, float speed, bool bob = false)
        {
            if (bob)
                target.y += Mathf.Sin(Time.time * bobFrequency * Mathf.PI * 2f + side) * bobAmplitude;
            Vector2 toTarget = target - body.position;
            Vector2 desired = Vector2.ClampMagnitude(toTarget * 3f, speed * speedScale);
            body.linearVelocity = Vector2.MoveTowards(body.linearVelocity, desired, flyAcceleration * speedScale * Time.fixedDeltaTime);
        }

        float staggerUntil;

        protected override void OnDamaged()
        {
            // Darbe: Gila'dan uzağa savrulur, kısa bir an sersemler. Dalıyor ya da fitil yakıyorsa saldırısı bozulur;
            // sonra Gila'nın çaprazına tırmanıp saldırıya yeniden başlar.
            staggerUntil = Time.time + staggerTime;
            Vector2 away = PlayerAlive ? (body.position - PlayerTarget).normalized : Vector2.up;
            body.linearVelocity = (away + Vector2.up * 0.4f).normalized * staggerPush;
            PixelBurst.Spawn(body.position, dustColor, 4, 2f, 0.25f);
            if (State == BatState.Dive || State == BatState.Windup)
                Enter(BatState.Climb, climbDuration + staggerTime);
            else if (State == BatState.Hover || State == BatState.Aim)
                stateEndsAt += staggerTime;
        }

        protected override void OnDied()
        {
            bool flipX = spriteRenderer != null && spriteRenderer.flipX;
            int order = spriteRenderer != null ? spriteRenderer.sortingOrder : 8;
            BatDefeat.Spawn(transform.position, body.linearVelocity, defeatFrames, flipX, order, dustColor);
            Destroy(gameObject);
        }

        /// <summary>Oyuncu yeniden doğdu: çağrılmış yarasa geri gelmez, Bard sürüyü baştan çağırır.</summary>
        public override void ResetEnemy()
        {
            Destroy(gameObject);
        }

        protected override void Update()
        {
            base.Update();
            if (spriteRenderer == null)
                return;
            Sprite frame = CurrentFrame();
            if (frame != null)
                spriteRenderer.sprite = frame;
            // Dalış uyarısı kırmızı yanıp söner; ayrılırken solar. (Darbe kızarması ve koruma yanıp sönmesi EnemyBase'de.)
            Color color = spriteRenderer.color;
            if (State == BatState.Aim && Time.time > stateEndsAt - aimDuration / speedScale && Mathf.Sin(Time.time * 40f) > 0f)
                color = Color.Lerp(baseTint, diveColor, 0.7f);
            if (State == BatState.Leave)
                color.a *= Mathf.Clamp01((stateEndsAt - Time.time) / leaveDuration);
            spriteRenderer.color = color;
        }

        Sprite CurrentFrame()
        {
            if (Time.time < staggerUntil)
                return Loop(hurtFrames, flyFps * 1.5f);
            switch (State)
            {
                case BatState.Windup: return Loop(windupFrames, flyFps);
                case BatState.Release: return Loop(releaseFrames, flyFps);
                case BatState.Aim:
                    return Time.time > stateEndsAt - aimDuration / speedScale ? Loop(tauntFrames, tauntFps) : Loop(flyFrames, flyFps);
                case BatState.Dive: return Loop(glideFrames, flyFps * 1.5f);
                default:
                    return Mathf.Abs(body.linearVelocity.x) > flySpeed * 0.6f ? Loop(glideFrames, flyFps) : Loop(flyFrames, flyFps);
            }
        }

        static Sprite Loop(Sprite[] frames, float fps)
        {
            if (frames == null || frames.Length == 0)
                return null;
            return frames[Mathf.FloorToInt(Time.time * fps) % frames.Length];
        }
    }
}
