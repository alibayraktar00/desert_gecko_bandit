using DesertGecko.Core;
using UnityEngine;

namespace DesertGecko.Enemies
{
    /// <summary>
    /// Çakalın komik yenilgisi: gövde geriye doğru uçup yere kapaklanır, dev sombrero havada asılı kalıp
    /// sallanarak yavaşça düşer. Düşman kapanmış olduğu için kendi başına yaşar ve süresi bitince silinir.
    /// </summary>
    public class JackalDefeat : MonoBehaviour
    {
        const float BodyGravity = 22f, HatPopSpeed = 4.5f, HatDrag = 9f, HatFallSpeed = 1.1f, HatSwayWidth = 1.6f;
        const float LingerAfterLanding = 1.2f, MaxLifetime = 6f;

        Transform bodyPart, hatPart;
        Vector2 bodyVelocity;
        float hatVelocityY, hatStartX, age, landedAt = -1f;
        bool bodyLanded, hatLanded;

        public static JackalDefeat Spawn(Vector2 position, Sprite defeat, Sprite hat, bool flipX, int order, int knockDirection)
        {
            var effect = new GameObject("CakalYenilgi").AddComponent<JackalDefeat>();
            effect.bodyPart = Part(effect.transform, "Govde", defeat, flipX, order, position);
            effect.hatPart = Part(effect.transform, "Sapka", hat, flipX, order + 1, position + Vector2.up * 0.95f);
            effect.bodyVelocity = new Vector2(knockDirection * 7f, 6f);
            effect.hatVelocityY = HatPopSpeed;
            effect.hatStartX = position.x;
            return effect;
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

            if (bodyLanded && hatLanded && landedAt < 0f)
                landedAt = age;
            if ((landedAt >= 0f && age - landedAt > LingerAfterLanding) || age > MaxLifetime)
                Destroy(gameObject);
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
