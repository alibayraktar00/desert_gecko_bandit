using System;
using DesertGecko.Collectibles;
using DesertGecko.Core;
using DesertGecko.Effects;
using DesertGecko.Player;
using UnityEngine;

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

        [Header("Hasar tepkisi")]
        [SerializeField] Vector2 knockbackVelocity = new Vector2(4f, 3f);
        [SerializeField] float stunDuration = 0.3f;
        [SerializeField] float flashDuration = 0.12f;
        [SerializeField] Color flashColor = new Color(1f, 0.35f, 0.35f);
        [SerializeField] Color deathBurstColor = new Color32(0xB5, 0x45, 0x2B, 0xFF);

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
        protected PlayerHealth Player => player;

        public event Action<EnemyBase> Died;

        protected Rigidbody2D body;
        protected BoxCollider2D box;
        Vector2 spawnPosition;
        int patrolDirection = -1;
        int health;
        float stunnedUntil;
        float flashUntil;
        bool lootDropped;
        Color baseColor = Color.white;
        PlayerHealth player;
        readonly Collider2D[] contactHits = new Collider2D[2];

        protected virtual void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            box = GetComponent<BoxCollider2D>();
            body.freezeRotation = true;
            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
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
            spriteRenderer.color = Time.time < flashUntil ? flashColor : baseColor;
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
            if (Time.time < stunnedUntil || !IsGrounded())
                return; // havadayken yön değiştirip titremesin

            if (HoldPosition)
            {
                body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
                return;
            }

            UpdateAggro();
            Move();
        }

        protected bool IsGrounded()
        {
            Bounds b = box.bounds;
            return Physics2D.Raycast(new Vector2(b.center.x, b.min.y + 0.05f), Vector2.down, 0.15f, groundMask);
        }

        void UpdateAggro()
        {
            if (!CanChase || player == null || !player.IsAlive)
            {
                IsChasing = false;
                return;
            }

            Vector2 toPlayer = player.transform.position - transform.position;
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

        bool IsBlocked(int direction)
        {
            Bounds b = box.bounds;
            Vector2 footProbe = new Vector2(b.center.x + direction * (b.extents.x + 0.05f), b.min.y + 0.1f);
            bool groundAhead = Physics2D.Raycast(footProbe, Vector2.down, 0.6f, groundMask);
            bool wallAhead = Physics2D.Raycast(b.center, Vector2.right * direction, b.extents.x + 0.1f, groundMask);
            return wallAhead || !groundAhead;
        }

        void DealContactDamage()
        {
            if (contactDamage <= 0 || player == null || !player.IsAlive)
                return;

            Bounds b = box.bounds;
            var filter = new ContactFilter2D { useLayerMask = true, layerMask = GameLayers.PlayerMask, useTriggers = false };
            if (Physics2D.OverlapBox(b.center, b.size * 0.9f, 0f, filter, contactHits) == 0)
                return;

            float side = Mathf.Sign(player.transform.position.x - transform.position.x);
            player.TakeDamage(contactDamage, new Vector2(side, 0f));
        }

        public void TakeDamage(int amount, Vector2 knockbackDirection)
        {
            if (!IsAlive || amount <= 0)
                return;

            health = Mathf.Max(0, health - amount);
            flashUntil = Time.time + flashDuration;
            if (CanChase && player != null && player.IsAlive)
                IsChasing = true; // vurulan düşman saldırganı takip eder

            if (health == 0)
            {
                Die();
                return;
            }

            float side = Mathf.Abs(knockbackDirection.x) > 0.01f
                ? Mathf.Sign(knockbackDirection.x)
                : (player != null ? Mathf.Sign(transform.position.x - player.transform.position.x) : 1f);
            body.linearVelocity = new Vector2(side * knockbackVelocity.x, knockbackVelocity.y);
            stunnedUntil = Time.time + stunDuration;
            OnDamaged();
        }

        protected virtual void OnDamaged() { }
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
            OnDied();
            Died?.Invoke(this);
            gameObject.SetActive(false);
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
