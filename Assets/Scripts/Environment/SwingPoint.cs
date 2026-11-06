using System.Collections.Generic;
using UnityEngine;

namespace DesertGecko.Environment
{
    /// <summary>
    /// Dilin tutunabildiği kanca (telgraf hattına asılı fener). Gila menzildeyken halkası parlar; J ile yapışır,
    /// bırakınca savrulur. Hattın üstünde gidip gelen kancalarda taşınan nesnenin çocuğudur.
    /// </summary>
    public class SwingPoint : MonoBehaviour
    {
        static readonly List<SwingPoint> All = new List<SwingPoint>();

        [SerializeField] SpriteRenderer marker;

        public void Configure(SpriteRenderer targetMarker)
        {
            marker = targetMarker;
            if (marker != null)
                marker.enabled = false;
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        /// <summary>Menzilin içinde görüş hattı olan en iyi kanca: önündeki ve üstündeki, en yakın olan.</summary>
        public static SwingPoint FindBest(Vector2 mouth, int facing, float range, int blockMask, SwingPoint ignore = null)
        {
            SwingPoint best = null;
            float bestScore = float.PositiveInfinity;
            foreach (SwingPoint point in All)
            {
                if (point == ignore)
                    continue;
                Vector2 toPoint = (Vector2)point.transform.position - mouth;
                float distance = toPoint.magnitude;
                if (distance > range || toPoint.y < 0.3f)
                    continue; // altındaki kancaya yapışmak savrulma vermez
                float score = distance + (toPoint.x * facing < -0.5f ? 4f : 0f); // arkadaki ancak yakınsa seçilir
                if (score >= bestScore)
                    continue;
                if (Physics2D.Linecast(mouth, point.transform.position, blockMask))
                    continue;
                best = point;
                bestScore = score;
            }
            return best;
        }

        public void SetTargeted(bool targeted)
        {
            if (marker == null)
                return;
            marker.enabled = targeted;
            if (targeted)
                marker.transform.localScale = Vector3.one * (1f + Mathf.Sin(Time.time * 9f) * 0.12f);
        }
    }
}
