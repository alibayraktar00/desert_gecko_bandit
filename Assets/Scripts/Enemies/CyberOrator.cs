using DesertGecko.Core;
using DesertGecko.Effects;
using UnityEngine;

namespace DesertGecko.Enemies
{
    /// <summary>
    /// Siber Hatip: uzun kabanlı, akıllı gözlüklü, boynunda ses yükselteci olan konuşmacı. Yerden biraz yukarıda
    /// süzülür, yürümez: sağa sola kayar. Yerdeki alçak engeller ona işlemez, Gila'yla arasına mesafe koyar.
    ///
    /// Saldırılar:
    ///  1. Hızlı monolog: kısa hazırlık, ardından 3-4 ses halkası. Her halka o anki Gila'ya nişanlanır: yatay,
    ///     çapraz ya da yukarı (balkondaki, çatıdaki Gila). Halka zıplanarak ya da kasa arkasına saklanarak atlatılır.
    ///  2. Frekans patlaması: el uzanır, gözlük parlar, şarj sesi (uyarı); biraz daha yükselir ve Gila'ya doğru
    ///     hızla uçup büyüyen dev bir dalga yollar. Gila geri kaçar ya da yükseğe çıkar.
    /// Halkalar Gila'ya kilitlenir: uçarken ona doğru hafifçe kıvrılır.
    /// Gila çok yaklaşınca botlarını ateşleyip geriye kayar (hover-dash). Arkasında kenar varsa durmaz: kenardan
    /// aşağıdaki zemine süzülür, inerken de geri gitmeye devam eder. Yüksekliği gövdesinin altındaki en yüksek
    /// zeminden ölçülür: gövdesi kenarı tamamen geçmeden alçalmaz, köşeye takılmaz. Arkasında duvar, dipsiz çukur
    /// ya da yuva sınırı varsa (köşeye sıkıştıysa) Gila'nın üstünden aşıp öbür yanına geçer. Darbe onu itmez.
    ///
    /// Savunma ve açığı (yorulup yere düşmez):
    ///  Mermi (ya da patlama, fırlatılan silah) yiyince etrafında enerji kalkanı açılır, kalkan sürerken hiçbir darbe
    ///  işlemez. Ama konuşurken kalkanı tutamaz: hazırlık ya da atış başlayınca kalkan söner. Konuşurken vurulursa
    ///  (dil gözlüğünü çeker ya da mermi gelir) hologramı bozulur: saldırı yarıda kalır, havada sendeler, kıvılcım
    ///  saçar ve bir süre kalkan açamaz. Asıl hasar penceresi bu sendeleme.
    /// Havada süzüldüğü için dil ona böcekteki gibi kıvrılır (ITongueMagnet): dilin yolu yakınından geçse yeter.
    /// Transform botların altıdır (kare tuvallerinin altı); kareler sağa bakar.
    /// </summary>
    public class CyberOrator : EnemyBase, ITongueMagnet
    {
        public enum OratorState { Hover, Dash, Telegraph, Burst, MegaCharge, Mega, Recover, Stagger }

        [Header("Süzülme")]
        [Tooltip("Zeminin bu kadar üstünde süzülür (botların altı)")]
        [SerializeField] float hoverHeight = 0.4f;
        [SerializeField] float bobAmplitude = 0.08f;
        [SerializeField] float bobFrequency = 1.1f;
        [SerializeField] float glideSpeed = 2.2f;
        [SerializeField] float glideAcceleration = 8f;
        [Tooltip("Başlangıç noktasından en fazla bu kadar uzaklaşır")]
        [SerializeField] float leashRange = 7f;
        [Tooltip("Gila'yla arasında tutmak istediği yatay mesafe")]
        [SerializeField] float preferredRange = 3.4f;
        [Tooltip("Dilin yolu gövdenin ortasına bu kadar yaklaşırsa dil ona kıvrılır")]
        [SerializeField] float tongueCatchRadius = 1.3f;

