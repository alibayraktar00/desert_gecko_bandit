using UnityEngine;

namespace DesertGecko.Environment
{
    /// <summary>
    /// İki nokta arasında yumuşakça gidip gelen platform. Hızı gövdesindedir; PlayerController
    /// üstünde duran oyuncuya bunu ekler, böylece oyuncu platformla birlikte taşınır.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class MovingPlatform : MonoBehaviour
    {
        [Tooltip("Başlangıçtan gidilecek noktaya olan fark")]
        [SerializeField] Vector2 travel = new Vector2(6f, 0f);
        [Tooltip("Yolun ortasındaki en yüksek hız")]
        [SerializeField] float speed = 2f;
        [Tooltip("Başlangıç evresi (0-1): aynı yoldaki platformlar birbirini takip etsin")]
        [Range(0f, 1f)]
        [SerializeField] float phase;

        Rigidbody2D body;
        Vector2 start;
        float angularSpeed, elapsed;

        public void Configure(Vector2 travelBy, float peakSpeed, float startPhase)
        {
            travel = travelBy;
            speed = peakSpeed;
            phase = startPhase;
        }

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            // Başlangıç evresi varsa platform zaten yolun o noktasında dursun.
            start = transform.position;
            angularSpeed = speed * 2f / Mathf.Max(0.01f, travel.magnitude);
        }

        void FixedUpdate()
        {
            // Kendi saati: sahne geç yüklenince ilk adımda fırlayıp üstündekini savurmasın.
            elapsed += Time.fixedDeltaTime;
            float along = (1f - Mathf.Cos(angularSpeed * elapsed + phase * Mathf.PI * 2f)) / 2f;
            body.linearVelocity = (start + travel * along - body.position) / Time.fixedDeltaTime;
        }
    }
}
