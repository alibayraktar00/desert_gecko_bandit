using DesertGecko.Core;
using DesertGecko.Effects;
using DesertGecko.Player;
using UnityEngine;

namespace DesertGecko.Enemies
{
    /// <summary>
    /// Yılanın mermisi: atıldığı anda Gila'nın bulunduğu noktaya doğru düz bir çizgide uçar (takip etmez,
    /// oyuncu yerinden kayarak kaçabilir). Her karede katettiği yolu denetler, bu yüzden
    /// katman ayarı ya da Rigidbody gerekmez, hızlı olsa da duvarın içinden geçmez.
    /// Gila'ya çarparsa can yakar; zemine, duvara çarparsa ya da menzili bitince yok olur.
    /// Atan düşmanın dışındaki düşmanlara da işler: Gila mermiden kaçınca arkadaki düşman vurulabilir.
    /// </summary>
    public class EnemyBullet : MonoBehaviour
    {
        [SerializeField] int damage = 1;
        [Tooltip("Yanlışlıkla vurulan başka bir düşmana verdiği hasar")]
        [SerializeField] int enemyDamage = 2;
        [SerializeField] float speed = 9f;
        [SerializeField] float lifetime = 2.5f;
        [SerializeField] float radius = 0.1f;
        [SerializeField] SpriteRenderer spriteRenderer;
        [SerializeField] Color burstColor = new Color32(0xE8, 0xC0, 0x4A, 0xFF);

        readonly RaycastHit2D[] hits = new RaycastHit2D[4];
        ContactFilter2D filter;
        Vector2 direction = Vector2.right;
        Vector2 previous;
        float destroyAt;

        void Awake()
        {
            filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = GameLayers.GroundMask | GameLayers.PlayerMask | GameLayers.EnemyMask | LayerMask.GetMask("Default"),
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
        /// <summary>Mermiyi atan düşman: kendi gövdesine çarpmaz.</summary>
        public EnemyBase Owner { get; set; }

        public void Launch(int dir, Vector2 checkFrom) => Launch(new Vector2(dir < 0 ? -1f : 1f, 0f), checkFrom);

        /// <param name="dir">Uçuş yönü (normalize edilir).</param>
        /// <param name="checkFrom">İlk denetimin başlayacağı nokta (atanın gövdesi).</param>
        public void Launch(Vector2 dir, Vector2 checkFrom)
        {
            direction = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;
            previous = checkFrom;
            destroyAt = Time.time + lifetime;
            // Kareler sağa bakar: uçuş açısına döner, sola giderken ters durmasın diye dikey aynalanır.
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = false;
                spriteRenderer.flipY = direction.x < 0f;
            }
        }

        /// <summary>
        /// Namludan hedefe nişan yönü. Hedef namluya çok yakınsa (namlu oyuncunun içindeyse) gövdeden alınır.
        /// Açı, bakılan yönün ±maxAngle derecesiyle sınırlanır: arkaya ya da dimdik yukarı ateş etmez.
        /// </summary>
        public static Vector2 AimDirection(Vector2 body, Vector2 muzzle, Vector2 target, int facing, float maxAngle)
        {
            Vector2 from = (target.x - muzzle.x) * facing > 0.3f ? muzzle : body;
            Vector2 to = target - from;
            float angle = Mathf.Atan2(to.y, Mathf.Max(0.01f, to.x * facing)) * Mathf.Rad2Deg;
            angle = Mathf.Clamp(angle, -maxAngle, maxAngle) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(angle) * facing, Mathf.Sin(angle));
        }

        void Update()
        {
            if (Time.time >= destroyAt)
            {
                Remove();
                return;
            }

            Vector2 next = (Vector2)transform.position + direction * (speed * Time.deltaTime);
            Vector2 path = next - previous;
            int count = Physics2D.CircleCast(previous, radius, path.normalized, filter, hits, path.magnitude);
            for (int i = 0; i < count; i++)
            {
                // Tek yönlü platformlar mermiyi durdurmaz: havadaki Gila'ya alttan da ulaşabilsin.
                if (hits[i].collider.usedByEffector)
                    continue;
                var enemy = hits[i].collider.GetComponentInParent<EnemyBase>();
                if (enemy != null && (enemy == Owner || !enemy.IsAlive))
                    continue; // atan kendisi ya da ölmüş düşman
                Hit(hits[i]);
                return;
            }
            transform.position = next;
            previous = next;
        }

        void Hit(RaycastHit2D hit)
        {
            var player = hit.collider.GetComponentInParent<PlayerHealth>();
            Vector2 push = new Vector2(direction.x >= 0f ? 1f : -1f, 0f);
            if (player != null)
                player.TakeDamage(damage, push);
            else if (hit.collider.GetComponentInParent<EnemyBase>() is EnemyBase enemy)
                enemy.TakeDamage(new DamageInfo(enemyDamage, push, DamageKind.Bullet)); // dost ateşi
            PixelBurst.Spawn(hit.point, burstColor, 6, 3f, 0.25f);
            Remove();
        }

        void Remove() => Destroy(gameObject);
    }
}