        [Header("Algılama")]
        [SerializeField] float engageRange = 10f;
        [SerializeField] float engageHeight = 6f;

        [Header("Hover-dash")]
        [Tooltip("Gila yatayda bu kadar yaklaşınca geriye kayar")]
        [SerializeField] float dashTrigger = 1.8f;
        [SerializeField] float dashSpeed = 9f;
        [SerializeField] float dashDuration = 0.28f;
        [SerializeField] float dashCooldown = 2.2f;
        [Tooltip("Arkasında bundan az yer kaldıysa geri değil, Gila'nın üstünden aşıp öbür yanına kayar")]
        [SerializeField] float cornerRoom = 1.8f;
        [Tooltip("Gila'nın üstünden aşarken bu kadar yükselir")]
        [SerializeField] float vaultLift = 1.9f;
        [Tooltip("Kenardan en fazla bu kadar aşağıdaki zemine iner; daha derini (çukur) köşe sayılır")]
        [SerializeField] float maxDrop = 6f;

        [Header("Hızlı monolog (ses halkaları)")]
        [SerializeField] SoundWave wavePrefab;
        [SerializeField] float burstWindup = 0.4f;
        [SerializeField] int ringsMin = 3;
        [SerializeField] int ringsMax = 4;
        [SerializeField] float ringInterval = 0.22f;
        [SerializeField] int ringDamage = 1;
        [SerializeField] float ringSpeed = 6.5f;
        [SerializeField] float ringLifetime = 1.7f;
        [Tooltip("Halka uçarken Gila'ya doğru saniyede en fazla bu kadar derece döner")]
        [SerializeField] float ringHoming = 110f;
        [Tooltip("Baktığı yönden en fazla bu kadar derece yukarı/aşağı nişan alır")]
        [SerializeField] float maxAimAngle = 70f;
        [Tooltip("Transform'a göre ağız (sağa bakarken)")]
        [SerializeField] Vector2 mouthOffset = new Vector2(0.3f, 1.3f);

        [Header("Frekans patlaması (dev dalga)")]
        [Tooltip("Gözlük parlar, şarj sesi: oyuncunun kaçma payı")]
        [SerializeField] float megaTelegraph = 0.6f;
        [SerializeField] float megaRise = 0.5f;
        [SerializeField] float megaHold = 0.55f;
        [SerializeField] int megaDamage = 2;
        [SerializeField] float megaSpeed = 8f;
        [SerializeField] float megaLifetime = 1.4f;
        [Tooltip("Dev dalga Gila'ya en fazla bu kadar derece eğik nişanlanır")]
        [SerializeField] float megaAimAngle = 25f;
        [SerializeField] float megaStartScale = 1.2f;
        [SerializeField] float megaEndScale = 3.2f;
        [Tooltip("Gila bundan daha yukarıdaysa dev dalga yerine çapraz halkalar atar")]
        [SerializeField] float megaMaxHeight = 1.8f;

        [Header("Tempo ve sendeleme")]
        [SerializeField] float attackCooldown = 1.1f;
        [SerializeField] float firstAttackDelay = 0.7f;
        [SerializeField] float recoverDuration = 0.35f;
        [Tooltip("Konuşurken vurulunca hologramı bozulur: bu kadar sendeler, kalkan açamaz (asıl hasar penceresi)")]
        [SerializeField] float staggerDuration = 1.4f;
        [SerializeField] Color plasmaColor = new Color32(0x56, 0xD6, 0xFF, 0xFF);
        [SerializeField] Color glassFlashColor = new Color32(0xE6, 0xFB, 0xFF, 0xFF);
        [SerializeField] Color sparkColor = new Color32(0xFF, 0xD2, 0x4A, 0xFF);
        [SerializeField] Color smokeColor = new Color32(0x8A, 0x8A, 0x90, 0xFF);
        [SerializeField] Color dustColor = new Color32(0x9A, 0x78, 0x52, 0xFF);
        [Tooltip("Gövdenin ortası, transform'a göre: dilin yapıştığı yer")]
        [SerializeField] Vector2 bodyCenter = new Vector2(0f, 0.85f);

