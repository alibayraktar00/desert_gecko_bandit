using DesertGecko.Player;
using UnityEngine;

namespace DesertGecko.Environment
{
    /// <summary>Hedefi yumuşak takip eden 2D kamera; baktığı yöne doğru biraz önden gider.</summary>
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] Vector2 offset = new Vector2(0f, 1.5f);
        [SerializeField] float lookAhead = 2f;
        [SerializeField] float smoothTime = 0.18f;

        PlayerController player;
        Vector3 velocity;

        void Start()
        {
            if (target != null)
                player = target.GetComponent<PlayerController>();
        }

        void LateUpdate()
        {
            if (target == null)
                return;

            float ahead = player != null ? player.FacingDirection * lookAhead : 0f;
            Vector3 desired = new Vector3(target.position.x + offset.x + ahead, target.position.y + offset.y, transform.position.z);
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);
        }

        public void SnapToTarget()
        {
            if (target == null)
                return;
            transform.position = new Vector3(target.position.x + offset.x, target.position.y + offset.y, transform.position.z);
            velocity = Vector3.zero;
        }
    }
}
