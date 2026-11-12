using DesertGecko.Player;
using UnityEngine;

namespace DesertGecko.Environment
{
    /// <summary>
    /// Değen oyuncuyu anında öldüren görünmez bölge: duvarı ve rafların altını saran dikenli sarmaşık, sarı lav.
    /// Oyuncu son checkpoint'te yeniden doğar.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class DeadlyZone : MonoBehaviour
    {
        void Awake()
        {
            GetComponent<BoxCollider2D>().isTrigger = true;
        }

        void OnTriggerEnter2D(Collider2D other) => Touch(other);

        // Dokunulmazlıkla girip içinde kalan da ölsün.
        void OnTriggerStay2D(Collider2D other) => Touch(other);

        static void Touch(Collider2D other)
        {
            var health = other.GetComponentInParent<PlayerHealth>();
            if (health != null && health.IsAlive)
                health.Kill();
        }
    }
}
