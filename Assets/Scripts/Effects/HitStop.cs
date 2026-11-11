using System.Collections;
using UnityEngine;

namespace DesertGecko.Effects
{
    /// <summary>
    /// Vuruş anı duraklaması: ağır darbelerde (patlama, kazma, silahla öldürme) oyun bir an neredeyse durur,
    /// darbenin ağırlığı hissedilir. Üst üste gelenler en uzun olana uzar. Duraklatma menüsü zamanı durdurduysa
    /// ona dokunmaz.
    /// </summary>
    public class HitStop : MonoBehaviour
    {
        const float SlowScale = 0.05f;
        const float MaxDuration = 0.12f;

        static HitStop runner;
        float until;
        bool active;

        /// <summary>Gerçek zamanla <paramref name="duration"/> saniye (en fazla 0,12) neredeyse dur.</summary>
        public static void Request(float duration)
        {
            if (duration <= 0f || Time.timeScale <= 0f)
                return;
            if (runner == null)
            {
                var go = new GameObject("HitStop") { hideFlags = HideFlags.HideInHierarchy };
                DontDestroyOnLoad(go);
                runner = go.AddComponent<HitStop>();
            }
            runner.Begin(Mathf.Min(duration, MaxDuration));
        }

        void Begin(float duration)
        {
            until = Mathf.Max(until, Time.unscaledTime + duration);
            if (active)
                return;
            if (!Mathf.Approximately(Time.timeScale, 1f))
                return; // ağır çekim ya da menü: karışma
            active = true;
            Time.timeScale = SlowScale;
            StartCoroutine(Release());
        }

        IEnumerator Release()
        {
            while (Time.unscaledTime < until)
                yield return null;
            if (Mathf.Approximately(Time.timeScale, SlowScale))
                Time.timeScale = 1f; // bu arada menü açıldıysa onun zamanına dokunma
            active = false;
        }
    }
}
