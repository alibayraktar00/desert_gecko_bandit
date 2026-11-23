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
        Camera cam;
        float baseSize, sizeFrom, sizeTo, zoomStart, zoomDuration;
        Vector2? focus;
        float focusSmooth;

        void Start()
        {
            followPosition = transform.position;
            if (target != null)
                player = target.GetComponent<PlayerController>();
            cam = GetComponent<Camera>();
            if (cam != null)
                baseSize = sizeFrom = sizeTo = cam.orthographicSize;
        }

        /// <summary>Ara sahne: kamera oyuncu yerine bu noktaya kayar. smooth: varış süresi (sn, gerçek zaman).</summary>
        public void SetFocus(Vector2 point, float smooth = 0.6f)
        {
            focus = point;
            focusSmooth = smooth;
        }

        public void ClearFocus() => focus = null;

        /// <summary>Görüş alanını yumuşakça değiştirir; size boyunun yarısıdır (birim). Gerçek zamanla işler.</summary>
        public void ZoomTo(float size, float duration)
        {
            if (cam == null)
                return;
            sizeFrom = cam.orthographicSize;
            sizeTo = size;
            zoomStart = Time.unscaledTime;
            zoomDuration = Mathf.Max(0.0001f, duration);
        }

        /// <summary>Oyunun normal görüş alanına anında döner.</summary>
        public void ResetZoom()
        {
            if (cam == null)
                return;
            sizeFrom = sizeTo = baseSize;
            cam.orthographicSize = baseSize;
        }

        void LateUpdate()
        {
            if (target == null)
                return;

            if (cam != null && !Mathf.Approximately(cam.orthographicSize, sizeTo))
            {
                float k = Mathf.Clamp01((Time.unscaledTime - zoomStart) / zoomDuration);
                cam.orthographicSize = Mathf.Lerp(sizeFrom, sizeTo, k * k * (3f - 2f * k));
            }

            float ahead = player != null ? player.FacingDirection * lookAhead : 0f;
            Vector3 desired = new Vector3(target.position.x + offset.x + ahead, target.position.y + offset.y, transform.position.z);
            if (focus.HasValue)
            {
                desired = new Vector3(focus.Value.x, focus.Value.y, transform.position.z);
                followPosition = Vector3.SmoothDamp(followPosition, desired, ref velocity, focusSmooth, Mathf.Infinity, Time.unscaledDeltaTime);
            }
            else
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
