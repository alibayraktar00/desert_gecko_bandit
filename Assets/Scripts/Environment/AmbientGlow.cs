using UnityEngine;

namespace DesertGecko.Environment
{
    /// <summary>Fener, mantar ve kristal parıltısı: yumuşakça nefes alır, fenerlerde hafifçe titrer.</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class AmbientGlow : MonoBehaviour
    {
        [SerializeField] float pulseSpeed = 1.5f;
        [Range(0f, 1f)]
        [SerializeField] float pulseAmount = 0.25f;
        [Range(0f, 1f)]
        [SerializeField] float flicker;

        SpriteRenderer sr;
        Color baseColor;
        float seed;

        public void Configure(float speed, float amount, float flickerAmount)
        {
            pulseSpeed = speed;
            pulseAmount = amount;
            flicker = flickerAmount;
        }

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            baseColor = sr.color;
            seed = transform.position.x;
        }

        void Update()
        {
            float wave = Mathf.Sin(Time.time * pulseSpeed + seed) * pulseAmount;
            float noise = (Mathf.PerlinNoise(Time.time * 9f, seed) - 0.5f) * flicker;
            Color c = baseColor;
            c.a *= Mathf.Clamp01(1f + wave + noise);
            sr.color = c;
        }
    }
}
