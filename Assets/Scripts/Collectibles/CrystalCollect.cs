using UnityEngine;

namespace DesertGecko.Collectibles
{
    /// <summary>
    /// Altının toplanma anı: ışınlı patlama, ikiye kırılma, saçılıp seyrelen kırıntılar (son kare söner).
    /// Altın kaybolduğu yerde kendi başına oynar ve yok olur.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class CrystalCollect : MonoBehaviour
    {
        // Patlama biraz uzun kalsın, kırılma hızlı geçsin, son kırıntılar yavaş sönsün.
        static readonly float[] FrameTimes = { 0.08f, 0.07f, 0.08f, 0.16f };

        Sprite[] frames;
        SpriteRenderer sr;
        float age;

        public static void Spawn(Vector2 position, Sprite[] frames, int order, Material material)
        {
            if (frames == null || frames.Length == 0)
                return;
            var effect = new GameObject("ZumrutToplama").AddComponent<CrystalCollect>();
            effect.transform.position = position;
            effect.frames = frames;
            effect.sr = effect.GetComponent<SpriteRenderer>();
            effect.sr.sortingOrder = order + 1;
            if (material != null)
                effect.sr.sharedMaterial = material; // ışık patlaması gece de parlasın
            effect.sr.sprite = frames[0];
        }

        void Update()
        {
            age += Time.deltaTime;
            float t = age;
            for (int i = 0; i < frames.Length; i++)
            {
                float frameTime = FrameTimes[Mathf.Min(i, FrameTimes.Length - 1)];
                if (t < frameTime)
                {
                    sr.sprite = frames[i];
                    if (i == frames.Length - 1)
                    {
                        Color c = sr.color;
                        c.a = 1f - t / frameTime; // son kare (duman) söner
                        sr.color = c;
                    }
                    return;
                }
                t -= frameTime;
            }
            Destroy(gameObject);
        }
    }
}
