using DesertGecko.Core;
using DesertGecko.Effects;
using DesertGecko.Player;
using UnityEngine;

namespace DesertGecko.Collectibles
{
    /// <summary>
    /// Çöl kristali: oyuncu dokununca toplanır (+1). Seviyeye yerleştirilenler yerinde süzülür;
    /// düşmandan düşenler Launch ile fırlatılır, yere düşüp orada kalır.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class Crystal : MonoBehaviour
    {
        [SerializeField] int value = 1;
        [SerializeField] float hoverHeight = 0.08f;
        [SerializeField] float hoverSpeed = 3f;
        [Tooltip("Düşerken yerden yükseklik (sprite yarıçapı)")]
        [SerializeField] float radius = 0.25f;
        [SerializeField] float gravity = 22f;
        [Tooltip("Fırlatıldıktan sonra bu süre toplanamaz, böylece saçılması görünür")]
        [SerializeField] float pickupDelay = 0.35f;
        [SerializeField] LayerMask groundMask;
        [SerializeField] Color burstColor = new Color32(0x7F, 0xD8, 0xF0, 0xFF);

        public int Value => value;

        Vector3 restPosition;
        Vector2 velocity;
        bool falling;
        float pickupAllowedAt;
        float phase;
        bool collected;

        void Awake()
        {
            if (groundMask == 0)
                groundMask = GameLayers.GroundMask;
            restPosition = transform.position;
            phase = Random.value * 10f;
        }

        public void Launch(Vector2 launchVelocity)
        {
            velocity = launchVelocity;
            falling = true;
            pickupAllowedAt = Time.time + pickupDelay;
        }

        void Update()
        {
            if (falling)
                Fall();
            else
                transform.position = restPosition + Vector3.up * (Mathf.Sin(Time.time * hoverSpeed + phase) * hoverHeight);
        }

        void Fall()
        {
            velocity.y -= gravity * Time.deltaTime;
            Vector2 step = velocity * Time.deltaTime;
            Vector2 position = transform.position;

            if (Mathf.Abs(step.x) > 0f &&
                Physics2D.Raycast(position, new Vector2(Mathf.Sign(step.x), 0f), Mathf.Abs(step.x) + radius, groundMask))
            {
                velocity.x *= -0.4f; // duvardan sek
                step.x = 0f;
            }

            if (step.y < 0f)
            {
                RaycastHit2D ground = Physics2D.Raycast(position, Vector2.down, -step.y + radius, groundMask);
                if (ground)
                {
                    restPosition = new Vector3(position.x + step.x, ground.point.y + radius, 0f);
                    transform.position = restPosition;
                    falling = false;
                    return;
                }
            }

            transform.position = position + step;
            if (transform.position.y < -30f)
                Destroy(gameObject); // uçuruma düştü
        }

        void OnTriggerEnter2D(Collider2D other) => TryCollect(other);
        void OnTriggerStay2D(Collider2D other) => TryCollect(other);

        void TryCollect(Collider2D other)
        {
            if (collected || Time.time < pickupAllowedAt)
                return;
            var wallet = other.GetComponentInParent<PlayerWallet>();
            if (wallet == null)
                return;

            collected = true;
            wallet.Add(value);
            PixelBurst.Spawn(transform.position, burstColor, 8, 3f, 0.35f);
            Destroy(gameObject);
        }
    }
}
