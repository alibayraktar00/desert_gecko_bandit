using DesertGecko.Core;
using DesertGecko.Effects;
using DesertGecko.Player;
using UnityEngine;

namespace DesertGecko.Enemies
{
    /// <summary>
    /// Yılanın mermisi: düz ve yatay uçar. Her karede katettiği yolu denetler, bu yüzden
    /// katman ayarı ya da Rigidbody gerekmez, hızlı olsa da duvarın içinden geçmez.
    /// Gila'ya çarparsa can yakar; zemine, duvara çarparsa ya da menzili bitince yok olur.
    /// </summary>
    public class EnemyBullet : MonoBehaviour
    {
        [SerializeField] int damage = 1;
        [SerializeField] float speed = 9f;
        [SerializeField] float lifetime = 2.5f;
        [SerializeField] float radius = 0.1f;
        [SerializeField] SpriteRenderer spriteRenderer;
        [SerializeField] Color burstColor = new Color32(0xE8, 0xC0, 0x4A, 0xFF);

        readonly RaycastHit2D[] hits = new RaycastHit2D[1];
        ContactFilter2D filter;
        int direction = 1;
        Vector2 previous;
        float destroyAt;

        void Awake()
        {
            filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = GameLayers.GroundMask | GameLayers.PlayerMask | LayerMask.GetMask("Default"),
                useTriggers = false,
            };
            previous = transform.position;
            destroyAt = Time.time + lifetime;
            PlayerHealth.PlayerRespawned += Remove; // oyuncu checkpoint'te doğunca havada mermi kalmasın
        }

        void OnDestroy()
        {
            PlayerHealth.PlayerRespawned -= Remove;
        }

        /// <param name="dir">1 sağ, -1 sol.</param>
        /// <param name="checkFrom">İlk denetimin başlayacağı nokta (yılanın gövdesi).</param>
        public void Launch(int dir, Vector2 checkFrom)
        {
            direction = dir;
            previous = checkFrom;
            destroyAt = Time.time + lifetime;
            if (spriteRenderer != null)
                spriteRenderer.flipX = dir < 0;
        }

        void Update()
        {
            if (Time.time >= destroyAt)
            {
                Remove();
                return;
            }

            Vector2 next = (Vector2)transform.position + Vector2.right * (direction * speed * Time.deltaTime);
            Vector2 path = next - previous;
            int count = Physics2D.CircleCast(previous, radius, path.normalized, filter, hits, path.magnitude);
            if (count > 0)
            {
                Hit(hits[0]);
                return;
            }
            transform.position = next;
            previous = next;
        }

        void Hit(RaycastHit2D hit)
        {
            var player = hit.collider.GetComponentInParent<PlayerHealth>();
            if (player != null)
                player.TakeDamage(damage, new Vector2(direction, 0f));
            PixelBurst.Spawn(hit.point, burstColor, 6, 3f, 0.25f);
            Remove();
        }

        void Remove() => Destroy(gameObject);
    }
}
