using DesertGecko.Core;
using DesertGecko.Effects;
using DesertGecko.Player;
using UnityEngine;

namespace DesertGecko.Enemies
{
    /// <summary>
    /// Haydut Yarasa'nın bıraktığı dinamit: dönerek düşer, fitilinden kıvılcım saçar.
    /// Zemine ya da Gila'ya değince patlar; patlama alanındaki Gila can kaybeder.
    /// Rigidbody kullanmaz, EnemyBullet gibi her karede katettiği yolu denetler.
    /// </summary>
    public class BatDynamite : MonoBehaviour
    {
        [SerializeField] int damage = 1;
        [SerializeField] float gravity = 16f;
        [SerializeField] float maxFallSpeed = 12f;
        [SerializeField] float radius = 0.15f;
        [SerializeField] float blastRadius = 1.35f;
        [Tooltip("Hiçbir şeye değmezse bu kadar sonra havada patlar")]
        [SerializeField] float fuseTime = 3f;
        [SerializeField] float spinSpeed = 360f;
        [SerializeField] Transform visual;
        [SerializeField] Color fireColor = new Color32(0xFF, 0x9A, 0x2E, 0xFF);
        [SerializeField] Color flashColor = new Color32(0xFF, 0xE8, 0x7A, 0xFF);
        [SerializeField] Color smokeColor = new Color32(0x6E, 0x5E, 0x52, 0xFF);

        readonly RaycastHit2D[] hits = new RaycastHit2D[1];
        readonly Collider2D[] blastHits = new Collider2D[4];
        ContactFilter2D filter;
        Vector2 velocity;
        public float Gravity => gravity;
        float explodeAt, nextSparkAt;
        int spinDirection = 1;

        void Awake()
        {
            filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = GameLayers.GroundMask | GameLayers.PlayerMask,
                useTriggers = false,
            };
            explodeAt = Time.time + fuseTime;
            PlayerHealth.PlayerRespawned += Remove; // oyuncu doğunca havada dinamit kalmasın
        }

        void OnDestroy()
        {
            PlayerHealth.PlayerRespawned -= Remove;
        }

        /// <param name="startVelocity">Bırakıldığı andaki hız.</param>
        /// <param name="facing">Bırakan yarasanın baktığı yön; dinamit o yöne döner.</param>
        public void Launch(Vector2 startVelocity, int facing)
        {
            velocity = startVelocity;
            spinDirection = facing >= 0 ? -1 : 1;
            explodeAt = Time.time + fuseTime;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (Time.time >= explodeAt)
            {
                Explode(transform.position);
                return;
            }

            velocity.y = Mathf.Max(velocity.y - gravity * dt, -maxFallSpeed);
            Vector2 from = transform.position;
            Vector2 step = velocity * dt;
            if (step.sqrMagnitude > 0f && Physics2D.CircleCast(from, radius, step.normalized, filter, hits, step.magnitude) > 0)
            {
                Explode(hits[0].centroid);
                return;
            }
            transform.position = from + step;
            if (visual != null)
                visual.Rotate(0f, 0f, spinDirection * spinSpeed * dt);

            if (Time.time >= nextSparkAt)
            {
                nextSparkAt = Time.time + 0.07f;
                PixelBurst.Spawn(FuseTip(), flashColor, 1, 1.5f, 0.15f);
            }
        }

        Vector2 FuseTip() => visual != null ? (Vector2)(visual.position + visual.up * 0.25f) : (Vector2)transform.position;

        void Explode(Vector2 at)
        {
            PixelBurst.Spawn(at, flashColor, 10, 6f, 0.25f);
            PixelBurst.Spawn(at, fireColor, 18, 4.5f, 0.45f);
            PixelBurst.Spawn(at + Vector2.up * 0.2f, smokeColor, 12, 2.2f, 0.8f);
            ExplosionFx.Spawn(at, blastRadius * 0.8f, 10, 5); // şok halkası, ışık, uçuşan toprak
            CameraShake.AddAt(at, 0.5f);

            var blastFilter = new ContactFilter2D { useLayerMask = true, layerMask = GameLayers.PlayerMask, useTriggers = false };
            int count = Physics2D.OverlapCircle(at, blastRadius, blastFilter, blastHits);
            for (int i = 0; i < count; i++)
            {
                var player = blastHits[i].GetComponentInParent<PlayerHealth>();
                if (player == null)
                    continue;
                float side = Mathf.Sign(player.transform.position.x - at.x);
                player.TakeDamage(damage, new Vector2(side == 0f ? 1f : side, 0f));
                break;
            }
            Remove();
        }

        void Remove()
        {
            if (this != null)
                Destroy(gameObject);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, blastRadius);
        }
    }
}
