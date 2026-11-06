using DesertGecko.Effects;
using DesertGecko.Player;
using UnityEngine;

namespace DesertGecko.Collectibles
{
    /// <summary>Havada süzülen böcek. Sadece dil ile yakalanır, 1 kalp can yeniler. Gecede ısıtır da (ateş böceği).</summary>
    public class Bug : MonoBehaviour
    {
        [SerializeField] int healAmount = 2;
        [SerializeField] float heatAmount = 0.45f;
        [SerializeField] float hoverHeight = 0.12f;
        [SerializeField] float hoverSpeed = 4f;
        [SerializeField] Color burstColor = new Color32(0x7C, 0xC4, 0x4A, 0xFF);

        Vector3 restPosition;
        float phase;

        void Awake()
        {
            restPosition = transform.position;
            phase = Random.value * 10f;
        }

        void Update()
        {
            float t = Time.time * hoverSpeed + phase;
            transform.position = restPosition + new Vector3(Mathf.Sin(t * 0.5f) * hoverHeight, Mathf.Sin(t) * hoverHeight, 0f);
        }

        /// <returns>Yakalandıysa true.</returns>
        public bool Catch(PlayerHealth health)
        {
            if (!gameObject.activeSelf)
                return false;
            health.Heal(healAmount);
            var heat = health.GetComponent<BodyHeat>();
            if (heat != null)
                heat.AddHeat(heatAmount);
            PixelBurst.Spawn(transform.position, burstColor, 8, 3f, 0.35f);
            gameObject.SetActive(false);
            return true;
        }
    }
}
