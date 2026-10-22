using DesertGecko.Player;
using UnityEngine;
using UnityEngine.UI;

namespace DesertGecko.UI
{
    /// <summary>Sağ üstteki kristal sayacı; her toplamada sayı kısa bir an büyüyüp küçülür.</summary>
    public class ScoreUI : MonoBehaviour
    {
        [SerializeField] PlayerWallet wallet;
        [SerializeField] Text label;
        [SerializeField] float popScale = 1.5f;
        [SerializeField] float popDuration = 0.2f;

        float popTime = float.PositiveInfinity;

        void OnEnable() => wallet.Changed += OnChanged;
        void OnDisable() => wallet.Changed -= OnChanged;
        void Start() => label.text = wallet.Crystals.ToString();

        void OnChanged(int crystals)
        {
            label.text = crystals.ToString();
            popTime = 0f;
        }

        void Update()
        {
            if (popTime > popDuration)
                return;
            popTime += Time.deltaTime;
            float scale = Mathf.Lerp(popScale, 1f, Mathf.Clamp01(popTime / popDuration));
            label.transform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