        [Header("Kalkan")]
        [Tooltip("Mermi, patlama ya da fırlatma yiyince bu kadar süre kalkan açık kalır, darbeler sekmez işlemez")]
        [SerializeField] float shieldDuration = 2.5f;
        [SerializeField] SpriteRenderer shieldRenderer;
        [SerializeField] float shieldAlpha = 0.6f;
        [SerializeField] AudioClip shieldClip;
        [Tooltip("Kalkanın gövdeye göre yeri (pivot alt orta)")]
        [SerializeField] Vector3 shieldOffset = new Vector3(0f, -0.15f, 0f);

        [Header("Ses")]
        [SerializeField] AudioSource audioSource;
        [SerializeField] AudioClip ringClip;
        [SerializeField] AudioClip chargeClip;
        [Tooltip("Hologram bozulunca: kıvılcım çıtırtısı")]
        [SerializeField] AudioClip overheatClip;
        [SerializeField] AudioClip dashClip;

        [Header("Kareler")]
        [SerializeField] Sprite[] hoverFrames;
        [SerializeField] float hoverFps = 8f;
        [Tooltip("El uzanır: gözlüğü düzeltir, hazırlık")]
        [SerializeField] Sprite telegraphFrame;
        [Tooltip("Başı öne, ağız açık: ses atar")]
        [SerializeField] Sprite shoutFrame;
        [Tooltip("Dengesini kaybetmiş, düşer gibi")]
        [SerializeField] Sprite staggerFrame;
        [Tooltip("Ölüm: irtifa kaybı, düşüş, diz çökme, yıkılma, serilme, başparmak, hareketsiz")]
        [SerializeField] Sprite[] defeatFrames;
        [Tooltip("Gila düşünce: sakinleşir, kollarını kavuşturur, yumruk kaldırır")]
        [SerializeField] Sprite[] victoryFrames;
        [SerializeField] float victoryFps = 7f;

        public OratorState State { get; private set; } = OratorState.Hover;

        protected override bool CanChase => false;
        protected override bool HoldPosition => true; // kendi süzülmesini aşağıda yapar
        protected override bool ClimbsLayers => false;
        bool Speaking => State == OratorState.Telegraph || State == OratorState.Burst || State == OratorState.MegaCharge || State == OratorState.Mega;
        /// <summary>Kalkan yalnız susarken tutulur: konuşurken ve sendelerken söner.</summary>
        bool Shielded => Time.time < shieldUntil && !Speaking && State != OratorState.Stagger;
        protected override bool Deflects(in DamageInfo hit) => Shielded;

        public Vector2 TongueAnchor => (Vector2)transform.position + bodyCenter;
        public float TongueCatchRadius => tongueCatchRadius;
        bool PlayerDown => Player != null && !Player.IsAlive;
        bool PlayerAlive => Player != null && Player.IsAlive;
        int DirectionToPlayer => Player != null && Player.transform.position.x >= transform.position.x ? 1 : -1;

        Vector2 home;
        Vector3 visualHome;
        float groundY;
        float stateStartedAt, stateEndsAt, nextAttackAt, nextDashAt, nextFxAt, victoryStartedAt = -1f;
        int ringsLeft, dashDirection, baseContactDamage;
        bool vaulting;      // köşeden Gila'nın üstünden aşıyor
        float floorLevel;   // yuvanın zemin yüksekliği: başka bir kata inince yuva oraya taşınır
        bool nextIsMega;
        float lift; // frekans patlamasında fazladan yükselme
        float shieldUntil;

        protected override void Awake()
        {
            base.Awake();
            home = transform.position;
            body.gravityScale = 0f;
            if (spriteRenderer != null)
                visualHome = spriteRenderer.transform.localPosition;
            baseContactDamage = contactDamage;
            groundY = floorLevel = ProbeGround(home.x, home.y + 0.5f) ?? home.y;
            Enter(OratorState.Hover);
            ShowShield(false);
        }

