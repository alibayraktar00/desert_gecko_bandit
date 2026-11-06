using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DesertGecko.Environment
{
    /// <summary>Ateş ve fener ışığının hafif titremesi.</summary>
    [RequireComponent(typeof(Light2D))]
    public class LightFlicker : MonoBehaviour
    {
        [SerializeField] float amount = 0.12f;
        [SerializeField] float speed = 7f;

        Light2D target;
        float baseIntensity, seed;

        public void Configure(float flicker) => amount = flicker;

        void Awake()
        {
            target = GetComponent<Light2D>();
            baseIntensity = target.intensity;
            seed = Random.value * 50f;
        }

        void Update()
        {
            target.intensity = baseIntensity * (1f + (Mathf.PerlinNoise(seed, Time.time * speed) - 0.5f) * 2f * amount);
        }
    }
}
