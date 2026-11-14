using System.Collections.Generic;
using UnityEngine;

namespace DesertGecko.Environment
{
    /// <summary>
    /// Eğimli yüzey (çatı, merdiven): iki uç arasındaki görünmez çizgi. Gila üstüne inince çizgiye oturur ve yürüdükçe
    /// onu izler: çapraz çıkılır, çapraz inilir. Çarpışması yoktur, tek yönlü platform gibi davranır: alttan geçilir,
    /// S + Space ile inilir. Fizik çarpıştırıcısı olmadığı için duvar algılaması rampaya yapışmaz.
    /// </summary>
    public class RoofSlope : MonoBehaviour
    {
        static readonly List<RoofSlope> All = new List<RoofSlope>();

        [SerializeField] Vector2 from, to;

        public void Configure(Vector2 a, Vector2 b)
        {
            if (a.x <= b.x)
            {
                from = a;
                to = b;
            }
            else
            {
                from = b;
                to = a;
            }
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        /// <summary>x'teki yüzey yüksekliği; çizginin dışındaysa false.</summary>
        public bool HeightAt(float x, out float y)
        {
            y = 0f;
            if (x < from.x || x > to.x)
                return false;
            y = Mathf.Lerp(from.y, to.y, Mathf.InverseLerp(from.x, to.x, x));
            return true;
        }

        /// <summary>
        /// Ayakların altındaki en yüksek eğim: ayaklar çizginin biraz üstünde (inerken) ya da biraz altında (çıkarken,
        /// hızlı düşerken) olabilir. ignore: S + Space ile inilen eğim.
        /// </summary>
        public static RoofSlope Under(float x, float feetY, float above, float below, RoofSlope ignore, out float surfaceY)
        {
            RoofSlope best = null;
            surfaceY = float.NegativeInfinity;
            foreach (RoofSlope slope in All)
            {
                if (slope == ignore || !slope.HeightAt(x, out float y))
                    continue;
                if (feetY > y + above || feetY < y - below || y <= surfaceY)
                    continue;
                best = slope;
                surfaceY = y;
            }
            return best;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.1f);
            Gizmos.DrawLine(from, to);
        }
    }
}
