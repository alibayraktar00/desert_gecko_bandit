using DesertGecko.Core;
using DesertGecko.Effects;
using UnityEngine;

namespace DesertGecko.Player
{
    /// <summary>
    /// Fırlatılan kuyruk: fizikle uçar, dönerek ilerler. Düşmana çarparsa hasar verip kırılır,
    /// zemine/duvara çarparsa ya da süresi dolarsa yok olur.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class TailProjectile : MonoBehaviour
    {
        [SerializeField] int damage = 3;
        [SerializeField] float lifetime = 3f;
        [SerializeField] float spinSpeed = 900f;
        [SerializeField] Transform visual;
        [SerializeField] Color burstColor = new Color32(0xE8, 0xA0, 0x4A, 0xFF);

        Rigidbody2D body;
        float destroyAt;
        bool broken;

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            destroyAt = Time.time + lifetime;
        }

        public void Launch(Vector2 velocity)
        {
            body.linearVelocity = velocity;
            destroyAt = Time.time + lifetime;
        }

        void Update()
        {
            if (visual != null)
                visual.Rotate(0f, 0f, -spinSpeed * Mathf.Sign(body.linearVelocity.x + 0.0001f) * Time.deltaTime);
            if (Time.time >= destroyAt)
                Destroy(gameObject);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (broken)
                return;

            if (((1 << other.gameObject.layer) & GameLayers.GroundMask) != 0)
            {
                Break();
                return;
            }

            var target = other.GetComponentInParent<IDamageable>();
            if (target == null || target is PlayerHealth || !target.IsAlive)
                return;

            target.TakeDamage(new DamageInfo(damage, body.linearVelocity.normalized, DamageKind.Tail));
            Break();
        }

        void Break()
        {
            broken = true;
            PixelBurst.Spawn(transform.position, burstColor, 6, 3f, 0.3f);
            Destroy(gameObject);
        }
    }
}