        void Enter(OratorState state, float duration = 0f)
        {
            State = state;
            stateStartedAt = Time.time;
            stateEndsAt = Time.time + duration;
            if (Speaking || state == OratorState.Stagger)
                shieldUntil = 0f; // konuşurken kalkanı tutamaz
        }

        void OnEnable() => TongueMagnets.Register(this);
        void OnDisable() => TongueMagnets.Unregister(this);

        protected override void FixedUpdate()
        {
            base.FixedUpdate();
            if (!isActiveAndEnabled)
                return;
            body.gravityScale = 0f;

            TrackGround();

            bool timeUp = Time.time >= stateEndsAt;
            switch (State)
            {
                case OratorState.Hover:
                    HoverAround();
                    break;
                case OratorState.Dash:
                    // Geri kayış ya da Gila'nın üstünden aşış (yükselip öbür yanına süzülür).
                    lift = vaulting ? Mathf.MoveTowards(lift, vaultLift, vaultLift * 8f * Time.fixedDeltaTime)
                                    : Mathf.MoveTowards(lift, 0f, Time.fixedDeltaTime);
                    // Üstünden aşarken Gila'nın yanındaki boşluk sorulmaz; geri kayışta kenardan aşağı süzülebilir.
                    bool roomAhead = vaulting ? !WallAhead(dashDirection) : CanMove(dashDirection, 0.3f);
                    body.linearVelocity = new Vector2(roomAhead ? dashDirection * dashSpeed : 0f, HeightVelocity());
                    if (Time.time >= nextFxAt)
                    {
                        nextFxAt = Time.time + 0.05f;
                        PixelBurst.Spawn(body.position, plasmaColor, 3, 1.5f, 0.25f);
                    }
                    if (timeUp)
                    {
                        vaulting = false;
                        contactDamage = baseContactDamage;
                        Enter(OratorState.Hover);
                    }
                    break;
                case OratorState.Telegraph:
                    Settle();
                    if (timeUp)
                    {
                        ringsLeft = Random.Range(ringsMin, ringsMax + 1);
                        Enter(OratorState.Burst);
                        FireRing();
                    }
                    break;
                case OratorState.Burst:
                    Settle();
                    if (Time.time >= stateEndsAt)
                    {
                        if (ringsLeft > 0 && PlayerAlive)
                            FireRing();
                        else
                            FinishSpeech();
                    }
                    break;
                case OratorState.MegaCharge:
                    lift = Mathf.MoveTowards(lift, megaRise, megaRise / megaTelegraph * Time.fixedDeltaTime);
                    Settle();
                    if (Time.time >= nextFxAt)
                    {
                        nextFxAt = Time.time + 0.08f;
                        PixelBurst.Spawn(MouthPoint() + Vector2.up * 0.1f, glassFlashColor, 2, 1.2f, 0.2f); // gözlük parlar
                    }
                    if (timeUp)
                        FireMega();
                    break;
                case OratorState.Mega:
                    Settle();
                    if (timeUp)
                        FinishSpeech();
                    break;
                case OratorState.Recover:
                    lift = Mathf.MoveTowards(lift, 0f, Time.fixedDeltaTime);
                    Settle();
                    if (timeUp)
                        Enter(OratorState.Hover);
                    break;
                case OratorState.Stagger:
                    // Hologram bozuk: havada sendeler, boynundan kıvılcım ve duman çıkar. Yere düşmez.
                    lift = Mathf.MoveTowards(lift, 0f, Time.fixedDeltaTime);
                    body.linearVelocity = new Vector2(body.linearVelocity.x * 0.9f, HeightVelocity());
                    if (Time.time >= nextFxAt)
                    {
                        nextFxAt = Time.time + 0.18f;
                        Vector2 neck = (Vector2)transform.position + new Vector2(FacingDirection * 0.2f, 1.15f);
                        PixelBurst.Spawn(neck, sparkColor, 2, 2.5f, 0.2f);
                        PixelBurst.Spawn(neck + Vector2.up * 0.2f, smokeColor, 2, 0.8f, 0.6f);
                    }
                    if (timeUp)
                    {
                        nextAttackAt = Time.time + attackCooldown;
                        Enter(OratorState.Hover);
                    }
                    break;
            }
        }

