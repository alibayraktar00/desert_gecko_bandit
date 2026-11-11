using DesertGecko.Player;
using UnityEngine;

namespace DesertGecko.Environment
{
    /// <summary>
    /// Alevin kendisi: Gila ateşe dokunursa yarım kalp yanar ve ateşten uzağa savrulur.
    /// Isınmak için yanında durmak yeter (WarmthSource ayrı ve daha geniş); içine girmek can yakar.
    /// Hasardan sonraki dokunulmazlık süresi tekrar tekrar yanmayı önler.
    ///
    /// onlyFromAbove (ateş sunağı): yalnız tepesine basan yanar. Sunağın sepeti tek yönlü bir platform;
    /// alttan zıplayıp içinden geçerken ya da direğe yandan çarparken can gitmez, üstüne inip durunca
    /// her seferinde yarım kalp gider.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class FireHazard : MonoBehaviour
    {
        [Tooltip("Yarım kalp = 1")]
        [SerializeField] int damage = 1;
        [Tooltip("Yalnız yüzeyin üstünde duran (ya da üstüne inen) Gila yanar")]
        [SerializeField] bool onlyFromAbove;
        [Tooltip("onlyFromAbove için basılan yüzeyin yüksekliği, bu nesneye göre")]
        [SerializeField] float surfaceOffset;

        public void Configure(int burnDamage, bool fromAboveOnly = false, float surfaceHeight = 0f)
        {
            damage = burnDamage;
            onlyFromAbove = fromAboveOnly;
            surfaceOffset = surfaceHeight;
        }

        void Awake() => GetComponent<Collider2D>().isTrigger = true;

        void OnTriggerEnter2D(Collider2D other) => Burn(other);
        void OnTriggerStay2D(Collider2D other) => Burn(other);

        void Burn(Collider2D other)
        {
            var health = other.GetComponentInParent<PlayerHealth>();
            if (health == null || !health.IsAlive || health.IsInvincible)
                return;
            if (onlyFromAbove && !StandsOnTop(health))
                return;
            float side = Mathf.Sign(health.transform.position.x - transform.position.x);
            health.TakeDamage(damage, new Vector2(side == 0f ? 1f : side, 0f));
        }

        /// <summary>Ayakları yüzeyde ya da üstünde ve yükselmiyor: alttan geçerken sayılmaz.</summary>
        bool StandsOnTop(PlayerHealth health)
        {
            float feet = health.transform.position.y; // Gila'nın pivotu ayaklarında
            if (feet < transform.position.y + surfaceOffset - 0.12f)
                return false;
            var body = health.GetComponent<Rigidbody2D>();
            return body == null || body.linearVelocity.y <= 0.1f;
        }
    }
}
