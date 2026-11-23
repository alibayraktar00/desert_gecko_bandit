using DesertGecko.Core;
using DesertGecko.Effects;
using DesertGecko.Player;
using UnityEngine;

namespace DesertGecko.Enemies
{
    /// <summary>
    /// Siber Hatip'in ses halkası: atıldığı yöne (yatay, çapraz, yukarı) düz uçar, uçtukça büyür ve titreşir.
    /// Her karede katettiği yolu büyüyen bir daireyle denetler: katı zemin, duvar, kasa onu durdurur (arkasına
    /// saklanılır), tek yönlü platformlar durdurmaz. Gila'ya çarparsa can yakar. Düşmanlara işlemez.
    /// Aynı prefab iki saldırıda kullanılır: küçük hızlı halka ve büyüyen dev frekans dalgası (Configure).
    /// Küçük halkalar Gila'ya kilitlenir (Home): uçarken ona doğru hafifçe kıvrılır.
    /// </summary>
    public class SoundWave : MonoBehaviour
    {
        [SerializeField] int damage = 1;
        [SerializeField] float speed = 6.5f;
        [SerializeField] float lifetime = 1.6f;
        [Tooltip("Ölçek 1'deki çarpışma yarıçapı; ölçekle büyür")]
        [SerializeField] float radius = 0.3f;
        [SerializeField] float startScale = 0.8f;
        [SerializeField] float endScale = 1.5f;
        [Tooltip("Saydamlık titreşimi (Hz): ses dalgası nabız gibi atar")]
        [SerializeField] float pulseRate = 12f;
        [SerializeField] SpriteRenderer spriteRenderer;
        [SerializeField] Color burstColor = new Color32(0x56, 0xD6, 0xFF, 0xFF);

        readonly RaycastHit2D[] hits = new RaycastHit2D[4];
        ContactFilter2D filter;
        Vector2 direction = Vector2.right;
        Vector2 previous;
        float bornAt;
        Transform homingTarget;
        float homingRate; // derece/sn

        void Awake()
        {
            filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = GameLayers.GroundMask | GameLayers.PlayerMask | LayerMask.GetMask("Default"),
                useTriggers = false,
            };
            previous = transform.position;
            bornAt = Time.time;
            PlayerHealth.PlayerRespawned += Remove; // oyuncu checkpoint'te doğunca havada dalga kalmasın
        }

        void OnDestroy()
        {
            PlayerHealth.PlayerRespawned -= Remove;
        }

        /// <summary>Launch'tan önce: dalganın gücü, hızı, ömrü ve büyümesi.</summary>
        public void Configure(int damage, float speed, float lifetime, float startScale, float endScale)
        {
            this.damage = damage;
            this.speed = speed;
            this.lifetime = lifetime;
            this.startScale = startScale;
            this.endScale = endScale;
            transform.localScale = Vector3.one * startScale;
        }

        /// <summary>Uçarken hedefe doğru saniyede en fazla <paramref name="degreesPerSecond"/> derece kıvrılır.
        /// Hedef arkada kalınca (yanından geçti) kıvrılmayı bırakır: geri dönüp kovalamaz.</summary>
        public void Home(Transform target, float degreesPerSecond)
        {
            homingTarget = target;
            homingRate = degreesPerSecond;
        }

        /// <param name="dir">Uçuş yönü (normalize edilir).</param>
        /// <param name="checkFrom">İlk denetimin başlayacağı nokta (atanın ağzı).</param>
        public void Launch(Vector2 dir, Vector2 checkFrom)
        {
            direction = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;
            previous = checkFrom;
            bornAt = Time.time;
            // Halkalar sağa açılır: uçuş açısına döner. Yaylar dikeyde simetrik, aynalamaya gerek yok.
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            transform.localScale = Vector3.one * startScale;
        }

        void Update()
        {
            float age = Time.time - bornAt;
            if (age >= lifetime)
            {
                Remove();
                return;
            }
            float t = age / lifetime;
            float scale = Mathf.Lerp(startScale, endScale, 1f - (1f - t) * (1f - t)); // önce hızlı açılır
            transform.localScale = Vector3.one * scale;
            if (spriteRenderer != null)
            {
                Color c = spriteRenderer.color;
                float pulse = 0.75f + 0.25f * Mathf.Sin(age * pulseRate * Mathf.PI * 2f);
                c.a = pulse * Mathf.Clamp01((1f - t) / 0.25f); // son çeyrekte söner
                spriteRenderer.color = c;
            }

            if (homingTarget != null && homingRate > 0f)
            {
                Vector2 desired = (Vector2)homingTarget.position + Vector2.up * 0.35f - (Vector2)transform.position;
                if (desired.sqrMagnitude > 0.01f && Vector2.Dot(desired, direction) > 0f)
                {
                    float current = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                    float wanted = Mathf.Atan2(desired.y, desired.x) * Mathf.Rad2Deg;
                    float angle = Mathf.MoveTowardsAngle(current, wanted, homingRate * Time.deltaTime);
                    direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                    transform.rotation = Quaternion.Euler(0f, 0f, angle);
                }
                else
                    homingTarget = null;
            }

            Vector2 next = (Vector2)transform.position + direction * (speed * Time.deltaTime);
            Vector2 path = next - previous;
            float r = radius * scale;
            int count = Physics2D.CircleCast(previous, r, path.normalized, filter, hits, path.magnitude);
            for (int i = 0; i < count; i++)
            {
                if (hits[i].collider.usedByEffector)
                    continue; // tek yönlü platform: balkondaki Gila'ya alttan da ulaşır
                Hit(hits[i]);
                return;
            }
            transform.position = next;
            previous = next;
        }

        void Hit(RaycastHit2D hit)
        {
            var player = hit.collider.GetComponentInParent<PlayerHealth>();
            if (player != null)
                player.TakeDamage(damage, new Vector2(direction.x >= 0f ? 1f : -1f, 0f));
            PixelBurst.Spawn(hit.point, burstColor, 6 + damage * 3, 3f, 0.3f);
            Remove();
        }

        void Remove() => Destroy(gameObject);
    }
}