        /// <summary>Gila'yla arasını ayarlar, sırası gelince konuşmaya başlar. Gila yoksa yuvası çevresinde süzülür.</summary>
        void HoverAround()
        {
            lift = Mathf.MoveTowards(lift, 0f, Time.fixedDeltaTime);
            if (!PlayerAlive || !Engaged())
            {
                if (PlayerDown)
                {
                    Glide(0f);
                    return;
                }
                float dx = home.x - body.position.x;
                Glide(Mathf.Abs(dx) > 0.2f ? Mathf.Sign(dx) * glideSpeed * 0.6f : 0f);
                return;
            }

            int toPlayer = DirectionToPlayer;
            TurnTowards(toPlayer);
            Vector2 offset = Player.transform.position - transform.position;

            if (Mathf.Abs(offset.x) < dashTrigger && Mathf.Abs(offset.y) < 1.6f && Time.time >= nextDashAt)
            {
                // Arkasında yer varsa geri kayar; köşeye sıkıştıysa Gila'nın üstünden aşıp öbür yanına geçer.
                vaulting = !CanMove(-toPlayer, cornerRoom);
                dashDirection = vaulting ? toPlayer : -toPlayer;
                if (vaulting)
                    contactDamage = 0; // üstünden geçerken çarpıp can yakmasın
                nextDashAt = Time.time + dashCooldown;
                Play(dashClip);
                PixelBurst.Spawn(body.position, plasmaColor, 8, 3f, 0.3f);
                Enter(OratorState.Dash, dashDuration);
                return;
            }

            if (Time.time >= nextAttackAt)
            {
                StartSpeech(offset);
                return;
            }

            float distance = Mathf.Abs(offset.x);
            float want = 0f;
            if (distance < preferredRange - 0.8f)
                want = -toPlayer;
            else if (distance > preferredRange + 0.8f)
                want = toPlayer;
            Glide(want * glideSpeed);
        }

        void StartSpeech(Vector2 offset)
        {
            // Dev dalga yatay gider: Gila çok yukarıdaysa yerine çapraz halkalar.
            bool mega = nextIsMega && offset.y < megaMaxHeight;
            nextIsMega = !nextIsMega;
            if (mega)
            {
                Play(chargeClip);
                Enter(OratorState.MegaCharge, megaTelegraph);
            }
            else
                Enter(OratorState.Telegraph, burstWindup);
        }

        void FireRing()
        {
            ringsLeft--;
            stateEndsAt = Time.time + ringInterval;
            if (Player == null)
                return;
            TurnTowards(DirectionToPlayer);
            Vector2 mouth = MouthPoint();
            Vector2 target = (Vector2)Player.transform.position + Vector2.up * 0.35f;
            Vector2 dir = EnemyBullet.AimDirection((Vector2)transform.position + Vector2.up * 1.1f, mouth, target, FacingDirection, maxAimAngle);
            SpawnWave(mouth, dir, ringDamage, ringSpeed, ringLifetime, 0.7f, 1.4f, ringHoming);
            Play(ringClip);
        }

