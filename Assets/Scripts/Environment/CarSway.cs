using UnityEngine;

namespace DesertGecko.Environment
{
    /// <summary>
    /// Vagonun görseli ray eklerinde hafifçe zıplar ve yalpalar; çarpışma kutuları kıpırdamaz (Gila titremez).
    /// Piksel ızgarasına oturur: 1 piksellik adımlarla.
    /// </summary>
    public class CarSway : MonoBehaviour
    {
        [SerializeField] float amplitude = 1f / 32f;
        [SerializeField] float phase;

        Vector3 rest;

        void Awake() => rest = transform.localPosition;

        void LateUpdate()
        {
            float k = Mathf.Clamp01(TrainScroll.Speed / 12f);
            if (k <= 0f)
            {
                transform.localPosition = rest;
                return;
            }
            // Ray eki her ~1 birimde bir: hıza göre tıkırdar, iki dalga üst üste (düzensiz görünsün).
            float t = TrainScroll.Offset * 0.9f + phase;
            float wave = Mathf.Sin(t * Mathf.PI * 2f) * 0.6f + Mathf.Sin(t * 1.7f + phase * 3f) * 0.4f;
            float step = Mathf.Round(wave * k) * amplitude;
            transform.localPosition = rest + new Vector3(0f, step, 0f);
        }
    }
}
