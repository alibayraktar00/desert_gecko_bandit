using DesertGecko.Core;
using DesertGecko.Effects;
using UnityEngine;

namespace DesertGecko.Enemies
{
    /// <summary>
    /// Gila'nın diliyle yakalayıp savurduğu yarasa: takla atarak sahibine (Bard) uçar, çarpınca ona hasar verir ve
    /// toza karışır. Bard o sırada ölür ya da dönüşüyorsa (darbe işlemez) yarasa yine dağılır, hasar boşa gider.
    /// Yarasa kapanmış olduğu için kendi başına yaşar.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class BardBatFling : MonoBehaviour
    {
        const float SpinSpeed = 900f, HitRadius = 0.7f, MaxLifetime = 1.5f, TargetHeight = 0.9f;

        Bard target;
        int damage;
        float speed, age;
        Color dust;
        Vector2 direction;

        public static BardBatFling Spawn(Vector2 position, Bard target, Sprite sprite, int order, int damage, float speed, Color dustColor)
        {
            var fling = new GameObject("YarasaSavrulma").AddComponent<BardBatFling>();
            fling.transform.position = position;
            fling.target = target;
            fling.damage = damage;
            fling.speed = speed;
            fling.dust = dustColor;
            var sr = fling.GetComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order + 1;
            // Yarasa kareleri alt-orta pivotlu: dönerken ortasından dönsün diye görsel ayrı tutulmuyor, takla yeterince hızlı.
            PixelBurst.Spawn(position, dustColor, 6, 3f, 0.3f);
            return fling;
        }

        void Update()
        {
            age += Time.deltaTime;
            if (target != null && target.IsAlive)
            {
                Vector2 aim = (Vector2)target.transform.position + Vector2.up * TargetHeight;
                Vector2 to = aim - (Vector2)transform.position;
                if (to.magnitude <= HitRadius)
                {
                    target.TakeDamage(new DamageInfo(damage, direction, DamageKind.Thrown));
                    CameraShake.AddAt(transform.position, 0.2f);
                    Burst();
                    return;
                }
                direction = to.normalized;
            }
            else if (direction == Vector2.zero)
            {
                direction = Vector2.up;
            }
            transform.position += (Vector3)(direction * (speed * Time.deltaTime));
            transform.Rotate(0f, 0f, SpinSpeed * Time.deltaTime);
            if (age > MaxLifetime)
                Burst();
        }

        void Burst()
        {
            PixelBurst.Spawn(transform.position, dust, 14, 4.5f, 0.5f);
            Destroy(gameObject);
        }
    }
}