        void FireMega()
        {
            Vector2 mouth = MouthPoint();
            if (Player != null)
                TurnTowards(DirectionToPlayer);
            Vector2 dir = new Vector2(FacingDirection, 0f);
            if (Player != null)
                dir = EnemyBullet.AimDirection((Vector2)transform.position + Vector2.up * 1.1f, mouth,
                    (Vector2)Player.transform.position + Vector2.up * 0.35f, FacingDirection, megaAimAngle);
            SpawnWave(mouth, dir, megaDamage, megaSpeed, megaLifetime, megaStartScale, megaEndScale, 0f);
            Play(ringClip);
            CameraShake.AddAt(mouth, 0.25f);
            PixelBurst.Spawn(mouth, plasmaColor, 12, 4f, 0.35f);
            Enter(OratorState.Mega, megaHold);
        }

        void SpawnWave(Vector2 at, Vector2 dir, int damage, float speed, float lifetime, float startScale, float endScale, float homing)
        {
            if (wavePrefab == null)
                return;
            SoundWave wave = Instantiate(wavePrefab, at, Quaternion.identity);
            wave.Configure(damage, speed, lifetime, startScale, endScale);
            if (homing > 0f && Player != null)
                wave.Home(Player.transform, homing);
            wave.Launch(dir, (Vector2)transform.position + Vector2.up * 1.1f);
        }

        void FinishSpeech()
        {
            nextAttackAt = Time.time + attackCooldown;
            Enter(OratorState.Recover, recoverDuration);
        }

        protected override void OnDamaged()
        {
            // Konuşurken vuruldu (dil gözlüğünü çekti ya da mermi geldi): hologram bozulur, saldırı yarıda kalır.
            if (Speaking)
            {
                PixelBurst.Spawn(MouthPoint() + Vector2.up * 0.1f, glassFlashColor, 6, 3f, 0.25f);
                PixelBurst.Spawn((Vector2)transform.position + new Vector2(FacingDirection * 0.2f, 1.15f), sparkColor, 10, 3.5f, 0.35f);
                Play(overheatClip);
                Enter(OratorState.Stagger, staggerDuration);
                return;
            }
            // Susarken mermi yedi: kalkan açılır. Sendelerken açılamaz.
            if (LastHit.IsRapid && State != OratorState.Stagger)
            {
                shieldUntil = Time.time + shieldDuration;
                PixelBurst.Spawn((Vector2)transform.position + Vector2.up * 0.9f, plasmaColor, 10, 3f, 0.3f);
                Play(shieldClip);
            }
        }

        protected override void OnBlocked()
        {
            // Darbe kalkandan seker.
            PixelBurst.Spawn((Vector2)transform.position + Vector2.up * 0.9f, glassFlashColor, 5, 3.5f, 0.25f);
            Play(shieldClip);
        }

        void ShowShield(bool on)
        {
            if (shieldRenderer != null && shieldRenderer.gameObject.activeSelf != on)
                shieldRenderer.gameObject.SetActive(on);
        }

        protected override float WeaponDropDelay => (defeatFrames != null ? defeatFrames.Length : 0) * 0.09f + 1f;

        protected override void OnDied()
        {
            // Botlar kısa devre: kıvılcım saçar, ceset yere düşer.
            Vector2 ground = new Vector2(transform.position.x, groundY);
            PixelBurst.Spawn((Vector2)transform.position + Vector2.up * 0.1f, sparkColor, 12, 4f, 0.4f);
            PixelBurst.Spawn((Vector2)transform.position + Vector2.up * 0.1f, plasmaColor, 10, 3f, 0.4f);
            ArmadilloDefeat.Spawn(ground, defeatFrames, spriteRenderer != null && spriteRenderer.flipX,
                spriteRenderer != null ? spriteRenderer.sortingOrder : 8, dustColor);
        }

        public override void ResetEnemy()
        {
            base.ResetEnemy();
            body.gravityScale = 0f;
            lift = 0f;
            nextIsMega = false;
            nextAttackAt = Time.time + firstAttackDelay;
            nextDashAt = 0f;
            shieldUntil = 0f;
            ShowShield(false);
            victoryStartedAt = -1f;
            vaulting = false;
            contactDamage = baseContactDamage;
            home = transform.position;
            groundY = floorLevel = ProbeGround(home.x, home.y + 0.5f) ?? home.y;
            Enter(OratorState.Hover);
        }

