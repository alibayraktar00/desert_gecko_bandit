using DesertGecko.Effects;
using UnityEngine;

namespace DesertGecko.Collectibles
{
    /// <summary>
    /// Yakalanan böcek dilin ucunda Gila'nın ağzına çekilir, giderken kıvılcımlanıp küçülür.
    /// Ağza varınca yutulur: yutkunma/cızırtı sesi ve Gila'dan yayılan yeşil şifa halkası (HealRing).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class BugSwallow : MonoBehaviour
    {
        /// <summary>Ağza çekilme süresi; dil de aynı sürede, aynı eğriyle geri sarılır.</summary>
        public const float PullTime = 0.16f;

        /// <summary>Çekilme eğrisi (0..1): yavaş kopar, hızlanarak ağza girer.</summary>
        public static float PullCurve(float t) => t * t;

        Sprite[] frames;
        SpriteRenderer sr;
        Vector2 from, to;
        Transform eater;
        Sprite ringSprite;
        Color ringColor, sparkColor;
        AudioClip gulpClip;
        Vector3 eaterStart;
        Material unlit;
        float age;

        public static void Spawn(Vector2 position, Vector2 mouth, Transform eater, Sprite[] frames, bool flipX, int order,
            Sprite ringSprite, Color ringColor, Color sparkColor, AudioClip gulpClip, Material unlit = null)
        {
            var effect = new GameObject("BocekYutma").AddComponent<BugSwallow>();
            effect.transform.position = position;
            effect.from = position;
            effect.to = mouth;
            effect.eater = eater;
            effect.eaterStart = eater != null ? eater.position : Vector3.zero;
            effect.frames = frames;
            effect.ringSprite = ringSprite;
            effect.ringColor = ringColor;
            effect.sparkColor = sparkColor;
            effect.gulpClip = gulpClip;
            effect.unlit = unlit;
            effect.sr = effect.GetComponent<SpriteRenderer>();
            effect.sr.flipX = flipX;
            if (unlit != null)
                effect.sr.sharedMaterial = unlit;
            effect.sr.sortingOrder = order + 6; // Gila'nın önünde ağzına girsin
            if (frames != null && frames.Length > 0)
                effect.sr.sprite = frames[0];
        }

        void Update()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / PullTime);
            // Ağız Gila ile birlikte hareket eder: hedefi her kare güncelle.
            Vector2 target = eater != null ? to + (Vector2)(eater.position - eaterStart) : to;
            transform.position = Vector2.Lerp(from, target, PullCurve(t));
            transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.35f, t);
            if (frames != null && frames.Length > 0)
                sr.sprite = frames[Mathf.Min(frames.Length - 1, Mathf.FloorToInt(t * frames.Length))];

            if (t < 1f)
                return;
            PixelBurst.Spawn(transform.position, sparkColor, 5, 1.8f, 0.25f);
            OneShotSound.Play(gulpClip);
            if (eater != null)
                HealRing.Spawn(eater, ringSprite, ringColor, unlit);
            Destroy(gameObject);
        }
    }
}
