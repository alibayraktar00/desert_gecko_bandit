using UnityEngine;

namespace DesertGecko.Environment
{
    /// <summary>Tavandan zemine düşen su damlası; konumu yalnızca zamandan hesaplanır. Çarpışmaz.</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class WaterDrip : MonoBehaviour
    {
        [SerializeField] float top;
        [SerializeField] float bottom;
        [SerializeField] float fallTime = 0.7f;
        [SerializeField] float waitTime = 1.6f;

        SpriteRenderer sr;
        float offset;

        public void Configure(float topY, float bottomY)
        {
            top = topY;
            bottom = bottomY;
        }

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            offset = transform.position.x % 1f;
        }

        void Update()
        {
            float cycle = (Time.time + offset * waitTime) % (fallTime + waitTime);
            sr.enabled = cycle < fallTime;
            if (!sr.enabled)
                return;
            float t = cycle / fallTime;
            Vector3 p = transform.position;
            p.y = Mathf.Lerp(top, bottom, t * t);
            transform.position = p;
        }
    }
}