        protected override void Start()
        {
            base.Start();
            nextAttackAt = Time.time + firstAttackDelay;
        }

        bool Engaged()
        {
            Vector2 offset = Player.transform.position - transform.position;
            return Mathf.Abs(offset.x) <= engageRange && Mathf.Abs(offset.y) <= engageHeight
                && Mathf.Abs(Player.transform.position.x - home.x) <= leashRange + engageRange * 0.5f;
        }

        /// <summary>Yatayda kayar; yuva sınırını, duvarı ve uçurum kenarını aşmaz. Dikeyde süzülme yüksekliğine oturur.</summary>
        void Glide(float targetVx)
        {
            if (targetVx != 0f)
            {
                int dir = targetVx > 0f ? 1 : -1;
                if (!CanMove(dir, 0.3f))
                    targetVx = 0f;
            }
            float vx = Mathf.MoveTowards(body.linearVelocity.x, targetVx, glideAcceleration * Time.fixedDeltaTime);
            body.linearVelocity = new Vector2(vx, HeightVelocity());
        }

        /// <summary>Saldırırken yerinde durur (yalnız yüksekliğini tutar).</summary>
        void Settle()
        {
            float vx = Mathf.MoveTowards(body.linearVelocity.x, 0f, glideAcceleration * 2f * Time.fixedDeltaTime);
            body.linearVelocity = new Vector2(vx, HeightVelocity());
        }

        float HeightVelocity()
        {
            float bob = Mathf.Sin(Time.time * bobFrequency * Mathf.PI * 2f) * bobAmplitude;
            float targetY = groundY + hoverHeight + lift + bob;
            return Mathf.Clamp((targetY - body.position.y) * 6f, -4f, 4f);
        }

        /// <summary>
        /// Süzülme yüksekliğinin ölçüldüğü zemin: gövdenin iki kenarının ve ortasının altındaki en yüksek yüzey.
        /// Kenardan çıkarken gövdesi tamamen boşluğa geçene dek üst zeminde kalır, sonra aşağı süzülür (köşeye
        /// sürtünüp takılmaz). Altında hiç zemin yoksa son yüksekliği korur. Başka bir kata inince yuvası oraya taşınır.
        /// </summary>
        void TrackGround()
        {
            Bounds b = box.bounds;
            float from = body.position.y + 0.6f;
            float? best = null;
            foreach (float x in new[] { b.min.x + 0.05f, b.center.x, b.max.x - 0.05f })
            {
                float? top = ProbeGround(x, from, maxDrop + 1f);
                if (top.HasValue && (!best.HasValue || top.Value > best.Value))
                    best = top;
            }
            if (!best.HasValue)
                return;
            groundY = best.Value;
            if (Mathf.Abs(groundY - floorLevel) > 0.5f && Mathf.Abs(body.position.y - (groundY + hoverHeight + lift)) < 0.3f)
            {
                floorLevel = groundY; // yeni kata indi/çıktı: yuva sınırı buradan ölçülür, eski yere dönmeye çalışmaz
                home = body.position;
            }
        }

        /// <summary>Gövde hizasında katı duvar (tek yönlü yüzeyler sayılmaz).</summary>
        bool WallAhead(int dir)
        {
            Bounds b = box.bounds;
            RaycastHit2D[] hits = Physics2D.RaycastAll(b.center, Vector2.right * dir, b.extents.x + 0.15f, GameLayers.GroundMask);
            foreach (RaycastHit2D hit in hits)
                if (!hit.collider.usedByEffector)
                    return true;
            return false;
        }

