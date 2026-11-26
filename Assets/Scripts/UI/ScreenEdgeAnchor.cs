using UnityEngine;

namespace DesertGecko.UI
{
    /// <summary>
    /// Ortografik kamerada nesneyi ekranın bir kenarına yapıştırır; ekran oranı değişse de (16:9, 16:10, ultra geniş)
    /// sol menü solda, sağdaki karakter sağda kalır. Kapla modunda ise görseli kamerayı boşluk bırakmadan
    /// kaplayacak kadar büyütür (arka plan). Konum her karede değil, ekran boyutu değişince güncellenir.
    /// </summary>
    [ExecuteAlways]
    public class ScreenEdgeAnchor : MonoBehaviour
    {
        public enum Edge { Left, Right, Center }

        [Tooltip("Hangi kenara yapışır (yatayda); dikey konum değişmez")]
        [SerializeField] Edge edge = Edge.Left;
        [Tooltip("Kenardan içeri uzaklık (dünya birimi); sağ kenarda sola doğru")]
        [SerializeField] float inset;
        [Tooltip("Görseli kamerayı kaplayacak kadar büyüt (arka plan için)")]
        [SerializeField] bool cover;
        [SerializeField] Camera targetCamera;

        Vector2Int lastScreen;
        float lastAspect;
        Vector3 baseScale;
        bool scaleKnown;

        public void Configure(Edge anchorEdge, float edgeInset, bool coverScreen, Camera cam)
        {
            edge = anchorEdge;
            inset = edgeInset;
            cover = coverScreen;
            targetCamera = cam;
            Apply();
        }

        void OnEnable()
        {
            lastScreen = Vector2Int.zero;
            Apply();
        }

        void LateUpdate()
        {
            Camera cam = targetCamera != null ? targetCamera : Camera.main;
            if (cam == null)
                return;
            if (lastScreen.x != Screen.width || lastScreen.y != Screen.height || !Mathf.Approximately(lastAspect, cam.aspect))
                Apply();
        }

        void Apply()
        {
            Camera cam = targetCamera != null ? targetCamera : Camera.main;
            if (cam == null || !cam.orthographic)
                return;
            lastScreen = new Vector2Int(Screen.width, Screen.height);
            lastAspect = cam.aspect;
            float halfH = cam.orthographicSize, halfW = halfH * cam.aspect;
            Vector3 c = cam.transform.position;
            Vector3 p = transform.position;
            p.x = edge == Edge.Left ? c.x - halfW + inset : edge == Edge.Right ? c.x + halfW - inset : c.x;
            transform.position = p;

            if (!cover)
                return;
            if (!scaleKnown)
            {
                baseScale = transform.localScale;
                scaleKnown = true;
            }
            var sr = GetComponent<SpriteRenderer>();
            if (sr == null || sr.sprite == null)
                return;
            Vector2 size = sr.sprite.bounds.size;
            float k = Mathf.Max(1f, Mathf.Max(2f * halfW / (size.x * baseScale.x), 2f * halfH / (size.y * baseScale.y)));
            transform.localScale = new Vector3(baseScale.x * k, baseScale.y * k, baseScale.z);
        }
    }
}
