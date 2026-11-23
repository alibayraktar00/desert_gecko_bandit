using UnityEngine;

namespace DesertGecko.Environment
{
    /// <summary>
    /// Tren yolculuğunda dünyayla birlikte geriye kayan nesne: ray yatağı, telgraf direkleri, kaktüsler, peron.
    /// factor 1 ray hızında kayar; daha yakın (önde) olanlar 1'in üstünde, arkadakiler altında.
    /// wrapWidth verilirse [wrapMin, wrapMin + wrapWidth) aralığında sarar: sonsuz şerit. Verilmezse kayar gider,
    /// hideBelowX'in soluna geçince kapanır (istasyon geride kalır).
    /// </summary>
    public class WorldScroller : MonoBehaviour
    {
        [SerializeField] float factor = 1f;
        [SerializeField] float wrapMin;
        [SerializeField] float wrapWidth;
        [SerializeField] float hideBelowX = float.NegativeInfinity;

        float baseX;
        bool started;

        public void Configure(float scrollFactor, float wrapFrom, float wrapSize, float hideBelow = float.NegativeInfinity)
        {
            factor = scrollFactor;
            wrapMin = wrapFrom;
            wrapWidth = wrapSize;
            hideBelowX = hideBelow;
        }

        void Awake()
        {
            baseX = transform.position.x;
            started = true;
        }

        void LateUpdate()
        {
            if (!started)
                return;
            float x = baseX - TrainScroll.Offset * factor;
            if (wrapWidth > 0f)
                x = wrapMin + Mathf.Repeat(x - wrapMin, wrapWidth);
            Vector3 p = transform.position;
            transform.position = new Vector3(x, p.y, p.z);
            if (wrapWidth <= 0f && x < hideBelowX)
                gameObject.SetActive(false);
        }
    }
}
