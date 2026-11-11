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

        [Header("Sarkan hat (isteğe bağlı)")]
        [Tooltip("Açıksa yatay giderken yüksekliği sarkan hattın eğrisinden alır (hattaki makara)")]
        [SerializeField] bool followWire;
        [SerializeField] Vector2 wireA, wireB;
        [SerializeField] float wireSag;

        float hangOffset;

        public void Configure(Vector2 travelBy, float peakSpeed, float startPhase)
        {
            travel = travelBy;
            speed = peakSpeed;
            phase = startPhase;
        }

        /// <summary>A'dan B'ye ortası <paramref name="sag"/> kadar sarkan hat; platform bu eğri boyunca kayar.</summary>
        public void FollowWire(Vector2 a, Vector2 b, float sag)
        {
            followWire = true;
            wireA = a;
            wireB = b;
            wireSag = sag;
        }

        /// <summary>Hattın x'teki yüksekliği (LevelBuilder.WirePoint ile aynı eğri).</summary>
        float WireY(float x)
        {
            float t = Mathf.InverseLerp(wireA.x, wireB.x, x);
            return Mathf.Lerp(wireA.y, wireB.y, t) - Mathf.Sin(t * Mathf.PI) * wireSag;
        }

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            // Başlangıç evresi varsa platform zaten yolun o noktasında dursun.
            start = transform.position;
            angularSpeed = speed * 2f / Mathf.Max(0.01f, travel.magnitude);
            if (followWire)
                hangOffset = start.y - WireY(start.x);
        }

        void FixedUpdate()
        {
            // Kendi saati: sahne geç yüklenince ilk adımda fırlayıp üstündekini savurmasın.
            elapsed += Time.fixedDeltaTime;
            float along = (1f - Mathf.Cos(angularSpeed * elapsed + phase * Mathf.PI * 2f)) / 2f;
            Vector2 target = start + travel * along;
            if (followWire)
                target.y = WireY(target.x) + hangOffset; // hat ortada sarkar: makara da onunla iner çıkar
            body.linearVelocity = (target - body.position) / Time.fixedDeltaTime;
        }
    }
}
