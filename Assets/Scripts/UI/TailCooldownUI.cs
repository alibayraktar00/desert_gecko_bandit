using DesertGecko.Player;
using UnityEngine;
using UnityEngine.UI;

namespace DesertGecko.UI
{
    /// <summary>Kalplerin altındaki çember: kuyruk yeniden çıkana kadar dolar.</summary>
    public class TailCooldownUI : MonoBehaviour
    {
        [SerializeField] TailAttack tail;
        [Tooltip("Image Type = Filled, Radial 360")]
        [SerializeField] Image fill;
        [SerializeField] Color readyColor = new Color32(0xF4, 0xD3, 0x5E, 0xFF);
        [SerializeField] Color coolingColor = new Color(0.65f, 0.65f, 0.7f);

        void Update()
        {
            fill.fillAmount = tail.CooldownProgress;
            fill.color = tail.IsReady ? readyColor : coolingColor;
        }
    }
}
