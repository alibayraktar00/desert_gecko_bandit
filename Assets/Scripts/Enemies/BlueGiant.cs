using System.Collections.Generic;
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
    /// Engel tanımaz: ekranda Gila'yı görünce peşine düşer ve bırakmaz. Tek yönlü yüzeyler (veranda, tente, balkon,
    /// çatı) ona işlemez, içlerinden geçer; üstlerine takılıp kaymaz. Önündeki katı blok (hapishanenin taş katı gibi,
    /// altında sokak süren duvar) onu durdurmaz: içinden delip geçer, binanın görseli parçalanıp saçılır. Önündeki
    /// zemin basamağına (yükselen toprak) ezerek tırmanır, kenarı kırılır. Yalnız çukurun üstünden Gila'ya sıçrar.
    /// Yere çarpınca çevresindeki binaların ve eşyaların kenarları kırılır, parçalar fizikle saçılır (Shatter).
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
        [Tooltip("Dev ekranın bu kadar dışına taşsa da (görüş alanı oranı) Gila onu görmüş sayılır, peşine düşer")]
        [SerializeField] float screenMargin = 0.02f;

        [Header("Yıkım")]
        [Tooltip("Yere çarpınca bu yarıçaptaki bina ve eşya kenarları kırılır")]
        [SerializeField] float slamShatterRadius = 2.2f;
        [Tooltip("Duvarı delerken önündeki yıkımın yarıçapı")]
        [SerializeField] float plowShatterRadius = 1.1f;
        [Tooltip("Bu yüksekliğe kadar zemin basamağına ezerek çıkar; daha yüksekse bekler")]
        [SerializeField] float maxStepUp = 3.5f;
        [SerializeField] Color rubbleColor = new Color32(0x8A, 0x6A, 0x4E, 0xFF);

        [Header("Sıçrayış")]
        [Tooltip("Hazırlık + çömelme: oyuncunun uyarıyı görme payı")]
        [SerializeField] float telegraphDuration = 0.65f;
        [SerializeField] float leapHeight = 3.6f;
        [Tooltip("En uzak sıçrama; Gila daha uzaktaysa bu kadar yaklaşır")]
        [SerializeField] float maxLeapDistance = 7f;
        [Tooltip("Gila bu kadar yukarıdaki bir çatıda, balkonda olsa da üstüne sıçrar")]
        [SerializeField] float maxLeapRise = 7f;
        [Tooltip("Gila bu kadar aşağıdaysa da üstüne iner")]
        [SerializeField] float maxLeapDrop = 6f;
        [Tooltip("Yayın tepesi iniş yüzeyinin en az bu kadar üstünden geçer: gülle yukarıdan düşer")]
        [SerializeField] float leapClearance = 2.5f;
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
        [Tooltip("Hızlı düşüş: bacaklar aşağıda (şu an kullanılmıyor: dev yere top halinde iner)")]
        [SerializeField] Sprite fallFrame;
        [SerializeField] Sprite slamFrame;
        [Tooltip("Yumruklar yerde, soluklanır")]
        [SerializeField] Sprite stunnedFrame;
        [Tooltip("Ölüm: darbe, diz çökme, öne yıkılış, yere yığılma")]
        [SerializeField] Sprite[] defeatFrames;
        [Tooltip("Gila düşünce: yumruk sıkar, bağırır, kollarını kaldırıp kas gösterir")]
        [SerializeField] Sprite[] victoryFrames;
        [SerializeField] float victoryFps = 7f;

        public GiantState State { get; private set; } = GiantState.Walk;

        /// <summary>Yalnız yere çarptıktan sonraki nefes payında vurulur; kas kütlesi geri kalan her şeyi sektirir.</summary>
        protected override bool Invulnerable => State != GiantState.Stunned;
        protected override bool KeepsFooting => true;
        protected override bool ClimbsLayers => false;
        protected override bool HoldPosition => true; // yürüyüşünü kendisi yapar (Stride), engellere takılmaz
        bool PlayerDown => Player != null && !Player.IsAlive;

        Vector3 visualHome;
        readonly Collider2D[] solidHits = new Collider2D[4];
        int bodyContactDamage;
        float victoryStartedAt = -1f;
        float leapLandY;
        float stateStartedAt, stateEndsAt, nextAttackAt, leapVx, leapStartedAt, nextDustAt;
        bool aggro; // Gila'yı ekranda gördü: yeniden doğana kadar peşini bırakmaz
        readonly List<Collider2D> plowed = new List<Collider2D>(); // içinden geçtiği bloklar
        float nextPlowFxAt;
        readonly RaycastHit2D[] probeHits = new RaycastHit2D[8];

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

            UpdatePlowed();
            bool timeUp = Time.time >= stateEndsAt;
            switch (State)
            {
                case GiantState.Walk:
                    if (WantsToLeap())
                    {
                        TurnTowards(DirectionToPlayer);
                        Enter(GiantState.Telegraph, telegraphDuration);
                    }
                    else
                        Stride();
                    break;
                case GiantState.Telegraph:
                    if (timeUp)
                        Launch();
                    break;
                case GiantState.Leap:
                    // EnemyBase yerdeyken yürüyüşü uygular: kalkış anında hızı ezmesin, havada yönünü korusun.
                    body.linearVelocity = new Vector2(leapVx, body.linearVelocity.y);
                    UpdateLeapCollision();
                    if (Time.time > leapStartedAt + 0.15f && body.excludeLayers == 0 && SolidGrounded() && body.linearVelocity.y <= 0.1f)
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

        /// <summary>
        /// Gila'yı ekranda gördüyse ona yürür. Önünü katı duvar ya da çukur keserse (sırası gelince) üstünden ona
        /// sıçrar; tek yönlü yüzeyler engel sayılmaz. Gila'yı görmediyse yerinde nöbet tutar.
        /// </summary>
        void Stride()
        {
            if (!aggro && PlayerOnScreen())
                aggro = true;
            if (!aggro || PlayerDown || Player == null)
            {
                body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
                return;
            }
            if (!SolidGrounded())
                return; // basamağa tırmanırken havada: yönünü korur
            int dir = DirectionToPlayer;
            TurnTowards(dir);
            float feetY = box.bounds.min.y;
            if (SolidWallAhead(dir, out RaycastHit2D wall))
            {
                if (FloorContinuesUnder(wall, dir))
                    Plow(wall, dir); // tek başına blok: içinden delip geçer
                else
                {
                    // Yükselen zemin: ezerek üstüne çıkar, kenarı kırılır.
                    float top = ObstacleTop(wall, dir, feetY);
                    float rise = top - feetY;
                    if (rise > maxStepUp)
                    {
                        body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
                        return;
                    }
                    float gravity = -Physics2D.gravity.y * body.gravityScale;
                    body.linearVelocity = new Vector2(dir * chaseSpeed * 1.3f, Mathf.Sqrt(2f * gravity * (rise + 0.35f)));
                    Vector2 edge = new Vector2(wall.point.x, top);
                    Shatter.At(edge, plowShatterRadius, 0.7f);
                    Shatter.Chunks(edge, rubbleColor, 8, new Vector2(-dir, 0f) * 0.5f);
                    CameraShake.AddAt(edge, 0.2f);
                    return;
                }
            }
            else if (GapAhead(dir))
            {
                body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
                if (Time.time >= nextAttackAt)
                    Enter(GiantState.Telegraph, telegraphDuration); // çukurun üstünden Gila'ya sıçrar
                return;
            }
            float dx = Player.transform.position.x - transform.position.x;
            body.linearVelocity = new Vector2(Mathf.Abs(dx) < 0.3f ? 0f : dir * chaseSpeed, body.linearVelocity.y);
        }

        /// <summary>Bloğa girer: çarpışması kapanır, önü parçalanır. Bloktan çıkınca çarpışma geri açılır (ReleasePlowed).</summary>
        void Plow(RaycastHit2D wall, int dir)
        {
            Physics2D.IgnoreCollision(box, wall.collider, true);
            plowed.Add(wall.collider);
            Vector2 at = wall.point + Vector2.up * 0.5f;
            Shatter.At(at, plowShatterRadius * 1.3f, 0.9f);
            Shatter.Chunks(at, rubbleColor, 12, new Vector2(dir, 0f));
            CameraShake.AddAt(at, 0.3f);
            HitStop.Request(0.04f);
            Play(slamClip);
            nextPlowFxAt = Time.time + 0.12f;
        }

        /// <summary>Bloğun içinde yürürken önündeki görseli kırmaya devam eder; çıkınca bloğun çarpışmasını açar.</summary>
        void UpdatePlowed()
        {
            for (int i = plowed.Count - 1; i >= 0; i--)
            {
                Collider2D c = plowed[i];
                if (c == null)
                {
                    plowed.RemoveAt(i);
                    continue;
                }
                Bounds inner = box.bounds;
                inner.Expand(0.1f);
                if (inner.Intersects(c.bounds))
                {
                    if (Time.time >= nextPlowFxAt && Mathf.Abs(body.linearVelocity.x) > 0.1f)
                    {
                        nextPlowFxAt = Time.time + 0.12f;
                        Vector2 front = new Vector2(box.bounds.center.x + FacingDirection * (box.bounds.extents.x + 0.2f), box.bounds.center.y);
                        Shatter.At(front, plowShatterRadius, 0.6f);
                        Shatter.Chunks(front, rubbleColor, 3, new Vector2(FacingDirection, 0f) * 0.6f);
                    }
                    continue;
                }
                Physics2D.IgnoreCollision(box, c, false);
                plowed.RemoveAt(i);
            }
        }

        void ReleasePlowed()
        {
            foreach (Collider2D c in plowed)
                if (c != null)
                    Physics2D.IgnoreCollision(box, c, false);
            plowed.Clear();
        }

        /// <summary>Bloğun öbür yanında, ayak hizasında katı zemin sürüyor mu: sürüyorsa blok tek başına, delinir.</summary>
        bool FloorContinuesUnder(RaycastHit2D wall, int dir)
        {
            float feetY = box.bounds.min.y;
            Vector2 probe = new Vector2(wall.point.x + dir * 0.6f, feetY + 0.15f);
            return SolidRay(probe, Vector2.down, 0.4f, out _, wall.collider);
        }

        float ObstacleTop(RaycastHit2D wall, int dir, float feetY)
        {
            Vector2 probe = new Vector2(wall.point.x + dir * 0.3f, feetY + maxStepUp + 0.5f);
            return SolidRay(probe, Vector2.down, maxStepUp + 0.6f, out RaycastHit2D hit) ? hit.point.y : feetY + maxStepUp + 1f;
        }

        bool PlayerOnScreen()
        {
            if (Player == null || !Player.IsAlive)
                return false;
            Camera cam = Camera.main;
            if (cam == null)
                return Mathf.Abs(Player.transform.position.x - transform.position.x) <= detectRange;
            Vector3 v = cam.WorldToViewportPoint(transform.position + Vector3.up);
            return v.x > -screenMargin && v.x < 1f + screenMargin && v.y > -0.2f && v.y < 1.2f;
        }

        /// <summary>Katı (tek yönlü olmayan) zemine ışın: tente, balkon gibi tek yönlü yüzeyler atlanır.</summary>
        bool SolidRay(Vector2 from, Vector2 dir, float distance, out RaycastHit2D solid, Collider2D except = null)
        {
            var filter = new ContactFilter2D { useLayerMask = true, layerMask = GameLayers.GroundMask, useTriggers = false };
            int n = Physics2D.Raycast(from, dir, filter, probeHits, distance);
            solid = default;
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                Collider2D c = probeHits[i].collider;
                if (c.usedByEffector || c == except || plowed.Contains(c) || probeHits[i].distance >= best)
                    continue;
                best = probeHits[i].distance;
                solid = probeHits[i];
            }
            return best < float.MaxValue;
        }

        bool SolidGrounded()
        {
            Bounds b = box.bounds;
            return SolidRay(new Vector2(b.center.x, b.min.y + 0.05f), Vector2.down, 0.15f, out _);
        }

        bool SolidWallAhead(int dir, out RaycastHit2D wall)
        {
            Bounds b = box.bounds;
            // Gövdenin iki yüksekliğinden: alçak bir taş da, bel hizasındaki bir duvar da sayılır.
            return SolidRay(new Vector2(b.center.x, b.min.y + 0.3f), Vector2.right * dir, b.extents.x + 0.1f, out wall)
                || SolidRay(b.center, Vector2.right * dir, b.extents.x + 0.1f, out wall);
        }

        bool GapAhead(int dir)
        {
            Bounds b = box.bounds;
            Vector2 foot = new Vector2(b.center.x + dir * (b.extents.x + 0.2f), b.min.y + 0.1f);
            return !SolidRay(foot, Vector2.down, 1.2f, out _);
        }

        /// <summary>Tek yönlü yüzeylerle (veranda, tente, balkon, çatı) çarpışmaz: içlerinden geçer, üstlerine takılmaz.</summary>
        void IgnorePlatforms()
        {
            if (box == null)
                return;
            foreach (Collider2D c in FindObjectsByType<Collider2D>(FindObjectsSortMode.None))
                if (c.usedByEffector && ((1 << c.gameObject.layer) & GameLayers.GroundMask) != 0)
                    Physics2D.IgnoreCollision(box, c, true);
        }

        protected override void Start()
        {
            base.Start();
            IgnorePlatforms();
        }

        bool WantsToLeap()
        {
            if (Time.time < nextAttackAt || !SolidGrounded() || Player == null || !Player.IsAlive || !aggro)
                return false;
            // Katman fark etmez: Gila çatıda, balkonda ya da aşağıda olsa da menzildeyse üstüne sıçrar.
            Vector2 to = Player.transform.position - transform.position;
            return Mathf.Abs(to.x) <= leapRange && to.y <= maxLeapRise && to.y >= -maxLeapDrop;
        }

        void Launch()
        {
            TurnTowards(DirectionToPlayer);
            float feetY = box.bounds.min.y;
            float dx = Mathf.Clamp(Player.transform.position.x - transform.position.x, -maxLeapDistance, maxLeapDistance);
            if (Mathf.Abs(dx) < minLeap)
                dx = 0f;
            float landY = LandingHeight(ref dx, feetY);
            leapLandY = landY;

            // Yay Gila'nın yüzeyinin üstünden geçer: tepe noktası iniş yerinden en az leapClearance yüksekte.
            float gravity = -Physics2D.gravity.y * body.gravityScale;
            float rise = Mathf.Max(leapHeight, landY - feetY + leapClearance);
            float fall = rise - (landY - feetY);
            float vy = Mathf.Sqrt(2f * gravity * rise);
            float airTime = vy / gravity + Mathf.Sqrt(2f * fall / gravity);
            leapVx = dx / airTime;
            body.linearVelocity = new Vector2(leapVx, vy);
            body.excludeLayers = GameLayers.GroundMask; // yükselirken bina, saçak, çatı onu durdurmaz
            leapStartedAt = Time.time;
            PixelBurst.Spawn(new Vector2(box.bounds.center.x, feetY + 0.05f), dustColor, 10, 3f, 0.4f);
            Play(leapClip);
            Enter(GiantState.Leap);
        }

        /// <summary>
        /// Gila'nın altındaki yüzey (sokak, veranda, çatı): dev oraya iner. Altında zemin yoksa (uçurum, havadaki Gila)
        /// atlayış kısalır, devin ayağının hizasındaki en yakın zemine iner.
        /// </summary>
        float LandingHeight(ref float dx, float feetY)
        {
            float top = Mathf.Max(feetY, Player.transform.position.y) + 0.5f;
            for (int i = 0; i < 8; i++)
            {
                Vector2 probe = new Vector2(transform.position.x + dx, top);
                // Tek yönlü yüzeyler (tente, balkon) sayılmaz: içlerinden düşer, altlarındaki katı zemine iner.
                if (SolidRay(probe, Vector2.down, top - feetY + maxLeapDrop, out RaycastHit2D hit))
                    return hit.point.y;
                dx *= 0.75f;
                if (Mathf.Abs(dx) < 0.05f)
                    break;
            }
            dx = 0f;
            return feetY;
        }

        /// <summary>
        /// Zemin, düşüşte hedef yüzeye yaklaşınca yeniden işler: aradaki çatı ya da balkon onu tutmaz, Gila hangi
        /// katmandaysa (sokak, veranda, çatı) oraya iner.
        /// </summary>
        void UpdateLeapCollision()
        {
            if (body.excludeLayers == 0 || body.linearVelocity.y > 0f)
                return;
            float feetY = box.bounds.min.y;
            // Güvence: hedefin altına inmişse ya da uzun süredir havadaysa zemini her durumda açar, düşüp kaybolmasın.
            bool overdue = feetY < leapLandY - 0.8f || Time.time - leapStartedAt > 3f;
            if ((feetY <= leapLandY + 0.6f && !InsideSolid()) || overdue)
                body.excludeLayers = 0;
        }

        /// <summary>Gövde katı bir bloğun içindeyse zemin açılmaz: dev duvarın içinden fırlatılmasın, düşmeye devam eder.</summary>
        bool InsideSolid()
        {
            var filter = new ContactFilter2D { useLayerMask = true, layerMask = GameLayers.GroundMask, useTriggers = false };
            int n = Physics2D.OverlapBox(box.bounds.center, box.bounds.size * 0.8f, 0f, filter, solidHits);
            for (int i = 0; i < n; i++)
                if (!solidHits[i].usedByEffector) // tek yönlü yüzey içinden geçilir, sayılmaz
                    return true;
            return false;
        }

        void Slam()
        {
            leapVx = 0f;
            body.excludeLayers = 0;
            body.linearVelocity = Vector2.zero;
            Vector2 ground = new Vector2(transform.position.x, box.bounds.min.y);
            GiantShockwave.Spawn(ground, shockFrames, (spriteRenderer != null ? spriteRenderer.sortingOrder : 8) + 1,
                shockDamage, shockReach, shockDuration, shockHeight);
            PixelBurst.Spawn(ground + Vector2.up * 0.1f, dustColor, 16, 4f, 0.5f);
            // Çevresindeki binaların, eşyaların kenarları kırılır; toprak parçaları saçılır.
            Shatter.At(ground + Vector2.up * 0.6f, slamShatterRadius, 1f);
            Shatter.Chunks(ground + Vector2.up * 0.1f, rubbleColor, 10, Vector2.zero, 0.9f);
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
            aggro = false;
            ReleasePlowed();
            IgnorePlatforms(); // kapanıp açılınca yok sayma silinmiş olabilir
            body.excludeLayers = 0;
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
            // Gila düştü: yerdeyse kutlar (kareler bir kez oynar, son pozda kalır).
            if (PlayerDown && State == GiantState.Walk && victoryFrames != null && victoryFrames.Length > 0)
            {
                if (victoryStartedAt < 0f)
                    victoryStartedAt = Time.time;
                int i = Mathf.FloorToInt((Time.time - victoryStartedAt) * victoryFps);
                return victoryFrames[Mathf.Min(i, victoryFrames.Length - 1)];
            }
            victoryStartedAt = -1f;
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
                    return cannonballFrame; // tepeden yere çarpana kadar top halinde düşer, açılmaz
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
