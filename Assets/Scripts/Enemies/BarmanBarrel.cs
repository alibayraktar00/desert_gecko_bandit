using DesertGecko.Core;
using DesertGecko.Effects;
using DesertGecko.Player;
using UnityEngine;

namespace DesertGecko.Enemies
{
    /// <summary>
    /// Barmenin fırlattığı viski fıçısı: havadan yay çizerek, dönerek uçar. Zemine, duvara ya da Gila'ya değince
    /// parçalanır; parçalanma alanındaki Gila can kaybeder. Kırılma kareleri olduğu yerde oynar, sonra silinir.
    /// Rigidbody kullanmaz, BatDynamite gibi her karede katettiği yolu denetler.
    /// </summary>
    public class BarmanBarrel : MonoBehaviour
    {
        [SerializeField] int damage = 2;
        [SerializeField] float gravity = 18f;
        [SerializeField] float maxFallSpeed = 16f;
        [Tooltip("Çarpışma yarıçapı (fıçının gövdesi)")]
        [SerializeField] float radius = 0.4f;
        [Tooltip("Parçalanınca bu yarıçaptaki Gila hasar alır")]
        [SerializeField] float hitRadius = 1.3f;
        [SerializeField] float lifetime = 4f;
        [SerializeField] float spinSpeed = 420f;
        [Tooltip("Dönen eksen (fıçının ortası)")]
        [SerializeField] Transform visual;
        [SerializeField] SpriteRenderer spriteRenderer;
        [Tooltip("Parçalanma kareleri: alt orta pivotlu, zemine oturur")]
        [SerializeField] Sprite[] breakFrames;
        [SerializeField] float breakFps = 12f;
        [SerializeField] AudioClip breakClip;
        [SerializeField] Color chipColor = new Color32(0x8A, 0x5A, 0x34, 0xFF);

        readonly RaycastHit2D[] hits = new RaycastHit2D[4];
        readonly Collider2D[] blastHits = new Collider2D[4];
        ContactFilter2D filter;
        Vector2 velocity;
        float removeAt, brokenAt = -1f;
        int spinDirection = -1;

        public float Gravity => gravity;

        void Awake()
        {
            filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = GameLayers.GroundMask | GameLayers.PlayerMask,
                useTriggers = false,
            };
            removeAt = Time.time + lifetime;
            PlayerHealth.PlayerRespawned += Remove; // oyuncu doğunca havada fıçı kalmasın
        }

        void OnDestroy()
        {
            PlayerHealth.PlayerRespawned -= Remove;
        }

        /// <param name="startVelocity">Elden çıktığı andaki hız.</param>
        /// <param name="facing">Atanın baktığı yön; fıçı o yöne yuvarlanarak döner.</param>
        public void Launch(Vector2 startVelocity, int facing)
        {
            velocity = startVelocity;
            spinDirection = facing >= 0 ? -1 : 1;
            removeAt = Time.time + lifetime;
        }

        void Update()
        {
            if (brokenAt >= 0f)
            {
                PlayBreak();
                return;
            }
            if (Time.time >= removeAt)
            {
                Remove();
                return;
            }

            float dt = Time.deltaTime;
            velocity.y = Mathf.Max(velocity.y - gravity * dt, -maxFallSpeed);
            Vector2 from = transform.position;
            Vector2 step = velocity * dt;
            int count = step.sqrMagnitude > 0f ? Physics2D.CircleCast(from, radius, step.normalized, filter, hits, step.magnitude) : 0;
            for (int i = 0; i < count; i++)
            {
                // Tek yönlü platformun içinden yukarı doğru geçer, üstüne düşerken kırılır.
                if (hits[i].collider.usedByEffector && velocity.y > 0f)
                    continue;
                Break(hits[i].centroid, hits[i].point);
                return;
            }
            transform.position = from + step;
            if (visual != null)
                visual.Rotate(0f, 0f, spinDirection * spinSpeed * dt);
        }

        void Break(Vector2 center, Vector2 contact)
        {
            brokenAt = Time.time;
            transform.position = center;
            PixelBurst.Spawn(contact, chipColor, 10, 5f, 0.45f); // kıymıklar
            CameraShake.AddAt(center, 0.35f);
            OneShotSound.Play(breakClip);

            var playerFilter = new ContactFilter2D { useLayerMask = true, layerMask = GameLayers.PlayerMask, useTriggers = false };
            int count = Physics2D.OverlapCircle(center, hitRadius, playerFilter, blastHits);
            for (int i = 0; i < count; i++)
            {
                var player = blastHits[i].GetComponentInParent<PlayerHealth>();
                if (player == null)
                    continue;
                float side = Mathf.Abs(velocity.x) > 0.1f ? Mathf.Sign(velocity.x) : Mathf.Sign(player.transform.position.x - center.x);
                player.TakeDamage(damage, new Vector2(side == 0f ? 1f : side, 0f));
                break;
            }

            if (breakFrames == null || breakFrames.Length == 0 || spriteRenderer == null)
            {
                Remove();
                return;
            }
            // Kırılma kareleri dik durur ve fıçının tabanına oturur.
            if (visual != null)
                visual.rotation = Quaternion.identity;
            spriteRenderer.transform.localPosition = new Vector3(0f, -radius, 0f);
            PlayBreak();
        }

        void PlayBreak()
        {
            int frame = Mathf.FloorToInt((Time.time - brokenAt) * breakFps);
            if (frame >= breakFrames.Length)
            {
                Remove();
                return;
            }
            spriteRenderer.sprite = breakFrames[frame];
        }

        void Remove()
        {
            if (this != null)
                Destroy(gameObject);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.7f, 0.45f, 0.2f, 0.7f);
            Gizmos.DrawWireSphere(transform.position, hitRadius);
        }
    }
}
