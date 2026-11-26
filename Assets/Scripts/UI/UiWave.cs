using UnityEngine;
using UnityEngine.UI;

namespace DesertGecko.UI
{
    /// <summary>
    /// Bir UI görselini rüzgârda dalgalanan kumaş gibi çizer: düz dörtgen dikey şeritlere bölünür, her şerit sinüs
    /// dalgasıyla yukarı aşağı kayar. Sol uç sabittir (silaha bağlı), genlik sağa doğru artar. Reveal 0..1 ile kumaş
    /// soldan sağa açılır (sağ kısmı kırpılır). Kaymalar piksel ızgarasına yuvarlanır, piksel sanatı bozulmaz.
    /// Zaman ölçeksizdir (duraklatma menüsünde de oynar). Yalnızca Simple tipli Image ile kullanılır.
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    public class UiWave : BaseMeshEffect
    {
        [Tooltip("Dikey şerit sayısı (fazlası daha yumuşak dalga)")]
        [SerializeField] int segments = 32;
        [Tooltip("Sağ uçtaki en büyük kayma (kanvas birimi)")]
        [SerializeField] float amplitude = 4f;
        [Tooltip("Dalga boyu (kanvas birimi)")]
        [SerializeField] float wavelength = 150f;
        [Tooltip("Dalganın akış hızı (radyan/saniye); pozitif = sağa akar")]
        [SerializeField] float speed = 5f;
        [Tooltip("Kaymaların yuvarlandığı ızgara (kanvas birimi; 1 = bir piksel)")]
        [SerializeField] float pixelSnap = 1f;
        [Range(0f, 1f)] [SerializeField] float reveal = 1f;

        float time;

        /// <summary>0: tamamen kapalı (yalnızca sol uç), 1: tamamen açık.</summary>
        public float Reveal
        {
            get => reveal;
            set
            {
                value = Mathf.Clamp01(value);
                if (Mathf.Approximately(value, reveal))
                    return;
                reveal = value;
                graphic.SetVerticesDirty();
            }
        }

        /// <summary>Kumaşın x01 (0 sol, 1 sağ) noktasındaki o anki dikey kayması: üstüne konan yazı da bununla oynar.</summary>
        public float OffsetAt(float x01)
        {
            float width = ((RectTransform)transform).rect.width;
            float envelope = Mathf.SmoothStep(0f, 1f, x01); // sol uç sabit
            float y = amplitude * envelope * Mathf.Sin(x01 * width / wavelength * Mathf.PI * 2f - time * speed);
            return pixelSnap > 0f ? Mathf.Round(y / pixelSnap) * pixelSnap : y;
        }

        void Update()
        {
            if (!IsActive())
                return;
            time += Time.unscaledDeltaTime;
            graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount != 4)
                return;
            UIVertex bl = default, tl = default, tr = default, br = default;
            vh.PopulateUIVertex(ref bl, 0);
            vh.PopulateUIVertex(ref tl, 1);
            vh.PopulateUIVertex(ref tr, 2);
            vh.PopulateUIVertex(ref br, 3);
            vh.Clear();
            if (reveal <= 0f)
                return;

            int n = Mathf.Max(1, segments);
            for (int i = 0; i <= n; i++)
            {
                float x01 = reveal * i / n;
                float dy = OffsetAt(x01);
                UIVertex bottom = Lerp(bl, br, x01), top = Lerp(tl, tr, x01);
                bottom.position.y += dy;
                top.position.y += dy;
                vh.AddVert(bottom);
                vh.AddVert(top);
                if (i > 0)
                {
                    int b = i * 2;
                    vh.AddTriangle(b - 2, b - 1, b + 1);
                    vh.AddTriangle(b + 1, b, b - 2);
                }
            }
        }

        static UIVertex Lerp(UIVertex a, UIVertex b, float t)
        {
            UIVertex v = a;
            v.position = Vector3.Lerp(a.position, b.position, t);
            v.uv0 = Vector4.Lerp(a.uv0, b.uv0, t);
            v.color = Color32.Lerp(a.color, b.color, t);
            return v;
        }
    }
}