        /// <summary>
        /// Bu yöne bu kadar gidebilir mi: önünde duvar yok, yuva sınırı içinde ve gideceği yerin altında en fazla
        /// maxDrop aşağıda zemin var (kenardan aşağı süzülebilir, dipsiz çukura gitmez).
        /// </summary>
        bool CanMove(int dir, float distance)
        {
            if (WallAhead(dir))
                return false;
            Bounds b = box.bounds;
            float x = b.center.x + dir * (b.extents.x + distance);
            if (Mathf.Abs(x - home.x) > leashRange)
                return false;
            float? top = ProbeGround(x, body.position.y + 0.6f, maxDrop + 1f);
            return top.HasValue && groundY - top.Value <= maxDrop;
        }

        /// <summary>Altındaki zeminin üstü (tek yönlü platformlar dahil); yoksa null.</summary>
        float? ProbeGround(float x, float fromY, float distance = 4f)
        {
            RaycastHit2D hit = Physics2D.Raycast(new Vector2(x, fromY), Vector2.down, distance, GameLayers.GroundMask);
            return hit ? hit.point.y : (float?)null;
        }

        Vector2 MouthPoint() => (Vector2)transform.position + new Vector2(mouthOffset.x * FacingDirection, mouthOffset.y);

        void Play(AudioClip clip)
        {
            if (audioSource != null && clip != null)
                audioSource.PlayOneShot(clip);
        }

        protected override void Update()
        {
            base.Update();
            if (spriteRenderer == null)
                return;
            Sprite frame = CurrentFrame();
            if (frame != null)
                spriteRenderer.sprite = frame;

            // Frekans şarjında titrer: dalga geliyor.
            Vector3 offset = Vector3.zero;
            if (State == OratorState.MegaCharge && Time.time - stateStartedAt > megaTelegraph * 0.4f)
                offset.x = (Mathf.FloorToInt(Time.time * 30f) % 2 == 0 ? 1f : -1f) / 32f;
            spriteRenderer.transform.localPosition = visualHome + offset;

            // Kalkan nabız gibi atar, bitmeden son 0.5 sn'de yanıp söner: Gila ne zaman vurabileceğini görür.
            bool shielded = Shielded;
            ShowShield(shielded);
            if (shielded && shieldRenderer != null)
            {
                float left = shieldUntil - Time.time;
                float a = shieldAlpha * (0.8f + 0.2f * Mathf.Sin(Time.time * 9f));
                if (left < 0.5f && Mathf.FloorToInt(Time.time * 16f) % 2 == 0)
                    a *= 0.25f;
                Color c = shieldRenderer.color;
                c.a = a;
                shieldRenderer.color = c;
                shieldRenderer.transform.localPosition = spriteRenderer.transform.localPosition + shieldOffset;
            }
        }

        Sprite CurrentFrame()
        {
            if (PlayerDown && State == OratorState.Hover && victoryFrames != null && victoryFrames.Length > 0)
            {
                if (victoryStartedAt < 0f)
                    victoryStartedAt = Time.time;
                int i = Mathf.FloorToInt((Time.time - victoryStartedAt) * victoryFps);
                return victoryFrames[Mathf.Min(i, victoryFrames.Length - 1)];
            }
            victoryStartedAt = -1f;
            switch (State)
            {
                case OratorState.Telegraph:
                case OratorState.MegaCharge:
                    return telegraphFrame;
                case OratorState.Burst:
                case OratorState.Mega:
                    return shoutFrame;
                case OratorState.Stagger:
                    return staggerFrame;
                default:
                    if (hoverFrames == null || hoverFrames.Length == 0)
                        return null;
                    return hoverFrames[Mathf.FloorToInt(Time.time * hoverFps) % hoverFrames.Length];
            }
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            Vector3 center = Application.isPlaying ? (Vector3)home : transform.position;
            Gizmos.color = new Color(0.3f, 0.85f, 1f, 0.7f);
            Gizmos.DrawWireCube(center + Vector3.up * 0.8f, new Vector3(leashRange * 2f, 1.6f));
            Gizmos.color = new Color(1f, 0.8f, 0.3f, 0.5f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * 1f, new Vector3(engageRange * 2f, engageHeight * 2f));
        }
    }
}
