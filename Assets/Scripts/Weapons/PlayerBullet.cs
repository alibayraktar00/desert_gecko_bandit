using DesertGecko.Core;
using DesertGecko.Effects;
using DesertGecko.Player;
using UnityEngine;

namespace DesertGecko.Weapons
{
    /// <summary>
    /// Gila'nın mermisi ya da saçma tanesi: düz uçar, her karede katettiği yolu denetler (hızlı olsa da duvardan geçmez).
    /// Düşmana çarparsa mermi hasarı verir; zemine çarparsa ya da menzili bitince söner. Menzilin son kısmında
    /// solar: saçma uzakta dağılıp kaybolur. Zararsız olanlar (düşman saçmasının görseli) yalnız zemine çarpar.
    /// </summary>
    public class PlayerBullet : MonoBehaviour
    {
        const float FadeFrom = 0.7f; // menzilin bu oranından sonra solar
        static readonly Color SparkColor = new Color32(0xFF, 0xD6, 0x5A, 0xFF);
        static readonly Color DustColor = new Color32(0xC8, 0x9A, 0x60, 0xFF);

        readonly RaycastHit2D[] hits = new RaycastHit2D[6];
        ContactFilter2D filter;
        SpriteRenderer sr;
        Vector2 direction;
        Vector2 previous;
        float speed;
        float range;
        float travelled;
        int damage;
        int shotId;
        bool harmless;

        /// <summary>Silahın tek mermisi (ya da saçmanın bir tanesi: <paramref name="shotId"/> atışın numarası).</summary>
        /// <param name="from">Merminin yola çıktığı nokta (göğüs): namluya kadarki yol da denetlenir, duvara dayalı atış duvardan geçmez.</param>
        public static PlayerBullet Spawn(WeaponData weapon, Vector2 from, Vector2 muzzle, Vector2 direction, int order,
            int shotId = 0, float speed = -1f, float range = -1f)
        {
            return Create(weapon.BulletSprite, weapon.Damage, speed > 0f ? speed : weapon.BulletSpeed,
                range > 0f ? range : weapon.BulletRange, from, muzzle, direction, order, shotId, false);
        }

        /// <summary>Hasar vermeyen tane: düşmanın saçmasını göstermek için. Zemine çarpınca söner.</summary>
        public static PlayerBullet SpawnHarmless(Sprite sprite, float speed, float range, Vector2 from, Vector2 muzzle, Vector2 direction, int order)
        {
            return Create(sprite, 0, speed, range, from, muzzle, direction, order, 0, true);
        }

        static PlayerBullet Create(Sprite sprite, int damage, float speed, float range, Vector2 from, Vector2 muzzle,
            Vector2 direction, int order, int shotId, bool harmless)
        {
            var go = new GameObject(harmless ? "Sacma" : "GilaMermi");
            go.transform.position = muzzle;
            go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            var bullet = go.AddComponent<PlayerBullet>();
            bullet.sr = go.AddComponent<SpriteRenderer>();
            bullet.sr.sprite = sprite;
            bullet.sr.sortingOrder = order;
            bullet.direction = direction.normalized;
            bullet.previous = from;
            bullet.speed = speed;
            bullet.range = range;
            bullet.damage = damage;
            bullet.shotId = shotId;
            bullet.harmless = harmless;
            bullet.filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = harmless ? GameLayers.GroundMask : GameLayers.GroundMask | LayerMask.GetMask("Default", GameLayers.Enemy),
                useTriggers = false,
            };
            bullet.Step(0f); // namluya kadarki yol: duvarın içinden ateş edilmesin
            return bullet;
        }

        void Awake() => PlayerHealth.PlayerRespawned += Remove;
        void OnDestroy() => PlayerHealth.PlayerRespawned -= Remove;

        void Remove()
        {
            if (this != null)
                Destroy(gameObject);
        }

        void Update() => Step(speed * Time.deltaTime);

        void Step(float distance)
        {
            Vector2 next = (Vector2)transform.position + direction * distance;
            travelled += distance;

            int count = Physics2D.Linecast(previous, next, filter, hits);
            for (int i = 0; i < count; i++)
            {
                Collider2D other = hits[i].collider;
                if (((1 << other.gameObject.layer) & GameLayers.GroundMask) != 0)
                {
                    PixelBurst.Spawn(hits[i].point, DustColor, harmless ? 2 : 5, 2.5f, 0.25f);
                    Destroy(gameObject);
                    return;
                }
                if (harmless)
                    continue;
                var target = other.GetComponentInParent<IDamageable>();
                if (target == null || target is PlayerHealth || !target.IsAlive)
                    continue;
                target.TakeDamage(new DamageInfo(damage, direction, DamageKind.Bullet, shotId));
                PixelBurst.Spawn(hits[i].point, SparkColor, 4, 3f, 0.2f);
                Destroy(gameObject);
                return;
            }

            transform.position = next;
            previous = next;
            if (travelled >= range)
            {
                Destroy(gameObject);
                return;
            }
            float fade = Mathf.InverseLerp(range * FadeFrom, range, travelled);
            if (fade > 0f)
                sr.color = new Color(1f, 1f, 1f, 1f - fade);
        }
    }
}
