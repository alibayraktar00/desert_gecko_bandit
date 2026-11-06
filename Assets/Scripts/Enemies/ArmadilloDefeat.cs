using DesertGecko.Effects;
using UnityEngine;

namespace DesertGecko.Enemies
{
    /// <summary>Armadillonun yenilgisi: yere kapaklanır, kısa süre yatar, maden tozuna karışıp kaybolur.</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class ArmadilloDefeat : MonoBehaviour
    {
        const float FrameTime = 0.09f, LieTime = 0.6f, FadeTime = 0.4f;

        Sprite[] frames;
        SpriteRenderer sr;
        Color dust;
        float age;
        bool dusted;

        public static ArmadilloDefeat Spawn(Vector2 position, Sprite[] defeatFrames, bool flipX, int order, Color dustColor)
        {
            var effect = new GameObject("ArmadilloYenilgi").AddComponent<ArmadilloDefeat>();
            effect.transform.position = position;
            effect.frames = defeatFrames;
            effect.dust = dustColor;
            effect.sr = effect.GetComponent<SpriteRenderer>();
            effect.sr.flipX = flipX;
            effect.sr.sortingOrder = order;
            if (defeatFrames != null && defeatFrames.Length > 0)
                effect.sr.sprite = defeatFrames[0];
            return effect;
        }

        void Update()
        {
            age += Time.deltaTime;
            float animTime = frames != null ? frames.Length * FrameTime : 0f;
            if (frames != null && frames.Length > 0)
                sr.sprite = frames[Mathf.Min(frames.Length - 1, Mathf.FloorToInt(age / FrameTime))];

            float fadeAge = age - animTime - LieTime;
            if (fadeAge < 0f)
                return;
            if (!dusted)
            {
                dusted = true;
                PixelBurst.Spawn((Vector2)transform.position + Vector2.up * 0.3f, dust, 16, 3f, 0.6f);
            }
            Color c = sr.color;
            c.a = 1f - fadeAge / FadeTime;
            sr.color = c;
            if (fadeAge >= FadeTime)
                Destroy(gameObject);
        }
    }
}
