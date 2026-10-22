using UnityEngine;

namespace DesertGecko.Environment
{
    /// <summary>
    /// Arka plan katmanını kameraya göre kaydırır. Uzak katmanlar kamerayı daha çok takip eder,
    /// böylece daha yavaş kayıyormuş gibi görünür. İstenirse yatayda sonsuz tekrar eder.
    /// Konum sadece kamera konumundan hesaplanır, bu yüzden editörde de önizlenebilir.
    /// </summary>
    [DefaultExecutionOrder(100)] // CameraFollow kamerayı taşıdıktan sonra çalışsın
    [RequireComponent(typeof(SpriteRenderer))]
    public class ParallaxLayer : MonoBehaviour
    {
        [SerializeField] Transform cameraTransform;
        [Tooltip("Katmanın kamera hareketini ne kadar takip ettiği. 1 = kamerayla birlikte gider (gökyüzü), 0 = dünyaya sabit")]
        [SerializeField] Vector2 followFactor = new Vector2(0.5f, 0.5f);
        [Tooltip("Kamera bu noktadayken katman layerOrigin'de durur")]
        [SerializeField] Vector2 cameraOrigin;
        [SerializeField] Vector2 layerOrigin;
        [SerializeField] bool repeatX = true;
        [Tooltip("Yan yana kaç kopya çizilsin; ekranı her zaman kaplayacak kadar olmalı")]
        [SerializeField] int tileCount = 3;

        SpriteRenderer spriteRenderer;
        float tileWidth;

        void Awake()
        {
            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;
            Setup();
        }

        void LateUpdate()
        {
            if (cameraTransform != null)
                Follow(cameraTransform.position);
        }

        public void Setup()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (!repeatX || spriteRenderer.sprite == null)
                return;

            Vector2 spriteSize = spriteRenderer.sprite.bounds.size;
            tileWidth = spriteSize.x;
            spriteRenderer.drawMode = SpriteDrawMode.Tiled;
            spriteRenderer.size = new Vector2(tileWidth * tileCount, spriteSize.y);
        }

        public void Follow(Vector3 cameraPosition)
        {
            Vector2 delta = (Vector2)cameraPosition - cameraOrigin;
            float x = layerOrigin.x + delta.x * followFactor.x;
            float y = layerOrigin.y + delta.y * followFactor.y;

            // Katman kameradan bir kopya boyu uzaklaşınca bir kopya kaydır: ekran hiç boş kalmaz.
            if (repeatX && tileWidth > 0f)
                x += Mathf.Round((cameraPosition.x - x) / tileWidth) * tileWidth;

            transform.position = new Vector3(x, y, transform.position.z);
        }
    }
}
