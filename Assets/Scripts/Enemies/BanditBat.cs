using DesertGecko.Core;
using DesertGecko.Effects;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DesertGecko.Enemies
{
    /// <summary>
    /// Haydut Yarasa: tavana/dala asılı uyur. Gila altından geçince uyanır, havalanır ve tepesinde süzülür.
    /// Fitili yakıp (uyarı) dinamiti tam üstünden bırakır, her atıştan sonra göz kırpıp alay eder.
    /// Dinamitleri bitince yorgun düşer: başında yıldızlar, alçalıp birkaç saniye savunmasız kalır.
    /// O sürede alt edilmezse toparlanır, yeniden havalanır ve cebini doldurur.
    /// Havada olduğu için Gila ona dili yukarı/çapraz uzatarak (W + J) ulaşır.
    /// Ölünce kanatları kapanır, yere çakılır, toz kalkar.
    /// Transform yarasanın ortasıdır; kareler sağa bakar.
    /// Gece mor gövdesi gökyüzüne karışmasın diye arkasında soluk ay ışığı halesi yanar ve kendi küçük
    /// ışığı gövdesini aydınlatır; ikisi de ortam karardıkça güçlenir, gündüz tamamen söner.
    /// </summary>
    public class BanditBat : EnemyBase
    {
        public enum BatState { Asleep, Wake, Hover, Windup, Release, Taunt, Tired, Recover, Return }

        [Header("Uyku ve uyanma")]
        [Tooltip("Gila bu yatay mesafeye ve altındaki bu derinliğe girince uyanır")]
        [SerializeField] float wakeHalfWidth = 4.5f;
        [SerializeField] float wakeDepth = 12f; // yüksek tavanlı mağaralarda da yerdeki Gila'yı görsün
        [SerializeField] float wakeDuration = 0.6f;
        [SerializeField] float wakeDrop = 1.2f;

        [Header("Uçuş")]
        [SerializeField] float flySpeed = 4.5f;
        [SerializeField] float flyAcceleration = 14f;
        [Tooltip("Gila'nın bu kadar üstünde süzülür")]
        [SerializeField] float hoverHeight = 3.6f;
        [Tooltip("Asıldığı noktadan en fazla bu kadar uzaklaşır")]
        [SerializeField] float leashRange = 9f;
        [Tooltip("Gila bu kadar uzaklaşınca yuvasına döner")]
        [SerializeField] float giveUpRange = 13f;
        [SerializeField] float bobAmplitude = 0.18f;
        [SerializeField] float bobFrequency = 1.6f;

        [Header("Dinamit")]
        [SerializeField] BatDynamite dynamitePrefab;
        [SerializeField] int dynamiteCount = 3;
        [Tooltip("Yatayda Gila'ya bu kadar yaklaşınca fitili yakar")]
        [SerializeField] float aimTolerance = 0.9f;
        [SerializeField] float firstThrowDelay = 0.8f;
        [SerializeField] float windupDuration = 0.75f;
        [SerializeField] float releaseDuration = 0.3f;
        [SerializeField] float tauntDuration = 0.9f;
        [SerializeField] Vector2 dropOffset = new Vector2(0.2f, -0.55f);
        [SerializeField] Color sparkColor = new Color32(0xFF, 0xD2, 0x4A, 0xFF);

        [Header("Yorgunluk")]
        [SerializeField] float tiredDuration = 3.5f;
        [Tooltip("Yorgunken zeminin bu kadar üstüne alçalır: dil kolayca yetişsin")]
        [SerializeField] float tiredHeight = 1.6f;
        [SerializeField] float recoverDuration = 0.7f;

        [Header("Hasar")]
        [SerializeField] float hurtPoseTime = 0.4f;
        [SerializeField] Color dustColor = new Color32(0x9C, 0x7A, 0x52, 0xFF);

        [Header("Kareler (fps)")]
        [SerializeField] float hangFps = 4f;
        [SerializeField] float flyFps = 12f;
        [SerializeField] float tauntFps = 9f;
        [SerializeField] float dizzyFps = 8f;

        [Header("Gece görünürlüğü")]
        [Tooltip("Arkadaki soluk hale: silueti koyu gökyüzünden ayırır")]
        [SerializeField] SpriteRenderer nightHalo;
        [SerializeField] float nightHaloAlpha = 0.45f;
        [Tooltip("Gövdeyi aydınlatan küçük ışık")]
        [SerializeField] Light2D nightLight;
        [SerializeField] float nightLightIntensity = 1.4f;
        [Tooltip("Ortam ışığı bu aralıkta karardıkça hale ve ışık açılır (1 gündüz, 0.5 gece)")]
        [SerializeField] Vector2 darkRange = new Vector2(0.95f, 0.6f);

        [Header("Kareler")]
        [SerializeField] Sprite[] hangFrames;
        [SerializeField] Sprite[] flyFrames;
        [SerializeField] Sprite[] glideFrames;
        [SerializeField] Sprite[] windupFrames;
        [SerializeField] Sprite[] releaseFrames;
        [SerializeField] Sprite[] tauntFrames;
        [SerializeField] Sprite[] dizzyFrames;
        [SerializeField] Sprite[] hurtFrames;
        [SerializeField] Sprite[] recoverFrames;
        [SerializeField] Sprite[] defeatFrames;

        public BatState State { get; private set; } = BatState.Asleep;
        public int DynamitesLeft { get; private set; }

        protected override bool CanChase => false;
        protected override bool HoldPosition => true; // kendi uçuşunu aşağıda yapar

        Vector2 home;
        float stateStartedAt, stateEndsAt, hurtUntil, nextThrowAt, nextSparkAt;
        float tiredTargetY;
        int baseContactDamage;

        protected override void Awake()
        {
            base.Awake();
            home = transform.position;
            body.gravityScale = 0f;
            baseContactDamage = contactDamage;
            DynamitesLeft = dynamiteCount;
            Enter(BatState.Asleep, 0f);
        }

        void Enter(BatState state, float duration)
        {
            State = state;
            stateStartedAt = Time.time;
            stateEndsAt = Time.time + duration;
            // Uyurken ve yorgunken çarpmak can yakmaz: yorgunluk Gila'nın saldırı fırsatı.
            contactDamage = state == BatState.Asleep || state == BatState.Tired ? 0 : baseContactDamage;
        }

        int DirectionToPlayer => Player != null && Player.transform.position.x >= transform.position.x ? 1 : -1;

        bool PlayerAlive => Player != null && Player.IsAlive;

        protected override void FixedUpdate()
        {
            base.FixedUpdate();
            if (!isActiveAndEnabled)
                return;
            body.gravityScale = 0f;

            bool timeUp = Time.time >= stateEndsAt;
            switch (State)
            {
                case BatState.Asleep:
                    body.linearVelocity = Vector2.zero;
                    body.position = home;
                    if (PlayerBelowToWake())
                        Wake();
                    break;
                case BatState.Wake:
                    FlyTowards(home + Vector2.down * wakeDrop, flySpeed * 0.6f);
                    if (timeUp)
                        BeginHover(firstThrowDelay);
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
                        Enter(BatState.Taunt, tauntDuration);
                    break;
                case BatState.Taunt:
                    FlyTowards(HoverPoint(), flySpeed * 0.4f);
                    if (timeUp)
                    {
                        if (DynamitesLeft > 0)
                            BeginHover(0.35f);
                        else
                            BeginTired();
                    }
                    break;
                case BatState.Tired:
                    FlyTowards(new Vector2(transform.position.x, tiredTargetY), 2f, bob: true);
                    if (timeUp)
                        Enter(BatState.Recover, recoverDuration);
                    break;
                case BatState.Recover:
                    FlyTowards(HoverPoint(), flySpeed * 0.7f);
                    if (timeUp)
                    {
                        DynamitesLeft = dynamiteCount;
                        BeginHover(0.5f);
                    }
                    break;
                case BatState.Return:
                    FlyTowards(home, flySpeed);
                    if (Vector2.Distance(body.position, home) < 0.15f)
                        Enter(BatState.Asleep, 0f);
                    else if (PlayerAlive && PlayerNearHome())
                        BeginHover(0.5f);
                    break;
            }
        }

        bool PlayerBelowToWake()
        {
            if (!PlayerAlive)
                return false;
            Vector2 toPlayer = Player.transform.position - (Vector3)home;
            return Mathf.Abs(toPlayer.x) <= wakeHalfWidth && toPlayer.y <= 0.5f && toPlayer.y >= -wakeDepth;
        }

        bool PlayerNearHome()
        {
            Vector2 toPlayer = Player.transform.position - (Vector3)home;
            return Mathf.Abs(toPlayer.x) <= leashRange && toPlayer.y <= 0.5f && toPlayer.y >= -wakeDepth;
        }

        void Wake()
        {
            TurnTowards(DirectionToPlayer);
            DynamitesLeft = dynamiteCount;
            Enter(BatState.Wake, wakeDuration);
        }

        void BeginHover(float throwDelay)
        {
            nextThrowAt = Time.time + throwDelay;
            Enter(BatState.Hover, 0f);
        }

        /// <summary>Gila'nın tepesi; yuvadan uzaklaşmaz, yuvadan (tavandan) yukarı çıkmaz.</summary>
        Vector2 HoverPoint()
        {
            if (!PlayerAlive)
                return home;
            Vector2 p = Player.transform.position;
            float x = Mathf.Clamp(p.x, home.x - leashRange, home.x + leashRange);
            float y = Mathf.Min(home.y - 0.2f, p.y + hoverHeight);
            return new Vector2(x, y);
        }

        void Hover()
        {
            if (!PlayerAlive || Vector2.Distance(Player.transform.position, body.position) > giveUpRange)
            {
                Enter(BatState.Return, 0f);
                return;
            }
            FlyTowards(HoverPoint(), flySpeed, bob: true);
            TurnTowards(DirectionToPlayer);

            float dx = Mathf.Abs(Player.transform.position.x - body.position.x);
            if (Time.time >= nextThrowAt && dx <= aimTolerance && DynamitesLeft > 0)
                Enter(BatState.Windup, windupDuration);
        }

        void FlyTowards(Vector2 target, float speed, bool bob = false)
        {
            if (bob)
                target.y += Mathf.Sin(Time.time * bobFrequency * Mathf.PI * 2f) * bobAmplitude;
            Vector2 toTarget = target - body.position;
            // Hedefe yaklaştıkça yavaşlar: üstünde titreyip durmasın.
            Vector2 desired = Vector2.ClampMagnitude(toTarget * 3f, speed);
            body.linearVelocity = Vector2.MoveTowards(body.linearVelocity, desired, flyAcceleration * Time.fixedDeltaTime);
        }

        void Spark()
        {
            if (Time.time < nextSparkAt)
                return;
            nextSparkAt = Time.time + 0.08f;
            PixelBurst.Spawn(DropPoint() + new Vector2(FacingDirection * 0.1f, 0.15f), sparkColor, 2, 1.8f, 0.2f);
        }

        Vector2 DropPoint() => body.position + new Vector2(dropOffset.x * FacingDirection, dropOffset.y);

        void Release()
        {
            DynamitesLeft--;
            if (dynamitePrefab != null)
            {
                BatDynamite d = Instantiate(dynamitePrefab, DropPoint(), Quaternion.identity);
                d.Launch(new Vector2(body.linearVelocity.x * 0.4f, -1f), FacingDirection);
            }
            Enter(BatState.Release, releaseDuration);
        }

        void BeginTired()
        {
            RaycastHit2D hit = Physics2D.Raycast(body.position, Vector2.down, 30f, GameLayers.GroundMask);
            float floor = hit.collider != null ? hit.point.y : body.position.y - hoverHeight;
            tiredTargetY = Mathf.Min(body.position.y, floor + tiredHeight);
            PixelBurst.Spawn(body.position + Vector2.up * 0.5f, sparkColor, 6, 2f, 0.4f);
            Enter(BatState.Tired, tiredDuration);
        }

        protected override void OnDamaged()
        {
            hurtUntil = Time.time + hurtPoseTime;
            // Uyurken vurulan uyanır; fitil yanarken vurulan dinamiti elinden kaçırmaz, sadece sendeler.
            if (State == BatState.Asleep)
                Wake();
        }

        int lifeId; // dirilince artar: önceki ölümün yenilgi animasyonu silah bırakmasın

        /// <summary>Yere çakılıp toza karışınca dinamit demeti yattığı yerde kalır.</summary>
        protected override bool DropsWeaponAfterDefeat => true;

        protected override void OnDied()
        {
            BatDefeat defeat = BatDefeat.Spawn(transform.position, body.linearVelocity, defeatFrames,
                spriteRenderer != null && spriteRenderer.flipX,
                spriteRenderer != null ? spriteRenderer.sortingOrder : 8, dustColor);
            int life = lifeId;
            defeat.Finished += position =>
            {
                if (life == lifeId)
                    DropWeapon(position);
            };
        }

        public override void ResetEnemy()
        {
            lifeId++;
            base.ResetEnemy();
            body.position = home;
            transform.position = home;
            body.gravityScale = 0f;
            DynamitesLeft = dynamiteCount;
            hurtUntil = 0f;
            Enter(BatState.Asleep, 0f);
        }

        Light2D globalLight;

        protected override void Start()
        {
            base.Start();
            foreach (Light2D l in FindObjectsByType<Light2D>(FindObjectsSortMode.None))
                if (l.lightType == Light2D.LightType.Global)
                {
                    globalLight = l;
                    break;
                }
            UpdateNightVisibility();
        }

        /// <summary>Ortam karardıkça hale ve ışık güçlenir; şafakta yavaşça söner.</summary>
        void UpdateNightVisibility()
        {
            float darkness = globalLight != null ? Mathf.InverseLerp(darkRange.x, darkRange.y, globalLight.intensity) : 0f;
            if (nightHalo != null)
            {
                Color c = nightHalo.color;
                c.a = nightHaloAlpha * darkness;
                nightHalo.color = c;
                nightHalo.enabled = darkness > 0.01f;
            }
            if (nightLight != null)
            {
                nightLight.intensity = nightLightIntensity * darkness;
                nightLight.enabled = darkness > 0.01f;
            }
        }

        protected override void Update()
        {
            base.Update();
            UpdateNightVisibility();
            if (spriteRenderer == null)
                return;
            Sprite frame = CurrentFrame();
            if (frame != null)
                spriteRenderer.sprite = frame;
        }

        Sprite CurrentFrame()
        {
            if (Time.time < hurtUntil && State != BatState.Tired)
                return Loop(hurtFrames, dizzyFps * 1.5f);
            switch (State)
            {
                case BatState.Asleep: return Loop(hangFrames, hangFps);
                case BatState.Wake: return Once(recoverFrames, wakeDuration);
                case BatState.Hover:
                case BatState.Return:
                    return Mathf.Abs(body.linearVelocity.x) > flySpeed * 0.6f ? Loop(glideFrames, flyFps) : Loop(flyFrames, flyFps);
                case BatState.Windup: return Loop(windupFrames, flyFps);
                case BatState.Release: return Loop(releaseFrames, flyFps);
                case BatState.Taunt: return Loop(tauntFrames, tauntFps);
                case BatState.Tired: return Time.time < hurtUntil ? Loop(hurtFrames, dizzyFps) : Loop(dizzyFrames, dizzyFps);
                case BatState.Recover: return Once(recoverFrames, recoverDuration);
                default: return null;
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
            Vector3 origin = Application.isPlaying ? (Vector3)home : transform.position;
            Gizmos.color = new Color(0.6f, 0.3f, 1f, 0.6f);
            Gizmos.DrawWireCube(origin + Vector3.down * (wakeDepth * 0.5f), new Vector3(wakeHalfWidth * 2f, wakeDepth));
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(origin + Vector3.left * leashRange, origin + Vector3.right * leashRange);
        }
    }
}
