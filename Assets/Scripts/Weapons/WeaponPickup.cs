using DesertGecko.Core;
using DesertGecko.Effects;
using DesertGecko.Player;
using UnityEngine;

namespace DesertGecko.Weapons
{
    /// <summary>
    /// Yerde duran silah. Düşman ölünce ölüm animasyonu bitene kadar gizli bekler, sonra cesedin yerinden
    /// küçük bir sekmeyle çıkar, yere oturur, süzülüp parlar. Eli boş Gila üstünden geçince alır; aynı silahı
    /// taşıyorsa mermisini tamamlar; başka silah taşıyorsa aşağı (S) basıp değiştirir.
    /// Bir süre alınmazsa yanıp söner ve kaybolur. Oyuncu checkpoint'te doğunca silinir.
    /// </summary>
    public class WeaponPickup : MonoBehaviour
    {
        const float Gravity = 26f, MaxFall = 14f, BobHeight = 0.08f, BobSpeed = 3f;
        const float GlintEvery = 1.4f, BlinkTime = 3f;
        static readonly Vector2 PickupSize = new Vector2(0.9f, 0.8f);
        static readonly Color SparkleColor = new Color32(0xFF, 0xF2, 0xB0, 0xFF);

        [SerializeField] float lifetime = 15f;

        public WeaponData Weapon { get; private set; }
        public int Ammo { get; private set; }

        SpriteRenderer sr;
        Transform visual;
        Vector2 velocity;
        float appearAt;
        float pickableAt;
        float vanishAt;
        bool shown;
        bool landed;
        float nextGlintAt;
        PlayerWeapon player;
        PlayerInputReader playerInput;
        Collider2D playerCollider;

        /// <param name="delay">Görünmeden önce beklenen süre (ölüm animasyonu).</param>
        /// <param name="pickupDelay">Göründükten sonra alınamayan süre (bırakılan silah hemen geri alınmasın).</param>
        public static WeaponPickup Spawn(WeaponData weapon, int ammo, Vector2 position, Vector2 velocity, float delay = 0f, float pickupDelay = 0.25f)
        {
            if (weapon == null)
                return null;
            var go = new GameObject("Silah_" + weapon.DisplayName);
            go.transform.position = position;
            var pickup = go.AddComponent<WeaponPickup>();
            pickup.Weapon = weapon;
            pickup.Ammo = ammo;
            pickup.velocity = velocity;
            pickup.appearAt = Time.time + delay;
            pickup.pickableAt = pickup.appearAt + pickupDelay;

            var visualGo = new GameObject("Gorsel");
            visualGo.transform.SetParent(go.transform, false);
            pickup.visual = visualGo.transform;
            pickup.sr = visualGo.AddComponent<SpriteRenderer>();
            pickup.sr.sprite = weapon.Icon;
            pickup.sr.sortingOrder = 9;
            pickup.sr.enabled = false;
            return pickup;
        }

        void Awake() => PlayerHealth.PlayerRespawned += Remove;
        void OnDestroy() => PlayerHealth.PlayerRespawned -= Remove;

        void Remove()
        {
            if (this != null)
                Destroy(gameObject);
        }

        void Update()
        {
            if (Time.time < appearAt)
                return;
            if (!shown)
                Show();

            if (!landed)
                Fall(Time.deltaTime);
            else
                visual.localPosition = new Vector3(0f, BobHeight * (1f + Mathf.Sin((Time.time - appearAt) * BobSpeed)), 0f);

            UpdateLook();
            if (Time.time >= vanishAt)
            {
                PixelBurst.Spawn(transform.position, SparkleColor, 5, 1.5f, 0.3f);
                Destroy(gameObject);
                return;
            }
            if (Time.time >= pickableAt)
                TryGive();
        }

        /// <summary>Patlamanın şok dalgası: yerdeki silah havaya sıçrar, yeniden düşüp oturur.</summary>
        public void Push(Vector2 push)
        {
            if (!shown)
                return;
            velocity = push;
            landed = false;
            visual.localPosition = Vector3.zero;
        }

        void Show()
        {
            shown = true;
            sr.enabled = true;
            vanishAt = Time.time + lifetime;
            PixelBurst.Spawn(transform.position, SparkleColor, 8, 2.5f, 0.4f);
        }

        void Fall(float dt)
        {
            velocity.y = Mathf.Max(velocity.y - Gravity * dt, -MaxFall);
            Vector2 from = transform.position;
            Vector2 step = velocity * dt;
            // Sprite ortadan pivotlu: alt kenarı yere değince otursun.
            float halfHeight = sr.sprite != null ? sr.sprite.bounds.extents.y : 0.2f;
            if (step.x != 0f && Physics2D.Raycast(from, new Vector2(Mathf.Sign(step.x), 0f), Mathf.Abs(step.x) + 0.3f, GameLayers.GroundMask))
            {
                velocity.x = 0f; // duvara sekmesin, dibine düşsün
                step.x = 0f;
            }
            if (velocity.y <= 0f)
            {
                RaycastHit2D hit = Physics2D.Raycast(from, Vector2.down, halfHeight - step.y + 0.05f, GameLayers.GroundMask);
                if (hit.collider != null)
                {
                    transform.position = new Vector2(from.x + step.x, hit.point.y + halfHeight);
                    landed = true;
                    return;
                }
            }
            transform.position = from + step;
            if (transform.position.y < -20f)
                Destroy(gameObject); // dipsiz uçuruma düştü
        }

        void UpdateLook()
        {
            // Arada bir parıltı: yerde duran silah gözden kaçmasın.
            if (landed && Time.time >= nextGlintAt)
            {
                nextGlintAt = Time.time + GlintEvery;
                PixelBurst.Spawn((Vector2)visual.position + new Vector2(Random.Range(-0.2f, 0.2f), 0.1f), SparkleColor, 3, 1.2f, 0.3f);
            }
            Color c = Color.white;
            float left = vanishAt - Time.time;
            if (left < BlinkTime && Mathf.Sin(Time.time * 30f) < 0f)
                c.a = 0.25f;
            sr.color = c;
        }

        void TryGive()
        {
            if (player == null)
            {
                player = FindAnyObjectByType<PlayerWeapon>();
                if (player == null)
                    return;
                playerInput = player.GetComponent<PlayerInputReader>();
                playerCollider = player.GetComponent<Collider2D>();
            }
            if (!player.CanPickUp)
                return;

            Bounds mine = new Bounds(transform.position, PickupSize);
            Bounds theirs = playerCollider != null ? playerCollider.bounds : new Bounds(player.transform.position, Vector3.one);
            if (!mine.Intersects(theirs))
                return;

            bool swapPressed = playerInput != null && playerInput.Move.y < -0.5f;
            if (!player.TryTake(Weapon, Ammo, swapPressed))
                return;
            PixelBurst.Spawn(transform.position, SparkleColor, 8, 2.5f, 0.35f);
            Destroy(gameObject);
        }
    }
}
