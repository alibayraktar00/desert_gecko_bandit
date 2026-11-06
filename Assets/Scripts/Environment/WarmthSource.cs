using DesertGecko.Player;
using UnityEngine;

namespace DesertGecko.Environment
{
    /// <summary>Ateş, fener, mangal: yanındaki Gila'yı ısıtır. hot açıksa yer altının sıcağı gibi fazladan hız da verir.</summary>
    [RequireComponent(typeof(Collider2D))]
    public class WarmthSource : MonoBehaviour
    {
        [Tooltip("Saniyede kazanılan ısı (1 = tamamen dolu)")]
        [SerializeField] float heatPerSecond = 1.2f;
        [SerializeField] bool hot;

        public void Configure(float rate, bool isHot)
        {
            heatPerSecond = rate;
            hot = isHot;
        }

        void Awake() => GetComponent<Collider2D>().isTrigger = true;

        void OnTriggerStay2D(Collider2D other)
        {
            var heat = other.GetComponentInParent<BodyHeat>();
            if (heat == null)
                return;
            if (hot)
                heat.Overheat();
            else
                heat.WarmAt(heatPerSecond);
        }
    }
}
