using System;
using DesertGecko.Collectibles;
using DesertGecko.Core;
using DesertGecko.Effects;
using DesertGecko.Environment;
using DesertGecko.Player;
using DesertGecko.Weapons;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DesertGecko.Enemies
{
    /// <summary>
    /// Yerde yürüyen düşmanların ortak davranışı: başlangıç noktası çevresinde devriye,
    /// oyuncuyu görünce kovalama, temas hasarı, hasar alınca geri tepme ve kızarma.
    /// Ölünce patlar, kristal düşürür ve gizlenir; oyuncu yeniden doğunca baştan başlar.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public abstract class EnemyBase : MonoBehaviour, IDamageable
    {
        [Header("Can ve hasar")]
        [SerializeField] protected int maxHealth = 3;
        [SerializeField] protected int contactDamage = 1;

        [Header("Hareket")]
        [SerializeField] protected float patrolSpeed = 2f;
        [SerializeField] protected float chaseSpeed = 4f;
        [Tooltip("Devriyede başlangıç noktasından en fazla bu kadar uzaklaşır")]
        [SerializeField] protected float patrolDistance = 3f;

        [Header("Algılama")]
        [SerializeField] protected float detectRange = 5f;
        [SerializeField] protected float loseRange = 8f;
        [Tooltip("Oyuncu bundan daha yukarıda/aşağıdaysa fark edilmez")]
        [SerializeField] protected float verticalTolerance = 2.5f;
        [Tooltip("Oyuncu bundan daha aşağıdaysa kovalamaz: kenarın tepesinde onu beklemesin")]
        [SerializeField] protected float belowTolerance = 1.1f;
        [Tooltip("Oyuncu bundan yüksek bir yüzeyde duruyorsa kovalamaz: platformun altından peşinden gitmesin")]
        [SerializeField] protected float aboveTolerance = 0.6f;
        [Tooltip("Uçurum kenarına en fazla bu kadar yaklaşır: tırmanan oyuncuya çıkacak yer kalsın")]
        [SerializeField] protected float edgeMargin = 1.2f;

        [Header("Katmanlar")]
        [Tooltip("Gila tek yönlü bir platforma (veranda, balkon, çatı) çıkınca altından zıplayıp peşinden çıkar; " +
                 "alttaki katmana inince platformdan aşağı süzülür")]
        [SerializeField] bool climbsLayers = true;
        [Tooltip("En fazla bu kadar yükseğe zıplar (birim)")]
        [SerializeField] float maxClimb = 3.2f;
        [Tooltip("En fazla bu kadar aşağıdaki Gila için platformdan iner (birim)")]
        [SerializeField] float maxDrop = 8f;
        [Tooltip("Zıplamadan önce çömelme: oyuncunun görüp hazırlanma payı")]
        [SerializeField] float crouchTime = 0.18f;
        [SerializeField] float layerCooldown = 0.7f;
        [SerializeField] Color layerDustColor = new Color32(0xB0, 0x92, 0x6A, 0xFF);

        [Header("Temas")]
        [Tooltip("Can yaktıktan sonra bu kadar durur: oyuncu geri tepmeyle uzaklaşabilsin")]
        [SerializeField] float contactPause = 0.5f;

        [Header("Hasar tepkisi")]
        [SerializeField] Vector2 knockbackVelocity = new Vector2(4f, 3f);
        [SerializeField] float stunDuration = 0.3f;
        [Tooltip("Dil darbesinden sonraki sersemlik: dil az vurur ama düşmanı daha uzun durdurur")]
        [SerializeField] float tongueStunDuration = 0.6f;
        [SerializeField] float flashDuration = 0.12f;
        [Tooltip("Darbe aldıktan sonra bu kadar süre yeni darbe işlemez: peş peşe vurup kilitlenemesin, düşman karşılık verebilsin")]
        [SerializeField] float hitGuardDuration = 0.7f;
        [Tooltip("Mermi, patlama ve fırlatma gibi hızlı darbelerden sonraki kısa koruma: aynı saçmanın taneleri tek darbe sayılır, seri atış boşa gitmez")]
        [SerializeField] float rapidHitGuardDuration = 0.05f;
        [SerializeField] float guardBlinkRate = 14f;
        [SerializeField] Color flashColor = new Color(1f, 0.35f, 0.35f);
        [SerializeField] Color deathBurstColor = new Color32(0xB5, 0x45, 0x2B, 0xFF);

        [Header("Silah")]
        [Tooltip("Ölüm animasyonu bitince yerde kalan silah; boşsa silah düşürmez. Düşman her dirilişte yeniden düşürür")]
        [SerializeField] WeaponData weaponDrop;
        [Tooltip("Ölümden silahın görünmesine kadar geçen süre (ölüm animasyonu bitsin)")]
        [SerializeField] float weaponDropDelay = 0.8f;
        [Tooltip("Son düşen silahtan (hangi düşmandan olursa olsun) en az bu kadar uzakta ölürse silah bırakır. " +
                 "Kalabalık bölgeye tek silah düşer, geleni geçeni biçip silah toplama zinciri kurulmaz")]
        [SerializeField] float weaponDropSpacing = 25f;
        [Tooltip("Kaya kırabilen silah (kazma, dinamit), bu yarıçapta kırılmamış kaya varsa aralığa bakmadan her zaman düşer")]
        [SerializeField] float rockToolRadius = 40f;

        [Header("Görsel")]
        [SerializeField] protected SpriteRenderer spriteRenderer;
        [Tooltip("Sprite sola bakacak şekilde çizildiyse işaretli kalsın")]
        [SerializeField] bool spriteFacesLeft = true;
        [SerializeField] LayerMask groundMask;
        [Tooltip("Bu yüksekliğin altına düşen düşman kaybolur")]
        [SerializeField] float killY = -12f;

        public bool IsAlive => health > 0 && gameObject.activeInHierarchy;
        public int Health => health;
        public bool IsChasing { get; private set; }
        public int FacingDirection { get; private set; } = -1;

        /// <summary>false ise oyuncuyu görünce ya da vurulunca kovalamaz, devriyesine devam eder.</summary>
        protected virtual bool CanChase => true;
        /// <summary>true iken yerinde durur (örneğin nişan alırken).</summary>
        protected virtual bool HoldPosition => false;
        /// <summary>true iken saldırılar işlemez (zırh, kabuk).</summary>
        protected virtual bool Invulnerable => false;
        /// <summary>Bu darbe boşa çıkar mı (zırh, savuşturma). Tür, miktar ve yön <paramref name="hit"/> içinde.</summary>
        protected virtual bool Deflects(in DamageInfo hit) => Invulnerable;
        /// <summary>
        /// true ise başka düşmanların mermisi ona işlemez, içinden geçer (Bard ve sürüsü: yılanın kurşunu
        /// yarasaları biçmesin). Gila'nın saldırıları bundan etkilenmez.
        /// </summary>
        public virtual bool IgnoresEnemyFire => false;
        /// <summary>true ise darbe onu uçurumdan aşağı itmez (iri, ağır düşmanlar): kenardaysa yerinde sendeler.</summary>
        protected virtual bool KeepsFooting => false;
        /// <summary>false ise katmanlar arasında zıplamaz, inmez (uçanlar, sabit duranlar, iri gövdeler).</summary>
        protected virtual bool ClimbsLayers => climbsLayers && body != null && body.gravityScale > 0f && CanChase;
        protected PlayerHealth Player => player;
        /// <summary>Silah, ölüm animasyonu bu kadar sürdükten sonra görünür.</summary>
        protected virtual float WeaponDropDelay => weaponDropDelay;
        /// <summary>
        /// true ise silahı Die değil, ölüm animasyonu bitince alt sınıf bırakır (DropWeapon): ceset savrulup
        /// uzağa düşüyorsa silah cesedin yattığı yerden çıksın.
        /// </summary>
        protected virtual bool DropsWeaponAfterDefeat => false;
        public WeaponData WeaponDrop => weaponDrop;

        public event Action<EnemyBase> Died;

        protected Rigidbody2D body;
        protected BoxCollider2D box;
        Vector2 spawnPosition;
        int patrolDirection = -1;
        int health;
        float stunnedUntil;
        float flashUntil;
        float hitGuardUntil;      // dil, kuyruk, yakın dövüş
        float rapidHitGuardUntil; // mermi, patlama, fırlatma
        int lastShotId;           // son vuran atış: aynı saçmanın taneleri korumaya takılmaz
        int shotDamage;           // son atışın bu düşmana verdiği toplam hasar (atış tavanı için)
        bool weaponEarned;        // bu ölümde silah bırakılacak mı (DropWeapon da buna bakar)

        /// <summary>Bölümde son silahın düştüğü yer: düşme zara değil mesafeye bağlı.</summary>
        static Vector2 lastWeaponDropAt;
        static bool weaponDroppedHere;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void InitWeaponDrops()
        {
            ResetWeaponDrops();
            // Yeniden doğunca yerdeki silahlar silinir (WeaponPickup): diriltilen düşmanlar yine silah verebilsin.
            PlayerHealth.PlayerRespawned -= ResetWeaponDrops;
            PlayerHealth.PlayerRespawned += ResetWeaponDrops;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ResetWeaponDrops();
        static void ResetWeaponDrops() => weaponDroppedHere = false;
        float perchedSince = -1f;
        bool lootDropped;
        Color baseColor = Color.white;
        PlayerHealth player;
        PlayerController playerMovement;
        readonly Collider2D[] contactHits = new Collider2D[2];

        protected virtual void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            box = GetComponent<BoxCollider2D>();
            body.freezeRotation = true;
            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer != null)
                visualScale = spriteRenderer.transform.localScale;
            if (spriteRenderer != null)
                baseColor = spriteRenderer.color;
            if (groundMask == 0)
                groundMask = GameLayers.GroundMask;

            spawnPosition = transform.position;
            health = maxHealth;
            PlayerHealth.PlayerRespawned += ResetEnemy;
        }

        protected virtual void OnDestroy()
        {
            PlayerHealth.PlayerRespawned -= ResetEnemy;
        }

        protected virtual void Start()
        {
            player = FindAnyObjectByType<PlayerHealth>();
        }

        protected virtual void Update()
        {
            if (spriteRenderer == null)
                return;
            UpdateSquash();
            Color color = Time.time < flashUntil ? flashColor : baseColor;
            // Koruma süresinde yanıp söner: oyuncu şu an vuramayacağını görür.
            if (Time.time < hitGuardUntil && Mathf.Sin(Time.time * guardBlinkRate * Mathf.PI * 2f) > 0f)
                color.a *= 0.45f;
            spriteRenderer.color = color;
            spriteRenderer.flipX = spriteFacesLeft ? FacingDirection > 0 : FacingDirection < 0;
        }

        protected virtual void FixedUpdate()
        {
            if (player == null)
                player = FindAnyObjectByType<PlayerHealth>(); // oyuncu düşmandan sonra eklenmiş olabilir

            DealContactDamage(); // sersemlemişken de can yakar

            if (transform.position.y < killY)
            {
                gameObject.SetActive(false); // uçuruma düştü: ödül yok, oyuncu doğunca geri gelir
                return;
            }
            RestoreDropCollider();
            bool grounded = IsGrounded();
            if (!grounded)
                UnstickFromLedge();
            else
            {
                perchedSince = -1f;
                if (layerAirborne && Time.time > layerLeftAt + 0.1f)
                    Land();
            }
            if (Time.time < stunnedUntil || !grounded)
            {
                jumpAt = -1f; // darbe ya da düşüş çömelmeyi bozar
                return; // havadayken yön değiştirip titremesin
            }

            if (HoldPosition)
            {
                jumpAt = -1f;
                body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
                return;
            }

            if (jumpAt >= 0f)
            {
                // Çömeliyor: yerinde durur, süre dolunca sıçrar.
                body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
                if (Time.time >= jumpAt)
                    Jump();
                return;
            }

            UpdateAggro();
            if (TryChangeLayer())
                return;
            Move();
        }

        /// <summary>
        /// Gövdenin ortası boşlukta ama bir köşesi kenarın üstünde kaldıysa (darbe ya da geri tepme onu
        /// kenara itmiş) düşmez de: yerde sayılmadığı için hiçbir şey yapamaz, havada asılı kalır.
        /// Kısa bir süre böyle kalırsa basabildiği tarafa, kenarın üstüne geri kaydırılır.
        /// </summary>
        void UnstickFromLedge()
        {
            if (body.gravityScale <= 0f || Mathf.Abs(body.linearVelocity.y) > 0.05f)
            {
                perchedSince = -1f; // uçan ya da gerçekten düşen düşman
                return;
            }
            if (perchedSince < 0f)
                perchedSince = Time.time;
            if (Time.time - perchedSince < 0.12f)
                return;

            Bounds b = box.bounds;
            float footY = b.min.y + 0.05f;
            bool leftFoot = Physics2D.Raycast(new Vector2(b.min.x + 0.04f, footY), Vector2.down, 0.15f, groundMask);
            bool rightFoot = Physics2D.Raycast(new Vector2(b.max.x - 0.04f, footY), Vector2.down, 0.15f, groundMask);
            if (leftFoot == rightFoot)
                return;
            body.linearVelocity = new Vector2(leftFoot ? -2.5f : 2.5f, body.linearVelocity.y);
        }

        protected bool IsGrounded()
        {
            Bounds b = box.bounds;
            return Physics2D.Raycast(new Vector2(b.center.x, b.min.y + 0.05f), Vector2.down, 0.15f, groundMask);
        }

        /// <summary>Oyuncu daha yüksek bir yüzeyde duruyor. Zıplarken sayılmaz: kovalama bozulmasın.</summary>
        protected bool PlayerStandsAbove
        {
            get
            {
                if (player == null)
                    return false;
                if (playerMovement == null)
                    playerMovement = player.GetComponent<PlayerController>();
                return playerMovement != null && playerMovement.IsGrounded
                    && player.transform.position.y - transform.position.y > aboveTolerance;
            }
        }

        void UpdateAggro()
        {
            if (!CanChase || player == null || !player.IsAlive)
            {
                IsChasing = false;
                return;
            }

            Vector2 toPlayer = player.transform.position - transform.position;
            if (playerMovement == null)
                playerMovement = player.GetComponent<PlayerController>();
            // Başka katmandaki Gila: oraya zıplayıp inebiliyorsa peşini bırakmaz (TryChangeLayer).
            if (CanReachLayer(toPlayer))
            {
                if (!IsChasing)
                    IsChasing = Mathf.Abs(toPlayer.x) <= detectRange;
                else if (toPlayer.magnitude > loseRange)
                    IsChasing = false;
                return;
            }
            // Aşağıda kalan ya da duvara tırmanan oyuncuyu kenarda beklemez, devriyesine döner.
            if (toPlayer.y < -belowTolerance || PlayerStandsAbove || (playerMovement != null && playerMovement.State == PlayerController.MoveState.WallCling))
            {
                IsChasing = false;
                return;
            }

            if (!IsChasing)
                IsChasing = Mathf.Abs(toPlayer.x) <= detectRange && Mathf.Abs(toPlayer.y) <= verticalTolerance;
            else if (toPlayer.magnitude > loseRange)
                IsChasing = false;
        }

        void Move()
        {
            int direction;
            float speed;
            if (IsChasing)
            {
                float dx = player.transform.position.x - transform.position.x;
                direction = dx >= 0f ? 1 : -1;
                speed = Mathf.Abs(dx) < 0.1f ? 0f : chaseSpeed;
            }
            else
            {
                float fromSpawn = transform.position.x - spawnPosition.x;
                bool shouldTurn = IsBlocked(patrolDirection) || fromSpawn * patrolDirection > patrolDistance;
                // İki yön de kapalıysa (dar platform) her adımda dönüp durmasın.
                if (shouldTurn && !IsBlocked(-patrolDirection))
                    patrolDirection = -patrolDirection;
                direction = patrolDirection;
                speed = patrolSpeed;
            }

            // Kovalarken uçurumdan atlamaz, duvara yürümez: kenarda bekler.
            if (IsBlocked(direction))
                speed = 0f;

            FacingDirection = direction;
            body.linearVelocity = new Vector2(direction * speed, body.linearVelocity.y);
        }

        // --- Katmanlar arası: tek yönlü platformlara zıplama, platformdan inme ---

        Vector3 visualScale = Vector3.one;
        float jumpAt = -1f, jumpTargetY, layerReadyAt, layerLeftAt, squashUntil, squashStart;
        bool layerAirborne, squashIsLanding, squashApplied;
        Collider2D dropCollider;
        float dropRestoreAt;
        readonly RaycastHit2D[] layerHits = new RaycastHit2D[6];

        /// <summary>Gila'nın durduğu yüzey (yoksa null).</summary>
        Collider2D PlayerFloor()
        {
            if (playerMovement == null || !playerMovement.IsGrounded)
                return null;
            RaycastHit2D hit = Physics2D.Raycast((Vector2)player.transform.position + Vector2.up * 0.1f, Vector2.down, 0.4f, groundMask);
            return hit.collider;
        }

        /// <summary>Altındaki yüzey: tek yönlü platformsa üstünden aşağı süzülebilir.</summary>
        Collider2D OwnFloor()
        {
            Bounds b = box.bounds;
            RaycastHit2D hit = Physics2D.Raycast(new Vector2(b.center.x, b.min.y + 0.05f), Vector2.down, 0.15f, groundMask);
            // Yalnız ayağının altındaki yüzey: içinden geçtiği alçak veranda basamağı sayılmaz.
            return hit.collider != null && Mathf.Abs(hit.collider.bounds.max.y - b.min.y) < 0.1f ? hit.collider : null;
        }

        /// <summary>Gila başka bir katmanda ve oraya geçilebilir: yukarıda tek yönlü yüzeyde ya da aşağıda, biz tek yönlüdeyken.</summary>
        bool CanReachLayer(Vector2 toPlayer)
        {
            if (!ClimbsLayers || playerMovement == null || playerMovement.State == PlayerController.MoveState.WallCling)
                return false;
            if (toPlayer.y > aboveTolerance && toPlayer.y <= maxClimb + 0.2f)
            {
                Collider2D floor = PlayerFloor();
                return floor != null && floor.usedByEffector;
            }
            if (toPlayer.y < -belowTolerance && toPlayer.y >= -maxDrop)
            {
                Collider2D own = OwnFloor();
                return own != null && own.usedByEffector && playerMovement.IsGrounded;
            }
            return false;
        }

        /// <returns>Zıplamaya çömeldiyse ya da aşağı süzüldüyse true (bu adım yürümez).</returns>
        bool TryChangeLayer()
        {
            if (!IsChasing || Time.time < layerReadyAt || player == null)
                return false;
            Vector2 toPlayer = player.transform.position - transform.position;
            if (!CanReachLayer(toPlayer))
                return false;

            if (toPlayer.y < 0f)
            {
                // Aşağıdaki Gila: bastığı tek yönlü platformun içinden düşer.
                Collider2D own = OwnFloor();
                dropCollider = own;
                dropRestoreAt = Time.time + 0.45f;
                Physics2D.IgnoreCollision(box, own, true);
                body.linearVelocity = new Vector2(0f, -2f);
                LeaveGround();
                return true;
            }

            // Yukarıdaki Gila: başının üstünde, Gila'nın durduğu yüzeyin hizasında tek yönlü platform varsa zıplar.
            Bounds b = box.bounds;
            float playerFloorY = player.transform.position.y;
            var filter = new ContactFilter2D { useLayerMask = true, layerMask = groundMask, useTriggers = false };
            int count = Physics2D.Raycast(new Vector2(b.center.x, b.max.y), Vector2.up, filter, layerHits, maxClimb + 0.3f);
            for (int i = 0; i < count; i++)
            {
                Collider2D c = layerHits[i].collider;
                if (!c.usedByEffector)
                    return false; // tavan, katı blok: içinden geçilmez
                float top = c.bounds.max.y;
                if (top - b.min.y > maxClimb + 0.05f)
                    break;
                if (Mathf.Abs(top - playerFloorY) <= 0.35f)
                {
                    FacingDirectionTo(toPlayer.x);
                    jumpTargetY = top;
                    jumpAt = Time.time + crouchTime;
                    Squash(false, crouchTime);
                    return true;
                }
            }
            return false; // henüz altında değil: Gila'ya doğru yürür
        }

        void FacingDirectionTo(float dx)
        {
            if (Mathf.Abs(dx) > 0.05f)
                TurnTowards(dx > 0f ? 1 : -1);
        }

        void Jump()
        {
            jumpAt = -1f;
            float gravity = -Physics2D.gravity.y * body.gravityScale;
            float height = Mathf.Max(0.5f, jumpTargetY - box.bounds.min.y + 0.4f);
            float vy = Mathf.Sqrt(2f * gravity * height);
            body.linearVelocity = new Vector2(FacingDirection * 1.2f, vy);
            PixelBurst.Spawn(new Vector2(box.bounds.center.x, box.bounds.min.y + 0.05f), layerDustColor, 6, 2.5f, 0.35f);
            LeaveGround();
        }

        void LeaveGround()
        {
            layerAirborne = true;
            layerLeftAt = Time.time;
            layerReadyAt = Time.time + layerCooldown;
            squashUntil = 0f;
        }

        void Land()
        {
            layerAirborne = false;
            PixelBurst.Spawn(new Vector2(box.bounds.center.x, box.bounds.min.y + 0.05f), layerDustColor, 7, 2.8f, 0.4f);
            Squash(true, 0.12f);
        }

        void RestoreDropCollider()
        {
            if (dropCollider == null || Time.time < dropRestoreAt)
                return;
            // Platformun tamamen altına inene kadar bekler: içindeyken açılırsa yukarı itilirdi.
            if (box.bounds.max.y > dropCollider.bounds.min.y && box.bounds.min.y < dropCollider.bounds.max.y)
                return;
            Physics2D.IgnoreCollision(box, dropCollider, false);
            dropCollider = null;
        }

        void Squash(bool landing, float duration)
        {
            squashIsLanding = landing;
            squashStart = Time.time;
            squashUntil = Time.time + duration;
        }

        /// <summary>
        /// Zıplama görünümü: çömelirken ve yere değince basılır (alçalır, genişler), havada uzar.
        /// Kareler alttan pivotlu: ayaklar yerinde kalır.
        /// </summary>
        void UpdateSquash()
        {
            if (spriteRenderer == null || body == null || body.gravityScale <= 0f)
                return;
            bool active = Time.time < squashUntil || (layerAirborne && body.linearVelocity.y > 1f);
            if (!active && !squashApplied)
                return; // başka bir şey ölçeklemiyorsa dokunmaz (akrebin karşı duruşu kendi ölçeğini kullanır)
            squashApplied = active;
            Vector3 scale = visualScale;
            if (Time.time < squashUntil)
            {
                float k = (Time.time - squashStart) / Mathf.Max(0.01f, squashUntil - squashStart);
                float amount = squashIsLanding ? 1f - k : k; // çömelme derinleşir, iniş toparlanır
                scale = new Vector3(visualScale.x * (1f + 0.18f * amount), visualScale.y * (1f - 0.2f * amount), visualScale.z);
            }
            else if (layerAirborne && body.linearVelocity.y > 1f)
            {
                scale = new Vector3(visualScale.x * 0.9f, visualScale.y * 1.12f, visualScale.z);
            }
            spriteRenderer.transform.localScale = scale;
        }

        protected bool IsBlocked(int direction)
        {
            Bounds b = box.bounds;
            Vector2 footProbe = new Vector2(b.center.x + direction * (b.extents.x + 0.05f), b.min.y + 0.1f);
            Vector2 marginProbe = footProbe + Vector2.right * (direction * edgeMargin);
            bool groundAhead = Physics2D.Raycast(footProbe, Vector2.down, 0.6f, groundMask)
                && Physics2D.Raycast(marginProbe, Vector2.down, 0.6f, groundMask);
            bool wallAhead = Physics2D.Raycast(b.center, Vector2.right * direction, b.extents.x + 0.1f, groundMask);
            return wallAhead || !groundAhead;
        }

        void DealContactDamage()
        {
            if (contactDamage <= 0 || player == null || !player.IsAlive || player.IsInvincible)
                return;

            Bounds b = box.bounds;
            var filter = new ContactFilter2D { useLayerMask = true, layerMask = GameLayers.PlayerMask, useTriggers = false };
            if (Physics2D.OverlapBox(b.center, b.size * 0.9f, 0f, filter, contactHits) == 0)
                return;

            float dx = player.transform.position.x - transform.position.x;
            float side = Mathf.Abs(dx) > 0.1f ? Mathf.Sign(dx) : FacingDirection; // tam üst üsteyken baktığı yöne it
            player.TakeDamage(contactDamage, new Vector2(side, 0f));

            // Oyuncu geri tepme ile uzaklaşırken peşinden gidip yeniden içine girmesin.
            stunnedUntil = Time.time + contactPause;
            body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
        }

        /// <summary>Türü belirtilmemiş darbe (eski çağrılar, testler): dil darbesi gibi işlenir.</summary>
        public void TakeDamage(int amount, Vector2 knockbackDirection) =>
            TakeDamage(new DamageInfo(amount, knockbackDirection, DamageKind.Tongue));

        /// <summary>Son gelen darbe: alt sınıflar OnDamaged/OnBlocked içinde türüne bakar (patlama zırhı kırar gibi).</summary>
        protected DamageInfo LastHit { get; private set; }

        public bool TakeDamage(DamageInfo hit)
        {
            int amount = hit.Amount;
            Vector2 knockbackDirection = hit.Direction;
            if (!IsAlive || amount <= 0)
                return false;
            LastHit = hit;
            // Hızlı darbeler yalnız kısa korumaya bakar: dil darbesinden hemen sonra gelen mermi boşa gitmez.
            bool sameShot = hit.ShotId != 0 && hit.ShotId == lastShotId;
            bool guarded = hit.IsRapid ? Time.time < rapidHitGuardUntil && !sameShot : Time.time < hitGuardUntil;
            if (Deflects(hit) || guarded)
            {
                OnBlocked();
                return false;
            }

            // Atış tavanı: aynı saçmanın fazladan taneleri isabet eder ama can götürmez.
            int dealtThisShot = sameShot ? shotDamage : 0;
            if (hit.ShotCap > 0)
            {
                amount = Mathf.Min(amount, hit.ShotCap - dealtThisShot);
                if (amount <= 0)
                    return true;
            }

            health = Mathf.Max(0, health - amount);
            rapidHitGuardUntil = Time.time + rapidHitGuardDuration;
            lastShotId = hit.ShotId;
            shotDamage = dealtThisShot + amount;
            if (!hit.IsRapid)
                hitGuardUntil = Time.time + hitGuardDuration;
            flashUntil = Time.time + flashDuration;
            if (CanChase && player != null && player.IsAlive)
                IsChasing = true; // vurulan düşman saldırganı takip eder

            if (health == 0)
            {
                // Faz geçişi: ölmek yerine yeni canla ayağa kalkar (Bard insan formundan canavara döner).
                int revived = SecondWind();
                if (revived > 0)
                {
                    health = revived;
                    body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
                    return true;
                }
                // Silahla öldürmek bir an durur: son mermi, saçma, kazma oturur. Dille öldürmede akış bozulmaz.
                if (hit.Kind == DamageKind.Bullet || hit.Kind == DamageKind.Melee || hit.Kind == DamageKind.Thrown)
                    HitStop.Request(0.045f);
                Die();
                return true;
            }

            float side = Mathf.Abs(knockbackDirection.x) > 0.01f
                ? Mathf.Sign(knockbackDirection.x)
                : (player != null ? Mathf.Sign(transform.position.x - player.transform.position.x) : 1f);
            // Ağır düşman kenardaysa geriye savrulmaz, sadece yerinde sarsılır: uçurumdan itilip düşmesin.
            float pushX = KeepsFooting && IsBlocked((int)side) ? 0f : side * knockbackVelocity.x;
            body.linearVelocity = new Vector2(pushX, knockbackVelocity.y);
            stunnedUntil = Time.time + (hit.Kind == DamageKind.Tongue ? tongueStunDuration : stunDuration);
            OnDamaged();
            return true;
        }

        /// <summary>
        /// Patlamanın şok dalgası: düşman bu hızla savrulur ve kısa süre sersemler (havadayken yürümeye çalışmaz).
        /// İri, ağır düşmanlar (KeepsFooting) daha az kıpırdar.
        /// </summary>
        public void ApplyBlast(Vector2 velocity, float stun)
        {
            if (!IsAlive || velocity.sqrMagnitude < 0.01f)
                return;
            if (KeepsFooting)
                velocity *= 0.4f;
            body.linearVelocity = velocity;
            stunnedUntil = Mathf.Max(stunnedUntil, Time.time + stun);
        }

        /// <summary>Ölüm animasyonunun bittiği yerde silahı bırakır (DropsWeaponAfterDefeat).</summary>
        protected void DropWeapon(Vector2 position)
        {
            if (weaponDrop != null && weaponEarned)
                WeaponPickup.Spawn(weaponDrop, weaponDrop.Ammo, position + Vector2.up * 0.3f, new Vector2(0f, 3.5f));
        }

        /// <summary>
        /// Canı biten düşman ölmek yerine bu kadar canla devam eder (ikinci faz); 0 ise ölür.
        /// Geçişi (animasyon, dokunulmazlık) alt sınıf başlatır.
        /// </summary>
        protected virtual int SecondWind() => 0;

        protected virtual void OnDamaged() { }
        protected virtual void OnBlocked() { }
        protected virtual void OnDied() { }

        /// <summary>Bu yöne döner ve devriyesine o yönde devam eder.</summary>
        protected void TurnTowards(int direction)
        {
            patrolDirection = direction;
            FacingDirection = direction;
        }

        void Die()
        {
            PixelBurst.Spawn(box.bounds.center, deathBurstColor, 12, 4.5f, 0.5f);
            // Ödül düşman başına bir kez: oyuncu ölüp düşman dirilince yeniden kristal düşürmez.
            var drop = GetComponent<CrystalDrop>();
            if (drop != null && !lootDropped)
            {
                drop.Drop();
                lootDropped = true;
            }
            // Silah, ölüm animasyonu bitince cesedin yerinden küçük bir sekmeyle çıkar.
            weaponEarned = EarnsWeapon();
            if (weaponEarned && !DropsWeaponAfterDefeat)
                WeaponPickup.Spawn(weaponDrop, weaponDrop.Ammo, box.bounds.center, new Vector2(FacingDirection * -0.8f, 4f), WeaponDropDelay);
            OnDied();
            Died?.Invoke(this);
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Silah düşmesi zar değil kural: nasıl öldürülürse öldürülsün, son düşen silahtan weaponDropSpacing kadar
        /// uzaktaysa bırakır. Dağınık düşmanlar silah verir; birbirine yakın kalabalıkta yalnız ilk ölen bırakır.
        /// İstisnalar: AlwaysDrops silahlar (bıçak) her zaman, yakında kırılmamış kaya varken kazma ve dinamit
        /// her zaman düşer.
        /// Bölüm yüklenince ve oyuncu yeniden doğunca sıfırlanır.
        /// </summary>
        bool EarnsWeapon()
        {
            if (weaponDrop == null)
                return false;
            if (weaponDrop.AlwaysDrops)
                return true; // farenin bıçağı: hemen alınabilsin, öteki silahların sırasını da bozmaz
            Vector2 here = box.bounds.center;
            // Yol açan alet: yakında kırılacak kaya varsa kazma/dinamit her zaman düşer, oyuncu takılı kalmasın.
            bool neededForRock = weaponDrop.BreaksRocks && BreakableRock.AnyIntactNear(here, rockToolRadius);
            if (!neededForRock && weaponDroppedHere && Vector2.Distance(here, lastWeaponDropAt) < weaponDropSpacing)
                return false;
            weaponDroppedHere = true;
            lastWeaponDropAt = here;
            return true;
        }

        /// <summary>Başlangıç noktasına, tam cana ve devriyeye döner (ölüyse yeniden görünür).</summary>
        public virtual void ResetEnemy()
        {
            gameObject.SetActive(true);
            transform.position = spawnPosition;
            body.position = spawnPosition;
            body.linearVelocity = Vector2.zero;
            health = maxHealth;
            IsChasing = false;
            patrolDirection = -1;
            FacingDirection = -1;
            stunnedUntil = 0f;
            flashUntil = 0f;
            hitGuardUntil = 0f;
            rapidHitGuardUntil = 0f;
            lastShotId = 0;
            shotDamage = 0;
            perchedSince = -1f;
            jumpAt = -1f;
            layerAirborne = false;
            squashUntil = 0f;
            if (dropCollider != null)
                Physics2D.IgnoreCollision(box, dropCollider, false);
            dropCollider = null;
            if (spriteRenderer != null)
                spriteRenderer.transform.localScale = visualScale;
        }

        protected virtual void OnDrawGizmosSelected()
        {
            Vector3 origin = Application.isPlaying ? (Vector3)spawnPosition : transform.position;
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(origin + Vector3.left * patrolDistance, origin + Vector3.right * patrolDistance);
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.6f);
            Gizmos.DrawWireCube(transform.position, new Vector3(detectRange * 2f, verticalTolerance * 2f));
        }
    }
}
