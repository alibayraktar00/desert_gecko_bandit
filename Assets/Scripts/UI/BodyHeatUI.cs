using DesertGecko.Player;
using UnityEngine;
using UnityEngine.UI;

namespace DesertGecko.UI
{
    /// <summary>Kalplerin yanındaki ısı çubuğu: sıcakta turuncu, üşüdükçe maviye döner, donarken yanıp söner.</summary>
    public class BodyHeatUI : MonoBehaviour
    {
        [SerializeField] BodyHeat heat;
        [Tooltip("Image Type = Filled, Horizontal")]
        [SerializeField] Image fill;
        [SerializeField] Color warmColor = new Color32(0xF0, 0x8A, 0x32, 0xFF);
        [SerializeField] Color coldColor = new Color32(0x6E, 0xC4, 0xF0, 0xFF);
        [SerializeField] Color hotColor = new Color32(0xFF, 0xD8, 0x5E, 0xFF);

        void Update()
        {
            if (heat == null)
                return;
            fill.fillAmount = Mathf.Max(0.04f, heat.Heat);
            Color c = heat.IsHot ? hotColor : Color.Lerp(coldColor, warmColor, Mathf.InverseLerp(0.2f, 0.65f, heat.Heat));
            if (heat.IsFrozen)
                c.a = Mathf.PingPong(Time.time * 4f, 1f);
            fill.color = c;
        }
    }
}
