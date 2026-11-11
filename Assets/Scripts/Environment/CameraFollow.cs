using DesertGecko.Effects;
using DesertGecko.Player;
using UnityEngine;

namespace DesertGecko.Environment
{
    /// <summary>
    /// Hedefi yumuşak takip eden 2D kamera; baktığı yöne doğru biraz önden gider. Ekran sarsıntısı (CameraShake)
    /// takip konumunun üstüne ayrıca eklenir: sarsıntı takibi bozmaz, bitince kamera kaldığı yerden sürer.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] Vector2 offset = new Vector2(0f, 1.5f);
        [SerializeField] float lookAhead = 2f;
        [SerializeField] float smoothTime = 0.18f;

        PlayerController player;
        Vector3 velocity;
        Vector3 followPosition;

        void Start()
        {
            followPosition = transform.position;
            if (target != null)
                player = target.GetComponent<PlayerController>();
        }

        void LateUpdate()
        {
            if (target == null)
                return;

            float ahead = player != null ? player.FacingDirection * lookAhead : 0f;
            Vector3 desired = new Vector3(target.position.x + offset.x + ahead, target.position.y + offset.y, transform.position.z);
            followPosition = Vector3.SmoothDamp(followPosition, desired, ref velocity, smoothTime);
            CameraShake.Sample(out Vector2 shake, out float angle);
            transform.position = followPosition + (Vector3)shake;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        public void SnapToTarget()
        {
            if (target == null)
                return;
            transform.position = new Vector3(target.position.x + offset.x, target.position.y + offset.y, transform.position.z);
            followPosition = transform.position;
            velocity = Vector3.zero;
            CameraShake.Clear();
        }
    }
}
