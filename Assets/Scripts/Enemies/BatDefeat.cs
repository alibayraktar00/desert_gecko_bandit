using DesertGecko.Core;
using DesertGecko.Effects;
using UnityEngine;

namespace DesertGecko.Enemies
{
    /// <summary>
    /// Haydut Yarasa'nın yenilgisi: havada vurulur, kanatları kapanır ve takla atarak hızla yere çakılır.
    /// Çarpınca toz bulutu kalkar, yassı yatar, sonra toza karışıp kaybolur. Uçuruma düşerse sessizce silinir.
    /// Kareler (yarasa_64..71): 0-3 havada sarsılma/takla, 4-7 çarpma, toz ve yatış. Pivot kare altı = yer.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class BatDefeat : MonoBehaviour
    {
        const float Gravity = 30f, MaxFallSpeed = 18f, PopSpeed = 3f, ShockTime = 0.15f;
        const float FallFrameTime = 0.07f, LandFrameTime = 0.08f, LieTime = 0.7f, FadeTime = 0.4f, MaxLifetime = 5f;
        const float VisualHalfHeight = 0.55f; // yenilgi kareleri alta hizalı: gövde ortası pivotun ~18 px üstünde (32 PPU)

        Sprite[] frames;
        SpriteRenderer sr;
        Color dust;
        Vector2 velocity;
        float age, landedAt = -1f;
        bool dusted;

        /// <param name="center">Yarasanın ortası (EnemyBase transform'u).</param>
        public static BatDefeat Spawn(Vector2 center, Vector2 velocity, Sprite[] defeatFrames, bool flipX, int order, Color dustColor)
        {
            var effect = new GameObject("YarasaYenilgi").AddComponent<BatDefeat>();
            effect.transform.position = center + Vector2.down * VisualHalfHeight;
            effect.frames = defeatFrames;
            effect.dust = dustColor;
            // Vuruluşta kısa bir sıçrama, sonra yerçekimi: yatay hız neredeyse sıfırlanır, dimdik çakılır.
            effect.velocity = new Vector2(velocity.x * 0.2f, PopSpeed);
            effect.sr = effect.GetComponent<SpriteRenderer>();
            effect.sr.flipX = flipX;
            effect.sr.sortingOrder = order;
            if (defeatFrames != null && defeatFrames.Length > 0)
                effect.sr.sprite = defeatFrames[0];
            return effect;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            if (landedAt < 0f)
                Fall(dt);
            else
                Land();
            if (age > MaxLifetime && landedAt < 0f)
                Destroy(gameObject); // dipsiz uçuruma düştü
        }

        void Fall(float dt)
        {
            if (age > ShockTime) // ilk an havada asılı kalıp sarsılır, sonra düşer
                velocity.y = Mathf.Max(velocity.y - Gravity * dt, -MaxFallSpeed);
            Vector2 from = transform.position;
            Vector2 step = velocity * dt;
            if (velocity.y < 0f)
            {
                RaycastHit2D hit = Physics2D.Raycast(from + Vector2.up * 0.1f, Vector2.down, -step.y + 0.1f, GameLayers.GroundMask);
                if (hit.collider != null)
                {
                    transform.position = new Vector2(from.x + step.x, hit.point.y);
                    landedAt = age;
                    PixelBurst.Spawn(hit.point + Vector2.up * 0.1f, dust, 22, 4.5f, 0.6f);
                    PixelBurst.Spawn(hit.point + Vector2.up * 0.25f, new Color(dust.r * 0.75f, dust.g * 0.75f, dust.b * 0.75f), 12, 2.5f, 0.8f);
                    return;
                }
            }
            transform.position = from + step;
            if (frames != null && frames.Length >= 4)
                sr.sprite = frames[Mathf.FloorToInt(age / FallFrameTime) % 4];
        }

        void Land()
        {
            float t = age - landedAt;
            if (frames != null && frames.Length > 4)
                sr.sprite = frames[Mathf.Min(frames.Length - 1, 4 + Mathf.FloorToInt(t / LandFrameTime))];

            float fadeAge = t - (frames != null ? (frames.Length - 4) * LandFrameTime : 0f) - LieTime;
            if (fadeAge < 0f)
                return;
            if (!dusted)
            {
                dusted = true;
                PixelBurst.Spawn((Vector2)transform.position + Vector2.up * 0.2f, dust, 12, 2.5f, 0.6f);
            }
            Color c = sr.color;
            c.a = 1f - fadeAge / FadeTime;
            sr.color = c;
            if (fadeAge >= FadeTime)
                Destroy(gameObject);
        }
    }
}
