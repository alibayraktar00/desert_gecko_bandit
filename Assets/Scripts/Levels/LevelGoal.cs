using DesertGecko.Effects;
using DesertGecko.Player;
using UnityEngine;

namespace DesertGecko.Levels
{
    /// <summary>Levelin bitiş noktası (aranıyor posteri direği). Oyuncu dokununca level tamamlanır.</summary>
    [RequireComponent(typeof(Collider2D))]
    public class LevelGoal : MonoBehaviour
    {
        [Tooltip("Rüzgârda sallanan bayrak (alt obje)")]
        [SerializeField] Transform flag;
        [SerializeField] float waveSpeed = 4f;
        [SerializeField] float waveAngle = 6f;
        [SerializeField] Color burstColor = new Color32(0xF4, 0xD3, 0x5E, 0xFF);

        bool reached;

        void Update()
        {
            if (flag != null)
                flag.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * waveSpeed) * waveAngle);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (reached)
                return;
            var health = other.GetComponentInParent<PlayerHealth>();
            if (health == null || !health.IsAlive)
                return;

            // Bitiş yalnızca level gerçekten tamamlandıysa kullanılmış sayılır.
            if (LevelManager.Current == null || !LevelManager.Current.CompleteLevel())
                return;
            reached = true;
            PixelBurst.Spawn((Vector2)transform.position + Vector2.up * 2f, burstColor, 24, 6f, 0.9f);
        }
    }
}
