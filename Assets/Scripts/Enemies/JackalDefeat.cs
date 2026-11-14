using DesertGecko.Core;
using DesertGecko.Effects;
using UnityEngine;

namespace DesertGecko.Enemies
{
    /// <summary>
    /// Çakalın (ve şerifin) komik yenilgisi: gövde geriye doğru uçup yere kapaklanır, dev sombrero havada asılı kalıp
    /// sallanarak yavaşça düşer. Düşman kapanmış olduğu için kendi başına yaşar ve süresi bitince silinir.
    /// </summary>
    public class JackalDefeat : MonoBehaviour
    {
        const float BodyGravity = 22f, HatPopSpeed = 4.5f, HatDrag = 9f, HatFallSpeed = 1.1f, HatSwayWidth = 1.6f;
        const float LingerAfterLanding = 1.2f, MaxLifetime = 6f;

        Transform bodyPart, hatPart;
        Vector2 bodyVelocity;
        float hatVelocityY, hatStartX, age, landedAt = -1f, bodyLandedAt;
        SpriteRenderer bodyRenderer;
        Sprite[] fallFrames, groundFrames;
        float frameRate;
        bool bodyLanded, hatLanded;

        /// <summary>Gövde yere oturup yenilgi bittiğinde, gövdenin yattığı yer. Uçuruma düşerse çağrılmaz.</summary>
        public event System.Action<Vector2> Finished;

        public static JackalDefeat Spawn(Vector2 position, Sprite defeat, Sprite hat, bool flipX, int order, int knockDirection, float hatHeight = 0.95f)
        {
            var effect = new GameObject("CakalYenilgi").AddComponent<JackalDefeat>();
            effect.bodyPart = Part(effect.transform, "Govde", defeat, flipX, order, position);
            effect.bodyRenderer = effect.bodyPart.GetComponent<SpriteRenderer>();
            effect.hatPart = Part(effect.transform, "Sapka", hat, flipX, order + 1, position + Vector2.up * hatHeight);
            effect.bodyVelocity = new Vector2(knockDirection * 7f, 6f);
            effect.hatVelocityY = HatPopSpeed;
            effect.hatStartX = position.x;
            effect.hatLanded = hat == null; // şapkasız yenilgi (barmen): beklenecek şapka yok
            return effect;
        }

        /// <summary>
        /// Gövdeyi kare kare oynatır (şerif): havadayken <paramref name="fall"/>, yere değince <paramref name="ground"/>;
        /// ikisi de son karesinde kalır. Savrulma hızı da buradan yumuşatılabilir.
        /// </summary>
        public void Animate(Sprite[] fall, Sprite[] ground, float fps, Vector2 knockSpeed)
        {
            fallFrames = fall;
            groundFrames = ground;
            frameRate = fps;
            bodyVelocity = new Vector2(Mathf.Sign(bodyVelocity.x) * knockSpeed.x, knockSpeed.y);
            ShowFrame();
        }

        void ShowFrame()
        {
            Sprite[] frames = bodyLanded ? groundFrames : fallFrames;
            if (frames == null || frames.Length == 0)
                return;
            float since = bodyLanded ? age - bodyLandedAt : age;
            bodyRenderer.sprite = frames[Mathf.Min(Mathf.FloorToInt(since * frameRate), frames.Length - 1)];
        }

        static Transform Part(Transform parent, string name, Sprite sprite, bool flipX, int order, Vector2 position)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(parent);
            sr.transform.position = position;
            sr.sprite = sprite;
            sr.flipX = flipX;
            sr.sortingOrder = order;
            return sr.transform;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            MoveBody(dt);
            MoveHat(dt);
            ShowFrame();

            if (bodyLanded && hatLanded && landedAt < 0f)
                landedAt = age;
            if ((landedAt >= 0f && age - landedAt > LingerAfterLanding) || age > MaxLifetime)
            {
                if (bodyLanded)
                    Finished?.Invoke(bodyPart.position);
                Destroy(gameObject);
            }
        }

        void MoveBody(float dt)
        {
            if (bodyLanded)
                return;
            Vector2 p = bodyPart.position;
            bodyVelocity.y -= BodyGravity * dt;
            Vector2 step = bodyVelocity * dt;

            // Duvara çarpınca yatay hız biter; yalnızca düşerken yere değer (tek yönlü platform dahil).
            if (Physics2D.Raycast(p + Vector2.up * 0.3f, new Vector2(Mathf.Sign(step.x), 0f), Mathf.Abs(step.x) + 0.3f, GameLayers.GroundMask))
                step.x = bodyVelocity.x = 0f;
            if (step.y < 0f && TryLand(p, -step.y, out float ground))
            {
                bodyPart.position = new Vector2(p.x + step.x, ground);
                bodyLanded = true;
                bodyLandedAt = age;
                if (groundFrames != null)
                    PixelBurst.Spawn(bodyPart.position, new Color32(0xB0, 0x92, 0x6A, 0xFF), 6, 2.5f, 0.35f); // yere çarpma tozu
                return;
            }
            bodyPart.position = p + step;
        }

        void MoveHat(float dt)
        {
            if (hatLanded)
                return;
            Vector2 p = hatPart.position;
            hatVelocityY = Mathf.MoveTowards(hatVelocityY, -HatFallSpeed, HatDrag * dt);
            float dy = hatVelocityY * dt;
            if (dy < 0f && TryLand(p, -dy, out float ground))
            {
                hatPart.position = new Vector2(p.x, ground);
                hatLanded = true;
                return;
            }
            float sway = Mathf.Sin(age * 3.5f) * HatSwayWidth * 0.5f;
            hatPart.position = new Vector2(hatStartX + sway, p.y + dy);
        }

        static bool TryLand(Vector2 position, float distance, out float ground)
        {
            RaycastHit2D hit = Physics2D.Raycast(position + Vector2.up * 0.05f, Vector2.down, distance + 0.05f, GameLayers.GroundMask);
            ground = hit ? hit.point.y : 0f;
            return hit;
        }
    }
}
