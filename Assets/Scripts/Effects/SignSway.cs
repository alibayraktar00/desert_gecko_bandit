using UnityEngine;

namespace DesertGecko.Effects
{
    /// <summary>
    /// Zincirden asılı tabelayı rüzgârda hafifçe sallar. Nesnenin kendisi asılma noktasıdır (zincirlerin tepesi);
    /// tabela görseli bunun altında çocuk olarak durur. İki farklı hızdaki salınım üst üste biner, böylece hareket
    /// mekanik bir sarkaç gibi değil, ara ara esen rüzgâr gibi görünür. Menü duraklatılsa da çalışsın diye ölçeksiz zaman.
    /// </summary>
    public class SignSway : MonoBehaviour
    {
        [Tooltip("En büyük sallanma açısı (derece)")]
        [SerializeField] float angle = 2.5f;
        [Tooltip("Ana salınımın bir tam turu (saniye)")]
        [SerializeField] float period = 3.2f;
        [Tooltip("Rüzgâr esintisi: ikinci, yavaş salınımın gücü (0 = yok)")]
        [SerializeField] float gust = 0.6f;

        float phase;

        void Awake()
        {
            phase = Random.value * 10f;
        }

        void Update()
        {
            float t = Time.unscaledTime + phase;
            float main = Mathf.Sin(t * Mathf.PI * 2f / period);
            float slow = Mathf.Sin(t * Mathf.PI * 2f / (period * 2.7f));
            float amount = angle * main * (1f - gust * 0.5f + gust * 0.5f * slow);
            transform.localRotation = Quaternion.Euler(0f, 0f, amount);
        }
    }
}
